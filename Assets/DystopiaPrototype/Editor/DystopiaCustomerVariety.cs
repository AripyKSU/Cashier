using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>실제 세션으로 한 영업일의 방문 순서를 만들어 같은 외형이 가까운 순서에 다시 나오는지 검사합니다.</summary>
public static class DystopiaCustomerVariety
{
    /// <summary>중복으로 보고할 최대 간격입니다. 화면에는 앞 손님과 대기 2명이 동시에 보입니다.</summary>
    private const int ReportedGap = 6;

    /// <summary>여러 seed의 방문 순서에서 외형 재등장 간격을 집계하고 가까운 중복을 보고합니다.</summary>
    [MenuItem("Dystopia/손님/Validate Appearance Variety")]
    public static void Validate()
    {
        var owner = UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>();
        if (owner == null) { Debug.LogError("DystopiaScreen이 있는 Scene을 열어 주세요."); return; }

        var detail = new StringBuilder();
        var classCounts = new Dictionary<DystopiaCustomerClass, int>();
        int worstGap = int.MaxValue, closeRepeats = 0, totalCustomers = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var session = new DystopiaSession(owner.Settings, seed);
            session.OpenShop();
            // 앞 손님과 대기 순서를 합치면 그날의 실제 방문 순서가 됩니다.
            var appearances = new List<int> { session.Customer.appearance };
            foreach (var waiting in session.WaitingCustomers) appearances.Add(waiting.appearance);
            totalCustomers += appearances.Count;
            // 성별은 반드시 교대하므로 방문 순서의 홀짝이 성별을 구분합니다. 같은 성별끼리만 외형이 겹칩니다.
            var lastSeen = new Dictionary<(int gender, int appearance), int>();
            for (int i = 0; i < appearances.Count; i++)
            {
                var key = (i % 2, appearances[i]);
                var customerClass = DystopiaSession.AppearanceClass(appearances[i]);
                classCounts.TryGetValue(customerClass, out int seen);
                classCounts[customerClass] = seen + 1;
                if (lastSeen.TryGetValue(key, out int previous))
                {
                    int gap = i - previous;
                    if (gap < worstGap) worstGap = gap;
                    if (gap <= ReportedGap) { closeRepeats++; detail.AppendLine($"  seed {seed}: 방문 {previous + 1}번과 {i + 1}번이 같은 외형 {DystopiaSession.AppearanceFileName(i % 2 == 0, appearances[i])}, 간격 {gap}"); }
                }
                lastSeen[key] = i;
            }
        }
        var distribution = new StringBuilder("분류별 등장: ");
        foreach (DystopiaCustomerClass customerClass in System.Enum.GetValues(typeof(DystopiaCustomerClass)))
        {
            classCounts.TryGetValue(customerClass, out int seen);
            distribution.Append($"{customerClass} {seen}  ");
        }
        string summary = $"손님 {totalCustomers}명 생성. 간격 {ReportedGap} 이하 중복 {closeRepeats}건. 최소 재등장 간격 {(worstGap == int.MaxValue ? "재등장 없음" : worstGap.ToString())}.\n{distribution}\n";
        if (closeRepeats == 0) Debug.Log(summary + detail);
        else Debug.LogWarning(summary + detail);
    }
}
