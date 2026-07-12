using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;
//함수이름이랑 서식 전부 추천대로 고쳐줘 함수 기능에 따라
public class PoseNoteReader : MonoBehaviour
{
  private const int LandmarkCount = 33;

  /*
   * Module: PoseNoteReader
   *
   * Pattern Test scene에서 사용자의 MediaPipe 포즈와 CSV 패턴 프레임을 비교해
   * Perfect/Good/Bad/Miss 판정을 만드는 모듈입니다.
   *
   * 입력:
   * - CSV의 PatternFrame 
   * - PoseLandmarkerRunner 또는 WebCamPoseLandmarkerRunner
   *
   * 용빈수정 Vector3[] 배열사용
   * CSV 순서와 PatternJoint enum 순서를 맞춰 jointId로 비교
   */
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

  /*
  public enum JudgeRank
  {
    None,
    Miss,
    Bad,
    Good,
    Perfect
  }//판정들 맘에 안들면 바꿔도딤
  */
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
  [SerializeField] public JudgeUI judgeUI;

  [Header("Debug")]
  [SerializeField] private JudgeRank _lastJudge = JudgeRank.None;
  [SerializeField] private float _lastScore;
  [SerializeField] private float _lastAverageAngle;
  [SerializeField] private float _lastMatchedNoteTime = -1.0f;
  [SerializeField] private int _lastComparedParts;

  //주의!!!!! 중간 발표용으로 만든 임시용 코드임!!!!!!!!!!!!!!
  //나중에 리팩토링 할 때 싹 날려야 함!!!!!!!!!!!!!!!!!!!!
  [SerializeField] private float _judgeInterval = 2.0f;

  private float _nextJudgeTime = 2.0f;
  private float _lastObservedGameTime = float.NegativeInfinity; 

  //!!!!!!!!!!!!!여기까지!!!!!!!!!!!!!!!!!!!!!

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

    else if (_webCamPoseRunner != null)
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

    else if (_webCamPoseRunner != null)
    {
      _webCamPoseRunner.PoseLandmarksUpdated -= OnPoseLandmarksUpdated;
    }
  }
  //발표용 임시 update 함수임!!!!!!!!!!!!
  //나중에 꼭 지울 것!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
  private void Update()
  {
      if (GameManager.instance == null || !GameManager.instance.gamePlay)
      {
          return;
      }

      if (!_hasPose ||
          _patternLoader == null ||
          _patternLoader.Frames == null ||
          _patternLoader.Frames.Count == 0)
      {
          return;
      }

      float gameTime = GameManager.instance.gameTime;

      // 게임 시간이 처음으로 되돌아간 경우 타이머 초기화
      if (gameTime < _lastObservedGameTime)
      {
          _nextJudgeTime = gameTime + _judgeInterval;
      }

      _lastObservedGameTime = gameTime;

      // 다음 판정 시간이 아직 되지 않았으면 종료
      if (gameTime < _nextJudgeTime)
      {
          return;
      }

      // 프레임 드롭 등으로 시간을 크게 건너뛰어도
      // 다음 판정 시간이 현재 시간보다 뒤에 있도록 갱신
      do
      {
          _nextJudgeTime += _judgeInterval;
      }
      while (_nextJudgeTime <= gameTime);

      JudgeResult result = JudgeAtTime(gameTime);

      LastResult = result;
      _lastJudge = result.rank;
      _lastScore = result.score;
      _lastAverageAngle = result.averageAngle;
      _lastMatchedNoteTime = result.noteTime;
      _lastComparedParts = result.comparedParts;

      JudgementUpdated?.Invoke(result);

      if (result.rank == JudgeRank.None)
      {
          return;
      }

      if (judgeUI != null)
      {
          judgeUI.ShowJudge(result.rank);
      }
      else
      {
          Debug.LogError(
              $"{nameof(PoseNoteReader)}: JudgeUI가 연결되지 않았습니다.",
              this
          );
      }

      Debug.Log(
          $"{nameof(PoseNoteReader)} {result.rank} " +
          $"score={result.score:0.000}, " +
          $"averageAngle={result.averageAngle:0.0}, " +
          $"noteTime={result.noteTime:0.000}"
      );
  }

  /* 나중에 중간 발표 끝나면 요 코드로 되돌려야함!!!!!!!!!!!!!!!!!!!!!!!!!!!!
  private void Update()
  {
    // Pattern Test의 게임 시간이 흐르는 동안만 현재 포즈와 패턴을 비교//타임 맞게 썻는지 체크 한번만 여기 타임 이해도 자신업스
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

    if (result.rank != JudgeRank.None)
    {
      judgeUI.ShowJudge(result.rank);
      Debug.Log($"{nameof(PoseNoteReader)} {result.rank} score={result.score:0.000}, averageAngle={result.averageAngle:0.0}, noteTime={result.noteTime:0.000}");
    }
  }
  */

  public JudgeResult JudgeAtTime(float currentTime)
  {
    if (!TryBuildCurrentPoseAngles())
    {
      return new JudgeResult { rank = JudgeRank.None, noteTime = -1.0f };
    }

    // 현재 시간 근처의 CSV 프레임 비교
    // forward/backward spare는 판정 허용 프레임 범위
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
    // MediaPipe가 world landmark를 주면 우선 사용,
    // 없으면 normalized landmark를 중앙 기준 좌표로 변환해 사용//차피 csv 로 변환할거면 상관없음
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

    // 현재 landmark 좌표를 CSV와 같은 9개 관절 방향 벡터로 변환
    // PatternJointKeys / PatternFrame.angles
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

    // 변경된 CSV 구조Vector3[]-대용빈방식
    //  Dictionary -> jointId로
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
    // 관절  방향 예: shoulder_l = left shoulder -> left elbow.
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
    // neck은 hip center -> shoulder center 방향으로 계산//여기가 좀 그럼...
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
    // 카메라/화면 좌우가 반대로 들어오는 경우 MediaPipe left/right 인덱스를 교환//이거 수정됬으면(대용빈) 안해도 됨 추후 수정
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
