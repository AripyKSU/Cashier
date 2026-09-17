using System;
using CsvHelper.Configuration.Attributes;

/// <summary>엔딩 한 페이지의 순서와 Text·Resource 참조. 결과 판정은 세션이 소유한다.</summary>
public sealed class EndingPageData
{
    /// <summary>엔딩 페이지 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>GameOver=1, Good=2, Bad=3, CitizenshipNegative=4인 종료 종류의 숫자 코드.</summary>
    [Name("ending_kind")] public uint EndingKindValue { get; set; }
    /// <summary>해당 엔딩 안에서 1부터 연속하는 페이지 순서.</summary>
    [Name("page_order")] public uint PageOrder { get; set; }
    /// <summary>개행 포함 대사 TextData FK. 빈 셀은 무대사 이미지다.</summary>
    [Name("text_idx")] public uint? TextIdx { get; set; }
    /// <summary>화자 이름 TextData FK. 빈 셀은 이름을 숨긴다.</summary>
    [Name("speaker_nameidx")] public uint? SpeakerNameIdx { get; set; }
    /// <summary>배경 Sprite ResourceData FK. 빈 셀은 중간 또는 마지막 검은 화면이다.</summary>
    [Name("background_resource_idx")] public uint? BackgroundResourceIdx { get; set; }
    /// <summary>페이지 진입 시 한 번 재생할 Sound ResourceData FK. 빈 셀은 효과음 없음이다.</summary>
    [Name("sfx_resource_idx")] public uint? SfxResourceIdx { get; set; }
    /// <summary>페이지 진입 시 딜레이 시간. 빈 셀은 0초이고 딜레이 없음이다.</summary>
    [Name("delay_second")] public float? DelaySecond { get; set; }
    /// <summary>검증된 엔딩 종류.</summary>
    public EndingKind Kind => (EndingKind)EndingKindValue;

    /// <summary>행 단위의 PK·종류·순서·필수 참조를 검사한다.</summary>
    /// <exception cref="ArgumentException">허용하지 않는 값.</exception>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.EndingPage || Idx % 1000 == 0 ||
            Kind <= EndingKind.None || Kind >= EndingKind.EndingKind_End || PageOrder == 0 ||
            (TextIdx.HasValue && (TextIdx.Value == 0 || Util.GetDataTableType(TextIdx.Value) != DataTableType.Text)) ||
            (SpeakerNameIdx.HasValue && (SpeakerNameIdx.Value == 0 || Util.GetDataTableType(SpeakerNameIdx.Value) != DataTableType.Text)) ||
            (BackgroundResourceIdx.HasValue && (BackgroundResourceIdx.Value == 0 || Util.GetDataTableType(BackgroundResourceIdx.Value) != DataTableType.Resource)) ||
            (SfxResourceIdx.HasValue && (SfxResourceIdx.Value == 0 || Util.GetDataTableType(SfxResourceIdx.Value) != DataTableType.Resource)) ||
            (!TextIdx.HasValue && SpeakerNameIdx.HasValue) ||
            (!TextIdx.HasValue && !BackgroundResourceIdx.HasValue && !SfxResourceIdx.HasValue) ||
            (DelaySecond.HasValue && (DelaySecond.Value < 0 || float.IsNaN(DelaySecond.Value) || float.IsInfinity(DelaySecond.Value))))
            throw new ArgumentException($"EndingPage PK={Idx}: 종류·순서·필수 FK 오류");
    }
}
