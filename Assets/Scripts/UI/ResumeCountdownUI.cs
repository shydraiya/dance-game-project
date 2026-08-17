using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ResumeCountdownUI : MonoBehaviour
{
    [Header("# UI")]
    [SerializeField] private Image countImage;

    [Header("# Sprites")]
    [SerializeField] private Sprite resumeCount3;
    [SerializeField] private Sprite resumeCount2;
    [SerializeField] private Sprite resumeCount1;

    private Coroutine countdownCoroutine;

    private void Awake()
    {
        if (countImage != null)
        {
            countImage.gameObject.SetActive(false);
        }
    }

    public void PlayCountdown(Action onFinished)
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }

        countdownCoroutine = StartCoroutine(
            CountdownRoutine(onFinished)
        );
    }

    private IEnumerator CountdownRoutine(Action onFinished)
    {
        ShowCount(resumeCount3);
        yield return new WaitForSecondsRealtime(1.0f);

        ShowCount(resumeCount2);
        yield return new WaitForSecondsRealtime(1.0f);

        ShowCount(resumeCount1);
        yield return new WaitForSecondsRealtime(1.0f);

        HideCount();

        countdownCoroutine = null;

        onFinished?.Invoke();
    }

    private void ShowCount(Sprite sprite)
    {
        if (countImage == null)
        {
            return;
        }

        countImage.sprite = sprite;
        countImage.gameObject.SetActive(true);
    }

    private void HideCount()
    {
        if (countImage != null)
        {
            countImage.gameObject.SetActive(false);
        }
    }
}