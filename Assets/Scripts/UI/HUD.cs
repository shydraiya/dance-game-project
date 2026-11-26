// GameManager의 현재 점수와 진행 시간을 화면 텍스트·슬라이더에 반영한다.

using System;
using UnityEngine;
using UnityEngine.UI;

//말 그대로 HUD(UI) 정보 관련 코드임
public class HUD : MonoBehaviour
{
    public enum InfoType { Score, Time }
    // 같은 컴포넌트를 점수 텍스트 또는 시간 진행률 표시로 사용할지 지정한다.
    public InfoType type;

    Text myText;
    Slider mySlider;

    void Awake()
    {
        myText   = GetComponent<Text>();
        mySlider = GetComponent<Slider>();
    }

    void LateUpdate()
    {
        switch(type)
        {
            //스코어 출력 관련 코드
            case InfoType.Score :
                int curScore = GameManager.instance.score;
                myText.text = string.Format("Score:{0}", curScore);
                break;

            case InfoType.Time :
                float curTime = GameManager.instance.gameTime;
                float maxTime = GameManager.instance.maxGameTime;
                mySlider.value = curTime / maxTime;
                break;
        }
    }
}
