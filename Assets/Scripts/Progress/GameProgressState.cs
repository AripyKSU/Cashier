/// <summary>
/// 여러 영업일에 걸친 게임 전체 진행 상태를 나타냅니다.
/// </summary>
public enum GameProgressState
{
    /// <summary>전체 진행이 아직 시작되지 않은 상태입니다.</summary>
    Initializing,

    /// <summary>현재 날짜의 하루 진행이 실행 중인 상태입니다.</summary>
    DayInProgress,

    /// <summary>상납금 납부 결과를 결정해야 하는 상태입니다.</summary>
    Maintenance,

    /// <summary>상납금 부족 등 게임 규칙에 의해 실패한 상태입니다.</summary>
    Failed,

    /// <summary>게임의 최종 목표를 달성한 상태입니다.</summary>
    Completed
}
