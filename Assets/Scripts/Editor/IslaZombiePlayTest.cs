// Scripted play test of the Isla Zombie map (Tools > Island > Play Test Isla Zombie).
// Enters play mode, checks the first round (spawns + chasing), buys every door in order, walks the
// player through the doorways / cave / temple with the CharacterController, kills the boss and
// extracts. Results go to EditorPrefs ("IslaZombiePlayTest.Log") because the console overflows;
// screenshots go to Renders/. Play mode keeps advancing with the editor in the background.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class IslaZombiePlayTest
{
    const string Key = "IslaZombiePlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static int step;
    static float stepTime, realTime;
    static int doorIndex, missionStep;
    static float missionT;
    static readonly Dictionary<EnemyAI, float> firstDistance = new Dictionary<EnemyAI, float>();
    static readonly int[] DoorOrder = { 1, 3, 2, 4, 5, 6, 7 };

    static IslaZombiePlayTest()
    {
        if (EditorPrefs.GetBool(Key + "Running")) Hook();
    }

    static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    [MenuItem("Tools/Island/Play Test Isla Zombie")]
    public static void Run() => RunOn(TestMapSetup.IslaZombieScenePath);

    // The same test on the Phase 3 copy (positions are Isla del Faro's: valid while the copy keeps its zones).
    [MenuItem("Tools/Island/Play Test Isla Zombie (alturas)")]
    public static void RunAlturas() => RunOn(IslaZombieSetup.IslaAlturas.ScenePath);

    public static void RunOn(string scenePath)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EditorPrefs.SetBool(Key + "SlowThisRun", EditorPrefs.GetBool(Key + "Slow"));   // RunSlow() applies to this run only
        EditorPrefs.SetBool(Key + "Slow", false);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        step = 0; doorIndex = 0; missionStep = 0; firstDistance.Clear();
        Hook();   // with domain reload enabled the static constructor hooks it again after entering play mode
        EditorApplication.EnterPlaymode();
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");

    // Set to true to run the whole test at x1 (repeat a failure that happened with the fast clock).
    public static void RunSlow() { EditorPrefs.SetBool(Key + "Slow", true); Run(); }

    static void Finish(string status)
    {
        TestClock.Normal();
        EditorPrefs.SetString(Key + "Status", status);
        EditorPrefs.SetBool(Key + "Running", false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void Next() { step++; stepTime = Time.time; realTime = Time.realtimeSinceStartup; }

    static void Tick()
    {
        if (!EditorPrefs.GetBool(Key + "Running")) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) return;   // still entering play mode
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        try { Step(); }
        catch (System.Exception e) { Write("EXCEPTION: " + e); Finish("failed"); }
    }

    static void Step()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        if (gm.State == GameState.GameOver) { Write($"FAIL player died at step {step} (t={Time.time:F0}s)"); Finish("failed"); return; }
        var player = gm.Player;
        float t = Time.time - stepTime, rt = Time.realtimeSinceStartup - realTime;

        switch (step)
        {
            case 0:
                Write($"Player {gm.Animal.id} spawned at {player.transform.position}");
                player.GodMode = true;   // the idle test player must survive the round-1 chase (old men hit for 35: 3 hits kill)
                if (!EditorPrefs.GetBool(Key + "SlowThisRun")) TestClock.Fast();   // the chase check below only needs enemies to get closer
                Next();
                break;

            case 1:   // first round: enemies spawn in the start zone and come for the player
                if (t < 10f) return;
                var enemies = Object.FindObjectsByType<EnemyAI>();
                Write($"t=10s round={RoundManager.Instance.Round} enemies={enemies.Length} playerHP={player.CurrentHealth}");
                foreach (var e in enemies) firstDistance[e] = Vector3.Distance(e.transform.position, player.transform.position);
                Shot("Renders/IslaZombie_play_start.png");
                Next();
                break;

            case 2:
                if (t < 6f) return;
                int closer = 0, total = 0;
                foreach (var kv in firstDistance)
                {
                    if (kv.Key == null) continue;
                    total++;
                    if (Vector3.Distance(kv.Key.transform.position, player.transform.position) < kv.Value - 1f) closer++;
                }
                Write($"t=16s {closer}/{total} enemies got closer to the player (chasing); playerHP={player.CurrentHealth}");
                Write((total > 0 && closer >= total / 2 ? "OK " : "FAIL") + " round 1 spawns and chases");
                player.SetIgnored(100000f);   // from here on the enemies leave the test player alone
                TestClock.Normal();
                gm.AddPoints(100000);
                Next();
                break;

            case 3:   // buy every door, one per 0.3 s, taking the first card offered
                if (rt < 0.3f) return;
                if (CardManager.Instance.CurrentOffer != null) { CardManager.Instance.Choose(CardManager.Instance.CurrentOffer[0]); return; }
                if (doorIndex < DoorOrder.Length)
                {
                    int n = DoorOrder[doorIndex];
                    var door = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.name.StartsWith("Door" + n + "_"));
                    if (door == null) { Write($"FAIL door {n} not found"); doorIndex = 99; return; }
                    var interactor = player.GetComponent<PlayerInteractor>();
                    var m = BoatMission.Instance;
                    if (door.RequiresMission && m != null && !m.Complete)
                    {
                        // the dock: closed until the boat mission is done (light, petrol, call with the key instead of the radio)
                        if (missionStep == 0)
                        {
                            int p0 = gm.Points, z0 = ZoneManager.Instance.OpenCount;
                            Write("dock prompt before the mission: '" + door.Prompt(interactor) + "'");
                            door.Interact(interactor);
                            Write((gm.Points == p0 && ZoneManager.Instance.OpenCount == z0 && door != null ? "OK " : "FAIL") + " the Muelle stays closed (and free) before the boat mission");
                            var sw = Object.FindAnyObjectByType<PowerSwitch>();
                            if (sw != null) sw.Interact(interactor);
                            foreach (var it in Object.FindObjectsByType<MissionItem>()) if (it.ItemKind == MissionItem.Kind.Gasolina) it.Interact(interactor);
                            var key = MasterKey.Instance;
                            if (key != null) { Write($"llave maestra at {key.transform.position}"); key.Interact(interactor); }
                            var st = Object.FindAnyObjectByType<CallStation>();
                            Write("emisora prompt with petrol + key: '" + (st != null ? st.Prompt(interactor) : "NO STATION") + "'");
                            if (st != null) st.Interact(interactor);
                            Write((m.Power && m.Gas && m.Radio && m.Called && key != null && key.Used ? "OK " : "FAIL")
                                + $" light on, petrol picked, boat called with the Llave Maestra instead of the radio (arrives after round {m.DefenseUntilRound})");
                            missionStep = 1;
                            missionT = Time.realtimeSinceStartup;
                            return;
                        }
                        // hold out: end rounds until the boat arrives
                        if (Time.realtimeSinceStartup - missionT > 90f) { Write("FAIL the boat never arrived"); doorIndex = 99; return; }
                        RoundManager.Instance.DebugEndRound();
                        return;
                    }
                    if (door.RequiresMission && missionStep == 1)
                    {
                        Write($"OK  boat at the dock after round {m.DefenseUntilRound} (now round {RoundManager.Instance.Round}); dock prompt: '{door.Prompt(interactor)}'");
                        missionStep = 2;
                    }
                    doorIndex++;
                    int before = ZoneManager.Instance.OpenCount, pts = gm.Points;
                    door.Interact(interactor);
                    Write($"door {n} '{door.name}': paid {pts - gm.Points}, open zones {before} -> {ZoneManager.Instance.OpenCount}");
                    if (door.RequiresMission) Write((pts == gm.Points && ZoneManager.Instance.OpenCount > before ? "OK " : "FAIL") + " Muelle opened for free once the boat mission was done");
                    realTime = Time.realtimeSinceStartup;
                    return;
                }
                var open = ZoneManager.Instance.Zones.Where(z => z.IsOpen).Select(z => z.displayName);
                Write($"open zones: {string.Join(", ", open)} ({ZoneManager.Instance.OpenCount}/{ZoneManager.Instance.Zones.Count})");
                Write((ZoneManager.Instance.OpenCount == ZoneManager.Instance.Zones.Count ? "OK " : "FAIL") + " all zones opened by the doors");
                Write((BossEncounter.Instance.Boss != null ? "OK " : "FAIL") + " boss spawned when the Muelle opened");
                Next();
                break;

            case 4:   // doors gone: walk the player along NavMesh paths with the CharacterController
                if (rt < 1f) return;
                foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(e.Current + 1f, false);
                Walk(player, "Chiringuito -> extraction (all doors open)", new Vector3(-32, 0, 4), new Vector3(46, 0, 2));
                Walk(player, "through the cave labyrinth (Parking corner -> Faro corner)", new Vector3(-4, 0, 18), new Vector3(4, 0, 18));
                Walk(player, "Camping -> Lago through the temple", new Vector3(-10, 0, 2), new Vector3(10, 0, 2));
                Walk(player, "Mercadillo maze: door -> record", new Vector3(14, 0, -10), new Vector3(10, 0, -28));
                Write((gm.BossDefeated ? "OK " : "FAIL") + " boss defeated");
                Teleport(player, new Vector3(-10, 0, 24), 0f);
                Next();
                break;

            case 5:
                if (rt < 1.5f) return;
                Shot("Renders/IslaZombie_play_cave.png");
                Teleport(player, new Vector3(-8, 0, 2), 90f);
                Next();
                break;

            case 6:
                if (rt < 1.5f) return;
                Shot("Renders/IslaZombie_play_temple.png");
                Teleport(player, new Vector3(-30, 0, 4), 90f);
                Next();
                break;

            case 7:
                if (rt < 1.5f) return;
                Shot("Renders/IslaZombie_play_door.png");
                var ext = Object.FindAnyObjectByType<ExtractionPoint>();
                Teleport(player, ext.transform.position + Vector3.left * 1.5f, 90f);
                ext.Interact(player.GetComponent<PlayerInteractor>());
                Write((gm.State == GameState.Victory ? "OK " : "FAIL") + $" extraction -> state {gm.State}, run time {gm.RunTime:F0}s");
                Finish("done");
                break;
        }
    }

    static void Teleport(PlayerHealth player, Vector3 pos, float yaw)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        if (NavAgents.Sample(pos, 4f, out var h)) pos = h;
        player.transform.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.Euler(0, yaw, 0));
        cc.enabled = true;
    }

    // Follows the NavMesh path from a to b moving the CharacterController in small steps; reports
    // whether the player (not just the enemies' NavMesh) physically fits through.
    static void Walk(PlayerHealth player, string label, Vector3 a, Vector3 b)
    {
        if (!NavMesh.SamplePosition(a, out var ha, 4f, NavAgents.Filter(0)) || !NavMesh.SamplePosition(b, out var hb, 4f, NavAgents.Filter(0)))
        { Write($"FAIL walk {label}: endpoints not on the NavMesh"); return; }
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(ha.position, hb.position, NavAgents.Filter(0), path) || path.status != NavMeshPathStatus.PathComplete)
        { Write($"FAIL walk {label}: no NavMesh path ({path.status})"); return; }
        Teleport(player, ha.position, 0f);
        var cc = player.GetComponent<CharacterController>();
        float walked = 0f, stuck = 0f;
        foreach (var corner in path.corners.Skip(1))
            for (int i = 0; i < 2000; i++)
            {
                Vector3 d = corner - player.transform.position;
                d.y = 0f;
                if (d.magnitude < 0.3f) break;
                Vector3 before = player.transform.position;
                cc.Move(d.normalized * 0.25f + Vector3.down * 0.3f);
                float moved = Vector3.Distance(new Vector3(before.x, 0, before.z), new Vector3(player.transform.position.x, 0, player.transform.position.z));
                walked += moved;
                stuck = moved < 0.05f ? stuck + 1 : 0;
                if (stuck > 20) { Write($"FAIL walk {label}: player blocked at {player.transform.position} (heading to {corner})"); return; }
            }
        float miss = Vector3.Distance(player.transform.position, hb.position);
        Write($"{(miss < 1.5f ? "OK " : "FAIL")} walk {label}: {walked:F0} m, ended {miss:F1} m from target");
    }

    static void Shot(string path)
    {
        var cam = Camera.main;
        if (cam == null) return;
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
