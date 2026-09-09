using System;

/// <summary>
/// 현재 게임 세션의 런타임 시스템을 생성하고 Scene 전환 동안 수명을 유지합니다.
/// </summary>
public sealed class GameSessionManager : Singleton<GameSessionManager>
{
    // 현재 게임 세션에서 사용하는 경제 런타임입니다.
    private EconomyRuntime economy;
    private DataTableManager dataTables;
    private readonly PriceEventScheduler priceScheduler = new PriceEventScheduler(new Random());
    private bool hasClosedDay;
    // 영업 시간으로만 감소하며 정산·오류 시 취소한다.
    private float radioRemainingSeconds;
    private bool radioPending;
    /// <summary>가격 확정 이후 현재일 영업을 시작한다.</summary>
    /// <exception cref="InvalidOperationException">이미 정산한 날짜 또는 영업 중.</exception>
    public void BeginTradingDay()
    {
        EnsureDailyPrices();
        if (hasClosedDay || economy.QueryService.IsDayOpen) throw new InvalidOperationException("이미 시작하거나 정산한 날짜입니다.");
        economy.DailyAggregationService.BeginDay();
        radioRemainingSeconds = priceScheduler.GetRadioDelaySeconds();
        radioPending = DailyPrices.RadioEventIdx.HasValue && !DailyPrices.IsRadioBroadcast;
    }
    /// <summary>영업을 정산하고 날짜 완료를 허용한다.</summary>
    /// <returns>당일 판매 수입.</returns>
    public long EndTradingDay()
    {
        return EndTradingDay(out _);
    }
    /// <summary>일일 집계를 한 번 종료하고 원본 집계 결과와 판매 수입을 함께 반환한다.</summary>
    /// <param name="result">경제 시스템이 확정한 일일 결과.</param>
    /// <returns>당일 판매 수입. 기존 무인자 API와 동일하다.</returns>
    /// <exception cref="InvalidOperationException">초기화 전 또는 열린 영업일이 없음.</exception>
    public long EndTradingDay(out DailyAggregationResult result)
    {
        result = Economy.DailyAggregationService.EndDay();
        radioPending = false;
        hasClosedDay = true;
        return result.SaleIncome;
    }
    /// <summary>세션의 날짜 권위. 게임 시작일은 0이다.</summary>
    public uint ElapsedDays { get; private set; }
    /// <summary>동일 날짜의 재추첨을 방지하는 확정 상태.</summary>
    public DailyPriceState DailyPrices { get; private set; }

    /// <summary>오늘 가격을 한 번만 확정한다. UI 재진입 시 동일 객체를 반환한다.</summary>
    /// <returns>신문·라디오·현재가 snapshot.</returns>
    /// <exception cref="InvalidOperationException">초기화 전 호출.</exception>
    public DailyPriceState EnsureDailyPrices()
    {
        if (!IsInitialized) throw new InvalidOperationException("세션 초기화 전입니다.");
        if (DailyPrices != null && DailyPrices.ElapsedDays == ElapsedDays) return DailyPrices;
        try
        {
            var next = priceScheduler.CreateDay(ElapsedDays,
                dataTables.GetDB<PriceEventDataTable>(DataTableType.PriceEvent).Rows,
                dataTables.GetDB<PriceEventScheduleDataTable>(DataTableType.PriceEventSchedule).Rows,
                dataTables.Customers.Products.Rows);
            DailyPrices = next;
            return DailyPrices;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"가격 이벤트 day={ElapsedDays}: {exception}");
            throw;
        }
    }

    /// <summary>활성 게임 화면 한 곳에서 프레임당 한 번 호출한다. 영업 중이며 일시정지가 아닐 때만 방송 시간을 진행한다.</summary>
    /// <param name="deltaSeconds">시간 배율을 적용하지 않은 프레임 경과 초.</param>
    /// <param name="isPaused">게임 일시정지 여부.</param>
    /// <returns>이번 호출에서 방송·가격 교체를 완료했는지 여부.</returns>
    /// <exception cref="ArgumentOutOfRangeException">경과 시간이 음수 또는 비유한 값.</exception>
    /// <exception cref="Exception">가격 계산 또는 텍스트 참조 실패. 재시도 없이 취소한다.</exception>
    public bool AdvanceTradingTime(float deltaSeconds, bool isPaused)
    {
        if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!IsInitialized || !radioPending || !economy.QueryService.IsDayOpen || isPaused || deltaSeconds == 0) return false;
        radioRemainingSeconds -= deltaSeconds;
        if (radioRemainingSeconds > 0) return false;
        radioPending = false; // 실패도 반복 방송·반복 오류로 바꾸지 않는다.
        try
        {
            var events = dataTables.GetDB<PriceEventDataTable>(DataTableType.PriceEvent).Rows;
            var next = priceScheduler.ApplyRadio(DailyPrices, events, dataTables.Customers.Products.Rows);
            var item = events[next.RadioEventIdx.Value];
            var texts = dataTables.GetDB<TextDataTable>(DataTableType.Text).Rows;
            string message = $"[Radio] day={ElapsedDays}, event={item.Idx}: {texts[item.NameIdx].Text} / {texts[item.DescriptionIdx].Text}";
            DailyPrices = next; // 기존 방문 snapshot은 변경하지 않는다.
            UnityEngine.Debug.Log(message);
            return true;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"라디오 방송 day={ElapsedDays}: {exception}");
            throw;
        }
    }

    /// <summary>정산·상납 완료 후 다음 날로 이동한다. 동일 완료 요청은 두 번 반영하지 않는다.</summary>
    /// <param name="completedDay">호출자가 완료한 경과일.</param>
    /// <exception cref="InvalidOperationException">날짜 불일치, 영업 중 또는 미정산·미납.</exception>
    public void CompleteDay(uint completedDay)
    {
        if (!IsInitialized || !hasClosedDay || completedDay != ElapsedDays || economy.QueryService.IsDayOpen)
            throw new InvalidOperationException("날짜 완료 상태가 아닙니다.");
        int displayDay = checked((int)ElapsedDays + 1);
        if (displayDay % economy.QueryService.MaintenanceCycleDays == 0 &&
            economy.MaintenanceService.LastPaidRound < displayDay / economy.QueryService.MaintenanceCycleDays)
            throw new InvalidOperationException("상납금 처리가 남았습니다.");
        ElapsedDays = checked(ElapsedDays + 1);
        hasClosedDay = false;
    }

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
        this.dataTables = dataTableManager;
        this.IsInitialized = true;
        try { EnsureDailyPrices(); }
        catch
        {
            this.economy.Dispose();
            this.economy = null;
            this.IsInitialized = false;
            throw;
        }
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
        this.DailyPrices = null;
        this.radioPending = false;
        this.dataTables = null;
        base.OnSingletonDestroyed();
    }
}
