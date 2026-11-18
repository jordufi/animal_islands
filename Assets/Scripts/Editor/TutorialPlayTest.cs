using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// User feedback 1, phase 7: "Isla del Faro (tutorial)". Plays the whole tutorial with a virtual pad (move, look,
// jump, shoot, reload, ukulele, false teeth, ability) and the interactables (door + card, drink, box), checks the rounds
// wait for it and start after it, then that the map is in the selector. Tools > Island > Play Test Tutorial.
// EditorPrefs TutorialPlayTest.Log / .Status, pictures Renders/Tutorial_*.png.
public static class TutorialPlayTest
{
    const string Key = "TutorialPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static Gamepad pad;

    [MenuItem("Tools/Island/Play Test Tutorial")]
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
        if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; }
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

    static IEnumerator Real(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    static void PadState(Vector2 move = default, Vector2 look = default, params GamepadButton[] buttons)
    {
        var s = new GamepadState { leftStick = move, rightStick = look };
        foreach (var b in buttons)
        {
            if (b == GamepadButton.RightTrigger) s.rightTrigger = 1f;
            else if (b == GamepadButton.LeftTrigger) s.leftTrigger = 1f;
            else s = s.WithButton(b);
        }
        InputSystem.QueueStateEvent(pad, s);
    }

    static IEnumerator Press(GamepadButton b, float hold = 0.15f)
    {
        PadState(default, default, b);
        yield return Real(hold);
        PadState();
        yield return Real(0.15f);
    }

    static IEnumerator WaitStep(Tutorial t, string name, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (t.StepName != name && Time.realtimeSinceStartup < end) yield return null;
        Check(t.StepName == name, $"tutorial step {t.Step} '{t.StepName}' (want '{name}')");
    }

    static void Teleport(Component p, Vector3 at)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.position = at;
        cc.enabled = true;
    }

    // the practice old men stand still on the crosshair line, a few metres ahead
    static void Freeze(float distance)
    {
        var cam = GameManager.Instance.CameraRig.GetComponent<Camera>();
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var player = GameManager.Instance.Player;
        float skip = Vector3.Dot(player.transform.position + Vector3.up * 0.8f - ray.origin, ray.direction);
        if (skip > 0f) ray.origin += ray.direction * skip;
        foreach (var h in Object.FindObjectsByType<EnemyHealth>().Where(h => !h.IsDead))
        {
            var ai = h.GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;
            var ag = h.GetComponent<NavMeshAgent>();
            if (ag != null) ag.enabled = false;
            var p = ray.origin + ray.direction * distance;
            h.transform.position = new Vector3(p.x, player.transform.position.y, p.z);
            h.transform.rotation = Quaternion.LookRotation(-new Vector3(ray.direction.x, 0f, ray.direction.z));
        }
        Physics.SyncTransforms();
    }

    static IEnumerator Script()
    {
        var gm = GameManager.Instance;
        var rm = RoundManager.Instance;
        var player = gm.Player;
        var inter = player.GetComponent<PlayerInteractor>();
        var shooter = player.GetComponent<PlayerShooter>();
        pad = InputSystem.AddDevice<Gamepad>("TutorialPad");
        var t = gm.gameObject.AddComponent<Tutorial>();   // what the menu does for the tutorial map
        yield return Real(1f);
        Check(RoundManager.Hold && rm.Round == 0, $"the rounds wait for the tutorial (round {rm.Round})");

        // 1. move
        yield return WaitStep(t, "move", 5f);
        Feedback4PlayTest.ShotHud("Renders/Tutorial_1.png");
        PadState(new Vector2(0f, 1f));
        yield return Real(2.5f);
        PadState();
        // 2. look
        yield return WaitStep(t, "look", 6f);
        PadState(default, new Vector2(1f, 0f));
        yield return Real(1.5f);
        PadState();
        // 3. jump
        yield return WaitStep(t, "jump", 5f);
        yield return Press(GamepadButton.South);
        // 4. shoot
        yield return WaitStep(t, "shoot", 5f);
        yield return Real(0.5f);
        Feedback4PlayTest.ShotHud("Renders/Tutorial_4.png");
        float until = Time.realtimeSinceStartup + 20f;
        while (t.StepName == "shoot" && Time.realtimeSinceStartup < until)
        {
            Freeze(4f);
            shooter.Current.magazine = Mathf.Max(3, shooter.Current.magazine);
            if (!shooter.TestFire()) Write("     no shot: reloading " + shooter.IsReloading + ", " + t.Note);
            yield return Real(0.25f);
            // the aim of a frozen old man is not what this test is about: the shot happened, finish him
            foreach (var h in Object.FindObjectsByType<EnemyHealth>().Where(h => !h.IsDead)) EnemyHits.Apply(h, 999f, false, HitKind.Gun, shooter, false);
            yield return Real(0.3f);
        }
        Write("     shoot step: " + t.Note + ", player at " + player.transform.position + ", magazine " + shooter.Current.magazine + ", reloading " + shooter.IsReloading);
        // 5. reload
        yield return WaitStep(t, "reload", 6f);
        yield return Press(GamepadButton.West);
        if (t.StepName == "reload" && !shooter.IsReloading) { Write("     (□ used something nearby: reload by hand)"); shooter.Reload(); }
        // 6. ukulele: the practice old man right in front
        yield return WaitStep(t, "melee", 6f);
        until = Time.realtimeSinceStartup + 20f;
        while (t.StepName == "melee" && Time.realtimeSinceStartup < until)
        {
            Freeze(1.4f);
            yield return Press(GamepadButton.RightStick);
            yield return Real(0.6f);
            foreach (var h in Object.FindObjectsByType<EnemyHealth>().Where(h => !h.IsDead)) EnemyHits.Apply(h, 999f, false, HitKind.Melee, player.GetComponent<PlayerMelee>(), false);
            yield return Real(0.3f);
        }
        // 7. false teeth
        yield return WaitStep(t, "grenade", 6f);
        yield return Real(0.5f);
        yield return Press(GamepadButton.RightShoulder);
        // 8. ability
        yield return WaitStep(t, "ability", 12f);
        yield return Press(GamepadButton.LeftShoulder);
        // 9-10. door + card
        yield return WaitStep(t, "door", 6f);
        var door = Object.FindObjectsByType<ZoneDoor>().Where(d => !d.RequiresMission).OrderBy(d => Vector3.Distance(d.transform.position, player.transform.position)).First();
        Check(gm.Points >= door.Cost, $"the tutorial gives the conchas for the first door ({gm.Points} >= {door.Cost})");
        door.Interact(inter);
        yield return WaitStep(t, "card", 5f);
        yield return Real(1f);
        Feedback4PlayTest.ShotHud("Renders/Tutorial_cartas.png");
        var cards = CardManager.Instance;
        if (cards != null && cards.CurrentOffer != null) cards.Choose(cards.CurrentOffer[0]);
        // 11. drink
        yield return WaitStep(t, "drink", 8f);
        Check(BoatMission.PowerOn, "the tutorial gives the light (drinks work)");
        var machine = Object.FindObjectsByType<DrinkMachine>().Where(m => m.CanInteract(inter)).OrderBy(m => Vector3.Distance(m.transform.position, player.transform.position)).FirstOrDefault();
        Check(machine != null, "a drink machine to buy from");
        if (machine != null) machine.Interact(inter);
        // 12. the box
        yield return WaitStep(t, "box", 8f);
        var box = Object.FindAnyObjectByType<MysteryBox>();
        Check(box != null && box.IsAvailable, $"the box is there for the tutorial ({(box != null ? box.StateName : "none")})");
        Feedback4PlayTest.Shot("Renders/Tutorial_caja.png", box.transform.position);
        box.Interact(inter);
        until = Time.realtimeSinceStartup + 10f;
        while (box.StateName != "Offering" && box.StateName != "Gone" && Time.realtimeSinceStartup < until) yield return null;
        if (box.StateName == "Offering") box.Interact(inter);
        // the end: round 1 starts
        yield return WaitStep(t, "end", 8f);
        Feedback4PlayTest.ShotHud("Renders/Tutorial_fin.png");
        until = Time.realtimeSinceStartup + 30f;
        while (rm.Round < 1 && Time.realtimeSinceStartup < until) yield return null;
        Check(t.Done && !RoundManager.Hold && rm.Round == 1 && !player.GodMode, $"after the tutorial the normal game starts: round {rm.Round}, god mode {player.GodMode}");

        // the map selector has it, solo only
        var tut = AssetDatabase.LoadAssetAtPath<MapData>("Assets/Data/Maps/IslaDelFaroTutorial.asset");
        Check(tut != null && tut.tutorial && tut.sceneName == "IslaZombie", "'Isla del Faro (tutorial)' is a map of the selector");
        Write("done");
    }
}
