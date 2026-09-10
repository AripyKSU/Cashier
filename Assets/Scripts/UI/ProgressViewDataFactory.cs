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
    private readonly IReadOnlyDictionary<uint, Sprite> topViewSprites;
    private readonly IReadOnlyDictionary<uint, Sprite> appearanceSprites;
    private readonly DailyGuidelineDataTable guidelineTable;
    private readonly Func<uint, bool> isFacilityActive;

    /// <summary>검증된 카탈로그와 텍스트 테이블로 변환기를 생성합니다.</summary>
    /// <param name="customerCatalog">손님과 상품 데이터의 권위 카탈로그입니다.</param>
    /// <param name="textData">표시 문자열의 권위 테이블입니다.</param>
    /// <param name="productSprites">상품 ID별로 미리 로드된 표시 Sprite입니다.</param>
    /// <param name="isFacilityActive">세션의 현재 설비 활성 조회. 미연결이면 설비 상품을 잠근다.</param>
    /// <param name="topViewSprites">상품별 탑뷰 Sprite. 손님 화면 생성 시 필수다.</param>
    /// <param name="appearanceSprites">외형 PK별 미리 로드된 Sprite. 손님 화면 생성 시 필수다.</param>
    /// <param name="guidelineTable">선택적 당일 지침 데이터 테이블입니다.</param>
    /// <exception cref="ArgumentNullException">필수 데이터가 null인 경우 발생합니다.</exception>
    public ProgressViewDataFactory(
        CustomerCatalog customerCatalog,
        TextDataTable textData,
        IReadOnlyDictionary<uint, Sprite> productSprites, Func<uint, bool> isFacilityActive = null,
        IReadOnlyDictionary<uint, Sprite> topViewSprites = null, IReadOnlyDictionary<uint, Sprite> appearanceSprites = null, DailyGuidelineDataTable guidelineTable = null)
    {
        this.customerCatalog = customerCatalog ?? throw new ArgumentNullException(nameof(customerCatalog));
        this.textData = textData ?? throw new ArgumentNullException(nameof(textData));
        this.productSprites = productSprites ?? throw new ArgumentNullException(nameof(productSprites));
        this.isFacilityActive = isFacilityActive;
        this.topViewSprites = topViewSprites;
        this.appearanceSprites = appearanceSprites;
        this.guidelineTable = guidelineTable;
    }

    /// <summary>지침 테이블을 포함하는 이전 시그니처 호환용 생성자입니다.</summary>
    public ProgressViewDataFactory(
        CustomerCatalog customerCatalog,
        TextDataTable textData,
        IReadOnlyDictionary<uint, Sprite> productSprites,
        DailyGuidelineDataTable guidelineTable)
        : this(customerCatalog, textData, productSprites, isFacilityActive: null, guidelineTable: guidelineTable)
    {
    }

    /// <summary>
    /// 지정된 날짜의 영업 전 일일 지침서 화면 데이터를 만듭니다.
    /// 추후 지침 CSV 데이터가 추가되면 지침 텍스트 조회 로직을 교체할 수 있도록 설계되었습니다.
    /// </summary>
    /// <param name="day">1부터 시작하는 게임 날짜입니다.</param>
    /// <param name="dailyPrices">표시일과 일치하는 세션 현재가.</param>
    /// <returns>일일 지침서 화면 렌더링에 필요한 스냅샷입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">날짜가 1 미만인 경우 발생합니다.</exception>
    public PreOpenGuidelineViewData CreatePreOpenGuidelineViewData(int day, DailyPriceState dailyPrices)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        if (dailyPrices == null) throw new ArgumentNullException(nameof(dailyPrices));
        if (dailyPrices.ElapsedDays != checked((uint)(day - 1)))
            throw new InvalidOperationException("지침 가격표와 세션 현재가 날짜가 다릅니다.");
        IReadOnlyList<ProductData> products = CustomerProductAvailability.GetAvailableProducts(
            this.customerCatalog.Products.Rows,
            checked((uint)(day - 1)),
            this.isFacilityActive);

        var productList = new List<PriceGuideProductViewData>();
        int maxSlots = Math.Min(4, products.Count);
        for (int i = 0; i < maxSlots; i++)
        {
            ProductData product = products[i];
            string name = this.textData.Rows.TryGetValue(product.NameIdx, out TextData text)
                ? text.Text
                : $"Product {product.Idx}";

            this.productSprites.TryGetValue(product.Idx, out Sprite icon);
            if (!dailyPrices.Prices.TryGetValue(product.Idx, out uint price) || price == 0)
                throw new InvalidOperationException($"상품 {product.Idx}의 현재가가 준비되지 않았습니다.");
            productList.Add(new PriceGuideProductViewData(product.Idx, name, price, icon));
        }

        string heading = "영업 전, 가격을 기억하세요";
        string ruleTitle = "오늘의 지침";
        string ruleContent = "제한 없음.";
        string restriction = "영업이 시작되면 가격표를 다시 볼 수 없습니다.";
        string recheck = "당일 지침은 영업 중에도 다시 확인할 수 있습니다.";

        DailyGuidelineDataTable targetGuidelineTable = this.guidelineTable
            ?? (DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<DailyGuidelineDataTable>(DataTableType.DailyGuideline) : null);

        if (targetGuidelineTable != null && targetGuidelineTable.TryGetByDay(checked((uint)day), out DailyGuidelineData guideline))
        {
            if (this.textData.Rows.TryGetValue(guideline.NameIdx, out TextData titleData))
            {
                ruleTitle = titleData.Text;
            }
            if (this.textData.Rows.TryGetValue(guideline.DescriptionIdx, out TextData descData))
            {
                ruleContent = descData.Text;
            }
        }

        return new PreOpenGuidelineViewData(
            day,
            heading,
            ruleTitle,
            ruleContent,
            productList,
            restriction,
            recheck);
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

        if (this.appearanceSprites == null || !this.appearanceSprites.TryGetValue(visit.AppearanceIdx, out Sprite appearanceSprite) || appearanceSprite == null)
            throw new InvalidOperationException($"외형 {visit.AppearanceIdx}의 Sprite가 준비되지 않았습니다.");

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
                unitPrice, getTopViewSprite(item.ProductIdx)));
        }

        return new CustomerViewData(true, Color.white, appearanceSprite, dialogue, basket, visit.Attributes);
    }

    /// <summary>명시적으로 준비한 탑뷰 이미지만 사용한다. 누락을 기본 이미지로 숨기지 않는다.</summary>
    /// <param name="productIdx">상품 PK.</param><returns>탑뷰 Sprite.</returns>
    /// <exception cref="InvalidOperationException">로드 결과 누락.</exception>
    private Sprite getTopViewSprite(uint productIdx)
    {
        if (topViewSprites == null || !topViewSprites.TryGetValue(productIdx, out var sprite) || sprite == null)
            throw new InvalidOperationException($"상품 {productIdx}의 탑뷰 Sprite가 준비되지 않았습니다.");
        return sprite;
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
}
