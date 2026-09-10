using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영업 전 일일 지침서(가격표) 화면을 렌더링하는 Presenter.
/// 지침, 당일 등장 상품 가격표 카드 4종, 일자 배지를 표시합니다.
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

    [Tooltip("지침 본문 내용 (제한 없음.)")]
    [SerializeField] private TextMeshProUGUI ruleContentText;

    [Header("Product Grid Slots (2x2)")]
    [Tooltip("2열 2행 상품 카드 슬롯 목록")]
    [SerializeField] private ProductCardSlot[] productSlots = new ProductCardSlot[4];

    [Header("Notice Labels")]
    [Tooltip("하단 주의사항 1 (영업이 시작되면 가격표를 다시 볼 수 없습니다.)")]
    [SerializeField] private TextMeshProUGUI restrictionNoticeText;

    [Tooltip("하단 주의사항 2 (당일 지침은 영업 중에도 다시 확인할 수 있습니다.)")]
    [SerializeField] private TextMeshProUGUI recheckNoticeText;

    [Header("Action Controls")]
    [Tooltip("영업 시작 버튼")]
    [SerializeField] private Button openBusinessButton;

    /// <summary>영업 시작 버튼의 공개 참조입니다.</summary>
    public Button OpenBusinessButton => this.openBusinessButton;

    /// <summary>패널이 활성화될 때 기본 지침서 스냅샷을 자체 렌더링합니다.</summary>
    private void OnEnable()
    {
        int day = GameSessionManager.Instance != null
            ? checked((int)GameSessionManager.Instance.ElapsedDays + 1)
            : 1;

        if (DataTableManager.Instance != null
            && DataTableManager.Instance.Customers != null
            && DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text) != null)
        {
            var factory = new ProgressViewDataFactory(
                DataTableManager.Instance.Customers,
                DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text),
                new System.Collections.Generic.Dictionary<uint, Sprite>(),
                DataTableManager.Instance.GetDB<DailyGuidelineDataTable>(DataTableType.DailyGuideline));
            this.UpdateView(factory.CreatePreOpenGuidelineViewData(day));
        }
        else
        {
            var defaultProducts = new[]
            {
                new PriceGuideProductViewData(1001, "물", 100, null),
                new PriceGuideProductViewData(1004, "통조림", 250, null),
                new PriceGuideProductViewData(1007, "붕대", 300, null),
                new PriceGuideProductViewData(1010, "건전지", 200, null),
            };

            var fallbackData = new PreOpenGuidelineViewData(
                day,
                "영업 전, 가격을 기억하세요",
                "오늘의 지침",
                "제한 없음.",
                defaultProducts,
                "영업이 시작되면 가격표를 다시 볼 수 없습니다.",
                "당일 지침은 영업 중에도 다시 확인할 수 있습니다.");

            this.UpdateView(fallbackData);
        }
    }

    /// <summary>
    /// 지침서 스냅샷 데이터를 기반으로 화면을 갱신합니다.
    /// 추후 CSV 데이터가 연동되면 이 메서드에 전달되는 스냅샷을 통해 텍스트가 자동 반영됩니다.
    /// </summary>
    /// <param name="viewData">일일 지침서 표시용 스냅샷입니다.</param>
    public void UpdateView(PreOpenGuidelineViewData viewData)
    {
        if (this.dayText != null)
        {
            this.dayText.text = $"{viewData.Day}일차";
        }

        if (this.headingText != null && !string.IsNullOrEmpty(viewData.Heading))
        {
            this.headingText.text = viewData.Heading;
        }

        if (this.ruleTitleText != null && !string.IsNullOrEmpty(viewData.RuleTitle))
        {
            this.ruleTitleText.text = viewData.RuleTitle;
        }

        if (this.ruleContentText != null)
        {
            this.ruleContentText.text = viewData.RuleContent;
        }

        if (this.restrictionNoticeText != null && !string.IsNullOrEmpty(viewData.RestrictionNotice))
        {
            this.restrictionNoticeText.text = viewData.RestrictionNotice;
        }

        if (this.recheckNoticeText != null && !string.IsNullOrEmpty(viewData.RecheckNotice))
        {
            this.recheckNoticeText.text = viewData.RecheckNotice;
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
        TextMeshProUGUI ruleContentDisplay,
        ProductCardSlot[] slots,
        TextMeshProUGUI restrictionDisplay,
        TextMeshProUGUI recheckDisplay,
        Button openButton)
    {
        this.dayText = dayDisplay;
        this.headingText = headingDisplay;
        this.ruleTitleText = ruleTitleDisplay;
        this.ruleContentText = ruleContentDisplay;
        this.productSlots = slots;
        this.restrictionNoticeText = restrictionDisplay;
        this.recheckNoticeText = recheckDisplay;
        this.openBusinessButton = openButton;
    }
}
