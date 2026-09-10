using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 하루 동안 등장할 손님의 구성 snapshot을 선택하고 성별 교대 상태를 소유합니다.
/// </summary>
/// <remarks>
/// 명성 가중치로 성향 구성군을 고른 뒤 해당 군의 타입과 행을 선택합니다.
/// 외형은 아직 성별별 이미지 자산에 연결하지 않으며, 선택된 성별은 구성 snapshot의
/// <see cref="CustomerAttributes"/>로만 전달합니다.
/// </remarks>
public sealed class CustomerCompositionSelector
{
    /// <summary>방문 간 재사용할 난수원입니다.</summary>
    private readonly Random random;

    /// <summary>직전 방문에서 확정한 성별입니다. 첫 방문 전에는 값이 없습니다.</summary>
    private CustomerAttributes? previousGender;

    /// <summary>
    /// 성별 교대와 구성 선택에 사용할 난수원을 지정합니다.
    /// </summary>
    /// <param name="random">null이 아닌 난수원입니다.</param>
    /// <exception cref="ArgumentNullException">난수원이 null인 경우 발생합니다.</exception>
    public CustomerCompositionSelector(Random random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>다음 손님의 성별·연령·현재 특수 속성을 선택합니다.</summary>
    /// <returns>성별·연령·특수 속성이 모두 지정된 손님 속성입니다.</returns>
    public CustomerAttributes SelectAttributes()
    {
        CustomerAttributes selectedGender;
        CustomerAttributes attributes = this.selectAttributes(out selectedGender);
        this.previousGender = selectedGender;
        return attributes;
    }

    /// <summary>
    /// 하루 시작 명성 구간과 현재 설비 상태를 사용해 다음 손님 구성을 확정합니다.
    /// </summary>
    /// <param name="appearanceIds">외형 후보 PK입니다.</param>
    /// <param name="dispositions">검증된 성향 행 후보입니다.</param>
    /// <param name="products">상품 PK → 상품 데이터 사전입니다.</param>
    /// <param name="reputationBalance">하루 시작 명성에 대응하는 구성 가중치입니다.</param>
    /// <param name="currentPrices">구성 시점의 상품 현재가 snapshot입니다.</param>
    /// <param name="elapsedDays">게임 시작 후 경과 일수입니다.</param>
    /// <param name="isFacilityActive">설비 활성 조회입니다. null이면 설비 상품을 잠급니다.</param>
    /// <returns>판매 가능 상품이 없으면 null, 아니면 생성에 필요한 불변 구성입니다.</returns>
    /// <exception cref="ArgumentNullException">필수 입력이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">후보 PK·상품 FK·현재가·행 설정이 잘못된 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">가중치 또는 구성군 데이터가 일관되지 않는 경우 발생합니다.</exception>
    public CustomerComposition SelectComposition(
        IReadOnlyList<uint> appearanceIds,
        IReadOnlyList<CustomerDispositionData> dispositions,
        IReadOnlyDictionary<uint, ProductData> products,
        ReputationBalanceData reputationBalance,
        IReadOnlyDictionary<uint, uint> currentPrices,
        uint elapsedDays = 0,
        Func<uint, bool> isFacilityActive = null)
    {
        if (reputationBalance == null)
            throw new ArgumentNullException(nameof(reputationBalance));
        return this.selectComposition(appearanceIds, dispositions, products, currentPrices,
            reputationBalance, elapsedDays, isFacilityActive);
    }

    /// <summary>명성 데이터가 아직 연결되지 않은 호환 호출을 위해 타입을 균등 선택합니다.</summary>
    /// <param name="appearanceIds">외형 후보 PK입니다.</param>
    /// <param name="dispositions">검증된 성향 행 후보입니다.</param>
    /// <param name="products">상품 PK → 상품 데이터 사전입니다.</param>
    /// <param name="currentPrices">구성 시점의 상품 현재가 snapshot입니다.</param>
    /// <param name="elapsedDays">게임 시작 후 경과 일수입니다.</param>
    /// <param name="isFacilityActive">설비 활성 조회입니다.</param>
    /// <returns>판매 가능 상품이 없으면 null, 아니면 생성에 필요한 불변 구성입니다.</returns>
    /// <exception cref="ArgumentNullException">필수 입력이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">후보·상품·행 설정이 잘못된 경우 발생합니다.</exception>
    public CustomerComposition SelectCompositionUniform(
        IReadOnlyList<uint> appearanceIds,
        IReadOnlyList<CustomerDispositionData> dispositions,
        IReadOnlyDictionary<uint, ProductData> products,
        IReadOnlyDictionary<uint, uint> currentPrices,
        uint elapsedDays = 0,
        Func<uint, bool> isFacilityActive = null)
    {
        return this.selectComposition(appearanceIds, dispositions, products, currentPrices,
            null, elapsedDays, isFacilityActive);
    }

    /// <summary>입력 후보를 검증하고 실제 구성 snapshot을 만드는 내부 흐름입니다.</summary>
    private CustomerComposition selectComposition(
        IReadOnlyList<uint> appearanceIds,
        IReadOnlyList<CustomerDispositionData> dispositions,
        IReadOnlyDictionary<uint, ProductData> products,
        IReadOnlyDictionary<uint, uint> currentPrices,
        ReputationBalanceData reputationBalance,
        uint elapsedDays,
        Func<uint, bool> isFacilityActive)
    {
        if (appearanceIds == null || appearanceIds.Count == 0)
            throw new ArgumentException("외형 후보가 필요합니다.", nameof(appearanceIds));
        if (dispositions == null || dispositions.Count == 0)
            throw new ArgumentException("성향 후보가 필요합니다.", nameof(dispositions));
        if (products == null)
            throw new ArgumentNullException(nameof(products));
        if (currentPrices == null)
            throw new ArgumentNullException(nameof(currentPrices));

        List<uint> sortedAppearanceIds = validateAppearanceIds(appearanceIds);
        List<CustomerDispositionData> sortedDispositions = validateDispositions(dispositions, products);
        Dictionary<uint, ProductData> availableProducts = validateAndFilterProducts(products, currentPrices,
            elapsedDays, isFacilityActive);
        if (availableProducts.Count == 0)
            return null;

        CustomerDispositionData disposition = reputationBalance == null
            ? selectUniformDisposition(sortedDispositions)
            : selectWeightedDisposition(sortedDispositions, reputationBalance);
        CustomerAttributes selectedGender;
        CustomerAttributes attributes = this.selectAttributes(out selectedGender);
        uint appearanceIdx = sortedAppearanceIds[this.random.Next(sortedAppearanceIds.Count)];
        List<CustomerOrderItem> items = selectItems(disposition, availableProducts, currentPrices);
        CustomerComposition composition = new CustomerComposition(
            appearanceIdx,
            disposition.Idx,
            disposition.DispositionType,
            attributes,
            items,
            selectText(disposition.EntryTextIdxs),
            selectText(disposition.RegularSaleTextIdxs),
            selectText(disposition.DiscountSaleTextIdxs),
            selectText(disposition.ExploitativeSaleTextIdxs),
            selectText(disposition.RejectTextIdxs),
            disposition.PriceTolerance,
            disposition.RegularPriceMinRate,
            disposition.RegularPriceMaxRate,
            availableProducts.Keys.OrderBy(id => id));

        // 구성 snapshot 생성까지 성공한 경우에만 다음 방문의 성별 기준을 갱신합니다.
        this.previousGender = selectedGender;
        return composition;
    }

    /// <summary>외형 PK를 정렬하고 중복·0을 거부합니다.</summary>
    private static List<uint> validateAppearanceIds(IReadOnlyList<uint> appearanceIds)
    {
        HashSet<uint> seen = new HashSet<uint>();
        List<uint> result = new List<uint>(appearanceIds.Count);
        foreach (uint id in appearanceIds)
        {
            if (id == 0 || !seen.Add(id))
                throw new ArgumentException("외형 ID는 0이 아니며 고유해야 합니다.", nameof(appearanceIds));
            result.Add(id);
        }

        result.Sort();
        return result;
    }

    /// <summary>성향 행을 PK 순서로 정렬하고 상품 FK를 확인합니다.</summary>
    private static List<CustomerDispositionData> validateDispositions(
        IReadOnlyList<CustomerDispositionData> dispositions,
        IReadOnlyDictionary<uint, ProductData> products)
    {
        HashSet<uint> seen = new HashSet<uint>();
        List<CustomerDispositionData> result = new List<CustomerDispositionData>(dispositions.Count);
        foreach (CustomerDispositionData data in dispositions)
        {
            if (data == null || data.Idx == 0 || !seen.Add(data.Idx))
                throw new ArgumentException("성향 ID 또는 구매 설정 범위가 잘못되었습니다.", nameof(dispositions));
            data.ValidatePurchaseSettings();
            foreach (uint idx in data.PreferredProductIdxs)
            {
                if (!products.ContainsKey(idx))
                    throw new ArgumentException($"성향 {data.Idx}: preferred_product_idxs FK={idx} 참조 실패", nameof(dispositions));
            }

            result.Add(data);
        }

        result.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        return result;
    }

    /// <summary>상품 데이터와 현재가를 검증하고 날짜·설비 필터를 적용합니다.</summary>
    private static Dictionary<uint, ProductData> validateAndFilterProducts(
        IReadOnlyDictionary<uint, ProductData> products,
        IReadOnlyDictionary<uint, uint> currentPrices,
        uint elapsedDays,
        Func<uint, bool> isFacilityActive)
    {
        Dictionary<uint, ProductData> availableProducts = new Dictionary<uint, ProductData>();
        foreach (KeyValuePair<uint, ProductData> pair in products)
        {
            if (pair.Value == null || pair.Key != pair.Value.Idx)
                throw new ArgumentException("상품 사전 키와 PK가 다릅니다.", nameof(products));
            pair.Value.Validate();
            if (!currentPrices.TryGetValue(pair.Key, out uint price)) continue;
            if (price == 0)
                throw new ArgumentException($"상품 PK={pair.Key}: 현재가가 0입니다.", nameof(currentPrices));
            if (!CustomerProductAvailability.IsAvailable(pair.Value, elapsedDays, isFacilityActive))
                throw new ArgumentException($"상품 PK={pair.Key}: 등장할 수 없는 상품의 현재가가 포함됐습니다.", nameof(currentPrices));
            availableProducts.Add(pair.Key, pair.Value);
        }

        foreach (uint productIdx in currentPrices.Keys)
        {
            if (!products.ContainsKey(productIdx))
                throw new ArgumentException($"상품 PK={productIdx}: 상품 원본에 없는 현재가입니다.", nameof(currentPrices));
        }

        return availableProducts;
    }

    /// <summary>명성 연결 전 호환 경로의 타입·행 균등 선택입니다.</summary>
    private CustomerDispositionData selectUniformDisposition(IReadOnlyList<CustomerDispositionData> dispositions)
    {
        List<CustomerDispositionType> types = dispositions.Select(data => data.DispositionType)
            .Distinct().OrderBy(type => type).ToList();
        CustomerDispositionType selectedType = types[this.random.Next(types.Count)];
        CustomerDispositionData[] rows = dispositions.Where(data => data.DispositionType == selectedType)
            .OrderBy(data => data.Idx).ToArray();
        return rows[this.random.Next(rows.Length)];
    }

    /// <summary>명성 구성군을 가중치로 고른 뒤 군 내부 타입·행을 균등 선택합니다.</summary>
    private CustomerDispositionData selectWeightedDisposition(
        IReadOnlyList<CustomerDispositionData> dispositions,
        ReputationBalanceData balance)
    {
        if (balance.NormalWeight < 0 || balance.WealthyWeight < 0 || balance.HastyWeight < 0 || balance.SpecialWeight < 0 ||
            balance.NormalWeight + balance.WealthyWeight + balance.HastyWeight + balance.SpecialWeight != 1000)
            throw new InvalidDataException("명성 손님 구성 가중치 합은 1000이어야 합니다.");
        if (balance.SpecialWeight > 0)
            throw new InvalidDataException("특수 손님 구성군의 타입 매핑이 아직 정의되지 않았습니다.");

        List<CustomerDispositionData> normal = dispositions.Where(data =>
            data.DispositionType == CustomerDispositionType.Normal ||
            data.DispositionType == CustomerDispositionType.PriceSensitive).ToList();
        List<CustomerDispositionData> wealthy = dispositions.Where(data =>
            data.DispositionType == CustomerDispositionType.Wealthy).ToList();
        List<CustomerDispositionData> hasty = dispositions.Where(data =>
            data.DispositionType == CustomerDispositionType.Hasty).ToList();
        if (balance.NormalWeight > 0 && normal.Count == 0)
            throw new InvalidDataException("일반 손님 구성군에 사용할 성향 행이 없습니다.");
        if (balance.WealthyWeight > 0 && wealthy.Count == 0)
            throw new InvalidDataException("Wealthy 손님 구성군에 사용할 성향 행이 없습니다.");
        if (balance.HastyWeight > 0 && hasty.Count == 0)
            throw new InvalidDataException("Hasty 손님 구성군에 사용할 성향 행이 없습니다.");

        int roll = this.random.Next(1000);
        if (roll < balance.NormalWeight)
            return selectTypeThenRow(normal);
        roll -= balance.NormalWeight;
        if (roll < balance.WealthyWeight)
            return selectTypeThenRow(wealthy);
        roll -= balance.WealthyWeight;
        if (roll < balance.HastyWeight)
            return selectTypeThenRow(hasty);

        throw new InvalidDataException("명성 손님 구성군 추첨 결과가 매핑되지 않았습니다.");
    }

    /// <summary>한 구성군 안에서 타입을 먼저 균등 선택하고 해당 타입의 행을 선택합니다.</summary>
    private CustomerDispositionData selectTypeThenRow(IReadOnlyList<CustomerDispositionData> candidates)
    {
        List<CustomerDispositionType> types = candidates.Select(data => data.DispositionType)
            .Distinct().OrderBy(type => type).ToList();
        CustomerDispositionType selectedType = types[this.random.Next(types.Count)];
        CustomerDispositionData[] rows = candidates.Where(data => data.DispositionType == selectedType)
            .OrderBy(data => data.Idx).ToArray();
        return rows[this.random.Next(rows.Length)];
    }

    /// <summary>선호 타입·개별 선호를 합친 후보에서 구매 항목을 확정합니다.</summary>
    private List<CustomerOrderItem> selectItems(
        CustomerDispositionData disposition,
        IReadOnlyDictionary<uint, ProductData> availableProducts,
        IReadOnlyDictionary<uint, uint> currentPrices)
    {
        HashSet<ProductType> preferredTypes = new HashSet<ProductType>(disposition.PreferredProductTypes);
        HashSet<uint> preferredProductIds = new HashSet<uint>(disposition.PreferredProductIdxs);
        List<uint> preferred = new List<uint>();
        List<uint> others = new List<uint>();
        foreach (KeyValuePair<uint, ProductData> pair in availableProducts)
        {
            if (preferredTypes.Contains(pair.Value.ProductType) || preferredProductIds.Contains(pair.Key))
                preferred.Add(pair.Key);
            else
                others.Add(pair.Key);
        }

        preferred.Sort();
        others.Sort();
        int maxKinds = Math.Min(disposition.MaxProductKinds, availableProducts.Count);
        int minKinds = Math.Min(disposition.MinProductKinds, maxKinds);
        int count = this.random.Next(minKinds, maxKinds + 1);
        List<CustomerOrderItem> items = new List<CustomerOrderItem>(count);
        for (int i = 0; i < count; i++)
        {
            bool usePreferred = preferred.Count > 0 &&
                (others.Count == 0 || this.random.Next(1000) < disposition.PreferredSelectionChance);
            List<uint> candidates = usePreferred ? preferred : others;
            if (candidates.Count == 0)
                candidates = preferred.Count > 0 ? preferred : others;
            int index = this.random.Next(candidates.Count);
            uint productId = candidates[index];
            items.Add(new CustomerOrderItem(productId,
                this.random.Next(disposition.MinQuantity, disposition.MaxQuantity + 1),
                currentPrices[productId]));
            candidates.RemoveAt(index);
        }

        return items;
    }

    /// <summary>대사 후보에서 하나의 TextData FK를 선택합니다.</summary>
    private uint selectText(IReadOnlyList<uint> textIds)
    {
        return textIds[this.random.Next(textIds.Count)];
    }

    /// <summary>성별 교대와 연령·특수 속성을 선택하되 호출자가 확정할 때까지 상태를 바꾸지 않습니다.</summary>
    private CustomerAttributes selectAttributes(out CustomerAttributes selectedGender)
    {
        selectedGender = this.selectNextGender();
        CustomerAttributes age = this.random.Next(3) switch
        {
            0 => CustomerAttributes.Adult,
            1 => CustomerAttributes.Child,
            _ => CustomerAttributes.Elderly
        };
        CustomerAttributes attributes = selectedGender | age | CustomerAttributes.Normal;
        CustomerProfileValidation.ValidateCompleteAttributes(attributes);
        return attributes;
    }

    /// <summary>첫 방문은 무작위로, 이후 방문은 직전 성별의 반대로 선택합니다.</summary>
    private CustomerAttributes selectNextGender()
    {
        if (!this.previousGender.HasValue)
            return this.random.Next(2) == 0 ? CustomerAttributes.Male : CustomerAttributes.Female;

        return this.previousGender.Value == CustomerAttributes.Male
            ? CustomerAttributes.Female
            : CustomerAttributes.Male;
    }
}
