using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

public class HumanoidPoseDriver : MonoBehaviour
{
  private const int LandmarkCount = 33;

  [Header("Sources")]
  [SerializeField] private Animator _targetAnimator;
  [SerializeField] private PoseLandmarkerRunner _poseRunner;
  [SerializeField] private Transform _modelRoot;

  [Header("Driving")]
  [SerializeField] private bool _driveRootPosition;
  [SerializeField] private bool _driveTorso = true;
  [SerializeField] private bool _driveHead = true;
  [SerializeField] private bool _driveArms = true;
  [SerializeField] private bool _driveLegs = true;
  [SerializeField] private float _rotationSmoothing = 18.0f;
  [SerializeField] private float _positionSmoothing = 12.0f;
  [SerializeField, Range(0, 1)] private float _minimumVisibility = 0.35f;

  [Header("MediaPipe Axis")]
  [SerializeField] private bool _mirrorHorizontally = true;
  [SerializeField] private bool _swapLeftRightLandmarksWhenNotMirrored = true;
  [SerializeField] private Vector3 _landmarkScale = new Vector3(1.0f, -1.0f, -1.0f);
  [SerializeField] private Vector3 _rootPositionScale = new Vector3(1.0f, 1.0f, 1.0f);
  [SerializeField] private Vector3 _rootPositionOffset;

  [Header("Debug")]
  [SerializeField] private bool _logMissingReferences = true;
  [SerializeField] private bool _logStatus;
  [SerializeField] private bool _hasPose;
  [SerializeField] private bool _isCalibrated;
  [SerializeField] private int _receivedPoseFrameCount;
  [SerializeField] private int _appliedPoseFrameCount;
  [SerializeField] private int _drivenBoneCount;
  [SerializeField] private string _status = "Not started";

  private readonly Vector3[] _latestPose = new Vector3[LandmarkCount];
  private readonly float[] _latestVisibility = new float[LandmarkCount];
  private readonly Vector3[] _pendingPose = new Vector3[LandmarkCount];
  private readonly float[] _pendingVisibility = new float[LandmarkCount];
  private readonly Dictionary<HumanBodyBones, DrivenBone> _bones = new Dictionary<HumanBodyBones, DrivenBone>();
  private readonly object _poseLock = new object();

  private bool _hasPendingPose;
  private float _nextStatusLogTime;
  private Vector3 _calibratedPelvisCenter;
  private Vector3 _calibratedRootLocalPosition;
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

    if (_poseRunner == null)
    {
      _poseRunner = FindAnyObjectByType<PoseLandmarkerRunner>();
    }

    if (_modelRoot == null && _targetAnimator != null)
    {
      _modelRoot = _targetAnimator.transform;
    }

    CacheHumanoidBones();
  }

  private void Start()
  {
    CacheHumanoidBones();
    UpdateStatus();
  }

  private void OnEnable()
  {
    if (_poseRunner == null)
    {
      _poseRunner = FindAnyObjectByType<PoseLandmarkerRunner>();
    }

    if (_poseRunner != null)
    {
      _poseRunner.PoseLandmarksUpdated += OnPoseLandmarksUpdated;
    }
  }

  private void OnDisable()
  {
    if (_poseRunner != null)
    {
      _poseRunner.PoseLandmarksUpdated -= OnPoseLandmarksUpdated;
    }
  }

  private void LateUpdate()
  {
    ConsumePendingPose();

    if (_drivenBoneCount == 0 && _targetAnimator != null)
    {
      CacheHumanoidBones();
    }

    if (_logStatus && Time.unscaledTime >= _nextStatusLogTime)
    {
      _nextStatusLogTime = Time.unscaledTime + 1.0f;
      UpdateStatus();
      Debug.Log($"{nameof(HumanoidPoseDriver)} status: {_status}", this);
    }

    if (!_hasPose || !_isCalibrated)
    {
      return;
    }

    var t = 1.0f - Mathf.Exp(-_rotationSmoothing * Time.deltaTime);

    if (_driveRootPosition)
    {
      ApplyRootPosition();
    }

    if (_driveTorso)
    {
      ApplyOrientation(_hipsOrientation, GetTorsoOrientation(_latestPose));
      ApplyOrientation(_chestOrientation, GetTorsoOrientation(_latestPose));
    }

    if (_driveHead)
    {
      ApplyOrientation(_headOrientation, GetHeadOrientation(_latestPose));
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

    _appliedPoseFrameCount++;
  }

  public void SetPoseRunner(PoseLandmarkerRunner poseRunner)
  {
    if (_poseRunner == poseRunner)
    {
      return;
    }

    if (isActiveAndEnabled && _poseRunner != null)
    {
      _poseRunner.PoseLandmarksUpdated -= OnPoseLandmarksUpdated;
    }

    _poseRunner = poseRunner;

    if (isActiveAndEnabled && _poseRunner != null)
    {
      _poseRunner.PoseLandmarksUpdated += OnPoseLandmarksUpdated;
    }
  }

  public void Recalibrate()
  {
    _isCalibrated = false;
    CacheHumanoidBones();

    if (_hasPose)
    {
      CalibrateFromCurrentPose();
    }
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
      _hasPendingPose = false;
    }
  }

  private void CopyWorldPose(IReadOnlyList<Landmark> landmarks)
  {
    lock (_poseLock)
    {
      for (var i = 0; i < LandmarkCount; i++)
      {
        var landmark = landmarks[GetSourceLandmarkIndex(i)];
        var x = _mirrorHorizontally ? -landmark.x : landmark.x;
        _pendingPose[i] = new Vector3(x * _landmarkScale.x, landmark.y * _landmarkScale.y, landmark.z * _landmarkScale.z);
        _pendingVisibility[i] = landmark.visibility ?? 1.0f;
      }

      _hasPendingPose = true;
      _receivedPoseFrameCount++;
    }
  }

  private void CopyNormalizedPose(IReadOnlyList<NormalizedLandmark> landmarks)
  {
    lock (_poseLock)
    {
      for (var i = 0; i < LandmarkCount; i++)
      {
        var landmark = landmarks[GetSourceLandmarkIndex(i)];
        var x = _mirrorHorizontally ? -(landmark.x - 0.5f) : landmark.x - 0.5f;
        _pendingPose[i] = new Vector3(x * _landmarkScale.x, (landmark.y - 0.5f) * _landmarkScale.y, landmark.z * _landmarkScale.z);
        _pendingVisibility[i] = landmark.visibility ?? 1.0f;
      }

      _hasPendingPose = true;
      _receivedPoseFrameCount++;
    }
  }

  private int GetSourceLandmarkIndex(int targetIndex)
  {
    if (_mirrorHorizontally || !_swapLeftRightLandmarksWhenNotMirrored)
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

  private void ConsumePendingPose()
  {
    lock (_poseLock)
    {
      if (!_hasPendingPose)
      {
        return;
      }

      Array.Copy(_pendingPose, _latestPose, LandmarkCount);
      Array.Copy(_pendingVisibility, _latestVisibility, LandmarkCount);
      _hasPendingPose = false;
      _hasPose = true;
    }

    if (!_isCalibrated)
    {
      CalibrateFromCurrentPose();
    }

    UpdateStatus();
  }

  private void CacheHumanoidBones()
  {
    _bones.Clear();
    _drivenBoneCount = 0;

    if (_targetAnimator == null)
    {
      if (_logMissingReferences)
      {
        Debug.LogWarning($"{nameof(HumanoidPoseDriver)} needs a target Animator.", this);
      }
      UpdateStatus();
      return;
    }

    if (!_targetAnimator.isHuman)
    {
      if (_logMissingReferences)
      {
        Debug.LogWarning($"{nameof(HumanoidPoseDriver)} target Animator is not Humanoid.", this);
      }
      UpdateStatus();
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
    _drivenBoneCount = _bones.Count;
    UpdateStatus();
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
    if (_targetAnimator == null || !_targetAnimator.isHuman || _modelRoot == null || !HasCorePose())
    {
      return;
    }

    _calibratedPelvisCenter = GetPelvisCenter(_latestPose);
    _calibratedRootLocalPosition = _modelRoot.localPosition;

    _hipsOrientation.RestPoseOrientation = GetTorsoOrientation(_latestPose);
    _chestOrientation.RestPoseOrientation = _hipsOrientation.RestPoseOrientation;
    _headOrientation.RestPoseOrientation = GetHeadOrientation(_latestPose);
    _isCalibrated = true;
    UpdateStatus();
  }

  private void ApplyRootPosition()
  {
    if (_modelRoot == null)
    {
      return;
    }

    var pelvisDelta = GetPelvisCenter(_latestPose) - _calibratedPelvisCenter;
    var targetLocalPosition = _calibratedRootLocalPosition + Vector3.Scale(pelvisDelta, _rootPositionScale) + _rootPositionOffset;
    var t = 1.0f - Mathf.Exp(-_positionSmoothing * Time.deltaTime);
    _modelRoot.localPosition = Vector3.Lerp(_modelRoot.localPosition, targetLocalPosition, t);
  }

  private void ApplySegment(HumanBodyBones bone, PoseIndex start, PoseIndex end, float smoothing)
  {
    if (!_bones.TryGetValue(bone, out var drivenBone) || !HasVisible(start) || !HasVisible(end))
    {
      return;
    }

    var targetDirection = _latestPose[(int)end] - _latestPose[(int)start];
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

    var targetWorldOrientation = GetModelSpaceRotation(targetPoseOrientation);
    var targetRotation = targetWorldOrientation * Quaternion.Inverse(calibration.RestPoseOrientation) * calibration.RestBoneRotation;
    var t = 1.0f - Mathf.Exp(-_rotationSmoothing * Time.deltaTime);
    calibration.Transform.rotation = Quaternion.Slerp(calibration.Transform.rotation, targetRotation, t);
  }

  private Quaternion GetTorsoOrientation(Vector3[] pose)
  {
    if (!HasVisible(PoseIndex.LeftShoulder) || !HasVisible(PoseIndex.RightShoulder) || !HasVisible(PoseIndex.LeftHip) || !HasVisible(PoseIndex.RightHip))
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
    return _modelRoot != null ? _modelRoot.TransformDirection(landmarkDirection).normalized : landmarkDirection.normalized;
  }

  private Quaternion GetModelSpaceRotation(Quaternion landmarkRotation)
  {
    return _modelRoot != null ? _modelRoot.rotation * landmarkRotation : landmarkRotation;
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
    return _latestVisibility[(int)index] >= _minimumVisibility;
  }

  private void UpdateStatus()
  {
    if (_targetAnimator == null)
    {
      _status = "Target Animator is missing";
    }
    else if (!_targetAnimator.isHuman)
    {
      _status = "Target Animator is not Humanoid";
    }
    else if (_poseRunner == null)
    {
      _status = "Pose Runner is missing";
    }
    else if (_drivenBoneCount == 0)
    {
      _status = "No humanoid limb bones cached";
    }
    else if (!_hasPose)
    {
      _status = "Waiting for pose landmarks";
    }
    else if (!_isCalibrated)
    {
      _status = "Waiting for calibration core landmarks";
    }
    else
    {
      _status = "Driving humanoid pose";
    }
  }
}
