using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 저녁 정산 화면을 도트 느낌으로 맞추는 에디터 도구입니다.
/// 정산 화면 글씨(딸 말풍선·가계부 양쪽 페이지·명성 포스터 제목·다음날 글씨·소지금 판자)를 Neo둥근모로 바꾸고,
/// 가계부 양쪽 페이지에 공책 줄을 긋고, 소지금 판자 글씨를 다음날 버튼 그림 글씨처럼 위에서 아래로 짙어지는
/// 크림색·검은 테두리·아래 그림자로 바꿉니다. 여러 번 실행해도 같은 결과입니다.
/// </summary>
public static class SettlementPixelLookSetup
{
    private const string SettlementPanelPrefabPath = "Assets/Prefabs/GameUI/SettlementPanel.prefab";
    private const string DaughterPanelPrefabPath = "Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab";
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string RuledLinePath = "Assets/Textures/UI/Dystopia/Settlement/LedgerRuledLine.png";
    private const string NeoDgmFontPath = "Assets/Fonts/NeoDGM/neodgm.ttf";
    // 테두리가 필요 없는 정산 글씨용. 흑백 래스터 아틀라스라 글자 도트가 그대로 찍힌다.
    public const string NeoDgmAssetPath = "Assets/Fonts/NeoDGM/NeoDGM Raster.asset";
    // 소지금 판자용. 테두리·그림자를 넣으려고 같은 크기의 SDF로 만들고 Point 필터로 읽는다.
    private const string NeoDgmOutlineAssetPath = "Assets/Fonts/NeoDGM/NeoDGM SDF.asset";
    private const string PlankMaterialPath = "Assets/Fonts/NeoDGM/NeoDGM SDF Plank Outline.mat";
    // Neo둥근모 원본 글자는 1em = 16px로 그려져 있다. 샘플도 16으로 두어 글꼴 1px = 아틀라스 1텍셀이 되게 한다.
    private const int NativePixelSize = 16;
    private const int RasterPadding = 2;
    private const int OutlinePadding = 3;
    private const int AtlasSize = 1024;
    // 캔버스 기준 1280x720이 1920x1080 화면에서 1.5배로 보인다. 글씨 크기 21.333(= 32 / 1.5)이면 화면에서 정확히 32px,
    // 글꼴 1px이 화면 2px로 찍혀 도트 크기가 고르게 맞는다.
    public const float TextSize = 32f / 1.5f;
    // 줄무늬 그림 1px이 UI 몇 단위인지. 줄무늬 그림 높이 24px × 1.25 = 줄 간격 30.
    private const float RuledPixelUnits = 1.25f;
    // 글자 아래 끝(기준선)에서 줄 윗면까지의 거리(UI 단위). 글자 도트 2칸만큼 줄 위에 살짝 떠 앉는다.
    private const float RuleBelowBaseline = 2.5f;
    // 다음날 글씨에서 뽑은 색: 위쪽 밝은 크림 → 아래쪽 짙은 황갈색, 검은 테두리, 아래로 떨어지는 짙은 갈색 그림자.
    private static readonly Color PlankTopColor = new Color32(251, 230, 190, 255);
    private static readonly Color PlankBottomColor = new Color32(221, 175, 128, 255);
    private static readonly Color PlankOutlineColor = new Color32(6, 4, 3, 255);
    private static readonly Color PlankShadowColor = new Color32(36, 23, 15, 255);
    // 테두리 두께(SDF 값). TMP 테두리는 글자 경계 안팎으로 반씩 걸쳐서 Neo둥근모의 1px 획을 통째로 덮는다.
    // 같은 값만큼 글자면을 부풀려(FaceDilate) 경계를 원래 자리로 돌려 테두리가 바깥 1텍셀(글꼴 1px)에만 오게 한다.
    private const float PlankOutlineWidth = 0.15f;
    // 그림자는 아래로 글꼴 1px 내린다(오프셋 × ScaleRatioC 0.75 × GradientScale 4 = 1텍셀).
    private static readonly Vector2 PlankShadowOffset = new Vector2(0f, -1f / 3f);
    // 그림자 경계(SDF 0.36)를 대각선 1텍셀(0.41)과 2텍셀(0.31) 거리 사이 가운데에 둔다.
    // 경계가 어느 텍셀 값에도 가깝지 않아 모든 텍셀이 완전히 칠해지거나 비어, 가장자리가 반투명하게 번지지 않는다.
    private const float PlankShadowDilate = 0.22f;

    [MenuItem("Cashier/Setup/Settlement Pixel Look")]
    public static void Install()
    {
        TMP_FontAsset raster = ensureFont(NeoDgmAssetPath, "NeoDGM Raster", GlyphRenderMode.RASTER, RasterPadding);
        TMP_FontAsset outline = ensureFont(NeoDgmOutlineAssetPath, "NeoDGM SDF", GlyphRenderMode.SDFAA, OutlinePadding);
        installSettlementPanel(raster);
        installDaughterDialogue(raster);
        restylePlankText(outline);
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementPixelLookSetup] 정산 화면 Neo둥근모·공책 줄·소지금 글씨 설치 완료");
    }

    /// <summary>
    /// Neo둥근모 폰트 에셋을 돌려줍니다. 없으면 원본 크기(16)로 만들고, 게임 문구 글자를 채우고, 아틀라스를 Point 필터로 둔다.
    /// 글꼴 1px이 아틀라스 1텍셀이므로 화면에서 정수 배로 키우면 도트가 흐려지지 않는다.
    /// </summary>
    /// <param name="assetPath">폰트 에셋 경로입니다.</param>
    /// <param name="name">에셋·아틀라스·머티리얼 이름입니다.</param>
    /// <param name="renderMode">RASTER는 테두리 없는 글씨, SDFAA는 테두리·그림자가 필요한 글씨에 쓴다.</param>
    /// <param name="padding">글자 사이 여백(텍셀)입니다. SDF는 테두리·그림자가 들어갈 만큼 둔다.</param>
    /// <returns>저장된 폰트 에셋입니다.</returns>
    private static TMP_FontAsset ensureFont(string assetPath, string name, GlyphRenderMode renderMode, int padding)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(NeoDgmFontPath)
            ?? throw new System.InvalidOperationException($"{NeoDgmFontPath}를 찾을 수 없습니다.");
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(font, NativePixelSize, padding, renderMode, AtlasSize, AtlasSize,
                AtlasPopulationMode.Dynamic, false);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = name + " Atlas";
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        asset.TryAddCharacters(GalmuriFontSetup.GameCharacters(), out string missing);
        foreach (Texture2D atlas in asset.atlasTextures)
        {
            if (atlas == null) continue;
            atlas.filterMode = FilterMode.Point;
            EditorUtility.SetDirty(atlas);
        }

        if (renderMode == GlyphRenderMode.SDFAA) asset.material.SetFloat(ShaderUtilities.ID_Sharpness, 1f);
        EditorUtility.SetDirty(asset.material);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementPixelLookSetup] {name}: 없는 글자: {(string.IsNullOrEmpty(missing) ? "없음" : missing)}");
        return asset;
    }

    /// <summary>
    /// 가계부 양쪽 페이지·명성 포스터 제목·다음날 글씨를 Neo둥근모로 바꾸고, 페이지 글씨 뒤에 공책 줄을 다시 깐다.
    /// 페이지 글씨의 기울기는 없앤다. 도트 글씨를 1도만 돌려도 글자 도트가 계단처럼 어긋나 보이기 때문이다.
    /// </summary>
    /// <param name="font">정산 화면 Neo둥근모 래스터 에셋입니다.</param>
    private static void installSettlementPanel(TMP_FontAsset font)
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
                applyFont(text, font);
                text.lineSpacing = 0f;
                text.rectTransform.localRotation = Quaternion.identity;
                installLines(text, ruled);
            }

            applyFont(findDeep(root.transform, "ReputationTitle")?.GetComponent<TextMeshProUGUI>()
                ?? throw new System.InvalidOperationException("ReputationTitle을 찾을 수 없습니다."), font);
            applyFont(findDeep(root.transform, "SettlementNext")?.Find("Label")?.GetComponent<TextMeshProUGUI>()
                ?? throw new System.InvalidOperationException("SettlementNext/Label을 찾을 수 없습니다."), font);
            PrefabUtility.SaveAsPrefabAsset(root, SettlementPanelPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>딸 말풍선 대사를 Neo둥근모로 바꾼다. 말풍선은 줄 수만큼 자라므로 크기만 맞추면 넘치지 않는다.</summary>
    /// <param name="font">정산 화면 Neo둥근모 래스터 에셋입니다.</param>
    private static void installDaughterDialogue(TMP_FontAsset font)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DaughterPanelPrefabPath);
        try
        {
            applyFont(findDeep(root.transform, "Dialogue")?.GetComponent<TextMeshProUGUI>()
                ?? throw new System.InvalidOperationException("Dialogue를 찾을 수 없습니다."), font);
            PrefabUtility.SaveAsPrefabAsset(root, DaughterPanelPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>글씨를 Neo둥근모·기본 머티리얼·고정 크기로 바꾼다. 자동 크기는 도트 배율을 깨므로 끈다.</summary>
    /// <param name="text">바꿀 글씨입니다.</param>
    /// <param name="font">Neo둥근모 에셋입니다.</param>
    private static void applyFont(TextMeshProUGUI text, TMP_FontAsset font)
    {
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.enableAutoSizing = false;
        text.fontSize = TextSize;
        text.fontStyle = FontStyles.Normal;
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
    /// 소지금 판자 글씨(잔액·증감)를 Neo둥근모로 바꾸고 다음날 버튼 글씨와 같게 만든다: 글자마다 위에서 아래로 짙어지는
    /// 크림색, 검은 테두리, 아래로 떨어지는 짙은 갈색 그림자.
    /// </summary>
    /// <param name="font">테두리·그림자를 넣을 수 있는 Neo둥근모 SDF 에셋입니다.</param>
    private static void restylePlankText(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PlankMaterialPath);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, PlankMaterialPath);
        }

        material.CopyPropertiesFromMaterial(font.material);
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetColor(ShaderUtilities.ID_OutlineColor, PlankOutlineColor);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, PlankOutlineWidth);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, PlankOutlineWidth);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, PlankShadowColor);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, PlankShadowOffset.x);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, PlankShadowOffset.y);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, PlankShadowDilate);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        EditorUtility.SetDirty(material);

        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            Transform balance = findDeep(root.transform, "SettlementBalance")
                ?? throw new System.InvalidOperationException("SettlementBalance를 찾을 수 없습니다.");
            foreach (string name in new[] { "Balance", "Delta" })
            {
                var text = balance.Find(name)?.GetComponent<TextMeshProUGUI>()
                    ?? throw new System.InvalidOperationException($"SettlementBalance/{name}을 찾을 수 없습니다.");
                applyFont(text, font);
                text.fontSharedMaterial = material;
            }

            var balanceText = balance.Find("Balance").GetComponent<TextMeshProUGUI>();
            balanceText.color = Color.white;
            balanceText.enableVertexGradient = true;
            balanceText.colorGradient = new VertexGradient(PlankTopColor, PlankTopColor, PlankBottomColor, PlankBottomColor);
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
