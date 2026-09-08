using System;
using System.Collections.Generic;

/// <summary>
/// 현재 날짜와 하루 진행 수명을 관리하고 주기적인 상납 결과를 전체 진행에 반영합니다.
/// 재정·손님·상품의 규칙은 각 런타임 시스템에 위임합니다.
/// </summary>
public sealed class GameProgress
{
    // 현재 세션의 보유금·일일 집계·상납 서비스를 소유한 런타임입니다.
    private readonly EconomyRuntime economy;

    // 검증된 손님·상품 데이터의 소유자입니다.
    private readonly CustomerCatalog customerCatalog;

    // 날짜가 바뀌어도 재사용할 손님 생성 난수원입니다.
    private readonly Random random;

    // 모든 하루에 적용할 영업시간(초)입니다.
    private readonly float businessDurationSeconds;

    // 현재 날짜의 하루 진행을 소유합니다.
    private DayProgress currentDayProgress;

    /// <summary>전체 진행의 현재 상태입니다.</summary>
    public GameProgressState State { get; private set; }

    /// <summary>현재 게임 날짜입니다. 첫날은 1입니다.</summary>
    public int CurrentDay { get; private set; }

    /// <summary>현재 실행 중인 하루 진행입니다. 시작 전에는 null입니다.</summary>
    public DayProgress CurrentDayProgress => this.currentDayProgress;

    /// <summary>현재 날짜가 상납일인지 나타냅니다.</summary>
    public bool IsMaintenanceDay => this.CurrentDay > 0
        && this.CurrentDay % this.economy.Settings.MaintenanceCycleDays == 0;

    /// <summary>다음 상납일까지 남은 날짜 수입니다.</summary>
    public int DaysUntilMaintenance
    {
        get
        {
            if (this.CurrentDay <= 0)
            {
                return 0;
            }

            int cycleDays = this.economy.Settings.MaintenanceCycleDays;
            int remainder = this.CurrentDay % cycleDays;
            return remainder == 0 ? 0 : cycleDays - remainder;
        }
    }

    /// <summary>현재 날짜에 납부할 상납금 회차입니다.</summary>
    public int CurrentMaintenanceRound => this.IsMaintenanceDay
        ? this.CurrentDay / this.economy.Settings.MaintenanceCycleDays
        : 0;

    /// <summary>전체 진행 상태가 변경된 뒤 발생합니다.</summary>
    public event Action<GameProgressState> StateChanged;

    /// <summary>새로운 하루가 시작된 뒤 발생합니다.</summary>
    public event Action<DayProgress> DayStarted;

    /// <summary>상납 실패로 전체 진행이 실패 상태가 된 뒤 발생합니다.</summary>
    public event Action<MaintenancePaymentResult> GameFailed;

    /// <summary>
    /// 검증된 런타임 시스템을 사용하는 전체 진행을 생성합니다.
    /// </summary>
    /// <param name="economy">현재 세션의 경제 런타임입니다.</param>
    /// <param name="customerCatalog">검증된 손님·상품 데이터입니다.</param>
    /// <param name="random">손님 생성에 사용할 난수원입니다.</param>
    /// <param name="businessDurationSeconds">하루 영업시간(초)입니다.</param>
    /// <exception cref="ArgumentNullException">필수 인수가 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">영업시간이 허용 범위를 벗어난 경우 발생합니다.</exception>
    public GameProgress(
        EconomyRuntime economy,
        CustomerCatalog customerCatalog,
        Random random,
        float businessDurationSeconds = DayProgress.DefaultBusinessDurationSeconds)
    {
        if (economy == null)
        {
            throw new ArgumentNullException(nameof(economy));
        }

        if (customerCatalog == null)
        {
            throw new ArgumentNullException(nameof(customerCatalog));
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

        this.economy = economy;
        this.customerCatalog = customerCatalog;
        this.random = random;
        this.businessDurationSeconds = businessDurationSeconds;
        this.State = GameProgressState.Initializing;
    }

    /// <summary>1일차부터 전체 진행을 시작합니다.</summary>
    /// <exception cref="InvalidOperationException">이미 전체 진행을 시작한 경우 발생합니다.</exception>
    public void Start()
    {
        if (this.State != GameProgressState.Initializing)
        {
            throw new InvalidOperationException("전체 진행은 한 번만 시작할 수 있습니다.");
        }

        this.CurrentDay = 1;
        this.startCurrentDay();
    }

    /// <summary>현재 하루의 영업 전 단계를 완료하고 영업을 시작합니다.</summary>
    /// <exception cref="InvalidOperationException">현재 하루가 없거나 영업을 시작할 수 없는 경우 발생합니다.</exception>
    public void OpenBusiness()
    {
        this.requireDayInProgress();
        this.currentDayProgress.OpenBusiness();
    }

    /// <summary>현재 하루의 제한시간을 진행합니다.</summary>
    /// <param name="deltaSeconds">경과 시간(초)입니다.</param>
    public void Tick(float deltaSeconds)
    {
        this.requireDayInProgress();
        this.currentDayProgress.Tick(deltaSeconds);
    }

    /// <summary>현재 손님에게 판매 상품 목록과 가격을 확정합니다.</summary>
    /// <param name="offeredTotal">플레이어가 제안한 양의 가격입니다.</param>
    /// <param name="saleItems">플레이어가 판매 대상으로 선택한 상품별 수량입니다.</param>
    /// <returns>손님이 수락하면 true입니다.</returns>
    public bool SubmitOffer(long offeredTotal, IReadOnlyList<SaleItem> saleItems)
    {
        this.requireDayInProgress();
        return this.currentDayProgress.SubmitOffer(offeredTotal, saleItems);
    }

    /// <summary>현재 손님의 등장 연출을 마치고 물품 분류 단계로 진행합니다.</summary>
    public void BeginCustomerSorting()
    {
        this.requireDayInProgress();
        this.currentDayProgress.BeginSorting();
    }

    /// <summary>현재 거래 결과 화면을 닫고 다음 거래 또는 마감으로 진행합니다.</summary>
    public void CompleteTransactionResult()
    {
        this.requireDayInProgress();
        this.currentDayProgress.CompleteTransactionResult();
    }

    /// <summary>현재 하루의 영업시간을 일시정지합니다.</summary>
    public void Pause()
    {
        this.requireDayInProgress();
        this.currentDayProgress.Pause();
    }

    /// <summary>일시정지한 현재 하루의 영업시간을 재개합니다.</summary>
    public void Resume()
    {
        this.requireDayInProgress();
        this.currentDayProgress.Resume();
    }

    /// <summary>일일 정산 화면 확인을 완료합니다.</summary>
    public void CompleteSettlement()
    {
        this.requireDayInProgress();
        this.currentDayProgress.CompleteSettlement();
    }

    /// <summary>
    /// 현재 상납일의 다음 회차 상납을 시도하고 다음 날 또는 실패 상태로 전환합니다.
    /// </summary>
    /// <returns>상납에 성공하면 true, 잔액 부족으로 실패하면 false입니다.</returns>
    /// <exception cref="InvalidOperationException">현재 상납 상태가 아닌 경우 발생합니다.</exception>
    public bool TryPayMaintenance()
    {
        if (this.State != GameProgressState.Maintenance)
        {
            throw new InvalidOperationException("상납 상태에서만 상납을 시도할 수 있습니다.");
        }

        int paymentRound = this.CurrentMaintenanceRound;
        if (paymentRound <= 0)
        {
            throw new InvalidOperationException("현재 날짜가 상납일이 아닙니다.");
        }

        bool isPaid = this.economy.MaintenanceService.TryPay(
            paymentRound,
            out MaintenancePaymentResult result);

        if (!isPaid)
        {
            this.changeState(GameProgressState.Failed);
            this.GameFailed?.Invoke(result);
            return false;
        }

        this.CurrentDay = checked(this.CurrentDay + 1);
        this.startCurrentDay();
        return true;
    }

    /// <summary>하루 완료 이벤트를 현재 하루와 대조해 한 번만 처리합니다.</summary>
    /// <param name="completedDay">완료된 하루 진행입니다.</param>
    private void handleDayCompleted(DayProgress completedDay)
    {
        if (this.State != GameProgressState.DayInProgress
            || !ReferenceEquals(this.currentDayProgress, completedDay)
            || completedDay.State != DayProgressState.Completed)
        {
            throw new InvalidOperationException("완료 통지의 하루 진행이 현재 상태와 일치하지 않습니다.");
        }

        if (this.IsMaintenanceDay)
        {
            this.changeState(GameProgressState.Maintenance);
            return;
        }

        this.CurrentDay = checked(this.CurrentDay + 1);
        this.startCurrentDay();
    }

    /// <summary>현재 날짜의 하루 객체를 만들고 시작합니다.</summary>
    private void startCurrentDay()
    {
        if (this.currentDayProgress != null)
        {
            this.currentDayProgress.Completed -= this.handleDayCompleted;
        }

        var nextDay = new DayProgress(
            this.CurrentDay,
            this.economy,
            this.customerCatalog,
            this.random,
            this.businessDurationSeconds);

        nextDay.Completed += this.handleDayCompleted;
        nextDay.Start();
        this.currentDayProgress = nextDay;
        this.changeState(GameProgressState.DayInProgress);
        this.DayStarted?.Invoke(nextDay);
    }

    /// <summary>하루 진행 API를 사용할 수 있는지 확인합니다.</summary>
    private void requireDayInProgress()
    {
        if (this.State != GameProgressState.DayInProgress || this.currentDayProgress == null)
        {
            throw new InvalidOperationException("현재 진행 중인 하루가 없습니다.");
        }
    }

    /// <summary>전체 진행 상태를 변경하고 변경 사실을 알립니다.</summary>
    /// <param name="nextState">변경할 상태입니다.</param>
    private void changeState(GameProgressState nextState)
    {
        this.State = nextState;
        this.StateChanged?.Invoke(nextState);
    }
}
