using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
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
    /// <summary>가게 단계가 오를 때 한 번 내는 알림음입니다.</summary>
    [SerializeField] private AudioClip stageUpSound;
    /// <summary>알림음이 효과음 볼륨 설정을 따르도록 연결하는 SFX 그룹입니다.</summary>
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    private const float PaperOpenVolume = 0.5f;
    private const float PaperCloseVolume = 0.4f;

    private FacilityShopViewData currentData;
    private bool interactive = true;
    private bool isCitizenshipPageOpen;
    /// <summary>열린 수명 동안 표시한 가게 단계입니다. 0이면 이번 수명에 아직 표시하지 않았습니다.</summary>
    private uint displayedStage;
    /// <summary>선행 설비가 막 끝난 순간을 찾기 위한 직전 값입니다.</summary>
    private bool wasPrerequisiteComplete;
    /// <summary>wasPrerequisiteComplete가 가리키는 단계입니다. 단계가 바뀐 직후에는 해금 연출을 내지 않습니다.</summary>
    private uint prerequisiteCheckedStage;
    private bool isBannerPlaying;
    private FacilityButtonFeedback stage1ProgressionFeedback;
    private FacilityButtonFeedback stage2ProgressionFeedback;
    private FacilityButtonFeedback stage3ProgressionFeedback;
    private FacilityStageUpBanner stageUpBanner;
    private Tween pageTween;

    /// <summary>단일 설비 PK의 구매 요청.</summary>
    public event Action<uint> OnPurchaseRequested;

    /// <summary>날짜 변경 없이 패널만 닫는 요청.</summary>
    public event Action OnCloseRequested;

    /// <summary>튜토리얼 강조용 1단계 팸플릿 영역입니다.</summary>
    public RectTransform Stage1PanelRect => stage1Panel != null ? (RectTransform)stage1Panel.transform : null;

    /// <summary>튜토리얼 강조용 1단계 설비 슬롯입니다. 0·1은 위 두 설비, 2는 분류 막대입니다.</summary>
    /// <param name="index">슬롯 순서입니다.</param>
    /// <returns>없으면 null입니다.</returns>
    public FacilityPamphletSlotView GetStage1Slot(int index) =>
        stage1Slots != null && index >= 0 && index < stage1Slots.Length ? stage1Slots[index] : null;

    /// <summary>진행 버튼 반응과 단계 상승 배너를 한 번만 준비한다.</summary>
    private void Awake()
    {
        stage1ProgressionFeedback = attachProgressionFeedback(stage1ProgressionButton, stage1ProgressionImage);
        stage2ProgressionFeedback = attachProgressionFeedback(stage2ProgressionButton, stage2ProgressionImage);
        stage3ProgressionFeedback = attachProgressionFeedback(stage3ProgressionButton, stage3ProgressionImage);
        if (stageUpSound == null) return;
        var soundSource = gameObject.AddComponent<AudioSource>();
        soundSource.playOnAwake = false;
        soundSource.spatialBlend = 0f;
        soundSource.outputAudioMixerGroup = sfxMixerGroup;
        TextMeshProUGUI font = GetComponentInChildren<TextMeshProUGUI>(true);
        stageUpBanner = FacilityStageUpBanner.Create((RectTransform)transform, font != null ? font.font : null, soundSource, stageUpSound);
    }

    /// <summary>열린 수명에만 고정 슬롯과 팸플릿 버튼 요청을 구독한다.</summary>
    private void OnEnable()
    {
        SoundManager.Instance?.PlaySfx(SoundKeys.ItemPickup, PaperOpenVolume);
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
        displayedStage = 0;
        prerequisiteCheckedStage = 0;
        wasPrerequisiteComplete = false;
        pageTween?.Kill();
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

        FacilityPamphletSlotView[] activeSlots = data.CurrentStoreStage switch
        {
            1 => stage1Slots,
            2 => stage2Slots,
            3 => stage3Slots,
            _ => throw new InvalidOperationException($"지원하지 않는 가게 단계입니다. stage={data.CurrentStoreStage}")
        };
        if (activeSlots == null || activeSlots.Length != data.RegularItems.Count)
            throw new InvalidOperationException($"{data.CurrentStoreStage}단계 팸플릿 슬롯 수가 설비 수와 다릅니다.");

        // 슬롯이 Awake에서 버튼 반응을 만든 뒤 표시값을 받도록 새 팸플릿을 먼저 켠다.
        bool stageRose = displayedStage != 0 && data.CurrentStoreStage > displayedStage;
        bool wasFirstDisplay = displayedStage == 0;
        displayedStage = data.CurrentStoreStage;
        updatePageVisibility(data.CurrentStoreStage);

        for (int index = 0; index < activeSlots.Length; index++)
            activeSlots[index].UpdateView(data.RegularItems[index], interactive);

        if (data.CurrentStoreStage == 3 && isCitizenshipPageOpen)
        {
            if (!data.ProgressionItem.HasValue || citizenshipSlot == null)
                throw new InvalidOperationException("시민권 팸플릿 슬롯 또는 표시 데이터가 누락되었습니다.");
            citizenshipSlot.UpdateView(data.ProgressionItem.Value, interactive);
        }

        updateProgressionButton(data);
        if (stageRose) playStageUp(data.CurrentStoreStage);
        else if (wasFirstDisplay) playPageIn(activePage(data.CurrentStoreStage));
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
        FacilityButtonFeedback feedback = data.CurrentStoreStage switch
        {
            1 => stage1ProgressionFeedback,
            2 => stage2ProgressionFeedback,
            3 => stage3ProgressionFeedback,
            _ => null
        };
        if (feedback == null) return;
        // 잠긴 이유가 있으면 눌렀을 때 떨어 주고, 살 수 있으면 눈에 띄게 맥동시킨다.
        feedback.SetRejectsWhenDisabled(!prerequisitesComplete || progression.State == FacilityDisplayState.InsufficientFunds);
        feedback.SetAttention(button != null && button.interactable);
        if (prerequisitesComplete && !wasPrerequisiteComplete && prerequisiteCheckedStage == data.CurrentStoreStage)
            feedback.PlayUnlocked();
        wasPrerequisiteComplete = prerequisitesComplete;
        prerequisiteCheckedStage = data.CurrentStoreStage;
    }

    /// <summary>진행 버튼에 반응을 붙인다. 구성이 빠진 단계는 건너뛴다.</summary>
    /// <param name="button">진행 버튼입니다.</param>
    /// <param name="image">버튼 그림입니다.</param>
    /// <returns>붙은 반응입니다. 구성이 없으면 null입니다.</returns>
    private static FacilityButtonFeedback attachProgressionFeedback(Button button, Image image) =>
        button != null && image != null ? FacilityButtonFeedback.AttachToSprite(button, image) : null;

    /// <summary>현재 보이는 팸플릿을 반환한다.</summary>
    /// <param name="storeStage">현재 가게 단계입니다.</param>
    /// <returns>보이는 팸플릿입니다. 구성이 없으면 null입니다.</returns>
    private GameObject activePage(uint storeStage) => storeStage switch
    {
        1 => stage1Panel,
        2 => stage2Panel,
        3 => isCitizenshipPageOpen ? citizenshipPanel : stage3Panel,
        _ => null
    };

    /// <summary>팸플릿을 살짝 작은 크기에서 튀어 오르게 보여 준다.</summary>
    /// <param name="page">보여 줄 팸플릿입니다.</param>
    private void playPageIn(GameObject page)
    {
        if (page == null || !isActiveAndEnabled) return;
        pageTween?.Kill();
        page.transform.localScale = Vector3.one * 0.92f;
        pageTween = page.transform.DOScale(Vector3.one, 0.24f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(page);
    }

    /// <summary>단계 상승 배너를 재생하고 걷힌 뒤 새 팸플릿을 보여 준다. 재생 중에는 배너가 입력을 막는다.</summary>
    /// <param name="newStage">새 가게 단계입니다.</param>
    private void playStageUp(uint newStage)
    {
        if (stageUpBanner == null || !isActiveAndEnabled) return;
        isBannerPlaying = true;
        stageUpBanner.Play(newStage, () =>
        {
            isBannerPlaying = false;
            playPageIn(activePage(newStage));
        });
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
            SoundManager.Instance?.PlaySfx(SoundKeys.ItemPickup, PaperOpenVolume);
            playPageIn(citizenshipPanel);
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
        if (isActiveAndEnabled && outsideCloseButton != null && outsideCloseButton.interactable && !isBannerPlaying)
        {
            SoundManager.Instance?.PlaySfx(SoundKeys.ItemPlace, PaperCloseVolume);
            OnCloseRequested?.Invoke();
        }
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
