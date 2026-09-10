using CsvHelper.Configuration.Attributes;

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
