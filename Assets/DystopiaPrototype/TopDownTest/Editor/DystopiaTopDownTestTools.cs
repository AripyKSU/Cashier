using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>기존 씬을 저장하거나 수정하지 않고 탑다운 테스트 씬과 Play 검증을 관리합니다.</summary>
[InitializeOnLoad]
public static class DystopiaTopDownTestTools
{
    /// <summary>현재 가판 Scene에 사용자 구분봉을 임포트·배치하고 입력 참조를 연결합니다.</summary>
    [MenuItem("Dystopia/Connect Divider Bar")]
    public static void ConnectDividerBar()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before connecting the saved divider.");
        var checkout = UnityEngine.Object.FindFirstObjectByType<DystopiaTopDownTest>(FindObjectsInactive.Include);
        if (checkout == null) throw new InvalidOperationException("Open the checkout scene first.");
        const string path = "Assets/DystopiaPrototype/TopDownTest/Art/DividerBar.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 500;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var existing = checkout.transform.Find("DividerBar");
        var go = existing != null ? existing.gameObject : new GameObject("DividerBar");
        if (existing == null) { Undo.RegisterCreatedObjectUndo(go, "Connect divider"); go.transform.SetParent(checkout.transform, false); }
        var controller = go.GetComponent<DividerBarController2D>() ?? go.AddComponent<DividerBarController2D>();
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        renderer.sortingOrder = 100;
        var capsule = go.GetComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Horizontal;
        capsule.size = new Vector2(3.1f, .32f);
        var fields = new SerializedObject(controller);
        fields.FindProperty("pushableLayers").intValue = 1;
        fields.FindProperty("useMovementBounds").boolValue = true;
        var bounds = checkout.transform.Find("MovementArea").GetComponent<BoxCollider2D>().bounds;
        fields.FindProperty("minWorldPosition").vector2Value = bounds.min;
        fields.FindProperty("maxWorldPosition").vector2Value = bounds.max;
        fields.ApplyModifiedProperties();
        var owner = new SerializedObject(checkout);
        owner.FindProperty("dividerBar").objectReferenceValue = controller;
        owner.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(checkout.gameObject.scene);
        EditorSceneManager.SaveScene(checkout.gameObject.scene);
        Debug.Log("Divider connected: sprite, collider, bounds and checkout input saved.", go);
    }
    private const string TestRoot = "Assets/DystopiaPrototype/TopDownTest";
    private const string ScenePath = TestRoot + "/Scenes/DystopiaTopDownTest.unity";
    private const string EvidencePath = TestRoot + "/Evidence/TopDownPlayVerification.txt";
    private const string ScreenshotPath = TestRoot + "/Evidence/TopDownPlay.png";
    private const string PendingKey = "DystopiaTopDownTest.VerificationPending";
    private const string CleanupKey = "DystopiaTopDownTest.CleanupPending";
    private const string OriginalSceneKey = "DystopiaTopDownTest.OriginalScene";
    private static readonly StringBuilder Report = new StringBuilder();
    private static IEnumerator verification;
    private static double nextStep;
    private static double deadline;
    private static Scene loadedTestScene;
    private static Scene originalActiveScene;

    /// <summary>Play 진입 domain reload 뒤에도 검증 요청을 복원하도록 상태 이벤트를 등록합니다.</summary>
    static DystopiaTopDownTestTools()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    /// <summary>신규 이미지 Import 설정을 적용하고 별도 테스트 씬 자산만 생성합니다.</summary>
    [MenuItem("Dystopia/Top Down Test/Create Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory(TestRoot + "/Scenes");
        Directory.CreateDirectory(TestRoot + "/Evidence");
        ConfigureArtImporters();
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "DystopiaTopDownTest";
        SceneManager.SetActiveScene(scene);
        var root = new GameObject("DystopiaTopDownTest");
        root.AddComponent<DystopiaTopDownTest>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TopDownTest] Created isolated scene: {ScenePath}");
    }

    /// <summary>새 테스트 씬을 추가 로드해 실제 Play Mode 검증을 시작합니다.</summary>
    [MenuItem("Dystopia/Top Down Test/Run Play Verification")]
    public static void RunPlayVerification()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode is already changing or active.");
        if (!File.Exists(ScenePath)) CreateScene();
        originalActiveScene = SceneManager.GetActiveScene();
        if (originalActiveScene.path == ScenePath)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.path == ScenePath) continue;
                originalActiveScene = candidate;
                break;
            }
        }
        SessionState.SetString(OriginalSceneKey, originalActiveScene.path);
        loadedTestScene = SceneManager.GetSceneByPath(ScenePath);
        if (!loadedTestScene.IsValid() || !loadedTestScene.isLoaded)
            loadedTestScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(loadedTestScene);
        Report.Clear();
        Report.AppendLine("Actual Unity Play Mode verification of isolated additive test scene.");
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool(CleanupKey, false);
        EditorApplication.isPlaying = true;
    }

    /// <summary>이미 실행 중인 테스트 씬에서 domain reload 없이 자동 검사를 시작합니다.</summary>
    [MenuItem("Dystopia/Top Down Test/Verify Current Play")]
    public static void VerifyCurrentPlay()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("The isolated top-down scene must be in Play Mode.");
        if (verification != null) throw new InvalidOperationException("Top-down verification is already running.");
        Report.Clear();
        Report.AppendLine("Actual Unity Play Mode verification of isolated additive test scene.");
        verification = VerifyInPlay();
        nextStep = 0;
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= VerificationStep;
        EditorApplication.update += VerificationStep;
    }

    /// <summary>Play 진입과 종료에 맞춰 검사 루틴 및 추가 씬을 정리합니다.</summary>
    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.delayCall += AttachBridgeAfterPlayEntry;
            if (!SessionState.GetBool(PendingKey, false)) return;
            verification = VerifyInPlay();
            nextStep = 0;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += VerificationStep;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= VerificationStep;
            if (!SessionState.GetBool(CleanupKey, false)) return;
            Scene testScene = SceneManager.GetSceneByPath(ScenePath);
            if (testScene.IsValid() && testScene.isLoaded) EditorSceneManager.CloseScene(testScene, true);
            string originalPath = SessionState.GetString(OriginalSceneKey, "");
            Scene original = SceneManager.GetSceneByPath(originalPath);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            else
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene candidate = SceneManager.GetSceneAt(i);
                    if (candidate.path == ScenePath) continue;
                    SceneManager.SetActiveScene(candidate);
                    break;
                }
            }
            SessionState.EraseBool(CleanupKey);
            SessionState.EraseString(OriginalSceneKey);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>Enter Play Mode 설정이 Scene reload를 생략해도 기존 정면 화면에 브리지를 붙입니다.</summary>
    private static void AttachBridgeAfterPlayEntry()
    {
        if (!EditorApplication.isPlaying) return;
        DystopiaScreen screen = UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>();
        if (screen != null) DystopiaTopDownTest.AttachToExistingScreen(screen);
    }

    /// <summary>실제 PlayerLoop를 유지하며 시간 간격에 맞춰 검사 단계를 진행합니다.</summary>
    private static void VerificationStep()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Top-down Play verification exceeded 180 seconds.");
            if (verification == null || !verification.MoveNext())
            {
                FinishVerification();
                return;
            }
            nextStep = EditorApplication.timeSinceStartup + (verification.Current is float seconds ? seconds : .1f);
        }
        catch (Exception exception)
        {
            Report.AppendLine("FAIL: " + exception);
            Debug.LogException(exception);
            FinishVerification();
        }
    }

    /// <summary>사용자 입력을 가로채지 않고 런타임 상태와 실제 Rigidbody2D 결과를 검사합니다.</summary>
    private static IEnumerator VerifyInPlay()
    {
        DystopiaTopDownTest screen = null;
        for (int i = 0; i < 100 && screen == null; i++)
        {
            screen = UnityEngine.Object.FindFirstObjectByType<DystopiaTopDownTest>();
            yield return .05f;
        }
        Require(screen != null, "isolated top-down controller is active");
        for (int i = 0; i < 200 && !screen.IsSorting; i++) yield return .05f;
        Require(screen.IsSorting, "front -> transition -> top-down pour -> sorting");
        Require(screen.Items.Count >= 4, "existing customer basket produced at least four independent unit instances");
        Require(screen.Items.Select(item => item.InstanceId).Distinct().Count() == screen.Items.Count, "duplicate products retain unique instance IDs");
        Require(screen.Items.Select(item => Mathf.RoundToInt(item.transform.eulerAngles.z / 5f)).Distinct().Count() > 1, "poured items have varied planar rotation");
        Require(screen.Items.Select(item => new Vector2(Mathf.Round(item.transform.position.x * 4), Mathf.Round(item.transform.position.y * 4))).Distinct().Count() > 1, "poured items spread to different positions");
        ScreenCapture.CaptureScreenshot(ScreenshotPath);
        yield return .3f;

        DystopiaTopDownItem forceItem = screen.Items.First(item => item.State == TopDownItemState.Working);
        forceItem.transform.position = Vector3.zero;
        forceItem.Body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        screen.ApplyCursorSweep(new Vector2(-.7f, 0), new Vector2(.7f, 0), 1f);
        yield return .1f;
        float slowSpeed = forceItem.Body.linearVelocity.magnitude;
        forceItem.transform.position = Vector3.zero;
        forceItem.Body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        screen.ApplyCursorSweep(new Vector2(-.7f, 0), new Vector2(.7f, 0), .05f);
        yield return .1f;
        float fastSpeed = forceItem.Body.linearVelocity.magnitude;
        Require(fastSpeed > slowSpeed + .05f, "fast cursor sweep pushes more strongly than slow sweep");

        var workingPair = screen.Items.Where(item => item.State == TopDownItemState.Working).Take(2).ToArray();
        Require(workingPair.Length == 2, "two working items available for overlap test");
        workingPair[0].transform.position = new Vector2(0, 0);
        workingPair[1].transform.position = new Vector2(.03f, 0);
        workingPair[0].Body.linearVelocity = new Vector2(-.2f, 0);
        workingPair[1].Body.linearVelocity = new Vector2(.2f, 0);
        Physics2D.SyncTransforms();
        yield return .5f;
        Require(Vector2.Distance(workingPair[0].transform.position, workingPair[1].transform.position) > .08f, "overlapping items separate through Rigidbody2D collision");

        DystopiaTopDownItem excluded = screen.Items.First(item => item.State == TopDownItemState.Working);
        int beforeTotal = screen.Session.Customer.total;
        int excludedPrice = screen.Session.Customer.basket[excluded.LineIndex].product.price;
        excluded.WasStirred = true;
        excluded.transform.position = new Vector2(-5.7f, 0);
        Physics2D.SyncTransforms();
        yield return .2f;
        Require(excluded.State == TopDownItemState.Excluded && !excluded.gameObject.activeSelf, "left chute excludes one item after its center enters");
        Require(screen.Session.Customer.total == beforeTotal - excludedPrice, "excluded item is removed from the existing transaction total");
        Require(!screen.TryClassify(excluded, TopDownItemState.Excluded), "excluded item cannot be counted twice");

        DystopiaTopDownItem sale = screen.Items.First(item => item.State == TopDownItemState.Working);
        sale.WasStirred = true;
        sale.transform.position = new Vector2(4.4f, 1.5f);
        Physics2D.SyncTransforms();
        yield return .2f;
        Require(sale.State == TopDownItemState.ForSale && sale.gameObject.activeSelf, "right tray keeps one classified sale item visible");
        Require(!screen.TryClassify(sale, TopDownItemState.ForSale), "sale item cannot be counted twice");
        Require(!screen.TryConfirm("1"), "remaining working item blocks confirmation");

        screen.ClassifyAllForVerification(TopDownItemState.ForSale);
        int acceptedPrice = Math.Min(screen.Session.Customer.total, screen.Session.Customer.budget);
        int cashBefore = screen.Session.Cash;
        Require(screen.TryConfirm(acceptedPrice.ToString()), "classified sale list routes to existing Confirm");
        Require(screen.Session.LastAccepted && screen.Session.Cash == cashBefore + acceptedPrice, "accepted offer adds exactly the entered price once");
        Require(!screen.TryConfirm(acceptedPrice.ToString()) && screen.Session.Cash == cashBefore + acceptedPrice, "rapid repeated confirmation cannot duplicate income");
        for (int i = 0; i < 100 && !screen.IsSorting; i++) yield return .05f;
        Require(screen.IsSorting && screen.EnteredAmount.Length == 0, "next customer starts with cleared amount");
        Require(screen.Items.All(item => item.State == TopDownItemState.Working), "next customer has no previous classification state");

        screen.ClassifyAllForVerification(TopDownItemState.ForSale);
        int refusalCash = screen.Session.Cash;
        int refusedBefore = screen.Session.Refused;
        Require(screen.TryConfirm("9999999"), "overpriced offer reaches existing judgment");
        Require(!screen.Session.LastAccepted && screen.Session.Cash == refusalCash && screen.Session.Refused == refusedBefore + 1, "refused offer adds no income and shows existing reaction state");
        for (int i = 0; i < 100 && !screen.IsSorting; i++) yield return .05f;
        Require(screen.IsSorting, "refused customer exits and next customer reaches sorting");

        DystopiaTopDownItem pauseItem = screen.Items.First(item => item.State == TopDownItemState.Working);
        pauseItem.Body.linearVelocity = new Vector2(2, 0);
        Vector2 pausedPosition = pauseItem.transform.position;
        float pausedMinute = screen.BusinessMinute;
        screen.SetPaused(true);
        yield return .5f;
        Require(Vector2.Distance(pausedPosition, pauseItem.transform.position) < .01f && Mathf.Approximately(pausedMinute, screen.BusinessMinute), "pause freezes item physics and business clock");
        screen.SetPaused(false);
        yield return .15f;
        Require(Vector2.Distance(pausedPosition, pauseItem.transform.position) > .01f, "resume restores item physics");

        int closeCash = screen.Session.Cash;
        screen.SetBusinessMinuteForVerification(21 * 60);
        yield return .2f;
        Require(screen.IsSorting && screen.Session.Remaining == 1 && screen.Session.WaitingCustomers.Count == 0, "21:00 stops arrivals but preserves the current customer");
        screen.ClassifyAllForVerification(TopDownItemState.ForSale);
        int finalPrice = Math.Min(screen.Session.Customer.total, screen.Session.Customer.budget);
        Require(screen.TryConfirm(finalPrice.ToString()) && screen.Session.Cash == closeCash + finalPrice, "last customer can pay after 21:00");
        for (int i = 0; i < 100 && !screen.IsClosed; i++) yield return .05f;
        Require(screen.IsClosed && screen.Session.Phase == DystopiaPhase.Settlement && !screen.TryConfirm("1"), "last customer finishes the day without another arrival");
        Require(UnityEngine.Object.FindObjectsByType<DystopiaTopDownTest>(FindObjectsSortMode.None).Length == 1, "one isolated test controller in Play Mode");
    }

    /// <summary>검사 결과와 생성 화면을 저장하고 Play Mode를 종료합니다.</summary>
    private static void FinishVerification()
    {
        EditorApplication.update -= VerificationStep;
        verification = null;
        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
        File.WriteAllText(EvidencePath, Report.ToString());
        Debug.Log("[TopDownTest] Play verification finished.\n" + Report);
        SessionState.SetBool(PendingKey, false);
        SessionState.SetBool(CleanupKey, true);
        EditorApplication.isPlaying = false;
    }

    /// <summary>검사 조건을 보고서에 기록하고 실패 시 즉시 중단합니다.</summary>
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Report.AppendLine("OK: " + label);
    }

    /// <summary>신규 테스트 PNG만 Point·무압축·mipmap 해제 Sprite로 설정합니다.</summary>
    private static void ConfigureArtImporters()
    {
        AssetDatabase.Refresh();
        string artPath = TestRoot + "/Art";
        foreach (string path in Directory.GetFiles(artPath, "*.png", SearchOption.TopDirectoryOnly).Select(path => path.Replace('\\', '/')))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
