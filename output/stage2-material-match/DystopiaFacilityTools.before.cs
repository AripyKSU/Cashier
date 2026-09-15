using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>단계별 상판·설비·상자·작업대 그림을 임포트하고 승인 기준 씬에 배치합니다. 배치는 기준 씬이 소유합니다.</summary>
public static class DystopiaFacilityTools
{
    private const string Root="Assets/DystopiaPrototype/";
    /// <summary>2026-09-15 이후 단계별 아트의 권위 폴더입니다. 기존 Checkout 자산은 그대로 둡니다.</summary>
    private const string Art="Assets/Textures/art/";
    private const string Shop=Art+"Facility/CounterTop/";
    private const string Crates=Art+"Facility/Crate/";
    private const string Facilities=Art+"Facility/Props/";
    private const string Workbench=Art+"Facility/Workbench/";
    /// <summary>1280×720 화면 기준 상판 윗면이 시작하는 Y입니다. 아래쪽 하부장 일부는 화면 밖으로 잘립니다.</summary>
    private const float CounterTopY=380;
    /// <summary>기존 탑다운 작업대의 월드 폭입니다. 새 그림도 같은 폭으로 맞춥니다.</summary>
    private const float WorkbenchWorldWidth=12.8f;

    /// <summary>설비 오브젝트 이름과 상품 해금 설비의 대응입니다. 그리기 순서는 단계 배치에서 정합니다.</summary>
    private static readonly (string name,DystopiaFacility facility)[] FacilityObjects=
    {
        ("FacilityFoodShelf",DystopiaFacility.FoodShelf),
        ("FacilityMedicineCabinet",DystopiaFacility.MedicineCabinet),
        ("FacilityToolBench",DystopiaFacility.ToolBench),
        ("FacilityPowerCommunications",DystopiaFacility.PowerCommunications),
        ("FacilityNuclearProtection",DystopiaFacility.NuclearProtection),
        ("FacilityPrecisionElectronics",DystopiaFacility.PrecisionElectronics)
    };

    /// <summary>한 설비의 화면 배치입니다. x·y는 좌상단, 높이는 잘라낸 그림의 비율로 계산합니다.</summary>
    private struct Placement
    {
        public DystopiaFacility facility; public float x,y,width;
        public Placement(DystopiaFacility facility,float x,float y,float width) { this.facility=facility; this.x=x; this.y=y; this.width=width; }
    }

    /// <summary>단계별 설비 배치입니다. 배열 순서가 뒤에서 앞으로의 그리기 순서입니다.</summary>
    private static Placement[] StagePlacements(int stage) => stage switch
    {
        1=>new[]
        {
            new Placement(DystopiaFacility.FoodShelf,9,CounterTopY+53,213),
            new Placement(DystopiaFacility.MedicineCabinet,1045,CounterTopY+53,221)
        },
        2=>new[]
        {
            new Placement(DystopiaFacility.ToolBench,31,CounterTopY-71,354),
            new Placement(DystopiaFacility.PowerCommunications,1045,CounterTopY-71,204),
            new Placement(DystopiaFacility.FoodShelf,9,CounterTopY+106,204),
            new Placement(DystopiaFacility.MedicineCabinet,1054,CounterTopY+142,221)
        },
        _=>new[]
        {
            new Placement(DystopiaFacility.NuclearProtection,27,CounterTopY-182,399),
            new Placement(DystopiaFacility.PrecisionElectronics,841,CounterTopY-120,438),
            new Placement(DystopiaFacility.ToolBench,35,CounterTopY+17,301),
            new Placement(DystopiaFacility.PowerCommunications,1058,CounterTopY+12,182),
            new Placement(DystopiaFacility.FoodShelf,4,CounterTopY+123,208),
            new Placement(DystopiaFacility.MedicineCabinet,1063,CounterTopY+136,213)
        }
    };

    /// <summary>제공된 PNG를 원본 그대로 두고 Sprite 영역만 실제 그림에 맞춰 임포트합니다.</summary>
    [MenuItem("Dystopia/설비/1. Import Facility Artwork")]
    public static void ImportArtwork()
    {
        var cropped=new[]
        {
            Shop+"Stage1CounterTop.png",Shop+"Stage2CounterTop.png",Shop+"Stage3CounterTop.png",
            Crates+"Stage1CrateClosed.png",Crates+"Stage1CrateOpen.png",Crates+"Stage2CrateClosed.png",Crates+"Stage2CrateOpen.png",Crates+"Stage3CrateClosed.png",Crates+"Stage3CrateOpen.png",
            Facilities+"Stage1FoodShelf.png",Facilities+"Stage1MedicineCabinet.png",
            Facilities+"Stage2FoodShelf.png",Facilities+"Stage2MedicineCabinet.png",Facilities+"Stage2ToolBench.png",Facilities+"Stage2PowerCommunications.png",
            Facilities+"Stage3FoodShelf.png",Facilities+"Stage3MedicineCabinet.png",Facilities+"Stage3ToolBench.png",Facilities+"Stage3PowerCommunications.png",Facilities+"Stage3NuclearProtection.png",Facilities+"Stage3PrecisionElectronics.png"
        };
        foreach(string path in cropped) ImportSprite(path,true);
        for(int stage=1;stage<=3;stage++) ImportSprite(Workbench+"Stage"+stage+"TopDownWorkbench.png",false);
        Debug.Log("Facility artwork imported: "+(cropped.Length+3)+" sprites.");
    }

    /// <summary>세 단계 기준 씬을 차례로 적용·배치·저장하고 작업 씬은 3단계 상태로 남깁니다.</summary>
    [MenuItem("Dystopia/설비/2. Build Stage 1~3 References")]
    public static void BuildAllReferences()
    {
        for(int stage=1;stage<=3;stage++)
        {
            DystopiaTools.ApplyApprovedStageReference(stage);
            BuildStageLayout(stage);
            SaveReference(stage);
        }
        Debug.Log("Stage 1~3 references rebuilt with facilities. The open scene is at stage 3 and unsaved.");
    }

    [MenuItem("Dystopia/설비/Apply And Build Stage 1")] public static void ApplyAndBuild1() { DystopiaTools.ApplyApprovedStageReference(1); BuildStageLayout(1); }
    [MenuItem("Dystopia/설비/Apply And Build Stage 2")] public static void ApplyAndBuild2() { DystopiaTools.ApplyApprovedStageReference(2); BuildStageLayout(2); }
    [MenuItem("Dystopia/설비/Apply And Build Stage 3")] public static void ApplyAndBuild3() { DystopiaTools.ApplyApprovedStageReference(3); BuildStageLayout(3); }
    [MenuItem("Dystopia/설비/Build Current Stage 1 Layout")] public static void Build1() => BuildStageLayout(1);
    [MenuItem("Dystopia/설비/Build Current Stage 2 Layout")] public static void Build2() => BuildStageLayout(2);
    [MenuItem("Dystopia/설비/Build Current Stage 3 Layout")] public static void Build3() => BuildStageLayout(3);
    [MenuItem("Dystopia/설비/Save Stage 1 Reference")] public static void Save1() => SaveReference(1);
    [MenuItem("Dystopia/설비/Save Stage 2 Reference")] public static void Save2() => SaveReference(2);
    [MenuItem("Dystopia/설비/Save Stage 3 Reference")] public static void Save3() => SaveReference(3);

    /// <summary>현재 단계의 배경·연기 배치를 1단계 기준(화면 전체)으로 맞추고 캐노피와 양옆 기둥을 숨깁니다. 천장등은 건드리지 않습니다.</summary>
    [MenuItem("Dystopia/설비/Fill Background Like Stage 1")]
    public static void FillBackgroundLikeStage1()
    {
        var stage=FindStage();
        Undo.RecordObject(stage,"Fill background like stage 1");
        var preview=EditorSceneManager.OpenPreviewScene(Root+"Editor/References/Stage1Reference.unity");
        try
        {
            var saved=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DystopiaPixelStage>(true)).Single();
            foreach(string name in new[] { "FarBackground","DawnBackground","EveningBackground","SunsetBackground","CityLights","MidBackground","LeftChimneySmoke","RightChimneySmoke" })
            {
                var target=stage.layers.Single(l=>l.source!=null && l.source.name==name).source.rectTransform;
                var reference=saved.layers.Single(l=>l.source!=null && l.source.name==name).source.rectTransform;
                DystopiaTools.CopyStageReferenceRect(target,reference);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach(string name in new[] { "Canopy","Stage3Ceiling","Stage3LeftPillar","Stage3RightPillar" })
        {
            var layer=stage.layers.FirstOrDefault(l=>l.source!=null && l.source.name==name);
            if(layer==null) continue;
            Undo.RecordObject(layer.source.gameObject,"Hide shop frame");
            layer.source.gameObject.SetActive(false);
            Dirty(layer.source.gameObject);
        }
        DystopiaTools.AlignRearGuardRects(stage);
        Dirty(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("Background filled like stage 1; canopy and pillars hidden.");
    }

    /// <summary>천장에 매단 시계는 바닥에 닿지 않으므로 접촉 그림자와 하단 음영을 끕니다. 배치는 건드리지 않습니다.</summary>
    [MenuItem("Dystopia/설비/Clock: Remove Shadow")]
    public static void RemoveClockShadow()
    {
        var stage=FindStage();
        var layer=stage.layers.FirstOrDefault(l=>l.source!=null && l.source.name=="CounterClock");
        if(layer==null) throw new InvalidOperationException("CounterClock 레이어를 찾지 못했습니다.");
        Undo.RecordObject(stage,"Remove clock shadow");
        layer.contactShadow=Vector4.zero;
        layer.bottomShade=0;
        // uGUI Shadow 효과가 붙어 있으면 그것도 그림자로 보이므로 함께 끕니다.
        var uiShadow=layer.source.GetComponent<UnityEngine.UI.Shadow>();
        if(uiShadow!=null && uiShadow.enabled) { Undo.RecordObject(uiShadow,"Disable clock UI shadow"); uiShadow.enabled=false; Dirty(uiShadow); }
        Dirty(stage);
        // 접촉 그림자 렌더러는 최초 생성 때 만들어지므로 임시 렌더를 다시 만들어 즉시 사라지게 합니다.
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("CounterClock shadow removed (contactShadow, bottomShade"+(uiShadow!=null ? ", UI Shadow" : "")+").");
    }

    /// <summary>시계 Image의 색 틴트를 흰색으로 되돌려 원본 색 그대로 보이게 합니다.</summary>
    [MenuItem("Dystopia/설비/Clock: Reset Tint")]
    public static void ResetClockTint()
    {
        var stage=FindStage();
        var layer=stage.layers.FirstOrDefault(l=>l.source!=null && l.source.name=="CounterClock");
        if(layer==null) throw new InvalidOperationException("CounterClock 레이어를 찾지 못했습니다.");
        var image=(Image)layer.source;
        Undo.RecordObject(image,"Reset clock tint");
        image.color=Color.white;
        Dirty(image);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("CounterClock tint reset to white.");
    }

    /// <summary>편집 중인 픽셀 렌더를 output 폴더에 저장해 Play 없이 배치를 확인합니다.</summary>
    [MenuItem("Dystopia/설비/Capture Front Preview")]
    public static void CapturePreview()
    {
        var stage=FindStage();
        Canvas.ForceUpdateCanvases();
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
        var texture=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage);
        if(texture==null) throw new InvalidOperationException("Pixel stage has no render texture. Enable Preview In Editor and show the front layout.");
        Directory.CreateDirectory("output/facility-preview");
        string path="output/facility-preview/front-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png";
        var previous=RenderTexture.active; var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try { RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply(); File.WriteAllBytes(path,pixels.EncodeToPNG()); }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        File.WriteAllText("output/facility-preview/latest.txt",path);
        Debug.Log("Front preview captured: "+path);
    }

    /// <summary>현재 씬에 한 단계의 상판·설비·상자·작업대 그림과 배치를 적용합니다. 저장은 하지 않습니다.</summary>
    /// <param name="stageNumber">1~3 단계입니다.</param>
    private static void BuildStageLayout(int stageNumber)
    {
        var stage=FindStage();
        Undo.RecordObject(stage,"Build stage facilities");
        var layers=stage.layers.ToList();
        var counter=layers.Single(l=>l.source!=null && (l.source.name=="Counter" || l.source.name=="Stage3Counter"));
        var counterImage=(Image)counter.source;
        var canvas=counterImage.transform.parent;

        // 상판: 잘라낸 Sprite를 화면 폭에 맞추고 윗면 시작 높이를 고정합니다.
        var counterSprite=LoadSprite(Shop+"Stage"+stageNumber+"CounterTop.png");
        Undo.RecordObjects(new UnityEngine.Object[] { counterImage,counterImage.rectTransform },"Stage counter top");
        counterImage.sprite=counterSprite; counterImage.color=Color.white; counterImage.preserveAspect=false;
        SetRect(counterImage.rectTransform,0,CounterTopY,1280,1280*counterSprite.rect.height/counterSprite.rect.width);
        counter.normalMap=null; counter.normalSprite=null;
        Dirty(counterImage);

        // 기둥은 상판 앞이 아니라 뒤에 그려 상판 윗면과 겹치지 않게 합니다.
        foreach(string pillarName in new[] { "Stage3LeftPillar","Stage3RightPillar" })
        {
            var pillar=layers.FirstOrDefault(l=>l.source!=null && l.source.name==pillarName);
            if(pillar==null) continue;
            layers.Remove(pillar);
            layers.Insert(layers.IndexOf(counter),pillar);
            var pillarTransform=pillar.source.transform;
            if(pillarTransform.parent==canvas && pillarTransform.GetSiblingIndex()>counterImage.transform.GetSiblingIndex())
            {
                Undo.SetSiblingIndex(pillarTransform,counterImage.transform.GetSiblingIndex(),"Pillar behind counter");
            }
        }

        // 설비: 없는 오브젝트는 만들고, 이 단계에 없는 설비는 숨깁니다. 그리기 순서는 배치 배열을 따릅니다.
        var placements=StagePlacements(stageNumber);
        int insertLayer=layers.IndexOf(counter)+1;
        int insertSibling=counterImage.transform.GetSiblingIndex()+1;
        var ordered=FacilityObjects.OrderBy(f=>{ int i=Array.FindIndex(placements,p=>p.facility==f.facility); return i<0 ? int.MaxValue : i; }).ToArray();
        foreach(var entry in ordered)
        {
            var layer=layers.FirstOrDefault(l=>l.source!=null && l.source.name==entry.name);
            if(layer==null)
            {
                var go=new GameObject(entry.name,typeof(RectTransform),typeof(Image));
                Undo.RegisterCreatedObjectUndo(go,"Create facility");
                go.transform.SetParent(canvas,false);
                layer=JsonUtility.FromJson<DystopiaPixelStage.Layer>(JsonUtility.ToJson(counter));
                layer.source=go.GetComponent<Image>();
                ((Image)layer.source).raycastTarget=false;
                layer.normalMap=null; layer.normalSprite=null; layer.textureEdgeTrim=Vector2.zero;
                // 얇은 검은 외곽선을 테두리 조명으로 밝히지 않고 놓인 소품의 밑면만 살짝 어둡게 합니다.
                layer.rimResponse=0; layer.specularResponse=.2f; layer.highlightResponse=.9f; layer.bottomShade=.3f; layer.contactShadow=Vector4.zero;
            }
            else layers.Remove(layer);
            layers.Insert(insertLayer++,layer);
            var image=(Image)layer.source;
            Undo.RecordObjects(new UnityEngine.Object[] { image,image.rectTransform,image.gameObject },"Place facility");
            if(image.transform.parent==canvas) Undo.SetSiblingIndex(image.transform,insertSibling++,"Facility order");
            int index=Array.FindIndex(placements,p=>p.facility==entry.facility);
            if(index<0) { image.gameObject.SetActive(false); Dirty(image); continue; }
            var placement=placements[index];
            var sprite=LoadSprite(Facilities+"Stage"+stageNumber+FacilityFile(entry.facility)+".png");
            image.sprite=sprite; image.color=Color.white; image.preserveAspect=true; image.enabled=true;
            SetRect(image.rectTransform,placement.x,placement.y,placement.width,placement.width*sprite.rect.height/sprite.rect.width);
            image.gameObject.SetActive(true);
            Dirty(image);
        }
        stage.layers=layers.ToArray();

        // 상자: 정면의 닫힌 상자와 쏟는 열린 상자를 단계 그림으로 바꾸고 배치는 유지합니다.
        var front=layers.Single(l=>l.source!=null && l.source.name=="FrontContainer");
        var frontImage=(Image)front.source;
        Undo.RecordObject(frontImage,"Stage crate");
        frontImage.sprite=LoadSprite(Crates+"Stage"+stageNumber+"CrateClosed.png"); frontImage.color=Color.white; frontImage.preserveAspect=true;
        front.normalMap=null; front.normalSprite=null; front.rimResponse=0;
        Dirty(frontImage);
        var pouring=FindInScene(stage.gameObject.scene,"TopDownCheckout/TopDownTestCanvas/WorkViewUI/PouringContainer").GetComponent<Image>();
        Undo.RecordObject(pouring,"Stage open crate");
        pouring.sprite=LoadSprite(Crates+"Stage"+stageNumber+"CrateOpen.png"); pouring.preserveAspect=true;
        Dirty(pouring);

        // 작업대: 폭을 기존 월드 크기에 맞추고 위아래는 그림 비율대로 둡니다.
        var bench=FindInScene(stage.gameObject.scene,"TopDownCheckout/TopDownWorkbench");
        var renderer=bench.GetComponent<SpriteRenderer>();
        Undo.RecordObjects(new UnityEngine.Object[] { renderer,bench.transform },"Stage workbench");
        renderer.sprite=LoadSprite(Workbench+"Stage"+stageNumber+"TopDownWorkbench.png");
        float scale=WorkbenchWorldWidth/(renderer.sprite.rect.width/renderer.sprite.pixelsPerUnit);
        bench.transform.localScale=new Vector3(scale,scale,1);
        Dirty(renderer); Dirty(bench.transform);

        // 손님 그림자가 떨어지는 상판 뒤·앞 경계를 새 홈 위치로 옮깁니다.
        stage.shadowTableY=new Vector2(CounterTopY+17,CounterTopY+309);
        Dirty(stage);
        // 픽셀 렌더는 최초 생성 시의 레이어 목록만 캡처하므로 새 설비가 포함되도록 임시 렌더를 다시 만들게 합니다.
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("Stage "+stageNumber+" facilities laid out in the open scene.");
    }

    /// <summary>현재 씬 사본으로 적용 메뉴의 기준 파일을 갱신하고 이전 기준을 output에 보존합니다.</summary>
    /// <param name="stageNumber">1~3 단계입니다.</param>
    private static void SaveReference(int stageNumber)
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice in edit mode.");
        string directory="output/stage"+stageNumber+"-reference/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        string reference=Root+"Editor/References/Stage"+stageNumber+"Reference.unity";
        File.Copy(reference,directory+"/previous-reference.unity");
        if(!EditorSceneManager.SaveScene(scene,directory+"/Stage"+stageNumber+".unity",true)) throw new IOException("Stage "+stageNumber+" snapshot failed.");
        File.Copy(directory+"/Stage"+stageNumber+".unity",reference,true);
        AssetDatabase.ImportAsset(reference,ImportAssetOptions.ForceSynchronousImport);
        File.WriteAllText("output/stage"+stageNumber+"-reference/latest.txt",directory+"/Stage"+stageNumber+".unity");
        Debug.Log("Stage "+stageNumber+" reference saved: "+directory);
    }

    private static string FacilityFile(DystopiaFacility facility) => facility switch
    {
        DystopiaFacility.FoodShelf=>"FoodShelf",
        DystopiaFacility.MedicineCabinet=>"MedicineCabinet",
        DystopiaFacility.ToolBench=>"ToolBench",
        DystopiaFacility.PowerCommunications=>"PowerCommunications",
        DystopiaFacility.NuclearProtection=>"NuclearProtection",
        _=>"PrecisionElectronics"
    };

    /// <summary>좌상단 기준 배치를 기존 소품과 같은 앵커·피벗으로 설정합니다.</summary>
    private static void SetRect(RectTransform rect,float x,float y,float width,float height)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
        rect.anchoredPosition3D=new Vector3(x,-y,0); rect.sizeDelta=new Vector2(width,height);
        rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity;
        Dirty(rect);
    }

    private static void Dirty(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    /// <summary>현재 2단계 상판의 배치를 보존하고 원본 가장자리 UV를 반사한 두 면으로 화면 좌우 빈 곳을 채웁니다. 명시적 메뉴 실행 때만 배치합니다.</summary>
    [MenuItem("Dystopia/설비/Extend Current Stage 2 Counter Sides")]
    public static void ExtendStage2CounterSides()
    {
        var stage=FindStage();
        var layer=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        var counter=(Image)layer.source;
        if(counter.sprite==null || counter.sprite.name!="Stage2CounterTop") throw new InvalidOperationException("Stage2CounterTop 상판에서만 실행하세요.");
        var rect=counter.rectTransform;
        if(rect.childCount!=0) throw new InvalidOperationException("기존 상판 자식이 있습니다. 중복 적용하지 않습니다.");
        var canvas=(RectTransform)rect.parent;
        if(Quaternion.Angle(rect.localRotation,Quaternion.identity)>.01f || rect.localScale.x<=0) throw new InvalidOperationException("회전 없는 양수 배율 상판이 필요합니다.");
        var corners=new Vector3[4]; rect.GetWorldCorners(corners);
        float left=canvas.InverseTransformPoint(corners[0]).x;
        float right=canvas.InverseTransformPoint(corners[3]).x;
        float[] widths={ (left-canvas.rect.xMin)/rect.localScale.x,(canvas.rect.xMax-right)/rect.localScale.x };
        if(widths.Any(w=>w<=0 || w>rect.rect.width*.25f)) throw new InvalidOperationException("화면 양옆 여백이 상판 폭의 0~25%인 배치가 필요합니다.");
        string directory="output/stage2-counter-extension/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Extend Stage 2 counter sides");
        var layers=stage.layers.ToList();
        var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(counter.sprite);
        for(int side=0;side<2;side++)
        {
            string name=side==0 ? "CounterLeftExtension" : "CounterRightExtension";
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));
            Undo.RegisterCreatedObjectUndo(go,"Extend Stage 2 counter sides");
            var image=go.GetComponent<RawImage>();
            image.rectTransform.SetParent(rect,false);
            image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(side,1);
            image.rectTransform.pivot=new Vector2(1-side,1);
            image.rectTransform.anchoredPosition=Vector2.zero;
            image.rectTransform.sizeDelta=new Vector2(widths[side],rect.rect.height);
            image.texture=counter.sprite.texture; image.color=counter.color; image.raycastTarget=false;
            float span=(uv.z-uv.x)*widths[side]/rect.rect.width;
            // 접점에서 같은 원본 픽셀을 만나도록 반사합니다. 원본 PNG와 중앙 상판은 변경하지 않습니다.
            image.uvRect=new Rect(side==0 ? uv.x+span : uv.z,uv.y,-span,uv.w-uv.y);
            var extension=JsonUtility.FromJson<DystopiaPixelStage.Layer>(JsonUtility.ToJson(layer));
            extension.source=image; extension.normalMap=null; extension.normalSprite=null;
            extension.contactShadow=Vector4.zero;
            layers.Insert(layers.IndexOf(layer),extension);
            Dirty(image);
        }
        stage.layers=layers.ToArray(); Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification copy failed.");
        File.WriteAllText("output/stage2-counter-extension/latest.txt",directory);
        Debug.Log("Stage 2 counter extended; original RectTransform preserved. Local side widths: "+widths[0]+", "+widths[1]+". Scene left unsaved.");
    }

    private static DystopiaPixelStage FindStage()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null || stage.gameObject.scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        return stage;
    }

    /// <summary>비활성 오브젝트도 찾도록 루트부터 경로를 따라갑니다.</summary>
    internal static GameObject FindInScene(Scene scene,string path)
    {
        string[] parts=path.Split('/');
        var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name==parts[0]);
        if(root==null) throw new InvalidOperationException("Missing "+parts[0]+" in "+scene.name);
        var current=root.transform;
        for(int i=1;i<parts.Length;i++)
        {
            current=current.Find(parts[i]);
            if(current==null) throw new InvalidOperationException("Missing "+path+" in "+scene.name);
        }
        return current.gameObject;
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().SingleOrDefault();
        if(sprite==null) throw new InvalidOperationException("Run Import Facility Artwork first: "+path);
        return sprite;
    }

    /// <summary>Point·비압축 Sprite로 임포트하고 필요하면 투명 여백을 제외한 단일 Sprite 영역을 설정합니다.</summary>
    /// <param name="path">프로젝트 내 PNG 경로입니다.</param>
    /// <param name="cropToOpaque">알파 경계로 Sprite 영역을 줄일지 여부입니다.</param>
    private static void ImportSprite(string path,bool cropToOpaque)
    {
        if(!File.Exists(path)) throw new FileNotFoundException(path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100; importer.filterMode=FilterMode.Bilinear; importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048; importer.alphaIsTransparency=true;
        if(!cropToOpaque) { importer.SaveAndReimport(); return; }
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        Rect crop;
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(path))) throw new IOException("Could not read "+path);
            var pixels=texture.GetPixels32();
            int minX=texture.width,minY=texture.height,maxX=-1,maxY=-1;
            for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
                if(pixels[y*texture.width+x].a>8) { minX=Math.Min(minX,x); minY=Math.Min(minY,y); maxX=Math.Max(maxX,x); maxY=Math.Max(maxY,y); }
            if(maxX<minX) throw new InvalidOperationException("Empty PNG: "+path);
            crop=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        importer.spriteImportMode=SpriteImportMode.Multiple; importer.SaveAndReimport();
        var factory=new SpriteDataProviderFactories(); factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous=provider.GetSpriteRects().FirstOrDefault();
        var spriteRect=new SpriteRect { name=Path.GetFileNameWithoutExtension(path),rect=crop,pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=previous!=null ? previous.spriteID : GUID.Generate() };
        provider.SetSpriteRects(new[] { spriteRect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(spriteRect.name,spriteRect.spriteID) });
        provider.Apply(); importer.SaveAndReimport();
    }
}
