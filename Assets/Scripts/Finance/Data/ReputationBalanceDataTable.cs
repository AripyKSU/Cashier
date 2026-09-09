using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CsvHelper;
using UnityEngine;

/// <summary>
/// 명성 구간별 손님 구성과 회복 배율 CSV를 파싱하고 검증합니다.
/// </summary>
public sealed class ReputationBalanceDataTable : IDataLoad
{
    private IReadOnlyDictionary<uint, ReputationBalanceData> rows =
        new ReadOnlyDictionary<uint, ReputationBalanceData>(new Dictionary<uint, ReputationBalanceData>());

    /// <summary>검증된 명성 구간 행을 반환합니다.</summary>
    public IReadOnlyDictionary<uint, ReputationBalanceData> Rows => this.rows;

    /// <summary>검증된 행 수를 반환합니다.</summary>
    /// <returns>명성 구간 행 수입니다.</returns>
    public int GetDataCount()
    {
        return this.rows.Count;
    }

    /// <summary>CSV 전체를 임시 사전에서 검증한 뒤 공개 데이터를 교체합니다.</summary>
    /// <param name="csvText">파싱할 ReputationBalanceData CSV 문자열입니다.</param>
    /// <exception cref="InvalidDataException">헤더, PK, 구간 또는 확률이 잘못된 경우 발생합니다.</exception>
    public void LoadData(string csvText)
    {
        using StringReader reader = new StringReader(csvText ?? string.Empty);
        using CsvReader csv = new CsvReader(reader, Util.GetCsvConfiguration());
        Dictionary<uint, ReputationBalanceData> parsed = new Dictionary<uint, ReputationBalanceData>();

        try
        {
            if (!csv.Read()) throw new InvalidDataException("header 누락");
            csv.ReadHeader();
            csv.ValidateHeader<ReputationBalanceData>();

            while (csv.Read())
            {
                ReputationBalanceData item = csv.GetRecord<ReputationBalanceData>();
                validateRow(item, parsed);
                parsed.Add(item.Idx, item);
            }

            validateBands(parsed.Values);
            this.rows = new ReadOnlyDictionary<uint, ReputationBalanceData>(parsed);
        }
        catch (Exception exception)
        {
            Debug.LogError($"ReputationBalanceData.csv row={csv.Parser.Row}, columnIndex={csv.CurrentIndex}: {exception}");
            throw;
        }
    }

    /// <summary>현재 명성에 해당하는 구간 데이터를 조회합니다.</summary>
    /// <param name="reputation">-100 이상 100 이하의 현재 명성입니다.</param>
    /// <param name="data">해당 구간 데이터입니다.</param>
    /// <returns>해당 구간을 찾으면 true입니다.</returns>
    public bool TryGetByReputation(int reputation, out ReputationBalanceData data)
    {
        foreach (ReputationBalanceData row in this.rows.Values)
        {
            if (reputation >= row.MinReputation && reputation <= row.MaxReputation)
            {
                data = row;
                return true;
            }
        }

        data = null;
        return false;
    }

    /// <summary>행동 점수에 해당하는 일일 정산 데이터를 조회합니다.</summary>
    /// <param name="settlementScore">0~100 행동 점수입니다.</param>
    /// <param name="data">정산 데이터입니다.</param>
    /// <returns>구간이 있으면 true입니다.</returns>
    public bool TryGetBySettlementScore(int settlementScore, out ReputationBalanceData data)
    {
        foreach (ReputationBalanceData row in this.rows.Values)
        {
            if (settlementScore >= row.SettlementMinScore && settlementScore <= row.SettlementMaxScore)
            {
                data = row;
                return true;
            }
        }

        data = null;
        return false;
    }

    /// <summary>보관 중인 검증 데이터를 해제합니다.</summary>
    public void Release()
    {
        this.rows = new ReadOnlyDictionary<uint, ReputationBalanceData>(new Dictionary<uint, ReputationBalanceData>());
    }

    private static void validateRow(ReputationBalanceData item, Dictionary<uint, ReputationBalanceData> parsed)
    {
        if (Util.GetDataTableType(item.Idx) != DataTableType.ReputationBalance || item.Idx % 1000 == 0 || parsed.ContainsKey(item.Idx))
            throw new InvalidDataException($"column=idx, PK={item.Idx}: 대역 위반 또는 중복");
        if (item.MinReputation < -100 || item.MaxReputation > 100 || item.MinReputation > item.MaxReputation)
            throw new InvalidDataException($"PK={item.Idx}: 명성 구간 오류");
        if (item.NormalWeight < 0 || item.WealthyWeight < 0 || item.HastyWeight < 0 || item.SpecialWeight < 0 ||
            item.NormalWeight + item.WealthyWeight + item.HastyWeight + item.SpecialWeight != 1000)
            throw new InvalidDataException($"PK={item.Idx}: 손님 구성 확률 합은 1000이어야 합니다.");
        if (item.RecoveryRate < 1000)
            throw new InvalidDataException($"PK={item.Idx}: recovery_rate는 1000 이상이어야 합니다.");
        if (item.SettlementMinScore < 0 || item.SettlementMaxScore > 100 ||
            item.SettlementMinScore > item.SettlementMaxScore ||
            item.SettlementDelta < -15 || item.SettlementDelta > 10)
            throw new InvalidDataException($"PK={item.Idx}: 정산 점수 구간 또는 변화량 오류");
    }

    private static void validateBands(ICollection<ReputationBalanceData> rows)
    {
        if (rows.Count != 5) throw new InvalidDataException("명성 구간은 정확히 5개여야 합니다.");

        for (int reputation = -100; reputation <= 100; reputation++)
        {
            int matches = 0;
            foreach (ReputationBalanceData row in rows)
                if (reputation >= row.MinReputation && reputation <= row.MaxReputation) matches++;
            if (matches != 1) throw new InvalidDataException($"명성 {reputation}을 포함하는 구간은 정확히 하나여야 합니다.");
        }

        for (int score = 0; score <= 100; score++)
        {
            int matches = 0;
            foreach (ReputationBalanceData row in rows)
                if (score >= row.SettlementMinScore && score <= row.SettlementMaxScore) matches++;
            if (matches != 1) throw new InvalidDataException($"정산 점수 {score}을 포함하는 구간은 정확히 하나여야 합니다.");
        }
    }
}
