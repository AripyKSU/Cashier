using System;
using System.Collections.Generic;

/// <summary>
/// 회차별 상납금을 조회하고 재정 시스템을 통해 납부를 처리합니다.
/// 날짜 진행, 게임 오버와 UI 표시는 담당하지 않습니다.
/// </summary>
public sealed class MaintenanceService
{
    // 상납금을 실제 보유금에서 차감하는 재정 시스템입니다.
    private readonly FinanceService financeService;

    // 밸런스 데이터에서 전달받아 검증한 회차별 상납금 스냅샷입니다.
    private readonly long[] maintenanceAmounts;

    // 같은 회차의 상납금이 중복 차감되지 않도록 마지막 성공 회차를 보관합니다.
    private int lastPaidRound;

    /// <summary>
    /// 마지막으로 납부에 성공한 상납금 회차입니다.
    /// </summary>
    public int LastPaidRound => this.lastPaidRound;

    /// <summary>
    /// 상납금이 성공적으로 납부된 뒤 발생합니다.
    /// </summary>
    public event Action<MaintenancePaymentResult> MaintenancePaid;

    /// <summary>
    /// 재정 시스템과 회차별 상납금으로 상납금 시스템을 생성합니다.
    /// </summary>
    /// <param name="financeService">현재 보유금을 관리하는 재정 시스템입니다.</param>
    /// <param name="maintenanceAmounts">1회차부터 순서대로 정렬된 상납금 목록입니다.</param>
    /// <param name="lastPaidRound">저장 데이터에서 복원할 마지막 납부 성공 회차입니다.</param>
    /// <exception cref="ArgumentNullException">재정 시스템이나 상납금 목록이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">상납금 목록이 비어 있거나 0 이하의 금액을 포함한 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">마지막 납부 회차가 상납금 목록 범위를 벗어난 경우 발생합니다.</exception>
    public MaintenanceService(
        FinanceService financeService,
        IReadOnlyList<long> maintenanceAmounts,
        int lastPaidRound = 0)
    {
        this.financeService = financeService ?? throw new ArgumentNullException(nameof(financeService));

        if (maintenanceAmounts == null)
        {
            throw new ArgumentNullException(nameof(maintenanceAmounts));
        }

        if (maintenanceAmounts.Count == 0)
        {
            throw new ArgumentException("상납금 목록은 한 회차 이상이어야 합니다.", nameof(maintenanceAmounts));
        }

        if (lastPaidRound < 0 || lastPaidRound > maintenanceAmounts.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastPaidRound),
                lastPaidRound,
                "마지막 납부 회차가 상납금 목록 범위를 벗어났습니다.");
        }

        // 외부 목록 변경이 런타임 상납금에 영향을 주지 않도록 검증하며 복사합니다.
        this.maintenanceAmounts = new long[maintenanceAmounts.Count];
        for (int index = 0; index < maintenanceAmounts.Count; index++)
        {
            long amount = maintenanceAmounts[index];
            if (amount <= 0)
            {
                throw new ArgumentException(
                    $"{index + 1}회차 상납금은 0보다 커야 합니다.",
                    nameof(maintenanceAmounts));
            }

            this.maintenanceAmounts[index] = amount;
        }

        this.lastPaidRound = lastPaidRound;
    }

    /// <summary>
    /// 지정한 회차의 상납금을 조회합니다.
    /// </summary>
    /// <param name="paymentRound">조회할 상납금 회차입니다.</param>
    /// <returns>해당 회차에 납부해야 하는 금액입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">회차가 상납금 목록 범위를 벗어난 경우 발생합니다.</exception>
    public long GetRequiredAmount(int paymentRound)
    {
        this.validateRoundRange(paymentRound);
        return this.maintenanceAmounts[paymentRound - 1];
    }

    /// <summary>
    /// 지정한 회차의 상납금 납부를 시도합니다.
    /// </summary>
    /// <param name="paymentRound">납부할 상납금 회차입니다.</param>
    /// <param name="result">상납금 납부 시도의 상세 결과입니다.</param>
    /// <returns>납부에 성공하면 true, 현재 보유금이 부족하면 false입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">회차가 상납금 목록 범위를 벗어난 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">이미 납부했거나 다음 순서가 아닌 회차를 요청한 경우 발생합니다.</exception>
    public bool TryPay(int paymentRound, out MaintenancePaymentResult result)
    {
        this.validateRoundRange(paymentRound);
        this.validatePaymentOrder(paymentRound);

        long requiredAmount = this.maintenanceAmounts[paymentRound - 1];
        long previousBalance = this.financeService.CurrentBalance;

        if (!this.financeService.TrySpend(
                requiredAmount,
                FinanceChangeReason.Maintenance,
                out FinanceChangeResult financeResult))
        {
            // 잔액 부족은 상태를 바꾸지 않고 게임 진행 시스템이 판단할 결과로 반환합니다.
            result = new MaintenancePaymentResult(
                paymentRound,
                requiredAmount,
                false,
                previousBalance,
                previousBalance);

            return false;
        }

        // 재정 차감이 성공한 경우에만 회차를 완료하고 알림 이벤트를 발행합니다.
        this.lastPaidRound = paymentRound;
        result = new MaintenancePaymentResult(
            paymentRound,
            requiredAmount,
            true,
            financeResult.PreviousBalance,
            financeResult.CurrentBalance);

        this.MaintenancePaid?.Invoke(result);
        return true;
    }

    /// <summary>
    /// 상납금 회차가 설정된 목록 범위 안인지 확인합니다.
    /// </summary>
    /// <param name="paymentRound">검사할 상납금 회차입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">회차가 상납금 목록 범위를 벗어난 경우 발생합니다.</exception>
    private void validateRoundRange(int paymentRound)
    {
        if (paymentRound <= 0 || paymentRound > this.maintenanceAmounts.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentRound),
                paymentRound,
                $"상납금 회차는 1부터 {this.maintenanceAmounts.Length}까지 사용할 수 있습니다.");
        }
    }

    /// <summary>
    /// 상납금 회차가 마지막 성공 회차의 다음 순서인지 확인합니다.
    /// </summary>
    /// <param name="paymentRound">검사할 상납금 회차입니다.</param>
    /// <exception cref="InvalidOperationException">이미 납부했거나 다음 순서가 아닌 회차를 요청한 경우 발생합니다.</exception>
    private void validatePaymentOrder(int paymentRound)
    {
        int nextPaymentRound = this.lastPaidRound + 1;
        if (paymentRound != nextPaymentRound)
        {
            // 회차 건너뛰기와 이미 납부한 회차의 중복 차감을 함께 방지합니다.
            throw new InvalidOperationException(
                $"상납금 납부 회차가 올바르지 않습니다. 요청: {paymentRound}, 다음 회차: {nextPaymentRound}");
        }
    }
}
