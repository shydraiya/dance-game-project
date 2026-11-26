// 음악 볼륨의 저장 키와 읽기·쓰기 처리를 공유한다. 옵션 화면과 게임 음악이 같은 저장값을 사용한다.

using UnityEngine;

public static class MusicVolumeSettings
{
    public const string MusicVolumeKey = "MusicVolume";

    public static float LoadVolume()
    {
        // 저장된 값이 없으면 기본 볼륨 1.0
        return PlayerPrefs.GetFloat(MusicVolumeKey, 1.0f);
    }

    // 볼륨을 저장하여 옵션과 게임 씬에서 동일한 설정을 사용하게 한다.
    public static void SaveVolume(float volume)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, volume);
        PlayerPrefs.Save();
    }
}