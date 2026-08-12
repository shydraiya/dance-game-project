using System;
using System.Collections.Generic;

[Serializable]
public class GameResult
{
    public int songId;
    public int score;
    public float accuracy;
    public int perfect;
    public int good;
    public int bad;
    public int miss;
}

[Serializable]
public class SongRecord
{
    public int songId;
    public int highScore;
    public float bestAccuracy;
    public int playCount;
    public int bestPerfect;
    public int bestGood;
    public int bestBad;
    public int bestMiss;
}

[Serializable]
public class RecordSaveData
{
    public int version = 1;
    public List<SongRecord> records = new List<SongRecord>();
}
