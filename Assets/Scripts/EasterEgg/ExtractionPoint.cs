using UnityEngine;

// The boat at the end of the dock. Available once the final boss is defeated. Co-op: every standing
// player has to be near the boat (CoopData.extractionGatherRadius); downed / bled-out ones are left behind.
public class ExtractionPoint : Interactable
{
    // Standing players still too far from the boat ("J2, J3").
    public string Missing()
    {
        var gm = Game;
        if (gm == null || !gm.IsCoop) return "";
        float r = gm.Coop != null ? gm.Coop.extractionGatherRadius : 10f;
        var far = new System.Collections.Generic.List<string>();
        foreach (var p in gm.Standing)
        {
            Vector3 d = p.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > r) { var slot = PlayerSlot.Of(p); far.Add(slot != null ? slot.Tag : "?"); }
        }
        return string.Join(", ", far);
    }

    [SerializeField] GameObject beacon;

    void Update()
    {
        if (beacon != null) beacon.SetActive(Game != null && Game.BossDefeated);
    }

    public override string Prompt(PlayerInteractor player)
    {
        if (Game == null || !Game.BossDefeated) return "";
        string missing = Missing();
        if (missing.Length > 0) return "Esperando a " + missing + " para subir al " + BoatMission.Vehicle;
        var carried = player.Carry != null ? player.Carry.Carried : null;
        return "□  Subir al " + BoatMission.Vehicle + " y escapar" + (carried != null ? " con " + carried.DisplayName : "");
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || Game == null || !Game.BossDefeated || Missing().Length > 0) return;
        Game.Victory();
    }
}
