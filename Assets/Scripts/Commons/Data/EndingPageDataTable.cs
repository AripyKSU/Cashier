using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CsvHelper;
using UnityEngine;

/// <summary>굿·배드 엔딩 페이지 CSV를 전체 검증한 뒤 공개한다.</summary>
public sealed class EndingPageDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, EndingPageData> rows =
        new ReadOnlyDictionary<uint, EndingPageData>(new Dictionary<uint, EndingPageData>());
    /// <summary>전체 FK 검증 전의 파싱 결과.</summary>
    internal Dictionary<uint, EndingPageData> PendingRows { get; private set; }
    /// <summary>검증 후 공개한 엔딩 페이지.</summary>
    public IReadOnlyDictionary<uint, EndingPageData> Rows => rows;
    /// <summary>검증된 행 수를 반환한다.</summary>
    /// <returns>공개 행 수.</returns>
    public int GetDataCount() => rows.Count;

    /// <summary>CSV를 임시 파싱하고 두 엔딩의 연속 페이지 순서를 검사한다.</summary>
    /// <param name="csvText">CSV 원문.</param>
    /// <exception cref="Exception">헤더·PK·종류·순서 오류.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<EndingPageData>();
            var parsed = new Dictionary<uint, EndingPageData>();
            while (csv.Read())
            {
                var item = csv.GetRecord<EndingPageData>();
                item.Validate();
                if (!parsed.TryAdd(item.Idx, item)) throw new InvalidDataException($"PK={item.Idx}: 중복");
            }
            foreach (EndingKind kind in new[] { EndingKind.Good, EndingKind.Bad })
            {
                var pages = parsed.Values.Where(p => p.Kind == kind).OrderBy(p => p.PageOrder).ToArray();
                if (pages.Length == 0) throw new InvalidDataException($"{kind}: 페이지 누락");
                for (int i = 0; i < pages.Length; i++)
                    if (pages[i].PageOrder != i + 1)
                        throw new InvalidDataException($"{kind}: page_order는 1부터 중복 없이 연속해야 합니다.");
            }
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"EndingPageData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>모든 대사·화자·이미지의 FK와 표시 문자열을 검사한다.</summary>
    /// <param name="texts">검증 대기 TextData.</param>
    /// <param name="resources">ResourceData.</param>
    /// <exception cref="InvalidDataException">필수 테이블·FK·문구 누락.</exception>
    public void Validate(IReadOnlyDictionary<uint, TextData> texts, ResourceDataTable resources)
    {
        if (PendingRows == null || texts == null || resources == null)
            throw new InvalidDataException("EndingPage/Text/Resource CSV 필요");
        foreach (var page in PendingRows.Values)
        {
            foreach (uint textIdx in new[] { page.TextIdx, page.SpeakerNameIdx })
                if (!texts.TryGetValue(textIdx, out var text) || string.IsNullOrWhiteSpace(text.Text))
                    throw new InvalidDataException($"EndingPage PK={page.Idx}: Text FK={textIdx} 누락/빈 문구");
            if (!resources.TryGetResource(page.BackgroundResourceIdx, out _))
                throw new InvalidDataException($"EndingPage PK={page.Idx}: Resource FK={page.BackgroundResourceIdx} 실패");
        }
    }

    /// <summary>전체 로딩 검증 후 대기 행을 공개한다.</summary>
    internal void Commit()
    {
        rows = new ReadOnlyDictionary<uint, EndingPageData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>매니저 종료 시 대기·공개 데이터를 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        rows = new ReadOnlyDictionary<uint, EndingPageData>(new Dictionary<uint, EndingPageData>());
    }
}
