using System;
using CsvHelper.Configuration.Attributes;

/// <summary>
/// 게임 시작 보유금과 상납 주기 등 전역 경제 설정을 나타냅니다.
/// </summary>
[Serializable]
public sealed class EconomyBalanceData
{
    /// <summary>
    /// 경제 기본 밸런스 데이터의 고유 식별자입니다.
    /// </summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>
    /// 새 게임을 시작할 때 적용하는 보유금입니다.
    /// </summary>
    [Name("initialBalance")]
    public long InitialBalance { get; set; }

    /// <summary>
    /// 상납금 납부 사이의 게임 내 일수입니다.
    /// </summary>
    [Name("maintenanceCycleDays")]
    public int MaintenanceCycleDays { get; set; }
}
