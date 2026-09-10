using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 현재 게임 세션에서 사용하는 검증된 경제 밸런스 설정을 보관합니다.
/// </summary>
public sealed class EconomySettings
{
    // 외부에서 상납금 목록을 변경하지 못하도록 읽기 전용 컬렉션으로 보관합니다.
    private readonly ReadOnlyCollection<long> maintenanceAmounts;

    /// <summary>
    /// 새 게임을 시작할 때 적용하는 보유금입니다.
    /// </summary>
    public long InitialBalance { get; }

    /// <summary>
    /// 1회차부터 순서대로 정렬된 상납금 목록입니다.
    /// </summary>
    public IReadOnlyList<long> MaintenanceAmounts => this.maintenanceAmounts;

    /// <summary>
    /// 경제 기본 설정과 회차별 상납금으로 현재 세션의 경제 설정을 생성합니다.
    /// </summary>
    /// <param name="balanceData">CSV에서 읽고 검증한 경제 기본 설정입니다.</param>
    /// <param name="maintenanceAmounts">1회차부터 순서대로 정렬된 상납금 목록입니다.</param>
    /// <exception cref="ArgumentNullException">설정 데이터나 상납금 목록이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">상납금 목록이 비어 있거나 0 이하의 금액을 포함한 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">경제 기본 설정값이 허용 범위를 벗어난 경우 발생합니다.</exception>
    public EconomySettings(
        EconomyBalanceData balanceData,
        IReadOnlyList<long> maintenanceAmounts)
    {
        if (balanceData == null)
        {
            throw new ArgumentNullException(nameof(balanceData));
        }

        if (maintenanceAmounts == null)
        {
            throw new ArgumentNullException(nameof(maintenanceAmounts));
        }

        if (balanceData.InitialBalance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(balanceData),
                balanceData.InitialBalance,
                "초기 보유금은 음수일 수 없습니다.");
        }

        if (maintenanceAmounts.Count == 0)
        {
            throw new ArgumentException("상납금 목록은 한 회차 이상이어야 합니다.", nameof(maintenanceAmounts));
        }

        long[] copiedAmounts = new long[maintenanceAmounts.Count];
        for (int index = 0; index < maintenanceAmounts.Count; index++)
        {
            long amount = maintenanceAmounts[index];
            if (amount <= 0)
            {
                throw new ArgumentException(
                    $"{index + 1}회차 상납금은 0보다 커야 합니다.",
                    nameof(maintenanceAmounts));
            }

            copiedAmounts[index] = amount;
        }

        // 서로 다른 CSV에서 가져온 값을 하나의 변경 불가능한 세션 설정으로 확정합니다.
        this.InitialBalance = balanceData.InitialBalance;
        this.maintenanceAmounts = Array.AsReadOnly(copiedAmounts);
    }
}
