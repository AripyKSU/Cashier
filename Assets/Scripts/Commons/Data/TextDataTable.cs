using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>표시 문자열 CSV의 파싱·PK·행 검증과 조회를 담당한다. 연관 FK 검증 후에만 공개한다.</summary>
public sealed class TextDataTable : IDataLoad
{
    // 실패 시 이전 공개 데이터를 유지하며 catalog가 검증한 후 교체한다.
    private IReadOnlyDictionary<uint, TextData> dataDict = new ReadOnlyDictionary<uint, TextData>(new Dictionary<uint, TextData>());
    /// <summary>FK 검증 대기 데이터. 공개 조회에는 사용하지 않는다.</summary>
    internal Dictionary<uint, TextData> PendingRows { get; private set; }
    /// <summary>FK까지 검증된 공개 행. 소비자는 DTO 값을 변경하지 않는다.</summary>
    public IReadOnlyDictionary<uint, TextData> Rows => dataDict;

    /// <summary>공개 데이터 개수를 반환한다.</summary>
    /// <returns>검증된 행 수.</returns>
    public int GetDataCount() => dataDict.Count;

    /// <summary>PK로 검증된 데이터를 조회한다.</summary>
    /// <param name="idx">조회할 PK.</param>
    /// <param name="data">조회 결과. 없으면 null.</param>
    /// <returns>존재 여부.</returns>
    public bool TryGetData(uint idx, out TextData data) => dataDict.TryGetValue(idx, out data);

    /// <summary>통화 서식 템플릿 PK (TextData.csv 8248).</summary>
    public const uint CurrencyFormatIdx = 8248;
    /// <summary>통화 단위 텍스트 PK (하위 호환성 유지).</summary>
    public const uint CurrencyUnitIdx = 8248;

    /// <summary>TextData PK 8248번에서 통화 서식 템플릿(기본: "{0:N0} 원")을 조회합니다.</summary>
    public string GetCurrencyFormat()
    {
        return TryGetData(CurrencyFormatIdx, out var textData) && !string.IsNullOrWhiteSpace(textData.Text)
            ? textData.Text
            : "{0:N0} 원";
    }

    /// <summary>TextData PK 8248번에서 통화 단위를 조회합니다. 포맷 템플릿인 경우 서식 기호를 제외한 단위 문자열을 반환합니다.</summary>
    public string GetCurrencyUnit()
    {
        string format = GetCurrencyFormat();
        int braceEnd = format.LastIndexOf('}');
        if (braceEnd >= 0 && braceEnd < format.Length - 1)
        {
            return format.Substring(braceEnd + 1).Trim();
        }
        return format;
    }

    /// <summary>현재 로드된 DataTableManager 싱글톤에서 통화 서식 템플릿을 조회합니다.</summary>
    public static string ResolveCurrencyFormat()
    {
        if (DataTableManager.Instance != null &&
            DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text) is { } textTable)
        {
            return textTable.GetCurrencyFormat();
        }
        return "{0:N0} 원";
    }

    /// <summary>현재 로드된 DataTableManager 싱글톤에서 통화 단위를 조회합니다. 없으면 '원'을 반환합니다.</summary>
    public static string ResolveCurrencyUnit()
    {
        if (DataTableManager.Instance != null &&
            DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text) is { } textTable)
        {
            return textTable.GetCurrencyUnit();
        }
        return "원";
    }

    /// <summary>금액을 등록된 통화 서식 템플릿(기본: "{0:N0} 원")에 맞추어 포맷팅합니다. (예: 1000 -> "1,000 원")</summary>
    public static string FormatCurrency(long amount)
    {
        string format = ResolveCurrencyFormat();
        try
        {
            return string.Format(format, amount);
        }
        catch (FormatException)
        {
            return $"{amount:N0} 원";
        }
    }

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
            csv.ValidateHeader<TextData>();
            var parsed = new Dictionary<uint, TextData>();
            while (csv.Read())
            {
                var item = csv.GetRecord<TextData>();
                if (Util.GetDataTableType(item.Idx) != DataTableType.Text || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
                    throw new InvalidDataException($"column=idx, PK={item.Idx}: 대역 위반 또는 중복");
                if (string.IsNullOrWhiteSpace(item.Text)) throw new InvalidDataException("column=text: 필수값 누락");
                parsed.Add(item.Idx, item);
            }
            if (parsed.Count == 0) throw new InvalidDataException("데이터 행 누락");
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"TextData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>catalog의 전체 FK 검증 성공 후 공개 사전을 교체한다.</summary>
    internal void Commit()
    {
        dataDict = new ReadOnlyDictionary<uint, TextData>(PendingRows);
        PendingRows = null;
    }

    /// <summary>manager 종료 시 공개·대기 데이터를 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        dataDict = new ReadOnlyDictionary<uint, TextData>(new Dictionary<uint, TextData>());
    }
}
