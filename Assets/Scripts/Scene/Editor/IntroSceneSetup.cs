using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 인트로 씬을 생성하고 IntroDialogueController의 모든 참조를 연결한 뒤 Addressables에 등록하는 에디터 도구입니다.
/// 컷 아트(Assets/Textures/art/Intro/Cut1~4.png)와 9-Slice 대화창 스프라이트는 있으면 연결하고 없으면 비워 둡니다.
/// </summary>
public static class IntroSceneSetup
{
    private const string ScenePath = "Assets/Scenes/IntroScene.unity";
    private const string ArtFolder = "Assets/Textures/art/Intro";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [MenuItem("Cashier/Intro/Build Intro Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[IntroSceneSetup] Play Mode를 종료한 뒤 실행하세요.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 카메라·EventSystem
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // 캔버스
        var canvasObject = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(CanvasGroup), typeof(IntroDialogueController), typeof(IntroSceneEntry));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRect = canvasObject.GetComponent<RectTransform>();

        Image blocker = createFullScreenImage("Blocker", canvasRect, Color.black, null);
        blocker.raycastTarget = true;

        GameObject[] cutRoots = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/Cut{i + 1}.png");
            Image cut = createFullScreenImage($"Cut{i + 1}", canvasRect, art != null ? Color.white : new Color(0.08f, 0.08f, 0.08f), art);
            cut.raycastTarget = false;
            cut.preserveAspect = true;
            cutRoots[i] = cut.gameObject;
        }

        // CUT 3 허가증 연출: 기본 비활성, "내 밑에서 일해." 줄에서 켜진다.
        Sprite permitSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/Permit.png");
        var permit = new GameObject("PermitProp", typeof(Image)).GetComponent<Image>();
        permit.transform.SetParent(cutRoots[2].transform, false);
        permit.sprite = permitSprite;
        permit.color = permitSprite != null ? Color.white : new Color(0.6f, 0.55f, 0.45f);
        permit.raycastTarget = false;
        permit.preserveAspect = true;
        setRect(permit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, -40f), new Vector2(360f, 240f));
        permit.gameObject.SetActive(false);

        // 화자 이름
        var speakerObject = new GameObject("Speaker", typeof(RectTransform));
        speakerObject.transform.SetParent(canvasRect, false);
        setRect(speakerObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 236f), new Vector2(320f, 40f));
        TextMeshProUGUI speakerText = createText("Text", speakerObject.transform, font, 26f, TextAlignmentOptions.Center, new Color(0.75f, 0.75f, 0.72f));
        setRect(speakerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 9-Slice 대화창
        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/DialogueFrame.png");
        var box = new GameObject("DialogueBox", typeof(Image), typeof(AutoSizeNineSliceDialogueBox)).GetComponent<Image>();
        box.transform.SetParent(canvasRect, false);
        box.sprite = frame;
        box.type = Image.Type.Sliced;
        box.color = frame != null ? Color.white : new Color(0.16f, 0.17f, 0.18f, 0.96f);
        box.raycastTarget = false;
        setRect(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(320f, 80f));
        TextMeshProUGUI bodyText = createText("Body", box.transform, font, 30f, TextAlignmentOptions.Center, new Color(0.9f, 0.9f, 0.88f));
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.enableAutoSizing = false;

        // CUT 4 표시
        CanvasGroup remainingGroup = createGroup("RemainingTime", canvasRect);
        TextMeshProUGUI caption = createText("Caption", remainingGroup.transform, font, 34f, TextAlignmentOptions.Center, new Color(0.8f, 0.78f, 0.74f));
        setRect(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(900f, 60f));
        TextMeshProUGUI days = createText("Days", remainingGroup.transform, font, 128f, TextAlignmentOptions.Center, new Color(0.85f, 0.82f, 0.78f));
        setRect(days.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(900f, 180f));
        CanvasGroup dayGroup = createGroup("DayLabel", canvasRect);
        TextMeshProUGUI dayText = createText("Text", dayGroup.transform, font, 44f, TextAlignmentOptions.Center, new Color(0.8f, 0.78f, 0.74f));
        dayText.characterSpacing = 12f;
        setRect(dayText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 80f));

        // 참조 연결
        var controller = canvasObject.GetComponent<IntroDialogueController>();
        var fill = typeof(IntroDialogueController).GetMethod("fillDefaultScript", BindingFlags.Instance | BindingFlags.NonPublic);
        fill?.Invoke(controller, null);

        var so = new SerializedObject(controller);
        so.FindProperty("introRoot").objectReferenceValue = canvasObject.GetComponent<CanvasGroup>();
        so.FindProperty("inputBlocker").objectReferenceValue = blocker;
        so.FindProperty("dialogueBox").objectReferenceValue = box.GetComponent<AutoSizeNineSliceDialogueBox>();
        so.FindProperty("dialogueBoxObject").objectReferenceValue = box.gameObject;
        so.FindProperty("bodyText").objectReferenceValue = bodyText;
        so.FindProperty("speakerText").objectReferenceValue = speakerText;
        so.FindProperty("speakerObject").objectReferenceValue = speakerObject;
        so.FindProperty("remainingTimeGroup").objectReferenceValue = remainingGroup;
        so.FindProperty("remainingTimeCaptionText").objectReferenceValue = caption;
        so.FindProperty("remainingDaysText").objectReferenceValue = days;
        so.FindProperty("dayLabelGroup").objectReferenceValue = dayGroup;
        so.FindProperty("dayLabelText").objectReferenceValue = dayText;
        so.FindProperty("playOnStart").boolValue = true;
        SerializedProperty cuts = so.FindProperty("cuts");
        for (int i = 0; i < cuts.arraySize && i < cutRoots.Length; i++)
            cuts.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue = cutRoots[i];
        if (cuts.arraySize > 2)
        {
            SerializedProperty lines = cuts.GetArrayElementAtIndex(2).FindPropertyRelative("lines");
            for (int i = 0; i < lines.arraySize; i++)
            {
                if (lines.GetArrayElementAtIndex(i).FindPropertyRelative("body").stringValue != "내 밑에서 일해.") continue;
                lines.GetArrayElementAtIndex(i).FindPropertyRelative("revealOnStart").objectReferenceValue = permit.gameObject;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        var boxSo = new SerializedObject(box.GetComponent<AutoSizeNineSliceDialogueBox>());
        boxSo.FindProperty("boxRect").objectReferenceValue = box.rectTransform;
        boxSo.FindProperty("background").objectReferenceValue = box;
        boxSo.FindProperty("bodyText").objectReferenceValue = bodyText;
        boxSo.ApplyModifiedPropertiesWithoutUndo();

        var entrySo = new SerializedObject(canvasObject.GetComponent<IntroSceneEntry>());
        entrySo.FindProperty("intro").objectReferenceValue = controller;
        entrySo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        registerAddressable(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[IntroSceneSetup] 생성 완료: {ScenePath} (Addressables 주소 IntroScene). 컷 아트는 {ArtFolder}/Cut1~4.png, 대화창은 DialogueFrame.png, 허가증은 Permit.png를 찾습니다.");
    }

    /// <summary>씬을 기본 그룹에 파일명 주소로 등록한다. 같은 주소의 다른 에셋이 있으면 실패한다.</summary>
    private static void registerAddressable(string path)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables 설정이 없습니다.");
        string guid = AssetDatabase.AssetPathToGUID(path);
        string address = Path.GetFileNameWithoutExtension(path);
        if (settings.groups.Where(g => g != null).SelectMany(g => g.entries).Any(e => e.address == address && e.guid != guid))
            throw new InvalidOperationException($"Address duplicate: {address}");
        var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.address = address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
    }

    private static Image createFullScreenImage(string name, Transform parent, Color color, Sprite sprite)
    {
        var image = new GameObject(name, typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.color = color;
        setRect(image.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return image;
    }

    private static CanvasGroup createGroup(string name, Transform parent)
    {
        var group = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
        group.transform.SetParent(parent, false);
        group.alpha = 0f;
        group.blocksRaycasts = false;
        setRect(group.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return group;
    }

    private static TextMeshProUGUI createText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color)
    {
        var text = new GameObject(name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private static void setRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
