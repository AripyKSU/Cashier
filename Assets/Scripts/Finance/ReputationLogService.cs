using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 거래별 명성 판정과 일일 명성 정산 과정을 세션 로그로 수집합니다.
/// 로그 기록은 계산 결과를 관찰할 뿐 게임 상태를 변경하지 않습니다.
/// </summary>
public sealed class ReputationLogService
{
    private static readonly ReputationTransactionGrade[] LoggedGrades =
    {
        ReputationTransactionGrade.Discount,
        ReputationTransactionGrade.Regular,
        ReputationTransactionGrade.ModerateMarkup,
        ReputationTransactionGrade.ExtremeMarkup
    };

    // 거래 타입별 가격 규칙을 거래 등급 분류에 사용합니다.
    private readonly Dictionary<CustomerDispositionType, CustomerDispositionData> dispositionsByType;

    // 현재 세션의 거래 로그를 생성 순서대로 보관합니다.
    private readonly List<ReputationTransactionLogEntry> transactionEntries;

    // 현재 세션의 일일 정산 로그를 생성 순서대로 보관합니다.
    private readonly List<ReputationSettlementLogEntry> settlementEntries;

    private long nextTransactionSequence;
    private long nextSettlementSequence;

    /// <summary>현재 세션의 거래별 명성 로그입니다.</summary>
    public IReadOnlyList<ReputationTransactionLogEntry> TransactionEntries => this.transactionEntries;

    /// <summary>현재 세션의 일일 정산 명성 로그입니다.</summary>
    public IReadOnlyList<ReputationSettlementLogEntry> SettlementEntries => this.settlementEntries;

    /// <summary>새 거래 명성 로그가 추가된 뒤 발생합니다.</summary>
    public event Action<ReputationTransactionLogEntry> TransactionLogged;

    /// <summary>새 일일 정산 명성 로그가 추가된 뒤 발생합니다.</summary>
    public event Action<ReputationSettlementLogEntry> SettlementLogged;

    /// <summary>
    /// 검증된 손님 성향 테이블을 사용하는 명성 로그 서비스를 생성합니다.
    /// </summary>
    /// <param name="dispositionTable">거래 등급 분류에 사용할 손님 성향 테이블입니다.</param>
    /// <exception cref="ArgumentNullException">성향 테이블이 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">성향 타입이 누락되거나 중복된 경우 발생합니다.</exception>
    public ReputationLogService(CustomerDispositionDataTable dispositionTable)
    {
        if (dispositionTable == null)
        {
            throw new ArgumentNullException(nameof(dispositionTable));
        }

        this.dispositionsByType = new Dictionary<CustomerDispositionType, CustomerDispositionData>();
        foreach (CustomerDispositionData data in dispositionTable.Rows.Values)
        {
            if (data == null || !this.dispositionsByType.TryAdd(data.DispositionType, data))
            {
                throw new InvalidDataException("명성 로그에 사용할 손님 성향 타입이 누락되었거나 중복되었습니다.");
            }
        }

        this.transactionEntries = new List<ReputationTransactionLogEntry>();
        this.settlementEntries = new List<ReputationSettlementLogEntry>();
        this.nextTransactionSequence = 1;
        this.nextSettlementSequence = 1;
    }

    /// <summary>
    /// 저장된 명성 로그를 제거하고 순서를 처음부터 다시 시작합니다.
    /// </summary>
    public void Clear()
    {
        this.transactionEntries.Clear();
        this.settlementEntries.Clear();
        this.nextTransactionSequence = 1;
        this.nextSettlementSequence = 1;
    }

    /// <summary>
    /// 거래 결과를 등급으로 분류하고 Console과 구조화된 목록에 기록합니다.
    /// </summary>
    /// <param name="day">거래가 발생한 게임 날짜입니다.</param>
    /// <param name="transaction">거래 결과 snapshot입니다.</param>
    /// <returns>추가된 거래 로그입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">게임 날짜가 1 미만인 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">거래 성향 데이터가 없는 경우 발생합니다.</exception>
    public ReputationTransactionLogEntry RecordTransaction(int day, TransactionResult transaction)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        if (!this.dispositionsByType.TryGetValue(transaction.DispositionType, out CustomerDispositionData dispositionData))
        {
            throw new InvalidDataException($"명성 거래 로그 성향 데이터가 없습니다: {transaction.DispositionType}");
        }

        ReputationTransactionGrade grade = ReputationTransactionClassifier.Classify(transaction, dispositionData);
        ReputationTransactionLogEntry entry = new ReputationTransactionLogEntry(
            this.nextTransactionSequence,
            day,
            transaction,
            grade);
        this.transactionEntries.Add(entry);
        this.nextTransactionSequence = checked(this.nextTransactionSequence + 1);
        Debug.Log(this.formatTransactionEntry(entry));
        this.TransactionLogged?.Invoke(entry);
        return entry;
    }

    /// <summary>
    /// 하루 명성 계산 결과와 거래 비율을 Console과 구조화된 목록에 기록합니다.
    /// </summary>
    /// <param name="day">정산 대상 게임 날짜입니다.</param>
    /// <param name="dayStartReputation">하루 시작 명성입니다.</param>
    /// <param name="result">명성 계산 결과입니다.</param>
    /// <returns>추가된 정산 로그입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">게임 날짜 또는 시작 명성이 범위를 벗어난 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">명성 구간 회복 배율을 조회할 수 없는 경우 발생합니다.</exception>
    public ReputationSettlementLogEntry RecordDailySettlement(int day, int dayStartReputation,
        DailyReputationCalculationResult result, ReputationBalanceData reputationData)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        if (dayStartReputation < -100 || dayStartReputation > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(dayStartReputation), dayStartReputation,
                "명성은 -100~100 범위여야 합니다.");
        }

        if (reputationData == null)
        {
            throw new InvalidDataException($"명성 {dayStartReputation}의 밸런스 데이터가 없습니다.");
        }

        int[] counts = new int[LoggedGrades.Length];
        int actualTransactionCount = 0;
        foreach (ReputationTransactionLogEntry entry in this.transactionEntries)
        {
            if (entry.Day != day)
            {
                continue;
            }

            int gradeIndex = (int)entry.Grade;
            if (gradeIndex < 0 || gradeIndex >= counts.Length)
            {
                throw new InvalidDataException($"정의되지 않은 거래 등급 로그입니다: {entry.Grade}");
            }

            counts[gradeIndex]++;
            actualTransactionCount++;
        }

        if (actualTransactionCount != result.ActualTransactionCount)
        {
            throw new InvalidDataException(
                $"명성 로그 거래 수({actualTransactionCount})와 계산 결과 거래 수({result.ActualTransactionCount})가 다릅니다.");
        }

        List<ReputationGradeSummary> summaries = new List<ReputationGradeSummary>(LoggedGrades.Length);
        for (int i = 0; i < LoggedGrades.Length; i++)
        {
            decimal ratioPercent = actualTransactionCount == 0
                ? 0m
                : (decimal)counts[i] * 100m / actualTransactionCount;
            summaries.Add(new ReputationGradeSummary(LoggedGrades[i], counts[i], ratioPercent));
        }

        int virtualRegularTransactionCount = result.WasSmallSampleAdjusted
            ? Math.Max(0, 5 - result.ActualTransactionCount)
            : 0;
        ReputationSettlementLogEntry settlementEntry = new ReputationSettlementLogEntry(
            this.nextSettlementSequence,
            day,
            dayStartReputation,
            virtualRegularTransactionCount,
            summaries,
            result,
            reputationData.RecoveryRate);
        this.settlementEntries.Add(settlementEntry);
        this.nextSettlementSequence = checked(this.nextSettlementSequence + 1);
        Debug.Log(this.formatSettlementEntry(settlementEntry));
        this.SettlementLogged?.Invoke(settlementEntry);
        return settlementEntry;
    }

    private string formatTransactionEntry(ReputationTransactionLogEntry entry)
    {
        string offered = entry.OfferedTotal.HasValue ? $"{entry.OfferedTotal.Value:N0}G" : "N/A";
        string reference = entry.ReferenceTotal.HasValue ? $"{entry.ReferenceTotal.Value:N0}G" : "N/A";
        string ratio = entry.PriceRatioPercent.HasValue ? $"{entry.PriceRatioPercent.Value:0.##}%" : "N/A";
        return $"[ReputationLog] Day={entry.Day} Transaction #{entry.Sequence} | "
            + $"Type={entry.DispositionType} Attributes={entry.CustomerAttributes} | "
            + $"Outcome={entry.Outcome} Grade={entry.Grade} | "
            + $"Offered={offered} Reference={reference} Ratio={ratio}";
    }

    private string formatSettlementEntry(ReputationSettlementLogEntry entry)
    {
        List<string> gradeRatios = new List<string>(entry.GradeSummaries.Count);
        foreach (ReputationGradeSummary summary in entry.GradeSummaries)
        {
            gradeRatios.Add($"{summary.Grade}={summary.Count}({summary.RatioPercent:0.##}%)");
        }

        string sampleAdjustment = entry.WasSmallSampleAdjusted
            ? $" VirtualRegular={entry.VirtualRegularTransactionCount}"
            : string.Empty;
        return $"[ReputationLog] Day={entry.Day} Settlement | "
            + $"StartReputation={entry.DayStartReputation} Actual={entry.ActualTransactionCount}"
            + sampleAdjustment + " | "
            + $"GradeRatio=[{string.Join(", ", gradeRatios)}] | "
            + $"Score={entry.WeightedScoreSum}/{entry.TotalWeight}="
            + $"{entry.RawSettlementScore} -> {entry.SettlementScore} | "
            + $"BaseDelta={formatSigned(entry.BaseDelta)} RecoveryRate={entry.RecoveryRate}/1000 "
            + $"FinalDelta={formatSigned(entry.FinalDelta)}";
    }

    private static string formatSigned(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }
}
