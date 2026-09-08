using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>자체 진입점 또는 외부 Test Runner 실행 중 재진입 안전성을 검사한다.</summary>
public sealed class CashierTestRunTests
{
    /// <summary>중복 Start가 pending·옵션·출력 경로를 변경하지 않는지 확인한다.</summary>
    [Test]
    public void RejectStartWhileRunnerActive()
    {
        string pending = SessionState.GetString("Cashier.Tests.Pending", "");
        bool background = Application.runInBackground;
        bool savedBackground = SessionState.GetBool("Cashier.Tests.Background", false);
        bool finished = SessionState.GetBool("Cashier.Tests.Finished", false);
        string id = "rejected-" + Guid.NewGuid().ToString("N");
        var exception = Assert.Throws<InvalidOperationException>(() => CashierTestRun.Start("EditMode", id));
        Assert.That(exception.Message, Does.Contain("job is active"));
        Assert.That(SessionState.GetString("Cashier.Tests.Pending", ""), Is.EqualTo(pending));
        Assert.That(SessionState.GetBool("Cashier.Tests.Background", false), Is.EqualTo(savedBackground));
        Assert.That(SessionState.GetBool("Cashier.Tests.Finished", false), Is.EqualTo(finished));
        Assert.That(Application.runInBackground, Is.EqualTo(background));
        Assert.That(Directory.Exists(Path.Combine("Temp", "TestResults", id)), Is.False);
        TestContext.WriteLine(string.IsNullOrEmpty(pending) ? "External GUI/API run protected" : "Owned pipeline run protected");
    }
}
