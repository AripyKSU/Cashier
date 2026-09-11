using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>딸 대사 CSV 구간·FK와 선택 경계를 검사한다.</summary>
public sealed class DaughterDialogueTests
{
    /// <summary>승인된 실제 CSV의 6구간·18대사·3이미지와 FK를 검사한다.</summary>
    [Test]
    public void ActualCsvLoadsAllApprovedRowsAndReferences()
    {
        DaughterDialogueDataTable dialogues = loadDialogues(File.ReadAllText("Assets/Datas/DaughterDialogueData.csv"));
        DaughterAppearanceDataTable appearances = loadAppearances(File.ReadAllText("Assets/Datas/DaughterAppearanceData.csv"));
        TextDataTable texts = new TextDataTable();
        texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));
        ResourceDataTable resources = new ResourceDataTable();
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("^\\[ResourceDataTable\\]"));
        resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        Assert.DoesNotThrow(() => dialogues.Validate(pending<TextDataTable, TextData>(texts)));
        Assert.DoesNotThrow(() => appearances.Validate(resources));
        Assert.That(pending<DaughterDialogueDataTable, DaughterDialogueData>(dialogues).Count, Is.EqualTo(6));
        Assert.That(pending<DaughterDialogueDataTable, DaughterDialogueData>(dialogues).Values.SelectMany(row => row.TextIdxs),
            Is.EquivalentTo(Enumerable.Range(8183, 18).Select(value => (uint)value)));
        Assert.That(pending<DaughterAppearanceDataTable, DaughterAppearanceData>(appearances).Values.OrderBy(row => row.StartDay)
            .Select(row => (row.StartDay, row.ResourceIdx)), Is.EqualTo(new[] { (1u, 4201u), (11u, 4201u), (21u, 4201u) }));
        var panel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab");
        var font = panel.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).font;
        foreach (uint textIdx in pending<DaughterDialogueDataTable, DaughterDialogueData>(dialogues).Values.SelectMany(row => row.TextIdxs))
            Assert.That(pending<TextDataTable, TextData>(texts)[textIdx].Text.All(character => font.HasCharacter(character)),
                Is.True, $"딸 대사 TextData={textIdx}를 표시할 글리프가 없습니다.");
    }

    /// <summary>양끝·정확한 경계·소수점에서 min 포함/max 미포함과 0의 긍정 구간을 검사한다.</summary>
    [Test]
    public void SelectionUsesInclusiveMinExclusiveMaxAndInfiniteTails()
    {
        DaughterDialogueService service = actualService(1);
        foreach (var sample in new[] {
            (decimal.MinValue, 16001u), (-120.0001m, 16001u), (-120m, 16002u), (-119.9999m, 16002u),
            (-40.0001m, 16002u), (-40m, 16003u), (-39.9999m, 16003u), (-0.0001m, 16003u),
            (0m, 16004u), (0.0001m, 16004u), (39.9999m, 16004u), (40m, 16005u),
            (40.0001m, 16005u), (119.9999m, 16005u), (120m, 16006u), (120.0001m, 16006u),
            (decimal.MaxValue, 16006u) })
            Assert.That(service.Select(1, sample.Item1).DialogueIdx, Is.EqualTo(sample.Item2));
    }

    /// <summary>여섯 구간의 후보 3개가 모두 도달하고 날짜 꼬리가 유지되는지 검사한다.</summary>
    [Test]
    public void EveryCandidateAndAppearancePeriodIsReachable()
    {
        foreach (var sample in new[] {
            (-121m, new[] { 8183u, 8184u, 8185u }), (-41m, new[] { 8186u, 8187u, 8188u }),
            (-1m, new[] { 8189u, 8190u, 8191u }), (1m, new[] { 8192u, 8193u, 8194u }),
            (41m, new[] { 8195u, 8196u, 8197u }), (121m, new[] { 8198u, 8199u, 8200u }) })
        {
            var selected = Enumerable.Range(0, 100).Select(seed => actualService(seed).Select(1, sample.Item1).TextIdx).Distinct();
            Assert.That(selected, Is.EquivalentTo(sample.Item2));
        }
        DaughterDialogueDataTable dialogues = loadDialogues(File.ReadAllText("Assets/Datas/DaughterDialogueData.csv"));
        var periodService = new DaughterDialogueService(
            pending<DaughterDialogueDataTable, DaughterDialogueData>(dialogues).Values,
            new[] {
                new DaughterAppearanceData { Idx = 17001, StartDay = 1, ResourceIdx = 4201 },
                new DaughterAppearanceData { Idx = 17002, StartDay = 11, ResourceIdx = 4202 },
                new DaughterAppearanceData { Idx = 17003, StartDay = 21, ResourceIdx = 4203 }
            }, new System.Random(1));
        foreach (var sample in new[] { (1u, 4201u), (10u, 4201u), (11u, 4202u),
            (20u, 4202u), (21u, 4203u), (uint.MaxValue, 4203u) })
            Assert.That(periodService.Select(sample.Item1, 0).ResourceIdx, Is.EqualTo(sample.Item2));
    }

    /// <summary>빈 후보·중복 후보·누락 구간·양끝 유한·0 경계 누락을 거부한다.</summary>
    [Test]
    public void InvalidDialogueTablesAreRejected()
    {
        foreach (string csv in new[] {
            "idx,morality_min,morality_max,text_idxs\n16001,,,",
            "idx,morality_min,morality_max,text_idxs\n16001,,,8183_8183",
            "idx,morality_min,morality_max,text_idxs\n16001,,-1,8183\n16002,0,,8184",
            "idx,morality_min,morality_max,text_idxs\n16001,-20,0,8183\n16002,0,,8184",
            "idx,morality_min,morality_max,text_idxs\n16001,,1,8183\n16002,1,,8184" })
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^DaughterDialogueData.csv"));
            Assert.Catch<Exception>(() => new DaughterDialogueDataTable().LoadData(csv));
        }
        DaughterDialogueDataTable table = loadDialogues(
            File.ReadAllText("Assets/Datas/DaughterDialogueData.csv").Replace("8183_8184_8185", "8999_8184_8185"));
        Assert.Throws<InvalidDataException>(() => table.Validate(new Dictionary<uint, TextData>()));
    }

    /// <summary>이미지 첫날·양수·고유 일차와 Resource FK를 강제한다.</summary>
    [Test]
    public void InvalidAppearanceTablesAreRejected()
    {
        foreach (string csv in new[] {
            "idx,start_day,resource_idx\n17001,2,4201",
            "idx,start_day,resource_idx\n17001,1,4201\n17002,1,4201",
            "idx,start_day,resource_idx\n17001,0,4201" })
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^DaughterAppearanceData.csv"));
            Assert.Catch<Exception>(() => new DaughterAppearanceDataTable().LoadData(csv));
        }
        DaughterAppearanceDataTable table = loadAppearances("idx,start_day,resource_idx\n17001,1,4999");
        ResourceDataTable resources = new ResourceDataTable();
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("^\\[ResourceDataTable\\]"));
        resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        Assert.Throws<InvalidDataException>(() => table.Validate(resources));
    }

    private static DaughterDialogueService actualService(int seed)
    {
        DaughterDialogueDataTable dialogues = loadDialogues(File.ReadAllText("Assets/Datas/DaughterDialogueData.csv"));
        DaughterAppearanceDataTable appearances = loadAppearances(File.ReadAllText("Assets/Datas/DaughterAppearanceData.csv"));
        return new DaughterDialogueService(pending<DaughterDialogueDataTable, DaughterDialogueData>(dialogues).Values,
            pending<DaughterAppearanceDataTable, DaughterAppearanceData>(appearances).Values, new System.Random(seed));
    }

    private static DaughterDialogueDataTable loadDialogues(string csv)
    {
        var table = new DaughterDialogueDataTable();
        table.LoadData(csv);
        return table;
    }

    private static DaughterAppearanceDataTable loadAppearances(string csv)
    {
        var table = new DaughterAppearanceDataTable();
        table.LoadData(csv);
        return table;
    }

    private static Dictionary<uint, TRow> pending<TTable, TRow>(TTable table) =>
        (Dictionary<uint, TRow>)typeof(TTable).GetProperty("PendingRows",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(table);
}
