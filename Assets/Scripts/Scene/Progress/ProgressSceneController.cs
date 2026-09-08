using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진행용 개인 씬의 진행 로직과 UI Presenter를 연결하는 Scene 수명 조립 컴포넌트입니다.
/// 진행 규칙은 GameProgress와 DayProgress에 위임하고 이 클래스는 입력·ViewData·화면 수명만 담당합니다.
/// </summary>
public sealed class ProgressSceneController : MonoBehaviour
{
    private GameProgress gameProgress;
    private DayProgress subscribedDay;
    private EconomyRuntime economy;
    private CustomerCatalog customerCatalog;
    private TextDataTable textData;
    private ProgressViewDataFactory viewDataFactory;

    [Header("Presenter references")]
    [SerializeField] private GameDayPresenter gameDayPresenter;
    [SerializeField] private BusinessTimerPresenter businessTimerPresenter;
    [SerializeField] private EconomyStatusPresenter economyStatusPresenter;
    [SerializeField] private CustomerPresenter customerPresenter;
    [SerializeField] private PriceInputPresenter priceInputPresenter;
    [SerializeField] private DailySettlementPresenter dailySettlementPresenter;
    [SerializeField] private KeypadController keypadController;

    [Header("Progress panels")]
    [SerializeField] private GameObject preOpenPanel;
    [SerializeField] private GameObject operatingPanel;
    [SerializeField] private GameObject settlementPanel;
    [SerializeField] private GameObject tributePanel;
    [SerializeField] private GameObject failurePanel;

    [Header("Progress UI fields")]
    [SerializeField] private TextMeshProUGUI priceListText;
    [SerializeField] private TextMeshProUGUI validationText;
    [SerializeField] private TextMeshProUGUI transactionStatusText;
    [SerializeField] private TextMeshProUGUI tributeText;
    [SerializeField] private TextMeshProUGUI failureText;
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Progress UI controls")]
    [SerializeField] private Button openBusinessButton;
    [SerializeField] private Button transactionContinueButton;
    [SerializeField] private Button maintenanceButton;
    [SerializeField] private Button[] keypadButtons;

    private bool isReady;
    private bool hasError;

    /// <summary>Scene 진입 후 부트스트랩된 런타임을 확인하고 UI와 진행을 초기화합니다.</summary>
    private async void Start()
    {
        try
        {
            if (DataTableManager.Instance == null || GameSessionManager.Instance == null)
            {
                throw new InvalidOperationException("InitScene부터 실행해 진행 런타임을 준비해야 합니다.");
            }

            await DataTableManager.Instance.EnsureDataLoadedAsync()
                .AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

            this.economy = GameSessionManager.Instance.Economy;
            this.customerCatalog = DataTableManager.Instance.Customers;
            this.textData = DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text);
            if (this.customerCatalog == null || this.textData == null)
            {
                throw new InvalidOperationException("손님 또는 텍스트 데이터가 준비되지 않았습니다.");
            }

            IReadOnlyDictionary<uint, Sprite> productSprites = await this.loadProductSpritesAsync();
            this.viewDataFactory = new ProgressViewDataFactory(
                this.customerCatalog,
                this.textData,
                productSprites);

            this.validateUiReferences();
            this.subscribeUi();
            this.gameProgress = new GameProgress(
                this.economy,
                this.customerCatalog,
                new System.Random());
            this.subscribeProgress();
            this.isReady = true;
            this.gameProgress.Start();
            this.refreshAllViews();
        }
        catch (OperationCanceledException)
        {
            // Scene이 파괴되는 동안의 취소는 정상적인 수명 종료입니다.
        }
        catch (Exception exception)
        {
            this.showError(exception);
        }
    }

    /// <summary>프레임 경과 시간을 진행 로직에 전달하고 UI 스냅샷을 갱신합니다.</summary>
    private void Update()
    {
        if (!this.isReady || this.hasError || this.gameProgress == null)
        {
            return;
        }

        try
        {
            if (this.gameProgress.State == GameProgressState.DayInProgress)
            {
                this.gameProgress.Tick(Time.deltaTime);
                this.refreshFrameViews();
            }
        }
        catch (Exception exception)
        {
            this.showError(exception);
        }
    }

    /// <summary>상품 데이터 FK를 따라 장바구니 표시용 Sprite를 한 번씩 로드합니다.</summary>
    /// <returns>모든 상품 ID에 대응하는 로드 완료 Sprite 사전입니다.</returns>
    /// <exception cref="InvalidOperationException">리소스 시스템, FK 또는 Sprite 로드가 실패한 경우 발생합니다.</exception>
    private async UniTask<IReadOnlyDictionary<uint, Sprite>> loadProductSpritesAsync()
    {
        if (ResourceManager.Instance == null)
        {
            throw new InvalidOperationException("ResourceManager가 준비되지 않았습니다.");
        }

        ResourceDataTable resources = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
        if (resources == null)
        {
            throw new InvalidOperationException("ResourceDataTable이 준비되지 않았습니다.");
        }

        var spritesByResource = new Dictionary<uint, Sprite>();
        var spritesByProduct = new Dictionary<uint, Sprite>();
        foreach (ProductData product in this.customerCatalog.Products.Rows.Values)
        {
            if (!product.ImageResourceIdx.HasValue)
            {
                throw new InvalidOperationException($"상품 {product.Idx}의 image_resource_idx가 비어 있습니다.");
            }

            uint resourceId = product.ImageResourceIdx.Value;
            if (!spritesByResource.TryGetValue(resourceId, out Sprite sprite))
            {
                string address = resources.GetResourcePath(resourceId);
                if (string.IsNullOrWhiteSpace(address))
                {
                    throw new InvalidOperationException(
                        $"상품 {product.Idx}의 ResourceData FK {resourceId}를 찾을 수 없습니다.");
                }

                sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(address)
                    .AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
                if (sprite == null)
                {
                    throw new InvalidOperationException(
                        $"상품 {product.Idx}의 Sprite를 로드하지 못했습니다. Resource {resourceId}, Address {address}");
                }

                spritesByResource.Add(resourceId, sprite);
            }

            spritesByProduct.Add(product.Idx, sprite);
        }

        return spritesByProduct;
    }

    /// <summary>진행 이벤트와 UI 입력 이벤트를 해제합니다.</summary>
    private void OnDestroy()
    {
        this.unsubscribeProgress();
        this.unsubscribeUi();
    }

    /// <summary>씬에 직렬화된 Presenter와 진행 필수 UI 참조가 연결됐는지 확인합니다.</summary>
    /// <exception cref="InvalidOperationException">진행에 필요한 씬 참조가 누락된 경우 발생합니다.</exception>
    private void validateUiReferences()
    {
        if (this.gameDayPresenter == null
            || this.businessTimerPresenter == null
            || this.economyStatusPresenter == null
            || this.customerPresenter == null
            || this.priceInputPresenter == null
            || this.dailySettlementPresenter == null
            || this.keypadController == null
            || this.preOpenPanel == null
            || this.operatingPanel == null
            || this.settlementPanel == null
            || this.tributePanel == null
            || this.failurePanel == null
            || this.openBusinessButton == null
            || this.transactionContinueButton == null
            || this.maintenanceButton == null)
        {
            throw new InvalidOperationException("ProgressScene의 Presenter 또는 패널 참조가 누락되었습니다.");
        }
    }

    /// <summary>씬에 배치된 Presenter와 버튼의 입력 이벤트를 구독합니다.</summary>
    private void subscribeUi()
    {
        this.priceInputPresenter.OnPriceConfirmed += this.handlePriceConfirmed;
        this.priceInputPresenter.OnInputCancelled += this.handleInputCancelled;
        this.businessTimerPresenter.OnPauseRequested += this.handlePauseRequested;
        this.businessTimerPresenter.OnResumeRequested += this.handleResumeRequested;
        this.dailySettlementPresenter.OnNextStepRequested += this.handleSettlementNextRequested;
        this.keypadController.OnPriceChanged += this.handlePriceChanged;
        this.openBusinessButton.onClick.AddListener(this.handleOpenBusinessClicked);
        this.transactionContinueButton.onClick.AddListener(this.handleTransactionContinueClicked);
        this.maintenanceButton.onClick.AddListener(this.handleMaintenanceClicked);
    }

    /// <summary>씬 UI 입력 이벤트를 해제합니다.</summary>
    private void unsubscribeUi()
    {
        if (this.priceInputPresenter != null)
        {
            this.priceInputPresenter.OnPriceConfirmed -= this.handlePriceConfirmed;
            this.priceInputPresenter.OnInputCancelled -= this.handleInputCancelled;
        }

        if (this.businessTimerPresenter != null)
        {
            this.businessTimerPresenter.OnPauseRequested -= this.handlePauseRequested;
            this.businessTimerPresenter.OnResumeRequested -= this.handleResumeRequested;
        }

        if (this.dailySettlementPresenter != null)
        {
            this.dailySettlementPresenter.OnNextStepRequested -= this.handleSettlementNextRequested;
        }

        if (this.keypadController != null)
        {
            this.keypadController.OnPriceChanged -= this.handlePriceChanged;
        }

        if (this.openBusinessButton != null)
        {
            this.openBusinessButton.onClick.RemoveListener(this.handleOpenBusinessClicked);
        }

        if (this.transactionContinueButton != null)
        {
            this.transactionContinueButton.onClick.RemoveListener(this.handleTransactionContinueClicked);
        }

        if (this.maintenanceButton != null)
        {
            this.maintenanceButton.onClick.RemoveListener(this.handleMaintenanceClicked);
        }
    }

    /// <summary>진행 이벤트를 구독합니다.</summary>
    private void subscribeProgress()
    {
        this.gameProgress.StateChanged += this.handleGameStateChanged;
        this.gameProgress.DayStarted += this.handleDayStarted;
        this.gameProgress.GameFailed += this.handleGameFailed;
    }

    /// <summary>진행 이벤트와 현재 하루의 이벤트를 해제합니다.</summary>
    private void unsubscribeProgress()
    {
        if (this.gameProgress != null)
        {
            this.gameProgress.StateChanged -= this.handleGameStateChanged;
            this.gameProgress.DayStarted -= this.handleDayStarted;
            this.gameProgress.GameFailed -= this.handleGameFailed;
        }

        if (this.subscribedDay != null)
        {
            this.subscribedDay.StateChanged -= this.handleDayStateChanged;
            this.subscribedDay.CustomerStarted -= this.handleCustomerStarted;
            this.subscribedDay.TransactionCompleted -= this.handleTransactionCompleted;
            this.subscribedDay.SettlementStarted -= this.handleSettlementStarted;
        }
    }

    /// <summary>새 하루의 상태와 결과 이벤트를 UI 어댑터에 연결합니다.</summary>
    /// <param name="day">새로 시작된 하루 진행 인스턴스입니다.</param>
    private void handleDayStarted(DayProgress day)
    {
        if (this.subscribedDay != null)
        {
            this.subscribedDay.StateChanged -= this.handleDayStateChanged;
            this.subscribedDay.CustomerStarted -= this.handleCustomerStarted;
            this.subscribedDay.TransactionCompleted -= this.handleTransactionCompleted;
            this.subscribedDay.SettlementStarted -= this.handleSettlementStarted;
        }

        this.subscribedDay = day;
        this.subscribedDay.StateChanged += this.handleDayStateChanged;
        this.subscribedDay.CustomerStarted += this.handleCustomerStarted;
        this.subscribedDay.TransactionCompleted += this.handleTransactionCompleted;
        this.subscribedDay.SettlementStarted += this.handleSettlementStarted;
        this.renderPriceList(day.Day);
        this.refreshAllViews();
    }

    /// <summary>전체 진행 상태 변경을 화면 표시 상태에 반영합니다.</summary>
    /// <param name="state">변경된 전체 진행 상태입니다.</param>
    private void handleGameStateChanged(GameProgressState state)
    {
        if (state == GameProgressState.Maintenance)
        {
            this.setPanelVisibility(this.preOpenPanel, false);
            this.setPanelVisibility(this.operatingPanel, false);
            this.setPanelVisibility(this.settlementPanel, false);
            this.setPanelVisibility(this.tributePanel, true);
            this.setPanelVisibility(this.failurePanel, false);
            long requiredAmount = this.economy.MaintenanceService.GetRequiredAmount(
                this.gameProgress.CurrentMaintenanceRound);
            this.tributeText.text = $"DAY {this.gameProgress.CurrentDay} · REQUIRED {requiredAmount:N0} G";
        }
        else if (state == GameProgressState.Failed)
        {
            this.setPanelVisibility(this.preOpenPanel, false);
            this.setPanelVisibility(this.operatingPanel, false);
            this.setPanelVisibility(this.settlementPanel, false);
            this.setPanelVisibility(this.tributePanel, false);
            this.setPanelVisibility(this.failurePanel, true);
        }
        else if (state == GameProgressState.DayInProgress)
        {
            this.setPanelVisibility(this.tributePanel, false);
            this.setPanelVisibility(this.failurePanel, false);
            this.refreshAllViews();
        }
    }

    /// <summary>상납 실패 결과를 실패 화면 문구로 전달합니다.</summary>
    /// <param name="result">실패한 유지비 납부 결과입니다.</param>
    private void handleGameFailed(MaintenancePaymentResult result)
    {
        if (this.failureText != null)
        {
            this.failureText.text = $"Maintenance failed · required {result.RequiredAmount:N0} G, balance {result.PreviousBalance:N0} G.";
        }
    }

    /// <summary>하루 상태 변경을 화면과 입력 가능 상태에 반영합니다.</summary>
    /// <param name="state">변경된 하루 진행 상태입니다.</param>
    private void handleDayStateChanged(DayProgressState state)
    {
        this.refreshAllViews();
    }

    /// <summary>새 손님 데이터를 Presenter에 전달합니다.</summary>
    /// <param name="visit">가격 입력을 기다리는 새 손님 방문입니다.</param>
    private void handleCustomerStarted(CustomerVisit visit)
    {
        this.customerPresenter.UpdateView(this.viewDataFactory.CreateCustomerViewData(visit));
        this.transactionContinueButton.gameObject.SetActive(false);
        this.transactionStatusText.text = "Enter the total price for the basket.";
        this.refreshRuntimeViews();
    }

    /// <summary>거래 결과를 손님 대사와 결과 확인 버튼에 반영합니다.</summary>
    /// <param name="visit">수락 또는 거절 판정이 완료된 손님 방문입니다.</param>
    private void handleTransactionCompleted(CustomerVisit visit)
    {
        this.customerPresenter.UpdateView(this.viewDataFactory.CreateCustomerViewData(visit));
        this.transactionContinueButton.gameObject.SetActive(true);
        this.transactionStatusText.text = visit.WasAccepted == true
            ? "ACCEPTED · income applied"
            : "REJECTED · no income";
        this.refreshRuntimeViews();
    }

    /// <summary>일일 집계 결과를 정산 Presenter에 전달합니다.</summary>
    /// <param name="result">확정된 하루 재정 집계입니다.</param>
    private void handleSettlementStarted(DailyAggregationResult result)
    {
        this.settlementPanel.SetActive(true);
        this.operatingPanel.SetActive(false);
        this.dailySettlementPresenter.UpdateView(new DailySettlementViewData(
            this.subscribedDay.Day,
            result.SaleIncome,
            0,
            result.SaleIncome,
            this.economy.QueryService.CurrentBalance,
            0,
            this.subscribedDay.SuccessfulSales,
            this.subscribedDay.RefusedCustomers,
            0));
        this.refreshAllViews();
    }

    /// <summary>영업 전 버튼 요청을 하루 진행에 전달합니다.</summary>
    private void handleOpenBusinessClicked()
    {
        this.runProgressAction(this.gameProgress.OpenBusiness);
    }

    /// <summary>가격 입력 결과를 하루 진행에 전달합니다.</summary>
    /// <param name="offeredTotal">플레이어가 확정한 전체 판매 가격입니다.</param>
    private void handlePriceConfirmed(long offeredTotal)
    {
        // 거래 전환 직후 도착한 연속 입력은 사용자 입력 경계에서 멱등하게 무시합니다.
        if (this.subscribedDay == null || !this.subscribedDay.CanSubmitOffer)
        {
            return;
        }

        this.runProgressAction(() =>
        {
            this.gameProgress.SubmitOffer(offeredTotal);
            this.keypadController.OnClearButtonClick();
        });
    }

    /// <summary>가격 입력 취소 후 입력 ViewData를 갱신합니다.</summary>
    private void handleInputCancelled()
    {
        this.validationText.text = string.Empty;
        this.refreshRuntimeViews();
    }

    /// <summary>키패드 입력값 변경을 가격 입력 UI의 활성 상태에 즉시 반영합니다.</summary>
    /// <param name="currentPrice">변경된 현재 입력 가격입니다.</param>
    private void handlePriceChanged(long currentPrice)
    {
        this.refreshPriceInputView(currentPrice);
    }

    /// <summary>거래 결과 확인 요청을 하루 진행에 전달합니다.</summary>
    private void handleTransactionContinueClicked()
    {
        this.runProgressAction(this.gameProgress.CompleteTransactionResult);
    }

    /// <summary>정산 결과 확인 요청을 하루 진행에 전달합니다.</summary>
    private void handleSettlementNextRequested()
    {
        this.runProgressAction(this.gameProgress.CompleteSettlement);
    }

    /// <summary>상납금 납부 요청을 전체 진행에 전달합니다.</summary>
    private void handleMaintenanceClicked()
    {
        this.runProgressAction(() => this.gameProgress.TryPayMaintenance());
    }

    /// <summary>타이머 일시정지 요청을 하루 진행에 전달합니다.</summary>
    private void handlePauseRequested()
    {
        if (!this.canPause())
        {
            return;
        }

        this.runProgressAction(this.gameProgress.Pause);
    }

    /// <summary>타이머 재개 요청을 하루 진행에 전달합니다.</summary>
    private void handleResumeRequested()
    {
        if (!this.canResume())
        {
            return;
        }

        this.runProgressAction(this.gameProgress.Resume);
    }

    /// <summary>진행 호출의 예외를 UI 오류로 표시합니다.</summary>
    /// <param name="action">실행할 Progress 공개 동작입니다.</param>
    private void runProgressAction(Action action)
    {
        if (!this.isReady || this.hasError || action == null)
        {
            return;
        }

        try
        {
            action();
            this.refreshAllViews();
        }
        catch (Exception exception)
        {
            this.showError(exception);
        }
    }

    /// <summary>현재 진행 스냅샷을 모든 Presenter에 전달합니다.</summary>
    private void refreshAllViews()
    {
        if (!this.isReady || this.gameProgress == null || this.subscribedDay == null)
        {
            return;
        }

        this.refreshRuntimeViews();
        this.refreshPanelVisibility();
    }

    /// <summary>날짜·타이머·경제·입력 ViewData를 갱신합니다.</summary>
    private void refreshRuntimeViews()
    {
        if (!this.isReady || this.gameProgress == null || this.subscribedDay == null)
        {
            return;
        }

        this.gameDayPresenter.UpdateView(new GameDayViewData(
            this.gameProgress.CurrentDay,
            this.gameProgress.DaysUntilMaintenance,
            this.gameProgress.IsMaintenanceDay,
            this.toUiPhase(this.subscribedDay.State)));

        this.economyStatusPresenter.UpdateView(new EconomyStatusViewData(
            this.economy.QueryService.CurrentBalance,
            this.economy.QueryService.DailySaleIncome));

        float normalizedTime = this.subscribedDay.BusinessDurationSeconds <= 0f
            ? 0f
            : this.subscribedDay.RemainingSeconds / this.subscribedDay.BusinessDurationSeconds;
        this.businessTimerPresenter.UpdateView(new BusinessTimerViewData(
            this.subscribedDay.RemainingSeconds,
            normalizedTime,
            this.subscribedDay.IsPaused,
            this.canPause(),
            this.canResume()));

        long currentPrice = this.keypadController == null ? 0 : this.keypadController.CurrentPrice;
        this.refreshPriceInputView(currentPrice);

        bool canContinueTransaction = this.subscribedDay.CurrentVisit != null
            && (this.subscribedDay.CurrentVisit.State == CustomerState.Accepted
                || this.subscribedDay.CurrentVisit.State == CustomerState.Rejected)
            && (this.subscribedDay.State == DayProgressState.TransactionResult
                || this.subscribedDay.State == DayProgressState.Closing);
        this.transactionContinueButton.gameObject.SetActive(canContinueTransaction);
        this.setKeypadInteractable(this.subscribedDay.CanSubmitOffer);
    }

    /// <summary>현재 가격과 거래 상태만 가격 입력 Presenter에 전달합니다.</summary>
    /// <param name="currentPrice">키패드에 입력된 0 이상의 가격입니다.</param>
    private void refreshPriceInputView(long currentPrice)
    {
        if (!this.isReady || this.subscribedDay == null || this.priceInputPresenter == null)
        {
            return;
        }

        this.priceInputPresenter.UpdateView(new PriceInputViewData(
            currentPrice > 0 ? currentPrice : (long?)null,
            currentPrice > 0,
            this.subscribedDay.CanSubmitOffer,
            this.hasError ? this.errorText.text : this.validationText.text));
    }

    /// <summary>프레임 경과에 따라 실제로 변하는 타이머 표시만 갱신합니다.</summary>
    private void refreshFrameViews()
    {
        if (this.subscribedDay == null)
        {
            return;
        }

        float normalizedTime = this.subscribedDay.BusinessDurationSeconds <= 0f
            ? 0f
            : this.subscribedDay.RemainingSeconds / this.subscribedDay.BusinessDurationSeconds;
        this.businessTimerPresenter.UpdateView(new BusinessTimerViewData(
            this.subscribedDay.RemainingSeconds,
            normalizedTime,
            this.subscribedDay.IsPaused,
            this.canPause(),
            this.canResume()));
    }

    /// <summary>진행 상태에 따라 패널과 기본 버튼을 표시합니다.</summary>
    private void refreshPanelVisibility()
    {
        bool preOpen = this.subscribedDay.State == DayProgressState.PreOpen;
        bool operating = this.subscribedDay.State == DayProgressState.Operating
            || this.subscribedDay.State == DayProgressState.TransactionResult
            || this.subscribedDay.State == DayProgressState.Closing;
        bool settlement = this.subscribedDay.State == DayProgressState.Settlement;

        this.setPanelVisibility(this.preOpenPanel, preOpen);
        this.setPanelVisibility(this.operatingPanel, operating);
        this.setPanelVisibility(this.settlementPanel, settlement);
        this.setPanelVisibility(this.tributePanel, this.gameProgress.State == GameProgressState.Maintenance);
        this.setPanelVisibility(this.failurePanel, this.gameProgress.State == GameProgressState.Failed);
        this.openBusinessButton.interactable = preOpen;
        this.maintenanceButton.interactable = this.gameProgress.State == GameProgressState.Maintenance;
    }

    /// <summary>현재 날짜에 등장 가능한 상품을 영업 전 가격표에 표시합니다.</summary>
    /// <param name="day">가격표를 표시할 1부터 시작하는 게임 날짜입니다.</param>
    private void renderPriceList(int day)
    {
        if (this.priceListText == null || this.customerCatalog == null || this.textData == null)
        {
            return;
        }

        this.priceListText.text = this.viewDataFactory.CreatePriceListText(day);
    }

    /// <summary>진행 상태를 UI 표현 계약으로 변환합니다.</summary>
    /// <param name="state">변환할 하루 진행 상태입니다.</param>
    /// <returns>UI가 표시할 진행 단계입니다.</returns>
    private GameDayPhase toUiPhase(DayProgressState state)
    {
        return state switch
        {
            DayProgressState.PreOpen => GameDayPhase.PreOpen,
            DayProgressState.Operating => GameDayPhase.Operating,
            DayProgressState.TransactionResult => GameDayPhase.TradingResult,
            DayProgressState.Closing => GameDayPhase.Closing,
            DayProgressState.Settlement => GameDayPhase.DailySettlement,
            _ => GameDayPhase.PreOpen
        };
    }

    /// <summary>버튼 입력이 가능한 상태에 맞춰 동적 키패드를 잠급니다.</summary>
    /// <param name="isInteractable">키패드 입력 허용 여부입니다.</param>
    private void setKeypadInteractable(bool isInteractable)
    {
        if (this.keypadController != null)
        {
            this.keypadController.SetInputEnabled(isInteractable);
        }

        foreach (Button button in this.keypadButtons)
        {
            if (button != null)
            {
                button.interactable = isInteractable;
            }
        }
    }

    /// <summary>현재 하루가 일시정지를 받을 수 있는 상태인지 확인합니다.</summary>
    /// <returns>영업 중이고 아직 일시정지하지 않은 경우 true입니다.</returns>
    private bool canPause()
    {
        return this.gameProgress != null
            && this.gameProgress.State == GameProgressState.DayInProgress
            && this.subscribedDay != null
            && !this.subscribedDay.IsPaused
            && (this.subscribedDay.State == DayProgressState.Operating
                || this.subscribedDay.State == DayProgressState.TransactionResult);
    }

    /// <summary>현재 하루가 재개 요청을 받을 수 있는 상태인지 확인합니다.</summary>
    /// <returns>영업 중이고 일시정지된 경우 true입니다.</returns>
    private bool canResume()
    {
        return this.gameProgress != null
            && this.gameProgress.State == GameProgressState.DayInProgress
            && this.subscribedDay != null
            && this.subscribedDay.IsPaused
            && (this.subscribedDay.State == DayProgressState.Operating
                || this.subscribedDay.State == DayProgressState.TransactionResult);
    }

    /// <summary>기술 오류를 화면에 표시하고 추가 입력을 차단합니다.</summary>
    /// <param name="exception">표시하고 기록할 원본 오류입니다.</param>
    private void showError(Exception exception)
    {
        this.hasError = true;
        string message = exception?.Message ?? "Unknown progress error.";
        if (this.errorText != null)
        {
            this.errorText.text = message;
        }

        Debug.LogException(exception, this);
        this.setKeypadInteractable(false);
        if (this.openBusinessButton != null) this.openBusinessButton.interactable = false;
        if (this.transactionContinueButton != null) this.transactionContinueButton.interactable = false;
        if (this.maintenanceButton != null) this.maintenanceButton.interactable = false;
    }

    /// <summary>패널의 활성 상태를 설정합니다.</summary>
    /// <param name="panel">표시 상태를 변경할 씬 UI 패널입니다.</param>
    /// <param name="isVisible">패널 표시 여부입니다.</param>
    private void setPanelVisibility(GameObject panel, bool isVisible)
    {
        if (panel != null)
        {
            panel.SetActive(isVisible);
        }
    }

}
