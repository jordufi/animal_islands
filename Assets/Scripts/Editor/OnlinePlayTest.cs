// Online play test (Tools > Island > Play Test Online). The editor is the HOST; a Windows build of the game
// (Builds/NetTest, made by "Build Online Test Client") is started as the CLIENT with -netbot, so it joins by
// IP and obeys the test's commands (NetTestBot) and reports what it sees. Checks: room (join, animal),
// match start when both loaded, player snapshots both ways, shared pot, old men replicated (count and
// positions), client hits / shots kill on the host and pay the pot, damage reaches the client, the client
// opens a door and chooses the card for both, the box appears for both and the client gets a weapon from it,
// down / revive both ways, Llave Maestra, boss and its death, extraction -> victory for both, back to the
// room, restart, and the client leaving mid-match. Results in EditorPrefs "OnlinePlayTest.Log" / ".Status",
// screenshots Renders/Online_*.png (host and client).
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class OnlinePlayTest
{
    const string Key = "OnlinePlayTest.";
    const string ClientFolder = "Builds/NetTest";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static readonly Dictionary<string, string> reports = new Dictionary<string, string>();
    static bool startedScript;
    static Process client;
    static string Project => Path.GetDirectoryName(Application.dataPath);
    static string ClientExe => Path.Combine(Project, ClientFolder, "AnimalIsland.exe");

    // Windows player for the test client (no zip). ~1-4 min.
    [MenuItem("Tools/Island/Build Online Test Client")]
    public static string BuildClient()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.Combine(Project, ClientFolder));
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ClientExe,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        string result = report.summary.result + " (" + report.summary.totalErrors + " errors, " + Mathf.RoundToInt((float)report.summary.totalTime.TotalSeconds) + " s)";
        EditorPrefs.SetString(Key + "Build", result);
        return result;
    }

    // Same as Run but through Unity Relay (needs internet + Relay enabled for the project): the host gets a
    // join code, the client joins with it, the match starts and both see each other.
    [MenuItem("Tools/Island/Play Test Online (Relay)")]
    public static void RunRelay()
    {
        relay = true; matchScene = "IslaZombie"; matchMap = null;
        Run();
    }

    static bool relay;
    static string matchScene = "IslaZombie", matchMap;

    // Same as RunDirect on "Isla del Faro (alturas)" (Phase 3 heights map). Rebuild the client first: it carries the scene.
    [MenuItem("Tools/Island/Play Test Online (alturas)")]
    public static void RunDirectAlturas()
    {
        relay = false; matchScene = "IslaZombie"; matchMap = null;
        matchScene = IslaZombieSetup.IslaAlturas.scene; matchMap = "isla-del-faro-alturas";
        Run();
    }

    [MenuItem("Tools/Island/Play Test Online")]
    public static void RunDirect()
    {
        relay = false; matchScene = "IslaZombie"; matchMap = null;
        Run();
    }

    // La Serenisima from a client (own script: the Isla del Faro steps don't apply). Rebuild the client first.
    [MenuItem("Tools/Island/Play Test Online (La Serenisima)")]
    public static void RunDirectVenecia()
    {
        relay = false; matchScene = "Venecia"; matchMap = "la-serenisima";
        Run();
    }

    // Fiestas de Villajubilada from a client: act 1, the first encierro seen from both machines, light, traca.
    [MenuItem("Tools/Island/Play Test Online (Villajubilada)")]
    public static void RunDirectVillajubilada()
    {
        relay = false; matchScene = "Villajubilada"; matchMap = "villajubilada";
        Run();
    }

    static bool IsVenecia => matchScene == "Venecia";
    static bool IsVilla => matchScene == "Villajubilada";

    static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorPrefs.SetString(Key + "Log", "");
        if (!File.Exists(ClientExe)) { EditorPrefs.SetString(Key + "Log", "FAIL no client build: run OnlinePlayTest.BuildClient() first\n"); EditorPrefs.SetString(Key + "Status", "failed"); return; }
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        script.Clear();
        reports.Clear();
        startedScript = false;
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
        try { if (client != null && !client.HasExited) client.Kill(); } catch { }
        client = null;
        if (NetSession.Instance != null) NetSession.Instance.Leave();
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void Tick()
    {
        if (!EditorPrefs.GetBool(Key + "Running")) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        ZoneDoor.IgnoreRoundLimit = true;   // opens several doors in one round (the one-door-per-round rule is checked in SmokePlayTest)
        try
        {
            if (!startedScript) { startedScript = true; script.Push(IsVenecia ? VeneciaScript() : IsVilla ? VillaScript() : Script()); }
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
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    static IEnumerator Until(System.Func<bool> cond, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!cond() && Time.realtimeSinceStartup < end) yield return null;
    }

    // ---------------------------------------------------------------- talking to the client bot
    static NetSession Ns => NetSession.Instance;

    static void Cmd(string line)
    {
        if (Ns != null) Ns.ToPlayer(1, NetBuf.Make(NetMsg.TestCmd).Str(line));
    }

    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static void Tp(Vector3 p, float yaw = 0f) => Cmd("tp " + F(p.x) + " " + F(p.y) + " " + F(p.z) + " " + F(yaw));

    static Dictionary<string, string> last = new Dictionary<string, string>();

    // asks the client for a report and waits for it (last = its key / values)
    static IEnumerator Ask(string tag)
    {
        reports.Remove(tag);
        Cmd("report " + tag);
        yield return Until(() => reports.ContainsKey(tag), 6f);
        last = new Dictionary<string, string>();
        if (!reports.TryGetValue(tag, out var text)) { Write("   (no report '" + tag + "' from the client)"); yield break; }
        foreach (var kv in text.Split(';'))
        {
            int eq = kv.IndexOf('=');
            if (eq > 0) last[kv.Substring(0, eq)] = kv.Substring(eq + 1);
        }
    }

    static string V(string key) => last.TryGetValue(key, out var v) ? v : "?";
    static int I(string key) => int.TryParse(V(key), out var i) ? i : -999;

    static void HostTp(PlayerHealth p, Vector3 pos)
    {
        var cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = pos + Vector3.up * 0.05f;
        if (cc != null) cc.enabled = true;
    }

    static Vector3 Near(Vector3 p, float radius)
    {
        for (int i = 0; i < 20; i++)
        {
            var o = Random.insideUnitCircle.normalized * radius;
            if (NavAgents.Sample(p + new Vector3(o.x, 0f, o.y), 0.6f, out var s)) return s;
        }
        return NavAgents.Sample(p, 3f, out var t) ? t : p;
    }

    // a walkable spot `radius` m from p with a clear line of sight to it (chest height)
    static Vector3 ClearSpot(Vector3 p, float radius)
    {
        int mask = (1 << 0) | (1 << 11) | (1 << NavAgents.PropLayer);
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f * Mathf.Deg2Rad;
            var c = p + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            if (!NavAgents.Sample(c, 0.5f, out var s)) continue;
            if (!Physics.Linecast(s + Vector3.up * 1.4f, p + Vector3.up * 1.1f, mask, QueryTriggerInteraction.Ignore)) return s;
        }
        return Near(p, radius);
    }

    static string ClientShot(string name) => Path.Combine(Project, "Renders", name).Replace('\\', '/');

    // ---------------------------------------------------------------- La Serenisima
    static Vector3 VCell(float c, float r, float y) => new Vector3((c - 34f) * 2f, y, (24f - r) * 2f);

    static Vector3 ClientPos()
    {
        var pos = V("pos").Split(',');
        return pos.Length == 3 ? new Vector3(float.Parse(pos[0], CultureInfo.InvariantCulture), float.Parse(pos[1], CultureInfo.InvariantCulture), float.Parse(pos[2], CultureInfo.InvariantCulture)) : new Vector3(0f, -99f, 0f);
    }

    static string PaintOf(Component c)
    {
        if (c == null) return "?";
        return c.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null && m.name.StartsWith("VN_MaskPaint_")).Select(m => m.name.Substring(13)).FirstOrDefault() ?? "?";
    }

    static void Move(PlayerHealth p, Vector3 pos)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false; p.transform.position = pos + Vector3.up * 0.1f; cc.enabled = true;
    }

    static IEnumerator VillaScript()
    {
        yield return Wait(1.5f);
        NetSession.On(NetMsg.TestReport, (s, r) =>
        {
            string t = r.Str();
            int bar = t.IndexOf('|');
            if (bar > 0) reports[t.Substring(0, bar)] = t.Substring(bar + 1);
        });
        var ns = NetSession.Ensure();
        ns.HostDirect(NetSession.DefaultPort, true);
        yield return Wait(0.5f);
        Check(ns.Active && ns.IsHost, "host opened a room (direct IP)");
        var mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm != null) mm.OpenRoom();
        string log = Path.Combine(Project, ClientFolder, "client.log");
        client = Process.Start(new ProcessStartInfo(ClientExe,
            "-netjoin 127.0.0.1 -netanimal Toucan -netbot -netprofile testclient -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile \"" + log + "\"") { UseShellExecute = false });
        yield return Until(() => ns.Roster.Count >= 2, 60f);
        Check(ns.Roster.Count == 2, "the client joined the room");
        if (ns.Roster.Count < 2) yield break;

        ns.SetSettings(Difficulty.Normal, false, matchMap);
        ns.StartMatch(matchScene);
        yield return Until(() => NetGame.Instance != null && NetGame.Instance.Ready, 90f);
        var net = NetGame.Instance;
        Check(net != null && net.Ready && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Villajubilada", "both loaded Villajubilada: match started");
        if (net == null) yield break;
        var gm = GameManager.Instance;
        var me = gm.Player;
        var inter = me.GetComponent<PlayerInteractor>();
        me.GodMode = true;
        Cmd("god on");
        yield return Wait(2f);
        var enc = Encierro.Instance;
        var bm = BoatMission.Instance;
        var gate = Object.FindAnyObjectByType<EncierroGate>();
        var rm = RoundManager.Instance;
        yield return Ask("start");
        Check(V("scene") == "Villajubilada" && I("players") == 2 && V("ready") == "True", "client: in Villajubilada, 2 players, ready");
        Check(enc != null && enc.ActOne && V("enc") == "act1,idle,0" && V("gate") == "shut" && V("dark") == "0", "act 1 on both: the Porton shut, still evening (client " + V("enc") + ", gate " + V("gate") + ")");
        if (enc == null || bm == null) yield break;
        yield return Until(() => rm.Round >= 1, 30f);

        // the host opens the two start zones; the client finds the lighter in the second one
        gm.AddPoints(50000, PointSource.Debug);
        foreach (string dn in new[] { "Door1_", "Door2_" })
        {
            var d = Object.FindObjectsByType<ZoneDoor>().First(x => x.name.StartsWith(dn));
            d.Interact(inter);
            yield return Wait(0.5f);
            var cm = CardManager.Instance;
            if (cm != null && cm.CurrentOffer != null) cm.Choose(cm.CurrentOffer[0]);
            yield return Wait(0.5f);
        }
        yield return Ask("doors");
        Check(V("open") == ZoneManager.Instance.OpenCount.ToString() && ZoneManager.Instance.OpenCount == 3, "both start zones open on host and client (" + V("open") + ")");
        var lighter = Object.FindObjectsByType<MissionTask>().FirstOrDefault(t => t.name.StartsWith("Mechero_") && t.Here);
        Check(lighter != null && lighter.name == "Mechero_Corrales", "the lighter lies in the zone opened second (" + (lighter != null ? lighter.name : "none") + ")");
        if (lighter != null)
        {
            Tp(lighter.transform.position);
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => bm.Shovel, 5f);
            yield return Ask("lighter");
            Check(bm.Shovel && V("lighter") == "1", "the client picks up the lighter: host and client have it");
        }
        var bar = ZoneManager.Instance.Zones.First(z => z.displayName == "El Bar");
        var box = Object.FindObjectsByType<PowerSwitch>().First(s => s.Zone == bar);
        Tp(box.InteractPoint);
        yield return Wait(0.8f);
        Cmd("use");
        yield return Until(() => bm.ZoneLit(bar), 5f);
        yield return Wait(1f);
        yield return Ask("bar");
        Check(bm.ZoneLit(bar) && I("glow") >= 1, "the client switches on the bar's fuse box: lit on the host, its lights on at the client (" + V("glow") + ")");

        // the first encierro: the client stays in La Parada, the host waits in a doorway up the street
        Tp(new Vector3(-4f, 0f, -46f));          // cell (26, 53)
        Move(me, enc.Route[enc.GateIndex + 28] + new Vector3(4f, 0f, 0f));   // the doorway (31, 42)... or beside it: god mode anyway
        foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(999999f, false, true);
        enc.ForceNext = true;
        rm.DebugEndRound();
        yield return Until(() => enc.Warning || enc.Running, 40f);
        yield return Wait(1f);
        yield return Ask("rocket");
        Check(enc.FirstRun && !gate.Blocked && V("gate") == "open" && V("enc").StartsWith("act1,"), "rocket on both: the Porton is open at the client too (" + V("enc") + ")");
        yield return Until(() => !enc.ActOne, 60f);
        yield return Wait(2.5f);
        yield return Ask("act2");
        Vector3 cp = ClientPos();
        Check(!enc.ActOne && V("enc").StartsWith("act2,") && V("gate") == "shut", "act 1 over on both machines, the Porton shut at the client (" + V("enc") + ")");
        Check(!enc.InActOneArea(cp) && I("enchits") >= 1, "the client who stayed behind was dragged through by its own herd (z " + F(cp.z) + ", hits " + V("enchits") + ")");
        Check(V("open") == ZoneManager.Instance.OpenCount.ToString(), "same open zones on both after the start zones closed (" + V("open") + ")");
        Cmd("shot " + ClientShot("Online_villajubilada_encierro_client.png"));
        Tp(new Vector3(-8f, 0f, 4f));            // a doorway of the Calle Mayor
        Move(me, new Vector3(10f, 0f, 4f));      // another one
        yield return Until(() => !enc.Running, 150f);
        yield return Wait(12f);
        yield return Ask("after");
        Check(enc.Done == 1 && V("enc") == "act2,idle,1" && V("dark") == "1", "the herd finished on both and it is night at the client (" + V("enc") + ", dark " + V("dark") + ")");

        // the client lights a traca: the host charges and kills, the client sees it burn
        var trap = Object.FindObjectsByType<TracaTrap>().OrderBy(t => (t.transform.position - new Vector3(16f, 0f, 2f)).sqrMagnitude).First();
        for (int i = 0; i < 2; i++)
        {
            var e = rm.SpawnEnemy(null, (trap.From + trap.To) / 2f);
            var ag = e != null ? e.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            if (ag != null && ag.isOnNavMesh) ag.Warp(Vector3.Lerp(trap.From, trap.To, 0.4f + 0.2f * i));
        }
        Tp(trap.InteractPoint + Vector3.forward * 0.6f);
        yield return Wait(1.5f);
        int pot = gm.Points;
        Cmd("use");
        yield return Until(() => trap.Burning, 5f);
        yield return Wait(0.6f);
        yield return Ask("traca");
        Check(trap.Burning && V("tracas") == "1", "the client lights the traca: it burns on the host and at the client (" + V("tracas") + ")");
        yield return Until(() => !trap.Burning, 10f);
        Check(trap.Kills >= 2 && gm.Points > pot - gm.Economy.tracaCost, "the host charged " + gm.Economy.tracaCost + " and the traca's " + trap.Kills + " kills paid the pot");
        CoopPlayTest.ShotAll("Renders/Online_villajubilada_host.png");
        Cmd("quit");
        yield return Wait(1f);
    }

    static IEnumerator VeneciaScript()
    {
        yield return Wait(1.5f);
        NetSession.On(NetMsg.TestReport, (s, r) =>
        {
            string t = r.Str();
            int bar = t.IndexOf('|');
            if (bar > 0) reports[t.Substring(0, bar)] = t.Substring(bar + 1);
        });
        var ns = NetSession.Ensure();
        ns.HostDirect(NetSession.DefaultPort, true);
        yield return Wait(0.5f);
        Check(ns.Active && ns.IsHost, "host opened a room (direct IP)");
        var mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm != null) mm.OpenRoom();
        string log = Path.Combine(Project, ClientFolder, "client.log");
        client = Process.Start(new ProcessStartInfo(ClientExe,
            "-netjoin 127.0.0.1 -netanimal Toucan -netbot -netprofile testclient -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile \"" + log + "\"") { UseShellExecute = false });
        yield return Until(() => ns.Roster.Count >= 2, 60f);
        Check(ns.Roster.Count == 2, "the client joined the room");
        if (ns.Roster.Count < 2) yield break;

        ns.SetSettings(Difficulty.Normal, false, matchMap);
        ns.StartMatch(matchScene);
        yield return Until(() => NetGame.Instance != null && NetGame.Instance.Ready, 90f);
        var net = NetGame.Instance;
        Check(net != null && net.Ready && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Venecia", "both loaded La Serenisima: match started");
        if (net == null) yield break;
        var gm = GameManager.Instance;
        var me = gm.Player;
        var proxy = net.PlayerAt(1);
        me.GodMode = true;
        Cmd("god on");
        yield return Wait(2f);
        yield return Ask("start");
        Check(V("scene") == "Venecia" && I("players") == 2 && V("ready") == "True", "client: in Venecia, 2 players, ready");
        var bm = BoatMission.Instance;
        Check(bm != null && bm.HasTasks && bm.GondolierSpot >= 0 && V("tasks").EndsWith("@" + bm.GondolierSpot), "the host's golden gondolier spot reaches the client (host " + (bm != null ? bm.GondolierSpot : -9) + ", client " + V("tasks") + ")");
        var fences = Object.FindObjectsByType<ZoneFence>(FindObjectsInactive.Include);
        Check(V("fences") == fences.Count(f => f.Up) + "/" + fences.Length, "closed barrios fenced on both (host " + fences.Count(f => f.Up) + "/" + fences.Length + ", client " + V("fences") + ")");

        // masks: an old man in a bauta, same paint on both machines; the client's hits break it on the host
        var rm = RoundManager.Instance;
        yield return Until(() => rm.Round >= 1, 30f);
        foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(999999f, false, true);
        var bauta = AssetDatabase.LoadAssetAtPath<EnemyVariant>("Assets/Data/Enemies/AnselmoBauta.asset");
        var masked = rm.SpawnEnemy(bauta, Near(me.transform.position, 6f));
        yield return Wait(3f);
        var mh = masked != null ? masked.GetComponent<EnemyHealth>() : null;
        Check(mh != null && mh.Masked, "host: an old man in a bauta mask (" + (mh != null ? "id " + mh.NetId : "none") + ")");
        if (mh != null)
        {
            string hostPaint = PaintOf(masked.GetComponentInChildren<MaskLook>());
            yield return Ask("mask");
            Check(hostPaint != "?" && V("m" + mh.NetId) == hostPaint, "the mask has the same paint on both machines (host " + hostPaint + ", client " + V("m" + mh.NetId) + ")");
            Tp(Near(masked.transform.position, 2f));
            yield return Wait(0.8f);
            float hp0 = mh.Current;
            for (int i = 0; i < 3 && mh != null && mh.Masked; i++) { Cmd("hit 50"); yield return Wait(0.6f); }
            Check(mh != null && !mh.IsDead && !mh.Masked, "client hits break the bauta on the host (alive, health " + F(hp0) + " -> " + (mh != null ? F(mh.Current) : "-") + ")");
            yield return Wait(1f);
            yield return Ask("mask2");
            Check(V("m" + mh.NetId) == "?", "the broken mask is gone on the client too (" + V("m" + mh.NetId) + ")");
            mh.TakeDamage(999999f, false, true);
        }

        // bridge 1 opened by the client: the Mercato fence goes on both
        gm.AddPoints(20000, PointSource.Debug);
        var door = Object.FindObjectsByType<ZoneDoor>().OrderBy(d => d.Cost).FirstOrDefault();
        var merc = fences.FirstOrDefault(f => f.Zone != null && f.Zone.displayName.StartsWith("Mercato"));
        if (door != null)
        {
            Tp(Near(door.InteractPoint + (me.transform.position - door.InteractPoint).normalized * 1.2f, 0.3f));
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => door == null, 3f);
            yield return Until(() => CardManager.Instance.CurrentOffer != null, 2f);
            if (CardManager.Instance.CurrentOffer != null) { Cmd("card 1"); yield return Until(() => CardManager.Instance.CurrentOffer == null, 3f); }
            yield return Wait(0.8f);
            yield return Ask("door");
            Check(door == null && merc != null && !merc.Up && V("fences") == fences.Count(f => f.Up) + "/" + fences.Length,
                "client opens bridge 1: the Mercato fence is gone on both (client fences " + V("fences") + ")");
        }
        // the rest open from the host (debug), except the vaporetto
        foreach (var d in Object.FindObjectsByType<ZoneDoor>().ToList())
            if (!d.RequiresMission) d.DebugOpen();
        yield return Wait(1f);
        if (CardManager.Instance.CurrentOffer != null) { Cmd("card 1"); yield return Wait(1f); }

        // jobs done by the client: shovel, Giuseppe, golden gondolier, bell
        var tasks = Object.FindObjectsByType<MissionTask>();
        foreach (var kind in new[] { MissionTask.Kind.Shovel, MissionTask.Kind.Grave, MissionTask.Kind.Gondolier })
        {
            var t = tasks.FirstOrDefault(x => x.TaskKind == kind && x.Here);
            if (t == null) { Write("FAIL no task " + kind); continue; }
            int pot = gm.Points;
            Vector3 stand = Near(t.InteractPoint, 1.1f);
            Tp(stand, Quaternion.LookRotation(Flat(t.InteractPoint - stand) + Vector3.forward * 0.001f).eulerAngles.y);
            yield return Wait(0.9f);
            Cmd("use");
            System.Func<bool> done = () => kind == MissionTask.Kind.Shovel ? bm.Shovel : kind == MissionTask.Kind.Grave ? bm.Giuseppe : bm.Gondolier;
            yield return Until(done, 3f);
            Check(done(), "client: " + kind + " done on the host" + (kind == MissionTask.Kind.Shovel ? " (paid " + (pot - gm.Points) + ")" : ""));
        }
        var climb = Object.FindAnyObjectByType<TowerClimb>();
        var bell = Object.FindAnyObjectByType<PowerSwitch>();
        if (climb != null && bell != null)
        {
            Tp(climb.Entry + Vector3.up * 0.1f);
            yield return Wait(3f);
            yield return Ask("climb");
            float y = ClientPos().y;
            Check(y > 15f, "client walks into the campanile door: carried up to the belfry (y " + F(y) + ")");
            Cmd("use");
            yield return Until(() => bm.Power, 3f);
            Check(bm.Power, "client rings the bell: the host has the light on");
        }
        yield return Wait(1f);
        yield return Ask("tasks");
        Check(V("tasks").StartsWith("S1G1O1B1"), "client sees the 4 jobs done (" + V("tasks") + ")");
        Cmd("shot " + ClientShot("Online_venecia_tasks_client.png"));
        yield return Wait(22f);   // the campanile throws him off after 20 s

        // gondola: the client pays at the Ferrovia stop, rides on its own machine, lands at another stop
        // feedback 1: only some stops have a gondola each round (the same ones on both machines), to a random open stop
        var from = Object.FindObjectsByType<GondolaStop>().OrderByDescending(g => g.Zone != null && g.Zone.displayName.StartsWith("Ferrovia")).FirstOrDefault(g => g.CanRide);
        if (from == null) Write("FAIL no gondola stop to ride from");
        if (from != null)
        {
            Vector3 dest = from.Destination;
            int pot = gm.Points;
            Tp(Near(from.InteractPoint, 1f));
            yield return Wait(0.9f);
            Cmd("use");
            yield return Wait(1.5f);
            yield return Ask("ride");
            Check(gm.Points == pot - gm.Economy.gondolaCost && I("gondolasBusy") >= 1, "client takes the gondola: the host charges " + (pot - gm.Points) + ", the client rides (" + V("gondolasBusy") + " busy)");
            Cmd("shot " + ClientShot("Online_venecia_gondola_client.png"));
            yield return Until(() => Vector3.Distance(Flat(proxy.transform.position), Flat(dest)) < 1.5f, 16f);
            yield return Wait(0.5f);
            yield return Ask("landed");
            float off = Vector3.Distance(Flat(proxy.transform.position), Flat(dest));
            Check(off < 1.5f && I("gondolasBusy") == 0, "client lands at the next stop, the host sees it there (" + F(off) + " m)");
        }

        // acqua alta: both flood, the client swims a canal, the tide goes down without hurting him
        var tide = AcquaAlta.Instance;
        var rescue = CanalRescue.Instance;
        if (tide != null && rescue != null)
        {
            Cmd("god off");
            // a real flood round (every 5): the client works the tide out from the round number (ForceNext is host-only)
            float until = Time.realtimeSinceStartup + 150f;
            while (!tide.Flooding && Time.realtimeSinceStartup < until)
            {
                foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(999999f, false, true);
                if (!rm.InIntermission) rm.DebugEndRound();
                yield return Wait(1f);
            }
            Write("   round " + rm.Round + ", flooding " + tide.Flooding);
            yield return Wait(2f);
            yield return Ask("tide");
            Check(tide.Flooding && V("tide").StartsWith("up,swim"), "acqua alta on both (client " + V("tide") + ")");
            Tp(VCell(29f, 34f, 0.3f));
            yield return Wait(2.5f);
            yield return Ask("swim");
            Vector3 cp = ClientPos();
            int hpSwim = I("hp");
            Check(rescue.InWater(cp) && cp.y > tide.SwimY - 0.3f, "the client swims in the canal (y " + F(cp.y) + ")");
            Cmd("shot " + ClientShot("Online_venecia_swim_client.png"));
            foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(999999f, false, true);
            rm.DebugEndRound();
            yield return Until(() => !tide.Flooding, 20f);
            yield return Wait(2.5f);
            yield return Ask("dry");
            cp = ClientPos();
            Check(V("tide").StartsWith("down") && !rescue.InWater(cp) && I("hp") >= hpSwim, "tide down: the client is back on a quay, no damage (hp " + V("hp") + "/" + hpSwim + ", " + V("tide") + ")");
            Cmd("god on");
        }
        CoopPlayTest.ShotAll("Renders/Online_venecia_host.png");
        Cmd("quit");
        yield return Wait(1f);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // ---------------------------------------------------------------- the test
    static IEnumerator Script()
    {
        yield return Wait(1.5f);
        NetSession.On(NetMsg.TestReport, (s, r) =>
        {
            string t = r.Str();
            int bar = t.IndexOf('|');
            if (bar > 0) reports[t.Substring(0, bar)] = t.Substring(bar + 1);
        });

        // ---------------------------------------------------------- 1. room
        var ns = NetSession.Ensure();
        if (relay)
        {
            ns.HostRelay();
            yield return Until(() => ns.Active || (!ns.Busy && ns.Status.Length > 0 && !ns.Status.StartsWith("Conectando")), 40f);
            Check(ns.Active && ns.UsingRelay && ns.JoinCode.Length >= 6, "Unity Relay: room created, code " + ns.JoinCode + " (" + ns.Status.Replace("\n", " ") + ")");
            if (!ns.Active) yield break;
        }
        else
        {
            ns.HostDirect(NetSession.DefaultPort, true);
            yield return Wait(0.5f);
            Check(ns.Active && ns.IsHost, "host opened a room (direct IP " + ns.JoinCode + ")");
        }
        var mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm != null) mm.OpenRoom();
        string log = Path.Combine(Project, ClientFolder, "client.log");
        string join = relay ? ns.JoinCode : "127.0.0.1";
        client = Process.Start(new ProcessStartInfo(ClientExe,
            "-netjoin " + join + " -netanimal Toucan -netbot -netprofile testclient -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile \"" + log + "\"") { UseShellExecute = false });
        Write("   client started (log " + log + ")");
        yield return Until(() => ns.Roster.Count >= 2, 60f);
        Check(ns.Roster.Count == 2, "the client joined the room (" + ns.Roster.Count + " players)");
        if (ns.Roster.Count < 2) yield break;
        Check(ns.Roster[1].animal == "Toucan", "J2 came with its animal (" + ns.Roster[1].animal + ")");
        yield return Wait(1f);
        Cmd("animal Panda");
        yield return Until(() => ns.Roster[1].animal == "Panda", 5f);
        Check(ns.Roster[1].animal == "Panda", "J2 changes animal in the room -> host sees " + ns.Roster[1].animal);
        Cmd("animal Toucan");
        yield return Wait(0.8f);
        CoopPlayTest.ShotAll("Renders/Online_room_host.png");
        Cmd("shot " + ClientShot("Online_room_client.png"));
        yield return Ask("room");
        Check(V("scene") == "MainMenu" && V("active") == "True" && I("index") == 1, "client is in the room as J2 (" + V("scene") + ", index " + V("index") + ")");

        // ---------------------------------------------------------- 2. start
        ns.SetSettings(Difficulty.Normal, false, matchMap ?? ns.MapId);
        ns.StartMatch(matchScene);
        Write("   match scene " + matchScene);
        yield return Until(() => NetGame.Instance != null && NetGame.Instance.Ready, 90f);
        var net = NetGame.Instance;
        Check(net != null && net.Ready, "both loaded Isla del Faro: match started");
        if (net == null) yield break;
        var gm = GameManager.Instance;
        var me = gm.Player;
        var proxy = net.PlayerAt(1);
        Check(gm.PlayerCount == 2 && proxy != null && PlayerSlot.IsProxy(proxy) && !PlayerSlot.IsProxy(me), "host has its player + J2's proxy");
        me.GodMode = true;
        yield return Wait(1.5f);
        yield return Ask("start");
        Check(I("players") == 2 && I("index") == 1 && V("ready") == "True", "client: 2 players, it is J2, ready (" + V("players") + ")");
        float age = Time.unscaledTime - proxy.GetComponent<NetPlayer>().LastSnapTime;
        Check(age < 0.5f, "host receives J2's snapshots (last " + F(age) + " s ago)");
        Check(V("remote1").Contains("age=0.0") || V("remote1").Contains("age=0.1") || V("remote1").Contains("age=0.2"), "client receives J1's snapshots (" + V("remote1") + ")");

        // movement
        Vector3 target = Near(me.transform.position, 3f);
        Tp(target, 90f);
        yield return Wait(0.8f);
        float moved = Vector3.Distance(proxy.transform.position, target);
        Check(moved < 0.8f, "J2 moves on the client -> its proxy follows on the host (" + F(moved) + " m off)");
        HostTp(me, Near(target, 2f));
        yield return Wait(0.8f);
        yield return Ask("move");
        var r1 = V("remote1").Split(',');
        float dx = r1.Length > 1 ? float.Parse(r1[0], CultureInfo.InvariantCulture) - me.transform.position.x : 99f;
        float dz = r1.Length > 1 ? float.Parse(r1[1], CultureInfo.InvariantCulture) - me.transform.position.z : 99f;
        Check(new Vector2(dx, dz).magnitude < 0.8f, "J1 moves on the host -> the client sees it (" + F(new Vector2(dx, dz).magnitude) + " m off)");

        // shared pot
        gm.AddPoints(5000, PointSource.Debug);
        yield return Wait(0.6f);
        yield return Ask("pot");
        Check(I("points") == gm.Points, "shared pot: client shows the host's conchas (" + V("points") + " / " + gm.Points + ")");

        // ---------------------------------------------------------- 3. old men
        var rm = RoundManager.Instance;
        yield return Until(() => rm.Round >= 1 && Object.FindObjectsByType<EnemyHealth>().Count(e => !e.IsDead) >= 3, 40f);
        yield return Wait(2f);
        int hostEnemies = Object.FindObjectsByType<EnemyHealth>().Count(e => !e.IsDead);
        yield return Ask("enemies");
        Check(I("round") == rm.Round && Mathf.Abs(I("enemies") - hostEnemies) <= 1, "old men replicated: round " + V("round") + "/" + rm.Round + ", " + V("enemies") + " on the client / " + hostEnemies + " on the host");
        float worst = 0f;
        int compared = 0;
        foreach (var kv in last.Where(k => k.Key.StartsWith("e")))
        {
            if (!int.TryParse(kv.Key.Substring(1), out int id)) continue;
            var e = Object.FindObjectsByType<EnemyHealth>().FirstOrDefault(x => x.NetId == id);
            if (e == null) continue;
            var xz = kv.Value.Split(',');
            float d = new Vector2(float.Parse(xz[0], CultureInfo.InvariantCulture) - e.transform.position.x, float.Parse(xz[1], CultureInfo.InvariantCulture) - e.transform.position.z).magnitude;
            worst = Mathf.Max(worst, d);
            compared++;
        }
        Check(compared > 0 && worst < 2f, "old men positions match (" + compared + " compared, worst " + F(worst) + " m: ~0.1 s behind)");
        Cmd("shot " + ClientShot("Online_enemies_client.png"));

        // client hit kills on the host and pays the pot
        int kills0 = gm.Kills, pts0 = gm.Points;
        Cmd("hit 99999 head");
        yield return Until(() => gm.Kills > kills0, 3f);
        Check(gm.Kills == kills0 + 1 && gm.Points >= pts0 + gm.Economy.headshotKillPoints, "client headshot -> host kills him and pays +" + (gm.Points - pts0));
        var j2 = PlayerSlot.Of(proxy);
        Check(j2.Kills >= 1 && j2.Headshots >= 1, "the kill counts for J2 (" + j2.Kills + " kills, " + j2.Headshots + " headshots)");

        // a real shot from the client's gun
        var victim = Object.FindObjectsByType<EnemyAI>().Where(a => !a.GetComponent<EnemyHealth>().IsDead && !a.IsEmerging && !a.InTunnel)
            .OrderBy(a => (a.transform.position - me.transform.position).sqrMagnitude).FirstOrDefault();
        if (victim != null)
        {
            var vh = victim.GetComponent<EnemyHealth>();
            float hp0 = vh.Current;
            int k0 = gm.Kills;
            victim.Stun(6f);   // stands still while we line up
            yield return Wait(0.3f);
            Vector3 v = victim.transform.position;
            Vector3 stand = ClearSpot(v, 4.5f);
            Tp(stand, Quaternion.LookRotation(v - stand).eulerAngles.y);
            yield return Wait(0.5f);
            Cmd("shoot 4 " + vh.NetId);
            yield return Wait(2.5f);
            yield return Ask("shots");
            Check(gm.Kills > k0 || vh == null || vh.Current < hp0, "client fires its pistol at an old man -> the host's old man takes it (hp " + F(hp0) + " -> " + (vh != null ? F(vh.Current) : "dead") + ", shots/hits/fails " + V("shots") + ")");
        }
        else Write("   (no old man to shoot at)");

        // damage reaches the client
        yield return Ask("hp0");
        int hpBefore = I("hp");
        proxy.TakeDamage(30);
        yield return Wait(0.8f);
        yield return Ask("hp1");
        Check(I("hp") <= hpBefore - 25 && I("hp") > 0, "an old man's hit on J2's proxy hurts the client (" + hpBefore + " -> " + V("hp") + ")");
        Check(Mathf.Abs(proxy.CurrentHealth - I("hp")) <= 15, "host mirrors J2's health (" + proxy.CurrentHealth + ")");

        // ---------------------------------------------------------- 3b. night: senderistas, chanclas, the dawn cooler
        Cmd("god on");
        int thrown = 0;
        System.Action<EnemyAI> onThrow = _ => thrown++;
        EnemyAI.Thrown += onThrow;
        rm.ForceNextNight = true;
        rm.DebugEndRound();
        yield return Until(() => rm.IsNight && Object.FindObjectsByType<EnemyHealth>().Count(e => !e.IsDead) > 0, 30f);
        Check(rm.IsNight, "night round starts on the host");
        yield return Wait(2f);
        yield return Ask("night");
        Check(V("night") == "True" && I("enemies") > 0, "client: night round with " + V("enemies") + " senderistas");
        Cmd("shot " + ClientShot("Online_night_client.png"));
        // J1 (god mode) stands ~8 m from a senderista so he throws
        float until = Time.realtimeSinceStartup + 30f;
        while (thrown == 0 && Time.realtimeSinceStartup < until)
        {
            var s = Object.FindObjectsByType<EnemyAI>().FirstOrDefault(x => !x.GetComponent<EnemyHealth>().IsDead && !x.IsEmerging);
            if (s != null) HostTp(me, ClearSpot(s.transform.position, 8f));
            yield return Wait(3f);
        }
        yield return Wait(1.2f);
        yield return Ask("chancla");
        Check(thrown > 0 && I("chanclas") > 0, "chanclas thrown on the host (" + thrown + ") fly on the client too (" + V("chanclas") + ")");
        EnemyAI.Thrown -= onThrow;
        Cmd("empty");
        rm.DebugEndRound();   // dawn: the Nevera de munición
        yield return Until(() => Object.FindObjectsByType<AmmoPickup>().Length > 0, 8f);
        var cooler = Object.FindAnyObjectByType<AmmoPickup>();
        yield return Wait(0.4f);
        yield return Ask("dawn");
        Check(cooler != null && I("coolers") >= 1 && V("night") == "False", "dawn: the Nevera de munición appears for the client too (host " + (cooler != null) + ", client " + V("coolers") + "; toasts: " + V("toasts") + ")");
        if (cooler != null)
        {
            Tp(Near(cooler.transform.position, 0.3f));
            yield return Until(() => cooler == null, 3f);
            yield return Wait(0.8f);
            yield return Ask("cooler");
            Check(cooler == null && I("coolers") == 0 && I("reserve") > 0, "client walks into it: taken for everyone, its ammo refilled (reserve " + V("reserve") + ", host cooler gone " + (cooler == null) + ", client coolers " + V("coolers") + ")");
        }

        // ---------------------------------------------------------- 4. door + card chosen by the client
        var door = Object.FindObjectsByType<ZoneDoor>().OrderBy(d => d.Cost).FirstOrDefault();
        int zones0 = ZoneManager.Instance.OpenCount;
        if (door != null)
        {
            Vector3 dp = door.InteractPoint;
            Vector3 side = Near(dp + (me.transform.position - dp).normalized * 1.2f, 0.3f);
            Tp(side);
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => door == null, 3f);
            Check(door == null && ZoneManager.Instance.OpenCount > zones0, "client opens the first door: host opens the zone (" + zones0 + " -> " + ZoneManager.Instance.OpenCount + " zones)");
            var cm = CardManager.Instance;
            yield return Until(() => cm.CurrentOffer != null, 2f);
            Check(cm.CurrentOffer != null && NetGame.IndexOf(cm.Chooser) == 1 && !cm.PausesGame, "J2 opened it -> J2 chooses the card, the game doesn't pause");
            yield return Wait(0.6f);
            yield return Ask("offer");
            Check(I("doors") == 1 && I("zonesOpen") == ZoneManager.Instance.OpenCount && V("offer") == "3@J2", "client: door gone, zones " + V("zonesOpen") + ", offer " + V("offer"));
            CoopPlayTest.ShotAll("Renders/Online_cards_host.png");
            Cmd("shot " + ClientShot("Online_cards_client.png"));
            yield return Wait(0.5f);
            Cmd("card 1");
            yield return Until(() => cm.CurrentOffer == null, 3f);
            Check(cm.CurrentOffer == null && cm.Taken.Count == 1, "client picks a card -> applied on the host (" + (cm.Taken.Count > 0 ? cm.Taken[0].title : "-") + ")");
            yield return Wait(0.5f);
            yield return Ask("card");
            Check(I("cards") == 1 && V("offer") == "none" && V("doorCost") == F(RunModifiers.Get(Stat.DoorCost)), "client applied the same card (cards " + V("cards") + ", door cost x" + V("doorCost") + ")");
        }
        else Write("FAIL no door found");

        // ---------------------------------------------------------- 5. box
        var box = Object.FindAnyObjectByType<MysteryBox>();
        var parking = Object.FindObjectsByType<ZoneDoor>().Where(d => d.Cost <= 1100).OrderBy(d => d.Cost).FirstOrDefault();
        if (parking != null) parking.DebugOpen();
        yield return Wait(1.2f);
        if (box != null && box.StateName == "Ready")
        {
            yield return Ask("box");
            Vector3 bp = box.transform.position;
            Check(V("box").StartsWith("Ready@") && V("box").Contains(bp.x.ToString("0.0", CultureInfo.InvariantCulture)), "the box appears in the same place for the client (" + V("box") + ")");
            gm.AddPoints(3000, PointSource.Debug);
            Vector3 stand = Near(bp, 1.4f);
            Tp(stand, Quaternion.LookRotation(bp - stand).eulerAngles.y);
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => box.StateName == "Offering", 6f);
            Check(box.StateName == "Offering", "client pays the box -> it rolls and offers " + (box.Offered != null ? box.Offered.name : "?"));
            string offered = box.Offered != null ? box.Offered.name : "";
            yield return Wait(0.3f);
            Cmd("use");
            yield return Until(() => box.StateName == "Gone", 3f);
            yield return Wait(0.8f);
            yield return Ask("weapon");
            Check(V("weapons").Contains(offered) && offered.Length > 0, "client takes the weapon from the box (" + V("weapons") + ")");
            Check(proxy.GetComponent<PlayerShooter>().HasWeapon(box.Offered != null ? box.Offered : NetGame.FindAsset<WeaponData>(offered)), "the host's proxy of J2 carries it too");
            CoopPlayTest.ShotAll("Renders/Online_box_host.png");
            Cmd("shot " + ClientShot("Online_box_client.png"));
        }
        else Write("FAIL the box didn't appear after opening " + (parking != null ? parking.name : "a 1000 door") + " (" + (box != null ? box.StateName : "no box") + ")");

        // ---------------------------------------------------------- 6. down / revive
        HostTp(me, Near(proxy.transform.position, 1.2f));
        Cmd("god off");
        proxy.TakeDamage(999);
        yield return Until(() => proxy.IsDowned, 3f);
        Check(proxy.IsDowned && proxy.InLastStand, "J2 goes down on the client -> host sees the last stand");
        yield return Ask("down");
        Check(V("downed") == "True", "client: downed (" + V("weapons") + ")");
        var hostInteractor = me.GetComponent<PlayerInteractor>();
        HostTp(me, Near(proxy.transform.position, 1.2f));
        yield return Wait(0.3f);
        hostInteractor.HoldOverride = true;
        hostInteractor.BeginRevive(proxy.GetComponent<ReviveTarget>());
        float t0 = Time.realtimeSinceStartup;
        yield return Until(() => !proxy.IsDowned, 9f);
        hostInteractor.HoldOverride = false;
        Check(!proxy.IsDowned, "J1 holds □ next to J2 -> J2 gets up (" + F(Time.realtimeSinceStartup - t0) + " s)");
        yield return Ask("revived");
        Check(V("downed") == "False", "client: back on its feet with " + V("hp") + " hp");
        Cmd("god on");

        me.GodMode = false;
        me.TakeDamage(999);
        yield return Until(() => me.IsDowned, 2f);
        Check(me.IsDowned && me.InLastStand, "J1 goes down (last stand)");
        Tp(Near(me.transform.position, 1.2f));
        yield return Wait(0.6f);
        Cmd("revive on");
        t0 = Time.realtimeSinceStartup;
        yield return Until(() => !me.IsDowned, 9f);
        Cmd("revive off");
        Check(!me.IsDowned, "J2 holds □ next to J1 -> J1 gets up (" + F(Time.realtimeSinceStartup - t0) + " s)");
        me.GodMode = true;

        // bleed-out, back next round
        Cmd("god off");
        yield return Wait(0.3f);
        proxy.TakeDamage(999);
        yield return Until(() => proxy.IsDowned, 3f);
        float bleed = gm.Coop != null ? gm.Coop.bleedOutTime : 30f;
        t0 = Time.realtimeSinceStartup;
        yield return Until(() => proxy.IsDead, bleed + 8f);
        Check(proxy.IsDead, "nobody revives J2: bleeds out after " + F(Time.realtimeSinceStartup - t0) + " s (host sees it)");
        yield return Ask("bled");
        Check(V("dead") == "True" && gm.State == GameState.Playing, "client: bled out, the host keeps playing");
        rm.DebugEndRound();
        yield return Until(() => !proxy.IsDead, 30f);
        Check(!proxy.IsDead, "next round J2 comes back to the island");
        yield return Wait(0.8f);
        yield return Ask("back");
        Check(V("dead") == "False" && V("weapons") == "Pistol,-", "client: back with the pistol only (" + V("weapons") + ")");
        Cmd("god on");

        // ---------------------------------------------------------- 7. key, boat mission (light, petrol, radio, call), dock
        foreach (var d in Object.FindObjectsByType<ZoneDoor>()) if (!d.RequiresMission) d.DebugOpen();   // all zones except the dock
        yield return Wait(1f);
        var key = MasterKey.Instance;
        if (key != null && key.Holder == null && !key.Used)
        {
            Vector3 kp = key.transform.position;
            Tp(Near(kp, 1f));
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => key.Holder != null, 3f);
            Check(key.Holder != null && NetGame.IndexOf(key.Holder) == 1, "client picks up the Llave Maestra -> host knows J2 has it");
            yield return Ask("key");
            Check(V("key") == "J2", "client: key in its pocket (" + V("key") + ")");
        }
        else Write("   (no free key on this map / run)");
        var mission = BoatMission.Instance;
        if (mission != null && mission.HasMission)
        {
            // the client gives the light and picks the radio; the host picks the petrol; the client calls the boat
            var sw = Object.FindAnyObjectByType<PowerSwitch>();
            if (sw != null)
            {
                Tp(Near(sw.InteractPoint, 1.2f));
                yield return Wait(0.8f);
                Cmd("use");
                yield return Until(() => mission.Power, 3f);
            }
            var radio = Object.FindObjectsByType<MissionItem>().FirstOrDefault(i => i.ItemKind == MissionItem.Kind.Radio);
            if (radio != null)
            {
                Tp(Near(radio.transform.position, 1f));
                yield return Wait(0.8f);
                Cmd("use");
                yield return Until(() => mission.Radio, 3f);
            }
            var petrol = Object.FindObjectsByType<MissionItem>().FirstOrDefault(i => i.ItemKind == MissionItem.Kind.Gasolina);
            if (petrol != null) petrol.Interact(gm.Player.GetComponent<PlayerInteractor>());
            yield return Wait(0.6f);
            yield return Ask("quest");
            Check(mission.Power && mission.Radio && mission.Gas && V("quest") == "P1G1R1C0D0", "client gives the light and picks the radio, host picks the petrol: both see it (client " + V("quest") + ")");
            var st = Object.FindAnyObjectByType<CallStation>();
            if (st != null)
            {
                Tp(Near(st.InteractPoint, 1.2f));
                yield return Wait(0.8f);
                Cmd("use");
                yield return Until(() => mission.Called, 3f);
            }
            Check(mission.Called && key != null && !key.Used, $"client calls the boat (arrives after round {mission.DefenseUntilRound}); the key is not spent with both pieces");
            float boatDeadline = Time.realtimeSinceStartup + 60f;
            while (!mission.Complete && Time.realtimeSinceStartup < boatDeadline) { RoundManager.Instance.DebugEndRound(); yield return Wait(0.3f); }
            yield return Wait(0.6f);
            yield return Ask("quest");
            Check(mission.Complete && V("quest") == "P1G1R1C1D1", "boat at the dock for both (client " + V("quest") + ")");
            var dock = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.RequiresMission);
            if (dock != null)
            {
                Vector3 dp = dock.InteractPoint;
                Tp(Near(dp, 1.3f));
                yield return Wait(0.8f);
                int pot = gm.Points;
                Cmd("use");
                yield return Until(() => dock == null, 3f);
                Check(dock == null && gm.Points == pot, "client opens the dock for free once the boat is there");
                var cm = CardManager.Instance;
                yield return Until(() => cm.CurrentOffer != null, 2f);
                if (cm.CurrentOffer != null) { Cmd("card 0"); yield return Until(() => cm.CurrentOffer == null, 3f); }
            }
        }
        else Write("   (no boat mission on this map)");

        // ---------------------------------------------------------- 7b. easter egg: records, karaoke, Golden Disc
        var egg = EasterEgg.Instance;
        var records = Object.FindObjectsByType<DiscPickup>().ToList();
        foreach (var d in records)
        {
            if (d == null) continue;
            Tp(Near(d.transform.position, 0.8f));
            yield return Wait(0.8f);
            Cmd("use");
            var dd = d;
            yield return Until(() => dd == null, 2.5f);
        }
        yield return Wait(0.5f);
        yield return Ask("records");
        Check(records.Count >= 3 && egg != null && egg.DiscsFound == records.Count && I("discs") == egg.DiscsFound, "client picks up the Julio records (host " + (egg != null ? egg.DiscsFound : 0) + ", client " + V("discs") + ")");
        var karaoke = Object.FindAnyObjectByType<Karaoke>();
        Carryable gold = null;
        if (karaoke != null && egg != null)
        {
            Tp(Near(karaoke.transform.position, 1.5f));
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => karaoke.IsSinging, 2f);
            yield return Wait(1f);
            yield return Ask("singing");
            Check(karaoke.IsSinging && V("singing") == "True", "client sings at the karaoke (both see it)");
            yield return Until(() => egg.Completed, 10f);
            gold = Object.FindAnyObjectByType<Carryable>();
            yield return Wait(0.6f);
            yield return Ask("gold");
            Check(egg.Completed && gold != null && V("eggDone") == "True" && V("gold") == "floor", "song finished: the Golden Disc appears for both (" + V("gold") + ")");
        }
        if (gold != null)
        {
            Tp(Near(gold.transform.position, 0.8f));
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => gold.IsCarried, 3f);
            var pc = proxy.GetComponent<PlayerCarry>();
            Check(pc.Carried == gold, "client picks up the Golden Disc: the host sees J2 carrying it");
            yield return Ask("carry");
            Check(V("carry") == "True", "client: hands busy with the disc");
            Cmd("god off");
            proxy.TakeDamage(5);
            yield return Until(() => !gold.IsCarried, 3f);
            Check(!gold.IsCarried && pc.Carried == null, "J2 is hit and drops the disc (host sees it on the floor)");
            yield return Wait(0.5f);
            yield return Ask("drop");
            Check(V("carry") == "False" && V("gold") == "floor", "client: disc dropped");
            Cmd("god on");
            Tp(Near(gold.transform.position, 0.8f));
            yield return Wait(0.8f);
            Cmd("use");
            yield return Until(() => gold.IsCarried, 3f);
        }
        var enc = BossEncounter.Instance;
        yield return Until(() => enc != null && enc.Boss != null, 3f);
        if (enc != null && enc.Boss != null)
        {
            yield return Wait(1f);
            yield return Ask("boss");
            Check(V("boss") == "alive", "Don Anselmo appears for the client too (" + V("boss") + ")");
            enc.Boss.Health.TakeDamage(enc.Boss.Health.Current + 1f, false);
            yield return Wait(1.5f);
            yield return Ask("bossdead");
            Check(V("bossDefeated") == "True", "boss defeated on both (" + V("boss") + ")");
            var exit = Object.FindAnyObjectByType<ExtractionPoint>();
            if (exit != null)
            {
                Vector3 ep = exit.InteractPoint;
                HostTp(me, Near(ep, 2f));
                Tp(Near(ep, 1.2f));
                yield return Wait(1f);
                Cmd("use");
                yield return Until(() => gm.State == GameState.Victory, 3f);
                Check(gm.State == GameState.Victory, "client boards the boat with everyone near -> victory");
                if (gold != null) Check(gm.ComputeScore().itemMultiplier >= 2f, "J2 got the Golden Disc out: score ×" + gm.ComputeScore().itemMultiplier);
                yield return Wait(1f);
                yield return Ask("victory");
                Check(V("state") == "Victory" && I("kills") == gm.Kills, "client shows the victory screen with the host's numbers (kills " + V("kills") + "/" + gm.Kills + ")");
                CoopPlayTest.ShotAll("Renders/Online_victory_host.png");
                Cmd("shot " + ClientShot("Online_victory_client.png"));
                yield return Wait(1f);
            }
        }
        else Write("FAIL no boss after opening the dock");

        // ---------------------------------------------------------- 8. back to the room, restart, leave
        gm.ReturnToMenu();
        yield return Wait(3f);
        yield return Ask("lobby");
        Check(V("scene") == "MainMenu" && V("active") == "True" && ns.Active && ns.Roster.Count == 2, "host takes everyone back to the room (client: " + V("scene") + ")");
        ns.StartMatch(matchScene);
        yield return Until(() => NetGame.Instance != null && NetGame.Instance.Ready, 90f);
        Check(NetGame.Instance != null && NetGame.Instance.Ready, "second match from the room starts");
        yield return Wait(1f);
        GameManager.Instance.Restart();
        yield return Wait(2f);
        yield return Until(() => NetGame.Instance != null && NetGame.Instance.Ready, 90f);
        yield return Ask("restart");
        Check(NetGame.Instance != null && NetGame.Instance.Ready && V("scene") == matchScene && V("ready") == "True" && I("doors") == 0, "restart for everyone (client " + V("scene") + ", doors " + V("doors") + ")");

        // everyone down = game over for both
        var gm2 = GameManager.Instance;
        var px2 = NetGame.Instance.PlayerAt(1);
        gm2.Player.GodMode = false;
        Cmd("god off");
        yield return Wait(0.5f);
        px2.TakeDamage(999);
        yield return Until(() => px2.IsDowned, 3f);
        gm2.Player.TakeDamage(999);
        yield return Until(() => gm2.State == GameState.GameOver, 6f);
        Check(gm2.State == GameState.GameOver, "both down -> game over on the host");
        yield return Wait(1f);
        yield return Ask("over");
        Check(V("state") == "GameOver", "client shows the game over screen too");
        Cmd("shot " + ClientShot("Online_gameover_client.png"));
        yield return Wait(0.8f);
        gm2.Restart();
        yield return Wait(2f);
        yield return Until(() => NetGame.Instance != null && NetGame.Instance.Ready, 90f);
        Check(NetGame.Instance != null && NetGame.Instance.Ready, "\"Reintentar\" after the game over restarts both");
        yield return Wait(1f);
        Cmd("quit");
        yield return Wait(4f);
        var gone = NetGame.Instance != null ? NetGame.Instance.PlayerAt(1) : null;
        Check(gone != null && gone.IsDead && PlayerSlot.Of(gone).Gone && GameManager.Instance.State == GameState.Playing, "client quits mid-match: its player leaves, the host keeps playing");
        try { if (client != null && !client.HasExited) Write("   (client still running)"); } catch { }
    }
}
