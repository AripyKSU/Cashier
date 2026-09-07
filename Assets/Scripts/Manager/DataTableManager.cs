using Cysharp.Threading.Tasks;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 데이터 테이블 매니저 (Singleton)
/// CSV 데이터 테이블 전반의 비동기 로드, 파싱 및 캐싱을 총괄 관리합니다.
/// 데이터 식별 및 검증은 오직 idx 기반(Util.GetDataTableType)으로만 수행합니다.
/// </summary>
public class DataTableManager : Singleton<DataTableManager>
{
    // =========================================================================
    // 1. PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    private readonly Dictionary<DataTableType, IDataLoad> dataList = new Dictionary<DataTableType, IDataLoad>();
    private readonly UniTaskCompletionSource loadCompletionSource = new UniTaskCompletionSource();
    private bool isLoaded = false;

    /// <summary>EnsureDataLoadedAsync 성공 후 접근하는 검증된 손님 데이터.</summary>
    public CustomerCatalog Customers { get; private set; }


    // =========================================================================
    // 2. PUBLIC METHODS (PascalCase)
    // =========================================================================

    /// <summary>
    /// 데이터 테이블이 모두 로드 및 캐싱될 때까지 비동기로 안전하게 대기합니다.
    /// </summary>
    /// <returns>전체 CSV·손님 FK 검증 완료. 실패는 호출자에게 전달한다.</returns>
    public async UniTask EnsureDataLoadedAsync()
    {
        if (this.isLoaded) return;
        await this.loadCompletionSource.Task;
    }

    public T GetDB<T>(uint idx) where T : class, IDataLoad
    {
        DataTableType dtt = Util.GetDataTableType(idx);
        if (idx <= 0)
        {
            dtt = this.dataList.FirstOrDefault(kv => kv.Value is T).Key;
        }
        return this.GetDB<T>(dtt);
    }

    public T GetDB<T>(DataTableType dataTableType) where T : class, IDataLoad
    {
        return this.dataList.TryGetValue(dataTableType, out var value) ? value as T : null;
    }

    public int GetDataCount<T>(DataTableType dataTableType) where T : class, IDataLoad
    {
        var db = this.GetDB<T>(dataTableType);
        return db?.GetDataCount() ?? 0;
    }


    // =========================================================================
    // 3. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================

    /// <summary>리소스·Text와 손님 CSV 네 테이블을 등록하고 로딩을 시작한다.</summary>
    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();

        // [우선순위 순서 정렬 등록]
        this.dataList[DataTableType.EconomyBalance] = new EconomyBalanceDataTable();
        this.dataList[DataTableType.MaintenanceBalance] = new MaintenanceBalanceDataTable();
        this.dataList[DataTableType.Resource] = new ResourceDataTable();
        this.dataList[DataTableType.CustomerAppearance] = new CustomerAppearanceDataTable();
        this.dataList[DataTableType.CustomerDisposition] = new CustomerDispositionDataTable();
        this.dataList[DataTableType.ProductCategory] = new ProductCategoryDataTable();
        this.dataList[DataTableType.Product] = new ProductDataTable();
        this.dataList[DataTableType.Text] = new TextDataTable();

        Customers = new CustomerCatalog(
            GetDB<CustomerAppearanceDataTable>(DataTableType.CustomerAppearance),
            GetDB<CustomerDispositionDataTable>(DataTableType.CustomerDisposition),
            GetDB<ProductCategoryDataTable>(DataTableType.ProductCategory),
            GetDB<ProductDataTable>(DataTableType.Product));

        this.preloadDataTablesAsync().Forget();
    }

    /// <summary>로딩 대기자를 취소하고 manager 소유 테이블을 해제한다.</summary>
    protected override void OnSingletonDestroyed()
    {
        loadCompletionSource.TrySetCanceled();
        foreach (var pair in this.dataList)
        {
            pair.Value?.Release();
        }
        this.dataList.Clear();
        base.OnSingletonDestroyed();
    }

    /// <summary>리소스 실패·파싱 실패를 완료로 바꾸지 않고 모든 손님 FK 확인 후 대기자를 해제한다.</summary>
    /// <returns>로드 성공 또는 관찰된 실패 처리 완료.</returns>
    private async UniTask preloadDataTablesAsync()
    {
        const string targetLabel = "Datas";
        var locationsHandle = Addressables.LoadResourceLocationsAsync(targetLabel, typeof(TextAsset));
        try
        {
            await locationsHandle.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
            if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidDataException("Datas 리소스 위치 로드 실패", locationsHandle.OperationException);
            if (locationsHandle.Result.Count > 0)
            {
                foreach (var location in locationsHandle.Result)
                {
                    var asset = await ResourceManager.Instance.LoadAssetAsync<TextAsset>(location.PrimaryKey)
                        .AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
                    if (asset == null) throw new InvalidDataException($"CSV 리소스 누락: {location.PrimaryKey}");
                    this.parseAndCacheCsv(asset.name, asset.text);
                }
            }
            else
            {
                Debug.LogWarning("[DataTableManager] Datas 라벨이 비어 있습니다. 기존 Resources fallback을 검사합니다.");
                this.fallbackLoadFromResources();
            }
            Customers.ValidateAndCommit(GetDB<TextDataTable>(DataTableType.Text), GetDB<ResourceDataTable>(DataTableType.Resource));
            this.isLoaded = true;
            this.loadCompletionSource.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            loadCompletionSource.TrySetCanceled();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DataTableManager] CSV 로드 실패: {exception.Message}");
            loadCompletionSource.TrySetException(exception);
        }
        finally
        {
            if (locationsHandle.IsValid()) Addressables.Release(locationsHandle);
        }
    }

    /// <summary>CSV 첫 PK로 등록된 로더를 선택한다. 파싱 오류는 상위 완료 경계로 전달한다.</summary>
    /// <param name="assetName">오류 문맥용 에셋 이름.</param>
    /// <param name="csvText">CSV 원문.</param>
    /// <exception cref="InvalidDataException">빈 데이터 또는 미등록 테이블.</exception>
    private void parseAndCacheCsv(string assetName, string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            throw new InvalidDataException($"{assetName}: CSV 내용이 비어있습니다.");
        }

        // 오직 idx 기반으로만 DataTableType 식별 (파일명 검사 없음)
        uint firstIdx = this.extractFirstRowIdx(csvText);
        DataTableType dtt = Util.GetDataTableType(firstIdx);

        if (dtt == DataTableType.None)
        {
            throw new InvalidDataException($"{assetName}: 첫 idx={firstIdx}의 테이블을 찾을 수 없습니다.");
        }

        if (!this.dataList.TryGetValue(dtt, out var loader))
        {
            throw new InvalidDataException($"{assetName}: DataTableType.{dtt} 로더가 등록되지 않았습니다.");
        }

        loader.LoadData(csvText);
        Debug.Log($"<color=green>[DataTableManager] 데이터 캐싱 성공: {assetName} (Type: {dtt}, Count: {loader.GetDataCount()})</color>");
    }

    /// <summary>따옴표·구분자 처리도 실제 CSV parser와 동일하게 첫 PK를 읽는다.</summary>
    /// <param name="csvText">CSV 원문.</param>
    /// <returns>첫 행의 idx.</returns>
    /// <exception cref="InvalidDataException">header 또는 첫 데이터 행 누락.</exception>
    private uint extractFirstRowIdx(string csvText)
    {
        using (var reader = new StringReader(csvText))
        using (var csv = new CsvReader(reader, Util.GetCsvConfiguration()))
        {
            if (!csv.Read()) throw new InvalidDataException("CSV header 누락");
            csv.ReadHeader();
            if (!csv.Read()) throw new InvalidDataException("CSV 데이터 행 누락");
            return csv.GetField<uint>("idx");
        }
    }

    /// <summary>Addressables 후보가 없을 때만 기존 Resources·Editor 경로를 검사한다.</summary>
    private void fallbackLoadFromResources()
    {
        TextAsset[] csvAssets = Resources.LoadAll<TextAsset>("datas");
        if (csvAssets != null && csvAssets.Length > 0)
        {
            foreach (var asset in csvAssets)
            {
                this.parseAndCacheCsv(asset.name, asset.text);
            }
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Datas" });
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            TextAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null)
            {
                this.parseAndCacheCsv(asset.name, asset.text);
            }
        }
#endif
    }
}
