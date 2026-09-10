using System;
using System.Collections.Generic;

/// <summary>검증된 구간 데이터와 거래 snapshot만으로 도덕성 변화량을 계산한다.</summary>
public sealed class MoralityCalculator
{
    private readonly IReadOnlyList<MoralityData> rows;

    /// <summary>로드 시 전체 구간 검증을 마친 행을 참조한다.</summary>
    /// <param name="rows">변경되지 않는 도덕성 규칙.</param>
    public MoralityCalculator(IReadOnlyList<MoralityData> rows)
    {
        this.rows = rows ?? throw new ArgumentNullException(nameof(rows));
    }

    /// <summary>제안 총액과 기준 총액을 나누지 않고 정확한 구간을 찾아 점수를 확정한다.</summary>
    /// <param name="dispositionType">거래 시점 성향 snapshot.</param>
    /// <param name="attributes">거래 시점 속성 snapshot.</param>
    /// <param name="isAccepted">거래 수락 snapshot.</param>
    /// <param name="offeredTotal">양수 제안 총액.</param>
    /// <param name="referenceTotal">양수 현재가 합계.</param>
    /// <returns>규칙이 없는 성향은 null, 있으면 유일한 구간 결과.</returns>
    /// <exception cref="InvalidOperationException">등록된 성향인데 일치 구간이 없거나 중복인 경우.</exception>
    public MoralityEvaluation? Calculate(CustomerDispositionType dispositionType, CustomerAttributes attributes,
        bool isAccepted, long offeredTotal, long referenceTotal)
    {
        CustomerProfileValidation.ValidateType(dispositionType);
        CustomerProfileValidation.ValidateCompleteAttributes(attributes);
        if (offeredTotal <= 0 || referenceTotal <= 0) throw new ArgumentOutOfRangeException(nameof(offeredTotal));
        bool hasDisposition = false;
        decimal offer = offeredTotal;
        decimal reference = referenceTotal;
        foreach (MoralityData row in this.rows)
        {
            if (row.CustomerDispositionType != dispositionType) continue;
            hasDisposition = true;
            if (row.IsAccepted != isAccepted || !contains(row, offer, reference)) continue;
            bool isAdult = (attributes & CustomerAttributes.Adult) != 0;
            return new MoralityEvaluation(row.Idx,
                isAdult ? row.AdultMoralityPoint : row.ChildElderlyMoralityPoint);
        }
        if (hasDisposition) throw new InvalidOperationException($"성향 {dispositionType}의 도덕성 구간이 누락되었습니다.");
        return null;
    }

    /// <summary>교차 곱으로 포함 경계를 비교한다.</summary>
    /// <param name="row">로드 시 검증한 가격 구간.</param>
    /// <param name="offer">최종 제시 총액.</param>
    /// <param name="reference">제출 시점 현재가 합계.</param>
    /// <returns>제시 비율이 해당 구간에 포함되면 true.</returns>
    private static bool contains(MoralityData row, decimal offer, decimal reference)
    {
        decimal scaled = offer * 1000m;
        decimal minimum = reference * row.OfferMinRate;
        if (scaled < minimum || (scaled == minimum && !row.IncludeMin)) return false;
        if (row.OfferMaxRate == 0) return true;
        decimal maximum = reference * row.OfferMaxRate;
        return scaled < maximum || (scaled == maximum && row.IncludeMax);
    }
}
