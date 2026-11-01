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

    /// <summary>마지막 날 시민권 살 돈이 모자라 쓰러진 하루가 정산에서 남기는 줄("……")입니다.</summary>
    public static readonly uint[] CollapsedLines = { 8596 };

    /// <summary>시민권 구매 실패 검은 화면의 줄 이름: 시민권 가격, 총 자산, 부족한 금액, 구매 실패입니다.</summary>
    public static readonly uint[] ShortfallLabels = { 8597, 8598, 8599, 8600 };

    /// <summary>하루가 쓰러진 마지막 날 "다음 날"을 누르면 아빠가 하루를 부르며 절규하는 줄입니다.</summary>
    public static readonly uint[] FatherScreamLines = { 8592, 8593, 8594, 8595 };

    /// <summary>설비 안내: 팜플렛을 가리키는 줄과 "눌러 봐" 줄입니다.</summary>
    public static readonly uint[] FacilityPointLines = { 8575, 8576 };

    /// <summary>설비 안내: 위 두 설비(새 품목) 설명 줄입니다.</summary>
    public static readonly uint[] FacilityShelfLines = { 8577, 8578 };

    /// <summary>설비 안내: 판매가와 구매 버튼 설명 줄입니다.</summary>
    public static readonly uint[] FacilityPurchaseLines = { 8579 };

    /// <summary>설비 안내: 분류 막대(거래 속도) 설명 줄입니다.</summary>
    public static readonly uint[] FacilitySortingLines = { 8580, 8581 };

    // 딸 이미지 단계(DaughterAppearanceData: 1·4·7·10·13·16·19일 시작)마다 몸 상태를 말하는 두 줄. 날짜에 따라 번갈아 쓴다.
    private static readonly uint[][] SymptomLines =
    {
        new uint[] { 8601, 8602 }, new uint[] { 8603, 8604 }, new uint[] { 8605, 8606 }, new uint[] { 8607, 8608 },
        new uint[] { 8609, 8610 }, new uint[] { 8611, 8612 }, new uint[] { 8613, 8614 }
    };
    private static readonly uint[] ViolationLines = { 8615, 8616 };
    private static readonly uint[] MoralityDropLines = { 8617, 8618 };
    private static readonly uint[] MoralityRiseLines = { 8619, 8620 };
    private static readonly uint[] RefusedLines = { 8621, 8622 };
    private static readonly uint[] ReputationUpLines = { 8623, 8624 };
    private static readonly uint[] ReputationDownLines = { 8625, 8626 };

    /// <summary>하루 도덕성 변화가 이 값 이하면 "오늘 나쁜 일을 봤다"는 반응을 한다.</summary>
    public const decimal MoralityDropThreshold = -10m;
    /// <summary>하루 도덕성 변화가 이 값 이상이면 "고맙다는 말을 들었다"는 반응을 한다.</summary>
    public const decimal MoralityRiseThreshold = 8m;
    /// <summary>거절한 손님이 이 수 이상이면 돌아간 손님 이야기를 한다.</summary>
    public const int RefusedThreshold = 3;

    /// <summary>
    /// 대본이 없는 보통 날 밤, 도장 전에 말할 줄: 몸 상태 한 줄과 그날 행동 반응(있으면) 한 줄입니다.
    /// 반응은 지침 위반, 도덕성 크게 하락, 크게 상승, 거절 많음 순으로 하나만 고릅니다.
    /// </summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <param name="violationCount">오늘 지침 위반 횟수입니다.</param>
    /// <param name="moralityDelta">오늘 누적 도덕성 변화량입니다.</param>
    /// <param name="refusedCustomers">오늘 거절한 손님 수입니다.</param>
    /// <returns>TextData 키 1~2개입니다.</returns>
    public static uint[] GetNightBeforeStamp(int day, int violationCount, decimal moralityDelta, int refusedCustomers)
    {
        uint symptom = pick(SymptomLines[Math.Min((Math.Max(day, 1) - 1) / 3, SymptomLines.Length - 1)], day);
        uint[] reaction = violationCount > 0 ? ViolationLines
            : moralityDelta <= MoralityDropThreshold ? MoralityDropLines
            : moralityDelta >= MoralityRiseThreshold ? MoralityRiseLines
            : refusedCustomers >= RefusedThreshold ? RefusedLines
            : null;
        return reaction == null ? new[] { symptom } : new[] { symptom, pick(reaction, day) };
    }

    /// <summary>보통 날 밤, 도장 뒤 명성 변화에 반응하는 줄입니다. 변화가 없으면 빈 배열입니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <param name="reputationDelta">오늘 명성 변화량입니다.</param>
    /// <returns>TextData 키 0~1개입니다.</returns>
    public static uint[] GetNightAfterStamp(int day, int reputationDelta) =>
        reputationDelta > 0 ? new[] { pick(ReputationUpLines, day) }
        : reputationDelta < 0 ? new[] { pick(ReputationDownLines, day) }
        : Array.Empty<uint>();

    private static uint pick(uint[] lines, int day) => lines[Math.Abs(day) % lines.Length];

    /// <summary>해당 날짜의 도장 전 대본 키를 반환합니다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차입니다.</param>
    /// <returns>대본이 없으면 빈 배열입니다.</returns>
    public static uint[] GetBeforeStamp(int day) => day == 1 ? FirstNightBeforeStamp : Array.Empty<uint>();

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
