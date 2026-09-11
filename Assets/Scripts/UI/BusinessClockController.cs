using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 공통 영업 시각을 표시합니다. 통합 게임은 DisplayTime으로 DayProgress를 따르고, 독립 미리보기만 자체 시계를 사용합니다.
/// </summary>
public sealed class BusinessClockController : MonoBehaviour
{
    // =========================================================================
    // 1. CONSTANTS & SETTINGS
    // =========================================================================

    [Header("UI References")]
    [Tooltip("시각을 표시할 TextMeshPro 텍스트 (예: 09:00)")]
    [SerializeField] private TextMeshProUGUI clockText;

    [Header("Clock Settings")]
    [Tooltip("현실 1초당 흐르는 게임 시간(분). 기본 10분/초 -> 720분(12시간) 영업에 72초 소요")]
    [SerializeField, Min(0.1f)] private float gameMinutesPerRealSecond = 10f;

    [Tooltip("Start 시 자동으로 시계를 시작할지 여부")]
    [SerializeField] private bool autoStartOnStart = true;


    // =========================================================================
    // 2. EVENTS & PROPERTIES
    // =========================================================================

    /// <summary>매 게임 분이 바뀔 때 호출되는 이벤트 (hour, minute)</summary>
    public event Action<int, int> OnTimeChanged;

    /// <summary>21:00 마감 시각에 도달했을 때 호출되는 이벤트</summary>
    public event Action OnBusinessClosed;

    /// <summary>현재 시각(분 단위 누적, 예: 9시 30분 = 570)</summary>
    public float CurrentBusinessMinutes => this.currentMinutes;

    /// <summary>현재 시간(시)</summary>
    public int CurrentHour => Mathf.FloorToInt(this.currentMinutes) / 60;

    /// <summary>현재 시간(분)</summary>
    public int CurrentMinute => Mathf.FloorToInt(this.currentMinutes) % 60;

    /// <summary>시계가 현재 동작 중인지 여부</summary>
    public bool IsRunning => this.isRunning;

    /// <summary>영업 마감 시각에 도달했는지 여부</summary>
    public bool IsClosed => this.currentMinutes >= BusinessHours.CloseMinutes;


    /// <summary>공통 영업 시작 시각. 기존 setter는 같은 공통값만 허용한다.</summary>
    /// <exception cref="ArgumentOutOfRangeException">공통 영업 시작 시각과 다른 값.</exception>
    public int StartHour
    {
        get => BusinessHours.OpenHour;
        set
        {
            if (value != BusinessHours.OpenHour) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    /// <summary>공통 영업 마감 시각. 기존 setter는 같은 공통값만 허용한다.</summary>
    /// <exception cref="ArgumentOutOfRangeException">공통 영업 마감 시각과 다른 값.</exception>
    public int CloseHour
    {
        get => BusinessHours.CloseHour;
        set
        {
            if (value != BusinessHours.CloseHour) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }


    // =========================================================================
    // 3. PRIVATE FIELDS
    // =========================================================================

    private float currentMinutes = BusinessHours.OpenMinutes;
    private int lastBroadcastMinute = -1;
    private bool isRunning;
    private bool isPaused;


    // =========================================================================
    // 4. UNITY LIFECYCLE
    // =========================================================================

    /// <summary>공통 시작 시각으로 최초 표시를 준비한다.</summary>
    private void Awake()
    {
        this.currentMinutes = BusinessHours.OpenMinutes;
        this.updateDisplay();
    }

    private void Start()
    {
        if (this.autoStartOnStart)
        {
            this.StartClock();
        }
    }

    private void Update()
    {
        if (!this.isRunning || this.isPaused)
        {
            return;
        }

        float deltaSeconds = Time.unscaledDeltaTime;
        if (deltaSeconds <= 0f)
        {
            return;
        }

        this.currentMinutes += deltaSeconds * this.gameMinutesPerRealSecond;

        if (this.currentMinutes >= BusinessHours.CloseMinutes)
        {
            this.currentMinutes = BusinessHours.CloseMinutes;
            this.isRunning = false;
            this.updateDisplay();
            this.notifyTimeChangeIfMinuteChanged();
            Debug.Log("<color=yellow><b>[BusinessClock] 21:00 영업 종료 시각에 도달했습니다.</b></color>");
            this.OnBusinessClosed?.Invoke();
            return;
        }

        this.updateDisplay();
        this.notifyTimeChangeIfMinuteChanged();
    }


    // =========================================================================
    // 5. PUBLIC API
    // =========================================================================

    /// <summary>시계를 09:00으로 초기화하고 카운트를 시작합니다.</summary>
    public void StartClock()
    {
        this.currentMinutes = BusinessHours.OpenMinutes;
        this.isRunning = true;
        this.isPaused = false;
        this.lastBroadcastMinute = -1;
        this.updateDisplay();
        this.notifyTimeChangeIfMinuteChanged();
    }

    /// <summary>일시정지 상태를 토글하거나 지정합니다.</summary>
    /// <param name="paused">일시정지 여부입니다.</param>
    public void SetPaused(bool paused)
    {
        this.isPaused = paused;
    }

    /// <summary>시계 동작을 일시 중단합니다.</summary>
    public void StopClock()
    {
        this.isRunning = false;
    }

    /// <summary>시계를 영업 시작 시각(기본 09:00)으로 되돌립니다.</summary>
    public void ResetToStart()
    {
        this.currentMinutes = BusinessHours.OpenMinutes;
        this.updateDisplay();
        this.notifyTimeChangeIfMinuteChanged();
    }

    /// <summary>테스트나 연출을 위해 임의의 시각으로 설정합니다.</summary>
    /// <param name="hour">설정할 시 (0~23)</param>
    /// <param name="minute">설정할 분 (0~59)</param>
    public void SetTime(int hour, int minute)
    {
        bool wasClosed = this.IsClosed;
        this.currentMinutes = Mathf.Clamp(hour * 60f + minute, BusinessHours.OpenMinutes, BusinessHours.CloseMinutes);
        this.updateDisplay();
        this.notifyTimeChangeIfMinuteChanged();
        if (!wasClosed && this.IsClosed)
        {
            this.isRunning = false;
            this.OnBusinessClosed?.Invoke();
        }
    }

    /// <summary>진행 모델의 시각만 표시한다. 자체 진행과 자동 시작을 끄고 마감 이벤트를 발행하지 않는다.</summary>
    /// <param name="minutes">DayProgress 경과 비율로 계산한 누적 게임 분. 공통 영업 범위로 제한한다.</param>
    public void DisplayTime(int minutes)
    {
        this.autoStartOnStart = false;
        this.isRunning = false;
        this.currentMinutes = Mathf.Clamp(minutes, BusinessHours.OpenMinutes, BusinessHours.CloseMinutes);
        this.updateDisplay();
        this.notifyTimeChangeIfMinuteChanged();
    }


    // =========================================================================
    // 6. PRIVATE HELPERS
    // =========================================================================

    /// <summary>화면 텍스트를 "HH:mm" 형식으로 갱신합니다.</summary>
    private void updateDisplay()
    {
        if (this.clockText == null)
        {
            return;
        }

        int totalMinutes = Mathf.FloorToInt(this.currentMinutes);
        int hour = totalMinutes / 60;
        int minute = totalMinutes % 60;
        this.clockText.text = $"{hour:00}:{minute:00}";
    }

    /// <summary>정수 분 단위가 변경되었을 때만 이벤트를 발행합니다.</summary>
    private void notifyTimeChangeIfMinuteChanged()
    {
        int currentFloorMinute = Mathf.FloorToInt(this.currentMinutes);
        if (currentFloorMinute != this.lastBroadcastMinute)
        {
            this.lastBroadcastMinute = currentFloorMinute;
            int hour = currentFloorMinute / 60;
            int minute = currentFloorMinute % 60;
            this.OnTimeChanged?.Invoke(hour, minute);
        }
    }
}
