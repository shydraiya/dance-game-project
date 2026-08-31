using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PatternTestMergedPoseController : MonoBehaviour
{
    private const string PatternTestSceneName = "Pattern Test";
    private const string DroidCamName = "DroidCam";

    private static PatternTestMergedPoseController _instance;

    private ModelTestPoseLandmarkerSampleSetup _sampleSetup;
    private HumanoidPoseDriver _webCamDriver;
    private WebCamPoseLandmarkerRunner _droidCamRunner;
    private MergedHumanoidPoseDriver _mergedDriver;
    private RectTransform _runnerPanel;
    private Animator _configuredAnimator;

    public static bool IsMergeActive => _instance != null &&
                                        _instance._mergedDriver != null &&
                                        _instance._mergedDriver.enabled &&
                                        _instance._mergedDriver.IsUsingDroidPose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == PatternTestSceneName &&
            FindAnyObjectByType<PatternTestMergedPoseController>() == null)
        {
            new GameObject(nameof(PatternTestMergedPoseController))
                .AddComponent<PatternTestMergedPoseController>();
        }
    }

    private void Awake()
    {
        _instance = this;
    }

    private IEnumerator Start()
    {
        yield return null;

        if (!HasDroidCamDevice())
        {
            Debug.Log("Pattern Test: DroidCam 장치가 없어 Webcam 단독 아바타를 사용합니다.", this);
            yield break;
        }

        _sampleSetup = FindAnyObjectByType<ModelTestPoseLandmarkerSampleSetup>();
        _webCamDriver = FindAnyObjectByType<HumanoidPoseDriver>();
        if (_sampleSetup == null || _sampleSetup.PoseRunner == null ||
            _sampleSetup.WebCamPanel == null || _webCamDriver == null)
        {
            Debug.LogWarning("Pattern Test: 병합 포즈에 필요한 Webcam 설정 또는 아바타 드라이버를 찾지 못했습니다.", this);
            yield break;
        }

        CreateDroidCamRunner();

        float timeoutAt = Time.realtimeSinceStartup + 12f;
        while (!_droidCamRunner.IsSourcePrepared && Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        if (!_droidCamRunner.IsSourcePrepared)
        {
            Debug.LogWarning("Pattern Test: DroidCam 영상 준비에 실패하여 Webcam 단독 아바타를 유지합니다.", this);
            yield break;
        }

        _mergedDriver = _webCamDriver.gameObject.AddComponent<MergedHumanoidPoseDriver>();
        _webCamDriver.SetPoseInputBlocked(true);
        ConfigureCurrentAvatar();
        Debug.Log("Pattern Test: Webcam 90% + DroidCam 10% visibility 병합 아바타를 활성화했습니다.", this);

        while (true)
        {
            if (_webCamDriver != null && _webCamDriver.TargetAnimator != _configuredAnimator)
            {
                ConfigureCurrentAvatar();
            }
            yield return null;
        }
    }

    private void CreateDroidCamRunner()
    {
        GameObject panelObject = new GameObject("Pattern Test DroidCam Runner Panel", typeof(RectTransform));
        _runnerPanel = panelObject.GetComponent<RectTransform>();
        _runnerPanel.SetParent(_sampleSetup.WebCamPanel.parent, false);
        _runnerPanel.anchorMin = _runnerPanel.anchorMax = Vector2.zero;
        _runnerPanel.sizeDelta = Vector2.one;
        _runnerPanel.anchoredPosition = new Vector2(-100f, -100f);

        GameObject runnerObject = new GameObject("Pattern Test DroidCam Pose Runner");
        _droidCamRunner = runnerObject.AddComponent<WebCamPoseLandmarkerRunner>();
        _droidCamRunner.Configure(
            _runnerPanel,
            _sampleSetup.BootstrapPrefab,
            _sampleSetup.AnnotatableScreenPrefab,
            _sampleSetup.PoseAnnotationPrefab,
            DroidCamName);
    }

    private void ConfigureCurrentAvatar()
    {
        if (_mergedDriver == null || _webCamDriver == null ||
            _webCamDriver.TargetAnimator == null)
        {
            return;
        }

        _configuredAnimator = _webCamDriver.TargetAnimator;
        _mergedDriver.Configure(
            _configuredAnimator,
            _webCamDriver.ModelRoot,
            _sampleSetup.PoseRunner,
            _droidCamRunner);
        _mergedDriver.SetWeights(0.9f, 0.1f);
    }

    private static bool HasDroidCamDevice()
    {
        return WebCamTexture.devices.Any(device =>
            device.name.IndexOf(DroidCamName, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void OnDestroy()
    {
        if (_webCamDriver != null)
        {
            _webCamDriver.SetPoseInputBlocked(false);
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
