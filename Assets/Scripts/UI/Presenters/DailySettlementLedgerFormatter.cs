using System.Text;

/// <summary>가계부의 왼쪽 영업 결산과 오른쪽 운영 기록에 표시할 완성 문자열입니다.</summary>
public readonly struct DailySettlementLedgerText
{
    /// <summary>왼쪽 페이지에 표시할 영업 결산입니다.</summary>
    public string LeftPage { get; }

    /// <summary>오른쪽 페이지에 표시할 손님·지침·납부 기록입니다.</summary>
    public string RightPage { get; }

    /// <summary>가계부 양쪽 페이지의 완성 문자열을 생성합니다.</summary>
    /// <param name="leftPage">왼쪽 페이지 문자열입니다.</param>
    /// <param name="rightPage">오른쪽 페이지 문자열입니다.</param>
    public DailySettlementLedgerText(string leftPage, string rightPage)
    {
        LeftPage = leftPage ?? string.Empty;
        RightPage = rightPage ?? string.Empty;
    }
}

/// <summary>확정된 일일 정산 스냅샷을 두 페이지의 가계부 문구로 변환합니다.</summary>
public static class DailySettlementLedgerFormatter
{
    /// <summary>정산 값을 재조회하거나 재계산하지 않고 왼쪽·오른쪽 페이지 문구를 만듭니다.</summary>
    /// <param name="viewData">도메인에서 확정되어 UI로 전달된 정산 스냅샷입니다.</param>
    /// <returns>가계부 양쪽 페이지에 바로 표시할 문자열입니다.</returns>
    public static DailySettlementLedgerText Format(DailySettlementViewData viewData)
    {
        return new DailySettlementLedgerText(formatLeftPage(viewData), formatRightPage(viewData));
    }

    // 진짜 가계부처럼 아래로 갈수록 글씨가 조금씩 커진다(종이도 아래로 넓어진다). 줄마다 이만큼 키운다.
    private const int SizeStepPercent = 3;
    // 구획 제목·구분선·수입·지출 잉크 색. 갈색 종이에서 검은 글씨보다 눈에 잘 들어온다.
    private const string TitleColor = "#5a1a12";
    private const string RuleColor = "#5a3a28";
    private const string IncomeColor = "#2f4a1e";
    private const string ExpenseColor = "#7a1f14";

    /// <summary>왼쪽 페이지의 금액 결산 문구를 만듭니다.</summary>
    /// <param name="viewData">확정된 정산 스냅샷입니다.</param>
    /// <returns>왼쪽 페이지 문자열입니다.</returns>
    private static string formatLeftPage(DailySettlementViewData viewData)
    {
        var builder = new StringBuilder(384);
        int size = 88;
        appendTitle(builder, DayCountdownLabel.Format(viewData.Day) + " 영업 결산");
        appendAmount(builder, "판매 수입", viewData.SaleIncome, ref size, true);
        appendAmount(builder, "유지비", viewData.MaintenanceAmount, ref size, false, true);
        appendAmount(builder, "지침 벌금", viewData.GuidelinePenaltyAmount, ref size, false, true);
        appendRule(builder, ref size);
        appendAmount(builder, "순이익", viewData.NetProfit, ref size, true, false, 6);
        // 현재 보유금은 화면 아래 가운데 별도 UI로 보여 준다(설비 창 위에서도 보이도록).
        return builder.ToString().TrimEnd();
    }

    /// <summary>오른쪽 페이지의 운영 결과와 납부 상태 문구를 만듭니다.</summary>
    /// <param name="viewData">확정된 정산 스냅샷입니다.</param>
    /// <returns>오른쪽 페이지 문자열입니다.</returns>
    private static string formatRightPage(DailySettlementViewData viewData)
    {
        var builder = new StringBuilder(384);
        int size = 88;
        appendTitle(builder, "손님 기록");
        appendLine(builder, $"판매 성공  {viewData.SuccessfulSales}명", ref size);
        appendLine(builder, $"판매 거절  {viewData.RefusedCustomers}명", ref size);
        appendRule(builder, ref size);
        int chargedViolationCount = System.Math.Min(
            viewData.GuidelineViolationCount,
            DailyGuidelinePenaltyCalculator.MaximumChargedViolationCount);
        int penaltyPercent = chargedViolationCount * DailyGuidelinePenaltyCalculator.PercentPerViolation;
        appendLine(builder, $"지침 위반  {viewData.GuidelineViolationCount}회 · {penaltyPercent}%", ref size,
            viewData.GuidelineViolationCount > 0 ? ExpenseColor : null);
        appendPaymentStatus(builder, viewData, ref size);
        return builder.ToString().TrimEnd();
    }

    /// <summary>납부·미납·유예 상태를 오른쪽 페이지에 추가합니다.</summary>
    /// <param name="builder">오른쪽 페이지 문자열 작성기입니다.</param>
    /// <param name="viewData">확정된 정산 스냅샷입니다.</param>
    /// <param name="size">현재 줄 글씨 크기(%)입니다. 줄을 쓸 때마다 커집니다.</param>
    private static void appendPaymentStatus(StringBuilder builder, DailySettlementViewData viewData, ref int size)
    {
        if (viewData.GracePeriodEndDay.HasValue)
        {
            appendLine(builder, "상환 기한  " + DayCountdownLabel.Format(viewData.GracePeriodEndDay.Value), ref size, ExpenseColor);
            appendLine(builder, $"남은 기간  {viewData.RemainingGraceDays}일", ref size, ExpenseColor);
        }
    }

    /// <summary>페이지 맨 위 구획 제목과 그 아래 작은 여백을 추가합니다.</summary>
    /// <param name="builder">페이지 문자열 작성기입니다.</param>
    /// <param name="title">제목 문구입니다.</param>
    private static void appendTitle(StringBuilder builder, string title)
    {
        builder.Append("<size=108%><color=").Append(TitleColor).Append('>').Append(title).AppendLine("</color></size>");
        builder.AppendLine("<size=45%> </size>");
    }

    /// <summary>구획을 나누는 잉크 줄을 추가합니다.</summary>
    /// <param name="builder">페이지 문자열 작성기입니다.</param>
    /// <param name="size">현재 줄 글씨 크기(%)입니다.</param>
    private static void appendRule(StringBuilder builder, ref int size)
    {
        builder.Append("<size=").Append(size).Append("%><color=").Append(RuleColor).AppendLine(">──────────</color></size>");
        size += SizeStepPercent;
    }

    /// <summary>글씨 크기와 (선택) 잉크 색을 입힌 한 줄을 추가하고 다음 줄 크기를 키웁니다.</summary>
    /// <param name="builder">페이지 문자열 작성기입니다.</param>
    /// <param name="text">줄 내용입니다.</param>
    /// <param name="size">현재 줄 글씨 크기(%)입니다.</param>
    /// <param name="color">잉크 색. null이면 기본 글씨 색입니다.</param>
    /// <param name="extraSizePercent">이 줄만 더 키울 크기(%)입니다. 합계 줄 강조에 씁니다.</param>
    private static void appendLine(StringBuilder builder, string text, ref int size, string color = null, int extraSizePercent = 0)
    {
        builder.Append("<size=").Append(size + extraSizePercent).Append("%>");
        if (color != null) builder.Append("<color=").Append(color).Append('>');
        builder.Append(text);
        if (color != null) builder.Append("</color>");
        builder.AppendLine("</size>");
        size += SizeStepPercent;
    }

    /// <summary>라벨과 금액을 가계부 한 줄로 추가합니다. 들어온 돈은 초록, 나간 돈은 붉은 잉크로 씁니다.</summary>
    /// <param name="builder">페이지 문자열 작성기입니다.</param>
    /// <param name="label">금액 항목 이름입니다.</param>
    /// <param name="amount">표시할 금액입니다.</param>
    /// <param name="size">현재 줄 글씨 크기(%)입니다.</param>
    /// <param name="showPositiveSign">양수에 더하기 기호를 표시할지 여부입니다.</param>
    /// <param name="showAsExpense">양수를 지출로 표시할지 여부입니다.</param>
    /// <param name="extraSizePercent">이 줄만 더 키울 크기(%)입니다.</param>
    private static void appendAmount(
        StringBuilder builder,
        string label,
        long amount,
        ref int size,
        bool showPositiveSign = false,
        bool showAsExpense = false,
        int extraSizePercent = 0)
    {
        var line = new StringBuilder(label).Append("  ");
        if (amount > 0)
        {
            if (showAsExpense) line.Append('-');
            else if (showPositiveSign) line.Append('+');
        }

        line.Append(amount.ToString("N0")).Append("원");
        string color = amount == 0 ? null : (showAsExpense || amount < 0) ? ExpenseColor : IncomeColor;
        appendLine(builder, line.ToString(), ref size, color, extraSizePercent);
    }

    /// <summary>정수 변화량에 양수 부호를 포함해 표시합니다.</summary>
    /// <param name="value">표시할 변화량입니다.</param>
    /// <returns>부호가 포함된 정수 문자열입니다.</returns>
    private static string formatSignedNumber(int value) => value > 0 ? $"+{value}" : value.ToString();
}
