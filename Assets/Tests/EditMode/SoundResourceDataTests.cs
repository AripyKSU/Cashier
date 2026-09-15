using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 사운드 ResourceData ID와 address 매핑의 정적 계약을 검사한다.
/// </summary>
public sealed class SoundResourceDataTests
{
    /// <summary>
    /// 사운드 키 20개가 Resource 대역에서 고유하게 정의되었는지 확인한다.
    /// </summary>
    [Test]
    public void SoundKeysAreUniqueResourceIds()
    {
        Assert.That(SoundKeys.All, Is.Not.Null);
        Assert.That(SoundKeys.All.Count, Is.EqualTo(20));
        Assert.That(SoundKeys.All.Distinct().Count(), Is.EqualTo(20));
        foreach (uint resourceIdx in SoundKeys.All)
        {
            Assert.That(resourceIdx, Is.GreaterThan(4000u));
            Assert.That(resourceIdx, Is.LessThan(5000u));
        }
    }

    /// <summary>
    /// 실제 ResourceData.csv의 20개 사운드 행이 각 address를 보존하는지 확인한다.
    /// </summary>
    [Test]
    public void ResourceDataContainsAllSoundAddresses()
    {
        var expected = new Dictionary<uint, string>
        {
            [SoundKeys.SupervisorBgm] = "SupervisorBgm",
            [SoundKeys.GameplayAmbience] = "GameplayAmbience",
            [SoundKeys.SettlementBgm] = "SettlementBgm",
            [SoundKeys.TransactionSuccess] = "TransactionSuccess",
            [SoundKeys.TransactionFail] = "TransactionFail",
            [SoundKeys.CalculatorOpen] = "CalculatorOpen",
            [SoundKeys.CalculatorButton] = "CalculatorButton",
            [SoundKeys.ItemPickup] = "ItemPickup",
            [SoundKeys.ItemPlace] = "ItemPlace",
            [SoundKeys.ItemRemove] = "ItemRemove",
            [SoundKeys.BoxItemDrop] = "BoxItemDrop",
            [SoundKeys.CustomerBoxDrop] = "CustomerBoxDrop",
            [SoundKeys.Vacuum] = "VacuumLoop",
            [SoundKeys.FacilityUpgrade] = "FacilityUpgrade",
            [SoundKeys.DailyGuideline] = "DailyGuideline",
            [SoundKeys.DayStart] = "DayStart",
            [SoundKeys.DayEnd] = "DayEnd",
            [SoundKeys.ReputationStamp] = "ReputationStamp",
            [SoundKeys.LedgerWrite] = "LedgerWriteLoop",
            [SoundKeys.DialogueVoice] = "DialogueVoice"
        };

        var table = new ResourceDataTable();
        LogAssert.Expect(
            LogType.Log,
            new Regex(@"^\[ResourceDataTable\] 총 92개의 리소스 경로 데이터 로드 완료\."));
        table.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));

        Assert.That(expected.Keys, Is.EquivalentTo(SoundKeys.All));
        foreach (var pair in expected)
        {
            Assert.That(table.TryGetResource(pair.Key, out ResourceData resource), Is.True);
            Assert.That(resource.Path, Is.EqualTo(pair.Value));
        }
    }
}
