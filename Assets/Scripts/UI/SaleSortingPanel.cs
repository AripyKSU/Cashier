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

    [Header("Landing Dust")]
    [Tooltip("상자 착지 충격 시 좌우로 흩뿌려지는 10개의 픽셀 먼지 효과 컴포넌트")]
    [SerializeField] private LandingDustEffect landingDustEffect;

    [Header("Calculator")]
    [SerializeField] private RectTransform calculatorPanel;
    /// <summary>계산기가 화면 밖과 도착 위치 사이를 완전히 이동하는 시간(초)입니다.</summary>
    [SerializeField, Min(0.01f)] private float calculatorSlideSeconds = 1f;

    [Header("Items")]
    [SerializeField] private SaleSortingItemView itemPrefab;
    [SerializeField, Min(24f)] private float itemSizePixels = 144f;

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

    [Header("Hand Cursor")]
    [Tooltip("물품, 막대와 청소기의 통합 잡기 상태를 표시하는 최상단 손 커서")]
    [SerializeField] private SaleSortingHandCursor handCursor;

    private readonly List<SaleSortingItemView> items = new List<SaleSortingItemView>();
    private readonly HashSet<SaleSortingItemView> dividerMovedItems = new HashSet<SaleSortingItemView>();
    private ViewState state;
    private bool dividerBarAvailable;
    private bool autoSortingAvailable;
    private bool vacuumAvailable;
    private bool layoutContractViolationLogged;
    private SaleSortingItemView draggedItem;
    private Vector2 dragOffset;
    private Coroutine transitionRoutine;
    private Coroutine autoSortingRoutine;
    private Coroutine calculatorSlideRoutine;
    private Vector2 calculatorOpenPosition;
    private Vector2 calculatorClosedPosition;
    private bool isCalculatorOpen;
    private bool calculatorTargetVisible;
    private IReadOnlyList<CustomerBasketItemViewData> pendingBasket = Array.Empty<CustomerBasketItemViewData>();
    // 로컬 큐 표현에서만 제공하며 Controller 제거 시 해제합니다.
    private Func<bool> isPresentationBlocked;

    /// <summary>기존 연출 시계를 멈출 조회자를 연결한다. null은 기존 unscaled 동작이다.</summary>
    /// <param name="isBlocked">오류나 비활성화로 표현 진행이 막혔는지 조회합니다.</param>
    public void SetPresentationBlockQuery(Func<bool> isBlocked) => this.isPresentationBlocked = isBlocked;

    /// <summary>기존 unscaled 시간을 사용하되 표현 진행이 막힌 동안은 제외합니다.</summary>
    private float PresentationDeltaSeconds => this.isPresentationBlocked?.Invoke() == true ? 0f : Time.unscaledDeltaTime;

    /// <summary>판매 상품 목록이 확정됐을 때 가격과 함께 전달됩니다.</summary>
    public event Action<IReadOnlyList<SaleItem>> SaleItemsConfirmed;

    /// <summary>쏟기 연출이 끝나 실제 물품 분류를 시작할 때 발생합니다.</summary>
    public event Action SortingStarted;

    /// <summary>정면 상자를 현재 단계의 닫힌/열린 Sprite로 바꿔야 할 때 발생합니다.</summary>
    public event Action<bool> ContainerOpenChanged;
    /// <summary>매대의 모든 물품이 폐기 구역으로 이동되어 판매 물품이 0개일 때 발생합니다.</summary>
    public event Action AllItemsDiscarded;

    /// <summary>월드 정면 표시가 기존 슬라이드 전환과 같은 가시성·좌표를 관찰하는 영역.</summary>
    public RectTransform FrontView => this.frontView != null ? this.frontView.transform as RectTransform : null;

    /// <summary>실제 상품이 모두 분류되고 이동 조작도 끝나 판매 목록을 확정할 수 있는지 나타냅니다.</summary>
    public bool CanConfirm => this.state == ViewState.Sorting &&
        (this.vacuum == null || !this.vacuum.IsBusy) && this.items.Any(item => item != null) &&
        this.getWorkingCount() == 0 && this.items.All(item => item == null ||
            item.State == SaleSortingItemView.SortingState.Excluded ||
            item.Manipulation == SaleSortingItemView.ManipulationState.Idle);

    /// <summary>현재 분류 화면이 조작 가능한 상태인지 나타냅니다.</summary>
    public bool IsSorting => this.state == ViewState.Sorting;

    /// <summary>계산기가 입장을 완료하여 가격 입력을 허용하는지 나타냅니다.</summary>
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
                this.cancelVacuumItems();
                this.vacuum.SetVisible(false);
            }

            return;
        }

        this.vacuumAvailable = available;
        if (!available)
        {
            this.cancelVacuumItems();
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
        if (this.calculatorPanel != null)
        {
            this.calculatorOpenPosition = this.calculatorPanel.anchoredPosition;
            this.calculatorClosedPosition = this.getCalculatorClosedPosition();
        }
        this.hideCalculatorImmediately();

        if (this.frontContainerButton != null)
        {
            this.frontContainerButton.onClick.AddListener(this.handleFrontContainerClicked);
        }

        if (this.dividerBar != null && this.workArea != null)
        {
            this.dividerBar.Initialize(this.workArea);
        }
        if (this.vacuum != null && this.workArea != null)
        {
            this.vacuum.Initialize(this.workArea, this.itemRoot);
        }

        this.ensureLandingDustEffect();
        this.showFrontOnly();
    }

    private LandingDustEffect ensureLandingDustEffect()
    {
        if (this.landingDustEffect == null)
        {
            this.landingDustEffect = this.GetComponentInChildren<LandingDustEffect>(true);
            if (this.landingDustEffect == null && this.frontView != null)
            {
                this.landingDustEffect = this.frontView.AddComponent<LandingDustEffect>();
            }
        }
        return this.landingDustEffect;
    }

    /// <summary>현재 도구 또는 플레이어가 소유한 상품만 한 번 이동시킵니다.</summary>
    private void Update()
    {
        if (this.state != ViewState.Sorting || this.workArea == null || this.isPresentationBlocked?.Invoke() == true)
        {
            this.stopAutoSorting();
            if (this.vacuum != null)
            {
                this.cancelVacuumItems();
            }
            if (this.dividerBar != null)
            {
                this.dividerBar.UpdateMotion(false, Vector2.zero, Time.unscaledDeltaTime);
            }
            this.releaseDraggedItem();
            if (this.handCursor != null) this.handCursor.Hide();
            return;
        }

        float deltaSeconds = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (deltaSeconds < MinimumDeltaSeconds)
        {
            return;
        }

        bool allowNewInteraction = !this.isPointerOverCalculator();
        this.drainVacuumItems();
        bool wasVacuumBusy = this.vacuum != null && this.vacuum.IsBusy;
        bool wasVacuumHolding = this.vacuum != null && this.vacuum.IsHolding;
        bool wasDividerHolding = this.dividerBar != null && this.dividerBar.IsHolding;
        if (this.vacuum != null)
        {
            this.vacuum.UpdateMotion(
                this.vacuumAvailable && (wasVacuumHolding ||
                    (allowNewInteraction && this.draggedItem == null && !wasDividerHolding)),
                this.getPointerScreenPosition(),
                deltaSeconds,
                this.items);
        }

        bool isVacuumHolding = this.vacuum != null && this.vacuum.IsHolding;
        bool isVacuumBusy = this.vacuum != null && this.vacuum.IsBusy;
        if (isVacuumBusy)
        {
            this.stopAutoSorting();
        }

        bool wasHolding = wasDividerHolding;
        if (this.dividerBar != null)
        {
            this.dividerBar.UpdateMotion(
                this.dividerBarAvailable && !isVacuumBusy &&
                    (allowNewInteraction || wasHolding) && !isVacuumHolding,
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
        if (!isDividerHolding && !isVacuumHolding && !isVacuumBusy)
        {
            this.updatePlayerDrag(allowNewInteraction);
        }
        this.updateHandCursor(
            this.draggedItem != null || isDividerHolding || isVacuumHolding,
            allowNewInteraction,
            deltaSeconds);
        if (wasVacuumBusy && !isVacuumBusy)
        {
            this.requestAutoSort();
        }
        this.refreshStatus();
    }

    /// <summary>버튼 이벤트 구독과 진행 중 연출을 정리합니다.</summary>
    private void OnDestroy()
    {
        this.hideCalculatorImmediately();
        if (this.frontContainerButton != null)
        {
            this.frontContainerButton.onClick.RemoveListener(this.handleFrontContainerClicked);
        }
        if (this.handCursor != null) this.handCursor.Hide();
        this.cancelVacuumItems();
    }

    /// <summary>비활성화 중 계산기 연출과 열린 상자 상태를 남기지 않습니다.</summary>
    private void OnDisable()
    {
        this.hideCalculatorImmediately();
        this.ContainerOpenChanged?.Invoke(false);
    }

    /// <summary>새 손님의 주문을 개별 상품 오브젝트로 생성하고 Astra 순서의 화면 전환을 시작합니다.</summary>
    /// <param name="basket">상품 ID, 수량과 이미지가 포함된 장바구니 표시 데이터입니다.</param>
    public void BeginCustomer(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        this.stopAutoSorting();
        this.cancelVacuumItems();
        this.hideCalculatorImmediately();
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

        this.ContainerOpenChanged?.Invoke(false);
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
        this.cancelVacuumItems();
        this.state = ViewState.Locked;
        this.showFrontOnly();
    }

    /// <summary>모든 거래 화면과 생성한 상품을 정리합니다.</summary>
    public void ClearCustomer()
    {
        this.stopAutoSorting();
        this.cancelVacuumItems();
        this.hideCalculatorImmediately();
        if (this.transitionRoutine != null)
        {
            StopCoroutine(this.transitionRoutine);
            this.transitionRoutine = null;
        }

        this.clearItems();
        this.pendingBasket = Array.Empty<CustomerBasketItemViewData>();
        this.ContainerOpenChanged?.Invoke(false);
        this.showFrontOnly();
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
        this.hideCalculatorImmediately();
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
            this.hideCalculatorImmediately();
        }
    }

    /// <summary>가게 단계의 상자 이미지를 이후 쏟기·퇴장 연출에 사용한다. 현재 연출 상태는 유지한다.</summary>
    /// <param name="tilted">쏟는 동안의 이미지.</param>
    /// <param name="empty">쏟은 뒤 퇴장 이미지.</param>
    /// <exception cref="System.ArgumentNullException">단계 이미지 누락.</exception>
    public void SetContainerSprites(Sprite tilted, Sprite empty)
    {
        if (tilted == null) throw new System.ArgumentNullException(nameof(tilted));
        if (empty == null) throw new System.ArgumentNullException(nameof(empty));
        this.tiltedContainerSprite = tilted;
        this.emptyContainerSprite = empty;
    }

    /// <summary>정면, 전환, 쏟기, 분류 순서로 새 손님 작업 화면을 엽니다.</summary>
    /// <returns>Unity 프레임에 걸쳐 진행되는 전환 열거자입니다.</returns>
    private IEnumerator playEntryFlow()
    {
        this.ContainerOpenChanged?.Invoke(true);
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
                slideElapsed += this.PresentationDeltaSeconds;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slideElapsed / slideSeconds));
                sortingRect.anchoredPosition = new Vector2(Mathf.Lerp(-screenWidth, 0f, progress), 0f);
                yield return null;
            }

            sortingRect.anchoredPosition = Vector2.zero;
        }

        if (this.frontView != null) this.frontView.SetActive(false);
        this.setCalculatorVisible(false);
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
        SoundManager.Instance?.PlaySfx(SoundKeys.BoxItemDrop);
        float elapsed = 0f;
        while (elapsed < this.pourSeconds)
        {
            elapsed += this.PresentationDeltaSeconds;
            float t = this.pourSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / this.pourSeconds);

            // 상자를 물품이 쏟아지는 방향으로 조금 더 기울입니다 (-90도 -> -100도)
            if (this.containerImage != null)
            {
                this.containerImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-90f, -100f, t));
            }

            for (int i = 0; i < this.items.Count; i++)
            {
                SaleSortingItemView item = this.items[i];
                if (item == null)
                {
                    continue;
                }

                Vector2 target = this.getInitialSpreadPosition(i, this.items.Count);
                item.Position = Vector2.Lerp(this.getPourStartPosition(i), target, t);
            }

            yield return null;
        }

        // 빈 상자로 이미지 변경 후 페이드아웃 및 퇴장
        if (this.containerImage != null)
        {
            if (this.emptyContainerSprite != null)
            {
                this.containerImage.sprite = this.emptyContainerSprite;
                this.containerImage.preserveAspect = true;
            }

            Vector2 exitStartPos = this.containerImage.rectTransform.anchoredPosition;
            Vector2 exitTargetPos = exitStartPos + new Vector2(-150f, 80f);
            float exitDuration = 0.35f;
            float exitElapsed = 0f;
            Color initialColor = this.containerImage.color;

            while (exitElapsed < exitDuration)
            {
                exitElapsed += this.PresentationDeltaSeconds;
                float exitT = Mathf.Clamp01(exitElapsed / exitDuration);

                this.containerImage.rectTransform.anchoredPosition = Vector2.Lerp(exitStartPos, exitTargetPos, exitT);
                this.containerImage.color = new Color(initialColor.r, initialColor.g, initialColor.b, Mathf.Lerp(1f, 0f, exitT));

                yield return null;
            }

            this.containerImage.gameObject.SetActive(false);
            this.containerImage.rectTransform.localRotation = Quaternion.identity;
            this.containerImage.rectTransform.anchoredPosition = exitStartPos;
            this.containerImage.color = initialColor;
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
        LandingDustEffect dust = this.ensureLandingDustEffect();
        if (dust != null) dust.Stop();

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
            bool dustPlayed = false;
            while (elapsed < ArrivalSeconds)
            {
                elapsed += this.PresentationDeltaSeconds;
                float t = Mathf.Clamp01(elapsed / ArrivalSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                box.anchoredPosition = Vector2.LerpUnclamped(start, destination, eased);

                // DEV-2D-11-01: 착지 순간 10개의 픽셀 먼지 조각이 양옆으로 포물선 비산하는 연출
                if (!dustPlayed && elapsed >= 0.3f)
                {
                    dustPlayed = true;
                    if (dust != null)
                    {
                        dust.Play(box);
                    }
                    SoundManager.Instance?.PlaySfx(SoundKeys.CustomerBoxDrop);
                }

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
        if (this.state != ViewState.FrontWaiting || this.transitionRoutine != null || this.isPresentationBlocked?.Invoke() == true) return;
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
                item.Initialize(line.ItemId, quantityIndex, line.TopViewIcon, this.itemSizePixels, line.DisplayName, this.workArea);
                item.Position = this.getPourStartPosition(unitSequence);
                item.DragStarted += this.handleItemDragStarted;
                item.Dragged += this.handleItemDragged;
                item.DragEnded += this.handleItemDragEnded;
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
            elapsed += this.PresentationDeltaSeconds;
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

    /// <summary>정상 배출된 상품을 Panel 소유로 인수하고 현재 영역을 즉시 판정합니다.</summary>
    private void drainVacuumItems()
    {
        if (this.vacuum == null)
        {
            return;
        }

        IReadOnlyList<SaleSortingItemView> spatItems = this.vacuum.DrainSpatItems();
        for (int index = 0; index < spatItems.Count; index++)
        {
            SaleSortingItemView item = spatItems[index];
            if (item == null || !this.items.Contains(item))
            {
                continue;
            }

            item.Manipulation = SaleSortingItemView.ManipulationState.Idle;
            SaleSortingItemView.SortingState previousState = item.State;
            this.classifyItem(item);
            SoundManager.Instance?.PlaySfx(
                item.State == SaleSortingItemView.SortingState.Excluded && previousState != item.State
                    ? SoundKeys.ItemRemove
                    : SoundKeys.ItemPlace);
        }
    }

    /// <summary>강제 취소 경로에서 내부 상품만 복원하고 배출 대기 이벤트는 폐기합니다.</summary>
    private void cancelVacuumItems()
    {
        if (this.vacuum == null)
        {
            return;
        }

        this.vacuum.CancelAndRestoreItems();
        this.vacuum.DrainSpatItems();
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
                SoundManager.Instance?.PlaySfx(SoundKeys.ItemPickup);
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
        SaleSortingItemView.SortingState previousState = releasedItem.State;
        this.classifyItem(releasedItem);
        releasedItem.Manipulation = SaleSortingItemView.ManipulationState.Idle;
        SoundManager.Instance?.PlaySfx(
            releasedItem.State == SaleSortingItemView.SortingState.Excluded && previousState != releasedItem.State
                ? SoundKeys.ItemRemove
                : SoundKeys.ItemPlace);
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
        this.refreshStatus();
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
        if (item == null || !item.gameObject.activeInHierarchy ||
            item.State == SaleSortingItemView.SortingState.Excluded ||
            item.Manipulation == SaleSortingItemView.ManipulationState.VacuumAttached)
        {
            return;
        }

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

        item.UpdateVisualState();
    }

    /// <summary>작업대 위의 모든 상품 구역 상태를 판정합니다.</summary>
    private void classifyItems()
    {
        for (int index = 0; index < this.items.Count; index++)
        {
            this.classifyItem(this.items[index]);
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
        return this.calculatorPanel != null && this.calculatorPanel.gameObject.activeSelf
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

    /// <summary>물품과 편의 도구의 최종 잡기 상태를 손 커서 표현에 반영합니다.</summary>
    /// <param name="isHolding">물품, 막대 또는 청소기 중 하나를 잡고 있는지 여부입니다.</param>
    /// <param name="allowNewInteraction">포인터가 새 작업대 입력을 시작할 수 있는 위치인지 여부입니다.</param>
    /// <param name="deltaSeconds">손 방향 속도 계산에 사용할 실제 경과 시간입니다.</param>
    private void updateHandCursor(bool isHolding, bool allowNewInteraction, float deltaSeconds)
    {
        if (this.handCursor == null) return;
        Vector2 pointerScreenPosition = this.getPointerScreenPosition();
        bool isInsideScreen = pointerScreenPosition.x >= 0f && pointerScreenPosition.x <= Screen.width &&
            pointerScreenPosition.y >= 0f && pointerScreenPosition.y <= Screen.height;
        bool isInsideWorkArea = RectTransformUtility.RectangleContainsScreenPoint(this.workArea, pointerScreenPosition, null);
        bool visible = Application.isFocused && isInsideScreen && isInsideWorkArea && (allowNewInteraction || isHolding);
        this.handCursor.UpdatePresentation(visible, isHolding, pointerScreenPosition, deltaSeconds);
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
        if (this.state != ViewState.Sorting) return;

        int working = 0;
        int forSale = 0;
        int excluded = 0;
        foreach (SaleSortingItemView item in this.items)
        {
            if (item.State == SaleSortingItemView.SortingState.Working) working++;
            else if (item.State == SaleSortingItemView.SortingState.ForSale) forSale++;
            else excluded++;
        }

        this.setCalculatorVisible(this.CanConfirm && forSale > 0);
        if (this.sortingStatusText != null)
        {
            this.sortingStatusText.text = $"미분류 {working} · 판매 {forSale} · 판매 안함 {excluded}";
        }

        if (this.CanConfirm && forSale == 0 && excluded > 0)
        {
            this.state = ViewState.Locked;
            this.hideCalculatorImmediately();
            this.AllItemsDiscarded?.Invoke();
        }
    }

    /// <summary>기존 개별 상품 오브젝트를 제거합니다.</summary>
    private void clearItems()
    {
        foreach (SaleSortingItemView item in this.items)
        {
            if (item != null)
            {
                item.DragStarted -= this.handleItemDragStarted;
                item.Dragged -= this.handleItemDragged;
                item.DragEnded -= this.handleItemDragEnded;
                Destroy(item.gameObject);
            }
        }

        this.items.Clear();
        this.setCalculatorVisible(false);
        this.dividerMovedItems.Clear();
        this.draggedItem = null;
        this.dragOffset = Vector2.zero;
    }

    /// <summary>아이템 드래그 시작 시 상태를 갱신합니다.</summary>
    private void handleItemDragStarted(SaleSortingItemView item)
    {
        this.stopAutoSorting();
        this.classifyItems();
        this.refreshStatus();
    }

    /// <summary>아이템 드래그 이동 시 상태를 실시간 갱신합니다.</summary>
    private void handleItemDragged(SaleSortingItemView item)
    {
        this.classifyItems();
        this.refreshStatus();
    }

    /// <summary>아이템 드래그 종료(드롭) 시 분류 결과를 최종 확정하고 갱신합니다.</summary>
    private void handleItemDragEnded(SaleSortingItemView item)
    {
        this.classifyItems();
        this.refreshStatus();
    }

    /// <summary>정면 거래 화면만 표시하고 작업 입력을 닫습니다.</summary>
    private void showFrontOnly()
    {
        this.state = ViewState.Hidden;
        if (this.frontView != null)
        {
            this.frontView.SetActive(true);
            // 기존 UI-only 씬은 직렬화된 컨트롤러만 갱신한다. world 씬에 배경 Image를 재생성하지 않는다.
            this.frontView.GetComponent<TimeOfDayUIController>()?.RefreshTime();
        }
        if (this.sortingView != null) this.sortingView.SetActive(false);
        if (this.transitionOverlay != null) this.transitionOverlay.SetActive(false);
        if (this.frontContainerButton != null) this.frontContainerButton.gameObject.SetActive(false);
        this.hideCalculatorImmediately();
        if (this.dividerBar != null) this.dividerBar.SetVisible(false);
        if (this.vacuum != null)
        {
            this.cancelVacuumItems();
            this.vacuum.SetVisible(false);
        }
        if (this.landingDustEffect != null) this.landingDustEffect.Stop();
        this.releaseDraggedItem();
    }

    /// <summary>계산기를 아래 화면 밖으로 열고 닫으며 도착 완료 상태만 입력 라우팅에 알립니다.</summary>
    private void setCalculatorVisible(bool visible)
    {
        if (this.calculatorPanel == null || this.calculatorTargetVisible == visible && this.calculatorSlideRoutine != null) return;
        if (visible && this.isCalculatorOpen || !visible && !this.calculatorPanel.gameObject.activeSelf) return;

        this.calculatorTargetVisible = visible;
        if (!visible) this.setCalculatorInputOpen(false);
        if (this.calculatorSlideRoutine != null) StopCoroutine(this.calculatorSlideRoutine);
        if (visible && !this.calculatorPanel.gameObject.activeSelf)
        {
            this.calculatorPanel.anchoredPosition = this.calculatorOpenPosition;
            this.calculatorClosedPosition = this.getCalculatorClosedPosition();
            this.calculatorPanel.anchoredPosition = this.calculatorClosedPosition;
            this.calculatorPanel.gameObject.SetActive(true);
        }
        else if (!visible)
        {
            this.calculatorClosedPosition = this.getCalculatorClosedPosition();
        }
        this.calculatorSlideRoutine = StartCoroutine(this.slideCalculator(visible));
    }

    /// <summary>현재 위치에서 목표 방향으로 전환해 중간 반전에도 위치가 튀지 않습니다.</summary>
    private IEnumerator slideCalculator(bool visible)
    {
        Vector2 start = this.calculatorPanel.anchoredPosition;
        Vector2 target = visible ? this.calculatorOpenPosition : this.calculatorClosedPosition;
        float fullDistance = Vector2.Distance(this.calculatorOpenPosition, this.calculatorClosedPosition);
        float duration = this.calculatorSlideSeconds * (fullDistance <= 0f ? 0f : Vector2.Distance(start, target) / fullDistance);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += this.PresentationDeltaSeconds;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            this.calculatorPanel.anchoredPosition = Vector2.LerpUnclamped(start, target, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        this.calculatorPanel.anchoredPosition = target;
        this.calculatorSlideRoutine = null;
        if (visible) this.setCalculatorInputOpen(true);
        else this.calculatorPanel.gameObject.SetActive(false);
    }

    /// <summary>계산기 상단이 부모 하단을 완전히 벗어나는 실제 닫힘 좌표를 구합니다.</summary>
    private Vector2 getCalculatorClosedPosition()
    {
        RectTransform boundary = this.calculatorPanel.parent as RectTransform;
        if (boundary == null) return this.calculatorOpenPosition - Vector2.up * this.calculatorPanel.rect.height;

        Vector3[] corners = new Vector3[4];
        this.calculatorPanel.GetWorldCorners(corners);
        float top = corners.Max(corner => boundary.InverseTransformPoint(corner).y);
        return this.calculatorPanel.anchoredPosition + Vector2.up * (boundary.rect.yMin - top - 1f);
    }

    /// <summary>입력 허용 상태를 먼저 갱신한 뒤 재진입 가능한 구독자에게 알립니다.</summary>
    private void setCalculatorInputOpen(bool open)
    {
        if (this.isCalculatorOpen == open) return;
        this.isCalculatorOpen = open;
        this.CalculatorVisibilityChanged?.Invoke(open);
    }

    /// <summary>새 방문·비활성화 경계에서는 진행 중 연출까지 즉시 정리합니다.</summary>
    private void hideCalculatorImmediately()
    {
        if (this.calculatorSlideRoutine != null)
        {
            StopCoroutine(this.calculatorSlideRoutine);
            this.calculatorSlideRoutine = null;
        }
        this.calculatorTargetVisible = false;
        this.setCalculatorInputOpen(false);
        if (this.calculatorPanel == null) return;
        this.calculatorPanel.anchoredPosition = this.calculatorOpenPosition;
        this.calculatorClosedPosition = this.getCalculatorClosedPosition();
        this.calculatorPanel.anchoredPosition = this.calculatorClosedPosition;
        this.calculatorPanel.gameObject.SetActive(false);
    }
}
