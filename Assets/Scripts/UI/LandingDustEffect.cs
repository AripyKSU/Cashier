using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님이 카운터에 상자를 내려놓을 때 상자 하단 접점에서 10개의 픽셀 먼지 입자가
/// 양옆으로 픽셀 격자에 맞춰 비산했다가 부드럽게 사라지는 착지 더스트 연출 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LandingDustEffect : MonoBehaviour
{
    private const int ParticleCount = 10;

    [Header("Dust Settings")]
    [Tooltip("착지 시 좌우로 흩뿌려지는 10개의 픽셀 먼지 이미지 배열 (미할당 시 런타임 자동 생성)")]
    [SerializeField] private Image[] dustImages = new Image[ParticleCount];

    [Tooltip("더스트 지속 시간 (초)")]
    [SerializeField, Range(0.1f, 1f)] private float durationSeconds = 0.55f;

    [Tooltip("먼지 입자의 기본 색상 (프로토타입 기준 흙먼지 톤)")]
    [SerializeField] private Color dustColor = new Color(0.34f, 0.32f, 0.28f, 0.42f);

    [Tooltip("상자가 없는 수동 테스트에서만 사용하는 기준 좌표")]
    [SerializeField] private Vector2 baseContactPoint = new Vector2(640f, -578f);

    [Tooltip("먼지 발생 기준점의 추가 미세 조정 오프셋 (X, Y)")]
    [SerializeField] private Vector2 dustOffset = Vector2.zero;

    private Coroutine playRoutine;
    /// <summary>오류나 비활성화로 표현 진행이 막혔는지 조회합니다.</summary>
    private System.Func<bool> isPresentationBlocked;

    /// <summary>더스트 연출이 현재 진행 중인지 나타냅니다.</summary>
    public bool IsPlaying => this.playRoutine != null;

    /// <summary>오류나 비활성화로 표현 진행이 막힌 동안 시간을 멈출 조회자를 연결합니다.</summary>
    /// <param name="isBlocked">표현 진행 차단 여부를 반환하는 조회자입니다.</param>
    public void SetPresentationBlockQuery(System.Func<bool> isBlocked) => this.isPresentationBlocked = isBlocked;

    /// <summary>수동 테스트 또는 상자가 없을 때만 사용하는 접점 좌표입니다.</summary>
    public Vector2 BaseContactPoint
    {
        get => this.baseContactPoint;
        set => this.baseContactPoint = value;
    }

    /// <summary>먼지 발생 기준점 오프셋을 가져오거나 설정합니다.</summary>
    public Vector2 DustOffset
    {
        get => this.dustOffset;
        set => this.dustOffset = value;
    }

    private void Awake()
    {
        this.ensureDustImages(this.transform);
        this.clearDust();
    }

    /// <summary>인스펙터 컨텍스트 메뉴에서 더스트 효과를 즉시 테스트합니다.</summary>
    [ContextMenu("Test Play Dust")]
    public void TestPlay()
    {
        this.Play(this.baseContactPoint + this.dustOffset);
    }

    /// <summary>
    /// 상자의 실제 배율·피벗·회전을 반영한 하단 중앙을 부모 좌상단 기준 좌표로 반환합니다.
    /// 상자가 없을 때만 수동 테스트용 BaseContactPoint를 사용합니다.
    /// </summary>
    /// <param name="box">카운터에 착지한 상자의 RectTransform입니다.</param>
    /// <returns>카운터 매대 접점의 로컬 좌표입니다.</returns>
    public Vector2 CalculateContactPoint(RectTransform box)
    {
        if (box == null) return this.baseContactPoint + this.dustOffset;
        Transform parentTransform = box.parent;
        Vector3 world = box.TransformPoint(new Vector3(box.rect.center.x, box.rect.yMin));
        if (parentTransform == null) return (Vector2)world + this.dustOffset;
        Vector2 parentLocal = parentTransform.InverseTransformPoint(world);
        if (parentTransform is RectTransform parent)
            parentLocal -= new Vector2(parent.rect.xMin, parent.rect.yMax);
        return parentLocal + this.dustOffset;
    }

    /// <summary>지정된 상자 RectTransform의 하단 카운터 접점 위치에서 착지 더스트 효과를 재생합니다.</summary>
    /// <param name="box">카운터에 착지한 상자의 RectTransform입니다.</param>
    public void Play(RectTransform box)
    {
        if (box == null)
        {
            this.Play(this.baseContactPoint + this.dustOffset);
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
                rect.sizeDelta = new Vector2(12f, 6f);
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

    /// <summary>기존 먼지 재생을 중단하고 지정 접점에서 다시 시작합니다.</summary>
    /// <param name="origin">먼지 부모의 좌상단 기준 접점입니다.</param>
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
                rect.sizeDelta = new Vector2(12f, 6f);

                var image = dustGo.GetComponent<Image>();
                image.color = new Color(this.dustColor.r, this.dustColor.g, this.dustColor.b, 0f);
                image.raycastTarget = false;
                image.canvasRenderer.SetColor(Color.white);
                this.dustImages[i] = image;
            }
        }
    }

    /// <summary>더스트 입자를 좌우로 번갈아 확산하고 알파 페이드아웃을 적용하는 코루틴입니다.</summary>
    /// <param name="origin">먼지 부모의 좌상단 기준 접점입니다.</param>
    /// <returns>표시 차단을 따르는 프레임 대기 열거자입니다.</returns>
    private IEnumerator animateDust(Vector2 origin)
    {
        for (int i = 0; i < ParticleCount; i++)
        {
            if (this.dustImages[i] == null) continue;
            this.dustImages[i].rectTransform.anchoredPosition = origin;
            this.setParticleAlpha(this.dustImages[i], this.dustColor.a);
            this.dustImages[i].gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < this.durationSeconds)
        {
            if (this.isPresentationBlocked?.Invoke() != true) elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / this.durationSeconds);
            float alpha = (1f - t) * this.dustColor.a;

            for (int i = 0; i < ParticleCount; i++)
            {
                if (this.dustImages[i] == null) continue;

                float side = i % 2 == 0 ? -1f : 1f;
                float spread = 80f + i / 2 * 13f + t * (35f + i * 3f);
                Vector2 position = origin + new Vector2(side * spread,
                    12f + Mathf.Sin(t * Mathf.PI * 0.5f) * (12f + i % 3 * 5f));
                this.dustImages[i].rectTransform.anchoredPosition = new Vector2(
                    Mathf.Round(position.x / 2f) * 2f,
                    Mathf.Round(position.y / 2f) * 2f);
                this.setParticleAlpha(this.dustImages[i], alpha);
            }

            yield return null;
        }

        this.clearDust();
        this.playRoutine = null;
    }

    /// <summary>이미지 색이 알파를 단독 소유하도록 CanvasRenderer의 중복 틴트를 제거합니다.</summary>
    /// <param name="image">먼지 이미지입니다.</param>
    /// <param name="alpha">적용할 알파입니다.</param>
    private void setParticleAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = this.dustColor;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
        image.canvasRenderer.SetColor(Color.white);
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
