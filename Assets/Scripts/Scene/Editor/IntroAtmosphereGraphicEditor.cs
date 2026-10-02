using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>기존 Graphic 설정과 인트로 효과의 위치/속도/영역을 Inspector에 표시한다.</summary>
[CustomEditor(typeof(IntroAtmosphereGraphic))]
public sealed class IntroAtmosphereGraphicEditor : Editor
{
    /// <summary>런타임 배치 덮어쓰기 없이 저장된 효과 값만 편집한다.</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    /// <summary>현재 씬을 수정하지 않고 저장된 효과를 별도 프리뷰 씬에서 렌더링한다.</summary>
    [MenuItem("Cashier/Intro/Preview Subtle Atmosphere")]
    public static void PreviewSavedEffects()
    {
        const string output = "Temp/IntroAtmosphere";
        Directory.CreateDirectory(output);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/IntroScene.unity");
        try
        {
            var layers = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<IntroAtmosphereGraphic>(true)).ToArray();
            if (layers.Length != 2) throw new InvalidOperationException("Expected cloud and steam layers.");
            foreach (var layer in layers)
            {
                var settings = new SerializedObject(layer);
                Rect region = settings.FindProperty("region").rectValue;
                if (region.width <= 0 || region.height <= 0) throw new InvalidOperationException("Effect region is empty.");
                int cut = settings.FindProperty("cutIndex").intValue;
                var art = (Image)settings.FindProperty("artwork").objectReferenceValue;
                art.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Textures/art/Intro/NineCut/Scene{cut+1:00}.png");
                layer.rectTransform.anchorMin = layer.rectTransform.anchorMax = Vector2.one * .5f;
                layer.rectTransform.sizeDelta = art.sprite.rect.size;
                layer.ShowCut(cut);
                using (var vertices = new VertexHelper())
                {
                    populate(layer, vertices);
                    if (vertices.currentVertCount == 0) throw new InvalidOperationException("Effect mesh is empty.");
                }
                render(layer, art, output, 0);
                layer.Tick(3f);
                render(layer, art, output, 3);
                layer.ShowCut(-1);
                using (var vertices = new VertexHelper())
                {
                    populate(layer, vertices);
                    if (vertices.currentVertCount != 0) throw new InvalidOperationException("Effect survives reset.");
                }
            }
            File.WriteAllText(output + "/result.txt", "PASS: cloud/steam mesh rendering and reset. Saved scene preview only; Play Mode not tested.");
        }
        catch (Exception error)
        {
            File.WriteAllText(output + "/result.txt", error.ToString());
            throw;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    /// <summary>실제 UI 컴포넌트의 메시 생성 경로를 호출한다.</summary>
    private static void populate(IntroAtmosphereGraphic layer, VertexHelper vertices)
    {
        typeof(IntroAtmosphereGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(VertexHelper) }, null)
            .Invoke(layer, new object[] { vertices });
    }

    /// <summary>원본과 실제 효과 메시를 같은 UI 셰이더로 프리뷰 렌더링한다.</summary>
    private static void render(IntroAtmosphereGraphic layer, Image art, string output, int seconds)
    {
        var preview = new PreviewRenderUtility();
        var background = new Mesh();
        var overlay = new Mesh();
        var artMaterial = new Material(Shader.Find("UI/Default"));
        var effectMaterial = new Material(Shader.Find("UI/Default"));
        Texture2D result = null;
        try
        {
            Vector2 size = art.sprite.rect.size;
            background.vertices = new[] { new Vector3(-size.x/2,-size.y/2,.1f), new Vector3(-size.x/2,size.y/2,.1f),
                new Vector3(size.x/2,size.y/2,.1f), new Vector3(size.x/2,-size.y/2,.1f) };
            background.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            background.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            background.triangles = new[] { 0,1,2,0,2,3 };
            using (var vertices = new VertexHelper()) { populate(layer, vertices); vertices.FillMesh(overlay); }
            File.WriteAllText($"{output}/{layer.name}-{seconds}s-mesh.txt",
                $"vertices={overlay.vertexCount}; bounds={overlay.bounds}; color={layer.color}; uv0={(overlay.vertexCount>0?overlay.uv[0]:Vector2.zero)}; alphaMax={(overlay.vertexCount>0?overlay.colors.Max(c=>c.a):0)}");
            artMaterial.mainTexture = art.mainTexture; effectMaterial.mainTexture = layer.mainTexture;
            preview.BeginStaticPreview(new Rect(0,0,size.x,size.y));
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = size.y/2;
            preview.camera.aspect = size.x/size.y;
            preview.camera.transform.position = new Vector3(0,0,-10);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 100f;
            preview.DrawMesh(background, Matrix4x4.identity, artMaterial, 0);
            preview.DrawMesh(overlay, Matrix4x4.identity, effectMaterial, 0);
            preview.Render();
            result = preview.EndStaticPreview();
            File.WriteAllBytes($"{output}/{layer.name}-{seconds}s.png", result.EncodeToPNG());
        }
        finally
        {
            preview.Cleanup();
            DestroyImmediate(background); DestroyImmediate(overlay);
            DestroyImmediate(artMaterial); DestroyImmediate(effectMaterial);
            if (result != null) DestroyImmediate(result);
        }
    }
}
