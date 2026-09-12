// Conditional 31-day funding simulation. Writes analysis artifacts only.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const R=require('../reputation-20260912/calculate.js'),M=require('../draft-20260912/model.js');
const root=path.resolve(__dirname,'../../../..'),GOAL=1000000,RUNS=10000;
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex');
const csv=file=>{const [h,...lines]=fs.readFileSync(path.join(root,file),'utf8').trim().replace(/^\uFEFF/,'').split(/\r?\n/);return lines.map(l=>Object.fromEntries(l.split(',').map((v,i)=>[h.split(',')[i],v])));};
for(const s of R.I.sources)assert.equal(hash(s.file),s.sha256,`Prior input changed: ${s.file}`);
const files={events:'Assets/Datas/PriceEventData.csv',schedule:'Assets/Datas/PriceEventScheduleData.csv',guidelines:'Assets/Datas/DailyGuidelineData.csv'};
const tables=Object.fromEntries(Object.entries(files).map(([k,f])=>[k,csv(f)]));
const sources=[...R.I.sources,...[...Object.values(files),'Assets/Scripts/Events/PriceEventScheduler.cs','Assets/Scripts/Commons/Data/PriceEventScheduleData.cs','Assets/Scripts/Commons/SaleRestriction.cs','Assets/Scripts/Finance/DailyAggregationService.cs','doc/work/balancing-reference/reputation-20260912/calculate.js','doc/work/balancing-reference/draft-20260912/model.js','doc/work/balancing-reference/citizenship-20260913/calculate.js'].map(file=>({file,sha256:hash(file)}))];
fs.writeFileSync(path.join(__dirname,'inputs.json'),JSON.stringify({goal:GOAL,priorInputs:'../reputation-20260912/inputs.json',tables,sources},null,2)+'\n');
const events=Object.fromEntries(tables.events.map(e=>[+e.idx,{...e,ids:e.product_idxs.split('_').filter(Boolean).map(Number),types:e.product_types.split('_').filter(Boolean).map(Number)}]));
const due=(s,day)=>day-1>=+s.start_day&&(!s.end_day||day-1<=+s.end_day)&&(+s.channel===2||(+s.repeat_days===0?day-1===+s.start_day:(day-1-(+s.start_day))%+s.repeat_days===0));
function news(day,random){const choose=channel=>{const a=tables.schedule.filter(s=>+s.channel===channel&&due(s,day));return a.length?+R.pick(a,a.map(s=>+s.selection_weight),random()).event_idx:null;};return {paper:choose(1),radio:choose(2),delay:random()*60};}
function priceAt(p,ids){let rate=0,amount=0;for(const id of new Set(ids.filter(Boolean))){const e=events[id];if(!e.ids.includes(p.idx)&&!e.types.includes(p.type))continue;if(+e.change_type===1)rate+=+e.change_value;else if(+e.change_type===2)amount+=+e.change_value;}return Math.max(1,Math.floor(p.price*(1000+rate)/1000)+amount);}
const targets=['all','male','female','child','adult','elderly'],targetWeights=[70,6,6,6,6,6];
const guidelineCount=day=>day>=20?2:day>=10?1:0;
function guidelines(day,catalog,random){
  // Candidate weights factor into independent template/attribute/product draws.
  const available=catalog.map(p=>p.idx),rules=[];
  for(let i=0;i<guidelineCount(day);i++){
    const template=tables.guidelines[Math.floor(random()*tables.guidelines.length)];
    const target=R.pick(targets,targetWeights,random());
    const idx=available.splice(Math.floor(random()*available.length),1)[0];
    rules.push({idx,target,allowed:+template.allowed_quantity,penalty:+template.penalty_amount});
  }return rules;
}
function penalty(rules,items,attributes,accepted){return accepted?rules.reduce((sum,r)=>sum+((r.target==='all'||attributes.includes(r.target))&&((items.find(p=>p.idx===r.idx)?.quantity||0)>r.allowed)?r.penalty:0),0):0;}
const T=R.I.tables,facilities=Object.fromEntries(T.facilities.map(f=>[+f.idx,f]));
const currentCosts=Object.fromEntries(T.facilities.map(f=>[+f.idx,+f.purchase_price]));
const allSequence=[12001,12002,12008,12003,12004,12010,12005,12006],primarySequence=[12001,12008,12003,12010,12005];
const maintenance=T.maintenance.map(r=>+r.maintenanceAmount),initial=+T.economy[0].initialBalance;
const fair={key:'fair',errorRate:0},mixed=rate=>({key:`mixed${rate*100}`,errorRate:rate,intentionalShare:.6,intentionalRate:1100});
function settleCash(cash,unpaid,grace,day,due){
  const total=unpaid+due;
  if(cash>=total)return {cash:cash-total,unpaid:0,grace:null,paid:total,failed:false};
  grace=unpaid?grace:day+3;
  return {cash,unpaid:total,grace,paid:0,failed:day>=grace};
}
function simulate(config,run){
  const attempts=config.attempts||8,prices=config.cost==='current'?currentCosts:R.I.proposedCosts;
  const sequence=config.primary?primarySequence:allSequence;
  let cash=initial,unpaid=0,grace=null,rep=0,mask=0,stage=1,next=0,hit=null,failed=false,debtDays=0;
  const daily=[],purchases=[];
  const firstGender=Math.floor(M.rng(R.seed(run,0,8991))()*2);
  for(let day=1;day<=31;day++){
    if(failed){daily.push({...daily.at(-1),day,revenue:0,penalties:0,spent:0,paid:0,accepted:0});continue;}
    const n=news(day,M.rng(R.seed(run,day,3445))),attributesRandom=M.rng(R.seed(run,day,8823));
    let rules=[],penalties=0;
    const environment={attempts,
      onCatalog:catalog=>{if(config.fines)rules=guidelines(day,catalog,M.rng(R.seed(run,day,5432)));},
      priceAt:(p,j)=>config.events?priceAt(p,[n.paper,...((j+.5)*180/attempts>=n.delay?[n.radio]:[])]):p.price,
      onTrade:(items,result,j)=>{
        const gender=(firstGender+(day-1)*attempts+j)%2?'female':'male';
        const age=['adult','child','elderly'][Math.floor(attributesRandom()*3)];
        penalties+=penalty(rules,items,[gender,age],result.accepted);
      }};
    const opening=cash,activeMask=mask;
    const s=R.daySample(day,mask,rep,M.rng(R.seed(run,day)),config.policy,M.rng(R.seed(run,day,9176)),environment);
    const settlement=settleCash(cash+s.revenue,unpaid,grace,day,maintenance[day-1]+penalties);
    ({cash,unpaid,grace,failed}=settlement);if(unpaid)debtDays++;
    if(!failed&&!unpaid&&cash>=GOAL&&hit===null)hit=day;
    let spent=0;
    // Explicit player strategy: save next day's upkeep, defer investments while unpaid,
    // and stop buying optional equipment once the citizenship fund is secured.
    while(!failed&&!unpaid&&(config.ignoreGoal||hit===null)&&next<sequence.length){
      const id=sequence[next],f=facilities[id];assert.ok(stage>=+f.required_store_stage);
      if(+f.upgrade_kind===3)assert.equal(+f.target_store_stage,stage+1);
      if(cash<prices[id]+(maintenance[day]||0))break;
      cash-=prices[id];spent+=prices[id];next++;
      if(+f.upgrade_kind===3)stage=+f.target_store_stage;else mask|=1<<(id-12001);
      purchases.push({id,day,activeDay:+f.upgrade_kind===3?day:day+1});
    }
    rep=s.next;assert.equal(cash,opening+s.revenue-settlement.paid-spent);
    daily.push({day,cash,net:cash-unpaid,unpaid,rep,revenue:s.revenue,penalties,spent,paid:settlement.paid,accepted:s.accepted,activeMask,failed});
  }return {daily,purchases,hit,failed,debtDays};
}
function probability(k,n){
  const p=k/n,z=1.95996398454,den=1+z*z/n;
  const center=(p+z*z/(2*n))/den,half=z*Math.sqrt(p*(1-p)/n+z*z/(4*n*n))/den;
  return {hits:k,n,rate:p,wilson95:[Math.max(0,center-half),Math.min(1,center+half)],zeroHitUpper95:k===0?1-Math.pow(.05,1/n):null};
}
function checks(){
  R.selfCheck();
  assert.deepEqual(Array.from({length:31},(_,i)=>i+1).filter(d=>due(tables.schedule[0],d)),[3,10,17,24,31]);
  assert.ok(tables.schedule.filter(s=>+s.channel===2).every(s=>due(s,1)&&due(s,31)&&!due(s,32)));
  assert.equal(priceAt({idx:1001,type:1,price:100},[9002,9002]),115);
  assert.equal(priceAt({idx:1,type:2,price:101},[9001]),90);
  assert.equal(priceAt({idx:1,type:3,price:700},[9004]),805);
  assert.equal(priceAt({idx:1,type:7,price:700},[9001,9004]),700);
  assert.deepEqual([9,10,19,20,31].map(guidelineCount),[0,1,1,2,2]);
  const rules=[{idx:1,target:'all',allowed:0,penalty:500},{idx:2,target:'child',allowed:1,penalty:500}],items=[{idx:1,quantity:3},{idx:2,quantity:3}];
  assert.equal(penalty(rules,items,['child'],true),1000);assert.equal(penalty(rules,items,['adult'],true),500);assert.equal(penalty(rules,items,['child'],false),0);
  assert.equal(penalty([rules[1]],[{idx:2,quantity:1}],['child'],true),0);
  for(let i=0;i<100;i++)assert.equal(new Set(guidelines(20,[{idx:1},{idx:2},{idx:3}],M.rng(i+1)).map(r=>r.idx)).size,2);
  assert.deepEqual(settleCash(500,0,null,1,600),{cash:500,unpaid:600,grace:4,paid:0,failed:false});
  assert.equal(settleCash(500,600,4,4,600).failed,true);
  assert.deepEqual(settleCash(1200,600,4,4,600),{cash:0,unpaid:0,grace:null,paid:1200,failed:false});
  // Defaults must exactly reproduce the preceding model, including every purchase.
  for(const policy of [fair,mixed(.5)])for(let run=0;run<100;run++){
    const old=R.simulate(R.I.proposedCosts,policy,run),fresh=simulate({policy,ignoreGoal:true},run);
    assert.deepEqual(fresh.purchases,old.purchases);
    assert.deepEqual(fresh.daily.map(d=>[d.cash,d.rep,d.revenue,d.accepted]),old.daily.map(d=>[d.cash,d.repEnd,d.revenue,d.accepted]));
  }
  const old=JSON.parse(fs.readFileSync(path.join(__dirname,'../reputation-20260912/results.json'),'utf8')).scenarios.find(s=>s.costKey==='proposed'&&s.policy.key==='fair');
  const values=Array.from({length:2000},(_,i)=>R.simulate(R.I.proposedCosts,fair,i));
  assert.deepEqual(R.summary(values.map(r=>r.daily[30].cash)),old.finalCash);
  assert.deepEqual(R.summary(values.map(r=>r.daily[30].repEnd)),old.finalRep);
}
checks();console.log('Boundary checks and historical 2,000-run regression passed.');
const configs=[
  {key:'current-fair-8',label:'현재 데이터 · 정가 8회',cost:'current',policy:fair,events:true,fines:true},
  {key:'draft-fair-8',label:'비용 조정안 · 정가 8회',policy:fair,events:true,fines:true},
  ...[.25,.5,1].map(rate=>({key:`draft-mixed${rate*100}-8`,label:`비용 조정안 · 혼합 ${rate*100}% · 8회`,policy:mixed(rate),events:true,fines:true})),
  {key:'draft-fair-noevents',label:'비용 조정안 · 기존 비교선',policy:fair},
  {key:'draft-fair-prices',label:'비용 조정안 · 가격 이벤트만',policy:fair,events:true},
  {key:'draft-mixed50-prices',label:'비용 조정안 · 혼합50% · 가격만',policy:mixed(.5),events:true},
  ...[10,12].map(attempts=>({key:`draft-fair-${attempts}`,label:`비용 조정안 · 정가 ${attempts}회`,attempts,policy:fair,events:true,fines:true})),
  {key:'draft-primary-8',label:'비용 조정안 · 대표 설비만 · 8회',primary:true,policy:fair,events:true,fines:true}
];
const scenarios=[];
for(const config of configs){
  const raw=Array.from({length:RUNS},(_,run)=>simulate(config,run));
  const sum=key=>R.summary(raw.map(r=>r.daily.reduce((s,d)=>s+d[key],0)));
  const finalCash=R.summary(raw.map(r=>r.daily[30].net));
  const result={...config,runs:RUNS,finalCash,progressPercent:Object.fromEntries(['mean','p10','p50','p90'].map(k=>[k,finalCash[k]/GOAL*100])),success:probability(raw.filter(r=>r.hit!==null).length,RUNS),hitDay:R.summary(raw.flatMap(r=>r.hit===null?[]:[r.hit])),failures:raw.filter(r=>r.failed).length,debtRuns:raw.filter(r=>r.debtDays>0).length,totalRevenue:sum('revenue'),totalFines:sum('penalties'),totalInvestment:sum('spent'),days:Array.from({length:31},(_,i)=>({day:i+1,cash:R.summary(raw.map(r=>r.daily[i].net)),rep:R.summary(raw.map(r=>r.daily[i].rep)),revenue:R.summary(raw.map(r=>r.daily[i].revenue))}))};
  scenarios.push(result);console.log(JSON.stringify({key:config.key,cash:finalCash.p50,p10:finalCash.p10,p90:finalCash.p90,success:result.success.rate,meanFines:result.totalFines.mean,failed:result.failures,debt:result.debtRuns}));
}
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify({date:'2026-09-13',goal:GOAL,days:31,runs:RUNS,initial,maintenanceTotal:maintenance.reduce((a,b)=>a+b,0),costSets:{current:currentCosts,proposed:R.I.proposedCosts},allSequence,primarySequence,scenarios,checks:['source hashes unchanged','historical 2000-run cash and reputation identical','200 daily-path/purchase regressions','event dates, radio window, duplicate IDs, prices and rounding','guideline day boundaries, distinct products, attributes, quantity boundary and rejected trades','settlement full payment, debt grace deadline, recovery, all-path cash conservation']},null,2)+'\n');
