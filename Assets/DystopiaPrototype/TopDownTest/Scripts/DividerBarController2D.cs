using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>상품에 막히지 않는 키네마틱 구분봉을 마우스 관성과 접촉 속도로 제어합니다.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(SpriteRenderer))]
public sealed class DividerBarController2D : MonoBehaviour
{
    /// <summary>입력 속도에 따른 기울기 제한, 감도와 회전 응답입니다.</summary>
    [SerializeField, Range(0, 45)] private float maxTiltAngle = 18f;
    [SerializeField, Min(0)] private float tiltSensitivity = 1.5f;
    [SerializeField, Min(.1f)] private float rotationSmoothSpeed = 12f;
    [SerializeField, Min(.1f)] private float rotationDamping = 1f;
    /// <summary>접촉 속도 배율, 최대 추가 충격량, 상품 최대 선속도입니다.</summary>
    [SerializeField, Min(0)] private float pushPower = 1.1f;
    [SerializeField, Min(0)] private float maxPushForce = 3f;
    [SerializeField, Min(.1f)] private float maxItemVelocity = 5f;
    /// <summary>힘을 전달할 기존 상품 레이어를 Inspector에서 지정합니다.</summary>
    [SerializeField] private LayerMask pushableLayers;
    /// <summary>회전한 콜라이더 전체가 들어가야 하는 선택적 월드 사각 영역입니다.</summary>
    [SerializeField] private bool useMovementBounds;
    [SerializeField] private Vector2 minWorldPosition = new Vector2(-5.8f, -3f);
    [SerializeField] private Vector2 maxWorldPosition = new Vector2(5.8f, 3f);

    private readonly HashSet<Rigidbody2D> contacts = new HashSet<Rigidbody2D>();
    private Rigidbody2D body;
    private CapsuleCollider2D capsule;
    private SpriteRenderer visual;
    private Vector2 target, previousMouse, mouseVelocity, barVelocity;
    private float angleVelocity, barAngularVelocity;
    private bool hasSample, canControl;
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
    /// <param name="allowed">분류 중이며 UI 위에 있지 않을 때만 true입니다.</param>
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
        if (awaitingPickup)
        {
            // 커서를 강제로 이동시키지 않고 왼쪽 시작점에서 명시적으로 넘겨받습니다.
            // 물리는 계속 꺼져 있어 대기 중에는 옆의 상품도 밀지 않습니다.
            if ((pointer - startingPosition).sqrMagnitude > .45f * .45f)
            {
                visual.enabled = true;
                return;
            }
            awaitingPickup = false;
            hasSample = false;
        }
        target = pointer;
        mouseVelocity = hasSample ? (target - previousMouse) / Time.deltaTime : Vector2.zero;
        previousMouse = target;
        hasSample = true;
        canControl = true;
        body.simulated = true;
        visual.enabled = true;
    }

    /// <summary>추종 지연 없이 마우스 목표로 이동하며 충돌은 고정 물리 루프에서 처리합니다.</summary>
    private void FixedUpdate()
    {
        if (!canControl) return;
        contacts.RemoveWhere(item => item == null || !item.simulated || !item.gameObject.activeInHierarchy);
        float dt = Time.fixedDeltaTime;
        float tilt = 90 + Mathf.Clamp(-mouseVelocity.x * tiltSensitivity, -maxTiltAngle, maxTiltAngle);
        float angle = Mathf.SmoothDampAngle(body.rotation, tilt, ref angleVelocity,
            rotationDamping / (rotationSmoothSpeed * 3), 720f, dt);
        angle = 90 + Mathf.Clamp(Mathf.DeltaAngle(90, angle), -maxTiltAngle, maxTiltAngle);
        Vector2 next = target;
        if (useMovementBounds)
        {
            Vector2 half = Vector2.Scale(capsule.size, new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y))) * .5f;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 extent = new Vector2(Mathf.Abs(Mathf.Cos(radians)) * half.x + Mathf.Abs(Mathf.Sin(radians)) * half.y,
                Mathf.Abs(Mathf.Sin(radians)) * half.x + Mathf.Abs(Mathf.Cos(radians)) * half.y);
            Vector2 low = Vector2.Min(minWorldPosition, maxWorldPosition) + extent;
            Vector2 high = Vector2.Max(minWorldPosition, maxWorldPosition) - extent;
            Vector2 offset = Quaternion.Euler(0, 0, angle) * Vector2.Scale(capsule.offset, transform.lossyScale);
            next.x = low.x <= high.x ? Mathf.Clamp(next.x + offset.x, low.x, high.x) - offset.x : (low.x + high.x) * .5f - offset.x;
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
        mouseVelocity = barVelocity = Vector2.zero;
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
