using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마지막 날 정산 화면에 "테이블에 쓰러진 하루" 그림과 아빠 절규 대사창을 만들고 GameUIController에 연결하는 도구.
/// 대사창은 감독관·엔딩 대사창과 같은 녹슨 철판 틀을 쓴다. 여러 번 실행해도 같은 결과가 됩니다.
/// </summary>
public static class FinalDayCollapseSetup
{
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string InspectorPrefabPath = "Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab";
    private const string LyingSpritePath = "Assets/Textures/UI/Dystopia/Settlement/LedgerDaughterStage7.png";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private const string LyingName = "FinalDayDaughter";
    private const string OverlayName = "FinalDayCollapse";
    // 정산 화면(1280x720 기준) 안에서 테이블 위 하루 자리와 크기 배율.
    private static readonly Vector2 LyingPosition = new Vector2(-390f, -125f);
    private const float LyingScale = 1f;

    [MenuItem("Cashier/Setup/Final Day Collapse")]
    public static void Install()
    {
        Sprite frame = null;
        float multiplier = 3f;
        GameObject inspector = PrefabUtility.LoadPrefabContents(InspectorPrefabPath);
        try
        {
            foreach (var image in inspector.GetComponentsInChildren<Image>(true))
                if (image.name == "Dialogue" && image.sprite != null) { frame = image.sprite; multiplier = image.pixelsPerUnitMultiplier; break; }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(inspector);
        }

        if (frame == null) throw new System.InvalidOperationException("감독관 대사창 틀 그림을 찾을 수 없습니다.");
        var lyingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LyingSpritePath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            var controller = root.GetComponent<GameUIController>();
            var controllerSerialized = new SerializedObject(controller);
            var settlementPanel = ((GameObject)controllerSerialized.FindProperty("settlementPanel").objectReferenceValue).transform;
            var daughterPresenter = (DaughterDialoguePresenter)controllerSerialized.FindProperty("daughterDialoguePresenter").objectReferenceValue;
            var portrait = (Image)new SerializedObject(daughterPresenter).FindProperty("portrait").objectReferenceValue;

            removeChild(settlementPanel, LyingName);
            removeChild(settlementPanel.parent, OverlayName);

            // 테이블 위 하루: 정산 화면 안, 딸 대사 패널 바로 뒤(말풍선보다 아래)에 둔다.
            Transform daughterBranch = daughterPresenter.transform;
            while (daughterBranch.parent != settlementPanel && daughterBranch.parent != null) daughterBranch = daughterBranch.parent;
            var lying = new GameObject(LyingName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var lyingRect = (RectTransform)lying.transform;
            lyingRect.SetParent(settlementPanel, false);
            lyingRect.SetSiblingIndex(daughterBranch.parent == settlementPanel ? daughterBranch.GetSiblingIndex() : settlementPanel.childCount - 1);
            lyingRect.anchorMin = lyingRect.anchorMax = lyingRect.pivot = new Vector2(.5f, .5f);
            float pixelScale = portrait.sprite != null ? portrait.rectTransform.rect.height / portrait.sprite.rect.height : 4f;
            lyingRect.sizeDelta = lyingSprite.rect.size * pixelScale * LyingScale;
            lyingRect.anchoredPosition = LyingPosition;
            var lyingImage = lying.GetComponent<Image>();
            lyingImage.sprite = lyingSprite;
            lyingImage.raycastTarget = false;
            lying.SetActive(false);

            // 절규 연출: 소지금 판처럼 따로 앞에 그려지는 장식까지 덮도록 자체 캔버스로 손 커서 바로 아래에 그린다.
            var overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(FinalDayCollapsePresenter));
            var overlayRect = (RectTransform)overlay.transform;
            overlayRect.SetParent(settlementPanel.parent, false);
            overlayRect.SetAsLastSibling();
            stretch(overlayRect);
            var overlayCanvas = overlay.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 30000;
            overlay.AddComponent<GraphicRaycaster>();
            var group = overlay.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(overlayRect, false);
            stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(.08f, .01f, .01f, 0f);

            var box = new GameObject("Dialogue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var boxRect = (RectTransform)box.transform;
            boxRect.SetParent(overlayRect, false);
            boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(.5f, .5f);
            boxRect.anchoredPosition = new Vector2(0f, -220f);
            boxRect.sizeDelta = new Vector2(960f, 200f);
            var boxImage = box.GetComponent<Image>();
            boxImage.sprite = frame;
            boxImage.type = Image.Type.Sliced;
            boxImage.pixelsPerUnitMultiplier = multiplier;

            var backing = new GameObject("Backing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var backingRect = (RectTransform)backing.transform;
            backingRect.SetParent(boxRect, false);
            backingRect.SetAsFirstSibling();
            stretch(backingRect);
            backingRect.offsetMin = new Vector2(9f, 9f);
            backingRect.offsetMax = new Vector2(-9f, -9f);
            backing.GetComponent<Image>().color = new Color(.13f, .12f, .11f, .92f);

            var speaker = text("Speaker", boxRect, font, 19f, new Vector2(0f, 61f), new Vector2(827f, 30f));
            speaker.alignment = TextAlignmentOptions.Left;
            speaker.color = new Color(.8f, .69f, .48f);
            speaker.text = "아빠";

            var dialogue = text("Text", boxRect, font, 28f, new Vector2(0f, -5f), new Vector2(827f, 107f));
            dialogue.alignment = TextAlignmentOptions.Center;
            dialogue.color = new Color(.92f, .86f, .8f);
            dialogue.text = "하… 하루야…?";

            var presenter = overlay.GetComponent<FinalDayCollapsePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("lyingDaughter").objectReferenceValue = lyingImage;
            serialized.FindProperty("overlayGroup").objectReferenceValue = group;
            serialized.FindProperty("dim").objectReferenceValue = dim.GetComponent<Image>();
            serialized.FindProperty("dialogueBox").objectReferenceValue = boxRect;
            serialized.FindProperty("dialogue").objectReferenceValue = dialogue;
            serialized.FindProperty("shakeTarget").objectReferenceValue = (RectTransform)settlementPanel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            controllerSerialized.FindProperty("finalDayCollapse").objectReferenceValue = presenter;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
            Debug.Log("[FinalDayCollapseSetup] 마지막 날 쓰러진 하루·아빠 절규 설치 완료");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void removeChild(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    private static TextMeshProUGUI text(string name, Transform parent, TMP_FontAsset font, float size, Vector2 position, Vector2 sizeDelta)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static void stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
