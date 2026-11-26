// 게임 시작 또는 재개 전에 카운트다운 이미지를 순서대로 표시한다.
// 완료 콜백으로 GameManager에 재생 시작 시점을 전달한다.

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

    // 카운트다운 종료 후 실행할 동작을 콜백으로 받아 시작·재개 양쪽에서 재사용한다.
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