const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../../..'),runs=10000,prices=[250000,500000,750000,1000000,1500000];
const R=require('../reputation-20260912/calculate.js');
const currentFacilityCosts=Object.fromEntries(fs.readFileSync(path.join(root,'Assets/Datas/FacilityData.csv'),'utf8').trim().split(/\r?\n/).slice(1).map(row=>{const c=row.split(',');return [+c[0],+c[2]];}));
assert.deepEqual(Object.keys(currentFacilityCosts).map(Number).filter(id=>!(id in R.I.proposedCosts)),[12012]);
assert.deepEqual(Object.fromEntries(Object.entries(currentFacilityCosts).filter(([id])=>+id!==12012)),R.I.proposedCosts);
assert.equal(currentFacilityCosts[12012],1000000);
R.I.proposedCosts[12012]=currentFacilityCosts[12012];
const Q=require('../citizenship-queue-20260913/calculate.js'),C=require('../citizenship-20260913/calculate.js');
const sequence=[12001,12002,12007,12008,12003,12004,12009,12010,12005,12006,12011];
const read=file=>{const [h,...rows]=fs.readFileSync(path.join(root,file),'utf8').trim().replace(/^\uFEFF/,'').split(/\r?\n/);const keys=h.split(',');return rows.map(row=>Object.fromEntries(row.split(',').map((v,i)=>[keys[i],v])));};
const facilityFile='Assets/Datas/FacilityData.csv',economyFile='Assets/Datas/EconomyBalanceData.csv',maintenanceFile='Assets/Datas/MaintenanceBalanceData.csv';
const facilities=Object.fromEntries(read(facilityFile).map(x=>[+x.idx,x])),costs=Object.fromEntries(Object.entries(facilities).map(([id,x])=>[id,+x.purchase_price]));
const initial=+read(economyFile)[0].initialBalance,maintenance=read(maintenanceFile).map(x=>+x.maintenanceAmount);
const facilityTotal=sequence.reduce((sum,id)=>sum+costs[id],0);

const eligible=(owned,cash,price,unpaid,failed)=>owned===sequence.length&&cash>=price&&!unpaid&&!failed;
function simulate(count,price,run){
  let cash=initial,unpaid=0,grace=null,rep=0,mask=0,stage=1,next=0,failed=false,hit=null;
  const purchases=[];let processedDays=0;
  for(let day=1;day<=31&&!failed&&hit===null;day++){
    processedDays++;
    const opening=cash,s=Q.daySample(day,mask,rep,count,true,'jitter',run);
    const settlement=C.settleCash(cash+s.revenue,unpaid,grace,day,maintenance[day-1]+s.penalties);
    ({cash,unpaid,grace,failed}=settlement);
    let spent=0;
    while(!failed&&!unpaid&&next<sequence.length){
      const id=sequence[next],f=facilities[id];
      assert.ok(stage>=+f.required_store_stage);
      if(cash<costs[id]+(maintenance[day]||0))break;
      cash-=costs[id];spent+=costs[id];next++;purchases.push({id,day});
      if(+f.upgrade_kind===3){assert.equal(+f.target_store_stage,stage+1);stage=+f.target_store_stage;}else mask|=1<<(id-12001);
    }
    if(eligible(next,cash,price,unpaid,failed)){cash-=price;spent+=price;hit=day;}
    assert.equal(cash,opening+s.revenue-settlement.paid-spent);
    rep=s.next;
  }
  return {hit,failed,cash,purchases,processedDays};
}

function probability(success,n){
  const z=1.959963984540054,p=success/n,d=1+z*z/n,c=(p+z*z/(2*n))/d,m=z*Math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d;
  return {success,n,rate:p,low:Math.max(0,c-m),high:Math.min(1,c+m)};
}
const median=a=>{if(!a.length)return null;const s=[...a].sort((x,y)=>x-y);return s[Math.floor((s.length-1)/2)];};
function checks(){
  Q.checks();
  assert.equal(facilityTotal,524500);assert.deepEqual(sequence.map(id=>+facilities[id].required_store_stage),[1,1,1,1,2,2,2,2,3,3,3]);
  for(let run=0;run<100;run++){
    const low=simulate(10,250000,run),high=simulate(10,1500000,run);
    for(const s of [low,high]){assert.ok(s.purchases.length<=11);if(s.hit!==null){assert.equal(s.purchases.length,11);assert.equal(s.processedDays,s.hit);assert.ok(s.cash>=0);}}
    assert.ok(!(high.hit!==null&&low.hit===null));
  }
  assert.equal(eligible(10,2000000,250000,0,false),false);
  assert.equal(eligible(11,249999,250000,0,false),false);
  assert.equal(eligible(11,250000,250000,0,false),true);
  const noMoney=simulate(8,Number.MAX_SAFE_INTEGER,0);assert.equal(noMoney.hit,null);assert.equal(noMoney.processedDays,31);
}
function hash(file){return crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex');}

if(require.main===module){
  checks();
  const prior=JSON.parse(fs.readFileSync(path.join(__dirname,'../citizenship-queue-20260913/inputs.json')));
  const sources=[...prior.sources.map(x=>x.file),facilityFile,economyFile,maintenanceFile].filter((x,i,a)=>a.indexOf(x)===i);
  fs.mkdirSync(__dirname,{recursive:true});
  fs.writeFileSync(path.join(__dirname,'inputs.json'),JSON.stringify({runs,days:31,prices,counts:[8,10,12],currentCsvCitizenshipPrice:costs[12012],facilityTotal,sequence,costs:Object.fromEntries(sequence.map(id=>[id,costs[id]])),sources:sources.map(file=>({file,sha256:hash(file)}))},null,2)+'\n');
  const scenarios=[];
  for(const count of [8,10,12])for(const price of prices){
    const raw=Array.from({length:runs},(_,run)=>simulate(count,price,run)),success=raw.filter(x=>x.hit!==null),failed=raw.filter(x=>x.failed).length;
    const result={count,price,...probability(success.length,runs),conditionalPurchaseDayMedian:median(success.map(x=>x.hit)),failed,unmet:runs-success.length-failed};
    scenarios.push(result);console.log(JSON.stringify(result));
  }
  for(const count of [8,10,12])for(let i=1;i<prices.length;i++)assert.ok(scenarios.find(x=>x.count===count&&x.price===prices[i]).rate<=scenarios.find(x=>x.count===count&&x.price===prices[i-1]).rate);
  fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify({runs,days:31,facilityTotal,scenarios},null,2)+'\n');
  console.log('Checks passed: cost/ownership, insufficient funds, immediate stop, conservation, source hashes, monotonic rates.');
}
module.exports={simulate,eligible,checks};
