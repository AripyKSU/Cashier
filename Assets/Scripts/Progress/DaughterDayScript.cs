using System;

/// <summary>
/// 특정 날짜 정산에서 딸이 도장 전후로 말하는 여러 줄 대본의 TextData 키입니다.
/// 1일차 밤에는 명성(도장)의 의미를 설명하고, 설비 업그레이드를 직접 열어 보게 한 뒤,
/// 숨은 도덕성은 직접 말하지 않고 마음으로만 암시합니다.
/// 대본이 없는 날은 기존 도덕성 구간 한 줄 대사를 그대로 사용합니다.
/// </summary>
public static class DaughterDayScript
{
    private static readonly uint[] FirstNightBeforeStamp = { 8563, 8541, 8542, 8543, 8544 };
    private static readonly uint[] FirstNightAfterStamp = { 8545, 8546, 8547, 8548, 8549 };
    private static readonly uint[] FirstNightAfterFacility = { 8550, 8551, 8552, 8553 };
    // 마지막 날 하루는 테이블에 쓰러져 말을 못 한다.
    private static readonly uint[] FinalDayBeforeStamp = { 8596 };

    /// <summary>마지막 날 시민권 없이 "다음 날"을 누르면 아빠가 쓰러진 하루를 부르며 절규하는 줄입니다.</summary>
    public static readonly uint[] FatherScreamLines = { 8592, 8593, 8594, 8595 };

    /// <summary>설비 안내: 팜플렛을 가리키는 줄과 "눌러 봐" 줄입니다.</summary>
    public static readonly uint[] FacilityPointLines = { 8575, 8576 };

    /// <summary>설비 안내: 위 두 설비(새 품목) 설명 줄입니다.</summary>
    public static readonly uint[] FacilityShelfLines = { 8577, 8578 };

    /// <summary>설비 안내: 판매가와 구매 버튼 설명 줄입니다.</summary>
    public static readonly uint[] FacilityPurchaseLines = { 8579 };

    /// <summary>설비 안내: 분류 막대(거래 속도) 설명 줄입니다.</summary>
    public static readonly uint[] FacilitySortingLines = { 8580, 8581 };

    /// <summary>해당 날짜의 도장 전 대본 키를 반환합니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>대본이 없으면 빈 배열입니다.</returns>
    public static uint[] GetBeforeStamp(int day) => day == 1 ? FirstNightBeforeStamp
        : day == GameSessionManager.FinalDay ? FinalDayBeforeStamp : Array.Empty<uint>();

    /// <summary>해당 날짜의 도장 뒤 대본 키를 반환합니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>대본이 없으면 빈 배열입니다.</returns>
    public static uint[] GetAfterStamp(int day) => day == 1 ? FirstNightAfterStamp : Array.Empty<uint>();

    /// <summary>해당 날짜에 도장 뒤 대본 다음으로 설비 업그레이드 안내를 하는지 여부입니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>1일차면 true입니다.</returns>
    public static bool HasFacilityGuide(int day) => day == 1;

    /// <summary>설비 안내를 마치고 창을 닫은 뒤 이어서 말할 대본 키를 반환합니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>대본이 없으면 빈 배열입니다.</returns>
    public static uint[] GetAfterFacility(int day) => day == 1 ? FirstNightAfterFacility : Array.Empty<uint>();
}
