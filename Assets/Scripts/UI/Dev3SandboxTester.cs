using System;
using System.Linq;
using System.Globalization;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Root view state for the single-scene lifecycle.
/// </summary>
public enum GameViewState
{
    Title,
    Tutorial,
    MainHub,
    Trading
}

/// <summary>
/// Active tab on the Main Hub navigation header.
/// </summary>
public enum HubTab
{
    Flyer = 1,
    Ledger = 2,
    Building = 3,
    Options = 4,
    SaveQuit = 5
}

/// <summary>
/// Single-scene lifecycle controller and sandbox tester for Developer 3.
/// Implements Title Screen, Tutorial, Main Hub (Flyer, Ledger, Building, Options, SaveQuit),
/// and the Full HD 1920x1080 Daily Business Session all in Leegyuyoung.unity.
/// </summary>
public sealed class Dev3SandboxTester : MonoBehaviour
{
    // =========================================================================
    // 1. CONSTANTS & SERIALIZED FIELDS
    // =========================================================================

    /// <summary>Scale factor from 1280x720 prototype coordinate space to 1920x1080 Full HD.</summary>
    private const float S = 1.5f;

    // CSV 행 참조만 정렬해 보관한다. 이름·가격·이미지는 별도 상품 모델에 복제하지 않는다.
    private ProductData[] products = Array.Empty<ProductData>();

    /// <summary>거래 상태는 CustomerVisit, 금액은 EconomyRuntime이 소유한다.</summary>
    public CustomerVisit CurrentVisit { get; private set; }
    private CustomerQueue customerQueue;
    private readonly Image[] queuePortraits = new Image[CustomerQueue.Capacity];
    private readonly TextMeshProUGUI[] queueLabels = new TextMeshProUGUI[CustomerQueue.Capacity];
    private TextMeshProUGUI queueLeaveText;
    private float resultRemainingSeconds;
    /// <summary>현재 화면이 소유한 대기열. 다른 시계에서 중복 Advance하지 않는다.</summary>
    public CustomerQueue Queue => customerQueue;
    private CustomerCatalog catalog;
    // DataTableManager가 소유하는 게임 전체 공용 텍스트를 참조한다.
    private TextDataTable texts;
    private EconomyRuntime economy;
    private readonly CustomerGenerator generator = new CustomerGenerator(new System.Random());
    private readonly Dictionary<uint, Sprite> productSprites = new Dictionary<uint, Sprite>();
    private bool ready, paused, faulted, showingGuide;
    private int day => checked((int)GameSessionManager.Instance.ElapsedDays + 1);
    private long closedRevenue;
    private Button nextCustomerButton, endDayButton;
    private Sprite squareSprite;
    private TMP_FontAsset displayFont;
    /// <summary>임시 수동 날짜. 저장·실제 날짜 시스템 연결 전 씬 수명에 한정한다.</summary>
    public int Day => day;
    private bool canOffer => ready && !faulted && !paused && !showingGuide
        && economy.DailyAggregationService.IsDayOpen && CurrentVisit?.State == CustomerState.AwaitingOffer;

    // =========================================================================
    // 2. STATE & DATA
    // =========================================================================

    private GameViewState currentViewState = GameViewState.Title;
    private HubTab currentHubTab = HubTab.Flyer;
    private int currentFlyerPage = 1;
    private GameSaveData currentSaveData;

    // View Root Containers
    private RectTransform canvasRoot;
    private GameObject titleRoot;
    private GameObject tutorialRoot;
    private GameObject mainHubRoot;
    private GameObject tradingRoot;

    // Main Hub Dynamic Elements
    private Transform hubContentContainer;
    private TextMeshProUGUI hubBusinessBtnText;
    private TextMeshProUGUI hubBusinessBtnSubtext;
    private readonly List<Button> hubTabButtons = new List<Button>();
    private GameObject quitConfirmModal;

    // Trading View Elements (from core POS session)
    private RectTransform tradingModal;
    private RectTransform basketRoot;
    private TextMeshProUGUI dayText, statusText, cashText, goalHintText, gaugeText, queueText;
    private TextMeshProUGUI dialogueText, reasonText, inputText;
    private Image gaugeFill;
    private Image portrait;
    private readonly Image[] waiting = new Image[2];
    private Button confirmButton;

    private string amount = "";
    private bool invalidAmount;
    private float alertUntil;

    private readonly List<GameObject> activeBasketCards = new List<GameObject>();


    // =========================================================================
    // 3. UNITY LIFECYCLE
    // =========================================================================

    /// <summary>MainScene 통합 화면의 Awake 처리를 수행한다.</summary>
    private void Awake()
    {
        ensureValidEventSystem();
        // 저장은 통합 범위 밖이며 기존 PlayerPrefs를 읽거나 덮어쓰지 않는다.
        currentSaveData = GameSaveData.CreateNewGame();
    }

    /// <summary>MainScene 통합 화면의 Update 처리를 수행한다.</summary>
    private void Update()
    {
        if (!ready || faulted || currentViewState != GameViewState.Trading) return;
        try
        {
            bool stopped = paused || Time.timeScale == 0;
            customerQueue.Advance(Time.unscaledDeltaTime, stopped);
            if (!stopped && economy.QueryService.IsDayOpen)
            {
                if (CurrentVisit?.WasAccepted.HasValue == true) resultRemainingSeconds -= Time.unscaledDeltaTime;
                if ((CurrentVisit == null && customerQueue.Waiting.Count > 0) || (CurrentVisit?.WasAccepted.HasValue == true && resultRemainingSeconds <= 0)) GenerateCustomer();
            }
            if (GameSessionManager.Instance.AdvanceTradingTime(Time.unscaledDeltaTime, paused || Time.timeScale == 0))
                refreshTradingView();
            renderQueue();
            handleTradingInput();
        }
        catch (Exception exception) { fail(exception); }
    }


    // =========================================================================
    // 4. VIEW STATE MACHINE
    // =========================================================================

    public void setViewState(GameViewState newState)
    {
        this.currentViewState = newState;

        if (this.titleRoot != null) this.titleRoot.SetActive(newState == GameViewState.Title);
        if (this.tutorialRoot != null) this.tutorialRoot.SetActive(newState == GameViewState.Tutorial);
        if (this.mainHubRoot != null) this.mainHubRoot.SetActive(newState == GameViewState.MainHub);
        if (this.tradingRoot != null) this.tradingRoot.SetActive(newState == GameViewState.Trading);

        if (newState == GameViewState.MainHub)
        {
            this.refreshMainHubHeader();
            this.selectHubTab(this.currentHubTab);
        }
    }

    /// <summary>MainScene 통합 화면의 onNewGameClicked 처리를 수행한다.</summary>
    private void onNewGameClicked()
    {
        if (!ready || economy.DailyAggregationService.IsDayOpen) return;
        setViewState(GameViewState.Tutorial);
    }

    /// <summary>MainScene 통합 화면의 onContinueClicked 처리를 수행한다.</summary>
    private void onContinueClicked()
    {
        // 저장 복원이 구현될 때까지 비활성화한다.
    }

    private void onTutorialNextClicked()
    {
        this.setViewState(GameViewState.MainHub);
    }

    /// <summary>MainScene 통합 화면의 launchTradingSession 처리를 수행한다.</summary>
    private void launchTradingSession()
    {
        if (!ready || faulted || economy.DailyAggregationService.IsDayOpen) return;
        showingGuide = true;
        amount = "";
        setViewState(GameViewState.Trading);
        refreshTradingView();
    }


    // =========================================================================
    // 5. MASTER SCREEN BUILDER (Full HD 1920x1080 Canvas)
    // =========================================================================

    private void buildAllScreens()
    {
        var canvasObject = new GameObject("DystopiaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(this.transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        this.canvasRoot = canvasObject.GetComponent<RectTransform>();

        // Build all 4 views as sibling roots under canvas
        this.buildTitleScreen();
        this.buildTutorialScreen();
        this.buildMainHubScreen();
        this.buildTradingScreen();
    }


    // =========================================================================
    // 6. TITLE VIEW BUILDER
    // =========================================================================

    private void buildTitleScreen()
    {
        this.titleRoot = this.rect(this.canvasRoot, "TitleView", 0, 0, 1280, 720).gameObject;

        // Dark Atmospheric Background
        this.panel(this.titleRoot.transform, "Bg", 0, 0, 1280, 720, new Color(0.06f, 0.08f, 0.11f, 1f));

        // Decorative Frame
        this.panel(this.titleRoot.transform, "OuterFrame", 40, 40, 1200, 640, new Color(0.18f, 0.22f, 0.28f, 0.6f));
        this.panel(this.titleRoot.transform, "InnerFrame", 44, 44, 1192, 632, new Color(0.08f, 0.10f, 0.14f, 0.98f));

        // Game Title & Subtitle
        this.label(this.titleRoot.transform, "TitleText", "PROJECT CASHIER", 140, 140, 1000, 70, 44, new Color(0.95f, 0.85f, 0.45f), TextAlignmentOptions.Center);
        this.label(this.titleRoot.transform, "SubtitleText", "DISTRICT 9 RATION STALL SIMULATOR", 140, 220, 1000, 35, 18, new Color(0.65f, 0.72f, 0.80f), TextAlignmentOptions.Center);

        // Action Buttons
        this.makeButton(this.titleRoot.transform, "BtnNewGame", "START SESSION", 490, 360, 300, 52, this.onNewGameClicked, 22, new Color(0.22f, 0.55f, 0.38f));

        var continueBtn = this.makeButton(this.titleRoot.transform, "BtnContinue", "CONTINUE", 490, 430, 300, 52, this.onContinueClicked, 22, new Color(0.24f, 0.42f, 0.65f));
        continueBtn.interactable = false;

        this.label(this.titleRoot.transform, "VerText", "v1.0 Local Sandbox Prototype | FHD 1080p", 490, 580, 300, 25, 13, new Color(0.45f, 0.50f, 0.55f), TextAlignmentOptions.Center);
    }


    // =========================================================================
    // 7. TUTORIAL VIEW BUILDER
    // =========================================================================

    private void buildTutorialScreen()
    {
        this.tutorialRoot = this.rect(this.canvasRoot, "TutorialView", 0, 0, 1280, 720).gameObject;

        // Dark dim backdrop
        this.panel(this.tutorialRoot.transform, "Dim", 0, 0, 1280, 720, new Color(0.04f, 0.05f, 0.07f, 0.92f));

        // Center Modal Window (760 x 520)
        var modal = this.panel(this.tutorialRoot.transform, "ModalPaper", 260, 100, 760, 520, new Color(0.10f, 0.13f, 0.17f, 0.99f));
        this.panel(modal.transform, "Border", 0, 0, 760, 520, new Color(0.40f, 0.48f, 0.55f, 0.8f));

        this.label(modal.transform, "Title", "HOW TO OPERATE", 40, 28, 680, 40, 28, Color.white, TextAlignmentOptions.MidlineLeft);
        this.label(modal.transform, "Subtitle", "Basic guidelines for the ration stall cashier.", 40, 72, 680, 25, 16, new Color(0.65f, 0.75f, 0.85f), TextAlignmentOptions.TopLeft);

        string rules =
            "<b>1. Memorize Product Prices:</b>\n" +
            "   Before each business day begins, memorize the prices on the catalog.\n\n" +
            "<b>2. Count Items on Counter:</b>\n" +
            "   Customers place individual goods on the desk. Count them and sum the total.\n\n" +
            "<b>3. Register on POS:</b>\n" +
            "   Use the numeric keys to enter the charge amount and press <b>[Enter]</b>.\n\n" +
            "<b>4. Watch the Queue Gauge:</b>\n" +
            "   The crowd becomes restless over time. Sell quickly to prevent departures!\n\n" +
            "<b>5. Weekly Tribute & Citizenship:</b>\n" +
            "   Pay stall tributes to the Inspector on inspection days, and purchase a Citizenship Pass to escape!";

        this.label(modal.transform, "Content", rules, 40, 115, 680, 310, 17, new Color(0.88f, 0.92f, 0.92f), TextAlignmentOptions.TopLeft);

        this.makeButton(modal.transform, "BtnStartJourney", "START JOURNEY >", 220, 440, 320, 48, this.onTutorialNextClicked, 20, new Color(0.20f, 0.58f, 0.38f));
    }


    // =========================================================================
    // 8. MAIN HUB VIEW BUILDER (5 Tabs + Center Action Button)
    // =========================================================================

    private void buildMainHubScreen()
    {
        this.mainHubRoot = this.rect(this.canvasRoot, "MainHubView", 0, 0, 1280, 720).gameObject;

        // Light/White Aesthetic Background (Notes requirement: "하양색 살짝 둥근 UI")
        this.panel(this.mainHubRoot.transform, "MainBg", 0, 0, 1280, 720, new Color(0.88f, 0.91f, 0.94f, 1f));

        // ---------------------------------------------------------------------
        // Top Navigation Header Bar (0, 0, 1280, 83)
        // ---------------------------------------------------------------------
        var header = this.panel(this.mainHubRoot.transform, "TopHeader", 0, 0, 1280, 83, new Color(0.14f, 0.18f, 0.23f, 0.98f));
        this.panel(header.transform, "HeaderBottomLine", 0, 80, 1280, 3, new Color(0.28f, 0.35f, 0.45f));

        this.hubTabButtons.Clear();

        // 1. FLYER (Left)
        var btnFlyer = this.makeButton(header.transform, "TabFlyer", "1. FLYER", 20, 18, 140, 48, () => this.selectHubTab(HubTab.Flyer), 16, new Color(0.22f, 0.28f, 0.35f));
        this.hubTabButtons.Add(btnFlyer);

        // 2. LEDGER (Left)
        var btnLedger = this.makeButton(header.transform, "TabLedger", "2. LEDGER", 175, 18, 140, 48, () => this.selectHubTab(HubTab.Ledger), 16, new Color(0.22f, 0.28f, 0.35f));
        this.hubTabButtons.Add(btnLedger);

        // CENTER: START BUSINESS BUTTON (Prominent Center)
        var btnBusiness = this.makeButton(header.transform, "BtnStartBusiness", "", 390, 10, 500, 62, this.launchTradingSession, 20, new Color(0.20f, 0.58f, 0.36f));
        this.hubBusinessBtnText = this.label(btnBusiness.transform, "MainTxt", "START BUSINESS", 10, 6, 480, 28, 20, Color.white, TextAlignmentOptions.Center);
        this.hubBusinessBtnSubtext = this.label(btnBusiness.transform, "SubTxt", "(Tribute in 6 Days)", 10, 34, 480, 22, 14, new Color(0.85f, 0.95f, 0.85f), TextAlignmentOptions.Center);

        // 3. UPGRADE (Right)
        var btnUpgrade = this.makeButton(header.transform, "TabUpgrade", "3. UPGRADE", 920, 18, 140, 48, () => this.selectHubTab(HubTab.Building), 16, new Color(0.22f, 0.28f, 0.35f));
        this.hubTabButtons.Add(btnUpgrade);

        // 4. OPTIONS (Right)
        var btnOptions = this.makeButton(header.transform, "TabOptions", "4. OPTIONS", 1075, 18, 100, 48, () => this.selectHubTab(HubTab.Options), 15, new Color(0.22f, 0.28f, 0.35f));
        this.hubTabButtons.Add(btnOptions);

        // 5. QUIT (Right)
        var btnQuit = this.makeButton(header.transform, "TabQuit", "5. QUIT", 1190, 18, 70, 48, () => this.selectHubTab(HubTab.SaveQuit), 15, new Color(0.48f, 0.22f, 0.22f));
        this.hubTabButtons.Add(btnQuit);

        // ---------------------------------------------------------------------
        // Central Content Viewport (30, 95, 1220, 610)
        // ---------------------------------------------------------------------
        var contentFrame = this.panel(this.mainHubRoot.transform, "HubContentFrame", 30, 95, 1220, 610, new Color(0.96f, 0.97f, 0.98f, 0.98f));
        this.panel(contentFrame.transform, "Border", 0, 0, 1220, 610, new Color(0.72f, 0.78f, 0.84f));
        this.panel(contentFrame.transform, "InnerPaper", 3, 3, 1214, 604, new Color(0.96f, 0.97f, 0.98f, 1f));

        this.hubContentContainer = contentFrame.transform;

        // Build Quit Confirm Modal (Hidden by default)
        this.buildQuitConfirmModal();
    }

    /// <summary>MainScene 통합 화면의 refreshMainHubHeader 처리를 수행한다.</summary>
    private void refreshMainHubHeader()
    {
        if (hubBusinessBtnText != null) hubBusinessBtnText.text = $"START DAY {day}";
        if (hubBusinessBtnSubtext != null) hubBusinessBtnSubtext.text = ready ? $"Cash: {economy.QueryService.CurrentBalance:N0} G" : "Loading";
    }

    public void selectHubTab(HubTab tab)
    {
        this.currentHubTab = tab;

        // Clear dynamic viewport container
        foreach (Transform child in this.hubContentContainer)
        {
            if (child.name != "Border" && child.name != "InnerPaper")
            {
                Destroy(child.gameObject);
            }
        }

        switch (tab)
        {
            case HubTab.Flyer:
                this.renderFlyerBooklet();
                break;
            case HubTab.Ledger:
                this.renderLedgerNotebook();
                break;
            case HubTab.Building:
                this.renderBuildingUpgrade();
                break;
            case HubTab.Options:
                this.renderOptionsSettings();
                break;
            case HubTab.SaveQuit:
                if (this.quitConfirmModal != null) this.quitConfirmModal.SetActive(true);
                break;
        }
    }


    // =========================================================================
    // 9. TAB 1: FLYER BOOKLET VIEW (4-Page Magazine Style)
    // =========================================================================

    /// <summary>선정된 신문의 제목과 설명을 조회한다. 조회는 재추첨하지 않는다.</summary>
    /// <returns>신문 문구 또는 뉴스 부재 안내.</returns>
    private string newspaperText()
    {
        var state = GameSessionManager.Instance.EnsureDailyPrices();
        if (!state.NewspaperEventIdx.HasValue) return "오늘의 신문 이벤트 없음";
        var item = DataTableManager.Instance.GetDB<PriceEventDataTable>(DataTableType.PriceEvent).Rows[state.NewspaperEventIdx.Value];
        return texts.Rows[item.NameIdx].Text + " — " + texts.Rows[item.DescriptionIdx].Text;
    }

    /// <summary>현재일 신문과 가격표를 기존 전단지 화면에 표시한다.</summary>
    private void renderFlyerBooklet()
    {
        // Flyer Booklet Header Title
        this.label(this.hubContentContainer, "BookletTitle", "DISTRICT 9 RATION PROMOTION CATALOG", 40, 20, 1140, 35, 22, new Color(0.15f, 0.20f, 0.28f), TextAlignmentOptions.MidlineLeft);

        if (this.currentFlyerPage == 1)
        {
            // ---------------- PAGE 1: COVER & HIGHLIGHT ----------------
            var coverCard = this.panel(this.hubContentContainer, "Page1Card", 40, 65, 1140, 480, Color.white);
            this.panel(coverCard.transform, "Border", 0, 0, 1140, 480, new Color(0.80f, 0.84f, 0.88f));

            // Special Event Banner
            this.panel(coverCard.transform, "NoticeBanner", 20, 20, 1100, 55, new Color(0.85f, 0.35f, 0.30f, 0.95f));
            this.label(coverCard.transform, "BannerTxt", newspaperText(), 30, 25, 1080, 45, 20, Color.white, TextAlignmentOptions.Center);

            // Featured Product Highlight Box
            var featProduct = products[0];
            var featBox = this.panel(coverCard.transform, "FeatBox", 380, 100, 380, 320, new Color(0.94f, 0.96f, 0.98f));
            this.panel(featBox.transform, "BoxBorder", 0, 0, 380, 320, new Color(0.70f, 0.75f, 0.82f));

            if (productSprites[featProduct.Idx] != null)
            {
                var img = this.panel(featBox.transform, "FeatImg", 20, 20, 340, 180, Color.white);
                img.sprite = productSprites[featProduct.Idx];
                img.preserveAspect = true;
            }
            else
            {
                this.label(featBox.transform, "FeatIcon", "[ESSENTIAL RATION]", 20, 60, 340, 40, 24, new Color(0.40f, 0.55f, 0.70f), TextAlignmentOptions.Center);
            }

            this.label(featBox.transform, "FeatName", texts.Rows[featProduct.NameIdx].Text, 20, 215, 340, 35, 22, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.Center);
            this.label(featBox.transform, "FeatPrice", $"현재가 {GameSessionManager.Instance.EnsureDailyPrices().Prices[featProduct.Idx]:N0} G", 20, 255, 340, 40, 26, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.Center);

            // Page 1 Navigation (Folded corner next button)
            this.label(coverCard.transform, "PageNum", "1 / 4", 520, 435, 100, 30, 16, new Color(0.50f, 0.55f, 0.60f), TextAlignmentOptions.Center);
            this.makeButton(coverCard.transform, "BtnNextP1", "Next Page >", 980, 425, 130, 40, () => { this.currentFlyerPage = 2; this.renderFlyerBooklet(); }, 16, new Color(0.24f, 0.45f, 0.65f));
        }
        else if (this.currentFlyerPage == 2 || this.currentFlyerPage == 3)
        {
            // ---------------- PAGES 2 & 3: SPREAD CATALOG ----------------
            var spreadCard = this.panel(this.hubContentContainer, "SpreadCard", 40, 65, 1140, 480, Color.white);
            this.panel(spreadCard.transform, "Border", 0, 0, 1140, 480, new Color(0.80f, 0.84f, 0.88f));

            // Center Soft Divider Line
            this.panel(spreadCard.transform, "CenterDivider", 569, 15, 2, 450, new Color(0.78f, 0.82f, 0.86f));

            // Left Page (Products 0, 1, 2)
            this.label(spreadCard.transform, "LeftPageHeader", "BASIC FOOD SUPPLIES", 30, 15, 500, 30, 16, new Color(0.30f, 0.35f, 0.45f), TextAlignmentOptions.Center);
            for (int i = 0; i < 3 && i < products.Length; i++)
            {
                var prod = products[i];
                float y = 55 + i * 115;
                var slot = this.panel(spreadCard.transform, $"Slot_{i}", 40, y, 480, 100, new Color(0.95f, 0.97f, 0.99f));
                this.panel(slot.transform, "SlotBorder", 0, 0, 480, 100, new Color(0.80f, 0.84f, 0.88f));

                if (productSprites[prod.Idx] != null)
                {
                    var img = this.panel(slot.transform, "Img", 10, 10, 100, 80, Color.white);
                    img.sprite = productSprites[prod.Idx];
                    img.preserveAspect = true;
                }
                else
                {
                    this.label(slot.transform, "Token", "[ITEM]", 10, 30, 100, 35, 16, new Color(0.45f, 0.55f, 0.68f), TextAlignmentOptions.Center);
                }

                this.label(slot.transform, "Name", texts.Rows[prod.NameIdx].Text, 130, 20, 240, 30, 18, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Tag", $"Available Day {((long)prod.AvailableDay + 1)}", 130, 52, 240, 25, 13, new Color(0.55f, 0.60f, 0.68f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Price", $"{GameSessionManager.Instance.EnsureDailyPrices().Prices[prod.Idx]:N0} G", 370, 32, 90, 35, 20, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.MidlineRight);
            }

            // Right Page (Products 3, 4, 5)
            this.label(spreadCard.transform, "RightPageHeader", "MEDICAL & UTILITY", 610, 15, 500, 30, 16, new Color(0.30f, 0.35f, 0.45f), TextAlignmentOptions.Center);
            for (int i = 3; i < 6 && i < products.Length; i++)
            {
                var prod = products[i];
                float y = 55 + (i - 3) * 115;
                var slot = this.panel(spreadCard.transform, $"Slot_{i}", 620, y, 480, 100, new Color(0.95f, 0.97f, 0.99f));
                this.panel(slot.transform, "SlotBorder", 0, 0, 480, 100, new Color(0.80f, 0.84f, 0.88f));

                if (productSprites[prod.Idx] != null)
                {
                    var img = this.panel(slot.transform, "Img", 10, 10, 100, 80, Color.white);
                    img.sprite = productSprites[prod.Idx];
                    img.preserveAspect = true;
                }
                else
                {
                    this.label(slot.transform, "Token", "[ITEM]", 10, 30, 100, 35, 16, new Color(0.45f, 0.55f, 0.68f), TextAlignmentOptions.Center);
                }

                this.label(slot.transform, "Name", texts.Rows[prod.NameIdx].Text, 130, 20, 240, 30, 18, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Tag", $"Available Day {((long)prod.AvailableDay + 1)}", 130, 52, 240, 25, 13, new Color(0.55f, 0.60f, 0.68f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Price", $"{GameSessionManager.Instance.EnsureDailyPrices().Prices[prod.Idx]:N0} G", 370, 32, 90, 35, 20, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.MidlineRight);
            }

            // Navigation
            this.makeButton(spreadCard.transform, "BtnPrevP2", "< Prev Page", 30, 425, 130, 40, () => { this.currentFlyerPage = 1; this.renderFlyerBooklet(); }, 16, new Color(0.24f, 0.45f, 0.65f));
            this.label(spreadCard.transform, "PageNum", "2 - 3 / 4", 520, 435, 100, 30, 16, new Color(0.50f, 0.55f, 0.60f), TextAlignmentOptions.Center);
            this.makeButton(spreadCard.transform, "BtnNextP3", "Next Page >", 980, 425, 130, 40, () => { this.currentFlyerPage = 4; this.renderFlyerBooklet(); }, 16, new Color(0.24f, 0.45f, 0.65f));
        }
        else
        {
            // ---------------- PAGE 4: END & ADVANCED GOODS ----------------
            var p4Card = this.panel(this.hubContentContainer, "Page4Card", 40, 65, 1140, 480, Color.white);
            this.panel(p4Card.transform, "Border", 0, 0, 1140, 480, new Color(0.80f, 0.84f, 0.88f));

            this.label(p4Card.transform, "P4Title", "SPECIAL ADVANCED RATIONS & INSPECTOR RULES", 30, 18, 1080, 30, 18, new Color(0.18f, 0.22f, 0.30f), TextAlignmentOptions.Center);

            // Remaining products 6..9
            for (int i = 6; i < products.Length && i < 10; i++)
            {
                var prod = products[i];
                float colX = 50 + (i - 6) * 265;
                var slot = this.panel(p4Card.transform, $"Slot_{i}", colX, 65, 245, 180, new Color(0.95f, 0.97f, 0.99f));
                this.panel(slot.transform, "SlotBorder", 0, 0, 245, 180, new Color(0.80f, 0.84f, 0.88f));

                if (productSprites[prod.Idx] != null)
                {
                    var img = this.panel(slot.transform, "Img", 10, 10, 225, 90, Color.white);
                    img.sprite = productSprites[prod.Idx];
                    img.preserveAspect = true;
                }
                else
                {
                    this.label(slot.transform, "Token", "[SPECIAL]", 10, 35, 225, 30, 16, new Color(0.45f, 0.55f, 0.68f), TextAlignmentOptions.Center);
                }

                this.label(slot.transform, "Name", texts.Rows[prod.NameIdx].Text, 10, 110, 225, 28, 16, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.Center);
                this.label(slot.transform, "Price", $"{GameSessionManager.Instance.EnsureDailyPrices().Prices[prod.Idx]:N0} G", 10, 140, 225, 30, 19, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.Center);
            }

            // Rules Box
            var ruleBox = this.panel(p4Card.transform, "NoticeBox", 50, 270, 1040, 135, new Color(0.94f, 0.95f, 0.96f));
            this.panel(ruleBox.transform, "RBorder", 0, 0, 1040, 135, new Color(0.78f, 0.82f, 0.86f));
            this.label(ruleBox.transform, "RTitle", "DISTRICT 9 CASHIER PROTOCOL", 20, 12, 1000, 25, 15, new Color(0.20f, 0.25f, 0.35f));
            this.label(ruleBox.transform, "RTxt",
                "- All transactions are subject to unexpected Inspector audits.\n" +
                "- Overcharging beyond customer tolerance will immediately deduct reputation.\n" +
                "- Maintain at least 50 Morality and save cash to secure your family's Safe Zone entry.",
                20, 42, 1000, 80, 14, new Color(0.35f, 0.40f, 0.48f));

            // Navigation
            this.makeButton(p4Card.transform, "BtnPrevP3", "< Prev Page", 30, 425, 130, 40, () => { this.currentFlyerPage = 3; this.renderFlyerBooklet(); }, 16, new Color(0.24f, 0.45f, 0.65f));
            this.label(p4Card.transform, "PageNum", "4 / 4", 520, 435, 100, 30, 16, new Color(0.50f, 0.55f, 0.60f), TextAlignmentOptions.Center);
        }
    }


    // =========================================================================
    // 10. TAB 2: LEDGER NOTEBOOK VIEW (Scrollable Historical Records)
    // =========================================================================

    /// <summary>별도 장부 상태 없이 현재 Finance 금액과 마지막 정산만 표시한다.</summary>
    private void renderLedgerNotebook()
    {
        label(hubContentContainer, "FinanceSummary", $"Cash: {economy.QueryService.CurrentBalance:N0} G\nLast closed revenue: {closedRevenue:N0} G\nPersistent ledger: disabled", 40, 40, 1100, 160, 24, Color.black);
    }


    // =========================================================================
    // 11. TAB 3: BUILDING UPGRADE VIEW (Isometric Tier Progression)
    // =========================================================================

    /// <summary>MainScene 통합 화면의 renderBuildingUpgrade 처리를 수행한다.</summary>
    private void renderBuildingUpgrade()
    {
        label(hubContentContainer, "Unavailable", "Not connected in this integration.", 40, 40, 1100, 60, 22);
    }

    private void renderTierCard(Transform parent, int tier, string title, string perks, int cost, bool isOwned, float x, float y, float width, float height)
    {
        var card = this.panel(parent, $"TierCard_{tier}", x, y, width, height, isOwned ? new Color(0.93f, 0.96f, 0.99f) : new Color(0.97f, 0.98f, 0.99f));
        this.panel(card.transform, "Border", 0, 0, width, height, isOwned ? new Color(0.35f, 0.60f, 0.85f) : new Color(0.78f, 0.82f, 0.88f));

        // Header
        this.panel(card.transform, "Head", 0, 0, width, 45, isOwned ? new Color(0.22f, 0.45f, 0.70f) : new Color(0.32f, 0.38f, 0.46f));
        this.label(card.transform, "Title", title, 10, 10, width - 20, 26, 16, Color.white, TextAlignmentOptions.Center);

        // Visual Placeholder (Isometric Stall)
        var imgBox = this.panel(card.transform, "ImgBox", 25, 60, width - 50, 140, new Color(0.12f, 0.16f, 0.22f, 0.95f));
        this.label(imgBox.transform, "IsoLabel", $"[TIER {tier} STALL]\nIsometric Model", 10, 45, width - 70, 50, 16, new Color(0.65f, 0.75f, 0.88f), TextAlignmentOptions.Center);

        // Perks
        this.label(card.transform, "Perks", perks, 20, 215, width - 40, 70, 14, new Color(0.20f, 0.25f, 0.32f), TextAlignmentOptions.TopLeft);

        // Action / Status Button
        if (isOwned)
        {
            this.label(card.transform, "Status", "<color=#188038><b>CURRENT ACTIVE STALL</b></color>", 10, 315, width - 20, 30, 16, Color.black, TextAlignmentOptions.Center);
        }
        else if (tier == this.currentSaveData.buildingTier + 1)
        {
            bool canAfford = this.currentSaveData.cash >= cost;
            var btn = this.makeButton(card.transform, $"BtnUpgrade_{tier}", $"UPGRADE ({cost:N0} G)", 25, 310, width - 50, 46, () => this.purchaseUpgrade(tier, cost), 16, canAfford ? new Color(0.20f, 0.58f, 0.36f) : new Color(0.55f, 0.55f, 0.55f));
            btn.interactable = canAfford;
        }
        else
        {
            this.label(card.transform, "Locked", $"Locked (Requires Tier {tier - 1})", 10, 315, width - 20, 30, 15, new Color(0.60f, 0.65f, 0.70f), TextAlignmentOptions.Center);
        }
    }

    /// <summary>MainScene 통합 화면의 purchaseUpgrade 처리를 수행한다.</summary>
    private void purchaseUpgrade(int nextTier, int cost)
    {
        // 업그레이드와 별도 자금 저장은 통합 범위 밖이다.
    }


    // =========================================================================
    // 12. TAB 4: OPTIONS SETTINGS VIEW
    // =========================================================================

    /// <summary>MainScene 통합 화면의 renderOptionsSettings 처리를 수행한다.</summary>
    private void renderOptionsSettings()
    {
        label(hubContentContainer, "Unavailable", "Not connected in this integration.", 40, 40, 1100, 60, 22);
    }


    // =========================================================================
    // 13. TAB 5: SAVE & QUIT CONFIRM MODAL
    // =========================================================================

    private void buildQuitConfirmModal()
    {
        this.quitConfirmModal = this.panel(this.mainHubRoot.transform, "QuitModalDim", 0, 0, 1280, 720, new Color(0.05f, 0.07f, 0.09f, 0.85f)).gameObject;

        var box = this.panel(this.quitConfirmModal.transform, "Box", 380, 220, 520, 260, Color.white);
        this.panel(box.transform, "Border", 0, 0, 520, 260, new Color(0.70f, 0.75f, 0.82f));

        this.label(box.transform, "Prompt", "Return to Title? Saving is disabled.", 30, 40, 460, 60, 20, new Color(0.15f, 0.20f, 0.28f), TextAlignmentOptions.Center);

        // Yes: Save & Return to Title
        this.makeButton(box.transform, "BtnYes", "RETURN TO TITLE", 70, 150, 180, 48, () => {
            // Persistence disabled in integrated scene.
            this.quitConfirmModal.SetActive(false);
            this.setViewState(GameViewState.Title);
        }, 16, new Color(0.22f, 0.55f, 0.38f));

        // No: Cancel & Stay
        this.makeButton(box.transform, "BtnNo", "NO (CANCEL)", 270, 150, 180, 48, () => {
            this.quitConfirmModal.SetActive(false);
            this.selectHubTab(HubTab.Flyer);
        }, 16, new Color(0.48f, 0.22f, 0.22f));

        this.quitConfirmModal.SetActive(false);
    }


    // =========================================================================
    // 14. TRADING CORE SESSION VIEW (Full HD POS Gameplay)
    // =========================================================================

    private void buildTradingScreen()
    {
        this.tradingRoot = this.rect(this.canvasRoot, "TradingSessionView", 0, 0, 1280, 720).gameObject;

        // Background
        this.panel(this.tradingRoot.transform, "MainBackground", 0, 0, 1280, 720, new Color(0.075f, 0.095f, 0.12f, 1f));

        // Waiting Customers Silhouettes
        this.waiting[0] = this.panel(this.tradingRoot.transform, "WaitingLeft", 185, 170, 215, 350, new Color(0.20f, 0.25f, 0.30f, 0.85f));
        this.label(this.waiting[0].transform, "TagL", "[WAITING]", 20, 140, 175, 35, 18, new Color(0.55f, 0.65f, 0.75f), TextAlignmentOptions.Center);

        this.waiting[1] = this.panel(this.tradingRoot.transform, "WaitingRight", 780, 170, 215, 350, new Color(0.20f, 0.25f, 0.30f, 0.85f));
        this.label(this.waiting[1].transform, "TagR", "[WAITING]", 20, 140, 175, 35, 18, new Color(0.55f, 0.65f, 0.75f), TextAlignmentOptions.Center);

        // Customer Portrait Frame
        this.portrait = this.panel(this.tradingRoot.transform, "Customer", 405, 82, 400, 550, new Color(0.16f, 0.20f, 0.26f, 0.95f));
        this.panel(this.portrait.transform, "CustBorder", 0, 0, 400, 550, new Color(0.28f, 0.34f, 0.42f, 0.6f));
        this.panel(this.portrait.transform, "CustInner", 3, 3, 394, 544, new Color(0.14f, 0.17f, 0.22f, 1f));
        this.label(this.portrait.transform, "CustomerTag", "[CUSTOMER]", 50, 190, 300, 45, 26, new Color(0.7f, 0.8f, 0.9f), TextAlignmentOptions.Center);

        // Counter Desk
        this.panel(this.tradingRoot.transform, "CounterBarrier", 0, 215, 1280, 535, new Color(0.045f, 0.060f, 0.080f, 0.98f));
        this.panel(this.tradingRoot.transform, "CounterEdge", 0, 215, 1280, 6, new Color(0.15f, 0.20f, 0.25f, 1f));

        // Player / Daughter Placeholder
        var daughter = this.panel(this.tradingRoot.transform, "Daughter", -10, 480, 260, 260, new Color(0.12f, 0.15f, 0.19f, 0.92f));
        this.panel(daughter.transform, "DaughterBorder", 0, 0, 260, 260, new Color(0.25f, 0.30f, 0.38f, 0.7f));
        this.label(daughter.transform, "DaughterTag", "[DAUGHTER]\nPlayer", 20, 100, 220, 60, 19, new Color(0.8f, 0.85f, 0.9f), TextAlignmentOptions.Center);

        // TopBar (1280 x 83)
        this.panel(this.tradingRoot.transform, "TopBar", 0, 0, 1280, 83, new Color(0.045f, 0.065f, 0.075f, 0.97f));
        this.label(this.tradingRoot.transform, "Title", "PROJECT CASHIER", 25, 12, 270, 28, 22, null, TextAlignmentOptions.MidlineLeft);
        this.label(this.tradingRoot.transform, "Place", "DISTRICT 9 | RATION STALL", 25, 44, 270, 23, 14, new Color(0.60f, 0.67f, 0.69f), TextAlignmentOptions.MidlineLeft);

        this.dayText = this.label(this.tradingRoot.transform, "Day", "", 330, 14, 230, 30, 22, null, TextAlignmentOptions.MidlineLeft);
        this.statusText = this.label(this.tradingRoot.transform, "Status", "", 330, 48, 270, 24, 15, null, TextAlignmentOptions.MidlineLeft);

        this.cashText = this.label(this.tradingRoot.transform, "Cash", "", 655, 14, 240, 30, 25, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.MidlineLeft);
        this.goalHintText = this.label(this.tradingRoot.transform, "GoalHint", "Citizenship: disabled", 655, 49, 265, 23, 14, new Color(0.70f, 0.75f, 0.80f), TextAlignmentOptions.MidlineLeft);

        this.gaugeText = this.label(this.tradingRoot.transform, "GaugeText", "", 945, 14, 240, 25, 18, null, TextAlignmentOptions.MidlineLeft);
        this.panel(this.tradingRoot.transform, "GaugeRail", 945, 51, 300, 8, new Color(0.16f, 0.20f, 0.22f));
        this.gaugeFill = this.panel(this.tradingRoot.transform, "GaugeFill", 945, 51, 300, 8, Color.white);

        this.queueText = this.label(this.tradingRoot.transform, "Queue", "", 36, 106, 350, 29, 18, null, TextAlignmentOptions.MidlineLeft);

        // Dialogue Box
        this.panel(this.tradingRoot.transform, "DialogueBorder", 318, 357, 564, 74, new Color(0.22f, 0.28f, 0.32f));
        this.panel(this.tradingRoot.transform, "DialoguePanel", 320, 359, 560, 70, new Color(0.040f, 0.055f, 0.065f, 0.96f));
        this.dialogueText = this.label(this.tradingRoot.transform, "Dialogue", "", 337, 370, 525, 49, 19, new Color(0.95f, 0.95f, 0.95f), TextAlignmentOptions.Center);

        // Basket Root on Counter
        this.basketRoot = this.rect(this.tradingRoot.transform, "Basket", 275, 442, 635, 138);

        // Instructions
        this.reasonText = this.label(this.tradingRoot.transform, "Feedback", "", 273, 674, 655, 35, 14, new Color(0.70f, 0.75f, 0.80f), TextAlignmentOptions.Center);

        nextCustomerButton = makeButton(tradingRoot.transform, "NextCustomer", "NEXT CUSTOMER", 30, 150, 220, 40, GenerateCustomer, 18);
        endDayButton = makeButton(tradingRoot.transform, "EndDay", "END DAY", 30, 195, 220, 40, endDay, 18);
        // POS Keypad
        this.panel(this.tradingRoot.transform, "RegisterBorder", 945, 403, 309, 304, new Color(0.24f, 0.30f, 0.36f));
        this.panel(this.tradingRoot.transform, "Register", 947, 405, 305, 300, new Color(0.045f, 0.065f, 0.075f, 0.98f));
        this.label(this.tradingRoot.transform, "PriceHeading", "CHARGE AMOUNT", 962, 416, 160, 23, 15, new Color(0.60f, 0.70f, 0.80f), TextAlignmentOptions.MidlineLeft);
        this.inputText = this.label(this.tradingRoot.transform, "PriceInput", "0 G", 962, 443, 276, 40, 30, Color.white, TextAlignmentOptions.MidlineRight);

        for (int i = 1; i <= 9; i++)
        {
            string val = i.ToString();
            float bx = 961 + ((i - 1) % 3) * 94;
            float by = 491 + ((i - 1) / 3) * 38;
            this.makeButton(this.tradingRoot.transform, $"Digit{val}", val, bx, by, 88, 33, () => this.digit(val), 18);
        }

        this.makeButton(this.tradingRoot.transform, "Erase", "DEL", 961, 605, 88, 33, this.erase, 16, new Color(0.50f, 0.22f, 0.22f));
        this.makeButton(this.tradingRoot.transform, "Digit0", "0", 1055, 605, 88, 33, () => this.digit("0"), 18);
        this.makeButton(this.tradingRoot.transform, "Digit00", "00", 1149, 605, 88, 33, this.doubleZero, 18, new Color(0.25f, 0.34f, 0.44f));

        this.confirmButton = this.makeButton(this.tradingRoot.transform, "Confirm", "CONFIRM SALE [Enter]", 961, 646, 276, 44, this.confirm, 18, new Color(0.28f, 0.55f, 0.42f));
    }

    private void handleTradingInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame) this.pause();

        if (canOffer)
        {
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame ||
                kb.periodKey.wasPressedThisFrame || kb.numpadPeriodKey.wasPressedThisFrame)
            {
                this.doubleZero();
            }

            for (int i = 0; i < 10; i++)
            {
                Key topKey = i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1);
                Key numpadKey = Key.Numpad0 + i;
                if (kb[topKey].wasPressedThisFrame || kb[numpadKey].wasPressedThisFrame)
                {
                    this.digit(i.ToString());
                }
            }

            if (kb.backspaceKey.wasPressedThisFrame) this.erase();
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) this.confirm();
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape)) this.pause();

        if (canOffer)
        {
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus) ||
                Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.KeypadPeriod))
            {
                this.doubleZero();
            }

            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                {
                    this.digit(i.ToString());
                    break;
                }
            }

            if (Input.GetKeyDown(KeyCode.Backspace)) this.erase();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) this.confirm();
        }
#endif
    }

    public void digit(string d)
    {
        if (!canOffer || this.invalidAmount) return;
        if (d == null || d.Length != 1 || d[0] < '0' || d[0] > '9' || this.amount.Length >= 18) return;
        if (this.amount == "0") this.amount = "";
        this.amount += d;
        this.alertUntil = 0;
        this.showAmount();
    }

    public void doubleZero()
    {
        if (!canOffer || this.invalidAmount) return;
        if (string.IsNullOrEmpty(this.amount) || this.amount == "0") return;
        if (this.amount.Length + 2 > 18) return;
        this.amount += "00";
        this.alertUntil = 0;
        this.showAmount();
    }

    public void erase()
    {
        if (!canOffer) return;
        if (this.invalidAmount)
        {
            this.invalidAmount = false;
            this.amount = "";
            this.showAmount();
            return;
        }
        if (this.amount.Length > 0)
        {
            this.amount = this.amount.Substring(0, this.amount.Length - 1);
        }
        this.showAmount();
    }

    /// <summary>MainScene 통합 화면의 confirm 처리를 수행한다.</summary>
    public void confirm()
    {
        if (!canOffer || !long.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out long total) || total <= 0) return;
        try
        {
            // Customer가 판정하고 Finance만 금액을 변경한다. 재호출은 방문 상태로 차단한다.
            bool accepted = CurrentVisit.SubmitOffer(total);
            if (accepted && !economy.DailyAggregationService.TryApplyTransaction(new TransactionResult(total, 0)))
                throw new InvalidOperationException("영업 종료로 거래 수입 반영이 거부되었습니다.");
            amount = "";
            resultRemainingSeconds = 3;
            refreshTradingView();
        }
        catch (Exception exception) { fail(exception); }
    }

    /// <summary>MainScene 통합 화면의 pause 처리를 수행한다.</summary>
    public void pause()
    {
        if (!ready || faulted) return;
        paused = !paused;
        refreshTradingView();
    }

    private void showAmount()
    {
        if (this.inputText == null) return;
        this.inputText.fontSize = (this.invalidAmount ? 16f : 30f) * S;
        this.inputText.text = this.invalidAmount ? "Digits only - DEL to clear" :
                              string.IsNullOrEmpty(this.amount) ? "<color=#777777>0 G</color>" :
                              $"{long.Parse(this.amount, CultureInfo.InvariantCulture):N0} G";
    }

    /// <summary>MainScene 통합 화면의 refreshTradingView 처리를 수행한다.</summary>
    private void refreshTradingView()
    {
        if (!ready) return;
        portrait.gameObject.SetActive(CurrentVisit != null);
        dayText.text = $"DAY {day:00}";
        cashText.text = $"{economy.QueryService.CurrentBalance:N0} G";
        statusText.text = faulted ? "ERROR - CHECK CONSOLE" : "Finance / Customer connected";
        goalHintText.text = "Save / reputation / citizenship: disabled";
        queueText.text = $"대기 {customerQueue.Waiting.Count}/10";
        gaugeText.text = paused ? "PAUSED" : "5초 간격 입장";
        gaugeFill.gameObject.SetActive(false);
        foreach (var image in waiting) image.gameObject.SetActive(false);
        confirmButton.interactable = canOffer;
        nextCustomerButton.interactable = !faulted && !paused && !showingGuide && economy.QueryService.IsDayOpen
            && (CurrentVisit == null || CurrentVisit.State == CustomerState.Accepted || CurrentVisit.State == CustomerState.Rejected);
        endDayButton.interactable = !faulted && !paused && !showingGuide && economy.QueryService.IsDayOpen
            && CurrentVisit?.State != CustomerState.AwaitingOffer;
        dialogueText.text = CurrentVisit == null ? "" : texts.Rows[CurrentVisit.FeedbackTextIdx].Text;
        reasonText.text = CurrentVisit?.WasAccepted.HasValue == true
            ? ($"{CurrentVisit.OutcomeLabel} (판정값: {(int)CurrentVisit.Outcome})" + (CurrentVisit.WasAccepted.Value ? " - income applied" : " - no income")) : "Enter the whole basket total";
        if (CurrentVisit != null)
        {
            var appearance = catalog.Appearances.Rows[CurrentVisit.AppearanceIdx];
            var inner = portrait.transform.Find("CustInner").GetComponent<Image>();
            inner.color = new Color32(appearance.ColorR, appearance.ColorG, appearance.ColorB, appearance.ColorA);
            var identity = portrait.transform.Find("CustomerTag").GetComponent<TextMeshProUGUI>();
            identity.rectTransform.anchoredPosition = new Vector2(50 * S, -20 * S);
            identity.color = Color.black;
            identity.text = $"{CurrentVisit.AppearanceIdx} + {CurrentVisit.DispositionIdx}";
        }
        showAmount();
        renderBasket();
        renderTradingModal();
    }

    /// <summary>MainScene 통합 화면의 renderBasket 처리를 수행한다.</summary>
    private void renderBasket()
    {
        foreach (var card in activeBasketCards) { card.SetActive(false); Destroy(card); }
        activeBasketCards.Clear();
        if (CurrentVisit == null || showingGuide) return;
        for (int i = 0; i < CurrentVisit.Items.Count; i++)
        {
            var item = CurrentVisit.Items[i];
            var product = catalog.Products.Rows[item.ProductIdx];
            var card = panel(basketRoot, $"Product_{item.ProductIdx}", (i % 4) * 150, (i / 4) * 100, 130, 130, Color.white);
            card.sprite = productSprites[item.ProductIdx];
            card.preserveAspect = true;
            label(card.transform, "Name", texts.Rows[product.NameIdx].Text + $" × {item.Quantity}\n기본: {product.BasePrice:N0}\n현재: {item.UnitPrice:N0} / 개",
                4, 35, 122, 60, 16, Color.black, TextAlignmentOptions.Center);
            activeBasketCards.Add(card.gameObject);
        }
    }


    /// <summary>MainScene 통합 화면의 renderTradingModal 처리를 수행한다.</summary>
    private void renderTradingModal()
    {
        if (tradingModal != null) { tradingModal.gameObject.SetActive(false); Destroy(tradingModal.gameObject); tradingModal = null; }
        if (faulted) { createTradingModalWindow("INTEGRATION ERROR", "Check Console. Input is disabled; no automatic retry."); return; }
        if (paused)
        {
            createTradingModalWindow("PAUSED", "No transaction input.");
            makeButton(tradingModal, "Resume", "RESUME", 190, 320, 360, 48, pause, 18);
            return;
        }
        if (showingGuide)
        {
            createTradingModalWindow("PRODUCT PRICES", "CSV catalogue - available products");
            var products = catalog.Products.Rows.Values.Where(p => p.IsAvailable && p.AvailableDay <= day - 1).OrderBy(p => p.Idx).ToArray();
            for (int i = 0; i < products.Length; i++)
            {
                var p = products[i];
                label(tradingModal, $"Price_{p.Idx}", texts.Rows[p.NameIdx].Text + $"  {GameSessionManager.Instance.EnsureDailyPrices().Prices[p.Idx]:N0}",
                    25 + (i % 4) * 175, 140 + (i / 4) * 55, 170, 50, 15);
            }
            makeButton(tradingModal, "OpenShop", "OPEN STORE", 170, 435, 400, 46, openShop, 18);
        }
        else if (!economy.QueryService.IsDayOpen)
        {
            createTradingModalWindow($"DAY {day} SETTLEMENT", $"Revenue: {closedRevenue:N0} G / Cash: {economy.QueryService.CurrentBalance:N0} G");
            bool due = day % economy.QueryService.MaintenanceCycleDays == 0
                && economy.MaintenanceService.LastPaidRound < day / economy.QueryService.MaintenanceCycleDays;
            if (due && economy.QueryService.TryGetNextMaintenanceAmount(out long required))
            {
                label(tradingModal, "Maintenance", $"Maintenance: {required:N0} G", 40, 180, 650, 60, 22);
                makeButton(tradingModal, "PayTribute", "PAY MAINTENANCE", 80, 420, 400, 48, payMaintenance, 18);
            }
            else makeButton(tradingModal, "NextDay", "COMPLETE DAY", 190, 420, 360, 48, completeTradingDayAndReturnToHub, 18);
        }
    }

    private void createTradingModalWindow(string title, string subtitle)
    {
        this.tradingModal = this.rect(this.tradingRoot.transform, "TradingModal", 270, 111, 740, 510);
        this.panel(this.tradingModal, "Border", 0, 0, 740, 510, new Color(0.46f, 0.51f, 0.51f));
        this.panel(this.tradingModal, "Paper", 2, 2, 736, 506, new Color(0.075f, 0.095f, 0.105f, 0.99f));
        this.label(this.tradingModal, "ModalTitle", title, 35, 28, 670, 44, 30, Color.white, TextAlignmentOptions.MidlineLeft);
        this.label(this.tradingModal, "ModalSubtitle", subtitle, 35, 85, 670, 55, 18, new Color(0.70f, 0.75f, 0.80f), TextAlignmentOptions.TopLeft);
    }


    /// <summary>MainScene 통합 화면의 restartTradingDay 처리를 수행한다.</summary>
    private void restartTradingDay()
    {
        // 경제 상태를 복제하거나 초기화하지 않는다. 재시작은 미연결이다.
    }

    /// <summary>MainScene 통합 화면의 completeTradingDayAndReturnToHub 처리를 수행한다.</summary>
    private void completeTradingDayAndReturnToHub()
    {
        if (faulted || paused || economy.QueryService.IsDayOpen || currentViewState != GameViewState.Trading) return;
        try
        {
            GameSessionManager.Instance.CompleteDay((uint)(day - 1));
            GameSessionManager.Instance.EnsureDailyPrices();
            CurrentVisit = null;
            setViewState(GameViewState.MainHub);
        }
        catch (Exception exception) { fail(exception); }
    }


    // =========================================================================
    // 15. EVENT SYSTEM & UTILITY HELPERS
    // =========================================================================


    /// <summary>InitScene이 소유한 manager의 준비를 기다린 뒤 UI를 만든다. 직접 Main 실행은 거부한다.</summary>
    private async void Start()
    {
        try
        {
            var token = this.GetCancellationTokenOnDestroy();
            if (DataTableManager.Instance == null || GameSessionManager.Instance == null)
                throw new InvalidOperationException("InitScene부터 실행하세요.");
            await DataTableManager.Instance.EnsureDataLoadedAsync().AttachExternalCancellation(token);
            if (!GameSessionManager.Instance.IsInitialized) throw new InvalidOperationException("경제 세션이 준비되지 않았습니다.");
            catalog = DataTableManager.Instance.Customers;
            customerQueue = new CustomerQueue(createQueuedVisit, catalog.Dispositions.Rows);
            texts = DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text);
            economy = GameSessionManager.Instance.Economy;
            if (economy.QueryService.IsDayOpen) throw new InvalidOperationException("영업 중 씬 재진입은 아직 지원하지 않습니다.");
            GameSessionManager.Instance.EnsureDailyPrices();
            squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            // 한국어는 실행 환경 글꼴로 표시한다. TMP 기본 자산은 변경하지 않는다.
            displayFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 32);
            if (displayFont == null) throw new InvalidOperationException("통합 테스트 UI에는 Malgun Gothic 글꼴이 필요합니다.");
            var resources = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
            foreach (var p in catalog.Products.Rows.Values)
            {
                Sprite sprite = squareSprite;
                if (p.ImageResourceIdx.HasValue)
                    sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(resources.GetResourcePath(p.ImageResourceIdx.Value)).AttachExternalCancellation(token);
                token.ThrowIfCancellationRequested();
                if (sprite == null) throw new InvalidOperationException($"상품 이미지 실패: {p.Idx}");
                productSprites.Add(p.Idx, sprite);
            }
            products = catalog.Products.Rows.Values.OrderBy(p => p.Idx).ToArray();
            ready = true;
            buildAllScreens();
            setViewState(GameViewState.Title);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { fail(exception); }
    }

    /// <summary>영업 시작은 Finance 집계에 한 번 전달한다.</summary>
    private void openShop()
    {
        if (!ready || faulted || paused || !showingGuide) return;
        try
        {
            GameSessionManager.Instance.BeginTradingDay();
            customerQueue.Start();
            customerQueue.TryAdd(); // 최초 계산대 손님. 이후에는 5초 간격 입장.
            showingGuide = false;
            GenerateCustomer();
        }
        catch (Exception exception) { fail(exception); }
    }

    /// <summary>결과 손님을 정리하고 FIFO 맨 앞만 계산대로 이동한다. 빈 줄에서 손님을 즉석 생성하지 않는다.</summary>
    public void GenerateCustomer()
    {
        if (!ready || faulted || paused || showingGuide || !economy.QueryService.IsDayOpen
            || CurrentVisit?.State == CustomerState.AwaitingOffer) return;
        try
        {
            if (CurrentVisit != null && CurrentVisit.State != CustomerState.Departed) CurrentVisit.Depart();
            CurrentVisit = customerQueue.TakeNext();
            CurrentVisit?.BeginOffer();
            amount = "";
            refreshTradingView();
        }
        catch (Exception exception) { fail(exception); }
    }

    /// <summary>줄 합류 시점 현재가와 외형·성향·구매 목록을 고정한다.</summary>
    /// <returns>판매 후보가 없으면 null.</returns>
    private CustomerVisit createQueuedVisit() => generator.Generate(catalog.Appearances.Rows.Keys.OrderBy(x => x).ToArray(),
        catalog.Dispositions.Rows.Values.OrderBy(x => x.Idx).ToArray(), catalog.Products.Rows,
        (uint)(day - 1), GameSessionManager.Instance.EnsureDailyPrices().Prices);

    /// <summary>고정된 10개 슬롯을 재사용한다. 이탈자는 줄에서 제거하고 불만 텍스트만 잠시 남긴다.</summary>
    private void renderQueue()
    {
        if (queueLeaveText == null)
        {
            for (int i = 0; i < CustomerQueue.Capacity; i++)
            {
                var image = panel(tradingRoot.transform, "Queue_" + i, i < 5 ? 10 : 135, 260 + (i % 5) * 65, 120, 60, Color.white);
                queuePortraits[i] = image;
                queueLabels[i] = label(image.transform, "IdentitySpeech", "", 4, 2, 112, 56, 11, Color.black, TextAlignmentOptions.Center);
            }
            queueLeaveText = label(tradingRoot.transform, "QueueLeaveSpeech", "", 825, 85, 430, 310, 16, Color.yellow, TextAlignmentOptions.TopLeft);
            queueLeaveText.raycastTarget = false;
        }
        for (int i = 0; i < queuePortraits.Length; i++)
        {
            bool visible = i < customerQueue.Waiting.Count;
            queuePortraits[i].gameObject.SetActive(visible);
            if (!visible) continue;
            var entry = customerQueue.Waiting[i];
            var appearance = catalog.Appearances.Rows[entry.Visit.AppearanceIdx];
            queuePortraits[i].color = new Color32(appearance.ColorR, appearance.ColorG, appearance.ColorB, appearance.ColorA);
            uint speech = customerQueue.GetSpeech(entry);
            queueLabels[i].text = $"#{i + 1} {entry.Visit.AppearanceIdx}+{entry.Visit.DispositionIdx}" + (speech == 0 ? "" : "\n" + texts.Rows[speech].Text);
        }
        queueLeaveText.text = string.Join("\n", customerQueue.Leaving.Select(x => $"{x.Visit.AppearanceIdx}+{x.Visit.DispositionIdx}: {texts.Rows[customerQueue.GetSpeech(x)].Text}"));
        queueText.text = $"대기 {customerQueue.Waiting.Count}/10";
    }

    /// <summary>미판정 손님이 없을 때 일일 집계를 닫는다.</summary>
    private void endDay()
    {
        if (!ready || faulted || paused || showingGuide || !economy.QueryService.IsDayOpen
            || CurrentVisit?.State == CustomerState.AwaitingOffer) return;
        if (CurrentVisit != null && CurrentVisit.State != CustomerState.Departed) CurrentVisit.Depart();
        closedRevenue = GameSessionManager.Instance.EndTradingDay();
        customerQueue.Stop();
        refreshTradingView();
    }

    /// <summary>Finance가 정한 순서와 금액으로 납부한다. 부족하면 재시도 가능한 상태를 유지한다.</summary>
    private void payMaintenance()
    {
        if (!ready || faulted || paused || economy.QueryService.IsDayOpen) return;
        int round = day / economy.QueryService.MaintenanceCycleDays;
        if (day % economy.QueryService.MaintenanceCycleDays != 0 || round != economy.MaintenanceService.LastPaidRound + 1) return;
        if (!economy.MaintenanceService.TryPay(round, out _))
        {
            reasonText.text = "Insufficient funds - maintenance not paid.";
            return;
        }
        refreshTradingView();
    }

    /// <summary>거래가 부분 실패하면 재시도 없이 멈춰 중복 입금을 방지한다.</summary>
    /// <param name="exception">원본 오류.</param>
    private void fail(Exception exception)
    {
        faulted = true;
        Debug.LogException(exception, this);
        if (ready && tradingRoot != null) refreshTradingView();
    }

    /// <summary>화면이 생성한 표시 리소스만 정리한다.</summary>
    private void OnDestroy()
    {
        customerQueue?.Stop();
        if (squareSprite != null) Destroy(squareSprite);
        if (displayFont != null) Destroy(displayFont);
    }

    private void ensureValidEventSystem()
    {
        var existingEs = FindFirstObjectByType<EventSystem>();
        if (existingEs != null)
        {
            var legacyModule = existingEs.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                DestroyImmediate(legacyModule);
#if ENABLE_INPUT_SYSTEM
                existingEs.gameObject.AddComponent<InputSystemUIInputModule>();
#endif
            }
            existingEs.sendNavigationEvents = false;
        }
        else
        {
            var esGo = new GameObject("EventSystem");
            var es = esGo.AddComponent<EventSystem>();
            es.sendNavigationEvents = false;
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    private RectTransform rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x * S, -y * S);
        r.sizeDelta = new Vector2(width * S, height * S);
        return r;
    }

    private Image panel(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var r = this.rect(parent, name, x, y, width, height);
        var image = r.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI label(Transform parent, string name, string text, float x, float y, float width, float height, float fontSize, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var r = this.rect(parent, name, x, y, width, height);
        var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (displayFont != null) tmp.font = displayFont;
        tmp.text = text;
        tmp.fontSize = fontSize * S;
        tmp.color = color ?? new Color(0.91f, 0.93f, 0.91f);
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button makeButton(Transform parent, string name, string text, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, float fontSize, Color? btnColor = null)
    {
        var image = this.panel(parent, name, x, y, width, height, Color.white);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        Color baseCol = btnColor ?? new Color(0.19f, 0.25f, 0.28f);
        colors.normalColor = baseCol;
        colors.highlightedColor = baseCol * 1.25f;
        colors.pressedColor = baseCol * 0.75f;
        colors.disabledColor = new Color(0.15f, 0.18f, 0.20f, 0.5f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        if (action != null) button.onClick.AddListener(action);

        if (!string.IsNullOrEmpty(text))
        {
            this.label(image.transform, "Label", text, 2, 0, width - 4, height, fontSize, Color.white, TextAlignmentOptions.Center);
        }

        return button;
    }
}
