using System;

/// <summary>
/// 거래 결과와 손님 성향의 가격 규칙을 사용해 명성용 거래 등급을 판정합니다.
/// </summary>
public static class ReputationTransactionClassifier
{
    /// <summary>
    /// 확정 거래를 할인·정가·적당한 폭리·큰 폭리로 분류합니다.
    /// </summary>
    /// <param name="transactionResult">손님 성향과 가격 snapshot을 가진 거래 결과입니다.</param>
    /// <param name="dispositionData">거래 손님 성향의 가격 규칙입니다.</param>
    /// <returns>명성 정산에 사용할 거래 등급입니다.</returns>
    /// <exception cref="ArgumentException">거래와 성향이 일치하지 않거나 가격 규칙이 잘못된 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">가격 합계가 없는 legacy 결과인 경우 발생합니다.</exception>
    public static ReputationTransactionGrade Classify(TransactionResult transactionResult, CustomerDispositionData dispositionData)
    {
        if (dispositionData == null) throw new ArgumentNullException(nameof(dispositionData));
        CustomerProfileValidation.ValidateType(transactionResult.DispositionType);
        CustomerProfileValidation.ValidateType(dispositionData.DispositionType);
        if (transactionResult.DispositionType != dispositionData.DispositionType)
            throw new ArgumentException("거래 결과와 성향 데이터의 타입이 일치하지 않습니다.", nameof(dispositionData));
        if (dispositionData.RegularPriceMinRate <= 0 || dispositionData.RegularPriceMinRate > 1000 ||
            dispositionData.RegularPriceMaxRate < 1000 || dispositionData.PriceTolerance <= 0)
            throw new ArgumentException("성향 가격 규칙의 범위가 잘못되었습니다.", nameof(dispositionData));
        if (!transactionResult.OfferedTotal.HasValue || !transactionResult.ReferenceTotal.HasValue ||
            transactionResult.OfferedTotal.Value <= 0 || transactionResult.ReferenceTotal.Value <= 0)
            throw new InvalidOperationException("가격 합계가 없는 거래 결과는 명성 등급을 판정할 수 없습니다.");

        if (transactionResult.Outcome == CustomerTradeOutcome.PaymentRefused)
            return ReputationTransactionGrade.ExtremeMarkup;

        long offeredTotal = transactionResult.OfferedTotal.Value;
        long referenceTotal = transactionResult.ReferenceTotal.Value;
        if ((decimal)offeredTotal * 1000m < (decimal)referenceTotal * dispositionData.RegularPriceMinRate)
            return ReputationTransactionGrade.Discount;
        if ((decimal)offeredTotal * 1000m > (decimal)referenceTotal * dispositionData.RegularPriceMaxRate)
            return ReputationTransactionGrade.ModerateMarkup;
        return ReputationTransactionGrade.Regular;
    }
}
