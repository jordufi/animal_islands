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

    public Kind TaskKind => kind;

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
        if (before != null && before.activeSelf == done) before.SetActive(!done);
        if (after != null && after.activeSelf != done) after.SetActive(done);
        if (worldLabel == null) return;
        switch (kind)
        {
            case Kind.Shovel: worldLabel.text = done ? "" : "PALAS\n" + ShovelCost + " conchas"; break;
            case Kind.Grave: worldLabel.text = done ? "GIUSEPPE\n(desenterrado)" : "AQUÍ YACE\nGIUSEPPE"; break;
            default: worldLabel.text = ""; break;
        }
    }

    public override string Prompt(PlayerInteractor player)
    {
        if (Mission == null || Done) return "";
        switch (kind)
        {
            case Kind.Shovel: return "□  Comprar la pala (" + CostText(ShovelCost) + ")";
            case Kind.Grave: return Mission.Shovel ? "□  Desenterrar a Giuseppe" : "Para cavar hace falta una pala (se vende en el Mercato)";
            default: return "□  Hablar con el gondolero de oro";
        }
    }

    public override bool CanInteract(PlayerInteractor player) => Mission != null && !Done && (kind != Kind.Grave || Mission.Shovel);

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority || Mission == null || Done) return;   // online client: PlayerInteractor asks the host
        switch (kind)
        {
            case Kind.Shovel: if (Pay(ShovelCost, player)) Mission.BuyShovel(); break;
            case Kind.Grave: Mission.Dig(); break;
            default: Mission.MeetGondolier(); break;
        }
    }
}
