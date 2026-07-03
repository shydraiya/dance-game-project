using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

//사용 방법
//Framse[i] 여기에 i번째 패턴 데이터가 들어있음
//Frames[i].time = i번째 패턴 데이터의 시간 (t = 1 : 1초에서의 패턴 디자인)
//Frames[i].GetAngle("이름") = 이름의 요구 각도 출력
//이름 : neck	shoulder_l	shoulder_r	elbow_l	elbow_r	hip_l	hip_r	knee_l	knee_r
//패턴 파일은 Assets/Patterns 내부에 존재
//지금은 fileName = "pattern_sample.csv" 요렇게 넣었는데, 나중에 바꿀 필요가 있음

public class PatternLoader : MonoBehaviour
{
    [SerializeField] private string fileName = "pattern_sample.csv";

    public List<PatternFrame> Frames { get; private set; } = new List<PatternFrame>();

    private void Start()
    {
        Frames = LoadPattern(fileName);

        Debug.Log($"Loaded Pattern Frames: {Frames.Count}");

        if (Frames.Count > 0)
        {
            Debug.Log($"First frame time: {Frames[0].time}");
            Debug.Log($"Neck angle: {Frames[0].GetAngle("neck")}");
        }
    }

    public static List<PatternFrame> LoadPattern(string fileName)
    {
        List<PatternFrame> result = new List<PatternFrame>();

        string path = Path.Combine(Application.dataPath, "Patterns", fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"Pattern file not found: {path}");
            return result;
        }

        string[] lines = File.ReadAllLines(path);

        if (lines.Length <= 1)
        {
            Debug.LogWarning("Pattern file is empty or has no data rows.");
            return result;
        }

        List<string> headers = SplitCsvLine(lines[0]);

        int timeIndex = headers.FindIndex(h => h.Equals("time", StringComparison.OrdinalIgnoreCase));

        if (timeIndex == -1)
        {
            Debug.LogError("CSV must contain a 'time' column.");
            return result;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            List<string> cells = SplitCsvLine(lines[i]);

            if (cells.Count != headers.Count)
            {
                Debug.LogWarning($"Invalid column count at line {i + 1}. Skipped.");
                continue;
            }

            PatternFrame frame = new PatternFrame();

            frame.time = ParseFloat(cells[timeIndex]);

            for (int j = 0; j < headers.Count; j++)
            {
                if (j == timeIndex)
                    continue;

                string key = headers[j];
                Vector3 angle = ParseVector3(cells[j]);

                frame.angles[key] = angle;
            }

            result.Add(frame);
        }

        return result;
    }

    private static Vector3 ParseVector3(string value)
    {
        value = value.Trim();

        value = value.Trim('"');
        value = value.Trim();
        value = value.Trim('(', ')');

        string[] parts = value.Split(',');

        if (parts.Length != 3)
        {
            Debug.LogWarning($"Invalid Vector3 format: {value}");
            return Vector3.zero;
        }

        float x = ParseFloat(parts[0]);
        float y = ParseFloat(parts[1]);
        float z = ParseFloat(parts[2]);

        return new Vector3(x, y, z);
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture
        );
    }

    private static List<string> SplitCsvLine(string line)
    {
        List<string> result = new List<string>();
        StringBuilder current = new StringBuilder();

        bool insideQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                insideQuote = !insideQuote;
            }
            else if (c == ',' && !insideQuote)
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

[Serializable]
public class PatternFrame
{
    public float time;

    public Dictionary<string, Vector3> angles = new Dictionary<string, Vector3>();

    public Vector3 GetAngle(string angleName)
    {
        if (angles.TryGetValue(angleName, out Vector3 value))
        {
            return value;
        }

        Debug.LogWarning($"Angle not found: {angleName}");
        return Vector3.zero;
    }
}