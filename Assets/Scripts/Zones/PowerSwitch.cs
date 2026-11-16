using UnityEngine;

// "El cuadro eléctrico": dar la luz. Until someone does, drink machines and the Microondas are off
// (BoatMission.PowerOn). Usually one per map, far from the start. Villajubilada has one per zone (`zone`): each
// lights its own zone (fiesta lights, the machines of that zone).
public class PowerSwitch : Interactable
{
    [SerializeField] Renderer lamp;        // turns green with the light on
    [SerializeField] Transform lever;      // flips down
    [SerializeField] TextMesh worldLabel;
    [SerializeField] string action = "Dar la luz";   // the bell of La Serenisima: "Tocar la campana"
    [SerializeField] Zone zone;            // null = the whole map
    [SerializeField] bool counts = true;   // false: not needed for "the whole village is lit" (zones you lose)

    bool shown;

    public Zone Zone => zone;
    public bool Counts => counts;
    public void SetZone(Zone z, bool neededForMission) { zone = z; counts = neededForMission; }

    bool On => BoatMission.Instance != null && (zone != null ? BoatMission.Instance.ZoneLit(zone) : BoatMission.Instance.Power);

    void Start() => Refresh(true);

    void Update() => Refresh(false);

    void Refresh(bool force)
    {
        bool on = On;
        if (on == shown && !force) return;
        shown = on;
        if (lamp != null) lamp.material.color = on ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f);
        if (lever != null) lever.localRotation = Quaternion.Euler(on ? 60f : -60f, 0f, 0f);
        if (worldLabel != null) worldLabel.text = zone != null ? "CUADRO DE LUCES\n" + (on ? "(encendido)" : zone.displayName.ToUpperInvariant())
            : on ? "CUADRO ELÉCTRICO\n(con luz)" : "CUADRO ELÉCTRICO\nDAR LA LUZ";
    }

    public override string Prompt(PlayerInteractor player) => BoatMission.Instance != null && !On ? "□  " + (zone != null ? "Encender las luces de " + zone.displayName : action) : "";
    public override bool CanInteract(PlayerInteractor player) => BoatMission.Instance != null && !On;

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || BoatMission.Instance == null) return;
        BoatMission.Instance.TurnPowerOn(zone);
    }
}
