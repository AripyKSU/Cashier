using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Git에서 제외되는 개인 씬에만 재현 가능한 손님 테스트 UI를 배치한다.</summary>
public static class CustomerSandboxSetup
{
    /// <summary>현재 개인 씬의 기존 객체를 보존하며 테스트 화면을 한 번 추가한다. 저장은 사용자에게 맡긴다.</summary>
    /// <exception cref="InvalidOperationException">PlayMode 또는 개인 씬 밖에서 호출한 경우.</exception>
    [MenuItem("Cashier/Setup Customer Sandbox")]
    public static void Setup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !scene.path.StartsWith(GameSceneManager.LocalSceneFolder, StringComparison.Ordinal))
            throw new InvalidOperationException("Assets/Scenes/Local/의 저장된 개인 씬을 EditMode에서 열어주세요.");
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<CustomerSandbox>(true) != null)
            {
                Selection.activeGameObject = root;
                return;
            }
        var canvasObject = new GameObject("Customer Sandbox", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Setup Customer Sandbox");
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        var background = CreateRect("Background", canvas.transform, Vector2.zero, new Vector2(1280, 720));
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.sizeDelta = Vector2.zero;
        background.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.07f, 0.1f, 1);
        CreateText("Title", canvas.transform, new Vector2(0, 280), new Vector2(1100, 60), "손님 생성 테스트", 32, TextAnchor.MiddleLeft);
        var identity = CreateText("Identity", canvas.transform, new Vector2(-310, 160), new Vector2(550, 60), "외형 PK / 성향 PK", 26, TextAnchor.MiddleCenter);
        var square = CreateRect("Customer Square", canvas.transform, new Vector2(-310, -15), new Vector2(250, 250)).gameObject.AddComponent<Image>();
        square.color = new Color(0.25f, 0.3f, 0.38f);
        square.raycastTarget = false;
        var order = CreateText("Order", canvas.transform, new Vector2(260, 0), new Vector2(540, 350), "CSV 로딩 대기", 25, TextAnchor.UpperLeft);
        var status = CreateText("Status", canvas.transform, new Vector2(0, -245), new Vector2(1120, 48), "대기 중", 20, TextAnchor.MiddleLeft);
        var buttonRect = CreateRect("Generate Customer", canvas.transform, new Vector2(370, -290), new Vector2(340, 60));
        var buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.12f, 0.42f, 0.67f);
        var button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        CreateText("Label", buttonRect, Vector2.zero, new Vector2(340, 60), "무작위 손님 생성", 24, TextAnchor.MiddleCenter);
        var sandbox = canvasObject.AddComponent<CustomerSandbox>();
        var serialized = new SerializedObject(sandbox);
        serialized.FindProperty("appearanceImage").objectReferenceValue = square;
        serialized.FindProperty("identityText").objectReferenceValue = identity;
        serialized.FindProperty("orderText").objectReferenceValue = order;
        serialized.FindProperty("statusText").objectReferenceValue = status;
        serialized.FindProperty("generateButton").objectReferenceValue = button;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("Customer Sandbox EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(events, "Setup Customer Sandbox");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvasObject;
    }

    /// <summary>중앙 기준 UI 사각 영역을 생성한다.</summary>
    /// <param name="name">객체 이름.</param>
    /// <param name="parent">부모.</param>
    /// <param name="position">기준 해상도에서의 위치.</param>
    /// <param name="size">기준 해상도에서의 크기.</param>
    /// <returns>생성한 RectTransform.</returns>
    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>프로젝트 font asset을 변경하지 않는 로컬 테스트용 uGUI 텍스트를 만든다.</summary>
    /// <param name="name">객체 이름.</param>
    /// <param name="parent">부모.</param>
    /// <param name="position">위치.</param>
    /// <param name="size">크기.</param>
    /// <param name="value">초기 문구.</param>
    /// <param name="fontSize">글자 크기.</param>
    /// <param name="alignment">정렬.</param>
    /// <returns>연결할 텍스트 component.</returns>
    private static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size,
        string value, int fontSize, TextAnchor alignment)
    {
        var text = CreateRect(name, parent, position, size).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.92f, 0.95f, 1);
        text.raycastTarget = false;
        return text;
    }
}
