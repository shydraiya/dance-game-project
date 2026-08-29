using System;
using System.Collections;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class InitialTPoseGateController : MonoBehaviour
{
    private const string PatternTestSceneName = "Pattern Test";
    private const int RequiredLandmarkCount = 29;
    private const float InitialGraceSeconds = 3.0f;
    private const float RequiredHoldSeconds = 0.6f;
    private const float SuccessDisplaySeconds = 0.5f;

    private PoseLandmarkerRunner _poseRunner;
    private HumanoidPoseDriver _poseDriver;
    private GameManager _gameManager;
    private Canvas _canvas;
    private RectTransform _silhouette;
    private Text _guideText;
    private Image[] _parts;
    private bool _latestPoseMatches;
    private bool _graceCompleted;
    private bool _completed;
    private float _graceStartedAt;
    private float _matchingSince = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == PatternTestSceneName &&
            FindAnyObjectByType<InitialTPoseGateController>() == null)
        {
            new GameObject(nameof(InitialTPoseGateController))
                .AddComponent<InitialTPoseGateController>();
        }
    }

    private IEnumerator Start()
    {
        _gameManager = GameManager.instance != null
            ? GameManager.instance
            : FindAnyObjectByType<GameManager>();
        if (_gameManager != null)
        {
            _gameManager.gamePlay = false;
        }

        CreateOverlay();
        _graceStartedAt = Time.realtimeSinceStartup;
        SetGuideText($"준비하세요... {Mathf.CeilToInt(InitialGraceSeconds)}");
        _poseDriver = FindAnyObjectByType<HumanoidPoseDriver>();
        if (_poseDriver != null)
        {
            _poseDriver.SetPoseInputBlocked(true);
        }

        float timeoutAt = Time.realtimeSinceStartup + 15f;
        while (_poseRunner == null && Time.realtimeSinceStartup < timeoutAt)
        {
            _poseRunner = FindAnyObjectByType<PoseLandmarkerRunner>();
            yield return null;
        }

        if (_poseRunner == null)
        {
            Debug.LogError("Initial T-pose gate: PoseLandmarkerRunner was not found.", this);
            yield break;
        }

        _poseRunner.PoseLandmarksUpdated += OnPoseLandmarksUpdated;
    }

    private void Update()
    {
        if (_completed)
        {
            return;
        }

        if (!_graceCompleted)
        {
            float remaining = InitialGraceSeconds - (Time.realtimeSinceStartup - _graceStartedAt);
            if (remaining > 0f)
            {
                SetGuideText($"준비하세요... {Mathf.CeilToInt(remaining)}");
                SetSilhouetteColor(new Color(1f, 1f, 1f, 0.32f));
                return;
            }

            _graceCompleted = true;
            _latestPoseMatches = false;
            _matchingSince = -1f;
            if (_poseDriver == null)
            {
                _poseDriver = FindAnyObjectByType<HumanoidPoseDriver>();
            }
            _poseDriver?.SetPoseInputBlocked(false);
            _poseDriver?.Recalibrate();
            SetGuideText("화면 중앙에서 T 포즈를 취해 주세요");
        }

        if (!_latestPoseMatches)
        {
            _matchingSince = -1f;
            SetSilhouetteColor(new Color(1f, 1f, 1f, 0.58f));
            return;
        }

        if (_matchingSince < 0f)
        {
            _matchingSince = Time.realtimeSinceStartup;
        }

        float heldFor = Time.realtimeSinceStartup - _matchingSince;
        float progress = Mathf.Clamp01(heldFor / RequiredHoldSeconds);
        SetSilhouetteColor(Color.Lerp(
            new Color(1f, 1f, 1f, 0.58f),
            new Color(0.1f, 1f, 0.25f, 0.8f),
            progress));

        if (heldFor >= RequiredHoldSeconds)
        {
            _completed = true;
            StartCoroutine(CompleteGate());
        }
    }

    private void OnPoseLandmarksUpdated(PoseLandmarkerResult result)
    {
        if (!_graceCompleted)
        {
            return;
        }

        var poses = result.poseLandmarks;
        if (poses == null || poses.Count == 0 || poses[0].landmarks == null ||
            poses[0].landmarks.Count < RequiredLandmarkCount)
        {
            _latestPoseMatches = false;
            return;
        }

        _latestPoseMatches = IsCenteredTPose(poses[0].landmarks);
    }

    private static bool IsCenteredTPose(System.Collections.Generic.IReadOnlyList<NormalizedLandmark> pose)
    {
        NormalizedLandmark leftShoulder = pose[11];
        NormalizedLandmark rightShoulder = pose[12];
        NormalizedLandmark leftElbow = pose[13];
        NormalizedLandmark rightElbow = pose[14];
        NormalizedLandmark leftWrist = pose[15];
        NormalizedLandmark rightWrist = pose[16];
        NormalizedLandmark leftHip = pose[23];
        NormalizedLandmark rightHip = pose[24];

        if (!Visible(leftShoulder) || !Visible(rightShoulder) ||
            !Visible(leftElbow) || !Visible(rightElbow) ||
            !Visible(leftWrist) || !Visible(rightWrist) ||
            !Visible(leftHip) || !Visible(rightHip))
        {
            return false;
        }

        Vector2 ls = Point(leftShoulder);
        Vector2 rs = Point(rightShoulder);
        Vector2 le = Point(leftElbow);
        Vector2 re = Point(rightElbow);
        Vector2 lw = Point(leftWrist);
        Vector2 rw = Point(rightWrist);
        Vector2 lh = Point(leftHip);
        Vector2 rh = Point(rightHip);

        Vector2 shoulderCenter = (ls + rs) * 0.5f;
        Vector2 hipCenter = (lh + rh) * 0.5f;
        Vector2 bodyCenter = (shoulderCenter + hipCenter) * 0.5f;

        bool centered = Mathf.Abs(bodyCenter.x - 0.5f) <= 0.14f &&
                        Mathf.Abs(bodyCenter.y - 0.5f) <= 0.20f;
        bool leftArm = IsStraightHorizontalArm(ls, le, lw);
        bool rightArm = IsStraightHorizontalArm(rs, re, rw);

        return centered && leftArm && rightArm;
    }

    private static bool IsStraightHorizontalArm(Vector2 shoulder, Vector2 elbow, Vector2 wrist)
    {
        Vector2 upper = elbow - shoulder;
        Vector2 lower = wrist - elbow;
        if (upper.sqrMagnitude < 0.001f || lower.sqrMagnitude < 0.001f)
        {
            return false;
        }

        float upperVerticalRatio = Mathf.Abs(upper.y) / upper.magnitude;
        float lowerVerticalRatio = Mathf.Abs(lower.y) / lower.magnitude;
        return upperVerticalRatio <= 0.34f && lowerVerticalRatio <= 0.34f &&
               Vector2.Angle(upper, lower) <= 30f;
    }

    private static bool Visible(NormalizedLandmark landmark)
    {
        return !landmark.visibility.HasValue || landmark.visibility.Value >= 0.45f;
    }

    private static Vector2 Point(NormalizedLandmark landmark)
    {
        return new Vector2(landmark.x, landmark.y);
    }

    private IEnumerator CompleteGate()
    {
        SetSilhouetteColor(new Color(0.1f, 1f, 0.25f, 0.88f));
        if (_guideText != null)
        {
            _guideText.text = "준비 완료";
            _guideText.color = new Color(0.2f, 1f, 0.35f, 1f);
        }

        yield return new WaitForSecondsRealtime(SuccessDisplaySeconds);
        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(false);
        }

        if (_gameManager == null)
        {
            _gameManager = GameManager.instance;
        }
        _gameManager?.BeginInitialCountdown();
    }

    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("Initial T-Pose Guide", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 30000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject silhouetteObject = new GameObject("T-Pose Silhouette", typeof(RectTransform), typeof(Image));
        _silhouette = silhouetteObject.GetComponent<RectTransform>();
        _silhouette.SetParent(canvasObject.transform, false);
        _silhouette.anchorMin = _silhouette.anchorMax = new Vector2(0.5f, 0.5f);
        _silhouette.sizeDelta = new Vector2(760f, 820f);

        Image silhouetteImage = silhouetteObject.GetComponent<Image>();
        silhouetteImage.sprite = Resources.Load<Sprite>("PatternTest/UI/InitialTPose");
        silhouetteImage.preserveAspect = true;
        silhouetteImage.raycastTarget = false;
        silhouetteImage.color = new Color(1f, 1f, 1f, 0.58f);
        _parts = new[] { silhouetteImage };

        GameObject textObject = new GameObject("Guide Text", typeof(RectTransform), typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(canvasObject.transform, false);
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, -405f);
        textRect.sizeDelta = new Vector2(900f, 80f);
        _guideText = textObject.GetComponent<Text>();
        _guideText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _guideText.fontSize = 38;
        _guideText.fontStyle = FontStyle.Bold;
        _guideText.alignment = TextAnchor.MiddleCenter;
        _guideText.color = Color.white;
        _guideText.text = "화면 중앙에서 T 포즈를 취해 주세요";
    }

    private void SetSilhouetteColor(Color color)
    {
        if (_parts == null)
        {
            return;
        }
        foreach (Image part in _parts)
        {
            if (part != null)
            {
                part.color = color;
            }
        }
    }

    private void SetGuideText(string text)
    {
        if (_guideText != null)
        {
            _guideText.text = text;
        }
    }

    private void OnDestroy()
    {
        if (_poseRunner != null)
        {
            _poseRunner.PoseLandmarksUpdated -= OnPoseLandmarksUpdated;
        }
        if (_poseDriver != null && !_completed)
        {
            _poseDriver.SetPoseInputBlocked(false, false);
        }
    }
}
