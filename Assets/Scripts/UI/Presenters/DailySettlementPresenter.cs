using System;
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

    [Tooltip("평판 변화량 텍스트")]
    [SerializeField] private TextMeshProUGUI reputationDeltaText;

    [Tooltip("손님 통계 텍스트 (성공/거절/이탈)")]
    [SerializeField] private TextMeshProUGUI customerStatsText;

    [Header("Buttons")]
    [Tooltip("다음 단계 요청 버튼")]
    [SerializeField] private Button nextStepButton;

    /// <summary>사용자가 일일 정산 확인을 완료하고 다음 단계 진행을 요청할 때 발생하는 이벤트</summary>
    public event Action OnNextStepRequested;

    private void Awake()
    {
        if (this.nextStepButton != null)
        {
            this.nextStepButton.onClick.AddListener(this.handleNextStepClicked);
        }
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
            string prefix = viewData.ReputationDelta >= 0 ? "+" : "";
            string colorTag = viewData.ReputationDelta >= 0 ? "<color=#FFD700>" : "<color=#FF5722>";
            this.reputationDeltaText.text = $"{colorTag}Reputation {prefix}{viewData.ReputationDelta}</color>";
        }

        if (this.customerStatsText != null)
        {
            this.customerStatsText.text = $"Sales: <b>{viewData.SuccessfulSales}</b>  |  Refused: <b>{viewData.RefusedCustomers}</b>  |  Departed: <b>{viewData.DepartedCustomers}</b>";
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

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    public void Bind(GameObject root, TextMeshProUGUI incomeTxt, TextMeshProUGUI expTxt, TextMeshProUGUI profitTxt, TextMeshProUGUI balTxt, TextMeshProUGUI repTxt, TextMeshProUGUI statsTxt = null, TextMeshProUGUI dayTxt = null, Button nextBtn = null)
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
}
