using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 영업 중 화면 좌상단의 녹슨 철판 명패에 남은 날짜(D-N), 현재 시각, 보유금을 표시하는 Presenter.
/// 보유금이 바뀌면 바뀐 만큼을 +/− 문구로 띄워 거래마다 얼마가 들어오고 나갔는지 보여 줍니다.
/// 돈과 시간을 계산하지 않고 전달받은 값만 표시합니다.
/// </summary>
public sealed class ShopStatusHudPresenter : MonoBehaviour
{
    /// <summary>수입 문구 색입니다. 녹슨 화면에서 잘 보이는 탁한 초록입니다.</summary>
    private static readonly Color IncomeColor = new Color(0.62f, 0.86f, 0.46f);
    /// <summary>지출 문구 색입니다.</summary>
    private static readonly Color ExpenseColor = new Color(0.93f, 0.42f, 0.32f);

    [Tooltip("명패 전체 표시 여부를 제어하는 CanvasGroup")]
    [SerializeField] private CanvasGroup rootGroup;

    [Tooltip("남은 날짜 텍스트 (예: D-20)")]
    [SerializeField] private TextMeshProUGUI dayText;

    [Tooltip("현재 영업 시각 텍스트 (예: 09:40)")]
    [SerializeField] private TextMeshProUGUI clockText;

    [Tooltip("현재 보유금 텍스트 (예: 12,300원)")]
    [SerializeField] private TextMeshProUGUI balanceText;

    [Tooltip("보유금 앞에 붙일 말 (예: 소지금). 비우면 금액만 씁니다.")]
    [SerializeField] private string balanceLabel = string.Empty;

    [Tooltip("보유금 변화량을 띄우는 텍스트. 평소에는 투명합니다.")]
    [SerializeField] private TextMeshProUGUI deltaText;

    [Tooltip("왼쪽 위 철판 명패 영역. 튜토리얼에서 시계를 가리킬 때 강조합니다.")]
    [SerializeField] private RectTransform plateRect;

    [Tooltip("손님 성향 설명표. 명패가 숨겨질 때 함께 닫습니다.")]
    [SerializeField] private CustomerTraitGuidePresenter traitGuide;

    [Tooltip("변화량 문구가 위로 떠오르는 거리(UI 픽셀)")]
    [SerializeField, Min(0f)] private float deltaRisePixels = 16f;

    [Tooltip("변화량 문구가 보이는 시간(초)")]
    [SerializeField, Min(0.1f)] private float deltaSeconds = 1.6f;

    private bool hasBalance;
    private long lastBalance;
    private int lastMinutes = -1;
    private Vector2 deltaRestPosition;
    private Sequence deltaSequence;
    private Tween balancePunch;

    /// <summary>튜토리얼 강조 대상. 철판 명패가 없으면 이 오브젝트 자체입니다.</summary>
    public RectTransform HighlightTarget => this.plateRect != null ? this.plateRect : (RectTransform)this.transform;

    private void Awake()
    {
        if (this.deltaText != null)
        {
            this.deltaRestPosition = this.deltaText.rectTransform.anchoredPosition;
            this.deltaText.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        this.deltaSequence?.Kill();
        this.balancePunch?.Kill();
    }

    /// <summary>
    /// 명패를 보이거나 숨깁니다. 숨겨도 마지막 보유금은 기억해 다음 표시 때 변화량을 이어서 계산합니다.
    /// </summary>
    /// <param name="isVisible">영업 화면(정면·작업대·거래 결과)일 때 true입니다.</param>
    public void SetVisible(bool isVisible)
    {
        if (this.rootGroup == null) return;
        this.rootGroup.alpha = isVisible ? 1f : 0f;
        // 명패의 이미지·글자는 클릭을 받지 않고, 안의 "손님 성향" 버튼만 영업 중에 눌린다.
        this.rootGroup.blocksRaycasts = isVisible;
        this.rootGroup.interactable = isVisible;
        if (!isVisible && this.traitGuide != null) this.traitGuide.Close();
    }

    /// <summary>남은 날짜 문구를 갱신합니다.</summary>
    /// <param name="displayDay">1부터 시작하는 표시 일차입니다.</param>
    public void SetDay(int displayDay)
    {
        if (this.dayText == null) return;
        this.dayText.text = DayCountdownLabel.Format(displayDay);
        // 마지막 날은 붉게 강조해 압박감을 줍니다.
        this.dayText.color = DayCountdownLabel.IsFinalDay(displayDay) ? ExpenseColor : Color.white;
    }

    /// <summary>현재 영업 시각을 갱신합니다. 같은 분이면 문자열을 다시 만들지 않습니다.</summary>
    /// <param name="minutesOfDay">자정 기준 분입니다 (예: 9시 = 540).</param>
    public void SetClock(int minutesOfDay)
    {
        if (this.clockText == null || minutesOfDay == this.lastMinutes) return;
        this.lastMinutes = minutesOfDay;
        this.clockText.text = $"{minutesOfDay / 60:00}:{minutesOfDay % 60:00}";
    }

    /// <summary>
    /// 보유금을 갱신하고 이전 값과 다르면 변화량을 띄웁니다. 첫 호출은 기준값만 기록합니다.
    /// </summary>
    /// <param name="balance">현재 보유금입니다.</param>
    /// <param name="showDelta">변화량 연출을 보여 줄지 여부입니다. 하루 시작처럼 연출이 어색한 경우 false입니다.</param>
    public void SetBalance(long balance, bool showDelta)
    {
        if (this.balanceText != null) this.balanceText.text = $"{this.balanceLabel}{balance:N0}원";
        if (this.hasBalance && balance != this.lastBalance && showDelta)
            this.playDelta(balance - this.lastBalance);
        this.lastBalance = balance;
        this.hasBalance = true;
    }

    /// <summary>변화량 문구를 띄우고 보유금 글자를 살짝 튀게 합니다.</summary>
    /// <param name="delta">보유금 변화량입니다.</param>
    private void playDelta(long delta)
    {
        if (this.deltaText == null) return;
        bool isIncome = delta > 0;
        this.deltaText.text = isIncome ? $"+{delta:N0}원" : $"−{-delta:N0}원";
        this.deltaText.color = isIncome ? IncomeColor : ExpenseColor;

        this.deltaSequence?.Kill();
        RectTransform rect = this.deltaText.rectTransform;
        rect.anchoredPosition = this.deltaRestPosition;
        this.deltaText.alpha = 1f;
        rect.localScale = Vector3.one * 1.35f;
        // 크게 나타났다가 제자리 크기로 줄고, 위로 떠오르며 사라집니다.
        this.deltaSequence = DOTween.Sequence()
            .Append(rect.DOScale(1f, 0.18f).SetEase(Ease.OutBack))
            .Join(DOTween.To(() => rect.anchoredPosition, value => rect.anchoredPosition = value,
                this.deltaRestPosition + Vector2.up * this.deltaRisePixels, this.deltaSeconds).SetEase(Ease.OutCubic))
            .Insert(this.deltaSeconds * 0.55f, DOTween.To(() => this.deltaText.alpha, value => this.deltaText.alpha = value,
                0f, this.deltaSeconds * 0.45f))
            .SetUpdate(true);

        if (this.balanceText != null)
        {
            this.balancePunch?.Kill(true);
            this.balanceText.rectTransform.localScale = Vector3.one;
            this.balancePunch = this.balanceText.rectTransform.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6, 0.6f).SetUpdate(true);
        }
    }
}
