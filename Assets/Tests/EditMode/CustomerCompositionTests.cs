using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>손님 구성 snapshot의 값 검증과 외부 컬렉션 변경 차단을 검사한다.</summary>
public sealed class CustomerCompositionTests
{
    private Dictionary<uint, ProductData> products;
    private Dictionary<uint, uint> prices;
    private CustomerVisit visit;

    /// <summary>현재 생성기 계약으로 유효한 최소 방문을 만든다.</summary>
    [SetUp]
    public void SetUp()
    {
        products = new Dictionary<uint, ProductData>
        {
            [1] = new ProductData
            {
                Idx = 1,
                ProductType = ProductType.Water,
                BasePrice = 100,
                CostPrice = 50,
                IsAvailable = true
            }
        };
        prices = products.ToDictionary(pair => pair.Key, pair => pair.Value.BasePrice);
        var disposition = new CustomerDispositionData
        {
            Idx = 6001,
            DispositionType = CustomerDispositionType.Normal,
            PreferredProductTypes = new[] { ProductType.Water },
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
        visit = new CustomerGenerator(new Random(1)).Generate(
            new uint[] { 5001 },
            new[] { disposition },
            products,
            getCurrentPrices: () => prices);
    }

    /// <summary>구성 값과 순서를 보존하며 입력 컬렉션을 복사한다.</summary>
    [Test]
    public void CopiesValuesAndInputCollections()
    {
        var items = visit.Items.ToList();
        var availableProductIds = new List<uint> { 1, 2 };
        CustomerComposition composition = createComposition(items, availableProductIds, attributes: visit.Attributes);

        items.Clear();
        availableProductIds.Clear();

        Assert.That(composition.AppearanceIdx, Is.EqualTo(visit.AppearanceIdx));
        Assert.That(composition.DispositionIdx, Is.EqualTo(visit.DispositionIdx));
        Assert.That(composition.DispositionType, Is.EqualTo(visit.DispositionType));
        Assert.That(composition.Attributes, Is.EqualTo(visit.Attributes));
        Assert.That(composition.Items.Select(item => item.ProductIdx), Is.EqualTo(new uint[] { 1 }));
        Assert.That(composition.AvailableProductIds, Is.EqualTo(new uint[] { 1, 2 }));
        Assert.That(composition.PriceTolerance, Is.EqualTo(visit.PriceTolerance));
        Assert.That(composition.RegularPriceMinRate, Is.EqualTo(visit.RegularPriceMinRate));
        Assert.That(composition.RegularPriceMaxRate, Is.EqualTo(visit.RegularPriceMaxRate));
    }

    /// <summary>공개된 구성 컬렉션이 읽기 전용이며 내부 순서를 보존하는지 검사한다.</summary>
    [Test]
    public void ExposesReadOnlySnapshots()
    {
        CustomerComposition composition = createComposition(visit.Items.ToList(), new List<uint> { 1, 2 });

        Assert.That(composition.Items, Is.TypeOf<System.Collections.ObjectModel.ReadOnlyCollection<CustomerOrderItem>>());
        Assert.That(composition.AvailableProductIds, Is.TypeOf<System.Collections.ObjectModel.ReadOnlyCollection<uint>>());
        Assert.Throws<NotSupportedException>(() => ((IList<CustomerOrderItem>)composition.Items).Add(visit.Items.Single()));
        Assert.Throws<NotSupportedException>(() => ((IList<uint>)composition.AvailableProductIds).Add(3));
        Assert.That(composition.AvailableProductIds, Is.EqualTo(new uint[] { 1, 2 }));
    }

    /// <summary>구성에 필요한 불변 조건을 위반한 입력을 거부한다.</summary>
    [Test]
    public void RejectsInvalidCompositionValues()
    {
        var items = visit.Items.ToList();
        var availableProductIds = new uint[] { 1 };

        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, appearanceIdx: 0));
        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, dispositionIdx: 0));
        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, dispositionType: CustomerDispositionType.None));
        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, attributes: CustomerAttributes.Male));
        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, entryTextIdx: 0));
        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, priceTolerance: 0));
        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, regularPriceMinRate: 1001));
        Assert.Throws<ArgumentException>(() => createComposition(items, availableProductIds, regularPriceMaxRate: 999));
    }

    /// <summary>상품 목록이 비어 있거나 허용 snapshot과 불일치하는 구매 항목을 거부한다.</summary>
    [Test]
    public void RejectsInvalidProductSnapshots()
    {
        var item = visit.Items.Single();

        Assert.Throws<ArgumentException>(() => createComposition(Array.Empty<CustomerOrderItem>(), new uint[] { 1 }));
        Assert.Throws<ArgumentException>(() => createComposition(new[] { item, item }, new uint[] { 1 }));
        Assert.Throws<ArgumentException>(() => createComposition(new[] { item }, Array.Empty<uint>()));
        Assert.Throws<ArgumentException>(() => createComposition(new[] { item }, new uint[] { 2 }));
        Assert.Throws<ArgumentException>(() => createComposition(new[] { item }, new uint[] { 1, 1 }));
        Assert.Throws<ArgumentException>(() => createComposition(new[] { item }, new uint[] { 0 }));
    }

    /// <summary>테스트용 유효 구성을 만든다.</summary>
    /// <param name="items">구매 항목.</param>
    /// <param name="availableProductIds">허용 상품 snapshot.</param>
    /// <param name="appearanceIdx">외형 PK.</param>
    /// <param name="dispositionIdx">성향 PK.</param>
    /// <param name="dispositionType">성향 타입.</param>
    /// <param name="attributes">완성 손님 속성.</param>
    /// <param name="entryTextIdx">입장 대사 FK.</param>
    /// <param name="priceTolerance">가격 허용 배율.</param>
    /// <param name="regularPriceMinRate">정가 하한 배율.</param>
    /// <param name="regularPriceMaxRate">정가 상한 배율.</param>
    /// <returns>생성된 구성.</returns>
    private CustomerComposition createComposition(
        IEnumerable<CustomerOrderItem> items,
        IEnumerable<uint> availableProductIds,
        uint appearanceIdx = 5001,
        uint dispositionIdx = 6001,
        CustomerDispositionType dispositionType = CustomerDispositionType.Normal,
        CustomerAttributes attributes = CustomerAttributes.Male | CustomerAttributes.Adult | CustomerAttributes.Normal,
        uint entryTextIdx = 1,
        int priceTolerance = 1100,
        int regularPriceMinRate = 1000,
        int regularPriceMaxRate = 1000)
    {
        return new CustomerComposition(
            appearanceIdx,
            dispositionIdx,
            dispositionType,
            attributes,
            items,
            entryTextIdx,
            2,
            3,
            4,
            5,
            priceTolerance,
            regularPriceMinRate,
            regularPriceMaxRate,
            availableProductIds);
    }
}
