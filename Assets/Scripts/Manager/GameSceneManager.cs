using System;
using System.Collections.Generic;
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
    // Hub에서 새 게임 세션을 준비하는 동안 다른 전환 요청을 차단한다.
    private bool isPreparingNewGame;

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
        if (!result.HasValue || result.Value.Kind <= EndingKind.None || result.Value.Kind >= EndingKind.EndingKind_End)
            throw new InvalidOperationException("확정된 엔딩 결과가 필요합니다.");
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

    /// <summary>이미 부트스트랩된 런타임으로 새 세션을 만들고 Gameplay 씬으로 직접 진입한다.</summary>
    /// <returns>Gameplay 전환 또는 재시도 안내 완료.</returns>
    public async UniTask RestartGameAsync()
    {
        if (isTransitioning || isPreparingNewGame)
            throw new InvalidOperationException("A scene transition is already running.");

        isPreparingNewGame = true;
        bool sessionPrepared = false;
        try
        {
            await PrepareNewGameSessionAsync();
            sessionPrepared = true;
            // TransitionAsync가 전환 lock을 소유하므로 준비 단계에서만 별도 lock을 사용한다.
            isPreparingNewGame = false;
            await TransitionToGameplayAsync();
        }
        catch (Exception exception)
        {
            var loading = UnityEngine.Object.FindFirstObjectByType<LoadingScene>();
            if (loading == null && sessionPrepared)
            {
                // 목적지 사전 검사 실패도 LoadingScene에서 동일한 새 게임 재시도를 제공한다.
                await LoadAddressableSceneAsync(LoadingScene);
                loading = UnityEngine.Object.FindFirstObjectByType<LoadingScene>();
            }
            if (loading == null) throw;
            Debug.LogError($"새 게임 씬 전환 실패: {exception}");
            loading.ShowNewGameRetry();
        }
        finally
        {
            isPreparingNewGame = false;
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

    /// <summary>최초 부트를 마친 뒤 기본 목적지인 Hub 메뉴로 이동한다.</summary>
    /// <param name="defaultDestination">Init Inspector에 지정된 최초 부트 목적지.</param>
    /// <returns>부트 이후 씬 전환 완료.</returns>
    public async UniTask TransitionAfterBootAsync(SceneName defaultDestination)
    {
        // Init의 Start가 이전 Single 전환의 완료보다 먼저 실행되는 경우를 분리한다.
        await UniTask.NextFrame(this.GetCancellationTokenOnDestroy());
        await TransitionTo(defaultDestination);
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

    /// <summary>전환을 직렬화하고 목적지를 미리 검사한 뒤 최소 표시 후 목적지를 활성화한다.</summary>
    /// <param name="address">공유 씬 키. 개인 씬이면 null.</param>
    /// <param name="localPath">Editor 개인 씬 경로. 공유 씬이면 null.</param>
    /// <param name="cancellationToken">시작 전 취소 요청. 씬 로드 자체는 중간 취소하지 않는다.</param>
    /// <returns>목적지 활성화 완료.</returns>
    /// <exception cref="InvalidOperationException">중복 요청 또는 목적지 누락.</exception>
    private async UniTask TransitionAsync(object address, string localPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isTransitioning || isPreparingNewGame)
            throw new InvalidOperationException("A scene transition is already running.");
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
            var loading = UnityEngine.Object.FindFirstObjectByType<LoadingScene>();
            if (loading == null)
                throw new InvalidOperationException("LoadingScene component is missing.");
            if (isBoot)
            {
                await LoadBuildSceneAsync("InitScene", loading);
                return;
            }
#if UNITY_EDITOR
            if (localPath != null)
            {
                // 개인 씬은 Build Settings와 Addressables를 수정하지 않고 Editor에서만 로드한다.
                await LoadEditorSceneAsync(localPath, loading);
                return;
            }
#endif
            if (address is string targetKey && targetKey == "MainScene")
                await PrepareMainSceneAssetsAsync(loading, this.GetCancellationTokenOnDestroy());
            await LoadAddressableSceneAsync(address, loading);
        }
        finally
        {
            isTransitioning = false;
        }
    }

    /// <summary>이미 부트스트랩된 공용 런타임으로 새 게임 세션을 준비한다.</summary>
    /// <returns>Addressables·CSV·사운드 준비 및 새 세션 초기화 완료.</returns>
    private async UniTask PrepareNewGameSessionAsync()
    {
        if (ResourceManager.Instance == null || DataTableManager.Instance == null
            || SoundManager.Instance == null || GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException("InitScene 부트스트랩이 완료된 뒤 새 게임을 시작해야 합니다.");
        }

        var cancellationToken = this.GetCancellationTokenOnDestroy();
        await ResourceManager.Instance.InitAsync(null, cancellationToken);
        await DataTableManager.Instance.EnsureDataLoadedAsync()
            .AttachExternalCancellation(cancellationToken);
        await SoundManager.Instance.InitializeAsync(DataTableManager.Instance, cancellationToken);

        GameSessionManager.Instance.ResetSession();
        GameSessionManager.Instance.InitializeNewGame(DataTableManager.Instance);
    }

    /// <summary>MainScene 활성화 전에 표시 자산을 네 단계로 로드하고 데이터·Prefab 계약을 검증한다.</summary>
    /// <param name="loading">현재 단계 이미지를 표시하는 로딩 화면.</param>
    /// <param name="cancellationToken">전역 Scene manager 수명 토큰.</param>
    /// <returns>필수 표시 자산이 ResourceManager 캐시에 준비되면 완료되는 작업.</returns>
    /// <exception cref="InvalidOperationException">필수 manager, 데이터, FK 또는 자산 구성이 잘못된 경우.</exception>
    private async UniTask PrepareMainSceneAssetsAsync(LoadingScene loading, CancellationToken cancellationToken)
    {
        loading.SetLoadingPhase(0);
        if (ResourceManager.Instance == null || DataTableManager.Instance == null)
            throw new InvalidOperationException("MainScene 자산 준비에 필요한 manager가 없습니다.");

        await ResourceManager.Instance.InitAsync(null, cancellationToken);
        await DataTableManager.Instance.EnsureDataLoadedAsync().AttachExternalCancellation(cancellationToken);

        var tables = DataTableManager.Instance;
        ResourceDataTable resources = tables.GetDB<ResourceDataTable>(DataTableType.Resource);
        CustomerCatalog catalog = tables.Customers;
        if (resources == null || catalog == null)
            throw new InvalidOperationException("MainScene 자산 준비에 필요한 Resource 또는 Customer 데이터가 없습니다.");
        await loading.WaitForCurrentPhaseCycleAsync();

        loading.SetLoadingPhase(1);
        var loadedSpriteIds = new HashSet<uint>();
        foreach (ProductData product in catalog.Products.Rows.Values)
        {
            if (!product.ImageResourceIdx.HasValue)
                continue;

            await LoadRequiredResourceAsync<Sprite>(product.ImageResourceIdx.Value, resources, loadedSpriteIds, cancellationToken);
            if (!product.TopViewImageResourceIdx.HasValue)
                throw new InvalidOperationException($"Product {product.Idx}: top-view Resource FK가 없습니다.");
            await LoadRequiredResourceAsync<Sprite>(product.TopViewImageResourceIdx.Value, resources, loadedSpriteIds, cancellationToken);
        }
        foreach (CustomerAppearanceData appearance in catalog.Appearances.Rows.Values)
            if (appearance.ImageResourceIdx.HasValue)
                await LoadRequiredResourceAsync<Sprite>(appearance.ImageResourceIdx.Value, resources, loadedSpriteIds, cancellationToken);
        await loading.WaitForCurrentPhaseCycleAsync();

        loading.SetLoadingPhase(2);
        var loadedTextureIds = new HashSet<uint>();
        foreach (CustomerAppearanceData appearance in catalog.Appearances.Rows.Values)
            await LoadRequiredResourceAsync<Texture2D>(appearance.NormalResourceIdx, resources, loadedTextureIds, cancellationToken);

        InspectorEventDataTable inspectors = tables.GetDB<InspectorEventDataTable>(DataTableType.InspectorEvent);
        DaughterAppearanceDataTable daughters = tables.GetDB<DaughterAppearanceDataTable>(DataTableType.DaughterAppearance);
        if (inspectors == null || daughters == null)
            throw new InvalidOperationException("MainScene 감독관 또는 딸 외형 데이터가 없습니다.");
        foreach (InspectorEventData inspector in inspectors.Rows.Values)
            await LoadRequiredResourceAsync<Sprite>(inspector.PortraitResourceIdx, resources, loadedSpriteIds, cancellationToken);
        foreach (DaughterAppearanceData daughter in daughters.Rows.Values)
            if (daughter.ResourceIdx.HasValue)
                await LoadRequiredResourceAsync<Sprite>(daughter.ResourceIdx.Value, resources, loadedSpriteIds, cancellationToken);
        await loading.WaitForCurrentPhaseCycleAsync();

        loading.SetLoadingPhase(3);
        StoreStageDataTable stages = tables.GetDB<StoreStageDataTable>(DataTableType.StoreStage);
        FacilityDataTable facilities = tables.GetDB<FacilityDataTable>(DataTableType.Facility);
        if (stages == null || facilities == null)
            throw new InvalidOperationException("MainScene 가게 단계 또는 설비 데이터가 없습니다.");

        var loadedPrefabIds = new HashSet<uint>();
        var loadedClockIds = new HashSet<uint>();
        foreach (StoreStageData stage in stages.Rows.Values)
        {
            uint[] prefabIds =
            {
                stage.WorldPrefabResourceIdx,
                stage.FrontPrefabResourceIdx,
                stage.TopViewPrefabResourceIdx
            };
            for (int index = 0; index < prefabIds.Length; index++)
            {
                GameObject prefab = await LoadRequiredResourceAsync<GameObject>(
                    prefabIds[index], resources, loadedPrefabIds, cancellationToken);
                StoreStageVisual visual = prefab.GetComponent<StoreStageVisual>();
                if (visual == null)
                    throw new InvalidOperationException($"StoreStage {stage.Idx}: Resource {prefabIds[index]}에 StoreStageVisual이 없습니다.");
                visual.Validate((StoreStageVisual.Region)index);
            }
            await LoadRequiredResourceAsync<Sprite>(stage.ClockResourceIdx, resources, loadedClockIds, cancellationToken);
            foreach (FacilityData facility in facilities.Rows.Values)
            {
                uint resourceIdx = facility.GetStageResourceIdx(stage.StoreStage);
                if (resourceIdx == 0) continue;
                GameObject prefab = await LoadRequiredResourceAsync<GameObject>(resourceIdx, resources, loadedPrefabIds, cancellationToken);
                if (prefab.GetComponent<RectTransform>() == null || prefab.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length == 0)
                    throw new InvalidOperationException($"Facility {facility.Idx}: Resource {resourceIdx} is not a visual prefab");
            }
        }
        await loading.WaitForCurrentPhaseCycleAsync();
    }

    /// <summary>Resource FK를 한 번만 로드하고 요청 타입과 실제 결과를 검증한다.</summary>
    /// <typeparam name="T">필수 Unity 자산 타입.</typeparam>
    /// <param name="resourceIdx">ResourceData 기본 키.</param>
    /// <param name="resources">검증된 Resource 데이터 테이블.</param>
    /// <param name="loadedIds">현재 타입에서 이미 준비된 FK 집합.</param>
    /// <param name="cancellationToken">Scene manager 수명 토큰.</param>
    /// <returns>로드된 필수 자산.</returns>
    /// <exception cref="InvalidOperationException">FK, 주소 또는 로드 결과가 누락된 경우.</exception>
    private static async UniTask<T> LoadRequiredResourceAsync<T>(
        uint resourceIdx,
        ResourceDataTable resources,
        HashSet<uint> loadedIds,
        CancellationToken cancellationToken) where T : UnityEngine.Object
    {
        if (!resources.TryGetResource(resourceIdx, out ResourceData resource)
            || resource == null
            || string.IsNullOrWhiteSpace(resource.Path))
        {
            throw new InvalidOperationException($"Resource FK {resourceIdx}의 유효한 주소가 없습니다.");
        }

        if (!loadedIds.Add(resourceIdx))
        {
            T cached = ResourceManager.Instance.GetResource<T>(resource.Path);
            if (cached == null)
                throw new InvalidOperationException($"Resource {resourceIdx}, address={resource.Path}: 캐시 결과가 없습니다.");
            return cached;
        }

        T asset = await ResourceManager.Instance.LoadAssetAsync<T>(resource.Path, cancellationToken);
        if (asset == null)
            throw new InvalidOperationException($"Resource {resourceIdx}, address={resource.Path}: {typeof(T).Name} 로드 결과가 없습니다.");
        return asset;
    }

    /// <summary>Build Settings 씬을 준비하고 최소 표시 시간이 지난 뒤 활성화한다.</summary>
    /// <param name="sceneName">Build Settings에 등록된 씬 이름.</param>
    /// <param name="loading">현재 활성화된 로딩 화면.</param>
    /// <returns>씬 활성화 완료.</returns>
    private async UniTask LoadBuildSceneAsync(string sceneName, LoadingScene loading)
    {
        var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
            throw new InvalidOperationException($"씬을 비동기로 로드할 수 없습니다: {sceneName}");

        operation.allowSceneActivation = false;
        await ActivateWhenReadyAsync(operation, loading.WaitForMinimumDisplayAsync());
    }

#if UNITY_EDITOR
    /// <summary>Editor 개인 씬을 준비하고 최소 표시 시간이 지난 뒤 활성화한다.</summary>
    /// <param name="scenePath">허용된 Local 개인 씬 경로.</param>
    /// <param name="loading">현재 활성화된 로딩 화면.</param>
    /// <returns>씬 활성화 완료.</returns>
    private async UniTask LoadEditorSceneAsync(string scenePath, LoadingScene loading)
    {
        var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            scenePath, new LoadSceneParameters(LoadSceneMode.Single));
        if (operation == null)
            throw new InvalidOperationException($"개인 씬을 비동기로 로드할 수 없습니다: {scenePath}");

        operation.allowSceneActivation = false;
        await ActivateWhenReadyAsync(operation, loading.WaitForMinimumDisplayAsync());
    }
#endif

    /// <summary>AsyncOperation이 준비되고 로딩 화면 최소 시간이 지난 뒤 씬을 활성화한다.</summary>
    /// <param name="operation">activation 보류 상태의 씬 로드 작업.</param>
    /// <param name="minimumDisplayTask">로딩 화면 최소 표시 작업.</param>
    /// <returns>씬 활성화 완료.</returns>
    private async UniTask ActivateWhenReadyAsync(AsyncOperation operation, UniTask minimumDisplayTask)
    {
        // Unity SceneManager는 allowSceneActivation=false일 때 progress 0.9에서 대기한다.
        await UniTask.WaitUntil(() => operation.isDone || operation.progress >= 0.9f,
            cancellationToken: this.GetCancellationTokenOnDestroy());
        await minimumDisplayTask;
        operation.allowSceneActivation = true;
        await operation.ToUniTask();
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

    /// <summary>Addressable 씬을 준비하고 최소 표시 시간이 지난 뒤 활성화한다.</summary>
    /// <param name="address">로드할 씬 address.</param>
    /// <param name="loading">현재 활성화된 로딩 화면.</param>
    /// <returns>씬 활성화 완료.</returns>
    private async UniTask LoadAddressableSceneAsync(object address, LoadingScene loading)
    {
        // Addressables SceneProvider는 activation 보류 시 준비 완료를 0.9 지점에서 핸들에 알린다.
        var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single, activateOnLoad: false);
        try
        {
            var minimumDisplayTask = loading.WaitForMinimumDisplayAsync();
            await handle.ToUniTask();
            await minimumDisplayTask;
            await handle.Result.ActivateAsync().ToUniTask();
        }
        catch
        {
            // 실패한 handle만 해제하고 성공한 씬의 기존 수명 정책은 유지한다.
            if (handle.IsValid()) Addressables.Release(handle);
            throw;
        }
    }

}
