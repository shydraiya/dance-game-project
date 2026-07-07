using UnityEngine;
using UnityEngine.UI;

//노래 리스트의 한 줄(곡 선곡 화면에서 우측에 보이는 리스트) UI를 가져오는 코드
//참고로 클리어 성과 (S, A, B, C, X)를 가져오는 기능은 미완성임 
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class SongRowUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Legacy UI Text")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text difficultyText;

    [SerializeField] private GameObject selectedFrame;

    private Vector3 baseScale;
    private Vector2 baseSize;

    private void Awake()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // 프리팹에 원래 설정해 둔 UI 크기와 Scale 보관
        baseScale = rectTransform.localScale;
        baseSize = rectTransform.sizeDelta;
    }

    public void Bind(SongData song)
    {
        if (song == null)
        {
            titleText.text = "";
            difficultyText.text = "";
            return;
        }

        //현재 클리어 성과도 추가해야함
        //X는 현재 플레이 기록 없음을 의미
        titleText.text = song.title;
        difficultyText.text = $"★{song.difficulty} X";
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
    }

    public void SetVisual(Vector2 position, Vector2 size, float scale, float alpha)
    {
        rectTransform.anchoredPosition = position;

        // manager의 rowSize로 프리팹 원래 크기를 덮어쓰지 않음
        rectTransform.sizeDelta = baseSize;

        // 프리팹의 기존 0.35 Scale을 유지한 상태에서 배율만 적용
        rectTransform.localScale = new Vector3(
            baseScale.x * scale,
            baseScale.y * scale,
            baseScale.z
        );

        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = alpha > 0.01f;
        canvasGroup.interactable = alpha > 0.01f;
    }
}