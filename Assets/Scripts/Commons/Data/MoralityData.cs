using CsvHelper.Configuration.Attributes;

/// <summary>성향·수락 여부·제안 비율 구간에 대응하는 도덕성 변화 데이터.</summary>
public sealed class MoralityData
{
    /// <summary>도덕성 규칙 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>대상 손님 성향 타입.</summary>
    [Name("customer_disposition_type")] public CustomerDispositionType CustomerDispositionType { get; set; }
    /// <summary>거래 수락 여부.</summary>
    [Name("is_accepted"), TypeConverter(typeof(ZeroOneBooleanConverter))] public bool IsAccepted { get; set; }
    /// <summary>제안가/현재가 구간 하한. 1000=100%.</summary>
    [Name("offer_min_rate")] public uint OfferMinRate { get; set; }
    /// <summary>제안가/현재가 구간 상한. 0은 명시적 무상한이며 include_max=false여야 한다.</summary>
    [Name("offer_max_rate")] public uint OfferMaxRate { get; set; }
    /// <summary>하한 포함 여부.</summary>
    [Name("include_min"), TypeConverter(typeof(ZeroOneBooleanConverter))] public bool IncludeMin { get; set; }
    /// <summary>상한 포함 여부.</summary>
    [Name("include_max"), TypeConverter(typeof(ZeroOneBooleanConverter))] public bool IncludeMax { get; set; }
    /// <summary>성인 도덕성 변화량.</summary>
    [Name("adult_morality_point")] public decimal AdultMoralityPoint { get; set; }
    /// <summary>아이·노인 도덕성 변화량.</summary>
    [Name("child_elderly_morality_point")] public decimal ChildElderlyMoralityPoint { get; set; }
}
