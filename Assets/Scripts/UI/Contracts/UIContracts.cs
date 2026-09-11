using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================================
// UI 표현 계층 데이터 계약 (UIContracts.cs)
//
// 원칙:
// 1. 모든 ViewData는 UI에 필요한 시점의 읽기 전용 스냅샷이다.
// 2. 게임 규칙, 날짜 진행, 거래 판정, 재정 계산과 데이터 저장은 UI 계층에 포함하지 않는다.
// 3. UI 표시 문구를 게임 상태 식별자나 시스템 간 routing 값으로 사용하지 않는다.
// =========================================================================

/// <summary>
/// 게임 날짜 및 영업 진행 상태를 나타내는 열거형
/// </summary>
public enum GameDayPhase
{
    PreOpen,         // 영업 전 (메인 허브)
    PriceGuide,      // 가격표 안내 (레거시 호환; 신규 진행은 PreOpen에 통합)
    Operating,       // 영업 진행 중 (손님 거래)
    TradingResult,   // 거래 결과 확인
    Closing,         // 제한시간 만료 후 마지막 거래 마감
    DailySettlement  // 일일 정산
}

/// <summary>
/// 3.1 EconomyStatusPresenter가 수신하는 재정 상태 읽기 전용 스냅샷
/// </summary>
[Serializable]
public struct EconomyStatusViewData
{
    public long CurrentBalance;  // 현재 플레이어 보유금
    public long DailySaleIncome; // 현재 영업일에 누적된 판매 수입

    public EconomyStatusViewData(long currentBalance, long dailySaleIncome)
    {
        this.CurrentBalance = currentBalance;
        this.DailySaleIncome = dailySaleIncome;
    }
}

/// <summary>
/// 3.2 GameDayPresenter가 수신하는 게임 일자 및 진행 상태 스냅샷
/// </summary>
[Serializable]
public struct GameDayViewData
{
    public int CurrentDay;            // 현재 게임 날짜
    public GameDayPhase Phase;        // 영업 전, 영업 중, 거래 결과, 일일 정산 등 현재 진행 상태

    public GameDayViewData(int currentDay, GameDayPhase phase)
    {
        this.CurrentDay = currentDay;
        this.Phase = phase;
    }
}

/// <summary>
/// 3.3 BusinessTimerPresenter가 수신하는 영업시간 스냅샷
/// </summary>
[Serializable]
public struct BusinessTimerViewData
{
    public float RemainingSeconds; // 현재 영업에 남은 시간(초)
    public float NormalizedTime;    // UI 게이지에 사용할 0~1 범위 진행값
    public bool IsPaused;          // 영업 시간이 일시정지된 상태인지 여부
    public bool CanPause;          // 현재 진행 상태가 일시정지를 허용하는지 여부
    public bool CanResume;         // 현재 진행 상태가 재개를 허용하는지 여부

    /// <summary>영업시간 표시와 입력 가능 상태를 생성합니다.</summary>
    /// <param name="remainingSeconds">남은 영업시간(초)입니다.</param>
    /// <param name="normalizedTime">게이지에 표시할 0~1 진행값입니다.</param>
    /// <param name="isPaused">현재 일시정지 여부입니다.</param>
    /// <param name="canPause">일시정지 입력 허용 여부입니다.</param>
    /// <param name="canResume">재개 입력 허용 여부입니다.</param>
    public BusinessTimerViewData(
        float remainingSeconds,
        float normalizedTime,
        bool isPaused,
        bool canPause,
        bool canResume)
    {
        this.RemainingSeconds = remainingSeconds;
        this.NormalizedTime = normalizedTime;
        this.IsPaused = isPaused;
        this.CanPause = canPause;
        this.CanResume = canResume;
    }
}

/// <summary>
/// 3.4 PriceListPresenter가 수신하는 개별 물품 가격표 항목 스냅샷
/// </summary>
[Serializable]
public struct ItemPriceViewData
{
    public uint ItemId;         // 물품 시스템의 권위 식별자 (FK)
    public string DisplayName;  // 화면에 표시할 물품 이름
    public long Price;          // 화면에 표시할 판매 기준 가격
    public string SpecialNote;  // 할인, 판매 조건 등 선택 표시 문구
    public Sprite Icon;         // 물품 아이콘
    public bool IsAvailable;    // 현재 가격표에 활성 상태로 표시할 수 있는지 여부

    public ItemPriceViewData(uint itemId, string displayName, long price, string specialNote = null, Sprite icon = null, bool isAvailable = true)
    {
        this.ItemId = itemId;
        this.DisplayName = displayName;
        this.Price = price;
        this.SpecialNote = specialNote;
        this.Icon = icon;
        this.IsAvailable = isAvailable;
    }
}

/// <summary>
/// 3.5 CustomerPresenter가 수신하는 손님 장바구니 개별 물품 스냅샷
/// </summary>
[Serializable]
public struct CustomerBasketItemViewData
{
    public uint ItemId;         // 물품 시스템 식별자
    public string DisplayName;  // 표시 상품명
    public int Quantity;        // 수량
    public Sprite Icon;         // 상품 아이콘
    /// <summary>탑뷰 분류 작업에 표시할 Sprite. 실제 데이터 경로에서는 명시적으로 전달한다.</summary>
    public Sprite TopViewIcon;
    public int UnitPrice;       // 화면 표시용 단가

    public CustomerBasketItemViewData(uint itemId, string displayName, int quantity, Sprite icon = null, int unitPrice = 0, Sprite topViewIcon = null)
    {
        this.ItemId = itemId;
        this.DisplayName = displayName;
        this.Quantity = quantity;
        this.Icon = icon;
        this.TopViewIcon = topViewIcon ?? icon;
        this.UnitPrice = unitPrice;
    }
}

/// <summary>
/// 3.5 CustomerPresenter가 수신하는 손님 화면 스냅샷
/// 손님의 내부 판정 수치(예산, 인내도)는 UI 표시 요구가 확정되지 않았으므로 제외합니다.
/// </summary>
[Serializable]
public struct CustomerViewData
{
    public bool HasCustomer;                               // 손님 존재 여부 (false면 UI 초기화/숨김)
    public Color AppearanceColor;                          // 손님 외형 컬러
    public Sprite AppearanceSprite;                        // 손님 외형 스프라이트 (선택)
    public CustomerAttributes Attributes;                  // 성별·연령·특수 속성 스냅샷
    public string DialogueText;                            // 손님 입장 또는 피드백 대사
    public IReadOnlyList<CustomerBasketItemViewData> Basket; // 장바구니 품목 목록

    public CustomerViewData(
        bool hasCustomer,
        Color appearanceColor,
        Sprite appearanceSprite,
        string dialogueText,
        IReadOnlyList<CustomerBasketItemViewData> basket,
        CustomerAttributes attributes = CustomerAttributes.None)
    {
        this.HasCustomer = hasCustomer;
        this.AppearanceColor = appearanceColor;
        this.AppearanceSprite = appearanceSprite;
        this.Attributes = attributes;
        this.DialogueText = dialogueText;
        this.Basket = basket;
    }

    public static CustomerViewData Empty => new CustomerViewData(false, Color.clear, null, string.Empty, Array.Empty<CustomerBasketItemViewData>());
}

/// <summary>
/// 3.6 PriceInputPresenter가 수신하는 가격 입력 상태 스냅샷
/// </summary>
[Serializable]
public struct PriceInputViewData
{
    public long? InputAmount;        // 현재 입력된 가격 (입력이 비어 있으면 null)
    public bool CanConfirm;          // 현재 확정 버튼을 활성화할 수 있는지 여부
    public bool IsInputEnabled;      // 현재 가격 입력을 받을 수 있는 상태인지 여부
    public string ValidationMessage; // 형식 오류 등 입력 단계에서 표시할 안내 문구

    public PriceInputViewData(long? inputAmount, bool canConfirm, bool isInputEnabled, string validationMessage = null)
    {
        this.InputAmount = inputAmount;
        this.CanConfirm = canConfirm;
        this.IsInputEnabled = isInputEnabled;
        this.ValidationMessage = validationMessage;
    }
}

/// <summary>
/// 거래 판정 결과 스냅샷
/// </summary>
[Serializable]
public struct TransactionViewData
{
    public bool WasAccepted;       // 거래 수락 여부
    public long OfferedPrice;      // 제안 가격
    public string FeedbackMessage; // 안내/반응 문구

    public TransactionViewData(bool wasAccepted, long offeredPrice, string feedbackMessage)
    {
        this.WasAccepted = wasAccepted;
        this.OfferedPrice = offeredPrice;
        this.FeedbackMessage = feedbackMessage;
    }
}

/// <summary>정산 화면에 표시할 지침별 위반 횟수와 벌금입니다.</summary>
public readonly struct SettlementGuidelineViolationViewData
{
    /// <summary>손님 조건·상품·제한을 조합한 완성 문구입니다.</summary>
    public string Content { get; }
    /// <summary>해당 지침의 당일 위반 횟수입니다.</summary>
    public int ViolationCount { get; }
    /// <summary>해당 지침의 당일 벌금 합계입니다.</summary>
    public long PenaltyAmount { get; }

    /// <summary>지침별 정산 표시 항목을 생성합니다.</summary>
    /// <param name="content">완성된 지침 문구입니다.</param>
    /// <param name="violationCount">양수 위반 횟수입니다.</param>
    /// <param name="penaltyAmount">음수가 아닌 벌금 합계입니다.</param>
    public SettlementGuidelineViolationViewData(string content, int violationCount, long penaltyAmount)
    {
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("지침 표시 문구가 필요합니다.", nameof(content));
        if (violationCount <= 0) throw new ArgumentOutOfRangeException(nameof(violationCount));
        if (penaltyAmount < 0) throw new ArgumentOutOfRangeException(nameof(penaltyAmount));
        Content = content;
        ViolationCount = violationCount;
        PenaltyAmount = penaltyAmount;
    }
}

/// <summary>최종 통합 정산 결과를 화면에 그대로 전달하는 읽기 전용 스냅샷입니다.</summary>
public readonly struct DailySettlementViewData
{
    public int Day { get; }
    public long SaleIncome { get; }
    public long Expenses { get; }
    public long NetProfit { get; }
    public long CurrentBalance { get; }
    public int ReputationDelta { get; }
    public int SuccessfulSales { get; }
    public int RefusedCustomers { get; }
    public int DepartedCustomers { get; }
    public long MaintenanceAmount { get; }
    public long GuidelinePenaltyAmount { get; }
    public IReadOnlyList<SettlementGuidelineViolationViewData> GuidelineViolations { get; }
    public long PreviousUnpaidAmount { get; }
    public long TotalPaymentDue { get; }
    public long PaidAmount { get; }
    public long UnpaidAmount { get; }
    public int? GracePeriodEndDay { get; }
    public int RemainingGraceDays { get; }
    public bool IsGameOverConditionMet { get; }

    /// <summary>도메인에서 확정된 값을 재계산하지 않는 정산 표시 스냅샷을 생성합니다.</summary>
    public DailySettlementViewData(
        int day,
        long saleIncome,
        long currentBalance,
        int reputationDelta,
        int successfulSales,
        int refusedCustomers,
        int departedCustomers,
        long maintenanceAmount,
        long guidelinePenaltyAmount,
        IReadOnlyList<SettlementGuidelineViolationViewData> guidelineViolations,
        long previousUnpaidAmount,
        long totalPaymentDue,
        long paidAmount,
        long unpaidAmount,
        int? gracePeriodEndDay,
        int remainingGraceDays,
        bool isGameOverConditionMet)
    {
        Day = day;
        SaleIncome = saleIncome;
        MaintenanceAmount = maintenanceAmount;
        GuidelinePenaltyAmount = guidelinePenaltyAmount;
        Expenses = checked(maintenanceAmount + guidelinePenaltyAmount);
        NetProfit = checked(saleIncome - Expenses);
        CurrentBalance = currentBalance;
        ReputationDelta = reputationDelta;
        SuccessfulSales = successfulSales;
        RefusedCustomers = refusedCustomers;
        DepartedCustomers = departedCustomers;
        GuidelineViolations = new List<SettlementGuidelineViolationViewData>(
            guidelineViolations ?? Array.Empty<SettlementGuidelineViolationViewData>()).AsReadOnly();
        PreviousUnpaidAmount = previousUnpaidAmount;
        TotalPaymentDue = totalPaymentDue;
        PaidAmount = paidAmount;
        UnpaidAmount = unpaidAmount;
        GracePeriodEndDay = gracePeriodEndDay;
        RemainingGraceDays = remainingGraceDays;
        IsGameOverConditionMet = isGameOverConditionMet;
    }
}

/// <summary>
/// 영업 전 지침서(가격표) 내 단일 상품 항목 스냅샷
/// </summary>
[Serializable]
public struct PriceGuideProductViewData
{
    public uint ProductIdx;     // 상품 식별자
    public string Name;         // 상품명
    public long Price;          // 단가
    public Sprite Icon;         // 상품 아이콘

    public PriceGuideProductViewData(uint productIdx, string name, long price, Sprite icon)
    {
        this.ProductIdx = productIdx;
        this.Name = name;
        this.Price = price;
        this.Icon = icon;
    }
}

/// <summary>영업 시작 화면에 표시할 일일지침 한 항목의 식별자와 완성 문구입니다.</summary>
public readonly struct DailyGuidelineViewData
{
    /// <summary>표시 문구의 원본이 된 일일지침 데이터 PK입니다.</summary>
    public uint GuidelineIdx { get; }
    /// <summary>손님 조건, 물품명과 제한 유형을 조합한 완성 문구입니다.</summary>
    public string Content { get; }

    /// <summary>일일지침 표시 항목을 생성합니다.</summary>
    /// <param name="guidelineIdx">DailyGuidelineData PK입니다.</param>
    /// <param name="content">화면에 바로 표시할 비어 있지 않은 문구입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">지침 PK가 0인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">표시 문구가 비어 있는 경우 발생합니다.</exception>
    public DailyGuidelineViewData(uint guidelineIdx, string content)
    {
        if (guidelineIdx == 0) throw new ArgumentOutOfRangeException(nameof(guidelineIdx));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("지침 표시 문구가 필요합니다.", nameof(content));
        GuidelineIdx = guidelineIdx;
        Content = content;
    }
}

/// <summary>영업 시작 화면이 렌더링할 날짜·당일 상품·지침과 입력 가능 상태의 불변 스냅샷입니다.</summary>
public readonly struct PreOpenGuidelineViewData
{
    /// <summary>1부터 시작하는 표시 일차입니다.</summary>
    public int Day { get; }
    /// <summary>아이콘·이름·당일 가격으로 구성된 최대 8개의 상품입니다.</summary>
    public IReadOnlyList<PriceGuideProductViewData> Products { get; }
    /// <summary>완성 문구를 포함한 최대 2개의 일일지침입니다.</summary>
    public IReadOnlyList<DailyGuidelineViewData> Guidelines { get; }
    /// <summary>영업 시작 후 일일지침을 다시 볼 수 없음을 알리는 단일 안내 문구입니다.</summary>
    public string Notice { get; }
    /// <summary>현재 영업 시작 입력을 받을 수 있는지 나타냅니다.</summary>
    public bool CanOpenBusiness { get; }

    /// <summary>검증된 영업 시작 화면 스냅샷을 생성합니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <param name="products">최대 8개의 당일 상품 표시 항목입니다.</param>
    /// <param name="guidelines">최대 2개의 일일지침 표시 항목입니다.</param>
    /// <param name="notice">비어 있지 않은 단일 안내 문구입니다.</param>
    /// <param name="canOpenBusiness">영업 시작 버튼 활성 여부입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">일차나 목록 개수가 범위를 벗어난 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">안내 문구가 비어 있는 경우 발생합니다.</exception>
    public PreOpenGuidelineViewData(
        int day,
        IReadOnlyList<PriceGuideProductViewData> products,
        IReadOnlyList<DailyGuidelineViewData> guidelines,
        string notice,
        bool canOpenBusiness)
    {
        if (day <= 0) throw new ArgumentOutOfRangeException(nameof(day));
        if (products != null && products.Count > 8) throw new ArgumentOutOfRangeException(nameof(products));
        if (guidelines != null && guidelines.Count > 2) throw new ArgumentOutOfRangeException(nameof(guidelines));
        if (string.IsNullOrWhiteSpace(notice)) throw new ArgumentException("영업 시작 안내 문구가 필요합니다.", nameof(notice));

        Day = day;
        Products = new List<PriceGuideProductViewData>(products ?? Array.Empty<PriceGuideProductViewData>()).AsReadOnly();
        Guidelines = new List<DailyGuidelineViewData>(guidelines ?? Array.Empty<DailyGuidelineViewData>()).AsReadOnly();
        Notice = notice;
        CanOpenBusiness = canOpenBusiness;
    }

}
