using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>실제 엔딩 CSV의 순서·숫자 코드·문구·리소스 참조를 검사한다.</summary>
public sealed class EndingPageTests
{
    [Test]
    public void EndingKindsSelectExpectedBgm()
    {
        var selector = typeof(EndingPresenter).GetMethod("GetEndingBgmResourceIdx",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.That(selector, Is.Not.Null);
        Assert.That((uint)selector.Invoke(null, new object[] { EndingKind.Good }), Is.EqualTo(SoundKeys.GoodEndingBgm));
        foreach (EndingKind kind in new[] { EndingKind.GameOver, EndingKind.Bad, EndingKind.CitizenshipNegative })
            Assert.That((uint)selector.Invoke(null, new object[] { kind }), Is.EqualTo(SoundKeys.BadEndingBgm));
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => selector.Invoke(null, new object[] { EndingKind.None }));
        Assert.That(exception.InnerException, Is.TypeOf<System.ArgumentOutOfRangeException>());
    }

    /// <summary>시민권 엔딩은 보유와 도덕성 부호가 모순된 snapshot을 거부한다.</summary>
    [Test]
    public void EndingResultRejectsContradictoryCitizenshipMorality()
    {
        Assert.DoesNotThrow(() => new GameEndingResult(EndingKind.Good, 1, true, 0, 0, 0m));
        Assert.DoesNotThrow(() => new GameEndingResult(EndingKind.CitizenshipNegative, 1, true, 0, 0, -1m));
        Assert.Throws<ArgumentException>(() => new GameEndingResult(EndingKind.Good, 1, true, 0, 0, -1m));
        Assert.Throws<ArgumentException>(() => new GameEndingResult(EndingKind.CitizenshipNegative, 1, true, 0, 0, 0m));
        Assert.Throws<ArgumentException>(() => new GameEndingResult(EndingKind.CitizenshipNegative, 1, false, 0, 0, -1m));
    }

    /// <summary>실제 네 엔딩의 페이지·침묵 컷·마지막 검은 화면과 FK를 확인한다.</summary>
    [Test]
    public void ActualPagesHaveFourEndingsSilenceAndFinalBlackPage()
    {
        var pages = new EndingPageDataTable();
        pages.LoadData(File.ReadAllText("Assets/Datas/EndingPageData.csv"));
        var texts = Util.ParseFromCSV<TextData>(File.ReadAllText("Assets/Datas/TextData.csv")).ToDictionary(t => t.Idx);
        var resources = new ResourceDataTable();
        LogAssert.Expect(LogType.Log, new Regex(@"^\[ResourceDataTable\]"));
        resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        pages.Validate(texts, resources);
        Assert.That(pages.GetDataCount(), Is.Zero);
        typeof(EndingPageDataTable).GetMethod("Commit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(pages, null);
        Assert.That(pages.Rows.Count, Is.EqualTo(36));
        foreach (var group in pages.Rows.Values.GroupBy(p => p.Kind))
        {
            int expected = group.Key switch { EndingKind.Good or EndingKind.CitizenshipNegative => 8, EndingKind.Bad => 11, _ => 9 };
            Assert.That(group.Count(), Is.EqualTo(expected));
            var ordered = group.OrderBy(page => page.PageOrder).ToArray();
            Assert.That(ordered[^1].BackgroundResourceIdx, Is.Null);
            Assert.That(ordered[^1].TextIdx, Is.Not.Null);
            Assert.That(ordered[^1].SpeakerNameIdx, Is.Null);
            int silentCount = ordered.Take(ordered.Length - 1).Count(page => !page.TextIdx.HasValue);
            int expectedSilent = group.Key == EndingKind.Bad ? 3 : group.Key == EndingKind.CitizenshipNegative ? 1 : 0;
            Assert.That(silentCount, Is.EqualTo(expectedSilent));
            Assert.That(ordered.Count(page => !page.TextIdx.HasValue && page.BackgroundResourceIdx.HasValue),
                Is.EqualTo(group.Key == EndingKind.Bad || group.Key == EndingKind.CitizenshipNegative ? 1 : 0));
            Assert.That(ordered.Count(page => !page.TextIdx.HasValue && page.SfxResourceIdx.HasValue),
                Is.EqualTo(group.Key == EndingKind.Bad ? 2 : 0));
        }
        uint referencedText = pages.Rows.Values.Select(page => page.TextIdx).First(value => value.HasValue).Value;
        texts.Remove(referencedText);
        pages.LoadData(File.ReadAllText("Assets/Datas/EndingPageData.csv"));
        Assert.Throws<InvalidDataException>(() => pages.Validate(texts, resources));
        Assert.That(pages.Rows.Count, Is.EqualTo(36), "검증 실패 전 공개 데이터 보존");
        var resourcesWithoutSfx = new ResourceDataTable();
        LogAssert.Expect(LogType.Log, new Regex(@"^\[ResourceDataTable\]"));
        resourcesWithoutSfx.LoadData(string.Join("\n", File.ReadAllLines("Assets/Datas/ResourceData.csv")
            .Where(line => !line.StartsWith("4263,"))));
        pages.LoadData(File.ReadAllText("Assets/Datas/EndingPageData.csv"));
        Assert.Throws<InvalidDataException>(() => pages.Validate(Util.ParseFromCSV<TextData>(
            File.ReadAllText("Assets/Datas/TextData.csv")).ToDictionary(t => t.Idx), resourcesWithoutSfx));
        Assert.That(pages.Rows.Count, Is.EqualTo(36));
    }

    /// <summary>종류·순서·PK·필수 헤더 오류를 숨기지 않는다.</summary>
    [TestCase("kind"), TestCase("gap"), TestCase("duplicate"), TestCase("missing-ending"), TestCase("header")]
    [TestCase("zero"), TestCase("sfx-zero"), TestCase("all-empty"), TestCase("final-background")]
    [TestCase("final-text-missing"), TestCase("silent-speaker")]
    public void InvalidPageTablesAreRejected(string kind)
    {
        string csv = File.ReadAllText("Assets/Datas/EndingPageData.csv");
        if (kind == "kind") csv = csv.Replace("18001,2,1", "18001,Good,1");
        if (kind == "gap") csv = csv.Replace("18001,2,1", "18001,2,5");
        if (kind == "duplicate") csv = csv.Replace("18002,", "18001,");
        if (kind == "missing-ending") csv = csv.Replace(",3,", ",2,");
        if (kind == "header") csv = csv.Replace("background_resource_idx", "missing_background");
        if (kind == "zero") csv = csv.Replace("18001,2,1,8481", "18001,2,1,0");
        if (kind == "sfx-zero") csv = csv.Replace("18035,3,9,,,,4263", "18035,3,9,,,,0");
        if (kind == "all-empty") csv = csv.Replace("18035,3,9,,,,4263", "18035,3,9,,,,");
        if (kind == "final-background") csv = csv.Replace("18012,2,8,8488,,,", "18012,2,8,8488,,4397,");
        if (kind == "final-text-missing") csv = csv.Replace("18012,2,8,8488,,,", "18012,2,8,,,,");
        if (kind == "silent-speaker") csv = csv.Replace("18019,4,7,,,4402,", "18019,4,7,,8478,4402,");
        var table = new EndingPageDataTable();
        LogAssert.Expect(LogType.Error, new Regex("EndingPageData.csv"));
        Assert.Catch(() => table.LoadData(csv));
        Assert.That(table.GetDataCount(), Is.Zero);
    }
}
