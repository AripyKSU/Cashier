using System;
using CsvHelper.Configuration.Attributes;

/// <summary>상품의 분류·정가·등장일과 표시 리소스. 재고 수량은 별도 계약이다.</summary>
public sealed class ProductData
{
    /// <summary>상품 PK, 1001~1999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>구매 목록에 출력할 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
    /// <summary>구매 선호 판정에 사용하는 고정 enum 분류.</summary>
    [Name("product_type"), TypeConverter(typeof(ProductTypeConverter))]
    public ProductType ProductType { get; set; }
    /// <summary>상품 하나의 정가. 양수 정수 통화 단위.</summary>
    [Name("base_price")]
    public uint BasePrice { get; set; }
    /// <summary>상품 단위 원가. 양수이며 기본가격보다 작아야 한다는 일반 제한은 없다.</summary>
    [Name("cost_price")]
    public uint CostPrice { get; set; }
    /// <summary>등장하는 게임 경과 일수. 0은 게임 시작일이다.</summary>
    [Name("available_day")]
    public uint AvailableDay { get; set; }
    /// <summary>ResourceData FK. 빈 셀만 null이며 흰 정사각형을 표시한다. 0은 잘못된 참조다.</summary>
    [Name("image_resource_idx")]
    public uint? ImageResourceIdx { get; set; }
    /// <summary>현재 판매 가능한 상품인지 여부. CSV 값은 0 또는 1.</summary>
    [Name("is_available"), TypeConverter(typeof(ZeroOneBooleanConverter))]
    public bool IsAvailable { get; set; }

    /// <summary>CSV와 생성기에서 공유하는 상품 유효성을 검사한다.</summary>
    /// <exception cref="ArgumentException">필수 가격·분류·리소스 대역 오류.</exception>
    public void Validate()
    {
        if (Idx == 0 || BasePrice == 0 || CostPrice == 0 || ProductType == ProductType.None || !Enum.IsDefined(typeof(ProductType), ProductType))
            throw new ArgumentException($"ProductData.csv PK={Idx}: base_price, cost_price 또는 product_type 오류");
        if (ImageResourceIdx.HasValue && (ImageResourceIdx.Value % 1000 == 0 ||
            Util.GetDataTableType(ImageResourceIdx.Value) != DataTableType.Resource))
            throw new ArgumentException($"ProductData.csv PK={Idx}, image_resource_idx={ImageResourceIdx}: Resource FK 대역 오류");
    }
}
