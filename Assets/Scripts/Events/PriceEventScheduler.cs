using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>신문·라디오를 독립적으로 선정하고 기본가격에서 당일 현재가를 계산한다.</summary>
public sealed class PriceEventScheduler
{
    private readonly Random random;

    /// <summary>세션이 소유하는 난수원을 지정한다.</summary>
    /// <param name="random">재현 검증에는 고정 seed를 사용한다.</param>
    /// <exception cref="ArgumentNullException">난수원 누락.</exception>
    public PriceEventScheduler(Random random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>당일 두 채널을 선정하고 신문만 반영한 현재가를 반환한다. 라디오는 방송 시 별도로 적용한다.</summary>
    /// <param name="day">경과일.</param>
    /// <param name="events">검증된 이벤트.</param>
    /// <param name="schedules">검증된 스케줄.</param>
    /// <param name="products">기본가격의 단일 원본.</param>
    /// <returns>완전히 계산된 일간 상태.</returns>
    /// <exception cref="InvalidDataException">스케줄 이벤트 FK 또는 상품 참조 오류.</exception>
    /// <exception cref="OverflowException">가중치·현재가 범위 초과.</exception>
    public DailyPriceState CreateDay(uint day, IReadOnlyDictionary<uint, PriceEventData> events,
        IReadOnlyDictionary<uint, PriceEventScheduleData> schedules, IReadOnlyDictionary<uint, ProductData> products)
    {
        foreach (var row in events.Values) row.Validate();
        foreach (var row in schedules.Values)
        {
            row.Validate();
            if (!events.ContainsKey(row.EventIdx)) throw new InvalidDataException($"Schedule PK={row.Idx}: event FK={row.EventIdx}");
        }
        foreach (var row in events.Values)
            foreach (uint idx in row.ProductIdxs)
                if (!products.ContainsKey(idx)) throw new InvalidDataException($"Event PK={row.Idx}: product FK={idx}");
        uint? newspaper = select(day, PriceEventChannel.Newspaper, schedules);
        uint? radio = select(day, PriceEventChannel.Radio, schedules);
        return new DailyPriceState(day, newspaper, radio, calculatePrices(newspaper, null, events, products));
    }

    /// <summary>재추첨 없이 예약한 방송을 적용한다. 이미 방송했거나 후보가 없으면 기존 상태를 반환한다.</summary>
    /// <param name="state">현재 일간 상태.</param>
    /// <param name="events">검증된 이벤트 원본.</param>
    /// <param name="products">기본가격 원본.</param>
    /// <returns>신문과 라디오 효과를 함께 계산한 새 snapshot.</returns>
    /// <exception cref="OverflowException">현재가 범위 초과.</exception>
    public DailyPriceState ApplyRadio(DailyPriceState state, IReadOnlyDictionary<uint, PriceEventData> events,
        IReadOnlyDictionary<uint, ProductData> products)
    {
        if (state.IsRadioBroadcast || !state.RadioEventIdx.HasValue) return state;
        return new DailyPriceState(state.ElapsedDays, state.NewspaperEventIdx, state.RadioEventIdx,
            calculatePrices(state.NewspaperEventIdx, state.RadioEventIdx, events, products), true);
    }

    /// <summary>영업 시작 기준 0~60초 미만의 무작위 방송 대기 시간을 생성한다.</summary>
    /// <returns>일시정지 시간을 제외할 대기 초.</returns>
    public float GetRadioDelaySeconds() => (float)(random.NextDouble() * 60);

    /// <summary>활성 효과만 기본가격에서 재계산하여 누적 적용을 방지한다.</summary>
    /// <param name="newspaper">신문 PK.</param>
    /// <param name="radio">방송된 라디오 PK.</param>
    /// <param name="events">이벤트 원본.</param>
    /// <param name="products">상품 원본.</param>
    /// <returns>검증된 현재가 사전.</returns>
    /// <exception cref="OverflowException">현재가 범위 초과.</exception>
    private Dictionary<uint, uint> calculatePrices(uint? newspaper, uint? radio,
        IReadOnlyDictionary<uint, PriceEventData> events, IReadOnlyDictionary<uint, ProductData> products)
    {
        // 같은 사건이 두 채널에 보도되어도 가격 효과는 한 번만 반영한다.
        var selected = new HashSet<uint>();
        if (newspaper.HasValue) selected.Add(newspaper.Value);
        if (radio.HasValue) selected.Add(radio.Value);
        var prices = new Dictionary<uint, uint>();
        foreach (var pair in products)
        {
            pair.Value.Validate();
            decimal rate = 0, amount = 0;
            foreach (uint idx in selected)
            {
                var effect = events[idx];
                if (!effect.ProductIdxs.Contains(pair.Key) && !effect.ProductTypes.Contains(pair.Value.ProductType)) continue;
                if (effect.ChangeType == PriceChangeType.Rate) rate += effect.ChangeValue;
                if (effect.ChangeType == PriceChangeType.Amount) amount += effect.ChangeValue;
            }
            decimal value = Math.Max(1m, decimal.Floor(pair.Value.BasePrice * (1000m + rate) / 1000m) + amount);
            prices.Add(pair.Key, checked((uint)value));
        }
        return prices;
    }

    /// <summary>같은 채널의 날짜 후보에서 가중치로 하나를 고른다. 중복 이벤트 스케줄은 동일 이벤트의 가중치를 더하는 효과다.</summary>
    /// <param name="day">경과일.</param>
    /// <param name="channel">선정 채널.</param>
    /// <param name="schedules">스케줄 원본.</param>
    /// <returns>후보가 없으면 null.</returns>
    /// <exception cref="OverflowException">지원 가능한 총 가중치 초과.</exception>
    private uint? select(uint day, PriceEventChannel channel, IReadOnlyDictionary<uint, PriceEventScheduleData> schedules)
    {
        var candidates = schedules.Values.Where(x => x.Channel == channel && x.IsDue(day)).OrderBy(x => x.Idx).ToArray();
        if (candidates.Length == 0) return null;
        int total = 0;
        foreach (var row in candidates) total = checked(total + checked((int)row.SelectionWeight));
        int roll = random.Next(total);
        foreach (var row in candidates)
        {
            if (roll < row.SelectionWeight) return row.EventIdx;
            roll -= (int)row.SelectionWeight;
        }
        throw new InvalidOperationException("가중치 선택 실패");
    }
}
