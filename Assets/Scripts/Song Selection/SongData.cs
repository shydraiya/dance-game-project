using System;

//말 그대로 노래 데이터를 불러옴
//데이터는 저기 Data 폴더에 SongData.csv 파일로 존재함
[Serializable]
public class SongData
{
    public int no;

    public string title;
    public string author;

    public float time;
    public int difficulty;

    public string musicPath;
    public string patternPath;
    public string coverPath;

    // Pattern Test presentation metadata.
    public string backgroundPath;
    public string avatarPath;

}
