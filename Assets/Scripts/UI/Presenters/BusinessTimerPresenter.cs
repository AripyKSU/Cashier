using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3.3 BusinessTimerPresenter
/// 현재 영업의 남은 시간과 진행률을 표시하는 Presenter.
/// 시간 감소와 영업 종료 판정은 수행하지 않습니다.
/// </summary>
public class BusinessTimerPresenter : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("남은 시간 게이지 슬라이더 (0~1)")]
    [SerializeField] private Slider timeSlider;

    [Tooltip("남은 시간 텍스트 (예: 01:25)")]
    [SerializeField] private TextMeshProUGUI timeText;

    /// <summary>
    /// 외부 영업시간 진행 시스템에서 전달된 시간 스냅샷을 기반으로 UI를 갱신합니다.
    /// </summary>
    /// <param name="viewData">남은 시간과 입력 가능 상태를 담은 스냅샷입니다.</param>
    public void UpdateView(BusinessTimerViewData viewData)
    {
        if (this.timeSlider != null)
        {
            this.timeSlider.value = Mathf.Clamp01(viewData.NormalizedTime);
        }

        if (this.timeText != null)
        {
            int totalSec = Mathf.Max(0, Mathf.CeilToInt(viewData.RemainingSeconds));
            int min = totalSec / 60;
            int sec = totalSec % 60;
            this.timeText.text = $"{min:00}:{sec:00}";
        }

    }

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    /// <param name="slider">시간 진행률을 표시하는 슬라이더입니다.</param>
    /// <param name="textDisplay">남은 시간을 표시하는 텍스트입니다.</param>
    public void Bind(Slider slider, TextMeshProUGUI textDisplay)
    {
        this.timeSlider = slider;
        this.timeText = textDisplay;
    }
}
