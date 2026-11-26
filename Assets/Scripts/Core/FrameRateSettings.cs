// 60/120 FPS 선택값을 PlayerPrefs에 저장하고 시작 시 복원한다.
// VSync를 해제하여 Application.targetFrameRate로 목표 프레임률을 제어한다.

using UnityEngine;

public static class FrameRateSettings
{
    private const string PreferenceKey = "TargetFrameRate";

    public static int Load()
    {
        return PlayerPrefs.GetInt(PreferenceKey, 60) == 120 ? 120 : 60;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ApplySaved()
    {
        // VSync overrides targetFrameRate on desktop, so use the selected cap.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Load();
    }

    // 지원 값인 60/120으로 제한해 저장하고 즉시 현재 실행에 적용한다.
    public static void Save(int frameRate)
    {
        PlayerPrefs.SetInt(PreferenceKey, frameRate == 120 ? 120 : 60);
        PlayerPrefs.Save();
        ApplySaved();
    }
}