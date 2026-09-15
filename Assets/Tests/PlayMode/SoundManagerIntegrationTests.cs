using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 실제 ResourceManager와 Addressables를 통과하는 SoundManager 초기화·재생 계약을 검사한다.
/// </summary>
public sealed class SoundManagerIntegrationTests
{
    private GameObject root;
    private ResourceManager resources;
    private DataTableManager tables;
    private SoundManager sounds;

    /// <summary>테스트 전용 manager를 실제 Addressables와 CSV로 초기화한다.</summary>
    /// <returns>manager 초기화 완료 대기.</returns>
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(ResourceManager.Instance, Is.Null);
        Assert.That(DataTableManager.Instance, Is.Null);
        Assert.That(SoundManager.Instance, Is.Null);

        root = new GameObject("SoundManagerIntegrationFixture");
        resources = root.AddComponent<ResourceManager>();
        yield return wait(resources.InitAsync().AsTask());

        tables = root.AddComponent<DataTableManager>();
        yield return wait(tables.EnsureDataLoadedAsync().AsTask());

        sounds = root.AddComponent<SoundManager>();
    }

    /// <summary>테스트 전용 manager를 제거하고 singleton 수명을 확인한다.</summary>
    /// <returns>Unity destruction callback 완료 대기.</returns>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        UnityEngine.Object.Destroy(root);
        yield return null;

        Assert.That(ResourceManager.Instance, Is.Null);
        Assert.That(DataTableManager.Instance, Is.Null);
        Assert.That(SoundManager.Instance, Is.Null);
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>20개 SoundKeys가 실제 ResourceData address의 AudioClip으로 로드되는지 확인한다.</summary>
    /// <returns>전체 사운드 초기화 완료 대기.</returns>
    [UnityTest]
    public IEnumerator InitializeLoadsAllSoundClips()
    {
        Task initialization = sounds.InitializeAsync(tables).AsTask();
        yield return wait(initialization);

        Assert.That(sounds.IsInitialized, Is.True);
        Assert.That(sounds.CachedClips.Count, Is.EqualTo(20));
        foreach (uint resourceIdx in SoundKeys.All)
        {
            Assert.That(sounds.CachedClips[resourceIdx], Is.Not.Null);
            Assert.That(
                resources.GetResource<AudioClip>(tables.GetDB<ResourceDataTable>(DataTableType.Resource)
                    .GetResourcePath(resourceIdx)),
                Is.SameAs(sounds.CachedClips[resourceIdx]));
        }
    }

    /// <summary>동시 초기화 공유와 성공 후 반복 호출의 멱등성을 확인한다.</summary>
    /// <returns>공유 초기화 완료 대기.</returns>
    [UnityTest]
    public IEnumerator InitializeSharesAndRepeatedCallIsIdempotent()
    {
        Task first = sounds.InitializeAsync(tables).AsTask();
        Task second = sounds.InitializeAsync(tables).AsTask();
        yield return wait(first);
        yield return wait(second);

        var cache = sounds.CachedClips;
        Task repeated = sounds.InitializeAsync(tables).AsTask();
        yield return wait(repeated);

        Assert.That(sounds.IsInitialized, Is.True);
        Assert.That(sounds.CachedClips, Is.SameAs(cache));
    }

    /// <summary>호출자 취소가 공유 로드와 공개 캐시에 영향을 주지 않는지 확인한다.</summary>
    /// <returns>취소 대기와 원본 초기화 완료 대기.</returns>
    [UnityTest]
    public IEnumerator CallerCancellationDoesNotCancelSharedInitialization()
    {
        Task first = sounds.InitializeAsync(tables).AsTask();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Task cancelled = sounds.InitializeAsync(tables, cancellation.Token).AsTask();
        yield return wait(cancelled, false);
        Assert.That(
            cancelled.IsCanceled
                || cancelled.Exception?.GetBaseException() is OperationCanceledException,
            Is.True);
        Assert.That(sounds.IsInitialized, Is.False);

        yield return wait(first);
        Assert.That(sounds.IsInitialized, Is.True);
        Assert.That(sounds.CachedClips.Count, Is.EqualTo(20));
    }

    /// <summary>필수 ResourceData 누락이 부분 캐시를 공개하지 않고 재시도 가능한지 확인한다.</summary>
    /// <returns>실패와 복구 초기화 완료 대기.</returns>
    [UnityTest]
    public IEnumerator MissingResourceFailsWithoutPublishingPartialCacheAndCanRetry()
    {
        ResourceDataTable resourceTable =
            tables.GetDB<ResourceDataTable>(DataTableType.Resource);
        Dictionary<uint, ResourceData> rows = getResourceRows(resourceTable);
        ResourceData removed = rows[SoundKeys.SupervisorBgm];
        rows.Remove(SoundKeys.SupervisorBgm);

        Task failed = sounds.InitializeAsync(tables).AsTask();
        yield return wait(failed, false);

        Assert.That(failed.IsFaulted, Is.True);
        Assert.That(failed.Exception.GetBaseException(), Is.TypeOf<InvalidDataException>());
        Assert.That(sounds.IsInitialized, Is.False);
        Assert.That(sounds.CachedClips, Is.Empty);

        rows.Add(SoundKeys.SupervisorBgm, removed);
        Task retry = sounds.InitializeAsync(tables).AsTask();
        yield return wait(retry);
        Assert.That(sounds.IsInitialized, Is.True);
        Assert.That(sounds.CachedClips.Count, Is.EqualTo(20));
    }

    /// <summary>빈 Addressables address가 부분 캐시 없이 실패하는지 확인한다.</summary>
    /// <returns>잘못된 address 검증 실패 대기.</returns>
    [UnityTest]
    public IEnumerator EmptyAddressFailsWithoutPublishingPartialCache()
    {
        ResourceDataTable resourceTable =
            tables.GetDB<ResourceDataTable>(DataTableType.Resource);
        Dictionary<uint, ResourceData> rows = getResourceRows(resourceTable);
        ResourceData resource = rows[SoundKeys.SupervisorBgm];
        string originalPath = resource.Path;
        resource.Path = " ";

        try
        {
            Task failed = sounds.InitializeAsync(tables).AsTask();
            yield return wait(failed, false);

            Assert.That(failed.IsFaulted, Is.True);
            Assert.That(failed.Exception.GetBaseException(), Is.TypeOf<InvalidDataException>());
            Assert.That(sounds.IsInitialized, Is.False);
            Assert.That(sounds.CachedClips, Is.Empty);
        }
        finally
        {
            resource.Path = originalPath;
        }
    }

    /// <summary>BGM, one-shot SFX와 반복 SFX가 null 없이 동작하고 중복 loop가 위치를 유지하는지 확인한다.</summary>
    /// <returns>초기화와 재생 상태 반영 대기.</returns>
    [UnityTest]
    public IEnumerator PlaybackApisKeepExistingBgmAndLoopBehavior()
    {
        yield return wait(sounds.InitializeAsync(tables).AsTask());

        sounds.PlayBgm(SoundKeys.SupervisorBgm);
        AudioSource bgm = root.transform.Find("BGM Source").GetComponent<AudioSource>();
        Assert.That(bgm.clip, Is.SameAs(sounds.CachedClips[SoundKeys.SupervisorBgm]));
        Assert.That(bgm.loop, Is.True);
        Assert.That(bgm.isPlaying, Is.True);

        bgm.time = 0.1f;
        sounds.PlayBgm(SoundKeys.SupervisorBgm);
        Assert.That(bgm.time, Is.GreaterThanOrEqualTo(0.05f));

        sounds.PlaySfx(SoundKeys.CalculatorButton, 2f);
        sounds.PlayLoopSfx(SoundKeys.Vacuum, 2f);
        AudioSource loop = root.transform.Find("Loop SFX Source 4268").GetComponent<AudioSource>();
        Assert.That(loop.clip, Is.SameAs(sounds.CachedClips[SoundKeys.Vacuum]));
        Assert.That(loop.volume, Is.EqualTo(1f));
        Assert.That(loop.isPlaying, Is.True);

        loop.time = 0.1f;
        sounds.PlayLoopSfx(SoundKeys.Vacuum, 0.25f);
        Assert.That(loop.time, Is.GreaterThanOrEqualTo(0.05f));
        Assert.That(loop.volume, Is.EqualTo(0.25f));

        sounds.StopLoopSfx(SoundKeys.Vacuum);
        Assert.That(loop.isPlaying, Is.False);
        Assert.That(loop.clip, Is.Null);
    }

    private static Dictionary<uint, ResourceData> getResourceRows(ResourceDataTable table)
    {
        FieldInfo field = typeof(ResourceDataTable).GetField(
            "dataDict",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (Dictionary<uint, ResourceData>)field.GetValue(table);
    }

    private static IEnumerator wait(Task task, bool requireSuccess = true)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(task.IsCompleted, Is.True, "SoundManager task timed out.");
        if (requireSuccess)
        {
            task.GetAwaiter().GetResult();
        }
    }
}
