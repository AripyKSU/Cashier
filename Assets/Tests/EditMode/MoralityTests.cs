using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>실제 성향·도덕성 CSV의 거래 수락 경계와 성향별 소수 점수를 검증한다.</summary>
public sealed class MoralityTests
{
    /// <summary>실제 36개 행과 다섯 성향의 가격 경계 및 성인·아이·노인 점수를 검증한다.</summary>
    [Test]
    public void ActualCsvCalculatesAllBandsForFiveDispositions()
    {
        MoralityDataTable table = loadActual();
        MoralityCalculator calculator = calculatorFor(table);
        var adult = CustomerAttributes.Male | CustomerAttributes.Adult | CustomerAttributes.Normal;
        var child = CustomerAttributes.Female | CustomerAttributes.Child | CustomerAttributes.Normal;
        var elderly = CustomerAttributes.Male | CustomerAttributes.Elderly | CustomerAttributes.Normal;
        Assert.That(table.Rows.Count, Is.EqualTo(36));
        Assert.That(table.Rows.Values.GroupBy(x => x.CustomerDispositionType).ToDictionary(x => x.Key, x => x.Count()),
            Is.EquivalentTo(new Dictionary<CustomerDispositionType, int>
            {
                [CustomerDispositionType.Normal] = 8,
                [CustomerDispositionType.Hasty] = 9,
                [CustomerDispositionType.PriceSensitive] = 3,
                [CustomerDispositionType.Wealthy] = 8,
                [CustomerDispositionType.Poor] = 8
            }));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 800, 1000)?.Delta, Is.EqualTo(1m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 999, 1000)?.Delta, Is.EqualTo(0.5m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, child, true, 801, 1000)?.Delta, Is.EqualTo(1m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1000, 1000)?.DataIdx, Is.EqualTo(14003));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1001, 1000)?.Delta, Is.EqualTo(-0.25m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1050, 1000)?.Delta, Is.EqualTo(-0.25m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1051, 1000)?.Delta, Is.EqualTo(-0.5m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1101, 1000)?.Delta, Is.EqualTo(-0.75m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, child, true, 1200, 1000)?.Delta, Is.EqualTo(-1.5m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1201, 1000)?.Delta, Is.EqualTo(-1m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, child, true, 1300, 1000)?.Delta, Is.EqualTo(-2m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, false, 1301, 1000)?.Delta, Is.EqualTo(-2m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, child, true, 800, 1000)?.Delta, Is.EqualTo(9m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, adult, true, 1300, 1000)?.Delta, Is.EqualTo(-6m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, adult, true, 1301, 1000)?.Delta, Is.EqualTo(-8m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, child, true, 1400, 1000)?.Delta, Is.EqualTo(-12m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, adult, true, 1401, 1000)?.Delta, Is.EqualTo(-10m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, adult, true, 1499, 1000)?.Delta, Is.EqualTo(-10m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, child, true, 1500, 1000)?.Delta, Is.EqualTo(-15m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, child, false, 1501, 1000)?.Delta, Is.EqualTo(-15m));
        Assert.That(calculator.Calculate(CustomerDispositionType.PriceSensitive, adult, true, 1000, 1000)?.Delta, Is.Zero);
        Assert.That(calculator.Calculate(CustomerDispositionType.PriceSensitive, child, false, 999, 1000)?.Delta, Is.EqualTo(-4m));
        Assert.That(calculator.Calculate(CustomerDispositionType.PriceSensitive, adult, false, 1001, 1000)?.Delta, Is.EqualTo(-2m));
        var addedBoundaries = new (CustomerDispositionType type, bool accepted, long rate, uint id, decimal adultDelta, decimal childDelta)[]
        {
            (CustomerDispositionType.Poor, true, 719, 14021, 6m, 9m), (CustomerDispositionType.Poor, true, 720, 14021, 6m, 9m), (CustomerDispositionType.Poor, true, 721, 14022, 3m, 4.5m),
            (CustomerDispositionType.Poor, true, 899, 14022, 3m, 4.5m), (CustomerDispositionType.Poor, true, 900, 14023, 0m, 0m), (CustomerDispositionType.Poor, true, 901, 14024, -1.5m, -2.25m),
            (CustomerDispositionType.Poor, true, 944, 14024, -1.5m, -2.25m), (CustomerDispositionType.Poor, true, 945, 14024, -1.5m, -2.25m), (CustomerDispositionType.Poor, true, 946, 14025, -3m, -4.5m),
            (CustomerDispositionType.Poor, true, 989, 14025, -3m, -4.5m), (CustomerDispositionType.Poor, true, 990, 14025, -3m, -4.5m), (CustomerDispositionType.Poor, true, 991, 14026, -4.5m, -6.75m),
            (CustomerDispositionType.Poor, true, 1079, 14026, -4.5m, -6.75m), (CustomerDispositionType.Poor, true, 1080, 14026, -4.5m, -6.75m), (CustomerDispositionType.Poor, true, 1081, 14027, -6m, -9m),
            (CustomerDispositionType.Poor, true, 1149, 14027, -6m, -9m), (CustomerDispositionType.Poor, true, 1150, 14027, -6m, -9m), (CustomerDispositionType.Poor, false, 1151, 14028, -6m, -9m),
            (CustomerDispositionType.Wealthy, true, 799, 14029, .5m, 1m), (CustomerDispositionType.Wealthy, true, 800, 14029, .5m, 1m), (CustomerDispositionType.Wealthy, true, 801, 14030, .25m, .5m),
            (CustomerDispositionType.Wealthy, true, 999, 14030, .25m, .5m), (CustomerDispositionType.Wealthy, true, 1000, 14031, 0m, 0m), (CustomerDispositionType.Wealthy, true, 1001, 14032, -.1m, -.2m),
            (CustomerDispositionType.Wealthy, true, 1199, 14032, -.1m, -.2m), (CustomerDispositionType.Wealthy, true, 1200, 14032, -.1m, -.2m), (CustomerDispositionType.Wealthy, true, 1201, 14033, -.2m, -.4m),
            (CustomerDispositionType.Wealthy, true, 1399, 14033, -.2m, -.4m), (CustomerDispositionType.Wealthy, true, 1400, 14033, -.2m, -.4m), (CustomerDispositionType.Wealthy, true, 1401, 14034, -.3m, -.6m),
            (CustomerDispositionType.Wealthy, true, 1599, 14034, -.3m, -.6m), (CustomerDispositionType.Wealthy, true, 1600, 14034, -.3m, -.6m), (CustomerDispositionType.Wealthy, true, 1601, 14035, -.5m, -1m),
            (CustomerDispositionType.Wealthy, true, 1799, 14035, -.5m, -1m), (CustomerDispositionType.Wealthy, true, 1800, 14035, -.5m, -1m), (CustomerDispositionType.Wealthy, false, 1801, 14036, -.5m, -1m)
        };
        foreach (var boundary in addedBoundaries)
        {
            MoralityEvaluation adultResult = calculator.Calculate(boundary.type, adult, boundary.accepted, boundary.rate, 1000).Value;
            MoralityEvaluation childResult = calculator.Calculate(boundary.type, child, boundary.accepted, boundary.rate, 1000).Value;
            MoralityEvaluation elderlyResult = calculator.Calculate(boundary.type, elderly, boundary.accepted, boundary.rate, 1000).Value;
            Assert.That((adultResult.DataIdx, adultResult.Delta), Is.EqualTo((boundary.id, boundary.adultDelta)), $"{boundary.type} rate={boundary.rate} adult");
            Assert.That((childResult.DataIdx, childResult.Delta), Is.EqualTo((boundary.id, boundary.childDelta)), $"{boundary.type} rate={boundary.rate} child");
            Assert.That((elderlyResult.DataIdx, elderlyResult.Delta), Is.EqualTo((boundary.id, boundary.childDelta)), $"{boundary.type} rate={boundary.rate} elderly");
        }
    }

    /// <summary>실제 CSV의 같은 타입 전행으로 방문을 생성해 확정 상한과 거절·도덕성 결과를 검증한다.</summary>
    /// <param name="type">검증할 성향 타입.</param>
    /// <param name="expectedTolerance">정가 1000 기준 확정 구매 상한.</param>
    [TestCase(CustomerDispositionType.Normal, 1300)]
    [TestCase(CustomerDispositionType.Hasty, 1500)]
    [TestCase(CustomerDispositionType.PriceSensitive, 1000)]
    [TestCase(CustomerDispositionType.Wealthy, 1800)]
    [TestCase(CustomerDispositionType.Poor, 1150)]
    public void ActualDispositionCsvUsesConfirmedOfferBoundaries(CustomerDispositionType type, int expectedTolerance)
    {
        CustomerDispositionData[] rows = dispositions().Values.Where(row => row.DispositionType == type).ToArray();
        Assert.That(rows, Is.Not.Empty);
        MoralityCalculator calculator = calculatorFor(loadActual());
        // 기준 총액을 1000으로 고정해 비율 경계와 1원 차이를 실제 제출 API에서 검사한다.
        var products = new Dictionary<uint, ProductData>
        {
            [1001] = new ProductData { Idx = 1001, ProductType = ProductType.Water,
                BasePrice = 1000, CostPrice = 500, IsAvailable = true }
        };
        var prices = new Dictionary<uint, uint> { [1001] = 1000 };
        foreach (CustomerDispositionData row in rows)
        {
            Assert.That(row.PriceTolerance, Is.EqualTo(expectedTolerance), $"PK={row.Idx}");
            Assert.That(row.MinimumPriceTolerance, Is.EqualTo(type == CustomerDispositionType.PriceSensitive ? 1000 : 0));
            Assert.That(row.RegularPriceMinRate, Is.EqualTo(1000));
            Assert.That(row.RegularPriceMaxRate, Is.EqualTo(1000));
            foreach (long offeredTotal in new long[] { 999, 1000, expectedTolerance, expectedTolerance + 1 }.Distinct())
            {
                CustomerComposition composition = new CustomerCompositionSelector(new Random(17)).SelectCompositionUniform(
                    new uint[] { 5001 }, new[] { row }, products, prices);
                CustomerVisit visit = new CustomerGenerator().Generate(composition, products, () => prices,
                    moralityCalculator: calculator);
                visit.BeginOffer();
                bool expectedAccepted = type == CustomerDispositionType.PriceSensitive
                    ? offeredTotal == 1000 : offeredTotal <= expectedTolerance;
                Assert.That(visit.SubmitOffer(offeredTotal, new[] { new SaleItem(1001, 1) }),
                    Is.EqualTo(expectedAccepted), $"PK={row.Idx}, offer={offeredTotal}");
                CustomerTradeOutcome expectedOutcome = !expectedAccepted ? CustomerTradeOutcome.PaymentRefused
                    : offeredTotal < 1000 ? CustomerTradeOutcome.DiscountSale
                    : offeredTotal == 1000 ? CustomerTradeOutcome.RegularSale : CustomerTradeOutcome.ExploitativeSale;
                TransactionResult result = visit.Result.Value;
                Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
                Assert.That(result.ReferenceTotal, Is.EqualTo(1000));
                Assert.That(visit.AllowedTotal, Is.EqualTo(expectedTolerance));
                Assert.That(result.SaleIncome, Is.EqualTo(expectedAccepted ? offeredTotal : 0));
                Assert.That(result.MoralityDataIdx.HasValue, Is.True);
                Assert.That(result.MoralityDelta.HasValue, Is.True);
            }
        }
    }

    /// <summary>가난·부자 성향의 대표 수락 경계가 실제 제출 결과와 도덕성 행에 함께 반영되는지 검사한다.</summary>
    [Test]
    public void AddedDispositionBandsFlowThroughCustomerVisit()
    {
        var products = new Dictionary<uint, ProductData>
        {
            [1001] = new ProductData { Idx = 1001, ProductType = ProductType.Water, BasePrice = 1000, CostPrice = 500, IsAvailable = true }
        };
        var prices = new Dictionary<uint, uint> { [1001] = 1000 };
        MoralityCalculator calculator = calculatorFor(loadActual());
        IReadOnlyDictionary<uint, CustomerDispositionData> dispositionRows = dispositions();
        var cases = new (CustomerDispositionType type, long offer, bool accepted, CustomerTradeOutcome outcome, uint id, decimal delta)[]
        {
            (CustomerDispositionType.Poor, 900, true, CustomerTradeOutcome.DiscountSale, 14023, 0m),
            (CustomerDispositionType.Poor, 1000, true, CustomerTradeOutcome.RegularSale, 14026, -4.5m),
            (CustomerDispositionType.Poor, 1150, true, CustomerTradeOutcome.ExploitativeSale, 14027, -6m),
            (CustomerDispositionType.Poor, 1151, false, CustomerTradeOutcome.PaymentRefused, 14028, -6m),
            (CustomerDispositionType.Wealthy, 1800, true, CustomerTradeOutcome.ExploitativeSale, 14035, -.5m),
            (CustomerDispositionType.Wealthy, 1801, false, CustomerTradeOutcome.PaymentRefused, 14036, -.5m)
        };
        foreach (var item in cases)
        {
            CustomerDispositionData row = dispositionRows.Values.First(x => x.DispositionType == item.type);
            CustomerComposition selected = new CustomerCompositionSelector(new Random(17)).SelectCompositionUniform(
                new uint[] { 5001 }, new[] { row }, products, prices);
            var composition = new CustomerComposition(5001, row.Idx, item.type,
                CustomerAttributes.Male | CustomerAttributes.Adult | CustomerAttributes.Normal,
                selected.Items, row.EntryTextIdxs[0], row.RegularSaleTextIdxs[0],
                row.DiscountSaleTextIdxs[0], row.ExploitativeSaleTextIdxs[0], row.RejectTextIdxs[0],
                row.PriceTolerance, row.MinimumPriceTolerance, row.RegularPriceMinRate, row.RegularPriceMaxRate,
                new uint[] { 1001 });
            CustomerVisit visit = new CustomerGenerator().Generate(composition, products, () => prices, moralityCalculator: calculator);
            visit.BeginOffer();
            Assert.That(visit.SubmitOffer(item.offer, new[] { new SaleItem(1001, 1) }), Is.EqualTo(item.accepted));
            Assert.That((visit.Result.Value.Outcome, visit.Result.Value.MoralityDataIdx, visit.Result.Value.MoralityDelta),
                Is.EqualTo((item.outcome, (uint?)item.id, (decimal?)item.delta)), $"{item.type} offer={item.offer}");
        }
    }

    /// <summary>유한 거절 상한과 구간 중복을 로드 후 공개 전에 거부한다.</summary>
    [Test]
    public void ValidationRejectsTailGapAndOverlap()
    {
        string csv = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Datas/MoralityData.csv"));
        MoralityDataTable tailGap = new MoralityDataTable();
        tailGap.LoadData(csv.Replace("14008,1,0,1300,0,0,0", "14008,1,0,1300,2000,0,1"));
        Assert.Throws<InvalidDataException>(() => tailGap.Validate(dispositions()));
        MoralityDataTable overlap = new MoralityDataTable();
        overlap.LoadData(csv.Replace("14005,1,1,1050,1100,0,1", "14005,1,1,1040,1100,0,1"));
        Assert.Throws<InvalidDataException>(() => overlap.Validate(dispositions()));
    }

    /// <summary>실제 두 CSV의 수락 구간을 함께 검증하고 도덕성 행을 공개한다.</summary>
    /// <returns>성향과 연계 검증한 도덕성 테이블.</returns>
    private static MoralityDataTable loadActual()
    {
        var table = new MoralityDataTable();
        table.LoadData(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Datas/MoralityData.csv")));
        table.Validate(dispositions());
        typeof(MoralityDataTable).GetMethod("Commit", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(table, null);
        return table;
    }

    private static MoralityCalculator calculatorFor(MoralityDataTable table) =>
        new MoralityCalculator(new List<MoralityData>(table.Rows.Values).AsReadOnly());

    /// <summary>실제 성향 CSV를 전용 로더로 읽어 공개 전 도덕성 교차 검증에 전달한다.</summary>
    /// <returns>PK·행 검증을 마친 성향 목록. 전체 FK 공개 검증은 CustomerCsvTests에서 수행한다.</returns>
    private static IReadOnlyDictionary<uint, CustomerDispositionData> dispositions()
    {
        var table = new CustomerDispositionDataTable();
        table.LoadData(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Datas/Customer/CustomerDispositionData.csv")));
        return (IReadOnlyDictionary<uint, CustomerDispositionData>)typeof(CustomerDispositionDataTable)
            .GetProperty("PendingRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(table);
    }
}
