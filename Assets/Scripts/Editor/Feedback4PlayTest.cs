using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// User feedback 1, phase 4 (Docs/FEEDBACK1_PLAN.md): one door per round (refused in the same round, fine in the next),
// the Julio records fire the old men up, hurt old men lose their glasses (slower) or drop their cane (faster), the
// "brújula de mierdecillas", the false teeth (Dentadura) and the map carousel of the main menu.
// Tools > Island > Play Test Feedback 4 (Run = Isla del Faro, RunMenu = the main menu). EditorPrefs Feedback4PlayTest.Log /
// .Status, pictures Renders/Feedback4_*.png.
public static class Feedback4PlayTest
{
    const string Key = "Feedback4PlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");
    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static bool menu;
    static bool compassBefore;

    [MenuItem("Tools/Island/Play Test Feedback 4")]
    public static void Run() => Start("Assets/Scenes/IslaZombie.unity", false);
    [MenuItem("Tools/Island/Play Test Feedback 4 (menu)")]
    public static void RunMenu() => Start("Assets/Scenes/MainMenu.unity", true);

    static void Start(string scene, bool isMenu)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        menu = isMenu;
        compassBefore = GameSettings.Compass;
        if (!isMenu) GameSettings.Compass = true;   // the HUD reads it when the players spawn
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        script.Clear();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");
    static void Check(bool ok, string what) => Write((ok ? "OK   " : "FAIL ") + what);

    static void Finish(string status)
    {
        GameSettings.Compass = compassBefore;
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
        if (menu ? Object.FindAnyObjectByType<MainMenu>() == null : GameManager.Instance == null || GameManager.Instance.Player == null) return;
        try
        {
            if (script.Count == 0) script.Push(menu ? MenuScript() : Script());
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

    static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    static IEnumerator Real(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    static EnemyAI Spawn(Vector3 near, float hp)
    {
        var ai = RoundManager.Instance.SpawnEnemy(null, near);
        if (ai == null) return null;
        ai.GetComponent<EnemyHealth>().Init(hp);
        return ai;
    }

    static float Speed(EnemyAI ai) => ai.GetComponent<NavMeshAgent>().speed;

    // ------------------------------------------------------------------ in game (Isla del Faro)
    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var player = gm.Player;
        var inter = player.GetComponent<PlayerInteractor>();
        var rm = RoundManager.Instance;
        var eco = gm.Economy;
        player.GodMode = true;
        yield return Wait(1f);
        gm.AddPoints(100000);   // (rounds keep running: DebugEndRound clears the old men when needed)

        // 1. one door per round, the next round another one
        while (rm.Round < 1) yield return null;
        var door1 = Object.FindObjectsByType<ZoneDoor>().First(d => d.name.StartsWith("Door1_"));
        var door2 = Object.FindObjectsByType<ZoneDoor>().First(d => d.name.StartsWith("Door2_"));
        door1.Interact(inter);
        yield return CloseCards();
        int z = ZoneManager.Instance.OpenCount, p = gm.Points;
        door2.Interact(inter);
        yield return null;
        Check(door2 != null && ZoneManager.Instance.OpenCount == z && gm.Points == p, $"round {rm.Round}: a second door is refused ('{(door2 != null ? door2.Prompt(inter) : "")}')");
        int round = rm.Round;
        rm.DebugEndRound();
        float t0 = Time.time;
        while (rm.Round == round && Time.time - t0 < 40f) { rm.DebugEndRound(); yield return null; }
        Check(rm.Round == round + 1, $"next round came ({rm.Round})");
        z = ZoneManager.Instance.OpenCount;
        door2.Interact(inter);
        yield return CloseCards();
        Check(ZoneManager.Instance.OpenCount > z, $"round {rm.Round}: the next door opens");
        rm.DebugEndRound();

        // 2. Julio fires the old men up
        var ground = player.transform.position + player.transform.forward * 6f;
        var a = Spawn(ground, 100000f);
        yield return Wait(0.8f);
        float before = Speed(a);
        EasterEgg.Instance.SongStarted = true;
        yield return Wait(0.8f);
        float after = Speed(a);
        Check(Mathf.Abs(after / before - eco.julioSpeedFactor) < 0.02f, $"the Julio records fire the old men up: speed {before:0.00} -> {after:0.00} (x{eco.julioSpeedFactor})");
        EasterEgg.Instance.SongStarted = false;
        a.GetComponent<EnemyHealth>().TakeDamage(1e9f, false, true);
        yield return Wait(0.8f);

        // 3. hurt old men: glasses (slower) or cane (faster)
        int glasses = 0, cane = 0, props = 0;
        for (int i = 0; i < 8; i++)
        {
            var o = Spawn(ground, 1000f);
            if (o == null) continue;
            yield return Wait(0.6f);
            float s0 = Speed(o);
            var h = o.GetComponent<EnemyHealth>();
            EnemyHits.ApplyLocal(h, 500f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
            Check(o.HurtState == 0, $"old man {i}: half health, still fine");
            EnemyHits.ApplyLocal(h, 300f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
            yield return Wait(0.7f);
            float s1 = Speed(o);
            if (o.HurtState == 1) { glasses++; Check(Mathf.Abs(s1 / s0 - o.Variant.glassesSpeedFactor) < 0.02f, $"old man {i}: lost his glasses, speed {s0:0.00} -> {s1:0.00}"); }
            else if (o.HurtState == 2) { cane++; Check(Mathf.Abs(s1 / s0 - o.Variant.caneSpeedFactor) < 0.02f, $"old man {i}: dropped his cane, speed {s0:0.00} -> {s1:0.00}"); }
            else Check(false, $"old man {i}: at 20 % health nothing fell");
            props += Object.FindObjectsByType<Transform>().Count(t => t.name == "GafasCaidas" || t.name == "BastonCaido") > 0 ? 1 : 0;
            if (i == 0) Shot("Renders/Feedback4_herido.png", o.transform.position);
            h.TakeDamage(1e9f, false, true);
            yield return Wait(0.3f);
        }
        Check(glasses > 0 && cane > 0, $"both happen: glasses {glasses}, cane {cane}");
        Check(props == 8, $"the glasses / cane fall to the ground every time ({props}/8)");

        // 4. the compass
        var compass = Object.FindAnyObjectByType<Compass>();
        Check(compass != null, "the compass is on the HUD (option on)");
        yield return Wait(1.2f);
        if (compass != null) Check(compass.Target != null && compass.TargetName.Length > 0, $"the compass points at '{compass.TargetName}'");
        ShotHud("Renders/Feedback4_brujula.png");

        // 5. the false teeth
        var grenade = player.GetComponent<PlayerGrenade>();
        Check(grenade != null && grenade.Count == eco.dentaduraCount, $"the player carries {grenade?.Count} false teeth");
        var hud = Object.FindAnyObjectByType<PlayerHUD>();
        Check(hud != null && hud.GrenadeText.Contains("x" + eco.dentaduraCount), $"HUD: '{hud?.GrenadeText}'");
        var targets = new List<EnemyHealth>();
        Vector3 spot = player.transform.position + player.transform.forward * 5f;
        for (int i = 0; i < 3; i++) { var o = Spawn(spot, 100000f); if (o != null) targets.Add(o.GetComponent<EnemyHealth>()); }
        yield return Wait(1.2f);
        int bitten = -1;
        System.Action<Dentadura, int> onBite = (d, n) => { if (d.Hurts) bitten = n; };
        Dentadura.Bit += onBite;
        var c = targets.Select(e => e.transform.position).Aggregate(Vector3.zero, (s, v) => s + v) / Mathf.Max(1, targets.Count);
        Dentadura.Throw(c + Vector3.up * 3f, Vector3.down * 2f, player, true);
        float tb = Time.time;
        while (bitten < 0 && Time.time - tb < 4f) yield return null;
        Dentadura.Bit -= onBite;
        Check(bitten >= targets.Count, $"the false teeth bite {bitten} old men of {targets.Count} in the radius");
        Check(targets.All(e => e.MaxHealth - e.Current >= eco.dentaduraDamage - 1f), $"each loses {eco.dentaduraDamage:0} health");
        foreach (var e in targets) e.TakeDamage(1e9f, false, true);
        bool thrown = grenade.TryThrow();
        yield return Wait(0.2f);
        Check(thrown && grenade.Count == eco.dentaduraCount - 1 && hud.GrenadeText.Contains("x" + (eco.dentaduraCount - 1)), $"R1 throws one: {grenade.Count} left, HUD '{hud.GrenadeText}'");
        yield return Wait(2f);
        PowerUpManager.Instance.Apply(PowerUpKind.MaxAmmo, player.GetComponent<PlayerShooter>());
        yield return Wait(0.2f);
        Check(grenade.Count == eco.dentaduraCount, $"the Nevera de munición brings them back ({grenade.Count})");
        Write("done");
    }

    static IEnumerator CloseCards()
    {
        for (int i = 0; i < 20; i++)
        {
            var cm = CardManager.Instance;
            if (cm != null && cm.CurrentOffer != null) { cm.Choose(cm.CurrentOffer[0]); yield return Real(0.2f); }
            else yield return Real(0.05f);
        }
    }

    // ------------------------------------------------------------------ main menu: the map carousel
    static IEnumerator MenuScript()
    {
        var mm = Object.FindAnyObjectByType<MainMenu>();
        yield return Real(1.5f);
        mm.OpenMapSelector();
        yield return Real(0.8f);
        int n = mm.MapCount;
        Check(n >= 2, $"{n} maps in the carousel");
        Check(Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include).Count(b => b.name.StartsWith("Map_")) == n, "every map keeps its Map_<id> button (far cards of the carousel are hidden)");
        int start = mm.MapIndex;
        var mid = mm.MapCard(start);
        Check(mid != null && Mathf.Abs(mid.anchoredPosition.x) < 1f && mid.localScale.x > 0.99f, $"map {start} big in the middle");
        bool inside = true;
        for (int i = 0; i < n; i++)
        {
            var c = mm.MapCard(i);
            if (!c.gameObject.activeSelf) continue;
            float half = 270f * c.localScale.x;
            if (Mathf.Abs(c.anchoredPosition.x) - half > 960f + 1f) inside = false;
        }
        Check(inside, "no card that is shown starts beyond the screen edge (1920 wide)");
        ShotMenu("Renders/Feedback4_carrusel.png");
        mm.MapStep(1);
        yield return Real(0.8f);
        Check(mm.MapIndex == (start + 1) % n, $"► moves to map {mm.MapIndex}");
        var next = mm.MapCard(mm.MapIndex);
        Check(Mathf.Abs(next.anchoredPosition.x) < 1f && next.localScale.x > 0.99f, "it slid to the middle");
        for (int i = 0; i < n; i++) mm.MapStep(-1);
        yield return Real(1f);
        Check(mm.MapIndex == (start + 1) % n, "◄ all the way round wraps back to the same map");
        ShotMenu("Renders/Feedback4_carrusel2.png");
        Write("done");
    }

    // ------------------------------------------------------------------ pictures
    internal static void Shot(string path, Vector3 at)
    {
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        go.transform.position = at + new Vector3(2.5f, 1.8f, 2.5f);
        go.transform.LookAt(at + Vector3.up * 0.8f);
        Render(cam, path, false);
        Object.Destroy(go);
    }

    internal static void ShotHud(string path) => Render(GameManager.Instance.CameraRig.GetComponent<Camera>(), path, true);

    static void ShotMenu(string path)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        Render(cam, path, true);
    }

    internal static void Render(Camera cam, string path, bool ui)
    {
        const int w = 1600, h = 900;
        var canvases = ui ? Object.FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToList() : new List<Canvas>();
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
        rt.Release();
    }
}
