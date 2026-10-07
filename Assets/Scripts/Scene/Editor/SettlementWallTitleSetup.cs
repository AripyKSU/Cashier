using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정산 벽의 아빠·딸 그림 위 명성 도장, 딸 말풍선 교체, 타이틀 모래시계 동전 연출을 설치하는 에디터 도구입니다.
/// 여러 번 실행해도 같은 이름의 오브젝트를 지우고 다시 만듭니다.
/// </summary>
public static class SettlementWallTitleSetup
{
    private const string SettlementPanelPrefabPath = "Assets/Prefabs/GameUI/SettlementPanel.prefab";
    private const string DaughterPanelPrefabPath = "Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab";
    private const string HubScenePath = "Assets/Scenes/HubScene.unity";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private const string BubblePath = "Assets/Textures/UI/Dystopia/Settlement/DaughterBubble.png";
    private const string BubbleTailPath = "Assets/Textures/UI/Dystopia/Settlement/DaughterBubbleTail.png";
    private const string TitleCoinPath = "Assets/Textures/UI/Hub/TitleCoin.png";
    private const string TextBlipPath = "Assets/Sounds/Intro/NineCut/TextBlip.ogg";
    private const string DrawingName = "FamilyDrawing";
    private const string OriginalDrawingName = "Image";
    // 원래 벽 그림(134x168)을 도장보다 크게 키우는 배율.
    private const float DrawingScale = 1.72f;
    private const string DrawingTitleName = "ReputationTitle";
    private const string TailName = "BubbleTail";
    // 정산 화면 그림은 원본 1px이 UI 약 3.8px로 보인다.
    private const float ScenePixelScale = 3.8f;
    // 벽에 걸린 아빠·딸 그림 위치(정산 패널 중심 기준 UI 좌표)와 도장 크기.
    private static readonly Vector2 DrawingCenter = new Vector2(493f, 190f);
    private const float StampSize = 170f;
    // 말풍선 종이색을 가계부 종이(약 145,103,79)에 가깝게 맞추는 곱셈 색.
    private static readonly Color BubbleTint = new Color(.74f, .62f, .55f);
    // 딸 대사 한 줄의 최대 폭. 넘치면 다음 줄로 넘어간다.
    private const float DialogueWidth = 470f;

    /// <summary>세 가지 연출을 모두 설치합니다.</summary>
    [MenuItem("Cashier/Setup/Settlement Wall, Daughter Bubble, Title Coins")]
    public static void Install()
    {
        installWallDrawing();
        installDaughterBubble();
        installTitleCoins();
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementWallTitleSetup] 설치 완료");
    }

    /// <summary>
    /// 정산 화면 벽의 아빠·딸 그림 종이를 도장보다 크게 걸고, 그 위에 "우리 가게 명성" 제목과 큰 도장을 올립니다.
    /// </summary>
    private static void installWallDrawing()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath)
            ?? throw new System.InvalidOperationException("폰트가 없습니다.");
        GameObject root = PrefabUtility.LoadPrefabContents(SettlementPanelPrefabPath);
        try
        {
            Transform stamp = findDeep(root.transform, "ReputationStamp")
                ?? throw new System.InvalidOperationException("ReputationStamp를 찾을 수 없습니다.");
            Transform parent = stamp.parent;
            // 가계부 위 펜 쥔 손 연출은 쓰지 않는다.
            Transform penHand = findDeep(root.transform, "PenHand");
            if (penHand != null) Object.DestroyImmediate(penHand.gameObject);
            // 따로 그렸던 그림 대신 원래 벽에 걸려 있던 아빠·딸 그림(Image, LedgerDrawing)을 크게 키워 쓴다.
            removeChild(parent, DrawingName);
            Transform original = parent.Find(OriginalDrawingName)
                ?? throw new System.InvalidOperationException("원래 벽 그림(Image)을 찾을 수 없습니다.");
            removeChild(original, DrawingTitleName);
            var paperRect = (RectTransform)original;
            paperRect.anchorMin = paperRect.anchorMax = new Vector2(.5f, .5f);
            paperRect.pivot = new Vector2(.5f, .5f);
            paperRect.sizeDelta = new Vector2(134f, 168f) * DrawingScale;
            paperRect.anchoredPosition = DrawingCenter;
            paperRect.localRotation = Quaternion.identity;
            // 원래는 벽에 비친 흐린 그림(반투명)이었다. 실제 종이처럼 보이도록 방 조명 톤으로만 살짝 어둡게 한다.
            var paperImage = original.GetComponent<Image>();
            if (paperImage != null) paperImage.color = new Color(.86f, .78f, .68f, 1f);

            var title = new GameObject(DrawingTitleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var titleRect = (RectTransform)title.transform;
            titleRect.SetParent(paperRect, false);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(.5f, 1f);
            // 종이 윗부분에 쓴다.
            titleRect.anchoredPosition = new Vector2(0f, -22f);
            titleRect.sizeDelta = new Vector2(-40f, 34f);
            var titleText = title.GetComponent<TextMeshProUGUI>();
            titleText.font = font;
            titleText.fontSize = 21f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(.25f, .15f, .1f);
            titleText.raycastTarget = false;
            titleText.text = "우리 가게 명성";

            // 도장은 그림 위, 제목 아래 가운데에 크게 찍는다.
            var stampRect = (RectTransform)stamp;
            stampRect.SetAsLastSibling();
            stampRect.anchorMin = stampRect.anchorMax = new Vector2(.5f, .5f);
            stampRect.sizeDelta = new Vector2(StampSize, StampSize);
            stampRect.anchoredPosition = DrawingCenter + new Vector2(0f, -22f);

            PrefabUtility.SaveAsPrefabAsset(root, SettlementPanelPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 딸 말풍선을 픽셀 크기가 맞는 9-slice 종이 말풍선으로 바꾸고, 폭을 고정해 긴 대사는 줄바꿈되게 합니다.
    /// </summary>
    private static void installDaughterBubble()
    {
        var bubble = AssetDatabase.LoadAssetAtPath<Sprite>(BubblePath)
            ?? throw new System.InvalidOperationException("딸 말풍선 이미지가 없습니다.");
        var tail = AssetDatabase.LoadAssetAtPath<Sprite>(BubbleTailPath)
            ?? throw new System.InvalidOperationException("딸 말풍선 꼬리 이미지가 없습니다.");
        GameObject root = PrefabUtility.LoadPrefabContents(DaughterPanelPrefabPath);
        try
        {
            Transform bubbleTransform = findDeep(root.transform, "SpeechBubble")
                ?? throw new System.InvalidOperationException("SpeechBubble을 찾을 수 없습니다.");
            var bubbleImage = bubbleTransform.GetComponent<Image>();
            bubbleImage.sprite = bubble;
            bubbleImage.type = Image.Type.Sliced;
            // 밝은 종이색이 튀지 않도록 가계부 종이처럼 어두운 갈색 톤으로 눌러 준다.
            bubbleImage.color = BubbleTint;
            // 원본 1px이 장면의 다른 픽셀 그림과 같은 크기로 보이도록 테두리를 키운다.
            bubbleImage.pixelsPerUnitMultiplier = 1f / ScenePixelScale * (bubble.pixelsPerUnit / 100f);

            var fitter = bubbleTransform.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                // 폭은 대사 칸의 고정 선호 폭(LayoutElement)을 따르고, 높이는 줄바꿈된 줄 수만큼 늘어난다.
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            var layout = bubbleTransform.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(40, 40, 30, 36);
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
            }

            Transform dialogue = findDeep(root.transform, "Dialogue");
            if (dialogue != null)
            {
                var text = dialogue.GetComponent<TextMeshProUGUI>();
                text.textWrappingMode = TextWrappingModes.Normal;
                text.margin = Vector4.zero;
                text.fontSize = 26f;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                if (!dialogue.TryGetComponent(out LayoutElement element)) element = dialogue.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = DialogueWidth;
            }

            // 인트로 하루 대사와 같은 글자 소리를 쓴다(감독관 목소리와 구분).
            var presenter = root.GetComponentInChildren<DaughterDialoguePresenter>(true);
            var blip = AssetDatabase.LoadAssetAtPath<AudioClip>(TextBlipPath)
                ?? throw new System.InvalidOperationException("TextBlip 소리가 없습니다.");
            if (!presenter.TryGetComponent(out AudioSource source)) source = presenter.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("textBlipClip").objectReferenceValue = blip;
            serializedPresenter.FindProperty("textBlipSource").objectReferenceValue = source;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            // 날짜별 딸 이미지는 144x108 전체 캔버스 Sprite다. 기존 102x92 잘라낸 Sprite와 같은 배율(약 2.51배)과
            // 같은 자리에 보이도록 초상 크기와 위치를 캔버스 기준으로 다시 잡는다.
            Transform portraitTransform = findDeep(root.transform, "Portrait");
            if (portraitTransform != null)
            {
                var portraitRect = (RectTransform)portraitTransform;
                const float scale = 256f / 102f;
                // 캔버스는 144x128(아래 20줄은 테이블 위로 쓰러지는 단계용 여백). 위쪽 기준 위치는 그대로 둔다.
                portraitRect.sizeDelta = new Vector2(144f, 128f) * scale;
                // 피벗이 세로 가운데라, 아래로 늘린 20줄의 절반만큼 내려야 그림 윗부분 위치가 유지된다.
                portraitRect.anchoredPosition = new Vector2(14f - 23f * scale, -79.18515f - 1f * scale - 10f * scale);
            }

            removeChild(bubbleTransform, TailName);
            var tailObject = new GameObject(TailName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            tailObject.GetComponent<LayoutElement>().ignoreLayout = true;
            var tailRect = (RectTransform)tailObject.transform;
            tailRect.SetParent(bubbleTransform, false);
            tailRect.anchorMin = tailRect.anchorMax = new Vector2(0f, 0f);
            tailRect.pivot = new Vector2(0f, 1f);
            tailRect.sizeDelta = new Vector2(tail.rect.width, tail.rect.height) * ScenePixelScale;
            // 말풍선 아래 테두리와 한 픽셀 겹쳐 이어지게 한다.
            tailRect.anchoredPosition = new Vector2(40f, ScenePixelScale * 2f);
            var tailImage = tailObject.GetComponent<Image>();
            tailImage.sprite = tail;
            tailImage.raycastTarget = false;
            tailImage.color = BubbleTint;

            PrefabUtility.SaveAsPrefabAsset(root, DaughterPanelPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>타이틀 배경의 모래시계 목에서 동전이 떨어지는 연출을 HubScene 배경에 붙입니다.</summary>
    private static void installTitleCoins()
    {
        var coin = AssetDatabase.LoadAssetAtPath<Sprite>(TitleCoinPath)
            ?? throw new System.InvalidOperationException("타이틀 동전 이미지가 없습니다.");
        var scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
        GameObject background = null;
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            Transform found = findDeep(rootObject.transform, "Background");
            if (found != null) { background = found.gameObject; break; }
        }

        if (background == null) throw new System.InvalidOperationException("HubScene Background를 찾을 수 없습니다.");
        if (!background.TryGetComponent(out TitleCoinFallView view)) view = background.AddComponent<TitleCoinFallView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("coinSprite").objectReferenceValue = coin;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// 날짜별 딸 상태 이미지 7장을 Addressables 기본 그룹에 LedgerDaughterStage1~7 주소로 등록합니다.
    /// ResourceData(4421~4427)와 DaughterAppearanceData(17001~17007)는 CSV에서 같은 주소를 가리킵니다.
    /// </summary>
    [MenuItem("Cashier/Setup/Register Daughter Stage Images")]
    public static void RegisterDaughterStages()
    {
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings
            ?? throw new System.InvalidOperationException("Addressables 설정이 없습니다.");
        var group = settings.DefaultGroup;
        for (int stage = 1; stage <= 7; stage++)
        {
            string path = $"Assets/Textures/UI/Dystopia/Settlement/LedgerDaughterStage{stage}.png";
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new System.InvalidOperationException($"딸 단계 이미지가 없습니다: {path}");
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = $"LedgerDaughterStage{stage}";
        }

        settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementWallTitleSetup] 딸 단계 이미지 7장 등록 완료");
    }

    private static Transform findDeep(Transform root, string name)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            if (transform.name == name) return transform;
        return null;
    }

    private static void removeChild(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }
}
