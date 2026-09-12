using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>감독관 조건·날짜 캐시·대사 입력·완료 이력 및 실제 CSV 형식을 검사한다.</summary>
public sealed class InspectorEventTests
{
    /// <summary>실제 세 이벤트의 1·10·21일 페이지와 다중행 Text 표시 계약을 검사한다.</summary>
    [Test]
    public void ActualCsvHasThreeScheduledEventsAndMultilinePages()
    {
        var table = new InspectorEventDataTable();
        table.LoadData(File.ReadAllText("Assets/Datas/InspectorEventData.csv"));
        var pending = (System.Collections.Generic.Dictionary<uint, InspectorEventData>)typeof(InspectorEventDataTable)
            .GetProperty("PendingRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(table);
        var rows = pending.Values.OrderBy(x => x.Idx).ToArray();
        Assert.That(rows.Length, Is.EqualTo(3));
        foreach (var row in rows) Assert.DoesNotThrow(row.Validate);
        Assert.That(rows[0].DialogueTextIdxs, Is.EqualTo(new uint[] { 8131, 8132, 8133, 8134 }));
        Assert.That(rows[1].DialogueTextIdxs, Is.EqualTo(new uint[] { 8151, 8152, 8153 }));
        Assert.That(rows[0].Day, Is.EqualTo(1)); Assert.That(rows[1].Day, Is.EqualTo(21));
        Assert.That(rows[2].Idx, Is.EqualTo(15003));
        Assert.That(rows[2].NameIdx, Is.EqualTo(8180));
        Assert.That(rows[2].Day, Is.EqualTo(10));
        Assert.That(rows[2].RequiredFacilityIdx, Is.Null);
        Assert.That(rows[2].MinStoreStage, Is.Null);
        Assert.That(rows[2].Priority, Is.Zero);
        Assert.That(rows[2].RepeatMode, Is.EqualTo(InspectorRepeatMode.OncePerSession));
        Assert.That(rows[2].DialogueTextIdxs, Is.EqualTo(new uint[] { 8181, 8135 }));
        Assert.That(rows.All(x => !x.RequiredFacilityIdx.HasValue && !x.MinStoreStage.HasValue &&
            x.Priority == 0 && x.RepeatMode == InspectorRepeatMode.OncePerSession), Is.True);
        Assert.That(rows.All(x => x.PortraitResourceIdx == 4256));
        Assert.That((uint)DataTableType.DataTableType_End, Is.EqualTo(19));

        var texts = new TextDataTable();
        texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));
        var textRows = (System.Collections.Generic.Dictionary<uint, TextData>)typeof(TextDataTable)
            .GetProperty("PendingRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(texts);
        Assert.That(textRows.Count, Is.EqualTo(242), "따옴표 안 실제 개행은 한 CSV 레코드로 파싱되어야 합니다.");
        uint[] pageIds = rows.SelectMany(x => x.DialogueTextIdxs).ToArray();
        Assert.That(pageIds.Length, Is.EqualTo(9));
        Assert.That(rows.All(x => textRows.ContainsKey(x.NameIdx)) && pageIds.All(textRows.ContainsKey), Is.True);
        var panel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab");
        var dialogue = panel.transform.Find("Dialogue/Text").GetComponent<TMPro.TextMeshProUGUI>();
        foreach (uint pageId in pageIds)
        {
            string text = textRows[pageId].Text.Replace("\r\n", "\n");
            Assert.That(text.Count(character => character == '\n'), Is.EqualTo(2), $"TextData={pageId}");
            Assert.That(text.Where(character => character != '\n').All(character => dialogue.font.HasCharacter(character)), Is.True, $"TextData={pageId} glyph");
            Vector2 unwrapped = dialogue.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity);
            foreach (var bounds in new[] { new Vector2(571, 124), new Vector2(537, 116) })
            {
                Vector2 preferred = dialogue.GetPreferredValues(text, bounds.x, float.PositiveInfinity);
                Assert.That(unwrapped.x, Is.LessThanOrEqualTo(bounds.x), $"TextData={pageId} unwrapped width={bounds.x}");
                Assert.That(preferred.y, Is.EqualTo(unwrapped.y).Within(.01f), $"TextData={pageId} must keep three lines");
                Assert.That(preferred.y, Is.LessThanOrEqualTo(bounds.y), $"TextData={pageId} height={bounds.y}");
            }
        }

        var service = new InspectorEventService(rows);
        service.BeginDay(1, Array.Empty<uint>(), 1);
        Assert.That(service.Current.EventIdx, Is.EqualTo(15001));
        finish(service);
        service.BeginDay(2, new uint[] { 12001, 12008, 12010 }, 3);
        Assert.That(service.HasPending, Is.False, "조기 설비·단계 구매는 정확한 공개일을 앞당기지 않는다.");
        service.BeginDay(10, new uint[] { 12001, 12008, 12010 }, 3);
        Assert.That(service.Current.EventIdx, Is.EqualTo(15003)); Assert.That(service.Current.TextIdx, Is.EqualTo(8181));
        finish(service);
        service.BeginDay(10, Array.Empty<uint>(), 1);
        Assert.That(service.HasPending, Is.False);
        service.BeginDay(20, Array.Empty<uint>(), 1); Assert.That(service.HasPending, Is.False);
        service.BeginDay(21, Array.Empty<uint>(), 1); Assert.That(service.Current.EventIdx, Is.EqualTo(15002));
        finish(service); service.BeginDay(22, Array.Empty<uint>(), 1); Assert.That(service.HasPending, Is.False);
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
