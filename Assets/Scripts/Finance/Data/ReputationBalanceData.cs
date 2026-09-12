using CsvHelper.Configuration.Attributes;

/// <summary>
/// 한 명성 구간의 손님 구성 가중치와 양수 회복 배율을 정의합니다.
/// </summary>
public sealed class ReputationBalanceData
{
    /// <summary>명성 밸런스 행의 PK입니다.</summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>구간에 포함되는 최소 명성입니다.</summary>
    [Name("min_reputation")]
    public int MinReputation { get; set; }

    /// <summary>구간에 포함되는 최대 명성입니다.</summary>
    [Name("max_reputation")]
    public int MaxReputation { get; set; }

    /// <summary>일반 손님 구성 확률입니다. 1000이 100%입니다.</summary>
    [Name("normal_weight")]
    public int NormalWeight { get; set; }

    /// <summary>가격 민감 손님 구성 확률입니다. 1000이 100%입니다.</summary>
    [Name("price_sensitive_weight")]
    public int PriceSensitiveWeight { get; set; }

    /// <summary>Wealthy 손님 구성 확률입니다. 1000이 100%입니다.</summary>
    [Name("wealthy_weight")]
    public int WealthyWeight { get; set; }

    /// <summary>Hasty 손님 구성 확률입니다. 1000이 100%입니다.</summary>
    [Name("hasty_weight")]
    public int HastyWeight { get; set; }

    /// <summary>가난 손님 구성 확률입니다. 1000이 100%입니다.</summary>
    [Name("poor_weight")]
    public int PoorWeight { get; set; }

    /// <summary>양수 명성 변화에 적용할 회복 배율입니다. 1000이 1배입니다.</summary>
    [Name("recovery_rate")]
    public int RecoveryRate { get; set; }

    /// <summary>정산 행동 점수의 하한입니다.</summary>
    [Name("settlement_min_score")]
    public int SettlementMinScore { get; set; }

    /// <summary>정산 행동 점수의 상한입니다.</summary>
    [Name("settlement_max_score")]
    public int SettlementMaxScore { get; set; }

    /// <summary>회복 배율 적용 전 기본 일일 변화량입니다.</summary>
    [Name("settlement_delta")]
    public int SettlementDelta { get; set; }
}
