using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 정면 거래에서 작업대 분류 화면으로 전환하고 UI 좌표 기반 상품 물리와 판매 목록 집계를 담당합니다.
/// </summary>
public sealed class SaleSortingPanel : MonoBehaviour
{
    private enum ViewState
    {
        Hidden,
        FrontWaiting,
        Transition,
        Pouring,
        Sorting,
        Locked
    }

    private const float MinimumDeltaSeconds = 0.0001f;

    [Header("Views")]
    [SerializeField] private GameObject frontView;
    [SerializeField] private GameObject sortingView;
    [SerializeField] private RectTransform workArea;
    [SerializeField] private RectTransform itemRoot;
    [SerializeField] private RectTransform excludedZone;
    [SerializeField] private RectTransform saleZone;
    [SerializeField] private GameObject transitionOverlay;
    [SerializeField] private Image containerImage;
    [SerializeField] private Sprite tiltedContainerSprite;
    [SerializeField] private Sprite emptyContainerSprite;
    [SerializeField] private Button frontContainerButton;
    [SerializeField] private GameObject frontBasketRoot;

    [Header("Calculator")]
    [SerializeField] private RectTransform calculatorPanel;
    [SerializeField] private Button calculatorToggleButton;
    [SerializeField] private Sprite calculatorOpenSprite;
    [SerializeField] private Sprite calculatorClosedSprite;

    [Header("Items")]
    [SerializeField] private SaleSortingItemView itemPrefab;
    [SerializeField, Min(24f)] private float itemSizePixels = 72f;
    [SerializeField, Min(0f)] private float cursorRadiusPixels = 30f;
    [SerializeField, Min(0f)] private float cursorImpulse = 0.065f;
    [SerializeField, Min(0f)] private float maximumSpeedPixels = 230f;
    [SerializeField, Min(0f)] private float frictionPerSecond = 6.5f;
    [SerializeField, Range(0f, 1f)] private float itemRestitution = 0.1f;

    [Header("Flow")]
    [SerializeField, Min(0f)] private float transitionSeconds = 0.25f;
    [SerializeField, Min(0f)] private float customerArrivalSeconds = 0.8f;
    [SerializeField, Min(0f)] private float pourSeconds = 0.65f;
    [SerializeField] private TextMeshProUGUI sortingStatusText;

    private readonly List<SaleSortingItemView> items = new List<SaleSortingItemView>();
    private ViewState state;
    private bool isCalculatorOpen = true;
    private bool hasPointerSample;
    private Vector2 previousPointerPosition;
    private Coroutine transitionRoutine;
    private IReadOnlyList<CustomerBasketItemViewData> pendingBasket = Array.Empty<CustomerBasketItemViewData>();

    /// <summary>판매 상품 목록이 확정됐을 때 가격과 함께 전달됩니다.</summary>
    public event Action<IReadOnlyList<SaleItem>> SaleItemsConfirmed;

    /// <summary>쏟기 연출이 끝나 실제 물품 분류를 시작할 때 발생합니다.</summary>
    public event Action SortingStarted;

    /// <summary>현재 모든 상품이 판매 또는 제외 상태로 분류됐는지 나타냅니다.</summary>
    public bool CanConfirm => this.state == ViewState.Sorting && this.getWorkingCount() == 0;

    /// <summary>현재 분류 화면이 조작 가능한 상태인지 나타냅니다.</summary>
    public bool IsSorting => this.state == ViewState.Sorting;

    /// <summary>계산기 패널이 현재 열려 있는지 나타냅니다.</summary>
    public bool IsCalculatorOpen => this.isCalculatorOpen;

    /// <summary>계산기 표시 상태가 바뀐 뒤 발생합니다.</summary>
    public event Action<bool> CalculatorVisibilityChanged;

    /// <summary>버튼 이벤트를 연결하고 초기 화면을 숨깁니다.</summary>
    private void Awake()
    {
        if (this.calculatorToggleButton != null)
        {
            this.calculatorToggleButton.onClick.AddListener(this.ToggleCalculator);
        }
        if (this.frontContainerButton != null)
        {
            this.frontContainerButton.onClick.AddListener(this.handleFrontContainerClicked);
        }

        this.CalculatorVisibilityChanged?.Invoke(this.isCalculatorOpen);

        this.showFrontOnly();
    }

    /// <summary>상품 이동과 충돌을 프레임 경과 시간으로 계산합니다.</summary>
    private void Update()
    {
        if (this.state != ViewState.Sorting || this.workArea == null)
        {
            this.hasPointerSample = false;
            return;
        }

        float deltaSeconds = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (deltaSeconds < MinimumDeltaSeconds)
        {
            return;
        }

        this.applyPointerImpulse(deltaSeconds);
        this.integrateMotion(deltaSeconds);
        this.resolveItemCollisions();
        this.classifyItems();
        this.refreshStatus();
    }

    /// <summary>버튼 이벤트 구독과 진행 중 연출을 정리합니다.</summary>
    private void OnDestroy()
    {
        if (this.calculatorToggleButton != null)
        {
            this.calculatorToggleButton.onClick.RemoveListener(this.ToggleCalculator);
        }
        if (this.frontContainerButton != null)
        {
            this.frontContainerButton.onClick.RemoveListener(this.handleFrontContainerClicked);
        }
    }

    /// <summary>새 손님의 주문을 개별 상품 오브젝트로 생성하고 Astra 순서의 화면 전환을 시작합니다.</summary>
    /// <param name="basket">상품 ID, 수량과 이미지가 포함된 장바구니 표시 데이터입니다.</param>
    public void BeginCustomer(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        this.clearItems();
        if (basket == null)
        {
            throw new ArgumentNullException(nameof(basket));
        }

        this.pendingBasket = basket;
        if (this.frontBasketRoot != null) this.frontBasketRoot.SetActive(false);

        if (this.transitionRoutine != null)
        {
            StopCoroutine(this.transitionRoutine);
        }

        this.transitionRoutine = StartCoroutine(this.playContainerArrival());
    }

    /// <summary>거래 결과 표시를 위해 분류 입력을 잠그고 정면 화면으로 돌아갑니다.</summary>
    public void ShowTransactionResult()
    {
        this.state = ViewState.Locked;
        this.showFrontOnly();
    }

    /// <summary>모든 거래 화면과 생성한 상품을 정리합니다.</summary>
    public void ClearCustomer()
    {
        if (this.transitionRoutine != null)
        {
            StopCoroutine(this.transitionRoutine);
            this.transitionRoutine = null;
        }

        this.clearItems();
        this.pendingBasket = Array.Empty<CustomerBasketItemViewData>();
        this.showFrontOnly();
    }

    /// <summary>계산기 패널을 열거나 닫습니다.</summary>
    public void ToggleCalculator()
    {
        this.isCalculatorOpen = !this.isCalculatorOpen;
        if (this.calculatorPanel != null)
        {
            this.calculatorPanel.gameObject.SetActive(this.isCalculatorOpen);
        }

        if (this.calculatorToggleButton != null)
        {
            Image toggleImage = this.calculatorToggleButton.GetComponent<Image>();
            if (toggleImage != null)
            {
                toggleImage.sprite = this.isCalculatorOpen
                    ? this.calculatorOpenSprite
                    : this.calculatorClosedSprite;
            }
        }

        this.CalculatorVisibilityChanged?.Invoke(this.isCalculatorOpen);
    }

    /// <summary>현재 판매 영역에 확정된 상품을 ID별 수량으로 집계해 전달합니다.</summary>
    /// <returns>모든 상품이 분류되어 전달됐으면 true입니다.</returns>
    public bool TryConfirmSaleItems()
    {
        if (!this.TryGetSaleItems(out IReadOnlyList<SaleItem> saleItems))
        {
            return false;
        }

        this.state = ViewState.Locked;
        this.SaleItemsConfirmed?.Invoke(saleItems);
        return true;
    }

    /// <summary>현재 판매 영역의 상품을 ID별 판매 수량 목록으로 집계합니다.</summary>
    /// <param name="saleItems">모든 상품이 분류됐을 때 생성되는 불변 판매 목록입니다.</param>
    /// <returns>모든 상품이 분류돼 목록을 생성했으면 true입니다.</returns>
    public bool TryGetSaleItems(out IReadOnlyList<SaleItem> saleItems)
    {
        if (!this.CanConfirm)
        {
            saleItems = Array.Empty<SaleItem>();
            this.refreshStatus();
            return false;
        }

        var quantities = new SortedDictionary<uint, int>();
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.State != SaleSortingItemView.SortingState.ForSale) continue;
            quantities.TryGetValue(item.ProductId, out int quantity);
            quantities[item.ProductId] = checked(quantity + 1);
        }

        var result = new List<SaleItem>(quantities.Count);
        foreach (KeyValuePair<uint, int> pair in quantities)
        {
            result.Add(new SaleItem(pair.Key, pair.Value));
        }

        saleItems = result.AsReadOnly();
        return true;
    }

    /// <summary>거래 판정 요청이 전달된 뒤 추가 분류 입력을 잠급니다.</summary>
    public void LockSelection()
    {
        if (this.state == ViewState.Sorting)
        {
            this.state = ViewState.Locked;
        }
    }

    /// <summary>정면, 전환, 쏟기, 분류 순서로 새 손님 작업 화면을 엽니다.</summary>
    /// <returns>Unity 프레임에 걸쳐 진행되는 전환 열거자입니다.</returns>
    private IEnumerator playEntryFlow()
    {
        this.state = ViewState.Transition;
        if (this.frontView != null) this.frontView.SetActive(true);
        if (this.transitionOverlay != null) this.transitionOverlay.SetActive(false);

        RectTransform sortingRect = this.sortingView == null
            ? null
            : this.sortingView.transform as RectTransform;
        if (sortingRect != null)
        {
            this.sortingView.SetActive(true);
            float screenWidth = Mathf.Max(1f, sortingRect.rect.width);
            sortingRect.anchoredPosition = new Vector2(-screenWidth, 0f);
            float slideElapsed = 0f;
            float slideSeconds = Mathf.Max(0.01f, this.transitionSeconds);
            while (slideElapsed < slideSeconds)
            {
                slideElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slideElapsed / slideSeconds));
                sortingRect.anchoredPosition = new Vector2(Mathf.Lerp(-screenWidth, 0f, progress), 0f);
                yield return null;
            }

            sortingRect.anchoredPosition = Vector2.zero;
        }

        if (this.frontView != null) this.frontView.SetActive(false);
        if (this.calculatorPanel != null) this.calculatorPanel.gameObject.SetActive(this.isCalculatorOpen);
        if (this.calculatorToggleButton != null) this.calculatorToggleButton.gameObject.SetActive(true);
        if (this.containerImage != null)
        {
            this.containerImage.sprite = this.tiltedContainerSprite;
            this.containerImage.gameObject.SetActive(true);
        }

        this.state = ViewState.Pouring;
        this.createPendingItems();
        float elapsed = 0f;
        while (elapsed < this.pourSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = this.pourSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / this.pourSeconds);
            for (int i = 0; i < this.items.Count; i++)
            {
                SaleSortingItemView item = this.items[i];
                Vector2 target = this.getInitialSpreadPosition(i, this.items.Count);
                item.Position = Vector2.Lerp(this.getPourStartPosition(i), target, t);
            }

            yield return null;
        }

        if (this.containerImage != null)
        {
            this.containerImage.sprite = this.emptyContainerSprite;
        }

        for (int i = 0; i < this.items.Count; i++)
        {
            this.items[i].Velocity = UnityEngine.Random.insideUnitCircle * 14f;
        }

        this.state = ViewState.Sorting;
        this.hasPointerSample = false;
        this.refreshStatus();
        this.SortingStarted?.Invoke();
        this.transitionRoutine = null;
    }

    /// <summary>손님 정면 화면을 먼저 보여주고 박스를 가판대 위에 내려놓은 뒤 클릭을 기다립니다.</summary>
    /// <returns>박스 도착 연출을 프레임별로 진행하는 열거자입니다.</returns>
    private IEnumerator playContainerArrival()
    {
        this.state = ViewState.Transition;
        if (this.frontView != null) this.frontView.SetActive(true);
        if (this.sortingView != null) this.sortingView.SetActive(false);
        yield return this.waitUnscaled(this.customerArrivalSeconds);
        if (this.frontContainerButton != null)
        {
            RectTransform box = (RectTransform)this.frontContainerButton.transform;
            this.frontContainerButton.gameObject.SetActive(true);
            this.frontContainerButton.interactable = false;
            Vector2 destination = box.anchoredPosition;
            Vector2 start = destination + new Vector2(0f, 180f);
            float elapsed = 0f;
            const float ArrivalSeconds = 0.85f;
            while (elapsed < ArrivalSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ArrivalSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                box.anchoredPosition = Vector2.LerpUnclamped(start, destination, eased);
                yield return null;
            }

            box.anchoredPosition = destination;
            this.frontContainerButton.interactable = true;
        }

        this.state = ViewState.FrontWaiting;
        this.transitionRoutine = null;
    }

    /// <summary>가판대 위 박스를 클릭했을 때만 탑다운 작업대로 전환합니다.</summary>
    private void handleFrontContainerClicked()
    {
        if (this.state != ViewState.FrontWaiting || this.transitionRoutine != null) return;
        this.frontContainerButton.interactable = false;
        this.transitionRoutine = StartCoroutine(this.playEntryFlow());
    }

    /// <summary>보관한 주문을 수량 단위의 독립 상품 GameObject로 펼칩니다.</summary>
    private void createPendingItems()
    {
        int unitSequence = 0;
        foreach (CustomerBasketItemViewData line in this.pendingBasket)
        {
            for (int quantityIndex = 0; quantityIndex < line.Quantity; quantityIndex++)
            {
                SaleSortingItemView item = Instantiate(this.itemPrefab, this.itemRoot);
                item.name = $"SaleItem_{line.ItemId}_{quantityIndex}";
                item.Initialize(line.ItemId, quantityIndex, line.Icon, this.itemSizePixels, line.DisplayName);
                item.Position = this.getPourStartPosition(unitSequence);
                this.items.Add(item);
                unitSequence++;
            }
        }
    }

    /// <summary>지정한 실제 시간만큼 게임 시간 배율과 관계없이 기다립니다.</summary>
    /// <param name="seconds">기다릴 실제 시간입니다.</param>
    /// <returns>Unity 프레임 대기 열거자입니다.</returns>
    private IEnumerator waitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>마우스 이동량을 반경 안의 상품에 충격량으로 적용합니다.</summary>
    /// <param name="deltaSeconds">현재 프레임의 제한된 경과 시간입니다.</param>
    private void applyPointerImpulse(float deltaSeconds)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.workArea,
                this.getPointerScreenPosition(),
                null,
                out Vector2 pointerPosition)
            || !this.workArea.rect.Contains(pointerPosition)
            || this.isPointerOverCalculator())
        {
            this.hasPointerSample = false;
            return;
        }

        if (!this.hasPointerSample)
        {
            this.previousPointerPosition = pointerPosition;
            this.hasPointerSample = true;
            return;
        }

        Vector2 pointerVelocity = Vector2.ClampMagnitude(
            (pointerPosition - this.previousPointerPosition) / deltaSeconds,
            800f);
        this.previousPointerPosition = pointerPosition;
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.State == SaleSortingItemView.SortingState.Excluded)
            {
                continue;
            }

            Vector2 offset = item.Position - pointerPosition;
            float influenceRadius = this.cursorRadiusPixels + item.Radius;
            if (offset.sqrMagnitude > influenceRadius * influenceRadius)
            {
                continue;
            }

            float distanceFactor = 1f - Mathf.Clamp01(offset.magnitude / influenceRadius);
            Vector2 outward = offset.sqrMagnitude > 0.01f ? offset.normalized * pointerVelocity.magnitude : Vector2.zero;
            item.Velocity += Vector2.ClampMagnitude(
                (pointerVelocity + outward) * this.cursorImpulse * distanceFactor,
                this.maximumSpeedPixels * 0.65f);
        }
    }

    /// <summary>속도, 마찰과 작업대 경계를 적용합니다.</summary>
    /// <param name="deltaSeconds">현재 프레임의 제한된 경과 시간입니다.</param>
    private void integrateMotion(float deltaSeconds)
    {
        Rect bounds = this.workArea.rect;
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.State == SaleSortingItemView.SortingState.Excluded)
            {
                continue;
            }

            item.Velocity = Vector2.ClampMagnitude(item.Velocity, this.maximumSpeedPixels);
            item.Position += item.Velocity * deltaSeconds;
            item.Velocity *= Mathf.Exp(-this.frictionPerSecond * deltaSeconds);

            Vector2 position = item.Position;
            float radius = item.Radius;
            if (position.x - radius < bounds.xMin)
            {
                position.x = bounds.xMin + radius;
                item.Velocity = new Vector2(Mathf.Abs(item.Velocity.x) * this.itemRestitution, item.Velocity.y);
            }
            else if (position.x + radius > bounds.xMax)
            {
                position.x = bounds.xMax - radius;
                item.Velocity = new Vector2(-Mathf.Abs(item.Velocity.x) * this.itemRestitution, item.Velocity.y);
            }

            if (position.y - radius < bounds.yMin)
            {
                position.y = bounds.yMin + radius;
                item.Velocity = new Vector2(item.Velocity.x, Mathf.Abs(item.Velocity.y) * this.itemRestitution);
            }
            else if (position.y + radius > bounds.yMax)
            {
                position.y = bounds.yMax - radius;
                item.Velocity = new Vector2(item.Velocity.x, -Mathf.Abs(item.Velocity.y) * this.itemRestitution);
            }

            item.Position = position;
        }
    }

    /// <summary>작업 중인 상품끼리 원형 충돌을 계산해 겹침과 속도를 분리합니다.</summary>
    private void resolveItemCollisions()
    {
        for (int leftIndex = 0; leftIndex < this.items.Count; leftIndex++)
        {
            SaleSortingItemView left = this.items[leftIndex];
            if (left.State == SaleSortingItemView.SortingState.Excluded) continue;

            for (int rightIndex = leftIndex + 1; rightIndex < this.items.Count; rightIndex++)
            {
                SaleSortingItemView right = this.items[rightIndex];
                if (right.State == SaleSortingItemView.SortingState.Excluded) continue;

                Vector2 delta = right.Position - left.Position;
                float minimumDistance = left.Radius + right.Radius;
                if (delta.sqrMagnitude >= minimumDistance * minimumDistance) continue;

                float distance = Mathf.Max(delta.magnitude, 0.001f);
                Vector2 normal = distance > 0.001f ? delta / distance : Vector2.right;
                Vector2 correction = normal * ((minimumDistance - distance) * 0.5f);
                left.Position -= correction;
                right.Position += correction;

                float relativeSpeed = Vector2.Dot(right.Velocity - left.Velocity, normal);
                if (relativeSpeed >= 0f) continue;
                float impulse = -(1f + this.itemRestitution) * relativeSpeed * 0.5f;
                left.Velocity -= normal * impulse;
                right.Velocity += normal * impulse;
            }
        }
    }

    /// <summary>상품 중심이 판매 또는 제외 영역에 들어오면 즉시 분류 상태를 확정합니다.</summary>
    private void classifyItems()
    {
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.State == SaleSortingItemView.SortingState.Excluded)
            {
                continue;
            }

            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, item.transform.position);
            if (this.excludedZone != null
                && RectTransformUtility.RectangleContainsScreenPoint(this.excludedZone, screenPosition))
            {
                item.State = SaleSortingItemView.SortingState.Excluded;
                item.Velocity = Vector2.zero;
                item.gameObject.SetActive(false);
            }
            else if (this.saleZone != null
                && RectTransformUtility.RectangleContainsScreenPoint(this.saleZone, screenPosition))
            {
                item.State = SaleSortingItemView.SortingState.ForSale;
            }
            else
            {
                item.State = SaleSortingItemView.SortingState.Working;
            }
        }
    }

    /// <summary>계산기 위의 포인터 조작을 상품 물리 입력에서 제외합니다.</summary>
    /// <returns>현재 포인터가 열린 계산기 영역 위에 있으면 true입니다.</returns>
    private bool isPointerOverCalculator()
    {
        return this.isCalculatorOpen
            && this.calculatorPanel != null
            && RectTransformUtility.RectangleContainsScreenPoint(this.calculatorPanel, this.getPointerScreenPosition())
            && (EventSystem.current == null || EventSystem.current.IsPointerOverGameObject());
    }

    /// <summary>활성 입력 시스템에서 현재 포인터 화면 위치를 반환합니다.</summary>
    /// <returns>화면 픽셀 좌표입니다.</returns>
    private Vector2 getPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Pointer.current == null ? Vector2.zero : Pointer.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }


    /// <summary>쏟기 연출의 통 입구 위치를 상품별로 조금씩 분산합니다.</summary>
    /// <param name="index">전체 상품 중 순번입니다.</param>
    /// <returns>작업대 로컬 시작 위치입니다.</returns>
    private Vector2 getPourStartPosition(int index)
    {
        return new Vector2(-this.workArea.rect.width * 0.28f + ((index % 3) * 6f), this.workArea.rect.height * 0.22f);
    }

    /// <summary>상품을 작업대 중앙에 겹치지 않게 펼칠 목표 위치를 계산합니다.</summary>
    /// <param name="index">전체 상품 중 순번입니다.</param>
    /// <param name="count">전체 상품 개수입니다.</param>
    /// <returns>작업대 로컬 목표 위치입니다.</returns>
    private Vector2 getInitialSpreadPosition(int index, int count)
    {
        float angle = count <= 1 ? 0f : (Mathf.PI * 2f * index / count);
        float ring = Mathf.Min(this.workArea.rect.width, this.workArea.rect.height) * (0.12f + (index % 3) * 0.04f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
    }

    /// <summary>현재 미분류 상품 수를 계산합니다.</summary>
    /// <returns>작업대에 남아 있는 상품 수입니다.</returns>
    private int getWorkingCount()
    {
        int count = 0;
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.State == SaleSortingItemView.SortingState.Working) count++;
        }

        return count;
    }

    /// <summary>현재 분류 개수를 안내 텍스트에 표시합니다.</summary>
    private void refreshStatus()
    {
        if (this.sortingStatusText == null) return;
        int working = 0;
        int forSale = 0;
        int excluded = 0;
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.State == SaleSortingItemView.SortingState.Working) working++;
            else if (item.State == SaleSortingItemView.SortingState.ForSale) forSale++;
            else excluded++;
        }

        this.sortingStatusText.text = $"미분류 {working} · 판매 {forSale} · 판매 안함 {excluded}";
    }

    /// <summary>기존 개별 상품 오브젝트를 제거합니다.</summary>
    private void clearItems()
    {
        foreach (SaleSortingItemView item in this.items)
        {
            if (item != null) Destroy(item.gameObject);
        }

        this.items.Clear();
    }

    /// <summary>정면 거래 화면만 표시하고 작업 입력을 닫습니다.</summary>
    private void showFrontOnly()
    {
        this.state = ViewState.Hidden;
        if (this.frontView != null) this.frontView.SetActive(true);
        if (this.sortingView != null) this.sortingView.SetActive(false);
        if (this.transitionOverlay != null) this.transitionOverlay.SetActive(false);
        if (this.frontContainerButton != null) this.frontContainerButton.gameObject.SetActive(false);
        if (this.calculatorPanel != null) this.calculatorPanel.gameObject.SetActive(false);
        if (this.calculatorToggleButton != null) this.calculatorToggleButton.gameObject.SetActive(false);
        this.hasPointerSample = false;
    }
}
