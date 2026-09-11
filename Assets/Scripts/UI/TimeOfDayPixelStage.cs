using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 UI 배치를 월드 메시로 렌더링하고 시간대별 조명을 거친 장면을 Point 확대합니다.</summary>
[ExecuteAlways, DefaultExecutionOrder(10000)]
public sealed class TimeOfDayPixelStage : MonoBehaviour
{
    /// <summary>표면 형태와 기존 특수 효과 유지 여부입니다.</summary>
    public enum Surface { Unlit, Environment, Person, Metal, CityLights, OriginalEffect, Hidden, Sky }
    /// <summary>Inspector에서 편집하는 원본 그래픽과 그 표면 반응입니다.</summary>
    [Serializable] public sealed class Layer
    {
        public Graphic source;
        public Surface surface;
        /// <summary>이 Sprite가 표시될 때만 사용하는 RGB tangent-space 노멀맵입니다.</summary>
        public Sprite normalSprite;
        public Texture2D normalMap;
        /// <summary>실내 표면의 넓은 반사광 반응입니다. 기존 직접광 값은 보존합니다.</summary>
        [Range(0, 2)] public float roomResponse;
        [Range(0, 1.5f)] public float lampResponse = 1;
        /// <summary>바닥에 놓인 소품의 하단만 어둡게 하는 접촉 명암 강도입니다.</summary>
        [Range(0, 1)] public float bottomShade;
        /// <summary>원본 Sprite의 정규화된 접점 X/Y와 그림자 폭/높이입니다. 높이 0은 미사용입니다.</summary>
        public Vector4 contactShadow;
        [NonSerialized] internal MeshRenderer contactRenderer;
        [NonSerialized] internal TimeOfDayPixelSource capture;
        [NonSerialized] internal MeshRenderer renderer;
        [NonSerialized] internal MeshFilter filter;
        [NonSerialized] internal MaterialPropertyBlock properties;
        [NonSerialized] internal RectMask2D[] masks;
    }

    /// <summary>정면 장면이 숨겨지면 렌더도 정지합니다.</summary>
    public RectTransform frontCanvas;
    /// <summary>표시 순서대로 지정한 배경·손님·소품입니다. 텍스트와 계산기는 제외합니다.</summary>
    public Layer[] layers = Array.Empty<Layer>();
    /// <summary>픽셀 렌더용 셰이더이며 빌드 참조를 명시적으로 보존합니다.</summary>
    public Shader lightingShader;
    /// <summary>16:9 장면의 실제 렌더 너비입니다. 높이는 자동 계산합니다.</summary>
    [Range(256, 640)] public int width = 480;
    /// <summary>편집 중에도 시간대와 표면 반응을 확인합니다.</summary>
    public bool previewInEditor = true;
    /// <summary>1280×720 가판 화면 좌상단 기준 전등 위치, 높이와 영향 반경입니다.</summary>
    public Vector2 lampPosition = new Vector2(700, 290);
    public float lampHeight = 240, lampRadius = 520;
    /// <summary>밤 가판 전등의 강도와 색입니다.</summary>
    [Range(0, 6)] public float lampIntensity = 3.2f;
    public Color lampColor = new Color(1f, .76f, .46f);
    /// <summary>밝기 단계와 역광 가장자리 강도입니다.</summary>
    [Range(2, 12)] public int lightingSteps = 6;
    [Range(0, 3)] public float rimIntensity = 1.3f;

    /// <summary>아침·석양·밤 안개의 색이며 원본 알파와 흐름은 유지합니다.</summary>
    public Color dawnFog = new Color(1f, .84f, .67f), sunsetFog = new Color(1f, .54f, .28f), nightFog = new Color(.28f, .39f, .59f);
    /// <summary>구름 사이로 새는 빛의 추가 강도입니다.</summary>
    [Range(0, 1)] public float skyGlowIntensity = .3f;

    /// <summary>낮의 주변광을 낮추고 방향광을 강화하는 정도입니다. 0이면 기존 낮 명암을 유지합니다.</summary>
    [Range(0, 1)] public float daylightContrast = .65f;
    /// <summary>석양의 밝은 면과 그늘 사이 대비입니다. 밤으로 전환되면 추가 대비는 사라집니다.</summary>
    [Range(0, 1)] public float sunsetContrast = .8f;
    /// <summary>낮에 햇빛 반대쪽으로 가판에 드리우는 손님 그림자의 불투명도입니다.</summary>
    [Range(0, 1)] public float daylightShadowOpacity = .3f;
    /// <summary>석양에 가판에 드리우는 손님 그림자의 불투명도입니다. 기존 야간 그림자와 별도입니다.</summary>
    [Range(0, 1)] public float sunsetShadowOpacity = .55f;

    // 시간별 곡선은 Scene에 저장하며 전용 편집 창에서만 표시합니다. 배율 1은 기존 설정을 유지합니다.
    [SerializeField, HideInInspector] private AnimationCurve hourlyAmbient = AnimationCurve.Linear(9, 1, 21, 1);
    [SerializeField, HideInInspector] private AnimationCurve hourlySunlight = AnimationCurve.Linear(9, 1, 21, 1);
    [SerializeField, HideInInspector] private AnimationCurve hourlyNormal = AnimationCurve.Linear(9, 1, 21, 1);
    // 경계 1은 야간과 같은 또렷한 단계 명암이며, 최소 밝기는 낮 인물의 무늬 보존용입니다.
    [SerializeField, HideInInspector] private AnimationCurve hourlyHardness = AnimationCurve.Linear(9, 1, 21, 1);
    [SerializeField, HideInInspector] private AnimationCurve hourlyFill = AnimationCurve.Linear(9, .15f, 21, .15f);
    [SerializeField, HideInInspector] private AnimationCurve hourlyShadow = AnimationCurve.Linear(9, 1, 21, 1);
    // 자동 태양 궤도에 더하는 화면 픽셀 오프셋이며 X는 오른쪽, Y는 위쪽이 양수입니다.
    [SerializeField, HideInInspector] private AnimationCurve hourlySunX = AnimationCurve.Linear(9, 0, 21, 0);
    [SerializeField, HideInInspector] private AnimationCurve hourlySunY = AnimationCurve.Linear(9, 0, 21, 0);

    /// <summary>끄면 이번 노멀맵·실내 반사광 시험을 모두 해제하고 기존 표현으로 돌아갑니다.</summary>
    public bool relightingTrial = true;
    /// <summary>인물 노멀맵만 별도로 비교합니다. 매칭된 Sprite 한 명에만 적용됩니다.</summary>
    public bool useCustomerNormalMap = true;
    [Range(0, 1)] public float normalStrength = .65f;
    /// <summary>노멀맵이 연결된 금속 소품의 강도입니다. 인물 강도와 별도로 Inspector에서 조절합니다.</summary>
    [Range(0, 1)] public float propNormalStrength = 1f;
    /// <summary>저녁·밤 소품의 최소 조명 밝기입니다. 광원 및 물건의 위치는 변경하지 않습니다.</summary>
    [Range(0, 1)] public float propNightFill = .3f;
    /// <summary>직접광 반경 바깥의 천막·기둥·가판에 퍼지는 반사광 세기입니다.</summary>
    [Range(0, 3)] public float roomLightStrength = 1.2f;

    /// <summary>끄면 추가 스포트라이트와 공기 중 빛줄기를 함께 해제합니다.</summary>
    public bool eveningSpotlight = true;
    /// <summary>1280×720 좌상단 기준 광원과 가판 위 목표 위치입니다.</summary>
    public Vector2 spotOrigin = new Vector2(1050, -60), spotTarget = new Vector2(600, 600);
    /// <summary>집중광 세기와 원뿔의 반각(도)입니다.</summary>
    [Range(0, 8)] public float spotIntensity = 4.5f;
    [Range(5, 45)] public float spotHalfAngle = 21;
    /// <summary>공기 중에 보이는 빛줄기의 불투명도입니다.</summary>
    [Range(0, .3f)] public float spotHaze = .025f;

    /// <summary>감시탑 양쪽의 인물 역광 세기입니다. 0이면 이전 인물 조명을 유지합니다.</summary>
    [Range(0, 6)] public float towerBacklight = 3.2f;
    /// <summary>가판 위 인물 투영 그림자 불투명도입니다. 0이면 해제합니다.</summary>
    [Range(0, 1)] public float customerShadowOpacity = .65f;
    /// <summary>1280×720 좌상단 기준 가판 상판의 뒤·앞 경계입니다.</summary>
    public Vector2 shadowTableY = new Vector2(520, 625);
    private TimeOfDayUIController dayNight;

    private Mesh beamMesh;

    private GameObject renderRoot, outputRoot;
    private Camera renderCamera;
    private RawImage output;
    private RenderTexture texture;
    private Material material;
    /// <summary>접촉 그림자가 공유하며 Release에서 해제하는 단위 사각형입니다.</summary>
    private Mesh contactMesh;
    private float dawnWeight, sunsetWeight, nightWeight;
    /// <summary>시간 소유자가 전달하는 일출~일몰 진행도입니다. 0은 화면 왼쪽, 1은 오른쪽입니다.</summary>
    private float sunProgress = .5f;
    // 곡선 평가에 사용하는 기존 영업 시각(시)과 이번 프레임의 노멀 강도입니다.
    private float lightingHour = 12, evaluatedNormalStrength;
    private readonly Vector3[] corners = new Vector3[4];
    /// <summary>기존 시간대 색 곱셈을 대체할 준비가 된 경우 true입니다.</summary>
    public bool IsRendering => isActiveAndEnabled && lightingShader != null && (Application.isPlaying || previewInEditor);

    /// <summary>시간 권위는 기존 DayNight에 유지하고 이 컴포넌트는 표현만 담당합니다.</summary>
    /// <param name="dawn">기존 시간 시스템이 계산한 아침 가중치입니다.</param>
    /// <param name="sunset">기존 시간 시스템이 계산한 석양 가중치입니다.</param>
    /// <param name="night">기존 시간 시스템이 계산한 밤 가중치입니다.</param>
    /// <param name="sunPosition">일출~일몰의 정규화된 진행도이며 기본값은 정오 위치입니다.</param>
    /// <param name="hour">기존 영업 시계 또는 미리보기 시각(시)입니다.</param>
    public void SetTimeWeights(float dawn, float sunset, float night, float sunPosition = .5f, float hour = 12)
    {
        dawnWeight = dawn; sunsetWeight = sunset; nightWeight = night;
        sunProgress = Mathf.Clamp01(sunPosition);
        lightingHour = hour;
    }

    /// <summary>임시 월드 렌더와 출력을 생성하고 원본 배치에 맞춥니다.</summary>
    private void LateUpdate()
    {
        if (frontCanvas == null) frontCanvas = transform as RectTransform;
        if (!IsRendering || frontCanvas == null) { Release(); return; }
        if (texture != null && texture.width != width) Release();
        if (renderRoot == null) Build();
        bool visible = frontCanvas.gameObject.activeInHierarchy;
        output.enabled = visible;
        renderCamera.enabled = visible;
        foreach (var layer in layers) if (layer.capture != null) layer.capture.SetCapture(true);
        Canvas.ForceUpdateCanvases();
        // 화면 전환으로 UI가 재활성화된 프레임에도 최신 시각의 색·알파·조명을 함께 반영합니다.
        if (dayNight != null && dayNight.isActiveAndEnabled) dayNight.RefreshTime();
        UpdateLights();
        for (int i = 0; i < layers.Length; i++) Sync(layers[i], i);
        if (visible && renderCamera != null)
        {
            renderCamera.Render();
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
    }

    /// <summary>기존 카메라나 URP 설정을 바꾸지 않고 격리된 월드 영역에 렌더 경로를 만듭니다.</summary>
    private void Build()
    {
        dayNight = GetComponent<TimeOfDayUIController>();
        if (dayNight == null) dayNight = GetComponentInParent<TimeOfDayUIController>();
        if (dayNight == null) dayNight = GetComponentInChildren<TimeOfDayUIController>();
        renderRoot = new GameObject("PixelStage Runtime Meshes") { hideFlags = HideFlags.HideAndDontSave };
        renderRoot.transform.position = new Vector3(10000, 10000, 0);
        material = new Material(lightingShader) { hideFlags = HideFlags.HideAndDontSave };
        var cameraObject = new GameObject("PixelStage Camera") { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
        cameraObject.transform.SetParent(renderRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(640, -360, -50);
        renderCamera = cameraObject.AddComponent<Camera>();
        renderCamera.orthographic = true; renderCamera.orthographicSize = 360; renderCamera.aspect = 16f / 9f;
        renderCamera.cullingMask = 1 << 31; renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = Color.black; renderCamera.allowMSAA = false; renderCamera.allowHDR = false; renderCamera.depth = -100;
        texture = new RenderTexture(width, Mathf.RoundToInt(width * 9f / 16f), 24) { name = "Pixel stage output", filterMode = FilterMode.Point, antiAliasing = 1, hideFlags = HideFlags.HideAndDontSave };
        texture.Create(); renderCamera.targetTexture = texture;
        outputRoot = new GameObject("PixelStage Output", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)) { hideFlags = HideFlags.HideAndDontSave };
        var canvas = outputRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 0;
        var scaler = outputRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        var imageObject = new GameObject("Point Upscale", typeof(RectTransform), typeof(RawImage)) { hideFlags = HideFlags.HideAndDontSave };
        imageObject.transform.SetParent(outputRoot.transform, false); output = imageObject.GetComponent<RawImage>(); output.texture = texture; output.raycastTarget = false;
        output.rectTransform.anchorMin = Vector2.zero; output.rectTransform.anchorMax = Vector2.one; output.rectTransform.offsetMin = output.rectTransform.offsetMax = Vector2.zero;
        contactMesh = new Mesh { name = "Contact shadow quad", hideFlags = HideFlags.HideAndDontSave };
        contactMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        contactMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        contactMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        contactMesh.RecalculateBounds();
        foreach (var layer in layers)
        {
            if (layer.source == null) continue;
            layer.capture = layer.source.GetComponent<TimeOfDayPixelSource>();
            if (layer.capture == null) layer.capture = layer.source.gameObject.AddComponent<TimeOfDayPixelSource>();
            var go = new GameObject(layer.source.name, typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
            go.transform.SetParent(renderRoot.transform, false);
            layer.filter = go.GetComponent<MeshFilter>(); layer.renderer = go.GetComponent<MeshRenderer>(); layer.properties = new MaterialPropertyBlock();
            layer.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; layer.renderer.receiveShadows = false;
            layer.renderer.sharedMaterial = layer.surface == Surface.OriginalEffect ? layer.source.material : material;
            layer.masks = layer.source.GetComponentsInParent<RectMask2D>();
            layer.capture.SetCapture(true);

        }
        // 인물 뒤에 빛줄기를 놓아 얼굴·옷의 대비를 흰 막으로 덮지 않습니다.
        var beam = new GameObject("Evening spotlight beam", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
        beam.transform.SetParent(renderRoot.transform, false);
        beamMesh = new Mesh { name = "Spotlight quad", hideFlags = HideFlags.HideAndDontSave };
        beamMesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1280, 0, 0), new Vector3(1280, -720, 0), new Vector3(0, -720, 0) };
        beamMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        beamMesh.RecalculateBounds();
        beam.GetComponent<MeshFilter>().sharedMesh = beamMesh;
        var beamRenderer = beam.GetComponent<MeshRenderer>();
        beamRenderer.sharedMaterial = material;
        int firstPerson = Array.FindIndex(layers, layer => layer.surface == Surface.Person);
        beamRenderer.sortingOrder = Mathf.Max(0, firstPerson) * 2 - 1;
        var beamProperties = new MaterialPropertyBlock();
        beamProperties.SetFloat("_Surface", 8);
        beamRenderer.SetPropertyBlock(beamProperties);
    }

    /// <summary>월드 렌더 좌표로 변환할 원본 화면 점을 계산합니다.</summary>
    /// <param name="source">원본 그래픽입니다.</param>
    /// <param name="point">원본의 로컬 점입니다.</param>
    /// <returns>1280×720 디자인 좌표입니다.</returns>
    private Vector2 ScreenPoint(Graphic source, Vector3 point)
    {
        // 비활성 가판의 Graphic.canvas 캐시가 비어도 저장된 부모 Canvas는 존재합니다.
        var canvas = source.canvas != null ? source.canvas : source.GetComponentInParent<Canvas>(true);
        Vector2 pixel = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, source.transform.TransformPoint(point));
        return new Vector2(pixel.x / Mathf.Max(1, Screen.width) * 1280, pixel.y / Mathf.Max(1, Screen.height) * 720 - 720);
    }

    /// <summary>애니메이션, 알파, 마스크, Sprite 교체를 원본에서 매 프레임 반영합니다.</summary>
    /// <param name="layer">편집 가능한 레이어입니다.</param>
    /// <param name="order">최종 그리기 순서입니다.</param>
    private void Sync(Layer layer, int order)
    {
        if (layer.renderer == null || layer.source == null) return;
        // 최초 생성 이후 Inspector/Scene에서 그림자가 켜져도 현재 설정대로 생성합니다.
        if (layer.contactShadow.w > 0 && layer.contactRenderer == null)
        {
            var shadow = new GameObject(layer.source.name + " Contact Shadow", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
            shadow.transform.SetParent(renderRoot.transform, false);
            shadow.GetComponent<MeshFilter>().sharedMesh = contactMesh;
            layer.contactRenderer = shadow.GetComponent<MeshRenderer>();
            layer.contactRenderer.sharedMaterial = material;
            layer.contactRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            layer.contactRenderer.receiveShadows = false;
        }
        var source = layer.source;
        bool visible = source.isActiveAndEnabled && layer.capture.isActiveAndEnabled && layer.capture.CapturedMesh != null && layer.surface != Surface.Hidden;
        layer.renderer.enabled = visible;
        if (layer.contactRenderer != null) layer.contactRenderer.enabled = visible && layer.contactShadow.w > 0;
        if (!visible) return;
        layer.filter.sharedMesh = layer.capture.CapturedMesh;
        Vector2 p = ScreenPoint(source, Vector3.zero), x = ScreenPoint(source, Vector3.right) - p, y = ScreenPoint(source, Vector3.up) - p;
        var t = layer.renderer.transform; t.localPosition = p;
        t.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(x.y, x.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(x.magnitude, y.magnitude * Mathf.Sign(x.x * y.y - x.y * y.x), 1);
        layer.renderer.sortingOrder = order * 2;
        var block = layer.properties; block.Clear();
        block.SetFloat("_ContactShadow", 0);
        // 기존 감시탑 다리의 녹 색 제거를 새 조명에서도 먼저 적용합니다.
        bool neutral = source.material.HasProperty("_GrayRegion") && source.material.HasProperty("_Brightness");
        block.SetFloat("_UseNeutralRegion", neutral ? 1 : 0);
        if (neutral)
        {
            block.SetVector("_NeutralRegion", source.material.GetVector("_GrayRegion"));
            block.SetFloat("_NeutralBrightness", source.material.GetFloat("_Brightness"));
        }
        Texture main = source.mainTexture; block.SetTexture("_MainTex", main); block.SetVector("_MainTex_TexelSize", new Vector4(1f / main.width, 1f / main.height, main.width, main.height));
        Color tint = source.canvasRenderer.GetColor();
        if (layer.surface == Surface.OriginalEffect)
        {
            Color fog = Color.Lerp(Color.Lerp(Color.Lerp(Color.white, dawnFog, dawnWeight), sunsetFog, sunsetWeight), nightFog, nightWeight);
            tint *= fog;
            if (source.material.HasProperty("_Color")) tint *= source.material.GetColor("_Color");
        }
        block.SetColor("_Tint", tint); block.SetColor("_Color", tint); block.SetFloat("_Surface", (float)layer.surface);
        block.SetFloat("_LampStrength", lampIntensity * NightWeight() * layer.lampResponse);
        block.SetFloat("_RimStrength", rimIntensity * (layer.surface == Surface.Person ? .7f : layer.surface == Surface.Metal ? .22f : .06f));
        var image = source as Image;
        bool mapped = relightingTrial && useCustomerNormalMap && layer.normalMap != null && image != null && image.sprite == layer.normalSprite;
        if (mapped) block.SetTexture("_NormalMap", layer.normalMap);
        // 소품과 인물은 각자 Inspector에 저장된 노멀 강도를 사용합니다.
        block.SetFloat("_NormalStrength", mapped ? (layer.surface == Surface.Metal ? propNormalStrength : evaluatedNormalStrength) : 0);
        block.SetFloat("_PropFill", mapped && layer.surface == Surface.Metal ? propNightFill * Mathf.Max(nightWeight, sunsetWeight * .35f) : 0);
        block.SetFloat("_BottomShade", layer.bottomShade);
        block.SetFloat("_RoomBounce", relightingTrial ? roomLightStrength * layer.roomResponse * Mathf.Lerp(.2f, 1, nightWeight) : 0);
        block.SetFloat("_SpotResponse", layer.surface == Surface.Person || mapped || layer.roomResponse > 0 ? 1 : 0);
        block.SetFloat("_ReceiveCustomerShadow", dayNight != null && dayNight.BusinessClock != null && source.name == "Counter" ? 1 : 0);
        if (layer.surface == Surface.Person && layer.normalSprite != null && image != null)
        {
            // 앞 손님 슬롯의 현재 알파를 사용하므로 손님 교체와 크기 변화도 따라갑니다.
            material.SetTexture("_CustomerSilhouette", source.mainTexture);
            Vector2 center = ScreenPoint(source, source.rectTransform.rect.center);
            float bodyWidth = source.rectTransform.rect.width * x.magnitude;
            material.SetVector("_ShadowBody", new Vector4(center.x, Mathf.Max(1, bodyWidth), -shadowTableY.x, Mathf.Max(1, shadowTableY.y - shadowTableY.x)));
        }
        Vector4 clip = new Vector4(-10000, -10000, 10000, 10000);
        foreach (var mask in layer.masks)
        {
            if (!mask.isActiveAndEnabled) continue;
            mask.rectTransform.GetWorldCorners(corners);
            Camera cam = source.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : source.canvas.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            clip = new Vector4(Mathf.Max(clip.x, a.x / Screen.width * 1280), Mathf.Max(clip.y, a.y / Screen.height * 720 - 720), Mathf.Min(clip.z, b.x / Screen.width * 1280), Mathf.Min(clip.w, b.y / Screen.height * 720 - 720));
        }
        block.SetVector("_ClipRect", clip); layer.renderer.SetPropertyBlock(block);
        if (layer.contactRenderer != null)
        {
            // UI 사각형의 여백이 아니라, 원본 Sprite에서 측정한 불투명 밑면에 접촉시킵니다.
            Rect drawing = source.GetPixelAdjustedRect();
            if (image != null && image.sprite != null && image.preserveAspect)
            {
                float aspect = image.sprite.rect.width / image.sprite.rect.height;
                if (aspect > drawing.width / drawing.height)
                {
                    float height = drawing.width / aspect;
                    drawing.y += (drawing.height - height) * source.rectTransform.pivot.y;
                    drawing.height = height;
                }
                else
                {
                    float width = drawing.height * aspect;
                    drawing.x += (drawing.width - width) * source.rectTransform.pivot.x;
                    drawing.width = width;
                }
            }
            Vector2 center = ScreenPoint(source, new Vector3(drawing.x + drawing.width * layer.contactShadow.x, drawing.y + drawing.height * layer.contactShadow.y, 0));
            // 밑면에 겹친 상태로 광원 반대쪽 상판으로 퍼지며 원본 Transform은 변경하지 않습니다.
            float spread = drawing.height * y.magnitude * layer.contactShadow.w;
            Vector4 lightOrigin = material.GetVector(nightWeight > .5f ? "_SpotOrigin" : "_SunShadowOrigin");
            float drift = Mathf.Clamp((center.x - lightOrigin.x) / Mathf.Max(100, lightOrigin.y - center.y), -1, 1);
            center += new Vector2(drift * spread * .35f, -spread * .22f);
            var shadowTransform = layer.contactRenderer.transform;
            shadowTransform.localPosition = center;
            shadowTransform.localRotation = Quaternion.identity;
            shadowTransform.localScale = new Vector3(drawing.width * x.magnitude * layer.contactShadow.z, drawing.height * y.magnitude * layer.contactShadow.w, 1);
            layer.contactRenderer.sortingOrder = order * 2 - 1;
            block.SetFloat("_ContactShadow", 1);
            // 그림자 메시가 이동해도 가장 진한 접촉부는 원본 밑면 좌표에 고정합니다.
            float shadowWidth = drawing.width * x.magnitude * layer.contactShadow.z;
            float shear = drift * spread * .35f / Mathf.Max(1, shadowWidth);
            block.SetVector("_ContactAnchor", new Vector4(.5f - shear, .72f, .9f / layer.contactShadow.z, shear));
            block.SetColor("_Tint", new Color(.035f, .025f, .018f, source.color.a * source.canvasRenderer.GetAlpha() * .95f));
            layer.contactRenderer.SetPropertyBlock(block);
        }
    }

    /// <summary>저녁의 가판 전등은 낮보다 강하게 켜집니다.</summary>
    private float NightWeight() { return Mathf.Lerp(.12f, 1, nightWeight); }

    /// <summary>네 시간대의 주변광·역광·전등을 같은 시각으로 혼합합니다.</summary>
    private void UpdateLights()
    {

        float dawn = dawnWeight;
        float sunset = sunsetWeight;
        float night = nightWeight;
        Color ambient = Color.Lerp(Color.Lerp(new Color(.86f,.87f,.88f),new Color(.62f,.53f,.43f),dawn),new Color(.44f,.30f,.25f),sunset);
        ambient = Color.Lerp(ambient, new Color(.16f,.21f,.32f),night);
        Color sun = Color.Lerp(Color.Lerp(new Color(.18f,.18f,.17f),new Color(1.35f,.88f,.4f),dawn),new Color(2.1f,.80f,.23f),sunset) * (1-night);
        // 새 낮·석양 명암은 밤에 0으로 수렴하여 사용자가 맞춘 야간 전등과 주변광을 보존합니다.
        float daylight = (1-sunset) * (1-night);
        float sunsetLight = sunset * (1-night);
        float daytimeContrast = relightingTrial ? daylightContrast * daylight + sunsetContrast * sunsetLight : 0;
        ambient *= 1 - daytimeContrast * .45f;
        sun += Color.Lerp(new Color(1.2f,1.25f,1.3f),new Color(1.2f,.5f,.18f),sunset) * daytimeContrast;
        ambient *= Mathf.Max(0, hourlyAmbient.Evaluate(lightingHour));
        sun *= Mathf.Max(0, hourlySunlight.Evaluate(lightingHour));
        evaluatedNormalStrength = Mathf.Clamp01(normalStrength * hourlyNormal.Evaluate(lightingHour));
        material.SetFloat("_AmbientSeconds", Time.realtimeSinceStartup);
        material.SetFloat("_RimWidthPixels", 3f);
        material.SetFloat("_SpotSoftness", 0.05f);
        material.SetFloat("_HighlightResponse", 1f);
        material.SetFloat("_SpecularResponse", 1f);
        material.SetColor("_Ambient", ambient); material.SetColor("_Sun", sun); material.SetColor("_LampColor", lampColor);
        // 낮의 얼굴·옷은 검게 뭉개지지 않도록 원본 무늬가 읽히는 보조광을 유지합니다.
        material.SetFloat("_DaylightDetail", relightingTrial ? (1-Mathf.Clamp01(hourlyHardness.Evaluate(lightingHour))) * (1-night) : 0);
        material.SetFloat("_DaylightFill", relightingTrial ? Mathf.Clamp01(hourlyFill.Evaluate(lightingHour)) * (1-night) : 0);
        // 방향광이 강한 시간에는 정면 보조광을 낮춰 반대쪽 면의 명암을 보존합니다.
        material.SetFloat("_KeyContrast", relightingTrial ? Mathf.Max(daytimeContrast, Mathf.Max(dawn, Mathf.Max(sunset, night))) : 0);
        material.SetVector("_LampPosition", new Vector4(lampPosition.x,-lampPosition.y,lampHeight,lampRadius));
        Vector2 direction = new Vector2(spotTarget.x - spotOrigin.x, spotOrigin.y - spotTarget.y).normalized;
        material.SetVector("_SpotOrigin", new Vector4(spotOrigin.x, -spotOrigin.y, lampHeight, 0));
        material.SetVector("_SpotDirection", new Vector4(direction.x, direction.y, Mathf.Cos(spotHalfAngle * Mathf.Deg2Rad), Vector2.Distance(spotOrigin, spotTarget) + 180));
        material.SetFloat("_SpotPower", eveningSpotlight ? spotIntensity * nightWeight : 0);
        material.SetFloat("_SpotHaze", eveningSpotlight ? spotHaze * nightWeight : 0);
        // 실제 감시탑 탐조등의 시작점과 인물 외곽광의 방향을 공유합니다.
        Vector2 leftTower = dayNight != null && dayNight.LeftBeam != null ? ScreenPoint(dayNight.LeftBeam, Vector3.zero) : new Vector2(110, -164);
        Vector2 rightTower = dayNight != null && dayNight.RightBeam != null ? ScreenPoint(dayNight.RightBeam, Vector3.zero) : new Vector2(1157, -216);
        material.SetVector("_TowerOrigins", new Vector4(leftTower.x, leftTower.y, rightTower.x, rightTower.y));
        material.SetFloat("_TowerPower", towerBacklight * Mathf.Max(nightWeight, sunsetWeight * .35f));
        material.SetFloat("_CustomerShadowOpacity", customerShadowOpacity * Mathf.Clamp01(towerBacklight) * Mathf.Max(nightWeight, sunsetWeight * .35f));
        // 실제 영업 시각을 사용해 정오 전후에도 멈추지 않고 동쪽(왼쪽)에서 서쪽으로 이동합니다.
        float elevation = Mathf.Sin(sunProgress * Mathf.PI);
        float skyX = Mathf.Lerp(.08f, .92f, sunProgress);
        float skyY = .64f + elevation * .3f;
        skyX += hourlySunX.Evaluate(lightingHour) / 1280;
        skyY += hourlySunY.Evaluate(lightingHour) / 720;
        // 밝은 면과 그림자가 동일한 광원을 따르며, 높은 정오에는 그림자의 옆방향 길이가 짧아집니다.
        material.SetVector("_SunShadowOrigin", new Vector4(skyX * 1280, (skyY - 1) * 720, 0, 0));
        material.SetFloat("_SunShadowOpacity", relightingTrial ? Mathf.Clamp01(Mathf.Lerp(daylightShadowOpacity, sunsetShadowOpacity, sunset) * Mathf.Max(0, hourlyShadow.Evaluate(lightingHour))) * (1-night) : 0);
        Vector4 sunDirection = new Vector4((skyX * 1280 - 640) / 400, ((skyY - 1) * 720 + 520) / 400, .3f, 0);
        float nightSkyX = Mathf.Lerp(Mathf.Lerp(.61f, .73f, dawn), .56f, sunset);
        material.SetVector("_SunDirection", Vector4.Lerp(sunDirection, new Vector4((nightSkyX - .5f) * 3f,.65f,.3f,0), night));
        material.SetVector("_SkyOrigin", new Vector4(skyX, skyY, dawn * 3 + sunset * 7, 0));
        Color skyColor = Color.Lerp(Color.Lerp(new Color(.83f,.9f,1),new Color(1,.83f,.58f),dawn),new Color(1,.58f,.22f),sunset);
        material.SetColor("_SkyGlow", skyColor * (skyGlowIntensity * (1-night)));
        material.SetFloat("_RimStrength", rimIntensity); material.SetFloat("_Steps", lightingSteps);
    }

    /// <summary>무효화 시 원본 UI를 즉시 복원하고 임시 GPU 리소스를 해제합니다.</summary>
    private void Release()
    {
        foreach (var layer in layers) if (layer.capture != null) layer.capture.SetCapture(false);
        if (renderCamera != null) renderCamera.targetTexture = null;
        if (outputRoot != null) DestroyImmediate(outputRoot);
        if (renderRoot != null) DestroyImmediate(renderRoot);
        if (beamMesh != null) DestroyImmediate(beamMesh);
        if (contactMesh != null) DestroyImmediate(contactMesh);
        if (texture != null) { texture.Release(); DestroyImmediate(texture); }
        if (material != null) DestroyImmediate(material);
    }
    /// <summary>정지와 재컴파일 때 원본 표시 및 렌더 자원을 복원합니다.</summary>
    private void OnDisable() { Release(); }
}
