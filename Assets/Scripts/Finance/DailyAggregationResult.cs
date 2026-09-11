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

/// <summary>현재 게임 실행 동안 유지되는 통합 정산 미납액과 고정 유예 기한의 단일 권위 상태입니다.</summary>
public sealed class SettlementDebtState
{
    /// <summary>현재 납부해야 하는 누적 미납액입니다.</summary>
    public long UnpaidAmount { get; private set; }
    /// <summary>미납액이 남아 있어 유예가 활성화됐는지 나타냅니다.</summary>
    public bool HasUnpaidAmount => UnpaidAmount > 0;
    /// <summary>현재 유예를 처음 발생시킨 미납 일차이며 미납이 없으면 null입니다.</summary>
    public int? FirstUnpaidDay { get; private set; }
    /// <summary>현재 유예의 고정 종료 일차이며 미납이 없으면 null입니다.</summary>
    public int? GracePeriodEndDay { get; private set; }

    /// <summary>새 미납과 고정 유예 기한을 시작합니다.</summary>
    /// <param name="firstUnpaidDay">납부에 처음 실패한 양수 게임 일차입니다.</param>
    /// <param name="unpaidAmount">전액 미납 처리할 양수 금액입니다.</param>
    /// <param name="gracePeriodEndDay">최초 미납 일차보다 뒤인 유예 종료 일차입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">일차 또는 금액이 유효하지 않은 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">기존 미납이 활성화된 상태에서 다시 시작하는 경우 발생합니다.</exception>
    internal void Begin(int firstUnpaidDay, long unpaidAmount, int gracePeriodEndDay)
    {
        if (HasUnpaidAmount) throw new InvalidOperationException("기존 미납이 활성화되어 있습니다.");
        if (firstUnpaidDay <= 0) throw new ArgumentOutOfRangeException(nameof(firstUnpaidDay));
        if (unpaidAmount <= 0) throw new ArgumentOutOfRangeException(nameof(unpaidAmount));
        if (gracePeriodEndDay <= firstUnpaidDay) throw new ArgumentOutOfRangeException(nameof(gracePeriodEndDay));

        UnpaidAmount = unpaidAmount;
        FirstUnpaidDay = firstUnpaidDay;
        GracePeriodEndDay = gracePeriodEndDay;
    }

    /// <summary>활성 유예의 기한을 바꾸지 않고 추가 미납액을 누적합니다.</summary>
    /// <param name="additionalAmount">추가할 양수 미납액입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">추가 금액이 양수가 아닌 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">활성 미납이 없는 경우 발생합니다.</exception>
    /// <exception cref="OverflowException">누적 미납액이 자료형 범위를 초과한 경우 발생합니다.</exception>
    internal void Add(long additionalAmount)
    {
        if (!HasUnpaidAmount) throw new InvalidOperationException("추가할 기존 미납이 없습니다.");
        if (additionalAmount <= 0) throw new ArgumentOutOfRangeException(nameof(additionalAmount));
        UnpaidAmount = checked(UnpaidAmount + additionalAmount);
    }

    /// <summary>완납된 미납액과 유예 기준을 함께 초기화합니다.</summary>
    internal void Clear()
    {
        UnpaidAmount = 0;
        FirstUnpaidDay = null;
        GracePeriodEndDay = null;
    }

    /// <summary>현재 일차 이후 유예 종료까지 남은 날짜 수를 계산합니다.</summary>
    /// <param name="currentDay">현재 양수 게임 일차입니다.</param>
    /// <returns>미납이 없거나 기한에 도달했으면 0, 그 외에는 종료 일차와 현재 일차의 차이입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">현재 일차가 양수가 아닌 경우 발생합니다.</exception>
    public int GetRemainingGraceDays(int currentDay)
    {
        if (currentDay <= 0) throw new ArgumentOutOfRangeException(nameof(currentDay));
        if (!GracePeriodEndDay.HasValue) return 0;
        return Math.Max(0, GracePeriodEndDay.Value - currentDay);
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

/// <summary>영업 집계와 하루 종료 시점의 통합 납부 결과를 함께 보존하는 최종 정산 결과입니다.</summary>
public readonly struct DailySettlementResult
{
    /// <summary>정산의 근거가 되는 영업 집계 snapshot입니다.</summary>
    public DailyAggregationResult Aggregation { get; }
    /// <summary>오늘 부과된 유지비입니다.</summary>
    public long MaintenanceAmount { get; }
    /// <summary>오늘 발생한 일일지침 벌금입니다.</summary>
    public long GuidelinePenaltyAmount => Aggregation.DailyGuidelinePenaltyAmount;
    /// <summary>정산 전부터 남아 있던 미납액입니다.</summary>
    public long PreviousUnpaidAmount { get; }
    /// <summary>기존 미납액, 오늘 유지비와 오늘 벌금의 합계입니다.</summary>
    public long TotalPaymentDue { get; }
    /// <summary>이번 정산에서 실제로 납부된 금액입니다.</summary>
    public long PaidAmount { get; }
    /// <summary>통합 납부 처리 이후 보유금입니다.</summary>
    public long BalanceAfterSettlement { get; }
    /// <summary>이번 정산 이후 남은 미납액입니다.</summary>
    public long UnpaidAmount { get; }
    /// <summary>활성 유예의 종료 일차이며 유예가 없으면 null입니다.</summary>
    public int? GracePeriodEndDay { get; }
    /// <summary>정산 이후 남은 유예 일수입니다.</summary>
    public int RemainingGraceDays { get; }
    /// <summary>유예 만료로 게임오버 조건이 성립했는지 나타냅니다.</summary>
    public bool IsGameOverConditionMet { get; }

    /// <summary>최종 정산 결과를 생성하고 금액 관계를 검증합니다.</summary>
    /// <param name="aggregation">확정된 영업 집계입니다.</param>
    /// <param name="maintenanceAmount">오늘 유지비입니다.</param>
    /// <param name="previousUnpaidAmount">정산 전 미납액입니다.</param>
    /// <param name="paidAmount">실제 납부액입니다.</param>
    /// <param name="balanceAfterSettlement">납부 후 보유금입니다.</param>
    /// <param name="unpaidAmount">납부 후 미납액입니다.</param>
    /// <param name="gracePeriodEndDay">활성 유예 종료 일차입니다.</param>
    /// <param name="remainingGraceDays">남은 유예 일수입니다.</param>
    /// <param name="isGameOverConditionMet">게임오버 조건 성립 여부입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">금액이나 남은 일수가 음수인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">납부액과 미납액이 총 납부 필요액과 일치하지 않는 경우 발생합니다.</exception>
    public DailySettlementResult(
        DailyAggregationResult aggregation,
        long maintenanceAmount,
        long previousUnpaidAmount,
        long paidAmount,
        long balanceAfterSettlement,
        long unpaidAmount,
        int? gracePeriodEndDay = null,
        int remainingGraceDays = 0,
        bool isGameOverConditionMet = false)
    {
        if (maintenanceAmount < 0 || previousUnpaidAmount < 0 || paidAmount < 0 ||
            balanceAfterSettlement < 0 || unpaidAmount < 0 || remainingGraceDays < 0)
            throw new ArgumentOutOfRangeException(nameof(maintenanceAmount), "정산 금액과 남은 유예 일수는 음수일 수 없습니다.");

        long totalPaymentDue = checked(previousUnpaidAmount + maintenanceAmount + aggregation.DailyGuidelinePenaltyAmount);
        if (checked(paidAmount + unpaidAmount) != totalPaymentDue)
            throw new ArgumentException("실제 납부액과 미납액의 합은 총 납부 필요액과 일치해야 합니다.");
        if ((gracePeriodEndDay.HasValue || remainingGraceDays > 0 || isGameOverConditionMet) && unpaidAmount == 0)
            throw new ArgumentException("미납액이 없으면 유예 또는 게임오버 조건을 설정할 수 없습니다.");

        Aggregation = aggregation;
        MaintenanceAmount = maintenanceAmount;
        PreviousUnpaidAmount = previousUnpaidAmount;
        TotalPaymentDue = totalPaymentDue;
        PaidAmount = paidAmount;
        BalanceAfterSettlement = balanceAfterSettlement;
        UnpaidAmount = unpaidAmount;
        GracePeriodEndDay = gracePeriodEndDay;
        RemainingGraceDays = remainingGraceDays;
        IsGameOverConditionMet = isGameOverConditionMet;
    }
}
