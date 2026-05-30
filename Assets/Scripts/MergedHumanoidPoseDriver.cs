using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

public class MergedHumanoidPoseDriver : MonoBehaviour
{
  private const int LandmarkCount = 33;

  [Header("Sources")]
  [SerializeField] private Animator _targetAnimator;
  [SerializeField] private Transform _modelRoot;
  [SerializeField] private PoseLandmarkerRunner _webCamRunner;
  [SerializeField] private WebCamPoseLandmarkerRunner _droidCamRunner;

  [Header("Merge")]
  [SerializeField, Range(0.0f, 1.0f)] private float _webCamWeight = 0.9f;
  [SerializeField, Range(0.0f, 1.0f)] private float _droidCamWeight = 0.1f;
  [SerializeField, Range(0.0f, 1.0f)] private float _droidVisibilityThreshold = 0.5f;
  [SerializeField] private bool _alignDroidToWebCamTorso = true;

  [Header("Driving")]
  [SerializeField] private bool _driveRootPosition;
  [SerializeField] private bool _driveTorso = true;
  [SerializeField] private bool _driveHead = true;
  [SerializeField] private bool _driveArms = true;
  [SerializeField] private bool _driveLegs = true;
  [SerializeField] private float _rotationSmoothing = 18.0f;
  [SerializeField] private float _positionSmoothing = 12.0f;
  [SerializeField, Range(0.0f, 1.0f)] private float _minimumVisibility = 0.35f;

  [Header("Landmark Mapping")]
  [SerializeField] private bool _mirrorHorizontally = true;
  [SerializeField] private Vector3 _landmarkScale = new(1.0f, -1.0f, -1.0f);
  [SerializeField] private Vector3 _rootPositionScale = Vector3.one;
  [SerializeField] private Vector3 _rootPositionOffset;

  private readonly Vector3[] _webCamPose = new Vector3[LandmarkCount];
  private readonly Vector3[] _droidPose = new Vector3[LandmarkCount];
  private readonly Vector3[] _mergedPose = new Vector3[LandmarkCount];
  private readonly float[] _webCamVisibility = new float[LandmarkCount];
  private readonly float[] _droidVisibility = new float[LandmarkCount];
  private readonly float[] _mergedVisibility = new float[LandmarkCount];
  private readonly Dictionary<HumanBodyBones, DrivenBone> _bones = new();
  private readonly object _poseLock = new();

  private bool _hasWebCamPose;
  private bool _hasDroidPose;
  private bool _hasMergedPose;
  private bool _isCalibrated;
  private Vector3 _calibratedPelvisCenter;
  private Vector3 _calibratedRootLocalPosition;
  private Quaternion _calibratedRootRotation = Quaternion.identity;
  private Quaternion _calibratedRootLocalRotation = Quaternion.identity;
  private OrientationCalibration _hipsOrientation;
  private OrientationCalibration _chestOrientation;
  private OrientationCalibration _headOrientation;

  private enum PoseIndex
  {
    Nose = 0,
    LeftEar = 7,
    RightEar = 8,
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

  private struct DrivenBone
  {
    public Transform Transform;
    public Vector3 RestDirection;
    public Quaternion RestRotation;
  }

  private struct OrientationCalibration
  {
    public Transform Transform;
    public Quaternion RestBoneRotation;
    public Quaternion RestPoseOrientation;
    public bool IsValid;
  }

  private void Reset()
  {
    _targetAnimator = GetComponentInChildren<Animator>();
    _modelRoot = _targetAnimator != null ? _targetAnimator.transform : transform;
  }

  private void Awake()
  {
    if (_targetAnimator == null)
    {
      _targetAnimator = GetComponentInChildren<Animator>();
    }

    if (_modelRoot == null && _targetAnimator != null)
    {
      _modelRoot = _targetAnimator.transform;
    }

    CacheHumanoidBones();
  }

  private void OnEnable()
  {
    Subscribe();
  }

  private void OnDisable()
  {
    Unsubscribe();
  }

  private void LateUpdate()
  {
    MergeLatestPoses();

    if (!_hasMergedPose)
    {
      return;
    }

    if (!_isCalibrated)
    {
      CalibrateFromCurrentPose();
      return;
    }

    var t = 1.0f - Mathf.Exp(-_rotationSmoothing * Time.deltaTime);

    if (_driveRootPosition)
    {
      ApplyRootPosition();
    }

    if (_driveTorso)
    {
      ApplyOrientation(_hipsOrientation, GetTorsoOrientation(_mergedPose));
      ApplyOrientation(_chestOrientation, GetTorsoOrientation(_mergedPose));
    }

    if (_driveHead)
    {
      ApplyOrientation(_headOrientation, GetHeadOrientation(_mergedPose));
    }

    if (_driveArms)
    {
      ApplySegment(HumanBodyBones.LeftUpperArm, PoseIndex.LeftShoulder, PoseIndex.LeftElbow, t);
      ApplySegment(HumanBodyBones.LeftLowerArm, PoseIndex.LeftElbow, PoseIndex.LeftWrist, t);
      ApplySegment(HumanBodyBones.RightUpperArm, PoseIndex.RightShoulder, PoseIndex.RightElbow, t);
      ApplySegment(HumanBodyBones.RightLowerArm, PoseIndex.RightElbow, PoseIndex.RightWrist, t);
    }

    if (_driveLegs)
    {
      ApplySegment(HumanBodyBones.LeftUpperLeg, PoseIndex.LeftHip, PoseIndex.LeftKnee, t);
      ApplySegment(HumanBodyBones.LeftLowerLeg, PoseIndex.LeftKnee, PoseIndex.LeftAnkle, t);
      ApplySegment(HumanBodyBones.RightUpperLeg, PoseIndex.RightHip, PoseIndex.RightKnee, t);
      ApplySegment(HumanBodyBones.RightLowerLeg, PoseIndex.RightKnee, PoseIndex.RightAnkle, t);
    }
  }

  public void Configure(Animator targetAnimator, Transform modelRoot, PoseLandmarkerRunner webCamRunner, WebCamPoseLandmarkerRunner droidCamRunner)
  {
    Unsubscribe();
    _targetAnimator = targetAnimator;
    _modelRoot = modelRoot;
    _webCamRunner = webCamRunner;
    _droidCamRunner = droidCamRunner;
    CacheHumanoidBones();
    Subscribe();
  }

  public void SetWeights(float webCamWeight, float droidCamWeight)
  {
    _webCamWeight = Mathf.Max(0.0f, webCamWeight);
    _droidCamWeight = Mathf.Max(0.0f, droidCamWeight);
  }

  private void Subscribe()
  {
    if (_webCamRunner != null)
    {
      _webCamRunner.PoseLandmarksUpdated += OnWebCamPoseUpdated;
    }

    if (_droidCamRunner != null)
    {
      _droidCamRunner.PoseLandmarksUpdated += OnDroidCamPoseUpdated;
    }
  }

  private void Unsubscribe()
  {
    if (_webCamRunner != null)
    {
      _webCamRunner.PoseLandmarksUpdated -= OnWebCamPoseUpdated;
    }

    if (_droidCamRunner != null)
    {
      _droidCamRunner.PoseLandmarksUpdated -= OnDroidCamPoseUpdated;
    }
  }

  private void OnWebCamPoseUpdated(PoseLandmarkerResult result)
  {
    CopyPose(result, _webCamPose, _webCamVisibility, out _hasWebCamPose);
  }

  private void OnDroidCamPoseUpdated(PoseLandmarkerResult result)
  {
    CopyPose(result, _droidPose, _droidVisibility, out _hasDroidPose);
  }

  private void CopyPose(PoseLandmarkerResult result, Vector3[] targetPose, float[] targetVisibility, out bool hasPose)
  {
    lock (_poseLock)
    {
      hasPose = false;

      var worldPose = result.poseWorldLandmarks;
      if (worldPose != null && worldPose.Count > 0 && worldPose[0].landmarks != null && worldPose[0].landmarks.Count >= LandmarkCount)
      {
        for (var i = 0; i < LandmarkCount; i++)
        {
          var landmark = worldPose[0].landmarks[GetSourceLandmarkIndex(i)];
          targetPose[i] = new Vector3(landmark.x * _landmarkScale.x, landmark.y * _landmarkScale.y, landmark.z * _landmarkScale.z);
          targetVisibility[i] = landmark.visibility ?? 1.0f;
        }

        hasPose = true;
        return;
      }

      var normalizedPose = result.poseLandmarks;
      if (normalizedPose != null && normalizedPose.Count > 0 && normalizedPose[0].landmarks != null && normalizedPose[0].landmarks.Count >= LandmarkCount)
      {
        for (var i = 0; i < LandmarkCount; i++)
        {
          var landmark = normalizedPose[0].landmarks[GetSourceLandmarkIndex(i)];
          targetPose[i] = new Vector3((landmark.x - 0.5f) * _landmarkScale.x, (landmark.y - 0.5f) * _landmarkScale.y, landmark.z * _landmarkScale.z);
          targetVisibility[i] = landmark.visibility ?? 1.0f;
        }

        hasPose = true;
      }
    }
  }

  private void MergeLatestPoses()
  {
    lock (_poseLock)
    {
      if (!_hasWebCamPose)
      {
        _hasMergedPose = false;
        return;
      }

      var useDroidPose = _hasDroidPose && HasReliableDroidPose();
      var droidAlignment = useDroidPose && _alignDroidToWebCamTorso
        ? GetTorsoOrientation(_webCamPose, _webCamVisibility) * Quaternion.Inverse(GetTorsoOrientation(_droidPose, _droidVisibility))
        : Quaternion.identity;
      var webWeight = Mathf.Max(0.0f, _webCamWeight);
      var droidWeight = useDroidPose ? Mathf.Max(0.0f, _droidCamWeight) : 0.0f;
      var totalWeight = Mathf.Max(webWeight + droidWeight, Mathf.Epsilon);
      var webNormalizedWeight = webWeight / totalWeight;
      var droidNormalizedWeight = droidWeight / totalWeight;
      var webCenter = GetPelvisCenter(_webCamPose);
      var droidCenter = GetPelvisCenter(_droidPose);

      for (var i = 0; i < LandmarkCount; i++)
      {
        var alignedDroid = useDroidPose
          ? webCenter + droidAlignment * (_droidPose[i] - droidCenter)
          : _webCamPose[i];
        _mergedPose[i] = _webCamPose[i] * webNormalizedWeight + alignedDroid * droidNormalizedWeight;
        _mergedVisibility[i] = useDroidPose
          ? Mathf.Max(_webCamVisibility[i], _droidVisibility[i])
          : _webCamVisibility[i];
      }

      _hasMergedPose = true;
    }
  }

  private bool HasReliableDroidPose()
  {
    return IsDroidVisible(PoseIndex.LeftShoulder) &&
      IsDroidVisible(PoseIndex.RightShoulder) &&
      IsDroidVisible(PoseIndex.LeftHip) &&
      IsDroidVisible(PoseIndex.RightHip) &&
      IsDroidVisible(PoseIndex.LeftElbow) &&
      IsDroidVisible(PoseIndex.RightElbow) &&
      IsDroidVisible(PoseIndex.LeftWrist) &&
      IsDroidVisible(PoseIndex.RightWrist);
  }

  private bool IsDroidVisible(PoseIndex index)
  {
    return _droidVisibility[(int)index] > _droidVisibilityThreshold;
  }

  private int GetSourceLandmarkIndex(int targetIndex)
  {
    if (!_mirrorHorizontally)
    {
      return targetIndex;
    }

    return targetIndex switch
    {
      1 => 4,
      2 => 5,
      3 => 6,
      4 => 1,
      5 => 2,
      6 => 3,
      7 => 8,
      8 => 7,
      9 => 10,
      10 => 9,
      11 => 12,
      12 => 11,
      13 => 14,
      14 => 13,
      15 => 16,
      16 => 15,
      17 => 18,
      18 => 17,
      19 => 20,
      20 => 19,
      21 => 22,
      22 => 21,
      23 => 24,
      24 => 23,
      25 => 26,
      26 => 25,
      27 => 28,
      28 => 27,
      29 => 30,
      30 => 29,
      31 => 32,
      32 => 31,
      _ => targetIndex
    };
  }

  private void CacheHumanoidBones()
  {
    _bones.Clear();

    if (_targetAnimator == null)
    {
      return;
    }

    AddSegment(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
    AddSegment(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
    AddSegment(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
    AddSegment(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
    AddSegment(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
    AddSegment(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
    AddSegment(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
    AddSegment(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);

    _hipsOrientation = CreateOrientationCalibration(HumanBodyBones.Hips);
    _chestOrientation = CreateOrientationCalibration(HumanBodyBones.Chest);
    if (!_chestOrientation.IsValid)
    {
      _chestOrientation = CreateOrientationCalibration(HumanBodyBones.Spine);
    }
    _headOrientation = CreateOrientationCalibration(HumanBodyBones.Head);
  }

  private void AddSegment(HumanBodyBones bone, HumanBodyBones childBone)
  {
    var boneTransform = _targetAnimator.GetBoneTransform(bone);
    var childTransform = _targetAnimator.GetBoneTransform(childBone);
    if (boneTransform == null || childTransform == null)
    {
      return;
    }

    var restDirection = childTransform.position - boneTransform.position;
    if (restDirection.sqrMagnitude < 0.000001f)
    {
      return;
    }

    _bones[bone] = new DrivenBone
    {
      Transform = boneTransform,
      RestDirection = restDirection.normalized,
      RestRotation = boneTransform.rotation
    };
  }

  private OrientationCalibration CreateOrientationCalibration(HumanBodyBones bone)
  {
    var boneTransform = _targetAnimator.GetBoneTransform(bone);
    if (boneTransform == null)
    {
      return default;
    }

    return new OrientationCalibration
    {
      Transform = boneTransform,
      RestBoneRotation = boneTransform.rotation,
      IsValid = true
    };
  }

  private void CalibrateFromCurrentPose()
  {
    if (_targetAnimator == null || _modelRoot == null || !HasCorePose())
    {
      return;
    }

    _calibratedPelvisCenter = GetPelvisCenter(_mergedPose);
    _calibratedRootLocalPosition = _modelRoot.localPosition;
    _calibratedRootRotation = _modelRoot.rotation;
    _calibratedRootLocalRotation = _modelRoot.localRotation;
    _hipsOrientation.RestPoseOrientation = GetTorsoOrientation(_mergedPose);
    _chestOrientation.RestPoseOrientation = _hipsOrientation.RestPoseOrientation;
    _headOrientation.RestPoseOrientation = GetHeadOrientation(_mergedPose);
    _isCalibrated = true;
  }

  private void ApplyRootPosition()
  {
    if (_modelRoot == null)
    {
      return;
    }

    var pelvisDelta = GetPelvisCenter(_mergedPose) - _calibratedPelvisCenter;
    var targetLocalPosition = _calibratedRootLocalPosition + (_calibratedRootLocalRotation * Vector3.Scale(pelvisDelta, _rootPositionScale)) + _rootPositionOffset;
    var t = 1.0f - Mathf.Exp(-_positionSmoothing * Time.deltaTime);
    _modelRoot.localPosition = Vector3.Lerp(_modelRoot.localPosition, targetLocalPosition, t);
  }

  private void ApplySegment(HumanBodyBones bone, PoseIndex start, PoseIndex end, float smoothing)
  {
    if (!_bones.TryGetValue(bone, out var drivenBone) || !HasVisible(start) || !HasVisible(end))
    {
      return;
    }

    var targetDirection = _mergedPose[(int)end] - _mergedPose[(int)start];
    if (targetDirection.sqrMagnitude < 0.000001f)
    {
      return;
    }

    var targetWorldDirection = GetModelSpaceDirection(targetDirection.normalized);
    var targetRotation = Quaternion.FromToRotation(drivenBone.RestDirection, targetWorldDirection) * drivenBone.RestRotation;
    drivenBone.Transform.rotation = Quaternion.Slerp(drivenBone.Transform.rotation, targetRotation, smoothing);
  }

  private void ApplyOrientation(OrientationCalibration calibration, Quaternion targetPoseOrientation)
  {
    if (!calibration.IsValid || calibration.Transform == null || targetPoseOrientation == Quaternion.identity)
    {
      return;
    }

    var targetWorldDelta = GetModelSpaceRotationDelta(targetPoseOrientation, calibration.RestPoseOrientation);
    var targetRotation = targetWorldDelta * calibration.RestBoneRotation;
    var t = 1.0f - Mathf.Exp(-_rotationSmoothing * Time.deltaTime);
    calibration.Transform.rotation = Quaternion.Slerp(calibration.Transform.rotation, targetRotation, t);
  }

  private Quaternion GetTorsoOrientation(Vector3[] pose)
  {
    return GetTorsoOrientation(pose, _mergedVisibility);
  }

  private Quaternion GetTorsoOrientation(Vector3[] pose, float[] visibility)
  {
    if (!HasVisible(PoseIndex.LeftShoulder, visibility) || !HasVisible(PoseIndex.RightShoulder, visibility) || !HasVisible(PoseIndex.LeftHip, visibility) || !HasVisible(PoseIndex.RightHip, visibility))
    {
      return Quaternion.identity;
    }

    var leftShoulder = pose[(int)PoseIndex.LeftShoulder];
    var rightShoulder = pose[(int)PoseIndex.RightShoulder];
    var leftHip = pose[(int)PoseIndex.LeftHip];
    var rightHip = pose[(int)PoseIndex.RightHip];
    var shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
    var hipCenter = (leftHip + rightHip) * 0.5f;
    var right = rightShoulder - leftShoulder;
    var up = shoulderCenter - hipCenter;
    return BuildOrientation(right, up);
  }

  private Quaternion GetHeadOrientation(Vector3[] pose)
  {
    if (!HasVisible(PoseIndex.Nose) || !HasVisible(PoseIndex.LeftEar) || !HasVisible(PoseIndex.RightEar))
    {
      return Quaternion.identity;
    }

    var leftEar = pose[(int)PoseIndex.LeftEar];
    var rightEar = pose[(int)PoseIndex.RightEar];
    var earCenter = (leftEar + rightEar) * 0.5f;
    var nose = pose[(int)PoseIndex.Nose];
    var right = rightEar - leftEar;
    var forward = nose - earCenter;
    var up = Vector3.Cross(right, forward);
    return BuildOrientation(right, up);
  }

  private Quaternion BuildOrientation(Vector3 right, Vector3 up)
  {
    if (right.sqrMagnitude < 0.000001f || up.sqrMagnitude < 0.000001f)
    {
      return Quaternion.identity;
    }

    right.Normalize();
    up.Normalize();
    var forward = Vector3.Cross(right, up);
    if (forward.sqrMagnitude < 0.000001f)
    {
      return Quaternion.identity;
    }

    return Quaternion.LookRotation(forward.normalized, up);
  }

  private Vector3 GetModelSpaceDirection(Vector3 landmarkDirection)
  {
    return _modelRoot != null ? (_calibratedRootRotation * landmarkDirection).normalized : landmarkDirection.normalized;
  }

  private Quaternion GetModelSpaceRotationDelta(Quaternion currentPoseRotation, Quaternion restPoseRotation)
  {
    var poseDelta = currentPoseRotation * Quaternion.Inverse(restPoseRotation);
    return _modelRoot != null ? _calibratedRootRotation * poseDelta * Quaternion.Inverse(_calibratedRootRotation) : poseDelta;
  }

  private Vector3 GetPelvisCenter(Vector3[] pose)
  {
    return (pose[(int)PoseIndex.LeftHip] + pose[(int)PoseIndex.RightHip]) * 0.5f;
  }

  private bool HasCorePose()
  {
    return HasVisible(PoseIndex.LeftShoulder) &&
      HasVisible(PoseIndex.RightShoulder) &&
      HasVisible(PoseIndex.LeftHip) &&
      HasVisible(PoseIndex.RightHip);
  }

  private bool HasVisible(PoseIndex index)
  {
    return HasVisible(index, _mergedVisibility);
  }

  private bool HasVisible(PoseIndex index, float[] visibility)
  {
    return visibility[(int)index] >= _minimumVisibility;
  }
}
