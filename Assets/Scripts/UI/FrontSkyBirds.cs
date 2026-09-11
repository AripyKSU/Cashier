using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 원경 하늘에 5마리의 작은 새 무리가 완만하게 날갯짓하며 횡단 비행하는 절차적 UI 그래픽 컴포넌트입니다.
/// 프로토타입 SkyBirds 셰이더의 비행 궤적 및 날갯짓 수학 공식을 2D 캔버스 버텍스 메쉬로 100% 재현합니다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FrontSkyBirds : MaskableGraphic
{
    [Header("Bird Flock Configuration")]
    [Tooltip("새 무리의 개체 수입니다. (기본 5마리)")]
    [Range(1, 10)]
    [SerializeField] private int birdCount = 5;

    [Tooltip("새 실루엣의 크기 배율입니다.")]
    [SerializeField] private float birdScale = 1.2f;

    [Tooltip("기본 비행 기준 고도(Y)입니다.")]
    [SerializeField] private float baseAltitude = 190f;

    [Tooltip("새들 간의 상하 고도 간격입니다.")]
    [SerializeField] private float altitudeSpread = 13f;

    [Tooltip("전체 횡단 비행 속도 배율입니다.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("날갯짓 속도 배율입니다.")]
    [SerializeField] private float flapSpeedMultiplier = 1f;

    [Header("Color & Silhouette")]
    [Tooltip("새 실루엣의 기본 색상입니다.")]
    [SerializeField] private Color birdColor = new Color(0.18f, 0.20f, 0.24f, 0.85f);

    [NonSerialized] private float accumulatedTime;
    [NonSerialized] private UIVertex[] quadBuffer = new UIVertex[4];

    /// <summary>
    /// 외부 애니메이션 컨트롤러에서 매 프레임 호출하여 시간을 누적하고 메쉬 버텍스를 갱신합니다.
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)입니다.</param>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        this.accumulatedTime += deltaTime;
        this.SetVerticesDirty();
    }

    /// <summary>
    /// 시간대별 환경광 틴트에 맞춰 새 실루엣 색상을 조정합니다.
    /// </summary>
    /// <param name="tint">적용할 틴트 색상입니다.</param>
    public void SetTintColor(Color tint)
    {
        Color adjusted = new Color(
            this.birdColor.r * tint.r,
            this.birdColor.g * tint.g,
            this.birdColor.b * tint.b,
            this.birdColor.a * tint.a);

        if (this.color != adjusted)
        {
            this.color = adjusted;
            this.SetVerticesDirty();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        this.color = this.birdColor;
        this.raycastTarget = false;
    }

#if UNITY_EDITOR
    private void Update()
    {
        // 에디터 씬 뷰 또는 단독 실행 환경에서 Controller가 없더라도 실시간 프리뷰를 지원합니다.
        if (!Application.isPlaying)
        {
            this.accumulatedTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
            this.SetVerticesDirty();
        }
    }
#endif

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float seconds = this.accumulatedTime;
        Color32 vertexColor = this.color;
        float scale = Mathf.Max(0.1f, this.birdScale);

        // 1280x720 가판 캔버스 영역 (화면 중앙 기준 X: -640~+640, Y: -360~+360)
        float leftBound = -640f - 100f;
        float totalWidth = 1480f;

        for (int bird = 0; bird < this.birdCount; bird++)
        {
            // 1. 프로토타입 SkyBirds의 수평 비행 궤적 (좌측에서 우측으로 순환 이동)
            float speedRate = (0.009f + bird * 0.0005f) * this.speedMultiplier;
            float progress = (seconds * speedRate + bird * 0.17f) % 1f;
            if (progress < 0f) progress += 1f;
            float bx = leftBound + progress * totalWidth;

            // 2. 상하 완만 사인파 부유 운동
            float by = this.baseAltitude - bird * this.altitudeSpread + Mathf.Sin(seconds * 0.7f + bird) * 5f;

            // 3. 날갯짓 (sin 주기)
            float flap = Mathf.Sin((seconds * (6f + bird * 0.3f) + bird * 2f) * this.flapSpeedMultiplier);

            // 4. 새 기하 메쉬 생성 (몸통 1개, 좌우 날개 2개)
            float wingSpan = 7f * scale;
            float wingTipY = wingSpan * (0.25f + 0.5f * flap);
            float wingThickness = 1.3f * scale;

            Vector2 center = new Vector2(bx, by);

            // (1) 몸통 (Body) - 소형 다이아몬드/직사각형
            float bodyHalfWidth = 1.5f * scale;
            float bodyHalfHeight = 1.8f * scale;
            this.addQuad(vh,
                new Vector2(bx - bodyHalfWidth, by - bodyHalfHeight - 0.5f * scale),
                new Vector2(bx - bodyHalfWidth, by + bodyHalfHeight - 0.5f * scale),
                new Vector2(bx + bodyHalfWidth, by + bodyHalfHeight - 0.5f * scale),
                new Vector2(bx + bodyHalfWidth, by - bodyHalfHeight - 0.5f * scale),
                vertexColor);

            // (2) 좌측 날개 (Left Wing) - 중심에서 좌측 상단/하단 날개 끝으로 연장
            Vector2 leftTip = new Vector2(bx - wingSpan, by + wingTipY);
            Vector2 leftDir = (leftTip - center).normalized;
            Vector2 leftNormal = new Vector2(-leftDir.y, leftDir.x) * wingThickness;

            this.addQuad(vh,
                center - leftNormal,
                center + leftNormal,
                leftTip + leftNormal * 0.5f,
                leftTip - leftNormal * 0.5f,
                vertexColor);

            // (3) 우측 날개 (Right Wing) - 중심에서 우측 상단/하단 날개 끝으로 연장
            Vector2 rightTip = new Vector2(bx + wingSpan, by + wingTipY);
            Vector2 rightDir = (rightTip - center).normalized;
            Vector2 rightNormal = new Vector2(-rightDir.y, rightDir.x) * wingThickness;

            this.addQuad(vh,
                center - rightNormal,
                center + rightNormal,
                rightTip + rightNormal * 0.5f,
                rightTip - rightNormal * 0.5f,
                vertexColor);
        }
    }

    private void addQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color32 col)
    {
        this.quadBuffer[0].position = p0;
        this.quadBuffer[0].color = col;
        this.quadBuffer[0].uv0 = Vector2.zero;

        this.quadBuffer[1].position = p1;
        this.quadBuffer[1].color = col;
        this.quadBuffer[1].uv0 = new Vector2(0f, 1f);

        this.quadBuffer[2].position = p2;
        this.quadBuffer[2].color = col;
        this.quadBuffer[2].uv0 = Vector2.one;

        this.quadBuffer[3].position = p3;
        this.quadBuffer[3].color = col;
        this.quadBuffer[3].uv0 = new Vector2(1f, 0f);

        vh.AddUIVertexQuad(this.quadBuffer);
    }
}
