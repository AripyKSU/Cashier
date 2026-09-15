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
        table.LoadData(File.ReadAllText("Assets/Datas/StoreStageData.csv").Replace("4292", "4999"));
        Assert.Throws<InvalidDataException>(() => table.Validate(resources, facilities));
        Assert.That(table.GetStage(1).WorldPrefabResourceIdx, Is.EqualTo(4292), "실패한 재로딩은 공개 행을 바꾸지 않는다.");
        table.LoadData(File.ReadAllText("Assets/Datas/StoreStageData.csv"));
        facilities.Values.First().RequiredStoreStage = 4;
        Assert.Throws<InvalidDataException>(() => table.Validate(resources, facilities));
    }

    [TestCase("duplicate"), TestCase("missing-first"), TestCase("range"), TestCase("zero-fk"), TestCase("header")]
    public void InvalidStageTablesAreRejected(string error)
    {
        string csv = File.ReadAllText("Assets/Datas/StoreStageData.csv");
        if (error == "duplicate") csv = csv.Replace("19002,2", "19002,1");
        if (error == "missing-first") csv = string.Join("\n", csv.Split('\n').Where(line => !line.StartsWith("19001,")));
        if (error == "range") csv = csv.Replace("19001,1", "19001,0");
        if (error == "zero-fk") csv = csv.Replace("4292", "0");
        if (error == "header") csv = csv.Replace("world_prefab_resource_idx", "wrong_column");
        var table = new StoreStageDataTable();
        LogAssert.Expect(LogType.Error, new Regex("StoreStageData.csv"));
        Assert.Catch(() => table.LoadData(csv));
        Assert.That(table.GetDataCount(), Is.Zero);
    }
}
