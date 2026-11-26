// 화면 크기와 전체화면 모드를 주기적으로 확인하여 1080p 출력을 유지한다.
// Time.unscaledTime을 사용하므로 게임이 일시정지되어도 화면 설정을 확인한다.

using UnityEngine;

// Borderless fullscreen uses the desktop resolution. Use exclusive fullscreen
// on Windows so Alt+Enter does not increase the camera render dimensions.
public sealed class FixedResolutionController : MonoBehaviour
{
    // 매 프레임 해상도 변경을 요청하지 않도록 다음 확인 시점을 제한한다.
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