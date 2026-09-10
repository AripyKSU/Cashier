using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>명성 구성군·설비 해금·성별 교대가 구성 선택 단계에서 함께 적용되는지 검사합니다.</summary>
public sealed class CustomerCompositionSelectorIntegrationTests
{
    /// <summary>명성 일반군 안의 Normal·PriceSensitive 타입과 Wealthy·Hasty가 가중치대로 도달하는지 확인합니다.</summary>
    [Test]
    public void ReputationWeightsSelectTypeGroupsAndKeepPreferenceIndependent()
    {
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData>
        {
            [1] = product(1, ProductType.Water),
            [2] = product(2, ProductType.Food),
            [3] = product(3, ProductType.Medicine),
            [4] = product(4, ProductType.DailyNecessities)
        };
        CustomerDispositionData[] dispositions =
        {
            disposition(6001, CustomerDispositionType.Normal, ProductType.Water),
            disposition(6002, CustomerDispositionType.PriceSensitive, ProductType.Food),
            disposition(6003, CustomerDispositionType.Wealthy, ProductType.Medicine),
            disposition(6004, CustomerDispositionType.Hasty, ProductType.DailyNecessities)
        };
        ReputationBalanceData balance = new ReputationBalanceData
        {
            NormalWeight = 700,
            WealthyWeight = 200,
            HastyWeight = 100,
            SpecialWeight = 0
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

        Assert.That(counts[CustomerDispositionType.Normal], Is.InRange(3200, 3800));
        Assert.That(counts[CustomerDispositionType.PriceSensitive], Is.InRange(3200, 3800));
        Assert.That(counts[CustomerDispositionType.Wealthy], Is.InRange(1700, 2300));
        Assert.That(counts[CustomerDispositionType.Hasty], Is.InRange(700, 1300));
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
            WealthyWeight = 0,
            HastyWeight = 0,
            SpecialWeight = 0
        };
        Dictionary<uint, uint> prices = products.ToDictionary(pair => pair.Key, pair => pair.Value.BasePrice);
        CustomerCompositionSelector selector = new CustomerCompositionSelector(new Random(7));

        CustomerComposition before = selector.SelectComposition(new uint[] { 5001 }, new[] { normalDisposition },
            products, balance, prices, isFacilityActive: _ => false);
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
    private static CustomerDispositionData disposition(uint id, CustomerDispositionType type, ProductType preferredType)
    {
        return new CustomerDispositionData
        {
            Idx = id,
            DispositionType = type,
            PreferredProductTypes = new[] { preferredType },
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
}
