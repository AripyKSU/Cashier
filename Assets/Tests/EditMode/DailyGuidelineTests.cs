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
        catalog.ValidateAndCommit(textTable, facilities: facilityTable);
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

        PreOpenGuidelineViewData viewData = factory.CreatePreOpenGuidelineViewData(1);

        Assert.That(viewData.Day, Is.EqualTo(1));
        Assert.That(viewData.RuleTitle, Is.EqualTo("오늘의 지침"));
        Assert.That(viewData.RuleContent, Is.EqualTo("제한 없음."));
    }
}
