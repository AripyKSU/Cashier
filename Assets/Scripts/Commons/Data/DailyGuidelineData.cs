using System;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;

/// <summary>CSV 숫자 코드를 유효한 일일지침 유형으로 변환한다.</summary>
public sealed class DailyGuidelineRuleTypeConverter : DefaultTypeConverter
{
    /// <summary>정의된 판매 금지·수량 제한 숫자 코드만 변환한다.</summary>
    /// <param name="text">CSV 숫자 코드.</param>
    /// <param name="row">현재 CSV 행.</param>
    /// <param name="memberMapData">대상 컬럼 매핑.</param>
    /// <returns>검증된 일일지침 유형.</returns>
    /// <exception cref="FormatException">숫자가 아니거나 허용하지 않는 유형.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) ||
            value == (uint)DailyGuidelineRuleType.None ||
            value >= (uint)DailyGuidelineRuleType.DailyGuidelineRuleType_End ||
            !Enum.IsDefined(typeof(DailyGuidelineRuleType), value))
            throw new FormatException($"rule_type={text}: 정의된 숫자 일일지침 유형이 필요합니다.");
        return (DailyGuidelineRuleType)value;
    }
}

/// <summary>
/// 무작위 일일지침 생성에 사용하는 규칙 유형별 설정 DTO.
/// </summary>
public sealed class DailyGuidelineData
{
    /// <summary>지침 PK (13001~13999).</summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>무작위 생성 후보의 지침 유형.</summary>
    [Name("rule_type"), TypeConverter(typeof(DailyGuidelineRuleTypeConverter))]
    public DailyGuidelineRuleType RuleType { get; set; }

    /// <summary>거래당 판매 허용 수량. 판매 금지는 0, 수량 제한은 1.</summary>
    [Name("allowed_quantity")]
    public int AllowedQuantity { get; set; }

    /// <summary>해당 지침을 한 거래에서 위반했을 때의 양수 벌금.</summary>
    [Name("penalty_amount")]
    public long PenaltyAmount { get; set; }

    /// <summary>행 내부의 필수값과 데이터 유효성을 검사합니다.</summary>
    /// <exception cref="ArgumentException">필수값이 누락되었거나 범위를 벗어난 경우 발생합니다.</exception>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.DailyGuideline || Idx % 1000 == 0)
            throw new ArgumentException($"DailyGuideline PK={Idx}: idx 대역 오류");
        if (RuleType == DailyGuidelineRuleType.None || RuleType == DailyGuidelineRuleType.DailyGuidelineRuleType_End ||
            !Enum.IsDefined(typeof(DailyGuidelineRuleType), RuleType))
            throw new ArgumentException($"DailyGuideline PK={Idx}: rule_type 오류");
        int expectedQuantity = RuleType == DailyGuidelineRuleType.SaleProhibited ? 0 : 1;
        if (AllowedQuantity != expectedQuantity)
            throw new ArgumentException($"DailyGuideline PK={Idx}: allowed_quantity는 {expectedQuantity}이어야 합니다.");
        if (PenaltyAmount <= 0)
            throw new ArgumentException($"DailyGuideline PK={Idx}: penalty_amount는 양수여야 합니다.");
    }

    /// <summary>런타임 대상 조건과 물품을 결합해 불변 일일지침을 생성한다.</summary>
    /// <param name="requiredAttributes">성별·연령 AND 조건. None은 모든 손님.</param>
    /// <param name="targetProductIdx">당일 등장 ProductData PK.</param>
    /// <returns>검증된 런타임 일일지침.</returns>
    public DailyGuideline CreateGuideline(CustomerAttributes requiredAttributes, uint targetProductIdx)
    {
        Validate();
        return new DailyGuideline(Idx, RuleType, requiredAttributes, targetProductIdx, AllowedQuantity, PenaltyAmount);
    }
}
