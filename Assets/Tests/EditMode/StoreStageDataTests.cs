using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>실제 단계 데이터의 범위와 검증 실패 시 공개 상태 보존을 검사한다.</summary>
public sealed class StoreStageDataTests
{
    /// <summary>시계 슬롯은 배치만 소유하고 다른 표시 슬롯의 Sprite 검증은 유지한다.</summary>
    [Test]
    public void FrontClockSlotKeepsLayoutWithoutOwningSprite()
    {
        var root = new GameObject("StageFront");
        var texture = new Texture2D(1, 1);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
        try
        {
            var visual = root.AddComponent<StoreStageVisual>();
            visual.region = StoreStageVisual.Region.Front;
            visual.images = Enumerable.Range(0, 8).Select(index =>
            {
                var image = new GameObject($"Slot{index}", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                    .GetComponent<UnityEngine.UI.Image>();
                image.transform.SetParent(root.transform);
                if (index != 6) image.sprite = sprite;
                return image;
            }).ToArray();
            visual.clockDigits = new GameObject("ClockDigits", typeof(RectTransform)).GetComponent<RectTransform>();
            visual.clockDigits.SetParent(visual.images[6].transform);

            Assert.DoesNotThrow(() => visual.Validate(StoreStageVisual.Region.Front));
            visual.images[0].sprite = null;
            Assert.Throws<InvalidOperationException>(() => visual.Validate(StoreStageVisual.Region.Front));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ActualStagesValidateFacilityAndResourceReferencesBeforePublication()
    {
        var table = new StoreStageDataTable();
        table.LoadData(File.ReadAllText("Assets/Datas/StoreStageData.csv"));
        var resources = new ResourceDataTable();
        LogAssert.Expect(LogType.Log, new Regex(@"^\[ResourceDataTable\]"));
        resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        var facilities = Util.ParseFromCSV<FacilityData>(File.ReadAllText("Assets/Datas/FacilityData.csv")).ToDictionary(row => row.Idx);
        table.Validate(resources, facilities);
        Assert.That(table.GetDataCount(), Is.Zero);
        typeof(StoreStageDataTable).GetMethod("Commit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(table, null);
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(table.GetStage(2).TopViewPrefabResourceIdx, Is.EqualTo(table.GetStage(3).TopViewPrefabResourceIdx));
        Assert.That(table.Rows.Values.OrderBy(row => row.StoreStage).Select(row => row.ClockResourceIdx),
            Is.EqualTo(new uint[] { 4300, 4301, 4302 }));
        table.LoadData(File.ReadAllText("Assets/Datas/StoreStageData.csv").Replace("4292", "4999"));
        Assert.Throws<InvalidDataException>(() => table.Validate(resources, facilities));
        Assert.That(table.GetStage(1).WorldPrefabResourceIdx, Is.EqualTo(4292), "실패한 재로딩은 공개 행을 바꾸지 않는다.");
        table.LoadData(File.ReadAllText("Assets/Datas/StoreStageData.csv").Replace(",4300", ",4999"));
        Assert.Throws<InvalidDataException>(() => table.Validate(resources, facilities));
        Assert.That(table.GetStage(1).ClockResourceIdx, Is.EqualTo(4300), "시계 FK 실패도 공개 행을 바꾸지 않는다.");
        table.LoadData(File.ReadAllText("Assets/Datas/StoreStageData.csv"));
        facilities.Values.First().RequiredStoreStage = 4;
        Assert.Throws<InvalidDataException>(() => table.Validate(resources, facilities));
    }

    [TestCase("duplicate"), TestCase("missing-first"), TestCase("range"), TestCase("zero-fk"), TestCase("header"), TestCase("clock-header")]
    public void InvalidStageTablesAreRejected(string error)
    {
        string csv = File.ReadAllText("Assets/Datas/StoreStageData.csv");
        if (error == "duplicate") csv = csv.Replace("19002,2", "19002,1");
        if (error == "missing-first") csv = string.Join("\n", csv.Split('\n').Where(line => !line.StartsWith("19001,")));
        if (error == "range") csv = csv.Replace("19001,1", "19001,0");
        if (error == "zero-fk") csv = csv.Replace("4292", "0");
        if (error == "header") csv = csv.Replace("world_prefab_resource_idx", "wrong_column");
        if (error == "clock-header") csv = csv.Replace("clock_resource_idx", "wrong_clock_column");
        var table = new StoreStageDataTable();
        LogAssert.Expect(LogType.Error, new Regex("StoreStageData.csv"));
        Assert.Catch(() => table.LoadData(csv));
        Assert.That(table.GetDataCount(), Is.Zero);
    }
}
