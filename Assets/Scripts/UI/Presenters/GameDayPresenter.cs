using TMPro;
using UnityEngine;

/// <summary>
/// 3.2 GameDayPresenter
/// 현재 게임 날짜와 영업 진행 상태, 다음 정산일까지 남은 일수와 정산일 여부를 표시하는 Presenter.
/// 날짜 증가와 정산일 계산을 직접 수행하지 않으며, 전달받은 GameDayViewData를 표시 문구로 변환합니다.
/// </summary>
public class GameDayPresenter : MonoBehaviour
{
    [Header("UI Text Displays")]
    [Tooltip("현재 날짜 텍스트 (예: DAY 1)")]
    [SerializeField] private TextMeshProUGUI dayText;

    [Tooltip("정산일 안내 텍스트 (예: Tribute in 3 Days)")]
    [SerializeField] private TextMeshProUGUI settlementText;

    [Tooltip("현재 진행 단계 텍스트 (예: Operating)")]
    [SerializeField] private TextMeshProUGUI phaseText;

    /// <summary>
    /// 외부 진행 시스템에서 전달된 날짜 및 상태 스냅샷을 기반으로 UI를 갱신합니다.
    /// </summary>
    public void UpdateView(GameDayViewData viewData)
    {
        if (this.dayText != null)
        {
            this.dayText.text = $"DAY {viewData.CurrentDay}";
        }

        if (this.settlementText != null)
        {
            if (viewData.IsSettlementDay)
            {
                this.settlementText.text = "<color=#FF4444><b>[TODAY: TRIBUTE DAY]</b></color>";
            }
            else
            {
                this.settlementText.text = $"Tribute in {viewData.DaysUntilSettlement} Days";
            }
        }

        if (this.phaseText != null)
        {
            this.phaseText.text = this.formatPhaseText(viewData.Phase);
        }
    }

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    public void Bind(TextMeshProUGUI dayTextDisplay, TextMeshProUGUI settlementTextDisplay = null, TextMeshProUGUI phaseTextDisplay = null)
    {
        this.dayText = dayTextDisplay;
        this.settlementText = settlementTextDisplay;
        this.phaseText = phaseTextDisplay;
    }

    private string formatPhaseText(GameDayPhase phase)
    {
        return phase switch
        {
            GameDayPhase.PreOpen => "Pre-Open",
            GameDayPhase.PriceGuide => "Catalog Briefing",
            GameDayPhase.Operating => "Shop Open",
            GameDayPhase.TradingResult => "Trade Result",
            GameDayPhase.DailySettlement => "Daily Settlement",
            GameDayPhase.Tribute => "Tribute Due",
            _ => phase.ToString()
        };
    }
}
