using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>
/// 무작위 일일지침 규칙 설정의 로딩, 검증 및 유형별 조회를 담당하는 데이터 테이블.
/// </summary>
public sealed class DailyGuidelineDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, DailyGuidelineData> dataDict =
        new ReadOnlyDictionary<uint, DailyGuidelineData>(new Dictionary<uint, DailyGuidelineData>());
    private IReadOnlyDictionary<DailyGuidelineRuleType, DailyGuidelineData> ruleTypeDict =
        new ReadOnlyDictionary<DailyGuidelineRuleType, DailyGuidelineData>(new Dictionary<DailyGuidelineRuleType, DailyGuidelineData>());

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
    /// 지침 유형으로 무작위 생성 설정을 조회합니다.
    /// </summary>
    /// <param name="ruleType">조회할 지침 유형.</param>
    /// <param name="data">해당 유형의 설정.</param>
    /// <returns>존재 여부.</returns>
    public bool TryGetByRuleType(DailyGuidelineRuleType ruleType, out DailyGuidelineData data)
    {
        return this.ruleTypeDict.TryGetValue(ruleType, out data);
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
                var parsedByRuleType = new Dictionary<DailyGuidelineRuleType, DailyGuidelineData>();

                while (csv.Read())
                {
                    var item = csv.GetRecord<DailyGuidelineData>();
                    if (Util.GetDataTableType(item.Idx) != DataTableType.DailyGuideline || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
                    {
                        throw new InvalidDataException($"DailyGuideline PK={item.Idx}: 대역 위반 또는 중복");
                    }

                    item.Validate();
                    parsed.Add(item.Idx, item);

                    if (parsedByRuleType.ContainsKey(item.RuleType))
                        throw new InvalidDataException($"DailyGuideline rule_type={item.RuleType}: 중복 설정");
                    parsedByRuleType.Add(item.RuleType, item);
                }

                if (parsed.Count == 0)
                {
                    throw new InvalidDataException("DailyGuidelineData: 데이터 행 누락");
                }

                foreach (DailyGuidelineRuleType ruleType in new[]
                {
                    DailyGuidelineRuleType.SaleProhibited,
                    DailyGuidelineRuleType.QuantityLimited
                })
                    if (!parsedByRuleType.ContainsKey(ruleType))
                        throw new InvalidDataException($"DailyGuidelineData: rule_type={ruleType} 설정 누락");

                this.dataDict = new ReadOnlyDictionary<uint, DailyGuidelineData>(parsed);
                this.ruleTypeDict = new ReadOnlyDictionary<DailyGuidelineRuleType, DailyGuidelineData>(parsedByRuleType);
                this.PendingRows = parsed;
            }
            catch (Exception exception)
            {
                Debug.LogError($"DailyGuidelineData.csv row={csv.Parser?.Row}, columnIndex={csv.CurrentIndex}: {exception}");
                throw;
            }
        }

    }

    /// <summary>공개 전 새 규칙 스키마의 파싱 완료 여부를 검증한다.</summary>
    /// <param name="texts">통합 로더 호출 순서를 유지하기 위한 인수. 새 스키마에는 Text FK가 없다.</param>
    /// <param name="products">통합 로더 호출 순서를 유지하기 위한 인수. 대상 상품은 매일 런타임에 선택한다.</param>
    /// <exception cref="InvalidDataException">파싱 결과가 준비되지 않은 경우.</exception>
    public void Validate(IReadOnlyDictionary<uint, TextData> texts, IReadOnlyDictionary<uint, ProductData> products)
    {
        if (PendingRows == null)
            throw new InvalidDataException("DailyGuidelineData: 파싱 결과가 필요합니다.");
    }

    /// <summary>전체 검증 성공 후 PK와 규칙 유형 조회를 함께 공개한다.</summary>
    internal void Commit()
    {
        var committedRows = new Dictionary<uint, DailyGuidelineData>(PendingRows);
        var committedRuleTypes = new Dictionary<DailyGuidelineRuleType, DailyGuidelineData>();
        foreach (DailyGuidelineData row in committedRows.Values)
            committedRuleTypes.Add(row.RuleType, row);
        this.dataDict = new ReadOnlyDictionary<uint, DailyGuidelineData>(committedRows);
        this.ruleTypeDict = new ReadOnlyDictionary<DailyGuidelineRuleType, DailyGuidelineData>(committedRuleTypes);
        PendingRows = null;
    }

    /// <summary>
    /// 매니저 종료 시 캐시를 해제합니다.
    /// </summary>
    public void Release()
    {
        this.dataDict = new ReadOnlyDictionary<uint, DailyGuidelineData>(new Dictionary<uint, DailyGuidelineData>());
        this.ruleTypeDict = new ReadOnlyDictionary<DailyGuidelineRuleType, DailyGuidelineData>(new Dictionary<DailyGuidelineRuleType, DailyGuidelineData>());
        PendingRows = null;
    }
}
