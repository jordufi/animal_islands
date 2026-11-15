using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Testing helpers (F1 / Share on the DualShock): conchas, god mode, end the round, force a night
// round, open every door, and show the collision boxes of the map (to find invisible walls).
public class DebugMenu : MonoBehaviour
{
    GameObject panel;
    GameState before;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || Net.IsClient) return;   // online: only the host has the test menu
        bool pressed = (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) ||
                       (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
        if (!pressed) return;
        if (panel != null) Close();
        else if (gm.State == GameState.Playing) Open();
    }

    void Open()
    {
        var gm = GameManager.Instance;
        before = gm.State;
        gm.SetState(GameState.Debug);
        UIKit.EnsureEventSystem();
        var canvas = UIKit.CreateCanvas("DebugMenu", 50);
        panel = canvas.gameObject;
        var bg = UIKit.Stretch("Bg", canvas.transform);
        UIKit.Image(bg, new Color(0f, 0f, 0f, 0.6f), true);
        UIKit.Label("Title", bg, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1000, 70), "MENÚ DE PRUEBAS", 52, TextAnchor.MiddleCenter, UIKit.Accent);
        UIKit.Label("Hint", bg, new Vector2(0.5f, 1f), new Vector2(0, -115), new Vector2(1000, 40), "F1 / Share para cerrar", 24);

        var buttons = new List<Selectable>();
        float y = 260f;
        Button Add(string label, UnityEngine.Events.UnityAction action)
        {
            var b = UIKit.Button("B" + buttons.Count, bg, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(640, 66), label, action, 30);
            buttons.Add(b);
            y -= 78f;
            return b;
        }
        void Relabel(Button b, string text) => b.GetComponentInChildren<Text>().text = text;

        Add("+5000 conchas", () => gm.AddPoints(5000, PointSource.Debug));
        Button god = null;
        god = Add(GodText(), () =>
        {
            bool on = gm.Player != null && !gm.Player.GodMode;
            foreach (var p in gm.Players) if (p != null) p.GodMode = on;   // co-op: the whole team
            Relabel(god, GodText());
        });
        Add("Terminar ronda ahora", () => { if (RoundManager.Instance != null) RoundManager.Instance.DebugEndRound(); Close(); });
        Button night = null;
        night = Add(NightText(), () => { var rm = RoundManager.Instance; if (rm != null) rm.ForceNextNight = !rm.ForceNextNight; Relabel(night, NightText()); });
        Button cols = null;
        cols = Add(ColText(), () => { ColliderViewer.Toggle(); Relabel(cols, ColText()); });
        Add("Abrir todas las puertas (sin cartas)", () =>
        {
            foreach (var d in FindObjectsByType<ZoneDoor>()) d.DebugOpen();
            Close();
        });
        Add("Dar la luz y traer el barco (misión hecha)", () => { if (BoatMission.Instance != null) BoatMission.Instance.DebugComplete(); Close(); });
        Add("Cerrar", Close);
        UIKit.ChainVertical(buttons.ToArray());
        UIKit.Select(buttons[0]);
    }

    static string GodText() => "Modo dios: " + (GameManager.Instance != null && GameManager.Instance.Player != null && GameManager.Instance.Player.GodMode ? "SÍ" : "NO");
    static string NightText() => "Siguiente ronda de noche: " + (RoundManager.Instance != null && RoundManager.Instance.ForceNextNight ? "SÍ" : "NO");
    static string ColText() => "Ver colisiones: " + (ColliderViewer.Visible ? "SÍ" : "NO");

    void Close()
    {
        if (panel != null) Destroy(panel);
        panel = null;
        var gm = GameManager.Instance;
        if (gm != null && gm.State == GameState.Debug) gm.SetState(before == GameState.Debug ? GameState.Playing : before);
    }
}

// Draws every collider of the map as a translucent box (ground excluded): green = props/default,
// orange = Prop layer, blue = walls / rocks, cyan = water and nets, red = doors. Toggle from the debug menu.
public static class ColliderViewer
{
    static GameObject root;
    public static bool Visible => root != null;

    public static void Toggle()
    {
        if (root != null) { Object.Destroy(root); root = null; return; }
        root = new GameObject("ColliderViewer");
        var mats = new Dictionary<int, Material>();
        Material Mat(int layer)
        {
            if (mats.TryGetValue(layer, out var m)) return m;
            Color c = layer == NavAgents.PropLayer ? new Color(1f, 0.55f, 0.1f, 0.4f)
                    : layer == 11 ? new Color(0.3f, 0.45f, 1f, 0.3f)
                    : layer == 4 ? new Color(0.2f, 1f, 1f, 0.35f)
                    : layer == 10 ? new Color(1f, 0.15f, 0.15f, 0.4f)
                    : new Color(0.2f, 1f, 0.3f, 0.4f);
            return mats[layer] = new Material(Shader.Find("Sprites/Default")) { color = c };
        }
        foreach (var col in Object.FindObjectsByType<Collider>())
        {
            if (!col.enabled || col.isTrigger) continue;
            int layer = col.gameObject.layer;
            if (layer == 8 || layer == 9 || layer == 5) continue;   // player, enemies, UI
            var b = col.bounds;
            if (b.max.y < 0.05f) continue;                          // ground tiles
            GameObject g;
            if (col is BoxCollider box)
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.transform.SetPositionAndRotation(box.transform.TransformPoint(box.center), box.transform.rotation);
                g.transform.localScale = Vector3.Scale(box.size, box.transform.lossyScale) + Vector3.one * 0.03f;
            }
            else if (col is CapsuleCollider cap)
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                float s = Mathf.Max(cap.transform.lossyScale.x, cap.transform.lossyScale.z);
                g.transform.position = cap.transform.TransformPoint(cap.center);
                // capsule axis (leaning palm trunks): Y by default, X / Z by direction
                g.transform.rotation = cap.transform.rotation * (cap.direction == 0 ? Quaternion.Euler(0, 0, 90) : cap.direction == 2 ? Quaternion.Euler(90, 0, 0) : Quaternion.identity);
                g.transform.localScale = new Vector3(cap.radius * 2f * s, cap.height * cap.transform.lossyScale.y / 2f, cap.radius * 2f * s);
            }
            else
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.transform.position = b.center;
                g.transform.localScale = b.size + Vector3.one * 0.03f;
            }
            Object.Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = Mat(layer);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.SetParent(root.transform, true);
        }
    }
}
