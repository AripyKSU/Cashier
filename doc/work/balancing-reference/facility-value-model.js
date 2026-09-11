// Offline balance snapshot, 2026-09-11. Run: node doc/work/balancing-reference/facility-value-model.js
// No runtime CSV writes. Product tuple: model key, product type, draft price, unlock group.
// New string keys are analysis labels, not allocated runtime IDs.
// Neutral reputation; no price events, rejection or abandonment; eight completed trades/day.
// Daily weighted sampling and preference sampling mirror current C# rules, not System.Random's sequence.
// Quantity uses its exact expectation (2); Monte Carlo error is not player income variance.

const products = [
[1001,1,100,0],[1004,2,250,0],[1007,3,300,0],[1010,4,200,0],[1011,4,150,0],
[1005,2,150,1],[1006,2,200,1],[1013,3,500,2],[1014,3,900,2],[1009,3,700,2],
[1015,5,800,3],[1016,5,1000,3],[1017,5,1200,3],[1018,6,2000,4],[1019,6,1200,4],
[1020,7,2500,5],[1021,7,4000,5],[1022,7,5000,5],[1023,7,6000,6],["night",7,6500,6],["detector",7,7000,6]];
const profiles = [[.6,[[1,2],[5],[6],[7]]],[.05,[[4],[1,2],[3,4]]],[.1,[[6,7],[5],[3]]],[.15,[[3]]],[.1,[[2],[1],[4],[5]]]];
const weight = [100,220,220,280,280,350,350];
function simulate(facilities, kinds, seed=20260911, days=100000) {
 let state=seed>>>0;
 const rnd=()=> {state=(Math.imul(state,1664525)+1013904223)>>>0;return state/4294967296;};
 const pool=products.map((p,i)=>i).filter(i=>products[i][3]<=facilities);
 const units=products.map(()=>0); let sum=0,sq=0,changedSum=0,changedSq=0;
 for(let day=0;day<days;day++){
  const candidates=pool.slice(),catalog=[];
  while(catalog.length<Math.min(kinds,pool.length)){
   let roll=rnd()*candidates.reduce((s,i)=>s+weight[products[i][3]],0),j=0;
   for(;j<candidates.length-1;j++){roll-=weight[products[candidates[j]][3]];if(roll<0)break;}
   catalog.push(candidates.splice(j,1)[0]);
  }
  catalog.sort((a,b)=>a-b);
  let daily=0,changedDaily=0;
  for(let t=0;t<8;t++){
   let r=rnd(),g=0; for(;g<profiles.length-1;g++){r-=profiles[g][0];if(r<0)break;}
   const prefs=profiles[g][1][Math.floor(rnd()*profiles[g][1].length)];
   const yes=catalog.filter(i=>prefs.includes(products[i][1])),no=catalog.filter(i=>!prefs.includes(products[i][1]));
   const count=1+Math.floor(rnd()*3);
   for(let k=0;k<count;k++){
    const useYes=yes.length>0&&(no.length===0||rnd()<.9);
    const choices=useYes?yes:no;
    const i=choices.splice(Math.floor(rnd()*choices.length),1)[0];
    units[i]+=2;daily+=2*products[i][2];
    changedDaily+=2*(products[i][0]===1005?350:products[i][0]===1006?450:products[i][2]);
   }
  }
  sum+=daily;sq+=daily*daily;changedSum+=changedDaily;changedSq+=changedDaily*changedDaily;
 }
 return {facilities,kinds,days,revenue:sum/days,se:Math.sqrt(Math.max(0,sq/days-(sum/days)**2)/days),revised:changedSum/days,revisedSe:Math.sqrt(Math.max(0,changedSq/days-(changedSum/days)**2)/days),units:units.map(x=>x/days)};
}
const cases=[[0,4],[1,4],[2,4],[2,6],[3,6],[4,6],[4,8],[5,8],[6,8]];
const results=cases.map(([f,k])=>simulate(f,k));

console.log(JSON.stringify({seed:20260911,products,results},null,2));
