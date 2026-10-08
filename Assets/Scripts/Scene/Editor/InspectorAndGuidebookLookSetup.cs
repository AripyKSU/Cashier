using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 감독관이 판매대(테이블) 뒤에 서 있는 것처럼 보이게 하고, 영업 전 지침서 글씨를 굵게 바꾸는 도구.
/// 감독관 그림은 조금 키우고, 판매대 윗선 아래는 잘라(RectMask2D) 다리가 판 위로 보이지 않게 한다.
/// 여러 번 실행해도 같은 결과가 됩니다.
/// </summary>
public static class InspectorAndGuidebookLookSetup
{
    private const string InspectorPrefabPath = "Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab";
    private const string PreOpenPrefabPath = "Assets/Prefabs/GameUI/PreOpenPanel.prefab";
    private const string ClipName = "PortraitClip";
    // 화면 아래에서 판매대 윗선까지의 비율(영업 화면 캡처 기준).
    private const float CounterTopRatio = .30f;
    // 원래 354x708, 위에서 173 아래. 조금 키우고 머리 위치는 거의 그대로 둔다.
    private static readonly Vector2 PortraitSize = new Vector2(425f, 850f);
    private const float PortraitTop = -150f;

    [MenuItem("Cashier/Setup/Inspector Behind Counter And Bold Guidebook")]
    public static void Install()
    {
        installInspector();
        installGuidebook();
        AssetDatabase.SaveAssets();
        Debug.Log("[InspectorAndGuidebookLookSetup] 설치 완료");
    }

    /// <summary>
    /// 소지금 판자 글씨와 설비 창 가격·안내 글씨를 굵은 갈무리로 바꾼다. 소지금은 다음날 버튼처럼 두꺼운 테두리를 준다.
    /// 판자를 새로 만들지 않고 글씨만 바꾸므로 다른 설정은 그대로다.
    /// </summary>
    [MenuItem("Cashier/Setup/Bold Balance Plank And Facility Prices")]
    public static void InstallBoldPrices()
    {
        TMP_FontAsset bold = GalmuriFontSetup.EnsureBold();
        var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontSetup.RegularAssetPath);
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/GameUI/GameUI.prefab");
        try
        {
            Transform balance = null;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == "Balance" && transform.parent != null && transform.parent.name == "SettlementBalance") { balance = transform; break; }
            if (balance == null) throw new System.InvalidOperationException("SettlementBalance/Balance를 찾을 수 없습니다.");
            var text = balance.GetComponent<TextMeshProUGUI>();
            text.font = bold;
            text.fontSharedMaterial = GalmuriFontSetup.OutlineMaterial("Plank Outline", new Color32(9, 2, 1, 255), 0.42f, true);
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/GameUI/GameUI.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab");
        try
        {
            int count = 0;
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font != regular) continue;
                text.font = bold;
                text.fontSharedMaterial = bold.material;
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab");
            Debug.Log($"[InspectorAndGuidebookLookSetup] 소지금·설비 창 글씨 {count + 1}개 굵게");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void installInspector()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(InspectorPrefabPath);
        try
        {
            var presenter = root.GetComponent<InspectorPresenter>();
            var portrait = (Image)new SerializedObject(presenter).FindProperty("portrait").objectReferenceValue
                ?? throw new System.InvalidOperationException("감독관 Portrait를 찾을 수 없습니다.");
            var portraitRect = portrait.rectTransform;
            Transform clip = portraitRect.parent.name == ClipName ? portraitRect.parent : null;
            if (clip == null)
            {
                var clipObject = new GameObject(ClipName, typeof(RectTransform), typeof(RectMask2D));
                clip = clipObject.transform;
                clip.SetParent(portraitRect.parent, false);
                clip.SetSiblingIndex(portraitRect.GetSiblingIndex());
                portraitRect.SetParent(clip, false);
            }

            // 판매대 윗선 위만 보이게 자른다. 위쪽은 패널 끝까지.
            var clipRect = (RectTransform)clip;
            clipRect.anchorMin = new Vector2(0f, CounterTopRatio);
            clipRect.anchorMax = Vector2.one;
            clipRect.offsetMin = clipRect.offsetMax = Vector2.zero;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(.5f, 1f);
            portraitRect.pivot = new Vector2(.5f, 1f);
            portraitRect.anchoredPosition = new Vector2(0f, PortraitTop);
            portraitRect.sizeDelta = PortraitSize;
            PrefabUtility.SaveAsPrefabAsset(root, InspectorPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>지침서 안 글씨(품목명·가격·지침 등)를 굵은 갈무리11로 바꾼다.</summary>
    private static void installGuidebook()
    {
        TMP_FontAsset bold = GalmuriFontSetup.EnsureBold();
        var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontSetup.RegularAssetPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PreOpenPrefabPath);
        try
        {
            int count = 0;
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font != regular && text.font != bold) continue;
                text.font = bold;
                text.fontSharedMaterial = bold.material;
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PreOpenPrefabPath);
            Debug.Log($"[InspectorAndGuidebookLookSetup] 지침서 글씨 {count}개 굵게");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
