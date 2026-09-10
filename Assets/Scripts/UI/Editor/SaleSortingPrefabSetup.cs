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
    private const string FarBackgroundPath = "Assets/DystopiaPrototype/Art/FARBACKGROUND.png";
    private const string FogBackPath = "Assets/DystopiaPrototype/Art/FogBack.png";
    private const string FogMidPath = "Assets/DystopiaPrototype/Art/FogMid.png";
    private const string FogFrontPath = "Assets/DystopiaPrototype/Art/FogFront.png";
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
    private const int SaleAnchorCount = 9;
    private const int SaleAnchorRowSize = 3;

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
            RectTransform[] saleAnchors = createSaleAnchors(sale);

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

            RectTransform vacuumRect = createRect("Vacuum", workArea, new Vector2(-330f, 0f), new Vector2(48f, 480f));
            vacuumRect.SetAsLastSibling();
            Image vacuumImage = vacuumRect.gameObject.AddComponent<Image>();
            vacuumImage.color = new Color(0.72f, 0.82f, 0.88f, 0.95f);
            vacuumImage.raycastTarget = false;
            VacuumController vacuumController = vacuumRect.gameObject.AddComponent<VacuumController>();
            RectTransform suctionArea = createRect("SuctionArea", vacuumRect, new Vector2(0f, -250f), new Vector2(96f, 64f));
            SerializedObject vacuumObject = new SerializedObject(vacuumController);
            setObject(vacuumObject, "vacuumRect", vacuumRect);
            setObject(vacuumObject, "vacuumImage", vacuumImage);
            setObject(vacuumObject, "suctionArea", suctionArea);
            setFloat(vacuumObject, "startOffsetX", -330f);
            setFloat(vacuumObject, "liftOffsetPixels", 18f);
            setFloat(vacuumObject, "attachedSpacingPixels", 10f);
            setFloat(vacuumObject, "positionFollowSpeed", 32f);
            vacuumObject.ApplyModifiedPropertiesWithoutUndo();

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
            setObjectArray(panelObject, "saleAnchors", saleAnchors);
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
            setObject(panelObject, "vacuum", vacuumController);
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

    /// <summary>Astra 전면 화면의 배경, 손님 위치, 매대와 클릭 가능한 박스를 구성합니다.</summary>
    /// <param name="operating">OperatingPanel Prefab 루트입니다.</param>
    private static void setupFrontView(Transform operating)
    {
        stretch((RectTransform)operating);
        Transform customer = findChild(operating, "Customer");
        Transform existing = operating.Find("AstraFrontView");
        if (existing != null)
        {
            if (customer != null && customer.IsChildOf(existing))
            {
                customer.SetParent(operating, false);
            }

            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        RectTransform frontView = createRect("AstraFrontView", operating, Vector2.zero, Vector2.zero);
        stretch(frontView);
        frontView.SetAsFirstSibling();

        createFrontImage(frontView, "FarBackground", FarBackgroundPath, 0f, 0f, 1280f, 720f);
        createFrontImage(frontView, "FogBack", FogBackPath, 0f, -390f, 1280f, 720f);
        createFrontImage(frontView, "FogMid", FogMidPath, 0f, -390f, 1280f, 720f);
        createFrontImage(frontView, "FogFront", FogFrontPath, 0f, -390f, 1280f, 720f);
        createFrontImage(frontView, "MidBackground", MidBackgroundPath, 0f, -46f, 1280f, 576f);
        createFrontImage(frontView, "CrowdBack", CrowdBackPath, -4f, -119f, 1288f, 979f);
        createFrontImage(frontView, "CrowdMiddle", CrowdMiddlePath, -4f, -119f, 1288f, 979f);
        createFrontImage(frontView, "CrowdFront", CrowdFrontPath, -4f, -119f, 1288f, 979f);
        createFrontImage(frontView, "LeftWatchTower", LeftWatchTowerPath, 0f, 0f, 1280f, 720f);
        createFrontImage(frontView, "RightWatchTower", RightWatchTowerPath, 0f, 0f, 1280f, 720f);
        createFrontImage(frontView, "Barricade", BarricadePath, -14f, 305f, 1308f, 270f);

        if (customer != null)
        {
            customer.SetParent(frontView, false);
            setFrontRect((RectTransform)customer, 305f, 82f, 550f, 550f);
            customer.gameObject.SetActive(true);
            setDirectChildrenInactive(customer, "Dialogue", "Basket");
        }

        createFrontImage(frontView, "Canopy", CanopyPath, 0f, 0f, 1280f, 720f);
        createFrontImage(frontView, "Counter", CounterPath, 0f, 0f, 1280f, 720f);

        RectTransform clockRect = createFrontImage(frontView, "CounterClock", CounterClockPath, 1080f, 425f, 180f, 90f);
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

        RectTransform container = createFrontImage(
            frontView,
            "FrontContainer",
            FrontContainerPath,
            460f,
            405f,
            360f,
            240f);
        Image containerImage = container.GetComponent<Image>();
        containerImage.preserveAspect = true;
        containerImage.raycastTarget = true;
        Button button = container.gameObject.AddComponent<Button>();
        button.targetGraphic = containerImage;
        button.transition = Selectable.Transition.None;
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
        float height)
    {
        RectTransform rect = createRect(name, parent, Vector2.zero, Vector2.zero);
        setFrontRect(rect, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = loadSprite(spritePath);
        image.color = Color.white;
        image.raycastTarget = false;
        return rect;
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

    /// <summary>판매 구역 안에 ProductId 행 순서를 보존하는 3×3 앵커를 생성합니다.</summary>
    /// <param name="saleZone">판매 구역 RectTransform입니다.</param>
    /// <returns>위쪽에서 아래쪽, 왼쪽에서 오른쪽 순서의 9개 앵커입니다.</returns>
    private static RectTransform[] createSaleAnchors(RectTransform saleZone)
    {
        var anchors = new RectTransform[SaleAnchorCount];
        float[] xPositions = { -78f, 0f, 78f };
        float[] yPositions = { 60f, 0f, -60f };
        for (int row = 0; row < SaleAnchorRowSize; row++)
        {
            for (int column = 0; column < SaleAnchorRowSize; column++)
            {
                int index = row * SaleAnchorRowSize + column;
                anchors[index] = createRect(
                    $"SaleAnchor_Row{row + 1}_Slot{column + 1}",
                    saleZone,
                    new Vector2(xPositions[column], yPositions[row]),
                    new Vector2(72f, 72f));
            }
        }

        return anchors;
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
    /// <returns>로드한 Sprite입니다.</returns>
    private static Sprite loadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException($"Sprite를 로드하지 못했습니다: {path}");
        return sprite;
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

    /// <summary>직렬화된 Object 배열 참조를 순서대로 설정합니다.</summary>
    /// <param name="serializedObject">대상 직렬화 객체입니다.</param>
    /// <param name="propertyName">배열 필드 이름입니다.</param>
    /// <param name="values">연결할 Object 배열입니다.</param>
    private static void setObjectArray(SerializedObject serializedObject, string propertyName, UnityEngine.Object[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
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
}

/// <summary>에디터 리로드 시 프리팹 갱신을 자동으로 1회 실행하여 디스크의 프리팹 파일에 시계, 밀대, 대형 상자를 즉시 반영합니다.</summary>
[InitializeOnLoad]
public static class AutoSaleSortingPrefabUpdater
{
    private const string SessionKey = "SaleSortingUI_AutoSetup_Applied_v3";

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
            Debug.Log("[AutoSaleSortingPrefabUpdater] 시계, 밀대, 대형 상자가 포함된 SaleSortingUI 프리팹 갱신을 성공적으로 완료했습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AutoSaleSortingPrefabUpdater] 자동 프리팹 갱신 중 예외: {ex.Message}");
        }
    }
}
#endif
