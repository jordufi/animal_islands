// Scripted play test of the Original map's borders and collision (Tools > Island > Play Test Original):
// with every door closed, the player (virtual gamepad) sprints at border walls and closed doors, jumps and gets
// an extra 5 m launch in the air (more than the Frog's super jump) and must stay on its side; then every door
// is opened and the CharacterController walks random NavMesh paths (no invisible walls / props in the way).
// Results in EditorPrefs "OriginalMapPlayTest.Log" / ".Status", screenshots in Renders/Original_*.png.
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

public static class OriginalMapPlayTest
{
    const string Key = "OriginalMapPlayTest.";
    public static string Log => EditorPrefs.GetString(Key + "Log", "");
    public static string Status => EditorPrefs.GetString(Key + "Status", "");

    static readonly Stack<IEnumerator> script = new Stack<IEnumerator>();
    static Gamepad pad;
    static string prevAnimal;

    [MenuItem("Tools/Island/Play Test Original")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first"); return; }
        EditorSceneManager.OpenScene(TestMapSetup.GameScenePath, OpenSceneMode.Single);
        EditorPrefs.SetString(Key + "Log", "");
        EditorPrefs.SetString(Key + "Status", "running");
        EditorPrefs.SetBool(Key + "Running", true);
        prevAnimal = GameSettings.Animal;
        GameSettings.Animal = "Frog";
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
        if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; }
        if (prevAnimal != null) GameSettings.Animal = prevAnimal;
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

    struct Probe { public string name; public Vector3 from; public Transform wall; public float side; }

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
        foreach (var e in Object.FindObjectsByType<EnemyAI>()) Object.Destroy(e.gameObject);
        Write($"Player {gm.Animal.id}");
        pad = InputSystem.AddDevice<Gamepad>("TestPad");
        yield return null;

        // ---------------------------------------------------------- 1. jump at the borders (doors closed)
        var probes = new List<Probe>();
        var walls = GameObject.Find("TestMap/Walls");
        var boxes = walls.GetComponentsInChildren<BoxCollider>().Where(b => b.gameObject.layer == TestMapSetup.WallLayer).ToList();
        foreach (var box in boxes)
        {
            // thin axis = local x or z; probe 1.4 m out from its middle on each side that has NavMesh
            var t = box.transform;
            bool thinX = box.size.x < box.size.z;
            float len = thinX ? box.size.z : box.size.x;
            if (len < 1.4f) continue;
            Vector3 normal = (thinX ? t.right : t.forward).normalized;
            Vector3 mid = t.TransformPoint(box.center);
            foreach (float s in new[] { 1f, -1f })
            {
                var guess = new Vector3(mid.x, 0f, mid.z) + normal * s * 1.4f;
                if (!NavAgents.Sample(guess + Vector3.up * 2f, 3f, out var p)) continue;
                if (new Vector2(p.x - guess.x, p.z - guess.z).magnitude > 0.5f) continue;
                probes.Add(new Probe { name = t.name, from = p, wall = t, side = s });
            }
        }
        foreach (var d in Object.FindObjectsByType<ZoneDoor>())
        {
            var bar = new SerializedObject(d).FindProperty("barrier").objectReferenceValue as GameObject;
            foreach (float s in new[] { 1f, -1f })
                if (NavAgents.Sample(d.transform.position + d.transform.forward * s * 1.6f + Vector3.up, 2f, out var p))
                    probes.Add(new Probe { name = "PUERTA " + d.name, from = p, wall = bar.transform, side = s });
        }
        // spread over the island: at most 70 probes, doors always
        var rnd = new System.Random(3);
        var picked = probes.Where(p => p.name.StartsWith("PUERTA")).ToList();
        picked.AddRange(probes.Where(p => !p.name.StartsWith("PUERTA")).OrderBy(_ => rnd.Next()).Take(70 - picked.Count));
        Write($"{probes.Count} possible probes next to walls / doors, testing {picked.Count}");
        int crossed = 0, tested = 0;
        var fails = new List<string>();
        int shots = 0;
        foreach (var pr in picked)
        {
            Vector3 normal = pr.wall.GetComponent<BoxCollider>() is BoxCollider bc && bc.size.x < bc.size.z ? pr.wall.right : pr.wall.forward;
            if (pr.name.StartsWith("PUERTA")) normal = pr.wall.forward;
            float SideOf(Vector3 pos) => Mathf.Sign(Vector3.Dot(pos - pr.wall.position, normal));
            float startSide = SideOf(pr.from);
            Vector3 toWall = -normal * startSide;
            Teleport(player, pr.from, Quaternion.LookRotation(new Vector3(toWall.x, 0f, toWall.z)).eulerAngles.y);
            rig.SetTarget(player.transform);
            Pad(Vector2.zero);
            yield return Wait(0.15f);
            Pad(new Vector2(0f, 1f), GamepadButton.LeftStick);
            yield return Wait(0.1f);
            Pad(new Vector2(0f, 1f), GamepadButton.South);
            yield return Wait(0.1f);
            Pad(new Vector2(0f, 1f));
            yield return Wait(0.2f);
            pc.Launch(5f);   // more than the Frog's super jump (4 m), in the air
            yield return Wait(0.5f);
            if (shots < 3 && !pr.name.StartsWith("PUERTA") && pr.name.StartsWith("Borde")) { Shot(rig.GetComponent<Camera>(), $"Renders/Original_border_jump{shots}.png"); shots++; }
            yield return Wait(1.6f);
            Pad(Vector2.zero);
            yield return Wait(0.2f);
            tested++;
            var end = player.transform.position;
            if (SideOf(end) != startSide)
            {
                crossed++;
                if (fails.Count < 10) fails.Add($"{pr.name} from ({pr.from.x:F1},{pr.from.z:F1}) to ({end.x:F1},{end.y:F1},{end.z:F1})");
            }
        }
        Check(crossed == 0, $"jumps at borders / closed doors (jump + 5 m launch): {tested} tested, {crossed} got across" + (fails.Count > 0 ? ": " + string.Join(" | ", fails) : ""));

        // ---------------------------------------------------------- 2. random walks with every door open
        foreach (var d in Object.FindObjectsByType<ZoneDoor>()) d.DebugOpen();
        yield return Wait(0.5f);
        var boss = BossEncounter.Instance != null ? BossEncounter.Instance.Boss : null;
        if (boss != null) boss.Health.TakeDamage(boss.Health.Current + 1f, false);
        yield return Wait(0.5f);
        var targets = new List<Vector3>();
        foreach (var z in ZoneManager.Instance.Zones) foreach (var t in z.SpawnPoints.Concat(z.BoxSpots)) targets.Add(t.position);
        for (int i = 0; i < 80; i++)
        {
            var v = new Vector3(rnd.Next(-36, 26), 0f, rnd.Next(-34, 30));
            if (NavAgents.Sample(v + Vector3.up * 2f, 1.5f, out var s)) targets.Add(s);
        }
        // old men: every spawn point and box spot must have a complete NavMesh path from the start with the doors open
        {
            var start = GameObject.Find("TestMap/PlayerSpawn").transform.position;
            var pth = new NavMeshPath();
            var cut = new List<string>();
            int total = 0;
            foreach (var z in ZoneManager.Instance.Zones)
                foreach (var t in z.SpawnPoints.Concat(z.BoxSpots))
                {
                    total++;
                    if (!NavAgents.Sample(start, 3f, out var sa) || !NavAgents.Sample(t.position, 3f, out var sb)
                        || !NavMesh.CalculatePath(sa, sb, NavAgents.Filter(0), pth) || pth.status != NavMeshPathStatus.PathComplete)
                    {
                        string why = NavAgents.Sample(t.position, 3f, out var sq) ? $"mesh en {sq} (a {Vector3.Distance(sq, t.position):F1} m), ruta {pth.status}" : "sin malla a 3 m";
                        cut.Add($"{z.name}/{t.name} {t.position} [{why}]");
                    }
                }
            Check(cut.Count == 0, $"old men: {total - cut.Count}/{total} spawn points and box spots reachable on the NavMesh from the start with every door open" + (cut.Count > 0 ? ": " + string.Join(" | ", cut) : ""));
        }
        int walks = 0, stuck = 0, noPath = 0;
        var stuckAt = new List<string>();
        for (int i = 0; i < 120; i++)
        {
            var a = targets[rnd.Next(targets.Count)];
            var b = targets[rnd.Next(targets.Count)];
            var result = Walk(player, cc, a, b);
            if (result == null) { noPath++; continue; }
            walks++;
            if (result.Length > 0) { stuck++; if (stuckAt.Count < 12) stuckAt.Add(result); }
            if (i % 10 == 9) yield return null;
        }
        Check(stuck == 0, $"random walks (doors open): {walks} NavMesh paths walked with the CharacterController ({noPath} pairs without a path), {stuck} blocked" + (stuckAt.Count > 0 ? ": " + string.Join(" | ", stuckAt) : ""));

        // ---------------------------------------------------------- 3. pictures: graveyard rocks, a tree trunk
        Teleport(player, new Vector3(-8f, 0f, -21f), 0f);
        rig.SetTarget(player.transform);
        yield return Wait(0.6f);
        Shot(rig.GetComponent<Camera>(), "Renders/Original_cementerio_sur.png");
        Teleport(player, new Vector3(9f, 0f, -14f), 300f);
        rig.SetTarget(player.transform);
        yield return Wait(0.6f);
        Shot(rig.GetComponent<Camera>(), "Renders/Original_chiringuito.png");
        Pad(Vector2.zero);
        yield return null;
    }

    static void Pad(Vector2 stick, params GamepadButton[] buttons)
    {
        var state = new GamepadState { leftStick = stick };
        foreach (var b in buttons) state = state.WithButton(b);
        InputSystem.QueueStateEvent(pad, state);
    }

    static void Teleport(PlayerHealth player, Vector3 pos, float yaw)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
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
                    // the NavMesh puts corners right against props (agent radius 0.35, voxels ~0.12): rubbing past a
                    // corner within 0.6 m counts as reaching it, like a player would
                    if (d.magnitude < 0.6f) break;
                    var hits = Physics.CapsuleCastAll(p1 + Vector3.up * 0.33f, p1 + Vector3.up * 0.76f, 0.3f, d.normalized, 0.3f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)
                        .Select(h => $"{h.collider.name}{(h.collider.transform.parent != null && h.collider.transform.parent.parent != null ? "/" + h.collider.transform.parent.parent.name : "")} at ({h.point.x:F2},{h.point.y:F2},{h.point.z:F2}) n ({h.normal.x:F1},{h.normal.y:F1},{h.normal.z:F1})");
                    return $"{p1.x:F2},{p1.y:F2},{p1.z:F2} -> corner ({corner.x:F1},{corner.y:F1},{corner.z:F1}) {d.magnitude:F1} m [{string.Join("; ", hits)}]";
                }
            }
        }
        return "";
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
