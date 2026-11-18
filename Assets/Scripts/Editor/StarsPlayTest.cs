using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// User feedback 1, phase 5 (Docs/FEEDBACK_PROPUESTAS.md): weapons with stars 0-3. The starting pistol is 1 star, a wall
// weapon 0, the box rolls 1-3 (60 / 30 / 10 %), La Lijadora del Taller adds one for 1000 (max 3, not after the
// Microondas), the Microondas is a class of its own, and a bullet loses damage for EACH old man it goes through.
// Tools > Island > Play Test Estrellas. EditorPrefs StarsPlayTest.Log / .Status, pictures Renders/Stars_*.png.
public static class StarsPlayTest
{
    const string Key = "StarsPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();

    [MenuItem("Tools/Island/Play Test Estrellas")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene("Assets/Scenes/IslaZombie.unity", OpenSceneMode.Single);
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
        ZoneDoor.IgnoreRoundLimit = false;
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
        if (GameManager.Instance == null || GameManager.Instance.Player == null) return;
        ZoneDoor.IgnoreRoundLimit = true;
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

    static int Hit(List<float> got) => got.Count(x => x > 0f);
    static bool Near(float a, float b) => Mathf.Abs(a - b) <= Mathf.Max(0.5f, 0.02f * Mathf.Abs(b));

    // six frozen old men in a row on the crosshair line; one pistol shot; the damage each one took
    static IEnumerator PierceShot(PlayerShooter shooter, List<float> got)
    {
        got.Clear();
        var cam = GameManager.Instance.CameraRig.GetComponent<Camera>();
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float skip = Vector3.Dot(shooter.transform.position + Vector3.up * 0.8f - ray.origin, ray.direction);
        if (skip > 0f) ray.origin += ray.direction * skip;
        var hits = new List<EnemyHealth>();
        foreach (float d in new[] { 2f, 2.5f, 3f, 3.5f, 4f, 4.5f })
        {
            var p = ray.origin + ray.direction * d;
            var ai = RoundManager.Instance.SpawnEnemy(null, p);
            if (ai == null) continue;
            ai.enabled = false;
            var agent = ai.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            foreach (var a in ai.GetComponentsInChildren<Animator>()) a.enabled = false;
            ai.transform.position = p - Vector3.up * 0.6f;   // the ray at the belly (never the head)
            ai.transform.rotation = Quaternion.LookRotation(-new Vector3(ray.direction.x, 0f, ray.direction.z));
            var h = ai.GetComponent<EnemyHealth>();
            h.Init(100000f);
            hits.Add(h);
        }
        Physics.SyncTransforms();
        yield return null;
        var dmg = new float[hits.Count];
        for (int i = 0; i < hits.Count; i++) { int k = i; hits[i].Damaged += (e, info) => dmg[k] += info.amount; }
        shooter.Current.magazine = Mathf.Max(shooter.Current.magazine, 2);
        while (!shooter.TestFire()) yield return null;
        yield return null;
        got.AddRange(dmg);
        Write("     damage per old man: " + string.Join(" / ", dmg.Select(x => x.ToString("0"))) + $" ({hits.Count} placed)");
        foreach (var h in hits) if (h != null) Object.Destroy(h.gameObject);
        yield return null;
    }

    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var player = gm.Player;
        var inter = player.GetComponent<PlayerInteractor>();
        var shooter = player.GetComponent<PlayerShooter>();
        var eco = gm.Economy;
        var rm = RoundManager.Instance;
        player.GodMode = true;
        yield return Wait(1f);
        gm.AddPoints(200000);
        foreach (var e in Object.FindObjectsByType<EnemyHealth>()) Object.Destroy(e.gameObject);

        // 1. the starting pistol is 1 star, its name says so
        var pistol = shooter.Current;
        Check(pistol.stars == eco.startingWeaponStars && pistol.Name.EndsWith("★") && !pistol.Name.EndsWith("★★"), $"starting pistol: '{pistol.Name}' ({pistol.stars} star)");

        // 2. a bullet loses damage for each old man it goes through
        var got = new List<float>();
        rm.DebugEndRound();
        yield return Wait(0.3f);
        foreach (var e in Object.FindObjectsByType<EnemyHealth>()) Object.Destroy(e.gameObject);
        yield return null;
        yield return PierceShot(shooter, got);
        float baseDmg = pistol.data.damage * eco.StarDamage(1);
        Check(Hit(got) == 2, $"1 star: the bullet goes through {Hit(got) - 1} old man (want {eco.StarPierceCount(1)})");
        Check(got.Count >= 2 && Near(got[0], baseDmg) && Near(got[1], baseDmg * eco.StarPierceKeep(1)),
            $"1 star: first old man {(got.Count > 0 ? got[0] : -1):0}, the one behind {(got.Count > 1 ? got[1] : -1):0} (want {baseDmg:0} / {baseDmg * eco.StarPierceKeep(1):0})");
        Check(got.Count >= 2 && got[1] < got[0], "going through an old man never adds damage");

        // 3. La Lijadora: +1 star for 1000, twice, then refused at 3
        var lij = Object.FindAnyObjectByType<Lijadora>();
        var pap = Object.FindAnyObjectByType<PackAPunch>();
        Check(lij != null, "La Lijadora del Taller is on the map" + (lij != null && pap != null ? $" ({Vector3.Distance(lij.transform.position, pap.transform.position):0} m from the Microondas)" : ""));
        if (lij == null) yield break;
        Write("     lijadora at " + lij.transform.position + ", prompt '" + lij.Prompt(inter) + "'");
        int pts = gm.Points;
        lij.Interact(inter);
        Check(pistol.stars == 2 && pts - gm.Points == eco.lijadoraCost, $"lijadora: '{pistol.Name}' for {pts - gm.Points}");
        lij.Interact(inter);
        pts = gm.Points;
        lij.Interact(inter);
        Check(pistol.stars == 3 && pts == gm.Points && !lij.CanInteract(inter), $"3 stars is the top: '{pistol.Name}', refused ('{lij.Prompt(inter)}')");
        Feedback4PlayTest.ShotHud("Renders/Stars_hud_3.png");

        yield return PierceShot(shooter, got);
        baseDmg = pistol.data.damage * eco.StarDamage(3);
        Check(Hit(got) == 4, $"3 stars: the bullet goes through {Hit(got) - 1} old men (want {eco.StarPierceCount(3)})");
        Check(got.Count >= 2 && Near(got[0], baseDmg) && Near(got[1], baseDmg * eco.StarPierceKeep(3)),
            $"3 stars: {(got.Count > 0 ? got[0] : -1):0} / {(got.Count > 1 ? got[1] : -1):0} (want {baseDmg:0} / {baseDmg * eco.StarPierceKeep(3):0})");

        // 4. the Microondas: its own class, better than 3 stars, and no more stars after it
        if (BoatMission.Instance != null) BoatMission.Instance.TurnPowerOn();
        pts = gm.Points;
        pap.Interact(inter);
        Check(pistol.upgraded && pts - gm.Points == eco.packAPunchCost, $"Microondas: '{pistol.Name}'");
        Check(!lij.CanInteract(inter), $"the lijadora does nothing to a Microondas weapon ('{lij.Prompt(inter)}')");
        yield return PierceShot(shooter, got);
        baseDmg = pistol.data.damage * pistol.data.upgradeDamageMultiplier;
        Check(Hit(got) == 5, $"Microondas: the bullet goes through {Hit(got) - 1} old men (want {eco.microwavePierceCount})");
        Check(got.Count >= 2 && Near(got[0], baseDmg) && Near(got[1], baseDmg * eco.microwavePierceKeep) && got[0] > pistol.data.damage * eco.StarDamage(3),
            $"Microondas: {(got.Count > 0 ? got[0] : -1):0} / {(got.Count > 1 ? got[1] : -1):0} (more than 3 stars, keeps {eco.microwavePierceKeep:P0} through)");

        // 5. a wall weapon is 0 stars; the lijadora sands it up
        var wall = Object.FindObjectsByType<WallWeapon>().FirstOrDefault(w => w.Weapon.id == "lentospeta");
        wall.Interact(inter);
        var lentos = shooter.SlotOf(wall.Weapon);
        Check(lentos != null && lentos.stars == 0 && lentos.Name == wall.Weapon.displayName, $"wall weapon: '{(lentos != null ? lentos.Name : "-")}' (0 stars)");
        lij.Interact(inter);
        Check(lentos != null && lentos.stars == 1, $"lijadora on the wall weapon: '{(lentos != null ? lentos.Name : "-")}'");

        // 6. the box: 1-3 stars, the weapon you take keeps them
        foreach (var d in Object.FindObjectsByType<ZoneDoor>()) d.DebugOpen();
        yield return Wait(0.5f);
        var box = Object.FindAnyObjectByType<MysteryBox>();
        var seen = new List<int>();
        float tw = Time.time;
        while (!box.IsAvailable && Time.time - tw < 3f) yield return null;
        if (box.IsAvailable)
        {
            box.Interact(inter);
            float t0 = Time.time;
            while (box.StateName != "Offering" && box.StateName != "Gone" && Time.time - t0 < 6f) yield return null;
            if (box.StateName == "Offering")
            {
                int stars = box.OfferedStars;
                var w = box.Offered;
                Check(stars >= 1 && stars <= 3 && box.Prompt(inter).Contains("★"), $"box offers '{box.Prompt(inter)}'");
                box.Interact(inter);
                yield return null;
                var s = shooter.SlotOf(w);
                seen.Add(stars);
                Check(s != null && s.stars >= stars, $"box: took '{(s != null ? s.Name : w.displayName)}' (offered {stars} stars)");
            }
        }
        else Write("FAIL the box never became available");
        if (seen.Count == 0) Write("     (box: only the joke came out, no weapon checked)");

        // 7. the odds: 60 / 30 / 10
        var count = new int[4];
        var rnd = new System.Random(7);
        for (int i = 0; i < 20000; i++) count[eco.RollBoxStars((float)rnd.NextDouble())]++;
        Check(Mathf.Abs(count[1] / 20000f - 0.6f) < 0.02f && Mathf.Abs(count[2] / 20000f - 0.3f) < 0.02f && Mathf.Abs(count[3] / 20000f - 0.1f) < 0.02f,
            $"box odds: 1★ {count[1] / 200f:0.0} %, 2★ {count[2] / 200f:0.0} %, 3★ {count[3] / 200f:0.0} %");
        Feedback4PlayTest.Shot("Renders/Stars_lijadora.png", lij.transform.position);
        Write("done");
    }
}
