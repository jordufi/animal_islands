using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// A staff door ('$' in a layout with floors, Docs/PLANTAS_FORMATO.md): only whoever wears the pulsera de puertas
// (Hotel Jubiles) opens it, and only when the zones on both sides are open. It stays open a few seconds, so an old man
// right behind you gets through too (the rule: old men can always follow your route). Closed it carves the NavMesh;
// the setup checks treat it as always closed (NavMesh area "Servicio").
public class ServiceDoor : Interactable
{
    // Set by the map that has the bracelet (hotel). Without one, nobody can open these doors.
    public static System.Func<PlayerInteractor, bool> HasDoorBracelet = p => false;

    [SerializeField] GameObject panel;
    [SerializeField] Zone[] zones;
    [SerializeField] float openTime = 3f;

    public bool IsOpenNow { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => HasDoorBracelet = p => false;

    public void Configure(GameObject doorPanel, Zone[] sides)
    {
        panel = doorPanel;
        zones = sides;
    }

    bool ZonesOpen()
    {
        if (zones == null) return true;
        foreach (var z in zones) if (z != null && !z.IsOpen) return false;
        return true;
    }

    public override bool CanInteract(PlayerInteractor player) => !IsOpenNow;

    public override string Prompt(PlayerInteractor player)
    {
        if (!HasDoorBracelet(player)) return "SOLO PERSONAL (hace falta la pulsera de puertas)";
        if (!ZonesOpen()) return "SOLO PERSONAL (al otro lado está cerrado)";
        return "Abrir la puerta de servicio";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!HasDoorBracelet(player) || !ZonesOpen()) { if (Game != null) Game.ShowMessageTo(player, Prompt(player)); return; }
        StartCoroutine(OpenFor(openTime));
    }

    public void DebugOpen(float seconds) => StartCoroutine(OpenFor(seconds));

    IEnumerator OpenFor(float seconds)
    {
        IsOpenNow = true;
        SetPanel(false);
        yield return new WaitForSeconds(seconds);
        SetPanel(true);
        IsOpenNow = false;
    }

    void SetPanel(bool closed)
    {
        if (panel == null) return;
        foreach (var c in panel.GetComponentsInChildren<Collider>()) c.enabled = closed;
        foreach (var r in panel.GetComponentsInChildren<Renderer>()) r.enabled = closed;
        var ob = panel.GetComponent<NavMeshObstacle>();
        if (ob != null) ob.enabled = closed;
    }
}
