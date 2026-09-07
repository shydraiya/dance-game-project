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
  [SerializeField, Min(0.1f)] private float _droidPoseMaxAgeSeconds = 0.5f;

  [Header("Driving")]
  [SerializeField] private bool _driveRootPosition = true;
  [SerializeField] private bool _driveTorso = true;
  [SerializeField] private bool _driveHead = true;
  [SerializeField] private bool _driveArms = true;
  [SerializeField] private bool _driveLegs = true;
  [SerializeField] private float _rotationSmoothing = 18.0f;
  [SerializeField] private float _positionSmoothing = 12.0f;
  [SerializeField, Range(0.0f, 1.0f)] private float _minimumVisibility = 0.35f;

  [Header("Landmark Mapping")]
  [SerializeField] private bool _mirrorHorizontally;
  [SerializeField] private bool _invertXCoordinate = true;
  [SerializeField] private Vector3 _landmarkScale = new Vector3(1.0f, -1.0f, -1.0f);
  [SerializeField] private Vector3 _rootPositionScale = new Vector3(2.0f, 1.0f, 1.0f);
  [SerializeField] private Vector3 _rootPositionOffset;

  private readonly Vector3[] _webCamPose = new Vector3[LandmarkCount];
  private readonly Vector3[] _droidPose = new Vector3[LandmarkCount];
  private readonly Vector3[] _mergedPose = new Vector3[LandmarkCount];
  private readonly float[] _webCamVisibility = new float[LandmarkCount];
  private readonly float[] _droidVisibility = new float[LandmarkCount];
  private readonly float[] _mergedVisibility = new float[LandmarkCount];
  private readonly Dictionary<HumanBodyBones, DrivenBone> _bones = new Dictionary<HumanBodyBones, DrivenBone>();
  private readonly object _poseLock = new object();

  private bool _hasWebCamPose;
  private bool _hasDroidPose;
  private bool _hasMergedPose;
  private bool _isUsingAnyDroidJoint;
  private bool _hasDroidAlignment;
  private bool _isCalibrated;
  private long _lastDroidPoseUtcTicks;
  private Quaternion _lastDroidAlignment = Quaternion.identity;
  private Vector3 _calibratedPelvisCenter;
  private Vector3 _calibratedRootLocalPosition;
  private Quaternion _calibratedRootRotation = Quaternion.identity;
  private Quaternion _calibratedRootLocalRotation = Quaternion.identity;
  private OrientationCalibration _hipsOrientation;
  private OrientationCalibration _chestOrientation;
  private OrientationCalibration _headOrientation;

  public bool IsUsingDroidPose
  {
    get
    {
      lock (_poseLock)
      {
        return _isUsingAnyDroidJoint;
      }
    }
  }

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
      ApplyOrientation(_hipsOrientation, GetTorsoOrientation(_mergedPose, _mergedVisibility));
      ApplyOrientation(_chestOrientation, GetTorsoOrientation(_mergedPose, _mergedVisibility));
    }

    if (_driveHead)
    {
      ApplyOrientation(_headOrientation, GetHeadOrientation(_mergedPose, _mergedVisibility));
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
    _isCalibrated = false;
    _hasMergedPose = false;
    CacheHumanoidBones();
    Subscribe();
  }

  public void SetWeights(float webCamWeight, float droidCamWeight)
  {
    _webCamWeight = Mathf.Max(0.0f, webCamWeight);
    _droidCamWeight = Mathf.Max(0.0f, droidCamWeight);
  }

  public void ApplyWebCamPose(PoseLandmarkerResult result)
  {
    CopyPose(result, _webCamPose, _webCamVisibility, out _hasWebCamPose);
  }

  public void ApplyDroidCamPose(PoseLandmarkerResult result)
  {
    CopyPose(result, _droidPose, _droidVisibility, out _hasDroidPose);
    lock (_poseLock)
    {
      if (_hasDroidPose)
      {
        _lastDroidPoseUtcTicks = DateTime.UtcNow.Ticks;
      }
    }
  }

  public bool TryCopyMergedPose(Vector3[] targetPose, float[] targetVisibility, out bool usedDroidPose)
  {
    usedDroidPose = false;
    if (targetPose == null || targetVisibility == null ||
        targetPose.Length < LandmarkCount || targetVisibility.Length < LandmarkCount)
    {
      return false;
    }

    // PatternManager judges during Update, before this component's LateUpdate.
    // Refresh here so the judge reads the newest available camera frames.
    MergeLatestPoses();

    lock (_poseLock)
    {
      if (!_hasMergedPose)
      {
        return false;
      }

      Array.Copy(_mergedPose, targetPose, LandmarkCount);
      Array.Copy(_mergedVisibility, targetVisibility, LandmarkCount);
      usedDroidPose = _isUsingAnyDroidJoint;
      return true;
    }
  }

  private void Subscribe()
  {
    if (_webCamRunner != null)
    {
      _webCamRunner.PoseLandmarksUpdated += ApplyWebCamPose;
    }

    if (_droidCamRunner != null)
    {
      _droidCamRunner.PoseLandmarksUpdated += ApplyDroidCamPose;
    }
  }

  private void Unsubscribe()
  {
    if (_webCamRunner != null)
    {
      _webCamRunner.PoseLandmarksUpdated -= ApplyWebCamPose;
    }

    if (_droidCamRunner != null)
    {
      _droidCamRunner.PoseLandmarksUpdated -= ApplyDroidCamPose;
    }
  }

  private void CopyPose(PoseLandmarkerResult result, Vector3[] targetPose, float[] targetVisibility, out bool hasPose)
  {
    lock (_poseLock)
    {
      hasPose = false;

      var worldPose = result.poseWorldLandmarks;
      if (worldPose != null && worldPose.Count > 0 && worldPose[0].landmarks != null && worldPose[0].landmarks.Count >= LandmarkCount)
      {
        CopyWorldPose(worldPose[0].landmarks, targetPose, targetVisibility);
        hasPose = true;
        return;
      }

      var normalizedPose = result.poseLandmarks;
      if (normalizedPose != null && normalizedPose.Count > 0 && normalizedPose[0].landmarks != null && normalizedPose[0].landmarks.Count >= LandmarkCount)
      {
        CopyNormalizedPose(normalizedPose[0].landmarks, targetPose, targetVisibility);
        hasPose = true;
      }
    }
  }

  private void CopyWorldPose(IReadOnlyList<Landmark> landmarks, Vector3[] targetPose, float[] targetVisibility)
  {
    for (var i = 0; i < LandmarkCount; i++)
    {
      var landmark = landmarks[GetSourceLandmarkIndex(i)];
      targetPose[i] = new Vector3(GetMappedX(landmark.x), landmark.y * _landmarkScale.y, landmark.z * _landmarkScale.z);
      targetVisibility[i] = landmark.visibility ?? 1.0f;
    }
  }

  private void CopyNormalizedPose(IReadOnlyList<NormalizedLandmark> landmarks, Vector3[] targetPose, float[] targetVisibility)
  {
    for (var i = 0; i < LandmarkCount; i++)
    {
      var landmark = landmarks[GetSourceLandmarkIndex(i)];
      targetPose[i] = new Vector3(GetMappedX(landmark.x - 0.5f), (landmark.y - 0.5f) * _landmarkScale.y, landmark.z * _landmarkScale.z);
      targetVisibility[i] = landmark.visibility ?? 1.0f;
    }
  }

  private void MergeLatestPoses()
  {
    lock (_poseLock)
    {
      if (!_hasWebCamPose)
      {
        _hasMergedPose = false;
        _isUsingAnyDroidJoint = false;
        return;
      }

      var droidPoseAgeTicks = DateTime.UtcNow.Ticks - _lastDroidPoseUtcTicks;
      var maxDroidPoseAgeTicks = (long)(_droidPoseMaxAgeSeconds * TimeSpan.TicksPerSecond);
      var canUseDroidPose = _hasDroidPose &&
                            droidPoseAgeTicks >= 0 &&
                            droidPoseAgeTicks <= maxDroidPoseAgeTicks;
      var droidAlignment = Quaternion.identity;
      if (canUseDroidPose && _alignDroidToWebCamTorso)
      {
        var webTorso = GetTorsoOrientation(_webCamPose, _webCamVisibility);
        var droidTorso = GetTorsoOrientation(_droidPose, _droidVisibility);
        if (webTorso != Quaternion.identity && droidTorso != Quaternion.identity)
        {
          _lastDroidAlignment = webTorso * Quaternion.Inverse(droidTorso);
          _hasDroidAlignment = true;
        }

        canUseDroidPose = _hasDroidAlignment;
        droidAlignment = _lastDroidAlignment;
      }

      var webWeight = Mathf.Max(0.0f, _webCamWeight);
      var webCenter = GetPelvisCenter(_webCamPose);
      var droidCenter = GetPelvisCenter(_droidPose);
      _isUsingAnyDroidJoint = false;

      for (var i = 0; i < LandmarkCount; i++)
      {
        var useDroidJoint = canUseDroidPose &&
                            _droidVisibility[i] > _droidVisibilityThreshold;
        var droidWeight = useDroidJoint ? Mathf.Max(0.0f, _droidCamWeight) : 0.0f;
        var totalWeight = Mathf.Max(webWeight + droidWeight, Mathf.Epsilon);
        var webNormalizedWeight = webWeight / totalWeight;
        var droidNormalizedWeight = droidWeight / totalWeight;
        var alignedDroid = useDroidJoint
          ? webCenter + droidAlignment * (_droidPose[i] - droidCenter)
          : _webCamPose[i];
        _mergedPose[i] = _webCamPose[i] * webNormalizedWeight + alignedDroid * droidNormalizedWeight;
        _mergedVisibility[i] = useDroidJoint
          ? Mathf.Max(_webCamVisibility[i], _droidVisibility[i])
          : _webCamVisibility[i];
        _isUsingAnyDroidJoint |= useDroidJoint;
      }

      _hasMergedPose = true;
    }
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

  private float GetMappedX(float x)
  {
    return x * (_invertXCoordinate ? -_landmarkScale.x : _landmarkScale.x);
  }

  private void CacheHumanoidBones()
  {
    _bones.Clear();

    if (_targetAnimator == null || !_targetAnimator.isHuman)
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
    if (_targetAnimator == null || !_targetAnimator.isHuman || _modelRoot == null || !HasCorePose())
    {
      return;
    }

    _calibratedPelvisCenter = GetPelvisCenter(_mergedPose);
    _calibratedRootLocalPosition = _modelRoot.localPosition;
    _calibratedRootRotation = _modelRoot.rotation;
    _calibratedRootLocalRotation = _modelRoot.localRotation;
    _hipsOrientation.RestPoseOrientation = GetTorsoOrientation(_mergedPose, _mergedVisibility);
    _chestOrientation.RestPoseOrientation = _hipsOrientation.RestPoseOrientation;
    _headOrientation.RestPoseOrientation = GetHeadOrientation(_mergedPose, _mergedVisibility);
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

  private Quaternion GetHeadOrientation(Vector3[] pose, float[] visibility)
  {
    if (!HasVisible(PoseIndex.Nose, visibility) || !HasVisible(PoseIndex.LeftEar, visibility) || !HasVisible(PoseIndex.RightEar, visibility))
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
