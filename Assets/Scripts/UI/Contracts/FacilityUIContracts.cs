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

    /// <summary>변환기가 확정한 표시값을 복사한다.</summary>
    /// <param name="facilityIdx">설비 PK.</param><param name="displayName">이름.</param>
    /// <param name="purchasePrice">구매 가격.</param><param name="unlockProducts">해금 상품 이름.</param>
    /// <param name="state">표시 상태.</param><param name="activationDisplayDay">1기반 사용 가능일.</param>
    public FacilityItemViewData(uint facilityIdx, string displayName, long purchasePrice, string unlockProducts,
        FacilityDisplayState state, ulong activationDisplayDay)
        : this(facilityIdx, displayName, purchasePrice, unlockProducts, FacilityUpgradeKind.ProductUnlock, 1,
            ConvenienceEffectType.None, 0, state, activationDisplayDay)
    {
    }

    /// <summary>업그레이드 종류와 표시 상태를 포함한 행 snapshot을 복사한다.</summary>
    /// <param name="facilityIdx">설비 PK.</param><param name="displayName">이름.</param>
    /// <param name="purchasePrice">구매 가격.</param><param name="unlockProducts">해금 상품 이름.</param>
    /// <param name="upgradeKind">업그레이드 종류.</param><param name="requiredStoreStage">요구 단계.</param>
    /// <param name="effectType">편의성 효과.</param><param name="targetStoreStage">목표 단계.</param>
    /// <param name="state">표시 상태.</param><param name="activationDisplayDay">1기반 사용 가능일.</param>
    public FacilityItemViewData(uint facilityIdx, string displayName, long purchasePrice, string unlockProducts,
        FacilityUpgradeKind upgradeKind, uint requiredStoreStage, ConvenienceEffectType effectType,
        uint targetStoreStage, FacilityDisplayState state, ulong activationDisplayDay)
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
    }
}

/// <summary>같은 조회 시점의 잔액과 설비 목록을 보존한다.</summary>
public sealed class FacilityShopViewData
{
    /// <summary>조회 시점의 현재 가게 단계.</summary>
    public uint CurrentStoreStage { get; }
    /// <summary>조회 시점의 세션 잔액.</summary>
    public long CurrentBalance { get; }
    /// <summary>PK순 표시 목록. 외부 컬렉션 변경에 영향받지 않는다.</summary>
    public IReadOnlyList<FacilityItemViewData> Items { get; }

    /// <summary>조회값과 목록을 복사한다.</summary>
    /// <param name="currentBalance">현재 잔액.</param><param name="items">표시 목록.</param>
    /// <exception cref="ArgumentNullException">목록이 없음.</exception>
    public FacilityShopViewData(long currentBalance, IReadOnlyList<FacilityItemViewData> items)
        : this(1, currentBalance, items)
    {
    }

    /// <summary>단계·잔액·목록을 같은 조회 시점의 불변 값으로 보관한다.</summary>
    /// <param name="currentStoreStage">현재 가게 단계.</param>
    /// <param name="currentBalance">현재 잔액.</param>
    /// <param name="items">표시 목록.</param>
    /// <exception cref="ArgumentOutOfRangeException">가게 단계가 1~3이 아님.</exception>
    public FacilityShopViewData(uint currentStoreStage, long currentBalance, IReadOnlyList<FacilityItemViewData> items)
    {
        if (currentStoreStage < 1 || currentStoreStage > 3)
            throw new ArgumentOutOfRangeException(nameof(currentStoreStage));
        CurrentStoreStage = currentStoreStage;
        CurrentBalance = currentBalance;
        Items = new List<FacilityItemViewData>(items ?? throw new ArgumentNullException(nameof(items))).AsReadOnly();
    }
}
