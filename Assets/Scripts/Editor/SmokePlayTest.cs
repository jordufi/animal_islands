// Tools > Island > Play Test Smoke: the whole run by the short way in ONE play session on Isla del Faro (target under 90 s).
// Round 1 spawns, a hit and a kill pay what Economy says, a door + its card, a wall buy, no light = no drink / light = drink,
// the boat mission skipped (DebugComplete), free dock, boss with the expected health, boss killed by direct damage,
// extraction -> Victory, and 0 errors in the console. This is the test to run between changes; the long
// play tests (Base, IslaZombie, Feedback, Coop, Phase1, Online) are for behaviour and for before a commit.
// Results in EditorPrefs "SmokePlayTest.Log" / ".Status".
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SmokePlayTest
{
    const string Key = "SmokePlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static int step, errors;
    static float stepTime, realTime, startReal;
    static EnemyHealth victimA, victimB;

    static SmokePlayTest()
    {
        if (EditorPrefs.GetBool(Key + "Running")) Hook();
    }

    static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    [MenuItem("Tools/Island/Play Test Smoke")]
    public static void Run() => RunOn(TestMapSetup.IslaZombieScenePath);

    [MenuItem("Tools/Island/Play Test Smoke (alturas)")]
    public static void RunAlturas() => RunOn(IslaZombieSetup.IslaAlturas.ScenePath);

    [MenuItem("Tools/Island/Play Test Smoke (La Serenisima)")]
    public static void RunVenecia() => RunOn(IslaZombieSetup.Venecia.ScenePath);

    public static void RunOn(string scenePath)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
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

    static void Tick()
    {
        if (!EditorPrefs.GetBool(Key + "Running")) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        try { Step(); }
        catch (System.Exception e) { Write("EXCEPTION: " + e); Finish("failed"); }
    }

    static void Step()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        if (gm.State == GameState.GameOver) { Write($"FAIL player died at step {step}"); Finish("failed"); return; }
        var cards = CardManager.Instance;
        if (cards != null && cards.CurrentOffer != null) { cards.Choose(cards.CurrentOffer[0]); return; }   // a door opened: take the first card
        var player = gm.Player;
        var inter = player.GetComponent<PlayerInteractor>();
        var shooter = player.GetComponent<PlayerShooter>();
        var eco = gm.Economy;
        float t = Time.time - stepTime, rt = Time.realtimeSinceStartup - realTime;

        switch (step)
        {
            case 0:
                startReal = Time.realtimeSinceStartup;
                player.GodMode = true;
                Next();
                break;

            case 1:   // round 1 spawns old men
            {
                var alive = Object.FindObjectsByType<EnemyHealth>().Where(e => !e.IsDead && e.GetComponent<BossAI>() == null).ToArray();
                if (alive.Length < 2) { if (rt > 25f) { Write("FAIL round 1 never spawned 2 old men"); Finish("failed"); } return; }
                Check(RoundManager.Instance.Round == 1, $"round 1 running with {alive.Length} old men");
                victimA = alive[0]; victimB = alive[1];
                Next();
                break;
            }

            case 2:   // a hit pays the hit points, a kill the kill points, a headshot kill the headshot points
            {
                var slot = shooter.Current;
                int expectHit = eco.HitPointsFor(slot.data.fireInterval);
                int p0 = gm.Points;
                var info = EnemyHits.ApplyLocal(victimA, 10f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                Check(!info.killed && gm.Points - p0 == expectHit, $"non-lethal hit with {slot.Name} pays {gm.Points - p0} (expected {expectHit})");
                p0 = gm.Points;
                EnemyHits.ApplyLocal(victimA, 99999f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                int expectKill = Mathf.RoundToInt(eco.killPoints * victimA.PointsMultiplier);
                Check(victimA.IsDead && gm.Points - p0 == expectKill, $"body kill pays {gm.Points - p0} (expected {expectKill})");
                p0 = gm.Points;
                EnemyHits.ApplyLocal(victimB, 99999f, true, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                int expectHead = Mathf.RoundToInt(eco.headshotKillPoints * victimB.PointsMultiplier);
                Check(victimB.IsDead && gm.Points - p0 == expectHead, $"headshot kill pays {gm.Points - p0} (expected {expectHead})");
                gm.AddPoints(100000);
                Next();
                break;
            }

            case 3:   // first door + its card
            {
                if (rt < 0.3f) return;
                var door = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.name.StartsWith("Door1_"));
                if (door == null) { Write("FAIL Door1 not found"); Finish("failed"); return; }
                int z0 = ZoneManager.Instance.OpenCount, p0 = gm.Points;
                door.Interact(inter);
                Check(ZoneManager.Instance.OpenCount > z0 && p0 - gm.Points == 750, $"door 1 paid {p0 - gm.Points} (expected 750), zones {z0} -> {ZoneManager.Instance.OpenCount}");
                Next();
                break;
            }

            case 4:   // wall buy
            {
                if (rt < 0.5f) return;
                var wall = Object.FindObjectsByType<WallWeapon>().FirstOrDefault(w => w.Weapon.id == "lentospeta");
                if (wall == null) { Write("FAIL no Lentos-peta on the wall"); Finish("failed"); return; }
                int p0 = gm.Points;
                wall.Interact(inter);
                Check(shooter.SlotOf(wall.Weapon) != null && p0 - gm.Points == wall.Cost, $"wall buy: {wall.Weapon.displayName} for {p0 - gm.Points} (expected {wall.Cost})");
                Next();
                break;
            }

            case 5:   // no light = no drink, light = drink
            {
                if (rt < 0.3f) return;
                var machine = Object.FindAnyObjectByType<DrinkMachine>();
                var sw = Object.FindAnyObjectByType<PowerSwitch>();
                if (machine == null || sw == null) { Write("FAIL drink machine or power switch missing"); Finish("failed"); return; }
                int p0 = gm.Points;
                machine.Interact(inter);
                Check(gm.Points == p0 && !BoatMission.PowerOn, "no light: the drink machine refuses and charges nothing");
                sw.Interact(inter);
                Check(BoatMission.PowerOn, "the power switch gives the light");
                p0 = gm.Points;
                machine.Interact(inter);
                Check(p0 - gm.Points > 0, $"with light the drink costs {p0 - gm.Points}");
                Next();
                break;
            }

            case 6:   // boat mission skipped, dock free, boss with the expected health
            {
                if (rt < 0.3f) return;
                BoatMission.Instance.DebugComplete();
                var dock = Object.FindObjectsByType<ZoneDoor>().FirstOrDefault(d => d.RequiresMission);
                if (dock == null) { Write("FAIL no dock door"); Finish("failed"); return; }
                int p0 = gm.Points;
                dock.Interact(inter);
                var boss = BossEncounter.Instance.Boss;
                Check(gm.Points == p0 && boss != null, $"dock opens for free after the mission and the boss appears");
                if (boss != null)
                {
                    float expected = Mathf.Max(eco.bossMinHealth, eco.bossHealthInEnemies * RoundManager.Instance.HealthForRound(Mathf.Max(1, RoundManager.Instance.Round)));
                    Check(Mathf.Abs(boss.Health.MaxHealth - expected) < 1f, $"boss health {boss.Health.MaxHealth:F0} (expected {expected:F0})");
                }
                Next();
                break;
            }

            case 7:   // kill the boss by direct damage
            {
                var boss = BossEncounter.Instance.Boss;
                if (boss == null) { Write("FAIL no boss"); Finish("failed"); return; }
                if (!gm.BossDefeated)
                {
                    if (!boss.Health.IsDead) EnemyHits.ApplyLocal(boss.Health, 1000000f, false, HitKind.Gun, player, true, 0f, 0f, player.transform.position);
                    if (rt < 5f && !gm.BossDefeated) return;   // let the death settle
                }
                Check(gm.BossDefeated, "boss defeated");
                Next();
                break;
            }

            case 8:   // extraction -> Victory
            {
                if (rt < 0.5f) return;
                var ext = Object.FindAnyObjectByType<ExtractionPoint>();
                var cc = player.GetComponent<CharacterController>();
                cc.enabled = false;
                player.transform.position = ext.transform.position + Vector3.left * 1.5f;
                cc.enabled = true;
                ext.Interact(inter);
                Check(gm.State == GameState.Victory, $"extraction -> state {gm.State}");
                float took = Time.realtimeSinceStartup - startReal;
                Check(errors == 0, $"{errors} errors in the console");
                Check(took < 90f, $"whole smoke run took {took:F0} s (target under 90)");
                Finish(Log.Contains("FAIL") ? "failed" : "done");
                break;
            }
        }
    }
}
