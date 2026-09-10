/// <summary>일일 명성 변화의 표현 단계를 정의합니다.</summary>
public enum ReputationFeedbackTier
{
    GreatlyWorsened,
    Worsened,
    Stable,
    Improved,
    GreatlyImproved
}

/// <summary>
/// 일일 명성 변화량을 숫자 없이 표시할 정성적 피드백 문구와 이미지 단계로 변환합니다.
/// </summary>
public static class ReputationFeedbackFormatter
{
    /// <summary>
    /// 최종 명성 변화량에 해당하는 피드백 단계를 반환합니다.
    /// </summary>
    /// <param name="reputationDelta">일일 정산으로 확정된 명성 변화량입니다.</param>
    /// <returns>명성 변화의 정성적 단계입니다.</returns>
    public static ReputationFeedbackTier GetTier(int reputationDelta)
    {
        if (reputationDelta >= 10)
        {
            return ReputationFeedbackTier.GreatlyImproved;
        }

        if (reputationDelta > 0)
        {
            return ReputationFeedbackTier.Improved;
        }

        if (reputationDelta == 0)
        {
            return ReputationFeedbackTier.Stable;
        }

        if (reputationDelta <= -10)
        {
            return ReputationFeedbackTier.GreatlyWorsened;
        }

        return ReputationFeedbackTier.Worsened;
    }

    /// <summary>
    /// 최종 명성 변화량에 맞는 숫자 없는 정성적 문구를 반환합니다.
    /// </summary>
    /// <param name="reputationDelta">일일 정산으로 확정된 명성 변화량입니다.</param>
    /// <returns>숫자를 포함하지 않는 명성 피드백 표시 문자열입니다.</returns>
    public static string Format(int reputationDelta)
    {
        switch (GetTier(reputationDelta))
        {
            case ReputationFeedbackTier.GreatlyImproved:
                return "가게의 평판이 크게 올랐습니다.";
            case ReputationFeedbackTier.Improved:
                return "가게의 평판이 올랐습니다.";
            case ReputationFeedbackTier.Stable:
                return "가게의 평판에 큰 변화가 없습니다.";
            case ReputationFeedbackTier.GreatlyWorsened:
                return "가게의 평판이 크게 나빠졌습니다.";
            default:
                return "가게의 평판이 나빠졌습니다.";
        }
    }
}
