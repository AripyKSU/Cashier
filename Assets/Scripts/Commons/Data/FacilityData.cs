using System;
using System.Globalization;
using CsvHelper.Configuration.Attributes;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

/// <summary>설비 행이 제공하는 업그레이드의 기능 분류.</summary>
public enum FacilityUpgradeKind : uint
{
    None = 0,
    ProductUnlock = 1,
    Convenience = 2,
    StoreStage = 3,
    FacilityUpgradeKind_End
}

/// <summary>편의성 설비가 제공하는 플레이 화면 효과의 고정 키.</summary>
public enum ConvenienceEffectType : uint
{
    None = 0,
    DividerBar = 1,
    AutoSorting = 2,
    Vacuum = 3,
    ConvenienceEffectType_End
}

/// <summary>FacilityUpgradeKind를 CSV의 숫자 코드로만 변환한다.</summary>
public sealed class FacilityUpgradeKindConverter : DefaultTypeConverter
{
    /// <summary>정의된 숫자 업그레이드 분류만 변환한다.</summary>
    /// <param name="text">CSV 숫자 코드.</param>
    /// <param name="row">현재 CSV 행.</param>
    /// <param name="memberMapData">현재 컬럼 매핑.</param>
    /// <returns>변환된 업그레이드 분류.</returns>
    /// <exception cref="FormatException">숫자가 아니거나 유효한 분류가 아님.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) ||
            value == (uint)FacilityUpgradeKind.None || value >= (uint)FacilityUpgradeKind.FacilityUpgradeKind_End ||
            !Enum.IsDefined(typeof(FacilityUpgradeKind), value))
            throw new FormatException($"upgrade_kind={text}: 정의된 숫자 업그레이드 분류 필요");
        return (FacilityUpgradeKind)value;
    }
}

/// <summary>ConvenienceEffectType을 CSV의 숫자 코드로만 변환한다.</summary>
public sealed class ConvenienceEffectTypeConverter : DefaultTypeConverter
{
    /// <summary>정의된 숫자 편의성 효과만 변환한다.</summary>
    /// <param name="text">CSV 숫자 코드.</param>
    /// <param name="row">현재 CSV 행.</param>
    /// <param name="memberMapData">현재 컬럼 매핑.</param>
    /// <returns>변환된 편의성 효과.</returns>
    /// <exception cref="FormatException">숫자가 아니거나 유효한 효과가 아님.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) ||
            value >= (uint)ConvenienceEffectType.ConvenienceEffectType_End ||
            !Enum.IsDefined(typeof(ConvenienceEffectType), value))
            throw new FormatException($"effect_type={text}: 정의된 숫자 편의성 효과 필요");
        return (ConvenienceEffectType)value;
    }
}

/// <summary>단계·상품 해금·편의성 효과를 표현하는 설비 업그레이드 설정.</summary>
public sealed class FacilityData
{
    /// <summary>설비 PK. 종류 대역과 이름 FK는 로딩 경계에서 검증한다.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>설비 표시 이름의 TextData FK.</summary>
    [Name("nameidx")] public uint NameIdx { get; set; }
    /// <summary>일회 구매 가격. 양수 정수 통화 단위.</summary>
    [Name("purchase_price")] public long PurchasePrice { get; set; }
    /// <summary>상품 해금·편의성·가게 단계 상승 중 하나인 업그레이드 종류.</summary>
    [Name("upgrade_kind"), TypeConverter(typeof(FacilityUpgradeKindConverter))]
    public FacilityUpgradeKind UpgradeKind { get; set; }
    /// <summary>구매 가능한 현재 가게 단계. 실제 세션 단계와 같은 1~3 범위다.</summary>
    [Name("required_store_stage")] public uint RequiredStoreStage { get; set; }
    /// <summary>편의성 업그레이드의 고정 효과 키. 일반 업그레이드는 None이다.</summary>
    [Name("effect_type"), TypeConverter(typeof(ConvenienceEffectTypeConverter))]
    public ConvenienceEffectType EffectType { get; set; }
    /// <summary>단계 상승의 목표 단계. 단계 상승이 아니면 0이며 실제 가게 단계 0을 뜻하지 않는다.</summary>
    [Name("target_store_stage")] public uint TargetStoreStage { get; set; }

    /// <summary>행의 필수값과 업그레이드 종류별 단계·효과 조합을 검증한다.</summary>
    /// <exception cref="ArgumentException">PK·이름·가격·enum·단계·효과 조합이 유효하지 않음.</exception>
    public void Validate()
    {
        if (Idx == 0 || NameIdx == 0 || PurchasePrice <= 0)
            throw new ArgumentException($"Facility PK={Idx}: idx/nameidx 및 양수 purchase_price가 필요합니다.");
        if (UpgradeKind == FacilityUpgradeKind.None || UpgradeKind == FacilityUpgradeKind.FacilityUpgradeKind_End ||
            !Enum.IsDefined(typeof(FacilityUpgradeKind), UpgradeKind))
            throw new ArgumentException($"Facility PK={Idx}: upgrade_kind가 유효하지 않습니다.");
        if (RequiredStoreStage < 1 || RequiredStoreStage > 3)
            throw new ArgumentException($"Facility PK={Idx}: required_store_stage는 1~3이어야 합니다.");
        if (EffectType == ConvenienceEffectType.ConvenienceEffectType_End ||
            !Enum.IsDefined(typeof(ConvenienceEffectType), EffectType))
            throw new ArgumentException($"Facility PK={Idx}: effect_type이 유효하지 않습니다.");

        switch (UpgradeKind)
        {
            case FacilityUpgradeKind.ProductUnlock:
                if (EffectType != ConvenienceEffectType.None || TargetStoreStage != 0)
                    throw new ArgumentException($"Facility PK={Idx}: 상품 해금은 effect_type=None, target_store_stage=0이어야 합니다.");
                break;
            case FacilityUpgradeKind.Convenience:
                if (EffectType == ConvenienceEffectType.None || TargetStoreStage != 0)
                    throw new ArgumentException($"Facility PK={Idx}: 편의성 업그레이드는 효과가 있고 target_store_stage=0이어야 합니다.");
                break;
            case FacilityUpgradeKind.StoreStage:
                if ((TargetStoreStage != 2 && TargetStoreStage != 3) ||
                    RequiredStoreStage != TargetStoreStage - 1 || EffectType != ConvenienceEffectType.None)
                    throw new ArgumentException($"Facility PK={Idx}: 단계 상승은 요구 단계가 목표 단계보다 1 낮고 목표가 2 또는 3이어야 합니다.");
                break;
            default:
                throw new ArgumentException($"Facility PK={Idx}: upgrade_kind가 유효하지 않습니다.");
        }
    }
}
