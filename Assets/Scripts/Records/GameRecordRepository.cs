using System;
using System.IO;
using UnityEngine;

public static class GameRecordRepository
{
    private const string FileName = "records.json";

    private static RecordSaveData saveData;

    public static string SavePath =>
        Path.Combine(Application.persistentDataPath, FileName);

    public static SongRecord GetRecord(int songId)
    {
        EnsureLoaded();
        return saveData.records.Find(record => record.songId == songId);
    }

    public static bool SaveResult(GameResult result)
    {
        if (result == null)
        {
            Debug.LogError("GameRecordRepository: Cannot save a null game result.");
            return false;
        }

        EnsureLoaded();

        SongRecord record = saveData.records.Find(item => item.songId == result.songId);
        bool isNewRecord = record == null;

        if (isNewRecord)
        {
            record = new SongRecord { songId = result.songId };
            saveData.records.Add(record);
        }

        record.playCount++;

        if (isNewRecord || result.score > record.highScore)
        {
            record.highScore = result.score;
            record.bestPerfect = result.perfect;
            record.bestGood = result.good;
            record.bestBad = result.bad;
            record.bestMiss = result.miss;
        }

        if (isNewRecord || result.accuracy > record.bestAccuracy)
        {
            record.bestAccuracy = result.accuracy;
        }

        return WriteSaveData();
    }

    private static void EnsureLoaded()
    {
        if (saveData != null)
        {
            return;
        }

        saveData = LoadSaveData();
    }

    private static RecordSaveData LoadSaveData()
    {
        if (!File.Exists(SavePath))
        {
            return new RecordSaveData();
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            RecordSaveData loadedData = JsonUtility.FromJson<RecordSaveData>(json);

            if (loadedData == null)
            {
                return new RecordSaveData();
            }

            if (loadedData.records == null)
            {
                loadedData.records = new System.Collections.Generic.List<SongRecord>();
            }

            return loadedData;
        }
        catch (Exception exception)
        {
            Debug.LogError($"GameRecordRepository: Failed to load records from {SavePath}.\n{exception}");
            return new RecordSaveData();
        }
    }

    private static bool WriteSaveData()
    {
        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"GameRecordRepository: Game record saved to {SavePath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"GameRecordRepository: Failed to save records to {SavePath}.\n{exception}");
            return false;
        }
    }
}
