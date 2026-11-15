using UnityEngine;

// "El cuadro eléctrico": dar la luz. Until someone does, drink machines and the Microondas are off
// (BoatMission.PowerOn). One per map, far from the start.
public class PowerSwitch : Interactable
{
    [SerializeField] Renderer lamp;        // turns green with the light on
    [SerializeField] Transform lever;      // flips down
    [SerializeField] TextMesh worldLabel;

    bool shown;

    void Start() => Refresh(true);

    void Update() => Refresh(false);

    void Refresh(bool force)
    {
        bool on = BoatMission.Instance != null && BoatMission.Instance.Power;
        if (on == shown && !force) return;
        shown = on;
        if (lamp != null) lamp.material.color = on ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f);
        if (lever != null) lever.localRotation = Quaternion.Euler(on ? 60f : -60f, 0f, 0f);
        if (worldLabel != null) worldLabel.text = on ? "CUADRO ELÉCTRICO\n(con luz)" : "CUADRO ELÉCTRICO\nDAR LA LUZ";
    }

    public override string Prompt(PlayerInteractor player) => BoatMission.Instance != null && !BoatMission.Instance.Power ? "□  Dar la luz" : "";
    public override bool CanInteract(PlayerInteractor player) => BoatMission.Instance != null && !BoatMission.Instance.Power;

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || BoatMission.Instance == null) return;
        BoatMission.Instance.TurnPowerOn();
    }
}
