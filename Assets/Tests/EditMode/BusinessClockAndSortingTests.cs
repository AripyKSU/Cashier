using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 영업 시계(09:00~20:00) 및 큰 밀대(DividerBar) 기능 단위 테스트입니다.
/// </summary>
public sealed class BusinessClockAndSortingTests
{
    [Test]
    public void BusinessClock_StartsAt0900_AndTracksTimeCorrectly()
    {
        var go = new GameObject("TestClock");
        var clock = go.AddComponent<BusinessClockController>();

        // 기본 영업 시작: 9시 0분
        Assert.That(clock.CurrentHour, Is.EqualTo(9));
        Assert.That(clock.CurrentMinute, Is.EqualTo(0));
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(540f));
        Assert.That(clock.IsClosed, Is.False);

        // 시간 임의 설정 테스트 (예: 14시 35분)
        clock.SetTime(14, 35);
        Assert.That(clock.CurrentHour, Is.EqualTo(14));
        Assert.That(clock.CurrentMinute, Is.EqualTo(35));
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(14 * 60f + 35f));
        Assert.That(clock.IsClosed, Is.False);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BusinessClock_FiresEvent_When2000Reached()
    {
        var go = new GameObject("TestClock");
        var clock = go.AddComponent<BusinessClockController>();

        bool eventFired = false;
        clock.OnBusinessClosed += () => eventFired = true;

        // 20:00 도달
        clock.SetTime(20, 0);
        Assert.That(clock.IsClosed, Is.True);
        Assert.That(clock.CurrentHour, Is.EqualTo(20));
        Assert.That(clock.CurrentMinute, Is.EqualTo(0));
        Assert.That(eventFired, Is.True);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BusinessClock_ResetsToStart_Correctly()
    {
        var go = new GameObject("TestClock");
        var clock = go.AddComponent<BusinessClockController>();

        clock.SetTime(18, 0);
        Assert.That(clock.CurrentHour, Is.EqualTo(18));

        clock.ResetToStart();
        Assert.That(clock.CurrentHour, Is.EqualTo(9));
        Assert.That(clock.CurrentMinute, Is.EqualTo(0));
        Assert.That(clock.IsClosed, Is.False);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void DividerBar_InitializesAndResetsLeftEnd_Correctly()
    {
        var root = new GameObject("WorkbenchRoot", typeof(RectTransform));
        var workRect = (RectTransform)root.transform;
        workRect.sizeDelta = new Vector2(1000f, 600f);

        var barGo = new GameObject("DividerBar", typeof(RectTransform));
        barGo.transform.SetParent(workRect, false);
        var divider = barGo.AddComponent<DividerBarController>();

        divider.Initialize(workRect);
        Assert.That(divider.Position.x, Is.LessThan(0f));
        Assert.That(divider.IsHolding, Is.False);

        Object.DestroyImmediate(root);
    }
}
