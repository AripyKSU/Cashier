// Offline balance analysis; reuses the reviewed sampler, never writes runtime data.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const model = require('../draft-20260912/model.js');
const root = path.resolve(__dirname, '../../../..');
const readCsv = file => {
  const [header, ...rows] = fs.readFileSync(file, 'utf8').trim().replace(/^\uFEFF/, '').split(/\r?\n/);
  return rows.map(row => Object.fromEntries(row.split(',').map((value, i) => [header.split(',')[i], value])));
};
const live = readCsv(path.join(root, 'Assets/Datas/Customer/ProductData.csv'));
const ids = {'draft-night':'1024', 'draft-detector':'1025'};
assert.equal(live.length, 21);
for (const p of model.products) {
  const row = live.find(r => r.idx === (ids[p.key] || p.key));
  assert.ok(row);
  assert.equal(+row.base_price, p.price);
  assert.equal(+row.product_type, p.type);
  assert.equal(row.required_facility_idx ? +row.required_facility_idx - 12000 : 0, p.facility);
  assert.equal(+row.is_available, 1);
  assert.equal(+row.available_day, 0);
}
const fields = ['idx','disposition_type','preferred_product_types','preferred_selection_chance','min_product_kinds','max_product_kinds','min_quantity','max_quantity'];
const oldDispositions = readCsv(path.join(__dirname, '../draft-20260912/inputs/CustomerDispositionData.csv'));
const dispositions = readCsv(path.join(root, 'Assets/Datas/Customer/CustomerDispositionData.csv'));
assert.deepEqual(dispositions.map(r => fields.map(f => r[f])), oldDispositions.map(r => fields.map(f => r[f])));
const reputation = fs.readFileSync(path.join(root,'Assets/Datas/ReputationBalanceData.csv'),'utf8');
assert.equal(reputation.replace(/\r/g,''), fs.readFileSync(path.join(__dirname,'../draft-20260912/inputs/ReputationBalanceData.csv'),'utf8').replace(/\r/g,''));
const facilities = readCsv(path.join(root, 'Assets/Datas/FacilityData.csv'));
for (let i = 0; i < 6; i++) {
  const facility = facilities.find(r => +r.idx === 12001 + i);
  assert.ok(facility);
  assert.equal(+facility.upgrade_kind, 1);
  assert.equal(+facility.required_store_stage, [1,1,2,2,3,3][i]);
}
const runs = 12000, seed = 20260912;
const scenarios = [];
for (const demand of ['draft', 'flat']) {
  for (let phase = 0; phase < 3; phase++) {
    for (let mask = 0; mask < 64; mask++) {
      const random = model.rng(seed + phase * 7919);
      let sum = 0, sq = 0;
      for (let i = 0; i < runs; i++) {
        const value = model.sample(phase, mask, random, false, demand === 'flat').revenue / 8;
        sum += value; sq += value * value;
      }
      const mean = sum / runs;
      const se = Math.sqrt(Math.max(0, (sq - runs * mean * mean) / (runs - 1)) / runs);
      scenarios.push({demand, phase, mask, facilities: model.facilityNames.slice(1).filter((_,i)=>mask & (1<<i)), mean, se, expected6:mean*6, expected8:mean*8, exactTarget:mean*7.5});
    }
  }
}
const sourceFiles = ['Assets/Datas/Customer/ProductData.csv','Assets/Datas/Customer/CustomerDispositionData.csv','Assets/Datas/ReputationBalanceData.csv','Assets/Datas/FacilityData.csv','doc/work/balancing-reference/draft-20260912/model.js'];
const sources = sourceFiles.map(file => ({file, sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex')}));
fs.writeFileSync(path.join(__dirname,'estimates.json'),JSON.stringify({runs,seed,transactionsPerSample:8,sources,scenarios},null,2));
console.log(JSON.stringify({cases:scenarios.length,daysPerCase:runs,totalSyntheticTrades:scenarios.length*runs*8,baseline:scenarios.filter(r=>[0,1,3,7,15,16,32,48,63].includes(r.mask)&&r.demand==='draft').map(r=>({phase:r.phase,mask:r.mask,mean:Math.round(r.mean),target:Math.round(r.exactTarget)}))},null,2));
