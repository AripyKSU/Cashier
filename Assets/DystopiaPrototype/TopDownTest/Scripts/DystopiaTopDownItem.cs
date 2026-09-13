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

    /// <summary>한 물품 인스턴스를 기존 장바구니 단위와 연결합니다.</summary>
    internal void Initialize(int instanceId, int productId, int lineIndex, int unitIndex, Rigidbody2D body)
    {
        InstanceId = instanceId;
        ProductId = productId;
        LineIndex = lineIndex;
        UnitIndex = unitIndex;
        Body = body;
        RestingLinearDamping = body.linearDamping;
        State = TopDownItemState.Working;
    }

    /// <summary>충돌한 물품의 조작 여부만 전달하고 충돌 힘과 회전은 물리 엔진에 맡깁니다.</summary>
    /// <param name="collision">물품 사이의 물리 접촉입니다.</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        var other = collision.gameObject.GetComponent<DystopiaTopDownItem>();
        if (State == TopDownItemState.Excluded || other == null || other.State == TopDownItemState.Excluded) return;
        WasStirred |= other.WasStirred;
    }
}


