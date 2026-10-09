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
    // 일일 지침서 종이(1122x1402)에서 머리글만 비운 그림을 지침서보다 작게 보여 준다.
    private const float SheetHeight = 400f;

    [MenuItem("Cashier/Setup/Guideline Violation Notice")]
    public static void Install()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(PaperPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
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
            sheet.sizeDelta = new Vector2(SheetHeight * paper.rect.width / paper.rect.height, SheetHeight);
            sheetObject.GetComponent<Image>().sprite = paper;

            // 검은 머리띠의 비운 자리(지침서의 "일일 지침" 글씨 자리)에 제목.
            var title = text("Title", sheet, font, 34f, new Vector2(.14f, .815f), new Vector2(.62f, .925f));
            title.alignment = TextAlignmentOptions.Left;
            title.color = new Color(.9f, .86f, .79f);
            title.text = "위반 통지";

            var body = text("Body", sheet, font, 16f, new Vector2(.2f, .12f), new Vector2(.8f, .75f));
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = new Color(.29f, .2f, .14f);
            body.lineSpacing = 6f;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.text = "위반 지침";

            // 오른쪽 아래 붉은 도장.
            var stamp = text("Stamp", sheet, font, 24f, new Vector2(.52f, .05f), new Vector2(.88f, .16f));
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
