using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>명성 구성군·설비 해금·성별 교대가 구성 선택 단계에서 함께 적용되는지 검사합니다.</summary>
public sealed class CustomerCompositionSelectorIntegrationTests
{
    /// <summary>표시일9/10일과19/20일 경계에서 같은 성향의 선호 행 가중치가 바뀝니다.</summary>
    [TestCase(8u, 0.4d, 6001u)]
    [TestCase(9u, 0.4d, 6002u)]
    [TestCase(18u, 0.7d, 6002u)]
    [TestCase(19u, 0.7d, 6003u)]
    public void PreferenceRowsFollowElapsedDayBands(uint elapsedDays, double sample, uint expectedDispositionId)
    {
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData>
        {
            [1] = product(1, ProductType.Water),
            [2] = product(2, ProductType.Tools),
            [3] = product(3, ProductType.ProtectiveEquipment)
        };
        CustomerDispositionData[] dispositions =
        {
            disposition(6001, CustomerDispositionType.Normal, ProductType.Water),
            disposition(6002, CustomerDispositionType.Normal, ProductType.Tools),
            disposition(6003, CustomerDispositionType.Normal, ProductType.ProtectiveEquipment)
        };
        ReputationBalanceData balance = normalOnlyBalance();
        Dictionary<uint, uint> prices = products.ToDictionary(pair => pair.Key, pair => pair.Value.BasePrice);
        CustomerCompositionSelector selector = new CustomerCompositionSelector(new FixedRandom(sample));

        CustomerComposition composition = selector.SelectComposition(
            new uint[] { 5001 }, dispositions, products, balance, prices, elapsedDays);

        Assert.That(composition.DispositionIdx, Is.EqualTo(expectedDispositionId));
    }

    /// <summary>복수 선호 타입 행은 타입 수에 따라 유리해지지 않고 각 타입 가중치의 평균을 사용합니다.</summary>
    [Test]
    public void MultiplePreferredTypesUseArithmeticMeanWeight()
    {
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData>
        {
            [1] = product(1, ProductType.Water),
            [2] = product(2, ProductType.Tools),
            [3] = product(3, ProductType.ProtectiveEquipment)
        };
        CustomerDispositionData[] dispositions =
        {
            disposition(6001, CustomerDispositionType.Normal, ProductType.Water),
            disposition(6002, CustomerDispositionType.Normal,
                ProductType.Tools, ProductType.ProtectiveEquipment)
        };
        Dictionary<uint, uint> prices = products.ToDictionary(pair => pair.Key, pair => pair.Value.BasePrice);
        CustomerCompositionSelector selector = new CustomerCompositionSelector(new FixedRandom(0.65d));

        CustomerComposition composition = selector.SelectComposition(
            new uint[] { 5001 }, dispositions, products, normalOnlyBalance(), prices, elapsedDays: 0);

        Assert.That(composition.DispositionIdx, Is.EqualTo(6001u));
    }

    /// <summary>명성별 Normal·Wealthy·Hasty·Poor 타입이 가중치대로 도달하는지 확인합니다.</summary>
    [Test]
    public void ReputationWeightsSelectTypeGroupsAndKeepPreferenceIndependent()
    {
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData>
        {
            [1] = product(1, ProductType.Water),
            [2] = product(2, ProductType.Food),
            [3] = product(3, ProductType.Medicine),
            [4] = product(4, ProductType.DailyNecessities),
            [5] = product(5, ProductType.Tools)
        };
        CustomerDispositionData[] dispositions =
        {
            disposition(6001, CustomerDispositionType.Normal, ProductType.Water),
            disposition(6002, CustomerDispositionType.PriceSensitive, ProductType.Food),
            disposition(6003, CustomerDispositionType.Wealthy, ProductType.Medicine),
            disposition(6004, CustomerDispositionType.Hasty, ProductType.DailyNecessities),
            disposition(6005, CustomerDispositionType.Poor, ProductType.Tools)
        };
        ReputationBalanceData balance = new ReputationBalanceData
        {
            NormalWeight = 600,
            PriceSensitiveWeight = 100,
            WealthyWeight = 150,
            HastyWeight = 50,
            PoorWeight = 100
        };
        Dictionary<uint, uint> prices = products.ToDictionary(pair => pair.Key, pair => pair.Value.BasePrice);
        CustomerCompositionSelector selector = new CustomerCompositionSelector(new Random(41));
        Dictionary<CustomerDispositionType, int> counts = new Dictionary<CustomerDispositionType, int>();

        for (int i = 0; i < 10000; i++)
        {
            CustomerComposition composition = selector.SelectComposition(
                new uint[] { 5001, 5002 }, dispositions, products, balance, prices);
            counts.TryGetValue(composition.DispositionType, out int count);
            counts[composition.DispositionType] = count + 1;
        }

        Assert.That(counts[CustomerDispositionType.Normal], Is.InRange(5700, 6300));
        Assert.That(counts[CustomerDispositionType.PriceSensitive], Is.InRange(700, 1300));
        Assert.That(counts[CustomerDispositionType.Wealthy], Is.InRange(1200, 1800));
        Assert.That(counts[CustomerDispositionType.Hasty], Is.InRange(200, 800));
        Assert.That(counts[CustomerDispositionType.Poor], Is.InRange(700, 1300));
    }

    /// <summary>설비가 잠긴 동안에는 선호 타입을 만족하는 상품을 고르지 않고, 활성화 후에만 고르는지 확인합니다.</summary>
    [Test]
    public void PreferredEquipmentAppearsOnlyAfterFacilityActivation()
    {
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData>
        {
            [1] = product(1, ProductType.Water),
            [2] = product(2, ProductType.ElectricalEquipment, 12004)
        };
        CustomerDispositionData normalDisposition = disposition(6001, CustomerDispositionType.Normal,
            ProductType.ElectricalEquipment);
        ReputationBalanceData balance = new ReputationBalanceData
        {
            NormalWeight = 1000,
            PriceSensitiveWeight = 0,
            WealthyWeight = 0,
            HastyWeight = 0,
            PoorWeight = 0
        };
        Dictionary<uint, uint> prices = products.ToDictionary(pair => pair.Key, pair => pair.Value.BasePrice);
        CustomerCompositionSelector selector = new CustomerCompositionSelector(new Random(7));

        CustomerComposition before = selector.SelectComposition(new uint[] { 5001 }, new[] { normalDisposition },
            products, balance, new Dictionary<uint, uint> { [1] = prices[1] }, isFacilityActive: _ => false);
        CustomerComposition after = selector.SelectComposition(new uint[] { 5001 }, new[] { normalDisposition },
            products, balance, prices, isFacilityActive: _ => true);

        Assert.That(before.Items.Single().ProductIdx, Is.EqualTo(1));
        Assert.That(after.Items.Single().ProductIdx, Is.EqualTo(2));
        Assert.That(before.AvailableProductIds, Is.EqualTo(new uint[] { 1 }));
        Assert.That(after.AvailableProductIds, Is.EqualTo(new uint[] { 1, 2 }));
    }

    /// <summary>테스트용 상품을 만듭니다.</summary>
    private static ProductData product(uint id, ProductType type, uint? requiredFacilityIdx = null)
    {
        return new ProductData
        {
            Idx = id,
            ProductType = type,
            BasePrice = 100,
            CostPrice = 50,
            IsAvailable = true,
            RequiredFacilityIdx = requiredFacilityIdx
        };
    }

    /// <summary>테스트용 구매 설정을 만듭니다.</summary>
    private static CustomerDispositionData disposition(
        uint id,
        CustomerDispositionType type,
        params ProductType[] preferredTypes)
    {
        return new CustomerDispositionData
        {
            Idx = id,
            DispositionType = type,
            PreferredProductTypes = preferredTypes,
            PreferredSelectionChance = 1000,
            MinProductKinds = 1,
            MaxProductKinds = 1,
            MinQuantity = 1,
            MaxQuantity = 1,
            PriceTolerance = 1100,
            EntryTextIdxs = new uint[] { 1 },
            RegularSaleTextIdxs = new uint[] { 2 },
            DiscountSaleTextIdxs = new uint[] { 3 },
            ExploitativeSaleTextIdxs = new uint[] { 4 },
            RejectTextIdxs = new uint[] { 5 }
        };
    }

    /// <summary>Normal 구성군만 선택하는 명성 데이터를 만듭니다.</summary>
    private static ReputationBalanceData normalOnlyBalance()
    {
        return new ReputationBalanceData
        {
            NormalWeight = 1000,
            PriceSensitiveWeight = 0,
            WealthyWeight = 0,
            HastyWeight = 0,
            PoorWeight = 0
        };
    }

    /// <summary>모든 난수 호출에 지정한0~1 표본을 반환합니다.</summary>
    private sealed class FixedRandom : Random
    {
        private readonly double sample;

        public FixedRandom(double sample)
        {
            this.sample = sample;
        }

        protected override double Sample()
        {
            return this.sample;
        }
    }
}
