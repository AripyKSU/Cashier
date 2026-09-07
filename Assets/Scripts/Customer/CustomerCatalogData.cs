using CsvHelper.Configuration.Attributes;

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

/// <summary>상품과 선호 성향이 함께 참조하는 상품군.</summary>
public sealed class ProductCategoryData
{
    /// <summary>상품군 PK, 7001~7999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>상품군 표시 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
}

/// <summary>구매 목록 테스트용 상품. 가격·재고 수량은 이번 계약에 포함하지 않는다.</summary>
public sealed class ProductData
{
    /// <summary>상품 PK, 1001~1999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>구매 목록에 출력할 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
    /// <summary>ProductCategoryData.Idx를 참조하는 FK.</summary>
    [Name("category_idx")]
    public uint CategoryIdx { get; set; }
    /// <summary>현재 판매 가능한 상품인지 여부. CSV 값은 0 또는 1.</summary>
    [Name("is_available"), TypeConverter(typeof(ZeroOneBooleanConverter))]
    public bool IsAvailable { get; set; }
}
