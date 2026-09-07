using System;

/// <summary>
/// 거래 판정 시스템이 완성되기 전까지 사용하는 임시 완료 거래 결과입니다.
/// </summary>
public readonly struct TransactionResult
{
    /// <summary>
    /// 완료된 거래의 판매 수입입니다.
    /// </summary>
    public long SaleIncome { get; }

    /// <summary>
    /// 완료된 거래로 발생한 명성 변화량입니다.
    /// </summary>
    public int ReputationDelta { get; }

    /// <summary>
    /// 임시 완료 거래 결과를 생성합니다.
    /// </summary>
    /// <param name="saleIncome">완료된 거래의 판매 수입입니다.</param>
    /// <param name="reputationDelta">완료된 거래로 발생한 명성 변화량입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">판매 수입이 음수인 경우 발생합니다.</exception>
    public TransactionResult(long saleIncome, int reputationDelta)
    {
        if (saleIncome < 0)
        {
            // 판매 거래 결과에서 지출이 전달되는 잘못된 입력을 차단합니다.
            throw new ArgumentOutOfRangeException(nameof(saleIncome), saleIncome, "판매 수입은 음수일 수 없습니다.");
        }

        this.SaleIncome = saleIncome;
        this.ReputationDelta = reputationDelta;
    }
}
