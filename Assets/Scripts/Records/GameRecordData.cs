// JSON 기록 저장에 사용하는 직렬화 데이터 구조이다.
// GameResult는 한 번의 결과, SongRecord는 곡별 누적 기록, RecordSaveData는 전체 저장 파일을 나타낸다.

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
    // 최고 점수를 기록한 플레이의 판정 횟수이다. 최고 정확도를 낸 플레이와는 다를 수 있다.
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
