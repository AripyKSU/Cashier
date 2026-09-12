// Offline balance draft. Runtime rules are mirrored here; no game data or code is written.
const fs=require('node:fs'), path=require('node:path'), assert=require('node:assert/strict'), crypto=require('node:crypto');
const M=require('../draft-20260912/model.js');
const root=path.resolve(__dirname,'../../../..');
const readCsv=file=>{
  const [h,...rows]=fs.readFileSync(path.join(root,file),'utf8').trim().replace(/^\uFEFF/,'').split(/\r?\n/);
  const keys=h.split(',');
  return rows.map(r=>Object.fromEntries(r.split(',').map((v,i)=>[keys[i],v])));
};
const inputPath=path.join(__dirname,'inputs.json');
if(!fs.existsSync(inputPath)) {
  const files={products:'Assets/Datas/Customer/ProductData.csv',dispositions:'Assets/Datas/Customer/CustomerDispositionData.csv',reputation:'Assets/Datas/ReputationBalanceData.csv',facilities:'Assets/Datas/FacilityData.csv',maintenance:'Assets/Datas/MaintenanceBalanceData.csv',economy:'Assets/Datas/EconomyBalanceData.csv'};
  const sources=[...Object.values(files),'Assets/Scripts/Customer/CustomerCompositionSelector.cs','Assets/Scripts/Customer/CustomerProductAvailability.cs','Assets/Scripts/Customer/CustomerVisit.cs','Assets/Scripts/Finance/DailyReputationCalculator.cs','Assets/Scripts/Finance/ReputationTransactionClassifier.cs','Assets/Scripts/Manager/GameSessionManager.cs','Assets/Scripts/Facility/FacilityService.cs','doc/work/balancing-reference/store-stages-20260912/results.json'].map(file=>({file,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex')}));
  const costDraft=JSON.parse(fs.readFileSync(path.join(__dirname,'../store-stages-20260912/results.json'),'utf8'));
  fs.writeFileSync(inputPath,JSON.stringify({tables:Object.fromEntries(Object.entries(files).map(([k,f])=>[k,readCsv(f)])),proposedCosts:costDraft.proposed,sources},null,2)+'\n');
}
const I=JSON.parse(fs.readFileSync(inputPath,'utf8')), T=I.tables;
const products=T.products.map(p=>({...p,idx:+p.idx,type:+p.product_type,price:+p.base_price,facility:+p.required_facility_idx})).sort((a,b)=>a.idx-b.idx);
const facilities=Object.fromEntries(T.facilities.map(f=>[+f.idx,f]));
const reps=T.reputation.map(r=>Object.fromEntries(Object.entries(r).map(([k,v])=>[k,+v])));
const dispositions=T.dispositions.map(r=>({...r,idx:+r.idx,type:+r.disposition_type,types:r.preferred_product_types.split('_').filter(Boolean).map(Number),ids:r.preferred_product_idxs.split('_').filter(Boolean).map(Number)})).sort((a,b)=>a.idx-b.idx);
const typeOrder=[1,3,4,2,5], fields=['normal_weight','price_sensitive_weight','wealthy_weight','hasty_weight','poor_weight'];
const sequence=[12001,12002,12008,12003,12004,12010,12005,12006];
const maintenance=T.maintenance.map(r=>+r.maintenanceAmount), initial=+T.economy[0].initialBalance;
const targets=[{id:12001,day:3},{id:12008,day:10},{id:12003,day:12},{id:12010,day:20},{id:12005,day:22}];
const weights=[[160,100,45],[100,160,100],[100,120,180]], displayWeights=[100,220,280,350];
const bit=id=>id?1<<(id-12001):0;
const repRow=rep=>{const r=reps.find(r=>rep>=r.min_reputation&&rep<=r.max_reputation);assert.ok(r);return r;};
// Mix run/day seeds so adjacent days are not shifted slices of the same LCG sequence.
function seed(run,day,salt=0){let x=(run^Math.imul(day,0x9e3779b9)^salt^20260912)>>>0;x=Math.imul(x^(x>>>16),0x21f0aaad);x=Math.imul(x^(x>>>15),0x735a2d97);return(x^(x>>>15))>>>0;}
function pick(items,weights,u){let n=u*weights.reduce((a,b)=>a+b,0);for(let i=0;i<items.length;i++){n-=weights[i];if(n<0)return items[i];}return items.at(-1);}
function classify(row,reference,offered) {
  const type=+row.disposition_type;
  const below=offered*1000<reference*(+row.minimum_price_tolerance);
  const accepted=!below && offered<=Math.floor(reference*(+row.price_tolerance)/1000) && (type!==3||offered===reference);
  const grade=!accepted?(below?'regular':'extreme') : offered*1000<reference*(+row.regular_price_min_rate)?'discount':offered*1000>reference*(+row.regular_price_max_rate)?'markup':'regular';
  const score=grade==='discount'||(grade==='regular'&&type===2)?100:grade==='regular'?50:grade==='markup'?25:0;
  return {accepted,grade,score,weight:type===2?3:1,revenue:accepted?offered:0};
}
function settle(rep,trades) {
  let weight=0,total=0;
  for(const t of trades){weight+=t.weight;total+=t.score*t.weight;}
  for(let i=trades.length;i<5;i++){weight++;total+=50;}
  let score=Math.floor(total/weight);
  if(trades.length<5)score=Math.max(20,Math.min(80,score));
  const base=reps.find(r=>score>=r.settlement_min_score&&score<=r.settlement_max_score).settlement_delta;
  const delta=Math.max(-15,Math.min(10,base>0?Math.floor(base*repRow(rep).recovery_rate/1000):base));
  return {score,delta,next:Math.max(-100,Math.min(100,rep+delta))};
}
function offerAction(day,policy,u) {
  const share=policy.intentionalShare||0;
  assert.ok(share>=0&&share<=1);
  if(day<12||day>18||u>=policy.errorRate)return {rate:1000,error:false,intentional:false};
  const intentional=u>=policy.errorRate*(1-share);
  return {rate:intentional?(policy.intentionalRate||1100):1500,error:!intentional,intentional};
}
// Sequential weighted draws match the runtime distribution; seed streams are not C# replay seeds.
function daySample(day,mask,rep,random,policy,behaviorRandom,environment={}) {
  const phase=M.phaseOf(day), band=repRow(rep);
  const available=products.filter(p=>+p.is_available===1&&+p.available_day<=day-1&&(!p.facility||(mask&bit(p.facility))));
  const catalog=[];
  while(catalog.length<Math.min([4,6,8][phase],available.length+catalog.length)) {
    const p=pick(available,available.map(p=>displayWeights[p.facility?+facilities[p.facility].required_store_stage:0]),random());
    catalog.push(p);available.splice(available.indexOf(p),1);
  }
  catalog.sort((a,b)=>a.idx-b.idx);
  environment.onCatalog?.(catalog);
  const trades=[]; let revenue=0,referenceSum=0,errors=0,discounts=0,units=0,intentional=0,acceptedIntentional=0;
  const typeCounts=[0,0,0,0,0];
  for(let j=0;j<(environment.attempts||8);j++) {
    const type=pick(typeOrder,fields.map(k=>band[k]),random());typeCounts[type-1]++;
    const rows=dispositions.filter(r=>r.type===type);
    const row=pick(rows,rows.map(r=>r.types.length?r.types.reduce((s,t)=>s+weights[phase][t<=4?0:t<=6?1:2],0)/r.types.length:100),random());
    const yes=catalog.filter(p=>row.types.includes(p.type)||row.ids.includes(p.idx)), no=catalog.filter(p=>!yes.includes(p));
    const max=Math.min(+row.max_product_kinds,catalog.length), min=Math.min(+row.min_product_kinds,max);
    const count=min+Math.floor(random()*(max-min+1));
    let reference=0; const items=[];
    const picked=new Set();
    for(let k=0;k<count;k++) {
      const prefRoll=random(), indexRoll=random(), quantityRoll=random();
      const pool=yes.length&&(!no.length||prefRoll<+row.preferred_selection_chance/1000)?yes:no;
      const p=pool.splice(Math.floor(indexRoll*pool.length),1)[0];
      assert.ok(p&&!picked.has(p.idx)); picked.add(p.idx);
      const quantity=+row.min_quantity+Math.floor(quantityRoll*(+row.max_quantity-(+row.min_quantity)+1));
      reference+=quantity*(environment.priceAt?.(p,j)??p.price); units+=quantity;
      items.push({idx:p.idx,quantity});
    }
    const u=behaviorRandom();
    const action=offerAction(day,policy,u);
    let rate=action.rate;
    if(action.error)errors++;
    if(action.intentional)intentional++;
    if(day>=19&&policy.discountRate&&type!==3&&u<policy.discountRate){rate=950;discounts++;}
    const offered=Math.max(1,Math.floor(reference*rate/1000));
    const result=classify(row,reference,offered);
    environment.onTrade?.(items,result,j);
    if(action.intentional&&result.accepted)acceptedIntentional++;
    trades.push(result);revenue+=result.revenue;referenceSum+=reference;
  }
  return {revenue,referenceSum,errors,discounts,units,typeCounts,intentional,acceptedIntentional,accepted:trades.filter(t=>t.accepted).length,...settle(rep,trades)};
}
const policies=[
  {key:'fixed',name:'중립 고정 비교',fixed:true,errorRate:0},
  {key:'fair',name:'꾸준한 정가 거래',errorRate:0},
  {key:'light',name:'중반 실수 25%',errorRate:.25},
  {key:'frequent',name:'중반 실수 50%',errorRate:.5},
  {key:'repair',name:'실수 후 일부 할인',errorRate:.5,discountRate:.25}
];
function simulate(prices,policy,run) {
  let cash=initial,rep=0,mask=0,stage=1,next=0;
  const daily=[],purchases=[];
  for(let day=1;day<=31;day++) {
    const repStart=rep,activeMask=mask,opening=cash,stageAtOpen=stage;
    const s=daySample(day,mask,rep,M.rng(seed(run,day)),policy,M.rng(seed(run,day,9176)));
    cash+=s.revenue;
    // No fines assumed. Stop rather than approximate unpaid balance/grace-period logic if a sample enters debt.
    assert.ok(cash>=maintenance[day-1],'Debt case needs the implemented grace-period model');
    cash-=maintenance[day-1]; let spent=0;
    while(next<sequence.length) {
      const id=sequence[next],f=facilities[id];
      assert.ok(stage>=+f.required_store_stage);
      if(+f.upgrade_kind===3)assert.equal(+f.target_store_stage,stage+1);
      if(cash<prices[id]+(maintenance[day]||0))break;
      assert.ok(!purchases.some(p=>p.id===id));
      cash-=prices[id];spent+=prices[id];next++;
      if(+f.upgrade_kind===3)stage=+f.target_store_stage;else mask|=bit(id);
      purchases.push({id,day,activeDay:+f.upgrade_kind===3?day:day+1});
    }
    rep=policy.fixed?0:s.next;
    assert.equal(cash,opening+s.revenue-maintenance[day-1]-spent);
    daily.push({day,repStart,repEnd:rep,score:s.score,delta:rep-repStart,revenue:s.revenue,cash,spent,activeMask,stageAtOpen,accepted:s.accepted,errors:s.errors,intentional:s.intentional,acceptedIntentional:s.acceptedIntentional,discounts:s.discounts,wealthyShare:repRow(repStart).wealthy_weight/1000});
  }
  return {daily,purchases};
}
function summary(a) {
  if(!a.length)return null;
  a=a.slice().sort((x,y)=>x-y);
  const q=p=>{const x=(a.length-1)*p,l=Math.floor(x);return a[l]+(a[Math.ceil(x)]-a[l])*(x-l);};
  const mean=a.reduce((s,x)=>s+x,0)/a.length;
  return {n:a.length,mean,se:a.length>1?Math.sqrt(a.reduce((s,x)=>s+(x-mean)**2,0)/(a.length-1)/a.length):0,p10:q(.1),p50:q(.5),p90:q(.9)};
}
function selfCheck() {
  const normal=dispositions.find(r=>r.type===1),hasty=dispositions.find(r=>r.type===2),sensitive=dispositions.find(r=>r.type===3);
  assert.equal(classify(normal,100,130).accepted,true); assert.equal(classify(normal,100,131).grade,'extreme');
  assert.deepEqual([classify(sensitive,100,95).accepted,classify(sensitive,100,95).grade],[false,'regular']);
  assert.equal(classify(sensitive,100,101).grade,'extreme');
  assert.equal(settle(0,Array(8).fill(classify(normal,100,100))).delta,0);
  assert.equal(settle(0,[...Array(7).fill(classify(normal,100,100)),classify(hasty,100,100)]).delta,7);
  assert.equal(settle(0,Array(8).fill(classify(normal,100,150))).delta,-15);
  assert.equal(settle(0,Array(8).fill(classify(normal,100,110))).delta,-7);
  assert.equal(settle(-30,[...Array(7).fill(classify(normal,100,100)),classify(hasty,100,100)]).delta,10);
  assert.equal(settle(99,Array(8).fill(classify(hasty,100,100))).next,100);
  assert.equal(settle(-99,Array(8).fill(classify(normal,100,150))).next,-100);
  assert.equal(settle(0,[]).delta,0);
  assert.equal(settle(0,[classify(hasty,100,100)]).score,71);
  assert.equal(products.length,21);assert.equal(new Set(products.map(p=>p.idx)).size,21);
  assert.ok(reps.every(r=>fields.reduce((s,k)=>s+r[k],0)===1000));
  assert.deepEqual(T.facilities.filter(f=>+f.upgrade_kind===1).map(f=>+f.required_store_stage),[1,1,2,2,3,3]);
  const check=simulate(I.proposedCosts,policies[1],0);
  for(let i=1;i<31;i++)assert.equal(check.daily[i].repStart,check.daily[i-1].repEnd);
  for(const p of check.purchases.filter(p=>+facilities[p.id].upgrade_kind===1)) {
    assert.ok(!(check.daily[p.day-1].activeMask&bit(p.id)));
    if(p.day<31)assert.ok(check.daily[p.day].activeMask&bit(p.id));
  }
}
module.exports={I,simulate,summary,settle,classify,offerAction,selfCheck,daySample,seed,pick};
if(require.main===module) {
selfCheck();
const samplerChecks=[];
const oldEstimates=JSON.parse(fs.readFileSync(path.join(__dirname,'../daily-income-20260912/estimates.json'),'utf8'));
for(const [day,mask] of [[3,0],[12,3],[22,15],[22,63]]) {
  const values=[];
  for(let i=0;i<5000;i++)values.push(daySample(day,mask,0,M.rng(seed(i,day,3221)),policies[1],M.rng(seed(i,day,7331))).revenue);
  const s=summary(values),old=oldEstimates.scenarios.find(r=>r.demand==='draft'&&r.phase===M.phaseOf(day)&&r.mask===mask);
  const tolerance=5*Math.sqrt(s.se**2+(old.se*8)**2);
  assert.ok(Math.abs(s.mean-old.mean*8)<tolerance,'Neutral sampler disagrees with reviewed demand estimates');
  samplerChecks.push({day,mask,mean:s.mean,referenceMean:old.mean*8,tolerance});
}
const RUNS=2000, costSets={current:Object.fromEntries(T.facilities.map(f=>[f.idx,+f.purchase_price])),proposed:I.proposedCosts};
const scenarios=[],roi=[];
for(const [costKey,prices] of Object.entries(costSets)) {
  let fairRuns=null;
  for(const policy of policies) {
    const raw=Array.from({length:RUNS},(_,i)=>simulate(prices,policy,i));
    if(policy.key==='fair')fairRuns=raw;
    const end=h=>h.daily.at(-1);
    const affected=raw.filter(h=>h.daily[17].repEnd<h.daily[10].repEnd);
    const recovered=affected.flatMap(h=>{const threshold=h.daily[10].repEnd; const p=h.daily.find(d=>d.day>=19&&d.repEnd>=threshold);return p?[p.day]:[];});
    scenarios.push({costKey,policy,runs:RUNS,days:Array.from({length:31},(_,i)=>({day:i+1,...Object.fromEntries(['repStart','repEnd','score','revenue','cash','accepted','wealthyShare'].map(k=>[k,summary(raw.map(h=>h.daily[i][k]))]))})),milestones:targets.map(t=>({...t,...summary(raw.flatMap(h=>h.purchases.filter(p=>p.id===t.id).map(p=>p.day))),reached:raw.filter(h=>h.purchases.some(p=>p.id===t.id)).length/RUNS})),finalRep:summary(raw.map(h=>end(h).repEnd)),finalCash:summary(raw.map(h=>end(h).cash)),midRepLoss:summary(raw.map(h=>h.daily[17].repEnd-h.daily[10].repEnd)),midRepDroppedRate:raw.filter(h=>h.daily[17].repEnd<h.daily[10].repEnd).length/RUNS,recoveredDay:summary(recovered),recoveredBy31:recovered.length/RUNS,pairedFinalCashVsFair:fairRuns?summary(raw.map((h,i)=>end(h).cash-end(fairRuns[i]).cash)):null});
    scenarios.at(-1).recoveredBy31=affected.length?recovered.length/affected.length:null;
    scenarios.at(-1).recoveryEligibleRuns=affected.length;
    if(costKey==='proposed'&&['fair','light','frequent'].includes(policy.key)) {
      for(const a of [{id:12001,day:3,before:0,gate:null},{id:12003,day:12,before:3,gate:12008},{id:12005,day:22,before:15,gate:12010}]) {
        const cost=prices[a.id]+(a.gate?prices[a.gate]:0),values=Array.from({length:10},()=>[]),crossings=[];
        for(let run=0;run<RUNS;run++) {
          let net=-cost,crossing=null;values[0].push(net);
          for(let k=1;k<=9;k++) {
            const day=a.day+k,rep=raw[run].daily[day-1].repStart;
            const revenue=mask=>daySample(day,mask,rep,M.rng(seed(run,day,4401)),policy,M.rng(seed(run,day,9176))).revenue;
            net+=revenue(a.before|bit(a.id))-revenue(a.before);
            values[k].push(net);if(crossing===null&&net>=0)crossing=k;
          }
          crossings.push(crossing);
        }
        roi.push({...a,policy:policy.key,cost,curve:values.map((v,k)=>({elapsed:k,...summary(v)})),withinSeven:crossings.filter(k=>k!==null&&k<=7).length/RUNS,byNine:crossings.filter(k=>k!==null).length/RUNS});
      }
    }
  }
}
// Isolate reputation's customer-mix effect: same catalog ownership and regular prices, no lost sales or delayed investment.
const mix=[];
for(const mask of [0,3,15,31,63])for(const rep of [-80,-40,0,40,80]) {
  const values=[];
  for(let i=0;i<5000;i++)values.push(daySample(22,mask,rep,M.rng(seed(i,22,9191)),policies[1],M.rng(seed(i,22,7711))).revenue);
  mix.push({mask,rep,...summary(values)});
}
const output={runs:RUNS,initial,costSets,targets,policies,scenarios,mix,sourceSnapshot:'inputs.json',assumptions:['180 seconds with eight completed offer attempts/day; refused trades consume a slot and generate zero revenue','12..18 inclusive: each offer independently has 25% or50% chance of charging150% reference price; actual tolerance decides acceptance','19..31 recovery variant: 25% of non-price-sensitive offers at95%; other offers at100%; no new game mechanics','daily reputation uses actual grade scores, hasty triple weight, small-sample padding, CSV deltas/recovery and -100..100 clamp; composition changes next day','price events excluded from central curve as user-requested random variation; no queue abandonment, fines, morality/family consequences or convenience throughput modeled','prices are current runtime vs previous cost proposal; no runtime data writes; next-day maintenance reserve and sequence are player strategy'],checks:['offer tolerance and reputation grade boundaries','normal, hasty, refused and markup day scores','negative reputation recovery floor and daily clamp','small-sample padding and full reputation clamp','21 unique products, reputation weight sums, existing store gates','cash conservation and all simulated upkeep paid','next-day reputation and product activation']};
output.samplerChecks=samplerChecks;
output.roi=roi;
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify(output,null,2)+'\n');
console.log(JSON.stringify(scenarios.map(s=>({cost:s.costKey,policy:s.policy.name,rep11:s.days[10].repEnd.p50,rep18:s.days[17].repEnd.p50,rep31:s.finalRep.p50,repLoss:s.midRepLoss.p50,dropRate:s.midRepDroppedRate,recovery:s.recoveredDay?.p50,recoveryRate:s.recoveredBy31,cash31:s.finalCash.p50,milestones:s.milestones.map(m=>[m.id,m.p50,m.p10,m.p90,m.reached])})),null,2));
}
