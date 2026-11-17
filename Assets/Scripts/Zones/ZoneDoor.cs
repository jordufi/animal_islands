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
    static bool Pueblo => BoatMission.Instance != null && BoatMission.Instance.Pueblo;
    bool Free => requiresMission && BoatMission.Instance != null && BoatMission.Instance.HasMission;

    string Destination()
    {
        foreach (var z in connects) if (z != null && !z.IsOpen) return z.displayName;
        return connects.Length > 0 && connects[0] != null ? connects[connects.Length - 1].displayName : "";
    }

    // ---- one door per round (user feedback 1, idea 5): the team opens at most EconomyData.doorsPerRound doors a round.
    // Every ZoneDoor counts, the dock / mission door too (decided by the user); the Portón del encierro is not a ZoneDoor
    // and the debug opens don't count. Online every machine counts the same (Interact on the host, NetOpened on clients).
    static int countRound = -1, countInRound;
    public static bool IgnoreRoundLimit;   // play tests that open several doors in one round
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRoundCount() { countRound = -1; countInRound = 0; IgnoreRoundLimit = false; }
    static int CurrentRound => RoundManager.Instance != null ? RoundManager.Instance.Round : 0;
    static int RoundLimit => GameManager.Instance != null && GameManager.Instance.Economy != null ? GameManager.Instance.Economy.doorsPerRound : 0;
    public static bool RoundLimitReached => !IgnoreRoundLimit && RoundLimit > 0 && countRound == CurrentRound && countInRound >= RoundLimit;
    static void CountOpened()
    {
        if (countRound != CurrentRound) { countRound = CurrentRound; countInRound = 0; }
        countInRound++;
    }
    const string LimitText = "Ya habéis abierto una puerta esta ronda: aguantad hasta la siguiente";

    bool HasKey(PlayerInteractor player) => acceptsMasterKey && MasterKey.Instance != null && MasterKey.Instance.IsHeldBy(player);

    void Update()
    {
        if (worldLabel != null)
            worldLabel.text = doorName + "\n→ " + Destination() + "\n" + (Locked ? "CERRADO: falta el " + BoatMission.Vehicle : Free ? "ABRIR" : Cost + " conchas" + (acceptsMasterKey ? "\no la LLAVE MAESTRA" : ""));
    }

    public override bool CanInteract(PlayerInteractor player) => !Locked;

    public override string Prompt(PlayerInteractor player) => RoundLimitReached && !Locked ? LimitText : Locked ? (Pueblo ? "Cerrado. " : "Muelle cerrado. ") + BoatMission.Instance.Objective()
        : Free ? (BoatMission.Instance.DoorNeedsKey && !MasterKey.Instance.IsHeldBy(player) ? (Pueblo ? "Hace falta la LLAVE del tractor" : "Hace falta la LLAVE del vaporetto") : "□  Abrir hacia " + Destination())
        : HasKey(player)
        ?"□  Abrir hacia " + Destination() + " con la LLAVE MAESTRA (gratis)"
        : "□  Abrir hacia " + Destination() + " (" + CostText(Cost) + ")";

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority) return;   // online client: PlayerInteractor asks the host instead
        if (Locked) return;
        if (RoundLimitReached) { Game.ShowMessageTo(player, LimitText); return; }
        if (Free)
        {
            // task-list maps: the last door also wants the key in the opener's pocket
            var bm = BoatMission.Instance;
            if (bm != null && bm.DoorNeedsKey && !MasterKey.Instance.Use(player)) { Game.ShowMessageTo(player, (Pueblo ? "Hace falta la LLAVE del tractor" : "Hace falta la LLAVE del vaporetto")); return; }
        }
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
        CountOpened();
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
        CountOpened();
        if (barrier != null) Destroy(barrier);
        Destroy(gameObject);
    }
}
