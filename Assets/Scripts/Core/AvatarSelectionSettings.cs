using UnityEngine;

public static class AvatarSelectionSettings
{
    private const string Key = "SelectedAvatarId";
    public static string Load() =>
        AvatarCatalog.GetId(AvatarCatalog.IndexOf(PlayerPrefs.GetString(Key, AvatarCatalog.DefaultId)));
    public static void Save(string id)
    {
        PlayerPrefs.SetString(Key, AvatarCatalog.GetId(AvatarCatalog.IndexOf(id)));
        PlayerPrefs.Save();
    }
}
