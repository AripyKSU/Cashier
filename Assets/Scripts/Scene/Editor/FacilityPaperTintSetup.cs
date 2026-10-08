using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설비 업그레이드 창 종이가 정산 화면에서 너무 밝게 튀지 않도록, 창 안 그림들을 딸 말풍선 색 정도로 어둡게 물들이는 도구.
/// 바깥 닫기 영역(거의 투명)은 건드리지 않는다. 다시 실행해도 같은 색이 된다.
/// </summary>
public static class FacilityPaperTintSetup
{
    private const string PrefabPath = "Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab";
    private const string WindowName = "Window";
    // 정산 화면 딸 말풍선(약 167,127,91)과 원래 종이(약 228,205,176)의 비율.
    private static readonly Color PaperTint = new Color(.76f, .66f, .56f, 1f);

    [MenuItem("Cashier/Setup/Facility Paper Tint")]
    public static void Install()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform window = findDeep(root.transform, WindowName)
                ?? throw new System.InvalidOperationException("설비 창 Window를 찾을 수 없습니다.");
            int count = 0;
            foreach (var image in window.GetComponentsInChildren<Image>(true))
            {
                if (image.color.a < .5f) continue;
                image.color = PaperTint;
                count++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[FacilityPaperTintSetup] 설비 창 이미지 {count}개 색 조정 완료");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform findDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = findDeep(child, name);
            if (found != null) return found;
        }

        return null;
    }
}
