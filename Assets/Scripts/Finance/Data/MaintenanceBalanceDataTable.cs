using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using UnityEngine;

/// <summary>
/// 일자별 유지비 CSV를 파싱하고 날짜 순서가 검증된 설정을 보관합니다.
/// </summary>
public sealed class MaintenanceBalanceDataTable : IDataLoad
{
    // 검증을 마친 회차별 상납금 설정을 ID별로 보관합니다.
    private readonly Dictionary<uint, MaintenanceBalanceData> dataDict = new Dictionary<uint, MaintenanceBalanceData>();

    /// <summary>
    /// 현재 보관 중인 회차별 상납금 설정의 개수를 반환합니다.
    /// </summary>
    /// <returns>검증된 회차별 상납금 설정의 개수입니다.</returns>
    public int GetDataCount()
    {
        return this.dataDict.Count;
    }

    /// <summary>
    /// CSV 전체를 파싱하고 검증한 뒤 회차별 상납금 설정을 교체합니다.
    /// </summary>
    /// <param name="csvText">파싱할 MaintenanceBalanceData CSV 문자열입니다.</param>
    /// <exception cref="ArgumentException">CSV가 비어 있는 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">ID, 회차 또는 상납금이 올바르지 않은 경우 발생합니다.</exception>
    public void LoadData(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            throw new ArgumentException("회차별 상납금 CSV 내용이 비어 있습니다.", nameof(csvText));
        }

        Dictionary<uint, MaintenanceBalanceData> loadedData = new Dictionary<uint, MaintenanceBalanceData>();
        HashSet<int> loadedDays = new HashSet<int>();

        using (StringReader reader = new StringReader(csvText))
        using (CsvReader csv = new CsvReader(reader, Util.GetCsvConfiguration()))
        {
            int rowNumber = 1;
            foreach (MaintenanceBalanceData data in csv.GetRecords<MaintenanceBalanceData>())
            {
                rowNumber++;
                this.validateData(data, rowNumber);

                if (!loadedData.TryAdd(data.Idx, data))
                {
                    throw new InvalidDataException($"MaintenanceBalanceData.csv {rowNumber}행: idx {data.Idx}가 중복되었습니다.");
                }

                if (!loadedDays.Add(data.Day))
                {
                    throw new InvalidDataException($"MaintenanceBalanceData.csv {rowNumber}행: day {data.Day}가 중복되었습니다.");
                }
            }
        }

        if (loadedData.Count == 0)
        {
            throw new InvalidDataException("MaintenanceBalanceData.csv에는 한 회차 이상의 설정이 있어야 합니다.");
        }

        int[] orderedDays = loadedDays.OrderBy(day => day).ToArray();
        for (int index = 0; index < orderedDays.Length; index++)
        {
            if (orderedDays[index] != index + 1)
            {
                throw new InvalidDataException("MaintenanceBalanceData.csv의 day는 1부터 중간 누락 없이 이어져야 합니다.");
            }
        }

        // 모든 행과 회차 순서를 검증한 뒤 기존 설정을 한 번에 교체합니다.
        this.dataDict.Clear();
        foreach (KeyValuePair<uint, MaintenanceBalanceData> pair in loadedData)
        {
            this.dataDict.Add(pair.Key, pair.Value);
        }

        Debug.Log($"[MaintenanceBalanceDataTable] 총 {this.dataDict.Count}개 회차의 상납금 설정 로드 완료.");
    }

    /// <summary>
    /// 지정한 ID의 회차별 상납금 설정을 조회합니다.
    /// </summary>
    /// <param name="idx">조회할 회차별 상납금 설정 ID입니다.</param>
    /// <param name="data">설정을 찾은 경우 반환되는 상납금 설정입니다.</param>
    /// <returns>설정을 찾으면 true, 없으면 false입니다.</returns>
    public bool TryGetData(uint idx, out MaintenanceBalanceData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    /// <summary>
    /// 납부 회차 순서로 정렬된 상납금 목록을 생성합니다.
    /// </summary>
    /// <returns>1회차부터 순서대로 정렬된 상납금 배열입니다.</returns>
    public long[] GetMaintenanceAmounts()
    {
        return this.dataDict.Values
            .OrderBy(data => data.Day)
            .Select(data => data.MaintenanceAmount)
            .ToArray();
    }

    /// <summary>
    /// 보관 중인 회차별 상납금 설정을 해제합니다.
    /// </summary>
    public void Release()
    {
        this.dataDict.Clear();
    }

    /// <summary>
    /// 회차별 상납금 한 행의 ID 범위와 필수값을 검증합니다.
    /// </summary>
    /// <param name="data">검증할 회차별 상납금 설정입니다.</param>
    /// <param name="rowNumber">오류 문맥에 사용할 CSV 행 번호입니다.</param>
    /// <exception cref="InvalidDataException">검증 조건을 만족하지 않는 경우 발생합니다.</exception>
    private void validateData(MaintenanceBalanceData data, int rowNumber)
    {
        if (Util.GetDataTableType(data.Idx) != DataTableType.MaintenanceBalance)
        {
            throw new InvalidDataException($"MaintenanceBalanceData.csv {rowNumber}행: idx {data.Idx}는 회차별 상납금 ID 범위가 아닙니다.");
        }

        if (data.Day <= 0)
        {
            throw new InvalidDataException($"MaintenanceBalanceData.csv {rowNumber}행: day는 0보다 커야 합니다.");
        }

        if (data.MaintenanceAmount <= 0)
        {
            throw new InvalidDataException($"MaintenanceBalanceData.csv {rowNumber}행: maintenanceAmount는 0보다 커야 합니다.");
        }
    }
}
