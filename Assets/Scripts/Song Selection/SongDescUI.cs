using UnityEngine;
using UnityEngine.UI;

//노래의 상세 정보(곡 선곡 화면에서 좌측에 보이는 정보)를 가져오는 코드
//최고 기록 관련 데이터는 현재 미완성임
//노래 스프라이트 가져오기도 미완성임
public class SongDescUI : MonoBehaviour
{
    [SerializeField] private Image jacketImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Text authorText;
    [SerializeField] private Text difficultyText;
    [SerializeField] private Text bestScoreText;

    public void Bind(SongData song)
    {
        if (song == null)
            return;

        if (jacketImage == null)
        {
            Transform imageTransform = transform.Find("SongDesc_Image");
            if (imageTransform != null)
            {
                jacketImage = imageTransform.GetComponent<Image>();
            }
        }

        Sprite coverSprite = string.IsNullOrWhiteSpace(song.coverPath)
            ? null
            : Resources.Load<Sprite>(song.coverPath);

        if (jacketImage != null)
        {
            jacketImage.sprite = coverSprite;
            jacketImage.enabled = coverSprite != null;
            jacketImage.preserveAspect = true;
        }

        if (coverSprite == null && !string.IsNullOrWhiteSpace(song.coverPath))
        {
            Debug.LogWarning($"SongDescUI: Cover image not found at Resources/{song.coverPath}", this);
        }

        titleText.text      = song.title;
        authorText.text     = song.author;
        difficultyText.text = $"★ {song.difficulty}";
        SongRecord record = GameRecordRepository.GetRecord(song.no);
        int highScore = record != null ? record.highScore : 0;
        bestScoreText.text = $"Score : {highScore}";
        //bestScoreText.text = $"최고 기록 : {song.bestScore}";
    }
}
