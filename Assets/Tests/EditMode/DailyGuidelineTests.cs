using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>당일 지침(Daily Guidelines) CSV 파싱, 유효성 및 UI 뷰데이터 팩토리 연동 테스트.</summary>
public sealed class DailyGuidelineTests
{
    private DailyGuidelineDataTable guidelineTable;
    private TextDataTable textTable;
    private CustomerCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        string guidelineCsv = File.ReadAllText("Assets/Datas/DailyGuidelineData.csv");
        string textCsv = File.ReadAllText("Assets/Datas/TextData.csv");
        string productCsv = File.ReadAllText("Assets/Datas/Customer/ProductData.csv");
        string customerRoot = "Assets/Datas/Customer/";

        guidelineTable = new DailyGuidelineDataTable();
        guidelineTable.LoadData(guidelineCsv);

        textTable = new TextDataTable();
        textTable.LoadData(textCsv);

        var productTable = new ProductDataTable();
        productTable.LoadData(productCsv);

        catalog = new CustomerCatalog(
            new CustomerAppearanceDataTable(),
            new CustomerDispositionDataTable(),
            new ProductCategoryDataTable(),
            productTable);
        catalog.Appearances.LoadData(File.ReadAllText(customerRoot + "CustomerAppearanceData.csv"));
        catalog.Dispositions.LoadData(File.ReadAllText(customerRoot + "CustomerDispositionData.csv"));
        catalog.Categories.LoadData(File.ReadAllText(customerRoot + "ProductCategoryData.csv"));
        var facilityTable = new FacilityDataTable();
        facilityTable.LoadData(File.ReadAllText("Assets/Datas/FacilityData.csv"));
        var resources = new ResourceDataTable();
        resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        catalog.ValidateAndCommit(textTable, resources, facilities: facilityTable);
        guidelineTable.Validate(textTable.Rows, productTable.Rows);
        typeof(DailyGuidelineDataTable).GetMethod("Commit", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(guidelineTable, null);
    }

    [Test]
    public void DailyGuidelineData_LoadsAndValidates_Successfully()
    {
        Assert.That(guidelineTable.GetDataCount(), Is.GreaterThan(0));

        foreach (DailyGuidelineData row in guidelineTable.Rows.Values)
        {
            Assert.DoesNotThrow(row.Validate);
            Assert.That(Util.GetDataTableType(row.Idx), Is.EqualTo(DataTableType.DailyGuideline));
            Assert.That(textTable.Rows.ContainsKey(row.NameIdx), $"DailyGuideline PK={row.Idx}: nameidx={row.NameIdx} not found in TextData");
            Assert.That(textTable.Rows.ContainsKey(row.DescriptionIdx), $"DailyGuideline PK={row.Idx}: descriptionidx={row.DescriptionIdx} not found in TextData");
        }
    }

    [Test]
    public void DailyGuidelineData_Day1_ReturnsNoRestriction()
    {
        bool found = guidelineTable.TryGetByDay(1, out DailyGuidelineData day1Data);
        Assert.That(found, Is.True);
        Assert.That(day1Data.NameIdx, Is.EqualTo(8101u));
        Assert.That(day1Data.DescriptionIdx, Is.EqualTo(8102u));

        Assert.That(textTable.Rows[day1Data.NameIdx].Text, Is.EqualTo("오늘의 지침"));
        Assert.That(textTable.Rows[day1Data.DescriptionIdx].Text, Is.EqualTo("제한 없음."));
    }

    [Test]
    public void ProgressViewDataFactory_BindsGuidelineText_ForDay1()
    {
        var factory = new ProgressViewDataFactory(
            catalog,
            textTable,
            new Dictionary<uint, Sprite>(),
            guidelineTable);

        var scheduler = new PriceEventScheduler(new System.Random(1));
        var events = Util.ParseFromCSV<PriceEventData>(File.ReadAllText("Assets/Datas/PriceEventData.csv")).ToDictionary(x => x.Idx);
        var schedules = Util.ParseFromCSV<PriceEventScheduleData>(File.ReadAllText("Assets/Datas/PriceEventScheduleData.csv")).ToDictionary(x => x.Idx);
        var prices = scheduler.CreateDay(0, events, schedules, catalog.Products.Rows);
        PreOpenGuidelineViewData viewData = factory.CreatePreOpenGuidelineViewData(1, prices);

        Assert.That(viewData.Day, Is.EqualTo(1));
        Assert.That(viewData.RuleTitle, Is.EqualTo("오늘의 지침"));
        Assert.That(viewData.RuleContent, Is.EqualTo("제한 없음."));
        Assert.That(viewData.Products.Count, Is.EqualTo(4));
        Assert.That(viewData.Products.All(x => x.Price == prices.Prices[x.ProductIdx]));
        Assert.That(viewData.Products.Single(x => x.ProductIdx == 1004).Price, Is.EqualTo(200));
        Assert.Throws<System.InvalidOperationException>(() => factory.CreatePreOpenGuidelineViewData(2, prices));
    }

    /// <summary>잘못된 지침 FK는 공개 전에 거부하며 기존 공개 데이터는 유지한다.</summary>
    [Test]
    public void DailyGuidelineRejectsMissingForeignKeysBeforeCommit()
    {
        var invalid = new DailyGuidelineDataTable();
        string csv = File.ReadAllText("Assets/Datas/DailyGuidelineData.csv");
        invalid.LoadData(csv.Replace("8101", "8999"));
        Assert.Throws<InvalidDataException>(() => invalid.Validate(textTable.Rows, catalog.Products.Rows));
        Assert.That(invalid.GetDataCount(), Is.Zero);
        invalid.LoadData(csv.Replace(",1001,1", ",1999,1"));
        Assert.Throws<InvalidDataException>(() => invalid.Validate(textTable.Rows, catalog.Products.Rows));
        Assert.That(invalid.GetDataCount(), Is.Zero);
    }
}
