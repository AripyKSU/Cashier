using System;
using CsvHelper.Configuration.Attributes;

/// <summary>
/// Addressable 에셋 참조 데이터. 종류 배정은 CSV_RULES.md의 권위 문서를 따른다.
/// </summary>
[Serializable]
public class ResourceData
{
    /// <summary>현재 리소스 PK.</summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>명시적으로 문자열을 허용하는 Addressables 키.</summary>
    [Name("path")]
    public string Path { get; set; } // Addressable Key ("FemaleCustomer_01" 등)
}
