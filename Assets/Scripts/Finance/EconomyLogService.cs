using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 재정 시스템의 잔고 변화 이벤트를 세션 로그로 수집합니다.
/// 이 서비스는 개발용 분석과 표시만 담당하며 게임 계산에는 관여하지 않습니다.
/// </summary>
public sealed class EconomyLogService : IDisposable
{
    // 모든 경제 수치 변화가 통과하는 잔고 변경 이벤트의 원본입니다.
    private readonly FinanceService financeService;

    // 현재 세션에서 발생한 잔고 변경을 생성 순서대로 보관합니다.
    private readonly List<EconomyLogEntry> entries;

    // 다음 로그에 부여할 세션 내 순서 번호입니다.
    private long nextSequence;

    // 재정 이벤트 구독이 해제되었는지 나타냅니다.
    private bool isDisposed;

    /// <summary>
    /// 현재 세션에서 수집된 잔고 변경 기록을 제공합니다.
    /// </summary>
    public IReadOnlyList<EconomyLogEntry> Entries => this.entries;

    /// <summary>
    /// 새로운 잔고 변경 기록이 추가된 뒤 발생합니다.
    /// </summary>
    public event Action<EconomyLogEntry> LogAdded;

    /// <summary>
    /// 재정 시스템의 잔고 변경 이벤트를 수집하는 로그 서비스를 생성합니다.
    /// </summary>
    /// <param name="financeService">잔고 변경 이벤트를 발행하는 재정 시스템입니다.</param>
    /// <exception cref="ArgumentNullException">재정 시스템이 null인 경우 발생합니다.</exception>
    public EconomyLogService(FinanceService financeService)
    {
        this.financeService = financeService ?? throw new ArgumentNullException(nameof(financeService));
        this.entries = new List<EconomyLogEntry>();
        this.nextSequence = 1;

        // 판매·상납금·추후 추가될 모든 재화 변화가 동일한 입력 경로로 수집됩니다.
        this.financeService.BalanceChanged += this.handleBalanceChanged;
    }

    /// <summary>
    /// 저장된 로그를 제거하고 다음 기록 순서를 1부터 다시 시작합니다.
    /// 재정 시스템의 실제 잔고에는 영향을 주지 않습니다.
    /// </summary>
    /// <exception cref="ObjectDisposedException">서비스가 이미 해제된 경우 발생합니다.</exception>
    public void Clear()
    {
        this.throwIfDisposed();

        this.entries.Clear();
        this.nextSequence = 1;
    }

    /// <summary>
    /// 재정 시스템 이벤트 구독을 해제합니다.
    /// 여러 번 호출해도 한 번만 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.financeService.BalanceChanged -= this.handleBalanceChanged;
        this.LogAdded = null;
        this.isDisposed = true;
    }

    /// <summary>
    /// 적용이 완료된 잔고 변경 결과를 세션 로그로 기록합니다.
    /// </summary>
    /// <param name="result">재정 시스템에서 발행한 잔고 변경 결과입니다.</param>
    private void handleBalanceChanged(FinanceChangeResult result)
    {
        this.throwIfDisposed();

        EconomyLogEntry entry = new EconomyLogEntry(this.nextSequence, result);

        // 목록에 먼저 추가해 이벤트 소비자가 즉시 최신 기록을 조회할 수 있게 합니다.
        this.entries.Add(entry);
        this.nextSequence = checked(this.nextSequence + 1);
        // 임시 개발 단계에서는 UI가 없어도 경제 변화가 Console에 남도록 출력합니다.
        Debug.Log(this.formatLogEntry(entry));
        this.LogAdded?.Invoke(entry);
    }

    /// <summary>
    /// 구조화된 경제 로그를 Unity Console에서 읽을 수 있는 한 줄로 변환합니다.
    /// </summary>
    /// <param name="entry">변환할 경제 로그입니다.</param>
    /// <returns>순서, 잔고 변화와 사유가 포함된 로그 문자열입니다.</returns>
    private string formatLogEntry(EconomyLogEntry entry)
    {
        return $"[EconomyLog] #{entry.Sequence} Balance | "
            + $"{entry.PreviousBalance:N0} {this.formatDelta(entry.BalanceDelta)} = "
            + $"{entry.CurrentBalance:N0} | Reason={entry.Reason}";
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
    /// 해제된 로그 서비스에 대한 상태 변경 호출을 차단합니다.
    /// </summary>
    /// <exception cref="ObjectDisposedException">서비스가 이미 해제된 경우 발생합니다.</exception>
    private void throwIfDisposed()
    {
        if (this.isDisposed)
        {
            throw new ObjectDisposedException(nameof(EconomyLogService));
        }
    }
}
