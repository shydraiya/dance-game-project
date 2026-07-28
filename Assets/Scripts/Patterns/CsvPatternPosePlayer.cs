using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class CsvPatternPosePlayer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Animator _targetAnimator;
    [SerializeField] private bool _disableAnimatorController = true;

    [Header("CSV")]
    [Tooltip("Project 창의 CSV 파일을 여기로 드래그하세요. Unity에서 TextAsset으로 임포트됩니다.")]
    [SerializeField] private TextAsset _csvFile;
    [SerializeField] private bool _loadOnStart = true;

    [Header("Playback")]
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private bool _useGameManagerTime = true;
    [SerializeField] private bool _loop = true;
    [SerializeField] private float _playbackSmoothing = 18.0f;
    [SerializeField] private bool _normalizeBoneVectors = true;

    [Header("Root Movement")]
    [SerializeField] private bool _applyRootPosition = true;
    [SerializeField] private Vector3 _rootPositionScale = Vector3.one;
    [SerializeField] private Vector3 _rootPositionOffset;

    [Header("Status")]
    [SerializeField] private bool _isLoaded;
    [SerializeField] private bool _isPlaying;
    [SerializeField] private int _frameCount;
    [SerializeField] private int _cursor;
    [SerializeField] private string _status = "Not loaded";

    private readonly Dictionary<PatternJoint, BoneSegment> _segments = new Dictionary<PatternJoint, BoneSegment>();
    private PatternFrame[] _frames = Array.Empty<PatternFrame>();
    private Vector3 _initialRootLocalPosition;
    private float _localPlaybackTime;
    private float _lastAppliedTime = -1.0f;

    private sealed class BoneSegment
    {
        public Transform Bone;
        public Vector3 RestDirection;
        public Quaternion RestRotation;
    }

    private void Reset()
    {
        _targetAnimator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (_targetAnimator == null)
        {
            _targetAnimator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (_loadOnStart)
        {
            LoadCsv();
        }

        _isPlaying = _playOnStart && _isLoaded;
    }

    private void LateUpdate()
    {
        if (!_isLoaded || !_isPlaying)
        {
            return;
        }

        float time;
        if (_useGameManagerTime && GameManager.instance != null)
        {
            if (!GameManager.instance.gamePlay)
            {
                return;
            }

            time = GameManager.instance.gameTime;
        }
        else
        {
            _localPlaybackTime += Time.deltaTime;
            time = _localPlaybackTime;
        }

        ApplyAtTime(time);
    }

    public void LoadCsv()
    {
        Load(_csvFile);
    }

    public void Load(TextAsset csvFile)
    {
        _isLoaded = false;
        _isPlaying = false;
        _csvFile = csvFile;
        _frames = ParsePatternCsv(_csvFile).ToArray();
        _frameCount = _frames.Length;
        _cursor = 0;
        _localPlaybackTime = 0.0f;
        _lastAppliedTime = -1.0f;

        if (_targetAnimator == null)
        {
            _status = "Target Animator is missing";
            return;
        }

        if (!_targetAnimator.isHuman)
        {
            _status = "Target Animator is not Humanoid";
            return;
        }

        if (_frames.Length == 0)
        {
            _status = "CSV pattern frames are empty";
            return;
        }

        if (_disableAnimatorController)
        {
            _targetAnimator.runtimeAnimatorController = null;
            _targetAnimator.applyRootMotion = false;
        }

        Array.Sort(_frames, (a, b) => a.time.CompareTo(b.time));
        _initialRootLocalPosition = _targetAnimator.transform.localPosition;
        CacheSegments();

        _isLoaded = _segments.Count > 0;
        _status = _isLoaded
            ? $"Loaded {_frames.Length} frames from {_csvFile.name}"
            : "No humanoid bone segments were found";
    }

    public void Play()
    {
        if (!_isLoaded)
        {
            LoadCsv();
        }

        _isPlaying = _isLoaded;
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Stop()
    {
        _isPlaying = false;
        _localPlaybackTime = 0.0f;
        _cursor = 0;
        _lastAppliedTime = -1.0f;
    }

    public void ApplyAtTime(float time)
    {
        if (!_isLoaded || _frames.Length == 0)
        {
            return;
        }

        float firstTime = _frames[0].time;
        float lastTime = _frames[_frames.Length - 1].time;
        if (_loop && lastTime > firstTime)
        {
            time = firstTime + Mathf.Repeat(time - firstTime, lastTime - firstTime);
        }
        else
        {
            time = Mathf.Clamp(time, firstTime, lastTime);
        }

        if (_lastAppliedTime < 0.0f || time < _lastAppliedTime ||
            _cursor >= _frames.Length - 1 || time < _frames[_cursor].time)
        {
            _cursor = FindFrameBefore(time);
        }
        else
        {
            while (_cursor < _frames.Length - 2 && _frames[_cursor + 1].time <= time)
            {
                _cursor++;
            }
        }

        PatternFrame current = _frames[_cursor];
        PatternFrame next = _frames[Mathf.Min(_cursor + 1, _frames.Length - 1)];
        float duration = Mathf.Max(0.0001f, next.time - current.time);
        float blend = Mathf.Clamp01((time - current.time) / duration);

        ApplyBlendedFrame(current, next, blend);
        _lastAppliedTime = time;
    }

    private int FindFrameBefore(float time)
    {
        int low = 0;
        int high = _frames.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            if (_frames[middle].time <= time)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return Mathf.Clamp(high, 0, Mathf.Max(0, _frames.Length - 2));
    }

    private void ApplyBlendedFrame(PatternFrame a, PatternFrame b, float blend)
    {
        float smoothing = 1.0f - Mathf.Exp(-Mathf.Max(0.0f, _playbackSmoothing) * Time.deltaTime);

        if (_applyRootPosition)
        {
            Vector3 rootOffset = Vector3.Lerp(a.rootPosition, b.rootPosition, blend);
            Vector3 targetPosition = _initialRootLocalPosition +
                Vector3.Scale(rootOffset, _rootPositionScale) + _rootPositionOffset;
            _targetAnimator.transform.localPosition = Vector3.Lerp(
                _targetAnimator.transform.localPosition,
                targetPosition,
                smoothing);
        }

        for (int jointIndex = 0; jointIndex < PatternFrame.JointCount; jointIndex++)
        {
            PatternJoint joint = (PatternJoint)jointIndex;
            Vector3 direction = Vector3.Slerp(a.GetAngle(joint), b.GetAngle(joint), blend);
            ApplyDirection(joint, direction, smoothing);
        }
    }

    private void ApplyDirection(PatternJoint joint, Vector3 localDirection, float smoothing)
    {
        if (!_segments.TryGetValue(joint, out BoneSegment segment) ||
            segment.Bone == null || localDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        if (_normalizeBoneVectors)
        {
            localDirection.Normalize();
        }

        Vector3 worldDirection = _targetAnimator.transform.TransformDirection(localDirection).normalized;
        Quaternion targetRotation = Quaternion.FromToRotation(segment.RestDirection, worldDirection) * segment.RestRotation;
        segment.Bone.rotation = Quaternion.Slerp(segment.Bone.rotation, targetRotation, smoothing);
    }

    private void CacheSegments()
    {
        _segments.Clear();

        AddSegment(PatternJoint.Neck, HumanBodyBones.Neck, HumanBodyBones.Head);
        if (!_segments.ContainsKey(PatternJoint.Neck))
        {
            AddSegment(PatternJoint.Neck, HumanBodyBones.UpperChest, HumanBodyBones.Head);
        }
        if (!_segments.ContainsKey(PatternJoint.Neck))
        {
            AddSegment(PatternJoint.Neck, HumanBodyBones.Chest, HumanBodyBones.Head);
        }

        AddSegment(PatternJoint.ShoulderL, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
        AddSegment(PatternJoint.ShoulderR, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
        AddSegment(PatternJoint.ElbowL, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        AddSegment(PatternJoint.ElbowR, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        AddSegment(PatternJoint.HipL, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
        AddSegment(PatternJoint.HipR, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
        AddSegment(PatternJoint.KneeL, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        AddSegment(PatternJoint.KneeR, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
    }

    private void AddSegment(PatternJoint joint, HumanBodyBones start, HumanBodyBones end)
    {
        Transform startBone = _targetAnimator.GetBoneTransform(start);
        Transform endBone = _targetAnimator.GetBoneTransform(end);
        if (startBone == null || endBone == null)
        {
            return;
        }

        Vector3 restDirection = endBone.position - startBone.position;
        if (restDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        _segments[joint] = new BoneSegment
        {
            Bone = startBone,
            RestDirection = restDirection.normalized,
            RestRotation = startBone.rotation
        };
    }

    private static List<PatternFrame> ParsePatternCsv(TextAsset csvFile)
    {
        List<PatternFrame> frames = new List<PatternFrame>();
        if (csvFile == null)
        {
            Debug.LogError("CSV 파일이 인스펙터에 연결되지 않았습니다.");
            return frames;
        }

        string normalizedText = csvFile.text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalizedText.Split('\n');
        if (lines.Length <= 1)
        {
            Debug.LogWarning("CSV 파일이 비어 있거나 데이터 행이 없습니다.");
            return frames;
        }

        List<string> headers = SplitCsvLine(lines[0]);
        int timeIndex = headers.FindIndex(h => h.Equals("time", StringComparison.OrdinalIgnoreCase));
        if (timeIndex < 0)
        {
            Debug.LogError("CSV에는 'time' 컬럼이 있어야 합니다.");
            return frames;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            List<string> cells = SplitCsvLine(lines[i]);
            if (cells.Count != headers.Count)
            {
                Debug.LogWarning($"CSV {i + 1}번째 줄의 열 개수가 맞지 않아 건너뜁니다.");
                continue;
            }

            PatternFrame frame = new PatternFrame
            {
                time = ParseFloat(cells[timeIndex])
            };

            for (int j = 0; j < headers.Count; j++)
            {
                if (j == timeIndex || headers[j].Equals("is_pattern", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

            frames.Add(frame);
        }

        return frames;
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

    private static Vector3 ParseVector3(string value)
    {
        value = value.Trim();
        value = value.Trim('"');
        value = value.Trim();
        value = value.Trim('(', ')');

        string[] parts = value.Split(',');
        if (parts.Length != 3)
        {
            Debug.LogWarning($"Vector3 형식이 올바르지 않습니다: {value}");
            return Vector3.zero;
        }

        return new Vector3(
            ParseFloat(parts[0]),
            ParseFloat(parts[1]),
            ParseFloat(parts[2]));
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
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
