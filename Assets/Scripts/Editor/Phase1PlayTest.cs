// Play test of Phase 1 (Tools > Island > Play Test Phase 1), Isla del Faro: no light = no drinks / Microondas,
// the dock stays closed until the boat mission is done, the call station's blockers, the boat arrives when the round
// AFTER the call ends, the power-ups (Siesta fulminante, 2x1 en conchas, Paella para todos, Nevera), the drop caps,
// the boss's neighbours pay kills only, and the HUD objective / power-up lines. Results in EditorPrefs
// "Phase1PlayTest.Log" / ".Status", screenshot Renders/Phase1_hud.png.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Phase1PlayTest
{
    const string Key = "Phase1PlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static readonly List<(int amount, PointSource source)> deltas = new List<(int, PointSource)>();
    static PowerUpData data;
    static float ammoChance, otherChance;

    [MenuItem("Tools/Island/Play Test Phase 1")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(TestMapSetup.IslaZombieScenePath, OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        script.Clear();
        data = null;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Write(string line) => EditorPrefs.SetString(Key + "Log", Log + line + "\n");
    static void Check(bool ok, string what) => Write((ok ? "OK   " : "FAIL ") + what);

    static void RestoreData()
    {
        if (data == null) return;
        data.ammoDropChance = ammoChance;
        data.otherDropChance = otherChance;
        data = null;
    }

    static void Finish(string status)
    {
        RestoreData();
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

    static IEnumerator Until(System.Func<bool> cond, float timeout)
    {
        float end = Time.time + timeout;
        while (!cond() && Time.time < end) yield return null;
    }

    static EnemyHealth Spawn(Vector3 near, float hp)
    {
        var ai = RoundManager.Instance.SpawnEnemy(null, near);
        if (ai == null) return null;
        var h = ai.GetComponent<EnemyHealth>();
        h.Init(hp);
        return h;
    }

    static int Sum(PointSource s, int from) => deltas.Skip(from).Where(d => d.source == s).Sum(d => d.amount);

    static string HudText(string name)
    {
        var t = Object.FindObjectsByType<Text>().FirstOrDefault(x => x.name == name);
        return t != null ? t.text : "(no " + name + ")";
    }

    // ------------------------------------------------------------------ the test
    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var rm = RoundManager.Instance;
        var player = gm.Player;
        var interactor = player.GetComponent<PlayerInteractor>();
        var shooter = player.GetComponent<PlayerShooter>();
        var drinks = player.GetComponent<PlayerDrinks>();
        var m = BoatMission.Instance;
        var pm = PowerUpManager.Instance;
        rm.SpawningPaused = true;
        player.GodMode = true;
        player.SetIgnored(100000f);
        deltas.Clear();
        gm.PointsDelta += (a, s) => deltas.Add((a, s));
        gm.AddPoints(100000, PointSource.Debug);
        yield return null;
        Check(m != null && m.HasSwitch && m.HasMission && pm != null && pm.Data != null, "BoatMission (light + mission) and PowerUpManager with its data are in the scene");
        if (m == null || pm == null) yield break;

        // ---------------------------------------------------------- 1. no light: drinks and the Microondas are off
        foreach (var d in Object.FindObjectsByType<ZoneDoor>()) if (!d.RequiresMission) d.DebugOpen();
        yield return Wait(0.5f);
        var machine = Object.FindAnyObjectByType<DrinkMachine>();
        var pap = Object.FindAnyObjectByType<PackAPunch>();
        int pts = gm.Points;
        machine.Interact(interactor);
        pap.Interact(interactor);
        Check(!m.Power && !machine.CanInteract(interactor) && !pap.CanInteract(interactor) && gm.Points == pts && !drinks.Has(machine.Drink) && !shooter.Current.upgraded,
            $"no light: drink and Microondas refuse and charge nothing ('{machine.Prompt(interactor)}' / '{pap.Prompt(interactor)}')");
        Write("   HUD objective at the start: '" + HudText("Objective") + "'");

        // ---------------------------------------------------------- 2. the dock is closed before the mission
        var dock = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.RequiresMission);
        int open0 = ZoneManager.Instance.OpenCount;
        pts = gm.Points;
        dock.Interact(interactor);
        yield return null;
        Check(dock != null && !dock.CanInteract(interactor) && ZoneManager.Instance.OpenCount == open0 && gm.Points == pts,
            $"dock closed before the boat mission, charges nothing (prompt '{(dock != null ? dock.Prompt(interactor) : "-")}')");

        // ---------------------------------------------------------- 3. call station blockers, light on, pieces
        var st = Object.FindAnyObjectByType<CallStation>();
        string b0 = m.CallBlocker(interactor);
        Object.FindAnyObjectByType<PowerSwitch>().Interact(interactor);
        string b1 = m.CallBlocker(interactor);
        var items = Object.FindObjectsByType<MissionItem>();
        items.First(i => i.ItemKind == MissionItem.Kind.Gasolina).Interact(interactor);
        string b2 = m.CallBlocker(interactor);
        Check(b0.Contains("luz") && b1.Contains("GASOLINA y la RADIO") && b2.Contains("RADIO") && b2.Contains("LLAVE") && !st.CanInteract(interactor),
            $"emisora blockers: '{b0}' -> '{b1}' -> '{b2}'");

        // light: drinks and the Microondas work and charge
        pts = gm.Points;
        machine.Interact(interactor);
        int drinkPaid = pts - gm.Points;
        pts = gm.Points;
        pap.Interact(interactor);
        int papPaid = pts - gm.Points;
        Check(m.Power && drinks.Has(machine.Drink) && drinkPaid > 0 && shooter.Current.upgraded && papPaid > 0,
            $"light on: drink bought ({drinkPaid}) and the pistol microwaved ({papPaid})");

        items.First(i => i.ItemKind == MissionItem.Kind.Radio).Interact(interactor);
        yield return null;
        Check(m.Gas && m.Radio && st.CanInteract(interactor), "petrol + radio picked: the emisora can call ('" + st.Prompt(interactor) + "')");

        // ---------------------------------------------------------- 4. call mid-round: the boat comes when the NEXT round ends
        rm.SpawningPaused = false;
        rm.DebugEndRound();   // skip the first intermission
        yield return Until(() => !rm.InIntermission && rm.Round >= 1, 15f);
        int callRound = rm.Round;
        st.Interact(interactor);
        Check(m.Called && m.DefenseUntilRound == callRound + 1, $"called during round {callRound}: the boat comes after round {m.DefenseUntilRound}");
        rm.DebugEndRound();
        yield return Until(() => rm.InIntermission, 15f);
        Check(rm.InIntermission && !m.Complete, $"round {callRound} over: the boat is not there yet");
        rm.DebugEndRound();
        yield return Until(() => !rm.InIntermission && rm.Round == callRound + 1, 15f);
        rm.DebugEndRound();
        yield return Until(() => rm.InIntermission, 15f);
        yield return null;
        Check(m.Complete, $"round {callRound + 1} over: the boat is at the dock");
        Check(dock != null && dock.CanInteract(interactor), "the dock can be opened now ('" + (dock != null ? dock.Prompt(interactor) : "-") + "')");
        Write("   HUD objective now: '" + HudText("Objective") + "'");
        rm.SpawningPaused = true;
        foreach (var e in Object.FindObjectsByType<EnemyHealth>()) if (!e.IsDead) e.TakeDamage(e.Current + 1f, false);
        yield return Wait(0.5f);

        // ---------------------------------------------------------- 5. Siesta fulminante
        Vector3 near = player.transform.position + player.transform.forward * 5f;
        var tough = Spawn(near, 5000f);
        pm.Apply(PowerUpKind.InstaKill, shooter);
        var info = EnemyHits.ApplyLocal(tough, 50f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
        Check(PowerUpManager.InstaKill && info.killed && pm.InstaKillLeft > 25f, $"Siesta fulminante: one pistol shot kills a 5000 hp old man ({pm.InstaKillLeft:F0} s left)");
        yield return null;
        Write("   HUD power-ups: '" + HudText("Buffs") + "'");
        pm.NetSetBuffs(0f, 0f);
        var tough2 = Spawn(near, 5000f);
        info = EnemyHits.ApplyLocal(tough2, 50f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
        Check(!PowerUpManager.InstaKill && !info.killed, "after it runs out the same shot does not kill");
        tough2.TakeDamage(tough2.Current + 1f, false);

        // ---------------------------------------------------------- 6. 2x1 en conchas
        pm.Apply(PowerUpKind.DoublePoints, shooter);
        var victim = Spawn(near, 100f);
        int mark = deltas.Count;
        EnemyHits.ApplyLocal(victim, 500f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
        int killPay = Sum(PointSource.Kill, mark);
        int expected = Mathf.RoundToInt(gm.Economy.killPoints * victim.PointsMultiplier) * 2;
        pts = gm.Points;
        gm.TrySpend(100, "test");
        Check(PowerUpManager.DoublePoints && killPay == expected && pts - gm.Points == 100, $"2x1: a body kill pays {killPay} (expected {expected}); spending 100 still costs 100");
        pm.NetSetBuffs(0f, 0f);

        // ---------------------------------------------------------- 7. Paella para todos
        var many = new List<EnemyHealth>();
        for (int i = 0; i < 5; i++) { var e = Spawn(near, 3000f); if (e != null) many.Add(e); }
        int before = Object.FindObjectsByType<AmmoPickup>().Length;
        mark = deltas.Count;
        pm.Apply(PowerUpKind.Nuke, shooter);
        yield return null;
        int bonus = Sum(PointSource.Bonus, mark);
        Check(many.Count == 5 && many.All(e => e == null || e.IsDead) && bonus == pm.Data.nukePoints && Object.FindObjectsByType<AmmoPickup>().Length == before,
            $"Paella: {many.Count} old men dead, +{bonus} once (expected {pm.Data.nukePoints}), no power-ups dropped by it");
        yield return Wait(0.5f);

        // ---------------------------------------------------------- 8. drop caps (every kill drops while testing)
        foreach (var p in Object.FindObjectsByType<AmmoPickup>()) Object.Destroy(p.gameObject);
        yield return null;
        data = pm.Data;
        ammoChance = data.ammoDropChance; otherChance = data.otherDropChance;
        data.ammoDropChance = 1f; data.otherDropChance = 0f;
        rm.SpawningPaused = false;
        rm.DebugEndRound();
        yield return Until(() => !rm.InIntermission, 15f);
        rm.ClearQueue();
        rm.SpawningPaused = true;
        for (int i = 0; i < 6; i++) { var e = Spawn(near, 10f); if (e != null) e.TakeDamage(100f, false); yield return null; }
        int onGround = Object.FindObjectsByType<AmmoPickup>().Length;
        Check(onGround == Mathf.Min(pm.Data.maxActive, pm.Data.maxPerRound), $"6 kills with a 100 % drop chance: {onGround} power-ups on the ground (max active {pm.Data.maxActive})");
        foreach (var p in Object.FindObjectsByType<AmmoPickup>()) Object.Destroy(p.gameObject);
        yield return null;
        for (int i = 0; i < 6; i++) { var e = Spawn(near, 10f); if (e != null) e.TakeDamage(100f, false); yield return null; }
        int more = Object.FindObjectsByType<AmmoPickup>().Length;
        Check(onGround + more == pm.Data.maxPerRound, $"same round, 6 more kills: {more} more (round cap {pm.Data.maxPerRound})");
        RestoreData();

        // Nevera de munición picked up by walking into it
        foreach (var p in Object.FindObjectsByType<AmmoPickup>()) Object.Destroy(p.gameObject);
        yield return null;
        shooter.Current.reserve = 0;
        pm.Spawn(player.transform.position + Vector3.up * 0.6f, PowerUpKind.MaxAmmo);
        yield return Wait(0.5f);
        Check(shooter.Current.reserve > 0, $"walking into a Nevera de munición refills the reserve ({shooter.Current.reserve})");

        // ---------------------------------------------------------- 9. the boss's neighbours pay kills only
        var minion = Spawn(near, 1000f);
        minion.PaysHits = false;
        mark = deltas.Count;
        EnemyHits.ApplyLocal(minion, 50f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
        int hitPay = Sum(PointSource.Hit, mark);
        EnemyHits.ApplyLocal(minion, 5000f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
        int minionKill = Sum(PointSource.Kill, mark);
        var normal = Spawn(near, 1000f);
        int mark2 = deltas.Count;
        EnemyHits.ApplyLocal(normal, 50f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
        int normalHit = Sum(PointSource.Hit, mark2);
        normal.TakeDamage(normal.Current + 1f, false);
        Check(hitPay == 0 && minionKill > 0 && normalHit > 0, $"boss neighbour: hit pays {hitPay}, kill pays {minionKill} (a normal old man's hit pays {normalHit})");

        // ---------------------------------------------------------- 10. the dock opens for free, the boss comes
        pts = gm.Points;
        dock.Interact(interactor);
        // the card offer pauses the game (Time.time stops): pick by frames, not by game time
        var cm = CardManager.Instance;
        for (int f = 0; f < 10 && cm.CurrentOffer == null; f++) yield return null;
        if (cm.CurrentOffer != null) cm.Choose(cm.CurrentOffer[0]);
        for (int f = 0; f < 5; f++) yield return null;
        yield return Wait(0.5f);
        Check(dock == null && gm.Points == pts && BossEncounter.Instance.Boss != null, "dock opened for free and Don Anselmo appeared");
        if (BossEncounter.Instance.Boss != null)
        {
            var bossHp = BossEncounter.Instance.Boss.Health;
            pm.Apply(PowerUpKind.InstaKill, shooter);
            info = EnemyHits.ApplyLocal(bossHp, 50f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
            Check(!info.killed && !bossHp.IsDead, "Siesta fulminante does not kill the boss in one hit");
            pm.NetSetBuffs(0f, 0f);
        }

        // ---------------------------------------------------------- 11. HUD picture
        pm.Apply(PowerUpKind.DoublePoints, shooter);
        yield return Wait(0.3f);
        Write("   HUD objective: '" + HudText("Objective") + "', power-ups: '" + HudText("Buffs") + "'");
        Check(HudText("Objective").Contains("PARA QUE VENGA EL BARCO") && HudText("Objective").Contains("Acaba con Don Anselmo") &&HudText("Buffs").Contains("x2"), "HUD shows the objective and the active power-up");
        Shot("Renders/Phase1_hud.png");
        yield return null;
    }

    // Main camera + the overlay canvases into a PNG (canvases switched to the camera for the shot).
    static void Shot(string path)
    {
        var cam = GameManager.Instance.CameraRig.GetComponent<Camera>();
        var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
        rt.Release();
    }
}
