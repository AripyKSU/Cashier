using System;
using CsvHelper.Configuration.Attributes;

/// <summary>
/// 일자별 당일 지침 DTO.
/// 지침의 제목, 내용, 규칙 유형, 대상 물품 및 파라미터를 정의합니다.
/// </summary>
public sealed class DailyGuidelineData
{
    /// <summary>지침 PK (13001~13999).</summary>
    [Name("idx")]
    public uint Idx { get; set; }

    /// <summary>적용 일차 (1일차, 2일차...).</summary>
    [Name("day")]
    public uint Day { get; set; }

    /// <summary>지침 소제목의 TextData.idx FK (예: "오늘의 지침").</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }

    /// <summary>지침 상세 내용의 TextData.idx FK (예: "제한 없음.").</summary>
    [Name("descriptionidx")]
    public uint DescriptionIdx { get; set; }

    /// <summary>규칙 유형 (0: 제한 없음, 1: 수량 제한, 2: 판매 금지 등).</summary>
    [Name("rule_type")]
    public uint RuleType { get; set; }

    /// <summary>규칙 대상 물품 FK (ProductData.idx, 없으면 0).</summary>
    [Name("target_product_idx")]
    public uint TargetProductIdx { get; set; }

    /// <summary>규칙 수치 파라미터 (예: 수량 제한 개수 등).</summary>
    [Name("param_value")]
    public int ParamValue { get; set; }

    /// <summary>행 내부의 필수값과 데이터 유효성을 검사합니다.</summary>
    /// <exception cref="ArgumentException">필수값이 누락되었거나 범위를 벗어난 경우 발생합니다.</exception>
    public void Validate()
    {
        if (this.Idx == 0 || this.Day == 0 || this.NameIdx == 0 || this.DescriptionIdx == 0)
        {
            throw new ArgumentException($"DailyGuideline PK={this.Idx}: 필수 컬럼(idx, day, nameidx, descriptionidx) 누락");
        }
    }
}
