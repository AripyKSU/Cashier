using System;
using System.Collections.Generic;

/// <summary>
/// 제출 시 확정한 불변 거래 결과. 판정 완료와 재정 반영 완료는 별개입니다.
/// </summary>
public readonly struct TransactionResult
{
    /// <summary>외부 목록 변경으로부터 분리한 판매 snapshot.</summary>
    private readonly IReadOnlyList<SoldItem> soldItems;
    /// <summary>외부 목록 변경으로부터 분리한 위반 snapshot.</summary>
    private readonly IReadOnlyList<SaleRestrictionViolation> restrictionViolations;
    /// <summary>거래 판정. 상세 없는 재정 호환 생성자는 None.</summary>
    public CustomerTradeOutcome Outcome { get; }
    /// <summary>제출한 총액. 재정 호환 생성자는 미확정 null.</summary>
    public long? OfferedTotal { get; }
    /// <summary>최종 목록 현재가 합계. 재정 호환 생성자는 미확정 null.</summary>
    public long? ReferenceTotal { get; }
    /// <summary>수락한 판매 목록. 거부·재정 호환·default 결과는 빈 목록.</summary>
    public IReadOnlyList<SoldItem> SoldItems => soldItems ?? Array.Empty<SoldItem>();
    /// <summary>수락한 목록의 원가 합계. 실제 차감은 별도 단계다.</summary>
    public long CostTotal { get; }
    /// <summary>지침 공급 목록을 실제 평가했는지 여부. 미연결·거부·legacy·default는 false.</summary>
    public bool WereRestrictionsEvaluated { get; }
    /// <summary>조건-상품별 위반값. 미평가는 빈 목록이며 정상 위반은 결제를 취소하지 않는다.</summary>
    public IReadOnlyList<SaleRestrictionViolation> RestrictionViolations => restrictionViolations ?? Array.Empty<SaleRestrictionViolation>();
    /// <summary>완료된 거래의 판매 수입.</summary>
    public long SaleIncome { get; }
    /// <summary>완료된 거래의 명성 변화량. 지침 위반으로 자동 계산하지 않는다.</summary>
    public int ReputationDelta { get; }

    /// <summary>최종 검증 목록에서 기준액·원가를 합산하고 거래 결과를 생성한다.</summary>
    /// <param name="outcome">방문이 계산한 판정.</param>
    /// <param name="offeredTotal">양수 제시액.</param>
    /// <param name="items">중복 합산과 단가 검증을 마친 목록.</param>
    /// <param name="wereRestrictionsEvaluated">수락 경로에서 지침 공급 목록을 평가했는지 여부.</param>
    /// <param name="violations">검증된 조건-상품별 위반값.</param>
    /// <exception cref="OverflowException">합계 범위 초과.</exception>
    internal TransactionResult(CustomerTradeOutcome outcome, long offeredTotal, IReadOnlyList<SoldItem> items,
        bool wereRestrictionsEvaluated, IReadOnlyList<SaleRestrictionViolation> violations)
    {
        var copy = new List<SoldItem>(items);
        long reference = 0, cost = 0;
        foreach (var item in copy)
        {
            reference = checked(reference + (long)item.UnitPrice * item.Quantity);
            cost = checked(cost + (long)item.UnitCostPrice * item.Quantity);
        }
        Outcome = outcome; OfferedTotal = offeredTotal; ReferenceTotal = reference; ReputationDelta = 0;
        bool accepted = outcome != CustomerTradeOutcome.PaymentRefused;
        SaleIncome = accepted ? offeredTotal : 0;
        CostTotal = accepted ? cost : 0;
        soldItems = accepted ? copy.AsReadOnly() : (IReadOnlyList<SoldItem>)Array.Empty<SoldItem>();
        WereRestrictionsEvaluated = accepted && wereRestrictionsEvaluated;
        restrictionViolations = WereRestrictionsEvaluated ? new List<SaleRestrictionViolation>(violations).AsReadOnly() : Array.Empty<SaleRestrictionViolation>();
    }

    /// <summary>
    /// 상세 내역 없는 재정 단독 검사 호환 결과를 생성합니다. 새 거래 경로에서는 사용하지 않습니다.
    /// </summary>
    /// <param name="saleIncome">완료된 거래의 판매 수입입니다.</param>
    /// <param name="reputationDelta">완료된 거래로 발생한 명성 변화량입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">판매 수입이 음수인 경우 발생합니다.</exception>
    public TransactionResult(long saleIncome, int reputationDelta)
    {
        if (saleIncome < 0)
        {
            // 판매 거래 결과에서 지출이 전달되는 잘못된 입력을 차단합니다.
            throw new ArgumentOutOfRangeException(nameof(saleIncome), saleIncome, "판매 수입은 음수일 수 없습니다.");
        }

        this.SaleIncome = saleIncome;
        this.ReputationDelta = reputationDelta;
        Outcome = CustomerTradeOutcome.None;
        OfferedTotal = null;
        ReferenceTotal = null;
        CostTotal = 0;
        WereRestrictionsEvaluated = false;
        restrictionViolations = Array.Empty<SaleRestrictionViolation>();
        soldItems = Array.Empty<SoldItem>(); // 재정 단독 검사 호환. 상세 상품·판정을 만들어내지 않는다.
    }
}
