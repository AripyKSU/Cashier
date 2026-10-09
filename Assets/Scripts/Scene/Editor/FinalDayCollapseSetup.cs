using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마지막 날 시민권 살 돈이 모자랄 때 쓰는 "시민권 구매 실패" 검은 화면과 아빠 절규 대사창을 만들고 GameUIController에 연결하는 도구.
/// 쓰러진 하루는 정산 화면 원래 자리의 딸 그림(19일차부터 7단계)을 그대로 쓴다.
/// 대사창은 감독관·엔딩 대사창과 같은 녹슨 철판 틀을 쓴다. 여러 번 실행해도 같은 결과가 됩니다.
/// </summary>
public static class FinalDayCollapseSetup
{
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string InspectorPrefabPath = "Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private const string LyingName = "FinalDayDaughter";
    private const string OverlayName = "FinalDayCollapse";

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
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            var controller = root.GetComponent<GameUIController>();
            var controllerSerialized = new SerializedObject(controller);
            var settlementPanel = ((GameObject)controllerSerialized.FindProperty("settlementPanel").objectReferenceValue).transform;

            // 예전에 테이블 위에 따로 두던 하루 그림은 지운다.
            removeChild(settlementPanel, LyingName);
            removeChild(settlementPanel.parent, OverlayName);

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

            // 시민권 구매 실패 검은 화면: 가격·총 자산·부족한 금액을 가운데 정렬로 한 줄씩, 마지막에 붉은 실패 문구.
            var shortfall = new GameObject("Shortfall", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var shortfallRect = (RectTransform)shortfall.transform;
            shortfallRect.SetParent(overlayRect, false);
            stretch(shortfallRect);
            shortfall.GetComponent<Image>().color = Color.black;
            var shortfallGroup = shortfall.GetComponent<CanvasGroup>();
            shortfallGroup.alpha = 0f;
            var shortfallLines = new TextMeshProUGUI[4];
            float[] lineY = { 90f, 40f, -10f, -100f };
            for (int index = 0; index < shortfallLines.Length; index++)
            {
                bool isVerdict = index == shortfallLines.Length - 1;
                var line = text(isVerdict ? "Verdict" : $"Line{index + 1}", shortfallRect, font, isVerdict ? 44f : 26f,
                    new Vector2(0f, lineY[index]), new Vector2(900f, isVerdict ? 70f : 44f));
                line.alignment = TextAlignmentOptions.Center;
                line.color = isVerdict ? new Color(.66f, .2f, .16f) : new Color(.85f, .8f, .73f);
                line.alpha = 0f;
                shortfallLines[index] = line;
            }

            var presenter = overlay.GetComponent<FinalDayCollapsePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("shortfallGroup").objectReferenceValue = shortfallGroup;
            var linesProperty = serialized.FindProperty("shortfallLines");
            linesProperty.arraySize = shortfallLines.Length;
            for (int index = 0; index < shortfallLines.Length; index++)
                linesProperty.GetArrayElementAtIndex(index).objectReferenceValue = shortfallLines[index];
            serialized.FindProperty("overlayGroup").objectReferenceValue = group;
            serialized.FindProperty("dim").objectReferenceValue = dim.GetComponent<Image>();
            serialized.FindProperty("dialogueBox").objectReferenceValue = boxRect;
            serialized.FindProperty("dialogue").objectReferenceValue = dialogue;
            serialized.FindProperty("shakeTarget").objectReferenceValue = (RectTransform)settlementPanel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            controllerSerialized.FindProperty("finalDayCollapse").objectReferenceValue = presenter;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
            Debug.Log("[FinalDayCollapseSetup] 시민권 구매 실패 화면·아빠 절규 설치 완료");
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
