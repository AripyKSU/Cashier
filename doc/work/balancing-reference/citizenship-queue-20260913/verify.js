const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const S=require('./calculate.js'),R=require('../reputation-20260912/calculate.js');
const data=JSON.parse(fs.readFileSync(path.join(__dirname,'unity-fixtures.json'),'utf8'));
assert.equal(data.fixtures.length,60);
for(const f of data.fixtures){
  let id=0;
  const q=S.queueVisits(()=>{const i=id++;assert.ok(i<f.patience.length);return {id:i,row:{patience:f.patience[i]}};},f.ends);
  assert.deepEqual(q.served.map(v=>v.id),f.servedIds);
  assert.equal(q.abandoned,f.abandoned);assert.equal(q.generated,f.generated);
  const trades=f.dispositionIds.map(id=>R.classify(R.I.tables.dispositions.find(r=>+r.idx===id),1000,1000));
  const rep=R.settle(f.reputation,trades);
  assert.equal(rep.score,f.score);assert.equal(rep.delta,f.delta);
}
console.log('60/60 actual Unity traces: identical served arrival IDs, abandoned/generated counts, reputation score and delta.');
