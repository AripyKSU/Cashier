using System;

/// <summary>사용자가 제출하는 최종 상품·수량 입력. 가격은 제출 시점에 조회한다.</summary>
public readonly struct SaleItem
{
    /// <summary>상품 PK.</summary>
    public uint ProductId { get; }
    /// <summary>판매 수량.</summary>
    public int Quantity { get; }
    /// <summary>입력값을 보관한다. 기본 struct 값까지 SubmitOffer 경계에서 검증한다.</summary>
    /// <param name="productId">상품 PK.</param>
    /// <param name="quantity">수량.</param>
    public SaleItem(uint productId, int quantity) { ProductId = productId; Quantity = quantity; }
}

/// <summary>제출 시 검증·복사한 판매 단가와 원가. 입력 SaleItem과 구분한다.</summary>
public readonly struct SoldItem
{
    /// <summary>상품 PK.</summary>
    public uint ProductId { get; }
    /// <summary>중복 상품 수량을 합산한 양수 수량.</summary>
    public int Quantity { get; }
    /// <summary>제출 당시 현재 단가.</summary>
    public uint UnitPrice { get; }
    /// <summary>제출 당시 상품 단위 원가.</summary>
    public uint UnitCostPrice { get; }
    /// <summary>검증된 값만 불변 판매 내역으로 보관한다.</summary>
    /// <param name="productId">상품 PK.</param>
    /// <param name="quantity">양수 수량.</param>
    /// <param name="unitPrice">양수 단가.</param>
    /// <param name="unitCostPrice">양수 원가.</param>
    /// <exception cref="ArgumentOutOfRangeException">0 또는 음수 값.</exception>
    public SoldItem(uint productId, int quantity, uint unitPrice, uint unitCostPrice)
    {
        if (productId == 0 || quantity <= 0 || unitPrice == 0 || unitCostPrice == 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        ProductId = productId; Quantity = quantity; UnitPrice = unitPrice; UnitCostPrice = unitCostPrice;
    }
}
