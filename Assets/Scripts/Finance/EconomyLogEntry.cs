/// <summary>
/// 현재 게임 세션에서 발생한 하나의 잔고 변경 기록을 나타냅니다.
/// </summary>
public readonly struct EconomyLogEntry
{
    /// <summary>
    /// 현재 세션에서 기록이 생성된 순서입니다.
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// 잔고 변경 전 금액입니다.
    /// </summary>
    public long PreviousBalance { get; }

    /// <summary>
    /// 잔고 변화량입니다. 수입은 양수이고 지출은 음수입니다.
    /// </summary>
    public long BalanceDelta { get; }

    /// <summary>
    /// 잔고 변경 후 금액입니다.
    /// </summary>
    public long CurrentBalance { get; }

    /// <summary>
    /// 잔고가 변경된 업무 사유입니다.
    /// </summary>
    public FinanceChangeReason Reason { get; }

    /// <summary>
    /// 잔고 변경 결과로부터 세션 로그를 생성합니다.
    /// </summary>
    /// <param name="sequence">현재 세션에서 부여할 순서 번호입니다.</param>
    /// <param name="result">재정 시스템에서 발행한 잔고 변경 결과입니다.</param>
    internal EconomyLogEntry(long sequence, FinanceChangeResult result)
    {
        this.Sequence = sequence;
        this.PreviousBalance = result.PreviousBalance;
        this.BalanceDelta = result.BalanceDelta;
        this.CurrentBalance = result.CurrentBalance;
        this.Reason = result.Reason;
    }
}
