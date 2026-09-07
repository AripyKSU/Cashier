using System;
using System.Collections.Generic;

/// <summary>
/// 현재 게임 세션에서 사용하는 경제 설정과 경제 서비스 인스턴스를 소유합니다.
/// </summary>
public sealed class EconomyRuntime
{
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
        this.MaintenanceService = new MaintenanceService(
            this.FinanceService,
            this.Settings.MaintenanceAmounts);
    }
}
