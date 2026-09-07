using System;
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
    [Header("Customer Root & Appearance")]
    [Tooltip("손님 관련 UI 루트 오브젝트")]
    [SerializeField] private GameObject customerUIRoot;

    [Tooltip("손님 외형 이미지 (색상 또는 스프라이트)")]
    [SerializeField] private Image appearanceImage;

    [Header("Dialogue & Text")]
    [Tooltip("손님 대사 텍스트 (입장 인사 또는 판정 후 반응)")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Basket Display")]
    [Tooltip("장바구니 물품들이 배치되는 컨테이너 트랜스폼")]
    [SerializeField] private Transform basketContainer;

    [Tooltip("개별 물품 렌더링을 위한 콜백 (프리팹 또는 동적 오브젝트 생성기)")]
    private Action<Transform, CustomerBasketItemViewData> itemRendererCallback;

    /// <summary>
    /// 외부 손님·거래 시스템에서 전달된 손님 스냅샷을 기반으로 UI를 갱신합니다.
    /// </summary>
    public void UpdateView(CustomerViewData viewData)
    {
        if (!viewData.HasCustomer)
        {
            this.clearCustomerView();
            return;
        }

        if (this.customerUIRoot != null)
        {
            this.customerUIRoot.SetActive(true);
        }

        // 1. 외형 렌더링 (스프라이트 우선, 없으면 색상 블록)
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
                this.appearanceImage.color = viewData.AppearanceColor != Color.clear ? viewData.AppearanceColor : Color.white;
            }
            this.appearanceImage.gameObject.SetActive(true);
        }

        // 2. 대사 렌더링
        if (this.dialogueText != null)
        {
            this.dialogueText.text = !string.IsNullOrEmpty(viewData.DialogueText) ? viewData.DialogueText : "...";
        }

        // 3. 장바구니 렌더링
        this.renderBasket(viewData.Basket);
    }

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    public void Bind(GameObject root, Image appearanceImg, TextMeshProUGUI dialogueTxt, Transform basketCont, Action<Transform, CustomerBasketItemViewData> customItemRenderer = null)
    {
        this.customerUIRoot = root;
        this.appearanceImage = appearanceImg;
        this.dialogueText = dialogueTxt;
        this.basketContainer = basketCont;
        this.itemRendererCallback = customItemRenderer;
    }

    private void clearCustomerView()
    {
        if (this.customerUIRoot != null)
        {
            this.customerUIRoot.SetActive(false);
        }

        if (this.dialogueText != null)
        {
            this.dialogueText.text = string.Empty;
        }

        if (this.appearanceImage != null)
        {
            this.appearanceImage.gameObject.SetActive(false);
        }

        this.clearBasketChildren();
    }

    private void renderBasket(IReadOnlyList<CustomerBasketItemViewData> basket)
    {
        this.clearBasketChildren();

        if (this.basketContainer == null || basket == null || basket.Count == 0) return;

        foreach (var item in basket)
        {
            if (this.itemRendererCallback != null)
            {
                this.itemRendererCallback(this.basketContainer, item);
            }
            else
            {
                this.defaultRenderItem(this.basketContainer, item);
            }
        }
    }

    private void clearBasketChildren()
    {
        if (this.basketContainer == null) return;
        for (int i = this.basketContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(this.basketContainer.GetChild(i).gameObject);
        }
    }

    private void defaultRenderItem(Transform parent, CustomerBasketItemViewData item)
    {
        var itemObj = new GameObject($"BasketItem_{item.ItemId}");
        itemObj.transform.SetParent(parent, false);

        var text = itemObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 18;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.text = $"{item.DisplayName} x {item.Quantity}";
    }
}
