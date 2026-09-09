/// <summary>
/// 하루 거래 목록에서 계산한 명성 정산 중간·최종 결과입니다.
/// </summary>
public readonly struct DailyReputationCalculationResult
{
    /// <summary>가상 거래를 넣기 전 실제 거래 수입니다.</summary>
    public int ActualTransactionCount { get; }

    /// <summary>가중 거래 점수의 분자입니다.</summary>
    public int WeightedScoreSum { get; }

    /// <summary>가중 거래 수의 분모입니다.</summary>
    public int TotalWeight { get; }

    /// <summary>소표본 보정 전 내림 행동 점수입니다.</summary>
    public int RawSettlementScore { get; }

    /// <summary>소표본 보정 후 정산에 사용한 행동 점수입니다.</summary>
    public int SettlementScore { get; }

    /// <summary>CSV에서 조회한 회복 배율 적용 전 변화량입니다.</summary>
    public int BaseDelta { get; }

    /// <summary>회복 배율과 최종 상·하한을 적용한 변화량입니다.</summary>
    public int FinalDelta { get; }

    /// <summary>실제 거래가 5건 미만이어서 가상 정가 거래를 추가했는지 여부입니다.</summary>
    public bool WasSmallSampleAdjusted { get; }

    /// <summary>명성 정산 계산 결과를 생성합니다.</summary>
    internal DailyReputationCalculationResult(int actualTransactionCount, int weightedScoreSum, int totalWeight,
        int rawSettlementScore, int settlementScore, int baseDelta, int finalDelta, bool wasSmallSampleAdjusted)
    {
        this.ActualTransactionCount = actualTransactionCount;
        this.WeightedScoreSum = weightedScoreSum;
        this.TotalWeight = totalWeight;
        this.RawSettlementScore = rawSettlementScore;
        this.SettlementScore = settlementScore;
        this.BaseDelta = baseDelta;
        this.FinalDelta = finalDelta;
        this.WasSmallSampleAdjusted = wasSmallSampleAdjusted;
    }
}
