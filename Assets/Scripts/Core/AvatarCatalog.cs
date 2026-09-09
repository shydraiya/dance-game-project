using UnityEngine;

public static class AvatarCatalog
{
    public const string DefaultId = "santa";
    private static readonly string[] Ids = { DefaultId, "boy", "girl" };
    private static readonly string[] Names = { "Santa Claus", "Boy", "Girl" };
    public static int Count => Ids.Length;
    public static string GetId(int index) => Ids[index >= 0 && index < Count ? index : 0];
    public static string GetName(int index) => Names[index];
    public static int IndexOf(string id)
    {
        int index = System.Array.IndexOf(Ids, id);
        return index < 0 ? 0 : index;
    }
    public static GameObject Load(string id)
    {
        string name = id == "boy" ? "BoyAvatar" : id == "girl" ? "GirlAvatar" : "SantaClaus";
        return Resources.Load<GameObject>("PatternTest/Avatars/" + name);
    }
}
