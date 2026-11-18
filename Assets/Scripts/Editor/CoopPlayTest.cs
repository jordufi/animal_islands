// Split-screen co-op play test (Tools > Island > Play Test Coop). Starts in the main menu with three virtual
// gamepads: they join the lobby, J1 changes animal, START goes to the map selector, Isla del Faro loads with
// three players. Then: views and HUDs per player, each pad moves only its player and camera, shared pot,
// the door opener chooses the card (no pause, others keep playing, timer -> random), more old men per
// player, old men ignore downed players, last stand pistol, revive by holding □ (time), bleed-out and back
// next round with the pistol, Max Ammo refills everyone, extraction waits for everyone, all down = game
// over. Results in EditorPrefs "CoopPlayTest.Log" / ".Status", screenshots Renders/Coop_*.png.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class CoopPlayTest
{
    const string Key = "CoopPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static readonly List<Gamepad> pads = new List<Gamepad>();
    static bool startedScript;

    static string mapId = "isla-del-faro";

    // the same on La Serenisima (+ a gondola ride by J2 while the others play on)
    [MenuItem("Tools/Island/Play Test Coop (La Serenisima)")]
    public static void RunVenecia() { mapId = "la-serenisima"; Start(); }

    [MenuItem("Tools/Island/Play Test Coop (Villajubilada)")]
    public static void RunVillajubilada() { mapId = "villajubilada"; Start(); }

    [MenuItem("Tools/Island/Play Test Coop")]
    public static void Run() { mapId = "isla-del-faro"; Start(); }

    static void Start()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        script.Clear();
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
        foreach (var p in pads) if (p != null && p.added) InputSystem.RemoveDevice(p);
        pads.Clear();
        Party.Clear();
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
            if (!startedScript) { startedScript = true; script.Push(Script()); }
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
        float end = Time.realtimeSinceStartup + seconds;   // real time: works while the game is paused too
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    static IEnumerator WaitGame(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    static void Pad(int i, Vector2 stick, Vector2 look = default, params GamepadButton[] buttons)
    {
        var state = new GamepadState { leftStick = stick, rightStick = look };
        foreach (var b in buttons)
        {
            if (b == GamepadButton.RightTrigger) state.rightTrigger = 1f;
            else if (b == GamepadButton.LeftTrigger) state.leftTrigger = 1f;
            else state = state.WithButton(b);
        }
        InputSystem.QueueStateEvent(pads[i], state);
    }

    static IEnumerator Frames(int n)
    {
        for (int f = 0; f < n; f++) yield return null;
    }

    // press for a few frames, release for a few frames (the menu reads wasPressedThisFrame)
    static IEnumerator Press(int i, GamepadButton b)
    {
        Pad(i, Vector2.zero, default, b);
        yield return Frames(4);
        Pad(i, Vector2.zero);
        yield return Frames(4);
    }

    // ------------------------------------------------------------------ the test
    static IEnumerator Script()
    {
        // ---------------------------------------------------------- 1. lobby in the main menu
        for (int i = 0; i < 3; i++) pads.Add(InputSystem.AddDevice<Gamepad>("CoopPad" + (i + 1)));
        yield return Wait(1.5f);
        var mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm == null) { Write("FAIL no MainMenu in the scene"); yield break; }
        yield return Press(0, GamepadButton.South);   // title -> menu
        float m0 = Time.realtimeSinceStartup;
        while (mm.ScreenName != "Menu" && Time.realtimeSinceStartup - m0 < 5f) { if (mm.ScreenName == "Title") yield return Press(0, GamepadButton.South); yield return Wait(0.3f); }
        Write($"   menu screen: {mm.ScreenName}");
        yield return Wait(0.6f);
        ShotAll("Renders/Coop_menu.png");
        mm.OpenLobby();
        yield return Frames(3);
        Write($"   after OpenLobby: {mm.ScreenName}, pads {Gamepad.all.Count}");
        for (int i = 0; i < 3; i++) { yield return Press(i, GamepadButton.South); yield return Wait(0.1f); }
        Check(Party.Count == 3 && pads.All(p => Party.WithDevice(p) != null), $"lobby: 3 pads joined with X ({Party.Count} members)");
        string before = Party.Members[0].animal;
        yield return Press(0, GamepadButton.DpadRight);
        string after = Party.Members[0].animal;
        Check(before != after, $"J1 changes animal with ► ({before} -> {after})");
        var diff0 = GameSettings.Difficulty;
        bool mode0 = GameSettings.AnselmoCagon;
        yield return Press(0, GamepadButton.DpadDown);     // J1: cursor to "Dificultad"
        yield return Press(0, GamepadButton.DpadRight);
        var diff1 = GameSettings.Difficulty;
        yield return Press(0, GamepadButton.DpadDown);     // "Modo"
        yield return Press(0, GamepadButton.DpadRight);
        bool mode1 = GameSettings.AnselmoCagon;
        yield return Press(1, GamepadButton.DpadDown);     // J2 has no cursor: ▼ does nothing,
        yield return Press(1, GamepadButton.DpadRight);    // ► only changes J2's animal
        Check(diff1 != diff0 && mode1 != mode0 && GameSettings.Difficulty == diff1 && GameSettings.AnselmoCagon == mode1 && mm.LobbyRow == 2,
            $"lobby: J1 sets the difficulty ({diff0} -> {diff1}) and the mode ({(mode0 ? "Anselmo cagón" : "Clásico")} -> {(mode1 ? "Anselmo cagón" : "Clásico")}) with ▲ ▼ ◄ ►");
        ShotAll("Renders/Coop_lobby.png");
        // back to how they were (the user's settings)
        yield return Press(0, GamepadButton.DpadRight);
        yield return Press(0, GamepadButton.DpadUp);
        yield return Press(0, GamepadButton.DpadLeft);
        yield return Press(0, GamepadButton.DpadUp);
        Check(GameSettings.Difficulty == diff0 && GameSettings.AnselmoCagon == mode0 && mm.LobbyRow == 0, "lobby: settings restored, J1's cursor back on the animal");
        Party.Members[2].animal = Party.Members[1].animal;   // repeating an animal is allowed
        yield return Press(1, GamepadButton.Start);   // START -> map selector
        yield return Wait(0.5f);
        {
            var maps = new SerializedObject(mm).FindProperty("maps");
            int index = 0, k = 0;
            for (int i = 0; i < maps.arraySize; i++)
            {
                var m = maps.GetArrayElementAtIndex(i).objectReferenceValue as MapData;
                if (m == null) continue;
                if (m.id == mapId) index = k;
                k++;
            }
            mm.PlayMap(index);   // Isla del Faro (0) or the map asked for
        }
        float t0 = Time.realtimeSinceStartup;
        while ((GameManager.Instance == null || GameManager.Instance.PlayerCount < 3) && Time.realtimeSinceStartup - t0 < 30f) yield return null;
        var gm = GameManager.Instance;
        if (gm == null) { Write("FAIL the map did not load"); yield break; }
        yield return WaitGame(1f);
        var rm = RoundManager.Instance;
        rm.SpawningPaused = true;
        var players = gm.Players.ToList();
        foreach (var p in players) p.GodMode = true;
        var slots = players.Select(PlayerSlot.Of).ToList();
        Write($"loaded {SceneManager.GetActiveScene().name}: {string.Join(", ", players.Select(p => p.name))}");
        Check(players.Count == 3 && slots.Select(s => s.Index).SequenceEqual(new[] { 0, 1, 2 }), "3 players spawned (J1, J2, J3)");
        Check(slots[0].Animal.id == after && slots[1].Animal.id == slots[2].Animal.id, $"animals from the lobby: {string.Join(", ", slots.Select(s => s.Animal.id))} (J2 and J3 the same)");
        var rects = slots.Select(s => s.Camera.rect).ToList();
        Check(rects[0] == new Rect(0, 0.5f, 0.5f, 0.5f) && rects[1] == new Rect(0.5f, 0.5f, 0.5f, 0.5f) && rects[2] == new Rect(0, 0, 0.5f, 0.5f) && slots.Select(s => s.Camera).Distinct().Count() == 3,
            "three cameras in three quarters of the screen");
        var hud = Object.FindAnyObjectByType<GameHUD>();
        Check(hud != null && hud.PlayerHuds.Count == 3 && GameObject.Find("SpareView") != null, "one HUD per player + the island map in the 4th quarter");
        Check(slots.All(s => s.Actions != null && s.Actions.devices.HasValue && s.Actions.devices.Value.Count == 1 && s.Actions.devices.Value[0] == pads[s.Index]), "each player's input is bound to its own pad");
        Check(Mathf.Approximately(rm.PlayerCountFactor, 2f), $"old men per round x{rm.PlayerCountFactor} with 3 players");
        yield return WaitGame(0.5f);
        ShotAll("Renders/Coop_split3.png");

        // ---------------------------------------------------------- 2. each pad moves only its player / camera
        var start = players.Select(p => p.transform.position).ToList();
        var yaw0 = slots.Select(s => s.CameraRig.Yaw).ToList();
        Pad(1, new Vector2(0f, 1f), new Vector2(1f, 0f));
        yield return WaitGame(1f);
        Pad(1, Vector2.zero);
        yield return WaitGame(0.3f);
        var moved = players.Select((p, i) => Vector3.Distance(p.transform.position, start[i])).ToList();
        var turned = slots.Select((s, i) => Mathf.Abs(Mathf.DeltaAngle(s.CameraRig.Yaw, yaw0[i]))).ToList();
        Check(moved[1] > 1.2f && moved[0] < 0.2f && moved[2] < 0.2f, $"J2's stick moves only J2 (moved {moved[0]:F2} / {moved[1]:F2} / {moved[2]:F2} m)");
        Check(turned[1] > 20f && turned[0] < 1f && turned[2] < 1f, $"J2's right stick turns only J2's camera ({turned[0]:F0} / {turned[1]:F0} / {turned[2]:F0} deg)");

        // ---------------------------------------------------------- 2b. La Serenisima: J2 rides a gondola, J1 / J3 play on
        if (mapId == "la-serenisima")
        {
            foreach (var d in Object.FindObjectsByType<ZoneDoor>().ToList())   // not 1 and 3: the card steps below use them
                if (!d.RequiresMission && !d.name.StartsWith("Door1_") && !d.name.StartsWith("Door3_")) d.DebugOpen();
            yield return WaitGame(0.5f);
            var cmv = CardManager.Instance;
            if (cmv != null && cmv.CurrentOffer != null) { yield return Press(cmv.Chooser != null ? PlayerSlot.Of(cmv.Chooser).Index : 0, GamepadButton.South); yield return WaitGame(0.5f); }
            gm.AddPoints(1000, PointSource.Debug);
            // feedback 1: only some stops have a gondola each round, to a random open stop
            var stop = Object.FindObjectsByType<GondolaStop>().OrderByDescending(g => g.Zone != null && g.Zone.displayName.StartsWith("Ferrovia")).FirstOrDefault(g => g.CanRide);
            if (stop == null) Write("FAIL no gondola stop to ride from");
            else
            {
                Vector3 dest = stop.Destination;
                var j2 = players[1];
                var cc2 = j2.GetComponent<CharacterController>();
                cc2.enabled = false; j2.transform.position = stop.InteractPoint + Vector3.up * 0.1f; cc2.enabled = true;
                Vector3 j1At = players[0].transform.position;
                int potV = gm.Points;
                stop.Interact(j2.GetComponent<PlayerInteractor>());
                yield return WaitGame(1f);
                Check(stop.Busy && gm.Points == potV - gm.Economy.gondolaCost, "split-screen: J2 pays the gondola from the shared pot (" + (potV - gm.Points) + ")");
                Pad(0, new Vector2(0f, 1f));
                yield return WaitGame(0.8f);
                Pad(0, Vector2.zero);
                Check(Vector3.Distance(players[0].transform.position, j1At) > 1f, "J1 keeps moving while J2 is on the gondola");
                ShotAll("Renders/Coop_venecia_gondola.png");
                float t1 = Time.realtimeSinceStartup;
                while (stop.Busy && Time.realtimeSinceStartup - t1 < 16f) yield return null;
                yield return WaitGame(0.3f);
                Check(!stop.Busy && Vector3.Distance(j2.transform.position, dest) < 1.5f && j2.GetComponent<CharacterController>().enabled,
                    "J2 lands at the next stop (" + Vector3.Distance(j2.transform.position, dest).ToString("F1") + " m) and can move again");
                ShotAll("Renders/Coop_venecia_split3.png");
            }
        }

        // ---------------------------------------------------------- 2c. Villajubilada: the first encierro with three players
        if (mapId == "villajubilada")
        {
            var enc = Encierro.Instance;
            var gate = Object.FindAnyObjectByType<EncierroGate>();
            if (enc == null || gate == null) { Write("FAIL no Encierro in the scene"); yield break; }
            players[0].GodMode = true; players[1].GodMode = true;
            Teleport(players[0], enc.PastTheGate + Vector3.forward * 8f);   // J1 already up the street (through the shut Porton: test only)
            Teleport(players[1], players[1].transform.position);            // J2 stays in La Parada, standing
            players[2].GodMode = false;
            players[2].TakeDamage(99999);                                    // J3 goes down in La Parada
            yield return WaitGame(0.5f);
            Check(players[2].InLastStand && enc.InActOneArea(players[2].transform.position) && enc.InActOneArea(players[1].transform.position), "J2 standing and J3 downed in La Parada, J1 up the street");
            enc.DebugStartNow();
            float tv = Time.realtimeSinceStartup;
            while (enc.ActOne && Time.realtimeSinceStartup - tv < 60f) yield return null;
            yield return WaitGame(0.3f);
            Check(!enc.ActOne && gate.Blocked, "split-screen: the herd went through and the Porton shut");
            Check(players.All(p => !enc.InActOneArea(p.transform.position)), "nobody is left behind the Porton: J2 and the downed J3 were dragged through");
            Check(players[2].InLastStand && !players[2].IsDead, "J3 is still downed (not killed by the drag) and can be revived on this side");
            ShotAll("Renders/Coop_villajubilada_split3.png");
            players[2].GodMode = true;
            players[2].Revive();
            tv = Time.realtimeSinceStartup;
            while (players[2].IsDowned && Time.realtimeSinceStartup - tv < 6f) yield return null;   // he takes a moment to get up
            Check(!players[2].IsDowned, "J3 revived past the Porton");
            foreach (var p in players) { p.GodMode = true; Teleport(p, new Vector3(-8f, 0.1f, 4f)); }   // a doorway of the Calle Mayor
            tv = Time.realtimeSinceStartup;
            while (enc.Running && Time.realtimeSinceStartup - tv < 120f) yield return null;
            Check(!enc.Running && enc.Done == 1, "the encierro ended with three players");
            yield break;
        }

        // ---------------------------------------------------------- 3. shared pot + the opener chooses the card (game paused)
        gm.AddPoints(20000, PointSource.Debug);
        var door1 = Object.FindObjectsByType<ZoneDoor>().First(d => d.name.StartsWith("Door1_"));
        var i2 = players[1].GetComponent<PlayerInteractor>();
        Teleport(players[1], door1.transform.position + (players[0].transform.position - door1.transform.position).normalized * 1.5f);
        yield return WaitGame(0.4f);
        int pot = gm.Points;
        Write($"   J2 focus: {(i2.Focus != null ? i2.Focus.name : "none")} prompt '{(i2.Focus != null ? i2.Focus.Prompt(i2) : "")}'");
        yield return Press(1, GamepadButton.West);   // □
        yield return Wait(0.3f);
        var cm = CardManager.Instance;
        Check(pot - gm.Points == door1.Cost, $"door paid from the shared pot ({pot} -> {gm.Points})");
        Check(cm.CurrentOffer != null && cm.Chooser == i2 && cm.PausesGame && Time.timeScale == 0f && gm.State == GameState.ChoosingCard,
            $"J2 opened the door and chooses the card; the game is paused for everyone (timeScale {Time.timeScale}, state {gm.State})");
        var cardsPanel = GameObject.Find("Cards");
        var title = cardsPanel != null ? cardsPanel.GetComponentsInChildren<UnityEngine.UI.Text>().FirstOrDefault(t => t.name == "Title") : null;
        var prt = cardsPanel != null ? (RectTransform)cardsPanel.transform : null;
        Check(prt != null && prt.anchorMin == Vector2.zero && prt.anchorMax == Vector2.one && title != null && title.text.Contains("J2"), $"the cards cover the whole screen: '{(title != null ? title.text : "-")}'");
        var p1pos = players[0].transform.position;
        Pad(0, new Vector2(1f, 0f));
        yield return Wait(0.5f);
        Pad(0, Vector2.zero);
        Check(Vector3.Distance(players[0].transform.position, p1pos) < 0.05f, "J1 can't move while J2 chooses (paused)");
        var offer = cm.CurrentOffer.ToArray();
        yield return Wait(0.4f);
        yield return Press(0, GamepadButton.DpadRight);   // J1's pad does nothing to the cards
        yield return Press(0, GamepadButton.South);
        Check(cm.CurrentOffer != null && cm.Selected == 0, "J1's pad can't move or take J2's cards");
        yield return Press(1, GamepadButton.DpadRight);   // J2: D-pad ►
        int selDpad = cm.Selected;
        Pad(1, new Vector2(1f, 0f));                      // and the stick ►
        yield return Wait(0.15f);
        Pad(1, Vector2.zero);
        yield return Wait(0.15f);
        int selStick = cm.Selected;
        yield return Press(1, GamepadButton.DpadLeft);
        int selBack = cm.Selected;
        ShotAll("Renders/Coop_cards.png");
        yield return Press(1, GamepadButton.South);
        Check(selDpad == 1 && selStick == 2 && selBack == 1 && cm.CurrentOffer == null && cm.Taken.Count == 1 && cm.Taken[0] == offer[1],
            $"J2 moves with the D-pad and the stick (1, 2, back to 1) and takes the 2nd card with X: {cm.Taken.Select(c => c.title).FirstOrDefault()}");
        yield return Wait(0.2f);
        Check(Time.timeScale == 1f && gm.State == GameState.Playing, "the game goes on after the choice");
        // J3 opens the next door: J3 chooses (third card with ► ► and □)
        var door3 = Object.FindObjectsByType<ZoneDoor>().First(d => d.name.StartsWith("Door3_"));
        door3.Interact(players[2].GetComponent<PlayerInteractor>());
        Check(cm.CurrentOffer != null && PlayerSlot.Of(cm.Chooser).Index == 2 && Time.timeScale == 0f, "J3 opened the next door: J3 chooses, paused");
        var offer3 = cm.CurrentOffer.ToArray();
        yield return Wait(0.4f);
        yield return Press(2, GamepadButton.DpadRight);
        yield return Press(2, GamepadButton.DpadRight);
        yield return Press(2, GamepadButton.West);
        Check(cm.CurrentOffer == null && cm.Taken.Count == 2 && cm.Taken[1] == offer3[2], $"J3 took the 3rd card ({cm.Taken[1].title})");
        yield return Wait(0.2f);

        // ---------------------------------------------------------- 4. going down: last stand, ignored, -5 %
        foreach (var p in players) p.GodMode = false;
        var shooter1 = players[0].GetComponent<PlayerShooter>();
        var drinks1 = players[0].GetComponent<PlayerDrinks>();
        var drink = AssetDatabase.LoadAssetAtPath<DrinkData>("Assets/Data/Drinks/MojitoTurbo.asset");
        drinks1.Add(drink);
        var smg = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/SMG.asset");
        shooter1.GiveWeapon(smg);
        Teleport(players[0], new Vector3(-30f, 0f, 4f));
        Teleport(players[1], new Vector3(-26f, 0f, 4f));
        Teleport(players[2], new Vector3(-30f, 0f, 0f));
        yield return WaitGame(0.3f);
        pot = gm.Points;
        players[0].TakeDamage(10000);
        yield return WaitGame(1f);
        Check(players[0].InLastStand && !players[0].IsDead, "J1 at 0 HP is downed (last stand), not dead");
        Check(shooter1.InLastStand && shooter1.Current.data == shooter1.StartingWeapon && drinks1.Owned.Count == 0, $"last stand: pistol in hand ({shooter1.Current.Name}), drinks lost");
        Check(gm.Points == pot - Mathf.RoundToInt(pot * gm.Coop.downPenalty), $"going down costs {gm.Coop.downPenalty:P0} of the pot ({pot} -> {gm.Points})");
        var enemy = rm.SpawnEnemy(null, players[0].transform.position + Vector3.right * 2f);
        yield return WaitGame(1.5f);
        var targetField = enemy != null ? enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().destination : Vector3.zero;
        float toJ1 = Vector3.Distance(targetField, players[0].transform.position), toOthers = Mathf.Min(Vector3.Distance(targetField, players[1].transform.position), Vector3.Distance(targetField, players[2].transform.position));
        Check(enemy != null && toOthers < toJ1, $"old men ignore the downed J1 and go for a standing player (destination {toOthers:F1} m from J2/J3, {toJ1:F1} m from J1)");
        if (enemy != null) enemy.GetComponent<EnemyHealth>().TakeDamage(1e6f, false);
        ShotAll("Renders/Coop_downed.png");

        // ---------------------------------------------------------- 5. revive: J2 holds □ next to J1
        Teleport(players[1], players[0].transform.position + Vector3.right * 1f);
        yield return WaitGame(0.4f);
        Write($"   J2 focus: {(i2.Focus != null ? i2.Focus.GetType().Name : "none")} '{(i2.Focus != null ? i2.Focus.Prompt(i2) : "")}'");
        Pad(1, Vector2.zero, default, GamepadButton.West);   // hold □
        float r0 = Time.time;
        while (players[0].InLastStand && Time.time - r0 < 8f) yield return null;
        float took = Time.time - r0;
        Pad(1, Vector2.zero);
        yield return WaitGame(0.6f);
        Check(!players[0].IsDowned && !players[0].IsDead && took > gm.Coop.reviveTime - 0.4f && took < gm.Coop.reviveTime + 1f, $"J2 revived J1 by holding □ for {took:F1} s (revive time {gm.Coop.reviveTime} s)");
        Check(shooter1.Current.data == smg || shooter1.Other.data == smg, $"J1 got the weapons back after the revive ({shooter1.Current.Name} / {shooter1.Other.Name})");
        Check(slots[1].Revives == 1 && slots[0].Downs == 1, $"stats: J1 downs {slots[0].Downs}, J2 revives {slots[1].Revives}");

        // ---------------------------------------------------------- 6. Max Ammo refills everyone
        var shooter3 = players[2].GetComponent<PlayerShooter>();
        shooter3.Current.reserve = 0;
        var pm = Object.FindAnyObjectByType<PowerUpManager>();
        pm.Spawn(players[0].transform.position + Vector3.up * 0.6f);
        yield return WaitGame(0.5f);
        Check(shooter3.Current.reserve > 0, $"J1 took a Nevera de munición: J3's reserve refilled too ({shooter3.Current.reserve})");

        // ---------------------------------------------------------- 7. bleed out -> back next round with the pistol
        var sniper = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/Sniper.asset");
        shooter3.GiveWeapon(sniper);
        players[2].TakeDamage(10000);
        float b0 = Time.time;
        while (!players[2].IsDead && Time.time - b0 < gm.Coop.bleedOutTime + 5f) yield return null;
        Check(players[2].IsDead && Time.time - b0 > gm.Coop.bleedOutTime - 1f, $"J3 bled out after {Time.time - b0:F0} s (bleed-out {gm.Coop.bleedOutTime} s)");
        Check(gm.State == GameState.Playing, "the run goes on while J1 and J2 stand");
        rm.SpawningPaused = false;
        if (!rm.InIntermission) rm.DebugEndRound();
        int round = rm.Round;
        float w0 = Time.time;
        while (rm.Round == round && Time.time - w0 < 25f) yield return null;
        yield return WaitGame(0.5f);
        rm.SpawningPaused = true;
        rm.DebugEndRound();
        Check(!players[2].IsDead && shooter3.Current.data == shooter3.StartingWeapon && shooter3.Other.data == null, $"J3 is back at round {rm.Round} with the pistol only ({shooter3.Current.Name})");
        float mate = Mathf.Min(Vector3.Distance(players[2].transform.position, players[0].transform.position), Vector3.Distance(players[2].transform.position, players[1].transform.position));
        Check(mate < 5f, $"J3 comes back next to a teammate ({mate:F1} m)");

        // ---------------------------------------------------------- 8. extraction waits for everyone standing
        var ext = Object.FindAnyObjectByType<ExtractionPoint>();
        gm.BossDefeated = true;
        Teleport(players[0], ext.transform.position + Vector3.left * 1.5f);
        yield return WaitGame(0.3f);
        var i1 = players[0].GetComponent<PlayerInteractor>();
        string waitPrompt = ext.Prompt(i1);
        ext.Interact(i1);
        Check(gm.State == GameState.Playing && waitPrompt.Contains("J2") && waitPrompt.Contains("J3"), $"boat: '{waitPrompt}' (no escape yet)");
        gm.BossDefeated = false;

        // ---------------------------------------------------------- 9. everyone down = game over
        foreach (var p in players) p.TakeDamage(10000);
        float g0 = Time.time;
        while (gm.State != GameState.GameOver && Time.time - g0 < 6f) yield return null;
        Check(gm.State == GameState.GameOver, $"all three down -> game over ({gm.State})");
        yield return WaitGame(0.5f);
        ShotAll("Renders/Coop_gameover.png");

        // ---------------------------------------------------------- 10. 2 and 4 players (Reintentar keeps the party)
        pads.Add(InputSystem.AddDevice<Gamepad>("CoopPad4"));
        foreach (int n in new[] { 2, 4 })
        {
            Party.Clear();
            for (int i = 0; i < n; i++) Party.Add(i % 2 == 0 ? "Panda" : "Toucan", pads[i]);
            var old = GameManager.Instance;
            old.Restart();
            float l0 = Time.realtimeSinceStartup;
            while ((GameManager.Instance == null || GameManager.Instance == old || GameManager.Instance.PlayerCount < n) && Time.realtimeSinceStartup - l0 < 30f) yield return null;
            yield return WaitGame(1f);
            var g = GameManager.Instance;
            RoundManager.Instance.SpawningPaused = true;
            var views = g.Players.Select(p => PlayerSlot.CameraOf(p).rect).ToList();
            var huds = Object.FindAnyObjectByType<GameHUD>().PlayerHuds;
            bool layout = n == 2 ? views[0] == new Rect(0, 0.5f, 1, 0.5f) && views[1] == new Rect(0, 0, 1, 0.5f)
                                 : views.Distinct().Count() == 4 && views.All(r => Mathf.Approximately(r.width, 0.5f) && Mathf.Approximately(r.height, 0.5f));
            Check(g.PlayerCount == n && layout && huds.Count == n && (GameObject.Find("SpareView") == null), $"{n} players after Reintentar: {(n == 2 ? "top / bottom" : "four quarters")}, {huds.Count} HUDs, no spare map");
            var scales = huds.Select(h => h.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution.x).Distinct().ToList();
            Check(scales.Count == 1, $"every player's HUD has the same scale ({string.Join(", ", scales.Select(x => x.ToString("F0")))})");
            g.ShowMessage("Prueba de aviso en pantalla dividida");
            yield return WaitGame(0.6f);
            ShotAll($"Renders/Coop_split{n}.png");
        }
    }

    // ------------------------------------------------------------------ helpers
    static void Teleport(PlayerHealth player, Vector3 pos)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        if (NavAgents.Sample(pos, 4f, out var h)) pos = h;
        player.transform.position = pos + Vector3.up * 0.1f;
        cc.enabled = true;
    }

    static Texture2D Read(RenderTexture rt)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        return tex;
    }

    // Every player camera into one picture (each in its own viewport) + the overlay canvases on top.
    internal static void ShotAll(string path)
    {
        var gm = GameManager.Instance;
        var cams = gm != null ? gm.Players.Select(PlayerSlot.CameraOf).ToList() : new List<Camera> { Camera.main };   // menu: its camera
        var rt = new RenderTexture(1280, 720, 24);
        foreach (var cam in cams)
        {
            if (cam == null) continue;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }
        var tex = Read(rt);
        // the overlay canvases into a transparent texture (URP clears every camera), blended on top
        var uiRt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var uiCam = new GameObject("ShotUI").AddComponent<Camera>();
        uiCam.clearFlags = CameraClearFlags.SolidColor;
        uiCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        uiCam.cullingMask = 1 << 5;
        uiCam.orthographic = true;
        uiCam.targetTexture = uiRt;
        var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = uiCam; c.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        uiCam.Render();
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        var ui = Read(uiRt);
        var a = tex.GetPixels();
        var b = ui.GetPixels();
        for (int i = 0; i < a.Length; i++) a[i] = Color.Lerp(a[i], new Color(b[i].r, b[i].g, b[i].b, 1f), b[i].a);
        tex.SetPixels(a);
        tex.Apply();
        Object.Destroy(uiCam.gameObject);
        Object.Destroy(ui);
        uiRt.Release();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.Destroy(tex);
        rt.Release();
    }
}
