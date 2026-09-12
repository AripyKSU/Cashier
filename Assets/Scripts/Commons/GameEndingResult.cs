/// <summary>세션을 끝내는 최종 결과의 종류입니다.</summary>
public enum EndingKind
{
    /// <summary>아직 종료하지 않음.</summary>
    None = 0,
    /// <summary>미납 유예 종료로 실패.</summary>
    GameOver = 1,
    /// <summary>31일차 시민권 보유 성공.</summary>
    Good = 2,
    /// <summary>31일차 시민권 미보유 종료.</summary>
    Bad = 3,
    EndingKind_End
}

/// <summary>한 번 확정된 게임 종료 판정과 당시 세션 snapshot입니다.</summary>
public readonly struct GameEndingResult
{
    /// <summary>종료 종류.</summary>
    public EndingKind Kind { get; }
    /// <summary>종료한 1기반 표시 일차.</summary>
    public int DisplayDay { get; }
    /// <summary>종료 판정 당시 시민권 보유 여부.</summary>
    public bool HasCitizenship { get; }
    /// <summary>종료 판정 당시 현금 잔액.</summary>
    public long Balance { get; }
    /// <summary>종료 판정 당시 명성.</summary>
    public int Reputation { get; }
    /// <summary>종료 판정 당시 누적 도덕성.</summary>
    public decimal Morality { get; }

    /// <summary>검증된 최종 종료 snapshot을 생성합니다.</summary>
    /// <param name="kind">종료 종류.</param>
    /// <param name="displayDay">종료한 표시 일차.</param>
    /// <param name="hasCitizenship">종료 시 시민권 보유 여부.</param>
    /// <param name="balance">종료 시 잔액.</param>
    /// <param name="reputation">종료 시 명성.</param>
    /// <param name="morality">종료 시 누적 도덕성.</param>
    public GameEndingResult(EndingKind kind, int displayDay, bool hasCitizenship, long balance, int reputation, decimal morality)
    {
        if (kind <= EndingKind.None || kind >= EndingKind.EndingKind_End)
            throw new System.ArgumentOutOfRangeException(nameof(kind));
        if (displayDay <= 0) throw new System.ArgumentOutOfRangeException(nameof(displayDay));
        if (kind == EndingKind.Good && !hasCitizenship)
            throw new System.ArgumentException("Good 엔딩에는 시민권 보유가 필요합니다.", nameof(hasCitizenship));
        if (kind == EndingKind.Bad && hasCitizenship)
            throw new System.ArgumentException("Bad 엔딩에는 시민권 미보유가 필요합니다.", nameof(hasCitizenship));

        Kind = kind;
        DisplayDay = displayDay;
        HasCitizenship = hasCitizenship;
        Balance = balance;
        Reputation = reputation;
        Morality = morality;
    }
}
