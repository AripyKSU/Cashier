$ErrorActionPreference = 'Stop'
# InitScene에서 새 Play 세션을 시작하고 영업 버튼을 누르기 전에 실행한다.
# 실제 런타임 API로 두 날짜를 진행하므로 사용자 플레이 중에는 실행하지 않는다.
$result = @'
var s=GameSessionManager.Instance;
if(s==null || !s.IsInitialized || s.ElapsedDays!=0 || s.Economy.QueryService.IsDayOpen) throw new Exception("Fresh initialized session required");
var before=s.EnsureDailyPrices();
if(before.IsRadioBroadcast || !before.RadioEventIdx.HasValue || s.AdvanceTradingTime(60,false)) throw new Exception("Broadcast before business");
var c=DataTableManager.Instance.Customers;
var generator=new CustomerGenerator(new System.Random(1));
var visit=generator.Generate(c.Appearances.Rows.Keys.ToArray(),c.Dispositions.Rows.Values.ToArray(),c.Products.Rows,0,()=>s.EnsureDailyPrices().Prices);
var snapshot=visit.Items.Select(x=>x.UnitPrice).ToArray();
s.BeginTradingDay();
if(s.AdvanceTradingTime(60,true) || !object.ReferenceEquals(before,s.EnsureDailyPrices())) throw new Exception("Pause advanced radio");
if(!s.AdvanceTradingTime(60,false) || !s.DailyPrices.IsRadioBroadcast || s.AdvanceTradingTime(60,false)) throw new Exception("Within 60 seconds/exactly once");
if(!snapshot.SequenceEqual(visit.Items.Select(x=>x.UnitPrice))) throw new Exception("Existing visit mutated");
var expected=new PriceEventScheduler(new System.Random(1)).ApplyRadio(before,DataTableManager.Instance.GetDB<PriceEventDataTable>(DataTableType.PriceEvent).Rows,c.Products.Rows);
foreach(var p in expected.Prices) if(s.DailyPrices.Prices[p.Key]!=p.Value) throw new Exception("Broadcast price mismatch");
visit.BeginOffer();
var final=visit.Items.Select(x=>new SaleItem(x.ProductIdx,x.Quantity)).ToArray();
long total=final.Sum(x=>checked((long)x.Quantity*s.DailyPrices.Prices[x.ProductId]));
visit.SubmitOffer(total,final);
if(visit.Result.Value.ReferenceTotal!=total || visit.Result.Value.SoldItems.Any(x=>x.UnitPrice!=s.DailyPrices.Prices[x.ProductId]))throw new Exception("Radio price must apply at submission");
var nextVisit=generator.Generate(c.Appearances.Rows.Keys.ToArray(),c.Dispositions.Rows.Values.ToArray(),c.Products.Rows,0,()=>s.EnsureDailyPrices().Prices);
foreach(var item in nextVisit.Items) if(item.UnitPrice!=s.DailyPrices.Prices[item.ProductIdx]) throw new Exception("New visit price");
s.EndTradingDay();s.CompleteDay(0);var day1=s.EnsureDailyPrices();
s.BeginTradingDay();s.EndTradingDay();
if(s.AdvanceTradingTime(60,false) || s.DailyPrices.IsRadioBroadcast || !object.ReferenceEquals(day1,s.DailyPrices)) throw new Exception("Closed day radio not cancelled");
return "RADIO_TIMING_PASS: no entry broadcast, pause, within 60s, once, snapshots, next visit, early close cancellation";
'@ | unity-cli exec
if ($LASTEXITCODE -ne 0 -or "$result" -notmatch 'RADIO_TIMING_PASS:') { throw "Radio timing check failed: $result" }
$result
