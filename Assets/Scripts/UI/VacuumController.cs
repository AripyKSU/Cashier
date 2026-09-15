using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 작업대에서 드래그할 수 있는 UI 청소기의 회전·흡입·보관·배출 연출을 관리합니다.
/// 상품의 최종 판매/제외 판정은 소유자인 SaleSortingPanel이 수행합니다.
/// </summary>
public sealed class VacuumController : MonoBehaviour
{
    private const int SuctionVfxCount = 12;
    private const float SuctionFlipThreshold = 0.85f;
    private const float SuctionFanCosine = -0.17f;
    private const float MinimumDeltaSeconds = 0.0001f;
    private const float MaximumOutsideBodyRatio = 0.5f;

    private enum VacuumState
    {
        Idle,
        TurningAndDragging,
        Suction,
        Spitting,
        Returning
    }

    [Header("Visual & Transform")]
    [Tooltip("회전·이동·기준 상태를 소유하는 청소기 본체 RectTransform입니다.")]
    [SerializeField] private RectTransform vacuumRect;

    [Tooltip("Astra 청소기 Sprite를 표시하는 Image입니다.")]
    [SerializeField] private Image vacuumImage;

    [Tooltip("청소기 손잡이 위치와 회전 중 포인터 정렬을 위한 기준 영역입니다. 잡기 판정은 본체 전체입니다.")]
    [SerializeField] private RectTransform gripArea;

    [Tooltip("상품 흡입과 배출이 모두 일어나는 실제 입구 Transform입니다. 로컬 아래 방향이 대기 자세의 배출 방향입니다.")]
    [SerializeField] private RectTransform nozzle;

    [Tooltip("흡입 VFX Image가 재사용되는 UI 루트입니다.")]
    [SerializeField] private RectTransform suctionVfxRoot;

    [Tooltip("흡입 VFX에 사용할 원본 바람 재질입니다.")]
    [SerializeField] private Material windMaterial;

    [Header("Timing")]
    [Tooltip("청소기 뒤집기 감쇠 시간입니다.")]
    [SerializeField, Min(0.04f)] private float turnSmoothSeconds = 0.11f;

    [Tooltip("기준 위치와 회전으로 돌아오는 감쇠 시간입니다.")]
    [SerializeField, Min(0.04f)] private float returnSmoothSeconds = 0.11f;

    [Tooltip("상품을 노즐로 이동시키는 연출 시간입니다.")]
    [SerializeField, Min(0.05f)] private float swallowSeconds = 0.16f;

    [Tooltip("순차 배출 상품 사이의 시간 간격입니다.")]
    [SerializeField, Min(0.02f)] private float spitIntervalSeconds = 0.07f;

    [Header("UI Coordinates")]
    [Tooltip("노즐 중심에서 흡입 후보를 검사할 최대 거리입니다.")]
    [SerializeField, Min(1f)] private float suctionRadiusPixels = 150f;

    [Tooltip("상품이 노즐로 삼켜지기 시작하는 거리입니다.")]
    [SerializeField, Min(1f)] private float captureRadiusPixels = 28f;

    [Tooltip("흡입 중 상품을 노즐 방향으로 이동시키는 UI 가속도 참고값입니다.")]
    [SerializeField, Min(0f)] private float suctionAccelerationPixels = 1800f;

    [Tooltip("배출 방향으로 노즐에서 띄우는 최소 거리입니다.")]
    [SerializeField, Min(0f)] private float spitOffsetPixels = 12f;

    [Tooltip("배출 시 사용하는 UI 속도 참고값입니다. 제품은 물리 적분을 사용하지 않습니다.")]
    [SerializeField, Min(0f)] private float spitSpeedPixels = 420f;

    [Tooltip("배출 방향의 결정적 각도 변화 최대값입니다.")]
    [SerializeField, Min(0f)] private float spitAngleDegrees = 7f;

    [Tooltip("배출 속도의 결정적 변화량입니다. 0.1은 90~110%를 의미합니다.")]
    [SerializeField, Range(0f, 0.5f)] private float spitSpeedVariation = 0.1f;

    private readonly List<StoredItem> storedItems = new List<StoredItem>();
    private readonly List<SaleSortingItemView> pendingSpatItems = new List<SaleSortingItemView>();
    private readonly Image[] suctionVfx = new Image[SuctionVfxCount];

    private RectTransform workArea;
    private RectTransform itemRoot;
    private VacuumState state;
    private bool isHolding;
    private bool hasStartTransform;
    private Vector2 startAnchoredPosition;
    private Quaternion startLocalRotation;
    private Vector3 startLocalScale;
    private Vector2 dragOffset;
    private Vector2 returnVelocity;
    private float flip;
    private float flipVelocity;
    private float spitElapsed;
    private float vfxElapsed;
    private int spitSequence;

    /// <summary>실제 포인터 버튼으로 청소기를 잡고 있는지 나타냅니다.</summary>
    public bool IsHolding => this.isHolding;

    /// <summary>버튼은 놓았지만 저장 상품을 순차 배출 중인지 나타냅니다.</summary>
    public bool IsSpitting => this.state == VacuumState.Spitting;

    /// <summary>회전·흡입·배출·복귀 중 다른 입력을 막아야 하는지 나타냅니다.</summary>
    public bool IsBusy => this.state != VacuumState.Idle;

    /// <summary>흡입 연출 중이거나 청소기 내부에 저장된 상품 수입니다.</summary>
    public int StoredItemCount => this.storedItems.Count;

    /// <summary>기존 외부 사용처와의 호환을 위한 저장 상품 수 조회입니다.</summary>
    public int AttachedItemCount => this.StoredItemCount;

    /// <summary>청소기가 사용하는 작업대와 상품 좌표 기준을 초기화합니다.</summary>
    /// <param name="workAreaRect">청소기를 제한할 작업대 RectTransform입니다.</param>
    /// <param name="itemRootRect">상품 위치가 저장되는 RectTransform입니다.</param>
    public void Initialize(RectTransform workAreaRect, RectTransform itemRootRect)
    {
        this.workArea = workAreaRect;
        this.itemRoot = itemRootRect;
        this.resolveReferences();
        if (!this.hasStartTransform && this.vacuumRect != null)
        {
            this.startAnchoredPosition = this.vacuumRect.anchoredPosition;
            this.startLocalRotation = this.vacuumRect.localRotation;
            this.startLocalScale = this.vacuumRect.localScale;
            this.hasStartTransform = true;
        }

        this.ensureSuctionVfxPool();
        this.ResetToStart();
    }

    /// <summary>저장 상품과 연출을 정리하고 청소기를 저작 기준 상태로 되돌립니다.</summary>
    public void ResetToStart()
    {
        this.CancelAndRestoreItems();
        this.pendingSpatItems.Clear();
        this.dragOffset = Vector2.zero;
        this.returnVelocity = Vector2.zero;
        this.spitElapsed = 0f;
        this.spitSequence = 0;
        this.flip = 0f;
        this.flipVelocity = 0f;
        this.state = VacuumState.Idle;
        this.applyFlipRotation(0f);
        if (this.vacuumRect != null && this.hasStartTransform)
        {
            this.vacuumRect.anchoredPosition = this.startAnchoredPosition;
            this.vacuumRect.localRotation = this.startLocalRotation;
            this.vacuumRect.localScale = this.startLocalScale;
        }

        this.setSuctionVfxVisible(false);
    }

    /// <summary>청소기 Sprite 표시 여부를 설정합니다. 내부 상태는 변경하지 않습니다.</summary>
    /// <param name="visible">표시할지 여부입니다.</param>
    public void SetVisible(bool visible)
    {
        this.resolveReferences();
        if (this.vacuumImage != null)
        {
            this.vacuumImage.enabled = visible;
        }
        else if (this.gameObject != null)
        {
            this.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            this.setSuctionVfxVisible(false);
        }
    }

    /// <summary>
    /// 포인터 입력에 따라 청소기를 이동·회전하고 노즐 기준 상품 흡입 및 배출을 갱신합니다.
    /// 상품의 Velocity는 기록만 하며 UI에서 물리 적분하지 않습니다.
    /// </summary>
    /// <param name="allowed">현재 청소기가 새 입력을 받을 수 있는지 여부입니다.</param>
    /// <param name="pointerScreenPosition">현재 포인터의 화면 좌표입니다.</param>
    /// <param name="deltaSeconds">Time.unscaledDeltaTime 기반 프레임 경과 시간입니다.</param>
    /// <param name="items">작업대 상품 목록입니다.</param>
    public void UpdateMotion(
        bool allowed,
        Vector2 pointerScreenPosition,
        float deltaSeconds,
        IReadOnlyList<SaleSortingItemView> items)
    {
        this.resolveReferences();
        if (this.vacuumRect == null || this.workArea == null)
        {
            return;
        }

        float dt = Mathf.Max(0f, deltaSeconds);
        if (this.state == VacuumState.Spitting)
        {
            this.advanceSpitting(dt);
            this.applyFlipRotation(1f);
            return;
        }

        if (this.state == VacuumState.Returning)
        {
            this.advanceReturning(dt);
            return;
        }

        this.getPointerButtonState(out bool isPressed, out bool wasPressedThisFrame);
        if (this.isHolding && (!isPressed || !allowed))
        {
            this.BeginRelease();
            return;
        }

        if (!this.isHolding && this.state == VacuumState.Idle && allowed && isPressed && wasPressedThisFrame &&
            this.isPointerInsideVacuum(pointerScreenPosition))
        {
            this.isHolding = true;
            this.state = VacuumState.TurningAndDragging;
            this.dragOffset = this.vacuumRect.anchoredPosition - this.getWorkAreaLocalPointer(pointerScreenPosition);
            SoundManager.Instance?.PlayLoopSfx(SoundKeys.Vacuum);
        }

        if (!this.isHolding)
        {
            this.setSuctionVfxVisible(false);
            return;
        }

        if (dt <= MinimumDeltaSeconds)
        {
            return;
        }

        this.advanceHeld(dt, pointerScreenPosition, items);
    }

    /// <summary>정상 포인터 해제를 시작하고 저장 상품을 순서대로 배출합니다.</summary>
    public void BeginRelease()
    {
        if (!this.isHolding && this.state != VacuumState.TurningAndDragging && this.state != VacuumState.Suction)
        {
            return;
        }

        this.isHolding = false;
        SoundManager.Instance?.StopLoopSfx(SoundKeys.Vacuum);
        this.setSuctionVfxVisible(false);
        this.restoreAndRemoveUnswallowedItems();

        if (this.storedItems.Count == 0)
        {
            this.beginReturning();
            return;
        }

        // 저장 상품이 존재할 때는 배출이 끝날 때까지 정확히 180도 자세를 유지합니다.
        this.state = VacuumState.Spitting;
        this.flip = 1f;
        this.flipVelocity = 0f;
        this.applyFlipRotation(1f);
        this.spitElapsed = this.spitIntervalSeconds;
        this.spitSequence = 0;
        this.emitNextSpatItem();
    }

    /// <summary>
    /// 화면 종료와 시설 비활성화에 사용하는 강제 취소입니다.
    /// 이미 배출되어 Panel 소유가 된 상품은 되돌리지 않고 내부 잔여 상품만 복원합니다.
    /// </summary>
    public void CancelAndRestoreItems()
    {
        this.isHolding = false;
        SoundManager.Instance?.StopLoopSfx(SoundKeys.Vacuum);
        this.setSuctionVfxVisible(false);

        for (int index = this.storedItems.Count - 1; index >= 0; index--)
        {
            this.restoreStoredItem(this.storedItems[index]);
        }

        this.storedItems.Clear();
        this.state = VacuumState.Idle;
        this.flip = 0f;
        this.flipVelocity = 0f;
        this.returnVelocity = Vector2.zero;
        this.spitElapsed = 0f;
        this.spitSequence = 0;
        this.dragOffset = Vector2.zero;
        this.applyFlipRotation(0f);
        if (this.vacuumRect != null && this.hasStartTransform)
        {
            this.vacuumRect.anchoredPosition = this.startAnchoredPosition;
            this.vacuumRect.localRotation = this.startLocalRotation;
            this.vacuumRect.localScale = this.startLocalScale;
        }
    }

    /// <summary>이번 프레임 실제로 배출된 상품만 한 번 반환합니다.</summary>
    /// <returns>외부에서 내부 목록을 수정할 수 없는 배출 상품 목록입니다.</returns>
    public IReadOnlyList<SaleSortingItemView> DrainSpatItems()
    {
        if (this.pendingSpatItems.Count == 0)
        {
            return Array.Empty<SaleSortingItemView>();
        }

        var result = new List<SaleSortingItemView>(this.pendingSpatItems);
        this.pendingSpatItems.Clear();
        return result.AsReadOnly();
    }

    /// <summary>청소기 오브젝트가 비활성화될 때 상품과 효과를 멱등적으로 정리합니다.</summary>
    private void OnDisable()
    {
        this.CancelAndRestoreItems();
        this.pendingSpatItems.Clear();
    }

    /// <summary>파괴될 때도 청소기 내부 상품을 유실하지 않도록 정리합니다.</summary>
    private void OnDestroy()
    {
        this.CancelAndRestoreItems();
        this.pendingSpatItems.Clear();
    }

    /// <summary>잡고 있는 동안 회전·위치·흡입 연출을 갱신합니다.</summary>
    private void advanceHeld(float deltaSeconds, Vector2 pointerScreenPosition, IReadOnlyList<SaleSortingItemView> items)
    {
        this.updateFlip(1f, deltaSeconds, this.turnSmoothSeconds);
        this.alignGripToPointer(this.getWorkAreaLocalPointer(pointerScreenPosition));
        this.state = this.flip > SuctionFlipThreshold ? VacuumState.Suction : VacuumState.TurningAndDragging;
        bool isSuctioning = this.state == VacuumState.Suction;
        this.updateSuctionVfx(isSuctioning, deltaSeconds);
        if (isSuctioning)
        {
            this.collectItems(items);
            this.advanceStoredItems(deltaSeconds);
        }
    }

    /// <summary>저장 전 상품을 노즐 쪽으로 이동시키고 삼킨 상품을 숨깁니다.</summary>
    private void advanceStoredItems(float deltaSeconds)
    {
        for (int index = 0; index < this.storedItems.Count; index++)
        {
            StoredItem stored = this.storedItems[index];
            if (stored.hasSwallowed || stored.item == null)
            {
                continue;
            }

            Vector2 nozzlePosition = this.getNozzleLocalPosition();
            if (!stored.swallowStarted)
            {
                float distance = Vector2.Distance(stored.item.Position, nozzlePosition);
                if (distance > this.captureRadiusPixels)
                {
                    float movementBlend = this.suctionAccelerationPixels <= 0f
                        ? 0f
                        : 1f - Mathf.Exp(-(this.suctionAccelerationPixels / 1800f) * deltaSeconds);
                    stored.item.Position = Vector2.Lerp(stored.item.Position, nozzlePosition, movementBlend);
                    continue;
                }

                stored.swallowStarted = true;
                stored.swallowStartPosition = stored.item.Position;
                stored.elapsedSeconds = 0f;
            }

            stored.elapsedSeconds += deltaSeconds;
            float progress = Mathf.Clamp01(stored.elapsedSeconds / Mathf.Max(MinimumDeltaSeconds, this.swallowSeconds));
            float accelerationBlend = Mathf.Clamp01(this.suctionAccelerationPixels / 1800f);
            float eased = Mathf.Lerp(progress, progress * progress, accelerationBlend);
            stored.item.Position = Vector2.Lerp(stored.swallowStartPosition, nozzlePosition, eased);
            stored.item.transform.localScale = Vector3.Lerp(
                stored.originalLocalScale,
                stored.originalLocalScale * 0.02f,
                eased);
            stored.item.transform.localRotation = stored.originalLocalRotation *
                Quaternion.Euler(0f, 0f, 150f * eased);

            if (progress < 1f)
            {
                continue;
            }

            stored.hasSwallowed = true;
            stored.item.gameObject.SetActive(false);
        }
    }

    /// <summary>순차 배출 타이머를 진행합니다.</summary>
    private void advanceSpitting(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        this.spitElapsed -= deltaSeconds;
        while (this.state == VacuumState.Spitting && this.spitElapsed <= 0f)
        {
            this.emitNextSpatItem();
            if (this.state != VacuumState.Spitting)
            {
                break;
            }

            this.spitElapsed += Mathf.Max(0.02f, this.spitIntervalSeconds);
        }
    }

    /// <summary>청소기를 기준 Transform으로 부드럽게 복귀시킵니다.</summary>
    private void advanceReturning(float deltaSeconds)
    {
        if (this.vacuumRect == null || deltaSeconds <= 0f)
        {
            return;
        }

        this.updateFlip(0f, deltaSeconds, this.returnSmoothSeconds);
        this.vacuumRect.anchoredPosition = Vector2.SmoothDamp(
            this.vacuumRect.anchoredPosition,
            this.startAnchoredPosition,
            ref this.returnVelocity,
            Mathf.Max(0.04f, this.returnSmoothSeconds),
            Mathf.Infinity,
            deltaSeconds);
        this.vacuumRect.localScale = this.startLocalScale;
        if ((this.vacuumRect.anchoredPosition - this.startAnchoredPosition).sqrMagnitude < 0.01f &&
            Mathf.Abs(this.flip) < 0.001f)
        {
            this.vacuumRect.anchoredPosition = this.startAnchoredPosition;
            this.vacuumRect.localRotation = this.startLocalRotation;
            this.returnVelocity = Vector2.zero;
            this.state = VacuumState.Idle;
        }
    }

    /// <summary>정상 배출 중 다음 저장 상품을 Panel 소유로 넘깁니다.</summary>
    private void emitNextSpatItem()
    {
        if (this.storedItems.Count == 0)
        {
            this.beginReturning();
            return;
        }

        StoredItem stored = this.storedItems[0];
        this.storedItems.RemoveAt(0);
        SaleSortingItemView item = stored.item;
        if (item == null)
        {
            this.emitNextSpatItem();
            return;
        }

        float sample = this.spitSequence + 1f;
        float angle = Mathf.Sin(sample * 2.17f) * this.spitAngleDegrees;
        float speedMultiplier = Mathf.Lerp(
            1f - this.spitSpeedVariation,
            1f + this.spitSpeedVariation,
            Mathf.Repeat(sample * 0.37f, 1f));
        Vector2 direction = Quaternion.Euler(0f, 0f, angle) * this.getNozzleLocalDirection();
        Vector2 position = this.getNozzleLocalPosition() + direction * this.spitOffsetPixels;

        item.gameObject.SetActive(true);
        item.Position = this.clampItemPosition(position, item);
        item.transform.localScale = stored.originalLocalScale;
        item.transform.localRotation = stored.originalLocalRotation *
            Quaternion.Euler(0f, 0f, Mathf.Sin(sample * 1.31f) * 12f);
        item.State = SaleSortingItemView.SortingState.Working;
        item.Manipulation = SaleSortingItemView.ManipulationState.Idle;
        item.Velocity = direction * (this.spitSpeedPixels * speedMultiplier);
        item.UpdateVisualState();
        item.transform.SetAsLastSibling();
        this.pendingSpatItems.Add(item);
        this.spitSequence++;

        if (this.storedItems.Count == 0)
        {
            this.beginReturning();
        }
    }

    /// <summary>복귀 상태로 전환합니다.</summary>
    private void beginReturning()
    {
        this.state = VacuumState.Returning;
        this.returnVelocity = Vector2.zero;
        this.setSuctionVfxVisible(false);
    }

    /// <summary>포인터 해제 시 아직 삼키지 못한 상품만 원상복구합니다.</summary>
    private void restoreAndRemoveUnswallowedItems()
    {
        for (int index = this.storedItems.Count - 1; index >= 0; index--)
        {
            StoredItem stored = this.storedItems[index];
            if (stored.hasSwallowed)
            {
                continue;
            }

            this.restoreStoredItem(stored);
            this.storedItems.RemoveAt(index);
        }
    }

    /// <summary>상품의 흡입 전 상태를 복원합니다.</summary>
    private void restoreStoredItem(StoredItem stored)
    {
        SaleSortingItemView item = stored.item;
        if (item == null)
        {
            return;
        }

        item.gameObject.SetActive(stored.originalActiveSelf);
        item.Position = stored.originalPosition;
        item.transform.localRotation = stored.originalLocalRotation;
        item.transform.localScale = stored.originalLocalScale;
        item.Velocity = stored.originalVelocity;
        item.State = stored.originalSortingState;
        item.Manipulation = stored.originalManipulationState;
        item.UpdateVisualState();
    }

    /// <summary>노즐 부채꼴 안의 유효한 상품을 저장 목록에 한 번만 등록합니다.</summary>
    private void collectItems(IReadOnlyList<SaleSortingItemView> items)
    {
        if (this.itemRoot == null || this.nozzle == null || items == null)
        {
            return;
        }

        Vector2 nozzlePosition = this.getNozzleLocalPosition();
        Vector2 suctionDirection = this.getNozzleLocalDirection();
        for (int index = 0; index < items.Count; index++)
        {
            SaleSortingItemView item = items[index];
            if (item == null || !item.gameObject.activeInHierarchy ||
                item.State == SaleSortingItemView.SortingState.Excluded ||
                item.Manipulation != SaleSortingItemView.ManipulationState.Idle ||
                this.containsStoredItem(item))
            {
                continue;
            }

            Vector2 delta = item.Position - nozzlePosition;
            float distance = delta.magnitude;
            if (distance > this.suctionRadiusPixels)
            {
                continue;
            }

            if (distance > 0.05f && Vector2.Dot(delta / distance, suctionDirection) < SuctionFanCosine)
            {
                continue;
            }

            var stored = new StoredItem(item, this.getNozzleLocalPosition());
            item.Manipulation = SaleSortingItemView.ManipulationState.VacuumAttached;
            item.Velocity = Vector2.zero;
            this.storedItems.Add(stored);
            SoundManager.Instance?.PlaySfx(SoundKeys.ItemPickup);
        }
    }

    /// <summary>상품이 이미 흡입 연출 또는 내부 저장 상태인지 확인합니다.</summary>
    private bool containsStoredItem(SaleSortingItemView item)
    {
        for (int index = 0; index < this.storedItems.Count; index++)
        {
            if (this.storedItems[index].item == item)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>회전 진행도를 실제 본체 회전으로 반영합니다.</summary>
    private void updateFlip(float target, float deltaSeconds, float smoothSeconds)
    {
        this.flip = Mathf.SmoothDamp(
            this.flip,
            target,
            ref this.flipVelocity,
            Mathf.Max(0.04f, smoothSeconds),
            Mathf.Infinity,
            deltaSeconds);
        if (Mathf.Abs(this.flip - target) < 0.001f)
        {
            this.flip = target;
            this.flipVelocity = 0f;
        }

        this.applyFlipRotation(this.flip);
    }

    /// <summary>저작 기준 회전에 180도 뒤집기 진행도를 적용합니다.</summary>
    private void applyFlipRotation(float progress)
    {
        if (this.vacuumRect == null || !this.hasStartTransform)
        {
            return;
        }

        float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        this.vacuumRect.localRotation = this.startLocalRotation * Quaternion.Euler(0f, 0f, 180f * eased);
    }

    /// <summary>손잡이가 포인터에 붙도록 회전 후 본체 위치를 보정합니다.</summary>
    private void alignGripToPointer(Vector2 localPointer)
    {
        if (this.gripArea == null || this.workArea == null || this.vacuumRect == null)
        {
            this.vacuumRect.anchoredPosition = this.clampVacuumPosition(localPointer + this.dragOffset);
            return;
        }

        Vector3 pointerWorld = this.workArea.TransformPoint(localPointer);
        this.vacuumRect.position += pointerWorld - this.gripArea.position;
        Vector2 rootLocalPosition = this.workArea.InverseTransformPoint(this.vacuumRect.position);
        this.vacuumRect.anchoredPosition = this.clampVacuumPosition(rootLocalPosition);
    }

    /// <summary>청소기 중심 위치를 제한하되 본체 절반까지 작업대 밖으로 허용합니다.</summary>
    private Vector2 clampVacuumPosition(Vector2 targetPosition)
    {
        Rect bounds = this.workArea.rect;
        Vector2 halfSize = Vector2.zero;
        if (this.vacuumRect != null)
        {
            Vector3 scale = this.vacuumRect.localScale;
            halfSize = new Vector2(
                this.vacuumRect.rect.width * Mathf.Abs(scale.x),
                this.vacuumRect.rect.height * Mathf.Abs(scale.y)) * 0.5f;
        }

        float centerInsetRatio = Mathf.Clamp01(1f - MaximumOutsideBodyRatio * 2f);
        return new Vector2(
            Mathf.Clamp(targetPosition.x, bounds.xMin + halfSize.x * centerInsetRatio, bounds.xMax - halfSize.x * centerInsetRatio),
            Mathf.Clamp(targetPosition.y, bounds.yMin + halfSize.y * centerInsetRatio, bounds.yMax - halfSize.y * centerInsetRatio));
    }

    /// <summary>현재 Nozzle 위치를 상품 ItemRoot 로컬 좌표로 변환합니다.</summary>
    private Vector2 getNozzleLocalPosition()
    {
        if (this.itemRoot == null || this.nozzle == null)
        {
            return Vector2.zero;
        }

        Vector3 local = this.itemRoot.InverseTransformPoint(this.nozzle.position);
        return new Vector2(local.x, local.y);
    }

    /// <summary>실제 Nozzle의 로컬 아래 방향을 ItemRoot 좌표계로 변환합니다.</summary>
    private Vector2 getNozzleLocalDirection()
    {
        if (this.itemRoot == null || this.nozzle == null)
        {
            return Vector2.down;
        }

        Vector3 worldDirection = -this.nozzle.up;
        Vector3 localDirection = this.itemRoot.InverseTransformDirection(worldDirection);
        Vector2 result = new Vector2(localDirection.x, localDirection.y);
        return result.sqrMagnitude < 0.0001f ? Vector2.down : result.normalized;
    }

    /// <summary>상품 중심이 작업대 밖으로 과도하게 나가지 않도록 최소 제한합니다.</summary>
    private Vector2 clampItemPosition(Vector2 position, SaleSortingItemView item)
    {
        if (this.itemRoot == null || item == null)
        {
            return position;
        }

        Rect bounds = this.itemRoot.rect;
        Vector2 halfSize = item.HalfSize;
        return new Vector2(
            Mathf.Clamp(position.x, bounds.xMin + halfSize.x, bounds.xMax - halfSize.x),
            Mathf.Clamp(position.y, bounds.yMin + halfSize.y, bounds.yMax - halfSize.y));
    }

    /// <summary>포인터 위치를 작업대 로컬 좌표로 변환합니다.</summary>
    private Vector2 getWorkAreaLocalPointer(Vector2 pointerScreenPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            this.workArea,
            pointerScreenPosition,
            null,
            out Vector2 localPointer)
            ? localPointer
            : this.vacuumRect.anchoredPosition;
    }

    /// <summary>청소기 본체 전체 영역에서 잡기를 허용합니다.</summary>
    private bool isPointerInsideVacuum(Vector2 pointerScreenPosition)
    {
        RectTransform target = this.vacuumRect != null ? this.vacuumRect : this.gripArea;
        return target != null && RectTransformUtility.RectangleContainsScreenPoint(target, pointerScreenPosition, null);
    }

    /// <summary>직렬화 참조가 비어 있을 때 제품 Prefab의 기본 컴포넌트를 보완합니다.</summary>
    private void resolveReferences()
    {
        if (this.vacuumRect == null)
        {
            this.vacuumRect = this.transform as RectTransform;
        }

        if (this.vacuumImage == null)
        {
            Transform visual = this.transform.Find("Visual");
            this.vacuumImage = visual == null ? this.GetComponent<Image>() : visual.GetComponent<Image>();
        }

        if (this.nozzle == null)
        {
            Transform candidate = this.transform.Find("Nozzle") ?? this.transform.Find("SuctionArea");
            this.nozzle = candidate as RectTransform;
        }

        if (this.gripArea == null)
        {
            this.gripArea = this.transform.Find("GripArea") as RectTransform;
        }

        if (this.suctionVfxRoot == null)
        {
            this.suctionVfxRoot = this.transform.Find("SuctionVfxRoot") as RectTransform;
        }
    }

    /// <summary>12개 UI Image를 한 번만 만들고 재사용합니다.</summary>
    private void ensureSuctionVfxPool()
    {
        if (this.suctionVfxRoot == null)
        {
            return;
        }

        for (int index = 0; index < this.suctionVfx.Length; index++)
        {
            Transform existing = this.suctionVfxRoot.Find($"SuctionVfx{index}");
            GameObject go = existing == null ? new GameObject($"SuctionVfx{index}") : existing.gameObject;
            if (existing == null)
            {
                go.transform.SetParent(this.suctionVfxRoot, false);
            }

            Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.raycastTarget = false;
            image.material = this.windMaterial;
            image.enabled = false;
            this.suctionVfx[index] = image;
        }
    }

    /// <summary>흡입 중인 12개 VFX 스트릭을 갱신합니다.</summary>
    private void updateSuctionVfx(bool visible, float deltaSeconds)
    {
        if (!visible || this.suctionVfxRoot == null)
        {
            this.setSuctionVfxVisible(false);
            return;
        }

        this.vfxElapsed += deltaSeconds;
        for (int index = 0; index < this.suctionVfx.Length; index++)
        {
            Image image = this.suctionVfx[index];
            if (image == null)
            {
                continue;
            }

            float phase = Mathf.Repeat(this.vfxElapsed * 1.7f + index / (float)this.suctionVfx.Length, 1f);
            float distance = (1f - phase) * this.suctionRadiusPixels;
            float lane = (index % 5 - 2) * 0.12f;
            RectTransform rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(lane * distance, -distance);
            rect.sizeDelta = new Vector2(2.5f + (index % 3), 14f + phase * 18f);
            rect.localRotation = Quaternion.identity;
            image.color = new Color(0.72f, 0.9f, 1f, 0.12f + 0.5f * phase);
            image.enabled = true;
        }
    }

    /// <summary>모든 흡입 VFX 스트릭을 숨깁니다.</summary>
    private void setSuctionVfxVisible(bool visible)
    {
        for (int index = 0; index < this.suctionVfx.Length; index++)
        {
            if (this.suctionVfx[index] != null)
            {
                this.suctionVfx[index].enabled = visible;
            }
        }
    }

    /// <summary>현재 포인터의 마우스 누름 상태를 반환합니다.</summary>
    private void getPointerButtonState(out bool isPressed, out bool wasPressedThisFrame)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        isPressed = mouse != null && mouse.leftButton.isPressed;
        wasPressedThisFrame = mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        isPressed = Input.GetMouseButton(0);
        wasPressedThisFrame = Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>상품의 흡입 전 상태와 개별 연출 진행도를 저장합니다.</summary>
    private sealed class StoredItem
    {
        internal readonly SaleSortingItemView item;
        internal readonly Vector2 originalPosition;
        internal readonly Quaternion originalLocalRotation;
        internal readonly Vector3 originalLocalScale;
        internal readonly Vector2 originalVelocity;
        internal readonly SaleSortingItemView.SortingState originalSortingState;
        internal readonly SaleSortingItemView.ManipulationState originalManipulationState;
        internal readonly bool originalActiveSelf;
        internal Vector2 swallowStartPosition;
        internal float elapsedSeconds;
        internal bool swallowStarted;
        internal bool hasSwallowed;

        internal StoredItem(SaleSortingItemView item, Vector2 swallowStartPosition)
        {
            this.item = item;
            this.originalPosition = item.Position;
            this.originalLocalRotation = item.transform.localRotation;
            this.originalLocalScale = item.transform.localScale;
            this.originalVelocity = item.Velocity;
            this.originalSortingState = item.State;
            this.originalManipulationState = item.Manipulation;
            this.originalActiveSelf = item.gameObject.activeSelf;
            this.swallowStartPosition = this.originalPosition;
        }
    }
}
