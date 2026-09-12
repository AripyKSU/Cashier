// Offline cost proposal. No runtime writes. Reuse the reviewed demand sampler.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const M = require('../draft-20260912/model.js');
const root = path.resolve(__dirname, '../../../..');
const csv = file => {
  const [header, ...rows] = fs.readFileSync(path.join(root, file), 'utf8').trim().replace(/^\uFEFF/, '').split(/\r?\n/);
  const keys = header.split(',');
  return rows.map(row => Object.fromEntries(row.split(',').map((v, i) => [keys[i], v])));
};
const facilityPath = 'Assets/Datas/FacilityData.csv';
const facilities = csv(facilityPath);
const byId = Object.fromEntries(facilities.map(f => [+f.idx, f]));
const current = Object.fromEntries(facilities.map(f => [+f.idx, +f.purchase_price]));
const initial = +csv('Assets/Datas/EconomyBalanceData.csv')[0].initialBalance;
const maintenance = csv('Assets/Datas/MaintenanceBalanceData.csv').map(r => +r.maintenanceAmount);
const estimatesPath = 'doc/work/balancing-reference/daily-income-20260912/estimates.json';
const estimates = JSON.parse(fs.readFileSync(path.join(root, estimatesPath), 'utf8'));
// Refuse stale demand inputs. Prices alone are deliberately independent of the demand estimates.
for (const s of estimates.sources.filter(s => !s.file.endsWith('FacilityData.csv'))) {
  assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(root, s.file))).digest('hex'), s.sha256, s.file);
}
assert.deepEqual(facilities.filter(f => +f.upgrade_kind === 1).map(f => +f.required_store_stage), [1,1,2,2,3,3]);
assert.deepEqual(facilities.filter(f => +f.upgrade_kind === 3).map(f => [+f.idx,+f.required_store_stage,+f.target_store_stage]), [[12008,1,2],[12010,2,3]]);
assert.equal(initial, 1000);
assert.deepEqual(maintenance, M.maintenance);
const mean = (mask, day, trades = 8) => estimates.scenarios.find(r => r.demand === 'draft' && r.phase === M.phaseOf(day) && r.mask === mask).mean * trades;
const bit = id => M.bit(id - 12000);
const reserve = day => maintenance[day] || 0; // Player strategy, not a runtime purchase restriction.
const reference = [12001,12002,12008,12003,12004,12010,12005,12006];
const targets = [{id:12001,day:3}, {id:12008,day:10}, {id:12003,day:12}, {id:12010,day:20}, {id:12005,day:22}];

function simulate(prices, sequence = reference, trades = 8, run = null, stopBefore = null) {
  let cash = initial, stage = 1, mask = 0, next = 0;
  const daily = [], purchases = [];
  for (let day = 1; day <= 31; day++) {
    const opening = cash, activeMask = mask, stageAtOpen = stage;
    const revenue = run === null ? mean(mask, day, trades)
      : M.sample(M.phaseOf(day), mask, M.rng(20260912 + run * 1009 + day * 104729)).revenue;
    assert.ok(run === null || trades === 8, 'Stochastic sample is exactly eight successful sales');
    cash += revenue;
    // This scoped scenario has no fines and enough cash to pay every day. Fail rather than invent debt rules.
    assert.ok(cash >= maintenance[day - 1], 'Debt scenario requires the runtime settlement model');
    cash -= maintenance[day - 1];
    let spent = 0;
    const bought = [];
    while (next < sequence.length && sequence[next] !== stopBefore) {
      const id = sequence[next], f = byId[id];
      assert.ok(f);
      assert.ok(stage >= +f.required_store_stage, 'Sequence must include required store expansion');
      if (+f.upgrade_kind === 3) assert.equal(+f.target_store_stage, stage + 1);
      if (cash < prices[id] + reserve(day)) break;
      assert.ok(!purchases.some(p => p.id === id));
      const before = mask;
      cash -= prices[id]; spent += prices[id]; next++;
      if (+f.upgrade_kind === 3) stage = +f.target_store_stage;
      else if (+f.upgrade_kind === 1) mask |= bit(id);
      purchases.push({id, day, activeDay:+f.upgrade_kind === 3 ? day : day+1, before, after:mask, price:prices[id]});
      bought.push(id);
    }
    assert.ok(Math.abs(opening + revenue - maintenance[day-1] - spent - cash) < 1e-7);
    daily.push({day,opening,revenue,maintenance:maintenance[day-1],spent,cash,activeMask,stageAtOpen,stage,bought});
  }
  return {daily,purchases};
}

// Fit only the user's five milestones. Other facilities retain their current prices and buy when affordable.
// Expansion price uses the middle of the feasible day window; product price aims for seven days' incremental revenue,
// including the immediately required expansion once. Clamp to the target-day affordability window when necessary.
const proposed = {...current}, calibration = [];
for (const target of targets) {
  const history = simulate(proposed, reference, 8, null, target.id);
  const day = history.daily[target.day-1], previous = history.daily[target.day-2];
  const prefix = reference.slice(0, reference.indexOf(target.id));
  assert.ok(prefix.every(id => history.purchases.some(p => p.id === id && p.day < target.day)));
  const low = Math.floor((previous.cash - reserve(target.day-1)) / 1000) * 1000 + 1000;
  const high = Math.floor((day.cash - reserve(target.day)) / 1000) * 1000;
  assert.ok(low > 0 && high >= low, 'Target window must contain a positive thousand-G price');
  const f = byId[target.id];
  const expansionId = target.id === 12003 ? 12008 : target.id === 12005 ? 12010 : null;
  const expansionCost = expansionId ? proposed[expansionId] : 0;
  const incrementalSeven = +f.upgrade_kind === 1 ? Array.from({length:7}, (_,k) => mean(day.activeMask | bit(target.id),target.day+k+1) - mean(day.activeMask,target.day+k+1)).reduce((a,b)=>a+b,0) : null;
  const desired = incrementalSeven === null ? Math.round((low+high)/2000)*1000 : Math.round((incrementalSeven-expansionCost)/1000)*1000;
  proposed[target.id] = Math.max(low, Math.min(high, desired));
  calibration.push({...target,low,high,incrementalSeven,expansionCost,desired,price:proposed[target.id],clamped:proposed[target.id] !== desired});
}
const base = simulate(current), adjusted = simulate(proposed);
for (const target of targets) assert.equal(adjusted.purchases.find(p=>p.id===target.id)?.day, target.day);

const runs = 2000;
const quantile = (sorted, q) => { const x=(sorted.length-1)*q, lo=Math.floor(x); return sorted[lo]+(sorted[Math.ceil(x)]-sorted[lo])*(x-lo); };
const summary = values => { if (!values.length) return null; const a=values.slice().sort((a,b)=>a-b); return {n:a.length,mean:a.reduce((x,y)=>x+y,0)/a.length,p10:quantile(a,.1),p50:quantile(a,.5),p90:quantile(a,.9)}; };
const stochastic = Object.entries({current,proposed}).map(([name,prices]) => {
  const samples = Array.from({length:runs},(_,i)=>simulate(prices,reference,8,i));
  return {name,bands:Array.from({length:31},(_,i)=>({day:i+1,...summary(samples.map(s=>s.daily[i].cash))})),milestones:targets.map(t=>({...t,...summary(samples.flatMap(s=>s.purchases.filter(p=>p.id===t.id).map(p=>p.day))),reached:samples.filter(s=>s.purchases.some(p=>p.id===t.id)).length/runs,onOrBeforeTarget:samples.filter(s=>s.purchases.some(p=>p.id===t.id && p.day<=t.day)).length/runs}))};
});

// Counterfactual holds other facilities fixed. Expansion costs are charged once, at their actual earlier purchase day.
const recovery = [];
for (const [name,prices,history] of [['current',current,base],['proposed',proposed,adjusted]]) {
  for (const id of [12001,12003,12005]) {
    const p=history.purchases.find(p=>p.id===id), gate=id===12003?12008:id===12005?12010:null;
    const e=gate?history.purchases.find(p=>p.id===gate):null;
    if (!p) continue;
    const start=e?e.day:p.day, end=Math.min(31,p.day+7), cost=prices[id]+(e?prices[gate]:0);
    let incremental=0, crossing=null;
    const curve=[];
    for(let day=start;day<=31;day++) {
      if(day>p.day) incremental+=mean(p.after,day)-mean(p.before,day);
      const invested=prices[id]*(day>=p.day)+(e?prices[gate]:0);
      const net=incremental-invested;
      if(day>p.day && crossing===null && net>=0) crossing=day;
      curve.push({day,incremental,invested,net});
    }
    const seven=p.day+7<=31?curve.find(c=>c.day===p.day+7).incremental:null;
    const nets=[]; let crossed=0;
    for(let run=0;run<runs;run++) {
      let net=-cost, hit=false;
      for(let day=p.day+1;day<=end;day++) {
        const seed=20260912+run*1009+day*104729;
        net+=M.sample(M.phaseOf(day),p.after,M.rng(seed)).revenue-M.sample(M.phaseOf(day),p.before,M.rng(seed)).revenue;
        hit ||= net>=0;
      }
      nets.push(net); if(hit) crossed++;
    }
    recovery.push({name,id,purchaseDay:p.day,expansionDay:e?.day??null,activeDay:p.day+1,before:p.before,after:p.after,cost,seven,sevenDayRoi:seven===null?null:seven/cost-1,crossing,businessDays:crossing===null?null:crossing-p.day,elapsedFromFirstInvestment:crossing===null?null:crossing-start,curve,observedBusinessDays:end-p.day,stochasticNet:summary(nets),crossedRate:crossed/runs});
  }
}
// Whole group: both product facilities and the shared expansion, paid once at the actual dates.
const stageBundles = [
  {stage:1,ids:[12001,12002],before:0},
  {stage:2,ids:[12008,12003,12004],before:3},
  {stage:3,ids:[12010,12005,12006],before:15}
].map(group => {
  const purchases=adjusted.purchases.filter(p=>group.ids.includes(p.id));
  assert.equal(purchases.length,group.ids.length);
  const start=purchases[0].day, lastPurchase=purchases.at(-1).day, cost=purchases.reduce((s,p)=>s+p.price,0);
  let incremental=0, crossing=null;
  const curve=[];
  for(let day=start;day<=31;day++) {
    const mask=purchases.filter(p=>+byId[p.id].upgrade_kind===1 && p.activeDay<=day).reduce((m,p)=>m|bit(p.id),group.before);
    incremental+=mean(mask,day)-mean(group.before,day);
    const invested=purchases.filter(p=>p.day<=day).reduce((s,p)=>s+p.price,0);
    const net=incremental-invested;
    if(day>=lastPurchase && crossing===null && net>=0) crossing=day;
    curve.push({day,incremental,invested,net});
  }
  return {...group,cost,start,lastPurchase,crossing,curve};
});
const sensitivity=[6,8,10].map(trades=>({trades,...simulate(proposed,reference,trades)}));
const alternatives=[{name:'단계별 대표 설비만',sequence:[12001,12008,12003,12010,12005]},{name:'핵보호 우선 저축',sequence:[12008,12010,12005]},{name:'정밀장비 우선 저축',sequence:[12008,12010,12006]},{name:'설비 미구매',sequence:[]}].map(p=>({...p,...simulate(proposed,p.sequence)}));
const sourceFiles=[facilityPath,'Assets/Datas/EconomyBalanceData.csv','Assets/Datas/MaintenanceBalanceData.csv',estimatesPath,'Assets/Scripts/Facility/FacilityService.cs','Assets/Scripts/Customer/CustomerProductAvailability.cs','Assets/Scripts/Customer/CustomerCompositionSelector.cs','Assets/Scripts/Finance/DailyReputationCalculator.cs','Assets/Scripts/Manager/GameSessionManager.cs'];
const sources=sourceFiles.map(file=>({file,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex')}));
const output={initial,maintenance,reference,targets,current,proposed,calibration,base,adjusted,runs,stochastic,recovery,sensitivity,alternatives,sources,assumptions:['conditional base-price sales; eight successful transactions per day; neutral reputation fixed','actual store gates, immediate store expansion, next-day product activation, no product prerequisite chain','reference player chooses food, medicine, stage2, tools, electrical, stage3, nuclear, precision in order','next-day maintenance reserve is player policy; no one-purchase/day limit or day24 cutoff','cost prices not deducted; no price events, reputation evolution, queue departures, fines or convenience effects','mean-income path is not the median acquisition day or an enforced unlock schedule','6/10 transaction sensitivity scales conditional mean income; stochastic sampling is eight trades only'],checks:['demand source hashes match reviewed estimates','six product gate values and two sequential store gates','actual start and all31 maintenance values','cash conservation, no duplicates, immediate expansion and next-day revenue activation','five target dates achieved without date locks','2000 paired runs per price set; all daily maintenance payable in this scoped scenario']};
output.stageBundles=stageBundles;
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify(output,null,2)+'\n');
console.log(JSON.stringify({changes:facilities.filter(f=>current[f.idx]!==proposed[f.idx]).map(f=>({id:f.idx,current:current[f.idx],proposed:proposed[f.idx]})),calibration,base:base.purchases,adjusted:adjusted.purchases,recovery:recovery.map(({curve,...r})=>r),checks:output.checks},null,2));
