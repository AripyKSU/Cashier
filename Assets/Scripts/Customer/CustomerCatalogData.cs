using System;
using System.Linq;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;

/// <summary>상품 분류 코드. PK가 아니며 기존 분류의 의미와 숫자를 고정한다.</summary>
public enum ProductType : uint
{
    None = 0,
    Water = 1,
    Food = 2,
    Medicine = 3,
    DailyNecessities = 4
}

/// <summary>CSV에서 문자열 enum 이름을 허용하지 않는 숫자 분류 변환기.</summary>
public sealed class ProductTypeConverter : DefaultTypeConverter
{
    /// <summary>정의된 숫자 상품 분류만 변환한다.</summary>
    /// <param name="text">숫자 코드.</param>
    /// <param name="row">CSV 행.</param>
    /// <param name="memberMapData">컬럼 매핑.</param>
    /// <returns>유효한 상품 분류.</returns>
    /// <exception cref="FormatException">숫자가 아니거나 정의되지 않은 코드.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) ||
            value == 0 || !Enum.IsDefined(typeof(ProductType), value))
            throw new FormatException($"product_type={text}: 정의된 숫자 분류 필요");
        return (ProductType)value;
    }
}

/// <summary>기존 밑줄 구분 숫자 배열을 상품 분류 배열로 변환한다.</summary>
public sealed class ProductTypeArrayConverter : UIntArrayConverter
{
    /// <summary>각 원소의 enum 유효성을 확인한다. 빈 목록은 선호 없음이다.</summary>
    /// <param name="text">밑줄로 구분한 숫자 코드.</param>
    /// <param name="row">CSV 행.</param>
    /// <param name="memberMapData">컬럼 매핑.</param>
    /// <returns>상품 분류 배열.</returns>
    /// <exception cref="FormatException">정의되지 않은 상품 분류.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var values = (uint[])base.ConvertFromString(text, row, memberMapData);
        if (values.Any(x => x == 0 || !Enum.IsDefined(typeof(ProductType), x)))
            throw new FormatException($"preferred_product_types={text}: 잘못된 분류");
        return values.Select(x => (ProductType)x).ToArray();
    }
}

/// <summary>사각형 테스트 외형의 색상을 정의한다. 정식 아트 리소스 연결은 별도 계약이다.</summary>
public sealed class CustomerAppearanceData
{
    /// <summary>외형 PK, 5001~5999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>외형 표시 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
    /// <summary>빨강 채널, 0~255.</summary>
    [Name("color_r")]
    public byte ColorR { get; set; }
    /// <summary>초록 채널, 0~255.</summary>
    [Name("color_g")]
    public byte ColorG { get; set; }
    /// <summary>파랑 채널, 0~255.</summary>
    [Name("color_b")]
    public byte ColorB { get; set; }
    /// <summary>불투명도, 1~255. 보이지 않는 테스트 외형은 거부한다.</summary>
    [Name("color_a")]
    public byte ColorA { get; set; }
}

/// <summary>상품 enum 분류별 UI 표시 이름. 분류당 한 행만 허용한다.</summary>
public sealed class ProductCategoryData
{
    /// <summary>표시할 분류 코드. None은 허용하지 않는다.</summary>
    [Name("product_type"), TypeConverter(typeof(ProductTypeConverter))]
    public ProductType ProductType { get; set; }
    /// <summary>상품군 PK, 7001~7999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>상품군 표시 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
}

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
        if (Idx == 0 || BasePrice == 0 || ProductType == ProductType.None || !Enum.IsDefined(typeof(ProductType), ProductType))
            throw new ArgumentException($"ProductData.csv PK={Idx}: base_price 또는 product_type 오류");
        if (ImageResourceIdx.HasValue && (ImageResourceIdx.Value % 1000 == 0 ||
            Util.GetDataTableType(ImageResourceIdx.Value) != DataTableType.Resource))
            throw new ArgumentException($"ProductData.csv PK={Idx}, image_resource_idx={ImageResourceIdx}: Resource FK 대역 오류");
    }
}
