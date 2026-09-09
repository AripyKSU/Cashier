using System;
using System.Collections.Generic;
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
        IReadOnlyDictionary<uint, Sprite> productSprites, Func<uint, bool> isFacilityActive = null)
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

        return new CustomerViewData(true, appearanceColor, null, dialogue, basket);
    }
}
