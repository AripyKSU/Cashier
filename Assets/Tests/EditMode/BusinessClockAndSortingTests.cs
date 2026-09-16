using NUnit.Framework;
using UnityEditor;
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

        itemView.Initialize(1001, 0, null, 144f, "TestProduct", workRect);

        var image = itemGo.GetComponent<UnityEngine.UI.Image>();
        Assert.That(image.raycastTarget, Is.True, "Image raycastTarget must be true for drag and drop");
        Assert.That(itemView.IsDragging, Is.False);
        Assert.That(itemView.State, Is.EqualTo(SaleSortingItemView.SortingState.Working));
        Assert.That(((RectTransform)itemView.transform).sizeDelta, Is.EqualTo(new Vector2(144f, 144f)));

        // ForSale 상태 전환 시 시각 피드백 검증
        itemView.State = SaleSortingItemView.SortingState.ForSale;
        itemView.UpdateVisualState();
        Assert.That(image.color.g, Is.GreaterThan(0.9f));

        Object.DestroyImmediate(root);
    }

    [TestCase(false, 0f, 0f, SaleSortingHandCursor.HandCursorState.Released)]
    [TestCase(true, 0f, 0f, SaleSortingHandCursor.HandCursorState.HoldingStill)]
    [TestCase(true, -80f, 0f, SaleSortingHandCursor.HandCursorState.HoldingLeft)]
    [TestCase(true, 80f, 0f, SaleSortingHandCursor.HandCursorState.HoldingRight)]
    [TestCase(true, 80f, 100f, SaleSortingHandCursor.HandCursorState.HoldingStill)]
    public void SaleSortingHandCursor_ResolvesReleasedAndHoldingDirections(
        bool isHolding,
        float velocityX,
        float velocityY,
        SaleSortingHandCursor.HandCursorState expected)
    {
        SaleSortingHandCursor.HandCursorState actual = SaleSortingHandCursor.ResolveState(
            isHolding,
            new Vector2(velocityX, velocityY),
            30f);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void SaleSortingHandCursor_GameUiPrefab_IsTopmostAndFullyBound()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/GameUI.prefab");
        Assert.That(prefab, Is.Not.Null);

        Transform cursorRoot = prefab.transform.Find("HandCursorCanvas");
        Assert.That(cursorRoot, Is.Not.Null);
        Assert.That(cursorRoot.GetSiblingIndex(), Is.EqualTo(prefab.transform.childCount - 1));

        Canvas canvas = cursorRoot.GetComponent<Canvas>();
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.overrideSorting, Is.True);
        Assert.That(canvas.sortingOrder, Is.EqualTo(32760));

        SaleSortingHandCursor cursor = cursorRoot.GetComponent<SaleSortingHandCursor>();
        Assert.That(cursor, Is.Not.Null);
        SerializedObject cursorObject = new SerializedObject(cursor);
        Assert.That(cursorObject.FindProperty("releasedSprite").objectReferenceValue.name, Is.EqualTo("Hand2"));
        Assert.That(cursorObject.FindProperty("holdingStillSprite").objectReferenceValue.name, Is.EqualTo("Hand1"));
        Assert.That(cursorObject.FindProperty("holdingLeftSprite").objectReferenceValue.name, Is.EqualTo("Hand3"));
        Assert.That(cursorObject.FindProperty("holdingRightSprite").objectReferenceValue.name, Is.EqualTo("Hand4"));

        UnityEngine.UI.Image image = cursorRoot.GetComponentInChildren<UnityEngine.UI.Image>(true);
        Assert.That(image, Is.Not.Null);
        Assert.That(image.raycastTarget, Is.False);

        SaleSortingPanel panel = prefab.GetComponentInChildren<SaleSortingPanel>(true);
        SerializedObject panelObject = new SerializedObject(panel);
        Assert.That(panelObject.FindProperty("itemSizePixels").floatValue, Is.EqualTo(144f));
        var itemPrefab = (SaleSortingItemView)panelObject.FindProperty("itemPrefab").objectReferenceValue;
        Assert.That(((RectTransform)itemPrefab.transform).sizeDelta,
            Is.EqualTo(new Vector2(144f, 144f)));
        Assert.That(panelObject.FindProperty("handCursor").objectReferenceValue, Is.SameAs(cursor));
        Assert.That(panelObject.FindProperty("calculatorToggleButton"), Is.Null);
        Assert.That(System.Array.Exists(prefab.GetComponentsInChildren<Transform>(true), child => child.name == "CalculatorToggle"), Is.False);
    }

    [Test]
    public void VacuumAsset_UsesAstraImportContract()
    {
        const string path = "Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo("8c5509423fa570745bc62cafae857f42"));
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(40));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.alphaIsTransparency, Is.True);
    }

    [Test]
    public void Vacuum_GameUiPrefab_HasAstraVisualAndNozzleBindings()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/GameUI.prefab");
        Assert.That(prefab, Is.Not.Null);

        VacuumController controller = prefab.GetComponentInChildren<VacuumController>(true);
        Assert.That(controller, Is.Not.Null);
        SerializedObject controllerObject = new SerializedObject(controller);
        RectTransform vacuumRect = controllerObject.FindProperty("vacuumRect").objectReferenceValue as RectTransform;
        UnityEngine.UI.Image vacuumImage = controllerObject.FindProperty("vacuumImage").objectReferenceValue as UnityEngine.UI.Image;
        RectTransform gripArea = controllerObject.FindProperty("gripArea").objectReferenceValue as RectTransform;
        RectTransform nozzle = controllerObject.FindProperty("nozzle").objectReferenceValue as RectTransform;
        RectTransform suctionVfxRoot = controllerObject.FindProperty("suctionVfxRoot").objectReferenceValue as RectTransform;
        Material windMaterial = controllerObject.FindProperty("windMaterial").objectReferenceValue as Material;

        Assert.That(vacuumRect, Is.SameAs(controller.transform));
        Assert.That(vacuumRect.localScale.x, Is.EqualTo(1.9f).Within(0.001f));
        Assert.That(vacuumRect.localScale.y, Is.EqualTo(1.9f).Within(0.001f));
        Assert.That(vacuumImage, Is.Not.Null);
        Assert.That(vacuumImage.sprite, Is.Not.Null);
        Assert.That(vacuumImage.sprite.texture.name, Is.EqualTo("Vacuum"));
        Assert.That(vacuumImage.preserveAspect, Is.True);
        Assert.That(vacuumImage.raycastTarget, Is.False);
        Assert.That(gripArea, Is.SameAs(controller.transform.Find("GripArea")));
        Assert.That(nozzle, Is.SameAs(controller.transform.Find("Nozzle")));
        Assert.That(suctionVfxRoot, Is.SameAs(controller.transform.Find("SuctionVfxRoot")));
        Assert.That(windMaterial, Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat")));
        Assert.That(controllerObject.FindProperty("suctionAccelerationPixels").floatValue,
            Is.EqualTo(2800f).Within(0.001f));

        RectTransform handRect = prefab.transform.Find("HandCursorCanvas/HandCursorImage") as RectTransform;
        Assert.That(handRect, Is.Not.Null);
        Assert.That(handRect.sizeDelta, Is.EqualTo(new Vector2(160f, 160f)));
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
    public void LandingDustEffect_UsesFixedContactPointRegardlessOfBox()
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
        Assert.That(contact, Is.EqualTo(new Vector2(640f, -578f)));

        boxRect.anchoredPosition += new Vector2(100f, 180f);
        boxRect.sizeDelta *= 2f;
        Assert.That(dustEffect.CalculateContactPoint(boxRect), Is.EqualTo(contact));

        dustEffect.BaseContactPoint = new Vector2(600f, -500f);
        dustEffect.DustOffset = new Vector2(5f, -10f);
        Assert.That(dustEffect.CalculateContactPoint(boxRect), Is.EqualTo(new Vector2(605f, -510f)));
        Assert.That(dustEffect.CalculateContactPoint(null), Is.EqualTo(new Vector2(605f, -510f)));

        Object.DestroyImmediate(boxGo);
        Object.DestroyImmediate(dustGo);
        Object.DestroyImmediate(root);
    }
}
