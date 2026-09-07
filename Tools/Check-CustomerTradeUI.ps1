# GameplaySandbox를 Play한 뒤 실행한다. 파일을 변경하지 않고 현재 방문을 여러 번 교체한다.
$ErrorActionPreference = 'Stop'
$checkCode = @'
if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("PlayMode required");
var sandbox = UnityEngine.Object.FindFirstObjectByType<CustomerSandbox>();
if (sandbox == null) throw new Exception("CustomerSandbox required");
Func<string, object> field = name => typeof(CustomerSandbox).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(sandbox);
var generate = (UnityEngine.UI.Button)field("generateButton");
var submit = (UnityEngine.UI.Button)field("offerButton");
var input = (UnityEngine.UI.InputField)field("offerInput");
var dialog = (UnityEngine.UI.Text)field("dialogText");
var root = (UnityEngine.RectTransform)field("productRoot");
var catalog = DataTableManager.Instance.Customers;
Action<UnityEngine.UI.Button> click = button => {
    if (!button.IsInteractable()) throw new Exception("Button disabled: " + button.name);
    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,
        new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left },
        UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
};
if (sandbox.CurrentVisit == null || sandbox.CurrentVisit.State != CustomerState.AwaitingOffer) click(generate);
var dispositions = new System.Collections.Generic.HashSet<uint>();
for (int i = 0; i < 20; i++) {
    var visit = sandbox.CurrentVisit;
    dispositions.Add(visit.DispositionIdx);
    if (visit.State != CustomerState.AwaitingOffer || dialog.text != catalog.Texts.Rows[visit.EntryTextIdx].Text) throw new Exception("Entry state or dialog failed");
    sandbox.GenerateCustomer();
    if (!ReferenceEquals(visit, sandbox.CurrentVisit)) throw new Exception("Pending visit replaced");
    var cards = root.GetComponentsInChildren<UnityEngine.UI.Image>();
    if (cards.Length != visit.Items.Count || cards.Any(x => x.sprite == null || x.color != UnityEngine.Color.white)) throw new Exception("White product cards failed");
    foreach (var item in visit.Items) {
        string name = catalog.Texts.Rows[catalog.Products.Rows[item.ProductIdx].NameIdx].Text;
        if (!root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(x => x.text.Contains(name))) throw new Exception("Product name missing");
    }
    foreach (string bad in new[] { "", "0", "-1", "1.5", " 1", "1,000", "9223372036854775808" }) {
        input.text = bad; click(submit);
        if (visit.State != CustomerState.AwaitingOffer || visit.OfferedTotal.HasValue) throw new Exception("Invalid input consumed visit");
    }
    long total = visit.AllowedTotal + (i % 2);
    input.text = total.ToString(System.Globalization.CultureInfo.InvariantCulture); click(submit);
    if (visit.WasAccepted != (i % 2 == 0) || dialog.text != catalog.Texts.Rows[visit.FeedbackTextIdx].Text || submit.IsInteractable()) throw new Exception("Feedback or result failed");
    input.text = "1"; sandbox.SubmitPrice();
    if (visit.OfferedTotal != total) throw new Exception("Repeated submission changed result");
    input.text = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
    if (i < 19) {
        click(generate);
        if (visit.State != CustomerState.Departed || ReferenceEquals(visit, sandbox.CurrentVisit)) throw new Exception("Departure or next visit failed");
    }
}
return "CUSTOMER_TRADE_UI_PASS: 20 visits, 140 invalid inputs, 10 accepted/10 rejected, dialogs, white cards/names, duplicate offer blocked, dispositionVariants=" + dispositions.Count;
'@
$result = $checkCode | & unity-cli exec 2>&1
$result | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'CUSTOMER_TRADE_UI_PASS:') {
    throw 'Customer trade UI check did not complete successfully.'
}
