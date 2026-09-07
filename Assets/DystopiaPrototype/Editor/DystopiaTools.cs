using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>승인된 전용 경로에서만 Scene을 제작하고 검사하는 Editor 진입점입니다.</summary>
public static class DystopiaTools
{
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
