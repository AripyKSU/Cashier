using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>단일 세션의 독립 설비 구매와 다음 영업일 활성일을 소유한다. Unity 메인 스레드의 동기 호출만 지원한다.</summary>
public sealed class FacilityService
{
    private const uint InitialStoreStage = 1;
    private readonly FinanceService finance;
    private readonly Func<uint> getElapsedDays;
    // 검증된 가격만 복사해 외부 DTO 변경이 구매 금액을 바꾸지 못하게 한다.
    private readonly Dictionary<uint, long> purchasePrices = new Dictionary<uint, long>();
    private readonly Dictionary<uint, FacilityUpgradeKind> upgradeKinds = new Dictionary<uint, FacilityUpgradeKind>();
    private readonly Dictionary<uint, uint> requiredStoreStages = new Dictionary<uint, uint>();
    private readonly Dictionary<uint, ConvenienceEffectType> effectTypes = new Dictionary<uint, ConvenienceEffectType>();
    private readonly Dictionary<uint, uint> targetStoreStages = new Dictionary<uint, uint>();
    // 보유 여부와 활성일의 단일 권위. 별도 pending/active 목록을 만들지 않는다.
    private readonly Dictionary<uint, uint> activationDays = new Dictionary<uint, uint>();
    private bool isPurchasing;

    /// <summary>보유 설비 PK별 활성 경과일. 외부에서는 변경할 수 없다.</summary>
    public IReadOnlyDictionary<uint, uint> ActivationDays { get; }

    /// <summary>현재 세션의 가게 단계입니다. 새 세션은 1단계로 시작합니다.</summary>
    public uint CurrentStoreStage { get; private set; } = InitialStoreStage;

    /// <summary>결제와 보유 반영이 끝난 뒤 성공한 업그레이드마다 한 번 발생합니다.</summary>
    public event Action<FacilityPurchaseEvent> PurchaseCompleted;

    /// <summary>검증된 설비 가격과 소유 세션의 날짜 조회를 연결한다.</summary>
    /// <param name="finance">소유 세션의 재정 서비스.</param>
    /// <param name="facilities">FK까지 검증한 설비 원본.</param>
    /// <param name="getElapsedDays">현재 경과일 조회. 구매 요청에는 날짜를 받지 않는다.</param>
    /// <exception cref="ArgumentException">필수 의존성 또는 설비 행이 잘못됨.</exception>
    public FacilityService(FinanceService finance, IReadOnlyDictionary<uint, FacilityData> facilities, Func<uint> getElapsedDays)
    {
        this.finance = finance ?? throw new ArgumentNullException(nameof(finance));
        this.getElapsedDays = getElapsedDays ?? throw new ArgumentNullException(nameof(getElapsedDays));
        if (facilities == null) throw new ArgumentNullException(nameof(facilities));
        foreach (var pair in facilities)
        {
            if (pair.Value == null || pair.Key != pair.Value.Idx)
                throw new ArgumentException("설비 사전 키와 PK가 다릅니다.", nameof(facilities));
            pair.Value.Validate();
            purchasePrices.Add(pair.Key, pair.Value.PurchasePrice);
            upgradeKinds.Add(pair.Key, pair.Value.UpgradeKind);
            requiredStoreStages.Add(pair.Key, pair.Value.RequiredStoreStage);
            effectTypes.Add(pair.Key, pair.Value.EffectType);
            targetStoreStages.Add(pair.Key, pair.Value.TargetStoreStage);
        }
        ActivationDays = new ReadOnlyDictionary<uint, uint>(activationDays);
    }

    /// <summary>현재 날짜에 보유 설비가 활성인지 조회한다. 미보유는false다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <returns>보유하고 활성일에 도달했는지 여부.</returns>
    public bool IsActive(uint facilityIdx) => this.IsUpgradeActive(facilityIdx);

    /// <summary>설비 업그레이드의 보유 여부를 조회합니다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <returns>현재 세션이 해당 업그레이드를 구매했는지 여부.</returns>
    public bool IsOwned(uint facilityIdx) => activationDays.ContainsKey(facilityIdx);

    /// <summary>업그레이드가 현재 날짜에 실제 활성인지 조회합니다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <returns>보유하며 활성일에 도달했는지 여부.</returns>
    public bool IsUpgradeActive(uint facilityIdx)
    {
        if (!activationDays.TryGetValue(facilityIdx, out uint activationDay)) return false;
        return upgradeKinds.TryGetValue(facilityIdx, out FacilityUpgradeKind kind)
            && (kind == FacilityUpgradeKind.StoreStage || activationDay <= getElapsedDays());
    }

    /// <summary>설비의 활성 경과일을 조회합니다.</summary>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <param name="activationDay">보유 설비의 활성 경과일.</param>
    /// <returns>보유 설비이면 true.</returns>
    public bool TryGetActivationDay(uint facilityIdx, out uint activationDay) =>
        activationDays.TryGetValue(facilityIdx, out activationDay);

    /// <summary>편의성 효과의 활성 여부를 설비 종류가 아닌 고정 enum으로 조회합니다.</summary>
    /// <param name="effectType">조회할 편의성 효과.</param>
    /// <returns>해당 효과 설비를 구매했고 활성일에 도달했는지 여부.</returns>
    /// <exception cref="ArgumentException">None 또는 종료 표식이 전달된 경우.</exception>
    public bool IsConvenienceEffectActive(ConvenienceEffectType effectType)
    {
        if (effectType == ConvenienceEffectType.None || effectType == ConvenienceEffectType.ConvenienceEffectType_End)
            throw new ArgumentException("유효한 편의성 효과가 필요합니다.", nameof(effectType));
        foreach (var pair in effectTypes)
        {
            if (pair.Value == effectType && upgradeKinds[pair.Key] == FacilityUpgradeKind.Convenience)
                return this.IsUpgradeActive(pair.Key);
        }

        return false;
    }

    /// <summary>한 번 지불하고 다음 영업일 활성일을 등록한다. 고단계도 선행 구매를 요구하지 않는다.</summary>
    /// <param name="facilityIdx">등록된 설비 PK.</param>
    /// <param name="result">정상 처리 결과. 알림 예외 시 성공 상태는 보존하지만 예외를 전파한다.</param>
    /// <returns>이번 요청이 구매를 완료했으면true.</returns>
    /// <exception cref="ArgumentException">0 또는 미등록 설비.</exception>
    /// <exception cref="InvalidOperationException">구매 중 재진입.</exception>
    /// <exception cref="OverflowException">다음 활성일이uint 범위를 초과함.</exception>
    /// <exception cref="Exception">재정 알림 예외. 이미 완료한 차감·보유를 취소하지 않는다.</exception>
    public bool TryPurchase(uint facilityIdx, out FacilityPurchaseResult result)
    {
        result = default;
        if (facilityIdx == 0 || !purchasePrices.TryGetValue(facilityIdx, out long price))
            throw new ArgumentException($"등록되지 않은 설비 PK={facilityIdx}", nameof(facilityIdx));
        if (isPurchasing) throw new InvalidOperationException("설비 구매 처리 중입니다.");
        if (activationDays.TryGetValue(facilityIdx, out uint existingDay))
        {
            result = new FacilityPurchaseResult(FacilityPurchaseStatus.AlreadyOwned, facilityIdx, 0, existingDay);
            return false;
        }
        uint purchaseElapsedDay = getElapsedDays();
        FacilityUpgradeKind upgradeKind = upgradeKinds[facilityIdx];
        uint requiredStoreStage = requiredStoreStages[facilityIdx];
        uint targetStoreStage = targetStoreStages[facilityIdx];
        if (CurrentStoreStage < requiredStoreStage ||
            (upgradeKind == FacilityUpgradeKind.StoreStage && targetStoreStage != CurrentStoreStage + 1))
        {
            result = new FacilityPurchaseResult(FacilityPurchaseStatus.StageLocked, facilityIdx, 0, null);
            return false;
        }

        uint activationDay = upgradeKind == FacilityUpgradeKind.StoreStage
            ? purchaseElapsedDay
            : checked(purchaseElapsedDay + 1);
        if (!finance.CanAfford(price))
        {
            result = new FacilityPurchaseResult(FacilityPurchaseStatus.InsufficientFunds, facilityIdx, 0, null);
            return false;
        }

        isPurchasing = true;
        FinanceChangeResult payment = default;
        long previousBalance = finance.CurrentBalance;
        uint previousStoreStage = CurrentStoreStage;
        try
        {
            // 외부 알림 전에 차감과 보유가 함께 관찰되도록 먼저 예약한다.
            activationDays.Add(facilityIdx, activationDay);
            if (!finance.TrySpend(price, FinanceChangeReason.FacilityPurchase, out payment))
            {
                activationDays.Remove(facilityIdx);
                result = new FacilityPurchaseResult(FacilityPurchaseStatus.InsufficientFunds, facilityIdx, 0, null);
                return false;
            }
            if (upgradeKind == FacilityUpgradeKind.StoreStage)
            {
                CurrentStoreStage = targetStoreStage;
            }
            result = new FacilityPurchaseResult(FacilityPurchaseStatus.Purchased, facilityIdx, price, activationDay);
            this.PurchaseCompleted?.Invoke(new FacilityPurchaseEvent(
                facilityIdx,
                upgradeKind,
                purchaseElapsedDay,
                activationDay,
                previousStoreStage,
                CurrentStoreStage));
            return true;
        }
        catch
        {
            // TrySpend는 결과·잔액을 확정한 뒤 알림을 호출한다. 후속 이벤트의 잔액 변화와 비교하지 않는다.
            bool wasPaid = payment.Reason == FinanceChangeReason.FacilityPurchase &&
                payment.BalanceDelta == -price && payment.PreviousBalance == previousBalance &&
                payment.CurrentBalance == previousBalance - price;
            if (!wasPaid)
            {
                activationDays.Remove(facilityIdx);
                CurrentStoreStage = previousStoreStage;
            }
            throw;
        }
        finally { isPurchasing = false; }
    }
}
