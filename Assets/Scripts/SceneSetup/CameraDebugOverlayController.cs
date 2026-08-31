using System;
using System.Collections;
using System.Linq;
using Mediapipe.Unity.Sample;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CameraDebugOverlayController : MonoBehaviour
{
    private const string PatternTestSceneName = "Pattern Test";
    private const string MergedSceneName = "webcam+droidcam+merge";
    private const string DroidCamName = "DroidCam";

    private static CameraDebugOverlayController _instance;

    private Canvas _canvas;
    private RawImage _webCamImage;
    private RawImage _droidCamImage;
    private Text _webCamLabel;
    private Text _droidCamLabel;
    private GameObject _droidCamPanel;
    private WebCamPoseLandmarkerRunner _droidCamRunner;
    private WebCamTexture _directDroidCamTexture;
    private Coroutine _droidCamStartRoutine;

    public static bool IsDebugEnabled => _instance != null && _instance._canvas != null &&
                                         _instance._canvas.gameObject.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != PatternTestSceneName && scene.name != MergedSceneName)
        {
            return;
        }

        if (FindAnyObjectByType<CameraDebugOverlayController>() == null)
        {
            new GameObject(nameof(CameraDebugOverlayController))
                .AddComponent<CameraDebugOverlayController>();
        }
    }

    public static string GetAvatarOutputMode()
    {
        return PatternTestMergedPoseController.IsMergeActive
            ? "MERGED"
            : "ONLY_WEBCAM";
    }

    private void Awake()
    {
        _instance = this;
        CreateOverlay();
        _canvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            SetDebugEnabled(!IsDebugEnabled);
        }

        if (!IsDebugEnabled)
        {
            return;
        }

        UpdateWebCamPreview();
        UpdateDroidCamPreview();
    }

    private void SetDebugEnabled(bool enabled)
    {
        _canvas.gameObject.SetActive(enabled);

        if (enabled)
        {
            ResolveDroidCamSource();
            Debug.Log($"[Camera Debug] ON | 아바타 출력={GetAvatarOutputMode()}", this);
        }
        else
        {
            StopDirectDroidCam();
            Debug.Log("[Camera Debug] OFF", this);
        }
    }

    private void UpdateWebCamPreview()
    {
        var source = ImageSourceProvider.ImageSource;
        Texture texture = source?.GetCurrentTexture();
        _webCamImage.texture = texture;
        _webCamLabel.text = texture == null
            ? "Webcam (준비 중)"
            : $"Webcam ({source.sourceName})";
    }

    private void UpdateDroidCamPreview()
    {
        if (_droidCamRunner == null)
        {
            _droidCamRunner = FindAnyObjectByType<WebCamPoseLandmarkerRunner>();
        }

        Texture texture = _droidCamRunner != null && _droidCamRunner.IsSourcePrepared
            ? _droidCamRunner.CurrentTexture
            : _directDroidCamTexture;

        bool droidCamReady = texture != null &&
                             (!(_directDroidCamTexture is WebCamTexture directTexture) ||
                              directTexture.width > 16);
        _droidCamPanel.SetActive(droidCamReady);

        _droidCamImage.texture = texture;
        string sourceName = _droidCamRunner != null
            ? _droidCamRunner.SourceName
            : _directDroidCamTexture?.deviceName;
        _droidCamLabel.text = texture == null
            ? "DroidCam (장치 없음/준비 중)"
            : $"DroidCam ({sourceName})";
    }

    private void ResolveDroidCamSource()
    {
        _droidCamRunner = FindAnyObjectByType<WebCamPoseLandmarkerRunner>();

        if (_droidCamRunner == null && _droidCamStartRoutine == null)
        {
            _droidCamStartRoutine = StartCoroutine(StartDirectDroidCam());
        }
    }

    private IEnumerator StartDirectDroidCam()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogWarning("[Camera Debug] 카메라 권한이 없어 DroidCam 화면을 열 수 없습니다.", this);
            _droidCamStartRoutine = null;
            yield break;
        }

        WebCamDevice? device = WebCamTexture.devices
            .Cast<WebCamDevice?>()
            .FirstOrDefault(candidate => candidate.HasValue && ContainsDroidCam(candidate.Value.name));

        if (!device.HasValue)
        {
            Debug.LogWarning("[Camera Debug] 이름에 DroidCam이 포함된 카메라 장치를 찾지 못했습니다.", this);
            _droidCamStartRoutine = null;
            yield break;
        }

        _directDroidCamTexture = new WebCamTexture(device.Value.name);
        _directDroidCamTexture.Play();
        _droidCamStartRoutine = null;
    }

    private void StopDirectDroidCam()
    {
        if (_droidCamStartRoutine != null)
        {
            StopCoroutine(_droidCamStartRoutine);
            _droidCamStartRoutine = null;
        }

        if (_directDroidCamTexture == null)
        {
            return;
        }

        _directDroidCamTexture.Stop();
        Destroy(_directDroidCamTexture);
        _directDroidCamTexture = null;
    }

    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("Camera Debug Overlay", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 31000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreatePreview("Webcam Debug Preview", new Vector2(16f, -16f), out _, out _webCamImage, out _webCamLabel);
        CreatePreview("DroidCam Debug Preview", new Vector2(248f, -16f), out _droidCamPanel, out _droidCamImage, out _droidCamLabel);
        _droidCamPanel.SetActive(false);
    }

    private void CreatePreview(string name, Vector2 position, out GameObject panelObject,
        out RawImage preview, out Text label)
    {
        panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(_canvas.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = position;
        panel.sizeDelta = new Vector2(220f, 150f);
        panelObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

        GameObject imageObject = new GameObject("Camera Image", typeof(RectTransform), typeof(RawImage));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(panel, false);
        imageRect.anchorMin = new Vector2(0f, 0f);
        imageRect.anchorMax = new Vector2(1f, 1f);
        imageRect.offsetMin = new Vector2(4f, 24f);
        imageRect.offsetMax = new Vector2(-4f, -4f);
        preview = imageObject.GetComponent<RawImage>();
        preview.raycastTarget = false;

        GameObject labelObject = new GameObject("Camera Label", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(panel, false);
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 2f);
        labelRect.sizeDelta = new Vector2(0f, 20f);
        label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 13;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = name;
    }

    private static bool ContainsDroidCam(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(DroidCamName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnDestroy()
    {
        StopDirectDroidCam();
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
