using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업대 분류 결과를 구역별로 다르게 강조하는 표현 전용 컴포넌트입니다.
/// 판매(오른쪽)는 팝·반짝임·초록 플래시, 폐기(왼쪽)는 빨려 들어가는 잔상·먼지·흔들림을 재생합니다.
/// 분류 판정과 상품 상태는 SaleSortingPanel이 소유하며 이 컴포넌트는 Transform·색 표현만 변경합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaleSortingFeedback : MonoBehaviour
{
    private const int ParticlePoolSize = 32;
    private const int GhostPoolSize = 4;
    private const float ParticleGravity = -520f;

    [Header("Sale (Right)")]
    [Tooltip("판매 구역 플래시와 반짝임 보조 색입니다.")]
    [SerializeField] private Color saleColor = new Color(0.55f, 1f, 0.45f, 1f);
    [Tooltip("판매 확정 시 튀는 반짝임 입자 색입니다.")]
    [SerializeField] private Color saleSparkColor = new Color(1f, 0.88f, 0.3f, 1f);
    [SerializeField, Range(1f, 1.6f)] private float salePopScale = 1.3f;
    [SerializeField, Range(0.05f, 1f)] private float salePopSeconds = 0.34f;
    [SerializeField, Range(0, 12)] private int saleSparkCount = 8;

    [Header("Discard (Left)")]
    [Tooltip("폐기 구역 플래시 색입니다.")]
    [SerializeField] private Color discardColor = new Color(1f, 0.32f, 0.28f, 1f);
    [Tooltip("폐기 시 구역 중앙에서 피어오르는 먼지 색입니다.")]
    [SerializeField] private Color discardDustColor = new Color(0.42f, 0.4f, 0.36f, 0.85f);
    [SerializeField, Range(0.05f, 1f)] private float discardSuckSeconds = 0.3f;
    [SerializeField, Range(0f, 20f)] private float discardShakePixels = 8f;
    [SerializeField, Range(0.05f, 0.6f)] private float discardShakeSeconds = 0.22f;
    [SerializeField, Range(0, 12)] private int discardDustCount = 7;

    [Header("Zone Glow")]
    [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.4f;
    [SerializeField, Range(0.05f, 1f)] private float flashSeconds = 0.32f;
    [Tooltip("물건을 들고 구역 위에 있을 때 미리 보여주는 밝기입니다.")]
    [SerializeField, Range(0f, 0.6f)] private float hoverAlpha = 0.18f;

    [Header("Drag")]
    [SerializeField, Range(1f, 1.3f)] private float dragScale = 1.12f;
    [SerializeField, Range(0f, 30f)] private float maxDragTiltDegrees = 14f;
    [Tooltip("가로 이동 속도(px/s)당 기울기 각도입니다.")]
    [SerializeField, Range(0f, 0.1f)] private float dragTiltPerPixelPerSecond = 0.012f;
    [SerializeField, Range(0.05f, 0.6f)] private float landSeconds = 0.2f;

    [Header("Status Text")]
    [SerializeField, Range(1f, 1.5f)] private float statusPunchScale = 1.15f;
    [SerializeField, Range(0.05f, 0.6f)] private float statusPunchSeconds = 0.2f;

    private enum ItemEffectKind
    {
        SalePop,
        Land
    }

    private sealed class ZoneGlow
    {
        public RectTransform Zone;
        public Image Overlay;
        public Color Color;
        public float Flash;
        public bool Hovered;
        public float ShakeElapsed = -1f;
        public Vector2 ShakeBase;
    }

    private struct ItemEffect
    {
        public SaleSortingItemView Item;
        public ItemEffectKind Kind;
        public float Elapsed;
    }

    private sealed class Particle
    {
        public Image Image;
        public Vector2 Start;
        public Vector2 Velocity;
        public Color Color;
        public float Elapsed;
        public float Life;
        public bool Active;
    }

    private sealed class Ghost
    {
        public Image Image;
        public Vector2 Start;
        public Vector2 Target;
        public float Elapsed;
        public bool Active;
    }

    private readonly List<ItemEffect> itemEffects = new List<ItemEffect>();
    private readonly List<Particle> particles = new List<Particle>();
    private readonly List<Ghost> ghosts = new List<Ghost>();
    private RectTransform fxRoot;
    private ZoneGlow saleGlow;
    private ZoneGlow discardGlow;
    private RectTransform statusText;
    private float statusElapsed = -1f;
    private SaleSortingItemView draggedItem;
    private Vector2 lastDragPosition;
    private float dragTilt;
    private Func<bool> isPresentationBlocked;

    /// <summary>표현 진행이 막힌 동안 연출 시간을 멈출 조회자를 연결합니다. null은 항상 진행입니다.</summary>
    /// <param name="isBlocked">표현 진행 차단 여부를 반환하는 조회자입니다.</param>
    public void SetPresentationBlockQuery(Func<bool> isBlocked) => this.isPresentationBlocked = isBlocked;

    /// <summary>연출이 그려질 루트와 구역을 연결합니다. 여러 번 호출해도 오버레이는 하나만 만듭니다.</summary>
    /// <param name="itemRoot">상품이 배치되는 RectTransform입니다. 입자·잔상 루트를 이 아래에 둡니다.</param>
    /// <param name="saleZone">오른쪽 판매 구역입니다.</param>
    /// <param name="excludedZone">왼쪽 폐기 구역입니다.</param>
    /// <param name="statusLabel">분류 개수 안내 텍스트입니다. null이면 펀치를 생략합니다.</param>
    public void Initialize(RectTransform itemRoot, RectTransform saleZone, RectTransform excludedZone, RectTransform statusLabel)
    {
        if (itemRoot == null) throw new ArgumentNullException(nameof(itemRoot));
        this.ClearAll();
        this.fxRoot = this.ensureFxRoot(itemRoot);
        this.saleGlow = this.createGlow(saleZone, this.saleColor);
        this.discardGlow = this.createGlow(excludedZone, this.discardColor);
        this.statusText = statusLabel;
    }

    /// <summary>플레이어가 상품을 집었을 때 들어 올림·기울기 표현을 시작합니다.</summary>
    /// <param name="item">집은 상품입니다.</param>
    public void BeginDrag(SaleSortingItemView item)
    {
        if (item == null) return;
        this.removeItemEffect(item);
        this.draggedItem = item;
        this.lastDragPosition = item.Position;
        this.dragTilt = 0f;
    }

    /// <summary>상품을 놓았을 때 들기 표현을 끝내고 분류 결과에 맞는 연출을 재생합니다.</summary>
    /// <param name="item">놓은 상품입니다.</param>
    /// <param name="previousState">놓기 전 분류 상태입니다.</param>
    public void EndDrag(SaleSortingItemView item, SaleSortingItemView.SortingState previousState)
    {
        if (this.draggedItem == item) this.draggedItem = null;
        this.setHover(null);
        this.PlayClassified(item, previousState);
    }

    /// <summary>분류 결과가 정해진 상품에 구역별 연출을 재생합니다.</summary>
    /// <param name="item">판정이 끝난 상품입니다.</param>
    /// <param name="previousState">판정 전 분류 상태입니다.</param>
    public void PlayClassified(SaleSortingItemView item, SaleSortingItemView.SortingState previousState)
    {
        if (item == null || this.fxRoot == null) return;
        this.removeItemEffect(item);

        if (item.State == SaleSortingItemView.SortingState.Excluded)
        {
            item.transform.localScale = Vector3.one;
            item.transform.localRotation = Quaternion.identity;
            if (previousState != SaleSortingItemView.SortingState.Excluded) this.playDiscard(item);
            return;
        }

        if (item.State == SaleSortingItemView.SortingState.ForSale &&
            previousState != SaleSortingItemView.SortingState.ForSale)
        {
            this.playSale(item);
            return;
        }

        this.itemEffects.Add(new ItemEffect { Item = item, Kind = ItemEffectKind.Land });
    }

    /// <summary>분류 개수 문구가 바뀌었을 때 텍스트를 짧게 튕깁니다.</summary>
    public void PulseStatus()
    {
        if (this.statusText != null) this.statusElapsed = 0f;
    }

    /// <summary>진행 중 연출을 모두 끝내고 상품·구역·텍스트의 원래 Transform을 복원합니다.</summary>
    public void ClearAll()
    {
        for (int index = 0; index < this.itemEffects.Count; index++)
            this.resetItemTransform(this.itemEffects[index].Item);
        this.itemEffects.Clear();
        this.resetItemTransform(this.draggedItem);
        this.draggedItem = null;

        foreach (Particle particle in this.particles)
        {
            particle.Active = false;
            if (particle.Image != null) particle.Image.gameObject.SetActive(false);
        }
        foreach (Ghost ghost in this.ghosts)
        {
            ghost.Active = false;
            if (ghost.Image != null) ghost.Image.gameObject.SetActive(false);
        }

        this.resetGlow(this.saleGlow);
        this.resetGlow(this.discardGlow);
        if (this.statusText != null) this.statusText.localScale = Vector3.one;
        this.statusElapsed = -1f;
    }

    private void OnEnable() => this.ClearAll();

    private void OnDisable() => this.ClearAll();

    /// <summary>패널이 이번 프레임 위치를 쓴 뒤 표현만 덧입힙니다.</summary>
    private void LateUpdate()
    {
        float deltaSeconds = this.isPresentationBlocked?.Invoke() == true ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (deltaSeconds <= 0f) return;

        this.updateDrag(deltaSeconds);
        this.updateItemEffects(deltaSeconds);
        this.updateGlow(this.saleGlow, deltaSeconds);
        this.updateGlow(this.discardGlow, deltaSeconds);
        this.updateParticles(deltaSeconds);
        this.updateGhosts(deltaSeconds);
        this.updateStatus(deltaSeconds);
    }

    private void playSale(SaleSortingItemView item)
    {
        this.itemEffects.Add(new ItemEffect { Item = item, Kind = ItemEffectKind.SalePop });
        Vector2 origin = this.toFx(item.transform.position);
        for (int index = 0; index < this.saleSparkCount; index++)
        {
            float angle = (Mathf.PI * 2f * index / Mathf.Max(1, this.saleSparkCount)) + UnityEngine.Random.Range(-0.25f, 0.25f);
            float speed = UnityEngine.Random.Range(170f, 260f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + Vector2.up * 120f;
            Color color = index % 2 == 0 ? this.saleSparkColor : this.saleColor;
            this.spawnParticle(origin, velocity, color, new Vector2(8f, 8f), 0.45f);
        }
        if (this.saleGlow != null) this.saleGlow.Flash = 1f;
    }

    private void playDiscard(SaleSortingItemView item)
    {
        Vector2 start = this.toFx(item.transform.position);
        Vector2 target = this.discardGlow != null
            ? this.toFx(this.discardGlow.Zone.TransformPoint(this.discardGlow.Zone.rect.center))
            : start;
        Image source = item.GetComponent<Image>();
        Ghost ghost = this.rentGhost();
        if (ghost != null && source != null)
        {
            ghost.Image.sprite = source.sprite;
            ghost.Image.preserveAspect = true;
            ghost.Image.rectTransform.sizeDelta = ((RectTransform)item.transform).rect.size;
            ghost.Start = start;
            ghost.Target = target;
            ghost.Elapsed = 0f;
            ghost.Active = true;
            ghost.Image.gameObject.SetActive(true);
            ghost.Image.rectTransform.SetAsLastSibling();
            this.applyGhost(ghost, 0f);
        }

        for (int index = 0; index < this.discardDustCount; index++)
        {
            Vector2 velocity = new Vector2(UnityEngine.Random.Range(-140f, 140f), UnityEngine.Random.Range(120f, 220f));
            this.spawnParticle(target, velocity, this.discardDustColor, new Vector2(10f, 6f), 0.42f);
        }

        if (this.discardGlow != null)
        {
            this.discardGlow.Flash = 1f;
            if (this.discardGlow.ShakeElapsed < 0f) this.discardGlow.ShakeBase = this.discardGlow.Zone.anchoredPosition;
            this.discardGlow.ShakeElapsed = 0f;
        }
    }

    private void updateDrag(float deltaSeconds)
    {
        SaleSortingItemView item = this.draggedItem;
        if (item == null) return;
        if (!item.gameObject.activeInHierarchy ||
            item.Manipulation != SaleSortingItemView.ManipulationState.PlayerDragging)
        {
            this.resetItemTransform(item);
            this.draggedItem = null;
            this.setHover(null);
            return;
        }

        Vector2 velocity = (item.Position - this.lastDragPosition) / deltaSeconds;
        this.lastDragPosition = item.Position;
        float targetTilt = Mathf.Clamp(-velocity.x * this.dragTiltPerPixelPerSecond,
            -this.maxDragTiltDegrees, this.maxDragTiltDegrees);
        this.dragTilt = Mathf.Lerp(this.dragTilt, targetTilt, 1f - Mathf.Exp(-14f * deltaSeconds));
        item.transform.localScale = Vector3.one * this.dragScale;
        item.transform.localRotation = Quaternion.Euler(0f, 0f, this.dragTilt);

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, item.transform.position);
        if (this.discardGlow != null && RectTransformUtility.RectangleContainsScreenPoint(this.discardGlow.Zone, screen))
            this.setHover(this.discardGlow);
        else if (this.saleGlow != null && RectTransformUtility.RectangleContainsScreenPoint(this.saleGlow.Zone, screen))
            this.setHover(this.saleGlow);
        else
            this.setHover(null);
    }

    private void updateItemEffects(float deltaSeconds)
    {
        for (int index = this.itemEffects.Count - 1; index >= 0; index--)
        {
            ItemEffect effect = this.itemEffects[index];
            SaleSortingItemView item = effect.Item;
            // 청소기 등 다른 소유자가 Transform을 잡으면 그 값을 덮어쓰지 않고 연출만 버린다.
            if (item == null || !item.gameObject.activeInHierarchy ||
                (item.Manipulation != SaleSortingItemView.ManipulationState.Idle &&
                 item.Manipulation != SaleSortingItemView.ManipulationState.AutoSorting))
            {
                this.itemEffects.RemoveAt(index);
                continue;
            }

            effect.Elapsed += deltaSeconds;
            float duration = effect.Kind == ItemEffectKind.SalePop ? this.salePopSeconds : this.landSeconds;
            float t = Mathf.Clamp01(effect.Elapsed / duration);
            float fade = (1f - t) * (1f - t);
            if (effect.Kind == ItemEffectKind.SalePop)
            {
                float scale = 1f + (this.salePopScale - 1f) * Mathf.Cos(t * Mathf.PI * 3f) * fade;
                item.transform.localScale = new Vector3(scale, scale, 1f);
                item.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * 7f * fade);
            }
            else
            {
                float squash = 0.12f * Mathf.Cos(t * Mathf.PI * 2f) * fade;
                item.transform.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                item.transform.localRotation = Quaternion.identity;
            }

            if (t >= 1f)
            {
                this.resetItemTransform(item);
                this.itemEffects.RemoveAt(index);
            }
            else
            {
                this.itemEffects[index] = effect;
            }
        }
    }

    private void updateGlow(ZoneGlow glow, float deltaSeconds)
    {
        if (glow == null) return;
        glow.Flash = Mathf.Max(0f, glow.Flash - deltaSeconds / this.flashSeconds);
        float hover = glow.Hovered ? this.hoverAlpha * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f)) : 0f;
        float alpha = Mathf.Max(glow.Flash * glow.Flash * this.flashAlpha, hover);
        if (glow.Overlay != null)
        {
            Color color = glow.Color;
            color.a = alpha;
            glow.Overlay.color = color;
        }

        if (glow.ShakeElapsed < 0f) return;
        glow.ShakeElapsed += deltaSeconds;
        float t = glow.ShakeElapsed / this.discardShakeSeconds;
        if (t >= 1f)
        {
            glow.Zone.anchoredPosition = glow.ShakeBase;
            glow.ShakeElapsed = -1f;
            return;
        }

        float strength = this.discardShakePixels * (1f - t);
        glow.Zone.anchoredPosition = glow.ShakeBase + new Vector2(
            Mathf.Round(Mathf.Sin(glow.ShakeElapsed * 90f) * strength),
            Mathf.Round(Mathf.Cos(glow.ShakeElapsed * 70f) * strength * 0.4f));
    }

    private void updateParticles(float deltaSeconds)
    {
        foreach (Particle particle in this.particles)
        {
            if (!particle.Active) continue;
            particle.Elapsed += deltaSeconds;
            float t = particle.Elapsed / particle.Life;
            if (t >= 1f)
            {
                particle.Active = false;
                particle.Image.gameObject.SetActive(false);
                continue;
            }

            float time = particle.Elapsed;
            Vector2 position = particle.Start + particle.Velocity * time + 0.5f * ParticleGravity * time * time * Vector2.up;
            // 픽셀 아트 격자에 맞춰 2px 단위로 스냅한다.
            particle.Image.rectTransform.localPosition = new Vector3(
                Mathf.Round(position.x / 2f) * 2f, Mathf.Round(position.y / 2f) * 2f, 0f);
            Color color = particle.Color;
            color.a *= 1f - t;
            particle.Image.color = color;
        }
    }

    private void updateGhosts(float deltaSeconds)
    {
        foreach (Ghost ghost in this.ghosts)
        {
            if (!ghost.Active) continue;
            ghost.Elapsed += deltaSeconds;
            float t = ghost.Elapsed / this.discardSuckSeconds;
            if (t >= 1f)
            {
                ghost.Active = false;
                ghost.Image.gameObject.SetActive(false);
                continue;
            }

            this.applyGhost(ghost, t);
        }
    }

    /// <summary>잔상이 가속하며 회전·축소되어 폐기 구역 중앙으로 빨려 들어가는 자세를 적용합니다.</summary>
    private void applyGhost(Ghost ghost, float t)
    {
        float eased = t * t;
        RectTransform rect = ghost.Image.rectTransform;
        rect.localPosition = Vector2.LerpUnclamped(ghost.Start, ghost.Target, eased);
        float scale = Mathf.Lerp(1f, 0.15f, eased);
        rect.localScale = new Vector3(scale, scale, 1f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -260f * eased);
        float shade = Mathf.Lerp(1f, 0.45f, t);
        ghost.Image.color = new Color(shade, shade, shade, t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
    }

    private void updateStatus(float deltaSeconds)
    {
        if (this.statusText == null || this.statusElapsed < 0f) return;
        this.statusElapsed += deltaSeconds;
        float t = this.statusElapsed / this.statusPunchSeconds;
        if (t >= 1f)
        {
            this.statusText.localScale = Vector3.one;
            this.statusElapsed = -1f;
            return;
        }

        float scale = 1f + (this.statusPunchScale - 1f) * Mathf.Sin(t * Mathf.PI);
        this.statusText.localScale = new Vector3(scale, scale, 1f);
    }

    private void setHover(ZoneGlow hovered)
    {
        if (this.saleGlow != null) this.saleGlow.Hovered = hovered == this.saleGlow;
        if (this.discardGlow != null) this.discardGlow.Hovered = hovered == this.discardGlow;
    }

    private void removeItemEffect(SaleSortingItemView item)
    {
        for (int index = this.itemEffects.Count - 1; index >= 0; index--)
        {
            if (this.itemEffects[index].Item == item) this.itemEffects.RemoveAt(index);
        }
    }

    private void resetItemTransform(SaleSortingItemView item)
    {
        if (item == null || item.Manipulation == SaleSortingItemView.ManipulationState.VacuumAttached) return;
        item.transform.localScale = Vector3.one;
        item.transform.localRotation = Quaternion.identity;
    }

    private void resetGlow(ZoneGlow glow)
    {
        if (glow == null) return;
        glow.Flash = 0f;
        glow.Hovered = false;
        if (glow.Overlay != null) glow.Overlay.color = new Color(glow.Color.r, glow.Color.g, glow.Color.b, 0f);
        if (glow.ShakeElapsed >= 0f && glow.Zone != null) glow.Zone.anchoredPosition = glow.ShakeBase;
        glow.ShakeElapsed = -1f;
    }

    private Vector2 toFx(Vector3 worldPosition) => this.fxRoot.InverseTransformPoint(worldPosition);

    private void spawnParticle(Vector2 origin, Vector2 velocity, Color color, Vector2 size, float life)
    {
        Particle particle = null;
        foreach (Particle candidate in this.particles)
        {
            if (!candidate.Active)
            {
                particle = candidate;
                break;
            }
        }
        if (particle == null)
        {
            if (this.particles.Count >= ParticlePoolSize) return;
            particle = new Particle { Image = this.createFxImage("SortingFxParticle") };
            this.particles.Add(particle);
        }

        particle.Start = origin;
        particle.Velocity = velocity;
        particle.Color = color;
        particle.Elapsed = 0f;
        particle.Life = life;
        particle.Active = true;
        particle.Image.rectTransform.sizeDelta = size;
        particle.Image.rectTransform.localPosition = origin;
        particle.Image.color = color;
        particle.Image.gameObject.SetActive(true);
        particle.Image.rectTransform.SetAsLastSibling();
    }

    private Ghost rentGhost()
    {
        foreach (Ghost candidate in this.ghosts)
        {
            if (!candidate.Active) return candidate;
        }
        if (this.ghosts.Count >= GhostPoolSize) return null;
        var ghost = new Ghost { Image = this.createFxImage("SortingFxGhost") };
        this.ghosts.Add(ghost);
        return ghost;
    }

    private Image createFxImage(string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(this.fxRoot, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        go.SetActive(false);
        return image;
    }

    private RectTransform ensureFxRoot(RectTransform itemRoot)
    {
        const string RootName = "SortingFeedbackFx";
        Transform existing = itemRoot.Find(RootName);
        RectTransform root = existing as RectTransform;
        if (root == null)
        {
            var go = new GameObject(RootName, typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent(itemRoot, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
        return root;
    }

    private ZoneGlow createGlow(RectTransform zone, Color color)
    {
        if (zone == null) return null;
        const string OverlayName = "SortingZoneGlow";
        Transform existing = zone.Find(OverlayName);
        Image overlay = existing != null ? existing.GetComponent<Image>() : null;
        if (overlay == null)
        {
            var go = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(zone, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            overlay = go.GetComponent<Image>();
        }

        RectTransform rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();
        overlay.raycastTarget = false;
        overlay.color = new Color(color.r, color.g, color.b, 0f);
        return new ZoneGlow { Zone = zone, Overlay = overlay, Color = color };
    }
}
