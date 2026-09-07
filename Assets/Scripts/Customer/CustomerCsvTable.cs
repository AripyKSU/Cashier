using System;
using System.Collections.Generic;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>손님 관련 CSV와 TextData의 파싱·PK·행 검증. FK 검사 전에는 행을 공개하지 않는다.</summary>
/// <typeparam name="T">CSV DTO.</typeparam>
public sealed class CustomerCsvTable<T> : IDataLoad where T : class
{
    /// <summary>오류 문맥에 표시할 CSV 파일명.</summary>
    private readonly string fileName;
    /// <summary>허용 PK 천 단위 대역.</summary>
    private readonly uint tableType;
    /// <summary>DTO의 PK 접근자.</summary>
    private readonly Func<T, uint> getId;
    /// <summary>필수값 및 범위 검증.</summary>
    private readonly Action<T> validate;
    /// <summary>FK 검증 대기 행. 로딩 실패 시 이전 공개 데이터는 보존한다.</summary>
    internal Dictionary<uint, T> PendingRows { get; private set; }
    /// <summary>전체 데이터 검증 완료 후 공개하는 행.</summary>
    public IReadOnlyDictionary<uint, T> Rows { get; private set; } = new Dictionary<uint, T>();

    /// <summary>테이블별 실제로 다른 PK·필수값 검증만 지정한다.</summary>
    /// <param name="fileName">CSV 파일명.</param>
    /// <param name="tableType">DataTableType.</param>
    /// <param name="getId">행의 PK 접근자.</param>
    /// <param name="validate">행 검증 함수.</param>
    public CustomerCsvTable(string fileName, DataTableType tableType, Func<T, uint> getId, Action<T> validate)
    {
        this.fileName = fileName;
        this.tableType = (uint)tableType;
        this.getId = getId;
        this.validate = validate;
    }

    /// <summary>CSV 전체를 별도 사전에 파싱하고 오류에는 파일·행·열 문맥을 남긴다.</summary>
    /// <param name="csvText">UTF-8 CSV 내용.</param>
    /// <exception cref="Exception">필수 header, 형식, PK 또는 행 검증 실패.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using var reader = new StringReader(csvText ?? string.Empty);
        using var csv = new CsvReader(reader, Util.GetCsvConfiguration());
        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<T>();
            var parsed = new Dictionary<uint, T>();
            while (csv.Read())
            {
                var item = csv.GetRecord<T>();
                uint id = getId(item);
                if (id / 1000 != tableType || id % 1000 == 0 || parsed.ContainsKey(id))
                    throw new InvalidDataException($"column=idx, PK={id}: 대역 위반 또는 중복");
                validate(item);
                parsed.Add(id, item);
            }
            if (parsed.Count == 0) throw new InvalidDataException("데이터 행 누락");
            PendingRows = parsed;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Customer CSV] {fileName}, row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>연관 테이블과의 FK 검증 성공 후 공개한다.</summary>
    internal void Commit()
    {
        Rows = new System.Collections.ObjectModel.ReadOnlyDictionary<uint, T>(PendingRows);
        PendingRows = null;
    }

    /// <summary>검증 완료하여 공개된 행 수를 반환한다.</summary>
    /// <returns>행 수.</returns>
    public int GetDataCount() => Rows.Count;

    /// <summary>씬이 아닌 manager 수명 종료 시 데이터 참조를 해제한다.</summary>
    public void Release()
    {
        PendingRows = null;
        Rows = new Dictionary<uint, T>();
    }
}
