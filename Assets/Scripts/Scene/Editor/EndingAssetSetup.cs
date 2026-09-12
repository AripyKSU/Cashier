using System;
using System.IO;
using System.Linq;
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

/// <summary>승인된 엔딩 씬·공용 프리팹과 최종 확인 UI를 Editor에서 생성한다.</summary>
public static class EndingAssetSetup
{
    private const string EndingPrefab = "Assets/Prefabs/Ending/EndingPanel.prefab";
    private static TMP_FontAsset font;

    /// <summary>신규 엔딩 자산을 생성하고 기존 정산·실패·로딩 화면에 필요한 입력을 연결한다.</summary>
    [MenuItem("Cashier/Ending/Create Ending Assets")]
    public static void CreateAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 실행하세요.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EndingPrefab) != null)
            throw new InvalidOperationException("엔딩 자산이 이미 있습니다. 기존 자산을 덮어쓰지 않습니다.");
        font = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab")
            .GetComponentInChildren<TMP_Text>(true).font;
        Directory.CreateDirectory("Assets/Prefabs/Ending");
        AssetDatabase.Refresh();
        Scene previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var root = canvas("EndingPanel");
            var presenter = root.AddComponent<EndingPresenter>();
            var group = root.AddComponent<CanvasGroup>();
            var background = image("Background", root.transform, Color.white);
            stretch(background.rectTransform);
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Dystopia/MidBackground.png");
            background.preserveAspect = true;
            var dim = image("Dim", root.transform, new Color(0.02f, 0.025f, 0.03f, 0.45f));
            stretch(dim.rectTransform);
            var heading = label("Heading", root.transform, "문 안으로", 48, new Vector2(0, 340), new Vector2(1300, 80));
            var panel = image("DialoguePanel", root.transform, new Color(0.06f, 0.065f, 0.075f, 0.95f));
            place(panel.rectTransform, new Vector2(0, -245), new Vector2(1480, 370));
            var speaker = label("Speaker", panel.transform, "감독관", 27, new Vector2(0, 125), new Vector2(1300, 45));
            speaker.color = new Color(0.86f, 0.76f, 0.51f);
            var dialogue = label("Dialogue", panel.transform, "엔딩 대사를 불러오는 중입니다.", 32, new Vector2(0, 20), new Vector2(1300, 180));
            dialogue.alignment = TextAlignmentOptions.Center;
            var indicator = label("Page", panel.transform, "1 / 4 · 임시 이미지", 21, new Vector2(-390, -130), new Vector2(440, 40));
            var next = button("Next", panel.transform, "다음", new Vector2(520, -125), new Vector2(250, 62));
            var restart = button("NewGame", panel.transform, "새 게임", new Vector2(520, -125), new Vector2(250, 62));
            restart.gameObject.AddComponent<NewGameButton>();
            restart.gameObject.SetActive(false);
            bind(presenter, ("background", background), ("heading", heading), ("speaker", speaker),
                ("dialogue", dialogue), ("pageIndicator", indicator), ("nextButton", next),
                ("newGameButton", restart), ("pageGroup", group));
            PrefabUtility.SaveAsPrefabAsset(root, EndingPrefab);
            UnityEngine.Object.DestroyImmediate(root);
            foreach (string name in new[] { "GoodEndingScene", "BadEndingScene" })
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EndingPrefab), scene);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var camera = sceneCamera();
                EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{name}.unity", true);
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(events);
                UnityEngine.Object.DestroyImmediate(camera);
                register($"Assets/Scenes/{name}.unity", false);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        setupSettlement();
        setupFailure();
        FinishResourceLinks();
        Debug.Log("Ending assets created: two scenes, shared panel, settlement confirmation, failure restart, loading retry.");
    }

    /// <summary>기존 엔딩 씬에 카메라를 보완하고 빈 Hub에 실제 메뉴를 한 번 생성한다.</summary>
    [MenuItem("Cashier/Ending/Set Up Hub Menu And Cameras")]
    public static void SetUpHubMenuAndCameras()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 실행하세요.");
        font = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab")
            .GetComponentInChildren<TMP_Text>(true).font;
        Scene previous = SceneManager.GetActiveScene();
        foreach (string name in new[] { "GoodEndingScene", "BadEndingScene", "HubScene" })
        {
            string path = $"Assets/Scenes/{name}.unity";
            if (SceneManager.GetSceneByPath(path).isLoaded)
                throw new InvalidOperationException($"편집 중인 {name}을 닫은 뒤 실행하세요.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Any())
                    sceneCamera();
                if (name == "HubScene" && !scene.GetRootGameObjects().Any(r => r.name == "HubMenu"))
                {
                    var hub = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HubScene>(true)).Single();
                    var root = canvas("HubMenu");
                    var background = image("Background", root.transform, Color.white);
                    stretch(background.rectTransform);
                    background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Dystopia/MidBackground.png");
                    var dim = image("Dim", root.transform, new Color(0.015f, 0.02f, 0.025f, 0.8f));
                    stretch(dim.rectTransform);
                    label("Title", root.transform, "CASHIER", 64, new Vector2(0, 260), new Vector2(1200, 100));
                    label("Subtitle", root.transform, "시민권을 향한 31일", 28, new Vector2(0, 170), new Vector2(1200, 60));
                    var start = button("NewGame", root.transform, "새 게임", Vector2.zero, new Vector2(360, 80));
                    var quit = button("Quit", root.transform, "끝내기", new Vector2(0, -110), new Vector2(360, 80));
                    var status = label("Status", root.transform, string.Empty, 24, new Vector2(0, -240), new Vector2(1300, 100));
                    bind(hub, ("newGameButton", start), ("quitButton", quit), ("statusText", status));
                    if (!scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Any())
                        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }
    }

    /// <summary>Overlay UI 씬에도 실제 화면을 출력할 기본 카메라와 오디오 리스너를 둔다.</summary>
    /// <returns>현재 씬에 생성한 카메라 오브젝트.</returns>
    private static GameObject sceneCamera()
    {
        var root = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        root.tag = "MainCamera";
        root.transform.position = new Vector3(0, 0, -10);
        var camera = root.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 1);
        return root;
    }

    /// <summary>생성된 엔딩 자산의 로딩 UI·리소스 등록을 마무리한다.</summary>
    public static void FinishResourceLinks()
    {
        font = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab")
            .GetComponentInChildren<TMP_Text>(true).font;
        setupLoading();
        register("Assets/Datas/EndingPageData.csv", true);
        register("Assets/Textures/Environment/Dystopia/MidBackground.png", false);
        AssetDatabase.SaveAssets();
    }

    /// <summary>정산 프리팹에 최종 확인과 상점 복귀 선택을 연결한다.</summary>
    private static void setupSettlement()
    {
        const string path = "Assets/Prefabs/GameUI/SettlementPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var presenter = root.GetComponentInChildren<DailySettlementPresenter>(true);
            var overlay = image("FinalConfirmation", root.transform, new Color(0, 0, 0, 0.94f));
            stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            var modalCanvas = overlay.gameObject.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = 100;
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            label("Message", overlay.transform, "시민권을 구매하지 않았습니다.\n결과를 확인하면 구매 기회가 끝납니다.\n이대로 마무리할까요?", 30, new Vector2(0, 60), new Vector2(850, 180));
            var cancel = button("Cancel", overlay.transform, "돌아가서 구매하기", new Vector2(-210, -100), new Vector2(350, 75));
            var confirm = button("Confirm", overlay.transform, "구매 없이 마무리", new Vector2(210, -100), new Vector2(350, 75));
            bind(presenter, ("finalConfirmationPanel", overlay.gameObject), ("finalConfirmButton", confirm), ("finalCancelButton", cancel));
            overlay.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>기존 미납 실패 화면에 새 게임 입력을 추가한다.</summary>
    private static void setupFailure()
    {
        const string path = "Assets/Prefabs/GameUI/FailurePanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var restart = button("NewGame", root.transform, "새 게임", new Vector2(0, -130), new Vector2(300, 80));
            restart.gameObject.AddComponent<NewGameButton>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>기존 로딩 씬에 엔딩 전환 재시도 버튼을 연결한다.</summary>
    private static void setupLoading()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/LoadingScene.unity", OpenSceneMode.Additive);
        try
        {
            var loading = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LoadingScene>(true)).Single();
            var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single();
            if (owner.transform.Find("EndingRetry") != null) return;
            var retry = button("EndingRetry", owner.transform, "엔딩 다시 불러오기", new Vector2(0, -140), new Vector2(400, 80));
            retry.gameObject.SetActive(false);
            bind(loading, ("endingRetryButton", retry));
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    /// <summary>기존 기본 그룹에 고유 파일명 주소와 필요한 Datas 라벨만 등록한다.</summary>
    private static void register(string path, bool isData)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        string address = Path.GetFileNameWithoutExtension(path);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException($"Asset GUID missing: {path}");
        if (settings.groups.Where(g => g != null).SelectMany(g => g.entries).Any(e => e.address == address && e.guid != guid))
            throw new InvalidOperationException($"Address duplicate: {address}");
        var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.address = address;
        if (isData) entry.SetLabel("Datas", true, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
    }

    /// <summary>직렬화 필드를 이름과 검증된 객체로 연결한다.</summary>
    private static void bind(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] values)
    {
        var serialized = new SerializedObject(target);
        foreach (var pair in values)
        {
            var property = serialized.FindProperty(pair.name) ?? throw new InvalidOperationException($"Field missing: {pair.name}");
            property.objectReferenceValue = pair.value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>기준 해상도에서 크기가 조절되는 화면 Canvas를 만든다.</summary>
    private static GameObject canvas(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        return root;
    }

    /// <summary>텍스트·장식용 Image를 만든다.</summary>
    private static Image image(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var result = go.GetComponent<Image>();
        result.color = color;
        result.raycastTarget = false;
        return result;
    }

    /// <summary>기존 한글 폰트를 재사용하는 표시 문구를 만든다.</summary>
    private static TextMeshProUGUI label(string name, Transform parent, string text, float size, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var result = go.GetComponent<TextMeshProUGUI>();
        result.font = font;
        result.fontSize = size;
        result.text = text;
        result.color = Color.white;
        result.alignment = TextAlignmentOptions.Center;
        result.raycastTarget = false;
        place(result.rectTransform, position, dimensions);
        return result;
    }

    /// <summary>키보드 Submit을 지원하는 uGUI 버튼을 만든다.</summary>
    private static Button button(string name, Transform parent, string text, Vector2 position, Vector2 dimensions)
    {
        var visual = image(name, parent, new Color(0.28f, 0.25f, 0.18f, 1));
        visual.raycastTarget = true;
        place(visual.rectTransform, position, dimensions);
        var result = visual.gameObject.AddComponent<Button>();
        result.targetGraphic = visual;
        var textView = label("Label", visual.transform, text, 26, Vector2.zero, dimensions);
        stretch(textView.rectTransform);
        return result;
    }

    /// <summary>부모 전체에 맞춘다.</summary>
    private static void stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    /// <summary>중앙 기준 위치와 크기를 지정한다.</summary>
    private static void place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
