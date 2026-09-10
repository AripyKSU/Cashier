using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 작업대에서 드래그할 수 있는 청소기와 하단 흡착 영역을 관리합니다.
/// 상품의 분류 상태 판정은 소유자인 SaleSortingPanel이 수행합니다.
/// </summary>
public sealed class VacuumController : MonoBehaviour
{
    [Header("Visual & Transform")]
    [Tooltip("청소기 본체 RectTransform입니다.")]
    [SerializeField] private RectTransform vacuumRect;

    [Tooltip("정식 Sprite를 연결할 수 있는 청소기 이미지입니다.")]
    [SerializeField] private Image vacuumImage;

    [Tooltip("청소기 아래 중앙의 상품 흡착 영역입니다.")]
    [SerializeField] private RectTransform suctionArea;

    [Header("Placement")]
    [Tooltip("작업대 중심 기준 청소기 시작 X 위치입니다.")]
    [SerializeField] private float startOffsetX = -330f;

    [Tooltip("흡착된 상품을 위로 들어 올리는 픽셀 오프셋입니다.")]
    [SerializeField, Min(0f)] private float liftOffsetPixels = 18f;

    [Tooltip("흡착된 상품 사이의 가로 간격입니다.")]
    [SerializeField, Min(0f)] private float attachedSpacingPixels = 10f;

    [Tooltip("청소기 본체가 포인터를 따라가는 보간 속도입니다.")]
    [SerializeField, Min(1f)] private float positionFollowSpeed = 32f;

    private readonly List<SaleSortingItemView> attachedItems = new List<SaleSortingItemView>();
    private RectTransform workArea;
    private RectTransform itemRoot;
    private bool isHolding;
    private Vector2 movementDelta;

    /// <summary>현재 청소기 본체를 잡고 있는지 나타냅니다.</summary>
    public bool IsHolding => this.isHolding;

    /// <summary>현재 청소기에 붙어 있는 상품 수입니다.</summary>
    public int AttachedItemCount => this.attachedItems.Count;

    /// <summary>청소기 본체와 상품 작업대의 좌표 기준을 초기화합니다.</summary>
    /// <param name="workAreaRect">청소기를 제한할 작업대 RectTransform입니다.</param>
    /// <param name="itemRootRect">상품 위치가 저장되는 RectTransform입니다.</param>
    public void Initialize(RectTransform workAreaRect, RectTransform itemRootRect)
    {
        this.workArea = workAreaRect;
        this.itemRoot = itemRootRect;
        this.resolveReferences();
        this.ResetToStart();
    }

    /// <summary>청소기를 작업대 왼쪽 위의 기본 대기 위치로 되돌립니다.</summary>
    public void ResetToStart()
    {
        this.isHolding = false;
        this.movementDelta = Vector2.zero;
        if (this.vacuumRect == null)
        {
            return;
        }

        this.vacuumRect.anchoredPosition = new Vector2(this.startOffsetX, 0f);
        this.vacuumRect.localRotation = Quaternion.identity;
    }

    /// <summary>청소기 임시 이미지를 표시하거나 숨깁니다.</summary>
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
    }

    /// <summary>
    /// 포인터 입력에 따라 청소기를 이동하고 흡착 영역에 닿은 대기 상품을 수집합니다.
    /// </summary>
    /// <param name="allowed">현재 청소기 조작이 허용되는지 여부입니다.</param>
    /// <param name="pointerScreenPosition">현재 포인터의 화면 좌표입니다.</param>
    /// <param name="deltaSeconds">프레임 경과 시간입니다.</param>
    /// <param name="items">작업대 상품 목록입니다.</param>
    public void UpdateMotion(
        bool allowed,
        Vector2 pointerScreenPosition,
        float deltaSeconds,
        IReadOnlyList<SaleSortingItemView> items)
    {
        this.resolveReferences();
        if (this.vacuumRect == null || this.workArea == null || deltaSeconds <= 0f)
        {
            return;
        }

        if (!allowed)
        {
            this.isHolding = false;
            this.movementDelta = Vector2.zero;
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.workArea,
                pointerScreenPosition,
                null,
                out Vector2 localPointer))
        {
            this.isHolding = false;
            this.movementDelta = Vector2.zero;
            return;
        }

        this.getPointerButtonState(out bool isPressed, out bool wasPressedThisFrame);
        if (isPressed && !this.isHolding && wasPressedThisFrame &&
            RectTransformUtility.RectangleContainsScreenPoint(this.vacuumRect, pointerScreenPosition, null))
        {
            this.isHolding = true;
        }

        if (!isPressed)
        {
            this.isHolding = false;
            this.movementDelta = Vector2.zero;
            return;
        }

        if (!this.isHolding)
        {
            this.movementDelta = Vector2.zero;
            return;
        }

        Vector2 currentPosition = this.vacuumRect.anchoredPosition;
        Vector2 targetPosition = this.clampVacuumPosition(localPointer);
        Vector2 nextPosition = Vector2.Lerp(
            currentPosition,
            targetPosition,
            Mathf.Clamp01(deltaSeconds * this.positionFollowSpeed));
        this.vacuumRect.anchoredPosition = nextPosition;
        this.movementDelta = nextPosition - currentPosition;

        this.collectItems(items);
        this.updateAttachedItemPositions(true);
    }

    /// <summary>
    /// 청소기에 붙은 상품을 모두 내려놓고 들어 올림 오프셋을 복원합니다.
    /// </summary>
    /// <returns>이번 호출로 해제된 상품 목록입니다.</returns>
    public IReadOnlyList<SaleSortingItemView> ReleaseAttachedItems()
    {
        if (this.attachedItems.Count == 0)
        {
            this.isHolding = false;
            this.movementDelta = Vector2.zero;
            return Array.Empty<SaleSortingItemView>();
        }

        this.updateAttachedItemPositions(false);
        var releasedItems = new List<SaleSortingItemView>(this.attachedItems);
        foreach (SaleSortingItemView item in releasedItems)
        {
            if (item != null)
            {
                item.Manipulation = SaleSortingItemView.ManipulationState.Idle;
            }
        }

        this.attachedItems.Clear();
        this.isHolding = false;
        this.movementDelta = Vector2.zero;
        return releasedItems.AsReadOnly();
    }

    /// <summary>Inspector 참조가 비어 있는 경우 기본 컴포넌트 참조를 확보합니다.</summary>
    private void resolveReferences()
    {
        if (this.vacuumRect == null)
        {
            this.vacuumRect = this.transform as RectTransform;
        }

        if (this.vacuumImage == null)
        {
            this.vacuumImage = this.GetComponent<Image>();
        }
    }

    /// <summary>흡착 영역에 닿은 대기 상품을 청소기에 등록합니다.</summary>
    /// <param name="items">검사할 작업대 상품 목록입니다.</param>
    private void collectItems(IReadOnlyList<SaleSortingItemView> items)
    {
        if (this.suctionArea == null || items == null)
        {
            return;
        }

        for (int index = 0; index < items.Count; index++)
        {
            SaleSortingItemView item = items[index];
            if (item == null || !item.gameObject.activeInHierarchy ||
                item.Manipulation != SaleSortingItemView.ManipulationState.Idle ||
                this.attachedItems.Contains(item))
            {
                continue;
            }

            Vector2 itemScreenPosition = RectTransformUtility.WorldToScreenPoint(null, item.transform.position);
            if (!RectTransformUtility.RectangleContainsScreenPoint(this.suctionArea, itemScreenPosition, null))
            {
                continue;
            }

            this.attachedItems.Add(item);
            item.Manipulation = SaleSortingItemView.ManipulationState.VacuumAttached;
        }
    }

    /// <summary>청소기 본체를 작업대 안에 유지할 수 있는 위치로 제한합니다.</summary>
    /// <param name="position">포인터로 계산한 작업대 로컬 위치입니다.</param>
    /// <returns>작업대 내부의 청소기 위치입니다.</returns>
    private Vector2 clampVacuumPosition(Vector2 position)
    {
        Rect bounds = this.workArea.rect;
        Vector2 halfSize = this.vacuumRect.rect.size * 0.5f;
        return new Vector2(
            Mathf.Clamp(position.x, bounds.xMin + halfSize.x, bounds.xMax - halfSize.x),
            Mathf.Clamp(position.y, bounds.yMin + halfSize.y, bounds.yMax - halfSize.y));
    }

    /// <summary>청소기 아래에 상품을 들어 올린 상태 또는 내려놓은 상태로 배치합니다.</summary>
    /// <param name="lifted">true이면 들어 올림 오프셋을 적용합니다.</param>
    private void updateAttachedItemPositions(bool lifted)
    {
        if (this.itemRoot == null || this.attachedItems.Count == 0)
        {
            return;
        }

        Vector3 suctionWorldPosition = this.suctionArea == null
            ? this.vacuumRect.position
            : this.suctionArea.position;
        Vector3 localSuction = this.itemRoot.InverseTransformPoint(suctionWorldPosition);
        float centerIndex = (this.attachedItems.Count - 1) * 0.5f;
        for (int index = 0; index < this.attachedItems.Count; index++)
        {
            SaleSortingItemView item = this.attachedItems[index];
            if (item == null)
            {
                continue;
            }

            Vector2 position = new Vector2(
                localSuction.x + (index - centerIndex) * this.attachedSpacingPixels,
                localSuction.y + (lifted ? this.liftOffsetPixels : 0f));
            item.Position = this.clampItemPosition(position, item);
        }
    }

    /// <summary>상품 중심이 작업대 밖으로 나가지 않도록 위치를 제한합니다.</summary>
    /// <param name="position">상품 중심 후보 위치입니다.</param>
    /// <param name="item">위치를 제한할 상품입니다.</param>
    /// <returns>작업대 안으로 제한된 상품 위치입니다.</returns>
    private Vector2 clampItemPosition(Vector2 position, SaleSortingItemView item)
    {
        Rect bounds = this.itemRoot.rect;
        Vector2 halfSize = item.HalfSize;
        return new Vector2(
            Mathf.Clamp(position.x, bounds.xMin + halfSize.x, bounds.xMax - halfSize.x),
            Mathf.Clamp(position.y, bounds.yMin + halfSize.y, bounds.yMax - halfSize.y));
    }

    /// <summary>현재 포인터의 마우스 누름 상태를 반환합니다.</summary>
    /// <param name="isPressed">현재 버튼을 누르고 있는지 여부입니다.</param>
    /// <param name="wasPressedThisFrame">이번 프레임에 버튼을 눌렀는지 여부입니다.</param>
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
}
