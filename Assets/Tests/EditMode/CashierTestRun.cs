using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>로컬 Test Runner를 실행하고 domain reload 이후에도 결과 XML·로그를 남긴다.</summary>
[InitializeOnLoad]
public sealed class CashierTestRun : ICallbacks
{
    private const string PendingKey = "Cashier.Tests.Pending";
    private const string BackgroundKey = "Cashier.Tests.Background";
    private const string FinishedKey = "Cashier.Tests.Finished";
    private static readonly TestRunnerApi api;

    /// <summary>PlayMode domain reload마다 동일 callback을 다시 등록한다.</summary>
    static CashierTestRun()
    {
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new CashierTestRun());
        EditorApplication.update += finishAfterCleanup;
    }

    /// <summary>현재 프로젝트 테스트 assembly만 실행한다. 씬 저장이나 사용자 Play를 중단하지 않는다.</summary>
    /// <param name="mode">EditMode 또는 PlayMode.</param>
    /// <param name="runId">결과 폴더용 고유 영숫자 ID.</param>
    /// <returns>결과 XML 절대 경로.</returns>
    /// <exception cref="InvalidOperationException">실행·컴파일 중이거나 미저장 씬이 있음.</exception>
    /// <exception cref="ArgumentException">모드 또는 결과 ID 오류.</exception>
    public static string Start(string mode, string runId)
    {
        if (isRunActive())
            throw new InvalidOperationException("Another Unity Test Runner job is active; no state was changed.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed || !string.IsNullOrEmpty(SessionState.GetString(PendingKey, "")))
            throw new InvalidOperationException("Test Runner requires an idle, compiled Editor without a pending run.");
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            if (scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty)
                throw new InvalidOperationException("Save or handle dirty scenes before tests; tests do not save scenes.");
        if (mode != "EditMode" && mode != "PlayMode") throw new ArgumentException("Unknown test mode.");
        if (string.IsNullOrEmpty(runId) || !System.Text.RegularExpressions.Regex.IsMatch(runId, "^[a-zA-Z0-9-]+$")) throw new ArgumentException("Invalid run ID.");
        string path = Path.GetFullPath(Path.Combine("Temp", "TestResults", runId, mode + ".xml"));
        if (File.Exists(path)) throw new InvalidOperationException("Use a fresh run ID; prior evidence is not overwritten.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        SessionState.SetBool(BackgroundKey, Application.runInBackground);
        SessionState.SetString(PendingKey, path);
        SessionState.SetBool(FinishedKey, false);
        try
        {
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = mode == "EditMode" ? TestMode.EditMode : TestMode.PlayMode,
                assemblyNames = new[] { "Cashier." + mode + ".Tests" }
            }));
        }
        catch { restore(); throw; }
        return path;
    }

    /// <summary>Runner가 발견한 루트 정보는 최종 XML로 보존한다.</summary>
    /// <param name="testsToRun">발견 트리.</param>
    public void RunStarted(ITestAdaptor testsToRun) { }

    /// <summary>모든 결과를 XML과 읽기 쉬운 로그로 기록한 뒤 임시 실행값을 원복한다.</summary>
    /// <param name="result">완료 결과. 0개·skip도 그대로 기록하며 외부 진입점이 성공 여부를 판정한다.</param>
    public void RunFinished(ITestResultAdaptor result)
    {
        string path = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            // 완료 마커인 XML 경로는 쓰기가 끝난 뒤에만 공개한다.
            TestRunnerApi.SaveResultToFile(result, path + ".tmp");
            var log = new StringBuilder(); appendResult(log, result);
            File.WriteAllText(Path.ChangeExtension(path, ".log"), log.ToString());
            SessionState.SetBool(FinishedKey, true);
        }
        catch { restore(); throw; }
    }

    /// <summary>개별 시작 상태는 Test Runner에 위임한다.</summary>
    /// <param name="test">시작 사례.</param>
    public void TestStarted(ITestAdaptor test) { }
    /// <summary>개별 완료 결과는 최종 트리에서 일괄 기록한다.</summary>
    /// <param name="result">완료 사례.</param>
    public void TestFinished(ITestResultAdaptor result) { }

    /// <summary>완료 또는 시작 실패 후 pending 상태를 지운다. 프로젝트 옵션 복원은 Test Runner가 수행한다.</summary>
    private static void restore()
    {
        SessionState.EraseString(PendingKey);
        SessionState.SetBool(FinishedKey, false);
    }

    /// <summary>Test Framework 1.6의 RunFinished는 복원보다 먼저 오므로 실제 job 정리 후 XML을 공개한다.</summary>
    private static void finishAfterCleanup()
    {
        if (!SessionState.GetBool(FinishedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        string path = SessionState.GetString(PendingKey, "");
        try
        {
            if (isRunActive() || Application.runInBackground != SessionState.GetBool(BackgroundKey, false)) return;
            File.Move(path + ".tmp", path);
            restore();
        }
        catch (Exception exception)
        {
            // 다음 update에서 같은 오류를 반복하지 않으며 최종 XML 부재는 외부 실행 실패다.
            restore();
            Debug.LogError($"[CashierTestRun] Finalize '{path}' failed: {exception}");
        }
    }

    /// <summary>GUI/API가 시작한 실행도 포함해 현재 Test Runner 작업 상태를 확인한다.</summary>
    /// <returns>실행 또는 cleanup 중이면 true.</returns>
    /// <exception cref="InvalidOperationException">설치된 Test Framework에서 상태를 안전하게 조회할 수 없음.</exception>
    private static bool isRunActive()
    {
        var method = typeof(TestRunnerApi).GetMethod("IsRunActive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("Test Runner active-state API is unavailable; execution is blocked.");
        try { return (bool)method.Invoke(null, null); }
        catch (Exception exception) { throw new InvalidOperationException("Test Runner active-state query failed; execution is blocked.", exception); }
    }

    /// <summary>실패 원인·stacktrace·기대 로그를 포함한 사례별 결과를 기록한다.</summary>
    /// <param name="log">출력 버퍼.</param><param name="result">순회 노드.</param>
    private static void appendResult(StringBuilder log, ITestResultAdaptor result)
    {
        log.AppendLine(result.FullName + ": " + result.ResultState);
        log.AppendLine(result.Message); log.AppendLine(result.StackTrace); log.AppendLine(result.Output);
        foreach (var child in result.Children) appendResult(log, child);
    }
}
