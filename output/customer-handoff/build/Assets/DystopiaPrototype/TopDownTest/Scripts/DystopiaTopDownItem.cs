using UnityEngine;
/// <summary>실제 장바구니 한 단위와 Rigidbody2D를 연결하는 개별 물품입니다.</summary>
public sealed class DystopiaTopDownItem : MonoBehaviour
{
    public int InstanceId { get; private set; }
    public int ProductId { get; private set; }
    public int LineIndex { get; private set; }
    public int UnitIndex { get; private set; }
    public TopDownItemState State { get; set; }
    public bool WasStirred { get; set; }
    public Rigidbody2D Body { get; private set; }
    /// <summary>흡입 연출이 물리와 표시를 소유하는 동안 분류·잡기·경계 보정을 중지합니다.</summary>
    public bool IsBeingVacuumed { get; internal set; }
    /// <summary>쏟기 연출 뒤 복원할 Prefab의 평상시 선형 감쇠값입니다.</summary>
    public float RestingLinearDamping { get; private set; }

    /// <summary>흡입기에서 다시 활성화될 때 물품 간 충돌 제외를 복원합니다.</summary>
    private void OnEnable() => IgnoreItemCollisions();

    /// <summary>벽·도구 충돌은 유지하고 같은 계산대 물품끼리의 충돌 반응만 제외합니다.</summary>
    internal void IgnoreItemCollisions()
    {
        var checkout = GetComponentInParent<DystopiaTopDownTest>();
        if (checkout == null) return;
        var ownCollider = GetComponent<Collider2D>();
        if (ownCollider == null || !ownCollider.enabled || !gameObject.activeInHierarchy) return;
        foreach (var other in checkout.Items)
        {
            if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;
            var otherCollider = other.GetComponent<Collider2D>();
            if (otherCollider != null && otherCollider.enabled) Physics2D.IgnoreCollision(ownCollider, otherCollider);
        }
    }

    /// <summary>한 물품 인스턴스를 기존 장바구니 단위와 연결합니다.</summary>
    /// <param name="instanceId">손님 물품의 고유 번호입니다.</param>
    /// <param name="productId">현재 상품 목록의 인덱스입니다.</param>
    /// <param name="lineIndex">장바구니 품목 행입니다.</param>
    /// <param name="unitIndex">행 안의 물품 번호입니다.</param>
    /// <param name="body">이동에 사용하는 기존 물리 본체입니다.</param>
    internal void Initialize(int instanceId, int productId, int lineIndex, int unitIndex, Rigidbody2D body)
    {
        InstanceId = instanceId;
        ProductId = productId;
        LineIndex = lineIndex;
        UnitIndex = unitIndex;
        Body = body;
        // 도구로 이동은 가능하지만 접촉이나 방출 속도로 회전하지 않게 합니다.
        body.freezeRotation = true;
        body.angularVelocity = 0;
        RestingLinearDamping = body.linearDamping;
        State = TopDownItemState.Working;
        IgnoreItemCollisions();
    }

}


