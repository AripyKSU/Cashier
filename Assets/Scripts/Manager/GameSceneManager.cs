using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>전역 수명으로 로딩 화면과 공유·개인 씬 전환을 소유한다.</summary>
public class GameSceneManager : Singleton<GameSceneManager>
{
    private const string LoadingScene = "LoadingScene";

    // 이전 씬이 파괴돼도 manager가 목적지 활성화까지 재진입을 차단한다.
    private bool isTransitioning;
    // Hub에서 새 게임을 선택했을 때만 다음 Init 부트 후 게임으로 바로 진입한다.
    private bool startGameplayAfterBoot;

#if UNITY_EDITOR
    /// <summary>개인 씬과 metadata를 Git에서 제외하는 개발 전용 경로.</summary>
    public const string LocalSceneFolder = "Assets/Scenes/Local/";
    /// <summary>PC·프로젝트별 개인 씬 GUID를 저장하는 EditorPrefs 키.</summary>
    public static string LocalScenePreferenceKey => "Cashier.GameplayScene." + Application.dataPath;
#endif

    /// <summary>공유 씬 식별자. 개인 씬은 enum에 추가하지 않는다.</summary>
    public enum SceneName
    {
        Init,
        Hub,
        Main,
        GoodEnding,
        BadEnding
    }

    /// <summary>
    /// Transitions to the specified scene via the LoadingScene.
    /// Waits for LoadingScene fade-in before activating the target.
    /// </summary>
    /// <param name="target">공유 씬 식별자.</param>
    /// <returns>목적지 활성화 완료 또는 오류.</returns>
    public UniTask TransitionTo(SceneName target)
    {
        // Map enum to scene addressable names (assumes scenes are addressable by these keys)
        string targetSceneKey = target switch
        {
            SceneName.Init => "InitScene",
            SceneName.Hub => "HubScene",
            SceneName.Main => "MainScene",
            SceneName.GoodEnding => "GoodEndingScene",
            SceneName.BadEnding => "BadEndingScene",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown scene.")
        };

        return TransitionAsync(targetSceneKey, null, default);
    }

    /// <summary>동결된 결과로 엔딩을 로드한다. 실패 시 정산을 반복하지 않고 로딩 화면에서 재시도한다.</summary>
    /// <returns>엔딩 전환 또는 실패 안내 완료.</returns>
    /// <exception cref="InvalidOperationException">정상 엔딩 결과가 없음.</exception>
    public async UniTask TransitionToFinalEndingAsync()
    {
        var result = GameSessionManager.Instance.EndingResult;
        if (!result.HasValue || (result.Value.Kind != EndingKind.Good && result.Value.Kind != EndingKind.Bad))
            throw new InvalidOperationException("확정된 굿·배드 엔딩 결과가 필요합니다.");
        try
        {
            await TransitionTo(result.Value.Kind == EndingKind.Good ? SceneName.GoodEnding : SceneName.BadEnding);
        }
        catch (Exception exception)
        {
            var loading = UnityEngine.Object.FindFirstObjectByType<LoadingScene>();
            if (loading == null)
            {
                // 목적지 주소 사전 검사 실패도 결과를 유지한 채 재시도 화면으로 보낸다.
                await LoadAddressableSceneAsync(LoadingScene);
                loading = UnityEngine.Object.FindFirstObjectByType<LoadingScene>();
            }
            if (loading == null) throw;
            Debug.LogError($"엔딩 씬 전환 실패: {exception}");
            loading.ShowEndingRetry();
        }
    }

    /// <summary>기존 부트 씬으로 새 게임을 시작하고 로딩 중 실패하면 재시도를 제공한다.</summary>
    /// <returns>부트 전환 또는 재시도 안내 완료.</returns>
    public async UniTask RestartGameAsync()
    {
        if (isTransitioning) throw new InvalidOperationException("A scene transition is already running.");
        startGameplayAfterBoot = true;
        try { await TransitionTo(SceneName.Init); }
        catch (Exception exception)
        {
            var loading = UnityEngine.Object.FindFirstObjectByType<LoadingScene>();
            if (loading == null) throw;
            Debug.LogError($"새 게임 씬 전환 실패: {exception}");
            loading.ShowNewGameRetry();
        }
    }

    /// <summary>종료 결과를 보존한 채 Hub 메뉴로 돌아가며 로딩 실패는 같은 목적지로 재시도한다.</summary>
    /// <returns>메뉴 전환 또는 재시도 안내 완료.</returns>
    public async UniTask ReturnToHubAsync()
    {
        try { await TransitionTo(SceneName.Hub); }
        catch (Exception exception)
        {
            var loading = UnityEngine.Object.FindFirstObjectByType<LoadingScene>();
            if (loading == null) throw;
            Debug.LogError($"메뉴 씬 전환 실패: {exception}");
            loading.ShowHubRetry();
        }
    }

    /// <summary>최초 부트는 메뉴로, 메뉴에서 요청한 새 게임 부트는 선택된 게임 씬으로 보낸다.</summary>
    /// <param name="defaultDestination">Init Inspector에 지정된 최초 부트 목적지.</param>
    /// <returns>부트 이후 씬 전환 완료.</returns>
    public async UniTask TransitionAfterBootAsync(SceneName defaultDestination)
    {
        // Init의 Start가 이전 Single 전환의 완료보다 먼저 실행되는 경우를 분리한다.
        await UniTask.NextFrame(this.GetCancellationTokenOnDestroy());
        bool enterGameplay = startGameplayAfterBoot;
        startGameplayAfterBoot = false;
        if (enterGameplay) await TransitionToGameplayAsync();
        else await TransitionTo(defaultDestination);
    }

    /// <summary>Hub에서 개인 설정을 읽어 개발 씬 또는 MainScene으로 이동한다. 빌드는 Main만 사용한다.</summary>
    /// <returns>전환 완료 또는 오류.</returns>
    /// <exception cref="InvalidOperationException">개인 씬이 없거나 허용 경로 밖인 경우.</exception>
    public UniTask TransitionToGameplayAsync()
    {
#if UNITY_EDITOR
        string guid = UnityEditor.EditorPrefs.GetString(LocalScenePreferenceKey, string.Empty);
        if (!string.IsNullOrEmpty(guid))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            // 잘못된 설정을 Main으로 우회하면 다른 씬을 검증하게 되므로 명시적으로 실패한다.
            if (!path.StartsWith(LocalSceneFolder, StringComparison.Ordinal)
                || UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) == null)
                throw new InvalidOperationException("Personal scene is missing or outside " + LocalSceneFolder
                    + ". Select it again in Cashier/Gameplay Scene Settings.");
            return TransitionAsync(null, path, default);
        }
#endif
        return TransitionTo(SceneName.Main);
    }

    /// <summary>기존 AssetReference 호출도 공통 전환 경로로 처리한다.</summary>
    /// <param name="sceneRef">로드할 Addressable 씬.</param>
    /// <param name="cancellationToken">전환 시작 전 취소 요청.</param>
    /// <returns>전환 완료 또는 오류.</returns>
    public UniTask LoadSceneAsync(AssetReference sceneRef, CancellationToken cancellationToken = default)
    {
        if (sceneRef == null || !sceneRef.RuntimeKeyIsValid())
            throw new ArgumentException("A valid scene reference is required.", nameof(sceneRef));
        return TransitionAsync(sceneRef.RuntimeKey, null, cancellationToken);
    }

    /// <summary>전환을 직렬화하고 목적지를 미리 검사한다. Single 로드 시작 후에는 활성화까지 완료한다.</summary>
    /// <param name="address">공유 씬 키. 개인 씬이면 null.</param>
    /// <param name="localPath">Editor 개인 씬 경로. 공유 씬이면 null.</param>
    /// <param name="cancellationToken">시작 전 취소 요청. 씬 로드 자체는 중간 취소하지 않는다.</param>
    /// <returns>목적지 활성화 완료.</returns>
    /// <exception cref="InvalidOperationException">중복 요청 또는 목적지 누락.</exception>
    private async UniTask TransitionAsync(object address, string localPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isTransitioning) throw new InvalidOperationException("A scene transition is already running.");
        isTransitioning = true;
        try
        {
            // 부트 씬은 Build Settings의 시작 씬이며 Addressables 카탈로그에 등록하지 않는다.
            bool isBoot = localPath == null && address is string key && key == "InitScene";
            if (isBoot && !Application.CanStreamedLevelBeLoaded("InitScene"))
                throw new InvalidOperationException("Build Settings에 InitScene이 필요합니다.");
            if (localPath == null && !isBoot)
            {
                var locations = Addressables.LoadResourceLocationsAsync(address, typeof(SceneInstance));
                try
                {
                    await locations.ToUniTask();
                    if (locations.Result.Count != 1)
                        throw new InvalidOperationException($"Scene address must resolve exactly once: {address}");
                }
                finally
                {
                    if (locations.IsValid()) Addressables.Release(locations);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            await LoadAddressableSceneAsync(LoadingScene);
            await UniTask.Delay(TimeSpan.FromSeconds(0.3), ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            if (isBoot)
            {
                await SceneManager.LoadSceneAsync("InitScene", LoadSceneMode.Single);
                return;
            }
#if UNITY_EDITOR
            if (localPath != null)
            {
                // 개인 씬은 Build Settings와 Addressables를 수정하지 않고 Editor에서만 로드한다.
                await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    localPath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            await LoadAddressableSceneAsync(address);
        }
        finally
        {
            isTransitioning = false;
        }
    }

    /// <summary>실패 handle을 해제하고 성공한 씬 수명은 Addressables에 맡긴다.</summary>
    /// <param name="address">로드할 씬 키.</param>
    /// <returns>씬 활성화 완료.</returns>
    private static async UniTask LoadAddressableSceneAsync(object address)
    {
        var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single);
        try
        {
            await handle.ToUniTask();
        }
        catch
        {
            if (handle.IsValid()) Addressables.Release(handle);
            throw;
        }
    }

}
