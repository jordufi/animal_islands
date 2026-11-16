using UnityEngine;

// One job of the vaporetto task list (La Serenisima, see BoatMission.HasTasks):
//   Shovel    the stall that sells the shovel (EconomyData.shovelCost), for the whole team
//   Grave     Giuseppe's grave in the cemetery: needs the shovel
//   Gondolier the golden gondolier, hidden in a dead end: find him and talk to him
// The state lives in BoatMission (the host runs it and broadcasts it); this only shows it.
public class MissionTask : Interactable
{
    public enum Kind { Shovel, Grave, Gondolier }

    [SerializeField] Kind kind;
    [SerializeField] GameObject before;   // shown until done (the shovel on the stall, the mound, ...)
    [SerializeField] GameObject after;    // shown once done (the open grave with Giuseppe)
    [SerializeField] TextMesh worldLabel;
    [SerializeField] int spot;            // Gondolier: index among the possible spots (BoatMission.GondolierSpot)
    [SerializeField] GameObject spotRoot; // Gondolier: figure + blocker, hidden unless this spot was picked
    [SerializeField] bool free;           // Villajubilada: the lighter lying on the ground (the stall sells it otherwise)
    [SerializeField] Zone liesIn;         // ... only in the start zone that was opened second (Encierro.SecondZone)

    public Kind TaskKind => kind;
    public int Spot => spot;
    public bool Here => liesIn != null ? Encierro.Instance != null && Encierro.Instance.SecondZone == liesIn && Encierro.Instance.ActOne
        : kind != Kind.Gondolier || Mission == null || Mission.GondolierSpot < 0 || Mission.GondolierSpot == spot;
    public void SetFree(Zone zone) { free = true; liesIn = zone; }
    static bool Pueblo => Mission != null && Mission.Pueblo;

    public void SetSpot(int index, GameObject root) { spot = index; spotRoot = root; }

    public void Configure(Kind k, GameObject shownBefore, GameObject shownAfter, TextMesh label)
    {
        kind = k; before = shownBefore; after = shownAfter; worldLabel = label;
    }

    static BoatMission Mission => BoatMission.Instance;
    int ShovelCost => Game != null && Game.Economy != null ? Game.Economy.shovelCost : 500;

    public bool Done
    {
        get
        {
            var m = Mission;
            if (m == null) return false;
            return kind == Kind.Shovel ? m.Shovel : kind == Kind.Grave ? m.Giuseppe : m.Gondolier;
        }
    }

    void Update()
    {
        bool done = Done;
        if (spotRoot != null && spotRoot.activeSelf != Here) spotRoot.SetActive(Here);
        if (liesIn != null) { if (before != null && before.activeSelf != (Here && !done)) before.SetActive(Here && !done); if (worldLabel != null) worldLabel.text = Here && !done ? "MECHERO" : ""; return; }
        if (before != null && before.activeSelf == done) before.SetActive(!done);
        if (after != null && after.activeSelf != done) after.SetActive(done);
        if (worldLabel == null) return;
        switch (kind)
        {
            case Kind.Shovel: worldLabel.text = done ? "" : (Pueblo ? "MECHEROS\n" : "PALAS\n") + ShovelCost + " conchas"; break;
            case Kind.Grave: worldLabel.text = Pueblo ? (done ? "CHUPINAZO\n(tirado)" : "BALCÓN DEL\nAYUNTAMIENTO") : done ? "GIUSEPPE\n(desenterrado)" : "AQUÍ YACE\nGIUSEPPE"; break;
            default: worldLabel.text = ""; break;
        }
    }

    public override string Prompt(PlayerInteractor player)
    {
        if (Mission == null || Done || !Here) return "";
        switch (kind)
        {
            case Kind.Shovel: return free ? "□  Coger el mechero" : Pueblo ? "□  Comprar un mechero (" + CostText(ShovelCost) + ")" : "□  Comprar la pala (" + CostText(ShovelCost) + ")";
            case Kind.Grave: return Pueblo ? (Mission.Shovel ? "□  Tirar el chupinazo" : "Para el chupinazo hace falta un mechero") : Mission.Shovel ? "□  Desenterrar a Giuseppe" : "Para cavar hace falta una pala (se vende en el Mercato)";
            default: return Pueblo ? "□  Hablar con el cabezudo de oro" : "□  Hablar con el gondolero de oro";
        }
    }

    public override bool CanInteract(PlayerInteractor player) => Mission != null && !Done && Here && (kind != Kind.Grave || Mission.Shovel);

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || Mission == null || Done || !Here) return;   // online client: PlayerInteractor asks the host
        switch (kind)
        {
            case Kind.Shovel: if (free || Pay(ShovelCost, player)) Mission.BuyShovel(); break;
            case Kind.Grave: Mission.Dig(); break;
            default: Mission.MeetGondolier(); break;
        }
    }
}
