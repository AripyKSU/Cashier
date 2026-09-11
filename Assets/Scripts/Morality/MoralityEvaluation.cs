/// <summary>한 거래가 참조한 도덕성 행과 확정 변화량.</summary>
public readonly struct MoralityEvaluation
{
    /// <summary>적용한 MoralityData PK.</summary>
    public uint DataIdx { get; }
    /// <summary>손님 연령에 따라 확정한 변화량.</summary>
    public decimal Delta { get; }

    /// <summary>검증된 도덕성 계산 결과를 생성한다.</summary>
    /// <param name="dataIdx">0이 아닌 데이터 PK.</param>
    /// <param name="delta">반올림하지 않은 변화량.</param>
    public MoralityEvaluation(uint dataIdx, decimal delta)
    {
        if (dataIdx == 0) throw new System.ArgumentOutOfRangeException(nameof(dataIdx));
        DataIdx = dataIdx;
        Delta = delta;
    }
}
