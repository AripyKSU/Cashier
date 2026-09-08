$ErrorActionPreference = 'Stop'
# InitScene -> MainScene의 새 Play 세션에서 실행한다. 사용자 저장 파일은 변경하지 않는다.
$result = @'
var ui = UnityEngine.Object.FindFirstObjectByType<Dev3SandboxTester>();
if (ui == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainScene") throw new Exception("MainScene UI not ready");
var rows = (ProductData[])typeof(Dev3SandboxTester).GetField("products", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
var catalog = DataTableManager.Instance.Customers;
var db = DataTableManager.Instance;
if (!object.ReferenceEquals(catalog.Appearances, db.GetDB<CustomerAppearanceDataTable>(DataTableType.CustomerAppearance)) ||
    !object.ReferenceEquals(catalog.Dispositions, db.GetDB<CustomerDispositionDataTable>(DataTableType.CustomerDisposition)) ||
    !object.ReferenceEquals(catalog.Categories, db.GetDB<ProductCategoryDataTable>(DataTableType.ProductCategory)) ||
    !object.ReferenceEquals(catalog.Products, db.GetDB<ProductDataTable>(DataTableType.Product))) throw new Exception("Catalog must reference manager tables");
var texts = db.GetDB<TextDataTable>(DataTableType.Text);
var uiTexts = typeof(Dev3SandboxTester).GetField("texts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
if (!object.ReferenceEquals(texts, uiTexts) || !texts.TryGetData(8012, out var water) || water.Text != "물") throw new Exception("Shared text lookup failed");
if (rows == null || rows.Length != catalog.Products.Rows.Count || rows.Any(p => !object.ReferenceEquals(p, catalog.Products.Rows[p.Idx]))) throw new Exception("UI must reference ProductData directly");
Action<string> click = name => {
    var b = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(x => x.name == name && x.gameObject.activeInHierarchy);
    if (!b.interactable) throw new Exception("Button disabled: " + name);
    UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
};
click("BtnNewGame"); click("BtnStartJourney"); click("BtnStartBusiness"); click("OpenShop");
var economy = GameSessionManager.Instance.Economy;
long before = economy.QueryService.CurrentBalance;
var first = ui.CurrentVisit;
if (first == null) throw new Exception("Customer absent");
foreach (char c in first.BaseTotal.ToString()) click("Digit" + c);
click("Confirm");
if (first.WasAccepted != true || economy.QueryService.CurrentBalance != before + first.BaseTotal || economy.QueryService.DailySaleIncome != first.BaseTotal) throw new Exception("Accepted sale accounting failed");
ui.confirm();
if (economy.QueryService.CurrentBalance != before + first.BaseTotal) throw new Exception("Duplicate income");
ui.Queue.Advance(5, false); // 대기 손님을 먼저 입장시킨다. 버튼은 FIFO 인계만 수행한다.
click("NextCustomer");
if (first.State != CustomerState.Departed) throw new Exception("Departure failed");
var second = ui.CurrentVisit;
foreach (char c in (second.AllowedTotal + 1).ToString()) click("Digit" + c);
click("Confirm");
if (second.WasAccepted != false || economy.QueryService.CurrentBalance != before + first.BaseTotal) throw new Exception("Rejection accounting failed");
click("EndDay");
if (economy.QueryService.IsDayOpen) throw new Exception("Day still open");
click("NextDay");
if (ui.Day != 2) throw new Exception("Day progression failed");
return "MAIN_INTEGRATION_PASS: button input, accepted/rejected, one income, departure, settlement, next day";
'@ | unity-cli exec
if ($LASTEXITCODE -ne 0 -or "$result" -notmatch 'MAIN_INTEGRATION_PASS:') { throw "Unity integration check failed: $result" }
$result
