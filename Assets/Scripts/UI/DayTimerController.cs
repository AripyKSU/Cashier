using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영업시간 카운트다운 타이머 및 상태 표시 컨트롤러 (개발자 3 담당).
/// 영업시간이 0초에 도달하면 OnBusinessDayEnded 이벤트를 발생시킵니다.
/// </summary>
public class DayTimerController : MonoBehaviour
{
    // =========================================================================
    // 1. SERIALIZED FIELDS
    // =========================================================================

    [Header("UI References")]
    [Tooltip("남은 영업시간 게이지 슬라이더")]
    [SerializeField] private Slider timeSlider;

    [Tooltip("남은 영업시간 디지털 텍스트 (예: 01:30 또는 45초)")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Settings")]
    [Tooltip("기본 영업시간 (초 단위, 기본 60초)")]
    [SerializeField] private float defaultBusinessTimeSeconds = 60f;

    [Tooltip("씬 시작 시 타이머를 자동으로 시작할지 여부")]
    [SerializeField] private bool autoStartOnAwake = false;


    // =========================================================================
    // 2. EVENTS & PROPERTIES
    // =========================================================================

    /// <summary>영업시간이 종료되었을 때 발생하는 이벤트</summary>
    public event Action OnBusinessDayEnded;

    /// <summary>현재 타이머 실행 중 여부</summary>
    public bool IsRunning => this.isRunning;

    /// <summary>남은 영업시간(초)</summary>
    public float RemainingSeconds => this.remainingSeconds;

    /// <summary>총 영업시간(초)</summary>
    public float TotalBusinessTimeSeconds => this.totalBusinessTimeSeconds;


    // =========================================================================
    // 3. PRIVATE FIELDS
    // =========================================================================

    private float totalBusinessTimeSeconds = 60f;
    private float remainingSeconds = 0f;
    private bool isRunning = false;


    // =========================================================================
    // 4. UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        if (this.autoStartOnAwake)
        {
            this.StartTimer(this.defaultBusinessTimeSeconds);
        }
        else
        {
            this.remainingSeconds = this.defaultBusinessTimeSeconds;
            this.totalBusinessTimeSeconds = this.defaultBusinessTimeSeconds;
            this.updateUI();
        }
    }

    private void Update()
    {
        if (!this.isRunning) return;

        this.remainingSeconds -= Time.deltaTime;

        if (this.remainingSeconds <= 0f)
        {
            this.remainingSeconds = 0f;
            this.isRunning = false;
            this.updateUI();

            Debug.Log("<color=yellow><b>[DayTimer] 영업시간 종료! (OnBusinessDayEnded 발생)</b></color>");
            this.OnBusinessDayEnded?.Invoke();
            return;
        }

        this.updateUI();
    }


    // =========================================================================
    // 5. PUBLIC METHODS
    // =========================================================================

    /// <summary>
    /// 지정된 영업시간(초)으로 카운트다운 타이머를 시작합니다.
    /// </summary>
    public void StartTimer(float businessTimeSeconds)
    {
        this.totalBusinessTimeSeconds = Mathf.Max(1f, businessTimeSeconds);
        this.remainingSeconds = this.totalBusinessTimeSeconds;
        this.isRunning = true;
        this.updateUI();

        Debug.Log($"<color=cyan>[DayTimer] 영업 시작: {this.totalBusinessTimeSeconds}초 카운트다운</color>");
    }

    /// <summary>타이머 일시 정지</summary>
    public void PauseTimer()
    {
        this.isRunning = false;
    }

    /// <summary>타이머 재개</summary>
    public void ResumeTimer()
    {
        if (this.remainingSeconds > 0f)
        {
            this.isRunning = true;
        }
    }

    /// <summary>타이머 정지 및 초기화</summary>
    public void StopTimer()
    {
        this.isRunning = false;
        this.remainingSeconds = 0f;
        this.updateUI();
    }


    // =========================================================================
    // 6. PRIVATE HELPER METHODS
    // =========================================================================

    private void updateUI()
    {
        if (this.timeSlider != null && this.totalBusinessTimeSeconds > 0f)
        {
            this.timeSlider.value = Mathf.Clamp01(this.remainingSeconds / this.totalBusinessTimeSeconds);
        }

        if (this.timeText != null)
        {
            int minutes = Mathf.FloorToInt(this.remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(this.remainingSeconds % 60f);
            this.timeText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
