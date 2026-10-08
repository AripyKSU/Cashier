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
        /// <summary>도장 뒤 딸이 이어서 말하는 대본(1일차 명성 설명 등)을 출력하는 중입니다.</summary>
        DaughterAfterStampPresenting,
        /// <summary>도장 뒤 대본 다음에 딸이 설비 업그레이드 등을 직접 보여 주며 안내하는 중입니다.</summary>
        GuidePresenting,
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
    private bool guideAllowsFacility;
    private bool isFacilityFromGuide;

    public FlowState State { get; private set; } = FlowState.Inactive;
    public int ActiveDay => activeDay;
    public event Action OnFacilityOpenRequested;
    /// <summary>다음 날 완료 처리를 시작하기 직전에 발생합니다.</summary>
    public event Action OnDayAdvanceRequested;
    public event Action OnDayAdvanceStarted;
    public event Action<Exception> OnFlowFailed;

    /// <summary>
    /// 도장 뒤 대본이 끝난 직후 한 번 실행할 안내입니다. 인자로 받은 완료 콜백을 부르면 정산 입력이 열립니다.
    /// 실행되면 비워집니다.
    /// </summary>
    public Action<Action> AfterStampGuide { get; set; }

    private void Awake()
    {
        ValidateReferences();
        settlementPresenter.OnLedgerPresentationCompleted += handleLedgerCompleted;
        daughterPresenter.OnPresentationCompleted += handleDaughterCompleted;
        settlementPresenter.OnStampPresentationCompleted += handleStampCompleted;
        daughterPresenter.OnAfterStampCompleted += handleDaughterAfterStampCompleted;
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
        {
            daughterPresenter.OnPresentationCompleted -= handleDaughterCompleted;
            daughterPresenter.OnAfterStampCompleted -= handleDaughterAfterStampCompleted;
        }
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
        guideAllowsFacility = false;
        isFacilityFromGuide = false;
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
        if (isFacilityFromGuide)
        {
            // 안내 중에 연 창이면 안내로 돌아간다. 입력은 안내가 끝날 때 연다.
            isFacilityFromGuide = false;
            State = FlowState.GuidePresenting;
            return;
        }

        State = FlowState.ReadyForInteraction;
        interactionView.SetInteractionEnabled(true);
    }

    /// <summary>설비 UI를 열지 못한 요청을 취소하고 정산 입력을 복구합니다.</summary>
    public void CancelFacilityOpen()
    {
        if (State != FlowState.FacilityOpen) return;
        if (isFacilityFromGuide)
        {
            isFacilityFromGuide = false;
            State = FlowState.GuidePresenting;
            AllowGuideFacilityOpen();
            return;
        }

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
        // 도장 뒤에 딸이 이어서 할 말이 있으면 그 대본이 끝난 뒤에 입력을 연다.
        if (daughterPresenter.HasAfterStampLines)
        {
            runTransition(FlowState.DaughterAfterStampPresenting, daughterPresenter.PresentAfterStamp);
            return;
        }

        State = FlowState.ReadyForInteraction;
        interactionView.SetInteractionEnabled(true);
    }

    /// <summary>도장 뒤 대본이 끝나면 정산 입력을 엽니다.</summary>
    private void handleDaughterAfterStampCompleted()
    {
        if (State != FlowState.DaughterAfterStampPresenting) return;
        Action<Action> guide = AfterStampGuide;
        AfterStampGuide = null;
        if (guide != null)
        {
            runTransition(FlowState.GuidePresenting, () => guide(handleGuideCompleted));
            return;
        }

        State = FlowState.ReadyForInteraction;
        interactionView.SetInteractionEnabled(true);
    }

    /// <summary>안내가 "눌러 봐"를 기다리는 동안 팜플렛 하나만 누를 수 있게 합니다.</summary>
    public void AllowGuideFacilityOpen()
    {
        if (State != FlowState.GuidePresenting) return;
        guideAllowsFacility = true;
        interactionView.SetFacilityOnlyEnabled();
    }

    /// <summary>안내가 끝나면 정산 입력을 엽니다.</summary>
    private void handleGuideCompleted()
    {
        if (State != FlowState.GuidePresenting) return;
        guideAllowsFacility = false;
        State = FlowState.ReadyForInteraction;
        interactionView.SetInteractionEnabled(true);
    }

    private void handleFacilityRequested()
    {
        if (State == FlowState.GuidePresenting && guideAllowsFacility)
        {
            guideAllowsFacility = false;
            isFacilityFromGuide = true;
            State = FlowState.FacilityOpen;
            interactionView.SetInteractionEnabled(false);
            OnFacilityOpenRequested?.Invoke();
            return;
        }

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
            OnDayAdvanceRequested?.Invoke();
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
