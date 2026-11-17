// Scripted play test of the "base" features (Tools > Island > Play Test Base) on Isla del Faro:
// collision audit + random walks (invisible walls), sprint toggle with a virtual gamepad, camera with
// the back against a wall, ukulele melee (+ lunge), floating points, spawn entrances (time in sight,
// spawn side vs camera), night round (only senderistas, darkness, chanclas, dawn + ammo reward),
// shotgun points once per shot, run log. Results in EditorPrefs "BasePlayTest.Log" / ".Status",
// screenshots in Renders/Base_*.png. Runs as a coroutine driven by EditorApplication.update.
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

public static class BasePlayTest
{
    const string Key = "BasePlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();   // nested coroutines (Wait...)
    static Gamepad pad;
    static readonly List<(int amount, PointSource source)> deltas = new List<(int, PointSource)>();
    static int thrown;

    static bool probe;

    // Short diagnostic run: frame timing + sprint values per frame.
    public static void Probe() { probe = true; Start(); }

    [MenuItem("Tools/Island/Play Test Base")]
    public static void Run() { probe = false; Start(); }

    static void Start()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(TestMapSetup.IslaZombieScenePath, OpenSceneMode.Single);
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
        EnemyAI.Thrown -= OnThrown;
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
        ZoneDoor.IgnoreRoundLimit = true;   // opens several doors in one round (the one-door-per-round rule is checked in SmokePlayTest)
        try
        {
            if (script.Count == 0) script.Push(probe ? ProbeScript() : Script());
            while (script.Count > 0)
            {
                var top = script.Peek();
                if (!top.MoveNext()) { script.Pop(); if (script.Count == 0) { Finish("done"); return; } continue; }
                if (top.Current is IEnumerator nested) { script.Push(nested); continue; }
                break;   // yield return null: next frame
            }
        }
        catch (System.Exception e) { Write("EXCEPTION: " + e); Finish("failed"); }
    }

    static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    static void OnThrown(EnemyAI e) => thrown++;

    // ------------------------------------------------------------------ the test
    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var rm = RoundManager.Instance;
        var player = gm.Player;
        var cc = player.GetComponent<CharacterController>();
        var pc = player.GetComponent<PlayerController>();
        var rig = gm.CameraRig;
        rm.SpawningPaused = true;
        player.GodMode = true;
        deltas.Clear(); thrown = 0;
        gm.PointsDelta += (a, s) => deltas.Add((a, s));
        EnemyAI.Thrown -= OnThrown;
        EnemyAI.Thrown += OnThrown;
        Write($"Player {gm.Animal.id}, difficulty {GameSettings.Difficulty}, jumper agent type {NavAgents.JumperId}");
        yield return null;

        // ---------------------------------------------------------- 1. collision audit
        var lonely = new Dictionary<string, int>();
        foreach (var col in Object.FindObjectsByType<Collider>())
        {
            if (!col.enabled || col.isTrigger || col.bounds.max.y < 0.05f) continue;
            int layer = col.gameObject.layer;
            if (layer == 8 || layer == 9) continue;
            if (col.GetComponent<Renderer>() != null) continue;
            string n = col.name.Split('_')[0].Split('(')[0].Trim();
            lonely[n] = (lonely.TryGetValue(n, out var k) ? k : 0) + 1;
        }
        Write("Colliders without their own renderer (expected: Coast, Tronco, Agua, Cabina, door Barrier...): " + string.Join(", ", lonely.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "×" + kv.Value)));
        Check(!lonely.ContainsKey("Prop") && !lonely.ContainsKey("Caseta") && !lonely.ContainsKey("Coche") && !lonely.ContainsKey("Cobertura") && !lonely.ContainsKey("Autobus"),
            "no per-cell invisible prop blockers left (Prop / Caseta / Coche / Cobertura / Autobus)");

        // ---------------------------------------------------------- 2. random walks with every door open
        foreach (var d in Object.FindObjectsByType<ZoneDoor>()) d.DebugOpen();
        yield return Wait(0.5f);
        var boss = BossEncounter.Instance != null ? BossEncounter.Instance.Boss : null;   // opening the Muelle calls the boss
        if (boss != null) boss.Health.TakeDamage(boss.Health.Current + 1f, false);
        yield return Wait(0.5f);
        var targets = new List<Vector3>();
        foreach (var z in ZoneManager.Instance.Zones) foreach (var t in z.SpawnPoints.Concat(z.BoxSpots)) { var e = t.GetComponent<SpawnEntrance>(); targets.Add(e != null ? e.ExitPoint : t.position); }
        var rnd = new System.Random(7);
        for (int i = 0; i < 60; i++)
        {
            var v = new Vector3(rnd.Next(-44, 44), 0f, rnd.Next(-34, 36));
            if (NavAgents.Sample(v, 1f, out var s)) targets.Add(s);
        }
        int walks = 0, stuck = 0;
        var stuckAt = new List<string>();
        for (int i = 0; i < 90; i++)
        {
            var a = targets[rnd.Next(targets.Count)];
            var b = targets[rnd.Next(targets.Count)];
            var result = Walk(player, cc, a, b);
            if (result == null) continue;
            walks++;
            if (result.Length > 0) { stuck++; if (stuckAt.Count < 12) stuckAt.Add(result); }
            if (i % 10 == 9) yield return null;
        }
        Check(stuck == 0, $"random walks: {walks} paths walked with the CharacterController, {stuck} blocked" + (stuck > 0 ? ": " + string.Join(" | ", stuckAt) : ""));
        yield return null;

        // ---------------------------------------------------------- 3. sprint toggle (virtual gamepad)
        pad = InputSystem.AddDevice<Gamepad>("TestPad");
        Teleport(player, new Vector3(-30f, 0f, 4f), 0f);
        rig.SetTarget(player.transform);
        GameSettings.SprintToggle = true;
        float walkSpeed = 2.5f, sprintSpeed = 5f;
        Pad(new Vector2(0f, 1f), GamepadButton.LeftStick);
        yield return Wait(0.1f);
        Pad(new Vector2(0f, 1f));
        Vector3 sp0 = player.transform.position; float st0 = Time.time;
        yield return Wait(0.9f);
        float s1 = pc.HorizontalSpeed / Mult(player);
        Write($"   sprint diag: moved {Vector3.Distance(sp0, player.transform.position):F2} m in {Time.time - st0:F2} s, mult {Mult(player):F2}, strafe {pc.StrafeActive}, controls {pc.ControlsEnabled}, state {gm.State}, pads {Gamepad.all.Count}");
        bool sprintA = pc.IsSprinting;
        Pad(Vector2.zero);
        yield return Wait(0.5f);
        bool stopped = !pc.IsSprinting;
        Pad(new Vector2(0f, 1f));
        yield return Wait(0.8f);
        float s2 = pc.HorizontalSpeed / Mult(player);
        Check(sprintA && s1 > sprintSpeed * 0.9f, $"toggle: one L3 press keeps sprinting while the stick is held (speed {s1:F1} m/s base, sprinting={sprintA})");
        Check(stopped && s2 < walkSpeed * 1.1f, $"toggle: releasing the stick ends the sprint; pushing again walks ({s2:F1} m/s)");
        Pad(new Vector2(0f, 1f), GamepadButton.LeftStick);
        yield return Wait(0.1f);
        Pad(new Vector2(0f, 1f));
        yield return Wait(0.3f);
        Pad(new Vector2(0f, 1f), GamepadButton.LeftTrigger);   // aiming (L2) cancels it
        yield return Wait(0.4f);
        bool aimCancels = !pc.IsSprinting;
        Pad(new Vector2(0f, 1f));
        yield return Wait(0.4f);
        Check(aimCancels && !pc.IsSprinting, "toggle: aiming cancels the sprint and it does not come back by itself");
        GameSettings.SprintToggle = false;
        Pad(new Vector2(0f, 1f), GamepadButton.LeftStick);
        yield return Wait(0.6f);
        bool holdSprint = pc.IsSprinting;
        Pad(new Vector2(0f, 1f));
        yield return Wait(0.3f);
        Check(holdSprint && !pc.IsSprinting, "hold mode (setting): sprint only while L3 is held");
        GameSettings.SprintToggle = true;
        Pad(Vector2.zero);
        yield return Wait(0.3f);

        // ---------------------------------------------------------- 4. camera with the back to a wall
        Teleport(player, new Vector3(-27.45f, 0f, 6f), 270f);   // Chiringuito, wall of the camping right behind
        rig.SetTarget(player.transform);
        yield return Wait(0.6f);
        var cam = Camera.main;
        bool camInWall = Physics.CheckSphere(cam.transform.position, 0.06f, (1 << 0) | (1 << 11) | (1 << NavAgents.PropLayer), QueryTriggerInteraction.Ignore);
        Write($"back to wall: camera distance {rig.CurrentDistance:F2} m, character hidden={rig.TargetHidden}, camera height {cam.transform.position.y - player.transform.position.y:F2} m");
        Check(!camInWall && (rig.CurrentDistance > 0.95f || rig.TargetHidden), "back to a wall: camera outside the wall and never inside the head (pulled up over the shoulder, or the character hidden)");
        Shot(cam, "Renders/Base_camera_wall.png");
        Teleport(player, new Vector3(40f, 0f, 3f), 90f);          // the pier, looking at the sea: camera free behind
        rig.SetTarget(player.transform);
        yield return Wait(0.6f);
        Check(!rig.TargetHidden && rig.CurrentDistance > 3f, $"open space: normal camera ({rig.CurrentDistance:F1} m, character visible)");
        Shot(cam, "Renders/Base_camera_open.png");

        // ---------------------------------------------------------- 5. ukulele (on the pier)
        Teleport(player, new Vector3(36f, 0f, 3f), 90f);
        rig.SetTarget(player.transform);
        yield return Wait(0.3f);
        var victim = SpawnTestEnemy(rm, player.transform.position + player.transform.forward * 1.5f, 0f);
        deltas.Clear();
        yield return Wait(0.2f);
        Pad(Vector2.zero, GamepadButton.RightStick);
        yield return Wait(0.1f);
        Pad(Vector2.zero);
        yield return Wait(0.15f);
        var melee = player.GetComponent<PlayerMelee>();
        bool swinging = melee.IsSwinging;
        {
            Vector3 to = victim.transform.position - player.transform.position; to.y = 0f;
            Write($"   melee diag: enemy {to.magnitude:F2} m away, angle {Vector3.Angle(player.transform.forward, to):F0}, hp {victim.Current:F0}/{victim.MaxHealth:F0}, player {player.transform.position} fwd {player.transform.forward}, enemy {victim.transform.position}");
        }
        yield return Wait(0.5f);
        var meleePts = deltas.Where(x => x.source == PointSource.Melee).Sum(x => x.amount);
        Check(victim.IsDead, $"R3 ukulele kills a round-1 old man in one hit (hp {victim.MaxHealth:F0}, damage {melee.Data.damage})");
        Check(meleePts > 0 && swinging, $"melee kill pays +{meleePts} conchas (source Melee), ukulele swung");
        Check(GameObject.Find("PointsPopup") != null, "floating +points shown next to the counter");
        Shot(cam, "Renders/Base_melee.png");
        victim = SpawnTestEnemy(rm, player.transform.position + player.transform.forward * Mathf.Max(1.5f, melee.Data.lungeRange - 0.4f), 0f);
        Vector3 before = player.transform.position;
        yield return Wait(0.2f);
        melee.TrySwing();
        yield return Wait(0.7f);
        float lunge = Vector3.Distance(before, player.transform.position);
        Check(victim.IsDead && lunge > 0.6f, $"lunge: old man {melee.Data.lungeRange - 0.4f:F1} m away killed, player lunged {lunge:F1} m");

        // ---------------------------------------------------------- 6. shotgun: hit points once per shot, not per pellet
        var shooter = player.GetComponent<PlayerShooter>();
        var shotgun = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/Shotgun.asset");
        shooter.GiveWeapon(shotgun);
        Teleport(player, new Vector3(36f, 0f, 3f), 90f);
        rig.SetTarget(player.transform);
        yield return Wait(0.3f);
        // on the crosshair line (the camera is over the shoulder, so straight ahead of the body can be missed)
        var cross = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 flatDir = new Vector3(cross.direction.x, 0f, cross.direction.z).normalized;
        Vector3 onLine = cross.origin + flatDir * (Vector3.Dot(player.transform.position - cross.origin, flatDir) + 3f);
        var tank = SpawnTestEnemy(rm, new Vector3(onLine.x, player.transform.position.y, onLine.z), 100000f);
        yield return Wait(0.3f);
        deltas.Clear();
        Pad(Vector2.zero, GamepadButton.RightTrigger);
        yield return Wait(0.1f);
        Pad(Vector2.zero);
        yield return Wait(0.3f);
        var aimRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        string aimHit = Physics.Raycast(aimRay, out var ah, 60f, ~((1 << 8) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore) ? $"{ah.collider.name} ({LayerMask.LayerToName(ah.collider.gameObject.layer)}) at {ah.distance:F1} m" : "nothing";
        Write($"   shotgun diag: weapon {shooter.Current.Name} mag {shooter.Current.magazine}, enemy {Vector3.Distance(tank.transform.position, player.transform.position):F2} m at {tank.transform.position}, hp {tank.Current:F0}, deltas {deltas.Count}, crosshair hits {aimHit}, cam {cam.transform.position} fwd {cam.transform.forward}");
        int hitEvents = deltas.Count(x => x.source == PointSource.Hit);
        int hitPts = deltas.Where(x => x.source == PointSource.Hit).Sum(x => x.amount);
        Check(hitEvents == 1, $"shotgun: one shot on one old man pays hit points once ({hitEvents} events, +{hitPts}), not once per pellet");
        tank.TakeDamage(tank.Current + 1f, false);

        // ---------------------------------------------------------- 7. spawn entrances (day round)
        var spawns = new List<(EnemyAI ai, SpawnEntrance e, float t0, float angle)>();
        var visibleFrom = new Dictionary<EnemyAI, float>();
        var landed = new Dictionary<EnemyAI, float>();
        var inSight = new Dictionary<SpawnEntrance.Kind, List<float>>();
        System.Action<EnemyAI, Transform> onSpawn = (ai, t) =>
        {
            var e = t != null ? t.GetComponent<SpawnEntrance>() : null;
            Vector3 look = Camera.main.transform.forward; look.y = 0f;
            Vector3 to = (e != null ? e.ExitPoint : ai.transform.position) - player.transform.position; to.y = 0f;
            spawns.Add((ai, e, Time.time, Vector3.Angle(look, to)));
        };
        rm.EnemySpawned += onSpawn;
        Teleport(player, new Vector3(-10f, 0f, 2f), 0f);   // camping, everything open
        rig.SetTarget(player.transform);
        rm.SpawningPaused = false;
        var shotKinds = new HashSet<SpawnEntrance.Kind>();
        float until = Time.time + 40f;
        while (Time.time < until && spawns.Count < 24)
        {
            foreach (var sp in spawns)
            {
                if (sp.ai == null || sp.e == null) continue;
                if (!visibleFrom.ContainsKey(sp.ai))
                {
                    bool visible = sp.e.EntranceKind == SpawnEntrance.Kind.Cabina || sp.e.EntranceKind == SpawnEntrance.Kind.Escalerilla
                        ? Time.time >= sp.t0 + sp.e.ShakeTime
                        : Vector3.Dot(sp.ai.transform.position - sp.e.transform.position, sp.e.transform.forward) > 0f;
                    if (visible) visibleFrom[sp.ai] = Time.time;
                }
                if (!landed.ContainsKey(sp.ai) && !sp.ai.IsEmerging && Time.time > sp.t0 + 0.1f)
                {
                    landed[sp.ai] = Time.time;
                    if (visibleFrom.TryGetValue(sp.ai, out var vis))
                    {
                        if (!inSight.ContainsKey(sp.e.EntranceKind)) inSight[sp.e.EntranceKind] = new List<float>();
                        inSight[sp.e.EntranceKind].Add(Time.time - vis);
                    }
                }
                if (!shotKinds.Contains(sp.e.EntranceKind) && sp.ai.IsEmerging && Time.time > sp.t0 + sp.e.ShakeTime + 0.35f)
                {
                    shotKinds.Add(sp.e.EntranceKind);
                    ShotFrom(sp.e.transform.position + sp.e.transform.forward * 4.5f + Vector3.up * 1.8f, sp.e.transform.position + Vector3.up * 1f, $"Renders/Base_spawn_{sp.e.EntranceKind}.png");
                }
            }
            // keep the round flowing: old men that landed 2 s ago die (no points)
            foreach (var kv in landed) if (kv.Key != null && Time.time - kv.Value > 2f) { var h = kv.Key.GetComponent<EnemyHealth>(); if (!h.IsDead) h.TakeDamage(h.Current + 1f, false); }
            yield return null;
        }
        rm.EnemySpawned -= onSpawn;
        var withEntrance = spawns.Where(s => s.e != null).ToList();
        Check(withEntrance.Count == spawns.Count && spawns.Count > 0, $"{spawns.Count} spawns, {withEntrance.Count} through an entrance");
        foreach (var kv in inSight)
            Check(kv.Value.Average() >= 0.7f && kv.Value.Average() <= 1.6f, $"entrance {kv.Key}: {kv.Value.Count} old men, in sight {kv.Value.Average():F2} s before landing (min {kv.Value.Min():F2}, max {kv.Value.Max():F2})");
        int front = spawns.Count(s => s.angle < 55f), side = spawns.Count(s => s.angle >= 55f && s.angle < 125f), back = spawns.Count(s => s.angle >= 125f);
        Check(side + back > front, $"spawn side vs camera: front {front}, sides {side}, back {back}");

        // ---------------------------------------------------------- 8. night round
        rm.ForceNextNight = true;
        rm.DebugEndRound();
        var night = Object.FindAnyObjectByType<DayNightCycle>();
        until = Time.time + 20f;
        while (Time.time < until && !rm.IsNight) { rm.DebugEndRound(); yield return null; }
        Check(rm.IsNight, $"forced night round started (round {rm.Round})");
        yield return Wait(3.5f);
        Check(night != null && night.Darkness > 0.95f, $"it is dark (darkness {(night != null ? night.Darkness : 0f):F2}, sun {(RenderSettings.sun != null ? RenderSettings.sun.intensity : -1f):F2})");
        var nightVariant = AssetDatabase.LoadAssetAtPath<EnemyVariant>("Assets/Data/Enemies/AbueloSenderista.asset");
        var nightEnemies = new List<EnemyAI>();
        System.Action<EnemyAI, Transform> onNight = (ai, t) => nightEnemies.Add(ai);
        rm.EnemySpawned += onNight;
        float maxHop = 0f;
        Teleport(player, new Vector3(-18f, 0f, 16f), 180f);   // parking: cars and the bus to jump over
        rig.SetTarget(player.transform);
        until = Time.time + 30f;
        bool nightShot = false;
        while (Time.time < until && (thrown < 3 || maxHop < 0.5f))
        {
            foreach (var e in nightEnemies) if (e != null && e.Hopping) maxHop = Mathf.Max(maxHop, e.GetComponent<NavMeshAgent>().baseOffset);
            if (!nightShot && nightEnemies.Count > 3 && Time.time > until - 24f) { nightShot = true; Shot(cam, "Renders/Base_night.png"); }
            yield return null;
        }
        rm.EnemySpawned -= onNight;
        Check(nightEnemies.Count > 0 && nightEnemies.All(e => e == null || e.Variant == nightVariant), $"night: {nightEnemies.Count} spawned, all abuelos senderistas (green)");
        var jumperTypes = nightEnemies.Where(e => e != null).Select(e => e.GetComponent<NavMeshAgent>().agentTypeID).Distinct().ToList();
        Check(jumperTypes.All(id => id == NavAgents.JumperId && id != 0), $"night old men walk on the Saltador NavMesh (agent types: {string.Join(",", jumperTypes)})");
        Check(maxHop > 0.5f, $"a senderista hopped onto a prop (max height {maxHop:F1} m)");
        Check(thrown > 0, $"chanclas thrown: {thrown}");
        rm.DebugEndRound();
        until = Time.time + 6f;
        while (Time.time < until && night.Darkness > 0.02f) yield return null;
        Check(night.Darkness < 0.05f, $"dawn after the night round (darkness {night.Darkness:F2})");
        Check(GameObject.Find("PowerUp_MaxAmmo") != null, "night reward: Nevera de munición dropped next to the player");

        // ---------------------------------------------------------- 9. run log
        rm.SpawningPaused = true;
        RunLog.Instance.Write("test");
        Check(File.Exists(RunLog.LastPath), "run log written: " + RunLog.LastPath);
        if (File.Exists(RunLog.LastPath)) Write(File.ReadAllText(RunLog.LastPath));
    }

    static IEnumerator ProbeScript()
    {
        var gm = GameManager.Instance;
        var player = gm.Player;
        var pc = player.GetComponent<PlayerController>();
        RoundManager.Instance.SpawningPaused = true;
        pad = InputSystem.AddDevice<Gamepad>("TestPad");
        Teleport(player, new Vector3(-30f, 0f, 4f), 0f);
        float t0 = Time.time, sumDt = 0f;
        int f0 = Time.frameCount, lastFrame = -1;
        float realStart = Time.realtimeSinceStartup;
        Pad(new Vector2(0f, 1f), GamepadButton.LeftStick);
        int n = 0;
        while (Time.time - t0 < 1.5f)
        {
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                sumDt += Time.deltaTime;
                if (n++ < 40) Write($"f{Time.frameCount - f0} t={Time.time - t0:F3} dt={Time.deltaTime:F3} udt={Time.unscaledDeltaTime:F3} ts={Time.timeScale} state={gm.State} sprint={pc.IsSprinting} speed={pc.HorizontalSpeed:F2} pos={player.transform.position}");
                if (n == 3) Pad(new Vector2(0f, 1f));
            }
            yield return null;
        }
        Write($"frames {Time.frameCount - f0}, time {Time.time - t0:F2}, sum dt {sumDt:F2}, real {Time.realtimeSinceStartup - realStart:F2}");
    }

    // ------------------------------------------------------------------ helpers
    static float Mult(PlayerHealth p) { var s = p.GetComponent<PlayerStats>(); return s != null ? s.Get(Stat.MoveSpeed) : 1f; }

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
        player.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.Euler(0, yaw, 0));
        cc.enabled = true;
    }

    // Walks the NavMesh path a -> b with the CharacterController. null = no path, "" = arrived, else where it got stuck.
    static string Walk(PlayerHealth player, CharacterController cc, Vector3 a, Vector3 b)
    {
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(a, b, NavAgents.Filter(0), path) || path.status != NavMeshPathStatus.PathComplete) return null;
        Teleport(player, a, 0f);
        foreach (var corner in path.corners.Skip(1))
        {
            int still = 0;
            for (int i = 0; i < 3000; i++)
            {
                Vector3 d = corner - player.transform.position;
                d.y = 0f;
                if (d.magnitude < 0.25f) break;
                Vector3 p0 = player.transform.position;
                cc.Move(d.normalized * Mathf.Min(0.2f, d.magnitude) + Vector3.down * 0.3f);
                Vector3 p1 = player.transform.position;
                still = new Vector2(p1.x - p0.x, p1.z - p0.z).magnitude < 0.03f ? still + 1 : 0;
                if (still > 15)
                {
                    var near = Physics.OverlapSphere(p1 + Vector3.up * 0.6f, 0.55f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)
                        .Where(c => c.bounds.max.y > 0.05f).Select(c => c.name).Distinct();
                    return $"{p1.x:F1},{p1.z:F1} [{string.Join(",", near)}]";
                }
            }
        }
        return "";
    }

    static void Shot(Camera cam, string path)
    {
        if (cam == null) return;
        var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 0.3f; }
        Render(cam, path);
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    static void ShotFrom(Vector3 pos, Vector3 lookAt, string path)
    {
        var go = new GameObject("TestShotCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos));
        Render(cam, path);
        Object.Destroy(go);
    }

    static void Render(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        var old = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = old;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
        rt.Release();
    }
}
