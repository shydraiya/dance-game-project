using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.VectorGraphics;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    [Header("# Game Control")]
    public bool  gamePlay;    //현재 게임 진행 여부 (Pause 걸리면 false, 아니면 true)
    public float gameTime;    //현재 게임 진행 시간
    public float maxGameTime; //최대 게임 진행 시간 (곡이 2분30초면 150.0f)
    [Header("# Game Info")]
    public int score;         //점수
    [Header("# Pause Object")]
    public GameObject pausePanel;


    //Awake 함수
    //게임이 1초에 60프레임씩 진행되도록 고정함 (요거 안하면 30fps 나옴)
    void Awake()
    {
        instance = this;
        instance.gamePlay = true;
        instance.score = 0;
        instance.gameTime = 0.0f;
        instance.maxGameTime = 150.0f; // 요건 디버그임
        Application.targetFrameRate = 60;
    }

    //게임 정지 함수
    //나중에 게임 정지가 필요하면 요걸 호출하면 됨
    public void GameStop()
    {
        gamePlay = false;
        pausePanel.SetActive(true);
        Time.timeScale = 0;
    }

    //게임 이어하기 함수
    //일단 3, 2, 1 요건 나중에 넣을 예정!!!
    public void GameResume()
    {
        StartCoroutine(GameResumeRoutine());
    }
    //이어하기 함수에서 실행하는 seq
    IEnumerator GameResumeRoutine()
    {
        pausePanel.SetActive(false);
        yield return new WaitForSecondsRealtime(1f);
        yield return new WaitForSecondsRealtime(1f);
        yield return new WaitForSecondsRealtime(1f);
        gamePlay = true;
        Time.timeScale = 1;
    }

    //게임 다시하기 함수
    public void GameRetry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    //시간에 따라 타이머 업데이트
    //만약 maxGameTime에 도달하면 승리
    void Update()
    {
        if (!gamePlay)
            return;

        gameTime += Time.deltaTime;

        if (gameTime > maxGameTime)
        {
            gameTime = maxGameTime;
            GameVictory();
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            GameStop();
        }
    }

    //게임 승리 함수
    public void GameVictory()
    {
        StartCoroutine(GameVictoryRoutine());
    }
    //승리 함수에서 실행하는 seq
    IEnumerator GameVictoryRoutine()
    {
        gamePlay = false;
        yield return new WaitForSeconds(1.0f);
    }
}
