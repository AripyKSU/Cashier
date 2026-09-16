using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>정면 화면 아트의 확대 필터를 한 번에 바꿉니다. 렌더 해상도를 올리거나 내릴 때 함께 맞춥니다.</summary>
public static class DystopiaTextureFiltering
{
    /// <summary>픽셀 스테이지가 거의 1:1로 그리는 정면 아트 폴더입니다. 탑다운 저해상도 아트는 제외합니다.</summary>
    private static readonly string[] FrontArtFolders =
    {
        "Assets/Textures/art/Facility/CounterTop",
        "Assets/Textures/art/Facility/Crate",
        "Assets/Textures/art/Facility/Props",
        "Assets/Textures/art/Customer/Male",
        "Assets/Textures/art/Customer/Female",
        "Assets/Textures/art/Customer/NormalMap",
        "Assets/Textures/Checkout/Background",
        "Assets/Textures/Checkout/Characters",
        "Assets/Textures/Checkout/Shop",
        "Assets/Textures/Checkout/Effects"
    };

    /// <summary>원화를 부드럽게 확대합니다. 렌더 해상도를 화면 크기에 맞췄을 때 계단과 가장자리 떨림이 사라집니다.</summary>
    [MenuItem("Dystopia/텍스처 필터/Bilinear (고해상도 렌더용)")]
    public static void UseBilinear() => Apply(FilterMode.Bilinear);

    /// <summary>픽셀을 각지게 확대합니다. 480×270 같은 낮은 렌더 해상도의 도트 표현용입니다.</summary>
    [MenuItem("Dystopia/텍스처 필터/Point (도트 렌더용)")]
    public static void UsePoint() => Apply(FilterMode.Point);

    /// <summary>정면 아트 폴더의 텍스처 필터를 모두 지정한 모드로 바꿉니다.</summary>
    /// <param name="mode">적용할 확대 필터입니다.</param>
    private static void Apply(FilterMode mode)
    {
        var changed = new List<string>();
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string folder in FrontArtFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
                {
                    string assetPath = path.Replace('\\', '/');
                    if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) continue;
                    if (importer.filterMode == mode) continue;
                    importer.filterMode = mode;
                    importer.SaveAndReimport();
                    changed.Add(assetPath);
                }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        Debug.Log($"텍스처 필터를 {mode}로 변경: {changed.Count}장. 탑다운 작업대와 상품 아트는 그대로 둡니다.");
    }
}
