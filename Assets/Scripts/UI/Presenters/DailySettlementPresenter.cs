using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3.8 DailySettlementPresenter
/// 하루 영업이 종료된 뒤 확정된 일일 정산 결과(매출, 지출, 순익, 잔액, 평판, 손님 통계)를 표시하는 Presenter.
/// Finance의 정산 로직을 직접 호출하거나 자체 집계하지 않으며, 다음 단계 진행 요청만 외부에 전달합니다.
/// </summary>
public class DailySettlementPresenter : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Ledger")]
    [Tooltip("정산 snapshot을 양쪽 가계부 페이지에 순차 출력하는 View")]
    [SerializeField] private DailySettlementLedgerView ledgerView;

    [Header("Reputation Stamp")]
    [SerializeField] private ReputationStampPresenter reputationStampPresenter;

    [Header("Text Displays")]
    [Tooltip("정산 대상 일자 텍스트")]
    [SerializeField] private TextMeshProUGUI dayText;

    [Tooltip("총 판매 수입 텍스트")]
    [SerializeField] private TextMeshProUGUI saleIncomeText;

    [Tooltip("총 지출 텍스트")]
    [SerializeField] private TextMeshProUGUI expensesText;

    [Tooltip("일일 순이익 텍스트")]
    [SerializeField] private TextMeshProUGUI netProfitText;

    [Tooltip("현재 보유금 텍스트")]
    [SerializeField] private TextMeshProUGUI currentBalanceText;

    [Tooltip("평판 변화 피드백 텍스트")]
    [SerializeField] private TextMeshProUGUI reputationDeltaText;

    [Header("Reputation Feedback Image")]
    [Tooltip("명성 변화 단계 이미지를 표시할 UI Image 영역입니다.")]
    [SerializeField] private Image reputationFeedbackImage;

    [Tooltip("명성이 크게 나빠졌을 때 표시할 Sprite")]
    [SerializeField] private Sprite greatlyWorsenedReputationSprite;

    [Tooltip("명성이 나빠졌을 때 표시할 Sprite")]
    [SerializeField] private Sprite worsenedReputationSprite;

    [Tooltip("명성 변화가 없을 때 표시할 Sprite")]
    [SerializeField] private Sprite stableReputationSprite;

    [Tooltip("명성이 올랐을 때 표시할 Sprite")]
    [SerializeField] private Sprite improvedReputationSprite;

    [Tooltip("명성이 크게 올랐을 때 표시할 Sprite")]
    [SerializeField] private Sprite greatlyImprovedReputationSprite;

    [Tooltip("손님 통계 텍스트 (성공/거절/이탈)")]
    [SerializeField] private TextMeshProUGUI customerStatsText;

    [Header("Payment Breakdown")]
    [SerializeField] private TextMeshProUGUI maintenanceText;
    [SerializeField] private TextMeshProUGUI guidelinePenaltyText;
    [SerializeField] private TextMeshProUGUI guidelineViolationDetailsText;
    [SerializeField] private TextMeshProUGUI previousUnpaidText;
    [SerializeField] private TextMeshProUGUI totalPaymentDueText;
    [SerializeField] private TextMeshProUGUI paidAmountText;
    [SerializeField] private TextMeshProUGUI unpaidAmountText;
    [SerializeField] private TextMeshProUGUI gracePeriodText;

    [Header("Buttons")]
    [Tooltip("다음 단계 요청 버튼")]
    [SerializeField] private Button nextStepButton;

    private int presentedLedgerDay = -1;
    private bool hasCompletedLedgerPresentation;

    /// <summary>사용자가 일일 정산 확인을 완료하고 다음 단계 진행을 요청할 때 발생하는 이벤트</summary>
    public event Action OnNextStepRequested;

    /// <summary>가계부 양쪽 페이지의 순차 출력이 완료됐을 때 발생하는 이벤트입니다.</summary>
    public event Action OnLedgerPresentationCompleted;

    /// <summary>명성 도장이 최종 위치에 고정됐을 때 발생합니다.</summary>
    public event Action OnStampPresentationCompleted;

    private void Awake()
    {
        if (this.ledgerView != null)
        {
            this.ledgerView.OnPresentationCompleted += this.handleLedgerPresentationCompleted;
        }
        if (this.reputationStampPresenter != null)
            this.reputationStampPresenter.OnPresentationCompleted += this.handleStampPresentationCompleted;

        if (this.nextStepButton != null)
        {
            this.nextStepButton.onClick.AddListener(this.handleNextStepClicked);
        }
    }

    private void OnDestroy()
    {
        if (this.ledgerView != null)
        {
            this.ledgerView.OnPresentationCompleted -= this.handleLedgerPresentationCompleted;
        }
        if (this.reputationStampPresenter != null)
            this.reputationStampPresenter.OnPresentationCompleted -= this.handleStampPresentationCompleted;
    }

    /// <summary>
    /// 외부 정산 시스템에서 확정된 일일 정산 스냅샷을 받아 화면에 표시하고 패널을 엽니다.
    /// </summary>
    public void UpdateView(DailySettlementViewData viewData)
    {
        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(true);
        }

        if (this.dayText != null)
        {
            this.dayText.text = $"DAY {viewData.Day} SETTLEMENT";
        }

        if (this.ledgerView != null)
        {
            DailySettlementLedgerText ledgerText = DailySettlementLedgerFormatter.Format(viewData);
            if (this.presentedLedgerDay == viewData.Day && this.hasCompletedLedgerPresentation)
            {
                this.ledgerView.RefreshCompleted(ledgerText);
            }
            else
            {
                this.presentedLedgerDay = viewData.Day;
                this.hasCompletedLedgerPresentation = false;
                this.ledgerView.Present(ledgerText);
            }
        }

        if (this.reputationStampPresenter == null)
            throw new MissingReferenceException("DailySettlementPresenter: 명성 도장 Presenter가 필요합니다.");
        this.reputationStampPresenter.UpdateView(viewData.Day, viewData.FinalReputation);

        if (this.saleIncomeText != null)
        {
            this.saleIncomeText.text = $"+{viewData.SaleIncome:N0} G";
        }

        if (this.expensesText != null)
        {
            this.expensesText.text = $"-{viewData.Expenses:N0} G";
        }

        if (this.netProfitText != null)
        {
            string prefix = viewData.NetProfit >= 0 ? "+" : "";
            string colorTag = viewData.NetProfit >= 0 ? "<color=#4CAF50>" : "<color=#F44336>";
            this.netProfitText.text = $"{colorTag}<b>{prefix}{viewData.NetProfit:N0} G</b></color>";
        }

        if (this.currentBalanceText != null)
        {
            this.currentBalanceText.text = $"{viewData.CurrentBalance:N0} G";
        }

        if (this.reputationDeltaText != null)
        {
            this.reputationDeltaText.text = ReputationFeedbackFormatter.Format(viewData.ReputationDelta);
        }

        this.updateReputationFeedbackImage(viewData.ReputationDelta);

        if (this.customerStatsText != null)
        {
            this.customerStatsText.text = $"Sales: <b>{viewData.SuccessfulSales}</b>  |  Refused: <b>{viewData.RefusedCustomers}</b>  |  Departed: <b>{viewData.DepartedCustomers}</b>";
        }

        if (this.maintenanceText != null) this.maintenanceText.text = $"유지비  {viewData.MaintenanceAmount:N0} G";
        if (this.guidelinePenaltyText != null) this.guidelinePenaltyText.text = $"지침 벌금  {viewData.GuidelinePenaltyAmount:N0} G";
        if (this.previousUnpaidText != null) this.previousUnpaidText.text = $"기존 미납액  {viewData.PreviousUnpaidAmount:N0} G";
        if (this.totalPaymentDueText != null) this.totalPaymentDueText.text = $"총 납부 필요액  {viewData.TotalPaymentDue:N0} G";
        if (this.paidAmountText != null) this.paidAmountText.text = $"실제 납부액  {viewData.PaidAmount:N0} G";
        if (this.unpaidAmountText != null) this.unpaidAmountText.text = $"남은 미납액  {viewData.UnpaidAmount:N0} G";
        if (this.guidelineViolationDetailsText != null)
        {
            this.guidelineViolationDetailsText.text = viewData.GuidelineViolations.Count == 0
                ? "지침 위반 없음"
                : string.Join("\n", viewData.GuidelineViolations.Select(item =>
                    $"{item.Content} × {item.ViolationCount}  -{item.PenaltyAmount:N0} G"));
        }
        if (this.gracePeriodText != null)
        {
            this.gracePeriodText.text = formatGracePeriod(viewData);
        }
    }

    /// <summary>정산 패널 닫기</summary>
    public void Close()
    {
        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(false);
        }
    }

    /// <summary>딸 대사 완료 뒤 준비된 명성 도장 연출을 시작합니다.</summary>
    public void PresentReputationStamp() => this.reputationStampPresenter.Present();

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    public void Bind(GameObject root, TextMeshProUGUI incomeTxt, TextMeshProUGUI expTxt, TextMeshProUGUI profitTxt, TextMeshProUGUI balTxt, TextMeshProUGUI repTxt, TextMeshProUGUI statsTxt = null, TextMeshProUGUI dayTxt = null, Button nextBtn = null, Image repImage = null)
    {
        this.panelRoot = root;
        this.saleIncomeText = incomeTxt;
        this.expensesText = expTxt;
        this.netProfitText = profitTxt;
        this.currentBalanceText = balTxt;
        this.reputationDeltaText = repTxt;
        this.customerStatsText = statsTxt;
        this.dayText = dayTxt;
        this.nextStepButton = nextBtn;
        this.reputationFeedbackImage = repImage;

        if (this.nextStepButton != null)
        {
            this.nextStepButton.onClick.RemoveAllListeners();
            this.nextStepButton.onClick.AddListener(this.handleNextStepClicked);
        }
    }

    private void handleNextStepClicked()
    {
        this.Close();
        this.OnNextStepRequested?.Invoke();
    }

    /// <summary>가계부 View의 완료를 이후 딸 대사 흐름이 구독할 수 있도록 전달합니다.</summary>
    private void handleLedgerPresentationCompleted()
    {
        if (this.hasCompletedLedgerPresentation) return;
        this.hasCompletedLedgerPresentation = true;
        this.OnLedgerPresentationCompleted?.Invoke();
    }

    private void handleStampPresentationCompleted() => this.OnStampPresentationCompleted?.Invoke();

    /// <summary>미납과 유예 상태를 한 줄의 확정 표시 문구로 변환합니다.</summary>
    /// <param name="viewData">도메인 결과가 담긴 정산 스냅샷입니다.</param>
    /// <returns>완납, 유예 또는 게임오버 조건 문구입니다.</returns>
    private static string formatGracePeriod(DailySettlementViewData viewData)
    {
        if (viewData.UnpaidAmount == 0) return "납부 완료";
        if (viewData.IsGameOverConditionMet) return "유예 종료 · 게임오버 조건 성립";
        return viewData.GracePeriodEndDay.HasValue
            ? $"상환 기한 {viewData.GracePeriodEndDay.Value}일차 · {viewData.RemainingGraceDays}일 남음"
            : "미납";
    }

    /// <summary>
    /// 명성 변화량에 맞는 Sprite를 이미지 영역에 적용합니다.
    /// </summary>
    /// <param name="reputationDelta">일일 정산으로 확정된 명성 변화량입니다.</param>
    private void updateReputationFeedbackImage(int reputationDelta)
    {
        if (this.reputationFeedbackImage == null)
        {
            return;
        }

        switch (ReputationFeedbackFormatter.GetTier(reputationDelta))
        {
            case ReputationFeedbackTier.GreatlyWorsened:
                this.reputationFeedbackImage.sprite = this.greatlyWorsenedReputationSprite;
                break;
            case ReputationFeedbackTier.Worsened:
                this.reputationFeedbackImage.sprite = this.worsenedReputationSprite;
                break;
            case ReputationFeedbackTier.Stable:
                this.reputationFeedbackImage.sprite = this.stableReputationSprite;
                break;
            case ReputationFeedbackTier.Improved:
                this.reputationFeedbackImage.sprite = this.improvedReputationSprite;
                break;
            case ReputationFeedbackTier.GreatlyImproved:
                this.reputationFeedbackImage.sprite = this.greatlyImprovedReputationSprite;
                break;
        }
    }
}
