/// <summary>
/// 표시 일차를 딸에게 남은 날짜 카운트다운(D-N) 문구로 바꾸는 표시 전용 변환기.
/// 내부 일차·CSV의 day 값은 그대로 두고 화면 문구만 바꿉니다.
/// 마지막 영업일(<see cref="GameSessionManager.FinalDay"/>)은 D-DAY로 표시합니다.
/// </summary>
public static class DayCountdownLabel
{
    /// <summary>마감 당일 표기입니다.</summary>
    public const string FinalDayText = "D-DAY";

    /// <summary>
    /// 1부터 시작하는 표시 일차를 D-N 문구로 변환합니다.
    /// </summary>
    /// <param name="displayDay">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>1일차는 D-19, 마지막 날은 D-DAY입니다. 마지막 날 이후는 D+N입니다.</returns>
    public static string Format(int displayDay)
    {
        int remainingDays = GameSessionManager.FinalDay - displayDay;
        if (remainingDays == 0)
            return FinalDayText;

        return remainingDays > 0 ? $"D-{remainingDays}" : $"D+{-remainingDays}";
    }

    /// <summary>
    /// 마지막 날인지 확인합니다. 마지막 날 문구 강조에 사용합니다.
    /// </summary>
    /// <param name="displayDay">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>마지막 영업일이면 true입니다.</returns>
    public static bool IsFinalDay(int displayDay)
    {
        return displayDay == GameSessionManager.FinalDay;
    }
}
