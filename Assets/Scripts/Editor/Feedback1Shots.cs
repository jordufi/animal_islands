using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pictures to check user feedback 1, phase 1 by eye (Docs/FEEDBACK1_PLAN.md): the pause screen (map left, buttons,
// cards right) at 16:9 and 4:3, world labels that no longer turn with the camera (TAQUILLA, wall weapons, doors), and
// the volley stands of Isla del Faro (alturas). Renders/Feedback1_*.png. Log in EditorPrefs Feedback1Shots.Log / .Status.
public static class Feedback1Shots
{
    const string Key = "Feedback1Shots.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");
    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static string scenePath;

    [MenuItem("Tools/Island/Feedback 1 Shots")]
    public static void Run() => RunOn(TestMapSetup.IslaZombieScenePath);
    public static void RunAlturas() => RunOn(IslaZombieSetup.IslaAlturas.ScenePath);

    public static void RunOn(string path)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        scenePath = path;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        script.Clear();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");

    static void Finish(string status)
    {
        Time.timeScale = 1f;
        EditorPrefs.SetString(Key + "Status", status);
        EditorPrefs.SetBool(Key + "Running", false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void Tick()
    {
        if (!EditorPrefs.GetBool(Key + "Running")) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        try
        {
            if (script.Count == 0) script.Push(Script());
            while (script.Count > 0)
            {
                var top = script.Peek();
                if (!top.MoveNext()) { script.Pop(); if (script.Count == 0) { Finish("done"); return; } continue; }
                if (top.Current is IEnumerator nested) { script.Push(nested); continue; }
                break;
            }
        }
        catch (System.Exception e) { Write("EXCEPTION: " + e); Finish("failed"); }
    }

    static IEnumerator Real(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        yield return Real(1.5f);
        string tag = scenePath.Contains("Alturas") ? "alturas" : "isla";

        // world labels: from in front, from behind and at an angle (fixed text shows in perspective, never mirrored)
        var meshes = Object.FindObjectsByType<TextMesh>().Where(t => t.gameObject.activeInHierarchy && t.name != "Back").ToList();
        Write($"{meshes.Count} world labels");
        var picks = new List<(string name, TextMesh t)>();
        void Pick(string name, System.Func<TextMesh, bool> f) { var t = meshes.FirstOrDefault(f); if (t != null) picks.Add((name, t)); else Write("no label for " + name); }
        Pick("taquilla", t => t.text.Contains("TAQUILLA"));
        Pick("pared", t => t.GetComponentInParent<WallWeapon>() != null);
        Pick("puerta", t => t.GetComponentInParent<ZoneDoor>() != null);
        Pick("bebida", t => t.GetComponentInParent<DrinkMachine>() != null);
        foreach (var (name, t) in picks)
        {
            var p = t.transform.position;
            var f = t.transform.forward; f.y = 0f; f.Normalize();
            var side = Vector3.Cross(Vector3.up, f);
            Write($"label {name} '{t.text.Replace("\n", " ")}' at {p} facing {f}");
            ShotFrom(p - f * 4f + Vector3.up * 0.3f, p, $"Renders/Feedback1_{tag}_{name}_frente.png");
            ShotFrom(p + f * 4f + Vector3.up * 0.3f, p, $"Renders/Feedback1_{tag}_{name}_detras.png");
            ShotFrom(p - f * 3f + side * 3f + Vector3.up * 0.3f, p, $"Renders/Feedback1_{tag}_{name}_lado.png");
            yield return null;
        }

        // volley stands
        var grada = Object.FindObjectsByType<Transform>().FirstOrDefault(t => t.name.StartsWith("IZ_Grada"));
        if (grada != null)
        {
            var r = grada.GetComponentsInChildren<Renderer>();
            var b = r[0].bounds; foreach (var x in r) b.Encapsulate(x.bounds);
            ShotFrom(b.center + new Vector3(4f, 2.5f, 4f), b.center, $"Renders/Feedback1_{tag}_grada.png");
            ShotFrom(b.center + new Vector3(-1.5f, 0.6f, 3.5f), b.center, $"Renders/Feedback1_{tag}_grada_cerca.png");
        }
        else Write("no IZ_Grada in the scene");

        if (tag == "isla")
        {
            // pause screen with two cards taken
            var cm = CardManager.Instance;
            for (int i = 0; i < 3 && cm != null; i++)
            {
                cm.Offer();
                if (cm.CurrentOffer != null) cm.Choose(cm.CurrentOffer[0]);
                yield return Real(0.2f);
            }
            Write("cards taken: " + (cm != null ? cm.Taken.Count : 0) + ", state " + gm.State);
            gm.SetPaused(true);
            yield return Real(0.5f);
            Shot(1600, 900, "Renders/Feedback1_pausa_16x9.png");
            Shot(1024, 768, "Renders/Feedback1_pausa_4x3.png");
            gm.SetPaused(false);
            yield return Real(0.3f);
        }
        Write("done");
    }

    static void ShotFrom(Vector3 pos, Vector3 lookAt, string path)
    {
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        go.transform.position = pos;
        go.transform.LookAt(lookAt);
        Render(cam, 1280, 720, path);
        Object.Destroy(go);
    }

    static void Shot(int w, int h, string path)
    {
        var cam = GameManager.Instance.CameraRig.GetComponent<Camera>();
        var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        cam.Render();
        Save(rt, w, h, path);
        cam.targetTexture = null;
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        rt.Release();
    }

    static void Render(Camera cam, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        Save(rt, w, h, path);
        cam.targetTexture = null;
        rt.Release();
    }

    static void Save(RenderTexture rt, int w, int h, string path)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
    }
}
