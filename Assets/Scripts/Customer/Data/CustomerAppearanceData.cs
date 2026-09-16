using CsvHelper.Configuration.Attributes;

using System;

/// <summary>손님 외형의 표시 이름, Sprite 참조와 고정 성별·연령.</summary>
public sealed class CustomerAppearanceData
{
    /// <summary>외형 PK, 5001~5999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>외형 표시 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
    /// <summary>선택적 ResourceData.idx FK. 빈값은 사각형 표시이며 0은 허용하지 않는다.</summary>
    [Name("image_resource_idx")]
    public uint? ImageResourceIdx { get; set; }
    /// <summary>외형의 고정 성별. Male 또는 Female만 허용한다.</summary>
    [Name("gender")]
    public CustomerAttributes Gender { get; set; }
    /// <summary>외형의 고정 연령. Child, Elderly 또는 Adult만 허용한다.</summary>
    [Name("age")]
    public CustomerAttributes Age { get; set; }

    /// <summary>외형의 두 분류 축이 각각 허용된 단일 값인지 검사한다.</summary>
    /// <exception cref="ArgumentException">성별 또는 연령 값이 누락·중복·미정의인 경우.</exception>
    public void ValidateClassification()
    {
        if (this.Gender != CustomerAttributes.Male && this.Gender != CustomerAttributes.Female)
            throw new ArgumentException($"gender={this.Gender}: Male 또는 Female이 필요합니다.", nameof(this.Gender));
        if (this.Age != CustomerAttributes.Child && this.Age != CustomerAttributes.Elderly && this.Age != CustomerAttributes.Adult)
            throw new ArgumentException($"age={this.Age}: Child, Elderly 또는 Adult가 필요합니다.", nameof(this.Age));
    }
}
