using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// "Brújula de mierdecillas" (user feedback 1, idea 4; option GameSettings.Compass, in the main menu and in Ajustes): an
// arrow at the top of each player's view that points at a random task still to do (the light, mission pieces and
// tasks, the key, Julio's records, the karaoke, the dock, the way out), with its name and distance. When that task is
// done it picks another one. Local only: every player sees their own pick (each machine reads its own world state).
public class Compass : MonoBehaviour
{
    PlayerHealth player;
    Camera cam;
    RectTransform arrow;
    Text label;
    Transform target;
    string targetName = "";
    System.Func<bool> stillPending;
    float nextCheck;

    public Transform Target => target;          // tests
    public string TargetName => targetName;

    public static Compass Create(RectTransform view, PlayerHealth p, Camera camera)
    {
        var root = UIKit.Anchored("Compass", view, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(720f, 110f));
        var c = root.gameObject.AddComponent<Compass>();
        c.player = p;
        c.cam = camera;
        c.arrow = UIKit.Anchored("Arrow", root, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(60f, 60f));
        c.arrow.pivot = new Vector2(0.5f, 0.5f);
        var glyph = UIKit.Text(c.arrow, "▲", 48, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
        glyph.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        c.label = UIKit.Label("Label", root, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(720f, 30f), "", 22, TextAnchor.UpperCenter, new Color(1f, 0.92f, 0.6f));
        c.label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return c;
    }

    void Update()
    {
        if (player == null) return;
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 1f;
            if (target == null || stillPending == null || !stillPending() || !target.gameObject.activeInHierarchy) Pick();
        }
        bool show = target != null && !player.IsDead;
        arrow.gameObject.SetActive(show);
        label.gameObject.SetActive(show);
        if (!show) return;
        var view = cam != null ? cam.transform : player.transform;
        Vector3 fwd = view.forward; fwd.y = 0f;
        Vector3 to = target.position - player.transform.position; to.y = 0f;
        if (fwd.sqrMagnitude < 0.001f || to.sqrMagnitude < 0.001f) return;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
        arrow.localEulerAngles = new Vector3(0f, 0f, -angle);
        label.text = "Brújula de mierdecillas: " + targetName + " · " + Mathf.RoundToInt(to.magnitude) + " m";
    }

    // A random pending task, a different one from the last if there is a choice; else the nearest closed door.
    void Pick()
    {
        var list = Candidates();
        Transform last = target;
        target = null; stillPending = null; targetName = "";
        if (list.Count > 1) list.RemoveAll(c => c.t == last);
        if (list.Count > 0)
        {
            var c = list[Random.Range(0, list.Count)];
            target = c.t; targetName = c.name; stillPending = c.pending;
            return;
        }
        ZoneDoor best = null;
        float bestD = float.MaxValue;
        foreach (var d in FindObjectsByType<ZoneDoor>())
        {
            float dist = (d.transform.position - player.transform.position).sqrMagnitude;
            if (dist < bestD) { bestD = dist; best = d; }
        }
        if (best != null) { var door = best; target = door.transform; targetName = "la próxima puerta"; stillPending = () => door != null; }
    }

    static List<(Transform t, string name, System.Func<bool> pending)> Candidates()
    {
        var list = new List<(Transform, string, System.Func<bool>)>();
        var bm = BoatMission.Instance;
        bool pueblo = bm != null && bm.Pueblo;
        foreach (var sw in FindObjectsByType<PowerSwitch>())
        {
            var s = sw;
            System.Func<bool> pending = () => s != null && (bm == null || !bm.HasZonePower ? !BoatMission.PowerOn : s.Counts && !bm.ZoneLit(s.Zone));
            if (pending()) list.Add((s.transform, "el cuadro de luces", pending));
        }
        foreach (var it in FindObjectsByType<MissionItem>())
        {
            var i = it;
            System.Func<bool> pending = () => i != null && i.isActiveAndEnabled;
            if (pending()) list.Add((i.transform, i.ItemKind == MissionItem.Kind.Gasolina ? "la gasolina" : "la radio", pending));
        }
        foreach (var cs in FindObjectsByType<CallStation>())
        {
            var c = cs;
            System.Func<bool> pending = () => c != null && bm != null && bm.HasMission && !bm.Called && bm.MissingPieces == 0;
            if (pending()) list.Add((c.transform, "la emisora", pending));
        }
        foreach (var mt in FindObjectsByType<MissionTask>())
        {
            var m = mt;
            System.Func<bool> pending = () => m != null && m.isActiveAndEnabled && !m.Done && m.Here;
            if (!pending()) continue;
            string name = m.TaskKind == MissionTask.Kind.Shovel ? (pueblo ? "el mechero" : "la pala")
                : m.TaskKind == MissionTask.Kind.Grave ? (pueblo ? "el chupinazo" : "la tumba de Giuseppe")
                : (pueblo ? "el cabezudo dorado" : "el gondolero de oro");
            list.Add((m.transform, name, pending));
        }
        var key = MasterKey.Instance;
        if (key != null)
        {
            System.Func<bool> pending = () => key != null && key.Holder == null && !key.Used && key.isActiveAndEnabled;
            if (pending()) list.Add((key.transform, "la llave maestra", pending));
        }
        foreach (var dp in FindObjectsByType<DiscPickup>())
        {
            var d = dp;
            System.Func<bool> pending = () => d != null && d.isActiveAndEnabled;
            if (pending()) list.Add((d.transform, "un disco de Julio", pending));
        }
        var egg = EasterEgg.Instance;
        foreach (var kk in FindObjectsByType<Karaoke>())
        {
            var k = kk;
            System.Func<bool> pending = () => k != null && egg != null && !egg.Completed && egg.DiscsFound >= egg.DiscsNeeded;
            if (pending()) list.Add((k.transform, "el karaoke", pending));
        }
        foreach (var zd in FindObjectsByType<ZoneDoor>())
        {
            var d = zd;
            System.Func<bool> pending = () => d != null && d.RequiresMission && BoatMission.DockOpen;
            if (pending()) list.Add((d.transform, pueblo ? "Las Eras" : "el muelle", pending));
        }
        var gm = GameManager.Instance;
        foreach (var ep in FindObjectsByType<ExtractionPoint>())
        {
            var e = ep;
            System.Func<bool> pending = () => e != null && gm != null && gm.BossDefeated;
            if (pending()) list.Add((e.transform, "la salida", pending));
        }
        return list;
    }
}
