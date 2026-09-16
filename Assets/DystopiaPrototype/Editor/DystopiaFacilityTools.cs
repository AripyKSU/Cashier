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
    /// <summary>요청한 Stage 1 기준을 저장 없이 검사하고 격리된 렌더를 캡처합니다. 검증 후 제거합니다.</summary>
    [MenuItem("Dystopia/설비/Verify Requested Stage 1")]
    public static void VerifyRequestedStage1()
    {
        const string directory="output/stage1-scale-match-20260916";
        var preview=EditorSceneManager.OpenPreviewScene("Assets/DystopiaPrototype/Editor/References/Stage1Reference.unity");
        DystopiaPixelStage stage=null;
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        try
        {
            stage=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DystopiaPixelStage>(true)).Single();
            var report=new System.Text.StringBuilder();
            foreach(var layer in stage.layers.Where(l=>l.source!=null))
            {
                var rect=layer.source.rectTransform; var image=layer.source as Image;
                if(image!=null && image.sprite==null && image.gameObject.activeInHierarchy) report.AppendLine("EMPTY SPRITE: "+image.name);
                report.AppendLine(rect.name+" pos="+rect.anchoredPosition+" size="+rect.sizeDelta+" scale="+rect.localScale+" sprite="+(image!=null && image.sprite!=null ? image.sprite.name : "none"));
            }
            File.WriteAllText(directory+"/unity-verification.txt",report.ToString());
            // 미리보기 메쉬를 열린 씬의 임시 렌더 위치와 분리해 캡처하고 즉시 해제합니다.
            typeof(DystopiaPixelStage).GetMethod("Build",flags).Invoke(stage,null);
            var renderRoot=(GameObject)typeof(DystopiaPixelStage).GetField("renderRoot",flags).GetValue(stage);
            renderRoot.transform.position=new Vector3(20000,20000,0);
            typeof(DystopiaPixelStage).GetMethod("LateUpdate",flags).Invoke(stage,null);
            var texture=(RenderTexture)typeof(DystopiaPixelStage).GetField("texture",flags).GetValue(stage);
            if(texture==null) throw new InvalidOperationException("Preview render unavailable.");
            var previous=RenderTexture.active; var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply();
                File.WriteAllBytes(directory+"/stage1-preview.png",pixels.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        }
        finally
        {
            if(stage!=null) typeof(DystopiaPixelStage).GetMethod("Release",flags).Invoke(stage,null);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

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

    /// <summary>현재 1단계 편집 상태를 메인 씬과 단계 기준에 함께 저장하고 2·3·1단계 전환 뒤 동일성을 검증합니다.</summary>
    [MenuItem("Dystopia/설비/Save And Verify Current Stage 1")]
    public static void SaveAndVerifyCurrentStage1()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage1CounterTop") throw new InvalidOperationException("Stage 1 only.");
        var scene=stage.gameObject.scene;
        string directory="output/stage1-persistence/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        string mainPath=Root+"Scenes/DystopiaVerticalSlice.unity";
        string referencePath=Root+"Editor/References/Stage1Reference.unity";
        File.Copy(mainPath,directory+"/previous-main.unity");
        File.Copy(referencePath,directory+"/previous-reference.unity");
        if(!EditorSceneManager.SaveScene(scene,directory+"/live-before-save.unity",true)) throw new IOException("Live Stage 1 backup failed.");
        string before=Stage1PersistenceState(stage);
        File.WriteAllText(directory+"/before.txt",before);

        SaveReference(1);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Main scene save failed.");
        AssetDatabase.SaveAssets();

        DystopiaTools.ApplyApprovedStageReference(2);
        DystopiaTools.ApplyApprovedStageReference(3);
        DystopiaTools.ApplyApprovedStageReference(1);
        stage=FindStage();
        string after=Stage1PersistenceState(stage);
        File.WriteAllText(directory+"/after.txt",after);
        if(!String.Equals(before,after,StringComparison.Ordinal))
        {
            File.WriteAllText(directory+"/FAILED.txt","Stage 1 state changed during 1→2→3→1 verification.");
            EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Single);
            throw new InvalidOperationException("Stage 1 persistence verification failed. The saved pre-test scene was reloaded. See "+directory);
        }

        if(!EditorSceneManager.SaveScene(stage.gameObject.scene)) throw new IOException("Verified Stage 1 scene save failed.");
        AssetDatabase.SaveAssets();
        File.WriteAllText(directory+"/VERIFIED.txt","Main scene and Stage1Reference saved. 1→2→3→1 state comparison passed.\n"+DateTime.Now.ToString("O"));
        File.WriteAllText("output/stage1-persistence/latest.txt",directory);
        Debug.Log("Stage 1 main scene and reference saved; 1→2→3→1 persistence verified: "+directory);
    }

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
        var uiShadows=layer.source.GetComponents<UnityEngine.UI.Shadow>();
        foreach(var uiShadow in uiShadows)
        {
            Undo.RecordObject(uiShadow,"Disable clock UI shadow");
            uiShadow.enabled=false;
            Dirty(uiShadow);
        }
        Dirty(stage);
        // 접촉 그림자 렌더러는 최초 생성 때 만들어지므로 임시 렌더를 다시 만들어 즉시 사라지게 합니다.
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("CounterClock shadow removed (contactShadow, bottomShade"+(uiShadows.Length>0 ? ", all UI Shadows" : "")+").");
    }

    /// <summary>마지막으로 저장된 어두운 1단계 시계 표현을 복구하고 시계의 접촉 그림자를 현재 설비와 상자에 옮깁니다. 배치는 변경하지 않습니다.</summary>
    [MenuItem("Dystopia/설비/Stage 1: Restore Dark Clock And Transfer Shadow")]
    public static void RestoreDarkClockAndTransferShadow()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage1CounterTop") throw new InvalidOperationException("Stage 1 only.");
        string directory="output/stage1-clock-shadow-fix/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");

        var clock=stage.layers.Single(l=>l.source!=null && l.source.name=="CounterClock");
        var image=(Image)clock.source;
        const string clockPath="Assets/Textures/art/Facility/Clock/Stage1BasicClock.png";
        AssetDatabase.ImportAsset(clockPath);
        Undo.RecordObjects(new UnityEngine.Object[]{stage,image},"Restore dark clock and soft prop shadows");
        image.sprite=AssetDatabase.LoadAllAssetsAtPath(clockPath).OfType<Sprite>().Single();
        image.color=Color.white;
        image.preserveAspect=true;
        clock.normalSprite=image.sprite;
        clock.normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/art/Facility/Clock/Stage1BasicClockNormal.png");
        var digits=image.GetComponentInChildren<Text>(true);
        if(digits!=null)
        {
            Undo.RecordObject(digits,"Restore dark clock digits");
            digits.color=new Color(.36f,.49f,.35f,1);
            Dirty(digits);
        }
        Dirty(image);

        foreach(var layer in stage.layers.Where(l=>l.source!=null && l.source.gameObject.activeInHierarchy && (l.source.name.StartsWith("Facility",StringComparison.Ordinal) || l.source.name=="FrontContainer")))
        {
            layer.softContactShadow=false;
            layer.projectedContactShadow=true;
            layer.contactShadow=new Vector4(.5f,0,1f,.7f);
            layer.bottomShade=.12f;
            foreach(var shadow in layer.source.GetComponents<Shadow>().Where(s=>!(s is Outline)))
            {
                Undo.RecordObject(shadow,"Use soft prop shadow");
                shadow.enabled=false;
                Dirty(shadow);
            }
            layer.source.SetVerticesDirty();
        }

        clock.softContactShadow=false;
        clock.projectedContactShadow=false;
        clock.contactShadow=Vector4.zero;
        clock.bottomShade=0;
        foreach(var shadow in clock.source.GetComponents<Shadow>())
        {
            Undo.RecordObject(shadow,"Remove clock shadow");
            shadow.enabled=false;
            Dirty(shadow);
        }
        clock.source.SetVerticesDirty();
        Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("Dark Stage 1 clock restored; short soft shadows applied to visible facilities and FrontContainer. Layout preserved; scene left unsaved.");
    }

    /// <summary>현재 1단계 설비의 얇은 윤곽과 설비·상자의 접촉 그림자만 적용합니다. 배치를 보존합니다.</summary>
    [MenuItem("Dystopia/설비/Stage 1: Equipment Outlines And Shadows")]
    public static void ApplyStage1EquipmentOutlinesAndShadows()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage1CounterTop") throw new InvalidOperationException("Stage 1 only.");
        string directory="output/stage1-equipment-shadows/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Stage 1 equipment contact shadows");
        foreach(var layer in stage.layers.Where(l=>l.source!=null && (l.source.name.StartsWith("Facility",StringComparison.Ordinal) || l.source.name=="FrontContainer")))
        {
            layer.contactShadow=new Vector4(.5f,0,1.1f,.22f);
            layer.bottomShade=.22f;
            if(layer.source.name.StartsWith("Facility",StringComparison.Ordinal))
            {
                var outline=layer.source.GetComponent<Outline>();
                if(outline==null) outline=Undo.AddComponent<Outline>(layer.source.gameObject);
                Undo.RecordObject(outline,"Thin equipment outline");
                outline.enabled=true;
                outline.effectColor=new Color(.035f,.03f,.025f,.8f);
                outline.effectDistance=new Vector2(1,-1);
                outline.useGraphicAlpha=true;
                // 캡처보다 먼저 윤곽을 계산하여 픽셀 렌더에 포함합니다.
                var capture=layer.source.GetComponent<DystopiaPixelSource>();
                while(capture!=null && Array.IndexOf(layer.source.GetComponents<Component>(),outline)>Array.IndexOf(layer.source.GetComponents<Component>(),capture))
                    if(!UnityEditorInternal.ComponentUtility.MoveComponentUp(outline)) break;
                Dirty(outline);
            }
            layer.source.SetVerticesDirty();
        }
        Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("Stage 1 equipment outlines and crate/contact shadows applied; transforms preserved.");
    }

    /// <summary>현재 1단계 상자와 설비의 밑면에 진한 그림자를 추가하며 기존 배치와 윤곽은 유지합니다.</summary>
    [MenuItem("Dystopia/설비/Stage 1: Visible Prop Shadows")]
    public static void ApplyVisibleStage1PropShadows()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage1CounterTop") throw new InvalidOperationException("Stage 1 only.");
        string directory="output/stage1-visible-shadows/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Visible prop shadows");
        foreach(var layer in stage.layers.Where(l=>l.source!=null && (l.source.name.StartsWith("Facility",StringComparison.Ordinal) || l.source.name=="FrontContainer")))
        {
            var shadow=layer.source.GetComponents<Shadow>().FirstOrDefault(s=>!(s is Outline));
            if(shadow!=null)
            {
                Undo.RecordObject(shadow,"Remove hard offset shadow");
                shadow.enabled=false;
                Dirty(shadow);
            }
            // 원본 알파 실루엣을 광원 반대 방향으로 투영하여 물품 형태를 따르는 하드 엣지 그림자로 교체합니다.
            layer.softContactShadow=false;
            layer.projectedContactShadow=true;
            layer.contactShadow=new Vector4(.5f,0,1f,.7f);
            layer.bottomShade=.12f;
            layer.source.SetVerticesDirty();
        }
        Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
    }

    /// <summary>현재 1단계 설비의 강한 투영 그림자만 제거하고 상자처럼 옅은 바닥 그림자를 적용합니다. 상자와 배치는 유지합니다.</summary>
    [MenuItem("Dystopia/설비/Stage 1: Soft Facility Shadows Like Container")]
    public static void ApplySoftStage1FacilityShadows()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage1CounterTop") throw new InvalidOperationException("Stage 1 only.");
        string directory="output/stage1-facility-soft-shadows/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Soft Stage 1 facility shadows");
        foreach(var layer in stage.layers.Where(l=>l.source!=null && l.source.name.StartsWith("Facility",StringComparison.Ordinal)))
        {
            layer.projectedContactShadow=false;
            layer.softContactShadow=true;
            layer.contactShadow=new Vector4(.5f,0,1.12f,.24f);
            layer.bottomShade=.12f;
            foreach(var shadow in layer.source.GetComponents<Shadow>().Where(s=>!(s is Outline)))
            {
                Undo.RecordObject(shadow,"Disable hard facility shadow");
                shadow.enabled=false;
                Dirty(shadow);
            }
            layer.source.SetVerticesDirty();
        }
        Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("Stage 1 facility shadows softened; FrontContainer and layout preserved.");
    }

    /// <summary>현재 단계의 설비 6종과 정면 상자에 도트용 짧은 접지 그림자를 켭니다. 배치와 그림은 보존합니다.</summary>
    [MenuItem("Dystopia/설비/Pixel Contact Shadows (Facilities + Crate, Current Stage)")]
    public static void ApplySoftStage3Shadows()
    {
        var stage=FindStage();
        // 현재 적용된 단계와 무관하게 설비 6종과 정면 상자에 같은 접지 그림자를 적용합니다. 단계 전환 후에도 유지하려면 Save Stage N Reference로 기준에 반영합니다.
        string directory="output/stage-soft-shadows/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Soft Stage 3 shadows");
        foreach(var layer in stage.layers.Where(l=>l.source!=null && (l.source.name.StartsWith("Facility",StringComparison.Ordinal) || l.source.name=="FrontContainer")))
        {
            // 도트 화면에 맞춰 번짐 없는 짧은 접지 그림자(밑면 실루엣 투영)만 사용합니다.
            layer.projectedContactShadow=false;
            layer.softContactShadow=false;
            layer.contactShadow=new Vector4(.5f,0,1.04f,.10f);
            layer.bottomShade=.12f;
            foreach(var shadow in layer.source.GetComponents<Shadow>().Where(s=>!(s is Outline)))
            {
                Undo.RecordObject(shadow,"Disable hard shadow");
                shadow.enabled=false;
                Dirty(shadow);
            }
            layer.source.SetVerticesDirty();
        }
        Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log("Pixel contact shadows applied to facilities and crate; layout preserved. Scene left unsaved.");
    }

    /// <summary>1단계 나무 지붕의 아래쪽에만 얇은 외곽선을 적용하며 배치를 보존합니다.</summary>
    [MenuItem("Dystopia/설비/Stage 1: Roof Bottom Outline")]
    public static void ApplyStage1RoofBottomOutline()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage1CounterTop") throw new InvalidOperationException("Stage 1 only.");
        var canopy=stage.layers.Single(l=>l.source!=null && l.source.name=="Canopy").source;
        var shadow=canopy.GetComponent<Shadow>();
        if(shadow==null) shadow=Undo.AddComponent<Shadow>(canopy.gameObject);
        Undo.RecordObject(shadow,"Roof bottom outline");
        shadow.enabled=true;
        shadow.effectColor=new Color(.035f,.03f,.025f,.72f);
        shadow.effectDistance=new Vector2(0,-1);
        shadow.useGraphicAlpha=true;
        var capture=canopy.GetComponent<DystopiaPixelSource>();
        while(capture!=null && Array.IndexOf(canopy.GetComponents<Component>(),shadow)>Array.IndexOf(canopy.GetComponents<Component>(),capture))
            if(!UnityEditorInternal.ComponentUtility.MoveComponentUp(shadow)) break;
        Dirty(shadow); canopy.SetVerticesDirty(); Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
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

    /// <summary>1단계 버튼 전환 전후에 보존해야 하는 배치·그림·레이어·그림자 상태를 결정적인 문자열로 만듭니다.</summary>
    /// <param name="stage">현재 픽셀 스테이지입니다.</param>
    /// <returns>비교 가능한 1단계 상태 문자열입니다.</returns>
    private static string Stage1PersistenceState(DystopiaPixelStage stage)
    {
        string[] names={"Counter","Canopy","Stage3Ceiling","Stage3LeftPillar","Stage3RightPillar","FrontContainer","CounterClock","FacilityFoodShelf","FacilityMedicineCabinet","FacilityToolBench","FacilityPowerCommunications","FacilityNuclearProtection","FacilityPrecisionElectronics"};
        var report=new System.Text.StringBuilder();
        foreach(var layer in stage.layers.Where(l=>l.source!=null && names.Contains(l.source.name)).OrderBy(l=>l.source.name,StringComparer.Ordinal))
        {
            var image=layer.source as Image;
            var rect=layer.source.rectTransform;
            report.Append(layer.source.name).Append("|order=").Append(Array.IndexOf(stage.layers,layer));
            report.Append("|parent=").Append(TransformPath(rect.parent)).Append("|sibling=").Append(rect.GetSiblingIndex());
            report.Append("|anchor=").Append(rect.anchorMin).Append('/').Append(rect.anchorMax).Append("|pivot=").Append(rect.pivot);
            report.Append("|pos=").Append(rect.anchoredPosition3D).Append("|size=").Append(rect.sizeDelta).Append("|scale=").Append(rect.localScale).Append("|rot=").Append(rect.localRotation);
            report.Append("|active=").Append(layer.source.gameObject.activeSelf).Append("|enabled=").Append(layer.source.enabled).Append("|color=").Append(layer.source.color);
            if(image!=null) report.Append("|sprite=").Append(AssetDatabase.GetAssetPath(image.sprite)).Append(':').Append(image.sprite!=null ? image.sprite.name : "null").Append("|aspect=").Append(image.preserveAspect);
            report.Append("|surface=").Append(layer.surface).Append("|normal=").Append(AssetDatabase.GetAssetPath(layer.normalMap)).Append(':').Append(AssetDatabase.GetAssetPath(layer.normalSprite));
            report.Append("|light=").Append(layer.roomResponse).Append(',').Append(layer.lampResponse).Append(',').Append(layer.rimWidthPixels).Append(',').Append(layer.rimResponse).Append(',').Append(layer.normalResponse).Append(',').Append(layer.highlightResponse).Append(',').Append(layer.specularResponse).Append(',').Append(layer.emission);
            report.Append("|shadow=").Append(layer.bottomShade).Append(',').Append(layer.contactShadow).Append(',').Append(layer.softContactShadow).Append(',').Append(layer.projectedContactShadow);
            foreach(var effect in layer.source.GetComponents<Shadow>())
                report.Append("|effect=").Append(effect.GetType().FullName).Append(',').Append(effect.enabled).Append(',').Append(effect.effectColor).Append(',').Append(effect.effectDistance).Append(',').Append(effect.useGraphicAlpha);
            if(layer.source.name=="CounterClock")
            {
                var digits=layer.source.GetComponentInChildren<Text>(true);
                if(digits!=null) report.Append("|digits=").Append(digits.rectTransform.anchoredPosition3D).Append(',').Append(digits.rectTransform.sizeDelta).Append(',').Append(digits.fontSize).Append(',').Append(digits.color).Append(',').Append(digits.alignment);
            }
            report.AppendLine();
        }
        report.Append("layer-order=").Append(String.Join(",",stage.layers.Where(l=>l.source!=null).Select(l=>l.source.name)));
        return report.ToString();
    }

    /// <summary>부모 관계 검증을 위해 루트부터 현재 Transform까지의 이름 경로를 만듭니다.</summary>
    /// <param name="transform">검사할 Transform입니다.</param>
    /// <returns>슬래시로 구분한 계층 경로입니다.</returns>
    private static string TransformPath(Transform transform)
    {
        if(transform==null) return String.Empty;
        return transform.parent==null ? transform.name : TransformPath(transform.parent)+"/"+transform.name;
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

    /// <summary>현재 상판의 금속 반응을 기둥과 천장에 맞추며 원본 그림과 모든 배치를 보존합니다.</summary>
    [MenuItem("Dystopia/설비/Match Current Stage 2 Frame Metal")]
    public static void MatchStage2FrameMetal()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(!(counter.source is Image image) || image.sprite==null || image.sprite.name!="Stage2CounterTop") throw new InvalidOperationException("Stage 2 상판을 먼저 확인하세요.");
        string[] names={ "Canopy","Stage3Ceiling","Stage3LeftPillar","Stage3RightPillar" };
        var frames=stage.layers.Where(l=>l.source!=null && names.Contains(l.source.name) && l.source.gameObject.activeInHierarchy).ToArray();
        if(frames.Length<3) throw new InvalidOperationException("천장과 양쪽 기둥이 필요합니다.");
        string directory="output/stage2-material-match/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Match Stage 2 frame metal");
        foreach(var frame in frames)
        {
            Undo.RecordObject(frame.source,"Match frame steel tint");
            // 프레임 원화의 누런 기를 줄이고 상판과 동일한 회색 철판의 조명 반응을 사용합니다.
            frame.source.color=new Color(.88f,.94f,1f,frame.source.color.a);
            frame.surface=counter.surface;
            frame.roomResponse=counter.roomResponse; frame.lampResponse=counter.lampResponse;
            frame.rimWidthPixels=counter.rimWidthPixels; frame.rimResponse=counter.rimResponse;
            frame.normalResponse=counter.normalResponse; frame.highlightResponse=counter.highlightResponse;
            frame.specularResponse=counter.specularResponse;
            Dirty(frame.source);
        }
        Dirty(stage); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification copy failed.");
        File.WriteAllText("output/stage2-material-match/latest.txt",directory);
        Debug.Log("Matched Stage 2 frame metal on "+frames.Length+" layers; all transforms and original textures preserved. Scene left unsaved.");
    }

    /// <summary>현재 씬 배치와 렌더 설정을 변경 없이 인계용 사본과 텍스트로 기록합니다.</summary>
    [MenuItem("Dystopia/설비/Inspect Current Composition")]
    public static void InspectCurrentComposition()
    {
        var stage=FindStage();
        const string directory="output/stage2-composition";
        Directory.CreateDirectory(directory);
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before-"+stamp+".unity",true);
        var report=new System.Text.StringBuilder();
        foreach(var layer in stage.layers.Where(l=>l.source!=null))
        {
            var rect=layer.source.rectTransform; var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            report.AppendLine(layer.source.name+" active="+layer.source.gameObject.activeInHierarchy+" parent="+rect.parent.name+" pos="+rect.anchoredPosition+" size="+rect.sizeDelta+" scale="+rect.localScale+" color="+layer.source.color);
            report.AppendLine(" canvas bounds="+stage.frontCanvas.InverseTransformPoint(corners[0])+" / "+stage.frontCanvas.InverseTransformPoint(corners[2]));
            report.AppendLine(JsonUtility.ToJson(layer));
        }
        File.WriteAllText(directory+"/inspect.txt",report.ToString());
        File.WriteAllText(directory+"/before-path.txt",directory+"/before-"+stamp+".unity");
    }

    /// <summary>현재 2단계의 시계·설비 접점과 프레임 안쪽 배경을 조정합니다. 기존 손님·가판·소품 배치는 유지합니다.</summary>
    [MenuItem("Dystopia/설비/Fit Current Stage 2 Background And Contacts")]
    public static void FitStage2BackgroundAndContacts()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage2CounterTop") throw new InvalidOperationException("Stage 2 상판이 필요합니다.");
        var canvas=stage.frontCanvas;
        var background=stage.layers.Single(l=>l.source!=null && l.source.name=="FarBackground").source.rectTransform;
        float backgroundScale=1176f/background.rect.width;
        Vector3 backgroundOrigin=background.localPosition;
        string directory="output/stage2-composition/fit-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Stage 2 material and contact shadows");
        var clock=stage.layers.Single(l=>l.source!=null && l.source.name=="CounterClock");
        Undo.RecordObject(clock.source,"Clock steel tint");
        clock.source.color=new Color(.88f,.94f,1,clock.source.color.a);
        clock.roomResponse=counter.roomResponse; clock.lampResponse=counter.lampResponse;
        clock.rimResponse=counter.rimResponse; clock.normalResponse=counter.normalResponse;
        clock.highlightResponse=counter.highlightResponse; clock.specularResponse=counter.specularResponse;
        clock.contactShadow=Vector4.zero; clock.bottomShade=0;
        foreach(var effect in clock.source.GetComponents<Shadow>()) { Undo.RecordObject(effect,"Disable clock shadow"); effect.enabled=false; Dirty(effect); }
        Dirty(clock.source);
        foreach(var layer in stage.layers.Where(l=>l.source!=null && l.source.name.StartsWith("Facility",StringComparison.Ordinal) && l.source.gameObject.activeInHierarchy))
        {
            layer.contactShadow=new Vector4(.5f,0,1.35f,.5f);
            layer.bottomShade=.38f;
            var shadow=layer.source.GetComponent<Shadow>();
            if(shadow==null) shadow=Undo.AddComponent<Shadow>(layer.source.gameObject);
            Undo.RecordObject(shadow,"Facility bottom contour");
            shadow.enabled=true; shadow.effectColor=new Color(.015f,.012f,.01f,1); shadow.effectDistance=new Vector2(0,-2); shadow.useGraphicAlpha=true;
            // 픽셀 캡처 전에 윤곽 효과를 계산해야 최종 렌더에도 반영됩니다.
            var capture=layer.source.GetComponent<DystopiaPixelSource>();
            while(capture!=null && Array.IndexOf(layer.source.GetComponents<Component>(),shadow)>Array.IndexOf(layer.source.GetComponents<Component>(),capture))
                if(!UnityEditorInternal.ComponentUtility.MoveComponentUp(shadow)) break;
            Dirty(shadow); layer.source.SetVerticesDirty();
        }
        string[] names={ "FarBackground","DawnBackground","SunsetBackground","EveningBackground","CityLights","MidBackground","Fog_Back","Fog_Mid","Fog_Front","LeftChimneySmoke","RightChimneySmoke","LeftWatchGuard","RightWatchGuard","LeftSearchlight","RightSearchlight","CrowdRow0","CrowdRow1","CrowdRow2","Barricade","LeftWatchTower","RightWatchTower" };
        // 실기둥 안쪽 x=52~1228, 천장 뒤 y=42에 넣습니다. 현재 배경 기준으로 계산하여 재실행해도 누적 축소하지 않습니다.
        float scale=backgroundScale;
        foreach(var rect in stage.layers.Where(l=>l.source!=null && names.Contains(l.source.name)).Select(l=>l.source.rectTransform).Distinct())
        {
            if(rect.parent!=canvas) throw new InvalidOperationException("Unexpected background parent: "+rect.name);
            Undo.RecordObject(rect,"Fit background inside frame");
            Vector3 point=rect.localPosition;
            float width=rect.rect.width*scale, height=rect.rect.height*scale;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            rect.localPosition=new Vector3(canvas.rect.xMin+52+(point.x-backgroundOrigin.x)*scale,canvas.rect.yMax-42+(point.y-backgroundOrigin.y)*scale,point.z);
            Dirty(rect);
        }
        // 총구 불꽃의 자식들은 애니메이션 위치를 따르므로 크기만 같은 비율로 맞춥니다.
        foreach(string name in new[] { "WatchMuzzleFlash0","WatchMuzzleFlash1" })
        {
            var root=canvas.Find(name);
            if(root==null) continue;
            foreach(var rect in root.GetComponentsInChildren<RectTransform>(true).Where(r=>r!=root))
            { Undo.RecordObject(rect,"Scale guard flash"); rect.sizeDelta*=scale; rect.anchoredPosition*=scale; Dirty(rect); }
        }
        Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification copy failed.");
        File.WriteAllText("output/stage2-composition/fit-path.txt",directory);
    }

    /// <summary>기존 Sprite 영역을 동일한 캔버스의 새 텍스처에 복사하여 씬 배치와 조각 이름을 보존합니다.</summary>
    /// <param name="sourcePath">현재 사용하는 원본 Sprite sheet입니다.</param>
    /// <param name="targetPath">같은 크기의 수정 PNG입니다.</param>
    private static void ImportMatchingSheet(string sourcePath,string targetPath)
    {
        AssetDatabase.ImportAsset(targetPath,ImportAssetOptions.ForceSynchronousImport);
        var source=(TextureImporter)AssetImporter.GetAtPath(sourcePath);
        var target=(TextureImporter)AssetImporter.GetAtPath(targetPath);
        var sourceTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        var targetTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
        if(sourceTexture.width!=targetTexture.width || sourceTexture.height!=targetTexture.height) throw new InvalidOperationException("Sprite sheet canvas mismatch: "+targetPath);
        var settings=new TextureImporterSettings(); source.ReadTextureSettings(settings); target.SetTextureSettings(settings);
        target.maxTextureSize=source.maxTextureSize; target.textureCompression=TextureImporterCompression.Uncompressed;
        target.SaveAndReimport();
        var factory=new SpriteDataProviderFactories(); factory.Init();
        var from=factory.GetSpriteEditorDataProviderFromObject(source); from.InitSpriteEditorDataProvider();
        var to=factory.GetSpriteEditorDataProviderFromObject(target); to.InitSpriteEditorDataProvider();
        var rects=from.GetSpriteRects(); to.SetSpriteRects(rects);
        to.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        to.Apply(); target.SaveAndReimport();
    }

    /// <summary>현재 Stage 2에만 녹슨 프레임·시계·상자 텍스처를 연결하고 설비 밑면 그림자를 검증용 사본에 기록합니다.</summary>
    [MenuItem("Dystopia/설비/Apply Stage 2 Rusted Textures")]
    public static void ApplyStage2RustedTextures()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage2CounterTop") throw new InvalidOperationException("Current scene must be Stage 2.");
        string directory="output/stage2-rust-contact/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        string framePath=Art+"Facility/Frame/Stage2RustedFrame.png",clockPath=Art+"Facility/Clock/Stage2RustedClock.png";
        ImportMatchingSheet("Assets/Textures/art/Facility/Frame/Stage2Shop.png",framePath);
        ImportMatchingSheet("Assets/Textures/art/Facility/Clock/Stage2Clock.png",clockPath);
        ImportSprite(Crates+"Stage2RustedCrateClosed.png",true); ImportSprite(Crates+"Stage2RustedCrateOpen.png",true);
        Undo.RecordObject(stage,"Stage 2 rusted textures");
        foreach(var layer in stage.layers.Where(l=>l.source!=null && new[]{"Canopy","Stage3LeftPillar","Stage3RightPillar","CounterClock","FrontContainer"}.Contains(l.source.name)))
        {
            var image=(Image)layer.source;
            string path=image.name=="CounterClock" ? clockPath : framePath;
            Sprite replacement=image.name=="FrontContainer" ? LoadSprite(Crates+"Stage2RustedCrateClosed.png") : AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name==image.sprite.name);
            Undo.RecordObject(image,"Replace Stage 2 metal texture"); image.sprite=replacement; image.color=Color.white;
            layer.normalMap=null; layer.normalSprite=null; layer.normalResponse=0;
            Dirty(image);
        }
        var pouring=FindInScene(stage.gameObject.scene,"TopDownCheckout/TopDownTestCanvas/WorkViewUI/PouringContainer").GetComponent<Image>();
        Undo.RecordObject(pouring,"Match open crate texture"); pouring.sprite=LoadSprite(Crates+"Stage2RustedCrateOpen.png"); Dirty(pouring);
        foreach(var layer in stage.layers.Where(l=>l.source!=null && l.source.name.StartsWith("Facility",StringComparison.Ordinal) && l.source.gameObject.activeInHierarchy))
            layer.contactShadow=new Vector4(.5f,0,1.15f,.75f);
        Dirty(stage); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification failed.");
        File.WriteAllText("output/stage2-rust-contact/latest.txt",directory);
    }

    /// <summary>명시적 실행으로 승인된 2단계 상판 크기와 프레임 구성을 3단계에 적용합니다. 설비 배치는 보존합니다.</summary>
    [MenuItem("Dystopia/설비/Match Stage 3 To Saved Stage 2")]
    public static void MatchStage3ToSavedStage2()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage3CounterTop") throw new InvalidOperationException("Apply Stage 3 first.");
        string directory="output/stage3-approved-match/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        var preview=EditorSceneManager.OpenPreviewScene(Root+"Editor/References/Stage2Reference.unity");
        try
        {
            var saved=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DystopiaPixelStage>(true)).Single();
            Undo.RecordObject(stage,"Match approved Stage 2 metal");
            foreach(string name in new[]{"Counter","Canopy","Stage3LeftPillar","Stage3RightPillar","CounterClock"})
            {
                var target=stage.layers.Single(l=>l.source!=null && l.source.name==name);
                var reference=saved.layers.Single(l=>l.source!=null && l.source.name==name);
                var image=(Image)target.source; var source=(Image)reference.source;
                CopyAuthoredRect(image.rectTransform,source.rectTransform);
                Undo.RecordObject(image,"Match approved metal");
                if(name=="CounterClock") image.sprite=source.sprite;
                else if(name!="Counter") image.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/art/Facility/Frame/Stage3Shop.png").OfType<Sprite>().Single(s=>s.name==(name=="Canopy" ? "Stage3Ceiling" : name));
                image.color=source.color; image.enabled=source.enabled; image.gameObject.SetActive(source.gameObject.activeSelf);
                foreach(var field in typeof(DystopiaPixelStage.Layer).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                    if(field.Name!="source" && !field.IsNotSerialized) field.SetValue(target,field.GetValue(reference));
                if(name=="CounterClock")
                {
                    var digits=image.GetComponentInChildren<Text>(true); var savedDigits=source.GetComponentInChildren<Text>(true);
                    if(digits!=null && savedDigits!=null) { CopyAuthoredRect(digits.rectTransform,savedDigits.rectTransform); digits.fontSize=savedDigits.fontSize; digits.color=savedDigits.color; digits.alignment=savedDigits.alignment; Dirty(digits); }
                }
                Dirty(image);
            }
            foreach(var layer in stage.layers.Where(l=>l.source!=null && (l.source.name.StartsWith("Facility",StringComparison.Ordinal) || l.source.name=="FrontContainer")))
            {
                layer.source.color=Color.white;
                layer.roomResponse=counter.roomResponse; layer.lampResponse=counter.lampResponse;
                layer.highlightResponse=counter.highlightResponse; layer.specularResponse=counter.specularResponse;
                layer.normalMap=null; layer.normalSprite=null; layer.normalResponse=0;
                layer.contactShadow=new Vector4(.5f,0,1.15f,.75f);
                Dirty(layer.source);
            }
            RestoreCounterExtensions(stage,saved);
            var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(((Image)counter.source).sprite);
            for(int side=0;side<2;side++)
            {
                var image=(RawImage)stage.layers.Single(l=>l.source!=null && l.source.name==(side==0 ? "CounterLeftExtension" : "CounterRightExtension")).source;
                image.texture=((Image)counter.source).sprite.texture;
                float span=(uv.z-uv.x)*image.rectTransform.rect.width/counter.source.rectTransform.rect.width;
                image.uvRect=new Rect(side==0 ? uv.x+span : uv.z,uv.y,-span,uv.w-uv.y); Dirty(image);
            }
            Dirty(stage); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
            if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification copy failed.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    /// <summary>명시적 단계 전환에서 저장된 상판 연장면의 배치·UV·그림을 복원합니다.</summary>
    /// <param name="stage">현재 씬의 렌더러입니다.</param>
    /// <param name="saved">승인된 기준 씬의 렌더러입니다.</param>
    internal static void RestoreCounterExtensions(DystopiaPixelStage stage,DystopiaPixelStage saved)
    {
        var layers=stage.layers.ToList();
        var counter=layers.Single(l=>l.source!=null && l.source.name=="Counter");
        foreach(string name in new[]{"CounterLeftExtension","CounterRightExtension"})
        {
            var reference=saved.layers.SingleOrDefault(l=>l.source!=null && l.source.name==name);
            var target=layers.SingleOrDefault(l=>l.source!=null && l.source.name==name);
            if(reference==null) { if(target!=null) { target.source.gameObject.SetActive(false); Dirty(target.source); } continue; }
            if(target==null)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));
                Undo.RegisterCreatedObjectUndo(go,"Restore approved counter extension");
                go.transform.SetParent(counter.source.transform,false);
                target=new DystopiaPixelStage.Layer { source=go.GetComponent<RawImage>() };
                layers.Insert(layers.IndexOf(counter),target);
            }
            var image=(RawImage)target.source; var source=(RawImage)reference.source;
            CopyAuthoredRect(image.rectTransform,source.rectTransform);
            image.texture=source.texture; image.uvRect=source.uvRect; image.color=source.color; image.raycastTarget=false;
            image.enabled=source.enabled; image.gameObject.SetActive(source.gameObject.activeSelf);
            foreach(var field in typeof(DystopiaPixelStage.Layer).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                if(field.Name!="source" && !field.IsNotSerialized) field.SetValue(target,field.GetValue(reference));
            Dirty(image);
        }
        stage.layers=layers.ToArray(); Dirty(stage);
    }

    /// <summary>승인된 참조의 RectTransform 값을 명시적 편집 작업에서만 복사합니다.</summary>
    /// <param name="target">수정 대상입니다.</param>
    /// <param name="source">저장된 기준입니다.</param>
    private static void CopyAuthoredRect(RectTransform target,RectTransform source)
    {
        Undo.RecordObject(target,"Copy approved rectangle");
        target.anchorMin=source.anchorMin; target.anchorMax=source.anchorMax; target.pivot=source.pivot;
        target.sizeDelta=source.sizeDelta; target.anchoredPosition3D=source.anchoredPosition3D;
        target.localRotation=source.localRotation; target.localScale=source.localScale; Dirty(target);
    }

    /// <summary>Stage 3 배경 레이어를 승인된 Stage 2의 일관된 축소 배치로 맞춥니다. 이미지와 전경 설비 배치는 유지합니다.</summary>
    [MenuItem("Dystopia/설비/Fit Stage 3 Background To Saved Stage 2")]
    public static void FitStage3BackgroundToSavedStage2()
    {
        var stage=FindStage();
        if(((Image)stage.layers.Single(l=>l.source!=null && l.source.name=="Counter").source).sprite.name!="Stage3CounterTop") throw new InvalidOperationException("Stage 3 only.");
        string directory="output/stage3-background-fit/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        var preview=EditorSceneManager.OpenPreviewScene(Root+"Editor/References/Stage2Reference.unity");
        try
        {
            var saved=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DystopiaPixelStage>(true)).Single();
            string[] names={"FarBackground","DawnBackground","SunsetBackground","EveningBackground","CityLights","MidBackground","Fog_Back","Fog_Mid","Fog_Front","LeftChimneySmoke","RightChimneySmoke","LeftWatchGuard","RightWatchGuard","LeftSearchlight","RightSearchlight","CrowdRow0","CrowdRow1","CrowdRow2","Barricade","LeftWatchTower","RightWatchTower"};
            foreach(var layer in stage.layers.Where(l=>l.source!=null && names.Contains(l.source.name)))
            {
                var reference=saved.layers.Single(l=>l.source!=null && l.source.name==layer.source.name);
                CopyAuthoredRect(layer.source.rectTransform,reference.source.rectTransform);
            }
            foreach(string name in new[]{"WatchMuzzleFlash0","WatchMuzzleFlash1"})
            {
                var target=stage.frontCanvas.Find(name); var source=saved.frontCanvas.Find(name);
                if(target==null || source==null) continue;
                var targets=target.GetComponentsInChildren<RectTransform>(true); var sources=source.GetComponentsInChildren<RectTransform>(true);
                if(targets.Length!=sources.Length) throw new InvalidOperationException("Muzzle flash hierarchy mismatch.");
                for(int i=0;i<targets.Length;i++) CopyAuthoredRect(targets[i],sources[i]);
            }
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification failed.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    /// <summary>사용자가 요청한 원본 복원 후 상자 비율·중복 음영·3단계 시계만 교정합니다.</summary>
    [MenuItem("Dystopia/설비/Correct Stage 3 Original Art Presentation")]
    public static void CorrectStage3OriginalArtPresentation()
    {
        var stage=FindStage();
        var counter=stage.layers.Single(l=>l.source!=null && l.source.name=="Counter");
        if(((Image)counter.source).sprite.name!="Stage3CounterTop") throw new InvalidOperationException("Stage 3 only.");
        string directory="output/stage3-correction/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        Undo.RecordObject(stage,"Correct Stage 3 presentation");
        var box=(Image)stage.layers.Single(l=>l.source!=null && l.source.name=="FrontContainer").source;
        var preview=EditorSceneManager.OpenPreviewScene(Root+"Editor/References/Stage2Reference.unity");
        try
        {
            var saved=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DystopiaPixelStage>(true)).Single();
            var reference=saved.layers.Single(l=>l.source!=null && l.source.name=="FrontContainer").source.rectTransform;
            var rect=box.rectTransform;
            Undo.RecordObject(rect,"Restore unsquashed crate ratio");
            float scale=reference.rect.width*reference.localScale.x/rect.rect.width;
            rect.localScale=new Vector3(scale,scale,rect.localScale.z);
            box.preserveAspect=true; Dirty(rect); Dirty(box);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        foreach(var layer in stage.layers.Where(l=>l.source!=null && (l.source.name.StartsWith("Facility",StringComparison.Ordinal) || l.source.name=="FrontContainer")))
        {
            // 원본 PNG가 가진 바닥 그림자를 남기고 겹쳐 그리던 음영만 제거합니다.
            layer.contactShadow=Vector4.zero; layer.bottomShade=0;
            layer.highlightResponse=1;
            foreach(var shadow in layer.source.GetComponents<Shadow>()) { Undo.RecordObject(shadow,"Remove duplicate shadow"); shadow.enabled=false; Dirty(shadow); }
            layer.source.SetVerticesDirty();
        }
        counter.highlightResponse=1;
        foreach(var layer in stage.layers.Where(l=>l.source!=null && l.source.name.StartsWith("Counter",StringComparison.Ordinal) && l.source is RawImage)) layer.highlightResponse=1;
        var clock=stage.layers.Single(l=>l.source!=null && l.source.name=="CounterClock");
        var clockImage=(Image)clock.source; Undo.RecordObject(clockImage,"Restore Stage 3 clock artwork");
        clockImage.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/art/Facility/Clock/Stage3Clock.png").OfType<Sprite>().Single();
        clockImage.color=Color.white; clock.contactShadow=Vector4.zero; clock.bottomShade=0; clock.highlightResponse=1;
        foreach(var shadow in clockImage.GetComponents<Shadow>()) { Undo.RecordObject(shadow,"Remove clock shadow"); shadow.enabled=false; Dirty(shadow); }
        Dirty(clockImage); Dirty(stage);
        typeof(DystopiaPixelStage).GetMethod("Release",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(stage,null);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification copy failed.");
    }

    /// <summary>3단계 시계 원화 비율과 표시창에 맞춰 몸체·숫자를 한 번만 배치합니다.</summary>
    [MenuItem("Dystopia/설비/Fit Current Stage 3 Clock Housing")]
    public static void FitCurrentStage3ClockHousing()
    {
        var stage=FindStage();
        var layer=stage.layers.Single(l=>l.source!=null && l.source.name=="CounterClock");
        var image=(Image)layer.source;
        if(AssetDatabase.GetAssetPath(image.sprite)!="Assets/Textures/art/Facility/Clock/Stage3Clock.png") throw new InvalidOperationException("Stage 3 clock required.");
        string directory="output/stage3-clock-fit/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");
        var rect=image.rectTransform; var digits=image.GetComponentInChildren<Text>(true);
        Undo.RecordObjects(new UnityEngine.Object[]{rect,digits.rectTransform,digits},"Fit Stage 3 clock display");
        // 기존 폭과 배율은 유지하고 실제 Sprite 비율로 높이를 맞춥니다. 상단이 화면 밖으로 잘리지 않게 넣습니다.
        rect.sizeDelta=new Vector2(rect.sizeDelta.x,rect.sizeDelta.x*image.sprite.rect.height/image.sprite.rect.width);
        rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-4);
        var display=digits.rectTransform;
        display.anchorMin=display.anchorMax=new Vector2(0,1); display.pivot=new Vector2(.5f,.5f);
        display.anchoredPosition=new Vector2(rect.rect.width*.5f,-rect.rect.height*.51f);
        display.sizeDelta=new Vector2(rect.rect.width*.64f,rect.rect.height*.44f);
        digits.alignment=TextAnchor.MiddleCenter;
        Dirty(rect); Dirty(display); Dirty(digits);
        image.SetVerticesDirty(); digits.SetVerticesDirty();
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        if(!EditorSceneManager.SaveScene(stage.gameObject.scene,directory+"/after.unity",true)) throw new IOException("Verification copy failed.");
    }

    /// <summary>현재 시계의 배치를 유지하며 건메탈 텍스처만 연결합니다.</summary>
    [MenuItem("Dystopia/설비/Apply Stage 3 Gunmetal Clock Texture")]
    public static void ApplyStage3GunmetalClockTexture()
    {
        var stage=FindStage();
        var image=(Image)stage.layers.Single(l=>l.source!=null && l.source.name=="CounterClock").source;
        string path=Art+"Facility/Clock/Stage3GunmetalClock.png";
        ImportSprite(path,true);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var factory=new SpriteDataProviderFactories(); factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var sprite=provider.GetSpriteRects().Single();
        // 투명 여백을 보완해 기존 94:37 Sprite 비율을 유지하며 RectTransform은 바꾸지 않습니다.
        var rect=sprite.rect; var center=rect.center;
        float aspect=94f/37f;
        if(rect.width/rect.height<aspect) rect.width=rect.height*aspect; else rect.height=rect.width/aspect;
        rect.center=center; sprite.rect=rect;
        provider.SetSpriteRects(new[]{sprite}); provider.Apply();
        importer.filterMode=FilterMode.Point; importer.maxTextureSize=256; importer.SaveAndReimport();
        Undo.RecordObject(image,"Gunmetal clock texture only"); image.sprite=LoadSprite(path); Dirty(image);
        image.SetVerticesDirty(); EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
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
