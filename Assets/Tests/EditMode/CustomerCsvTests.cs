using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>실제 CSV parser와 FK 공개 경계를 각 음성 사례로 검사한다.</summary>
public sealed class CustomerCsvTests
{
    /// <summary>정상 파일의 행 수·조회·routing·초기값을 확인한다.</summary>
    [Test]
    public void ValidCatalogAndRouting()
    {
string root = "Assets/Datas/Customer/";
string appearance = File.ReadAllText(root + "CustomerAppearanceData.csv");
string disposition = File.ReadAllText(root + "CustomerDispositionData.csv");
string category = File.ReadAllText(root + "ProductCategoryData.csv");
string product = File.ReadAllText(root + "ProductData.csv");
string texts = File.ReadAllText("Assets/Datas/TextData.csv");
var textTables = new Dictionary<CustomerCatalog, TextDataTable>();
Func<CustomerCatalog> load = () => {
 var c = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
 textTables.Add(c, new TextDataTable());
 c.Appearances.LoadData(appearance); c.Dispositions.LoadData(disposition);
 c.Categories.LoadData(category); c.Products.LoadData(product);
 textTables[c].LoadData(texts);
 return c;
};
var valid = load();
if (valid.Products.GetDataCount() != 0) throw new Exception("Published before FK validation");
valid.ValidateAndCommit(textTables[valid], loadResources(), facilities: loadFacilities());
if (!valid.Appearances.TryGetData(5001, out _) || !valid.Dispositions.TryGetData(6001, out _) || !valid.Categories.TryGetData(7001, out _) || !valid.Products.TryGetData(1001, out var queriedProduct) || !object.ReferenceEquals(queriedProduct, valid.Products.Rows[1001]) || !textTables[valid].TryGetData(8001, out _) || valid.Products.TryGetData(0, out _)) throw new Exception("Concrete table lookup failed");
if (valid.Appearances.GetDataCount() != 45 || valid.Dispositions.GetDataCount() != 15 || valid.Categories.GetDataCount() != 7 || valid.Products.GetDataCount() != 16 || textTables[valid].GetDataCount() != 200) throw new Exception("Unexpected sample counts");
var expectedProductIds = new uint[] { 1001, 1004, 1005, 1006, 1007, 1010, 1013, 1014, 1015, 1016, 1018, 1019, 1020, 1021, 1022, 1023 };
if (!valid.Products.Rows.Keys.OrderBy(x => x).SequenceEqual(expectedProductIds)) throw new Exception("Unexpected final product IDs");
if (valid.Products.Rows.Values.Count(x => !x.RequiredFacilityIdx.HasValue) != 4) throw new Exception("Unexpected default product count");
if (valid.Products.Rows[1022].RequiredFacilityIdx != 12006 || valid.Products.Rows[1023].RequiredFacilityIdx != 12006 ||
    textTables[valid].Rows[valid.Products.Rows[1023].NameIdx].Text != "열화상 카메라") throw new Exception("New precision equipment product routing failed");
if (textTables[valid].Rows[valid.Products.Rows[1001].NameIdx].Text != "물") throw new Exception("nameidx lookup failed");
if (Util.GetDataTableType(1001) != DataTableType.Product || Util.GetDataTableType(2001) != DataTableType.EconomyBalance || Util.GetDataTableType(3001) != DataTableType.MaintenanceBalance || Util.GetDataTableType(4001) != DataTableType.Resource || Util.GetDataTableType(8001) != DataTableType.Text) throw new Exception("Routing failed");
if ((uint)DataTableType.DataTableType_End != (uint)DataTableType.DaughterAppearance + 1) throw new Exception("End marker must follow the last table");
if (valid.Dispositions.Rows.Values.Any(x => x.PreferredSelectionChance != 900)) throw new Exception("Probability migration failed");

Assert.That(valid.Dispositions.Rows.Values.All(x=>x.RegularPriceMinRate==1000 && x.RegularPriceMaxRate==1000));
Assert.That(valid.Dispositions.Rows.Values.All(x=>x.PreferredProductIdxs.Count==0));
Assert.That(valid.Dispositions.Rows.OrderBy(x=>x.Key).Select(x=>x.Value.DispositionType), Is.EqualTo(new[]{CustomerDispositionType.Normal,CustomerDispositionType.Hasty,CustomerDispositionType.PriceSensitive,CustomerDispositionType.Normal,CustomerDispositionType.Normal,CustomerDispositionType.Normal,CustomerDispositionType.Wealthy,CustomerDispositionType.PriceSensitive,CustomerDispositionType.PriceSensitive,CustomerDispositionType.Wealthy,CustomerDispositionType.Wealthy,CustomerDispositionType.Poor,CustomerDispositionType.Poor,CustomerDispositionType.Poor,CustomerDispositionType.Poor}));
    }
    /// <summary>명명된 잘못된 파일 하나가 LogError와 예외를 내며 공개되지 않는지 확인한다.</summary>
    /// <param name="name">오류 사례.</param>
    [TestCase("old probability header")]
    [TestCase("negative probability")]
    [TestCase("probability overflow")]
    [TestCase("header")]
    [TestCase("PK duplicate")]
    [TestCase("PK range")]
    [TestCase("product enum")]
    [TestCase("disposition enum")]
    [TestCase("boolean")]
    [TestCase("quantity")]
    [TestCase("product nameidx")]
    [TestCase("appearance nameidx")]
    [TestCase("disposition nameidx")]
    [TestCase("category nameidx")]
    [TestCase("empty text")]
    [TestCase("duplicate text")]
    [TestCase("missing text table")]
    [TestCase("enum string")]
    [TestCase("duplicate category type")]
    [TestCase("missing category display")]
    [TestCase("base price")]
    [TestCase("negative day")]
    [TestCase("zero image")]
    [TestCase("image FK")]
    [TestCase("entry dialog FK")]
    [TestCase("empty entry")]
    [TestCase("duplicate entry")]
    [TestCase("accept dialog FK")]
    [TestCase("reject dialog FK")]
    [TestCase("price tolerance")]
    [TestCase("queue patience")]
    [TestCase("queue warning FK")]
    [TestCase("queue leave FK")]
    [TestCase("queue header")]
    [TestCase("cost header")]
    [TestCase("zero cost")]
    [TestCase("type header")]
    [TestCase("product preference header")]
[TestCase("regular min header")]
[TestCase("regular max header")]
[TestCase("minimum tolerance header")]
[TestCase("minimum tolerance overflow")]
[TestCase("minimum tolerance above maximum")]
    [TestCase("type 0")]
[TestCase("type 6")]
    [TestCase("type 99")]
    [TestCase("type Normal")]
    [TestCase("type empty")]
    [TestCase("preferred product 0")]
    [TestCase("preferred product 1001_1001")]
    [TestCase("preferred product 1999")]
    [TestCase("regular min 0")]
    [TestCase("regular min -1")]
    [TestCase("regular min 1001")]
    [TestCase("regular min empty")]
    [TestCase("regular max 999")]
    [TestCase("regular max empty")]
    public void RejectInvalidCsv(string name)
    {
string root = "Assets/Datas/Customer/";
string appearance = File.ReadAllText(root + "CustomerAppearanceData.csv");
string disposition = File.ReadAllText(root + "CustomerDispositionData.csv");
string category = File.ReadAllText(root + "ProductCategoryData.csv");
string product = File.ReadAllText(root + "ProductData.csv");
string texts = File.ReadAllText("Assets/Datas/TextData.csv");
var textTables = new Dictionary<CustomerCatalog, TextDataTable>();
Func<CustomerCatalog> load = () => {
 var c = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
 textTables.Add(c, new TextDataTable());
 c.Appearances.LoadData(appearance); c.Dispositions.LoadData(disposition);
 c.Categories.LoadData(category); c.Products.LoadData(product);
 textTables[c].LoadData(texts);
 return c;
};

var c=load();
Action mutate;
switch(name) {
case "old probability header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("preferred_selection_chance", "preferred_selection_percent")); break;
case "negative probability": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",900,", ",-1,")); break;
case "probability overflow": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",900,", ",1001,")); break;
case "header": mutate=()=>c.Products.LoadData(product.Replace("product_type", "category_idx")); break;
case "PK duplicate": mutate=()=>c.Products.LoadData(product.TrimEnd() + "\n" + product.Split('\n')[1]); break;
case "PK range": mutate=()=>c.Products.LoadData(product.Replace("1001,", "9001,")); break;
case "product enum": mutate=()=>c.Products.LoadData(product.Replace("1001,8012,1", "1001,8012,99")); break;
case "disposition enum": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8006,3,", "8006,99,")); break;
case "boolean": mutate=()=>c.Products.LoadData(product.Replace("1001,8012,1,1", "1001,8012,1,2")); break;
case "quantity": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",900,1,3,1,3", ",900,1,3,0,3")); break;
case "product nameidx": mutate=()=>c.Products.LoadData(product.Replace("1001,8012", "1001,8999")); break;
case "appearance nameidx": mutate=()=>c.Appearances.LoadData(appearance.Replace("5001,8001", "5001,8999")); break;
case "disposition nameidx": mutate=()=>c.Dispositions.LoadData(disposition.Replace("6001,8005", "6001,0")); break;
case "category nameidx": mutate=()=>c.Categories.LoadData(category.Replace("7001,8008", "7001,8999")); break;
case "empty text": mutate=()=>textTables[c].LoadData(texts.Replace("8012,물", "8012,")); break;
case "duplicate text": mutate=()=>textTables[c].LoadData(texts.TrimEnd() + "\n8012,duplicate\n"); break;
case "missing text table": mutate=()=>textTables[c].Release(); break;
case "enum string": mutate=()=>c.Products.LoadData(product.Replace("1001,8012,1,", "1001,8012,Water,")); break;
case "duplicate category type": mutate=()=>c.Categories.LoadData(category.Replace("7002,8009,2", "7002,8009,1")); break;
case "missing category display": mutate=()=>c.Categories.LoadData(category.Replace("7004,8011,4", "")); break;
case "base price": mutate=()=>c.Products.LoadData(product.Replace("1,1,100,0,", "1,1,0,0,")); break;
case "negative day": mutate=()=>c.Products.LoadData(product.Replace("1,1,100,0,", "1,1,100,-1,")); break;
case "zero image": mutate=()=>c.Products.LoadData(product.Replace("100,0,4254,50", "100,0,0,50")); break;
case "image FK": mutate=()=>c.Products.LoadData(product.Replace("100,0,4254,50", "100,0,4999,50")); break;
case "entry dialog FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8024_8025", "8999")); break;
case "empty entry": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8024_8025", "")); break;
case "duplicate entry": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8024_8025", "8024_8024")); break;
case "accept dialog FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8026_8027", "8999")); break;
case "reject dialog FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8028_8029", "8999")); break;
case "price tolerance": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",1100,", ",0,")); break;
case "queue patience": mutate=()=>c.Dispositions.LoadData(disposition.Replace("12,8050,8051", "6,8050,8051")); break;
case "queue warning FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("12,8050,8051", "12,8999,8051")); break;
case "queue leave FK": mutate=()=>c.Dispositions.LoadData(disposition.Replace("12,8050,8051", "12,8050,8999")); break;
case "queue header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("queue_patience_seconds", "missing_queue_patience")); break;
case "cost header": mutate=()=>c.Products.LoadData(product.Replace("cost_price", "missing_cost")); break;
case "zero cost": mutate=()=>c.Products.LoadData(product.Replace("100,0,4254,50", "100,0,4254,0")); break;
case "type header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("disposition_type", "missing_type")); break;
case "product preference header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("preferred_product_idxs", "missing_products")); break;
case "regular min header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("regular_price_min_rate","missing_min")); break;
case "regular max header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("regular_price_max_rate","missing_max")); break;
case "minimum tolerance header": mutate=()=>c.Dispositions.LoadData(disposition.Replace("minimum_price_tolerance","missing_minimum_tolerance")); break;
case "minimum tolerance overflow": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",1000,8036_8037", ",1001,8036_8037")); break;
case "minimum tolerance above maximum": mutate=()=>c.Dispositions.LoadData(disposition.Replace(",0,8024_8025", ",1200,8024_8025")); break;
case "type 0": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,0,")); break;
case "type 6": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,6,")); break;
case "type 99": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,99,")); break;
case "type Normal": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,Normal,")); break;
case "type empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,,")); break;
case "preferred product 0": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,1,0")); break;
case "preferred product 1001_1001": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,1,1001_1001")); break;
case "preferred product 1999": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,", "8051,1,1999")); break;
case "regular min 0": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,0,1000")); break;
case "regular min -1": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,-1,1000")); break;
case "regular min 1001": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,1001,1000")); break;
case "regular min empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,,1000")); break;
case "regular max 999": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,1000,999")); break;
case "regular max empty": mutate=()=>c.Dispositions.LoadData(disposition.Replace("8051,1,,1000,1000", "8051,1,,1000,")); break;
default: throw new ArgumentOutOfRangeException(nameof(name));
}
var resources = loadResources();
LogAssert.Expect(LogType.Error,new Regex(@"^(?:\[Customer CSV\] |(?:CustomerAppearanceData|CustomerDispositionData|ProductCategoryData|ProductData|TextData)\.csv)"));
Assert.Catch(()=>{mutate(); c.ValidateAndCommit(textTables[c], resources, facilities: loadFacilities());});
Assert.That(c.Products.GetDataCount(), Is.Zero); Assert.That(c.Appearances.GetDataCount(), Is.Zero); Assert.That(textTables[c].GetDataCount(), Is.Zero);
LogAssert.NoUnexpectedReceived();
    }
    /// <summary>불량 재로딩은 이전 공개 외형을 유지한다.</summary>
    [Test]
    public void InvalidAppearancePreservesPublishedRows()
    {
string root = "Assets/Datas/Customer/";
string appearance = File.ReadAllText(root + "CustomerAppearanceData.csv");
string disposition = File.ReadAllText(root + "CustomerDispositionData.csv");
string category = File.ReadAllText(root + "ProductCategoryData.csv");
string product = File.ReadAllText(root + "ProductData.csv");
string texts = File.ReadAllText("Assets/Datas/TextData.csv");
var textTables = new Dictionary<CustomerCatalog, TextDataTable>();
Func<CustomerCatalog> load = () => {
 var c = new CustomerCatalog(new CustomerAppearanceDataTable(), new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable());
 textTables.Add(c, new TextDataTable());
 c.Appearances.LoadData(appearance); c.Dispositions.LoadData(disposition);
 c.Categories.LoadData(category); c.Products.LoadData(product);
 textTables[c].LoadData(texts);
 return c;
};

var c=load();c.ValidateAndCommit(textTables[c], loadResources(), facilities: loadFacilities());
LogAssert.Expect(LogType.Error,new Regex(@"^CustomerAppearanceData\.csv"));
Assert.Catch(()=>c.Appearances.LoadData(appearance.Replace("5001,8001,4201","5001,8001,0")));
Assert.That(c.Appearances.GetDataCount(),Is.EqualTo(45));
    }
    /// <summary>리소스 대역 검증 실패는 이전 공개 리소스를 보존한다.</summary>
    [Test]
    public void InvalidResourcePreservesPublishedRows()
    {
var resources=new ResourceDataTable(); string csv=File.ReadAllText("Assets/Datas/ResourceData.csv"); resources.LoadData(csv); int count=resources.GetDataCount();
Assert.That(resources.GetResourcePath(4201),Is.EqualTo("FemaleCustomer_01")); Assert.That(resources.TryGetResource(3001,out _),Is.False);
LogAssert.Expect(LogType.Error,new Regex(@"ResourceData\.csv"));
Assert.Catch(()=>resources.LoadData(csv.Replace("4201,FemaleCustomer_01","3001,FemaleCustomer_01")));
Assert.That(resources.GetDataCount(),Is.EqualTo(count));
    }
    /// <summary>실제 리소스 FK를 검증하는 기존 테이블을 읽는다.</summary>
    /// <returns>검증된 리소스 테이블.</returns>
    private static ResourceDataTable loadResources()
    {
        var table = new ResourceDataTable();
        LogAssert.Expect(LogType.Log, new Regex(@"^\[ResourceDataTable\] 총 54개의 리소스 경로 데이터 로드 완료\."));
        table.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
        return table;
    }

    /// <summary>실제 설비 CSV를 공개 전 FK 검증용으로 읽는다.</summary>
    /// <returns>검증 대기 설비 테이블.</returns>
    private static FacilityDataTable loadFacilities()
    {
        var table = new FacilityDataTable();
        table.LoadData(File.ReadAllText("Assets/Datas/FacilityData.csv"));
        return table;
    }
}
