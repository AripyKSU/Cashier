using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>정면 화면 아트의 확대 필터를 한 번에 바꿉니다. 렌더 해상도를 올리거나 내릴 때 함께 맞춥니다.</summary>
public static class DystopiaTextureFiltering
{
    /// <summary>픽셀 스테이지가 거의 1:1로 그리는 정면 아트 폴더입니다. 탑다운 저해상도 아트는 제외합니다.</summary>
    private static readonly string[] ExistingFrontArtFolders =
    {
        "Assets/Textures/art/Facility/CounterTop",
        "Assets/Textures/art/Facility/Crate",
        "Assets/Textures/art/Facility/Props",
        "Assets/Textures/art/Customer/Male",
        "Assets/Textures/art/Customer/Female",
        "Assets/Textures/art/Customer/NormalMap",
        "Assets/Textures/art/Facility/Frame",
        "Assets/Textures/art/Facility/Clock",
        "Assets/Textures/art/Customer/Legacy",
        "Assets/Textures/art/Background",
        "Assets/Textures/art/Characters/Crowd",
        "Assets/Textures/art/Characters/Guard",
        "Assets/Textures/art/Characters/Inspector",
        "Assets/Textures/art/Effects/TimeOfDay",
        "Assets/Textures/art/Effects/Fog",
        "Assets/Textures/art/Effects/Smoke"
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
            foreach (string path in movedFrontArtPaths()) apply(path, mode, changed);
            foreach (string folder in ExistingFrontArtFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
                {
                    apply(path.Replace('\\', '/'), mode, changed);
                }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        Debug.Log($"텍스처 필터를 {mode}로 변경: {changed.Count}장. 탑다운 작업대와 상품 아트는 그대로 둡니다.");
    }

    /// <summary>더 큰 목적 폴더의 다른 자산을 건드리지 않고 이관된 정면 아트만 열거합니다.</summary>
    private static IEnumerable<string> movedFrontArtPaths()
    {
        const string customer = "Assets/Textures/Customer/Dystopia/";
        string[] classes = { "Normal", "Hasty", "PriceSensitive", "Wealthy", "Poor", "Child", "Elder" };
        int[] counts = { 12, 3, 3, 3, 3, 3, 3 };
        foreach (string gender in new[] { "Male", "Female" })
        for (int classIndex = 0; classIndex < classes.Length; classIndex++)
        for (int number = 1; number <= counts[classIndex]; number++)
        {
            string name = $"{gender}{classes[classIndex]}_{number:00}";
            yield return customer + name + ".png";
            yield return customer + "NormalMaps/" + name + "_Normal.png";
        }

        const string environment = "Assets/Textures/Environment/Dystopia/";
        foreach (string name in new[]
        {
            "Stage1CounterTop", "Stage2CounterTop", "Stage3CounterTop",
            "Stage1FoodShelf", "Stage1MedicineCabinet", "Stage2FoodShelf", "Stage2MedicineCabinet",
            "Stage2PowerCommunications", "Stage2ToolBench", "Stage3FoodShelf", "Stage3MedicineCabinet",
            "Stage3NuclearProtection", "Stage3PowerCommunications", "Stage3PrecisionElectronics", "Stage3ToolBench"
        }) yield return environment + name + ".png";

        const string ui = "Assets/Textures/UI/Dystopia/";
        foreach (string name in new[]
        {
            "Stage1CrateClosed", "Stage1CrateOpen", "Stage2CrateClosed", "Stage2CrateOpen",
            "Stage2RustedCrateClosed", "Stage2RustedCrateOpen", "Stage3CrateClosed", "Stage3CrateOpen"
        }) yield return ui + name + ".png";
    }

    private static void apply(string path, FilterMode mode, ICollection<string> changed)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.filterMode == mode) return;
        importer.filterMode = mode;
        importer.SaveAndReimport();
        changed.Add(path);
    }
}
