using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>감독관 CSV를 임시 파싱하고 모든 참조 검증 후 공개한다.</summary>
public sealed class InspectorEventDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, InspectorEventData> rows =
        new ReadOnlyDictionary<uint, InspectorEventData>(new Dictionary<uint, InspectorEventData>());
    /// <summary>공개 전 검증할 행.</summary>
    internal Dictionary<uint, InspectorEventData> PendingRows { get; private set; }
    /// <summary>전체 검증 완료 후 공개된 행.</summary>
    public IReadOnlyDictionary<uint, InspectorEventData> Rows => rows;
    /// <summary>공개 행 수.</summary>
    /// <returns>검증 완료 행 수.</returns>
    public int GetDataCount() => rows.Count;

    /// <summary>CSV 헤더와 행을 검증하되 기존 공개 상태는 변경하지 않는다.</summary>
    /// <param name="csvText">CSV 원문.</param>
    /// <exception cref="Exception">헤더·PK·타입·범위 오류.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<InspectorEventData>();
            var parsed = new Dictionary<uint, InspectorEventData>();
            while (csv.Read())
            {
                InspectorEventData item = csv.GetRecord<InspectorEventData>();
                item.Validate();
                if (!parsed.TryAdd(item.Idx, item)) throw new InvalidDataException($"PK={item.Idx}: 중복");
            }
            if (parsed.Count == 0) throw new InvalidDataException("데이터 행 누락");
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"InspectorEventData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>공개 전에 이름·대사·초상·설비 FK를 검사한다.</summary>
    /// <param name="texts">공개 전 Text 행.</param>
    /// <param name="resources">리소스 테이블.</param>
    /// <param name="facilities">공개 전 설비 행.</param>
    /// <exception cref="InvalidDataException">데이터 누락 또는 FK 실패.</exception>
    public void Validate(IReadOnlyDictionary<uint, TextData> texts, ResourceDataTable resources,
        IReadOnlyDictionary<uint, FacilityData> facilities)
    {
        if (PendingRows == null || texts == null || resources == null || facilities == null)
            throw new InvalidDataException("InspectorEvent/Text/Resource/Facility CSV 필요");
        foreach (InspectorEventData row in PendingRows.Values)
        {
            if (!texts.ContainsKey(row.NameIdx)) throw new InvalidDataException($"InspectorEvent PK={row.Idx}: nameidx Text FK={row.NameIdx} 실패");
            foreach (uint idx in row.DialogueTextIdxs)
                if (!texts.ContainsKey(idx)) throw new InvalidDataException($"InspectorEvent PK={row.Idx}: dialogue_text_idxs Text FK={idx} 실패");
            if (!resources.TryGetResource(row.PortraitResourceIdx, out _))
                throw new InvalidDataException($"InspectorEvent PK={row.Idx}: portrait_resource_idx Resource FK={row.PortraitResourceIdx} 실패");
            if (row.RequiredFacilityIdx.HasValue && !facilities.ContainsKey(row.RequiredFacilityIdx.Value))
                throw new InvalidDataException($"InspectorEvent PK={row.Idx}: required_facility_idx Facility FK={row.RequiredFacilityIdx} 실패");
        }
    }

    /// <summary>모든 테이블 검증이 끝난 후에만 공개한다.</summary>
    internal void Commit()
    {
        rows = new ReadOnlyDictionary<uint, InspectorEventData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>세션 로더 종료 시 대기·공개 행을 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        rows = new ReadOnlyDictionary<uint, InspectorEventData>(new Dictionary<uint, InspectorEventData>());
    }
}
