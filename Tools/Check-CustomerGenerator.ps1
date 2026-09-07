# Unity 안에서 순수 생성 로직을 실행한다. 임시 ID는 실제 데이터에 등록하지 않는다.
$ErrorActionPreference = 'Stop'
$checkCode = @'
var config = new CustomerDispositionData { Idx = 1, PreferredCategoryIds = new uint[] { 1 } };
var products = new System.Collections.Generic.Dictionary<uint, uint> { { 1, 1 }, { 2, 1 }, { 3, 2 }, { 4, 2 } };
var appearances = new uint[] { 1, 2 };
var configs = new CustomerDispositionData[] { config };
var generator = new CustomerGenerator(new System.Random(17));
for (int i = 0; i < 1000; i++) {
    var visit = generator.Generate(appearances, configs, products);
    if (visit.Items.Count < 1 || visit.Items.Count > 3 || visit.Items.Select(x => x.ProductIdx).Distinct().Count() != visit.Items.Count || visit.Items.Any(x => x.Quantity < 1 || x.Quantity > 3))
        throw new System.Exception("Range or duplicate check failed");
}
config.MinProductKinds = 3; config.MaxProductKinds = 3;
var one = new System.Collections.Generic.Dictionary<uint, uint> { { 3, 2 } };
var snapshot = generator.Generate(appearances, configs, one);
if (snapshot.Items.Count != 1 || snapshot.Items[0].ProductIdx != 3) throw new System.Exception("Candidate shortage failed");
one.Clear(); config.MinQuantity = 3;
if (snapshot.Items.Count != 1 || generator.Generate(appearances, configs, one) != null) throw new System.Exception("Snapshot or empty candidate failed");
config.MinProductKinds = 1; config.MaxProductKinds = 1; config.MinQuantity = 1;
int preferred = 0;
for (int i = 0; i < 10000; i++) if (products[generator.Generate(appearances, configs, products).Items[0].ProductIdx] == 1) preferred++;
if (preferred < 8700 || preferred > 9300) throw new System.Exception("90 percent selection check failed");
foreach (int percent in new int[] { 0, 100 }) {
    config.PreferredSelectionPercent = percent;
    for (int i = 0; i < 100; i++) {
        uint category = products[generator.Generate(appearances, configs, products).Items[0].ProductIdx];
        if (category != (percent == 100 ? 1u : 2u)) throw new System.Exception("Probability endpoint failed");
    }
}
config.PreferredSelectionPercent = 100; config.MinProductKinds = 4; config.MaxProductKinds = 4;
if (generator.Generate(appearances, configs, products).Items.Count != 4) throw new System.Exception("Pool exhaustion failed");
var a = new CustomerGenerator(new System.Random(5)).Generate(appearances, configs, products);
var b = new CustomerGenerator(new System.Random(5)).Generate(appearances, configs, products);
if (a.AppearanceIdx != b.AppearanceIdx || !a.Items.Select(x => x.ProductIdx + ":" + x.Quantity).SequenceEqual(b.Items.Select(x => x.ProductIdx + ":" + x.Quantity))) throw new System.Exception("Reproducibility failed");
bool rejected = false;
config.MinQuantity = 0;
try { generator.Generate(appearances, configs, products); } catch (System.ArgumentException) { rejected = true; }
if (!rejected) throw new System.Exception("Invalid settings accepted");
config.MinQuantity = 1; rejected = false;
try { generator.Generate(new uint[] { 1, 1 }, configs, products); } catch (System.ArgumentException) { rejected = true; }
if (!rejected) throw new System.Exception("Duplicate appearance accepted");
return "CUSTOMER_CHECK_PASS: range, uniqueness, shortage, empty, snapshot, probability, exhaustion, seed, invalid inputs; preferred=" + preferred + "/10000";
'@
$result = $checkCode | & unity-cli exec 2>&1
$result | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'CUSTOMER_CHECK_PASS:') {
    throw 'Customer generator check did not complete successfully.'
}
