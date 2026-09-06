using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>테스트 assembly 추가 없이 실제 런 클래스를 검증하는 Editor 전용 검사입니다.</summary>
public static class DystopiaValidation
{
    /// <summary>가격 경계, 멱등성, 시간, 주간 상납과 완결 경로를 검증합니다.</summary>
    [MenuItem("Dystopia/Validate Rules")]
    public static void Validate()
    {
        var report=new StringBuilder("Dystopia rule verification (not UI input verification)\n");
        var settings=new DystopiaSettings();
        DystopiaSession New() { var s=new DystopiaSession(settings,123); s.OpenShop(); return s; }
        var invalid=New();
        foreach(string value in new[]{"","-1","1.5","0","99999999"," 1000","abc"})
            Require(!invalid.Confirm(value),"invalid input rejected: "+value,report);
        Require(invalid.Cash==0 && invalid.Phase==DystopiaPhase.Trading,"invalid input preserves transaction",report);
        var normal=New();
        normal.Customer.budget=normal.Customer.total*2;
        int total=normal.Customer.total;
        Require(normal.Confirm(total.ToString()) && normal.LastAccepted,"normal price accepted",report);
        Require(normal.Cash==total && normal.Morality==50 && normal.Reputation==51,"normal price cash and green reputation",report);
        Require(!normal.Confirm(total.ToString()) && normal.Cash==total,"duplicate confirmation blocked",report);
        var discount=New(); discount.Customer.budget=discount.Customer.total*2;
        discount.Confirm((discount.Customer.total*80/100).ToString());
        Require(discount.Morality==54,"80 percent discount is +4 only",report);
        var little=New(); little.Customer.budget=little.Customer.total*2;
        little.Confirm((little.Customer.total-1).ToString());
        Require(little.Morality==52,"small discount is +2",report);
        var markup=New(); markup.Customer.budget=markup.Customer.total*2;
        int boundary=markup.Customer.total*markup.Customer.tolerancePercent/100;
        markup.Confirm(boundary.ToString());
        Require(markup.LastAccepted && markup.Morality==48 && markup.Reputation==51,"tolerance boundary accepted without rejection reputation",report);
        var refusal=New(); refusal.Customer.budget=refusal.Customer.total*2;
        refusal.Confirm((refusal.Customer.total*refusal.Customer.tolerancePercent/100+1).ToString());
        Require(!refusal.LastAccepted && refusal.Cash==0 && refusal.Reputation==48 && refusal.Morality==49,"over tolerance rejected once",report);
        var budget=New(); budget.Customer.budget=budget.Customer.total-1;
        budget.Confirm(budget.Customer.total.ToString());
        Require(!budget.LastAccepted && budget.Reputation==49 && budget.Morality==49,"budget refusal morality applied",report);
        var compound=New(); compound.Customer.budget=1; compound.Confirm("9999999");
        Require(compound.Reputation==48 && compound.Morality==49,"combined refusal not double-counted",report);
        var waiting=New(); waiting.Tick(35);
        Require(waiting.Departed==2 && waiting.Remaining==6 && waiting.Gauge==30 && waiting.Reputation==45,"shared gauge departure and recovery",report);
        waiting.TogglePause(); waiting.Tick(100);
        Require(waiting.Gauge==30 && waiting.Departed==2,"pause freezes gauge",report);
        var guide=new DystopiaSession(settings,1); guide.Tick(100);
        Require(guide.Gauge==70,"guide freezes gauge",report);

        // 상태값을 지급하거나 날짜를 건너뛰지 않고 실제 거래/정산 API를 반복합니다.
        var full=new DystopiaSession(settings,456);
        int firstWeekCash=-1, trades=0;
        for(int step=0;step<3000 && full.Phase!=DystopiaPhase.Goal;step++)
        {
            switch(full.Phase)
            {
                case DystopiaPhase.PriceGuide: full.OpenShop(); break;
                case DystopiaPhase.Trading:
                    full.Tick(5); full.Confirm(full.Customer.total.ToString()); trades++; break;
                case DystopiaPhase.Result: full.Tick(settings.resultSeconds+.01f); break;
                case DystopiaPhase.Tribute:
                    if(full.Day==7) firstWeekCash=full.Cash;
                    full.PayTribute();
                    Require(full.Phase!=DystopiaPhase.Failed,"weekly tribute affordable day "+full.Day,report);
                    break;
                case DystopiaPhase.Settlement:
                    float gauge=full.Gauge; full.Tick(100);
                    Require(full.Gauge==gauge,"settlement freezes gauge day "+full.Day,report);
                    if(full.CanBuy) full.BuyCitizenship();
                    else full.NextDay();
                    break;
                case DystopiaPhase.Failed: throw new Exception("Normal-price simulation failed.");
            }
        }
        Require(firstWeekCash>=settings.firstTribute,"normal-price first tribute viability",report);
        Require(full.Phase==DystopiaPhase.Goal,"citizenship completion reachable",report);
        report.AppendLine($"Normal-price sample: first-week cash {firstWeekCash}; goal day {full.Day}; transactions {trades}; 5s decisions + results approx {trades*(5+settings.resultSeconds):0}s (reading time excluded).");
        var failed=new DystopiaSession(settings,99);
        for(int i=0;i<1000 && failed.Phase!=DystopiaPhase.Failed;i++)
        {
            if(failed.Phase==DystopiaPhase.PriceGuide) failed.OpenShop();
            else if(failed.Phase==DystopiaPhase.Trading) failed.Confirm("9999999");
            else if(failed.Phase==DystopiaPhase.Result) failed.Tick(1);
            else if(failed.Phase==DystopiaPhase.Settlement) failed.NextDay();
            else if(failed.Phase==DystopiaPhase.Tribute) failed.PayTribute();
        }
        Require(failed.Day==7 && failed.Phase==DystopiaPhase.Failed,"insufficient cash fails on first weekly tribute",report);
        var fresh=new DystopiaSession(settings,99);
        Require(fresh.Cash==0 && fresh.Day==1 && fresh.Reputation==50 && fresh.Morality==50,"fresh run reset",report);
        File.WriteAllText("Assets/DystopiaPrototype/Evidence/Rules.txt",report.ToString());
        Debug.Log("[Dystopia] RULE CHECKS PASSED\n"+report);
    }

    /// <summary>실패 시 정확한 검증 지점을 보존합니다.</summary>
    private static void Require(bool condition,string label,StringBuilder report)
    {
        if(!condition) throw new InvalidOperationException("Dystopia validation failed: "+label);
        report.AppendLine("OK: "+label);
    }
}
