using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

//사용 방법
//Framse[i] 여기에 i번째 패턴 데이터가 들어있음
//Frames[i].time = i번째 패턴 데이터의 시간 (t = 1 : 1초에서의 패턴 디자인)
//Frames[i].GetAngle("이름") = 이름의 요구 각도 출력

//Patterns[i]에는 csv에서 is_pattern = 1인 row만 들어감
//Patterns[i]는 Frames[i]와 동일하게 사용 가능

//이름 : root_position  neck	shoulder_l	shoulder_r	elbow_l	elbow_r	hip_l	hip_r	knee_l	knee_r
//패턴 파일은 Assets/Patterns 내부에 존재
//지금은 fileName = "pattern_sample.csv" 요렇게 넣었는데, 나중에 바꿀 필요가 있음

public class PatternLoader : MonoBehaviour
{
    [SerializeField] private string fileName = "pattern_sample.csv";

    public List<PatternFrame> Frames { get; private set; }   = new List<PatternFrame>();
    public List<PatternFrame> Patterns { get; private set; } = new List<PatternFrame>();
    public string FileName => fileName;

    private void Start()
    {
        LoadSelectedOrDefaultPattern();

        Debug.Log($"Loaded Pattern Frames: {Frames.Count} ({fileName})");
        Debug.Log($"Loaded Required Patterns: {Patterns.Count} ({fileName})");

        if (Frames.Count > 0)
        {
            //Debug.Log($"First frame time: {Frames[0].time}");
            //Debug.Log($"Neck angle: {Frames[0].GetAngle(PatternJoint.Neck)}");
        }

        if (Patterns.Count > 0)
        {
            Debug.Log($"First required pattern time: {Patterns[0].time}");
        }
    }

    public void LoadSelectedOrDefaultPattern()
    {
        SongSessionController session = SongSessionController.Instance;
        if (session != null && session.HasSelectedSong)
        {
            SongData song = session.SelectedSong;
            if (!string.IsNullOrWhiteSpace(song.patternPath))
            {
                fileName = song.patternPath;
            }

            if (session.HasSelectedPattern)
            {
                Frames = new List<PatternFrame>(session.SelectedPatternFrames);

                List<PatternFrame> loadedPatterns;
                LoadPattern(fileName, out loadedPatterns);
                Patterns = loadedPatterns;

                return;
            }
        }
        List<PatternFrame> patterns;
        Frames = LoadPattern(fileName, out patterns);
        Patterns = patterns;
    }

    public static List<PatternFrame> LoadPattern(string fileName)
    {
        List<PatternFrame> ignoredPatterns;
        return LoadPattern(fileName, out ignoredPatterns);
    }

    public static List<PatternFrame> LoadPattern(string fileName, out List<PatternFrame> patterns)
    {
        List<PatternFrame> result = new List<PatternFrame>();

        patterns = new List<PatternFrame>();

        string path = Path.Combine(Application.dataPath, "Patterns", fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"Pattern file not found: {path}");
            return result;
        }

        string[] lines;
        try
        {
            lines = ReadAllLinesShared(path);
        }
        catch (IOException exception)
        {
            Debug.LogError($"Pattern file could not be read: {path}\n{exception.Message}");
            return result;
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogError($"Pattern file access denied: {path}\n{exception.Message}");
            return result;
        }

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

        int isPatternIndex = headers.FindIndex(
            h => h.Equals(
                "is_pattern",
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (isPatternIndex == -1)
        {
            Debug.LogWarning(
                "CSV does not contain an 'is_pattern' column. " +
                "Patterns will be empty."
            );
        }


        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            List<string> cells = SplitCsvLine(lines[i]);

            if (isPatternIndex == headers.Count - 1 && cells.Count == headers.Count - 1)
            {
                cells.Add(string.Empty);
            }

            if (cells.Count != headers.Count)
            {
                Debug.LogWarning($"Invalid column count at line {i + 1}. Skipped.");
                continue;
            }

            PatternFrame frame = new PatternFrame();

            frame.time = ParseFloat(cells[timeIndex]);
            for (int j = 0; j < headers.Count; j++)
            {
                if (j == timeIndex || j == isPatternIndex)
                    continue;

                Vector3 value = ParseVector3(cells[j]);
                if (headers[j].Equals("root_position", StringComparison.OrdinalIgnoreCase))
                {
                    frame.rootPosition = value;
                    continue;
                }

                int jointId = GetJointId(headers[j]);
                if (jointId >= 0)
                {
                    frame.angles[jointId] = value;
                }
            }

            result.Add(frame);

            if (isPatternIndex >= 0 && IsPatternRow(cells[isPatternIndex]))
            {
                patterns.Add(frame);
            }
        }

        return result;
    }

    private static string[] ReadAllLinesShared(string path)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
        {
            List<string> lines = new List<string>();
            while (!reader.EndOfStream)
            {
                lines.Add(reader.ReadLine());
            }

            return lines.ToArray();
        }
    }

    private static int GetJointId(string header)
    {
        switch (header.Trim().ToLowerInvariant())
        {
            case "neck": return (int)PatternJoint.Neck;
            case "shoulder_l": return (int)PatternJoint.ShoulderL;
            case "shoulder_r": return (int)PatternJoint.ShoulderR;
            case "elbow_l": return (int)PatternJoint.ElbowL;
            case "elbow_r": return (int)PatternJoint.ElbowR;
            case "hip_l": return (int)PatternJoint.HipL;
            case "hip_r": return (int)PatternJoint.HipR;
            case "knee_l": return (int)PatternJoint.KneeL;
            case "knee_r": return (int)PatternJoint.KneeR;
            default: return -1;
        }
    }

    private static bool IsPatternRow(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        value = value.Trim('"');
        value = value.Trim();

        return value == "1";
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
