using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>승인된 참고 씬의 장식만 본편 단계 프리팹으로 추출한다.</summary>
public static class StoreStageAssetSetup
{
    private const string Root = "Assets/Prefabs/StoreStage/";
    private static readonly string[] Backgrounds = { "FarBackground", "DawnBackground", "SunsetBackground", "EveningBackground", "CityLights", "MidBackground" };
    private static readonly string[] Front = { "Counter", "Canopy", "Stage3LeftPillar", "Stage3RightPillar", "Stage3CeilingLamp", "FrontContainer", "CounterClock", "Stage2Cabinet" };

    /// <summary>참고 씬을 변경하지 않고 외형 프리팹·데이터 주소·MainScene 연결을 생성한다.</summary>
    [MenuItem("Cashier/Store Stage/Create Assets")]
    public static void CreateAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "StoreStage1World.prefab") != null)
            throw new InvalidOperationException("Stage assets already exist; edit them directly.");
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        var layoutScene = EditorSceneManager.OpenPreviewScene("Assets/DystopiaPrototype/Editor/References/Stage2Reference.unity");
        try
        {
            for (int stage = 1; stage <= 3; stage++)
            {
                var reference = EditorSceneManager.OpenPreviewScene($"Assets/DystopiaPrototype/Editor/References/Stage{stage}Reference.unity");
                try { CreateWorld(stage, reference, stage == 3 ? layoutScene : reference); CreateFront(stage, reference); }
                finally { EditorSceneManager.ClosePreviewScene(reference); }
                if (stage == 1) CreateTop(stage);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(layoutScene); }
        Register("Assets/Datas/StoreStageData.csv", true);
        InstallFrontSlots(); InstallScene(); AssetDatabase.SaveAssets();
    }

    private static Image Find(Scene scene, string name) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Image>(true)).SingleOrDefault(image => image.name == name);

    private static void CreateWorld(int stage, Scene reference, Scene layout)
    {
        var root = new GameObject($"StoreStage{stage}World");
        try
        {
            var visual = root.AddComponent<StoreStageVisual>(); visual.region = StoreStageVisual.Region.World;
            visual.worldLayers = new SpriteRenderer[6];
            for (int i = 0; i < Backgrounds.Length; i++)
            {
                var image = Find(reference, Backgrounds[i]); var rect = Find(layout, Backgrounds[i]).rectTransform;
                var renderer = new GameObject(Backgrounds[i], typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform, false); renderer.sprite = image.sprite;
                renderer.color = image.color;
                Vector2 size = Vector2.Scale(rect.sizeDelta, rect.localScale);
                renderer.transform.localPosition = rect.anchoredPosition + Vector2.Scale(Vector2.one * .5f - rect.pivot, size);
                renderer.transform.localScale = new Vector3(size.x / image.sprite.bounds.size.x, size.y / image.sprite.bounds.size.y, 1);
                renderer.transform.localRotation = rect.localRotation;
                visual.worldLayers[i] = renderer;
            }
            visual.smokeAnchors = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var image = Find(layout, i == 0 ? "LeftChimneySmoke" : "RightChimneySmoke");
                var anchor = new GameObject(image.name).transform; anchor.SetParent(root.transform, false);
                Vector2 size = Vector2.Scale(image.rectTransform.sizeDelta, image.transform.localScale);
                anchor.localPosition = image.rectTransform.anchoredPosition + Vector2.Scale(Vector2.one * .5f - image.rectTransform.pivot, size);
                anchor.localScale = new Vector3(size.x / image.sprite.bounds.size.x, size.y / image.sprite.bounds.size.y, 1);
                anchor.localRotation = image.transform.localRotation; visual.smokeAnchors[i] = anchor;
            }
            Save(root, visual);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CreateFront(int stage, Scene reference)
    {
        var root = new GameObject($"StoreStage{stage}Front", typeof(RectTransform));
        var cabinetReference = stage == 2 ? EditorSceneManager.OpenPreviewScene("Assets/DystopiaPrototype/Editor/References/Stage3Reference.unity") : default;
        try
        {
            var visual = root.AddComponent<StoreStageVisual>(); visual.region = StoreStageVisual.Region.Front;
            visual.images = new Image[Front.Length];
            for (int i = 0; i < Front.Length; i++)
            {
                var image = new GameObject(Front[i], typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(root.transform, false); image.raycastTarget = false;
                var source = Find(reference, Front[i]);
                // 분리 하부장은 Stage2Reference 저장 이후 추가되어 Stage3Reference에 보존돼 있다.
                if (i == 7 && stage == 2 && source == null) source = Find(cabinetReference, "Stage2Cabinet");
                if (source != null)
                {
                    StoreStageVisual.CopyRect(image.rectTransform, source.rectTransform);
                    image.sprite = source.sprite; image.color = source.color; image.preserveAspect = source.preserveAspect;
                    image.useSpriteMesh = source.useSpriteMesh;
                }
                image.enabled = i == 7 ? stage == 2 && source != null && source.sprite != null : source != null && source.enabled && source.gameObject.activeSelf;
                visual.images[i] = image;
            }
            if (stage == 2)
            {
                // 새 289x217 상판의 위 122px은 투명하다. 원본을 자르지 않고 표시 윗면을 기준 y에 맞춘다.
                var counter = visual.images[0].rectTransform; var cabinet = visual.images[7];
                counter.anchoredPosition += new Vector2(0, counter.sizeDelta.y * counter.localScale.y * 122f / 217f);
                StoreStageVisual.CopyRect(cabinet.rectTransform, counter);
                cabinet.rectTransform.anchoredPosition -= new Vector2(0, counter.sizeDelta.y * counter.localScale.y);
                cabinet.rectTransform.sizeDelta = new Vector2(counter.sizeDelta.x,
                    counter.sizeDelta.x * cabinet.sprite.rect.height / cabinet.sprite.rect.width);
            }
            var digits = new GameObject("ClockDigits", typeof(RectTransform)).GetComponent<RectTransform>();
            digits.SetParent(visual.images[6].transform, false);
            var original = Find(reference, "CounterClock").GetComponentInChildren<Text>(true);
            if (original == null) throw new InvalidOperationException("Reference clock digits missing");
            StoreStageVisual.CopyRect(digits, original.rectTransform); visual.clockDigits = digits;
            Save(root, visual);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); if (cabinetReference.IsValid()) EditorSceneManager.ClosePreviewScene(cabinetReference); }
    }

    private static void CreateTop(int stage)
    {
        var root = new GameObject($"StoreStage{stage}TopView", typeof(RectTransform));
        try
        {
            var visual = root.AddComponent<StoreStageVisual>(); visual.region = StoreStageVisual.Region.TopView;
            var image = new GameObject("Workbench", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(root.transform, false); image.raycastTarget = false;
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DystopiaPrototype/TopDownTest/Art/TopDownWorkbench.png");
            visual.images = new[] { image }; Save(root, visual);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Save(GameObject root, StoreStageVisual visual)
    {
        visual.Validate(visual.region);
        string path = Root + root.name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path); Register(path, false);
    }

    private static void Register(string path, bool data)
    {
        AssetDatabase.ImportAsset(path);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup);
        entry.address = Path.GetFileNameWithoutExtension(path);
        if (data) entry.SetLabel("Datas", true, true);
        EditorUtility.SetDirty(settings);
    }

    private static void InstallFrontSlots()
    {
        const string path = "Assets/Prefabs/GameUI/OperatingPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var front = root.transform.Find("AstraFrontView");
            foreach (string name in Front)
            {
                if (front.Find(name) != null) continue;
                var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(front, false); image.raycastTarget = false; image.enabled = false;
                image.transform.SetSiblingIndex(front.Find("Counter").GetSiblingIndex());
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void InstallScene()
    {
        const string path = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameUIController>(true)).Single();
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldSceneView>(true)).Single();
            var presentation = ui.gameObject.AddComponent<StoreStagePresentation>();
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("world").objectReferenceValue = world;
            var worldTargets = Backgrounds.Select(name => world.RenderRoot.Find(name).GetComponent<SpriteRenderer>()).ToArray();
            var frontTargets = Front.Select(name => ui.FrontView.Find(name).GetComponent<Image>()).ToArray();
            SetArray(serialized.FindProperty("worldTargets"), worldTargets);
            SetArray(serialized.FindProperty("frontTargets"), frontTargets);
            serialized.FindProperty("clockDigits").objectReferenceValue = ui.FrontView.Find("CounterClock/ClockText");
            serialized.FindProperty("workbench").objectReferenceValue = ui.GetComponentsInChildren<Image>(true).Single(i => i.name == "Workbench");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var controller = new SerializedObject(ui); controller.FindProperty("stagePresentation").objectReferenceValue = presentation;
            controller.ApplyModifiedPropertiesWithoutUndo();
            // 천장은 이제 UI 장식 슬롯이 소유한다. 기존 월드 시간대 배열의 참조는 보존한다.
            world.RenderRoot.Find("Canopy").GetComponent<SpriteRenderer>().enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(world.RenderRoot.Find("Canopy").GetComponent<SpriteRenderer>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
