using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>전용 Scene의 독립 Sprite 화면, 실제 버튼 입력 및 런 수명을 관리합니다.</summary>
public sealed partial class DystopiaScreen : MonoBehaviour
{
    /// <summary>얼굴·손 위주인 어린이 원화를 성인 상반신 영역에 표시할 때의 상대 크기입니다.</summary>
    private const float ChildPortraitScale = .4f;
    /// <summary>어린이 손의 하단이 가판 뒤로 살짝 겹치도록 기존 손님 기준점에서 올리는 상대 위치입니다.</summary>
    private Vector2 ChildPortraitOrigin => idleOrigins[2] + Vector2.up * (idlePeople[2].rect.height * placedPeopleScales[2].y * .18f);
    /// <summary>외형별 호흡 연출 분류입니다. 게임 능력이나 건강 판정에는 사용하지 않습니다.</summary>
    private enum BreathStyle { Normal, Heavy, Elderly }
    /// <summary>남성 Sprite 배열과 같은 순서의 호흡 분류입니다.</summary>
    [SerializeField] private BreathStyle[] maleBreathing = {
        BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Elderly, BreathStyle.Normal, BreathStyle.Heavy, BreathStyle.Normal,
        BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Heavy, BreathStyle.Heavy, BreathStyle.Elderly, BreathStyle.Normal,
        BreathStyle.Heavy, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Heavy, BreathStyle.Normal,
        BreathStyle.Heavy, BreathStyle.Elderly, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Heavy };
    /// <summary>여성 Sprite 배열과 같은 순서의 호흡 분류입니다.</summary>
    [SerializeField] private BreathStyle[] femaleBreathing = {
        BreathStyle.Normal, BreathStyle.Heavy, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Elderly, BreathStyle.Normal,
        BreathStyle.Elderly, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Heavy, BreathStyle.Normal,
        BreathStyle.Elderly, BreathStyle.Normal, BreathStyle.Normal, BreathStyle.Heavy };
    /// <summary>탑다운 계산기와 정면 시계에 공유하는 사용자 제공 UI 이미지입니다.</summary>
    [SerializeField] private Sprite calculatorArtwork, calculatorToggleArtwork, counterClockArtwork;
    public Sprite CalculatorArtwork => calculatorArtwork;
    public Sprite CalculatorToggleArtwork => calculatorToggleArtwork;
    public Sprite CounterClockArtwork => counterClockArtwork;
    /// <summary>1280×720 기준 좌상단 위치와 크기이며 실행 중에도 조절할 수 있습니다.</summary>
    [SerializeField] private Rect calculatorLayout = new Rect(900, 310, 360, 360);
    [SerializeField] private Rect calculatorToggleLayout = new Rect(1214, 659, 54, 58);
    public Rect CalculatorLayout => calculatorLayout;
    public Rect CalculatorToggleLayout => calculatorToggleLayout;
    /// <summary>정면 가판 시계의 좌상단 위치와 크기입니다. 1280×720 기준이며 실행 중에도 반영됩니다.</summary>
    [SerializeField] private Rect counterClockLayout = new Rect(1090, 380, 180, 180);
    public Rect CounterClockLayout => counterClockLayout;
    /// <summary>이 Scene에만 저장되는 조정 가능한 초기값입니다.</summary>
    [SerializeField] private DystopiaSettings settings = new DystopiaSettings();
    /// <summary>프로젝트 안에 저장된 배경, 가판, 딸, 감독관 참조입니다.</summary>
    [SerializeField] private Sprite background, counter, daughter, inspector;
    /// <summary>감독관 팝업 전용 머리·몸통 관절과 노멀 조명 프리팹입니다.</summary>
    [SerializeField] private GameObject inspectorPortraitPrefab;
    /// <summary>배급소 원본의 투명 여백을 유지하는 중경·군중·감시탑·가림막 레이어입니다.</summary>
    [SerializeField] private Sprite midBackground, leftWatchTower, rightWatchTower, canopy;
    /// <summary>원본 군중에서 분리한 뒤·중간·앞줄 Sprite입니다.</summary>
    [SerializeField] private Sprite[] crowdRows;
    /// <summary>두 감시탑 초소 안에서 좌우를 살피는 경비병 Sprite입니다.</summary>
    [SerializeField] private Sprite watchGuard;
    /// <summary>1280x720 화면 좌상단 기준 왼쪽 경비병의 X, Y, Width, Height입니다.</summary>
    [Header("감시탑 군인 배치 (1280x720 좌상단 기준)")]
    [SerializeField] private UnityEngine.Rect leftWatchGuardRect = new UnityEngine.Rect(141,142,44,34);
    /// <summary>1280x720 화면 좌상단 기준 오른쪽 경비병의 X, Y, Width, Height입니다.</summary>
    [SerializeField] private UnityEngine.Rect rightWatchGuardRect = new UnityEngine.Rect(1154,207,32,24);
    /// <summary>하루 정산의 방·책상·펼친 공책이 포함된 사용자 제공 배경입니다.</summary>
    [SerializeField] private Sprite dailyLedger;
    /// <summary>공책 인쇄 영역의 가로·세로 여백입니다. 334×188 배경 원본 픽셀 단위로 Inspector에서 조절합니다.</summary>
    [SerializeField] private Vector2 ledgerPageInset = new Vector2(4,3);
    /// <summary>정산 방의 왼쪽 의자에 앉아 공책을 바라보는 사용자 제공 딸 이미지입니다.</summary>
    [SerializeField] private Sprite ledgerDaughter;
    /// <summary>정산 화면에서 딸의 머리 위에 표시하는 9-slice 말풍선입니다.</summary>
    [SerializeField] private Sprite ledgerSpeechBubble;
    /// <summary>신뢰 등급의 기존 '참 잘했어요!' 도장입니다.</summary>
    [SerializeField] private Texture2D ledgerStamp;
    /// <summary>호평 '잘했어요!', 보통 '힘내요!', 악평 '소문이 안 좋아요...', 악명 '아무도 안 믿어요...' 도장입니다.</summary>
    [SerializeField] private Texture2D ledgerStampPopular, ledgerStampNeutral, ledgerStampUnpopular, ledgerStampNotorious;
    /// <summary>정산화면 좌상단 기준 도장 부모 영역입니다. 기존 그림 배치값을 유지합니다.</summary>
    [SerializeField] private Rect ledgerDrawingLayout = new Rect(1035,24,190,238);
    /// <summary>그림 좌상단 기준 도장의 위치와 크기입니다. 기본 배치는 그림 중앙입니다.</summary>
    [SerializeField] private Rect ledgerStampLayout = new Rect(27,51,136,136);
    /// <summary>종이 위에 찍힌 도장의 불투명도입니다.</summary>
    [SerializeField, Range(0,1)] private float ledgerStampOpacity = 1f;
    /// <summary>어두운 정산방에 맞추는 도장 잉크 색 배율입니다. 불투명도는 별도 설정을 사용합니다.</summary>
    [SerializeField] private Color ledgerStampTint = new Color(.5f,.65f,.6f);
    /// <summary>딸 앞을 가리는 공책의 원본 영역입니다. 화면 수명 동안 재사용하고 파괴 시 해제합니다.</summary>
    private Sprite ledgerForeground;
    /// <summary>영업 전 가격과 당일 규칙을 표시하는 사용자 제공 일일지침 Sprite입니다.</summary>
    [SerializeField] private Sprite dailyInstruction;
    /// <summary>화면에서 생성하는 모든 한글 UI에 사용하는 사용자 제공 물마루 폰트입니다.</summary>
    [SerializeField] private Font uiFont;
    /// <summary>군인 전체와 각 감시탑 다리 영역만 무채색으로 표시하는 머티리얼입니다.</summary>
    [SerializeField] private Material guardTone, leftTowerTone, rightTowerTone;
    /// <summary>사용자가 제공한 굴뚝 연기의 1~4번 순서입니다.</summary>
    [SerializeField] private Sprite[] chimneySmokeFrames;
    /// <summary>군중과 응대 손님 사이를 막는 전경 바리케이드 원본입니다.</summary>
    [SerializeField] private Sprite barricade;
    /// <summary>원본 PNG를 보존한 후경·중경·전경 안개입니다.</summary>
    [SerializeField] private Sprite fogBack, fogMid, fogFront;
    /// <summary>uGUI용 개별 설정을 저장한 공유 Fog Material이며 런타임 복제하지 않습니다.</summary>
    [SerializeField] private Material fogBackMaterial, fogMidMaterial, fogFrontMaterial;
    /// <summary>기존 Scene 참조를 보존하는 이전 손님 Sprite 배열입니다.</summary>
    [SerializeField] private Sprite[] customers = new Sprite[1];
    /// <summary>성인·어린이·노인을 포함한 남성 29종과 여성 20종의 손님 Sprite 묶음입니다.</summary>
    [SerializeField] private Sprite[] maleCustomers, femaleCustomers;
    private Font font;
    private bool ownsRuntimeFont;
    private RectTransform root, modal, basketRoot;
    private Text inputText, dialogue, reasonText, confirmButtonText;
    private Image portrait;
    /// <summary>손님 얼굴 오른쪽에 배치된 거래 결과 표정입니다. 결과 단계 외에는 숨깁니다.</summary>
    [SerializeField] private Image tradeReactionImage;
    /// <summary>만족, 대만족, 불만족 승낙, 거절 순서의 사용자 제공 표정입니다.</summary>
    [SerializeField] private Sprite[] tradeReactionSprites;
    /// <summary>네 표정의 원본 시트입니다. 개별 Sprite 연결이 없는 씬에서만 분리합니다.</summary>
    [SerializeField] private Texture2D tradeReactionSheet;
    /// <summary>이 화면이 생성하여 파괴 시 해제하는 표정 Sprite입니다.</summary>
    private Sprite[] ownedTradeReactionSprites;
    /// <summary>동일 결과를 일시정지 갱신 등으로 재생하지 않도록 기억합니다.</summary>
    private DystopiaCustomer reactionCustomer;
    /// <summary>현재 표정 연출과 연출 시작 전 사용자가 배치한 상태입니다.</summary>
    private Coroutine reactionAnimation;
    private Vector2 reactionOrigin;
    private Vector3 reactionScale;
    private Color reactionColor;
    // 원경의 두 굴뚝에만 사용하는 연기 이미지와 기준 위치입니다.
    private readonly Image[] chimneySmoke = new Image[2];
    private readonly Vector2[] chimneySmokeOrigins = new Vector2[2];
    private Image[] waiting = new Image[2];
    // 씬 수명 동안 대기 손님 둘·응대 손님의 기준 위치를 보존합니다.
    private readonly RectTransform[] idlePeople = new RectTransform[3];
    // 거래 대상이 바뀌었을 때만 대기 줄을 한 칸 전진시킵니다.
    private DystopiaCustomer displayedCustomer;
    private Coroutine queueMovement;
    private readonly Vector2[] idleOrigins = new Vector2[3];
    // 숨쉬기와 줄 이동 애니메이션은 편집된 원래 크기에 상대적으로 적용합니다.
    private readonly Vector3[] placedPeopleScales = { Vector3.one, Vector3.one, Vector3.one };
    // 일시정지 중에는 누적하지 않는 시각 연출 전용 경과 시간(초)입니다.
    private float idleSeconds;
    /// <summary>씬 수명 동안 줄별·인물 위치별 움직임을 갱신할 군중 이미지입니다.</summary>
    private readonly DystopiaCrowdImage[] animatedCrowd = new DystopiaCrowdImage[3];
    // 두 경비병은 같은 그림을 서로 다른 크기와 위상으로 사용합니다.
    private readonly RectTransform[] watchGuards = new RectTransform[2];
    private readonly Vector2[] watchGuardOrigins = new Vector2[2];
    // 경비병별 총구 불꽃과 시각 연출 시간(초)이며 거래 난수·판정과 분리합니다.
    private readonly RectTransform[] watchMuzzleFlashes = new RectTransform[2];
    private readonly float[] nextGuardShot = { 3f, 6f };
    private readonly float[] guardFlashUntil = new float[2];
    private Button confirmButton, ledgerConfirmButton, dailyInstructionButton;
    private string amount = "";
    // 잘못된 부호/소수 입력을 정수 금액으로 오인하여 결제하지 않게 합니다.
    private bool invalidAmount;
    private int drawnRevision = -1;
    private float alertUntil;
    // 같은 손님은 결과·일시정지 갱신에도 물건 위치를 유지합니다. 거래 난수와 분리합니다.
    private DystopiaCustomer laidOutCustomer;
    private int basketLayoutSeed;
    // 확인 처리된 날짜와 재표시 여부를 분리해 확인 연타 및 재정산을 막습니다.
    private int confirmedLedgerDay = -1;
    private bool isLedgerReopened;
    // 영업 중 지침 재열람은 거래 상태를 바꾸지 않고 화면 입력과 대기 시간만 잠시 막습니다.
    private bool isDailyInstructionOpen;
    public DystopiaSession Session { get; private set; }
    public DystopiaSettings Settings => settings;

#if UNITY_EDITOR
    /// <summary>열려 있던 전용 씬에도 신규 연기 참조를 연결하며 기존 연결은 보존합니다.</summary>
    private void BindEditorSmokeFrames()
    {
        if (gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity") return;
        if (chimneySmokeFrames != null && chimneySmokeFrames.Length == 4) return;
        var frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/ChimneySmoke" + i + ".png");
            if (frames[i] == null) return;
        }
        chimneySmokeFrames = frames;
    }

    /// <summary>기존 연결은 보존하고 에디터 실행에서 추가된 남녀 손님 외형 참조를 보완합니다.</summary>
    private void BindEditorCustomers()
    {
        if (maleCustomers == null || maleCustomers.Length != DystopiaSession.MaleAppearanceCount)
            Array.Resize(ref maleCustomers, DystopiaSession.MaleAppearanceCount);
        for (int i = 0; i < maleCustomers.Length; i++)
            if (maleCustomers[i] == null) maleCustomers[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/DystopiaPrototype/Art/Customers/MaleCustomer_{i + 1:00}.png");
        if (femaleCustomers == null || femaleCustomers.Length != DystopiaSession.FemaleAppearanceCount)
            Array.Resize(ref femaleCustomers, DystopiaSession.FemaleAppearanceCount);
        for (int i = 0; i < femaleCustomers.Length; i++)
            if (femaleCustomers[i] == null) femaleCustomers[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/DystopiaPrototype/Art/Customers/FemaleCustomer_{i + 1:00}.png");
    }
#endif

    /// <summary>Windows PC 프로토타입의 설치된 한글 폰트와 UI를 구성합니다.</summary>
    private void Awake()
    {
        if (Session != null) return;
#if UNITY_EDITOR
        BindEditorSmokeFrames();
        if (tradeReactionSheet == null) tradeReactionSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/TradeReactions.png");
        // 이미 열려 있던 씬의 신규 참조만 보완하며 Inspector의 기존 연결은 유지합니다.
        if (guardTone == null) guardTone = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/DystopiaPrototype/Art/GuardNeutral.mat");
        if (leftTowerTone == null) leftTowerTone = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/DystopiaPrototype/Art/LeftTowerNeutral.mat");
        if (rightTowerTone == null) rightTowerTone = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/DystopiaPrototype/Art/RightTowerNeutral.mat");
        if (dailyLedger == null) dailyLedger = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/DailyLedger.png");
        if (ledgerDaughter == null) ledgerDaughter = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/LedgerDaughter.png");
        if (ledgerSpeechBubble == null) ledgerSpeechBubble = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/LedgerSpeechBubble.png");
        if (ledgerStamp == null) ledgerStamp = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/LedgerStamp.png");
        if (ledgerStampPopular == null) ledgerStampPopular = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/LedgerStampPopular.png");
        if (ledgerStampNeutral == null) ledgerStampNeutral = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/LedgerStampNeutral.png");
        if (ledgerStampUnpopular == null) ledgerStampUnpopular = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/LedgerStampUnpopular.png");
        if (ledgerStampNotorious == null) ledgerStampNotorious = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/LedgerStampNotorious.png");
        if (dailyInstruction == null) dailyInstruction = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/DailyInstruction.png");
        BindEditorCustomers();
        if (inspectorPortraitPrefab == null) inspectorPortraitPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DystopiaPrototype/Prefabs/InspectorPortrait.prefab");
        if (uiFont == null) uiFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/DystopiaPrototype/Art/Mulmaru.otf");
#endif
        font = uiFont;
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 24);
            ownsRuntimeFont = true;
            Debug.LogError("물마루 UI Font 참조가 누락되어 임시 시스템 폰트를 사용합니다.", this);
        }
        if (font == null) throw new InvalidOperationException("UI에 사용할 한글 폰트가 필요합니다.");
        BuildScreen();
        PrepareTradeReaction();
        Restart();
    }

    /// <summary>키보드와 실제 버튼은 동일한 입력 경로를 사용합니다.</summary>
    private void Update()
    {
        if (Session == null) RestoreAfterReload();
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (isDailyInstructionOpen) CloseDailyInstruction();
                else Pause();
            }
            if (Session.Phase == DystopiaPhase.Trading && !Session.IsPaused && !isDailyInstructionOpen)
            {
                if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame ||
                    keyboard.periodKey.wasPressedThisFrame || keyboard.numpadPeriodKey.wasPressedThisFrame)
                {
                    invalidAmount = true;
                    ShowAmount();
                }
                for (int i = 0; i < 10; i++)
                {
                    Key top = i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1);
                    Key pad = Key.Numpad0 + i;
                    if (keyboard[top].wasPressedThisFrame || keyboard[pad].wasPressedThisFrame) Digit(i.ToString());
                }
                if (keyboard.backspaceKey.wasPressedThisFrame) Erase();
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Confirm();
            }
        }
        if (!isDailyInstructionOpen) Session.Tick(Time.unscaledDeltaTime);
        AnimatePeople();
        if (drawnRevision != Session.Revision) Refresh();
        if (Session.Phase == DystopiaPhase.Trading && !isDailyInstructionOpen && Time.unscaledTime > alertUntil) reasonText.text = "";
    }

    /// <summary>재컴파일로 세션이 소실된 경우 이 화면이 생성한 UI만 정리하고 새 실행을 구성합니다.</summary>
    public void RestoreAfterReload()
    {
        if (Session != null) return;
        StopAllCoroutines();
        queueMovement = null;
        displayedCustomer = null;
        // 저장된 배치는 보존하고 동적인 장바구니만 다시 구성합니다.
        var savedBasket = transform.Find("DystopiaCanvas/Basket");
        if (savedBasket != null) ClearChildren(savedBasket);
        if (ownsRuntimeFont && font != null) Destroy(font);
        ownsRuntimeFont = false;
        Awake();
        Debug.LogWarning("스크립트 재컴파일로 세션이 소실되어 새 영업을 시작했습니다.", this);
    }

    /// <summary>런타임에 생성한 폰트와 Sprite의 수명을 끝냅니다.</summary>
    private void OnDestroy()
    {
        if (ownsRuntimeFont && font != null) Destroy(font);
        if (ledgerForeground != null) Destroy(ledgerForeground);
        if (ownedTradeReactionSprites != null)
            foreach (var sprite in ownedTradeReactionSprites) Destroy(sprite);
    }

    /// <summary>군중·손님의 대기 동작과 경비병의 좌우 경계·간헐적인 외곽 사격을 갱신합니다.</summary>
    internal void AnimatePeople()
    {
        if (Session.IsPaused) return;
        idleSeconds += Time.unscaledDeltaTime;
        for (int i = 0; i < chimneySmoke.Length; i++)
        {
            if (chimneySmoke[i] == null) continue;
            float smokeTime = idleSeconds / (1.1f + i * .13f) + i * 1.7f;
            int frame = Mathf.FloorToInt(smokeTime) % chimneySmokeFrames.Length;
            chimneySmoke[i].sprite = chimneySmokeFrames[frame];
            chimneySmoke[i].rectTransform.anchoredPosition = chimneySmokeOrigins[i] +
                new Vector2(Mathf.Sin(idleSeconds * .28f + i) * 2, Mathf.Sin(idleSeconds * .4f + i) * 1.5f);
        }
        foreach (var row in animatedCrowd) row.Animate(idleSeconds);
        for (int i = 0; i < watchGuards.Length; i++)
        {
            float turnPhase = idleSeconds * (.12f + i * .012f) + i * 1.7f;
            float turn = Mathf.Sin(turnPhase);
            float facing = Mathf.Sign(turn) * Mathf.Lerp(.12f, 1, Mathf.SmoothStep(0, 1, Mathf.Abs(turn)));
            float outward = i == 0 ? -1 : 1;
            // 화면 중앙의 가판 쪽으로는 쏘지 않고, 총이 외곽을 향했을 때만 발사합니다.
            if (idleSeconds >= nextGuardShot[i] && facing * outward > .9f)
            {
                guardFlashUntil[i] = idleSeconds + .12f;
                nextGuardShot[i] = idleSeconds + 9f + i * 3.7f;
            }
            bool firing = idleSeconds < guardFlashUntil[i];
            watchGuards[i].localScale = new Vector3(facing, 1, 1);
            // 회전 중에만 낮은 진폭으로 체중을 옮기고, 좌우 끝을 주시할 때는 잦아듭니다.
            float stepping = Mathf.Abs(Mathf.Cos(turnPhase));
            float stepBob = Mathf.Sin(idleSeconds * 2.6f + i * 1.3f) * .65f * stepping;
            watchGuards[i].anchoredPosition = watchGuardOrigins[i] + new Vector2(turn * 2 - (firing ? outward : 0), stepBob);
            // Sprite의 총구 위치를 따라가며 총구 앞에서만 짧게 점멸합니다.
            watchMuzzleFlashes[i].anchoredPosition = watchGuards[i].anchoredPosition +
                new Vector2(facing * (watchGuards[i].sizeDelta.x * .5f + 3), -watchGuards[i].sizeDelta.y * .35f);
            watchMuzzleFlashes[i].localScale = new Vector3(outward, 1, 1);
            watchMuzzleFlashes[i].gameObject.SetActive(firing);
        }
        for (int i = 0; i < idlePeople.Length; i++)
        {
            if (queueMovement != null) continue;
            var sprite = idlePeople[i].GetComponent<Image>().sprite;
            int maleIndex = Array.IndexOf(maleCustomers, sprite);
            int femaleIndex = Array.IndexOf(femaleCustomers, sprite);
            BreathStyle style = maleIndex >= 0 && maleIndex < maleBreathing.Length ? maleBreathing[maleIndex] :
                femaleIndex >= 0 && femaleIndex < femaleBreathing.Length ? femaleBreathing[femaleIndex] : BreathStyle.Normal;
            float seed = (maleIndex >= 0 ? maleIndex + 1 : femaleIndex + 31) * .618f + i * .37f;
            float variation = Mathf.Repeat(seed, 1);
            float period = (style == BreathStyle.Heavy ? 1.75f : style == BreathStyle.Elderly ? 3.7f : 2.9f) * Mathf.Lerp(.88f, 1.12f, variation);
            float cycle = Mathf.Repeat(idleSeconds / period + seed, 1);
            float inhale = style == BreathStyle.Elderly ? .25f : .38f;
            float breath = Mathf.SmoothStep(0, 1, cycle < inhale ? cycle / inhale : 1 - (cycle - inhale) / (1 - inhale));
            // 노년형은 짧게 들이쉬고 길게 내려앉으며 호기 중 작은 떨림을 더합니다.
            if (style == BreathStyle.Elderly) breath = Mathf.Clamp01(breath + Mathf.Sin(idleSeconds * 8f + seed) * .045f * (1 - breath));
            float depth = style == BreathStyle.Elderly ? .035f : style == BreathStyle.Heavy ? .028f : .018f;
            float width = style == BreathStyle.Heavy ? .022f : .007f;
            float relativeSize = idlePeople[i].rect.height / 550f;
            float sway = Mathf.Sin(idleSeconds / period * 2.1f + seed) * (style == BreathStyle.Elderly ? .7f : .3f);
            bool isChild = i == 2 && Session.Customer.Type == DystopiaCustomerType.Child;
            float portraitScale = isChild ? ChildPortraitScale : 1;
            idlePeople[i].anchoredPosition = (isChild ? ChildPortraitOrigin : idleOrigins[i]) + new Vector2(sway, (breath - .5f) * (style == BreathStyle.Elderly ? 5f : 3f) * relativeSize) * portraitScale;
            idlePeople[i].localScale = Vector3.Scale(placedPeopleScales[i], new Vector3((1 + (breath - .5f) * width) * portraitScale, (1 - (1 - breath) * depth) * portraitScale, 1));
        }
    }

    /// <summary>기존 Scene을 참조하지 않고 모든 런 상태를 새로 시작합니다.</summary>
    public void Restart()
    {
        Session = new DystopiaSession(settings, Environment.TickCount);
        amount = "";
        invalidAmount = false;
        drawnRevision = -1;
        confirmedLedgerDay = -1;
        isLedgerReopened = false;
        isDailyInstructionOpen = false;
        Refresh();
    }

    /// <summary>최대 일곱 자리 양의 정수 입력만 추가합니다.</summary>
    /// <param name="digit">한 자리 숫자 문자열.</param>
    public void Digit(string digit)
    {
        if (Session.Phase != DystopiaPhase.Trading || Session.IsPaused || isDailyInstructionOpen || invalidAmount || digit.Length != 1 || digit[0] < '0' || digit[0] > '9' || amount.Length >= 7) return;
        if (amount == "0") amount = "";
        amount += digit;
        alertUntil = 0;
        ShowAmount();
    }

    /// <summary>마지막 한 자리를 삭제합니다.</summary>
    public void Erase()
    {
        if (Session.Phase != DystopiaPhase.Trading || Session.IsPaused || isDailyInstructionOpen) return;
        if (invalidAmount) { invalidAmount = false; amount = ""; ShowAmount(); return; }
        if (amount.Length > 0) amount = amount.Substring(0, amount.Length - 1);
        ShowAmount();
    }

    /// <summary>표시된 금액을 확정하고 결과 동안 추가 입력을 차단합니다.</summary>
    public void Confirm()
    {
        if (isDailyInstructionOpen || invalidAmount && Session.Customer.total > 0) return;
        if (Session.Confirm(amount)) { amount = ""; invalidAmount = false; Refresh(); }
        else if (Session.Phase == DystopiaPhase.Trading && !Session.IsPaused)
        {
            reasonText.text = "판매할 금액을 1원 이상 입력하세요.";
            alertUntil = Time.unscaledTime + 2;
        }
    }

    /// <summary>런의 시간과 입력을 함께 멈추거나 재개합니다.</summary>
    public void Pause()
    {
        if (isDailyInstructionOpen) return;
        Session.TogglePause();
        Refresh();
    }

    /// <summary>Scene에 저장된 정면 화면을 연결하며 기본 배치를 생성하지 않습니다.</summary>
    private void BuildScreen()
    {
        if (!BindPlacedScreen()) throw new InvalidOperationException("Scene에 배치된 DystopiaCanvas가 필요합니다.");
    }

    /// <summary>누락된 표정 연결만 생성하며 이미 배치된 Image의 위치와 크기는 보존합니다.</summary>
    private void PrepareTradeReaction()
    {
        if (tradeReactionSprites == null || tradeReactionSprites.Length != 4 || Array.Exists(tradeReactionSprites, sprite => sprite == null))
        {
            if (tradeReactionSheet == null)
            {
                Debug.LogError("거래 표정 시트 참조가 필요합니다.", this);
                return;
            }
            ownedTradeReactionSprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                // 자동 슬라이싱이 눈·입을 별도 Sprite로 나누므로 표정 전체 영역을 사용합니다.
                var region = new Rect(i % 2 == 0 ? 150 : 643, i < 2 ? 655 : 168, 462, 462);
                ownedTradeReactionSprites[i] = Sprite.Create(tradeReactionSheet, region, new Vector2(.5f,.5f), 100, 0, SpriteMeshType.FullRect);
            }
            tradeReactionSprites = ownedTradeReactionSprites;
        }
        if (tradeReactionImage == null)
        {
            var existing = portrait.transform.Find("TradeReaction");
            if (existing != null) tradeReactionImage = existing.GetComponent<Image>();
            if (tradeReactionImage == null)
            {
                var go = new GameObject("TradeReaction", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(portrait.transform, false);
                tradeReactionImage = go.GetComponent<Image>();
                var rect = tradeReactionImage.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.8f,.78f);
                rect.sizeDelta = new Vector2(56,56);
                tradeReactionImage.preserveAspect = true;
                tradeReactionImage.raycastTarget = false;
            }
        }
        tradeReactionImage.gameObject.SetActive(false);
    }

    /// <summary>결과마다 한 번 표정을 띄우며 다음 손님과 취소에서는 연출을 정리합니다.</summary>
    private void RefreshTradeReaction()
    {
        if (tradeReactionImage == null || tradeReactionSprites == null || tradeReactionSprites.Length != 4 || Array.Exists(tradeReactionSprites, sprite => sprite == null)) return;
        bool visible = Session.Phase == DystopiaPhase.Result && Session.LastReaction != DystopiaSession.TradeReaction.None;
        if (!visible)
        {
            if (reactionAnimation != null)
            {
                StopCoroutine(reactionAnimation);
                ResetTradeReaction();
            }
            tradeReactionImage.gameObject.SetActive(false);
            reactionCustomer = null;
            return;
        }
        if (ReferenceEquals(reactionCustomer, Session.Customer)) return;
        reactionCustomer = Session.Customer;
        tradeReactionImage.sprite = tradeReactionSprites[(int)Session.LastReaction - 1];
        reactionOrigin = tradeReactionImage.rectTransform.anchoredPosition;
        reactionScale = tradeReactionImage.rectTransform.localScale;
        reactionColor = tradeReactionImage.color;
        tradeReactionImage.gameObject.SetActive(true);
        reactionAnimation = StartCoroutine(AnimateTradeReaction());
    }

    /// <summary>얼굴 옆 표정을 짧게 확대하고 위로 띄우며 사라지게 합니다. 거래 진행 시간은 바꾸지 않습니다.</summary>
    /// <returns>일시정지 시 함께 멈추는 프레임 연출입니다.</returns>
    private System.Collections.IEnumerator AnimateTradeReaction()
    {
        float duration = Mathf.Max(.01f, Mathf.Min(.75f, settings.resultSeconds));
        float elapsed = 0;
        var rect = tradeReactionImage.rectTransform;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float pop = t < .2f ? Mathf.Lerp(.55f,1.15f,t / .2f) : Mathf.Lerp(1.15f,1,Mathf.Clamp01((t - .2f) / .2f));
            rect.localScale = reactionScale * pop;
            rect.anchoredPosition = reactionOrigin + Vector2.up * (40 * Mathf.SmoothStep(0,1,t));
            var color = reactionColor;
            color.a *= 1 - Mathf.Clamp01((t - .55f) / .45f);
            tradeReactionImage.color = color;
            yield return null;
            if (!Session.IsPaused) elapsed += Time.unscaledDeltaTime;
        }
        ResetTradeReaction();
    }

    /// <summary>연출에서 바꾼 값만 원래대로 돌려놓아 사용자의 배치를 보존합니다.</summary>
    private void ResetTradeReaction()
    {
        tradeReactionImage.gameObject.SetActive(false);
        tradeReactionImage.rectTransform.anchoredPosition = reactionOrigin;
        tradeReactionImage.rectTransform.localScale = reactionScale;
        tradeReactionImage.color = reactionColor;
        reactionAnimation = null;
    }

    /// <summary>상태 전환 때만 상품·대화·안내창을 갱신합니다.</summary>
    private void Refresh()
    {
        bool advanceQueue = (displayedCustomer != null || Session.Customer.Type == DystopiaCustomerType.Child) && !ReferenceEquals(displayedCustomer, Session.Customer)
            && Session.Phase == DystopiaPhase.Trading;
        displayedCustomer = Session.Customer;
        drawnRevision = Session.Revision;
        confirmButton.interactable = Session.Phase == DystopiaPhase.Trading && !Session.IsPaused && !isDailyInstructionOpen;
        dailyInstructionButton.interactable = Session.Phase == DystopiaPhase.Trading && !Session.IsPaused && !isDailyInstructionOpen;
        confirmButtonText.text = Session.Phase == DystopiaPhase.Trading && Session.Customer.total == 0 ? "거래 취소  ↵" : "판매 확정  ↵";
        ShowAmount();
        portrait.sprite = CustomerPoolSprite(Session.Customer.IsMale,Session.Customer.appearance);
        // 첫 표시와 결과 갱신에서도 성인 크기의 얼굴이 한 프레임 노출되지 않게 합니다.
        if (Session.Customer.Type == DystopiaCustomerType.Child && queueMovement == null)
        {
            portrait.rectTransform.anchoredPosition = ChildPortraitOrigin;
            portrait.rectTransform.localScale = Vector3.Scale(placedPeopleScales[2], new Vector3(ChildPortraitScale, ChildPortraitScale, 1));
        }
        portrait.color = Session.Phase == DystopiaPhase.Result && !Session.LastAccepted && !Session.LastCancelled ? new Color(.8f,.6f,.6f) : Color.white;
        RefreshTradeReaction();
        for (int i=0;i<waiting.Length;i++)
        {
            waiting[i].gameObject.SetActive(Session.WaitingCustomers.Count > i && Session.WaitingCustomers[i].Type != DystopiaCustomerType.Child);
            if (Session.WaitingCustomers.Count > i)
            {
                var next = Session.WaitingCustomers[i];
                waiting[i].sprite = CustomerPoolSprite(next.IsMale, next.appearance);
            }
        }
        if (advanceQueue)
        {
            if (queueMovement != null) StopCoroutine(queueMovement);
            queueMovement = StartCoroutine(AdvanceQueueVisual());
        }
        dialogue.text = Session.Phase == DystopiaPhase.Result ? Session.Feedback : Session.Customer.isPoor ?
            "집에 아이가 기다리고 있어요. 가진 돈이 얼마 없어요…" : "이 물건들로 주세요. 얼마나 드리면 될까요?";
        if (Session.Phase == DystopiaPhase.Result || Session.Departed > 0)
        {
            reasonText.text = Session.Reason;
            alertUntil = Time.unscaledTime + 3;
        }
        ClearChildren(basketRoot);
        if (laidOutCustomer != Session.Customer)
        {
            laidOutCustomer = Session.Customer;
            basketLayoutSeed = Environment.TickCount;
        }
        var layoutRandom = new System.Random(basketLayoutSeed);
        // 상품 아래쪽을 상판에 두되 위치·기울기를 흩뜨려 일부가 자연스럽게 겹치게 합니다.
        int unitCount = 0;
        foreach (var line in Session.Customer.basket) unitCount += line.quantity;
        int[] slots = new int[unitCount];
        var placedItems = new RectTransform[unitCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = i;
        for (int i = slots.Length - 1; i > 0; i--)
        {
            int other = layoutRandom.Next(i + 1);
            int slot = slots[i]; slots[i] = slots[other]; slots[other] = slot;
        }
        int placed = 0;
        for (int i=0;i<Session.Customer.basket.Count;i++)
        {
            var line = Session.Customer.basket[i];
            for (int unit = 0; unit < line.quantity; unit++)
            {
                int slot = slots[placed++];
                int columns = Math.Min(5, unitCount - slot / 5 * 5);
                float x = 317.5f + (slot % 5 - (columns - 1) * .5f) * 92 + layoutRandom.Next(-22,23);
                float y = layoutRandom.Next(74,127);
                float angle = layoutRandom.Next(6,19) * (layoutRandom.Next(2) == 0 ? -1 : 1);
                Vector2 size = line.product.sprite.rect.size;
                size *= Mathf.Min(192 / size.x, 216 / size.y);
                if (ReferenceEquals(line.product, settings.products[3])) size *= 1.35f;
                // 회전한 아래쪽 모서리도 상판 앞 테두리를 넘지 않게 합니다.
                y = Mathf.Min(y, 130 - size.x * .5f * Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)));
                var item = Picture(basketRoot,"Product"+i+"Unit"+unit,line.product.sprite,0,0,size.x,size.y,true);
                item.rectTransform.pivot = new Vector2(.5f,0);
                item.rectTransform.anchoredPosition = new Vector2(x,-y);
                item.rectTransform.localRotation = Quaternion.Euler(0,0,angle);
                int lineIndex = i;
                int unitIndex = unit;
                item.raycastTarget = true;
                var selection = item.gameObject.AddComponent<Button>();
                selection.targetGraphic = item;
                selection.navigation = new Navigation { mode = Navigation.Mode.None };
                selection.onClick.AddListener(() => ToggleBasketUnit(lineIndex, unitIndex));
                if (!line.IsIncluded(unit))
                {
                    item.color = new Color(.34f,.34f,.34f,.48f);
                    var badge = Panel(item.transform,"ExcludedBadge",2,Mathf.Max(2,size.y-22),Mathf.Max(36,size.x-4),20,new Color(.08f,.09f,.09f,.88f));
                    var excluded = Label(badge.transform,"ExcludedLabel","제외",0,0,badge.rectTransform.sizeDelta.x,20,12,new Color(.88f,.88f,.84f));
                    excluded.alignment = TextAnchor.MiddleCenter;
                }
                placedItems[slot] = item.rectTransform;
            }
        }
        // 실제 접지점이 가까운 상품을 나중에 그려 겹침의 앞뒤 관계를 맞춥니다.
        Array.Sort(placedItems, (a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        foreach (var item in placedItems) item.SetAsLastSibling();
        if (modal != null) { modal.gameObject.SetActive(false); }
        modal = null;
        if (Session.IsPaused)
        {
            Modal("일시정지","시간과 대기열이 멈췄습니다.",false);
            MakeButton(modal,"Resume","계속하기",190,320,360,48,Pause,21);
            MakeButton(modal,"Restart","처음부터",190,380,360,44,Restart,18);
            return;
        }
        if (isDailyInstructionOpen)
        {
            DailyInstruction(false);
            return;
        }
        switch (Session.Phase)
        {
            case DystopiaPhase.PriceGuide:
                DailyInstruction(true);
                break;
            case DystopiaPhase.Tribute:
                Modal("감독관의 방문",$"“물품대금, 자리값, 내 몫. 잊지는 않았겠지.”\n오늘 상납금  {Session.TributeAmount:N0}원",true);
                Label(modal,"TributeCash",$"보유 현금  {Session.Cash:N0}원\n상납 처리 후 시민권을 구매할 수 있습니다.",40,225,480,70,21);
                MakeButton(modal,"PayTribute","상납금 정산",80,420,400,48,()=>{Session.PayTribute();Refresh();},21);
                break;
            case DystopiaPhase.Settlement:
                if (confirmedLedgerDay != Session.Day || isLedgerReopened) DailyLedger();
                else SettlementActions();
                break;
            case DystopiaPhase.Goal:
                Modal("안전구역으로", "“아빠, 이제 우리 따뜻한 데서 잘 수 있어?”",false);
                Label(modal,"Ending","시민권을 손에 넣었습니다.\n\n당신이 매긴 가격들이 이곳까지 데려왔습니다.\n\n명성과 도덕성, 그리고 남겨진 사람들을 기억하며.",50,185,640,195,23);
                MakeButton(modal,"Restart","다시 시작",170,420,400,48,Restart,21);
                break;
            case DystopiaPhase.Failed:
                Modal("가판은 문을 닫았습니다", "“상납금이 없다고? 그럼 자리를 비워.”",true);
                Label(modal,"Failure",$"필요한 상납금 {Session.TributeAmount:N0}원\n보유 현금 {Session.Cash:N0}원\n\n기억과 선택을 되짚어 다시 시작하세요.",40,190,470,170,22);
                MakeButton(modal,"Restart","다시 시작",80,420,400,48,Restart,21);
                break;
        }
    }

    /// <summary>현재 장바구니의 개별 물품을 판매 대상에서 제외하거나 복구합니다.</summary>
    /// <param name="lineIndex">장바구니 품목 줄 번호입니다.</param>
    /// <param name="unitIndex">해당 줄 안의 개별 물품 번호입니다.</param>
    private void ToggleBasketUnit(int lineIndex, int unitIndex)
    {
        if (isDailyInstructionOpen || !Session.ToggleBasketUnit(lineIndex, unitIndex)) return;
        amount = "";
        invalidAmount = false;
        Refresh();
    }

    /// <summary>대기 2번은 카운터로, 3번은 앞 대기 위치로 이동하며 크기가 커집니다.</summary>
    private System.Collections.IEnumerator AdvanceQueueVisual()
    {
        float elapsed = 0;
        while (elapsed < .65f)
        {
            if (!Session.IsPaused) elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .65f));
            for (int i = 0; i < idlePeople.Length; i++)
            {
                Vector2 start = i == 2 ? idleOrigins[0] : i == 0 ? idleOrigins[1] : idleOrigins[1] + new Vector2(-65, 12);
                // 아이는 대기 위치에서 이동하지 않고 테이블 아래에서 짧게 올라옵니다.
                if (i == 2 && Session.Customer.Type == DystopiaCustomerType.Child)
                {
                    float pop = Mathf.Clamp01(elapsed / .28f);
                    float rise = 1 - Mathf.Pow(1 - pop, 3);
                    idlePeople[i].anchoredPosition = ChildPortraitOrigin + Vector2.down * (1 - rise) * 240;
                    idlePeople[i].localScale = Vector3.Scale(placedPeopleScales[i], new Vector3(ChildPortraitScale, ChildPortraitScale, 1));
                    continue;
                }
                float size = i == 2 ? 340f / 550 : i == 0 ? 240f / 340 : .8f;
                idlePeople[i].anchoredPosition = Vector2.Lerp(start, idleOrigins[i], t);
                idlePeople[i].localScale = placedPeopleScales[i] * Mathf.Lerp(size, 1, t);
            }
            yield return null;
        }
        queueMovement = null;
    }

    /// <summary>현재 손님의 성별과 외형 번호에 맞는 분리 Sprite를 반환합니다.</summary>
    /// <param name="isMale">남성 외형 묶음을 사용할 때 true입니다.</param>
    /// <param name="appearance">성별 외형 묶음 안의 번호입니다.</param>
    /// <returns>연결된 손님 Sprite이며, 빌드 연결 누락 시 이전 참조를 사용합니다.</returns>
    private Sprite CustomerPoolSprite(bool isMale, int appearance)
    {
        Sprite[] pool = isMale ? maleCustomers : femaleCustomers;
        if (pool != null && pool.Length > 0)
        {
            Sprite sprite = pool[Mathf.Abs(appearance) % pool.Length];
            if (sprite != null) return sprite;
        }
        if (customers == null || customers.Length == 0) return null;
        return customers[Mathf.Abs(appearance) % customers.Length];
    }

    /// <summary>영업 중 가격을 다시 공개하지 않고 당일 지침만 엽니다.</summary>
    private void OpenDailyInstruction()
    {
        if (Session.Phase != DystopiaPhase.Trading || Session.IsPaused || isDailyInstructionOpen) return;
        isDailyInstructionOpen = true;
        Refresh();
    }

    /// <summary>영업 중 지침 재열람을 닫고 같은 거래 상태로 돌아갑니다.</summary>
    private void CloseDailyInstruction()
    {
        if (!isDailyInstructionOpen) return;
        isDailyInstructionOpen = false;
        Refresh();
    }

    /// <summary>사용자 제공 종이 위에 당일 지침과 영업 전 가격을 별도 UI로 배치합니다.</summary>
    /// <param name="beforeOpening">영업 시작 전 가격까지 표시할 때 true입니다.</param>
    private void DailyInstruction(bool beforeOpening)
    {
        if (dailyInstruction == null)
        {
            Modal("일일지침 이미지 연결 필요","Daily Instruction Sprite를 Inspector에 연결하세요.",false);
            MakeButton(modal,beforeOpening ? "OpenShop" : "CloseInstruction",beforeOpening ? "영업 시작" : "닫기",190,380,360,44,
                beforeOpening ? (UnityEngine.Events.UnityAction)(() => { Session.OpenShop(); Refresh(); }) : CloseDailyInstruction,18);
            return;
        }

        var ink = new Color(.08f,.07f,.06f);
        var muted = new Color(.20f,.18f,.15f);
        modal = Rect(root,"DailyInstruction",0,0,1280,720);
        var blocker = Panel(modal,"InputBlocker",0,0,1280,720,new Color(0,0,0,.42f));
        blocker.raycastTarget = true;
        var sheet = Picture(modal,"Sheet",dailyInstruction,300,5,680,680,true).rectTransform;

        // 세로 원화의 실제 표시 영역을 기준으로 새 런타임 내용만 생성합니다.
        // 이미 만들어진 창과 내용의 사용자 편집 Transform은 다시 설정하지 않습니다.
        var content = sheet.Find("PrintedContent") as RectTransform;
        if (content == null)
        {
            Vector2 artwork = dailyInstruction.rect.size;
            Vector2 available = sheet.rect.size;
            float scale = Mathf.Min(available.x / artwork.x, available.y / artwork.y);
            Vector2 drawn = artwork * scale;
            Vector2 offset = Vector2.Scale(available - drawn, new Vector2(sheet.pivot.x, 1f - sheet.pivot.y));
            content = Rect(sheet,"PrintedContent",offset.x,offset.y,224,280);
            content.localScale = new Vector3(drawn.x / 224f, drawn.y / 280f, 1);
        }
        content.gameObject.SetActive(true);

        // 날짜 칸에 인쇄된 슬래시를 종이색으로 덮고 바깥 테두리는 보존합니다.
        Panel(content,"DayPaper",140,52,48,8,new Color(.81f,.80f,.76f));
        var day = Label(content,"InstructionDay",$"{Session.Day}일차",139,50,50,12,7,ink);
        day.alignment = TextAnchor.MiddleCenter;
        day.fontStyle = FontStyle.Bold;

        var heading = Label(content,"MemoryHeading","영업 전, 가격을 기억하세요",32,78,164,14,8,ink);
        heading.alignment = TextAnchor.MiddleCenter;
        heading.fontStyle = FontStyle.Bold;
        var ruleTitle = Label(content,"RuleTitle","오늘의 지침",32,97,164,11,7,muted);
        ruleTitle.alignment = TextAnchor.MiddleCenter;
        ruleTitle.fontStyle = FontStyle.Bold;
        var rule = Label(content,"Rule",Session.DailyRuleText,32,110,164,24,7,ink);
        rule.alignment = TextAnchor.UpperCenter;

        if (beforeOpening)
        {
            for (int i = 0; i < Session.ActiveProducts.Count; i++)
            {
                DystopiaProduct product = Session.ActiveProducts[i];
                int column = i % 2;
                int row = i / 2;
                float x = 32 + column * 85;
                float y = 143 + row * Mathf.Min(29f, 65f / Mathf.Max(1, (Session.ActiveProducts.Count + 1) / 2));
                Picture(content,"InstructionProduct"+i,product.sprite,x,y,18,21,true);
                var productName = Label(content,"InstructionName"+i,product.name,x+22,y,57,10,7,ink);
                productName.alignment = TextAnchor.MiddleLeft;
                var price = Label(content,"InstructionPrice"+i,$"{product.price:N0}원",x+22,y+10,57,11,8,ink);
                price.alignment = TextAnchor.MiddleLeft;
                price.fontStyle = FontStyle.Bold;
            }
            var restriction = Label(content,"PriceRestriction","영업이 시작되면 가격표를 다시 볼 수 없습니다.",30,214,168,12,5,muted);
            restriction.alignment = TextAnchor.MiddleCenter;
            var recheck = Label(content,"RecheckGuide","당일 지침은 영업 중에도 다시 확인할 수 있습니다.",30,226,168,12,5,muted);
            recheck.alignment = TextAnchor.MiddleCenter;
            // 원본 도장을 가리지 않는 폭과 지침서의 잉크·종이 색으로 버튼을 맞춥니다.
            Panel(content,"OpenShopBorder",73,242,82,18,muted);
            var openShop = MakeButton(content,"OpenShop","영업 시작",74,243,80,16,()=>{Session.OpenShop();Refresh();},8);
            var paperColors = openShop.colors;
            paperColors.normalColor = new Color(.78f,.76f,.69f);
            paperColors.highlightedColor = new Color(.88f,.85f,.77f);
            paperColors.pressedColor = new Color(.62f,.60f,.53f);
            paperColors.selectedColor = paperColors.normalColor;
            openShop.colors = paperColors;
            openShop.GetComponentInChildren<Text>().color = ink;
        }
        else
        {
            var restriction = Label(content,"PriceRestriction","가격표는 영업 중 다시 볼 수 없습니다.",32,151,164,15,7,muted);
            restriction.alignment = TextAnchor.MiddleCenter;
            var violations = Label(content,"ViolationCount",$"오늘 적발 {Session.RuleViolationCount:N0}회",32,173,164,13,7,ink);
            violations.alignment = TextAnchor.MiddleCenter;
            violations.fontStyle = FontStyle.Bold;
            if (Session.RuleViolationCount > 0)
            {
                var latest = Label(content,"LatestViolation",$"최근 위반: {Session.LastRuleViolation}",32,191,164,25,6,muted);
                latest.alignment = TextAnchor.UpperCenter;
            }
            MakeButton(content,"CloseInstruction","지침 닫기",74,243,80,16,CloseDailyInstruction,8);
        }
        foreach (var label in content.GetComponentsInChildren<Text>(true))
        {
            if (label.GetComponent<DystopiaInstructionText>() == null)
                label.gameObject.AddComponent<DystopiaInstructionText>();
        }
    }

    /// <summary>이미 완료된 당일 정산값만 읽어 펼친 책 위에 표시합니다.</summary>
    private void DailyLedger()
    {
        if (dailyLedger == null)
        {
            Modal("가계부 이미지 연결 필요","Daily Ledger Sprite를 Inspector에 연결하세요.",false);
            ledgerConfirmButton = MakeButton(modal,"LedgerConfirm","확인",190,380,360,44,ConfirmLedger,18);
            ledgerConfirmButton.interactable = true;
            return;
        }

        var ink = Color.black;
        var muted = Color.black;
        var rule = new Color(.25f,.17f,.11f,.45f);
        var income = Color.black;
        var expense = Color.black;
        modal = Rect(root,"DailyLedger",0,0,1280,720);
        var blocker = Panel(modal,"InputBlocker",0,0,1280,720,Color.clear);
        blocker.raycastTarget = true;
        Picture(modal,"Room",dailyLedger,0,0,1280,720,false);
        Texture2D reputationStamp = GetLedgerReputationStamp(Session.ReputationTier);
        if (reputationStamp != null)
        {
            var drawingRect = Rect(modal,"WallDrawing",ledgerDrawingLayout.x,ledgerDrawingLayout.y,ledgerDrawingLayout.width,ledgerDrawingLayout.height);
            var drawing = drawingRect.GetComponent<RawImage>();
            // 이전에 만든 그림은 표시만 끄고 도장 자식의 편집된 Transform은 유지합니다.
            if (drawing != null) drawing.enabled = false;
            var stampRect = Rect(drawingRect,"Stamp",ledgerStampLayout.x,ledgerStampLayout.y,ledgerStampLayout.width,ledgerStampLayout.height);
            var stamp = stampRect.GetComponent<RawImage>();
            if (stamp == null)
            {
                stamp = stampRect.gameObject.AddComponent<RawImage>();
                stamp.raycastTarget = false;
            }
            // 정산 UI를 재사용해도 현재 누적 명성에 맞는 도장으로 교체합니다.
            stamp.texture = reputationStamp;
            stamp.color = new Color(ledgerStampTint.r,ledgerStampTint.g,ledgerStampTint.b,ledgerStampOpacity);
        }
        if (ledgerDaughter != null)
        {
            var daughter = Picture(modal,"Daughter",ledgerDaughter,22*1280f/334,64*720f/188,78*1280f/334,78*720f/188,true);
            // 창밖의 차가운 밤빛보다 방 안의 낮고 따뜻한 빛을 따르도록 원본 색을 곱합니다.
            daughter.color = new Color(.62f,.50f,.40f);
            if (ledgerSpeechBubble != null)
            {
                // 꼬리까지 Y=230에 끝나므로 Y=245에서 시작하는 공책을 가리지 않습니다.
                var speech = Picture(modal,"DaughterSpeech",ledgerSpeechBubble,142,110,520,120,false);
                speech.type = Image.Type.Sliced;
                speech.pixelsPerUnitMultiplier = 6;
                var speechText = Label(speech.transform,"Text","아빠 오늘 돈 많이 벌었어?",44,20,440,66,24,ink);
                speechText.color = ink;
                speechText.alignment = TextAnchor.MiddleCenter;
                speechText.horizontalOverflow = HorizontalWrapMode.Wrap;
                speechText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
        // 원본 하단을 다시 그려 딸의 몸이 책상 뒤로 들어가게 합니다.
        var deskMask = Rect(modal,"DeskForeground",0,132*720f/188,1280,56*720f/188);
        if (deskMask.GetComponent<RectMask2D>() == null) deskMask.gameObject.AddComponent<RectMask2D>();
        Picture(deskMask,"Desk",dailyLedger,0,-132*720f/188,1280,720,false);
        if (ledgerForeground == null)
        {
            ledgerForeground = Sprite.Create(dailyLedger.texture,new UnityEngine.Rect(84,8,188,116),Vector2.zero,1,0,SpriteMeshType.FullRect);
            ledgerForeground.name = "Ledger notebook foreground";
            // 공책의 실제 사다리꼴 외곽만 복사해 딸의 얼굴을 사각형으로 잘라내지 않습니다.
            ledgerForeground.OverrideGeometry(new[] { new Vector2(22,116),new Vector2(166,116),new Vector2(188,0),Vector2.zero },new ushort[] { 0,1,2,2,3,0 });
        }
        var cover = Picture(modal,"NotebookForeground",ledgerForeground,84*1280f/334,64*720f/188,188*1280f/334,116*720f/188,false);
        cover.useSpriteMesh = true;
        var book = Rect(modal,"LedgerPageContent",0,0,1280,720);

        var day = LedgerLabel(book,"LedgerDay",$"영업 {Session.Day}일차",92,94,350,28,18,muted);
        day.alignment = TextAnchor.MiddleLeft;
        LedgerHeading(book,"오늘의 장사",92,128,350,ink,rule);
        LedgerRow(book,"Handled","응대한 손님",$"{Session.HandledCustomers:N0}명",92,180,350,muted,ink);
        LedgerRow(book,"MarkupCount","폭리 거래",$"{Session.MarkupTransactions:N0}건",92,228,350,muted,ink);
        LedgerRow(book,"MarkupAmount","폭리 금액",$"{Session.MarkupAmount:N0}원",92,276,350,muted,ink);
        Panel(book,"TradeDivider",92,322,350,1,rule);
        LedgerRow(book,"Morality","도덕성 변화",SignedNumber(Session.MoralityChange),92,344,350,muted,ink,18,23);
        LedgerRow(book,"Reputation","명성 변화",SignedNumber(Session.ReputationChange),92,394,350,muted,ink,18,23);

        LedgerHeading(book,"오늘의 정산",548,128,350,ink,rule);
        LedgerRow(book,"Revenue","판매수익",SignedMoney(Session.Revenue),548,180,350,ink,Session.Revenue > 0 ? income : ink,18,24);
        LedgerRow(book,"GoodsCost","물품대금","—",548,226,350,muted,muted);
        LedgerRow(book,"Rent","임대료","—",548,266,350,muted,muted);
        LedgerRow(book,"Medical","치료비","—",548,306,350,muted,muted);
        LedgerRow(book,"Tribute","상납금",Session.PaidTributeToday > 0 ? $"-{Session.PaidTributeToday:N0}원" : "0원",548,346,350,muted,Session.PaidTributeToday > 0 ? expense : muted);
        Panel(book,"SettlementDivider",548,389,350,2,rule);
        LedgerRow(book,"NetProfit","오늘 순이익","—",548,403,350,ink,muted,22,30);
        LedgerRow(book,"Cash","현재 보유금",$"{Session.Cash:N0}원",548,449,350,ink,ink,22,30);
        Panel(book,"TributeGuideDivider",548,488,350,1,new Color(rule.r,rule.g,rule.b,.3f));
        LedgerMini(book,"NextTribute","다음 상납까지",$"{Session.DaysUntilTribute}일",548,494,162,muted,ink);
        Panel(book,"TributeGuideSplit",722,495,1,28,new Color(rule.r,rule.g,rule.b,.3f));
        LedgerMini(book,"TributeDue","납부 예정",$"{Session.NextTributeAmount:N0}원",736,494,162,muted,ink);
        // 글자뿐 아니라 구분선도 같은 페이지 원근으로 투영합니다.
        foreach (var graphic in book.GetComponentsInChildren<Graphic>(true))
        {
            var projection = graphic.GetComponent<DystopiaLedgerPage>();
            if (projection == null) projection = graphic.gameObject.AddComponent<DystopiaLedgerPage>();
            projection.Configure(book,graphic.rectTransform.anchoredPosition.x > 500,ledgerPageInset);
        }
        ledgerConfirmButton = MakeButton(modal,"LedgerConfirm","확인",1080,646,150,44,ConfirmLedger,18);
        var buttonColors = ledgerConfirmButton.colors;
        buttonColors.normalColor = new Color(.25f,.17f,.11f);
        buttonColors.highlightedColor = new Color(.39f,.28f,.18f);
        buttonColors.pressedColor = new Color(.17f,.11f,.07f);
        buttonColors.selectedColor = buttonColors.normalColor;
        ledgerConfirmButton.colors = buttonColors;
        // 이전 날짜의 확인 처리로 비활성화된 재사용 버튼을 다시 열어 줍니다.
        ledgerConfirmButton.interactable = true;
        ledgerConfirmButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
    }

    /// <summary>명성 등급에 해당하는 사용자 제공 도장을 반환합니다.</summary>
    /// <param name="tier">세션이 분류한 현재 명성 등급입니다.</param>
    /// <returns>등급별 도장 원본입니다. 미연결이면 null입니다.</returns>
    private Texture2D GetLedgerReputationStamp(DystopiaReputationTier tier)
    {
        switch (tier)
        {
            case DystopiaReputationTier.Trusted: return ledgerStamp;
            case DystopiaReputationTier.Popular: return ledgerStampPopular;
            case DystopiaReputationTier.Neutral: return ledgerStampNeutral;
            case DystopiaReputationTier.Unpopular: return ledgerStampUnpopular;
            default: return ledgerStampNotorious;
        }
    }

    /// <summary>가계부 확인을 한 번만 기록하고 기존 정산 후 선택으로 진행합니다.</summary>
    private void ConfirmLedger()
    {
        if (Session.Phase != DystopiaPhase.Settlement ||
            confirmedLedgerDay == Session.Day && !isLedgerReopened) return;
        if (ledgerConfirmButton != null) ledgerConfirmButton.interactable = false;
        if (confirmedLedgerDay != Session.Day) confirmedLedgerDay = Session.Day;
        isLedgerReopened = false;
        Refresh();
    }

    /// <summary>가계부 뒤에 기존 다음 날과 시민권 선택을 그대로 제공합니다.</summary>
    private void SettlementActions()
    {
        Modal("정산 확인","다음 단계로 진행하세요.",false);
        MakeButton(modal,"ReopenLedger","가계부 다시 보기",190,350,360,44,ReopenLedger,18);
        MakeButton(modal,"NextDay","다음 날",390,420,300,48,()=>{Session.NextDay();Refresh();},21);
        var buy=MakeButton(modal,"BuyCitizenship","시민권 구매",40,420,320,48,()=>{Session.BuyCitizenship();Refresh();},21);
        buy.interactable=Session.CanBuy;
    }

    /// <summary>확인된 당일 값을 다시 표시하되 정산이나 확인 상태를 재적용하지 않습니다.</summary>
    private void ReopenLedger()
    {
        if (Session.Phase != DystopiaPhase.Settlement || confirmedLedgerDay != Session.Day || isLedgerReopened) return;
        isLedgerReopened = true;
        Refresh();
    }

    /// <summary>책 페이지의 작은 절 제목을 배치합니다.</summary>
    private void LedgerHeading(Transform parent,string value,float x,float y,float width,Color color,Color ruleColor)
    {
        var heading = LedgerLabel(parent,"Heading"+value,value,x,y,width,36,27,color);
        heading.fontStyle = FontStyle.Bold;
        heading.alignment = TextAnchor.MiddleLeft;
        Panel(parent,"HeadingRule"+value,x,y+39,width,2,ruleColor);
    }

    /// <summary>책 제본을 피한 한 줄 항목과 우측 정렬 값을 배치합니다.</summary>
    private void LedgerRow(Transform parent,string name,string label,string value,float x,float y,float width,Color labelColor,Color valueColor,int labelSize=18,int valueSize=21)
    {
        var key = LedgerLabel(parent,name+"Label",label,x,y,width*.56f,34,labelSize,labelColor);
        key.alignment = TextAnchor.MiddleLeft;
        if (value == "—")
        {
            Panel(parent,name+"MissingValue",x+width-20,y+16,20,2,valueColor);
            return;
        }
        var amountText = LedgerLabel(parent,name+"Value",value,x+width*.52f,y,width*.48f,34,valueSize,valueColor);
        amountText.fontStyle = FontStyle.Bold;
        amountText.alignment = TextAnchor.MiddleRight;
    }

    /// <summary>다음 상납 안내를 짧은 라벨과 강조 값 한 줄로 정렬합니다.</summary>
    private void LedgerMini(Transform parent,string name,string label,string value,float x,float y,float width,Color labelColor,Color valueColor)
    {
        var key = LedgerLabel(parent,name+"Label",label,x,y,width*.62f,28,14,labelColor);
        key.alignment = TextAnchor.MiddleLeft;
        var amountText = LedgerLabel(parent,name+"Value",value,x+width*.48f,y,width*.52f,28,17,valueColor);
        amountText.fontStyle = FontStyle.Bold;
        amountText.alignment = TextAnchor.MiddleRight;
    }

    /// <summary>공통 물마루 폰트의 가계부 텍스트를 만들고 재사용한 항목에도 현재 잉크 색을 적용합니다.</summary>
    private Text LedgerLabel(Transform parent,string name,string value,float x,float y,float width,float height,int size,Color color)
    {
        var text = Label(parent,name,value,x,y,width,height,size,color);
        text.color = color;
        return text;
    }

    /// <summary>변화량의 양수에만 더하기 기호를 붙입니다.</summary>
    private static string SignedNumber(int value) => value > 0 ? $"+{value:N0}" : value.ToString("N0");

    /// <summary>수입 금액의 양수에만 더하기 기호와 원 단위를 붙입니다.</summary>
    private static string SignedMoney(int value) => value > 0 ? $"+{value:N0}원" : $"{value:N0}원";

    /// <summary>게임 아트 위에 읽기 쉬운 안내 영역을 배치합니다.</summary>
    private void Modal(string title,string subtitle,bool showInspector)
    {
        modal = Rect(root,"Modal",270,111,740,510);
        Panel(modal,"Border",0,0,740,510,new Color(.46f,.51f,.51f));
        Panel(modal,"Paper",2,2,736,506,new Color(.075f,.095f,.105f,.99f));
        Label(modal,"ModalTitle",title,35,28,670,44,31);
        Label(modal,"ModalSubtitle",subtitle,35,85,670,55,18);
        if (showInspector)
        {
            if (inspectorPortraitPrefab != null)
            {
                var frame = Rect(modal,"Inspector",488,150,245,350);
                var portraitRoot = Instantiate(inspectorPortraitPrefab, frame, false).GetComponent<RectTransform>();
                portraitRoot.anchorMin = portraitRoot.anchorMax = new Vector2(.5f,.5f);
                portraitRoot.anchoredPosition = Vector2.zero;
                portraitRoot.localScale = Vector3.one * Mathf.Min(245 / portraitRoot.sizeDelta.x, 350 / portraitRoot.sizeDelta.y);
            }
            else Picture(modal,"Inspector",inspector,488,150,245,350,true);
        }
    }

    /// <summary>입력 금액은 플레이어가 직접 입력한 값만 표시합니다.</summary>
    private void ShowAmount()
    {
        inputText.fontSize = invalidAmount ? 17 : 30;
        inputText.text = invalidAmount ? "정수만 입력하세요 · 지우기로 초기화" : amount.Length==0 ? "금액 입력" : $"{int.Parse(amount):N0} 원";
    }

    /// <summary>자식들을 즉시 숨긴 뒤 프레임 끝에 해제합니다.</summary>
    private void ClearChildren(Transform parent)
    {
        for(int i=parent.childCount-1;i>=0;i--)
        {
            var child=parent.GetChild(i).gameObject; child.SetActive(false);
            if(Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }

    /// <summary>좌상단 기준의 고정 reference-resolution 영역을 만듭니다.</summary>
    private RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
    {
        bool isBasketItem = basketRoot != null && (parent == basketRoot || parent.IsChildOf(basketRoot));
        var existing = isBasketItem ? null : parent.Find(name) as RectTransform;
        if (existing != null)
        {
            if (parent == root && (name == "DailyInstruction" || name == "DailyLedger" || name == "Modal"))
                foreach (Transform child in existing.GetComponentsInChildren<Transform>(true))
                    if (child != existing) child.gameObject.SetActive(false);
            existing.gameObject.SetActive(true);
            return existing;
        }
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(width,height);
        return rect;
    }

    /// <summary>단색 UI 영역을 만듭니다.</summary>
    private Image Panel(Transform parent,string name,float x,float y,float width,float height,Color color)
    {
        var rect=Rect(parent,name,x,y,width,height);
        var image=rect.GetComponent<Image>();
        if (image == null) { image=rect.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; }
        return image;
    }

    /// <summary>독립적인 Sprite 참조를 UI에 표시합니다.</summary>
    private Image Picture(Transform parent,string name,Sprite sprite,float x,float y,float width,float height,bool preserve)
    {
        var image=Panel(parent,name,x,y,width,height,Color.white);
        if (image.sprite == null) { image.sprite=sprite; image.preserveAspect=preserve; }
        if(image.sprite==null) image.color=Color.clear;
        return image;
    }

    /// <summary>전체 감시탑 Sprite에서 초소 앞 난간 영역만 정확히 잘라 경비병 앞에 표시합니다.</summary>
    private void TowerFrontRail(Transform parent,string name,Sprite sprite,Material material,UnityEngine.Rect crop)
    {
        var mask = Rect(parent,name+"Mask",crop.x,crop.y,crop.width,crop.height);
        mask.gameObject.AddComponent<RectMask2D>();
        var rail = Picture(mask,name,sprite,-crop.x,-crop.y,1280,720,false);
        rail.material = material;
    }

    /// <summary>한국어 텍스트를 이미지와 분리하여 표시합니다.</summary>
    private Text Label(Transform parent,string name,string value,float x,float y,float width,float height,int size,Color? color=null)
    {
        var rect=Rect(parent,name,x,y,width,height);
        var text=rect.GetComponent<Text>();
        bool created=text==null;
        if (created) { text=rect.gameObject.AddComponent<Text>(); text.font=font; text.fontSize=size; }
        text.text=value;
        if (created) text.color=color??new Color(.91f,.93f,.91f);
        text.raycastTarget=false; text.verticalOverflow=VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>실제 uGUI 버튼 이벤트를 연결합니다.</summary>
    private Button MakeButton(Transform parent,string name,string value,float x,float y,float width,float height,UnityEngine.Events.UnityAction action,int size)
    {
        var image=Panel(parent,name,x,y,width,height,Color.white); image.raycastTarget=true;
        var button=image.GetComponent<Button>();
        if (button == null)
        {
            button=image.gameObject.AddComponent<Button>();
            var colors=button.colors; colors.normalColor=new Color(.19f,.25f,.28f); colors.highlightedColor=new Color(.30f,.39f,.42f); colors.pressedColor=new Color(.42f,.50f,.51f); button.colors=colors;
        }
        button.onClick.RemoveAllListeners(); button.targetGraphic=image;
        button.navigation=new Navigation {mode=Navigation.Mode.None}; button.onClick.AddListener(action);
        var label=Label(image.transform,"Label",value,4,0,width-8,height,size); label.alignment=TextAnchor.MiddleCenter;
        return button;
    }
}
