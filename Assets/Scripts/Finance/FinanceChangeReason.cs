using System;

/// <summary>
/// 잔액이 변경된 업무 사유를 구분합니다.
/// </summary>
[Serializable]
public enum FinanceChangeReason
{
    None = 0,

    Sale = 1,

    Maintenance = 2,

    /// <summary>다음 영업일 상품 해금을 위한 독립 설비 구매.</summary>
    FacilityPurchase = 3,

    /// <summary>유지비와 당일 패널티를 합친 일일 통합 정산 납부.</summary>
    DailySettlement = 4
}
