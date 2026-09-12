// Offline comparison. Uses the reviewed v0.1 demand sampler, never writes runtime CSV.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const M = require('../draft-20260912/model.js');
const read = name => JSON.parse(fs.readFileSync(path.join(__dirname,name),'utf8').replace(/^\uFEFF/,''));
const I = read('inputs.json');
const RUNS=1000, SEED=20260912, START=1000;
const costs=[0,...Array.from({length:6},(_,i)=>+I.facilities.find(f=>+f.idx===12001+i).purchase_price)];
const variants=[{name:'이벤트 없음',schedule:[],duration:180},{name:'기존 일정 · 180초',schedule:I.previousSchedule,duration:180},{name:'초안 일정 · 180초',schedule:I.proposedSchedule,duration:180},{name:'초안 일정 · 30초',schedule:I.proposedSchedule,duration:30}];
const ids=p=>p.key==='draft-night'?'1024':p.key==='draft-detector'?'1025':p.key;
for(const p of M.products){const row=I.products.find(r=>r.idx===ids(p));assert(row);assert.equal(+row.base_price,p.price);assert.equal(+row.product_type,p.type);}
assert(costs.slice(1).every(x=>Number.isFinite(x)&&x>0),'Facility cost column');
function due(row,day){return day>=+row.start_day&&(row.end_day===''||day<=+row.end_day)&&(+row.channel===2||(+row.repeat_days===0?day===+row.start_day:(day-row.start_day)%row.repeat_days===0));}
function selected(schedule,channel,day,random){const rows=schedule.filter(r=>+r.channel===channel&&due(r,day)).sort((a,b)=>a.idx-b.idx);if(!rows.length)return null;let roll=random()*rows.reduce((s,r)=>s+(+r.selection_weight),0);for(const r of rows){roll-=+r.selection_weight;if(roll<0)return r.event_idx;}throw Error('Selection');}
function eventPrice(p,events,eventRows=I.events){let rate=0,amount=0;for(const id of new Set(events.filter(Boolean))){const e=eventRows.find(e=>e.idx===id);assert(e);if(!e.product_idxs.split('_').includes(ids(p))&&!e.product_types.split('_').map(Number).includes(p.type))continue;if(+e.change_type===1)rate+=+e.change_value;if(+e.change_type===2)amount+=+e.change_value;}return Math.max(1,Math.floor(p.price*(1000+rate)/1000)+amount);}
function priceAt(variant,run,day){const random=M.rng(SEED+run*9176+day*13007);const paper=selected(variant.schedule,1,day-1,random),radio=selected(variant.schedule,2,day-1,random),delay=random()*60;const events=variant.schedule===I.proposedSchedule?I.proposedEvents:I.events;return(p,trade)=>eventPrice(p,[paper,((trade+.5)/8*variant.duration)>=delay?radio:null],events);}
function revenue(variant,run,day,mask){return M.sample(M.phaseOf(day),mask,M.rng(SEED+run*1009+day*104729),false,false,priceAt(variant,run,day)).revenue;}
function summary(values){const a=values.slice().sort((a,b)=>a-b);const q=p=>{const x=(a.length-1)*p,l=Math.floor(x);return a[l]+(a[Math.ceil(x)]-a[l])*(x-l);};return{mean:a.reduce((s,x)=>s+x,0)/a.length,p10:q(.1),p50:q(.5),p90:q(.9)};}
assert.equal(eventPrice(M.products.find(p=>p.key==='1001'),['9002']),130);
assert.equal(eventPrice(M.products.find(p=>p.key==='1009'),['9004']),910);
assert.equal(eventPrice(M.products.find(p=>p.key==='1001'),['9002'],I.proposedEvents),115);
assert.equal(eventPrice(M.products.find(p=>p.key==='1009'),['9004'],I.proposedEvents),805);
assert.deepEqual(Array.from({length:31},(_,i)=>i).filter(d=>due(I.proposedSchedule[0],d)),[2,9,16,23,30]);
assert(!due(I.proposedSchedule[2],31));
assert.equal(M.sample(0,1,M.rng(SEED+104729)).revenue,revenue(variants[0],0,1,1));
const plans=[M.plans[0],M.plans[1],M.plans[4]];
const strategies=plans.map(plan=>{
  const raw=variants.map(v=>Array.from({length:RUNS},(_,run)=>{let cash=START,mask=0,next=0;const balances=[],purchases=[];for(let day=1;day<=31;day++){cash+=revenue(v,run,day,mask)-M.maintenance[day-1];if(day<=24&&next<plan.sequence.length&&cash>=costs[plan.sequence[next]]+M.maintenance[day]){const f=plan.sequence[next++];cash-=costs[f];mask|=M.bit(f);purchases.push({day,f});}balances.push(cash);}return{balances,purchases};}));
  return{name:plan.name,variants:variants.map((v,i)=>({name:v.name,days:Array.from({length:31},(_,d)=>summary(raw[i].map(r=>r.balances[d]))),final:summary(raw[i].map(r=>r.balances[30])),pairedDelta:summary(raw[i].map((r,j)=>r.balances[30]-raw[0][j].balances[30])),purchaseDays:plan.sequence.map(f=>({f,rate:raw[i].filter(r=>r.purchases.some(p=>p.f===f)).length/RUNS})),targetRate:raw[i].filter(r=>r.balances[30]>=500000).length/RUNS}))};
});
const recoveries=M.anchors.map(a=>({f:a.f,day:a.day,cost:costs[a.f],variants:variants.map(v=>{let recovered=0;const totals=Array.from({length:8},()=>[]);for(let run=0;run<RUNS;run++){let net=-costs[a.f],crossed=false;totals[0].push(net);for(let k=1;k<=7;k++){const day=a.day+k;net+=revenue(v,run,day,a.before|M.bit(a.f))-revenue(v,run,day,a.before);totals[k].push(net);crossed ||= net>=0;}if(crossed)recovered++;}return{name:v.name,withinSeven:recovered/RUNS,days:totals.map(summary)};})}));
const schedule=Array.from({length:31},(_,i)=>({day:i+1,paper:I.proposedSchedule.filter(r=>+r.channel===1&&due(r,i)).map(r=>r.event_idx),radio:I.proposedSchedule.filter(r=>+r.channel===2&&due(r,i)).map(r=>({event:r.event_idx,weight:+r.selection_weight}))}));
const output={runs:RUNS,seed:SEED,variants:variants.map(({name,duration})=>({name,duration})),schedule,strategies,recoveries,checks:['v0.1 sampler default equals event-free callback','21 product prices and types match snapshot','facility costs valid','zero-based schedule boundaries','water +30 and antipyretic +30% prices'],assumptions:['8 trades completed at evenly spaced midpoint times; timing is an analysis assumption','same customer/catalog seeds across variants; separate shared event seeds','v0.1 proposed date preference and free purchase; not current runtime gates','no cost deduction, no refusal, reputation fixed neutral','one purchase at settlement with next-day upkeep reserve; cutoff day24','30sec sensitivity holds 8 trades and all other draft assumptions fixed; not actual play forecast','1000 paired simulations; RNG distribution aligned, not C# seed-identical','ROI fixes other facilities for7days, includes displaced sales; first crossing can reverse']};
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify(output,null,2));
console.log(JSON.stringify({strategies:strategies.map(s=>({name:s.name,variants:s.variants.map(v=>({name:v.name,p50:v.final.p50,deltaMean:v.pairedDelta.mean,targetRate:v.targetRate}))})),recoveries:recoveries.map(r=>({f:r.f,base:r.variants[0].withinSeven,proposed:r.variants[2].withinSeven}))},null,2));
