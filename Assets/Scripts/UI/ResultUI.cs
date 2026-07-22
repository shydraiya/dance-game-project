using UnityEngine;
using UnityEngine.UI;

public class ResultUI : MonoBehaviour
{
    [Header("# Result Text")]
    [SerializeField] private Text resultTitle;
    [SerializeField] private Text resultJudgeT;
    [SerializeField] private Text resultJudgeS;
    [SerializeField] private Text resultScore;
    [SerializeField] private Text resultScoreNew;
    [SerializeField] private Text resultScoreAcc;

    [Header("# Rank Image")]
    [SerializeField] private Image resultRank;

    [SerializeField] private Sprite resultRankS;
    [SerializeField] private Sprite resultRankA;
    [SerializeField] private Sprite resultRankB;
    [SerializeField] private Sprite resultRankC;

    [Header("# Settings")]
    [SerializeField] private bool hideOnAwake = true;

    private void Awake()
    {
        AutoBindReferences();

        if (resultScoreNew != null)
        {
            // 신기록 기능은 나중에 구현
            resultScoreNew.gameObject.SetActive(false);
        }

        if (hideOnAwake)
        {
            gameObject.SetActive(false);
        }
    }

    /// GameManager의 현재 결과를 화면에 출력함
    public void ShowResult()
    {
        if (GameManager.instance == null)
        {
            Debug.LogError(
                "ResultUI: GameManager.instance가 없습니다.",
                this
            );

            return;
        }

        GameManager gameManager = GameManager.instance;

        ShowResult(
            gameManager.score,
            gameManager.accuracy,
            gameManager.judgePerfect,
            gameManager.judgeGood,
            gameManager.judgeBad,
            gameManager.judgeMiss
        );
    }

    /// 전달받은 게임 결과를 화면에 출력함
    public void ShowResult(
        int score,
        float accuracy,
        int perfectCount,
        int goodCount,
        int badCount,
        int missCount
    )
    {
        AutoBindReferences();

        gameObject.SetActive(true);

        // 혹시 계산 오차 등으로 범위를 벗어난 경우를 방지
        accuracy = Mathf.Clamp(accuracy, 0.0f, 100.0f);

        if (resultTitle != null)
        {
            resultTitle.text = "CLEAR!";
        }

        if (resultJudgeT != null)
        {
            resultJudgeT.text =
                "PERFECT\n" +
                "GOOD\n" +
                "BAD\n" +
                "MISS";
        }

        if (resultJudgeS != null)
        {
            resultJudgeS.text =
                $"{perfectCount}\n" +
                $"{goodCount}\n" +
                $"{badCount}\n" +
                $"{missCount}";
        }

        if (resultScore != null)
        {
            resultScore.text = $"Score : {score}";
        }

        if (resultScoreAcc != null)
        {
            resultScoreAcc.text = $"({accuracy:0.0}%)";
        }

        if (resultScoreNew != null)
        {
            // 신기록 기능을 구현하기 전까지 비활성화
            resultScoreNew.gameObject.SetActive(false);
        }

        SetRankImage(accuracy);
    }

    //랭크 이미지는 인스펙터로 정해놨음
    //참고로 정확도가 95 >= S, 90 >= A, 80 >= B, 80 < C가 되도록 해놨음
    private void SetRankImage(float accuracy)
    {
        if (resultRank == null)
        {
            Debug.LogWarning(
                "ResultUI: Result_Rank Image가 연결되지 않았습니다.",
                this
            );

            return;
        }

        if (accuracy >= 95.0f)
        {
            resultRank.sprite = resultRankS;
        }
        else if (accuracy >= 90.0f)
        {
            resultRank.sprite = resultRankA;
        }
        else if (accuracy >= 80.0f)
        {
            resultRank.sprite = resultRankB;
        }
        else
        {
            resultRank.sprite = resultRankC;
        }

        resultRank.enabled = resultRank.sprite != null;
        resultRank.preserveAspect = true;
    }

    public void HideResult()
    {
        gameObject.SetActive(false);
    }

    /// Inspector 연결이 누락된 경우 자식 이름을 이용해 자동 탐색한다.
    private void AutoBindReferences()
    {
        if (resultTitle == null)
        {
            resultTitle = FindChildComponent<Text>("Result_Title");
        }

        if (resultJudgeT == null)
        {
            resultJudgeT = FindChildComponent<Text>("Result_JudgeT");
        }

        if (resultJudgeS == null)
        {
            resultJudgeS = FindChildComponent<Text>("Result_JudgeS");
        }

        if (resultScore == null)
        {
            resultScore = FindChildComponent<Text>("Result_Score");
        }

        if (resultScoreNew == null)
        {
            resultScoreNew = FindChildComponent<Text>("Result_ScoreNew");
        }

        if (resultScoreAcc == null)
        {
            resultScoreAcc = FindChildComponent<Text>("Result_ScoreAcc");
        }

        if (resultRank == null)
        {
            resultRank = FindChildComponent<Image>("Result_Rank");
        }
    }

    private T FindChildComponent<T>(string childName)
        where T : Component
    {
        Transform child = transform.Find(childName);

        if (child == null)
        {
            Debug.LogWarning(
                $"ResultUI: 자식 오브젝트 '{childName}'을 찾을 수 없습니다.",
                this
            );

            return null;
        }

        T component = child.GetComponent<T>();

        if (component == null)
        {
            Debug.LogWarning(
                $"ResultUI: '{childName}'에 " +
                $"{typeof(T).Name} 컴포넌트가 없습니다.",
                this
            );
        }

        return component;
    }
}