using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>일일지침이 최종 판매 목록에 적용하는 제한 종류.</summary>
public enum DailyGuidelineRuleType : uint
{
    None = 0,
    SaleProhibited = 1,
    QuantityLimited = 2,
    DailyGuidelineRuleType_End
}

/// <summary>하루 동안 고정되는 손님 조건·상품·판매 제한과 벌금의 불변 계약.</summary>
public readonly struct DailyGuideline
{
    private const CustomerAttributes SupportedTargetAttributes = CustomerAttributes.Male |
        CustomerAttributes.Female | CustomerAttributes.Child | CustomerAttributes.Elderly |
        CustomerAttributes.Adult;

    /// <summary>지침 템플릿의 DailyGuidelineData PK.</summary>
    public uint Idx { get; }
    /// <summary>판매 금지 또는 거래당 수량 제한.</summary>
    public DailyGuidelineRuleType RuleType { get; }
    /// <summary>AND로 검사할 성별·연령 속성. None은 모든 손님을 뜻한다.</summary>
    public CustomerAttributes RequiredAttributes { get; }
    /// <summary>제한할 ProductData PK.</summary>
    public uint TargetProductIdx { get; }
    /// <summary>거래당 판매 허용 수량. 판매 금지는 0이다.</summary>
    public int AllowedQuantity { get; }
    /// <summary>이 지침을 한 거래에서 위반했을 때의 벌금.</summary>
    public long PenaltyAmount { get; }

    /// <summary>검증된 템플릿과 런타임 대상 조건으로 하루 지침을 생성한다.</summary>
    /// <param name="idx">DailyGuidelineData PK.</param>
    /// <param name="ruleType">지침 제한 종류.</param>
    /// <param name="requiredAttributes">성별·연령 AND 조건. None은 전체.</param>
    /// <param name="targetProductIdx">ProductData PK.</param>
    /// <param name="allowedQuantity">거래당 허용 수량.</param>
    /// <param name="penaltyAmount">위반 1건의 양수 벌금.</param>
    /// <exception cref="ArgumentException">식별자, 규칙, 대상 조건 또는 수량 계약 오류.</exception>
    /// <exception cref="ArgumentOutOfRangeException">벌금이 양수가 아님.</exception>
    public DailyGuideline(uint idx, DailyGuidelineRuleType ruleType, CustomerAttributes requiredAttributes,
        uint targetProductIdx, int allowedQuantity, long penaltyAmount)
    {
        Idx = idx;
        RuleType = ruleType;
        RequiredAttributes = requiredAttributes;
        TargetProductIdx = targetProductIdx;
        AllowedQuantity = allowedQuantity;
        PenaltyAmount = penaltyAmount;
        Validate();
    }

    /// <summary>default struct를 포함해 일일지침 계약 전체를 검증한다.</summary>
    /// <exception cref="ArgumentException">식별자, 규칙, 대상 조건 또는 수량 계약 오류.</exception>
    /// <exception cref="ArgumentOutOfRangeException">벌금이 양수가 아님.</exception>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.DailyGuideline || Idx % 1000 == 0)
            throw new ArgumentException($"일일지침 idx={Idx}: DailyGuideline PK가 필요합니다.");
        if (RuleType == DailyGuidelineRuleType.None || RuleType == DailyGuidelineRuleType.DailyGuidelineRuleType_End ||
            !Enum.IsDefined(typeof(DailyGuidelineRuleType), RuleType))
            throw new ArgumentException($"일일지침 idx={Idx}: 유효한 규칙 유형이 필요합니다.");
        CustomerProfileValidation.ValidateAttributes(RequiredAttributes);
        if ((RequiredAttributes & ~SupportedTargetAttributes) != 0)
            throw new ArgumentException($"일일지침 idx={Idx}: 대상에는 성별·연령 속성만 사용할 수 있습니다.");
        if (Util.GetDataTableType(TargetProductIdx) != DataTableType.Product || TargetProductIdx % 1000 == 0)
            throw new ArgumentException($"일일지침 idx={Idx}: 유효한 ProductData PK가 필요합니다.");
        int expectedQuantity = RuleType == DailyGuidelineRuleType.SaleProhibited ? 0 : 1;
        if (AllowedQuantity != expectedQuantity)
            throw new ArgumentException($"일일지침 idx={Idx}: {RuleType} 허용 수량은 {expectedQuantity}이어야 합니다.");
        if (PenaltyAmount <= 0) throw new ArgumentOutOfRangeException(nameof(PenaltyAmount));
    }
}

/// <summary>성립한 한 거래에서 지침 하나를 위반한 사실과 벌금의 불변 기록.</summary>
public readonly struct DailyGuidelineViolation
{
    /// <summary>위반한 일일지침.</summary>
    public DailyGuideline Guideline { get; }
    /// <summary>판정에 사용한 손님의 완전한 속성.</summary>
    public CustomerAttributes CustomerAttributes { get; }
    /// <summary>최종 판매 목록의 대상 상품 수량.</summary>
    public int SoldQuantity { get; }
    /// <summary>해당 위반에 부과할 지침 고정 벌금.</summary>
    public long PenaltyAmount => Guideline.PenaltyAmount;

    /// <summary>검증된 지침 위반 결과를 생성한다.</summary>
    /// <param name="guideline">위반한 지침.</param>
    /// <param name="customerAttributes">판정한 손님의 완전한 속성.</param>
    /// <param name="soldQuantity">대상 물품의 양수 최종 판매 수량.</param>
    /// <exception cref="ArgumentException">지침·손님 속성 또는 위반 수량이 유효하지 않음.</exception>
    public DailyGuidelineViolation(DailyGuideline guideline, CustomerAttributes customerAttributes, int soldQuantity)
    {
        guideline.Validate();
        CustomerProfileValidation.ValidateCompleteAttributes(customerAttributes);
        if ((customerAttributes & guideline.RequiredAttributes) != guideline.RequiredAttributes)
            throw new ArgumentException("손님이 지침 대상 조건과 일치하지 않습니다.", nameof(customerAttributes));
        if (soldQuantity <= guideline.AllowedQuantity)
            throw new ArgumentException("허용 수량을 초과한 판매만 위반으로 기록할 수 있습니다.", nameof(soldQuantity));
        Guideline = guideline;
        CustomerAttributes = customerAttributes;
        SoldQuantity = soldQuantity;
    }
}

/// <summary>성립한 거래의 최종 판매 수량을 기준으로 일일지침별 위반을 독립 판정합니다.</summary>
public static class DailyGuidelineEvaluator
{
    /// <summary>손님 조건이 일치하고 대상 상품 수량이 허용량을 넘은 지침을 각각 한 건으로 반환합니다.</summary>
    /// <param name="guidelines">해당 거래에 적용할 당일 지침 snapshot입니다.</param>
    /// <param name="customerAttributes">거래 손님의 완전한 성별·연령·특수 속성입니다.</param>
    /// <param name="soldItems">중복 합산과 가격 검증을 마친 최종 판매 목록입니다.</param>
    /// <returns>입력 지침 순서를 유지하는 읽기 전용 위반 목록입니다.</returns>
    /// <exception cref="ArgumentNullException">지침 또는 판매 목록이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">지침·손님 속성·판매 항목이 유효하지 않거나 중복된 경우 발생합니다.</exception>
    public static IReadOnlyList<DailyGuidelineViolation> Evaluate(
        IReadOnlyList<DailyGuideline> guidelines,
        CustomerAttributes customerAttributes,
        IReadOnlyList<SoldItem> soldItems)
    {
        if (guidelines == null) throw new ArgumentNullException(nameof(guidelines));
        if (soldItems == null) throw new ArgumentNullException(nameof(soldItems));
        CustomerProfileValidation.ValidateCompleteAttributes(customerAttributes);

        var quantities = new Dictionary<uint, int>(soldItems.Count);
        foreach (SoldItem item in soldItems)
        {
            if (item.ProductId == 0 || item.Quantity <= 0 || item.UnitPrice == 0 || item.UnitCostPrice == 0)
                throw new ArgumentException("검증되지 않은 판매 항목이 포함됐습니다.", nameof(soldItems));
            if (!quantities.TryAdd(item.ProductId, item.Quantity))
                throw new ArgumentException($"상품 PK={item.ProductId}: 최종 판매 목록에 중복 상품이 포함됐습니다.", nameof(soldItems));
        }

        var seen = new HashSet<(uint, CustomerAttributes, uint)>();
        var violations = new List<DailyGuidelineViolation>();
        foreach (DailyGuideline guideline in guidelines)
        {
            guideline.Validate();
            if (!seen.Add((guideline.Idx, guideline.RequiredAttributes, guideline.TargetProductIdx)))
                throw new ArgumentException("동일한 일일지침이 중복되었습니다.", nameof(guidelines));
            if ((customerAttributes & guideline.RequiredAttributes) != guideline.RequiredAttributes)
                continue;
            if (!quantities.TryGetValue(guideline.TargetProductIdx, out int soldQuantity) ||
                soldQuantity <= guideline.AllowedQuantity)
                continue;

            // 한 거래가 여러 지침을 어기면 각 지침을 별도 위반으로 보존합니다.
            violations.Add(new DailyGuidelineViolation(guideline, customerAttributes, soldQuantity));
        }

        return violations.AsReadOnly();
    }
}

/// <summary>손님의 최초 요청 수량 중 일일지침에 따라 정상적으로 제외할 수 있는 수량을 계산합니다.</summary>
public static class DailyGuidelineExclusionAllowance
{
    /// <summary>대상 손님에게 적용되는 지침 중 가장 엄격한 허용 수량을 기준으로 정상 제외량을 반환합니다.</summary>
    /// <param name="guidelines">해당 거래에 적용할 당일 지침 snapshot입니다.</param>
    /// <param name="customerAttributes">거래 손님의 완전한 성별·연령·특수 속성입니다.</param>
    /// <param name="productIdx">제외량을 확인할 주문 상품 PK입니다.</param>
    /// <param name="requestedQuantity">손님이 최초 요청한 상품 수량입니다.</param>
    /// <returns>지침상 정상 제외로 인정되는 0 이상의 최대 수량입니다.</returns>
    /// <exception cref="ArgumentNullException">지침 목록이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentOutOfRangeException">상품 PK가 0이거나 요청 수량이 양수가 아닌 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">손님 속성 또는 지침이 유효하지 않은 경우 발생합니다.</exception>
    public static int GetAllowedExclusionQuantity(
        IReadOnlyList<DailyGuideline> guidelines,
        CustomerAttributes customerAttributes,
        uint productIdx,
        int requestedQuantity)
    {
        if (guidelines == null) throw new ArgumentNullException(nameof(guidelines));
        if (productIdx == 0) throw new ArgumentOutOfRangeException(nameof(productIdx));
        if (requestedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(requestedQuantity));
        CustomerProfileValidation.ValidateCompleteAttributes(customerAttributes);

        int allowedSaleQuantity = requestedQuantity;
        foreach (DailyGuideline guideline in guidelines)
        {
            guideline.Validate();
            if (guideline.TargetProductIdx != productIdx ||
                (customerAttributes & guideline.RequiredAttributes) != guideline.RequiredAttributes)
                continue;

            allowedSaleQuantity = Math.Min(allowedSaleQuantity, guideline.AllowedQuantity);
        }

        return requestedQuantity - allowedSaleQuantity;
    }
}

/// <summary>
/// 일차와 당일 등장 상품을 기준으로 서로 충돌하지 않는 일일지침을 무작위 생성합니다.
/// 대상 손님은 모든 손님을 70%로, 성별·연령 단일 속성을 각각 6%로 선택합니다.
/// </summary>
public sealed class DailyGuidelineGenerator
{
    private const int AllCustomerTargetWeight = 70;
    private const int SingleAttributeTargetWeight = 6;

    /// <summary>속성 후보와 상대 선택 가중치를 묶은 생성 전용 값입니다.</summary>
    private readonly struct TargetAttributeOption
    {
        /// <summary>지침에 기록할 손님 대상 조건입니다.</summary>
        public CustomerAttributes Attributes { get; }
        /// <summary>후보 선택 시 적용할 상대 가중치입니다.</summary>
        public int Weight { get; }

        /// <summary>속성 후보를 생성합니다.</summary>
        /// <param name="attributes">손님 대상 조건입니다.</param>
        /// <param name="weight">양의 상대 가중치입니다.</param>
        public TargetAttributeOption(CustomerAttributes attributes, int weight)
        {
            Attributes = attributes;
            Weight = weight;
        }
    }

    /// <summary>지침과 속성 후보의 상대 선택 가중치를 묶은 생성 전용 값입니다.</summary>
    private readonly struct WeightedGuidelineCandidate
    {
        /// <summary>생성된 지침입니다.</summary>
        public DailyGuideline Guideline { get; }
        /// <summary>속성 후보에서 상속한 상대 가중치입니다.</summary>
        public int Weight { get; }

        /// <summary>가중치가 적용된 지침 후보를 생성합니다.</summary>
        /// <param name="guideline">생성된 지침입니다.</param>
        /// <param name="weight">양의 상대 가중치입니다.</param>
        public WeightedGuidelineCandidate(DailyGuideline guideline, int weight)
        {
            Guideline = guideline;
            Weight = weight;
        }
    }

    // None은 모든 손님을 뜻하며, 나머지는 성별 또는 연령 중 하나만 지정한다.
    private static readonly TargetAttributeOption[] TargetAttributeOptions =
    {
        new TargetAttributeOption(CustomerAttributes.None, AllCustomerTargetWeight),
        new TargetAttributeOption(CustomerAttributes.Male, SingleAttributeTargetWeight),
        new TargetAttributeOption(CustomerAttributes.Female, SingleAttributeTargetWeight),
        new TargetAttributeOption(CustomerAttributes.Child, SingleAttributeTargetWeight),
        new TargetAttributeOption(CustomerAttributes.Adult, SingleAttributeTargetWeight),
        new TargetAttributeOption(CustomerAttributes.Elderly, SingleAttributeTargetWeight)
    };

    // 같은 생성기 인스턴스에서 날짜별 추첨 순서를 이어가는 난수원입니다.
    private readonly Random random;

    /// <summary>일일지침 생성에 사용할 난수원을 지정합니다.</summary>
    /// <param name="random">고정 seed 검증에도 사용할 수 있는 난수원입니다.</param>
    /// <exception cref="ArgumentNullException">난수원이 null인 경우 발생합니다.</exception>
    public DailyGuidelineGenerator(Random random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>경과 일수에 대응하는 일일지침 수를 반환합니다.</summary>
    /// <param name="elapsedDays">게임 시작일부터 경과한 일수입니다. 0은 1일차입니다.</param>
    /// <returns>1~9일차 0개, 10~19일차 1개, 20일차 이후 2개입니다.</returns>
    public static int GetGuidelineCount(uint elapsedDays)
    {
        if (elapsedDays >= 19) return 2;
        if (elapsedDays >= 9) return 1;
        return 0;
    }

    /// <summary>당일 등장 상품만 대상으로 지침 후보를 구성하고 충돌하지 않게 추첨합니다.</summary>
    /// <param name="elapsedDays">게임 시작일부터 경과한 일수입니다.</param>
    /// <param name="guidelineData">규칙 유형별 지침 설정 원본입니다.</param>
    /// <param name="dailyProductIds">당일 등장 상품 PK입니다.</param>
    /// <returns>해당 날짜 동안 유지할 읽기 전용 지침 목록입니다.</returns>
    /// <exception cref="ArgumentNullException">필수 입력이 null인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentException">지침 설정이나 당일 상품 PK가 유효하지 않은 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">필요한 개수만큼 충돌 없는 후보를 만들 수 없는 경우 발생합니다.</exception>
    public IReadOnlyList<DailyGuideline> Generate(
        uint elapsedDays,
        IReadOnlyDictionary<uint, DailyGuidelineData> guidelineData,
        IEnumerable<uint> dailyProductIds)
    {
        if (guidelineData == null) throw new ArgumentNullException(nameof(guidelineData));
        if (dailyProductIds == null) throw new ArgumentNullException(nameof(dailyProductIds));

        DailyGuidelineData[] templates = guidelineData.Values.OrderBy(data => data.Idx).ToArray();
        foreach (DailyGuidelineData template in templates)
        {
            if (template == null) throw new ArgumentException("일일지침 설정에 null 행이 포함됐습니다.", nameof(guidelineData));
            template.Validate();
        }

        uint[] productIds = dailyProductIds.OrderBy(productIdx => productIdx).ToArray();
        if (productIds.Any(productIdx => productIdx == 0) || productIds.Distinct().Count() != productIds.Length)
            throw new ArgumentException("당일 상품 PK는 0이 아닌 중복 없는 값이어야 합니다.", nameof(dailyProductIds));

        int guidelineCount = GetGuidelineCount(elapsedDays);
        if (guidelineCount == 0) return Array.Empty<DailyGuideline>();
        if (templates.Length == 0)
            throw new InvalidOperationException("일일지침 생성에 사용할 규칙 설정이 없습니다.");
        if (productIds.Length < guidelineCount)
            throw new InvalidOperationException($"일일지침 {guidelineCount}개에 사용할 서로 다른 당일 상품이 부족합니다.");

        var candidates = new List<WeightedGuidelineCandidate>();
        foreach (DailyGuidelineData template in templates)
            foreach (TargetAttributeOption target in TargetAttributeOptions)
                foreach (uint productIdx in productIds)
                    candidates.Add(new WeightedGuidelineCandidate(
                        template.CreateGuideline(target.Attributes, productIdx), target.Weight));

        var selected = new List<DailyGuideline>(guidelineCount);
        while (selected.Count < guidelineCount)
        {
            List<WeightedGuidelineCandidate> available = candidates.Where(candidate =>
                selected.All(existing => !conflicts(existing, candidate.Guideline))).ToList();
            if (available.Count == 0)
                throw new InvalidOperationException($"일일지침 {guidelineCount}개를 충돌 없이 생성할 수 없습니다.");

            DailyGuideline guideline = available[selectWeightedCandidateIndex(available)].Guideline;
            selected.Add(guideline);
            candidates.RemoveAll(candidate => sameCandidate(candidate.Guideline, guideline));
        }

        return selected.AsReadOnly();
    }

    /// <summary>후보의 상대 가중치에 따라 하나의 후보 인덱스를 선택합니다.</summary>
    /// <param name="candidates">하나 이상이며 양의 가중치를 가진 후보 목록입니다.</param>
    /// <returns>선택된 후보의 인덱스입니다.</returns>
    /// <exception cref="ArgumentException">후보가 없거나 가중치가 유효하지 않은 경우 발생합니다.</exception>
    private int selectWeightedCandidateIndex(IReadOnlyList<WeightedGuidelineCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            throw new ArgumentException("가중치 선택 후보가 필요합니다.", nameof(candidates));

        int totalWeight = 0;
        foreach (WeightedGuidelineCandidate candidate in candidates)
        {
            if (candidate.Weight <= 0)
                throw new ArgumentException("가중치는 양수여야 합니다.", nameof(candidates));
            totalWeight = checked(totalWeight + candidate.Weight);
        }

        int roll = this.random.Next(totalWeight);
        int cumulativeWeight = 0;
        for (int index = 0; index < candidates.Count; index++)
        {
            cumulativeWeight += candidates[index].Weight;
            if (roll < cumulativeWeight)
                return index;
        }

        // totalWeight와 누적값은 위 루프에서 동일한 후보 집합을 사용하므로 도달하지 않는다.
        throw new InvalidOperationException("가중치 후보 선택에 실패했습니다.");
    }

    /// <summary>한 물품에 복수 제한이 겹쳐 중복·포함·규칙 유형 충돌이 생기는지 검사합니다.</summary>
    /// <param name="left">이미 확정된 지침입니다.</param>
    /// <param name="right">추가할 후보 지침입니다.</param>
    /// <returns>같은 물품을 대상으로 하면 true입니다.</returns>
    private static bool conflicts(DailyGuideline left, DailyGuideline right)
    {
        return left.TargetProductIdx == right.TargetProductIdx;
    }

    /// <summary>후보 목록에서 이미 선택한 정확히 같은 조합을 식별합니다.</summary>
    /// <param name="left">비교할 후보입니다.</param>
    /// <param name="right">선택된 지침입니다.</param>
    /// <returns>템플릿·대상 조건·상품이 모두 같으면 true입니다.</returns>
    private static bool sameCandidate(DailyGuideline left, DailyGuideline right)
    {
        return left.Idx == right.Idx && left.RequiredAttributes == right.RequiredAttributes &&
            left.TargetProductIdx == right.TargetProductIdx;
    }
}

/// <summary>모든 필요 속성을 가진 손님에게 특정 분류 판매를 금지하는 조건값. 지침 ID·효력 수명은 소유하지 않는다.</summary>
public readonly struct SaleRestriction
{
    /// <summary>모두 일치해야 하는 손님 속성. None·배타 조합은 허용하지 않는다.</summary>
    public CustomerAttributes RequiredAttributes { get; }
    /// <summary>판매 제한 대상인 기존 상품 분류.</summary>
    public ProductType ProductType { get; }

    /// <summary>유효한 조건값을 복사한다.</summary>
    /// <param name="requiredAttributes">AND로 검사할 속성.</param>
    /// <param name="productType">None이 아닌 정의된 상품 분류.</param>
    /// <exception cref="ArgumentException">속성 또는 상품 분류가 유효하지 않음.</exception>
    public SaleRestriction(CustomerAttributes requiredAttributes, ProductType productType)
    {
        RequiredAttributes = requiredAttributes;
        ProductType = productType;
        Validate();
    }

    /// <summary>외부 입력 경계에서 default struct를 포함해 조건 유효성을 검사한다.</summary>
    /// <exception cref="ArgumentException">None·미정의·배타 속성 또는 상품 분류 오류.</exception>
    public void Validate()
    {
        CustomerProfileValidation.ValidateAttributes(RequiredAttributes);
        if (RequiredAttributes == CustomerAttributes.None || ProductType == ProductType.None ||
            !Enum.IsDefined(typeof(ProductType), ProductType))
            throw new ArgumentException("판매 제한에는 유효한 속성과 상품 분류가 필요합니다.");
    }
}

/// <summary>성립한 판매의 규칙-상품별 위반 기록. 이후 규칙·상품 데이터 수정과 무관한 값 복사다.</summary>
public readonly struct SaleRestrictionViolation
{
    /// <summary>위반한 조건값. 정식 지침 ID는 아직 연결하지 않는다.</summary>
    public SaleRestriction Restriction { get; }
    /// <summary>실제 판매한 상품 PK.</summary>
    public uint ProductId { get; }
    /// <summary>최종 판매 목록에서 중복을 합산한 수량.</summary>
    public int Quantity { get; }

    /// <summary>검증한 조건·상품·수량을 보존한다.</summary>
    /// <param name="restriction">일치한 판매 제한 조건.</param>
    /// <param name="productId">0이 아닌 판매 상품 PK.</param>
    /// <param name="quantity">양수 합산 수량.</param>
    /// <exception cref="ArgumentException">조건·상품·수량 오류.</exception>
    public SaleRestrictionViolation(SaleRestriction restriction, uint productId, int quantity)
    {
        restriction.Validate();
        if (productId == 0 || quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Restriction = restriction;
        ProductId = productId;
        Quantity = quantity;
    }
}
