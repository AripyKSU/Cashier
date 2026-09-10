using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 09:00부터 21:00까지 실시간 영업 시간을 카운트하고 화면에 표시하는 컨트롤러입니다.
/// 일간 영업 마감 시 이벤트를 발행하여 정산 루프와 연동됩니다.
/// </summary>
public sealed class BusinessClockController : MonoBehaviour
{
    // =========================================================================
    // 1. CONSTANTS & SETTINGS
    // =========================================================================

    private const int DefaultStartHour = 9;
    private const int DefaultStartMinute = 0;
    private const int DefaultCloseHour = 21;
    private const int DefaultCloseMinute = 0;

    [Header("UI References")]
    [Tooltip("시각을 표시할 TextMeshPro 텍스트 (예: 09:00)")]
    [SerializeField] private TextMeshProUGUI clockText;

    [Header("Clock Settings")]
    [Tooltip("영업 시작 시 (기본 9시)")]
    [SerializeField, Range(0, 23)] private int startHour = DefaultStartHour;

    [Tooltip("영업 마감 시 (기본 21시)")]
    [SerializeField, Range(0, 23)] private int closeHour = DefaultCloseHour;

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
    public bool IsClosed => this.currentMinutes >= this.closeMinutes;


    /// <summary>영업 시작 시각(시)</summary>
    public int StartHour
    {
        get => this.startHour;
        set
        {
            this.startHour = value;
            this.startMinutes = value * 60f;
        }
    }

    /// <summary>영업 마감 시각(시, 기본 21시)</summary>
    public int CloseHour
    {
        get => this.closeHour;
        set
        {
            this.closeHour = value;
            this.closeMinutes = value * 60f;
        }
    }


    // =========================================================================
    // 3. PRIVATE FIELDS
    // =========================================================================

    private float currentMinutes;
    private float startMinutes;
    private float closeMinutes;
    private int lastBroadcastMinute = -1;
    private bool isRunning;
    private bool isPaused;


    // =========================================================================
    // 4. UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (this.closeHour < DefaultCloseHour)
        {
            this.closeHour = DefaultCloseHour;
        }
        this.startMinutes = this.startHour * 60f;
        this.closeMinutes = this.closeHour * 60f;
        this.currentMinutes = this.startMinutes;
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

        if (this.currentMinutes >= this.closeMinutes)
        {
            this.currentMinutes = this.closeMinutes;
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
        if (this.closeHour < DefaultCloseHour)
        {
            this.closeHour = DefaultCloseHour;
        }
        this.startMinutes = this.startHour * 60f;
        this.closeMinutes = this.closeHour * 60f;
        this.currentMinutes = this.startMinutes;
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
        this.currentMinutes = this.startMinutes;
        this.updateDisplay();
        this.notifyTimeChangeIfMinuteChanged();
    }

    /// <summary>테스트나 연출을 위해 임의의 시각으로 설정합니다.</summary>
    /// <param name="hour">설정할 시 (0~23)</param>
    /// <param name="minute">설정할 분 (0~59)</param>
    public void SetTime(int hour, int minute)
    {
        if (this.closeMinutes <= 0f)
        {
            if (this.closeHour < DefaultCloseHour)
            {
                this.closeHour = DefaultCloseHour;
            }
            this.startMinutes = this.startHour * 60f;
            this.closeMinutes = this.closeHour * 60f;
        }
        this.currentMinutes = Mathf.Clamp(hour * 60f + minute, this.startMinutes, this.closeMinutes);
        this.updateDisplay();
        this.notifyTimeChangeIfMinuteChanged();
        if (this.currentMinutes >= this.closeMinutes)
        {
            this.OnBusinessClosed?.Invoke();
        }
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
