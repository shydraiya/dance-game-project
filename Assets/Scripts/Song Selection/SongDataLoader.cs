using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

//SongData.csv 파일을 가져오는 코드임
public static class SongDataLoader
{
    public static List<SongData> Load(TextAsset csvFile)
    {
        List<SongData> songs = new List<SongData>();

        if (csvFile == null)
        {
            Debug.LogError("Song CSV 파일이 연결되지 않았습니다.");
            return songs;
        }

        string text = csvFile.text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = text.Split('\n');

        // 첫 번째 줄은 헤더이므로 건너뜀.
        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row]))
                continue;

            List<string> columns = ParseCsvLine(lines[row]);

            // no, name, author, time, difficulty를 가져오게 됨
            if (columns.Count < 5)
            {
                Debug.LogWarning($"CSV {row + 1}번째 줄의 열 개수가 부족합니다.");
                continue;
            }

            if (!int.TryParse(columns[0], out int no))
            {
                Debug.LogWarning($"CSV {row + 1}번째 줄의 no 값이 올바르지 않습니다.");
                continue;
            }

            if (!float.TryParse(
                columns[3],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float time))
            {
                time = 0f;
            }

            if (!int.TryParse(columns[4], out int difficulty))
            {
                difficulty = 1;
            }

            //여기서 song 구조를 생성
            SongData song = new SongData
            {
                no         = no,
                title      = columns[1],
                author     = columns[2],
                time       = time,
                difficulty = difficulty,
                musicPath  = columns.Count > 5 ? columns[5] : "", // Path가 비워져 있으면 그냥 둠
                patternPath = columns.Count > 6 ? columns[6] : "",
                backgroundPath = columns.Count > 8 ? columns[8] : "",
                avatarPath = columns.Count > 9 ? columns[9] : "",
                coverPath = columns.Count > 7 ? columns[7] : "",
            };

            //그렇게 만들어진 song을 songs 배열에 추가함
            songs.Add(song);
        }

        return songs;
    }

    // 제목이나 작곡가 이름에 쉼표가 포함되어도 읽을 수 있도록 처리
    private static List<string> ParseCsvLine(string line)
    {
        List<string> result = new List<string>();
        StringBuilder current = new StringBuilder();

        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                // CSV 내부의 ""는 실제 큰따옴표 하나를 의미함.
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());

        return result;
    }
}
