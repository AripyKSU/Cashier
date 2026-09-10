using System;
using System.Collections.Generic;

/// <summary>
/// 확정된 손님 구성 snapshot으로 CustomerVisit 객체를 생성하고 전달합니다.
/// </summary>
/// <remarks>
/// 외형·성향·속성·상품을 고르는 규칙은 <see cref="CustomerCompositionSelector"/>가 소유합니다.
/// 이 클래스는 선택 결과를 방문 객체와 제출 시 조회 callback으로 연결하는 경계만 담당합니다.
/// </remarks>
public sealed class CustomerGenerator
{
    // 구형 호출부의 seed 재현을 위한 호환 경로 전용 난수원입니다. 신규 경로는 보관하지 않습니다.
    private readonly Random compatibilityRandom;
    /// <summary>구성 snapshot을 방문 객체로 바꾸는 무상태 생성기입니다.</summary>
    public CustomerGenerator()
    {
    }

    /// <summary>
    /// 이전 호출자의 생성 코드가 컴파일되도록 남겨 둔 호환 생성자입니다.
    /// </summary>
    /// <param name="random">더 이상 저장하지 않는 난수원입니다.</param>
    /// <exception cref="ArgumentNullException">난수원이 null인 경우 발생합니다.</exception>
    [Obsolete("손님 선택은 CustomerCompositionSelector가 담당합니다. 기본 생성자를 사용하세요.")]
    public CustomerGenerator(Random random)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        this.compatibilityRandom = random;
    }

    /// <summary>구형 확장 호출부가 seed 재현을 유지하도록 난수원을 전달합니다.</summary>
    internal Random CompatibilityRandom => this.compatibilityRandom;

    /// <summary>
    /// 선택된 구성 snapshot을 CustomerVisit으로 생성합니다.
    /// </summary>
    /// <param name="composition">선택기가 확정한 불변 구성입니다.</param>
    /// <param name="products">상품 PK → 상품 데이터 사전입니다.</param>
    /// <param name="getCurrentPrices">제출 시 최신 현재가를 조회하는 callback입니다.</param>
    /// <param name="getSaleRestrictions">제출 시 판매 지침을 조회하는 callback입니다.</param>
    /// <returns>구성 snapshot을 복사한 방문 객체입니다.</returns>
    /// <exception cref="ArgumentNullException">필수 인수가 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">구성 상품 또는 현재가 참조가 잘못된 경우 발생합니다.</exception>
    public CustomerVisit Generate(
        CustomerComposition composition,
        IReadOnlyDictionary<uint, ProductData> products,
        Func<IReadOnlyDictionary<uint, uint>> getCurrentPrices,
        Func<IReadOnlyList<SaleRestriction>> getSaleRestrictions = null)
    {
        if (composition == null)
            throw new ArgumentNullException(nameof(composition));
        if (products == null)
            throw new ArgumentNullException(nameof(products));
        if (getCurrentPrices == null)
            throw new ArgumentNullException(nameof(getCurrentPrices));

        IReadOnlyDictionary<uint, uint> currentPrices =
            getCurrentPrices() ?? throw new InvalidOperationException("현재가 조회 실패");
        HashSet<uint> availableIds = new HashSet<uint>();
        foreach (uint productId in composition.AvailableProductIds)
        {
            if (productId == 0 || !availableIds.Add(productId) || !products.TryGetValue(productId, out ProductData product) ||
                product == null || product.Idx != productId)
                throw new ArgumentException("구성의 허용 상품 snapshot이 상품 사전과 일치하지 않습니다.", nameof(composition));
        }

        foreach (KeyValuePair<uint, ProductData> pair in products)
        {
            if (pair.Value == null || pair.Key != pair.Value.Idx)
                throw new ArgumentException("상품 사전 키와 PK가 다릅니다.", nameof(products));
            pair.Value.Validate();
            if (!currentPrices.TryGetValue(pair.Key, out uint price) || price == 0)
                throw new ArgumentException($"상품 PK={pair.Key}: 현재가 누락 또는 0", nameof(getCurrentPrices));
        }

        foreach (CustomerOrderItem item in composition.Items)
        {
            if (item == null || !availableIds.Contains(item.ProductIdx) || !products.ContainsKey(item.ProductIdx))
                throw new ArgumentException("구성 구매 항목이 허용 상품 snapshot과 일치하지 않습니다.", nameof(composition));
        }

        return new CustomerVisit(
            composition.AppearanceIdx,
            composition.DispositionIdx,
            new List<CustomerOrderItem>(composition.Items),
            composition.PriceTolerance,
            composition.EntryTextIdx,
            composition.RegularSaleTextIdx,
            composition.DiscountSaleTextIdx,
            composition.ExploitativeSaleTextIdx,
            composition.RejectTextIdx,
            products,
            getCurrentPrices,
            composition.DispositionType,
            composition.Attributes,
            composition.RegularPriceMinRate,
            composition.RegularPriceMaxRate,
            getSaleRestrictions,
            availableIds);
    }
}
