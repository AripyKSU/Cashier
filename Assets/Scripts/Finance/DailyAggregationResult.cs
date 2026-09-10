using System;
using System.Collections.Generic;

/// <summary>
/// 하루 동안 접수된 거래와 경제 변화의 집계 결과입니다.
/// </summary>
public readonly struct DailyAggregationResult
{
    /// <summary>
    /// 하루 동안 누적된 판매 수입입니다.
    /// </summary>
    public long SaleIncome { get; }

    /// <summary>하루 종료 시 차감된 유지비입니다.</summary>
    public long Expenses { get; }

    /// <summary>판매 수입에서 유지비를 뺀 일일 순이익입니다.</summary>
    public long NetProfit { get; }

    /// <summary>
    /// 하루 동안 누적된 명성 변화량입니다.
    /// </summary>
    public int ReputationDelta { get; }

    /// <summary>하루 동안 접수된 성공·거절 거래 snapshot입니다.</summary>
    public IReadOnlyList<TransactionResult> Transactions { get; }

    /// <summary>
    /// 일일 집계 결과를 생성합니다.
    /// </summary>
    /// <param name="saleIncome">하루 동안 누적된 판매 수입입니다.</param>
    /// <param name="expenses">하루 종료 시 차감된 유지비입니다.</param>
    /// <param name="reputationDelta">기존 호환용 하루 명성 변화량입니다.</param>
    /// <param name="transactions">하루 동안 접수된 거래 snapshot입니다.</param>
    internal DailyAggregationResult(long saleIncome, long expenses, int reputationDelta, IReadOnlyList<TransactionResult> transactions)
    {
        this.SaleIncome = saleIncome;
        this.Expenses = expenses;
        this.NetProfit = checked(saleIncome - expenses);
        this.ReputationDelta = reputationDelta;
        this.Transactions = new List<TransactionResult>(transactions ?? Array.Empty<TransactionResult>()).AsReadOnly();
    }
}
