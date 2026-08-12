using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("# Game Control")]
    public bool gamePlay;
    public float gameTime;
    public float maxGameTime;

    //게임의 점수랑 판정 개수들 관리
    [Header("# Game Info")]
    public int score;
    public float accuracy;
    public int judgePerfect;
    public int judgeGood;
    public int judgeBad;
    public int judgeMiss;

    [Header("# Pause Object")]
    public GameObject pausePanel;

    [Header("# UI")]
    [SerializeField] private ResultUI resultUI;

    [Header("# Scene Transition")]
    [SerializeField] private string songSelectionSceneName = "Song Selection";

    private bool gameFinished;
    private bool resultDisplayed;

    private void Awake()
    {
        instance = this;
        instance.gamePlay = true;
        instance.score = 0;
        instance.accuracy = 0.0f;
        instance.judgePerfect = 0;
        instance.judgeGood = 0;
        instance.judgeBad = 0;
        instance.judgeMiss = 0;
        instance.gameTime = 0.0f;
        instance.maxGameTime = 150.0f;
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        LoadSelectedSongPattern();
    }

    public void GameStop()
    {
        gamePlay = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        Time.timeScale = 0;
    }

    public void GameResume()
    {
        StartCoroutine(GameResumeRoutine());
    }

    private IEnumerator GameResumeRoutine()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        yield return new WaitForSecondsRealtime(1f);
        yield return new WaitForSecondsRealtime(1f);
        yield return new WaitForSecondsRealtime(1f);
        gamePlay = true;
        Time.timeScale = 1;
    }

    public void GameRetry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        if (resultDisplayed)
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                ReturnToSongSelection();
            }

            return;
        }

        if (!gamePlay)
        {
            return;
        }

        gameTime += Time.deltaTime;

        if (gameTime > maxGameTime)
        {
            gameTime = maxGameTime;
            GameVictory();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            GameStop();
        }
    }

    public void GameVictory()
    {
        if (gameFinished)
        {
            return;
        }

        gameFinished = true;
        StartCoroutine(GameVictoryRoutine());
    }

    private IEnumerator GameVictoryRoutine()
    {
        gamePlay = false;
        SaveGameResult();

        yield return new WaitForSeconds(1.0f);

        if (resultUI == null)
        {
            resultUI = FindAnyObjectByType<ResultUI>(
                FindObjectsInactive.Include
            );
        }

        if (resultUI != null)
        {
            resultUI.ShowResult();
            resultDisplayed = true;
        }
        else
        {
            Debug.LogError(
                "GameManager: ResultUI를 찾을 수 없습니다.",
                this
            );
        }

        Time.timeScale = 0.0f;
    }

    private void ReturnToSongSelection()
    {
        resultDisplayed = false;
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(songSelectionSceneName);
    }

    private void SaveGameResult()
    {
        SongSessionController session = SongSessionController.Instance;

        if (session == null || !session.HasSelectedSong)
        {
            Debug.LogWarning("GameManager: Cannot save a result without a selected song.", this);
            return;
        }

        GameResult result = new GameResult
        {
            songId = session.SelectedSong.no,
            score = score,
            accuracy = accuracy,
            perfect = judgePerfect,
            good = judgeGood,
            bad = judgeBad,
            miss = judgeMiss
        };

        if (!GameRecordRepository.SaveResult(result))
        {
            Debug.LogError("GameManager: Failed to save the game result.", this);
        }
    }

    // 게임씬이 로드 되면 실행되어서 패턴과 곡 정보를 불러옴  
    private void LoadSelectedSongPattern()
    {
        SongSessionController session = SongSessionController.Instance;

        if (session == null || !session.HasSelectedSong)
        {
            Debug.LogWarning("No selected song session found.", this);
            return;
        }
        
        SongData song = session.SelectedSong;
        session.LoadSelectedPattern();
        if (song.time > 0.0f)
        {
            maxGameTime = song.time;
        }

        Debug.Log($"Selected song: {song.title} / {song.author}", this);
        Debug.Log($"Music path: {song.musicPath}", this);
        Debug.Log($"Pattern path: {song.patternPath}", this);
        Debug.Log($"Loaded pattern frames: {session.SelectedPatternFrames.Length}", this);
        Debug.Log($"Max game time: {maxGameTime:0.###}", this);

        if (session.SelectedPatternFrames.Length == 0)
        {
            return;
        }

        PatternFrame firstFrame = session.SelectedPatternFrames[0];
        PatternFrame lastFrame = session.SelectedPatternFrames[session.SelectedPatternFrames.Length - 1];
        Debug.Log($"Pattern time range: {firstFrame.time:0.###} - {lastFrame.time:0.###}", this);

        if (session.SelectedPatternFrames.Length <= 3)
        {
            for (int j = 0; j < session.SelectedPatternFrames.Length; j++)
            {
                PatternFrame frame = session.SelectedPatternFrames[j];
                Debug.Log($"PatternFrame[{j}] time: {frame.time}", this);
            }
        }
    }

    //새로운 판정이 입력되었을 때 카운터 반영
    //참고로 Perfect = 10000, Good = 7000, Bad = 3000, Miss = 0점 처리
    public void ApplyJudgeResult(JudgeRank rank)
    {
        switch (rank)
        {
            case JudgeRank.Perfect:
                score += 10000;
                judgePerfect++;
                break;

            case JudgeRank.Good:
                score += 7000;
                judgeGood++;
                break;

            case JudgeRank.Bad:
                score += 3000;
                judgeBad++;
                break;

            case JudgeRank.Miss:
                judgeMiss++;
                break;

            case JudgeRank.None:
            default:
                // 판정이 이루어지지 않은 경우에는
                // 점수와 정확도 계산에 포함하지 않음
                // 아마 None이 여기로 갈 건데, 카메라 끈 상황에 대한 예외처리임ㅇㅇ
                Debug.LogWarning(
                    $"GameManager: 유효하지 않은 판정입니다. rank={rank}",
                    this
                );
                return;
        }

        UpdateAccuracy();
    }

    //정확도 업데이트 함수
    //위와 비슷하게 각 판정은 100%, 70%, 30%, 0%의 정확도로 계산했음
    private void UpdateAccuracy()
    {
        int totalJudgeCount =
            judgePerfect +
            judgeGood +
            judgeBad +
            judgeMiss;

        if (totalJudgeCount <= 0)
        {
            accuracy = 0.0f;
            return;
        }

        float totalAccuracyScore =
            judgePerfect * 100.0f +
            judgeGood * 70.0f +
            judgeBad * 30.0f;

        accuracy =
            totalAccuracyScore / totalJudgeCount;
    }
}
