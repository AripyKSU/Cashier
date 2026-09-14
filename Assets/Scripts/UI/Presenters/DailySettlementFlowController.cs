using System;
using UnityEngine;

/// <summary>정산 UI 구성요소의 순서, 재진입과 입력 수명만 관리합니다.</summary>
public sealed class DailySettlementFlowController : MonoBehaviour
{
    public enum FlowState
    {
        Inactive,
        LedgerPresenting,
        DaughterPresenting,
        StampPresenting,
        ReadyForInteraction,
        FacilityOpen,
        AdvancingDay,
        Failed
    }

    [SerializeField] private DailySettlementPresenter settlementPresenter;
    [SerializeField] private DaughterDialoguePresenter daughterPresenter;
    [SerializeField] private SettlementInteractionView interactionView;

    private Action completeSettlement;
    private int activeDay = -1;

    public FlowState State { get; private set; } = FlowState.Inactive;
    public int ActiveDay => activeDay;
    public event Action OnFacilityOpenRequested;
    public event Action OnDayAdvanceStarted;
    public event Action<Exception> OnFlowFailed;

    private void Awake()
    {
        ValidateReferences();
        settlementPresenter.OnLedgerPresentationCompleted += handleLedgerCompleted;
        daughterPresenter.OnPresentationCompleted += handleDaughterCompleted;
        settlementPresenter.OnStampPresentationCompleted += handleStampCompleted;
        interactionView.OnFacilityRequested += handleFacilityRequested;
        settlementPresenter.OnNextStepRequested += handleNextDayRequested;
        interactionView.SetInteractionEnabled(false);
    }

    private void OnDestroy()
    {
        if (settlementPresenter != null)
        {
            settlementPresenter.OnLedgerPresentationCompleted -= handleLedgerCompleted;
            settlementPresenter.OnStampPresentationCompleted -= handleStampCompleted;
            settlementPresenter.OnNextStepRequested -= handleNextDayRequested;
        }
        if (daughterPresenter != null)
            daughterPresenter.OnPresentationCompleted -= handleDaughterCompleted;
        if (interactionView != null)
        {
            interactionView.OnFacilityRequested -= handleFacilityRequested;
        }
    }

    public void ValidateReferences()
    {
        if (settlementPresenter == null || daughterPresenter == null || interactionView == null)
            throw new InvalidOperationException("DailySettlementFlowController: 정산, 딸 대사와 상호작용 View가 필요합니다.");
        interactionView.ValidateReferences();
    }

    /// <summary>확정된 표시값을 준비하고 새 날짜의 정산 연출을 시작합니다.</summary>
    public void Begin(
        DayProgress day,
        DailySettlementViewData settlement,
        DaughterDialogueViewData daughter,
        Action completeSettlementAction)
    {
        ValidateReferences();
        if (day == null) throw new ArgumentNullException(nameof(day));
        if (day.State != DayProgressState.Settlement)
            throw new InvalidOperationException("정산 상태인 DayProgress만 표시할 수 있습니다.");
        if (settlement.Day != day.Day || daughter.Day != checked((uint)day.Day))
            throw new InvalidOperationException("정산, 딸 대사와 DayProgress의 날짜가 일치해야 합니다.");
        if (completeSettlementAction == null) throw new ArgumentNullException(nameof(completeSettlementAction));

        if (activeDay == day.Day)
        {
            if (State == FlowState.ReadyForInteraction || State == FlowState.FacilityOpen)
                RefreshSettlement(settlement);
            return;
        }

        activeDay = day.Day;
        completeSettlement = completeSettlementAction;
        interactionView.SetInteractionEnabled(false);
        try
        {
            daughterPresenter.UpdateView(daughter);
            State = FlowState.LedgerPresenting;
            settlementPresenter.UpdateView(settlement);
        }
        catch (Exception exception)
        {
            SetFailed(exception);
        }
    }

    /// <summary>설비 구매 뒤 최신 잔액 표시만 갱신하고 완료된 연출은 반복하지 않습니다.</summary>
    public void RefreshSettlement(DailySettlementViewData settlement)
    {
        if (activeDay <= 0 || settlement.Day != activeDay)
            throw new InvalidOperationException("현재 정산 날짜와 갱신할 표시값의 날짜가 다릅니다.");
        settlementPresenter.UpdateView(settlement);
    }

    /// <summary>외부 설비 UI가 닫혔음을 반영하고 정산 입력으로 복귀합니다.</summary>
    public void NotifyFacilityClosed()
    {
        if (State != FlowState.FacilityOpen) return;
        State = FlowState.ReadyForInteraction;
        interactionView.SetInteractionEnabled(true);
    }

    /// <summary>설비 UI를 열지 못한 요청을 취소하고 정산 입력을 복구합니다.</summary>
    public void CancelFacilityOpen()
    {
        if (State != FlowState.FacilityOpen) return;
        State = FlowState.ReadyForInteraction;
        interactionView.SetInteractionEnabled(true);
    }

    public void SetFailed(Exception exception)
    {
        State = FlowState.Failed;
        interactionView.SetInteractionEnabled(false);
        OnFlowFailed?.Invoke(exception ?? new InvalidOperationException("정산 UI 흐름 오류가 발생했습니다."));
    }

    private void handleLedgerCompleted()
    {
        if (State != FlowState.LedgerPresenting) return;
        runTransition(FlowState.DaughterPresenting, daughterPresenter.Present);
    }

    private void handleDaughterCompleted()
    {
        if (State != FlowState.DaughterPresenting) return;
        runTransition(FlowState.StampPresenting, settlementPresenter.PresentReputationStamp);
    }

    private void handleStampCompleted()
    {
        if (State != FlowState.StampPresenting) return;
        State = FlowState.ReadyForInteraction;
        interactionView.SetInteractionEnabled(true);
    }

    private void handleFacilityRequested()
    {
        if (State != FlowState.ReadyForInteraction) return;
        State = FlowState.FacilityOpen;
        interactionView.SetInteractionEnabled(false);
        OnFacilityOpenRequested?.Invoke();
    }

    private void handleNextDayRequested()
    {
        if (State != FlowState.ReadyForInteraction) return;
        State = FlowState.AdvancingDay;
        interactionView.SetInteractionEnabled(false);
        try
        {
            completeSettlement();
            OnDayAdvanceStarted?.Invoke();
        }
        catch (Exception exception)
        {
            State = FlowState.ReadyForInteraction;
            interactionView.SetInteractionEnabled(true);
            OnFlowFailed?.Invoke(exception);
        }
    }

    private void runTransition(FlowState nextState, Action action)
    {
        State = nextState;
        try { action(); }
        catch (Exception exception) { SetFailed(exception); }
    }
}
