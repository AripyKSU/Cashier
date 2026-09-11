using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정면 배경(AstraFrontView)의 굴뚝 연기 순환 애니메이션, 원경 군중 정점 변위,
/// 감시탑 경비병의 선회·발걸음 이동 및 주기적 외곽 사격 연출을 총괄 제어하는 컴포넌트입니다.
/// </summary>
public sealed class FrontBackgroundAnimationController : MonoBehaviour
{
    private const float MuzzleFlashDurationSeconds = 0.12f;
    private const float GuardBaseShotCooldownSeconds = 9f;
    private const float GuardShotIndexOffsetSeconds = 3.7f;

    [Header("Chimney Smoke")]
    [Tooltip("좌/우 굴뚝 연기 Image (0: 좌측, 1: 우측)")]
    [SerializeField] private Image[] chimneySmoke = new Image[2];
    [Tooltip("연기 프레임 애니메이션용 스프라이트 배열 (4장)")]
    [SerializeField] private Sprite[] chimneySmokeFrames;

    [Header("Watch Guards")]
    [Tooltip("좌/우 감시탑 경비병 RectTransform (0: 좌측, 1: 우측)")]
    [SerializeField] private RectTransform[] watchGuards = new RectTransform[2];
    [Tooltip("좌/우 감시탑 총구 불꽃 RectTransform (0: 좌측, 1: 우측)")]
    [SerializeField] private RectTransform[] watchMuzzleFlashes = new RectTransform[2];

    [Header("Crowd")]
    [Tooltip("후열·중열·전열 군중 정점 변위 이미지 컴포넌트")]
    [SerializeField] private FrontCrowdImage[] animatedCrowd = new FrontCrowdImage[3];

    [Header("Sky Birds")]
    [Tooltip("원경 하늘의 절차적 새 무리 비행 연출 컴포넌트")]
    [SerializeField] private FrontSkyBirds skyBirds;

    [Header("Settings & Pause")]
    [Tooltip("작업대 분류 패널 참조 (작업대 열림 시 배경 애니메이션 일시정지 연동)")]
    [SerializeField] private SaleSortingPanel sortingPanel;
    [Tooltip("작업대 분류 화면이 전면을 가릴 때 배경 애니메이션을 일시정지할지 여부")]
    [SerializeField] private bool pauseWhenCovered = true;

    private readonly Vector2[] chimneySmokeOrigins = new Vector2[2];
    private readonly Vector2[] watchGuardOrigins = new Vector2[2];
    private readonly float[] nextGuardShot = { 3f, 6f };
    private readonly float[] guardFlashUntil = new float[2];

    private float idleSeconds;
    private bool isInitialized;

    /// <summary>
    /// 현재 누적된 배경 연출 경과 시간을 반환합니다.
    /// </summary>
    public float IdleSeconds => this.idleSeconds;

    /// <summary>
    /// 원경 하늘의 절차적 새 무리 비행 컴포넌트를 반환합니다.
    /// </summary>
    public FrontSkyBirds SkyBirds => this.skyBirds;

    private void Awake()
    {
        this.Initialize();
    }

    private void OnEnable()
    {
        if (!this.isInitialized)
        {
            this.Initialize();
        }
        else
        {
            this.captureOrigins();
        }
    }

    private void Update()
    {
        // 작업대 화면이 열려 전면을 가리거나 게임이 일시정지된 경우 갱신 스킵
        if (this.pauseWhenCovered && this.sortingPanel != null && this.sortingPanel.IsSortingViewOpen)
        {
            return;
        }

        if (Time.timeScale <= 0f)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        this.idleSeconds += dt;

        if (this.skyBirds != null)
        {
            this.skyBirds.Tick(dt);
        }

        this.animateSmoke(this.idleSeconds);
        this.animateCrowd(this.idleSeconds);
        this.animateGuards(this.idleSeconds);
    }

    /// <summary>
    /// 하위 계층 참조를 자동으로 탐색하고 초기 기준 위치와 군중 행을 구성합니다.
    /// </summary>
    public void Initialize()
    {
        this.autoResolveReferences();
        this.captureOrigins();

        for (int i = 0; i < this.animatedCrowd.Length; i++)
        {
            if (this.animatedCrowd[i] != null)
            {
                this.animatedCrowd[i].Configure(i);
            }
        }

        for (int i = 0; i < this.watchMuzzleFlashes.Length; i++)
        {
            if (this.watchMuzzleFlashes[i] != null)
            {
                this.watchMuzzleFlashes[i].gameObject.SetActive(false);
            }
        }

        this.isInitialized = true;
    }

    /// <summary>
    /// 기준 해상도 기준 굴뚝 연기의 4프레임 교체 및 미세 부유 오프셋을 갱신합니다.
    /// </summary>
    /// <param name="time">누적 연출 경과 시간(초)</param>
    private void animateSmoke(float time)
    {
        if (this.chimneySmokeFrames == null || this.chimneySmokeFrames.Length == 0) return;

        for (int i = 0; i < this.chimneySmoke.Length; i++)
        {
            if (this.chimneySmoke[i] == null) continue;

            float smokeTime = time / (1.1f + i * 0.13f) + i * 1.7f;
            int frame = Mathf.FloorToInt(smokeTime) % this.chimneySmokeFrames.Length;
            this.chimneySmoke[i].sprite = this.chimneySmokeFrames[frame];
            this.chimneySmoke[i].rectTransform.anchoredPosition = this.chimneySmokeOrigins[i] +
                new Vector2(Mathf.Sin(time * 0.28f + i) * 2f, Mathf.Sin(time * 0.4f + i) * 1.5f);
        }
    }

    /// <summary>
    /// 군중 3행의 유기적인 대기 출렁임 메시 변위를 갱신합니다.
    /// </summary>
    /// <param name="time">누적 연출 경과 시간(초)</param>
    private void animateCrowd(float time)
    {
        for (int i = 0; i < this.animatedCrowd.Length; i++)
        {
            if (this.animatedCrowd[i] != null)
            {
                this.animatedCrowd[i].Animate(time);
            }
        }
    }

    /// <summary>
    /// 경비병의 선회, 발걸음 체중 이동, 외곽 조준 시 주기적 사격 및 총구 불꽃 점멸을 갱신합니다.
    /// </summary>
    /// <param name="time">누적 연출 경과 시간(초)</param>
    private void animateGuards(float time)
    {
        for (int i = 0; i < this.watchGuards.Length; i++)
        {
            if (this.watchGuards[i] == null) continue;

            float turnPhase = time * (0.12f + i * 0.012f) + i * 1.7f;
            float turn = Mathf.Sin(turnPhase);
            float facing = Mathf.Sign(turn) * Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Abs(turn)));
            float outward = i == 0 ? -1f : 1f;

            // 중앙 가판 쪽으로는 사격하지 않고, 총구가 외곽 바깥을 향했을 때만 발사
            if (time >= this.nextGuardShot[i] && facing * outward > 0.9f)
            {
                this.guardFlashUntil[i] = time + MuzzleFlashDurationSeconds;
                this.nextGuardShot[i] = time + GuardBaseShotCooldownSeconds + i * GuardShotIndexOffsetSeconds;
            }

            bool firing = time < this.guardFlashUntil[i];
            this.watchGuards[i].localScale = new Vector3(facing, 1f, 1f);

            // 선회 동작 중에만 낮은 진폭으로 체중을 옮기고 끝에 머물 때는 잦아듭니다
            float stepping = Mathf.Abs(Mathf.Cos(turnPhase));
            float stepBob = Mathf.Sin(time * 2.6f + i * 1.3f) * 0.65f * stepping;
            this.watchGuards[i].anchoredPosition = this.watchGuardOrigins[i] + new Vector2(turn * 2f - (firing ? outward : 0f), stepBob);

            if (this.watchMuzzleFlashes[i] != null)
            {
                this.watchMuzzleFlashes[i].anchoredPosition = this.watchGuards[i].anchoredPosition +
                    new Vector2(facing * (this.watchGuards[i].sizeDelta.x * 0.5f + 3f), -this.watchGuards[i].sizeDelta.y * 0.35f);
                this.watchMuzzleFlashes[i].localScale = new Vector3(outward, 1f, 1f);
                this.watchMuzzleFlashes[i].gameObject.SetActive(firing);
            }
        }
    }

    /// <summary>
    /// 연기 및 경비병의 초기 anchoredPosition을 캡처하여 보존합니다.
    /// </summary>
    private void captureOrigins()
    {
        for (int i = 0; i < this.chimneySmoke.Length; i++)
        {
            if (this.chimneySmoke[i] != null)
            {
                this.chimneySmokeOrigins[i] = this.chimneySmoke[i].rectTransform.anchoredPosition;
            }
        }

        for (int i = 0; i < this.watchGuards.Length; i++)
        {
            if (this.watchGuards[i] != null)
            {
                this.watchGuardOrigins[i] = this.watchGuards[i].anchoredPosition;
            }
        }
    }

    /// <summary>
    /// 필드가 비어 있을 때 계층 구조의 자식 오브젝트들을 이름으로 자동 탐색하여 연결합니다.
    /// </summary>
    private void autoResolveReferences()
    {
        Transform root = this.transform;

        if (this.sortingPanel == null)
        {
            this.sortingPanel = this.GetComponentInParent<SaleSortingPanel>();
            if (this.sortingPanel == null)
            {
                this.sortingPanel = FindFirstObjectByType<SaleSortingPanel>();
            }
        }

        string[] smokeNames = { "LeftChimneySmoke", "RightChimneySmoke" };
        for (int i = 0; i < smokeNames.Length; i++)
        {
            if (this.chimneySmoke[i] == null)
            {
                Transform child = root.Find(smokeNames[i]);
                if (child != null) this.chimneySmoke[i] = child.GetComponent<Image>();
            }
        }

        string[] guardNames = { "LeftWatchGuard", "RightWatchGuard" };
        for (int i = 0; i < guardNames.Length; i++)
        {
            if (this.watchGuards[i] == null)
            {
                Transform child = root.Find(guardNames[i]);
                if (child != null) this.watchGuards[i] = child as RectTransform;
            }
        }

        string[] flashNames = { "WatchMuzzleFlash0", "WatchMuzzleFlash1" };
        for (int i = 0; i < flashNames.Length; i++)
        {
            if (this.watchMuzzleFlashes[i] == null)
            {
                Transform child = root.Find(flashNames[i]);
                if (child != null) this.watchMuzzleFlashes[i] = child as RectTransform;
            }
        }

        string[] crowdNames = { "CrowdBack", "CrowdMiddle", "CrowdFront" };
        for (int i = 0; i < crowdNames.Length; i++)
        {
            if (this.animatedCrowd[i] == null)
            {
                Transform child = root.Find(crowdNames[i]);
                if (child != null) this.animatedCrowd[i] = child.GetComponent<FrontCrowdImage>();
            }
        }

        if (this.skyBirds == null)
        {
            Transform child = root.Find("SkyBirds");
            if (child != null) this.skyBirds = child.GetComponent<FrontSkyBirds>();
        }

#if UNITY_EDITOR
        if (this.chimneySmokeFrames == null || this.chimneySmokeFrames.Length != 4 || this.chimneySmokeFrames[0] == null)
        {
            var frames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                frames[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Textures/Environment/Dystopia/ChimneySmoke{i}.png");
                if (frames[i] == null)
                {
                    frames[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/DystopiaPrototype/Art/ChimneySmoke{i}.png");
                }
            }
            this.chimneySmokeFrames = frames;
        }
#endif
    }
}
