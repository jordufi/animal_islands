// Tools > Island > Play Test Plantas: real floors on the small test map (Assets/Maps/PruebaPlantas, Docs/PLANTAS_FORMATO.md).
// Before Play: the layout passes the grid checks and a copy without the void over a stair fails them (no headroom).
// In Play: the lift skips floors whose zone is closed and carries the player up; the chute drops the player from the roof
// to the ground floor and is jammed while that zone is closed; the service door stays shut without the pulsera and lets
// a player (and the NavMesh) through for a few seconds with it; the player's body walks both stairs up and down; an old
// man follows the player up two floors by the stairs and down the chute; the railing of the gallery holds; the camera
// stays under the slab; a shot upwards stops at the slab. Results in EditorPrefs "PlantasPlayTest.Log" / ".Status".
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class PlantasPlayTest
{
    const string Key = "PlantasPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static int linkJumps, chutes, rides;

    [MenuItem("Tools/Island/Play Test Plantas")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        if (!LayoutChecks()) { Finish("failed"); return; }
        var scene = IslaZombieSetup.PruebaPlantas.ScenePath;
        if (!File.Exists(scene)) { Write("FAIL no " + scene + " (run Tools > Island > Setup Prueba de plantas)"); Finish("failed"); return; }
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        EditorPrefs.SetBool(Key + "Running", true);
        script.Clear();
        linkJumps = chutes = rides = 0;
        EnemyAI.LinkJumped -= OnLink; EnemyAI.LinkJumped += OnLink;
        FloorLink.Used -= OnChute; FloorLink.Used += OnChute;
        Lift.Rode -= OnRide; Lift.Rode += OnRide;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLink(EnemyAI e) => linkJumps++;
    static void OnChute(PlayerHealth p, FloorLink l) => chutes++;
    static void OnRide(Lift l, int a, int b) => rides++;
    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");
    static void Check(bool ok, string what) => Write((ok ? "OK   " : "FAIL ") + what);

    static void Finish(string status)
    {
        EditorPrefs.SetString(Key + "Status", Log.Contains("FAIL") && status == "done" ? "failed" : status);
        EditorPrefs.SetBool(Key + "Running", false);
        EditorApplication.update -= Tick;
        EnemyAI.LinkJumped -= OnLink;
        FloorLink.Used -= OnChute;
        Lift.Rode -= OnRide;
        ServiceDoor.HasDoorBracelet = p => false;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    // ------------------------------------------------------------------ grid checks
    static bool LayoutChecks()
    {
        var path = IslaZombieSetup.PruebaPlantas.LayoutPath;
        if (!File.Exists(path)) { Write("FAIL no " + path); return false; }
        var lines = File.ReadAllLines(path);
        var good = IslaZombieSetup.CheckLayoutLines(lines);
        Check(!good.Any(l => l.StartsWith("ERROR") || l.StartsWith("FAIL")), "test layout passes the grid checks: " + string.Join(" | ", good));
        // the void over the first stair filled in: the stair would have no headroom
        var bad = (string[])lines.Clone();
        int row = System.Array.FindIndex(bad, l => l.Trim() == "[map 1]") + 1 + 7;   // row 7 of the first floor
        bad[row] = bad[row].Replace("#~~~#", "#ccc#");
        var badReport = IslaZombieSetup.CheckLayoutLines(bad);
        Check(badReport.Any(l => l.Contains("no headroom")), "a stair without a void above it is an error: " + badReport.FirstOrDefault(l => l.Contains("no headroom")));
        IslaZombieSetup.CheckLayoutLines(lines);
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

    // Test map cell -> world (origin cell (12, 8), 2 m cells; floor bases 0 / 4 / 8 m).
    static Vector3 W(int c, int r, int floor) => new Vector3((c - 12) * 2f, floor * 4f, (8 - r) * 2f);

    static void Teleport(PlayerHealth p, Vector3 pos, float yaw = 0f)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
        cc.enabled = true;
    }

    static IEnumerator Push(PlayerHealth p, Vector3 dir, float seconds, float speed = 4f)
    {
        var cc = p.GetComponent<CharacterController>();
        dir.y = 0f; dir.Normalize();
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            if (cc.enabled) cc.Move(dir * speed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
            yield return null;
        }
    }

    // Walks the player's own CharacterController along the NavMesh path from a to b (ramps, stairs, doorways).
    static IEnumerator Walk(PlayerHealth p, string label, Vector3 a, Vector3 b)
    {
        // walking only: no drops, chutes or slides (the walk moves the body within one frame, so a chute would not trigger)
        var walkOnly = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas & ~(1 << 12) & ~(1 << 13) & ~(1 << NavAgents.CanalArea) };
        if (!NavMesh.SamplePosition(a, out var ha, 1.5f, walkOnly) || !NavMesh.SamplePosition(b, out var hb, 1.5f, walkOnly))
        { Write($"FAIL walk {label}: endpoints not on the NavMesh"); yield break; }
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(ha.position, hb.position, walkOnly, path) || path.status != NavMeshPathStatus.PathComplete)
        { Write($"FAIL walk {label}: no NavMesh path ({path.status})"); yield break; }
        Teleport(p, ha.position);
        yield return null;
        var cc = p.GetComponent<CharacterController>();
        float walked = 0f;
        foreach (var corner in path.corners.Skip(1))
        {
            int stuck = 0;
            for (int i = 0; i < 4000; i++)
            {
                Vector3 d = corner - p.transform.position;
                d.y = 0f;
                if (d.magnitude < 0.3f) break;
                Vector3 before = p.transform.position;
                cc.Move(d.normalized * 0.2f + Vector3.down * 0.3f);
                float moved = new Vector2(p.transform.position.x - before.x, p.transform.position.z - before.z).magnitude;
                walked += moved;
                stuck = moved < 0.04f ? stuck + 1 : 0;
                if (stuck > 20) { Write($"FAIL walk {label}: blocked at {p.transform.position} heading to {corner}"); yield break; }
            }
        }
        float miss = Vector3.Distance(p.transform.position, hb.position);
        Check(miss < 1.5f, $"walk {label}: {walked:F0} m, ended {miss:F1} m from the target (y {p.transform.position.y:F2})");
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

    static ZoneDoor Door(char digit) => Object.FindObjectsByType<ZoneDoor>().First(d => d.name.StartsWith("Door" + digit + "_"));

    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var rm = RoundManager.Instance;
        var player = gm.Player;
        var inter = player.GetComponent<PlayerInteractor>();
        rm.SpawningPaused = true;
        player.GodMode = true;
        yield return Wait(0.5f);
        KillAll();
        var filter = NavAgents.Filter(0);
        var path = new NavMeshPath();

        // 1) the lift: no stop above while the floors' zones are closed
        var lift = Lift.All.FirstOrDefault();
        Check(lift != null && lift.StopCount == 3, $"the lift has 3 stops ({(lift != null ? lift.StopCount : 0)})");
        if (lift == null) yield break;
        var up0 = Object.FindObjectsByType<LiftButton>().First(b => b.name == "Subir" && b.transform.parent == lift.Stop(0));
        Check(!up0.CanInteract(inter) && lift.NextStop(0, 1) < 0, "with the 1st floor and the roof closed the lift has nowhere to go");

        // 2) the service door between Entrada and Sala: shut without the pulsera, open a few seconds with it
        var sd = Object.FindAnyObjectByType<ServiceDoor>();
        Check(sd != null, "the service door is built");
        Vector3 inA = W(8, 11, 0), inG = W(13, 11, 0);
        NavMesh.SamplePosition(inA, out var pa, 1.5f, filter);
        NavMesh.SamplePosition(inG, out var pg, 1.5f, filter);
        // the service door only works when the zones on both sides are open: open the Sala, keep its paid door shut
        Object.FindObjectsByType<Zone>().First(z => z.displayName == "Sala doble").Open();
        Teleport(player, W(9, 11, 0), 90f);
        yield return Wait(0.3f);
        sd.Interact(inter);
        yield return Wait(0.3f);
        bool shut = !sd.IsOpenNow && !(NavMesh.CalculatePath(pa.position, pg.position, filter, path) && path.status == NavMeshPathStatus.PathComplete);
        Check(shut, "without the pulsera the service door stays shut (and no NavMesh path through it)");
        ServiceDoor.HasDoorBracelet = p => true;
        sd.Interact(inter);
        yield return Wait(0.6f);
        bool through = NavMesh.CalculatePath(pa.position, pg.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
        yield return Push(player, Vector3.right, 1.0f);
        Check(sd.IsOpenNow && through && player.transform.position.x > W(10, 11, 0).x + 0.5f, $"with the pulsera it opens: the player walks through (x {player.transform.position.x:F1}) and the NavMesh joins both sides");
        yield return Wait(3.5f);
        bool closedAgain = !sd.IsOpenNow && !(NavMesh.CalculatePath(pa.position, pg.position, filter, path) && path.status == NavMeshPathStatus.PathComplete);
        Check(closedAgain, "it shuts again after a few seconds");
        ServiceDoor.HasDoorBracelet = p => false;

        // 3) the lift rides once the 1st floor is open, then to the roof
        Door('2').DebugOpen();
        yield return Wait(0.5f);
        Teleport(player, lift.Stop(0).position, 0f);
        yield return Wait(0.3f);
        Check(up0.CanInteract(inter), "door 2 open: the lift goes up to the 1st floor");
        int r0 = rides;
        up0.Interact(inter);
        float t0 = Time.time;
        while (Time.time - t0 < 8f && rides == r0) yield return null;
        Check(rides > r0 && Mathf.Abs(player.transform.position.y - 4f) < 0.6f, $"ride 0 -> 1: the player is on the 1st floor (y {player.transform.position.y:F2}, {Time.time - t0:F1} s)");
        Door('3').DebugOpen();
        yield return Wait(0.5f);
        var up1 = Object.FindObjectsByType<LiftButton>().First(b => b.name == "Subir" && b.transform.parent == lift.Stop(1));
        r0 = rides;
        up1.Interact(inter);
        t0 = Time.time;
        while (Time.time - t0 < 8f && rides == r0) yield return null;
        Check(rides > r0 && Mathf.Abs(player.transform.position.y - 8f) < 0.6f, $"ride 1 -> 2: the player is on the roof (y {player.transform.position.y:F2})");

        // 4) the chute: jammed while the ground floor's zone is closed, then down to the ground floor
        var chute = Object.FindAnyObjectByType<FloorLink>();
        var entrada = Object.FindObjectsByType<Zone>().First(z => z.displayName == "Entrada");
        entrada.Close();
        Teleport(player, chute.Entry, 0f);
        yield return Wait(1.0f);
        Check(chutes == 0 && player.transform.position.y > 7.5f, "the chute is jammed while the zone below is closed");
        entrada.Open();
        Teleport(player, chute.Entry + Vector3.back * 2f, 0f);
        yield return Wait(0.2f);
        Teleport(player, chute.Entry, 0f);
        t0 = Time.time;
        while (Time.time - t0 < 4f && chutes == 0) yield return null;
        yield return Wait(0.2f);
        Check(chutes == 1 && player.transform.position.y < 0.6f && Vector3.Distance(player.transform.position, chute.Exit) < 1.5f, $"the chute drops the player from the roof to the ground floor (y {player.transform.position.y:F2})");

        // 5) every door open: the player's body walks both stairs up and down
        Door('1').DebugOpen();
        yield return Wait(0.5f);
        yield return Walk(player, "ground floor -> 1st floor (stair 1)", W(5, 12, 0), W(10, 12, 1));
        yield return Walk(player, "1st floor -> roof (stair 2)", W(10, 12, 1), W(10, 5, 2));
        yield return Walk(player, "roof -> ground floor (both stairs down)", W(10, 5, 2), W(14, 12, 0));

        // 6) an old man in the Sala follows the player up to the roof by the stairs
        Teleport(player, W(6, 4, 2), 0f);
        var climber = Spawn(rm, W(18, 10, 0));
        yield return null;
        float cy0 = climber != null ? climber.transform.position.y : 99f;
        t0 = Time.time;
        while (Time.time - t0 < 100f && climber != null && climber.transform.position.y < 7.5f) yield return null;
        Check(cy0 < 1f && climber != null && climber.transform.position.y > 7.5f, $"an old man follows the player from the ground floor (start y {cy0:F2}) up two stairs to the roof ({Time.time - t0:F1} s, y {(climber != null ? climber.transform.position.y : 0f):F2})");
        KillAll();

        // 7) an old man on the roof comes down the chute to a player beside its exit
        Teleport(player, W(5, 3, 0), 90f);
        int j0 = linkJumps;
        var diver = Spawn(rm, W(5, 10, 2));
        yield return null;
        float y0 = diver.transform.position.y;
        t0 = Time.time;
        while (Time.time - t0 < 25f && diver != null && diver.transform.position.y > 1f) yield return null;
        // by the stairs it would take well over 30 s: under 12 s means the chute
        Check(y0 > 7.5f && diver != null && diver.transform.position.y < 1f && Time.time - t0 < 12f,
            $"an old man on the roof (y {y0:F2}) comes down the chute to the ground floor ({Time.time - t0:F1} s, {linkJumps - j0} link jumps counted)");
        KillAll();

        // 8) the gallery railing holds a player pushing into the void
        Teleport(player, W(14, 10, 1), 0f);
        yield return Wait(0.3f);
        yield return Push(player, Vector3.forward, 1.2f);
        Check(player.transform.position.y > 3.5f, $"the gallery railing holds (y {player.transform.position.y:F2})");

        // 9) the camera stays under the slab of the floor above; a shot upwards stops at the slab
        Teleport(player, W(4, 3, 1) + Vector3.right * 0.3f, 180f);
        yield return Wait(1.2f);
        var cam = Camera.main;
        int solid = (1 << 0) | (1 << TestMapSetup.WallLayer);
        bool inside = cam != null && Physics.CheckSphere(cam.transform.position, 0.08f, solid, QueryTriggerInteraction.Ignore);
        Check(cam != null && !inside && cam.transform.position.y < 7.6f, $"camera on the 1st floor is not inside a wall or the slab (cam y {(cam != null ? cam.transform.position.y : 0f):F2})");
        bool hitSlab = Physics.Raycast(W(4, 3, 0) + Vector3.up * 1.5f, Vector3.up, out var hit, 10f, solid, QueryTriggerInteraction.Ignore);
        Check(hitSlab && hit.point.y < 4.1f, $"a shot upwards from the ground floor stops at the slab ({(hitSlab ? hit.collider.name + " at y " + hit.point.y.ToString("F2") : "nothing")})");
        Write($"done: {rides} lift rides, {chutes} chutes, {linkJumps} link jumps");
    }
}
