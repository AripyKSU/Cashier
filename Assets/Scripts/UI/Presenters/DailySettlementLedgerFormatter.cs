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

    /// <summary>왼쪽 페이지의 금액 결산 문구를 만듭니다.</summary>
    /// <param name="viewData">확정된 정산 스냅샷입니다.</param>
    /// <returns>왼쪽 페이지 문자열입니다.</returns>
    private static string formatLeftPage(DailySettlementViewData viewData)
    {
        var builder = new StringBuilder(192);
        builder.Append("DAY ").Append(viewData.Day).AppendLine(" 영업 결산").AppendLine();
        appendAmount(builder, "판매 수입", viewData.SaleIncome, true);
        appendAmount(builder, "유지비", viewData.MaintenanceAmount, false, true);
        appendAmount(builder, "지침 벌금", viewData.GuidelinePenaltyAmount, false, true);
        builder.AppendLine("──────────");
        appendAmount(builder, "순이익", viewData.NetProfit, true);
        builder.AppendLine();
        appendAmount(builder, "현재 보유금", viewData.CurrentBalance);
        return builder.ToString().TrimEnd();
    }

    /// <summary>오른쪽 페이지의 운영 결과와 납부 상태 문구를 만듭니다.</summary>
    /// <param name="viewData">확정된 정산 스냅샷입니다.</param>
    /// <returns>오른쪽 페이지 문자열입니다.</returns>
    private static string formatRightPage(DailySettlementViewData viewData)
    {
        var builder = new StringBuilder(320);
        builder.AppendLine("손님 기록").AppendLine();
        builder.Append("판매 성공  ").Append(viewData.SuccessfulSales).AppendLine("명");
        builder.Append("판매 거절  ").Append(viewData.RefusedCustomers).AppendLine("명");
        builder.AppendLine();
        int chargedViolationCount = System.Math.Min(
            viewData.GuidelineViolationCount,
            DailyGuidelinePenaltyCalculator.MaximumChargedViolationCount);
        int penaltyPercent = chargedViolationCount * DailyGuidelinePenaltyCalculator.PercentPerViolation;
        builder.Append("지침 위반  ").Append(viewData.GuidelineViolationCount).Append("회 · ")
            .Append(penaltyPercent).AppendLine("%");
        appendPaymentStatus(builder, viewData);
        return builder.ToString().TrimEnd();
    }

    /// <summary>납부·미납·유예 상태를 오른쪽 페이지에 추가합니다.</summary>
    /// <param name="builder">오른쪽 페이지 문자열 작성기입니다.</param>
    /// <param name="viewData">확정된 정산 스냅샷입니다.</param>
    private static void appendPaymentStatus(StringBuilder builder, DailySettlementViewData viewData)
    {
        if (viewData.GracePeriodEndDay.HasValue)
        {
            builder.Append("상환 기한  DAY ").AppendLine(viewData.GracePeriodEndDay.Value.ToString());
            builder.Append("남은 기간  ").Append(viewData.RemainingGraceDays).Append("일");
            return;
        }
    }

    /// <summary>라벨과 금액을 가계부 한 줄로 추가합니다.</summary>
    /// <param name="builder">페이지 문자열 작성기입니다.</param>
    /// <param name="label">금액 항목 이름입니다.</param>
    /// <param name="amount">표시할 금액입니다.</param>
    /// <param name="showPositiveSign">양수에 더하기 기호를 표시할지 여부입니다.</param>
    /// <param name="showAsExpense">양수를 지출로 표시할지 여부입니다.</param>
    private static void appendAmount(
        StringBuilder builder,
        string label,
        long amount,
        bool showPositiveSign = false,
        bool showAsExpense = false)
    {
        builder.Append(label).Append("  ");
        if (amount > 0)
        {
            if (showAsExpense) builder.Append('-');
            else if (showPositiveSign) builder.Append('+');
        }

        builder.Append(amount.ToString("N0")).AppendLine("원");
    }

    /// <summary>정수 변화량에 양수 부호를 포함해 표시합니다.</summary>
    /// <param name="value">표시할 변화량입니다.</param>
    /// <returns>부호가 포함된 정수 문자열입니다.</returns>
    private static string formatSignedNumber(int value) => value > 0 ? $"+{value}" : value.ToString();
}
