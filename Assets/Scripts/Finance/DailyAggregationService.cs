using System;
using System.Collections.Generic;

/// <summary>
/// 영업 중 완료된 거래 결과를 재정에 반영하고 하루 판매 수입과 명성 변화량을 집계합니다.
/// </summary>
public sealed class DailyAggregationService
{
    // 판매 수입을 실제 보유금에 반영하는 재정 시스템입니다.
    private readonly FinanceService financeService;

    // 현재 영업일에 완료된 거래의 판매 수입 누적값입니다.
    private long dailySaleIncome;

    // 현재 영업일에 완료된 거래의 명성 변화 누적값입니다.
    private int dailyReputationDelta;

    // 현재 영업일에 접수한 모든 거래 결과 snapshot입니다. 결제 거절도 포함합니다.
    private readonly List<TransactionResult> dailyTransactions = new List<TransactionResult>();

    // 거래 결과를 받아들일 수 있는 영업 중 상태인지 나타냅니다.
    private bool isDayOpen;

    /// <summary>
    /// 현재 일일 집계가 거래 결과를 받고 있는지 나타냅니다.
    /// </summary>
    public bool IsDayOpen => this.isDayOpen;

    /// <summary>
    /// 현재 영업일에 누적된 판매 수입입니다.
    /// </summary>
    public long DailySaleIncome => this.dailySaleIncome;

    /// <summary>
    /// 현재 영업일에 누적된 명성 변화량입니다.
    /// </summary>
    public int DailyReputationDelta => this.dailyReputationDelta;

    /// <summary>현재 영업일에 접수한 성공·거절 거래 수입니다.</summary>
    public int DailyTransactionCount => this.dailyTransactions.Count;

    /// <summary>
    /// 일일 판매 수입을 반영할 재정 시스템을 지정합니다.
    /// </summary>
    /// <param name="financeService">현재 보유금을 관리하는 재정 시스템입니다.</param>
    /// <exception cref="ArgumentNullException">재정 시스템이 null인 경우 발생합니다.</exception>
    public DailyAggregationService(FinanceService financeService)
    {
        this.financeService = financeService ?? throw new ArgumentNullException(nameof(financeService));
    }

    /// <summary>
    /// 새로운 영업일의 거래 결과 접수를 시작합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">이미 영업일이 진행 중인 경우 발생합니다.</exception>
    public void BeginDay()
    {
        if (this.isDayOpen)
        {
            // 진행 중인 집계가 실수로 초기화되지 않도록 중복 시작을 거부합니다.
            throw new InvalidOperationException("이미 일일 집계가 진행 중입니다.");
        }

        this.isDayOpen = true;
    }

    /// <summary>
    /// 완료된 거래 결과를 재정과 현재 일일 집계에 반영합니다.
    /// </summary>
    /// <param name="transactionResult">반영할 임시 완료 거래 결과입니다.</param>
    /// <returns>영업 중 결과를 반영했으면 true, 영업 종료 후 도착한 결과라면 false입니다.</returns>
    /// <exception cref="OverflowException">일일 누적값 또는 현재 보유금이 자료형의 범위를 초과하는 경우 발생합니다.</exception>
    public bool TryApplyTransaction(TransactionResult transactionResult)
    {
        if (!this.isDayOpen)
        {
            // 하루 종료 후 도착한 거래 결과는 재정과 집계에 반영하지 않습니다.
            return false;
        }

        // 재정 변경 전에 모든 일일 누적값의 범위를 확인해 부분 갱신을 방지합니다.
        long nextDailySaleIncome = checked(this.dailySaleIncome + transactionResult.SaleIncome);
        int nextDailyReputationDelta = checked(this.dailyReputationDelta + transactionResult.ReputationDelta);

        if (transactionResult.SaleIncome > 0)
        {
            this.financeService.AddIncome(transactionResult.SaleIncome, FinanceChangeReason.Sale);
        }

        // 재정 반영이 완료된 결과만 현재 영업일의 집계값으로 확정합니다.
        this.dailySaleIncome = nextDailySaleIncome;
        this.dailyReputationDelta = nextDailyReputationDelta;
        this.dailyTransactions.Add(transactionResult);
        return true;
    }

    /// <summary>
    /// 거래 결과 접수를 종료하고 확정된 일일 집계 결과를 반환합니다.
    /// </summary>
    /// <returns>영업 종료 시점까지 누적된 판매 수입과 명성 변화량입니다.</returns>
    /// <exception cref="InvalidOperationException">진행 중인 영업일이 없는 경우 발생합니다.</exception>
    public DailyAggregationResult EndDay()
    {
        if (!this.isDayOpen)
        {
            // 종료되지 않은 영업일이 없으므로 중복 종료를 거부합니다.
            throw new InvalidOperationException("진행 중인 일일 집계가 없습니다.");
        }

        // 거래 접수를 먼저 닫아 종료 처리 중 들어오는 추가 결과를 거부합니다.
        this.isDayOpen = false;
        DailyAggregationResult result = new DailyAggregationResult(
            this.dailySaleIncome,
            0,
            this.dailyReputationDelta,
            this.dailyTransactions);

        // 반환 결과와 현재 집계 상태를 분리한 뒤 다음 영업일을 위해 누적값을 초기화합니다.
        this.resetAggregation();
        return result;
    }

    /// <summary>
    /// 현재 일일 누적값을 초기 상태로 되돌립니다.
    /// </summary>
    private void resetAggregation()
    {
        this.dailySaleIncome = 0;
        this.dailyReputationDelta = 0;
        this.dailyTransactions.Clear();
    }
}
