/// <summary>영업 진행을 시계와 시간대 배경에 표시할 때 사용하는 공통 게임 시각이다. 실제 영업 지속시간은 DayProgress가 소유한다.</summary>
public static class BusinessHours
{
    /// <summary>영업 시작 시.</summary>
    public const int OpenHour = 9;
    /// <summary>영업 마감 시.</summary>
    public const int CloseHour = 21;
    /// <summary>시작 시각의 누적 분.</summary>
    public const int OpenMinutes = OpenHour * 60;
    /// <summary>마감 시각의 누적 분.</summary>
    public const int CloseMinutes = CloseHour * 60;
    /// <summary>영업 중 표시할 게임 시간(분).</summary>
    public const int DurationMinutes = CloseMinutes - OpenMinutes;
}
