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
    private static readonly Rect SaleZone = new Rect(3.25f, 0.55f, 2.65f, 2.45f);
    private readonly List<DystopiaTopDownItem> items = new List<DystopiaTopDownItem>();
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[32];
    private static DystopiaScreen pendingHostScreen;
    private ViewState state;
    private DystopiaScreen hostScreen;
    private GameObject hostBasketRoot;
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
                if (child.name != "Basket") continue;
                hostBasketRoot = child.gameObject;
                break;
            }
        }
#if UNITY_EDITOR
        BindEditorAssets();
#endif
        if (!isEmbeddedInFrontScene) DisableOtherScenesDuringPlay();
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
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPaused(!isPaused);
        if (isPaused) return;
        float deltaSeconds = Time.unscaledDeltaTime;
        if (state == ViewState.Sorting)
        {
            businessMinute += deltaSeconds * gameMinutesPerRealSecond;
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

    /// <summary>포커스 복귀 직후의 큰 커서 속도 표본을 버립니다.</summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        ResetCursorSample();
    }

    /// <summary>테스트 브리지가 제거돼도 기존 상품 표시를 숨긴 채 남기지 않습니다.</summary>
    private void OnDestroy()
    {
        if (hostBasketRoot != null) hostBasketRoot.SetActive(true);
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
        scaler.matchWidthOrHeight = .5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        if (isEmbeddedInFrontScene || FindFirstObjectByType<EventSystem>() == null)
        {
            var eventObject = new GameObject("EventSystem");
            eventObject.transform.SetParent(transform, false);
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            ownedEventSystem = eventObject;
            if (isEmbeddedInFrontScene) ownedEventSystem.SetActive(false);
        }

        frontRoot = Rect(canvasObject.transform, "FrontView", 0, 0, 1280, 720).gameObject;
        if (isEmbeddedInFrontScene)
        {
            Panel(frontRoot.transform, "ExistingInputBlocker", 0, 0, 1280, 720, new Color(0, 0, 0, .001f));
            frontContainerImage = Picture(frontRoot.transform, "FrontContainer", frontContainerMale, 356, 322, 568, 350, true);
        }
        else
        {
            Picture(frontRoot.transform, "FrontBackground", frontBackground, 0, 0, 1280, 720, false);
            customerImage = Picture(frontRoot.transform, "Customer", null, 365, 55, 550, 550, true);
            frontContainerImage = Picture(frontRoot.transform, "FrontContainer", frontContainerMale, 382, 355, 516, 310, true);
            Panel(frontRoot.transform, "DialoguePanel", 320, 585, 640, 62, new Color(.035f, .045f, .05f, .95f));
            dialogueText = Label(frontRoot.transform, "Dialogue", "물품을 가져왔습니다.", 340, 600, 600, 34, 21, Color.white);
            Picture(frontRoot.transform, "Counter", frontCounter, 0, 0, 1280, 720, false);
        }

        workUiRoot = Rect(canvasObject.transform, "WorkViewUI", 0, 0, 1280, 720).gameObject;
        clueText = Label(workUiRoot.transform, "CustomerClue", "", 28, 20, 440, 54, 20, new Color(.87f, .86f, .79f));
        clockText = Label(workUiRoot.transform, "BusinessClock", "09:00", 1065, 22, 170, 42, 24, new Color(.84f, .84f, .78f));
        noticeText = Label(workUiRoot.transform, "Notice", "", 315, 640, 620, 44, 20, new Color(.92f, .77f, .55f));
        noticeText.alignment = TextAnchor.MiddleCenter;
        var register = Panel(workUiRoot.transform, "Register", 982, 392, 276, 306, new Color(.055f, .065f, .068f, .97f));
        keypadRect = register;
        Label(register, "Heading", "받을 금액", 18, 12, 150, 28, 17, new Color(.84f, .84f, .78f));
        inputText = Label(register, "PriceInput", "금액 입력", 18, 43, 240, 42, 29, Color.white);
        for (int i = 1; i <= 9; i++)
        {
            string digit = i.ToString();
            MakeButton(register, "Digit" + digit, digit, 17 + ((i - 1) % 3) * 82, 94 + ((i - 1) / 3) * 45, 74, 38, () => Digit(digit));
        }
        MakeButton(register, "Backspace", "한 칸", 17, 229, 74, 38, Backspace);
        MakeButton(register, "Digit0", "0", 99, 229, 74, 38, () => Digit("0"));
        MakeButton(register, "Clear", "전체", 181, 229, 74, 38, ClearAmount);
        MakeButton(register, "Confirm", "확인", 17, 273, 238, 27, ConfirmSale);
        pouringContainerImage = Picture(workUiRoot.transform, "PouringContainer", tiltedContainer, 40, 150, 310, 310, true);
        transitionBlock = Panel(canvasObject.transform, "Transition", 0, 0, 1280, 720, Color.black).gameObject;
        transitionBlock.SetActive(false);
        workUiRoot.SetActive(false);
    }

    /// <summary>정면 등장부터 쏟기, 분류, 반응, 다음 손님까지 한 거래를 진행합니다.</summary>
    private IEnumerator CustomerFlow()
    {
        ShowFront("물품을 가져왔습니다. 확인해 주세요.");
        yield return WaitUnscaled(frontArrivalSeconds);
        state = ViewState.Transition;
        transitionBlock.SetActive(true);
        yield return WaitUnscaled(transitionSeconds);
        ShowWork();
        transitionBlock.SetActive(false);
        state = ViewState.Pouring;
        yield return PourItems();
        state = ViewState.Sorting;
        pouringContainerImage.sprite = emptyContainer;
        ResetCursorSample();
        RefreshUi();
    }

    /// <summary>손님 정면 이미지와 철재통을 기존 성별·외형 데이터로 표시합니다.</summary>
    private void ShowFront(string message)
    {
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
        Vector2 pourStart = new Vector2(40, -150);
        Vector2 pourEnd = new Vector2(265, -165);
        pouringRect.anchoredPosition = pourStart;
        pouringRect.localRotation = Quaternion.identity;
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
                float y = Mathf.Lerp(1.45f, -1.25f, (float)random.NextDouble());
                var item = CreateItem(productId, lineIndex, unitIndex, new Vector2(-3.75f + created * .06f, y));
                float angle = Mathf.Lerp(-22f, 24f, (float)random.NextDouble());
                Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
                item.Body.linearVelocity = direction * Mathf.Lerp(pourForce * .68f, pourForce * 1.25f, (float)random.NextDouble());
                item.Body.angularVelocity = Mathf.Lerp(-150f, 150f, (float)random.NextDouble());
                created++;
            }
        }
        float remaining = Mathf.Max(0, pourDuration - pourSpreadSeconds);
        yield return WaitUnscaled(remaining);
        pouringContainerImage.sprite = emptyContainer;
        yield return WaitUnscaled(.2f);
        pouringContainerImage.gameObject.SetActive(false);
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
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, -16f, t));
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
        renderer.sortingOrder = 10 + nextInstanceId;
        var body = itemObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.linearDamping = itemFriction;
        body.angularDamping = rotationDamping;
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

    /// <summary>이전 위치부터 현재 위치까지 원형 스윕을 검사하고 속도에 비례한 충격을 줍니다.</summary>
    public int ApplyCursorSweep(Vector2 from, Vector2 to, float deltaSeconds)
    {
        if (state != ViewState.Sorting || isPaused || deltaSeconds <= 0) return 0;
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        Vector2 velocity = delta / deltaSeconds;
        int count = Physics2D.CircleCastNonAlloc(from, cursorRadius, distance > .0001f ? delta / distance : Vector2.right, sweepHits, distance);
        var affected = new HashSet<Rigidbody2D>();
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D body = sweepHits[i].rigidbody;
            var item = body != null ? body.GetComponent<DystopiaTopDownItem>() : null;
            if (item == null || item.State != TopDownItemState.Working || !affected.Add(body)) continue;
            item.WasStirred = true;
            Vector2 impulse = Vector2.ClampMagnitude(velocity * cursorForce, maximumItemSpeed * .55f);
            body.AddForce(impulse, ForceMode2D.Impulse);
            body.AddTorque(Vector2.SignedAngle(Vector2.right, delta) * .0025f, ForceMode2D.Impulse);
        }
        return affected.Count;
    }

    /// <summary>포인터가 계산기나 다른 UI를 통과할 때 물리 입력을 막습니다.</summary>
    private bool PointerOverUi(Vector2 screenPosition)
    {
        if (keypadRect != null && RectTransformUtility.RectangleContainsScreenPoint(keypadRect, screenPosition)) return true;
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>속도 상한을 적용하고 물품을 평면 경계 안에 유지합니다.</summary>
    private void ClampItemMotion()
    {
        foreach (DystopiaTopDownItem item in items)
        {
            if (item.State != TopDownItemState.Working) continue;
            item.Body.linearVelocity = Vector2.ClampMagnitude(item.Body.linearVelocity, maximumItemSpeed);
            Vector2 position = item.transform.localPosition;
            item.transform.localPosition = new Vector3(Mathf.Clamp(position.x, -5.95f, 5.95f), Mathf.Clamp(position.y, -3.15f, 3.15f), 0);
        }
    }

    /// <summary>쏟기 뒤 사용자가 움직인 물품의 중심이 영역 안에 들어왔을 때 한 번만 분류합니다.</summary>
    private void ClassifySettledItems()
    {
        foreach (DystopiaTopDownItem item in items)
        {
            if (item.State != TopDownItemState.Working || !item.WasStirred) continue;
            Vector2 center = item.transform.localPosition;
            if (ExcludedZone.Contains(center)) TryClassify(item, TopDownItemState.Excluded);
            else if (SaleZone.Contains(center)) TryClassify(item, TopDownItemState.ForSale);
        }
    }

    /// <summary>개별 물품을 제외 또는 판매 후보로 한 번만 확정합니다.</summary>
    public bool TryClassify(DystopiaTopDownItem item, TopDownItemState target)
    {
        if (state != ViewState.Sorting || isPaused || item == null || item.State != TopDownItemState.Working) return false;
        if (target == TopDownItemState.Excluded)
        {
            if (!Session.ToggleBasketUnit(item.LineIndex, item.UnitIndex)) return false;
            item.State = target;
            item.gameObject.SetActive(false);
        }
        else if (target == TopDownItemState.ForSale)
        {
            item.State = target;
            item.Body.linearVelocity = Vector2.zero;
            item.Body.angularVelocity = 0;
            item.Body.bodyType = RigidbodyType2D.Kinematic;
        }
        else return false;
        RefreshUi();
        return true;
    }

    /// <summary>키패드 한 자리 입력을 기존 최대 7자리 규칙에 맞게 보관합니다.</summary>
    private void Digit(string digit)
    {
        if (state != ViewState.Sorting || isPaused || amount.Length >= 7) return;
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
            if (hostBasketRoot != null) hostBasketRoot.SetActive(true);
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
                if (hostBasketRoot != null) hostBasketRoot.SetActive(true);
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
