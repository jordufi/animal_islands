using UnityEngine;
using UnityEngine.InputSystem;

// Who this player is in a co-op run: index (J1..J4), its own copy of the input actions (bound to its
// devices only), its camera and its HUD. Added by the GameManager before the player's components wake up,
// so they read their input from Actions instead of the shared asset.
public class PlayerSlot : MonoBehaviour
{
    public static readonly Color[] Colors =
    {
        new Color(1f, 0.72f, 0.2f),    // J1 orange-gold
        new Color(0.35f, 0.75f, 1f),   // J2 blue
        new Color(1f, 0.4f, 0.6f),     // J3 pink
        new Color(0.55f, 1f, 0.45f),   // J4 green
    };

    public int Index { get; set; }
    public InputActionAsset Actions { get; set; }
    public ThirdPersonCamera CameraRig { get; set; }
    public Camera Camera => CameraRig != null ? CameraRig.GetComponent<Camera>() : null;
    public PlayerHUD Hud { get; set; }
    public AnimalData Animal { get; set; }
    public string Tag => "J" + (Index + 1);
    public Color Color => Colors[Index % Colors.Length];

    // per-player stats for the end screen
    public int Kills { get; set; }
    public int Headshots { get; set; }
    public int Downs { get; set; }
    public int Revives { get; set; }

    // Online: a player controlled on another machine (a proxy here: no camera, no input, no HUD).
    public bool IsRemote { get; set; }
    public bool Gone { get; set; }               // left the match (online)
    public Vector3 RemoteLook { get; set; } = Vector3.forward;   // where their camera looks (spawn choice)

    // The actions a player component should read: its slot's copy, or the shared asset (solo / no slot).
    public static InputActionAsset ActionsFor(Component c, InputActionAsset fallback)
    {
        var slot = c.GetComponentInParent<PlayerSlot>(true);
        return slot != null && slot.Actions != null ? slot.Actions : fallback;
    }

    public static PlayerSlot Of(Component c) => c != null ? c.GetComponentInParent<PlayerSlot>(true) : null;

    // Online: true for a remote player's proxy.
    public static bool IsProxy(Component c) { var s = Of(c); return s != null && s.IsRemote; }

    // The camera that looks at this player (falls back to the main camera; none for a remote player).
    public static Camera CameraOf(Component c)
    {
        var slot = Of(c);
        if (slot != null && slot.IsRemote) return null;
        var cam = slot != null ? slot.Camera : null;
        return cam != null ? cam : Camera.main;
    }

    public static ThirdPersonCamera RigOf(Component c)
    {
        var slot = Of(c);
        if (slot != null && slot.IsRemote) return null;
        if (slot != null && slot.CameraRig != null) return slot.CameraRig;
        return GameManager.Instance != null ? GameManager.Instance.CameraRig : null;
    }

    // Flat direction this player's camera looks at.
    public static Vector3 LookOf(Component c)
    {
        var slot = Of(c);
        Vector3 look;
        if (slot != null && slot.IsRemote) look = slot.RemoteLook;
        else { var cam = CameraOf(c); look = cam != null ? cam.transform.forward : c.transform.forward; }
        look.y = 0f;
        return look.sqrMagnitude > 0.001f ? look.normalized : Vector3.forward;
    }
}
