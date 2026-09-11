using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CsvHelper;
using UnityEngine;

/// <summary>딸 대사 CSV를 전체 검증 후 공개한다.</summary>
public sealed class DaughterDialogueDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, DaughterDialogueData> rows =
        new ReadOnlyDictionary<uint, DaughterDialogueData>(new Dictionary<uint, DaughterDialogueData>());
    /// <summary>전체 FK 검증 전에 보관하는 행.</summary>
    internal Dictionary<uint, DaughterDialogueData> PendingRows { get; private set; }
    /// <summary>검증 후 공개한 대사 구간.</summary>
    public IReadOnlyDictionary<uint, DaughterDialogueData> Rows => rows;
    /// <summary>공개 행 수를 반환한다.</summary>
    /// <returns>검증 완료 행 수.</returns>
    public int GetDataCount() => rows.Count;

    /// <summary>CSV를 임시 파싱하고 구간 전체를 검사한다.</summary>
    /// <param name="csvText">CSV 원문.</param>
    /// <exception cref="Exception">헤더·행·구간 오류.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<DaughterDialogueData>();
            var parsed = new Dictionary<uint, DaughterDialogueData>();
            while (csv.Read())
            {
                DaughterDialogueData item = csv.GetRecord<DaughterDialogueData>();
                item.Validate();
                if (!parsed.TryAdd(item.Idx, item)) throw new InvalidDataException($"PK={item.Idx}: 중복");
            }
            validateRanges(parsed.Values);
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"DaughterDialogueData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>모든 후보의 TextData FK를 검사한다.</summary>
    /// <param name="texts">검증 대기 중인 TextData 행.</param>
    /// <exception cref="InvalidDataException">필수 테이블 또는 FK 누락.</exception>
    public void Validate(IReadOnlyDictionary<uint, TextData> texts)
    {
        if (PendingRows == null || texts == null) throw new InvalidDataException("DaughterDialogue/Text CSV 필요");
        foreach (DaughterDialogueData row in PendingRows.Values)
            foreach (uint textIdx in row.TextIdxs)
                if (!texts.ContainsKey(textIdx))
                    throw new InvalidDataException($"DaughterDialogue PK={row.Idx}: text_idxs Text FK={textIdx} 실패");
    }

    /// <summary>전체 테이블 검증 완료 후 대기 행을 공개한다.</summary>
    internal void Commit()
    {
        rows = new ReadOnlyDictionary<uint, DaughterDialogueData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>manager 종료 시 대기·공개 행을 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        rows = new ReadOnlyDictionary<uint, DaughterDialogueData>(new Dictionary<uint, DaughterDialogueData>());
    }

    private static void validateRanges(IEnumerable<DaughterDialogueData> source)
    {
        DaughterDialogueData[] sorted = source.OrderBy(row => row.MoralityMin ?? decimal.MinValue).ToArray();
        if (sorted.Length == 0) throw new InvalidDataException("데이터 행 누락");
        if (sorted[0].MoralityMin.HasValue || sorted[sorted.Length - 1].MoralityMax.HasValue)
            throw new InvalidDataException("양 끝 구간은 무한 경계여야 합니다.");
        for (int i = 1; i < sorted.Length; i++)
        {
            if (!sorted[i - 1].MoralityMax.HasValue || !sorted[i].MoralityMin.HasValue ||
                sorted[i - 1].MoralityMax.Value != sorted[i].MoralityMin.Value)
                throw new InvalidDataException("도덕성 구간에 누락 또는 겹침이 있습니다.");
        }
        if (!sorted.Any(row => row.MoralityMin == 0m))
            throw new InvalidDataException("0점은 긍정 구간의 포함 하한이어야 합니다.");
    }
}
