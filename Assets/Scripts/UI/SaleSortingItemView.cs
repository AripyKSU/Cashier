using UnityEngine;
using UnityEngine.UI;

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
    }

    /// <summary>한 개별 상품 오브젝트를 표시 데이터와 연결합니다.</summary>
    /// <param name="productId">상품 데이터 식별자입니다.</param>
    /// <param name="unitIndex">같은 상품 내 개별 순번입니다.</param>
    /// <param name="sprite">표시할 상품 이미지입니다.</param>
    /// <param name="sizePixels">작업대에 표시할 정사각형 크기입니다.</param>
    public void Initialize(uint productId, int unitIndex, Sprite sprite, float sizePixels)
    {
        if (this.rectTransform == null)
        {
            this.rectTransform = (RectTransform)this.transform;
            this.itemImage = this.GetComponent<Image>();
        }

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
        this.gameObject.SetActive(true);
    }
}
