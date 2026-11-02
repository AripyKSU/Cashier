using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 저녁 정산 화면을 도트 느낌으로 맞추는 에디터 도구입니다.
/// 비트비트체를 작은 해상도·Point 필터로 다시 만들어 글자가 픽셀로 보이게 하고, 가계부 양쪽 페이지에 공책 줄을 긋고,
/// 소지금 판자 글씨를 다음날 버튼 그림 글씨처럼 각진 굵은 글꼴·위에서 아래로 짙어지는 크림색·검은 테두리·아래 그림자로 바꿉니다.
/// 여러 번 실행해도 같은 결과입니다.
/// </summary>
public static class SettlementPixelLookSetup
{
    private const string SettlementPanelPrefabPath = "Assets/Prefabs/GameUI/SettlementPanel.prefab";
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string RuledLinePath = "Assets/Textures/UI/Dystopia/Settlement/LedgerRuledLine.png";
    // 줄무늬 그림 1px이 UI 몇 단위인지. 글자 1텍셀(글씨 크기 20 / 샘플 16)과 같아 줄과 글자의 도트 크기가 맞는다.
    // 줄무늬 그림 높이 24px × 1.25 = 줄 간격 30.
    private const float RuledPixelUnits = 1.25f;
    // 가계부 본문 글씨 크기. 줄 간격(DailySettlementLedgerFormatter.LineHeight) 안에 들어가는 크기입니다.
    private const float LedgerFontSize = 20f;
    // 글자 아래 끝(기준선)에서 줄 윗면까지의 거리(UI 단위). 글자 도트 2칸만큼 줄 위에 살짝 떠 앉는다.
    private const float RuleBelowBaseline = 2.5f;
    // 소지금 글씨. 다음날 글씨(NextDay.png에 그려진 글자)처럼 각진 갈무리 굵은체를 쓴다.
    // 크기 24는 갈무리 12px 격자의 2배라 글자 도트가 UI 2단위로 고르게 떨어진다.
    private const string PlankFontPath = "Assets/Fonts/Galmuri/Galmuri11-Bold SDF.asset";
    private const string PlankMaterialPath = "Assets/Fonts/Galmuri/Galmuri11-Bold SDF Plank Outline.mat";
    private const float PlankFontSize = 24f;
    // 다음날 글씨에서 뽑은 색: 위쪽 밝은 크림 → 아래쪽 짙은 황갈색, 검은 테두리, 아래로 떨어지는 짙은 갈색 그림자.
    private static readonly Color PlankTopColor = new Color32(251, 230, 190, 255);
    private static readonly Color PlankBottomColor = new Color32(221, 175, 128, 255);
    private static readonly Color PlankOutlineColor = new Color32(6, 4, 3, 255);
    private static readonly Color PlankShadowColor = new Color32(30, 13, 8, 255);
    // 테두리 두께와 그림자 위치(SDF 패딩 비율). 그림자는 테두리보다 조금 굵게, 아래로 크게·오른쪽으로 조금 밀린다.
    private const float PlankOutlineWidth = 0.3f;
    private static readonly Vector2 PlankShadowOffset = new Vector2(0.6f, -1f);
    private const float PlankShadowDilate = 0.5f;

    [MenuItem("Cashier/Setup/Settlement Pixel Look")]
    public static void Install()
    {
        pixelateFont();
        installRuledLines();
        restylePlankText();
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementPixelLookSetup] 정산 화면 도트 폰트·공책 줄·소지금 글씨 설치 완료");
    }

    /// <summary>
    /// 비트비트체 에셋을 같은 파일(GUID·머티리얼 그대로)에서 작은 샘플 크기로 다시 만들고 아틀라스를 Point 필터로 둔다.
    /// SDF 값이 텍셀마다 한 덩어리로 읽혀 글자 가장자리가 텍셀 크기 계단(도트)으로 보인다.
    /// </summary>
    private static void pixelateFont()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontSetup.BoldAssetPath)
            ?? throw new System.InvalidOperationException("비트비트체 에셋이 없습니다.");
        var font = AssetDatabase.LoadAssetAtPath<Font>(GalmuriFontSetup.BoldFontPath);
        if (FontEngine.LoadFontFace(font, GalmuriFontSetup.PixelSamplingSize) != FontEngineError.Success)
            throw new System.InvalidOperationException("비트비트체 폰트를 읽을 수 없습니다.");

        asset.faceInfo = FontEngine.GetFaceInfo();
        asset.isMultiAtlasTexturesEnabled = false;
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_AtlasPadding").intValue = GalmuriFontSetup.PixelPadding;
        serialized.FindProperty("m_AtlasWidth").intValue = GalmuriFontSetup.PixelAtlasSize;
        serialized.FindProperty("m_AtlasHeight").intValue = GalmuriFontSetup.PixelAtlasSize;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // 패딩은 머티리얼의 GradientScale에서 다시 읽히므로 먼저 맞춘다.
        asset.material.SetFloat(ShaderUtilities.ID_GradientScale, GalmuriFontSetup.PixelPadding + 1);
        asset.ClearFontAssetData(true);
        GalmuriFontSetup.EnsureBold();
        GalmuriFontSetup.ApplyPixelLook(asset);
        GalmuriFontSetup.Create();
    }

    /// <summary>가계부 양쪽 페이지 글씨 뒤에 공책 줄을 깔고, 줄이 글자 기준선 바로 아래에 오도록 맞춘다.</summary>
    private static void installRuledLines()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(RuledLinePath)
            ?? throw new System.InvalidOperationException("공책 줄 이미지가 없습니다.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();
        var ruled = AssetDatabase.LoadAssetAtPath<Sprite>(RuledLinePath);

        GameObject root = PrefabUtility.LoadPrefabContents(SettlementPanelPrefabPath);
        try
        {
            foreach (string name in new[] { "LeftPageText", "RightPageText" })
            {
                var text = findDeep(root.transform, name)?.GetComponent<TextMeshProUGUI>()
                    ?? throw new System.InvalidOperationException($"{name}을 찾을 수 없습니다.");
                text.enableAutoSizing = false;
                text.fontSize = LedgerFontSize;
                text.lineSpacing = 0f;
                installLines(text, ruled);
            }

            PrefabUtility.SaveAsPrefabAsset(root, SettlementPanelPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 페이지 글씨와 같은 자리·크기·기울기의 빈 상자를 글씨 바로 앞 형제로 만들고, 그 안에 줄무늬 반복 이미지를 깐다.
    /// 상자의 좌표가 글씨와 같으므로 글씨에서 잰 기준선 높이를 그대로 쓸 수 있다.
    /// </summary>
    /// <param name="text">가계부 한쪽 페이지 글씨입니다.</param>
    /// <param name="ruled">줄 하나가 그려진 반복 이미지입니다.</param>
    private static void installLines(TextMeshProUGUI text, Sprite ruled)
    {
        var textRect = text.rectTransform;
        string linesName = text.name.Replace("Text", "Lines");
        Transform old = textRect.parent.Find(linesName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var boxRect = (RectTransform)new GameObject(linesName, typeof(RectTransform)).transform;
        boxRect.SetParent(textRect.parent, false);
        boxRect.SetSiblingIndex(textRect.GetSiblingIndex());
        boxRect.anchorMin = textRect.anchorMin;
        boxRect.anchorMax = textRect.anchorMax;
        boxRect.pivot = textRect.pivot;
        boxRect.anchoredPosition = textRect.anchoredPosition;
        boxRect.sizeDelta = textRect.sizeDelta;
        boxRect.localRotation = textRect.localRotation;
        boxRect.localScale = textRect.localScale;

        // 둘째 줄(본문 첫 줄) 기준선. 이후 줄은 <line-height>로 고정한 간격만큼 한 줄씩 내려간다.
        float baseline = measureBodyBaseline(text);

        // 반복 이미지는 아래쪽부터 타일을 깐다. 맨 아래 타일의 바닥 줄이 어느 기준선 바로 아래에 오도록 아래 끝을 맞춘다.
        Rect rect = textRect.rect;
        float spacing = DailySettlementLedgerFormatter.LineHeight;
        float firstRuleBottom = baseline - RuleBelowBaseline - RuledPixelUnits;
        float bottom = firstRuleBottom - Mathf.Floor((firstRuleBottom - rect.yMin) / spacing) * spacing;
        // 높이를 줄 간격의 정수배로 두어 타일이 위·아래 어느 쪽부터 깔려도 줄 위치가 같게 한다.
        float height = Mathf.Floor((rect.yMax - bottom) / spacing) * spacing;

        Debug.Log($"[SettlementPixelLookSetup] {text.name}: 기준선 {baseline:F1}, 페이지 {rect.yMin:F1}~{rect.yMax:F1}, 줄 시작 {bottom:F1}");
        var lineObject = new GameObject("Rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var lineRect = (RectTransform)lineObject.transform;
        lineRect.SetParent(boxRect, false);
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.one;
        lineRect.offsetMin = new Vector2(text.margin.x, bottom - rect.yMin);
        lineRect.offsetMax = new Vector2(0f, bottom + height - rect.yMax);
        var image = lineObject.GetComponent<Image>();
        image.sprite = ruled;
        image.type = Image.Type.Tiled;
        image.pixelsPerUnitMultiplier = 100f / ruled.pixelsPerUnit / RuledPixelUnits;
        image.raycastTarget = false;
    }

    /// <summary>
    /// 페이지 글씨를 실제로 배치해 본문 첫 줄 기준선의 높이(글씨 상자 좌표)를 잰다.
    /// 프리팹 편집 중에는 TMP가 배치하지 않으므로 같은 글씨를 현재 씬의 숨은 임시 캔버스에 복제해 정산 문구로 배치한다.
    /// </summary>
    /// <param name="text">가계부 한쪽 페이지 글씨입니다.</param>
    /// <returns>글씨 상자 좌표의 본문 첫 줄 기준선 y입니다.</returns>
    private static float measureBodyBaseline(TextMeshProUGUI text)
    {
        var canvas = new GameObject("LedgerBaselineProbe", typeof(Canvas)) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var probe = Object.Instantiate(text.gameObject, canvas.transform, false).GetComponent<TextMeshProUGUI>();
            Rect rect = text.rectTransform.rect;
            probe.rectTransform.anchorMin = probe.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            probe.rectTransform.pivot = text.rectTransform.pivot;
            probe.rectTransform.sizeDelta = rect.size;
            var sample = new DailySettlementViewData(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                System.Array.Empty<SettlementGuidelineViolationViewData>(), 0, 0, 0, 0, null, 0, false);
            probe.text = DailySettlementLedgerFormatter.Format(sample).LeftPage;
            probe.ForceMeshUpdate(true, true);
            if (probe.textInfo.lineCount < 2)
                throw new System.InvalidOperationException($"{text.name} 기준선을 잴 수 없습니다.");
            return probe.textInfo.lineInfo[1].baseline - probe.rectTransform.rect.yMax + rect.yMax;
        }
        finally
        {
            Object.DestroyImmediate(canvas);
        }
    }

    /// <summary>
    /// 소지금 판자 글씨를 다음날 버튼 글씨와 같게 만든다: 각진 굵은 글꼴, 글자마다 위에서 아래로 짙어지는 크림색,
    /// 검은 테두리, 아래(조금 오른쪽)로 떨어지는 짙은 갈색 그림자.
    /// </summary>
    private static void restylePlankText()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            Transform balance = findDeep(root.transform, "SettlementBalance")
                ?? throw new System.InvalidOperationException("SettlementBalance를 찾을 수 없습니다.");
            var text = balance.Find("Balance").GetComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PlankFontPath)
                ?? throw new System.InvalidOperationException("갈무리 굵은체 에셋이 없습니다.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(PlankMaterialPath);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, PlankMaterialPath);
            }

            material.CopyPropertiesFromMaterial(font.material);
            material.SetFloat(ShaderUtilities.ID_Sharpness, 1f);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, PlankOutlineColor);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, PlankOutlineWidth);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, PlankShadowColor);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, PlankShadowOffset.x);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, PlankShadowOffset.y);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, PlankShadowDilate);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            EditorUtility.SetDirty(material);

            text.font = font;
            text.fontSharedMaterial = material;
            text.fontSize = PlankFontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(PlankTopColor, PlankTopColor, PlankBottomColor, PlankBottomColor);
            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform findDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = findDeep(child, name);
            if (found != null) return found;
        }

        return null;
    }
}
