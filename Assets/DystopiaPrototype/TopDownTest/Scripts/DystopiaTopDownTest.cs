using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>탑다운 분류 테스트에서 개별 물품의 중복 없는 상태를 나타냅니다.</summary>
public enum TopDownItemState { Working, Excluded, ForSale }

/// <summary>실제 장바구니 한 단위와 Rigidbody2D를 연결하는 개별 물품입니다.</summary>
public sealed class DystopiaTopDownItem : MonoBehaviour
{
    public int InstanceId { get; private set; }
    public int ProductId { get; private set; }
    public int LineIndex { get; private set; }
    public int UnitIndex { get; private set; }
    public TopDownItemState State { get; set; }
    public bool WasStirred { get; set; }
    public Rigidbody2D Body { get; private set; }

    /// <summary>한 물품 인스턴스를 기존 장바구니 단위와 연결합니다.</summary>
    internal void Initialize(int instanceId, int productId, int lineIndex, int unitIndex, Rigidbody2D body)
    {
        InstanceId = instanceId;
        ProductId = productId;
        LineIndex = lineIndex;
        UnitIndex = unitIndex;
        Body = body;
        State = TopDownItemState.Working;
    }

    /// <summary>회전하는 물품의 충돌을 받아 바깥으로 튕기며 회전도 전달받습니다.</summary>
    /// <param name="collision">물품 사이의 물리 접촉입니다.</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        var other = collision.gameObject.GetComponent<DystopiaTopDownItem>();
        if (State == TopDownItemState.Excluded || other == null || other.State == TopDownItemState.Excluded) return;
        float spin = other.Body.angularVelocity;
        if (Mathf.Abs(spin) < 90) return;
        Vector2 away = (Body.position - other.Body.position).normalized;
        Body.AddForce(away * Mathf.Min(Mathf.Abs(spin) / 360f, 1.5f), ForceMode2D.Impulse);
        Body.angularVelocity = Mathf.Clamp(Body.angularVelocity - spin * .65f, -720, 720);
        WasStirred |= other.WasStirred;
    }
}

/// <summary>기존 정면 거래와 경제 권위를 재사용하는 별도 탑다운 분류 테스트 화면입니다.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class DystopiaTopDownTest : MonoBehaviour
{
    private enum ViewState { Front, Transition, Pouring, Sorting, Locked, Closed }

    [Header("Flow seconds")]
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.35f;
    [SerializeField, Min(0.1f)] private float pourDuration = 1.2f;
    [SerializeField, Min(0f)] private float pourSpreadSeconds = 0.55f;
    [SerializeField, Min(0f)] private float frontArrivalSeconds = 2f;
    [SerializeField, Min(0f)] private float reactionSeconds = 0.9f;

    [Header("Planar item physics")]
    [SerializeField, Min(0.05f)] private float cursorRadius = 0.42f;
    [SerializeField, Min(0.01f)] private float cursorForce = 0.032f;
    [SerializeField, Min(0.1f)] private float maximumItemSpeed = 5.2f;
    [SerializeField, Min(0f)] private float itemFriction = 3.8f;
    [SerializeField, Min(0f)] private float rotationDamping = 4.5f;
    [SerializeField, Min(0f)] private float pourForce = 2.5f;

    [Header("Test business clock")]
    [SerializeField, Min(0.01f)] private float gameMinutesPerRealSecond = 2f;

    [Header("Isolated test data")]
    [SerializeField] private DystopiaSettings settings = new DystopiaSettings();
    [SerializeField] private Sprite frontBackground;
    [SerializeField] private Sprite frontCounter;
    [SerializeField] private Sprite workbench;
    [SerializeField] private Sprite frontContainerMale;
    [SerializeField] private Sprite frontContainerFemale;
    [SerializeField] private Sprite tiltedContainer;
    [SerializeField] private Sprite emptyContainer;
    [SerializeField] private Sprite[] productSprites = new Sprite[4];
    [SerializeField] private Sprite[] maleCustomers = new Sprite[24];
    [SerializeField] private Sprite[] femaleCustomers = new Sprite[16];
    [SerializeField] private Font uiFont;

    private static readonly Rect ExcludedZone = new Rect(-6.25f, -2.85f, 0.95f, 5.7f);
    // 오른쪽 트레이의 테두리를 포함한 전체 표시 영역에 대응합니다.
    private static readonly Rect SaleZone = new Rect(2.1f, -3.3f, 4.2f, 6.55f);
    private readonly List<DystopiaTopDownItem> items = new List<DystopiaTopDownItem>();
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[32];
    // 한 번의 스윕에서 같은 물품의 충격을 중복 적용하지 않습니다.
    private readonly HashSet<Rigidbody2D> currentCursorContacts = new HashSet<Rigidbody2D>();
    private static DystopiaScreen pendingHostScreen;
    private ViewState state;
    private DystopiaScreen hostScreen;
    private GameObject hostBasketRoot;
    // 착지 순간에만 표시하는 저해상도 먼지 조각이며 정면 UI와 수명을 공유합니다.
    private readonly Image[] landingDust = new Image[10];
    private bool isEmbeddedInFrontScene;
    private Camera worldCamera;
    private Transform itemRoot;
    private GameObject frontRoot;
    private GameObject workUiRoot;
    private GameObject workbenchObject;
    private Image customerImage;
    private Image frontContainerImage;
    private Image pouringContainerImage;
    private Text dialogueText;
    private Text clueText;
    private Text inputText;
    private Text noticeText;
    private Text clockText;
    // 사용자 UI 자산과 런타임에 잘라 쓰는 버튼 Sprite의 소유권입니다.
    [SerializeField] private Sprite calculatorArtwork, calculatorToggleArtwork, counterClockArtwork;
    private readonly List<Sprite> uiSlices = new List<Sprite>();
    private RectTransform clockRoot;
    private bool calculatorOpen = true;
    private float calculatorSlide;
    /// <summary>독립 테스트 Scene의 계산기와 토글 배치입니다. 정면 연결 시 DystopiaScreen 값을 사용합니다.</summary>
    [SerializeField] private Rect calculatorLayout = new Rect(900, 310, 360, 360);
    [SerializeField] private Rect calculatorToggleLayout = new Rect(1214, 659, 54, 58);
    /// <summary>독립 테스트의 정면 시계 위치와 크기이며 연결 모드에서는 DystopiaScreen 값을 사용합니다.</summary>
    [SerializeField] private Rect counterClockLayout = new Rect(1090, 380, 180, 180);
    private RectTransform calculatorToggleRect;
    // 날짜가 바뀌면 영업 시각을 09:00으로 초기화합니다.
    private int clockDay;

    private RectTransform keypadRect;
    private GameObject transitionBlock;
    private GameObject ownedEventSystem;
    private string amount = "";
    private bool isPaused;
    private bool hasCursorSample;
    private Vector2 previousCursorWorld;
    private float businessMinute = 9 * 60;
    private int nextInstanceId = 1;
    private Coroutine flowRoutine;

    public DystopiaSession Session { get; private set; }
    public IReadOnlyList<DystopiaTopDownItem> Items => items;
    public bool IsSorting => state == ViewState.Sorting;
    public bool IsClosed => state == ViewState.Closed;
    public float BusinessMinute => businessMinute;
    /// <summary>연결 모드에서 실제 UI 배치 값을 소유하는 정면 화면입니다.</summary>
    public DystopiaScreen LayoutOwner => hostScreen;
    public string EnteredAmount => amount;

    /// <summary>기존 정면 화면의 세션을 그대로 사용하도록 런타임 테스트 브리지를 붙입니다.</summary>
    public static void AttachToExistingScreen(DystopiaScreen screen)
    {
        if (screen == null || FindFirstObjectByType<DystopiaTopDownTest>() != null) return;
        pendingHostScreen = screen;
        var bridge = new GameObject("DystopiaTopDownRuntimeBridge");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bridge, screen.gameObject.scene);
        bridge.AddComponent<DystopiaTopDownTest>();
    }

    /// <summary>에디터 테스트 자산을 바인딩하고 독립 런을 구성합니다.</summary>
    private void Awake()
    {
        hostScreen = pendingHostScreen;
        pendingHostScreen = null;
        isEmbeddedInFrontScene = hostScreen != null;
        if (isEmbeddedInFrontScene)
        {
            settings = hostScreen.Settings;
            foreach (RectTransform child in hostScreen.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name != "Basket" || !child.parent.gameObject.activeInHierarchy) continue;
                hostBasketRoot = child.gameObject;
                hostBasketRoot.SetActive(false);
                break;
            }
        }
#if UNITY_EDITOR
        BindEditorAssets();
#endif
        if (!isEmbeddedInFrontScene) DisableOtherScenesDuringPlay();
        if (isEmbeddedInFrontScene)
        {
            calculatorArtwork = hostScreen.CalculatorArtwork;
            calculatorToggleArtwork = hostScreen.CalculatorToggleArtwork;
            counterClockArtwork = hostScreen.CounterClockArtwork;
        }
        BuildWorld();
        BuildUi();
        Session = isEmbeddedInFrontScene ? hostScreen.Session : CreateSessionWithUsefulBasket();
        flowRoutine = StartCoroutine(isEmbeddedInFrontScene ? WaitForExistingTrade() : CustomerFlow());
    }

    /// <summary>기존 가격 기억 화면이 끝나고 실제 거래가 시작될 때 철재통 연출을 시작합니다.</summary>
    private IEnumerator WaitForExistingTrade()
    {
        frontRoot.SetActive(false);
        while (Session.Phase == DystopiaPhase.PriceGuide) yield return null;
        if (Session.Phase != DystopiaPhase.Trading) yield break;
        flowRoutine = StartCoroutine(CustomerFlow());
    }

    /// <summary>기존 손님 생성기를 그대로 반복해 물리 검증에 충분한 단위 수를 가진 결정적 손님을 고릅니다.</summary>
    private DystopiaSession CreateSessionWithUsefulBasket()
    {
        for (int seed = 20260908; seed < 20261008; seed++)
        {
            var candidate = new DystopiaSession(settings, seed);
            int units = 0;
            foreach (DystopiaBasketLine line in candidate.Customer.basket) units += line.quantity;
            if (units < 4) continue;
            candidate.OpenShop();
            return candidate;
        }
        throw new InvalidOperationException("탑다운 물리 검증에 필요한 네 개 이상의 기존 장바구니 단위를 만들지 못했습니다.");
    }

    /// <summary>커서 이동, 평면 물리 제한, 분류와 영업시간을 실제 프레임에서 갱신합니다.</summary>
    private void Update()
    {
        // 16:9 작업대가 실제 화면을 채우도록 하여 넓은 창에서도 뒤쪽 Scene이 드러나지 않게 합니다.
        if (worldCamera != null)
            worldCamera.orthographicSize = Mathf.Min(3.6f, 6.4f / Mathf.Max(.01f, worldCamera.aspect));
        if (Session == null)
        {
            RestoreAfterReload();
            return;
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPaused(!isPaused);
        Rect layout = hostScreen != null ? hostScreen.CalculatorLayout : calculatorLayout;
        Rect toggleLayout = hostScreen != null ? hostScreen.CalculatorToggleLayout : calculatorToggleLayout;
        float targetSlide = calculatorOpen ? 0 : Mathf.Max(0, 1300 - layout.x);
        calculatorSlide = Mathf.MoveTowards(calculatorSlide, targetSlide, Time.unscaledDeltaTime * 1600);
        if (keypadRect != null)
        {
            keypadRect.anchoredPosition = new Vector2(layout.x + calculatorSlide, -layout.y);
            keypadRect.localScale = new Vector3(Mathf.Max(1, layout.width) / 360, Mathf.Max(1, layout.height) / 360, 1);
        }
        if (calculatorToggleRect != null)
        {
            calculatorToggleRect.anchoredPosition = new Vector2(toggleLayout.x, -toggleLayout.y);
            calculatorToggleRect.sizeDelta = new Vector2(Mathf.Max(1, toggleLayout.width), Mathf.Max(1, toggleLayout.height));
        }
        if (clockRoot != null)
        {
            Rect clockLayout = hostScreen != null ? hostScreen.CounterClockLayout : counterClockLayout;
            clockRoot.anchoredPosition = new Vector2(clockLayout.x, -clockLayout.y);
            clockRoot.localScale = new Vector3(Mathf.Max(1, clockLayout.width) / 180, Mathf.Max(1, clockLayout.height) / 180, 1);
            clockRoot.gameObject.SetActive(!workUiRoot.activeSelf && !transitionBlock.activeSelf);
        }
        if (clockDay != Session.Day) { clockDay = Session.Day; businessMinute = 9 * 60; }
        if (isPaused) return;
        float deltaSeconds = Time.unscaledDeltaTime;
        if (Session.Phase == DystopiaPhase.Trading && !Session.IsPaused)
            businessMinute = Mathf.Min(21 * 60, businessMinute + deltaSeconds * gameMinutesPerRealSecond);
        if (state == ViewState.Sorting)
        {
            Session.Tick(deltaSeconds);
            if (businessMinute >= 21 * 60) CloseBusiness();
            else
            {
                ProcessPhysicalCursor(deltaSeconds);
                ClampItemMotion();
                ClassifySettledItems();
            }
        }
        else ResetCursorSample();
        RefreshUi();
    }

    /// <summary>재컴파일 뒤 소실된 세션과 코루틴을 정면 화면의 새 세션에 다시 연결합니다.</summary>
    private void RestoreAfterReload()
    {
        StopAllCoroutines();
        // 이 컴포넌트가 만든 작업대·물품·UI만 제거합니다. Scene 자산은 수정하지 않습니다.
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        foreach (Sprite slice in uiSlices) if (slice != null) Destroy(slice);
        uiSlices.Clear();
        items.Clear();
        currentCursorContacts.Clear();
        hasCursorSample = false;
        isPaused = false;
        businessMinute = 9 * 60;
        amount = "";
        if (hostScreen == null)
            hostScreen = FindFirstObjectByType<DystopiaScreen>(FindObjectsInactive.Include);
        if (hostScreen != null)
        {
            hostScreen.RestoreAfterReload();
            hostScreen.gameObject.SetActive(true);
            hostScreen.enabled = true;
        }
        pendingHostScreen = hostScreen;
        Awake();
    }

    /// <summary>포커스 복귀 직후의 큰 커서 속도 표본을 버립니다.</summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        ResetCursorSample();
    }

    /// <summary>테스트 브리지가 제거돼도 기존 상품 표시를 숨긴 채 남기지 않습니다.</summary>
    private void OnDestroy()
    {
        if (hostBasketRoot != null) hostBasketRoot.SetActive(true);
        foreach (Sprite slice in uiSlices) if (slice != null) Destroy(slice);
    }

    /// <summary>에디터에서 테스트 전용 이미지와 기존 배경·손님·폰트를 연결합니다.</summary>
    private void BindEditorAssets()
    {
#if UNITY_EDITOR
        frontBackground = frontBackground != null ? frontBackground : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/FARBACKGROUND.png");
        frontCounter = frontCounter != null ? frontCounter : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/Counter.png");
        workbench = workbench != null ? workbench : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/TopDownTest/Art/TopDownWorkbench.png");
        frontContainerMale = frontContainerMale != null ? frontContainerMale : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerMale.png");
        frontContainerFemale = frontContainerFemale != null ? frontContainerFemale : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerFemale.png");
        tiltedContainer = tiltedContainer != null ? tiltedContainer : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/TopDownTest/Art/TopDownContainerTilted.png");
        emptyContainer = emptyContainer != null ? emptyContainer : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/TopDownTest/Art/TopDownContainerEmpty.png");
        string[] productNames = { "TopDownWater", "TopDownCrackers", "TopDownCan", "TopDownRiceRound" };
        if (productSprites == null || productSprites.Length != productNames.Length) productSprites = new Sprite[productNames.Length];
        for (int i = 0; i < productSprites.Length; i++)
            if (productSprites[i] == null) productSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/DystopiaPrototype/TopDownTest/Art/{productNames[i]}.png");
        for (int i = 0; i < maleCustomers.Length; i++)
            if (maleCustomers[i] == null) maleCustomers[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/DystopiaPrototype/Art/Customers/MaleCustomer_{i + 1:00}.png");
        for (int i = 0; i < femaleCustomers.Length; i++)
            if (femaleCustomers[i] == null) femaleCustomers[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/DystopiaPrototype/Art/Customers/FemaleCustomer_{i + 1:00}.png");
        uiFont = uiFont != null ? uiFont : UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/DystopiaPrototype/Art/Mulmaru.otf");
#endif
    }

    /// <summary>추가 로드된 테스트가 Play 중 기존 씬의 카메라와 입력을 실행하지 않게 합니다.</summary>
    private void DisableOtherScenesDuringPlay()
    {
        if (!Application.isPlaying) return;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene == gameObject.scene) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(false);
        }
    }

    /// <summary>직교 카메라, 작업대, 평면 경계와 물품 부모를 구성합니다.</summary>
    private void BuildWorld()
    {
        var cameraObject = new GameObject("TopDownCamera");
        cameraObject.transform.SetParent(transform, false);
        worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographic = true;
        worldCamera.orthographicSize = 3.6f;
        worldCamera.clearFlags = CameraClearFlags.SolidColor;
        worldCamera.backgroundColor = new Color(.035f, .035f, .035f);
        cameraObject.transform.position = new Vector3(0, 0, -10);
        cameraObject.SetActive(false);
        workbenchObject = new GameObject("TopDownWorkbench");
        workbenchObject.transform.SetParent(transform, false);
        var backgroundRenderer = workbenchObject.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = workbench;
        backgroundRenderer.sortingOrder = -20;
        if (workbench != null)
        {
            Vector2 size = workbench.bounds.size;
            workbenchObject.transform.localScale = new Vector3(12.8f / size.x, 7.2f / size.y, 1);
        }
        itemRoot = new GameObject("Items").transform;
        itemRoot.SetParent(transform, false);
        AddBoundary(new Vector2(0, 3.55f), new Vector2(12.8f, .1f));
        AddBoundary(new Vector2(0, -3.55f), new Vector2(12.8f, .1f));
        AddBoundary(new Vector2(-6.35f, 0), new Vector2(.1f, 7.2f));
        AddBoundary(new Vector2(6.35f, 0), new Vector2(.1f, 7.2f));
        workbenchObject.SetActive(false);
    }

    /// <summary>물건이 화면 밖으로 나가지 않게 정적 경계를 만듭니다.</summary>
    private void AddBoundary(Vector2 position, Vector2 size)
    {
        var wall = new GameObject("Boundary");
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = position;
        wall.AddComponent<BoxCollider2D>().size = size;
    }

    /// <summary>정면 화면, 작업 안내, 키패드와 전환 덮개를 별도 UI로 구성합니다.</summary>
    private void BuildUi()
    {
        if (uiFont == null) uiFont = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 22);
        var canvasObject = new GameObject("TopDownTestCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        // 기존 정면 Canvas가 sortingOrder 100을 사용하므로 통이 상품 UI 뒤로 숨지 않게 한 단계 위에 둡니다.
        canvas.sortingOrder = 1000;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        canvasObject.AddComponent<GraphicRaycaster>();
        if (isEmbeddedInFrontScene || FindFirstObjectByType<EventSystem>() == null)
        {
            var eventObject = new GameObject("EventSystem");
            // 정면의 시작 버튼을 처리하는 EventSystem과 동시에 등록되지 않도록
            // 컴포넌트의 OnEnable이 실행되기 전에 비활성화합니다.
            if (isEmbeddedInFrontScene) eventObject.SetActive(false);
            eventObject.transform.SetParent(transform, false);
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            ownedEventSystem = eventObject;
        }

        frontRoot = Rect(canvasObject.transform, "FrontView", 0, 0, 1280, 720).gameObject;
        if (isEmbeddedInFrontScene)
        {
            Panel(frontRoot.transform, "ExistingInputBlocker", 0, 0, 1280, 720, new Color(0, 0, 0, .001f));
            frontContainerImage = Picture(frontRoot.transform, "FrontContainer", frontContainerMale, 460, 405, 360, 240, true);
        }
        else
        {
            Picture(frontRoot.transform, "FrontBackground", frontBackground, 0, 0, 1280, 720, false);
            customerImage = Picture(frontRoot.transform, "Customer", null, 365, 55, 550, 550, true);
            frontContainerImage = Picture(frontRoot.transform, "FrontContainer", frontContainerMale, 460, 405, 360, 240, true);
            Panel(frontRoot.transform, "DialoguePanel", 320, 585, 640, 62, new Color(.035f, .045f, .05f, .95f));
            dialogueText = Label(frontRoot.transform, "Dialogue", "물품을 가져왔습니다.", 340, 600, 600, 34, 21, Color.white);
            Picture(frontRoot.transform, "Counter", frontCounter, 0, 0, 1280, 720, false);
        }

        for (int i = 0; i < landingDust.Length; i++)
        {
            var dust = Panel(frontRoot.transform, "LandingDust" + i, 0, 0, 12 + i % 3 * 4, 6 + i % 2 * 4, Color.clear);
            landingDust[i] = dust.GetComponent<Image>();
            landingDust[i].raycastTarget = false;
            dust.SetSiblingIndex(frontContainerImage.transform.GetSiblingIndex());
        }
        workUiRoot = Rect(canvasObject.transform, "WorkViewUI", 0, 0, 1280, 720).gameObject;
        clueText = Label(workUiRoot.transform, "CustomerClue", "", 28, 20, 440, 54, 20, new Color(.87f, .86f, .79f));
        clockRoot = Picture(canvasObject.transform, "CounterClock", counterClockArtwork, 1090, 380, 180, 180, false).rectTransform;
        clockText = Label(clockRoot, "BusinessClock", "09:00", 27, 77, 125, 38, 26, new Color(.40f,.58f,.43f));
        clockText.alignment = TextAnchor.MiddleCenter;
        noticeText = Label(workUiRoot.transform, "Notice", "", 315, 640, 620, 44, 20, new Color(.92f, .77f, .55f));
        noticeText.alignment = TextAnchor.MiddleCenter;
        var register = Picture(workUiRoot.transform, "Register", calculatorArtwork, 900, 310, 360, 360, false).rectTransform;
        register.GetComponent<Image>().raycastTarget = true;
        keypadRect = register;
        inputText = Label(register, "PriceInput", "0", 51, 50, 250, 51, 25, new Color(.40f,.58f,.43f));
        inputText.alignment = TextAnchor.MiddleRight;
        for (int i = 1; i <= 9; i++)
        {
            string digit = i.ToString();
            ArtworkButton(register, "Digit" + digit, 147 + (i - 1) % 3 * 243, 428 + (i - 1) / 3 * 166, 214, 151, () => Digit(digit));
        }
        ArtworkButton(register, "Backspace", 877, 428, 214, 151, Backspace);
        ArtworkButton(register, "Digit000", 877, 594, 214, 151, () => Digit("000"));
        ArtworkButton(register, "Digit00", 877, 760, 214, 151, () => Digit("00"));
        ArtworkButton(register, "Clear", 147, 922, 454, 191, ClearAmount);
        ArtworkButton(register, "Confirm", 635, 922, 456, 191, ConfirmSale);
        var toggleSprite = SliceUi(calculatorToggleArtwork, new Rect(343, 357, 552, 601));
        var toggleImage = Picture(workUiRoot.transform, "CalculatorToggle", toggleSprite, 1214, 659, 54, 58, false);
        calculatorToggleRect = toggleImage.rectTransform;
        toggleImage.raycastTarget = true;
        var toggle = toggleImage.gameObject.AddComponent<Button>();
        toggle.transition = Selectable.Transition.None;
        toggle.onClick.AddListener(() => calculatorOpen = !calculatorOpen);
        toggleImage.gameObject.AddComponent<DystopiaKeyFeedback>();
        pouringContainerImage = Picture(workUiRoot.transform, "PouringContainer", tiltedContainer, 40, 150, 450, 450, true);
        transitionBlock = Panel(canvasObject.transform, "Transition", 0, 0, 1280, 720, Color.black).gameObject;
        transitionBlock.SetActive(false);
        workUiRoot.SetActive(false);
    }

    /// <summary>정면 등장부터 쏟기, 분류, 반응, 다음 손님까지 한 거래를 진행합니다.</summary>
    private IEnumerator CustomerFlow()
    {
        ShowFront("물품을 가져왔습니다. 확인해 주세요.");
        yield return PlaceContainer();
        yield return WaitUnscaled(Mathf.Max(0, frontArrivalSeconds - .85f));
        state = ViewState.Transition;
        yield return ScrollToWork();
        ShowWork();
        transitionBlock.SetActive(false);
        state = ViewState.Pouring;
        yield return PourItems();
        state = ViewState.Sorting;
        pouringContainerImage.gameObject.SetActive(false);
        ResetCursorSample();
        RefreshUi();
    }

    /// <summary>상자를 내려놓고 바닥을 고정한 채 짧게 눌렀다 복원하며 픽셀 먼지를 흩뿌립니다.</summary>
    private IEnumerator PlaceContainer()
    {
        RectTransform box = frontContainerImage.rectTransform;
        Vector2 rest = box.anchoredPosition;
        float elapsed = 0;
        while (elapsed < .85f)
        {
            if (!isPaused) elapsed += Time.unscaledDeltaTime;
            float drop = Mathf.Clamp01(elapsed / .3f);
            float impact = Mathf.Clamp01((elapsed - .3f) / .55f);
            float squash = Mathf.Sin(impact * Mathf.PI * 2) * Mathf.Exp(-impact * 4) * .10f;
            float scaleX = 1 + squash;
            float scaleY = 1 - squash;
            box.localScale = new Vector3(scaleX, scaleY, 1);
            box.anchoredPosition = rest + new Vector2(box.sizeDelta.x * (1 - scaleX) * .5f,
                38 * (1 - drop * drop) - box.sizeDelta.y * (1 - scaleY));
            for (int i = 0; i < landingDust.Length; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float spread = 80 + i / 2 * 13 + impact * (35 + i * 3);
                landingDust[i].rectTransform.anchoredPosition = new Vector2(
                    Mathf.Round((640 + side * spread) / 2) * 2,
                    -Mathf.Round((633 - Mathf.Sin(impact * Mathf.PI * .5f) * (12 + i % 3 * 5)) / 2) * 2);
                landingDust[i].color = new Color(.34f, .32f, .28f,
                    elapsed > .3f ? (1 - impact) * .42f : 0);
            }
            yield return null;
        }
        box.anchoredPosition = rest;
        box.localScale = Vector3.one;
        foreach (Image dust in landingDust) dust.color = Color.clear;
    }

    /// <summary>작업대 전환 위로 상자가 왼쪽 밖에서 더 천천히 들어와 감속합니다.</summary>
    private IEnumerator ScrollToWork()
    {
        var cover = transitionBlock.GetComponent<Image>();
        var coverRect = cover.rectTransform;
        frontContainerImage.gameObject.SetActive(false);
        var box = pouringContainerImage.rectTransform;
        Transform originalParent = box.parent;
        int originalIndex = box.GetSiblingIndex();
        // 작업대와 분리해 화면 밖에서 들어오는 상자의 이동과 감속을 충분히 보여 줍니다.
        box.SetParent(coverRect.parent, false);
        box.anchoredPosition = new Vector2(-40, -150);
        box.sizeDelta = new Vector2(450, 450);
        box.localRotation = Quaternion.Euler(0, 0, -90);
        pouringContainerImage.sprite = tiltedContainer;
        pouringContainerImage.gameObject.SetActive(true);
        cover.sprite = workbench;
        cover.color = Color.white;
        coverRect.anchoredPosition = new Vector2(-1280, 0);
        transitionBlock.SetActive(true);
        box.SetAsLastSibling();
        float elapsed = 0;
        float slideSeconds = Mathf.Max(.85f, transitionSeconds);
        while (elapsed < slideSeconds)
        {
            if (!isPaused) elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, transitionSeconds)));
            coverRect.anchoredPosition = new Vector2(Mathf.Lerp(-1280, 0, progress), 0);
            float slide = Mathf.Clamp01(elapsed / slideSeconds);
            float easeOut = 1 - Mathf.Pow(1 - slide, 3);
            box.anchoredPosition = new Vector2(Mathf.Lerp(-40, 350, easeOut), -150);
            yield return null;
        }
        // 도착한 자세를 쏟기 화면에 그대로 넘기고 전환 덮개만 복구합니다.
        box.SetParent(originalParent, false);
        box.SetSiblingIndex(originalIndex);
        transitionBlock.SetActive(false);
        coverRect.anchoredPosition = Vector2.zero;
        cover.sprite = null;
        cover.color = Color.black;
    }

    /// <summary>손님 정면 이미지와 철재통을 기존 성별·외형 데이터로 표시합니다.</summary>
    private void ShowFront(string message)
    {
        frontContainerImage.gameObject.SetActive(true);
        state = ViewState.Front;
        worldCamera.gameObject.SetActive(false);
        workbenchObject.SetActive(false);
        workUiRoot.SetActive(false);
        if (ownedEventSystem != null && isEmbeddedInFrontScene) ownedEventSystem.SetActive(false);
        if (hostScreen != null)
        {
            hostScreen.gameObject.SetActive(true);
            hostScreen.enabled = false;
            if (hostBasketRoot != null) hostBasketRoot.SetActive(false);
        }
        frontRoot.SetActive(true);
        if (customerImage != null) customerImage.sprite = CustomerSprite(Session != null && Session.Customer.IsMale, Session != null ? Session.Customer.appearance : 0);
        frontContainerImage.sprite = Session != null && Session.Customer.IsMale ? frontContainerMale : frontContainerFemale;
        if (dialogueText != null) dialogueText.text = message;
    }

    /// <summary>작업대와 손님 단서를 표시하며 가격 정답은 노출하지 않습니다.</summary>
    private void ShowWork()
    {
        if (hostScreen != null) hostScreen.gameObject.SetActive(false);
        frontRoot.SetActive(false);
        worldCamera.gameObject.SetActive(true);
        workbenchObject.SetActive(true);
        workUiRoot.SetActive(true);
        if (ownedEventSystem != null) ownedEventSystem.SetActive(true);
        int unitCount = 0;
        foreach (DystopiaBasketLine line in Session.Customer.basket) unitCount += line.quantity;
        clueText.text = $"{(Session.Customer.IsMale ? "남성" : "여성")} · {(Session.Customer.isPoor ? "형편이 어려운 손님" : "일반 손님")}\n가져온 물품 {unitCount}개";
    }

    /// <summary>장바구니 단위마다 다른 시점·방향·속도·회전으로 물품을 쏟습니다.</summary>
    private IEnumerator PourItems()
    {
        ClearItems();
        pouringContainerImage.gameObject.SetActive(true);
        pouringContainerImage.sprite = tiltedContainer;
        RectTransform pouringRect = pouringContainerImage.rectTransform;
        Vector2 pourStart = new Vector2(350, -150);
        Vector2 pourEnd = new Vector2(400, -165);
        pouringRect.anchoredPosition = pourStart;
        pouringRect.localRotation = Quaternion.Euler(0, 0, -90);
        StartCoroutine(AnimatePourContainer(pouringRect, pourStart, pourEnd));
        var random = new System.Random(Session.Revision * 7919 + 17);
        int totalUnits = 0;
        foreach (DystopiaBasketLine line in Session.Customer.basket) totalUnits += line.quantity;
        int created = 0;
        for (int lineIndex = 0; lineIndex < Session.Customer.basket.Count; lineIndex++)
        {
            DystopiaBasketLine line = Session.Customer.basket[lineIndex];
            int productId = ProductId(line.product);
            for (int unitIndex = 0; unitIndex < line.quantity; unitIndex++)
            {
                float delay = totalUnits <= 1 ? 0 : pourSpreadSeconds * created / (totalUnits - 1f);
                if (delay > 0) yield return WaitUnscaled(pourSpreadSeconds / Mathf.Max(1, totalUnits - 1));
                // 제공 이미지의 위쪽 여백을 제외한 입구를 화면 좌표에서 물리 좌표로 옮깁니다.
                Vector3 mouth = pouringRect.TransformPoint(new Vector3(pouringRect.rect.center.x,
                    pouringRect.rect.yMax - pouringRect.rect.height * .13f, 0));
                Vector2 mouthScreen = RectTransformUtility.WorldToScreenPoint(null, mouth);
                Vector3 mouthWorld = worldCamera.ScreenToWorldPoint(new Vector3(mouthScreen.x, mouthScreen.y, -worldCamera.transform.position.z));
                var item = CreateItem(productId, lineIndex, unitIndex,
                    new Vector2(mouthWorld.x, mouthWorld.y + Mathf.Lerp(-.35f, .35f, (float)random.NextDouble())));
                // 쏟는 동안 마찰을 낮춰 입구에 뭉치지 않고 중앙의 서로 다른 지점으로 미끄러지게 합니다.
                Vector2 destination = new Vector2(Mathf.Lerp(-1.8f, -.5f, (float)random.NextDouble()),
                    Mathf.Lerp(-.65f, .65f, (float)random.NextDouble()));
                item.Body.linearDamping = Mathf.Min(itemFriction, 1f);
                item.Body.linearVelocity = Vector2.ClampMagnitude(
                    (destination - item.Body.position) * (pourForce * .64f), maximumItemSpeed);
                item.Body.angularVelocity = Mathf.Lerp(-65f, 65f, (float)random.NextDouble());
                created++;
            }
        }
        // 마지막 물품이 나온 즉시 정면·쏟기 상자를 모두 숨깁니다.
        frontContainerImage.gameObject.SetActive(false);
        pouringContainerImage.gameObject.SetActive(false);
        float remaining = Mathf.Max(0, pourDuration - pourSpreadSeconds);
        yield return WaitUnscaled(remaining);
        // 중앙으로 흘러간 뒤에는 기존 조작용 마찰로 복귀해 물품이 계속 떠다니지 않게 합니다.
        foreach (DystopiaTopDownItem item in items) item.Body.linearDamping = itemFriction;

    }

    /// <summary>쏟는 동안 사각 철재통을 왼쪽에서 오른쪽으로 옮기며 조금 더 기울입니다.</summary>
    private IEnumerator AnimatePourContainer(RectTransform rect, Vector2 from, Vector2 to)
    {
        float elapsed = 0;
        while (elapsed < pourDuration && state == ViewState.Pouring)
        {
            if (!isPaused) elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, pourDuration)));
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-90, -100f, t));
            yield return null;
        }
    }

    /// <summary>한 장바구니 단위를 고유 ID와 실제 상품 ID를 가진 독립 물리 객체로 만듭니다.</summary>
    private DystopiaTopDownItem CreateItem(int productId, int lineIndex, int unitIndex, Vector2 position)
    {
        var itemObject = new GameObject($"Item_{nextInstanceId}_{Session.ActiveProducts[productId].name}");
        itemObject.transform.SetParent(itemRoot, false);
        itemObject.transform.localPosition = position;
        var renderer = itemObject.AddComponent<SpriteRenderer>();
        renderer.sprite = productSprites[productId];
        // 저해상도 import 크기와 무관하게 긴 변을 가판 기준 180픽셀로 표시합니다.
        Vector2 spriteSize = renderer.sprite.bounds.size;
        itemObject.transform.localScale = Vector3.one * (1.8f / Mathf.Max(spriteSize.x, spriteSize.y));
        renderer.sortingOrder = 10 + nextInstanceId;
        var body = itemObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.linearDamping = itemFriction;
        body.angularDamping = rotationDamping * .2f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        var collider = itemObject.AddComponent<BoxCollider2D>();
        Vector2 visibleSize = renderer.sprite != null ? renderer.sprite.bounds.size : Vector2.one;
        collider.size = new Vector2(visibleSize.x * .72f, visibleSize.y * .72f);
        var item = itemObject.AddComponent<DystopiaTopDownItem>();
        item.Initialize(nextInstanceId++, productId, lineIndex, unitIndex, body);
        items.Add(item);
        return item;
    }

    /// <summary>기존 활성 상품 참조에서 테스트용 0~3 ID를 찾습니다.</summary>
    private int ProductId(DystopiaProduct product)
    {
        for (int i = 0; i < Session.ActiveProducts.Count; i++) if (ReferenceEquals(Session.ActiveProducts[i], product)) return i;
        throw new InvalidOperationException($"활성 상품에 없는 장바구니 품목입니다: {product.name}");
    }

    /// <summary>클릭 여부와 무관하게 실제 포인터의 프레임 이동 구간 전체로 물품을 밉니다.</summary>
    private void ProcessPhysicalCursor(float deltaSeconds)
    {
        if (Mouse.current == null || deltaSeconds <= 0) return;
        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (PointerOverUi(screenPosition))
        {
            ResetCursorSample();
            return;
        }
        Vector2 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        if (!hasCursorSample)
        {
            previousCursorWorld = worldPosition;
            hasCursorSample = true;
            return;
        }
        ApplyCursorSweep(previousCursorWorld, worldPosition, deltaSeconds);
        previousCursorWorld = worldPosition;
    }

    /// <summary>커서 이동에 비례한 충격과 회전을 접촉한 물품에 계속 전달합니다.</summary>
    /// <param name="from">이전 커서의 월드 위치입니다.</param>
    /// <param name="to">현재 커서의 월드 위치입니다.</param>
    /// <param name="deltaSeconds">표본 사이의 경과 초입니다.</param>
    /// <returns>이번 표본에서 새 충격을 받은 물품 수입니다.</returns>
    public int ApplyCursorSweep(Vector2 from, Vector2 to, float deltaSeconds)
    {
        if (state != ViewState.Sorting || isPaused || deltaSeconds <= 0) return 0;
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        Vector2 velocity = delta / deltaSeconds;
        int count = Physics2D.CircleCastNonAlloc(from, cursorRadius, distance > .0001f ? delta / distance : Vector2.right, sweepHits, distance);
        currentCursorContacts.Clear();
        int affected = 0;
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D body = sweepHits[i].rigidbody;
            var item = body != null ? body.GetComponent<DystopiaTopDownItem>() : null;
            if (item == null || item.State == TopDownItemState.Excluded || !currentCursorContacts.Add(body)) continue;
            // 커서 속도를 따라가게 하지 않고 현재 커서에서 바깥으로 밀어냅니다.
            // 정지한 커서와 겹쳐도 최소 이탈 속도를 주되 이미 멀어지는 물품에는 힘을 누적하지 않습니다.
            Vector2 away = body.position - to;
            if (away.sqrMagnitude < .000001f) away = distance > .0001f ? delta : Vector2.right;
            away.Normalize();
            float escapeSpeed = Mathf.Clamp(2.8f + velocity.magnitude * cursorForce, 2.8f, maximumItemSpeed);
            float speedToAdd = escapeSpeed - Vector2.Dot(body.linearVelocity, away);
            if (speedToAdd <= 0) continue;
            affected++;
            item.WasStirred = true;
            body.AddForce(away * speedToAdd * body.mass, ForceMode2D.Impulse);
            float spinDirection = Vector2.SignedAngle(Vector2.right, away) < 0 ? -1 : 1;
            body.angularVelocity = Mathf.Clamp(body.angularVelocity + spinDirection * 540, -720, 720);
        }
        return affected;
    }

    /// <summary>포인터가 계산기나 다른 UI를 통과할 때 물리 입력을 막습니다.</summary>
    private bool PointerOverUi(Vector2 screenPosition)
    {
        if (keypadRect != null && keypadRect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(keypadRect, screenPosition)) return true;
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>속도 상한을 적용하고 물품을 평면 경계 안에 유지합니다.</summary>
    private void ClampItemMotion()
    {
        foreach (DystopiaTopDownItem item in items)
        {
            if (item.State == TopDownItemState.Excluded) continue;
            item.Body.linearVelocity = Vector2.ClampMagnitude(item.Body.linearVelocity, maximumItemSpeed);
            item.Body.angularVelocity = Mathf.Clamp(item.Body.angularVelocity, -720, 720);
            Vector2 position = item.transform.localPosition;
            item.transform.localPosition = new Vector3(Mathf.Clamp(position.x, -5.95f, 5.95f), Mathf.Clamp(position.y, -3.15f, 3.15f), 0);
        }
    }

    /// <summary>현재 위치로 판매 후보를 갱신하고 영역 밖으로 나오면 미분류로 되돌립니다.</summary>
    private void ClassifySettledItems()
    {
        foreach (DystopiaTopDownItem item in items)
        {
            if (item.State == TopDownItemState.Excluded) continue;
            Vector2 center = item.transform.localPosition;
            if (item.WasStirred && ExcludedZone.Contains(center)) TryClassify(item, TopDownItemState.Excluded);
            else if (SaleZone.Contains(center)) item.State = TopDownItemState.ForSale;
            else item.State = TopDownItemState.Working;
        }
    }

    /// <summary>분류 상태를 바꾸되 판매 영역 안에서도 물리 움직임을 유지합니다.</summary>
    public bool TryClassify(DystopiaTopDownItem item, TopDownItemState target)
    {
        if (state != ViewState.Sorting || isPaused || item == null || item.State == TopDownItemState.Excluded) return false;
        if (target == TopDownItemState.Excluded)
        {
            if (!Session.ToggleBasketUnit(item.LineIndex, item.UnitIndex)) return false;
            item.State = target;
            item.gameObject.SetActive(false);
        }
        else if (target == TopDownItemState.ForSale)
        {
            item.State = target;

        }
        else return false;
        RefreshUi();
        return true;
    }

    /// <summary>키패드의 한 자리 또는 00·000 입력 전체를 최대 7자리 안에서 추가합니다.</summary>
    private void Digit(string digit)
    {
        if (state != ViewState.Sorting || isPaused || amount.Length + digit.Length > 7) return;
        amount += digit;
        noticeText.text = "";
        RefreshUi();
    }

    /// <summary>입력한 금액의 마지막 한 자리만 지웁니다.</summary>
    private void Backspace()
    {
        if (state != ViewState.Sorting || isPaused || amount.Length == 0) return;
        amount = amount.Substring(0, amount.Length - 1);
        RefreshUi();
    }

    /// <summary>입력 금액 전체를 지웁니다.</summary>
    private void ClearAmount()
    {
        if (state != ViewState.Sorting || isPaused) return;
        amount = "";
        RefreshUi();
    }

    /// <summary>미분류를 검사한 뒤 판매 목록을 잠그고 기존 거래 확정과 재정 반영을 호출합니다.</summary>
    private void ConfirmSale()
    {
        TryConfirm(amount);
    }

    /// <summary>자동 검사에서도 실제 확정 경로를 동일하게 실행합니다.</summary>
    public bool TryConfirm(string input)
    {
        if (state != ViewState.Sorting || isPaused || businessMinute >= 21 * 60) return false;
        ClassifySettledItems();
        foreach (DystopiaTopDownItem item in items)
        {
            if (item.State == TopDownItemState.Working)
            {
                noticeText.text = "중앙의 물품을 모두 분류하세요.";
                return false;
            }
        }
        state = ViewState.Locked;
        SetBodiesSimulated(false);
        if (!Session.Confirm(input))
        {
            state = ViewState.Sorting;
            SetBodiesSimulated(true);
            noticeText.text = Session.Customer.total == 0 ? "" : "판매 가격을 입력하세요.";
            return false;
        }
        if (flowRoutine != null) StopCoroutine(flowRoutine);
        flowRoutine = StartCoroutine(ResultFlow());
        return true;
    }

    /// <summary>기존 손님 반응을 정면에서 보여준 뒤 기존 결과 시간을 통해 다음 손님으로 이동합니다.</summary>
    private IEnumerator ResultFlow()
    {
        transitionBlock.SetActive(true);
        yield return WaitUnscaled(transitionSeconds);
        transitionBlock.SetActive(false);
        if (isEmbeddedInFrontScene)
        {
            worldCamera.gameObject.SetActive(false);
            workbenchObject.SetActive(false);
            workUiRoot.SetActive(false);
            frontRoot.SetActive(false);
            if (ownedEventSystem != null) ownedEventSystem.SetActive(false);
            hostScreen.gameObject.SetActive(true);
            if (hostBasketRoot != null) hostBasketRoot.SetActive(false);
            hostScreen.enabled = true;
            while (Session.Phase == DystopiaPhase.Result) yield return null;
        }
        else
        {
            ShowFront(Session.Feedback + (string.IsNullOrEmpty(Session.LastRuleViolation) ? "" : "\n지침 위반: " + Session.LastRuleViolation));
            yield return WaitUnscaled(reactionSeconds);
            Session.Tick(Mathf.Max(settings.resultSeconds, reactionSeconds) + .1f);
        }
        if (Session.Phase != DystopiaPhase.Trading)
        {
            if (hostScreen != null)
            {
                if (hostBasketRoot != null) hostBasketRoot.SetActive(false);
                hostScreen.enabled = true;
            }
            CloseBusiness();
            yield break;
        }
        amount = "";
        pouringContainerImage.sprite = tiltedContainer;
        flowRoutine = StartCoroutine(CustomerFlow());
    }

    /// <summary>테스트 영업 마감 뒤 입력과 물리를 잠그고 추가 거래를 막습니다.</summary>
    private void CloseBusiness()
    {
        state = ViewState.Closed;
        businessMinute = 21 * 60;
        SetBodiesSimulated(false);
        noticeText.text = "영업이 종료되었습니다. 새 거래는 반영되지 않습니다.";
        RefreshUi();
    }

    /// <summary>일반 일시정지에서 세션, 물리와 입력을 함께 정지하거나 재개합니다.</summary>
    public void SetPaused(bool paused)
    {
        if (isPaused == paused) return;
        isPaused = paused;
        if (Session != null && Session.IsPaused != paused) Session.TogglePause();
        SetBodiesSimulated(!paused && state != ViewState.Locked && state != ViewState.Closed);
        ResetCursorSample();
        noticeText.text = paused ? "일시정지" : "";
    }

    /// <summary>현재 물품 Rigidbody2D의 시뮬레이션 여부를 일괄 적용합니다.</summary>
    private void SetBodiesSimulated(bool simulated)
    {
        foreach (DystopiaTopDownItem item in items) if (item != null && item.Body != null) item.Body.simulated = simulated;
    }

    /// <summary>뷰 전환·일시정지·포커스 복귀 때 이전 포인터 위치를 폐기합니다.</summary>
    private void ResetCursorSample()
    {
        hasCursorSample = false;
    }

    /// <summary>다음 손님 전에 이전 물품 객체와 입력 상태를 제거합니다.</summary>
    private void ClearItems()
    {
        foreach (DystopiaTopDownItem item in items) if (item != null) Destroy(item.gameObject);
        items.Clear();
        amount = "";
        noticeText.text = "";
    }

    /// <summary>일시정지 중에는 경과하지 않는 unscaled 대기입니다.</summary>
    private IEnumerator WaitUnscaled(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0)
        {
            if (!isPaused) remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>현재 상태, 분류 수, 입력액과 09:00~21:00 시계를 화면에 반영합니다.</summary>
    private void RefreshUi()
    {
        if (inputText == null) return;
        inputText.text = string.IsNullOrEmpty(amount) ? "금액 입력" : int.TryParse(amount, out int parsed) ? $"{parsed:N0} 원" : amount;
        int excluded = 0;
        int forSale = 0;
        int working = 0;
        foreach (DystopiaTopDownItem item in items)
        {
            if (item.State == TopDownItemState.Excluded) excluded++;
            else if (item.State == TopDownItemState.ForSale) forSale++;
            else working++;
        }
        if (workUiRoot.activeSelf)
        {
            string status = state == ViewState.Pouring ? "물품을 쏟는 중…" : $"미분류 {working} · 판매 {forSale} · 제외 {excluded}";
            clueText.text = clueText.text.Split('\n')[0] + "\n" + status;
        }
        int minute = Mathf.Clamp(Mathf.FloorToInt(businessMinute), 9 * 60, 21 * 60);
        clockText.text = $"{minute / 60:00}:{minute % 60:00}";
    }

    /// <summary>현재 성별과 외형 번호에 맞는 기존 손님 Sprite를 반환합니다.</summary>
    private Sprite CustomerSprite(bool isMale, int appearance)
    {
        Sprite[] pool = isMale ? maleCustomers : femaleCustomers;
        return pool != null && pool.Length > 0 ? pool[Mathf.Abs(appearance) % pool.Length] : null;
    }

    /// <summary>확인용으로 모든 Working 물품을 지정 상태로 분류합니다.</summary>
    public void ClassifyAllForVerification(TopDownItemState target)
    {
        foreach (DystopiaTopDownItem item in new List<DystopiaTopDownItem>(items))
            if (item.State == TopDownItemState.Working) TryClassify(item, target);
    }

    /// <summary>확인용으로 테스트 시계를 마감 시각 직전 또는 이후로 설정합니다.</summary>
    public void SetBusinessMinuteForVerification(float minute)
    {
        businessMinute = minute;
    }

    /// <summary>원본 UI 이미지의 필요한 영역을 Point Sprite로 사용합니다.</summary>
    private Sprite SliceUi(Sprite source, Rect topLeftRect)
    {
        if (source == null) return null;
        Rect rect = new Rect(topLeftRect.x, source.texture.height - topLeftRect.y - topLeftRect.height, topLeftRect.width, topLeftRect.height);
        Sprite slice = Sprite.Create(source.texture, rect, new Vector2(.5f,.5f), 100);
        uiSlices.Add(slice);
        return slice;
    }

    /// <summary>그림의 버튼 위치에 같은 이미지 조각과 눌림 반응을 연결합니다.</summary>
    private void ArtworkButton(Transform parent, string name, float x, float y, float width, float height, Action action)
    {
        const float scale = 360f / 1254f;
        // 원본 버튼을 덮어 축소될 때 바닥 그림이 중복으로 비치지 않게 합니다.
        var socket = Panel(parent, name, x*scale, y*scale, width*scale, height*scale, new Color(.035f,.035f,.032f));
        var graphic = Picture(socket, "Key", SliceUi(calculatorArtwork, new Rect(x,y,width,height)), 0,0,width*scale,height*scale,false);
        graphic.rectTransform.pivot = new Vector2(.5f,.5f);
        graphic.rectTransform.anchoredPosition = new Vector2(width*scale*.5f,-height*scale*.5f);
        var button = socket.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = graphic;
        button.onClick.AddListener(() => action());
        socket.gameObject.AddComponent<DystopiaKeyFeedback>().Visual = graphic.rectTransform;
    }

    /// <summary>UI RectTransform을 좌상단 기준 픽셀 좌표로 만듭니다.</summary>
    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    /// <summary>단색 UI 패널을 만듭니다.</summary>
    private static RectTransform Panel(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        RectTransform rect = Rect(parent, name, x, y, width, height);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    /// <summary>원본 비율을 유지하는 UI Sprite 이미지를 만듭니다.</summary>
    private static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, bool preserveAspect)
    {
        RectTransform rect = Rect(parent, name, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>물마루 글꼴을 사용하는 텍스트를 만듭니다.</summary>
    private Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color)
    {
        RectTransform rect = Rect(parent, name, x, y, width, height);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = TextAnchor.MiddleLeft;
        return text;
    }

    /// <summary>동일 키패드 입력 함수를 호출하는 uGUI 버튼을 만듭니다.</summary>
    private Button MakeButton(Transform parent, string name, string caption, float x, float y, float width, float height, Action action)
    {
        RectTransform rect = Panel(parent, name, x, y, width, height, new Color(.19f, .24f, .26f, 1));
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => action());
        Text text = Label(rect, "Text", caption, 0, 0, width, height, 16, Color.white);
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }
}

/// <summary>기존 정면 프로토타입에 씬·프리팹 수정 없이 탑다운 테스트 브리지를 붙입니다.</summary>
public static class DystopiaTopDownRuntimeBootstrap
{
    /// <summary>런타임 씬 로드 이벤트를 등록해 Enter Play 설정과 관계없이 브리지를 연결합니다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Attach;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += Attach;
    }

    /// <summary>Scene의 Awake가 끝난 뒤 기존 DystopiaScreen과 동일한 세션으로 테스트를 연결합니다.</summary>
    private static void Attach(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        DystopiaScreen screen = UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>();
        if (screen == null) return;
        DystopiaTopDownTest.AttachToExistingScreen(screen);
    }
}

/// <summary>계산기 키를 가볍게 밝히고 누를 때 축소한 뒤 복원합니다.</summary>
public sealed class DystopiaKeyFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform Visual;
    private bool hovered, pressed;
    private void Update()
    {
        if (Visual == null) Visual = transform as RectTransform;
        float scale = pressed ? .91f : hovered ? 1.025f : 1;
        Visual.localScale = Vector3.Lerp(Visual.localScale, Vector3.one * scale, 1 - Mathf.Exp(-24 * Time.unscaledDeltaTime));
        var graphic = Visual.GetComponent<Image>();
        if (graphic != null) graphic.color = pressed ? new Color(.78f,.82f,.75f) : hovered ? new Color(1,.96f,.82f) : Color.white;
    }
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    private void OnDisable() { hovered = pressed = false; if (Visual != null) Visual.localScale = Vector3.one; }
}
