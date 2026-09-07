using System.Collections.Generic;
using System;
using CsvHelper.Configuration.Attributes;

/// <summary>손님 성향의 구매 설정. CSV 매핑과 가격·대사 계약은 별도 연결한다.</summary>
public sealed class CustomerDispositionData
{
    /// <summary>승인된 성향 데이터 ID.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>성향 표시 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
    /// <summary>주로 선택하는 상품군 ID. 빈 목록은 선호군 없음이다.</summary>
    [Name("preferred_category_ids"), TypeConverter(typeof(UIntArrayConverter))]
    public IReadOnlyList<uint> PreferredCategoryIds { get; set; } = new uint[0];
    /// <summary>양쪽 후보가 남아 있을 때 선호군 선택 확률, 0~100%.</summary>
    [Name("preferred_selection_percent")]
    public int PreferredSelectionPercent { get; set; } = 90;
    /// <summary>구매 종류 수 최솟값. 후보 부족 시 가능한 종류 수로 제한한다.</summary>
    [Name("min_product_kinds")]
    public int MinProductKinds { get; set; } = 1;
    /// <summary>구매 종류 수 최댓값, 포함 상한.</summary>
    [Name("max_product_kinds")]
    public int MaxProductKinds { get; set; } = 3;
    /// <summary>상품별 구매 수량 최솟값.</summary>
    [Name("min_quantity")]
    public int MinQuantity { get; set; } = 1;
    /// <summary>상품별 구매 수량 최댓값, 포함 상한.</summary>
    [Name("max_quantity")]
    public int MaxQuantity { get; set; } = 3;

    /// <summary>생성기와 CSV 로더가 공유하는 구매 설정의 불변 조건을 검사한다.</summary>
    /// <exception cref="ArgumentException">확률·수량 범위 또는 선호 상품군 ID가 잘못된 경우.</exception>
    public void ValidatePurchaseSettings()
    {
        if (PreferredCategoryIds == null || PreferredSelectionPercent < 0 || PreferredSelectionPercent > 100 ||
            MinProductKinds < 1 || MaxProductKinds < MinProductKinds || MaxProductKinds == int.MaxValue ||
            MinQuantity < 1 || MaxQuantity < MinQuantity || MaxQuantity == int.MaxValue)
            throw new ArgumentException($"성향 {Idx}: 구매 설정 범위가 잘못되었습니다.");
        var categories = new HashSet<uint>();
        foreach (uint category in PreferredCategoryIds)
            if (category == 0 || !categories.Add(category))
                throw new ArgumentException($"성향 {Idx}: preferred_category_ids는 0이 아니며 고유해야 합니다.");
    }
}
