using System;
using System.Collections.Generic;

/// <summary>
/// 현재 게임 세션에서 사용하는 경제 설정과 경제 서비스 인스턴스를 소유합니다.
/// </summary>
public sealed class EconomyRuntime : IDisposable
{
    // 내부 이벤트 구독을 이미 해제했는지 나타냅니다.
    private bool isDisposed;

    /// <summary>
    /// 현재 세션에 적용된 경제 밸런스 설정입니다.
    /// </summary>
    public EconomySettings Settings { get; }

    /// <summary>
    /// 현재 보유금을 관리하는 재정 서비스입니다.
    /// </summary>
    public FinanceService FinanceService { get; }

    /// <summary>
    /// 현재 영업일의 거래 결과를 집계하는 서비스입니다.
    /// </summary>
    public DailyAggregationService DailyAggregationService { get; }

    /// <summary>
    /// 회차별 상납금 조회와 납부를 처리하는 서비스입니다.
    /// </summary>
    public MaintenanceService MaintenanceService { get; }

    /// <summary>현재 실행에서 누적되는 통합 정산 미납액과 유예 기한입니다.</summary>
    public SettlementDebtState SettlementDebt { get; }

    /// <summary>
    /// 세션 중 발생한 잔고 변경 기록을 수집하는 로그 서비스입니다.
    /// </summary>
    public EconomyLogService LogService { get; }

    /// <summary>
    /// UI와 표현 계층에 최신 경제 상태를 읽기 전용으로 제공하는 조회 서비스입니다.
    /// </summary>
    public EconomyQueryService QueryService { get; }

    /// <summary>
    /// 로드된 경제 데이터로 현재 세션의 경제 런타임을 구성합니다.
    /// </summary>
    /// <param name="balanceData">CSV에서 읽고 검증한 경제 기본 설정입니다.</param>
    /// <param name="maintenanceAmounts">1회차부터 순서대로 정렬된 상납금 목록입니다.</param>
    /// <exception cref="ArgumentNullException">경제 기본 설정이나 상납금 목록이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">상납금 목록이 비어 있거나 올바르지 않은 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">경제 기본 설정값이 허용 범위를 벗어난 경우 발생합니다.</exception>
    public EconomyRuntime(
        EconomyBalanceData balanceData,
        IReadOnlyList<long> maintenanceAmounts)
    {
        // 세션 설정을 먼저 확정한 뒤 모든 경제 서비스가 같은 설정과 재정 상태를 사용하게 합니다.
        this.Settings = new EconomySettings(balanceData, maintenanceAmounts);
        this.FinanceService = new FinanceService(this.Settings.InitialBalance);
        this.DailyAggregationService = new DailyAggregationService(this.FinanceService);
        this.SettlementDebt = new SettlementDebtState();
        this.MaintenanceService = new MaintenanceService(
            this.FinanceService,
            this.Settings.MaintenanceAmounts);
        this.LogService = new EconomyLogService(this.FinanceService);
        this.QueryService = new EconomyQueryService(this);
    }

    /// <summary>
    /// 세션 내부 서비스의 이벤트 구독을 해제합니다.
    /// 여러 번 호출해도 한 번만 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        // Runtime이 소유한 로그 구독을 정리하고 기록 자체는 조회 가능 상태로 둡니다.
        this.LogService.Dispose();
        this.isDisposed = true;
    }
}
