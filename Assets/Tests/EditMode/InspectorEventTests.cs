using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>감독관 조건·날짜 캐시·대사 입력·완료 이력 및 실제 CSV 형식을 검사한다.</summary>
public sealed class InspectorEventTests
{
    /// <summary>실제 세 행의 대사·조건과 2일차 임시 이벤트의 선정·완료·날짜 제한을 검사한다.</summary>
    [Test]
    public void ActualCsvHasThreeEventsAndDayTwoRunsOnce()
    {
        var table = new InspectorEventDataTable();
        table.LoadData(File.ReadAllText("Assets/Datas/InspectorEventData.csv"));
        var pending = (System.Collections.Generic.Dictionary<uint, InspectorEventData>)typeof(InspectorEventDataTable)
            .GetProperty("PendingRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(table);
        var rows = pending.Values.OrderBy(x => x.Idx).ToArray();
        Assert.That(rows.Length, Is.EqualTo(3));
        foreach (var row in rows) Assert.DoesNotThrow(row.Validate);
        Assert.That(rows[0].DialogueTextIdxs, Is.EqualTo(Enumerable.Range(8131,20).Select(x => (uint)x)));
        Assert.That(rows[1].DialogueTextIdxs, Is.EqualTo(Enumerable.Range(8151,29).Select(x => (uint)x)));
        Assert.That(rows[0].Day, Is.EqualTo(1)); Assert.That(rows[1].Day, Is.Null);
        Assert.That(rows[1].MinStoreStage, Is.EqualTo(3));
        Assert.That(rows[2].Idx, Is.EqualTo(15003));
        Assert.That(rows[2].NameIdx, Is.EqualTo(8180));
        Assert.That(rows[2].Day, Is.EqualTo(2));
        Assert.That(rows[2].RequiredFacilityIdx, Is.Null);
        Assert.That(rows[2].MinStoreStage, Is.Null);
        Assert.That(rows[2].Priority, Is.Zero);
        Assert.That(rows[2].RepeatMode, Is.EqualTo(InspectorRepeatMode.OncePerSession));
        Assert.That(rows[2].DialogueTextIdxs, Is.EqualTo(new uint[] { 8181 }));
        Assert.That(rows.All(x => x.PortraitResourceIdx == 4201));
        Assert.That((uint)DataTableType.DataTableType_End, Is.EqualTo(18));

        var service = new InspectorEventService(rows);
        service.BeginDay(1, Array.Empty<uint>(), 1);
        Assert.That(service.Current.EventIdx, Is.EqualTo(15001));
        finish(service);
        Assert.That(service.HasPending, Is.False, "2일차 이벤트는 첫날에 등장하지 않는다.");
        service.BeginDay(2, Array.Empty<uint>(), 1);
        Assert.That(service.Current.EventIdx, Is.EqualTo(15003));
        Assert.That(service.Current.TextIdx, Is.EqualTo(8181));
        finish(service);
        service.BeginDay(2, Array.Empty<uint>(), 1);
        Assert.That(service.HasPending, Is.False);
        service.BeginDay(3, Array.Empty<uint>(), 1);
        Assert.That(service.HasPending, Is.False);
        var missedDay = new InspectorEventService(rows);
        missedDay.BeginDay(3, Array.Empty<uint>(), 1);
        Assert.That(missedDay.HasPending, Is.False, "미완료 2일차 이벤트도 다른 날짜로 이월하지 않는다.");
    }

    /// <summary>전체 조건 AND·priority/PK 정렬·완료 이력·중복 입력을 확인한다.</summary>
    [Test]
    public void SelectionAndProgressAreDeterministicAndIdempotent()
    {
        var a = row(15001); a.Day = 3; a.RequiredFacilityIdx = 12001; a.MinStoreStage = 2;
        var b = row(15002); b.Priority = -1;
        var service = new InspectorEventService(new[] { a, b });
        service.BeginDay(3, new uint[] { 12001 }, 2);
        Assert.That(service.Current.EventIdx, Is.EqualTo(15002));
        Assert.That(service.CompleteExit(3, 15002), Is.False);
        Assert.That(service.Advance(2,15002,0), Is.False);
        Assert.That(service.Advance(3,15002,0)); Assert.That(service.Advance(3,15002,0), Is.False);
        Assert.That(service.Advance(3,15002,1));
        Assert.That(service.Current.Phase, Is.EqualTo(InspectorEventPhase.AwaitingExit));
        Assert.Throws<InvalidOperationException>(() => service.BeginDay(4, new uint[0], 1));
        Assert.That(service.CompleteExit(3,15002)); Assert.That(service.CompleteExit(3,15002), Is.False);
        Assert.That(service.Current.EventIdx, Is.EqualTo(15001));
        finish(service);
        service.BeginDay(4, new uint[] { 12001 }, 3);
        Assert.That(service.HasPending, Is.False);
    }

    /// <summary>빈 선정은 당일 구매·UI 재진입으로 재평가하지 않고 익일부터 반영한다.</summary>
    [Test]
    public void EmptyDayIsFrozenAndNextDayUsesPriorPurchase()
    {
        var item = row(15001); item.RequiredFacilityIdx = 12001; item.MinStoreStage = 3;
        var service = new InspectorEventService(new[] { item });
        service.BeginDay(3, Array.Empty<uint>(), 2); Assert.That(service.HasPending, Is.False);
        service.BeginDay(3, new uint[] {12001}, 3); Assert.That(service.HasPending, Is.False);
        service.BeginDay(4, new uint[] {12001}, 3); Assert.That(service.HasPending);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.BeginDay(3, Array.Empty<uint>(), 1));
    }

    /// <summary>당일 조건과 설비 조건은 AND이며 날짜를 넘겼다고 미충족 일정을 보충하지 않는다.</summary>
    [Test]
    public void MissedExactDayDoesNotRunLater()
    {
        var item = row(15001); item.Day = 3; item.RequiredFacilityIdx = 12001;
        var service = new InspectorEventService(new[] { item });
        service.BeginDay(3, Array.Empty<uint>(), 1);
        service.BeginDay(4, new uint[] {12001}, 3); Assert.That(service.HasPending, Is.False);
    }

    /// <summary>매일 반복하는 같은 PK도 전날 콜백으로 다음날 대사·퇴장을 진행할 수 없다.</summary>
    [Test]
    public void OncePerDayRejectsPriorDayCallbacksAndCopiesData()
    {
        var item = row(15001); item.RepeatMode = InspectorRepeatMode.OncePerDay;
        var service = new InspectorEventService(new[] {item}); item.DialogueTextIdxs[0] = 8999;
        service.BeginDay(1, Array.Empty<uint>(), 1); Assert.That(service.Current.TextIdx, Is.EqualTo(8131)); finish(service);
        service.BeginDay(1, Array.Empty<uint>(), 1); Assert.That(service.HasPending, Is.False);
        service.BeginDay(2, Array.Empty<uint>(), 1); Assert.That(service.Advance(1,15001,0), Is.False);
        service.Advance(2,15001,0); service.Advance(2,15001,1);
        Assert.That(service.CompleteExit(1,15001), Is.False); Assert.That(service.CompleteExit(2,15001));
    }

    /// <summary>필수값·날짜·종료 enum·범위 오류를 승인값으로 보정하지 않는다.</summary>
    [Test]
    public void InvalidConditionsAndRepeatCodesAreRejected()
    {
        foreach (Action<InspectorEventData> mutation in new Action<InspectorEventData>[] {
            r => r.Day=0, r => r.MinStoreStage=0, r => r.MinStoreStage=4,
            r => r.RequiredFacilityIdx=0, r => r.RepeatMode=InspectorRepeatMode.InspectorRepeatMode_End,
            r => r.DialogueTextIdxs=Array.Empty<uint>(), r => r.PortraitResourceIdx=0 })
        { var item=row(15001); mutation(item); Assert.Throws<ArgumentException>(item.Validate); }
        var table = new InspectorEventDataTable();
        string csv = File.ReadAllText("Assets/Datas/InspectorEventData.csv").Replace(",0,1,8131", ",0,OncePerSession,8131");
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("InspectorEventData.csv"));
        Assert.Catch<Exception>(() => table.LoadData(csv));
        Assert.That(table.GetDataCount(), Is.Zero);
    }

    /// <summary>두 줄짜리 검증용 이벤트를 만든다.</summary>
    /// <param name="idx">테스트 PK.</param><returns>유효한 DTO.</returns>
    private static InspectorEventData row(uint idx) => new InspectorEventData { Idx=idx, NameIdx=8129,
        RepeatMode=InspectorRepeatMode.OncePerSession, DialogueTextIdxs=new uint[] {8131,8132}, PortraitResourceIdx=4201 };

    /// <summary>정상 대사·퇴장 API로 현재 이벤트 하나를 완료한다.</summary>
    /// <param name="service">검증 대상.</param>
    private static void finish(InspectorEventService service)
    {
        while(service.Current.Phase==InspectorEventPhase.Dialogue)
        { var s=service.Current; Assert.That(service.Advance(s.Day,s.EventIdx,s.LineIndex)); }
        var current=service.Current; Assert.That(service.CompleteExit(current.Day,current.EventIdx));
    }
}
