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

    public static void Save(int frameRate)
    {
        PlayerPrefs.SetInt(PreferenceKey, frameRate == 120 ? 120 : 60);
        PlayerPrefs.Save();
        ApplySaved();
    }
}