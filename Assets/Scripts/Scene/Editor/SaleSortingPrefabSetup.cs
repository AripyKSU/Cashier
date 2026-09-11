#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>GameUI Prefab에 판매 분류 작업대와 필수 직렬화 참조를 반복 가능하게 구성합니다.</summary>
public static class SaleSortingPrefabSetup
{
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string OperatingPrefabPath = "Assets/Prefabs/GameUI/OperatingPanel.prefab";
    private const string PreOpenPrefabPath = "Assets/Prefabs/GameUI/PreOpenPanel.prefab";
    private const string ProgressScenePath = "Assets/Scenes/Local/ProgressScene.unity";
    private const string WorkbenchPath = "Assets/DystopiaPrototype/TopDownTest/Art/TopDownWorkbench.png";
    private const string CalculatorPath = "Assets/DystopiaPrototype/TopDownTest/Art/Calculator.png";
    private const string CalculatorTogglePath = "Assets/DystopiaPrototype/TopDownTest/Art/CalculatorToggle.png";
    private const string TiltedContainerPath = "Assets/DystopiaPrototype/TopDownTest/Art/TopDownContainerTilted.png";
    private const string EmptyContainerPath = "Assets/DystopiaPrototype/TopDownTest/Art/TopDownContainerEmpty.png";
    private const string FrontContainerPath = "Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerMale.png";
    private const string FarBackgroundPath = "Assets/Textures/Environment/Dystopia/FARBACKGROUND.png";
    private const string SeoulPath = "Assets/Textures/Environment/Dystopia/Seoul.png";
    private const string FogBackPath = "Assets/Textures/Environment/Dystopia/FogBack.png";
    private const string FogMidPath = "Assets/Textures/Environment/Dystopia/FogMid.png";
    private const string FogFrontPath = "Assets/Textures/Environment/Dystopia/FogFront.png";
    private const string FogBackMatPath = "Assets/Materials/Dystopia/FogBack.mat";
    private const string FogMidMatPath = "Assets/Materials/Dystopia/FogMid.mat";
    private const string FogFrontMatPath = "Assets/Materials/Dystopia/FogFront.mat";
    private const string ChimneySmoke0Path = "Assets/Textures/Environment/Dystopia/ChimneySmoke0.png";
    private const string WatchGuardPath = "Assets/Textures/Environment/Dystopia/WatchGuard.png";
    private const string MidBackgroundPath = "Assets/DystopiaPrototype/Art/MidBackground.png";
    private const string CrowdBackPath = "Assets/DystopiaPrototype/Art/CrowdBack.png";
    private const string CrowdMiddlePath = "Assets/DystopiaPrototype/Art/CrowdMiddle.png";
    private const string CrowdFrontPath = "Assets/DystopiaPrototype/Art/CrowdFront.png";
    private const string LeftWatchTowerPath = "Assets/DystopiaPrototype/Art/LeftWatchTower.png";
    private const string RightWatchTowerPath = "Assets/DystopiaPrototype/Art/RightWatchTower.png";
    private const string BarricadePath = "Assets/DystopiaPrototype/Art/BoothBarricade.png";
    private const string CanopyPath = "Assets/DystopiaPrototype/Art/BoothCanopy.png";
    private const string CounterPath = "Assets/DystopiaPrototype/Art/BoothCounter.png";
    private const string DailyInstructionPath = "Assets/DystopiaPrototype/Art/DailyInstruction.png";
    private const string CounterClockPath = "Assets/DystopiaPrototype/Art/시계.png";
    private const string DividerBarPath = "Assets/DystopiaPrototype/TopDownTest/Art/DividerBar.png";
    private const string DialogueFramePath = "Assets/DystopiaPrototype/Art/DialogueFrame.png";
    private const string MabinogiFontPath = "Assets/TextMesh Pro/Fonts/Mabinogi_Classic_OTF SDF.asset";
    private const string DawnPath = "Assets/Textures/Environment/Dystopia/TimeOfDay/Dawn.png";
    private const string SunsetPath = "Assets/Textures/Environment/Dystopia/TimeOfDay/Sunset.png";
    private const string EveningPath = "Assets/Textures/Environment/Dystopia/TimeOfDay/Evening.png";
    private const string CityLightsPath = "Assets/Textures/Environment/Dystopia/TimeOfDay/CityLights.png";
    private const string CityLightsMatPath = "Assets/Materials/Dystopia/CityLights.mat";
    private const string SearchlightPath = "Assets/Textures/Environment/Dystopia/TimeOfDay/Searchlight.png";
    private const string CounterLightPath = "Assets/Textures/Environment/Dystopia/TimeOfDay/CounterLight.png";
    private const string GuardNeutralMatPath = "Assets/Materials/Dystopia/GuardNeutral.mat";
    private const string PixelStageLightingShaderPath = "Assets/Shaders/Dystopia/PixelStageLighting.shader";

    /// <summary>현재 GameUI Prefab에 작업대 UI를 생성하거나 기존 구성을 갱신합니다.</summary>
    [MenuItem("Cashier/Setup Sale Sorting UI")]
    public static void Setup()
    {
        setupPriceGuidePrefab();
        setupCalculatorPrefab();
        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            Transform operating = findChild(root.transform, "OperatingPanel");
            GameUIController gameUiController = root.GetComponentInChildren<GameUIController>(true);
            if (operating == null || gameUiController == null)
            {
                throw new InvalidOperationException("GameUI Prefab의 OperatingPanel 또는 GameUIController를 찾을 수 없습니다.");
            }

            stretch((RectTransform)operating);
            CanvasScaler canvasScaler = root.GetComponentInChildren<CanvasScaler>(true);
            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1280f, 720f);
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 0.5f;
            }

            Transform existing = operating.Find("SaleSortingUI");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            RectTransform sortingRoot = createRect("SaleSortingUI", operating, Vector2.zero, Vector2.zero);
            stretch(sortingtRoot: sortingRoot);
            sortingRoot.SetAsFirstSibling();

            SaleSortingPanel panel = operating.GetComponent<SaleSortingPanel>();
            if (panel == null)
            {
                panel = operating.gameObject.AddComponent<SaleSortingPanel>();
            }
            RectTransform workArea = createRect("Workbench", sortingRoot, Vector2.zero, Vector2.zero);
            stretch(sortingtRoot: workArea);
            Image workbenchImage = workArea.gameObject.AddComponent<Image>();
            workbenchImage.sprite = loadSprite(WorkbenchPath);
            workbenchImage.preserveAspect = false;
            workbenchImage.raycastTarget = false;

            RectTransform excluded = createRect("ExcludedZone", workArea, new Vector2(-560f, 0f), new Vector2(150f, 610f));

            RectTransform sale = createRect("ForSaleZone", workArea, new Vector2(410f, 125f), new Vector2(300f, 230f));

            RectTransform itemRoot = createRect("ItemRoot", workArea, Vector2.zero, Vector2.zero);
            stretch(sortingtRoot: itemRoot);
            itemRoot.SetAsLastSibling();

            RectTransform itemTemplate = createRect("ItemTemplate", itemRoot, Vector2.zero, new Vector2(72f, 72f));
            Image itemImage = itemTemplate.gameObject.AddComponent<Image>();
            itemImage.color = Color.white;
            itemImage.raycastTarget = false;
            SaleSortingItemView itemView = itemTemplate.gameObject.AddComponent<SaleSortingItemView>();
            itemTemplate.gameObject.SetActive(false);

            RectTransform dividerRect = createRect("DividerBar", workArea, new Vector2(-420f, 0f), new Vector2(480f, 48f));
            dividerRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
            dividerRect.SetAsLastSibling();
            Image dividerImage = dividerRect.gameObject.AddComponent<Image>();
            dividerImage.sprite = loadSprite(DividerBarPath);
            dividerImage.preserveAspect = false;
            dividerImage.raycastTarget = false;
            DividerBarController dividerController = dividerRect.gameObject.AddComponent<DividerBarController>();

            RectTransform container = createRect("PouringContainer", sortingRoot, new Vector2(-460f, 60f), new Vector2(420f, 420f));
            container.localRotation = Quaternion.Euler(0f, 0f, -90f);
            Image containerImage = container.gameObject.AddComponent<Image>();
            containerImage.sprite = loadSprite(TiltedContainerPath);
            containerImage.preserveAspect = true;
            containerImage.raycastTarget = false;
            container.gameObject.SetActive(false);

            RectTransform calculator = findChild(operating, "PriceInput") as RectTransform;
            if (calculator == null)
            {
                throw new InvalidOperationException("기존 PriceInput 계산기 패널을 찾을 수 없습니다.");
            }

            Image calculatorImage = calculator.GetComponent<Image>();
            if (calculatorImage != null)
            {
                calculatorImage.sprite = loadSprite(CalculatorPath);
            }

            RectTransform toggleRect = createRect("CalculatorToggle", operating, new Vector2(553f, -278f), new Vector2(78f, 78f));
            Image toggleImage = toggleRect.gameObject.AddComponent<Image>();
            toggleImage.sprite = loadSprite(CalculatorTogglePath);
            toggleImage.preserveAspect = true;
            Button toggleButton = toggleRect.gameObject.AddComponent<Button>();
            toggleButton.targetGraphic = toggleImage;
            toggleRect.SetAsLastSibling();

            removeDirectChildrenByName(operating, "CalculatorToggle", toggleRect);

            Transform frontView = findChild(operating, "AstraFrontView");
            if (frontView == null)
            {
                throw new InvalidOperationException("Astra 전면 화면을 찾을 수 없습니다.");
            }

            removeNestedObjectsOutsideParent(operating, "FrontContainer", frontView);
            Transform customer = frontView.Find("Customer");
            Transform frontContainer = frontView.Find("FrontContainer");
            if (frontContainer == null)
            {
                throw new InvalidOperationException("매대 위 박스를 찾을 수 없습니다.");
            }

            Button frontContainerButton = frontContainer.GetComponent<Button>();

            SerializedObject panelObject = new SerializedObject(panel);
            setObject(panelObject, "frontView", frontView.gameObject);
            setObject(panelObject, "sortingView", sortingRoot.gameObject);
            setObject(panelObject, "workArea", workArea);
            setObject(panelObject, "itemRoot", itemRoot);
            setObject(panelObject, "excludedZone", excluded);
            setObject(panelObject, "saleZone", sale);
            setObject(panelObject, "transitionOverlay", null);
            setObject(panelObject, "containerImage", containerImage);
            setObject(panelObject, "tiltedContainerSprite", loadSprite(TiltedContainerPath));
            setObject(panelObject, "emptyContainerSprite", loadSprite(EmptyContainerPath));
            setObject(panelObject, "frontContainerButton", frontContainerButton);
            Transform basket = customer == null ? null : findChild(customer, "Basket");
            setObject(panelObject, "frontBasketRoot", basket == null ? null : basket.gameObject);
            setObject(panelObject, "calculatorPanel", calculator);
            setObject(panelObject, "calculatorToggleButton", toggleButton);
            setObject(panelObject, "calculatorOpenSprite", loadSprite(CalculatorTogglePath));
            setObject(panelObject, "calculatorClosedSprite", loadSprite(CalculatorTogglePath));
            setObject(panelObject, "itemPrefab", itemView);
            setObject(panelObject, "sortingStatusText", null);
            setObject(panelObject, "dividerBar", dividerController);
            setFloat(panelObject, "cursorRadiusPixels", 30f);
            setFloat(panelObject, "cursorImpulse", 0.065f);
            setFloat(panelObject, "maximumSpeedPixels", 230f);
            setFloat(panelObject, "frictionPerSecond", 6.5f);
            setFloat(panelObject, "itemRestitution", 0.1f);
            setFloat(panelObject, "transitionSeconds", 1f);
            panelObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerObject = new SerializedObject(gameUiController);
            setObject(controllerObject, "saleSortingPanel", panel);
            Button[] mappedButtons = findCalculatorButtons(operating);
            SerializedProperty keypadButtonsProperty = controllerObject.FindProperty("keypadButtons");
            keypadButtonsProperty.arraySize = mappedButtons.Length;
            for (int i = 0; i < mappedButtons.Length; i++)
            {
                keypadButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = mappedButtons[i];
            }
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            Transform visualRoot = findChild(root.transform, "Root");
            setDirectChildrenInactive(visualRoot, "Background", "Timer", "Pause", "Resume", "PauseIndicator", "CommonHUD");

            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
            Debug.Log("[SaleSortingPrefabSetup] GameUI Prefab 판매 분류 UI 구성을 완료했습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>기존 버튼 그래픽을 제거하고 계산기 원본 이미지 위에 기능별 투명 버튼 영역을 배치합니다.</summary>
    private static void setupCalculatorPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(OperatingPrefabPath);
        try
        {
            setupFrontView(root.transform);

            Image operatingBackground = root.GetComponent<Image>();
            if (operatingBackground != null)
            {
                operatingBackground.raycastTarget = false;
                operatingBackground.color = new Color(0.08f, 0.12f, 0.14f, 0f);
            }

            RectTransform calculator = findChild(root.transform, "PriceInput") as RectTransform;
            KeypadController keypad = root.GetComponent<KeypadController>();
            KeypadButtonBinder binder = root.GetComponent<KeypadButtonBinder>();
            PriceInputPresenter presenter = root.GetComponent<PriceInputPresenter>();
            if (calculator == null || keypad == null || binder == null || presenter == null)
            {
                throw new InvalidOperationException("OperatingPanel의 계산기 필수 컴포넌트를 찾을 수 없습니다.");
            }

            string[] removableNames =
            {
                "Cancel", "Confirm", "Number0", "Number1", "Number2", "Number3", "Number4",
                "Number5", "Number6", "Number7", "Number8", "Number9", "DoubleZero", "Backspace",
                "TripleZero"
            };
            foreach (string removableName in removableNames)
            {
                Transform child = calculator.Find(removableName);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            calculator.sizeDelta = new Vector2(360f, 362f);
            calculator.anchoredPosition = new Vector2(435f, -95f);
            Image artwork = calculator.GetComponent<Image>();
            artwork.sprite = loadSprite(CalculatorPath);
            artwork.color = Color.white;
            artwork.preserveAspect = true;

            TextMeshProUGUI priceText = calculator.Find("Price").GetComponent<TextMeshProUGUI>();
            setTopLeftRect(priceText.rectTransform, 165f, 155f, 910f, 220f);
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.fontSize = 29f;

            TextMeshProUGUI validationText = calculator.Find("Validation").GetComponent<TextMeshProUGUI>();
            validationText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            validationText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            validationText.rectTransform.pivot = new Vector2(0.5f, 1f);
            validationText.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            validationText.rectTransform.sizeDelta = new Vector2(360f, 26f);
            validationText.alignment = TextAlignmentOptions.Center;
            validationText.fontSize = 15f;

            TextMeshProUGUI transactionStatus = calculator.Find("TransactionStatus").GetComponent<TextMeshProUGUI>();
            transactionStatus.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            transactionStatus.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            transactionStatus.rectTransform.pivot = new Vector2(0.5f, 1f);
            transactionStatus.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            transactionStatus.rectTransform.sizeDelta = new Vector2(360f, 30f);
            transactionStatus.alignment = TextAlignmentOptions.Center;
            transactionStatus.fontSize = 15f;

            Button[] numbers = new Button[10];
            for (int number = 1; number <= 9; number++)
            {
                int index = number - 1;
                float x = 147f + (index % 3) * 243f;
                float y = 428f + (index / 3) * 166f;
                numbers[number] = createArtworkButton(calculator, $"Number{number}", x, y, 214f, 151f);
            }

            Button backspace = createArtworkButton(calculator, "Backspace", 877f, 428f, 214f, 151f);
            Button tripleZero = createArtworkButton(calculator, "TripleZero", 877f, 594f, 214f, 151f);
            Button doubleZero = createArtworkButton(calculator, "DoubleZero", 877f, 760f, 214f, 151f);
            Button cancel = createArtworkButton(calculator, "Cancel", 147f, 922f, 454f, 191f);
            Button confirm = createArtworkButton(calculator, "Confirm", 635f, 922f, 456f, 191f);

            Transform continueTransform = calculator.Find("Continue");
            if (continueTransform != null)
            {
                setTopLeftRect((RectTransform)continueTransform, 635f, 922f, 456f, 191f);
                Image continueImage = continueTransform.GetComponent<Image>();
                if (continueImage != null) continueImage.color = new Color(1f, 1f, 1f, 0.001f);
                foreach (TextMeshProUGUI label in continueTransform.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    label.gameObject.SetActive(false);
                }
            }

            SerializedObject keypadObject = new SerializedObject(keypad);
            setObject(keypadObject, "priceDisplayText", priceText);
            keypadObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject binderObject = new SerializedObject(binder);
            SerializedProperty numbersProperty = binderObject.FindProperty("numberButtons");
            numbersProperty.arraySize = numbers.Length;
            for (int i = 0; i < numbers.Length; i++)
            {
                numbersProperty.GetArrayElementAtIndex(i).objectReferenceValue = numbers[i];
            }
            setObject(binderObject, "doubleZeroButton", doubleZero);
            setObject(binderObject, "tripleZeroButton", tripleZero);
            setObject(binderObject, "backspaceButton", backspace);
            binderObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject presenterObject = new SerializedObject(presenter);
            setObject(presenterObject, "priceDisplayText", priceText);
            setObject(presenterObject, "validationMessageText", validationText);
            setObject(presenterObject, "confirmButton", confirm);
            setObject(presenterObject, "cancelButton", cancel);
            setObject(presenterObject, "keypadController", keypad);
            presenterObject.ApplyModifiedPropertiesWithoutUndo();

            setDirectChildrenInactive(root.transform, "Dialogue");
            validationText.gameObject.SetActive(false);
            transactionStatus.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, OperatingPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void setInitialHidden(RectTransform rect)
    {
        var cr = rect.GetComponent<CanvasRenderer>();
        if (cr != null) cr.SetColor(new Color(1f, 1f, 1f, 0f));
    }

    /// <summary>Astra 전면 화면의 배경, 손님 위치, 매대와 클릭 가능한 박스를 구성합니다.</summary>
    /// <param name="operating">OperatingPanel Prefab 루트입니다.</param>
    private static void setupFrontView(Transform operating)
    {
        stretch((RectTransform)operating);
        Transform customer = findChild(operating, "Customer");
        RectTransform frontView;
        Transform existing = operating.Find("AstraFrontView");
        if (existing != null)
        {
            frontView = (RectTransform)existing;
            if (customer != null && customer.IsChildOf(frontView))
            {
                customer.SetParent(operating, false);
            }
            for (int i = frontView.childCount - 1; i >= 0; i--)
            {
                Transform child = frontView.GetChild(i);
                if (child.name == "FrontContainer") continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
        else
        {
            frontView = createRect("AstraFrontView", operating, Vector2.zero, Vector2.zero);
        }
        stretch(frontView);
        frontView.SetAsFirstSibling();

        // 1. 최원경 하늘 (내장 SkyBirds 셰이더 연출 포함)
        createFrontImage(frontView, "FarBackground", FarBackgroundPath, 0f, 0f, 1280f, 720f, "Assets/DystopiaPrototype/Art/FARBACKGROUND.png");

        // 2. 시간대별 하늘 페이드 레이어 (09~12시 새벽/아침, 15~18시 석양, 18~21시 야간)
        RectTransform dawnRt = createFrontImage(frontView, "DawnBackground", DawnPath, 0f, 0f, 1280f, 720f, "Assets/DystopiaPrototype/Art/TimeOfDay/Dawn.png");
        setInitialHidden(dawnRt);

        RectTransform sunsetRt = createFrontImage(frontView, "SunsetBackground", SunsetPath, 0f, 0f, 1280f, 720f, "Assets/DystopiaPrototype/Art/TimeOfDay/Sunset.png");
        setInitialHidden(sunsetRt);

        RectTransform eveningRt = createFrontImage(frontView, "EveningBackground", EveningPath, 0f, 0f, 1280f, 720f, "Assets/DystopiaPrototype/Art/TimeOfDay/Evening.png");
        setInitialHidden(eveningRt);

        // 3. 야간 원경 도심 창문 불빛
        RectTransform cityRect = createFrontImage(frontView, "CityLights", CityLightsPath, 0f, 0f, 1280f, 720f, "Assets/DystopiaPrototype/Art/TimeOfDay/CityLights.png");
        Image cityImg = cityRect.GetComponent<Image>();
        Material cityMat = loadMaterial(CityLightsMatPath, "Assets/DystopiaPrototype/Art/TimeOfDay/CityLights.mat");
        if (cityMat != null) cityImg.material = cityMat;
        setInitialHidden(cityRect);

        // 4. 좌/우 공장 굴뚝 연기 (4프레임 교체 및 미세 부유)
        createChimneySmoke(frontView, "LeftChimneySmoke", ChimneySmoke0Path, new Vector2(182f, -12f), new Vector2(62f, 137f), "Assets/DystopiaPrototype/Art/ChimneySmoke0.png");
        createChimneySmoke(frontView, "RightChimneySmoke", ChimneySmoke0Path, new Vector2(1152f, -23f), new Vector2(47f, 106f), "Assets/DystopiaPrototype/Art/ChimneySmoke0.png");

        // 5. 3단계 픽셀 안개 (프로토타입 사양: MidBackground 이전, Y=390 상단 330px 영역에만 렌더링, 군중·손님·가판 앞 중첩 방지)
        createFogLayer(frontView, "FogBack", FogBackPath, FogBackMatPath, "Assets/DystopiaPrototype/Art/FogBack.mat", "Assets/DystopiaPrototype/Art/FogBack.png");
        createFogLayer(frontView, "FogMid", FogMidPath, FogMidMatPath, "Assets/DystopiaPrototype/Art/FogMid.mat", "Assets/DystopiaPrototype/Art/FogMid.png");
        createFogLayer(frontView, "FogFront", FogFrontPath, FogFrontMatPath, "Assets/DystopiaPrototype/Art/FogFront.mat", "Assets/DystopiaPrototype/Art/FogFront.png");

        // 6. 중경 언덕 및 실루엣
        createFrontImage(frontView, "MidBackground", MidBackgroundPath, 0f, 46f, 1280f, 576f);

        // 7. 좌/우 감시탑 구조물
        createFrontImage(frontView, "LeftWatchTower", LeftWatchTowerPath, 0f, 0f, 1280f, 720f);
        createFrontImage(frontView, "RightWatchTower", RightWatchTowerPath, 0f, 0f, 1280f, 720f);

        // 8. 감시탑 난간 마스크 (경비병의 하체가 탑 난간 뒤에 가려지도록 클리핑)
        createWatchRailMask(frontView, "LeftWatchRailMask", "LeftWatchRail", LeftWatchTowerPath, new Vector2(62f, -171f), new Vector2(132f, 57f), new Vector2(-62f, 171f));
        createWatchRailMask(frontView, "RightWatchRailMask", "RightWatchRail", RightWatchTowerPath, new Vector2(1132f, -228f), new Vector2(80f, 35f), new Vector2(-1132f, 228f));

        // 9. 좌/우 감시탑 경비병 (선회, 체중 이동, 외곽 조준)
        createWatchGuard(frontView, "LeftWatchGuard", WatchGuardPath, new Vector2(163f, -174f), new Vector2(44f, 34f), "Assets/DystopiaPrototype/Art/WatchGuard.png");
        createWatchGuard(frontView, "RightWatchGuard", WatchGuardPath, new Vector2(1170f, -232f), new Vector2(32f, 24f), "Assets/DystopiaPrototype/Art/WatchGuard.png");

        // 10. 주기적 외곽 사격 총구 불꽃
        createWatchMuzzleFlash(frontView, "WatchMuzzleFlash0");
        createWatchMuzzleFlash(frontView, "WatchMuzzleFlash1");

        // 11. 3행 군중 정점 변위 메시 이미지 (후열, 중열, 전열)
        createCrowdRow(frontView, "CrowdBack", CrowdBackPath, 0, -4f, -119f, 1288f, 979f);
        createCrowdRow(frontView, "CrowdMiddle", CrowdMiddlePath, 1, -4f, -119f, 1288f, 979f);
        createCrowdRow(frontView, "CrowdFront", CrowdFrontPath, 2, -4f, -119f, 1288f, 979f);

        // 14. 가판 뒤 바리케이드
        createFrontImage(frontView, "Barricade", BarricadePath, -14f, 305f, 1308f, 270f);

        // 15. 가판 천막
        createFrontImage(frontView, "Canopy", CanopyPath, 0f, 0f, 1280f, 720f);

        // 16. 야간 감시탑 서치라이트 탐조등 (좌/우)
        RectTransform leftBeamRt = createRect("LeftBeam", frontView, Vector2.zero, Vector2.zero);
        leftBeamRt.anchorMin = new Vector2(0f, 1f);
        leftBeamRt.anchorMax = new Vector2(0f, 1f);
        leftBeamRt.pivot = new Vector2(0f, 0.5f);
        leftBeamRt.anchoredPosition = new Vector2(110f, -164f);
        leftBeamRt.sizeDelta = new Vector2(1800f, 360f);
        leftBeamRt.localRotation = Quaternion.Euler(0f, 0f, -7.67f);
        Image leftBeamImg = leftBeamRt.gameObject.AddComponent<Image>();
        leftBeamImg.sprite = loadSprite(SearchlightPath, "Assets/DystopiaPrototype/Art/TimeOfDay/Searchlight.png");
        leftBeamImg.color = Color.white;
        leftBeamImg.raycastTarget = false;
        setInitialHidden(leftBeamRt);

        RectTransform rightBeamRt = createRect("RightBeam", frontView, Vector2.zero, Vector2.zero);
        rightBeamRt.anchorMin = new Vector2(0f, 1f);
        rightBeamRt.anchorMax = new Vector2(0f, 1f);
        rightBeamRt.pivot = new Vector2(0f, 0.5f);
        rightBeamRt.anchoredPosition = new Vector2(1140f, -156f);
        rightBeamRt.sizeDelta = new Vector2(1800f, 366.67f);
        rightBeamRt.localRotation = Quaternion.Euler(0f, 0f, 186.29f);
        Image rightBeamImg = rightBeamRt.gameObject.AddComponent<Image>();
        rightBeamImg.sprite = loadSprite(SearchlightPath, "Assets/DystopiaPrototype/Art/TimeOfDay/Searchlight.png");
        rightBeamImg.color = Color.white;
        rightBeamImg.raycastTarget = false;
        setInitialHidden(rightBeamRt);

        // 17. 가판대 매대 전면
        createFrontImage(frontView, "Counter", CounterPath, 0f, 0f, 1280f, 720f);

        // 18. 야간 가판대 조명
        RectTransform counterLightRt = createRect("CounterLight", frontView, Vector2.zero, Vector2.zero);
        counterLightRt.anchorMin = new Vector2(0f, 1f);
        counterLightRt.anchorMax = new Vector2(0f, 1f);
        counterLightRt.pivot = new Vector2(0f, 1f);
        counterLightRt.anchoredPosition = new Vector2(355f, -455f);
        counterLightRt.sizeDelta = new Vector2(600f, 180f);
        Image counterLightImg = counterLightRt.gameObject.AddComponent<Image>();
        counterLightImg.sprite = loadSprite(CounterLightPath, "Assets/DystopiaPrototype/Art/TimeOfDay/CounterLight.png");
        counterLightImg.color = Color.white;
        counterLightImg.raycastTarget = false;
        setInitialHidden(counterLightRt);

        // 19. 손님
        if (customer != null)
        {
            customer.SetParent(frontView, false);
            setFrontRect((RectTransform)customer, 305f, 82f, 550f, 550f);
            customer.gameObject.SetActive(true);
            setDirectChildrenInactive(customer, "Dialogue", "Basket");
        }

        // 20. 상자와 시계를 화면 및 매대 정중앙(X=640)에 맞춰 가운데 정렬 배치합니다.
        RectTransform clockRect = createFrontImage(frontView, "CounterClock", CounterClockPath, 550f, 605f, 180f, 90f);
        Image clockImage = clockRect.GetComponent<Image>();
        clockImage.preserveAspect = true;
        clockImage.raycastTarget = false;
        BusinessClockController clockController = clockRect.gameObject.AddComponent<BusinessClockController>();

        RectTransform clockTextRect = createRect("ClockText", clockRect, Vector2.zero, new Vector2(110f, 28f));
        clockTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        clockTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        clockTextRect.pivot = new Vector2(0.5f, 0.5f);
        clockTextRect.anchoredPosition = new Vector2(0f, -2f);
        TextMeshProUGUI clockText = clockTextRect.gameObject.AddComponent<TextMeshProUGUI>();
        clockText.text = "09:00";
        clockText.fontSize = 22f;
        clockText.fontStyle = FontStyles.Bold;
        clockText.alignment = TextAlignmentOptions.Center;
        clockText.color = new Color(0.40f, 0.58f, 0.43f, 1f);
        clockText.raycastTarget = false;

        SerializedObject clockObj = new SerializedObject(clockController);
        setObject(clockObj, "clockText", clockText);
        clockObj.ApplyModifiedPropertiesWithoutUndo();

        // 21. FrontContainer(360x240, CenterX=640, Desk Contact Y=-578) - 기존 파일ID 보존
        Transform existingContainer = frontView.Find("FrontContainer");
        RectTransform container;
        if (existingContainer != null)
        {
            container = (RectTransform)existingContainer;
            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(0f, 1f);
            container.pivot = new Vector2(0f, 1f);
            container.anchoredPosition = new Vector2(460f, -350f);
            container.sizeDelta = new Vector2(360f, 240f);
        }
        else
        {
            container = createFrontImage(
                frontView,
                "FrontContainer",
                FrontContainerPath,
                460f,
                350f,
                360f,
                240f);
        }
        Image containerImage = container.GetComponent<Image>();
        if (containerImage == null) containerImage = container.gameObject.AddComponent<Image>();
        containerImage.sprite = loadSprite(FrontContainerPath);
        containerImage.preserveAspect = true;
        containerImage.raycastTarget = true;
        Button button = container.GetComponent<Button>();
        if (button == null) button = container.gameObject.AddComponent<Button>();
        button.targetGraphic = containerImage;
        button.transition = Selectable.Transition.None;

        // 22. 대화창 (항상 최상단 렌더링)
        setupDialoguePanel(frontView, operating);

        // 23. TimeOfDayPixelStage (480x270 저해상도 픽셀 스테이지 가동 및 Point 확대)
        TimeOfDayPixelStage pixelStage = frontView.gameObject.GetComponent<TimeOfDayPixelStage>();
        if (pixelStage == null) pixelStage = frontView.gameObject.AddComponent<TimeOfDayPixelStage>();
        pixelStage.frontCanvas = frontView;
        pixelStage.lightingShader = AssetDatabase.LoadAssetAtPath<Shader>(PixelStageLightingShaderPath) ?? Shader.Find("Cashier/PixelStageLighting");
        pixelStage.width = 480;
        pixelStage.previewInEditor = true;

        var stageLayers = new System.Collections.Generic.List<TimeOfDayPixelStage.Layer>();
        void addStageLayer(string childPath, TimeOfDayPixelStage.Surface surface, float roomResponse = 0f, float lampResponse = 0.15f)
        {
            Transform t = frontView.Find(childPath);
            if (t == null) return;
            Graphic g = t.GetComponent<Graphic>();
            if (g == null) return;
            stageLayers.Add(new TimeOfDayPixelStage.Layer
            {
                source = g,
                surface = surface,
                roomResponse = roomResponse,
                lampResponse = lampResponse
            });
        }

        addStageLayer("FarBackground", TimeOfDayPixelStage.Surface.Sky);
        addStageLayer("DawnBackground", TimeOfDayPixelStage.Surface.Sky);
        addStageLayer("SunsetBackground", TimeOfDayPixelStage.Surface.Sky);
        addStageLayer("EveningBackground", TimeOfDayPixelStage.Surface.Sky);
        addStageLayer("CityLights", TimeOfDayPixelStage.Surface.CityLights);
        addStageLayer("LeftChimneySmoke", TimeOfDayPixelStage.Surface.OriginalEffect);
        addStageLayer("RightChimneySmoke", TimeOfDayPixelStage.Surface.OriginalEffect);
        addStageLayer("FogBack", TimeOfDayPixelStage.Surface.OriginalEffect);
        addStageLayer("FogMid", TimeOfDayPixelStage.Surface.OriginalEffect);
        addStageLayer("FogFront", TimeOfDayPixelStage.Surface.OriginalEffect);
        addStageLayer("MidBackground", TimeOfDayPixelStage.Surface.Environment);
        addStageLayer("LeftWatchTower", TimeOfDayPixelStage.Surface.Metal);
        addStageLayer("RightWatchTower", TimeOfDayPixelStage.Surface.Metal);
        addStageLayer("LeftWatchRailMask/LeftWatchRail", TimeOfDayPixelStage.Surface.Metal);
        addStageLayer("RightWatchRailMask/RightWatchRail", TimeOfDayPixelStage.Surface.Metal);
        addStageLayer("LeftWatchGuard", TimeOfDayPixelStage.Surface.Person);
        addStageLayer("RightWatchGuard", TimeOfDayPixelStage.Surface.Person);
        addStageLayer("CrowdBack", TimeOfDayPixelStage.Surface.Environment);
        addStageLayer("CrowdMiddle", TimeOfDayPixelStage.Surface.Environment);
        addStageLayer("CrowdFront", TimeOfDayPixelStage.Surface.Environment);
        addStageLayer("Barricade", TimeOfDayPixelStage.Surface.Environment);
        addStageLayer("Canopy", TimeOfDayPixelStage.Surface.Environment, roomResponse: 0.2f);
        addStageLayer("LeftBeam", TimeOfDayPixelStage.Surface.Unlit, lampResponse: 0f);
        addStageLayer("RightBeam", TimeOfDayPixelStage.Surface.Unlit, lampResponse: 0f);
        addStageLayer("Counter", TimeOfDayPixelStage.Surface.Environment, roomResponse: 0.3f);
        addStageLayer("CounterLight", TimeOfDayPixelStage.Surface.Unlit, lampResponse: 0f);
        addStageLayer("Customer", TimeOfDayPixelStage.Surface.Person, lampResponse: 1f);
        addStageLayer("CounterClock", TimeOfDayPixelStage.Surface.Environment, lampResponse: 0.6f);
        addStageLayer("FrontContainer", TimeOfDayPixelStage.Surface.Metal, roomResponse: 0.5f, lampResponse: 1f);

        pixelStage.layers = stageLayers.ToArray();

        // 24. TimeOfDayUIController (시간대별 빛 및 틴트 제어)
        TimeOfDayUIController timeController = frontView.gameObject.GetComponent<TimeOfDayUIController>();
        if (timeController == null) timeController = frontView.gameObject.AddComponent<TimeOfDayUIController>();
        timeController.AutoResolveReferences();

        // 25. 배경 애니메이션 총괄 제어 컴포넌트 부착 및 초기화
        FrontBackgroundAnimationController animController = frontView.gameObject.GetComponent<FrontBackgroundAnimationController>();
        if (animController == null)
        {
            animController = frontView.gameObject.AddComponent<FrontBackgroundAnimationController>();
        }
        animController.Initialize();
    }

    /// <summary>Astra 지침서 배경 안에 현재 동적 가격 목록과 영업 시작 버튼을 배치합니다.</summary>
    private static void setupPriceGuidePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PreOpenPrefabPath);
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Image background = root.GetComponent<Image>();
            Transform title = root.transform.Find("Title");
            Transform description = root.transform.Find("Description");
            TextMeshProUGUI priceList = root.transform.Find("PriceList")?.GetComponent<TextMeshProUGUI>();
            RectTransform openBusiness = root.transform.Find("OpenBusiness") as RectTransform;
            if (rootRect == null || background == null || priceList == null || openBusiness == null)
            {
                throw new InvalidOperationException("PreOpenPanel의 가격표 필수 UI를 찾을 수 없습니다.");
            }

            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(680f, 680f);
            background.sprite = loadSprite(DailyInstructionPath);
            background.color = Color.white;
            background.preserveAspect = true;

            if (title != null) title.gameObject.SetActive(false);
            if (description != null) description.gameObject.SetActive(false);

            setGuideRect(priceList.rectTransform, 120f, 245f, 440f, 270f);
            priceList.color = new Color(0.18f, 0.18f, 0.17f, 1f);
            priceList.fontSize = 20f;
            priceList.alignment = TextAlignmentOptions.TopLeft;

            setGuideRect(openBusiness, 240f, 565f, 200f, 48f);
            Image buttonImage = openBusiness.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = new Color(0.78f, 0.76f, 0.69f, 1f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PreOpenPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>680×680 가격표의 좌상단 좌표를 RectTransform에 적용합니다.</summary>
    private static void setGuideRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>1280×720 기준 좌표로 전면 화면 이미지를 생성합니다.</summary>
    private static RectTransform createFrontImage(
        Transform parent,
        string name,
        string spritePath,
        float x,
        float y,
        float width,
        float height,
        string fallbackPath = null)
    {
        RectTransform rect = createRect(name, parent, Vector2.zero, Vector2.zero);
        setFrontRect(rect, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = loadSprite(spritePath, fallbackPath);
        image.color = Color.white;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>안개 레이어(PixelFog 셰이더 적용, Y=390 상단 오프셋)를 생성합니다.</summary>
    private static RectTransform createFogLayer(
        Transform parent,
        string name,
        string spritePath,
        string matPath,
        string fallbackMatPath = null,
        string fallbackSpritePath = null)
    {
        RectTransform rect = createRect(name, parent, Vector2.zero, Vector2.zero);
        setFrontRect(rect, 0f, -390f, 1280f, 720f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = loadSprite(spritePath, fallbackSpritePath);
        Material mat = loadMaterial(matPath, fallbackMatPath);
        if (mat != null) image.material = mat;
        image.color = Color.white;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>굴뚝 연기 이미지를 생성합니다.</summary>
    private static RectTransform createChimneySmoke(
        Transform parent,
        string name,
        string spritePath,
        Vector2 pos,
        Vector2 size,
        string fallbackPath = null)
    {
        RectTransform rect = createRect(name, parent, Vector2.zero, Vector2.zero);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = rect.gameObject.AddComponent<Image>();
        img.sprite = loadSprite(spritePath, fallbackPath);
        img.color = new Color(0.67f, 0.70f, 0.73f, 0.48f);
        img.raycastTarget = false;
        return rect;
    }

    /// <summary>감시탑 경비병 이미지를 생성합니다.</summary>
    private static RectTransform createWatchGuard(
        Transform parent,
        string name,
        string spritePath,
        Vector2 pos,
        Vector2 size,
        string fallbackPath = null)
    {
        RectTransform rect = createRect(name, parent, Vector2.zero, Vector2.zero);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = rect.gameObject.AddComponent<Image>();
        img.sprite = loadSprite(spritePath, fallbackPath);
        img.color = Color.white;
        img.preserveAspect = true;
        Material guardMat = loadMaterial(GuardNeutralMatPath, "Assets/DystopiaPrototype/Art/GuardNeutral.mat");
        if (guardMat != null) img.material = guardMat;
        img.raycastTarget = false;
        return rect;
    }

    /// <summary>감시탑 난간 뒤로 경비병 하체가 가려지도록 RectMask2D와 탑 이미지를 생성합니다.</summary>
    private static RectTransform createWatchRailMask(
        Transform parent,
        string maskName,
        string railName,
        string towerSpritePath,
        Vector2 maskPos,
        Vector2 maskSize,
        Vector2 railPos)
    {
        var maskGo = new GameObject(maskName, typeof(RectTransform), typeof(RectMask2D));
        RectTransform maskRt = maskGo.GetComponent<RectTransform>();
        maskRt.SetParent(parent, false);
        maskRt.anchorMin = new Vector2(0f, 1f);
        maskRt.anchorMax = new Vector2(0f, 1f);
        maskRt.pivot = new Vector2(0f, 1f);
        maskRt.anchoredPosition = maskPos;
        maskRt.sizeDelta = maskSize;

        var railGo = new GameObject(railName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform railRt = railGo.GetComponent<RectTransform>();
        railRt.SetParent(maskRt, false);
        railRt.anchorMin = new Vector2(0f, 1f);
        railRt.anchorMax = new Vector2(0f, 1f);
        railRt.pivot = new Vector2(0f, 1f);
        railRt.anchoredPosition = railPos;
        railRt.sizeDelta = new Vector2(1280f, 720f);

        Image img = railGo.GetComponent<Image>();
        img.sprite = loadSprite(towerSpritePath);
        img.color = Color.white;
        img.raycastTarget = false;

        return maskRt;
    }

    /// <summary>원경 하늘의 절차적 새 무리 비행 연출 객체를 구성합니다.</summary>
    private static FrontSkyBirds createSkyBirds(Transform parent, string name)
    {
        var birdsGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(FrontSkyBirds));
        RectTransform rt = birdsGo.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        stretch(rt);
        FrontSkyBirds birds = birdsGo.GetComponent<FrontSkyBirds>();
        birds.raycastTarget = false;
        return birds;
    }

    /// <summary>주기적 외곽 사격 총구 불꽃 객체를 구성합니다.</summary>
    private static RectTransform createWatchMuzzleFlash(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        createColorRect(rt, "Flame", new Vector2(-3f, 1f), new Vector2(10f, 2f), new Color(1f, 0.48f, 0.12f, 1f));
        createColorRect(rt, "Spark", new Vector2(0f, 3f), new Vector2(3f, 6f), new Color(1f, 0.7f, 0.2f, 1f));
        createColorRect(rt, "Core", new Vector2(-2f, 1f), new Vector2(5f, 2f), new Color(1f, 1f, 0.78f, 1f));

        go.SetActive(false);
        return rt;
    }

    /// <summary>단색 UI Image 사각형을 생성합니다.</summary>
    private static void createColorRect(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    /// <summary>군중 정점 변위 메시 이미지 컴포넌트를 생성합니다.</summary>
    private static FrontCrowdImage createCrowdRow(
        Transform parent,
        string name,
        string spritePath,
        int rowIndex,
        float x,
        float y,
        float width,
        float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(FrontCrowdImage));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        setFrontRect(rect, x, y, width, height);

        FrontCrowdImage crowd = go.GetComponent<FrontCrowdImage>();
        crowd.sprite = loadSprite(spritePath);
        crowd.color = Color.white;
        crowd.raycastTarget = false;
        crowd.Configure(rowIndex);

        return crowd;
    }

    /// <summary>1280×720 전면 화면 원본의 좌상단 좌표를 RectTransform에 적용합니다.</summary>
    private static void setFrontRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>같은 이름의 직계 자식 중 보존 대상을 제외하고 제거합니다.</summary>
    private static void removeDirectChildrenByName(Transform parent, string name, Transform except)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != except && child.name == name)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    /// <summary>허용된 부모 바깥에 남은 같은 이름의 이전 생성 오브젝트를 제거합니다.</summary>
    private static void removeNestedObjectsOutsideParent(Transform root, string name, Transform allowedParent)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = children.Length - 1; i >= 0; i--)
        {
            Transform child = children[i];
            if (child.name == name && child.parent != allowedParent)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    /// <summary>지정 부모의 이름이 일치하는 직계 자식 UI를 비활성화합니다.</summary>
    private static void setDirectChildrenInactive(Transform parent, params string[] names)
    {
        if (parent == null) return;
        foreach (string name in names)
        {
            Transform child = parent.Find(name);
            if (child != null) child.gameObject.SetActive(false);
        }
    }

    /// <summary>계산기 이미지 픽셀 좌표를 투명 버튼 영역으로 변환합니다.</summary>
    private static Button createArtworkButton(RectTransform parent, string name, float x, float y, float width, float height)
    {
        RectTransform rect = createRect(name, parent, Vector2.zero, Vector2.zero);
        setTopLeftRect(rect, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.001f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        return button;
    }

    /// <summary>1240×1248 계산기 원본의 좌상단 좌표를 현재 RectTransform 좌표로 변환합니다.</summary>
    private static void setTopLeftRect(RectTransform rect, float x, float y, float width, float height)
    {
        const float ScaleX = 360f / 1240f;
        const float ScaleY = 362f / 1248f;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x * ScaleX, -y * ScaleY);
        rect.sizeDelta = new Vector2(width * ScaleX, height * ScaleY);
    }

    /// <summary>GameUI 안에서 계산기 조작에 사용하는 버튼을 모두 반환합니다.</summary>
    private static Button[] findCalculatorButtons(Transform operating)
    {
        Transform calculator = findChild(operating, "PriceInput");
        return calculator == null ? Array.Empty<Button>() : calculator.GetComponentsInChildren<Button>(true);
    }

    /// <summary>개인 ProgressScene의 기존 임시 화면을 비활성화하고 GameUI Prefab과 EventSystem을 배치합니다.</summary>
    [MenuItem("Cashier/Install Sale Sorting UI In Local ProgressScene")]
    public static void InstallInProgressScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ProgressScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject existingGameUi = findRoot(scene, "GameUI");
            if (existingGameUi == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameUiPrefabPath);
                if (prefab == null) throw new InvalidOperationException($"Prefab을 찾을 수 없습니다: {GameUiPrefabPath}");
                existingGameUi = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                existingGameUi.name = "GameUI";
            }

            GameObject sandbox = findRoot(scene, "MainSceneUI");
            if (sandbox != null)
            {
                sandbox.SetActive(false);
            }

            if (findRoot(scene, "EventSystem") == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ProgressScenePath);
            Debug.Log("[SaleSortingPrefabSetup] Local ProgressScene에 GameUI와 EventSystem을 배치했습니다.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>이름이 일치하는 첫 자식을 재귀적으로 찾습니다.</summary>
    /// <param name="root">검색을 시작할 부모입니다.</param>
    /// <param name="name">찾을 GameObject 이름입니다.</param>
    /// <returns>일치하는 자식 또는 null입니다.</returns>
    private static Transform findChild(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = findChild(child, name);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>지정 Scene의 루트 GameObject를 이름으로 찾습니다.</summary>
    /// <param name="scene">검색할 Scene입니다.</param>
    /// <param name="name">찾을 루트 이름입니다.</param>
    /// <returns>일치하는 루트 또는 null입니다.</returns>
    private static GameObject findRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }

        return null;
    }

    /// <summary>중앙 기준 RectTransform GameObject를 생성합니다.</summary>
    /// <param name="name">생성할 이름입니다.</param>
    /// <param name="parent">부모 Transform입니다.</param>
    /// <param name="position">부모 중앙 기준 위치입니다.</param>
    /// <param name="size">픽셀 크기입니다.</param>
    /// <returns>생성된 RectTransform입니다.</returns>
    private static RectTransform createRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>RectTransform을 부모 전체에 맞춰 늘립니다.</summary>
    /// <param name="sortingtRoot">늘릴 RectTransform입니다.</param>
    private static void stretch(RectTransform sortingtRoot)
    {
        sortingtRoot.anchorMin = Vector2.zero;
        sortingtRoot.anchorMax = Vector2.one;
        sortingtRoot.offsetMin = Vector2.zero;
        sortingtRoot.offsetMax = Vector2.zero;
    }

    /// <summary>지정 경로의 Sprite를 필수 에셋으로 로드합니다.</summary>
    /// <param name="path">프로젝트 상대 에셋 경로입니다.</param>
    /// <param name="fallbackPath">선택적 대체 에셋 경로입니다.</param>
    /// <returns>로드한 Sprite입니다.</returns>
    private static Sprite loadSprite(string path, string fallbackPath = null)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null && !string.IsNullOrEmpty(fallbackPath))
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
        }
        if (sprite == null) throw new InvalidOperationException($"Sprite를 로드하지 못했습니다: {path} (fallback: {fallbackPath})");
        return sprite;
    }

    /// <summary>지정 경로의 Material을 로드합니다.</summary>
    /// <param name="path">프로젝트 상대 머티리얼 경로입니다.</param>
    /// <param name="fallbackPath">선택적 대체 머티리얼 경로입니다.</param>
    /// <returns>로드한 Material입니다.</returns>
    private static Material loadMaterial(string path, string fallbackPath = null)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null && !string.IsNullOrEmpty(fallbackPath))
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>(fallbackPath);
        }
        return mat;
    }

    /// <summary>RectTransform 전체를 채우는 TMP 안내 텍스트를 생성합니다.</summary>
    /// <param name="parent">텍스트 부모입니다.</param>
    /// <param name="text">초기 표시 문자열입니다.</param>
    /// <param name="fontSize">글자 크기입니다.</param>
    /// <param name="alignment">텍스트 정렬입니다.</param>
    /// <returns>생성된 TMP 컴포넌트입니다.</returns>
    private static TextMeshProUGUI addLabel(RectTransform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = createRect("Label", parent, Vector2.zero, Vector2.zero);
        stretch(sortingtRoot: rect);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>직렬화된 Object 참조 필드를 설정합니다.</summary>
    /// <param name="serializedObject">대상 직렬화 객체입니다.</param>
    /// <param name="propertyName">필드 이름입니다.</param>
    /// <param name="value">연결할 Unity Object입니다.</param>
    private static void setObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.objectReferenceValue = value;
    }

    /// <summary>직렬화된 실수 설정 필드를 갱신합니다.</summary>
    /// <param name="serializedObject">대상 직렬화 객체입니다.</param>
    /// <param name="propertyName">필드 이름입니다.</param>
    /// <param name="value">적용할 값입니다.</param>
    private static void setFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.floatValue = value;
    }

    /// <summary>손님 대화창(DialoguePanel)과 TextMeshProUGUI(마비노기 폰트)를 전면 화면에 구성하고 CustomerPresenter에 바인딩합니다.</summary>
    public static void setupDialoguePanel(Transform frontView, Transform operating)
    {
        Transform customer = frontView.Find("Customer");

        // 1. DialoguePanel 확보 또는 생성
        Transform existingPanel = frontView.Find("DialoguePanel");
        GameObject panelGo;
        RectTransform panelRect;

        if (existingPanel == null)
        {
            panelGo = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Canvas), typeof(Image));
            panelGo.transform.SetParent(frontView, false);
            panelRect = panelGo.GetComponent<RectTransform>();
        }
        else
        {
            panelGo = existingPanel.gameObject;
            panelRect = existingPanel.GetComponent<RectTransform>();
            if (panelGo.GetComponent<CanvasRenderer>() == null) panelGo.AddComponent<CanvasRenderer>();
            if (panelGo.GetComponent<Canvas>() == null) panelGo.AddComponent<Canvas>();
            if (panelGo.GetComponent<Image>() == null) panelGo.AddComponent<Image>();
        }

        // 손님 및 매대보다 앞쪽에 렌더링되도록 FrontContainer 뒤(맨 마지막 자식)로 설정
        panelRect.SetAsLastSibling();

        // 프로토타입 기준: (560, 70), (0, -34)
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -34f);
        panelRect.sizeDelta = new Vector2(560f, 70f);

        // Sorting Order 30 적용하여 확실히 앞에 오도록 보장
        Canvas canvas = panelGo.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30;

        // 9-슬라이스 DialogueFrame 프레임 이미지 설정
        Image frameImg = panelGo.GetComponent<Image>();
        Sprite frameSprite = loadDialogueFrameSprite();
        frameImg.sprite = frameSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.fillCenter = true;
        frameImg.pixelsPerUnitMultiplier = 4f;
        frameImg.color = Color.white;
        frameImg.raycastTarget = false;

        // 2. 자식 Dialogue TextMeshProUGUI 확보 및 마비노기 폰트 적용
        Transform textTrans = panelGo.transform.Find("Dialogue");
        GameObject textGo;
        RectTransform textRect;
        TextMeshProUGUI tmp;

        if (textTrans == null)
        {
            textGo = new GameObject("Dialogue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panelGo.transform, false);
            textRect = textGo.GetComponent<RectTransform>();
            tmp = textGo.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textGo = textTrans.gameObject;
            textRect = textTrans.GetComponent<RectTransform>();
            tmp = textTrans.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textGo.AddComponent<TextMeshProUGUI>();
        }

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(18f, 10f);
        textRect.offsetMax = new Vector2(-18f, -10f);

        TMP_FontAsset mabinogiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MabinogiFontPath);
        if (mabinogiFont != null)
        {
            tmp.font = mabinogiFont;
            tmp.fontSharedMaterial = mabinogiFont.material;
        }

        tmp.text = "...";
        tmp.fontSize = 24f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 16f;
        tmp.fontSizeMax = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        // 초기에는 대사가 없으므로 패널 비활성화
        panelGo.SetActive(false);

        // 기존 Customer/Dialogue가 있다면 비활성화
        if (customer != null)
        {
            Transform oldDialogue = customer.Find("Dialogue");
            if (oldDialogue != null)
            {
                oldDialogue.gameObject.SetActive(false);
            }
        }

        // 3. CustomerPresenter에 참조 연결
        CustomerPresenter presenter = operating.GetComponent<CustomerPresenter>();
        if (presenter != null)
        {
            SerializedObject presenterObj = new SerializedObject(presenter);
            setObject(presenterObj, "speechBubbleRoot", panelGo);
            setObject(presenterObj, "dialogueText", tmp);
            setObject(presenterObj, "dialogueFrameSprite", frameSprite);
            if (mabinogiFont != null)
            {
                setObject(presenterObj, "dialogueFont", mabinogiFont);
            }
            presenterObj.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>DialogueFrame 스프라이트를 안전하게 로드합니다.</summary>
    private static Sprite loadDialogueFrameSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DialogueFramePath);
        if (sprite != null) return sprite;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(DialogueFramePath);
        foreach (var a in assets)
        {
            if (a is Sprite s) return s;
        }
        throw new InvalidOperationException($"DialogueFrame Sprite를 로드할 수 없습니다: {DialogueFramePath}");
    }

    /// <summary>OperatingPanel과 GameUI Prefab에 대화창(DialoguePanel)과 마비노기 폰트를 구성합니다.</summary>
    [MenuItem("Cashier/Setup Dialogue Prefab")]
    public static void SetupDialoguePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(OperatingPrefabPath);
        try
        {
            Transform frontView = root.transform.Find("AstraFrontView");
            if (frontView == null)
            {
                throw new InvalidOperationException("AstraFrontView를 찾을 수 없습니다.");
            }

            setupDialoguePanel(frontView, root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, OperatingPrefabPath);
            Debug.Log("[SaleSortingPrefabSetup] OperatingPanel.prefab에 DialoguePanel 직렬화 완료!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (System.IO.File.Exists(GameUiPrefabPath))
        {
            GameObject gameUiRoot = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
            try
            {
                Transform operating = gameUiRoot.transform.Find("OperatingPanel") ?? gameUiRoot.transform.Find("ProgressCanvas/OperatingPanel");
                if (operating != null)
                {
                    Transform frontView = operating.Find("AstraFrontView");
                    if (frontView != null)
                    {
                        Transform dialoguePanel = frontView.Find("DialoguePanel");
                        CustomerPresenter presenter = operating.GetComponent<CustomerPresenter>();
                        if (presenter != null && dialoguePanel != null)
                        {
                            SerializedObject presenterObj = new SerializedObject(presenter);
                            setObject(presenterObj, "speechBubbleRoot", dialoguePanel.gameObject);
                            TextMeshProUGUI tmp = dialoguePanel.GetComponentInChildren<TextMeshProUGUI>(true);
                            if (tmp != null) setObject(presenterObj, "dialogueText", tmp);
                            TMP_FontAsset mabinogiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MabinogiFontPath);
                            if (mabinogiFont != null) setObject(presenterObj, "dialogueFont", mabinogiFont);
                            presenterObj.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(gameUiRoot, GameUiPrefabPath);
                Debug.Log("[SaleSortingPrefabSetup] GameUI.prefab에 CustomerPresenter 연결 확인 완료!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(gameUiRoot);
            }
        }
    }
}

/// <summary>에디터 리로드 시 프리팹 갱신을 자동으로 1회 실행하여 정면 배경 및 판매 분류 UI를 프리팹에 즉시 반영합니다.</summary>
[InitializeOnLoad]
public static class AutoSaleSortingPrefabUpdater
{
    private const string SessionKey = "SaleSortingUI_AutoSetup_Applied_v9";

    static AutoSaleSortingPrefabUpdater()
    {
        EditorApplication.delayCall += checkAndRun;
    }

    private static void checkAndRun()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        try
        {
            SaleSortingPrefabSetup.Setup();
            Debug.Log("[AutoSaleSortingPrefabUpdater] 정면 배경(군중, 연기, 경비병, 안개) 및 판매 분류 프리팹 갱신을 완료했습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AutoSaleSortingPrefabUpdater] 자동 프리팹 갱신 중 예외: {ex.Message}");
        }
    }
}
#endif
