using System;

/// <summary>
/// 현재 보유금의 유일한 원본을 관리하고 수입·지출을 원자적으로 반영합니다.
/// 거래 판정, 일일 집계, 상납금 계산과 게임 진행은 담당하지 않습니다.
/// </summary>
public sealed class FinanceService
{
    private long currentBalance;

    public long CurrentBalance => this.currentBalance;

    public event Action<FinanceChangeResult> BalanceChanged;

    /// <summary>
    /// 초기 보유금으로 재정 서비스를 생성합니다.
    /// </summary>
    /// <param name="initialBalance">게임 시작 또는 저장 데이터에서 복원할 보유금입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">초기 보유금이 음수인 경우 발생합니다.</exception>
    public FinanceService(long initialBalance)
    {
        if (initialBalance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialBalance), initialBalance, "초기 보유금은 음수일 수 없습니다.");
        }

        // 생성 시점에만 초기 잔액을 설정하고 이후 변경은 수입·지출 API로 제한합니다.
        this.currentBalance = initialBalance;
    }

    /// <summary>
    /// 지정한 금액을 지불할 수 있는지 확인합니다.
    /// </summary>
    /// <param name="amount">확인할 지출 금액입니다.</param>
    /// <returns>금액이 양수이고 현재 보유금 이하이면 true를 반환합니다.</returns>
    public bool CanAfford(long amount)
    {
        // 양수 지출이며 현재 잔액으로 전액 지불할 수 있는지 확인합니다.
        return amount > 0 && this.currentBalance >= amount;
    }

    /// <summary>
    /// 판매 수입과 같은 양의 재정 변화를 적용합니다.
    /// </summary>
    /// <param name="amount">추가할 수입 금액입니다.</param>
    /// <param name="reason">수입 발생 사유입니다.</param>
    /// <returns>적용된 변경 전·후 잔액과 변경량입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">금액이 0 이하인 경우 발생합니다.</exception>
    /// <exception cref="OverflowException">잔액이 <see cref="long.MaxValue"/>를 초과하는 경우 발생합니다.</exception>
    public FinanceChangeResult AddIncome(long amount, FinanceChangeReason reason)
    {
        // 수입 금액의 사전조건을 확인해 음수 또는 0인 변경을 차단합니다.
        this.validateAmount(amount);

        long previousBalance = this.currentBalance;
        // 잔액 상한을 넘는 수입은 적용하지 않고 예외로 알립니다.
        long nextBalance = checked(previousBalance + amount);
        FinanceChangeResult result = new FinanceChangeResult(previousBalance, amount, nextBalance, reason);

        // 결과를 만든 뒤 잔액을 교체하고 모든 변경 소비자에게 알립니다.
        this.currentBalance = nextBalance;
        this.BalanceChanged?.Invoke(result);
        return result;
    }

    /// <summary>
    /// 지정한 지출을 적용합니다.
    /// </summary>
    /// <param name="amount">지불할 지출 금액입니다.</param>
    /// <param name="reason">지출 발생 사유입니다.</param>
    /// <param name="result">성공한 경우 적용된 변경 전·후 잔액과 변경량입니다. 실패하면 기본값입니다.</param>
    /// <returns>지불에 성공하면 true, 보유금이 부족하면 false를 반환합니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">금액이 0 이하인 경우 발생합니다.</exception>
    public bool TrySpend(long amount, FinanceChangeReason reason, out FinanceChangeResult result)
    {
        // 지출 금액의 사전조건을 확인해 잘못된 호출을 조기에 중단합니다.
        this.validateAmount(amount);

        if (!this.CanAfford(amount))
        {
            // 잔액 부족은 정상적인 결제 실패이므로 상태와 이벤트를 변경하지 않습니다.
            result = default;
            return false;
        }

        long previousBalance = this.currentBalance;
        long delta = -amount;
        long nextBalance = previousBalance + delta;
        result = new FinanceChangeResult(previousBalance, delta, nextBalance, reason);

        // 지출 결과를 확정한 뒤 잔액을 차감하고 변경 이벤트를 발행합니다.
        this.currentBalance = nextBalance;
        this.BalanceChanged?.Invoke(result);
        return true;
    }

    /// <summary>
    /// 금액 인자의 공통 사전조건을 확인합니다.
    /// </summary>
    /// <param name="amount">검사할 금액입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">금액이 0 이하인 경우 발생합니다.</exception>
    private void validateAmount(long amount)
    {
        if (amount <= 0)
        {
            // 0 이하의 금액은 게임플레이 결과가 아닌 호출 코드 오류로 처리합니다.
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "재정 변경 금액은 0보다 커야 합니다.");
        }
    }
}
