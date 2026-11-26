// 아바타 ID, 표시 이름, Resources 프리팹 경로를 한곳에서 관리하는 목록이다.
// 옵션 선택과 플레이 씬의 모델 교체가 같은 ID를 사용하도록 연결한다.

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
    // 저장된 ID를 Resources의 아바타 프리팹 이름으로 변환한다.
    public static GameObject Load(string id)
    {
        string name = id == "boy" ? "BoyAvatar" : id == "girl" ? "GirlAvatar" : "SantaClaus";
        return Resources.Load<GameObject>("PatternTest/Avatars/" + name);
    }
}
