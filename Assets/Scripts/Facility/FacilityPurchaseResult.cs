/// <summary>설비 구매 요청의 정상 처리 결과. 입력·재진입 오류는 예외로 구분한다.</summary>
public enum FacilityPurchaseStatus
{
    None = 0,
    Purchased = 1,
    AlreadyOwned = 2,
    InsufficientFunds = 3,
    FacilityPurchaseStatus_End
}

/// <summary>구매 성공 여부와 이번 지출·활성일을 보존하는 불변 결과.</summary>
public readonly struct FacilityPurchaseResult
{
    /// <summary>정상 성공 또는 실패 사유.</summary>
    public FacilityPurchaseStatus Status { get; }
    /// <summary>요청한 설비 PK.</summary>
    public uint FacilityIdx { get; }
    /// <summary>이번 요청으로 지불한 금액. 정상 실패는0.</summary>
    public long PaidAmount { get; }
    /// <summary>보유 설비의 활성 경과일. 미보유 잔액 부족은null.</summary>
    public uint? ActivationDay { get; }

    /// <summary>서비스가 확정한 정상 결과를 보관한다.</summary>
    /// <param name="status">구매 결과.</param>
    /// <param name="facilityIdx">설비 PK.</param>
    /// <param name="paidAmount">이번 지출.</param>
    /// <param name="activationDay">활성 경과일 또는null.</param>
    internal FacilityPurchaseResult(FacilityPurchaseStatus status, uint facilityIdx, long paidAmount, uint? activationDay)
    {
        Status = status;
        FacilityIdx = facilityIdx;
        PaidAmount = paidAmount;
        ActivationDay = activationDay;
    }
}
