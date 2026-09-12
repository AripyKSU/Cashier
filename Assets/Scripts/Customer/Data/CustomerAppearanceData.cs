using CsvHelper.Configuration.Attributes;

/// <summary>손님 외형의 표시 이름과 Sprite 참조.</summary>
public sealed class CustomerAppearanceData
{
    /// <summary>외형 PK, 5001~5999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>외형 표시 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
    /// <summary>필수 ResourceData.idx FK. 0·빈값은 허용하지 않는다.</summary>
    [Name("image_resource_idx")]
    public uint ImageResourceIdx { get; set; }
}
