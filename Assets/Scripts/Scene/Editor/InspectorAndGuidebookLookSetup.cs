using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 감독관이 판매대(테이블) 뒤에 서 있는 것처럼 보이게 하고, 영업 전 지침서 글씨를 굵게 바꾸는 도구.
/// 감독관 그림은 키우고, 손님처럼 판매대 UI보다 먼저 그려 아랫몸이 판매대 뒤에 가려지게 한다.
/// 여러 번 실행해도 같은 결과가 됩니다.
/// </summary>
public static class InspectorAndGuidebookLookSetup
{
    private const string InspectorPrefabPath = "Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab";
    private const string PreOpenPrefabPath = "Assets/Prefabs/GameUI/PreOpenPanel.prefab";
    private const string ClipName = "PortraitClip";
    // 판매대(OperatingPanel/AstraFrontView)는 ProgressCanvas(정렬 20)의 UI이고, 손님은 그보다 먼저 그려지는
    // 월드 스프라이트라 판매대 뒤에 선다. 감독관도 20보다 낮은 캔버스에 그려 같은 자리(바리케이드 앞, 판매대 뒤)에 둔다.
    private const int PortraitSortingOrder = 19;
    // 원래 354x708, 위에서 173 아래. 2.65배로 키우고 위로 올려 머리부터 허리까지 보이게 한다.
    private static readonly Vector2 PortraitSize = new Vector2(469f, 938f);
    private const float PortraitTop = -57f;
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private const string HintName = "Hint";
    // 대사창은 영업 중 안내 말풍선(ShopStatusHudSetup.installTutorialCoach)과 같은 위치·폭·글씨 크기.
    private const float DialogueWidth = 860f;
    private const float DialogueBottom = 18f;
    private const float DialogueMinHeight = 150f;
    // 위아래 같은 여백. 세 줄이면 약 170 높이가 된다.
    private const float DialoguePaddingY = 44f;
    private const float DialoguePaddingX = 56f;

    [MenuItem("Cashier/Setup/Inspector Behind Counter And Bold Guidebook")]
    public static void Install()
    {
        installInspector();
        installGuidebook();
        AssetDatabase.SaveAssets();
        Debug.Log("[InspectorAndGuidebookLookSetup] 설치 완료");
    }

    /// <summary>
    /// 소지금 판자 글씨와 설비 창 가격·안내 글씨를 굵은 갈무리로 바꾼다. 소지금은 다음날 버튼처럼 두꺼운 테두리를 준다.
    /// 판자를 새로 만들지 않고 글씨만 바꾸므로 다른 설정은 그대로다.
    /// </summary>
    [MenuItem("Cashier/Setup/Bold Balance Plank And Facility Prices")]
    public static void InstallBoldPrices()
    {
        TMP_FontAsset bold = GalmuriFontSetup.EnsureBold();
        var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontSetup.RegularAssetPath);
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/GameUI/GameUI.prefab");
        try
        {
            // 정산 화면 소지금 판자의 금액·증감 글씨: 비트비트체 + 판자 테두리.
            Material plank = GalmuriFontSetup.OutlineMaterial("Plank Outline", new Color32(9, 2, 1, 255), 0.42f, true);
            int plankTexts = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.parent == null || transform.parent.name != "SettlementBalance") continue;
                if (!transform.TryGetComponent(out TextMeshProUGUI text)) continue;
                text.font = bold;
                text.fontSharedMaterial = plank;
                plankTexts++;
            }
            if (plankTexts == 0) throw new System.InvalidOperationException("SettlementBalance 글씨를 찾을 수 없습니다.");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/GameUI/GameUI.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab");
        try
        {
            int count = 0;
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font != regular) continue;
                text.font = bold;
                text.fontSharedMaterial = bold.material;
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab");
            Debug.Log($"[InspectorAndGuidebookLookSetup] 소지금·설비 창 글씨 {count + 1}개 굵게");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void installInspector()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(InspectorPrefabPath);
        try
        {
            var presenter = root.GetComponent<InspectorPresenter>();
            var portrait = (Image)new SerializedObject(presenter).FindProperty("portrait").objectReferenceValue
                ?? throw new System.InvalidOperationException("감독관 Portrait를 찾을 수 없습니다.");
            var portraitRect = portrait.rectTransform;
            Transform clip = portraitRect.parent.name == ClipName ? portraitRect.parent : null;
            if (clip == null)
            {
                var clipObject = new GameObject(ClipName, typeof(RectTransform), typeof(RectMask2D));
                clip = clipObject.transform;
                clip.SetParent(portraitRect.parent, false);
                clip.SetSiblingIndex(portraitRect.GetSiblingIndex());
                portraitRect.SetParent(clip, false);
            }

            // 패널 밖으로 나간 부분만 자른다. 판매대 아래는 판매대 UI가 가린다.
            // 프리팹만 열면 이 캔버스가 루트라 overrideSorting 대입이 무시되므로 직렬화 값으로 넣는다.
            var clipCanvas = new SerializedObject(getOrAdd<Canvas>(clip.gameObject));
            clipCanvas.FindProperty("m_OverrideSorting").boolValue = true;
            clipCanvas.FindProperty("m_SortingOrder").intValue = PortraitSortingOrder;
            clipCanvas.ApplyModifiedPropertiesWithoutUndo();
            var clipRect = (RectTransform)clip;
            clipRect.anchorMin = Vector2.zero;
            clipRect.anchorMax = Vector2.one;
            clipRect.offsetMin = clipRect.offsetMax = Vector2.zero;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(.5f, 1f);
            portraitRect.pivot = new Vector2(.5f, 1f);
            portraitRect.anchoredPosition = new Vector2(0f, PortraitTop);
            portraitRect.sizeDelta = PortraitSize;
            installDialogueBox(presenter);
            PrefabUtility.SaveAsPrefabAsset(root, InspectorPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 대사창을 안내 말풍선과 같은 크기로 만든다. 높이는 줄 수에 맞춰 늘어나고(최소 150),
    /// 글은 가운데 정렬로 위아래 여백이 같다. 오른쪽 아래에 "클릭해서 계속" 안내를 둔다.
    /// </summary>
    private static void installDialogueBox(InspectorPresenter presenter)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath)
            ?? throw new System.InvalidOperationException($"{FontPath}를 찾을 수 없습니다.");
        var serialized = new SerializedObject(presenter);
        var text = (TextMeshProUGUI)serialized.FindProperty("dialogue").objectReferenceValue
            ?? throw new System.InvalidOperationException("감독관 대사 Text를 찾을 수 없습니다.");
        var nextButton = (Button)serialized.FindProperty("nextButton").objectReferenceValue
            ?? throw new System.InvalidOperationException("감독관 Next 버튼을 찾을 수 없습니다.");
        var box = (RectTransform)text.transform.parent;

        box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, 0f);
        box.anchoredPosition = new Vector2(0f, DialogueBottom);
        box.sizeDelta = new Vector2(DialogueWidth, DialogueMinHeight);
        // 같은 대사틀을 9분할로 써서, 150 높이에서는 안내 말풍선과 같은 테두리 두께가 되게 한다.
        var frame = box.GetComponent<Image>();
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = frame.sprite.rect.height / DialogueMinHeight;

        var layout = getOrAdd<VerticalLayoutGroup>(box.gameObject);
        layout.padding = new RectOffset((int)DialoguePaddingX, (int)DialoguePaddingX, (int)DialoguePaddingY, (int)DialoguePaddingY);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        getOrAdd<LayoutElement>(box.gameObject).minHeight = DialogueMinHeight;
        var fitter = getOrAdd<ContentSizeFitter>(box.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontSize = 22f;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Midline;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.margin = Vector4.zero;
        getOrAdd<LayoutElement>(nextButton.gameObject).ignoreLayout = true;

        Transform oldHint = box.Find(HintName);
        if (oldHint != null) Object.DestroyImmediate(oldHint.gameObject);
        var hintRoot = new GameObject(HintName, typeof(RectTransform), typeof(LayoutElement));
        hintRoot.layer = box.gameObject.layer;
        hintRoot.GetComponent<LayoutElement>().ignoreLayout = true;
        var hintRect = (RectTransform)hintRoot.transform;
        hintRect.SetParent(box, false);
        hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-40f, 30f);
        hintRect.sizeDelta = new Vector2(220f, 20f);
        // TMP는 uGUI Shadow를 받지 않으므로 1px 어두운 글씨를 뒤에 깔아 그림자로 쓴다.
        createHintText("Shadow", hintRect, font, new Color32(20, 16, 12, 255), new Vector2(1f, -1f));
        createHintText("Text", hintRect, font, new Color32(220, 216, 208, 255), Vector2.zero);
    }

    private static void createHintText(string name, RectTransform parent, TMP_FontAsset font, Color32 color, Vector2 offset)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        var rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = offset;
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.color = color;
        text.text = "클릭해서 계속 <size=11>▼</size>";
    }

    private static T getOrAdd<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }

    /// <summary>지침서 안 글씨(품목명·가격·지침 등)를 굵은 갈무리11로 바꾼다.</summary>
    private static void installGuidebook()
    {
        TMP_FontAsset bold = GalmuriFontSetup.EnsureBold();
        var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontSetup.RegularAssetPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PreOpenPrefabPath);
        try
        {
            int count = 0;
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                // 지침서는 다른 화면과 같은 물마루를 쓴다(정산 화면만 비트비트체).
                if (text.font != regular && text.font != bold) continue;
                text.font = regular;
                text.fontSharedMaterial = regular.material;
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PreOpenPrefabPath);
            Debug.Log($"[InspectorAndGuidebookLookSetup] 지침서 글씨 {count}개 굵게");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
