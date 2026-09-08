using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>실제 CSV로 대기열 시간·소유권·일시정지 계약을 검사한다.</summary>
public sealed class CustomerQueueTests
{
    private CustomerQueue queue;
    private CustomerDispositionData config;
    private uint price;
    private int created;

    /// <summary>9초 대기 성향과 5초 입장 난수원을 매번 새로 구성한다.</summary>
    [SetUp]
    public void SetUp()
    {
        var dispositions = Util.ParseFromCSV<CustomerDispositionData>(File.ReadAllText("Assets/Datas/Customer/CustomerDispositionData.csv")).ToDictionary(x => x.Idx);
        var products = Util.ParseFromCSV<ProductData>(File.ReadAllText("Assets/Datas/Customer/ProductData.csv")).ToDictionary(x => x.Idx);
        config = dispositions[6002]; var generator = new CustomerGenerator(new Random(1)); price = 100; created = 0;
        queue = new CustomerQueue(() => { created++; return generator.Generate(new uint[] { 5001 }, new[] { config }, products, getCurrentPrices: () => products.ToDictionary(x => x.Key, x => price)); }, dispositions);
        queue.Start(); queue.TryAdd();
    }

    /// <summary>재촉·이탈·불만은 지정 시각에 한 번 처리한다.</summary>
    [Test]
    public void WarningExpiryAndComplaintLifetime()
    {
        var first = queue.Waiting[0]; queue.Advance(200, true);
        Assert.That(queue.Waiting.Count, Is.EqualTo(1)); queue.Advance(2, false); Assert.That(queue.GetSpeech(first), Is.Zero);
        queue.Advance(1, false); Assert.That(queue.GetSpeech(first), Is.EqualTo(config.QueueWarningTextIdx));
        queue.Advance(3, false); Assert.That(queue.GetSpeech(first), Is.Zero);
        queue.Advance(3, false); Assert.That(first.Visit.State, Is.EqualTo(CustomerState.Abandoned)); Assert.That(queue.GetSpeech(first), Is.EqualTo(config.QueueLeaveTextIdx));
        queue.Advance(3, false); Assert.That(queue.Leaving.Contains(first), Is.False);
    }

    /// <summary>입장 주기·상한·FIFO·가격 snapshot과 계산대 제외를 검사한다.</summary>
    [Test]
    public void CapacityFifoAndCounterOwnership()
    {
        queue.Advance(4, false); Assert.That(created, Is.EqualTo(1)); queue.Advance(1, false); Assert.That(created, Is.EqualTo(2));
        while (queue.TryAdd()) { }
        Assert.That(created, Is.EqualTo(10)); Assert.That(queue.TryAdd(), Is.False);
        var first = queue.Waiting[0].Visit; price = 999;
        Assert.That(queue.TakeNext(), Is.SameAs(first)); first.BeginOffer(); queue.Advance(200, false);
        Assert.That(first.State, Is.EqualTo(CustomerState.AwaitingOffer)); Assert.That(first.Items.All(x => x.UnitPrice == 100));
        Assert.That(queue.Waiting.Any(x => x.Visit.Items.All(y => y.UnitPrice == 999)));
        var remaining = queue.Waiting.Select(x => x.Visit).ToArray(); queue.Stop();
        Assert.That(queue.Waiting, Is.Empty); Assert.That(queue.Leaving, Is.Empty); Assert.That(remaining.All(x => x.State == CustomerState.Departed));
    }

    /// <summary>프레임 지연에도 만료 상태를 보존한다.</summary>
    [Test]
    public void HitchPrioritizesExpiry()
    {
        var first = queue.Waiting[0]; queue.Advance(9, false);
        Assert.That(first.Visit.State, Is.EqualTo(CustomerState.Abandoned)); Assert.That(queue.GetSpeech(first), Is.EqualTo(config.QueueLeaveTextIdx));
    }
}
