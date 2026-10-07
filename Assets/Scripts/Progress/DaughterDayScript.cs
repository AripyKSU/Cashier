using System;

/// <summary>
/// 특정 날짜 정산에서 딸이 도장 전후로 말하는 여러 줄 대본의 TextData 키입니다.
/// 1일차 밤에는 명성(도장)의 의미를 설명하고, 숨은 도덕성은 직접 말하지 않고 마음으로만 암시합니다.
/// 대본이 없는 날은 기존 도덕성 구간 한 줄 대사를 그대로 사용합니다.
/// </summary>
public static class DaughterDayScript
{
    private static readonly uint[] FirstNightBeforeStamp = { 8563, 8541, 8542, 8543, 8544 };
    private static readonly uint[] FirstNightAfterStamp =
    {
        8545, 8546, 8547, 8548, 8549, 8550, 8551, 8552, 8553
    };

    /// <summary>해당 날짜의 도장 전 대본 키를 반환합니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>대본이 없으면 빈 배열입니다.</returns>
    public static uint[] GetBeforeStamp(int day) => day == 1 ? FirstNightBeforeStamp : Array.Empty<uint>();

    /// <summary>해당 날짜의 도장 뒤 대본 키를 반환합니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>대본이 없으면 빈 배열입니다.</returns>
    public static uint[] GetAfterStamp(int day) => day == 1 ? FirstNightAfterStamp : Array.Empty<uint>();
}
