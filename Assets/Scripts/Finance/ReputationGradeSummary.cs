/// <summary>
/// 일일 명성 정산에 포함된 실제 거래 등급의 개수와 비율입니다.
/// </summary>
public readonly struct ReputationGradeSummary
{
    /// <summary>집계한 거래 등급입니다.</summary>
    public ReputationTransactionGrade Grade { get; }

    /// <summary>해당 등급의 실제 거래 수입니다.</summary>
    public int Count { get; }

    /// <summary>실제 거래 수 기준 해당 등급의 비율입니다.</summary>
    public decimal RatioPercent { get; }

    /// <summary>거래 등급 집계 결과를 생성합니다.</summary>
    /// <param name="grade">집계한 등급입니다.</param>
    /// <param name="count">해당 등급의 거래 수입니다.</param>
    /// <param name="ratioPercent">전체 실제 거래 중 해당 등급의 비율입니다.</param>
    internal ReputationGradeSummary(ReputationTransactionGrade grade, int count, decimal ratioPercent)
    {
        this.Grade = grade;
        this.Count = count;
        this.RatioPercent = ratioPercent;
    }
}
