using UnityEngine;

public static class MusicVolumeSettings
{
    public const string MusicVolumeKey = "MusicVolume";

    public static float LoadVolume()
    {
        // 저장된 값이 없으면 기본 볼륨 1.0
        return PlayerPrefs.GetFloat(MusicVolumeKey, 1.0f);
    }

    public static void SaveVolume(float volume)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, volume);
        PlayerPrefs.Save();
    }
}