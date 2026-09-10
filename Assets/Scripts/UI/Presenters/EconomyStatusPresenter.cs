using TMPro;
using UnityEngine;

/// <summary>
/// 3.1 EconomyStatusPresenter
/// 평상시 화면에 현재 재정 상태(보유금, 일일 판매 수입)를 표시하는 Presenter.
/// 보유금과 일일 매출을 직접 계산하거나 변경하지 않으며, 전달받은 ViewData 스냅샷만 렌더링합니다.
/// </summary>
public class EconomyStatusPresenter : MonoBehaviour
{
    [Header("UI Text Displays")]
    [Tooltip("현재 플레이어 보유금 텍스트")]
    [SerializeField] private TextMeshProUGUI balanceText;

    [Tooltip("현재 영업일 누적 판매 수입 텍스트")]
    [SerializeField] private TextMeshProUGUI dailyIncomeText;

    /// <summary>
    /// 외부 시스템에서 전달된 재정 상태 스냅샷을 기반으로 UI를 갱신합니다.
    /// </summary>
    /// <param name="viewData">재정 상태 스냅샷</param>
    public void UpdateView(EconomyStatusViewData viewData)
    {
        if (this.balanceText != null)
        {
            this.balanceText.text = $"{viewData.CurrentBalance:N0} G";
        }

        if (this.dailyIncomeText != null)
        {
            this.dailyIncomeText.text = $"+{viewData.DailySaleIncome:N0} G";
        }
    }

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    public void Bind(TextMeshProUGUI balanceTextDisplay, TextMeshProUGUI dailyIncomeTextDisplay = null)
    {
        this.balanceText = balanceTextDisplay;
        this.dailyIncomeText = dailyIncomeTextDisplay;
    }
}
