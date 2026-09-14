using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CsvHelper;
using UnityEngine;

/// <summary>가게 단계 CSV와 설비·리소스 참조를 검증한 뒤 공개한다.</summary>
public sealed class StoreStageDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, StoreStageData> rows = new ReadOnlyDictionary<uint, StoreStageData>(new Dictionary<uint, StoreStageData>());
    internal Dictionary<uint, StoreStageData> PendingRows { get; private set; }
    public IReadOnlyDictionary<uint, StoreStageData> Rows => rows;
    public int GetDataCount() => rows.Count;

    /// <summary>행 PK와 1/2/3 단계의 중복·누락을 검사한다.</summary>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader(); csv.ValidateHeader<StoreStageData>();
            var parsed = new Dictionary<uint, StoreStageData>();
            var stages = new HashSet<uint>();
            while (csv.Read())
            {
                var row = csv.GetRecord<StoreStageData>(); row.Validate();
                if (!parsed.TryAdd(row.Idx, row) || !stages.Add(row.StoreStage))
                    throw new InvalidDataException($"PK={row.Idx}: idx/store_stage 중복");
            }
            if (!stages.SetEquals(new uint[] { 1, 2, 3 })) throw new InvalidDataException("store_stage 1/2/3이 모두 필요합니다.");
            PendingRows = parsed;
        }
        catch (Exception error)
        {
            Debug.LogError($"StoreStageData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {error}");
            throw;
        }
    }

    /// <summary>공개 전에 리소스 FK와 설비에서 참조하는 모든 단계를 확인한다.</summary>
    public void Validate(ResourceDataTable resources, IReadOnlyDictionary<uint, FacilityData> facilities)
    {
        if (PendingRows == null || resources == null || facilities == null) throw new InvalidDataException("StoreStage/Resource/Facility CSV 필요");
        var stages = new HashSet<uint>(PendingRows.Values.Select(row => row.StoreStage));
        foreach (var facility in facilities.Values)
            if (!stages.Contains(facility.RequiredStoreStage) || (facility.TargetStoreStage > 0 && !stages.Contains(facility.TargetStoreStage)))
                throw new InvalidDataException($"Facility PK={facility.Idx}: StoreStage 누락");
        foreach (var row in PendingRows.Values)
            foreach (uint resource in new[] { row.WorldPrefabResourceIdx, row.FrontPrefabResourceIdx, row.TopViewPrefabResourceIdx })
                if (!resources.TryGetResource(resource, out _)) throw new InvalidDataException($"StoreStage PK={row.Idx}: Resource FK={resource} 누락");
    }

    /// <summary>현재 진행 단계로 정확히 한 외형 행을 조회한다.</summary>
    public StoreStageData GetStage(uint stage) => rows.Values.Single(row => row.StoreStage == stage);
    internal void Commit() { rows = new ReadOnlyDictionary<uint, StoreStageData>(PendingRows); PendingRows = null; }
    public void Release() { PendingRows = null; rows = new ReadOnlyDictionary<uint, StoreStageData>(new Dictionary<uint, StoreStageData>()); }
}
