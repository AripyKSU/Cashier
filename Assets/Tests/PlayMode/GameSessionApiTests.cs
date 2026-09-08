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

    /// <summary>공개 catalog로 실제 가격 공급을 연결한 방문을 만든다.</summary>
    /// <returns>현재일 방문.</returns>
    private CustomerVisit generate() => new CustomerGenerator(new System.Random(1)).Generate(tables.Customers.Appearances.Rows.Keys.ToArray(), tables.Customers.Dispositions.Rows.Values.ToArray(), tables.Customers.Products.Rows, session.ElapsedDays, () => session.EnsureDailyPrices().Prices);

    /// <summary>비동기 로더가 종료되지 않거나 실패하면 실패로 보고한다.</summary>
    /// <param name="task">로더 작업.</param><returns>완료 대기.</returns>
    private static IEnumerator wait(Task task)
    {
        float end = Time.realtimeSinceStartup + 20;
        while (!task.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(task.IsCompleted, "Actual CSV loader timed out."); task.GetAwaiter().GetResult();
    }
}
