using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3.3 BusinessTimerPresenter
/// 현재 영업의 남은 시간과 진행률, 일시정지 상태를 표시하는 Presenter.
/// 시간 감소, 일시정지 처리와 영업 종료 판정을 수행하지 않으며, 사용자 요청 이벤트만 외부에 전달합니다.
/// </summary>
public class BusinessTimerPresenter : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("남은 시간 게이지 슬라이더 (0~1)")]
    [SerializeField] private Slider timeSlider;

    [Tooltip("남은 시간 텍스트 (예: 01:25)")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Tooltip("일시정지 요청 버튼")]
    [SerializeField] private Button pauseButton;

    [Tooltip("재개 요청 버튼")]
    [SerializeField] private Button resumeButton;

    [Tooltip("일시정지 중 표시 오브젝트")]
    [SerializeField] private GameObject pauseIndicator;

    /// <summary>사용자가 일시정지 버튼을 눌렀을 때 외부로 전달하는 요청 이벤트</summary>
    public event Action OnPauseRequested;

    /// <summary>사용자가 재개 버튼을 눌렀을 때 외부로 전달하는 요청 이벤트</summary>
    public event Action OnResumeRequested;

    private void Awake()
    {
        if (this.pauseButton != null)
        {
            this.pauseButton.onClick.AddListener(this.handlePauseClicked);
        }

        if (this.resumeButton != null)
        {
            this.resumeButton.onClick.AddListener(this.handleResumeClicked);
        }
    }

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

        if (this.pauseIndicator != null)
        {
            this.pauseIndicator.SetActive(viewData.IsPaused);
        }

        if (this.pauseButton != null && this.resumeButton != null)
        {
            this.pauseButton.gameObject.SetActive(viewData.CanPause);
            this.resumeButton.gameObject.SetActive(viewData.CanResume);
        }
    }

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    /// <param name="slider">시간 진행률을 표시하는 슬라이더입니다.</param>
    /// <param name="textDisplay">남은 시간을 표시하는 텍스트입니다.</param>
    /// <param name="pauseBtn">일시정지 요청 버튼입니다.</param>
    /// <param name="resumeBtn">재개 요청 버튼입니다.</param>
    /// <param name="pauseInd">일시정지 상태 표시 오브젝트입니다.</param>
    public void Bind(Slider slider, TextMeshProUGUI textDisplay, Button pauseBtn = null, Button resumeBtn = null, GameObject pauseInd = null)
    {
        this.timeSlider = slider;
        this.timeText = textDisplay;
        this.pauseButton = pauseBtn;
        this.resumeButton = resumeBtn;
        this.pauseIndicator = pauseInd;

        if (this.pauseButton != null)
        {
            this.pauseButton.onClick.RemoveAllListeners();
            this.pauseButton.onClick.AddListener(this.handlePauseClicked);
        }

        if (this.resumeButton != null)
        {
            this.resumeButton.onClick.RemoveAllListeners();
            this.resumeButton.onClick.AddListener(this.handleResumeClicked);
        }
    }

    private void handlePauseClicked()
    {
        this.OnPauseRequested?.Invoke();
    }

    private void handleResumeClicked()
    {
        this.OnResumeRequested?.Invoke();
    }
}
