using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 명성 거래 판정에서 성향 행을 타입별 가격 규칙으로 축약합니다.
/// </summary>
/// <remarks>
/// 한 타입에 여러 행이 존재할 수 있지만 명성 정산은 선호 상품이나 대사와 무관하게
/// 거래 타입만 보므로, 같은 타입의 모든 행은 동일한 가격 규칙을 가져야 합니다.
/// </remarks>
public static class ReputationDispositionRules
{
    /// <summary>
    /// 성향 행을 거래 타입별 대표 규칙으로 변환합니다.
    /// </summary>
    /// <param name="dispositions">검증할 성향 행 목록입니다.</param>
    /// <returns>타입별 가격 판정에 사용할 대표 성향 행 사전입니다.</returns>
    /// <exception cref="ArgumentNullException">입력 목록이 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">행이 null이거나 같은 타입의 가격 규칙이 다른 경우 발생합니다.</exception>
    public static IReadOnlyDictionary<CustomerDispositionType, CustomerDispositionData> BuildByType(
        IEnumerable<CustomerDispositionData> dispositions)
    {
        if (dispositions == null)
        {
            throw new ArgumentNullException(nameof(dispositions));
        }

        Dictionary<CustomerDispositionType, CustomerDispositionData> result =
            new Dictionary<CustomerDispositionType, CustomerDispositionData>();
        foreach (CustomerDispositionData data in dispositions)
        {
            if (data == null)
            {
                throw new InvalidDataException("손님 성향 데이터에 null 행이 있습니다.");
            }

            try
            {
                data.ValidatePurchaseSettings();
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"손님 성향 PK={data.Idx}의 구매 설정이 잘못되었습니다.", exception);
            }

            if (!result.TryGetValue(data.DispositionType, out CustomerDispositionData representative))
            {
                result.Add(data.DispositionType, data);
                continue;
            }

            if (representative.PriceTolerance != data.PriceTolerance ||
                representative.MinimumPriceTolerance != data.MinimumPriceTolerance ||
                representative.RegularPriceMinRate != data.RegularPriceMinRate ||
                representative.RegularPriceMaxRate != data.RegularPriceMaxRate)
            {
                throw new InvalidDataException(
                    $"손님 성향 타입 {data.DispositionType}의 가격 규칙이 행마다 다릅니다.");
            }
        }

        return result;
    }
}
