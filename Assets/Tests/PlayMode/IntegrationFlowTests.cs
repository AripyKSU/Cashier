#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>통합된 실제 Init→Hub→Intro→Main과 재시작·단계별 상자 연결을 검증한다.</summary>
public sealed class IntegrationFlowTests
{
    private const string Evidence = "Logs/MergeVisualChecks";
    private string preference;
    private bool hadPreference;
    private bool background;

    /// <summary>분리된 테스트 프로젝트의 개인 씬 설정만 일시적으로 비운다.</summary>
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(GameSceneManager.Instance, Is.Null);
        hadPreference = EditorPrefs.HasKey(GameSceneManager.LocalScenePreferenceKey);
        preference = EditorPrefs.GetString(GameSceneManager.LocalScenePreferenceKey, "");
        EditorPrefs.DeleteKey(GameSceneManager.LocalScenePreferenceKey);
        background = Application.runInBackground;
        Application.runInBackground = true;
        Directory.CreateDirectory(Evidence);
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/InitScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return until(() => SceneManager.GetActiveScene().name == "HubScene", 90);
    }

    /// <summary>테스트가 만든 전역 manager와 임시 설정을 정리한다.</summary>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var sceneRoot in SceneManager.GetSceneAt(i).GetRootGameObjects()) sceneRoot.SetActive(false);
        if (SimplePoolManager.Instance != null) SimplePoolManager.Instance.ClearAll();
        var managers = new MonoBehaviour[] {GameSceneManager.Instance, GameSessionManager.Instance,
            SoundManager.Instance, DataTableManager.Instance, ResourceManager.Instance, SimplePoolManager.Instance};
        foreach (var owner in managers.Where(x => x != null)) Object.Destroy(owner.gameObject);
        if (hadPreference) EditorPrefs.SetString(GameSceneManager.LocalScenePreferenceKey, preference);
        else EditorPrefs.DeleteKey(GameSceneManager.LocalScenePreferenceKey);
        Application.runInBackground = background;
        yield return null;
    }

    /// <summary>실제 새 게임을 두 번 실행하고 인트로 중복 완료·풀 재준비·새 상자 연결을 확인한다.</summary>
    [UnityTest]
    public IEnumerator HubIntroMainRestartPreservesSessionPoolsAndCrates()
    {
        yield return capture("hub");
        for (int run = 0; run < 2; run++)
        {
            yield return awaitTask(GameSceneManager.Instance.RestartGameAsync().AsTask(), 90);
            yield return until(() => SceneManager.GetActiveScene().name == "IntroScene", 30);
            var intro = Object.FindFirstObjectByType<IntroDialogueController>();
            Assert.That(intro, Is.Not.Null);
            yield return until(() => intro.IsPlaying, 10);
            int completed = 0;
            intro.IntroCompleted += () => completed++;
            if (run == 0) {yield return new WaitForSecondsRealtime(1); yield return capture("intro-start");}
            intro.CompleteImmediately(); intro.CompleteImmediately();
            yield return until(() => SceneManager.GetActiveScene().name == "MainScene", 90);
            var ui = Object.FindFirstObjectByType<GameUIController>();
            yield return until(() => ui != null && ui.CurrentDayProgress != null && !ui.IsPresentationBlocked, 60);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(GameSessionManager.Instance.ElapsedDays, Is.Zero);
            Assert.That(SimplePoolManager.Instance.TryGetPool<WorldVisit>("WorldVisit", out var visits), Is.True);
            Assert.That(SimplePoolManager.Instance.TryGetPool<WorldQueueSpeech>("WorldQueueSpeech", out var speeches), Is.True);
            Assert.That(visits.Capacity, Is.EqualTo(22)); Assert.That(speeches.Capacity, Is.EqualTo(22));
            Assert.That(Object.FindObjectsByType<GameSceneManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<SimplePoolManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<IntroNineCutPlayer>(), Is.Null);
            if (run == 0)
            {
                yield return capture("inspector");
                var inspector = get<InspectorPresenter>(ui, "inspectorPresenter");
                var next = get<Button>(inspector, "nextButton");
                float end = Time.realtimeSinceStartup + 45;
                while (ui.CurrentDayProgress.State == DayProgressState.InspectorEvent && Time.realtimeSinceStartup < end)
                {
                    if (next.interactable) next.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.15f);
                }
                Assert.That(ui.CurrentDayProgress.State, Is.EqualTo(DayProgressState.PreOpen));
                get<Button>(ui, "openBusinessButton").onClick.Invoke();
                yield return until(() => ui.CurrentDayProgress.State == DayProgressState.Operating, 10);
                yield return new WaitForSecondsRealtime(1);
                var stage = Object.FindFirstObjectByType<StoreStagePresentation>();
                var sorting = get<SaleSortingPanel>(ui, "saleSortingPanel");
                for (uint value = 1; value <= 3; value++)
                {
                    stage.Apply(value, id => false, sorting);
                    stage.SetContainerOpen(false); yield return capture("stage" + value + "-closed");
                    stage.SetContainerOpen(true); yield return capture("stage" + value + "-open");
                    Assert.That(stage.AppliedStage, Is.EqualTo(value));
                    var front = get<Image[]>(stage, "frontTargets");
                    Assert.That(front[5], Is.Not.Null);
                    Assert.That(front[5].sprite, Is.Not.Null, "단계별 정면 상자 Sprite 연결");
                }
                stage.Apply(1, GameSessionManager.Instance.IsFacilityActive, sorting); stage.SetContainerOpen(false);
            }
            yield return awaitTask(GameSceneManager.Instance.ReturnToHubAsync().AsTask(), 60);
            yield return until(() => SceneManager.GetActiveScene().name == "HubScene", 20);
            // 엔딩 종료와 같은 공개 풀 정리 이후에도 다음 새 게임이 풀을 다시 준비해야 한다.
            SimplePoolManager.Instance.ClearAll();
        }
        File.WriteAllText(Evidence + "/flow.txt", "PASS: Init-Hub-Intro-Main twice; one completion; day 1; pools 22; three crate stages; pool clear/reprepare.");
    }

    /// <summary>실제 프레임의 화면을 증거로 저장한다.</summary>
    private static IEnumerator capture(string name)
    {
        yield return null;
        ScreenCapture.CaptureScreenshot(Evidence + "/" + name + ".png");
        yield return null;
    }

    /// <summary>직렬화된 실제 UI 연결을 읽는다.</summary>
    private static T get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    /// <summary>제한 시간 내 실제 상태 전이를 기다린다.</summary>
    private static IEnumerator until(Func<bool> condition, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(condition(), Is.True, "Integration transition timed out");
    }

    /// <summary>씬 전환 실패를 삼키지 않고 관찰한다.</summary>
    private static IEnumerator awaitTask(Task task, float seconds)
    {
        yield return until(() => task.IsCompleted, seconds);
        task.GetAwaiter().GetResult();
    }
}
#endif
