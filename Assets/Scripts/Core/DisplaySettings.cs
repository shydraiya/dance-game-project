using UnityEngine;

public static class DisplaySettings
{
    public const int Width = 1920;
    public const int Height = 1080;
    public static int Count => 1;
    public static Vector2Int GetResolution(int index) => new Vector2Int(Width, Height);
    public static int LoadIndex() => 0;
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