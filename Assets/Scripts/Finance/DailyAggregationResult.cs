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
    /// <param name="reputationDelta">기존 호환용 하루 명성 변화량입니다.</param>
    /// <param name="transactions">하루 동안 접수된 거래 snapshot입니다.</param>
    internal DailyAggregationResult(long saleIncome, int reputationDelta, IReadOnlyList<TransactionResult> transactions)
    {
        this.SaleIncome = saleIncome;
        this.ReputationDelta = reputationDelta;
        this.Transactions = new List<TransactionResult>(transactions ?? Array.Empty<TransactionResult>()).AsReadOnly();
    }
}
