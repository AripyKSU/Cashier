using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루 영업 종료 후 일일 정산 및 경영 피드백 화면 패널 (개발자 3 담당).
/// 총 매출, 재료비/유지비, 순이익, 명성 변화 피드백을 표시합니다.
/// </summary>
public class DailyResultPanel : MonoBehaviour
{
    // =========================================================================
    // 1. SERIALIZED FIELDS
    // =========================================================================

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Text Displays")]
    [Tooltip("총 매출액 텍스트")]
    [SerializeField] private TextMeshProUGUI totalSalesText;

    [Tooltip("총 지출액(재료비/유지비) 텍스트")]
    [SerializeField] private TextMeshProUGUI totalExpensesText;

    [Tooltip("순이익 (매출 - 지출) 텍스트")]
    [SerializeField] private TextMeshProUGUI netProfitText;

    [Tooltip("가게 명성 변화 피드백 텍스트")]
    [SerializeField] private TextMeshProUGUI reputationChangeText;

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

    [Header("Buttons")]
    [Tooltip("다음 날로 진행 버튼")]
    [SerializeField] private Button nextDayButton;


    // =========================================================================
    // 2. EVENTS
    // =========================================================================

    /// <summary>'다음 날로 진행' 버튼 클릭 시 발생하는 이벤트</summary>
    public event Action OnNextDayClicked;


    // =========================================================================
    // 3. UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (this.nextDayButton != null)
        {
            this.nextDayButton.onClick.AddListener(this.onNextDayButtonClicked);
        }
    }


    // =========================================================================
    // 4. PUBLIC METHODS
    // =========================================================================

    /// <summary>
    /// 계약 규격의 DailySettlementViewData를 받아 화면에 렌더링하고 패널을 엽니다.
    /// </summary>
    public void ShowResult(DailySettlementViewData viewData)
    {
        this.ShowResult(viewData.SaleIncome, viewData.Expenses, viewData.ReputationDelta);
    }

    /// <summary>
    /// 정산 데이터를 받아 화면에 렌더링하고 패널을 엽니다.
    /// </summary>
    /// <param name="totalSales">오늘 총 매출</param>
    /// <param name="totalExpenses">오늘 총 지출(재료비, 인건비 등)</param>
    /// <param name="reputationChange">내부 판정에 사용되는 명성 변화량입니다. 화면에는 숫자로 표시하지 않습니다.</param>
    public void ShowResult(long totalSales, long totalExpenses, int reputationChange)
    {
        long netProfit = totalSales - totalExpenses;

        if (this.totalSalesText != null)
        {
            this.totalSalesText.text = $"+{totalSales:N0} G";
        }

        if (this.totalExpensesText != null)
        {
            this.totalExpensesText.text = $"-{totalExpenses:N0} G";
        }

        if (this.netProfitText != null)
        {
            string prefix = netProfit >= 0 ? "+" : "";
            string colorTag = netProfit >= 0 ? "<color=#4CAF50>" : "<color=#F44336>";
            this.netProfitText.text = $"{colorTag}<b>{prefix}{netProfit:N0} G</b></color>";
        }

        if (this.reputationChangeText != null)
        {
            this.reputationChangeText.text = ReputationFeedbackFormatter.Format(reputationChange);
        }

        this.updateReputationFeedbackImage(reputationChange);

        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(true);
        }
        else
        {
            this.gameObject.SetActive(true);
        }

        Debug.Log($"<color=green><b>[DailyResult] 정산창 오픈: 매출 {totalSales:N0}원, 순익 {netProfit:N0}원</b></color>");
    }

    /// <summary>정산창 닫기</summary>
    public void Close()
    {
        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(false);
        }
        else
        {
            this.gameObject.SetActive(false);
        }
    }


    // =========================================================================
    // 5. PRIVATE HANDLERS
    // =========================================================================

    private void onNextDayButtonClicked()
    {
        this.Close();
        this.OnNextDayClicked?.Invoke();
    }

    /// <summary>
    /// 명성 변화량에 맞는 Sprite를 이미지 영역에 적용합니다.
    /// </summary>
    /// <param name="reputationChange">일일 정산으로 확정된 명성 변화량입니다.</param>
    private void updateReputationFeedbackImage(int reputationChange)
    {
        if (this.reputationFeedbackImage == null)
        {
            return;
        }

        switch (ReputationFeedbackFormatter.GetTier(reputationChange))
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
