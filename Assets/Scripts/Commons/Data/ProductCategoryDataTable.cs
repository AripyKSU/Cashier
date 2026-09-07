using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>상품 분류 CSV의 파싱·PK·행 검증과 조회를 담당한다. 연관 FK 검증 후에만 공개한다.</summary>
public sealed class ProductCategoryDataTable : IDataLoad
{
    // 실패 시 이전 공개 데이터를 유지하며 catalog가 검증한 후 교체한다.
    private IReadOnlyDictionary<uint, ProductCategoryData> dataDict = new ReadOnlyDictionary<uint, ProductCategoryData>(new Dictionary<uint, ProductCategoryData>());
    /// <summary>FK 검증 대기 데이터. 공개 조회에는 사용하지 않는다.</summary>
    internal Dictionary<uint, ProductCategoryData> PendingRows { get; private set; }
    /// <summary>FK까지 검증된 공개 행. 소비자는 DTO 값을 변경하지 않는다.</summary>
    public IReadOnlyDictionary<uint, ProductCategoryData> Rows => dataDict;

    /// <summary>공개 데이터 개수를 반환한다.</summary>
    /// <returns>검증된 행 수.</returns>
    public int GetDataCount() => dataDict.Count;

    /// <summary>PK로 검증된 데이터를 조회한다.</summary>
    /// <param name="idx">조회할 PK.</param>
    /// <param name="data">조회 결과. 없으면 null.</param>
    /// <returns>존재 여부.</returns>
    public bool TryGetData(uint idx, out ProductCategoryData data) => dataDict.TryGetValue(idx, out data);

    /// <summary>CSV 전체를 별도 사전에 검증한다. 공개는 CustomerCatalog의 FK 검사 후 수행한다.</summary>
    /// <param name="csvText">CSV 원문.</param>
    /// <exception cref="Exception">header·형식·PK·행 값 오류.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<ProductCategoryData>();
            var parsed = new Dictionary<uint, ProductCategoryData>();
            while (csv.Read())
            {
                var item = csv.GetRecord<ProductCategoryData>();
                if (Util.GetDataTableType(item.Idx) != DataTableType.ProductCategory || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
                    throw new InvalidDataException($"column=idx, PK={item.Idx}: 대역 위반 또는 중복");
                if (item.ProductType == ProductType.None || !Enum.IsDefined(typeof(ProductType), item.ProductType))
                    throw new InvalidDataException("column=product_type: 정의된 분류 필요");
                parsed.Add(item.Idx, item);
            }
            if (parsed.Count == 0) throw new InvalidDataException("데이터 행 누락");
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"ProductCategoryData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>catalog의 전체 FK 검증 성공 후 공개 사전을 교체한다.</summary>
    internal void Commit()
    {
        dataDict = new ReadOnlyDictionary<uint, ProductCategoryData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>manager 종료 시 공개·대기 데이터를 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        dataDict = new ReadOnlyDictionary<uint, ProductCategoryData>(new Dictionary<uint, ProductCategoryData>());
    }
}
