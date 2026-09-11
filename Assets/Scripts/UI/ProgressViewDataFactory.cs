using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 진행 도메인 데이터를 UI 전용 스냅샷과 표시 문자열로 변환합니다.
/// 게임 상태를 변경하지 않으며 Scene Controller의 표현 변환 책임을 분리합니다.
/// </summary>
public sealed class ProgressViewDataFactory
{
    private readonly CustomerCatalog customerCatalog;
    private readonly TextDataTable textData;
    private readonly IReadOnlyDictionary<uint, Sprite> productSprites;
    private readonly Func<uint, bool> isFacilityActive;

    /// <summary>검증된 카탈로그와 텍스트 테이블로 변환기를 생성합니다.</summary>
    /// <param name="customerCatalog">손님과 상품 데이터의 권위 카탈로그입니다.</param>
    /// <param name="textData">표시 문자열의 권위 테이블입니다.</param>
    /// <param name="productSprites">상품 ID별로 미리 로드된 표시 Sprite입니다.</param>
    /// <param name="isFacilityActive">세션의 현재 설비 활성 조회. 미연결이면 설비 상품을 잠근다.</param>
    /// <exception cref="ArgumentNullException">필수 데이터가 null인 경우 발생합니다.</exception>
    public ProgressViewDataFactory(
        CustomerCatalog customerCatalog,
        TextDataTable textData,
        IReadOnlyDictionary<uint, Sprite> productSprites,
        Func<uint, bool> isFacilityActive = null)
    {
        this.customerCatalog = customerCatalog ?? throw new ArgumentNullException(nameof(customerCatalog));
        this.textData = textData ?? throw new ArgumentNullException(nameof(textData));
        this.productSprites = productSprites ?? throw new ArgumentNullException(nameof(productSprites));
        this.isFacilityActive = isFacilityActive;
    }

    /// <summary>지정된 날짜에 판매 가능한 상품의 가격표 문자열을 만듭니다.</summary>
    /// <param name="day">1부터 시작하는 게임 날짜입니다.</param>
    /// <param name="dailyPrices">같은 세션·날짜의 확정 현재가입니다.</param>
    /// <returns>상품 식별자 순으로 구성된 가격표 문자열입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">날짜가 1 미만인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentNullException">현재가 snapshot이 없는 경우.</exception>
    /// <exception cref="InvalidOperationException">날짜 불일치 또는 상품 현재가 누락·0인 경우.</exception>
    public string CreatePriceListText(int day, DailyPriceState dailyPrices)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        if (dailyPrices == null) throw new ArgumentNullException(nameof(dailyPrices));
        if (dailyPrices.ElapsedDays != checked((uint)(day - 1)))
            throw new InvalidOperationException("가격표 날짜와 세션 현재가 날짜가 다릅니다.");

        IReadOnlyList<ProductData> products = CustomerProductAvailability.GetAvailableProducts(
            this.customerCatalog.Products.Rows,
            checked((uint)(day - 1)), this.isFacilityActive);
        var lines = new List<string> { "AVAILABLE PRODUCTS" };
        foreach (ProductData product in products)
        {
            string name = this.textData.Rows.TryGetValue(product.NameIdx, out TextData text)
                ? text.Text
                : $"Product {product.Idx}";
            if (!dailyPrices.Prices.TryGetValue(product.Idx, out uint price) || price == 0)
                throw new InvalidOperationException($"상품 {product.Idx}의 현재가가 준비되지 않았습니다.");
            lines.Add($"{name}  ·  {price:N0} G");
        }

        return string.Join("\n", lines);
    }

    /// <summary>같은 세션 조회값으로 설비 표시 상태를 계산한다. 구매나 날짜 변경은 수행하지 않는다.</summary>
    /// <param name="facilities">검증된 설비 원본.</param><param name="activationDays">보유 설비별 활성 경과일.</param>
    /// <param name="elapsedDays">현재 경과일.</param><param name="balance">현재 잔액.</param>
    /// <returns>PK순 불변 표시 스냅샷.</returns>
    /// <exception cref="ArgumentException">입력 누락·음수 잔액 또는 잘못된 설비 원본.</exception>
    /// <exception cref="InvalidOperationException">표시 이름 FK가 없음.</exception>
    public FacilityShopViewData CreateFacilityShopViewData(IReadOnlyDictionary<uint, FacilityData> facilities,
        IReadOnlyDictionary<uint, uint> activationDays, uint elapsedDays, long balance)
    {
        return this.CreateFacilityShopViewData(facilities, activationDays, 1, elapsedDays, balance);
    }

    /// <summary>세션에서 확정한 당일 상품·가격·지침으로 영업 시작 화면 스냅샷을 만듭니다.</summary>
    /// <param name="day">1부터 시작하는 게임 표시 일차입니다.</param>
    /// <param name="dailyPrices">같은 날짜에 확정된 당일 상품별 현재가입니다.</param>
    /// <param name="dailyGuidelines">같은 날짜에 확정된 최대 2개의 일일지침입니다.</param>
    /// <param name="canOpenBusiness">영업 시작 버튼 활성 여부입니다.</param>
    /// <returns>Presenter가 추가 조회 없이 렌더링할 수 있는 불변 스냅샷입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">날짜가 1 미만인 경우 발생합니다.</exception>
    /// <exception cref="ArgumentNullException">당일 가격 또는 지침 목록이 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">날짜·상품·가격·지침 참조가 서로 일치하지 않는 경우 발생합니다.</exception>
    public PreOpenGuidelineViewData CreatePreOpenGuidelineViewData(
        int day,
        DailyPriceState dailyPrices,
        IReadOnlyList<DailyGuideline> dailyGuidelines,
        bool canOpenBusiness)
    {
        if (day <= 0) throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        if (dailyPrices == null) throw new ArgumentNullException(nameof(dailyPrices));
        if (dailyGuidelines == null) throw new ArgumentNullException(nameof(dailyGuidelines));
        if (dailyPrices.ElapsedDays != checked((uint)(day - 1)))
            throw new InvalidOperationException("영업 시작 화면과 당일 가격의 날짜가 다릅니다.");
        if (dailyPrices.Prices.Count > 8)
            throw new InvalidOperationException("영업 시작 화면의 당일 상품은 최대 8개여야 합니다.");

        var products = new List<PriceGuideProductViewData>(dailyPrices.Prices.Count);
        foreach (KeyValuePair<uint, uint> price in dailyPrices.Prices.OrderBy(pair => pair.Key))
        {
            if (!this.customerCatalog.Products.Rows.TryGetValue(price.Key, out ProductData product) || product == null)
                throw new InvalidOperationException($"당일 상품 PK={price.Key}를 상품 데이터에서 찾을 수 없습니다.");
            if (price.Value == 0)
                throw new InvalidOperationException($"당일 상품 PK={price.Key}의 가격이 0입니다.");
            string name = this.getProductName(product);
            this.productSprites.TryGetValue(product.Idx, out Sprite icon);
            products.Add(new PriceGuideProductViewData(product.Idx, name, price.Value, icon));
        }

        var guidelines = new List<DailyGuidelineViewData>(dailyGuidelines.Count);
        foreach (DailyGuideline guideline in dailyGuidelines)
        {
            guideline.Validate();
            if (!dailyPrices.Prices.ContainsKey(guideline.TargetProductIdx))
                throw new InvalidOperationException($"일일지침 대상 상품 PK={guideline.TargetProductIdx}가 당일 상품에 없습니다.");
            ProductData product = this.customerCatalog.Products.Rows[guideline.TargetProductIdx];
            guidelines.Add(new DailyGuidelineViewData(guideline.Idx, this.formatGuideline(guideline, this.getProductName(product))));
        }

        return new PreOpenGuidelineViewData(
            day,
            products,
            guidelines,
            "일일 지침은 영업 시작 후 다시 확인할 수 없습니다.",
            canOpenBusiness);
    }

    /// <summary>최종 통합 정산 결과를 Presenter 전용 표시 스냅샷으로 변환합니다.</summary>
    /// <param name="day">정산 대상 일차입니다.</param>
    /// <param name="settlement">도메인에서 확정된 통합 정산 결과입니다.</param>
    /// <param name="reputationDelta">확정된 일일 명성 변화량입니다.</param>
    /// <param name="successfulSales">성공한 거래 수입니다.</param>
    /// <param name="refusedCustomers">거절된 거래 수입니다.</param>
    /// <param name="departedCustomers">이탈한 손님 수입니다.</param>
    /// <returns>UI가 금액을 다시 계산하지 않고 표시할 수 있는 정산 스냅샷입니다.</returns>
    public DailySettlementViewData CreateDailySettlementViewData(
        int day,
        DailySettlementResult settlement,
        int reputationDelta,
        int successfulSales,
        int refusedCustomers,
        int departedCustomers)
    {
        var violations = new List<SettlementGuidelineViolationViewData>(
            settlement.Aggregation.DailyGuidelineViolationSummaries.Count);
        foreach (DailyGuidelineViolationSummary summary in settlement.Aggregation.DailyGuidelineViolationSummaries)
        {
            if (!this.customerCatalog.Products.Rows.TryGetValue(summary.Guideline.TargetProductIdx, out ProductData product) ||
                product == null)
            {
                throw new InvalidOperationException(
                    $"정산 지침 대상 상품 PK={summary.Guideline.TargetProductIdx}를 상품 데이터에서 찾을 수 없습니다.");
            }
            violations.Add(new SettlementGuidelineViolationViewData(
                this.formatGuideline(summary.Guideline, this.getProductName(product)),
                summary.ViolationCount,
                summary.PenaltyAmount));
        }

        return new DailySettlementViewData(
            day,
            settlement.Aggregation.SaleIncome,
            settlement.BalanceAfterSettlement,
            reputationDelta,
            successfulSales,
            refusedCustomers,
            departedCustomers,
            settlement.MaintenanceAmount,
            settlement.GuidelinePenaltyAmount,
            violations,
            settlement.PreviousUnpaidAmount,
            settlement.TotalPaymentDue,
            settlement.PaidAmount,
            settlement.UnpaidAmount,
            settlement.GracePeriodEndDay,
            settlement.RemainingGraceDays,
            settlement.IsGameOverConditionMet);
    }

    /// <summary>현재 가게 단계까지 반영한 설비 상점 표시 snapshot을 만든다.</summary>
    /// <param name="facilities">검증된 설비 원본.</param>
    /// <param name="activationDays">보유 설비별 활성 경과일.</param>
    /// <param name="currentStoreStage">현재 세션 가게 단계.</param>
    /// <param name="elapsedDays">현재 경과일.</param>
    /// <param name="balance">현재 잔액.</param>
    /// <returns>PK순 불변 표시 스냅샷.</returns>
    /// <exception cref="ArgumentOutOfRangeException">단계 또는 잔액이 범위를 벗어남.</exception>
    public FacilityShopViewData CreateFacilityShopViewData(IReadOnlyDictionary<uint, FacilityData> facilities,
        IReadOnlyDictionary<uint, uint> activationDays, uint currentStoreStage, uint elapsedDays, long balance)
    {
        if (facilities == null) throw new ArgumentNullException(nameof(facilities));
        if (activationDays == null) throw new ArgumentNullException(nameof(activationDays));
        if (currentStoreStage < 1 || currentStoreStage > 3)
            throw new ArgumentOutOfRangeException(nameof(currentStoreStage));
        if (balance < 0) throw new ArgumentOutOfRangeException(nameof(balance));
        var items = new List<FacilityItemViewData>(facilities.Count);
        foreach (var pair in facilities.OrderBy(x => x.Key))
        {
            var facility = pair.Value;
            if (facility == null || pair.Key != facility.Idx) throw new ArgumentException("설비 키와 원본이 다릅니다.", nameof(facilities));
            facility.Validate();
            bool owned = activationDays.TryGetValue(facility.Idx, out uint activationDay);
            bool stageLocked = currentStoreStage < facility.RequiredStoreStage ||
                (facility.UpgradeKind == FacilityUpgradeKind.StoreStage &&
                 facility.TargetStoreStage != currentStoreStage + 1);
            FacilityDisplayState state;
            if (owned)
            {
                state = facility.UpgradeKind == FacilityUpgradeKind.StoreStage
                    ? FacilityDisplayState.OwnedStageUpgrade
                    : (activationDay <= elapsedDays ? FacilityDisplayState.Active : FacilityDisplayState.ActivationPending);
            }
            else if (stageLocked)
            {
                state = FacilityDisplayState.StageLocked;
            }
            else
            {
                state = balance >= facility.PurchasePrice
                    ? FacilityDisplayState.Purchasable
                    : FacilityDisplayState.InsufficientFunds;
            }
            string products = string.Join(", ", customerCatalog.Products.Rows.Values
                .Where(x => x.IsAvailable && x.RequiredFacilityIdx == facility.Idx).OrderBy(x => x.Idx)
                .Select(x => getFacilityText(x.NameIdx)));
            items.Add(new FacilityItemViewData(facility.Idx, getFacilityText(facility.NameIdx), facility.PurchasePrice,
                products, facility.UpgradeKind, facility.RequiredStoreStage, facility.EffectType,
                facility.TargetStoreStage, state, owned ? (ulong)activationDay + 1 : (ulong)elapsedDays + 2));
        }
        return new FacilityShopViewData(currentStoreStage, balance, items);
    }

    /// <summary>손님 방문 데이터를 UI 표현용 스냅샷으로 변환합니다.</summary>
    /// <param name="visit">현재 손님 방문입니다. null이면 빈 스냅샷을 반환합니다.</param>
    /// <returns>손님 외형, 대사와 장바구니를 담은 UI 스냅샷입니다.</returns>
    public CustomerViewData CreateCustomerViewData(CustomerVisit visit)
    {
        if (visit == null)
        {
            return CustomerViewData.Empty;
        }

        Color appearanceColor = Color.white;
        if (this.customerCatalog.Appearances.Rows.TryGetValue(
            visit.AppearanceIdx,
            out CustomerAppearanceData appearance))
        {
            appearanceColor = new Color32(
                appearance.ColorR,
                appearance.ColorG,
                appearance.ColorB,
                appearance.ColorA);
        }

        string dialogue = this.textData.Rows.TryGetValue(visit.FeedbackTextIdx, out TextData dialogueData)
            ? dialogueData.Text
            : string.Empty;
        var basket = new List<CustomerBasketItemViewData>(visit.Items.Count);
        foreach (CustomerOrderItem item in visit.Items)
        {
            string name = this.customerCatalog.Products.Rows.TryGetValue(item.ProductIdx, out ProductData product)
                && this.textData.Rows.TryGetValue(product.NameIdx, out TextData productText)
                ? productText.Text
                : $"Product {item.ProductIdx}";
            int unitPrice = item.UnitPrice > int.MaxValue ? int.MaxValue : (int)item.UnitPrice;
            if (!this.productSprites.TryGetValue(item.ProductIdx, out Sprite icon) || icon == null)
            {
                throw new InvalidOperationException($"상품 {item.ProductIdx}의 표시 Sprite가 준비되지 않았습니다.");
            }

            basket.Add(new CustomerBasketItemViewData(
                item.ProductIdx,
                name,
                item.Quantity,
                icon,
                unitPrice));
        }

        return new CustomerViewData(true, appearanceColor, null, dialogue, basket, visit.Attributes);
    }

    /// <summary>설비 표시 경계의 이름 FK 실패를 숨기지 않는다.</summary>
    /// <param name="idx">TextData PK.</param><returns>검증된 표시 문자열.</returns>
    /// <exception cref="InvalidOperationException">Text FK가 없거나 빈 문자열.</exception>
    private string getFacilityText(uint idx)
    {
        if (!textData.Rows.TryGetValue(idx, out var text) || string.IsNullOrWhiteSpace(text.Text))
            throw new InvalidOperationException($"설비 화면 TextData FK={idx} 참조 실패");
        return text.Text;
    }

    /// <summary>상품 표시 이름 FK를 조회합니다.</summary>
    /// <param name="product">이름을 조회할 상품 데이터입니다.</param>
    /// <returns>TextData에 등록된 상품 이름입니다.</returns>
    /// <exception cref="InvalidOperationException">상품 이름 FK가 없거나 빈 문자열인 경우 발생합니다.</exception>
    private string getProductName(ProductData product)
    {
        if (product == null || !this.textData.Rows.TryGetValue(product.NameIdx, out TextData text) ||
            string.IsNullOrWhiteSpace(text.Text))
            throw new InvalidOperationException($"상품 이름 TextData FK={product?.NameIdx ?? 0} 참조 실패");
        return text.Text;
    }

    /// <summary>구조화된 일일지침을 영업 시작 화면의 완성 문구로 변환합니다.</summary>
    /// <param name="guideline">표시할 검증된 일일지침입니다.</param>
    /// <param name="productName">대상 상품 표시 이름입니다.</param>
    /// <returns>손님 조건·상품·제한 유형이 포함된 문장입니다.</returns>
    private string formatGuideline(DailyGuideline guideline, string productName)
    {
        string target = guideline.RequiredAttributes == CustomerAttributes.None
            ? "모든"
            : string.Join(" ", new[]
            {
                (guideline.RequiredAttributes & CustomerAttributes.Male) != 0 ? "남자" :
                    (guideline.RequiredAttributes & CustomerAttributes.Female) != 0 ? "여자" : string.Empty,
                (guideline.RequiredAttributes & CustomerAttributes.Child) != 0 ? "아이" :
                    (guideline.RequiredAttributes & CustomerAttributes.Adult) != 0 ? "성인" :
                    (guideline.RequiredAttributes & CustomerAttributes.Elderly) != 0 ? "노인" : string.Empty
            }.Where(value => !string.IsNullOrEmpty(value)));

        return guideline.RuleType == DailyGuidelineRuleType.SaleProhibited
            ? $"{target} 손님에게는 {productName}을(를) 팔지 않는다."
            : $"{target} 손님에게는 {productName}을(를) 하나까지만 판다.";
    }
}
