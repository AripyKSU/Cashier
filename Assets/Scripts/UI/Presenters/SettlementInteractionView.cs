using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>정산 화면의 팜플렛과 다음 날 버튼 입력을 이벤트로만 전달합니다.</summary>
public sealed class SettlementInteractionView : MonoBehaviour
{
    [SerializeField] private Button facilityPamphletButton;
    [SerializeField] private Button nextDayButton;

    public event Action OnFacilityRequested;
    public event Action OnNextDayRequested;

    private void Awake()
    {
        ValidateReferences();
        facilityPamphletButton.onClick.AddListener(handleFacilityClicked);
        nextDayButton.onClick.AddListener(handleNextDayClicked);
    }

    private void OnDestroy()
    {
        if (facilityPamphletButton != null)
            facilityPamphletButton.onClick.RemoveListener(handleFacilityClicked);
        if (nextDayButton != null)
            nextDayButton.onClick.RemoveListener(handleNextDayClicked);
    }

    public void ValidateReferences()
    {
        if (facilityPamphletButton == null || nextDayButton == null)
            throw new InvalidOperationException("SettlementInteractionView: 팜플렛과 다음 날 Button 참조가 필요합니다.");
    }

    /// <summary>정산 연출과 설비 모달 상태에 맞춰 두 입력을 함께 잠그거나 해제합니다.</summary>
    public void SetInteractionEnabled(bool enabled)
    {
        ValidateReferences();
        facilityPamphletButton.interactable = enabled;
        nextDayButton.interactable = enabled;
    }

    private void handleFacilityClicked() => OnFacilityRequested?.Invoke();

    private void handleNextDayClicked() => OnNextDayRequested?.Invoke();
}
