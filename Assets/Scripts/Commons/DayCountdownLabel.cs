/// <summary>
/// 표시 일차를 딸(하루)이 죽기까지 남은 날 카운트다운(D-N) 문구로 바꾸는 표시 전용 변환기.
/// 인트로가 "주어진 하루 D-21 → -1 → D-20"으로 끝나므로 1일차는 D-20, 마지막 영업일(20일차)은 D-1이다.
/// 내부 일차·CSV의 day 값은 그대로 두고 화면 문구만 바꿉니다.
/// </summary>
public static class DayCountdownLabel
{
    /// <summary>남은 날이 0일 때의 표기입니다(영업일에는 나오지 않습니다).</summary>
    public const string FinalDayText = "D-DAY";

    /// <summary>
    /// 1부터 시작하는 표시 일차를 D-N 문구로 변환합니다.
    /// </summary>
    /// <param name="displayDay">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>1일차는 D-20, 20일차는 D-1입니다. 21일차는 D-DAY, 그 이후는 D+N입니다.</returns>
    public static string Format(int displayDay)
    {
        int remainingDays = GameSessionManager.FinalDay + 1 - displayDay;
        if (remainingDays == 0)
            return FinalDayText;

        return remainingDays > 0 ? $"D-{remainingDays}" : $"D+{-remainingDays}";
    }

    /// <summary>
    /// 마지막 영업일(시민권을 살 수 있는 마지막 날)인지 확인합니다. 그날 문구를 붉게 강조합니다.
    /// </summary>
    /// <param name="displayDay">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>마지막 영업일이면 true입니다.</returns>
    public static bool IsFinalDay(int displayDay)
    {
        return displayDay == GameSessionManager.FinalDay;
    }
}
