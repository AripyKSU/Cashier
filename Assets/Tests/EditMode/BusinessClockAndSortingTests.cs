using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 영업 시계(09:00~21:00) 및 큰 밀대(DividerBar) 기능 단위 테스트입니다.
/// </summary>
public sealed class BusinessClockAndSortingTests
{
    /// <summary>모델 시각 표시는 자체 진행과 마감 이벤트 없이 공통 영업 범위에 제한된다.</summary>
    [TestCase(-1, 540)]
    [TestCase(900, 900)]
    [TestCase(2000, 1260)]
    public void BusinessClock_DisplayTime_StopsPreviewWithoutClosingEvent(int requested, int expected)
    {
        var go = new GameObject("ModelClock");
        try
        {
            var clock = go.AddComponent<BusinessClockController>();
            int closed = 0;
            clock.OnBusinessClosed += () => closed++;
            clock.StartClock();
            clock.DisplayTime(requested);
            Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(expected));
            Assert.That(clock.IsRunning, Is.False);
            Assert.That(closed, Is.Zero);
            Assert.That(BusinessHours.DurationMinutes, Is.EqualTo(720));
        }
        finally { Object.DestroyImmediate(go); }
    }

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
    public void BusinessClock_FiresEvent_When2100Reached()
    {
        var go = new GameObject("TestClock");
        var clock = go.AddComponent<BusinessClockController>();

        bool eventFired = false;
        clock.OnBusinessClosed += () => eventFired = true;

        // 21:00 도달
        clock.SetTime(21, 0);
        Assert.That(clock.IsClosed, Is.True);
        Assert.That(clock.CurrentHour, Is.EqualTo(21));
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

        // EditMode에서는 Awake가 실행되지 않으므로 실제 prefab의 필수 참조를 fixture가 제공한다.
        typeof(DividerBarController).GetField("barRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(divider, (RectTransform)barGo.transform);

        divider.Initialize(workRect);
        Assert.That(divider.Position.x, Is.LessThan(0f));
        Assert.That(divider.IsHolding, Is.False);

        Object.DestroyImmediate(root);
    }

    [Test]
    public void SaleSortingItemView_InitializesRaycastTarget_AndSupportsDrag()
    {
        var root = new GameObject("WorkbenchRoot", typeof(RectTransform));
        var workRect = (RectTransform)root.transform;

        var itemGo = new GameObject("TestItem", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        itemGo.transform.SetParent(workRect, false);
        var itemView = itemGo.AddComponent<SaleSortingItemView>();

        itemView.Initialize(1001, 0, null, 72f, "TestProduct", workRect);

        var image = itemGo.GetComponent<UnityEngine.UI.Image>();
        Assert.That(image.raycastTarget, Is.True, "Image raycastTarget must be true for drag and drop");
        Assert.That(itemView.IsDragging, Is.False);
        Assert.That(itemView.State, Is.EqualTo(SaleSortingItemView.SortingState.Working));

        // ForSale 상태 전환 시 시각 피드백 검증
        itemView.State = SaleSortingItemView.SortingState.ForSale;
        itemView.UpdateVisualState();
        Assert.That(image.color.g, Is.GreaterThan(0.9f));

        Object.DestroyImmediate(root);
    }

    [Test]
    public void TimeOfDayUIController_EditorPreview_DoesNotChangeBusinessClock()
    {
        var clockGo = new GameObject("ClockGo");
        var clock = clockGo.AddComponent<BusinessClockController>();
        clock.SetTime(15, 30); // 15.5시 (석양 시작 구간)

        var dayNightGo = new GameObject("TimeOfDayGo");
        var timeOfDay = dayNightGo.AddComponent<TimeOfDayUIController>();
        timeOfDay.BusinessClock = clock;

        // Reflection으로 sunset 이미지 바인딩
        var sunsetImgGo = new GameObject("Sunset", typeof(UnityEngine.UI.Image));
        var sunsetImg = sunsetImgGo.GetComponent<UnityEngine.UI.Image>();
        var field = typeof(TimeOfDayUIController).GetField("sunset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(timeOfDay, sunsetImg);

        // EditMode는 명시적으로 미리보기를 켠 경우에만 표시를 갱신한다.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(TimeOfDayUIController).GetField("previewInEditor", flags).SetValue(timeOfDay, true);
        typeof(TimeOfDayUIController).GetField("previewHour", flags).SetValue(timeOfDay, 16.5f);
        timeOfDay.RefreshTime();
        Assert.That(timeOfDay.CurrentAppliedHour, Is.EqualTo(16.5f));
        Assert.That(clock.CurrentBusinessMinutes, Is.EqualTo(930f), "미리보기는 모델 시계를 바꾸지 않는다.");

        // 미리보기 16.5시는 sunsetStart(15)와 eveningStart(18) 사이이므로 알파가 0보다 크다.
        Assert.That(sunsetImg.canvasRenderer.GetColor().a, Is.GreaterThan(0f));

        Object.DestroyImmediate(sunsetImgGo);
        Object.DestroyImmediate(dayNightGo);
        Object.DestroyImmediate(clockGo);
    }

    [Test]
    public void CustomerPresenter_ControlsSpeechBubble_BasedOnDialogue()
    {
        var presenterGo = new GameObject("CustomerPresenter");
        var presenter = presenterGo.AddComponent<CustomerPresenter>();

        var bubbleGo = new GameObject("SpeechBubble");
        bubbleGo.SetActive(false);
        var dialogueField = typeof(CustomerPresenter).GetField("dialogueText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var textGo = new GameObject("Dialogue", typeof(RectTransform), dialogueField.FieldType);
        textGo.transform.SetParent(bubbleGo.transform, false);
        dialogueField.SetValue(presenter, textGo.GetComponent(dialogueField.FieldType));

        // Reflection으로 speechBubbleRoot 필드 바인딩
        var field = typeof(CustomerPresenter).GetField("speechBubbleRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(presenter, bubbleGo);

        // 1. 대사가 있는 손님 데이터 전달 -> 말풍선 활성화
        var viewDataWithDialogue = new CustomerViewData(
            true, Color.white, null, "안녕하세요!", System.Array.Empty<CustomerBasketItemViewData>());
        presenter.UpdateView(viewDataWithDialogue);
        Assert.That(bubbleGo.activeSelf, Is.True);

        // 2. 손님 없음 -> 말풍선 비활성화
        presenter.UpdateView(CustomerViewData.Empty);
        Assert.That(bubbleGo.activeSelf, Is.False);

        Object.DestroyImmediate(bubbleGo);
        Object.DestroyImmediate(presenterGo);
    }

    [Test]
    public void TimeOfDayUIController_BlendsDayAndNight_AccordingToBusinessClock()
    {
        var clockGo = new GameObject("ClockGo");
        var clock = clockGo.AddComponent<BusinessClockController>();
        clock.SetTime(9, 0);

        var dayNightGo = new GameObject("TimeOfDayGo");
        var timeOfDay = dayNightGo.AddComponent<TimeOfDayUIController>();
        timeOfDay.BusinessClock = clock;

        // 09:00 -> 아침 가중치 반영
        timeOfDay.ApplyHour(9f);
        Assert.That(timeOfDay.CurrentAppliedHour, Is.EqualTo(9f));

        // 21:00 -> 야간/영업 마감 가중치 반영
        clock.SetTime(21, 0);
        timeOfDay.ApplyHour(21f);
        Assert.That(timeOfDay.CurrentAppliedHour, Is.EqualTo(21f));

        Object.DestroyImmediate(dayNightGo);
        Object.DestroyImmediate(clockGo);
    }

    [Test]
    public void LandingDustEffect_CanPlayAndStop()
    {
        var dustGo = new GameObject("LandingDustGo");
        var dustEffect = dustGo.AddComponent<LandingDustEffect>();

        dustEffect.Play(Vector2.zero);
        dustEffect.Stop();
        Assert.Pass();

        Object.DestroyImmediate(dustGo);
    }

    [Test]
    public void LandingDustEffect_CalculatesContactPointAtBottomOfBox()
    {
        var root = new GameObject("Root", typeof(RectTransform));
        var boxGo = new GameObject("FrontContainer", typeof(RectTransform));
        boxGo.transform.SetParent(root.transform, false);
        var boxRect = (RectTransform)boxGo.transform;
        boxRect.anchorMin = new Vector2(0f, 1f);
        boxRect.anchorMax = new Vector2(0f, 1f);
        boxRect.pivot = new Vector2(0f, 1f);
        boxRect.anchoredPosition = new Vector2(460f, -350f);
        boxRect.sizeDelta = new Vector2(360f, 240f);

        var dustGo = new GameObject("LandingDustGo");
        var dustEffect = dustGo.AddComponent<LandingDustEffect>();

        Vector2 contact = dustEffect.CalculateContactPoint(boxRect);
        Assert.That(contact.x, Is.EqualTo(640f).Within(0.1f), "Center X should be 640");
        Assert.That(contact.y, Is.EqualTo(-578.13f).Within(0.5f), "Bottom contact Y should be near -578");

        Object.DestroyImmediate(boxGo);
        Object.DestroyImmediate(dustGo);
        Object.DestroyImmediate(root);
    }
}
