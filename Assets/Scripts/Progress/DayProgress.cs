using System;
using System.Collections.Generic;

/// <summary>
/// 한 영업일의 상태, 제한시간, 손님 거래와 일일 재정 집계 순서를 관리합니다.
/// 손님과 상품의 생성 규칙 및 거래 가격 판정은 각 담당 시스템에 위임합니다.
/// </summary>
public sealed class DayProgress
{
    /// <summary>기본 MVP 영업시간(초)입니다.</summary>
    public const float DefaultBusinessDurationSeconds = 30f;

    // 날짜·영업·현재가의 단일 소유자이며 경제 상태도 이 세션에서만 얻습니다.
    private readonly GameSessionManager session;

    // 판정 이후 재정 접수 실패는 재시도나 다음 거래로 우회하지 않습니다.
    private bool hasTransactionError;

    // 현재 하루를 식별하는 표시용 날짜입니다.
    private readonly int day;

    // 하루 영업에 사용할 제한시간(초)입니다.
    private readonly float businessDurationSeconds;

    // 현재 세션의 경제 서비스와 일일 집계를 소유한 런타임입니다.
    private readonly EconomyRuntime economy;

    // 검증된 손님·상품 데이터의 소유자입니다.
    private readonly CustomerCatalog customerCatalog;

    // 명성 구간·정산 규칙을 제공하는 검증된 밸런스 테이블입니다.
    private readonly ReputationBalanceDataTable reputationBalanceTable;

    // 이 날 정산에 사용하며 향후 손님 구성 요청에도 전달할 시작 명성 snapshot입니다.
    private readonly int dayStartReputation;

    // 확정 구성 snapshot을 CustomerVisit으로 옮기는 무상태 생성기입니다.
    private readonly CustomerGenerator customerGenerator;

    // 하루 시작 명성에 대응하는 손님 구성 가중치 snapshot입니다.
    private readonly ReputationBalanceData reputationBalance;

    // 하루 동안 성향·상품·성별 교대 상태를 선택하는 selector입니다.
    private readonly CustomerCompositionSelector customerCompositionSelector;

    // 검증된 외형 후보를 하루 동안 재사용합니다.
    private readonly IReadOnlyList<uint> appearanceIds;

    // 검증된 성향 후보를 하루 동안 재사용합니다.
    private readonly IReadOnlyList<CustomerDispositionData> dispositions;

    // 현재 계산대에서 처리 중인 손님입니다.
    private CustomerVisit currentVisit;

    // 정산 완료 후 확정된 일일 경제 집계입니다.
    private DailyAggregationResult? aggregationResult;

    // 미납과 유예 조건까지 포함해 정산 완료 후 확정된 최종 결과입니다.
    private DailySettlementResult? settlementResult;

    // 정산 완료 후 다음 날에 적용할 명성 계산 결과입니다.
    private DailyReputationCalculationResult? dailyReputationResult;

    // 현재 영업에 남은 시간(초)입니다.
    private float remainingSeconds;

    // 일시정지 여부입니다. Closing 이후에는 항상 false입니다.
    private bool isPaused;

    // 현재 날짜의 성공 거래 수입니다.
    private int successfulSales;

    // 현재 날짜의 거절 거래 수입니다.
    private int refusedCustomers;

    // 옵션이 켜진 하루만 큐를 소유하고 시간·인계를 수행합니다.
    private readonly CustomerQueue queue;

    /// <summary>현재 하루가 FIFO 큐를 사용하는지 여부.</summary>
    public bool UsesCustomerQueue => this.queue != null;
    /// <summary>FIFO 순서의 대기 조회. 표현은 목록이나 방문 상태를 변경하지 않는다.</summary>
    public IReadOnlyList<CustomerQueue.Entry> WaitingCustomers => this.queue?.Waiting ?? Array.Empty<CustomerQueue.Entry>();
    /// <summary>대사 유지 중인 만료 이탈 조회. 계산대 소유권은 포함하지 않는다.</summary>
    public IReadOnlyList<CustomerQueue.Entry> LeavingCustomers => this.queue?.Leaving ?? Array.Empty<CustomerQueue.Entry>();
    /// <summary>대기 만료로 이탈한 수. 별도 금액·명성 벌칙을 적용하지 않는다.</summary>
    public int DepartedCustomers => this.queue?.AbandonedCount ?? 0;

    /// <summary>담당하는 게임 날짜입니다.</summary>
    public int Day => this.day;

    /// <summary>현재 하루 진행 상태입니다.</summary>
    public DayProgressState State { get; private set; }

    /// <summary>현재 계산대 손님입니다. 손님이 없으면 null입니다.</summary>
    public CustomerVisit CurrentVisit => this.currentVisit;

    /// <summary>현재 영업에 남은 시간(초)입니다.</summary>
    public float RemainingSeconds => this.remainingSeconds;

    /// <summary>하루의 전체 영업시간(초)입니다.</summary>
    public float BusinessDurationSeconds => this.businessDurationSeconds;

    /// <summary>영업시간이 일시정지됐는지 나타냅니다.</summary>
    public bool IsPaused => this.isPaused;

    /// <summary>제한시간이 만료됐는지 나타냅니다.</summary>
    public bool IsBusinessTimeExpired => this.remainingSeconds <= 0f
        && (this.State == DayProgressState.Closing
            || this.State == DayProgressState.Settlement
            || this.State == DayProgressState.Completed);

    /// <summary>현재 손님에게 가격을 확정할 수 있는지 나타냅니다.</summary>
    public bool CanSubmitOffer =>
        !this.hasTransactionError
        && (this.State == DayProgressState.Sorting || this.State == DayProgressState.Closing)
        && this.currentVisit != null
        && this.currentVisit.State == CustomerState.AwaitingOffer;

    /// <summary>현재 날짜의 성공 거래 수입니다.</summary>
    public int SuccessfulSales => this.successfulSales;

    /// <summary>현재 날짜의 거절 거래 수입니다.</summary>
    public int RefusedCustomers => this.refusedCustomers;

    /// <summary>정산 완료 후 확정된 일일 집계입니다. 정산 전에는 null입니다.</summary>
    public DailyAggregationResult? AggregationResult => this.aggregationResult;

    /// <summary>미납·유예·게임오버 조건까지 포함한 최종 정산 결과입니다.</summary>
    public DailySettlementResult? SettlementResult => this.settlementResult;

    /// <summary>이 날의 명성 정산에 사용하며 향후 손님 구성 요청에도 전달할 시작 명성입니다.</summary>
    public int DayStartReputation => this.dayStartReputation;

    /// <summary>정산 완료 후 다음 날에 적용할 명성 계산 결과입니다.</summary>
    public DailyReputationCalculationResult? DailyReputationResult => this.dailyReputationResult;

    /// <summary>하루 진행 상태가 변경된 뒤 발생합니다.</summary>
    public event Action<DayProgressState> StateChanged;

    /// <summary>새로운 손님이 계산대에 활성화된 뒤 발생합니다.</summary>
    public event Action<CustomerVisit> CustomerStarted;
    /// <summary>거래 확인 후 계산대 소유권을 반납한 원본 방문. 연출 완료를 기다리지 않는다.</summary>
    public event Action<CustomerVisit> CustomerDeparted;

    /// <summary>거래 판정과 재정 반영이 완료된 뒤 발생합니다.</summary>
    public event Action<CustomerVisit> TransactionCompleted;

    /// <summary>일일 집계가 확정된 뒤 발생합니다.</summary>
    public event Action<DailySettlementResult> SettlementStarted;

    /// <summary>정산 확인이 끝나 하루가 완료된 뒤 발생합니다.</summary>
    public event Action<DayProgress> Completed;

    /// <summary>
    /// 지정된 날짜의 하루 진행을 생성합니다.
    /// </summary>
    /// <param name="day">1부터 시작하는 게임 날짜입니다.</param>
    /// <param name="session">현재 날짜와 경제 런타임을 소유한 초기화된 세션입니다.</param>
    /// <param name="customerCatalog">검증된 손님·상품 데이터입니다.</param>
    /// <param name="reputationBalanceTable">명성 구간과 일일 정산을 정의하는 검증된 테이블입니다.</param>
    /// <param name="random">손님 생성에 사용할 난수원입니다.</param>
    /// <param name="dayStartReputation">하루 시작 시점에 고정할 명성입니다.</param>
    /// <param name="businessDurationSeconds">영업 제한시간(초)입니다.</param>
    /// <param name="useCustomerQueue">true면 후속 방문을 5초 간격 FIFO에서 인계한다.</param>
    /// <exception cref="ArgumentNullException">필수 인수가 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">날짜 또는 영업시간이 허용 범위를 벗어난 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">세션이 초기화되지 않았거나 날짜가 다른 경우.</exception>
    public DayProgress(
        int day,
        GameSessionManager session,
        CustomerCatalog customerCatalog,
        ReputationBalanceDataTable reputationBalanceTable,
        Random random,
        int dayStartReputation = 0,
        float businessDurationSeconds = DefaultBusinessDurationSeconds,
        bool useCustomerQueue = false)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (checked((uint)(day - 1)) != session.ElapsedDays)
            throw new InvalidOperationException("하루 진행 날짜와 세션 날짜가 다릅니다.");

        if (customerCatalog == null)
        {
            throw new ArgumentNullException(nameof(customerCatalog));
        }

        if (reputationBalanceTable == null)
        {
            throw new ArgumentNullException(nameof(reputationBalanceTable));
        }

        if (dayStartReputation < -100 || dayStartReputation > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(dayStartReputation), dayStartReputation, "명성은 -100~100 범위여야 합니다.");
        }

        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        if (float.IsNaN(businessDurationSeconds)
            || float.IsInfinity(businessDurationSeconds)
            || businessDurationSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(businessDurationSeconds),
                businessDurationSeconds,
                "영업시간은 유한한 양수여야 합니다.");
        }

        this.day = day;
        this.session = session;
        this.economy = session.Economy;
        this.customerCatalog = customerCatalog;
        this.reputationBalanceTable = reputationBalanceTable;
        this.dayStartReputation = dayStartReputation;
        if (!this.reputationBalanceTable.TryGetByReputation(dayStartReputation, out ReputationBalanceData balance) ||
            balance == null)
            throw new InvalidOperationException($"명성 {dayStartReputation}에 대응하는 손님 구성 데이터가 없습니다.");

        this.reputationBalance = balance;
        this.customerCompositionSelector = new CustomerCompositionSelector(random);
        this.customerGenerator = new CustomerGenerator();
        this.businessDurationSeconds = businessDurationSeconds;

        var appearanceIds = new List<uint>(this.customerCatalog.Appearances.Rows.Keys);
        var dispositions = new List<CustomerDispositionData>(this.customerCatalog.Dispositions.Rows.Values);
        dispositions.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        this.appearanceIds = appearanceIds.AsReadOnly();
        this.dispositions = dispositions.AsReadOnly();
        if (useCustomerQueue) this.queue = new CustomerQueue(this.createCustomer, customerCatalog.Dispositions.Rows);
        this.State = DayProgressState.Initializing;
    }

    /// <summary>
    /// 하루를 초기화하고 가격표를 포함한 영업 전 상태로 진입합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">이미 하루를 시작한 경우 발생합니다.</exception>
    public void Start()
    {
        if (this.State != DayProgressState.Initializing)
        {
            throw new InvalidOperationException("하루 진행은 한 번만 시작할 수 있습니다.");
        }

        this.session.EnsureInspectorDay();
        this.changeState(this.session.InspectorEvents.HasPending ? DayProgressState.InspectorEvent : DayProgressState.PreOpen);
    }

    /// <summary>현재 화면의 감독관 대사 입력만 적용한다.</summary>
    /// <param name="snapshot">사용자가 보고 있던 대사 상태.</param>
    /// <returns>입력을 적용했으면 true.</returns>
    public bool AdvanceInspector(InspectorEventSnapshot snapshot)
    {
        return State == DayProgressState.InspectorEvent && snapshot.Day == (uint)Day &&
            session.InspectorEvents.Advance(snapshot.Day, snapshot.EventIdx, snapshot.LineIndex);
    }

    /// <summary>감독관 퇴장 완료 후 남은 이벤트가 없을 때만 영업 전 단계로 이동한다.</summary>
    /// <param name="snapshot">퇴장을 시작한 화면 상태.</param>
    /// <returns>이번 퇴장 완료를 적용했으면 true.</returns>
    public bool CompleteInspectorExit(InspectorEventSnapshot snapshot)
    {
        if (State != DayProgressState.InspectorEvent || snapshot.Day != (uint)Day ||
            !session.InspectorEvents.CompleteExit(snapshot.Day, snapshot.EventIdx)) return false;
        if (!session.InspectorEvents.HasPending) changeState(DayProgressState.PreOpen);
        return true;
    }

    /// <summary>
    /// 영업 전 정보 확인을 완료하고 제한시간 영업을 시작합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// 영업 전 상태가 아니거나 판매 가능한 상품이 없는 경우 발생합니다.
    /// </exception>
    public void OpenBusiness()
    {
        if (this.State != DayProgressState.PreOpen)
        {
            throw new InvalidOperationException("영업 전 상태에서만 영업을 시작할 수 있습니다.");
        }

        // 외부 집계를 열기 전에 첫 손님을 준비해 초기화 실패 시 부분 상태를 만들지 않습니다.
        CustomerVisit firstVisit;
        if (this.queue == null) firstVisit = this.createCustomer();
        else
        {
            this.queue.Start();
            try
            {
                if (!this.queue.TryAdd()) throw new InvalidOperationException("첫 대기 손님을 생성할 수 없습니다.");
                firstVisit = this.queue.TakeNext();
            }
            catch { this.queue.Stop(); throw; }
        }
        try { this.session.BeginTradingDay(); }
        catch { this.queue?.Stop(); throw; }
        this.remainingSeconds = this.businessDurationSeconds;
        this.isPaused = false;
        this.currentVisit = firstVisit;
        this.currentVisit.BeginOffer();
        this.changeState(DayProgressState.Operating);
        this.CustomerStarted?.Invoke(this.currentVisit);
    }

    /// <summary>
    /// 경과 시간만큼 영업 제한시간을 진행하고 만료 시 마감 상태로 전환합니다.
    /// </summary>
    /// <param name="deltaSeconds">이전 갱신 이후의 경과 시간(초)입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">경과 시간이 음수이거나 유한하지 않은 경우 발생합니다.</exception>
    public void Tick(float deltaSeconds)
    {
        this.requireTransactionHealthy();
        if (float.IsNaN(deltaSeconds)
            || float.IsInfinity(deltaSeconds)
            || deltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "경과 시간은 유한한 0 이상 값이어야 합니다.");
        }

        if (this.isPaused
            || (this.State != DayProgressState.Operating
                && this.State != DayProgressState.Sorting
                && this.State != DayProgressState.TransactionResult))
        {
            return;
        }

        // 긴 프레임도 실제 남은 영업시간만 방송 시계에 전달합니다. Closing에는 진행하지 않습니다.
        float tradingSeconds = Math.Min(deltaSeconds, this.remainingSeconds);
        this.session.AdvanceTradingTime(tradingSeconds, false);
        this.queue?.Advance(tradingSeconds, false);
        this.remainingSeconds = Math.Max(0f, this.remainingSeconds - tradingSeconds);
        if (this.remainingSeconds <= 0f)
        {
            this.beginClosing();
        }
        else if (this.queue != null && this.currentVisit == null) this.startNextCustomer();
    }

    /// <summary>
    /// 현재 손님에게 판매 상품 목록과 전체 장바구니 가격을 한 번 제안합니다.
    /// </summary>
    /// <param name="offeredTotal">플레이어가 입력한 양의 가격입니다.</param>
    /// <param name="saleItems">플레이어가 판매 대상으로 선택한 상품별 수량입니다.</param>
    /// <returns>손님이 가격을 수락하면 true입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">가격이 0 이하인 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">현재 상태에서 가격을 입력할 수 없는 경우 발생합니다.</exception>
    public bool SubmitOffer(long offeredTotal, IReadOnlyList<SaleItem> saleItems)
    {
        if (!this.CanSubmitOffer)
        {
            throw new InvalidOperationException("현재 상태에서는 가격을 확정할 수 없습니다.");
        }

        bool wasAccepted = this.currentVisit.SubmitOffer(offeredTotal, saleItems);
        // 거절도 명성 집계에 포함하며 상세 판정·원가·지침 snapshot은 그대로 한 번 전달합니다.
        try { this.applyTransactionResult(this.currentVisit.Result.Value); }
        catch
        {
            this.hasTransactionError = true;
            throw;
        }
        if (wasAccepted)
        {
            this.successfulSales = checked(this.successfulSales + 1);
        }
        else
        {
            this.refusedCustomers = checked(this.refusedCustomers + 1);
        }

        if (this.State == DayProgressState.Operating
            || this.State == DayProgressState.Sorting)
        {
            this.changeState(DayProgressState.TransactionResult);
        }

        this.TransactionCompleted?.Invoke(this.currentVisit);
        return wasAccepted;
    }

    /// <summary>손님 정면 등장과 물품 쏟기 연출을 마치고 판매 분류 상태로 진입합니다.</summary>
    /// <exception cref="InvalidOperationException">현재 손님이 없거나 영업 상태가 아닌 경우 발생합니다.</exception>
    public void BeginSorting()
    {
        if (this.State != DayProgressState.Operating
            || this.currentVisit == null
            || this.currentVisit.State != CustomerState.AwaitingOffer)
        {
            throw new InvalidOperationException("활성 손님의 등장 연출이 끝난 뒤에만 물품 분류를 시작할 수 있습니다.");
        }

        this.changeState(DayProgressState.Sorting);
    }

    /// <summary>
    /// 현재 거래 결과 확인을 마치고 손님을 퇴장시킵니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">확인할 거래 결과가 없는 경우 발생합니다.</exception>
    public void CompleteTransactionResult()
    {
        this.requireTransactionHealthy();
        if (this.currentVisit == null
            || (this.currentVisit.State != CustomerState.Accepted
                && this.currentVisit.State != CustomerState.Rejected))
        {
            throw new InvalidOperationException("확인할 거래 결과가 없습니다.");
        }

        CustomerVisit departed = this.currentVisit;
        departed.Depart();
        this.currentVisit = null;
        this.CustomerDeparted?.Invoke(departed);

        if (this.State == DayProgressState.Closing)
        {
            this.tryBeginSettlement();
            return;
        }

        if (this.State != DayProgressState.TransactionResult)
        {
            throw new InvalidOperationException("거래 결과 상태가 올바르지 않습니다.");
        }

        this.changeState(DayProgressState.Operating);
        this.startNextCustomer();
    }

    /// <summary>
    /// 영업시간을 일시정지합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">현재 영업을 일시정지할 수 없는 경우 발생합니다.</exception>
    public void Pause()
    {
        this.requireTransactionHealthy();
        if (this.isPaused
            || (this.State != DayProgressState.Operating
                && this.State != DayProgressState.Sorting
                && this.State != DayProgressState.TransactionResult))
        {
            throw new InvalidOperationException("현재 상태에서는 영업을 일시정지할 수 없습니다.");
        }

        this.isPaused = true;
    }

    /// <summary>
    /// 일시정지한 영업시간을 다시 진행합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">재개할 수 없는 상태인 경우 발생합니다.</exception>
    public void Resume()
    {
        this.requireTransactionHealthy();
        if (!this.isPaused || this.remainingSeconds <= 0f
            || (this.State != DayProgressState.Operating
                && this.State != DayProgressState.Sorting
                && this.State != DayProgressState.TransactionResult))
        {
            throw new InvalidOperationException("현재 상태에서는 영업을 재개할 수 없습니다.");
        }

        this.isPaused = false;
    }

    /// <summary>
    /// 일일 정산 화면 확인을 완료하고 하루를 종료합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">정산 상태가 아닌 경우 발생합니다.</exception>
    public void CompleteSettlement()
    {
        this.requireTransactionHealthy();
        if (this.State != DayProgressState.Settlement)
        {
            throw new InvalidOperationException("정산 상태에서만 하루를 완료할 수 있습니다.");
        }

        this.changeState(DayProgressState.Completed);
        this.Completed?.Invoke(this);
    }

    /// <summary>날짜에 등장할 수 있는 다음 손님을 생성하고 가격 제안 대기로 전환합니다.</summary>
    private void startNextCustomer()
    {
        if (this.State != DayProgressState.Operating || this.remainingSeconds <= 0f)
        {
            this.beginClosing();
            return;
        }

        CustomerVisit nextVisit = this.queue == null ? this.createCustomer() : this.queue.TakeNext();
        if (nextVisit == null) return;
        nextVisit.BeginOffer();
        this.currentVisit = nextVisit;
        this.CustomerStarted?.Invoke(this.currentVisit);
    }

    /// <summary>하루 시작 명성·현재가·설비 상태로 구성을 선택하고 방문 객체를 생성합니다.</summary>
    private CustomerVisit createCustomer()
    {
        if (checked((uint)(this.day - 1)) != this.session.ElapsedDays)
            throw new InvalidOperationException("하루 진행 날짜와 세션 날짜가 다릅니다.");
        DailyPriceState dailyPrices = this.session.EnsureDailyPrices();
        CustomerComposition composition = this.customerCompositionSelector.SelectComposition(
            this.appearanceIds,
            this.dispositions,
            this.customerCatalog.Products.Rows,
            this.reputationBalance,
            dailyPrices.Prices,
            this.session.ElapsedDays,
            this.session.IsFacilityActive);

        CustomerVisit visit = composition == null
            ? null
            : this.customerGenerator.Generate(
                composition,
                this.customerCatalog.Products.Rows,
                () => this.session.EnsureDailyPrices().Prices,
                moralityCalculator: this.session.MoralityCalculator,
                getDailyGuidelines: () =>
                {
                    this.session.EnsureDailyPrices();
                    return this.session.DailyGuidelines;
                });

        if (visit == null)
        {
            throw new InvalidOperationException(
                $"Day {this.day}에 생성 가능한 상품이 없습니다.");
        }

        return visit;
    }

    /// <summary>시간 만료를 확정하고 마지막 거래 마감 상태로 전환합니다.</summary>
    private void beginClosing()
    {
        if (this.State == DayProgressState.Closing
            || this.State == DayProgressState.Settlement
            || this.State == DayProgressState.Completed)
        {
            return;
        }

        this.remainingSeconds = 0f;
        this.isPaused = false;
        this.queue?.Stop();
        this.changeState(DayProgressState.Closing);
        this.tryBeginSettlement();
    }

    /// <summary>활성 거래가 없으면 일일 집계를 종료하고 정산 상태로 전환합니다.</summary>
    private void tryBeginSettlement()
    {
        if (this.State != DayProgressState.Closing)
        {
            return;
        }

        if (this.currentVisit != null)
        {
            if (this.currentVisit.State != CustomerState.Departed)
            {
                return;
            }

            this.currentVisit = null;
        }

        this.session.EndTradingDay(out DailyAggregationResult result);
        this.aggregationResult = result;
        this.settlementResult = this.session.LastSettlementResult ??
            throw new InvalidOperationException("세션에서 최종 정산 결과를 생성하지 않았습니다.");
        DailyReputationCalculator calculator = new DailyReputationCalculator(
            this.reputationBalanceTable,
            this.customerCatalog.Dispositions);
        this.dailyReputationResult = calculator.Calculate(
            this.dayStartReputation,
            this.aggregationResult.Value.Transactions);
        this.changeState(DayProgressState.Settlement);
        this.SettlementStarted?.Invoke(this.settlementResult.Value);
    }

    /// <summary>재정 접수 오류 이후 진행을 재개하거나 정산 성공으로 우회하지 못하게 한다.</summary>
    /// <exception cref="InvalidOperationException">확정 거래의 재정 접수가 실패한 상태.</exception>
    private void requireTransactionHealthy()
    {
        if (this.hasTransactionError)
            throw new InvalidOperationException("확정 거래의 재정 반영이 실패해 하루 진행이 중단되었습니다.");
    }

    /// <summary>표현 소유자가 파괴될 때 대기 방문과 남은 말풍선을 불만 없이 정리한다.</summary>
    internal void StopQueue() => this.queue?.Stop();

    /// <summary>모델 시계 기준으로 현재 표시할 대사 PK를 조회한다.</summary>
    /// <param name="entry">현재 대기 또는 이탈 항목.</param>
    /// <returns>표시할 대사 PK. 대사가 없으면 0.</returns>
    /// <exception cref="ArgumentNullException">항목이 null.</exception>
    public uint GetQueueSpeech(CustomerQueue.Entry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        return this.queue?.GetSpeech(entry) ?? 0;
    }

    /// <summary>방문이 확정한 원본 거래 결과를 현재 일일 재정 집계에 반영합니다.</summary>
    /// <param name="transactionResult">반영할 거래 결과입니다.</param>
    /// <exception cref="InvalidOperationException">종료된 일일 집계에 반영하려는 경우 발생합니다.</exception>
    private void applyTransactionResult(TransactionResult transactionResult)
    {
        if (!this.session.TryApplyTransaction(transactionResult))
        {
            throw new InvalidOperationException("종료된 일일 집계에는 거래를 반영할 수 없습니다.");
        }
    }

    /// <summary>하루 진행 상태를 변경합니다.</summary>
    /// <param name="nextState">변경할 상태입니다.</param>
    private void changeState(DayProgressState nextState)
    {
        this.State = nextState;
        this.StateChanged?.Invoke(nextState);
    }
}
