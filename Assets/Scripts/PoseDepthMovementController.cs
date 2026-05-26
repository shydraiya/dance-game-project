using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

public class PoseDepthMovementController : MonoBehaviour
{
  private const int LeftShoulder = 11;
  private const int RightShoulder = 12;
  private const int LeftHip = 23;
  private const int RightHip = 24;
  private const int RequiredLandmarkCount = 25;

  [SerializeField] private PoseLandmarkerRunner _runner;
  [SerializeField] private Transform _targetRoot;
  [SerializeField] private bool _autoFindRunner = true;
  [SerializeField] private bool _autoTargetPoseAnnotation = true;
  [SerializeField] private bool _moveInLocalSpace = true;
  [SerializeField] private bool _invertDirection = false;
  [SerializeField] private float _calibrationSeconds = 0.75f;
  [SerializeField] private float _visibilityThreshold = 0.5f;
  [SerializeField] private float _widthDepthMultiplier = 4.0f;
  [SerializeField] private float _landmarkZDepthMultiplier = 8.0f;
  [SerializeField, Range(0.0f, 1.0f)] private float _baseWidthWeight = 0.45f;
  [SerializeField, Range(0.0f, 1.0f)] private float _baseLandmarkZWeight = 0.55f;
  [SerializeField] private float _turnSensitivity = 6.0f;
  [SerializeField] private float _deadZone = 0.03f;
  [SerializeField] private float _maxZOffset = 3.0f;
  [SerializeField] private float _smoothTime = 0.12f;
  [SerializeField] private KeyCode _recalibrateKey = KeyCode.R;

  private readonly object _landmarkLock = new();
  private readonly List<NormalizedLandmark> _latestLandmarks = new(RequiredLandmarkCount);
  private Vector3 _startLocalPosition;
  private Vector3 _startWorldPosition;
  private float _baseBodyWidth;
  private float _baseHipZ;
  private float _calibrationBodyWidthSum;
  private float _calibrationHipZSum;
  private float _calibrationStartedAt;
  private int _calibrationSamples;
  private float _zVelocity;
  private float _currentZOffset;
  private bool _hasLandmarks;
  private bool _isCalibrating = true;
  private bool _isCalibrated;

  private void Awake()
  {
    ResolveTargetRoot();
    CaptureStartPosition();
  }

  private void OnEnable()
  {
    if (_runner == null && _autoFindRunner)
    {
      _runner = FindAnyObjectByType<PoseLandmarkerRunner>();
    }

    if (_runner != null)
    {
      _runner.PoseLandmarksUpdated += OnPoseLandmarksUpdated;
    }

    ResolveTargetRoot();
    CaptureStartPosition();
    BeginCalibration();
  }

  private void OnDisable()
  {
    if (_runner != null)
    {
      _runner.PoseLandmarksUpdated -= OnPoseLandmarksUpdated;
    }
  }

  private void Update()
  {
    if (Input.GetKeyDown(_recalibrateKey))
    {
      BeginCalibration();
    }

    if (!TryCopyLatestLandmarks(out var landmarks))
    {
      ApplyZOffset(0.0f);
      return;
    }

    var bodyWidth = GetBodyWidth(landmarks);
    var hipZ = GetAverageZ(landmarks[LeftHip], landmarks[RightHip]);

    if (_isCalibrating)
    {
      AddCalibrationSample(bodyWidth, hipZ);
      return;
    }

    if (!_isCalibrated || _baseBodyWidth <= Mathf.Epsilon)
    {
      return;
    }

    var targetZOffset = CalculateTargetZOffset(landmarks, bodyWidth, hipZ);
    _currentZOffset = Mathf.SmoothDamp(_currentZOffset, targetZOffset, ref _zVelocity, _smoothTime);
    ApplyZOffset(_currentZOffset);
  }

  public void BeginCalibration()
  {
    if (_targetRoot == null)
    {
      ResolveTargetRoot();
      CaptureStartPosition();
    }

    _calibrationStartedAt = Time.time;
    _calibrationSamples = 0;
    _calibrationBodyWidthSum = 0.0f;
    _calibrationHipZSum = 0.0f;
    _zVelocity = 0.0f;
    _currentZOffset = 0.0f;
    _isCalibrating = true;
    _isCalibrated = false;
    ApplyZOffset(0.0f);
  }

  private void OnPoseLandmarksUpdated(PoseLandmarkerResult result)
  {
    var poseLandmarks = result.poseLandmarks;
    if (poseLandmarks == null || poseLandmarks.Count == 0 || poseLandmarks[0].landmarks == null)
    {
      lock (_landmarkLock)
      {
        _hasLandmarks = false;
      }
      return;
    }

    var landmarks = poseLandmarks[0].landmarks;
    lock (_landmarkLock)
    {
      _latestLandmarks.Clear();
      _latestLandmarks.AddRange(landmarks);
      _hasLandmarks = _latestLandmarks.Count >= RequiredLandmarkCount;
    }
  }

  private bool TryCopyLatestLandmarks(out List<NormalizedLandmark> landmarks)
  {
    landmarks = null;
    lock (_landmarkLock)
    {
      if (!_hasLandmarks || _latestLandmarks.Count < RequiredLandmarkCount)
      {
        return false;
      }

      landmarks = new List<NormalizedLandmark>(_latestLandmarks);
    }

    return HasReliableTorso(landmarks);
  }

  private void AddCalibrationSample(float bodyWidth, float hipZ)
  {
    _calibrationBodyWidthSum += bodyWidth;
    _calibrationHipZSum += hipZ;
    _calibrationSamples++;

    if (Time.time - _calibrationStartedAt < _calibrationSeconds)
    {
      return;
    }

    _baseBodyWidth = _calibrationBodyWidthSum / _calibrationSamples;
    _baseHipZ = _calibrationHipZSum / _calibrationSamples;
    _isCalibrating = false;
    _isCalibrated = _baseBodyWidth > Mathf.Epsilon;
  }

  private float CalculateTargetZOffset(IReadOnlyList<NormalizedLandmark> landmarks, float bodyWidth, float hipZ)
  {
    var widthRatio = bodyWidth / _baseBodyWidth;
    var widthDepth = (widthRatio - 1.0f) * _widthDepthMultiplier;
    var landmarkZDepth = (_baseHipZ - hipZ) * _landmarkZDepthMultiplier;
    var turnAmount = GetTurnAmount(landmarks);
    var widthReliability = Mathf.Clamp01(1.0f - turnAmount * _turnSensitivity);
    var widthWeight = _baseWidthWeight * widthReliability;
    var zWeight = _baseLandmarkZWeight + _baseWidthWeight * (1.0f - widthReliability);
    var totalWeight = Mathf.Max(widthWeight + zWeight, Mathf.Epsilon);
    var zOffset = (widthDepth * widthWeight + landmarkZDepth * zWeight) / totalWeight;

    if (_invertDirection)
    {
      zOffset = -zOffset;
    }

    if (Mathf.Abs(zOffset) < _deadZone)
    {
      zOffset = 0.0f;
    }

    return Mathf.Clamp(zOffset, -_maxZOffset, _maxZOffset);
  }

  private void ApplyZOffset(float zOffset)
  {
    if (_targetRoot == null)
    {
      return;
    }

    if (_moveInLocalSpace)
    {
      var position = _startLocalPosition;
      position.z += zOffset;
      _targetRoot.localPosition = position;
      return;
    }

    var worldPosition = _startWorldPosition;
    worldPosition.z += zOffset;
    _targetRoot.position = worldPosition;
  }

  private void ResolveTargetRoot()
  {
    if (_targetRoot != null || !_autoTargetPoseAnnotation)
    {
      return;
    }

    var annotationController = FindAnyObjectByType<PoseLandmarkerResultAnnotationController>();
    if (annotationController != null)
    {
      _targetRoot = annotationController.transform;
      return;
    }

    _targetRoot = transform;
  }

  private void CaptureStartPosition()
  {
    if (_targetRoot == null)
    {
      return;
    }

    _startLocalPosition = _targetRoot.localPosition;
    _startWorldPosition = _targetRoot.position;
  }

  private bool HasReliableTorso(IReadOnlyList<NormalizedLandmark> landmarks)
  {
    return IsReliable(landmarks[LeftShoulder]) &&
      IsReliable(landmarks[RightShoulder]) &&
      IsReliable(landmarks[LeftHip]) &&
      IsReliable(landmarks[RightHip]);
  }

  private bool IsReliable(NormalizedLandmark landmark)
  {
    return (!landmark.visibility.HasValue || landmark.visibility.Value >= _visibilityThreshold) &&
      (!landmark.presence.HasValue || landmark.presence.Value >= _visibilityThreshold);
  }

  private float GetBodyWidth(IReadOnlyList<NormalizedLandmark> landmarks)
  {
    var shoulderWidth = GetScreenDistance(landmarks[LeftShoulder], landmarks[RightShoulder]);
    var hipWidth = GetScreenDistance(landmarks[LeftHip], landmarks[RightHip]);
    return (shoulderWidth + hipWidth) * 0.5f;
  }

  private float GetTurnAmount(IReadOnlyList<NormalizedLandmark> landmarks)
  {
    var shoulderTurn = Mathf.Abs(landmarks[LeftShoulder].z - landmarks[RightShoulder].z);
    var hipTurn = Mathf.Abs(landmarks[LeftHip].z - landmarks[RightHip].z);
    return shoulderTurn + hipTurn;
  }

  private float GetAverageZ(NormalizedLandmark first, NormalizedLandmark second)
  {
    return (first.z + second.z) * 0.5f;
  }

  private float GetScreenDistance(NormalizedLandmark first, NormalizedLandmark second)
  {
    var dx = first.x - second.x;
    var dy = first.y - second.y;
    return Mathf.Sqrt(dx * dx + dy * dy);
  }
}
