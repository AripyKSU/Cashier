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
    /// <summary>초기 대기열은 명성 50일 때 8명이며, 명성 10점당 1명 변화합니다. 하루 총 손님 제한은 아닙니다.</summary>
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
    private bool[] includedUnits;

    /// <summary>현재 판매 대상으로 남아 있는 같은 품목의 수량입니다.</summary>
    internal int IncludedQuantity
    {
        get
        {
            EnsureSelection();
            int count = 0;
            foreach (bool included in includedUnits) if (included) count++;
            return count;
        }
    }

    /// <summary>개별 물품의 판매 포함 상태를 반환합니다.</summary>
    /// <param name="unitIndex">같은 품목 안의 0부터 시작하는 개별 번호입니다.</param>
    /// <returns>판매 대상이면 true입니다.</returns>
    internal bool IsIncluded(int unitIndex)
    {
        EnsureSelection();
        return unitIndex >= 0 && unitIndex < includedUnits.Length && includedUnits[unitIndex];
    }

    /// <summary>개별 물품의 판매 포함 상태를 전환합니다.</summary>
    /// <param name="unitIndex">같은 품목 안의 0부터 시작하는 개별 번호입니다.</param>
    /// <returns>유효한 물품을 전환했으면 true입니다.</returns>
    internal bool ToggleIncluded(int unitIndex)
    {
        EnsureSelection();
        if (unitIndex < 0 || unitIndex >= includedUnits.Length) return false;
        includedUnits[unitIndex] = !includedUnits[unitIndex];
        return true;
    }

    /// <summary>기존 수량을 처음 읽을 때 모든 물품을 판매 대상으로 초기화합니다.</summary>
    private void EnsureSelection()
    {
        if (includedUnits != null && includedUnits.Length == quantity) return;
        includedUnits = new bool[Math.Max(0, quantity)];
        for (int i = 0; i < includedUnits.Length; i++) includedUnits[i] = true;
    }
}

/// <summary>현재 방문객의 장바구니, 예산, 허용치 및 외형 선택입니다.</summary>
public sealed class DystopiaCustomer
{
    public readonly List<DystopiaBasketLine> basket = new List<DystopiaBasketLine>();
    public int total, budget, tolerancePercent, appearance;
    public bool isPoor;
    /// <summary>선택된 남성·여성 외형 묶음과 성별 판매 지침 판정이 함께 사용하는 값입니다.</summary>
    internal bool IsMale { get; set; }
}

/// <summary>Scene 수명에 한정된 런 상태와 거래·시간·상납 불변 조건을 소유합니다.</summary>
public sealed class DystopiaSession
{
    private const int MaleAppearanceCount = 24;
    private const int FemaleAppearanceCount = 16;
    private static readonly string[] ProductNames = { "생수", "건빵", "통조림", "즉석밥" };
    private readonly DystopiaSettings settings;
    private readonly System.Random random;
    // 당일 방문 순서를 보존해 화면의 대기 손님과 실제 거래 손님을 일치시킵니다.
    private readonly List<DystopiaCustomer> waitingCustomers = new List<DystopiaCustomer>();
    /// <summary>현재 손님 뒤의 실제 대기 순서입니다.</summary>
    public IReadOnlyList<DystopiaCustomer> WaitingCustomers => waitingCustomers;
    private readonly DystopiaProduct[] activeProducts;
    private readonly List<string> ruleViolationHistory = new List<string>();
    private float resultRemaining;
    // 매일 영업 시작 때 열고, 마감 시각에 닫는 추가 손님 접수 상태입니다.
    private bool isAcceptingCustomers;
    private int dayStartReputation, dayStartMorality;
    // 거래 기록 객체가 없는 현재 구조에서 당일 가계부 표시에 필요한 합계만 보존합니다.
    private int markupTransactions, markupAmount, paidTributeToday;
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
    internal int Cancelled { get; private set; }
    public int Departed { get; private set; }
    public int Revenue { get; private set; }
    public int ReputationChange => Reputation - dayStartReputation;
    public int MoralityChange => Morality - dayStartMorality;
    public float Gauge { get; private set; }
    public bool IsPaused { get; private set; }
    public bool LastAccepted { get; private set; }
    internal bool LastCancelled { get; private set; }
    public string Feedback { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public int Revision { get; private set; }
    public int TributeAmount => Day <= 7 ? settings.firstTribute : Day <= 14 ? settings.secondTribute : settings.laterTribute;
    public int DaysUntilTribute => Day % 7 == 0 && Phase != DystopiaPhase.Settlement ? 0 : 7 - Day % 7;
    public bool CanBuy => Phase == DystopiaPhase.Settlement && Cash >= settings.citizenshipPrice;
    /// <summary>기존 완료 기준인 판매·거절을 중복 없이 합산한 당일 응대 인원입니다.</summary>
    internal int HandledCustomers => Sold + Refused + Cancelled;
    /// <summary>정가보다 높은 금액으로 완료된 당일 거래 수입니다.</summary>
    internal int MarkupTransactions => markupTransactions;
    /// <summary>완료 거래별 실제 결제액과 정가의 양수 차액을 합산한 참고 금액입니다.</summary>
    internal int MarkupAmount => markupAmount;
    /// <summary>오늘 실제로 현금에서 차감된 상납금입니다.</summary>
    internal int PaidTributeToday => paidTributeToday;
    /// <summary>현재 일자의 실제 판매 위반 거래 수입니다. 한 거래에서는 한 번만 증가합니다.</summary>
    internal int RuleViolationCount { get; private set; }
    /// <summary>가장 최근 성사 거래에서 위반한 당일 지침 전체입니다.</summary>
    internal string LastRuleViolation { get; private set; } = "";
    /// <summary>현재 일자에 적발된 거래별 위반 내용을 발생 순서대로 보존합니다.</summary>
    internal IReadOnlyList<string> RuleViolationHistory => ruleViolationHistory;
    /// <summary>이 시스템에서 사용하는 네 품목을 기존 설정의 가격과 Sprite 그대로 제공합니다.</summary>
    internal IReadOnlyList<DystopiaProduct> ActiveProducts => activeProducts;
    /// <summary>현재 날짜에만 적용할 지침 문구입니다.</summary>
    internal string DailyRuleText => GetDailyRuleText(Day);
    /// <summary>가장 가까운 다음 상납일에 적용될 기존 설정 금액입니다.</summary>
    internal int NextTributeAmount
    {
        get
        {
            int dueDay = Day + DaysUntilTribute;
            return dueDay <= 7 ? settings.firstTribute : dueDay <= 14 ? settings.secondTribute : settings.laterTribute;
        }
    }

    /// <summary>設定を参照し、独立した再現可能なランを開始します。</summary>
    /// <param name="settings">Scene が所有する設定。</param><param name="seed">検証用乱数 seed。</param>
    public DystopiaSession(DystopiaSettings settings, int seed)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        activeProducts = FindActiveProducts(settings.products);
        random = new System.Random(seed);
        BeginDay();
    }

    /// <summary>거래 중 개별 물품을 판매 대상에서 제외하거나 복구합니다.</summary>
    /// <param name="lineIndex">장바구니 품목 줄 번호입니다.</param>
    /// <param name="unitIndex">해당 줄 안의 개별 물품 번호입니다.</param>
    /// <returns>유효한 물품의 상태가 바뀌었으면 true입니다.</returns>
    internal bool ToggleBasketUnit(int lineIndex, int unitIndex)
    {
        if (Phase != DystopiaPhase.Trading || IsPaused || lineIndex < 0 || lineIndex >= Customer.basket.Count) return false;
        if (!Customer.basket[lineIndex].ToggleIncluded(unitIndex)) return false;
        RecalculateSelectedTotal();
        Revision++;
        return true;
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
        waitingCustomers.RemoveRange(waitingCustomers.Count - count, count);
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
        if (Phase != DystopiaPhase.Trading || IsPaused) return false;
        if (Customer.total == 0)
        {
            CancelEmptyTransaction();
            return true;
        }
        if (string.IsNullOrEmpty(input) || input.Length > 7) return false;
        foreach (char c in input) if (c < '0' || c > '9') return false;
        if (!int.TryParse(input, out int price) || price <= 0) return false;
        LastCancelled = false;
        LastRuleViolation = "";
        bool exceedsTolerance = (long)price * 100 > (long)Customer.total * Customer.tolerancePercent;
        bool exceedsBudget = price > Customer.budget;
        int repDelta = 0, moralDelta;
        LastAccepted = !exceedsTolerance && !exceedsBudget;
        if (LastAccepted)
        {
            Cash += price;
            Revenue += price;
            Sold++;
            int markup = Math.Max(0, price - Customer.total);
            if (markup > 0)
            {
                markupTransactions++;
                markupAmount += markup;
            }
            repDelta = Gauge >= settings.greenThreshold ? settings.greenReputation : 0;
            moralDelta = price > Customer.total ? settings.markupMorality : price == Customer.total ? 0 :
                (long)price * 100 <= (long)Customer.total * 80 ? settings.generousMorality : settings.discountMorality;
            Feedback = moralDelta > 0 ? "정말 고마워요. 오늘은 버틸 수 있겠네요." : moralDelta < 0 ? "…비싸군요. 그래도 가져가겠습니다." : "고맙습니다. 조심히 계세요.";
            Reason = $"판매 +{price:N0}원 · " + (moralDelta > 0 ? "할인 배려" : moralDelta < 0 ? "비싼 가격" : "정상 거래");
            LastRuleViolation = EvaluateRuleViolations();
            if (!string.IsNullOrEmpty(LastRuleViolation))
            {
                RuleViolationCount++;
                ruleViolationHistory.Add($"{RuleViolationCount}회: {LastRuleViolation}");
                Reason += $" · 지침 위반: {LastRuleViolation} · 오늘 적발 {RuleViolationCount}회";
            }
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
        else
        {
            paidTributeToday = TributeAmount;
            Cash -= paidTributeToday;
            Phase = DystopiaPhase.Settlement;
        }
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
        isAcceptingCustomers = true;
        Sold = Refused = Cancelled = Departed = Revenue = 0;
        markupTransactions = markupAmount = paidTributeToday = 0;
        RuleViolationCount = 0;
        LastRuleViolation = "";
        ruleViolationHistory.Clear();
        LastCancelled = false;
        Gauge = settings.gaugeStart;
        Feedback = Reason = "";
        Customer = CreateCustomer(Customer);
        waitingCustomers.Clear();
        for (int i = 1; i < Visitors; i++) waitingCustomers.Add(CreateCustomer(i == 1 ? Customer : waitingCustomers[i - 2]));
        Phase = DystopiaPhase.PriceGuide;
        Revision++;
    }

    /// <summary>마감 시각의 응대 손님만 남기고 추가 접수를 끝냅니다. 대기 시간 이탈 벌점은 적용하지 않습니다.</summary>
    public void StopAcceptingCustomers()
    {
        if ((Phase != DystopiaPhase.Trading && Phase != DystopiaPhase.Result) || !isAcceptingCustomers) return;
        isAcceptingCustomers = false;
        waitingCustomers.Clear();
        Remaining = 1;
        Revision++;
    }

    /// <summary>접수 중에는 다음 손님을 계속 보충하고, 마감 후 마지막 거래가 끝나면 정산으로 진행합니다.</summary>
    private void AdvanceCustomer()
    {
        Remaining--;
        if (isAcceptingCustomers)
        {
            // 초기 방문 수를 다 처리해도 영업 시간이 남아 있으면 대기 순서를 이어 갑니다.
            waitingCustomers.Add(CreateCustomer(waitingCustomers.Count > 0 ? waitingCustomers[waitingCustomers.Count - 1] : Customer));
            Remaining++;
            Visitors++;
        }
        if (Remaining <= 0) Phase = Day % 7 == 0 ? DystopiaPhase.Tribute : DystopiaPhase.Settlement;
        else
        {
            Customer = waitingCustomers[0];
            waitingCustomers.RemoveAt(0);
            Phase = DystopiaPhase.Trading;
        }
        Revision++;
    }

    /// <summary>앞 손님과 다른 외형을 선택하고 구매 품목과 경제 상황을 생성합니다.</summary>
    /// <param name="previous">실제 대기 순서에서 바로 앞에 있는 손님입니다. 첫 생성에는 null입니다.</param>
    /// <returns>購入商品と非公開予算を持つ客。</returns>
    private DystopiaCustomer CreateCustomer(DystopiaCustomer previous)
    {
        var customer = new DystopiaCustomer { isPoor = random.NextDouble() < settings.poorChance };
        int toleranceType = customer.isPoor ? 0 : random.Next(1, 4);
        customer.IsMale = random.Next(2) == 0;
        int appearanceCount = customer.IsMale ? MaleAppearanceCount : FemaleAppearanceCount;
        bool skipPrevious = previous != null && previous.IsMale == customer.IsMale;
        // 이전 외형을 후보에서 제외하여 재추첨 반복 없이 연속 중복을 막습니다.
        customer.appearance = random.Next(appearanceCount - (skipPrevious ? 1 : 0));
        if (skipPrevious && customer.appearance >= previous.appearance) customer.appearance++;
        customer.tolerancePercent = customer.isPoor ? 110 : 110 + toleranceType * 10;
        var available = new List<DystopiaProduct>(activeProducts);
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

    /// <summary>설정에서 요청된 네 품목을 명시된 순서대로 찾아 기존 가격과 Sprite 참조를 재사용합니다.</summary>
    /// <param name="products">Scene이 소유한 기존 상품 설정입니다.</param>
    /// <returns>생수, 건빵, 통조림, 즉석밥 순서의 상품 배열입니다.</returns>
    private static DystopiaProduct[] FindActiveProducts(DystopiaProduct[] products)
    {
        if (products == null) throw new InvalidOperationException("일일지침에 사용할 상품 설정이 없습니다.");
        var result = new DystopiaProduct[ProductNames.Length];
        for (int i = 0; i < ProductNames.Length; i++)
        {
            foreach (DystopiaProduct product in products)
            {
                if (product != null && product.name == ProductNames[i])
                {
                    result[i] = product;
                    break;
                }
            }
            if (result[i] == null) throw new InvalidOperationException($"일일지침 품목 '{ProductNames[i]}'이 기존 설정에 없습니다.");
        }
        return result;
    }

    /// <summary>판매 대상으로 남은 개별 물품만 합산해 기존 거래 기준 금액을 갱신합니다.</summary>
    private void RecalculateSelectedTotal()
    {
        int total = 0;
        foreach (DystopiaBasketLine line in Customer.basket) total += line.product.price * line.IncludedQuantity;
        Customer.total = total;
    }

    /// <summary>모든 물품을 제외한 거래를 수익과 위반 없이 완료 처리합니다.</summary>
    private void CancelEmptyTransaction()
    {
        Cancelled++;
        LastAccepted = false;
        LastCancelled = true;
        LastRuleViolation = "";
        Feedback = "판매할 물품이 없어 거래를 취소했습니다.";
        Reason = "거래 취소 · 판매수익 0원 · 지침 위반 없음";
        Phase = DystopiaPhase.Result;
        resultRemaining = settings.resultSeconds;
        Revision++;
    }

    /// <summary>현재 날짜와 최종 판매 수량으로 위반한 지침을 한 번에 계산합니다.</summary>
    /// <returns>위반이 없으면 빈 문자열, 있으면 위반 문구 전체입니다.</returns>
    private string EvaluateRuleViolations()
    {
        int water = IncludedQuantity("생수");
        int crackers = IncludedQuantity("건빵");
        int cannedFood = IncludedQuantity("통조림");
        int instantRice = IncludedQuantity("즉석밥");
        var violations = new List<string>(2);
        if ((Day == 2 || Day == 6) && Customer.IsMale && cannedFood > 0) violations.Add("남성에게 통조림 판매 금지");
        if (Day == 3 && !Customer.IsMale && instantRice > 0) violations.Add("여성에게 즉석밥 판매 금지");
        if ((Day == 4 || Day == 6) && water > 1) violations.Add("생수는 손님 1명당 1개까지");
        if (Day == 5 && crackers > 0 && instantRice > 0) violations.Add("건빵과 즉석밥 함께 판매 금지");
        return string.Join(" / ", violations);
    }

    /// <summary>최종 판매 대상으로 남은 특정 품목의 수량을 반환합니다.</summary>
    /// <param name="productName">기존 상품 표시 이름입니다.</param>
    /// <returns>판매 대상 수량입니다.</returns>
    private int IncludedQuantity(string productName)
    {
        foreach (DystopiaBasketLine line in Customer.basket)
            if (line.product.name == productName) return line.IncludedQuantity;
        return 0;
    }

    /// <summary>이전 날짜 지침을 누적하지 않고 지정 날짜 하나의 문구만 반환합니다.</summary>
    /// <param name="day">영업 일차입니다.</param>
    /// <returns>당일 표시 문구입니다.</returns>
    private static string GetDailyRuleText(int day)
    {
        switch (day)
        {
            case 1: return "제한 없음.";
            case 2: return "남성에게 통조림 판매 금지.";
            case 3: return "여성에게 즉석밥 판매 금지.";
            case 4: return "생수는 손님 1명당 1개까지 판매 가능.";
            case 5: return "건빵과 즉석밥은 함께 판매 금지.";
            case 6: return "남성에게 통조림 판매 금지.\n생수는 손님 1명당 1개까지 판매 가능.";
            default: return "등록된 지침 없음.";
        }
    }
}
