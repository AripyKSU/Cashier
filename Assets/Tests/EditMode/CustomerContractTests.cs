using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>손님 생성·최종 거래·지침 계약을 UI 없이 독립 사례로 검증한다.</summary>
public sealed class CustomerContractTests
{
    private CustomerDispositionData config;
    private Dictionary<uint, ProductData> products;
    private Dictionary<uint, uint> prices;
    private CustomerGenerator generator;

    /// <summary>각 사례마다 가변 원본과 난수원을 격리한다.</summary>
    [SetUp]
    public void SetUp()
    {
        config = makeConfig(1, CustomerDispositionType.Normal, 1);
        products = Enumerable.Range(1, 4).ToDictionary(x => (uint)x, x => new ProductData
        { Idx = (uint)x, ProductType = x <= 2 ? ProductType.Water : ProductType.Food, BasePrice = 101, CostPrice = 50, IsAvailable = true });
        prices = products.ToDictionary(x => x.Key, x => x.Value.BasePrice);
        generator = new CustomerGenerator(new Random(17));
    }

    /// <summary>생성 종류·수량·후보 소진·중복 방지를 검사한다.</summary>
    [Test]
    public void GenerationRangesAndPoolExhaustion()
    {
        config.MinProductKinds = 1; config.MaxProductKinds = 3; config.MaxQuantity = 3;
        for (int i = 0; i < 1000; i++)
        {
            var visit = generate();
            Assert.That(visit.Items.Count, Is.InRange(1, 3));
            Assert.That(visit.Items.Select(x => x.ProductIdx).Distinct().Count(), Is.EqualTo(visit.Items.Count));
            Assert.That(visit.Items.All(x => x.Quantity >= 1 && x.Quantity <= 3));
        }
        config.MinProductKinds = config.MaxProductKinds = 4;
        Assert.That(generate().Items.Count, Is.EqualTo(4));
        products.Remove(2); products.Remove(3); products.Remove(4);
        var snapshot = generate(); config.PreferredProductIdxs = Array.Empty<uint>(); products.Clear();
        Assert.That(snapshot.Items.Count, Is.EqualTo(1)); Assert.That(generate(), Is.Null);
    }

    /// <summary>확률 단위와 품목 OR 개별 상품 선호의 경계를 검사한다.</summary>
    /// <param name="chance">0~1000 선호 확률.</param>
    [TestCase(0), TestCase(900), TestCase(1000)]
    public void PreferenceProbabilityAndUnion(int chance)
    {
        config.PreferredProductTypes = new[] { ProductType.Water };
        config.PreferredProductIdxs = new uint[] { 1, 3 };
        config.PreferredSelectionChance = chance;
        int preferred = 0;
        var seen = new HashSet<uint>();
        for (int i = 0; i < 10000; i++)
        {
            uint id = generate().Items.Single().ProductIdx; seen.Add(id); if (id != 4) preferred++;
        }
        Assert.That(preferred, chance == 900 ? Is.InRange(8700, 9300) : Is.EqualTo(chance == 0 ? 0 : 10000));
        if (chance == 1000) Assert.That(seen, Is.EquivalentTo(new uint[] { 1, 2, 3 }));
        products[3].AvailableDay = 1; products[4].IsAvailable = false;
        config.MinProductKinds = config.MaxProductKinds = 4;
        Assert.That(generate().Items.Select(x => x.ProductIdx), Is.EquivalentTo(new uint[] { 1, 2 }));
    }

    /// <summary>미등장·비활성만 남으면 정상적으로 방문을 만들지 않는다.</summary>
    [Test]
    public void NoAvailableProducts()
    {
        foreach (var product in products.Values) product.AvailableDay = 2;
        Assert.That(generate(), Is.Null);
        foreach (var product in products.Values) { product.AvailableDay = 0; product.IsAvailable = false; }
        Assert.That(generate(), Is.Null);
    }

    /// <summary>잘못된 설정은 생성 경계에서 거부한다.</summary>
    /// <param name="kind">변경할 입력 경계.</param>
    [TestCase("chance-low"), TestCase("chance-high"), TestCase("quantity"), TestCase("null"), TestCase("zero"), TestCase("duplicate"), TestCase("fk"), TestCase("type"), TestCase("min"), TestCase("max")]
    public void InvalidConfiguration(string kind)
    {
        switch (kind)
        {
            case "chance-low": config.PreferredSelectionChance = -1; break;
            case "chance-high": config.PreferredSelectionChance = 1001; break;
            case "quantity": config.MinQuantity = 0; break;
            case "null": config.PreferredProductIdxs = null; break;
            case "zero": config.PreferredProductIdxs = new uint[] { 0 }; break;
            case "duplicate": config.PreferredProductIdxs = new uint[] { 1, 1 }; break;
            case "fk": config.PreferredProductIdxs = new uint[] { 999 }; break;
            case "type": config.DispositionType = CustomerDispositionType.CustomerDispositionType_End; break;
            case "min": config.RegularPriceMinRate = 1001; break;
            case "max": config.RegularPriceMaxRate = 999; break;
        }
        Assert.Catch<ArgumentException>(() => generate());
    }

    /// <summary>중복 외형 및 가격 공급 누락을 거부한다.</summary>
    [Test]
    public void InvalidGenerationInputs()
    {
        Assert.Throws<ArgumentException>(() => generator.Generate(new uint[] { 1, 1 }, new[] { config }, products, getCurrentPrices: () => prices));
        Assert.Throws<ArgumentNullException>(() => generator.Generate(new uint[] { 1 }, new[] { config }, products));
    }

    /// <summary>행 개수와 독립인 타입 균등성·정렬 seed·6속성을 검사한다.</summary>
    [Test]
    public void TypeThenRowSelectionAndIndependentAttributes()
    {
        var rows = new[] { makeConfig(1, CustomerDispositionType.Normal, 1), makeConfig(2, CustomerDispositionType.Normal, 2),
            makeConfig(3, CustomerDispositionType.Normal, 1), makeConfig(4, CustomerDispositionType.Hasty, 2) };
        var left = new CustomerGenerator(new Random(73)); var right = new CustomerGenerator(new Random(73));
        var counts = new Dictionary<uint, int>(); var attributes = new Dictionary<CustomerAttributes, int>(); int normal = 0;
        for (int i = 0; i < 12000; i++)
        {
            var a = left.Generate(new uint[] { 1 }, rows, products, getCurrentPrices: () => prices);
            var b = right.Generate(new uint[] { 1 }, rows.Reverse().ToArray(), products, getCurrentPrices: () => prices);
            Assert.That((a.DispositionIdx, a.Attributes, a.Items[0].ProductIdx), Is.EqualTo((b.DispositionIdx, b.Attributes, b.Items[0].ProductIdx)));
            Assert.That(a.Items.Single().ProductIdx, Is.EqualTo(rows.Single(x => x.Idx == a.DispositionIdx).PreferredProductIdxs.Single()));
            Assert.That(a.DispositionType, Is.EqualTo(CustomerDispositionType.Normal).Or.EqualTo(CustomerDispositionType.Hasty));
            if (a.DispositionType == CustomerDispositionType.Normal) normal++;
            counts.TryGetValue(a.DispositionIdx, out int count); counts[a.DispositionIdx] = count + 1;
            attributes.TryGetValue(a.Attributes, out count); attributes[a.Attributes] = count + 1;
        }
        Assert.That(normal, Is.InRange(5700, 6300)); Assert.That(counts.Count, Is.EqualTo(4));
        Assert.That(counts.Where(x => x.Key <= 3).All(x => x.Value >= 1750 && x.Value <= 2250));
        Assert.That(attributes.Count, Is.EqualTo(6)); Assert.That(attributes.ContainsKey(CustomerAttributes.None), Is.False);
        Assert.That(attributes.Values.All(x => x >= 1750 && x <= 2250));
    }

    /// <summary>동일 seed가 외형·상품·수량·속성 전체를 재현한다.</summary>
    [Test]
    public void SeedReproducesAppearanceAndOrder()
    {
        config.MaxQuantity = 3; config.MaxProductKinds = 3;
        var left = new CustomerGenerator(new Random(5)); var right = new CustomerGenerator(new Random(5));
        for (int i = 0; i < 20; i++)
        {
            var a = left.Generate(new uint[] { 1, 2 }, new[] { config }, products, getCurrentPrices: () => prices);
            var b = right.Generate(new uint[] { 1, 2 }, new[] { config }, products, getCurrentPrices: () => prices);
            Assert.That((a.AppearanceIdx, a.DispositionIdx, a.Attributes), Is.EqualTo((b.AppearanceIdx, b.DispositionIdx, b.Attributes)));
            Assert.That(a.Items.Select(x => (x.ProductIdx, x.Quantity, x.UnitPrice)), Is.EqualTo(b.Items.Select(x => (x.ProductIdx, x.Quantity, x.UnitPrice))));
        }
    }

    /// <summary>모든 비트 경계 및 enum 종료 표식을 검사한다.</summary>
    [Test]
    public void ProfileValidationAndSnapshots()
    {
        for (int type = -1; type <= 5; type++)
        {
            int value = type;
            if (type >= 1 && type <= 4) Assert.DoesNotThrow(() => CustomerProfileValidation.ValidateType((CustomerDispositionType)value));
            else Assert.Throws<ArgumentOutOfRangeException>(() => CustomerProfileValidation.ValidateType((CustomerDispositionType)value));
        }
        var valid = new HashSet<int> { 0, 1, 2, 4, 5, 6, 8, 9, 10 };
        for (int bits = -1; bits <= 31; bits++)
        {
            int value = bits;
            if (valid.Contains(bits)) Assert.DoesNotThrow(() => CustomerProfileValidation.ValidateAttributes((CustomerAttributes)value));
            else Assert.Throws<ArgumentException>(() => CustomerProfileValidation.ValidateAttributes((CustomerAttributes)value));
        }
        config.RegularPriceMinRate = 950; config.RegularPriceMaxRate = 1050;
        var visit = generate(); config.RegularPriceMinRate = 1000; config.DispositionType = CustomerDispositionType.Wealthy;
        Assert.That(visit.RegularPriceMinRate, Is.EqualTo(950)); Assert.That(visit.RegularPriceMaxRate, Is.EqualTo(1050));
        Assert.That(visit.DispositionType, Is.EqualTo(CustomerDispositionType.Normal));
    }

    /// <summary>최종 목록 교체에 대한 기존 네 판정과 정산 의미를 검사한다.</summary>
    /// <param name="offered">총액.</param>
    /// <param name="expected">예상 판정.</param>
    [TestCase(201, CustomerTradeOutcome.DiscountSale), TestCase(202, CustomerTradeOutcome.RegularSale), TestCase(203, CustomerTradeOutcome.ExploitativeSale), TestCase(223, CustomerTradeOutcome.PaymentRefused)]
    public void FinalItemsDetermineOutcome(long offered, CustomerTradeOutcome expected)
    {
        config.PriceTolerance = 1100; products[4].IsAvailable = false;
        var visit = generate(); visit.BeginOffer(); visit.SubmitOffer(offered, new[] { new SaleItem(4, 2) });
        var result = visit.Result.Value;
        Assert.That(visit.Items.Single().ProductIdx, Is.EqualTo(1)); Assert.That(result.ReferenceTotal, Is.EqualTo(202));
        Assert.That(visit.Outcome, Is.EqualTo(expected)); Assert.That(result.SaleIncome, Is.EqualTo(expected == CustomerTradeOutcome.PaymentRefused ? 0 : offered));
        Assert.That(result.CostTotal, Is.EqualTo(expected == CustomerTradeOutcome.PaymentRefused ? 0 : 100));
        Assert.That(result.SoldItems.Count, Is.EqualTo(expected == CustomerTradeOutcome.PaymentRefused ? 0 : 1));
        Assert.That(result.ReputationDelta, Is.Zero); Assert.Throws<InvalidOperationException>(() => visit.SubmitOffer(1, new[] { new SaleItem(1, 1) }));
        visit.Depart(); Assert.That(visit.Outcome, Is.EqualTo(expected)); Assert.That(visit.State, Is.EqualTo(CustomerState.Departed));
    }

    /// <summary>제출시 현재가 1회 조회·합산·불변 결과를 검사한다.</summary>
    [Test]
    public void SubmissionPricesAndQuantitiesAreImmutable()
    {
        int reads = 0; var visit = generator.Generate(new uint[] { 1 }, new[] { config }, products, getCurrentPrices: () => { reads++; return prices; });
        Assert.That(visit.Result, Is.Null); Assert.That(visit.AllowedTotal, Is.Null); var original = visit.Items;
        prices[1] = 200; var input = new List<SaleItem> { new SaleItem(1, 1), new SaleItem(1, 2) }; visit.BeginOffer();
        visit.SubmitOffer(600, input); Assert.That(reads, Is.EqualTo(2)); input.Clear(); prices[1] = 999; products[1].CostPrice = 999;
        Assert.That(visit.Result.Value.SoldItems.Single().Quantity, Is.EqualTo(3)); Assert.That(visit.Result.Value.CostTotal, Is.EqualTo(150));
        Assert.That(visit.Result.Value.SoldItems.Single().UnitPrice, Is.EqualTo(200)); Assert.That(visit.Items, Is.SameAs(original));
    }

    /// <summary>가격 인정 범위는 교차곱으로 양끝을 포함한다.</summary>
    /// <param name="price">기준 단가.</param>
    /// <param name="offer">제안가.</param>
    /// <param name="expected">판정.</param>
    [TestCase(101, 95, CustomerTradeOutcome.DiscountSale), TestCase(101, 96, CustomerTradeOutcome.RegularSale), TestCase(101, 106, CustomerTradeOutcome.RegularSale), TestCase(101, 107, CustomerTradeOutcome.ExploitativeSale), TestCase(100, 95, CustomerTradeOutcome.RegularSale), TestCase(100, 105, CustomerTradeOutcome.RegularSale)]
    public void RegularRangeIsInclusive(int price, long offer, CustomerTradeOutcome expected)
    {
        config.RegularPriceMinRate = 950; config.RegularPriceMaxRate = 1050; config.PriceTolerance = 1200; prices[1] = (uint)price;
        var visit = generate(); visit.BeginOffer(); visit.SubmitOffer(offer, new[] { new SaleItem(1, 1) }); Assert.That(visit.Outcome, Is.EqualTo(expected));
    }

    /// <summary>결제 거부는 인정범위보다 우선하며 큰 교차곱도 정확하다.</summary>
    [Test]
    public void RefusalPriorityAndLargeAmounts()
    {
        products[1].CostPrice = 200; Assert.DoesNotThrow(() => products[1].Validate()); products[1].CostPrice = 50;
        config.PriceTolerance = 900; var visit = generate(); visit.BeginOffer(); visit.SubmitOffer(101, new[] { new SaleItem(1, 1) });
        Assert.That(visit.Outcome, Is.EqualTo(CustomerTradeOutcome.PaymentRefused));
        config.PriceTolerance = 1000; config.RegularPriceMaxRate = int.MaxValue; prices[1] = uint.MaxValue;
        visit = generate(); visit.BeginOffer(); visit.SubmitOffer((long)uint.MaxValue * int.MaxValue, new[] { new SaleItem(1, int.MaxValue) });
        Assert.That(visit.Outcome, Is.EqualTo(CustomerTradeOutcome.RegularSale));
    }

    /// <summary>잘못된 최종 입력·가격·overflow는 방문 상태를 공개하지 않는다.</summary>
    /// <param name="kind">오류 경계.</param>
    [TestCase("zero-offer"), TestCase("negative-offer"), TestCase("null"), TestCase("empty"), TestCase("unknown"), TestCase("zero-quantity"), TestCase("negative-quantity"), TestCase("quantity-overflow"), TestCase("missing-price"), TestCase("zero-price"), TestCase("reference-overflow"), TestCase("cost-overflow"), TestCase("allowed-overflow"), TestCase("lookup")]
    public void InvalidSubmissionIsAtomic(string kind)
    {
        bool lookupFails = false; config.PriceTolerance = kind == "allowed-overflow" ? int.MaxValue : 1000;
        var visit = generator.Generate(new uint[] { 1 }, new[] { config }, products, getCurrentPrices: () => lookupFails ? throw new InvalidOperationException("lookup") : prices);
        visit.BeginOffer(); long offer = 1; IReadOnlyList<SaleItem> input = new[] { new SaleItem(1, 1) };
        switch (kind)
        {
            case "zero-offer": offer = 0; break; case "negative-offer": offer = -1; break;
            case "null": input = null; break; case "empty": input = Array.Empty<SaleItem>(); break;
            case "unknown": input = new[] { new SaleItem(99, 1) }; break;
            case "zero-quantity": input = new[] { new SaleItem(1, 0) }; break; case "negative-quantity": input = new[] { new SaleItem(1, -1) }; break;
            case "quantity-overflow": input = new[] { new SaleItem(1, int.MaxValue), new SaleItem(1, 1) }; break;
            case "missing-price": prices.Remove(1); break; case "zero-price": prices[1] = 0; break;
            case "reference-overflow": prices[1] = prices[2] = uint.MaxValue; input = new[] { new SaleItem(1, int.MaxValue), new SaleItem(2, int.MaxValue) }; break;
            case "cost-overflow": products[1].CostPrice = products[2].CostPrice = uint.MaxValue; input = new[] { new SaleItem(1, int.MaxValue), new SaleItem(2, int.MaxValue) }; break;
            case "allowed-overflow": prices[1] = uint.MaxValue; input = new[] { new SaleItem(1, int.MaxValue) }; break;
            case "lookup": lookupFails = true; break;
        }
        Assert.Catch(() => visit.SubmitOffer(offer, input)); assertUnpublished(visit);
        lookupFails = false; prices[1] = prices[2] = 101; products[1].CostPrice = products[2].CostPrice = 50;
        Assert.That(visit.SubmitOffer(101, new[] { new SaleItem(1, 1) }), Is.True);
    }

    /// <summary>지침 AND·분류·합산수량은 최종 판매를 검사하며 위반은 결제를 막지 않는다.</summary>
    [Test]
    public void RestrictionsRecordFinalItemsWithoutChangingIncome()
    {
        var rules = new List<SaleRestriction>(); int reads = 0;
        var visit = generate(() => { reads++; return rules; }); Assert.That(reads, Is.Zero);
        var gender = visit.Attributes & (CustomerAttributes.Male | CustomerAttributes.Female);
        var age = visit.Attributes & (CustomerAttributes.Child | CustomerAttributes.Elderly);
        var opposite = gender == CustomerAttributes.Male ? CustomerAttributes.Female : CustomerAttributes.Male;
        rules.Add(new SaleRestriction(visit.Attributes, ProductType.Water));
        rules.Add(new SaleRestriction(age == 0 ? gender | CustomerAttributes.Child : opposite | age, ProductType.Water));
        rules.Add(new SaleRestriction(opposite, ProductType.Water)); rules.Add(new SaleRestriction(visit.Attributes, ProductType.Medicine));
        visit.BeginOffer(); visit.SubmitOffer(606, new[] { new SaleItem(1, 1), new SaleItem(1, 2), new SaleItem(2, 1), new SaleItem(3, 2) });
        var result = visit.Result.Value; Assert.That(reads, Is.EqualTo(1)); Assert.That(result.RestrictionViolations.Count, Is.EqualTo(2));
        Assert.That(result.RestrictionViolations.Select(x => (x.ProductId, x.Quantity)), Is.EquivalentTo(new[] { (1u, 3), (2u, 1) }));
        rules.Clear(); products[1].ProductType = ProductType.Food;
        Assert.That(result.RestrictionViolations[0].Restriction.ProductType, Is.EqualTo(ProductType.Water));
        Assert.That(result.SaleIncome, Is.EqualTo(606)); Assert.That(result.ReputationDelta, Is.Zero);
        Assert.That(result.WereRestrictionsEvaluated, Is.True);
    }

    /// <summary>미연결·명시 빈목록·거부·legacy/default를 구분한다.</summary>
    [Test]
    public void RestrictionEvaluationStates()
    {
        foreach (bool connected in new[] { false, true })
        {
            var visit = generate(connected ? () => Array.Empty<SaleRestriction>() : null); visit.BeginOffer(); visit.SubmitOffer(101, new[] { new SaleItem(1, 1) });
            Assert.That(visit.Result.Value.WereRestrictionsEvaluated, Is.EqualTo(connected)); Assert.That(visit.Result.Value.RestrictionViolations, Is.Empty);
        }
        var rejected = generate(() => throw new Exception("must not read")); rejected.BeginOffer(); rejected.SubmitOffer(102, new[] { new SaleItem(1, 1) });
        Assert.That(rejected.Result.Value.WereRestrictionsEvaluated, Is.False); Assert.That(rejected.Result.Value.RestrictionViolations, Is.Empty);
        foreach (var result in new[] { default(TransactionResult), new TransactionResult(10, 2) })
        { Assert.That(result.WereRestrictionsEvaluated, Is.False); Assert.That(result.RestrictionViolations, Is.Empty); Assert.That(result.ReferenceTotal, Is.Null); Assert.That(result.SoldItems, Is.Empty); }
    }

    /// <summary>지침 경계 오류와 뒤늦은 원가 overflow도 실패 원자성을 유지한다.</summary>
    /// <param name="kind">오류 종류.</param>
    [TestCase("default"), TestCase("duplicate"), TestCase("null"), TestCase("throw"), TestCase("category"), TestCase("cost")]
    public void InvalidRestrictionsPermitValidRetry(string kind)
    {
        bool failing = true;
        var rule = new SaleRestriction(CustomerAttributes.Child, ProductType.Water);
        var visit = generate(() => !failing ? Array.Empty<SaleRestriction>() : kind == "throw" ? throw new InvalidOperationException("provider failure") : kind == "null" ? null : kind == "default" ? new[] { default(SaleRestriction) } : kind == "duplicate" ? new[] { rule, rule } : new[] { rule });
        visit.BeginOffer(); if (kind == "category") products[1].ProductType = (ProductType)99;
        if (kind == "cost") products[1].CostPrice = products[2].CostPrice = uint.MaxValue;
        Assert.Catch(() => visit.SubmitOffer(1, kind == "cost" ? new[] { new SaleItem(1, int.MaxValue), new SaleItem(2, int.MaxValue) } : new[] { new SaleItem(1, 1) }));
        assertUnpublished(visit); failing = false; products[1].ProductType = ProductType.Water; products[1].CostPrice = products[2].CostPrice = 50;
        Assert.That(visit.SubmitOffer(101, new[] { new SaleItem(1, 1) }), Is.True);
    }

    /// <summary>잘못된 조건을 생성자에서 거부한다.</summary>
    [Test]
    public void InvalidRestrictionValues()
    {
        foreach (var mask in new[] { CustomerAttributes.None, (CustomerAttributes)3, (CustomerAttributes)12, (CustomerAttributes)16 })
            Assert.Catch<ArgumentException>(() => new SaleRestriction(mask, ProductType.Water));
        Assert.Throws<ArgumentException>(() => new SaleRestriction(CustomerAttributes.Child, ProductType.None));
        Assert.Throws<ArgumentException>(() => new SaleRestriction(CustomerAttributes.Child, (ProductType)99));
    }

    /// <summary>공통 검사 설정을 만든다. 실제 PK에 등록하지 않는다.</summary>
    /// <param name="id">검사용 ID.</param><param name="type">성향 타입.</param><param name="product">선호 상품.</param>
    /// <returns>유효한 최소 설정.</returns>
    private static CustomerDispositionData makeConfig(uint id, CustomerDispositionType type, uint product) => new CustomerDispositionData
    {
        Idx = id, DispositionType = type, PreferredProductIdxs = new[] { product }, PreferredSelectionChance = 1000,
        MinProductKinds = 1, MaxProductKinds = 1, MinQuantity = 1, MaxQuantity = 1,
        EntryTextIdxs = new uint[] { 1 }, RegularSaleTextIdxs = new uint[] { 2 }, DiscountSaleTextIdxs = new uint[] { 3 }, ExploitativeSaleTextIdxs = new uint[] { 4 }, RejectTextIdxs = new uint[] { 5 }
    };

    /// <summary>현재 사례의 공개 생성 API를 호출한다.</summary>
    /// <param name="rules">선택 지침 공급자.</param><returns>생성 방문 또는 null.</returns>
    private CustomerVisit generate(Func<IReadOnlyList<SaleRestriction>> rules = null) => generator.Generate(new uint[] { 1 }, new[] { config }, products, getCurrentPrices: () => prices, getSaleRestrictions: rules);

    /// <summary>오류 이후 판정값이 하나도 공개되지 않았음을 검사한다.</summary>
    /// <param name="visit">검사 방문.</param>
    private static void assertUnpublished(CustomerVisit visit)
    { Assert.That(visit.Result, Is.Null); Assert.That(visit.AllowedTotal, Is.Null); Assert.That(visit.OfferedTotal, Is.Null); Assert.That(visit.Outcome, Is.EqualTo(CustomerTradeOutcome.None)); Assert.That(visit.State, Is.EqualTo(CustomerState.AwaitingOffer)); }
}
