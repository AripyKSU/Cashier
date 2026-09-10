using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;

/// <summary>메인 스레드에서 Addressables 자산과 인스턴스의 소유·해제를 관리한다.</summary>
public class ResourceManager : Singleton<ResourceManager>
{
    private const string TargetLabel = "Datas";
    // 진행 중 로드도 등록하여 동일 키·타입의 요청이 하나의 참조를 공유한다.
    private readonly Dictionary<string, AssetLoad> loadHandles = new Dictionary<string, AssetLoad>();
    private readonly List<AsyncOperationHandle<GameObject>> instantiateHandles = new List<AsyncOperationHandle<GameObject>>();
    private Task initialization;
    private bool isInitialized;
    private bool isDestroyed;
    // 전체 해제 전에 시작한 인스턴스는 늦게 완료되어도 다시 소유하지 않는다.
    private uint generation;

    /// <summary>초기화를 공유한다. 호출자 취소는 해당 대기만 취소한다.</summary>
    /// <param name="onComplete">초기화 성공 알림.</param>
    /// <param name="cancellationToken">호출자의 대기 취소.</param>
    /// <returns>초기화 완료.</returns>
    /// <exception cref="Exception">초기화 실패 또는 취소.</exception>
    public async UniTask InitAsync(Action onComplete = null, CancellationToken cancellationToken = default)
    {
        throwIfDestroyed();
        cancellationToken.ThrowIfCancellationRequested();
        if (!isInitialized)
        {
            if (initialization == null || initialization.IsFaulted || initialization.IsCanceled)
                initialization = initializeAsync().AsTask();
            await initialization.AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        throwIfDestroyed();
        cancellationToken.ThrowIfCancellationRequested();
        onComplete?.Invoke();
    }

    /// <summary>공유 로드 결과를 전달한다. 실패는 기록하고 null을 한 번 전달한다.</summary>
    /// <param name="key">Addressables 키.</param>
    /// <param name="onLoaded">결과 소비자. 종료 후에는 호출하지 않는다.</param>
    public void LoadAssetAsync<T>(string key, Action<T> onLoaded) where T : class
        => loadWithCallbackAsync(key, key, onLoaded, default).Forget();

    /// <summary>동일 키·타입의 자산 로드를 공유한다.</summary>
    /// <param name="key">Addressables 키.</param>
    /// <returns>로드한 자산.</returns>
    /// <exception cref="Exception">로드 실패, 타입 불일치 또는 해제 중 취소.</exception>
    public UniTask<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
        => loadAssetAsync<T>(key, key, default);

    /// <summary>다른 대기자에게 영향을 주지 않는 취소 가능한 공유 로드.</summary>
    /// <param name="key">Addressables 키.</param>
    /// <param name="cancellationToken">해당 호출자의 대기 취소.</param>
    /// <returns>로드한 자산.</returns>
    /// <exception cref="Exception">로드 실패, 타입 불일치 또는 취소.</exception>
    public UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken) where T : UnityEngine.Object
        => loadAssetAsync<T>(key, key, cancellationToken);

    /// <summary>기존 Task 소비자도 같은 로드·오류 경로를 사용한다.</summary>
    /// <param name="key">Addressables 키.</param>
    /// <returns>로드한 자산.</returns>
    /// <exception cref="Exception">로드 실패, 타입 불일치 또는 취소.</exception>
    public Task<T> LoadAssetAsyncTask<T>(string key) where T : class
        => loadAssetAsync<T>(key, key, default).AsTask();

    /// <summary>위치별 공유 로드를 시작한다. 취소 후 callback은 전달하지 않는다.</summary>
    /// <param name="locList">리소스 위치 목록.</param>
    /// <param name="onComp">성공 결과 또는 실패의 null.</param>
    /// <param name="cancellationToken">대기와 callback 취소.</param>
    /// <exception cref="ArgumentException">목록에 null 위치가 포함됨.</exception>
    public void LoadAssetsAsync<T>(IList<IResourceLocation> locList, Action<T> onComp, CancellationToken cancellationToken = default) where T : UnityEngine.Object
    {
        if (locList == null) return;
        foreach (var location in locList)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (location == null) throw new ArgumentException("리소스 위치가 null입니다.", nameof(locList));
            loadWithCallbackAsync(location.PrimaryKey, location, onComp, cancellationToken).Forget();
        }
    }

    /// <summary>고유 인스턴스를 생성한다. 실패는 기록하고 null을 반환한다.</summary>
    /// <param name="key">Prefab 키.</param>
    /// <param name="parent">생성 시 부모.</param>
    /// <param name="position">선택적 월드 위치.</param>
    /// <param name="rotation">선택적 월드 회전.</param>
    /// <returns>생성 결과. 실패 또는 생성 중 전체 해제 시 null.</returns>
    public async Task<GameObject> InstantiateAsyncTask(string key, Transform parent = null, Vector3? position = null, Quaternion? rotation = null)
    {
        AsyncOperationHandle<GameObject> handle = default;
        bool retained = false;
        uint startedGeneration = generation;
        try
        {
            throwIfDestroyed();
            validateKey(key);
            bool hadParent = parent != null;
            handle = position.HasValue || rotation.HasValue
                ? Addressables.InstantiateAsync(key, position ?? Vector3.zero, rotation ?? Quaternion.identity, parent)
                : Addressables.InstantiateAsync(key, parent);
            await handle.ToUniTask();
            if (isDestroyed || startedGeneration != generation || (hadParent && parent == null)) return null;
            instantiateHandles.Add(handle);
            retained = true;
            return handle.Result;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ResourceManager] Instantiate '{key}' failed: {exception}");
            return null;
        }
        finally
        {
            if (!retained && handle.IsValid()) releaseInstanceHandle(handle);
        }
    }

    /// <summary>완료된 캐시만 조회한다. 미로드·진행 중이면 null.</summary>
    /// <param name="key">자산 키.</param>
    /// <returns>캐시 자산 또는 null.</returns>
    /// <exception cref="InvalidOperationException">등록 타입 불일치.</exception>
    public T GetResource<T>(string key) where T : class
    {
        if (!loadHandles.TryGetValue(key, out var load)) return null;
        validateType<T>(key, load);
        return load.Handle.IsValid() && load.Handle.IsDone && load.Handle.Status == AsyncOperationStatus.Succeeded
            ? (T)load.Handle.Result : null;
    }

    /// <summary>로드된 Atlas에서 Sprite를 조회한다.</summary>
    /// <param name="atlasKey">Atlas 키.</param>
    /// <param name="key">Sprite 이름.</param>
    /// <returns>Sprite 또는 null.</returns>
    public Sprite GetSpriteFromAtlas(string atlasKey, string key) => GetResource<SpriteAtlas>(atlasKey)?.GetSprite(key);

    /// <summary>이 manager가 소유한 인스턴스만 한 번 해제한다.</summary>
    /// <param name="go">해제할 인스턴스. null 또는 비소유 객체는 무시한다.</param>
    public void ReleaseInstance(GameObject go)
    {
        if (go == null) return;
        int index = instantiateHandles.FindIndex(h => h.IsValid() && h.Result == go);
        if (index < 0) return;
        var handle = instantiateHandles[index];
        instantiateHandles.RemoveAt(index);
        releaseInstanceHandle(handle);
    }

    /// <summary>키의 공유 소유권을 종료한다. 기존 대기는 취소되고 새 요청은 새로 로드한다.</summary>
    /// <param name="key">해제할 키.</param>
    public void Release(string key)
    {
        if (!loadHandles.TryGetValue(key, out var load)) return;
        loadHandles.Remove(key);
        releaseAsset(load);
    }

    /// <summary>현재 자산·인스턴스와 진행 중 요청의 소유권을 종료한다. 이후 새 로드는 허용한다.</summary>
    public void ReleaseAll()
    {
        generation++;
        var assets = new List<AssetLoad>(loadHandles.Values);
        var instances = instantiateHandles.ToArray();
        loadHandles.Clear();
        instantiateHandles.Clear();
        foreach (var load in assets) releaseAsset(load);
        foreach (var handle in instances) releaseInstanceHandle(handle);
    }

    /// <summary>전역 Atlas 요청을 구독한다.</summary>
    protected override void OnSingletonAwake() => SpriteAtlasManager.atlasRequested += onAtlasRequested;

    /// <summary>새 요청을 차단한 후 구독과 자원을 정리한다.</summary>
    protected override void OnSingletonDestroyed()
    {
        isDestroyed = true;
        SpriteAtlasManager.atlasRequested -= onAtlasRequested;
        ReleaseAll();
        base.OnSingletonDestroyed();
    }

    /// <summary>공유 작업의 대기만 취소 가능하게 연결한다.</summary>
    /// <param name="key">캐시 키.</param>
    /// <param name="address">문자열 또는 리소스 위치.</param>
    /// <param name="token">대기 취소.</param>
    /// <returns>로드 결과.</returns>
    /// <exception cref="Exception">로드 실패, 타입 오류 또는 취소.</exception>
    private async UniTask<T> loadAssetAsync<T>(string key, object address, CancellationToken token) where T : class
    {
        throwIfDestroyed();
        validateKey(key);
        token.ThrowIfCancellationRequested();
        if (!loadHandles.TryGetValue(key, out var load))
        {
            var handle = Addressables.LoadAssetAsync<T>(address);
            load = new AssetLoad(handle, typeof(T));
            loadHandles.Add(key, load);
            var owned = load;
            handle.Completed += _ => completeAsset(key, owned);
        }
        validateType<T>(key, load);
        var result = await load.Completion.Task.AsUniTask().AttachExternalCancellation(token);
        // 해제와 await 재개가 같은 프레임에 겹쳐도 폐기된 결과를 전달하지 않는다.
        if (load.Released || isDestroyed) throw new OperationCanceledException("자산 소유권이 종료되었습니다.");
        return (T)result;
    }

    /// <summary>실패 핸들을 제거하거나 결과를 대기자에게 전달한다.</summary>
    /// <param name="key">캐시 키.</param>
    /// <param name="load">작업 소유권.</param>
    private void completeAsset(string key, AssetLoad load)
    {
        load.Completed = true;
        if (load.Released)
        {
            if (load.Handle.IsValid()) Addressables.Release(load.Handle);
            return;
        }
        if (load.Handle.Status == AsyncOperationStatus.Succeeded)
        {
            load.Completion.TrySetResult(load.Handle.Result);
            return;
        }
        var error = load.Handle.OperationException ?? new InvalidOperationException($"Load failed: {key}");
        if (loadHandles.TryGetValue(key, out var current) && ReferenceEquals(current, load)) loadHandles.Remove(key);
        load.Released = true;
        if (load.Handle.IsValid()) Addressables.Release(load.Handle);
        load.Completion.TrySetException(error);
    }

    /// <summary>완료 callback 전에는 핸들을 유지하고 callback에서 해제한다.</summary>
    /// <param name="load">해제할 소유권.</param>
    private static void releaseAsset(AssetLoad load)
    {
        load.Released = true;
        if (load.Completed && load.Handle.IsValid()) Addressables.Release(load.Handle);
        load.Completion.TrySetCanceled();
    }

    /// <summary>callback 예외를 로드 실패로 오인하여 두 번 호출하지 않는다.</summary>
    /// <param name="key">캐시 키.</param>
    /// <param name="address">로드 위치.</param>
    /// <param name="callback">결과 소비자.</param>
    /// <param name="token">대기 취소.</param>
    /// <returns>callback 전달 완료.</returns>
    private async UniTask loadWithCallbackAsync<T>(string key, object address, Action<T> callback, CancellationToken token) where T : class
    {
        T result = null;
        try { result = await loadAssetAsync<T>(key, address, token); }
        catch (OperationCanceledException) { return; }
        catch (Exception exception) { Debug.LogError($"[ResourceManager] Load '{key}' failed: {exception}"); }
        if (!isDestroyed && !token.IsCancellationRequested) callback?.Invoke(result);
    }

    /// <summary>등록 타입과 다른 요청을 조기에 거부한다.</summary>
    /// <param name="key">캐시 키.</param>
    /// <param name="load">등록 정보.</param>
    /// <exception cref="InvalidOperationException">타입 불일치.</exception>
    private static void validateType<T>(string key, AssetLoad load)
    {
        if (load.Type != typeof(T)) throw new InvalidOperationException($"Asset '{key}' is registered as {load.Type.Name}, requested {typeof(T).Name}.");
    }

    /// <summary>빈 키를 입력 경계에서 거부한다.</summary>
    /// <param name="key">Addressables 키.</param>
    /// <exception cref="ArgumentException">빈 키.</exception>
    private static void validateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Addressables key is required.", nameof(key));
    }

    /// <summary>종료 이후의 새 작업을 거부한다.</summary>
    /// <exception cref="ObjectDisposedException">manager 종료.</exception>
    private void throwIfDestroyed()
    {
        if (isDestroyed) throw new ObjectDisposedException(nameof(ResourceManager));
    }

    /// <summary>성공 인스턴스와 실패 작업에 맞는 해제 API를 호출한다.</summary>
    /// <param name="handle">소유 핸들.</param>
    private static void releaseInstanceHandle(AsyncOperationHandle<GameObject> handle)
    {
        if (!handle.IsValid()) return;
        if (handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(handle);
        else Addressables.Release(handle);
    }

    /// <summary>호출자별 취소와 분리된 manager 수명의 초기화.</summary>
    /// <returns>카탈로그·의존성 준비 완료.</returns>
    /// <exception cref="Exception">Addressables 실패 또는 manager 종료.</exception>
    private async UniTask initializeAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        var init = Addressables.InitializeAsync(false);
        try { await init.ToUniTask(cancellationToken: token); }
        finally { if (init.IsValid()) Addressables.Release(init); }
        var updates = Addressables.CheckForCatalogUpdates(false);
        try
        {
            await updates.ToUniTask(cancellationToken: token);
            if (updates.Result.Count > 0)
            {
                var catalogs = Addressables.UpdateCatalogs(updates.Result, false);
                try { await catalogs.ToUniTask(cancellationToken: token); }
                finally { if (catalogs.IsValid()) Addressables.Release(catalogs); }
            }
        }
        finally { if (updates.IsValid()) Addressables.Release(updates); }
        var locations = Addressables.LoadResourceLocationsAsync(TargetLabel, typeof(object));
        try
        {
            await locations.ToUniTask(cancellationToken: token);
            if (locations.Result.Count > 0)
            {
                var download = Addressables.DownloadDependenciesAsync(locations.Result, false);
                try { await download.ToUniTask(cancellationToken: token); }
                finally { if (download.IsValid()) Addressables.Release(download); }
            }
        }
        finally { if (locations.IsValid()) Addressables.Release(locations); }
        throwIfDestroyed();
        isInitialized = true;
    }

    /// <summary>Atlas 요청을 공유 로드로 연결한다.</summary>
    /// <param name="tag">Atlas 키.</param>
    /// <param name="onComplete">Atlas 소비자.</param>
    private void onAtlasRequested(string tag, Action<SpriteAtlas> onComplete) => LoadAssetAsync(tag, onComplete);

    /// <summary>한 번의 Addressables 참조와 여러 대기자의 완료 상태.</summary>
    private sealed class AssetLoad
    {
        internal readonly AsyncOperationHandle Handle;
        internal readonly Type Type;
        internal readonly TaskCompletionSource<object> Completion = new TaskCompletionSource<object>();
        internal bool Released;
        internal bool Completed;

        /// <summary>진행 중 로드의 소유 정보를 생성한다.</summary>
        /// <param name="handle">획득한 핸들.</param>
        /// <param name="type">최초 요청 타입.</param>
        internal AssetLoad(AsyncOperationHandle handle, Type type) { Handle = handle; Type = type; }
    }
}
