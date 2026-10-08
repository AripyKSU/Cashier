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
    private const string SettlementBalanceName = "SettlementBalance";
    private const string PlankPath = "Assets/Textures/UI/Dystopia/Settlement/PlankBlank.png";
    // 다음날 버튼 글자 색(크림)과 테두리 색(짙은 갈색).
    private static readonly Color PlankTextColor = new Color32(240, 202, 155, 255);
    private static readonly Color PlankOutlineColor = new Color32(9, 2, 1, 255);
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private const string PlatePath = "Assets/Textures/UI/Dystopia/Hud/HudPlate.png";
    private const string ClockIconPath = "Assets/Textures/UI/Dystopia/Hud/HudClock.png";
    private const string CoinIconPath = "Assets/Textures/UI/Dystopia/Hud/HudCoin.png";
    private const string GearIconPath = "Assets/Textures/UI/Dystopia/Hud/HudGear.png";
    private const string TraitFolder = "Assets/Textures/UI/Dystopia/CustomerTrait/";
    private const string SettlementPanelPrefabPath = "Assets/Prefabs/GameUI/SettlementPanel.prefab";
    private const string CoachName = "TutorialCoach";
    private const string DialogueFramePath = "Assets/Textures/art/UI/InspectorDialogueFrame.png";
    private const string InspectorPanelPrefabPath = "Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab";

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
        installSettlementBalance();
        installTraits();
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
            // 명패 루트는 화면 전체를 덮는 빈 영역이다. 왼쪽 위에 철판 명패, 오른쪽 위에 손님 성향·설정 버튼을 둔다.
            stretch(hudRect);
            var plateGroup = new GameObject("StatusPlate", typeof(RectTransform));
            var plateRect = (RectTransform)plateGroup.transform;
            plateRect.SetParent(hudRect, false);
            setRect(plateRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(236, 66));

            var plateImage = createImage("Plate", plateRect, plate);
            plateImage.type = Image.Type.Sliced;
            plateImage.pixelsPerUnitMultiplier = 0.5f;
            stretch((RectTransform)plateImage.transform);

            var clockImage = createImage("ClockIcon", plateRect, clockIcon);
            setRect((RectTransform)clockImage.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(20, 20));
            var clockText = createText("Clock", plateRect, font, 20, TextAlignmentOptions.MidlineLeft);
            setRect(clockText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -8), new Vector2(80, 24));
            clockText.text = "09:00";

            var dayText = createText("Day", plateRect, font, 20, TextAlignmentOptions.MidlineRight);
            setRect(dayText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(90, 24));
            dayText.text = "D-20";

            var coinImage = createImage("CoinIcon", plateRect, coinIcon);
            setRect((RectTransform)coinImage.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -36), new Vector2(20, 20));
            var balanceText = createText("Balance", plateRect, font, 22, TextAlignmentOptions.MidlineLeft);
            setRect(balanceText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -34), new Vector2(180, 26));
            balanceText.color = new Color(0.95f, 0.84f, 0.55f);
            balanceText.text = "0원";

            var deltaText = createText("Delta", plateRect, font, 22, TextAlignmentOptions.MidlineLeft);
            setRect(deltaText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -96), new Vector2(200, 26));
            deltaText.outlineWidth = 0.2f;
            deltaText.outlineColor = new Color32(20, 14, 10, 255);
            deltaText.text = string.Empty;

            var guide = createTraitGuide(hudRect, font, plate);
            createOptions(hudRect, font, plate);

            var presenter = hud.GetComponent<ShopStatusHudPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("traitGuide").objectReferenceValue = guide;
            serializedPresenter.FindProperty("plateRect").objectReferenceValue = plateRect;
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
        // 화면 오른쪽 위, 설정(톱니바퀴) 버튼 바로 왼쪽.
        setRect(guideRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -18), new Vector2(110, 40));

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
        setRect(panelRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -46), new Vector2(430, rowHeight * TraitFiles.Length + 24));

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

    /// <summary>
    /// 정산 화면 아래 가운데 현재 보유금 철판을 만듭니다. 설비 창(FacilityShopPanel)보다 뒤 형제로 두어 그 위에 그려지게 합니다.
    /// </summary>
    private static void installSettlementBalance()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var coinIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath);
        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            Transform canvas = root.transform.Find("ProgressCanvas")
                ?? throw new System.InvalidOperationException("ProgressCanvas를 찾을 수 없습니다.");
            Transform old = canvas.Find(SettlementBalanceName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform shop = canvas.Find("FacilityShopPanel")
                ?? throw new System.InvalidOperationException("FacilityShopPanel을 찾을 수 없습니다.");

            var balanceObject = new GameObject(SettlementBalanceName, typeof(RectTransform), typeof(CanvasGroup), typeof(ShopStatusHudPresenter));
            var balanceRect = (RectTransform)balanceObject.transform;
            balanceRect.SetParent(canvas, false);
            balanceRect.SetSiblingIndex(shop.GetSiblingIndex() + 1);
            // 다음날 버튼과 같은 나무 판자. 판자 높이에 맞춰 양 끝 못 박힌 부분 크기를 맞춘다.
            var plank = AssetDatabase.LoadAssetAtPath<Sprite>(PlankPath)
                ?? throw new System.InvalidOperationException("빈 판자 이미지가 없습니다.");
            const float plankHeight = 62f;
            setRect(balanceRect, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 10), new Vector2(330, plankHeight));

            var plateImage = createImage("Plate", balanceRect, plank);
            plateImage.type = Image.Type.Sliced;
            plateImage.pixelsPerUnitMultiplier = plank.rect.height / plankHeight * (100f / plank.pixelsPerUnit);
            stretch((RectTransform)plateImage.transform);
            var coin = createImage("CoinIcon", balanceRect, coinIcon);
            setRect((RectTransform)coin.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(44, 0), new Vector2(28, 28));
            var balanceText = createText("Balance", balanceRect, font, 25, TextAlignmentOptions.MidlineLeft);
            setRect(balanceText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(.5f, .5f), new Vector2(40, 0), new Vector2(-100, 0));
            // 다음날 글자처럼 크림색 글씨에 짙은 갈색 테두리.
            balanceText.color = PlankTextColor;
            balanceText.outlineWidth = 0.25f;
            balanceText.outlineColor = PlankOutlineColor;
            balanceText.text = "0원";
            var deltaText = createText("Delta", balanceRect, font, 22, TextAlignmentOptions.Center);
            setRect(deltaText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(0, 26));
            deltaText.outlineWidth = 0.2f;
            deltaText.outlineColor = new Color32(20, 14, 10, 255);
            deltaText.text = string.Empty;

            var presenter = balanceObject.GetComponent<ShopStatusHudPresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("rootGroup").objectReferenceValue = balanceObject.GetComponent<CanvasGroup>();
            serialized.FindProperty("balanceText").objectReferenceValue = balanceText;
            serialized.FindProperty("deltaText").objectReferenceValue = deltaText;
            serialized.FindProperty("plateRect").objectReferenceValue = balanceRect;
            serialized.FindProperty("balanceLabel").stringValue = "소지금 ";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var group = balanceObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var controller = root.GetComponentInChildren<GameUIController>(true);
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("settlementBalance").objectReferenceValue = presenter;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>화면 오른쪽 위 톱니바퀴 설정 버튼과 볼륨 설정 창을 만듭니다.</summary>
    /// <param name="hudRect">명패 루트(화면 전체)입니다.</param>
    /// <param name="font">글꼴입니다.</param>
    /// <param name="plate">철판 9-slice 이미지입니다.</param>
    private static void createOptions(RectTransform hudRect, TMP_FontAsset font, Sprite plate)
    {
        var gear = AssetDatabase.LoadAssetAtPath<Sprite>(GearIconPath)
            ?? throw new System.InvalidOperationException("톱니바퀴 아이콘이 없습니다.");
        var optionsObject = new GameObject("Options", typeof(RectTransform), typeof(OptionsPanelPresenter));
        var optionsRect = (RectTransform)optionsObject.transform;
        optionsRect.SetParent(hudRect, false);
        setRect(optionsRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(48, 40));

        var buttonImage = createImage("Gear", optionsRect, plate);
        buttonImage.type = Image.Type.Sliced;
        buttonImage.pixelsPerUnitMultiplier = 0.5f;
        buttonImage.raycastTarget = true;
        stretch((RectTransform)buttonImage.transform);
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        var icon = createImage("Icon", buttonImage.transform, gear);
        setRect((RectTransform)icon.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(28, 28));

        var panelImage = createImage("Panel", optionsRect, plate);
        panelImage.type = Image.Type.Sliced;
        panelImage.pixelsPerUnitMultiplier = 0.5f;
        panelImage.raycastTarget = true;
        var panelRect = (RectTransform)panelImage.transform;
        setRect(panelRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -46), new Vector2(320, 200));

        var title = createText("Title", panelRect, font, 20, TextAlignmentOptions.Center);
        setRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(0, 26));
        title.text = "설정";
        title.color = new Color(0.95f, 0.84f, 0.55f);

        string[] labels = { "전체 소리", "배경음", "효과음" };
        var sliders = new Slider[labels.Length];
        var resources = new DefaultControls.Resources();
        for (int index = 0; index < labels.Length; index++)
        {
            float y = -50 - index * 42;
            var label = createText("Label" + index, panelRect, font, 16, TextAlignmentOptions.MidlineLeft);
            setRect(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, y), new Vector2(90, 24));
            label.text = labels[index];
            var sliderObject = DefaultControls.CreateSlider(resources);
            sliderObject.name = "Slider" + index;
            var sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.SetParent(panelRect, false);
            setRect(sliderRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, y - 4), new Vector2(186, 16));
            sliders[index] = sliderObject.GetComponent<Slider>();
            sliders[index].minValue = 0f;
            sliders[index].maxValue = 1f;
            sliders[index].value = 1f;
            // 기본 흰 슬라이더를 녹슨 철판 톤으로 맞춘다.
            foreach (var image in sliderObject.GetComponentsInChildren<Image>(true))
                image.color = image.name == "Handle" ? new Color(0.95f, 0.84f, 0.55f) : image.name == "Fill"
                    ? new Color(0.62f, 0.45f, 0.25f) : new Color(0.18f, 0.15f, 0.13f);
        }

        var closeImage = createImage("Close", panelRect, plate);
        closeImage.type = Image.Type.Sliced;
        closeImage.pixelsPerUnitMultiplier = 0.5f;
        closeImage.raycastTarget = true;
        setRect((RectTransform)closeImage.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(90, 30));
        var closeButton = closeImage.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        var closeLabel = createText("Label", closeImage.transform, font, 16, TextAlignmentOptions.Center);
        stretch(closeLabel.rectTransform);
        closeLabel.text = "닫기";

        panelImage.gameObject.SetActive(false);
        var serialized = new SerializedObject(optionsObject.GetComponent<OptionsPanelPresenter>());
        serialized.FindProperty("toggleButton").objectReferenceValue = button;
        serialized.FindProperty("panel").objectReferenceValue = panelImage.gameObject;
        serialized.FindProperty("masterSlider").objectReferenceValue = sliders[0];
        serialized.FindProperty("bgmSlider").objectReferenceValue = sliders[1];
        serialized.FindProperty("sfxSlider").objectReferenceValue = sliders[2];
        serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
