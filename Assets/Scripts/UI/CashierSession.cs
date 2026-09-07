using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cashier settings and balance configuration based on the prototype.
/// </summary>
[Serializable]
public sealed class CashierSettings
{
    public CashierProduct[] products = {
        new CashierProduct("Bottled Water", 1000, 1),
        new CashierProduct("Hardtack", 1500, 1),
        new CashierProduct("Canned Food", 2500, 1),
        new CashierProduct("Instant Rice", 2000, 1),
        new CashierProduct("Bandage", 3000, 3),
        new CashierProduct("Painkiller", 4000, 3),
        new CashierProduct("Battery", 3500, 3),
        new CashierProduct("Soap", 1000, 5),
        new CashierProduct("Dust Mask", 2000, 5),
        new CashierProduct("Fuel Can", 5000, 5)
    };

    public int citizenshipPrice = 300000;
    public int firstTribute = 50000;
    public int secondTribute = 80000;
    public int laterTribute = 110000;

    public int baseVisitors = 8;
    public int minVisitors = 6;
    public int maxVisitors = 12;

    public float gaugeStart = 70f;
    public float greenThreshold = 70f;
    public float gaugeDecay = 2f;
    public float gaugeRecovery = 12f;
    public float departureRecovery = 30f;

    public int departureCount = 2;
    public int departureReputation = -5;
    public int greenReputation = 1;

    public int toleranceReputation = -2;
    public int budgetReputation = -1;
    public int refusalMorality = -1;

    public int discountMorality = 2;
    public int generousMorality = 4;
    public int markupMorality = -2;

    public float poorChance = 0.18f;
    public float poorBudgetRatio = 0.85f;
    public int normalBudgetMinPercent = 110;
    public int normalBudgetMaxPercent = 150;

    public float resultSeconds = 0.85f;
}

/// <summary>
/// Individual product definition conforming to DATA_RULES.md with name, price, intro day, PK idx, and sprite/resource references.
/// </summary>
[Serializable]
public sealed class CashierProduct
{
    public uint idx;
    public string id;
    public string name;
    public int price;
    public int firstDay;
    public uint resourceIdx;
    public string resourcePath;
    public Sprite sprite;

    public CashierProduct(string name, int price, int firstDay, uint idx = 0, string id = "", uint resourceIdx = 0, string resourcePath = "", Sprite sprite = null)
    {
        this.name = name;
        this.price = price;
        this.firstDay = firstDay;
        this.idx = idx;
        this.id = string.IsNullOrEmpty(id) ? name.Replace(" ", "") : id;
        this.resourceIdx = resourceIdx;
        this.resourcePath = resourcePath;
        this.sprite = sprite;
    }
}

public enum CashierPhase
{
    PriceGuide,
    Trading,
    Result,
    Settlement,
    Tribute,
    Goal,
    Failed
}

public sealed class CashierBasketLine
{
    public CashierProduct product;
    public int quantity;
}

public sealed class CashierCustomer
{
    public readonly List<CashierBasketLine> basket = new List<CashierBasketLine>();
    public int total;
    public int budget;
    public int tolerancePercent;
    public int appearance;
    public bool isPoor;
}

/// <summary>
/// Pure C# Session Engine porting DystopiaSession from the prototype.
/// Manages 7 phases, customer generation, price memory, queue gauge, tolerance, and tribute.
/// </summary>
public sealed class CashierSession
{
    private readonly CashierSettings settings;
    private readonly System.Random random;
    private float resultRemaining;
    private int dayStartReputation, dayStartMorality;

    public CashierPhase Phase { get; private set; }
    public CashierCustomer Customer { get; private set; }
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

    public int ReputationChange => this.Reputation - this.dayStartReputation;
    public int MoralityChange => this.Morality - this.dayStartMorality;
    public float Gauge { get; private set; }
    public bool IsPaused { get; private set; }
    public bool LastAccepted { get; private set; }
    public string Feedback { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public int Revision { get; private set; }

    public int TributeAmount => this.Day <= 7 ? this.settings.firstTribute : this.Day <= 14 ? this.settings.secondTribute : this.settings.laterTribute;
    public int DaysUntilTribute => this.Day % 7 == 0 && this.Phase != CashierPhase.Settlement ? 0 : 7 - (this.Day % 7);
    public bool CanBuy => this.Phase == CashierPhase.Settlement && this.Cash >= this.settings.citizenshipPrice;

    public CashierSession(CashierSettings settings, int seed)
    {
        this.settings = settings;
        this.random = new System.Random(seed);
        this.beginDay();
    }

    public void OpenShop()
    {
        if (this.Phase != CashierPhase.PriceGuide || this.IsPaused) return;
        this.Phase = CashierPhase.Trading;
        this.Revision++;
    }

    public void Tick(float seconds)
    {
        if (this.IsPaused || seconds <= 0) return;

        if (this.Phase == CashierPhase.Trading)
        {
            this.Gauge = Mathf.Max(0, this.Gauge - (this.settings.gaugeDecay * seconds));
            if (this.Gauge <= 0 && this.Remaining > 0)
            {
                int drop = Math.Min(this.settings.departureCount, this.Remaining);
                this.Departed += drop;
                this.Remaining -= drop;
                this.Reputation = Mathf.Clamp(this.Reputation + this.settings.departureReputation, 0, 100);
                this.Gauge = this.settings.departureRecovery;
                this.Feedback = "The queue was exhausted. Waiting customers left angrily.";
                this.Reason = $"Queue collapse · {drop} customers departed · Rep {this.settings.departureReputation:+0;-0;0}";
                if (this.Remaining <= 0)
                {
                    this.endTrading();
                }
                else
                {
                    this.spawnCustomer();
                }
                this.Revision++;
            }
        }
        else if (this.Phase == CashierPhase.Result)
        {
            this.resultRemaining -= seconds;
            if (this.resultRemaining <= 0)
            {
                if (this.Remaining > 0)
                {
                    this.Phase = CashierPhase.Trading;
                    this.spawnCustomer();
                }
                else
                {
                    this.endTrading();
                }
                this.Revision++;
            }
        }
    }

    public bool Confirm(string input)
    {
        if (this.Phase != CashierPhase.Trading || this.IsPaused || string.IsNullOrEmpty(input) || input.Length > 7) return false;
        foreach (char c in input) if (c < '0' || c > '9') return false;
        if (!int.TryParse(input, out int price) || price <= 0) return false;

        bool exceedsTolerance = (long)price * 100 > (long)this.Customer.total * this.Customer.tolerancePercent;
        bool exceedsBudget = price > this.Customer.budget;
        int repDelta = 0;
        int moralDelta;

        this.LastAccepted = !exceedsTolerance && !exceedsBudget;
        if (this.LastAccepted)
        {
            this.Cash += price;
            this.Revenue += price;
            this.Sold++;

            repDelta = this.Gauge >= this.settings.greenThreshold ? this.settings.greenReputation : 0;
            moralDelta = price > this.Customer.total ? this.settings.markupMorality :
                         price == this.Customer.total ? 0 :
                         ((long)price * 100 <= (long)this.Customer.total * 80 ? this.settings.generousMorality : this.settings.discountMorality);

            this.Feedback = moralDelta > 0 ? "Thank you so much! We can survive today thanks to your kindness." :
                            moralDelta < 0 ? "...It's quite expensive. But I have no choice, I will take it." :
                            "Thank you. Take care.";

            this.Reason = $"Sale +{price:N0} G · " + (moralDelta > 0 ? "Generous Discount" : moralDelta < 0 ? "Expensive Markup" : "Normal Transaction");
            this.Gauge = Mathf.Min(100, this.Gauge + this.settings.gaugeRecovery);
        }
        else
        {
            this.Refused++;
            repDelta = exceedsTolerance ? this.settings.toleranceReputation : this.settings.budgetReputation;
            moralDelta = this.settings.refusalMorality;

            this.Feedback = exceedsTolerance ? "This price is absurd! I am leaving." :
                            "My child is waiting at home... but I don't have enough money.";
            this.Reason = exceedsTolerance ? "Price rejected (Tolerance exceeded)" : "Insufficient budget (Sale refused)";
        }

        this.Reputation = Mathf.Clamp(this.Reputation + repDelta, 0, 100);
        this.Morality = Mathf.Clamp(this.Morality + moralDelta, 0, 100);
        this.Reason += $" · Rep {repDelta:+0;-0;0} / Morality {moralDelta:+0;-0;0}";

        this.Phase = CashierPhase.Result;
        this.resultRemaining = this.settings.resultSeconds;
        this.Revision++;
        return true;
    }

    public void PayTribute()
    {
        if (this.Phase != CashierPhase.Tribute || this.IsPaused) return;

        if (this.Cash >= this.TributeAmount)
        {
            this.Cash -= this.TributeAmount;
            this.Phase = CashierPhase.Settlement;
        }
        else
        {
            this.Phase = CashierPhase.Failed;
        }
        this.Revision++;
    }

    public void NextDay()
    {
        if (this.Phase != CashierPhase.Settlement || this.IsPaused) return;
        this.Day++;
        this.beginDay();
    }

    public void BuyCitizenship()
    {
        if (!this.CanBuy || this.IsPaused) return;
        this.Cash -= this.settings.citizenshipPrice;
        this.Phase = CashierPhase.Goal;
        this.Revision++;
    }

    public void TogglePause()
    {
        this.IsPaused = !this.IsPaused;
        this.Revision++;
    }

    private void beginDay()
    {
        this.dayStartReputation = this.Reputation;
        this.dayStartMorality = this.Morality;
        this.Sold = this.Refused = this.Departed = this.Revenue = 0;
        this.Gauge = this.settings.gaugeStart;

        int repBonus = (this.Reputation - 50) / 10;
        this.Visitors = Mathf.Clamp(this.settings.baseVisitors + repBonus, this.settings.minVisitors, this.settings.maxVisitors);
        this.Remaining = this.Visitors;

        this.spawnCustomer();
        this.Phase = CashierPhase.PriceGuide;
        this.Revision++;
    }

    private void endTrading()
    {
        this.Phase = (this.Day % 7 == 0) ? CashierPhase.Tribute : CashierPhase.Settlement;
    }

    private void spawnCustomer()
    {
        this.Customer = new CashierCustomer();
        var available = new List<CashierProduct>();
        foreach (var p in this.settings.products)
        {
            if (p.firstDay <= this.Day) available.Add(p);
        }

        int count = this.random.Next(1, Math.Min(available.Count, 3) + 1);
        var chosen = new HashSet<int>();
        while (chosen.Count < count)
        {
            chosen.Add(this.random.Next(available.Count));
        }

        int total = 0;
        foreach (int idx in chosen)
        {
            var p = available[idx];
            int qty = this.random.Next(1, 3);
            this.Customer.basket.Add(new CashierBasketLine { product = p, quantity = qty });
            total += p.price * qty;
        }

        this.Customer.total = total;
        this.Customer.isPoor = this.random.NextDouble() < this.settings.poorChance;
        this.Customer.appearance = this.random.Next(4);

        if (this.Customer.isPoor)
        {
            this.Customer.budget = Math.Max(1, (int)(total * this.settings.poorBudgetRatio));
            this.Customer.tolerancePercent = 100;
        }
        else
        {
            int budgetPct = this.random.Next(this.settings.normalBudgetMinPercent, this.settings.normalBudgetMaxPercent + 1);
            this.Customer.budget = Math.Max(total, (int)((long)total * budgetPct / 100));
            this.Customer.tolerancePercent = this.random.Next(110, 140);
        }

        this.Remaining--;
    }
}
