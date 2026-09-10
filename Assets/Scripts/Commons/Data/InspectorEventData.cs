using System;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;

/// <summary>감독관 대사의 세션 또는 날짜별 완료 제한.</summary>
public enum InspectorRepeatMode : uint
{
    OncePerSession = 1,
    OncePerDay = 2,
    InspectorRepeatMode_End
}

/// <summary>문자열 이름이나 종료 표식을 허용하지 않는 반복 모드 변환기.</summary>
public sealed class InspectorRepeatModeConverter : DefaultTypeConverter
{
    /// <summary>승인된 숫자 반복 모드만 읽는다.</summary>
    /// <param name="text">숫자 셀.</param>
    /// <param name="row">CSV 행.</param>
    /// <param name="memberMapData">컬럼 매핑.</param>
    /// <returns>반복 모드.</returns>
    /// <exception cref="FormatException">정의되지 않은 숫자 또는 문자열.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) ||
            (value != 1 && value != 2)) throw new FormatException($"repeat_mode={text}: 1 또는 2 필요");
        return (InspectorRepeatMode)value;
    }
}

/// <summary>하루 시작 조건과 순서가 있는 감독관 대사 CSV. 금액 변경은 포함하지 않는다.</summary>
public sealed class InspectorEventData
{
    /// <summary>감독관 이벤트 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>이름 Text FK.</summary>
    [Name("nameidx")] public uint NameIdx { get; set; }
    /// <summary>표시 일차(1부터). 빈 셀은 날짜 제한 없음.</summary>
    [Name("day")] public uint? Day { get; set; }
    /// <summary>전날까지 구매해야 하는 설비 FK. 빈 셀은 조건 없음.</summary>
    [Name("required_facility_idx")] public uint? RequiredFacilityIdx { get; set; }
    /// <summary>전날까지 도달한 최소 가게 단계(1~3). 빈 셀은 조건 없음.</summary>
    [Name("min_store_stage")] public uint? MinStoreStage { get; set; }
    /// <summary>낮은 값부터 표시하며 동률이면 PK 순서다.</summary>
    [Name("priority")] public int Priority { get; set; }
    /// <summary>완료 이력 적용 범위.</summary>
    [Name("repeat_mode"), TypeConverter(typeof(InspectorRepeatModeConverter))]
    public InspectorRepeatMode RepeatMode { get; set; }
    /// <summary>중복을 허용하는 순서 있는 Text FK. 최소 한 줄 필요.</summary>
    [Name("dialogue_text_idxs"), TypeConverter(typeof(UIntArrayConverter))]
    public uint[] DialogueTextIdxs { get; set; }
    /// <summary>초상 Sprite의 필수 Resource FK.</summary>
    [Name("portrait_resource_idx")] public uint PortraitResourceIdx { get; set; }

    /// <summary>FK 조회 이전에 숫자 대역과 조건·필수값을 검증한다.</summary>
    /// <exception cref="ArgumentException">유효하지 않은 행.</exception>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.InspectorEvent || Idx % 1000 == 0 || NameIdx == 0 || Day == 0 ||
            RequiredFacilityIdx == 0 || MinStoreStage < 1 || MinStoreStage > 3 ||
            (RepeatMode != InspectorRepeatMode.OncePerSession && RepeatMode != InspectorRepeatMode.OncePerDay) ||
            DialogueTextIdxs == null || DialogueTextIdxs.Length == 0 || PortraitResourceIdx == 0)
            throw new ArgumentException($"InspectorEvent PK={Idx}: 필수값·범위·조건 오류");
        foreach (uint textIdx in DialogueTextIdxs)
            if (textIdx == 0) throw new ArgumentException($"InspectorEvent PK={Idx}: dialogue_text_idxs에 0 불가");
    }
}
