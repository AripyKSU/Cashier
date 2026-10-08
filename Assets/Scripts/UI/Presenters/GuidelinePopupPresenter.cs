using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업대 오른쪽 아래 "지침" 버튼으로 여닫는 오늘의 지침서.
/// 열 때마다 영업 전 일일 지침서(지침과 가격표)를 그대로 복사해 보여 주고, 색만 영업 화면에 맞게 살짝 어둡게 한다.
/// 누르면 작업대 안쪽으로 미끄러져 나오고, 다시 누르면 화면 오른쪽 밖으로 들어갑니다. 영업 시간은 그대로 흐릅니다.
/// </summary>
public sealed class GuidelinePopupPresenter : MonoBehaviour
{
    [Tooltip("지침서를 여닫는 버튼")]
    [SerializeField] private Button toggleButton;

    [Tooltip("미끄러져 나오는 지침서 자리")]
    [SerializeField] private RectTransform sheet;

    [Tooltip("오늘 지침과 가격표를 가진 영업 전 지침서")]
    [SerializeField] private PreOpenPanelPresenter preOpenPanel;

    [Tooltip("지침서가 나와 있을 때의 위치(부모 기준)")]
    [SerializeField] private Vector2 openPosition;

    [Tooltip("지침서 높이. 원본 지침서를 이 높이에 맞춰 줄여 보여 줍니다.")]
    [SerializeField, Min(100f)] private float sheetHeight = 600f;

    [Tooltip("영업 화면에 맞춘 종이 색(곱하기)")]
    [SerializeField] private Color sceneTint = new Color(.84f, .78f, .7f, 1f);

    [Tooltip("나오고 들어가는 시간(초)")]
    [SerializeField, Min(0.05f)] private float slideSeconds = 0.25f;

    private bool isOpen;
    private Tween slideTween;
    private GameObject copy;
    private Vector2 closedPosition;

    private void Awake()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(toggle);
        closeImmediately();
    }

    private void OnDisable()
    {
        // 작업대가 닫히면 지침서도 들어간 상태로 되돌린다.
        closeImmediately();
    }

    private void OnDestroy()
    {
        slideTween?.Kill();
        if (toggleButton != null) toggleButton.onClick.RemoveListener(toggle);
    }

    /// <summary>지침서를 열거나 닫습니다. 열 때 오늘 지침서를 새로 복사합니다.</summary>
    private void toggle()
    {
        if (sheet == null) return;
        isOpen = !isOpen;
        if (isOpen)
        {
            rebuildCopy();
            sheet.anchoredPosition = closedPosition;
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

    /// <summary>
    /// 영업 전 지침서를 통째로 복사해 지침서 자리에 맞게 줄입니다. 영업 시작·테스트 버튼은 숨기고 색만 장면에 맞춥니다.
    /// </summary>
    private void rebuildCopy()
    {
        if (copy != null) Destroy(copy);
        if (preOpenPanel == null) return;
        var source = (RectTransform)preOpenPanel.transform;
        copy = Instantiate(preOpenPanel.gameObject, sheet, false);
        copy.name = "GuidebookCopy";
        // 복사본은 보여 주기만 한다. 원본 Presenter와 버튼 입력은 쓰지 않는다.
        Destroy(copy.GetComponent<PreOpenPanelPresenter>());
        foreach (var button in copy.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
        Transform openBorder = copy.transform.Find("OpenShopBorder");
        if (openBorder != null) openBorder.gameObject.SetActive(false);
        copy.SetActive(true);

        Vector2 size = source.rect.size;
        float scale = size.y > 0f ? sheetHeight / size.y : 1f;
        var rect = (RectTransform)copy.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(scale, scale, 1f);
        sheet.sizeDelta = size * scale;
        closedPosition = new Vector2(sheet.sizeDelta.x + 40f, openPosition.y);

        foreach (var image in copy.GetComponentsInChildren<Image>(true))
            image.color *= sceneTint;
    }

    /// <summary>애니메이션 없이 지침서를 닫힌 상태로 둡니다.</summary>
    private void closeImmediately()
    {
        slideTween?.Kill();
        isOpen = false;
        if (sheet == null) return;
        sheet.anchoredPosition = closedPosition;
        sheet.gameObject.SetActive(false);
    }
}
