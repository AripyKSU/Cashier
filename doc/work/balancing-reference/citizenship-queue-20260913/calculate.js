// Offline mirror of current pure APIs. Runtime data is read and never written.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const R=require('../reputation-20260912/calculate.js'),C=require('../citizenship-20260913/calculate.js'),M=require('../draft-20260912/model.js');
const root=path.resolve(__dirname,'../../../..'),GOAL=1000000,RUNS=10000;
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex');
const read=file=>{const [h,...lines]=fs.readFileSync(path.join(root,file),'utf8').trim().replace(/^\uFEFF/,'').split(/\r?\n/);assert.ok(!lines.some(l=>l.includes('"')),'Only simple numeric CSV supported');return lines.map(l=>Object.fromEntries(l.split(',').map((v,i)=>[h.split(',')[i],v])));};
const files={products:'Assets/Datas/Customer/ProductData.csv',dispositions:'Assets/Datas/Customer/CustomerDispositionData.csv',facilities:'Assets/Datas/FacilityData.csv',reputation:'Assets/Datas/ReputationBalanceData.csv',maintenance:'Assets/Datas/MaintenanceBalanceData.csv',economy:'Assets/Datas/EconomyBalanceData.csv',events:'Assets/Datas/PriceEventData.csv',schedule:'Assets/Datas/PriceEventScheduleData.csv',guidelines:'Assets/Datas/DailyGuidelineData.csv'};
const T=Object.fromEntries(Object.entries(files).map(([k,f])=>[k,read(f)]));
// Shared classification/settlement reads historical tables; require current values to match.
for(const k of ['products','dispositions','reputation','maintenance','economy'])assert.deepEqual(T[k],R.I.tables[k],`Shared model input drift: ${k}`);
const products=T.products.map(p=>({...p,idx:+p.idx,type:+p.product_type,price:+p.base_price,facility:+p.required_facility_idx})).sort((a,b)=>a.idx-b.idx);
const byId=Object.fromEntries(products.map(p=>[p.idx,p]));
const facilities=Object.fromEntries(T.facilities.map(f=>[+f.idx,f])),costs=Object.fromEntries(T.facilities.map(f=>[+f.idx,+f.purchase_price]));
assert.deepEqual(costs,R.I.proposedCosts,'Applied prices differ from reviewed cost proposal');
const dispositions=T.dispositions.map(r=>({...r,idx:+r.idx,type:+r.disposition_type,patience:+r.queue_patience_seconds,types:r.preferred_product_types.split('_').filter(Boolean).map(Number),ids:r.preferred_product_idxs.split('_').filter(Boolean).map(Number)}));
const reps=T.reputation.map(r=>Object.fromEntries(Object.entries(r).map(([k,v])=>[k,+v])));
const typeOrder=[1,3,4,2,5],fields=['normal_weight','price_sensitive_weight','wealthy_weight','hasty_weight','poor_weight'];
const weights=[[160,100,45],[100,160,100],[100,120,180]],displayWeights=[100,220,280,350];
const sequence=[12001,12002,12008,12003,12004,12010,12005,12006];
const maintenance=T.maintenance.map(r=>+r.maintenanceAmount),initial=+T.economy[0].initialBalance;
const rng=(run,day,salt)=>M.rng(R.seed(run,day,salt));
function catalog(day,mask,random){
  const available=products.filter(p=>+p.is_available===1&&+p.available_day<=day-1&&(!p.facility||(mask&(1<<(p.facility-12001))))),out=[];
  const count=Math.min([4,6,8][M.phaseOf(day)],available.length);
  while(out.length<count){const p=R.pick(available,available.map(p=>displayWeights[p.facility?+facilities[p.facility].required_store_stage:0]),random());out.push(p);available.splice(available.indexOf(p),1);}
  return out.sort((a,b)=>a.idx-b.idx);
}
function customerFactory(day,rep,stock,random,attributeRandom){
  const band=reps.find(r=>rep>=r.min_reputation&&rep<=r.max_reputation),phase=M.phaseOf(day);
  const rowOptions=Object.fromEntries(typeOrder.map(type=>{const a=dispositions.filter(r=>r.type===type);return [type,{a,w:a.map(r=>r.types.length?r.types.reduce((s,t)=>s+weights[phase][t<=4?0:t<=6?1:2],0)/r.types.length:100)}];}));
  let gender=Math.floor(attributeRandom()*2),arrival=0;
  return ()=>{
    const type=R.pick(typeOrder,fields.map(k=>band[k]),random()),opt=rowOptions[type],row=R.pick(opt.a,opt.w,random());
    const yes=stock.filter(p=>row.types.includes(p.type)||row.ids.includes(p.idx)),no=stock.filter(p=>!yes.includes(p)),items=[];
    const max=Math.min(+row.max_product_kinds,stock.length),min=Math.min(+row.min_product_kinds,max),count=min+Math.floor(random()*(max-min+1));
    for(let k=0;k<count;k++){
      const usePreferred=yes.length&&(!no.length||random()<+row.preferred_selection_chance/1000),pool=usePreferred?yes:no;
      const p=pool.splice(Math.floor(random()*pool.length),1)[0];assert.ok(p);
      items.push({idx:p.idx,quantity:+row.min_quantity+Math.floor(random()*(+row.max_quantity-(+row.min_quantity)+1))});
    }
    const attributes=[(gender+arrival)%2?'female':'male',['adult','child','elderly'][Math.floor(attributeRandom()*3)]];
    return {row,items,attributes,id:arrival++};
  };
}
function serviceEnds(count,timing,random){
  const w=Array.from({length:count},()=>timing==='fixed'?1:.8+.4*random()),total=w.reduce((a,b)=>a+b,0);
  let time=0;const ends=w.map(v=>(time+=v/total*180));ends[count-1]=180;return ends;
}
// Event ordering mirrors CustomerQueue: expiry first, then 5s arrival, FIFO at service start.
function queueVisits(create,ends){
  let waiting=[],nextArrival=5,abandoned=0,generated=0;
  const add=time=>{if(waiting.length>=10)return;const visit=create();generated++;waiting.push({...visit,deadline:time+visit.row.patience});};
  const expire=time=>{const before=waiting.length;waiting=waiting.filter(v=>v.deadline>time);abandoned+=before-waiting.length;};
  const advance=end=>{while(nextArrival<=end){expire(nextArrival);add(nextArrival);nextArrival+=5;}expire(end);};
  add(0);const served=[];
  for(let i=0;i<ends.length;i++){const start=i?ends[i-1]:0;advance(start);const visit=waiting.shift();assert.ok(visit,'Supply gap requires a different throughput model');served.push(visit);}
  advance(180);assert.equal(generated,served.length+abandoned+waiting.length);
  return {served,abandoned,generated};
}
function daySample(day,mask,rep,count,queue,timing,run,{events=true,fines=true}={}){
  const stock=catalog(day,mask,rng(run,day,1301));
  const create=customerFactory(day,rep,stock,rng(run,day,1302),rng(run,day,1303));
  const ends=serviceEnds(count,timing,rng(run,day,1304));
  const visits=queue?queueVisits(create,ends):{served:Array.from({length:count},create),abandoned:0,generated:count};
  const n=C.news(day,rng(run,day,3445)),rules=fines?C.guidelines(day,stock,rng(run,day,5432)):[];
  let revenue=0,penalties=0,hasty=0;const trades=[];
  visits.served.forEach((v,j)=>{
    const reference=v.items.reduce((s,i)=>s+i.quantity*(events?C.priceAt(byId[i.idx],[n.paper,...(ends[j]-.05>=n.delay?[n.radio]:[])]):byId[i.idx].price),0);
    const result=R.classify(v.row,reference,reference);assert.ok(result.accepted);
    revenue+=result.revenue;penalties+=C.penalty(rules,v.items,v.attributes,true);hasty+=v.row.type===2;trades.push(result);
  });
  return {...R.settle(rep,trades),revenue,penalties,hasty,abandoned:visits.abandoned,generated:visits.generated};
}
function simulate(config,run){
  let cash=initial,unpaid=0,grace=null,rep=0,mask=0,stage=1,next=0,hit=null,failed=false;
  const days=[],purchases=[];
  for(let day=1;day<=31;day++){
    if(failed){days.push({...days.at(-1),day,revenue:0,penalties:0,spent:0,abandoned:0,hasty:0});continue;}
    const opening=cash,activeMask=mask,repStart=rep;
    const s=daySample(day,mask,rep,config.count,config.queue,config.timing,run);
    const settlement=C.settleCash(cash+s.revenue,unpaid,grace,day,maintenance[day-1]+s.penalties);
    ({cash,unpaid,grace,failed}=settlement);
    if(!failed&&!unpaid&&cash>=GOAL&&hit===null)hit=day;
    let spent=0;
    while(!failed&&!unpaid&&hit===null&&next<sequence.length){
      const id=sequence[next],f=facilities[id];assert.ok(stage>=+f.required_store_stage);
      if(cash<costs[id]+(maintenance[day]||0))break;
      cash-=costs[id];spent+=costs[id];next++;
      if(+f.upgrade_kind===3){assert.equal(+f.target_store_stage,stage+1);stage=+f.target_store_stage;}else mask|=1<<(id-12001);
      purchases.push({id,day});
    }
    rep=s.next;assert.equal(cash,opening+s.revenue-settlement.paid-spent);
    days.push({day,cash,net:cash-unpaid,unpaid,rep,repStart,revenue:s.revenue,penalties:s.penalties,spent,abandoned:s.abandoned,hasty:s.hasty,activeMask});
  }
  return {days,purchases,hit,failed};
}
function checks(){
  R.selfCheck();
  assert.deepEqual(queueVisits((()=>{let id=0;return ()=>({id:id++,row:{patience:9}});})(),[15,30,45]).served.map(x=>x.id),[0,2,5]);
  // Reuse of historical helpers must not change the old model; exact seeded samples remain stable.
  for(let i=0;i<25;i++)assert.deepEqual(C.simulate({policy:{errorRate:0},ignoreGoal:true},i).daily.map(d=>[d.cash,d.rep]),R.simulate(R.I.proposedCosts,{errorRate:0},i).daily.map(d=>[d.cash,d.repEnd]));
  assert.equal(C.penalty([{idx:1,target:'female',allowed:0,penalty:500}],[{idx:1,quantity:1}],['male','adult'],true),0);
  assert.equal(C.priceAt({idx:1001,type:1,price:100},[9002,9002]),115);
  assert.equal(C.settleCash(500,600,4,4,600).failed,true);
  for(let i=0;i<30;i++){
    const s=simulate({count:10,queue:true,timing:'jitter'},i);
    s.days.slice(1).forEach((d,j)=>assert.equal(d.repStart,s.days[j].rep));
    for(const p of s.purchases.filter(p=>+facilities[p.id].upgrade_kind===1)){
      assert.ok(!(s.days[p.day-1].activeMask&(1<<(p.id-12001))));
      if(p.day<31)assert.ok(s.days[p.day].activeMask&(1<<(p.id-12001)));
    }
  }
}
module.exports={queueVisits,serviceEnds,daySample,simulate,checks};
if(require.main===module){
  checks();console.log('Boundary, 25 historical regressions, and 30 full-path activation checks passed.');
  const sourceFiles=[...Object.values(files),'Assets/Scripts/Customer/CustomerQueue.cs','Assets/Scripts/Customer/CustomerCompositionSelector.cs','Assets/Scripts/Customer/CustomerProductAvailability.cs','Assets/Scripts/Customer/CustomerVisit.cs','Assets/Scripts/Finance/DailyReputationCalculator.cs','Assets/Scripts/Progress/DayProgress.cs','Assets/Scripts/Events/PriceEventScheduler.cs','doc/work/balancing-reference/citizenship-20260913/calculate.js','doc/work/balancing-reference/reputation-20260912/calculate.js','doc/work/balancing-reference/draft-20260912/model.js','doc/work/balancing-reference/citizenship-queue-20260913/calculate.js'];
  fs.writeFileSync(path.join(__dirname,'inputs.json'),JSON.stringify({goal:GOAL,tables:T,sources:sourceFiles.map(file=>({file,sha256:hash(file)})),costs,sequence},null,2)+'\n');
  const configs=[...[8,10,12].flatMap(count=>[{key:`control-${count}`,count,queue:false,timing:'jitter'},{key:`queue-${count}`,count,queue:true,timing:'jitter'}]),{key:'queue-fixed-12',count:12,queue:true,timing:'fixed'}];
  const scenarios=[];
  for(const config of configs){
    const raw=Array.from({length:RUNS},(_,i)=>simulate(config,i));
    const sum=k=>R.summary(raw.map(r=>r.days.reduce((s,d)=>s+d[k],0)));
    const out={...config,runs:RUNS,finalCash:R.summary(raw.map(r=>r.days[30].net)),finalRep:R.summary(raw.map(r=>r.days[30].rep)),success:C.probability(raw.filter(r=>r.hit!==null).length,RUNS),hitDay:R.summary(raw.flatMap(r=>r.hit===null?[]:[r.hit])),failed:raw.filter(r=>r.failed).length,totalRevenue:sum('revenue'),totalFines:sum('penalties'),totalInvestment:sum('spent'),totalAbandoned:sum('abandoned'),days:Array.from({length:31},(_,j)=>({day:j+1,cash:R.summary(raw.map(r=>r.days[j].net)),rep:R.summary(raw.map(r=>r.days[j].rep)),hasty:R.summary(raw.map(r=>r.days[j].hasty))})),purchases:sequence.map(id=>({id,days:R.summary(raw.flatMap(r=>r.purchases.filter(p=>p.id===id).map(p=>p.day)))}))};
    scenarios.push(out);console.log(JSON.stringify({key:config.key,median:out.finalCash.p50,success:out.success.rate,rep:out.finalRep.mean}));
    fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify({goal:GOAL,runs:RUNS,days:31,initial,scenarios},null,2)+'\n');
  }
}
