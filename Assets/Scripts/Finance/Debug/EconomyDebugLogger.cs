using System;

/// <summary>
/// Finance 테스트 화면에서 경제 상태 변경을 사람이 읽을 수 있는 로그로 전달합니다.
/// </summary>
public sealed class EconomyDebugLogger : IDisposable
{
    private readonly FinanceService financeService;
    private readonly MaintenanceService maintenanceService;
    private bool isDisposed;

    /// <summary>
    /// 새 로그가 생성될 때 발생합니다.
    /// </summary>
    public event Action<string> LogAdded;

    /// <summary>
    /// 경제 서비스 이벤트를 구독하는 개발용 로그 수집기를 생성합니다.
    /// </summary>
    /// <param name="financeService">잔액 변경을 발행하는 재정 서비스입니다.</param>
    /// <param name="maintenanceService">상납금 납부 완료를 발행하는 서비스입니다.</param>
    /// <exception cref="ArgumentNullException">서비스 인자가 null인 경우 발생합니다.</exception>
    public EconomyDebugLogger(
        FinanceService financeService,
        MaintenanceService maintenanceService)
    {
        this.financeService = financeService ?? throw new ArgumentNullException(nameof(financeService));
        this.maintenanceService = maintenanceService ?? throw new ArgumentNullException(nameof(maintenanceService));

        this.financeService.BalanceChanged += this.handleBalanceChanged;
        this.maintenanceService.MaintenancePaid += this.handleMaintenancePaid;
    }

    /// <summary>
    /// 구독 중인 경제 이벤트를 해제합니다. 여러 번 호출해도 한 번만 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.financeService.BalanceChanged -= this.handleBalanceChanged;
        this.maintenanceService.MaintenancePaid -= this.handleMaintenancePaid;
        this.LogAdded = null;
        this.isDisposed = true;
    }

    /// <summary>
    /// 잔액 변경 이벤트를 판매 또는 상납금 금액 로그로 변환합니다.
    /// </summary>
    /// <param name="result">적용된 잔액 변경 결과입니다.</param>
    private void handleBalanceChanged(FinanceChangeResult result)
    {
        string changeName = result.Reason switch
        {
            FinanceChangeReason.Sale => "Sale",
            FinanceChangeReason.Maintenance => "Maintenance",
            _ => result.Reason.ToString()
        };

        this.addLog(
            $"[Economy] {changeName} | {result.PreviousBalance:N0} {this.formatDelta(result.BalanceDelta)} = {result.CurrentBalance:N0}");
    }

    /// <summary>
    /// 상납금 납부 완료 이벤트를 완료 사실 로그로 변환합니다.
    /// 금액 변경 로그는 BalanceChanged에서 이미 기록하므로 여기서는 완료 사실만 기록합니다.
    /// </summary>
    /// <param name="result">완료된 상납금 납부 결과입니다.</param>
    private void handleMaintenancePaid(MaintenancePaymentResult result)
    {
        this.addLog($"[Economy] Maintenance payment completed | amount={result.RequiredAmount:N0}");
    }

    /// <summary>
    /// 양수·음수 잔액 변경량을 로그용 표기로 변환합니다.
    /// </summary>
    /// <param name="delta">표시할 잔액 변경량입니다.</param>
    /// <returns>부호와 천 단위 구분을 포함한 변경량 문자열입니다.</returns>
    private string formatDelta(long delta)
    {
        if (delta > 0)
        {
            return $"+ {delta:N0}";
        }

        if (delta < 0)
        {
            return $"- {Math.Abs(delta):N0}";
        }

        return "0";
    }

    /// <summary>
    /// 로그 소비자에게 한 줄을 전달합니다.
    /// </summary>
    /// <param name="message">전달할 로그 문자열입니다.</param>
    private void addLog(string message)
    {
        if (!this.isDisposed)
        {
            this.LogAdded?.Invoke(message);
            UnityEngine.Debug.Log(message);
        }
    }
}
