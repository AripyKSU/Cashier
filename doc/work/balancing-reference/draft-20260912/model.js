// Offline draft only. Run with Node from any directory; no runtime data writes.
const fs = require('node:fs');
const path = require('node:path');
// These numeric CSVs contain no quoted delimiters. TextData is deliberately not parsed here.
function csv(relative) {
  // Preserve the reviewed v0.1 inputs when the runtime CSVs are updated.
  const [header, ...lines] = fs.readFileSync(path.join(__dirname, 'inputs', path.basename(relative)), 'utf8').trim().split(/\r?\n/);
  const keys = header.replace(/^\uFEFF/, '').split(',');
  return lines.map(line => Object.fromEntries(line.split(',').map((v, i) => [keys[i], v])));
}
const names = {1001:'물',1004:'통조림',1005:'분말 수프',1006:'영양바',1007:'붕대',1010:'건전지',1013:'연고',1014:'응급 주사',1015:'손전등',1016:'접이식 삽',1018:'무전기',1019:'배터리',1020:'방독면',1021:'방호복',1022:'방사능 측정기',1023:'열화상 카메라'};
const products = csv('Assets/Datas/Customer/ProductData.csv').map(p => ({key:p.idx,name:names[p.idx],type:+p.product_type,originalPrice:+p.base_price,price:p.idx==='1005'?350:p.idx==='1006'?450:+p.base_price,cost:+p.cost_price,facility:p.idx==='1022'?5:p.required_facility_idx?+p.required_facility_idx-12000:0}));
for(const [key,name,type,price,facility] of [['1011','성냥',4,150,0],['1009','해열제',3,700,2],['1017','쇠지렛대',5,1200,3],['draft-night','야간 투시경',7,6500,6],['draft-detector','휴대용 탐지기',7,7000,6]]) products.push({key,name,type,price,originalPrice:null,cost:price/2,facility});
const facilityNames=['기본','식량 선반','약품 보관장','공구대','전력·통신','핵보호','정밀장비'];
const dispositions=csv('Assets/Datas/Customer/CustomerDispositionData.csv');
const neutral=csv('Assets/Datas/ReputationBalanceData.csv').find(r=>+r.min_reputation<=0&&+r.max_reputation>=0);
const typeWeights=[+neutral.normal_weight,+neutral.hasty_weight,+neutral.price_sensitive_weight,+neutral.wealthy_weight,+neutral.poor_weight];
const groups=[1,2,3,4,5].map(type=>dispositions.filter(r=>+r.disposition_type===type).map(r=>({id:r.idx,types:r.preferred_product_types.split('_').map(Number)})));
const phases=[[1.6,1,.45],[1,1.6,1],[1,1.2,1.8]];
const phaseOf=day=>day<10?0:day<20?1:2;
const bandOf=type=>type<=4?0:type<=6?1:2;
const weightsByPhase=phases.map(w=>groups.map(rows=>rows.map(r=>r.types.reduce((s,t)=>s+w[bandOf(t)],0)/r.types.length)));
const displayWeights=[100,220,220,280,280,350,350];
const bit=f=>f?1<<(f-1):0;
const maintenance=csv('Assets/Datas/MaintenanceBalanceData.csv').map(r=>+r.maintenanceAmount);
maintenance.push(3200); // Day 31 is missing in current CSV; draft linear extension.
const SEED=20260912, CACHE_DAYS=4000, RUNS=5000, START=1000, TARGET=500000;
function rng(seed){let s=seed>>>0;return()=>{s=(Math.imul(s,1664525)+1013904223)>>>0;return(s+.5)/4294967296;};}
function pick(weights,u){let r=u*weights.reduce((a,b)=>a+b,0);for(let i=0;i<weights.length;i++){r-=weights[i];if(r<0)return i;}return weights.length-1;}
function sample(phase, mask, random, detailed=false, flat=false, priceAt=(p)=>p.price){
  // Exponential race equals weighted sampling without replacement. Draw all keys for paired comparisons.
  const ranked=products.map((p,i)=>({i,key:-Math.log(random())/displayWeights[p.facility]})).filter(x=>products[x.i].facility===0||(mask&bit(products[x.i].facility)));
  ranked.sort((a,b)=>a.key-b.key);
  const catalog=ranked.slice(0,[4,6,8][phase]).map(x=>x.i).sort((a,b)=>a-b);
  let revenue=0,soldMask=0,preferredMask=0;
  const units=[0,0,0],byFacility=Array(7).fill(0);
  for(let trade=0;trade<8;trade++){
    const type=pick(typeWeights,random());
    const row=pick(flat?groups[type].map(()=>1):weightsByPhase[phase][type],random());
    const pref=groups[type][row].types;
    preferredMask|=catalog.reduce((m,i)=>pref.includes(products[i].type)?m|bit(products[i].facility):m,0);
    const yes=catalog.filter(i=>pref.includes(products[i].type)),no=catalog.filter(i=>!pref.includes(products[i].type));
    const count=1+Math.floor(random()*3);
    for(let k=0;k<3;k++){
      const uPref=random(),uChoice=random(),quantity=1+Math.floor(random()*3);
      if(k>=count)continue;
      const choices=yes.length&&(no.length===0||uPref<.9)?yes:no;
      const index=choices.splice(Math.floor(uChoice*choices.length),1)[0],p=products[index];
      const price=priceAt(p,trade);
      revenue+=price*quantity; soldMask|=bit(p.facility);
      if(detailed){units[bandOf(p.type)]+=quantity;byFacility[p.facility]+=price*quantity;}
    }
  }
  return {revenue,soldMask,preferredMask,units,byFacility};
}
const plans=[{name:'저가부터 재투자',sequence:[1,2,3,4,5,6]},{name:'중간 설비 중심',sequence:[2,3,4,5,6]},{name:'핵보호 직행',sequence:[5,6]},{name:'정밀장비 직행',sequence:[6,5]},{name:'무구매 기준',sequence:[]}];
const masks=new Set([0,63]);
for(const plan of plans){let mask=0;for(const f of plan.sequence){mask|=bit(f);masks.add(mask);}}
const anchors=[{f:1,day:3,before:0},{f:2,day:8,before:1},{f:3,day:12,before:3},{f:4,day:15,before:7},{f:5,day:20,before:15},{f:6,day:23,before:31}];
for(const a of anchors){masks.add(a.before);masks.add(a.before|bit(a.f));}
// Reuse the reviewed demand sampler for data-only event comparisons.
module.exports={sample,rng,phaseOf,bit,plans,anchors,maintenance,products,facilityNames};
if(require.main===module){
const cache={};
for(const mask of masks)for(let phase=0;phase<3;phase++){
  const random=rng(SEED+phase*7919),units=[0,0,0],byFacility=Array(7).fill(0);let sum=0,sq=0;
  for(let i=0;i<CACHE_DAYS;i++){const s=sample(phase,mask,random,true);sum+=s.revenue;sq+=s.revenue**2;s.units.forEach((v,j)=>units[j]+=v);s.byFacility.forEach((v,j)=>byFacility[j]+=v);}
  cache[mask+':'+phase]={mean:sum/CACHE_DAYS,se:Math.sqrt((sq/CACHE_DAYS-(sum/CACHE_DAYS)**2)/CACHE_DAYS),units:units.map(x=>x/CACHE_DAYS),byFacility:byFacility.map(x=>x/CACHE_DAYS)};
}
const mean=(mask,day)=>cache[mask+':'+phaseOf(day)].mean;
const costs=Array(7).fill(0);
for(const a of anchors){a.after=a.before|bit(a.f);a.expectedSeven=0;for(let d=a.day+1;d<=a.day+7;d++)a.expectedSeven+=mean(a.after,d)-mean(a.before,d);costs[a.f]=Math.max(1000,Math.round(a.expectedSeven/1000)*1000);a.cost=costs[a.f];}
function runPlan(plan,runIndex,expected=false){
  let balance=START,mask=0,next=0;const daily=[],purchases=[];let firstPreferred=null,firstSale=null;
  for(let day=1;day<=31;day++){
    const sampleDay=expected?{revenue:mean(mask,day),preferredMask:0,soldMask:0}:sample(phaseOf(day),mask,rng(SEED+runIndex*1009+day*104729));
    balance+=sampleDay.revenue-maintenance[day-1];
    if(purchases.length&&!expected){const first=purchases[0];if(firstPreferred===null&&(sampleDay.preferredMask&bit(first.f)))firstPreferred=day-first.day;if(firstSale===null&&(sampleDay.soldMask&bit(first.f)))firstSale=day-first.day;}
    let bought=null;
    // Explicit comparison policy: one purchase per settlement, sequence order, next day's upkeep reserve, no purchases after day 24.
    if(day<=24&&next<plan.sequence.length&&balance>=costs[plan.sequence[next]]+maintenance[day]){bought=plan.sequence[next++];balance-=costs[bought];mask|=bit(bought);purchases.push({day,f:bought});}
    daily.push({day,revenue:sampleDay.revenue,balance,mask,bought});
  }
  return {daily,purchases,firstPreferred,firstSale};
}
function quantile(a,q){const s=a.slice().sort((a,b)=>a-b),index=(s.length-1)*q,l=Math.floor(index);return s[l]+(s[Math.ceil(index)]-s[l])*(index-l);}
function summary(a){return a.length?{n:a.length,mean:a.reduce((s,x)=>s+x,0)/a.length,p10:quantile(a,.1),p50:quantile(a,.5),p90:quantile(a,.9)}:null;}
const strategyResults=plans.map(plan=>{
  const runs=Array.from({length:RUNS},(_,i)=>runPlan(plan,i)),expected=runPlan(plan,0,true);
  const bands=Array.from({length:31},(_,d)=>({day:d+1,...summary(runs.map(r=>r.daily[d].balance))}));
  const purchases=plan.sequence.map(f=>({f,...(summary(runs.flatMap(r=>r.purchases.filter(p=>p.f===f).map(p=>p.day)))||{n:0}),rate:runs.filter(r=>r.purchases.some(p=>p.f===f)).length/RUNS}));
  return {...plan,expected:expected.daily,expectedPurchases:expected.purchases,bands,purchases,final:summary(runs.map(r=>r.daily[30].balance)),targetRate:runs.filter(r=>r.daily[30].balance>=TARGET).length/RUNS,deficitRate:runs.filter(r=>r.daily.some(d=>d.balance<0)).length/RUNS,firstPreferredWait:summary(runs.map(r=>r.firstPreferred).filter(x=>x!==null)),firstSaleWait:summary(runs.map(r=>r.firstSale).filter(x=>x!==null))};
});
const recoveries=anchors.map(a=>{
  const curves=Array.from({length:31-a.day+1},()=>[]),recovered=[];let withinSeven=0;
  for(let i=0;i<RUNS;i++){
    let cumulative=-a.cost,recovery=null;curves[0].push(cumulative);
    for(let day=a.day+1;day<=31;day++){
      const seed=SEED+i*1009+day*104729;
      cumulative+=sample(phaseOf(day),a.after,rng(seed)).revenue-sample(phaseOf(day),a.before,rng(seed)).revenue;
      curves[day-a.day].push(cumulative);if(recovery===null&&cumulative>=0)recovery=day-a.day;
    }
    if(recovery!==null){recovered.push(recovery);if(recovery<=7)withinSeven++;}
  }
  return {...a,bands:curves.map((values,i)=>({day:a.day+i,elapsed:i,...summary(values)})),recovery:summary(recovered),withinSeven:withinSeven/RUNS,by31:recovered.length/RUNS};
});
const flatDemand=[];
for(let phase=0;phase<3;phase++){const random=rng(SEED+phase*7919),units=[0,0,0];for(let i=0;i<CACHE_DAYS;i++){const s=sample(phase,63,random,true,true);s.units.forEach((v,j)=>units[j]+=v);}flatDemand.push(units.map(v=>v/CACHE_DAYS));}
const assumptions={seed:SEED,cacheDays:CACHE_DAYS,runs:RUNS,start:START,currentStart:100000,citizenshipTarget:TARGET,phases,typeWeights,maintenance,displayWeights,excluded:['가격 이벤트','할인·거절·이탈','명성의 시간 변화','편의설비 처리량 효과','벌금·치료 등','가게 단계 구매 제한'],purchaseCutoff:24};
if(products.length!==21||new Set(products.map(p=>p.key)).size!==21)throw Error('Product snapshot invalid');
if(typeWeights.reduce((a,b)=>a+b,0)!==1000)throw Error('Disposition weights invalid');
for(const s of strategyResults){let b=START,m=0;for(const d of s.expected){b+=d.revenue-maintenance[d.day-1]-(d.bought?costs[d.bought]:0);if(Math.abs(b-d.balance)>.001)throw Error('Cash conservation failed');if(d.bought&&(m&bit(d.bought)))throw Error('Duplicate purchase');m=d.mask;}if(s.bands.some(b=>b.p10>b.p50||b.p50>b.p90))throw Error('Quantiles invalid');}
for(const c of Object.values(cache)){if(Math.abs(c.units.reduce((a,b)=>a+b,0)-32)>.6)throw Error('Order quantity check failed');}
const output={assumptions,products,facilityNames,costs,anchors,cache,strategyResults,recoveries,flatDemand,checks:['21 unique products','1000 disposition weight sum','cash conservation','no duplicate expected purchases','quantile ordering','mean units about 32/day']};
fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify(output,null,2));
console.log(JSON.stringify({costs,strategies:strategyResults.map(s=>({name:s.name,purchases:s.expectedPurchases,final:s.final.p50,targetRate:s.targetRate})),recoveries:recoveries.map(r=>({f:r.f,withinSeven:r.withinSeven,recovery:r.recovery?.p50})),checks:output.checks},null,2));
}
