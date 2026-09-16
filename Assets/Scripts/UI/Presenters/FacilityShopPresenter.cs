using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>단계별 팸플릿과 고정 구매 영역을 표시하고 구매·닫기 의도를 Controller에 전달한다.</summary>
public sealed class FacilityShopPresenter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GameObject stage1Panel;
    [SerializeField] private GameObject stage2Panel;
    [SerializeField] private GameObject stage3Panel;
    [SerializeField] private GameObject citizenshipPanel;
    [SerializeField] private FacilityPamphletSlotView[] stage1Slots;
    [SerializeField] private FacilityPamphletSlotView[] stage2Slots;
    [SerializeField] private FacilityPamphletSlotView[] stage3Slots;
    [SerializeField] private FacilityPamphletSlotView citizenshipSlot;
    [SerializeField] private Button stage1ProgressionButton;
    [SerializeField] private Button stage2ProgressionButton;
    [SerializeField] private Button stage3ProgressionButton;
    [SerializeField] private Image stage1ProgressionImage;
    [SerializeField] private Image stage2ProgressionImage;
    [SerializeField] private Image stage3ProgressionImage;
    [SerializeField] private Sprite purchasedAllToUnlockSprite;
    [SerializeField] private Sprite upgradeToNextFacilitySprite;
    [SerializeField] private Sprite upgradeToCivilizationSprite;
    [SerializeField] private Button outsideCloseButton;

    private FacilityShopViewData currentData;
    private bool interactive = true;
    private bool isCitizenshipPageOpen;

    /// <summary>단일 설비 PK의 구매 요청.</summary>
    public event Action<uint> OnPurchaseRequested;

    /// <summary>날짜 변경 없이 패널만 닫는 요청.</summary>
    public event Action OnCloseRequested;

    /// <summary>열린 수명에만 고정 슬롯과 팸플릿 버튼 요청을 구독한다.</summary>
    private void OnEnable()
    {
        subscribeSlots(stage1Slots, true);
        subscribeSlots(stage2Slots, true);
        subscribeSlots(stage3Slots, true);
        if (citizenshipSlot != null) citizenshipSlot.PurchaseRequested += handlePurchase;
        if (stage1ProgressionButton != null) stage1ProgressionButton.onClick.AddListener(handleProgression);
        if (stage2ProgressionButton != null) stage2ProgressionButton.onClick.AddListener(handleProgression);
        if (stage3ProgressionButton != null) stage3ProgressionButton.onClick.AddListener(handleProgression);
        if (outsideCloseButton != null) outsideCloseButton.onClick.AddListener(handleClose);
    }

    /// <summary>닫기와 씬 종료 시 요청 구독을 해제한다.</summary>
    private void OnDisable()
    {
        subscribeSlots(stage1Slots, false);
        subscribeSlots(stage2Slots, false);
        subscribeSlots(stage3Slots, false);
        if (citizenshipSlot != null) citizenshipSlot.PurchaseRequested -= handlePurchase;
        if (stage1ProgressionButton != null) stage1ProgressionButton.onClick.RemoveListener(handleProgression);
        if (stage2ProgressionButton != null) stage2ProgressionButton.onClick.RemoveListener(handleProgression);
        if (stage3ProgressionButton != null) stage3ProgressionButton.onClick.RemoveListener(handleProgression);
        if (outsideCloseButton != null) outsideCloseButton.onClick.RemoveListener(handleClose);
    }

    /// <summary>현재 단계의 팸플릿, 가격, 구매 완료 도장과 진행 버튼을 갱신한다.</summary>
    /// <param name="data">현재 잔액·단계·설비 스냅샷.</param>
    /// <param name="feedback">구매 결과 안내.</param>
    /// <exception cref="ArgumentNullException">스냅샷이 없음.</exception>
    /// <exception cref="InvalidOperationException">팸플릿 슬롯 구성이 데이터와 다름.</exception>
    public void UpdateView(FacilityShopViewData data, string feedback)
    {
        currentData = data ?? throw new ArgumentNullException(nameof(data));
        if (data.CurrentStoreStage < 3) isCitizenshipPageOpen = false;
        if (feedbackText != null) feedbackText.text = feedback ?? string.Empty;

        FacilityPamphletSlotView[] activeSlots = data.CurrentStoreStage switch
        {
            1 => stage1Slots,
            2 => stage2Slots,
            3 => stage3Slots,
            _ => throw new InvalidOperationException($"지원하지 않는 가게 단계입니다. stage={data.CurrentStoreStage}")
        };
        if (activeSlots == null || activeSlots.Length != data.RegularItems.Count)
            throw new InvalidOperationException($"{data.CurrentStoreStage}단계 팸플릿 슬롯 수가 설비 수와 다릅니다.");
        for (int index = 0; index < activeSlots.Length; index++)
            activeSlots[index].UpdateView(data.RegularItems[index], interactive);

        if (data.CurrentStoreStage == 3 && isCitizenshipPageOpen)
        {
            if (!data.ProgressionItem.HasValue || citizenshipSlot == null)
                throw new InvalidOperationException("시민권 팸플릿 슬롯 또는 표시 데이터가 누락되었습니다.");
            citizenshipSlot.UpdateView(data.ProgressionItem.Value, interactive);
        }

        updatePageVisibility(data.CurrentStoreStage);
        updateProgressionButton(data);
    }

    /// <summary>구매 중 또는 기술 오류 이후 전체 구매·닫기 입력을 제어한다.</summary>
    /// <param name="enabled">구매 요청 허용 여부.</param>
    /// <param name="canClose">닫기 허용 여부.</param>
    public void SetInteractionEnabled(bool enabled, bool canClose = true)
    {
        interactive = enabled;
        setSlotsInteractive(stage1Slots, enabled);
        setSlotsInteractive(stage2Slots, enabled);
        setSlotsInteractive(stage3Slots, enabled);
        if (citizenshipSlot != null) citizenshipSlot.SetInteractionEnabled(enabled);
        if (outsideCloseButton != null) outsideCloseButton.interactable = canClose;
        if (currentData != null) updateProgressionButton(currentData);
    }

    /// <summary>현재 가게 단계와 기억된 시민권 페이지에 맞춰 팸플릿 하나만 표시한다.</summary>
    /// <param name="storeStage">현재 가게 단계.</param>
    private void updatePageVisibility(uint storeStage)
    {
        if (stage1Panel != null) stage1Panel.SetActive(storeStage == 1);
        if (stage2Panel != null) stage2Panel.SetActive(storeStage == 2);
        if (stage3Panel != null) stage3Panel.SetActive(storeStage == 3 && !isCitizenshipPageOpen);
        if (citizenshipPanel != null) citizenshipPanel.SetActive(storeStage == 3 && isCitizenshipPageOpen);
    }

    /// <summary>완료 전 안내와 완료 후 단계 진행 이미지를 교체하고 클릭 가능 상태를 맞춘다.</summary>
    /// <param name="data">현재 단계 표시 스냅샷.</param>
    private void updateProgressionButton(FacilityShopViewData data)
    {
        if (!data.ProgressionItem.HasValue) return;
        FacilityItemViewData progression = data.ProgressionItem.Value;
        bool prerequisitesComplete = progression.CompletedRegularCount == progression.RequiredRegularCount;
        Button button = data.CurrentStoreStage switch
        {
            1 => stage1ProgressionButton,
            2 => stage2ProgressionButton,
            3 => stage3ProgressionButton,
            _ => null
        };
        Image image = data.CurrentStoreStage switch
        {
            1 => stage1ProgressionImage,
            2 => stage2ProgressionImage,
            3 => stage3ProgressionImage,
            _ => null
        };
        if (image != null)
            image.sprite = prerequisitesComplete
                ? data.CurrentStoreStage == 3 ? upgradeToCivilizationSprite : upgradeToNextFacilitySprite
                : purchasedAllToUnlockSprite;
        if (button != null)
            button.interactable = interactive && prerequisitesComplete &&
                (data.CurrentStoreStage == 3 || progression.State == FacilityDisplayState.Purchasable);
    }

    /// <summary>현재 단계의 진행 항목을 구매하거나 3단계에서 시민권 페이지를 연다.</summary>
    private void handleProgression()
    {
        if (!interactive || currentData == null || !currentData.ProgressionItem.HasValue) return;
        FacilityItemViewData progression = currentData.ProgressionItem.Value;
        if (progression.CompletedRegularCount != progression.RequiredRegularCount) return;
        if (currentData.CurrentStoreStage == 3)
        {
            isCitizenshipPageOpen = true;
            updatePageVisibility(3);
            citizenshipSlot.UpdateView(progression, interactive);
            return;
        }
        if (progression.State == FacilityDisplayState.Purchasable)
            OnPurchaseRequested?.Invoke(progression.FacilityIdx);
    }

    /// <summary>표시 슬롯의 PK를 변경 없이 전달한다.</summary>
    /// <param name="facilityIdx">요청한 설비.</param>
    private void handlePurchase(uint facilityIdx)
    {
        if (interactive && isActiveAndEnabled) OnPurchaseRequested?.Invoke(facilityIdx);
    }

    /// <summary>구매 중이 아닐 때 바깥 배경 클릭을 닫기 요청으로 전달한다.</summary>
    private void handleClose()
    {
        if (isActiveAndEnabled && outsideCloseButton != null && outsideCloseButton.interactable)
            OnCloseRequested?.Invoke();
    }

    /// <summary>고정 슬롯의 요청 구독을 일괄 변경한다.</summary>
    private void subscribeSlots(FacilityPamphletSlotView[] slots, bool subscribe)
    {
        if (slots == null) return;
        foreach (FacilityPamphletSlotView slot in slots)
        {
            if (slot == null) continue;
            if (subscribe) slot.PurchaseRequested += handlePurchase;
            else slot.PurchaseRequested -= handlePurchase;
        }
    }

    /// <summary>고정 슬롯의 처리 중 입력 상태를 일괄 변경한다.</summary>
    private void setSlotsInteractive(FacilityPamphletSlotView[] slots, bool enabled)
    {
        if (slots == null) return;
        foreach (FacilityPamphletSlotView slot in slots)
            if (slot != null) slot.SetInteractionEnabled(enabled);
    }
}
