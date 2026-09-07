using System;

/// <summary>
/// 하나의 재정 변경이 적용된 결과를 나타냅니다.
/// </summary>
public readonly struct FinanceChangeResult
{
    public long PreviousBalance { get; }

    public long BalanceDelta { get; }

    public long CurrentBalance { get; }

    public FinanceChangeReason Reason { get; }

    /// <summary>
    /// 재정 변경 결과를 생성합니다.
    /// </summary>
    /// <param name="previousBalance">변경 직전의 보유금입니다.</param>
    /// <param name="balanceDelta">변경량입니다. 수입은 양수, 지출은 음수여야 합니다.</param>
    /// <param name="currentBalance">변경 직후의 보유금입니다.</param>
    /// <param name="reason">변경 사유입니다.</param>
    internal FinanceChangeResult(
        long previousBalance,
        long balanceDelta,
        long currentBalance,
        FinanceChangeReason reason)
    {
        this.PreviousBalance = previousBalance;
        this.BalanceDelta = balanceDelta;
        this.CurrentBalance = currentBalance;
        this.Reason = reason;
    }
}
