using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>작업대 위에서 독립적으로 움직이고 분류되는 상품 한 개를 표시합니다.</summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class SaleSortingItemView : MonoBehaviour
{
    private const float CollisionRadiusScale = 0.18f;
    /// <summary>상품의 현재 분류 상태입니다.</summary>
    public enum SortingState
    {
        /// <summary>작업대에서 아직 분류되지 않은 상태입니다.</summary>
        Working,

        /// <summary>판매 대상으로 확정된 상태입니다.</summary>
        ForSale,

        /// <summary>판매하지 않기로 확정된 상태입니다.</summary>
        Excluded
    }

    private RectTransform rectTransform;
    private Image itemImage;
    private TextMeshProUGUI itemNameText;

    /// <summary>상품 데이터 식별자입니다.</summary>
    public uint ProductId { get; private set; }

    /// <summary>같은 상품 수량 안에서의 개별 순번입니다.</summary>
    public int UnitIndex { get; private set; }

    /// <summary>현재 분류 상태입니다.</summary>
    public SortingState State { get; internal set; }

    /// <summary>작업대 로컬 좌표에서의 현재 속도입니다.</summary>
    public Vector2 Velocity { get; internal set; }

    /// <summary>상품 충돌에 사용하는 반지름입니다.</summary>
    public float Radius => Mathf.Min(this.rectTransform.rect.width, this.rectTransform.rect.height) * CollisionRadiusScale;

    /// <summary>상품의 작업대 로컬 위치입니다.</summary>
    public Vector2 Position
    {
        get => this.rectTransform.anchoredPosition;
        internal set => this.rectTransform.anchoredPosition = value;
    }

    /// <summary>상품 UI 참조를 준비합니다.</summary>
    private void Awake()
    {
        this.rectTransform = (RectTransform)this.transform;
        this.itemImage = this.GetComponent<Image>();
        this.ensureItemNameText();
    }

    /// <summary>한 개별 상품 오브젝트를 표시 데이터와 연결합니다.</summary>
    /// <param name="productId">상품 데이터 식별자입니다.</param>
    /// <param name="unitIndex">같은 상품 내 개별 순번입니다.</param>
    /// <param name="sprite">표시할 상품 이미지입니다.</param>
    /// <param name="sizePixels">작업대에 표시할 정사각형 크기입니다.</param>
    /// <param name="displayName">임시 이미지 위에 표시할 상품명입니다. 비어 있으면 상품 ID를 표시합니다.</param>
    public void Initialize(uint productId, int unitIndex, Sprite sprite, float sizePixels, string displayName = null)
    {
        if (this.rectTransform == null)
        {
            this.rectTransform = (RectTransform)this.transform;
            this.itemImage = this.GetComponent<Image>();
        }

        this.ensureItemNameText();

        ProductId = productId;
        UnitIndex = unitIndex;
        State = SortingState.Working;
        Velocity = Vector2.zero;
        this.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        this.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        this.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        this.rectTransform.sizeDelta = Vector2.one * sizePixels;
        this.itemImage.sprite = sprite;
        this.itemImage.preserveAspect = true;
        this.itemImage.raycastTarget = false;
        if (this.itemNameText != null)
        {
            this.itemNameText.text = string.IsNullOrWhiteSpace(displayName)
                ? $"#{productId}"
                : displayName;
            this.itemNameText.gameObject.SetActive(true);
        }
        this.gameObject.SetActive(true);
    }

    /// <summary>임시 흰색 상품 이미지 위에 상품명을 표시할 TMP 자식을 확보합니다.</summary>
    private void ensureItemNameText()
    {
        if (this.itemNameText != null || this.transform == null)
        {
            return;
        }

        Transform existing = this.transform.Find("TemporaryProductName");
        if (existing != null)
        {
            this.itemNameText = existing.GetComponent<TextMeshProUGUI>();
        }

        if (this.itemNameText == null)
        {
            GameObject labelObject = new GameObject("TemporaryProductName", typeof(RectTransform));
            labelObject.transform.SetParent(this.transform, false);
            this.itemNameText = labelObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform labelRect = this.itemNameText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(2f, 2f);
        labelRect.offsetMax = new Vector2(-2f, -2f);
        labelRect.localScale = Vector3.one;
        this.assignTemporaryFont();
        this.itemNameText.alignment = TextAlignmentOptions.Center;
        this.itemNameText.enableAutoSizing = true;
        this.itemNameText.fontSizeMin = 7f;
        this.itemNameText.fontSizeMax = 15f;
        this.itemNameText.enableWordWrapping = true;
        this.itemNameText.color = Color.black;
        this.itemNameText.raycastTarget = false;
        this.itemNameText.transform.SetAsLastSibling();
    }

    /// <summary>현재 UI에서 사용할 수 있는 TMP 글꼴을 임시 라벨에 연결합니다.</summary>
    private void assignTemporaryFont()
    {
        TextMeshProUGUI[] candidates = this.transform.root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int index = 0; index < candidates.Length; index++)
        {
            TextMeshProUGUI candidate = candidates[index];
            if (candidate == this.itemNameText || candidate.font == null)
            {
                continue;
            }

            this.itemNameText.font = candidate.font;
            return;
        }

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null)
        {
            return;
        }

        this.itemNameText.font = defaultFont;
    }
}
