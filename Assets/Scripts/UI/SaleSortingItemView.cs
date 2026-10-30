using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>작업대 위에서 독립적으로 움직이고 분류되는 상품 한 개를 표시합니다.</summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class SaleSortingItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float CollisionRadiusScale = 0.18f;

    /// <summary>상품의 현재 분류 상태입니다.</summary>
    public enum SortingState
    {
        /// <summary>작업대에서 아직 분류되지 않은 상태입니다.</summary>
        Working,

        /// <summary>판매 대상으로 확정된 상태입니다.</summary>
        ForSale,

        /// <summary>판매하지 않기로 확정된 상태입니다.</summary>
        Excluded
    }

    /// <summary>상품 위치를 현재 변경하고 있는 주체입니다. 분류 상태와 별개로 관리합니다.</summary>
    public enum ManipulationState
    {
        /// <summary>현재 조작되지 않는 상태입니다.</summary>
        Idle,
        /// <summary>플레이어가 직접 드래그하는 상태입니다.</summary>
        PlayerDragging,
        /// <summary>막대가 이동시키는 상태입니다.</summary>
        DividerMoving,
        /// <summary>청소기 흡입 연출 또는 내부 보관이 위치·표시를 소유하는 상태입니다.</summary>
        VacuumAttached,
        /// <summary>자동 정렬이 이동시키는 상태입니다.</summary>
        AutoSorting,
        ManipulationState_End
    }

    private RectTransform rectTransform;
    private Image itemImage;
    private RectTransform workArea;
    private bool isDragging;
    // 손맛 연출이 켜졌을 때만 런타임에 붙이는 그림자입니다. 꺼진 상태에서는 만들지 않습니다.
    private Shadow liftShadow;
    private Tween feedbackTween;
    private float dragTiltDegrees;

    /// <summary>
    /// 판매 구역 상품을 연두색 대신 바랜 황동색으로 표시할지 여부입니다.
    /// 손맛 연출 스위치가 SaleSortingPanel을 통해 설정하며, 모든 작업대 상품에 공통으로 적용됩니다.
    /// </summary>
    public static bool UseMutedSaleTint { get; set; }

    /// <summary>상품 데이터 식별자입니다.</summary>
    public uint ProductId { get; private set; }

    /// <summary>같은 상품 수량 안에서의 개별 순번입니다.</summary>
    public int UnitIndex { get; private set; }

    /// <summary>현재 분류 상태입니다.</summary>
    public SortingState State { get; set; }

    /// <summary>상품 위치를 변경하는 주체입니다. 분류 상태와 독립적입니다.</summary>
    public ManipulationState Manipulation { get; internal set; }

    /// <summary>플레이어가 현재 이 아이템을 마우스로 드래그하고 있는지 여부입니다.</summary>
    public bool IsDragging => this.isDragging;

    /// <summary>상품 RectTransform의 절반 크기입니다.</summary>
    public Vector2 HalfSize => this.rectTransform == null
        ? Vector2.zero
        : this.rectTransform.rect.size * 0.5f;

    /// <summary>작업대 로컬 좌표에서의 현재 속도입니다.</summary>
    public Vector2 Velocity { get; internal set; }

    /// <summary>상품 충돌에 사용하는 반지름입니다.</summary>
    public float Radius => Mathf.Min(this.rectTransform.rect.width, this.rectTransform.rect.height) * CollisionRadiusScale;

    /// <summary>상품의 작업대 로컬 위치입니다.</summary>
    public Vector2 Position
    {
        get => this.rectTransform.anchoredPosition;
        internal set => this.rectTransform.anchoredPosition = value;
    }

    /// <summary>드래그 시작 시 발생합니다.</summary>
    public event Action<SaleSortingItemView> DragStarted;

    /// <summary>어느 물건이든 집어 올리기 시작했을 때 발생합니다. 지침서 창이 스스로 닫히는 데 씁니다.</summary>
    public static event Action AnyDragStarted;

    /// <summary>드래그 이동 시 발생합니다.</summary>
    public event Action<SaleSortingItemView> Dragged;

    /// <summary>드래그 종료 시 발생합니다.</summary>
    public event Action<SaleSortingItemView> DragEnded;

    /// <summary>상품 UI 참조를 준비합니다.</summary>
    private void Awake()
    {
        this.rectTransform = (RectTransform)this.transform;
        this.itemImage = this.GetComponent<Image>();
    }

    /// <summary>한 개별 상품 오브젝트를 표시 데이터와 연결합니다.</summary>
    /// <param name="productId">상품 데이터 식별자입니다.</param>
    /// <param name="unitIndex">같은 상품 내 개별 순번입니다.</param>
    /// <param name="sprite">표시할 상품 이미지입니다.</param>
    /// <param name="sizePixels">작업대에 표시할 정사각형 크기입니다.</param>
    /// <param name="displayName">기존 호출 호환용 표시 이름이며 탑뷰에는 출력하지 않습니다.</param>
    /// <param name="workAreaRect">드래그 좌표 변환에 사용할 작업대 RectTransform입니다.</param>
    public void Initialize(uint productId, int unitIndex, Sprite sprite, float sizePixels, string displayName = null, RectTransform workAreaRect = null)
    {
        if (this.rectTransform == null)
        {
            this.rectTransform = (RectTransform)this.transform;
            this.itemImage = this.GetComponent<Image>();
        }

        this.workArea = workAreaRect;
        this.ProductId = productId;
        this.UnitIndex = unitIndex;
        this.State = SortingState.Working;
        this.Manipulation = ManipulationState.Idle;
        this.Velocity = Vector2.zero;
        this.isDragging = false;
        this.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        this.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        this.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        this.rectTransform.sizeDelta = Vector2.one * sizePixels;
        this.transform.localScale = Vector3.one;
        this.transform.localRotation = Quaternion.identity;
        this.ClearFeedback();
        this.itemImage.sprite = sprite;
        this.itemImage.preserveAspect = true;
        this.itemImage.raycastTarget = true;

        this.UpdateVisualState();
        this.gameObject.SetActive(true);
    }

    /// <summary>풀 반환 전에 드래그 구독과 이전 손님의 표시·물리 상태를 제거합니다.</summary>
    internal void ResetForPool()
    {
        if (this.rectTransform == null)
        {
            this.rectTransform = (RectTransform)this.transform;
            this.itemImage = this.GetComponent<Image>();
        }

        this.ClearFeedback();
        this.DragStarted = null;
        this.Dragged = null;
        this.DragEnded = null;
        this.workArea = null;
        this.isDragging = false;
        this.State = SortingState.Working;
        this.Manipulation = ManipulationState.Idle;
        this.Velocity = Vector2.zero;
        this.rectTransform.anchoredPosition = Vector2.zero;
        this.transform.localScale = Vector3.one;
        this.transform.localRotation = Quaternion.identity;
        if (this.itemImage != null)
        {
            this.itemImage.sprite = null;
            this.itemImage.color = Color.white;
            this.itemImage.raycastTarget = true;
        }
    }

    /// <summary>마우스 클릭 시 드래그 준비를 처리합니다.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
    }

    /// <summary>마우스 클릭 해제 시 처리를 수행합니다.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
    }

    /// <summary>아이템을 집어 올리고 드래그를 시작합니다.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        this.isDragging = true;
        this.Manipulation = ManipulationState.PlayerDragging;
        this.Velocity = Vector2.zero;
        this.transform.SetAsLastSibling();
        this.transform.localScale = Vector3.one * 1.08f;
        this.DragStarted?.Invoke(this);
    }

    /// <summary>마우스 위치를 따라 아이템 위치를 갱신합니다.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!this.isDragging) return;

        RectTransform parentRect = this.workArea != null ? this.workArea : (RectTransform)this.transform.parent;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            Rect bounds = parentRect.rect;
            float radius = this.Radius;
            float clampedX = Mathf.Clamp(localPoint.x, bounds.xMin + radius, bounds.xMax - radius);
            float clampedY = Mathf.Clamp(localPoint.y, bounds.yMin + radius, bounds.yMax - radius);
            this.Position = new Vector2(clampedX, clampedY);
        }

        this.Dragged?.Invoke(this);
    }

    /// <summary>아이템을 내려놓고 드래그를 마칩니다.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        this.isDragging = false;
        if (this.Manipulation == ManipulationState.PlayerDragging)
        {
            this.Manipulation = ManipulationState.Idle;
        }
        this.transform.localScale = Vector3.one;

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        this.Velocity = Vector2.ClampMagnitude(eventData.delta / dt * 0.15f, 150f);

        this.DragEnded?.Invoke(this);
        this.UpdateVisualState();
    }

    /// <summary>현재 분류 상태(판매/미분류/제외)에 맞춰 아이템 색상을 갱신합니다.</summary>
    public void UpdateVisualState()
    {
        if (this.itemImage == null) return;

        switch (this.State)
        {
            case SortingState.ForSale:
                // 판매 구역 틴트입니다. 손맛 연출에서는 디스토피아 톤에 맞춰 바랜 황동색을 씁니다.
                this.itemImage.color = UseMutedSaleTint
                    ? new Color(1f, 0.9f, 0.68f, 1f)
                    : new Color(0.85f, 1.0f, 0.85f, 1f);
                break;

            case SortingState.Excluded:
                this.itemImage.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                break;

            case SortingState.Working:
            default:
                this.itemImage.color = Color.white;
                break;
        }
    }

    /// <summary>집어 올린 상품을 살짝 키우고 아래에 그림자를 떨어뜨려 들린 느낌을 줍니다.</summary>
    /// <param name="liftScale">들린 동안의 배율입니다.</param>
    /// <param name="shadowPixels">그림자가 아래로 떨어지는 거리(픽셀)입니다.</param>
    internal void PlayLift(float liftScale, float shadowPixels)
    {
        this.killFeedbackTween();
        this.dragTiltDegrees = 0f;
        Shadow shadow = this.ensureShadow();
        shadow.enabled = true;
        shadow.effectDistance = new Vector2(Mathf.Round(shadowPixels * 0.5f), -Mathf.Round(shadowPixels));
        // 1프레임 눌렸다가 들리는 느낌을 위해 아주 짧게 작아졌다 커집니다.
        this.transform.localScale = Vector3.one * 0.94f;
        this.feedbackTween = this.transform.DOScale(liftScale, 0.08f)
            .SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(this);
    }

    /// <summary>드래그 중 좌우 이동 속도에 따라 상품을 계단식으로 기울입니다.</summary>
    /// <param name="horizontalSpeed">작업대 로컬 좌표 기준 가로 속도(픽셀/초)입니다.</param>
    /// <param name="maxDegrees">최대 기울기(도)입니다.</param>
    /// <param name="deltaSeconds">이번 프레임 시간(초)입니다.</param>
    internal void UpdateDragTilt(float horizontalSpeed, float maxDegrees, float deltaSeconds)
    {
        float target = Mathf.Clamp(-horizontalSpeed * 0.012f, -maxDegrees, maxDegrees);
        this.dragTiltDegrees = Mathf.Lerp(this.dragTiltDegrees, target, 1f - Mathf.Exp(-14f * deltaSeconds));
        // 픽셀 아트가 번지지 않도록 2도 단위로만 기울입니다.
        this.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(this.dragTiltDegrees / 2f) * 2f);
    }

    /// <summary>
    /// 내려놓은 상품을 잠깐 멈췄다가(히트스톱) 납작하게 찌그러뜨린 뒤 원래 모양으로 돌립니다.
    /// 판매 구역이면 한 번 더 작게 튀어 올라 등록된 느낌을 줍니다.
    /// </summary>
    /// <param name="squash">찌그러지는 비율입니다. 0.18이면 가로 118%, 세로 82%입니다.</param>
    /// <param name="hitstopSeconds">착지 직후 멈춰 있는 시간(초)입니다.</param>
    /// <param name="isForSale">판매 구역에 놓였는지 여부입니다.</param>
    internal void PlayDrop(float squash, float hitstopSeconds, bool isForSale)
    {
        this.killFeedbackTween();
        if (this.liftShadow != null) this.liftShadow.enabled = false;
        this.dragTiltDegrees = 0f;
        this.transform.localRotation = Quaternion.identity;
        this.transform.localScale = new Vector3(1f + squash, 1f - squash, 1f);

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        sequence.AppendInterval(Mathf.Max(0f, hitstopSeconds));
        sequence.Append(this.transform.DOScale(new Vector3(1f - squash * 0.4f, 1f + squash * 0.4f, 1f), 0.06f).SetEase(Ease.OutQuad));
        if (isForSale)
        {
            sequence.Append(this.transform.DOScale(Vector3.one * (1f + squash * 0.6f), 0.06f).SetEase(Ease.OutQuad));
        }
        sequence.Append(this.transform.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutQuad));
        this.feedbackTween = sequence;
    }

    /// <summary>쏟기 착지처럼 외부 연출이 지정한 배율을 그대로 적용합니다.</summary>
    /// <param name="scale">적용할 배율입니다.</param>
    internal void SetFeedbackScale(Vector3 scale)
    {
        this.transform.localScale = scale;
    }

    /// <summary>진행 중인 손맛 연출을 멈추고 크기·기울기·그림자를 기본값으로 돌립니다.</summary>
    internal void ClearFeedback()
    {
        this.killFeedbackTween();
        this.dragTiltDegrees = 0f;
        if (this.liftShadow != null) this.liftShadow.enabled = false;
        this.transform.localScale = Vector3.one;
        this.transform.localRotation = Quaternion.identity;
    }

    /// <summary>오브젝트가 꺼질 때 남은 연출 tween을 정리합니다.</summary>
    private void OnDisable()
    {
        this.killFeedbackTween();
    }

    /// <summary>현재 소유한 연출 tween을 종료합니다.</summary>
    private void killFeedbackTween()
    {
        if (this.feedbackTween != null && this.feedbackTween.IsActive()) this.feedbackTween.Kill(false);
        this.feedbackTween = null;
    }

    /// <summary>들림 그림자 컴포넌트를 처음 필요할 때 한 번만 붙입니다.</summary>
    /// <returns>이 상품의 그림자 컴포넌트입니다.</returns>
    private Shadow ensureShadow()
    {
        if (this.liftShadow == null)
        {
            this.liftShadow = this.GetComponent<Shadow>();
            if (this.liftShadow == null) this.liftShadow = this.gameObject.AddComponent<Shadow>();
            // 검은 그림자 대신 어두운 갈색 반투명으로 작업대 나무색에 묻히게 합니다.
            this.liftShadow.effectColor = new Color(0.12f, 0.08f, 0.05f, 0.45f);
            this.liftShadow.useGraphicAlpha = true;
        }

        return this.liftShadow;
    }
}
