using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>씬/UI를 만들지 않고 실제 CSV 로딩과 게임 세션의 날짜·라디오·정산 API를 검사한다.</summary>
public sealed class GameSessionApiTests
{
    private GameObject root;
    private GameSessionManager session;
    private DataTableManager tables;

    /// <summary>테스트 소유 manager만 만들고 실제 로더 완료를 제한 시간 내 확인한다.</summary>
    /// <returns>CSV 로드 완료 대기.</returns>
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(ResourceManager.Instance, Is.Null); Assert.That(DataTableManager.Instance, Is.Null); Assert.That(GameSessionManager.Instance, Is.Null);
        root = new GameObject("GameSessionApiFixture"); var resources = root.AddComponent<ResourceManager>();
        yield return wait(resources.InitAsync().AsTask());
        tables = root.AddComponent<DataTableManager>(); yield return wait(tables.EnsureDataLoadedAsync().AsTask());
        session = root.AddComponent<GameSessionManager>(); session.InitializeNewGame(tables);
        Assert.That(session.IsInitialized); Assert.That(session.ElapsedDays, Is.Zero);
    }

    /// <summary>테스트 소유 객체를 제거한다. 사용자 씬·prefs·저장 파일은 접근하지 않는다.</summary>
    /// <returns>파괴 callback 완료 대기.</returns>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        UnityEngine.Object.Destroy(root); yield return null;
        Assert.That(ResourceManager.Instance, Is.Null); Assert.That(DataTableManager.Instance, Is.Null); Assert.That(GameSessionManager.Instance, Is.Null);
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>라디오는 입장/일시정지에 방송하지 않고 영업 후 60초 안에 한 번만 방송한다.</summary>
    [Test]
    public void RadioTimingPauseAndCloseCancellation()
    {
        var before = session.EnsureDailyPrices(); Assert.That(before.IsRadioBroadcast, Is.False); Assert.That(before.RadioEventIdx.HasValue);
        Assert.That(session.AdvanceTradingTime(60, false), Is.False); session.BeginTradingDay();
        Assert.That(session.AdvanceTradingTime(60, true), Is.False); Assert.That(session.DailyPrices, Is.SameAs(before));
        Assert.That(session.AdvanceTradingTime(60, false)); Assert.That(session.AdvanceTradingTime(60, false), Is.False);
        session.EndTradingDay(); session.CompleteDay(0); var day1 = session.EnsureDailyPrices();
        session.BeginTradingDay(); session.EndTradingDay(); Assert.That(session.AdvanceTradingTime(60, false), Is.False);
        Assert.That(session.DailyPrices, Is.SameAs(day1)); Assert.That(day1.IsRadioBroadcast, Is.False);
    }

    /// <summary>방문 희망 snapshot과 방송 이후 제출 현재가를 구분한다.</summary>
    [Test]
    public void RadioUpdatesSubmissionButNotInitialItems()
    {
        var before = session.EnsureDailyPrices(); var visit = generate(); var initial = visit.Items.Select(x => x.UnitPrice).ToArray();
        session.BeginTradingDay(); session.AdvanceTradingTime(60, false);
        Assert.That(visit.Items.Select(x => x.UnitPrice), Is.EqualTo(initial));
        var expected = new PriceEventScheduler(new System.Random(1)).ApplyRadio(before, tables.GetDB<PriceEventDataTable>(DataTableType.PriceEvent).Rows, tables.Customers.Products.Rows);
        Assert.That(session.DailyPrices.Prices, Is.EquivalentTo(expected.Prices));
        var final = visit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
        long total = final.Sum(x => (long)x.Quantity * session.DailyPrices.Prices[x.ProductId]);
        visit.BeginOffer(); visit.SubmitOffer(total, final); Assert.That(visit.Result.Value.ReferenceTotal, Is.EqualTo(total));
        Assert.That(visit.Result.Value.SoldItems.All(x => x.UnitPrice == session.DailyPrices.Prices[x.ProductId]));
        Assert.That(generate().Items.All(x => x.UnitPrice == session.DailyPrices.Prices[x.ProductIdx]));
    }

    /// <summary>매출 반영·종료 후 접수 거부·날짜 상태 전이를 화면 없이 검사한다.</summary>
    [Test]
    public void AccountingAndDayTransitions()
    {
        var catalog = tables.Customers;
        Assert.That(catalog.Products, Is.SameAs(tables.GetDB<ProductDataTable>(DataTableType.Product)));
        Assert.That(catalog.Dispositions, Is.SameAs(tables.GetDB<CustomerDispositionDataTable>(DataTableType.CustomerDisposition)));
        session.BeginTradingDay(); Assert.Throws<InvalidOperationException>(() => session.BeginTradingDay());
        var visit = generate(); visit.BeginOffer(); long total = visit.Items.Sum(x => (long)x.Quantity * session.DailyPrices.Prices[x.ProductIdx]);
        visit.SubmitOffer(total, visit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        long balance = session.Economy.QueryService.CurrentBalance;
        Assert.That(session.Economy.DailyAggregationService.TryApplyTransaction(visit.Result.Value));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance + total));
        Assert.That(session.EndTradingDay(), Is.EqualTo(total));
        Assert.That(session.Economy.DailyAggregationService.TryApplyTransaction(visit.Result.Value), Is.False);
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance + total));
        session.CompleteDay(0); Assert.That(session.ElapsedDays, Is.EqualTo(1)); Assert.Throws<InvalidOperationException>(() => session.CompleteDay(0));
    }

    /// <summary>잘못된 시각과 조기 날짜 완료가 세션 상태를 변경하지 않는지 검사한다.</summary>
    [Test]
    public void InvalidTimeAndPrematureCompletionPreserveState()
    {
        var prices = session.EnsureDailyPrices();
        Assert.That(session.EnsureDailyPrices(), Is.SameAs(prices));
        Assert.Throws<InvalidOperationException>(() => session.InitializeNewGame(tables));
        Assert.Throws<InvalidOperationException>(() => session.CompleteDay(0));
        session.BeginTradingDay();
        foreach (float seconds in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceTradingTime(seconds, false));
        Assert.That(session.AdvanceTradingTime(0, false), Is.False);
        Assert.Throws<InvalidOperationException>(() => session.CompleteDay(0));
        Assert.That(session.ElapsedDays, Is.Zero);
        Assert.That(session.DailyPrices, Is.SameAs(prices));
        Assert.That(session.Economy.QueryService.IsDayOpen);
    }

    /// <summary>실제 진행 경로가 확정 판매 목록·원가·판정을 보존하고 한 번만 입금한다.</summary>
    [Test]
    public void ProgressPreservesTransactionAndRejectsDuplicateSubmission()
    {
        var progress = new GameProgress(session, tables.Customers, new System.Random(1));
        progress.Start(); progress.OpenBusiness(); progress.BeginCustomerSorting();
        var day = progress.CurrentDayProgress;
        var visit = day.CurrentVisit;
        uint product = tables.Customers.Products.Rows.Keys.First();
        var items = new[] { new SaleItem(product, 1), new SaleItem(product, 2) };
        long total = (long)session.EnsureDailyPrices().Prices[product] * 3;
        long balance = session.Economy.QueryService.CurrentBalance;
        Assert.Throws<ArgumentOutOfRangeException>(() => progress.SubmitOffer(0, items));
        Assert.That(day.CanSubmitOffer);
        int completed = 0;
        day.TransactionCompleted += actual =>
        {
            completed++;
            Assert.That(actual, Is.SameAs(visit));
            Assert.That(actual.Result.Value.SoldItems.Count, Is.EqualTo(1));
            Assert.That(actual.Result.Value.SoldItems[0].Quantity, Is.EqualTo(3));
        };
        Assert.That(progress.SubmitOffer(1, items));
        var result = visit.Result.Value;
        Assert.That(result.OfferedTotal, Is.EqualTo(1));
        Assert.That(result.ReferenceTotal, Is.EqualTo(total));
        Assert.That(result.Outcome, Is.EqualTo(CustomerTradeOutcome.DiscountSale));
        Assert.That(result.CostTotal, Is.EqualTo((long)tables.Customers.Products.Rows[product].CostPrice * 3));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance + result.SaleIncome));
        Assert.Throws<InvalidOperationException>(() => progress.SubmitOffer(1, items));
        Assert.That(completed, Is.EqualTo(1));
        progress.Tick(day.BusinessDurationSeconds);
        progress.CompleteTransactionResult();
        Assert.That(day.AggregationResult.Value.SaleIncome, Is.EqualTo(result.SaleIncome));
        Assert.Throws<InvalidOperationException>(() => session.EndTradingDay());
        Assert.That(visit.Result.Value.SoldItems, Is.SameAs(result.SoldItems));
    }

    /// <summary>준비된 지침 방문을 진행 경계에 넣어 위반 snapshot이 정산 이후에도 보존되는지 검사한다.</summary>
    [Test]
    public void ProgressPreservesRestrictionSnapshot()
    {
        var progress = new GameProgress(session, tables.Customers, new System.Random(1));
        progress.Start(); progress.OpenBusiness(); progress.BeginCustomerSorting();
        System.Collections.Generic.IReadOnlyList<SaleRestriction> rules = null;
        var catalog = tables.Customers;
        var visit = new CustomerGenerator(new System.Random(1)).Generate(
            catalog.Appearances.Rows.Keys.ToArray(), catalog.Dispositions.Rows.Values.ToArray(),
            catalog.Products.Rows, session.ElapsedDays, () => session.EnsureDailyPrices().Prices, () => rules);
        var product = catalog.Products.Rows.Values.First();
        rules = new[] { new SaleRestriction(visit.Attributes, product.ProductType) };
        visit.BeginOffer();
        // 실제 지침 공급은 미연결이므로 테스트에서만 같은 공개 생성기로 만든 방문을 주입한다.
        typeof(DayProgress).GetField("currentVisit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(progress.CurrentDayProgress, visit);
        Assert.That(progress.SubmitOffer(1, new[] { new SaleItem(product.Idx, 2) }));
        var result = visit.Result.Value;
        Assert.That(result.WereRestrictionsEvaluated);
        Assert.That(result.RestrictionViolations.Count, Is.EqualTo(1));
        Assert.That(result.RestrictionViolations[0].Quantity, Is.EqualTo(2));
        progress.Tick(progress.CurrentDayProgress.BusinessDurationSeconds);
        progress.CompleteTransactionResult();
        Assert.That(visit.Result.Value.RestrictionViolations, Is.SameAs(result.RestrictionViolations));
        Assert.That(progress.CurrentDayProgress.AggregationResult.Value.SaleIncome, Is.EqualTo(result.SaleIncome));
    }

    /// <summary>접수 false/예외 이후 판정을 보존하고 진행·재접수를 차단한다.</summary>
    /// <param name="overflow">true면 잔고 overflow, false면 종료된 집계로 실패를 주입한다.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void ProgressSettlementFailureStopsWithoutRetry(bool overflow)
    {
        var progress = new GameProgress(session, tables.Customers, new System.Random(1));
        progress.Start(); progress.OpenBusiness(); progress.BeginCustomerSorting();
        var day = progress.CurrentDayProgress;
        var items = day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
        if (overflow)
            session.Economy.FinanceService.AddIncome(long.MaxValue - session.Economy.QueryService.CurrentBalance, FinanceChangeReason.Sale);
        else
            session.EndTradingDay();
        long before = session.Economy.QueryService.CurrentBalance;
        int completed = 0;
        day.TransactionCompleted += _ => completed++;
        if (overflow) Assert.Throws<OverflowException>(() => progress.SubmitOffer(1, items));
        else Assert.That(Assert.Throws<InvalidOperationException>(() => progress.SubmitOffer(1, items)).Message,
            Does.Contain("종료된 일일 집계"));
        Assert.That(day.CurrentVisit.State, Is.EqualTo(CustomerState.Accepted));
        var result = day.CurrentVisit.Result.Value;
        Assert.That(result.SoldItems.Count, Is.GreaterThan(0));
        Assert.That(day.State, Is.EqualTo(DayProgressState.Sorting));
        Assert.That(day.SuccessfulSales, Is.Zero);
        Assert.That(completed, Is.Zero);
        Assert.Throws<InvalidOperationException>(() => progress.SubmitOffer(1, items));
        Assert.Throws<InvalidOperationException>(() => progress.Tick(100));
        Assert.Throws<InvalidOperationException>(() => progress.CompleteTransactionResult());
        Assert.Throws<InvalidOperationException>(() => progress.CompleteSettlement());
        Assert.Throws<InvalidOperationException>(() => progress.Pause());
        Assert.Throws<InvalidOperationException>(() => progress.Resume());
        Assert.That(day.CurrentVisit.Result.Value.SoldItems, Is.SameAs(result.SoldItems));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(before));
        Assert.That(session.ElapsedDays, Is.Zero);
    }

    /// <summary>일반일과 상납 성공/실패에서 표시일·세션일·가격일 및 중복 완료를 검사한다.</summary>
    /// <param name="canPay">상납 시 잔고가 충분한지 여부.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void ProgressDatesAndMaintenanceRemainSynchronized(bool canPay)
    {
        if (!canPay)
            Assert.That(session.Economy.FinanceService.TrySpend(session.Economy.QueryService.CurrentBalance, FinanceChangeReason.Sale, out _));
        var progress = new GameProgress(session, tables.Customers, new System.Random(1));
        progress.Start();
        Assert.Throws<InvalidOperationException>(() => progress.Start());
        int cycle = session.Economy.Settings.MaintenanceCycleDays;
        for (int displayDay = 1; displayDay <= cycle; displayDay++)
        {
            Assert.That(progress.CurrentDay, Is.EqualTo(displayDay));
            Assert.That(session.ElapsedDays, Is.EqualTo(displayDay - 1));
            Assert.That(session.EnsureDailyPrices().ElapsedDays, Is.EqualTo(displayDay - 1));
            closeProgressDay(progress);
            var closed = progress.CurrentDayProgress;
            progress.CompleteSettlement();
            Assert.Throws<InvalidOperationException>(() => closed.CompleteSettlement());
            Assert.Throws<InvalidOperationException>(() => session.CompleteDay((uint)(displayDay - 1)));
        }
        Assert.That(progress.State, Is.EqualTo(GameProgressState.Maintenance));
        Assert.That(session.ElapsedDays, Is.EqualTo(cycle - 1));
        Assert.That(progress.TryPayMaintenance(), Is.EqualTo(canPay));
        Assert.That(progress.State, Is.EqualTo(canPay ? GameProgressState.DayInProgress : GameProgressState.Failed));
        if (!canPay) Assert.Throws<InvalidOperationException>(() => progress.TryPurchaseFacility(12001, out _));
        Assert.That(session.ElapsedDays, Is.EqualTo(canPay ? cycle : cycle - 1));
        Assert.That(progress.CurrentDay, Is.EqualTo(canPay ? cycle + 1 : cycle));
        Assert.That(session.Economy.MaintenanceService.LastPaidRound, Is.EqualTo(canPay ? 1 : 0));
        Assert.That(session.EnsureDailyPrices().ElapsedDays, Is.EqualTo(session.ElapsedDays));
        Assert.Throws<InvalidOperationException>(() => progress.TryPayMaintenance());
    }

    /// <summary>진행 Tick의 pause와 방송, 방문 snapshot 및 제출 단가의 동일 원본을 검사한다.</summary>
    [Test]
    public void ProgressRadioUsesOnlyUnpausedTradingTime()
    {
        var progress = new GameProgress(session, tables.Customers, new System.Random(1), 90);
        progress.Start(); progress.OpenBusiness(); progress.BeginCustomerSorting();
        var day = progress.CurrentDayProgress;
        var original = day.CurrentVisit.Items.Select(x => x.UnitPrice).ToArray();
        var before = session.DailyPrices;
        progress.Pause(); progress.Tick(1000);
        Assert.That(day.RemainingSeconds, Is.EqualTo(90));
        Assert.That(session.DailyPrices, Is.SameAs(before));
        progress.Resume(); progress.Tick(60);
        Assert.That(day.RemainingSeconds, Is.EqualTo(30));
        Assert.That(session.DailyPrices.IsRadioBroadcast);
        var broadcast = session.DailyPrices;
        progress.Tick(1);
        Assert.That(session.DailyPrices, Is.SameAs(broadcast));
        Assert.That(day.CurrentVisit.Items.Select(x => x.UnitPrice), Is.EqualTo(original));
        var final = day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
        Assert.That(progress.SubmitOffer(1, final));
        Assert.That(day.CurrentVisit.Result.Value.ReferenceTotal,
            Is.EqualTo(final.Sum(x => (long)x.Quantity * broadcast.Prices[x.ProductId])));
    }

    /// <summary>긴 프레임이 영업시간을 초과해 방송하지 않으며 Closing 마지막 거래 정책은 유지한다.</summary>
    [Test]
    public void ProgressLongFrameDoesNotBroadcastAfterBusinessDeadline()
    {
        var progress = new GameProgress(session, tables.Customers, new System.Random(1), 10);
        progress.Start(); progress.OpenBusiness();
        // 난수 대기값만 고정해 실제 Tick의 10초 제한을 결정적으로 검사한다.
        typeof(GameSessionManager).GetField("radioRemainingSeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(session, 50f);
        progress.Tick(1000);
        var day = progress.CurrentDayProgress;
        Assert.That(day.State, Is.EqualTo(DayProgressState.Closing));
        Assert.That(day.CanSubmitOffer);
        Assert.That(session.DailyPrices.IsRadioBroadcast, Is.False);
        progress.Tick(1000);
        Assert.That(session.DailyPrices.IsRadioBroadcast, Is.False);
        Assert.That(progress.SubmitOffer(1, day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray()));
        progress.CompleteTransactionResult();
        Assert.That(day.State, Is.EqualTo(DayProgressState.Settlement));
        Assert.That(session.Economy.QueryService.IsDayOpen, Is.False);
        Assert.That(session.AdvanceTradingTime(1000, false), Is.False);
    }

    /// <summary>가격표는 현재가·해금일을 사용하며 누락 가격과 다른 날짜 snapshot을 거부한다.</summary>
    [Test]
    public void ProgressPriceListUsesDailyPricesAndUnlockDay()
    {
        var factory = new ProgressViewDataFactory(tables.Customers,
            tables.GetDB<TextDataTable>(DataTableType.Text), new System.Collections.Generic.Dictionary<uint, Sprite>());
        var product = tables.Customers.Products.Rows.Values.First();
        product.AvailableDay = 1; // 메모리 fixture만 수정하며 다음 SetUp에서 실제 CSV를 다시 로드한다.
        string name = tables.GetDB<TextDataTable>(DataTableType.Text).Rows[product.NameIdx].Text;
        var progress = new GameProgress(session, tables.Customers, new System.Random(1));
        progress.Start();
        string firstList = factory.CreatePriceListText(progress.CurrentDay, session.DailyPrices);
        Assert.That(firstList, Does.Not.Contain(name + "  ·"));
        var discounted = CustomerProductAvailability.GetAvailableProducts(tables.Customers.Products.Rows, 0)
            .First(x => session.DailyPrices.Prices[x.Idx] != x.BasePrice);
        string discountedName = tables.GetDB<TextDataTable>(DataTableType.Text).Rows[discounted.NameIdx].Text;
        Assert.That(firstList, Does.Contain($"{discountedName}  ·  {session.DailyPrices.Prices[discounted.Idx]:N0} G"));
        Assert.That(firstList, Does.Not.Contain($"{discountedName}  ·  {discounted.BasePrice:N0} G"));
        Assert.Throws<InvalidOperationException>(() => factory.CreatePriceListText(2, session.DailyPrices));
        Assert.Throws<InvalidOperationException>(() => new DayProgress(2, session, tables.Customers, new System.Random(1)));
        closeProgressDay(progress); progress.CompleteSettlement();
        string actual = factory.CreatePriceListText(progress.CurrentDay, session.DailyPrices);
        Assert.That(actual, Does.Contain($"{name}  ·  {session.DailyPrices.Prices[product.Idx]:N0} G"));
        var missing = new PriceEventScheduler(new System.Random(1)).CreateDay(session.ElapsedDays,
            new System.Collections.Generic.Dictionary<uint, PriceEventData>(),
            new System.Collections.Generic.Dictionary<uint, PriceEventScheduleData>(),
            tables.Customers.Products.Rows.Where(x => x.Key != product.Idx).ToDictionary(x => x.Key, x => x.Value));
        Assert.Throws<InvalidOperationException>(() => factory.CreatePriceListText(progress.CurrentDay, missing));
    }

    /// <summary>실제 로더·진행 구매 API·가격표·생성·최종 제출의 다음날 해금 경계를 함께 검증한다.</summary>
    [Test]
    public void FacilityPurchaseUnlocksOnlyNextDayAcrossProgressInstances()
    {
        var progress = new GameProgress(session, tables.Customers, new System.Random(1));
        Assert.Throws<InvalidOperationException>(() => progress.TryPurchaseFacility(12005, out _));
        progress.Start();
        long balance = session.Economy.QueryService.CurrentBalance;
        long price = tables.GetDB<FacilityDataTable>(DataTableType.Facility).Rows[12005].PurchasePrice;
        Assert.That(progress.TryPurchaseFacility(12005, out var purchase));
        Assert.That(purchase.ActivationDay, Is.EqualTo(1));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance - price));
        Assert.That(session.IsFacilityActive(12005), Is.False);
        Assert.That(progress.TryPurchaseFacility(12005, out purchase), Is.False);
        Assert.That(purchase.Status, Is.EqualTo(FacilityPurchaseStatus.AlreadyOwned));
        var factory = new ProgressViewDataFactory(tables.Customers, tables.GetDB<TextDataTable>(DataTableType.Text),
            new System.Collections.Generic.Dictionary<uint, Sprite>(), session.IsFacilityActive);
        string name = tables.GetDB<TextDataTable>(DataTableType.Text).Rows[tables.Customers.Products.Rows[1020].NameIdx].Text;
        Assert.That(factory.CreatePriceListText(1, session.EnsureDailyPrices()), Does.Not.Contain(name + "  ·"));
        progress.OpenBusiness(); progress.BeginCustomerSorting();
        var before = progress.CurrentDayProgress.CurrentVisit;
        Assert.Throws<ArgumentException>(() => progress.SubmitOffer(1, new[] { new SaleItem(1020, 1) }));
        Assert.That(before.Result, Is.Null);
        progress.Tick(progress.CurrentDayProgress.BusinessDurationSeconds);
        progress.SubmitOffer(long.MaxValue, before.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        progress.CompleteTransactionResult(); progress.CompleteSettlement();
        Assert.That(session.ElapsedDays, Is.EqualTo(1)); Assert.That(session.IsFacilityActive(12005));
        Assert.That(factory.CreatePriceListText(2, session.EnsureDailyPrices()), Does.Contain(name + "  ·"));
        var expected = new uint[] { 1001, 1004, 1007, 1010, 1011, 1020, 1021, 1022 };
        Assert.That(CustomerProductAvailability.GetAvailableProducts(tables.Customers.Products.Rows, 1, session.IsFacilityActive).Select(x => x.Idx), Is.EquivalentTo(expected));
        for (int i = 0; i < 20; i++) Assert.That(generate().Items.All(x => expected.Contains(x.ProductIdx)));
        progress.OpenBusiness(); progress.BeginCustomerSorting();
        Assert.That(progress.SubmitOffer(1, new[] { new SaleItem(1020, 1) }));
        // 표현/진행 객체 수명이 바뀌어도 보유는 세션에 남는다. 실제 씬은 수정하지 않는다.
        var nextProgress = new GameProgress(session, tables.Customers, new System.Random(2));
        Assert.That(session.FacilityActivationDays.Count, Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => session.InitializeNewGame(tables));
    }

    /// <summary>세션 파괴 후 새 세션은 보유·활성일을 이어받지 않는다.</summary>
    /// <returns>테스트 소유 세션 파괴 완료 대기.</returns>
    [UnityTest]
    public IEnumerator NewSessionClearsFacilityOwnership()
    {
        var progress = new GameProgress(session, tables.Customers, new System.Random(1)); progress.Start();
        Assert.That(progress.TryPurchaseFacility(12001, out _));
        UnityEngine.Object.Destroy(session); yield return null;
        Assert.That(GameSessionManager.Instance, Is.Null);
        session = root.AddComponent<GameSessionManager>(); session.InitializeNewGame(tables);
        Assert.That(session.FacilityActivationDays, Is.Empty); Assert.That(session.ElapsedDays, Is.Zero);
        Assert.That(session.IsFacilityActive(12001), Is.False);
    }

    /// <summary>현재 진행의 마지막 손님을 거절하고 일일 집계까지 실제 API로 종료한다.</summary>
    /// <param name="progress">시작된 영업 전 진행.</param>
    private static void closeProgressDay(GameProgress progress)
    {
        progress.OpenBusiness();
        var day = progress.CurrentDayProgress;
        progress.Tick(day.BusinessDurationSeconds);
        Assert.That(progress.SubmitOffer(long.MaxValue, day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray()), Is.False);
        progress.CompleteTransactionResult();
        Assert.That(day.State, Is.EqualTo(DayProgressState.Settlement));
        Assert.That(day.AggregationResult.Value.SaleIncome, Is.Zero);
    }

    /// <summary>공개 catalog로 실제 가격 공급을 연결한 방문을 만든다.</summary>
    /// <returns>현재일 방문.</returns>
    private CustomerVisit generate() => new CustomerGenerator(new System.Random(1)).Generate(tables.Customers.Appearances.Rows.Keys.ToArray(), tables.Customers.Dispositions.Rows.Values.ToArray(), tables.Customers.Products.Rows, session.ElapsedDays, () => session.EnsureDailyPrices().Prices, isFacilityActive: session.IsFacilityActive);

    /// <summary>비동기 로더가 종료되지 않거나 실패하면 실패로 보고한다.</summary>
    /// <param name="task">로더 작업.</param><returns>완료 대기.</returns>
    private static IEnumerator wait(Task task)
    {
        float end = Time.realtimeSinceStartup + 20;
        while (!task.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(task.IsCompleted, "Actual CSV loader timed out."); task.GetAwaiter().GetResult();
    }
}
