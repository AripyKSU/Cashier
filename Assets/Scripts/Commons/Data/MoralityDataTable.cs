using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>도덕성 CSV를 임시 파싱하고 성향별 가격 구간을 공개 전에 한 번 검증한다.</summary>
public sealed class MoralityDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, MoralityData> rows =
        new ReadOnlyDictionary<uint, MoralityData>(new Dictionary<uint, MoralityData>());

    /// <summary>공개 전 검증 중인 행.</summary>
    internal Dictionary<uint, MoralityData> PendingRows { get; private set; }
    /// <summary>검증 후 공개된 행.</summary>
    public IReadOnlyDictionary<uint, MoralityData> Rows => this.rows;
    /// <summary>공개 행 수.</summary>
    public int GetDataCount() => this.rows.Count;

    /// <summary>CSV 헤더·PK·행 자체를 임시 사전에 파싱한다.</summary>
    /// <param name="csvText">도덕성 CSV 원문.</param>
    /// <exception cref="InvalidDataException">필수 행 또는 구간 형식이 잘못된 경우.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<MoralityData>();
            var parsed = new Dictionary<uint, MoralityData>();
            while (csv.Read())
            {
                MoralityData item = csv.GetRecord<MoralityData>();
                if (Util.GetDataTableType(item.Idx) != DataTableType.Morality || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
                    throw new InvalidDataException($"column=idx, PK={item.Idx}: 대역 위반 또는 중복");
                CustomerProfileValidation.ValidateType(item.CustomerDispositionType);
                if (item.OfferMaxRate == 0 ? item.IncludeMax : item.OfferMaxRate < item.OfferMinRate)
                    throw new InvalidDataException($"PK={item.Idx}: 상한 0은 include_max=false인 무상한이며 그 외에는 하한 이상이어야 합니다.");
                if (item.OfferMaxRate == item.OfferMinRate && (!item.IncludeMin || !item.IncludeMax))
                    throw new InvalidDataException($"PK={item.Idx}: 동일한 하한·상한은 양끝을 포함해야 합니다.");
                parsed.Add(item.Idx, item);
            }
            if (parsed.Count == 0) throw new InvalidDataException("데이터 행 누락");
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"MoralityData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>성향의 실제 수락 범위에서 모든 가능한 비율이 정확히 한 행에 대응하는지 검사한다.</summary>
    /// <param name="dispositions">공개 전 손님 성향 행.</param>
    public void Validate(IReadOnlyDictionary<uint, CustomerDispositionData> dispositions)
    {
        if (PendingRows == null || dispositions == null) throw new InvalidDataException("도덕성·성향 CSV가 필요합니다.");
        var represented = new HashSet<CustomerDispositionType>();
        foreach (MoralityData row in PendingRows.Values) represented.Add(row.CustomerDispositionType);
        foreach (CustomerDispositionType type in represented)
        {
            bool hasDisposition = false;
            foreach (CustomerDispositionData disposition in dispositions.Values)
                if (disposition.DispositionType == type) { hasDisposition = true; break; }
            if (!hasDisposition)
                throw new InvalidDataException($"MoralityData: 성향 {type} FK 실패");
        }
        foreach (CustomerDispositionData disposition in dispositions.Values)
        {
            CustomerDispositionType type = disposition.DispositionType;
            if (!represented.Contains(type)) continue;
            int tolerance = type == CustomerDispositionType.PriceSensitive ? 1000 : disposition.PriceTolerance;
            var points = new SortedSet<decimal> { 0.5m, 1000m, tolerance, tolerance + 1m };
            foreach (MoralityData row in PendingRows.Values)
                if (row.CustomerDispositionType == type)
                {
                    points.Add(row.OfferMinRate);
                    if (row.OfferMaxRate > 0) points.Add(row.OfferMaxRate);
                }
            var boundaries = new List<decimal>(points);
            points.Add(boundaries[boundaries.Count - 1] + 1m);
            for (int i = 1; i < boundaries.Count; i++) points.Add((boundaries[i - 1] + boundaries[i]) / 2m);
            foreach (decimal rate in points)
            {
                if (rate <= 0) continue;
                bool accepted = type == CustomerDispositionType.PriceSensitive ? rate == 1000m : rate <= tolerance;
                int matches = 0;
                foreach (MoralityData row in PendingRows.Values)
                    if (row.CustomerDispositionType == type && row.IsAccepted == accepted && contains(row, rate)) matches++;
                if (matches != 1) throw new InvalidDataException($"MoralityData: {type}, accepted={accepted}, rate={rate} 구간은 정확히 하나여야 합니다.");
            }
        }
    }

    /// <summary>전체 검증 성공 후 공개한다.</summary>
    internal void Commit()
    {
        rows = new ReadOnlyDictionary<uint, MoralityData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>공개·대기 데이터를 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        rows = new ReadOnlyDictionary<uint, MoralityData>(new Dictionary<uint, MoralityData>());
    }

    /// <summary>로드 검증에 사용할 비율의 구간 포함 여부를 확인한다.</summary>
    /// <param name="row">검증할 가격 구간.</param>
    /// <param name="rate">1000을 100%로 표현한 비율.</param>
    /// <returns>비율이 해당 구간에 포함되면 true.</returns>
    private static bool contains(MoralityData row, decimal rate)
    {
        if (rate < row.OfferMinRate || (rate == row.OfferMinRate && !row.IncludeMin)) return false;
        return row.OfferMaxRate == 0 || rate < row.OfferMaxRate || (rate == row.OfferMaxRate && row.IncludeMax);
    }
}
