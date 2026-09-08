using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>외부에서 받은 유효 후보로 일반 손님의 방문과 구매 목록을 생성한다.</summary>
public sealed class CustomerGenerator
{
    /// <summary>호출자가 소유하는 난수원. 고정 seed로 생성 결과를 재현할 수 있다.</summary>
    private readonly Random random;

    /// <summary>방문 간 재사용할 난수원을 지정한다.</summary>
    /// <param name="random">null이 아닌 난수원.</param>
    /// <exception cref="ArgumentNullException">난수원이 null인 경우.</exception>
    public CustomerGenerator(Random random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>외형·타입·타입 내 설정과 독립 속성을 균등 선정하고 중복 없는 상품·수량을 확정한다.</summary>
    /// <param name="appearanceIds">외부에서 리소스 참조를 검증한 외형 ID 후보.</param>
    /// <param name="dispositions">외부에서 상품군 참조를 검증한 성향 후보.</param>
    /// <param name="products">상품 ID → 상품 데이터. 비활성·미등장 상품은 제외한다.</param>
    /// <param name="elapsedDays">게임 시작 후 경과 일수. 0은 시작일.</param>
    /// <param name="getCurrentPrices">현재 가격표 조회 함수. 생성 시 희망 목록 표시, 제출 시 최신 가격 확정에 각각 사용한다.</param>
    /// <returns>판매 가능 상품이 없으면 null. 나머지는 확정된 방문 데이터.</returns>
    /// <exception cref="ArgumentException">필수 후보 누락, 0·중복 ID 또는 잘못된 설정 범위.</exception>
    public CustomerVisit Generate(IReadOnlyList<uint> appearanceIds,
        IReadOnlyList<CustomerDispositionData> dispositions,
        IReadOnlyDictionary<uint, ProductData> products, uint elapsedDays = 0, Func<IReadOnlyDictionary<uint, uint>> getCurrentPrices = null)
    {
        if (getCurrentPrices == null) throw new ArgumentNullException(nameof(getCurrentPrices));
        var currentPrices = getCurrentPrices() ?? throw new InvalidOperationException("현재가 조회 실패");
        if (appearanceIds == null || appearanceIds.Count == 0)
            throw new ArgumentException("외형 후보가 필요합니다.", nameof(appearanceIds));
        if (dispositions == null || dispositions.Count == 0)
            throw new ArgumentException("성향 후보가 필요합니다.", nameof(dispositions));
        if (products == null)
            throw new ArgumentNullException(nameof(products));

        var ids = new HashSet<uint>();
        foreach (uint id in appearanceIds)
            if (id == 0 || !ids.Add(id))
                throw new ArgumentException("외형 ID는 0이 아니며 고유해야 합니다.", nameof(appearanceIds));
        ids.Clear();
        foreach (var data in dispositions)
        {
            if (data == null || data.Idx == 0 || !ids.Add(data.Idx))
                throw new ArgumentException("성향 ID 또는 구매 설정 범위가 잘못되었습니다.", nameof(dispositions));
            data.ValidatePurchaseSettings();
            foreach (uint idx in data.PreferredProductIdxs)
                if (!products.ContainsKey(idx))
                    throw new ArgumentException($"성향 {data.Idx}: preferred_product_idxs FK={idx} 참조 실패", nameof(dispositions));
        }
        var availableProducts = new Dictionary<uint, ProductData>();
        foreach (var product in products)
        {
            if (product.Value == null || product.Key != product.Value.Idx)
                throw new ArgumentException("상품 사전 키와 PK가 다릅니다.", nameof(products));
            product.Value.Validate();
            if (currentPrices != null && (!currentPrices.TryGetValue(product.Key, out uint price) || price == 0))
                throw new ArgumentException($"상품 PK={product.Key}: 현재가 누락 또는 0", nameof(currentPrices));
            if (product.Value.IsAvailable && product.Value.AvailableDay <= elapsedDays)
                availableProducts.Add(product.Key, product.Value);
        }

        // 정상적인 판매 후보 부재는 잘못된 설정과 달리 방문을 만들지 않는다.
        if (availableProducts.Count == 0) return null;

        uint appearanceIdx = appearanceIds[random.Next(appearanceIds.Count)];
        // 타입별 설정 개수가 달라도 타입 출현율은 같으며 입력 행 순서에 의존하지 않는다.
        var groups = dispositions.GroupBy(x => x.DispositionType).OrderBy(x => x.Key).ToArray();
        var candidatesByType = groups[random.Next(groups.Length)].OrderBy(x => x.Idx).ToArray();
        var disposition = candidatesByType[random.Next(candidatesByType.Length)];
        var attributes = random.Next(2) == 0 ? CustomerAttributes.Male : CustomerAttributes.Female;
        int age = random.Next(3);
        if (age == 1) attributes |= CustomerAttributes.Child;
        else if (age == 2) attributes |= CustomerAttributes.Elderly;
        var preferredCategories = new HashSet<ProductType>(disposition.PreferredProductTypes);
        var preferredProducts = new HashSet<uint>(disposition.PreferredProductIdxs);
        var preferred = new List<uint>();
        var others = new List<uint>();
        foreach (var product in availableProducts)
            (preferredCategories.Contains(product.Value.ProductType) || preferredProducts.Contains(product.Key) ? preferred : others).Add(product.Key);
        // Dictionary 삽입 순서가 달라도 같은 seed와 후보 집합으로 같은 상품을 고른다.
        preferred.Sort();
        others.Sort();
        int maxKinds = Math.Min(disposition.MaxProductKinds, availableProducts.Count);
        int minKinds = Math.Min(disposition.MinProductKinds, maxKinds);
        int count = random.Next(minKinds, maxKinds + 1);
        var items = new List<CustomerOrderItem>(count);
        for (int i = 0; i < count; i++)
        {
            // 한쪽 후보 소진 시 남은 쪽을 사용하므로 재추첨 루프가 필요 없다.
            bool usePreferred = preferred.Count > 0 &&
                (others.Count == 0 || random.Next(1000) < disposition.PreferredSelectionChance);
            var candidates = usePreferred ? preferred : others;
            int index = random.Next(candidates.Count);
            items.Add(new CustomerOrderItem(candidates[index],
                random.Next(disposition.MinQuantity, disposition.MaxQuantity + 1),
                currentPrices[candidates[index]]));
            candidates.RemoveAt(index);
        }
        return new CustomerVisit(appearanceIdx, disposition.Idx, items, disposition.PriceTolerance,
            disposition.EntryTextIdxs[random.Next(disposition.EntryTextIdxs.Count)],
            disposition.RegularSaleTextIdxs[random.Next(disposition.RegularSaleTextIdxs.Count)],
            disposition.DiscountSaleTextIdxs[random.Next(disposition.DiscountSaleTextIdxs.Count)],
            disposition.ExploitativeSaleTextIdxs[random.Next(disposition.ExploitativeSaleTextIdxs.Count)],
            disposition.RejectTextIdxs[random.Next(disposition.RejectTextIdxs.Count)], products, getCurrentPrices,
            disposition.DispositionType, attributes);
    }
}
