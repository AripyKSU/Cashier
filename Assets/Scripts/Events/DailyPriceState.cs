using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>선정된 뉴스와 현재가의 불변 snapshot. 방송 시 새 결과로 교체하고 기존 방문은 유지한다.</summary>
public sealed class DailyPriceState
{
    /// <summary>게임 시작 후 경과일.</summary>
    public uint ElapsedDays { get; }
    /// <summary>신문 이벤트 PK. 뉴스가 없으면 null.</summary>
    public uint? NewspaperEventIdx { get; }
    /// <summary>방송 예정 라디오 PK. 후보가 없으면 null이며 방송 여부는 IsRadioBroadcast로 확인한다.</summary>
    public uint? RadioEventIdx { get; }
    /// <summary>선정된 라디오가 방송되어 가격에 반영됐는지 나타낸다.</summary>
    public bool IsRadioBroadcast { get; }
    /// <summary>전 상품 PK별 현재가. 기본가격 원본과 별개다.</summary>
    public IReadOnlyDictionary<uint, uint> Prices { get; }

    /// <summary>계산이 모두 성공한 결과만 읽기 전용으로 공개한다.</summary>
    /// <param name="day">경과일.</param>
    /// <param name="newspaper">신문 이벤트.</param>
    /// <param name="radio">라디오 이벤트.</param>
    /// <param name="prices">검증된 상품 가격.</param>
    /// <param name="isRadioBroadcast">라디오 방송 완료 여부.</param>
    internal DailyPriceState(uint day, uint? newspaper, uint? radio, Dictionary<uint, uint> prices, bool isRadioBroadcast = false)
    {
        ElapsedDays = day;
        NewspaperEventIdx = newspaper;
        RadioEventIdx = radio;
        IsRadioBroadcast = isRadioBroadcast;
        Prices = new ReadOnlyDictionary<uint, uint>(prices);
    }
}
