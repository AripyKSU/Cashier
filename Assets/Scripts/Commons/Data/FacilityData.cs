using System;
using CsvHelper.Configuration.Attributes;

/// <summary>한 번 구매하면 다음 영업일부터 상품을 해금하는 독립 설비 설정.</summary>
public sealed class FacilityData
{
    /// <summary>설비 PK. 종류 대역과 이름 FK는 로딩 경계에서 검증한다.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>설비 표시 이름의 TextData FK.</summary>
    [Name("nameidx")] public uint NameIdx { get; set; }
    /// <summary>일회 구매 가격. 양수 정수 통화 단위.</summary>
    [Name("purchase_price")] public long PurchasePrice { get; set; }

    /// <summary>행의 필수값을 검증한다. 단계 선행조건은 존재하지 않는다.</summary>
    /// <exception cref="ArgumentException">PK·이름 FK가0이거나 구매 가격이 양수가 아님.</exception>
    public void Validate()
    {
        if (Idx == 0 || NameIdx == 0 || PurchasePrice <= 0)
            throw new ArgumentException($"Facility PK={Idx}: idx/nameidx 및 양수 purchase_price가 필요합니다.");
    }
}
