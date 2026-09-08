using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3.5 CustomerPresenter
/// 현재 손님의 외형, 대화와 장바구니 품목을 화면에 표시하는 Presenter.
/// 손님이 없는 상태(HasCustomer=false)에서는 관련 UI를 초기화하거나 숨깁니다.
/// 손님 생성, 예산, 인내도와 가격 수락 여부를 UI가 판단하지 않으며, 전달받은 스냅샷만 렌더링합니다.
/// </summary>
public class CustomerPresenter : MonoBehaviour
{
    private const int RandomPlacementAttempts = 32;

    [Header("Customer Root & Appearance")]
    [Tooltip("손님 관련 UI 루트 오브젝트")]
    [SerializeField] private GameObject customerUIRoot;

    [Tooltip("손님 외형 이미지 (색상 또는 스프라이트)")]
    [SerializeField] private Image appearanceImage;

    [Header("Dialogue & Text")]
    [Tooltip("손님 대사 텍스트 (입장 인사 또는 판정 후 반응)")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Basket Display")]
    [Tooltip("장바구니 물품들이 배치되는 컨테이너 트랜스폼")]
    [SerializeField] private Transform basketContainer;

    [Tooltip("장바구니 한 항목을 표시하는 UI 프리팹")]
    [SerializeField] private CustomerBasketItemPresenter basketItemPrefab;

    [Tooltip("랜덤 배치된 장바구니 항목 사이에 확보할 최소 간격(픽셀)")]
    [SerializeField] private float basketItemSpacingPixels = 8f;

    // 생성한 프리팹 인스턴스를 재사용해 거래 갱신 시 파괴와 재생성을 피합니다.
    private readonly List<CustomerBasketItemPresenter> basketItems = new List<CustomerBasketItemPresenter>();

    // 동일한 장바구니를 다시 표시할 때 기존 랜덤 위치를 유지하기 위한 구성 식별값입니다.
    private int basketSignature;

    /// <summary>
    /// 외부 손님·거래 시스템에서 전달된 손님 스냅샷을 기반으로 UI를 갱신합니다.
    /// </summary>
    /// <param name="viewData">표시할 손님과 장바구니 스냅샷입니다.</param>
    public void UpdateView(CustomerViewData viewData)
    {
        if (!viewData.HasCustomer)
        {
            this.clearCustomerView();
            return;
        }

        if (this.customerUIRoot != null)
        {
            this.customerUIRoot.SetActive(true);
        }

        // 1. 외형 렌더링 (스프라이트 우선, 없으면 색상 블록)
        if (this.appearanceImage != null)
        {
            if (viewData.AppearanceSprite != null)
            {
                this.appearanceImage.sprite = viewData.AppearanceSprite;
                this.appearanceImage.color = Color.white;
            }
            else
            {
                this.appearanceImage.sprite = null;
                this.appearanceImage.color = viewData.AppearanceColor != Color.clear ? viewData.AppearanceColor : Color.white;
            }
            this.appearanceImage.gameObject.SetActive(true);
        }

        // 2. 대사 렌더링
        if (this.dialogueText != null)
        {
            this.dialogueText.text = !string.IsNullOrEmpty(viewData.DialogueText) ? viewData.DialogueText : "...";
        }

        // 3. 장바구니 렌더링
        this.renderBasket(viewData.Basket);
    }

    /// <summary>손님이 없을 때 관련 표시를 비우고 숨깁니다.</summary>
    private void clearCustomerView()
    {
        if (this.customerUIRoot != null)
        {
            this.customerUIRoot.SetActive(false);
        }

        if (this.dialogueText != null)
        {
            this.dialogueText.text = string.Empty;
        }

        if (this.appearanceImage != null)
        {
            this.appearanceImage.gameObject.SetActive(false);
        }

        this.hideBasketItems();
    }

    /// <summary>필요한 프리팹 항목을 확보하고 장바구니 데이터를 표시합니다.</summary>
    /// <param name="basket">표시할 장바구니 스냅샷입니다.</param>
    private void renderBasket(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        int nextBasketSignature = this.calculateBasketSignature(basket);
        bool shouldPlaceItems = nextBasketSignature != this.basketSignature;
        this.hideBasketItems();
        if (this.basketContainer == null || this.basketItemPrefab == null || basket == null)
        {
            this.basketSignature = 0;
            return;
        }

        var occupiedAreas = new List<Rect>(basket.Count);
        for (int index = 0; index < basket.Count; index++)
        {
            if (index >= this.basketItems.Count)
            {
                this.basketItems.Add(Instantiate(this.basketItemPrefab, this.basketContainer));
            }

            CustomerBasketItemPresenter itemPresenter = this.basketItems[index];
            itemPresenter.gameObject.SetActive(true);
            itemPresenter.UpdateView(basket[index]);
            if (shouldPlaceItems)
            {
                this.placeBasketItem(itemPresenter, index, basket.Count, occupiedAreas);
            }
        }

        this.basketSignature = nextBasketSignature;
    }

    /// <summary>생성한 장바구니 항목을 다음 손님을 위해 비활성화합니다.</summary>
    private void hideBasketItems()
    {
        foreach (CustomerBasketItemPresenter item in this.basketItems)
        {
            if (item != null)
            {
                item.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>장바구니 항목을 컨테이너 안의 겹치지 않는 임의 위치에 배치합니다.</summary>
    /// <param name="itemPresenter">배치할 항목 Presenter입니다.</param>
    /// <param name="itemIndex">안전 배치에 사용할 현재 항목 순서입니다.</param>
    /// <param name="itemCount">현재 장바구니의 전체 항목 수입니다.</param>
    /// <param name="occupiedAreas">이미 배치된 항목의 충돌 영역입니다.</param>
    private void placeBasketItem(
        CustomerBasketItemPresenter itemPresenter,
        int itemIndex,
        int itemCount,
        List<Rect> occupiedAreas)
    {
        if (!(this.basketContainer is RectTransform containerRect)
            || !(itemPresenter.transform is RectTransform itemRect))
        {
            return;
        }

        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        Vector2 itemSize = itemRect.rect.size;
        float halfWidth = itemSize.x * 0.5f;
        float halfHeight = itemSize.y * 0.5f;
        float minX = containerRect.rect.xMin + halfWidth;
        float maxX = containerRect.rect.xMax - halfWidth;
        float minY = containerRect.rect.yMin + halfHeight;
        float maxY = containerRect.rect.yMax - halfHeight;

        for (int attempt = 0; attempt < RandomPlacementAttempts; attempt++)
        {
            Vector2 position = new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY));
            Rect occupiedArea = this.createOccupiedArea(position, itemSize);
            if (!this.overlapsAny(occupiedArea, occupiedAreas))
            {
                itemRect.anchoredPosition = position;
                occupiedAreas.Add(occupiedArea);
                return;
            }
        }

        // 좁은 영역이나 항목 과다로 임의 배치가 실패해도 겹침을 최소화하는 규칙적 위치를 사용합니다.
        int columns = Mathf.Max(1, Mathf.FloorToInt(
            containerRect.rect.width / (itemSize.x + this.basketItemSpacingPixels)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)itemCount / columns));
        int column = itemIndex % columns;
        int row = itemIndex / columns;
        float xStep = columns <= 1 ? 0f : (maxX - minX) / (columns - 1);
        float yStep = rows <= 1 ? 0f : (maxY - minY) / (rows - 1);
        Vector2 fallbackPosition = new Vector2(minX + (column * xStep), maxY - (row * yStep));
        itemRect.anchoredPosition = fallbackPosition;
        occupiedAreas.Add(this.createOccupiedArea(fallbackPosition, itemSize));
    }

    /// <summary>간격을 포함한 항목 충돌 영역을 생성합니다.</summary>
    /// <param name="position">컨테이너 중앙 기준 항목 위치입니다.</param>
    /// <param name="itemSize">항목의 실제 크기입니다.</param>
    /// <returns>최소 간격을 포함한 충돌 영역입니다.</returns>
    private Rect createOccupiedArea(Vector2 position, Vector2 itemSize)
    {
        Vector2 paddedSize = itemSize + (Vector2.one * Mathf.Max(0f, this.basketItemSpacingPixels));
        return new Rect(position - (paddedSize * 0.5f), paddedSize);
    }

    /// <summary>후보 영역이 기존 항목 영역과 겹치는지 확인합니다.</summary>
    /// <param name="candidate">검사할 후보 충돌 영역입니다.</param>
    /// <param name="occupiedAreas">이미 사용 중인 충돌 영역 목록입니다.</param>
    /// <returns>하나 이상의 기존 영역과 겹치면 true입니다.</returns>
    private bool overlapsAny(Rect candidate, IReadOnlyList<Rect> occupiedAreas)
    {
        foreach (Rect occupiedArea in occupiedAreas)
        {
            if (candidate.Overlaps(occupiedArea))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>장바구니 구성 변경 여부를 판별할 안정적인 식별값을 계산합니다.</summary>
    /// <param name="basket">식별할 장바구니 스냅샷입니다.</param>
    /// <returns>상품 식별자와 수량으로 계산한 구성 식별값입니다.</returns>
    private int calculateBasketSignature(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        if (basket == null || basket.Count == 0)
        {
            return 0;
        }

        unchecked
        {
            int signature = 17;
            foreach (CustomerBasketItemViewData item in basket)
            {
                signature = (signature * 31) + item.ItemId.GetHashCode();
                signature = (signature * 31) + item.Quantity;
            }

            return signature;
        }
    }
}
