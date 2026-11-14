using UnityEngine;

// Window mode of the exported game (Ajustes > Pantalla completa). Unity restores the last window size / mode from
// the registry, shared by every build of this project (the online test client runs windowed at 1280x720), so the
// player forces our setting at startup. Alt+Enter still toggles it for the session. Does nothing in the editor and
// when the command line sets the screen itself (-screen-fullscreen, used by the test client).
public static class ScreenMode
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnStartup() => Apply();

    public static void Apply()
    {
        if (Application.isEditor || Application.isMobilePlatform) return;
        if (System.Environment.CommandLine.Contains("-screen-fullscreen")) return;
        int w = Display.main.systemWidth, h = Display.main.systemHeight;
        if (GameSettings.Fullscreen)
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        else
        {
            // a 16:9 window ~75 % of the monitor
            int ww = Mathf.RoundToInt(w * 0.75f);
            Screen.SetResolution(ww, Mathf.Min(Mathf.RoundToInt(ww * 9f / 16f), h - 80), FullScreenMode.Windowed);
        }
    }
}
