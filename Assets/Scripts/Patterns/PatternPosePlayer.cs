using System;
using System.Collections.Generic;
using UnityEngine;

public class PatternPosePlayer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Animator _targetAnimator;
    [SerializeField] private bool _disableAnimatorController = true;

    [Header("Playback")]
    [SerializeField] private bool _playFromSongSession = true;
    [SerializeField] private bool _loop;
    [SerializeField] private float _playbackSmoothing = 18.0f;
    [SerializeField] private bool _normalizeBoneVectors = true;

    [Header("Root Movement")]
    [SerializeField] private bool _applyRootPosition = true;
    [SerializeField] private Vector3 _rootPositionScale = Vector3.one;
    [SerializeField] private Vector3 _rootPositionOffset;

    [Header("Status")]
    [SerializeField] private bool _isLoaded;
    [SerializeField] private int _frameCount;
    [SerializeField] private int _cursor;
    [SerializeField] private string _status = "Not loaded";

    private readonly Dictionary<PatternJoint, BoneSegment> _segments = new Dictionary<PatternJoint, BoneSegment>();
    private PatternFrame[] _frames = Array.Empty<PatternFrame>();
    private Vector3 _initialRootLocalPosition;
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
        if (!_playFromSongSession)
        {
            return;
        }

        SongSessionController session = SongSessionController.Instance;
        if (session == null || !session.HasSelectedPattern)
        {
            _status = "Selected song pattern is missing";
            return;
        }

        Load(session.SelectedPatternFrames);
    }

    private void LateUpdate()
    {
        if (!_isLoaded || GameManager.instance == null || !GameManager.instance.gamePlay)
        {
            return;
        }

        ApplyAtTime(GameManager.instance.gameTime);
    }

    public void Load(PatternFrame[] frames)
    {
        _isLoaded = false;
        _frames = frames != null ? (PatternFrame[])frames.Clone() : Array.Empty<PatternFrame>();
        _frameCount = _frames.Length;
        _cursor = 0;
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
            _status = "Pattern frames are empty";
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
            ? $"Loaded {_frames.Length} pattern frames"
            : "No humanoid bone segments were found";
    }

    public void ReloadFromSongSession()
    {
        SongSessionController session = SongSessionController.Instance;
        if (session == null || !session.HasSelectedPattern)
        {
            _status = "Selected song pattern is missing";
            return;
        }

        Load(session.SelectedPatternFrames);
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
}
