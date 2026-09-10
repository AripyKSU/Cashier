using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 정면 거래에서 작업대 분류 화면으로 전환하고 UI 좌표 기반 상품 드래그와 판매 목록 집계를 담당합니다.
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
    private const int SaleAnchorCount = 9;
    private const int SaleAnchorRowSize = 3;
    private const float AutoSortingDurationSeconds = 0.15f;

    [Header("Views")]
    [SerializeField] private GameObject frontView;
    [SerializeField] private GameObject sortingView;
    [SerializeField] private RectTransform workArea;
    [SerializeField] private RectTransform itemRoot;
    [SerializeField] private RectTransform excludedZone;
    [SerializeField] private RectTransform saleZone;
    [Tooltip("판매 구역 안의 3종×3개 자동 소팅 앵커입니다. ProductId 순으로 행을 사용합니다.")]
    [SerializeField] private RectTransform[] saleAnchors = new RectTransform[SaleAnchorCount];
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

    [Header("Flow")]
    [SerializeField, Min(0f)] private float transitionSeconds = 0.25f;
    [SerializeField, Min(0f)] private float customerArrivalSeconds = 0.8f;
    [SerializeField, Min(0f)] private float pourSeconds = 0.65f;
    [SerializeField, Min(0f)] private float autoAdvanceDelaySeconds = 0.5f;
    [SerializeField] private TextMeshProUGUI sortingStatusText;

    [Header("Divider Bar")]
    [Tooltip("작업대에서 상품을 물리적으로 밀어내는 큰 밀대")]
    [SerializeField] private DividerBarController dividerBar;

    [Header("Vacuum")]
    [Tooltip("상품을 여러 개 흡착해 함께 이동하는 청소기")]
    [SerializeField] private VacuumController vacuum;

    private readonly List<SaleSortingItemView> items = new List<SaleSortingItemView>();
    private readonly HashSet<SaleSortingItemView> dividerMovedItems = new HashSet<SaleSortingItemView>();
    private ViewState state;
    private bool isCalculatorOpen = true;
    private bool dividerBarAvailable;
    private bool autoSortingAvailable;
    private bool vacuumAvailable;
    private bool layoutContractViolationLogged;
    private SaleSortingItemView draggedItem;
    private Vector2 dragOffset;
    private Coroutine transitionRoutine;
    private Coroutine autoSortingRoutine;
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

    /// <summary>현재 세션에서 막대 편의성 효과가 활성화되었는지 나타냅니다.</summary>
    public bool IsDividerBarAvailable => this.dividerBarAvailable;

    /// <summary>현재 세션에서 자동 소팅 효과가 활성화되었는지 나타냅니다.</summary>
    public bool IsAutoSortingAvailable => this.autoSortingAvailable;

    /// <summary>현재 세션에서 청소기 편의성 효과가 활성화되었는지 나타냅니다.</summary>
    public bool IsVacuumAvailable => this.vacuumAvailable;

    /// <summary>현재 방문 상품이 고정된 3종×3개 계약을 위반했는지 나타냅니다.</summary>
    public bool HasSaleLayoutContractViolation { get; private set; }

    /// <summary>계산기 표시 상태가 바뀐 뒤 발생합니다.</summary>
    public event Action<bool> CalculatorVisibilityChanged;

    /// <summary>세션의 막대 활성 상태를 반영하고 비활성 상태에서는 막대를 숨깁니다.</summary>
    /// <param name="available">막대 효과가 현재 활성화되었는지 여부입니다.</param>
    public void SetDividerBarAvailable(bool available)
    {
        if (this.dividerBarAvailable == available)
        {
            if (this.dividerBar != null && !available)
            {
                this.dividerBar.SetVisible(false);
            }

            return;
        }

        this.dividerBarAvailable = available;
        if (!available)
        {
            this.releaseDividerItems();
            this.clearDividerManipulations();
        }

        if (this.dividerBar != null)
        {
            this.dividerBar.SetVisible(available && this.state == ViewState.Sorting);
        }
    }

    /// <summary>세션의 자동 소팅 활성 상태를 반영합니다.</summary>
    /// <param name="available">자동 소팅 효과가 현재 활성화되었는지 여부입니다.</param>
    public void SetAutoSortingAvailable(bool available)
    {
        if (this.autoSortingAvailable == available) return;
        this.autoSortingAvailable = available;
        if (!available)
        {
            this.stopAutoSorting();
        }
        else if (this.state == ViewState.Sorting)
        {
            this.requestAutoSort();
        }
    }

    /// <summary>세션의 청소기 활성 상태를 반영하고 비활성 상태에서는 상품을 해제합니다.</summary>
    /// <param name="available">청소기 효과가 현재 활성화되었는지 여부입니다.</param>
    public void SetVacuumAvailable(bool available)
    {
        if (this.vacuumAvailable == available)
        {
            if (this.vacuum != null && !available)
            {
                this.vacuum.SetVisible(false);
            }

            return;
        }

        this.vacuumAvailable = available;
        if (!available)
        {
            this.releaseVacuumItems();
            if (this.vacuum != null)
            {
                this.vacuum.ResetToStart();
                this.vacuum.SetVisible(false);
            }
        }
        else if (this.vacuum != null)
        {
            this.vacuum.SetVisible(this.state == ViewState.Sorting);
        }
    }

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

        if (this.dividerBar != null && this.workArea != null)
        {
            this.dividerBar.Initialize(this.workArea);
        }
        if (this.vacuum != null && this.workArea != null)
        {
            this.vacuum.Initialize(this.workArea, this.itemRoot);
        }

        this.showFrontOnly();
    }

    /// <summary>현재 도구 또는 플레이어가 소유한 상품만 한 번 이동시킵니다.</summary>
    private void Update()
    {
        if (this.state != ViewState.Sorting || this.workArea == null)
        {
            this.stopAutoSorting();
            bool wasVacuumHoldingOutsideSorting = this.vacuum != null && this.vacuum.IsHolding;
            if (this.vacuum != null)
            {
                this.vacuum.UpdateMotion(false, Vector2.zero, Time.unscaledDeltaTime, this.items);
            }
            if (wasVacuumHoldingOutsideSorting)
            {
                this.releaseVacuumItems();
            }
            if (this.dividerBar != null)
            {
                this.dividerBar.UpdateMotion(false, Vector2.zero, Time.unscaledDeltaTime);
            }
            this.releaseDraggedItem();
            return;
        }

        float deltaSeconds = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (deltaSeconds < MinimumDeltaSeconds)
        {
            return;
        }

        bool allowNewInteraction = !this.isPointerOverCalculator();
        bool wasVacuumHolding = this.vacuum != null && this.vacuum.IsHolding;
        if (this.vacuum != null)
        {
            this.vacuum.UpdateMotion(
                this.vacuumAvailable && (allowNewInteraction || wasVacuumHolding),
                this.getPointerScreenPosition(),
                deltaSeconds,
                this.items);
        }

        bool isVacuumHolding = this.vacuum != null && this.vacuum.IsHolding;
        if (wasVacuumHolding && !isVacuumHolding)
        {
            this.releaseVacuumItems();
        }
        if (isVacuumHolding)
        {
            this.stopAutoSorting();
        }

        bool wasHolding = this.dividerBar != null && this.dividerBar.IsHolding;
        if (this.dividerBar != null)
        {
            this.dividerBar.UpdateMotion(
                this.dividerBarAvailable && (allowNewInteraction || wasHolding) && !isVacuumHolding,
                this.getPointerScreenPosition(),
                deltaSeconds);
        }

        bool isDividerHolding = this.dividerBar != null && this.dividerBar.IsHolding;
        if (wasHolding && !isDividerHolding)
        {
            this.releaseDividerItems();
        }
        if (isDividerHolding)
        {
            if (!wasHolding)
            {
                this.dividerMovedItems.Clear();
            }

            this.stopAutoSorting();
            this.clearDividerManipulations();
            this.dividerBar.PushItems(this.items);
            this.trackDividerMovedItems();
        }
        if (!isDividerHolding && !isVacuumHolding)
        {
            this.updatePlayerDrag(allowNewInteraction);
        }
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
        this.stopAutoSorting();
        this.releaseVacuumItems();
        if (this.vacuum != null)
        {
            this.vacuum.ResetToStart();
        }
        this.clearItems();
        this.HasSaleLayoutContractViolation = false;
        this.layoutContractViolationLogged = false;
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
        this.stopAutoSorting();
        this.releaseVacuumItems();
        this.state = ViewState.Locked;
        this.showFrontOnly();
    }

    /// <summary>모든 거래 화면과 생성한 상품을 정리합니다.</summary>
    public void ClearCustomer()
    {
        this.stopAutoSorting();
        this.releaseVacuumItems();
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
            this.containerImage.sprite = this.tiltedContainerSprite != null ? this.tiltedContainerSprite : this.containerImage.sprite;
            this.containerImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            this.containerImage.gameObject.SetActive(true);
        }

        this.state = ViewState.Pouring;
        if (this.sortingStatusText != null)
        {
            this.sortingStatusText.text = "물품을 쏟는 중…";
        }

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

        // 쏟기가 끝나면 바구니를 숨기고 회전값을 원복합니다.
        if (this.containerImage != null)
        {
            this.containerImage.sprite = this.emptyContainerSprite;
            this.containerImage.gameObject.SetActive(false);
            this.containerImage.rectTransform.localRotation = Quaternion.identity;
        }

        if (this.dividerBar != null)
        {
            this.dividerBar.ResetToLeftEnd();
            this.dividerBar.SetVisible(this.dividerBarAvailable);
        }
        if (this.vacuum != null)
        {
            this.vacuum.ResetToStart();
            this.vacuum.SetVisible(this.vacuumAvailable);
        }

        this.state = ViewState.Sorting;
        this.draggedItem = null;
        this.dragOffset = Vector2.zero;
        this.refreshStatus();
        this.SortingStarted?.Invoke();
        this.transitionRoutine = null;
    }

    /// <summary>손님 정면 화면을 먼저 보여주고 박스를 가판대 위에 내려놓은 뒤 클릭 또는 자동 시간 경과로 작업대로 전환합니다.</summary>
    /// <returns>박스 도착 연출을 프레임별로 진행하는 열거자입니다.</returns>
    private IEnumerator playContainerArrival()
    {
        this.state = ViewState.Transition;
        if (this.frontView != null) this.frontView.SetActive(true);
        if (this.sortingView != null) this.sortingView.SetActive(false);
        if (this.dividerBar != null) this.dividerBar.SetVisible(false);
        if (this.vacuum != null) this.vacuum.SetVisible(false);
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

        // 자동으로 작업대 전환 (사용자가 직접 클릭하지 않아도 일정 시간 후 자동 진행)
        if (this.autoAdvanceDelaySeconds > 0f)
        {
            yield return this.waitUnscaled(this.autoAdvanceDelaySeconds);
            if (this.state == ViewState.FrontWaiting)
            {
                if (this.frontContainerButton != null) this.frontContainerButton.interactable = false;
                yield return this.playEntryFlow();
                yield break;
            }
        }

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

    /// <summary>막대가 지난 프레임에 소유했던 상품의 조작 상태를 정리합니다.</summary>
    private void clearDividerManipulations()
    {
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.Manipulation == SaleSortingItemView.ManipulationState.DividerMoving)
                item.Manipulation = SaleSortingItemView.ManipulationState.Idle;
        }
    }

    /// <summary>막대를 놓은 순간 막대가 소유했던 상품만 현재 위치로 분류합니다.</summary>
    private void releaseDividerItems()
    {
        foreach (SaleSortingItemView item in this.dividerMovedItems)
        {
            if (item == null)
            {
                continue;
            }

            this.classifyItem(item);
            if (item.Manipulation == SaleSortingItemView.ManipulationState.DividerMoving)
            {
                item.Manipulation = SaleSortingItemView.ManipulationState.Idle;
            }
        }

        this.dividerMovedItems.Clear();
        this.requestAutoSort();
    }

    /// <summary>현재 막대 드래그에서 막대가 한 번이라도 이동시킨 상품을 기록합니다.</summary>
    private void trackDividerMovedItems()
    {
        foreach (SaleSortingItemView item in this.items)
        {
            if (item != null && item.Manipulation == SaleSortingItemView.ManipulationState.DividerMoving)
            {
                this.dividerMovedItems.Add(item);
            }
        }
    }

    /// <summary>청소기를 놓은 순간 붙어 있던 상품을 현재 위치로 분류합니다.</summary>
    private void releaseVacuumItems()
    {
        if (this.vacuum == null)
        {
            return;
        }

        IReadOnlyList<SaleSortingItemView> releasedItems = this.vacuum.ReleaseAttachedItems();
        for (int index = 0; index < releasedItems.Count; index++)
        {
            this.classifyItem(releasedItems[index]);
        }

        if (releasedItems.Count > 0)
        {
            this.requestAutoSort();
        }
    }

    /// <summary>현재 포인터 입력을 기준으로 상품 하나를 직접 드래그합니다.</summary>
    /// <param name="allowPickup">계산대 위에서 새 상품을 잡을 수 있는지 여부입니다.</param>
    private void updatePlayerDrag(bool allowPickup)
    {
        bool isPressed;
        bool wasPressedThisFrame;
        this.getPointerButtonState(out isPressed, out wasPressedThisFrame);
        Vector2 pointerScreenPosition = this.getPointerScreenPosition();

        if (this.draggedItem == null && allowPickup && isPressed && wasPressedThisFrame &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(this.itemRoot, pointerScreenPosition, null,
                out Vector2 pointerPosition))
        {
            SaleSortingItemView item = this.getTopItemAt(pointerScreenPosition);
            if (item != null)
            {
                this.draggedItem = item;
                this.draggedItem.Manipulation = SaleSortingItemView.ManipulationState.PlayerDragging;
                this.dragOffset = item.Position - pointerPosition;
            }
        }

        if (this.draggedItem == null) return;
        if (isPressed && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.itemRoot, pointerScreenPosition, null, out Vector2 currentPointerPosition))
        {
            this.draggedItem.Position = this.clampItemPosition(currentPointerPosition + this.dragOffset, this.draggedItem);
        }
        else
        {
            this.releaseDraggedItem();
        }
    }

    /// <summary>포인터 아래 최상단의 활성 상품을 찾습니다.</summary>
    /// <param name="pointerScreenPosition">포인터 화면 좌표.</param>
    /// <returns>선택 가능한 상품 또는null.</returns>
    private SaleSortingItemView getTopItemAt(Vector2 pointerScreenPosition)
    {
        for (int index = this.items.Count - 1; index >= 0; index--)
        {
            SaleSortingItemView item = this.items[index];
            if (item == null || !item.gameObject.activeInHierarchy ||
                item.Manipulation == SaleSortingItemView.ManipulationState.PlayerDragging ||
                item.Manipulation == SaleSortingItemView.ManipulationState.DividerMoving ||
                item.Manipulation == SaleSortingItemView.ManipulationState.VacuumAttached ||
                item.State == SaleSortingItemView.SortingState.Excluded) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)item.transform, pointerScreenPosition, null))
                return item;
        }

        return null;
    }

    /// <summary>상품을 놓을 때 중심이 작업대 안에 있도록 위치를 제한합니다.</summary>
    /// <param name="position">상품 중심 후보 위치.</param>
    /// <param name="item">위치를 제한할 상품.</param>
    /// <returns>작업대 내부의 위치.</returns>
    private Vector2 clampItemPosition(Vector2 position, SaleSortingItemView item)
    {
        Rect bounds = this.itemRoot.rect;
        Vector2 halfSize = item.HalfSize;
        return new Vector2(
            Mathf.Clamp(position.x, bounds.xMin + halfSize.x, bounds.xMax - halfSize.x),
            Mathf.Clamp(position.y, bounds.yMin + halfSize.y, bounds.yMax - halfSize.y));
    }

    /// <summary>상품을 놓고 중심 위치 기준으로 한 번만 판매·제거·작업 구역을 판정합니다.</summary>
    private void releaseDraggedItem()
    {
        if (this.draggedItem == null) return;
        SaleSortingItemView releasedItem = this.draggedItem;
        this.draggedItem = null;
        this.dragOffset = Vector2.zero;
        this.classifyItem(releasedItem);
        releasedItem.Manipulation = SaleSortingItemView.ManipulationState.Idle;
        this.requestAutoSort();
    }

    /// <summary>판매 구역의 현재 상품을 고정된 앵커 순서로 자동 배치합니다.</summary>
    private void requestAutoSort()
    {
        if (!this.autoSortingAvailable || this.state != ViewState.Sorting) return;

        this.stopAutoSorting();
        Dictionary<SaleSortingItemView, Vector2> targets = this.createAutoSortTargets();
        if (targets == null) return;

        if (targets.Count > 0)
        {
            this.autoSortingRoutine = StartCoroutine(this.playAutoSorting(targets));
        }
    }

    /// <summary>현재 판매 상품에 대응하는 앵커 위치를 계산합니다.</summary>
    /// <returns>상품별 목표 위치 또는 계약 위반 시 null입니다.</returns>
    private Dictionary<SaleSortingItemView, Vector2> createAutoSortTargets()
    {
        if (!this.validateSaleLayoutContract()) return null;

        var grouped = new Dictionary<uint, List<SaleSortingItemView>>();
        foreach (SaleSortingItemView item in this.items)
        {
            if (item == null || item.State != SaleSortingItemView.SortingState.ForSale ||
                item.Manipulation != SaleSortingItemView.ManipulationState.Idle) continue;

            if (!grouped.TryGetValue(item.ProductId, out List<SaleSortingItemView> group))
            {
                group = new List<SaleSortingItemView>();
                grouped.Add(item.ProductId, group);
            }

            group.Add(item);
        }

        var targets = new Dictionary<SaleSortingItemView, Vector2>();
        int row = 0;
        foreach (KeyValuePair<uint, List<SaleSortingItemView>> pair in grouped.OrderBy(x => x.Key))
        {
            List<SaleSortingItemView> group = pair.Value;
            group.Sort((left, right) => left.UnitIndex.CompareTo(right.UnitIndex));
            for (int unitIndex = 0; unitIndex < group.Count; unitIndex++)
            {
                SaleSortingItemView item = group[unitIndex];
                Vector2 target = this.getSaleAnchorPosition(row * SaleAnchorRowSize + unitIndex);
                if ((item.Position - target).sqrMagnitude > 0.01f)
                {
                    targets.Add(item, target);
                }
            }

            row++;
        }

        return targets;
    }

    /// <summary>상품 생성 계약과 앵커 수를 검사하고 위반을 조용히 처리하지 않습니다.</summary>
    /// <returns>자동 소팅을 진행할 수 있으면 true입니다.</returns>
    private bool validateSaleLayoutContract()
    {
        if (this.saleAnchors == null || this.saleAnchors.Length != SaleAnchorCount ||
            this.saleAnchors.Any(anchor => anchor == null))
        {
            this.reportSaleLayoutContractViolation("판매 구역 자동 소팅 앵커는 9개가 모두 연결되어야 합니다.");
            return false;
        }

        var counts = new Dictionary<uint, int>();
        foreach (SaleSortingItemView item in this.items)
        {
            if (item == null) continue;
            counts.TryGetValue(item.ProductId, out int count);
            counts[item.ProductId] = count + 1;
        }

        if (counts.Count > SaleAnchorRowSize || counts.Any(pair => pair.Value > SaleAnchorRowSize))
        {
            this.reportSaleLayoutContractViolation("방문 상품은 최대 3종이며 상품별 최대 수량은 3개입니다.");
            return false;
        }

        HasSaleLayoutContractViolation = false;
        return true;
    }

    /// <summary>계약 위반을 한 번 기록하고 상품을 삭제하거나 겹치게 만들지 않습니다.</summary>
    /// <param name="message">개발자에게 전달할 위반 사유입니다.</param>
    private void reportSaleLayoutContractViolation(string message)
    {
        HasSaleLayoutContractViolation = true;
        if (this.layoutContractViolationLogged) return;
        this.layoutContractViolationLogged = true;
        Debug.LogError($"[SaleSortingPanel] {message}", this);
    }

    /// <summary>Inspector에서 조정한 앵커를 상품 작업대 좌표로 변환합니다.</summary>
    /// <param name="anchorIndex">0부터 시작하는 앵커 인덱스입니다.</param>
    /// <returns>상품 ItemRoot 기준 목표 좌표입니다.</returns>
    private Vector2 getSaleAnchorPosition(int anchorIndex)
    {
        Vector3 localPosition = this.itemRoot.InverseTransformPoint(this.saleAnchors[anchorIndex].position);
        return new Vector2(localPosition.x, localPosition.y);
    }

    /// <summary>자동 소팅 중인 상품을 짧게 보간하고 중단된 상품은 현재 위치에 둡니다.</summary>
    /// <param name="targets">상품별 목표 위치입니다.</param>
    /// <returns>Unity 프레임별 보간 열거자입니다.</returns>
    private IEnumerator playAutoSorting(IReadOnlyDictionary<SaleSortingItemView, Vector2> targets)
    {
        var starts = new Dictionary<SaleSortingItemView, Vector2>();
        foreach (KeyValuePair<SaleSortingItemView, Vector2> pair in targets)
        {
            SaleSortingItemView item = pair.Key;
            if (item == null || !item.gameObject.activeInHierarchy ||
                item.State != SaleSortingItemView.SortingState.ForSale ||
                item.Manipulation != SaleSortingItemView.ManipulationState.Idle) continue;

            starts.Add(item, item.Position);
            item.Manipulation = SaleSortingItemView.ManipulationState.AutoSorting;
        }

        float elapsed = 0f;
        while (elapsed < AutoSortingDurationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / AutoSortingDurationSeconds));
            foreach (KeyValuePair<SaleSortingItemView, Vector2> pair in targets)
            {
                SaleSortingItemView item = pair.Key;
                if (item != null && starts.ContainsKey(item) &&
                    item.Manipulation == SaleSortingItemView.ManipulationState.AutoSorting)
                {
                    item.Position = Vector2.Lerp(starts[item], pair.Value, progress);
                }
            }

            yield return null;
        }

        foreach (KeyValuePair<SaleSortingItemView, Vector2> pair in targets)
        {
            SaleSortingItemView item = pair.Key;
            if (item != null && item.Manipulation == SaleSortingItemView.ManipulationState.AutoSorting)
            {
                item.Position = pair.Value;
                item.Manipulation = SaleSortingItemView.ManipulationState.Idle;
            }
        }

        this.autoSortingRoutine = null;
    }

    /// <summary>진행 중 자동 소팅을 중단하고 상품 조작 상태를 대기로 되돌립니다.</summary>
    private void stopAutoSorting()
    {
        if (this.autoSortingRoutine != null)
        {
            StopCoroutine(this.autoSortingRoutine);
            this.autoSortingRoutine = null;
        }

        foreach (SaleSortingItemView item in this.items)
        {
            if (item != null && item.Manipulation == SaleSortingItemView.ManipulationState.AutoSorting)
            {
                item.Manipulation = SaleSortingItemView.ManipulationState.Idle;
            }
        }
    }

    /// <summary>상품 하나의 중심 위치로 구역 상태를 결정합니다.</summary>
    /// <param name="item">판정할 상품.</param>
    private void classifyItem(SaleSortingItemView item)
    {
        if (item == null || item.State == SaleSortingItemView.SortingState.Excluded) return;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, item.transform.position);
        if (this.excludedZone != null && RectTransformUtility.RectangleContainsScreenPoint(this.excludedZone, screenPosition))
        {
            item.State = SaleSortingItemView.SortingState.Excluded;
            item.gameObject.SetActive(false);
        }
        else if (this.saleZone != null && RectTransformUtility.RectangleContainsScreenPoint(this.saleZone, screenPosition))
        {
            item.State = SaleSortingItemView.SortingState.ForSale;
        }
        else
        {
            item.State = SaleSortingItemView.SortingState.Working;
        }
    }

    /// <summary>현재 입력 장치의 누름 상태를 반환합니다.</summary>
    /// <param name="isPressed">현재 누르고 있는지 여부.</param>
    /// <param name="wasPressedThisFrame">이번 프레임에 눌렸는지 여부.</param>
    private void getPointerButtonState(out bool isPressed, out bool wasPressedThisFrame)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        isPressed = mouse != null && mouse.leftButton.isPressed;
        wasPressedThisFrame = mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        isPressed = Input.GetMouseButton(0);
        wasPressedThisFrame = Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>계산기 위의 포인터 조작을 상품 물리 입력에서 제외합니다.</summary>
    /// <returns>현재 포인터가 열린 계산기 영역 위에 있으면 true입니다.</returns>
    private bool isPointerOverCalculator()
    {
        return this.isCalculatorOpen
            && this.calculatorPanel != null
            && RectTransformUtility.RectangleContainsScreenPoint(this.calculatorPanel, this.getPointerScreenPosition());
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
        return new Vector2(-280f + ((index % 3) * 12f), 60f + ((index / 3) * 16f));
    }

    /// <summary>상품을 작업대 중앙에 겹치지 않게 펼칠 목표 위치를 계산합니다.</summary>
    /// <param name="index">전체 상품 중 순번입니다.</param>
    /// <param name="count">전체 상품 개수입니다.</param>
    /// <returns>작업대 로컬 목표 위치입니다.</returns>
    private Vector2 getInitialSpreadPosition(int index, int count)
    {
        float angle = count <= 1 ? 0f : (Mathf.PI * 2f * index / count);
        float ring = Mathf.Min(this.workArea.rect.width, this.workArea.rect.height) * (0.13f + (index % 3) * 0.04f);
        return new Vector2(Mathf.Cos(angle) * ring - 40f, Mathf.Sin(angle) * ring + 10f);
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
        this.dividerMovedItems.Clear();
        this.draggedItem = null;
        this.dragOffset = Vector2.zero;
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
        if (this.dividerBar != null) this.dividerBar.SetVisible(false);
        if (this.vacuum != null) this.vacuum.SetVisible(false);
        this.releaseDraggedItem();
    }
}
