using System.Collections.Generic;

/// <summary>한 방문에서 확정된 손님 조합과 변경 불가능한 구매 목록.</summary>
public sealed class CustomerVisit
{
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
    internal CustomerVisit(uint appearanceIdx, uint dispositionIdx, List<CustomerOrderItem> items)
    {
        AppearanceIdx = appearanceIdx;
        DispositionIdx = dispositionIdx;
        Items = new List<CustomerOrderItem>(items).AsReadOnly();
    }
}

/// <summary>동일 상품의 구매를 하나로 표현하는 불변 상품 ID·수량 쌍.</summary>
public sealed class CustomerOrderItem
{
    /// <summary>상품 데이터 ID.</summary>
    public uint ProductIdx { get; }
    /// <summary>해당 상품의 구매 개수.</summary>
    public int Quantity { get; }

    /// <summary>생성기가 검증한 상품과 수량을 보관한다.</summary>
    /// <param name="productIdx">선택된 상품 ID.</param>
    /// <param name="quantity">검증된 양수 수량.</param>
    internal CustomerOrderItem(uint productIdx, int quantity)
    {
        ProductIdx = productIdx;
        Quantity = quantity;
    }
}
