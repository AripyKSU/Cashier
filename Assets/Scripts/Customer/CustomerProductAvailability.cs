using System;
using System.Collections.Generic;

/// <summary>
/// 손님 생성과 상품 표시가 공유하는 날짜별 판매 가능 상품 규칙을 제공합니다.
/// </summary>
public static class CustomerProductAvailability
{
    /// <summary>상품의 기존 설비 참조에서 일일 등장 가중치 단계를 계산합니다.</summary>
    /// <param name="product">검증할 상품 데이터입니다.</param>
    /// <param name="facilities">상품 해금 설비를 조회할 검증된 설비 사전입니다.</param>
    /// <returns>기본 상품 또는 설비 요구 가게 단계와 같은 1~3단계입니다.</returns>
    /// <exception cref="ArgumentNullException">상품 또는 설비 사전이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">상품 PK·설비 FK 또는 상품 해금 설비 계약이 유효하지 않은 경우 발생합니다.</exception>
    public static ProductUnlockStage GetUnlockStage(
        ProductData product,
        IReadOnlyDictionary<uint, FacilityData> facilities)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        if (facilities == null) throw new ArgumentNullException(nameof(facilities));
        product.Validate();

        if (!product.RequiredFacilityIdx.HasValue)
        {
            return ProductUnlockStage.Base;
        }

        uint facilityIdx = product.RequiredFacilityIdx.Value;
        if (!facilities.TryGetValue(facilityIdx, out FacilityData facility) || facility == null || facility.Idx != facilityIdx)
        {
            throw new ArgumentException(
                $"상품 PK={product.Idx}: required_facility_idx={facilityIdx} FK가 유효하지 않습니다.",
                nameof(facilities));
        }

        facility.Validate();
        if (facility.UpgradeKind != FacilityUpgradeKind.ProductUnlock)
        {
            throw new ArgumentException(
                $"상품 PK={product.Idx}: 설비 PK={facilityIdx}는 상품 해금 설비가 아닙니다.",
                nameof(facilities));
        }

        return facility.RequiredStoreStage switch
        {
            1 => ProductUnlockStage.Stage1,
            2 => ProductUnlockStage.Stage2,
            3 => ProductUnlockStage.Stage3,
            _ => throw new ArgumentException(
                $"상품 PK={product.Idx}: 설비 PK={facilityIdx}의 요구 가게 단계가 1~3 범위가 아닙니다.",
                nameof(facilities))
        };
    }

    /// <summary>
    /// 지정된 경과 일수에 상품을 판매할 수 있는지 확인합니다.
    /// </summary>
    /// <param name="product">확인할 상품 데이터입니다.</param>
    /// <param name="elapsedDays">게임 시작일부터 경과한 0 이상의 일수입니다.</param>
    /// <returns>활성 상품이며 등장일이 지난 경우 true입니다.</returns>
    /// <param name="isFacilityActive">세션의 설비 활성 조회. 미연결은 설비 상품을 잠근다.</param>
    public static bool IsAvailable(ProductData product, uint elapsedDays, Func<uint, bool> isFacilityActive = null)
    {
        return product != null && product.IsAvailable && product.AvailableDay <= elapsedDays &&
            (!product.RequiredFacilityIdx.HasValue || (isFacilityActive != null && isFacilityActive(product.RequiredFacilityIdx.Value)));
    }

    /// <summary>
    /// 지정된 경과 일수에 판매 가능한 상품을 식별자 순서로 반환합니다.
    /// </summary>
    /// <param name="products">검증된 상품 사전입니다.</param>
    /// <param name="elapsedDays">게임 시작일부터 경과한 0 이상의 일수입니다.</param>
    /// <returns>판매 가능한 상품 목록입니다.</returns>
    /// <param name="isFacilityActive">세션의 설비 활성 조회.</param>
    /// <exception cref="ArgumentNullException">상품 사전이 null인 경우 발생합니다.</exception>
    public static IReadOnlyList<ProductData> GetAvailableProducts(
        IReadOnlyDictionary<uint, ProductData> products,
        uint elapsedDays, Func<uint, bool> isFacilityActive = null)
    {
        if (products == null)
        {
            throw new ArgumentNullException(nameof(products));
        }

        var availableProducts = new List<ProductData>();
        foreach (KeyValuePair<uint, ProductData> entry in products)
        {
            if (entry.Value == null || entry.Key != entry.Value.Idx)
            {
                throw new ArgumentException("상품 사전 키와 PK가 다릅니다.", nameof(products));
            }

            if (IsAvailable(entry.Value, elapsedDays, isFacilityActive))
            {
                availableProducts.Add(entry.Value);
            }
        }

        availableProducts.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        return availableProducts.AsReadOnly();
    }
}

/// <summary>해금·등장 조건을 통과한 상품 중 하루의 판매 상품을 단계 가중치로 중복 없이 선택합니다.</summary>
public sealed class DailyProductSelector
{
    private const int BaseWeight = 100;
    private const int Stage1Weight = 220;
    private const int Stage2Weight = 280;
    private const int Stage3Weight = 350;

    // 같은 추첨기 인스턴스 안에서 일관된 난수 흐름을 사용합니다.
    private readonly Random random;

    /// <summary>일일 상품 추첨에 사용할 난수원을 지정합니다.</summary>
    /// <param name="random">고정 시드 테스트에도 사용할 수 있는 난수원입니다.</param>
    /// <exception cref="ArgumentNullException">난수원이 null인 경우 발생합니다.</exception>
    public DailyProductSelector(Random random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>게임 시작 후 경과 일수에 따른 일일 등장 상품 최대 종류 수를 반환합니다.</summary>
    /// <param name="elapsedDays">게임 시작일부터 경과한 0 이상의 일수입니다. 0은 1일차입니다.</param>
    /// <returns>1~9일차 4종, 10~19일차 6종, 20일차 이후 8종입니다.</returns>
    public static int GetMaximumProductKinds(uint elapsedDays)
    {
        if (elapsedDays >= 19) return 8;
        if (elapsedDays >= 9) return 6;
        return 4;
    }

    /// <summary>해금 단계에 대응하는 상대 등장 가중치를 반환합니다.</summary>
    /// <param name="unlockStage">기본 또는 1~3단계 해금 단계입니다.</param>
    /// <returns>기본 100, 1단계 220, 2단계 280, 3단계 350입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">유효한 상품 해금 단계가 아닌 경우 발생합니다.</exception>
    public static int GetSelectionWeight(ProductUnlockStage unlockStage)
    {
        return unlockStage switch
        {
            ProductUnlockStage.Base => BaseWeight,
            ProductUnlockStage.Stage1 => Stage1Weight,
            ProductUnlockStage.Stage2 => Stage2Weight,
            ProductUnlockStage.Stage3 => Stage3Weight,
            _ => throw new ArgumentOutOfRangeException(nameof(unlockStage), unlockStage, "유효한 상품 해금 단계가 필요합니다.")
        };
    }

    /// <summary>현재 날짜와 설비 상태에서 판매 가능한 상품을 가중치 비복원 추출합니다.</summary>
    /// <param name="products">검증된 상품 PK 사전입니다.</param>
    /// <param name="facilities">상품 해금 단계를 조회할 검증된 설비 PK 사전입니다.</param>
    /// <param name="elapsedDays">게임 시작일부터 경과한 0 이상의 일수입니다.</param>
    /// <param name="isFacilityActive">현재 세션의 설비 활성 조회입니다.</param>
    /// <returns>PK 순으로 정렬한 중복 없는 당일 등장 상품입니다.</returns>
    /// <exception cref="ArgumentNullException">필수 사전이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">상품·설비 데이터 또는 가중치 후보가 유효하지 않은 경우 발생합니다.</exception>
    public IReadOnlyList<ProductData> Select(
        IReadOnlyDictionary<uint, ProductData> products,
        IReadOnlyDictionary<uint, FacilityData> facilities,
        uint elapsedDays,
        Func<uint, bool> isFacilityActive)
    {
        if (products == null) throw new ArgumentNullException(nameof(products));
        if (facilities == null) throw new ArgumentNullException(nameof(facilities));
        IReadOnlyList<ProductData> availableProducts = CustomerProductAvailability.GetAvailableProducts(
            products,
            elapsedDays,
            isFacilityActive);
        var candidates = new List<WeightedProductCandidate>(availableProducts.Count);
        foreach (ProductData product in availableProducts)
        {
            ProductUnlockStage unlockStage = CustomerProductAvailability.GetUnlockStage(product, facilities);
            candidates.Add(new WeightedProductCandidate(product, GetSelectionWeight(unlockStage)));
        }

        int selectionCount = Math.Min(GetMaximumProductKinds(elapsedDays), candidates.Count);
        var selected = new List<ProductData>(selectionCount);
        while (selected.Count < selectionCount)
        {
            int totalWeight = 0;
            foreach (WeightedProductCandidate candidate in candidates)
            {
                totalWeight = checked(totalWeight + candidate.Weight);
            }

            if (totalWeight <= 0)
                throw new ArgumentException("일일 상품 후보의 전체 가중치는 양수여야 합니다.", nameof(products));

            int roll = this.random.Next(totalWeight);
            int selectedIndex = -1;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (roll < candidates[index].Weight)
                {
                    selectedIndex = index;
                    break;
                }

                roll -= candidates[index].Weight;
            }

            if (selectedIndex < 0)
                throw new InvalidOperationException("일일 상품 가중치 추첨 결과를 선택하지 못했습니다.");

            selected.Add(candidates[selectedIndex].Product);
            candidates.RemoveAt(selectedIndex);
        }

        selected.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        return selected.AsReadOnly();
    }

    /// <summary>한 추첨 후보의 상품과 양수 상대 가중치입니다.</summary>
    private readonly struct WeightedProductCandidate
    {
        /// <summary>후보 상품입니다.</summary>
        public ProductData Product { get; }
        /// <summary>비복원 추첨에 사용할 상대 가중치입니다.</summary>
        public int Weight { get; }

        /// <summary>검증된 상품 후보를 생성합니다.</summary>
        /// <param name="product">null이 아닌 상품입니다.</param>
        /// <param name="weight">양수 상대 가중치입니다.</param>
        /// <exception cref="ArgumentNullException">상품이 null인 경우 발생합니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">가중치가 양수가 아닌 경우 발생합니다.</exception>
        public WeightedProductCandidate(ProductData product, int weight)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
            Weight = weight;
        }
    }
}
