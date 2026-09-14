using System;
using UnityEngine;

/// <summary>
/// 3.8 DailySettlementPresenter
/// 하루 영업이 종료된 뒤 확정된 일일 정산 결과(매출, 지출, 순익, 잔액, 평판, 손님 통계)를 표시하는 Presenter.
/// Finance의 정산 로직을 직접 호출하거나 자체 집계하지 않으며, 다음 단계 진행 요청만 외부에 전달합니다.
/// </summary>
public class DailySettlementPresenter : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Ledger")]
    [Tooltip("정산 snapshot을 양쪽 가계부 페이지에 순차 출력하는 View")]
    [SerializeField] private DailySettlementLedgerView ledgerView;

    [Header("Reputation Stamp")]
    [SerializeField] private ReputationStampPresenter reputationStampPresenter;

    private int presentedLedgerDay = -1;
    private bool hasCompletedLedgerPresentation;

    /// <summary>가계부 양쪽 페이지의 순차 출력이 완료됐을 때 발생하는 이벤트입니다.</summary>
    public event Action OnLedgerPresentationCompleted;

    /// <summary>명성 도장이 최종 위치에 고정됐을 때 발생합니다.</summary>
    public event Action OnStampPresentationCompleted;

    private void Awake()
    {
        if (this.ledgerView != null)
        {
            this.ledgerView.OnPresentationCompleted += this.handleLedgerPresentationCompleted;
        }
        if (this.reputationStampPresenter != null)
            this.reputationStampPresenter.OnPresentationCompleted += this.handleStampPresentationCompleted;

    }

    private void OnDestroy()
    {
        if (this.ledgerView != null)
        {
            this.ledgerView.OnPresentationCompleted -= this.handleLedgerPresentationCompleted;
        }
        if (this.reputationStampPresenter != null)
            this.reputationStampPresenter.OnPresentationCompleted -= this.handleStampPresentationCompleted;
    }

    /// <summary>
    /// 외부 정산 시스템에서 확정된 일일 정산 스냅샷을 받아 화면에 표시하고 패널을 엽니다.
    /// </summary>
    public void UpdateView(DailySettlementViewData viewData)
    {
        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(true);
        }

        if (this.ledgerView != null)
        {
            DailySettlementLedgerText ledgerText = DailySettlementLedgerFormatter.Format(viewData);
            if (this.presentedLedgerDay == viewData.Day && this.hasCompletedLedgerPresentation)
            {
                this.ledgerView.RefreshCompleted(ledgerText);
            }
            else
            {
                this.presentedLedgerDay = viewData.Day;
                this.hasCompletedLedgerPresentation = false;
                this.ledgerView.Present(ledgerText);
            }
        }

        if (this.reputationStampPresenter == null)
            throw new MissingReferenceException("DailySettlementPresenter: 명성 도장 Presenter가 필요합니다.");
        this.reputationStampPresenter.UpdateView(viewData.Day, viewData.FinalReputation);

    }

    /// <summary>정산 패널 닫기</summary>
    public void Close()
    {
        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(false);
        }
    }

    /// <summary>딸 대사 완료 뒤 준비된 명성 도장 연출을 시작합니다.</summary>
    public void PresentReputationStamp() => this.reputationStampPresenter.Present();

    /// <summary>가계부 View의 완료를 이후 딸 대사 흐름이 구독할 수 있도록 전달합니다.</summary>
    private void handleLedgerPresentationCompleted()
    {
        if (this.hasCompletedLedgerPresentation) return;
        this.hasCompletedLedgerPresentation = true;
        this.OnLedgerPresentationCompleted?.Invoke();
    }

    private void handleStampPresentationCompleted() => this.OnStampPresentationCompleted?.Invoke();

}
