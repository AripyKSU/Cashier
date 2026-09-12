using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 게임 진행 로직과 UI 계층을 연결하는 게임 화면 수명 조립 컴포넌트입니다.
/// 진행 규칙은 GameProgress와 DayProgress에 위임하고 이 클래스는 입력·ViewData·화면 수명만 담당합니다.
/// </summary>
public sealed class GameUIController : MonoBehaviour
{
    private GameProgress gameProgress;
    private DayProgress subscribedDay;
    private EconomyRuntime economy;
    private CustomerCatalog customerCatalog;
    private TextDataTable textData;
    private ProgressViewDataFactory viewDataFactory;
    private PreOpenPanelPresenter preOpenPanelPresenter;

    [Header("Presenter references")]
    [SerializeField] private GameDayPresenter gameDayPresenter;
    [SerializeField] private BusinessTimerPresenter businessTimerPresenter;
    [SerializeField] private EconomyStatusPresenter economyStatusPresenter;
    [SerializeField] private CustomerPresenter customerPresenter;
    [SerializeField] private PriceInputPresenter priceInputPresenter;
    [SerializeField] private DailySettlementPresenter dailySettlementPresenter;
    [SerializeField] private DaughterDialoguePresenter daughterDialoguePresenter;
    [SerializeField] private KeypadController keypadController;
    [SerializeField] private GameInputRouter gameInputRouter;
    [SerializeField] private SaleSortingPanel saleSortingPanel;
    /// <summary>세션 현재가·해금·이미지를 전달받는 영업 전 카드 표시.</summary>
    [SerializeField] private PreOpenPanelPresenter preOpenPresenter;
    /// <summary>진행 시각을 표시할 시계. 자체 시간은 사용하지 않는다.</summary>
    [SerializeField] private BusinessClockController businessClock;
    /// <summary>세션 감독관 대사와 연출을 표시하는 독립 패널.</summary>
    [SerializeField] private InspectorPresenter inspectorPresenter;
    /// <summary>직렬화 상태부터 활성·불투명한 전체 화면 초기화 덮개.</summary>
    [SerializeField] private CanvasGroup startupCover;
    /// <summary>덮개 위에서 초기화 실패를 알리는 문구.</summary>
    [SerializeField] private TextMeshProUGUI startupErrorText;

    [Header("Progress panels")]
    [SerializeField] private GameObject preOpenPanel;
    [SerializeField] private GameObject operatingPanel;
    [SerializeField] private GameObject settlementPanel;
    [SerializeField] private GameObject failurePanel;

    [Header("Progress UI fields")]
    [SerializeField] private TextMeshProUGUI priceListText;
    [SerializeField] private TextMeshProUGUI validationText;
    [SerializeField] private TextMeshProUGUI transactionStatusText;
    [SerializeField] private TextMeshProUGUI failureText;
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Progress UI controls")]
    [SerializeField] private Button openBusinessButton;
    [SerializeField] private Button transactionContinueButton;
    [SerializeField] private Button[] keypadButtons;

    /// <summary>독립 설비 상점 프리팹의 표시·입력 어댑터.</summary>
    [SerializeField] private FacilityShopPresenter facilityShopPresenter;
    /// <summary>일일 정산 화면에서만 보이는 설비 진입 버튼.</summary>
    [SerializeField] private Button facilityOpenButton;
    /// <summary>모달 뒤 정산 버튼의 포인터·Submit 입력을 차단한다.</summary>
    [SerializeField] private CanvasGroup settlementInputGroup;

    private bool isPurchasingFacility;
    private bool wasInputRouterEnabled;
    private string facilityFeedback = string.Empty;

    private bool isReady;
    private bool presentationReady;
    private bool hasError;
    private bool isOpeningBusiness;
    private Sprite productPlaceholderSprite;
    private readonly Dictionary<uint, Sprite> appearanceSprites = new Dictionary<uint, Sprite>();
    private readonly Dictionary<uint, Sprite> topViewSprites = new Dictionary<uint, Sprite>();
    private readonly Dictionary<uint, Sprite> inspectorSprites = new Dictionary<uint, Sprite>();
    private readonly Dictionary<uint, Sprite> daughterSprites = new Dictionary<uint, Sprite>();

    /// <summary>개인 씬에서 실제 FIFO 대기열을 사용할 때만 켠다. 공유 prefab 기본값은 false.</summary>
    [SerializeField] private bool useCustomerQueue;
    /// <summary>로컬 퇴장 이동과 마지막 정산 표시 대기에 사용하는 단일 시간(초).</summary>
    [SerializeField, Min(0.01f)] private float queueExitSeconds = 0.45f;
    private float queueExitRemaining;
    private bool isSettlementPresentationPending;
    /// <summary>로컬 외형 퇴장에 적용할 검증된 시간(초).</summary>
    public float QueueExitSeconds => this.queueExitSeconds;
    /// <summary>모델 정산은 완료했지만 마지막 퇴장 표시를 기다리는 상태.</summary>
    public bool IsSettlementPresentationPending => this.isSettlementPresentationPending;
    /// <summary>로컬 표현이 관찰하는 현재 하루. 비동기 초기화 전에는 null.</summary>
    public DayProgress CurrentDayProgress => this.gameProgress?.CurrentDayProgress;
    /// <summary>표현 시간이 멈춰야 하는 일시정지·기술 오류 상태.</summary>
    public bool IsPresentationPaused => this.hasError || !this.isActiveAndEnabled || this.subscribedDay?.IsPaused == true;
    /// <summary>월드 표시가 정렬·가시성만 관찰하는 기존 전면 UI 영역.</summary>
    public RectTransform FrontView => this.saleSortingPanel.FrontView;
    /// <summary>진행 시간이 이미 반영된 표시 전용 시계.</summary>
    public BusinessClockController BusinessClock => this.businessClock;

    /// <summary>설비 패널의 실제 활성 상태가 열린 여부의 권위다.</summary>
    private bool IsFacilityShopOpen => facilityShopPresenter != null && facilityShopPresenter.gameObject.activeSelf;

    /// <summary>
    /// 구형 배경을 숨기고 비동기 준비 전 입력과 자체 시계 진행을 차단합니다. StartupCover는 유지합니다.
    /// </summary>
    private void Awake()
    {
        Transform bg = this.transform.Find("Root/Background");
        if (bg == null) bg = this.transform.Find("ProgressCanvas/Root/Background");
        if (bg != null && bg.gameObject.activeSelf)
        {
            bg.gameObject.SetActive(false);
        }
        if (gameInputRouter != null) gameInputRouter.enabled = false;
        if (openBusinessButton != null) openBusinessButton.interactable = false;
        if (inspectorPresenter != null) inspectorPresenter.gameObject.SetActive(false);
        if (businessClock != null) businessClock.DisplayTime(BusinessHours.OpenMinutes);
    }

    /// <summary>같은 화면을 다시 켜면 세션에 남아 있는 대사를 재표시한다.</summary>
    private void OnEnable()
    {
        if (isReady && !hasError) refreshAllViews();
    }

    /// <summary>Scene 진입 후 부트스트랩된 런타임을 확인하고 UI와 진행을 초기화합니다.</summary>
    private async void Start()
    {
        try
        {
            if (DataTableManager.Instance == null || GameSessionManager.Instance == null)
            {
                throw new InvalidOperationException("InitScene부터 실행해 진행 런타임을 준비해야 합니다.");
            }

            await DataTableManager.Instance.EnsureDataLoadedAsync()
                .AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

            this.economy = GameSessionManager.Instance.Economy;
            this.customerCatalog = DataTableManager.Instance.Customers;
            this.textData = DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text);
            if (this.customerCatalog == null || this.textData == null)
            {
                throw new InvalidOperationException("손님 또는 텍스트 데이터가 준비되지 않았습니다.");
            }

            IReadOnlyDictionary<uint, Sprite> productSprites = await this.loadDisplaySpritesAsync();
            this.viewDataFactory = new ProgressViewDataFactory(
                this.customerCatalog,
                this.textData,
                productSprites, GameSessionManager.Instance.IsFacilityActive, this.topViewSprites, this.appearanceSprites,
                DataTableManager.Instance.GetDB<DailyGuidelineDataTable>(DataTableType.DailyGuideline));
            this.preOpenPanelPresenter = this.preOpenPanel != null
                ? this.preOpenPanel.GetComponentInChildren<PreOpenPanelPresenter>(true)
                : null;
            this.openBusinessButton = this.preOpenPanelPresenter?.OpenBusinessButton;

            this.validateUiReferences();
            if (this.businessClock != null) this.businessClock.StopClock();
            if (this.useCustomerQueue) this.saleSortingPanel.SetPauseQuery(() => this.IsPresentationPaused);
            this.subscribeUi();
            this.gameProgress = new GameProgress(
                GameSessionManager.Instance,
                this.customerCatalog,
                DataTableManager.Instance.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance),
                new System.Random(), useCustomerQueue: this.useCustomerQueue);
            this.subscribeProgress();
            this.gameProgress.Start();
            this.isReady = true;
            this.refreshAllViews();
            Canvas.ForceUpdateCanvases();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            Canvas.ForceUpdateCanvases();
            this.presentationReady = true;
            this.startupCover.blocksRaycasts = false;
            this.startupCover.interactable = false;
            this.startupCover.gameObject.SetActive(false);
            this.refreshAllViews();
        }
        catch (OperationCanceledException)
        {
            // Scene이 파괴되는 동안의 취소는 정상적인 수명 종료입니다.
        }
        catch (Exception exception)
        {
            this.showError(exception);
        }
    }

    /// <summary>프레임 경과 시간을 진행 로직에 전달하고 UI 스냅샷을 갱신합니다.</summary>
    private void Update()
    {
        if (!this.isReady || this.hasError || this.gameProgress == null)
        {
            return;
        }

        try
        {
            if (this.useCustomerQueue && this.queueExitRemaining > 0 && !this.IsPresentationPaused)
            {
                this.queueExitRemaining = Mathf.Max(0, this.queueExitRemaining - Time.deltaTime);
                if (this.isSettlementPresentationPending && this.queueExitRemaining <= 0)
                {
                    this.isSettlementPresentationPending = false;
                    this.renderSettlement(this.subscribedDay.SettlementResult.Value);
                    this.refreshAllViews();
                }
            }
            if (this.gameProgress.State == GameProgressState.DayInProgress)
            {
                this.gameProgress.Tick(Time.deltaTime);
                if (this.isTransactionResultAwaitingAdvance() && this.wasPointerClickThisFrame())
                {
                    this.runProgressAction(this.gameProgress.CompleteTransactionResult);
                }
                this.refreshFrameViews();
            }
        }
        catch (Exception exception)
        {
            this.showError(exception);
        }
    }

    /// <summary>상품 기본·탑뷰와 손님 외형 Sprite를 Resource FK별로 한 번 로드한다.</summary>
    /// <returns>상품 기본 Sprite 사전. 탑뷰·외형도 같은 화면 수명에 보관하며 상품의 두 FK가 빈 경우만 흰색을 사용한다.</returns>
    /// <exception cref="InvalidOperationException">리소스 시스템 또는 ResourceDataTable이 준비되지 않은 경우 발생합니다.</exception>
    private async UniTask<IReadOnlyDictionary<uint, Sprite>> loadDisplaySpritesAsync()
    {
        if (ResourceManager.Instance == null)
        {
            throw new InvalidOperationException("ResourceManager가 준비되지 않았습니다.");
        }

        ResourceDataTable resources = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
        if (resources == null)
        {
            throw new InvalidOperationException("ResourceDataTable이 준비되지 않았습니다.");
        }

        var spritesByResource = new Dictionary<uint, Sprite>();
        var spritesByProduct = new Dictionary<uint, Sprite>();
        // 공용 manager가 핸들을 소유하고 이 화면은 동일 Resource FK의 로드 결과만 재사용한다.
        foreach (ProductData product in this.customerCatalog.Products.Rows.Values)
        {
            if (!product.ImageResourceIdx.HasValue)
            {
                Debug.LogWarning($"[GameUIController] 상품 {product.Idx}의 기본·탑뷰 이미지가 비어 있어 임시 흰색 이미지를 사용합니다.", this);
                spritesByProduct.Add(product.Idx, this.getProductPlaceholderSprite());
                this.topViewSprites.Add(product.Idx, this.getProductPlaceholderSprite());
                continue;
            }
            spritesByProduct.Add(product.Idx, await this.loadSpriteAsync(product.ImageResourceIdx.Value, resources, spritesByResource));
            this.topViewSprites.Add(product.Idx, await this.loadSpriteAsync(product.TopViewImageResourceIdx.Value, resources, spritesByResource));
        }
        foreach (CustomerAppearanceData appearance in this.customerCatalog.Appearances.Rows.Values)
            this.appearanceSprites.Add(appearance.Idx, await this.loadSpriteAsync(appearance.ImageResourceIdx, resources, spritesByResource));
        foreach (InspectorEventData inspector in DataTableManager.Instance.GetDB<InspectorEventDataTable>(DataTableType.InspectorEvent).Rows.Values)
            if (!inspectorSprites.ContainsKey(inspector.PortraitResourceIdx))
                inspectorSprites.Add(inspector.PortraitResourceIdx, await loadSpriteAsync(inspector.PortraitResourceIdx, resources, spritesByResource));
        foreach (DaughterAppearanceData appearance in DataTableManager.Instance.GetDB<DaughterAppearanceDataTable>(DataTableType.DaughterAppearance).Rows.Values)
            if (!daughterSprites.ContainsKey(appearance.ResourceIdx))
                daughterSprites.Add(appearance.ResourceIdx, await loadSpriteAsync(appearance.ResourceIdx, resources, spritesByResource));
        return spritesByProduct;
    }

    /// <summary>상품·외형의 필수 FK를 로드한다. 실제 로드 실패를 placeholder로 바꾸지 않는다.</summary>
    /// <param name="resourceIdx">Resource PK.</param><param name="resources">검증된 Resource 테이블.</param>
    /// <param name="loaded">이번 화면에서 이미 로드한 Sprite.</param><returns>로드 완료 Sprite.</returns>
    /// <exception cref="InvalidOperationException">FK 또는 로드 결과 누락.</exception>
    private async UniTask<Sprite> loadSpriteAsync(uint resourceIdx, ResourceDataTable resources, Dictionary<uint, Sprite> loaded)
    {
        if (loaded.TryGetValue(resourceIdx, out var sprite)) return sprite;
        string address = resources.GetResourcePath(resourceIdx);
        if (string.IsNullOrWhiteSpace(address)) throw new InvalidOperationException($"Resource FK {resourceIdx}가 없습니다.");
        sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(address, this.GetCancellationTokenOnDestroy());
        if (sprite == null) throw new InvalidOperationException($"Resource {resourceIdx}, address={address}: Sprite 로드 결과가 없습니다.");
        loaded.Add(resourceIdx, sprite);
        return sprite;
    }

    /// <summary>상품 이미지 누락 시 사용할 임시 흰색 Sprite를 생성하고 재사용합니다.</summary>
    /// <returns>1x1 흰색 텍스처를 기반으로 한 임시 Sprite입니다.</returns>
    private Sprite getProductPlaceholderSprite()
    {
        if (this.productPlaceholderSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            this.productPlaceholderSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            this.productPlaceholderSprite.name = "TemporaryProductPlaceholder";
        }

        return this.productPlaceholderSprite;
    }

    /// <summary>진행 이벤트와 UI 입력 이벤트를 해제합니다.</summary>
    private void OnDestroy()
    {
        this.isSettlementPresentationPending = false;
        this.queueExitRemaining = 0;
        this.subscribedDay?.StopQueue();
        if (this.useCustomerQueue && this.saleSortingPanel != null) this.saleSortingPanel.SetPauseQuery(null);
        if (this.economy != null) this.economy.FinanceService.BalanceChanged -= this.handleFacilityBalanceChanged;
        this.unsubscribeProgress();
        this.unsubscribeUi();
        if (this.productPlaceholderSprite != null)
        {
            Destroy(this.productPlaceholderSprite);
            this.productPlaceholderSprite = null;
        }
    }

    /// <summary>개인 대기열도 화면 초기화 때 로드한 같은 외형을 재사용한다. 로드나 상태 변경은 하지 않는다.</summary>
    /// <param name="appearanceIdx">외형 PK.</param><returns>공용 로드 결과 Sprite.</returns>
    /// <exception cref="InvalidOperationException">초기화 전 또는 잘못된 외형 PK.</exception>
    public Sprite GetCustomerAppearanceSprite(uint appearanceIdx)
    {
        if (!this.appearanceSprites.TryGetValue(appearanceIdx, out var sprite) || sprite == null)
            throw new InvalidOperationException($"외형 {appearanceIdx}의 Sprite가 준비되지 않았습니다.");
        return sprite;
    }

    /// <summary>씬에 직렬화된 Presenter와 진행 필수 UI 참조가 연결됐는지 확인합니다.</summary>
    /// <exception cref="InvalidOperationException">진행에 필요한 씬 참조가 누락된 경우 발생합니다.</exception>
    private void validateUiReferences()
    {
        if (inspectorPresenter == null || startupCover == null || startupErrorText == null)
            throw new InvalidOperationException("감독관 패널 또는 초기화 덮개 참조가 누락되었습니다.");
        inspectorPresenter.ValidateReferences();
        daughterDialoguePresenter?.ValidateReferences();
        if (this.useCustomerQueue && (float.IsNaN(this.queueExitSeconds) || float.IsInfinity(this.queueExitSeconds) || this.queueExitSeconds <= 0))
            throw new InvalidOperationException("큐 퇴장 시간은 유한한 양수여야 합니다.");
        if (this.gameDayPresenter == null
            || this.businessTimerPresenter == null
            || this.economyStatusPresenter == null
            || this.customerPresenter == null
            || this.priceInputPresenter == null
            || this.dailySettlementPresenter == null
            || this.daughterDialoguePresenter == null
            || this.keypadController == null
            || this.gameInputRouter == null
            || this.saleSortingPanel == null
            || this.preOpenPanelPresenter == null
            || this.preOpenPanel == null
            || this.operatingPanel == null
            || this.settlementPanel == null
            || this.failurePanel == null
            || this.openBusinessButton == null
            || this.transactionContinueButton == null
            || this.facilityShopPresenter == null
            || this.facilityOpenButton == null
            || this.settlementInputGroup == null)
        {
            throw new InvalidOperationException("게임 UI의 Presenter, 입력 라우터 또는 패널 참조가 누락되었습니다.");
        }
    }

    /// <summary>씬에 배치된 Presenter와 버튼의 입력 이벤트를 구독합니다.</summary>
    private void subscribeUi()
    {
        inspectorPresenter.NextRequested += handleInspectorNext;
        inspectorPresenter.ExitCompleted += handleInspectorExit;
        inspectorPresenter.Failed += showError;
        this.facilityOpenButton.onClick.AddListener(this.handleFacilityOpenClicked);
        this.facilityShopPresenter.OnPurchaseRequested += this.handleFacilityPurchaseRequested;
        this.facilityShopPresenter.OnCloseRequested += this.handleFacilityCloseRequested;
        this.priceInputPresenter.OnPriceConfirmed += this.handlePriceConfirmed;
        this.priceInputPresenter.OnInputCancelled += this.handleInputCancelled;
        this.businessTimerPresenter.OnPauseRequested += this.handlePauseRequested;
        this.businessTimerPresenter.OnResumeRequested += this.handleResumeRequested;
        this.dailySettlementPresenter.OnNextStepRequested += this.handleSettlementNextRequested;
        this.keypadController.OnPriceChanged += this.handlePriceChanged;
        this.gameInputRouter.OnConfirmRequested += this.handleKeyboardConfirmRequested;
        this.gameInputRouter.OnContinueRequested += this.handleTransactionContinueClicked;
        this.saleSortingPanel.CalculatorVisibilityChanged += this.handleCalculatorVisibilityChanged;
        this.saleSortingPanel.SortingStarted += this.handleSortingStarted;
        this.openBusinessButton.onClick.AddListener(this.handleOpenBusinessClicked);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (this.preOpenPanelPresenter.DebugDay10Button != null)
            this.preOpenPanelPresenter.DebugDay10Button.onClick.AddListener(this.handleDebugDay10Clicked);
        if (this.preOpenPanelPresenter.DebugDay20Button != null)
            this.preOpenPanelPresenter.DebugDay20Button.onClick.AddListener(this.handleDebugDay20Clicked);
        if (this.preOpenPanelPresenter.DebugDay30Button != null)
            this.preOpenPanelPresenter.DebugDay30Button.onClick.AddListener(this.handleDebugDay30Clicked);
#endif
        this.transactionContinueButton.onClick.AddListener(this.handleTransactionContinueClicked);
    }

    /// <summary>씬 UI 입력 이벤트를 해제합니다.</summary>
    private void unsubscribeUi()
    {
        if (inspectorPresenter != null)
        {
            inspectorPresenter.NextRequested -= handleInspectorNext;
            inspectorPresenter.ExitCompleted -= handleInspectorExit;
            inspectorPresenter.Failed -= showError;
        }
        if (this.facilityOpenButton != null) this.facilityOpenButton.onClick.RemoveListener(this.handleFacilityOpenClicked);
        if (this.facilityShopPresenter != null)
        {
            this.facilityShopPresenter.OnPurchaseRequested -= this.handleFacilityPurchaseRequested;
            this.facilityShopPresenter.OnCloseRequested -= this.handleFacilityCloseRequested;
        }
        if (this.priceInputPresenter != null)
        {
            this.priceInputPresenter.OnPriceConfirmed -= this.handlePriceConfirmed;
            this.priceInputPresenter.OnInputCancelled -= this.handleInputCancelled;
        }

        if (this.businessTimerPresenter != null)
        {
            this.businessTimerPresenter.OnPauseRequested -= this.handlePauseRequested;
            this.businessTimerPresenter.OnResumeRequested -= this.handleResumeRequested;
        }

        if (this.dailySettlementPresenter != null)
        {
            this.dailySettlementPresenter.OnNextStepRequested -= this.handleSettlementNextRequested;
        }

        if (this.keypadController != null)
        {
            this.keypadController.OnPriceChanged -= this.handlePriceChanged;
        }
        if (this.gameInputRouter != null)
        {
            this.gameInputRouter.OnConfirmRequested -= this.handleKeyboardConfirmRequested;
            this.gameInputRouter.OnContinueRequested -= this.handleTransactionContinueClicked;
        }
        if (this.saleSortingPanel != null)
        {
            this.saleSortingPanel.CalculatorVisibilityChanged -= this.handleCalculatorVisibilityChanged;
            this.saleSortingPanel.SortingStarted -= this.handleSortingStarted;
        }

        if (this.openBusinessButton != null)
        {
            this.openBusinessButton.onClick.RemoveListener(this.handleOpenBusinessClicked);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (this.preOpenPanelPresenter != null)
        {
            if (this.preOpenPanelPresenter.DebugDay10Button != null)
                this.preOpenPanelPresenter.DebugDay10Button.onClick.RemoveListener(this.handleDebugDay10Clicked);
            if (this.preOpenPanelPresenter.DebugDay20Button != null)
                this.preOpenPanelPresenter.DebugDay20Button.onClick.RemoveListener(this.handleDebugDay20Clicked);
            if (this.preOpenPanelPresenter.DebugDay30Button != null)
                this.preOpenPanelPresenter.DebugDay30Button.onClick.RemoveListener(this.handleDebugDay30Clicked);
        }
#endif

        if (this.transactionContinueButton != null)
        {
            this.transactionContinueButton.onClick.RemoveListener(this.handleTransactionContinueClicked);
        }

    }

    /// <summary>진행 이벤트를 구독합니다.</summary>
    private void subscribeProgress()
    {
        this.gameProgress.StateChanged += this.handleGameStateChanged;
        this.gameProgress.DayStarted += this.handleDayStarted;
    }

    /// <summary>진행 이벤트와 현재 하루의 이벤트를 해제합니다.</summary>
    private void unsubscribeProgress()
    {
        if (this.gameProgress != null)
        {
            this.gameProgress.StateChanged -= this.handleGameStateChanged;
            this.gameProgress.DayStarted -= this.handleDayStarted;
        }

        if (this.subscribedDay != null)
        {
            this.subscribedDay.StateChanged -= this.handleDayStateChanged;
            this.subscribedDay.CustomerStarted -= this.handleCustomerStarted;
            this.subscribedDay.CustomerDeparted -= this.handleCustomerDeparted;
            this.subscribedDay.TransactionCompleted -= this.handleTransactionCompleted;
            this.subscribedDay.SettlementStarted -= this.handleSettlementStarted;
        }
    }

    /// <summary>새 하루의 상태와 결과 이벤트를 UI 어댑터에 연결합니다.</summary>
    /// <param name="day">새로 시작된 하루 진행 인스턴스입니다.</param>
    private void handleDayStarted(DayProgress day)
    {
        if (this.subscribedDay != null)
        {
            this.subscribedDay.StateChanged -= this.handleDayStateChanged;
            this.subscribedDay.CustomerStarted -= this.handleCustomerStarted;
            this.subscribedDay.CustomerDeparted -= this.handleCustomerDeparted;
            this.subscribedDay.TransactionCompleted -= this.handleTransactionCompleted;
            this.subscribedDay.SettlementStarted -= this.handleSettlementStarted;
        }

        this.subscribedDay = day;
        this.isOpeningBusiness = false;
        this.subscribedDay.StateChanged += this.handleDayStateChanged;
        this.subscribedDay.CustomerStarted += this.handleCustomerStarted;
        this.subscribedDay.CustomerDeparted += this.handleCustomerDeparted;
        this.subscribedDay.TransactionCompleted += this.handleTransactionCompleted;
        this.subscribedDay.SettlementStarted += this.handleSettlementStarted;
        this.renderPreOpen(day);
        this.refreshAllViews();
    }

    /// <summary>전체 진행 상태 변경을 화면 표시 상태에 반영합니다.</summary>
    /// <param name="state">변경된 전체 진행 상태입니다.</param>
    private void handleGameStateChanged(GameProgressState state)
    {
        if (state != GameProgressState.DayInProgress && this.IsFacilityShopOpen) this.closeFacilityShop();
        if (state == GameProgressState.Failed)
        {
            this.gameInputRouter.enabled = false;
            this.failureText.text = "영업권을 잃었습니다.\n유지비·벌금의 미납 유예기간이 끝났습니다.\n새 게임에서 다시 시작할 수 있습니다.";
            this.setPanelVisibility(this.preOpenPanel, false);
            this.setPanelVisibility(this.operatingPanel, false);
            this.setPanelVisibility(this.settlementPanel, false);
            this.setPanelVisibility(this.failurePanel, true);
        }
        else if (state == GameProgressState.Completed)
        {
            this.gameInputRouter.enabled = false;
            this.setPanelVisibility(this.settlementPanel, false);
            this.openEndingAsync().Forget();
        }
        else if (state == GameProgressState.DayInProgress)
        {
            this.setPanelVisibility(this.failurePanel, false);
            this.refreshAllViews();
        }
    }

    /// <summary>하루 상태 변경을 화면과 입력 가능 상태에 반영합니다.</summary>
    /// <param name="state">변경된 하루 진행 상태입니다.</param>
    private void handleDayStateChanged(DayProgressState state)
    {
        if (state != DayProgressState.PreOpen) this.isOpeningBusiness = false;
        this.refreshAllViews();
    }

    /// <summary>새 손님 데이터를 Presenter에 전달합니다.</summary>
    /// <param name="visit">가격 입력을 기다리는 새 손님 방문입니다.</param>
    private void handleCustomerStarted(CustomerVisit visit)
    {
        CustomerViewData viewData = this.viewDataFactory.CreateCustomerViewData(visit);
        this.customerPresenter.UpdateView(viewData);
        this.saleSortingPanel.BeginCustomer(viewData.Basket);
        this.transactionContinueButton.gameObject.SetActive(false);
        this.transactionStatusText.text = "Enter the total price for the basket.";
        this.refreshRuntimeViews();
    }

    /// <summary>빈 계산대에서도 이전 외형·대사·박스·상품이 남지 않도록 정리한다.</summary>
    /// <param name="visit">거래 확인 후 퇴장한 방문. 새 방문을 생성하지 않는다.</param>
    private void handleCustomerDeparted(CustomerVisit visit)
    {
        if (this.useCustomerQueue) this.queueExitRemaining = this.queueExitSeconds;
        this.customerPresenter.UpdateView(CustomerViewData.Empty);
        this.saleSortingPanel.ClearCustomer();
        this.transactionStatusText.text = "다음 손님을 기다리는 중입니다.";
    }

    /// <summary>거래 결과를 손님 대사에 반영하고 마우스 또는 Enter 입력을 기다립니다.</summary>
    /// <param name="visit">수락 또는 거절 판정이 완료된 손님 방문입니다.</param>
    private void handleTransactionCompleted(CustomerVisit visit)
    {
        this.saleSortingPanel.ShowTransactionResult();
        this.customerPresenter.UpdateView(this.viewDataFactory.CreateCustomerViewData(visit));
        this.transactionContinueButton.gameObject.SetActive(false);
        this.transactionStatusText.text = visit.WasAccepted == true
            ? "ACCEPTED · income applied"
            : "REJECTED · no income";
        this.refreshRuntimeViews();
    }

    /// <summary>일일 집계 결과를 정산 Presenter에 전달합니다.</summary>
    /// <param name="result">미납과 유예 조건까지 포함한 최종 하루 정산 결과입니다.</param>
    private void handleSettlementStarted(DailySettlementResult result)
    {
        if (this.gameProgress.State == GameProgressState.Failed) return;
        if (this.useCustomerQueue && this.queueExitRemaining > 0)
        {
            this.isSettlementPresentationPending = true;
            this.refreshAllViews();
            return;
        }
        this.settlementPanel.SetActive(true);
        this.operatingPanel.SetActive(false);
        this.renderSettlement(result);
        this.refreshAllViews();
    }

    /// <summary>확정된 정산 통계는 그대로 두고 현재 잔액을 다시 표시한다.</summary>
    /// <param name="result">미납과 유예까지 확정된 최종 정산 결과입니다.</param>
    private void renderSettlement(DailySettlementResult result)
    {
        int finalReputationDelta = this.subscribedDay.DailyReputationResult.HasValue
            ? this.subscribedDay.DailyReputationResult.Value.FinalDelta
            : 0;
        this.dailySettlementPresenter.UpdateView(this.viewDataFactory.CreateDailySettlementViewData(
            this.subscribedDay.Day,
            result,
            finalReputationDelta,
            this.subscribedDay.SuccessfulSales,
            this.subscribedDay.RefusedCustomers,
            this.subscribedDay.DepartedCustomers,
            this.economy.QueryService.CurrentBalance));
        this.dailySettlementPresenter.ConfigureEnding(this.subscribedDay.Day == 31,
            this.gameProgress.HasCitizenship, this.gameProgress.WasLastSettlementUnpaidGameOverExempted);
        if (!this.subscribedDay.DaughterDialogueResult.HasValue)
            throw new InvalidOperationException("정산 화면에 표시할 딸 대사 결과가 없습니다.");
        this.daughterDialoguePresenter.UpdateView(this.viewDataFactory.CreateDaughterDialogueViewData(
            this.subscribedDay.DaughterDialogueResult.Value, this.daughterSprites));
    }

    /// <summary>날짜를 완료하기 전의 일일 정산에서만 설비 UI를 열 수 있다.</summary>
    /// <returns>상납 화면을 포함한 다른 진행 단계는false.</returns>
    private bool canOpenFacilityShop() => this.isReady && !this.hasError &&
        !this.isSettlementPresentationPending && !this.dailySettlementPresenter.IsFinalConfirmationOpen &&
        this.gameProgress.State == GameProgressState.DayInProgress && this.subscribedDay?.State == DayProgressState.Settlement;

    /// <summary>현재 설비 상태를 읽고 뒤 정산·키보드 입력을 차단한다.</summary>
    private void handleFacilityOpenClicked()
    {
        if (!this.canOpenFacilityShop() || this.IsFacilityShopOpen) return;
        try
        {
            var citizenship = System.Linq.Enumerable.Single(
                DataTableManager.Instance.GetDB<FacilityDataTable>(DataTableType.Facility).Rows.Values,
                row => row.UpgradeKind == FacilityUpgradeKind.Citizenship);
            long shortfall = Math.Max(0, citizenship.PurchasePrice - this.economy.QueryService.CurrentBalance);
            this.facilityFeedback = this.gameProgress.HasCitizenship ? "시민권 보유 · 마지막 날 최종 확인 시 엔딩을 판정합니다."
                : $"시민권 {citizenship.PurchasePrice:N0} G · 부족액 {shortfall:N0} G · 31일차 정산까지 구매 가능";
            this.wasInputRouterEnabled = this.gameInputRouter.enabled;
            this.gameInputRouter.enabled = false;
            this.settlementInputGroup.interactable = false;
            this.settlementInputGroup.blocksRaycasts = false;
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            this.facilityShopPresenter.gameObject.SetActive(true);
            this.economy.FinanceService.BalanceChanged += this.handleFacilityBalanceChanged;
            this.refreshFacilityShop();
        }
        catch (Exception exception) { this.showError(exception); }
    }

    /// <summary>구매 중에는 닫기 요청을 무시한다. 닫기는 날짜를 바꾸지 않는다.</summary>
    private void handleFacilityCloseRequested()
    {
        if (!this.isPurchasingFacility) this.closeFacilityShop();
    }

    /// <summary>모달 수명의 구독과 입력 잠금을 해제한다.</summary>
    private void closeFacilityShop()
    {
        if (!this.IsFacilityShopOpen) return;
        this.economy.FinanceService.BalanceChanged -= this.handleFacilityBalanceChanged;
        this.facilityShopPresenter.gameObject.SetActive(false);
        this.settlementInputGroup.interactable = !this.hasError;
        this.settlementInputGroup.blocksRaycasts = true;
        this.gameInputRouter.enabled = this.wasInputRouterEnabled;
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>PK만 구매 API에 전달하고 알림 오류 이후에도 실제 차감·보유 상태를 다시 읽는다.</summary>
    /// <param name="facilityIdx">요청된 설비 PK.</param>
    private void handleFacilityPurchaseRequested(uint facilityIdx)
    {
        if (!this.canOpenFacilityShop() || !this.IsFacilityShopOpen || this.isPurchasingFacility) return;
        this.isPurchasingFacility = true;
        Exception failure = null;
        try
        {
            this.facilityShopPresenter.SetInteractionEnabled(false, false);
            this.gameProgress.TryPurchaseFacility(facilityIdx, out var result);
            this.facilityFeedback = result.Status switch
            {
                FacilityPurchaseStatus.Purchased => result.ActivationDay.HasValue &&
                    result.ActivationDay.Value > GameSessionManager.Instance.ElapsedDays
                    ? $"구매 완료 · {result.PaidAmount:N0} G · 다음 영업일부터 적용"
                    : $"구매 완료 · {result.PaidAmount:N0} G · 즉시 적용되었습니다.",
                FacilityPurchaseStatus.AlreadyOwned => "이미 구매한 설비입니다. 추가 결제하지 않았습니다.",
                FacilityPurchaseStatus.InsufficientFunds => "보유금이 부족합니다. 결제하지 않았습니다.",
                FacilityPurchaseStatus.StageLocked => "현재 가게 단계에서 잠긴 업그레이드입니다.",
                _ => throw new InvalidOperationException("설비 구매 결과가 유효하지 않습니다.")
            };
        }
        catch (Exception exception)
        {
            failure = exception;
            this.facilityFeedback = "처리 오류 · 아래 보유 상태를 확인하세요. 자동 재결제·환불하지 않습니다.";
        }
        finally
        {
            this.isPurchasingFacility = false;
            try { this.refreshFacilityShop(); }
            catch (Exception exception) { failure = failure == null ? exception : new AggregateException(failure, exception); }
            if (failure != null) this.showError(failure);
        }
    }

    /// <summary>패널이 열린 동안 외부 잔액 변경을 반영하되 구매 중 재진입 렌더를 미룬다.</summary>
    /// <param name="change">잔액 변경 알림. 표시값은 세션에서 다시 읽는다.</param>
    private void handleFacilityBalanceChanged(FinanceChangeResult change)
    {
        if (!this.IsFacilityShopOpen || this.isPurchasingFacility) return;
        try { this.refreshFacilityShop(); }
        catch (Exception exception) { this.showError(exception); }
    }

    /// <summary>설비와 경제 표시를 새로 읽되 정산 매출·비용은 확정 결과를 그대로 표시한다.</summary>
    private void refreshFacilityShop()
    {
        var session = GameSessionManager.Instance;
        this.facilityShopPresenter.UpdateView(this.viewDataFactory.CreateFacilityShopViewData(
            DataTableManager.Instance.GetDB<FacilityDataTable>(DataTableType.Facility).Rows,
            session.FacilityActivationDays, session.CurrentStoreStage, session.ElapsedDays,
            this.economy.QueryService.CurrentBalance), this.facilityFeedback);
        this.facilityShopPresenter.SetInteractionEnabled(!this.hasError && !this.isPurchasingFacility, !this.isPurchasingFacility);
        if (this.subscribedDay.SettlementResult.HasValue) this.renderSettlement(this.subscribedDay.SettlementResult.Value);
        this.economyStatusPresenter.UpdateView(new EconomyStatusViewData(
            this.economy.QueryService.CurrentBalance, this.economy.QueryService.DailySaleIncome));
    }

    /// <summary>영업 전 버튼 요청을 하루 진행에 전달합니다.</summary>
    private void handleOpenBusinessClicked()
    {
        if (!this.isReady || this.hasError || this.isOpeningBusiness ||
            this.subscribedDay == null || this.subscribedDay.State != DayProgressState.PreOpen)
        {
            return;
        }

        this.isOpeningBusiness = true;
        this.openBusinessButton.interactable = false;
        try
        {
            // 전환 직전에 동일한 세션 snapshot을 다시 검증해 잘못된 콘텐츠로 영업을 시작하지 않습니다.
            this.preOpenPanelPresenter.UpdateView(this.viewDataFactory.CreatePreOpenGuidelineViewData(
                this.subscribedDay.Day,
                GameSessionManager.Instance.EnsureDailyPrices(),
                GameSessionManager.Instance.DailyGuidelines,
                false));
            this.gameProgress.OpenBusiness();
            this.refreshAllViews();
        }
        catch (Exception exception)
        {
            this.showError(exception);
        }
        finally
        {
            if (!this.hasError && this.subscribedDay?.State == DayProgressState.PreOpen)
            {
                this.isOpeningBusiness = false;
                this.refreshPanelVisibility();
            }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>엔딩 수동 검증용 자금 버튼의 사용 가능 상태. 종료 결과 확정 후에는 지급하지 않는다.</summary>
    private bool CanGrantTestFunds => this.isReady && this.presentationReady && !this.hasError &&
        !this.isPurchasingFacility && this.economy != null &&
        this.gameProgress?.State == GameProgressState.DayInProgress &&
        GameSessionManager.Instance != null && !GameSessionManager.Instance.EndingResult.HasValue &&
        this.economy.FinanceService.CurrentBalance <= long.MaxValue - 100_000;

    /// <summary>임시 플레이 테스트 버튼. 검증 종료 시 이 개발 전용 블록을 제거한다.</summary>
    private void OnGUI()
    {
        if (!this.CanGrantTestFunds) return;
        if (GUI.Button(new Rect(12, 6, 220, 32), "TEST +100,000 G"))
            this.grantTestFunds();
    }

    /// <summary>기존 잔액 API로 10만G를 지급한다. 거래 집계를 호출하지 않아 매출·명성·도덕성은 유지한다.</summary>
    private void grantTestFunds()
    {
        if (!this.CanGrantTestFunds) return;
        if (this.IsFacilityShopOpen) this.facilityFeedback = "테스트 자금 100,000 G 지급";
        this.economy.FinanceService.AddIncome(100_000, FinanceChangeReason.None);
        if (!this.IsFacilityShopOpen && this.subscribedDay?.SettlementResult.HasValue == true)
            this.renderSettlement(this.subscribedDay.SettlementResult.Value);
        this.refreshAllViews();
    }

    /// <summary>영업 전 수동 검증을 위해 10일차를 새로 준비합니다.</summary>
    private void handleDebugDay10Clicked() => debugJumpToDay(10);

    /// <summary>영업 전 수동 검증을 위해 20일차를 새로 준비합니다.</summary>
    private void handleDebugDay20Clicked() => debugJumpToDay(20);

    /// <summary>영업 전 수동 검증을 위해 엔딩 전날인 30일차를 새로 준비합니다.</summary>
    private void handleDebugDay30Clicked() => debugJumpToDay(30);

    /// <summary>테스트 날짜 점프를 진행 경계에 전달하고 화면을 갱신합니다.</summary>
    /// <param name="displayDay">이동할 표시 일차입니다.</param>
    private void debugJumpToDay(int displayDay)
    {
        this.runProgressAction(() => this.gameProgress.DebugJumpToDay(displayDay));
    }
#endif

    /// <summary>가격 입력 결과를 하루 진행에 전달합니다.</summary>
    /// <param name="offeredTotal">플레이어가 확정한 전체 판매 가격입니다.</param>
    private void handlePriceConfirmed(long offeredTotal)
    {
        // 거래 전환 직후 도착한 연속 입력은 사용자 입력 경계에서 멱등하게 무시합니다.
        if (this.subscribedDay == null || !this.subscribedDay.CanSubmitOffer)
        {
            return;
        }

        if (!this.saleSortingPanel.TryGetSaleItems(out IReadOnlyList<SaleItem> saleItems)
            || saleItems.Count == 0)
        {
            Debug.LogWarning("[GameUIController] 판매할 물품을 하나 이상 선택해야 합니다.", this);
            return;
        }

        this.runProgressAction(() => this.submitSelectedOffer(offeredTotal, saleItems));
    }

    /// <summary>가격 입력 취소 후 입력 ViewData를 갱신합니다.</summary>
    private void handleInputCancelled()
    {
        this.validationText.text = string.Empty;
        this.refreshRuntimeViews();
    }

    /// <summary>키패드 입력값 변경을 가격 입력 UI의 활성 상태에 즉시 반영합니다.</summary>
    /// <param name="currentPrice">변경된 현재 입력 가격입니다.</param>
    private void handlePriceChanged(long currentPrice)
    {
        this.refreshPriceInputView(currentPrice);
        this.refreshInputRouting(currentPrice);
    }

    /// <summary>키보드 Confirm 요청을 현재 Keypad의 단일 확정 경로로 전달합니다.</summary>
    private void handleKeyboardConfirmRequested()
    {
        this.keypadController.OnConfirmButtonClick();
    }

    /// <summary>계산기 표시 상태가 바뀌면 가격 입력과 Enter 라우팅을 즉시 갱신합니다.</summary>
    /// <param name="isOpen">계산기 패널이 열려 있으면 true입니다.</param>
    private void handleCalculatorVisibilityChanged(bool isOpen)
    {
        if (!this.isReady || this.subscribedDay == null) return;
        this.refreshRuntimeViews();
    }

    /// <summary>상품 쏟기 연출 완료를 진행 상태에 반영하고 현재 거래 입력 상태를 갱신합니다.</summary>
    private void handleSortingStarted()
    {
        if (this.subscribedDay == null) return;

        if (this.subscribedDay.State == DayProgressState.Operating)
        {
            this.runProgressAction(this.gameProgress.BeginCustomerSorting);
            return;
        }

        // 입장 연출 중 영업시간이 만료되면 Progress는 Closing이지만 마지막 손님의 거래는 유효합니다.
        if (this.subscribedDay.State == DayProgressState.Closing && this.subscribedDay.CanSubmitOffer)
        {
            this.refreshRuntimeViews();
        }
    }

    /// <summary>선택한 판매 상품 목록과 가격을 제출하고 거래 결과 화면을 엽니다.</summary>
    /// <param name="offeredTotal">플레이어가 입력한 판매 가격입니다.</param>
    /// <param name="saleItems">판매 영역에서 상품 ID별로 집계한 수량입니다.</param>
    private void submitSelectedOffer(long offeredTotal, IReadOnlyList<SaleItem> saleItems)
    {
        this.gameProgress.SubmitOffer(offeredTotal, saleItems);

        this.saleSortingPanel.LockSelection();
        this.keypadController.OnClearButtonClick();
    }

    /// <summary>거래 결과 확인 요청을 하루 진행에 전달합니다.</summary>
    private void handleTransactionContinueClicked()
    {
        this.runProgressAction(this.gameProgress.CompleteTransactionResult);
    }

    /// <summary>세션의 확정 결과를 사용해 엔딩 씬으로 전환한다.</summary>
    /// <returns>전환 완료 또는 화면 오류 표시 완료.</returns>
    private async UniTask openEndingAsync()
    {
        try { await GameSceneManager.Instance.TransitionToFinalEndingAsync(); }
        catch (Exception exception)
        {
            if (this != null) this.showError(exception);
            else Debug.LogException(exception);
        }
    }

    /// <summary>정산 결과 확인 요청을 하루 진행에 전달합니다.</summary>
    private void handleSettlementNextRequested()
    {
        if (this.IsFacilityShopOpen || this.isPurchasingFacility) return;
        this.runProgressAction(this.gameProgress.CompleteSettlement);
    }

    /// <summary>타이머 일시정지 요청을 하루 진행에 전달합니다.</summary>
    private void handlePauseRequested()
    {
        if (!this.canPause())
        {
            return;
        }

        this.runProgressAction(this.gameProgress.Pause);
    }

    /// <summary>타이머 재개 요청을 하루 진행에 전달합니다.</summary>
    private void handleResumeRequested()
    {
        if (!this.canResume())
        {
            return;
        }

        this.runProgressAction(this.gameProgress.Resume);
    }

    /// <summary>진행 호출의 예외를 UI 오류로 표시합니다.</summary>
    /// <param name="action">실행할 Progress 공개 동작입니다.</param>
    private void runProgressAction(Action action)
    {
        if (!this.isReady || this.hasError || this.IsFacilityShopOpen || this.isPurchasingFacility || action == null)
        {
            return;
        }

        try
        {
            action();
            this.refreshAllViews();
        }
        catch (Exception exception)
        {
            this.showError(exception);
        }
    }

    /// <summary>현재 진행 스냅샷을 모든 Presenter에 전달합니다.</summary>
    private void refreshAllViews()
    {
        if (!this.isReady || this.gameProgress == null || this.subscribedDay == null)
        {
            return;
        }

        this.refreshRuntimeViews();
        this.refreshPanelVisibility();
    }

    /// <summary>날짜·타이머·경제·입력 ViewData를 갱신합니다.</summary>
    private void refreshRuntimeViews()
    {
        if (!this.isReady || this.gameProgress == null || this.subscribedDay == null)
        {
            return;
        }

        this.saleSortingPanel.SetDividerBarAvailable(
            GameSessionManager.Instance.IsFacilityEffectActive(ConvenienceEffectType.DividerBar));
        this.saleSortingPanel.SetAutoSortingAvailable(
            GameSessionManager.Instance.IsFacilityEffectActive(ConvenienceEffectType.AutoSorting));
        this.saleSortingPanel.SetVacuumAvailable(
            GameSessionManager.Instance.IsFacilityEffectActive(ConvenienceEffectType.Vacuum));

        this.gameDayPresenter.UpdateView(new GameDayViewData(
            this.gameProgress.CurrentDay,
            this.toUiPhase(this.subscribedDay.State)));

        this.economyStatusPresenter.UpdateView(new EconomyStatusViewData(
            this.economy.QueryService.CurrentBalance,
            this.economy.QueryService.DailySaleIncome));

        this.refreshFrameViews();

        long currentPrice = this.keypadController == null ? 0 : this.keypadController.CurrentPrice;
        this.refreshPriceInputView(currentPrice);

        bool canContinueTransaction = this.isTransactionResultAwaitingAdvance();
        this.transactionContinueButton.gameObject.SetActive(false);
        this.setKeypadInteractable(this.subscribedDay.CanSubmitOffer
            && this.saleSortingPanel.IsSorting
            && this.saleSortingPanel.IsCalculatorOpen);
        this.refreshInputRouting(currentPrice, canContinueTransaction);
    }

    /// <summary>현재 가격과 거래 상태만 가격 입력 Presenter에 전달합니다.</summary>
    /// <param name="currentPrice">키패드에 입력된 0 이상의 가격입니다.</param>
    private void refreshPriceInputView(long currentPrice)
    {
        if (!this.isReady || this.subscribedDay == null || this.priceInputPresenter == null)
        {
            return;
        }

        this.priceInputPresenter.UpdateView(new PriceInputViewData(
            currentPrice > 0 ? currentPrice : (long?)null,
            currentPrice > 0,
            this.subscribedDay.CanSubmitOffer,
            this.hasError ? this.errorText.text : this.validationText.text));
    }

    /// <summary>현재 진행과 가격 상태를 Enter 입력 라우터에 전달합니다.</summary>
    /// <param name="currentPrice">현재 Keypad 입력 가격입니다.</param>
    /// <param name="canContinueOverride">이미 계산한 거래 결과 확인 가능 상태입니다.</param>
    private void refreshInputRouting(long currentPrice, bool? canContinueOverride = null)
    {
        if (this.gameInputRouter == null || this.subscribedDay == null)
        {
            return;
        }

        bool canContinue = canContinueOverride ?? (this.subscribedDay.CurrentVisit != null
            && (this.subscribedDay.CurrentVisit.State == CustomerState.Accepted
                || this.subscribedDay.CurrentVisit.State == CustomerState.Rejected)
            && (this.subscribedDay.State == DayProgressState.TransactionResult
                || this.subscribedDay.State == DayProgressState.Closing));
        this.gameInputRouter.SetState(
            this.subscribedDay.CanSubmitOffer
                && this.saleSortingPanel.IsSorting
                && this.saleSortingPanel.IsCalculatorOpen
                && this.saleSortingPanel.CanConfirm
                && currentPrice > 0,
            canContinue);
    }

    /// <summary>거래 결과 화면에서 다음 손님으로 이동할 수 있는지 확인합니다.</summary>
    /// <returns>결과가 확정됐고 거래 결과 확인 단계에 있으면 true입니다.</returns>
    private bool isTransactionResultAwaitingAdvance()
    {
        return this.subscribedDay != null
            && this.subscribedDay.CurrentVisit != null
            && (this.subscribedDay.CurrentVisit.State == CustomerState.Accepted
                || this.subscribedDay.CurrentVisit.State == CustomerState.Rejected)
            && (this.subscribedDay.State == DayProgressState.TransactionResult
                || this.subscribedDay.State == DayProgressState.Closing);
    }

    /// <summary>이번 프레임에 마우스 왼쪽 버튼이 눌렸는지 확인합니다.</summary>
    /// <returns>이번 프레임에 마우스 클릭이 시작됐으면 true입니다.</returns>
    private bool wasPointerClickThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>DayProgress의 남은 시간으로 타이머와 공통 영업 시계 표시를 갱신한다.</summary>
    private void refreshFrameViews()
    {
        if (this.subscribedDay == null)
        {
            return;
        }

        // 영업 전 remainingSeconds는 아직 0이다. 감독관/준비 화면에서는 시작 시각을 유지한다.
        bool beforeOpening = this.subscribedDay.State == DayProgressState.InspectorEvent
            || this.subscribedDay.State == DayProgressState.PreOpen;
        float normalizedTime = beforeOpening ? 1f
            : this.subscribedDay.BusinessDurationSeconds <= 0f ? 0f
            : this.subscribedDay.RemainingSeconds / this.subscribedDay.BusinessDurationSeconds;
        if (this.businessClock != null)
        {
            // 진행 시간의 단일 권위는 DayProgress이며 0/50/100%를 09/15/21시로 표시한다.
            int minutes = BusinessHours.OpenMinutes + Mathf.FloorToInt(
                BusinessHours.DurationMinutes * (1f - Mathf.Clamp01(normalizedTime)));
            this.businessClock.DisplayTime(minutes);
        }
        this.businessTimerPresenter.UpdateView(new BusinessTimerViewData(
            this.subscribedDay.RemainingSeconds,
            normalizedTime,
            this.subscribedDay.IsPaused,
            this.canPause(),
            this.canResume()));
    }

    /// <summary>진행 상태에 따라 패널과 기본 버튼을 표시합니다.</summary>
    private void refreshPanelVisibility()
    {
        if (this.gameProgress.State != GameProgressState.DayInProgress)
        {
            this.setPanelVisibility(this.preOpenPanel, false);
            this.setPanelVisibility(this.operatingPanel, false);
            this.setPanelVisibility(this.settlementPanel, false);
            this.setPanelVisibility(this.failurePanel, this.gameProgress.State == GameProgressState.Failed);
            this.gameInputRouter.enabled = false;
            return;
        }
        if (this.IsFacilityShopOpen && !this.canOpenFacilityShop()) this.closeFacilityShop();
        this.facilityOpenButton.gameObject.SetActive(this.canOpenFacilityShop());
        bool preOpen = this.subscribedDay.State == DayProgressState.PreOpen;
        bool operating = this.isSettlementPresentationPending || this.subscribedDay.State == DayProgressState.Operating
            || this.subscribedDay.State == DayProgressState.Sorting
            || this.subscribedDay.State == DayProgressState.TransactionResult
            || this.subscribedDay.State == DayProgressState.Closing;
        bool settlement = !this.isSettlementPresentationPending && this.subscribedDay.State == DayProgressState.Settlement;

        this.setPanelVisibility(this.preOpenPanel, preOpen);
        this.setPanelVisibility(this.operatingPanel, operating);
        this.setPanelVisibility(this.settlementPanel, settlement);
        this.setPanelVisibility(this.failurePanel, this.gameProgress.State == GameProgressState.Failed);
        this.openBusinessButton.interactable = preOpen && !this.isOpeningBusiness && !this.hasError && presentationReady && !hasError;
        bool inspector = this.subscribedDay.State == DayProgressState.InspectorEvent;
        this.gameInputRouter.enabled = presentationReady && !hasError && !inspector && !IsFacilityShopOpen;
        this.inspectorPresenter.gameObject.SetActive(inspector && !hasError);
        if (inspector && !hasError)
        {
            InspectorEventSnapshot snapshot = GameSessionManager.Instance.InspectorEvents.Current;
            this.inspectorPresenter.Present(snapshot, textData.Rows[snapshot.TextIdx].Text,
                inspectorSprites[snapshot.PortraitResourceIdx], presentationReady, () => IsPresentationPaused);
        }
    }

    /// <summary>표시 중이던 줄을 모델과 대조한 뒤 화면을 갱신한다.</summary>
    /// <param name="snapshot">표시했던 날짜·이벤트·줄.</param>
    private void handleInspectorNext(InspectorEventSnapshot snapshot)
    {
        if (!presentationReady || hasError) return;
        runProgressAction(() => subscribedDay.AdvanceInspector(snapshot));
    }

    /// <summary>실제 퇴장 완료 후 남은 이벤트 또는 영업 전 화면으로 진행한다.</summary>
    /// <param name="snapshot">퇴장 시작 당시 상태.</param>
    private void handleInspectorExit(InspectorEventSnapshot snapshot)
    {
        if (!presentationReady || hasError) return;
        runProgressAction(() => subscribedDay.CompleteInspectorExit(snapshot));
    }

    /// <summary>세션에서 확정된 당일 상품·가격·지침을 영업 시작 Presenter에 전달합니다.</summary>
    /// <param name="day">현재 영업 전 상태의 하루 진행입니다.</param>
    /// <exception cref="ArgumentNullException">하루 진행이 null인 경우 발생합니다.</exception>
    private void renderPreOpen(DayProgress day)
    {
        if (day == null) throw new ArgumentNullException(nameof(day));
        DailyPriceState dailyPrices = GameSessionManager.Instance.EnsureDailyPrices();
        PreOpenGuidelineViewData viewData = this.viewDataFactory.CreatePreOpenGuidelineViewData(
            day.Day,
            dailyPrices,
            GameSessionManager.Instance.DailyGuidelines,
            day.State == DayProgressState.PreOpen && !this.hasError);
        this.preOpenPanelPresenter.UpdateView(viewData);
    }

    /// <summary>진행 상태를 UI 표현 계약으로 변환합니다.</summary>
    /// <param name="state">변환할 하루 진행 상태입니다.</param>
    /// <returns>UI가 표시할 진행 단계입니다.</returns>
    private GameDayPhase toUiPhase(DayProgressState state)
    {
        return state switch
        {
            DayProgressState.PreOpen => GameDayPhase.PreOpen,
            DayProgressState.Operating => GameDayPhase.Operating,
            DayProgressState.Sorting => GameDayPhase.Operating,
            DayProgressState.TransactionResult => GameDayPhase.TradingResult,
            DayProgressState.Closing => GameDayPhase.Closing,
            DayProgressState.Settlement => GameDayPhase.DailySettlement,
            _ => GameDayPhase.PreOpen
        };
    }

    /// <summary>버튼 입력이 가능한 상태에 맞춰 동적 키패드를 잠급니다.</summary>
    /// <param name="isInteractable">키패드 입력 허용 여부입니다.</param>
    private void setKeypadInteractable(bool isInteractable)
    {
        if (this.keypadController != null)
        {
            this.keypadController.SetInputEnabled(isInteractable);
        }

        foreach (Button button in this.keypadButtons)
        {
            if (button != null)
            {
                button.interactable = isInteractable;
            }
        }
    }

    /// <summary>현재 하루가 일시정지를 받을 수 있는 상태인지 확인합니다.</summary>
    /// <returns>영업 중이고 아직 일시정지하지 않은 경우 true입니다.</returns>
    private bool canPause()
    {
        return this.gameProgress != null
            && this.gameProgress.State == GameProgressState.DayInProgress
            && this.subscribedDay != null
            && !this.subscribedDay.IsPaused
            && (this.subscribedDay.State == DayProgressState.Operating
                || this.subscribedDay.State == DayProgressState.Sorting
                || this.subscribedDay.State == DayProgressState.TransactionResult);
    }

    /// <summary>현재 하루가 재개 요청을 받을 수 있는 상태인지 확인합니다.</summary>
    /// <returns>영업 중이고 일시정지된 경우 true입니다.</returns>
    private bool canResume()
    {
        return this.gameProgress != null
            && this.gameProgress.State == GameProgressState.DayInProgress
            && this.subscribedDay != null
            && this.subscribedDay.IsPaused
            && (this.subscribedDay.State == DayProgressState.Operating
                || this.subscribedDay.State == DayProgressState.Sorting
                || this.subscribedDay.State == DayProgressState.TransactionResult);
    }

    /// <summary>기술 오류를 화면에 표시하고 추가 입력을 차단합니다.</summary>
    /// <param name="exception">표시하고 기록할 원본 오류입니다.</param>
    private void showError(Exception exception)
    {
        this.hasError = true;
        if (gameInputRouter != null) gameInputRouter.enabled = false;
        if (inspectorPresenter != null) inspectorPresenter.gameObject.SetActive(false);
        if (startupCover != null)
        {
            startupCover.gameObject.SetActive(true);
            startupCover.alpha = 1;
            startupCover.blocksRaycasts = true;
            startupCover.transform.SetAsLastSibling();
        }
        if (startupErrorText != null) startupErrorText.text = "초기화 또는 진행 오류\n" + exception?.Message;
        if (this.facilityOpenButton != null) this.facilityOpenButton.interactable = false;
        if (this.facilityShopPresenter != null) this.facilityShopPresenter.SetInteractionEnabled(false, true);
        if (this.settlementInputGroup != null) this.settlementInputGroup.interactable = false;
        string message = exception?.Message ?? "Unknown progress error.";
        if (this.errorText != null)
        {
            this.errorText.text = message;
        }

        Debug.LogException(exception, this);
        this.setKeypadInteractable(false);
        if (this.openBusinessButton != null) this.openBusinessButton.interactable = false;
        if (this.transactionContinueButton != null) this.transactionContinueButton.interactable = false;
    }

    /// <summary>패널의 활성 상태를 설정합니다.</summary>
    /// <param name="panel">표시 상태를 변경할 씬 UI 패널입니다.</param>
    /// <param name="isVisible">패널 표시 여부입니다.</param>
    private void setPanelVisibility(GameObject panel, bool isVisible)
    {
        if (panel != null)
        {
            panel.SetActive(isVisible);
        }
    }

}
