using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>실제 Addressables API에 테스트 provider만 주입해 리소스/풀의 비동기 소유권을 검사한다.</summary>
public sealed class ResourcePoolTests
{
    private GameObject root;
    private GameObject source;
    private TextAsset text;
    private ResourceManager resources;
    private SimplePoolManager pools;
    private ProbeProvider provider;
    private ResourceLocationMap locator;
    private SimplePool<Transform> local;
    private string key;
    private string prefabKey;

    /// <summary>사용자 manager가 없는 Test Runner 씬에만 격리 fixture를 만든다.</summary>
    [SetUp]
    public void SetUp()
    {
        Assert.That(ResourceManager.Instance, Is.Null, "Do not run inside user gameplay.");
        key = "ResourcePoolTests/" + Guid.NewGuid().ToString("N"); prefabKey = key + "/prefab";
        root = new GameObject("ResourcePoolTests"); resources = root.AddComponent<ResourceManager>(); pools = root.AddComponent<SimplePoolManager>();
        source = new GameObject("ResourcePoolSource"); source.SetActive(false); text = new TextAsset("probe");
        provider = new ProbeProvider(); provider.Initialize(key, null); provider.Asset = text;
        locator = new ResourceLocationMap(key);
        locator.Add(key, new ResourceLocationBase(key, key, provider.ProviderId, typeof(TextAsset)));
        locator.Add(prefabKey, new ResourceLocationBase(prefabKey, prefabKey, provider.ProviderId, typeof(GameObject)));
        Addressables.ResourceManager.ResourceProviders.Add(provider); Addressables.AddResourceLocator(locator);
    }

    /// <summary>모든 fixture 소유 참조와 locator만 정리하며 공유 설정 파일은 변경하지 않는다.</summary>
    /// <returns>Destroy 및 늦은 callback 완료 대기.</returns>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        local?.Clear(); if (pools != null) pools.ClearAll(); if (resources != null) resources.ReleaseAll(); provider?.Flush();
        yield return null;
        if (locator != null) Addressables.RemoveResourceLocator(locator);
        if (provider != null) Addressables.ResourceManager.ResourceProviders.Remove(provider);
        Object.Destroy(root); Object.Destroy(source); Object.Destroy(text); yield return null;
        Assert.That(ResourceManager.Instance, Is.Null); Assert.That(SimplePoolManager.Instance, Is.Null);
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>동일 키의 로드 공유·개별 취소·타입 충돌을 검사한다.</summary>
    /// <returns>비동기 완료 대기.</returns>
    [UnityTest]
    public IEnumerator SharedLoadAndIndependentCancellation()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            var a = resources.LoadAssetAsync<TextAsset>(key).AsTask(); var b = resources.LoadAssetAsync<TextAsset>(key, cancellation.Token).AsTask();
            cancellation.Cancel(); yield return wait(b, false); Assert.Catch<OperationCanceledException>(() => b.GetAwaiter().GetResult());
            provider.Flush(); yield return wait(a); Assert.That(a.Result, Is.SameAs(text)); Assert.That(provider.Loads, Is.EqualTo(1));
        }
        var conflict = resources.LoadAssetAsync<GameObject>(key).AsTask(); yield return wait(conflict, false);
        Assert.Throws<InvalidOperationException>(() => conflict.GetAwaiter().GetResult());
        resources.Release(key); Assert.That(provider.Releases, Is.EqualTo(1));
    }

    /// <summary>해제 이후 늦은 완료가 새 요청을 덮거나 참조를 유출하지 않는다.</summary>
    /// <returns>늦은 완료 및 해제 대기.</returns>
    [UnityTest]
    public IEnumerator ReleaseDuringLoadAndReleaseAll()
    {
        var old = resources.LoadAssetAsync<TextAsset>(key).AsTask(); resources.Release(key);
        var next = resources.LoadAssetAsync<TextAsset>(key).AsTask(); provider.Flush(); yield return wait(old, false); yield return wait(next);
        Assert.Catch<OperationCanceledException>(() => old.GetAwaiter().GetResult()); Assert.That(next.Result, Is.SameAs(text));
        resources.ReleaseAll(); yield return null; Assert.That(provider.Loads, Is.EqualTo(provider.Releases));
        var pending = resources.LoadAssetAsync<TextAsset>(key).AsTask(); resources.ReleaseAll(); provider.Flush(); yield return wait(pending, false);
        Assert.Catch<OperationCanceledException>(() => pending.GetAwaiter().GetResult()); yield return null;
        Assert.That(provider.Loads, Is.EqualTo(provider.Releases));
    }

    /// <summary>provider 오류·재시도·callback/Task 공유·manager 파괴 취소를 검사한다.</summary>
    /// <returns>각 비동기 요청 완료 대기.</returns>
    [UnityTest]
    public IEnumerator FailureRetryCallbacksAndShutdown()
    {
        provider.FailNext = true;
        LogAssert.Expect(LogType.Error, new Regex("^System.InvalidOperationException: ResourcePoolCheck expected provider failure"));
        var failure = resources.LoadAssetAsync<TextAsset>(key).AsTask(); provider.Flush(); yield return wait(failure, false);
        Assert.That(Assert.Catch(() => failure.GetAwaiter().GetResult()).ToString(), Does.Contain("ResourcePoolCheck expected provider failure"));
        Assert.That(resources.GetResource<TextAsset>(key), Is.Null);
        var retry = resources.LoadAssetAsync<TextAsset>(key).AsTask(); provider.Flush(); yield return wait(retry); Assert.That(retry.Result, Is.SameAs(text)); resources.Release(key);
        int callbacks = 0; resources.LoadAssetAsync<TextAsset>(key, value => { Assert.That(value, Is.SameAs(text)); callbacks++; });
        var task = resources.LoadAssetAsyncTask<TextAsset>(key); provider.Flush(); yield return wait(task);
        Assert.That(callbacks, Is.EqualTo(1)); resources.Release(key);
        var destroyed = resources.LoadAssetAsync<TextAsset>(key).AsTask(); Object.Destroy(root); yield return null; provider.Flush(); yield return wait(destroyed, false);
        Assert.Catch<OperationCanceledException>(() => destroyed.GetAwaiter().GetResult());
        Assert.That(provider.Loads - provider.Failures, Is.EqualTo(provider.Releases));
    }

    /// <summary>동기 풀의 이중반환·외부객체 보호·대여 객체 정리·hook 실패를 검사한다.</summary>
    /// <returns>Destroy 완료 대기.</returns>
    [UnityTest]
    public IEnumerator LocalPoolOwnershipAndHookFailure()
    {
        local = new SimplePool<Transform>(2, source.transform); local.Prewarm(2); var rented = local.Get();
        Assert.That(rented.gameObject.activeSelf); Assert.That(local.Available, Is.EqualTo(1));
        Assert.Throws<ArgumentException>(() => local.Release(source.transform)); Assert.That(source != null);
        local.Release(rented); local.Release(rented); Assert.That(local.Available, Is.EqualTo(2));
        rented = local.Get(); local.Clear(); local.Clear(); yield return null;
        Assert.That(rented == null); Assert.That(local.TotalOwned, Is.Zero); Assert.That(local.Available, Is.Zero);
        Assert.Throws<ObjectDisposedException>(() => local.Get());
        local = new SimplePool<Transform>(1, source.transform, onGet: _ => throw new InvalidOperationException("expected hook failure"));
        Assert.Throws<InvalidOperationException>(() => local.Get()); Assert.That(local.TotalOwned, Is.Zero);
    }

    /// <summary>Addressables 풀의 동시 생성·타입·prewarm 취소·누락 component를 검사한다.</summary>
    /// <returns>provider와 Unity 프레임 완료 대기.</returns>
    [UnityTest]
    public IEnumerator AddressablePoolPrewarmAndCancellation()
    {
        provider.Asset = source;
        var first = pools.CreatePoolAsync<Transform>(prefabKey, 2, 2).AsTask();
        var duplicate = pools.CreatePoolAsync<Transform>(prefabKey, 2, 2).AsTask(); yield return wait(duplicate); Assert.That(duplicate.Result, Is.False);
        yield return flushUntil(first); Assert.That(first.Result);
        var wrongType = pools.CreatePoolAsync<BoxCollider>(prefabKey, 2, 2).AsTask(); yield return wait(wrongType); Assert.That(wrongType.Result, Is.False);
        var item = pools.Get<Transform>(prefabKey); pools.ClearAll(); yield return null; Assert.That(item == null);
        LogAssert.Expect(LogType.Error, new Regex(@"\[SimplePoolManager\] Create '.*' failed: System.InvalidOperationException: Pool closed, parent destroyed or Transform missing:"));
        var removed = pools.CreatePoolAsync<Transform>(prefabKey, 2, 2).AsTask(); pools.ClearPool(prefabKey);
        yield return flushUntil(removed); Assert.That(removed.Result, Is.False); Assert.That(pools.TryGetPool<Transform>(prefabKey, out _), Is.False);
        LogAssert.Expect(LogType.Error, new Regex(@"\[SimplePoolManager\] Create '.*' failed: System.InvalidOperationException: Pool closed, parent destroyed or BoxCollider missing:"));
        var missing = pools.CreatePoolAsync<BoxCollider>(prefabKey, 1, 1).AsTask(); yield return flushUntil(missing);
        Assert.That(missing.Result, Is.False); Assert.That(pools.TryGetPool<BoxCollider>(prefabKey, out _), Is.False);
        yield return null; Assert.That(provider.Loads - provider.Failures, Is.EqualTo(provider.Releases));
    }

    /// <summary>대기 중 provider 완료를 진행하되 무한 대기하지 않는다.</summary>
    /// <param name="task">완료 대상.</param><returns>완료 대기.</returns>
    private IEnumerator flushUntil(Task task)
    {
        for (int i = 0; i < 60 && !task.IsCompleted; i++) { provider.Flush(); yield return null; }
        Assert.That(task.IsCompleted, "Provider did not complete within 60 frames."); task.GetAwaiter().GetResult();
    }

    /// <summary>취소/실패를 숨기지 않고 제한 시간까지 기다린다.</summary>
    /// <param name="task">대상 Task.</param><param name="requireSuccess">false는 호출자가 오류를 직접 검사한다.</param><returns>완료 대기.</returns>
    private static IEnumerator wait(Task task, bool requireSuccess = true)
    {
        float end = Time.realtimeSinceStartup + 10;
        while (!task.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(task.IsCompleted, "API task timed out."); if (requireSuccess) task.GetAwaiter().GetResult();
    }

    /// <summary>실제 ResourceManager 경로를 통과하며 완료 시각만 제어하는 fixture provider.</summary>
    private sealed class ProbeProvider : ResourceProviderBase
    {
        private readonly List<ProvideHandle> pending = new List<ProvideHandle>();
        public Object Asset;
        public int Loads;
        public int Releases;
        public int Failures;
        public bool FailNext;

        /// <summary>호출자가 Flush할 때까지 완료를 보류한다.</summary>
        /// <param name="handle">요청 handle.</param>
        public override void Provide(ProvideHandle handle) { Loads++; pending.Add(handle); }
        /// <summary>성공한 provider 참조 해제를 센다.</summary>
        /// <param name="location">해제 위치.</param><param name="asset">해제 자산.</param>
        public override void Release(IResourceLocation location, object asset) { Releases++; }
        /// <summary>현재 대기 요청만 성공 또는 승인된 실패로 완료한다.</summary>
        public void Flush()
        {
            var batch = pending.ToArray(); pending.Clear();
            foreach (var handle in batch)
            {
                if (FailNext) { FailNext = false; Failures++; handle.Complete<Object>(null, false, new InvalidOperationException("ResourcePoolCheck expected provider failure")); }
                else handle.Complete(Asset, true, (Exception)null);
            }
        }
    }
}
