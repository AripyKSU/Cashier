using System;
using CsvHelper.Configuration.Attributes;

/// <summary>
/// 한 회차에 적용할 상납금 밸런스 설정을 나타냅니다.
/// </summary>
[Serializable]
public sealed class MaintenanceBalanceData
{
    /// <summary>
    /// 회차별 상납금 데이터의 고유 식별자입니다.
    /// </summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>
    /// 상납금을 적용할 납부 회차입니다.
    /// </summary>
    [Name("paymentRound")]
    public int PaymentRound { get; set; }

    /// <summary>
    /// 해당 회차에 납부해야 하는 상납금입니다.
    /// </summary>
    [Name("maintenanceAmount")]
    public long MaintenanceAmount { get; set; }
}
