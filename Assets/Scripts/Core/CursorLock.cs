using UnityEngine;

// Hides and locks the mouse cursor while a match is being played, and gives it back in every menu (main menu,
// pause, cards, end screen, debug menu) and when the window loses focus. User feedback 2026-11-17: in full screen
// the cursor stayed in the middle of the game.
public class CursorLock : MonoBehaviour
{
    static CursorLock instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("CursorLock") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CursorLock>();
    }

    // true = the cursor should be hidden right now
    public static bool WantsHidden
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return false;
            return Application.isFocused;
        }
    }

    void Update()
    {
        bool hide = WantsHidden;
        var mode = hide ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != mode) Cursor.lockState = mode;
        if (Cursor.visible == hide) Cursor.visible = !hide;
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
