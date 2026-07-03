using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(AspectRatioFitter))]
public class WebCamRawImageView : MonoBehaviour
{
  [SerializeField] private RawImage _rawImage;
  [SerializeField] private AspectRatioFitter _aspectRatioFitter;
  [SerializeField] private string _deviceName;
  [SerializeField] private int _requestedWidth = 1280;
  [SerializeField] private int _requestedHeight = 720;
  [SerializeField] private int _requestedFps = 30;
  [SerializeField] private bool _mirrorHorizontally = true;
  [SerializeField] private AspectRatioFitter.AspectMode _aspectMode = AspectRatioFitter.AspectMode.FitInParent;
  [SerializeField] private Vector2 _visibleUvSize = Vector2.one;
  [SerializeField] private Vector2 _visibleUvCenter = new(0.5f, 0.5f);

  private WebCamTexture _webCamTexture;

  public Texture CurrentTexture => _webCamTexture;
  public bool IsPrepared => _webCamTexture != null && _webCamTexture.width > 16 && _webCamTexture.height > 16;
  public bool MirrorHorizontally => _mirrorHorizontally;
  public bool VideoVerticallyMirrored => _webCamTexture != null && _webCamTexture.videoVerticallyMirrored;
  public int VideoRotationAngle => _webCamTexture == null ? 0 : _webCamTexture.videoRotationAngle;
  public RectTransform ImageRectTransform => _rawImage == null ? null : _rawImage.rectTransform;

  private void Awake()
  {
    if (_rawImage == null)
    {
      _rawImage = GetComponent<RawImage>();
    }

    if (_aspectRatioFitter == null)
    {
      _aspectRatioFitter = GetComponent<AspectRatioFitter>();
    }

    if (_aspectRatioFitter == null)
    {
      _aspectRatioFitter = gameObject.AddComponent<AspectRatioFitter>();
    }

    _aspectRatioFitter.aspectMode = _aspectMode;
  }

  private IEnumerator Start()
  {
    if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
    {
      yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
    }

    if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
    {
      Debug.LogWarning($"{nameof(WebCamRawImageView)} could not get webcam permission.");
      yield break;
    }

    var deviceName = SelectDeviceName();
    if (string.IsNullOrEmpty(deviceName))
    {
      Debug.LogWarning($"{nameof(WebCamRawImageView)} could not find a webcam device.");
      yield break;
    }

    _webCamTexture = new WebCamTexture(deviceName, _requestedWidth, _requestedHeight, _requestedFps);
    _rawImage.texture = _webCamTexture;
    _webCamTexture.Play();
  }

  private void Update()
  {
    if (_webCamTexture == null || !_webCamTexture.isPlaying)
    {
      return;
    }

    var uvRect = GetVisibleUvRect();
    _rawImage.uvRect = _mirrorHorizontally
      ? new Rect(uvRect.xMax, uvRect.y, -uvRect.width, uvRect.height)
      : uvRect;

    if (_webCamTexture.width > 16 && _webCamTexture.height > 16)
    {
      _aspectRatioFitter.aspectRatio = ((float)_webCamTexture.width * uvRect.width) / (_webCamTexture.height * uvRect.height);
    }

    var rectTransform = _rawImage.rectTransform;
    rectTransform.localEulerAngles = new Vector3(0, 0, -_webCamTexture.videoRotationAngle);
  }

  private void OnDestroy()
  {
    if (_webCamTexture != null)
    {
      _webCamTexture.Stop();
      Destroy(_webCamTexture);
      _webCamTexture = null;
    }
  }

  public void SetAspectMode(AspectRatioFitter.AspectMode aspectMode)
  {
    _aspectMode = aspectMode;

    if (_aspectRatioFitter == null)
    {
      _aspectRatioFitter = GetComponent<AspectRatioFitter>();
    }

    if (_aspectRatioFitter != null)
    {
      _aspectRatioFitter.aspectMode = _aspectMode;
    }
  }

  private string SelectDeviceName()
  {
    if (!string.IsNullOrEmpty(_deviceName))
    {
      return _deviceName;
    }

    var devices = WebCamTexture.devices;
    return devices.Length > 0 ? devices[0].name : null;
  }

  private Rect GetVisibleUvRect()
  {
    var size = new Vector2(
      Mathf.Clamp(_visibleUvSize.x, 0.01f, 1.0f),
      Mathf.Clamp(_visibleUvSize.y, 0.01f, 1.0f)
    );
    var halfSize = size * 0.5f;
    var center = new Vector2(
      Mathf.Clamp(_visibleUvCenter.x, halfSize.x, 1.0f - halfSize.x),
      Mathf.Clamp(_visibleUvCenter.y, halfSize.y, 1.0f - halfSize.y)
    );

    return new Rect(center - halfSize, size);
  }
}
