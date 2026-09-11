using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>Canvas 밖의 배경과 손님 표시 공간을 카메라에 정렬한다. 진행·리소스·입력의 권위는 소유하지 않는다.</summary>
[DefaultExecutionOrder(100)]
[ExecuteAlways]
public sealed class WorldSceneView : MonoBehaviour
{
    /// <summary>준비된 게임 화면과 전면 영역. 이미지 계층은 복사하지 않고 viewport 기준만 참조한다.</summary>
    [SerializeField] private GameUIController controller;
    /// <summary>전면 화면 좌표를 월드로 투영할 직교 카메라.</summary>
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RectTransform frontView;
    [SerializeField] private BusinessClockController clock;
    /// <summary>전면 UI 로컬 픽셀 좌표로 authoring한 월드 Transform 루트.</summary>
    [SerializeField] private Transform renderRoot;
    /// <summary>이관된 원본 Sprite·시간대 역할. 런타임 Image를 복사하지 않는다.</summary>
    [SerializeField] private Layer[] layers;
    /// <summary>월드 뒤로 옮기면 매대에 가려지는 기존 조명/매대만 UI로 유지한다.</summary>
    [SerializeField] private Image counterLight;
    [SerializeField] private Graphic[] counterGraphics;
    [SerializeField] private float dayStart = 12f, sunsetStart = 15f, eveningStart = 18f;
    [SerializeField] private Color dawnTint = new Color(1f, .90f, .80f);
    [SerializeField] private Color sunsetTint = new Color(1f, .72f, .49f);
    [SerializeField] private Color nightTint = new Color(.38f, .43f, .56f);
    [SerializeField, Range(0, 1)] private float peopleBrightness = .72f;
    [SerializeField, Range(0, 1)] private float cityIntensity = .7f, beamIntensity = .11f, counterIntensity = .12f;
    /// <summary>표현만 바꾸는 테스트 옵션. 실제 시계와 진행에는 쓰지 않는다.</summary>
    [SerializeField] private bool debugOverrideTime, enableDebugKeys, autoAdvanceClockForTesting;
    [SerializeField, Range(9, 21)] private float debugHour = BusinessHours.OpenHour;
    [SerializeField, Min(.1f)] private float fullCycleSeconds = 45f;
    /// <summary>전용 material의 외부 시간만 갱신할 새·안개. 다른 MPB 소유자를 함께 붙이지 않는다.</summary>
    [SerializeField] private SpriteRenderer[] timedEffects = Array.Empty<SpriteRenderer>();
    /// <summary>원본의 4개 연기 프레임과 좌우 굴뚝 배치.</summary>
    [SerializeField] private Sprite[] smokeFrames = Array.Empty<Sprite>();
    [SerializeField] private Smoke[] smoke = Array.Empty<Smoke>();
    /// <summary>좌우 경비병과 총구. 수치는 원본 표현이며 거래 판정과 무관하다.</summary>
    [SerializeField] private Guard[] guards = Array.Empty<Guard>();
    private MaterialPropertyBlock effectBlock;
    private bool effectsInitialized;
    private float effectSeconds;
    private readonly Vector3[] corners = new Vector3[4];
    private CanvasGroup[] opacityGroups;

    /// <summary>현재 전면의 최종 표시 alpha. 전환 슬라이드 중에는 전면을 계속 표시한다.</summary>
    public float Opacity { get; private set; }
    /// <summary>퇴장 색과 합성할 현재 인물 시간대 색조.</summary>
    public Color PeopleTint { get; private set; } = Color.white;
    /// <summary>픽셀 좌표를 유지하는 실제 월드 Transform.</summary>
    public Transform RenderRoot => renderRoot;
    /// <summary>같은 준비 경계와 진행을 관찰하는 화면.</summary>
    public GameUIController Controller => controller;
    /// <summary>실제로 적용한 시각. 게임 시간을 바꾸지 않는다.</summary>
    public float CurrentAppliedHour { get; private set; } = BusinessHours.OpenHour;

    /// <summary>첫 프레임의 미연결 표시를 차단한다.</summary>
    private void Awake()
    {
        if (controller != null && worldCamera != null) Bind(controller, worldCamera);
        if (Application.isPlaying && renderRoot != null) renderRoot.gameObject.SetActive(false);
    }

    /// <summary>선택한 테스트 시간만 변경한다. 키 입력·자동 테스트는 기본 비활성이다.</summary>
    private void Update()
    {
        if (!Application.isPlaying) return;
        var keyboard = Keyboard.current;
        if (enableDebugKeys && keyboard != null)
        {
            if (keyboard.leftBracketKey.wasPressedThisFrame) PreviewHour(CurrentAppliedHour - 1);
            if (keyboard.rightBracketKey.wasPressedThisFrame) PreviewHour(CurrentAppliedHour + 1);
            if (keyboard.backslashKey.wasPressedThisFrame || keyboard.tKey.wasPressedThisFrame)
                PreviewHour(CurrentAppliedHour < 10.5f ? 12 : CurrentAppliedHour < 14 ? 16.5f : CurrentAppliedHour < 18 ? 19.5f : CurrentAppliedHour < 20.5f ? 21 : 9);
        }
        if (autoAdvanceClockForTesting)
            PreviewHour(BusinessHours.OpenHour + Mathf.Repeat((debugOverrideTime ? debugHour : CurrentAppliedHour) - BusinessHours.OpenHour +
                Time.unscaledDeltaTime * (BusinessHours.CloseHour - BusinessHours.OpenHour) / Mathf.Max(.1f, fullCycleSeconds),
                BusinessHours.CloseHour - BusinessHours.OpenHour));
    }

    /// <summary>UI 갱신 이후 viewport와 시간대 표시만 갱신한다.</summary>
    private void LateUpdate()
    {
        RefreshPresentation();
        if (Application.isPlaying) AdvanceEffects(Time.unscaledDeltaTime);
    }

    /// <summary>꺼진 씬 표시가 월드에 남거나 UI tint를 점유하지 않도록 정리한다.</summary>
    private void OnDisable()
    {
        Opacity = 0;
        if (renderRoot != null) renderRoot.gameObject.SetActive(false);
        if (counterLight != null) counterLight.canvasRenderer.SetColor(new Color(1, 1, 1, 0));
        if (counterGraphics != null) foreach (var graphic in counterGraphics) if (graphic != null) graphic.canvasRenderer.SetColor(Color.white);
        foreach (var effect in timedEffects) if (effect != null) effect.SetPropertyBlock(null);
    }

    /// <summary>진행·시계 비간섭 배경 미리보기.</summary>
    /// <param name="hour">유한 표시 시각.</param>
    /// <exception cref="ArgumentOutOfRangeException">NaN 또는 무한값.</exception>
    public void PreviewHour(float hour)
    {
        if (float.IsNaN(hour) || float.IsInfinity(hour)) throw new ArgumentOutOfRangeException(nameof(hour));
        debugOverrideTime = true;
        debugHour = Mathf.Clamp(hour, BusinessHours.OpenHour, BusinessHours.CloseHour);
        RefreshPresentation();
    }

    /// <summary>테스트 override를 해제하고 기존 표시 시계를 다시 따른다.</summary>
    public void FollowClock() { debugOverrideTime = false; RefreshPresentation(); }

    /// <summary>실제 화면이 진행 가능한 시간만 원본 환경 연출에 누적한다. 수동 검사도 같은 경계를 사용한다.</summary>
    /// <param name="deltaSeconds">유한한 0 이상의 표현 경과 초.</param>
    /// <exception cref="ArgumentOutOfRangeException">유효하지 않은 경과 시간.</exception>
    public void AdvanceEffects(float deltaSeconds)
    {
        if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        var currentDay = controller != null ? controller.CurrentDayProgress : null;
        if (!isActiveAndEnabled || currentDay == null) return;
        if (!effectsInitialized)
        {
            effectsInitialized = true;
            for (int i = 0; i < guards.Length; i++) { guards[i].NextShot = 3 + i * 3; guards[i].FlashUntil = 0; }
            // 원본 idleSeconds처럼 씬 수명 동안 유지한다. 매일 초기화하면 30초 영업에서 경비병 발사가 불가능하다.
            deltaSeconds = 0;
        }
        bool canAdvance = Opacity > 0 && renderRoot != null && renderRoot.gameObject.activeInHierarchy && !controller.IsPresentationPaused;
        if (canAdvance) effectSeconds += deltaSeconds;
        // 재활성 중 pause라도 MPB에는 마지막 시간을 복원하여 shader 기본 시간으로 점프하지 않는다.
        if (effectBlock == null) effectBlock = new MaterialPropertyBlock();
        foreach (var effect in timedEffects)
        {
            if (effect == null) continue;
            effect.GetPropertyBlock(effectBlock);
            effectBlock.SetFloat("_UsePresentationTime", 1);
            effectBlock.SetFloat("_PresentationSeconds", effectSeconds);
            effect.SetPropertyBlock(effectBlock);
            effectBlock.Clear();
        }
        for (int i = 0; i < smoke.Length; i++)
        {
            var item = smoke[i];
            if (item.Renderer == null || smokeFrames.Length != 4) continue;
            item.Renderer.sprite = smokeFrames[Mathf.FloorToInt(effectSeconds / (1.1f + i * .13f) + i * 1.7f) % 4];
            item.Renderer.transform.localPosition = item.Origin + new Vector3(Mathf.Sin(effectSeconds * .28f + i) * 2, Mathf.Sin(effectSeconds * .4f + i) * 1.5f, 0);
        }
        for (int i = 0; i < guards.Length; i++)
        {
            var guard = guards[i];
            if (guard.Root == null || guard.Flash == null) continue;
            float turnPhase = effectSeconds * (.12f + i * .012f) + i * 1.7f;
            float turn = Mathf.Sin(turnPhase);
            float facing = Mathf.Sign(turn) * Mathf.Lerp(.12f, 1, Mathf.SmoothStep(0, 1, Mathf.Abs(turn)));
            float outward = i == 0 ? -1 : 1;
            if (canAdvance && effectSeconds >= guard.NextShot && facing * outward > .9f)
            {
                guard.FlashUntil = effectSeconds + .12f;
                guard.NextShot = effectSeconds + 9 + i * 3.7f;
            }
            bool firing = effectSeconds < guard.FlashUntil;
            guard.Root.localScale = new Vector3(facing, 1, 1);
            float bob = Mathf.Sin(effectSeconds * 2.6f + i * 1.3f) * .65f * Mathf.Abs(Mathf.Cos(turnPhase));
            guard.Root.localPosition = guard.Origin + new Vector3(turn * 2 - (firing ? outward : 0), bob, 0);
            guard.Flash.localPosition = guard.Root.localPosition + new Vector3(facing * (guard.WidthPixels * .5f + 3), -guard.HeightPixels * .35f, 0);
            guard.Flash.localScale = new Vector3(outward, 1, 1);
            guard.Flash.gameObject.SetActive(firing);
        }
    }

    /// <summary>씬 조립 또는 테스트에서 이미 준비된 UI·카메라를 연결한다.</summary>
    /// <param name="ui">이미지 준비와 모델을 소유한 UI.</param><param name="camera">현재 orthographic 카메라.</param>
    /// <exception cref="ArgumentException">필수 UI/카메라 누락 또는 perspective 카메라.</exception>
    public void Bind(GameUIController ui, Camera camera)
    {
        if (ui == null || camera == null || !camera.orthographic) throw new ArgumentException("World view requires UI and orthographic camera.");
        controller = ui; worldCamera = camera;
        frontView = ui.FrontView;
        if (frontView == null) throw new ArgumentException("World view requires a front view.", nameof(ui));
        clock = ui.BusinessClock;
        counterLight = frontView.Find("CounterLight")?.GetComponent<Image>();
        counterGraphics = new Graphic[] { frontView.Find("Counter")?.GetComponent<Graphic>() };
        opacityGroups = frontView.GetComponentsInParent<CanvasGroup>(true);
    }

    /// <summary>씬 재활성·카메라/화면비 변경도 같은 정렬 경로를 사용한다.</summary>
    public void RefreshPresentation()
    {
        if (renderRoot == null) return;
        bool visible = controller != null && frontView != null && worldCamera != null && worldCamera.orthographic &&
            (!Application.isPlaying || (controller.isActiveAndEnabled && controller.CurrentDayProgress != null && frontView.gameObject.activeInHierarchy));
        renderRoot.gameObject.SetActive(visible);
        Opacity = visible ? 1 : 0;
        if (!visible) return;
        if (opacityGroups == null) opacityGroups = frontView.GetComponentsInParent<CanvasGroup>(true);
        foreach (var group in opacityGroups)
        {
            if (group == null || !group.isActiveAndEnabled) continue;
            Opacity *= group.alpha;
            if (group.ignoreParentGroups) break;
        }
        frontView.GetWorldCorners(corners);
        var canvas = frontView.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        // UI 레이어를 추적/복제하지 않는다. 전면 사각형의 화면 좌표만 카메라 viewport로 변환한다.
        float depth = Mathf.Max(worldCamera.nearClipPlane + 1, 10);
        Vector3 origin = ScreenToWorld(worldCamera, RectTransformUtility.WorldToScreenPoint(uiCamera, corners[1]), depth);
        Vector3 bottomLeft = ScreenToWorld(worldCamera, RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]), depth);
        Vector3 topRight = ScreenToWorld(worldCamera, RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]), depth);
        Vector3 localSize = worldCamera.transform.InverseTransformVector(topRight - bottomLeft);
        if (frontView.rect.width <= 0 || frontView.rect.height <= 0) { renderRoot.gameObject.SetActive(false); Opacity = 0; return; }
        renderRoot.SetPositionAndRotation(origin, worldCamera.transform.rotation);
        renderRoot.localScale = new Vector3(localSize.x / frontView.rect.width, localSize.y / frontView.rect.height, 1);
        CurrentAppliedHour = debugOverrideTime ? debugHour : clock != null ? clock.CurrentBusinessMinutes / 60f : BusinessHours.OpenHour;
        Vector3 weights = TimeOfDayUIController.GetBlendWeights(CurrentAppliedHour, dayStart, sunsetStart, eveningStart);
        Color tint = TimeOfDayUIController.GetEnvironmentTint(weights, dawnTint, sunsetTint, nightTint);
        PeopleTint = Color.Lerp(Color.white, new Color(peopleBrightness, peopleBrightness, peopleBrightness), weights.z);
        if (layers == null) return;
        foreach (var layer in layers)
        {
            if (layer == null || layer.Renderer == null) continue;
            float phaseAlpha = layer.Phase switch { Phase.Dawn => weights.x, Phase.Sunset => weights.y,
                Phase.Night => weights.z, Phase.CityLights => weights.z * cityIntensity, Phase.Beam => weights.z * beamIntensity, _ => 1 };
            Color color = layer.Color * (layer.Environment ? tint : Color.white);
            color.a *= phaseAlpha * Opacity;
            layer.Renderer.color = color;
        }
        if (counterLight != null) counterLight.canvasRenderer.SetColor(new Color(1, 1, 1, weights.z * counterIntensity));
        if (counterGraphics != null) foreach (var graphic in counterGraphics) if (graphic != null) graphic.canvasRenderer.SetColor(tint);
    }

    /// <summary>카메라 pixelRect를 고려해 화면 지점을 고정 depth의 월드 지점으로 변환한다.</summary>
    /// <param name="camera">orthographic 카메라.</param><param name="screen">화면 픽셀.</param><param name="depth">카메라 전방 거리.</param>
    /// <returns>viewport에 대응하는 월드 좌표.</returns>
    public static Vector3 ScreenToWorld(Camera camera, Vector2 screen, float depth) => camera.ViewportToWorldPoint(
        new Vector3((screen.x - camera.pixelRect.x) / camera.pixelRect.width, (screen.y - camera.pixelRect.y) / camera.pixelRect.height, depth));

    /// <summary>배경의 시간대 alpha 역할. 데이터 enum이 아니다.</summary>
    public enum Phase { Constant, Dawn, Sunset, Night, CityLights, Beam }
    /// <summary>원본 굴뚝 사각형의 중심을 유지하면서 Sprite만 교체한다.</summary>
    [Serializable]
    private sealed class Smoke
    {
        public SpriteRenderer Renderer;
        public Vector3 Origin;
    }
    /// <summary>Sprite 크기는 자식에 두고 루트는 원본 top-center 피벗으로 회전·이동한다.</summary>
    [Serializable]
    private sealed class Guard
    {
        public Transform Root, Flash;
        public Vector3 Origin;
        public float WidthPixels, HeightPixels;
        [NonSerialized] public float NextShot, FlashUntil;
    }
    /// <summary>일회성 이관한 원본 Sprite·색·레이어 역할. UI Image 참조를 보관하지 않는다.</summary>
    [Serializable]
    public sealed class Layer
    {
        public SpriteRenderer Renderer;
        public Color Color = Color.white;
        public bool Environment;
        public Phase Phase;
    }
}
