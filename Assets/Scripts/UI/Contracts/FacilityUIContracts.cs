using System;
using System.Collections.Generic;

/// <summary>구매 요청 결과와 별개인 현재 설비 표시 상태.</summary>
public enum FacilityDisplayState
{
    StageLocked,
    Purchasable,
    InsufficientFunds,
    ActivationPending,
    Active,
    OwnedStageUpgrade,
    PrerequisiteLocked,
    OwnedProgression,
    FacilityDisplayState_End
}

/// <summary>한 설비의 표시용 불변 스냅샷. 비용과 날짜를 구매 입력으로 전달하지 않는다.</summary>
public readonly struct FacilityItemViewData
{
    /// <summary>구매 요청에 사용하는 유일한 식별자.</summary>
    public uint FacilityIdx { get; }
    /// <summary>TextData에서 조회한 이름.</summary>
    public string DisplayName { get; }
    /// <summary>CSV의 양수 구매 가격.</summary>
    public long PurchasePrice { get; }
    /// <summary>활성 상품 FK에서 조회한 해금 상품 이름 목록.</summary>
    public string UnlockProducts { get; }
    /// <summary>현재 보유·잔액·활성일로 계산한 화면 상태.</summary>
    public FacilityDisplayState State { get; }
    /// <summary>상품 해금·편의성·단계 상승 중 업그레이드 종류.</summary>
    public FacilityUpgradeKind UpgradeKind { get; }
    /// <summary>구매에 필요한 가게 단계.</summary>
    public uint RequiredStoreStage { get; }
    /// <summary>편의성 업그레이드 효과.</summary>
    public ConvenienceEffectType EffectType { get; }
    /// <summary>단계 상승 목표. 단계 상승이 아니면0.</summary>
    public uint TargetStoreStage { get; }
    /// <summary>사용 가능 날짜의1기반 표시값. uint 경과일에1을 더해도 overflow하지 않는다.</summary>
    public ulong ActivationDisplayDay { get; }
    /// <summary>진행 항목이 요구하는 현재 단계 일반 업그레이드의 보유 수.</summary>
    public uint CompletedRegularCount { get; }
    /// <summary>진행 항목이 요구하는 현재 단계 일반 업그레이드의 전체 수.</summary>
    public uint RequiredRegularCount { get; }

    /// <summary>변환기가 확정한 표시값을 복사한다.</summary>
    /// <param name="facilityIdx">설비 PK.</param><param name="displayName">이름.</param>
    /// <param name="purchasePrice">구매 가격.</param><param name="unlockProducts">해금 상품 이름.</param>
    /// <param name="state">표시 상태.</param><param name="activationDisplayDay">1기반 사용 가능일.</param>
    public FacilityItemViewData(uint facilityIdx, string displayName, long purchasePrice, string unlockProducts,
        FacilityDisplayState state, ulong activationDisplayDay)
        : this(facilityIdx, displayName, purchasePrice, unlockProducts, FacilityUpgradeKind.ProductUnlock, 1,
            ConvenienceEffectType.None, 0, state, activationDisplayDay, 0, 0)
    {
    }

    /// <summary>업그레이드 종류와 표시 상태를 포함한 행 snapshot을 복사한다.</summary>
    /// <param name="facilityIdx">설비 PK.</param><param name="displayName">이름.</param>
    /// <param name="purchasePrice">구매 가격.</param><param name="unlockProducts">해금 상품 이름.</param>
    /// <param name="upgradeKind">업그레이드 종류.</param><param name="requiredStoreStage">요구 단계.</param>
    /// <param name="effectType">편의성 효과.</param><param name="targetStoreStage">목표 단계.</param>
    /// <param name="state">표시 상태.</param><param name="activationDisplayDay">1기반 사용 가능일.</param>
    /// <param name="completedRegularCount">완료한 현재 단계 일반 업그레이드 수.</param>
    /// <param name="requiredRegularCount">현재 단계 일반 업그레이드 전체 수.</param>
    public FacilityItemViewData(uint facilityIdx, string displayName, long purchasePrice, string unlockProducts,
        FacilityUpgradeKind upgradeKind, uint requiredStoreStage, ConvenienceEffectType effectType,
        uint targetStoreStage, FacilityDisplayState state, ulong activationDisplayDay,
        uint completedRegularCount = 0, uint requiredRegularCount = 0)
    {
        FacilityIdx = facilityIdx;
        DisplayName = displayName;
        PurchasePrice = purchasePrice;
        UnlockProducts = unlockProducts;
        UpgradeKind = upgradeKind;
        RequiredStoreStage = requiredStoreStage;
        EffectType = effectType;
        TargetStoreStage = targetStoreStage;
        State = state;
        ActivationDisplayDay = activationDisplayDay;
        CompletedRegularCount = completedRegularCount;
        RequiredRegularCount = requiredRegularCount;
    }
}

/// <summary>같은 조회 시점의 잔액과 설비 목록을 보존한다.</summary>
public sealed class FacilityShopViewData
{
    /// <summary>조회 시점의 현재 가게 단계.</summary>
    public uint CurrentStoreStage { get; }
    /// <summary>조회 시점의 세션 잔액.</summary>
    public long CurrentBalance { get; }
    /// <summary>현재 단계의 일반 업그레이드만 담은 PK순 목록.</summary>
    public IReadOnlyList<FacilityItemViewData> RegularItems { get; }
    /// <summary>현재 단계의 하단 진행 항목. 항상 하나를 담아야 한다.</summary>
    public FacilityItemViewData? ProgressionItem { get; }
    /// <summary>현재 단계 일반 업그레이드 중 보유한 수.</summary>
    public uint CompletedRegularCount { get; }
    /// <summary>현재 단계 일반 업그레이드 전체 수.</summary>
    public uint RequiredRegularCount { get; }
    /// <summary>기존 소비자 호환용 일반 목록과 진행 항목의 결합 view입니다.</summary>
    public IReadOnlyList<FacilityItemViewData> Items
    {
        get
        {
            var items = new List<FacilityItemViewData>(RegularItems.Count + (ProgressionItem.HasValue ? 1 : 0));
            items.AddRange(RegularItems);
            if (ProgressionItem.HasValue) items.Add(ProgressionItem.Value);
            return items.AsReadOnly();
        }
    }

    /// <summary>조회값과 목록을 복사한다.</summary>
    /// <param name="currentBalance">현재 잔액.</param><param name="items">표시 목록.</param>
    /// <exception cref="ArgumentNullException">목록이 없음.</exception>
    public FacilityShopViewData(long currentBalance, IReadOnlyList<FacilityItemViewData> items)
        : this(1, currentBalance, items, null, 0, 0)
    {
    }

    /// <summary>단계·잔액·목록을 같은 조회 시점의 불변 값으로 보관한다.</summary>
    /// <param name="currentStoreStage">현재 가게 단계.</param>
    /// <param name="currentBalance">현재 잔액.</param>
    /// <param name="items">기존 호출자 호환용 일반 목록.</param>
    /// <exception cref="ArgumentOutOfRangeException">가게 단계가 1~3이 아님.</exception>
    public FacilityShopViewData(uint currentStoreStage, long currentBalance, IReadOnlyList<FacilityItemViewData> items)
        : this(currentStoreStage, currentBalance, items, null, 0, 0)
    {
    }

    /// <summary>현재 단계의 일반 목록과 진행 항목을 같은 조회 시점의 불변 값으로 보관한다.</summary>
    /// <param name="currentStoreStage">현재 가게 단계.</param>
    /// <param name="currentBalance">현재 잔액.</param>
    /// <param name="regularItems">현재 단계 일반 업그레이드 목록.</param>
    /// <param name="progressionItem">단계 확장 또는 시민권 항목.</param>
    /// <param name="completedRegularCount">보유한 일반 업그레이드 수.</param>
    /// <param name="requiredRegularCount">필요한 일반 업그레이드 전체 수.</param>
    /// <exception cref="ArgumentOutOfRangeException">가게 단계가 1~3이 아니거나 완료 수가 전체 수보다 큼.</exception>
    /// <exception cref="ArgumentNullException">일반 목록이 없음.</exception>
    public FacilityShopViewData(uint currentStoreStage, long currentBalance,
        IReadOnlyList<FacilityItemViewData> regularItems, FacilityItemViewData? progressionItem,
        uint completedRegularCount, uint requiredRegularCount)
    {
        if (currentStoreStage < 1 || currentStoreStage > 3)
            throw new ArgumentOutOfRangeException(nameof(currentStoreStage));
        if (completedRegularCount > requiredRegularCount)
            throw new ArgumentOutOfRangeException(nameof(completedRegularCount));
        CurrentStoreStage = currentStoreStage;
        CurrentBalance = currentBalance;
        RegularItems = new List<FacilityItemViewData>(regularItems ?? throw new ArgumentNullException(nameof(regularItems))).AsReadOnly();
        ProgressionItem = progressionItem;
        CompletedRegularCount = completedRegularCount;
        RequiredRegularCount = requiredRegularCount;
    }
}
