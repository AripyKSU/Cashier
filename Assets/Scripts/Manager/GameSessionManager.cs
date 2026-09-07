using System;

/// <summary>
/// 현재 게임 세션의 런타임 시스템을 생성하고 Scene 전환 동안 수명을 유지합니다.
/// </summary>
public sealed class GameSessionManager : Singleton<GameSessionManager>
{
    // 현재 게임 세션에서 사용하는 경제 런타임입니다.
    private EconomyRuntime economy;

    /// <summary>
    /// 현재 게임 세션의 초기화가 완료됐는지 나타냅니다.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 현재 게임 세션에서 사용하는 경제 런타임입니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">게임 세션이 아직 초기화되지 않은 경우 발생합니다.</exception>
    public EconomyRuntime Economy => this.economy
        ?? throw new InvalidOperationException("게임 세션이 아직 초기화되지 않았습니다.");

    /// <summary>
    /// 로드된 데이터 테이블로 새 게임 세션의 경제 런타임을 초기화합니다.
    /// </summary>
    /// <param name="dataTableManager">CSV 로딩을 완료한 데이터 테이블 관리자입니다.</param>
    /// <exception cref="ArgumentNullException">데이터 테이블 관리자가 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">이미 초기화됐거나 필수 경제 데이터 테이블이 없는 경우 발생합니다.</exception>
    public void InitializeNewGame(DataTableManager dataTableManager)
    {
        if (dataTableManager == null)
        {
            throw new ArgumentNullException(nameof(dataTableManager));
        }

        if (this.IsInitialized)
        {
            throw new InvalidOperationException("게임 세션이 이미 초기화되었습니다.");
        }

        EconomyBalanceDataTable economyBalanceTable =
            dataTableManager.GetDB<EconomyBalanceDataTable>(DataTableType.EconomyBalance);
        MaintenanceBalanceDataTable maintenanceBalanceTable =
            dataTableManager.GetDB<MaintenanceBalanceDataTable>(DataTableType.MaintenanceBalance);

        if (economyBalanceTable == null || maintenanceBalanceTable == null)
        {
            throw new InvalidOperationException("경제 밸런스 데이터 테이블이 준비되지 않았습니다.");
        }

        EconomyBalanceData balanceData = economyBalanceTable.GetData();
        long[] maintenanceAmounts = maintenanceBalanceTable.GetMaintenanceAmounts();

        // 검증된 두 데이터 테이블을 결합해 현재 세션의 경제 런타임을 한 번만 생성합니다.
        this.economy = new EconomyRuntime(balanceData, maintenanceAmounts);
        this.IsInitialized = true;
    }

    /// <summary>
    /// 싱글톤이 제거될 때 현재 세션의 런타임 참조를 정리합니다.
    /// </summary>
    protected override void OnSingletonDestroyed()
    {
        // Scene 또는 세션 수명이 끝날 때 경제 이벤트 구독을 정리합니다.
        this.economy?.Dispose();
        this.economy = null;
        this.IsInitialized = false;
        base.OnSingletonDestroyed();
    }
}
