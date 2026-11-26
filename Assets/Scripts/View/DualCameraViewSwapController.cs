// 두 Unity 카메라의 화면 배치를 단축키로 전환한다. 영상 출력 배치용이며 자세 병합 기능과는 별개이다.

using UnityEngine;

public class DualCameraViewSwapController : MonoBehaviour
{
  [SerializeField] private Camera _modelCamera;
  [SerializeField] private Camera _webCamCamera;
  [SerializeField] private KeyCode _toggleKey = KeyCode.R;
  [SerializeField] private bool _startWithWebCamPrimary = true;
  [SerializeField] private Vector2 _previewSizePixels = new(360.0f, 202.5f);
  [SerializeField] private Vector2 _previewMarginPixels = new(24.0f, 24.0f);
  [SerializeField] private bool _showSecondaryPreview = true;

  private bool _webCamIsPrimary;

  private void Awake()
  {
    _webCamIsPrimary = _startWithWebCamPrimary;
    ApplyLayout();
  }

  // 지정 키 입력을 확인해 두 출력 카메라의 표시 배치를 전환한다.
  private void Update()
  {
    if (Input.GetKeyDown(_toggleKey))
    {
      _webCamIsPrimary = !_webCamIsPrimary;
      ApplyLayout();
    }
  }

  public void ShowModelPrimary()
  {
    _webCamIsPrimary = false;
    ApplyLayout();
  }

  public void ShowWebCamPrimary()
  {
    _webCamIsPrimary = true;
    ApplyLayout();
  }

  private void ApplyLayout()
  {
    if (_modelCamera == null || _webCamCamera == null)
    {
      Debug.LogWarning($"{nameof(DualCameraViewSwapController)} needs both cameras assigned.");
      return;
    }

    var primaryCamera = _webCamIsPrimary ? _webCamCamera : _modelCamera;
    var previewCamera = _webCamIsPrimary ? _modelCamera : _webCamCamera;

    ApplyPrimaryCamera(primaryCamera);
    ApplyPreviewCamera(previewCamera);
  }

  private void ApplyPrimaryCamera(Camera camera)
  {
    camera.enabled = true;
    camera.targetTexture = null;
    camera.rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
    camera.depth = 0.0f;
  }

  private void ApplyPreviewCamera(Camera camera)
  {
    camera.enabled = _showSecondaryPreview;
    camera.targetTexture = null;

    if (!_showSecondaryPreview)
    {
      return;
    }

    camera.rect = GetPreviewRect();
    camera.depth = 10.0f;
  }

  private Rect GetPreviewRect()
  {
    var width = Mathf.Clamp01(_previewSizePixels.x / Screen.width);
    var height = Mathf.Clamp01(_previewSizePixels.y / Screen.height);
    var marginX = Mathf.Clamp01(_previewMarginPixels.x / Screen.width);
    var marginY = Mathf.Clamp01(_previewMarginPixels.y / Screen.height);
    return new Rect(1.0f - width - marginX, 1.0f - height - marginY, width, height);
  }
}
