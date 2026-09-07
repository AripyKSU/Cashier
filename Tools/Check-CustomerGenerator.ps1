# Unity 안에서 순수 생성 로직을 실행한다. 임시 ID는 실제 데이터에 등록하지 않는다.
$ErrorActionPreference = 'Stop'
$checkCode = @'
var config = new CustomerDispositionData { Idx = 1, PreferredProductTypes = new[] { ProductType.Water },
    EntryTextIdxs = new uint[] { 1 }, RegularSaleTextIdxs = new uint[] { 2 }, DiscountSaleTextIdxs = new uint[] { 4 }, ExploitativeSaleTextIdxs = new uint[] { 5 }, RejectTextIdxs = new uint[] { 3 } };
var products = Enumerable.Range(1, 4).ToDictionary(x => (uint)x, x => new ProductData {
    Idx = (uint)x, ProductType = x <= 2 ? ProductType.Water : ProductType.Food, BasePrice = 101, IsAvailable = true });
var appearances = new uint[] { 1, 2 };
var configs = new CustomerDispositionData[] { config };
var generator = new CustomerGenerator(new System.Random(17));
for (int i = 0; i < 1000; i++) {
    var visit = generator.Generate(appearances, configs, products);
    if (visit.Items.Count < 1 || visit.Items.Count > 3 || visit.Items.Select(x => x.ProductIdx).Distinct().Count() != visit.Items.Count || visit.Items.Any(x => x.Quantity < 1 || x.Quantity > 3))
        throw new System.Exception("Range or duplicate check failed");
}
config.MinProductKinds = 3; config.MaxProductKinds = 3;
var one = new System.Collections.Generic.Dictionary<uint, ProductData> { { 3, products[3] } };
var snapshot = generator.Generate(appearances, configs, one);
if (snapshot.Items.Count != 1 || snapshot.Items[0].ProductIdx != 3) throw new System.Exception("Candidate shortage failed");
one.Clear(); config.MinQuantity = 3;
if (snapshot.Items.Count != 1 || generator.Generate(appearances, configs, one) != null) throw new System.Exception("Snapshot or empty candidate failed");
config.MinProductKinds = 1; config.MaxProductKinds = 1; config.MinQuantity = 1;
int preferred = 0;
for (int i = 0; i < 10000; i++) if (products[generator.Generate(appearances, configs, products).Items[0].ProductIdx].ProductType == ProductType.Water) preferred++;
if (preferred < 8700 || preferred > 9300) throw new System.Exception("90 percent selection check failed");
foreach (int chance in new int[] { 0, 1000 }) {
    config.PreferredSelectionChance = chance;
    for (int i = 0; i < 100; i++) {
        var category = products[generator.Generate(appearances, configs, products).Items[0].ProductIdx].ProductType;
        if (category != (chance == 1000 ? ProductType.Water : ProductType.Food)) throw new System.Exception("Probability endpoint failed");
    }
}
config.PreferredSelectionChance = 1000; config.MinProductKinds = 4; config.MaxProductKinds = 4;
if (generator.Generate(appearances, configs, products).Items.Count != 4) throw new System.Exception("Pool exhaustion failed");
var a = new CustomerGenerator(new System.Random(5)).Generate(appearances, configs, products);
var b = new CustomerGenerator(new System.Random(5)).Generate(appearances, configs, products);
if (a.AppearanceIdx != b.AppearanceIdx || !a.Items.Select(x => x.ProductIdx + ":" + x.Quantity).SequenceEqual(b.Items.Select(x => x.ProductIdx + ":" + x.Quantity))) throw new System.Exception("Reproducibility failed");
foreach (int invalidChance in new int[] { -1, 1001 }) {
    config.PreferredSelectionChance = invalidChance;
    bool invalidRejected = false;
    try { generator.Generate(appearances, configs, products); } catch (System.ArgumentException) { invalidRejected = true; }
    if (!invalidRejected) throw new System.Exception("Invalid probability accepted");
}
config.PreferredSelectionChance = 900;
bool rejected = false;
config.MinQuantity = 0;
try { generator.Generate(appearances, configs, products); } catch (System.ArgumentException) { rejected = true; }
if (!rejected) throw new System.Exception("Invalid settings accepted");
config.MinQuantity = 1; rejected = false;
try { generator.Generate(new uint[] { 1, 1 }, configs, products); } catch (System.ArgumentException) { rejected = true; }
if (!rejected) throw new System.Exception("Duplicate appearance accepted");
config.MinProductKinds = config.MaxProductKinds = config.MinQuantity = config.MaxQuantity = 1;
config.PriceTolerance = 1100;
one.Add(3, products[3]);
products[3].AvailableDay = 2;
if (generator.Generate(appearances, configs, one, 1) != null) throw new Exception("Future product appeared");
products[3].IsAvailable = false;
if (generator.Generate(appearances, configs, one, 2) != null) throw new Exception("Disabled product appeared");
products[3].IsAvailable = true;
var trade = generator.Generate(appearances, configs, one, 2);
if (trade.BaseTotal != 101 || trade.AllowedTotal != 111 || trade.State != CustomerState.Entering || trade.FeedbackTextIdx != 1) throw new Exception("Floor or entry failed");
products[3].BasePrice = 10000; config.PriceTolerance = 9999;
if (trade.Items[0].UnitPrice != 101 || trade.BaseTotal != 101 || trade.AllowedTotal != 111) throw new Exception("Price snapshot changed");
trade.BeginOffer();
foreach (long invalid in new long[] { 0, -1 }) {
    bool blocked = false;
    try { trade.SubmitOffer(invalid); } catch (ArgumentOutOfRangeException) { blocked = true; }
    if (!blocked || trade.State != CustomerState.AwaitingOffer || trade.OfferedTotal != null) throw new Exception("Invalid input consumed offer");
}
if (!trade.SubmitOffer(111) || trade.State != CustomerState.Accepted || trade.FeedbackTextIdx != 5) throw new Exception("Boundary acceptance failed");
bool secondBlocked = false;
try { trade.SubmitOffer(1); } catch (InvalidOperationException) { secondBlocked = true; }
if (!secondBlocked || trade.OfferedTotal != 111) throw new Exception("Second offer accepted");
trade.Depart();
if (trade.State != CustomerState.Departed || trade.WasAccepted != true) throw new Exception("Departure lost result");
products[3].BasePrice = 101; config.PriceTolerance = 1100;
var refusal = generator.Generate(appearances, configs, one, 2); refusal.BeginOffer();
if (refusal.SubmitOffer(112) || refusal.State != CustomerState.Rejected || refusal.FeedbackTextIdx != 3) throw new Exception("Over-limit rejection failed");
refusal.Depart();
foreach (long offered in new long[] { 100, 101, 102, 111, 112 }) {
    var visit = generator.Generate(appearances, configs, one, 2);
    visit.BeginOffer();
    visit.SubmitOffer(offered);
    var expected = offered > 111 ? CustomerTradeOutcome.PaymentRefused
        : offered < 101 ? CustomerTradeOutcome.DiscountSale
        : offered == 101 ? CustomerTradeOutcome.RegularSale : CustomerTradeOutcome.ExploitativeSale;
    uint expectedText = expected == CustomerTradeOutcome.PaymentRefused ? 3u
        : expected == CustomerTradeOutcome.DiscountSale ? 4u
        : expected == CustomerTradeOutcome.RegularSale ? 2u : 5u;
    if (visit.Outcome != expected || visit.FeedbackTextIdx != expectedText) throw new Exception("Outcome boundary/dialog failed: " + offered);
    visit.Depart();
    if (visit.Outcome != expected) throw new Exception("Departure lost outcome");
}
config.PriceTolerance = 900;
var cheapRefusal = generator.Generate(appearances, configs, one, 2);
cheapRefusal.BeginOffer(); cheapRefusal.SubmitOffer(100);
if (cheapRefusal.Outcome != CustomerTradeOutcome.PaymentRefused) throw new Exception("Tolerance must precede discount classification");
config.PriceTolerance = 1100;
config.MinQuantity = config.MaxQuantity = 3;
var multiple = generator.Generate(appearances, configs, one, 2);
if (multiple.BaseTotal != 303 || multiple.AllowedTotal != 333 || multiple.Items.Count != 1) throw new Exception("Whole-list total failed");
products[3].BasePrice = uint.MaxValue; config.PriceTolerance = int.MaxValue;
config.MinQuantity = config.MaxQuantity = int.MaxValue - 1;
bool overflowBlocked = false;
try { generator.Generate(appearances, configs, one, 2); } catch (OverflowException) { overflowBlocked = true; }
if (!overflowBlocked) throw new Exception("Overflow accepted");
return "CUSTOMER_CHECK_PASS: generation, probability, day gate, immutable prices, floor, entry/offer/accept/reject/depart, one offer, overflow; preferred=" + preferred + "/10000";
'@
$result = $checkCode | & unity-cli exec 2>&1
$result | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'CUSTOMER_CHECK_PASS:') {
    throw 'Customer generator check did not complete successfully.'
}
