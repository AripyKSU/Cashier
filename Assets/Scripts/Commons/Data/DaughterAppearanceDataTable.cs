using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CsvHelper;
using UnityEngine;

/// <summary>딸 이미지 날짜 CSV를 전체 검증 후 공개한다.</summary>
public sealed class DaughterAppearanceDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, DaughterAppearanceData> rows =
        new ReadOnlyDictionary<uint, DaughterAppearanceData>(new Dictionary<uint, DaughterAppearanceData>());
    /// <summary>전체 FK 검증 전에 보관하는 행.</summary>
    internal Dictionary<uint, DaughterAppearanceData> PendingRows { get; private set; }
    /// <summary>검증 후 공개한 날짜 구간.</summary>
    public IReadOnlyDictionary<uint, DaughterAppearanceData> Rows => rows;
    /// <summary>공개 행 수를 반환한다.</summary>
    /// <returns>검증 완료 행 수.</returns>
    public int GetDataCount() => rows.Count;

    /// <summary>CSV를 임시 파싱하고 날짜·PK를 검사한다.</summary>
    /// <param name="csvText">CSV 원문.</param>
    /// <exception cref="Exception">헤더·행·날짜 오류.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<DaughterAppearanceData>();
            var parsed = new Dictionary<uint, DaughterAppearanceData>();
            var days = new HashSet<uint>();
            while (csv.Read())
            {
                DaughterAppearanceData item = csv.GetRecord<DaughterAppearanceData>();
                item.Validate();
                if (!parsed.TryAdd(item.Idx, item)) throw new InvalidDataException($"PK={item.Idx}: 중복");
                if (!days.Add(item.StartDay)) throw new InvalidDataException($"start_day={item.StartDay}: 중복");
            }
            if (parsed.Count == 0 || parsed.Values.Min(row => row.StartDay) != 1)
                throw new InvalidDataException("첫 이미지 구간은 1일차부터 시작해야 합니다.");
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"DaughterAppearanceData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>모든 이미지의 ResourceData FK를 검사한다.</summary>
    /// <param name="resources">검증 완료 ResourceData 테이블.</param>
    /// <exception cref="InvalidDataException">필수 테이블 또는 FK 누락.</exception>
    public void Validate(ResourceDataTable resources)
    {
        if (PendingRows == null || resources == null) throw new InvalidDataException("DaughterAppearance/Resource CSV 필요");
        foreach (DaughterAppearanceData row in PendingRows.Values)
            if (!resources.TryGetResource(row.ResourceIdx, out _))
                throw new InvalidDataException($"DaughterAppearance PK={row.Idx}: Resource FK={row.ResourceIdx} 실패");
    }

    /// <summary>전체 테이블 검증 완료 후 대기 행을 공개한다.</summary>
    internal void Commit()
    {
        rows = new ReadOnlyDictionary<uint, DaughterAppearanceData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>manager 종료 시 대기·공개 행을 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        rows = new ReadOnlyDictionary<uint, DaughterAppearanceData>(new Dictionary<uint, DaughterAppearanceData>());
    }
}
