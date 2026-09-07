using System;
using CsvHelper.Configuration.Attributes;

/// <summary>
/// 표시 문자열의 단일 원본 (TextData.csv, Type 8: 8001~). 다른 CSV는 nameidx로 참조한다.
/// </summary>
[Serializable]
public class TextData
{
    /// <summary>표시 문자열 PK, 8001~8999.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>실제 표시 문구. 명시적으로 문자열을 허용하는 text 열.</summary>
    [Name("text")]
    public string Text { get; set; }
}
