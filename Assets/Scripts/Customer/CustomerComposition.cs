using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 선택기가 확정한 한 명의 손님 구성 snapshot입니다.
/// </summary>
/// <remarks>
/// 원본 성향 DTO를 보관하지 않고 생성에 필요한 값만 복사하여,
/// 이후 데이터 테이블이나 선택기 입력이 변경되어도 구성 결과가 바뀌지 않도록 합니다.
/// </remarks>
public sealed class CustomerComposition
{
    /// <summary>선택된 외형 PK입니다.</summary>
    public uint AppearanceIdx { get; }

    /// <summary>선택된 성향 PK입니다.</summary>
    public uint DispositionIdx { get; }

    /// <summary>선택된 성향의 타입 snapshot입니다.</summary>
    public CustomerDispositionType DispositionType { get; }

    /// <summary>성별·연령·특수 축이 완성된 손님 속성입니다.</summary>
    public CustomerAttributes Attributes { get; }

    /// <summary>생성 시 확정한 구매 목록 snapshot입니다.</summary>
    public IReadOnlyList<CustomerOrderItem> Items { get; }

    /// <summary>입장 대사의 TextData FK입니다.</summary>
    public uint EntryTextIdx { get; }

    /// <summary>정상 판매 대사의 TextData FK입니다.</summary>
    public uint RegularSaleTextIdx { get; }

    /// <summary>할인 판매 대사의 TextData FK입니다.</summary>
    public uint DiscountSaleTextIdx { get; }

    /// <summary>착취 판매 대사의 TextData FK입니다.</summary>
    public uint ExploitativeSaleTextIdx { get; }

    /// <summary>거절 대사의 TextData FK입니다.</summary>
    public uint RejectTextIdx { get; }

    /// <summary>생성 시 복사한 가격 허용 배율입니다. 1000=100%입니다.</summary>
    public int PriceTolerance { get; }

    /// <summary>정가 인정 하한 배율입니다. 1000=100%입니다.</summary>
    public int RegularPriceMinRate { get; }

    /// <summary>정가 인정 상한 배율입니다. 1000=100%입니다.</summary>
    public int RegularPriceMaxRate { get; }

    /// <summary>생성 시 활성·날짜·설비 조건을 통과한 전체 상품 PK snapshot입니다.</summary>
    public IReadOnlyCollection<uint> AvailableProductIds { get; }

    /// <summary>
    /// 검증된 선택 결과를 복사하여 불변 구성 snapshot을 만듭니다.
    /// </summary>
    /// <param name="appearanceIdx">선택된 외형 PK입니다.</param>
    /// <param name="dispositionIdx">선택된 성향 PK입니다.</param>
    /// <param name="dispositionType">선택된 성향 타입입니다.</param>
    /// <param name="attributes">완성된 손님 속성입니다.</param>
    /// <param name="items">중복 없는 구매 목록입니다.</param>
    /// <param name="entryTextIdx">입장 대사 FK입니다.</param>
    /// <param name="regularSaleTextIdx">정상 판매 대사 FK입니다.</param>
    /// <param name="discountSaleTextIdx">할인 판매 대사 FK입니다.</param>
    /// <param name="exploitativeSaleTextIdx">착취 판매 대사 FK입니다.</param>
    /// <param name="rejectTextIdx">거절 대사 FK입니다.</param>
    /// <param name="priceTolerance">가격 허용 배율입니다.</param>
    /// <param name="regularPriceMinRate">정가 인정 하한 배율입니다.</param>
    /// <param name="regularPriceMaxRate">정가 인정 상한 배율입니다.</param>
    /// <param name="availableProductIds">생성 시 활성화된 전체 상품 PK입니다.</param>
    /// <exception cref="ArgumentNullException">목록 입력이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">구성 값, 상품 목록 또는 대사 FK가 잘못된 경우 발생합니다.</exception>
    public CustomerComposition(
        uint appearanceIdx,
        uint dispositionIdx,
        CustomerDispositionType dispositionType,
        CustomerAttributes attributes,
        IEnumerable<CustomerOrderItem> items,
        uint entryTextIdx,
        uint regularSaleTextIdx,
        uint discountSaleTextIdx,
        uint exploitativeSaleTextIdx,
        uint rejectTextIdx,
        int priceTolerance,
        int regularPriceMinRate,
        int regularPriceMaxRate,
        IEnumerable<uint> availableProductIds)
    {
        if (appearanceIdx == 0)
            throw new ArgumentException("외형 PK가 필요합니다.", nameof(appearanceIdx));
        if (dispositionIdx == 0)
            throw new ArgumentException("성향 PK가 필요합니다.", nameof(dispositionIdx));
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (availableProductIds == null)
            throw new ArgumentNullException(nameof(availableProductIds));
        if (entryTextIdx == 0 || regularSaleTextIdx == 0 || discountSaleTextIdx == 0 ||
            exploitativeSaleTextIdx == 0 || rejectTextIdx == 0)
            throw new ArgumentException("모든 대사 FK가 필요합니다.");
        if (priceTolerance <= 0 || regularPriceMinRate <= 0 || regularPriceMinRate > 1000 || regularPriceMaxRate < 1000)
            throw new ArgumentException("가격 규칙 범위가 잘못되었습니다.");

        try
        {
            CustomerProfileValidation.ValidateType(dispositionType);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentException("유효한 손님 성향 타입이 필요합니다.", nameof(dispositionType), exception);
        }
        CustomerProfileValidation.ValidateCompleteAttributes(attributes);

        var availableIds = new HashSet<uint>();
        var copiedAvailableIds = new List<uint>();
        foreach (uint productId in availableProductIds)
        {
            if (productId == 0 || !availableIds.Add(productId))
                throw new ArgumentException("허용 상품 PK는 0이 아니며 고유해야 합니다.", nameof(availableProductIds));
            copiedAvailableIds.Add(productId);
        }

        var copiedItems = new List<CustomerOrderItem>();
        var itemIds = new HashSet<uint>();
        foreach (CustomerOrderItem item in items)
        {
            if (item == null || item.ProductIdx == 0 || item.Quantity <= 0 || item.UnitPrice == 0 ||
                !itemIds.Add(item.ProductIdx) || !availableIds.Contains(item.ProductIdx))
                throw new ArgumentException("구매 목록은 허용된 상품의 중복 없는 양수 항목이어야 합니다.", nameof(items));
            copiedItems.Add(item);
        }

        if (copiedItems.Count == 0)
            throw new ArgumentException("구매 목록이 필요합니다.", nameof(items));
        if (availableIds.Count == 0)
            throw new ArgumentException("허용 상품 목록이 필요합니다.", nameof(availableProductIds));

        AppearanceIdx = appearanceIdx;
        DispositionIdx = dispositionIdx;
        DispositionType = dispositionType;
        Attributes = attributes;
        Items = new ReadOnlyCollection<CustomerOrderItem>(copiedItems);
        EntryTextIdx = entryTextIdx;
        RegularSaleTextIdx = regularSaleTextIdx;
        DiscountSaleTextIdx = discountSaleTextIdx;
        ExploitativeSaleTextIdx = exploitativeSaleTextIdx;
        RejectTextIdx = rejectTextIdx;
        PriceTolerance = priceTolerance;
        RegularPriceMinRate = regularPriceMinRate;
        RegularPriceMaxRate = regularPriceMaxRate;
        AvailableProductIds = new ReadOnlyCollection<uint>(copiedAvailableIds);
    }
}
