using CsvHelper.Configuration.Attributes;

using System;

/// <summary>손님 외형의 표시 이름, 컬러·노멀 리소스와 고정 성별·연령·성향.</summary>
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
    /// <summary>필수 ResourceData.idx FK. 컬러 외형과 짝을 이루는 raw RGB 노멀맵입니다.</summary>
    [Name("normal_resource_idx")]
    public uint NormalResourceIdx { get; set; }
    /// <summary>외형의 고정 성별. Male 또는 Female만 허용한다.</summary>
    [Name("gender")]
    public CustomerAttributes Gender { get; set; }
    /// <summary>외형의 고정 연령. Child, Elderly 또는 Adult만 허용한다.</summary>
    [Name("age")]
    public CustomerAttributes Age { get; set; }
    /// <summary>이 외형이 사용할 CustomerDispositionType입니다.</summary>
    [Name("disposition_type")]
    public CustomerDispositionType DispositionType { get; set; }

    /// <summary>외형의 두 분류 축이 각각 허용된 단일 값인지 검사한다.</summary>
    /// <exception cref="ArgumentException">성별 또는 연령 값이 누락·중복·미정의인 경우.</exception>
    public void ValidateClassification()
    {
        if (this.Gender != CustomerAttributes.Male && this.Gender != CustomerAttributes.Female)
            throw new ArgumentException($"gender={this.Gender}: Male 또는 Female이 필요합니다.", nameof(this.Gender));
        if (this.Age != CustomerAttributes.Child && this.Age != CustomerAttributes.Elderly && this.Age != CustomerAttributes.Adult)
            throw new ArgumentException($"age={this.Age}: Child, Elderly 또는 Adult가 필요합니다.", nameof(this.Age));
    }

    /// <summary>성향 enum과 성향별 연령 조합을 검사한다.</summary>
    /// <exception cref="ArgumentException">성향이 미정의되었거나 비-Normal 연령인 경우.</exception>
    public void ValidateDisposition()
    {
        CustomerProfileValidation.ValidateType(this.DispositionType);
        if (this.DispositionType != CustomerDispositionType.Normal && this.Age != CustomerAttributes.Adult)
            throw new ArgumentException($"disposition_type={this.DispositionType}는 Adult 외형만 허용합니다.", nameof(this.Age));
    }
}
