using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Individual daily record in the ledger accounting table.
/// Conforms to DATA_RULES.md for serialization and reporting.
/// </summary>
[Serializable]
public sealed class LedgerRecord
{
    public int day;
    public int maintenanceFee;
    public int upgradeExpense;
    public int revenue;
    public int netCash;
    public int reputationChange;
    public int netReputation;

    public LedgerRecord() { }

    public LedgerRecord(int day, int maintenanceFee, int upgradeExpense, int revenue, int netCash, int reputationChange, int netReputation)
    {
        this.day = day;
        this.maintenanceFee = maintenanceFee;
        this.upgradeExpense = upgradeExpense;
        this.revenue = revenue;
        this.netCash = netCash;
        this.reputationChange = reputationChange;
        this.netReputation = netReputation;
    }
}

/// <summary>
/// Full game state data container for save and load persistence.
/// </summary>
[Serializable]
public sealed class GameSaveData
{
    public int currentDay = 1;
    public int cash = 0;
    public int reputation = 50;
    public int morality = 50;
    public int buildingTier = 1;
    public float bgmVolume = 0.8f;
    public float sfxVolume = 1.0f;
    public List<LedgerRecord> ledgerHistory = new List<LedgerRecord>();

    /// <summary>Creates a default initialized new game save.</summary>
    public static GameSaveData CreateNewGame()
    {
        return new GameSaveData
        {
            currentDay = 1,
            cash = 0,
            reputation = 50,
            morality = 50,
            buildingTier = 1,
            bgmVolume = 0.8f,
            sfxVolume = 1.0f,
            ledgerHistory = new List<LedgerRecord>()
        };
    }
}

/// <summary>
/// Persistent storage helper utilizing JSON serialization and PlayerPrefs.
/// </summary>
public static class CashierSaveManager
{
    private const string SaveKey = "Cashier_LocalGameSave_v1";

    public static bool HasSave()
    {
        return PlayerPrefs.HasKey(SaveKey);
    }

    public static void Save(GameSaveData data)
    {
        if (data == null) return;
        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public static GameSaveData Load()
    {
        if (!HasSave()) return GameSaveData.CreateNewGame();
        try
        {
            string json = PlayerPrefs.GetString(SaveKey);
            var data = JsonUtility.FromJson<GameSaveData>(json);
            return data ?? GameSaveData.CreateNewGame();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CashierSaveManager] Failed to load save data: {ex.Message}. Creating new game.");
            return GameSaveData.CreateNewGame();
        }
    }

    public static void DeleteSave()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
