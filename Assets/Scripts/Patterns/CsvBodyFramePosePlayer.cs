using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class CsvBodyFramePosePlayer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Animator _targetAnimator;
    [SerializeField] private bool _disableAnimatorController = true;

    [Header("CSV")]
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

    [Header("Body Frame Rotation")]
    [SerializeField] private bool _applyRootRotation = true;
    [SerializeField] private bool _applyChestRotation = true;
    [SerializeField] private HumanBodyBones _rootRotationBone = HumanBodyBones.Hips;
    [SerializeField] private HumanBodyBones _chestRotationBone = HumanBodyBones.UpperChest;

    [Header("Status")]
    [SerializeField] private bool _isLoaded;
    [SerializeField] private bool _isPlaying;
    [SerializeField] private int _frameCount;
    [SerializeField] private int _cursor;
    [SerializeField] private string _status = "Not loaded";

    private readonly Dictionary<PatternJoint, BoneSegment> _segments = new Dictionary<PatternJoint, BoneSegment>();
    private BodyFramePatternFrame[] _frames = Array.Empty<BodyFramePatternFrame>();
    private Vector3 _initialRootLocalPosition;
    private Transform _rootRotationTransform;
    private Transform _chestRotationTransform;
    private Quaternion _rootRestRotation;
    private Quaternion _chestRestRotation;
    private BodyFrame _referenceRootFrame;
    private BodyFrame _referenceChestFrame;
    private float _localPlaybackTime;
    private float _lastAppliedTime = -1.0f;

    private sealed class BoneSegment
    {
        public Transform Bone;
        public Vector3 RestDirection;
        public Quaternion RestRotation;
    }

    private sealed class BodyFramePatternFrame
    {
        public float time;
        public Vector3 rootPosition;
        public BodyFrame rootFrame;
        public BodyFrame chestFrame;
        public Vector3[] angles = new Vector3[PatternFrame.JointCount];

        public Vector3 GetAngle(PatternJoint joint)
        {
            return angles[(int)joint];
        }
    }

    private struct BodyFrame
    {
        public Vector3 right;
        public Vector3 up;
        public Vector3 forward;
        public bool isValid;
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
        _frames = ParseBodyFrameCsv(_csvFile).ToArray();
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
        CacheBodyFrameTargets();

        _isLoaded = _segments.Count > 0;
        _status = _isLoaded
            ? $"Loaded {_frames.Length} body-frame rows from {_csvFile.name}"
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

        BodyFramePatternFrame current = _frames[_cursor];
        BodyFramePatternFrame next = _frames[Mathf.Min(_cursor + 1, _frames.Length - 1)];
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

    private void ApplyBlendedFrame(BodyFramePatternFrame a, BodyFramePatternFrame b, float blend)
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

        ApplyBodyFrameRotations(a, b, blend, smoothing);

        for (int jointIndex = 0; jointIndex < PatternFrame.JointCount; jointIndex++)
        {
            PatternJoint joint = (PatternJoint)jointIndex;
            Vector3 direction = Vector3.Slerp(a.GetAngle(joint), b.GetAngle(joint), blend);
            ApplyDirection(joint, direction, smoothing);
        }
    }

    private void ApplyBodyFrameRotations(BodyFramePatternFrame a, BodyFramePatternFrame b, float blend, float smoothing)
    {
        if (_applyRootRotation && _rootRotationTransform != null && a.rootFrame.isValid && b.rootFrame.isValid)
        {
            Quaternion delta = GetFrameDelta(_referenceRootFrame, LerpFrame(a.rootFrame, b.rootFrame, blend));
            _rootRotationTransform.rotation = Quaternion.Slerp(
                _rootRotationTransform.rotation,
                delta * _rootRestRotation,
                smoothing);
        }

        if (_applyChestRotation && _chestRotationTransform != null && a.chestFrame.isValid && b.chestFrame.isValid)
        {
            Quaternion delta = GetFrameDelta(_referenceChestFrame, LerpFrame(a.chestFrame, b.chestFrame, blend));
            _chestRotationTransform.rotation = Quaternion.Slerp(
                _chestRotationTransform.rotation,
                delta * _chestRestRotation,
                smoothing);
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

    private void CacheBodyFrameTargets()
    {
        _rootRotationTransform = _targetAnimator.GetBoneTransform(_rootRotationBone);
        if (_rootRotationTransform != null)
        {
            _rootRestRotation = _rootRotationTransform.rotation;
        }

        _chestRotationTransform = _targetAnimator.GetBoneTransform(_chestRotationBone);
        if (_chestRotationTransform == null && _chestRotationBone == HumanBodyBones.UpperChest)
        {
            _chestRotationTransform = _targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
        }
        if (_chestRotationTransform != null)
        {
            _chestRestRotation = _chestRotationTransform.rotation;
        }

        _referenceRootFrame = _frames[0].rootFrame;
        _referenceChestFrame = _frames[0].chestFrame;
    }

    private Quaternion GetFrameDelta(BodyFrame referenceFrame, BodyFrame currentFrame)
    {
        Quaternion referenceRotation = FrameToWorldRotation(referenceFrame);
        Quaternion currentRotation = FrameToWorldRotation(currentFrame);
        return currentRotation * Quaternion.Inverse(referenceRotation);
    }

    private Quaternion FrameToWorldRotation(BodyFrame frame)
    {
        Vector3 forward = _targetAnimator.transform.TransformDirection(frame.forward).normalized;
        Vector3 up = _targetAnimator.transform.TransformDirection(frame.up).normalized;
        if (forward.sqrMagnitude < 0.000001f || up.sqrMagnitude < 0.000001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(forward, up);
    }

    private static BodyFrame LerpFrame(BodyFrame a, BodyFrame b, float blend)
    {
        return new BodyFrame
        {
            right = Vector3.Slerp(a.right, b.right, blend).normalized,
            up = Vector3.Slerp(a.up, b.up, blend).normalized,
            forward = Vector3.Slerp(a.forward, b.forward, blend).normalized,
            isValid = a.isValid && b.isValid
        };
    }

    private static List<BodyFramePatternFrame> ParseBodyFrameCsv(TextAsset csvFile)
    {
        List<BodyFramePatternFrame> frames = new List<BodyFramePatternFrame>();
        if (csvFile == null)
        {
            Debug.LogError("CSV file is not assigned.");
            return frames;
        }

        string normalizedText = csvFile.text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalizedText.Split('\n');
        if (lines.Length <= 1)
        {
            Debug.LogWarning("CSV file is empty or has no data rows.");
            return frames;
        }

        List<string> headers = SplitCsvLine(lines[0]);
        int timeIndex = headers.FindIndex(h => h.Equals("time", StringComparison.OrdinalIgnoreCase));
        if (timeIndex < 0)
        {
            Debug.LogError("CSV must contain a 'time' column.");
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
                Debug.LogWarning($"CSV line {i + 1} has an invalid column count. Skipped.");
                continue;
            }

            BodyFramePatternFrame frame = new BodyFramePatternFrame
            {
                time = ParseFloat(cells[timeIndex])
            };

            bool hasRootRight = false;
            bool hasRootUp = false;
            bool hasRootForward = false;
            bool hasChestRight = false;
            bool hasChestUp = false;
            bool hasChestForward = false;

            for (int j = 0; j < headers.Count; j++)
            {
                string header = headers[j];
                if (j == timeIndex || header.Equals("is_pattern", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Vector3 value = ParseVector3(cells[j]);
                if (header.Equals("root_position", StringComparison.OrdinalIgnoreCase))
                {
                    frame.rootPosition = value;
                    continue;
                }

                if (header.Equals("root_right", StringComparison.OrdinalIgnoreCase))
                {
                    frame.rootFrame.right = value;
                    hasRootRight = true;
                    continue;
                }

                if (header.Equals("root_up", StringComparison.OrdinalIgnoreCase))
                {
                    frame.rootFrame.up = value;
                    hasRootUp = true;
                    continue;
                }

                if (header.Equals("root_forward", StringComparison.OrdinalIgnoreCase))
                {
                    frame.rootFrame.forward = value;
                    hasRootForward = true;
                    continue;
                }

                if (header.Equals("chest_right", StringComparison.OrdinalIgnoreCase))
                {
                    frame.chestFrame.right = value;
                    hasChestRight = true;
                    continue;
                }

                if (header.Equals("chest_up", StringComparison.OrdinalIgnoreCase))
                {
                    frame.chestFrame.up = value;
                    hasChestUp = true;
                    continue;
                }

                if (header.Equals("chest_forward", StringComparison.OrdinalIgnoreCase))
                {
                    frame.chestFrame.forward = value;
                    hasChestForward = true;
                    continue;
                }

                int jointId = GetJointId(header);
                if (jointId >= 0)
                {
                    frame.angles[jointId] = value;
                }
            }

            frame.rootFrame.isValid = hasRootRight && hasRootUp && hasRootForward;
            frame.chestFrame.isValid = hasChestRight && hasChestUp && hasChestForward;
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
            Debug.LogWarning($"Invalid Vector3 format: {value}");
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
