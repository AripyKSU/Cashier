using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>
/// 당일 지침 CSV 데이터의 로딩, 파싱, 유효성 검증 및 일자별 조회를 담당하는 데이터 테이블.
/// </summary>
public sealed class DailyGuidelineDataTable : IDataLoad
{
    private Dictionary<uint, DailyGuidelineData> dataDict = new Dictionary<uint, DailyGuidelineData>();
    private Dictionary<uint, DailyGuidelineData> dayDict = new Dictionary<uint, DailyGuidelineData>();

    /// <summary>전체 FK 검증을 기다리는 파싱 결과.</summary>
    internal Dictionary<uint, DailyGuidelineData> PendingRows { get; private set; }

    /// <summary>검증된 전체 지침 데이터 사전입니다.</summary>
    public IReadOnlyDictionary<uint, DailyGuidelineData> Rows => this.dataDict;

    /// <summary>로드된 지침 데이터 행 수를 반환합니다.</summary>
    /// <returns>행 수.</returns>
    public int GetDataCount() => this.dataDict.Count;

    /// <summary>
    /// PK(idx)로 지침 데이터를 조회합니다.
    /// </summary>
    /// <param name="idx">지침 식별자.</param>
    /// <param name="data">조회된 지침 데이터.</param>
    /// <returns>존재 여부.</returns>
    public bool TryGetData(uint idx, out DailyGuidelineData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    /// <summary>
    /// 일차(day)로 지침 데이터를 조회합니다.
    /// </summary>
    /// <param name="day">게임 일차 (1부터 시작).</param>
    /// <param name="data">해당 일차의 지침 데이터.</param>
    /// <returns>존재 여부.</returns>
    public bool TryGetByDay(uint day, out DailyGuidelineData data)
    {
        return this.dayDict.TryGetValue(day, out data);
    }

    /// <summary>
    /// CSV 텍스트를 파싱하고 대역 및 유효성을 검증하여 메모리에 캐싱합니다.
    /// </summary>
    /// <param name="csvText">CSV 원문 문자열.</param>
    /// <exception cref="InvalidDataException">헤더, 대역, 필수값 누락 등 유효성 실패 시 발생합니다.</exception>
    public void LoadData(string csvText)
    {
        PendingRows = null;
        using (var reader = new StringReader(csvText ?? string.Empty))
        using (var csv = new CsvReader(reader, Util.GetCsvConfiguration()))
        {
            try
            {
                if (!csv.Read()) throw new InvalidDataException("DailyGuidelineData: header 누락");
                csv.ReadHeader();
                csv.ValidateHeader<DailyGuidelineData>();

                var parsed = new Dictionary<uint, DailyGuidelineData>();
                var parsedByDay = new Dictionary<uint, DailyGuidelineData>();

                while (csv.Read())
                {
                    var item = csv.GetRecord<DailyGuidelineData>();
                    if (Util.GetDataTableType(item.Idx) != DataTableType.DailyGuideline || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
                    {
                        throw new InvalidDataException($"DailyGuideline PK={item.Idx}: 대역 위반 또는 중복");
                    }

                    item.Validate();
                    parsed.Add(item.Idx, item);

                    if (parsedByDay.ContainsKey(item.Day))
                        throw new InvalidDataException($"DailyGuideline PK={item.Idx}: day={item.Day} 중복");
                    parsedByDay.Add(item.Day, item);
                }

                if (parsed.Count == 0)
                {
                    throw new InvalidDataException("DailyGuidelineData: 데이터 행 누락");
                }

                this.PendingRows = parsed;
            }
            catch (Exception exception)
            {
                Debug.LogError($"DailyGuidelineData.csv row={csv.Parser?.Row}, columnIndex={csv.CurrentIndex}: {exception}");
                throw;
            }
        }

    }

    /// <summary>공개 전 지침의 Text·상품 FK를 검증한다.</summary>
    /// <param name="texts">공개 전 텍스트 행.</param>
    /// <param name="products">공개 전 상품 행.</param>
    /// <exception cref="InvalidDataException">필수 테이블 또는 FK 누락.</exception>
    public void Validate(IReadOnlyDictionary<uint, TextData> texts, IReadOnlyDictionary<uint, ProductData> products)
    {
        if (PendingRows == null || texts == null || products == null)
            throw new InvalidDataException("DailyGuidelineData: 지침·Text·Product CSV가 필요합니다.");
        foreach (var row in PendingRows.Values)
        {
            if (!texts.ContainsKey(row.NameIdx) || !texts.ContainsKey(row.DescriptionIdx))
                throw new InvalidDataException($"DailyGuidelineData PK={row.Idx}: nameidx/descriptionidx Text FK 실패");
            if (row.TargetProductIdx != 0 && !products.ContainsKey(row.TargetProductIdx))
                throw new InvalidDataException($"DailyGuidelineData PK={row.Idx}: target_product_idx={row.TargetProductIdx} Product FK 실패");
        }
    }

    /// <summary>전체 검증 성공 후 일차 조회와 PK 조회를 함께 공개한다.</summary>
    internal void Commit()
    {
        var days = new Dictionary<uint, DailyGuidelineData>();
        foreach (var row in PendingRows.Values) days.Add(row.Day, row);
        this.dataDict = PendingRows;
        this.dayDict = days;
        PendingRows = null;
    }

    /// <summary>
    /// 매니저 종료 시 캐시를 해제합니다.
    /// </summary>
    public void Release()
    {
        PendingRows = null;
        this.dataDict.Clear();
        this.dayDict.Clear();
    }
}
