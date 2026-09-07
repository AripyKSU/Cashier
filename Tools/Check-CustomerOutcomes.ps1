$ErrorActionPreference = 'Stop'
# InitScene에서 새로 시작해 MainScene 또는 개인 씬의 타이틀 화면에 도착한 뒤 실행한다.
$result = @'
var ui = UnityEngine.Object.FindFirstObjectByType<Dev3SandboxTester>();
if (ui == null) throw new Exception("UI not ready");
Action<string> click = name => {
    var button = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(x => x.name == name && x.gameObject.activeInHierarchy);
    if (!button.interactable) throw new Exception("Disabled: " + name);
    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
};
click("BtnNewGame"); click("BtnStartJourney"); click("BtnStartBusiness"); click("OpenShop");
var economy = GameSessionManager.Instance.Economy;
var texts = DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text);
foreach (var expected in new[] { CustomerTradeOutcome.RegularSale, CustomerTradeOutcome.DiscountSale, CustomerTradeOutcome.PaymentRefused, CustomerTradeOutcome.ExploitativeSale }) {
    // 동일한 실제 UI 경로로 후보를 넘기되 무한 재시도하지 않는다.
    int skipped = 0;
    while (expected == CustomerTradeOutcome.ExploitativeSale && ui.CurrentVisit.AllowedTotal <= ui.CurrentVisit.BaseTotal) {
        if (++skipped > 30) throw new Exception("No markup-capable customer");
        foreach (char digit in ui.CurrentVisit.BaseTotal.ToString()) click("Digit" + digit);
        click("Confirm"); click("NextCustomer");
    }
    var visit = ui.CurrentVisit;
    long total = expected == CustomerTradeOutcome.RegularSale ? visit.BaseTotal
        : expected == CustomerTradeOutcome.DiscountSale ? visit.BaseTotal - 1
        : expected == CustomerTradeOutcome.PaymentRefused ? visit.AllowedTotal + 1 : visit.BaseTotal + 1;
    long before = economy.QueryService.CurrentBalance;
    foreach (char digit in total.ToString()) click("Digit" + digit);
    click("Confirm");
    if (visit.Outcome != expected) throw new Exception("Outcome mismatch: " + expected);
    long income = expected == CustomerTradeOutcome.PaymentRefused ? 0 : total;
    if (economy.QueryService.CurrentBalance != before + income) throw new Exception("Income mismatch");
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    var reason = (TMPro.TMP_Text)typeof(Dev3SandboxTester).GetField("reasonText", flags).GetValue(ui);
    var dialog = (TMPro.TMP_Text)typeof(Dev3SandboxTester).GetField("dialogueText", flags).GetValue(ui);
    if (!reason.text.Contains($"{visit.OutcomeLabel} (판정값: {(int)visit.Outcome})") || dialog.text != texts.Rows[visit.FeedbackTextIdx].Text) throw new Exception("Feedback mismatch");
    ui.confirm();
    if (economy.QueryService.CurrentBalance != before + income) throw new Exception("Duplicate income");
    click("NextCustomer");
    if (visit.State != CustomerState.Departed || visit.Outcome != expected) throw new Exception("Outcome lost after departure");
}
return "CUSTOMER_OUTCOMES_PASS: four outcomes, UI/dialog, income, duplicate guard, departure";
'@ | unity-cli exec
if ($LASTEXITCODE -ne 0 -or "$result" -notmatch 'CUSTOMER_OUTCOMES_PASS:') { throw "Outcome check failed: $result" }
$result
