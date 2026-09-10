using System;
using CsvHelper.Configuration.Attributes;

/// <summary>독립적으로 선정하는 뉴스 채널.</summary>
public enum PriceEventChannel
{
    None = 0,
    Newspaper = 1,
    Radio = 2,
    PriceEventChannel_End
}

/// <summary>이벤트의 날짜 후보와 채널별 상대 가중치.</summary>
public sealed class PriceEventScheduleData
{
    /// <summary>스케줄 PK.</summary>
    [Name("idx")] public uint Idx { get; set; }
    /// <summary>PriceEventData FK.</summary>
    [Name("event_idx")] public uint EventIdx { get; set; }
    /// <summary>1=신문, 2=라디오. 문자열 enum 이름은 파싱하지 않는다.</summary>
    [Name("channel")] public uint ChannelValue { get; set; }
    /// <summary>최초 발생 가능일. 게임 시작일은 0이다.</summary>
    [Name("start_day")] public uint StartDay { get; set; }
    /// <summary>종료일 포함. 빈 셀은 제한 없음.</summary>
    [Name("end_day")] public uint? EndDay { get; set; }
    /// <summary>신문 반복 간격. 0은 지정일 한 번. 라디오는 0만 허용한다.</summary>
    [Name("repeat_days")] public uint RepeatDays { get; set; }
    /// <summary>양수 상대 가중치.</summary>
    [Name("selection_weight")] public uint SelectionWeight { get; set; }
    /// <summary>검증된 채널.</summary>
    [Ignore] public PriceEventChannel Channel => (PriceEventChannel)ChannelValue;

    /// <summary>채널·날짜·가중치 계약을 검사한다.</summary>
    /// <exception cref="ArgumentException">잘못된 스케줄 값.</exception>
    public void Validate()
    {
        if (Idx == 0 || EventIdx == 0 || ChannelValue == 0 ||
            ChannelValue >= (uint)PriceEventChannel.PriceEventChannel_End ||
            EndDay < StartDay || SelectionWeight == 0 ||
            (Channel == PriceEventChannel.Radio && RepeatDays != 0))
            throw new ArgumentException($"PriceEventSchedule PK={Idx}: 채널·날짜·가중치 오류");
    }

    /// <summary>주어진 경과일에 발생 후보인지 확인한다.</summary>
    /// <param name="day">0부터 시작하는 경과일.</param>
    /// <returns>날짜 범위와 반복 조건 충족 여부.</returns>
    public bool IsDue(uint day) => day >= StartDay && (!EndDay.HasValue || day <= EndDay.Value) &&
        (Channel == PriceEventChannel.Radio || (RepeatDays == 0 ? day == StartDay : (day - StartDay) % RepeatDays == 0));
}
