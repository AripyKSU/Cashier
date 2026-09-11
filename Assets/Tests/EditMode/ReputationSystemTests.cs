using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 명성 시스템 회귀 테스트 사양
// 1. ReputationBalanceData.csv는 PK 11001~11005의 다섯 행으로 -100~100을 빈틈·중복 없이 포함한다.
// 2. 각 행의 일반/Wealthy/Hasty/특수 가중치 합은 1000이며 현재 특수 가중치는 0이다.
// 3. 잘못된 헤더·PK·명성 구간·가중치·회복 배율·정산 구간·일일 변화량은 공개되지 않는다.
// 4. 거래는 성향별 정가 범위를 기준으로 할인/정가/적당한 폭리로 분류하고 결제 거절은 큰 폭리로 분류한다.
// 5. 실제 거래가 5건 미만이면 부족한 수만큼 50점 가상 정가 거래를 추가하고 최종 점수를 20~80으로 제한한다.
// 6. 따라서 실제 거래 4건 이하가 모두 할인 또는 큰 폭리여도 최고 할인/최고 폭리 정산 구간에 도달하지 않는다.
// 7. 실제 거래가 5건이면 소표본 보정 없이 0~100의 극단 정산 구간에 도달할 수 있다.
// 8. Hasty 거래는 세 사람분의 가중치를 가지며, Hasty의 정가 거래는 할인 거래와 같은 100점으로 계산한다.
// 9. 양수 변화량에만 하루 시작 명성 구간의 회복 배율을 내림 적용하고, 최종 일일 변화량은 -15~+10으로 제한한다.
// 10. 거래 로그는 타입·속성·판정·가격 비율·등급을 보존하고, 정산 로그는 실제 비율·가상 거래·점수·변화량을 보존한다.
// 11. 명성·날짜·성향·가격 snapshot이 없거나 로그 거래 수와 정산 결과가 다르면 명시적으로 실패한다.

/// <summary>명성 CSV, 거래 분류, 일일 계산과 디버그 로그의 공개 계약을 검증합니다.</summary>
public sealed class ReputationSystemTests
{
    private const string BalanceCsvPath = "Assets/Datas/ReputationBalanceData.csv";

    private ReputationBalanceDataTable balanceTable;
    private CustomerDispositionDataTable dispositionTable;

    /// <summary>각 테스트에 실제 CSV를 통과한 독립 테이블을 제공합니다.</summary>
    [SetUp]
    public void SetUp()
    {
        this.balanceTable = new ReputationBalanceDataTable();
        this.balanceTable.LoadData(File.ReadAllText(BalanceCsvPath));
        this.dispositionTable = loadCustomerCatalog().Dispositions;
    }

    /// <summary>실제 명성 CSV의 행, 경계, 가중치와 정산 구간을 전수 확인합니다.</summary>
    [Test]
    public void BalanceCsvCoversEveryReputationAndSettlementScore()
    {
        Assert.That(this.balanceTable.GetDataCount(), Is.EqualTo(5));
        Assert.That(this.balanceTable.Rows.Keys.OrderBy(x => x), Is.EqualTo(new uint[] { 11001, 11002, 11003, 11004, 11005 }));

        for (int reputation = -100; reputation <= 100; reputation++)
        {
            Assert.That(this.balanceTable.TryGetByReputation(reputation, out ReputationBalanceData row), Is.True);
            Assert.That(reputation, Is.InRange(row.MinReputation, row.MaxReputation));
        }

        for (int score = 0; score <= 100; score++)
        {
            Assert.That(this.balanceTable.TryGetBySettlementScore(score, out ReputationBalanceData row), Is.True);
            Assert.That(score, Is.InRange(row.SettlementMinScore, row.SettlementMaxScore));
        }

        Assert.That(this.balanceTable.Rows.Values.All(row =>
            row.NormalWeight + row.PriceSensitiveWeight + row.WealthyWeight + row.HastyWeight + row.PoorWeight == 1000), Is.True);
        Assert.That(this.balanceTable.Rows.Values.All(row => row.PoorWeight == 100), Is.True);
    }

    /// <summary>명성 CSV의 각 불변 조건 위반을 거부하고 이전 공개 데이터를 보존하는지 확인합니다.</summary>
    /// <param name="caseName">변형할 불변 조건입니다.</param>
    [TestCase("header")]
    [TestCase("pk")]
    [TestCase("gap")]
    [TestCase("overlap")]
    [TestCase("weight")]
    [TestCase("negative-weight")]
    [TestCase("recovery")]
    [TestCase("settlement-gap")]
    [TestCase("delta")]
    [TestCase("row-count")]
    public void InvalidBalanceCsvIsRejectedAtomically(string caseName)
    {
        string validCsv = File.ReadAllText(BalanceCsvPath);
        string invalidCsv = caseName switch
        {
            "header" => validCsv.Replace("normal_weight", "missing_normal_weight"),
            "pk" => validCsv.Replace("11001,-100", "10001,-100"),
            "gap" => validCsv.Replace("11001,-100,-61", "11001,-100,-62"),
            "overlap" => validCsv.Replace("11001,-100,-61", "11001,-100,-60"),
            "weight" => validCsv.Replace("500,50,20,330,100", "499,50,20,330,100"),
            "negative-weight" => validCsv.Replace("500,50,20,330,100", "1001,-1,0,0,0"),
            "recovery" => validCsv.Replace("300,0,2000", "300,0,999"),
            "settlement-gap" => validCsv.Replace("2000,0,19", "2000,0,18"),
            "delta" => validCsv.Replace("0,19,-15", "0,19,-16"),
            "row-count" => string.Join("\n", validCsv.Split('\n').Take(5)),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, null)
        };

        LogAssert.Expect(LogType.Error, new Regex(@"^ReputationBalanceData\.csv"));
        Assert.Catch(() => this.balanceTable.LoadData(invalidCsv));
        Assert.That(this.balanceTable.GetDataCount(), Is.EqualTo(5));
        Assert.That(this.balanceTable.TryGetByReputation(0, out ReputationBalanceData preserved), Is.True);
        Assert.That(preserved.Idx, Is.EqualTo(11003));
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>가격 경계와 결제 거절이 네 명성 거래 등급으로 변환되는지 확인합니다.</summary>
    /// <param name="offeredTotal">제시 총액입니다.</param>
    /// <param name="tolerance">손님의 결제 허용 배율입니다.</param>
    /// <param name="expectedGrade">예상 명성 거래 등급입니다.</param>
    [TestCase(99, 1200, ReputationTransactionGrade.Discount)]
    [TestCase(100, 1200, ReputationTransactionGrade.Regular)]
    [TestCase(101, 1200, ReputationTransactionGrade.ModerateMarkup)]
    [TestCase(121, 1200, ReputationTransactionGrade.ExtremeMarkup)]
    public void TransactionClassificationUsesPriceAndRefusalBoundaries(long offeredTotal, int tolerance,
        ReputationTransactionGrade expectedGrade)
    {
        CustomerDispositionData disposition = makeDisposition(CustomerDispositionType.Normal, tolerance);
        TransactionResult transaction = makeTransaction(disposition, offeredTotal);

        Assert.That(ReputationTransactionClassifier.Classify(transaction, disposition), Is.EqualTo(expectedGrade));
    }

    /// <summary>가격 민감 손님의 하한 미달 거절은 판매자 폭리로 기록하지 않습니다.</summary>
    [Test]
    public void BelowMinimumRefusalIsNeutralForReputation()
    {
        CustomerDispositionData disposition = makeDisposition(CustomerDispositionType.PriceSensitive, 1000);
        disposition.MinimumPriceTolerance = 1000;
        TransactionResult transaction = makeTransaction(disposition, 99);

        Assert.That(ReputationTransactionClassifier.Classify(transaction, disposition),
            Is.EqualTo(ReputationTransactionGrade.Regular));
    }

    /// <summary>거래가 없거나 4건 이하이면 가상 정가와 20~80 제한이 극단 정산을 막는지 확인합니다.</summary>
    [Test]
    public void SmallSampleAddsVirtualRegularTransactionsAndAvoidsExtremeBands()
    {
        DailyReputationCalculator calculator = new DailyReputationCalculator(this.balanceTable, this.dispositionTable);
        DailyReputationCalculationResult empty = calculator.Calculate(0, Array.Empty<TransactionResult>());
        Assert.That((empty.ActualTransactionCount, empty.TotalWeight, empty.RawSettlementScore, empty.SettlementScore,
            empty.BaseDelta, empty.FinalDelta, empty.WasSmallSampleAdjusted),
            Is.EqualTo((0, 5, 50, 50, 0, 0, true)));

        TransactionResult discount = makeTransaction(makeDisposition(CustomerDispositionType.Normal, 1200), 99);
        TransactionResult extreme = makeTransaction(makeDisposition(CustomerDispositionType.Normal, 1200), 121);
        DailyReputationCalculationResult fourDiscounts = calculator.Calculate(0, repeat(discount, 4));
        DailyReputationCalculationResult fourExtremes = calculator.Calculate(0, repeat(extreme, 4));

        Assert.That((fourDiscounts.WeightedScoreSum, fourDiscounts.TotalWeight, fourDiscounts.RawSettlementScore,
            fourDiscounts.SettlementScore, fourDiscounts.BaseDelta, fourDiscounts.FinalDelta),
            Is.EqualTo((450, 5, 90, 80, 7, 7)));
        Assert.That((fourExtremes.WeightedScoreSum, fourExtremes.TotalWeight, fourExtremes.RawSettlementScore,
            fourExtremes.SettlementScore, fourExtremes.BaseDelta, fourExtremes.FinalDelta),
            Is.EqualTo((50, 5, 10, 20, -7, -7)));
    }

    /// <summary>실제 거래 5건부터 최고 할인·최고 폭리 정산 구간을 사용할 수 있는지 확인합니다.</summary>
    [Test]
    public void FiveTransactionsCanReachExtremeSettlementBands()
    {
        DailyReputationCalculator calculator = new DailyReputationCalculator(this.balanceTable, this.dispositionTable);
        TransactionResult discount = makeTransaction(makeDisposition(CustomerDispositionType.Normal, 1200), 99);
        TransactionResult extreme = makeTransaction(makeDisposition(CustomerDispositionType.Normal, 1200), 121);

        DailyReputationCalculationResult discounts = calculator.Calculate(0, repeat(discount, 5));
        DailyReputationCalculationResult extremes = calculator.Calculate(0, repeat(extreme, 5));

        Assert.That((discounts.SettlementScore, discounts.BaseDelta, discounts.FinalDelta, discounts.WasSmallSampleAdjusted),
            Is.EqualTo((100, 10, 10, false)));
        Assert.That((extremes.SettlementScore, extremes.BaseDelta, extremes.FinalDelta, extremes.WasSmallSampleAdjusted),
            Is.EqualTo((0, -15, -15, false)));
    }

    /// <summary>Hasty 한 건이 세 사람분이며 정가도 100점으로 계산되는지 확인합니다.</summary>
    [Test]
    public void HastyRegularCountsAsThreeDiscountEquivalentTransactions()
    {
        DailyReputationCalculator calculator = new DailyReputationCalculator(this.balanceTable, this.dispositionTable);
        TransactionResult hastyRegular = makeTransaction(makeDisposition(CustomerDispositionType.Hasty, 1200), 100);

        DailyReputationCalculationResult result = calculator.Calculate(0, new[] { hastyRegular });

        Assert.That((result.ActualTransactionCount, result.WeightedScoreSum, result.TotalWeight,
            result.RawSettlementScore, result.SettlementScore, result.BaseDelta),
            Is.EqualTo((1, 500, 7, 71, 71, 7)));
    }

    /// <summary>양수 회복 배율의 내림·상한과 음수 변화량의 비적용을 확인합니다.</summary>
    [Test]
    public void RecoveryMultiplierAppliesOnlyToPositiveDeltaAndClampsDailyRange()
    {
        DailyReputationCalculator calculator = new DailyReputationCalculator(this.balanceTable, this.dispositionTable);
        TransactionResult hastyRegular = makeTransaction(makeDisposition(CustomerDispositionType.Hasty, 1200), 100);
        TransactionResult extreme = makeTransaction(makeDisposition(CustomerDispositionType.Normal, 1200), 121);

        DailyReputationCalculationResult recovery = calculator.Calculate(-40, new[] { hastyRegular });
        DailyReputationCalculationResult loss = calculator.Calculate(-80, repeat(extreme, 5));

        Assert.That((recovery.BaseDelta, recovery.FinalDelta), Is.EqualTo((7, 10)));
        Assert.That((loss.BaseDelta, loss.FinalDelta), Is.EqualTo((-15, -15)));
    }

    /// <summary>구조화 로그가 거래와 정산의 입력·비율·중간값을 보존하고 Clear로 초기화되는지 확인합니다.</summary>
    [Test]
    public void ReputationLogRecordsTransactionsSettlementAndClear()
    {
        ReputationLogService logService = new ReputationLogService(this.dispositionTable);
        DailyReputationCalculator calculator = new DailyReputationCalculator(this.balanceTable, this.dispositionTable);
        TransactionResult discount = makeTransaction(makeDisposition(CustomerDispositionType.Normal, 1200), 99);
        TransactionResult regular = makeTransaction(makeDisposition(CustomerDispositionType.Normal, 1200), 100);
        int transactionEvents = 0;
        int settlementEvents = 0;
        logService.TransactionLogged += _ => transactionEvents++;
        logService.SettlementLogged += _ => settlementEvents++;

        ReputationTransactionLogEntry first = logService.RecordTransaction(1, discount);
        ReputationTransactionLogEntry second = logService.RecordTransaction(1, regular);
        DailyReputationCalculationResult calculation = calculator.Calculate(0, new[] { discount, regular });
        Assert.That(this.balanceTable.TryGetByReputation(0, out ReputationBalanceData reputationData), Is.True);
        ReputationSettlementLogEntry settlement = logService.RecordDailySettlement(1, 0, calculation, reputationData);

        Assert.That((first.Sequence, first.Grade, first.PriceRatioPercent), Is.EqualTo((1L, ReputationTransactionGrade.Discount, (decimal?)99m)));
        Assert.That((second.Sequence, second.Grade, second.PriceRatioPercent), Is.EqualTo((2L, ReputationTransactionGrade.Regular, (decimal?)100m)));
        Assert.That((settlement.ActualTransactionCount, settlement.VirtualRegularTransactionCount,
            settlement.WeightedScoreSum, settlement.TotalWeight), Is.EqualTo((2, 3, 300, 5)));
        Assert.That(settlement.GradeSummaries.Single(x => x.Grade == ReputationTransactionGrade.Discount).RatioPercent, Is.EqualTo(50m));
        Assert.That(settlement.GradeSummaries.Single(x => x.Grade == ReputationTransactionGrade.Regular).RatioPercent, Is.EqualTo(50m));
        Assert.That((transactionEvents, settlementEvents), Is.EqualTo((2, 1)));

        logService.Clear();
        Assert.That(logService.TransactionEntries, Is.Empty);
        Assert.That(logService.SettlementEntries, Is.Empty);
        Assert.That(logService.RecordTransaction(2, regular).Sequence, Is.EqualTo(1));
    }

    /// <summary>잘못된 명성, 날짜, legacy 거래, 성향 불일치와 로그 수 불일치를 거부합니다.</summary>
    [Test]
    public void InvalidReputationInputsAreRejected()
    {
        DailyReputationCalculator calculator = new DailyReputationCalculator(this.balanceTable, this.dispositionTable);
        CustomerDispositionData normal = makeDisposition(CustomerDispositionType.Normal, 1200);
        TransactionResult transaction = makeTransaction(normal, 100);
        ReputationLogService logService = new ReputationLogService(this.dispositionTable);

        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(-101, Array.Empty<TransactionResult>()));
        Assert.Throws<ArgumentNullException>(() => calculator.Calculate(0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReputationTransactionClassifier.Classify(new TransactionResult(0, 0), normal));
        Assert.Throws<ArgumentException>(() => ReputationTransactionClassifier.Classify(
            transaction, makeDisposition(CustomerDispositionType.Hasty, 1200)));
        Assert.Throws<ArgumentOutOfRangeException>(() => logService.RecordTransaction(0, transaction));

        DailyReputationCalculationResult result = calculator.Calculate(0, new[] { transaction });
        Assert.That(this.balanceTable.TryGetByReputation(0, out ReputationBalanceData reputationData), Is.True);
        Assert.Throws<InvalidDataException>(() => logService.RecordDailySettlement(1, 0, result, reputationData));
    }

    /// <summary>유효한 실제 손님 CSV를 공개한 카탈로그를 만듭니다.</summary>
    /// <returns>FK 검증과 공개가 완료된 손님 카탈로그입니다.</returns>
    private static CustomerCatalog loadCustomerCatalog()
    {
        const string customerRoot = "Assets/Datas/Customer/";
        CustomerCatalog catalog = new CustomerCatalog(new CustomerAppearanceDataTable(),
            new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
        TextDataTable texts = new TextDataTable();
        catalog.Appearances.LoadData(File.ReadAllText(customerRoot + "CustomerAppearanceData.csv"));
        catalog.Dispositions.LoadData(File.ReadAllText(customerRoot + "CustomerDispositionData.csv"));
        catalog.Categories.LoadData(File.ReadAllText(customerRoot + "ProductCategoryData.csv"));
        catalog.Products.LoadData(File.ReadAllText(customerRoot + "ProductData.csv"));
        texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));
        FacilityDataTable facilities = new FacilityDataTable();
        facilities.LoadData(File.ReadAllText("Assets/Datas/FacilityData.csv"));
        catalog.ValidateAndCommit(texts, loadResources(), facilities: facilities);
        return catalog;
    }

    /// <summary>명성 분류 검사용 최소 성향 데이터를 만듭니다.</summary>
    /// <param name="type">손님 성향 타입입니다.</param>
    /// <param name="priceTolerance">결제 허용 배율입니다.</param>
    /// <returns>정가 범위가 정확히 100%인 성향 데이터입니다.</returns>
    private static CustomerDispositionData makeDisposition(CustomerDispositionType type, int priceTolerance)
    {
        return new CustomerDispositionData
        {
            Idx = 6001,
            DispositionType = type,
            PreferredProductIdxs = Array.Empty<uint>(),
            PreferredProductTypes = Array.Empty<ProductType>(),
            PreferredSelectionChance = 0,
            MinProductKinds = 1,
            MaxProductKinds = 1,
            MinQuantity = 1,
            MaxQuantity = 1,
            PriceTolerance = priceTolerance,
            RegularPriceMinRate = 1000,
            RegularPriceMaxRate = 1000,
            EntryTextIdxs = new uint[] { 1 },
            RegularSaleTextIdxs = new uint[] { 2 },
            DiscountSaleTextIdxs = new uint[] { 3 },
            ExploitativeSaleTextIdxs = new uint[] { 4 },
            RejectTextIdxs = new uint[] { 5 }
        };
    }

    /// <summary>정가 100인 상품 하나를 제출해 불변 거래 결과를 만듭니다.</summary>
    /// <param name="disposition">거래에 사용할 성향입니다.</param>
    /// <param name="offeredTotal">제시 총액입니다.</param>
    /// <returns>판정이 완료된 거래 결과입니다.</returns>
    private static TransactionResult makeTransaction(CustomerDispositionData disposition, long offeredTotal)
    {
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData>
        {
            [1] = new ProductData
            {
                Idx = 1,
                ProductType = ProductType.Water,
                BasePrice = 100,
                CostPrice = 50,
                IsAvailable = true
            }
        };
        Dictionary<uint, uint> prices = new Dictionary<uint, uint> { [1] = 100 };
        CustomerVisit visit = new CustomerGenerator(new System.Random(17)).Generate(new uint[] { 1 },
            new[] { disposition }, products, getCurrentPrices: () => prices);
        visit.BeginOffer();
        visit.SubmitOffer(offeredTotal, new[] { new SaleItem(1, 1) });
        return visit.Result.Value;
    }

    /// <summary>같은 불변 거래 결과를 지정 횟수만큼 배열에 배치합니다.</summary>
    /// <param name="transaction">반복할 거래 결과입니다.</param>
    /// <param name="count">반복 횟수입니다.</param>
    /// <returns>계산기에 전달할 거래 배열입니다.</returns>
    private static TransactionResult[] repeat(TransactionResult transaction, int count)
    {
        return Enumerable.Repeat(transaction, count).ToArray();
    }
    /// <summary>실제 Resource CSV를 FK 검증에 사용한다.</summary>
    /// <returns>파싱한 Resource 테이블.</returns>
    private static ResourceDataTable loadResources()
    {
        var resources = new ResourceDataTable();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("^\\[ResourceDataTable\\]"));
        resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        return resources;
    }
}
