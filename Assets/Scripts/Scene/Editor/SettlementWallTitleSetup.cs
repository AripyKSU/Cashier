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
    private const string DrawingPath = "Assets/Textures/UI/Dystopia/Settlement/FamilyDrawing.png";
    private const string BubblePath = "Assets/Textures/UI/Dystopia/Settlement/DaughterBubble.png";
    private const string BubbleTailPath = "Assets/Textures/UI/Dystopia/Settlement/DaughterBubbleTail.png";
    private const string TitleCoinPath = "Assets/Textures/UI/Hub/TitleCoin.png";
    private const string DrawingName = "FamilyDrawing";
    private const string DrawingTitleName = "ReputationTitle";
    private const string TailName = "BubbleTail";
    // 정산 화면 그림은 원본 1px이 UI 약 3.8px로 보인다.
    private const float ScenePixelScale = 3.8f;
    // 벽에 걸린 아빠·딸 그림 위치(정산 패널 중심 기준 UI 좌표)와 도장 크기.
    private static readonly Vector2 DrawingCenter = new Vector2(493f, 190f);
    private const float StampSize = 170f;
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
        var drawing = AssetDatabase.LoadAssetAtPath<Sprite>(DrawingPath)
            ?? throw new System.InvalidOperationException("아빠·딸 그림 이미지가 없습니다.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath)
            ?? throw new System.InvalidOperationException("폰트가 없습니다.");
        GameObject root = PrefabUtility.LoadPrefabContents(SettlementPanelPrefabPath);
        try
        {
            Transform stamp = findDeep(root.transform, "ReputationStamp")
                ?? throw new System.InvalidOperationException("ReputationStamp를 찾을 수 없습니다.");
            Transform parent = stamp.parent;
            removeChild(parent, DrawingName);

            var paper = new GameObject(DrawingName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var paperRect = (RectTransform)paper.transform;
            paperRect.SetParent(parent, false);
            paperRect.SetSiblingIndex(stamp.GetSiblingIndex());
            var paperImage = paper.GetComponent<Image>();
            paperImage.sprite = drawing;
            paperImage.raycastTarget = false;
            paperRect.anchorMin = paperRect.anchorMax = new Vector2(.5f, .5f);
            paperRect.pivot = new Vector2(.5f, .5f);
            paperRect.sizeDelta = new Vector2(drawing.rect.width, drawing.rect.height) * ScenePixelScale;
            paperRect.anchoredPosition = DrawingCenter;
            paperRect.localRotation = Quaternion.Euler(0f, 0f, -2f);

            var title = new GameObject(DrawingTitleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var titleRect = (RectTransform)title.transform;
            titleRect.SetParent(paperRect, false);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(.5f, 1f);
            // 테이프와 종이 윗선 아래, 그림 위 빈칸에 쓴다.
            titleRect.anchoredPosition = new Vector2(0f, -20f);
            titleRect.sizeDelta = new Vector2(-30f, 34f);
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
                var element = dialogue.GetComponent<LayoutElement>() ?? dialogue.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = DialogueWidth;
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
        var view = background.GetComponent<TitleCoinFallView>() ?? background.AddComponent<TitleCoinFallView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("coinSprite").objectReferenceValue = coin;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
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
