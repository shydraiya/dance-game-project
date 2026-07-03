using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;
using UnityEngine.Rendering;

using Stopwatch = System.Diagnostics.Stopwatch;

public class WebCamPoseLandmarkerRunner : MonoBehaviour
{
  [Header("Scene References")]
  [SerializeField] private RectTransform _webCamPanel;
  [SerializeField] private GameObject _bootstrapPrefab;
  [SerializeField] private GameObject _annotatableScreenPrefab;
  [SerializeField] private GameObject _poseAnnotationPrefab;

  [Header("WebCam")]
  [SerializeField] private string _deviceNameContains = "DroidCam";
  [SerializeField] private int _requestedWidth = 1280;
  [SerializeField] private int _requestedHeight = 720;
  [SerializeField] private int _requestedFps = 30;
  [SerializeField] private bool _mirrorHorizontally = true;

  [Header("Pose Runner")]
  [SerializeField] private RunningMode _runningMode = RunningMode.Async;
  [SerializeField] private PoseLandmarkListAnnotation.BodyParts _visibleBodyParts = PoseLandmarkListAnnotation.BodyParts.All;
  [SerializeField] private bool _visualizeZ;

  [Header("Landmarks")]
  [SerializeField] private Color _leftLandmarkColor = Color.green;
  [SerializeField] private Color _rightLandmarkColor = Color.green;
  [SerializeField] private float _landmarkRadius = 15.0f;

  [Header("Connections")]
  [SerializeField] private Color _connectionColor = Color.white;
  [SerializeField, Range(0.0f, 1.0f)] private float _connectionWidth = 1.0f;

  [Header("Face Circle")]
  [SerializeField] private bool _drawFaceCircle = true;
  [SerializeField] private Color _faceCircleColor = Color.white;
  [SerializeField, Range(0.0f, 1.0f)] private float _faceCircleWidth = 0.35f;
  [SerializeField] private float _faceCirclePadding;
  [SerializeField] private float _faceCircleRadiusScale = 1.5f;
  [SerializeField] private Vector2 _faceCircleOffset = Vector2.zero;

  private const string BootstrapName = "Bootstrap";

  private readonly Stopwatch _stopwatch = new();
  private readonly PoseLandmarkDetectionConfig _config = new();

  private Bootstrap _bootstrap;
  private WebCamImageSource _imageSource;
  private PoseLandmarker _poseLandmarker;
  private TextureFramePool _textureFramePool;
  private Mediapipe.Unity.Screen _screen;
  private MultiPoseLandmarkListWithMaskAnnotation _annotation;
  private PoseLandmarkerResultAnnotationController _annotationController;
  private Coroutine _coroutine;
  private volatile bool _isLiveStreamRequestPending;

  public event Action<PoseLandmarkerResult> PoseLandmarksUpdated;

  public void Configure(
    RectTransform webCamPanel,
    GameObject bootstrapPrefab,
    GameObject annotatableScreenPrefab,
    GameObject poseAnnotationPrefab,
    string deviceNameContains)
  {
    _webCamPanel = webCamPanel;
    _bootstrapPrefab = bootstrapPrefab;
    _annotatableScreenPrefab = annotatableScreenPrefab;
    _poseAnnotationPrefab = poseAnnotationPrefab;
    _deviceNameContains = deviceNameContains;
  }

  private IEnumerator Start()
  {
    _bootstrap = FindBootstrap();
    yield return new WaitUntil(() => _bootstrap != null && _bootstrap.isFinished);
    Play();
  }

  private void OnDestroy()
  {
    Stop();
  }

  public void Play()
  {
    Stop();
    _stopwatch.Restart();
    _coroutine = StartCoroutine(Run());
  }

  public void Stop()
  {
    if (_coroutine != null)
    {
      StopCoroutine(_coroutine);
      _coroutine = null;
    }

    _textureFramePool?.Dispose();
    _textureFramePool = null;
    _poseLandmarker?.Close();
    _poseLandmarker = null;
    _imageSource?.Stop();
    _isLiveStreamRequestPending = false;
    _stopwatch.Stop();
  }

  private IEnumerator Run()
  {
    if (_webCamPanel == null || _annotatableScreenPrefab == null || _poseAnnotationPrefab == null)
    {
      Debug.LogError($"{nameof(WebCamPoseLandmarkerRunner)} needs panel and annotation prefabs.", this);
      yield break;
    }

    EnsureScreenAndAnnotation();
    ApplyAnnotationOptions();

    _config.VisibleBodyParts = _visibleBodyParts;
    yield return AssetLoader.PrepareAssetAsync(_config.ModelPath);

    var taskRunningMode = _runningMode == RunningMode.Async
      ? Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM
      : Mediapipe.Tasks.Vision.Core.RunningMode.VIDEO;
    _config.RunningMode = taskRunningMode;

    var options = _config.GetPoseLandmarkerOptions(taskRunningMode == Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null);
    _poseLandmarker = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);

    _imageSource = new WebCamImageSource(_deviceNameContains, _requestedWidth, _requestedHeight, _requestedFps, _mirrorHorizontally);
    yield return _imageSource.Play();

    if (!_imageSource.isPrepared)
    {
      Debug.LogError($"{nameof(WebCamPoseLandmarkerRunner)} failed to start webcam source.", this);
      yield break;
    }

    _textureFramePool = new TextureFramePool(_imageSource.textureWidth, _imageSource.textureHeight, TextureFormat.RGBA32, 10);
    _screen.Initialize(_imageSource);
    SetupAnnotationController(_annotationController, _imageSource);
    _annotationController.InitScreen(_imageSource.textureWidth, _imageSource.textureHeight);
    _annotationController.SetBodyPartsMask(_visibleBodyParts);
    ApplyAnnotationOptions();

    var transformationOptions = _imageSource.GetTransformationOptions();
    var flipHorizontally = transformationOptions.flipHorizontally;
    var flipVertically = transformationOptions.flipVertically;
    var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

    AsyncGPUReadbackRequest req = default;
    var waitUntilReqDone = new WaitUntil(() => req.done);
    var waitForEndOfFrame = new WaitForEndOfFrame();
    var result = PoseLandmarkerResult.Alloc(options.numPoses, options.outputSegmentationMasks);
    var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
    using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

    while (true)
    {
      if (_poseLandmarker.runningMode == Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM && _isLiveStreamRequestPending)
      {
        yield return waitForEndOfFrame;
        continue;
      }

      ApplyAnnotationOptions();

      if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
      {
        yield return waitForEndOfFrame;
        continue;
      }

      Mediapipe.Image image;
      switch (_config.ImageReadMode)
      {
        case ImageReadMode.GPU:
          if (!canUseGpuImage)
          {
            throw new Exception("ImageReadMode.GPU is not supported");
          }
          textureFrame.ReadTextureOnGPU(_imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
          image = textureFrame.BuildGPUImage(glContext);
          yield return waitForEndOfFrame;
          break;
        case ImageReadMode.CPU:
          yield return waitForEndOfFrame;
          textureFrame.ReadTextureOnCPU(_imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
          image = textureFrame.BuildCPUImage();
          textureFrame.Release();
          break;
        case ImageReadMode.CPUAsync:
        default:
          req = textureFrame.ReadTextureAsync(_imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
          yield return waitUntilReqDone;

          if (req.hasError)
          {
            Debug.LogWarning("Failed to read texture from the webcam source");
            continue;
          }

          image = textureFrame.BuildCPUImage();
          textureFrame.Release();
          break;
      }

      if (_poseLandmarker.runningMode == Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM)
      {
        _isLiveStreamRequestPending = true;
        _poseLandmarker.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
      }
      else if (_poseLandmarker.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
      {
        _annotationController.DrawNow(result);
        PoseLandmarksUpdated?.Invoke(result);
        DisposeAllMasks(result);
      }
      else
      {
        _annotationController.DrawNow(default);
      }
    }
  }

  private Bootstrap FindBootstrap()
  {
    var bootstrapObject = GameObject.Find(BootstrapName);
    if (bootstrapObject == null && _bootstrapPrefab != null)
    {
      bootstrapObject = Instantiate(_bootstrapPrefab);
      bootstrapObject.name = BootstrapName;
      DontDestroyOnLoad(bootstrapObject);
    }

    return bootstrapObject == null ? null : bootstrapObject.GetComponent<Bootstrap>();
  }

  private void EnsureScreenAndAnnotation()
  {
    if (_screen != null && _annotationController != null)
    {
      return;
    }

    var screenObject = Instantiate(_annotatableScreenPrefab, _webCamPanel);
    screenObject.name = "DroidCam Annotatable Screen";
    screenObject.transform.SetAsFirstSibling();

    var rectTransform = (RectTransform)screenObject.transform;
    rectTransform.anchorMin = Vector2.zero;
    rectTransform.anchorMax = Vector2.one;
    rectTransform.offsetMin = Vector2.zero;
    rectTransform.offsetMax = Vector2.zero;
    rectTransform.pivot = new Vector2(0.5f, 0.5f);

    _screen = screenObject.GetComponent<Mediapipe.Unity.Screen>();
    var annotationLayer = _screen.transform.Find("Annotation Layer");
    var annotationObject = Instantiate(_poseAnnotationPrefab, annotationLayer);
    annotationObject.name = "DroidCam Pose Annotation";

    _annotation = annotationObject.GetComponent<MultiPoseLandmarkListWithMaskAnnotation>();
    SetField(_annotation, "_screen", _screen.GetComponent<UnityEngine.UI.RawImage>());

    _annotationController = annotationLayer.gameObject.AddComponent<PoseLandmarkerResultAnnotationController>();
    SetField(_annotationController, "annotation", _annotation);
  }

  private void ApplyAnnotationOptions()
  {
    if (_annotationController != null)
    {
      SetField(_annotationController, "_visualizeZ", _visualizeZ);
      SetField(_annotationController, "_bodyPartsMask", _visibleBodyParts);
      _annotationController.SetBodyPartsMask(_visibleBodyParts);
      _annotationController.SetFaceCircleOptions(_drawFaceCircle, _faceCircleColor, _faceCircleWidth, _faceCirclePadding, _faceCircleRadiusScale, _faceCircleOffset);
    }

    if (_annotation == null)
    {
      return;
    }

    _annotation.SetLeftLandmarkColor(_leftLandmarkColor);
    _annotation.SetRightLandmarkColor(_rightLandmarkColor);
    _annotation.SetLandmarkRadius(_landmarkRadius);
    _annotation.SetConnectionColor(_connectionColor);
    _annotation.SetConnectionWidth(_connectionWidth);
    _annotation.SetFaceCircleOptions(_drawFaceCircle, _faceCircleColor, _faceCircleWidth, _faceCirclePadding, _faceCircleRadiusScale, _faceCircleOffset);
  }

  private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Mediapipe.Image image, long timestamp)
  {
    _annotationController.DrawLater(result);
    PoseLandmarksUpdated?.Invoke(result);
    DisposeAllMasks(result);
    _isLiveStreamRequestPending = false;
  }

  private void DisposeAllMasks(PoseLandmarkerResult result)
  {
    if (result.segmentationMasks == null)
    {
      return;
    }

    foreach (var mask in result.segmentationMasks)
    {
      mask.Dispose();
    }
  }

  private long GetCurrentTimestampMillisec()
  {
    return _stopwatch.IsRunning ? _stopwatch.ElapsedTicks / TimeSpan.TicksPerMillisecond : -1;
  }

  private static void SetupAnnotationController<T>(AnnotationController<T> annotationController, ImageSource imageSource) where T : HierarchicalAnnotation
  {
    annotationController.isMirrored = false;
    annotationController.imageSize = new Vector2Int(imageSource.textureWidth, imageSource.textureHeight);
  }

  private static void SetField(object target, string fieldName, object value)
  {
    var type = target.GetType();
    while (type != null)
    {
      var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
      if (field != null)
      {
        field.SetValue(target, value);
        return;
      }
      type = type.BaseType;
    }

    Debug.LogWarning($"Could not find field {fieldName} on {target.GetType().Name}");
  }

  private sealed class WebCamImageSource : ImageSource
  {
    private readonly string _deviceNameContains;
    private readonly int _requestedWidth;
    private readonly int _requestedHeight;
    private readonly int _requestedFps;
    private WebCamTexture _webCamTexture;

    public WebCamImageSource(string deviceNameContains, int requestedWidth, int requestedHeight, int requestedFps, bool mirrorHorizontally)
    {
      _deviceNameContains = deviceNameContains;
      _requestedWidth = requestedWidth;
      _requestedHeight = requestedHeight;
      _requestedFps = requestedFps;
      isHorizontallyFlipped = mirrorHorizontally;
      resolution = new ResolutionStruct(requestedWidth, requestedHeight, requestedFps);
    }

    public override string sourceName => _webCamTexture == null ? null : _webCamTexture.deviceName;
    public override string[] sourceCandidateNames => WebCamTexture.devices.Select(device => device.name).ToArray();
    public override ResolutionStruct[] availableResolutions => new[] { resolution };
    public override bool isPrepared => _webCamTexture != null && _webCamTexture.width > 16 && _webCamTexture.height > 16;
    public override bool isPlaying => _webCamTexture != null && _webCamTexture.isPlaying;
    public override int textureWidth => isPrepared ? _webCamTexture.width : _requestedWidth;
    public override int textureHeight => isPrepared ? _webCamTexture.height : _requestedHeight;
    public override bool isVerticallyFlipped => _webCamTexture != null && _webCamTexture.videoVerticallyMirrored;
    public override RotationAngle rotation => _webCamTexture == null ? RotationAngle.Rotation0 : (RotationAngle)_webCamTexture.videoRotationAngle;

    public override void SelectSource(int sourceId)
    {
    }

    public override IEnumerator Play()
    {
      if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
      {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
      }

      if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
      {
        Debug.LogWarning($"{nameof(WebCamPoseLandmarkerRunner)} could not get webcam permission.");
        yield break;
      }

      var deviceName = SelectDeviceName();
      if (string.IsNullOrEmpty(deviceName))
      {
        Debug.LogWarning($"{nameof(WebCamPoseLandmarkerRunner)} could not find a webcam device.");
        yield break;
      }

      _webCamTexture = new WebCamTexture(deviceName, _requestedWidth, _requestedHeight, _requestedFps);
      _webCamTexture.Play();

      const int timeoutFrame = 2000;
      var count = 0;
      yield return new WaitUntil(() => count++ > timeoutFrame || isPrepared);
    }

    public override IEnumerator Resume()
    {
      if (_webCamTexture != null && !_webCamTexture.isPlaying)
      {
        _webCamTexture.Play();
      }

      yield return null;
    }

    public override void Pause()
    {
      _webCamTexture?.Pause();
    }

    public override void Stop()
    {
      if (_webCamTexture == null)
      {
        return;
      }

      _webCamTexture.Stop();
      UnityEngine.Object.Destroy(_webCamTexture);
      _webCamTexture = null;
    }

    public override Texture GetCurrentTexture()
    {
      return _webCamTexture;
    }

    private string SelectDeviceName()
    {
      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        return null;
      }

      if (!string.IsNullOrWhiteSpace(_deviceNameContains))
      {
        var matchedDevice = devices.FirstOrDefault(device => device.name.IndexOf(_deviceNameContains, StringComparison.OrdinalIgnoreCase) >= 0);
        if (!string.IsNullOrEmpty(matchedDevice.name))
        {
          return matchedDevice.name;
        }
      }

      return devices.Length > 1 ? devices[1].name : devices[0].name;
    }
  }
}
