using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>하루 정산 시 확정되는 딸 대사와 이미지.</summary>
public readonly struct DaughterDialogueResult
{
    /// <summary>평가한 표시 일차.</summary>
    public uint Day { get; }
    /// <summary>정산 시점 누적 도덕성.</summary>
    public decimal Morality { get; }
    /// <summary>선택된 대사 구간 PK.</summary>
    public uint DialogueIdx { get; }
    /// <summary>선택된 TextData FK.</summary>
    public uint TextIdx { get; }
    /// <summary>선택된 ResourceData FK.</summary>
    public uint ResourceIdx { get; }

    internal DaughterDialogueResult(uint day, decimal morality, uint dialogueIdx, uint textIdx, uint resourceIdx)
    {
        Day = day;
        Morality = morality;
        DialogueIdx = dialogueIdx;
        TextIdx = textIdx;
        ResourceIdx = resourceIdx;
    }
}

/// <summary>검증된 표에서 누적 도덕성과 날짜에 맞는 한 문장을 선택한다.</summary>
public sealed class DaughterDialogueService
{
    private readonly DaughterDialogueData[] dialogues;
    private readonly DaughterAppearanceData[] appearances;
    private readonly Random random;

    /// <summary>검증된 대사·이미지 행과 세션 난수원을 복사한다.</summary>
    /// <param name="dialogues">전체 도덕성 구간.</param>
    /// <param name="appearances">1일차부터의 이미지 구간.</param>
    /// <param name="random">동일 확률 후보 선택 난수원.</param>
    /// <exception cref="ArgumentNullException">입력 누락.</exception>
    /// <exception cref="ArgumentException">빈 테이블.</exception>
    public DaughterDialogueService(IEnumerable<DaughterDialogueData> dialogues,
        IEnumerable<DaughterAppearanceData> appearances, Random random)
    {
        if (dialogues == null) throw new ArgumentNullException(nameof(dialogues));
        if (appearances == null) throw new ArgumentNullException(nameof(appearances));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        this.dialogues = dialogues.OrderBy(row => row.MoralityMin ?? decimal.MinValue).ToArray();
        this.appearances = appearances.OrderBy(row => row.StartDay).ToArray();
        if (this.dialogues.Length == 0 || this.appearances.Length == 0)
            throw new ArgumentException("딸 대사와 이미지 데이터가 필요합니다.");
    }

    /// <summary>현재 누적 도덕성과 표시 일차에 맞는 한 문장과 이미지를 선택한다.</summary>
    /// <param name="day">1부터 시작하는 표시 일차.</param>
    /// <param name="morality">정산 시점 누적 도덕성.</param>
    /// <returns>하루 동안 보관할 불변 결과.</returns>
    /// <exception cref="ArgumentOutOfRangeException">0일차.</exception>
    /// <exception cref="InvalidOperationException">대사 또는 이미지 구간 누락.</exception>
    public DaughterDialogueResult Select(uint day, decimal morality)
    {
        if (day == 0) throw new ArgumentOutOfRangeException(nameof(day));
        DaughterDialogueData dialogue = Array.Find(dialogues, row =>
            (!row.MoralityMin.HasValue || row.MoralityMin.Value <= morality) &&
            (!row.MoralityMax.HasValue || morality < row.MoralityMax.Value))
            ?? throw new InvalidOperationException($"도덕성 {morality}의 딸 대사 구간이 없습니다.");
        DaughterAppearanceData appearance = appearances.LastOrDefault(row => row.StartDay <= day)
            ?? throw new InvalidOperationException($"{day}일차의 딸 이미지 구간이 없습니다.");
        return new DaughterDialogueResult(day, morality, dialogue.Idx,
            dialogue.TextIdxs[random.Next(dialogue.TextIdxs.Length)], appearance.ResourceIdx);
    }
}
