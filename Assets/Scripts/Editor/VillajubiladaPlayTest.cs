// Tools > Island > Play Test Villajubilada: one play session on the village map.
// Act 1: the Porton is shut (collider + NavMesh), the two paid start zones, the lighter lies in the one opened second,
// the bar's drink needs the bar's fuse box. First encierro (forced): the Porton opens, night falls, the Calle Mayor
// opens, flowerpots fly, the herd goes up the street, whoever stayed behind is dragged through, the Porton shuts, the
// start zones close and their old men are gone; the herd turns round at the closed barricades (never inside a closed
// zone), a player in the street is hit, a player in a doorway is not, trampled old men pay nothing; a Nevera at the end;
// the night stays after the round. Act 2: a fuse box lights its zone only (bulbs, machines); every box = the task;
// the traca (charge, kills that pay, cooldown, burns the player); second encierro with the whole ring open = laps;
// the drummer's aura, the firecracker patch; the pregon task list to the tractor, last barricade with the key, boss,
// extraction; 0 errors in the console. Results in EditorPrefs "VillajubiladaPlayTest.Log" / ".Status",
// screenshots Renders/VillajubiladaTest_*.png.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class VillajubiladaPlayTest
{
    const string Key = "VillajubiladaPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static int step, errors;
    static float stepTime, realTime, endRoundAt, maxBullZ, healthBefore;
    static int hits0, trampled0, points0, kills0, planPartial;
    static EnemyAI drummer, follower;
    static TracaTrap trap;

    static VillajubiladaPlayTest()
    {
        if (EditorPrefs.GetBool(Key + "Running")) Hook();
    }

    static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    [MenuItem("Tools/Island/Play Test Villajubilada")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(IslaZombieSetup.Villajubilada.ScenePath, OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        step = 0; errors = 0;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        Hook();
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; if (errors <= 5) Write("   console: " + msg); }
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");
    static void Check(bool pass, string text) => Write((pass ? "OK   " : "FAIL ") + text);

    static void Finish(string status)
    {
        Application.logMessageReceived -= OnLog;
        EditorPrefs.SetString(Key + "Status", status);
        EditorPrefs.SetBool(Key + "Running", false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void Next() { step++; stepTime = Time.time; realTime = Time.realtimeSinceStartup; }
    static void Fail(string why) { Write("FAIL " + why); Finish("failed"); }

    static void Tick()
    {
        if (!EditorPrefs.GetBool(Key + "Running")) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        try { Step(); }
        catch (System.Exception e) { Write("EXCEPTION: " + e); Finish("failed"); }
    }

    // layout cell -> world (origin cell 28, 30)
    static Vector3 Cell(float c, float r, float y = 0f) => new Vector3((c - 28f) * 2f, y, (30f - r) * 2f);

    static void Teleport(PlayerHealth p, Vector3 pos)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.position = pos + Vector3.up * 0.1f;
        cc.enabled = true;
    }

    static void Walk(PlayerHealth player, string label, Vector3 a, Vector3 b)
    {
        if (!NavMesh.SamplePosition(a, out var ha, 4f, NavAgents.Filter(0)) || !NavMesh.SamplePosition(b, out var hb, 4f, NavAgents.Filter(0)))
        { Write($"FAIL walk {label}: endpoints not on the NavMesh"); return; }
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(ha.position, hb.position, NavAgents.Filter(0), path) || path.status != NavMeshPathStatus.PathComplete)
        { Write($"FAIL walk {label}: no NavMesh path ({path.status})"); return; }
        Teleport(player, ha.position);
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

    static Zone ZoneNamed(string display) => ZoneManager.Instance.Zones.FirstOrDefault(z => z.displayName == display);

    static void Shot(string name, Vector3 pos, Vector3 look)
    {
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 70f; cam.farClipPlane = 300f;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        RenderTexture.active = null; cam.targetTexture = null;
        Directory.CreateDirectory("Renders");
        File.WriteAllBytes("Renders/VillajubiladaTest_" + name + ".png", tex.EncodeToPNG());
        Object.Destroy(tex); rt.Release(); Object.Destroy(rt); Object.Destroy(go);
    }

    static void Step()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        ZoneDoor.IgnoreRoundLimit = true;   // opens several doors in one round (the one-door-per-round rule is checked in SmokePlayTest)
        if (gm.State == GameState.GameOver) { Fail($"player died at step {step}"); return; }
        var cards = CardManager.Instance;
        if (cards != null && cards.CurrentOffer != null) { cards.Choose(cards.CurrentOffer[0]); return; }
        var player = gm.Player;
        var inter = player.GetComponent<PlayerInteractor>();
        var rm = RoundManager.Instance;
        var enc = Encierro.Instance;
        var bm = BoatMission.Instance;
        var gate = Object.FindAnyObjectByType<EncierroGate>();
        var night = Object.FindAnyObjectByType<DayNightCycle>();
        float rt = Time.realtimeSinceStartup - realTime;
        if (enc != null && enc.Running) foreach (var b in enc.Bulls) if (b.gameObject.activeSelf) maxBullZ = Mathf.Max(maxBullZ, b.position.z);

        switch (step)
        {
            case 0:
                player.GodMode = true;
                if (enc == null || gate == null || bm == null) { Fail("no Encierro / Porton / mission in the scene"); return; }
                Check(bm.Pueblo && bm.HasTasks && bm.HasZonePower, $"mission in village mode: task list, {bm.ZonesToLight} zones to light");
                Check(enc.ActOne && gate.Blocked && !ZoneNamed("Calle Mayor").IsOpen && ZoneNamed("La Parada").IsOpen, "act 1: the Porton is shut, only La Parada is open");
                Next();
                break;

            case 1:   // round 1 spawns; the Porton really blocks (collider and NavMesh)
            {
                var alive = Object.FindObjectsByType<EnemyHealth>().Where(e => !e.IsDead).ToArray();
                if (alive.Length < 1) { if (rt > 25f) Fail("round 1 never spawned an old man"); return; }
                Vector3 south = enc.Route[enc.GateIndex] + Vector3.back * 3f, north = enc.PastTheGate;
                bool wall = Physics.Linecast(south + Vector3.up, north + Vector3.up, 1 << 11, QueryTriggerInteraction.Ignore);
                var path = new NavMeshPath();
                bool reach = NavMesh.CalculatePath(south, north, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                Check(wall && !reach, $"the Porton blocks the way out (wall {wall}, old men can path through {reach})");
                Check(enc.IsEncierroRound(5) && !enc.IsEncierroRound(4) && enc.IsEncierroRound(10), "encierros in rounds 5, 10, ...");
                gm.AddPoints(100000);
                Next();
                break;
            }

            case 2:   // the two paid start zones; the lighter lies in the one opened second
            {
                if (rt < 0.3f) return;
                var d1 = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.name.StartsWith("Door1_"));
                var d2 = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.name.StartsWith("Door2_"));
                if (d1 == null || d2 == null) { Fail("start doors not found"); return; }
                int p0 = gm.Points;
                d1.Interact(inter);
                Check(p0 - gm.Points == 750 && ZoneNamed("El Bar").IsOpen && enc.SecondZone == null, "El Bar opened for 750; no lighter yet");
                step = 200; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }
            case 200:
            {
                if (rt < 0.5f) return;
                var d2 = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.name.StartsWith("Door2_"));
                d2.Interact(inter);
                step = 201; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }
            case 201:
            {
                if (rt < 0.5f) return;
                var corrales = ZoneNamed("Los Corrales");
                var lighters = Object.FindObjectsByType<MissionTask>(FindObjectsInactive.Include).Where(t => t.name.StartsWith("Mechero_")).ToArray();
                var here = lighters.Where(t => t.Here).ToArray();
                Check(enc.SecondZone == corrales && here.Length == 1 && here[0].name == "Mechero_Corrales", $"the lighter lies in the zone opened second ({(here.Length == 1 ? here[0].name : here.Length + " spots")})");
                int p0 = gm.Points;
                if (here.Length == 1) here[0].Interact(inter);
                Check(bm.Shovel && gm.Points == p0, "lighter picked up for free");
                step = 3; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 3:   // the bar's drink works with the bar's fuse box only
            {
                var bar = ZoneNamed("El Bar");
                var machine = Object.FindObjectsByType<DrinkMachine>().OrderBy(m => (m.transform.position - Cell(14, 55)).sqrMagnitude).First();
                var box = Object.FindObjectsByType<PowerSwitch>().FirstOrDefault(s => s.Zone == bar);
                if (box == null) { Fail("no fuse box in El Bar"); return; }
                int p0 = gm.Points;
                machine.Interact(inter);
                Check(gm.Points == p0 && !machine.CanInteract(inter), "no light in the bar: the drink machine refuses and charges nothing");
                box.Interact(inter);
                Check(bm.ZoneLit(bar) && !bm.Power && machine.CanInteract(inter), "the bar's fuse box lights the bar only; its machine works now");
                machine.Interact(inter);
                Check(gm.Points < p0, "drink bought in the bar");
                Next();
                break;
            }

            case 4:   // force the encierro in the next round, stay in La Parada
                enc.ForceNext = true;
                rm.DebugEndRound();
                hits0 = enc.PlayersHit;
                Teleport(player, Cell(26, 53));
                Next();
                break;

            case 5:   // the rocket: Porton open, the Calle Mayor opens, night falls
            {
                if (!enc.Warning && !enc.Running) { if (rt > 40f) Fail("the encierro never started"); return; }
                Check(enc.FirstRun && !gate.Blocked && ZoneNamed("Calle Mayor").IsOpen && night != null && night.HoldNight, "rocket: the Porton opens, the Calle Mayor opens, night starts to fall");
                Shot("porton_abierto", Cell(30, 53, 2.5f), Cell(28.5f, 46, 1.5f));
                Teleport(player, enc.Route[enc.GateIndex + 14]);   // up the Calle del Encierro: the old ladies see him
                step = 50; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }
            case 50:
            {
                if (enc.PotsThrown == 0 || MacetaProjectile.Landed == 0) { if (rt > 12f) Fail("in the Calle del Encierro and no flowerpot was thrown / landed"); return; }
                Check(true, $"old ladies at the windows throw flowerpots at a player in the Calle del Encierro ({enc.PotsThrown} thrown, {MacetaProjectile.Hits} hit him standing still)");
                Teleport(player, Cell(26, 53));   // back to La Parada: he stays behind
                hits0 = enc.PlayersHit;
                step = 6; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 6:   // the herd goes through; the player who stayed is dragged, act 1 is over
            {
                if (enc.ActOne) { if (rt > 60f) Fail("act 1 never ended"); return; }
                bool south = Object.FindObjectsByType<EnemyHealth>().Any(e => !e.IsDead && enc.InActOneArea(e.transform.position));
                Check(!enc.InActOneArea(player.transform.position) && enc.PlayersHit > hits0, "the player who stayed in La Parada was dragged through the Porton by the herd");
                Check(gate.Blocked && !ZoneNamed("La Parada").IsOpen && !ZoneNamed("El Bar").IsOpen && !ZoneNamed("Los Corrales").IsOpen && !south, "the Porton is shut for good, the start zones are closed, no old men left behind");
                Shot("toros", player.transform.position + new Vector3(3f, 3f, -1f), enc.Bulls[0].position + Vector3.up);
                // now stand in the middle of the Calle Mayor: the herd must hit
                hits0 = enc.PlayersHit; trampled0 = enc.EnemiesTrampled; points0 = gm.Points;
                Teleport(player, enc.Route[enc.RingFrom + 14]);
                Next();
                break;
            }

            case 7:
            {
                if (enc.Running && enc.PlayersHit == hits0) { if (rt > 60f) Fail("standing on the route, never hit by the herd"); return; }
                Check(enc.PlayersHit > hits0, "a player standing in the street is hit by the herd");
                Teleport(player, Cell(24, 28));   // a doorway of the Calle Mayor
                hits0 = enc.PlayersHit;
                Next();
                break;
            }

            case 8:   // in the doorway until the herd is back in the lorry
            {
                if (enc.Running) { if (rt > 120f) Fail("the encierro never finished"); return; }
                Check(enc.PlayersHit == hits0, "a player in a doorway is never hit");
                Check(maxBullZ < 15.5f, $"ring half open: the herd turned round at the closed barricades (max z {maxBullZ:F1}, the barricades are at 14)");
                Check(gm.Points == points0, $"{enc.EnemiesTrampled - trampled0} old men trampled in the street paid nothing");
                Check(enc.Done == 1 && Object.FindObjectsByType<AmmoPickup>().Length > 0, "the herd is back in the lorry and left a Nevera de munición");
                Next();
                break;
            }

            case 9:   // the night stays after the round
                rm.DebugEndRound();
                Next();
                break;
            case 10:
            {
                if (rt < 4f) return;
                Check(night.IsDark && night.HoldNight, "it stays night after the round (no dawn)");
                Shot("noche", Cell(21, 29.5f, 1.8f), Cell(36, 29.5f, 2.5f));
                Next();
                break;
            }

            case 11:   // a fuse box lights its own zone: bulbs, machine
            {
                var calle = ZoneNamed("Calle Mayor");
                var fl = Object.FindObjectsByType<FiestaLights>().First(f => f.Zone == calle);
                var box = Object.FindObjectsByType<PowerSwitch>().First(s => s.Zone == calle);
                var machine = Object.FindObjectsByType<DrinkMachine>().OrderBy(m => (m.transform.position - Cell(20, 32)).sqrMagnitude).First();
                Check(!fl.On && !machine.CanInteract(inter), "Calle Mayor dark: no bulbs, its machine has no light");
                box.Interact(inter);
                Check(bm.ZoneLit(calle) && !bm.ZoneLit(ZoneNamed("Plaza Mayor")), "the fuse box lights the Calle Mayor, not the plaza");
                Next();
                break;
            }
            case 12:
            {
                var calle = ZoneNamed("Calle Mayor");
                var fl = Object.FindObjectsByType<FiestaLights>().First(f => f.Zone == calle);
                if (fl.BulbsOn < fl.BulbCount) { if (rt > 20f) Fail($"the garlands never finished coming on ({fl.BulbsOn}/{fl.BulbCount})"); return; }
                if (rt < 6f) return;   // the real lights fade in after the last string
                Check(fl.On && fl.BulbCount > 5, $"{fl.BulbCount} strings of bulbs came on one after another");
                Shot("luces", Cell(21, 29.5f, 1.8f), Cell(36, 29.5f, 2.5f));
                Next();
                break;
            }

            case 13:   // the traca: charge, kills that pay, burns the player, cooldown
            {
                trap = Object.FindObjectsByType<TracaTrap>().OrderBy(t => (t.transform.position - Cell(36, 29)).sqrMagnitude).First();
                Vector3 mid = (trap.From + trap.To) / 2f;
                for (int i = 0; i < 2; i++)
                {
                    var e = rm.SpawnEnemy(null, mid);
                    if (e == null) continue;
                    var ag = e.GetComponent<NavMeshAgent>();
                    if (ag != null && ag.isOnNavMesh) ag.Warp(Vector3.Lerp(trap.From, trap.To, 0.4f + 0.2f * i));
                }
                player.GodMode = false;
                Teleport(player, Vector3.Lerp(trap.From, trap.To, 0.8f));
                healthBefore = player.CurrentHealth;
                Next();
                break;
            }
            case 14:
            {
                if (rt < 1.2f) return;   // the old men finish coming out
                points0 = gm.Points; kills0 = trap.Kills;
                trap.Interact(inter);
                Check(points0 - gm.Points == gm.Economy.tracaCost && trap.Burning, $"traca lit for {points0 - gm.Points} conchas");
                points0 = gm.Points;
                Next();
                break;
            }
            case 15:
            {
                if (trap.Burning) { if (rt > 10f) Fail("the traca never burnt out"); return; }
                player.GodMode = true;
                Check(trap.Kills - kills0 >= 2 && gm.Points > points0, $"the traca killed {trap.Kills - kills0} old men in its lane and they paid ({gm.Points - points0} conchas)");
                Check(player.CurrentHealth < healthBefore, $"the player in the lane got burnt ({healthBefore} -> {player.CurrentHealth})");
                Check(!trap.Ready && !trap.CanInteract(inter), "the traca is spent: cooldown before the next one");
                Next();
                break;
            }

            case 16:   // the whole ring open: the second encierro does laps
            {
                planPartial = enc.PlanSamples().Count;
                foreach (var d in Object.FindObjectsByType<ZoneDoor>().Where(d => d.name.StartsWith("Door3_") || d.name.StartsWith("Door4_") || d.name.StartsWith("Door5_"))) d.DebugOpen();
                Next();
                break;
            }
            case 17:
            {
                if (rt < 0.5f) return;
                int full = enc.PlanSamples().Count;
                Check(full > planPartial, $"ring open: the herd's plan grows from {planPartial} to {full} samples (laps)");
                maxBullZ = -999f;
                enc.ForceNext = true;
                rm.DebugEndRound();
                Teleport(player, Cell(24, 28));
                Next();
                break;
            }
            case 18:
            {
                if (enc.Done < 2) { if (rt > 150f) Fail("the second encierro never finished"); return; }
                Check(maxBullZ > 30f && gate.Blocked && !enc.ActOne, $"second encierro: laps round the whole ring (max z {maxBullZ:F1}), the Porton stays shut");
                Next();
                break;
            }

            case 19:   // el del bombo: the old men around him walk faster while he lives
            {
                var bombo = Resources.FindObjectsOfTypeAll<EnemyVariant>().FirstOrDefault(v => v.name == "AbueloBombo");
                if (bombo == null) { Fail("no AbueloBombo variant"); return; }
                Teleport(player, Cell(30, 24));
                drummer = rm.SpawnEnemy(bombo, Cell(28, 24));
                follower = rm.SpawnEnemy(null, Cell(27, 25));
                step = 190; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }
            case 190:
            {
                if (drummer == null || follower == null) { Fail("could not spawn the drummer"); return; }
                if (follower.AuraFactor < 1.01f) { if (rt > 6f) Fail("the drummer's aura never reached the old man beside him"); return; }
                Check(Mathf.Abs(follower.AuraFactor - drummer.Variant.auraSpeedFactor) < 0.01f && drummer.AuraFactor < 1.01f, $"beside the drummer an old man walks x{follower.AuraFactor:F1} (the drummer himself x1)");
                drummer.GetComponent<EnemyHealth>().TakeDamage(999999f, false, true);
                step = 191; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }
            case 191:
            {
                if (follower != null && follower.AuraFactor > 1.01f) { if (rt > 4f) Fail("the aura stayed after the drummer died"); return; }
                Check(true, "the drummer dies and the old man goes back to his speed");
                // la de los petardos: what she throws leaves a burning patch
                var petardos = Resources.FindObjectsOfTypeAll<EnemyVariant>().FirstOrDefault(v => v.name == "AbuelaPetardos");
                var granny = petardos != null ? rm.SpawnEnemy(petardos, Cell(28, 25)) : null;
                if (granny == null) { Fail("could not spawn la de los petardos"); return; }
                ChanclaProjectile.Throw(granny.transform.position + Vector3.up * 1.4f, Cell(31, 24, 0.2f), petardos.throwSpeed, petardos.throwDamage, granny.transform);
                step = 192; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }
            case 192:
            {
                if (GasCloud.Alive == 0) { if (rt > 5f) Fail("the firecrackers left no burning patch"); return; }
                Check(true, "la de los petardos: her throw leaves a burning patch on the ground");
                foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(999999f, false, true);
                step = 22; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 22:   // every fuse box = the task; Las Eras lights up with the whole village
            {
                foreach (var s in Object.FindObjectsByType<PowerSwitch>().Where(s => s.Counts)) s.Interact(inter);
                var eras = Object.FindObjectsByType<FiestaLights>().FirstOrDefault(f => f.Zone == ZoneNamed("Las Eras"));
                Check(bm.Power && bm.LitZones == bm.ZonesToLight, $"all {bm.ZonesToLight} fuse boxes on: the village is lit");
                Check(BoatMission.PowerOnFor(ZoneNamed("Plaza Mayor")), "the Microondas of the plaza has light");
                Write("   Las Eras lights follow the whole village: " + (eras != null));
                Next();
                break;
            }

            case 23:   // chupinazo (needs the lighter), cabezudo, records, key
            {
                if (rt < 0.5f) return;
                var chup = Object.FindObjectsByType<MissionTask>().First(t => t.TaskKind == MissionTask.Kind.Grave);
                Check(chup.Prompt(inter).Contains("chupinazo") && chup.CanInteract(inter), "the balcony: " + chup.Prompt(inter));
                chup.Interact(inter);
                var cab = Object.FindObjectsByType<MissionTask>().FirstOrDefault(t => t.TaskKind == MissionTask.Kind.Gondolier && t.Here);
                if (cab == null) { Fail("no golden cabezudo spot is active"); return; }
                Shot("cabezudo", cab.transform.position + new Vector3(2.5f, 2f, -2.5f), cab.transform.position + Vector3.up * 1.2f);
                cab.Interact(inter);
                Check(bm.Giuseppe && bm.Gondolier, "chupinazo fired and the golden cabezudo found (spot " + bm.GondolierSpot + ")");
                foreach (var d in Object.FindObjectsByType<DiscPickup>()) d.Interact(inter);
                var egg = EasterEgg.Instance;
                Check(egg != null && egg.DiscsFound >= egg.DiscsNeeded, $"the 3 Julio records picked up ({(egg != null ? egg.DiscsFound : 0)})");
                egg.Completed = true;   // the karaoke song itself is the Isla del Faro test's business
                egg.NotifyChanged();
                MasterKey.Instance.Interact(inter);
                Check(bm.KeyDone, "the tractor key found");
                Next();
                break;
            }

            case 24:   // four jobs done -> the tractor is on its way; it arrives when a later round ends
            {
                if (!bm.Called) { if (rt > 5f) Fail("all the jobs done and the tractor was not called"); return; }
                if (!bm.Complete)
                {
                    if (rt > 120f) { Fail("the tractor never arrived"); return; }
                    if (Time.realtimeSinceStartup > endRoundAt && !rm.InIntermission) { rm.DebugEndRound(); endRoundAt = Time.realtimeSinceStartup + 2f; }
                    return;
                }
                Check(bm.TaskList().All(t => t.done), "all 5 checks ticked, the tractor arrived after round " + bm.DefenseUntilRound);
                Write("   objective: " + bm.Objective());
                Next();
                break;
            }

            case 25:   // the last barricade: free, with the key
            {
                var door = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.RequiresMission);
                if (door == null) { Fail("no last door"); return; }
                int p0 = gm.Points, z0 = ZoneManager.Instance.OpenCount;
                foreach (var d in Object.FindObjectsByType<ZoneDoor>().Where(d => !d.RequiresMission)) d.DebugOpen();
                z0 = ZoneManager.Instance.OpenCount;
                door.Interact(inter);
                Check(ZoneManager.Instance.OpenCount > z0 && gm.Points == p0 && MasterKey.Instance.Used, "Las Eras opened for free and the key was spent");
                step = 250; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 250:   // every barricade open: walk the village with the player's own body (ramps, doorways, alleys)
            {
                if (rt < 1.5f) return;
                var tour = new[]
                {
                    ("Calle del Encierro, from the Porton up to the Calle Mayor", enc.PastTheGate, Cell(28.5f, 30)),
                    ("into a doorway of the Calle del Encierro and one of the Calle Mayor", Cell(33, 37), Cell(24, 28)),
                    ("Calle Mayor -> Cuestas, up the ramp to the mirador", Cell(24, 28), Cell(5, 13, 2)),
                    ("the upper alleys of the Cuestas and down again to the dead end with the drink", Cell(5, 13, 2), Cell(14, 13)),
                    ("Cuestas -> round the north street -> the atrium of the church (up its ramp)", Cell(14, 13), Cell(28, 7, 2)),
                    ("atrium -> Plaza Mayor, up onto the bandstand", Cell(28, 7, 2), Cell(28.5f, 21.5f, 2)),
                    ("bandstand -> ferial, between the stalls to the far corner", Cell(28.5f, 21.5f, 2), Cell(54, 28)),
                    ("ferial -> Las Eras, to the tractor", Cell(54, 28), Cell(54, 4)),
                };
                foreach (var (label, a, b) in tour) Walk(player, label, a, b);
                var lit = Object.FindObjectsByType<FiestaLights>();
                Check(lit.All(f => f.On) && lit.Sum(f => f.WindowCount) > 50, $"every zone has its fiesta lights on: {lit.Sum(f => f.BulbCount)} strings, {lit.Sum(f => f.WindowCount)} house fronts with lit windows");
                Shot("pueblo_encendido", Cell(28.5f, 36, 16f), Cell(28.5f, 22, 0f));
                step = 26; stepTime = Time.time; realTime = Time.realtimeSinceStartup;
                break;
            }

            case 26:   // boss, extraction on the tractor
            {
                var boss = BossEncounter.Instance != null ? BossEncounter.Instance.Boss : null;
                if (boss == null) { if (rt > 15f) Fail("the boss never showed up"); return; }
                var look = Object.FindAnyObjectByType<PregoneroLook>();
                if (look == null || !look.Wearing) { if (rt > 15f) Fail("the boss is not dressed as the pregonero"); return; }
                if (!gm.BossDefeated && !boss.Health.IsDead && rt < 1f) Shot("pregonero", boss.transform.position + boss.transform.forward * 5f + Vector3.up * 3f, boss.transform.position + Vector3.up * 2.6f);
                if (!gm.BossDefeated)
                {
                    if (!boss.Health.IsDead) EnemyHits.ApplyLocal(boss.Health, 100000000f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                    if (rt < 6f && !gm.BossDefeated) return;
                }
                Check(gm.BossDefeated, "boss defeated");
                Next();
                break;
            }
            case 27:
            {
                if (rt < 0.5f) return;
                var ext = Object.FindAnyObjectByType<ExtractionPoint>();
                Teleport(player, ext.transform.position + Vector3.left * 1.5f);
                Shot("tractor", ext.transform.position + new Vector3(-6f, 3f, 2f), ext.transform.position + Vector3.back * 4f + Vector3.up);
                ext.Interact(inter);
                Check(gm.State == GameState.Victory, $"extraction on the tractor -> state {gm.State}");
                Check(errors == 0, $"{errors} errors in the console");
                Write("DONE");
                Finish(Log.Contains("FAIL") ? "failed" : "done");
                break;
            }
        }
    }
}
