/// <summary>
/// 한 영업일의 시작부터 정산 완료까지의 진행 상태를 나타냅니다.
/// </summary>
public enum DayProgressState
{
    /// <summary>하루 진행을 시작하기 전 외부 시스템을 확인하는 상태입니다.</summary>
    Initializing,

    /// <summary>가격표와 확장 가능한 영업 전 정보를 확인하는 상태입니다.</summary>
    PreOpen,

    /// <summary>제한시간 안에서 손님 거래를 진행하는 상태입니다.</summary>
    Operating,

    /// <summary>거래 결과를 표시하고 확인을 기다리는 상태입니다.</summary>
    TransactionResult,

    /// <summary>시간 만료 후 마지막 거래를 마무리하는 상태입니다.</summary>
    Closing,

    /// <summary>일일 재정 집계를 확정하고 결과 확인을 기다리는 상태입니다.</summary>
    Settlement,

    /// <summary>정산 결과 확인이 끝난 하루 완료 상태입니다.</summary>
    Completed
}
