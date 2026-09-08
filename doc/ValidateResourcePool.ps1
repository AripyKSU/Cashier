# Execute in the current Cashier Play Mode using the installed local connector.
param([int]$Port = 8090)
$ErrorActionPreference = 'Stop'
$health = Invoke-RestMethod "http://127.0.0.1:$Port/health"
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path.Replace('\','/')
if ($health.data.projectPath -ne $project -or $health.data.state -ne 'playing' -or $health.data.compileErrors) {
    throw 'Expected this Cashier project in Play Mode without compile errors.'
}
$code = @'
if (!Application.isPlaying || ResourceManager.Instance == null) throw new InvalidOperationException("Boot Cashier first.");
SessionState.SetString("Cashier.ResourcePoolCheck", "RUNNING");
RunCheck().Forget();
return "RUNNING";
}
private sealed class ProbeProvider : ResourceProviderBase
{
    public readonly List<ProvideHandle> Pending = new List<ProvideHandle>();
    public UnityEngine.Object Asset;
    public int Loads, Releases, Failures;
    public bool FailNext;
    public override void Provide(ProvideHandle handle) { Loads++; Pending.Add(handle); }
    public override void Release(IResourceLocation location, object asset) { Releases++; }
    public void Flush()
    {
        var pending = Pending.ToArray(); Pending.Clear();
        foreach (var handle in pending)
        {
            if (FailNext)
            {
                FailNext = false; Failures++;
                handle.Complete<UnityEngine.Object>(null, false, new InvalidOperationException("ResourcePoolCheck expected provider failure"));
            }
            else handle.Complete(Asset, true, (Exception)null);
        }
    }
}
private static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("ResourcePoolCheck: " + name);
}
private static async UniTask RunCheck()
{
    bool background = Application.runInBackground;
    Application.runInBackground = true;
    string key = "ResourcePoolCheck/" + Guid.NewGuid().ToString("N");
    var root = new GameObject("ResourcePoolCheck");
    root.SetActive(false);
    var resources = root.AddComponent<ResourceManager>();
    var pools = root.AddComponent<SimplePoolManager>();
    var provider = new ProbeProvider();
    provider.Initialize(key, null);
    var locator = new ResourceLocationMap(key);
    var source = new GameObject("ResourcePoolCheckSource");
    source.SetActive(false);
    var text = new TextAsset("ResourcePoolCheck");
    locator.Add(key, new ResourceLocationBase(key, key, provider.ProviderId, typeof(TextAsset)));
    string prefabKey = key + "/prefab";
    locator.Add(prefabKey, new ResourceLocationBase(prefabKey, prefabKey, provider.ProviderId, typeof(GameObject)));
    Addressables.ResourceManager.ResourceProviders.Add(provider);
    Addressables.AddResourceLocator(locator);
    SimplePool<Transform> local = null;
    try
    {
        provider.Asset = text;
        using (var cancel = new System.Threading.CancellationTokenSource())
        {
            var a = resources.LoadAssetAsync<TextAsset>(key).AsTask();
            var b = resources.LoadAssetAsync<TextAsset>(key, cancel.Token).AsTask();
            cancel.Cancel();
            try { await b; throw new Exception("cancel did not cancel"); } catch (OperationCanceledException) { }
            provider.Flush();
            Check(await a == text && provider.Loads == 1, "shared load / independent cancellation");
        }
        try { await resources.LoadAssetAsync<GameObject>(key); throw new Exception("type conflict accepted"); }
        catch (InvalidOperationException) { }
        resources.Release(key);
        Check(provider.Releases == 1, "one release for shared ownership");
        var old = resources.LoadAssetAsync<TextAsset>(key).AsTask();
        resources.Release(key);
        var next = resources.LoadAssetAsync<TextAsset>(key).AsTask();
        provider.Flush();
        try { await old; throw new Exception("released waiter succeeded"); } catch (OperationCanceledException) { }
        Check(await next == text, "late completion preserves new request");
        resources.ReleaseAll();
        await UniTask.NextFrame();
        Check(provider.Loads == provider.Releases, "pending release balanced");
        var pending = resources.LoadAssetAsync<TextAsset>(key).AsTask();
        resources.ReleaseAll();
        provider.Flush();
        try { await pending; throw new Exception("ReleaseAll waiter succeeded"); } catch (OperationCanceledException) { }
        await UniTask.NextFrame();
        Check(provider.Loads == provider.Releases, "ReleaseAll late completion balanced");

        provider.FailNext = true;
        var failure = resources.LoadAssetAsync<TextAsset>(key).AsTask();
        provider.Flush();
        bool failed = false;
        try { await failure; }
        catch (Exception error) { failed = error.ToString().Contains("ResourcePoolCheck expected provider failure"); }
        Check(failed, "provider failure propagated");
        Check(resources.GetResource<TextAsset>(key) == null, "failed load removed from cache");
        var retry = resources.LoadAssetAsync<TextAsset>(key).AsTask();
        provider.Flush();
        Check(await retry == text, "failed key can be loaded again");
        resources.Release(key);
        int callbacks = 0;
        resources.LoadAssetAsync<TextAsset>(key, value => { Check(value == text, "callback value"); callbacks++; });
        var taskApi = resources.LoadAssetAsyncTask<TextAsset>(key);
        provider.Flush();
        Check(await taskApi == text && callbacks == 1, "callback and Task share one load");
        resources.Release(key);
        var destroyed = resources.LoadAssetAsync<TextAsset>(key).AsTask();
        typeof(ResourceManager).GetMethod("OnSingletonDestroyed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(resources, null);
        provider.Flush();
        try { await destroyed; throw new Exception("destroyed manager returned result"); } catch (OperationCanceledException) { }
        Check(provider.Loads - provider.Failures == provider.Releases, "manager shutdown releases pending reference");

        local = new SimplePool<Transform>(2, source.transform);
        local.Prewarm(2);
        var rented = local.Get();
        Check(rented.gameObject.activeSelf && local.Available == 1, "prewarm/get");
        try { local.Release(source.transform); throw new Exception("foreign object accepted"); } catch (ArgumentException) { }
        Check(source != null, "foreign object preserved");
        local.Release(rented); local.Release(rented);
        Check(local.Available == 2, "double return");
        rented = local.Get();
        local.Clear(); local.Clear();
        await UniTask.NextFrame();
        Check(rented == null && local.TotalOwned == 0 && local.Available == 0, "clear rented and idle objects");
        try { local.Get(); throw new Exception("closed pool reused"); } catch (ObjectDisposedException) { }
        local = new SimplePool<Transform>(1, source.transform, onGet: _ => { throw new InvalidOperationException("expected hook failure"); });
        try { local.Get(); throw new Exception("hook error swallowed"); } catch (InvalidOperationException) { }
        Check(local.TotalOwned == 0, "hook failure releases ownership");
        local.Clear();

        provider.Asset = source;
        var first = pools.CreatePoolAsync<Transform>(prefabKey, 2, 2).AsTask();
        Check(!await pools.CreatePoolAsync<Transform>(prefabKey, 2, 2), "concurrent pool create rejected");
        for (int i = 0; i < 30 && !first.IsCompleted; i++) { provider.Flush(); await UniTask.NextFrame(); }
        Check(first.IsCompleted && await first, "addressable prewarm");
        Check(!await pools.CreatePoolAsync<BoxCollider>(prefabKey, 2, 2), "pool type mismatch rejected");
        var addressableItem = pools.Get<Transform>(prefabKey);
        pools.ClearAll();
        await UniTask.NextFrame();
        Check(addressableItem == null, "addressable rented instance cleared");
        var removed = pools.CreatePoolAsync<Transform>(prefabKey, 2, 2).AsTask();
        pools.ClearPool(prefabKey);
        for (int i = 0; i < 30 && !removed.IsCompleted; i++) { provider.Flush(); await UniTask.NextFrame(); }
        Check(removed.IsCompleted && !await removed, "clear during prewarm reports failure");
        Check(!pools.TryGetPool<Transform>(prefabKey, out _), "cleared pool not published");
        var missing = pools.CreatePoolAsync<BoxCollider>(prefabKey, 1, 1).AsTask();
        for (int i = 0; i < 30 && !missing.IsCompleted; i++) { provider.Flush(); await UniTask.NextFrame(); }
        Check(missing.IsCompleted && !await missing, "missing component reports failure");
        Check(!pools.TryGetPool<BoxCollider>(prefabKey, out _), "failed pool not published");
        await UniTask.NextFrame();
        Check(provider.Loads - provider.Failures == provider.Releases, "all successful provider references released");
        SessionState.SetString("Cashier.ResourcePoolCheck", "PASS: shared load, cancellation, type mismatch, late release, failed load/retry, callback/Task, local/addressable pool, ownership, hook failure, prewarm failure and shutdown; loads=" + provider.Loads + ", failed=" + provider.Failures + ", releases=" + provider.Releases);
    }
    catch (Exception error) { SessionState.SetString("Cashier.ResourcePoolCheck", "FAIL: " + error); Debug.LogException(error); }
    finally
    {
        local?.Clear(); pools.ClearAll(); resources.ReleaseAll();
        provider.Flush();
        await UniTask.NextFrame();
        Addressables.RemoveResourceLocator(locator);
        Addressables.ResourceManager.ResourceProviders.Remove(provider);
        UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(source); UnityEngine.Object.Destroy(text);
        Application.runInBackground = background;
    }
}
private static object EndSnippet() { return null;
'@
$payload = @{command='exec';params=@{code=$code;usings=@('Cysharp.Threading.Tasks','UnityEngine.AddressableAssets','UnityEngine.AddressableAssets.ResourceLocators','UnityEngine.ResourceManagement.ResourceLocations','UnityEngine.ResourceManagement.ResourceProviders')}} | ConvertTo-Json -Depth 5
$result = Invoke-RestMethod "http://127.0.0.1:$Port/command" -Method Post -ContentType 'application/json' -Body ([System.Text.Encoding]::UTF8.GetBytes($payload)) -TimeoutSec 50
if (!$result.success) { throw ($result | ConvertTo-Json -Depth 5) }
for ($i=0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    $poll = @{command='exec';params=@{code='return SessionState.GetString("Cashier.ResourcePoolCheck", "MISSING");'}} | ConvertTo-Json
    $result = Invoke-RestMethod "http://127.0.0.1:$Port/command" -Method Post -ContentType 'application/json' -Body $poll -TimeoutSec 50
    if (!$result.success) { throw ($result | ConvertTo-Json -Depth 5) }
    if ($result.data -ne 'RUNNING') { $result.data; if ($result.data -notlike 'PASS:*') { throw 'Resource/pool check failed.' }; return }
}
throw 'Resource/pool check timed out.'
