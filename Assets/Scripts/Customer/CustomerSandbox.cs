using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>개인 씬에서 상품·입장 대사·한 번의 총액 제안·결과 피드백을 검증한다. 실제 자금·재고는 변경하지 않는다.</summary>
public sealed class CustomerSandbox : MonoBehaviour
{
    /// <summary>임시 사각형 Sprite와 외형 CSV 색상을 표시할 Image.</summary>
    [SerializeField] private Image appearanceImage;
    /// <summary>외형 바로 위의 PK 조합 표시.</summary>
    [SerializeField] private Text identityText;
    /// <summary>성향·선호군·구매 상품별 수량 표시.</summary>
    [SerializeField] private Text orderText;
    /// <summary>로딩·생성 횟수·오류 표시.</summary>
    [SerializeField] private Text statusText;
    /// <summary>매 클릭마다 새로운 일반 방문을 생성하는 버튼.</summary>
    [SerializeField] private Button generateButton;
    /// <summary>전체 구매 목록의 제안 총액 입력.</summary>
    [SerializeField] private InputField offerInput;
    /// <summary>손님당 한 번의 가격 제안 버튼.</summary>
    [SerializeField] private Button offerButton;
    /// <summary>입장·수락·거절 대사 표시.</summary>
    [SerializeField] private Text dialogText;
    /// <summary>상품별 정사각형 이미지와 이름을 배치할 영역.</summary>
    [SerializeField] private RectTransform productRoot;
    /// <summary>화면에서 생성한 상품 카드만 소유하고 교체 시 제거한다.</summary>
    private readonly List<GameObject> productCards = new List<GameObject>();
    /// <summary>방문마다 재시드하지 않고 같은 난수 흐름을 사용한다.</summary>
    private readonly CustomerGenerator generator = new CustomerGenerator(new System.Random());
    /// <summary>로딩 성공 후 사용하는 manager 소유 데이터.</summary>
    private CustomerCatalog catalog;
    // DataTableManager가 소유하는 게임 전체 공용 텍스트를 참조한다.
    private TextDataTable texts;
    /// <summary>이 화면 수명 동안 소유하는 테스트 Sprite.</summary>
    private Sprite squareSprite;
    /// <summary>로컬 PC 글꼴. 프로젝트에 글꼴 파일을 복사하지 않는다.</summary>
    private Font displayFont;
    /// <summary>버튼 교체 동작 확인용 방문 생성 횟수.</summary>
    private int generationCount;

    /// <summary>현재 표시 중인 확정 방문. 생성 전 또는 판매 후보가 없으면 null.</summary>
    public CustomerVisit CurrentVisit { get; private set; }

    /// <summary>구독 수명을 component 활성 상태에 맞춘다.</summary>
    private void OnEnable()
    {
        if (generateButton == null) return;
        generateButton.onClick.AddListener(GenerateCustomer);
        if (offerButton != null) offerButton.onClick.AddListener(SubmitPrice);
        updateControls();
    }

    /// <summary>Init 진입과 개인 씬 직접 Play 모두 기존 manager 로더를 사용한다.</summary>
    private async void Start()
    {
        try
        {
            if (appearanceImage == null || identityText == null || orderText == null ||
                statusText == null || generateButton == null || offerInput == null ||
                offerButton == null || dialogText == null || productRoot == null)
                throw new InvalidOperationException("CustomerSandbox Inspector 연결이 누락되었습니다.");
            statusText.text = "CSV 로딩 및 참조 검사 중…";
            // 테스트 전용 사각형. 정식 아트 도입 시 Sprite/리소스 계약으로 교체한다.
            squareSprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
            appearanceImage.sprite = squareSprite;
            appearanceImage.enabled = false;
            displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = displayFont;

            var token = this.GetCancellationTokenOnDestroy();
            // 이미 부팅한 manager는 그대로 사용한다. 직접 Play 때만 기존 manager를 보충한다.
            if (ResourceManager.Instance == null)
            {
                var resources = new GameObject("ResourceManager").AddComponent<ResourceManager>();
                await resources.InitAsync(cancellationToken: token);
            }
            if (DataTableManager.Instance == null)
                new GameObject("DataTableManager").AddComponent<DataTableManager>();
            await DataTableManager.Instance.EnsureDataLoadedAsync().AttachExternalCancellation(token);
            token.ThrowIfCancellationRequested();
            var loadedCatalog = DataTableManager.Instance.Customers;
            var resourcesTable = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
            foreach (uint imageIdx in loadedCatalog.Products.Rows.Values.Where(x => x.ImageResourceIdx.HasValue)
                .Select(x => x.ImageResourceIdx.Value).Distinct())
            {
                string path = resourcesTable.GetResourcePath(imageIdx);
                var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(path).AttachExternalCancellation(token);
                token.ThrowIfCancellationRequested();
                if (sprite == null) throw new InvalidOperationException($"상품 image_resource_idx={imageIdx}: Sprite 로드 실패");
            }
            catalog = loadedCatalog;
            texts = DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text);
            if (GameSessionManager.Instance == null) new GameObject("GameSessionManager").AddComponent<GameSessionManager>();
            if (!GameSessionManager.Instance.IsInitialized) GameSessionManager.Instance.InitializeNewGame(DataTableManager.Instance);
            updateControls();
            identityText.text = "외형 PK / 성향 PK";
            orderText.text = "버튼을 눌러 손님을 생성하세요.";
            dialogText.text = "손님 입장 대기";
            statusText.text = $"준비 완료 · 외형 {catalog.Appearances.Rows.Count} / 성향 {catalog.Dispositions.Rows.Count} / 상품 {catalog.Products.Rows.Count}";
        }
        catch (OperationCanceledException) { /* 씬 종료는 정상 취소다. */ }
        catch (Exception exception)
        {
            if (this == null) return;
            if (statusText != null) statusText.text = "로드 실패 · Console을 확인하세요.";
            if (generateButton != null) generateButton.interactable = false;
            if (offerButton != null) offerButton.interactable = false;
            catalog = null;
            Debug.LogException(exception, this);
        }
    }

    /// <summary>하나의 방문을 생성하고 같은 표시 객체를 교체한다. 새 UI 객체를 누적하지 않는다.</summary>
    public void GenerateCustomer()
    {
        if (catalog == null || !isActiveAndEnabled) return;
        if (CurrentVisit != null && (CurrentVisit.State == CustomerState.Entering || CurrentVisit.State == CustomerState.AwaitingOffer)) return;
        try
        {
            var dailyPrices = GameSessionManager.Instance.EnsureDailyPrices();
            var visit = generator.Generate(catalog.Appearances.Rows.Keys.OrderBy(x => x).ToArray(),
                catalog.Dispositions.Rows.Values.OrderBy(x => x.Idx).ToArray(), catalog.Products.Rows, dailyPrices.ElapsedDays, () => GameSessionManager.Instance.EnsureDailyPrices().Prices);
            if (CurrentVisit != null && CurrentVisit.State != CustomerState.Departed) CurrentVisit.Depart();
            clearProducts();
            if (visit == null)
            {
                CurrentVisit = null;
                appearanceImage.enabled = false;
                identityText.text = "생성된 손님 없음";
                orderText.text = "현재 판매 가능한 상품이 없습니다.";
                dialogText.text = "등장 날짜와 활성 여부를 확인하세요.";
                statusText.text = "판매 가능 상품 0개 · CSV 수정 후 Play를 다시 시작하세요.";
                updateControls();
                return;
            }
            var appearance = catalog.Appearances.Rows[visit.AppearanceIdx];
            var color = new Color32(appearance.ColorR, appearance.ColorG, appearance.ColorB, appearance.ColorA);

            // 결과를 모두 준비한 뒤 방문과 화면을 함께 교체한다.
            CurrentVisit = visit;
            appearanceImage.color = color;
            appearanceImage.enabled = true;
            identityText.text = $"외형 {visit.AppearanceIdx} + 성향 {visit.DispositionIdx}";
            orderText.text = $"경과 {dailyPrices.ElapsedDays}일 · {visit.Items.Count}종 / 총 {visit.Items.Sum(x => (long)x.Quantity)}개\n희망 목록 표시 합계 {visit.Items.Sum(x => (long)x.Quantity * x.UnitPrice):N0} · 최종액 미확정";
            dialogText.text = texts.Rows[visit.EntryTextIdx].Text;
            showProducts(visit);
            offerInput.text = string.Empty;
            visit.BeginOffer();
            statusText.text = $"입장 #{++generationCount} · 전체 물품의 총액을 제안하세요.";
            updateControls();
        }
        catch (Exception exception)
        {
            generateButton.interactable = false;
            offerButton.interactable = false;
            statusText.text = "생성 실패 · Console을 확인하세요.";
            Debug.LogException(exception, this);
        }
    }

    /// <summary>숫자 문자열을 경계에서 검사하고 방문당 한 번 판정한다.</summary>
    public void SubmitPrice()
    {
        if (CurrentVisit == null || CurrentVisit.State != CustomerState.AwaitingOffer || !isActiveAndEnabled) return;
        if (!long.TryParse(offerInput.text, NumberStyles.None, CultureInfo.InvariantCulture, out long total) || total <= 0)
        {
            statusText.text = "총액은 1 이상의 정수로 입력하세요. 공백·소수점·기호는 사용할 수 없습니다.";
            return;
        }
        CurrentVisit.SubmitOffer(total, CurrentVisit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray());
        dialogText.text = texts.Rows[CurrentVisit.FeedbackTextIdx].Text;
        statusText.text = $"{CurrentVisit.OutcomeLabel} (판정값: {(int)CurrentVisit.Outcome}) · 제안 {total:N0} · 다음 손님 버튼으로 퇴장·교체";
        updateControls();
    }

    /// <summary>입력·버튼의 활성 상태를 방문 상태에 맞춘다.</summary>
    private void updateControls()
    {
        bool ready = catalog != null && isActiveAndEnabled;
        bool waiting = CurrentVisit?.State == CustomerState.AwaitingOffer;
        if (generateButton != null) generateButton.interactable = ready && !waiting && CurrentVisit?.State != CustomerState.Entering;
        if (offerButton != null) offerButton.interactable = ready && waiting;
        if (offerInput != null) offerInput.interactable = ready && waiting;
    }

    /// <summary>상품별 흰 정사각형 또는 등록 이미지 위에 상품명을 표시한다.</summary>
    /// <param name="visit">확정된 구매 목록.</param>
    private void showProducts(CustomerVisit visit)
    {
        productRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Max(220, ((visit.Items.Count + 2) / 3) * 205));
        productRoot.anchoredPosition = Vector2.zero;
        var resources = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
        for (int i = 0; i < visit.Items.Count; i++)
        {
            var item = visit.Items[i];
            var product = catalog.Products.Rows[item.ProductIdx];
            var card = new GameObject("Product " + item.ProductIdx, typeof(RectTransform), typeof(Image));
            productCards.Add(card);
            var rect = card.GetComponent<RectTransform>();
            rect.SetParent(productRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2((i % 3) * 205, -(i / 3) * 205);
            rect.sizeDelta = new Vector2(180, 180);
            var image = card.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = product.ImageResourceIdx.HasValue
                ? ResourceManager.Instance.GetResource<Sprite>(resources.GetResourcePath(product.ImageResourceIdx.Value)) : squareSprite;
            if (image.sprite == null) throw new InvalidOperationException($"상품 {product.Idx}: 이미지 미준비");
            image.color = Color.white;
            var category = catalog.Categories.Rows.Values.Single(x => x.ProductType == product.ProductType);
            var label = new GameObject("Name", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(5, 5);
            label.rectTransform.offsetMax = new Vector2(-5, -5);
            label.font = displayFont;
            label.fontSize = 22;
            label.color = Color.black;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.text = $"{texts.Rows[product.NameIdx].Text}\n기본: {product.BasePrice:N0} / 희망시: {item.UnitPrice:N0}\n{texts.Rows[category.NameIdx].Text} × {item.Quantity}";
        }
    }

    /// <summary>다음 방문 표시 전 이 화면 소유 상품 카드만 정리한다.</summary>
    private void clearProducts()
    {
        foreach (var card in productCards) { card.SetActive(false); Destroy(card); }
        productCards.Clear();
    }

    /// <summary>비활성화 중 버튼 callback이 남지 않도록 해제한다.</summary>
    private void OnDisable()
    {
        if (generateButton != null) generateButton.onClick.RemoveListener(GenerateCustomer);
        if (offerButton != null) offerButton.onClick.RemoveListener(SubmitPrice);
    }

    /// <summary>이 화면이 생성한 임시 표시 리소스만 해제한다.</summary>
    private void OnDestroy()
    {
        if (squareSprite != null) Destroy(squareSprite);
        if (displayFont != null) Destroy(displayFont);
    }
}
