// Tools > Island > Play Test Alturas: the heights format on the small test map (Assets/Maps/PruebaAlturas,
// Docs/FASE3_FORMATO.md section 6). Before Play: broken layouts (reversed slide, 1-cell ramp) must fail the checks.
// In Play: walk up the ramp, a normal jump can't climb a ledge but the Rana's super jump can, dropping hurts nothing,
// old men drop down ledges (one-way links) and walk up the ramp, the closed door seals Arriba (also against the super
// jump), the slide carries a player down and never up, old men use it too, bullets stop at the slope and the slide
// wall, and the camera never ends inside the slope. Results in EditorPrefs "HeightsPlayTest.Log" / ".Status".
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class HeightsPlayTest
{
    const string Key = "HeightsPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static int linkJumps, slides;

    [MenuItem("Tools/Island/Play Test Alturas")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        if (!LayoutChecks()) { Finish("failed"); return; }
        var scene = IslaZombieSetup.Prueba.ScenePath;
        if (!File.Exists(scene)) { Write("FAIL no " + scene + " (run Tools > Island > Setup Prueba de alturas)"); Finish("failed"); return; }
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        EditorPrefs.SetBool(Key + "Running", true);
        script.Clear();
        linkJumps = slides = 0;
        EnemyAI.LinkJumped -= OnLink; EnemyAI.LinkJumped += OnLink;
        OneWaySlide.Slid -= OnSlid; OneWaySlide.Slid += OnSlid;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLink(EnemyAI e) => linkJumps++;
    static void OnSlid(PlayerHealth p) => slides++;
    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");
    static void Check(bool ok, string what) => Write((ok ? "OK   " : "FAIL ") + what);

    static void Finish(string status)
    {
        EditorPrefs.SetString(Key + "Status", Log.Contains("FAIL") && status == "done" ? "failed" : status);
        EditorPrefs.SetBool(Key + "Running", false);
        EditorApplication.update -= Tick;
        EnemyAI.LinkJumped -= OnLink;
        OneWaySlide.Slid -= OnSlid;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    // ------------------------------------------------------------------ grid checks on broken copies of the layout
    static bool LayoutChecks()
    {
        var path = IslaZombieSetup.Prueba.LayoutPath;
        if (!File.Exists(path)) { Write("FAIL no " + path); return false; }
        var lines = File.ReadAllLines(path);
        var good = IslaZombieSetup.CheckLayoutLines(lines);
        Check(!good.Any(l => l.StartsWith("ERROR") || l.StartsWith("FAIL")), "test layout passes the grid checks: " + string.Join(" | ", good));
        // reversed slide: the Abajo side of the slide raised to level 2 -> the slide goes from Abajo (start) up into Arriba
        var rev = (string[])lines.Clone();
        int hRow = System.Array.FindIndex(rev, l => l.Trim() == "[heights]") + 1 + 5;   // row 5 of [heights]
        rev[hRow] = rev[hRow].Substring(0, 8) + "2" + rev[hRow].Substring(9);
        var revReport = IslaZombieSetup.CheckLayoutLines(rev);
        Check(revReport.Any(l => l.StartsWith("ERROR slide")), "a slide into a zone that can still be closed is an error: " + revReport.FirstOrDefault(l => l.StartsWith("ERROR slide")));
        // a ramp of one cell (45 degrees) is an error
        var ramp = (string[])lines.Clone();
        int rRow = System.Array.FindIndex(ramp, l => l.Trim() == "[heights]") + 1 + 2;
        ramp[rRow] = ramp[rRow].Replace("0>>1", "00>1");
        var rampReport = IslaZombieSetup.CheckLayoutLines(ramp);
        Check(rampReport.Any(l => l.Contains("ramp run") && l.Contains("1 cells long")), "a 1-cell ramp is an error: " + rampReport.FirstOrDefault(l => l.Contains("ramp run")));
        IslaZombieSetup.CheckLayoutLines(lines);   // leave the real layout loaded
        return true;
    }

    // ------------------------------------------------------------------ play
    static void Tick()
    {
        if (!EditorPrefs.GetBool(Key + "Running")) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        ZoneDoor.IgnoreRoundLimit = true;   // opens several doors in one round (the one-door-per-round rule is checked in SmokePlayTest)
        try
        {
            var cm = CardManager.Instance;
            if (cm != null && cm.CurrentOffer != null) { cm.Choose(cm.CurrentOffer[0]); return; }
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

    static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    // Test map grid -> world (origin cell (8, 4), 2 m cells, level = 2 m).
    static Vector3 W(int c, int r, int level) => new Vector3((c - 8) * 2f, level * IslaZombieSetup.LevelH, (4 - r) * 2f);

    static void Teleport(PlayerHealth p, Vector3 pos, float yaw)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
        cc.enabled = true;
    }

    // Moves the CharacterController towards `to` (horizontal) for up to `seconds`, one step per frame, with gravity.
    static IEnumerator Move(PlayerHealth p, Vector3 dir, float seconds, float speed = 4f)
    {
        var cc = p.GetComponent<CharacterController>();
        dir.y = 0f; dir.Normalize();
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            if (cc.enabled) cc.Move(dir * speed * Time.deltaTime);
            yield return null;
        }
    }

    static EnemyAI Spawn(RoundManager rm, Vector3 at)
    {
        var ai = rm.SpawnEnemy(null, at);
        var agent = ai.GetComponent<NavMeshAgent>();
        if (NavAgents.Sample(at, 1.5f, out var p)) agent.Warp(p);
        return ai;
    }

    static void KillAll()
    {
        foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(e.Current + 1f, false);
    }

    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var rm = RoundManager.Instance;
        var player = gm.Player;
        var pc = player.GetComponent<PlayerController>();
        rm.SpawningPaused = true;
        yield return Wait(0.5f);
        KillAll();
        player.GodMode = false;   // steps 1-3: no old men around, and a drop must not hurt

        // 1) up the ramp: row 2, from (2,2) level 0 east to the terrace
        Teleport(player, W(2, 2, 0), 90f);
        yield return Wait(0.3f);
        yield return Move(player, Vector3.right, 1.6f);
        Check(player.transform.position.y > 1.7f && player.transform.position.x > W(5, 2, 0).x - 0.5f,
            $"walks up the 2-cell ramp onto the terrace (y {player.transform.position.y:F2}, x {player.transform.position.x:F1})");

        // 2) a normal jump does not climb a ledge, the Rana's super jump (4 m) does
        Teleport(player, W(6, 4, 0) + Vector3.back * 0.6f, 0f);
        yield return Wait(0.3f);
        pc.Launch(1.2f);
        yield return Move(player, Vector3.forward, 1.0f, 3f);
        Check(player.transform.position.y < 1f, $"a normal jump (1.2 m) does not climb a 2 m ledge (y {player.transform.position.y:F2})");
        Teleport(player, W(6, 4, 0) + Vector3.back * 0.6f, 0f);
        yield return Wait(0.3f);
        pc.Launch(4f);
        yield return Move(player, Vector3.forward, 1.2f, 3f);
        Check(player.transform.position.y > 1.7f, $"the super jump (4 m) climbs it (y {player.transform.position.y:F2})");

        // 3) dropping off the ledge costs nothing
        Teleport(player, W(6, 3, 1), 180f);
        yield return Wait(0.3f);
        int hp = player.CurrentHealth;
        yield return Move(player, Vector3.back, 1.0f);
        yield return Wait(0.5f);
        Check(player.transform.position.y < 0.3f && player.CurrentHealth == hp && gm.State == GameState.Playing,
            $"dropping off the ledge: down to y {player.transform.position.y:F2}, health {hp} -> {player.CurrentHealth}");

        // 4) an old man on the terrace drops down the ledge (one-way link) to reach the player below
        player.GodMode = true;
        Teleport(player, W(7, 6, 0), 0f);
        int jumps0 = linkJumps;
        var oldOnTop = Spawn(rm, W(7, 2, 1));
        float t0 = Time.time;
        while (Time.time - t0 < 10f && (oldOnTop.transform.position.y > 0.5f || linkJumps == jumps0)) yield return null;
        Check(oldOnTop.transform.position.y < 0.5f && linkJumps > jumps0, $"old man on the terrace drops down a ledge link ({linkJumps - jumps0} jumps, y {oldOnTop.transform.position.y:F2}, {Time.time - t0:F1} s)");
        KillAll();

        // 5) an old man below walks UP the ramp (never up a ledge) to reach the player on the terrace
        Teleport(player, W(8, 2, 1), 270f);
        jumps0 = linkJumps;
        var oldBelow = Spawn(rm, W(2, 6, 0));
        t0 = Time.time;
        while (Time.time - t0 < 15f && oldBelow.transform.position.y < 1.7f) yield return null;
        Check(oldBelow.transform.position.y > 1.7f && linkJumps == jumps0, $"old man below climbs the ramp to the terrace (y {oldBelow.transform.position.y:F2}, {Time.time - t0:F1} s, {linkJumps - jumps0} link jumps)");
        KillAll();

        // 6) the closed door seals Arriba: no NavMesh path, and the super jump can't get over the walls
        var path = new NavMeshPath();
        NavMesh.SamplePosition(W(6, 3, 1), out var a, 1f, NavAgents.Filter(0));
        NavMesh.SamplePosition(W(12, 4, 1), out var b, 1f, NavAgents.Filter(0));
        bool open0 = NavMesh.CalculatePath(a.position, b.position, NavAgents.Filter(0), path) && path.status == NavMeshPathStatus.PathComplete;
        Check(!open0, "with the door closed there is no path from Abajo to Arriba");
        Teleport(player, W(8, 2, 1), 90f);
        yield return Wait(0.3f);
        pc.Launch(4f);
        yield return Move(player, Vector3.right, 1.3f, 3f);
        Check(player.transform.position.x < W(9, 2, 0).x - 0.9f, $"super jump from the terrace does not cross the wall into Arriba (x {player.transform.position.x:F2})");
        Teleport(player, W(8, 5, 0), 90f);
        yield return Wait(0.3f);
        pc.Launch(4f);
        yield return Move(player, Vector3.right, 1.3f, 3f);
        Check(player.transform.position.x < W(9, 5, 0).x - 0.9f && slides == 0, $"super jump at the slide's bottom hatch does not get up into Arriba (x {player.transform.position.x:F2})");

        // 7) open the door
        var door = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault();
        door.DebugOpen();
        yield return Wait(0.5f);
        bool open1 = NavMesh.CalculatePath(a.position, b.position, NavAgents.Filter(0), path) && path.status == NavMeshPathStatus.PathComplete;
        Check(open1, "with the door open Arriba is reachable");

        // 8) the slide carries the player down from Arriba to Abajo, and never up
        var slide = Object.FindAnyObjectByType<OneWaySlide>();
        Check(slide != null, "the slide has its OneWaySlide");
        Teleport(player, W(11, 5, 1), 270f);
        yield return Wait(0.3f);
        int s0 = slides;
        yield return Move(player, Vector3.left, 1.5f, 3f);
        yield return Wait(0.8f);
        Check(slides > s0 && player.transform.position.x < W(9, 5, 0).x - 0.9f && player.transform.position.y < 0.4f,
            $"the slide takes the player from Arriba down to Abajo (at {player.transform.position})");
        Teleport(player, W(8, 5, 0), 90f);
        yield return Wait(0.3f);
        s0 = slides;
        yield return Move(player, Vector3.right, 1.5f, 3f);
        Check(slides == s0 && player.transform.position.x < W(9, 5, 0).x - 0.9f, "walking into the bottom hatch does nothing (one way)");

        // 9) an old man in Arriba comes down to a player in Abajo (slide link or the door + a ledge)
        Teleport(player, W(6, 5, 0), 90f);
        jumps0 = linkJumps;
        var oldUp = Spawn(rm, W(12, 5, 1));
        t0 = Time.time;
        while (Time.time - t0 < 12f && !(linkJumps > jumps0 && oldUp.transform.position.x < W(9, 5, 0).x - 0.9f && oldUp.transform.position.y < 1f)) yield return null;
        // the slide is by far the shortest way: he must take it (a finished link jump), not walk round by the door
        Check(linkJumps > jumps0 && oldUp.transform.position.x < W(9, 5, 0).x - 0.9f && oldUp.transform.position.y < 1f,
            $"old man from Arriba comes down the slide to Abajo ({Time.time - t0:F1} s, {linkJumps - jumps0} link jumps, at {oldUp.transform.position})");
        KillAll();

        // 10) bullets stop at the slope and at the slide wall
        Vector3 from = W(6, 6, 0) + Vector3.up;
        bool hitSlope = Physics.Raycast(from, Vector3.forward, out var h1, 20f, ~0, QueryTriggerInteraction.Ignore);
        Check(hitSlope && h1.distance < 5.2f, $"a shot at the terrace hits its slope ({(hitSlope ? h1.collider.name + " at " + h1.distance.ToString("F1") + " m" : "nothing")})");
        from = W(7, 5, 0) + Vector3.up;
        bool hitWall = Physics.Raycast(from, Vector3.right, out var h2, 20f, ~0, QueryTriggerInteraction.Ignore);
        Check(hitWall && h2.distance < 3.2f, $"a shot at the slide wall stops there ({(hitWall ? h2.collider.name + " at " + h2.distance.ToString("F1") + " m" : "nothing")})");

        // 11) the camera never ends inside the slope (player against it, looking away)
        Teleport(player, W(6, 4, 0) + Vector3.back * 0.4f, 180f);
        yield return Wait(1.2f);
        var cam = Camera.main;
        int solid = (1 << 0) | (1 << TestMapSetup.WallLayer);
        bool inside = cam != null && Physics.CheckSphere(cam.transform.position, 0.08f, solid, QueryTriggerInteraction.Ignore);
        Check(cam != null && !inside, $"camera with the player's back to the slope is not inside it (cam {(cam != null ? cam.transform.position.ToString() : "-")})");
        Write($"done: {linkJumps} link jumps, {slides} slides");
    }
}
