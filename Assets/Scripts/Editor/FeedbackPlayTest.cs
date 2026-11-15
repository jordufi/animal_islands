// Play test of the 2026-10-08 feedback pass (Tools > Island > Play Test Feedback), Isla del Faro:
// FPS counter, Máquina de Granizados first in Parking / Volley, Lentos-peta wall buy (500, shells one by
// one, a shot stops the reload, ammo 250), headshot kill = 65, night rounds capped at 5 alive, tree trunk
// colliders on the visible trunk, spawn entrance colour, "Anselmo cagón" (half the old men take shortcuts
// through the entrances, one old man per entrance at a time). Results in EditorPrefs
// "FeedbackPlayTest.Log" / ".Status", screenshots Renders/Feedback_*.png.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class FeedbackPlayTest
{
    const string Key = "FeedbackPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static Gamepad pad;
    static readonly List<(int amount, PointSource source)> deltas = new List<(int, PointSource)>();
    static readonly List<(EnemyAI ai, SpawnEntrance into, SpawnEntrance outOf, float time)> tunnels = new List<(EnemyAI, SpawnEntrance, SpawnEntrance, float)>();
    static bool showFpsBefore, cagonBefore;

    [MenuItem("Tools/Island/Play Test Feedback")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(TestMapSetup.IslaZombieScenePath, OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        showFpsBefore = GameSettings.ShowFps;
        cagonBefore = GameSettings.AnselmoCagon;
        GameSettings.AnselmoCagon = false;   // the test switches the mode on itself
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
        EnemyAI.Tunnelled -= OnTunnelled;
        GameSettings.ShowFps = showFpsBefore;
        GameSettings.AnselmoCagon = cagonBefore;
        if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; }
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

    static void OnTunnelled(EnemyAI ai, SpawnEntrance a, SpawnEntrance b) => tunnels.Add((ai, a, b, Time.time));

    // a door still closed (opened doors are removed)
    static void OpenDoor(int n)
    {
        var d = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(x => x.name.StartsWith("Door" + n + "_"));
        if (d != null) d.DebugOpen();
    }

    // ------------------------------------------------------------------ the test
    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var rm = RoundManager.Instance;
        var player = gm.Player;
        var rig = gm.CameraRig;
        var cam = rig.GetComponent<Camera>();
        var interactor = player.GetComponent<PlayerInteractor>();
        var shooter = player.GetComponent<PlayerShooter>();
        rm.SpawningPaused = true;
        player.GodMode = true;
        deltas.Clear(); tunnels.Clear();
        SpawnEntrance.Overlaps = 0;
        gm.PointsDelta += (a, s) => deltas.Add((a, s));
        EnemyAI.Tunnelled -= OnTunnelled;
        EnemyAI.Tunnelled += OnTunnelled;
        pad = InputSystem.AddDevice<Gamepad>("FeedbackPad");
        Write($"Player {gm.Animal.id}, mode at start: {(rm.TunnelMode ? "Anselmo cagón" : "clásico")}");
        yield return null;

        // ---------------------------------------------------------- 1. FPS counter
        GameSettings.ShowFps = true;
        yield return Wait(1.2f);
        var fps = FpsCounter.Instance;
        var fpsText = fps != null ? fps.GetComponentInChildren<UnityEngine.UI.Text>(false) : null;
        Check(fps != null && fpsText != null && fpsText.text.Contains("FPS"), $"FPS counter on screen: '{(fpsText != null ? fpsText.text : "-")}'");
        GameSettings.ShowFps = false;
        yield return Wait(0.6f);
        Check(fps != null && fps.GetComponentInChildren<UnityEngine.UI.Text>(false) == null, "FPS counter hidden when the setting is off");

        // ---------------------------------------------------------- 2. Máquina de Granizados: first in Parking / Volley
        var box = Object.FindAnyObjectByType<MysteryBox>();
        Check(box.WaitingForFirstZone && !box.IsAvailable, "box not on the map at the start (Chiringuito)");
        OpenDoor(1);   // Camping
        yield return Wait(0.3f);
        Check(box.WaitingForFirstZone && !box.IsAvailable, "box still hidden after opening the Camping");
        OpenDoor(4);   // Templo + Lago: not there either
        yield return Wait(0.3f);
        Check(box.WaitingForFirstZone && !box.IsAvailable, "box still hidden after opening the Templo (it must not appear there)");
        OpenDoor(2);   // Parking (+ Cueva + Faro)
        yield return Wait(0.3f);
        var boxZone = box.CurrentSpot != null ? box.CurrentSpot.GetComponentInParent<Zone>() : null;
        Check(box.IsAvailable && boxZone != null && boxZone.displayName == "Parking", $"box appears in the Parking when it opens (zone {(boxZone != null ? boxZone.displayName : "-")}, prompt '{box.Prompt(interactor)}')");

        // ---------------------------------------------------------- 3. Lentos-peta on the Camping wall
        var wall = Object.FindObjectsByType<WallWeapon>().FirstOrDefault(w => w.Weapon.id == "lentospeta");
        var wallZone = wall != null ? ZoneOf(wall.transform.position) : null;
        Check(wall != null && wall.Weapon.displayName == "Lentos-peta" && wall.Cost == 500, $"wall weapon: {(wall != null ? wall.Weapon.displayName + " " + wall.Cost : "missing")} in {(wallZone != null ? wallZone.displayName : "?")}");
        Teleport(player, wall.transform.position + wall.transform.forward * 1.2f, wall.transform.eulerAngles.y + 180f);
        yield return Wait(0.5f);
        gm.AddPoints(2000, PointSource.Debug);
        int before = gm.Points;
        Write($"   prompt: '{wall.Prompt(interactor)}', focus: {(interactor.Focus != null ? interactor.Focus.name : "none")}");
        wall.Interact(interactor);
        var slot = shooter.SlotOf(wall.Weapon);
        Check(slot != null && before - gm.Points == 500 && shooter.Current == slot, $"bought the Lentos-peta for {before - gm.Points} (in hand: {shooter.Current.Name})");
        rig.SetTarget(player.transform);
        yield return Wait(0.6f);
        Shot(cam, "Renders/Feedback_lentospeta.png");
        // shells one by one: empty the gun and time the reload
        int shells = 0;
        System.Action onShell = () => shells++;
        shooter.ShellLoaded += onShell;
        slot.magazine = 0;
        shooter.Reload();
        float r0 = Time.time;
        yield return Wait(2.1f);   // 0.5 start + 0.75 per shell -> 2 shells
        Check(shells == 2 && slot.magazine == 2 && shooter.IsReloading, $"reload goes shell by shell: {shells} shells after {Time.time - r0:F1} s (mag {slot.magazine}/5), still reloading");
        // a shot stops the reload
        Pad(Vector2.zero, GamepadButton.RightTrigger);
        yield return Wait(0.15f);
        Pad(Vector2.zero);
        yield return Wait(0.2f);
        Check(!shooter.IsReloading && slot.magazine == 1, $"R2 during the reload fires and stops it (mag {slot.magazine}, reloading {shooter.IsReloading})");
        shooter.Reload();
        yield return Wait(0.5f + 0.75f * 4 + 0.3f);
        Check(slot.magazine == 5, $"full reload: mag {slot.magazine}/5, reserve {slot.reserve}");
        string ammoPrompt = wall.Prompt(interactor);
        before = gm.Points;
        wall.Interact(interactor);
        Check(ammoPrompt.Contains("Munición") && before - gm.Points == 250 && shooter.IsFull(slot), $"ammo from the wall: '{ammoPrompt}' paid {before - gm.Points}");
        shooter.ShellLoaded -= onShell;

        // ---------------------------------------------------------- 4. headshot kill = 65
        var pistol = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/Pistol.asset");
        Teleport(player, new Vector3(36f, 0f, 3f), 90f);
        rig.SetTarget(player.transform);
        shooter.GiveWeapon(pistol);
        yield return Wait(0.4f);
        var victim = SpawnTestEnemy(rm, player.transform.position + player.transform.forward * 4f, 1f);
        foreach (var hz in victim.GetComponentsInChildren<HitZone>())   // the head covers the whole old man: any hit is a headshot
        {
            if (!hz.isHead) continue;
            var col = hz.GetComponent<Collider>();
            if (col is SphereCollider sc) sc.radius *= 12f;
            else if (col is BoxCollider bc) bc.size *= 12f;
            else if (col is CapsuleCollider cc2) { cc2.radius *= 12f; cc2.height *= 12f; }
        }
        yield return Wait(0.3f);
        deltas.Clear();
        Pad(Vector2.zero, GamepadButton.RightTrigger);
        yield return Wait(0.1f);
        Pad(Vector2.zero);
        yield return Wait(0.3f);
        var hs = deltas.Where(d => d.source == PointSource.Headshot).ToList();
        Check(victim.IsDead && hs.Count == 1 && hs[0].amount == gm.Economy.headshotKillPoints && gm.Economy.headshotKillPoints > gm.Economy.killPoints, $"headshot kill pays {(hs.Count > 0 ? hs[0].amount : 0)} (economy: head {gm.Economy.headshotKillPoints}, body {gm.Economy.killPoints})");
        if (!victim.IsDead) victim.TakeDamage(victim.Current + 1f, false);

        // ---------------------------------------------------------- 5. tree trunks: collision on the visible trunk
        int trunksOk = 0, trees = 0;
        string worst = "";
        foreach (var mf in Object.FindObjectsByType<MeshFilter>().Where(m => m.name.StartsWith("Tree_")))
        {
            trees++;
            var bark = BarkPoints(mf);
            float minY = bark.Min(v => v.y);
            bool ok = true;
            foreach (float h in new[] { 0.3f, 1.0f, 1.7f })
            {
                var sl = bark.Where(v => Mathf.Abs(v.y - (minY + h)) < 0.2f).ToList();
                if (sl.Count == 0) continue;
                var c = sl.Aggregate(Vector3.zero, (a, b) => a + b) / sl.Count;
                bool hit = Physics.OverlapSphere(c, 0.12f, ~0, QueryTriggerInteraction.Ignore).Any(col => col.name == "Tronco");
                if (!hit) { ok = false; worst = $"{mf.name} at {c} (h {h})"; }
            }
            if (ok) trunksOk++;
        }
        Check(trees > 0 && trunksOk == trees, $"tree trunks: the visible trunk is solid at 0.3 / 1.0 / 1.7 m on {trunksOk}/{trees} trees {worst}");
        var palm = Object.FindObjectsByType<MeshFilter>().Where(m => m.name.StartsWith("Tree_Palm")).OrderBy(m => (m.transform.position - new Vector3(-30, 0, 4)).sqrMagnitude).FirstOrDefault();
        if (palm != null)
        {
            var bark = BarkPoints(palm);
            float minY = bark.Min(v => v.y);
            var basePt = bark.Where(v => v.y < minY + 0.3f).Aggregate(Vector3.zero, (a, b) => a + b) / bark.Count(v => v.y < minY + 0.3f);
            // walk the player straight into the trunk: it must stop right at the bark, not 1 m before or inside it
            Teleport(player, basePt + Vector3.left * 3f, 90f);
            yield return Wait(0.2f);
            var cc = player.GetComponent<CharacterController>();
            for (int i = 0; i < 60; i++) { cc.Move(Vector3.right * 0.08f + Vector3.down * 0.2f); }
            float gap = new Vector2(player.transform.position.x - basePt.x, player.transform.position.z - basePt.z).magnitude;
            Check(gap > 0.3f && gap < 1.0f, $"walking into a palm stops {gap:F2} m from the trunk centre (player radius {cc.radius:F2})");
            ColliderViewer.Toggle();
            rig.SetTarget(player.transform);
            yield return Wait(0.5f);
            Shot(cam, "Renders/Feedback_palm_colliders.png");
            ColliderViewer.Toggle();
        }

        // ---------------------------------------------------------- 6. spawn entrances: one colour for all
        var paints = new HashSet<string>();
        foreach (var e in SpawnEntrance.All)
            foreach (var r in e.GetComponentsInChildren<Renderer>())
                if (r.name == "Valla" || r.name == "Puerta") paints.Add(ColorUtility.ToHtmlStringRGB(r.sharedMaterial.color));
        var cabins = Object.FindObjectsByType<Transform>().Where(t => t.name.StartsWith("Cabina_") && t.parent != null && t.parent.name == "Cabinas").ToList();
        var cabinMats = cabins.SelectMany(c => c.GetComponentsInChildren<Renderer>().Where(r => r.name == "Pared")).Select(r => r.sharedMaterial.name).Distinct().ToList();
        Check(cabinMats.Count == 1, $"spawn cabins share one material: {string.Join(", ", cabinMats)} ({cabins.Count} cabins)");
        var doorCloths = Object.FindObjectsByType<ZoneDoor>().SelectMany(d => d.GetComponentsInChildren<Renderer>()).Select(r => ColorUtility.ToHtmlStringRGB(r.sharedMaterial.color)).Distinct().ToList();
        Write($"   entrance fence / door colours: {string.Join(", ", paints)}; zone door colours: {string.Join(", ", doorCloths)}");
        Check(paints.Count <= 2 && !paints.Intersect(doorCloths).Any(), "every spawn entrance uses the spawn colour, never a zone-door colour");
        var wc = SpawnEntrance.All.FirstOrDefault(e => e.name.Contains("WC"));
        if (wc != null)
        {
            Teleport(player, wc.ExitPoint + wc.transform.forward * 3f, wc.transform.eulerAngles.y + 180f);
            rig.SetTarget(player.transform);
            yield return Wait(0.6f);
            Shot(cam, "Renders/Feedback_spawn_wc.png");
        }

        // ---------------------------------------------------------- 7. night round: at most 5 alive at once
        rm.ForceNextNight = true;   // the next round (spawning was paused until now) is a night round
        Teleport(player, new Vector3(-30f, 0f, 4f), 90f);
        rm.SpawningPaused = false;
        if (!rm.InIntermission) rm.DebugEndRound();
        float waitNight = Time.time;
        while (!rm.IsNight && Time.time - waitNight < 30f) yield return null;
        rm.DebugAddToQueue(8);   // a round-1 night has only 5 old men: queue more so the cap matters
        Write($"   night round {rm.Round} started: {rm.IsNight}, {rm.RemainingInRound} in the queue");
        int maxAlive = 0, spawnedNight = 0;
        System.Action<EnemyAI, Transform> countSpawn = (ai, t) => spawnedNight++;
        rm.EnemySpawned += countSpawn;
        float n0 = Time.time;
        float nextKill = n0 + 14f;
        while (Time.time - n0 < 45f && rm.IsNight)
        {
            var aliveNow = Object.FindObjectsByType<EnemyAI>().Where(e => !e.GetComponent<EnemyHealth>().IsDead).ToList();
            maxAlive = Mathf.Max(maxAlive, aliveNow.Count);
            if (Time.time >= nextKill && aliveNow.Count > 0)   // the player kills one now and then: the next one comes
            {
                aliveNow[0].GetComponent<EnemyHealth>().TakeDamage(1e6f, false);
                nextKill = Time.time + 4f;
            }
            yield return null;
        }
        rm.EnemySpawned -= countSpawn;
        Check(rm.MaxAliveNow <= 5 || !rm.IsNight, $"night round {rm.Round}: limit {rm.MaxAliveNow}");
        Check(maxAlive <= 5 && maxAlive >= 4 && spawnedNight > 5, $"night round: at most {maxAlive} senderistas alive at once (limit 5), {spawnedNight} spawned, {rm.RemainingInRound} left");
        rm.SpawningPaused = true;
        rm.DebugEndRound();
        yield return Wait(0.5f);

        // ---------------------------------------------------------- 8. classic mode: nobody tunnels
        var classic = new List<EnemyAI>();
        for (int i = 0; i < 6; i++) classic.Add(rm.SpawnEnemy(null, new Vector3(-30f, 0f, 4f)));
        Check(classic.All(a => a != null && !a.Tunneler), "classic mode: no old man is a tunneler");
        foreach (var a in classic) if (a != null) a.GetComponent<EnemyHealth>().TakeDamage(1e6f, false);

        // ---------------------------------------------------------- 9. Anselmo cagón
        rm.TunnelMode = true;
        var mixed = new List<EnemyAI>();
        for (int i = 0; i < 40; i++) mixed.Add(rm.SpawnEnemy(null, new Vector3(-30f, 0f, 4f)));
        int through = mixed.Count(a => a != null && a.Tunneler);
        Check(through >= 10 && through <= 30, $"Anselmo cagón: {through}/40 old men got spawn_through = true (chance {rm.Tunnels.chance:P0})");
        foreach (var a in mixed) if (a != null) a.GetComponent<EnemyHealth>().TakeDamage(1e6f, false);
        yield return Wait(0.5f);

        for (int d = 3; d <= 6; d++) OpenDoor(d);   // everything but the Muelle (boss)
        yield return Wait(0.5f);
        // the player runs off to the Mercadillo; old men are left behind on the Chiringuito beach
        Teleport(player, new Vector3(10f, 0f, -28f), 0f);
        rig.SetTarget(player.transform);
        var group = new List<EnemyAI>();
        for (int i = 0; i < 8; i++)
        {
            var ai = rm.SpawnEnemy(null, new Vector3(-30f + (i % 4) * 1.5f, 0f, 2f + (i / 4) * 2f));
            ai.EnableTunnels(rm.Tunnels, i < 6);   // 6 tunnelers, 2 walkers
            group.Add(ai);
        }
        var start = group.ToDictionary(a => a, a => PathLen(a.transform.position, player.transform.position));
        float g0 = Time.time;
        var arrival = new Dictionary<EnemyAI, float>();
        bool shotTaken = false;
        int maxInside = 0;
        while (Time.time - g0 < 45f && arrival.Count < group.Count)
        {
            foreach (var a in group)
            {
                if (a == null || arrival.ContainsKey(a)) continue;
                Vector3 to = a.transform.position - player.transform.position; to.y = 0f;
                if (!a.InTunnel && !a.IsEmerging && to.magnitude < 4f) arrival[a] = Time.time - g0;
            }
            maxInside = Mathf.Max(maxInside, group.Count(a => a != null && a.InTunnel));
            if (!shotTaken && tunnels.Count > 0 && Time.time - tunnels[0].time > 0.6f)
            {
                shotTaken = true;
                var outE = tunnels[0].outOf;
                Teleport(player, outE.ExitPoint + outE.transform.forward * 3f, outE.transform.eulerAngles.y + 180f);
                rig.SetTarget(player.transform);
                yield return Wait(0.3f);
                Shot(cam, "Renders/Feedback_cagon_exit.png");
                Teleport(player, new Vector3(10f, 0f, -28f), 0f);
            }
            yield return null;
        }
        var tunnelers = group.Where(a => a.Tunneler).ToList();
        var walkers = group.Where(a => !a.Tunneler).ToList();
        foreach (var x in tunnels)
            Write($"   {x.ai.name} went in at {x.into.name} ({ZoneName(x.into)}) and came out of {x.outOf.name} ({ZoneName(x.outOf)}) at t={x.time - g0:F1}s");
        string Times(IEnumerable<EnemyAI> l) => string.Join(" ", l.Select(a => arrival.TryGetValue(a, out var t) ? t.ToString("F0") + "s" : "-"));
        Write($"   start walking distance {start.Values.Average():F0} m; arrival near the player: tunnelers {Times(tunnelers)} | walkers {Times(walkers)}");
        Check(tunnels.Count > 0 && tunnels.All(x => x.ai.Tunneler), $"tunnelers took {tunnels.Count} shortcuts, walkers none");
        Check(tunnels.All(x => x.into.Zone != x.outOf.Zone), "every shortcut goes from one zone to another");
        float tunnelAvg = tunnelers.Where(arrival.ContainsKey).Select(a => arrival[a]).DefaultIfEmpty(99f).Average();
        float walkAvg = walkers.Where(arrival.ContainsKey).Select(a => arrival[a]).DefaultIfEmpty(99f).Average();
        Check(tunnelAvg < walkAvg, $"tunnelers reach the player sooner ({tunnelAvg:F0} s) than walkers ({walkAvg:F0} s)");
        Check(SpawnEntrance.Overlaps == 0, $"one old man at a time per entrance ({SpawnEntrance.Overlaps} overlaps; up to {maxInside} in the pipes at once)");
        foreach (var a in group) if (a != null) a.GetComponent<EnemyHealth>().TakeDamage(1e6f, false);

        // ---------------------------------------------------------- 10. stress: many tunnelers at once (queueing)
        SpawnEntrance.Overlaps = 0;
        tunnels.Clear();
        var crowd = new List<EnemyAI>();
        for (int i = 0; i < 14; i++)
        {
            var ai = rm.SpawnEnemy(null, new Vector3(-30f + (i % 4), 0f, 0f + (i / 4) * 1.5f));
            ai.EnableTunnels(rm.Tunnels, true);
            crowd.Add(ai);
        }
        float s0 = Time.time;
        while (Time.time - s0 < 25f) yield return null;
        foreach (var a in crowd.Where(a => a != null && a.TunnelsTaken == 0))
        {
            var tt = a.TunnelTarget;
            Vector3 to = tt != null ? tt.ExitPoint - a.transform.position : Vector3.zero; to.y = 0f;
            var ag = a.GetComponent<NavMeshAgent>();
            Write($"   no shortcut yet: {a.name} at {a.transform.position} target {(tt != null ? tt.name + (tt.Busy ? " (busy)" : "") + $" {to.magnitude:F1} m" : "none")}, inTunnel {a.InTunnel}, emerging {a.IsEmerging}, path {PathLen(a.transform.position, player.transform.position):F0} m, agent {(ag.enabled ? ag.pathStatus + " stopped " + ag.isStopped : "off")}");
        }
        var perEntrance = tunnels.GroupBy(x => x.into).Select(g => g.Count()).DefaultIfEmpty(0).Max();
        Check(SpawnEntrance.Overlaps == 0 && tunnels.Count >= 3, $"14 tunnelers at once: {tunnels.Count} shortcuts, up to {perEntrance} through the same entrance, {SpawnEntrance.Overlaps} overlaps");
        foreach (var a in crowd) if (a != null) a.GetComponent<EnemyHealth>().TakeDamage(1e6f, false);
        rm.TunnelMode = false;
        Write("errors in the console are checked by the caller");
    }

    // ------------------------------------------------------------------ helpers
    static string ZoneName(SpawnEntrance e) => e.Zone != null ? e.Zone.displayName : "?";

    static Zone ZoneOf(Vector3 p)
    {
        Zone best = null;
        float bd = float.MaxValue;
        foreach (var z in ZoneManager.Instance.Zones)
            foreach (var t in z.SpawnPoints.Concat(z.BoxSpots))
            {
                float d = (t.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = z; }
            }
        return best;
    }

    static float PathLen(Vector3 a, Vector3 b)
    {
        var path = new NavMeshPath();
        if (!NavAgents.Sample(a, 2f, out var pa) || !NavAgents.Sample(b, 2f, out var pb) || !NavMesh.CalculatePath(pa, pb, NavAgents.Filter(0), path)) return -1f;
        float len = 0f;
        for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return len;
    }

    static List<Vector3> BarkPoints(MeshFilter mf)
    {
        var mesh = mf.sharedMesh;
        var mr = mf.GetComponent<MeshRenderer>();
        int sub = -1;
        for (int i = 0; i < mr.sharedMaterials.Length; i++) if (mr.sharedMaterials[i] != null && mr.sharedMaterials[i].name.Contains("Bark")) sub = i;
        var verts = mesh.vertices;
        return (sub >= 0 ? mesh.GetTriangles(sub).Distinct() : Enumerable.Range(0, verts.Length)).Select(i => mf.transform.TransformPoint(verts[i])).ToList();
    }

    static void Pad(Vector2 stick, params GamepadButton[] buttons)
    {
        var state = new GamepadState { leftStick = stick };
        foreach (var b in buttons)
        {
            if (b == GamepadButton.LeftTrigger) state.leftTrigger = 1f;
            else if (b == GamepadButton.RightTrigger) state.rightTrigger = 1f;
            else state = state.WithButton(b);
        }
        InputSystem.QueueStateEvent(pad, state);
    }

    static EnemyHealth SpawnTestEnemy(RoundManager rm, Vector3 at, float health)
    {
        var ai = rm.SpawnEnemy(null, at);
        var agent = ai.GetComponent<NavMeshAgent>();
        if (NavAgents.Sample(at, 2f, out var p)) agent.Warp(p);
        ai.Stun(30f);
        var h = ai.GetComponent<EnemyHealth>();
        if (health > 0f) h.Init(health);
        return h;
    }

    static void Teleport(PlayerHealth player, Vector3 pos, float yaw)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        if (NavAgents.Sample(pos, 4f, out var h)) pos = h;
        player.transform.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.Euler(0, yaw, 0));
        cc.enabled = true;
    }

    static void Shot(Camera cam, string path)
    {
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
