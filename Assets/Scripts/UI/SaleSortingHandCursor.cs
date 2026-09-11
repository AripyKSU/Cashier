using UnityEngine;
using UnityEngine.UI;

/// <summary>분류 화면의 포인터를 손 아트로 표시하고 잡기 방향에 맞는 자세를 선택합니다.</summary>
public sealed class SaleSortingHandCursor : MonoBehaviour
{
    /// <summary>손 커서가 표현할 잡기 상태입니다.</summary>
    public enum HandCursorState
    {
        Released,
        HoldingStill,
        HoldingLeft,
        HoldingRight,
        HandCursorState_End
    }

    [SerializeField] private Canvas cursorCanvas;
    [SerializeField] private RectTransform cursorRect;
    [SerializeField] private Image cursorImage;
    [SerializeField] private Sprite releasedSprite;
    [SerializeField] private Sprite holdingStillSprite;
    [SerializeField] private Sprite holdingLeftSprite;
    [SerializeField] private Sprite holdingRightSprite;
    [SerializeField, Min(0f)] private float directionThresholdPixelsPerSecond = 30f;
    [SerializeField, Min(0f)] private float velocitySmoothing = 20f;

    private Vector2 previousPointerScreenPosition;
    private Vector2 smoothedVelocity;
    private bool hasPointerSample;
    private bool ownsCursorVisibility;
    private bool previousCursorVisible;

    /// <summary>현재 화면에 적용된 손 자세입니다.</summary>
    public HandCursorState CurrentState { get; private set; } = HandCursorState.Released;

    /// <summary>직렬화 참조 누락을 보완하고 입력을 가로채지 않도록 준비합니다.</summary>
    private void Awake()
    {
        if (this.cursorCanvas == null) this.cursorCanvas = this.GetComponent<Canvas>();
        if (this.cursorRect == null) this.cursorRect = this.transform as RectTransform;
        if (this.cursorImage == null) this.cursorImage = this.GetComponentInChildren<Image>(true);
        if (this.cursorImage != null) this.cursorImage.raycastTarget = false;
        this.Hide();
    }

    /// <summary>포인터 위치와 통합 잡기 상태를 현재 손 표현에 반영합니다.</summary>
    /// <param name="visible">손 커서를 표시할 수 있는 상태인지 여부입니다.</param>
    /// <param name="isHolding">물품, 막대 또는 청소기 중 하나를 잡고 있는지 여부입니다.</param>
    /// <param name="pointerScreenPosition">현재 포인터의 화면 픽셀 좌표입니다.</param>
    /// <param name="deltaSeconds">방향 속도를 계산할 실제 경과 시간입니다.</param>
    public void UpdatePresentation(bool visible, bool isHolding, Vector2 pointerScreenPosition, float deltaSeconds)
    {
        if (!visible || this.cursorRect == null || this.cursorImage == null)
        {
            this.Hide();
            return;
        }

        this.moveToScreenPosition(pointerScreenPosition);
        if (!this.hasPointerSample || deltaSeconds <= 0f)
        {
            this.smoothedVelocity = Vector2.zero;
            this.hasPointerSample = true;
        }
        else
        {
            Vector2 velocity = (pointerScreenPosition - this.previousPointerScreenPosition) / deltaSeconds;
            float blend = 1f - Mathf.Exp(-this.velocitySmoothing * deltaSeconds);
            this.smoothedVelocity = Vector2.Lerp(this.smoothedVelocity, velocity, blend);
        }

        this.previousPointerScreenPosition = pointerScreenPosition;
        this.CurrentState = ResolveState(isHolding, this.smoothedVelocity, this.directionThresholdPixelsPerSecond);
        this.cursorImage.sprite = this.getSprite(this.CurrentState);
        if (this.cursorImage.sprite == null)
        {
            this.Hide();
            return;
        }

        this.cursorImage.enabled = true;
        if (this.cursorCanvas != null)
        {
            this.cursorCanvas.enabled = true;
            this.cursorCanvas.transform.SetAsLastSibling();
        }
        else
        {
            this.cursorImage.gameObject.SetActive(true);
        }

        this.hideSystemCursor();
    }

    /// <summary>잡기 여부와 포인터 속도에서 사용할 손 자세를 결정합니다.</summary>
    /// <param name="isHolding">조작 대상을 잡고 있는지 여부입니다.</param>
    /// <param name="velocityPixelsPerSecond">포인터의 화면 픽셀 속도입니다.</param>
    /// <param name="directionThresholdPixelsPerSecond">좌우 자세로 전환할 최소 수평 속도입니다.</param>
    /// <returns>현재 입력에 대응하는 손 자세입니다.</returns>
    public static HandCursorState ResolveState(bool isHolding, Vector2 velocityPixelsPerSecond, float directionThresholdPixelsPerSecond)
    {
        if (!isHolding) return HandCursorState.Released;
        if (Mathf.Abs(velocityPixelsPerSecond.x) <= directionThresholdPixelsPerSecond ||
            Mathf.Abs(velocityPixelsPerSecond.x) <= Mathf.Abs(velocityPixelsPerSecond.y))
            return HandCursorState.HoldingStill;
        return velocityPixelsPerSecond.x < 0f ? HandCursorState.HoldingLeft : HandCursorState.HoldingRight;
    }

    /// <summary>손을 숨기고 이 컴포넌트가 숨겼던 시스템 커서를 복원합니다.</summary>
    public void Hide()
    {
        this.hasPointerSample = false;
        this.smoothedVelocity = Vector2.zero;
        if (this.cursorCanvas != null) this.cursorCanvas.enabled = false;
        else if (this.cursorImage != null) this.cursorImage.gameObject.SetActive(false);
        this.restoreSystemCursor();
    }

    /// <summary>비활성화될 때 시스템 커서 소유권을 남기지 않습니다.</summary>
    private void OnDisable() => this.Hide();

    /// <summary>파괴될 때 시스템 커서 소유권을 남기지 않습니다.</summary>
    private void OnDestroy() => this.restoreSystemCursor();

    /// <summary>애플리케이션 포커스를 잃으면 손과 시스템 커서를 정상 상태로 되돌립니다.</summary>
    /// <param name="hasFocus">애플리케이션이 입력 포커스를 가지고 있는지 여부입니다.</param>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) this.Hide();
    }

    /// <summary>화면 픽셀 좌표를 손 Canvas 좌표로 변환합니다.</summary>
    /// <param name="pointerScreenPosition">현재 포인터의 화면 픽셀 좌표입니다.</param>
    private void moveToScreenPosition(Vector2 pointerScreenPosition)
    {
        RectTransform parentRect = this.cursorRect.parent as RectTransform;
        Camera eventCamera = this.cursorCanvas == null || this.cursorCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : this.cursorCanvas.worldCamera;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, pointerScreenPosition, eventCamera, out Vector2 localPosition))
            this.cursorRect.anchoredPosition = localPosition;
    }

    /// <summary>현재 손 자세에 할당된 스프라이트를 반환합니다.</summary>
    /// <param name="state">표시할 손 자세입니다.</param>
    /// <returns>해당 자세의 스프라이트입니다.</returns>
    private Sprite getSprite(HandCursorState state)
    {
        return state switch
        {
            HandCursorState.Released => this.releasedSprite,
            HandCursorState.HoldingStill => this.holdingStillSprite,
            HandCursorState.HoldingLeft => this.holdingLeftSprite,
            HandCursorState.HoldingRight => this.holdingRightSprite,
            _ => this.releasedSprite
        };
    }

    /// <summary>손이 보이는 동안 현재 시스템 커서 표시 상태를 보관하고 숨깁니다.</summary>
    private void hideSystemCursor()
    {
        if (!Application.isPlaying || this.ownsCursorVisibility) return;
        this.previousCursorVisible = Cursor.visible;
        this.ownsCursorVisibility = true;
        Cursor.visible = false;
    }

    /// <summary>이 컴포넌트가 변경한 시스템 커서 표시 상태만 복원합니다.</summary>
    private void restoreSystemCursor()
    {
        if (!this.ownsCursorVisibility) return;
        Cursor.visible = this.previousCursorVisible;
        this.ownsCursorVisibility = false;
    }
}
