using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.U2D.Sprites;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>승인된 전용 경로에서만 Scene을 제작하고 검사하는 Editor 진입점입니다.</summary>
public static class DystopiaTools
{
    /// <summary>현재 Stage 3 전체 배치를 보존하고 적용 메뉴의 기준 사본을 갱신합니다.</summary>
    [MenuItem("Dystopia/Save Current Stage 3 Reference")]
    public static void SaveCurrentStage3Reference()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode.");
        string directory="output/stage3-reference/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        string reference=Root+"Editor/References/Stage3Reference.unity";
        File.Copy(reference,directory+"/previous-reference.unity");
        if(!EditorSceneManager.SaveScene(scene,directory+"/Stage3.unity",true)) throw new IOException("Stage 3 snapshot failed.");
        File.Copy(directory+"/Stage3.unity",reference,true);
        AssetDatabase.ImportAsset(reference,ImportAssetOptions.ForceSynchronousImport);
        File.WriteAllText("output/stage3-reference/latest.txt",directory+"/Stage3.unity");
        Debug.Log("Current Stage 3 reference saved: "+directory);
    }

    /// <summary>제공받은 Stage 3 상자를 임포트하고 현재 상자의 배치를 유지한 채 교체합니다.</summary>
    [MenuItem("Dystopia/Import Stage 3 Container")]
    public static void ImportStage3Container()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        const string path=Root+"Art/Stage3Container.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.isReadable=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=512;
        importer.npotScale=TextureImporterNPOTScale.None; importer.alphaIsTransparency=true;
        importer.SaveAndReimport();
        var factory=new SpriteDataProviderFactories(); factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects().FirstOrDefault();
        var rect=new SpriteRect { name="Stage3Container",rect=new Rect(179,121,1178,777),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=old?.spriteID ?? GUID.Generate() };
        provider.SetSpriteRects(new[] { rect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(rect.name,rect.spriteID) });
        provider.Apply(); importer.SaveAndReimport();
        Undo.RecordObject(stage,"Replace stage 3 container");
        SetStage3Container(stage.layers.Single(l=>l.source!=null && l.source.name=="FrontContainer"));
        EditorUtility.SetDirty(stage);
    }

    /// <summary>Stage 3 상자의 외형만 바꾸며 RectTransform과 그림자 설정은 보존합니다.</summary>
    /// <param name="layer">기존 상자 레이어입니다.</param>
    private static void SetStage3Container(DystopiaPixelStage.Layer layer)
    {
        var image=(UnityEngine.UI.Image)layer.source;
        var sprite=AssetDatabase.LoadAllAssetsAtPath(Root+"Art/Stage3Container.png").OfType<Sprite>().Single();
        Undo.RecordObject(image,"Replace stage 3 container sprite");
        image.sprite=sprite; image.color=new Color(.62f,.66f,.70f,1);
        layer.normalSprite=null; layer.normalMap=null;
        // 얇은 검은 외곽선을 추가 테두리 조명으로 밝히지 않습니다.
        layer.rimResponse=0;
        layer.specularResponse=0;
        layer.bottomShade=.35f;
        layer.contactShadow=new Vector4(.5f,layer.contactShadow.y,1.5f,.65f);
        EditorUtility.SetDirty(image); PrefabUtility.RecordPrefabInstancePropertyModifications(image);
    }

    /// <summary>현재 상자·시계·배경 배치를 Stage 1 기준 사본으로 보존합니다.</summary>
    [MenuItem("Dystopia/Save Current Stage 1 Reference")]
    public static void SaveCurrentStage1Reference()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode.");
        string directory="output/stage1-reference/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(scene,directory+"/Stage1.unity",true)) throw new IOException("Stage 1 snapshot failed.");
        File.WriteAllText("output/stage1-reference/latest.txt",directory+"/Stage1.unity");
        Debug.Log("Stage 1 reference saved: "+directory);
    }

    /// <summary>현재 편집 상태를 상자·시계·배경이 포함된 Stage 2 기준 사본으로 보존합니다.</summary>
    [MenuItem("Dystopia/Save Current Stage 2 Reference")]
    public static void SaveCurrentStage2Reference()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode.");
        string directory="output/stage2-reference/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(scene,directory+"/Stage2.unity",true)) throw new IOException("Stage 2 snapshot failed.");
        File.WriteAllText("output/stage2-reference/latest.txt",directory+"/Stage2.unity");
        Debug.Log("Stage 2 reference saved with current props and background: "+directory);
    }

    /// <summary>왼쪽 연기 하단을 타워 옆 건물 지붕 좌표에 맞추고 결과를 촬영합니다.</summary>
    [MenuItem("Dystopia/Align Smoke To Inset Background")]
    public static void AlignSmokeToInsetBackground()
    {
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || EditorApplication.isPlaying || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode.");
        string directory="output/background-tower-move/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        var background=stage.layers.Single(l=>l.source!=null && l.source.name=="FarBackground").source.rectTransform;
        var parent=(RectTransform)background.parent;
        // 배경 원화의 작은 사각 건물 지붕 (282,195)을 사용합니다. 반복 적용해도 누적 이동하지 않습니다.
        foreach(string name in new[] { "LeftChimneySmoke" })
        {
            var smoke=(RectTransform)parent.Find(name);
            Undo.RecordObject(smoke,"Align chimney smoke");
            var b=background.rect;
            Vector3 roof=background.TransformPoint(new Vector3(b.xMin+b.width*282f/1672f,b.yMax-b.height*195f/941f,0));
            // 연기 Sprite의 하단 중앙을 지붕에 붙입니다. 크기와 피벗은 기존 편집값을 보존합니다.
            Vector3 basePoint=smoke.TransformPoint(new Vector3(smoke.rect.center.x,smoke.rect.yMin,0));
            smoke.position+=roof-basePoint;
            EditorUtility.SetDirty(smoke); PrefabUtility.RecordPrefabInstancePropertyModifications(smoke);
        }
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Snapshot failed.");
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        var rt=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(stage);
        var previous=RenderTexture.active; RenderTexture.active=rt;
        var preview=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false); preview.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); preview.Apply();
        File.WriteAllBytes(directory+"/preview.png",preview.EncodeToPNG()); RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(preview);
        Debug.Log("Chimney smoke aligned. "+directory);
    }

    /// <summary>2단계 소품의 가져오기 해상도, 약한 노멀과 요청된 배치를 한 번 조절합니다.</summary>
    [MenuItem("Dystopia/Tune Stage 2 Props")]
    public static void TuneStage2Props()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Wrong scene.");
        var layers=new[] { "FrontContainer","CounterClock" }.Select(n=>stage.layers.Single(l=>l.source!=null && l.source.name==n)).ToArray();
        if(layers.Any(l=>!AssetDatabase.GetAssetPath(((UnityEngine.UI.Image)l.source).sprite).Contains("/Stage2"))) throw new InvalidOperationException("Apply Stage 2 props first.");
        string directory="output/shop-stage-switch/stage2-tune-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Tune stage 2 props");
        for(int i=0;i<2;i++)
        {
            var image=(UnityEngine.UI.Image)layers[i].source;
            string path=AssetDatabase.GetAssetPath(image.sprite);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize=256; importer.filterMode=FilterMode.Point; importer.isReadable=true; importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            // 셰이더가 읽는 선형 RGB 노멀을 현재 낮은 해상도에서 계산합니다.
            var normal=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
            for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
            {
                float dx=texture.GetPixel(Mathf.Min(x+1,texture.width-1),y).grayscale-texture.GetPixel(Mathf.Max(x-1,0),y).grayscale;
                float dy=texture.GetPixel(x,Mathf.Min(y+1,texture.height-1)).grayscale-texture.GetPixel(x,Mathf.Max(y-1,0)).grayscale;
                Vector3 n=new Vector3(-dx*.7f,-dy*.7f,1).normalized;
                normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1));
            }
            normal.Apply(); string normalPath=path.Replace(".png","Normal.png");
            File.WriteAllBytes(normalPath,normal.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(normal);
            AssetDatabase.ImportAsset(normalPath,ImportAssetOptions.ForceSynchronousImport);
            var ni=(TextureImporter)AssetImporter.GetAtPath(normalPath);
            ni.textureType=TextureImporterType.Default; ni.sRGBTexture=false; ni.filterMode=FilterMode.Point;
            ni.mipmapEnabled=false; ni.npotScale=TextureImporterNPOTScale.None; ni.textureCompression=TextureImporterCompression.Uncompressed; ni.SaveAndReimport();
            layers[i].normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath); layers[i].normalSprite=image.sprite; layers[i].normalResponse=.3f;
            var rect=image.rectTransform; Undo.RecordObject(rect,"Fit stage 2 prop");
            var old=rect.sizeDelta; var size=Vector2.Scale(old,i==0 ? new Vector2(.9f,.95f) : new Vector2(1,1.2f));
            rect.anchoredPosition+=new Vector2((old.x-size.x)*(.5f-rect.pivot.x),(size.y-old.y)*rect.pivot.y)+ (i==0 ? new Vector2(0,4) : new Vector2(-12,6));
            rect.sizeDelta=size; PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            if(i==1)
            {
                var digits=image.transform.Find("BusinessClock").GetComponent<UnityEngine.UI.Text>();
                var dr=digits.rectTransform; Undo.RecordObjects(new UnityEngine.Object[] {digits,dr},"Center stage 2 clock digits");
                var b=rect.rect; float fit=Mathf.Min(b.width/image.sprite.rect.width,b.height/image.sprite.rect.height);
                float w=image.preserveAspect ? image.sprite.rect.width*fit : b.width;
                float h=image.preserveAspect ? image.sprite.rect.height*fit : b.height;
                dr.anchorMin=dr.anchorMax=new Vector2(0,1); dr.pivot=new Vector2(.5f,.5f);
                dr.anchoredPosition=new Vector2((b.width-w)*rect.pivot.x+w*.5f,-(b.height-h)*(1-rect.pivot.y)-h*.59f);
                dr.sizeDelta=new Vector2(w*.72f,h*.38f); digits.fontSize=Mathf.RoundToInt(h*.29f); digits.alignment=TextAnchor.MiddleCenter;
                PrefabUtility.RecordPrefabInstancePropertyModifications(dr); PrefabUtility.RecordPrefabInstancePropertyModifications(digits);
            }
        }
        EditorUtility.SetDirty(stage); PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Snapshot failed.");
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        var rt=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(stage);
        var previous=RenderTexture.active; RenderTexture.active=rt;
        var preview=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false); preview.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); preview.Apply();
        File.WriteAllBytes(directory+"/preview.png",preview.EncodeToPNG()); RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(preview);
        Debug.Log("Stage 2 props tuned. "+directory);
    }

    /// <summary>제공된 2단계 소품 원본만 연결하고 상자와 시계 숫자의 배치를 보존합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 2 Props Only")]
    public static void ApplyStage2PropsOnly()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage = UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if (stage == null || stage.gameObject.scene.path != Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string directory = "output/shop-stage-switch/stage2-props-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if (!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        string[] names = { "FrontContainer", "CounterClock" };
        var crops = new[] { new Rect(105,125,1435,715), new Rect(377,162,1023,500) };
        Undo.RecordObject(stage,"Apply stage 2 props");
        for (int i=0;i<2;i++)
        {
            string path=Root+"Art/"+(i==0 ? "Stage2Container" : "Stage2Clock")+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048; importer.alphaIsTransparency=true; importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories(); factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var old=provider.GetSpriteRects();
            var rect=new SpriteRect { name=i==0 ? "Stage2Container" : "Stage2Clock", rect=crops[i],pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=old.FirstOrDefault()?.spriteID ?? GUID.Generate() };
            provider.SetSpriteRects(new[] { rect });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(rect.name,rect.spriteID) });
            provider.Apply(); importer.SaveAndReimport();
            var layer=stage.layers.Single(x=>x.source!=null && x.source.name==names[i]);
            var image=(UnityEngine.UI.Image)layer.source;
            Undo.RecordObject(image,"Apply stage 2 prop image");
            image.sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
            image.color=Color.white;
            // 이전 나무 소품의 노멀을 새 금속 원본에 겹치지 않습니다.
            layer.normalMap=null; layer.normalSprite=null;
            EditorUtility.SetDirty(image); PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
        EditorUtility.SetDirty(stage); PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        if (!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification snapshot failed.");
        Debug.Log("Stage 2 props applied; transforms preserved. "+directory);
    }

    /// <summary>경비병의 실제 애니메이션 참조와 렌더 표시 조건을 읽기 전용으로 기록합니다.</summary>
    [MenuItem("Dystopia/Inspect Rear Guard Links")]
    public static void InspectRearGuardLinks()
    {
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        var screen=stage.frontCanvas.GetComponentInParent<DystopiaScreen>();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var animated=(RectTransform[])typeof(DystopiaScreen).GetField("watchGuards",flags).GetValue(screen);
        var lines=new List<string> { "playing="+EditorApplication.isPlaying, "screenEnabled="+screen.enabled };
        foreach(var layer in stage.layers.Where(layer=>layer.source!=null && layer.source.name.EndsWith("WatchGuard")))
        {
            var capture=layer.source.GetComponent<DystopiaPixelSource>();
            var renderer=(MeshRenderer)typeof(DystopiaPixelStage.Layer).GetField("renderer",flags).GetValue(layer);
            lines.Add(layer.source.name+" sprite="+((UnityEngine.UI.Image)layer.source).sprite.name+" source="+layer.source.isActiveAndEnabled+" capture="+capture.isActiveAndEnabled+" vertices="+(capture.CapturedMesh==null?-1:capture.CapturedMesh.vertexCount)+" surface="+layer.surface+" renderer="+renderer.enabled+" animationLinked="+animated.Contains(layer.source.rectTransform)+" position="+layer.source.rectTransform.anchoredPosition+" scale="+layer.source.rectTransform.localScale);
        }
        Directory.CreateDirectory("output/shop-stage-switch/rear-guard-animation");
        File.WriteAllLines("output/shop-stage-switch/rear-guard-animation/live-links.txt",lines);
    }

    /// <summary>배경을 상판 기준으로 축소하고 상판 원본 명암을 복원합니다.</summary>
    [MenuItem("Dystopia/Inset Background And Restore Counter Detail")]
    public static void InsetBackgroundAndRestoreCounterDetail()
    {
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode first.");
        var names=new[] { "FarBackground", "DawnBackground", "EveningBackground", "SunsetBackground", "CityLights", "MidBackground" };
        var backgrounds=names.Select(name=>stage.layers.Single(layer=>layer.source!=null && layer.source.name==name).source.rectTransform).ToArray();
        var counter=stage.layers.Single(layer=>layer.source is UnityEngine.UI.Image image && image.sprite!=null && image.sprite.name=="Stage2Counter");
        string directory="output/shop-stage-switch/background-inset/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        var table=counter.source.rectTransform;
        var corners=new Vector3[4]; table.GetWorldCorners(corners);
        Vector3 origin=(corners[1]+corners[2])*0.5f;
        foreach(var rect in backgrounds)
        {
            Undo.RecordObject(rect,"Inset background eight percent");
            rect.position=origin+(rect.position-origin)*0.92f;
            rect.localScale*=0.92f;
            EditorUtility.SetDirty(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
        Undo.RecordObject(stage,"Restore counter texture contrast");
        counter.highlightResponse=0.8f;
        EditorUtility.SetDirty(stage);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        var texture=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage);
        var previous=RenderTexture.active;
        var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(directory+"/preview.png",pixels.EncodeToPNG()); }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        Debug.Log("Background inset and counter detail applied: "+directory);
    }

    /// <summary>분리한 배경 경비병을 기존 움직임 오브젝트에 연결합니다.</summary>
    [MenuItem("Dystopia/Animate Extracted Rear Guards")]
    public static void AnimateExtractedRearGuards()
    {
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode first.");
        var mid=stage.layers.Single(layer=>layer.source.name=="MidBackground");
        var midImage=(UnityEngine.UI.Image)mid.source;
        var sprites=new Sprite[2];
        for(int i=0;i<2;i++)
        {
            string path=Root+"Art/RearWatchGuard"+i+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.npotScale=TextureImporterNPOTScale.None;
            importer.SaveAndReimport(); sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        AssetDatabase.ImportAsset(Root+"Art/MidBackground.png",ImportAssetOptions.ForceSynchronousImport);
        var names=new[] { "LeftWatchGuard", "RightWatchGuard" };
        var layers=names.Select(name=>stage.layers.Single(layer=>layer.source.name==name)).ToArray();
        string directory="output/shop-stage-switch/rear-guard-animation/applied/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Reconnect extracted rear guards");
        var crops=new[] { new Rect(83,286,45,43),new Rect(1557,401,36,34) };
        for(int i=0;i<2;i++)
        {
            var layer=layers[i]; var image=(UnityEngine.UI.Image)layer.source; var rect=image.rectTransform;
            Undo.RecordObject(image,"Use extracted guard pixels"); Undo.RecordObject(rect,"Match background guard location");
            image.sprite=sprites[i]; image.material=null; image.color=Color.white; image.enabled=true; image.preserveAspect=false;
            var spriteRect=midImage.sprite.rect; var parent=(RectTransform)rect.parent;
            Vector2 uv=new Vector2((crops[i].center.x-spriteRect.x)/spriteRect.width,(midImage.sprite.texture.height-crops[i].center.y-spriteRect.y)/spriteRect.height);
            Vector3 point=midImage.rectTransform.TransformPoint(new Vector3(Mathf.Lerp(midImage.rectTransform.rect.xMin,midImage.rectTransform.rect.xMax,uv.x),Mathf.Lerp(midImage.rectTransform.rect.yMin,midImage.rectTransform.rect.yMax,uv.y),0));
            rect.pivot=new Vector2(.5f,.5f); rect.localScale=Vector3.one; rect.position=point;
            rect.sizeDelta=new Vector2(midImage.rectTransform.rect.width*crops[i].width/spriteRect.width*midImage.rectTransform.lossyScale.x/parent.lossyScale.x,midImage.rectTransform.rect.height*crops[i].height/spriteRect.height*midImage.rectTransform.lossyScale.y/parent.lossyScale.y);
            var capture=image.GetComponent<DystopiaPixelSource>(); if(capture!=null) capture.enabled=true;
            layer.normalMap=null; layer.normalSprite=null; layer.normalResponse=0; layer.surface=mid.surface;
            layer.highlightResponse=mid.highlightResponse; layer.lampResponse=mid.lampResponse; layer.rimResponse=mid.rimResponse;
            EditorUtility.SetDirty(image); EditorUtility.SetDirty(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image); PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
        EditorUtility.SetDirty(stage);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        var texture=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage);
        var previous=RenderTexture.active;
        var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(directory+"/preview.png",pixels.EncodeToPNG()); }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        Debug.Log("Extracted rear guards applied: "+directory);
    }

    /// <summary>가게 프레임 네 부품의 노멀 반응만 끄고 비교용 렌더를 기록합니다.</summary>
    [MenuItem("Dystopia/Disable Stage 2 Frame Normals")]
    public static void DisableStage2FrameNormals()
    {
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode first.");
        var names=new[] { "Stage2Counter", "Stage2Ceiling", "Stage2LeftPillar", "Stage2RightPillar" };
        var layers=names.Select(name=>stage.layers.Single(layer=>layer.source is UnityEngine.UI.Image image && image.sprite!=null && image.sprite.name==name)).ToArray();
        string directory="output/shop-stage-switch/frame-normal-off/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Disable frame normals");
        foreach(var layer in layers) layer.normalResponse=0;
        EditorUtility.SetDirty(stage);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        var texture=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage);
        var previous=RenderTexture.active;
        var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(directory+"/preview.png",pixels.EncodeToPNG()); }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        Debug.Log("Frame normalResponse verified: "+string.Join(",",layers.Select(layer=>layer.normalResponse))+" "+directory);
    }

    /// <summary>상판과 소품 높이를 유지하며 2단계 천장과 기둥을 원본 폭에 맞춥니다.</summary>
    [MenuItem("Dystopia/Fit Stage 2 Frame Keep Table Height")]
    public static void FitStage2FrameKeepTableHeight()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string[] names={ "Stage2Counter","Stage2Ceiling","Stage2LeftPillar","Stage2RightPillar" };
        var images=names.Select(name=>stage.layers.Select(layer=>layer.source).OfType<UnityEngine.UI.Image>().Single(image=>image.sprite!=null && image.sprite.name==name)).ToArray();
        string directory="output/shop-stage-switch/stage2-frame-fit/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up current layout.");
        float scale=stage.frontCanvas.rect.width/images[0].sprite.texture.width;
        float tableY=images[0].rectTransform.anchoredPosition.y;
        // 바깥쪽 투명 여백은 화면 밖으로 보내고 천장 높이를 30% 줄여 시야를 확보합니다.
        float frameWidth=stage.frontCanvas.rect.width*1.04f;
        float horizontalScale=frameWidth/images[0].sprite.texture.width;
        float frameLeft=(stage.frontCanvas.rect.width-frameWidth)*0.5f;
        float ceilingHeight=images[1].sprite.rect.height*scale*0.7f;
        for(int i=0;i<images.Length;i++)
        {
            var rect=images[i].rectTransform;
            Undo.RecordObject(rect,"Fit stage 2 frame around fixed tabletop");
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.localScale=Vector3.one;
            rect.localRotation=Quaternion.identity;
            Rect crop=images[i].sprite.rect;
            rect.anchoredPosition=new Vector2(frameLeft+crop.x*horizontalScale,i==0 ? tableY : i==1 ? 0 : -ceilingHeight);
            // 상판 두께와 소품 접점은 유지하고 수직 기둥만 천장과 상판 사이에 연결합니다.
            rect.sizeDelta=new Vector2(crop.width*horizontalScale,i==0 ? rect.sizeDelta.y : i==1 ? ceilingHeight : -tableY-ceilingHeight);
            EditorUtility.SetDirty(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        var texture=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage);
        var previous=RenderTexture.active;
        var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(directory+"/preview.png",pixels.EncodeToPNG()); }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        Debug.Log("Stage 2 frame fitted with tabletop and props preserved: "+directory);
    }

    /// <summary>앞쪽 탑과 소속 경비 표현만 숨기고 배경 탑은 유지합니다.</summary>
    [MenuItem("Dystopia/Hide Front Watchtowers")]
    public static void HideFrontWatchtowers()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var names=new[] { "LeftWatchTower","RightWatchTower","LeftWatchRail","RightWatchRail","LeftWatchGuard","RightWatchGuard","WatchMuzzleFlash0","WatchMuzzleFlash1" };
        var images=names.Select(name=>stage.frontCanvas.GetComponentsInChildren<RectTransform>(true).Single(target=>target.name==name)).SelectMany(target=>target.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)).Distinct().ToArray();
        string directory="output/shop-stage-switch/hide-front-towers/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        foreach(var image in images)
        {
            // 발사 연출이 오브젝트를 활성화해도 숨김 상태가 유지되도록 렌더러만 끕니다.
            Undo.RecordObject(image,"Hide front watchtower visuals");
            image.enabled=false;
            EditorUtility.SetDirty(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true);
        Debug.Log("Front watchtower images hidden: "+directory);
    }

    /// <summary>탑 교체에서 추가된 속성만 교체 전 프리팹 값으로 복구합니다.</summary>
    [MenuItem("Dystopia/Restore Watchtowers Before Replacement")]
    public static void RestoreWatchtowersBeforeReplacement()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string directory="output/shop-stage-switch/tower-restore/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        Undo.RecordObject(stage,"Restore tower lighting");
        foreach(string side in new[] { "Left","Right" })
        {
            foreach(string suffix in new[] { "WatchTower","WatchRail","WatchRailMask","WatchGuard" })
            {
                var target=stage.frontCanvas.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name==side+suffix);
                var serialized=new SerializedObject(target);
                // 교체 전후 기록에서 새로 추가된 네 배치 속성만 복구합니다.
                foreach(string property in new[] { "m_SizeDelta.x","m_SizeDelta.y","m_AnchoredPosition.x","m_AnchoredPosition.y" })
                    PrefabUtility.RevertPropertyOverride(serialized.FindProperty(property),InteractionMode.UserAction);
                if(suffix=="WatchTower" || suffix=="WatchRail")
                {
                    var image=target.GetComponent<UnityEngine.UI.Image>();
                    PrefabUtility.RevertPropertyOverride(new SerializedObject(image).FindProperty("m_Sprite"),InteractionMode.UserAction);
                    var layer=stage.layers.Single(l=>l.source==image);
                    layer.textureEdgeTrim=side=="Left" ? new Vector2(.0645933f,0) : new Vector2(0,.05861244f);
                    layer.rimResponse=1; layer.highlightResponse=1; layer.specularResponse=1;
                }
            }
        }
        EditorUtility.SetDirty(stage);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true);
        Debug.Log("Watchtower replacement reverted: "+directory);
    }

    /// <summary>승인된 분리 탑을 기존 앞쪽 탑 위치에 연결하고 현재 경비를 빈 창에 배치합니다.</summary>
    [MenuItem("Dystopia/Replace Front Watchtowers")]
    public static void ReplaceFrontWatchtowers()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        const string path=Root+"Art/ExtractedWatchTowers.png";
        string directory="output/shop-stage-switch/tower-replacement/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up current scene.");
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
        var factory=new SpriteDataProviderFactories(); factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects();
        string[] sides={ "Left","Right" };
        var regions=new[] { new Rect(38,941-655,197,420),new Rect(1452,941-655,182,296) };
        var rects=new SpriteRect[2];
        for(int i=0;i<2;i++) rects[i]=new SpriteRect { name=sides[i]+"ExtractedTower",rect=regions[i],pivot=new Vector2(0,1),alignment=SpriteAlignment.Custom,spriteID=old.FirstOrDefault(r=>r.name==sides[i]+"ExtractedTower")?.spriteID ?? GUID.Generate() };
        provider.SetSpriteRects(rects); provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        Undo.RecordObject(stage,"Replace front watchtowers");
        for(int i=0;i<2;i++)
        {
            var layer=stage.layers.Single(l=>l.source!=null && l.source.name==sides[i]+"WatchTower");
            var image=(UnityEngine.UI.Image)layer.source; var rect=image.rectTransform;
            var guard=(RectTransform)stage.frontCanvas.Find(sides[i]+"WatchGuard");
            var rail=stage.layers.Single(l=>l.source!=null && l.source.name==sides[i]+"WatchRail");
            var railImage=(UnityEngine.UI.Image)rail.source;
            var mask=(RectTransform)railImage.transform.parent;
            var changed=new UnityEngine.Object[] { image,rect,image.gameObject,guard,guard.gameObject,railImage,railImage.rectTransform,mask };
            Undo.RecordObjects(changed,"Fit extracted tower and moving guard");
            bool applied=image.sprite!=null && AssetDatabase.GetAssetPath(image.sprite)==path;
            if(!applied)
            {
                // 원래 지붕의 중심을 기준으로 이동하고 새 탑의 가로세로 비율은 유지합니다.
                float pixelScale=rect.sizeDelta.x/1672f;
                Vector2 roof=rect.anchoredPosition+new Vector2((i==0?178:1527)*pixelScale,-(i==0?120:237)*rect.sizeDelta.y/941f);
                rect.sizeDelta=regions[i].size*pixelScale;
                rect.anchoredPosition=roof-new Vector2(rect.sizeDelta.x*.5f,0);
                Vector2 windowCenter=new Vector2(i==0?70:61,i==0?86:77)*pixelScale;
                guard.sizeDelta=new Vector2(i==0?32:27,i==0?34:28)*pixelScale;
                guard.anchoredPosition=rect.anchoredPosition+new Vector2(windowCenter.x,-windowCenter.y)+Vector2.Scale(guard.pivot-new Vector2(.5f,.5f),guard.sizeDelta);
                mask.anchoredPosition=rect.anchoredPosition-new Vector2(0,(i==0?108:96)*pixelScale);
                mask.sizeDelta=new Vector2(rect.sizeDelta.x,(i==0?80:69)*pixelScale);
                railImage.rectTransform.anchoredPosition=new Vector2(0,(i==0?108:96)*pixelScale);
                railImage.rectTransform.sizeDelta=rect.sizeDelta;
            }
            image.sprite=sprites.Single(s=>s.name==sides[i]+"ExtractedTower"); image.preserveAspect=false;
            image.gameObject.SetActive(true); guard.gameObject.SetActive(true);
            railImage.sprite=image.sprite; railImage.preserveAspect=false;
            layer.textureEdgeTrim=Vector2.zero; rail.textureEdgeTrim=Vector2.zero;
            // 분리 경계의 인위적인 흰 외곽광과 밝은 얼룩을 억제합니다.
            layer.rimResponse=rail.rimResponse=0;
            layer.highlightResponse=rail.highlightResponse=.7f;
            layer.specularResponse=rail.specularResponse=0;
            layer.normalMap=null; layer.normalSprite=null; rail.normalMap=null; rail.normalSprite=null;
            foreach(var obj in changed) { EditorUtility.SetDirty(obj); PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
        }
        EditorUtility.SetDirty(stage); PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Canvas.ForceUpdateCanvases();
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true);
        var texture=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage);
        var previous=RenderTexture.active; var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(directory+"/preview.png",pixels.EncodeToPNG()); }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        Debug.Log("Extracted watchtowers and existing moving guards connected. "+directory);
    }

    /// <summary>사선 분리한 다섯 부품을 현재 상판과 소품 배치를 기준으로 연결합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 2 Seam Parts")]
    public static void ApplyStage2SeamParts()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var counter=stage.layers.Single(l=>l.source!=null && (l.source.name=="Counter" || l.source.name=="Stage3Counter"));
        var ceiling=stage.layers.Single(l=>l.source!=null && (l.source.name=="Canopy" || l.source.name=="Stage3Ceiling"));
        var left=stage.layers.Single(l=>l.source!=null && l.source.name=="Stage3LeftPillar");
        var right=stage.layers.Single(l=>l.source!=null && l.source.name=="Stage3RightPillar");
        var counterImage=(UnityEngine.UI.Image)counter.source;
        bool alreadySplit=counterImage.sprite!=null && counterImage.sprite.name=="Stage2_TopPlate";
        if(!alreadySplit && (counterImage.sprite==null || counterImage.sprite.name!="Stage2Counter")) throw new InvalidOperationException("Apply stage 2 shop first.");
        string directory="output/shop-stage-switch/stage2-seams-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up current layout.");
        SliceStage2AlongSeams();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Root+"Art/Stage2Shop.png").OfType<Sprite>().ToArray();
        Undo.RecordObject(stage,"Connect stage 2 seam parts");
        var cabinet=stage.layers.SingleOrDefault(l=>l.source!=null && l.source.name=="Stage2Cabinet");
        if(cabinet==null)
        {
            var go=new GameObject("Stage2Cabinet",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            Undo.RegisterCreatedObjectUndo(go,"Separate stage 2 cabinet");
            go.transform.SetParent(counter.source.transform.parent,false);
            cabinet=JsonUtility.FromJson<DystopiaPixelStage.Layer>(JsonUtility.ToJson(counter));
            cabinet.source=go.GetComponent<UnityEngine.UI.Image>();
            ((UnityEngine.UI.Image)cabinet.source).raycastTarget=false;
        }
        var targets=new[] { left,right,counter,ceiling,cabinet };
        string[] names={ "Stage2_LeftPost","Stage2_RightPost","Stage2_TopPlate","Stage2_Ceiling","Stage2_Cabinet" };
        foreach(var layer in targets) Undo.RecordObjects(new UnityEngine.Object[] { layer.source,layer.source.rectTransform,layer.source.gameObject },"Connect stage 2 part");
        if(!alreadySplit)
        {
            var plate=counter.source.rectTransform; var roof=ceiling.source.rectTransform;
            var originalSize=plate.sizeDelta; var originalPosition=plate.anchoredPosition;
            float xPerPixel=originalSize.x*plate.localScale.x/1672;
            float postTop=roof.anchoredPosition.y-88*roof.sizeDelta.y*roof.localScale.y/180;
            float postScaleY=(postTop-originalPosition.y)/(599-88);
            var storage=cabinet.source.rectTransform;
            storage.anchorMin=plate.anchorMin; storage.anchorMax=plate.anchorMax; storage.pivot=plate.pivot;
            storage.localScale=plate.localScale; storage.localRotation=plate.localRotation;
            storage.anchoredPosition=originalPosition-new Vector2(0,originalSize.y*.5f*plate.localScale.y);
            storage.sizeDelta=new Vector2(originalSize.x,originalSize.y*.5f);
            plate.sizeDelta=new Vector2(originalSize.x,originalSize.y*.5f);
            roof.sizeDelta=new Vector2(roof.sizeDelta.x,roof.sizeDelta.y*181/180);
            for(int i=0;i<2;i++)
            {
                var post=targets[i].source.rectTransform;
                post.anchoredPosition=new Vector2(originalPosition.x+(i==0 ? 28 : 1572)*xPerPixel,postTop);
                post.sizeDelta=new Vector2(72*xPerPixel/post.localScale.x,558*postScaleY/post.localScale.y);
            }
        }
        for(int i=0;i<targets.Length;i++)
        {
            var layer=targets[i]; var image=(UnityEngine.UI.Image)layer.source;
            image.sprite=sprites.Single(s=>s.name==names[i]); image.useSpriteMesh=true; image.preserveAspect=false;
            image.gameObject.SetActive(true); layer.normalSprite=image.sprite;
            layer.normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Art/Stage2ShopNormal.png");
            foreach(var obj in new UnityEngine.Object[] { image,image.rectTransform,image.gameObject }) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }
        // 상판이 기둥 밑동을 덮는 원본 순서를 유지하고 다른 배경 레이어 순서는 보존합니다.
        var layers=stage.layers.Where(l=>l!=counter && l!=cabinet).ToList();
        int index=Math.Max(layers.IndexOf(left),Math.Max(layers.IndexOf(right),layers.IndexOf(ceiling)))+1;
        layers.Insert(index,cabinet); layers.Insert(index+1,counter); stage.layers=layers.ToArray();
        EditorUtility.SetDirty(stage); PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("Release",flags).Invoke(stage,null);
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not verify seam parts.");
        var texture=typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage) as RenderTexture;
        if(texture!=null)
        {
            var previous=RenderTexture.active; var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(directory+"/preview.png",pixels.EncodeToPNG()); }
            finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        }
        Debug.Log("Five independent stage 2 seam parts applied; box and clock preserved. "+directory);
    }

    /// <summary>2단계 원본의 검은 사선 경계를 따라 다섯 부품을 독립 스프라이트로 나눕니다.</summary>
    [MenuItem("Dystopia/Slice Stage 2 Along Seams")]
    public static void SliceStage2AlongSeams()
    {
        const string path=Root+"Art/Stage2Shop.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.Tight; importer.SetTextureSettings(settings);
        var factory=new SpriteDataProviderFactories(); factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        string[] names={ "Stage2_LeftPost","Stage2_RightPost","Stage2_TopPlate","Stage2_Ceiling","Stage2_Cabinet" };
        // 좌표는 원본의 좌상단 기준이며 기둥-천장 및 기둥-상판의 검은 사선을 공유합니다.
        Vector2[][] polygons={
            new[] { new Vector2(28,88),new Vector2(100,88),new Vector2(100,599),new Vector2(28,646) },
            new[] { new Vector2(1572,88),new Vector2(1644,88),new Vector2(1644,646),new Vector2(1572,599) },
            new[] { new Vector2(100,599),new Vector2(1572,599),new Vector2(1672,668),new Vector2(1672,770),new Vector2(0,770),new Vector2(0,668) },
            new[] { new Vector2(0,0),new Vector2(1672,0),new Vector2(1672,56),new Vector2(1644,88),new Vector2(1572,88),new Vector2(1572,181),new Vector2(1493,108),new Vector2(179,108),new Vector2(100,181),new Vector2(100,88),new Vector2(28,88),new Vector2(0,56) },
            new[] { new Vector2(0,770),new Vector2(1672,770),new Vector2(1672,941),new Vector2(0,941) }
        };
        var rects=provider.GetSpriteRects().ToList();
        for(int i=0;i<names.Length;i++)
        {
            var polygon=polygons[i]; float left=polygon.Min(p=>p.x),right=polygon.Max(p=>p.x),top=polygon.Min(p=>p.y),bottom=polygon.Max(p=>p.y);
            var existing=rects.FirstOrDefault(r=>r.name==names[i]);
            if(existing!=null) rects.Remove(existing);
            rects.Add(new SpriteRect { name=names[i],rect=new Rect(left,941-bottom,right-left,bottom-top),pivot=new Vector2(0,1),alignment=SpriteAlignment.Custom,spriteID=existing?.spriteID ?? GUID.Generate() });
        }
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        var outlines=provider.GetDataProvider<ISpriteOutlineDataProvider>();
        for(int i=0;i<names.Length;i++)
        {
            var rect=rects.Single(r=>r.name==names[i]);
            var outline=polygons[i].Select(p=>new Vector2(p.x,941-p.y)-rect.rect.center).Reverse().ToArray();
            outlines.SetOutlines(rect.spriteID,new List<Vector2[]> { outline });
        }
        provider.Apply(); importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Where(s=>names.Contains(s.name)).ToArray();
        if(sprites.Length!=5 || sprites.Any(s=>s.triangles.Length==0)) throw new InvalidOperationException("Five valid sprite meshes required.");
        Directory.CreateDirectory("output/shop-stage-switch/stage2-seam-slices");
        File.WriteAllLines("output/shop-stage-switch/stage2-seam-slices/sprites.txt",sprites.Select(s=>s.name+" rect="+s.rect+" vertices="+s.vertices.Length+" triangles="+s.triangles.Length/3));
        var preview=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Stage2ShopParts",typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root,preview);
        try
        {
            ((RectTransform)root.transform).sizeDelta=new Vector2(1672,941);
            foreach(string name in names)
            {
                var sprite=sprites.Single(s=>s.name==name);
                var part=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));
                part.transform.SetParent(root.transform,false);
                var rect=(RectTransform)part.transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
                rect.anchoredPosition=new Vector2(sprite.rect.x,sprite.rect.yMax-941); rect.sizeDelta=sprite.rect.size;
                var image=part.GetComponent<UnityEngine.UI.Image>(); image.sprite=sprite; image.useSpriteMesh=true; image.raycastTarget=false;
            }
            PrefabUtility.SaveAsPrefabAsset(root,Root+"Prefabs/Stage2ShopParts.prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Debug.Log("Five stage 2 seam sprites created; original pixels and existing sprite IDs preserved.");
    }

    /// <summary>배경 군중의 인위적인 외곽광만 끄고 편집 모드 전후 렌더를 기록합니다.</summary>
    [MenuItem("Dystopia/Remove Crowd Rim Light")]
    public static void RemoveCrowdRimLight()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var crowd=stage.layers.Where(layer=>layer.source is DystopiaCrowdImage).ToArray();
        if(crowd.Length!=3) throw new InvalidOperationException("Expected three crowd layers.");
        string directory="output/shop-stage-switch/crowd-rim-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        Action<string> capture=label=>
        {
            typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
            var texture=typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage) as RenderTexture;
            if(texture==null) return;
            var previous=RenderTexture.active; var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(directory+"/"+label+".png",pixels.EncodeToPNG()); }
            finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        };
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        capture("before");
        Undo.RecordObject(stage,"Remove crowd rim light");
        foreach(var layer in crowd) layer.rimResponse=0;
        EditorUtility.SetDirty(stage); PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not verify crowd lighting.");
        capture("after");
        Debug.Log("Three crowd rim responses set to zero; layout and motion preserved. "+directory);
    }

    /// <summary>2단계에서 감시탑 원본에 포함된 옛 상점 기둥만 숨깁니다.</summary>
    [MenuItem("Dystopia/Hide Legacy Stage 2 Posts")]
    public static void HideLegacyStage2Posts()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        if(!stage.layers.Any(l=>l.source is UnityEngine.UI.Image image && image.sprite != null && image.sprite.name=="Stage2Ceiling")) throw new InvalidOperationException("Apply stage 2 first.");
        string directory="output/shop-stage-switch/stage2-posts-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        Undo.RecordObject(stage,"Hide embedded legacy posts");
        SetLegacyPostVisibility(stage,true);
        EditorUtility.SetDirty(stage); PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not verify post removal.");
        Debug.Log("Legacy embedded posts hidden: "+directory);
        // 편집 모드의 기존 렌더 텍스처를 읽어 실제로 옛 기둥이 빠졌는지 확인합니다.
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        var texture=typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage) as RenderTexture;
        if(texture != null)
        {
            var previous=RenderTexture.active; var capture=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try { RenderTexture.active=texture; capture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); capture.Apply(); File.WriteAllBytes(directory+"/preview.png",capture.EncodeToPNG()); }
            finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(capture); }
        }
    }

    /// <summary>감시탑과 난간 레이어의 외곽 기둥 표시 범위만 전환합니다.</summary>
    /// <param name="stage">현재 상점 렌더러입니다.</param>
    /// <param name="hide">2단계에서 옛 기둥을 숨길지 여부입니다.</param>
    private static void SetLegacyPostVisibility(DystopiaPixelStage stage,bool hide)
    {
        foreach(var layer in stage.layers)
        {
            if(!(layer.source is UnityEngine.UI.Image image) || image.sprite == null) continue;
            string path=AssetDatabase.GetAssetPath(image.sprite);
            if(path==Root+"Art/LeftWatchTower.png") layer.textureEdgeTrim=hide ? new Vector2(108f/1672,0) : Vector2.zero;
            else if(path==Root+"Art/RightWatchTower.png") layer.textureEdgeTrim=hide ? new Vector2(0,98f/1672) : Vector2.zero;
        }
    }

    /// <summary>고정 상품 ID 순서에 대응하는 제공 원본의 프로젝트 파일명입니다.</summary>
    private static readonly string[] SurvivalProductArt = { "DrinkingWater","CannedFood","MedicalBandage","DryBattery","MilitaryRation","NutritionBar","Medicine","EmergencyInjection","Flashlight","FoldingShovel","Radio","PowerBattery","GasMask","ProtectiveSuit","RadiationDetector","ThermalCamera" };

    /// <summary>교체한 네 상품의 Sprite ID를 유지하며 새 이미지의 실제 영역으로 갱신합니다.</summary>
    [MenuItem("Dystopia/Refresh Replaced Protection Products")]
    public static void RefreshReplacedProtectionProducts()
    {
        foreach(string name in new[] { "ProtectiveSuit","RadiationDetector","GasMask","ThermalCamera" })
        {
            string path=Root+"Art/Products/"+name+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var factory=new SpriteDataProviderFactories(); factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var rects=provider.GetSpriteRects();
            if(rects.Length != 1) throw new InvalidOperationException("Expected one existing product sprite: "+path);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(path))) throw new IOException(path);
                var pixels=texture.GetPixels32();
                int minX=texture.width,minY=texture.height,maxX=-1,maxY=-1;
                for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
                    if(pixels[y*texture.width+x].a > 0) { minX=Math.Min(minX,x); minY=Math.Min(minY,y); maxX=Math.Max(maxX,x); maxY=Math.Max(maxY,y); }
                if(maxX < minX) throw new InvalidOperationException("Empty product artwork: "+path);
                rects[0].rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
                provider.SetSpriteRects(rects); provider.Apply(); importer.SaveAndReimport();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        Debug.Log("Four replacement product sprites refreshed; existing GUIDs and sprite IDs preserved.");
    }

    /// <summary>상자와 시계를 유지하며 크기 조정 전 2단계 프레임으로 복원합니다.</summary>
    [MenuItem("Dystopia/Restore Previous Stage 2 Frame")]
    public static void RestorePreviousStage2Frame()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string[] names={ "Stage2Counter","Stage2Ceiling","Stage2LeftPillar","Stage2RightPillar" };
        var images=names.Select(name=>stage.layers.Select(l=>l.source as UnityEngine.UI.Image).Single(image=>image != null && image.sprite != null && image.sprite.name==name)).ToArray();
        string directory="output/shop-stage-switch/stage2-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        for(int i=0;i<images.Length;i++)
        {
            var rect=images[i].rectTransform;
            Undo.RecordObject(rect,"Restore stage 2 frame");
            var position=rect.anchoredPosition; var size=rect.sizeDelta;
            // 직전 변경에서 수정한 네 부품의 세로 값만 복원합니다.
            if(i==0) size.y=249.43677f;
            else if(i==1) { position.y=51.325226f; size.y=125.48353f; }
            else { position.y=i==2 ? -74.99997f : -75; size.y=345.0797f; }
            rect.anchoredPosition=position; rect.sizeDelta=size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not verify frame fit.");
        Debug.Log("Previous stage 2 frame restored; counter top, box and clock preserved. "+directory);
    }

    /// <summary>2단계 상점을 네 부품으로 나눠 승인된 3단계 부품 배치에 적용합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 2 Shop")]
    public static void ApplyStage2Shop()
    {
        ApplyApprovedStageReference(2);
    }

    /// <summary>指定段階の承認済みシーンから店舗と背景の設定を適用します。</summary>
    /// <param name="stageNumber">保存済みの段階番号です。</param>
    private static void ApplyApprovedStageReference(int stageNumber)
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string directory="output/shop-stage-switch/stage"+stageNumber+"-approved-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        var preview=EditorSceneManager.OpenPreviewScene(Root+"Editor/References/Stage"+stageNumber+"Reference.unity");
        try
        {
            var saved=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DystopiaPixelStage>(true)).Single();
            var names=new[] { "Counter","Stage3Counter","Canopy","Stage3Ceiling","Stage3LeftPillar","Stage3RightPillar","Stage3CeilingLamp","FrontContainer","CounterClock","FarBackground","DawnBackground","EveningBackground","SunsetBackground","CityLights","MidBackground","LeftChimneySmoke","RightChimneySmoke" };
            // 승인된 참조에서 대상 레이어만 복사하며 현재 씬과 게임 상태는 교체하지 않습니다.
            var pairs=stage.layers.Where(l=>l.source!=null && names.Contains(l.source.name)).Select(l=>new { target=l, reference=saved.layers.Single(s=>s.source!=null && s.source.name==l.source.name) }).ToArray();
            Undo.RecordObject(stage,"Apply approved stage 2");
            foreach(var pair in pairs)
            {
                var image=pair.target.source as UnityEngine.UI.Image;
                var reference=pair.reference.source as UnityEngine.UI.Image;
                if(image==null || reference==null) throw new InvalidOperationException("Expected Image layer.");
                CopyStageReferenceRect(image.rectTransform,reference.rectTransform);
                Undo.RecordObject(image,"Apply approved stage 2 sprite");
                Undo.RecordObject(image.gameObject,"Apply stage visibility"); image.gameObject.SetActive(reference.gameObject.activeSelf); image.sprite=reference.sprite; image.color=reference.color; image.preserveAspect=reference.preserveAspect; image.enabled=reference.enabled;
                foreach(var field in typeof(DystopiaPixelStage.Layer).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                    if(field.Name!="source" && !field.IsNotSerialized) field.SetValue(pair.target,field.GetValue(pair.reference));
                EditorUtility.SetDirty(image);
                if(image.name=="CounterClock")
                {
                    var digits=image.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    var savedDigits=reference.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    if(digits!=null && savedDigits!=null)
                    {
                        CopyStageReferenceRect(digits.rectTransform,savedDigits.rectTransform);
                        Undo.RecordObject(digits,"Apply stage 2 clock digits");
                        digits.fontSize=savedDigits.fontSize; digits.color=savedDigits.color; digits.alignment=savedDigits.alignment;
                        EditorUtility.SetDirty(digits);
                    }
                }
            }
            // 참조에 없는 별도 하부장이 다른 단계에 남지 않도록 표시 상태를 함께 복원합니다.
            var cabinet=stage.layers.FirstOrDefault(l=>l.source!=null && l.source.name=="Stage2Cabinet")?.source;
            if(cabinet!=null)
            {
                var savedCabinet=saved.layers.FirstOrDefault(l=>l.source!=null && l.source.name=="Stage2Cabinet")?.source;
                Undo.RecordObject(cabinet,"Apply stage cabinet visibility");
                cabinet.enabled=savedCabinet!=null && savedCabinet.enabled && savedCabinet.gameObject.activeSelf;
                EditorUtility.SetDirty(cabinet);
            }
            if(stageNumber==3)
            {
                SetStage3Container(stage.layers.Single(l=>l.source!=null && l.source.name=="FrontContainer"));
                // 분리 제작한 Stage 2 하부장은 Stage 3 원본 하부장을 가리지 않아야 합니다.
                var stage2Cabinet=stage.layers.FirstOrDefault(l=>l.source!=null && l.source.name=="Stage2Cabinet")?.source;
                if(stage2Cabinet!=null)
                {
                    Undo.RecordObject(stage2Cabinet,"Hide stage 2 cabinet in stage 3");
                    stage2Cabinet.enabled=false;
                    EditorUtility.SetDirty(stage2Cabinet);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(stage2Cabinet);
                }
                // Stage 3의 옛 원본 크기 대신 승인된 철제 가게의 배경 여백을 사용합니다.
                var backgroundPreview=EditorSceneManager.OpenPreviewScene(Root+"Editor/References/Stage2Reference.unity");
                try
                {
                    var backgroundStage=backgroundPreview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DystopiaPixelStage>(true)).Single();
                    var backgroundNames=new[] { "FarBackground","DawnBackground","EveningBackground","SunsetBackground","CityLights","MidBackground" };
                    foreach(string name in backgroundNames)
                        CopyStageReferenceRect(stage.layers.Single(l=>l.source!=null && l.source.name==name).source.rectTransform,backgroundStage.layers.Single(l=>l.source!=null && l.source.name==name).source.rectTransform);
                }
                finally { EditorSceneManager.ClosePreviewScene(backgroundPreview); }
            }
            stage.ceilingLamp=stage.layers.FirstOrDefault(l=>l.source!=null && l.source.name=="Stage3CeilingLamp")?.source as UnityEngine.UI.Image;
            stage.lampPosition=saved.lampPosition; stage.lampHeight=saved.lampHeight; stage.lampRadius=saved.lampRadius; stage.lampIntensity=saved.lampIntensity; stage.lampColor=saved.lampColor;
            SetProductShopStage(stageNumber);
            EditorUtility.SetDirty(stage);
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification copy failed.");
            Debug.Log("Approved Stage 2 applied including box, clock and backgrounds: "+directory);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    /// <summary>사용자가 승인한 참조의 배치만 명시적 Stage 적용 시 복사합니다.</summary>
    /// <param name="target">현재 씬의 대상입니다.</param>
    /// <param name="reference">저장된 기준 배치입니다.</param>
    private static void CopyStageReferenceRect(RectTransform target,RectTransform reference)
    {
        Undo.RecordObject(target,"Apply stage reference layout");
        target.anchorMin=reference.anchorMin; target.anchorMax=reference.anchorMax; target.pivot=reference.pivot;
        target.sizeDelta=reference.sizeDelta; target.anchoredPosition3D=reference.anchoredPosition3D;
        target.localScale=reference.localScale; target.localRotation=reference.localRotation;
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    /// <summary>작업 씬을 변경하지 않고 16종의 자동 생성 충돌 외곽선을 검증합니다.</summary>
    [MenuItem("Dystopia/Validate Survival Product Colliders")]
    public static void ValidateSurvivalProductColliders()
    {
        var preview=EditorSceneManager.NewPreviewScene();
        var report=new System.Text.StringBuilder();
        try
        {
            foreach(string name in SurvivalProductArt)
            {
                var sprite=AssetDatabase.LoadAllAssetsAtPath(Root+"Art/Products/"+name+".png").OfType<Sprite>().Single();
                var instance=new GameObject(name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance,preview);
                instance.AddComponent<SpriteRenderer>().sprite=sprite;
                var collider=instance.AddComponent<PolygonCollider2D>();
                if(collider.pathCount == 0 || collider.GetTotalPointCount() < 3) throw new InvalidOperationException("Missing collision outline: "+name);
                Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
                for(int path=0;path<collider.pathCount;path++) foreach(var point in collider.GetPath(path))
                { min=Vector2.Min(min,point); max=Vector2.Max(max,point); }
                report.AppendLine(name+": paths="+collider.pathCount+", points="+collider.GetTotalPointCount()+", sprite="+sprite.bounds.size+", collision="+(max-min));
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Directory.CreateDirectory("output/survival-products");
        File.WriteAllText("output/survival-products/collider-validation.txt",report.ToString());
        Debug.Log("16 product polygon outlines verified in a temporary preview scene. Play Mode drag not tested.");
    }

    /// <summary>씬을 변경하지 않고 카탈로그 참조와 설비별 주문 제외를 실제 세션으로 검증합니다.</summary>
    [MenuItem("Dystopia/Validate Survival Product Unlocks")]
    public static void ValidateSurvivalProductUnlocks()
    {
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>(FindObjectsInactive.Include);
        if(screen == null || screen.gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var settings=JsonUtility.FromJson<DystopiaSettings>(JsonUtility.ToJson(screen.Settings));
        if(settings.products.Length != 16 || settings.products.Select(p => p.id).Distinct().Count() != 16) throw new InvalidOperationException("Expected 16 unique products.");
        // 가격 밸런스와 분리해 검증용 복사본에만 양수 가격을 지정합니다.
        for(int i=0;i<16;i++)
        {
            var product=settings.products[i];
            if(product.id != (DystopiaProductId)(i+1) || product.sprite == null || AssetDatabase.GetAssetPath(product.sprite) != Root+"Art/Products/"+SurvivalProductArt[i]+".png") throw new InvalidOperationException("Wrong product sprite: "+product.name);
            product.price=100;
        }
        int scenarios=0;
        for(int stage=0;stage<=3;stage++) for(int mask=0;mask<64;mask++)
        {
            settings.shopStage=stage; settings.ownedFacilities=(DystopiaFacility)mask;
            var expected=new HashSet<DystopiaProductId> { DystopiaProductId.Water,DystopiaProductId.CannedFood,DystopiaProductId.Bandage,DystopiaProductId.DryBattery };
            for(int facility=0;facility<6;facility++)
                if(stage >= facility/2+1 && (mask & (1<<facility)) != 0)
                { expected.Add((DystopiaProductId)(5+facility*2)); expected.Add((DystopiaProductId)(6+facility*2)); }
            var session=new DystopiaSession(settings,scenarios++);
            var active=(IReadOnlyList<DystopiaProduct>)typeof(DystopiaSession).GetProperty("ActiveProducts",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(session);
            if(!expected.SetEquals(active.Select(p => p.id))) throw new InvalidOperationException("Unlock mismatch: "+stage+"/"+mask);
            foreach(var customer in session.WaitingCustomers.Concat(new[] { session.Customer }))
                if(customer.basket.Any(line => !expected.Contains(line.product.id) || line.product.sprite == null)) throw new InvalidOperationException("Locked or imageless product in order.");
        }
        settings.products[0].price=0;
        var unpricedSession=new DystopiaSession(settings,99);
        if(unpricedSession.WaitingCustomers.Concat(new[] { unpricedSession.Customer }).Any(c => c.basket.Any(l => l.product.price <= 0))) throw new InvalidOperationException("Unpriced product in order.");
        Directory.CreateDirectory("output/survival-products");
        File.WriteAllText("output/survival-products/validation.txt","16 unique sprite bindings; 256 stage/facility combinations; generated customer orders exclude locked and unpriced products. No scene mutation.\n");
        Debug.Log("Survival product validation passed: "+scenarios+" stage/facility combinations. Play Mode visuals not tested.");
    }

    /// <summary>외형 단계 적용 시 상품 해금의 단계 조건만 갱신하며 보유 설비는 유지합니다.</summary>
    /// <param name="stage">적용할 가게 단계입니다.</param>
    private static void SetProductShopStage(int stage)
    {
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>(FindObjectsInactive.Include);
        if(screen == null) return;
        Undo.RecordObject(screen,"Set product shop stage");
        screen.Settings.shopStage=stage;
        PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        EditorUtility.SetDirty(screen);
    }

    /// <summary>16종 원본을 Sprite로 가져오고 현재 Scene의 상품 설정만 교체합니다.</summary>
    [MenuItem("Dystopia/Register 16 Survival Products")]
    public static void RegisterSurvivalProducts()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>(FindObjectsInactive.Include);
        if(screen == null || screen.gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity")
            throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string directory="output/survival-products/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(screen.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        var catalog=new DystopiaSettings().products;
        for(int i=0;i<catalog.Length;i++)
        {
            string path=Root+"Art/Products/"+SurvivalProductArt[i]+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=100; importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false; importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048; importer.alphaIsTransparency=true; importer.sRGBTexture=true;
            importer.SaveAndReimport();
            // 원본 PNG는 보존하고 투명 여백만 Sprite 영역에서 제외합니다.
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            Rect crop;
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(path))) throw new IOException(path);
                var pixels=texture.GetPixels32();
                int minX=texture.width,minY=texture.height,maxX=-1,maxY=-1;
                for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
                    if(pixels[y*texture.width+x].a > 0) { minX=Math.Min(minX,x); minY=Math.Min(minY,y); maxX=Math.Max(maxX,x); maxY=Math.Max(maxY,y); }
                if(maxX < minX) throw new InvalidOperationException("Empty product artwork: "+path);
                crop=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            var factory=new SpriteDataProviderFactories(); factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous=provider.GetSpriteRects().FirstOrDefault();
            var spriteRect=new SpriteRect { name=SurvivalProductArt[i],rect=crop,pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=previous != null ? previous.spriteID : GUID.Generate() };
            provider.SetSpriteRects(new[] { spriteRect });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(spriteRect.name,spriteRect.spriteID) });
            provider.Apply(); importer.SaveAndReimport();
            catalog[i].sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
            var existing=screen.Settings.products.FirstOrDefault(p => p != null && (p.id == catalog[i].id || p.name == catalog[i].name));
            if(existing != null) catalog[i].price=existing.price;
        }
        var owners=new List<UnityEngine.Object> { screen };
        owners.AddRange(UnityEngine.Object.FindObjectsByType<DystopiaTopDownTest>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p => p.gameObject.scene == screen.gameObject.scene));
        foreach(var owner in owners)
        {
            var serialized=new SerializedObject(owner);
            var products=serialized.FindProperty("settings").FindPropertyRelative("products");
            products.arraySize=catalog.Length;
            for(int i=0;i<catalog.Length;i++)
            {
                var entry=products.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").intValue=(int)catalog[i].id;
                entry.FindPropertyRelative("name").stringValue=catalog[i].name;
                entry.FindPropertyRelative("price").intValue=catalog[i].price;
                entry.FindPropertyRelative("firstDay").intValue=1;
                entry.FindPropertyRelative("requiredFacility").intValue=(int)catalog[i].requiredFacility;
                entry.FindPropertyRelative("sprite").objectReferenceValue=catalog[i].sprite;
            }
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            EditorUtility.SetDirty(owner);
        }
        EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
        if(!EditorSceneManager.SaveScene(screen.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not verify catalog.");
        Debug.Log("16 survival products registered. Unpriced products stay out of orders. Scene left unsaved. "+directory);
    }
    /// <summary>사용자 제공 일일지침의 바깥 체크무늬를 제거하고 기존 문서의 본문 배치만 맞춥니다.</summary>
    [MenuItem("Dystopia/Apply New Daily Instruction")]
    public static void ApplyNewDailyInstruction()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before changing document layout.");
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>(FindObjectsInactive.Include);
        if(screen==null) throw new InvalidOperationException("Open the Dystopia scene first.");
        var sheet=screen.transform.Find("DystopiaCanvas/DailyInstruction/Sheet").GetComponent<UnityEngine.UI.Image>();
        var content=(RectTransform)sheet.transform.Find("PrintedContent");
        const string source="output/instruction-update/DailyInstruction-source.png";
        const string target="Assets/DystopiaPrototype/Art/DailyInstruction.png";
        if(!File.Exists(source)) throw new FileNotFoundException("Daily instruction source is missing.",source);
        string backup="output/instruction-update/Live-before-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
        if(!EditorSceneManager.SaveScene(screen.gameObject.scene,backup,true)) throw new IOException("Could not back up live scene.");
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(source))) throw new IOException("Could not read daily instruction image.");
            Color32[] pixels=texture.GetPixels32();
            var queue=new System.Collections.Generic.Queue<int>();
            var visited=new bool[pixels.Length];
            int width=texture.width, height=texture.height;
            for(int x=0;x<width;x++) { queue.Enqueue(x); queue.Enqueue((height-1)*width+x); }
            for(int y=0;y<height;y++) { queue.Enqueue(y*width); queue.Enqueue(y*width+width-1); }
            // 화면 가장자리와 연결된 무채색 체크무늬만 투명화합니다. 종이 안의 흰 글자는 보존합니다.
            while(queue.Count>0)
            {
                int index=queue.Dequeue(); if(visited[index]) continue; visited[index]=true;
                Color32 p=pixels[index]; int max=Mathf.Max(p.r,Mathf.Max(p.g,p.b)),min=Mathf.Min(p.r,Mathf.Min(p.g,p.b));
                if(min<95 || max-min>22) continue;
                p.a=0; pixels[index]=p;
                int x=index%width,y=index/width;
                if(x>0) queue.Enqueue(index-1); if(x<width-1) queue.Enqueue(index+1);
                if(y>0) queue.Enqueue(index-width); if(y<height-1) queue.Enqueue(index+width);
            }
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(target,texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(target);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048; importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(target);
        Undo.RecordObject(sheet,"Replace daily instruction paper"); sheet.sprite=sprite;
        var data=new SerializedObject(screen); data.FindProperty("dailyInstruction").objectReferenceValue=sprite; data.ApplyModifiedProperties();
        SetInstructionRect(content,"DayPaper",140,51,51,9,0);
        var paper=content.Find("DayPaper").GetComponent<UnityEngine.UI.Image>();
        Undo.RecordObject(paper,"Match date paper"); paper.color=new Color(.85f,.83f,.79f);
        SetInstructionRect(content,"InstructionDay",140,50,51,12,8);
        SetInstructionRect(content,"MemoryHeading",32,76,160,16,9);
        SetInstructionRect(content,"RuleTitle",32,99,160,12,8);
        SetInstructionRect(content,"Rule",32,114,160,30,8);
        int count=content.Cast<Transform>().Count(x=>x.name.StartsWith("InstructionProduct",StringComparison.Ordinal));
        for(int i=0;i<count;i++)
        {
            float x=32+(i%2)*83, y=154+(i/2)*Mathf.Min(26,58f/Mathf.Max(1,(count+1)/2));
            SetInstructionRect(content,"InstructionProduct"+i,x,y,18,21,0);
            SetInstructionRect(content,"InstructionName"+i,x+22,y,57,10,7);
            SetInstructionRect(content,"InstructionPrice"+i,x+22,y+10,57,11,8);
        }
        foreach(var obj in new UnityEngine.Object[]{screen,sheet,paper})
        { EditorUtility.SetDirty(obj); PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
        EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
        Debug.Log("New daily instruction connected; original scene preserved at "+backup+". Scene not automatically saved.");
    }

    /// <summary>교체 요청된 문서 안의 기존 텍스트·그림 위치만 갱신합니다.</summary>
    /// <param name="content">문서의 224×280 기준 내용 영역입니다.</param>
    /// <param name="name">기존 문서 요소입니다.</param>
    /// <param name="x">왼쪽 좌표입니다.</param>
    /// <param name="y">위쪽 좌표입니다.</param>
    /// <param name="width">표시 너비입니다.</param>
    /// <param name="height">표시 높이입니다.</param>
    /// <param name="fontSize">텍스트 크기이며 0이면 변경하지 않습니다.</param>
    private static void SetInstructionRect(RectTransform content,string name,float x,float y,float width,float height,int fontSize)
    {
        var rect=content.Find(name) as RectTransform; if(rect==null) return;
        Undo.RecordObject(rect,"Fit new instruction text"); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(width,height);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        var label=rect.GetComponent<UnityEngine.UI.Text>();
        if(label!=null && fontSize>0)
        { Undo.RecordObject(label,"Fit instruction font"); label.fontSize=fontSize; PrefabUtility.RecordPrefabInstancePropertyModifications(label); }
    }

    /// <summary>손 시트와 교체 이미지만 import합니다. 씬 검색·수정·저장 또는 배치 적용은 수행하지 않습니다.</summary>
    [MenuItem("Dystopia/Assets/Import Pending Artwork Only")]
    public static void ImportPendingArtworkOnly()
    {
        const string handsPath = "Assets/DystopiaPrototype/Art/Hands.png";
        const string boxPath = "Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerMale.png";
        const string normalPath = "Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerNormal.png";
        const string instructionPath = "Assets/DystopiaPrototype/Art/DailyInstruction.png";
        foreach (string path in new[] { handsPath, boxPath, instructionPath })
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = path == handsPath ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        var handImporter = (TextureImporter)AssetImporter.GetAtPath(handsPath);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(handImporter);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects();
        var rects = new SpriteRect[4];
        for (int i = 0; i < rects.Length; i++)
        {
            string name = "Hand" + (i + 1);
            var existing = previous.FirstOrDefault(rect => rect.name == name);
            rects[i] = new SpriteRect { name = name, rect = new Rect(i % 2 * 64, i < 2 ? 64 : 0, 64, 64),
                pivot = new Vector2(.5f,.5f), alignment = SpriteAlignment.Center,
                spriteID = existing != null ? existing.spriteID : GUID.Generate() };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name,rect.spriteID)));
        provider.Apply(); handImporter.SaveAndReimport();

        // 원본 픽셀과 UV가 정확히 일치하는 약한 요철 데이터입니다. 확산색 이미지는 수정하지 않습니다.
        var source = new Texture2D(2,2,TextureFormat.RGBA32,false);
        var normal = new Texture2D(2,2,TextureFormat.RGB24,false,true);
        try
        {
            if (!source.LoadImage(File.ReadAllBytes(boxPath))) throw new InvalidOperationException("상자 PNG를 읽을 수 없습니다.");
            int width = source.width, height = source.height;
            var pixels = source.GetPixels();
            var heights = new float[pixels.Length];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float sum = 0, weight = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = Mathf.Clamp(x+dx,0,width-1), py = Mathf.Clamp(y+dy,0,height-1);
                    Color pixel = pixels[py*width+px];
                    float w = (dx == 0 ? 2 : 1) * (dy == 0 ? 2 : 1) * pixel.a;
                    sum += pixel.grayscale * w; weight += w;
                }
                heights[y*width+x] = weight > 0 ? sum / weight : 0;
            }
            var normals = new Color[pixels.Length];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y*width+x;
                int left = y*width+Mathf.Max(0,x-1), right = y*width+Mathf.Min(width-1,x+1);
                int down = Mathf.Max(0,y-1)*width+x, up = Mathf.Min(height-1,y+1)*width+x;
                // 투명 경계에서 과도한 테두리 노멀이 생기지 않도록 표면 내부만 미세하게 기울입니다.
                bool interior = pixels[index].a > .5f && pixels[left].a > .5f && pixels[right].a > .5f && pixels[down].a > .5f && pixels[up].a > .5f;
                Vector3 direction = interior ? new Vector3((heights[left]-heights[right])*.65f,(heights[down]-heights[up])*.65f,1).normalized : Vector3.forward;
                normals[index] = new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1);
            }
            normal.Reinitialize(width,height,TextureFormat.RGB24,false);
            normal.SetPixels(normals); normal.Apply();
            File.WriteAllBytes(normalPath,normal.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(normal); }
        AssetDatabase.ImportAsset(normalPath,ImportAssetOptions.ForceSynchronousImport);
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        // 기존 셰이더는 RGB를 직접 복호화하므로 Unity 압축 노멀 형식으로 바꾸지 않습니다.
        normalImporter.textureType = TextureImporterType.Default;
        normalImporter.sRGBTexture = false;
        normalImporter.filterMode = FilterMode.Point;
        normalImporter.mipmapEnabled = false;
        normalImporter.textureCompression = TextureImporterCompression.Uncompressed;
        normalImporter.npotScale = TextureImporterNPOTScale.None;
        normalImporter.SaveAndReimport();
        Debug.Log("Assets only: Hand1–4 sliced; instruction/chest imported; UV-aligned subtle RGB normal generated. No scene objects or layout modified.");
    }

    /// <summary>사용자 말풍선을 꼬리까지 보존하는 9-slice Sprite로 가져와 정산 화면에 연결합니다.</summary>
    [MenuItem("Dystopia/Apply Ledger Speech Bubble")]
    public static void ApplyLedgerSpeechBubble()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var screen = UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>();
        if (screen == null) throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        const string path = "Assets/DystopiaPrototype/Art/LedgerSpeechBubble.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true; importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=128;
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().FirstOrDefault();
        var spriteRect = new SpriteRect { name="LedgerSpeechBubble",rect=new Rect(54,34,2069,597),
            border=new Vector4(300,180,200,120),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,
            spriteID=previous != null ? previous.spriteID : GUID.Generate() };
        provider.SetSpriteRects(new[] { spriteRect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(spriteRect.name,spriteRect.spriteID) });
        provider.Apply(); importer.SaveAndReimport();
        var data = new SerializedObject(screen);
        data.FindProperty("ledgerSpeechBubble").objectReferenceValue=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
    }

    /// <summary>메뉴를 직접 실행할 때만 2026-09-13에 확정한 3단계 가게의 배치와 표면·전등 설정을 적용합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 3 Shop")]
    public static void ApplyStage3Shop()
    {
        ApplyApprovedStageReference(3);
    }

    /// <summary>현재 편집 상태를 별도 백업한 뒤 bd48cb5의 천막 가게 배치와 가게 전등을 적용합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 1 Shop")]
    public static void ApplyStage1Shop()
    {
        ApplyApprovedStageReference(1);
    }

    /// <summary>제공된 1단계 그림의 투명 여백을 제외하고 가게 겹침 순서와 상자 접지 배치를 적용합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 1 Artwork")]
    public static void ApplyStage1Artwork()
    {
        ApplyStage1Artwork(false);
    }

    /// <summary>새 1단계 상자만 교체하고 손님 중심에 정렬합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 1 Box")]
    public static void ApplyStage1Box()
    {
        ApplyStage1Artwork(true);
    }

    /// <summary>승인된 전체 그림 또는 상자만 적용하고 변경 전후 상태를 보존합니다.</summary>
    /// <param name="boxOnly">상자 이외의 배치와 그림을 유지할지 여부입니다.</param>
    private static void ApplyStage1Artwork(bool boxOnly)
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity")
            throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string directory="output/shop-stage-switch/art-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        string[] names={ "Counter","FrontContainer","CounterClock" };
        string[] paths={ "Assets/DystopiaPrototype/Art/Stage1WoodCounter.png","Assets/DystopiaPrototype/TopDownTest/Art/Stage1WoodContainerCompact.png","Assets/DystopiaPrototype/Art/Stage1BasicClock.png" };
        Undo.RecordObject(stage,"Apply stage 1 artwork");
        for(int i=0;i<paths.Length;i++)
        {
            if(boxOnly && i != 1) continue;
            AssetDatabase.ImportAsset(paths[i]);
            var importer=(TextureImporter)AssetImporter.GetAtPath(paths[i]);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048; importer.alphaIsTransparency=true; importer.SaveAndReimport();
            if(i > 0)
            {
                // PNG는 보존하고 Sprite 영역만 실제 그림에 맞춰 작은 소품의 픽셀 손실을 줄입니다.
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                Rect crop;
                try
                {
                    if(!texture.LoadImage(File.ReadAllBytes(paths[i]))) throw new IOException("Could not read prop PNG.");
                    var pixels=texture.GetPixels32();
                    int minX=texture.width,minY=texture.height,maxX=-1,maxY=-1;
                    for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
                        if(pixels[y*texture.width+x].a > 0) { minX=Math.Min(minX,x); minY=Math.Min(minY,y); maxX=Math.Max(maxX,x); maxY=Math.Max(maxY,y); }
                    if(maxX < minX) throw new InvalidOperationException("Prop PNG is empty.");
                    crop=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                importer.spriteImportMode=SpriteImportMode.Multiple; importer.SaveAndReimport();
                var factory=new SpriteDataProviderFactories(); factory.Init();
                var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var previous=provider.GetSpriteRects().FirstOrDefault();
                var spriteRect=new SpriteRect { name=Path.GetFileNameWithoutExtension(paths[i]),rect=crop,pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=previous != null ? previous.spriteID : GUID.Generate() };
                provider.SetSpriteRects(new[] { spriteRect });
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(spriteRect.name,spriteRect.spriteID) });
                provider.Apply(); importer.SaveAndReimport();
            }
            var layer=stage.layers.Single(x => x.source != null && (x.source.name == names[i] || i == 0 && x.source.name == "Stage3Counter"));
            var image=(UnityEngine.UI.Image)layer.source;
            Undo.RecordObject(image,"Replace stage 1 image");
            image.sprite=AssetDatabase.LoadAllAssetsAtPath(paths[i]).OfType<Sprite>().Single();
            if(i > 0) { image.color=Color.white; image.preserveAspect=true; }
            if(!boxOnly && i > 0)
            {
                // 현재 승인된 접지 그림자를 1단계 전체 적용 시 함께 복원합니다.
                layer.bottomShade=.5f;
                layer.contactShadow=i == 1
                    ? new Vector4(.5f,.056f,1.9f,1.3f)
                    : new Vector4(.5f,.1851852f,1.9f,1.5f);
            }
            if(i == 1) { layer.rimWidthPixels=1; layer.rimResponse=.08f; }
            if(i == 1 && boxOnly)
            {
                Undo.RecordObject(image.rectTransform,"Place stage 1 box on tabletop");
                // 서로 다른 Canvas 부모의 로컬 좌표를 직접 비교하지 않고 손님 중심을 변환합니다.
                var customer=stage.layers.Single(x => x.source != null && x.source.name == "Customer").source.rectTransform;
                var rect=image.rectTransform;
                var parent=rect.parent;
                float customerX=parent.InverseTransformPoint(customer.TransformPoint(customer.rect.center)).x;
                float drawnWidth=Mathf.Min(rect.rect.width,rect.rect.height*image.sprite.rect.width/image.sprite.rect.height);
                float boxX=parent.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.x+drawnWidth*.5f,rect.rect.center.y,0))).x;
                rect.anchoredPosition+=new Vector2(customerX-boxX,0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(image.rectTransform);
            }
            // 각 1단계 그림의 원본 UV에 맞춰 생성한 전용 노멀맵을 연결합니다.
            layer.normalMap=i == 0 ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i].Replace(".png","Normal.png"));
            layer.normalSprite=layer.normalMap != null ? image.sprite : null;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
        // PixelStage는 Hierarchy가 아닌 layers 순서로 그리므로 캐노피를 테이블 뒤로 옮깁니다.
        int tableIndex=Array.FindIndex(stage.layers,x => x.source != null && (x.source.name == "Counter" || x.source.name == "Stage3Counter"));
        int canopyIndex=Array.FindIndex(stage.layers,x => x.source != null && (x.source.name == "Canopy" || x.source.name == "Stage3Ceiling"));
        if(!boxOnly && canopyIndex > tableIndex)
        {
            var canopy=stage.layers[canopyIndex];
            stage.layers[canopyIndex]=stage.layers[tableIndex]; stage.layers[tableIndex]=canopy;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorUtility.SetDirty(stage); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        // 단계 재적용은 사용자가 맞춘 시계 숫자의 위치와 크기도 유지합니다.
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not save verification copy.");
        Debug.Log("Three supplied stage 1 images applied; layout preserved. Scene left unsaved. "+directory);
    }

    /// <summary>저해상도 1단계 상자의 과도한 외곽광 폭과 강도만 줄입니다.</summary>
    [MenuItem("Dystopia/Soften Stage 1 Box Rim")]
    public static void SoftenStage1BoxRim()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var layer=stage.layers.Single(x => x.source != null && x.source.name == "FrontContainer");
        if(AssetDatabase.GetAssetPath(((UnityEngine.UI.Image)layer.source).sprite) != "Assets/DystopiaPrototype/TopDownTest/Art/Stage1WoodContainerCompact.png") throw new InvalidOperationException("Apply stage 1 box first.");
        string directory="output/shop-stage-switch/box-rim-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        Undo.RecordObject(stage,"Soften stage 1 box rim");
        layer.rimWidthPixels=1; layer.rimResponse=.08f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorUtility.SetDirty(stage); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not save rim verification.");
        Debug.Log("Stage 1 box rim reduced; scene left unsaved.");
    }

    /// <summary>상자·시계의 밑면 음영과 상판에 퍼지는 접촉 그림자만 강화합니다.</summary>
    [MenuItem("Dystopia/Strengthen Stage 1 Contact Shadows")]
    public static void StrengthenStage1ContactShadows()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var box=stage.layers.Single(x => x.source != null && x.source.name == "FrontContainer");
        var clock=stage.layers.Single(x => x.source != null && x.source.name == "CounterClock");
        string directory="output/shop-stage-switch/contact-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        Undo.RecordObject(stage,"Strengthen box and clock contact shadows");
        // 接地位置は既存の実画像の底面測定に任せ、横幅と下方への広がりだけ調整します。
        box.contactShadow=new Vector4(box.contactShadow.x,box.contactShadow.y,1.9f,1.3f);
        clock.contactShadow=new Vector4(clock.contactShadow.x,clock.contactShadow.y,1.9f,1.5f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorUtility.SetDirty(stage); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not save contact shadow verification.");
        Debug.Log("Box and clock contact shadows strengthened; scene left unsaved.");
    }

    /// <summary>현재 상자·시계에 전용 RGB 노멀맵만 연결하며 배치·색상·조명 강도를 유지합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 1 Prop Normals")]
    public static void ApplyStage1PropNormals()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        string[] names={ "FrontContainer","CounterClock" };
        string[] paths={ "Assets/DystopiaPrototype/TopDownTest/Art/Stage1WoodContainerCompact.png","Assets/DystopiaPrototype/Art/Stage1BasicClock.png" };
        var layers=names.Select(name => stage.layers.Single(x => x.source != null && x.source.name == name)).ToArray();
        for(int i=0;i<2;i++)
            if(AssetDatabase.GetAssetPath(((UnityEngine.UI.Image)layers[i].source).sprite) != paths[i]) throw new InvalidOperationException("Apply current stage 1 artwork first.");
        string directory="output/shop-stage-switch/normals-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up scene.");
        Undo.RecordObject(stage,"Connect stage 1 prop normals");
        for(int i=0;i<2;i++)
        {
            string path=paths[i].Replace(".png","Normal.png");
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            // PixelStage 셰이더는 압축된 Unity NormalMap 대신 선형 RGB를 직접 해석합니다.
            importer.textureType=TextureImporterType.Default; importer.sRGBTexture=false;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048; importer.SaveAndReimport();
            layers[i].normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            layers[i].normalSprite=((UnityEngine.UI.Image)layers[i].source).sprite;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorUtility.SetDirty(stage); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not save normal-map verification.");
        Debug.Log("Stage 1 box and clock normals connected. Scene left unsaved. "+directory);
    }

    /// <summary>현재 시계 그림의 실제 표시창에 숫자 크기와 중심만 맞춥니다.</summary>
    [MenuItem("Dystopia/Fit Current Clock Display")]
    [MenuItem("Dystopia/Fit Stage 1 Clock Display")]
    public static void FitStage1ClockDisplay()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage == null || stage.gameObject.scene.path != "Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity")
            throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var image=(UnityEngine.UI.Image)stage.layers.Single(x => x.source != null && x.source.name == "CounterClock").source;
        string spritePath=AssetDatabase.GetAssetPath(image.sprite);
        bool stage3=spritePath == "Assets/DystopiaPrototype/Art/시계.png";
        if(image.sprite == null || !stage3 && spritePath != "Assets/DystopiaPrototype/Art/Stage1BasicClock.png")
            throw new InvalidOperationException("Unsupported clock artwork.");
        var text=image.transform.Find("BusinessClock").GetComponent<UnityEngine.UI.Text>();
        var rect=text.rectTransform;
        string directory="output/shop-stage-switch/clock-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Could not back up clock.");
        // Image.preserveAspect는 부모 pivot을 기준으로 그림을 배치합니다.
        var bounds=image.rectTransform.rect;
        float scale=Mathf.Min(bounds.width/image.sprite.rect.width,bounds.height/image.sprite.rect.height);
        float width=image.preserveAspect ? image.sprite.rect.width*scale : bounds.width;
        float height=image.preserveAspect ? image.sprite.rect.height*scale : bounds.height;
        float left=(bounds.width-width)*image.rectTransform.pivot.x;
        float top=(bounds.height-height)*(1-image.rectTransform.pivot.y);
        Undo.RecordObjects(new UnityEngine.Object[] { text,rect },"Fit stage 1 clock digits");
        rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=new Vector2(left+width*.5f,-top-height*(stage3 ? 45f/78f : .55f));
        rect.sizeDelta=new Vector2(width*(stage3 ? .52f : .78f),height*(stage3 ? .18f : .4f));
        text.alignment=TextAnchor.MiddleCenter; text.resizeTextForBestFit=false;
        text.fontSize=Mathf.RoundToInt(height*(stage3 ? .16f : .32f));
        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Could not save clock verification.");
        Debug.Log("Stage 1 clock digits fitted to display. Scene left unsaved.");
    }

    /// <summary>씬에서 편집한 배치를 유지하며 가게 단계에 맞는 상자·시계 원본과 노멀맵을 연결합니다.</summary>
    /// <param name="stage">현재 Scene의 소품 레이어 소유자입니다.</param>
    /// <param name="stage1">이전 천막 가게 원본을 사용할지 여부입니다.</param>
    private static void ApplyShopProps(DystopiaPixelStage stage,bool stage1)
    {
        string[] names={ "FrontContainer","CounterClock" };
        string[] paths=stage1 ? new[] { "Assets/DystopiaPrototype/TopDownTest/Art/Stage1Container.png","Assets/DystopiaPrototype/Art/Stage1Clock.png" }
            : new[] { "Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerMale.png","Assets/DystopiaPrototype/Art/시계.png" };
        string[] normals=stage1 ? new[] { "Assets/DystopiaPrototype/TopDownTest/Art/Stage1ContainerNormal.png","Assets/DystopiaPrototype/Art/Stage1ClockNormal.png" }
            : new[] { "Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerNormal.png","Assets/DystopiaPrototype/Art/CounterClockNormal.png" };
        for(int i=0;i<2;i++)
        {
            if(stage1)
            {
                // 복원 자산만 import 설정하며 현재 3단계 원본과 GUID는 유지합니다.
                foreach(string path in new[] { paths[i],normals[i] })
                {
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    bool isNormal=path == normals[i];
                    importer.textureType=isNormal ? TextureImporterType.Default : TextureImporterType.Sprite;
                    if(!isNormal) { importer.spriteImportMode=SpriteImportMode.Single; importer.spritePixelsPerUnit=100; }
                    importer.sRGBTexture=!isNormal; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
                    importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency=!isNormal; importer.SaveAndReimport();
                }
            }
            var layer=stage.layers.Single(x => x.source != null && x.source.name == names[i]);
            var image=(UnityEngine.UI.Image)layer.source;
            Undo.RecordObject(image,"Apply shop props");
            image.sprite=AssetDatabase.LoadAllAssetsAtPath(paths[i]).OfType<Sprite>().Single();
            if(!stage1) image.color=new Color(.7075472f,.5892614f,.5039605f,1);
            layer.normalSprite=image.sprite; layer.normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>(normals[i]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
    }

    /// <summary>기존 단일 Sprite로 가져온 천막 원본을 그대로 연결합니다.</summary>
    /// <param name="name">기존 Art 폴더의 천막 또는 가판 원본 이름입니다.</param>
    /// <returns>전체 원본 영역을 사용하는 영속 Sprite입니다.</returns>
    private static Sprite FullBoothSprite(string name)
    {
        string path="Assets/DystopiaPrototype/Art/"+name+".png";
        string spriteName=name+"Full";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Scene에 저장된 3단계 가게 부위들을 선택하여 RectTransform 편집을 시작합니다.</summary>
    [MenuItem("Dystopia/Select Stage 3 Shop Parts")]
    public static void SelectStage3ShopParts()
    {
        var stage = UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if (stage == null) throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/Stage3ShopNormal.png");
        Selection.objects = stage.layers.Where(x => x.source != null && (normal != null && x.normalMap == normal || x.source == stage.ceilingLamp))
            .Select(x => (UnityEngine.Object)x.source.gameObject).ToArray();
    }

    /// <summary>사용자 표정 시트를 네 상태로 슬라이싱하고 현재 정면 화면의 손님 오른쪽에 연결합니다.</summary>
    [MenuItem("Dystopia/Apply Trade Reactions")]
    public static void ApplyTradeReactions()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var screen = UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>();
        if (screen == null) throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var portrait = screen.transform.Find("DystopiaCanvas/Customer");
        if (portrait == null) throw new InvalidOperationException("Customer portrait is missing.");
        const string path = "Assets/DystopiaPrototype/Art/TradeReactions.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        string[] names = { "Satisfied", "Delighted", "Reluctant", "Refused" };
        var rects = new SpriteRect[4];
        for (int i = 0; i < rects.Length; i++)
        {
            var previous = old.FirstOrDefault(x => x.name == names[i]);
            rects[i] = new SpriteRect { name = names[i], rect = new Rect(i % 2 == 0 ? 150 : 643, i < 2 ? 655 : 168, 462, 462),
                pivot = new Vector2(.5f,.5f), alignment = SpriteAlignment.Center, spriteID = previous != null ? previous.spriteID : GUID.Generate() };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name,x.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        var child = portrait.Find("TradeReaction");
        if (child == null)
        {
            var go = new GameObject("TradeReaction", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            Undo.RegisterCreatedObjectUndo(go,"Add trade reaction");
            go.transform.SetParent(portrait,false); child = go.transform;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.8f,.78f);
            rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(56,56);
        }
        var image = child.GetComponent<UnityEngine.UI.Image>();
        Undo.RecordObject(image,"Configure trade reaction");
        image.preserveAspect = true; image.raycastTarget = false;
        image.sprite = sprites.Single(x => x.name == names[0]);
        image.gameObject.SetActive(false);
        var serialized = new SerializedObject(screen);
        serialized.FindProperty("tradeReactionImage").objectReferenceValue = image;
        serialized.FindProperty("tradeReactionSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var list = serialized.FindProperty("tradeReactionSprites"); list.arraySize = 4;
        for (int i = 0; i < 4; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sprites.Single(x => x.name == names[i]);
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
        Debug.Log("Trade reactions installed: satisfied, delighted, reluctant, refused.");
    }

    /// <summary>프리팹 내부 대사를 박스의 자식으로 저장합니다.</summary>
    [MenuItem("Dystopia/Fix Dialogue Prefab Layout")]
    public static void FixDialoguePrefabLayout()
    {
        const string path = "Assets/DystopiaPrototype/Prefabs/FrontView.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var panel = root.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(x => x.name == "DialoguePanel");
            var text = root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(x => x.name == "Dialogue");
            var rect = text.rectTransform;
            rect.SetParent(panel.transform, false);
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(-36,-20);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = text.fontSize;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    /// <summary>사용자 대사창을 9-slice Sprite로 임포트하고 대사를 박스 중앙에 연결합니다.</summary>
    [MenuItem("Dystopia/Apply Dialogue Frame")]
    public static void ApplyDialogueFrame()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        const string path = "Assets/DystopiaPrototype/Art/DialogueFrame.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        var rect = new SpriteRect { name = "DialogueFrame", rect = new Rect(78, 212, 2016, 306), pivot = new Vector2(.5f,.5f), alignment = SpriteAlignment.Center,
            border = new Vector4(48,48,48,48), spriteID = old.Length > 0 ? old[0].spriteID : GUID.Generate() };
        provider.SetSpriteRects(new[] { rect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(rect.name, rect.spriteID) });
        provider.Apply();
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        int count = 0;
        foreach (var image in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.name != "DialoguePanel") continue;
            var text = image.transform.parent.Find("Dialogue")?.GetComponent<UnityEngine.UI.Text>();
            if (text == null) text = image.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (text == null) continue;
            Undo.RecordObject(image, "Apply dialogue frame");
            image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced;
            image.preserveAspect = false; image.pixelsPerUnitMultiplier = 4;
            image.color = Color.white; image.raycastTarget = false;
            Undo.SetTransformParent(text.transform, image.transform, "Center dialogue");
            text.rectTransform.localScale = Vector3.one;
            text.rectTransform.localRotation = Quaternion.identity;
            text.rectTransform.pivot = new Vector2(.5f,.5f);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(18,10); text.rectTransform.offsetMax = new Vector2(-18,-10);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Min(12, text.fontSize);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            PrefabUtility.RecordPrefabInstancePropertyModifications(text.rectTransform);
            EditorUtility.SetDirty(image); EditorUtility.SetDirty(text);
            EditorSceneManager.MarkSceneDirty(image.gameObject.scene);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("No dialogue panels found in the open scene.");
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"Dialogue 9-slice applied: {count} centered panels.");
    }
    private const string Root = "Assets/DystopiaPrototype/";
    private const string ScenePath = Root + "Scenes/DystopiaVerticalSlice.unity";
    private static readonly string[] Products = {"Water","Crackers","Can","Rice","Bandage","Painkiller","Battery","Soap","Mask","Fuel"};

    /// <summary>승인된 빈 무제목 Scene만 교체하고, 저장된 Scene은 보존하여 전용 장면을 생성합니다.</summary>
    [MenuItem("Dystopia/Create Dedicated Scene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists; use Refresh Art References.");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene changes: preserve them before creating the prototype.");
        ImportArt();
        Scene previous=SceneManager.GetActiveScene();
        bool replaceApprovedEmpty=SceneManager.sceneCount==1 && string.IsNullOrEmpty(previous.path) &&
            !previous.isDirty && previous.rootCount==1 && previous.GetRootGameObjects()[0].name=="Main Camera";
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,replaceApprovedEmpty?NewSceneMode.Single:NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var cameraObject=new GameObject("DystopiaCamera",typeof(Camera));
        var camera=cameraObject.GetComponent<Camera>(); camera.orthographic=true; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        var screen=new GameObject("DystopiaGame",typeof(DystopiaScreen)).GetComponent<DystopiaScreen>();
        AssignArt(screen);
        EditorSceneManager.SaveScene(scene,ScenePath);
        if(!replaceApprovedEmpty)
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
        }
        Debug.Log("[Dystopia] Created " + Path.GetFullPath(ScenePath) + "; existing scenes preserved.");
    }

    /// <summary>전용 폴더의 신규 아트에만 Sprite import 설정을 적용합니다.</summary>
    [MenuItem("Dystopia/Import Art")]
    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach(string path in Directory.GetFiles(Root+"Art","*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;
            importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;
            importer.SaveAndReimport();
        }
    }

    /// <summary>개별 배급소 레이어와 기존 인물·상품 Sprite를 씬에 직접 연결합니다.</summary>
    /// <param name="screen">참조를 저장할 전용 씬의 화면 컴포넌트입니다.</param>
    private static void AssignArt(DystopiaScreen screen)
    {
        var serialized=new SerializedObject(screen);
        serialized.FindProperty("background").objectReferenceValue=Art("FARBACKGROUND");
        serialized.FindProperty("counter").objectReferenceValue=Art("BoothCounter");
        serialized.FindProperty("midBackground").objectReferenceValue=Art("MidBackground");
        var crowdRows=serialized.FindProperty("crowdRows"); crowdRows.arraySize=3;
        crowdRows.GetArrayElementAtIndex(0).objectReferenceValue=Art("CrowdBack");
        crowdRows.GetArrayElementAtIndex(1).objectReferenceValue=Art("CrowdMiddle");
        crowdRows.GetArrayElementAtIndex(2).objectReferenceValue=Art("CrowdFront");
        serialized.FindProperty("watchGuard").objectReferenceValue=Art("WatchGuard");
        serialized.FindProperty("guardTone").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/GuardNeutral.mat");
        serialized.FindProperty("leftTowerTone").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/LeftTowerNeutral.mat");
        serialized.FindProperty("rightTowerTone").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/RightTowerNeutral.mat");
        var smokeFrames=serialized.FindProperty("chimneySmokeFrames"); smokeFrames.arraySize=4;
        for(int i=0;i<smokeFrames.arraySize;i++) smokeFrames.GetArrayElementAtIndex(i).objectReferenceValue=Art("ChimneySmoke"+i);
        serialized.FindProperty("leftWatchTower").objectReferenceValue=Art("LeftWatchTower");
        serialized.FindProperty("rightWatchTower").objectReferenceValue=Art("RightWatchTower");
        serialized.FindProperty("canopy").objectReferenceValue=Art("BoothCanopy");
        serialized.FindProperty("barricade").objectReferenceValue=Art("BoothBarricade");
        serialized.FindProperty("fogBack").objectReferenceValue=Art("FogBack");
        serialized.FindProperty("fogMid").objectReferenceValue=Art("FogMid");
        serialized.FindProperty("fogFront").objectReferenceValue=Art("FogFront");
        serialized.FindProperty("fogBackMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/FogBack.mat");
        serialized.FindProperty("fogMidMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/FogMid.mat");
        serialized.FindProperty("fogFrontMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/FogFront.mat");
        serialized.FindProperty("daughter").objectReferenceValue=Art("Daughter");
        serialized.FindProperty("inspector").objectReferenceValue=Art("Inspector");
        serialized.FindProperty("inspectorPortraitPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/InspectorPortrait.prefab");
        var customers=serialized.FindProperty("customers"); customers.arraySize=1;
        customers.GetArrayElementAtIndex(0).objectReferenceValue=Art("MaleCustomer0");
        var products=serialized.FindProperty("settings").FindPropertyRelative("products");
        for(int i=0;i<products.arraySize;i++)
        {
            var entry=products.GetArrayElementAtIndex(i);
            int id=entry.FindPropertyRelative("id").intValue;
            if(id > 0 && id <= SurvivalProductArt.Length)
                entry.FindPropertyRelative("sprite").objectReferenceValue=AssetDatabase.LoadAllAssetsAtPath(Root+"Art/Products/"+SurvivalProductArt[id-1]+".png").OfType<Sprite>().SingleOrDefault();
            else if(i < Products.Length) entry.FindPropertyRelative("sprite").objectReferenceValue=Art(Products[i]);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>プロジェクト内部の正確なパスからEditorでのみ取得します。</summary>
    private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Art/"+name+".png");

    /// <summary>専用Sceneの画像参照だけを更新し、調整済みの設定値を維持します。</summary>
    [MenuItem("Dystopia/Refresh Art References")]
    public static void RefreshArt()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene changes must be preserved.");
        ImportArt();
        Scene scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid() || !scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        foreach(var root in scene.GetRootGameObjects())
        {
            var screen=root.GetComponent<DystopiaScreen>();
            if(screen!=null) AssignArt(screen);
        }
        EditorSceneManager.SaveScene(scene);
        if(opened) EditorSceneManager.CloseScene(scene,true);
        Debug.Log("[Dystopia] Art references refreshed; authored settings preserved.");
    }

    /// <summary>既存Sceneセットを閉じずに専用SceneをPlay開始Sceneとして指定します。</summary>
    [MenuItem("Dystopia/Play Vertical Slice")]
    public static void Play()
    {
        if(EditorApplication.isPlaying) return;
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene changes must be preserved before Play.");
        SessionState.SetString("Dystopia.PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("Dystopia.RestoreStart",true);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.isPlaying=true;
    }

    /// <summary>Play終了時に以前の開始Scene設定へ戻します。</summary>
    [InitializeOnLoadMethod]
    private static void RegisterRestore()
    {
        EditorApplication.playModeStateChanged-=Restore;
        EditorApplication.playModeStateChanged+=Restore;
    }

    /// <summary>このツールが設定したEditor状態だけを復元します。</summary>
    private static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Dystopia.RestoreStart",false)) return;
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("Dystopia.PreviousStart",""));
        SessionState.SetBool("Dystopia.RestoreStart",false);
    }

    /// <summary>実際のGame Viewをプロジェクト内の証拠フォルダへ保存します。</summary>
    [MenuItem("Dystopia/Capture Game View")]
    public static void Capture()
    {
        if(!EditorApplication.isPlaying) throw new InvalidOperationException("Capture requires Play Mode.");
        ScreenCapture.CaptureScreenshot(Root+"Evidence/GameView-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");
    }

    /// <summary>UnityのPlay状態を終了します。</summary>
    [MenuItem("Dystopia/Stop Play")]
    public static void Stop() { EditorApplication.isPlaying=false; }
}

/// <summary>실행 전에도 배치 수치를 확인할 수 있는 UI 전용 Inspector 미리보기입니다.</summary>
[CustomEditor(typeof(DystopiaScreen))]
public class DystopiaLayoutInspector : Editor
{
    // 표시 모드만 보관하며 실제 배치 값은 Scene 컴포넌트에 직렬화합니다.
    private bool showClock;

    /// <summary>실제 배치 소유자의 값을 편집하고 1280×720 미리보기를 즉시 그립니다.</summary>
    public override void OnInspectorGUI()
    {
        var component = (MonoBehaviour)target;
        var lighting = component.GetComponent<DystopiaPixelStage>();
        if (lighting != null && GUILayout.Button("시간별 조명 편집")) DystopiaLightingWindow.Open(lighting);
        if (component.transform.Find("DystopiaCanvas") != null || component.transform.Find("TopDownTestCanvas") != null)
        {
            EditorGUILayout.HelpBox("배치는 Hierarchy의 실제 오브젝트에서 Rect Transform / Image로 편집합니다. 판매·제외 영역은 TopDownCheckout의 Box Collider 2D를 편집하세요. 물품 크기·이미지는 Prefabs/Product0~3에서 바꿀 수 있습니다.", MessageType.Info);
            if (!Application.isPlaying)
            {
                if (GUILayout.Button("정면 배치 표시")) DystopiaSceneLayout.ShowLayout(false);
                if (GUILayout.Button("탑다운 배치 표시")) DystopiaSceneLayout.ShowLayout(true);
                if (GUILayout.Button("지침서 배치 표시")) DystopiaSceneLayout.ShowDocument("DailyInstruction");
                if (GUILayout.Button("가계부 배치 표시")) DystopiaSceneLayout.ShowDocument("DailyLedger");
            }
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "calculatorLayout", "calculatorToggleLayout", "counterClockLayout");
            serializedObject.ApplyModifiedProperties();
            return;
        }
        var topDown = target as DystopiaTopDownTest;
        UnityEngine.Object owner = topDown != null && topDown.LayoutOwner != null ? topDown.LayoutOwner : target;
        var layoutObject = owner == target ? serializedObject : new SerializedObject(owner);
        layoutObject.Update();
        EditorGUILayout.LabelField("UI 위치 · 크기 (즉시 미리보기)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("X는 오른쪽, Y는 아래쪽입니다. Play 전에는 아래 미리보기에서 확인하세요. Play 중 변경은 종료하면 되돌아갑니다.", MessageType.Info);
        if (owner != target) EditorGUILayout.ObjectField("현재 배치 설정", owner, typeof(DystopiaScreen), true);
        EditorGUILayout.PropertyField(layoutObject.FindProperty("calculatorLayout"), new GUIContent("계산기 위치 / 크기"));
        EditorGUILayout.PropertyField(layoutObject.FindProperty("calculatorToggleLayout"), new GUIContent("토글 위치 / 크기"));
        EditorGUILayout.PropertyField(layoutObject.FindProperty("counterClockLayout"), new GUIContent("시계 위치 / 크기"));
        layoutObject.ApplyModifiedProperties();
        showClock = GUILayout.Toolbar(showClock ? 1 : 0, new[] { "탑다운 계산기", "정면 시계" }) == 1;
        Rect area = GUILayoutUtility.GetAspectRect(1280f / 720f);
        EditorGUI.DrawRect(area, new Color(.08f,.08f,.08f));
        string art = "Assets/DystopiaPrototype/";
        Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(art + (showClock ? "Art/BoothCounter.png" : "TopDownTest/Art/TopDownWorkbench.png"));
        if (background != null) GUI.DrawTexture(area, background, ScaleMode.StretchToFill);
        GUI.BeginClip(area);
        float scale = area.width / 1280;
        if (showClock)
            DrawArtwork(layoutObject, "counterClockLayout", "CounterClock", scale, new Rect(0,0,1,1));
        else
        {
            DrawArtwork(layoutObject, "calculatorLayout", "Calculator", scale, new Rect(0,0,1,1));
            DrawArtwork(layoutObject, "calculatorToggleLayout", "CalculatorToggle", scale, new Rect(343f/1254,296f/1254,552f/1254,601f/1254));
        }
        GUI.EndClip();
        EditorGUILayout.Space();
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "calculatorLayout", "calculatorToggleLayout", "counterClockLayout");
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>실제 UI와 같은 위치·크기 및 텍스처 영역을 표시합니다.</summary>
    /// <param name="data">배치 소유자의 직렬화 데이터입니다.</param>
    /// <param name="field">Rect 필드 이름입니다.</param>
    /// <param name="asset">UI 이미지 이름입니다.</param>
    /// <param name="scale">미리보기의 기준 해상도 배율입니다.</param>
    /// <param name="uv">이미지에서 표시할 영역입니다.</param>
    private static void DrawArtwork(SerializedObject data, string field, string asset, float scale, Rect uv)
    {
        Rect layout = data.FindProperty(field).rectValue;
        Rect display = new Rect(layout.x*scale,layout.y*scale,Mathf.Max(1,layout.width)*scale,Mathf.Max(1,layout.height)*scale);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/TopDownTest/Art/"+asset+".png");
        if (texture != null) GUI.DrawTextureWithTexCoords(display,texture,uv);
    }
}

/// <summary>독립 탑다운 Scene에서도 동일한 배치 편집과 미리보기를 제공합니다.</summary>
[CustomEditor(typeof(DystopiaTopDownTest))]
public sealed class DystopiaTopDownLayoutInspector : DystopiaLayoutInspector { }

/// <summary>픽셀 조명의 시간별 곡선을 전용 창에서 편집하도록 연결합니다.</summary>
[CustomEditor(typeof(DystopiaPixelStage))]
public sealed class DystopiaLightingInspector : Editor
{
    /// <summary>기존 기본값과 별도의 시간별 편집 버튼을 표시합니다.</summary>
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("시간별 조명 편집")) DystopiaLightingWindow.Open((DystopiaPixelStage)target);
        DrawDefaultInspector();
    }
}

/// <summary>Scene의 기존 조명 컴포넌트에 시간별 키를 저장하고 미리보기 시각을 함께 편집합니다.</summary>
public sealed class DystopiaLightingWindow : EditorWindow
{
    // 창 선택과 스크롤만 보관하며 조명 데이터의 소유자는 Scene 컴포넌트입니다.
    [SerializeField] private DystopiaPixelStage lighting;
    [SerializeField] private float hour = 9;
    private Vector2 scroll;
    private bool showPosition, showNight;

    /// <summary>선택한 오브젝트의 조명 편집 창을 엽니다.</summary>
    [MenuItem("Dystopia/시간별 조명 편집")]
    private static void OpenSelected()
    {
        Open(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<DystopiaPixelStage>() : null);
    }

    /// <summary>지정한 조명 컴포넌트를 전용 창에 연결합니다.</summary>
    /// <param name="target">편집할 Scene 조명이며 없으면 창에서 직접 선택합니다.</param>
    public static void Open(DystopiaPixelStage target)
    {
        var window = GetWindow<DystopiaLightingWindow>("시간별 조명");
        window.lighting = target;
        window.minSize = new Vector2(460, 500);
        window.Show();
    }

    /// <summary>현재 시각의 조명값과 곡선, 기존 야간 설정을 표시합니다.</summary>
    private void OnGUI()
    {
        lighting = (DystopiaPixelStage)EditorGUILayout.ObjectField("조명 대상", lighting, typeof(DystopiaPixelStage), true);
        if (lighting == null) { EditorGUILayout.HelpBox("TopDownCheckout의 Dystopia Pixel Stage를 연결하세요.", MessageType.Info); return; }
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Play를 종료한 뒤 편집하세요. Scene에 저장할 설정입니다.", MessageType.Info);
            return;
        }
        var data = new SerializedObject(lighting);
        data.Update();
        var clock = lighting.GetComponent<DystopiaDayNight>();
        EditorGUILayout.HelpBox("시각을 고르고 값을 바꾸면 해당 시각에 키가 생깁니다. 중간 시간은 선형 보간합니다. 곡선을 열어 키를 이동·삭제할 수 있습니다. 완료 후 Scene을 저장하세요.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        hour = EditorGUILayout.Slider("편집 시각", hour, 9, 21);
        bool timeChanged = EditorGUI.EndChangeCheck();
        EditorGUILayout.BeginHorizontal();
        foreach (int time in new[] { 9, 12, 15, 18, 21 })
            if (GUILayout.Button(time + "시")) { hour = time; timeChanged = true; }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("이 시각 미리보기")) timeChanged = true;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("시간별 명암", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(data.FindProperty("normalStrength"), new GUIContent("기본 노멀 강도"));
        Row(data, "hourlyNormal", "노멀 배율", 0, 2);
        Row(data, "hourlyAmbient", "주변광 배율", 0, 2);
        Row(data, "hourlySunlight", "햇빛 배율", 0, 3);
        Row(data, "hourlyHardness", "명암 경계 (1: 또렷)", 0, 1);
        Row(data, "hourlyFill", "인물 그늘 최소 밝기", 0, 1);
        Row(data, "hourlyShadow", "햇빛 그림자 배율", 0, 3);
        EditorGUILayout.HelpBox("9시의 또렷한 명암: 기본 노멀 1, 명암 경계 1에서 시작하세요. 흐릿하면 주변광·그늘 최소 밝기를 내리고, 밝은 면이 부족하면 햇빛 배율을 올리세요. 기본 노멀은 모든 시간에 적용되고 노멀 배율로 시간별 조절합니다.", MessageType.None);
        showPosition = EditorGUILayout.Foldout(showPosition, "태양 위치 보정", true);
        if (showPosition)
        {
            Row(data, "hourlySunX", "좌우 보정 (픽셀)", -640, 640);
            Row(data, "hourlySunY", "상하 보정 (위 +)", -360, 360);
        }
        showNight = EditorGUILayout.Foldout(showNight, "기존 야간 램프 · 스포트 설정", true);
        if (showNight)
        {
            EditorGUILayout.HelpBox("Ceiling Lamp가 연결되어 있으면 해당 오브젝트의 RectTransform으로 광원 위치를 옮깁니다. Spot Target은 비추는 지점(오른쪽 X+, 아래 Y+), Spot Half Angle은 빛의 폭, Spot Softness는 가장자리 부드러움입니다. Lamp Radius는 주변광 범위입니다.", MessageType.Info);
            foreach (string field in new[] { "ceilingLamp", "lampPosition", "lampHeight", "lampRadius", "lampIntensity", "lampColor", "eveningSpotlight", "spotOrigin", "spotTarget", "spotIntensity", "spotHalfAngle", "spotSoftness", "spotHaze", "towerBacklight", "customerShadowOpacity" })
                EditorGUILayout.PropertyField(data.FindProperty(field));
        }
        EditorGUILayout.EndScrollView();
        bool changed = data.ApplyModifiedProperties();
        if ((timeChanged || changed) && clock != null)
        {
            var preview = new SerializedObject(clock);
            preview.Update();
            preview.FindProperty("preview").boolValue = true;
            preview.FindProperty("previewHour").floatValue = hour;
            preview.ApplyModifiedProperties();
        }
        if (timeChanged || changed) { EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
    }

    /// <summary>현재 시각의 값을 조절하거나 전체 곡선을 직접 편집합니다. 새 키는 양쪽 모두 선형입니다.</summary>
    /// <param name="data">값을 저장할 조명 직렬화 객체입니다.</param>
    /// <param name="field">시간별 곡선 필드입니다.</param>
    /// <param name="label">사용자에게 표시할 조절 항목입니다.</param>
    /// <param name="min">슬라이더 최솟값입니다.</param>
    /// <param name="max">슬라이더 최댓값입니다.</param>
    private void Row(SerializedObject data, string field, string label, float min, float max)
    {
        var property = data.FindProperty(field);
        var curve = property.animationCurveValue;
        EditorGUI.BeginChangeCheck();
        float value = EditorGUILayout.Slider(label, curve.Evaluate(hour), min, max);
        if (EditorGUI.EndChangeCheck())
        {
            int index = Array.FindIndex(curve.keys, key => Mathf.Abs(key.time-hour) < .001f);
            index = index < 0 ? curve.AddKey(new Keyframe(hour, value)) : curve.MoveKey(index, new Keyframe(hour, value));
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            property.animationCurveValue = curve;
        }
        EditorGUILayout.PropertyField(property, new GUIContent("시간 곡선"));
    }
}

/// <summary>Scene에 저장된 정면과 탑다운 배치의 편집 표시를 전환합니다.</summary>
public static class DystopiaSceneLayout
{
    /// <summary>편집 화면 표시만 바꾸며 실행 시에는 세션이 표시 상태를 결정합니다.</summary>
    public static void ShowLayout(bool topDown)
    {
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>(FindObjectsInactive.Include);
        var top=UnityEngine.Object.FindFirstObjectByType<DystopiaTopDownTest>(FindObjectsInactive.Include);
        if (screen == null || top == null) return;
        screen.transform.Find("DystopiaCanvas").gameObject.SetActive(!topDown);
        foreach(string name in new[]{"DailyInstruction","DailyLedger","Modal"})
            screen.transform.Find("DystopiaCanvas/"+name).gameObject.SetActive(false);
        top.transform.Find("TopDownCamera").gameObject.SetActive(topDown);
        top.transform.Find("TopDownWorkbench").gameObject.SetActive(topDown);
        var canvas=top.transform.Find("TopDownTestCanvas");
        canvas.Find("FrontView").gameObject.SetActive(!topDown);
        canvas.Find("CounterClock").gameObject.SetActive(!topDown);
        canvas.Find("WorkViewUI").gameObject.SetActive(topDown);
        canvas.Find("WorkViewUI/PouringContainer").gameObject.SetActive(topDown);
        SceneView.RepaintAll();
    }

    /// <summary>저장된 지침서 또는 가계부를 선택해 편집할 수 있도록 표시합니다.</summary>
    /// <param name="name">정면 Canvas의 문서 오브젝트 이름입니다.</param>
    public static void ShowDocument(string name)
    {
        ShowLayout(false);
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>(FindObjectsInactive.Include);
        screen.transform.Find("DystopiaCanvas/"+name).gameObject.SetActive(true);
        SceneView.RepaintAll();
    }

}
