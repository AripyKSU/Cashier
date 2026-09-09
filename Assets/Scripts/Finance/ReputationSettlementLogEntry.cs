using System.Collections.Generic;

/// <summary>
/// 하루 명성 정산의 비율·점수·변화량 계산 과정을 기록한 디버그 로그입니다.
/// </summary>
public readonly struct ReputationSettlementLogEntry
{
    /// <summary>세션 내 정산 로그 순서입니다.</summary>
    public long Sequence { get; }

    /// <summary>정산 대상 게임 날짜입니다.</summary>
    public int Day { get; }

    /// <summary>하루 시작 시점의 명성입니다.</summary>
    public int DayStartReputation { get; }

    /// <summary>실제 거래 수입니다.</summary>
    public int ActualTransactionCount { get; }

    /// <summary>소표본 보정을 위해 추가한 가상 정가 거래 수입니다.</summary>
    public int VirtualRegularTransactionCount { get; }

    /// <summary>등급별 실제 거래 비율 snapshot입니다.</summary>
    public IReadOnlyList<ReputationGradeSummary> GradeSummaries { get; }

    /// <summary>가중 점수의 분자입니다.</summary>
    public int WeightedScoreSum { get; }

    /// <summary>가중 점수의 분모입니다.</summary>
    public int TotalWeight { get; }

    /// <summary>내림 계산한 소표본 보정 전 점수입니다.</summary>
    public int RawSettlementScore { get; }

    /// <summary>정산 구간 조회에 사용한 점수입니다.</summary>
    public int SettlementScore { get; }

    /// <summary>회복 배율 적용 전 CSV 변화량입니다.</summary>
    public int BaseDelta { get; }

    /// <summary>명성 구간의 회복 배율입니다. 1000이 1배입니다.</summary>
    public int RecoveryRate { get; }

    /// <summary>다음 날에 적용할 최종 변화량입니다.</summary>
    public int FinalDelta { get; }

    /// <summary>소표본 보정 여부입니다.</summary>
    public bool WasSmallSampleAdjusted { get; }

    /// <summary>명성 정산 로그를 생성합니다.</summary>
    /// <param name="sequence">세션 내 로그 순서입니다.</param>
    /// <param name="day">정산 대상 게임 날짜입니다.</param>
    /// <param name="dayStartReputation">하루 시작 명성입니다.</param>
    /// <param name="virtualRegularTransactionCount">추가한 가상 정가 거래 수입니다.</param>
    /// <param name="gradeSummaries">등급별 실제 거래 비율입니다.</param>
    /// <param name="result">명성 계산 결과입니다.</param>
    /// <param name="recoveryRate">명성 구간 회복 배율입니다.</param>
    internal ReputationSettlementLogEntry(long sequence, int day, int dayStartReputation,
        int virtualRegularTransactionCount, IReadOnlyList<ReputationGradeSummary> gradeSummaries,
        DailyReputationCalculationResult result, int recoveryRate)
    {
        this.Sequence = sequence;
        this.Day = day;
        this.DayStartReputation = dayStartReputation;
        this.ActualTransactionCount = result.ActualTransactionCount;
        this.VirtualRegularTransactionCount = virtualRegularTransactionCount;
        this.GradeSummaries = new List<ReputationGradeSummary>(gradeSummaries).AsReadOnly();
        this.WeightedScoreSum = result.WeightedScoreSum;
        this.TotalWeight = result.TotalWeight;
        this.RawSettlementScore = result.RawSettlementScore;
        this.SettlementScore = result.SettlementScore;
        this.BaseDelta = result.BaseDelta;
        this.RecoveryRate = recoveryRate;
        this.FinalDelta = result.FinalDelta;
        this.WasSmallSampleAdjusted = result.WasSmallSampleAdjusted;
    }
}
