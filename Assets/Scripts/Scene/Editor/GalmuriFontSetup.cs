using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 게임 한글 폰트를 갈무리11(Galmuri11)로 바꾸기 위한 TMP 폰트 에셋을 만드는 도구.
/// 물마루와 같은 설정(샘플 48, 여백 5, SDFAA, 2048 아틀라스)으로 만들고, 게임 문구에 쓰이는 글자를 미리 채워 둔다.
/// </summary>
public static class GalmuriFontSetup
{
    public const string RegularFontPath = "Assets/Fonts/Galmuri/Galmuri11.ttf";
    public const string RegularAssetPath = "Assets/Fonts/Galmuri/Galmuri11 SDF.asset";
    private const string TextDataPath = "Assets/Datas/TextData.csv";

    [MenuItem("Cashier/Setup/Create Galmuri Font Asset")]
    public static void Create()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(RegularFontPath)
            ?? throw new System.InvalidOperationException($"{RegularFontPath}를 찾을 수 없습니다.");
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularAssetPath);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(font, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            asset.name = "Galmuri11 SDF";
            AssetDatabase.CreateAsset(asset, RegularAssetPath);
            asset.atlasTexture.name = "Galmuri11 SDF Atlas";
            asset.material.name = "Galmuri11 SDF Material";
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
        Debug.Log($"[GalmuriFontSetup] 글자 {unique.Length}개 중 없는 글자: {(string.IsNullOrEmpty(missing) ? "없음" : missing)}");
    }

    /// <summary>
    /// 테두리 있는 글씨용 머티리얼을 에셋으로 만들거나 갱신해 돌려줍니다.
    /// 에디터에서 TMP outlineWidth를 바로 바꾸면 저장되지 않는 임시 머티리얼이 생기므로, 공유 머티리얼 에셋을 쓴다.
    /// </summary>
    /// <param name="suffix">머티리얼 이름 뒤에 붙일 구분 이름입니다.</param>
    /// <param name="color">테두리 색입니다.</param>
    /// <param name="width">테두리 두께(0~1)입니다.</param>
    /// <returns>저장된 머티리얼입니다.</returns>
    public static Material OutlineMaterial(string suffix, Color color, float width)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularAssetPath)
            ?? throw new System.InvalidOperationException($"{RegularAssetPath}를 먼저 만들어야 합니다.");
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
