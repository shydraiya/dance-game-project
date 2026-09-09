using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PoseComparisonTestingController : MonoBehaviour
{
    private const string PatternTestSceneName = "Pattern Test";
    private const float WebCamAvatarOffset = 2.2f;
    private static PoseComparisonTestingController _instance;

    private StreamWriter _writer;
    private string _outputPath;
    private bool _testingEnabled;
    private GameObject _webCamAvatar;
    private Animator _clonedFromAnimator;
    private int _recordCount;
    private float _webCamAngleTotal;
    private float _mergedAngleTotal;
    private int _webCamValidCount;
    private int _mergedValidCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == PatternTestSceneName &&
            FindAnyObjectByType<PoseComparisonTestingController>() == null)
        {
            new GameObject(nameof(PoseComparisonTestingController))
                .AddComponent<PoseComparisonTestingController>();
        }
    }

    private void Awake()
    {
        _instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            SetTestingEnabled(!_testingEnabled);
        }

        if (!_testingEnabled)
        {
            return;
        }

        HumanoidPoseDriver sourceDriver = PatternTestMergedPoseController.WebCamDriver;
        if (sourceDriver != null && sourceDriver.TargetAnimator != _clonedFromAnimator)
        {
            RebuildWebCamAvatar(sourceDriver);
        }
    }

    public static void RecordJudgement(
        PoseNoteReader reader, PatternFrame pattern, int patternIndex)
    {
        if (_instance != null && _instance._testingEnabled)
        {
            _instance.WriteJudgement(reader, pattern, patternIndex);
        }
    }

    private void SetTestingEnabled(bool enabled)
    {
        if (_testingEnabled == enabled)
        {
            return;
        }

        _testingEnabled = enabled;
        if (enabled)
        {
            StartRecording();
        }
        else
        {
            StopRecording();
        }
    }

    private void StartRecording()
    {
        string directory = Path.Combine(Application.persistentDataPath, "TestingMode");
        Directory.CreateDirectory(directory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        _outputPath = Path.Combine(directory, $"pose_comparison_{stamp}.csv");
        _writer = new StreamWriter(_outputPath, false, new UTF8Encoding(true));
        _writer.WriteLine(
            "recorded_utc,song_title,game_time,pattern_index,pattern_time," +
            "merged_landmarks_used," +
            "only_webcam_rank,only_webcam_score,only_webcam_average_angle,only_webcam_compared_parts," +
            "only_webcam_excluded_landmarks,only_webcam_excluded_directions," +
            "merged_rank,merged_score,merged_average_angle,merged_compared_parts," +
            "merged_excluded_landmarks,merged_excluded_directions,rank_equal,angle_delta");
        _writer.Flush();

        _recordCount = 0;
        _webCamAngleTotal = 0f;
        _mergedAngleTotal = 0f;
        _webCamValidCount = 0;
        _mergedValidCount = 0;

        HumanoidPoseDriver sourceDriver = PatternTestMergedPoseController.WebCamDriver;
        if (sourceDriver != null)
        {
            RebuildWebCamAvatar(sourceDriver);
        }

        Debug.Log($"[Testing Mode] ON | comparison CSV={_outputPath}", this);
    }

    private void StopRecording()
    {
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        if (_webCamAvatar != null)
        {
            Destroy(_webCamAvatar);
            _webCamAvatar = null;
        }
        _clonedFromAnimator = null;

        float webAverage = _webCamValidCount > 0 ? _webCamAngleTotal / _webCamValidCount : 0f;
        float mergedAverage = _mergedValidCount > 0 ? _mergedAngleTotal / _mergedValidCount : 0f;
        Debug.Log(
            $"[Testing Mode] OFF | records={_recordCount}, " +
            $"webcamAverageAngle={webAverage:0.000}, mergedAverageAngle={mergedAverage:0.000}, " +
            $"CSV={_outputPath}", this);
    }

    private void WriteJudgement(
        PoseNoteReader reader, PatternFrame pattern, int patternIndex)
    {
        if (_writer == null || reader == null || pattern == null)
        {
            return;
        }

        PoseNoteReader.JudgeDiagnostic webCam =
            reader.EvaluatePatternForTesting(pattern, false);
        PoseNoteReader.JudgeDiagnostic merged =
            reader.EvaluatePatternForTesting(pattern, true);
        float gameTime = GameManager.instance != null ? GameManager.instance.gameTime : -1f;
        string songTitle = SongSessionController.Instance != null &&
                           SongSessionController.Instance.HasSelectedSong
            ? SongSessionController.Instance.SelectedSong.title
            : string.Empty;
        float angleDelta = merged.result.averageAngle - webCam.result.averageAngle;

        _writer.WriteLine(string.Join(",", new[]
        {
            Csv(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)), Csv(songTitle),
            Number(gameTime), patternIndex.ToString(CultureInfo.InvariantCulture), Number(pattern.time),
            merged.usedMergedLandmarks ? "1" : "0",
            Csv(webCam.result.rank.ToString()), Number(webCam.result.score), Number(webCam.result.averageAngle),
            webCam.result.comparedParts.ToString(CultureInfo.InvariantCulture),
            Csv(webCam.excludedLandmarks), Csv(webCam.excludedDirections),
            Csv(merged.result.rank.ToString()), Number(merged.result.score), Number(merged.result.averageAngle),
            merged.result.comparedParts.ToString(CultureInfo.InvariantCulture),
            Csv(merged.excludedLandmarks), Csv(merged.excludedDirections),
            webCam.result.rank == merged.result.rank ? "1" : "0", Number(angleDelta)
        }));
        _writer.Flush();

        _recordCount++;
        AccumulateAngle(webCam.result, ref _webCamAngleTotal, ref _webCamValidCount);
        AccumulateAngle(merged.result, ref _mergedAngleTotal, ref _mergedValidCount);
    }

    private void RebuildWebCamAvatar(HumanoidPoseDriver sourceDriver)
    {
        if (_webCamAvatar != null)
        {
            Destroy(_webCamAvatar);
        }

        Animator sourceAnimator = sourceDriver.TargetAnimator;
        if (sourceAnimator == null)
        {
            return;
        }

        _clonedFromAnimator = sourceAnimator;
        _webCamAvatar = Instantiate(
            sourceAnimator.gameObject,
            sourceAnimator.transform.position + Vector3.right * WebCamAvatarOffset,
            sourceAnimator.transform.rotation,
            sourceAnimator.transform.parent);
        _webCamAvatar.name = sourceAnimator.gameObject.name + "_ONLY_WEBCAM_TEST";
        _webCamAvatar.transform.localScale = sourceAnimator.transform.localScale;

        // Keep the clone active so its webcam-only pose is still calculated,
        // but do not draw the comparison avatar in the game view.
        foreach (Renderer avatarRenderer in
                 _webCamAvatar.GetComponentsInChildren<Renderer>(true))
        {
            avatarRenderer.enabled = false;
        }

        foreach (MergedHumanoidPoseDriver mergedDriver in
                 _webCamAvatar.GetComponentsInChildren<MergedHumanoidPoseDriver>(true))
        {
            mergedDriver.enabled = false;
            Destroy(mergedDriver);
        }

        Animator targetAnimator = _webCamAvatar.GetComponentInChildren<Animator>();
        HumanoidPoseDriver driver = _webCamAvatar.GetComponent<HumanoidPoseDriver>();
        if (driver == null)
        {
            driver = _webCamAvatar.AddComponent<HumanoidPoseDriver>();
        }

        driver.enabled = false;
        driver.CopyDrivingConfigurationFrom(sourceDriver);
        driver.SetPoseRunner(PatternTestMergedPoseController.WebCamRunner);
        driver.SetTargetAvatar(targetAnimator, targetAnimator.transform);
        driver.SetPoseInputBlocked(false);
        driver.enabled = true;
        Debug.Log($"[Testing Mode] Webcam-only calculation avatar created (rendering disabled): {_webCamAvatar.name}", this);
    }

    private static void AccumulateAngle(
        PoseNoteReader.JudgeResult result, ref float total, ref int count)
    {
        if (result.rank != JudgeRank.None)
        {
            total += result.averageAngle;
            count++;
        }
    }

    private static string Number(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private void OnDestroy()
    {
        if (_testingEnabled)
        {
            _testingEnabled = false;
            StopRecording();
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
