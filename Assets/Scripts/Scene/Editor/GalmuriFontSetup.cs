using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 게임 폰트 도구. 대부분의 화면은 물마루(Mulmaru)를 쓰고, 저녁 정산 화면(가계부·딸 말풍선·설비 창·소지금 판자)만 던파 비트비트체 v2를 쓴다.
/// 물마루와 같은 설정(샘플 48, 여백 5, SDFAA, 2048 아틀라스)으로 만들고, 게임 문구에 쓰이는 글자를 미리 채워 둔다.
/// </summary>
public static class GalmuriFontSetup
{
    // 기본 글씨(물마루). 이미 만들어진 에셋을 그대로 쓴다.
    public const string RegularAssetPath = "Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset";
    // 정산 화면 글씨(비트비트체). 코드 곳곳에서 "굵은 글씨"로 부르던 자리다.
    public const string BoldFontPath = "Assets/Fonts/DNFBitBit/DNFBitBitv2.ttf";
    public const string BoldAssetPath = "Assets/Fonts/DNFBitBit/DNFBitBitv2 SDF.asset";
    private const string TextDataPath = "Assets/Datas/TextData.csv";

    [MenuItem("Cashier/Setup/Create Galmuri Font Asset")]
    public static void Create()
    {
        EnsureBold();
        refreshOutlineMaterials();
    }

    /// <summary>
    /// 이미 쓰이는 테두리 머티리얼을 테두리 색·두께는 그대로 두고 알맞은 폰트 아틀라스로 다시 채운다.
    /// 판자(Plank) 테두리는 정산 화면용 비트비트체, 나머지(Delta 등)는 물마루.
    /// </summary>
    private static void refreshOutlineMaterials()
    {
        var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularAssetPath);
        var bold = EnsureBold();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Fonts/Galmuri" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = path.Contains("Plank") ? bold : regular;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.IsKeywordEnabled("OUTLINE_ON")) continue;
            Color color = material.GetColor(ShaderUtilities.ID_OutlineColor);
            float width = material.GetFloat(ShaderUtilities.ID_OutlineWidth);
            material.CopyPropertiesFromMaterial(font.material);
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>정산 화면용 비트비트체 에셋을 돌려줍니다. 없으면 만듭니다.</summary>
    public static TMP_FontAsset EnsureBold() => ensure(BoldFontPath, BoldAssetPath, "DNFBitBitv2 SDF");

    private static TMP_FontAsset ensure(string fontPath, string assetPath, string name)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath)
            ?? throw new System.InvalidOperationException($"{fontPath}를 찾을 수 없습니다.");
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(font, 36, 4, GlyphRenderMode.SDFAA, 4096, 4096,
                AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = name + " Atlas";
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        // 게임 문구와 기본 기호를 미리 아틀라스에 넣어 첫 표시 때 글자가 늦게 뜨지 않게 한다.
        string characters = File.ReadAllText(TextDataPath)
            + " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~…·×→←↑↓★☆♥※○●◆■□원일차명성소지금";
        string unique = new string(characters.Where(c => !char.IsControl(c)).Distinct().ToArray());
        asset.TryAddCharacters(unique, out string missing);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GalmuriFontSetup] {name}: 글자 {unique.Length}개 중 없는 글자: {(string.IsNullOrEmpty(missing) ? "없음" : missing)}");
        return asset;
    }

    /// <summary>
    /// 테두리 있는 글씨용 머티리얼을 에셋으로 만들거나 갱신해 돌려줍니다.
    /// 에디터에서 TMP outlineWidth를 바로 바꾸면 저장되지 않는 임시 머티리얼이 생기므로, 공유 머티리얼 에셋을 쓴다.
    /// </summary>
    /// <param name="suffix">머티리얼 이름 뒤에 붙일 구분 이름입니다.</param>
    /// <param name="color">테두리 색입니다.</param>
    /// <param name="width">테두리 두께(0~1)입니다.</param>
    /// <returns>저장된 머티리얼입니다.</returns>
    /// <param name="bold">true면 정산 화면용 비트비트체 아틀라스로 만든다.</param>
    public static Material OutlineMaterial(string suffix, Color color, float width, bool bold = false)
    {
        var font = bold ? EnsureBold() : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularAssetPath)
            ?? throw new System.InvalidOperationException($"{RegularAssetPath}를 먼저 만들어야 합니다.");
        // 프리팹이 참조하는 기존 테두리 머티리얼 파일을 그대로 쓰고, 내용만 지금 폰트 아틀라스로 다시 채운다.
        string path = $"Assets/Fonts/Galmuri/Galmuri11 SDF {suffix}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, path);
        }

        material.CopyPropertiesFromMaterial(font.material);
        material.EnableKeyword("OUTLINE_ON");
        material.SetColor(ShaderUtilities.ID_OutlineColor, color);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
