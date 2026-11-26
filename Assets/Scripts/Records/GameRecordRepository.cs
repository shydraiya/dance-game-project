// 곡별 최고 점수·정확도·플레이 횟수를 records.json으로 저장하고 복원한다.
// Application.persistentDataPath를 사용하여 실행 파일 위치와 별개로 기록을 유지한다.

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

    // 플레이 횟수는 매번 증가시키고 최고 점수와 최고 정확도는 각각 더 좋은 값일 때 갱신한다.
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
