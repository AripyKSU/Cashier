/// <summary>
/// 한 거래에서 판매 대상으로 확정된 상품 식별자와 수량을 전달하는 불변 값입니다.
/// </summary>
public readonly struct SaleItem
{
    /// <summary>판매할 상품의 데이터 식별자입니다.</summary>
    public uint ProductId { get; }

    /// <summary>해당 상품의 판매 수량입니다.</summary>
    public int Quantity { get; }

    /// <summary>판매 상품 식별자와 양의 수량을 지정합니다.</summary>
    /// <param name="productId">판매할 상품의 데이터 식별자입니다.</param>
    /// <param name="quantity">해당 상품의 판매 수량입니다.</param>
    public SaleItem(uint productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }
}
