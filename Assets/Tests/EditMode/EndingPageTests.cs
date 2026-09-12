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
    /// <summary>각 엔딩 네 페이지와 세 줄 대사가 참조되는지 확인한다.</summary>
    [Test]
    public void ActualPagesHaveBothEndingsAndThreeLineDialogue()
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
        Assert.That(pages.Rows.Count, Is.EqualTo(8));
        foreach (var group in pages.Rows.Values.GroupBy(p => p.Kind))
        {
            Assert.That(group.Count(), Is.EqualTo(4));
            foreach (var page in group) Assert.That(texts[page.TextIdx].Text.Split('\n').Length, Is.EqualTo(3));
        }
        texts.Remove(8240);
        pages.LoadData(File.ReadAllText("Assets/Datas/EndingPageData.csv"));
        Assert.Throws<InvalidDataException>(() => pages.Validate(texts, resources));
        Assert.That(pages.Rows.Count, Is.EqualTo(8), "검증 실패 전 공개 데이터 보존");
    }

    /// <summary>종류·순서·PK·필수 헤더 오류를 숨기지 않는다.</summary>
    [TestCase("kind"), TestCase("gap"), TestCase("duplicate"), TestCase("missing-ending"), TestCase("header")]
    public void InvalidPageTablesAreRejected(string kind)
    {
        string csv = File.ReadAllText("Assets/Datas/EndingPageData.csv");
        if (kind == "kind") csv = csv.Replace("18001,2,1", "18001,Good,1");
        if (kind == "gap") csv = csv.Replace("18001,2,1", "18001,2,5");
        if (kind == "duplicate") csv = csv.Replace("18002,", "18001,");
        if (kind == "missing-ending") csv = csv.Replace(",3,", ",2,");
        if (kind == "header") csv = csv.Replace("background_resource_idx", "missing_background");
        var table = new EndingPageDataTable();
        LogAssert.Expect(LogType.Error, new Regex("EndingPageData.csv"));
        Assert.Catch(() => table.LoadData(csv));
        Assert.That(table.GetDataCount(), Is.Zero);
    }
}
