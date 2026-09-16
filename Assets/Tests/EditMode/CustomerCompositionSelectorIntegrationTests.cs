using System;
using System.Collections.Generic;
using System.IO;
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
            CustomerAppearanceFixtures.Create(), dispositions, products, balance, prices, elapsedDays);

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
            CustomerAppearanceFixtures.Create(), dispositions, products, normalOnlyBalance(), prices, elapsedDays: 0);

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
                CustomerAppearanceFixtures.Create(), dispositions, products, balance, prices);
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

        CustomerComposition before = selector.SelectComposition(CustomerAppearanceFixtures.Create(), new[] { normalDisposition },
            products, balance, new Dictionary<uint, uint> { [1] = prices[1] }, isFacilityActive: _ => false);
        CustomerComposition after = selector.SelectComposition(CustomerAppearanceFixtures.Create(), new[] { normalDisposition },
            products, balance, prices, isFacilityActive: _ => true);

        Assert.That(before.Items.Single().ProductIdx, Is.EqualTo(1));
        Assert.That(after.Items.Single().ProductIdx, Is.EqualTo(2));
        Assert.That(before.AvailableProductIds, Is.EqualTo(new uint[] { 1 }));
        Assert.That(after.AvailableProductIds, Is.EqualTo(new uint[] { 1, 2 }));
    }

    /// <summary>선택된 여섯 속성 조합이 외형 분류와 일치하고 동일 seed가 후보 선택까지 재현됩니다.</summary>
    [Test]
    public void AppearanceMatchesSelectedGenderAndAge()
    {
        IReadOnlyDictionary<uint, CustomerAppearanceData> appearances = CustomerAppearanceFixtures.Create();
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData> { [1] = product(1, ProductType.Water) };
        Dictionary<uint, uint> prices = new Dictionary<uint, uint> { [1] = 100 };
        CustomerDispositionData[] dispositions = { disposition(6001, CustomerDispositionType.Normal, ProductType.Water) };
        var left = new CustomerCompositionSelector(new Random(29));
        var right = new CustomerCompositionSelector(new Random(29));
        var combinations = new HashSet<CustomerAttributes>();
        var selectedAppearanceIds = new HashSet<uint>();

        for (int i = 0; i < 600; i++)
        {
            CustomerComposition first = left.SelectComposition(appearances, dispositions, products, normalOnlyBalance(), prices);
            CustomerComposition second = right.SelectComposition(appearances, dispositions, products, normalOnlyBalance(), prices);
            CustomerAppearanceData appearance = appearances[first.AppearanceIdx];
            CustomerAttributes gender = first.Attributes & (CustomerAttributes.Male | CustomerAttributes.Female);
            CustomerAttributes age = first.Attributes & (CustomerAttributes.Child | CustomerAttributes.Elderly | CustomerAttributes.Adult);
            Assert.That((appearance.Gender, appearance.Age), Is.EqualTo((gender, age)));
            Assert.That(second.AppearanceIdx, Is.EqualTo(first.AppearanceIdx));
            combinations.Add(gender | age);
            selectedAppearanceIds.Add(first.AppearanceIdx);
        }

        Assert.That(combinations.Count, Is.EqualTo(6));
        Assert.That(selectedAppearanceIds.Count, Is.EqualTo(appearances.Values.Count(row => row.DispositionType == CustomerDispositionType.Normal)));
    }

    /// <summary>무상품 null과 외형 설정 실패는 다음 방문의 성별 교대 상태를 소비하지 않습니다.</summary>
    [Test]
    public void FailedCompositionPreservesGenderState()
    {
        IReadOnlyDictionary<uint, CustomerAppearanceData> appearances = CustomerAppearanceFixtures.Create();
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData> { [1] = product(1, ProductType.Water) };
        Dictionary<uint, uint> prices = new Dictionary<uint, uint> { [1] = 100 };
        CustomerDispositionData[] dispositions = { disposition(6001, CustomerDispositionType.Normal, ProductType.Water) };
        var selector = new CustomerCompositionSelector(new Random(31));
        CustomerComposition first = selector.SelectComposition(appearances, dispositions, products, normalOnlyBalance(), prices);
        CustomerAttributes firstGender = first.Attributes & (CustomerAttributes.Male | CustomerAttributes.Female);

        ProductData unavailable = product(1, ProductType.Water);
        unavailable.IsAvailable = false;
        Assert.That(selector.SelectComposition(appearances, dispositions,
            new Dictionary<uint, ProductData> { [1] = unavailable }, normalOnlyBalance(),
            new Dictionary<uint, uint>()), Is.Null);

        var wrongGenderAppearances = appearances.Values.Where(row => row.Gender == firstGender).ToDictionary(row => row.Idx);
        Assert.Throws<InvalidDataException>(() => selector.SelectComposition(
            wrongGenderAppearances, dispositions, products, normalOnlyBalance(), prices));

        CustomerComposition next = selector.SelectComposition(appearances, dispositions, products, normalOnlyBalance(), prices);
        CustomerAttributes nextGender = next.Attributes & (CustomerAttributes.Male | CustomerAttributes.Female);
        Assert.That(nextGender, Is.EqualTo(firstGender == CustomerAttributes.Male ? CustomerAttributes.Female : CustomerAttributes.Male));
    }

    /// <summary>연속 방문의 남녀 성별에 맞는 판매 대사 후보만 선택합니다.</summary>
    [Test]
    public void DialogueCandidatesFollowSelectedGender()
    {
        CustomerDispositionData config = disposition(6001, CustomerDispositionType.Normal, ProductType.Water);
        config.MaleEntryTextIdxs = new uint[] { 101 };
        config.MaleRegularSaleTextIdxs = new uint[] { 102 };
        config.MaleDiscountSaleTextIdxs = new uint[] { 103 };
        config.MaleExploitativeSaleTextIdxs = new uint[] { 104 };
        config.MaleRejectTextIdxs = new uint[] { 105 };
        config.FemaleEntryTextIdxs = new uint[] { 201 };
        config.FemaleRegularSaleTextIdxs = new uint[] { 202 };
        config.FemaleDiscountSaleTextIdxs = new uint[] { 203 };
        config.FemaleExploitativeSaleTextIdxs = new uint[] { 204 };
        config.FemaleRejectTextIdxs = new uint[] { 205 };
        Dictionary<uint, ProductData> products = new Dictionary<uint, ProductData>
        {
            [1] = product(1, ProductType.Water)
        };
        Dictionary<uint, uint> prices = new Dictionary<uint, uint> { [1] = 100 };
        CustomerCompositionSelector selector = new CustomerCompositionSelector(new FixedRandom(0));
        IReadOnlyDictionary<uint, CustomerAppearanceData> appearances = CustomerAppearanceFixtures.Create();

        CustomerComposition first = selector.SelectComposition(appearances, new[] { config }, products,
            normalOnlyBalance(), prices);
        CustomerComposition second = selector.SelectComposition(appearances, new[] { config }, products,
            normalOnlyBalance(), prices);

        Assert.That(first.Attributes & CustomerAttributes.Male, Is.EqualTo(CustomerAttributes.Male));
        Assert.That((first.EntryTextIdx, first.RegularSaleTextIdx, first.DiscountSaleTextIdx,
            first.ExploitativeSaleTextIdx, first.RejectTextIdx), Is.EqualTo((101u, 102u, 103u, 104u, 105u)));
        Assert.That(second.Attributes & CustomerAttributes.Female, Is.EqualTo(CustomerAttributes.Female));
        Assert.That((second.EntryTextIdx, second.RegularSaleTextIdx, second.DiscountSaleTextIdx,
            second.ExploitativeSaleTextIdx, second.RejectTextIdx), Is.EqualTo((201u, 202u, 203u, 204u, 205u)));
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
