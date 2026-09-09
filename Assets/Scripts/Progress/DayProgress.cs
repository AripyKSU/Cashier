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

    // 손님 생성 규칙을 위임받은 기존 생성기입니다.
    private readonly CustomerGenerator customerGenerator;

    // 검증된 외형 후보를 하루 동안 재사용합니다.
    private readonly IReadOnlyList<uint> appearanceIds;

    // 검증된 성향 후보를 하루 동안 재사용합니다.
    private readonly IReadOnlyList<CustomerDispositionData> dispositions;

    // 현재 계산대에서 처리 중인 손님입니다.
    private CustomerVisit currentVisit;

    // 정산 완료 후 확정된 일일 경제 집계입니다.
    private DailyAggregationResult? aggregationResult;

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
        (this.State == DayProgressState.Sorting || this.State == DayProgressState.Closing)
        && this.currentVisit != null
        && this.currentVisit.State == CustomerState.AwaitingOffer;

    /// <summary>현재 날짜의 성공 거래 수입니다.</summary>
    public int SuccessfulSales => this.successfulSales;

    /// <summary>현재 날짜의 거절 거래 수입니다.</summary>
    public int RefusedCustomers => this.refusedCustomers;

    /// <summary>정산 완료 후 확정된 일일 집계입니다. 정산 전에는 null입니다.</summary>
    public DailyAggregationResult? AggregationResult => this.aggregationResult;

    /// <summary>이 날의 명성 정산에 사용하며 향후 손님 구성 요청에도 전달할 시작 명성입니다.</summary>
    public int DayStartReputation => this.dayStartReputation;

    /// <summary>정산 완료 후 다음 날에 적용할 명성 계산 결과입니다.</summary>
    public DailyReputationCalculationResult? DailyReputationResult => this.dailyReputationResult;

    /// <summary>하루 진행 상태가 변경된 뒤 발생합니다.</summary>
    public event Action<DayProgressState> StateChanged;

    /// <summary>새로운 손님이 계산대에 활성화된 뒤 발생합니다.</summary>
    public event Action<CustomerVisit> CustomerStarted;

    /// <summary>거래 판정과 재정 반영이 완료된 뒤 발생합니다.</summary>
    public event Action<CustomerVisit> TransactionCompleted;

    /// <summary>일일 집계가 확정된 뒤 발생합니다.</summary>
    public event Action<DailyAggregationResult> SettlementStarted;

    /// <summary>정산 확인이 끝나 하루가 완료된 뒤 발생합니다.</summary>
    public event Action<DayProgress> Completed;

    /// <summary>
    /// 지정된 날짜의 하루 진행을 생성합니다.
    /// </summary>
    /// <param name="day">1부터 시작하는 게임 날짜입니다.</param>
    /// <param name="economy">현재 세션의 경제 런타임입니다.</param>
    /// <param name="customerCatalog">검증된 손님·상품 데이터입니다.</param>
    /// <param name="reputationBalanceTable">명성 구간과 일일 정산을 정의하는 검증된 테이블입니다.</param>
    /// <param name="random">손님 생성에 사용할 난수원입니다.</param>
    /// <param name="dayStartReputation">하루 시작 시점에 고정할 명성입니다.</param>
    /// <param name="businessDurationSeconds">영업 제한시간(초)입니다.</param>
    /// <exception cref="ArgumentNullException">필수 인수가 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">날짜 또는 영업시간이 허용 범위를 벗어난 경우 발생합니다.</exception>
    public DayProgress(
        int day,
        EconomyRuntime economy,
        CustomerCatalog customerCatalog,
        ReputationBalanceDataTable reputationBalanceTable,
        Random random,
        int dayStartReputation = 0,
        float businessDurationSeconds = DefaultBusinessDurationSeconds)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        if (economy == null)
        {
            throw new ArgumentNullException(nameof(economy));
        }

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
        this.economy = economy;
        this.customerCatalog = customerCatalog;
        this.reputationBalanceTable = reputationBalanceTable;
        this.dayStartReputation = dayStartReputation;
        this.customerGenerator = new CustomerGenerator(random);
        this.businessDurationSeconds = businessDurationSeconds;

        var appearanceIds = new List<uint>(this.customerCatalog.Appearances.Rows.Keys);
        var dispositions = new List<CustomerDispositionData>(this.customerCatalog.Dispositions.Rows.Values);
        dispositions.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        this.appearanceIds = appearanceIds.AsReadOnly();
        this.dispositions = dispositions.AsReadOnly();
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

        this.changeState(DayProgressState.PreOpen);
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
        CustomerVisit firstVisit = this.createCustomer();
        this.economy.DailyAggregationService.BeginDay();
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

        this.remainingSeconds = Math.Max(0f, this.remainingSeconds - deltaSeconds);
        if (this.remainingSeconds <= 0f)
        {
            this.beginClosing();
        }
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
        TransactionResult transactionResult = this.createTransactionResult(this.currentVisit);
        this.applyTransactionResult(transactionResult);
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
        if (this.currentVisit == null
            || (this.currentVisit.State != CustomerState.Accepted
                && this.currentVisit.State != CustomerState.Rejected))
        {
            throw new InvalidOperationException("확인할 거래 결과가 없습니다.");
        }

        this.currentVisit.Depart();
        this.currentVisit = null;

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

        CustomerVisit nextVisit = this.createCustomer();
        nextVisit.BeginOffer();
        this.currentVisit = nextVisit;
        this.CustomerStarted?.Invoke(this.currentVisit);
    }

    /// <summary>검증된 카탈로그 후보를 기존 CustomerGenerator에 전달합니다.</summary>
    private CustomerVisit createCustomer()
    {
        CustomerVisit visit = this.customerGenerator.Generate(
            this.appearanceIds,
            this.dispositions,
            this.customerCatalog.Products.Rows,
            checked((uint)(this.day - 1)),
            () => GameSessionManager.Instance.EnsureDailyPrices().Prices);

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

        this.aggregationResult = this.economy.DailyAggregationService.EndDay();
        DailyReputationCalculator calculator = new DailyReputationCalculator(
            this.reputationBalanceTable,
            this.customerCatalog.Dispositions);
        this.dailyReputationResult = calculator.Calculate(
            this.dayStartReputation,
            this.aggregationResult.Value.Transactions);
        this.changeState(DayProgressState.Settlement);
        this.SettlementStarted?.Invoke(this.aggregationResult.Value);
    }

    /// <summary>
    /// CustomerVisit가 확정한 거래 결과를 재정 집계로 전달합니다.
    /// </summary>
    /// <param name="visit">거래 판정이 완료된 손님 방문입니다.</param>
    /// <returns>성공·거절 여부와 손님 snapshot을 포함한 거래 결과입니다.</returns>
    /// <exception cref="ArgumentNullException">방문이 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">수락 또는 판매 금액이 확정되지 않은 경우 발생합니다.</exception>
    private TransactionResult createTransactionResult(CustomerVisit visit)
    {
        if (visit == null)
        {
            throw new ArgumentNullException(nameof(visit));
        }

        if ((visit.State != CustomerState.Accepted && visit.State != CustomerState.Rejected) ||
            !visit.OfferedTotal.HasValue || !visit.Result.HasValue)
        {
            throw new InvalidOperationException("거래 판정이 확정된 방문만 거래 결과로 변환할 수 있습니다.");
        }
        return visit.Result.Value;
    }

    /// <summary>변환된 거래 결과를 현재 일일 재정 집계에 반영합니다.</summary>
    /// <param name="transactionResult">반영할 거래 결과입니다.</param>
    /// <exception cref="InvalidOperationException">종료된 일일 집계에 반영하려는 경우 발생합니다.</exception>
    private void applyTransactionResult(TransactionResult transactionResult)
    {
        if (!this.economy.DailyAggregationService.TryApplyTransaction(transactionResult))
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
