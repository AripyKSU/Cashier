using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 대사창을 감독관 대사창과 같은 녹슨 철판 틀로 바꾸는 도구.
/// 반투명 검은 띠 대신 게임 안 대화창과 같은 틀·글씨 색을 쓰고, 화자 이름은 틀 안 왼쪽 위에 둔다.
/// 여러 번 실행해도 같은 결과가 됩니다.
/// </summary>
public static class EndingDialogueFrameSetup
{
    private const string EndingPrefabPath = "Assets/Prefabs/Ending/EndingPanel.prefab";
    private const string InspectorPrefabPath = "Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab";

    [MenuItem("Cashier/Setup/Ending Dialogue Frame")]
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
        GameObject root = PrefabUtility.LoadPrefabContents(EndingPrefabPath);
        try
        {
            Transform panel = find(root.transform, "DialoguePanel");
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = frame;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = multiplier;
            panelImage.color = Color.white;
            var panelRect = (RectTransform)panel;
            // 그림을 가리지 않도록 작게, 화면(1600×900) 아래 끝 안쪽에 둔다.
            panelRect.anchoredPosition = new Vector2(0f, -345f);
            panelRect.sizeDelta = new Vector2(1000f, 180f);

            // 틀 그림 안쪽이 비쳐 배경이 보이므로, 틀 뒤에 어두운 철판색 판을 깐다.
            Transform oldBacking = panel.Find("Backing");
            if (oldBacking != null) Object.DestroyImmediate(oldBacking.gameObject);
            var backing = new GameObject("Backing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var backingRect = (RectTransform)backing.transform;
            backingRect.SetParent(panel, false);
            backingRect.SetAsFirstSibling();
            backingRect.anchorMin = Vector2.zero;
            backingRect.anchorMax = Vector2.one;
            backingRect.offsetMin = new Vector2(14f, 14f);
            backingRect.offsetMax = new Vector2(-14f, -14f);
            var backingImage = backing.GetComponent<Image>();
            backingImage.color = new Color(.13f, .12f, .11f, .92f);
            backingImage.raycastTarget = false;

            // 화자 이름: 틀 안 왼쪽 위, 바랜 황동색.
            var speaker = find(panel, "Speaker").GetComponent<TextMeshProUGUI>();
            speaker.rectTransform.anchoredPosition = new Vector2(0f, 56f);
            speaker.rectTransform.sizeDelta = new Vector2(860f, 40f);
            speaker.alignment = TextAlignmentOptions.Left;
            speaker.color = new Color(.8f, .69f, .48f);
            speaker.fontSize = 26f;

            // 대사: 화자 이름 유무와 관계없이 틀 정가운데, 하얀색 대신 바랜 미색.
            var dialogue = find(panel, "Dialogue").GetComponent<TextMeshProUGUI>();
            dialogue.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            dialogue.rectTransform.sizeDelta = new Vector2(860f, 96f);
            dialogue.fontSize = 30f;
            dialogue.alignment = TextAlignmentOptions.Center;
            dialogue.color = new Color(.87f, .83f, .76f);

            // 쪽 번호는 쓰지 않는다.
            Transform page = find(panel, "Page");
            if (page != null) Object.DestroyImmediate(page.gameObject);

            PrefabUtility.SaveAsPrefabAsset(root, EndingPrefabPath);
            Debug.Log("[EndingDialogueFrameSetup] 엔딩 대사창 틀 적용 완료");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform find(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = find(child, name);
            if (found != null) return found;
        }

        return null;
    }
}
