using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지침을 어긴 거래 직후 화면 가운데에 내려오는 "거래 위반 통지서" 종이.
/// 어떤 지침을 어겼는지, 오늘 몇 번째인지, 정산 때 빠질 벌금 비율을 보여 줍니다. 클릭하면 닫힙니다.
/// 진행 판단은 하지 않고 표시만 합니다.
/// </summary>
public sealed class GuidelineViolationNoticePresenter : MonoBehaviour
{
    [Tooltip("통지서 전체(입력 차단 포함)")]
    [SerializeField] private CanvasGroup rootGroup;

    [Tooltip("통지서 종이")]
    [SerializeField] private RectTransform sheet;

    [Tooltip("위반 내용 텍스트")]
    [SerializeField] private TextMeshProUGUI bodyText;

    [Tooltip("위반 횟수 도장 텍스트")]
    [SerializeField] private TextMeshProUGUI stampText;

    private Action closed;
    private float shownAt;
    private bool closeOnClick;

    /// <summary>통지서가 떠 있는지 여부입니다.</summary>
    public bool IsShowing { get; private set; }

    /// <summary>튜토리얼 강조에 쓸 종이 영역입니다.</summary>
    public RectTransform Sheet => sheet;

    private void Awake()
    {
        hide();
    }

    private void Update()
    {
        if (!IsShowing || !closeOnClick || Time.unscaledTime - shownAt < .3f) return;
#if ENABLE_INPUT_SYSTEM
        bool clicked = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
        bool clicked = Input.GetMouseButtonDown(0);
#endif
        if (clicked) Close();
    }

    /// <summary>통지서를 띄웁니다.</summary>
    /// <param name="guidelineText">어긴 지침 문장입니다.</param>
    /// <param name="todayCount">오늘 누적 위반 횟수입니다.</param>
    /// <param name="percentPerViolation">위반 1건당 벌금 비율(%)입니다.</param>
    /// <param name="closeOnClick">true면 클릭으로 닫습니다. false면 <see cref="Close"/>를 불러야 닫힙니다.</param>
    /// <param name="onClosed">닫힐 때 한 번 호출됩니다.</param>
    public void Show(string guidelineText, int todayCount, int percentPerViolation, bool closeOnClick, Action onClosed)
    {
        int percent = Mathf.Min(100, todayCount * percentPerViolation);
        bodyText.text =
            $"<b>위반 지침</b>\n{guidelineText}\n\n" +
            $"<b>오늘 위반</b>  {todayCount}건\n" +
            $"<b>벌금</b>  오늘 판매 수입의 {percent}%\n" +
            "<size=85%>(정산 때 소지금에서 차감)</size>";
        stampText.text = $"위반 {todayCount}";
        closed = onClosed;
        this.closeOnClick = closeOnClick;
        shownAt = Time.unscaledTime;
        IsShowing = true;
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;
    }

    /// <summary>통지서를 닫고 닫힘 콜백을 부릅니다.</summary>
    public void Close()
    {
        if (!IsShowing) return;
        Action callback = closed;
        hide();
        callback?.Invoke();
    }

    private void hide()
    {
        IsShowing = false;
        closed = null;
        if (rootGroup == null) return;
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
    }
}
