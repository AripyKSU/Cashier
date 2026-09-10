using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>메인 스레드에서 키별 풀 생성·조회·전체 종료를 관리한다.</summary>
public class SimplePoolManager : Singleton<SimplePoolManager>
{
    private readonly Dictionary<string, IPool> poolContainer = new Dictionary<string, IPool>();
    // 생성 중 풀도 소유하되 준비가 끝나기 전 조회는 거부한다.
    private readonly HashSet<string> creating = new HashSet<string>();
    private bool isDestroyed;

    /// <summary>풀을 준비한 뒤 공개한다. 같은 키의 진행 중 생성은 false로 거부한다.</summary>
    /// <param name="addressableKey">Prefab 키.</param>
    /// <param name="capacity">양수 정원.</param>
    /// <param name="prewarmCount">0부터 정원까지 초기 준비 수.</param>
    /// <param name="parent">반환 부모.</param>
    /// <param name="onGet">대여 hook.</param>
    /// <param name="onRelease">반환 hook.</param>
    /// <returns>준비 성공 여부. 중복·타입 충돌·실패는 false.</returns>
    public async UniTask<bool> CreatePoolAsync<T>(string addressableKey, int capacity, int prewarmCount, Transform parent = null, Action<T> onGet = null, Action<T> onRelease = null) where T : Component
    {
        SimplePool<T> pool = null;
        try
        {
            if (isDestroyed) throw new ObjectDisposedException(nameof(SimplePoolManager));
            if (string.IsNullOrWhiteSpace(addressableKey)) throw new ArgumentException("Prefab key is required.", nameof(addressableKey));
            if (capacity <= 0 || prewarmCount < 0 || prewarmCount > capacity) throw new ArgumentOutOfRangeException(nameof(prewarmCount));
            if (creating.Contains(addressableKey)) return false;
            if (poolContainer.TryGetValue(addressableKey, out var existing))
                return existing is SimplePool<T> typed && !typed.IsClosed && typed.Capacity == capacity;
            pool = new SimplePool<T>(capacity, addressableKey, parent, onGet, onRelease);
            poolContainer.Add(addressableKey, pool);
            creating.Add(addressableKey);
            await pool.PrewarmAsync(prewarmCount);
            return !isDestroyed && !pool.IsClosed;
        }
        catch (Exception exception)
        {
            pool?.Clear();
            Debug.LogError($"[SimplePoolManager] Create '{addressableKey}' failed: {exception}");
            return false;
        }
        finally
        {
            // 이전 생성이 늦게 완료되어도 같은 키의 새 풀을 제거하지 않는다.
            if (pool != null && poolContainer.TryGetValue(addressableKey, out var current) && ReferenceEquals(current, pool))
            {
                creating.Remove(addressableKey);
                if (pool.IsClosed) poolContainer.Remove(addressableKey);
            }
        }
    }

    /// <summary>준비된 타입 일치 풀에서 대여한다.</summary>
    /// <param name="addressableKey">등록 키.</param>
    /// <returns>대여 객체. 풀 누락·준비 중·타입 불일치·소진 시 null.</returns>
    public T Get<T>(string addressableKey) where T : Component
        => TryGetPool<T>(addressableKey, out var pool) ? pool.Get() : null;

    /// <summary>일치하는 풀로 반환한다. 풀을 찾지 못하면 객체를 보존하고 오류를 기록한다.</summary>
    /// <param name="addressableKey">등록 키.</param>
    /// <param name="instance">반환 객체.</param>
    /// <exception cref="Exception">풀 소유권 위반 또는 hook 실패.</exception>
    public void Release<T>(string addressableKey, T instance) where T : Component
    {
        if (TryGetPool<T>(addressableKey, out var pool)) pool.Release(instance);
        else Debug.LogError($"[SimplePoolManager] Ready pool<{typeof(T).Name}> not found: {addressableKey}");
    }

    /// <summary>대여 중·준비 중 객체까지 풀의 전체 수명을 종료한다.</summary>
    /// <param name="addressableKey">제거할 키.</param>
    public void ClearPool(string addressableKey)
    {
        if (!poolContainer.TryGetValue(addressableKey, out var pool)) return;
        poolContainer.Remove(addressableKey);
        creating.Remove(addressableKey);
        pool.Clear();
    }

    /// <summary>현재 등록된 모든 풀을 종료한다.</summary>
    public void ClearAll()
    {
        var pools = new List<IPool>(poolContainer.Values);
        poolContainer.Clear();
        creating.Clear();
        foreach (var pool in pools) pool.Clear();
    }

    /// <summary>준비 완료·타입 일치·미종료 풀만 조회한다.</summary>
    /// <param name="addressableKey">등록 키.</param>
    /// <param name="pool">조회 결과. 실패하면 null.</param>
    /// <returns>사용 가능한 풀 존재 여부.</returns>
    public bool TryGetPool<T>(string addressableKey, out SimplePool<T> pool) where T : Component
    {
        pool = null;
        if (isDestroyed || string.IsNullOrWhiteSpace(addressableKey) || creating.Contains(addressableKey)) return false;
        if (!poolContainer.TryGetValue(addressableKey, out var found) || !(found is SimplePool<T> typed) || typed.IsClosed) return false;
        pool = typed;
        return true;
    }

    /// <summary>종료 이후 생성 요청을 차단하고 모든 풀을 정리한다.</summary>
    protected override void OnSingletonDestroyed()
    {
        isDestroyed = true;
        ClearAll();
        base.OnSingletonDestroyed();
    }
}
