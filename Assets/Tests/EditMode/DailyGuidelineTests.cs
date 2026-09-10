using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>일일지침 규칙 설정 CSV와 런타임 계약의 최소 검증.</summary>
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
            Assert.That(row.PenaltyAmount, Is.EqualTo(500));
        }
    }

    [Test]
    public void DailyGuidelineData_ProvidesBothRuleTypes()
    {
        Assert.That(guidelineTable.TryGetByRuleType(DailyGuidelineRuleType.SaleProhibited, out DailyGuidelineData prohibited), Is.True);
        Assert.That(prohibited.AllowedQuantity, Is.Zero);
        Assert.That(guidelineTable.TryGetByRuleType(DailyGuidelineRuleType.QuantityLimited, out DailyGuidelineData limited), Is.True);
        Assert.That(limited.AllowedQuantity, Is.EqualTo(1));
    }

    [Test]
    public void DailyGuidelineData_CreatesValidatedRuntimeGuideline()
    {
        Assert.That(guidelineTable.TryGetByRuleType(DailyGuidelineRuleType.QuantityLimited, out DailyGuidelineData data), Is.True);
        DailyGuideline guideline = data.CreateGuideline(CustomerAttributes.Female | CustomerAttributes.Adult, 1001);
        Assert.That(guideline.AllowedQuantity, Is.EqualTo(1));
        Assert.That(guideline.PenaltyAmount, Is.EqualTo(500));
    }
}
