# Unity 안에서 순수 생성 로직을 실행한다. 임시 ID는 실제 데이터에 등록하지 않는다.
$ErrorActionPreference = 'Stop'
$checkCode = @'
var config = new CustomerDispositionData { Idx = 1, PreferredProductTypes = new[] { ProductType.Water },
    EntryTextIdxs = new uint[] { 1 }, RegularSaleTextIdxs = new uint[] { 2 }, DiscountSaleTextIdxs = new uint[] { 4 }, ExploitativeSaleTextIdxs = new uint[] { 5 }, RejectTextIdxs = new uint[] { 3 } };
var products = Enumerable.Range(1, 4).ToDictionary(x => (uint)x, x => new ProductData {
    Idx = (uint)x, ProductType = x <= 2 ? ProductType.Water : ProductType.Food, BasePrice = 101, CostPrice = 50, IsAvailable = true });
var appearances = new uint[] { 1, 2 };
var configs = new CustomerDispositionData[] { config };
var generator = new CustomerGenerator(new System.Random(17));
for (int i = 0; i < 1000; i++) {
    var visit = generator.Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice));
    if (visit.Items.Count < 1 || visit.Items.Count > 3 || visit.Items.Select(x => x.ProductIdx).Distinct().Count() != visit.Items.Count || visit.Items.Any(x => x.Quantity < 1 || x.Quantity > 3))
        throw new System.Exception("Range or duplicate check failed");
}
config.MinProductKinds = 3; config.MaxProductKinds = 3;
var one = new System.Collections.Generic.Dictionary<uint, ProductData> { { 3, products[3] } };
var snapshot = generator.Generate(appearances, configs, one, getCurrentPrices: () => one.ToDictionary(x=>x.Key,x=>x.Value.BasePrice));
if (snapshot.Items.Count != 1 || snapshot.Items[0].ProductIdx != 3) throw new System.Exception("Candidate shortage failed");
one.Clear(); config.MinQuantity = 3;
if (snapshot.Items.Count != 1 || generator.Generate(appearances, configs, one, getCurrentPrices: () => one.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)) != null) throw new System.Exception("Snapshot or empty candidate failed");
config.MinProductKinds = 1; config.MaxProductKinds = 1; config.MinQuantity = 1;
int preferred = 0;
for (int i = 0; i < 10000; i++) if (products[generator.Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)).Items[0].ProductIdx].ProductType == ProductType.Water) preferred++;
if (preferred < 8700 || preferred > 9300) throw new System.Exception("90 percent selection check failed");
foreach (int chance in new int[] { 0, 1000 }) {
    config.PreferredSelectionChance = chance;
    for (int i = 0; i < 100; i++) {
        var category = products[generator.Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)).Items[0].ProductIdx].ProductType;
        if (category != (chance == 1000 ? ProductType.Water : ProductType.Food)) throw new System.Exception("Probability endpoint failed");
    }
}
config.PreferredSelectionChance = 1000; config.MinProductKinds = 4; config.MaxProductKinds = 4;
if (generator.Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)).Items.Count != 4) throw new System.Exception("Pool exhaustion failed");
var a = new CustomerGenerator(new System.Random(5)).Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice));
var b = new CustomerGenerator(new System.Random(5)).Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice));
if (a.AppearanceIdx != b.AppearanceIdx || !a.Items.Select(x => x.ProductIdx + ":" + x.Quantity).SequenceEqual(b.Items.Select(x => x.ProductIdx + ":" + x.Quantity))) throw new System.Exception("Reproducibility failed");
foreach (int invalidChance in new int[] { -1, 1001 }) {
    config.PreferredSelectionChance = invalidChance;
    bool invalidRejected = false;
    try { generator.Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)); } catch (System.ArgumentException) { invalidRejected = true; }
    if (!invalidRejected) throw new System.Exception("Invalid probability accepted");
}
config.PreferredSelectionChance = 900;
bool rejected = false;
config.MinQuantity = 0;
try { generator.Generate(appearances, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)); } catch (System.ArgumentException) { rejected = true; }
if (!rejected) throw new System.Exception("Invalid settings accepted");
config.MinQuantity = 1; rejected = false;
try { generator.Generate(new uint[] { 1, 1 }, configs, products, getCurrentPrices: () => products.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)); } catch (System.ArgumentException) { rejected = true; }
if (!rejected) throw new System.Exception("Duplicate appearance accepted");
config.MinProductKinds = config.MaxProductKinds = config.MinQuantity = config.MaxQuantity = 1;
config.PriceTolerance = 1100;
one.Add(3, products[3]);
products[3].AvailableDay = 2;
if (generator.Generate(appearances, configs, one, 1, () => one.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)) != null) throw new Exception("Future product appeared");
products[3].IsAvailable = false;
if (generator.Generate(appearances, configs, one, 2, () => one.ToDictionary(x=>x.Key,x=>x.Value.BasePrice)) != null) throw new Exception("Disabled product appeared");
products[3].IsAvailable = true;
// 제출 전 미확정. 테스트용 현재가를 별도로 소유한다.
var prices=products.ToDictionary(x=>x.Key,x=>101u);
int reads=0;
Func<CustomerVisit> create=()=>generator.Generate(appearances,configs,one,2,()=>{reads++;return prices;});
var trade=create();
if(trade.BaseTotal.HasValue || trade.AllowedTotal.HasValue || trade.Result.HasValue)throw new Exception("Premature final amounts");
trade.BeginOffer();
var original=trade.Items;
var final=new List<SaleItem>{new SaleItem(3,1),new SaleItem(3,2)};
prices[3]=200;int beforeReads=reads;
trade.SubmitOffer(600,final);
if(reads!=beforeReads+1 || trade.BaseTotal!=600 || trade.AllowedTotal!=660 || trade.Result.Value.CostTotal!=150 || trade.Result.Value.SoldItems.Single().Quantity!=3)throw new Exception("Latest price/duplicate/cost");
final.Clear();prices[3]=999;products[3].CostPrice=99;
if(trade.Result.Value.SoldItems.Single().UnitPrice!=200 || trade.Result.Value.CostTotal!=150 || !ReferenceEquals(original,trade.Items) || trade.Items[0].Quantity!=1)throw new Exception("Mutable result/initial list");
bool blocked=false;try{trade.SubmitOffer(1,new[]{new SaleItem(3,1)});}catch(InvalidOperationException){blocked=true;}
if(!blocked || trade.BaseTotal!=600)throw new Exception("Resubmission");
trade.Depart();if(!trade.Result.HasValue || trade.State!=CustomerState.Departed)throw new Exception("Depart result");
prices[3]=101;products[3].CostPrice=50;
// 최초 희망 상품 3과 다른 상품 4로 최종 목록을 교체한다.
one.Add(4,products[4]);products[4].IsAvailable=false;
foreach(long offered in new long[]{201,202,203,223}){
 var visit=create();visit.BeginOffer();visit.SubmitOffer(offered,new[]{new SaleItem(4,2)});
 var expected=offered>222?CustomerTradeOutcome.PaymentRefused:offered<202?CustomerTradeOutcome.DiscountSale:offered==202?CustomerTradeOutcome.RegularSale:CustomerTradeOutcome.ExploitativeSale;
 var result=visit.Result.Value;
 if(visit.Outcome!=expected || visit.Items.Any(x=>x.ProductIdx!=3) || result.ReferenceTotal!=202 || result.ReputationDelta!=0)throw new Exception("Final replacement outcome");
 if(expected==CustomerTradeOutcome.PaymentRefused){if(result.SaleIncome!=0 || result.CostTotal!=0 || result.SoldItems.Count!=0)throw new Exception("Rejected financial contents");}
 else if(result.SaleIncome!=offered || result.CostTotal!=100 || result.SoldItems.Single().ProductId!=4)throw new Exception("Accepted contents");
}
var invalid=create();invalid.BeginOffer();
Action<Action> reject=action=>{bool failed=false;try{action();}catch(Exception){failed=true;}if(!failed || invalid.Result.HasValue || invalid.BaseTotal.HasValue || invalid.AllowedTotal.HasValue || invalid.OfferedTotal.HasValue || invalid.State!=CustomerState.AwaitingOffer)throw new Exception("Failure mutated visit");};
reject(()=>invalid.SubmitOffer(0,new[]{new SaleItem(3,1)}));
reject(()=>invalid.SubmitOffer(-1,new[]{new SaleItem(3,1)}));
reject(()=>invalid.SubmitOffer(1,null));
reject(()=>invalid.SubmitOffer(1,Array.Empty<SaleItem>()));
reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(9999,1)}));
reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(3,0)}));
reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(3,-1)}));
reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(3,int.MaxValue),new SaleItem(3,1)}));
prices.Remove(3);reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(3,1)}));
prices[3]=0;reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(3,1)}));
prices[3]=uint.MaxValue;prices[4]=uint.MaxValue;
reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(3,int.MaxValue),new SaleItem(4,int.MaxValue)}));
prices[3]=1;prices[4]=1;products[3].CostPrice=uint.MaxValue;products[4].CostPrice=uint.MaxValue;
reject(()=>invalid.SubmitOffer(1,new[]{new SaleItem(3,int.MaxValue),new SaleItem(4,int.MaxValue)}));
products[3].CostPrice=50;products[4].CostPrice=50;prices[3]=101;prices[4]=101;
invalid.SubmitOffer(101,new[]{new SaleItem(3,1)});if(invalid.Outcome!=CustomerTradeOutcome.RegularSale)throw new Exception("Valid retry after failure");
bool throwLookup=false;
var lookupVisit=generator.Generate(appearances,configs,one,2,()=>throwLookup?throw new InvalidOperationException("lookup failure"):prices);
lookupVisit.BeginOffer();throwLookup=true;blocked=false;
try{lookupVisit.SubmitOffer(1,new[]{new SaleItem(3,1)});}catch(InvalidOperationException){blocked=true;}
if(!blocked || lookupVisit.Result.HasValue || lookupVisit.State!=CustomerState.AwaitingOffer)throw new Exception("Lookup failure");
throwLookup=false;lookupVisit.SubmitOffer(101,new[]{new SaleItem(3,1)});
if(lookupVisit.Outcome!=CustomerTradeOutcome.RegularSale)throw new Exception("Lookup failure retry");
products[3].CostPrice=200;products[3].Validate();products[3].CostPrice=50;
config.PriceTolerance=int.MaxValue;var limit=create();limit.BeginOffer();prices[3]=uint.MaxValue;blocked=false;
try{limit.SubmitOffer(1,new[]{new SaleItem(3,int.MaxValue)});}catch(OverflowException){blocked=true;}
if(!blocked || limit.Result.HasValue || limit.AllowedTotal.HasValue)throw new Exception("Allowed overflow");
var legacy=new TransactionResult(10,2);
if(legacy.Outcome!=CustomerTradeOutcome.None || legacy.ReferenceTotal.HasValue || legacy.OfferedTotal.HasValue || legacy.SoldItems.Count!=0 || legacy.SaleIncome!=10)throw new Exception("Legacy result");
return "CUSTOMER_CHECK_PASS: generation, probability, final replacement four outcomes, latest price once, immutable lists/prices/cost, duplicates, rejection, invalid/lookup/overflow atomicity, resubmit, legacy; preferred="+preferred+"/10000";

'@
$result = $checkCode | & unity-cli exec 2>&1
$result | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'CUSTOMER_CHECK_PASS:') {
    throw 'Customer generator check did not complete successfully.'
}
