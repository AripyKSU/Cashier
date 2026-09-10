using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>잡기 지점에 따른 구분봉의 회전 관성과 상품 접촉 속도를 제어합니다.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(SpriteRenderer))]
public sealed class DividerBarController2D : MonoBehaviour
{
    /// <summary>잡기 지점의 관성 감도와 회전 감쇠입니다. 원래 각도로 복원하지 않습니다.</summary>
    [SerializeField, Min(0)] private float tiltSensitivity = 1.5f;
    [SerializeField, Min(.1f)] private float rotationDamping = 1f;
    /// <summary>접촉 속도 배율, 최대 추가 충격량, 상품 최대 선속도입니다.</summary>
    [SerializeField, Min(0)] private float pushPower = 1.1f;
    [SerializeField, Min(0)] private float maxPushForce = 3f;
    [SerializeField, Min(.1f)] private float maxItemVelocity = 5f;
    /// <summary>힘을 전달할 기존 상품 레이어를 Inspector에서 지정합니다.</summary>
    [SerializeField] private LayerMask pushableLayers;
    /// <summary>왼쪽과 위아래 경계입니다. 오른쪽은 바가 판매 구역 끝까지 이동하도록 제한하지 않습니다.</summary>
    [SerializeField] private bool useMovementBounds;
    [SerializeField] private Vector2 minWorldPosition = new Vector2(-5.8f, -3f);
    [SerializeField] private Vector2 maxWorldPosition = new Vector2(5.8f, 3f);

    private readonly HashSet<Rigidbody2D> contacts = new HashSet<Rigidbody2D>();
    private Rigidbody2D body;
    private CapsuleCollider2D capsule;
    private SpriteRenderer visual;
    private Vector2 target, barVelocity;
    /// <summary>홀드 중 로컬 잡기 지점과 고정 물리 프레임의 입력 기록입니다.</summary>
    private Vector2 grabLocalPoint, previousTarget, previousTargetVelocity;
    private float angleVelocity, barAngularVelocity;
    private bool hasSample, canControl;
    /// <summary>잡은 뒤 UI를 지나도 버튼을 놓기 전까지 홀드를 유지합니다.</summary>
    internal bool IsHeld => canControl;
    /// <summary>렌더된 막대의 실제 잡기 지점입니다. 손 그림도 같은 지점에 붙입니다.</summary>
    internal Vector3 GripWorldPoint => transform.TransformPoint(grabLocalPoint);
    /// <summary>새 손님마다 왼쪽 바를 잡기 전에는 이전 커서 위치를 무시합니다.</summary>
    private bool awaitingPickup;
    private Vector2 startingPosition;
    /// <summary>대기 안내에만 사용하는 머티리얼 색 보정이며 원본 자산은 변경하지 않습니다.</summary>
    private MaterialPropertyBlock pickupHighlight;
    private Color restingMaterialColor;
    private float pickupStartedAt;

    /// <summary>키네마틱 물리 설정 후 체크아웃의 입력 시작을 기다립니다.</summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        visual = GetComponent<SpriteRenderer>();
        pickupHighlight = new MaterialPropertyBlock();
        restingMaterialColor = visual.sharedMaterial.GetColor("_Color");
        startingPosition = body.position;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.None;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.gravityScale = 0;
        capsule.isTrigger = false;
        transform.rotation = Quaternion.Euler(0, 0, 90);
        body.rotation = 90;
        SetInactive();
    }

    /// <summary>새 손님의 체크아웃마다 편집된 시작 위치에서 바를 준비합니다.</summary>
    internal void ResetToStart()
    {
        SetInactive();
        // 시뮬레이션이 꺼진 동안에도 렌더 Transform을 즉시 초기화합니다.
        transform.SetPositionAndRotation(new Vector3(startingPosition.x, startingPosition.y, transform.position.z), Quaternion.Euler(0, 0, 90));
        body.position = startingPosition;
        body.rotation = 90;
        target = startingPosition;
        awaitingPickup = true;
        pickupStartedAt = Time.unscaledTime;
        visual.enabled = true;
    }

    /// <summary>작업대에서는 항상 표시하되 분류 및 UI 판정을 통과한 입력만 물리에 전달합니다.</summary>
    /// <param name="camera">상품을 표시하는 실제 카메라입니다.</param>
    /// <param name="allowed">분류 중이며 새 잡기를 시작할 때 UI 위에 있지 않으면 true입니다.</param>
    internal void SampleInput(Camera camera, bool allowed)
    {
        if (!isActiveAndEnabled || body == null) return;
        if (!allowed || camera == null || Mouse.current == null || Time.deltaTime <= 0)
        {
            SetInactive();
            // 쏟기·일시정지·계산기 호버는 입력 제한이며 바를 숨기는 조건이 아닙니다.
            visual.enabled = camera != null && camera.isActiveAndEnabled;
            return;
        }
        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
        if (!plane.Raycast(ray, out float distance)) { SetInactive(); return; }
        Vector2 pointer = ray.GetPoint(distance);
        if (!Mouse.current.leftButton.isPressed)
        {
            SetInactive();
            visual.enabled = true;
            return;
        }
        if (!canControl)
        {
            // 대기 중 시뮬레이션이 꺼져 있어도 로컬 캡슐 영역에서 클릭을 판정합니다.
            Vector2 local = transform.InverseTransformPoint(pointer);
            Vector2 relative = local - capsule.offset;
            bool horizontal = capsule.direction == CapsuleDirection2D.Horizontal;
            float radius = (horizontal ? capsule.size.y : capsule.size.x) * .5f;
            float segment = Mathf.Max(0, (horizontal ? capsule.size.x : capsule.size.y) * .5f - radius);
            Vector2 nearest = horizontal ? new Vector2(Mathf.Clamp(relative.x, -segment, segment), 0)
                : new Vector2(0, Mathf.Clamp(relative.y, -segment, segment));
            if (!Mouse.current.leftButton.wasPressedThisFrame || (relative - nearest).sqrMagnitude > radius * radius)
            {
                visual.enabled = true;
                return;
            }
            grabLocalPoint = local;
            awaitingPickup = false;
            hasSample = false;
        }
        target = pointer;
        canControl = true;
        body.simulated = true;
        visual.enabled = true;
    }

    /// <summary>잡기 지점의 가속도와 질량 중심까지의 지렛팔로 회전을 적분합니다.</summary>
    private void FixedUpdate()
    {
        if (!canControl) return;
        contacts.RemoveWhere(item => item == null || !item.simulated || !item.gameObject.activeInHierarchy);
        float dt = Time.fixedDeltaTime;
        Vector2 velocity = hasSample ? (target - previousTarget) / dt : Vector2.zero;
        Vector2 acceleration = hasSample ? (velocity - previousTargetVelocity) / dt : Vector2.zero;
        previousTarget = target;
        previousTargetVelocity = velocity;
        hasSample = true;
        Vector2 scale = transform.lossyScale;
        Vector2 localArm = Vector2.Scale(capsule.offset - grabLocalPoint, scale);
        Vector2 arm = Quaternion.Euler(0, 0, body.rotation) * localArm;
        Vector2 size = Vector2.Scale(capsule.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        float gripOffset = Mathf.Clamp01(localArm.magnitude / Mathf.Max(.01f, Mathf.Max(size.x, size.y) * .5f));
        // 평행축 정리: 중심은 토크가 작고 양 끝은 반대 방향으로 회전합니다.
        float inertia = size.sqrMagnitude / 12f + localArm.sqrMagnitude;
        Vector2 inertialForce = -Vector2.ClampMagnitude(acceleration, 100f) - velocity * 3f;
        float torque = (arm.x * inertialForce.y - arm.y * inertialForce.x) / Mathf.Max(.001f, inertia);
        // 손은 위치만 구속합니다. 각도 복원/한계를 없애 바가 잡은 점을 중심으로 돌아갑니다.
        // 중심 가까이는 손의 회전 저항이 크고, 끝을 잡으면 반대쪽의 관성이 남습니다.
        angleVelocity += torque * Mathf.Rad2Deg * tiltSensitivity * dt;
        angleVelocity *= Mathf.Exp(-rotationDamping * Mathf.Lerp(20f, 1.5f, Mathf.Sqrt(gripOffset)) * dt);
        angleVelocity = Mathf.Clamp(angleVelocity, -540f, 540f);
        float angle = body.rotation + angleVelocity * dt;
        Vector2 grip = Quaternion.Euler(0, 0, angle) * Vector2.Scale(grabLocalPoint, scale);
        Vector2 next = target - grip;
        if (useMovementBounds)
        {
            Vector2 half = Vector2.Scale(capsule.size, new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y))) * .5f;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 extent = new Vector2(Mathf.Abs(Mathf.Cos(radians)) * half.x + Mathf.Abs(Mathf.Sin(radians)) * half.y,
                Mathf.Abs(Mathf.Sin(radians)) * half.x + Mathf.Abs(Mathf.Cos(radians)) * half.y);
            Vector2 low = Vector2.Min(minWorldPosition, maxWorldPosition) + extent;
            Vector2 high = Vector2.Max(minWorldPosition, maxWorldPosition) - extent;
            Vector2 offset = Quaternion.Euler(0, 0, angle) * Vector2.Scale(capsule.offset, transform.lossyScale);
            // 오른쪽 끝에서는 바의 길이/기울기 때문에 손보다 먼저 멈추지 않습니다.
            next.x = Mathf.Max(next.x + offset.x, low.x) - offset.x;
            next.y = low.y <= high.y ? Mathf.Clamp(next.y + offset.y, low.y, high.y) - offset.y : (low.y + high.y) * .5f - offset.y;
        }
        barVelocity = (next - body.position) / dt;
        barAngularVelocity = Mathf.DeltaAngle(body.rotation, angle) * Mathf.Deg2Rad / dt;
        body.MovePosition(next);
        body.MoveRotation(angle);
    }

    /// <summary>접촉점 회전 속도를 포함하여 바깥 방향으로 부족한 속도만 보충합니다.</summary>
    /// <param name="collision">상품과의 접촉입니다.</param>
    private void Push(Collision2D collision)
    {
        Rigidbody2D itemBody = collision.rigidbody;
        if (!canControl || itemBody == null || itemBody.bodyType != RigidbodyType2D.Dynamic ||
            (pushableLayers.value & (1 << itemBody.gameObject.layer)) == 0 || collision.contactCount == 0) return;
        contacts.Add(itemBody);
        ContactPoint2D contact = collision.GetContact(0);
        Vector2 normal = contact.normal;
        if (Vector2.Dot(normal, itemBody.worldCenterOfMass - body.worldCenterOfMass) < 0) normal = -normal;
        Vector2 arm = contact.point - body.worldCenterOfMass;
        Vector2 pointVelocity = barVelocity + new Vector2(-arm.y, arm.x) * barAngularVelocity;
        float approach = Mathf.Max(0, Vector2.Dot(pointVelocity, normal));
        float deficit = Mathf.Max(0, approach * pushPower - Vector2.Dot(itemBody.linearVelocity, normal));
        itemBody.AddForceAtPosition(normal * Mathf.Min(maxPushForce, deficit * itemBody.mass), contact.point, ForceMode2D.Impulse);
        itemBody.linearVelocity = Vector2.ClampMagnitude(itemBody.linearVelocity, maxItemVelocity);
        var item = itemBody.GetComponent<DystopiaTopDownItem>();
        if (item != null && approach > .001f) item.WasStirred = true;
    }

    /// <summary>잡기 전 밝기 안내를 표시하고 접촉 상품의 선속도 상한을 유지합니다.</summary>
    private void LateUpdate()
    {
        if (visual != null && pickupHighlight != null)
        {
            float pulse = awaitingPickup && visual.enabled ? .5f + .5f * Mathf.Cos((Time.unscaledTime - pickupStartedAt) * Mathf.PI * 2 / 1.2f) : 0;
            Color tint = restingMaterialColor * (1 + pulse * 2);
            tint.a = restingMaterialColor.a;
            visual.GetPropertyBlock(pickupHighlight);
            pickupHighlight.SetColor("_Color", tint);
            visual.SetPropertyBlock(pickupHighlight);
        }
        if (!canControl) return;
        foreach (var item in contacts)
            if (item != null && item.bodyType == RigidbodyType2D.Dynamic) item.linearVelocity = Vector2.ClampMagnitude(item.linearVelocity, maxItemVelocity);
    }
    /// <summary>첫 접촉에 속도 기반 밀기를 적용합니다.</summary>
    private void OnCollisionEnter2D(Collision2D collision) { Push(collision); }
    /// <summary>접촉 중에는 바깥 방향 속도 부족분만 적용합니다.</summary>
    private void OnCollisionStay2D(Collision2D collision) { Push(collision); }
    /// <summary>떨어진 상품을 저항 계산에서 제외합니다.</summary>
    private void OnCollisionExit2D(Collision2D collision) { if (collision.rigidbody != null) contacts.Remove(collision.rigidbody); }
    /// <summary>화면 전환과 UI 조작 중 물리를 끄고 재개 시 속도 급증을 방지합니다.</summary>
    private void SetInactive()
    {
        canControl = hasSample = false;
        barVelocity = previousTargetVelocity = Vector2.zero;
        angleVelocity = barAngularVelocity = 0;
        contacts.Clear();
        if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; body.simulated = false; }
        if (visual != null) visual.enabled = false;
    }
    /// <summary>비활성화 시 물리 접촉과 입력 기록을 해제합니다.</summary>
    private void OnDisable()
    {
        SetInactive();
        if (visual != null && pickupHighlight != null)
        {
            visual.GetPropertyBlock(pickupHighlight);
            pickupHighlight.SetColor("_Color", restingMaterialColor);
            visual.SetPropertyBlock(pickupHighlight);
        }
    }
}
