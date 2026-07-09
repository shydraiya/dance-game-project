using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

public class PoseNoteReader : MonoBehaviour
{
  private const int LandmarkCount = 33;
  private static readonly string[] PatternJointKeys =
  {
    "neck",
    "shoulder_l",
    "shoulder_r",
    "elbow_l",
    "elbow_r",
    "hip_l",
    "hip_r",
    "knee_l",
    "knee_r"
  };

  private enum PoseIndex
  {
    Nose = 0,
    LeftShoulder = 11,
    RightShoulder = 12,
    LeftElbow = 13,
    RightElbow = 14,
    LeftWrist = 15,
    RightWrist = 16,
    LeftHip = 23,
    RightHip = 24,
    LeftKnee = 25,
    RightKnee = 26,
    LeftAnkle = 27,
    RightAnkle = 28
  }

  public enum JudgeRank
  {
    None,
    Miss,
    Bad,
    Good,
    Perfect
  }

  [Serializable]
  public struct JudgeResult
  {
    public JudgeRank rank;
    public float score;
    public float averageAngle;
    public float noteTime;
    public int comparedParts;
  }

  [Header("Sources")]
  [SerializeField] private PatternLoader _patternLoader;
  [SerializeField] private PoseLandmarkerRunner _poseRunner;
  [SerializeField] private WebCamPoseLandmarkerRunner _webCamPoseRunner;
  [SerializeField] private bool _autoFindSources = true;

  [Header("Timing Spare")]
  [SerializeField] private int _forwardSpare = 10;
  [SerializeField] private int _backwardSpare = 10;
  [SerializeField] private float _frameTime = 1.0f / 60.0f;

  [Header("Judgement")]
  [SerializeField] private float _minimumVisibility = 0.35f;
  [SerializeField] private float _perfectAngle = 15.0f;
  [SerializeField] private float _goodAngle = 30.0f;
  [SerializeField] private float _badAngle = 45.0f;
  [SerializeField] private bool _mirrorHorizontally = true;
  [SerializeField] private bool _logJudgement;

  [Header("Debug")]
  [SerializeField] private JudgeRank _lastJudge = JudgeRank.None;
  [SerializeField] private float _lastScore;
  [SerializeField] private float _lastAverageAngle;
  [SerializeField] private float _lastMatchedNoteTime = -1.0f;
  [SerializeField] private int _lastComparedParts;

  private readonly object _poseLock = new();
  private readonly Vector3[] _latestPose = new Vector3[LandmarkCount];
  private readonly float[] _latestVisibility = new float[LandmarkCount];
  private readonly Dictionary<string, Vector3> _currentPoseAngles = new();

  private bool _hasPose;

  public JudgeResult LastResult { get; private set; }
  public event Action<JudgeResult> JudgementUpdated;

  private void Awake()
  {
    ResolveSources();
  }

  private void OnEnable()
  {
    ResolveSources();

    if (_poseRunner != null)
    {
      _poseRunner.PoseLandmarksUpdated += OnPoseLandmarksUpdated;
    }

    if (_webCamPoseRunner != null)
    {
      _webCamPoseRunner.PoseLandmarksUpdated += OnPoseLandmarksUpdated;
    }
  }

  private void OnDisable()
  {
    if (_poseRunner != null)
    {
      _poseRunner.PoseLandmarksUpdated -= OnPoseLandmarksUpdated;
    }

    if (_webCamPoseRunner != null)
    {
      _webCamPoseRunner.PoseLandmarksUpdated -= OnPoseLandmarksUpdated;
    }
  }

  private void Update()
  {
    if (!GameManager.instance || !GameManager.instance.gamePlay)
    {
      return;
    }

    if (!_hasPose || _patternLoader == null || _patternLoader.Frames == null || _patternLoader.Frames.Count == 0)
    {
      return;
    }

    var result = JudgeAtTime(GameManager.instance.gameTime);
    LastResult = result;
    _lastJudge = result.rank;
    _lastScore = result.score;
    _lastAverageAngle = result.averageAngle;
    _lastMatchedNoteTime = result.noteTime;
    _lastComparedParts = result.comparedParts;
    JudgementUpdated?.Invoke(result);

    if (_logJudgement && result.rank != JudgeRank.None)
    {
      Debug.Log($"{nameof(PoseNoteReader)} {result.rank} score={result.score:0.000}, averageAngle={result.averageAngle:0.0}, noteTime={result.noteTime:0.000}");
    }
  }

  public JudgeResult JudgeAtTime(float currentTime)
  {
    if (!TryBuildCurrentPoseAngles())
    {
      return new JudgeResult { rank = JudgeRank.None, noteTime = -1.0f };
    }

    var fromTime = currentTime - Mathf.Max(0, _backwardSpare) * _frameTime;
    var toTime = currentTime + Mathf.Max(0, _forwardSpare) * _frameTime;
    var bestResult = new JudgeResult { rank = JudgeRank.None, score = -1.0f, noteTime = -1.0f };

    foreach (var frame in _patternLoader.Frames)
    {
      if (frame.time < fromTime || frame.time > toTime)
      {
        continue;
      }

      var result = CompareWithPatternFrame(frame);
      if (result.score > bestResult.score)
      {
        bestResult = result;
      }
    }

    return bestResult.score < 0.0f ? new JudgeResult { rank = JudgeRank.None, noteTime = -1.0f } : bestResult;
  }

  private void OnPoseLandmarksUpdated(PoseLandmarkerResult result)
  {
    var worldPose = result.poseWorldLandmarks;
    if (worldPose != null && worldPose.Count > 0 && worldPose[0].landmarks != null && worldPose[0].landmarks.Count >= LandmarkCount)
    {
      CopyWorldPose(worldPose[0].landmarks);
      return;
    }

    var normalizedPose = result.poseLandmarks;
    if (normalizedPose != null && normalizedPose.Count > 0 && normalizedPose[0].landmarks != null && normalizedPose[0].landmarks.Count >= LandmarkCount)
    {
      CopyNormalizedPose(normalizedPose[0].landmarks);
      return;
    }

    lock (_poseLock)
    {
      _hasPose = false;
    }
  }

  private void CopyWorldPose(IReadOnlyList<Landmark> landmarks)
  {
    lock (_poseLock)
    {
      for (var i = 0; i < LandmarkCount; i++)
      {
        var landmark = landmarks[GetSourceLandmarkIndex(i)];
        _latestPose[i] = new Vector3(landmark.x, landmark.y, landmark.z);
        _latestVisibility[i] = landmark.visibility ?? 1.0f;
      }

      _hasPose = true;
    }
  }

  private void CopyNormalizedPose(IReadOnlyList<NormalizedLandmark> landmarks)
  {
    lock (_poseLock)
    {
      for (var i = 0; i < LandmarkCount; i++)
      {
        var landmark = landmarks[GetSourceLandmarkIndex(i)];
        _latestPose[i] = new Vector3(landmark.x - 0.5f, landmark.y - 0.5f, landmark.z);
        _latestVisibility[i] = landmark.visibility ?? 1.0f;
      }

      _hasPose = true;
    }
  }

  private bool TryBuildCurrentPoseAngles()
  {
    Vector3[] pose;
    float[] visibility;

    lock (_poseLock)
    {
      if (!_hasPose)
      {
        return false;
      }

      pose = (Vector3[])_latestPose.Clone();
      visibility = (float[])_latestVisibility.Clone();
    }

    _currentPoseAngles.Clear();
    TryAddDirection("neck", pose, visibility, PoseIndex.LeftHip, PoseIndex.RightHip, PoseIndex.LeftShoulder, PoseIndex.RightShoulder);
    TryAddDirection("shoulder_l", pose, visibility, PoseIndex.LeftShoulder, PoseIndex.LeftElbow);
    TryAddDirection("shoulder_r", pose, visibility, PoseIndex.RightShoulder, PoseIndex.RightElbow);
    TryAddDirection("elbow_l", pose, visibility, PoseIndex.LeftElbow, PoseIndex.LeftWrist);
    TryAddDirection("elbow_r", pose, visibility, PoseIndex.RightElbow, PoseIndex.RightWrist);
    TryAddDirection("hip_l", pose, visibility, PoseIndex.LeftHip, PoseIndex.LeftKnee);
    TryAddDirection("hip_r", pose, visibility, PoseIndex.RightHip, PoseIndex.RightKnee);
    TryAddDirection("knee_l", pose, visibility, PoseIndex.LeftKnee, PoseIndex.LeftAnkle);
    TryAddDirection("knee_r", pose, visibility, PoseIndex.RightKnee, PoseIndex.RightAnkle);

    return _currentPoseAngles.Count > 0;
  }

  private JudgeResult CompareWithPatternFrame(PatternFrame frame)
  {
    var totalAngle = 0.0f;
    var comparedParts = 0;
    var jointCount = Mathf.Min(PatternFrame.JointCount, PatternJointKeys.Length);

    for (var jointId = 0; jointId < jointCount; jointId++)
    {
      var jointKey = PatternJointKeys[jointId];
      if (!_currentPoseAngles.TryGetValue(jointKey, out var currentDirection))
      {
        continue;
      }

      var targetDirection = frame.GetAngle(jointId);
      if (targetDirection.sqrMagnitude < 0.000001f || currentDirection.sqrMagnitude < 0.000001f)
      {
        continue;
      }

      totalAngle += Vector3.Angle(currentDirection.normalized, targetDirection.normalized);
      comparedParts++;
    }

    if (comparedParts == 0)
    {
      return new JudgeResult { rank = JudgeRank.None, score = -1.0f, noteTime = frame.time };
    }

    var averageAngle = totalAngle / comparedParts;
    var score = Mathf.Clamp01(1.0f - averageAngle / 180.0f);

    return new JudgeResult
    {
      rank = GetJudgeRank(averageAngle),
      score = score,
      averageAngle = averageAngle,
      noteTime = frame.time,
      comparedParts = comparedParts
    };
  }

  private void TryAddDirection(string key, Vector3[] pose, float[] visibility, PoseIndex start, PoseIndex end)
  {
    if (!IsVisible(visibility, start) || !IsVisible(visibility, end))
    {
      return;
    }

    var direction = pose[(int)end] - pose[(int)start];
    if (direction.sqrMagnitude < 0.000001f)
    {
      return;
    }

    _currentPoseAngles[key] = direction.normalized;
  }

  private void TryAddDirection(string key, Vector3[] pose, float[] visibility, PoseIndex firstStart, PoseIndex secondStart, PoseIndex firstEnd, PoseIndex secondEnd)
  {
    if (!IsVisible(visibility, firstStart) || !IsVisible(visibility, secondStart) || !IsVisible(visibility, firstEnd) || !IsVisible(visibility, secondEnd))
    {
      return;
    }

    var startCenter = (pose[(int)firstStart] + pose[(int)secondStart]) * 0.5f;
    var endCenter = (pose[(int)firstEnd] + pose[(int)secondEnd]) * 0.5f;
    var direction = endCenter - startCenter;
    if (direction.sqrMagnitude < 0.000001f)
    {
      return;
    }

    _currentPoseAngles[key] = direction.normalized;
  }

  private bool IsVisible(float[] visibility, PoseIndex index)
  {
    return visibility[(int)index] >= _minimumVisibility;
  }

  private JudgeRank GetJudgeRank(float averageAngle)
  {
    if (averageAngle <= _perfectAngle)
    {
      return JudgeRank.Perfect;
    }

    if (averageAngle <= _goodAngle)
    {
      return JudgeRank.Good;
    }

    if (averageAngle <= _badAngle)
    {
      return JudgeRank.Bad;
    }

    return JudgeRank.Miss;
  }

  private int GetSourceLandmarkIndex(int targetIndex)
  {
    if (!_mirrorHorizontally)
    {
      return targetIndex;
    }

    return targetIndex switch
    {
      11 => 12,
      12 => 11,
      13 => 14,
      14 => 13,
      15 => 16,
      16 => 15,
      23 => 24,
      24 => 23,
      25 => 26,
      26 => 25,
      27 => 28,
      28 => 27,
      _ => targetIndex
    };
  }

  private void ResolveSources()
  {
    if (!_autoFindSources)
    {
      return;
    }

    if (_patternLoader == null)
    {
      _patternLoader = FindAnyObjectByType<PatternLoader>();
    }

    if (_poseRunner == null)
    {
      _poseRunner = FindAnyObjectByType<PoseLandmarkerRunner>();
    }

    if (_webCamPoseRunner == null)
    {
      _webCamPoseRunner = FindAnyObjectByType<WebCamPoseLandmarkerRunner>();
    }
  }
}
