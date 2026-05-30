using System.Reflection;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

public class ModelTestPoseLandmarkerSampleSetup : MonoBehaviour
{
  [Header("Scene References")]
  [SerializeField] private RectTransform _webCamPanel;
  [SerializeField] private GameObject _bootstrapPrefab;
  [SerializeField] private GameObject _annotatableScreenPrefab;
  [SerializeField] private GameObject _poseAnnotationPrefab;
  [SerializeField] private PoseLandmarkerRunner _poseRunner;

  [Header("Pose Runner")]
  [SerializeField] private PoseLandmarkListAnnotation.BodyParts _visibleBodyParts = PoseLandmarkListAnnotation.BodyParts.All;
  [SerializeField] private bool _visualizeZ = false;

  [Header("Landmarks")]
  [SerializeField] private Color _leftLandmarkColor = Color.green;
  [SerializeField] private Color _rightLandmarkColor = Color.green;
  [SerializeField] private float _landmarkRadius = 15.0f;

  [Header("Connections")]
  [SerializeField] private Color _connectionColor = Color.white;
  [SerializeField, Range(0.0f, 1.0f)] private float _connectionWidth = 1.0f;

  [Header("Face Circle")]
  [SerializeField] private bool _drawFaceCircle = true;
  [SerializeField] private Color _faceCircleColor = Color.cyan;
  [SerializeField, Range(0.0f, 1.0f)] private float _faceCircleWidth = 0.35f;
  [SerializeField] private float _faceCirclePadding = 0.0f;
  [SerializeField] private float _faceCircleRadiusScale = 0.6f;
  [SerializeField] private Vector2 _faceCircleOffset = Vector2.zero;

  private MultiPoseLandmarkListWithMaskAnnotation _annotation;
  private PoseLandmarkerResultAnnotationController _annotationController;
  private PoseLandmarkerRunner _runner;

  public PoseLandmarkerRunner PoseRunner => _runner;
  public RectTransform WebCamPanel => _webCamPanel;
  public GameObject BootstrapPrefab => _bootstrapPrefab;
  public GameObject AnnotatableScreenPrefab => _annotatableScreenPrefab;
  public GameObject PoseAnnotationPrefab => _poseAnnotationPrefab;

  private void Awake()
  {
    ConfigureCanvasForSampleAnnotations();
    var screen = InstantiateScreen();
    var annotationController = AddAnnotation(screen);
    var runner = ResolvePoseRunner();

    SetField(runner, "_bootstrapPrefab", _bootstrapPrefab);
    SetField(runner, "screen", screen);
    SetField(runner, "_poseLandmarkerResultAnnotationController", annotationController);
    _runner = runner;
    _annotationController = annotationController;
    ApplyAnnotationOptions();
    runner.runningMode = RunningMode.Async;
  }

  private void OnValidate()
  {
    ApplyAnnotationOptions();
  }

  private void ConfigureCanvasForSampleAnnotations()
  {
    var canvas = GetComponent<Canvas>();
    if (canvas == null)
    {
      return;
    }

    canvas.renderMode = RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = Camera.main;
    canvas.planeDistance = 100.0f;
  }

  private Mediapipe.Unity.Screen InstantiateScreen()
  {
    var screenObject = Instantiate(_annotatableScreenPrefab, _webCamPanel);
    screenObject.name = "Sample Annotatable Screen";

    var rectTransform = (RectTransform)screenObject.transform;
    rectTransform.anchorMin = Vector2.zero;
    rectTransform.anchorMax = Vector2.one;
    rectTransform.offsetMin = Vector2.zero;
    rectTransform.offsetMax = Vector2.zero;
    rectTransform.pivot = new Vector2(0.5f, 0.5f);

    return screenObject.GetComponent<Mediapipe.Unity.Screen>();
  }

  private PoseLandmarkerResultAnnotationController AddAnnotation(Mediapipe.Unity.Screen screen)
  {
    var annotationLayer = screen.transform.Find("Annotation Layer");
    var annotationObject = Instantiate(_poseAnnotationPrefab, annotationLayer);
    annotationObject.name = "MultiPoseLandmarkListWithMaskAnnotation";

    _annotation = annotationObject.GetComponent<MultiPoseLandmarkListWithMaskAnnotation>();
    SetField(_annotation, "_screen", screen.GetComponent<UnityEngine.UI.RawImage>());

    var controller = annotationLayer.gameObject.AddComponent<PoseLandmarkerResultAnnotationController>();
    SetField(controller, "annotation", _annotation);
    return controller;
  }

  private PoseLandmarkerRunner ResolvePoseRunner()
  {
    if (_poseRunner != null)
    {
      return _poseRunner;
    }

    _poseRunner = FindAnyObjectByType<PoseLandmarkerRunner>();
    if (_poseRunner != null)
    {
      return _poseRunner;
    }

    Debug.LogWarning($"{nameof(ModelTestPoseLandmarkerSampleSetup)} could not find a scene PoseLandmarkerRunner, so it created one on this GameObject. For the final setup, place a Solution object in the scene and assign its PoseLandmarkerRunner here.", this);
    _poseRunner = gameObject.AddComponent<PoseLandmarkerRunner>();
    return _poseRunner;
  }

  private void ApplyAnnotationOptions()
  {
    if (_runner != null)
    {
      _runner.VisibleBodyParts = _visibleBodyParts;
      SetField(_runner, "_drawFaceCircle", _drawFaceCircle);
      SetField(_runner, "_faceCircleColor", _faceCircleColor);
      SetField(_runner, "_faceCircleWidth", _faceCircleWidth);
      SetField(_runner, "_faceCirclePadding", _faceCirclePadding);
      SetField(_runner, "_faceCircleRadiusScale", _faceCircleRadiusScale);
      SetField(_runner, "_faceCircleOffset", _faceCircleOffset);
    }

    if (_annotationController != null)
    {
      SetField(_annotationController, "_visualizeZ", _visualizeZ);
      SetField(_annotationController, "_bodyPartsMask", _visibleBodyParts);
      _annotationController.SetBodyPartsMask(_visibleBodyParts);
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
    _annotation.SetFaceCircleOptions(
      _drawFaceCircle,
      _faceCircleColor,
      _faceCircleWidth,
      _faceCirclePadding,
      _faceCircleRadiusScale,
      _faceCircleOffset
    );
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
}
