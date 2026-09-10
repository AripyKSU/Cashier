using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>명성 정산이 선호 행이 아니라 타입 공통 가격 규칙만 사용하는지 검증합니다.</summary>
public sealed class ReputationDispositionRulesTests
{
    /// <summary>같은 타입의 서로 다른 선호 상품 행은 하나의 대표 가격 규칙으로 축약합니다.</summary>
    [Test]
    public void SameTypeRowsWithSamePricingRulesAreAccepted()
    {
        CustomerDispositionData first = disposition(6001, ProductType.Water);
        CustomerDispositionData second = disposition(6002, ProductType.Tools);

        IReadOnlyDictionary<CustomerDispositionType, CustomerDispositionData> result =
            ReputationDispositionRules.BuildByType(new[] { first, second });

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[CustomerDispositionType.Normal], Is.SameAs(first));
    }

    /// <summary>같은 타입에 서로 다른 가격 규칙이 있으면 명성 매핑을 거부합니다.</summary>
    [Test]
    public void SameTypeRowsWithDifferentPricingRulesAreRejected()
    {
        CustomerDispositionData first = disposition(6001, ProductType.Water);
        CustomerDispositionData second = disposition(6002, ProductType.Tools);
        second.PriceTolerance = 1400;

        Assert.Throws<InvalidDataException>(() => ReputationDispositionRules.BuildByType(new[] { first, second }));
    }

    /// <summary>테스트용 정상 성향 행을 만듭니다.</summary>
    private static CustomerDispositionData disposition(uint id, ProductType preferredType)
    {
        return new CustomerDispositionData
        {
            Idx = id,
            DispositionType = CustomerDispositionType.Normal,
            PreferredProductTypes = new[] { preferredType },
            PreferredSelectionChance = 1000,
            MinProductKinds = 1,
            MaxProductKinds = 1,
            MinQuantity = 1,
            MaxQuantity = 1,
            PriceTolerance = 1100,
            RegularPriceMinRate = 1000,
            RegularPriceMaxRate = 1000,
            EntryTextIdxs = new uint[] { 1 },
            RegularSaleTextIdxs = new uint[] { 2 },
            DiscountSaleTextIdxs = new uint[] { 3 },
            ExploitativeSaleTextIdxs = new uint[] { 4 },
            RejectTextIdxs = new uint[] { 5 }
        };
    }
}
