using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 하루 거래 결과를 명성 행동 점수와 다음날 적용 변화량으로 변환합니다.
/// </summary>
public sealed class DailyReputationCalculator
{
    private const int MinimumSampleCount = 5;
    private const int RegularScore = 50;
    private const int SmallSampleLowerScore = 20;
    private const int SmallSampleUpperScore = 80;
    private const int RecoveryRateBase = 1000;
    private const int MinimumReputation = -100;
    private const int MaximumReputation = 100;
    private const int MinimumDailyDelta = -15;
    private const int MaximumDailyDelta = 10;

    private readonly ReputationBalanceDataTable reputationBalanceTable;
    private readonly CustomerDispositionDataTable dispositionTable;

    /// <summary>
    /// 명성 계산에 사용할 밸런스와 손님 성향 테이블을 지정합니다.
    /// </summary>
    /// <param name="reputationBalanceTable">검증된 명성 밸런스 테이블입니다.</param>
    /// <param name="dispositionTable">검증된 손님 성향 테이블입니다.</param>
    /// <exception cref="ArgumentNullException">필수 테이블이 null인 경우 발생합니다.</exception>
    public DailyReputationCalculator(ReputationBalanceDataTable reputationBalanceTable,
        CustomerDispositionDataTable dispositionTable)
    {
        this.reputationBalanceTable = reputationBalanceTable ?? throw new ArgumentNullException(nameof(reputationBalanceTable));
        this.dispositionTable = dispositionTable ?? throw new ArgumentNullException(nameof(dispositionTable));
    }

    /// <summary>
    /// 하루 거래 목록을 명성 정산 결과로 계산합니다.
    /// </summary>
    /// <param name="dayStartReputation">하루 시작 시점의 명성입니다.</param>
    /// <param name="transactions">하루 동안 접수된 성공·거절 거래 목록입니다.</param>
    /// <returns>소표본 보정과 회복 배율을 포함한 일일 명성 계산 결과입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">하루 시작 명성이 범위를 벗어난 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">거래 성향 또는 명성 밸런스 데이터가 누락된 경우 발생합니다.</exception>
    public DailyReputationCalculationResult Calculate(int dayStartReputation,
        IReadOnlyList<TransactionResult> transactions)
    {
        if (dayStartReputation < MinimumReputation || dayStartReputation > MaximumReputation)
            throw new ArgumentOutOfRangeException(nameof(dayStartReputation), dayStartReputation, "명성은 -100~100 범위여야 합니다.");
        if (transactions == null) throw new ArgumentNullException(nameof(transactions));
        if (!this.reputationBalanceTable.TryGetByReputation(dayStartReputation, out ReputationBalanceData reputationData))
            throw new InvalidDataException($"명성 {dayStartReputation}에 대응하는 밸런스 데이터가 없습니다.");

        Dictionary<CustomerDispositionType, CustomerDispositionData> dispositions = this.buildDispositionMap();
        int weightedScoreSum = 0;
        int totalWeight = 0;
        foreach (TransactionResult transaction in transactions)
        {
            if (!dispositions.TryGetValue(transaction.DispositionType, out CustomerDispositionData dispositionData))
                throw new InvalidDataException($"거래 성향 데이터가 없습니다: {transaction.DispositionType}");

            ReputationTransactionGrade grade = ReputationTransactionClassifier.Classify(transaction, dispositionData);
            int score = getScore(transaction, grade);
            int weight = transaction.DispositionType == CustomerDispositionType.Hasty ? 3 : 1;
            weightedScoreSum = checked(weightedScoreSum + score * weight);
            totalWeight = checked(totalWeight + weight);
        }

        int actualTransactionCount = transactions.Count;
        bool wasSmallSampleAdjusted = actualTransactionCount < MinimumSampleCount;
        for (int virtualTransactionIndex = actualTransactionCount;
            virtualTransactionIndex < MinimumSampleCount;
            virtualTransactionIndex++)
        {
            weightedScoreSum = checked(weightedScoreSum + RegularScore);
            totalWeight++;
        }

        int rawSettlementScore = weightedScoreSum / totalWeight;
        int settlementScore = wasSmallSampleAdjusted
            ? Math.Max(SmallSampleLowerScore, Math.Min(SmallSampleUpperScore, rawSettlementScore))
            : rawSettlementScore;
        if (!this.reputationBalanceTable.TryGetBySettlementScore(settlementScore, out ReputationBalanceData settlementData))
            throw new InvalidDataException($"행동 점수 {settlementScore}에 대응하는 정산 데이터가 없습니다.");

        int finalDelta = applyRecoveryAndClamp(settlementData.SettlementDelta, reputationData.RecoveryRate);
        return new DailyReputationCalculationResult(actualTransactionCount, weightedScoreSum, totalWeight,
            rawSettlementScore, settlementScore, settlementData.SettlementDelta, finalDelta, wasSmallSampleAdjusted);
    }

    private Dictionary<CustomerDispositionType, CustomerDispositionData> buildDispositionMap()
    {
        Dictionary<CustomerDispositionType, CustomerDispositionData> result =
            new Dictionary<CustomerDispositionType, CustomerDispositionData>();
        foreach (CustomerDispositionData data in this.dispositionTable.Rows.Values)
        {
            if (data == null) throw new InvalidDataException("손님 성향 데이터에 null 행이 있습니다.");
            if (!result.TryAdd(data.DispositionType, data))
                throw new InvalidDataException($"손님 성향 타입이 중복되었습니다: {data.DispositionType}");
        }
        return result;
    }

    private static int getScore(TransactionResult transaction, ReputationTransactionGrade grade)
    {
        if (transaction.DispositionType == CustomerDispositionType.Hasty && grade == ReputationTransactionGrade.Regular)
            return 100;

        switch (grade)
        {
            case ReputationTransactionGrade.Discount:
                return 100;
            case ReputationTransactionGrade.Regular:
                return 50;
            case ReputationTransactionGrade.ModerateMarkup:
                return 25;
            case ReputationTransactionGrade.ExtremeMarkup:
                return 0;
            default:
                throw new InvalidDataException($"정의되지 않은 명성 거래 등급입니다: {grade}");
        }
    }

    private static int applyRecoveryAndClamp(int baseDelta, int recoveryRate)
    {
        int adjustedDelta = baseDelta > 0
            ? (int)Math.Floor((decimal)baseDelta * recoveryRate / RecoveryRateBase)
            : baseDelta;
        return Math.Max(MinimumDailyDelta, Math.Min(MaximumDailyDelta, adjustedDelta));
    }
}
