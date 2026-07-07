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

        //jacketImage.sprite = song.jacket;

        titleText.text      = song.title;
        authorText.text     = song.author;
        difficultyText.text = $"★ {song.difficulty}";
        bestScoreText.text  = $"Score 1000000";
        //bestScoreText.text = $"최고 기록 : {song.bestScore}";
    }
}