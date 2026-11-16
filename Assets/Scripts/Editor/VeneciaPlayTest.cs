// Tools > Island > Play Test La Serenisima: the rules that only exist on the Venice map, in one play session.
//   canal: a player over the water is put back on land (with damage) and never left in the canal
//   closed barrio: standing in a zone that is not open sends you back; once its bridge is open you stay
//   carnival mask: the hits only chip the mask (no headshot, no kill), then he dies like anyone
//   acqua alta: the forced flood round raises the water, wading is slower, high ground is not, a Nevera at the end
//   gondola: pays and carries the player to the other stop (only between open barrios)
// Results in EditorPrefs "VeneciaPlayTest.Log" / ".Status", screenshots Renders/VeneciaTest_*.png.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class VeneciaPlayTest
{
    const string Key = "VeneciaPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static int step, errors, rescuedWater, rescuedZone, hp0, pickups0;
    static float stepTime, realTime;
    static EnemyHealth masked;
    static GondolaStop stop;
    static int climbed, thrown;
    static float endRoundAt;
    static Vector3 before;
    static EnemyAI swimmer;
    static int hullClips, hullSamples;
    static bool dryCooler;
    static float swimStartDist;

    static VeneciaPlayTest()
    {
        if (EditorPrefs.GetBool(Key + "Running")) Hook();
    }

    static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    [MenuItem("Tools/Island/Play Test La Serenisima")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(IslaZombieSetup.Venecia.ScenePath, OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        step = 0; errors = 0; rescuedWater = 0; rescuedZone = 0;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        CanalRescue.Rescued -= OnRescued;
        CanalRescue.Rescued += OnRescued;
        climbed = thrown = 0;
        TowerClimb.Climbed -= OnClimbed; TowerClimb.Climbed += OnClimbed;
        TowerClimb.ThrownOff -= OnThrown; TowerClimb.ThrownOff += OnThrown;
        Hook();
        EditorApplication.EnterPlaymode();
    }

    static void OnClimbed(PlayerHealth p) => climbed++;
    static void OnThrown(PlayerHealth p) => thrown++;
    static void OnRescued(PlayerHealth p, bool water) { if (water) rescuedWater++; else rescuedZone++; }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; if (errors <= 5) Write("   console: " + msg); }
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");
    static void Check(bool pass, string text) => Write((pass ? "OK   " : "FAIL ") + text);

    static void Finish(string status)
    {
        Application.logMessageReceived -= OnLog;
        CanalRescue.Rescued -= OnRescued;
        TowerClimb.Climbed -= OnClimbed; TowerClimb.ThrownOff -= OnThrown;
        EditorPrefs.SetString(Key + "Status", status);
        EditorPrefs.SetBool(Key + "Running", false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void Next() { step++; stepTime = Time.time; realTime = Time.realtimeSinceStartup; }

    static void Tick()
    {
        if (!EditorPrefs.GetBool(Key + "Running")) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        try { Step(); }
        catch (System.Exception e) { Write("EXCEPTION: " + e); Finish("failed"); }
    }

    // layout cell -> world (La Serenisima: origin cell 34, 24)
    static Vector3 Cell(float c, float r, float y) => new Vector3((c - 34f) * 2f, y, (24f - r) * 2f);

    static void Teleport(PlayerHealth p, Vector3 pos)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.position = pos;
        cc.enabled = true;
    }

    static void Walk(PlayerHealth player, string label, Vector3 a, Vector3 b)
    {
        if (!NavMesh.SamplePosition(a, out var ha, 4f, NavAgents.Filter(0)) || !NavMesh.SamplePosition(b, out var hb, 4f, NavAgents.Filter(0)))
        { Write($"FAIL walk {label}: endpoints not on the NavMesh"); return; }
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(ha.position, hb.position, NavAgents.Filter(0), path) || path.status != NavMeshPathStatus.PathComplete)
        { Write($"FAIL walk {label}: no NavMesh path ({path.status})"); return; }
        Teleport(player, ha.position + Vector3.up * 0.1f);
        var cc = player.GetComponent<CharacterController>();
        float walked = 0f, stuck = 0f;
        foreach (var corner in path.corners.Skip(1))
            for (int i = 0; i < 3000; i++)
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
        Write($"{(miss < 1.5f ? "OK  " : "FAIL")} walk {label}: {walked:F0} m, ended {miss:F1} m from target");
    }

    static void Shot(string name, Vector3 pos, Vector3 look)
    {
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.LookAt(look);
        cam.fieldOfView = 60f;
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        Directory.CreateDirectory("Renders");
        File.WriteAllBytes("Renders/VeneciaTest_" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(go);
    }

    static void Step()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        if (gm.State == GameState.GameOver) { Write($"FAIL player died at step {step}"); Finish("failed"); return; }
        var cards = CardManager.Instance;
        if (cards != null && cards.CurrentOffer != null) { cards.Choose(cards.CurrentOffer[0]); return; }
        var player = gm.Player;
        var rm = RoundManager.Instance;
        var tide = AcquaAlta.Instance;
        var rescue = CanalRescue.Instance;
        float rt = Time.realtimeSinceStartup - realTime;

        switch (step)
        {
            case 0:
                if (rescue == null || tide == null) { Write("FAIL the scene has no CanalRescue / AcquaAlta"); Finish("failed"); return; }
                if (rm.Round < 1) return;
                Check(!rm.IsNightRound(5), "no night rounds on this map (round 5 is not a night)");
                Next();
                break;

            case 1:   // stand still a moment so the last safe cell is recorded, then jump into the Canal Grande
                if (rt < 1.0f) return;
                before = player.transform.position;
                hp0 = player.CurrentHealth;
                Teleport(player, Cell(29f, 35f, 0.5f));
                Next();
                break;

            case 2:
                if (rescuedWater == 0) { if (rt > 6f) { Write("FAIL fell into the canal and nobody fished him out (y " + player.transform.position.y.ToString("F1") + ")"); Finish("failed"); } return; }
                if (rt < 1.5f) return;
                Check(rescuedWater == 1 && Vector3.Distance(player.transform.position, before) < 3f && player.transform.position.y > -0.2f,
                    $"canal: back where he stood ({Vector3.Distance(player.transform.position, before):F1} m away, y {player.transform.position.y:F1})");
                Check(hp0 - player.CurrentHealth > 0, $"canal: the bath cost {hp0 - player.CurrentHealth} health");
                player.GodMode = true;
                Next();
                break;

            case 3:   // closed barrio: put him in the Mercato (door 1 still closed)
                Teleport(player, Cell(12f, 25.5f, 0.3f));
                Next();
                break;

            case 4:
                if (rescuedZone == 0) { if (rt > 5f) { Write("FAIL stood in a closed barrio and stayed there"); Finish("failed"); } return; }
                if (rt < 1.5f) return;
                Check(rescue.ZoneAt(player.transform.position) != null && rescue.ZoneAt(player.transform.position).IsOpen, "closed barrio: sent back to an open zone (" + rescue.ZoneAt(player.transform.position).displayName + ")");
            {
                var fences = Object.FindObjectsByType<ZoneFence>(FindObjectsInactive.Include);
                var merc = fences.FirstOrDefault(f => f.Zone != null && f.Zone.displayName.StartsWith("Mercato"));
                Check(fences.Length >= 6 && merc != null && merc.Up, $"closed barrios have their fence up ({fences.Length} fences, Mercato {(merc != null && merc.Up ? "up" : "missing")})");
                // a Frog super jump from the Ferrovia quay towards the Mercato: 4 m up, the fence collider is 6 m
                var from = Cell(9f, 33f, 4.2f);
                bool blocked = Physics.Raycast(from, Vector3.forward, out var fh, 6f, 1 << 11, QueryTriggerInteraction.Ignore) && fh.collider.transform.parent != null && fh.collider.transform.parent.name == "Tramos";
                Check(blocked || Physics.Raycast(Cell(5f, 33f, 4.2f), Vector3.forward, 6f, 1 << 11), "the fence stops a 4 m jump into the closed Mercato");
                Shot("valla_mercato", Cell(14f, 34.5f, 1.7f), Cell(14f, 30.5f, 1.5f));
            }
                Next();
                break;

            case 5:   // open the bridge and try again: now he stays
            {
                var door = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.name.StartsWith("Door1_"));
                gm.AddPoints(100000);
                door.Interact(player.GetComponent<PlayerInteractor>());
                Next();
                break;
            }

            case 6:
                if (rt < 0.6f) return;
                rescuedZone = 0;
                Teleport(player, Cell(12f, 25.5f, 0.3f));
                Next();
                break;

            case 7:
                if (rt < 2f) return;
                Check(rescuedZone == 0 && rescue.ZoneAt(player.transform.position).displayName.StartsWith("Mercato"), "bridge open: he stays in the Mercato");
            {
                var merc = Object.FindObjectsByType<ZoneFence>(FindObjectsInactive.Include).FirstOrDefault(f => f.Zone != null && f.Zone.displayName.StartsWith("Mercato"));
                Check(merc != null && !merc.Up, "bridge 1 open: the Mercato fence is gone");
            }
                Next();
                break;

            case 8:   // carnival mask
            {
                var so = new SerializedObject(rm);
                var sv = so.FindProperty("specialVariants");
                EnemyVariant bauta = null;
                for (int i = 0; i < sv.arraySize; i++) { var v = sv.GetArrayElementAtIndex(i).objectReferenceValue as EnemyVariant; if (v != null && v.maskHits == 2) bauta = v; }
                if (bauta == null) { Write("FAIL no masked variant in this scene's RoundManager"); Finish("failed"); return; }
                var ai = rm.SpawnEnemy(bauta, player.transform.position + player.transform.forward * 3f);
                if (ai == null) { Write("FAIL could not spawn the masked old man"); Finish("failed"); return; }
                masked = ai.GetComponent<EnemyHealth>();
                Next();
                break;
            }

            case 9:
            {
                if (rt < 0.5f) return;
                bool hasMask = masked.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("VN_Mascara"));
                Check(masked.Masked && hasMask, $"masked old man: Masked {masked.Masked}, mask on his face {hasMask}, health {masked.Current:F0}/{masked.MaxHealth:F0}");
                Shot("mascara", masked.transform.position + masked.transform.forward * 2.2f + Vector3.up * 1.5f, masked.transform.position + Vector3.up * 1.4f);
                var a = EnemyHits.ApplyLocal(masked, 99999f, true, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                Check(!a.killed && !a.headshot && masked.Masked, $"hit 1 (a 99999 headshot): alive, no headshot, still masked ({masked.Current:F0})");
                var b = EnemyHits.ApplyLocal(masked, 99999f, true, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                Check(!b.killed && !masked.Masked, $"hit 2: mask broken, alive ({masked.Current:F0})");
                var c = EnemyHits.ApplyLocal(masked, 99999f, true, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                Check(c.killed && c.headshot, "hit 3: dead, headshot counts again");
                // the plague doctor leaves a cloud where he dies
                var so2 = new SerializedObject(rm);
                var sv2 = so2.FindProperty("specialVariants");
                EnemyVariant peste = null;
                for (int i = 0; i < sv2.arraySize; i++) { var v = sv2.GetArrayElementAtIndex(i).objectReferenceValue as EnemyVariant; if (v != null && v.deathCloudRadius > 0f) peste = v; }
                var doc = peste != null ? rm.SpawnEnemy(peste, player.transform.position + player.transform.forward * 6f) : null;
                if (doc == null) Write("FAIL no plague doctor variant with a death cloud in this scene");
                else
                {
                    int clouds = GasCloud.Alive;
                    doc.GetComponent<EnemyHealth>().TakeDamage(999999f, false, true);
                    Check(GasCloud.Alive == clouds + 1, $"plague doctor: 3-hit mask ({peste.maskHits}), dies and leaves a cloud of {peste.deathCloudRadius} m for {peste.deathCloudSeconds} s");
                }
                Next();
                break;
            }

            case 10:   // acqua alta: force it on the next round
                tide.ForceNext = true;
                rm.DebugEndRound();
                Next();
                break;

            case 11:
                if (!tide.Flooding) { if (rt > 40f) { Write("FAIL the forced acqua alta never started"); Finish("failed"); } return; }
                Check(true, $"acqua alta started in round {rm.Round}");
                Teleport(player, Cell(13f, 40f, 0.3f));   // the campo of Ferrovia, level 0
                pickups0 = Object.FindObjectsByType<AmmoPickup>().Length;
                Next();
                break;

            case 12:
            {
                if (rt < 9f) return;
                var pc = player.GetComponent<PlayerController>();
                Check(tide.WaterY > 0.3f, $"water rose to {tide.WaterY:F2} m over the low floor");
                Check(pc.ExternalSpeedFactor < 0.9f, $"wading on level 0: speed x{pc.ExternalSpeedFactor:F2}");
                Check(rescuedWater == 1, "wading is not falling into the canal (no rescue)");
                Shot("acqua_alta", Cell(11f, 42f, 3.5f), Cell(17f, 38f, 0.5f));
                Check(tide.SwimActive && NavAgents.SwimOpen, "acqua alta: the canals can be swum (floor on, old men get the Canal area)");
                // into the canal between Ferrovia and the (closed) Carnevale
                rescuedWater = 0;
                Teleport(player, Cell(29f, 34f, 0.3f));
                step = 100; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 100:
            {
                if (rt < 2f) return;
                var pc = player.GetComponent<PlayerController>();
                Check(rescuedWater == 0 && rescue.InWater(player.transform.position) && player.transform.position.y > tide.SwimY - 0.3f,
                    $"swimming in the canal: no rescue, y {player.transform.position.y:F2}, speed x{pc.ExternalSpeedFactor:F2}");
                Check(pc.ExternalSpeedFactor <= 0.55f, $"slower in the canal than wading (x{pc.ExternalSpeedFactor:F2})");
                Shot("nadando", Cell(24f, 36f, 2.6f), Cell(29.5f, 33.5f, 0f));
                swimmer = rm.SpawnEnemy(null, Cell(24f, 34f, 0f));
                if (swimmer != null) { var ag = swimmer.GetComponent<NavMeshAgent>(); if (ag != null && ag.isOnNavMesh) ag.Warp(Cell(24f, 34f, 0f)); }
                swimStartDist = swimmer != null ? Vector3.Distance(swimmer.transform.position, player.transform.position) : 0f;
                Next();
                break;
            }

            case 101:
            {
                if (swimmer == null) { Write("FAIL could not spawn an old man on the Ferrovia quay"); step = 103; break; }
                var ag = swimmer.GetComponent<NavMeshAgent>();
                float d = Vector3.Distance(swimmer.transform.position, player.transform.position);
                if (d > 2.2f && rt < 14f) return;
                Check(d <= 2.2f && rescue.InWater(swimmer.transform.position), $"an old man follows into the canal: {swimStartDist:F1} m -> {d:F1} m in {rt:F1} s (in water: {rescue.InWater(swimmer.transform.position)}, speed {ag.speed:F2})");
                Next();
                break;
            }

            case 102:   // swim out onto the Ferrovia quay without jumping
            {
                if (swimmer != null) { swimmer.GetComponent<EnemyHealth>().TakeDamage(999999f, false, true); swimmer = null; }
                Teleport(player, Cell(29f, 34f, 0.3f));
                var cc = player.GetComponent<CharacterController>();
                for (int i = 0; i < 60; i++) cc.Move(Vector3.left * 0.15f + Vector3.down * 0.2f);
                Check(!rescue.InWater(player.transform.position) && player.transform.position.y > -0.05f && rescuedWater == 0,
                    $"walked out of the canal onto the quay (y {player.transform.position.y:F2}, cell {(rescue.ZoneAt(player.transform.position) != null ? rescue.ZoneAt(player.transform.position).displayName : "?")})");
                // and towards the closed Carnevale: the fence keeps him in the water
                Teleport(player, Cell(31f, 34f, 0.3f));
                for (int i = 0; i < 60; i++) cc.Move(Vector3.right * 0.15f + Vector3.down * 0.2f);
                Check(rescue.InWater(player.transform.position) && rescuedZone == 0, $"the Carnevale fence keeps him out of the closed barrio (x {player.transform.position.x:F1})");
                Next();
                break;
            }

            case 103:   // the tide goes down with him in the canal: back on a quay, no damage, no bath
            {
                Teleport(player, Cell(29f, 34f, 0.3f));
                hp0 = player.CurrentHealth;
                dryCooler = false;
                foreach (var a in Object.FindObjectsByType<AmmoPickup>()) Object.Destroy(a.gameObject);   // only count the tide's
                if (swimmer != null) swimmer.GetComponent<EnemyHealth>().TakeDamage(999999f, false, true);
                rm.DebugEndRound();
                step = 104; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 104:
                foreach (var a in Object.FindObjectsByType<AmmoPickup>()) if (!rescue.InWater(a.transform.position)) dryCooler = true;   // he may grab it at once
                if (tide.Flooding) { if (rt > 20f) { Write("FAIL the tide never went down"); Finish("failed"); } return; }
                if (rt < 2f) return;
                Check(!rescue.InWater(player.transform.position) && player.CurrentHealth == hp0 && !tide.SwimActive && !NavAgents.SwimOpen,
                    $"tide down: back on the quay without damage ({player.CurrentHealth}/{hp0}), canals closed again");
                Check(dryCooler, "the tide leaves a Nevera de munición on dry ground ("
                    + string.Join(", ", Object.FindObjectsByType<AmmoPickup>().Select(a => a.name + " " + a.transform.position.ToString("F1") + (rescue.InWater(a.transform.position) ? " water" : " dry"))) + ")");
                step = 15; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;

            case 13:
            {
                if (rt < 1.2f) return;
                var pc = player.GetComponent<PlayerController>();
                Check(Mathf.Approximately(pc.ExternalSpeedFactor, 1f), $"on the terrace (level 1): speed x{pc.ExternalSpeedFactor:F2}");
                rm.DebugEndRound();
                Next();
                break;
            }

            case 14:
                if (tide.Flooding) { if (rt > 20f) { Write("FAIL the tide never went down"); Finish("failed"); } return; }
                if (rt < 1f) return;
                Check(Object.FindObjectsByType<AmmoPickup>().Length > pickups0, "the tide leaves a Nevera de munición");
                Next();
                break;

            case 15:   // gondola (if the map has one): both ends open -> pays and carries
            {
                var g = Object.FindObjectsByType<GondolaStop>().FirstOrDefault(s => s.CanRide);
                if (g == null)   // the gondolas only sail open water: open the Cimitero (door 4) for the Mercato <-> Cimitero canal
                {
                    var d4 = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.name.StartsWith("Door4_"));
                    if (d4 != null) d4.DebugOpen();
                    g = Object.FindObjectsByType<GondolaStop>().FirstOrDefault(s => s.CanRide);
                }
                if (g == null) { Write("FAIL no gondola stop with both ends open"); step = 17; break; }
                hullClips = 0; hullSamples = 0;
                Teleport(player, g.transform.position + Vector3.up * 0.3f);
                before = g.Destination;
                hp0 = gm.Points;
                g.Interact(player.GetComponent<PlayerInteractor>());
                stop = g;
                Next();
                break;
            }

            case 16:
                if (stop.Busy && stop.Boat != null)   // A4: the hull (4.7 x 0.65 m) must not go through quays, bridges or props
                {
                    hullSamples++;
                    var b = stop.Boat;
                    var hits = Physics.OverlapBox(b.position + Vector3.up * 0.35f, new Vector3(0.3f, 0.25f, 2.2f), b.rotation, (1 << 0) | (1 << 11) | (1 << 12), QueryTriggerInteraction.Ignore);
                    if (hits.Any(h => h.transform.parent == null || h.transform.parent.name != "SueloNado")) hullClips++;
                    if (hullSamples == 40) Shot("gondola_viaje", b.position + b.right * 4f + Vector3.up * 2.5f, b.position);
                }
                if (stop.Busy) { if (rt > 60f) { Write("FAIL the gondola never arrived"); Finish("failed"); } return; }
                if (rt < 0.5f) return;
                Check(hullClips <= hullSamples / 20, $"gondola hull touched something in {hullClips}/{hullSamples} frames of the ride");
                Check(Vector3.Distance(player.transform.position, before) < 2.5f && hp0 - gm.Points > 0,
                    $"gondola: paid {hp0 - gm.Points}, {rt:F1} s ride, landed {Vector3.Distance(player.transform.position, before):F1} m from the other stop ({rescue.ZoneAt(player.transform.position).displayName})");
                Next();
                break;

            // ---------------------------------------------------------------- the vaporetto task list
            case 17:
            {
                var m = BoatMission.Instance;
                var inter = player.GetComponent<PlayerInteractor>();
                var tasks = Object.FindObjectsByType<MissionTask>(FindObjectsInactive.Include);
                var shovel = tasks.FirstOrDefault(t => t.TaskKind == MissionTask.Kind.Shovel);
                var grave = tasks.FirstOrDefault(t => t.TaskKind == MissionTask.Kind.Grave);
                var gond = tasks.FirstOrDefault(t => t.TaskKind == MissionTask.Kind.Gondolier);
                if (m == null || !m.HasTasks || shovel == null || grave == null || gond == null) { Write("FAIL the scene has no task list (shovel / grave / gondolier)"); Finish("failed"); return; }
                var list = m.TaskList();
                Check(list.Count == 5 && list.All(t => !t.done), $"task list: 5 jobs, none done yet ({m.Objective()})");
                var hud = GameObject.Find("Objective");
                var txt = hud != null ? hud.GetComponent<UnityEngine.UI.Text>() : null;
                Check(txt != null && txt.text.Contains("VAPORETTO") && txt.text.Contains("[  ]"), "the HUD shows the list with empty checks");
                Check(!grave.CanInteract(inter), "no shovel: Giuseppe's grave can't be dug (" + grave.Prompt(inter) + ")");
                gm.AddPoints(5000);
                int p0 = gm.Points;
                shovel.Interact(inter);
                Check(m.Shovel && p0 - gm.Points == gm.Economy.shovelCost, $"shovel bought for {p0 - gm.Points} (expected {gm.Economy.shovelCost})");
                grave.Interact(inter);
                Check(m.Giuseppe, "Giuseppe dug up");
                var spots = tasks.Where(t => t.TaskKind == MissionTask.Kind.Gondolier).ToList();
                var here = spots.Where(t => t.Here).ToList();
                var wrong = spots.FirstOrDefault(t => !t.Here);
                Check(spots.Count >= 3 && here.Count == 1 && m.GondolierSpot >= 0, $"golden gondolier: {spots.Count} possible spots, only spot {m.GondolierSpot} has him");
                if (wrong != null) { wrong.Interact(inter); Check(!m.Gondolier && !wrong.CanInteract(inter), "an empty spot does nothing"); }
                if (here.Count > 0) here[0].Interact(inter);
                Check(m.Gondolier, "the golden gondolier found");
                Next();
                break;
            }

            case 18:   // into the campanile door: carried up to the belfry
            {
                var climb = Object.FindAnyObjectByType<TowerClimb>();
                if (climb == null) { Write("FAIL no TowerClimb in the scene"); Finish("failed"); return; }
                player.GodMode = false;
                Teleport(player, climb.Entry + Vector3.up * 0.1f);
                Next();
                break;
            }

            case 19:
            {
                if (climbed == 0) { if (rt > 8f) { Write("FAIL walked into the campanile door and nothing happened"); Finish("failed"); } return; }
                if (rt < 0.5f) return;
                var climb = Object.FindAnyObjectByType<TowerClimb>();
                Check(player.transform.position.y > 15f, $"campanile: up in the belfry (y {player.transform.position.y:F1})");
                var bell = Object.FindObjectsByType<PowerSwitch>().FirstOrDefault();
                var inter = player.GetComponent<PlayerInteractor>();
                Check(bell != null && Vector3.Distance(bell.InteractPoint, player.transform.position) < bell.Radius && bell.Prompt(inter).Contains("campana"), "the bell is within reach up there (" + (bell != null ? bell.Prompt(inter) : "none") + ")");
                bell.Interact(inter);
                Check(BoatMission.Instance.Power && BoatMission.PowerOn, "bell rung: the light is on");
                hp0 = player.CurrentHealth;
                Teleport(player, climb.Top + Vector3.left * 3.5f);   // step off the west side
                Next();
                break;
            }

            case 20:
                if (player.transform.position.y > 0.6f) { if (rt > 8f) { Write("FAIL jumped off the campanile and never landed (y " + player.transform.position.y.ToString("F1") + ")"); Finish("failed"); } return; }
                if (rt < 0.5f) return;
                Check(player.CurrentHealth == hp0, $"jumped off the campanile (16 m): no fall damage ({player.CurrentHealth}/{hp0})");
                Next();
                break;

            case 21:   // nobody camps up there
            {
                var climb = Object.FindAnyObjectByType<TowerClimb>();
                climb.MaxStay = 1.5f;
                player.GodMode = true;
                Teleport(player, climb.Top + Vector3.up * 0.2f);
                Next();
                break;
            }

            case 22:
            {
                if (thrown == 0) { if (rt > 8f) { Write("FAIL stayed on the belfry past the limit and was not thrown off"); Finish("failed"); } return; }
                if (player.transform.position.y > 0.6f) return;
                Check(true, "thrown off the belfry after the time limit, landed on the piazza");
                var inter = player.GetComponent<PlayerInteractor>();
                foreach (var d in Object.FindObjectsByType<DiscPickup>()) d.Interact(inter);
                var egg = EasterEgg.Instance;
                Check(egg != null && egg.DiscsFound >= egg.DiscsNeeded, $"the 3 Julio records picked up ({(egg != null ? egg.DiscsFound : 0)})");
                egg.Completed = true;   // the karaoke song itself is the Isla del Faro test's business
                egg.NotifyChanged();
                MasterKey.Instance.Interact(inter);
                Check(BoatMission.Instance.KeyDone, "the key found");
                Next();
                break;
            }

            case 23:   // four jobs done -> the vaporetto is on its way; it arrives when a later round ends
            {
                var m = BoatMission.Instance;
                if (!m.Called) { if (rt > 5f) { Write("FAIL all the jobs done and the vaporetto was not called"); Finish("failed"); } return; }
                if (!m.Complete)
                {
                    if (rt > 90f) { Write("FAIL the vaporetto never arrived"); Finish("failed"); return; }
                    if (Time.realtimeSinceStartup > endRoundAt && !rm.InIntermission) { rm.DebugEndRound(); endRoundAt = Time.realtimeSinceStartup + 2f; }
                    return;
                }
                Check(m.TaskList().All(t => t.done), "all 5 checks ticked, vaporetto arrived after round " + m.DefenseUntilRound);
                var hud = GameObject.Find("Objective");
                var txt = hud != null ? hud.GetComponent<UnityEngine.UI.Text>() : null;
                Write("   HUD: " + (txt != null ? txt.text.Replace("\n", " | ") : "none"));
                Next();
                break;
            }

            case 24:   // the last door: free, but it wants the key
            {
                var door = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.RequiresMission);
                if (door == null) { Write("FAIL no vaporetto door"); Finish("failed"); return; }
                var inter = player.GetComponent<PlayerInteractor>();
                Check(BoatMission.Instance.DoorNeedsKey && door.Prompt(inter).Contains("Abrir"), "vaporetto door: opens with the key in the pocket (" + door.Prompt(inter) + ")");
                int p0 = gm.Points, z0 = ZoneManager.Instance.OpenCount;
                door.Interact(inter);
                Check(ZoneManager.Instance.OpenCount > z0 && gm.Points == p0 && MasterKey.Instance.Used, "vaporetto door opened for free and the key was spent");
                Next();
                break;
            }

            case 25:   // the boss comes as the Dux, with his golden mask
            {
                var boss = BossEncounter.Instance != null ? BossEncounter.Instance.Boss : null;
                var dux = Object.FindAnyObjectByType<DuxMask>();
                if (boss == null || dux == null || !dux.Wearing) { if (rt > 15f) { Write("FAIL the Dux never showed up with his mask (boss " + (boss != null) + ", DuxMask " + (dux != null) + ")"); Finish("failed"); } return; }
                float before0 = boss.Health.Current;
                var hit = EnemyHits.ApplyLocal(boss.Health, 1000000f, true, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                Check(boss.Health.Masked && !hit.killed && !hit.headshot && boss.Health.Current < before0, $"the Dux wears the golden mask: a 1,000,000 headshot only chips it ({before0:F0} -> {boss.Health.Current:F0})");
                Shot("dux", boss.transform.position + boss.transform.forward * 5f + Vector3.up * 3f, boss.transform.position + Vector3.up * 2.6f);
                step = 30; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 30:   // every bridge open: walk the whole map with the player's own body (ramps, bridges, terraces)
                foreach (var d in Object.FindObjectsByType<ZoneDoor>()) d.DebugOpen();
                Next();
                break;

            case 31:
            {
                if (rt < 1.5f) return;
                int rescues = rescuedWater + rescuedZone;
                var tour = new[]
                {
                    ("Ferrovia -> Mercato (ponte 1)", Cell(13, 40, 0), Cell(12, 25, 0)),
                    ("Mercato -> Cannaregio (ponte 2), up to the fondamenta alta", Cell(12, 25, 0), Cell(15, 3, 2)),
                    ("Cannaregio -> Arsenale over the big bridge (ponte 7), up to the rampart", Cell(15, 3, 2), Cell(45, 3, 2)),
                    ("Arsenale -> Piazza (ponte 8), up to the loggia", Cell(45, 3, 2), Cell(56, 19, 2)),
                    ("Piazza -> Cimitero (ponte 5), up to the tower top", Cell(56, 19, 2), Cell(30, 23, 4)),
                    ("Cimitero -> Mercato (ponte 4) -> Ferrovia", Cell(30, 23, 4), Cell(13, 40, 0)),
                    ("Ferrovia -> Carnevale over the big bridge (ponte 3), up to the stage", Cell(13, 40, 0), Cell(45, 39, 2)),
                    ("Carnevale -> Piazza (ponte 6) -> vaporetto", Cell(45, 39, 2), Cell(66, 24, 0)),
                };
                foreach (var (label, a, b) in tour) Walk(player, label, a, b);
                Check(rescuedWater + rescuedZone == rescues, "nobody fell or was sent back during the walks");
                Next();
                break;
            }

            default:
                Check(errors == 0, $"{errors} errors in the console");
                Write("DONE");
                Finish(Log.Contains("FAIL") ? "failed" : "done");
                break;
        }
    }
}
