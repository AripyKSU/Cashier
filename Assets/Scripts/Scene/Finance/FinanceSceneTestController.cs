using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FinanceScene의 경제 상태 UI를 갱신하고 테스트 전용 버튼으로 임시 경제 흐름을 호출합니다.
/// 상태 표시 영역은 이후 GameSessionManager의 조회 서비스에 연결할 수 있도록 유지하며,
/// CSV 직접 초기화와 버튼·피드백은 Finance 검증을 위한 테스트 어댑터입니다.
/// </summary>
public sealed class FinanceSceneTestController : MonoBehaviour
{
    private const long SaleIncomeSmall = 5000;
    private const long SaleIncomeLarge = 20000;
    private const int MaxLogCount = 30;

    [Header("Test-only CSV bootstrap")]
    [SerializeField] private TextAsset economyBalanceCsv;
    [SerializeField] private TextAsset maintenanceBalanceCsv;

    [Header("Runtime finance state UI")]
    [SerializeField] private TextMeshProUGUI balanceText;
    [SerializeField] private TextMeshProUGUI dailySaleIncomeText;
    [SerializeField] private TextMeshProUGUI dayOpenStateText;
    [SerializeField] private TextMeshProUGUI maintenanceCycleText;
    [SerializeField] private TextMeshProUGUI nextMaintenanceAmountText;

    [Header("Test-only feedback UI")]
    [SerializeField] private TextMeshProUGUI lastActionText;
    [SerializeField] private TextMeshProUGUI logText;

    [Header("Test-only controls")]
    [SerializeField] private Button beginDayButton;
    [SerializeField] private Button addSale5000Button;
    [SerializeField] private Button addSale20000Button;
    [SerializeField] private Button endDayButton;
    [SerializeField] private Button payMaintenanceButton;
    [SerializeField] private Button resetTestButton;

    private readonly List<string> logs = new List<string>();
    private EconomyRuntime economy;
    private EconomyQueryService query;
    private EconomyLogService logService;

    /// <summary>
    /// Inspector에 연결된 버튼에 테스트 동작을 등록합니다.
    /// </summary>
    private void Awake()
    {
        this.addButtonListeners();
    }

    /// <summary>
    /// Finance CSV를 파싱하고 경제 런타임과 조회 서비스를 초기화합니다.
    /// </summary>
    private void Start()
    {
        this.initializeEconomy();
    }

    /// <summary>
    /// 버튼과 경제 이벤트 구독을 Scene 수명 종료 전에 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        this.removeButtonListeners();
        this.disposeEconomy();
    }

    /// <summary>
    /// CSV에서 새로운 테스트용 경제 런타임을 생성합니다.
    /// </summary>
    private void initializeEconomy()
    {
        this.disposeEconomy();
        this.logs.Clear();
        this.refreshLogView();
        this.setInteractionState(false);
        this.setLastAction("Initializing economy test.");

        try
        {
            this.validateCsvReferences();

            EconomyBalanceDataTable economyTable = new EconomyBalanceDataTable();
            economyTable.LoadData(this.economyBalanceCsv.text);

            MaintenanceBalanceDataTable maintenanceTable = new MaintenanceBalanceDataTable();
            maintenanceTable.LoadData(this.maintenanceBalanceCsv.text);

            this.economy = new EconomyRuntime(
                economyTable.GetData(),
                maintenanceTable.GetMaintenanceAmounts());
            this.query = this.economy.QueryService;
            this.logService = this.economy.LogService;
            this.logService.LogAdded += this.handleLogAdded;

            this.setLastAction("Economy test initialized.");
            this.addLog("[Economy] Finance test runtime initialized");
            this.refreshView();
        }
        catch (Exception exception)
        {
            this.disposeEconomy();
            this.setLastAction($"Initialization failed: {exception.Message}");
            this.refreshView();
            this.setInteractionState(false);
            Debug.LogError($"[FinanceSceneTestController] 경제 초기화 실패: {exception}");
        }
    }

    /// <summary>
    /// 시작 보유금과 상납금 설정 CSV가 연결되었는지 확인합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">필수 CSV 참조가 없을 때 발생합니다.</exception>
    private void validateCsvReferences()
    {
        if (this.economyBalanceCsv == null)
        {
            throw new InvalidOperationException("EconomyBalanceData.csv is not assigned.");
        }

        if (this.maintenanceBalanceCsv == null)
        {
            throw new InvalidOperationException("MaintenanceBalanceData.csv is not assigned.");
        }
    }

    /// <summary>
    /// 영업 시작을 요청하고 중복 시작 오류를 화면에 표시합니다.
    /// </summary>
    private void beginDay()
    {
        if (!this.canUseEconomy())
        {
            return;
        }

        try
        {
            this.economy.DailyAggregationService.BeginDay();
            this.setLastAction("Day opened. Daily sale income is 0.");
        }
        catch (InvalidOperationException exception)
        {
            this.setLastAction("Begin day failed: the day is already open.");
            Debug.Log($"[FinanceSceneTestController] Begin day request was rejected: {exception.Message}");
        }
        catch (Exception exception)
        {
            this.setLastAction("Begin day failed. See Console for details.");
            Debug.LogError($"[FinanceSceneTestController] Begin day failed: {exception}");
        }

        this.refreshView();
    }

    /// <summary>
    /// 지정한 임시 판매 수입을 정상 일일 집계 경로로 반영합니다.
    /// </summary>
    /// <param name="saleIncome">반영할 임시 판매 수입입니다.</param>
    private void addSale(long saleIncome)
    {
        if (!this.canUseEconomy())
        {
            return;
        }

        try
        {
            bool wasApplied = this.economy.DailyAggregationService.TryApplyTransaction(
                new TransactionResult(saleIncome, 0));

            this.setLastAction(
                wasApplied
                    ? $"Sale income {saleIncome:N0} was applied."
                    : "Day is not open, so the transaction was not applied.");
        }
        catch (Exception exception)
        {
            this.setLastAction($"Sale failed: {exception.Message}");
            Debug.LogError($"[FinanceSceneTestController] 판매 반영 실패: {exception}");
        }

        this.refreshView();
    }

    /// <summary>
    /// 영업을 종료하고 날짜를 진행하지 않은 당일 집계 결과를 표시합니다.
    /// </summary>
    private void endDay()
    {
        if (!this.canUseEconomy())
        {
            return;
        }

        try
        {
            DailyAggregationResult result = this.economy.DailyAggregationService.EndDay();
            // 테스트 화면의 End Day는 일일 집계만 종료하며 날짜 증가를 수행하지 않습니다.
            this.setLastAction($"Day ended. Daily sale income: {result.SaleIncome:N0}.");
        }
        catch (InvalidOperationException exception)
        {
            this.setLastAction("End day failed: no day is open.");
            Debug.Log($"[FinanceSceneTestController] End day request was rejected: {exception.Message}");
        }
        catch (Exception exception)
        {
            this.setLastAction("End day failed. See Console for details.");
            Debug.LogError($"[FinanceSceneTestController] End day failed: {exception}");
        }

        this.refreshView();
    }

    /// <summary>
    /// 다음 회차 상납금 납부를 시도하고 성공·실패 결과를 표시합니다.
    /// </summary>
    private void payMaintenance()
    {
        if (!this.canUseEconomy())
        {
            return;
        }

        try
        {
            int nextRound = this.economy.MaintenanceService.LastPaidRound + 1;
            if (!this.query.TryGetNextMaintenanceAmount(out long configuredAmount))
            {
                this.setLastAction("No maintenance configuration remains.");
                this.refreshView();
                return;
            }

            bool wasPaid = this.economy.MaintenanceService.TryPay(
                nextRound,
                out MaintenancePaymentResult result);

            this.setLastAction(
                wasPaid
                    ? $"Maintenance {result.RequiredAmount:N0} paid. Balance: {result.CurrentBalance:N0}."
                    : $"Maintenance failed. Required: {configuredAmount:N0}; balance: {result.CurrentBalance:N0}.");
        }
        catch (Exception exception)
        {
            this.setLastAction($"Maintenance failed: {exception.Message}");
            Debug.LogError($"[FinanceSceneTestController] 상납금 납부 실패: {exception}");
        }

        this.refreshView();
    }

    /// <summary>
    /// CSV를 다시 읽어 시작 보유금과 모든 테스트 상태를 초기화합니다.
    /// </summary>
    private void resetTest()
    {
        this.initializeEconomy();
    }

    /// <summary>
    /// 현재 런타임의 상태를 조회 서비스에서 읽어 UI에 반영합니다.
    /// </summary>
    private void refreshView()
    {
        if (this.query == null)
        {
            this.setText(this.balanceText, "-");
            this.setText(this.dailySaleIncomeText, "-");
            this.setText(this.dayOpenStateText, "Initialization failed");
            this.setText(this.maintenanceCycleText, "-");
            this.setText(this.nextMaintenanceAmountText, "-");
            this.setInteractionState(false);
            return;
        }

        this.setText(this.balanceText, this.query.CurrentBalance.ToString("N0"));
        this.setText(this.dailySaleIncomeText, this.query.DailySaleIncome.ToString("N0"));
        this.setText(this.dayOpenStateText, this.query.IsDayOpen ? "Day open" : "Day closed");
        this.setText(this.maintenanceCycleText, $"Every {this.query.MaintenanceCycleDays} days");

        if (this.query.TryGetNextMaintenanceAmount(out long amount))
        {
            this.setText(this.nextMaintenanceAmountText, amount.ToString("N0"));
        }
        else
        {
            this.setText(this.nextMaintenanceAmountText, "No maintenance configuration");
        }

        this.setInteractionState(true);
    }

    /// <summary>
    /// 런타임 유효 여부에 따라 계획된 버튼 상태를 적용합니다.
    /// </summary>
    /// <param name="isInitialized">경제 런타임 초기화 성공 여부입니다.</param>
    private void setInteractionState(bool isInitialized)
    {
        bool isDayOpen = isInitialized && this.query != null && this.query.IsDayOpen;
        bool hasMaintenance = isInitialized
            && this.query != null
            && this.query.TryGetNextMaintenanceAmount(out _);

        this.setButtonInteractable(this.beginDayButton, isInitialized && !isDayOpen);
        this.setButtonInteractable(this.addSale5000Button, isInitialized && isDayOpen);
        this.setButtonInteractable(this.addSale20000Button, isInitialized && isDayOpen);
        this.setButtonInteractable(this.endDayButton, isInitialized && isDayOpen);
        this.setButtonInteractable(this.payMaintenanceButton, isInitialized && !isDayOpen && hasMaintenance);
        // Reset은 초기화 실패에서도 재시도할 수 있어 항상 사용할 수 있게 둡니다.
        this.setButtonInteractable(this.resetTestButton, true);
    }

    /// <summary>
    /// 버튼 클릭 이벤트를 등록합니다.
    /// </summary>
    private void addButtonListeners()
    {
        this.beginDayButton?.onClick.AddListener(this.beginDay);
        this.addSale5000Button?.onClick.AddListener(this.addSale5000);
        this.addSale20000Button?.onClick.AddListener(this.addSale20000);
        this.endDayButton?.onClick.AddListener(this.endDay);
        this.payMaintenanceButton?.onClick.AddListener(this.payMaintenance);
        this.resetTestButton?.onClick.AddListener(this.resetTest);
    }

    /// <summary>
    /// 버튼 클릭 이벤트를 해제합니다.
    /// </summary>
    private void removeButtonListeners()
    {
        this.beginDayButton?.onClick.RemoveListener(this.beginDay);
        this.addSale5000Button?.onClick.RemoveListener(this.addSale5000);
        this.addSale20000Button?.onClick.RemoveListener(this.addSale20000);
        this.endDayButton?.onClick.RemoveListener(this.endDay);
        this.payMaintenanceButton?.onClick.RemoveListener(this.payMaintenance);
        this.resetTestButton?.onClick.RemoveListener(this.resetTest);
    }

    /// <summary>
    /// 5,000원 임시 판매 수입 버튼 동작입니다.
    /// </summary>
    private void addSale5000()
    {
        this.addSale(SaleIncomeSmall);
    }

    /// <summary>
    /// 20,000원 임시 판매 수입 버튼 동작입니다.
    /// </summary>
    private void addSale20000()
    {
        this.addSale(SaleIncomeLarge);
    }

    /// <summary>
    /// 경제 런타임이 준비되었는지 확인합니다.
    /// </summary>
    /// <returns>경제 런타임과 조회 서비스가 준비되었으면 true입니다.</returns>
    private bool canUseEconomy()
    {
        if (this.economy != null && this.query != null)
        {
            return true;
        }

        this.setLastAction("Economy runtime is not initialized.");
        return false;
    }

    /// <summary>
    /// 현재 UI 로그 구독과 경제 런타임을 정리합니다.
    /// </summary>
    private void disposeEconomy()
    {
        if (this.logService != null)
        {
            this.logService.LogAdded -= this.handleLogAdded;
            this.logService = null;
        }

        // Runtime이 소유한 로그 이벤트 구독을 함께 해제합니다.
        this.economy?.Dispose();
        this.economy = null;
        this.query = null;
    }

    /// <summary>
    /// 새 로그를 최근 목록에 추가하고 테스트 UI에 표시합니다.
    /// </summary>
    /// <param name="entry">표시할 구조화된 경제 로그입니다.</param>
    private void handleLogAdded(EconomyLogEntry entry)
    {
        this.addLog(this.formatLogEntry(entry));
    }

    /// <summary>
    /// 구조화된 로그를 FinanceScene에서 읽을 수 있는 한 줄로 변환합니다.
    /// </summary>
    /// <param name="entry">변환할 경제 로그입니다.</param>
    /// <returns>순서와 잔고 변화가 포함된 표시 문자열입니다.</returns>
    private string formatLogEntry(EconomyLogEntry entry)
    {
        return $"[Economy] #{entry.Sequence} {entry.Reason} | "
            + $"{entry.PreviousBalance:N0} {this.formatDelta(entry.BalanceDelta)} = "
            + $"{entry.CurrentBalance:N0}";
    }

    /// <summary>
    /// 양수·음수 잔고 변화량을 로그 표시용 문자열로 변환합니다.
    /// </summary>
    /// <param name="delta">표시할 잔고 변화량입니다.</param>
    /// <returns>부호와 천 단위 구분이 포함된 변화량입니다.</returns>
    private string formatDelta(long delta)
    {
        if (delta > 0)
        {
            return $"+ {delta:N0}";
        }

        if (delta < 0)
        {
            return $"- {Math.Abs(delta):N0}";
        }

        return "0";
    }

    /// <summary>
    /// 최근 로그 목록에 한 줄을 추가합니다.
    /// </summary>
    /// <param name="message">추가할 로그 문자열입니다.</param>
    private void addLog(string message)
    {
        this.logs.Add(message);
        if (this.logs.Count > MaxLogCount)
        {
            this.logs.RemoveAt(0);
        }

        this.refreshLogView();
    }

    /// <summary>
    /// 최근 로그 목록을 한 텍스트 영역에 표시합니다.
    /// </summary>
    private void refreshLogView()
    {
        if (this.logText != null)
        {
            this.logText.text = string.Join("\n", this.logs);
        }
    }

    /// <summary>
    /// 마지막 동작 결과를 표시합니다.
    /// </summary>
    /// <param name="message">표시할 동작 결과입니다.</param>
    private void setLastAction(string message)
    {
        this.setText(this.lastActionText, message);
    }

    /// <summary>
    /// TextMesh Pro 텍스트가 연결된 경우에만 문자열을 대입합니다.
    /// </summary>
    /// <param name="target">대상 텍스트입니다.</param>
    /// <param name="value">표시할 문자열입니다.</param>
    private void setText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    /// <summary>
    /// Button이 연결된 경우에만 상호작용 가능 여부를 변경합니다.
    /// </summary>
    /// <param name="button">대상 버튼입니다.</param>
    /// <param name="isInteractable">상호작용 가능 여부입니다.</param>
    private void setButtonInteractable(Button button, bool isInteractable)
    {
        if (button != null)
        {
            button.interactable = isInteractable;
        }
    }
}
