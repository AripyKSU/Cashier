using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 작업대 왼쪽 끝에 대기하며, 마우스로 클릭하여 잡고 드래그하여 상품들을 물리적으로 밀어내는 큰 밀대(DividerBar) 컨트롤러입니다.
/// 마우스를 놓으면 그 자리에 멈추고, 다시 클릭하여 잡을 수 있습니다.
/// </summary>
public sealed class DividerBarController : MonoBehaviour
{
    // =========================================================================
    // 1. SERIALIZED FIELDS
    // =========================================================================

    [Header("Visual & Transform")]
    [Tooltip("밀대 RectTransform")]
    [SerializeField] private RectTransform barRect;

    [Tooltip("밀대 이미지 (DividerBar.png)")]
    [SerializeField] private Image barImage;

    [Header("Bar Dimensions")]
    [Tooltip("밀대의 유효 길이 (픽셀)")]
    [SerializeField, Min(50f)] private float barLength = 480f;

    [Tooltip("밀대의 충돌 두께 (픽셀)")]
    [SerializeField, Min(10f)] private float barThickness = 48f;

    [Header("Starting Position")]
    [Tooltip("작업대 중심 기준 왼쪽 시작 X 오프셋")]
    [SerializeField] private float startOffsetX = -420f;

    [Header("Movement & Tilt Settings")]
    [Tooltip("마우스 속도에 따른 기울기 민감도")]
    [SerializeField, Min(0f)] private float tiltSensitivity = 0.045f;

    [Tooltip("최대 기울기 각도 (도)")]
    [SerializeField, Range(0f, 60f)] private float maxTiltAngle = 30f;

    [Tooltip("기울기 회전 보간 속도")]
    [SerializeField, Min(1f)] private float rotationSmoothSpeed = 14f;

    [Tooltip("밀대 위치 추종 보간 속도")]
    [SerializeField, Min(1f)] private float positionFollowSpeed = 32f;

    [Header("Push Physics")]
    [Tooltip("상품에 가해지는 밀기 힘 계수")]
    [SerializeField, Min(0.1f)] private float pushForceMultiplier = 1.6f;

    [Tooltip("최대 밀기 속도 (픽셀/초)")]
    [SerializeField, Min(100f)] private float maxPushSpeed = 500f;


    // =========================================================================
    // 2. PROPERTIES & FIELDS
    // =========================================================================

    private RectTransform workArea;
    private Vector2 targetPosition;
    private Vector2 previousPosition;
    private Vector2 currentVelocity;
    private float currentAngle;
    private bool isHolding;
    private bool hasSample;
    private Color originalColor = Color.white;

    /// <summary>현재 밀대의 로컬 위치입니다.</summary>
    public Vector2 Position => this.barRect != null ? this.barRect.anchoredPosition : Vector2.zero;

    /// <summary>플레이어가 현재 밀대를 클릭하여 잡고 있는지 여부입니다.</summary>
    public bool IsHolding => this.isHolding;


    // =========================================================================
    // 3. UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (this.barRect == null)
        {
            this.barRect = this.transform as RectTransform;
        }

        if (this.barImage == null)
        {
            this.barImage = this.GetComponent<Image>();
        }

        if (this.barImage != null)
        {
            this.originalColor = this.barImage.color;
        }

        if (this.barRect != null)
        {
            this.targetPosition = this.barRect.anchoredPosition;
            this.previousPosition = this.targetPosition;
        }
    }


    // =========================================================================
    // 4. PUBLIC API
    // =========================================================================

    /// <summary>작업대 영역과 초기 위치를 설정하고 왼쪽 끝 대기 위치로 이동합니다.</summary>
    /// <param name="workAreaRect">작업대 RectTransform입니다.</param>
    public void Initialize(RectTransform workAreaRect)
    {
        this.workArea = workAreaRect;
        this.ResetToLeftEnd();
    }

    /// <summary>밀대를 작업대 왼쪽 끝 기본 위치로 리셋합니다.</summary>
    public void ResetToLeftEnd()
    {
        this.isHolding = false;
        this.hasSample = false;
        this.currentVelocity = Vector2.zero;
        this.currentAngle = 0f;

        Vector2 startPos = new Vector2(this.startOffsetX, 0f);
        if (this.barRect != null)
        {
            this.barRect.anchoredPosition = startPos;
            this.barRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
            this.targetPosition = startPos;
            this.previousPosition = startPos;
        }

        this.SetVisible(true);
    }

    /// <summary>밀대 표시 여부를 설정합니다.</summary>
    /// <param name="visible">표시 여부입니다.</param>
    public void SetVisible(bool visible)
    {
        if (this.barImage != null)
        {
            this.barImage.enabled = visible;
        }
        else if (this.gameObject != null)
        {
            this.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 마우스 포인터 입력과 활성화 상태를 받아 밀대의 클릭-잡기 및 이동을 갱신합니다.
    /// </summary>
    /// <param name="allowed">현재 밀대 조작이 허용되는지 여부입니다.</param>
    /// <param name="pointerScreenPosition">현재 마우스 스크린 좌표입니다.</param>
    /// <param name="deltaSeconds">프레임 경과 시간입니다.</param>
    public void UpdateMotion(bool allowed, Vector2 pointerScreenPosition, float deltaSeconds)
    {
        if (this.barRect == null || this.workArea == null || deltaSeconds <= 0f)
        {
            return;
        }

        if (!allowed)
        {
            this.isHolding = false;
            this.hasSample = false;
            this.currentVelocity = Vector2.zero;
            this.currentAngle = Mathf.Lerp(this.currentAngle, 0f, deltaSeconds * this.rotationSmoothSpeed);
            this.barRect.localRotation = Quaternion.Euler(0f, 0f, 90f + this.currentAngle);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                this.workArea,
                pointerScreenPosition,
                null,
                out Vector2 localPointer))
        {
            this.isHolding = false;
            return;
        }

        // 마우스 클릭 상태 확인
        bool isPressed = false;
        bool wasPressedThisFrame = false;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            isPressed = mouse.leftButton.isPressed;
            wasPressedThisFrame = mouse.leftButton.wasPressedThisFrame;
        }
#else
        isPressed = Input.GetMouseButton(0);
        wasPressedThisFrame = Input.GetMouseButtonDown(0);
#endif

        // 1. 마우스가 눌려 있고 아직 잡지 않은 상태일 때 밀대 근처인지 검사 (Pickup)
        if (isPressed && !this.isHolding)
        {
            Vector2 barPos = this.barRect.anchoredPosition;
            float dx = Mathf.Abs(localPointer.x - barPos.x);
            float dy = Mathf.Abs(localPointer.y - barPos.y);

            // 밀대 판정 범위 (가로 두께의 2.5배, 세로 길이의 절반 여유)
            if (dx <= this.barThickness * 2.5f && dy <= (this.barLength * 0.5f) + 30f)
            {
                this.isHolding = true;
                this.hasSample = false;
            }
        }

        // 2. 클릭을 뗐으면 잡기 해제
        if (!isPressed)
        {
            this.isHolding = false;
            this.hasSample = false;
        }

        // 3. 밀대를 잡지 않은 대기 상태 (시각적 펄스 힌트)
        if (!this.isHolding)
        {
            this.currentVelocity = Vector2.zero;
            this.currentAngle = Mathf.Lerp(this.currentAngle, 0f, deltaSeconds * this.rotationSmoothSpeed);
            this.barRect.localRotation = Quaternion.Euler(0f, 0f, 90f + this.currentAngle);

            if (this.barImage != null)
            {
                // 잡기 대기 중에는 부드러운 밝기 펄스로 클릭 가능함을 안내
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
                this.barImage.color = new Color(this.originalColor.r * pulse, this.originalColor.g * pulse, this.originalColor.b * pulse, this.originalColor.a);
            }
            return;
        }

        // 4. 밀대를 잡고 드래그 중인 상태
        if (this.barImage != null)
        {
            this.barImage.color = this.originalColor;
        }

        // 작업대 내부 범위로 목표 위치 클램핑
        Rect bounds = this.workArea.rect;
        float halfLength = this.barLength * 0.5f;
        float halfThickness = this.barThickness * 0.5f;

        float clampedX = Mathf.Clamp(localPointer.x, bounds.xMin + halfThickness, bounds.xMax - halfThickness);
        float clampedY = Mathf.Clamp(localPointer.y, bounds.yMin + halfLength, bounds.yMax - halfLength);
        this.targetPosition = new Vector2(clampedX, clampedY);

        // 스무스 위치 추종
        Vector2 currentPos = this.barRect.anchoredPosition;
        Vector2 nextPos = Vector2.Lerp(currentPos, this.targetPosition, Mathf.Clamp01(deltaSeconds * this.positionFollowSpeed));
        this.barRect.anchoredPosition = nextPos;

        // 속도 계산
        if (this.hasSample)
        {
            this.currentVelocity = (nextPos - this.previousPosition) / deltaSeconds;
        }
        else
        {
            this.currentVelocity = Vector2.zero;
            this.hasSample = true;
        }
        this.previousPosition = nextPos;

        // 마우스 좌우 속도에 따른 기울기(Tilt) 적용
        float targetTilt = Mathf.Clamp(-this.currentVelocity.x * this.tiltSensitivity, -this.maxTiltAngle, this.maxTiltAngle);
        this.currentAngle = Mathf.Lerp(this.currentAngle, targetTilt, Mathf.Clamp01(deltaSeconds * this.rotationSmoothSpeed));
        this.barRect.localRotation = Quaternion.Euler(0f, 0f, 90f + this.currentAngle);
    }

    /// <summary>
    /// 작업대 위의 상품 목록에 밀대 충돌 및 밀기 물리 충격량을 적용합니다.
    /// 플레이어가 밀대를 잡고 움직이고 있을 때만 작동합니다.
    /// </summary>
    /// <param name="items">작업대 위의 상품 목록입니다.</param>
    public void PushItems(IReadOnlyList<SaleSortingItemView> items)
    {
        if (!this.isHolding || this.barRect == null || items == null || items.Count == 0)
        {
            return;
        }

        Vector2 barCenter = this.barRect.anchoredPosition;
        float radians = (90f + this.currentAngle) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 halfExtents = dir * (this.barLength * 0.5f);
        Vector2 segA = barCenter - halfExtents;
        Vector2 segB = barCenter + halfExtents;
        float barRadius = this.barThickness * 0.5f;

        for (int i = 0; i < items.Count; i++)
        {
            SaleSortingItemView item = items[i];
            if (item == null || !item.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 itemPos = item.Position;
            Vector2 closest = this.getClosestPointOnSegment(segA, segB, itemPos);
            Vector2 diff = itemPos - closest;
            float distSqr = diff.sqrMagnitude;
            float totalRadius = barRadius + item.Radius;

            if (distSqr < totalRadius * totalRadius)
            {
                float dist = Mathf.Sqrt(distSqr);
                Vector2 normal = dist > 0.0001f ? diff / dist : new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                // 겹침 침투 즉시 해소
                float penetration = totalRadius - dist;
                item.Position += normal * penetration;

                // 속도 가산
                float approach = Vector2.Dot(this.currentVelocity, normal);
                Vector2 pushVel = normal * Mathf.Max(approach * this.pushForceMultiplier, 80f);
                item.Velocity = Vector2.ClampMagnitude(item.Velocity + pushVel, this.maxPushSpeed);
            }
        }
    }


    // =========================================================================
    // 5. HELPER METHODS
    // =========================================================================

    private Vector2 getClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr < 0.0001f)
        {
            return a;
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abSqr);
        return a + ab * t;
    }
}
