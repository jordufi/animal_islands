using UnityEngine;

// A piece of the boat mission lying on the map (the petrol can, the radio). Picking it up is for the whole team:
// it is never carried or dropped (see BoatMission).
public class MissionItem : Interactable
{
    public enum Kind { Gasolina, Radio }

    [SerializeField] Kind kind;
    [SerializeField] Transform visual;
    [SerializeField] TextMesh worldLabel;

    public Kind ItemKind => kind;

    bool Taken => BoatMission.Instance != null && (kind == Kind.Gasolina ? BoatMission.Instance.Gas : BoatMission.Instance.Radio);

    void Update()
    {
        bool show = !Taken;
        if (visual != null)
        {
            if (visual.gameObject.activeSelf != show) visual.gameObject.SetActive(show);
            if (show)
            {
                visual.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                visual.localPosition = new Vector3(0f, 0.8f + Mathf.Sin(Time.time * 2.5f) * 0.1f, 0f);
            }
        }
        if (worldLabel != null && worldLabel.gameObject.activeSelf != show) worldLabel.gameObject.SetActive(show);
    }

    string Name => kind == Kind.Gasolina ? "la GASOLINA" : "la RADIO";

    public override string Prompt(PlayerInteractor player) => BoatMission.Instance != null && !Taken ? "□  Coger " + Name : "";
    public override bool CanInteract(PlayerInteractor player) => BoatMission.Instance != null && !Taken;

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || BoatMission.Instance == null) return;
        BoatMission.Instance.Pick(kind);
    }
}
