using UnityEngine;
using UnityEngine.UI;

public class ModelWebCamViewSwapController : MonoBehaviour
{
  [SerializeField] private Camera _modelCamera;
  [SerializeField] private RectTransform _webCamPanel;
  [SerializeField] private WebCamRawImageView _webCamView;
  [SerializeField] private KeyCode _toggleKey = KeyCode.R;
  [SerializeField] private Vector2 _previewSize = new(360.0f, 202.5f);
  [SerializeField] private Vector2 _previewMargin = new(24.0f, 24.0f);
  [SerializeField] private int _modelPreviewWidth = 1280;
  [SerializeField] private int _modelPreviewHeight = 720;
  [SerializeField] private bool _startWithWebCamPrimary = true;

  private GameObject _modelPreviewPanel;
  private RawImage _modelPreviewImage;
  private Camera _modelPreviewCamera;
  private RenderTexture _modelPreviewTexture;
  private bool _webCamIsPrimary;

  private void Awake()
  {
    if (_modelCamera == null)
    {
      _modelCamera = Camera.main;
    }

    if (_webCamView == null)
    {
      _webCamView = FindAnyObjectByType<WebCamRawImageView>();
    }

    if (_webCamPanel == null && _webCamView != null)
    {
      _webCamPanel = _webCamView.transform.parent as RectTransform;
    }

    CreateModelPreview();
    _webCamIsPrimary = _startWithWebCamPrimary;

    if (_webCamIsPrimary)
    {
      ApplyWebCamPrimaryLayout();
    }
    else
    {
      ApplyModelPrimaryLayout();
    }
  }

  private void Update()
  {
    if (Input.GetKeyDown(_toggleKey))
    {
      _webCamIsPrimary = !_webCamIsPrimary;

      if (_webCamIsPrimary)
      {
        ApplyWebCamPrimaryLayout();
      }
      else
      {
        ApplyModelPrimaryLayout();
      }
    }
  }

  private void LateUpdate()
  {
    if (_modelPreviewCamera == null || _modelCamera == null || !_modelPreviewCamera.enabled)
    {
      return;
    }

    _modelPreviewCamera.transform.SetPositionAndRotation(_modelCamera.transform.position, _modelCamera.transform.rotation);
  }

  private void OnDestroy()
  {
    if (_modelPreviewTexture != null)
    {
      _modelPreviewTexture.Release();
      Destroy(_modelPreviewTexture);
    }

    if (_modelPreviewCamera != null)
    {
      Destroy(_modelPreviewCamera.gameObject);
    }
  }

  private void ApplyModelPrimaryLayout()
  {
    if (_modelCamera != null)
    {
      _modelCamera.targetTexture = null;
      _modelCamera.rect = new Rect(0, 0, 1, 1);
    }

    if (_modelPreviewCamera != null)
    {
      _modelPreviewCamera.enabled = false;
    }

    SetTopRightPreview(_webCamPanel);
    _webCamView?.SetAspectMode(AspectRatioFitter.AspectMode.FitInParent);

    if (_modelPreviewPanel != null)
    {
      _modelPreviewPanel.SetActive(false);
    }
  }

  private void ApplyWebCamPrimaryLayout()
  {
    if (_modelCamera != null)
    {
      EnsureModelPreviewCamera();
      _modelCamera.rect = new Rect(0, 0, 1, 1);
    }

    SetFullScreen(_webCamPanel);
    _webCamView?.SetAspectMode(AspectRatioFitter.AspectMode.EnvelopeParent);

    if (_modelPreviewPanel != null)
    {
      _modelPreviewPanel.SetActive(true);
    }
  }

  private void CreateModelPreview()
  {
    if (_modelPreviewPanel != null)
    {
      return;
    }

    _modelPreviewPanel = new GameObject("Model Preview Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
    _modelPreviewPanel.transform.SetParent(transform, false);
    _modelPreviewImage = _modelPreviewPanel.GetComponent<RawImage>();
    _modelPreviewImage.raycastTarget = false;

    var rectTransform = (RectTransform)_modelPreviewPanel.transform;
    SetTopRightPreview(rectTransform);
    _modelPreviewPanel.SetActive(false);
  }

  private void EnsureModelPreviewTexture()
  {
    if (_modelPreviewTexture != null)
    {
      return;
    }

    _modelPreviewTexture = new RenderTexture(_modelPreviewWidth, _modelPreviewHeight, 24)
    {
      name = "Model Preview RenderTexture"
    };
    _modelPreviewTexture.Create();

    if (_modelPreviewImage != null)
    {
      _modelPreviewImage.texture = _modelPreviewTexture;
    }
  }

  private void EnsureModelPreviewCamera()
  {
    EnsureModelPreviewTexture();

    if (_modelPreviewCamera == null)
    {
      var previewCameraObject = new GameObject("Model Preview Camera");
      _modelPreviewCamera = previewCameraObject.AddComponent<Camera>();
    }

    _modelPreviewCamera.CopyFrom(_modelCamera);
    _modelPreviewCamera.transform.SetPositionAndRotation(_modelCamera.transform.position, _modelCamera.transform.rotation);
    _modelPreviewCamera.targetTexture = _modelPreviewTexture;
    _modelPreviewCamera.rect = new Rect(0, 0, 1, 1);
    _modelPreviewCamera.enabled = true;
  }

  private void SetTopRightPreview(RectTransform rectTransform)
  {
    if (rectTransform == null)
    {
      return;
    }

    rectTransform.anchorMin = Vector2.one;
    rectTransform.anchorMax = Vector2.one;
    rectTransform.pivot = Vector2.one;
    rectTransform.anchoredPosition = new Vector2(-_previewMargin.x, -_previewMargin.y);
    rectTransform.sizeDelta = _previewSize;
  }

  private void SetFullScreen(RectTransform rectTransform)
  {
    if (rectTransform == null)
    {
      return;
    }

    rectTransform.anchorMin = Vector2.zero;
    rectTransform.anchorMax = Vector2.one;
    rectTransform.pivot = new Vector2(0.5f, 0.5f);
    rectTransform.anchoredPosition = Vector2.zero;
    rectTransform.sizeDelta = Vector2.zero;
  }
}
