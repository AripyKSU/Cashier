using System.Collections.Generic;
using System;

/// <summary>계산대 방문 상태. 판정 후 명시적으로 퇴장하며 재제안을 금지한다.</summary>
public enum CustomerState
{
    Entering,
    AwaitingOffer,
    Accepted,
    Rejected,
    Departed
}

/// <summary>한 방문에서 확정된 손님 조합과 변경 불가능한 구매 목록.</summary>
public sealed class CustomerVisit
{
    /// <summary>생성 시 선택한 수락 대사.</summary>
    private readonly uint acceptTextIdx;
    /// <summary>생성 시 선택한 거절 대사.</summary>
    private readonly uint rejectTextIdx;
    /// <summary>방문의 현재 상태. 이 객체의 API만 변경한다.</summary>
    public CustomerState State { get; private set; } = CustomerState.Entering;
    /// <summary>입장 시 고정한 기준 총액.</summary>
    public long BaseTotal { get; }
    /// <summary>입장 시 고정한 가격 허용 배율. 1000=100%.</summary>
    public int PriceTolerance { get; }
    /// <summary>소수점 이하를 버린 수락 상한. UI에 자동 노출하지 않는다.</summary>
    public long AllowedTotal { get; }
    /// <summary>유효한 제안 총액. 미제안 상태는 null.</summary>
    public long? OfferedTotal { get; private set; }
    /// <summary>제안 후 결과. 퇴장 후에도 유지하며 판정 전은 null이다.</summary>
    public bool? WasAccepted => OfferedTotal.HasValue ? OfferedTotal.Value <= AllowedTotal : (bool?)null;
    /// <summary>입장 대사의 TextData FK.</summary>
    public uint EntryTextIdx { get; }
    /// <summary>현재 결과에 대응하는 대사. 제안 전은 입장 대사다.</summary>
    public uint FeedbackTextIdx => WasAccepted.HasValue ? (WasAccepted.Value ? acceptTextIdx : rejectTextIdx) : EntryTextIdx;
    /// <summary>이번 방문의 외형 ID. 동일 외형의 재등장은 동일 인물을 뜻하지 않는다.</summary>
    public uint AppearanceIdx { get; }
    /// <summary>이번 방문 생성에 사용한 성향 ID.</summary>
    public uint DispositionIdx { get; }
    /// <summary>상품별 한 항목만 존재하는 구매 목록.</summary>
    public IReadOnlyList<CustomerOrderItem> Items { get; }

    /// <summary>생성기가 검증한 결과를 복사하여 외부 변경으로부터 보호한다.</summary>
    /// <param name="appearanceIdx">선정된 외형 ID.</param>
    /// <param name="dispositionIdx">선정된 성향 ID.</param>
    /// <param name="items">중복 없는 검증된 구매 목록.</param>
    /// <param name="priceTolerance">검증된 양수 가격 배율.</param>
    /// <param name="entryTextIdx">입장 대사.</param>
    /// <param name="acceptTextIdx">수락 대사.</param>
    /// <param name="rejectTextIdx">거절 대사.</param>
    /// <exception cref="OverflowException">지원 가능한 총액 범위 초과.</exception>
    internal CustomerVisit(uint appearanceIdx, uint dispositionIdx, List<CustomerOrderItem> items,
        int priceTolerance, uint entryTextIdx, uint acceptTextIdx, uint rejectTextIdx)
    {
        AppearanceIdx = appearanceIdx;
        DispositionIdx = dispositionIdx;
        Items = new List<CustomerOrderItem>(items).AsReadOnly();
        PriceTolerance = priceTolerance;
        EntryTextIdx = entryTextIdx;
        this.acceptTextIdx = acceptTextIdx;
        this.rejectTextIdx = rejectTextIdx;
        long total = 0;
        foreach (var item in Items) total = checked(total + (long)item.UnitPrice * item.Quantity);
        BaseTotal = total;
        // decimal 중간값은 long 곱셈 overflow와 부동소수점 반올림을 피한다.
        AllowedTotal = checked((long)decimal.Floor((decimal)total * priceTolerance / 1000m));
    }

    /// <summary>입장 피드백을 표시한 뒤 한 번만 가격 제안 대기로 전환한다.</summary>
    /// <exception cref="InvalidOperationException">이미 입장이 끝난 방문.</exception>
    public void BeginOffer()
    {
        if (State != CustomerState.Entering) throw new InvalidOperationException("이미 입장한 손님입니다.");
        State = CustomerState.AwaitingOffer;
    }

    /// <summary>전체 구매 목록의 총액을 한 번 판정한다. 입력 오류는 기회를 소모하지 않는다.</summary>
    /// <param name="total">양의 정수 제안 총액.</param>
    /// <returns>수락 여부.</returns>
    /// <exception cref="ArgumentOutOfRangeException">0 또는 음수 입력.</exception>
    /// <exception cref="InvalidOperationException">대기 상태가 아닌 방문.</exception>
    public bool SubmitOffer(long total)
    {
        if (State != CustomerState.AwaitingOffer) throw new InvalidOperationException("가격을 다시 제안할 수 없습니다.");
        if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total), "양의 정수 총액이 필요합니다.");
        OfferedTotal = total;
        State = total <= AllowedTotal ? CustomerState.Accepted : CustomerState.Rejected;
        return WasAccepted.Value;
    }

    /// <summary>결과 확인 후 퇴장한다. 결과·가격 snapshot은 유지한다.</summary>
    /// <exception cref="InvalidOperationException">거래 결과가 없거나 이미 퇴장한 방문.</exception>
    public void Depart()
    {
        if (State != CustomerState.Accepted && State != CustomerState.Rejected)
            throw new InvalidOperationException("거래 판정 후에만 퇴장할 수 있습니다.");
        State = CustomerState.Departed;
    }
}

/// <summary>동일 상품의 구매를 하나로 표현하는 불변 상품 ID·수량 쌍.</summary>
public sealed class CustomerOrderItem
{
    /// <summary>입장 시 고정한 상품 단가. 원본 CSV 변경과 무관하다.</summary>
    public uint UnitPrice { get; }
    /// <summary>상품 데이터 ID.</summary>
    public uint ProductIdx { get; }
    /// <summary>해당 상품의 구매 개수.</summary>
    public int Quantity { get; }

    /// <summary>생성기가 검증한 상품과 수량을 보관한다.</summary>
    /// <param name="productIdx">선택된 상품 ID.</param>
    /// <param name="quantity">검증된 양수 수량.</param>
    /// <param name="unitPrice">검증된 상품 단가.</param>
    internal CustomerOrderItem(uint productIdx, int quantity, uint unitPrice)
    {
        ProductIdx = productIdx;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
