using System;

/// <summary>
/// 현재 게임 세션의 런타임 시스템을 생성하고 Scene 전환 동안 수명을 유지합니다.
/// </summary>
public sealed class GameSessionManager : Singleton<GameSessionManager>
{
    // 현재 게임 세션에서 사용하는 경제 런타임입니다.
    private EconomyRuntime economy;
    private DataTableManager dataTables;
    private FacilityService facilities;
    // 화면 재진입으로 초기화하지 않는 세션 명성과 마지막 반영 표시일입니다.
    private int currentReputation;
    private int lastReputationAppliedDay;
    private ReputationLogService reputationLogService;
    private MoralityCalculator moralityCalculator;
    private decimal currentMorality;
    private bool isApplyingTransaction;

    private readonly PriceEventScheduler priceScheduler = new PriceEventScheduler(new Random());
    private bool hasClosedDay;
    // 영업 시간으로만 감소하며 정산·오류 시 취소한다.
    private float radioRemainingSeconds;
    private bool radioPending;

    /// <summary>현재 세션의 명성. 다음 하루는 이 값을 snapshot으로 사용한다.</summary>
    public int CurrentReputation => this.currentReputation;
    /// <summary>게임 시작부터 반올림 없이 누적한 현재 도덕성.</summary>
    public decimal CurrentMorality => this.currentMorality;
    /// <summary>검증된 도덕성 계산기. 손님 거래 snapshot 생성에만 사용한다.</summary>
    internal MoralityCalculator MoralityCalculator => this.moralityCalculator
        ?? throw new InvalidOperationException("도덕성 데이터가 초기화되지 않았습니다.");
    /// <summary>화면 전환과 무관하게 유지되는 거래·정산 명성 로그.</summary>
    public ReputationLogService ReputationLogService => this.reputationLogService
        ?? throw new InvalidOperationException("명성 세션이 초기화되지 않았습니다.");

    /// <summary>날짜 완료 직후 해당 날짜의 명성을 세션에 한 번 반영한다.</summary>
    /// <param name="completedDisplayDay">완료한 1기반 표시일.</param>
    /// <param name="delta">명성 계산기가 확정한 변화량.</param>
    /// <exception cref="InvalidOperationException">세션 초기화 전이거나 날짜 완료 순서가 다름.</exception>
    /// <exception cref="OverflowException">명성 합산 범위 초과.</exception>
    internal void ApplyCompletedDayReputation(int completedDisplayDay, int delta)
    {
        if (!IsInitialized || completedDisplayDay <= 0 || (uint)completedDisplayDay != ElapsedDays)
            throw new InvalidOperationException("날짜 완료 직후의 명성만 반영할 수 있습니다.");
        if (completedDisplayDay <= this.lastReputationAppliedDay) return;
        this.currentReputation = Math.Max(-100, Math.Min(100, checked(this.currentReputation + delta)));
        this.lastReputationAppliedDay = completedDisplayDay;
    }

    /// <summary>보유 설비와 활성 경과일. 구매는 GameProgress 경계를 사용한다.</summary>
    public System.Collections.Generic.IReadOnlyDictionary<uint, uint> FacilityActivationDays => facilities?.ActivationDays
        ?? throw new InvalidOperationException("설비 세션이 초기화되지 않았습니다.");

    /// <summary>현재 세션 날짜의 설비 활성 여부를 조회한다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <returns>보유하며 활성일에 도달했는지 여부.</returns>
    /// <exception cref="InvalidOperationException">초기화 전 조회.</exception>
    public bool IsFacilityActive(uint facilityIdx)
    {
        if (!IsInitialized) throw new InvalidOperationException("세션 초기화 전입니다.");
        return facilities.IsActive(facilityIdx);
    }

    /// <summary>진행 소유자가 허용한 구매를 세션 현재 날짜와 검증 가격으로 처리한다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <param name="result">정상 구매 결과.</param>
    /// <returns>이번 요청의 구매 성공.</returns>
    /// <exception cref="InvalidOperationException">초기화 전 호출.</exception>
    internal bool TryPurchaseFacility(uint facilityIdx, out FacilityPurchaseResult result)
    {
        if (!IsInitialized) throw new InvalidOperationException("세션 초기화 전입니다.");
        return facilities.TryPurchase(facilityIdx, out result);
    }

    /// <summary>한 거래의 재정·도덕성 누적을 같은 확정 경계에서 한 번 반영한다.</summary>
    /// <param name="transactionResult">도덕성 snapshot을 포함한 확정 거래.</param>
    /// <returns>열린 영업일에 반영했으면 true.</returns>
    internal bool TryApplyTransaction(TransactionResult transactionResult)
    {
        if (!IsInitialized) throw new InvalidOperationException("세션 초기화 전입니다.");
        if (isApplyingTransaction) throw new InvalidOperationException("거래 반영 중 재진입할 수 없습니다.");
        if (!economy.DailyAggregationService.IsDayOpen) return false;
        decimal nextMorality = checked(this.currentMorality + (transactionResult.MoralityDelta ?? 0m));
        economy.DailyAggregationService.ValidateTransaction(transactionResult);
        bool hasMoralityChanged = nextMorality != this.currentMorality;
        isApplyingTransaction = true;
        this.currentMorality = nextMorality;
        try
        {
            if (hasMoralityChanged)
                UnityEngine.Debug.Log($"[Morality] current={this.currentMorality}, delta={transactionResult.MoralityDelta}");
            return economy.DailyAggregationService.TryApplyTransaction(transactionResult);
        }
        finally { isApplyingTransaction = false; }
    }

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
        try
        {
            var facilityTable = dataTableManager.GetDB<FacilityDataTable>(DataTableType.Facility)
                ?? throw new InvalidOperationException("설비 데이터 테이블이 준비되지 않았습니다.");
            if (facilityTable.Rows.Count == 0) throw new InvalidOperationException("설비 데이터가 공개되지 않았습니다.");
            this.facilities = new FacilityService(this.economy.FinanceService, facilityTable.Rows, () => this.ElapsedDays);
            this.reputationLogService = new ReputationLogService(dataTableManager.Customers.Dispositions);
            var moralityRows = new System.Collections.Generic.List<MoralityData>(
                dataTableManager.GetDB<MoralityDataTable>(DataTableType.Morality).Rows.Values);
            moralityRows.Sort((left, right) => left.Idx.CompareTo(right.Idx));
            this.moralityCalculator = new MoralityCalculator(moralityRows.AsReadOnly());
            this.currentReputation = 0;
            this.currentMorality = 0m;
            this.lastReputationAppliedDay = 0;
            this.IsInitialized = true;
            EnsureDailyPrices();
        }
        catch
        {
            this.economy.Dispose();
            this.economy = null;
            this.IsInitialized = false;
            this.facilities = null;
            this.reputationLogService = null;
            this.moralityCalculator = null;
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
        this.facilities = null;
        this.reputationLogService = null;
        this.moralityCalculator = null;
        this.currentReputation = 0;
        this.currentMorality = 0m;
        this.lastReputationAppliedDay = 0;
        base.OnSingletonDestroyed();
    }
}
