using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class JudgeUI : MonoBehaviour
{
    public enum JudgeRank
    {
        None,
        Miss,
        Bad,
        Good,
        Perfect
    }
    [Header("UI")]
    [SerializeField] private Image judgeImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Sprites")]
    [SerializeField] private Sprite perfectSprite;
    [SerializeField] private Sprite goodSprite;
    [SerializeField] private Sprite badSprite;
    [SerializeField] private Sprite missSprite;

    [Header("Animation")]
    [SerializeField] private float startScale = 1.2f;
    [SerializeField] private float endScale = 1.0f;
    [SerializeField] private float scaleDuration = 0.5f;
    [SerializeField] private float holdDuration = 1.0f;
    [SerializeField] private float fadeDuration = 0.25f;

    private RectTransform rectTransform;
    private Coroutine judgeRoutine;

    private void Awake()
    {
        rectTransform = judgeImage.GetComponent<RectTransform>();

        canvasGroup.alpha = 0f;
        judgeImage.enabled = false;
    }

    public void ShowJudge(JudgeRank status)
    {
        if (judgeRoutine != null)
        {
            StopCoroutine(judgeRoutine);
        }

        judgeRoutine = StartCoroutine(ShowJudgeRoutine(status));
    }

    private IEnumerator ShowJudgeRoutine(JudgeRank status)
    {
        judgeImage.sprite = GetJudgeSprite(status);
        judgeImage.SetNativeSize();

        judgeImage.enabled = true;
        canvasGroup.alpha = 1f;

        rectTransform.localScale = Vector3.one * startScale;

        float timer = 0f;

        while (timer < scaleDuration)
        {
            timer += Time.deltaTime;

            float t = timer / scaleDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            float scale = Mathf.Lerp(startScale, endScale, t);
            rectTransform.localScale = Vector3.one * scale;

            yield return null;
        }

        rectTransform.localScale = Vector3.one * endScale;

        yield return new WaitForSeconds(holdDuration);

        timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;

            float t = timer / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        canvasGroup.alpha = 0f;
        judgeImage.enabled = false;

        judgeRoutine = null;
    }

    private Sprite GetJudgeSprite(JudgeRank status)
    {
        switch (status)
        {
            case JudgeRank.Perfect:
                return perfectSprite;

            case JudgeRank.Good:
                return goodSprite;

            case JudgeRank.Bad:
                return badSprite;

            case JudgeRank.Miss:
                return missSprite;

            default:
                return null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            ShowJudge(JudgeRank.Perfect);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            ShowJudge(JudgeRank.Good);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            ShowJudge(JudgeRank.Bad);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            ShowJudge(JudgeRank.Miss);
        }
    }
}
