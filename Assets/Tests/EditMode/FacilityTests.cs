using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>설비의 독립 구매·원자성·다음날 판매 후보와 최종 제출 경계를 검사한다.</summary>
public sealed class FacilityTests
{
    private uint day;
    private FinanceService finance;
    private FacilityService service;
    private Dictionary<uint, FacilityData> facilities;

    /// <summary>외부 파일이나 세션을 변경하지 않는 독립 입력을 만든다.</summary>
    [SetUp]
    public void SetUp()
    {
        day = 0;
        finance = new FinanceService(100);
        facilities = new Dictionary<uint, FacilityData>
        {
            [12001] = new FacilityData { Idx = 12001, NameIdx = 8056, PurchasePrice = 30 },
            [12005] = new FacilityData { Idx = 12005, NameIdx = 8060, PurchasePrice = 80 }
        };
        service = new FacilityService(finance, facilities, () => day);
    }

    /// <summary>고단계부터 살 수 있고 같은 날 잠금·다음날 활성·중복 결제 방지를 보장한다.</summary>
    [Test]
    public void IndependentPurchaseAndNextDayActivation()
    {
        Assert.That(service.TryPurchase(12005, out var result));
        Assert.That(result.Status, Is.EqualTo(FacilityPurchaseStatus.Purchased));
        Assert.That(result.PaidAmount, Is.EqualTo(80)); Assert.That(result.ActivationDay, Is.EqualTo(1));
        Assert.That(finance.CurrentBalance, Is.EqualTo(20)); Assert.That(service.IsActive(12005), Is.False);
        Assert.That(service.ActivationDays.ContainsKey(12001), Is.False);
        Assert.That(service.TryPurchase(12005, out result), Is.False);
        Assert.That(result.Status, Is.EqualTo(FacilityPurchaseStatus.AlreadyOwned)); Assert.That(result.PaidAmount, Is.Zero);
        day = 1; Assert.That(service.IsActive(12005)); Assert.That(finance.CurrentBalance, Is.EqualTo(20));
    }

    /// <summary>잔액 부족은 정상 실패이며 보유와 금액을 변경하지 않는다.</summary>
    [Test]
    public void InsufficientFundsPreserveState()
    {
        service.TryPurchase(12001, out _);
        Assert.That(service.TryPurchase(12005, out var result), Is.False);
        Assert.That(result.Status, Is.EqualTo(FacilityPurchaseStatus.InsufficientFunds));
        Assert.That(result.ActivationDay, Is.Null); Assert.That(result.PaidAmount, Is.Zero);
        Assert.That(finance.CurrentBalance, Is.EqualTo(70)); Assert.That(service.ActivationDays.Count, Is.EqualTo(1));
    }

    /// <summary>잘못된 ID 및 활성일 overflow는 지불 전에 실패한다.</summary>
    [Test]
    public void InvalidIdAndDayOverflowDoNotCharge()
    {
        Assert.Throws<ArgumentException>(() => service.TryPurchase(0, out _));
        Assert.Throws<ArgumentException>(() => service.TryPurchase(12999, out _));
        day = uint.MaxValue;
        Assert.Throws<OverflowException>(() => service.TryPurchase(12001, out _));
        Assert.That(finance.CurrentBalance, Is.EqualTo(100)); Assert.That(service.ActivationDays, Is.Empty);
    }

    /// <summary>알림 소비자는 차감·보유를 함께 보고 다른 설비 구매로도 재진입할 수 없다.</summary>
    [Test]
    public void ReentryIsRejectedAndFinallyReleasesGuard()
    {
        Action<FinanceChangeResult> handler = payment =>
        {
            Assert.That(payment.Reason, Is.EqualTo(FinanceChangeReason.FacilityPurchase));
            Assert.That(service.ActivationDays.ContainsKey(12001));
            Assert.That(finance.CurrentBalance, Is.EqualTo(70));
            Assert.Throws<InvalidOperationException>(() => service.TryPurchase(12005, out _));
        };
        finance.BalanceChanged += handler;
        Assert.That(service.TryPurchase(12001, out _));
        finance.BalanceChanged -= handler;
        Assert.That(service.TryPurchase(12001, out var result), Is.False);
        Assert.That(result.Status, Is.EqualTo(FacilityPurchaseStatus.AlreadyOwned));
    }

    /// <summary>알림에서 잔액을 추가 변경한 뒤 던져도 원거래의 차감·보유는 보존한다.</summary>
    [Test]
    public void SubscriberExceptionAfterAnotherBalanceChangeKeepsPurchase()
    {
        var expected = new InvalidOperationException("facility subscriber failure");
        Action<FinanceChangeResult> handler = null;
        handler = payment =>
        {
            finance.BalanceChanged -= handler;
            finance.AddIncome(5, FinanceChangeReason.Sale);
            throw expected;
        };
        finance.BalanceChanged += handler;
        Assert.That(Assert.Throws<InvalidOperationException>(() => service.TryPurchase(12001, out _)), Is.SameAs(expected));
        Assert.That(finance.CurrentBalance, Is.EqualTo(75)); Assert.That(service.ActivationDays[12001], Is.EqualTo(1));
        Assert.That(service.TryPurchase(12001, out var result), Is.False);
        Assert.That(result.Status, Is.EqualTo(FacilityPurchaseStatus.AlreadyOwned));
        Assert.That(finance.CurrentBalance, Is.EqualTo(75));
    }

    /// <summary>외부 가격 변경과 읽기 전용 보유 목록을 통한 수정으로 상태를 바꿀 수 없다.</summary>
    [Test]
    public void CatalogPricesAreCopiedAndOwnershipIsReadonly()
    {
        facilities[12001].PurchasePrice = 1;
        Assert.That(service.TryPurchase(12001, out var result)); Assert.That(result.PaidAmount, Is.EqualTo(30));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<uint, uint>)service.ActivationDays).Add(12005, 0));
        var bad = new FacilityData { Idx = 1, NameIdx = 0, PurchasePrice = 1 };
        Assert.Throws<ArgumentException>(() => bad.Validate()); bad.NameIdx = 1; bad.PurchasePrice = 0;
        Assert.Throws<ArgumentException>(() => bad.Validate());
    }

    /// <summary>잠긴 선호 상품은 주문·제출에서 제외하고 다음 날 생성 방문부터 판매할 수 있다.</summary>
    [Test]
    public void AvailabilityAndSubmissionUseDailyUnlockedSnapshot()
    {
        var products = new Dictionary<uint, ProductData>
        {
            [1001] = new ProductData { Idx = 1001, ProductType = ProductType.Water, BasePrice = 10, CostPrice = 5, IsAvailable = true },
            [1002] = new ProductData { Idx = 1002, ProductType = ProductType.Food, BasePrice = 20, CostPrice = 10, IsAvailable = true, RequiredFacilityIdx = 12005 }
        };
        var config = new CustomerDispositionData { Idx = 6001, DispositionType = CustomerDispositionType.Normal,
            PreferredProductIdxs = new uint[] { 1002 }, PreferredSelectionChance = 1000, MinProductKinds = 2, MaxProductKinds = 2,
            MinQuantity = 1, MaxQuantity = 1, EntryTextIdxs = new uint[] { 1 }, RegularSaleTextIdxs = new uint[] { 1 },
            DiscountSaleTextIdxs = new uint[] { 1 }, ExploitativeSaleTextIdxs = new uint[] { 1 }, RejectTextIdxs = new uint[] { 1 } };
        var generator = new CustomerGenerator(new System.Random(1));
        Func<CustomerVisit> generate = () => generator.Generate(new uint[] { 5001 }, new[] { config }, products, day,
            () => products.ToDictionary(x => x.Key, x => x.Value.BasePrice), isFacilityActive: service.IsActive);
        service.TryPurchase(12005, out _);
        var before = generate(); Assert.That(before.Items.Select(x => x.ProductIdx), Is.EqualTo(new uint[] { 1001 }));
        before.BeginOffer(); Assert.Throws<ArgumentException>(() => before.SubmitOffer(1, new[] { new SaleItem(1002, 1) }));
        Assert.That(before.Result, Is.Null);
        day = 1;
        Assert.Throws<ArgumentException>(() => before.SubmitOffer(1, new[] { new SaleItem(1002, 1) }));
        var after = generate(); Assert.That(after.Items.Select(x => x.ProductIdx), Is.EquivalentTo(new uint[] { 1001, 1002 }));
        Assert.That(CustomerProductAvailability.GetAvailableProducts(products, day, service.IsActive).Select(x => x.Idx),
            Is.EquivalentTo(after.Items.Select(x => x.ProductIdx)));
        after.BeginOffer(); Assert.That(after.SubmitOffer(1, new[] { new SaleItem(1002, 1) }));
        Assert.That(CustomerProductAvailability.GetAvailableProducts(products, day).Count, Is.EqualTo(1), "미연결 설비는 잠금을 유지한다");
    }

    /// <summary>설비 CSV와 상품 FK의 오류가 공개 전에 로그와 예외로 거부되는지 확인한다.</summary>
    /// <param name="kind">형식·대역·이름·상품 FK 오류 구분.</param>
    [TestCase("id"), TestCase("duplicate"), TestCase("price"), TestCase("name"), TestCase("product"), TestCase("zero-product"), TestCase("header")]
    public void CsvAndForeignKeysRejectInvalidRows(string kind)
    {
        string csv = File.ReadAllText("Assets/Datas/FacilityData.csv");
        string product = File.ReadAllText("Assets/Datas/Customer/ProductData.csv");
        if (kind == "id") csv = csv.Replace("12001,", "11001,");
        if (kind == "duplicate") csv += "12001,8056,5000\n";
        if (kind == "price") csv = csv.Replace("8056,5000", "8056,0");
        if (kind == "name") csv = csv.Replace("8056,", "8999,");
        if (kind == "product") product = product.Replace(",12001", ",12999");
        if (kind == "zero-product") product = product.Replace(",12001", ",0");
        if (kind == "header") product = product.Replace("required_facility_idx", "missing_facility");
        var table = new FacilityDataTable();
        var catalog = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
        var texts = new TextDataTable();
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("FacilityData|ProductData|Customer CSV"));
        Assert.Catch(() =>
        {
            table.LoadData(csv);
            catalog.Appearances.LoadData(File.ReadAllText("Assets/Datas/Customer/CustomerAppearanceData.csv"));
            catalog.Dispositions.LoadData(File.ReadAllText("Assets/Datas/Customer/CustomerDispositionData.csv"));
            catalog.Categories.LoadData(File.ReadAllText("Assets/Datas/Customer/ProductCategoryData.csv"));
            catalog.Products.LoadData(product); texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));
            catalog.ValidateAndCommit(texts, facilities: table);
        });
        Assert.That(table.GetDataCount(), Is.Zero); Assert.That(catalog.Products.GetDataCount(), Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }
}
