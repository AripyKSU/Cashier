using System;
using System.Collections.Generic;

/// <summary>
/// 프로젝트에서 사용하는 사운드의 ResourceData 식별자를 관리한다.
/// </summary>
public static class SoundKeys
{
    // BGM
    public const uint SupervisorBgm = 4257;
    public const uint GameplayAmbience = 4258;
    public const uint SettlementBgm = 4259;
    public const uint TitleBgm = 4413;
    public const uint GoodEndingBgm = 4414;
    public const uint BadEndingBgm = 4415;

    // 무기
    public const uint Reload = 4416;
    public const uint Gunshot = 4417;

    // 거래
    public const uint TransactionSuccess = 4260;
    public const uint TransactionFail = 4261;

    // 계산기
    public const uint CalculatorOpen = 4262;
    public const uint CalculatorButton = 4263;

    // 물건 상호작용
    public const uint ItemPickup = 4264;
    public const uint ItemPlace = 4265;
    public const uint ItemRemove = 4266;
    public const uint BoxItemDrop = 4267;
    public const uint CustomerBoxDrop = 4295;

    // 작업 / 설비
    public const uint Vacuum = 4268;
    public const uint FacilityUpgrade = 4269;

    // 진행 연출
    public const uint DailyGuideline = 4270;
    public const uint DayStart = 4271;
    public const uint DayEnd = 4272;

    // 정산
    public const uint ReputationStamp = 4273;
    public const uint LedgerWrite = 4274;

    // 대화
    public const uint DialogueVoice = 4275;

    private static readonly IReadOnlyList<uint> all = Array.AsReadOnly(new[]
    {
        SupervisorBgm,
        GameplayAmbience,
        SettlementBgm,
        TitleBgm,
        GoodEndingBgm,
        BadEndingBgm,
        Reload,
        Gunshot,
        TransactionSuccess,
        TransactionFail,
        CalculatorOpen,
        CalculatorButton,
        ItemPickup,
        ItemPlace,
        ItemRemove,
        BoxItemDrop,
        CustomerBoxDrop,
        Vacuum,
        FacilityUpgrade,
        DailyGuideline,
        DayStart,
        DayEnd,
        ReputationStamp,
        LedgerWrite,
        DialogueVoice
    });

    /// <summary>
    /// 부팅 중 초기화해야 하는 모든 사운드 ResourceData 식별자를 반환한다.
    /// </summary>
    public static IReadOnlyList<uint> All => all;
}
