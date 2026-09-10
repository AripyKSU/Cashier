using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 손님이 카운터에 상자를 내려놓을 때 상자 하단 접점에서 10개의 픽셀 먼지 입자가
/// 양옆으로 포물선을 그리며 비산했다가 부드럽게 사라지는 착지 더스트 연출 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LandingDustEffect : MonoBehaviour
{
    private const int ParticleCount = 10;

    [Header("Dust Settings")]
    [Tooltip("착지 시 좌우로 흩뿌려지는 10개의 픽셀 먼지 이미지 배열 (미할당 시 런타임 자동 생성)")]
    [SerializeField] private Image[] dustImages = new Image[ParticleCount];

    [Tooltip("더스트 지속 시간 (초)")]
    [SerializeField, Range(0.1f, 1f)] private float durationSeconds = 0.40f;

    [Tooltip("먼지 입자의 기본 색상 (프로토타입 기준 흙먼지 톤)")]
    [SerializeField] private Color dustColor = new Color(0.48f, 0.43f, 0.39f, 0.95f);

    [Tooltip("먼지 발생 기준점의 추가 미세 조정 오프셋 (X, Y)")]
    [SerializeField] private Vector2 dustOffset = Vector2.zero;

    private Coroutine playRoutine;

    /// <summary>먼지 발생 기준점 오프셋을 가져오거나 설정합니다.</summary>
    public Vector2 DustOffset
    {
        get => this.dustOffset;
        set => this.dustOffset = value;
    }

    private void Awake()
    {
        this.ensureDustImages(this.transform);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        bool isLKeyPressed = false;
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.lKey.wasPressedThisFrame)
        {
            isLKeyPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.L))
        {
            isLKeyPressed = true;
        }
#endif

        if (isLKeyPressed)
        {
            this.TestPlay();
        }
    }

    /// <summary>인스펙터 컨텍스트 메뉴 및 L키로 더스트 효과를 즉시 테스트합니다.</summary>
    [ContextMenu("Test Play Dust")]
    public void TestPlay()
    {
        Transform container = this.transform.Find("FrontContainer");
        if (container != null && container is RectTransform rect)
        {
            this.Play(rect);
        }
        else
        {
            this.Play(new Vector2(640f, -578f) + this.dustOffset);
        }
    }

    /// <summary>상자 RectTransform의 중심 X 및 하단 카운터 접점 Y 좌표를 계산합니다.</summary>
    /// <param name="box">카운터에 착지한 상자의 RectTransform입니다.</param>
    /// <returns>카운터 매대 접점의 로컬 좌표입니다.</returns>
    public Vector2 CalculateContactPoint(RectTransform box)
    {
        if (box == null) return new Vector2(640f, -578f) + this.dustOffset;

        float boxLeftX = box.anchoredPosition.x - box.pivot.x * box.sizeDelta.x;
        float boxTopY = box.anchoredPosition.y + (1f - box.pivot.y) * box.sizeDelta.y;
        float centerX = boxLeftX + box.sizeDelta.x * 0.5f;

        // FrontContainer 스프라이트(1254x1254)는 preserveAspect 적용 시 240x240으로 표시되며,
        // 스프라이트 내 상자 밑면은 Y=1192(1192/1254 = 95.06%)에 위치합니다.
        // 따라서 상자 밑면의 실제 카운터 접점 Y는 상단에서 240 * 0.9506f 아래입니다.
        float renderedHeight = Mathf.Min(box.sizeDelta.x, box.sizeDelta.y);
        float visualBottomY = boxTopY - (renderedHeight * 0.9506f);

        return new Vector2(centerX, visualBottomY) + this.dustOffset;
    }

    /// <summary>지정된 상자 RectTransform의 하단 카운터 접점 위치에서 착지 더스트 효과를 재생합니다.</summary>
    /// <param name="box">카운터에 착지한 상자의 RectTransform입니다.</param>
    public void Play(RectTransform box)
    {
        if (box == null)
        {
            this.Play(new Vector2(640f, -578f) + this.dustOffset);
            return;
        }

        Transform parentTransform = box.parent != null ? box.parent : this.transform;
        this.ensureDustImages(parentTransform);

        Vector2 contactCenter = this.CalculateContactPoint(box);

        // 상자 및 카운터 바로 앞에 그려지도록 형제 인덱스 배치
        int siblingIndex = box.GetSiblingIndex() + 1;
        for (int i = 0; i < ParticleCount; i++)
        {
            if (this.dustImages[i] != null)
            {
                RectTransform rect = this.dustImages[i].rectTransform;
                rect.SetParent(parentTransform, false);
                rect.SetSiblingIndex(Mathf.Min(siblingIndex + i, parentTransform.childCount - 1));
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
            }
        }

        this.startAnimate(contactCenter);
    }

    /// <summary>지정된 기준 위치에서 착지 더스트 효과를 재생합니다.</summary>
    /// <param name="origin">더스트가 발생할 기준 로컬 좌표입니다.</param>
    public void Play(Vector2 origin)
    {
        this.ensureDustImages(this.transform);
        this.startAnimate(origin);
    }

    private void startAnimate(Vector2 origin)
    {
        if (this.playRoutine != null)
        {
            this.StopCoroutine(this.playRoutine);
        }

        this.playRoutine = this.StartCoroutine(this.animateDust(origin));
    }

    /// <summary>현재 재생 중인 더스트 효과를 중지하고 투명하게 숨깁니다.</summary>
    public void Stop()
    {
        if (this.playRoutine != null)
        {
            this.StopCoroutine(this.playRoutine);
            this.playRoutine = null;
        }

        this.clearDust();
    }

    private void OnDisable()
    {
        this.Stop();
    }

    /// <summary>10개의 픽셀 먼지 이미지를 검색하거나 생성합니다.</summary>
    private void ensureDustImages(Transform parentTransform)
    {
        if (this.dustImages == null || this.dustImages.Length != ParticleCount)
        {
            this.dustImages = new Image[ParticleCount];
        }

        for (int i = 0; i < ParticleCount; i++)
        {
            if (this.dustImages[i] != null) continue;

            Transform existing = parentTransform.Find($"LandingDust{i}");
            if (existing != null && existing.TryGetComponent<Image>(out var img))
            {
                this.dustImages[i] = img;
            }
            else
            {
                var dustGo = new GameObject($"LandingDust{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dustGo.transform.SetParent(parentTransform, false);
                var rect = dustGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(12f + (i % 3) * 3f, 6f + (i % 2) * 2f);

                var image = dustGo.GetComponent<Image>();
                image.color = this.dustColor;
                image.raycastTarget = false;
                image.canvasRenderer.SetColor(new Color(1f, 1f, 1f, 0f));
                this.dustImages[i] = image;
            }
        }
    }

    /// <summary>더스트 입자들을 좌우로 흩뿌리고 포물선 호와 알파 페이드아웃을 적용하는 코루틴입니다.</summary>
    private IEnumerator animateDust(Vector2 origin)
    {
        Vector2[] startPositions = new Vector2[ParticleCount];
        Vector2[] targetPositions = new Vector2[ParticleCount];
        float[] arcHeights = new float[ParticleCount];

        for (int i = 0; i < ParticleCount; i++)
        {
            if (this.dustImages[i] == null) continue;

            // 좌측 5개(-X), 우측 5개(+X)로 분산
            bool isLeft = i < ParticleCount / 2;
            int rank = isLeft ? i : (i - ParticleCount / 2);

            // 상자 하단 테두리(폭 ~228px, 반폭 ~114px) 아래에서 자연스럽게 흩어지도록 시작점 및 분산 폭 설정
            float startOffsetX = (isLeft ? -1f : 1f) * (30f + rank * 16f);
            float spreadX = (isLeft ? -1f : 1f) * (35f + rank * 16f + ((i * 5) % 11));
            arcHeights[i] = 8f + (rank % 3) * 5f;

            startPositions[i] = origin + new Vector2(startOffsetX, 0f);
            targetPositions[i] = origin + new Vector2(startOffsetX + spreadX, -2f);

            RectTransform rect = this.dustImages[i].rectTransform;
            rect.anchoredPosition = startPositions[i];
            this.setParticleAlpha(this.dustImages[i], this.dustColor.a);
            this.dustImages[i].gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < this.durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / this.durationSeconds);
            float easeOut = 1f - Mathf.Pow(1f - t, 2.5f);
            float arc = Mathf.Sin(t * Mathf.PI);
            float alpha = (1f - Mathf.Pow(t, 1.8f)) * this.dustColor.a;

            for (int i = 0; i < ParticleCount; i++)
            {
                if (this.dustImages[i] == null) continue;

                Vector2 pos = Vector2.Lerp(startPositions[i], targetPositions[i], easeOut);
                pos.y += arc * arcHeights[i];

                this.dustImages[i].rectTransform.anchoredPosition = pos;
                this.setParticleAlpha(this.dustImages[i], alpha);
            }

            yield return null;
        }

        this.clearDust();
        this.playRoutine = null;
    }

    private void setParticleAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = this.dustColor;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
        image.canvasRenderer.SetColor(new Color(1f, 1f, 1f, c.a));
    }

    private void clearDust()
    {
        if (this.dustImages == null) return;

        for (int i = 0; i < this.dustImages.Length; i++)
        {
            if (this.dustImages[i] != null)
            {
                this.setParticleAlpha(this.dustImages[i], 0f);
            }
        }
    }
}
