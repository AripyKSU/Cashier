using System;

/// <summary>결제가 확정된 설비 업그레이드 한 건의 후속 연출용 snapshot입니다.</summary>
public readonly struct FacilityPurchaseEvent
{
    /// <summary>구매한 설비 업그레이드 PK입니다.</summary>
    public uint FacilityIdx { get; }
    /// <summary>구매한 업그레이드 종류입니다.</summary>
    public FacilityUpgradeKind UpgradeKind { get; }
    /// <summary>구매가 확정된 경과일입니다.</summary>
    public uint PurchaseElapsedDay { get; }
    /// <summary>효과가 활성화되는 경과일입니다. 단계 상승은 구매일에 즉시 반영됩니다.</summary>
    public uint ActivationElapsedDay { get; }
    /// <summary>구매 전 가게 단계입니다.</summary>
    public uint PreviousStoreStage { get; }
    /// <summary>구매 후 가게 단계입니다.</summary>
    public uint CurrentStoreStage { get; }

    /// <summary>확정된 구매 정보를 불변 값으로 보관합니다.</summary>
    /// <param name="facilityIdx">구매한 설비 PK.</param>
    /// <param name="upgradeKind">업그레이드 종류.</param>
    /// <param name="purchaseElapsedDay">구매 경과일.</param>
    /// <param name="activationElapsedDay">활성 경과일.</param>
    /// <param name="previousStoreStage">구매 전 단계.</param>
    /// <param name="currentStoreStage">구매 후 단계.</param>
    public FacilityPurchaseEvent(uint facilityIdx, FacilityUpgradeKind upgradeKind, uint purchaseElapsedDay,
        uint activationElapsedDay, uint previousStoreStage, uint currentStoreStage)
    {
        FacilityIdx = facilityIdx;
        UpgradeKind = upgradeKind;
        PurchaseElapsedDay = purchaseElapsedDay;
        ActivationElapsedDay = activationElapsedDay;
        PreviousStoreStage = previousStoreStage;
        CurrentStoreStage = currentStoreStage;
    }
}
