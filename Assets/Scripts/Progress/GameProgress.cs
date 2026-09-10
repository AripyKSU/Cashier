using System;
using System.Collections.Generic;

/// <summary>
/// 현재 날짜와 하루 진행 수명을 관리하고 일일 정산 결과를 전체 진행에 반영합니다.
/// 재정·손님·상품의 규칙은 각 런타임 시스템에 위임합니다.
/// </summary>
public sealed class GameProgress
{
    // 경제 런타임과 경과일의 단일 소유자입니다.
    private readonly GameSessionManager session;
    // 현재 세션의 보유금·일일 집계·유지비 서비스를 소유한 런타임입니다.
    private readonly EconomyRuntime economy;

    // 검증된 손님·상품 데이터의 소유자입니다.
    private readonly CustomerCatalog customerCatalog;

    // 명성 구간과 일일 정산 규칙을 제공하는 검증된 밸런스 테이블입니다.
    private readonly ReputationBalanceDataTable reputationBalanceTable;

    // 날짜가 바뀌어도 재사용할 손님 생성 난수원입니다.
    private readonly Random random;

    // 모든 하루에 적용할 영업시간(초)입니다.
    private readonly float businessDurationSeconds;

    // 현재 날짜의 하루 진행을 소유합니다.
    private DayProgress currentDayProgress;

    /// <summary>전체 진행의 현재 상태입니다.</summary>
    public GameProgressState State { get; private set; }

    /// <summary>현재 게임 날짜입니다. 첫날은 1입니다.</summary>
    public int CurrentDay => this.State == GameProgressState.Initializing
        ? 0 : checked((int)this.session.ElapsedDays + 1);

    /// <summary>현재 실행 중인 하루 진행입니다. 시작 전에는 null입니다.</summary>
    public DayProgress CurrentDayProgress => this.currentDayProgress;

    /// <summary>다음 영업일의 손님 구성에 사용할 현재 명성입니다.</summary>
    public int CurrentReputation => this.session.CurrentReputation;

    /// <summary>현재 세션의 거래별·일일 명성 계산 로그 서비스입니다.</summary>
    public ReputationLogService ReputationLogService => this.session.ReputationLogService;

    /// <summary>설비 결제와 보유 반영이 끝난 뒤 세션에서 전달하는 완료 이벤트입니다.</summary>
    public event Action<FacilityPurchaseEvent> FacilityPurchaseCompleted
    {
        add => this.session.FacilityPurchaseCompleted += value;
        remove => this.session.FacilityPurchaseCompleted -= value;
    }

    /// <summary>현재 세션의 가게 단계입니다.</summary>
    public uint CurrentStoreStage => this.session.CurrentStoreStage;

    /// <summary>설비 업그레이드 보유 여부를 조회합니다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <returns>구매했으면 true.</returns>
    public bool IsFacilityOwned(uint facilityIdx) => this.session.IsFacilityOwned(facilityIdx);

    /// <summary>설비 업그레이드가 현재 활성인지 조회합니다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <returns>현재 날짜에 활성화됐으면 true.</returns>
    public bool IsFacilityUpgradeActive(uint facilityIdx) => this.session.IsFacilityUpgradeActive(facilityIdx);

    /// <summary>편의성 효과가 현재 활성인지 조회합니다.</summary>
    /// <param name="effectType">조회할 편의성 효과.</param>
    /// <returns>효과가 활성화됐으면 true.</returns>
    public bool IsFacilityEffectActive(ConvenienceEffectType effectType) =>
        this.session.IsFacilityEffectActive(effectType);

    /// <summary>설비 업그레이드의 활성 경과일을 조회합니다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <param name="activationDay">보유 설비의 활성 경과일.</param>
    /// <returns>보유 설비이면 true.</returns>
    public bool TryGetFacilityActivationDay(uint facilityIdx, out uint activationDay) =>
        this.session.TryGetFacilityActivationDay(facilityIdx, out activationDay);

    /// <summary>전체 진행 상태가 변경된 뒤 발생합니다.</summary>
    public event Action<GameProgressState> StateChanged;

    /// <summary>새로운 하루가 시작된 뒤 발생합니다.</summary>
    public event Action<DayProgress> DayStarted;

    /// <summary>
    /// 검증된 런타임 시스템을 사용하는 전체 진행을 생성합니다.
    /// </summary>
    /// <param name="session">초기화된 날짜·경제 상태의 단일 소유자입니다.</param>
    /// <param name="customerCatalog">검증된 손님·상품 데이터입니다.</param>
    /// <param name="reputationBalanceTable">명성 구간과 일일 정산을 정의하는 검증된 테이블입니다.</param>
    /// <param name="random">손님 생성에 사용할 난수원입니다.</param>
    /// <param name="businessDurationSeconds">하루 영업시간(초)입니다.</param>
    /// <exception cref="ArgumentNullException">필수 인수가 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">영업시간이 허용 범위를 벗어난 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">세션 경제 런타임이 초기화되지 않은 경우.</exception>
    public GameProgress(
        GameSessionManager session,
        CustomerCatalog customerCatalog,
        ReputationBalanceDataTable reputationBalanceTable,
        Random random,
        float businessDurationSeconds = DayProgress.DefaultBusinessDurationSeconds)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (customerCatalog == null)
        {
            throw new ArgumentNullException(nameof(customerCatalog));
        }

        if (reputationBalanceTable == null)
        {
            throw new ArgumentNullException(nameof(reputationBalanceTable));
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

        this.session = session;
        this.economy = session.Economy;
        this.customerCatalog = customerCatalog;
        this.reputationBalanceTable = reputationBalanceTable;
        this.random = random;
        this.businessDurationSeconds = businessDurationSeconds;
        this.State = GameProgressState.Initializing;
    }

    /// <summary>세션의 현재 날짜에서 진행을 시작합니다. 영업 중 저장 복원은 지원하지 않습니다.</summary>
    /// <exception cref="InvalidOperationException">이미 전체 진행을 시작한 경우 발생합니다.</exception>
    public void Start()
    {
        if (this.State != GameProgressState.Initializing)
        {
            throw new InvalidOperationException("전체 진행은 한 번만 시작할 수 있습니다.");
        }

        if (this.economy.QueryService.IsDayOpen)
            throw new InvalidOperationException("영업 중인 세션의 진행을 새로 만들 수 없습니다.");
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

    /// <summary>살아 있는 진행에서 설비를 일회 구매한다. 구매 UI의 세부 phase 정책은 포함하지 않는다.</summary>
    /// <param name="facilityIdx">구매할 설비 PK. 가격·날짜는 외부에서 받지 않는다.</param>
    /// <param name="result">구매 결과·이번 지출·활성일.</param>
    /// <returns>이번 요청이 구매를 완료했으면true.</returns>
    /// <exception cref="InvalidOperationException">시작 전 또는 종료된 진행.</exception>
    /// <exception cref="Exception">입력·날짜·재정 알림 오류. 차감 후 알림 실패는 보유 상태를 유지한다.</exception>
    public bool TryPurchaseFacility(uint facilityIdx, out FacilityPurchaseResult result)
    {
        if (State == GameProgressState.Initializing || State == GameProgressState.Failed || State == GameProgressState.Completed)
            throw new InvalidOperationException("시작 전 또는 종료한 게임에서는 설비를 구매할 수 없습니다.");
        return session.TryPurchaseFacility(facilityIdx, out result);
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

        this.session.CompleteDay(checked((uint)(completedDay.Day - 1)));
        this.applyCompletedDayReputation(completedDay);
        this.startCurrentDay();
    }

    /// <summary>현재 날짜의 하루 객체를 만들고 시작합니다.</summary>
    private void startCurrentDay()
    {
        // 새 날짜의 가격 계산이 실패하면 새 하루를 공개하지 않습니다.
        this.session.EnsureDailyPrices();
        if (this.currentDayProgress != null)
        {
            this.currentDayProgress.Completed -= this.handleDayCompleted;
            this.currentDayProgress.TransactionCompleted -= this.handleTransactionCompleted;
            this.currentDayProgress.SettlementStarted -= this.handleSettlementStarted;
        }

        var nextDay = new DayProgress(
            checked((int)this.session.ElapsedDays + 1),
            this.session,
            this.customerCatalog,
            this.reputationBalanceTable,
            this.random,
            this.CurrentReputation,
            this.businessDurationSeconds);

        nextDay.Completed += this.handleDayCompleted;
        nextDay.TransactionCompleted += this.handleTransactionCompleted;
        nextDay.SettlementStarted += this.handleSettlementStarted;
        nextDay.Start();
        this.currentDayProgress = nextDay;
        this.changeState(GameProgressState.DayInProgress);
        this.DayStarted?.Invoke(nextDay);
    }

    /// <summary>확정된 거래를 명성 로그 서비스에 기록합니다.</summary>
    /// <param name="visit">판정과 재정 반영이 완료된 손님 방문입니다.</param>
    /// <exception cref="ArgumentNullException">방문이 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">거래 결과가 없는 경우 발생합니다.</exception>
    private void handleTransactionCompleted(CustomerVisit visit)
    {
        if (visit == null)
        {
            throw new ArgumentNullException(nameof(visit));
        }

        if (!visit.Result.HasValue)
        {
            throw new InvalidOperationException("명성 로그에는 확정된 거래 결과가 필요합니다.");
        }

        this.ReputationLogService.RecordTransaction(this.CurrentDay, visit.Result.Value);
    }

    /// <summary>계산이 끝난 일일 명성 정산 과정을 로그 서비스에 기록합니다.</summary>
    /// <param name="aggregationResult">하루 동안 접수된 거래 snapshot입니다.</param>
    /// <exception cref="InvalidOperationException">일일 명성 계산 결과가 없는 경우 발생합니다.</exception>
    private void handleSettlementStarted(DailyAggregationResult aggregationResult)
    {
        if (this.currentDayProgress == null || !this.currentDayProgress.DailyReputationResult.HasValue)
        {
            throw new InvalidOperationException("명성 정산 로그에는 일일 계산 결과가 필요합니다.");
        }

        DailyReputationCalculationResult reputationResult = this.currentDayProgress.DailyReputationResult.Value;
        if (aggregationResult.Transactions.Count != reputationResult.ActualTransactionCount)
        {
            throw new InvalidOperationException("명성 정산 로그의 거래 수가 일일 집계와 일치하지 않습니다.");
        }

        if (!this.reputationBalanceTable.TryGetByReputation(
                this.currentDayProgress.DayStartReputation,
                out ReputationBalanceData reputationData))
        {
            throw new InvalidOperationException("명성 정산 로그에 사용할 밸런스 데이터가 없습니다.");
        }

        this.ReputationLogService.RecordDailySettlement(
            this.CurrentDay,
            this.currentDayProgress.DayStartReputation,
            reputationResult,
            reputationData);
    }

    /// <summary>완료된 하루의 정산 결과를 다음 날 시작 직전에 한 번 적용합니다.</summary>
    /// <param name="completedDay">정산이 끝난 완료된 하루입니다.</param>
    /// <exception cref="ArgumentNullException">완료된 하루가 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">명성 정산 결과가 없는 경우 발생합니다.</exception>
    private void applyCompletedDayReputation(DayProgress completedDay)
    {
        if (completedDay == null)
        {
            throw new ArgumentNullException(nameof(completedDay));
        }

        if (!completedDay.DailyReputationResult.HasValue)
        {
            throw new InvalidOperationException("완료된 하루의 명성 정산 결과가 없습니다.");
        }

        this.session.ApplyCompletedDayReputation(completedDay.Day, completedDay.DailyReputationResult.Value.FinalDelta);
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
