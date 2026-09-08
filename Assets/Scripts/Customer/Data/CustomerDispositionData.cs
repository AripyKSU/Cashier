using System.Collections.Generic;
using System;
using CsvHelper.Configuration.Attributes;

/// <summary>손님 성향의 구매 선호·가격 허용도·TextData 대사 참조.</summary>
public sealed class CustomerDispositionData
{
    /// <summary>대기 한도 초. 초기 규격은 재촉 기준 6초보다 커야 한다.</summary>
    [Name("queue_patience_seconds")] public uint QueuePatienceSeconds { get; set; }
    /// <summary>재촉 대사의 TextData FK.</summary>
    [Name("queue_warning_textidx")] public uint QueueWarningTextIdx { get; set; }
    /// <summary>이탈 불만 대사의 TextData FK.</summary>
    [Name("queue_leave_textidx")] public uint QueueLeaveTextIdx { get; set; }

    /// <summary>승인된 성향 데이터 ID.</summary>
    [Name("idx")]
    public uint Idx { get; set; }
    /// <summary>성향 표시 이름의 TextData.idx FK.</summary>
    [Name("nameidx")]
    public uint NameIdx { get; set; }
    /// <summary>CSV의 숫자 전용 성향 타입. 빈값·문자열은 uint 변환에서 거부한다.</summary>
    [Name("disposition_type")]
    public uint DispositionTypeValue { get; set; }
    /// <summary>개별 성향 PK와 별개인 타입. 유효성은 구매 설정 검증에서 확인한다.</summary>
    [Ignore]
    public CustomerDispositionType DispositionType
    {
        get => (CustomerDispositionType)DispositionTypeValue;
        set => DispositionTypeValue = (uint)value;
    }
    /// <summary>품목 선호와 OR로 적용하는 ProductData.idx 목록. 빈 목록 허용, 0·중복 금지.</summary>
    [Name("preferred_product_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public IReadOnlyList<uint> PreferredProductIdxs { get; set; } = new uint[0];
    /// <summary>주로 선택하는 enum 분류. 빈 목록은 선호 없음이다.</summary>
    [Name("preferred_product_types"), TypeConverter(typeof(ProductTypeArrayConverter))]
    public IReadOnlyList<ProductType> PreferredProductTypes { get; set; } = new ProductType[0];
    /// <summary>가격 허용 배율. 1000=정가의 100%, 확률과 달리 1000 초과 허용.</summary>
    [Name("price_tolerance")]
    public int PriceTolerance { get; set; } = 1000;
    /// <summary>입장 시 선택할 대사 TextData FK 목록. 필수이며 중복 금지.</summary>
    [Name("entry_text_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public IReadOnlyList<uint> EntryTextIdxs { get; set; } = new uint[0];
    /// <summary>수락 대사 TextData FK 목록.</summary>
    [Name("regular_sale_text_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public IReadOnlyList<uint> RegularSaleTextIdxs { get; set; } = new uint[0];
    /// <summary>저가 판매 대사 TextData FK 목록.</summary>
    [Name("discount_sale_text_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public IReadOnlyList<uint> DiscountSaleTextIdxs { get; set; } = new uint[0];
    /// <summary>착취 판매 대사 TextData FK 목록.</summary>
    [Name("exploitative_sale_text_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public IReadOnlyList<uint> ExploitativeSaleTextIdxs { get; set; } = new uint[0];
    /// <summary>거절 대사 TextData FK 목록.</summary>
    [Name("reject_text_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public IReadOnlyList<uint> RejectTextIdxs { get; set; } = new uint[0];
    /// <summary>양쪽 후보가 남아 있을 때 선호군 선택 확률, 0~1000 (1000 = 100%).</summary>
    [Name("preferred_selection_chance")]
    public int PreferredSelectionChance { get; set; } = 900;
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

    /// <summary>대기열 데이터의 필수값을 검증한다. 독립 구매 테스트와 별도 계약이다.</summary>
    /// <exception cref="ArgumentException">대기 한도 또는 대사 참조가 잘못됨.</exception>
    public void ValidateQueueSettings()
    {
        if (QueuePatienceSeconds <= 6 || QueueWarningTextIdx == 0 || QueueLeaveTextIdx == 0)
            throw new ArgumentException($"성향 {Idx}: queue_patience_seconds > 6 및 대사 FK가 필요합니다.");
    }

    /// <summary>생성기와 CSV 로더가 공유하는 구매 설정의 불변 조건을 검사한다.</summary>
    /// <exception cref="ArgumentException">확률·수량 범위 또는 선호 상품군 ID가 잘못된 경우.</exception>
    public void ValidatePurchaseSettings()
    {
        CustomerProfileValidation.ValidateType(DispositionType);
        if (PreferredProductIdxs == null) throw new ArgumentException($"성향 {Idx}: preferred_product_idxs null");
        var productIds = new HashSet<uint>();
        foreach (uint idx in PreferredProductIdxs)
            if (idx == 0 || !productIds.Add(idx))
                throw new ArgumentException($"성향 {Idx}: preferred_product_idxs 중복 또는 0");
        if (PreferredProductTypes == null || PreferredSelectionChance < 0 || PreferredSelectionChance > 1000 || PriceTolerance <= 0 ||
            MinProductKinds < 1 || MaxProductKinds < MinProductKinds || MaxProductKinds == int.MaxValue ||
            MinQuantity < 1 || MaxQuantity < MinQuantity || MaxQuantity == int.MaxValue)
            throw new ArgumentException($"성향 {Idx}: 구매 설정 범위가 잘못되었습니다.");
        var categories = new HashSet<ProductType>();
        foreach (var category in PreferredProductTypes)
            if (category == ProductType.None || !Enum.IsDefined(typeof(ProductType), category) || !categories.Add(category))
                throw new ArgumentException($"성향 {Idx}: preferred_product_types는 정의된 고유 분류여야 합니다.");
        validateDialog(EntryTextIdxs, "entry_text_idxs");
        validateDialog(RegularSaleTextIdxs, "regular_sale_text_idxs");
        validateDialog(DiscountSaleTextIdxs, "discount_sale_text_idxs");
        validateDialog(ExploitativeSaleTextIdxs, "exploitative_sale_text_idxs");
        validateDialog(RejectTextIdxs, "reject_text_idxs");
    }

    /// <summary>대사 후보의 필수값·중복을 검사한다. 실제 FK는 catalog에서 검사한다.</summary>
    /// <param name="values">대사 PK 목록.</param>
    /// <param name="column">오류 컬럼.</param>
    /// <exception cref="ArgumentException">빈 목록·0·중복 ID.</exception>
    private void validateDialog(IReadOnlyList<uint> values, string column)
    {
        if (values == null || values.Count == 0) throw new ArgumentException($"성향 {Idx}: {column} 필수");
        var seen = new HashSet<uint>();
        foreach (uint value in values)
            if (value == 0 || !seen.Add(value)) throw new ArgumentException($"성향 {Idx}: {column} 중복 또는 0");
    }
}
