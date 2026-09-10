using System;
using System.Collections.Generic;

/// <summary>구매 요청 결과와 별개인 현재 설비 표시 상태.</summary>
public enum FacilityDisplayState
{
    Available,
    InsufficientFunds,
    Pending,
    Active,
    FacilityDisplayState_End
}

/// <summary>한 설비의 표시용 불변 스냅샷. 비용과 날짜를 구매 입력으로 전달하지 않는다.</summary>
public readonly struct FacilityItemViewData
{
    /// <summary>구매 요청에 사용하는 유일한 식별자.</summary>
    public uint FacilityIdx { get; }
    /// <summary>TextData에서 조회한 이름.</summary>
    public string DisplayName { get; }
    /// <summary>CSV의 양수 구매 가격.</summary>
    public long PurchasePrice { get; }
    /// <summary>활성 상품 FK에서 조회한 해금 상품 이름 목록.</summary>
    public string UnlockProducts { get; }
    /// <summary>현재 보유·잔액·활성일로 계산한 화면 상태.</summary>
    public FacilityDisplayState State { get; }
    /// <summary>사용 가능 날짜의1기반 표시값. uint 경과일에1을 더해도 overflow하지 않는다.</summary>
    public ulong ActivationDisplayDay { get; }

    /// <summary>변환기가 확정한 표시값을 복사한다.</summary>
    /// <param name="facilityIdx">설비 PK.</param><param name="displayName">이름.</param>
    /// <param name="purchasePrice">구매 가격.</param><param name="unlockProducts">해금 상품 이름.</param>
    /// <param name="state">표시 상태.</param><param name="activationDisplayDay">1기반 사용 가능일.</param>
    public FacilityItemViewData(uint facilityIdx, string displayName, long purchasePrice, string unlockProducts,
        FacilityDisplayState state, ulong activationDisplayDay)
    {
        FacilityIdx = facilityIdx;
        DisplayName = displayName;
        PurchasePrice = purchasePrice;
        UnlockProducts = unlockProducts;
        State = state;
        ActivationDisplayDay = activationDisplayDay;
    }
}

/// <summary>같은 조회 시점의 잔액과 설비 목록을 보존한다.</summary>
public sealed class FacilityShopViewData
{
    /// <summary>조회 시점의 세션 잔액.</summary>
    public long CurrentBalance { get; }
    /// <summary>PK순 표시 목록. 외부 컬렉션 변경에 영향받지 않는다.</summary>
    public IReadOnlyList<FacilityItemViewData> Items { get; }

    /// <summary>조회값과 목록을 복사한다.</summary>
    /// <param name="currentBalance">현재 잔액.</param><param name="items">표시 목록.</param>
    /// <exception cref="ArgumentNullException">목록이 없음.</exception>
    public FacilityShopViewData(long currentBalance, IReadOnlyList<FacilityItemViewData> items)
    {
        CurrentBalance = currentBalance;
        Items = new List<FacilityItemViewData>(items ?? throw new ArgumentNullException(nameof(items))).AsReadOnly();
    }
}
