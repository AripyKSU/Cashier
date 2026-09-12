using System;
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

    /// <summary>3단계 가게 원본을 연결합니다. 기존 배치와 조명값은 보존하고 새 부위만 초기 배치합니다.</summary>
    [MenuItem("Dystopia/Apply Stage 3 Shop")]
    public static void ApplyStage3Shop()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage = UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if (stage == null || stage.frontCanvas == null) throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var counterLayer = stage.layers.Single(x => x.source != null && (x.source.name == "Counter" || x.source.name == "Stage3Counter"));
        var canopyLayer = stage.layers.Single(x => x.source != null && (x.source.name == "Canopy" || x.source.name == "Stage3Ceiling"));
        const string artPath = "Assets/DystopiaPrototype/Art/Stage3Shop.png";
        const string normalPath = "Assets/DystopiaPrototype/Art/Stage3ShopNormal.png";
        AssetDatabase.ImportAsset(artPath);
        AssetDatabase.ImportAsset(normalPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(artPath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048; importer.SaveAndReimport();
        string[] names = { "Stage3Counter", "Stage3Ceiling", "Stage3LeftPillar", "Stage3RightPillar", "Stage3CeilingLamp" };
        // 동일한 원본 UV를 노멀맵에도 사용하며 중앙의 인물·배경·대사는 제외합니다.
        var crops = new[] { new Rect(0,0,1672,326),new Rect(0,777,1672,164),new Rect(0,326,183,451),new Rect(1493,326,179,451),new Rect(738,843,196,27) };
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        var rects = new SpriteRect[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var previous = old.FirstOrDefault(x => x.name == names[i]);
            rects[i] = new SpriteRect { name=names[i],rect=crops[i],pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=previous != null ? previous.spriteID : GUID.Generate() };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name,x.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        // 셰이더가 선형 RGB를 직접 해석하므로 플랫폼별 압축 노멀 형식으로 변환하지 않습니다.
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        normalImporter.textureType = TextureImporterType.Default; normalImporter.sRGBTexture = false;
        normalImporter.filterMode = FilterMode.Point; normalImporter.mipmapEnabled = false;
        normalImporter.npotScale = TextureImporterNPOTScale.None;
        normalImporter.textureCompression = TextureImporterCompression.Uncompressed;
        normalImporter.maxTextureSize = 2048; normalImporter.SaveAndReimport();
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>().ToArray();
        Undo.RecordObject(stage,"Apply stage 3 shop lighting");
        bool wasEnabled = stage.enabled;
        stage.enabled = false; // 이전 메시를 해제해 추가된 레이어까지 다음 프레임에 생성합니다.
        try
        {
            var layers = stage.layers.ToList();
            for (int i = 0; i < names.Length; i++)
            {
                var sprite = sprites.Single(x => x.name == names[i]);
                var layer = i == 0 ? counterLayer : i == 1 ? canopyLayer : layers.FirstOrDefault(x =>
                    x.source is UnityEngine.UI.Image existing && (existing.sprite == sprite || existing.name == names[i]));
                bool created = layer == null;
                if (layer == null)
                {
                    var go = new GameObject(names[i],typeof(RectTransform),typeof(UnityEngine.UI.Image));
                    Undo.RegisterCreatedObjectUndo(go,"Add stage 3 shop part");
                    go.transform.SetParent(stage.frontCanvas,false);
                    layer = new DystopiaPixelStage.Layer { source=go.GetComponent<UnityEngine.UI.Image>() };
                    layers.Insert(layers.IndexOf(canopyLayer)+i-1,layer);
                }
                var image = (UnityEngine.UI.Image)layer.source;
                // 이미 연결한 부위는 이름·색·표면 반응까지 사용자의 편집값을 그대로 유지합니다.
                if (!created && image.sprite == sprite) continue;
                Undo.RecordObject(image,"Set stage 3 sprite");
                image.sprite = sprite;
                if (created)
                {
                    image.color=Color.white; image.preserveAspect=false; image.raycastTarget=false;
                    var rect = image.rectTransform;
                    rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
                    rect.anchoredPosition=new Vector2(crops[i].x*1280/1672,-(941-crops[i].yMax)*720/941);
                    rect.sizeDelta=new Vector2(crops[i].width*1280/1672,crops[i].height*720/941);
                }
                layer.surface = i == 4 ? DystopiaPixelStage.Surface.Unlit : i == 0 ? DystopiaPixelStage.Surface.Metal : DystopiaPixelStage.Surface.Environment;
                layer.normalSprite=image.sprite; layer.normalMap=i == 4 ? null : normal;
                if (created)
                {
                    layer.normalResponse=.55f; layer.roomResponse=i == 4 ? 0 : .3f; layer.lampResponse=.4f;
                    layer.rimWidthPixels=1; layer.rimResponse=.1f;
                    layer.highlightResponse=.75f; layer.specularResponse=.12f; layer.emission=i == 4 ? 1.5f : 0;
                    if (i == 4 && stage.ceilingLamp == null) stage.ceilingLamp=image;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
            stage.layers=layers.ToArray();
        }
        finally { stage.enabled=wasEnabled; }
        EditorUtility.SetDirty(stage);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("Stage 3 art connected. Existing layout and lighting preserved; scene not automatically saved.");
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
        for(int i=0;i<products.arraySize;i++) products.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue=Art(Products[i]);
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
