using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PatternManager : MonoBehaviour
{
    [Header("# References")]
    [SerializeField] private PatternLoader patternLoader;
    [SerializeField] private PoseNoteReader poseNoteReader;
    [SerializeField] private PatternPosePlayer patternPosePlayer;

    [Header("# Pattern Settings")]
    [SerializeField] private float patternLoadLeadTime = 2.0f;
    [SerializeField] private PosePreviewController posePreviewController;

    private List<PatternFrame> patterns = new List<PatternFrame>();

    // 다음에 패턴 로더를 호출할 Patterns 인덱스
    private int nextLoadIndex;

    // 다음에 판정 함수를 호출할 Patterns 인덱스
    private int nextJudgeIndex;

    private float lastGameTime;
    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (patternLoader == null)
        {
            patternLoader = FindAnyObjectByType<PatternLoader>();
        }

        if (poseNoteReader == null)
        {
            poseNoteReader = FindAnyObjectByType<PoseNoteReader>();
        }

        if (patternLoader == null)
        {
            Debug.LogError(
                "PatternManager: PatternLoader를 찾을 수 없습니다.",
                this
            );

            return;
        }

        if (poseNoteReader == null)
        {
            Debug.LogError(
                "PatternManager: PoseNoteReader를 찾을 수 없습니다.",
                this
            );

            return;
        }

        if (posePreviewController == null)
        {
            posePreviewController =
                FindAnyObjectByType<PosePreviewController>();
        }

        if (posePreviewController == null)
        {
            Debug.LogError(
                "PatternManager: PosePreviewController를 찾을 수 없습니다.",
                this
            );

            return;
        }

        patterns = new List<PatternFrame>(patternLoader.Patterns);

        patterns.Sort(
            (left, right) => left.time.CompareTo(right.time)
        );

        nextLoadIndex = 0;
        nextJudgeIndex = 0;
        lastGameTime = 0.0f;
        initialized = true;

        Debug.Log(
            $"PatternManager initialized: {patterns.Count} patterns",
            this
        );

        ProcessPatternLoadEvents(0.0f);
}

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (GameManager.instance == null)
        {
            return;
        }

        float currentGameTime = GameManager.instance.gameTime;

        // 게임 시간이 이전 값보다 작아진 경우
        // 게임 재시작 또는 시간 초기화로 간주
        if (currentGameTime < lastGameTime)
        {
            ResetPatternProgress();
        }

        lastGameTime = currentGameTime;

        if (!GameManager.instance.gamePlay)
        {
            return;
        }

        ProcessPatternLoadEvents(currentGameTime);
        ProcessJudgeEvents(currentGameTime);
    }

    private void ProcessPatternLoadEvents(float currentGameTime)
    {
        while (nextLoadIndex < patterns.Count)
        {
            PatternFrame pattern = patterns[nextLoadIndex];

            float loadTime =
                Mathf.Max(0.0f, pattern.time - patternLoadLeadTime);

            if (currentGameTime < loadTime)
            {
                break;
            }

            CallPatternLoader(pattern, nextLoadIndex);

            nextLoadIndex++;
        }
    }

    private void ProcessJudgeEvents(float currentGameTime)
    {
        while (nextJudgeIndex < patterns.Count)
        {
            PatternFrame pattern = patterns[nextJudgeIndex];

            if (currentGameTime < pattern.time)
            {
                break;
            }

            CallJudgeFunction(pattern, nextJudgeIndex);

            nextJudgeIndex++;
        }
    }

    private void CallPatternLoader(
        PatternFrame pattern,
        int patternIndex
    )
    {
        if (posePreviewController == null)
        {
            Debug.LogError(
                "PatternManager: PosePreviewController가 " +
                "연결되지 않았습니다.",
                this
            );

            return;
        }

        // 패턴 판정 시간 약 2초 전에 마네킹 생성
        posePreviewController.ShowPattern(pattern);

        Debug.Log(
            $"Pattern preview spawned: " +
            $"Patterns[{patternIndex}], " +
            $"patternTime={pattern.time:0.000}, " +
            $"gameTime={GameManager.instance.gameTime:0.000}",
            this
        );
    }

    private void CallJudgeFunction(
        PatternFrame pattern,
        int patternIndex
    )
    {
        if (poseNoteReader == null)
        {
            Debug.LogError(
                "PatternManager: PoseNoteReader가 연결되지 않았습니다.",
                this
            );

            return;
        }

        // 정확히 Patterns[patternIndex].time에 실행되는 판정 함수
        PoseNoteReader.JudgeResult result =
            poseNoteReader.EvaluatePattern(pattern);

        Debug.Log(
            $"Pattern judgment executed: " +
            $"Patterns[{patternIndex}], " +
            $"patternTime={pattern.time:0.000}, " +
            $"gameTime={GameManager.instance.gameTime:0.000}, " +
            $"rank={result.rank}",
            this
        );
    }

    private void ResetPatternProgress()
    {
        nextLoadIndex = 0;
        nextJudgeIndex = 0;

        Debug.Log(
            "PatternManager: Pattern progress reset.",
            this
        );
    }
}