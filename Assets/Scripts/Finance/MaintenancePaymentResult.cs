/// <summary>
/// 상납금 납부 시도의 결과입니다.
/// </summary>
public readonly struct MaintenancePaymentResult
{
    /// <summary>
    /// 납부를 시도한 상납금 회차입니다.
    /// </summary>
    public int PaymentRound { get; }

    /// <summary>
    /// 해당 회차에 필요한 상납금입니다.
    /// </summary>
    public long RequiredAmount { get; }

    /// <summary>
    /// 상납금 납부 성공 여부입니다.
    /// </summary>
    public bool IsPaid { get; }

    /// <summary>
    /// 납부 시도 전 보유금입니다.
    /// </summary>
    public long PreviousBalance { get; }

    /// <summary>
    /// 납부 시도 후 보유금입니다. 실패한 경우 이전 보유금과 같습니다.
    /// </summary>
    public long CurrentBalance { get; }

    /// <summary>
    /// 상납금 납부 결과를 생성합니다.
    /// </summary>
    /// <param name="paymentRound">납부를 시도한 상납금 회차입니다.</param>
    /// <param name="requiredAmount">해당 회차에 필요한 상납금입니다.</param>
    /// <param name="isPaid">상납금 납부 성공 여부입니다.</param>
    /// <param name="previousBalance">납부 시도 전 보유금입니다.</param>
    /// <param name="currentBalance">납부 시도 후 보유금입니다.</param>
    internal MaintenancePaymentResult(
        int paymentRound,
        long requiredAmount,
        bool isPaid,
        long previousBalance,
        long currentBalance)
    {
        this.PaymentRound = paymentRound;
        this.RequiredAmount = requiredAmount;
        this.IsPaid = isPaid;
        this.PreviousBalance = previousBalance;
        this.CurrentBalance = currentBalance;
    }
}
