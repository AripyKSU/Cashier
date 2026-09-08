using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>manager가 소유한 풀을 종료하는 공통 계약.</summary>
public interface IPool
{
    /// <summary>대여 중 객체까지 정리하고 풀을 영구 종료한다. 반복 호출은 무시한다.</summary>
    void Clear();
}

/// <summary>메인 스레드에서 제한된 수의 Component를 대여한다. Clear 이후 재사용하지 않는다.</summary>
public class SimplePool<T> : IPool where T : Component
{
    private readonly int capacity;
    private readonly T prefab;
    private readonly Transform parent;
    private readonly bool hadParent;
    private readonly string addressableKey;
    private readonly ResourceManager resources;
    private readonly Action<T> onGet;
    private readonly Action<T> onRelease;
    // 대여 중인 객체도 추적하여 전체 종료 시 빠짐없이 해제한다.
    private readonly Dictionary<EntityId, T> owned = new Dictionary<EntityId, T>();
    private readonly Queue<EntityId> available = new Queue<EntityId>();
    private readonly HashSet<EntityId> pooled = new HashSet<EntityId>();
    private bool isClosed;
    private bool isBusy;
    private int totalCreated;

    /// <summary>반환되어 대기 중인 슬롯 수.</summary>
    public int Available => pooled.Count;
    /// <summary>현재 소유한 객체 수. 외부 직접 Destroy는 지원하지 않는다.</summary>
    public int TotalOwned => owned.Count;
    /// <summary>풀 수명 동안 생성한 누적 수. 종료 시 0으로 초기화한다.</summary>
    public int TotalCreated => totalCreated;
    /// <summary>동시에 소유할 수 있는 최대 객체 수.</summary>
    public int Capacity => capacity;
    /// <summary>종료된 풀인지 여부.</summary>
    public bool IsClosed => isClosed;

    /// <summary>기존 Prefab으로 동기 생성 가능한 풀을 구성한다.</summary>
    /// <param name="capacity">양수 정원.</param>
    /// <param name="prefab">생성 원본.</param>
    /// <param name="parent">반환 시 부모. 살아 있는 동안 유지해야 한다.</param>
    /// <param name="onGet">대여 hook. 풀 변경 재진입은 금지한다.</param>
    /// <param name="onRelease">반환 hook. 풀 변경 재진입은 금지한다.</param>
    /// <exception cref="ArgumentException">정원 또는 Prefab 오류.</exception>
    public SimplePool(int capacity, T prefab, Transform parent = null, Action<T> onGet = null, Action<T> onRelease = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        this.capacity = capacity;
        this.prefab = prefab;
        this.parent = parent;
        hadParent = parent != null;
        this.onGet = onGet;
        this.onRelease = onRelease;
    }

    /// <summary>Addressables 풀을 구성한다. Get 전 PrewarmAsync가 필요하다.</summary>
    /// <param name="capacity">양수 정원.</param>
    /// <param name="addressableKey">Prefab 키.</param>
    /// <param name="parent">반환 시 부모.</param>
    /// <param name="onGet">대여 hook.</param>
    /// <param name="onRelease">반환 hook.</param>
    /// <exception cref="ArgumentException">정원 또는 키 오류.</exception>
    /// <exception cref="InvalidOperationException">ResourceManager 누락.</exception>
    public SimplePool(int capacity, string addressableKey, Transform parent = null, Action<T> onGet = null, Action<T> onRelease = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (string.IsNullOrWhiteSpace(addressableKey)) throw new ArgumentException("Prefab key is required.", nameof(addressableKey));
        resources = ResourceManager.Instance;
        if (resources == null) throw new InvalidOperationException("ResourceManager is required.");
        this.capacity = capacity;
        this.addressableKey = addressableKey;
        this.parent = parent;
        hadParent = parent != null;
        this.onGet = onGet;
        this.onRelease = onRelease;
    }

    /// <summary>객체 하나를 활성화하여 대여한다. 정원 소진 또는 Addressables 준비 부족이면 null.</summary>
    /// <returns>대여 객체 또는 null.</returns>
    /// <exception cref="Exception">종료·재진입·부모 소멸 또는 hook 실패.</exception>
    public T Get()
    {
        beginOperation();
        T item = null;
        try
        {
            while (available.Count > 0 && item == null)
            {
                var id = available.Dequeue();
                pooled.Remove(id);
                owned.TryGetValue(id, out item);
                if (item == null) owned.Remove(id);
            }
            if (item == null && owned.Count < capacity && addressableKey == null)
            {
                item = UnityEngine.Object.Instantiate(prefab, parent);
                // Prefab Awake에서도 풀이 종료될 수 있으므로 등록 전에 수명을 다시 확인한다.
                if (isClosed) { destroyItem(item); ensureOpen(); }
                track(item);
            }
            if (item == null) return null;
            item.gameObject.SetActive(true);
            ensureOpen();
            onGet?.Invoke(item);
            ensureOpen();
            if (item == null) throw new InvalidOperationException("Pool hook destroyed the rented object.");
            return item;
        }
        catch
        {
            discard(item);
            throw;
        }
        finally { isBusy = false; }
    }

    /// <summary>소유 객체만 반환한다. 중복 반환은 무시하고 비소유 객체는 보존하며 거부한다.</summary>
    /// <param name="obj">반환 객체. null은 무시한다.</param>
    /// <exception cref="Exception">비소유 객체, 종료·재진입 또는 hook 실패.</exception>
    public void Release(T obj)
    {
        if (obj == null) return;
        beginOperation();
        try
        {
            var id = obj.GetEntityId();
            if (!owned.ContainsKey(id)) throw new ArgumentException("Object belongs to another pool.", nameof(obj));
            if (pooled.Contains(id)) return;
            try
            {
                onRelease?.Invoke(obj);
                ensureOpen();
                obj.transform.SetParent(parent);
                obj.gameObject.SetActive(false);
                ensureOpen();
                available.Enqueue(id);
                pooled.Add(id);
            }
            catch { discard(obj); throw; }
        }
        finally { isBusy = false; }
    }

    /// <summary>대여 중 객체까지 해제하고 풀을 영구 종료한다. 진행 중 prewarm의 늦은 결과도 폐기한다.</summary>
    public void Clear()
    {
        if (isClosed) return;
        isClosed = true;
        var items = new List<T>(owned.Values);
        owned.Clear();
        available.Clear();
        pooled.Clear();
        totalCreated = 0;
        // Destroy의 OnDisable callback이 재진입해도 소유 목록은 이미 비어 있다.
        foreach (var item in items) destroyItem(item);
    }

    /// <summary>로컬 Prefab을 정원 내에서 추가 준비한다. 실패하면 부분 준비를 포함해 풀을 종료한다.</summary>
    /// <param name="count">추가 생성 요청 수. 0 이상.</param>
    /// <exception cref="Exception">입력·수명 오류 또는 생성 실패.</exception>
    public void Prewarm(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (addressableKey != null) throw new InvalidOperationException("Use PrewarmAsync for Addressables.");
        beginOperation();
        try
        {
            int toCreate = Math.Min(count, capacity - owned.Count);
            for (int i = 0; i < toCreate; i++) addPrepared(UnityEngine.Object.Instantiate(prefab, parent));
        }
        catch { Clear(); throw; }
        finally { isBusy = false; }
    }

    /// <summary>정원 내에서 추가 준비한다. 중복 준비는 거부하며 실패는 전체 정리 후 전달한다.</summary>
    /// <param name="count">추가 생성 요청 수. 0 이상.</param>
    /// <returns>요청한 수 또는 남은 정원까지 준비 완료.</returns>
    /// <exception cref="Exception">생성 실패, Component 누락, 종료 또는 재진입.</exception>
    public async Task PrewarmAsync(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (addressableKey == null) { Prewarm(count); return; }
        beginOperation();
        try
        {
            int toCreate = Math.Min(count, capacity - owned.Count);
            for (int i = 0; i < toCreate; i++)
            {
                ensureOpen();
                var go = await resources.InstantiateAsyncTask(addressableKey, parent);
                if (go == null) throw new InvalidOperationException($"Prewarm failed: {addressableKey}");
                var item = go.GetComponent<T>();
                if (isClosed || (hadParent && parent == null) || item == null)
                {
                    resources.ReleaseInstance(go);
                    throw new InvalidOperationException($"Pool closed, parent destroyed or {typeof(T).Name} missing: {addressableKey}");
                }
                addPrepared(item);
            }
        }
        catch { Clear(); throw; }
        finally { isBusy = false; }
    }

    /// <summary>생성 직후 소유권을 등록하여 hook 실패도 정리 가능하게 한다.</summary>
    /// <param name="item">생성 객체.</param>
    private void track(T item)
    {
        owned.Add(item.GetEntityId(), item);
        totalCreated++;
    }

    /// <summary>새 객체를 비활성 대기열에 넣는다.</summary>
    /// <param name="item">생성 객체.</param>
    /// <exception cref="Exception">비활성화 중 종료 또는 객체 소멸.</exception>
    private void addPrepared(T item)
    {
        if (isClosed) { destroyItem(item); ensureOpen(); }
        track(item);
        item.gameObject.SetActive(false);
        ensureOpen();
        var id = item.GetEntityId();
        available.Enqueue(id);
        pooled.Add(id);
    }

    /// <summary>실패한 대여·반환의 객체만 소유 목록에서 제거한다.</summary>
    /// <param name="item">실패 객체.</param>
    private void discard(T item)
    {
        if (item != null && owned.Remove(item.GetEntityId())) destroyItem(item);
    }

    /// <summary>생성 경로와 동일한 소유자를 통해 해제한다.</summary>
    /// <param name="item">해제할 객체.</param>
    private void destroyItem(T item)
    {
        if (item == null) return;
        if (addressableKey == null) UnityEngine.Object.Destroy(item.gameObject);
        else if (resources != null) resources.ReleaseInstance(item.gameObject);
        // manager가 먼저 파괴된 경우 그 manager의 ReleaseAll이 정리한다.
    }

    /// <summary>동기 hook 재진입과 비동기 prewarm 중 변경을 거부한다.</summary>
    /// <exception cref="InvalidOperationException">다른 변경 작업 진행 중.</exception>
    private void beginOperation()
    {
        ensureOpen();
        if (isBusy) throw new InvalidOperationException("Pool operation is already running.");
        isBusy = true;
    }

    /// <summary>풀과 지정 부모의 수명을 확인한다.</summary>
    /// <exception cref="ObjectDisposedException">풀 종료.</exception>
    /// <exception cref="InvalidOperationException">지정 부모 소멸.</exception>
    private void ensureOpen()
    {
        if (isClosed) throw new ObjectDisposedException(nameof(SimplePool<T>));
        if (hadParent && parent == null) throw new InvalidOperationException("Pool parent was destroyed.");
    }
}
