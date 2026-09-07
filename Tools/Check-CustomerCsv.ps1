# 원본 CSV를 바꾸지 않고 메모리 사본의 파싱·FK 실패를 검사한다. 예상 LogError 16건이 발생한다.
$ErrorActionPreference = 'Stop'
$checkCode = @'
string root = "Assets/Datas/Customer/";
string appearance = File.ReadAllText(root + "CustomerAppearanceData.csv");
string disposition = File.ReadAllText(root + "CustomerDispositionData.csv");
string category = File.ReadAllText(root + "ProductCategoryData.csv");
string product = File.ReadAllText(root + "ProductData.csv");
string texts = File.ReadAllText("Assets/Datas/TextData.csv");
Func<CustomerCatalog> load = () => {
 var c = new CustomerCatalog();
 c.Appearances.LoadData(appearance); c.Dispositions.LoadData(disposition);
 c.Categories.LoadData(category); c.Products.LoadData(product);
 c.Texts.LoadData(texts);
 return c;
};
var valid = load();
if (valid.Products.GetDataCount() != 0) throw new Exception("Published before FK validation");
valid.ValidateAndCommit();
if (valid.Appearances.GetDataCount() != 4 || valid.Dispositions.GetDataCount() != 3 || valid.Categories.GetDataCount() != 4 || valid.Products.GetDataCount() != 12 || valid.Texts.GetDataCount() != 23) throw new Exception("Unexpected sample counts");
if (valid.Texts.Rows[valid.Products.Rows[1001].NameIdx].Text != "물") throw new Exception("nameidx lookup failed");
if (Util.GetDataTableType(1001) != DataTableType.Product || Util.GetDataTableType(2001) != DataTableType.Balance || Util.GetDataTableType(3001) != DataTableType.Resource || Util.GetDataTableType(8001) != DataTableType.Text) throw new Exception("Routing failed");
int rejected = 0;
Action<string, Action<CustomerCatalog>> reject = (name, mutate) => {
 var c = load();
 try { mutate(c); c.ValidateAndCommit(); }
 catch (Exception) {
  if (c.Products.GetDataCount() != 0 || c.Appearances.GetDataCount() != 0 || c.Texts.GetDataCount() != 0) throw new Exception("Invalid data published: " + name);
  rejected++; return;
 }
 throw new Exception("Invalid data accepted: " + name);
};
reject("header", c => c.Products.LoadData(product.Replace("category_idx", "missing_category")));
reject("PK duplicate", c => c.Products.LoadData(product.TrimEnd() + "\n1001,8012,7001,1\n"));
reject("PK range", c => c.Products.LoadData(product.Replace("1001,", "9001,")));
reject("product FK", c => c.Products.LoadData(product.Replace("1001,8012,7001", "1001,8012,7999")));
reject("disposition FK", c => c.Dispositions.LoadData(disposition.Replace("7003", "7999")));
reject("boolean", c => c.Products.LoadData(product.Replace("1001,8012,7001,1", "1001,8012,7001,2")));
reject("quantity", c => c.Dispositions.LoadData(disposition.Replace(",90,1,3,1,3", ",90,1,3,0,3")));
reject("product nameidx", c => c.Products.LoadData(product.Replace("1001,8012", "1001,8999")));
reject("appearance nameidx", c => c.Appearances.LoadData(appearance.Replace("5001,8001", "5001,8999")));
reject("disposition nameidx", c => c.Dispositions.LoadData(disposition.Replace("6001,8005", "6001,0")));
reject("category nameidx", c => c.Categories.LoadData(category.Replace("7001,8008", "7001,8999")));
reject("empty text", c => c.Texts.LoadData(texts.Replace("8012,물", "8012,")));
reject("duplicate text", c => c.Texts.LoadData(texts.TrimEnd() + "\n8012,duplicate\n"));
reject("missing text table", c => c.Texts.Release());
try { valid.Appearances.LoadData(appearance.Replace("101,184", "256,184")); }
catch (Exception) { rejected++; }
var resources = new ResourceDataTable();
string resourceCsv = File.ReadAllText("Assets/Datas/ResourceData.csv");
resources.LoadData(resourceCsv);
int resourceCount = resources.GetDataCount();
if (resources.GetResourcePath(3001) != "Unit_3001" || resources.TryGetResource(1001, out _)) throw new Exception("Resource migration failed");
try { resources.LoadData(resourceCsv.Replace("3001,Unit_3001", "1001,Unit_3001")); }
catch (Exception) { rejected++; }
if (rejected != 16 || valid.Appearances.GetDataCount() != 4 || resources.GetDataCount() != resourceCount) throw new Exception("Rejection or prior snapshot preservation failed");
return "CUSTOMER_CSV_CHECK_PASS: valid=4/3/4/12/23, rejected=16/16, routing and nameidx checked, resources=" + resourceCount + "; expected LogError=16";
'@
$result = $checkCode | & unity-cli exec 2>&1
$result | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'CUSTOMER_CSV_CHECK_PASS:') {
    throw 'Customer CSV check did not complete successfully.'
}
