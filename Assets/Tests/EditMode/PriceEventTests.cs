using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>실제 이벤트 CSV 및 순수 가격 스케줄 API를 검사한다.</summary>
public sealed class PriceEventTests
{
    private Dictionary<uint, PriceEventData> events;
    private Dictionary<uint, PriceEventScheduleData> schedules;
    private Dictionary<uint, ProductData> products;

    /// <summary>실제 CSV의 파싱·테이블 검증·텍스트 FK 준비를 사례마다 재사용한다.</summary>
    [SetUp]
    public void SetUp()
    {
        string eventCsv = File.ReadAllText("Assets/Datas/PriceEventData.csv");
        string scheduleCsv = File.ReadAllText("Assets/Datas/PriceEventScheduleData.csv");
        events = Util.ParseFromCSV<PriceEventData>(eventCsv).ToDictionary(x => x.Idx);
        schedules = Util.ParseFromCSV<PriceEventScheduleData>(scheduleCsv).ToDictionary(x => x.Idx);
        products = Util.ParseFromCSV<ProductData>(File.ReadAllText("Assets/Datas/Customer/ProductData.csv")).ToDictionary(x => x.Idx);
        var texts = Util.ParseFromCSV<TextData>(File.ReadAllText("Assets/Datas/TextData.csv")).ToDictionary(x => x.Idx);
        Assert.DoesNotThrow(() => new PriceEventDataTable().LoadData(eventCsv));
        Assert.DoesNotThrow(() => new PriceEventScheduleDataTable().LoadData(scheduleCsv));
        foreach (var row in events.Values)
        {
            Assert.DoesNotThrow(row.Validate);
            Assert.That(texts.ContainsKey(row.NameIdx), $"event {row.Idx} name FK");
            Assert.That(texts.ContainsKey(row.DescriptionIdx), $"event {row.Idx} description FK");
        }
        foreach (var row in schedules.Values) Assert.DoesNotThrow(row.Validate);
    }

    /// <summary>정기 신문·단발 일정·라디오 종료일 포함 경계를 검사한다.</summary>
    [Test]
    public void ScheduleDayBoundaries()
    {
        var newspaper = schedules[10001];
        Assert.That(newspaper.IsDue(newspaper.StartDay));
        Assert.That(newspaper.IsDue(newspaper.StartDay + 1), Is.False);
        Assert.That(newspaper.IsDue(newspaper.StartDay + 2));
        var bounded = new PriceEventScheduleData
        { Idx = 1, EventIdx = 9001, ChannelValue = 1, StartDay = 2, EndDay = 4, RepeatDays = 0, SelectionWeight = 1 };
        Assert.That(bounded.IsDue(1), Is.False);
        Assert.That(bounded.IsDue(2));
        Assert.That(bounded.IsDue(4), Is.False);
        Assert.That(bounded.IsDue(5), Is.False);
        bounded.ChannelValue = 2;
        Assert.That(bounded.IsDue(4));
        Assert.That(bounded.IsDue(5), Is.False);
    }

    /// <summary>실제 CSV에 라디오 후보가 없고 신문 후보만 선택되는지 검사한다.</summary>
    [Test]
    public void ActualSchedulesContainNoRadioCandidates()
    {
        Assert.That(schedules.Values.All(x => x.Channel == PriceEventChannel.Newspaper));
        for (int i = 0; i < 20; i++)
        {
            var scheduler = new PriceEventScheduler(new Random(i));
            var state = scheduler.CreateDay((uint)(i % 2), events, schedules, products);
            Assert.That(state.RadioEventIdx, Is.Null);
            Assert.That(state.IsRadioBroadcast, Is.False);
        }
    }

    /// <summary>데이터 후보의 유무만으로 채널 네 조합을 표현한다.</summary>
    /// <param name="newspaper">신문 후보 포함 여부.</param><param name="radio">라디오 후보 포함 여부.</param>
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void IndependentChannelCandidates(bool newspaper, bool radio)
    {
        var selected = newspaper
            ? schedules.ToDictionary(x => x.Key, x => x.Value)
            : new Dictionary<uint, PriceEventScheduleData>();
        uint day = newspaper ? schedules.Values.First().StartDay : 0;
        if (radio)
        {
            selected.Add(10999, new PriceEventScheduleData
            {
                Idx = 10999,
                EventIdx = 9001,
                ChannelValue = (uint)PriceEventChannel.Radio,
                StartDay = day,
                RepeatDays = 0,
                SelectionWeight = 1
            });
        }
        var engine = new PriceEventScheduler(new Random(1));
        var state = engine.CreateDay(day, events, selected, products);
        Assert.That(state.NewspaperEventIdx.HasValue, Is.EqualTo(newspaper));
        Assert.That(state.RadioEventIdx.HasValue, Is.EqualTo(radio));
        if (!radio) Assert.That(engine.ApplyRadio(state, events, products), Is.SameAs(state));
    }

    /// <summary>스케줄 순서와 무관한 seed 재현 및 FK·가중치 오류 거부를 확인한다.</summary>
    [Test]
    public void ReproducibleSelectionAndInvalidReferences()
    {
        var reversed = schedules.Reverse().ToDictionary(x => x.Key, x => x.Value);
        uint testDay = schedules.Values.First().StartDay;
        var first = new PriceEventScheduler(new Random(42)).CreateDay(testDay, events, schedules, products);
        var second = new PriceEventScheduler(new Random(42)).CreateDay(testDay, events, reversed, products);
        Assert.That(second.NewspaperEventIdx, Is.EqualTo(first.NewspaperEventIdx));
        Assert.That(second.RadioEventIdx, Is.EqualTo(first.RadioEventIdx));
        var engine = new PriceEventScheduler(new Random(1));
        var row = schedules.Values.First();
        uint eventIdx = row.EventIdx;
        row.EventIdx = 9999;
        Assert.Throws<InvalidDataException>(() => engine.CreateDay(testDay, events, schedules, products));
        row.EventIdx = eventIdx;
        row.SelectionWeight = uint.MaxValue;
        Assert.Throws<OverflowException>(() => engine.CreateDay(testDay, events, schedules, products));
        row.SelectionWeight = 1;
        events[eventIdx].ProductIdxs = new uint[] { 1999 };
        Assert.Throws<InvalidDataException>(() => engine.CreateDay(testDay, events, schedules, products));
    }

    /// <summary>합집합·소수 버림·일자 초기화·불변 가격표·최솟값·중립/overflow를 검사한다.</summary>
    [Test]
    public void PriceUnionRoundingResetAndOverflow()
    {
        var one = new Dictionary<uint, ProductData>
        { [1001] = new ProductData { Idx = 1001, NameIdx = 8012, ProductType = ProductType.Water, BasePrice = 101, CostPrice = 50, IsAvailable = true } };
        var effect = new PriceEventData
        { Idx = 9001, NameIdx = 8042, DescriptionIdx = 8043, ProductIdxs = new uint[] { 1001 }, ProductTypes = new[] { ProductType.Water }, ChangeTypeValue = 1, ChangeValue = -200 };
        var effects = new Dictionary<uint, PriceEventData> { [9001] = effect };
        var rows = new Dictionary<uint, PriceEventScheduleData>
        { [10001] = new PriceEventScheduleData { Idx = 10001, EventIdx = 9001, ChannelValue = 1, StartDay = 0, SelectionWeight = 1 } };
        var engine = new PriceEventScheduler(new Random(1));
        var today = engine.CreateDay(0, effects, rows, one);
        Assert.That(today.Prices[1001], Is.EqualTo(80));
        Assert.That(one[1001].BasePrice, Is.EqualTo(101));
        Assert.That(engine.CreateDay(1, effects, rows, one).Prices[1001], Is.EqualTo(101));
        rows.Add(10002, new PriceEventScheduleData { Idx = 10002, EventIdx = 9001, ChannelValue = 2, StartDay = 0, SelectionWeight = 1 });
        for (int i = 0; i < 20; i++)
            Assert.That(engine.ApplyRadio(new PriceEventScheduler(new Random(i)).CreateDay(0, effects, rows, one), effects, one).Prices[1001], Is.EqualTo(80), "Same event must apply only once");
        rows.Remove(10001);
        var beforeRadio = engine.CreateDay(0, effects, rows, one);
        var afterRadio = engine.ApplyRadio(beforeRadio, effects, one);
        Assert.That(beforeRadio.Prices[1001], Is.EqualTo(101));
        Assert.That(afterRadio.Prices[1001], Is.EqualTo(80));
        Assert.That(afterRadio.IsRadioBroadcast);
        Assert.That(engine.ApplyRadio(afterRadio, effects, one), Is.SameAs(afterRadio));
        rows.Add(10001, new PriceEventScheduleData { Idx = 10001, EventIdx = 9001, ChannelValue = 1, StartDay = 0, SelectionWeight = 1 });
        rows.Remove(10002);
        effect.ChangeTypeValue = 2;
        effect.ChangeValue = -1000;
        Assert.That(engine.CreateDay(0, effects, rows, one).Prices[1001], Is.EqualTo(1));
        effect.ChangeTypeValue = 0;
        effect.ChangeValue = 0;
        effect.ProductIdxs = Array.Empty<uint>();
        effect.ProductTypes = Array.Empty<ProductType>();
        var neutral = engine.CreateDay(0, effects, rows, one);
        Assert.That(neutral.NewspaperEventIdx.HasValue);
        Assert.That(neutral.Prices[1001], Is.EqualTo(101));
        effect.ChangeTypeValue = 1;
        effect.ChangeValue = int.MaxValue;
        effect.ProductIdxs = new uint[] { 1001 };
        one[1001].BasePrice = uint.MaxValue;
        Assert.Throws<OverflowException>(() => engine.CreateDay(0, effects, rows, one));
    }
}
