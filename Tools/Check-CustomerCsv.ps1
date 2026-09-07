# 원본 CSV를 바꾸지 않고 메모리 사본의 파싱·FK 실패를 검사한다. 의도적인 LogError는 제품 오류와 구분한다.
$ErrorActionPreference = 'Stop'
$checkCode = @'
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
valid.ValidateAndCommit(textTables[valid]);
if (!valid.Appearances.TryGetData(5001, out _) || !valid.Dispositions.TryGetData(6001, out _) || !valid.Categories.TryGetData(7001, out _) || !valid.Products.TryGetData(1001, out var queriedProduct) || !object.ReferenceEquals(queriedProduct, valid.Products.Rows[1001]) || !textTables[valid].TryGetData(8001, out _) || valid.Products.TryGetData(0, out _)) throw new Exception("Concrete table lookup failed");
if (valid.Appearances.GetDataCount() != 4 || valid.Dispositions.GetDataCount() != 3 || valid.Categories.GetDataCount() != 4 || valid.Products.GetDataCount() != 12 || textTables[valid].GetDataCount() != 41) throw new Exception("Unexpected sample counts");
if (textTables[valid].Rows[valid.Products.Rows[1001].NameIdx].Text != "물") throw new Exception("nameidx lookup failed");
if (Util.GetDataTableType(1001) != DataTableType.Product || Util.GetDataTableType(2001) != DataTableType.EconomyBalance || Util.GetDataTableType(3001) != DataTableType.MaintenanceBalance || Util.GetDataTableType(4001) != DataTableType.Resource || Util.GetDataTableType(8001) != DataTableType.Text || Enum.IsDefined(typeof(DataTableType), Util.GetDataTableType(9001))) throw new Exception("Routing failed");
if (valid.Dispositions.Rows.Values.Any(x => x.PreferredSelectionChance != 900)) throw new Exception("Probability migration failed");
int rejected = 0;
Action<string, Action<CustomerCatalog>> reject = (name, mutate) => {
 var c = load();
 try { mutate(c); c.ValidateAndCommit(textTables[c]); }
 catch (Exception) {
  if (c.Products.GetDataCount() != 0 || c.Appearances.GetDataCount() != 0 || textTables[c].GetDataCount() != 0) throw new Exception("Invalid data published: " + name);
  rejected++; return;
 }
 throw new Exception("Invalid data accepted: " + name);
};
reject("old probability header", c => c.Dispositions.LoadData(disposition.Replace("preferred_selection_chance", "preferred_selection_percent")));
reject("negative probability", c => c.Dispositions.LoadData(disposition.Replace(",900,", ",-1,")));
reject("probability overflow", c => c.Dispositions.LoadData(disposition.Replace(",900,", ",1001,")));
reject("header", c => c.Products.LoadData(product.Replace("product_type", "category_idx")));
reject("PK duplicate", c => c.Products.LoadData(product.TrimEnd() + "\n1001,8012,1,1,100,0,\n"));
reject("PK range", c => c.Products.LoadData(product.Replace("1001,", "9001,")));
reject("product enum", c => c.Products.LoadData(product.Replace("1001,8012,1", "1001,8012,99")));
reject("disposition enum", c => c.Dispositions.LoadData(disposition.Replace("8006,3,", "8006,99,")));
reject("boolean", c => c.Products.LoadData(product.Replace("1001,8012,1,1", "1001,8012,1,2")));
reject("quantity", c => c.Dispositions.LoadData(disposition.Replace(",900,1,3,1,3", ",900,1,3,0,3")));
reject("product nameidx", c => c.Products.LoadData(product.Replace("1001,8012", "1001,8999")));
reject("appearance nameidx", c => c.Appearances.LoadData(appearance.Replace("5001,8001", "5001,8999")));
reject("disposition nameidx", c => c.Dispositions.LoadData(disposition.Replace("6001,8005", "6001,0")));
reject("category nameidx", c => c.Categories.LoadData(category.Replace("7001,8008", "7001,8999")));
reject("empty text", c => textTables[c].LoadData(texts.Replace("8012,물", "8012,")));
reject("duplicate text", c => textTables[c].LoadData(texts.TrimEnd() + "\n8012,duplicate\n"));
reject("missing text table", c => textTables[c].Release());
reject("enum string", c => c.Products.LoadData(product.Replace("1001,8012,1,", "1001,8012,Water,")));
reject("duplicate category type", c => c.Categories.LoadData(category.Replace("7002,8009,2", "7002,8009,1")));
reject("missing category display", c => c.Categories.LoadData(category.Replace("7004,8011,4", "")));
reject("base price", c => c.Products.LoadData(product.Replace("1,1,100,0,", "1,1,0,0,")));
reject("negative day", c => c.Products.LoadData(product.Replace("1,1,100,0,", "1,1,100,-1,")));
reject("zero image", c => c.Products.LoadData(product.Replace("1,1,100,0,", "1,1,100,0,0")));
reject("image FK", c => c.Products.LoadData(product.Replace("1,1,100,0,", "1,1,100,0,4999")));
reject("entry dialog FK", c => c.Dispositions.LoadData(disposition.Replace("8024_8025", "8999")));
reject("empty entry", c => c.Dispositions.LoadData(disposition.Replace("8024_8025", "")));
reject("duplicate entry", c => c.Dispositions.LoadData(disposition.Replace("8024_8025", "8024_8024")));
reject("accept dialog FK", c => c.Dispositions.LoadData(disposition.Replace("8026_8027", "8999")));
reject("reject dialog FK", c => c.Dispositions.LoadData(disposition.Replace("8028_8029", "8999")));
reject("price tolerance", c => c.Dispositions.LoadData(disposition.Replace(",1100,", ",0,")));
if (valid.Products.Rows.Values.Any(x => x.ImageResourceIdx.HasValue || x.AvailableDay != 0 || x.BasePrice == 0)) throw new Exception("Test product defaults failed");
try { valid.Appearances.LoadData(appearance.Replace("101,184", "256,184")); }
catch (Exception) { rejected++; }
var resources = new ResourceDataTable();
string resourceCsv = File.ReadAllText("Assets/Datas/ResourceData.csv");
resources.LoadData(resourceCsv);
int resourceCount = resources.GetDataCount();
if (resources.GetResourcePath(4001) != "Unit_3001" || resources.TryGetResource(3001, out _) || resources.TryGetResource(1001, out _)) throw new Exception("Resource migration failed");
try { resources.LoadData(resourceCsv.Replace("4001,Unit_3001", "3001,Unit_3001")); }
catch (Exception) { rejected++; }
if (rejected != 32 || valid.Appearances.GetDataCount() != 4 || resources.GetDataCount() != resourceCount) throw new Exception("Rejection or prior snapshot preservation failed: " + rejected);
return "CUSTOMER_CSV_CHECK_PASS: valid=4/3/4/12/41, rejected=32/32, enum/dialog/image/price/day/routing checked, resources=" + resourceCount + "; expected LogError=32";
'@
$result = $checkCode | & unity-cli exec 2>&1
$result | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'CUSTOMER_CSV_CHECK_PASS:') {
    throw 'Customer CSV check did not complete successfully.'
}
