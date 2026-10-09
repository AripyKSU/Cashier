using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameUI 영업 화면 위(감독관 안내 바로 아래)에 "거래 위반 통지서"를 만들고 GameUIController에 연결하는 도구.
/// 여러 번 실행해도 같은 결과가 됩니다.
/// </summary>
public static class ViolationNoticeSetup
{
    private const string GameUiPrefabPath = "Assets/Prefabs/GameUI/GameUI.prefab";
    private const string PaperPath = "Assets/Textures/UI/Dystopia/Notice/ViolationNotice.png";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private const string NoticeName = "ViolationNotice";
    // 종이 그림(120x150)을 3배로 보여 준다.
    private const float PaperScale = 3f;

    [MenuItem("Cashier/Setup/Guideline Violation Notice")]
    public static void Install()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(PaperPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var paper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPrefabPath);
        try
        {
            var controller = root.GetComponent<GameUIController>();
            var coach = root.GetComponentInChildren<TutorialCoachPresenter>(true)
                ?? throw new System.InvalidOperationException("TutorialCoach를 찾을 수 없습니다.");
            Transform parent = coach.transform.parent;
            Transform old = parent.Find(NoticeName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var notice = new GameObject(NoticeName, typeof(RectTransform), typeof(CanvasGroup), typeof(GuidelineViolationNoticePresenter));
            var rect = (RectTransform)notice.transform;
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(coach.transform.GetSiblingIndex());
            stretch(rect);

            // 뒤 화면을 살짝 어둡게 하고 클릭을 막는다.
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(rect, false);
            stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(.05f, .04f, .03f, .45f);

            var sheetObject = new GameObject("Sheet", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var sheet = (RectTransform)sheetObject.transform;
            sheet.SetParent(rect, false);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
            sheet.anchoredPosition = new Vector2(0f, 80f);
            sheet.sizeDelta = paper.rect.size * PaperScale;
            sheet.localRotation = Quaternion.Euler(0f, 0f, -2f);
            sheetObject.GetComponent<Image>().sprite = paper;

            // 빨간 머리띠(그림 y 8~24) 위 제목.
            var title = text("Title", sheet, font, 30f, new Vector2(.05f, 1f - 24f / 150f), new Vector2(.95f, 1f - 8f / 150f));
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(.91f, .86f, .78f);
            title.text = "거래 위반 통지서";

            var body = text("Body", sheet, font, 22f, new Vector2(.1f, .12f), new Vector2(.9f, 1f - 34f / 150f));
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = new Color(.29f, .2f, .14f);
            body.lineSpacing = 8f;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.text = "위반 지침";

            // 오른쪽 아래 붉은 도장.
            var stamp = text("Stamp", sheet, font, 26f, new Vector2(.58f, .03f), new Vector2(.96f, .16f));
            stamp.alignment = TextAlignmentOptions.Center;
            stamp.color = new Color(.66f, .2f, .16f, .9f);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            stamp.text = "위반 1";

            var hint = text("Hint", sheet, font, 16f, new Vector2(.05f, -.09f), new Vector2(.95f, -.01f));
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color(.85f, .8f, .73f, .75f);
            hint.text = "클릭해서 닫기";

            var presenter = notice.GetComponent<GuidelineViolationNoticePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("rootGroup").objectReferenceValue = notice.GetComponent<CanvasGroup>();
            serialized.FindProperty("sheet").objectReferenceValue = sheet;
            serialized.FindProperty("bodyText").objectReferenceValue = body;
            serialized.FindProperty("stampText").objectReferenceValue = stamp;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var group = notice.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("violationNotice").objectReferenceValue = presenter;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
            Debug.Log("[ViolationNoticeSetup] 거래 위반 통지서 설치 완료");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static TextMeshProUGUI text(string name, Transform parent, TMP_FontAsset font, float size, Vector2 anchorMin, Vector2 anchorMax)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.raycastTarget = false;
        return label;
    }

    private static void stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
