using System.Collections.Generic;
using CsvHelper;
using System.IO;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Addressable 키 리소스 참조 데이터 테이블 (Type 3: 3001~3999).
/// </summary>
public class ResourceDataTable : IDataLoad
{
    // 전체 파싱 성공 후 교체하는 리소스 ID의 단일 조회 사전.
    private Dictionary<uint, ResourceData> dataDict = new Dictionary<uint, ResourceData>();

    /// <summary>공개된 리소스 행 수.</summary>
    /// <returns>행 수.</returns>
    public int GetDataCount() => this.dataDict.Count;

    /// <summary>현재 대역·중복·필수 경로를 검증하고 원래 path 문자열은 그대로 유지한다.</summary>
    /// <param name="csvText">ResourceData CSV 원문.</param>
    /// <exception cref="System.Exception">CSV 형식, PK 대역 또는 path 검증 실패.</exception>
    public void LoadData(string csvText)
    {
        using (var reader = new StringReader(csvText))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            try
            {
                var parsed = new Dictionary<uint, ResourceData>();
                foreach (var item in csv.GetRecords<ResourceData>())
                {
                    if (Util.GetDataTableType(item.Idx) != DataTableType.Resource || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
                        throw new InvalidDataException($"column=idx, PK={item.Idx}: Resource 대역 위반 또는 중복");
                    if (string.IsNullOrWhiteSpace(item.Path))
                        throw new InvalidDataException($"column=path, PK={item.Idx}: 필수값 누락");
                    parsed.Add(item.Idx, item);
                }
                if (parsed.Count == 0) throw new InvalidDataException("ResourceData 데이터 행 누락");
                this.dataDict = parsed;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"ResourceData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
                throw;
            }
        }
        Debug.Log($"[ResourceDataTable] 총 {this.dataDict.Count}개의 리소스 경로 데이터 로드 완료.");
    }

    /// <summary>리소스 PK로 경로 데이터를 조회한다.</summary>
    /// <param name="idx">현재 Resource PK.</param>
    /// <param name="data">조회 결과. 실패 시 null.</param>
    /// <returns>존재 여부.</returns>
    public bool TryGetResource(uint idx, out ResourceData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    /// <summary>기존 호출 계약대로 리소스 path를 반환한다.</summary>
    /// <param name="idx">현재 Resource PK.</param>
    /// <returns>path. 참조가 없으면 빈 문자열.</returns>
    public string GetResourcePath(uint idx)
    {
        return this.dataDict.TryGetValue(idx, out var data) ? data.Path : string.Empty;
    }

    /// <summary>manager 수명 종료 시 캐시를 해제한다.</summary>
    public void Release() => this.dataDict.Clear();
}
