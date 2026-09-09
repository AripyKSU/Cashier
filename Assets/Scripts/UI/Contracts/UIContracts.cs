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
    DailySettlement, // 일일 정산
    Tribute          // 상납/공물
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
    public int DaysUntilSettlement;   // 다음 정산(상납)일까지 남은 게임 내 날짜 수
    public bool IsSettlementDay;      // 현재 날짜가 정산일인지 여부
    public GameDayPhase Phase;        // 영업 전, 영업 중, 거래 결과, 일일 정산 등 현재 진행 상태

    public GameDayViewData(int currentDay, int daysUntilSettlement, bool isSettlementDay, GameDayPhase phase)
    {
        this.CurrentDay = currentDay;
        this.DaysUntilSettlement = daysUntilSettlement;
        this.IsSettlementDay = isSettlementDay;
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
    public int UnitPrice;       // 화면 표시용 단가

    public CustomerBasketItemViewData(uint itemId, string displayName, int quantity, Sprite icon = null, int unitPrice = 0)
    {
        this.ItemId = itemId;
        this.DisplayName = displayName;
        this.Quantity = quantity;
        this.Icon = icon;
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
    public string DialogueText;                            // 손님 입장 또는 피드백 대사
    public IReadOnlyList<CustomerBasketItemViewData> Basket; // 장바구니 품목 목록

    public CustomerViewData(bool hasCustomer, Color appearanceColor, Sprite appearanceSprite, string dialogueText, IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        this.HasCustomer = hasCustomer;
        this.AppearanceColor = appearanceColor;
        this.AppearanceSprite = appearanceSprite;
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

/// <summary>
/// 3.8 DailySettlementPresenter가 수신하는 일일 정산 결과 스냅샷
/// </summary>
[Serializable]
public struct DailySettlementViewData
{
    public int Day;                 // 정산 대상 게임 날짜
    public long SaleIncome;         // 하루 동안 확정된 판매 수입
    public long Expenses;           // 하루 동안 확정된 지출
    public long NetProfit;          // 외부 로직에서 확정한 일일 순이익
    public long CurrentBalance;     // 정산 완료 시점의 현재 보유금
    public int ReputationDelta;     // formatter 입력용 내부 값이며 UI에는 숫자로 직접 표시하지 않음
    public int SuccessfulSales;     // 거래에 성공한 손님 또는 거래 수
    public int RefusedCustomers;    // 거래가 거절된 손님 수
    public int DepartedCustomers;   // 대기 중 이탈한 손님 수

    public DailySettlementViewData(int day, long saleIncome, long expenses, long netProfit, long currentBalance, int reputationDelta, int successfulSales, int refusedCustomers, int departedCustomers)
    {
        this.Day = day;
        this.SaleIncome = saleIncome;
        this.Expenses = expenses;
        this.NetProfit = netProfit;
        this.CurrentBalance = currentBalance;
        this.ReputationDelta = reputationDelta;
        this.SuccessfulSales = successfulSales;
        this.RefusedCustomers = refusedCustomers;
        this.DepartedCustomers = departedCustomers;
    }
}
