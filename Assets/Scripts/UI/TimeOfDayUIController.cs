using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 영업 시간(09:00~21:00)에 맞춰 TimeOfDay 아트 레이어(Dawn, Sunset, Evening, CityLights, CounterLight, Searchlight)를
/// 부드럽게 페이드 블렌딩하고 환경광 및 인물 틴트를 제어하는 정규 UI 컨트롤러입니다.
/// 플레이 모드 테스트를 위해 단축키([, ], \) 및 인스펙터 슬라이더를 지원합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TimeOfDayUIController : MonoBehaviour
{
    [Header("Clock Authority")]
    [Tooltip("DayProgress의 시각을 표시하는 시계. 배경 미리보기는 이 값을 변경하지 않음")]
    [SerializeField] private BusinessClockController businessClock;

    [Header("TimeOfDay Art Layers")]
    [Tooltip("아침 레이어 (DawnBackground)")]
    [SerializeField] private Image dawn;

    [Tooltip("석양 레이어 (SunsetBackground)")]
    [SerializeField] private Image sunset;

    [Tooltip("저녁/야간 레이어 (EveningBackground)")]
    [SerializeField] private Image evening;

    [Tooltip("원경 도시 불빛 (CityLights)")]
    [SerializeField] private Image cityLights;

    [Tooltip("가판대 조명 (CounterLight)")]
    [SerializeField] private Image counterLight;

    [Tooltip("좌측 감시탑 탐조등 (LeftBeam)")]
    [SerializeField] private Image leftBeam;

    [Tooltip("우측 감시탑 탐조등 (RightBeam)")]
    [SerializeField] private Image rightBeam;

    [Header("Layer Sprites (Auto-loaded if unassigned)")]
    [SerializeField] private Sprite dawnSprite;
    [SerializeField] private Sprite sunsetSprite;
    [SerializeField] private Sprite eveningSprite;
    [SerializeField] private Sprite cityLightsSprite;
    [SerializeField] private Sprite counterLightSprite;
    [SerializeField] private Sprite searchlightSprite;
    [SerializeField] private Material cityLightsMaterial;

    [Header("Tint Target Graphics")]
    [Tooltip("환경 배경 그래픽 배열 (미할당 시 자동 탐색)")]
    [SerializeField] private Graphic[] environment;

    [Tooltip("인물 그래픽 배열 (미할당 시 Customer 자동 탐색)")]
    [SerializeField] private Graphic[] people;

    [Header("Time Settings (Hour)")]
    [SerializeField] private float dayStart = 12f;
    [SerializeField] private float sunsetStart = 15f;
    [SerializeField] private float eveningStart = 18f;

    [Header("Tints & Intensities")]
    [SerializeField] private Color dawnTint = new Color(1f, 0.90f, 0.80f);
    [SerializeField] private Color sunsetTint = new Color(1f, 0.72f, 0.49f);
    [SerializeField] private Color nightTint = new Color(0.38f, 0.43f, 0.56f);
    [SerializeField, Range(0f, 1f)] private float peopleBrightness = 0.72f;
    [SerializeField, Range(0f, 1f)] private float cityIntensity = 0.7f;
    [SerializeField, Range(0f, 1f)] private float beamIntensity = 0.11f;
    [SerializeField, Range(0f, 1f)] private float counterIntensity = 0.12f;

    [Header("Pixel Stage (Optional Shader)")]
    [Tooltip("표면 조명 및 노말맵 픽셀 스테이지 (선택 사항)")]
    [SerializeField] private TimeOfDayPixelStage pixelStage;

    [Header("Debug & Test Controls")]
    [Tooltip("수동 시간 오버라이드 활성화 (체크 시 슬라이더나 단축키로 시간 조절 가능)")]
    [SerializeField] private bool debugOverrideTime;

    [Tooltip("수동 테스트 시간 슬라이더 (09:00 ~ 21:00)")]
    [SerializeField, Range(BusinessHours.OpenHour, BusinessHours.CloseHour)] private float debugHour = BusinessHours.OpenHour;

    [Tooltip("배경 전환 테스트를 위해 시간을 09시부터 21시까지 부드럽게 자동 순환할지 여부")]
    [SerializeField] private bool autoAdvanceClockForTesting;

    [Tooltip("09시~21시 1회 전체 순환 소요 실제 시간(초)")]
    [SerializeField, Range(10f, 180f)] private float fullCycleSeconds = 45f;

    [Tooltip("화면 좌상단에 단축키 안내 오버레이 표시 여부")]
    [SerializeField] private bool showDebugOverlay;

    [Header("Editor Preview")]
    [SerializeField] private bool previewInEditor;
    [SerializeField, Range(BusinessHours.OpenHour, BusinessHours.CloseHour)] private float previewHour = BusinessHours.OpenHour;

    private float currentAppliedHour = BusinessHours.OpenHour;

    public BusinessClockController BusinessClock
    {
        get => this.businessClock;
        set => this.businessClock = value;
    }

    public TimeOfDayPixelStage PixelStage
    {
        get => this.pixelStage;
        set => this.pixelStage = value;
    }

    public Image Dawn => this.dawn;
    public Image Sunset => this.sunset;
    public Image Evening => this.evening;
    public Image CityLights => this.cityLights;
    public Image CounterLight => this.counterLight;
    public Image LeftBeam => this.leftBeam;
    public Image RightBeam => this.rightBeam;
    public float CurrentAppliedHour => this.currentAppliedHour;

    private void Awake()
    {
        this.autoResolveReferences();
    }

    private void OnEnable()
    {
        this.autoResolveReferences();
    }

    /// <summary>테스트 입력은 배경 표시 시각만 바꾸며 영업 진행과 시계에는 쓰지 않는다.</summary>
    private void Update()
    {
        if (!Application.isPlaying) return;

        bool stepForward = false;
        bool stepBackward = false;
        bool cyclePhase = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.rightBracketKey.wasPressedThisFrame) stepForward = true;
            if (kb.leftBracketKey.wasPressedThisFrame) stepBackward = true;
            if (kb.backslashKey.wasPressedThisFrame || kb.tKey.wasPressedThisFrame) cyclePhase = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.RightBracket)) stepForward = true;
        if (Input.GetKeyDown(KeyCode.LeftBracket)) stepBackward = true;
        if (Input.GetKeyDown(KeyCode.Backslash) || Input.GetKeyDown(KeyCode.T)) cyclePhase = true;
#endif

        // 단축키 제어: [ (1시간 뒤로), ] (1시간 앞으로), \ 또는 T (주요 페이즈 순환)
        if (stepForward)
        {
            this.debugOverrideTime = true;
            this.debugHour = Mathf.Min(this.debugHour + 1f, BusinessHours.CloseHour);
            Debug.Log($"<color=cyan>[TimeOfDay] 1시간 앞으로 이동 -> {this.formatHour(this.debugHour)}</color>");
        }
        else if (stepBackward)
        {
            this.debugOverrideTime = true;
            this.debugHour = Mathf.Max(this.debugHour - 1f, BusinessHours.OpenHour);
            Debug.Log($"<color=cyan>[TimeOfDay] 1시간 뒤로 이동 -> {this.formatHour(this.debugHour)}</color>");
        }
        else if (cyclePhase)
        {
            this.debugOverrideTime = true;
            this.cycleNextPhase();
            Debug.Log($"<color=cyan>[TimeOfDay] 페이즈 전환 -> {this.formatHour(this.debugHour)} ({this.getPhaseName(this.debugHour)})</color>");
        }

        // 자동 순환 테스트
        if (this.autoAdvanceClockForTesting)
        {
            this.debugOverrideTime = true;
            float speed = (BusinessHours.CloseHour - BusinessHours.OpenHour) / Mathf.Max(this.fullCycleSeconds, 5f);
            this.debugHour += Time.unscaledDeltaTime * speed;
            if (this.debugHour > BusinessHours.CloseHour)
            {
                this.debugHour = BusinessHours.OpenHour;
            }
        }
    }

    private void LateUpdate()
    {
        if (this.pixelStage != null && this.pixelStage.IsRendering) return;
        this.RefreshTime();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !this.showDebugOverlay) return;

        GUI.color = Color.white;
        string phase = this.getPhaseName(this.currentAppliedHour);
        string timeStr = this.formatHour(this.currentAppliedHour);
        string overrideStr = this.debugOverrideTime ? "<color=#FFCC00>[테스트 오버라이드]</color>" : "[클럭 연동]";

        string msg = $"{overrideStr} <b>{timeStr} ({phase})</b>  |  단축키: <b>[</b> -1h  <b>]</b> +1h  <b>\\</b> 페이즈변경  <b>L</b> 상자더스트";
        GUI.Box(new Rect(10, 10, 520, 28), GUIContent.none);
        GUI.Label(new Rect(16, 14, 510, 24), msg);
    }

    /// <summary>미할당된 참조들을 자식 오브젝트 및 씬에서 안전하게 탐색하고 부재 시 레이어를 자동 구성합니다.</summary>
    public void AutoResolveReferences()
    {
        this.autoResolveReferences();
    }

    private void autoResolveReferences()
    {
        if (this.businessClock == null)
        {
            this.businessClock = FindFirstObjectByType<BusinessClockController>();
        }

        Transform root = this.transform;

        // 0. 스카이라인(아파트 및 서울타워)을 가리는 잘못된 안개 레이어 비활성화
        string[] fogNames = { "FogBack", "FogMid", "FogFront" };
        foreach (string fogName in fogNames)
        {
            Transform fogChild = root.Find(fogName);
            if (fogChild != null)
            {
                fogChild.gameObject.SetActive(false);
            }
        }

        // FarBackground를 최하단(sibling 0)에 배치
        Transform farBg = root.Find("FarBackground");
        if (farBg != null)
        {
            farBg.SetSiblingIndex(0);
        }

#if UNITY_EDITOR
        // 에디터 실행 시 TimeOfDay 폴더의 원본 스프라이트 및 머티리얼 자동 탐색 및 로드
        if (this.dawnSprite == null) this.dawnSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/TimeOfDay/Dawn.png");
        if (this.sunsetSprite == null) this.sunsetSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/TimeOfDay/Sunset.png");
        if (this.eveningSprite == null) this.eveningSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/TimeOfDay/Evening.png");
        if (this.cityLightsSprite == null) this.cityLightsSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/TimeOfDay/CityLights.png");
        if (this.counterLightSprite == null) this.counterLightSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/TimeOfDay/CounterLight.png");
        if (this.searchlightSprite == null) this.searchlightSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/Art/TimeOfDay/Searchlight.png");
        if (this.cityLightsMaterial == null) this.cityLightsMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/DystopiaPrototype/Art/TimeOfDay/CityLights.mat");
#endif

        // 1. 하늘 및 배경 레이어 자동 연결 및 FarBackground 바로 위 순서 정렬
        if (this.dawn == null) this.dawn = this.ensureLayerImage(root, "DawnBackground", this.dawnSprite, Vector2.zero, new Vector2(1280f, 720f), 1);
        else { this.dawn.transform.SetSiblingIndex(1); this.dawn.color = Color.white; }

        if (this.sunset == null) this.sunset = this.ensureLayerImage(root, "SunsetBackground", this.sunsetSprite, Vector2.zero, new Vector2(1280f, 720f), 2);
        else { this.sunset.transform.SetSiblingIndex(2); this.sunset.color = Color.white; }

        if (this.evening == null) this.evening = this.ensureLayerImage(root, "EveningBackground", this.eveningSprite, Vector2.zero, new Vector2(1280f, 720f), 3);
        else { this.evening.transform.SetSiblingIndex(3); this.evening.color = Color.white; }

        if (this.cityLights == null) this.cityLights = this.ensureLayerImage(root, "CityLights", this.cityLightsSprite, Vector2.zero, new Vector2(1280f, 720f), 4, this.cityLightsMaterial);
        else
        {
            this.cityLights.transform.SetSiblingIndex(4);
            this.cityLights.color = Color.white;
            if (this.cityLightsMaterial != null && (this.cityLights.material == null || this.cityLights.material == this.cityLights.defaultMaterial))
            {
                this.cityLights.material = this.cityLightsMaterial;
            }
        }

        Transform midBg = root.Find("MidBackground");
        if (midBg != null)
        {
            midBg.SetSiblingIndex(5);
        }

        // 2. 탐조등 빔 레이어 자동 연결 또는 생성
        if (this.leftBeam == null)
        {
            this.leftBeam = this.ensureBeam(root, "LeftBeam", this.searchlightSprite, new Vector2(110f, -164f), new Vector2(1800f, 360f), -7.67f);
        }
        else { this.leftBeam.color = Color.white; }

        if (this.rightBeam == null)
        {
            this.rightBeam = this.ensureBeam(root, "RightBeam", this.searchlightSprite, new Vector2(1140f, -156f), new Vector2(1800f, 366.67f), 186.29f);
        }
        else { this.rightBeam.color = Color.white; }

        // 3. 카운터 조명 레이어
        if (this.counterLight == null)
        {
            this.counterLight = this.ensureLayerImage(root, "CounterLight", this.counterLightSprite, new Vector2(355f, -455f), new Vector2(600f, 180f));
        }
        else { this.counterLight.color = Color.white; }

        // 4. 환경 틴트 대상 그래픽 수집
        if (this.environment == null || this.environment.Length == 0)
        {
            var list = new List<Graphic>();
            string[] names =
            {
                "FarBackground", "MidBackground", "CrowdBack", "CrowdMiddle", "CrowdFront",
                "LeftWatchTower", "RightWatchTower", "Barricade", "Canopy", "Counter"
            };
            foreach (string name in names)
            {
                Image img = findImage(root, name);
                if (img != null) list.Add(img);
            }
            if (list.Count > 0) this.environment = list.ToArray();
        }

        // 5. 인물 그래픽 수집
        if (this.people == null || this.people.Length == 0)
        {
            Transform customer = root.Find("Customer") ?? (root.parent != null ? root.parent.Find("Customer") : null);
            if (customer != null)
            {
                this.people = customer.GetComponentsInChildren<Graphic>(true);
            }
        }
    }

    private Image ensureLayerImage(Transform parent, string objectName, Sprite defaultSprite, Vector2 anchoredPos, Vector2 sizeDelta, int targetSiblingIndex = -1, Material defaultMaterial = null)
    {
        Transform child = parent.Find(objectName);
        Image img;
        if (child != null && child.TryGetComponent<Image>(out img))
        {
            if (img.sprite == null && defaultSprite != null) img.sprite = defaultSprite;
            if (defaultMaterial != null && (img.material == null || img.material == img.defaultMaterial)) img.material = defaultMaterial;
            img.color = Color.white;
            if (targetSiblingIndex >= 0 && targetSiblingIndex < parent.childCount)
            {
                img.transform.SetSiblingIndex(targetSiblingIndex);
            }
            return img;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        if (targetSiblingIndex >= 0 && targetSiblingIndex < parent.childCount)
        {
            go.transform.SetSiblingIndex(targetSiblingIndex);
        }

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        img = go.GetComponent<Image>();
        img.sprite = defaultSprite;
        if (defaultMaterial != null) img.material = defaultMaterial;
        img.raycastTarget = false;
        img.color = Color.white;
        img.canvasRenderer.SetColor(new Color(1f, 1f, 1f, 0f));
        return img;
    }

    private Image ensureBeam(Transform parent, string objectName, Sprite beamSprite, Vector2 anchoredPos, Vector2 sizeDelta, float zAngle)
    {
        Transform child = parent.Find(objectName);
        Image img;
        if (child != null && child.TryGetComponent<Image>(out img))
        {
            if (img.sprite == null && beamSprite != null) img.sprite = beamSprite;
            img.color = Color.white;
            return img;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
        rect.localRotation = Quaternion.Euler(0f, 0f, zAngle);

        img = go.GetComponent<Image>();
        img.sprite = beamSprite;
        img.raycastTarget = false;
        img.color = Color.white;
        img.canvasRenderer.SetColor(new Color(1f, 1f, 1f, 0f));
        return img;
    }

    private static Image findImage(Transform parent, params string[] names)
    {
        foreach (string n in names)
        {
            Transform child = parent.Find(n);
            if (child != null && child.TryGetComponent<Image>(out var img))
            {
                return img;
            }
        }
        return null;
    }

    /// <summary>현재 영업 시간을 읽어 TimeOfDay 레이어와 틴트를 갱신합니다.</summary>
    public void RefreshTime()
    {
        if (!Application.isPlaying && !this.previewInEditor)
        {
            this.Restore();
            return;
        }

        float hour = this.previewHour;
        if (Application.isPlaying)
        {
            if (this.debugOverrideTime)
            {
                hour = this.debugHour;
            }
            else
            {
                if (this.businessClock == null)
                {
                    this.businessClock = FindFirstObjectByType<BusinessClockController>();
                }
                if (this.businessClock != null)
                {
                    hour = this.businessClock.CurrentBusinessMinutes / 60f;
                }
            }
        }

        this.ApplyHour(hour);
    }

    /// <summary>공통 영업 시각 범위에 맞춰 각 레이어의 알파와 색상만 보간한다.</summary>
    /// <param name="hour">영업 시간 (시간 단위 소수점 포함, 예: 9.5는 09:30)</param>
    public void ApplyHour(float hour)
    {
        hour = Mathf.Clamp(hour, BusinessHours.OpenHour, BusinessHours.CloseHour);
        this.currentAppliedHour = hour;

        Vector3 weights = GetBlendWeights(hour, this.dayStart, this.sunsetStart, this.eveningStart);
        float morning = weights.x, sunsetBlend = weights.y, night = weights.z;

        setAlpha(this.sunset, sunsetBlend);
        setAlpha(this.dawn, morning);
        setAlpha(this.evening, night);
        setAlpha(this.cityLights, night * this.cityIntensity);
        setAlpha(this.leftBeam, night * this.beamIntensity);
        setAlpha(this.rightBeam, night * this.beamIntensity);
        setAlpha(this.counterLight, night * this.counterIntensity);

        Color tint = GetEnvironmentTint(weights, this.dawnTint, this.sunsetTint, this.nightTint);

        bool lit = this.pixelStage != null && this.pixelStage.IsRendering;
        if (lit)
        {
            this.pixelStage.SetTimeWeights(morning, sunsetBlend, night, Mathf.InverseLerp(BusinessHours.OpenHour, this.eveningStart, hour), hour);
        }

        applyTint(this.environment, lit ? Color.white : tint);
        applyTint(this.people, lit ? Color.white : Color.Lerp(Color.white, new Color(this.peopleBrightness, this.peopleBrightness, this.peopleBrightness), night));
    }

    /// <summary>UI와 실제 SpriteRenderer가 공유하는 기존 시간대 곡선.</summary>
    /// <param name="hour">표시 시각.</param><param name="dayStart">낮 경계.</param><param name="sunsetStart">석양 경계.</param><param name="eveningStart">야간 시작.</param>
    /// <returns>아침·석양·야간 가중치.</returns>
    public static Vector3 GetBlendWeights(float hour, float dayStart, float sunsetStart, float eveningStart) => new Vector3(
        1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BusinessHours.OpenHour, dayStart, hour)),
        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(sunsetStart, eveningStart, hour)),
        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(eveningStart, BusinessHours.CloseHour, hour)));

    /// <summary>원본 시간대 색조 보간을 두 렌더 경로에 동일 적용한다.</summary>
    /// <param name="weights">아침·석양·야간 가중치.</param><param name="dawn">아침 색.</param><param name="sunset">석양 색.</param><param name="night">밤 색.</param>
    /// <returns>환경에 곱할 시간대 색.</returns>
    public static Color GetEnvironmentTint(Vector3 weights, Color dawn, Color sunset, Color night) =>
        Color.Lerp(Color.Lerp(Color.Lerp(Color.white, dawn, weights.x), sunset, weights.y), night, weights.z);

    /// <summary>초기 주간 기본 상태로 복원합니다.</summary>
    public void Restore()
    {
        if (this.pixelStage != null)
        {
            this.pixelStage.SetTimeWeights(0f, 0f, 0f);
        }
        setAlpha(this.dawn, 0f);
        setAlpha(this.sunset, 0f);
        setAlpha(this.evening, 0f);
        setAlpha(this.cityLights, 0f);
        setAlpha(this.leftBeam, 0f);
        setAlpha(this.rightBeam, 0f);
        setAlpha(this.counterLight, 0f);
        applyTint(this.environment, Color.white);
        applyTint(this.people, Color.white);
    }

    private void OnDisable()
    {
        this.Restore();
    }

    private static void setAlpha(Image image, float alpha)
    {
        if (image != null)
        {
            float a = Mathf.Clamp01(alpha);
            image.canvasRenderer.SetColor(new Color(1f, 1f, 1f, a));
        }
    }

    private static void applyTint(Graphic[] graphics, Color color)
    {
        if (graphics == null) return;
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].canvasRenderer.SetColor(color);
            }
        }
    }

    /// <summary>배경 미리보기의 대표 시각만 순환한다.</summary>
    private void cycleNextPhase()
    {
        // 09:00 (아침) -> 12:00 (대낮) -> 16:30 (석양) -> 19:30 (저녁) -> 21:00 (마감) -> 09:00
        if (this.debugHour < 10.5f)
        {
            this.debugHour = 12f;
        }
        else if (this.debugHour < 14f)
        {
            this.debugHour = 16.5f;
        }
        else if (this.debugHour < 18f)
        {
            this.debugHour = 19.5f;
        }
        else if (this.debugHour < 20.5f)
        {
            this.debugHour = BusinessHours.CloseHour;
        }
        else
        {
            this.debugHour = BusinessHours.OpenHour;
        }
    }

    private string formatHour(float hour)
    {
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60f);
        return $"{h:00}:{m:00}";
    }

    private string getPhaseName(float hour)
    {
        if (hour < 12f) return "아침(Dawn)";
        if (hour < 15f) return "주간(Day)";
        if (hour < 18f) return "석양(Sunset)";
        return "야간(Night)";
    }
}
