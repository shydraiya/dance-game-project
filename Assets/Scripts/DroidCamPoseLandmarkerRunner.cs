using System.Collections;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;
using UnityEngine.Rendering;

using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class DroidCamPoseLandmarkerRunner : MonoBehaviour
{
  [SerializeField] private Mediapipe.Unity.Screen screen;
  [SerializeField] private PoseLandmarkerResultAnnotationController annotationController;
  [SerializeField] private string[] droidCamKeywords = { "droid", "droidcam" };
  [SerializeField] private int preferredWidth = 1280;

  private readonly PoseLandmarkDetectionConfig config = new();
  private readonly Stopwatch stopwatch = new();
  private WebCamSource imageSource;
  private Mediapipe.Unity.Experimental.TextureFramePool textureFramePool;
  private PoseLandmarker taskApi;
  private Coroutine coroutine;
  private volatile bool isLiveStreamRequestPending;

  public void Configure(Mediapipe.Unity.Screen targetScreen, PoseLandmarkerResultAnnotationController targetAnnotationController)
  {
    screen = targetScreen;
    annotationController = targetAnnotationController;
  }

  private IEnumerator Start()
  {
    var bootstrap = FindFirstObjectByType<Bootstrap>();
    if (bootstrap == null)
    {
      Debug.LogError("[DroidCamPose] Bootstrap was not found. DroidCam pose runner cannot start.");
      yield break;
    }

    yield return new WaitUntil(() => bootstrap.isFinished);
    Play();
  }

  private void OnDestroy()
  {
    Stop();
  }

  public void Play()
  {
    if (coroutine != null)
    {
      StopCoroutine(coroutine);
    }

    stopwatch.Restart();
    coroutine = StartCoroutine(Run());
  }

  public void Stop()
  {
    if (coroutine != null)
    {
      StopCoroutine(coroutine);
      coroutine = null;
    }

    stopwatch.Stop();
    textureFramePool?.Dispose();
    textureFramePool = null;
    imageSource?.Stop();
    taskApi?.Close();
    taskApi = null;
    isLiveStreamRequestPending = false;
  }

  private IEnumerator Run()
  {
    if (screen == null || annotationController == null)
    {
      Debug.LogError("[DroidCamPose] Screen or annotation controller is missing.");
      yield break;
    }

    config.ImageReadMode = ImageReadMode.CPU;
    config.RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM;
    config.Delegate = Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU;
    config.OutputSegmentationMasks = false;
    config.VisibleBodyParts = PoseLandmarkListAnnotation.BodyParts.All & ~PoseLandmarkListAnnotation.BodyParts.Face;

    Debug.Log("[DroidCamPose] Starting DroidCam pose landmarker.");
    Debug.Log($"[DroidCamPose] Delegate = {config.Delegate}");
    Debug.Log($"[DroidCamPose] Image Read Mode = {config.ImageReadMode}");
    Debug.Log($"[DroidCamPose] Model = {config.ModelName}");
    Debug.Log($"[DroidCamPose] Running Mode = {config.RunningMode}");
    Debug.Log($"[DroidCamPose] NumPoses = {config.NumPoses}");
    Debug.Log($"[DroidCamPose] MinPoseDetectionConfidence = {config.MinPoseDetectionConfidence}");
    Debug.Log($"[DroidCamPose] MinPosePresenceConfidence = {config.MinPosePresenceConfidence}");
    Debug.Log($"[DroidCamPose] MinTrackingConfidence = {config.MinTrackingConfidence}");
    Debug.Log($"[DroidCamPose] OutputSegmentationMasks = {config.OutputSegmentationMasks}");
    Debug.Log($"[DroidCamPose] VisibleBodyParts = {config.VisibleBodyParts}");
    yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

    var options = config.GetPoseLandmarkerOptions(OnPoseLandmarkDetectionOutput);
    taskApi = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
    imageSource = BuildDroidCamSource();

    yield return imageSource.Play();

    if (!imageSource.isPrepared)
    {
      Debug.LogError("[DroidCamPose] Failed to start DroidCam image source.");
      yield break;
    }
    Debug.Log($"[DroidCamPose] Source = {imageSource.sourceName}");
    Debug.Log($"[DroidCamPose] Texture = {imageSource.textureWidth}x{imageSource.textureHeight}, rotation = {imageSource.rotation}, frontFacing = {imageSource.isFrontFacing}, verticallyFlipped = {imageSource.isVerticallyFlipped}");

    textureFramePool = new Mediapipe.Unity.Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

    screen.Initialize(imageSource);
    SetupAnnotationController(annotationController, imageSource);
    annotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);
    annotationController.SetBodyPartsMask(config.VisibleBodyParts);
    annotationController.SetFaceCircleOptions(true, Color.white, 1.0f, 1.0f, 1.5f, Vector2.zero);

    var transformationOptions = imageSource.GetTransformationOptions();
    var flipHorizontally = transformationOptions.flipHorizontally;
    var flipVertically = transformationOptions.flipVertically;
    var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

    var waitForEndOfFrame = new WaitForEndOfFrame();

    while (true)
    {
      if (isLiveStreamRequestPending)
      {
        yield return waitForEndOfFrame;
        continue;
      }

      if (!textureFramePool.TryGetTextureFrame(out var textureFrame))
      {
        yield return waitForEndOfFrame;
        continue;
      }

      yield return waitForEndOfFrame;
      textureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
      var image = textureFrame.BuildCPUImage();
      textureFrame.Release();

      isLiveStreamRequestPending = true;
      taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
    }
  }

  private WebCamSource BuildDroidCamSource()
  {
    return new WebCamSource(
      preferredWidth,
      new[]
      {
        new ImageSource.ResolutionStruct(640, 480, 30),
        new ImageSource.ResolutionStruct(1280, 720, 30),
        new ImageSource.ResolutionStruct(1920, 1080, 30),
      },
      droidCamKeywords
    );
  }

  private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Mediapipe.Image image, long timestamp)
  {
    PoseLandmarkFusionStore.Submit(PoseCameraSource.DroidCam, result, timestamp);
    annotationController.DrawLater(result);
    DisposeAllMasks(result);
    isLiveStreamRequestPending = false;
  }

  private void SetupAnnotationController(PoseLandmarkerResultAnnotationController controller, ImageSource source)
  {
    controller.isMirrored = false;
    controller.imageSize = new Vector2Int(source.textureWidth, source.textureHeight);
  }

  private long GetCurrentTimestampMillisec() =>
    stopwatch.IsRunning ? stopwatch.ElapsedTicks / System.TimeSpan.TicksPerMillisecond : -1;

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
}
