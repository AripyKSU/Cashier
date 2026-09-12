// Reuse the preceding runtime-aligned model. This is analysis only, not a game data update.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const R=require('../reputation-20260912/calculate.js');
const previous=JSON.parse(fs.readFileSync(path.join(__dirname,'../reputation-20260912/results.json'),'utf8'));
const root=path.resolve(__dirname,'../../../..');
for(const s of R.I.sources)assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(root,s.file))).digest('hex'),s.sha256,s.file);
R.selfCheck();
const test={errorRate:.5,intentionalShare:.6};
assert.equal(R.offerAction(12,test,.19999).error,true);
assert.equal(R.offerAction(12,test,.2).intentional,true);
assert.equal(R.offerAction(12,test,.49999).rate,1100);
assert.equal(R.offerAction(12,test,.5).rate,1000);
assert.equal(R.offerAction(11,test,.1).rate,1000);
assert.equal(R.offerAction(19,test,.1).rate,1000);
for(const row of R.I.tables.dispositions) {
  const x=R.classify(row,1000,1100);
  assert.equal(x.accepted,+row.disposition_type!==3);
  assert.equal(x.score,+row.disposition_type===3?0:25);
}
const runs=2000,prices=R.I.proposedCosts;
const policies=[
  {key:'fair',name:'꾸준한 정가',errorRate:0},
  {key:'light',name:'문제25% 전부 실수',errorRate:.25},
  {key:'frequent',name:'문제50% 전부 실수',errorRate:.5},
  {key:'mixed25',name:'문제25% 안에서 4:6',errorRate:.25,intentionalShare:.6},
  {key:'mixed50',name:'문제50% 안에서 4:6',errorRate:.5,intentionalShare:.6},
  {key:'mixed100',name:'중반 전체 거래 4:6',errorRate:1,intentionalShare:.6}
];
const raw={},scenarios=[];
const sum=(a,key)=>a.reduce((s,r)=>s+r[key],0);
for(const p of policies) {
  const samples=Array.from({length:runs},(_,i)=>R.simulate(prices,p,i));raw[p.key]=samples;
  const affected=samples.filter(h=>h.daily[17].repEnd<h.daily[10].repEnd);
  const recovery=affected.flatMap(h=>{const d=h.daily.find(d=>d.day>=19&&d.repEnd>=h.daily[10].repEnd);return d?[d.day]:[];});
  const days=Array.from({length:31},(_,i)=>({day:i+1,...Object.fromEntries(['repEnd','revenue','cash','accepted'].map(k=>[k,R.summary(samples.map(h=>h.daily[i][k]))]))}));
  const mids=samples.map(h=>h.daily.slice(11,18));
  const errors=mids.reduce((s,a)=>s+sum(a,'errors'),0),intentional=mids.reduce((s,a)=>s+sum(a,'intentional'),0),acceptedIntentional=mids.reduce((s,a)=>s+sum(a,'acceptedIntentional'),0);
  const milestones=previous.targets.map(t=>({...t,...R.summary(samples.flatMap(h=>h.purchases.filter(p=>p.id===t.id).map(p=>p.day))),reached:samples.filter(h=>h.purchases.some(p=>p.id===t.id)).length/runs}));
  const s={policy:p,runs,days,milestones,midRevenue:R.summary(mids.map(a=>sum(a,'revenue'))),midAccepted:R.summary(mids.map(a=>sum(a,'accepted')/7)),midEndRep:days[17].repEnd,finalCash:days[30].cash,finalRep:days[30].repEnd,midDroppedRate:affected.length/runs,recoveryEligible:affected.length,recoveryDay:R.summary(recovery),recoveredBy31:affected.length?recovery.length/affected.length:null,actions:{opportunities:runs*7*8,errors,intentional,acceptedIntentional,actualErrorShare:errors+intentional?errors/(errors+intentional):null},pairedVsFair:R.summary(samples.map((h,i)=>h.daily[30].cash-raw.fair[i].daily[30].cash))};
  if(['fair','light','frequent'].includes(p.key)) {
    const old=previous.scenarios.find(s=>s.costKey==='proposed'&&s.policy.key===p.key);
    assert.deepEqual(s.finalCash,old.finalCash,'Existing cash result must stay identical');
    assert.deepEqual(s.finalRep,old.finalRep,'Existing reputation result must stay identical');
    assert.deepEqual(s.milestones,old.milestones,'Existing purchase results must stay identical');
  } else {
    assert.ok(Math.abs(s.actions.actualErrorShare-.4)<.015);
    assert.ok(intentional>acceptedIntentional,'Price-sensitive intentional offers must not be silently accepted');
    assert.ok(samples.every(h=>h.daily.every(d=>d.day>=12&&d.day<=18||d.errors+d.intentional===0)));
    if(p.errorRate===1)assert.equal(errors+intentional,runs*7*8);
    if(p.key!=='mixed100') {
      const key=p.key==='mixed25'?'light':'frequent';
      s.pairedVsAllErrors=R.summary(samples.map((h,i)=>h.daily[30].cash-raw[key][i].daily[30].cash));
    }
  }
  scenarios.push(s);
}
const sourceFiles=['doc/work/balancing-reference/reputation-20260912/inputs.json','doc/work/balancing-reference/reputation-20260912/calculate.js','doc/work/balancing-reference/reputation-20260912/results.json'];
const sources=sourceFiles.map(file=>({file,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex')}));
const output={runs,prices,policies,scenarios,sources,assumptions:['12..18 days only, eight offer attempts/day; normal trades before and after','inside problem opportunities, 40% input150% of current price, 60% intentionally charge110%; intentions are analysis labels, not runtime fields','accepted markup earns25 behavior points; overprice refusal earns0; daily weighted aggregate can still yield zero or positive reputation change','price-sensitive intentional110% is refused; no perfect knowledge of per-type maximum price assumed','25/50% total problem rates retain previous context;100% provided separately for entire-midgame40:60 interpretation','same existing cost proposal, no price events/queue departures/fines/family/morality effects, no runtime writes'],checks:['six action interval boundaries','110% acceptance and score across15 disposition rows','2000 runs each across6 scenarios','previous fair/25/50 final cash, reputation and purchase results identical','40:60 sampled ratio, price-sensitive refusal, period boundaries','cash conservation, next-day reputation/activation and upkeep checks inherited from reviewed model']};
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify(output,null,2)+'\n');
console.log(JSON.stringify(scenarios.map(s=>({key:s.policy.key,rep18:s.midEndRep.p50,rep31:s.finalRep.p50,cash31:s.finalCash.p50,accepted:s.midAccepted.mean,milestones:s.milestones.map(m=>[m.id,m.p50]),recovery:s.recoveryDay?.p50,ratio:s.actions.actualErrorShare,paired:s.pairedVsAllErrors?.mean})),null,2));
