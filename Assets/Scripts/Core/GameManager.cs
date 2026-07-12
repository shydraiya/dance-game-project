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

    [Header("# Game Info")]
    public int score;

    [Header("# Pause Object")]
    public GameObject pausePanel;

    private int patternOrder;

    private void Awake()
    {
        instance = this;
        instance.gamePlay = true;
        instance.score = 0;
        instance.gameTime = 0.0f;
        instance.maxGameTime = 150.0f;
        Application.targetFrameRate = 60;
        instance.patternOrder = 0;
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
        StartCoroutine(GameVictoryRoutine());
    }

    private IEnumerator GameVictoryRoutine()
    {
        gamePlay = false;
        yield return new WaitForSeconds(1.0f);
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
}
