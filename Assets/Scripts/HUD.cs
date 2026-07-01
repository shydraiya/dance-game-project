using System;
using UnityEngine;
using UnityEngine.UI;

//말 그대로 HUD(UI) 정보 관련 코드임
public class HUD : MonoBehaviour
{
    public enum InfoType { Score, Time }
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
