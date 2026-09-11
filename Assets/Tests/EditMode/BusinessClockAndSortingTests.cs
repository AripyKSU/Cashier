using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 영업 시계(09:00~21:00) 및 큰 밀대(DividerBar) 기능 단위 테스트입니다.
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
    public void TimeOfDayUIController_AppliesBusinessClockTime_Correctly()
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

        timeOfDay.RefreshTime();

        // 15.5시는 sunsetStart(15)와 eveningStart(18) 사이이므로 sunset 알파가 0보다 커야 함
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

    [Test]
    public void TimeOfDayUIController_PhaseCalculations_MatchOperatingHours()
    {
        var go = new GameObject("TimeOfDayTestGo");
        var controller = go.AddComponent<TimeOfDayUIController>();

        // 9 AM (Dawn / Morning)
        controller.ApplyHour(9f);
        Assert.That(controller.CurrentPhaseName, Does.Contain("아침"));

        // 13 PM (Day / Clear daylight)
        controller.ApplyHour(13f);
        Assert.That(controller.CurrentPhaseName, Does.Contain("주간"));

        // 16.5 PM (Sunset)
        controller.ApplyHour(16.5f);
        Assert.That(controller.CurrentPhaseName, Does.Contain("석양"));

        // 20 PM (Night)
        controller.ApplyHour(20f);
        Assert.That(controller.CurrentPhaseName, Does.Contain("야간"));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TimeOfDayPixelStage_AcceptsTimeWeights_AndConfiguresParameters()
    {
        var go = new GameObject("PixelStageTestGo", typeof(RectTransform));
        var stage = go.AddComponent<TimeOfDayPixelStage>();

        stage.SetTimeWeights(0.8f, 0.2f, 0f, 0.4f, 10.5f);
        Assert.Pass();

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SaleSortingPanel_Awake_WithButtonOnTransform_DoesNotThrowInvalidCast()
    {
        var panelGo = new GameObject("PanelGo", typeof(RectTransform));
        var panel = panelGo.AddComponent<SaleSortingPanel>();

        // 1. Button with standard Transform (not RectTransform)
        var btnGo = new GameObject("FrontContainerBtn");
        var btn = btnGo.AddComponent<UnityEngine.UI.Button>();

        var serialized = new UnityEditor.SerializedObject(panel);
        serialized.FindProperty("frontContainerButton").objectReferenceValue = btn;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Invoking Awake via reflection
        var awakeMethod = typeof(SaleSortingPanel).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.DoesNotThrow(() => awakeMethod.Invoke(panel, null));

        Object.DestroyImmediate(btnGo);
        Object.DestroyImmediate(panelGo);
    }

    [Test]
    public void GameInputRouter_SetState_AppliesPauseAndResumeFlags()
    {
        var go = new GameObject("TestInputRouter");
        var router = go.AddComponent<GameInputRouter>();

        router.SetState(canConfirm: false, canContinue: false, canPause: true, canResume: false);

        bool pauseFired = false;
        bool resumeFired = false;
        router.OnPauseRequested += () => pauseFired = true;
        router.OnResumeRequested += () => resumeFired = true;

        Assert.That(router, Is.Not.Null);
        Assert.That(pauseFired, Is.False);
        Assert.That(resumeFired, Is.False);

        Object.DestroyImmediate(go);
    }
}
