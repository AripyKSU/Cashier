using System;
using CsvHelper.Configuration.Attributes;

/// <summary>엔딩 한 페이지의 순서와 Text·Resource 참조. 결과 판정은 세션이 소유한다.</summary>
public sealed class EndingPageData
{
    /// <summary>엔딩 페이지 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>Good=2 또는 Bad=3인 종료 종류의 숫자 코드.</summary>
    [Name("ending_kind")] public uint EndingKindValue { get; set; }
    /// <summary>해당 엔딩 안에서 1부터 연속하는 페이지 순서.</summary>
    [Name("page_order")] public uint PageOrder { get; set; }
    /// <summary>개행 포함 대사 TextData FK.</summary>
    [Name("text_idx")] public uint TextIdx { get; set; }
    /// <summary>화자 이름 TextData FK.</summary>
    [Name("speaker_nameidx")] public uint SpeakerNameIdx { get; set; }
    /// <summary>배경 Sprite ResourceData FK.</summary>
    [Name("background_resource_idx")] public uint BackgroundResourceIdx { get; set; }
    /// <summary>검증된 엔딩 종류.</summary>
    public EndingKind Kind => (EndingKind)EndingKindValue;

    /// <summary>행 단위의 PK·종류·순서·필수 참조를 검사한다.</summary>
    /// <exception cref="ArgumentException">허용하지 않는 값.</exception>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.EndingPage || Idx % 1000 == 0 ||
            (Kind != EndingKind.Good && Kind != EndingKind.Bad) || PageOrder == 0 ||
            Util.GetDataTableType(TextIdx) != DataTableType.Text ||
            Util.GetDataTableType(SpeakerNameIdx) != DataTableType.Text ||
            Util.GetDataTableType(BackgroundResourceIdx) != DataTableType.Resource)
            throw new ArgumentException($"EndingPage PK={Idx}: 종류·순서·필수 FK 오류");
    }
}
