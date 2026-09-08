using System;
using System.Linq;
using CsvHelper.Configuration.Attributes;

/// <summary>가격 효과 방식. 비율 증감은 1000=100%이며 값의 부호로 방향을 표현한다.</summary>
public enum PriceChangeType
{
    None = 0,
    Rate = 1,
    Amount = 2,
    PriceChangeType_End
}

/// <summary>뉴스 제목·설명과 상품 또는 품목류에 적용하는 당일 가격 효과.</summary>
public sealed class PriceEventData
{
    /// <summary>이벤트 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>TextData 제목 FK.</summary>
    [Name("nameidx")] public uint NameIdx { get; set; }
    /// <summary>TextData 설명 FK.</summary>
    [Name("descriptionidx")] public uint DescriptionIdx { get; set; }
    /// <summary>대상 상품 FK 목록. 품목류 대상과 합집합으로 적용한다.</summary>
    [Name("product_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public uint[] ProductIdxs { get; set; } = Array.Empty<uint>();
    /// <summary>숫자 ProductType 목록.</summary>
    [Name("product_types"), TypeConverter(typeof(ProductTypeArrayConverter))]
    public ProductType[] ProductTypes { get; set; } = Array.Empty<ProductType>();
    /// <summary>0=효과 없음, 1=비율, 2=금액. 숫자로 읽고 enum으로 해석한다.</summary>
    [Name("change_type")] public uint ChangeTypeValue { get; set; }
    /// <summary>부호 있는 증감량. 비율 -200은 20% 인하다.</summary>
    [Name("change_value")] public int ChangeValue { get; set; }
    /// <summary>검증된 가격 효과 종류.</summary>
    [Ignore] public PriceChangeType ChangeType => (PriceChangeType)ChangeTypeValue;

    /// <summary>행 내부의 필수값·범위·대상 중복을 검사한다. 실제 FK는 로딩 경계에서 검사한다.</summary>
    /// <exception cref="ArgumentException">잘못된 값 또는 중복 대상.</exception>
    public void Validate()
    {
        if (Idx == 0 || NameIdx == 0 || DescriptionIdx == 0 || ProductIdxs == null || ProductTypes == null ||
            ProductIdxs.Any(x => x == 0) || ProductIdxs.Distinct().Count() != ProductIdxs.Length ||
            ProductTypes.Any(x => x == ProductType.None || !Enum.IsDefined(typeof(ProductType), x)) ||
            ProductTypes.Distinct().Count() != ProductTypes.Length ||
            ChangeTypeValue >= (uint)PriceChangeType.PriceChangeType_End ||
            (ChangeType == PriceChangeType.Rate && ChangeValue < -1000) ||
            (ChangeType == PriceChangeType.None && (ChangeValue != 0 || ProductIdxs.Length + ProductTypes.Length != 0)) ||
            (ChangeType != PriceChangeType.None && ProductIdxs.Length + ProductTypes.Length == 0))
            throw new ArgumentException($"PriceEvent PK={Idx}: 효과·대상·필수값 오류");
    }
}
