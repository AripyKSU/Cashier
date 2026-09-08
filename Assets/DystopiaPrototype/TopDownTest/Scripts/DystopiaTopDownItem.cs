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
    /// <summary>판매 구역에서 충분히 감속한 뒤 고정되어 다시 조작할 수 없는 상태입니다.</summary>
    public bool IsSettledForSale { get; private set; }
    private float saleRestSeconds;

    /// <summary>판매 영역 내 저속 상태를 고정 시간으로 확인하며 진입 즉시 멈추지 않습니다.</summary>
    private void FixedUpdate()
    {
        if (Body == null || !Body.simulated || IsSettledForSale) return;
        bool resting = State == TopDownItemState.ForSale && Body.linearVelocity.sqrMagnitude < .0225f && Mathf.Abs(Body.angularVelocity) < 12;
        saleRestSeconds = resting ? saleRestSeconds + Time.fixedDeltaTime : 0;
        if (saleRestSeconds < .3f) return;
        IsSettledForSale = true;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0;
        Body.bodyType = RigidbodyType2D.Static;
    }
    public Rigidbody2D Body { get; private set; }
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

    /// <summary>회전하는 물품의 충돌을 받아 바깥으로 튕기며 회전도 전달받습니다.</summary>
    /// <param name="collision">물품 사이의 물리 접촉입니다.</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        var other = collision.gameObject.GetComponent<DystopiaTopDownItem>();
        if (IsSettledForSale || State == TopDownItemState.Excluded || other == null || other.IsSettledForSale || other.State == TopDownItemState.Excluded) return;
        float spin = other.Body.angularVelocity;
        if (Mathf.Abs(spin) < 90) return;
        Vector2 away = (Body.position - other.Body.position).normalized;
        Body.AddForce(away * Mathf.Min(Mathf.Abs(spin) / 360f, 1.5f), ForceMode2D.Impulse);
        Body.angularVelocity = Mathf.Clamp(Body.angularVelocity - spin * .65f, -720, 720);
        WasStirred |= other.WasStirred;
    }
}


