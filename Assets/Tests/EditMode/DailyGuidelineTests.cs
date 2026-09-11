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

    /// <summary>생성 대상이 모든 손님 또는 성별·연령 단일 속성으로만 구성되고 모든 손님이 우세한지 검사합니다.</summary>
    [Test]
    public void DailyGuidelineGenerator_PrefersAllCustomersWithoutCombinedAttributes()
    {
        var generator = new DailyGuidelineGenerator(new System.Random(7));
        uint[] productIds = { 1001, 1004, 1005, 1006 };
        int allCustomerCount = 0;
        int singleAttributeCount = 0;

        for (int index = 0; index < 1000; index++)
        {
            DailyGuideline guideline = generator.Generate(9, guidelineTable.Rows, productIds)[0];
            if (guideline.RequiredAttributes == CustomerAttributes.None)
            {
                allCustomerCount++;
                continue;
            }

            bool isSingleAttribute = guideline.RequiredAttributes == CustomerAttributes.Male ||
                guideline.RequiredAttributes == CustomerAttributes.Female ||
                guideline.RequiredAttributes == CustomerAttributes.Child ||
                guideline.RequiredAttributes == CustomerAttributes.Adult ||
                guideline.RequiredAttributes == CustomerAttributes.Elderly;
            if (isSingleAttribute) singleAttributeCount++;
        }

        Assert.That(allCustomerCount, Is.GreaterThanOrEqualTo(600));
        Assert.That(singleAttributeCount, Is.GreaterThan(0));
        Assert.That(allCustomerCount + singleAttributeCount, Is.EqualTo(1000));
    }
}
