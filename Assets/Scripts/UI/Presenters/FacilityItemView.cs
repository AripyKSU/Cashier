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
    /// <summary>단계·효과 또는 해금 상품 표시.</summary>
    [SerializeField] private TextMeshProUGUI categoryText;
    /// <summary>단계 잠금·구매·대기·사용 중 상태 표시.</summary>
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
        canPurchase = data.State == FacilityDisplayState.Purchasable;
        if (nameText != null) nameText.text = data.UpgradeKind == FacilityUpgradeKind.Citizenship
            ? data.DisplayName : $"[{data.RequiredStoreStage}단계] {data.DisplayName}";
        if (priceText != null) priceText.text = $"{data.PurchasePrice:N0} G";
        if (categoryText != null) categoryText.text = formatCategory(data);
        else if (unlockProductsText != null) unlockProductsText.text = formatCategory(data);
        if (activationText != null) activationText.text = formatActivation(data);
        if (statusText == null) return;
        statusText.text = data.State switch
        {
            FacilityDisplayState.StageLocked => "단계 잠김",
            FacilityDisplayState.Purchasable => "구매 가능",
            FacilityDisplayState.InsufficientFunds => "잔액 부족",
            FacilityDisplayState.ActivationPending => "구매 완료 · 적용 대기",
            FacilityDisplayState.Active => "사용 중",
            FacilityDisplayState.OwnedStageUpgrade => "단계 확장 완료",
            _ => throw new ArgumentOutOfRangeException(nameof(data), "유효한 설비 표시 상태가 필요합니다.")
        };
        SetInteractionEnabled(interactive);
    }

    /// <summary>표시 상태를 바꾸지 않고 처리 중 입력만 잠근다.</summary>
    /// <param name="interactive">상위 화면의 입력 허용 여부.</param>
    public void SetInteractionEnabled(bool interactive) => purchaseButton.interactable = interactive && canPurchase;

    /// <summary>업그레이드 종류와 효과를 사용자 표시 문자열로 변환합니다. 내부 판정 키로 사용하지 않습니다.</summary>
    /// <param name="data">행 snapshot.</param>
    /// <returns>상품·효과·단계 설명.</returns>
    private string formatCategory(FacilityItemViewData data)
    {
        return data.UpgradeKind switch
        {
            FacilityUpgradeKind.ProductUnlock => $"해금 상품: {data.UnlockProducts}",
            FacilityUpgradeKind.Convenience => $"편의성 효과: {formatEffect(data.EffectType)}",
            FacilityUpgradeKind.StoreStage => $"가게 단계 → {data.TargetStoreStage}",
            FacilityUpgradeKind.Citizenship => "나와 딸의 안전구역 입국 자격 · 1회 구매",
            _ => throw new ArgumentOutOfRangeException(nameof(data), "유효한 업그레이드 종류가 필요합니다.")
        };
    }

    /// <summary>행 상태에 맞는 단계·활성 시점 안내를 만듭니다.</summary>
    /// <param name="data">행 snapshot.</param>
    /// <returns>행의 적용 안내.</returns>
    private string formatActivation(FacilityItemViewData data)
    {
        if (data.UpgradeKind == FacilityUpgradeKind.Citizenship)
            return "구매 즉시 보유 · 31일차 최종 확인 전까지 구매 가능";
        if (data.UpgradeKind == FacilityUpgradeKind.StoreStage)
            return $"요구 단계 {data.RequiredStoreStage} · 구매 즉시 해금";
        return data.State == FacilityDisplayState.StageLocked
            ? $"요구 단계 {data.RequiredStoreStage} · 잠금"
            : $"DAY {data.ActivationDisplayDay}부터 사용";
    }

    /// <summary>편의성 enum을 표시용 문자열로 변환합니다.</summary>
    /// <param name="effectType">편의성 효과.</param>
    /// <returns>효과 표시 문자열.</returns>
    private string formatEffect(ConvenienceEffectType effectType)
    {
        return effectType switch
        {
            ConvenienceEffectType.DividerBar => "막대",
            ConvenienceEffectType.AutoSorting => "소팅",
            ConvenienceEffectType.Vacuum => "청소기",
            _ => throw new ArgumentOutOfRangeException(nameof(effectType), "유효한 편의성 효과가 필요합니다.")
        };
    }

    /// <summary>활성 상태의 구매 가능한 행만 단일 PK 요청을 전달한다.</summary>
    private void handlePurchase()
    {
        if (isActiveAndEnabled && purchaseButton.interactable && canPurchase) PurchaseRequested?.Invoke(facilityIdx);
    }
}
