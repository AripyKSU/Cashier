using System;
using System.Collections.Generic;

/// <summary>하루 동안 동일한 조건의 일일지침에서 발생한 위반 횟수와 벌금 합계입니다.</summary>
public readonly struct DailyGuidelineViolationSummary
{
    /// <summary>위반 횟수를 집계한 일일지침입니다.</summary>
    public DailyGuideline Guideline { get; }

    /// <summary>해당 지침을 위반한 거래 건수입니다.</summary>
    public int ViolationCount { get; }

    /// <summary>해당 지침의 위반 횟수에 따른 벌금 합계입니다.</summary>
    public long PenaltyAmount { get; }

    /// <summary>동일한 일일지침의 위반 집계 결과를 생성합니다.</summary>
    /// <param name="guideline">집계 대상 일일지침입니다.</param>
    /// <param name="violationCount">해당 지침을 위반한 거래 건수입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">위반 건수가 양수가 아닌 경우 발생합니다.</exception>
    /// <exception cref="OverflowException">벌금 합계가 자료형 범위를 초과한 경우 발생합니다.</exception>
    public DailyGuidelineViolationSummary(DailyGuideline guideline, int violationCount)
    {
        guideline.Validate();
        if (violationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(violationCount), violationCount, "위반 건수는 양수여야 합니다.");

        Guideline = guideline;
        ViolationCount = violationCount;
        PenaltyAmount = checked(guideline.PenaltyAmount * violationCount);
    }
}

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

    /// <summary>하루 동안 성립한 거래에서 발생한 정식 일일지침 위반 건수입니다.</summary>
    public int DailyGuidelineViolationCount { get; }

    /// <summary>하루의 지침별 고정 벌금을 합산한 예정 벌금입니다.</summary>
    public long DailyGuidelinePenaltyAmount { get; }

    /// <summary>정산 표시와 상세 확인에 사용하는 지침 위반 snapshot입니다.</summary>
    public IReadOnlyList<DailyGuidelineViolation> DailyGuidelineViolations { get; }

    /// <summary>대상 물품과 손님 조건까지 동일한 지침별 위반 집계입니다.</summary>
    public IReadOnlyList<DailyGuidelineViolationSummary> DailyGuidelineViolationSummaries { get; }

    /// <summary>하루 동안 접수된 성공·거절 거래 snapshot입니다.</summary>
    public IReadOnlyList<TransactionResult> Transactions { get; }

    /// <summary>
    /// 일일 집계 결과를 생성합니다.
    /// </summary>
    /// <param name="saleIncome">하루 동안 누적된 판매 수입입니다.</param>
    /// <param name="expenses">하루 종료 시 차감된 유지비입니다.</param>
    /// <param name="reputationDelta">기존 호환용 하루 명성 변화량입니다.</param>
    /// <param name="transactions">하루 동안 접수된 거래 snapshot입니다.</param>
    /// <param name="dailyGuidelineViolationCount">정식 일일지침 위반 건수입니다.</param>
    /// <param name="dailyGuidelinePenaltyAmount">실제 차감 전 지침 벌금 예정액입니다.</param>
    /// <param name="dailyGuidelineViolations">정산 표시용 지침 위반 내역입니다.</param>
    internal DailyAggregationResult(
        long saleIncome,
        long expenses,
        int reputationDelta,
        IReadOnlyList<TransactionResult> transactions,
        int dailyGuidelineViolationCount = 0,
        long dailyGuidelinePenaltyAmount = 0,
        IReadOnlyList<DailyGuidelineViolation> dailyGuidelineViolations = null)
    {
        if (dailyGuidelineViolationCount < 0)
            throw new ArgumentOutOfRangeException(nameof(dailyGuidelineViolationCount));
        if (dailyGuidelinePenaltyAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(dailyGuidelinePenaltyAmount));
        var violationCopy = new List<DailyGuidelineViolation>(
            dailyGuidelineViolations ?? Array.Empty<DailyGuidelineViolation>());
        if (violationCopy.Count != dailyGuidelineViolationCount)
            throw new ArgumentException("지침 위반 건수와 상세 내역 수가 일치해야 합니다.", nameof(dailyGuidelineViolations));

        var guidelineCounts = new Dictionary<DailyGuideline, int>();
        long calculatedPenaltyAmount = 0;
        foreach (DailyGuidelineViolation violation in violationCopy)
        {
            violation.Guideline.Validate();
            guidelineCounts.TryGetValue(violation.Guideline, out int count);
            guidelineCounts[violation.Guideline] = checked(count + 1);
            calculatedPenaltyAmount = checked(calculatedPenaltyAmount + violation.PenaltyAmount);
        }
        if (calculatedPenaltyAmount != dailyGuidelinePenaltyAmount)
            throw new ArgumentException("지침 벌금 합계와 상세 내역의 벌금 합계가 일치해야 합니다.", nameof(dailyGuidelinePenaltyAmount));

        var summaries = new List<DailyGuidelineViolationSummary>(guidelineCounts.Count);
        foreach (KeyValuePair<DailyGuideline, int> pair in guidelineCounts)
            summaries.Add(new DailyGuidelineViolationSummary(pair.Key, pair.Value));

        this.SaleIncome = saleIncome;
        this.Expenses = expenses;
        this.NetProfit = checked(saleIncome - expenses);
        this.ReputationDelta = reputationDelta;
        this.DailyGuidelineViolationCount = dailyGuidelineViolationCount;
        this.DailyGuidelinePenaltyAmount = dailyGuidelinePenaltyAmount;
        this.DailyGuidelineViolations = violationCopy.AsReadOnly();
        this.DailyGuidelineViolationSummaries = summaries.AsReadOnly();
        this.Transactions = new List<TransactionResult>(transactions ?? Array.Empty<TransactionResult>()).AsReadOnly();
    }
}
