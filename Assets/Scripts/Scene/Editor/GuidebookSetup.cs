using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 지침서 안쪽을 구획(제목·오늘의 지침·오늘의 가격표)으로 나누고, 작업대 오른쪽에 "지침" 버튼과
/// 미끄러져 나오는 지침 쪽지를 만드는 에디터 설치 도구입니다. 여러 번 실행해도 같은 결과가 됩니다.
/// </summary>
public static class GuidebookSetup
{
    private const string PreOpenPrefabPath = "Assets/Prefabs/GameUI/PreOpenPanel.prefab";
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string FontPath = "Assets/Fonts/Galmuri/Galmuri11 SDF.asset";
    private const string DividerPath = "Assets/Textures/UI/Dystopia/Hud/InkDivider.png";
    private const string PlatePath = "Assets/Textures/UI/Dystopia/Hud/HudPlate.png";
    private const string PriceTitleName = "PriceTitle";
    private const string PopupName = "GuidelinePopup";
    // 영업 중 "지침" 버튼으로 여는 전용 클립보드 쪽지(100x124 픽셀아트, 3배로 표시).
    private const string NotePath = "Assets/Textures/UI/Dystopia/Guideline/GuidelineNote.png";
    private const float NoteScale = 3f;
    // 구획 제목 색(빛바랜 붉은 잉크)과 가격표를 내릴 거리.
    private static readonly Color SectionColor = new Color(.52f, .16f, .12f);
    private const float ProductShiftPixels = 26f;

    /// <summary>지침서 구획과 작업대 지침 버튼을 모두 설치합니다.</summary>
    [MenuItem("Cashier/Setup/Guidebook Sections And Guideline Popup")]
    public static void Install()
    {
        installSections();
        installPopup();
        AssetDatabase.SaveAssets();
        Debug.Log("[GuidebookSetup] 설치 완료");
    }

    /// <summary>작업대 "지침" 버튼과 지침 쪽지만 다시 설치합니다. 지침서 구획은 건드리지 않습니다.</summary>
    [MenuItem("Cashier/Setup/Guideline Popup Only")]
    public static void InstallPopupOnly()
    {
        installPopup();
        AssetDatabase.SaveAssets();
        Debug.Log("[GuidebookSetup] 지침 쪽지 설치 완료");
    }

    /// <summary>픽셀아트가 번지지 않도록 쪽지 그림의 가져오기 설정을 맞춥니다.</summary>
    private static Sprite loadPixelSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path)
            ?? throw new System.InvalidOperationException($"{path}를 찾을 수 없습니다.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>지침서 안에 구분선 두 줄과 "오늘의 가격표" 제목을 넣고, 영업 후 확인 불가 문구를 숨깁니다.</summary>
    private static void installSections()
    {
        var divider = AssetDatabase.LoadAssetAtPath<Sprite>(DividerPath)
            ?? throw new System.InvalidOperationException("구분선 이미지가 없습니다.");
        GameObject root = PrefabUtility.LoadPrefabContents(PreOpenPrefabPath);
        try
        {
            var heading = (RectTransform)root.transform.Find("MemoryHeading");
            var ruleTitle = (RectTransform)root.transform.Find("RuleTitle");
            var slot1 = (RectTransform)root.transform.Find("GuidelineSlot1");
            var products = (RectTransform)root.transform.Find("Products");
            var notice = root.transform.Find("GuidelineNotice");
            if (heading == null || ruleTitle == null || slot1 == null || products == null)
                throw new System.InvalidOperationException("지침서 구성 요소를 찾을 수 없습니다.");

            // 다시 실행해도 겹치지 않도록 전에 만든 것을 지우고, 가격표 위치는 원래 값 기준으로 다시 내린다.
            foreach (string name in new[] { "DividerTop", "DividerPrices", PriceTitleName })
            {
                Transform old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            if (notice != null) notice.gameObject.SetActive(false);
            products.anchoredPosition = new Vector2(products.anchoredPosition.x, ProductBaseY - ProductShiftPixels);

            ruleTitle.GetComponent<TextMeshProUGUI>().color = SectionColor;
            float headingBottom = bottomOf(heading);
            float ruleTop = topOf(ruleTitle);
            createDivider(root.transform, "DividerTop", ruleTitle, (headingBottom + ruleTop) * .5f, divider);

            float guidelineBottom = bottomOf(slot1);
            float productsTop = topOf(products);
            float pricesDividerY = guidelineBottom - 6f;
            createDivider(root.transform, "DividerPrices", ruleTitle, pricesDividerY, divider);

            var priceTitle = Object.Instantiate(ruleTitle.gameObject, root.transform);
            priceTitle.name = PriceTitleName;
            var priceRect = (RectTransform)priceTitle.transform;
            priceRect.SetSiblingIndex(ruleTitle.GetSiblingIndex() + 1);
            setTop(priceRect, (pricesDividerY + productsTop) * .5f + priceRect.rect.height * .5f);
            var priceText = priceTitle.GetComponent<TextMeshProUGUI>();
            priceText.text = "오늘의 가격표";
            priceText.color = SectionColor;

            PrefabUtility.SaveAsPrefabAsset(root, PreOpenPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 원래 프리팹의 가격표 세로 위치(중심 기준). 반복 실행해도 같은 자리에 오도록 고정값을 쓴다.
    private const float ProductBaseY = -74.827f;

    /// <summary>작업대 오른쪽 아래에 "지침" 버튼과, 그날 지침만 담은 클립보드 쪽지를 만듭니다.</summary>
    private static void installPopup()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var plate = AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath);
        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            var preOpen = root.GetComponentInChildren<PreOpenPanelPresenter>(true)
                ?? throw new System.InvalidOperationException("PreOpenPanelPresenter를 찾을 수 없습니다.");
            Transform workbench = null;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == "Workbench" && transform.parent != null && transform.parent.name == "SaleSortingUI") { workbench = transform; break; }
            if (workbench == null) throw new System.InvalidOperationException("작업대(Workbench)를 찾을 수 없습니다.");
            Transform sortingUi = workbench.parent;
            Transform old = sortingUi.Find(PopupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var popup = new GameObject(PopupName, typeof(RectTransform), typeof(GuidelinePopupPresenter));
            var popupRect = (RectTransform)popup.transform;
            popupRect.SetParent(sortingUi, false);
            popupRect.SetAsLastSibling();
            popupRect.anchorMin = Vector2.zero;
            popupRect.anchorMax = Vector2.one;
            popupRect.offsetMin = popupRect.offsetMax = Vector2.zero;

            // 지침 쪽지: 지침서와 별개인 클립보드 픽셀아트에 그날 지침만 적는다. 닫히면 화면 오른쪽 밖으로 들어간다.
            Sprite noteSprite = loadPixelSprite(NotePath);
            var sheetObject = new GameObject("Sheet", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var sheet = (RectTransform)sheetObject.transform;
            sheet.SetParent(popupRect, false);
            sheet.anchorMin = sheet.anchorMax = new Vector2(1f, 0f);
            sheet.pivot = new Vector2(1f, 0f);
            sheet.sizeDelta = noteSprite.rect.size * NoteScale;
            var sheetImage = sheetObject.GetComponent<Image>();
            sheetImage.sprite = noteSprite;
            sheetImage.preserveAspect = false;
            sheetImage.raycastTarget = true;

            // 종이 윗부분(빨간 밑줄 위)에 제목, 그 아래에 지침 본문. 위치는 그림 픽셀 기준 비율이다.
            var titleText = createText("Title", sheet, font, 24f, new Vector2(.12f, .735f), new Vector2(.88f, .88f));
            titleText.color = new Color(.45f, .13f, .1f);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.text = "오늘의 지침";

            var bodyText = createText("Body", sheet, font, 20f, new Vector2(.14f, .1f), new Vector2(.86f, .69f));
            bodyText.color = new Color(.18f, .14f, .11f);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.lineSpacing = 12f;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.text = "지침 없음.";

            var button = new GameObject("Toggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)button.transform;
            buttonRect.SetParent(popupRect, false);
            // 작업대 오른쪽 아래 구석.
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-8f, 10f);
            buttonRect.sizeDelta = new Vector2(64f, 46f);
            var buttonImage = button.GetComponent<Image>();
            buttonImage.sprite = plate;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.pixelsPerUnitMultiplier = .5f;
            button.GetComponent<Button>().targetGraphic = buttonImage;
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var labelText = label.GetComponent<TextMeshProUGUI>();
            labelText.font = font;
            labelText.fontSize = 19f;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            labelText.text = "지침";

            var serialized = new SerializedObject(popup.GetComponent<GuidelinePopupPresenter>());
            serialized.FindProperty("toggleButton").objectReferenceValue = button.GetComponent<Button>();
            serialized.FindProperty("sheet").objectReferenceValue = sheet;
            serialized.FindProperty("bodyText").objectReferenceValue = bodyText;
            serialized.FindProperty("preOpenPanel").objectReferenceValue = preOpen;
            // 버튼 바로 왼쪽, 아래 끝을 맞춰 나오고, 닫히면 화면 오른쪽 밖으로 들어간다.
            serialized.FindProperty("openPosition").vector2Value = new Vector2(-80f, 10f);
            serialized.FindProperty("closedPosition").vector2Value = new Vector2(sheet.sizeDelta.x + 40f, 10f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>부모 안의 비율 영역에 꽉 차는 TMP 글자를 만듭니다.</summary>
    private static TextMeshProUGUI createText(string name, RectTransform parent, TMP_FontAsset font, float size,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>기준 텍스트와 같은 폭·가로 위치로, 주어진 세로 위치에 잉크 구분선을 만듭니다.</summary>
    private static void createDivider(Transform parent, string name, RectTransform widthSource, float centerY, Sprite sprite)
    {
        var line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)line.transform;
        rect.SetParent(parent, false);
        rect.SetSiblingIndex(widthSource.GetSiblingIndex());
        rect.anchorMin = widthSource.anchorMin;
        rect.anchorMax = widthSource.anchorMax;
        rect.pivot = new Vector2(widthSource.pivot.x, .5f);
        rect.sizeDelta = new Vector2(widthSource.sizeDelta.x * .86f, 4f);
        // 기준 텍스트의 가운데에 맞춘 뒤, 피벗 기준 세로 위치만 바꾼다.
        float leftInset = widthSource.sizeDelta.x * .07f;
        rect.anchoredPosition = new Vector2(widthSource.anchoredPosition.x + leftInset * (1f - 2f * widthSource.pivot.x), 0f);
        setCenterY(rect, centerY);
        var image = line.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
    }

    // 아래 함수들은 같은 부모 안에서 RectTransform의 위·아래 가장자리를 부모 로컬 y로 다룬다.
    private static float topOf(RectTransform rect) => rect.localPosition.y + rect.rect.yMax;

    private static float bottomOf(RectTransform rect) => rect.localPosition.y + rect.rect.yMin;

    private static void setTop(RectTransform rect, float top) =>
        rect.localPosition = new Vector3(rect.localPosition.x, top - rect.rect.yMax, rect.localPosition.z);

    private static void setCenterY(RectTransform rect, float centerY) =>
        rect.localPosition = new Vector3(rect.localPosition.x, centerY - rect.rect.center.y, rect.localPosition.z);
}
