using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설비 한 행을 표시하고 PK만 요청한다. 구매·보유 상태의 권위는 아니다.</summary>
public sealed class FacilityItemView : MonoBehaviour
{
    /// <summary>설비 이름 표시.</summary>
    [SerializeField] private TextMeshProUGUI nameText;
    /// <summary>원본 구매 가격 표시.</summary>
    [SerializeField] private TextMeshProUGUI priceText;
    /// <summary>해금 상품 이름 목록 표시.</summary>
    [SerializeField] private TextMeshProUGUI unlockProductsText;
    /// <summary>1기반 활성 날짜 표시.</summary>
    [SerializeField] private TextMeshProUGUI activationText;
    /// <summary>구매 가능·부족·대기·사용 중 상태 표시.</summary>
    [SerializeField] private TextMeshProUGUI statusText;
    /// <summary>해당 행의 PK만 요청하는 버튼.</summary>
    [SerializeField] private Button purchaseButton;
    private uint facilityIdx;
    private bool canPurchase;

    /// <summary>사용자가 요청한 설비 PK. 가격·날짜는 전달하지 않는다.</summary>
    public event Action<uint> PurchaseRequested;

    /// <summary>활성 수명에만 클릭 리스너를 연결한다.</summary>
    private void OnEnable() => purchaseButton.onClick.AddListener(handlePurchase);

    /// <summary>비활성 수명에서 리스너를 해제해 반복 열기의 중복 요청을 막는다.</summary>
    private void OnDisable() => purchaseButton.onClick.RemoveListener(handlePurchase);

    /// <summary>스냅샷을 표시한다. 요청 이벤트를 발생시키지 않는다.</summary>
    /// <param name="data">확정 표시값.</param><param name="interactive">상위 패널이 입력을 허용하는지 여부.</param>
    public void UpdateView(FacilityItemViewData data, bool interactive)
    {
        facilityIdx = data.FacilityIdx;
        canPurchase = data.State == FacilityDisplayState.Available;
        nameText.text = data.DisplayName;
        priceText.text = $"{data.PurchasePrice:N0} G";
        unlockProductsText.text = $"해금 상품: {data.UnlockProducts}";
        activationText.text = $"DAY {data.ActivationDisplayDay}부터 사용";
        statusText.text = data.State switch
        {
            FacilityDisplayState.Available => "구매 가능",
            FacilityDisplayState.InsufficientFunds => "잔액 부족",
            FacilityDisplayState.Pending => "구매 완료 · 적용 대기",
            FacilityDisplayState.Active => "사용 중",
            _ => throw new ArgumentOutOfRangeException(nameof(data), "유효한 설비 표시 상태가 필요합니다.")
        };
        SetInteractionEnabled(interactive);
    }

    /// <summary>표시 상태를 바꾸지 않고 처리 중 입력만 잠근다.</summary>
    /// <param name="interactive">상위 화면의 입력 허용 여부.</param>
    public void SetInteractionEnabled(bool interactive) => purchaseButton.interactable = interactive && canPurchase;

    /// <summary>활성 상태의 구매 가능한 행만 단일 PK 요청을 전달한다.</summary>
    private void handlePurchase()
    {
        if (isActiveAndEnabled && purchaseButton.interactable && canPurchase) PurchaseRequested?.Invoke(facilityIdx);
    }
}
