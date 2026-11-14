// Scripted test of the main menu's map selector (Tools > Island > Play Test Map Selector):
// enters play mode in the menu, opens the selector (screenshot with UI), picks a map, waits for its
// scene to load and round 1 to start, and checks that no errors were logged. Results in EditorPrefs
// ("MapSelectorPlayTest.Log"), screenshots in Renders/.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapSelectorPlayTest
{
    const string Key = "MapSelectorPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static int step, mapIndex, errors;
    static float realTime;

    [MenuItem("Tools/Island/Play Test Map Selector")]
    public static void RunOriginal() => Run(1);

    public static void Run(int map)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        step = 0; mapIndex = map; errors = 0; realTime = 0f;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        errors++;
        if (errors <= 5) Write("LOGGED " + type + ": " + msg.Split('\n')[0]);
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");

    static void Finish(string status)
    {
        EditorPrefs.SetString(Key + "Status", status);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if (realTime == 0f) realTime = Time.realtimeSinceStartup;
        float t = Time.realtimeSinceStartup - realTime;
        try
        {
            switch (step)
            {
                case 0:   // menu: open the selector straight away (the title / menu screens are UI only)
                    if (t < 2f) return;
                    var menu = Object.FindAnyObjectByType<MainMenu>();
                    if (menu == null) { Write("FAIL no MainMenu"); Finish("failed"); return; }
                    Write($"maps in the selector: {menu.MapCount}");
                    menu.OpenMapSelector();
                    Next();
                    break;
                case 1:
                    if (t < 1f) return;
                    var cards = Object.FindObjectsByType<UnityEngine.UI.Button>().Where(b => b.name.StartsWith("Map_")).Select(b => b.name).OrderBy(n => n).ToArray();
                    Write((cards.Length >= 2 ? "OK " : "FAIL") + " selector cards: " + string.Join(", ", cards));
                    ShotWithUI("Renders/MapSelector.png");
                    Object.FindAnyObjectByType<MainMenu>().PlayMap(mapIndex);
                    Next();
                    break;
                case 2:   // wait for the map scene and the player
                    var gm = GameManager.Instance;
                    if (gm == null || gm.Player == null) { if (t > 30f) { Write("FAIL map scene did not start"); Finish("failed"); } return; }
                    Write($"OK loaded scene '{SceneManager.GetActiveScene().name}' (map '{GameSettings.Map}'), player {gm.Animal.id} at {gm.Player.transform.position}");
                    Next();
                    break;
                case 3:
                    if (t < 14f) return;
                    var enemies = Object.FindObjectsByType<EnemyAI>().Length;
                    Write((RoundManager.Instance.Round >= 1 && enemies > 0 ? "OK " : "FAIL") + $" round {RoundManager.Instance.Round}, {enemies} enemies alive after 14 s");
                    var cam = Camera.main;
                    if (cam != null) Shot(cam, "Renders/MapSelector_loaded.png");
                    Write((errors == 0 ? "OK " : "FAIL") + $" errors logged during the test: {errors}");
                    Finish("done");
                    break;
            }
        }
        catch (System.Exception e) { Write("EXCEPTION: " + e); Finish("failed"); }
    }

    static void Next() { step++; realTime = Time.realtimeSinceStartup; }

    // Renders the menu camera with the overlay canvas switched to camera space so the UI is in the picture.
    static void ShotWithUI(string path)
    {
        var cam = Camera.main;
        var canvas = Object.FindObjectsByType<Canvas>().FirstOrDefault(c => c.isRootCanvas);
        if (cam == null || canvas == null) return;
        var mode = canvas.renderMode;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.05f;
        Canvas.ForceUpdateCanvases();
        Shot(cam, path);
        canvas.renderMode = mode;
    }

    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
        rt.Release();
    }
}
