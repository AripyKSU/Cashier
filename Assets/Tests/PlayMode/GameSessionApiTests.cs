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
        // 기존 회귀도 실제 첫날 대사/퇴장 API를 완료하고 시작한다. 감독관 자체 검사는 아래 별도 사례에서 수행한다.
        if (!TestContext.CurrentContext.Test.Name.StartsWith("Inspector", StringComparison.Ordinal))
        {
            var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
            progress.Start();
            completeInspectors(progress);
        }
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

    /// <summary>현재 상품·손님이 참조하는 고유 Sprite 53개를 실제 ResourceManager로 로드한다.</summary>
    /// <returns>Addressables 로드 완료 대기.</returns>
    [UnityTest]
    public IEnumerator ActualProductAndCustomerSpritesLoad()
    {
        var resources = tables.GetDB<ResourceDataTable>(DataTableType.Resource);
        var ids = tables.Customers.Appearances.Rows.Values.Select(x => x.ImageResourceIdx)
            .Concat(tables.Customers.Products.Rows.Values.Where(x => x.ImageResourceIdx.HasValue).Select(x => x.ImageResourceIdx.Value))
            .Concat(tables.Customers.Products.Rows.Values.Where(x => x.TopViewImageResourceIdx.HasValue).Select(x => x.TopViewImageResourceIdx.Value)).Distinct().ToArray();
        Assert.That(ids.Length, Is.EqualTo(53));
        foreach (var id in ids)
        {
            var task = ResourceManager.Instance.LoadAssetAsync<Sprite>(resources.GetResourcePath(id)).AsTask();
            yield return wait(task);
            Assert.That(task.Result, Is.Not.Null, $"Sprite FK {id}");
            Assert.That(task.Result.rect.width, Is.GreaterThan(0));
        }
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
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); completeInspectors(progress);
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
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance + total - 200));
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
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
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

    /// <summary>실제 하루 진행 경로에서 연속 손님 성별이 교대하는지 확인합니다. 영업일별 selector 재생성은 EditMode에서 별도로 검사합니다.</summary>
    [Test]
    public void CustomerGenderAlternatesWithinBusinessDay()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(23));
        progress.Start();
        progress.OpenBusiness();
        progress.BeginCustomerSorting();

        CustomerAttributes gender = progress.CurrentDayProgress.CurrentVisit.Attributes &
            (CustomerAttributes.Male | CustomerAttributes.Female);
        for (int i = 0; i < 3; i++)
        {
            CustomerVisit visit = progress.CurrentDayProgress.CurrentVisit;
            var saleItems = visit.Items.Select(item => new SaleItem(item.ProductIdx, item.Quantity)).ToArray();
            // 성별 교대 검사는 기존 수락가 helper로 거래한다. 1원은 가격 민감형이 거절한다.
            long offered = acceptedOffer(visit, saleItems);
            Assert.That(progress.SubmitOffer(offered, saleItems), Is.True);
            progress.CompleteTransactionResult();
            progress.BeginCustomerSorting();
            CustomerAttributes nextGender = progress.CurrentDayProgress.CurrentVisit.Attributes &
                (CustomerAttributes.Male | CustomerAttributes.Female);
            Assert.That(nextGender, Is.Not.EqualTo(gender));
            gender = nextGender;
        }
    }

    /// <summary>소수 도덕성을 거래마다 한 번 누적하고 재정 알림 시 세 상태가 함께 확정됐는지 확인한다.</summary>
    [Test]
    public void ProgressAccumulatesDecimalMoralityAndPreservesItAcrossProgressObjects()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); progress.OpenBusiness();
        for (int index = 0; index < 4; index++)
        {
            CustomerVisit visit = generateAdultNormalWithMorality(index);
            visit.BeginOffer();
            typeof(DayProgress).GetField("currentVisit", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(progress.CurrentDayProgress, visit);
            progress.BeginCustomerSorting();
            var items = visit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
            long reference = items.Sum(x => (long)x.Quantity * session.DailyPrices.Prices[x.ProductId]);
            long offered = checked((reference * 1001 + 999) / 1000);
            bool observed = false;
            Action<FinanceChangeResult> observer = _ =>
            {
                observed = true;
                Assert.That(session.CurrentMorality, Is.EqualTo(-0.25m * (index + 1)));
                Assert.That(session.DailyMoralityDelta, Is.EqualTo(session.CurrentMorality));
                Assert.That(session.Economy.DailyAggregationService.DailyTransactionCount, Is.EqualTo(index + 1));
            };
            session.Economy.FinanceService.BalanceChanged += observer;
            Assert.That(progress.SubmitOffer(offered, items));
            session.Economy.FinanceService.BalanceChanged -= observer;
            Assert.That(observed);
            Assert.That(visit.Result.Value.MoralityDataIdx, Is.EqualTo(14004));
            Assert.That(visit.Result.Value.MoralityDelta, Is.EqualTo(-0.25m));
            progress.CompleteTransactionResult();
        }
        Assert.That(session.CurrentMorality, Is.EqualTo(-1m));
        _ = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(2));
        Assert.That(session.CurrentMorality, Is.EqualTo(-1m));
    }

    /// <summary>실제 양음수·0·거절 거래를 정산하고 다음날에도 총누적과 이전 snapshot을 보존한다.</summary>
    [Test]
    public void DailyMoralitySettlementResetsOnlyDailyTotal()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); progress.OpenBusiness();
        decimal[] deltas = { 0.5m, 0m, -0.25m, -2m };
        decimal expected = 0m;
        for (int i = 0; i < deltas.Length; i++)
        {
            var visit = generateAdultNormalWithMorality(i + 50);
            visit.BeginOffer();
            typeof(DayProgress).GetField("currentVisit", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(progress.CurrentDayProgress, visit);
            progress.BeginCustomerSorting();
            var items = visit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
            long reference = items.Sum(x => (long)x.Quantity * session.DailyPrices.Prices[x.ProductId]);
            long offered = i == 0 ? reference - 1 : i == 1 ? reference : i == 2 ? reference + 1 : long.MaxValue;
            Assert.That(progress.SubmitOffer(offered, items), Is.EqualTo(i != 3));
            Assert.That(visit.Result.Value.MoralityDelta, Is.EqualTo(deltas[i]));
            expected += deltas[i];
            Assert.That(session.DailyMoralityDelta, Is.EqualTo(expected));
            Assert.That(session.CurrentMorality, Is.EqualTo(expected));
            progress.CompleteTransactionResult();
        }
        // 기존 재정 전용 결과의 미평가 null은 일일값을 바꾸지 않는다.
        Assert.That(session.Economy.DailyAggregationService.TryApplyTransaction(new TransactionResult(0, 0)));
        session.EndTradingDay(out var result);
        Assert.That(result.MoralityDelta, Is.EqualTo(expected));
        Assert.That(result.Expenses, Is.GreaterThan(0));
        Assert.That(result.NetProfit, Is.EqualTo(result.SaleIncome - result.Expenses));
        Assert.That(session.DailyMoralityDelta, Is.Zero);
        Assert.That(session.CurrentMorality, Is.EqualTo(expected));
        session.CompleteDay(0);
        var nextProgress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        nextProgress.Start(); completeInspectors(nextProgress);
        session.BeginTradingDay();
        Assert.That(session.DailyMoralityDelta, Is.Zero);
        session.EndTradingDay(out var emptyDay);
        Assert.That(emptyDay.MoralityDelta, Is.Zero);
        Assert.That(result.MoralityDelta, Is.EqualTo(expected));
        Assert.That(session.CurrentMorality, Is.EqualTo(expected));
    }

    /// <summary>일일 decimal 한계에서 실패하면 총누적·잔고·거래 기록을 변경하지 않는다.</summary>
    [Test]
    public void DailyMoralityOverflowPreventsSessionMutation()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); progress.OpenBusiness();
        var visit = generateAdultNormalWithMorality(80);
        visit.BeginOffer();
        typeof(DayProgress).GetField("currentVisit", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(progress.CurrentDayProgress, visit);
        progress.BeginCustomerSorting();
        var aggregation = session.Economy.DailyAggregationService;
        typeof(DailyAggregationService).GetField("dailyMoralityDelta", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(aggregation, decimal.MaxValue);
        long balance = session.Economy.QueryService.CurrentBalance;
        var items = visit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
        Assert.Throws<OverflowException>(() => progress.SubmitOffer(1, items));
        Assert.That(session.CurrentMorality, Is.Zero);
        Assert.That(session.DailyMoralityDelta, Is.EqualTo(decimal.MaxValue));
        Assert.That(aggregation.DailyTransactionCount, Is.Zero);
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance));
    }

    /// <summary>재정 알림 실패에도 잔고·거래 기록·도덕성을 함께 보존하고 같은 방문 재제출을 막는다.</summary>
    [Test]
    public void FinanceNotificationFailurePreservesMoralityAndTransactionSnapshot()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); progress.OpenBusiness();
        CustomerVisit visit = generateAdultNormalWithMorality(10);
        visit.BeginOffer();
        typeof(DayProgress).GetField("currentVisit", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(progress.CurrentDayProgress, visit);
        progress.BeginCustomerSorting();
        var items = visit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
        long reference = items.Sum(x => (long)x.Quantity * session.DailyPrices.Prices[x.ProductId]);
        long offered = checked((reference * 1001 + 999) / 1000);
        long balance = session.Economy.QueryService.CurrentBalance;
        Action<FinanceChangeResult> failure = _ => throw new InvalidOperationException("morality notification failure");
        session.Economy.FinanceService.BalanceChanged += failure;
        Assert.Throws<InvalidOperationException>(() => progress.SubmitOffer(offered, items));
        session.Economy.FinanceService.BalanceChanged -= failure;
        Assert.That(session.CurrentMorality, Is.EqualTo(-0.25m));
        Assert.That(session.DailyMoralityDelta, Is.EqualTo(-0.25m));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance + offered));
        Assert.That(session.Economy.DailyAggregationService.DailyTransactionCount, Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => progress.SubmitOffer(offered, items));
    }

    /// <summary>준비된 지침 방문을 진행 경계에 넣어 위반 snapshot이 정산 이후에도 보존되는지 검사한다.</summary>
    [Test]
    public void ProgressPreservesRestrictionSnapshot()
    {
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
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
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
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

    /// <summary>매일 유지비가 정산 전에 차감되고 별도 상납 상태 없이 다음 날로 진행되는지 검사한다.</summary>
    [Test]
    public void ProgressDatesAndDailyMaintenanceRemainSynchronized()
    {
        long[] maintenanceAmounts = tables.GetDB<MaintenanceBalanceDataTable>(
            DataTableType.MaintenanceBalance).GetMaintenanceAmounts();
        Assert.That(maintenanceAmounts.Length, Is.EqualTo(30));
        for (int day = 1; day <= maintenanceAmounts.Length; day++)
            Assert.That(maintenanceAmounts[day - 1], Is.EqualTo(200 + ((day - 1) * 100)));

        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start();
        Assert.Throws<InvalidOperationException>(() => progress.Start());
        long previousBalance = session.Economy.QueryService.CurrentBalance;
        closeProgressDay(progress);
        var closed = progress.CurrentDayProgress;
        Assert.That(closed.AggregationResult.Value.Expenses, Is.EqualTo(200));
        Assert.That(closed.AggregationResult.Value.NetProfit, Is.EqualTo(-200));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(previousBalance - 200));
        progress.CompleteSettlement();
        Assert.Throws<InvalidOperationException>(() => closed.CompleteSettlement());
        Assert.Throws<InvalidOperationException>(() => session.CompleteDay(0));
        Assert.That(progress.State, Is.EqualTo(GameProgressState.DayInProgress));
        Assert.That(session.ElapsedDays, Is.EqualTo(1));
        Assert.That(progress.CurrentDay, Is.EqualTo(2));
        Assert.That(session.Economy.MaintenanceService.LastPaidDay, Is.EqualTo(1));
        Assert.That(session.EnsureDailyPrices().ElapsedDays, Is.EqualTo(session.ElapsedDays));
    }

    /// <summary>잔액 부족 시 유지비·날짜를 반영하지 않고 오류를 기록하는지 검사한다.</summary>
    [Test]
    public void DailyMaintenanceInsufficientBalanceLogsAndStopsSettlement()
    {
        Assert.That(session.Economy.FinanceService.TrySpend(
            session.Economy.QueryService.CurrentBalance,
            FinanceChangeReason.Sale,
            out _));
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start();
        progress.OpenBusiness();
        var day = progress.CurrentDayProgress;
        progress.Tick(day.BusinessDurationSeconds);
        Assert.That(progress.SubmitOffer(long.MaxValue,
            day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray()), Is.False);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            @"\[Maintenance\] DAY 1 .*200 G.*0 G"));
        Assert.Throws<InvalidOperationException>(() => progress.CompleteTransactionResult());
        Assert.That(day.State, Is.EqualTo(DayProgressState.Closing));
        Assert.That(session.ElapsedDays, Is.Zero);
        Assert.That(session.Economy.MaintenanceService.LastPaidDay, Is.Zero);
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.Zero);
    }

    /// <summary>설비 구매와 명성 정산이 같은 날짜 완료 경계에서 각각 한 번 적용된다.</summary>
    [Test]
    public void FacilityAndReputationShareCompletedDayBoundary()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); progress.OpenBusiness();
        var day = progress.CurrentDayProgress;
        long balance = session.Economy.QueryService.CurrentBalance;
        long acceptedIncome = 0;
        for (int index = 0; index < 5; index++)
        {
            CustomerVisit normal = generateAdultNormalWithMorality(index + 30);
            normal.BeginOffer();
            typeof(DayProgress).GetField("currentVisit", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(day, normal);
            progress.BeginCustomerSorting();
            var items = day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
            long offered = acceptedOffer(day.CurrentVisit, items);
            Assert.That(progress.SubmitOffer(offered, items));
            acceptedIncome += offered;
            progress.CompleteTransactionResult();
        }
        progress.Tick(day.BusinessDurationSeconds);
        decimal moralityBeforeRefusal = session.CurrentMorality;
        Assert.That(progress.SubmitOffer(long.MaxValue, day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray()), Is.False);
        Assert.That(day.CurrentVisit.Result.Value.MoralityDelta.HasValue);
        Assert.That(session.CurrentMorality, Is.EqualTo(moralityBeforeRefusal + day.CurrentVisit.Result.Value.MoralityDelta.Value));
        progress.CompleteTransactionResult();
        Assert.That(day.AggregationResult.Value.Transactions.Count, Is.EqualTo(6));
        Assert.That(day.AggregationResult.Value.Transactions.Last().Outcome, Is.EqualTo(CustomerTradeOutcome.PaymentRefused));
        Assert.That(day.AggregationResult.Value.Transactions.Take(5).All(x => x.CostTotal > 0));
        Assert.That(progress.ReputationLogService.TransactionEntries.Count, Is.EqualTo(6));
        Assert.That(progress.ReputationLogService.SettlementEntries.Count, Is.EqualTo(1));
        int delta = day.DailyReputationResult.Value.FinalDelta;
        Assert.That(delta, Is.GreaterThan(0));
        Assert.That(progress.TryPurchaseFacility(12001, out var purchase));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance + acceptedIncome - day.AggregationResult.Value.Expenses - purchase.PaidAmount));
        Assert.That(progress.CurrentReputation, Is.Zero);
        Assert.That(session.IsFacilityActive(12001), Is.False);
        progress.CompleteSettlement();
        Assert.That(session.ElapsedDays, Is.EqualTo(1));
        Assert.That(session.IsFacilityActive(12001));
        Assert.That(progress.CurrentReputation, Is.EqualTo(delta));
        Assert.That(progress.CurrentDayProgress.DayStartReputation, Is.EqualTo(delta));
        Assert.Throws<InvalidOperationException>(() => day.CompleteSettlement());
        Assert.Throws<InvalidOperationException>(() => session.CompleteDay(0));
        Assert.That(progress.CurrentReputation, Is.EqualTo(delta));
        Assert.That(progress.ReputationLogService.SettlementEntries.Count, Is.EqualTo(1));
        var reentered = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(2));
        reentered.Start();
        Assert.That(reentered.CurrentReputation, Is.EqualTo(delta));
        Assert.That(reentered.CurrentDayProgress.DayStartReputation, Is.EqualTo(delta));
        Assert.That(reentered.ReputationLogService, Is.SameAs(progress.ReputationLogService));
    }

    /// <summary>대기열은 Visit 정체성을 유지하고 빈 계산대에서 새 방문을 즉석 생성하지 않는다.</summary>
    [Test]
    public void QueueProgressFifoEmptyCounterAndPause()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1), 90, true);
        progress.Start(); progress.OpenBusiness();
        var day = progress.CurrentDayProgress;
        Assert.That(day.UsesCustomerQueue);
        Assert.That(day.CurrentVisit.State, Is.EqualTo(CustomerState.AwaitingOffer));
        Assert.That(day.WaitingCustomers, Is.Empty);
        progress.BeginCustomerSorting();
        var first = day.CurrentVisit;
        progress.SubmitOffer(1, first.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        progress.CompleteTransactionResult();
        Assert.That(first.State, Is.EqualTo(CustomerState.Departed));
        Assert.That(day.CurrentVisit, Is.Null);
        Assert.That(day.State, Is.EqualTo(DayProgressState.Operating));
        progress.Pause(); progress.Tick(50);
        Assert.That(day.CurrentVisit, Is.Null); Assert.That(day.WaitingCustomers, Is.Empty);
        progress.Resume(); progress.Tick(5);
        Assert.That(day.CurrentVisit, Is.Not.Null);
        var counter = day.CurrentVisit;
        progress.Tick(5);
        var queued = day.WaitingCustomers[0].Visit;
        progress.BeginCustomerSorting();
        progress.SubmitOffer(1, counter.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        progress.CompleteTransactionResult();
        Assert.That(day.CurrentVisit, Is.SameAs(queued));
        Assert.That(day.WaitingCustomers, Is.Empty);
        Assert.That(day.CurrentVisit.State, Is.EqualTo(CustomerState.AwaitingOffer));
        Assert.Throws<InvalidOperationException>(() => progress.CompleteTransactionResult());
        progress.Tick(20);
        Assert.That(day.CurrentVisit, Is.SameAs(queued));
        Assert.That(day.CurrentVisit.State, Is.EqualTo(CustomerState.AwaitingOffer));
    }

    /// <summary>긴 프레임은 만료를 먼저 확정하며 계산대 인계와 마감 시 새 거래를 만들지 않는다.</summary>
    [Test]
    public void QueueProgressExpiryHitchAndClosing()
    {
        foreach (var disposition in tables.Customers.Dispositions.Rows.Values) disposition.QueuePatienceSeconds = 9;
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1), 30, true);
        progress.Start(); progress.OpenBusiness(); progress.Tick(5);
        var day = progress.CurrentDayProgress;
        var expired = day.WaitingCustomers[0].Visit;
        progress.BeginCustomerSorting();
        progress.SubmitOffer(1, day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        progress.Tick(9); // 5초 입장자는 14초에 만료한다.
        Assert.That(expired.State, Is.EqualTo(CustomerState.Abandoned));
        Assert.That(expired.Result.HasValue, Is.False);
        var next = day.WaitingCustomers[0].Visit;
        progress.CompleteTransactionResult();
        Assert.That(day.CurrentVisit, Is.SameAs(next));
        Assert.That(day.CurrentVisit, Is.Not.SameAs(expired));
        Assert.That(day.DepartedCustomers, Is.EqualTo(1));
        progress.Tick(1000);
        Assert.That(day.State, Is.EqualTo(DayProgressState.Closing));
        Assert.That(day.WaitingCustomers, Is.Empty); Assert.That(day.LeavingCustomers, Is.Empty);
        Assert.That(day.CurrentVisit, Is.SameAs(next));
        var nextItems = next.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray();
        Assert.That(progress.SubmitOffer(acceptedOffer(next, nextItems), nextItems));
        progress.CompleteTransactionResult();
        Assert.That(day.State, Is.EqualTo(DayProgressState.Settlement));
        Assert.That(day.AggregationResult.Value.Transactions.Count, Is.EqualTo(2));
        Assert.That(progress.ReputationLogService.TransactionEntries.Count, Is.EqualTo(2));
    }

    /// <summary>긴 빈 계산대 프레임에서는 프레임 종료 시 살아 있는 맨 앞만 인계한다.</summary>
    [Test]
    public void QueueEmptyCounterHitchSkipsExpiredArrivals()
    {
        foreach (var disposition in tables.Customers.Dispositions.Rows.Values) disposition.QueuePatienceSeconds = 9;
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1), 90, true);
        progress.Start(); progress.OpenBusiness(); progress.BeginCustomerSorting();
        progress.SubmitOffer(1, progress.CurrentDayProgress.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        progress.CompleteTransactionResult(); progress.Tick(20);
        var day = progress.CurrentDayProgress;
        Assert.That(day.DepartedCustomers, Is.EqualTo(2));
        Assert.That(day.LeavingCustomers.All(x => x.Visit.State == CustomerState.Abandoned));
        Assert.That(day.CurrentVisit.State, Is.EqualTo(CustomerState.AwaitingOffer));
        Assert.That(day.WaitingCustomers.Count, Is.EqualTo(1));
        Assert.That(day.LeavingCustomers.All(x => !ReferenceEquals(x.Visit, day.CurrentVisit)));
    }

    /// <summary>진행 Tick의 pause와 방송, 방문 snapshot 및 제출 단가의 동일 원본을 검사한다.</summary>
    [Test]
    public void ProgressRadioUsesOnlyUnpausedTradingTime()
    {
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1), 90);
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
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1), 10);
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
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start();
        string firstList = factory.CreatePriceListText(progress.CurrentDay, session.DailyPrices);
        Assert.That(firstList, Does.Not.Contain(name + "  ·"));
        var discounted = CustomerProductAvailability.GetAvailableProducts(tables.Customers.Products.Rows, 0)
            .First(x => session.DailyPrices.Prices[x.Idx] != x.BasePrice);
        string discountedName = tables.GetDB<TextDataTable>(DataTableType.Text).Rows[discounted.NameIdx].Text;
        Assert.That(firstList, Does.Contain($"{discountedName}  ·  {session.DailyPrices.Prices[discounted.Idx]:N0} G"));
        Assert.That(firstList, Does.Not.Contain($"{discountedName}  ·  {discounted.BasePrice:N0} G"));
        Assert.Throws<InvalidOperationException>(() => factory.CreatePriceListText(2, session.DailyPrices));
        Assert.Throws<InvalidOperationException>(() => new DayProgress(2, session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1)));
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
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        Assert.Throws<InvalidOperationException>(() => progress.TryPurchaseFacility(12005, out _));
        progress.Start();
        long balance = session.Economy.QueryService.CurrentBalance;
        Assert.That(progress.CurrentStoreStage, Is.EqualTo(1));
        Assert.That(progress.TryPurchaseFacility(12005, out var lockedPurchase), Is.False);
        Assert.That(lockedPurchase.Status, Is.EqualTo(FacilityPurchaseStatus.StageLocked));
        Assert.That(progress.TryPurchaseFacility(12008, out _));
        Assert.That(progress.CurrentStoreStage, Is.EqualTo(2));
        Assert.That(progress.TryPurchaseFacility(12010, out _));
        Assert.That(progress.CurrentStoreStage, Is.EqualTo(3));
        long price = tables.GetDB<FacilityDataTable>(DataTableType.Facility).Rows[12005].PurchasePrice;
        long stagePrice = tables.GetDB<FacilityDataTable>(DataTableType.Facility).Rows[12008].PurchasePrice
            + tables.GetDB<FacilityDataTable>(DataTableType.Facility).Rows[12010].PurchasePrice;
        Assert.That(progress.TryPurchaseFacility(12005, out var purchase));
        Assert.That(purchase.ActivationDay, Is.EqualTo(1));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance - stagePrice - price));
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
        var expected = new uint[] { 1001, 1004, 1007, 1010, 1020, 1021 };
        Assert.That(CustomerProductAvailability.GetAvailableProducts(tables.Customers.Products.Rows, 1, session.IsFacilityActive).Select(x => x.Idx), Is.EquivalentTo(expected));
        for (int i = 0; i < 20; i++) Assert.That(generate().Items.All(x => expected.Contains(x.ProductIdx)));
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.InspectorEvent));
        completeInspectors(progress);
        progress.OpenBusiness(); progress.BeginCustomerSorting();
        var facilityItems = new[] { new SaleItem(1020, 1) };
        Assert.That(progress.SubmitOffer(acceptedOffer(progress.CurrentDayProgress.CurrentVisit, facilityItems), facilityItems));
        // 표현/진행 객체 수명이 바뀌어도 보유는 세션에 남는다. 실제 씬은 수정하지 않는다.
        var nextProgress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(2));
        Assert.That(session.FacilityActivationDays.Count, Is.EqualTo(3));
        Assert.Throws<InvalidOperationException>(() => session.InitializeNewGame(tables));
    }

    /// <summary>세션 파괴 후 새 세션은 보유·활성일을 이어받지 않는다.</summary>
    /// <returns>테스트 소유 세션 파괴 완료 대기.</returns>
    [UnityTest]
    public IEnumerator NewSessionClearsFacilityOwnership()
    {
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1)); progress.Start();
        Assert.That(progress.TryPurchaseFacility(12001, out _));
        UnityEngine.Object.Destroy(session); yield return null;
        Assert.That(GameSessionManager.Instance, Is.Null);
        session = root.AddComponent<GameSessionManager>(); session.InitializeNewGame(tables);
        Assert.That(session.FacilityActivationDays, Is.Empty); Assert.That(session.ElapsedDays, Is.Zero);
        Assert.That(session.IsFacilityActive(12001), Is.False);
    }

#if UNITY_EDITOR
    /// <summary>월드 표시의 방문 identity·퇴장/대사 수명·pause·숨김·재활성 정리를 검사한다.</summary>
    /// <returns>실제 프레임과 표시 보간 대기.</returns>
    [UnityTest]
    public IEnumerator InspectorWorldQueuePreservesIdentityAndIndependentSpeechLifetime()
    {
        var ui = createGameUi();
        var settings = new UnityEditor.SerializedObject(ui);
        settings.FindProperty("useCustomerQueue").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var world = createWorld(ui);
        var queue = world.GetComponent<CustomerWorldQueueView>();
        yield return waitForGameUi(ui);
        Assert.That(queue.VisualCount, Is.Zero);
        var progress = uiProgress(ui);
        completeInspectors(progress);
        progress.OpenBusiness();
        yield return new WaitForSeconds(.85f);
        var day = progress.CurrentDayProgress;
        var current = day.CurrentVisit;
        var currentBody = world.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.sortingOrder == 200);
        Assert.That(currentBody.GetComponentInParent<Canvas>(), Is.Null);
        Assert.That(currentBody.color.a, Is.EqualTo(1).Within(.001f));
        progress.Tick(5);
        yield return null;
        var waiting = day.WaitingCustomers[0].Visit;
        var states = (IDictionary)typeof(CustomerWorldQueueView).GetField("visuals", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(queue);
        var visual = states[waiting];
        var type = visual.GetType();
        var body = (SpriteRenderer)type.GetField("Body").GetValue(visual);
        var speech = (TMPro.TextMeshPro)type.GetField("Speech").GetValue(visual);
        yield return new WaitForSeconds(.8f);
        Assert.That(states[waiting], Is.SameAs(visual));
        progress.Tick(tables.Customers.Dispositions.Rows[waiting.DispositionIdx].QueuePatienceSeconds);
        yield return null;
        Assert.That(day.LeavingCustomers.Any(x => ReferenceEquals(x.Visit, waiting)), Is.True);
        Vector3 speechPosition = speech.transform.localPosition;
        yield return new WaitForSeconds(.55f);
        Assert.That(body.color.a, Is.Zero.Within(.001f));
        Assert.That(speech.color.a, Is.EqualTo(1).Within(.001f));
        Assert.That(speech.text, Is.Not.Empty);
        Assert.That(speech.transform.localPosition, Is.EqualTo(speechPosition));
        progress.Pause();
        float remaining = day.RemainingSeconds;
        yield return new WaitForSeconds(.2f);
        Assert.That(day.RemainingSeconds, Is.EqualTo(remaining));
        Assert.That(speech.color.a, Is.EqualTo(1).Within(.001f));
        ui.FrontView.gameObject.SetActive(false);
        yield return null;
        Assert.That(world.RenderRoot.gameObject.activeSelf, Is.False);
        ui.FrontView.gameObject.SetActive(true);
        yield return null;
        Assert.That(speech.color.a, Is.EqualTo(1).Within(.001f));
        progress.Resume();
        progress.Tick(3);
        yield return null;
        Assert.That(states.Contains(waiting), Is.False);
        world.gameObject.SetActive(false);
        Assert.That(queue.VisualCount, Is.Zero);
        world.gameObject.SetActive(true);
        yield return null;
        Assert.That(day.CurrentVisit, Is.SameAs(current));
        Assert.That(queue.VisualCount, Is.GreaterThan(0));
        Assert.That(world.GetComponentsInChildren<SpriteRenderer>(true).Count(x => x.sortingOrder == 200), Is.EqualTo(1));
    }

    /// <summary>실제 UI의 시계와 배경은 DayProgress를 따르며 테스트 배경 시각은 영업 시간을 바꾸지 않는다.</summary>
    /// <returns>실제 prefab 초기화 대기.</returns>
    [UnityTest]
    public IEnumerator InspectorMainClockFollowsDayProgressAndPreviewCannotAdvanceBusiness()
    {
        var ui = createGameUi();
        yield return waitForGameUi(ui);
        var progress = uiProgress(ui);
        var clock = uiReference<BusinessClockController>(ui, "businessClock");
        Assert.That(clock, Is.Not.Null);
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.InspectorEvent));
        float beforeOpeningRemaining = progress.CurrentDayProgress.RemainingSeconds;
        progress.Tick(60f);
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(BusinessHours.OpenMinutes));
        Assert.That(progress.CurrentDayProgress.RemainingSeconds, Is.EqualTo(beforeOpeningRemaining));
        completeInspectors(progress);
        progress.OpenBusiness();
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(BusinessHours.OpenMinutes));
        var day = progress.CurrentDayProgress;
        Assert.That(day.BusinessDurationSeconds, Is.EqualTo(30f));
        progress.Tick(15f);
        var refresh = typeof(GameUIController).GetMethod("refreshFrameViews", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        refresh.Invoke(ui, null);
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(900f));
        Assert.That(clock.IsRunning, Is.False);
        progress.Pause();
        progress.Tick(10f);
        refresh.Invoke(ui, null);
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(900f));
        Assert.That(day.RemainingSeconds, Is.EqualTo(15f));
        progress.Resume();
        var background = createWorld(ui);
        background.RefreshPresentation();
        Assert.That(background.CurrentAppliedHour, Is.EqualTo(15f));
        var settings = new UnityEditor.SerializedObject(background);
        settings.FindProperty("debugOverrideTime").boolValue = true;
        settings.FindProperty("debugHour").floatValue = 20f;
        settings.FindProperty("autoAdvanceClockForTesting").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        typeof(WorldSceneView).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(background, null);
        background.RefreshPresentation();
        Assert.That(background.CurrentAppliedHour, Is.GreaterThanOrEqualTo(20f));
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(900f));
        Assert.That(day.RemainingSeconds, Is.EqualTo(15f));
        settings.Update();
        settings.FindProperty("debugOverrideTime").boolValue = false;
        settings.FindProperty("autoAdvanceClockForTesting").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        progress.Tick(15f);
        refresh.Invoke(ui, null);
        background.RefreshPresentation();
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(BusinessHours.CloseMinutes));
        Assert.That(background.CurrentAppliedHour, Is.EqualTo(BusinessHours.CloseHour));
        Assert.That(day.IsBusinessTimeExpired, Is.True);
    }

    /// <summary>실제 CSV 첫 대사·불투명 초기 덮개·입력 잠금·패널 재생성·퇴장 후 영업을 검증한다.</summary>
    /// <returns>프리팹 초기화와 실제 페이드 대기.</returns>
    [UnityTest]
    public IEnumerator InspectorStartupCoverDialogueRecreationAndExit()
    {
        long balance = session.Economy.QueryService.CurrentBalance;
        var ui = createGameUi();
        var cover = uiReference<CanvasGroup>(ui, "startupCover");
        Assert.That(cover.gameObject.activeInHierarchy); Assert.That(cover.alpha, Is.EqualTo(1)); Assert.That(cover.blocksRaycasts);
        Assert.That(uiReference<GameInputRouter>(ui, "gameInputRouter").enabled, Is.False);
        yield return waitForGameUi(ui);
        var progress = uiProgress(ui);
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.InspectorEvent));
        Assert.That(cover.gameObject.activeSelf, Is.False); Assert.That(cover.blocksRaycasts, Is.False);
        Assert.Throws<InvalidOperationException>(progress.OpenBusiness);
        Assert.Throws<InvalidOperationException>(() => progress.TryPurchaseFacility(12001, out _));
        progress.Tick(60); Assert.That(session.Economy.QueryService.IsDayOpen, Is.False);
        var panel = uiReference<InspectorPresenter>(ui, "inspectorPresenter");
        var next = uiReference<UnityEngine.UI.Button>(panel, "nextButton");
        Assert.That(next.interactable, Is.False);
        yield return new WaitForSeconds(.5f); Assert.That(next.interactable);
        ui.gameObject.SetActive(false); ui.gameObject.SetActive(true);
        Assert.That(next.interactable, Is.False);
        yield return new WaitForSeconds(.5f); Assert.That(next.interactable, "same root re-enable must resume entry");
        var first = session.InspectorEvents.Current;
        next.onClick.Invoke(); Assert.That(session.InspectorEvents.Current.LineIndex, Is.EqualTo(1));
        Assert.That(progress.CurrentDayProgress.AdvanceInspector(first), Is.False);
        UnityEngine.Object.Destroy(ui.gameObject); yield return null;
        ui = createGameUi(); yield return waitForGameUi(ui); progress = uiProgress(ui);
        Assert.That(session.InspectorEvents.Current.LineIndex, Is.EqualTo(1));
        panel = uiReference<InspectorPresenter>(ui, "inspectorPresenter");
        next = uiReference<UnityEngine.UI.Button>(panel, "nextButton");
        yield return new WaitForSeconds(.5f);
        for (int i=1; i<20; i++) next.onClick.Invoke();
        Assert.That(session.InspectorEvents.Current.Phase, Is.EqualTo(InspectorEventPhase.AwaitingExit));
        // 퇴장 중 비활성화는 세션 완료를 만들지 않는다. 새 화면이 같은 퇴장을 이어 처리한다.
        UnityEngine.Object.Destroy(ui.gameObject); yield return null;
        Assert.That(session.InspectorEvents.HasPending);
        ui = createGameUi(); yield return waitForGameUi(ui);
        yield return new WaitForSeconds(.5f);
        Assert.That(ui.CurrentDayProgress.State, Is.EqualTo(DayProgressState.PreOpen));
        Assert.That(session.InspectorEvents.HasPending, Is.False);
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance));
        Assert.That(session.CurrentMorality, Is.Zero); Assert.That(session.CurrentReputation, Is.Zero);
        uiProgress(ui).OpenBusiness(); Assert.That(session.Economy.QueryService.IsDayOpen);
    }

    /// <summary>실제 단계 구매가 3일차에 즉시 감독관을 추가하지 않고 4일차에 한 번 나타나는지 검사한다.</summary>
    [Test]
    public void InspectorPurchaseDayThreeAppearsDayFourOnce()
    {
        var progress = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); completeInspectors(progress);
        for(int i=0; i<2; i++) { completeInspectors(progress); closeProgressDay(progress); progress.CompleteSettlement(); }
        Assert.That(progress.CurrentDay, Is.EqualTo(3));
        Assert.That(progress.TryPurchaseFacility(12008, out _)); Assert.That(progress.TryPurchaseFacility(12010, out _));
        Assert.That(session.CurrentStoreStage, Is.EqualTo(3));
        var reentry = new GameProgress(session, tables.Customers, tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        reentry.Start(); Assert.That(reentry.CurrentDayProgress.State, Is.EqualTo(DayProgressState.PreOpen));
        closeProgressDay(progress); progress.CompleteSettlement();
        Assert.That(progress.CurrentDay, Is.EqualTo(4));
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.InspectorEvent));
        Assert.That(session.InspectorEvents.Current.EventIdx, Is.EqualTo(15002));
        long balance=session.Economy.QueryService.CurrentBalance;
        completeInspectors(progress); Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance));
        closeProgressDay(progress); progress.CompleteSettlement();
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.PreOpen));
    }

    /// <summary>실제 CSV/FK로 로드한 임시 이벤트가 무설비 2일차에만 등장하고 퇴장 후 정상 진행되는지 검사한다.</summary>
    [Test]
    public void InspectorDayTwoTemporaryEventAppearsOnceWithoutFacilities()
    {
        var progress = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(1));
        progress.Start(); completeInspectors(progress);
        closeProgressDay(progress); progress.CompleteSettlement();
        Assert.That(progress.CurrentDay, Is.EqualTo(2));
        Assert.That(session.FacilityActivationDays, Is.Empty);
        Assert.That(session.CurrentStoreStage, Is.EqualTo(1));
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.InspectorEvent));
        var snapshot = session.InspectorEvents.Current;
        Assert.That(snapshot.EventIdx, Is.EqualTo(15003));
        Assert.That(snapshot.TextIdx, Is.EqualTo(8181));
        Assert.That(snapshot.PortraitResourceIdx, Is.EqualTo(4201));
        var text = tables.GetDB<TextDataTable>(DataTableType.Text);
        Assert.That(text.Rows[8180].Text, Is.EqualTo("2일차 등장 확인"));
        Assert.That(text.Rows[snapshot.TextIdx].Text, Is.EqualTo("2일차 감독관 등장 확인용 임시 대사입니다."));
        long balance = session.Economy.QueryService.CurrentBalance;
        decimal morality = session.CurrentMorality;
        int reputation = session.CurrentReputation;
        Assert.Throws<InvalidOperationException>(progress.OpenBusiness);
        Assert.That(progress.CurrentDayProgress.AdvanceInspector(snapshot));
        Assert.That(session.InspectorEvents.Current.Phase, Is.EqualTo(InspectorEventPhase.AwaitingExit));
        Assert.Throws<InvalidOperationException>(progress.OpenBusiness);
        Assert.That(progress.CurrentDayProgress.CompleteInspectorExit(snapshot));
        Assert.That(progress.CurrentDayProgress.CompleteInspectorExit(snapshot), Is.False);
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.PreOpen));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(balance));
        Assert.That(session.CurrentMorality, Is.EqualTo(morality));
        Assert.That(session.CurrentReputation, Is.EqualTo(reputation));
        var reentry = new GameProgress(session, tables.Customers,
            tables.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance), new System.Random(2));
        reentry.Start();
        Assert.That(reentry.CurrentDayProgress.State, Is.EqualTo(DayProgressState.PreOpen));
        closeProgressDay(reentry); reentry.CompleteSettlement();
        Assert.That(reentry.CurrentDay, Is.EqualTo(3));
        Assert.That(reentry.CurrentDayProgress.State, Is.EqualTo(DayProgressState.PreOpen));
        Assert.That(session.InspectorEvents.HasPending, Is.False);
    }

    /// <summary>초기화 실패 시 덮개가 입력을 계속 차단하고 오류 문구가 위에 표시되는지 검사한다.</summary>
    /// <returns>UI 오류 처리 완료 대기.</returns>
    [UnityTest]
    public IEnumerator InspectorStartupFailureKeepsVisibleErrorCover()
    {
        var ui=createGameUi();
        var serialized=new UnityEditor.SerializedObject(ui);
        serialized.FindProperty("inspectorPresenter").objectReferenceValue=null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("감독관 패널 또는 초기화 덮개 참조"));
        float deadline=Time.realtimeSinceStartup+20;
        var error=uiReference<TMPro.TextMeshProUGUI>(ui,"startupErrorText");
        while(string.IsNullOrEmpty(error.text) && Time.realtimeSinceStartup<deadline) yield return null;
        Assert.That(error.text, Does.Contain("감독관 패널")); Assert.That(error.gameObject.activeInHierarchy);
        var cover=uiReference<CanvasGroup>(ui,"startupCover"); Assert.That(cover.alpha,Is.EqualTo(1)); Assert.That(cover.blocksRaycasts);
        Assert.That(uiReference<GameInputRouter>(ui,"gameInputRouter").enabled,Is.False);
    }

    /// <summary>로컬 큐 옵션의 빈 계산대와 마지막 퇴장 표시 대기가 도메인 정산을 중복시키지 않는다.</summary>
    /// <returns>UI 초기화와 표현 지연 완료 대기.</returns>
    [UnityTest]
    public IEnumerator QueueControllerEmptyCounterAndFinalExitPresentation()
    {
        var ui = createGameUi();
        var serialized = new UnityEditor.SerializedObject(ui);
        serialized.FindProperty("useCustomerQueue").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        yield return waitForGameUi(ui);
        var progress = uiProgress(ui);
        var customer = uiReference<CustomerPresenter>(ui, "customerPresenter");
        var sorting = uiReference<SaleSortingPanel>(ui, "saleSortingPanel");
        progress.OpenBusiness();
        progress.Pause();
        yield return new WaitForSecondsRealtime(0.1f);
        Assert.That(progress.CurrentDayProgress.RemainingSeconds, Is.EqualTo(30));
        Assert.That(uiReference<UnityEngine.UI.Button>(sorting, "frontContainerButton").gameObject.activeSelf, Is.False);
        progress.Resume();
        progress.BeginCustomerSorting();
        var day = progress.CurrentDayProgress;
        progress.SubmitOffer(1, day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        progress.CompleteTransactionResult();
        Assert.That(day.CurrentVisit, Is.Null);
        Assert.That(uiReference<GameObject>(customer, "customerUIRoot").activeSelf, Is.False);
        Assert.That(uiReference<UnityEngine.UI.Button>(sorting, "frontContainerButton").gameObject.activeSelf, Is.False);
        progress.Tick(5);
        Assert.That(day.CurrentVisit, Is.Not.Null);
        progress.Tick(100);
        progress.SubmitOffer(1, day.CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        progress.CompleteTransactionResult();
        Assert.That(day.State, Is.EqualTo(DayProgressState.Settlement));
        Assert.That(ui.IsSettlementPresentationPending);
        Assert.That(uiReference<GameObject>(ui, "settlementPanel").activeSelf, Is.False);
        Assert.That(uiReference<GameObject>(ui, "operatingPanel").activeSelf);
        Assert.That(uiReference<UnityEngine.UI.Button>(ui, "facilityOpenButton").gameObject.activeSelf, Is.False);
        yield return new WaitForSeconds(ui.QueueExitSeconds + 0.1f);
        Assert.That(ui.IsSettlementPresentationPending, Is.False);
        Assert.That(uiReference<GameObject>(ui, "settlementPanel").activeSelf);
        Assert.That(progress.ReputationLogService.SettlementEntries.Count, Is.EqualTo(1));
    }

    /// <summary>독립 프리팹의 반복 열기·표시 갱신은 요청을 만들지 않고 클릭만 PK를 한 번 전달한다.</summary>
    /// <returns>UI 수명과 TMP 배치 갱신 대기.</returns>
    [UnityTest]
    public IEnumerator FacilityPresenterRequestsAndLongValues()
    {
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab");
        var panel = UnityEngine.Object.Instantiate(asset, root.transform).GetComponent<FacilityShopPresenter>();
        var factory = new ProgressViewDataFactory(tables.Customers, tables.GetDB<TextDataTable>(DataTableType.Text),
            new System.Collections.Generic.Dictionary<uint, Sprite>());
        var view = factory.CreateFacilityShopViewData(tables.GetDB<FacilityDataTable>(DataTableType.Facility).Rows,
            session.FacilityActivationDays, session.CurrentStoreStage, session.ElapsedDays,
            session.Economy.QueryService.CurrentBalance);
        int purchases = 0, closes = 0; uint requested = 0;
        panel.OnPurchaseRequested += idx => { purchases++; requested = idx; };
        panel.OnCloseRequested += () => closes++;
        for (int i = 0; i < 3; i++)
        {
            panel.gameObject.SetActive(true); panel.UpdateView(view, ""); panel.UpdateView(view, "");
            Assert.That(purchases, Is.Zero); panel.gameObject.SetActive(false);
        }
        panel.gameObject.SetActive(true); panel.UpdateView(view, ""); yield return null;
        var rows = panel.GetComponentsInChildren<FacilityItemView>(); Assert.That(rows.Length, Is.EqualTo(11));
        var button = uiReference<UnityEngine.UI.Button>(rows[0], "purchaseButton");
        button.onClick.Invoke(); Assert.That(purchases, Is.EqualTo(1)); Assert.That(requested, Is.EqualTo(12001u));
        panel.SetInteractionEnabled(false); button.onClick.Invoke(); Assert.That(purchases, Is.EqualTo(1));
        uiReference<UnityEngine.UI.Button>(panel, "closeButton").onClick.Invoke(); Assert.That(closes, Is.EqualTo(1));
        rows[0].UpdateView(new FacilityItemViewData(12001, new string('가', 30), long.MaxValue,
            "방독면, 방호복, 방사능 측정기", FacilityDisplayState.Purchasable, 4294967297UL), true);
        Canvas.ForceUpdateCanvases();
        foreach (var text in rows[0].GetComponentsInChildren<TMPro.TextMeshProUGUI>())
        {
            text.ForceMeshUpdate(); Assert.That(text.raycastTarget, Is.False);
            Assert.That(text.isTextOverflowing, Is.False, text.name);
        }
    }

    /// <summary>실제 GameUI는 정산에서만 구매하고 뒤 Submit·중복 구매를 막으며 다음날을 해금한다.</summary>
    /// <returns>Controller 시작 대기.</returns>
    [UnityTest]
    public IEnumerator FacilityControllerPurchaseAndModalBoundaries()
    {
        var ui = createGameUi(); yield return waitForGameUi(ui);
        var progress = uiProgress(ui); var open = uiReference<UnityEngine.UI.Button>(ui, "facilityOpenButton");
        var panel = uiReference<FacilityShopPresenter>(ui, "facilityShopPresenter");
        Assert.That(open.gameObject.activeInHierarchy, Is.False); open.onClick.Invoke(); Assert.That(panel.gameObject.activeSelf, Is.False);
        closeProgressDay(progress);
        Assert.That(open.gameObject.activeInHierarchy); open.onClick.Invoke();
        var settlement = uiReference<DailySettlementPresenter>(ui, "dailySettlementPresenter");
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(settlement, "expensesText").text, Is.EqualTo("-200 G"));
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(settlement, "netProfitText").text, Does.Contain("-200 G"));
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(settlement, "currentBalanceText").text, Is.EqualTo("99,800 G"));
        var next = uiReference<UnityEngine.UI.Button>(settlement, "nextStepButton");
        Assert.That(next.IsInteractable(), Is.False);
        UnityEngine.EventSystems.ExecuteEvents.Execute(next.gameObject, new UnityEngine.EventSystems.BaseEventData(null), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
        Assert.That(progress.CurrentDayProgress.State, Is.EqualTo(DayProgressState.Settlement));
        Assert.That(uiReference<GameInputRouter>(ui, "gameInputRouter").enabled, Is.False);
        var rows = panel.GetComponentsInChildren<FacilityItemView>(); var buy = uiReference<UnityEngine.UI.Button>(rows[0], "purchaseButton");
        long previous = session.Economy.QueryService.CurrentBalance;
        buy.onClick.Invoke(); buy.onClick.Invoke();
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(previous - 1000));
        Assert.That(session.FacilityActivationDays.Count, Is.EqualTo(1)); Assert.That(session.IsFacilityActive(12001), Is.False);
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(rows[0], "statusText").text, Does.Contain("적용 대기"));
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(settlement, "currentBalanceText").text, Is.EqualTo($"{previous - 1000:N0} G"));
        session.Economy.FinanceService.TrySpend(session.Economy.QueryService.CurrentBalance, FinanceChangeReason.Maintenance, out _);
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(panel, "balanceText").text, Does.Contain("0 G"));
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(rows[1], "statusText").text, Is.EqualTo("잔액 부족"));
        uiReference<UnityEngine.UI.Button>(panel, "closeButton").onClick.Invoke();
        Assert.That(session.ElapsedDays, Is.Zero); Assert.That(next.IsInteractable());
        Assert.That(uiReference<GameInputRouter>(ui, "gameInputRouter").enabled);
        next.onClick.Invoke(); Assert.That(session.ElapsedDays, Is.EqualTo(1)); Assert.That(session.IsFacilityActive(12001));
        Assert.That(open.gameObject.activeInHierarchy, Is.False);
        session.Economy.FinanceService.AddIncome(1000, FinanceChangeReason.Sale);
        completeInspectors(progress);
        closeProgressDay(progress); open.onClick.Invoke();
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(rows[0], "statusText").text, Is.EqualTo("사용 중"));
    }

    /// <summary>차감 후 알림 예외도 보유를 표시하고 자동 재결제 없이 기존 기술 오류 잠금을 따른다.</summary>
    /// <returns>Controller 시작 대기.</returns>
    [UnityTest]
    public IEnumerator FacilityControllerNotificationFailurePreservesPurchase()
    {
        var ui = createGameUi(); yield return waitForGameUi(ui);
        closeProgressDay(uiProgress(ui)); uiReference<UnityEngine.UI.Button>(ui, "facilityOpenButton").onClick.Invoke();
        var panel = uiReference<FacilityShopPresenter>(ui, "facilityShopPresenter");
        var row = panel.GetComponentsInChildren<FacilityItemView>()[0];
        Action<FinanceChangeResult> fail = _ => throw new InvalidOperationException("ui purchase notification");
        session.Economy.FinanceService.BalanceChanged += fail;
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("ui purchase notification"));
        uiReference<UnityEngine.UI.Button>(row, "purchaseButton").onClick.Invoke();
        session.Economy.FinanceService.BalanceChanged -= fail;
        Assert.That(session.FacilityActivationDays.ContainsKey(12001));
        Assert.That(session.Economy.QueryService.CurrentBalance, Is.EqualTo(98800));
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(row, "statusText").text, Does.Contain("적용 대기"));
        Assert.That(uiReference<TMPro.TextMeshProUGUI>(panel, "feedbackText").text, Does.Contain("처리 오류"));
        Assert.That(panel.GetComponentsInChildren<FacilityItemView>().All(x => !uiReference<UnityEngine.UI.Button>(x, "purchaseButton").interactable));
        uiReference<UnityEngine.UI.Button>(panel, "closeButton").onClick.Invoke();
        Assert.That(panel.gameObject.activeSelf, Is.False); Assert.That(uiReference<CanvasGroup>(ui, "settlementInputGroup").interactable, Is.False);
    }

    /// <summary>고정 한 프레임 대신 실제 이미지 로드와 진행 초기화 완료를 기다린다.</summary>
    /// <param name="ui">테스트 소유 화면.</param><returns>최대20초 초기화 대기.</returns>
    private static IEnumerator waitForGameUi(GameUIController ui)
    {
        float deadline = Time.realtimeSinceStartup + 20;
        while ((uiProgress(ui) == null || uiReference<CanvasGroup>(ui, "startupCover").gameObject.activeSelf) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(uiProgress(ui), Is.Not.Null, "GameUI image loading/initialization timed out");
        Assert.That(uiReference<CanvasGroup>(ui, "startupCover").gameObject.activeSelf, Is.False, "GameUI first binding/layout did not finish");
    }

    /// <summary>공유 원본을 수정하지 않고 테스트 소유 GameUI 인스턴스를 만든다.</summary>
    /// <returns>테스트 root와 함께 제거할 Controller.</returns>
    private GameUIController createGameUi() => UnityEngine.Object.Instantiate(
        UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/GameUI.prefab"), root.transform).GetComponent<GameUIController>();

    /// <summary>공유 월드 prefab을 Canvas 밖 테스트 소유 카메라에 연결한다.</summary>
    /// <param name="ui">테스트 UI.</param><returns>테스트 소유 월드.</returns>
    private WorldSceneView createWorld(GameUIController ui)
    {
        // 공유 legacy prefab을 쓰는 테스트 인스턴스만 로컬 world UI 계약으로 전환한다.
        // Local 씬/자산을 읽거나 수정하지 않으며 원본 prefab은 보존한다.
        var front = ui.FrontView;
        var legacyTime = front.GetComponent<TimeOfDayUIController>();
        if (legacyTime != null) UnityEngine.Object.DestroyImmediate(legacyTime);
        var presenter = ui.GetComponentInChildren<CustomerPresenter>(true);
        var presenterSettings = new UnityEditor.SerializedObject(presenter);
        var appearance = (UnityEngine.UI.Image)presenterSettings.FindProperty("appearanceImage").objectReferenceValue;
        var label = (TMPro.TextMeshProUGUI)presenterSettings.FindProperty("temporaryGenderText").objectReferenceValue;
        if (label != null && appearance != null && label.transform.IsChildOf(appearance.transform)) label.transform.SetParent(front, true);
        presenterSettings.FindProperty("appearanceImage").objectReferenceValue = null;
        presenterSettings.ApplyModifiedPropertiesWithoutUndo();
        if (appearance != null) UnityEngine.Object.DestroyImmediate(appearance.gameObject);
        foreach (string name in new[] { "FarBackground", "DawnBackground", "SunsetBackground", "EveningBackground", "CityLights",
            "MidBackground", "FogBack", "FogMid", "FogFront", "CrowdBack", "CrowdMiddle", "CrowdFront", "LeftWatchTower",
            "RightWatchTower", "Barricade", "Canopy", "LeftBeam", "RightBeam" })
            if (front.Find(name) != null) UnityEngine.Object.DestroyImmediate(front.Find(name).gameObject);
        var panelImage = presenter.GetComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(panelImage.color.r, panelImage.color.g, panelImage.color.b, 0);
        var camera = new GameObject("WorldCamera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.transform.localPosition = new Vector3(0, 0, -10);
        camera.orthographic = true; camera.orthographicSize = 5;
        var world = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/World/CustomerWorld.prefab"), root.transform).GetComponent<WorldSceneView>();
        world.Bind(ui, camera);
        return world;
    }

    /// <summary>테스트에서 실제 직렬화 참조를 읽는다.</summary>
    /// <typeparam name="T">예상 UI 컴포넌트.</typeparam><param name="target">연결 소유자.</param><param name="field">직렬화 필드.</param>
    /// <returns>연결된 객체.</returns>
    private static T uiReference<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object =>
        (T)new UnityEditor.SerializedObject(target).FindProperty(field).objectReferenceValue;

    /// <summary>제품 API를 늘리지 않고 테스트에서 실제 진행 인스턴스를 관찰한다.</summary>
    /// <param name="ui">테스트 Controller.</param><returns>Controller가 소유한 진행.</returns>
    private static GameProgress uiProgress(GameUIController ui) => (GameProgress)typeof(GameUIController)
        .GetField("gameProgress", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
#endif

    /// <summary>테스트 선행 단계도 제품 대사·퇴장 API를 통해 완료한다.</summary>
    /// <param name="progress">시작된 진행.</param>
    private void completeInspectors(GameProgress progress)
    {
        for (int guard=0; guard<100 && session.InspectorEvents.HasPending; guard++)
        {
            var snapshot=session.InspectorEvents.Current;
            if(snapshot.Phase==InspectorEventPhase.Dialogue) Assert.That(progress.CurrentDayProgress.AdvanceInspector(snapshot));
            else Assert.That(progress.CurrentDayProgress.CompleteInspectorExit(snapshot));
        }
        Assert.That(session.InspectorEvents.HasPending,Is.False);
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
        Assert.That(day.AggregationResult.Value.Expenses, Is.EqualTo(200 + ((day.Day - 1) * 100)));
    }

    /// <summary>공개 catalog로 실제 가격 공급을 연결한 방문을 만든다.</summary>
    /// <returns>현재일 방문.</returns>
    private CustomerVisit generate() => new CustomerGenerator(new System.Random(1)).Generate(tables.Customers.Appearances.Rows.Keys.ToArray(), tables.Customers.Dispositions.Rows.Values.ToArray(), tables.Customers.Products.Rows, session.ElapsedDays, () => session.EnsureDailyPrices().Prices, isFacilityActive: session.IsFacilityActive);

    /// <summary>가격 민감 성향만 정확한 현재가를 사용하고 나머지는 기존 최소 제안을 유지한다.</summary>
    /// <param name="visit">현재 방문.</param><param name="items">최종 판매 목록.</param>
    /// <returns>현재 성향이 수락하는 제안 총액.</returns>
    private long acceptedOffer(CustomerVisit visit, SaleItem[] items) =>
        visit.DispositionType == CustomerDispositionType.PriceSensitive
            ? items.Sum(x => (long)x.Quantity * session.DailyPrices.Prices[x.ProductId])
            : 1;

    /// <summary>성인 평범 성향과 실제 도덕성 계산기를 갖는 방문을 생성한다.</summary>
    /// <param name="seedOffset">반복 생성의 시작 seed.</param>
    /// <returns>성인 평범 방문.</returns>
    private CustomerVisit generateAdultNormalWithMorality(int seedOffset)
    {
        var dispositions = tables.Customers.Dispositions.Rows.Values
            .Where(x => x.DispositionType == CustomerDispositionType.Normal).ToArray();
        var morality = new MoralityCalculator(tables.GetDB<MoralityDataTable>(DataTableType.Morality).Rows.Values.ToList().AsReadOnly());
        for (int seed = seedOffset; seed < seedOffset + 100; seed++)
        {
            CustomerVisit visit = new CustomerGenerator(new System.Random(seed)).Generate(
                tables.Customers.Appearances.Rows.Keys.ToArray(), dispositions, tables.Customers.Products.Rows,
                session.ElapsedDays, () => session.EnsureDailyPrices().Prices,
                isFacilityActive: session.IsFacilityActive, moralityCalculator: morality);
            if ((visit.Attributes & CustomerAttributes.Adult) != 0) return visit;
        }
        throw new InvalidOperationException("성인 테스트 방문을 생성하지 못했습니다.");
    }

    /// <summary>비동기 로더가 종료되지 않거나 실패하면 실패로 보고한다.</summary>
    /// <param name="task">로더 작업.</param><returns>완료 대기.</returns>
    private static IEnumerator wait(Task task)
    {
        float end = Time.realtimeSinceStartup + 20;
        while (!task.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(task.IsCompleted, "Actual CSV loader timed out."); task.GetAwaiter().GetResult();
    }
}
