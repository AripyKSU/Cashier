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
    private readonly DailyGuidelineDataTable guidelineTable;

    /// <summary>검증된 카탈로그와 텍스트 테이블로 변환기를 생성합니다.</summary>
    /// <param name="customerCatalog">손님과 상품 데이터의 권위 카탈로그입니다.</param>
    /// <param name="textData">표시 문자열의 권위 테이블입니다.</param>
    /// <param name="productSprites">상품 ID별로 미리 로드된 표시 Sprite입니다.</param>
    /// <param name="guidelineTable">선택적 당일 지침 데이터 테이블입니다.</param>
    /// <exception cref="ArgumentNullException">필수 데이터가 null인 경우 발생합니다.</exception>
    public ProgressViewDataFactory(
        CustomerCatalog customerCatalog,
        TextDataTable textData,
        IReadOnlyDictionary<uint, Sprite> productSprites,
        DailyGuidelineDataTable guidelineTable = null)
    {
        this.customerCatalog = customerCatalog ?? throw new ArgumentNullException(nameof(customerCatalog));
        this.textData = textData ?? throw new ArgumentNullException(nameof(textData));
        this.productSprites = productSprites ?? throw new ArgumentNullException(nameof(productSprites));
        this.guidelineTable = guidelineTable;
    }

    /// <summary>
    /// 지정된 날짜의 영업 전 일일 지침서 화면 데이터를 만듭니다.
    /// 추후 지침 CSV 데이터가 추가되면 지침 텍스트 조회 로직을 교체할 수 있도록 설계되었습니다.
    /// </summary>
    /// <param name="day">1부터 시작하는 게임 날짜입니다.</param>
    /// <returns>일일 지침서 화면 렌더링에 필요한 스냅샷입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">날짜가 1 미만인 경우 발생합니다.</exception>
    public PreOpenGuidelineViewData CreatePreOpenGuidelineViewData(int day)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        IReadOnlyList<ProductData> products = CustomerProductAvailability.GetAvailableProducts(
            this.customerCatalog.Products.Rows,
            checked((uint)(day - 1)));

        var productList = new List<PriceGuideProductViewData>();
        int maxSlots = Math.Min(4, products.Count);
        for (int i = 0; i < maxSlots; i++)
        {
            ProductData product = products[i];
            string name = this.textData.Rows.TryGetValue(product.NameIdx, out TextData text)
                ? text.Text
                : $"Product {product.Idx}";

            this.productSprites.TryGetValue(product.Idx, out Sprite icon);
            productList.Add(new PriceGuideProductViewData(product.Idx, name, product.BasePrice, icon));
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
    /// <returns>상품 식별자 순으로 구성된 가격표 문자열입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">날짜가 1 미만인 경우 발생합니다.</exception>
    public string CreatePriceListText(int day)
    {
        if (day <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "게임 날짜는 1 이상이어야 합니다.");
        }

        IReadOnlyList<ProductData> products = CustomerProductAvailability.GetAvailableProducts(
            this.customerCatalog.Products.Rows,
            checked((uint)(day - 1)));
        var lines = new List<string>();
        foreach (ProductData product in products)
        {
            string name = this.textData.Rows.TryGetValue(product.NameIdx, out TextData text)
                ? text.Text
                : $"Product {product.Idx}";
            lines.Add($"{name}  ·  {product.BasePrice:N0}원");
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
