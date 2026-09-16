using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>팸플릿 위에서 사람이 이동할 수 있는 가격·구매·SoldOut 표시 한 묶음을 관리한다.</summary>
public sealed class FacilityPamphletSlotView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Image soldOutImage;

    private uint facilityIdx;
    private bool canPurchase;

    /// <summary>사용자가 요청한 설비 PK. 화면 문자열이나 슬롯 순서를 식별자로 사용하지 않는다.</summary>
    public event Action<uint> PurchaseRequested;

    /// <summary>활성 수명에만 구매 버튼 요청을 구독한다.</summary>
    private void OnEnable()
    {
        if (purchaseButton != null) purchaseButton.onClick.AddListener(handlePurchase);
    }

    /// <summary>비활성화 시 구매 버튼 요청을 해제한다.</summary>
    private void OnDisable()
    {
        if (purchaseButton != null) purchaseButton.onClick.RemoveListener(handlePurchase);
    }

    /// <summary>가격과 구매 완료 도장을 현재 설비 상태로 갱신한다.</summary>
    /// <param name="data">표시할 설비 snapshot.</param>
    /// <param name="interactive">상위 팸플릿이 구매 입력을 허용하는지 여부.</param>
    public void UpdateView(FacilityItemViewData data, bool interactive)
    {
        facilityIdx = data.FacilityIdx;
        canPurchase = data.State == FacilityDisplayState.Purchasable;
        if (priceText != null) priceText.text = data.PurchasePrice.ToString("N0");
        bool isOwned = data.State == FacilityDisplayState.ActivationPending ||
            data.State == FacilityDisplayState.Active ||
            data.State == FacilityDisplayState.OwnedStageUpgrade ||
            data.State == FacilityDisplayState.OwnedProgression;
        if (soldOutImage != null) soldOutImage.gameObject.SetActive(isOwned);
        SetInteractionEnabled(interactive);
    }

    /// <summary>표시 상태를 바꾸지 않고 처리 중 구매 입력만 잠근다.</summary>
    /// <param name="interactive">상위 화면의 입력 허용 여부.</param>
    public void SetInteractionEnabled(bool interactive)
    {
        if (purchaseButton != null) purchaseButton.interactable = interactive && canPurchase;
    }

    /// <summary>구매 가능한 상태의 PK만 한 번 전달한다.</summary>
    private void handlePurchase()
    {
        if (isActiveAndEnabled && purchaseButton != null && purchaseButton.interactable && canPurchase)
            PurchaseRequested?.Invoke(facilityIdx);
    }
}
