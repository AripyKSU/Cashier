using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>전용 Scene이 소유하는 상품과 조정 가능한 프로토타입 임시값입니다.</summary>
[Serializable]
public sealed class DystopiaSettings
{
    /// <summary>상품 순서는 도입일 안내와 장바구니 참조에 사용됩니다. 공용 CSV ID가 아닙니다.</summary>
    public DystopiaProduct[] products = {
        new DystopiaProduct("생수", 1000, 1), new DystopiaProduct("건빵", 1500, 1),
        new DystopiaProduct("통조림", 2500, 1), new DystopiaProduct("즉석밥", 2000, 1),
        new DystopiaProduct("붕대", 3000, 3), new DystopiaProduct("진통제", 4000, 3),
        new DystopiaProduct("건전지", 3500, 3), new DystopiaProduct("비누", 1000, 5),
        new DystopiaProduct("방진 마스크", 2000, 5), new DystopiaProduct("연료캔", 5000, 5)
    };
    /// <summary>시민권 및 주간 상납금, 단위 원. 테스트용 초기값입니다.</summary>
    public int citizenshipPrice = 300000, firstTribute = 50000, secondTribute = 80000, laterTribute = 110000;
    /// <summary>명성 50일 때 8명이며, 명성 10점당 1명 변화합니다.</summary>
    public int baseVisitors = 8, minVisitors = 6, maxVisitors = 12;
    /// <summary>게이지 단위는 0~100, 감소는 초당 값입니다.</summary>
    public float gaugeStart = 70, greenThreshold = 70, gaugeDecay = 2, gaugeRecovery = 12, departureRecovery = 30;
    /// <summary>손님 이탈 및 거래의 평판/도덕성 변화입니다.</summary>
    public int departureCount = 2, departureReputation = -5, greenReputation = 1;
    /// <summary>복합 거절은 허용치 초과를 우선하여 한 번만 차감합니다.</summary>
    public int toleranceReputation = -2, budgetReputation = -1, refusalMorality = -1;
    /// <summary>할인 80% 이하 +4, 그 외 할인 +2, 바가지 성공 -2입니다.</summary>
    public int discountMorality = 2, generousMorality = 4, markupMorality = -2;
    /// <summary>가난한 손님의 빈도와 정가 대비 예산 비율입니다.</summary>
    public float poorChance = .18f, poorBudgetRatio = .85f;
    /// <summary>일반 손님 예산과 유형별 허용치의 정가 대비 백분율입니다.</summary>
    public int normalBudgetMinPercent = 110, normalBudgetMaxPercent = 150;
    /// <summary>짧은 결과 표시 동안 새 결제를 차단합니다. 단위 초.</summary>
    public float resultSeconds = .85f;
}

/// <summary>Scene에 직렬화하는 상품 한 종류입니다.</summary>
[Serializable]
public sealed class DystopiaProduct
{
    /// <summary>표시 이름, 정상 판매가(원), 최초 도입일입니다.</summary>
    public string name;
    public int price, firstDay;
    /// <summary>실제 프로젝트 내부 Sprite 참조입니다.</summary>
    public Sprite sprite;
    /// <summary>프로토타입 상품의 초기 authoring 값을 구성합니다.</summary>
    public DystopiaProduct(string name, int price, int firstDay) { this.name = name; this.price = price; this.firstDay = firstDay; }
}

/// <summary>화면 및 입력을 허용하는 런의 명시적 단계입니다.</summary>
public enum DystopiaPhase { PriceGuide, Trading, Result, Settlement, Tribute, Goal, Failed }

/// <summary>장바구니는 상품 참조와 수량을 보관합니다.</summary>
public sealed class DystopiaBasketLine
{
    public DystopiaProduct product;
    public int quantity;
}

/// <summary>현재 방문객의 장바구니, 예산, 허용치 및 외형 선택입니다.</summary>
public sealed class DystopiaCustomer
{
    public readonly List<DystopiaBasketLine> basket = new List<DystopiaBasketLine>();
    public int total, budget, tolerancePercent, appearance;
    public bool isPoor;
}

/// <summary>Scene 수명에 한정된 런 상태와 거래·시간·상납 불변 조건을 소유합니다.</summary>
public sealed class DystopiaSession
{
    private readonly DystopiaSettings settings;
    private readonly System.Random random;
    private float resultRemaining;
    private int dayStartReputation, dayStartMorality;
    public DystopiaPhase Phase { get; private set; }
    public DystopiaCustomer Customer { get; private set; }
    public int Day { get; private set; } = 1;
    public int Cash { get; private set; }
    public int Reputation { get; private set; } = 50;
    public int Morality { get; private set; } = 50;
    public int Visitors { get; private set; }
    public int Remaining { get; private set; }
    public int Sold { get; private set; }
    public int Refused { get; private set; }
    public int Departed { get; private set; }
    public int Revenue { get; private set; }
    public int ReputationChange => Reputation - dayStartReputation;
    public int MoralityChange => Morality - dayStartMorality;
    public float Gauge { get; private set; }
    public bool IsPaused { get; private set; }
    public bool LastAccepted { get; private set; }
    public string Feedback { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public int Revision { get; private set; }
    public int TributeAmount => Day <= 7 ? settings.firstTribute : Day <= 14 ? settings.secondTribute : settings.laterTribute;
    public int DaysUntilTribute => Day % 7 == 0 && Phase != DystopiaPhase.Settlement ? 0 : 7 - Day % 7;
    public bool CanBuy => Phase == DystopiaPhase.Settlement && Cash >= settings.citizenshipPrice;

    /// <summary>設定を参照し、独立した再現可能なランを開始します。</summary>
    /// <param name="settings">Scene が所有する設定。</param><param name="seed">検証用乱数 seed。</param>
    public DystopiaSession(DystopiaSettings settings, int seed)
    {
        this.settings = settings;
        random = new System.Random(seed);
        BeginDay();
    }

    /// <summary>案内を閉じて営業を開始します。営業中は価格表を再表示しません。</summary>
    public void OpenShop()
    {
        if (Phase != DystopiaPhase.PriceGuide || IsPaused) return;
        Phase = DystopiaPhase.Trading;
        Revision++;
    }

    /// <summary>営業と結果表示だけに経過時間を適用します。</summary>
    /// <param name="seconds">非負の経過秒数。</param>
    public void Tick(float seconds)
    {
        if (IsPaused || seconds <= 0) return;
        if (Phase == DystopiaPhase.Result)
        {
            resultRemaining -= seconds;
            if (resultRemaining <= 0) AdvanceCustomer();
            return;
        }
        if (Phase != DystopiaPhase.Trading) return;
        Gauge = Mathf.Max(0, Gauge - settings.gaugeDecay * seconds);
        if (Gauge > 0 || Remaining <= 1) return;
        // 活性客は残し、後ろの待機客だけを一度に最大2人除きます。
        int count = Math.Min(settings.departureCount, Remaining - 1);
        Remaining -= count;
        Departed += count;
        Reputation = Mathf.Clamp(Reputation + settings.departureReputation, 0, 100);
        Gauge = settings.departureRecovery;
        Feedback = $"기다리던 {count}명이 돌아갔습니다.";
        Reason = $"대기 시간 초과 · {count}명 이탈 · 명성 {settings.departureReputation}";
        Revision++;
    }

    /// <summary>整数量のみを受け付け、1つの取引を一度だけ決済します。</summary>
    /// <param name="input">入力された総販売額。</param><returns>有効な確定を処理した場合 true。</returns>
    public bool Confirm(string input)
    {
        if (Phase != DystopiaPhase.Trading || IsPaused || string.IsNullOrEmpty(input) || input.Length > 7) return false;
        foreach (char c in input) if (c < '0' || c > '9') return false;
        if (!int.TryParse(input, out int price) || price <= 0) return false;
        bool exceedsTolerance = (long)price * 100 > (long)Customer.total * Customer.tolerancePercent;
        bool exceedsBudget = price > Customer.budget;
        int repDelta = 0, moralDelta;
        LastAccepted = !exceedsTolerance && !exceedsBudget;
        if (LastAccepted)
        {
            Cash += price;
            Revenue += price;
            Sold++;
            repDelta = Gauge >= settings.greenThreshold ? settings.greenReputation : 0;
            moralDelta = price > Customer.total ? settings.markupMorality : price == Customer.total ? 0 :
                (long)price * 100 <= (long)Customer.total * 80 ? settings.generousMorality : settings.discountMorality;
            Feedback = moralDelta > 0 ? "정말 고마워요. 오늘은 버틸 수 있겠네요." : moralDelta < 0 ? "…비싸군요. 그래도 가져가겠습니다." : "고맙습니다. 조심히 계세요.";
            Reason = $"판매 +{price:N0}원 · " + (moralDelta > 0 ? "할인 배려" : moralDelta < 0 ? "비싼 가격" : "정상 거래");
            Gauge = Mathf.Min(100, Gauge + settings.gaugeRecovery);
        }
        else
        {
            Refused++;
            repDelta = exceedsTolerance ? settings.toleranceReputation : settings.budgetReputation;
            moralDelta = settings.refusalMorality;
            Feedback = exceedsTolerance ? "이 가격은 너무하군요. 다른 곳에 가겠어요." : "집에 아이가 기다리는데… 가진 돈이 모자라요.";
            Reason = exceedsTolerance ? "가격을 받아들이지 못함 · 판매 거절" : "손님의 예산 부족 · 판매 거절";
        }
        Reputation = Mathf.Clamp(Reputation + repDelta, 0, 100);
        Morality = Mathf.Clamp(Morality + moralDelta, 0, 100);
        Reason += $" · 명성 {repDelta:+0;-0;0} / 도덕성 {moralDelta:+0;-0;0}";
        Phase = DystopiaPhase.Result;
        resultRemaining = settings.resultSeconds;
        Revision++;
        return true;
    }

    /// <summary>未払いの週次費用を一度だけ精算し、不足時は失敗にします。</summary>
    public void PayTribute()
    {
        if (Phase != DystopiaPhase.Tribute || IsPaused) return;
        if (Cash < TributeAmount) Phase = DystopiaPhase.Failed;
        else { Cash -= TributeAmount; Phase = DystopiaPhase.Settlement; }
        Revision++;
    }

    /// <summary>精算を終え、累積状態を維持して翌日を始めます。</summary>
    public void NextDay()
    {
        if (Phase != DystopiaPhase.Settlement || IsPaused) return;
        Day++;
        BeginDay();
    }

    /// <summary>当日の上納後にのみ市民権を購入します。</summary>
    public void BuyCitizenship()
    {
        if (!CanBuy || IsPaused) return;
        Cash -= settings.citizenshipPrice;
        Phase = DystopiaPhase.Goal;
        Revision++;
    }

    /// <summary>ランの時間だけを停止します。Unity 全体の timeScale は変更しません。</summary>
    public void TogglePause() { IsPaused = !IsPaused; Revision++; }

    /// <summary>当日のみの集計と待機状態を初期化します。</summary>
    private void BeginDay()
    {
        dayStartReputation = Reputation;
        dayStartMorality = Morality;
        Visitors = Mathf.Clamp(settings.baseVisitors + (Reputation - 50) / 10, settings.minVisitors, settings.maxVisitors);
        Remaining = Visitors;
        Sold = Refused = Departed = Revenue = 0;
        Gauge = settings.gaugeStart;
        Feedback = Reason = "";
        Customer = CreateCustomer();
        Phase = Array.Exists(settings.products, p => p.firstDay == Day) ? DystopiaPhase.PriceGuide : DystopiaPhase.Trading;
        Revision++;
    }

    /// <summary>完了した客を消費して次の客または終業へ進めます。</summary>
    private void AdvanceCustomer()
    {
        Remaining--;
        if (Remaining <= 0) Phase = Day % 7 == 0 ? DystopiaPhase.Tribute : DystopiaPhase.Settlement;
        else { Customer = CreateCustomer(); Phase = DystopiaPhase.Trading; }
        Revision++;
    }

    /// <summary>導入済み商品の重複しない組合せと客の経済状況を生成します。</summary>
    /// <returns>購入商品と非公開予算を持つ客。</returns>
    private DystopiaCustomer CreateCustomer()
    {
        var customer = new DystopiaCustomer { isPoor = random.NextDouble() < settings.poorChance };
        customer.appearance = customer.isPoor ? 0 : random.Next(1, 4);
        customer.tolerancePercent = customer.isPoor ? 110 : 110 + customer.appearance * 10;
        var available = new List<DystopiaProduct>();
        foreach (var product in settings.products) if (product.firstDay <= Day) available.Add(product);
        int count = random.Next(1, Day < 3 ? 3 : 4);
        for (int i = 0; i < count; i++)
        {
            int index = random.Next(available.Count);
            var line = new DystopiaBasketLine { product = available[index], quantity = random.Next(1, 4) };
            available.RemoveAt(index);
            customer.basket.Add(line);
            customer.total += line.product.price * line.quantity;
        }
        customer.budget = customer.isPoor ? (int)(customer.total * settings.poorBudgetRatio) :
            customer.total * random.Next(settings.normalBudgetMinPercent, settings.normalBudgetMaxPercent + 1) / 100;
        return customer;
    }
}
