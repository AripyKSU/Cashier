using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영업 중 좌상단 명패(D-N·시각·보유금·변화량)를 GameUI.prefab에 만들고,
/// 손님 성향 아이콘을 CustomerWorld.prefab에 연결하는 에디터 설치 도구입니다.
/// 여러 번 실행해도 기존 명패를 지우고 같은 구조로 다시 만듭니다.
/// </summary>
public static class ShopStatusHudSetup
{
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string CustomerWorldPrefabPath = "Assets/Prefabs/World/CustomerWorld.prefab";
    private const string HudRootPath = "ProgressCanvas/Root";
    private const string HudName = "ShopStatusHud";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private const string PlatePath = "Assets/Textures/UI/Dystopia/Hud/HudPlate.png";
    private const string ClockIconPath = "Assets/Textures/UI/Dystopia/Hud/HudClock.png";
    private const string CoinIconPath = "Assets/Textures/UI/Dystopia/Hud/HudCoin.png";
    private const string TraitFolder = "Assets/Textures/UI/Dystopia/CustomerTrait/";
    private const string SettlementPanelPrefabPath = "Assets/Prefabs/GameUI/SettlementPanel.prefab";
    private const string PenHandPath = "Assets/Textures/UI/Dystopia/Settlement/LedgerPenHand.png";
    private const string PenHandName = "PenHand";
    private const string CoachName = "TutorialCoach";
    private const string DialogueFramePath = "Assets/Textures/art/UI/InspectorDialogueFrame.png";
    private const string InspectorPanelPrefabPath = "Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab";
    // 가계부 334px 원본이 1280 기준 화면을 채우므로 원본 1px ≈ UI 3.83px
    private const float LedgerPixelScale = 1280f / 334f;

    // CustomerDispositionType 순서: Normal, Hasty, PriceSensitive, Wealthy, Poor
    private static readonly string[] TraitFiles =
    {
        "TraitNormal.png", "TraitHasty.png", "TraitPriceSensitive.png", "TraitRich.png", "TraitPoor.png"
    };

    /// <summary>명패와 성향 아이콘 연결을 모두 설치합니다.</summary>
    [MenuItem("Cashier/Setup/Shop Status HUD And Traits")]
    public static void Install()
    {
        installHud();
        installTraits();
        installPenHand();
        installTutorialCoach();
        hideInspectorBackground();
        AssetDatabase.SaveAssets();
        Debug.Log("[ShopStatusHudSetup] 명패와 성향 아이콘 설치 완료");
    }

    /// <summary>GameUI.prefab에 명패를 만들고 GameUIController에 연결합니다.</summary>
    private static void installHud()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var plate = AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath);
        var clockIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ClockIconPath);
        var coinIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath);
        if (font == null || plate == null || clockIcon == null || coinIcon == null)
            throw new System.InvalidOperationException("명패 리소스(폰트·철판·아이콘)를 찾을 수 없습니다.");

        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            Transform parent = root.transform.Find(HudRootPath)
                ?? throw new System.InvalidOperationException($"{HudRootPath}를 찾을 수 없습니다.");
            Transform old = parent.Find(HudName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var hud = new GameObject(HudName, typeof(RectTransform), typeof(CanvasGroup), typeof(ShopStatusHudPresenter));
            var hudRect = (RectTransform)hud.transform;
            hudRect.SetParent(parent, false);
            // 오류 패널보다는 아래, 영업 화면보다는 위에 둔다.
            Transform errorPanel = parent.Find("ErrorPanel");
            hudRect.SetSiblingIndex(errorPanel != null ? errorPanel.GetSiblingIndex() : parent.childCount - 1);
            setRect(hudRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(236, 66));

            var plateImage = createImage("Plate", hudRect, plate);
            plateImage.type = Image.Type.Sliced;
            plateImage.pixelsPerUnitMultiplier = 0.5f;
            stretch((RectTransform)plateImage.transform);

            var clockImage = createImage("ClockIcon", hudRect, clockIcon);
            setRect((RectTransform)clockImage.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(20, 20));
            var clockText = createText("Clock", hudRect, font, 20, TextAlignmentOptions.MidlineLeft);
            setRect(clockText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -8), new Vector2(80, 24));
            clockText.text = "09:00";

            var dayText = createText("Day", hudRect, font, 20, TextAlignmentOptions.MidlineRight);
            setRect(dayText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(90, 24));
            dayText.text = "D-19";

            var coinImage = createImage("CoinIcon", hudRect, coinIcon);
            setRect((RectTransform)coinImage.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -36), new Vector2(20, 20));
            var balanceText = createText("Balance", hudRect, font, 22, TextAlignmentOptions.MidlineLeft);
            setRect(balanceText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -34), new Vector2(180, 26));
            balanceText.color = new Color(0.95f, 0.84f, 0.55f);
            balanceText.text = "0원";

            var deltaText = createText("Delta", hudRect, font, 22, TextAlignmentOptions.MidlineLeft);
            setRect(deltaText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -96), new Vector2(200, 26));
            deltaText.outlineWidth = 0.2f;
            deltaText.outlineColor = new Color32(20, 14, 10, 255);
            deltaText.text = string.Empty;

            var guide = createTraitGuide(hudRect, font, plate);

            var presenter = hud.GetComponent<ShopStatusHudPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("traitGuide").objectReferenceValue = guide;
            serializedPresenter.FindProperty("rootGroup").objectReferenceValue = hud.GetComponent<CanvasGroup>();
            serializedPresenter.FindProperty("dayText").objectReferenceValue = dayText;
            serializedPresenter.FindProperty("clockText").objectReferenceValue = clockText;
            serializedPresenter.FindProperty("balanceText").objectReferenceValue = balanceText;
            serializedPresenter.FindProperty("deltaText").objectReferenceValue = deltaText;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            var group = hud.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var controller = root.GetComponentInChildren<GameUIController>(true)
                ?? throw new System.InvalidOperationException("GameUIController를 찾을 수 없습니다.");
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("shopStatusHud").objectReferenceValue = presenter;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 명패 오른쪽에 "손님 성향" 버튼과, 누르면 열리는 아이콘별 특징 설명표를 만듭니다. 수치는 쓰지 않습니다.
    /// </summary>
    /// <param name="hudRect">명패 루트입니다.</param>
    /// <param name="font">글꼴입니다.</param>
    /// <param name="plate">철판 9-slice 이미지입니다.</param>
    /// <returns>연결된 설명표 Presenter입니다.</returns>
    private static CustomerTraitGuidePresenter createTraitGuide(RectTransform hudRect, TMP_FontAsset font, Sprite plate)
    {
        var guideObject = new GameObject("TraitGuide", typeof(RectTransform), typeof(CustomerTraitGuidePresenter));
        var guideRect = (RectTransform)guideObject.transform;
        guideRect.SetParent(hudRect, false);
        setRect(guideRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(8, 0), new Vector2(110, 36));

        var buttonImage = createImage("Toggle", guideRect, plate);
        buttonImage.type = Image.Type.Sliced;
        buttonImage.pixelsPerUnitMultiplier = 0.5f;
        buttonImage.raycastTarget = true;
        stretch((RectTransform)buttonImage.transform);
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        var label = createText("Label", buttonImage.transform, font, 17, TextAlignmentOptions.Center);
        stretch(label.rectTransform);
        label.text = "손님 성향";

        var panelImage = createImage("Panel", guideRect, plate);
        panelImage.type = Image.Type.Sliced;
        panelImage.pixelsPerUnitMultiplier = 0.5f;
        var panelRect = (RectTransform)panelImage.transform;
        const float rowHeight = 50f;
        setRect(panelRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -42), new Vector2(430, rowHeight * TraitFiles.Length + 24));

        var names = new TextMeshProUGUI[TraitFiles.Length];
        var descriptions = new TextMeshProUGUI[TraitFiles.Length];
        for (int index = 0; index < TraitFiles.Length; index++)
        {
            float top = -12 - rowHeight * index;
            var icon = createImage("Icon" + index, panelRect,
                AssetDatabase.LoadAssetAtPath<Sprite>(TraitFolder + TraitFiles[index]));
            setRect((RectTransform)icon.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, top - 4), new Vector2(40, 40));
            names[index] = createText("Name" + index, panelRect, font, 18, TextAlignmentOptions.TopLeft);
            setRect(names[index].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, top), new Vector2(350, 22));
            names[index].color = new Color(0.95f, 0.84f, 0.55f);
            descriptions[index] = createText("Description" + index, panelRect, font, 15, TextAlignmentOptions.TopLeft);
            setRect(descriptions[index].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, top - 22), new Vector2(350, 22));
        }

        panelImage.gameObject.SetActive(false);
        var presenter = guideObject.GetComponent<CustomerTraitGuidePresenter>();
        var serialized = new SerializedObject(presenter);
        serialized.FindProperty("toggleButton").objectReferenceValue = button;
        serialized.FindProperty("toggleLabel").objectReferenceValue = label;
        serialized.FindProperty("panel").objectReferenceValue = panelImage.gameObject;
        SerializedProperty nameProperty = serialized.FindProperty("nameTexts");
        SerializedProperty descriptionProperty = serialized.FindProperty("descriptionTexts");
        nameProperty.arraySize = names.Length;
        descriptionProperty.arraySize = descriptions.Length;
        for (int index = 0; index < names.Length; index++)
        {
            nameProperty.GetArrayElementAtIndex(index).objectReferenceValue = names[index];
            descriptionProperty.GetArrayElementAtIndex(index).objectReferenceValue = descriptions[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return presenter;
    }

    /// <summary>CustomerWorld.prefab의 대기열 표시에 성향 아이콘 다섯 개를 연결합니다.</summary>
    private static void installTraits()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CustomerWorldPrefabPath);
        try
        {
            var view = root.GetComponentInChildren<CustomerWorldQueueView>(true)
                ?? throw new System.InvalidOperationException("CustomerWorldQueueView를 찾을 수 없습니다.");
            var serializedView = new SerializedObject(view);
            SerializedProperty sprites = serializedView.FindProperty("traitSprites");
            sprites.arraySize = TraitFiles.Length;
            for (int index = 0; index < TraitFiles.Length; index++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TraitFolder + TraitFiles[index])
                    ?? throw new System.InvalidOperationException($"성향 아이콘이 없습니다: {TraitFiles[index]}");
                sprites.GetArrayElementAtIndex(index).objectReferenceValue = sprite;
            }

            serializedView.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CustomerWorldPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 정산 가계부에 글씨를 따라 움직이는 펜 쥔 손을 추가합니다.
    /// 가계부 원본 픽셀 크기(화면 약 3.83배)에 맞추고 피벗을 펜 끝에 둡니다.
    /// </summary>
    private static void installPenHand()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PenHandPath)
            ?? throw new System.InvalidOperationException("펜 손 이미지가 없습니다.");
        GameObject root = PrefabUtility.LoadPrefabContents(SettlementPanelPrefabPath);
        try
        {
            var ledgerView = root.GetComponentInChildren<DailySettlementLedgerView>(true)
                ?? throw new System.InvalidOperationException("DailySettlementLedgerView를 찾을 수 없습니다.");
            Transform parent = ledgerView.transform;
            Transform old = parent.Find(PenHandName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var image = createImage(PenHandName, parent, sprite);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            // 64x64 원본에서 펜 끝 픽셀 (2,61)의 중심. 공책 크기에 맞는 실제 손 크기로 보이게 한다.
            rect.pivot = new Vector2(2.5f / 64f, 2.5f / 64f);
            rect.sizeDelta = new Vector2(64f, 64f) * LedgerPixelScale;
            rect.SetAsLastSibling();
            image.gameObject.SetActive(false);

            var serializedView = new SerializedObject(ledgerView);
            serializedView.FindProperty("penHand").objectReferenceValue = rect;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SettlementPanelPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 영업 화면 위 감독관 안내 말풍선(하단 대사 상자, 감독관 얼굴, 강조 테두리)을 GameUI.prefab에 만들고 연결합니다.
    /// </summary>
    private static void installTutorialCoach()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        // 대사틀은 Multiple 스프라이트라 하위 Sprite를 찾아 쓴다.
        Sprite frame = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(DialogueFramePath))
            if (asset is Sprite sprite) { frame = sprite; break; }

        if (font == null || frame == null)
            throw new System.InvalidOperationException("안내 말풍선 리소스(폰트·대사틀·감독관 얼굴)를 찾을 수 없습니다.");

        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            Transform parent = root.transform.Find(HudRootPath)
                ?? throw new System.InvalidOperationException($"{HudRootPath}를 찾을 수 없습니다.");
            Transform old = parent.Find(CoachName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var coach = new GameObject(CoachName, typeof(RectTransform), typeof(CanvasGroup), typeof(TutorialCoachPresenter));
            var coachRect = (RectTransform)coach.transform;
            coachRect.SetParent(parent, false);
            Transform errorPanel = parent.Find("ErrorPanel");
            coachRect.SetSiblingIndex(errorPanel != null ? errorPanel.GetSiblingIndex() : parent.childCount - 1);
            stretch(coachRect);

            // 안내 중 화면 입력을 막는 투명 막. 클릭은 Presenter가 직접 읽는다.
            var blocker = createImage("Blocker", coachRect, null);
            blocker.color = new Color(0f, 0f, 0f, 0.18f);
            blocker.raycastTarget = true;
            stretch((RectTransform)blocker.transform);

            var highlight = new GameObject("Highlight", typeof(RectTransform));
            var highlightRect = (RectTransform)highlight.transform;
            highlightRect.SetParent(coachRect, false);
            var edges = new Image[4];
            for (int index = 0; index < 4; index++)
            {
                edges[index] = createImage("Edge" + index, highlightRect, null);
                var edgeRect = (RectTransform)edges[index].transform;
                bool horizontal = index < 2;
                edgeRect.anchorMin = horizontal ? new Vector2(0, index == 0 ? 1 : 0) : new Vector2(index == 2 ? 0 : 1, 0);
                edgeRect.anchorMax = horizontal ? new Vector2(1, index == 0 ? 1 : 0) : new Vector2(index == 2 ? 0 : 1, 1);
                edgeRect.pivot = new Vector2(.5f, .5f);
                edgeRect.anchoredPosition = Vector2.zero;
                edgeRect.sizeDelta = horizontal ? new Vector2(0, 5) : new Vector2(5, 0);
            }

            var box = createImage("Box", coachRect, frame);
            box.type = Image.Type.Simple;
            var boxRect = (RectTransform)box.transform;
            setRect(boxRect, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(860, 150));


            var line = createText("Line", boxRect, font, 22, TextAlignmentOptions.MidlineLeft);
            line.textWrappingMode = TextWrappingModes.Normal;
            setRect(line.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero);
            // 감독관 그림 없이 대사와 글자 소리만으로 감독관임을 알린다.
            line.rectTransform.offsetMin = new Vector2(44, 18);
            line.rectTransform.offsetMax = new Vector2(-28, -18);

            var hint = createText("Hint", boxRect, font, 14, TextAlignmentOptions.BottomRight);
            setRect(hint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 8), new Vector2(200, 20));
            hint.color = new Color(1f, 1f, 1f, .6f);
            hint.text = "클릭해서 계속";

            var presenter = coach.GetComponent<TutorialCoachPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("rootGroup").objectReferenceValue = coach.GetComponent<CanvasGroup>();
            serializedPresenter.FindProperty("lineText").objectReferenceValue = line;
            serializedPresenter.FindProperty("highlight").objectReferenceValue = highlightRect;
            SerializedProperty edgeProperty = serializedPresenter.FindProperty("highlightEdges");
            edgeProperty.arraySize = edges.Length;
            for (int index = 0; index < edges.Length; index++)
                edgeProperty.GetArrayElementAtIndex(index).objectReferenceValue = edges[index];
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            var group = coach.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var controller = root.GetComponentInChildren<GameUIController>(true)
                ?? throw new System.InvalidOperationException("GameUIController를 찾을 수 없습니다.");
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("tutorialCoach").objectReferenceValue = presenter;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>감독관 패널의 별도 배경을 꺼서 실제 가게 정면 위에서 말하게 합니다.</summary>
    private static void hideInspectorBackground()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(InspectorPanelPrefabPath);
        try
        {
            Transform background = null;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == "Background") { background = transform; break; }
            if (background == null) throw new System.InvalidOperationException("감독관 패널 Background를 찾을 수 없습니다.");
            background.gameObject.SetActive(false);
            // 패널 바탕은 클릭을 받기 위해 남기되 투명하게 해 뒤의 가게 정면이 보이게 한다.
            var panelImage = root.GetComponent<Image>();
            if (panelImage != null)
            {
                Color color = panelImage.color;
                color.a = 0f;
                panelImage.color = color;
            }

            PrefabUtility.SaveAsPrefabAsset(root, InspectorPanelPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Image createImage(string name, Transform parent, Sprite sprite)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        var image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI createText(string name, Transform parent, TMP_FontAsset font, float size,
        TextAlignmentOptions alignment)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        var text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Color.white;
        return text;
    }

    private static void setRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
