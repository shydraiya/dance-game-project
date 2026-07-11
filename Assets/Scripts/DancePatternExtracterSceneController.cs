using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DancePatternExtracterSceneController : MonoBehaviour
{
  private static readonly string[] CsvHeaders =
  {
    "time",
    "root_position",
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

  [Header("Avatar Animation Preview")]
  [SerializeField] private Animator _avatarAnimator;
  [SerializeField] private Animator _csvCheckingAnimator;
  [SerializeField] private RuntimeAnimatorController _previewDanceController;
  [SerializeField] private RuntimeAnimatorController[] _danceControllers;
  [SerializeField] private int _selectedDanceIndex;
  [SerializeField] private bool _playPreviewOnStart = true;

  [Header("CSV Export")]
  [SerializeField] private string _exportFileName = "dance_pattern_export.csv";
  [SerializeField] private float _recordSeconds = 10.0f;
  [SerializeField] private float _sampleRate = 30.0f;
  [SerializeField] private bool _normalizeBoneVectors = true;
  [SerializeField] private bool _autoRecordPatternOnDanceChange = true;
  [SerializeField] private bool _recordOnStart;

  [Header("CSV Playback")]
  [SerializeField] private TextAsset _playbackCsvAsset;
  [SerializeField] private string _playbackFileName = "dance_pattern_export.csv";
  [SerializeField] private bool _playCsvAssetOnStart;
  [SerializeField] private bool _autoPlayCsvAssetOnChange = true;
  [SerializeField] private bool _loopPlayback = true;
  [SerializeField] private float _playbackSmoothing = 18.0f;

  [Header("Status")]
  [SerializeField] private bool _isRecording;
  [SerializeField] private bool _isPlayingCsv;
  [SerializeField] private string _lastExportPath;
  [SerializeField] private string _status = "Ready";

  private readonly SkeletonCsvExporter _csvExporter = new SkeletonCsvExporter();
  private readonly SkeletonCsvPlayback _csvPlayback = new SkeletonCsvPlayback();

  private float _recordStartedAt;
  private float _nextSampleAt;
  private RuntimeAnimatorController _lastAppliedDanceController;
  private int _lastAppliedDanceIndex = -1;
  private TextAsset _lastPlaybackCsvAsset;

  public Animator AvatarAnimator
  {
    get { return _avatarAnimator; }
  }

  public Animator CsvCheckingAnimator
  {
    get { return _csvCheckingAnimator; }
  }

  private void Reset()
  {
    _avatarAnimator = GetComponentInChildren<Animator>();
  }

  private void Awake()
  {
    if (_avatarAnimator == null)
    {
      _avatarAnimator = GetComponentInChildren<Animator>();
    }

    _csvPlayback.Configure(_csvCheckingAnimator, _playbackSmoothing, _normalizeBoneVectors);
  }

  private void Start()
  {
    if (_playPreviewOnStart)
    {
      PlaySelectedDance();
    }

    if (_recordOnStart)
    {
      StartCsvRecording();
    }

    if (_playCsvAssetOnStart && _playbackCsvAsset != null)
    {
      PlayCsv();
    }
  }

  private void Update()
  {
    ApplyInspectorDanceChanges();
    ApplyInspectorCsvChanges();

    if (_isRecording)
    {
      UpdateRecording();
    }

    if (_isPlayingCsv)
    {
      _isPlayingCsv = _csvPlayback.Tick(Time.deltaTime, _loopPlayback);
      _status = _isPlayingCsv ? "Playing CSV" : "CSV playback finished";
    }
  }

  public void PlaySelectedDance()
  {
    if (_previewDanceController != null)
    {
      PlayDance(_previewDanceController);
      return;
    }

    PlayDance(_selectedDanceIndex);
  }

  public void PlayDance(RuntimeAnimatorController danceController)
  {
    if (_avatarAnimator == null || danceController == null)
    {
      _status = "Animator or preview dance controller is missing";
      return;
    }

    ApplyDanceController(danceController);
  }

  public void PlayDance(int index)
  {
    if (_avatarAnimator == null || _danceControllers == null || _danceControllers.Length == 0)
    {
      _status = "Animator or dance controllers are missing";
      return;
    }

    _selectedDanceIndex = Mathf.Clamp(index, 0, _danceControllers.Length - 1);
    _previewDanceController = _danceControllers[_selectedDanceIndex];
    ApplyDanceController(_previewDanceController);
  }

  private void ApplyDanceController(RuntimeAnimatorController danceController)
  {
    _avatarAnimator.runtimeAnimatorController = danceController;
    _avatarAnimator.Rebind();
    _avatarAnimator.Update(0.0f);
    _lastAppliedDanceController = danceController;
    _lastAppliedDanceIndex = _selectedDanceIndex;

    _exportFileName = GetCurrentDancePatternFileName();
    if (_playbackCsvAsset == null)
    {
      _playbackFileName = _exportFileName;
    }
    _status = "Previewing " + _avatarAnimator.runtimeAnimatorController.name;

    if (_autoRecordPatternOnDanceChange)
    {
      StartCsvRecordingIfPatternMissing();
    }
  }

  public void NextDance()
  {
    if (_danceControllers == null || _danceControllers.Length == 0)
    {
      return;
    }

    PlayDance((_selectedDanceIndex + 1) % _danceControllers.Length);
  }

  public void StartCsvRecording()
  {
    if (_isRecording)
    {
      return;
    }

    if (_avatarAnimator == null)
    {
      _status = "Cannot record without an avatar Animator";
      return;
    }

    _exportFileName = GetCurrentDancePatternFileName();
    _playbackFileName = _exportFileName;

    if (PatternFileExists(_exportFileName))
    {
      _isRecording = false;
      _lastExportPath = Path.Combine(PatternsDirectory, _exportFileName);
      _status = "Pattern already exists: " + _exportFileName;
      PlayCsv(_exportFileName);
      return;
    }

    _csvExporter.Begin(_avatarAnimator, _exportFileName, _normalizeBoneVectors);
    _recordStartedAt = Time.time;
    _nextSampleAt = Time.time;
    _isRecording = true;
    _status = "Recording CSV: " + _exportFileName;
  }

  public void StartCsvRecordingIfPatternMissing()
  {
    string fileName = GetCurrentDancePatternFileName();
    _exportFileName = fileName;
    _playbackFileName = fileName;

    if (PatternFileExists(fileName))
    {
      _isRecording = false;
      _lastExportPath = Path.Combine(PatternsDirectory, fileName);
      _status = "Pattern already exists: " + fileName;
      PlayCsv(fileName);
      return;
    }

    StartCsvRecording();
  }

  public void StopCsvRecording()
  {
    if (!_isRecording)
    {
      return;
    }

    _lastExportPath = _csvExporter.Finish();
    _isRecording = false;
    _status = "CSV exported: " + _lastExportPath;
    PlayCsv(_exportFileName);
  }

  public void ExportCurrentAnimationToCsv()
  {
    StartCsvRecording();
  }

  public void PlayCsv()
  {
    if (_playbackCsvAsset != null)
    {
      PlayCsv(_playbackCsvAsset);
      return;
    }

    PlayCsv(string.IsNullOrWhiteSpace(_playbackFileName) ? GetCurrentDancePatternFileName() : _playbackFileName);
  }

  public void PlayCsv(TextAsset csvAsset)
  {
    if (csvAsset == null)
    {
      _status = "CSV asset is missing";
      return;
    }

    _playbackCsvAsset = csvAsset;
    _lastPlaybackCsvAsset = csvAsset;
    _playbackFileName = GetCsvAssetDisplayName(csvAsset);

    if (_csvCheckingAnimator == null)
    {
      _status = "Cannot play CSV without a CSV checking Animator";
      return;
    }

    StopCsvCheckingAnimatorController();
    _csvPlayback.Configure(_csvCheckingAnimator, _playbackSmoothing, _normalizeBoneVectors);
    if (!_csvPlayback.Load(csvAsset))
    {
      _status = "CSV asset is empty or invalid: " + csvAsset.name;
      return;
    }

    _isPlayingCsv = true;
    _status = "Playing CSV asset: " + csvAsset.name;
  }

  public void PlayCsv(string fileName)
  {
    _playbackFileName = fileName;

    if (_csvCheckingAnimator == null)
    {
      _status = "Cannot play CSV without a CSV checking Animator";
      return;
    }

    StopCsvCheckingAnimatorController();
    _csvPlayback.Configure(_csvCheckingAnimator, _playbackSmoothing, _normalizeBoneVectors);
    if (!_csvPlayback.Load(fileName))
    {
      _status = "CSV file not found or empty: " + fileName;
      return;
    }

    _isPlayingCsv = true;
    _status = "Playing CSV: " + fileName;
  }

  public void StopCsv()
  {
    _isPlayingCsv = false;
    _csvPlayback.Stop();
    _status = "CSV playback stopped";
  }

  public void ApplyCsvFrame(int frameIndex)
  {
    StopCsvCheckingAnimatorController();
    _csvPlayback.Configure(_csvCheckingAnimator, _playbackSmoothing, _normalizeBoneVectors);
    if (_csvPlayback.FrameCount == 0)
    {
      _csvPlayback.Load(_playbackFileName);
    }

    _csvPlayback.ApplyFrame(frameIndex, 1.0f);
  }

  private void UpdateRecording()
  {
    if (Time.time >= _nextSampleAt)
    {
      _csvExporter.Sample(Time.time - _recordStartedAt);
      _nextSampleAt += 1.0f / Mathf.Max(1.0f, _sampleRate);
    }

    if (Time.time - _recordStartedAt >= _recordSeconds)
    {
      StopCsvRecording();
    }
  }

  private static string PatternsDirectory
  {
    get { return Path.Combine(Application.dataPath, "Patterns"); }
  }

  private string GetCurrentDancePatternFileName()
  {
    RuntimeAnimatorController controller = _avatarAnimator != null ? _avatarAnimator.runtimeAnimatorController : null;
    if (controller == null && _danceControllers != null && _danceControllers.Length > 0)
    {
      controller = _danceControllers[Mathf.Clamp(_selectedDanceIndex, 0, _danceControllers.Length - 1)];
    }

    string baseName = controller != null ? controller.name : "dance_pattern_export";
    return SanitizeFileName(baseName) + "_pattern.csv";
  }

  private static string SanitizeFileName(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return "dance";
    }

    char[] invalidChars = Path.GetInvalidFileNameChars();
    StringBuilder builder = new StringBuilder(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
      char c = value[i];
      builder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
    }

    return builder.ToString().Trim();
  }

  private static bool PatternFileExists(string fileName)
  {
    return File.Exists(Path.Combine(PatternsDirectory, fileName));
  }

  private void ApplyInspectorDanceChanges()
  {
    if (_avatarAnimator == null)
    {
      return;
    }

    RuntimeAnimatorController animatorController = _avatarAnimator.runtimeAnimatorController;
    if (animatorController != null && animatorController != _lastAppliedDanceController && animatorController != _previewDanceController)
    {
      _previewDanceController = animatorController;
      ApplyDanceController(animatorController);
      return;
    }

    if (_previewDanceController != null && _previewDanceController != _lastAppliedDanceController)
    {
      ApplyDanceController(_previewDanceController);
      return;
    }

    if (_danceControllers != null && _danceControllers.Length > 0 && _selectedDanceIndex != _lastAppliedDanceIndex)
    {
      PlayDance(_selectedDanceIndex);
    }
  }

  private void ApplyInspectorCsvChanges()
  {
    if (!_autoPlayCsvAssetOnChange || _playbackCsvAsset == _lastPlaybackCsvAsset)
    {
      return;
    }

    _lastPlaybackCsvAsset = _playbackCsvAsset;
    if (_playbackCsvAsset != null)
    {
      PlayCsv(_playbackCsvAsset);
    }
  }

  private static string GetCsvAssetDisplayName(TextAsset csvAsset)
  {
    return csvAsset != null ? csvAsset.name + ".csv" : string.Empty;
  }

  private void StopCsvCheckingAnimatorController()
  {
    if (_csvCheckingAnimator == null)
    {
      return;
    }

    _csvCheckingAnimator.runtimeAnimatorController = null;
    _csvCheckingAnimator.applyRootMotion = false;
  }

  private static Vector3 GetBoneDirection(Animator animator, HumanBodyBones start, HumanBodyBones end, bool normalize)
  {
    Transform startBone = animator.GetBoneTransform(start);
    Transform endBone = animator.GetBoneTransform(end);
    if (startBone == null || endBone == null)
    {
      return Vector3.zero;
    }

    Vector3 direction = animator.transform.InverseTransformDirection(endBone.position - startBone.position);
    if (normalize && direction.sqrMagnitude > 0.000001f)
    {
      direction.Normalize();
    }

    return direction;
  }

  private static Vector3 GetNeckDirection(Animator animator, bool normalize)
  {
    HumanBodyBones startBone = animator.GetBoneTransform(HumanBodyBones.Neck) != null ? HumanBodyBones.Neck : HumanBodyBones.UpperChest;
    if (animator.GetBoneTransform(startBone) == null)
    {
      startBone = HumanBodyBones.Chest;
    }

    return GetBoneDirection(animator, startBone, HumanBodyBones.Head, normalize);
  }

  private static Dictionary<string, Vector3> ReadSkeletonFrame(Animator animator, bool normalize)
  {
    Dictionary<string, Vector3> values = new Dictionary<string, Vector3>();
    values["neck"] = GetNeckDirection(animator, normalize);
    values["shoulder_l"] = GetBoneDirection(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, normalize);
    values["shoulder_r"] = GetBoneDirection(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, normalize);
    values["elbow_l"] = GetBoneDirection(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, normalize);
    values["elbow_r"] = GetBoneDirection(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, normalize);
    values["hip_l"] = GetBoneDirection(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, normalize);
    values["hip_r"] = GetBoneDirection(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, normalize);
    values["knee_l"] = GetBoneDirection(animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, normalize);
    values["knee_r"] = GetBoneDirection(animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, normalize);
    return values;
  }

  private static string FormatVector(Vector3 value)
  {
    return string.Format(
      CultureInfo.InvariantCulture,
      "\"({0:0.######}, {1:0.######}, {2:0.######})\"",
      value.x,
      value.y,
      value.z);
  }

  private static Vector3 ParseVector(string value)
  {
    value = value.Trim().Trim('"').Trim().Trim('(', ')');
    string[] parts = value.Split(',');
    if (parts.Length != 3)
    {
      return Vector3.zero;
    }

    return new Vector3(ParseFloat(parts[0]), ParseFloat(parts[1]), ParseFloat(parts[2]));
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
        current.Length = 0;
      }
      else
      {
        current.Append(c);
      }
    }

    result.Add(current.ToString().Trim());
    return result;
  }

  [Serializable]
  private sealed class SkeletonCsvExporter
  {
    private Animator _animator;
    private string _fileName;
    private bool _normalize;
    private Transform _hips;
    private Vector3 _initialHipsPosition;
    private Quaternion _initialAnimatorRotation;
    private readonly List<string> _lines = new List<string>();

    public void Begin(Animator animator, string fileName, bool normalize)
    {
      _animator = animator;
      _fileName = string.IsNullOrWhiteSpace(fileName) ? "dance_pattern_export.csv" : fileName;
      _normalize = normalize;
      _hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
      _initialHipsPosition = _hips != null ? _hips.position : animator.transform.position;
      _initialAnimatorRotation = animator.transform.rotation;
      _lines.Clear();
      _lines.Add(string.Join(",", CsvHeaders));
    }

    public void Sample(float time)
    {
      if (_animator == null)
      {
        return;
      }

      Dictionary<string, Vector3> frame = ReadSkeletonFrame(_animator, _normalize);
      List<string> cells = new List<string>();
      cells.Add(time.ToString("0.######", CultureInfo.InvariantCulture));
      for (int i = 1; i < CsvHeaders.Length; i++)
      {
        if (CsvHeaders[i] == "root_position")
        {
          Vector3 currentPosition = _hips != null ? _hips.position : _animator.transform.position;
          Vector3 rootDelta = Quaternion.Inverse(_initialAnimatorRotation) * (currentPosition - _initialHipsPosition);
          cells.Add(FormatVector(rootDelta));
        }
        else
        {
          cells.Add(FormatVector(frame[CsvHeaders[i]]));
        }
      }

      _lines.Add(string.Join(",", cells));
    }

    public string Finish()
    {
      Directory.CreateDirectory(PatternsDirectory);
      string path = Path.Combine(PatternsDirectory, _fileName);

      if (File.Exists(path))
      {
        return path;
      }

      File.WriteAllLines(path, _lines, Encoding.UTF8);

#if UNITY_EDITOR
      AssetDatabase.Refresh();
#endif

      return path;
    }
  }

  [Serializable]
  private sealed class SkeletonCsvPlayback
  {
    private readonly List<SkeletonFrame> _frames = new List<SkeletonFrame>();
    private readonly Dictionary<string, BoneSegment> _segments = new Dictionary<string, BoneSegment>();
    private Animator _animator;
    private bool _normalize;
    private float _smoothing = 18.0f;
    private float _time;
    private int _cursor;
    private Vector3 _playbackRootPosition;
    private Quaternion _playbackRootRotation;
    private Animator _rootReferenceAnimator;

    public int FrameCount
    {
      get { return _frames.Count; }
    }

    public void Configure(Animator animator, float smoothing, bool normalize)
    {
      _animator = animator;
      _smoothing = smoothing;
      _normalize = normalize;
      if (_animator != null && _animator != _rootReferenceAnimator)
      {
        _playbackRootPosition = _animator.transform.position;
        _playbackRootRotation = _animator.transform.rotation;
        _rootReferenceAnimator = _animator;
      }
      CacheSegments();
    }

    public bool Load(string fileName)
    {
      _frames.Clear();
      _time = 0.0f;
      _cursor = 0;

      string path = Path.Combine(PatternsDirectory, fileName);
      if (!File.Exists(path))
      {
        return false;
      }

      string[] lines = File.ReadAllLines(path);
      return LoadLines(lines);
    }

    public bool Load(TextAsset csvAsset)
    {
      _frames.Clear();
      _time = 0.0f;
      _cursor = 0;

      if (csvAsset == null || string.IsNullOrWhiteSpace(csvAsset.text))
      {
        return false;
      }

      string[] lines = csvAsset.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
      return LoadLines(lines);
    }

    private bool LoadLines(string[] lines)
    {
      if (lines.Length <= 1)
      {
        return false;
      }

      List<string> headers = SplitCsvLine(lines[0]);
      for (int i = 1; i < lines.Length; i++)
      {
        if (string.IsNullOrWhiteSpace(lines[i]))
        {
          continue;
        }

        List<string> cells = SplitCsvLine(lines[i]);
        if (cells.Count != headers.Count)
        {
          continue;
        }

        SkeletonFrame frame = new SkeletonFrame();
        frame.Time = ParseFloat(cells[0]);
        for (int cell = 1; cell < cells.Count; cell++)
        {
          frame.Values[headers[cell]] = ParseVector(cells[cell]);
        }

        _frames.Add(frame);
      }

      return _frames.Count > 0;
    }

    public bool Tick(float deltaTime, bool loop)
    {
      if (_frames.Count == 0)
      {
        return false;
      }

      _time += deltaTime;
      float lastTime = _frames[_frames.Count - 1].Time;
      if (_time > lastTime)
      {
        if (!loop)
        {
          ApplyFrame(_frames.Count - 1, 1.0f);
          return false;
        }

        _time = 0.0f;
        _cursor = 0;
      }

      while (_cursor < _frames.Count - 2 && _frames[_cursor + 1].Time <= _time)
      {
        _cursor++;
      }

      SkeletonFrame current = _frames[_cursor];
      SkeletonFrame next = _frames[Mathf.Min(_cursor + 1, _frames.Count - 1)];
      float duration = Mathf.Max(0.0001f, next.Time - current.Time);
      float blend = Mathf.Clamp01((_time - current.Time) / duration);
      ApplyBlendedFrame(current, next, blend);
      return true;
    }

    public void ApplyFrame(int frameIndex, float weight)
    {
      if (_frames.Count == 0)
      {
        return;
      }

      frameIndex = Mathf.Clamp(frameIndex, 0, _frames.Count - 1);
      ApplyBlendedFrame(_frames[frameIndex], _frames[frameIndex], 0.0f, weight);
    }

    public void Stop()
    {
      _time = 0.0f;
      _cursor = 0;
    }

    private void ApplyBlendedFrame(SkeletonFrame a, SkeletonFrame b, float blend, float weight = 1.0f)
    {
      float smoothing = 1.0f - Mathf.Exp(-_smoothing * Time.deltaTime);
      smoothing = Mathf.Clamp01(smoothing * weight);
      ApplyRootPosition(Vector3.Lerp(a.Get("root_position"), b.Get("root_position"), blend), smoothing);
      ApplyDirection("neck", Vector3.Slerp(a.Get("neck"), b.Get("neck"), blend), smoothing);
      ApplyDirection("shoulder_l", Vector3.Slerp(a.Get("shoulder_l"), b.Get("shoulder_l"), blend), smoothing);
      ApplyDirection("shoulder_r", Vector3.Slerp(a.Get("shoulder_r"), b.Get("shoulder_r"), blend), smoothing);
      ApplyDirection("elbow_l", Vector3.Slerp(a.Get("elbow_l"), b.Get("elbow_l"), blend), smoothing);
      ApplyDirection("elbow_r", Vector3.Slerp(a.Get("elbow_r"), b.Get("elbow_r"), blend), smoothing);
      ApplyDirection("hip_l", Vector3.Slerp(a.Get("hip_l"), b.Get("hip_l"), blend), smoothing);
      ApplyDirection("hip_r", Vector3.Slerp(a.Get("hip_r"), b.Get("hip_r"), blend), smoothing);
      ApplyDirection("knee_l", Vector3.Slerp(a.Get("knee_l"), b.Get("knee_l"), blend), smoothing);
      ApplyDirection("knee_r", Vector3.Slerp(a.Get("knee_r"), b.Get("knee_r"), blend), smoothing);
    }

    private void ApplyRootPosition(Vector3 localOffset, float smoothing)
    {
      if (_animator == null)
      {
        return;
      }

      Vector3 targetPosition = _playbackRootPosition + (_playbackRootRotation * localOffset);
      _animator.transform.position = Vector3.Lerp(_animator.transform.position, targetPosition, smoothing);
    }

    private void ApplyDirection(string key, Vector3 localDirection, float smoothing)
    {
      BoneSegment segment;
      if (!_segments.TryGetValue(key, out segment) || segment.Bone == null || localDirection.sqrMagnitude < 0.000001f)
      {
        return;
      }

      if (_normalize)
      {
        localDirection.Normalize();
      }

      Vector3 worldDirection = _animator.transform.TransformDirection(localDirection).normalized;
      Quaternion targetRotation = Quaternion.FromToRotation(segment.RestDirection, worldDirection) * segment.RestRotation;
      segment.Bone.rotation = Quaternion.Slerp(segment.Bone.rotation, targetRotation, smoothing);
    }

    private void CacheSegments()
    {
      _segments.Clear();
      if (_animator == null || !_animator.isHuman)
      {
        return;
      }

      AddSegment("neck", HumanBodyBones.Neck, HumanBodyBones.Head);
      if (!_segments.ContainsKey("neck"))
      {
        AddSegment("neck", HumanBodyBones.UpperChest, HumanBodyBones.Head);
      }
      if (!_segments.ContainsKey("neck"))
      {
        AddSegment("neck", HumanBodyBones.Chest, HumanBodyBones.Head);
      }

      AddSegment("shoulder_l", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
      AddSegment("shoulder_r", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
      AddSegment("elbow_l", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
      AddSegment("elbow_r", HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
      AddSegment("hip_l", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
      AddSegment("hip_r", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
      AddSegment("knee_l", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
      AddSegment("knee_r", HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
    }

    private void AddSegment(string key, HumanBodyBones start, HumanBodyBones end)
    {
      Transform startBone = _animator.GetBoneTransform(start);
      Transform endBone = _animator.GetBoneTransform(end);
      if (startBone == null || endBone == null)
      {
        return;
      }

      Vector3 restDirection = endBone.position - startBone.position;
      if (restDirection.sqrMagnitude < 0.000001f)
      {
        return;
      }

      BoneSegment segment = new BoneSegment();
      segment.Bone = startBone;
      segment.RestDirection = restDirection.normalized;
      segment.RestRotation = startBone.rotation;
      _segments[key] = segment;
    }
  }

  private sealed class SkeletonFrame
  {
    public float Time;
    public readonly Dictionary<string, Vector3> Values = new Dictionary<string, Vector3>();

    public Vector3 Get(string key)
    {
      Vector3 value;
      return Values.TryGetValue(key, out value) ? value : Vector3.zero;
    }
  }

  private sealed class BoneSegment
  {
    public Transform Bone;
    public Vector3 RestDirection;
    public Quaternion RestRotation;
  }
}
