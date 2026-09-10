using System;

/// <summary>
/// 한 거래의 명성 판정과 가격 비율을 기록한 디버그 로그입니다.
/// </summary>
public readonly struct ReputationTransactionLogEntry
{
    /// <summary>세션 내 거래 로그 순서입니다.</summary>
    public long Sequence { get; }

    /// <summary>거래가 발생한 게임 날짜입니다.</summary>
    public int Day { get; }

    /// <summary>거래 손님의 성향 타입입니다.</summary>
    public CustomerDispositionType DispositionType { get; }

    /// <summary>거래 손님의 속성 snapshot입니다.</summary>
    public CustomerAttributes CustomerAttributes { get; }

    /// <summary>손님의 거래 판정입니다.</summary>
    public CustomerTradeOutcome Outcome { get; }

    /// <summary>명성 정산용 거래 등급입니다.</summary>
    public ReputationTransactionGrade Grade { get; }

    /// <summary>플레이어가 제시한 금액입니다.</summary>
    public long? OfferedTotal { get; }

    /// <summary>거래 당시 정가 기준 합계입니다.</summary>
    public long? ReferenceTotal { get; }

    /// <summary>정가 대비 제시 금액의 백분율입니다.</summary>
    public decimal? PriceRatioPercent { get; }

    /// <summary>거래 결과로부터 명성 거래 로그를 생성합니다.</summary>
    /// <param name="sequence">세션 내 로그 순서입니다.</param>
    /// <param name="day">거래가 발생한 게임 날짜입니다.</param>
    /// <param name="transaction">거래 snapshot입니다.</param>
    /// <param name="grade">분류된 명성 거래 등급입니다.</param>
    internal ReputationTransactionLogEntry(long sequence, int day, TransactionResult transaction,
        ReputationTransactionGrade grade)
    {
        this.Sequence = sequence;
        this.Day = day;
        this.DispositionType = transaction.DispositionType;
        this.CustomerAttributes = transaction.CustomerAttributes;
        this.Outcome = transaction.Outcome;
        this.Grade = grade;
        this.OfferedTotal = transaction.OfferedTotal;
        this.ReferenceTotal = transaction.ReferenceTotal;
        this.PriceRatioPercent = transaction.OfferedTotal.HasValue && transaction.ReferenceTotal.HasValue
            && transaction.ReferenceTotal.Value > 0
            ? (decimal)transaction.OfferedTotal.Value * 100m / transaction.ReferenceTotal.Value
            : (decimal?)null;
    }
}
