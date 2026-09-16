using System;
using CsvHelper.Configuration.Attributes;

/// <summary>표시 일차부터 적용할 딸 이미지.</summary>
public sealed class DaughterAppearanceData
{
    /// <summary>이미지 구간 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>이미지가 적용되는 첫 표시 일차.</summary>
    [Name("start_day")] public uint StartDay { get; set; }
    /// <summary>Sprite ResourceData FK. 빈 셀은 사각형 표시이며 0은 허용하지 않는다.</summary>
    [Name("resource_idx")] public uint? ResourceIdx { get; set; }

    /// <summary>PK 대역·날짜와 지정된 리소스 범위를 확인한다.</summary>
    /// <exception cref="ArgumentException">필수값 또는 범위 오류.</exception>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.DaughterAppearance || Idx % 1000 == 0 ||
            StartDay == 0 || (ResourceIdx.HasValue && (ResourceIdx.Value % 1000 == 0 ||
                Util.GetDataTableType(ResourceIdx.Value) != DataTableType.Resource)))
            throw new ArgumentException($"DaughterAppearance PK={Idx}: 필수값·범위 오류");
    }
}
