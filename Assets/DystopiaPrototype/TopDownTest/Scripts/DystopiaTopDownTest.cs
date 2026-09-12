using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>탑다운 분류 테스트에서 개별 물품의 중복 없는 상태를 나타냅니다.</summary>
public enum TopDownItemState { Working, Excluded, ForSale }

/// <summary>기존 정면 거래와 경제 권위를 재사용하는 별도 탑다운 분류 테스트 화면입니다.</summary>
[DefaultExecutionOrder(-10000)]
public sealed partial class DystopiaTopDownTest : MonoBehaviour
{
    private enum ViewState { Front, Transition, Pouring, Sorting, Locked, Closed }

    [Header("Flow seconds")]
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.35f;
    [SerializeField, Min(0.1f)] private float pourDuration = 1.2f;
    [SerializeField, Min(0f)] private float pourSpreadSeconds = 0.55f;
    [SerializeField, Min(0f)] private float frontArrivalSeconds = 2f;
    [SerializeField, Min(0f)] private float reactionSeconds = 0.9f;

    /// <summary>연결하면 기존 커서 밀기 대신 구분봉으로 상품을 조작합니다.</summary>
    [SerializeField] private DividerBarController2D dividerBar;
    /// <summary>편집 가능한 청소기 배치와 클릭 홀드 흡입을 담당합니다.</summary>
    [SerializeField] private DystopiaVacuumController vacuum;
    /// <summary>원본 손 시트입니다. 4번은 사용하지 않으며 씬 배치와 무관한 커서로 그립니다.</summary>
    [SerializeField] private Texture2D handArtwork;
    /// <summary>손 커서의 화면 픽셀 크기입니다.</summary>
    [SerializeField, Min(16)] private float handSizePixels = 64;
    /// <summary>현재 잡은 상품과 상품 로컬 좌표의 잡기 지점입니다.</summary>
    private DystopiaTopDownItem heldItem;
    /// <summary>홀드 중에만 바꾼 물리 모드를 놓을 때 원래대로 돌립니다.</summary>
    private RigidbodyType2D heldBodyType;
    private RigidbodyInterpolation2D heldInterpolation;
    private Vector2 heldLocalPoint, heldTarget, previousHandPointer;
    /// <summary>가로 이동 판정의 프레임 간 속도와 커서 표시 복원 상태입니다.</summary>
    private Vector2 handVelocity;
    private bool hasHandPointer, showHand, ownsCursorVisibility, previousCursorVisible;

    /// <summary>쏟기 중 중앙 유도 지점과 확산 폭입니다. 월드 좌표 기준입니다.</summary>
    [SerializeField] private Vector2 pourCenter = new Vector2(-1.5f, 0);
    [SerializeField] private Vector2 pourSpread = new Vector2(2.6f, 2.2f);
    private readonly Dictionary<DystopiaTopDownItem, Vector2> pourTargets = new Dictionary<DystopiaTopDownItem, Vector2>();

    [Header("Planar item physics")]
    [SerializeField, Min(0.05f)] private float cursorRadius = 0.42f;
    [SerializeField, Min(0.01f)] private float cursorForce = 0.032f;
    [SerializeField, Min(0.1f)] private float maximumItemSpeed = 5.2f;
    [SerializeField, Min(0f)] private float itemFriction = 6.5f;
    [SerializeField, Min(0f)] private float rotationDamping = 4.5f;
    [SerializeField, Min(0f)] private float pourForce = 2.5f;

    [Header("Test business clock")]
    /// <summary>실제 1초당 게임 분입니다. 6이면 09~21시의 12시간이 실제 120초입니다.</summary>
    [SerializeField, Min(0.01f)] private float gameMinutesPerRealSecond = 6f;

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
    /// <summary>성인 24종, 남자아이 2종, 할아버지 3종 순서의 외형 참조입니다.</summary>
    [SerializeField] private Sprite[] maleCustomers = new Sprite[DystopiaSession.MaleAppearanceCount];
    /// <summary>성인 16종, 여자아이 2종, 할머니 2종 순서의 외형 참조입니다.</summary>
    [SerializeField] private Sprite[] femaleCustomers = new Sprite[DystopiaSession.FemaleAppearanceCount];
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
    /// <summary>작업대가 열린 뒤에는 물품을 쏟는 중에도 계산기 금액을 편집할 수 있습니다.</summary>
    private bool CanEditAmount => (state == ViewState.Pouring || state == ViewState.Sorting) && !isPaused && !Session.IsPaused;

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
        hostScreen = placedHostScreen != null ? placedHostScreen : pendingHostScreen;
        pendingHostScreen = null;
        isEmbeddedInFrontScene = hostScreen != null;
        if (isEmbeddedInFrontScene)
        {
            hostScreen.InitializePlacedScreen();
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
        if (handArtwork == null) handArtwork = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/Hands.png");
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
        if (Application.isPlaying) flowRoutine = StartCoroutine(isEmbeddedInFrontScene ? WaitForExistingTrade() : CustomerFlow());
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

    /// <summary>체크아웃 비활성화 시 연결된 구분봉의 충돌과 입력을 중지합니다.</summary>
    private void OnDisable()
    {
        if (vacuum != null) vacuum.Release();
        ReleaseHeldItem();
        SetHandVisible(false);
        if (dividerBar != null) dividerBar.SampleInput(worldCamera, false);
    }

    /// <summary>커서 이동, 평면 물리 제한, 분류와 영업시간을 실제 프레임에서 갱신합니다.</summary>
    private void Update()
    {
        ProcessGrabInput();
        if (hostScreen != null && hostScreen.gameObject.activeInHierarchy && !hostScreen.enabled && !isPaused) hostScreen.AnimatePeople();
        if (Session == null)
        {
            RestoreAfterReload();
            return;
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPaused(!isPaused);
        Rect layout = hasPlacedUi ? placedCalculatorLayout : hostScreen != null ? hostScreen.CalculatorLayout : calculatorLayout;
        float targetSlide = calculatorOpen ? 0 : Mathf.Max(0, 1300 - layout.x);
        calculatorSlide = Mathf.MoveTowards(calculatorSlide, targetSlide, Time.unscaledDeltaTime * 1600);
        if (hasPlacedUi && keypadMotion != null)
            keypadMotion.anchoredPosition = new Vector2(calculatorSlide, 0);
        else if (keypadRect != null)
        {
            keypadRect.anchoredPosition = new Vector2(layout.x + calculatorSlide, -layout.y);
        }
        if (clockRoot != null)
            clockRoot.gameObject.SetActive(!workUiRoot.activeSelf && !transitionBlock.activeSelf);
        // 시계는 별도 Canvas에 있어 정산 배경보다 위에 남으므로 숫자 표시를 상태에 맞춰 끕니다.
        if (clockText != null) clockText.enabled = Session.Phase != DystopiaPhase.Settlement;
        if (clockDay != Session.Day)
        {
            clockDay = Session.Day;
            businessMinute = 9 * 60;
            // 전날 마감된 흐름을 다시 연결하되, 가격 안내의 영업 시작을 기다립니다.
            if (isEmbeddedInFrontScene && state == ViewState.Closed)
            {
                if (flowRoutine != null) StopCoroutine(flowRoutine);
                ClearItems();
                state = ViewState.Front;
                flowRoutine = StartCoroutine(WaitForExistingTrade());
            }
        }
        if (isPaused) return;
        float deltaSeconds = Time.unscaledDeltaTime;
        if ((Session.Phase == DystopiaPhase.Trading || Session.Phase == DystopiaPhase.Result) && !Session.IsPaused)
            businessMinute = Mathf.Min(21 * 60, businessMinute + deltaSeconds * gameMinutesPerRealSecond);
        // 결과 단계가 다음 손님으로 넘어가기 전에 접수를 닫고 현재 거래는 끝까지 허용합니다.
        if (businessMinute >= 21 * 60) Session.StopAcceptingCustomers();
        // 계산기 입력은 쏟기 완료를 기다리지 않으며 물품 분류와 거래 확정 상태는 유지합니다.
        if (CanEditAmount) ProcessNumberPad();
        if (state == ViewState.Sorting)
        {
            Session.Tick(deltaSeconds);
            ClampItemMotion();
            ClassifySettledItems();
        }
        else ResetGrabInput();
        RefreshUi();
    }

    /// <summary>재컴파일 뒤 소실된 세션과 코루틴을 정면 화면의 새 세션에 다시 연결합니다.</summary>
    private void RestoreAfterReload()
    {
        StopAllCoroutines();
        // 재컴파일 뒤에도 Scene 배치는 유지하고 생성된 물품만 제거합니다.
        var savedItems = transform.Find("Items");
        if (savedItems != null)
            foreach (Transform child in savedItems) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        uiSlices.Clear();
        items.Clear();
        currentCursorContacts.Clear();
        ReleaseHeldItem();
        hasHandPointer = false;
        handVelocity = Vector2.zero;
        SetHandVisible(false);
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
        ResetGrabInput();
    }

    /// <summary>테스트 브리지가 제거돼도 기존 상품 표시를 숨긴 채 남기지 않습니다.</summary>
    private void OnDestroy()
    {
        if (hostBasketRoot != null) hostBasketRoot.SetActive(true);
        foreach (Sprite slice in uiSlices)
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.Contains(slice)) continue;
#endif
            if (slice != null) Destroy(slice);
        }
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
        if (maleCustomers == null || maleCustomers.Length != DystopiaSession.MaleAppearanceCount)
            Array.Resize(ref maleCustomers, DystopiaSession.MaleAppearanceCount);
        if (femaleCustomers == null || femaleCustomers.Length != DystopiaSession.FemaleAppearanceCount)
            Array.Resize(ref femaleCustomers, DystopiaSession.FemaleAppearanceCount);
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

    /// <summary>Scene에 저장된 카메라와 작업대를 연결하며 기본 배치를 생성하지 않습니다.</summary>
    private void BuildWorld()
    {
        if (!BindPlacedWorld()) throw new InvalidOperationException("Scene에 배치된 TopDownCamera, TopDownWorkbench, Items가 필요합니다.");
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
        if (!BindPlacedUi()) throw new InvalidOperationException("Scene에 배치된 TopDownTestCanvas가 필요합니다.");
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
        ResetGrabInput();
        RefreshUi();
    }

    /// <summary>상자를 내려놓고 바닥을 고정한 채 짧게 눌렀다 복원하며 픽셀 먼지를 흩뿌립니다.</summary>
    private IEnumerator PlaceContainer()
    {
        // 어린이는 정면 상자로 얼굴을 가리지 않으며 상자 착지 효과도 생략합니다.
        if (Session.Customer.Type == DystopiaCustomerType.Child)
        {
            frontContainerImage.gameObject.SetActive(false);
            if (customerImage != null)
            {
                // 독립 작업대 화면도 정면 화면과 같은 짧은 등장 동작을 사용합니다.
                RectTransform child = customerImage.rectTransform;
                Vector2 restPosition = child.anchoredPosition;
                float popElapsed = 0;
                while (popElapsed < .28f)
                {
                    if (!isPaused) popElapsed += Time.unscaledDeltaTime;
                    float remaining = Mathf.Pow(1 - Mathf.Clamp01(popElapsed / .28f), 3);
                    child.anchoredPosition = restPosition + Vector2.down * remaining * 240;
                    yield return null;
                }
                child.anchoredPosition = restPosition;
                yield return WaitUnscaled(.57f);
            }
            else yield return WaitUnscaled(.85f);
            yield break;
        }
        RectTransform box = frontContainerImage.rectTransform;
        Vector2 rest = box.anchoredPosition;
        Vector3 restScale = box.localScale;
        // Inspector에서 변경한 배치는 애니메이션 시작값으로 덮어쓰지 않습니다.
        Vector2 lastPosition = rest;
        Vector3 lastScale = restScale;
        float elapsed = 0;
        while (elapsed < .85f)
        {
            if (box.anchoredPosition != lastPosition || box.localScale != lastScale)
            {
                foreach (Image dust in landingDust) dust.color = Color.clear;
                yield break;
            }
            if (!isPaused) elapsed += Time.unscaledDeltaTime;
            float drop = Mathf.Clamp01(elapsed / .3f);
            float impact = Mathf.Clamp01((elapsed - .3f) / .55f);
            float squash = Mathf.Sin(impact * Mathf.PI * 2) * Mathf.Exp(-impact * 4) * .10f;
            float scaleX = 1 + squash;
            float scaleY = 1 - squash;
            box.localScale = Vector3.Scale(restScale, new Vector3(scaleX, scaleY, 1));
            box.anchoredPosition = rest + new Vector2(box.sizeDelta.x * (1 - scaleX) * .5f,
                38 * (1 - drop * drop) - box.sizeDelta.y * (1 - scaleY));
            lastPosition = box.anchoredPosition;
            lastScale = box.localScale;
            for (int i = 0; i < landingDust.Length; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float spread = 80 + i / 2 * 13 + impact * (35 + i * 3);
                landingDust[i].rectTransform.anchoredPosition = new Vector2(
                    Mathf.Round((rest.x + box.sizeDelta.x * restScale.x * .5f + side * spread) / 2) * 2,
                    Mathf.Round((rest.y - box.sizeDelta.y * restScale.y + 12 + Mathf.Sin(impact * Mathf.PI * .5f) * (12 + i % 3 * 5)) / 2) * 2);
                landingDust[i].color = new Color(.34f, .32f, .28f,
                    elapsed > .3f ? (1 - impact) * .42f : 0);
            }
            yield return null;
        }
        if (box.anchoredPosition == lastPosition && box.localScale == lastScale)
        {
            box.anchoredPosition = rest;
            box.localScale = restScale;
        }
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
        Vector2 destination = hasPlacedUi ? placedPourPosition : new Vector2(350, -150);
        box.anchoredPosition = new Vector2(-box.sizeDelta.x * Mathf.Abs(box.localScale.x) - 40, destination.y);
        Vector2 offscreen = box.anchoredPosition;
        box.localRotation = Quaternion.Euler(0, 0, hasPlacedUi ? placedPourAngle : -90);
        if (!hasPlacedUi) pouringContainerImage.sprite = tiltedContainer;
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
            box.anchoredPosition = Vector2.Lerp(offscreen, destination, easeOut);
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
        frontContainerImage.gameObject.SetActive(Session == null || Session.Customer.Type != DystopiaCustomerType.Child);
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
        if (!hasPlacedUi) frontContainerImage.sprite = Session != null && Session.Customer.IsMale ? frontContainerMale : frontContainerFemale;
        if (dialogueText != null) dialogueText.text = message;
    }

    /// <summary>작업대와 손님 단서를 표시하며 가격 정답은 노출하지 않습니다.</summary>
    private void ShowWork()
    {
        if (dividerBar != null) dividerBar.ResetToStart();
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
        pourTargets.Clear();
        pouringContainerImage.gameObject.SetActive(true);
        if (!hasPlacedUi) pouringContainerImage.sprite = tiltedContainer;
        RectTransform pouringRect = pouringContainerImage.rectTransform;
        Vector2 pourStart = hasPlacedUi ? placedPourPosition : new Vector2(350, -150);
        Vector2 pourEnd = pourStart + new Vector2(50, -15);
        pouringRect.anchoredPosition = pourStart;
        pouringRect.localRotation = Quaternion.Euler(0, 0, hasPlacedUi ? placedPourAngle : -90);
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
                    (Vector2)itemRoot.InverseTransformPoint(new Vector3(mouthWorld.x, mouthWorld.y + Mathf.Lerp(-.35f, .35f, (float)random.NextDouble()), itemRoot.position.z)));
                // 쏟는 동안 마찰을 낮춰 입구에 뭉치지 않고 중앙의 서로 다른 지점으로 미끄러지게 합니다.
                int columns = Mathf.CeilToInt(Mathf.Sqrt(totalUnits));
                int rows = Mathf.CeilToInt(totalUnits / (float)columns);
                Vector2 destination = pourCenter + new Vector2(
                    columns <= 1 ? 0 : Mathf.Lerp(-pourSpread.x * .5f, pourSpread.x * .5f, (created % columns) / (columns - 1f)),
                    rows <= 1 ? 0 : Mathf.Lerp(-pourSpread.y * .5f, pourSpread.y * .5f, (created / columns) / (rows - 1f)));
                pourTargets[item] = destination;
                item.Body.linearDamping = Mathf.Min(item.RestingLinearDamping, 1f);
                item.Body.linearVelocity = Vector2.ClampMagnitude(
                    (destination - item.Body.position) * (pourForce * .64f), maximumItemSpeed);
                item.Body.angularVelocity = Mathf.Lerp(-65f, 65f, (float)random.NextDouble());
                created++;
            }
        }
        // 마지막 물품이 나온 즉시 정면·쏟기 상자를 모두 숨깁니다.
        frontContainerImage.gameObject.SetActive(false);
        pouringContainerImage.gameObject.SetActive(false);
        float remaining = Mathf.Max(2f, pourDuration - pourSpreadSeconds);
        yield return WaitUnscaled(remaining);
        // 중앙으로 흘러간 뒤에는 기존 조작용 마찰로 복귀해 물품이 계속 떠다니지 않게 합니다.
        pourTargets.Clear();
        foreach (DystopiaTopDownItem item in items) item.Body.linearDamping = item.RestingLinearDamping;

    }

    /// <summary>쏟기 단계에서만 중앙 도착을 보조하고 분류 중에는 자유 물리를 유지합니다.</summary>
    private void FixedUpdate()
    {
        if (heldItem != null && state == ViewState.Sorting && !isPaused && !Session.IsPaused)
        {
            // 홀드 중에는 스프링 추종과 속도 제한 없이 클릭 지점을 손에 고정합니다.
            Vector2 grip = heldItem.transform.TransformPoint(heldLocalPoint);
            heldItem.Body.position += heldTarget - grip;
            heldItem.Body.linearVelocity = Vector2.zero;
            heldItem.Body.angularVelocity = 0;
        }
        if (state != ViewState.Pouring || isPaused) return;
        foreach (var pair in pourTargets)
        {
            var item = pair.Key;
            if (item == null || !item.Body.simulated) continue;
            Vector2 delta = pair.Value - item.Body.position;
            Vector2 desired = Vector2.ClampMagnitude(delta * 3f, maximumItemSpeed);
            // 한 번의 발사 후 마찰에 멈추지 않도록 제한된 가속도로 중앙 방향을 유지합니다.
            item.Body.linearVelocity = Vector2.MoveTowards(item.Body.linearVelocity, desired, 18f * Time.fixedDeltaTime);
        }
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
            float angle = hasPlacedUi ? placedPourAngle : -90;
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(angle, angle - 10, t));
            yield return null;
        }
    }

    /// <summary>한 장바구니 단위를 고유 ID와 실제 상품 ID를 가진 독립 물리 객체로 만듭니다.</summary>
    private DystopiaTopDownItem CreateItem(int productId, int lineIndex, int unitIndex, Vector2 position)
    {
        bool fromPrefab = placedItemPrefabs != null && productId < placedItemPrefabs.Length && placedItemPrefabs[productId] != null;
        var itemObject = fromPrefab ? Instantiate(placedItemPrefabs[productId]) : new GameObject($"Item_{nextInstanceId}_{Session.ActiveProducts[productId].name}");
        itemObject.SetActive(true);
        itemObject.transform.SetParent(itemRoot, false);
        itemObject.transform.localPosition = position;
        var renderer = itemObject.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = itemObject.AddComponent<SpriteRenderer>();
        if (!fromPrefab) renderer.sprite = productSprites[productId];
        // 저해상도 import 크기와 무관하게 긴 변을 가판 기준 180픽셀로 표시합니다.
        Vector2 spriteSize = renderer.sprite.bounds.size;
        if (!fromPrefab) itemObject.transform.localScale = Vector3.one * (1.8f / Mathf.Max(spriteSize.x, spriteSize.y));
        renderer.sortingOrder = 10 + nextInstanceId;
        var body = itemObject.GetComponent<Rigidbody2D>();
        if (body == null) body = itemObject.AddComponent<Rigidbody2D>();
        if (!fromPrefab)
        {
            body.gravityScale = 0;
            body.linearDamping = itemFriction;
            body.angularDamping = rotationDamping * .2f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        var collider = itemObject.GetComponent<BoxCollider2D>();
        if (collider == null) collider = itemObject.AddComponent<BoxCollider2D>();
        Vector2 visibleSize = renderer.sprite != null ? renderer.sprite.bounds.size : Vector2.one;
        if (!fromPrefab) collider.size = new Vector2(visibleSize.x * .72f, visibleSize.y * .72f);
        var item = itemObject.GetComponent<DystopiaTopDownItem>();
        if (item == null) item = itemObject.AddComponent<DystopiaTopDownItem>();
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
        var filter = ContactFilter2D.noFilter;
        filter.useTriggers = Physics2D.queriesHitTriggers;
        int count = Physics2D.CircleCast(from, cursorRadius, distance > .0001f ? delta / distance : Vector2.right, filter, sweepHits, distance);
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
            if (item.State == TopDownItemState.Excluded || item.IsBeingVacuumed) continue;
            item.Body.linearVelocity = Vector2.ClampMagnitude(item.Body.linearVelocity, maximumItemSpeed);
            item.Body.angularVelocity = Mathf.Clamp(item.Body.angularVelocity, -720, 720);
            if (placedMovementZone != null) ClampPlacedItem(item);
            else
            {
                Vector2 position = item.transform.localPosition;
                item.transform.localPosition = new Vector3(Mathf.Clamp(position.x, -5.95f, 5.95f), Mathf.Clamp(position.y, -3.15f, 3.15f), 0);
            }
        }
    }

    /// <summary>현재 위치로 판매 후보를 갱신하고 영역 밖으로 나오면 미분류로 되돌립니다.</summary>
    private void ClassifySettledItems()
    {
        foreach (DystopiaTopDownItem item in items)
        {
            if (item == heldItem || item.State == TopDownItemState.Excluded || item.IsBeingVacuumed) continue;
            Vector2 center = item.transform.localPosition;
            if (item.WasStirred && ZoneContains(placedExcludedZone, ExcludedZone, center)) TryClassify(item, TopDownItemState.Excluded);
            else if (ZoneContains(placedSaleZone, SaleZone, center)) item.State = TopDownItemState.ForSale;
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

    /// <summary>계산기가 열린 동안 넘버패드 숫자를 버튼과 같은 금액 입력 경로로 전달합니다.</summary>
    private void ProcessNumberPad()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !calculatorOpen || Session.IsPaused) return;
        // 00/000 화면 버튼과 달리 넘버패드 0은 누를 때마다 한 자리만 입력합니다.
        if (keyboard.numpad0Key.wasPressedThisFrame) Digit("0");
        if (keyboard.numpad1Key.wasPressedThisFrame) Digit("1");
        if (keyboard.numpad2Key.wasPressedThisFrame) Digit("2");
        if (keyboard.numpad3Key.wasPressedThisFrame) Digit("3");
        if (keyboard.numpad4Key.wasPressedThisFrame) Digit("4");
        if (keyboard.numpad5Key.wasPressedThisFrame) Digit("5");
        if (keyboard.numpad6Key.wasPressedThisFrame) Digit("6");
        if (keyboard.numpad7Key.wasPressedThisFrame) Digit("7");
        if (keyboard.numpad8Key.wasPressedThisFrame) Digit("8");
        if (keyboard.numpad9Key.wasPressedThisFrame) Digit("9");
    }

    /// <summary>키패드의 한 자리 또는 00·000 입력 전체를 최대 7자리 안에서 추가합니다.</summary>
    private void Digit(string digit)
    {
        if (!CanEditAmount || amount.Length + digit.Length > 7) return;
        amount += digit;
        noticeText.text = "";
        RefreshUi();
    }

    /// <summary>입력한 금액의 마지막 한 자리만 지웁니다.</summary>
    private void Backspace()
    {
        if (!CanEditAmount || amount.Length == 0) return;
        amount = amount.Substring(0, amount.Length - 1);
        RefreshUi();
    }

    /// <summary>입력 금액 전체를 지웁니다.</summary>
    private void ClearAmount()
    {
        if (!CanEditAmount) return;
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
        if (state != ViewState.Sorting || isPaused) return false;
        ClassifySettledItems();
        foreach (DystopiaTopDownItem item in items)
        {
            if (item.State == TopDownItemState.Working || item.IsBeingVacuumed)
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
        ResetGrabInput();
        // 흡입 취소가 원래 물리를 복원한 뒤 전체 일시정지 상태를 적용합니다.
        SetBodiesSimulated(!paused && state != ViewState.Locked && state != ViewState.Closed);
        noticeText.text = paused ? "일시정지" : "";
    }

    /// <summary>현재 물품 Rigidbody2D의 시뮬레이션 여부를 일괄 적용합니다.</summary>
    private void SetBodiesSimulated(bool simulated)
    {
        foreach (DystopiaTopDownItem item in items) if (item != null && item.Body != null) item.Body.simulated = simulated;
    }

    /// <summary>뷰 전환·일시정지·포커스 복귀 때 이전 포인터 위치를 폐기합니다.</summary>
    private void ResetGrabInput()
    {
        if (vacuum != null) vacuum.Release();
        ReleaseHeldItem();
        hasHandPointer = false;
        handVelocity = Vector2.zero;
        SetHandVisible(false);
    }

    /// <summary>다음 손님 전에 이전 물품 객체와 입력 상태를 제거합니다.</summary>
    private void ClearItems()
    {
        if (vacuum != null) vacuum.Release();
        ReleaseHeldItem();
        foreach (DystopiaTopDownItem item in items) if (item != null) Destroy(item.gameObject);
        items.Clear();
        amount = "";
        noticeText.text = "";
    }

    /// <summary>청소기 손잡이·봉·상품 순서로 입력 소유자를 한 개만 결정합니다.</summary>
    private void ProcessGrabInput()
    {
        var mouse = Mouse.current;
        bool allowed = Session != null && state == ViewState.Sorting && !isPaused && !Session.IsPaused &&
            Application.isFocused && worldCamera != null && worldCamera.isActiveAndEnabled && mouse != null;
        Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        bool overUi = allowed && PointerOverUi(pointer);
        if (vacuum != null) vacuum.SampleInput(worldCamera,allowed,heldItem == null && !overUi && (dividerBar == null || !dividerBar.IsHeld));
        if (vacuum != null && vacuum.IsHeld)
        {
            ReleaseHeldItem();
            if (dividerBar != null) dividerBar.SampleInput(worldCamera,false);
            SetHandVisible(allowed && handArtwork != null);
            return;
        }
        if (!allowed || !mouse.leftButton.isPressed || overUi) ReleaseHeldItem();
        if (dividerBar != null) dividerBar.SampleInput(worldCamera, allowed && heldItem == null && (dividerBar.IsHeld || !overUi));
        SetHandVisible(allowed && !overUi && worldCamera.pixelRect.Contains(pointer) && handArtwork != null);
        if (!allowed || overUi) { hasHandPointer = false; return; }
        Vector2 velocity = hasHandPointer ? (pointer - previousHandPointer) / Mathf.Max(.001f,Time.unscaledDeltaTime) : Vector2.zero;
        handVelocity = Vector2.Lerp(handVelocity,velocity,1-Mathf.Exp(-20*Time.unscaledDeltaTime));
        previousHandPointer = pointer; hasHandPointer = true;
        heldTarget = worldCamera.ScreenToWorldPoint(pointer);
        if (heldItem != null || dividerBar != null && dividerBar.IsHeld || !mouse.leftButton.wasPressedThisFrame) return;
        // 나중에 생성되어 위에 그려진 상품부터 확인합니다. 빈 곳을 누른 채 지나가는 것으로 잡지 않습니다.
        for (int i = items.Count-1; i >= 0; i--)
        {
            var item = items[i];
            if (item == null || !item.gameObject.activeInHierarchy || item.IsBeingVacuumed || item.State == TopDownItemState.Excluded || !item.Body.simulated) continue;
            var collider = item.GetComponent<Collider2D>();
            if (collider == null || !collider.OverlapPoint(heldTarget)) continue;
            heldItem = item;
            heldLocalPoint = item.transform.InverseTransformPoint(heldTarget);
            heldBodyType = item.Body.bodyType;
            heldInterpolation = item.Body.interpolation;
            item.Body.bodyType = RigidbodyType2D.Kinematic;
            item.Body.interpolation = RigidbodyInterpolation2D.None;
            item.Body.linearVelocity = Vector2.zero;
            item.Body.angularVelocity = 0;
            item.WasStirred = true;
            item.State = TopDownItemState.Working;
            break;
        }
    }

    /// <summary>놓기·UI 진입·중단 시 물품의 잡기 제약만 해제합니다.</summary>
    private void ReleaseHeldItem()
    {
        if (heldItem != null && heldItem.Body != null)
        {
            heldItem.Body.bodyType = heldBodyType;
            heldItem.Body.interpolation = heldInterpolation;
            heldItem.Body.linearVelocity = Vector2.zero;
            heldItem.Body.angularVelocity = 0;
        }
        heldItem = null;
    }

    /// <summary>손 표시 중에만 OS 커서를 숨기고 이전 표시 상태를 복원합니다.</summary>
    /// <param name="visible">작업대에 손 커서를 표시할지 여부입니다.</param>
    private void SetHandVisible(bool visible)
    {
        showHand = visible;
        if (visible && !ownsCursorVisibility)
        {
            previousCursorVisible = Cursor.visible; ownsCursorVisibility = true; Cursor.visible = false;
        }
        else if (!visible && ownsCursorVisibility)
        {
            Cursor.visible = previousCursorVisible; ownsCursorVisibility = false;
        }
    }

    /// <summary>손바닥의 접점을 상품·막대의 실제 잡기 지점에 맞춰 세 가지 자세로 표시합니다.</summary>
    private void OnGUI()
    {
        if (!showHand || Mouse.current == null || handArtwork == null) return;
        bool holding = heldItem != null || dividerBar != null && dividerBar.IsHeld || vacuum != null && vacuum.IsHeld;
        bool horizontal = holding && Mathf.Abs(handVelocity.x) > 30 && Mathf.Abs(handVelocity.x) > Mathf.Abs(handVelocity.y)*1.2f;
        // 시트 순서: 1번을 90도 돌린 정지 자세, 2번 펼친 손, 3번 가로 잡기. 4번은 사용하지 않습니다.
        Rect uv = !holding ? new Rect(.5f,.5f,.5f,.5f) : horizontal ? new Rect(.5f,0,-.5f,.5f) : new Rect(0,.5f,.5f,.5f);
        Vector2 pointer = Mouse.current.position.ReadValue();
        // 커서와 물리 프레임 간 차이, 경계 제한, 막대 회전에도 손이 물체에서 떨어지지 않습니다.
        if (heldItem != null) pointer = worldCamera.WorldToScreenPoint(heldItem.transform.TransformPoint(heldLocalPoint));
        else if (dividerBar != null && dividerBar.IsHeld) pointer = worldCamera.WorldToScreenPoint(dividerBar.GripWorldPoint);
        else if (vacuum != null && vacuum.IsHeld) pointer = worldCamera.WorldToScreenPoint(vacuum.GripWorldPoint);
        Vector2 center = new Vector2(pointer.x,Screen.height-pointer.y);
        // 64px 타일의 투명 여백 중심이 아니라 실제 손바닥 접점을 사용합니다.
        Vector2 hotspot = !holding ? new Vector2(32,32) : horizontal ? new Vector2(30,23) : new Vector2(33,25);
        var previousMatrix = GUI.matrix;
        var previousColor = GUI.color;
        GUI.color = Color.white;
        float rotation = holding && !horizontal ? 90f : 0f;
        if (dividerBar != null && dividerBar.IsHeld) rotation += 90f - dividerBar.transform.eulerAngles.z;
        GUIUtility.RotateAroundPivot(rotation,center);
        GUI.DrawTextureWithTexCoords(new Rect(center.x-hotspot.x*handSizePixels/64f,center.y-hotspot.y*handSizePixels/64f,handSizePixels,handSizePixels),handArtwork,uv,true);
        GUI.matrix = previousMatrix; GUI.color = previousColor;
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
