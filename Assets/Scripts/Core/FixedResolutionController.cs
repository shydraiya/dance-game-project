using UnityEngine;

// Borderless fullscreen uses the desktop resolution. Use exclusive fullscreen
// on Windows so Alt+Enter does not increase the camera render dimensions.
public sealed class FixedResolutionController : MonoBehaviour
{
    private float nextCheckTime;
    private bool loggedUnsupportedResolution;

    private void Update()
    {
        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + 0.5f;

        FullScreenMode mode = Screen.fullScreen
            ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        if (Screen.width == DisplaySettings.Width &&
            Screen.height == DisplaySettings.Height && Screen.fullScreenMode == mode)
        {
            loggedUnsupportedResolution = false;
            return;
        }

        if (!loggedUnsupportedResolution)
        {
            Debug.Log("Display: applying fixed 1920x1080, mode=" + mode +
                ", previous=" + Screen.width + "x" + Screen.height);
            loggedUnsupportedResolution = true;
        }
        Screen.SetResolution(DisplaySettings.Width, DisplaySettings.Height, mode);
    }
}