using System;

/// <summary>
/// UI와 기타 표현 계층이 경제 런타임을 직접 변경하지 않고 필요한 최신 경제 상태를 읽도록 제공합니다.
/// 경제 화면에서는 FinanceService나 MaintenanceService에 직접 접근하지 말고 이 조회 서비스를 사용합니다.
/// </summary>
public sealed class EconomyQueryService
{
    // 조회할 설정과 경제 서비스들을 소유한 현재 세션의 경제 런타임입니다.
    private readonly EconomyRuntime economy;

    /// <summary>
    /// 현재 플레이어가 보유한 금액입니다.
    /// </summary>
    public long CurrentBalance => this.economy.FinanceService.CurrentBalance;

    /// <summary>
    /// 현재 영업일에 누적된 판매 수입입니다.
    /// </summary>
    public long DailySaleIncome => this.economy.DailyAggregationService.DailySaleIncome;

    /// <summary>
    /// 현재 일일 집계가 거래 결과를 받고 있는지 나타냅니다.
    /// </summary>
    public bool IsDayOpen => this.economy.DailyAggregationService.IsDayOpen;

    /// <summary>
    /// 상납금 납부 사이의 게임 내 일수입니다.
    /// </summary>
    public int MaintenanceCycleDays => this.economy.Settings.MaintenanceCycleDays;

    /// <summary>
    /// 지정한 경제 런타임을 읽는 조회 서비스를 생성합니다.
    /// </summary>
    /// <param name="economy">조회할 현재 세션의 경제 런타임입니다.</param>
    /// <exception cref="ArgumentNullException">경제 런타임이 null인 경우 발생합니다.</exception>
    public EconomyQueryService(EconomyRuntime economy)
    {
        this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
    }

    /// <summary>
    /// 다음 납부 예정 상납금을 조회합니다.
    /// </summary>
    /// <param name="amount">다음 상납금이 있으면 해당 금액을 반환합니다.</param>
    /// <returns>다음 상납금 설정이 있으면 true, 모든 설정 회차를 납부했다면 false입니다.</returns>
    public bool TryGetNextMaintenanceAmount(out long amount)
    {
        int nextPaymentRound = this.economy.MaintenanceService.LastPaidRound + 1;
        if (nextPaymentRound > this.economy.Settings.MaintenanceAmounts.Count)
        {
            // 모든 설정 회차를 납부한 경우 UI가 금액 표시를 숨길 수 있도록 조회 실패로 반환합니다.
            amount = default;
            return false;
        }

        amount = this.economy.MaintenanceService.GetRequiredAmount(nextPaymentRound);
        return true;
    }
}
