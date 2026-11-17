using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// "Donde subes tú, sube el abuelo" (user feedback 1, phase 2): the player is put on top of props he can reach by jumping
// (the Imserso bus with its ramp, a crate stack, a car, the crashed plane, a hut roof, a drink machine, the tractor...)
// and one old man is spawned on the ground a few metres away: he must get to him and hit him. Tools > Island >
// Play Test Climb. Log in EditorPrefs ClimbPlayTest.Log / .Status.
public static class ClimbPlayTest
{
    const string Key = "ClimbPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");
    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static string scenePath;

    [MenuItem("Tools/Island/Play Test Climb")]
    public static void Run() => RunOn("Assets/Scenes/IslaZombie.unity");
    public static void RunVillajubilada() => RunOn("Assets/Scenes/Villajubilada.unity");
    public static void RunVenecia() => RunOn("Assets/Scenes/Venecia.unity");

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
    static void Check(bool ok, string what) => Write((ok ? "OK   " : "FAIL ") + what);

    static void Finish(string status)
    {
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

    static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    // Top of the highest prop part under a point of the object (a spot the player can stand on).
    static bool TopOf(GameObject go, out Vector3 top)
    {
        top = default;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float best = -1f;
        for (float fx = 0.3f; fx <= 0.7f; fx += 0.1f)
            for (float fz = 0.3f; fz <= 0.7f; fz += 0.1f)
            {
                var o = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), b.max.y + 2f, Mathf.Lerp(b.min.z, b.max.z, fz));
                if (Physics.Raycast(o, Vector3.down, out var hit, b.size.y + 3f, 1 << NavAgents.PropLayer, QueryTriggerInteraction.Ignore)
                    && hit.normal.y > 0.7f && hit.point.y > best
                    && !Physics.CheckCapsule(hit.point + Vector3.up * 0.4f, hit.point + Vector3.up * 1.5f, 0.25f, ~((1 << 8) | (1 << 9) | (1 << 4)), QueryTriggerInteraction.Ignore))
                { best = hit.point.y; top = hit.point; }
            }
        return best > 0.5f;
    }

    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var player = gm.Player;
        var rm = RoundManager.Instance;
        yield return Wait(1f);
        rm.SpawningPaused = true;
        rm.DebugEndRound();
        // every zone open: in a real game the player is never inside a closed zone (closed doors cut the NavMesh)
        foreach (var door in Object.FindObjectsByType<ZoneDoor>()) door.DebugOpen();
        yield return Wait(1f);
        Check(NavAgents.JumperId != 0, "this map has the Saltador NavMesh (the old men need it to climb)");

        var picks = new List<(string name, GameObject go)>();
        void Pick(string name, System.Func<GameObject, bool> f, bool highest = false)
        {
            var all = Object.FindObjectsByType<Transform>().Where(t => f(t.gameObject)).Select(t => t.gameObject).ToList();
            GameObject chosen = null;
            float bestY = -1f;
            foreach (var go in all)
                if (TopOf(go, out var tp) && (!highest ? chosen == null : tp.y > bestY)) { chosen = go; bestY = tp.y; }
            if (chosen != null) picks.Add((name, chosen)); else Write("   (no " + name + " on this map)");
        }
        Pick("bus del Imserso (rampa)", g => g.name.StartsWith("IZ_Autobus"), true);
        Pick("pila de cajas", g => g.name.StartsWith("Prop_Crate"), true);
        Pick("coche", g => g.name.StartsWith("IZ_Coche"));
        Pick("avioneta", g => g.name.StartsWith("IZ_Avioneta"));
        Pick("caseta", g => g.name.StartsWith("IZ_Caseta"), true);
        Pick("máquina de bebidas", g => g.name.StartsWith("Drink_"));
        Pick("tractor", g => g.name == "Tractor" || g.name.StartsWith("VJ_Tractor"), true);
        Pick("puesto", g => g.name.StartsWith("IZ_Puesto"), true);

        var cc = player.GetComponent<CharacterController>();
        int hits = 0;
        System.Action<int> onHit = _ => hits++;
        player.Damaged += onHit;
        foreach (var (name, go) in picks)
        {
            if (!TopOf(go, out var top)) continue;
            // the player on top
            cc.enabled = false;
            player.transform.position = top + Vector3.up * 0.05f;
            cc.enabled = true;
            yield return Wait(0.6f);
            bool perched = EnemyAI.Perched(player.transform.position);
            // one old man on the ground 6-10 m away
            EnemyAI ai = null;
            // the floor under the prop (the Saltador NavMesh runs under props): spawn on that floor, not on a rock top
            float floorY = UnityEngine.AI.NavMesh.SamplePosition(top + Vector3.down * 1.5f, out var fh, 3f, NavAgents.Filter(NavAgents.JumperId)) ? fh.position.y : 0f;
            foreach (var d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left, new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, -1).normalized })
            {
                var at = top + d * 7f;
                if (NavAgents.Sample(at, 3f, out var g) && Mathf.Abs(g.y - floorY) < 0.4f && (Flat(g) - Flat(top)).magnitude > 4f)
                {
                    ai = rm.SpawnEnemy(null, g);
                    if (ai != null) break;
                }
            }
            if (ai == null) { Check(false, $"{name}: no place to spawn an old man near it"); continue; }
            var hp = ai.GetComponent<EnemyHealth>();
            hp.Init(100000f);
            int before = hits;
            float t0 = Time.time;
            bool climbed = false;
            float nextLog = t0 + 5f;
            var agent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
            while (hits == before && Time.time - t0 < 30f && player.CurrentHealth > 0)
            {
                if (ai.Climbing) climbed = true;
                if (player.CurrentHealth < 60) Heal(player);
                if (Time.time > nextLog)
                {
                    nextLog += 5f;
                    var d = player.transform.position - ai.transform.position;
                    Write($"     t {Time.time - t0:0} s: old man at {ai.transform.position:F1} offset {agent.baseOffset:0.00} climbing {ai.Climbing} path {agent.pathStatus} dest {agent.destination:F1} flat {new Vector2(d.x, d.z).magnitude:0.00} dy {d.y:0.00} attacking {ai.IsAttacking}; player at {player.transform.position:F1}");
                }
                // the player stays up there (a hit can push him)
                yield return null;
            }
            float took = Time.time - t0;
            Check(hits > before, $"{name}: player {top.y:0.0} m up (perched {perched}), the old man got to him and hit him in {took:0.0} s ({(climbed ? "climbing" : "walking")})");
            hp.TakeDamage(hp.Current + 1f, false, true);
            yield return Wait(0.8f);
            Heal(player);
        }
        player.Damaged -= onHit;
        // once nobody is up there, a climber goes back to the normal NavMesh
        cc.enabled = false;
        if (NavAgents.Sample(player.transform.position + Vector3.right * 3f, 6f, out var floor)) player.transform.position = floor;
        cc.enabled = true;
        var last = rm.SpawnEnemy(null, player.transform.position + Vector3.forward * 6f);
        if (last != null)
        {
            last.GetComponent<EnemyHealth>().Init(100000f);
            yield return Wait(4f);
            Check(!last.Climbing, "an old man chasing a player on the ground does not climb");
        }
        Write("done");
    }

    static void Heal(PlayerHealth p) =>
        typeof(PlayerHealth).GetField("current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(p, (float)p.MaxHealth);

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
