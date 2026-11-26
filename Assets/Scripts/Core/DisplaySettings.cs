// 게임 화면을 1920×1080으로 고정하는 설정이다. 빌드 실행 시 창 모드를 적용하고 유지용 컴포넌트를 생성한다.
// 해상도 목록 인터페이스는 옵션 UI와 연결되지만 현재는 사용자 변경값을 저장하지 않는다.

using UnityEngine;

public static class DisplaySettings
{
    public const int Width = 1920;
    public const int Height = 1080;
    public static int Count => 1;
    public static Vector2Int GetResolution(int index) => new Vector2Int(Width, Height);
    public static int LoadIndex() => 0;
    // 현재는 1080p 고정이므로 해상도 저장 요청은 처리하지 않는다.
    public static void Save(int index) { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyStartupResolution()
    {
        if (Application.isEditor) return;
        Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
        var controller = new GameObject("Fixed 1080p Display");
        Object.DontDestroyOnLoad(controller);
        controller.AddComponent<FixedResolutionController>();
    }
}