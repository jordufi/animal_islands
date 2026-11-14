using UnityEngine;

public enum Difficulty { Facil, Normal, Dificil, Pesadilla }

// Difficulty preset: multipliers applied to the run at start.
public struct DifficultyPreset
{
    public string label;
    public string description;
    public float enemyHealth, enemyDamage, enemySpeed, enemyCount, pointsGain, regenDelay;
}

// Menu settings, persisted in PlayerPrefs. Read by the game scene when a run starts.
public static class GameSettings
{
    const string KeyDifficulty = "settings.difficulty";
    const string KeyAnimal = "settings.animal";
    const string KeyMap = "settings.map";
    const string KeySensitivity = "settings.sensitivity";
    const string KeyInvertY = "settings.invertY";
    const string KeyFov = "settings.fov";
    const string KeySprintToggle = "settings.sprintToggle";
    const string KeyShowFps = "settings.showFps";
    const string KeyAnselmo = "settings.anselmoCagon";
    const string KeyFullscreen = "settings.fullscreen";

    public static Difficulty Difficulty
    {
        get => (Difficulty)PlayerPrefs.GetInt(KeyDifficulty, (int)Difficulty.Normal);
        set => PlayerPrefs.SetInt(KeyDifficulty, (int)value);
    }

    public static string Animal
    {
        get => PlayerPrefs.GetString(KeyAnimal, "RedPanda");
        set => PlayerPrefs.SetString(KeyAnimal, value);
    }

    // id of the last map chosen in the menu (MapData.id)
    public static string Map
    {
        get => PlayerPrefs.GetString(KeyMap, "");
        set => PlayerPrefs.SetString(KeyMap, value);
    }

    // camera settings are cached because the camera reads them every frame
    static float? sensitivity, fieldOfView;
    static bool? invertY;

    public static float Sensitivity
    {
        get => sensitivity ??= PlayerPrefs.GetFloat(KeySensitivity, 1f);
        set { sensitivity = Mathf.Clamp(value, 0.2f, 3f); PlayerPrefs.SetFloat(KeySensitivity, sensitivity.Value); }
    }

    public static bool InvertY
    {
        get => invertY ??= PlayerPrefs.GetInt(KeyInvertY, 0) == 1;
        set { invertY = value; PlayerPrefs.SetInt(KeyInvertY, value ? 1 : 0); }
    }

    public static float FieldOfView
    {
        get => fieldOfView ??= PlayerPrefs.GetFloat(KeyFov, 60f);
        set { fieldOfView = Mathf.Clamp(value, 50f, 90f); PlayerPrefs.SetFloat(KeyFov, fieldOfView.Value); }
    }

    // Gamepad sprint: true = press L3 once and keep running while the stick is held (default),
    // false = hold L3. The keyboard (Shift) always holds.
    static bool? sprintToggle;
    public static bool SprintToggle
    {
        get => sprintToggle ??= PlayerPrefs.GetInt(KeySprintToggle, 1) == 1;
        set { sprintToggle = value; PlayerPrefs.SetInt(KeySprintToggle, value ? 1 : 0); }
    }

    // FPS counter in the top-right corner (FpsCounter)
    static bool? showFps;
    public static bool ShowFps
    {
        get => showFps ??= PlayerPrefs.GetInt(KeyShowFps, 0) == 1;
        set { showFps = value; PlayerPrefs.SetInt(KeyShowFps, value ? 1 : 0); }
    }

    // Full screen (borderless, native resolution) or a window. Applied at startup by ScreenMode, so the window
    // size Unity remembers in the registry (e.g. from the windowed online test client) never wins.
    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
        set { PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); ScreenMode.Apply(); }
    }

    // Game mode chosen in the menu. "Anselmo cagón": some old men (TunnelData.chance, fixed at spawn) take
    // shortcuts through the spawn entrances (WC, cracks, bus...) from one zone to another. Off = classic.
    public static bool AnselmoCagon
    {
        get => PlayerPrefs.GetInt(KeyAnselmo, 0) == 1;
        set => PlayerPrefs.SetInt(KeyAnselmo, value ? 1 : 0);
    }

    public static void Save() => PlayerPrefs.Save();

    public static DifficultyPreset Preset(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Facil:
                return new DifficultyPreset { label = "Fácil", description = "Abuelos blanditos. Para aprender.", enemyHealth = 0.7f, enemyDamage = 0.75f, enemySpeed = 0.9f, enemyCount = 0.85f, pointsGain = 1.2f, regenDelay = 0.75f };
            case Difficulty.Dificil:
                return new DifficultyPreset { label = "Difícil", description = "Los abuelos han desayunado fuerte.", enemyHealth = 1.3f, enemyDamage = 1.25f, enemySpeed = 1.08f, enemyCount = 1.15f, pointsGain = 1f, regenDelay = 1.4f };
            case Difficulty.Pesadilla:
                return new DifficultyPreset { label = "Jubilación Anticipada", description = "Pesadilla. Nadie sale de esta isla.", enemyHealth = 1.7f, enemyDamage = 1.5f, enemySpeed = 1.18f, enemyCount = 1.3f, pointsGain = 0.9f, regenDelay = 2f };
            default:
                return new DifficultyPreset { label = "Normal", description = "La experiencia recomendada.", enemyHealth = 1f, enemyDamage = 1f, enemySpeed = 1f, enemyCount = 1f, pointsGain = 1f, regenDelay = 1f };
        }
    }
}
