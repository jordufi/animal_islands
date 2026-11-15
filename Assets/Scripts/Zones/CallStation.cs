using UnityEngine;

// "La emisora del farero": with the light on, the petrol and the radio (or the Llave Maestra instead of one of
// them) the team calls the rescue boat and has to hold out until it arrives (see BoatMission).
public class CallStation : Interactable
{
    [SerializeField] TextMesh worldLabel;

    void Update()
    {
        var m = BoatMission.Instance;
        if (worldLabel == null || m == null) return;
        worldLabel.text = "EMISORA\n" + (m.Complete ? "barco en el muelle" : m.Called ? "barco en camino" : "LLAMAR AL BARCO");
    }

    public override string Prompt(PlayerInteractor player)
    {
        var m = BoatMission.Instance;
        if (m == null) return "";
        string blocker = m.CallBlocker(player);
        if (blocker.Length > 0) return blocker;
        return m.MissingPieces == 1 ? "□  Llamar al barco (gasta la LLAVE MAESTRA)" : "□  Llamar al barco";
    }

    public override bool CanInteract(PlayerInteractor player) => BoatMission.Instance != null && BoatMission.Instance.CallBlocker(player).Length == 0;

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || BoatMission.Instance == null) return;
        BoatMission.Instance.Call(player);
    }
}
