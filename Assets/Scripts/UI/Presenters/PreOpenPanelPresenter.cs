using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller가 전달한 영업 전 일일지침·당일 상품 스냅샷만 렌더링합니다.
/// </summary>
public class PreOpenPanelPresenter : MonoBehaviour
{
    [Serializable]
    public class ProductCardSlot
    {
        [Tooltip("슬롯 루트 오브젝트")]
        public GameObject root;

        [Tooltip("상품 아이콘 이미지")]
        public Image iconImage;

        [Tooltip("상품명 텍스트")]
        public TextMeshProUGUI nameText;

        [Tooltip("상품 가격 텍스트")]
        public TextMeshProUGUI priceText;
    }

    [Header("Day & Heading")]
    [Tooltip("우측 상단 일자 텍스트 (예: 1일차)")]
    [SerializeField] private TextMeshProUGUI dayText;

    [Tooltip("상단 안내 제목 (영업 전, 가격을 기억하세요)")]
    [SerializeField] private TextMeshProUGUI headingText;

    [Tooltip("지침 소제목 (오늘의 지침)")]
    [SerializeField] private TextMeshProUGUI ruleTitleText;

    [Tooltip("최대 2개의 일일지침 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI[] guidelineTexts = new TextMeshProUGUI[2];

    [Header("Product Grid Slots (2x4)")]
    [Tooltip("2열 4행 상품 카드 슬롯 목록")]
    [SerializeField] private ProductCardSlot[] productSlots = new ProductCardSlot[8];

    [Header("Notice Labels")]
    [Tooltip("영업 시작 후 일일지침을 다시 확인할 수 없음을 알리는 안내")]
    [SerializeField] private TextMeshProUGUI restrictionNoticeText;

    [Header("Action Controls")]
    [Tooltip("영업 시작 버튼")]
    [SerializeField] private Button openBusinessButton;

    [Header("Temporary Test Controls")]
    [SerializeField] private Button debugDay10Button;
    [SerializeField] private Button debugDay20Button;
    /// <summary>엔딩 전날부터 진행할 임시 테스트 버튼.</summary>
    [SerializeField] private Button debugDay30Button;

    /// <summary>영업 시작 버튼의 공개 참조입니다.</summary>
    public Button OpenBusinessButton => this.openBusinessButton;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>10일차 이동용 임시 테스트 버튼입니다.</summary>
    public Button DebugDay10Button => this.debugDay10Button;
    /// <summary>20일차 이동용 임시 테스트 버튼입니다.</summary>
    public Button DebugDay20Button => this.debugDay20Button;
    /// <summary>30일차 이동용 임시 테스트 버튼입니다.</summary>
    public Button DebugDay30Button => this.debugDay30Button;
#endif

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (this.debugDay10Button != null) this.debugDay10Button.gameObject.SetActive(true);
        if (this.debugDay20Button != null) this.debugDay20Button.gameObject.SetActive(true);
        if (this.debugDay30Button != null) this.debugDay30Button.gameObject.SetActive(true);
#else
        if (this.debugDay10Button != null) this.debugDay10Button.gameObject.SetActive(false);
        if (this.debugDay20Button != null) this.debugDay20Button.gameObject.SetActive(false);
        if (this.debugDay30Button != null) this.debugDay30Button.gameObject.SetActive(false);
#endif
    }

    /// <summary>
    /// Controller가 완성한 지침서 스냅샷만 사용해 화면을 갱신합니다.
    /// </summary>
    /// <param name="viewData">일일 지침서 표시용 스냅샷입니다.</param>
    public void UpdateView(PreOpenGuidelineViewData viewData)
    {
        if (this.dayText != null)
        {
            this.dayText.text = $"{viewData.Day}일차";
        }

        if (this.headingText != null)
        {
            this.headingText.text = "영업 전, 가격을 기억하세요";
        }

        if (this.ruleTitleText != null)
        {
            this.ruleTitleText.text = "오늘의 지침";
        }

        updateGuidelineSlots(viewData.Guidelines);

        if (this.restrictionNoticeText != null)
        {
            this.restrictionNoticeText.text = viewData.Notice;
        }

        if (this.openBusinessButton != null)
        {
            this.openBusinessButton.interactable = viewData.CanOpenBusiness;
        }

        if (this.productSlots != null)
        {
            int productCount = viewData.Products != null ? viewData.Products.Count : 0;
            for (int i = 0; i < this.productSlots.Length; i++)
            {
                ProductCardSlot slot = this.productSlots[i];
                if (slot == null || slot.root == null) continue;

                if (i < productCount)
                {
                    PriceGuideProductViewData product = viewData.Products[i];
                    slot.root.SetActive(true);

                    if (slot.iconImage != null)
                    {
                        slot.iconImage.sprite = product.Icon;
                        slot.iconImage.color = product.Icon != null ? Color.white : Color.clear;
                    }

                    if (slot.nameText != null)
                    {
                        slot.nameText.text = product.Name;
                    }

                    if (slot.priceText != null)
                    {
                        slot.priceText.text = $"{product.Price:N0}원";
                    }
                }
                else
                {
                    slot.root.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 수동 또는 에디터 도구에서 컴포넌트 참조를 연결할 때 사용하는 바인딩 메서드입니다.
    /// </summary>
    public void BindComponents(
        TextMeshProUGUI dayDisplay,
        TextMeshProUGUI headingDisplay,
        TextMeshProUGUI ruleTitleDisplay,
        TextMeshProUGUI[] guidelineDisplays,
        ProductCardSlot[] slots,
        TextMeshProUGUI restrictionDisplay,
        Button openButton)
    {
        this.dayText = dayDisplay;
        this.headingText = headingDisplay;
        this.ruleTitleText = ruleTitleDisplay;
        this.guidelineTexts = guidelineDisplays;
        this.productSlots = slots;
        this.restrictionNoticeText = restrictionDisplay;
        this.openBusinessButton = openButton;
    }

    /// <summary>
    /// 최대 두 개의 일일지침을 각 슬롯에 표시하고 사용하지 않는 슬롯을 비활성화합니다.
    /// </summary>
    /// <param name="guidelines">표시할 일일지침 목록입니다.</param>
    private void updateGuidelineSlots(System.Collections.Generic.IReadOnlyList<DailyGuidelineViewData> guidelines)
    {
        if (this.guidelineTexts == null) return;

        int guidelineCount = guidelines != null ? guidelines.Count : 0;
        for (int i = 0; i < this.guidelineTexts.Length; i++)
        {
            TextMeshProUGUI guidelineText = this.guidelineTexts[i];
            if (guidelineText == null) continue;

            bool hasGuideline = i < guidelineCount;
            bool showsEmptyState = guidelineCount == 0 && i == 0;
            guidelineText.gameObject.SetActive(hasGuideline || showsEmptyState);
            guidelineText.text = hasGuideline
                ? $"{i + 1}. {guidelines[i].Content}"
                : showsEmptyState ? "지침 없음." : string.Empty;
        }
    }
}
