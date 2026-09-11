using System;
using CsvHelper.Configuration.Attributes;

/// <summary>
/// 한 영업일에 적용할 유지비 밸런스 설정을 나타냅니다.
/// </summary>
[Serializable]
public sealed class MaintenanceBalanceData
{
    /// <summary>
    /// 일자별 유지비 데이터의 고유 식별자입니다.
    /// </summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>
    /// 유지비를 적용할 1부터 시작하는 게임 표시 일자입니다.
    /// </summary>
    [Name("day")]
    public int Day { get; set; }

    /// <summary>
    /// 해당 일자에 납부해야 하는 유지비입니다.
    /// </summary>
    [Name("maintenanceAmount")]
    public long MaintenanceAmount { get; set; }
}
