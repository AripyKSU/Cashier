using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>실제 CSV parser와 FK 공개 경계를 각 음성 사례로 검사한다.</summary>
public sealed class CustomerCsvTests
{
    /// <summary>실제 성향 CSV의 성별·연령 대사가 생성과 전량 제외 결과까지 연결됩니다.</summary>
    [Test]
    public void NoSaleItemsDialogueRoutesActualProfilesAndSurvivesDeparture()
    {
        var catalog = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(),
            new ProductCategoryDataTable(), new ProductDataTable());
        catalog.Appearances.LoadData(File.ReadAllText("Assets/Datas/Customer/CustomerAppearanceData.csv"));
        catalog.Dispositions.LoadData(File.ReadAllText("Assets/Datas/Customer/CustomerDispositionData.csv"));
        catalog.Categories.LoadData(File.ReadAllText("Assets/Datas/Customer/ProductCategoryData.csv"));
        catalog.Products.LoadData(File.ReadAllText("Assets/Datas/Customer/ProductData.csv"));
        var texts = new TextDataTable();
        texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));
        catalog.ValidateAndCommit(texts, loadResources(), facilities: loadFacilities());
        var prices = CustomerProductAvailability.GetAvailableProducts(catalog.Products.Rows, 20)
            .ToDictionary(product => product.Idx, product => product.BasePrice);
        var seen = new HashSet<(CustomerDispositionType, CustomerAttributes)>();
        foreach (CustomerDispositionData row in catalog.Dispositions.Rows.Values)
        {
            var selector = new CustomerCompositionSelector(new System.Random(37));
            for (int i = 0; i < 64; i++)
            {
                CustomerComposition composition = selector.SelectCompositionUniform(
                    CustomerAppearanceFixtures.Create(), new[] { row }, catalog.Products.Rows, prices, 20);
                var candidates = row.GetProfileDialogue(composition.Attributes,
                    row.MaleNoSaleItemsTextIdxs, row.FemaleNoSaleItemsTextIdxs,
                    row.MaleChildNoSaleItemsTextIdxs, row.FemaleChildNoSaleItemsTextIdxs,
                    row.MaleElderlyNoSaleItemsTextIdxs, row.FemaleElderlyNoSaleItemsTextIdxs,
                    row.NoSaleItemsTextIdxs);
                Assert.That(candidates, Does.Contain(composition.NoSaleItemsTextIdx));
                Assert.That(composition.NoSaleItemsTextIdx, Is.Not.EqualTo(composition.RejectTextIdx));
                Assert.That(texts.Rows[composition.NoSaleItemsTextIdx].Text, Is.Not.Empty);
                CustomerVisit visit = new CustomerGenerator().Generate(composition, catalog.Products.Rows, () => prices);
                Assert.That(visit.RejectionReason, Is.EqualTo(CustomerRejectionReason.None));
                visit.BeginOffer();
                Assert.That(visit.SubmitOffer(0, Array.Empty<SaleItem>()), Is.False);
                Assert.That(visit.FeedbackTextIdx, Is.EqualTo(composition.NoSaleItemsTextIdx));
                visit.Depart();
                Assert.That(visit.FeedbackTextIdx, Is.EqualTo(composition.NoSaleItemsTextIdx));
                Assert.That(visit.RejectionReason, Is.EqualTo(CustomerRejectionReason.NoSaleItems));
                seen.Add((row.DispositionType, composition.Attributes));
            }
        }
        Assert.That(seen.Count, Is.EqualTo(14), "일반 6프로필과 나머지 4성향의 남녀 8프로필");
    }

    /// <summary>빈 외형 이미지 FK만 허용하고 필수 헤더·잘못된 값·없는 참조는 거부한다.</summary>
    /// <param name="value">첫 외형의 시험 값 또는 헤더 누락 표시.</param>
    /// <param name="accepted">전체 카탈로그 공개 성공 여부.</param>
    [TestCase("", true)]
    [TestCase("0", false)]
    [TestCase("3001", false)]
    [TestCase("4999", false)]
    [TestCase("invalid", false)]
    [TestCase("missing-header", false)]
    public void AppearanceImageAllowsOnlyBlankOrValidResource(string value, bool accepted)
    {
        var catalog = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(),
            new ProductCategoryDataTable(), new ProductDataTable());
        var texts = new TextDataTable();
        var resources = loadResources();
        var facilities = loadFacilities();
        string source = File.ReadAllText("Assets/Datas/Customer/CustomerAppearanceData.csv");
        string csv = value == "missing-header" ? source.Replace("image_resource_idx", "wrong_header") :
            Regex.Replace(source, @"(?m)^(5001,[^,\r\n]*,)[^,\r\n]*", match => match.Groups[1].Value + value);
        Action load = () =>
        {
            catalog.Appearances.LoadData(csv);
            catalog.Dispositions.LoadData(File.ReadAllText("Assets/Datas/Customer/CustomerDispositionData.csv"));
            catalog.Categories.LoadData(File.ReadAllText("Assets/Datas/Customer/ProductCategoryData.csv"));
            catalog.Products.LoadData(File.ReadAllText("Assets/Datas/Customer/ProductData.csv"));
            texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));
            catalog.ValidateAndCommit(texts, resources, facilities: facilities);
        };
        if (accepted)
        {
            Assert.DoesNotThrow(() => load());
            Assert.That(catalog.Appearances.Rows[5001].ImageResourceIdx, Is.Null);
        }
        else
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^(CustomerAppearanceData\.csv|\[Customer CSV\])"));
            Assert.Catch(() => load());
            Assert.That(catalog.Appearances.GetDataCount(), Is.Zero);
        }
    }

    /// <summary>실제 경제 CSV의 시작금과 20일 유지비가 런타임 서비스까지 전달되는지 확인한다.</summary>
    [Test]
    public void EconomyCsvSupportsDayTwentySettlement()
    {
        var economy = new EconomyBalanceDataTable();
        economy.LoadData(File.ReadAllText("Assets/Datas/EconomyBalanceData.csv"));
        Assert.That(economy.GetData().InitialBalance, Is.EqualTo(10000));

        var maintenance = new MaintenanceBalanceDataTable();
        maintenance.LoadData(File.ReadAllText("Assets/Datas/MaintenanceBalanceData.csv"));
        long[] amounts = maintenance.GetMaintenanceAmounts();
        var service = new MaintenanceService(new FinanceService(amounts.Sum()), amounts);
        for (int day = 1; day <= amounts.Length; day++)
            Assert.That(service.TryPay(day, out _), Is.True);

        Assert.That(amounts.Length, Is.EqualTo(20));
        Assert.That(service.GetRequiredAmount(20), Is.EqualTo(250000));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetRequiredAmount(21));
        Assert.That(service.LastPaidDay, Is.EqualTo(20));
    }

    /// <summary>상품 16종의 20일 리밸런싱 판매가와 원가를 검증한다.</summary>
    [Test]
    public void ProductCsvUsesRebalancedPricesAndCosts()
    {
        var table = new ProductDataTable();
        table.LoadData(File.ReadAllText("Assets/Datas/Customer/ProductData.csv"));
        var expectedPrices = new Dictionary<uint, uint>
        {
            [1001] = 1000, [1010] = 2000, [1004] = 2500, [1007] = 3000,
            [1005] = 5000, [1006] = 6000, [1013] = 8000, [1014] = 10000,
            [1015] = 15000, [1016] = 20000, [1019] = 25000, [1018] = 30000,
            [1020] = 50000, [1021] = 60000, [1022] = 80000, [1023] = 100000
        };
        var expectedCosts = new Dictionary<uint, uint>
        {
            [1001] = 500, [1010] = 1000, [1004] = 1250, [1007] = 1500,
            [1005] = 2500, [1006] = 3000, [1013] = 4000, [1014] = 5000,
            [1015] = 7500, [1016] = 10000, [1019] = 12500, [1018] = 15000,
            [1020] = 25000, [1021] = 30000, [1022] = 40000, [1023] = 50000
        };
        Assert.That(table.Rows.Count, Is.EqualTo(16));
        foreach (var expected in expectedPrices)
        {
            Assert.That(table.Rows[expected.Key].BasePrice, Is.EqualTo(expected.Value));
            Assert.That(table.Rows[expected.Key].CostPrice, Is.EqualTo(expectedCosts[expected.Key]));
            Assert.That(table.Rows[expected.Key].BasePrice, Is.GreaterThan(0));
            Assert.That(table.Rows[expected.Key].CostPrice, Is.GreaterThan(0));
            Assert.That(table.Rows[expected.Key].CostPrice, Is.LessThanOrEqualTo(table.Rows[expected.Key].BasePrice));
        }
    }

    /// <summary>날짜 경계·활성 설비·최고 단계 최소 등장과 고정 seed 재현성을 검증한다.</summary>
    [Test]
    public void DailyProductSelectorUsesRebalancedKindsAndStageGuarantee()
    {
        var products = new ProductDataTable();
        products.LoadData(File.ReadAllText("Assets/Datas/Customer/ProductData.csv"));
        var facilities = new FacilityDataTable();
        facilities.LoadData(File.ReadAllText("Assets/Datas/FacilityData.csv"));
        var expectedCounts = new Dictionary<uint, int> { [0] = 4, [5] = 4, [6] = 6, [11] = 6, [12] = 8, [19] = 8 };
        Func<uint, bool> allProductFacilities = facilityIdx => facilityIdx == 12001 || facilityIdx == 12002 ||
            facilityIdx == 12003 || facilityIdx == 12004 || facilityIdx == 12005 || facilityIdx == 12006;
        foreach (var expected in expectedCounts)
        {
            Func<uint, bool> active = facilityIdx => expected.Key >= 12
                ? allProductFacilities(facilityIdx)
                : expected.Key >= 6 && (facilityIdx == 12001 || facilityIdx == 12002);
            var selector = new DailyProductSelector(new System.Random(20260921));
            var selected = selector.Select(products.Rows, facilities.Rows, expected.Key, active);
            Assert.That(selected.Count, Is.EqualTo(expected.Value), $"elapsedDays={expected.Key}");
            Assert.That(selected.Select(product => product.Idx).Distinct().Count(), Is.EqualTo(selected.Count));
        }

        var first = new DailyProductSelector(new System.Random(42)).Select(products.Rows, facilities.Rows, 12, allProductFacilities);
        var second = new DailyProductSelector(new System.Random(42)).Select(products.Rows, facilities.Rows, 12, allProductFacilities);
        Assert.That(second.Select(product => product.Idx), Is.EqualTo(first.Select(product => product.Idx)));
        var stages = first.Select(product => CustomerProductAvailability.GetUnlockStage(product, facilities.Rows));
        Assert.That(stages.Count(stage => stage == ProductUnlockStage.Stage3), Is.GreaterThanOrEqualTo(2));

        var stageOneOnly = new DailyProductSelector(new System.Random(7)).Select(
            products.Rows, facilities.Rows, 12, facilityIdx => facilityIdx == 12001 || facilityIdx == 12002);
        Assert.That(stageOneOnly.Any(product => product.RequiredFacilityIdx == 12003 || product.RequiredFacilityIdx == 12004 ||
            product.RequiredFacilityIdx == 12005 || product.RequiredFacilityIdx == 12006), Is.False);
    }

    /// <summary>정상 파일의 행 수·조회·routing·초기값을 확인한다.</summary>
    [Test]
    public void ValidCatalogAndRouting()
    {
string root = "Assets/Datas/Customer/";
string appearance = File.ReadAllText(root + "CustomerAppearanceData.csv");
string disposition = File.ReadAllText(root + "CustomerDispositionData.csv");
string category = File.ReadAllText(root + "ProductCategoryData.csv");
string product = File.ReadAllText(root + "ProductData.csv");
string texts = File.ReadAllText("Assets/Datas/TextData.csv");
var textTables = new Dictionary<CustomerCatalog, TextDataTable>();
Func<CustomerCatalog> load = () => {
 var c = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
 textTables.Add(c, new TextDataTable());
 c.Appearances.LoadData(appearance); c.Dispositions.LoadData(disposition);
 c.Categories.LoadData(category); c.Products.LoadData(product);
 textTables[c].LoadData(texts);
 return c;
};
var valid = load();
if (valid.Products.GetDataCount() != 0) throw new Exception("Published before FK validation");
valid.ValidateAndCommit(textTables[valid], loadResources(), facilities: loadFacilities());
if (!valid.Appearances.TryGetData(5001, out _) || !valid.Dispositions.TryGetData(6001, out _) || !valid.Categories.TryGetData(7001, out _) || !valid.Products.TryGetData(1001, out var queriedProduct) || !object.ReferenceEquals(queriedProduct, valid.Products.Rows[1001]) || !textTables[valid].TryGetData(8001, out _) || valid.Products.TryGetData(0, out _)) throw new Exception("Concrete table lookup failed");
if (valid.Appearances.GetDataCount() != 60 || valid.Dispositions.GetDataCount() != 15 || valid.Categories.GetDataCount() != 7 || valid.Products.GetDataCount() != 16 || textTables[valid].GetDataCount() != 527)
    throw new Exception($"Unexpected sample counts: appearances={valid.Appearances.GetDataCount()}, dispositions={valid.Dispositions.GetDataCount()}, categories={valid.Categories.GetDataCount()}, products={valid.Products.GetDataCount()}, texts={textTables[valid].GetDataCount()}");
foreach (CustomerAttributes gender in new[] { CustomerAttributes.Male, CustomerAttributes.Female })
foreach (CustomerAttributes age in new[] { CustomerAttributes.Child, CustomerAttributes.Elderly, CustomerAttributes.Adult })
    Assert.That(valid.Appearances.Rows.Values.Any(row => row.Gender == gender && row.Age == age && row.DispositionType == CustomerDispositionType.Normal), Is.True, $"{gender}/{age}");
Assert.That(valid.Appearances.Rows.Values.Count(row => row.Gender == CustomerAttributes.Male), Is.EqualTo(30));
Assert.That(valid.Appearances.Rows.Values.Count(row => row.Gender == CustomerAttributes.Female), Is.EqualTo(30));
Assert.That(valid.Appearances.Rows.Values.Count(row => row.DispositionType == CustomerDispositionType.Normal && row.Age == CustomerAttributes.Adult), Is.EqualTo(24));
Assert.That(valid.Appearances.Rows.Values.Count(row => row.DispositionType == CustomerDispositionType.Normal && row.Age == CustomerAttributes.Child), Is.EqualTo(6));
Assert.That(valid.Appearances.Rows.Values.Count(row => row.DispositionType == CustomerDispositionType.Normal && row.Age == CustomerAttributes.Elderly), Is.EqualTo(6));
foreach (CustomerDispositionType type in new[] { CustomerDispositionType.Hasty, CustomerDispositionType.PriceSensitive, CustomerDispositionType.Wealthy, CustomerDispositionType.Poor })
    Assert.That(valid.Appearances.Rows.Values.Count(row => row.DispositionType == type && row.Age == CustomerAttributes.Adult), Is.EqualTo(6), type.ToString());
Assert.That(textTables[valid].GetCurrencyUnit(), Is.EqualTo("원"));
Assert.That(textTables[valid].GetCurrencyFormat(), Is.EqualTo("{0:N0} 원"));
var expectedProductIds = new uint[] { 1001, 1004, 1005, 1006, 1007, 1010, 1013, 1014, 1015, 1016, 1018, 1019, 1020, 1021, 1022, 1023 };
if (!valid.Products.Rows.Keys.OrderBy(x => x).SequenceEqual(expectedProductIds)) throw new Exception("Unexpected final product IDs");
if (valid.Products.Rows.Values.Count(x => !x.RequiredFacilityIdx.HasValue) != 4) throw new Exception("Unexpected default product count");
var expectedFacilityProductCounts = new Dictionary<uint, int> { [12001] = 2, [12002] = 2, [12003] = 2, [12004] = 2, [12005] = 2, [12006] = 2 };
if (expectedFacilityProductCounts.Any(expected => valid.Products.Rows.Values.Count(x => x.RequiredFacilityIdx == expected.Key) != expected.Value)) throw new Exception("Unexpected facility product grouping");
if (textTables[valid].Rows[valid.Products.Rows[1005].NameIdx].Text != "군용식량" ||
    textTables[valid].Rows[valid.Products.Rows[1013].NameIdx].Text != "약통" ||
    valid.Products.Rows[1022].RequiredFacilityIdx != 12006) throw new Exception("Final product routing failed");
if (textTables[valid].Rows[valid.Products.Rows[1001].NameIdx].Text != "물") throw new Exception("nameidx lookup failed");
if (Util.GetDataTableType(1001) != DataTableType.Product || Util.GetDataTableType(2001) != DataTableType.EconomyBalance || Util.GetDataTableType(3001) != DataTableType.MaintenanceBalance || Util.GetDataTableType(4001) != DataTableType.Resource || Util.GetDataTableType(8001) != DataTableType.Text) throw new Exception("Routing failed");
if ((uint)DataTableType.DataTableType_End != (uint)DataTableType.StoreStage + 1) throw new Exception("End marker must follow the last table");
if (valid.Dispositions.Rows.Values.Any(x => x.PreferredSelectionChance != 900)) throw new Exception("Probability migration failed");

Assert.That(valid.Dispositions.Rows.Values.All(x=>x.RegularPriceMinRate==1000 && x.RegularPriceMaxRate==1000));
Assert.That(valid.Dispositions.Rows.Values.All(x=>x.PreferredProductIdxs.Count==0));
Assert.That(valid.Dispositions.Rows.OrderBy(x=>x.Key).Select(x=>x.Value.DispositionType), Is.EqualTo(new[]{CustomerDispositionType.Normal,CustomerDispositionType.Hasty,CustomerDispositionType.PriceSensitive,CustomerDispositionType.Normal,CustomerDispositionType.Normal,CustomerDispositionType.Normal,CustomerDispositionType.Wealthy,CustomerDispositionType.PriceSensitive,CustomerDispositionType.PriceSensitive,CustomerDispositionType.Wealthy,CustomerDispositionType.Wealthy,CustomerDispositionType.Poor,CustomerDispositionType.Poor,CustomerDispositionType.Poor,CustomerDispositionType.Poor}));
var normal = valid.Dispositions.Rows.Values.Where(x => x.DispositionType == CustomerDispositionType.Normal);
Assert.That(normal.All(x => x.EntryTextIdxs.SequenceEqual(new uint[] { 8024, 8025 }) && x.RegularSaleTextIdxs.SequenceEqual(new uint[] { 8026, 8027 }) &&
    x.DiscountSaleTextIdxs.SequenceEqual(new uint[] { 8203, 8204 }) && x.ExploitativeSaleTextIdxs.SequenceEqual(new uint[] { 8205, 8206 }) && x.RejectTextIdxs.SequenceEqual(new uint[] { 8028, 8029 })), Is.True);
Assert.That(normal.All(x => x.MaleEntryTextIdxs.SequenceEqual(new uint[] { 8249, 8250 }) &&
    x.FemaleEntryTextIdxs.SequenceEqual(new uint[] { 8263, 8264 }) &&
    x.MaleQueueWarningTextIdxs.SequenceEqual(new uint[] { 8259, 8260 }) &&
    x.FemaleQueueLeaveTextIdxs.SequenceEqual(new uint[] { 8275, 8276 })), Is.True);
Assert.That(normal.All(x => x.MaleChildEntryTextIdxs.SequenceEqual(new uint[] { 8422, 8423 }) &&
    x.FemaleChildEntryTextIdxs.SequenceEqual(new uint[] { 8436, 8437 }) &&
    x.MaleElderlyEntryTextIdxs.SequenceEqual(new uint[] { 8450, 8451 }) &&
    x.FemaleElderlyEntryTextIdxs.SequenceEqual(new uint[] { 8464, 8465 }) &&
    x.MaleChildQueueLeaveTextIdxs.SequenceEqual(new uint[] { 8434, 8435 }) &&
    x.FemaleElderlyQueueLeaveTextIdxs.SequenceEqual(new uint[] { 8476, 8477 })), Is.True);
Assert.That(valid.Dispositions.Rows.Values.Where(x => x.DispositionType != CustomerDispositionType.Normal)
    .All(x => x.MaleChildEntryTextIdxs.Count == 0 && x.FemaleChildEntryTextIdxs.Count == 0 &&
        x.MaleElderlyEntryTextIdxs.Count == 0 && x.FemaleElderlyEntryTextIdxs.Count == 0), Is.True);
var hasty = valid.Dispositions.Rows.Values.Single(x => x.DispositionType == CustomerDispositionType.Hasty);
Assert.That(hasty.EntryTextIdxs, Is.EqualTo(new uint[] { 8030, 8031 })); Assert.That(hasty.RegularSaleTextIdxs, Is.EqualTo(new uint[] { 8032, 8033 }));
Assert.That(hasty.DiscountSaleTextIdxs, Is.EqualTo(new uint[] { 8207, 8208 })); Assert.That(hasty.ExploitativeSaleTextIdxs, Is.EqualTo(new uint[] { 8209, 8210 })); Assert.That(hasty.RejectTextIdxs, Is.EqualTo(new uint[] { 8034, 8035 }));
var wealthy = valid.Dispositions.Rows.Values.Where(x => x.DispositionType == CustomerDispositionType.Wealthy);
Assert.That(wealthy.All(x => x.EntryTextIdxs.SequenceEqual(new uint[] { 8211, 8212 }) && x.RegularSaleTextIdxs.SequenceEqual(new uint[] { 8213, 8214 }) &&
    x.DiscountSaleTextIdxs.SequenceEqual(new uint[] { 8215, 8216 }) && x.ExploitativeSaleTextIdxs.SequenceEqual(new uint[] { 8217, 8218 }) && x.RejectTextIdxs.SequenceEqual(new uint[] { 8219, 8220 })), Is.True);
var poor = valid.Dispositions.Rows.Values.Where(x => x.DispositionType == CustomerDispositionType.Poor);
Assert.That(poor.All(x => x.EntryTextIdxs.SequenceEqual(new uint[] { 8221, 8222 }) && x.RegularSaleTextIdxs.SequenceEqual(new uint[] { 8223, 8224 }) &&
    x.DiscountSaleTextIdxs.SequenceEqual(new uint[] { 8225, 8226 }) && x.ExploitativeSaleTextIdxs.SequenceEqual(new uint[] { 8227, 8228 }) && x.RejectTextIdxs.SequenceEqual(new uint[] { 8229, 8230 })), Is.True);
var priceSensitive = valid.Dispositions.Rows.Values.Where(x => x.DispositionType == CustomerDispositionType.PriceSensitive);
Assert.That(priceSensitive.All(x => x.EntryTextIdxs.SequenceEqual(new uint[] { 8036, 8037 }) && x.RegularSaleTextIdxs.SequenceEqual(new uint[] { 8038, 8039 }) &&
    x.DiscountSaleTextIdxs.SequenceEqual(x.RegularSaleTextIdxs) && x.ExploitativeSaleTextIdxs.SequenceEqual(x.RegularSaleTextIdxs) && x.RejectTextIdxs.SequenceEqual(new uint[] { 8040, 8041 })), Is.True);
    }
    /// <summary>명명된 잘못된 파일 하나가 LogError와 예외를 내며 공개되지 않는지 확인한다.</summary>
    /// <param name="name">오류 사례.</param>
    [TestCase("no sale header")]
    [TestCase("no sale empty")]
    [TestCase("no sale duplicate")]
    [TestCase("no sale zero")]
    [TestCase("no sale FK")]
    [TestCase("no sale gender empty")]
    [TestCase("no sale age empty")]
    [TestCase("old probability header")]
    [TestCase("negative probability")]
    [TestCase("probability overflow")]
    [TestCase("header")]
    [TestCase("PK duplicate")]
    [TestCase("PK range")]
    [TestCase("product enum")]
    [TestCase("disposition enum")]
    [TestCase("boolean")]
    [TestCase("quantity")]
    [TestCase("product nameidx")]
    [TestCase("appearance nameidx")]
    [TestCase("appearance gender")]
    [TestCase("appearance age")]
    [TestCase("appearance disposition")]
    [TestCase("appearance normal FK")]
    [TestCase("appearance combination")]
    [TestCase("disposition nameidx")]
    [TestCase("category nameidx")]
    [TestCase("empty text")]
    [TestCase("duplicate text")]
    [TestCase("missing text table")]
    [TestCase("enum string")]
    [TestCase("duplicate category type")]
    [TestCase("missing category display")]
    [TestCase("base price")]
    [TestCase("negative day")]
    [TestCase("zero image")]
    [TestCase("image FK")]
    [TestCase("entry dialog FK")]
    [TestCase("empty entry")]
    [TestCase("duplicate entry")]
    [TestCase("accept dialog FK")]
    [TestCase("reject dialog FK")]
    [TestCase("price tolerance")]
    [TestCase("queue patience")]
    [TestCase("queue warning FK")]
    [TestCase("queue leave FK")]
    [TestCase("queue header")]
    [TestCase("cost header")]
    [TestCase("zero cost")]
    [TestCase("type header")]
    [TestCase("product preference header")]
[TestCase("regular min header")]
[TestCase("regular max header")]
[TestCase("minimum tolerance header")]
[TestCase("minimum tolerance overflow")]
[TestCase("minimum tolerance above maximum")]
    [TestCase("type 0")]
[TestCase("type 6")]
    [TestCase("type 99")]
    [TestCase("type Normal")]
    [TestCase("type empty")]
    [TestCase("preferred product 0")]
    [TestCase("preferred product 1001_1001")]
    [TestCase("preferred product 1999")]
    [TestCase("regular min 0")]
    [TestCase("regular min -1")]
    [TestCase("regular min 1001")]
    [TestCase("regular min empty")]
    [TestCase("regular max 999")]
    [TestCase("regular max empty")]
    public void RejectInvalidCsv(string name)
    {
string root = "Assets/Datas/Customer/";
string appearance = File.ReadAllText(root + "CustomerAppearanceData.csv");
string disposition = File.ReadAllText(root + "CustomerDispositionData.csv");
string category = File.ReadAllText(root + "ProductCategoryData.csv");
string product = File.ReadAllText(root + "ProductData.csv");
string texts = File.ReadAllText("Assets/Datas/TextData.csv");
var textTables = new Dictionary<CustomerCatalog, TextDataTable>();
Func<CustomerCatalog> load = () => {
 var c = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
 textTables.Add(c, new TextDataTable());
 c.Appearances.LoadData(appearance); c.Dispositions.LoadData(disposition);
 c.Categories.LoadData(category); c.Products.LoadData(product);
 textTables[c].LoadData(texts);
 return c;
};

var c=load();
Action mutate;
switch(name) {
case "no sale header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("no_sale_items_text_idxs", "missing_no_sale_column")); break;
case "no sale empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8513_8514_8515_8516", "")); break;
case "no sale duplicate": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8513_8514_8515_8516", "8513_8513")); break;
case "no sale zero": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8513_8514_8515_8516", "0")); break;
case "no sale FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8513_8514_8515_8516", "8999")); break;
case "no sale gender empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",8513_8514,", ",,")); break;
case "no sale age empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",8517_8518,", ",,")); break;
case "old probability header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("preferred_selection_chance", "preferred_selection_percent")); break;
case "negative probability": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",900,", ",-1,")); break;
case "probability overflow": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",900,", ",1001,")); break;
case "header": mutate=()=>c.Products.LoadData(product.Replace("product_type", "category_idx")); break;
case "PK duplicate": mutate=()=>c.Products.LoadData(product.TrimEnd() + "\n" + product.Split('\n')[1]); break;
case "PK range": mutate=()=>c.Products.LoadData(product.Replace("1001,", "9001,")); break;
case "product enum": mutate=()=>c.Products.LoadData(product.Replace("1001,8012,1", "1001,8012,99")); break;
case "disposition enum": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8006,3,", "8006,99,")); break;
case "boolean": mutate=()=>c.Products.LoadData(product.Replace("1001,8012,1,1", "1001,8012,1,2")); break;
case "quantity": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",900,1,3,1,3", ",900,1,3,0,3")); break;
case "product nameidx": mutate=()=>c.Products.LoadData(product.Replace("1001,8012", "1001,8999")); break;
case "appearance nameidx": mutate=()=>c.Appearances.LoadData(appearance.Replace("5001,8001", "5001,8999")); break;
case "appearance gender": mutate=()=>c.Appearances.LoadData(appearance.Replace("5001,8001,4201,4318,2,16,1", "5001,8001,4201,4318,3,16,1")); break;
case "appearance age": mutate=()=>c.Appearances.LoadData(appearance.Replace("5001,8001,4201,4318,2,16,1", "5001,8001,4201,4318,2,12,1")); break;
case "appearance disposition": mutate=()=>c.Appearances.LoadData(appearance.Replace("5001,8001,4201,4318,2,16,1", "5001,8001,4201,4318,2,16,99")); break;
case "appearance normal FK": mutate=()=>c.Appearances.LoadData(appearance.Replace("5001,8001,4201,4318", "5001,8001,4201,0")); break;
case "appearance combination": mutate=()=>c.Appearances.LoadData(appearance.Replace(",2,4,1", ",2,16,1")); break;
case "disposition nameidx": mutate=()=>c.Dispositions.LoadData(disposition.Replace("6001,8005", "6001,0")); break;
case "category nameidx": mutate=()=>c.Categories.LoadData(category.Replace("7001,8008", "7001,8999")); break;
case "empty text": mutate=()=>textTables[c].LoadData(texts.Replace("8012,물", "8012,")); break;
case "duplicate text": mutate=()=>textTables[c].LoadData(texts.TrimEnd() + "\n8012,duplicate\n"); break;
case "missing text table": mutate=()=>textTables[c].Release(); break;
case "enum string": mutate=()=>c.Products.LoadData(product.Replace("1001,8012,1,", "1001,8012,Water,")); break;
case "duplicate category type": mutate=()=>c.Categories.LoadData(category.Replace("7002,8009,2", "7002,8009,1")); break;
case "missing category display": mutate=()=>c.Categories.LoadData(category.Replace("7004,8011,4", "")); break;
case "base price": mutate=()=>c.Products.LoadData(product.Replace("1,1,1000,0,", "1,1,0,0,")); break;
case "negative day": mutate=()=>c.Products.LoadData(product.Replace("1,1,1000,0,", "1,1,1000,-1,")); break;
case "zero image": mutate=()=>c.Products.LoadData(product.Replace("1000,0,4276,500", "1000,0,0,500")); break;
case "image FK": mutate=()=>c.Products.LoadData(product.Replace("1000,0,4276,500", "1000,0,4999,500")); break;
case "entry dialog FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8024_8025", "8999")); break;
case "empty entry": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8024_8025", "")); break;
case "duplicate entry": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8024_8025", "8024_8024")); break;
case "accept dialog FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8026_8027", "8999")); break;
case "reject dialog FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8028_8029", "8999")); break;
case "price tolerance":
 string invalidTolerance = disposition.Replace(",1300,0,", ",0,0,");
 Assert.That(invalidTolerance, Is.Not.EqualTo(disposition), "가격 허용치 오류 주입이 실제 CSV를 변경해야 합니다.");
 mutate=()=>c.Dispositions.LoadData(invalidTolerance); break;
case "queue patience": mutate=()=>c.Dispositions.LoadData(disposition.Replace("12,8050,8051", "6,8050,8051")); break;
case "queue warning FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("12,8050,8051", "12,8999,8051")); break;
case "queue leave FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("12,8050,8051", "12,8050,8999")); break;
case "queue header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("queue_patience_seconds", "missing_queue_patience")); break;
case "cost header": mutate=()=>c.Products.LoadData(product.Replace("cost_price", "missing_cost")); break;
case "zero cost": mutate=()=>c.Products.LoadData(product.Replace("1000,0,4276,500", "1000,0,4276,0")); break;
case "type header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("disposition_type", "missing_type")); break;
case "product preference header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("preferred_product_idxs", "missing_products")); break;
case "regular min header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("regular_price_min_rate","missing_min")); break;
case "regular max header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("regular_price_max_rate","missing_max")); break;
case "minimum tolerance header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("minimum_price_tolerance","missing_minimum_tolerance")); break;
case "minimum tolerance overflow": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",1000,8036_8037", ",1001,8036_8037")); break;
case "minimum tolerance above maximum": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",0,8024_8025", ",1200,8024_8025")); break;
case "type 0": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,0,")); break;
case "type 6": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,6,")); break;
case "type 99": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,99,")); break;
case "type Normal": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,Normal,")); break;
case "type empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,,")); break;
case "preferred product 0": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,1,0")); break;
case "preferred product 1001_1001": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,1,1001_1001")); break;
case "preferred product 1999": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,1,1999")); break;
case "regular min 0": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,0,1000")); break;
case "regular min -1": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,-1,1000")); break;
case "regular min 1001": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,1001,1000")); break;
case "regular min empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,,1000")); break;
case "regular max 999": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,1000,999")); break;
case "regular max empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,1000,")); break;
default: throw new ArgumentOutOfRangeException(nameof(name));
}
var resources = loadResources();
LogAssert.Expect(LogType.Error,new Regex(@"^(?:\[Customer CSV\] |(?:CustomerAppearanceData|CustomerDispositionData|ProductCategoryData|ProductData|TextData)\.csv)"));
Assert.Catch(()=>{mutate(); c.ValidateAndCommit(textTables[c], resources, facilities: loadFacilities());});
Assert.That(c.Products.GetDataCount(), Is.Zero); Assert.That(c.Appearances.GetDataCount(), Is.Zero); Assert.That(textTables[c].GetDataCount(), Is.Zero);
LogAssert.NoUnexpectedReceived();
    }
    /// <summary>불량 재로딩은 이전 공개 외형을 유지한다.</summary>
    [Test]
    public void InvalidAppearancePreservesPublishedRows()
    {
string root = "Assets/Datas/Customer/";
string appearance = File.ReadAllText(root + "CustomerAppearanceData.csv");
string disposition = File.ReadAllText(root + "CustomerDispositionData.csv");
string category = File.ReadAllText(root + "ProductCategoryData.csv");
string product = File.ReadAllText(root + "ProductData.csv");
string texts = File.ReadAllText("Assets/Datas/TextData.csv");
var textTables = new Dictionary<CustomerCatalog, TextDataTable>();
Func<CustomerCatalog> load = () => {
 var c = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
 textTables.Add(c, new TextDataTable());
 c.Appearances.LoadData(appearance); c.Dispositions.LoadData(disposition);
 c.Categories.LoadData(category); c.Products.LoadData(product);
 textTables[c].LoadData(texts);
 return c;
};

var c=load();c.ValidateAndCommit(textTables[c], loadResources(), facilities: loadFacilities());
LogAssert.Expect(LogType.Error,new Regex(@"^CustomerAppearanceData\.csv"));
Assert.Catch(()=>c.Appearances.LoadData(appearance.Replace("5001,8001,4201,4318","5001,8001,0,4318")));
Assert.That(c.Appearances.GetDataCount(),Is.EqualTo(60));
    }
    /// <summary>리소스 대역 검증 실패는 이전 공개 리소스를 보존한다.</summary>
    [Test]
    public void InvalidResourcePreservesPublishedRows()
    {
var resources=new ResourceDataTable(); string csv=File.ReadAllText("Assets/Datas/ResourceData.csv"); resources.LoadData(csv); int count=resources.GetDataCount();
Assert.That(resources.GetResourcePath(4201),Is.EqualTo("FemaleNormal_01")); Assert.That(resources.TryGetResource(3001,out _),Is.False);
LogAssert.Expect(LogType.Error,new Regex(@"ResourceData\.csv"));
Assert.Catch(()=>resources.LoadData(csv.Replace("4201,FemaleNormal_01","3001,FemaleNormal_01")));
Assert.That(resources.GetDataCount(),Is.EqualTo(count));
    }
    /// <summary>실제 리소스 FK를 검증하는 기존 테이블을 읽는다.</summary>
    /// <returns>검증된 리소스 테이블.</returns>
    private static ResourceDataTable loadResources()
    {
        var table = new ResourceDataTable();
        LogAssert.Expect(LogType.Log, new Regex(@"^\[ResourceDataTable\] 총 \d+개의 리소스 경로 데이터 로드 완료\."));
        table.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        return table;
    }

    /// <summary>실제 설비 CSV를 공개 전 FK 검증용으로 읽는다.</summary>
    /// <returns>검증 대기 설비 테이블.</returns>
    private static FacilityDataTable loadFacilities()
    {
        var table = new FacilityDataTable();
        table.LoadData(File.ReadAllText("Assets/Datas/FacilityData.csv"));
        return table;
    }
}
