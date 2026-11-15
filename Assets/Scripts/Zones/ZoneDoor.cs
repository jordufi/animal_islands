using UnityEngine;

// A paid barrier between zones. Buying it opens the zones behind it, removes the barrier
// (and its NavMeshObstacle, so enemies can path through) and offers a blessing/curse card.
// A door that accepts the Llave Maestra opens for free for the player carrying it.
public class ZoneDoor : Interactable
{
    [SerializeField] string doorName = "Barrera";
    [SerializeField] int cost = 750;
    [SerializeField] Zone[] connects;
    [SerializeField] GameObject barrier;
    [SerializeField] TextMesh worldLabel;
    [SerializeField] bool acceptsMasterKey;

    public int Cost => Mathf.RoundToInt(cost * RunModifiers.Get(Stat.DoorCost) / 10f) * 10;
    [SerializeField] bool requiresMission;   // the dock: free, but only once the boat mission is complete

    public bool AcceptsMasterKey => acceptsMasterKey;
    public bool RequiresMission => requiresMission;
    bool Locked => requiresMission && !BoatMission.DockOpen;
    bool Free => requiresMission && BoatMission.Instance != null && BoatMission.Instance.HasMission;

    string Destination()
    {
        foreach (var z in connects) if (z != null && !z.IsOpen) return z.displayName;
        return connects.Length > 0 && connects[0] != null ? connects[connects.Length - 1].displayName : "";
    }

    bool HasKey(PlayerInteractor player) => acceptsMasterKey && MasterKey.Instance != null && MasterKey.Instance.IsHeldBy(player);

    void Update()
    {
        if (worldLabel != null)
            worldLabel.text = doorName + "\n→ " + Destination() + "\n" + (Locked ? "CERRADO: falta el barco" : Free ? "ABRIR" : Cost + " conchas" + (acceptsMasterKey ? "\no la LLAVE MAESTRA" : ""));
    }

    public override bool CanInteract(PlayerInteractor player) => !Locked;

    public override string Prompt(PlayerInteractor player) => Locked ? "Muelle cerrado. " + BoatMission.Instance.Objective()
        : Free ? "□  Abrir hacia " + Destination()
        : HasKey(player)
        ?"□  Abrir hacia " + Destination() + " con la LLAVE MAESTRA (gratis)"
        : "□  Abrir hacia " + Destination() + " (" + CostText(Cost) + ")";

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority) return;   // online client: PlayerInteractor asks the host instead
        if (Locked) return;
        if (Free) { }
        else if (HasKey(player)) MasterKey.Instance.Use(player);
        else if (!Pay(Cost, player)) return;
        bool newZone = false;
        foreach (var z in connects)
            if (ZoneManager.Instance != null && ZoneManager.Instance.Open(z))
            {
                newZone = true;
                Game.ShowMessage("¡Zona abierta: " + z.displayName + "!");
            }
        Game.DoorsOpened++;
        if (barrier != null) Destroy(barrier);
        if (NetGame.H != null) NetGame.H.DoorOpened(this);   // before the cards: the zones open first everywhere
        if (CardManager.Instance != null) CardManager.Instance.Offer(player);   // whoever opened it chooses
        if (!newZone) Game.ShowMessage("Paso abierto");
        Destroy(gameObject);
    }

    // Debug menu: open without paying and without the card offer.
    public void DebugOpen()
    {
        foreach (var z in connects) if (ZoneManager.Instance != null) ZoneManager.Instance.Open(z);
        if (barrier != null) Destroy(barrier);
        if (NetGame.H != null) NetGame.H.DoorOpened(this);
        Destroy(gameObject);
    }

    // Online client: the host opened it (zones, barrier; the toasts and cards come from the host).
    public void NetOpened()
    {
        foreach (var z in connects) if (ZoneManager.Instance != null) ZoneManager.Instance.Open(z);
        if (Game != null) Game.DoorsOpened++;
        if (barrier != null) Destroy(barrier);
        Destroy(gameObject);
    }
}
