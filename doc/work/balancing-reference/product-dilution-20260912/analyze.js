// Offline counterfactuals only. Product prices and all runtime files remain unchanged.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const original = require('../draft-20260912/model.js');
const prior = require('../daily-income-20260912/estimates.json');
const root = path.resolve(__dirname, '../../../..');
for (const s of prior.sources) assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(root,s.file))).digest('hex'),s.sha256);
const csv = file => {
  const [head,...lines]=fs.readFileSync(path.join(root,file),'utf8').trim().replace(/^\uFEFF/,'').split(/\r?\n/);
  return lines.map(line=>Object.fromEntries(line.split(',').map((v,i)=>[head.split(',')[i],v])));
};
const products=original.products;
const dispositions=csv('Assets/Datas/Customer/CustomerDispositionData.csv');
const groups=[1,2,3,4,5].map(type=>dispositions.filter(r=>+r.disposition_type===type).map(r=>r.preferred_product_types.split('_').map(Number)));
const typeWeights=[600,150,50,100,100];
const phaseWeights=[[1.6,1,.45],[1,1.6,1],[1,1.2,1.8]];
const displayWeights=[100,220,220,280,280,350,350];
const variants=[
  {id:'baseline',label:'현재 선호90%',scope:'baseline'},
  {id:'pref50',label:'선호50%',scope:'existing_csv',chance:.5},
  {id:'pref100',label:'선호100%',scope:'existing_csv',chance:1},
  {id:'weight4',label:'고급 진열4배',scope:'code_required',highWeight:4},
  {id:'weight20',label:'고급 진열20배',scope:'diagnostic_code',highWeight:20},
  {id:'all',label:'진열 제한 제거',scope:'diagnostic_code',all:true},
  {id:'uniform',label:'선호 무시',scope:'diagnostic_code',uniform:true},
  {id:'all_uniform',label:'진열 제한·선호 제거',scope:'diagnostic_code',all:true,uniform:true},
  {id:'strict',label:'선호 없으면 미구매',scope:'code_required',strict:true},
  {id:'strict_all',label:'미구매 규칙·전체 진열',scope:'diagnostic_code',strict:true,all:true},
  {id:'top',label:'고가순 진열',scope:'code_required',top:true}
];
function pick(weights,u){let r=u*weights.reduce((a,b)=>a+b,0);for(let i=0;i<weights.length;i++){r-=weights[i];if(r<0)return i;}return weights.length-1;}
function sample(phase,mask,random,variant,demand){
  const ranked=products.map((p,i)=>({i,key:-Math.log(random())/(displayWeights[p.facility]*(p.facility>=5?(variant.highWeight||1):1))})).filter(x=>products[x.i].facility===0||(mask&original.bit(products[x.i].facility)));
  ranked.sort((a,b)=>variant.top ? products[b.i].price-products[a.i].price || a.key-b.key : a.key-b.key);
  const catalog=ranked.slice(0,variant.all?ranked.length:[4,6,8][phase]).map(x=>x.i).sort((a,b)=>a-b);
  let revenue=0,sales=0,missing=0,missingRevenue=0,highRevenue=0;
  for(let trade=0;trade<8;trade++){
    const type=pick(typeWeights,random());
    const weights=groups[type].map(types=>demand==='flat'?1:types.reduce((s,t)=>s+phaseWeights[phase][t<=4?0:t<=6?1:2],0)/types.length);
    const pref=groups[type][pick(weights,random())];
    const yes=catalog.filter(i=>pref.includes(products[i].type)),no=catalog.filter(i=>!pref.includes(products[i].type)),pool=[...catalog];
    const absent=yes.length===0;
    if(absent)missing++;
    const count=1+Math.floor(random()*3);
    let value=0;
    for(let k=0;k<3;k++){
      const uPref=random(),uChoice=random(),quantity=1+Math.floor(random()*3);
      if(k>=count)continue;
      const choices=variant.uniform?pool:yes.length&&(no.length===0||uPref<(variant.chance??.9))?yes:no;
      const index=choices.splice(Math.floor(uChoice*choices.length),1)[0],p=products[index];
      assert.ok(p);
      if(variant.strict&&absent)continue;
      const income=p.price*quantity;
      value+=income;
      if(p.facility>=5)highRevenue+=income;
    }
    if(value>0)sales++;
    revenue+=value;
    if(absent)missingRevenue+=value;
  }
  return {revenue,sales,missing,missingRevenue,highRevenue,highSlots:catalog.filter(i=>products[i].facility>=5).length,slots:catalog.length};
}
// Ensure the diagnostic sampler reproduces the reviewed baseline exactly with identical RNG consumption.
for(const demand of ['flat','draft'])for(let phase=0;phase<3;phase++)for(let mask=0;mask<64;mask++){
  const seed=20260912+mask*173+phase;
  assert.equal(sample(phase,mask,original.rng(seed),variants[0],demand).revenue,original.sample(phase,mask,original.rng(seed),false,demand==='flat').revenue);
}
const runs=3000, seed=20260912, cells=[],edges=[];
for(const demand of ['flat','draft'])for(let phase=0;phase<3;phase++)for(const variant of variants){
  const samples=[];
  for(let mask=0;mask<64;mask++){
    const values=new Float64Array(runs),random=original.rng(seed+phase*7919);
    let sum=0,sq=0,sales=0,missing=0,missingRevenue=0,highRevenue=0,highSlots=0,slots=0;
    for(let i=0;i<runs;i++){
      const s=sample(phase,mask,random,variant,demand),value=s.revenue/8;
      values[i]=value;sum+=value;sq+=value*value;sales+=s.sales;missing+=s.missing;missingRevenue+=s.missingRevenue;highRevenue+=s.highRevenue;highSlots+=s.highSlots;slots+=s.slots;
    }
    const mean=sum/runs,se=Math.sqrt(Math.max(0,(sq-runs*mean*mean)/(runs-1))/runs);
    samples.push(values);
    cells.push({demand,phase,variant:variant.id,mask,mean,se,saleRate:sales/(runs*8),meanPerSale:sales?sum*8/sales:null,missingRate:missing/(runs*8),missingRevenueShare:sum?missingRevenue/(sum*8):0,highRevenueShare:sum?highRevenue/(sum*8):0,highSlots:highSlots/runs,slots:slots/runs});
  }
  for(let before=0;before<64;before++)for(let f=0;f<6;f++)if(!(before&(1<<f))){
    const after=before|(1<<f);
    let sum=0,sq=0,base=0;
    for(let i=0;i<runs;i++){const delta=samples[after][i]-samples[before][i];sum+=delta;sq+=delta*delta;base+=samples[before][i];}
    const delta=sum/runs,se=Math.sqrt(Math.max(0,(sq-runs*delta*delta)/(runs-1))/runs);
    edges.push({demand,phase,variant:variant.id,before,after,facility:12001+f,delta,relative:base?sum/base:null,se,upper95:delta+1.96*se,lower95:delta-1.96*se});
  }
  console.log(`${demand} phase${phase} ${variant.id} completed`);
}
assert.equal(cells.length,4224);assert.equal(edges.length,12672);
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify({runs,seed,variants,sources:prior.sources,baselineEquivalenceChecks:384,cells,edges},null,2));
console.log('Finished: 4224 conditions, 12672 paired upgrade comparisons');
