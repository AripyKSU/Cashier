using System;
using System.Collections.Generic;

/// <summary>
/// 손님 생성과 상품 표시가 공유하는 날짜별 판매 가능 상품 규칙을 제공합니다.
/// </summary>
public static class CustomerProductAvailability
{
    /// <summary>
    /// 지정된 경과 일수에 상품을 판매할 수 있는지 확인합니다.
    /// </summary>
    /// <param name="product">확인할 상품 데이터입니다.</param>
    /// <param name="elapsedDays">게임 시작일부터 경과한 0 이상의 일수입니다.</param>
    /// <returns>활성 상품이며 등장일이 지난 경우 true입니다.</returns>
    public static bool IsAvailable(ProductData product, uint elapsedDays)
    {
        return product != null && product.IsAvailable && product.AvailableDay <= elapsedDays;
    }

    /// <summary>
    /// 지정된 경과 일수에 판매 가능한 상품을 식별자 순서로 반환합니다.
    /// </summary>
    /// <param name="products">검증된 상품 사전입니다.</param>
    /// <param name="elapsedDays">게임 시작일부터 경과한 0 이상의 일수입니다.</param>
    /// <returns>판매 가능한 상품 목록입니다.</returns>
    /// <exception cref="ArgumentNullException">상품 사전이 null인 경우 발생합니다.</exception>
    public static IReadOnlyList<ProductData> GetAvailableProducts(
        IReadOnlyDictionary<uint, ProductData> products,
        uint elapsedDays)
    {
        if (products == null)
        {
            throw new ArgumentNullException(nameof(products));
        }

        var availableProducts = new List<ProductData>();
        foreach (KeyValuePair<uint, ProductData> entry in products)
        {
            if (entry.Value == null || entry.Key != entry.Value.Idx)
            {
                throw new ArgumentException("상품 사전 키와 PK가 다릅니다.", nameof(products));
            }

            if (IsAvailable(entry.Value, elapsedDays))
            {
                availableProducts.Add(entry.Value);
            }
        }

        availableProducts.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        return availableProducts.AsReadOnly();
    }
}
