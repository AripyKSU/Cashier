using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3.5 CustomerPresenter
/// 현재 손님의 외형, 대화와 장바구니 품목을 화면에 표시하는 Presenter.
/// 손님이 없는 상태(HasCustomer=false)에서는 관련 UI를 초기화하거나 숨깁니다.
/// 손님 생성, 예산, 인내도와 가격 수락 여부를 UI가 판단하지 않으며, 전달받은 스냅샷만 렌더링합니다.
/// </summary>
public class CustomerPresenter : MonoBehaviour
{
    private const int RandomPlacementAttempts = 32;

    [Header("Customer Root & Appearance")]
    [Tooltip("손님 관련 UI 루트 오브젝트")]
    [SerializeField] private GameObject customerUIRoot;

    [Tooltip("손님 외형 Sprite 이미지")]
    [SerializeField] private Image appearanceImage;

    [Tooltip("임시 외형 이미지 위에 성별을 표시하는 TextMeshPro 텍스트")]
    [SerializeField] private TextMeshProUGUI temporaryGenderText;

    [Header("Dialogue & Text")]
    [Tooltip("손님 대사 텍스트 (입장 인사 또는 판정 후 반응)")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Tooltip("말풍선 루트 오브젝트 (배경 패널)")]
    [SerializeField] private GameObject speechBubbleRoot;

    [Tooltip("말풍선 9-슬라이스 프레임 스프라이트 (DialogueFrame)")]
    [SerializeField] private Sprite dialogueFrameSprite;

    [Tooltip("손님 대사 폰트 (Mabinogi_Classic_OTF SDF)")]
    [SerializeField] private TMP_FontAsset dialogueFont;

    /// <summary>말풍선 루트 게임오브젝트</summary>
    public GameObject SpeechBubbleRoot
    {
        get => this.speechBubbleRoot;
        set => this.speechBubbleRoot = value;
    }

    /// <summary>손님 대사 텍스트 컴포넌트</summary>
    public TextMeshProUGUI DialogueText
    {
        get => this.dialogueText;
        set => this.dialogueText = value;
    }

    /// <summary>말풍선 9-슬라이스 프레임 스프라이트</summary>
    public Sprite DialogueFrameSprite
    {
        get => this.dialogueFrameSprite;
        set => this.dialogueFrameSprite = value;
    }

    /// <summary>손님 대사 폰트 에셋</summary>
    public TMP_FontAsset DialogueFont
    {
        get => this.dialogueFont;
        set => this.dialogueFont = value;
    }

    [Header("Basket Display")]
    [Tooltip("장바구니 물품들이 배치되는 컨테이너 트랜스폼")]
    [SerializeField] private Transform basketContainer;

    [Tooltip("장바구니 한 항목을 표시하는 UI 프리팹")]
    [SerializeField] private CustomerBasketItemPresenter basketItemPrefab;

    [Tooltip("랜덤 배치된 장바구니 항목 사이에 확보할 최소 간격(픽셀)")]
    [SerializeField] private float basketItemSpacingPixels = 8f;

    // 생성한 프리팹 인스턴스를 재사용해 거래 갱신 시 파괴와 재생성을 피합니다.
    private readonly List<CustomerBasketItemPresenter> basketItems = new List<CustomerBasketItemPresenter>();

    // 동일한 장바구니를 다시 표시할 때 기존 랜덤 위치를 유지하기 위한 구성 식별값입니다.
    private int basketSignature;

    /// <summary>프리팹에 별도 라벨이 연결되지 않은 경우 외형 이미지 자식으로 임시 성별 라벨을 만듭니다.</summary>
    private void Awake()
    {
        this.ensureTemporaryGenderText();
        this.ensureSpeechBubble();
    }

    /// <summary>
    /// 외부 손님·거래 시스템에서 전달된 손님 스냅샷을 기반으로 UI를 갱신합니다.
    /// </summary>
    /// <param name="viewData">표시할 손님과 장바구니 스냅샷입니다.</param>
    public void UpdateView(CustomerViewData viewData)
    {
        this.ensureSpeechBubble();
        if (!viewData.HasCustomer)
        {
            this.clearCustomerView();
            return;
        }

        if (this.customerUIRoot != null)
        {
            this.customerUIRoot.SetActive(true);
        }

        // 필수 외형은 공용 로드 경계에서 검증된 Sprite를 전달받는다.
        if (this.appearanceImage != null)
        {
            if (viewData.AppearanceSprite != null)
            {
                this.appearanceImage.sprite = viewData.AppearanceSprite;
                this.appearanceImage.color = Color.white;
            }
            else
            {
                this.appearanceImage.sprite = null;
                this.appearanceImage.color = Color.white;
            }
            this.appearanceImage.preserveAspect = true;
            this.appearanceImage.gameObject.SetActive(true);
        }

        this.ensureTemporaryGenderText();
        if (this.temporaryGenderText != null)
        {
            CustomerAttributes gender = viewData.Attributes & (CustomerAttributes.Male | CustomerAttributes.Female);
            this.temporaryGenderText.text = gender == CustomerAttributes.Male
                ? "남성"
                : gender == CustomerAttributes.Female ? "여성" : "성별 미지정";
            this.temporaryGenderText.gameObject.SetActive(true);
        }

        // 2. 대사 및 말풍선 렌더링
        bool hasDialogue = !string.IsNullOrEmpty(viewData.DialogueText);
        if (this.speechBubbleRoot != null)
        {
            this.speechBubbleRoot.SetActive(hasDialogue);
        }

        if (this.dialogueText != null)
        {
            this.dialogueText.gameObject.SetActive(hasDialogue);
            this.dialogueText.text = hasDialogue ? viewData.DialogueText : "...";
        }

        // 3. 장바구니 렌더링
        this.renderBasket(viewData.Basket);
    }

    /// <summary>손님이 없을 때 관련 표시를 비우고 숨깁니다.</summary>
    private void clearCustomerView()
    {
        if (this.customerUIRoot != null)
        {
            this.customerUIRoot.SetActive(false);
        }

        if (this.speechBubbleRoot != null)
        {
            this.speechBubbleRoot.SetActive(false);
        }

        if (this.dialogueText != null)
        {
            this.dialogueText.text = string.Empty;
            this.dialogueText.gameObject.SetActive(false);
        }

        if (this.appearanceImage != null)
        {
            this.appearanceImage.gameObject.SetActive(false);
        }

        if (this.temporaryGenderText != null)
        {
            this.temporaryGenderText.text = string.Empty;
            this.temporaryGenderText.gameObject.SetActive(false);
        }

        this.hideBasketItems();
    }

    /// <summary>필요한 프리팹 항목을 확보하고 장바구니 데이터를 표시합니다.</summary>
    /// <param name="basket">표시할 장바구니 스냅샷입니다.</param>
    private void renderBasket(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        int nextBasketSignature = this.calculateBasketSignature(basket);
        bool shouldPlaceItems = nextBasketSignature != this.basketSignature;
        this.hideBasketItems();
        if (this.basketContainer == null || this.basketItemPrefab == null || basket == null)
        {
            this.basketSignature = 0;
            return;
        }

        int totalQuantity = this.getTotalQuantity(basket);
        var occupiedAreas = new List<Rect>(totalQuantity);
        int instanceIndex = 0;
        for (int basketIndex = 0; basketIndex < basket.Count; basketIndex++)
        {
            CustomerBasketItemViewData basketItem = basket[basketIndex];
            for (int quantityIndex = 0; quantityIndex < basketItem.Quantity; quantityIndex++)
            {
                if (instanceIndex >= this.basketItems.Count)
                {
                    this.basketItems.Add(Instantiate(this.basketItemPrefab, this.basketContainer));
                }

                CustomerBasketItemPresenter itemPresenter = this.basketItems[instanceIndex];
                itemPresenter.gameObject.SetActive(true);
                itemPresenter.UpdateView(basketItem);
                if (shouldPlaceItems)
                {
                    this.placeBasketItem(itemPresenter, instanceIndex, totalQuantity, occupiedAreas);
                }

                instanceIndex++;
            }
        }

        this.basketSignature = nextBasketSignature;
    }

    /// <summary>생성한 장바구니 항목을 다음 손님을 위해 비활성화합니다.</summary>
    private void hideBasketItems()
    {
        foreach (CustomerBasketItemPresenter item in this.basketItems)
        {
            if (item != null)
            {
                item.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>장바구니 항목을 컨테이너 안의 겹치지 않는 임의 위치에 배치합니다.</summary>
    /// <param name="itemPresenter">배치할 항목 Presenter입니다.</param>
    /// <param name="itemIndex">안전 배치에 사용할 현재 항목 순서입니다.</param>
    /// <param name="itemCount">현재 장바구니의 전체 항목 수입니다.</param>
    /// <param name="occupiedAreas">이미 배치된 항목의 충돌 영역입니다.</param>
    private void placeBasketItem(
        CustomerBasketItemPresenter itemPresenter,
        int itemIndex,
        int itemCount,
        List<Rect> occupiedAreas)
    {
        if (!(this.basketContainer is RectTransform containerRect)
            || !(itemPresenter.transform is RectTransform itemRect))
        {
            return;
        }

        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        Vector2 itemSize = itemRect.rect.size;
        float halfWidth = itemSize.x * 0.5f;
        float halfHeight = itemSize.y * 0.5f;
        float minX = containerRect.rect.xMin + halfWidth;
        float maxX = containerRect.rect.xMax - halfWidth;
        float minY = containerRect.rect.yMin + halfHeight;
        float maxY = containerRect.rect.yMax - halfHeight;

        for (int attempt = 0; attempt < RandomPlacementAttempts; attempt++)
        {
            Vector2 position = new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY));
            Rect occupiedArea = this.createOccupiedArea(position, itemSize);
            if (!this.overlapsAny(occupiedArea, occupiedAreas))
            {
                itemRect.anchoredPosition = position;
                occupiedAreas.Add(occupiedArea);
                return;
            }
        }

        // 좁은 영역이나 항목 과다로 임의 배치가 실패해도 겹침을 최소화하는 규칙적 위치를 사용합니다.
        int columns = Mathf.Max(1, Mathf.FloorToInt(
            containerRect.rect.width / (itemSize.x + this.basketItemSpacingPixels)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)itemCount / columns));
        int column = itemIndex % columns;
        int row = itemIndex / columns;
        float xStep = columns <= 1 ? 0f : (maxX - minX) / (columns - 1);
        float yStep = rows <= 1 ? 0f : (maxY - minY) / (rows - 1);
        Vector2 fallbackPosition = new Vector2(minX + (column * xStep), maxY - (row * yStep));
        itemRect.anchoredPosition = fallbackPosition;
        occupiedAreas.Add(this.createOccupiedArea(fallbackPosition, itemSize));
    }

    /// <summary>간격을 포함한 항목 충돌 영역을 생성합니다.</summary>
    /// <param name="position">컨테이너 중앙 기준 항목 위치입니다.</param>
    /// <param name="itemSize">항목의 실제 크기입니다.</param>
    /// <returns>최소 간격을 포함한 충돌 영역입니다.</returns>
    private Rect createOccupiedArea(Vector2 position, Vector2 itemSize)
    {
        Vector2 paddedSize = itemSize + (Vector2.one * Mathf.Max(0f, this.basketItemSpacingPixels));
        return new Rect(position - (paddedSize * 0.5f), paddedSize);
    }

    /// <summary>후보 영역이 기존 항목 영역과 겹치는지 확인합니다.</summary>
    /// <param name="candidate">검사할 후보 충돌 영역입니다.</param>
    /// <param name="occupiedAreas">이미 사용 중인 충돌 영역 목록입니다.</param>
    /// <returns>하나 이상의 기존 영역과 겹치면 true입니다.</returns>
    private bool overlapsAny(Rect candidate, IReadOnlyList<Rect> occupiedAreas)
    {
        foreach (Rect occupiedArea in occupiedAreas)
        {
            if (candidate.Overlaps(occupiedArea))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>장바구니 구성 변경 여부를 판별할 안정적인 식별값을 계산합니다.</summary>
    /// <param name="basket">식별할 장바구니 스냅샷입니다.</param>
    /// <returns>상품 식별자와 수량으로 계산한 구성 식별값입니다.</returns>
    private int calculateBasketSignature(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        if (basket == null || basket.Count == 0)
        {
            return 0;
        }

        unchecked
        {
            int signature = 17;
            foreach (CustomerBasketItemViewData item in basket)
            {
                signature = (signature * 31) + item.ItemId.GetHashCode();
                signature = (signature * 31) + item.Quantity;
            }

            return signature;
        }
    }

    /// <summary>장바구니에 표시할 개별 상품 이미지의 총개수를 계산합니다.</summary>
    /// <param name="basket">수량이 확정된 장바구니 스냅샷입니다.</param>
    /// <returns>모든 상품 수량의 합계입니다.</returns>
    private int getTotalQuantity(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        int totalQuantity = 0;
        foreach (CustomerBasketItemViewData item in basket)
        {
            totalQuantity = checked(totalQuantity + Mathf.Max(0, item.Quantity));
        }

        return totalQuantity;
    }

    /// <summary>임시 외형 이미지 위에 표시할 성별 TMP 라벨을 확보합니다.</summary>
    private void ensureTemporaryGenderText()
    {
        if (this.temporaryGenderText != null || this.appearanceImage == null)
        {
            return;
        }

        Transform existing = this.appearanceImage.transform.Find("TemporaryGender");
        if (existing != null)
        {
            this.temporaryGenderText = existing.GetComponent<TextMeshProUGUI>();
        }

        if (this.temporaryGenderText == null)
        {
            GameObject labelObject = new GameObject("TemporaryGender", typeof(RectTransform));
            labelObject.transform.SetParent(this.appearanceImage.transform, false);
            this.temporaryGenderText = labelObject.AddComponent<TextMeshProUGUI>();
        }

        if (this.dialogueText != null)
        {
            this.temporaryGenderText.font = this.dialogueText.font;
            this.temporaryGenderText.fontSharedMaterial = this.dialogueText.fontSharedMaterial;
        }

        RectTransform labelRect = this.temporaryGenderText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(2f, 2f);
        labelRect.offsetMax = new Vector2(-2f, -2f);
        labelRect.localScale = Vector3.one;
        this.temporaryGenderText.alignment = TextAlignmentOptions.Center;
        this.temporaryGenderText.fontSize = 22f;
        this.temporaryGenderText.color = Color.white;
        this.temporaryGenderText.outlineWidth = 0.25f;
        this.temporaryGenderText.outlineColor = Color.black;
        this.temporaryGenderText.raycastTarget = false;
        this.temporaryGenderText.transform.SetAsLastSibling();
        this.temporaryGenderText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 프로토타입 규격에 맞추어 손님 앞에 9-슬라이스 메탈 프레임(DialogueFrame) 말풍선 패널을 구성합니다.
    /// </summary>
    private void ensureSpeechBubble()
    {
        // 단위 테스트 등 외부에서 임의의 speechBubbleRoot를 주입한 경우 보존
        if (this.speechBubbleRoot != null && this.speechBubbleRoot.name != "DialoguePanel" && this.dialogueText != null)
        {
            if (this.dialogueFont != null && this.dialogueText.font != this.dialogueFont)
            {
                this.dialogueText.font = this.dialogueFont;
                this.dialogueText.fontSharedMaterial = this.dialogueFont.material;
            }
            return;
        }

        if (this.speechBubbleRoot != null && this.speechBubbleRoot.name == "DialoguePanel" && this.dialogueText != null)
        {
            if (this.dialogueFont != null && this.dialogueText.font != this.dialogueFont)
            {
                this.dialogueText.font = this.dialogueFont;
                this.dialogueText.fontSharedMaterial = this.dialogueFont.material;
            }
            return;
        }

#if UNITY_EDITOR
        if (this.dialogueFont == null)
        {
            this.dialogueFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Mabinogi_Classic_OTF SDF.asset");
        }

        if (this.dialogueFrameSprite == null)
        {
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/DystopiaPrototype/Art/DialogueFrame.png");
            foreach (var a in assets)
            {
                if (a is Sprite s)
                {
                    this.dialogueFrameSprite = s;
                    if (s.name == "DialogueFrame") break;
                }
            }
        }
#endif

        Transform panel = null;
        Transform frontView = this.transform.Find("AstraFrontView");
        if (frontView != null) panel = frontView.Find("DialoguePanel");
        if (panel == null) panel = this.transform.Find("DialoguePanel");
        if (panel == null && this.transform.parent != null)
        {
            Transform parentFront = this.transform.parent.Find("AstraFrontView");
            if (parentFront != null) panel = parentFront.Find("DialoguePanel");
            if (panel == null) panel = this.transform.parent.Find("DialoguePanel");
        }

        Transform targetParent = frontView ?? (this.transform.parent != null ? this.transform.parent : this.transform);

        if (panel == null)
        {
            GameObject panelGo = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Canvas), typeof(Image));
            panelGo.transform.SetParent(targetParent, false);
            panelGo.transform.SetAsLastSibling();

            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -34f);
            panelRect.sizeDelta = new Vector2(560f, 70f);

            // 손님 및 배경보다 무조건 앞에 그려지도록 Canvas Sorting Order 지정
            Canvas canvas = panelGo.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;

            Image frameImg = panelGo.GetComponent<Image>();
            frameImg.sprite = this.dialogueFrameSprite;
            frameImg.type = Image.Type.Sliced;
            frameImg.fillCenter = true;
            frameImg.pixelsPerUnitMultiplier = 4f;
            frameImg.color = Color.white;
            frameImg.raycastTarget = false;

            GameObject textGo = new GameObject("Dialogue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panelGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(18f, 10f);
            textRect.offsetMax = new Vector2(-18f, -10f);

            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (this.dialogueFont != null)
            {
                tmp.font = this.dialogueFont;
                tmp.fontSharedMaterial = this.dialogueFont.material;
            }

            tmp.text = "...";
            tmp.fontSize = 24f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 16f;
            tmp.fontSizeMax = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            Transform oldDialogue = this.transform.Find("Customer/Dialogue") ?? this.transform.Find("Dialogue");
            if (oldDialogue != null && oldDialogue != textGo.transform)
            {
                oldDialogue.gameObject.SetActive(false);
            }

            panelGo.SetActive(false);
            this.speechBubbleRoot = panelGo;
            this.dialogueText = tmp;
        }
        else
        {
            this.speechBubbleRoot = panel.gameObject;
            this.dialogueText = panel.GetComponentInChildren<TextMeshProUGUI>(true);

            panel.SetAsLastSibling();
            Canvas canvas = panel.GetComponent<Canvas>() ?? panel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;

            Image frameImg = panel.GetComponent<Image>();
            if (frameImg != null)
            {
                if (this.dialogueFrameSprite != null && frameImg.sprite == null)
                {
                    frameImg.sprite = this.dialogueFrameSprite;
                }
                else if (frameImg.sprite != null && this.dialogueFrameSprite == null)
                {
                    this.dialogueFrameSprite = frameImg.sprite;
                }
                frameImg.type = Image.Type.Sliced;
                frameImg.fillCenter = true;
                frameImg.pixelsPerUnitMultiplier = 4f;
                frameImg.color = Color.white;
                frameImg.raycastTarget = false;
            }

            if (this.dialogueFont != null && this.dialogueText != null && this.dialogueText.font != this.dialogueFont)
            {
                this.dialogueText.font = this.dialogueFont;
                this.dialogueText.fontSharedMaterial = this.dialogueFont.material;
            }

            Transform oldDialogue = this.transform.Find("Customer/Dialogue") ?? this.transform.Find("Dialogue");
            if (oldDialogue != null && oldDialogue != this.dialogueText?.transform)
            {
                oldDialogue.gameObject.SetActive(false);
            }
        }
    }
}
