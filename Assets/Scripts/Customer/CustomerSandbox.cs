using System;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>개인 씬의 CSV 기반 손님 생성 실험 화면. 거래·가격·대사 시스템은 포함하지 않는다.</summary>
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
    /// <summary>방문마다 재시드하지 않고 같은 난수 흐름을 사용한다.</summary>
    private readonly CustomerGenerator generator = new CustomerGenerator(new System.Random());
    /// <summary>로딩 성공 후 사용하는 manager 소유 데이터.</summary>
    private CustomerCatalog catalog;
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
        generateButton.interactable = catalog != null;
    }

    /// <summary>Init 진입과 개인 씬 직접 Play 모두 기존 manager 로더를 사용한다.</summary>
    private async void Start()
    {
        try
        {
            if (appearanceImage == null || identityText == null || orderText == null ||
                statusText == null || generateButton == null)
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
            catalog = DataTableManager.Instance.Customers;
            generateButton.interactable = isActiveAndEnabled;
            identityText.text = "외형 PK / 성향 PK";
            orderText.text = "버튼을 눌러 손님을 생성하세요.";
            statusText.text = $"준비 완료 · 외형 {catalog.Appearances.Rows.Count} / 성향 {catalog.Dispositions.Rows.Count} / 상품 {catalog.Products.Rows.Count}";
        }
        catch (OperationCanceledException) { /* 씬 종료는 정상 취소다. */ }
        catch (Exception exception)
        {
            if (this == null) return;
            if (statusText != null) statusText.text = "로드 실패 · Console을 확인하세요.";
            if (generateButton != null) generateButton.interactable = false;
            Debug.LogException(exception, this);
        }
    }

    /// <summary>하나의 방문을 생성하고 같은 표시 객체를 교체한다. 새 UI 객체를 누적하지 않는다.</summary>
    public void GenerateCustomer()
    {
        if (catalog == null || !isActiveAndEnabled) return;
        try
        {
            var available = catalog.Products.Rows.Values.Where(x => x.IsAvailable)
                .ToDictionary(x => x.Idx, x => x.CategoryIdx);
            var visit = generator.Generate(catalog.Appearances.Rows.Keys.OrderBy(x => x).ToArray(),
                catalog.Dispositions.Rows.Values.OrderBy(x => x.Idx).ToArray(), available);
            if (visit == null)
            {
                CurrentVisit = null;
                appearanceImage.enabled = false;
                identityText.text = "생성된 손님 없음";
                orderText.text = "현재 판매 가능한 상품이 없습니다.";
                statusText.text = "판매 가능 상품 0개 · CSV 수정 후 Play를 다시 시작하세요.";
                return;
            }
            var disposition = catalog.Dispositions.Rows[visit.DispositionIdx];
            var text = new StringBuilder();
            text.AppendLine($"성향: {catalog.Texts.Rows[disposition.NameIdx].Text}");
            text.AppendLine("선호: " + string.Join(", ", disposition.PreferredCategoryIds.Select(x => catalog.Texts.Rows[catalog.Categories.Rows[x].NameIdx].Text)));
            text.AppendLine($"선호 선택 {disposition.PreferredSelectionPercent}%\n");
            foreach (var item in visit.Items)
            {
                var product = catalog.Products.Rows[item.ProductIdx];
                text.AppendLine($"{catalog.Texts.Rows[product.NameIdx].Text} × {item.Quantity}  [PK {item.ProductIdx}]");
            }
            var appearance = catalog.Appearances.Rows[visit.AppearanceIdx];
            var color = new Color32(appearance.ColorR, appearance.ColorG, appearance.ColorB, appearance.ColorA);

            // 결과를 모두 준비한 뒤 방문과 화면을 함께 교체한다.
            CurrentVisit = visit;
            appearanceImage.color = color;
            appearanceImage.enabled = true;
            identityText.text = $"외형 {visit.AppearanceIdx} + 성향 {visit.DispositionIdx}";
            orderText.text = text.ToString();
            statusText.text = $"생성 #{++generationCount} · {visit.Items.Count}종 / 총 {visit.Items.Sum(x => (long)x.Quantity)}개";
        }
        catch (Exception exception)
        {
            generateButton.interactable = false;
            statusText.text = "생성 실패 · Console을 확인하세요.";
            Debug.LogException(exception, this);
        }
    }

    /// <summary>비활성화 중 버튼 callback이 남지 않도록 해제한다.</summary>
    private void OnDisable()
    {
        if (generateButton != null) generateButton.onClick.RemoveListener(GenerateCustomer);
    }

    /// <summary>이 화면이 생성한 임시 표시 리소스만 해제한다.</summary>
    private void OnDestroy()
    {
        if (squareSprite != null) Destroy(squareSprite);
        if (displayFont != null) Destroy(displayFont);
    }
}
