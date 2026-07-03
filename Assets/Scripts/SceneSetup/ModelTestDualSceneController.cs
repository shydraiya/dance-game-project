using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

public class ModelTestDualSceneController : MonoBehaviour
{
  [Header("Existing Left Scene")]
  [SerializeField] private ModelTestPoseLandmarkerSampleSetup _leftSetup;
  [SerializeField] private HumanoidPoseDriver _leftPoseDriver;
  [SerializeField] private Camera _leftModelCamera;
  [SerializeField] private Camera _leftWebCamCamera;
  [SerializeField] private Transform _leftAvatarRoot;

  [Header("Right Scene")]
  [SerializeField] private string _rightDeviceNameContains = "DroidCam";
  [SerializeField] private Vector3 _rightAvatarOffset = new(4.0f, 0.0f, 0.0f);

  private WebCamPoseLandmarkerRunner _rightRunner;
  private HumanoidPoseDriver _rightPoseDriver;
  private Camera _rightModelCamera;
  private RectTransform _rightRunnerPanel;

  private void Start()
  {
    ResolveReferences();
    DisableLegacySwapController();
    ConfigureLeftFullBody();
    ConfigureFourColumnLayout();
    ConfigureRightSide();
  }

  private void ResolveReferences()
  {
    if (_leftSetup == null)
    {
      _leftSetup = FindAnyObjectByType<ModelTestPoseLandmarkerSampleSetup>();
    }

    if (_leftPoseDriver == null)
    {
      _leftPoseDriver = FindAnyObjectByType<HumanoidPoseDriver>();
    }

    if (_leftAvatarRoot == null && _leftPoseDriver != null)
    {
      _leftAvatarRoot = _leftPoseDriver.transform;
    }

    if (_leftModelCamera == null)
    {
      var modelCameraObject = GameObject.Find("modelCamera");
      _leftModelCamera = modelCameraObject == null ? Camera.main : modelCameraObject.GetComponent<Camera>();
    }

    if (_leftWebCamCamera == null)
    {
      _leftWebCamCamera = Camera.main;
    }
  }

  private void DisableLegacySwapController()
  {
    var swapController = GetComponent<DualCameraViewSwapController>();
    if (swapController != null)
    {
      swapController.enabled = false;
    }
  }

  private void ConfigureLeftFullBody()
  {
    if (_leftPoseDriver == null)
    {
      return;
    }

    _leftPoseDriver.ConfigureDriving(false, true, true, true, true);
    _leftPoseDriver.Recalibrate();
  }

  private void ConfigureFourColumnLayout()
  {
    if (_leftSetup != null && _leftSetup.WebCamPanel != null)
    {
      SetPanelRect(_leftSetup.WebCamPanel, 0.0f, 0.25f);
    }

    if (_leftWebCamCamera != null)
    {
      _leftWebCamCamera.rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
      _leftWebCamCamera.depth = -1.0f;
    }

    if (_leftModelCamera != null)
    {
      _leftModelCamera.rect = new Rect(0.25f, 0.0f, 0.25f, 1.0f);
      _leftModelCamera.depth = 0.0f;
    }
  }

  private void ConfigureRightSide()
  {
    if (_leftSetup == null || _leftSetup.WebCamPanel == null || _leftAvatarRoot == null || _leftModelCamera == null)
    {
      Debug.LogWarning($"{nameof(ModelTestDualSceneController)} could not configure the right side because a left-side reference is missing.", this);
      return;
    }

    _rightRunnerPanel = CreateRightPanel(_leftSetup.WebCamPanel.parent as RectTransform);
    var runnerObject = new GameObject("DroidCam Pose Runner");
    _rightRunner = runnerObject.AddComponent<WebCamPoseLandmarkerRunner>();
    _rightRunner.Configure(
      _rightRunnerPanel,
      _leftSetup.BootstrapPrefab,
      _leftSetup.AnnotatableScreenPrefab,
      _leftSetup.PoseAnnotationPrefab,
      _rightDeviceNameContains);

    var rightAvatar = Instantiate(_leftAvatarRoot.gameObject, _leftAvatarRoot.position + _rightAvatarOffset, _leftAvatarRoot.rotation);
    rightAvatar.name = $"{_leftAvatarRoot.name} DroidCam";
    _rightPoseDriver = rightAvatar.GetComponentInChildren<HumanoidPoseDriver>();
    if (_rightPoseDriver != null)
    {
      _rightPoseDriver.SetPoseRunner(null);
      _rightPoseDriver.ConfigureDriving(false, true, true, true, true);
      _rightRunner.PoseLandmarksUpdated += _rightPoseDriver.ApplyPoseLandmarkerResult;
      _rightPoseDriver.Recalibrate();
    }

    var rightCameraObject = new GameObject("DroidCam modelCamera");
    _rightModelCamera = rightCameraObject.AddComponent<Camera>();
    _rightModelCamera.CopyFrom(_leftModelCamera);
    _rightModelCamera.transform.SetPositionAndRotation(_leftModelCamera.transform.position + _rightAvatarOffset, _leftModelCamera.transform.rotation);
    _rightModelCamera.rect = new Rect(0.75f, 0.0f, 0.25f, 1.0f);
    _rightModelCamera.depth = 0.0f;
    _rightModelCamera.targetTexture = null;
  }

  private RectTransform CreateRightPanel(RectTransform canvasRoot)
  {
    var rightPanelObject = new GameObject("DroidCam Preview Panel", typeof(RectTransform));
    rightPanelObject.transform.SetParent(canvasRoot, false);
    var rightPanel = (RectTransform)rightPanelObject.transform;
    SetPanelRect(rightPanel, 0.5f, 0.75f);
    return rightPanel;
  }

  private static void SetPanelRect(RectTransform panel, float minX, float maxX)
  {
    if (panel == null)
    {
      return;
    }

    panel.anchorMin = new Vector2(minX, 0.0f);
    panel.anchorMax = new Vector2(maxX, 1.0f);
    panel.pivot = new Vector2(0.5f, 0.5f);
    panel.anchoredPosition = Vector2.zero;
    panel.sizeDelta = Vector2.zero;
  }

}
