/// <summary>
/// 하루 동안 완료된 판매 거래의 경제 변화 집계 결과입니다.
/// </summary>
public readonly struct DailyAggregationResult
{
    /// <summary>
    /// 하루 동안 누적된 판매 수입입니다.
    /// </summary>
    public long SaleIncome { get; }

    /// <summary>
    /// 하루 동안 누적된 명성 변화량입니다.
    /// </summary>
    public int ReputationDelta { get; }

    /// <summary>
    /// 일일 집계 결과를 생성합니다.
    /// </summary>
    /// <param name="saleIncome">하루 동안 누적된 판매 수입입니다.</param>
    /// <param name="reputationDelta">하루 동안 누적된 명성 변화량입니다.</param>
    internal DailyAggregationResult(long saleIncome, int reputationDelta)
    {
        this.SaleIncome = saleIncome;
        this.ReputationDelta = reputationDelta;
    }
}
