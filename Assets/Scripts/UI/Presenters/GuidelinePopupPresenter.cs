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

    [Tooltip("지침서가 덮을 영역(물건을 놓는 판매 칸). 이 영역에 맞춰 줄여 가운데에 놓습니다.")]
    [SerializeField] private RectTransform coverArea;

    [Tooltip("영업 화면에 맞춘 종이 색(곱하기)")]
    [SerializeField] private Color sceneTint = new Color(.84f, .78f, .7f, 1f);

    [Tooltip("나오고 들어가는 시간(초)")]
    [SerializeField, Min(0.05f)] private float slideSeconds = 0.25f;

    private bool isOpen;
    private Tween slideTween;
    private GameObject copy;
    private Vector2 openPosition;
    private Vector2 closedPosition;

    private void Awake()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(toggle);
        closeImmediately();
    }

    private void OnEnable()
    {
        SaleSortingItemView.AnyDragStarted += closeOnPickup;
    }

    private void OnDisable()
    {
        SaleSortingItemView.AnyDragStarted -= closeOnPickup;
        // 작업대가 닫히면 지침서도 들어간 상태로 되돌린다.
        closeImmediately();
    }

    private void OnDestroy()
    {
        slideTween?.Kill();
        if (toggleButton != null) toggleButton.onClick.RemoveListener(toggle);
    }

    /// <summary>물건을 집는 순간 펼쳐 둔 지침서를 닫는다. 지침서를 편 채로는 장사를 이어갈 수 없다.</summary>
    private void closeOnPickup()
    {
        if (isOpen) toggle();
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

        // 판매 칸 크기에 맞춰 비율을 지킨 채 줄이고, 판매 칸 가운데를 덮는다. 닫히면 화면 오른쪽 밖으로 빠진다.
        var parent = (RectTransform)sheet.parent;
        Rect area = coverArea != null ? rectIn(coverArea, parent) : parent.rect;
        Vector2 size = source.rect.size;
        float scale = size.x > 0f && size.y > 0f ? Mathf.Min(area.width / size.x, area.height / size.y) * .98f : 1f;
        var rect = (RectTransform)copy.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(scale, scale, 1f);
        sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
        sheet.sizeDelta = size * scale;
        Vector2 parentCenter = parent.rect.center;
        openPosition = area.center - parentCenter;
        closedPosition = new Vector2(parent.rect.width * .5f + sheet.sizeDelta.x * .5f + 20f, openPosition.y);

        foreach (var image in copy.GetComponentsInChildren<Image>(true))
            image.color *= sceneTint;
    }

    /// <summary>다른 RectTransform의 사각형을 부모 좌표로 옮깁니다.</summary>
    private static Rect rectIn(RectTransform target, RectTransform parent)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
