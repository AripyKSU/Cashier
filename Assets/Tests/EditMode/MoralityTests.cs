using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>도덕성 CSV 구간과 무상태 계산기의 경계를 검증한다.</summary>
public sealed class MoralityTests
{
    /// <summary>실제 20개 행의 모든 경계와 성인·아이/노인 점수를 검증한다.</summary>
    [Test]
    public void ActualCsvCalculatesAllBandsAndSkipsUnregisteredWealthy()
    {
        MoralityDataTable table = loadActual();
        MoralityCalculator calculator = calculatorFor(table);
        var adult = CustomerAttributes.Male | CustomerAttributes.Adult | CustomerAttributes.Normal;
        var child = CustomerAttributes.Female | CustomerAttributes.Child | CustomerAttributes.Normal;
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 800, 1000)?.Delta, Is.EqualTo(1m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 999, 1000)?.Delta, Is.EqualTo(0.5m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, child, true, 801, 1000)?.Delta, Is.EqualTo(1m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1000, 1000)?.DataIdx, Is.EqualTo(14003));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1001, 1000)?.Delta, Is.EqualTo(-0.25m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1050, 1000)?.Delta, Is.EqualTo(-0.25m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1051, 1000)?.Delta, Is.EqualTo(-0.5m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, true, 1101, 1000)?.Delta, Is.EqualTo(-0.75m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, child, true, 1200, 1000)?.Delta, Is.EqualTo(-1.5m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, child, true, 1300, 1000)?.Delta, Is.EqualTo(-2m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Normal, adult, false, 1301, 1000)?.Delta, Is.EqualTo(-2m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, child, true, 800, 1000)?.Delta, Is.EqualTo(9m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, adult, true, 1499, 1000)?.Delta, Is.EqualTo(-10m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Hasty, child, false, 1501, 1000)?.Delta, Is.EqualTo(-15m));
        Assert.That(calculator.Calculate(CustomerDispositionType.PriceSensitive, adult, true, 1000, 1000)?.Delta, Is.Zero);
        Assert.That(calculator.Calculate(CustomerDispositionType.PriceSensitive, child, false, 999, 1000)?.Delta, Is.EqualTo(-4m));
        Assert.That(calculator.Calculate(CustomerDispositionType.PriceSensitive, adult, false, 1001, 1000)?.Delta, Is.EqualTo(-2m));
        Assert.That(calculator.Calculate(CustomerDispositionType.Wealthy, adult, true, 1800, 1000), Is.Null);
    }

    /// <summary>유한 거절 상한과 구간 중복을 로드 후 공개 전에 거부한다.</summary>
    [Test]
    public void ValidationRejectsTailGapAndOverlap()
    {
        string csv = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Datas/MoralityData.csv"));
        MoralityDataTable tailGap = new MoralityDataTable();
        tailGap.LoadData(csv.Replace("14008,1,0,1300,0,0,0", "14008,1,0,1300,2000,0,1"));
        Assert.Throws<InvalidDataException>(() => tailGap.Validate(dispositions()));
        MoralityDataTable overlap = new MoralityDataTable();
        overlap.LoadData(csv.Replace("14005,1,1,1050,1100,0,1", "14005,1,1,1040,1100,0,1"));
        Assert.Throws<InvalidDataException>(() => overlap.Validate(dispositions()));
    }

    private static MoralityDataTable loadActual()
    {
        var table = new MoralityDataTable();
        table.LoadData(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Datas/MoralityData.csv")));
        table.Validate(dispositions());
        typeof(MoralityDataTable).GetMethod("Commit", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(table, null);
        return table;
    }

    private static MoralityCalculator calculatorFor(MoralityDataTable table) =>
        new MoralityCalculator(new List<MoralityData>(table.Rows.Values).AsReadOnly());

    private static IReadOnlyDictionary<uint, CustomerDispositionData> dispositions() =>
        new Dictionary<uint, CustomerDispositionData>
        {
            [6001] = new CustomerDispositionData { Idx = 6001, DispositionType = CustomerDispositionType.Normal, PriceTolerance = 1300 },
            [6002] = new CustomerDispositionData { Idx = 6002, DispositionType = CustomerDispositionType.Hasty, PriceTolerance = 1500 },
            [6003] = new CustomerDispositionData { Idx = 6003, DispositionType = CustomerDispositionType.PriceSensitive, PriceTolerance = 1000 },
            [6004] = new CustomerDispositionData { Idx = 6004, DispositionType = CustomerDispositionType.Wealthy, PriceTolerance = 1800 },
        };
}
