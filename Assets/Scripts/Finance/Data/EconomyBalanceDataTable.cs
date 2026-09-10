using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using UnityEngine;

/// <summary>
/// 전역 경제 밸런스 CSV를 파싱하고 검증된 설정을 보관합니다.
/// </summary>
public sealed class EconomyBalanceDataTable : IDataLoad
{
    // 검증을 마친 경제 기본 설정을 ID별로 보관합니다.
    private readonly Dictionary<uint, EconomyBalanceData> dataDict = new Dictionary<uint, EconomyBalanceData>();

    /// <summary>
    /// 현재 보관 중인 경제 기본 설정의 개수를 반환합니다.
    /// </summary>
    /// <returns>검증된 경제 기본 설정의 개수입니다.</returns>
    public int GetDataCount()
    {
        return this.dataDict.Count;
    }

    /// <summary>
    /// CSV 전체를 파싱하고 검증한 뒤 경제 기본 설정을 교체합니다.
    /// </summary>
    /// <param name="csvText">파싱할 EconomyBalanceData CSV 문자열입니다.</param>
    /// <exception cref="ArgumentException">CSV가 비어 있거나 설정 행 개수가 올바르지 않은 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">ID 종류, 중복 또는 설정값이 올바르지 않은 경우 발생합니다.</exception>
    public void LoadData(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            throw new ArgumentException("경제 밸런스 CSV 내용이 비어 있습니다.", nameof(csvText));
        }

        Dictionary<uint, EconomyBalanceData> loadedData = new Dictionary<uint, EconomyBalanceData>();

        using (StringReader reader = new StringReader(csvText))
        using (CsvReader csv = new CsvReader(reader, Util.GetCsvConfiguration()))
        {
            int rowNumber = 1;
            foreach (EconomyBalanceData data in csv.GetRecords<EconomyBalanceData>())
            {
                rowNumber++;
                this.validateData(data, rowNumber);

                if (!loadedData.TryAdd(data.Idx, data))
                {
                    throw new InvalidDataException($"EconomyBalanceData.csv {rowNumber}행: idx {data.Idx}가 중복되었습니다.");
                }
            }
        }

        if (loadedData.Count != 1)
        {
            throw new InvalidDataException("EconomyBalanceData.csv에는 경제 기본 설정이 정확히 한 행 있어야 합니다.");
        }

        // 모든 행을 검증한 뒤 기존 설정을 한 번에 교체합니다.
        this.dataDict.Clear();
        foreach (KeyValuePair<uint, EconomyBalanceData> pair in loadedData)
        {
            this.dataDict.Add(pair.Key, pair.Value);
        }

        Debug.Log("[EconomyBalanceDataTable] 경제 기본 설정 로드 완료.");
    }

    /// <summary>
    /// 지정한 ID의 경제 기본 설정을 조회합니다.
    /// </summary>
    /// <param name="idx">조회할 경제 기본 설정 ID입니다.</param>
    /// <param name="data">설정을 찾은 경우 반환되는 경제 기본 설정입니다.</param>
    /// <returns>설정을 찾으면 true, 없으면 false입니다.</returns>
    public bool TryGetData(uint idx, out EconomyBalanceData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    /// <summary>
    /// 로드된 단일 경제 기본 설정을 반환합니다.
    /// </summary>
    /// <returns>검증을 마친 경제 기본 설정입니다.</returns>
    /// <exception cref="InvalidOperationException">경제 기본 설정이 로드되지 않은 경우 발생합니다.</exception>
    public EconomyBalanceData GetData()
    {
        if (this.dataDict.Count != 1)
        {
            throw new InvalidOperationException("경제 기본 설정이 로드되지 않았습니다.");
        }

        return this.dataDict.Values.First();
    }

    /// <summary>
    /// 보관 중인 경제 기본 설정을 해제합니다.
    /// </summary>
    public void Release()
    {
        this.dataDict.Clear();
    }

    /// <summary>
    /// 경제 기본 설정 한 행의 ID 범위와 필수값을 검증합니다.
    /// </summary>
    /// <param name="data">검증할 경제 기본 설정입니다.</param>
    /// <param name="rowNumber">오류 문맥에 사용할 CSV 행 번호입니다.</param>
    /// <exception cref="InvalidDataException">검증 조건을 만족하지 않는 경우 발생합니다.</exception>
    private void validateData(EconomyBalanceData data, int rowNumber)
    {
        if (Util.GetDataTableType(data.Idx) != DataTableType.EconomyBalance)
        {
            throw new InvalidDataException($"EconomyBalanceData.csv {rowNumber}행: idx {data.Idx}는 경제 기본 밸런스 ID 범위가 아닙니다.");
        }

        if (data.InitialBalance < 0)
        {
            throw new InvalidDataException($"EconomyBalanceData.csv {rowNumber}행: initialBalance는 음수일 수 없습니다.");
        }

    }
}
