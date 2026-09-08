using System;

/// <summary>모든 필요 속성을 가진 손님에게 특정 분류 판매를 금지하는 조건값. 지침 ID·효력 수명은 소유하지 않는다.</summary>
public readonly struct SaleRestriction
{
    /// <summary>모두 일치해야 하는 손님 속성. None·배타 조합은 허용하지 않는다.</summary>
    public CustomerAttributes RequiredAttributes { get; }
    /// <summary>판매 제한 대상인 기존 상품 분류.</summary>
    public ProductType ProductType { get; }

    /// <summary>유효한 조건값을 복사한다.</summary>
    /// <param name="requiredAttributes">AND로 검사할 속성.</param>
    /// <param name="productType">None이 아닌 정의된 상품 분류.</param>
    /// <exception cref="ArgumentException">속성 또는 상품 분류가 유효하지 않음.</exception>
    public SaleRestriction(CustomerAttributes requiredAttributes, ProductType productType)
    {
        RequiredAttributes = requiredAttributes;
        ProductType = productType;
        Validate();
    }

    /// <summary>외부 입력 경계에서 default struct를 포함해 조건 유효성을 검사한다.</summary>
    /// <exception cref="ArgumentException">None·미정의·배타 속성 또는 상품 분류 오류.</exception>
    public void Validate()
    {
        CustomerProfileValidation.ValidateAttributes(RequiredAttributes);
        if (RequiredAttributes == CustomerAttributes.None || ProductType == ProductType.None ||
            !Enum.IsDefined(typeof(ProductType), ProductType))
            throw new ArgumentException("판매 제한에는 유효한 속성과 상품 분류가 필요합니다.");
    }
}

/// <summary>성립한 판매의 규칙-상품별 위반 기록. 이후 규칙·상품 데이터 수정과 무관한 값 복사다.</summary>
public readonly struct SaleRestrictionViolation
{
    /// <summary>위반한 조건값. 정식 지침 ID는 아직 연결하지 않는다.</summary>
    public SaleRestriction Restriction { get; }
    /// <summary>실제 판매한 상품 PK.</summary>
    public uint ProductId { get; }
    /// <summary>최종 판매 목록에서 중복을 합산한 수량.</summary>
    public int Quantity { get; }

    /// <summary>검증한 조건·상품·수량을 보존한다.</summary>
    /// <param name="restriction">일치한 판매 제한 조건.</param>
    /// <param name="productId">0이 아닌 판매 상품 PK.</param>
    /// <param name="quantity">양수 합산 수량.</param>
    /// <exception cref="ArgumentException">조건·상품·수량 오류.</exception>
    public SaleRestrictionViolation(SaleRestriction restriction, uint productId, int quantity)
    {
        restriction.Validate();
        if (productId == 0 || quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Restriction = restriction;
        ProductId = productId;
        Quantity = quantity;
    }
}
