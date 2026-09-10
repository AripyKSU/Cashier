using System;
using System.Collections.Generic;

/// <summary>
/// 기존 테스트·도구 호출을 새 selector–generator 경계로 연결하는 임시 호환 확장입니다.
/// </summary>
/// <remarks>
/// 제품 영업 흐름은 이 API를 사용하지 않고, 명성 기반 <see cref="CustomerCompositionSelector.SelectComposition"/>
/// 을 호출한 뒤 <see cref="CustomerGenerator.Generate"/>를 사용합니다.
/// </remarks>
public static class CustomerGeneratorCompatibility
{
    /// <summary>이전의 명성 미연결 생성 시그니처를 구성 선택과 방문 생성으로 변환합니다.</summary>
    /// <param name="generator">호환 생성기 인스턴스입니다.</param>
    /// <param name="appearanceIds">외형 후보 PK입니다.</param>
    /// <param name="dispositions">성향 후보입니다.</param>
    /// <param name="products">상품 사전입니다.</param>
    /// <param name="elapsedDays">경과 일수입니다.</param>
    /// <param name="getCurrentPrices">현재가 조회 callback입니다.</param>
    /// <param name="getSaleRestrictions">판매 지침 조회 callback입니다.</param>
    /// <param name="isFacilityActive">설비 활성 조회입니다.</param>
    /// <param name="moralityCalculator">제출 시 도덕성 평가기. null이면 미평가입니다.</param>
    /// <returns>판매 가능 상품이 없으면 null, 아니면 방문입니다.</returns>
    [Obsolete("CustomerCompositionSelector와 CustomerGenerator.Generate(composition, ...)를 사용하세요.")]
    public static CustomerVisit Generate(
        this CustomerGenerator generator,
        IReadOnlyList<uint> appearanceIds,
        IReadOnlyList<CustomerDispositionData> dispositions,
        IReadOnlyDictionary<uint, ProductData> products,
        uint elapsedDays = 0,
        Func<IReadOnlyDictionary<uint, uint>> getCurrentPrices = null,
        Func<IReadOnlyList<SaleRestriction>> getSaleRestrictions = null,
        Func<uint, bool> isFacilityActive = null,
        MoralityCalculator moralityCalculator = null)
    {
        if (generator == null)
            throw new ArgumentNullException(nameof(generator));
        if (getCurrentPrices == null)
            throw new ArgumentNullException(nameof(getCurrentPrices));

        CustomerCompositionSelector selector = new CustomerCompositionSelector(
            generator.CompatibilityRandom ?? new Random());
        IReadOnlyDictionary<uint, uint> currentPrices =
            getCurrentPrices() ?? throw new InvalidOperationException("현재가 조회 실패");
        CustomerComposition composition = selector.SelectCompositionUniform(
            appearanceIds,
            dispositions,
            products,
            currentPrices,
            elapsedDays,
            isFacilityActive);
        bool useCreationSnapshot = true;
        Func<IReadOnlyDictionary<uint, uint>> visitPriceProvider = () =>
        {
            if (useCreationSnapshot)
            {
                useCreationSnapshot = false;
                return currentPrices;
            }

            return getCurrentPrices();
        };
        return composition == null
            ? null
            : generator.Generate(composition, products, visitPriceProvider, getSaleRestrictions, moralityCalculator);
    }
}
