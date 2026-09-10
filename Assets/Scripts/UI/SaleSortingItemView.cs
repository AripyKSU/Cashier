using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>작업대 위에서 독립적으로 움직이고 분류되는 상품 한 개를 표시하며, 마우스 드래그 앤 드롭 조작을 지원합니다.</summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class SaleSortingItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
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
    private RectTransform workArea;
    private bool isDragging;

    /// <summary>상품 데이터 식별자입니다.</summary>
    public uint ProductId { get; private set; }

    /// <summary>같은 상품 수량 안에서의 개별 순번입니다.</summary>
    public int UnitIndex { get; private set; }

    /// <summary>현재 분류 상태입니다.</summary>
    public SortingState State { get; set; }

    /// <summary>작업대 로컬 좌표에서의 현재 속도입니다.</summary>
    public Vector2 Velocity { get; internal set; }

    /// <summary>플레이어가 현재 이 아이템을 마우스로 드래그하고 있는지 여부입니다.</summary>
    public bool IsDragging => this.isDragging;

    /// <summary>상품 충돌에 사용하는 반지름입니다.</summary>
    public float Radius => Mathf.Min(this.rectTransform.rect.width, this.rectTransform.rect.height) * CollisionRadiusScale;

    /// <summary>상품의 작업대 로컬 위치입니다.</summary>
    public Vector2 Position
    {
        get => this.rectTransform.anchoredPosition;
        internal set => this.rectTransform.anchoredPosition = value;
    }

    /// <summary>드래그 시작 시 발생합니다.</summary>
    public event Action<SaleSortingItemView> DragStarted;

    /// <summary>드래그 이동 시 발생합니다.</summary>
    public event Action<SaleSortingItemView> Dragged;

    /// <summary>드래그 종료 시 발생합니다.</summary>
    public event Action<SaleSortingItemView> DragEnded;

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
    /// <param name="workAreaRect">드래그 좌표 변환에 사용할 작업대 RectTransform입니다.</param>
    public void Initialize(uint productId, int unitIndex, Sprite sprite, float sizePixels, string displayName = null, RectTransform workAreaRect = null)
    {
        if (this.rectTransform == null)
        {
            this.rectTransform = (RectTransform)this.transform;
            this.itemImage = this.GetComponent<Image>();
        }

        this.ensureItemNameText();

        this.workArea = workAreaRect;
        this.ProductId = productId;
        this.UnitIndex = unitIndex;
        this.State = SortingState.Working;
        this.Velocity = Vector2.zero;
        this.isDragging = false;
        this.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        this.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        this.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        this.rectTransform.sizeDelta = Vector2.one * sizePixels;
        this.transform.localScale = Vector3.one;
        this.itemImage.sprite = sprite;
        this.itemImage.preserveAspect = true;
        this.itemImage.raycastTarget = true;
        if (this.itemNameText != null)
        {
            this.itemNameText.text = string.IsNullOrWhiteSpace(displayName)
                ? $"#{productId}"
                : displayName;
            this.itemNameText.gameObject.SetActive(true);
            this.itemNameText.raycastTarget = false;
        }

        this.UpdateVisualState();
        this.gameObject.SetActive(true);
    }

    /// <summary>마우스 클릭 시 드래그 준비를 처리합니다.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 클릭 감지 및 피드백 준비
    }

    /// <summary>마우스 클릭 해제 시 처리를 수행합니다.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        // 클릭 해제 처리
    }

    /// <summary>아이템을 집어 올리고 드래그를 시작합니다.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        this.isDragging = true;
        this.Velocity = Vector2.zero;
        this.transform.SetAsLastSibling();
        this.transform.localScale = Vector3.one * 1.08f;
        this.DragStarted?.Invoke(this);
    }

    /// <summary>마우스 위치를 따라 아이템 위치를 갱신합니다.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!this.isDragging) return;

        RectTransform parentRect = this.workArea != null ? this.workArea : (RectTransform)this.transform.parent;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            Rect bounds = parentRect.rect;
            float radius = this.Radius;
            float clampedX = Mathf.Clamp(localPoint.x, bounds.xMin + radius, bounds.xMax - radius);
            float clampedY = Mathf.Clamp(localPoint.y, bounds.yMin + radius, bounds.yMax - radius);
            this.Position = new Vector2(clampedX, clampedY);
        }

        this.Dragged?.Invoke(this);
    }

    /// <summary>아이템을 내려놓고 드래그를 마칩니다.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        this.isDragging = false;
        this.transform.localScale = Vector3.one;

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        this.Velocity = Vector2.ClampMagnitude(eventData.delta / dt * 0.15f, 150f);

        this.DragEnded?.Invoke(this);
        this.UpdateVisualState();
    }

    /// <summary>현재 분류 상태(판매/미분류/제외)에 맞춰 아이템 색상을 갱신합니다.</summary>
    public void UpdateVisualState()
    {
        if (this.itemImage == null) return;

        switch (this.State)
        {
            case SortingState.ForSale:
                // 판매 구역에 들어가면 산뜻한 연두색 틴트로 판매 등록 피드백 표시
                this.itemImage.color = new Color(0.85f, 1.0f, 0.85f, 1f);
                break;

            case SortingState.Excluded:
                this.itemImage.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                break;

            case SortingState.Working:
            default:
                this.itemImage.color = Color.white;
                break;
        }
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
        this.itemNameText.textWrappingMode = TextWrappingModes.Normal;
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
