using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>설비 CSV의 파싱·PK·행 검증과 조회를 담당한다. 연관 FK 검증 후에만 공개한다.</summary>
public sealed class FacilityDataTable : IDataLoad
{
    // 실패 시 이전 공개 데이터를 유지하며 catalog가 검증한 후 교체한다.
    private IReadOnlyDictionary<uint, FacilityData> dataDict = new ReadOnlyDictionary<uint, FacilityData>(new Dictionary<uint, FacilityData>());
    /// <summary>FK 검증 대기 데이터. 공개 조회에는 사용하지 않는다.</summary>
    internal Dictionary<uint, FacilityData> PendingRows { get; private set; }
    /// <summary>FK까지 검증된 공개 행. 소비자는 DTO 값을 변경하지 않는다.</summary>
    public IReadOnlyDictionary<uint, FacilityData> Rows => dataDict;

    /// <summary>공개 데이터 개수를 반환한다.</summary>
    /// <returns>검증된 행 수.</returns>
    public int GetDataCount() => dataDict.Count;

    /// <summary>PK로 검증된 데이터를 조회한다.</summary>
    /// <param name="idx">조회할 PK.</param>
    /// <param name="data">조회 결과. 없으면 null.</param>
    /// <returns>존재 여부.</returns>
    public bool TryGetData(uint idx, out FacilityData data) => dataDict.TryGetValue(idx, out data);

    /// <summary>CSV 전체를 별도 사전에 검증한다. 공개는 DataTableManager의 FK 검사 후 수행한다.</summary>
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
            csv.ValidateHeader<FacilityData>();
            var parsed = new Dictionary<uint, FacilityData>();
            while (csv.Read())
            {
                var item = csv.GetRecord<FacilityData>();
                if (Util.GetDataTableType(item.Idx) != DataTableType.Facility || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
                    throw new InvalidDataException($"column=idx, PK={item.Idx}: 대역 위반 또는 중복");
                item.Validate();
                parsed.Add(item.Idx, item);
            }
            if (parsed.Count == 0) throw new InvalidDataException("데이터 행 누락");
            validateUniqueUpgradeTargets(parsed);
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"FacilityData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>catalog의 전체 FK 검증 성공 후 공개 사전을 교체한다.</summary>
    internal void Commit()
    {
        dataDict = new ReadOnlyDictionary<uint, FacilityData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>manager 종료 시 공개·대기 데이터를 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        dataDict = new ReadOnlyDictionary<uint, FacilityData>(new Dictionary<uint, FacilityData>());
    }

    /// <summary>편의성 효과와 단계 상승 목표가 각각 하나씩만 존재하고 최종 효과·단계를 모두 제공하는지 확인한다.</summary>
    /// <param name="rows">행 단위 검증이 끝난 설비 행.</param>
    /// <exception cref="InvalidDataException">중복되거나 필요한 효과·목표가 누락됨.</exception>
    private void validateUniqueUpgradeTargets(IReadOnlyDictionary<uint, FacilityData> rows)
    {
        var convenienceEffects = new HashSet<ConvenienceEffectType>();
        var stageTargets = new HashSet<uint>();
        foreach (var row in rows.Values)
        {
            if (row.UpgradeKind == FacilityUpgradeKind.Convenience && !convenienceEffects.Add(row.EffectType))
                throw new InvalidDataException($"FacilityData.csv: effect_type={row.EffectType} 설비가 중복됩니다.");
            if (row.UpgradeKind == FacilityUpgradeKind.StoreStage && !stageTargets.Add(row.TargetStoreStage))
                throw new InvalidDataException($"FacilityData.csv: target_store_stage={row.TargetStoreStage} 단계 상승이 중복됩니다.");
        }

        foreach (ConvenienceEffectType effect in new[]
        {
            ConvenienceEffectType.DividerBar,
            ConvenienceEffectType.AutoSorting,
            ConvenienceEffectType.Vacuum
        })
            if (!convenienceEffects.Contains(effect))
                throw new InvalidDataException($"FacilityData.csv: effect_type={effect} 설비가 필요합니다.");
        foreach (uint target in new[] { 2u, 3u })
            if (!stageTargets.Contains(target))
                throw new InvalidDataException($"FacilityData.csv: target_store_stage={target} 단계 상승이 필요합니다.");
    }
}
