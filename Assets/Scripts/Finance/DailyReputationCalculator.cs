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
    /// <param name="abandonedDispositions">줄에서 기다리다 떠난 손님들의 성향입니다. 각각 0점 거래로 집계합니다. null이면 없음으로 봅니다.</param>
    /// <returns>소표본 보정과 회복 배율을 포함한 일일 명성 계산 결과입니다. 거래 수에는 이탈 손님을 넣지 않습니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">하루 시작 명성이 범위를 벗어난 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">거래 성향 또는 명성 밸런스 데이터가 누락된 경우 발생합니다.</exception>
    public DailyReputationCalculationResult Calculate(int dayStartReputation,
        IReadOnlyList<TransactionResult> transactions,
        IReadOnlyList<CustomerDispositionType> abandonedDispositions = null)
    {
        if (dayStartReputation < MinimumReputation || dayStartReputation > MaximumReputation)
            throw new ArgumentOutOfRangeException(nameof(dayStartReputation), dayStartReputation, "명성은 -100~100 범위여야 합니다.");
        if (transactions == null) throw new ArgumentNullException(nameof(transactions));
        if (!this.reputationBalanceTable.TryGetByReputation(dayStartReputation, out ReputationBalanceData reputationData))
            throw new InvalidDataException($"명성 {dayStartReputation}에 대응하는 밸런스 데이터가 없습니다.");

        IReadOnlyDictionary<CustomerDispositionType, CustomerDispositionData> dispositions =
            ReputationDispositionRules.BuildByType(this.dispositionTable.Rows.Values);
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

        // 너무 늦게 처리해 줄에서 떠난 손님은 거래 실패와 같은 0점으로 소문을 깎는다.
        int abandonedCount = abandonedDispositions?.Count ?? 0;
        for (int index = 0; index < abandonedCount; index++)
        {
            int weight = abandonedDispositions[index] == CustomerDispositionType.Hasty ? 3 : 1;
            totalWeight = checked(totalWeight + weight);
        }

        // 결과의 거래 수는 일일 집계와 대조하므로 실제 거래만 센다. 소표본 보정에는 이탈 손님도 표본으로 넣는다.
        int actualTransactionCount = transactions.Count;
        int sampleCount = checked(actualTransactionCount + abandonedCount);
        bool wasSmallSampleAdjusted = sampleCount < MinimumSampleCount;
        for (int virtualTransactionIndex = sampleCount;
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

    private static int getScore(TransactionResult transaction, ReputationTransactionGrade grade)
    {
        // 전부 빼서 판매가 없던 거래는 중립 50점이다. 급한 손님 가산으로 소문을 쌓는 데 쓰이지 않게 한다.
        if (!transaction.ReferenceTotal.HasValue || transaction.ReferenceTotal.Value <= 0)
            return RegularScore;
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
