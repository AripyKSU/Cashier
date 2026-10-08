using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업대 오른쪽 테두리 가운데의 "지침" 버튼으로 여닫는 오늘의 지침 쪽지.
/// 누르면 작업대 안쪽으로 미끄러져 나오고, 다시 누르면 테두리 뒤로 들어갑니다. 영업 시간은 그대로 흐릅니다.
/// </summary>
public sealed class GuidelinePopupPresenter : MonoBehaviour
{
    [Tooltip("지침 쪽지를 여닫는 버튼")]
    [SerializeField] private Button toggleButton;

    [Tooltip("미끄러져 나오는 지침 쪽지")]
    [SerializeField] private RectTransform sheet;

    [Tooltip("지침 본문 텍스트")]
    [SerializeField] private TextMeshProUGUI bodyText;

    [Tooltip("오늘 지침을 가진 영업 전 지침서")]
    [SerializeField] private PreOpenPanelPresenter preOpenPanel;

    [Tooltip("쪽지가 나와 있을 때의 위치(부모 기준)")]
    [SerializeField] private Vector2 openPosition;

    [Tooltip("쪽지가 들어가 있을 때의 위치(부모 기준)")]
    [SerializeField] private Vector2 closedPosition;

    [Tooltip("나오고 들어가는 시간(초)")]
    [SerializeField, Min(0.05f)] private float slideSeconds = 0.25f;

    private bool isOpen;
    private Tween slideTween;

    private void Awake()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(toggle);
        closeImmediately();
    }

    private void OnDisable()
    {
        // 작업대가 닫히면 쪽지도 들어간 상태로 되돌린다.
        closeImmediately();
    }

    private void OnDestroy()
    {
        slideTween?.Kill();
        if (toggleButton != null) toggleButton.onClick.RemoveListener(toggle);
    }

    /// <summary>쪽지를 열거나 닫습니다. 열 때 오늘 지침 문구를 채웁니다.</summary>
    private void toggle()
    {
        if (sheet == null) return;
        isOpen = !isOpen;
        if (isOpen)
        {
            if (bodyText != null && preOpenPanel != null) bodyText.text = preOpenPanel.CurrentGuidelineText;
            sheet.gameObject.SetActive(true);
        }

        slideTween?.Kill();
        bool opening = isOpen;
        slideTween = DOTween.To(() => sheet.anchoredPosition, value => sheet.anchoredPosition = value,
                opening ? openPosition : closedPosition, slideSeconds)
            .SetEase(opening ? Ease.OutCubic : Ease.InCubic)
            .SetUpdate(true)
            .OnComplete(() => { if (!opening) sheet.gameObject.SetActive(false); });
    }

    /// <summary>애니메이션 없이 쪽지를 닫힌 상태로 둡니다.</summary>
    private void closeImmediately()
    {
        slideTween?.Kill();
        isOpen = false;
        if (sheet == null) return;
        sheet.anchoredPosition = closedPosition;
        sheet.gameObject.SetActive(false);
    }
}
