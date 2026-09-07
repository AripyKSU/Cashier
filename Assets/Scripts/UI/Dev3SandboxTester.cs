using System;
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

    [SerializeField] private CashierSettings settings = new CashierSettings();

    public CashierSession Session { get; private set; }
    public CashierSettings Settings => this.settings;

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
    private int drawnRevision = -1;
    private float alertUntil;

    private CashierCustomer laidOutCustomer;
    private int basketLayoutSeed;
    private readonly List<GameObject> activeBasketCards = new List<GameObject>();


    // =========================================================================
    // 3. UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        this.ensureValidEventSystem();

        // Load existing save data or default
        this.currentSaveData = CashierSaveManager.Load();

        this.buildAllScreens();
        this.setViewState(GameViewState.Title);
    }

    private void Update()
    {
        if (this.currentViewState == GameViewState.Trading)
        {
            this.handleTradingInput();

            if (this.Session != null)
            {
                this.Session.Tick(Time.unscaledDeltaTime);

                if (this.drawnRevision != this.Session.Revision)
                {
                    this.refreshTradingView();
                }

                // Real-time Queue Gauge Update
                if (this.gaugeFill != null)
                {
                    float fillRatio = Mathf.Clamp01(this.Session.Gauge / 100f);
                    this.gaugeFill.rectTransform.sizeDelta = new Vector2(300f * S * fillRatio, 8f * S);
                    this.gaugeFill.color = this.Session.Gauge >= this.settings.greenThreshold ? new Color(0.54f, 0.72f, 0.65f) :
                                           this.Session.Gauge > 30f ? new Color(0.78f, 0.64f, 0.38f) :
                                           new Color(0.85f, 0.35f, 0.30f);
                }

                if (this.gaugeText != null)
                {
                    this.gaugeText.text = $"QUEUE  {this.Session.Gauge:0}%";
                }

                if (this.Session.Phase == CashierPhase.Trading && Time.unscaledTime > this.alertUntil && this.reasonText != null)
                {
                    this.reasonText.text = "Num keys: Amount | Enter: Confirm | Backspace: Erase | Esc: Pause";
                }
            }
        }
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

    private void onNewGameClicked()
    {
        this.currentSaveData = GameSaveData.CreateNewGame();
        CashierSaveManager.Save(this.currentSaveData);
        this.setViewState(GameViewState.Tutorial);
    }

    private void onContinueClicked()
    {
        this.currentSaveData = CashierSaveManager.Load();
        this.setViewState(GameViewState.MainHub);
    }

    private void onTutorialNextClicked()
    {
        this.setViewState(GameViewState.MainHub);
    }

    private void launchTradingSession()
    {
        // Start or resume session with current save day & cash
        this.Session = new CashierSession(this.settings, Environment.TickCount);
        this.amount = "";
        this.invalidAmount = false;
        this.drawnRevision = -1;

        this.setViewState(GameViewState.Trading);
        this.refreshTradingView();
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
        this.makeButton(this.titleRoot.transform, "BtnNewGame", "NEW GAME", 490, 360, 300, 52, this.onNewGameClicked, 22, new Color(0.22f, 0.55f, 0.38f));

        var continueBtn = this.makeButton(this.titleRoot.transform, "BtnContinue", "CONTINUE", 490, 430, 300, 52, this.onContinueClicked, 22, new Color(0.24f, 0.42f, 0.65f));
        continueBtn.interactable = CashierSaveManager.HasSave();

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

    private void refreshMainHubHeader()
    {
        if (this.currentSaveData == null) return;

        int daysLeft = 7 - ((this.currentSaveData.currentDay - 1) % 7);
        if (daysLeft == 7) daysLeft = 0;

        if (daysLeft == 0)
        {
            this.hubBusinessBtnSubtext.text = "<color=#FF4444>[ TODAY: TRIBUTE INSPECTION ]</color>";
        }
        else
        {
            this.hubBusinessBtnSubtext.text = $"(Tribute in {daysLeft} Days)";
        }
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
            this.label(coverCard.transform, "BannerTxt", "SPECIAL STORE NOTICE: MANDATORY RATION DISTRIBUTION", 30, 25, 1080, 45, 20, Color.white, TextAlignmentOptions.Center);

            // Featured Product Highlight Box
            var featProduct = this.settings.products[0];
            var featBox = this.panel(coverCard.transform, "FeatBox", 380, 100, 380, 320, new Color(0.94f, 0.96f, 0.98f));
            this.panel(featBox.transform, "BoxBorder", 0, 0, 380, 320, new Color(0.70f, 0.75f, 0.82f));

            if (featProduct.sprite != null)
            {
                var img = this.panel(featBox.transform, "FeatImg", 20, 20, 340, 180, Color.white);
                img.sprite = featProduct.sprite;
                img.preserveAspect = true;
            }
            else
            {
                this.label(featBox.transform, "FeatIcon", "[ESSENTIAL RATION]", 20, 60, 340, 40, 24, new Color(0.40f, 0.55f, 0.70f), TextAlignmentOptions.Center);
            }

            this.label(featBox.transform, "FeatName", featProduct.name, 20, 215, 340, 35, 22, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.Center);
            this.label(featBox.transform, "FeatPrice", $"{featProduct.price:N0} G", 20, 255, 340, 40, 26, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.Center);

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
            for (int i = 0; i < 3 && i < this.settings.products.Length; i++)
            {
                var prod = this.settings.products[i];
                float y = 55 + i * 115;
                var slot = this.panel(spreadCard.transform, $"Slot_{i}", 40, y, 480, 100, new Color(0.95f, 0.97f, 0.99f));
                this.panel(slot.transform, "SlotBorder", 0, 0, 480, 100, new Color(0.80f, 0.84f, 0.88f));

                if (prod.sprite != null)
                {
                    var img = this.panel(slot.transform, "Img", 10, 10, 100, 80, Color.white);
                    img.sprite = prod.sprite;
                    img.preserveAspect = true;
                }
                else
                {
                    this.label(slot.transform, "Token", "[ITEM]", 10, 30, 100, 35, 16, new Color(0.45f, 0.55f, 0.68f), TextAlignmentOptions.Center);
                }

                this.label(slot.transform, "Name", prod.name, 130, 20, 240, 30, 18, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Tag", $"Available Day {prod.firstDay}", 130, 52, 240, 25, 13, new Color(0.55f, 0.60f, 0.68f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Price", $"{prod.price:N0} G", 370, 32, 90, 35, 20, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.MidlineRight);
            }

            // Right Page (Products 3, 4, 5)
            this.label(spreadCard.transform, "RightPageHeader", "MEDICAL & UTILITY", 610, 15, 500, 30, 16, new Color(0.30f, 0.35f, 0.45f), TextAlignmentOptions.Center);
            for (int i = 3; i < 6 && i < this.settings.products.Length; i++)
            {
                var prod = this.settings.products[i];
                float y = 55 + (i - 3) * 115;
                var slot = this.panel(spreadCard.transform, $"Slot_{i}", 620, y, 480, 100, new Color(0.95f, 0.97f, 0.99f));
                this.panel(slot.transform, "SlotBorder", 0, 0, 480, 100, new Color(0.80f, 0.84f, 0.88f));

                if (prod.sprite != null)
                {
                    var img = this.panel(slot.transform, "Img", 10, 10, 100, 80, Color.white);
                    img.sprite = prod.sprite;
                    img.preserveAspect = true;
                }
                else
                {
                    this.label(slot.transform, "Token", "[ITEM]", 10, 30, 100, 35, 16, new Color(0.45f, 0.55f, 0.68f), TextAlignmentOptions.Center);
                }

                this.label(slot.transform, "Name", prod.name, 130, 20, 240, 30, 18, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Tag", $"Available Day {prod.firstDay}", 130, 52, 240, 25, 13, new Color(0.55f, 0.60f, 0.68f), TextAlignmentOptions.MidlineLeft);
                this.label(slot.transform, "Price", $"{prod.price:N0} G", 370, 32, 90, 35, 20, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.MidlineRight);
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
            for (int i = 6; i < this.settings.products.Length && i < 10; i++)
            {
                var prod = this.settings.products[i];
                float colX = 50 + (i - 6) * 265;
                var slot = this.panel(p4Card.transform, $"Slot_{i}", colX, 65, 245, 180, new Color(0.95f, 0.97f, 0.99f));
                this.panel(slot.transform, "SlotBorder", 0, 0, 245, 180, new Color(0.80f, 0.84f, 0.88f));

                if (prod.sprite != null)
                {
                    var img = this.panel(slot.transform, "Img", 10, 10, 225, 90, Color.white);
                    img.sprite = prod.sprite;
                    img.preserveAspect = true;
                }
                else
                {
                    this.label(slot.transform, "Token", "[SPECIAL]", 10, 35, 225, 30, 16, new Color(0.45f, 0.55f, 0.68f), TextAlignmentOptions.Center);
                }

                this.label(slot.transform, "Name", prod.name, 10, 110, 225, 28, 16, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.Center);
                this.label(slot.transform, "Price", $"{prod.price:N0} G", 10, 140, 225, 30, 19, new Color(0.85f, 0.55f, 0.10f), TextAlignmentOptions.Center);
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

    private void renderLedgerNotebook()
    {
        this.label(this.hubContentContainer, "LedgerTitle", "ACCOUNTING LEDGER & DAILY SETTLEMENT HISTORY", 40, 18, 1140, 32, 22, new Color(0.15f, 0.20f, 0.28f), TextAlignmentOptions.MidlineLeft);

        var ledgerFrame = this.panel(this.hubContentContainer, "LedgerPaper", 40, 58, 1140, 500, Color.white);
        this.panel(ledgerFrame.transform, "Border", 0, 0, 1140, 500, new Color(0.75f, 0.80f, 0.86f));

        // Spreadsheet Column Header Row
        var head = this.panel(ledgerFrame.transform, "HeaderRow", 20, 15, 1100, 40, new Color(0.16f, 0.22f, 0.28f));
        this.label(head.transform, "Col1", "DATE", 15, 8, 120, 24, 15, Color.white, TextAlignmentOptions.MidlineLeft);
        this.label(head.transform, "Col2", "MAINTENANCE", 150, 8, 160, 24, 15, Color.white, TextAlignmentOptions.MidlineLeft);
        this.label(head.transform, "Col3", "UPGRADE EXP", 320, 8, 160, 24, 15, Color.white, TextAlignmentOptions.MidlineLeft);
        this.label(head.transform, "Col4", "DAILY REVENUE", 490, 8, 170, 24, 15, Color.white, TextAlignmentOptions.MidlineLeft);
        this.label(head.transform, "Col5", "NET CASH", 680, 8, 180, 24, 15, Color.white, TextAlignmentOptions.MidlineLeft);
        this.label(head.transform, "Col6", "REPUTATION", 880, 8, 200, 24, 15, Color.white, TextAlignmentOptions.MidlineLeft);

        // Historical Rows (Shows up to 8 recent historical records with clean table layout)
        var history = this.currentSaveData.ledgerHistory;
        int displayCount = Mathf.Min(8, history.Count);

        if (displayCount == 0)
        {
            this.label(ledgerFrame.transform, "EmptyMsg", "No settled business days recorded yet. Press [START BUSINESS] to begin Day 01.", 40, 180, 1060, 40, 17, new Color(0.55f, 0.60f, 0.68f), TextAlignmentOptions.Center);
        }
        else
        {
            for (int i = 0; i < displayCount; i++)
            {
                var rec = history[history.Count - 1 - i]; // Recent first
                float rowY = 62 + i * 44;
                Color rowBg = i % 2 == 0 ? new Color(0.96f, 0.98f, 1f) : Color.white;

                var row = this.panel(ledgerFrame.transform, $"Row_{i}", 20, rowY, 1100, 40, rowBg);
                this.panel(row.transform, "RowLine", 0, 39, 1100, 1, new Color(0.85f, 0.88f, 0.92f));

                this.label(row.transform, "Val1", $"Day {rec.day:00}", 15, 8, 120, 24, 15, new Color(0.15f, 0.20f, 0.28f), TextAlignmentOptions.MidlineLeft);
                this.label(row.transform, "Val2", $"-{rec.maintenanceFee:N0} G", 150, 8, 160, 24, 15, new Color(0.75f, 0.25f, 0.25f), TextAlignmentOptions.MidlineLeft);
                this.label(row.transform, "Val3", rec.upgradeExpense > 0 ? $"-{rec.upgradeExpense:N0} G" : "0 G", 320, 8, 160, 24, 15, new Color(0.50f, 0.55f, 0.62f), TextAlignmentOptions.MidlineLeft);
                this.label(row.transform, "Val4", $"+{rec.revenue:N0} G", 490, 8, 170, 24, 15, new Color(0.20f, 0.60f, 0.35f), TextAlignmentOptions.MidlineLeft);
                this.label(row.transform, "Val5", $"{rec.netCash:N0} G", 680, 8, 180, 24, 15, new Color(0.12f, 0.16f, 0.22f), TextAlignmentOptions.MidlineLeft);
                this.label(row.transform, "Val6", $"{rec.reputationChange:+0;-0;0} (Total {rec.netReputation})", 880, 8, 200, 24, 15, new Color(0.24f, 0.45f, 0.65f), TextAlignmentOptions.MidlineLeft);
            }
        }

        // Official Signature & Ledger Stamp Section (Notes sketch: "날짜, 서명, 도장")
        var stampArea = this.panel(ledgerFrame.transform, "StampArea", 20, 425, 1100, 60, new Color(0.94f, 0.96f, 0.98f));
        this.panel(stampArea.transform, "SBorder", 0, 0, 1100, 60, new Color(0.80f, 0.84f, 0.90f));

        this.label(stampArea.transform, "SigLabel", $"Official District Audit: Day {this.currentSaveData.currentDay:00} | Cashier Identity Verified", 25, 18, 600, 26, 15, new Color(0.25f, 0.32f, 0.42f));
        this.label(stampArea.transform, "StampSeal", "[ CERTIFIED OFFICIAL ACCOUNTING RECORD ]", 650, 16, 420, 28, 15, new Color(0.80f, 0.28f, 0.25f), TextAlignmentOptions.MidlineRight);
    }


    // =========================================================================
    // 11. TAB 3: BUILDING UPGRADE VIEW (Isometric Tier Progression)
    // =========================================================================

    private void renderBuildingUpgrade()
    {
        this.label(this.hubContentContainer, "UpTitle", "STALL FACILITY & TIER UPGRADE", 40, 18, 1140, 32, 22, new Color(0.15f, 0.20f, 0.28f), TextAlignmentOptions.MidlineLeft);

        var upFrame = this.panel(this.hubContentContainer, "UpPaper", 40, 58, 1140, 500, Color.white);
        this.panel(upFrame.transform, "Border", 0, 0, 1140, 500, new Color(0.75f, 0.80f, 0.86f));

        this.label(upFrame.transform, "CurrentCash", $"Available Treasury: <color=#D48800><b>{this.currentSaveData.cash:N0} G</b></color>", 40, 20, 600, 30, 20, new Color(0.15f, 0.20f, 0.28f));

        // 3 Tiers Side by Side
        int currentTier = this.currentSaveData.buildingTier;

        // TIER 1
        this.renderTierCard(upFrame.transform, 1, "Tier 1: Temporary Stall", "Capacity: 8 Customers\nQueue Decay: 2.0/s\nBasic Wooden Barrier", 0, currentTier >= 1, 40, 75, 335, 380);

        // TIER 2
        this.renderTierCard(upFrame.transform, 2, "Tier 2: Reinforced Stand", "Capacity: 10 Customers\nQueue Decay: 1.6/s (-20%)\nMetal Awning & Sound Insulation", 15000, currentTier >= 2, 400, 75, 335, 380);

        // TIER 3
        this.renderTierCard(upFrame.transform, 3, "Tier 3: Modern Kiosk", "Capacity: 12 Customers\nQueue Decay: 1.2/s (-40%)\nProtected District License", 40000, currentTier >= 3, 760, 75, 335, 380);
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

    private void purchaseUpgrade(int nextTier, int cost)
    {
        if (this.currentSaveData.cash >= cost)
        {
            this.currentSaveData.cash -= cost;
            this.currentSaveData.buildingTier = nextTier;

            // Log into ledger expenses
            this.currentSaveData.ledgerHistory.Add(new LedgerRecord(
                this.currentSaveData.currentDay,
                0,
                cost,
                0,
                this.currentSaveData.cash,
                0,
                this.currentSaveData.reputation
            ));

            CashierSaveManager.Save(this.currentSaveData);
            this.renderBuildingUpgrade();
        }
    }


    // =========================================================================
    // 12. TAB 4: OPTIONS SETTINGS VIEW
    // =========================================================================

    private void renderOptionsSettings()
    {
        this.label(this.hubContentContainer, "OptTitle", "GAME & AUDIO SETTINGS", 40, 18, 1140, 32, 22, new Color(0.15f, 0.20f, 0.28f), TextAlignmentOptions.MidlineLeft);

        var optFrame = this.panel(this.hubContentContainer, "OptPaper", 320, 80, 580, 440, Color.white);
        this.panel(optFrame.transform, "Border", 0, 0, 580, 440, new Color(0.75f, 0.80f, 0.86f));

        // BGM Control
        this.label(optFrame.transform, "BGMLabel", "BGM Volume", 40, 40, 200, 30, 18, new Color(0.15f, 0.20f, 0.28f));
        int bgmPct = Mathf.RoundToInt(this.currentSaveData.bgmVolume * 100f);
        var bgmText = this.label(optFrame.transform, "BGMVal", $"{bgmPct}%", 240, 40, 100, 30, 18, new Color(0.24f, 0.45f, 0.65f), TextAlignmentOptions.Center);

        this.makeButton(optFrame.transform, "BGMDown", "-", 360, 38, 48, 36, () => {
            this.currentSaveData.bgmVolume = Mathf.Clamp01(this.currentSaveData.bgmVolume - 0.1f);
            CashierSaveManager.Save(this.currentSaveData);
            bgmText.text = $"{Mathf.RoundToInt(this.currentSaveData.bgmVolume * 100f)}%";
        }, 20);

        this.makeButton(optFrame.transform, "BGMUp", "+", 420, 38, 48, 36, () => {
            this.currentSaveData.bgmVolume = Mathf.Clamp01(this.currentSaveData.bgmVolume + 0.1f);
            CashierSaveManager.Save(this.currentSaveData);
            bgmText.text = $"{Mathf.RoundToInt(this.currentSaveData.bgmVolume * 100f)}%";
        }, 20);

        // SFX Control
        this.label(optFrame.transform, "SFXLabel", "SFX Volume", 40, 120, 200, 30, 18, new Color(0.15f, 0.20f, 0.28f));
        int sfxPct = Mathf.RoundToInt(this.currentSaveData.sfxVolume * 100f);
        var sfxText = this.label(optFrame.transform, "SFXVal", $"{sfxPct}%", 240, 120, 100, 30, 18, new Color(0.24f, 0.45f, 0.65f), TextAlignmentOptions.Center);

        this.makeButton(optFrame.transform, "SFXDown", "-", 360, 118, 48, 36, () => {
            this.currentSaveData.sfxVolume = Mathf.Clamp01(this.currentSaveData.sfxVolume - 0.1f);
            CashierSaveManager.Save(this.currentSaveData);
            sfxText.text = $"{Mathf.RoundToInt(this.currentSaveData.sfxVolume * 100f)}%";
        }, 20);

        this.makeButton(optFrame.transform, "SFXUp", "+", 420, 118, 48, 36, () => {
            this.currentSaveData.sfxVolume = Mathf.Clamp01(this.currentSaveData.sfxVolume + 0.1f);
            CashierSaveManager.Save(this.currentSaveData);
            sfxText.text = $"{Mathf.RoundToInt(this.currentSaveData.sfxVolume * 100f)}%";
        }, 20);

        // Screen Resolution Spec
        this.label(optFrame.transform, "ResLabel", "Target Resolution", 40, 200, 200, 30, 18, new Color(0.15f, 0.20f, 0.28f));
        this.label(optFrame.transform, "ResVal", "1920 x 1080 (Full HD)", 240, 200, 280, 30, 18, new Color(0.40f, 0.45f, 0.55f));

        this.makeButton(optFrame.transform, "BtnDone", "SAVE & CLOSE", 190, 340, 200, 48, () => this.selectHubTab(HubTab.Flyer), 18, new Color(0.22f, 0.55f, 0.38f));
    }


    // =========================================================================
    // 13. TAB 5: SAVE & QUIT CONFIRM MODAL
    // =========================================================================

    private void buildQuitConfirmModal()
    {
        this.quitConfirmModal = this.panel(this.mainHubRoot.transform, "QuitModalDim", 0, 0, 1280, 720, new Color(0.05f, 0.07f, 0.09f, 0.85f)).gameObject;

        var box = this.panel(this.quitConfirmModal.transform, "Box", 380, 220, 520, 260, Color.white);
        this.panel(box.transform, "Border", 0, 0, 520, 260, new Color(0.70f, 0.75f, 0.82f));

        this.label(box.transform, "Prompt", "Do you want to save your progress\nand return to the Title Screen?", 30, 40, 460, 60, 20, new Color(0.15f, 0.20f, 0.28f), TextAlignmentOptions.Center);

        // Yes: Save & Return to Title
        this.makeButton(box.transform, "BtnYes", "YES (SAVE & EXIT)", 70, 150, 180, 48, () => {
            CashierSaveManager.Save(this.currentSaveData);
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
        this.goalHintText = this.label(this.tradingRoot.transform, "GoalHint", $"Safe Zone Pass: {this.settings.citizenshipPrice:N0} G", 655, 49, 265, 23, 14, new Color(0.70f, 0.75f, 0.80f), TextAlignmentOptions.MidlineLeft);

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

        if (this.Session != null && this.Session.Phase == CashierPhase.Trading && !this.Session.IsPaused)
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

        if (this.Session != null && this.Session.Phase == CashierPhase.Trading && !this.Session.IsPaused)
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
        if (this.Session == null || this.Session.Phase != CashierPhase.Trading || this.Session.IsPaused || this.invalidAmount) return;
        if (d.Length != 1 || d[0] < '0' || d[0] > '9' || this.amount.Length >= 7) return;
        if (this.amount == "0") this.amount = "";
        this.amount += d;
        this.alertUntil = 0;
        this.showAmount();
    }

    public void doubleZero()
    {
        if (this.Session == null || this.Session.Phase != CashierPhase.Trading || this.Session.IsPaused || this.invalidAmount) return;
        if (string.IsNullOrEmpty(this.amount) || this.amount == "0") return;
        if (this.amount.Length + 2 > 7) return;
        this.amount += "00";
        this.alertUntil = 0;
        this.showAmount();
    }

    public void erase()
    {
        if (this.Session == null || this.Session.Phase != CashierPhase.Trading || this.Session.IsPaused) return;
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

    public void confirm()
    {
        if (this.invalidAmount || this.Session == null) return;

        if (this.Session.Confirm(this.amount))
        {
            this.amount = "";
            this.refreshTradingView();
        }
        else if (this.Session.Phase == CashierPhase.Trading && !this.Session.IsPaused)
        {
            if (this.reasonText != null)
            {
                this.reasonText.text = "Please enter an amount of 1 G or more.";
                this.alertUntil = Time.unscaledTime + 2f;
            }
        }
    }

    public void pause()
    {
        if (this.Session != null)
        {
            this.Session.TogglePause();
            this.refreshTradingView();
        }
    }

    private void showAmount()
    {
        if (this.inputText == null) return;
        this.inputText.fontSize = (this.invalidAmount ? 16f : 30f) * S;
        this.inputText.text = this.invalidAmount ? "Digits only - DEL to clear" :
                              string.IsNullOrEmpty(this.amount) ? "<color=#777777>0 G</color>" :
                              $"{int.Parse(this.amount):N0} G";
    }

    private void refreshTradingView()
    {
        if (this.Session == null) return;
        this.drawnRevision = this.Session.Revision;

        if (this.dayText != null) this.dayText.text = $"DAY {this.Session.Day:00} / OPERATING";
        if (this.cashText != null) this.cashText.text = $"{this.Session.Cash:N0} G";
        if (this.statusText != null) this.statusText.text = $"Rep: {this.Session.Reputation}   |   Morality: {this.Session.Morality}";
        if (this.queueText != null) this.queueText.text = $"Today's Visitors: {this.Session.Visitors} | Remaining: {this.Session.Remaining}";

        if (this.confirmButton != null)
        {
            this.confirmButton.interactable = this.Session.Phase == CashierPhase.Trading && !this.Session.IsPaused;
        }

        this.showAmount();

        if (this.portrait != null)
        {
            this.portrait.color = this.Session.Phase == CashierPhase.Result && !this.Session.LastAccepted ?
                                  new Color(0.60f, 0.25f, 0.25f) :
                                  new Color(0.16f, 0.20f, 0.26f, 0.95f);
        }

        for (int i = 0; i < this.waiting.Length; i++)
        {
            if (this.waiting[i] != null)
            {
                this.waiting[i].gameObject.SetActive(this.Session.Remaining > i + 1);
            }
        }

        if (this.dialogueText != null)
        {
            if (this.Session.Phase == CashierPhase.Result)
            {
                this.dialogueText.text = this.Session.Feedback;
            }
            else if (this.Session.Customer != null)
            {
                this.dialogueText.text = this.Session.Customer.isPoor ?
                    "<color=#FFAB91>\"My child is waiting at home... This is all the money I have.\"</color>" :
                    "\"Please ring these up. How much is it?\"";
            }
            else
            {
                this.dialogueText.text = "";
            }
        }

        if (this.reasonText != null)
        {
            if (this.Session.Phase == CashierPhase.Result || this.Session.Departed > 0)
            {
                this.reasonText.text = this.Session.Reason;
                this.alertUntil = Time.unscaledTime + 3f;
            }
        }

        this.renderBasket();
        this.renderTradingModal();
    }

    private void renderBasket()
    {
        foreach (var card in this.activeBasketCards)
        {
            if (card != null) Destroy(card);
        }
        this.activeBasketCards.Clear();

        if (this.basketRoot == null || this.Session.Customer == null || this.Session.Phase == CashierPhase.PriceGuide) return;

        var units = new List<(CashierProduct product, int unitIndex, int totalOfSame)>();
        foreach (var line in this.Session.Customer.basket)
        {
            for (int u = 0; u < line.quantity; u++)
            {
                units.Add((line.product, u, line.quantity));
            }
        }

        int totalUnits = units.Count;
        if (totalUnits == 0) return;

        if (totalUnits <= 5)
        {
            float itemWidth = totalUnits <= 3 ? 105f : totalUnits == 4 ? 96f : 88f;
            float itemHeight = 110f;
            float spacing = Mathf.Min(24f, (615f - (totalUnits * itemWidth)) / Mathf.Max(1, totalUnits + 1));
            float totalWidth = totalUnits * itemWidth + (totalUnits - 1) * spacing;
            float startX = (635f - totalWidth) / 2f;
            float y = 14f;

            for (int i = 0; i < totalUnits; i++)
            {
                float x = startX + i * (itemWidth + spacing);
                this.renderIndividualItemUnit(units[i].product, units[i].unitIndex, units[i].totalOfSame, i, x, y, itemWidth, itemHeight, false);
            }
        }
        else
        {
            int r1Count = (totalUnits + 1) / 2;
            int r2Count = totalUnits - r1Count;
            float itemWidth = 88f;
            float itemHeight = 56f;
            float spacing1 = (615f - r1Count * itemWidth) / Mathf.Max(1, r1Count + 1);
            float spacing2 = (615f - r2Count * itemWidth) / Mathf.Max(1, r2Count + 1);

            float totalW1 = r1Count * itemWidth + (r1Count - 1) * spacing1;
            float startX1 = (635f - totalW1) / 2f;
            for (int i = 0; i < r1Count; i++)
            {
                float x = startX1 + i * (itemWidth + spacing1);
                this.renderIndividualItemUnit(units[i].product, units[i].unitIndex, units[i].totalOfSame, i, x, 8f, itemWidth, itemHeight, true);
            }

            float totalW2 = r2Count * itemWidth + (r2Count - 1) * spacing2;
            float startX2 = (635f - totalW2) / 2f;
            for (int i = 0; i < r2Count; i++)
            {
                int idx = r1Count + i;
                float x = startX2 + i * (itemWidth + spacing2);
                this.renderIndividualItemUnit(units[idx].product, units[idx].unitIndex, units[idx].totalOfSame, idx, x, 72f, itemWidth, itemHeight, true);
            }
        }
    }

    private void renderIndividualItemUnit(CashierProduct product, int unitIndex, int totalOfSame, int globalSlot, float x, float y, float width, float height, bool isCompact)
    {
        var unitPanel = this.panel(this.basketRoot, $"Unit_{globalSlot}_{product.name}", x, y, width, height, new Color(0.12f, 0.16f, 0.22f, 0.95f));
        this.panel(unitPanel.transform, "Border", 0, 0, width, height, new Color(0.28f, 0.35f, 0.44f, 0.85f));

        if (product.sprite != null)
        {
            float imgHeight = isCompact ? height - 18f : height - 26f;
            var img = this.panel(unitPanel.transform, "Sprite", 3, 3, width - 6, imgHeight, Color.white);
            img.sprite = product.sprite;
            img.preserveAspect = true;

            string tagText = totalOfSame > 1 ? $"{product.name} #{unitIndex + 1}" : product.name;
            this.label(unitPanel.transform, "Label", tagText, 2, height - (isCompact ? 16f : 22f), width - 4, 16, isCompact ? 10 : 12, new Color(0.85f, 0.90f, 0.95f), TextAlignmentOptions.Center);
        }
        else
        {
            if (isCompact)
            {
                this.label(unitPanel.transform, "Name", product.name, 2, 6, width - 4, 22, 13, Color.white, TextAlignmentOptions.Center);
                string tag = totalOfSame > 1 ? $"#{unitIndex + 1}" : "[ITEM]";
                this.label(unitPanel.transform, "UnitTag", tag, 2, 30, width - 4, 20, 12, new Color(0.40f, 0.85f, 0.55f), TextAlignmentOptions.Center);
            }
            else
            {
                this.label(unitPanel.transform, "Icon", "[ITEM]", 2, 12, width - 4, 26, 15, new Color(0.60f, 0.75f, 0.90f), TextAlignmentOptions.Center);
                this.label(unitPanel.transform, "Name", product.name, 2, 42, width - 4, 32, 14, Color.white, TextAlignmentOptions.Center);
                string tag = totalOfSame > 1 ? $"Item #{unitIndex + 1}" : "x 1";
                this.label(unitPanel.transform, "UnitTag", tag, 2, 76, width - 4, 20, 12, new Color(0.40f, 0.85f, 0.55f), TextAlignmentOptions.Center);
            }
        }

        this.activeBasketCards.Add(unitPanel.gameObject);
    }

    private void renderTradingModal()
    {
        if (this.tradingModal != null)
        {
            this.tradingModal.gameObject.SetActive(false);
            Destroy(this.tradingModal.gameObject);
            this.tradingModal = null;
        }

        if (this.Session.IsPaused)
        {
            this.createTradingModalWindow("PAUSED", "Time and queue are frozen.");
            this.makeButton(this.tradingModal, "Resume", "RESUME", 190, 320, 360, 48, this.pause, 21, new Color(0.22f, 0.45f, 0.65f));
            this.makeButton(this.tradingModal, "Restart", "RESTART", 190, 380, 360, 44, this.restartTradingDay, 18, new Color(0.50f, 0.25f, 0.25f));
            return;
        }

        switch (this.Session.Phase)
        {
            case CashierPhase.PriceGuide:
                this.createTradingModalWindow(
                    this.Session.Day == 1 ? "BEFORE BUSINESS: MEMORIZE THE PRICES" : "NEW PRODUCTS INTRODUCED",
                    "Once business begins, the price list cannot be viewed again."
                );

                var dayProducts = new List<CashierProduct>();
                foreach (var product in this.settings.products)
                {
                    if (product.firstDay == this.Session.Day)
                    {
                        dayProducts.Add(product);
                    }
                }

                int prodCount = dayProducts.Count;
                if (prodCount <= 4)
                {
                    float cardWidth = prodCount == 1 ? 190f : prodCount == 2 ? 175f : 155f;
                    float cardHeight = 160f;
                    float spacing = prodCount == 1 ? 0f : prodCount == 2 ? 35f : 20f;
                    float totalRowWidth = prodCount * cardWidth + (prodCount - 1) * spacing;
                    float startX = (740f - totalRowWidth) / 2f;

                    for (int i = 0; i < prodCount; i++)
                    {
                        var product = dayProducts[i];
                        float x = startX + i * (cardWidth + spacing);
                        this.renderProductGuideCard(this.tradingModal, product, i, x, 140f, cardWidth, cardHeight, false);
                    }
                }
                else
                {
                    int r1Count = (prodCount + 1) / 2;
                    int r2Count = prodCount - r1Count;
                    float cardWidth = 140f;
                    float cardHeight = 110f;
                    float spacing = 16f;

                    float totalRow1 = r1Count * cardWidth + (r1Count - 1) * spacing;
                    float startX1 = (740f - totalRow1) / 2f;
                    for (int i = 0; i < r1Count; i++)
                    {
                        var product = dayProducts[i];
                        float x = startX1 + i * (cardWidth + spacing);
                        this.renderProductGuideCard(this.tradingModal, product, i, x, 125f, cardWidth, cardHeight, true);
                    }

                    float totalRow2 = r2Count * cardWidth + (r2Count - 1) * spacing;
                    float startX2 = (740f - totalRow2) / 2f;
                    for (int i = 0; i < r2Count; i++)
                    {
                        var product = dayProducts[r1Count + i];
                        float x = startX2 + i * (cardWidth + spacing);
                        this.renderProductGuideCard(this.tradingModal, product, r1Count + i, x, 245f, cardWidth, cardHeight, true);
                    }
                }

                this.label(this.tradingModal, "Narrative",
                    $"In {this.Session.DaysUntilTribute} days, the Inspector will collect the tribute.\nGather enough cash to reach the Safe Zone with your daughter.",
                    35, 366, 670, 56, 17, new Color(0.85f, 0.88f, 0.85f), TextAlignmentOptions.TopLeft);

                this.makeButton(this.tradingModal, "OpenShop", "I MEMORIZED - OPEN STORE", 170, 435, 400, 46, () => { this.Session.OpenShop(); this.refreshTradingView(); }, 20, new Color(0.20f, 0.55f, 0.35f));
                break;

            case CashierPhase.Tribute:
                this.createTradingModalWindow(
                    "INSPECTOR'S VISIT",
                    "\"Stall fee, protection tax, my cut. You haven't forgotten, have you?\""
                );

                this.label(this.tradingModal, "TributeInfo",
                    $"Due Tribute Today: <color=#FF5533>{this.Session.TributeAmount:N0} G</color>\nCurrent Cash: <b>{this.Session.Cash:N0} G</b>\n\nCitizenship pass can be purchased after tribute is paid.",
                    40, 200, 650, 100, 21, new Color(0.9f, 0.9f, 0.9f));

                this.makeButton(this.tradingModal, "PayTribute", "PAY TRIBUTE", 80, 420, 400, 48, () => { this.Session.PayTribute(); this.refreshTradingView(); }, 21, new Color(0.70f, 0.28f, 0.28f));
                break;

            case CashierPhase.Settlement:
                this.createTradingModalWindow(
                    $"DAY {this.Session.Day:00} SETTLEMENT",
                    $"Revenue: +{this.Session.Revenue:N0} G    |    Cash Balance: {this.Session.Cash:N0} G"
                );

                this.label(this.tradingModal, "Summary",
                    $"Successful Sales: <b>{this.Session.Sold}</b>    Refused: <b>{this.Session.Refused}</b>    Departed: <b>{this.Session.Departed}</b>\n\n" +
                    $"Reputation: <b>{this.Session.ReputationChange:+0;-0;0}</b>      Morality: <b>{this.Session.MoralityChange:+0;-0;0}</b>\n\n" +
                    $"Next tribute inspection in <b>{this.Session.DaysUntilTribute} days</b>\n" +
                    $"Remaining to Citizenship Pass: <b>{Math.Max(0, this.settings.citizenshipPrice - this.Session.Cash):N0} G</b>",
                    40, 160, 650, 210, 22, new Color(0.90f, 0.93f, 0.90f));

                // NEXT DAY -> Commit record to Ledger & Return to Main Hub!
                this.makeButton(this.tradingModal, "NextDay", "COMPLETE DAY >", 390, 420, 300, 48, this.completeTradingDayAndReturnToHub, 20, new Color(0.20f, 0.55f, 0.35f));

                var buyBtn = this.makeButton(this.tradingModal, "BuyCitizenship", "BUY CITIZENSHIP", 40, 420, 320, 48, () => { this.Session.BuyCitizenship(); this.refreshTradingView(); }, 20, new Color(0.25f, 0.45f, 0.75f));
                buyBtn.interactable = this.Session.CanBuy;
                break;

            case CashierPhase.Goal:
                this.createTradingModalWindow(
                    "TO THE SAFE ZONE",
                    "\"Dad, can we finally sleep in a warm bed now?\""
                );

                this.label(this.tradingModal, "Ending",
                    "You have obtained the Citizenship Pass.\n\nYour fair pricing decisions and endurance brought you here.\n\nRemembering the reputation, morality, and those left behind.",
                    50, 185, 640, 195, 22, new Color(0.90f, 0.93f, 0.90f));

                this.makeButton(this.tradingModal, "RestartGoal", "START NEW JOURNEY", 170, 420, 400, 48, this.onNewGameClicked, 20, new Color(0.20f, 0.55f, 0.35f));
                break;

            case CashierPhase.Failed:
                this.createTradingModalWindow(
                    "THE STALL HAS CLOSED",
                    "\"No tribute? Then clear out of here.\""
                );

                this.label(this.tradingModal, "Failure",
                    $"Required Tribute: <color=#FF5533>{this.Session.TributeAmount:N0} G</color>\nYour Cash: {this.Session.Cash:N0} G\n\nThe stall was seized by district enforcers.\nReview your prices and try again.",
                    40, 190, 470, 170, 21, new Color(0.95f, 0.75f, 0.75f));

                this.makeButton(this.tradingModal, "RestartFail", "TRY AGAIN", 80, 420, 400, 48, this.onNewGameClicked, 20, new Color(0.65f, 0.25f, 0.25f));
                break;
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

    private void renderProductGuideCard(Transform parent, CashierProduct product, int index, float x, float y, float width, float height, bool isCompact)
    {
        var card = this.panel(parent, $"GuideCard_{index}", x, y, width, height, new Color(0.13f, 0.17f, 0.22f, 1f));
        this.panel(card.transform, "CardBorder", 0, 0, width, height, new Color(0.28f, 0.35f, 0.44f, 0.85f));

        if (product.sprite != null)
        {
            float imgH = isCompact ? height - 44f : height - 54f;
            var img = this.panel(card.transform, "Sprite", 4, 4, width - 8, imgH, Color.white);
            img.sprite = product.sprite;
            img.preserveAspect = true;
        }
        else
        {
            this.label(card.transform, "Icon", "[ITEM]", 2, isCompact ? 10 : 20, width - 4, 26, isCompact ? 14 : 18, new Color(0.65f, 0.75f, 0.85f), TextAlignmentOptions.Center);
        }

        this.label(card.transform, "Name", product.name, 4, height - (isCompact ? 40f : 50f), width - 8, 20, isCompact ? 14 : 16, Color.white, TextAlignmentOptions.Center);
        this.label(card.transform, "Price", $"{product.price:N0} G", 4, height - (isCompact ? 22f : 26f), width - 8, 22, isCompact ? 16 : 19, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center);
    }

    private void restartTradingDay()
    {
        this.Session = new CashierSession(this.settings, Environment.TickCount);
        this.amount = "";
        this.invalidAmount = false;
        this.drawnRevision = -1;
        this.refreshTradingView();
    }

    private void completeTradingDayAndReturnToHub()
    {
        // 1. Commit day record to ledger history
        int maintenance = 500;
        int revenue = this.Session.Revenue;
        int netCash = this.Session.Cash;
        int repChange = this.Session.ReputationChange;
        int netRep = this.Session.Reputation;

        this.currentSaveData.ledgerHistory.Add(new LedgerRecord(
            this.Session.Day,
            maintenance,
            0,
            revenue,
            netCash,
            repChange,
            netRep
        ));

        // 2. Increment save state day & balances
        this.currentSaveData.currentDay = this.Session.Day + 1;
        this.currentSaveData.cash = netCash;
        this.currentSaveData.reputation = netRep;
        this.currentSaveData.morality = this.Session.Morality;

        CashierSaveManager.Save(this.currentSaveData);

        // 3. Return to Main Hub
        this.setViewState(GameViewState.MainHub);
    }


    // =========================================================================
    // 15. EVENT SYSTEM & UTILITY HELPERS
    // =========================================================================

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
