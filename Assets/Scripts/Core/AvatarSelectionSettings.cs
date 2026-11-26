// 선택한 아바타 ID를 PlayerPrefs에 저장한다. 저장값은 AvatarCatalog에서 유효성을 확인한다.

using UnityEngine;

public static class AvatarSelectionSettings
{
    private const string Key = "SelectedAvatarId";
    public static string Load() =>
        AvatarCatalog.GetId(AvatarCatalog.IndexOf(PlayerPrefs.GetString(Key, AvatarCatalog.DefaultId)));
    // 유효한 아바타 ID로 보정한 후 다음 실행에도 유지되도록 저장한다.
    public static void Save(string id)
    {
        PlayerPrefs.SetString(Key, AvatarCatalog.GetId(AvatarCatalog.IndexOf(id)));
        PlayerPrefs.Save();
    }
}
