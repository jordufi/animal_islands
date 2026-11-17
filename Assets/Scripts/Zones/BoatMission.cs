using System;
using UnityEngine;

// The way to the final boss (replaces "pay 10,000 for the dock"):
//   0. Dar la luz (PowerSwitch): drinks and the Microondas don't work without it.
//   1. Gasolina + 2. Radio (MissionItem pickups, shared by the team, never dropped).
//   3. Llamar al barco (CallStation, needs the light and both pieces; the Llave Maestra replaces ONE missing piece)
//      and hold out until the end of the next round(s) (EconomyData.missionDefenseRounds).
// Then the dock door (ZoneDoor.requiresMission) opens for free and the boss comes.
// Lives next to the GameManager. Maps without a CallStation have no mission (doors work as before); maps without
// a PowerSwitch always have light. Online: the host runs it and broadcasts the state (NetGame.QuestChanged).
public class BoatMission : MonoBehaviour
{
    public static BoatMission Instance { get; private set; }

    public bool Power { get; private set; }
    public bool Gas { get; private set; }
    public bool Radio { get; private set; }
    public bool Called { get; private set; }
    public int DefenseUntilRound { get; private set; }   // the boat arrives when this round ends
    public bool Complete { get; private set; }
    public bool HasSwitch { get; private set; }
    public bool HasMission { get; private set; }
    public event Action Changed;

    // ---- task list (La Serenisima): instead of gasolina + radio + emisora, a list of jobs spread over the barrios.
    // The vaporetto comes (after the usual defence round) when the first four are done; the fifth, the key, opens the
    // last door. A map is in this mode when it has MissionTask objects. Discs and key are read from their own systems.
    public bool HasTasks { get; private set; }
    public bool Shovel { get; private set; }
    public bool Giuseppe { get; private set; }
    public bool Gondolier { get; private set; }
    public int GondolierSpot { get; private set; } = -1;   // which of the possible spots has him (host picks, B2)
    public bool DebugSkipped { get; private set; }

    // ---- Villajubilada: the same task list with other words (flavor "pueblo": Shovel = the lighter, Giuseppe = the
    // chupinazo, Gondolier = the golden cabezudo) and one fuse box per zone instead of a single switch.
    [SerializeField] string flavor = "";
    public bool Pueblo => flavor == "pueblo";
    // The rescue vehicle and where it waits, per map (feedback 2026-11-17: the village still talked about a boat).
    public static string Vehicle => Instance != null && Instance.Pueblo ? "tractor" : Instance != null && Instance.HasTasks ? "vaporetto" : "barco";
    public static string BoardText => Instance != null && Instance.Pueblo ? "Sube al remolque del tractor"
        : Instance != null && Instance.HasTasks ? "Sube al vaporetto al final del embarcadero" : "Sube al barco al final del muelle";
    PowerSwitch[] zoneSwitches = new PowerSwitch[0];   // sorted by zone name: the same order on every machine
    int litMask;
    public bool HasZonePower => zoneSwitches.Length > 0;
    public bool ZoneLit(Zone z)
    {
        for (int i = 0; i < zoneSwitches.Length; i++) if (zoneSwitches[i].Zone == z) return (litMask & (1 << i)) != 0;
        return Power;
    }
    public int LitZones { get { int n = 0; for (int i = 0; i < zoneSwitches.Length; i++) if (zoneSwitches[i].Counts && (litMask & (1 << i)) != 0) n++; return n; } }
    public int ZonesToLight { get { int n = 0; foreach (var s in zoneSwitches) if (s.Counts) n++; return n; } }
    // May the machines of this zone be used? (null zone, or a map with a single switch = the old rule)
    public static bool PowerOnFor(Zone z) => Instance == null || z == null || !Instance.HasZonePower ? PowerOn : Instance.ZoneLit(z);
    public bool DiscsDone => EasterEgg.Instance != null && EasterEgg.Instance.Completed;
    public bool KeyDone => MasterKey.Instance != null && (MasterKey.Instance.Holder != null || MasterKey.Instance.Used);
    // The last door also wants the key (not after the debug skip).
    public bool DoorNeedsKey => HasTasks && !DebugSkipped && MasterKey.Instance != null && !MasterKey.Instance.Used;

    public System.Collections.Generic.List<(string text, bool done)> TaskList()
    {
        var egg = EasterEgg.Instance;
        if (!HasTasks)   // petrol + radio maps (Isla del Faro): every step at once (feedback 2026-11-17)
        {
            var l = new System.Collections.Generic.List<(string, bool)>();
            if (HasSwitch) l.Add(("Dar la luz en el cuadro eléctrico", Power));
            if (HasMission)
            {
                l.Add(("Encontrar la gasolina", Gas));
                l.Add(("Encontrar la radio", Radio));
                l.Add(("Llamar al barco desde la emisora" + (MissingPieces == 1 ? " (la llave maestra vale por la pieza que falta)" : ""), Called || Complete));
            }
            return l;
        }
        if (Pueblo)
            return new System.Collections.Generic.List<(string, bool)>
            {
                ("Discos de Julio al karaoke del templete" + (egg != null && !egg.Completed ? " (" + Mathf.Min(egg.DiscsFound, egg.DiscsNeeded) + "/" + egg.DiscsNeeded + ")" : ""), DiscsDone),
                ("Encender todo el pueblo" + (Power ? "" : " (" + LitZones + "/" + ZonesToLight + ")"), Power),
                ("Encontrar al cabezudo de oro", Gondolier),
                (Shovel ? "Tirar el chupinazo desde el ayuntamiento (tienes el mechero)" : "Tirar el chupinazo (falta el mechero)", Giuseppe),
                ("Encontrar la llave del tractor", KeyDone),
            };
        return new System.Collections.Generic.List<(string, bool)>
        {
            ("Discos de Julio al karaoke" + (egg != null && !egg.Completed ? " (" + Mathf.Min(egg.DiscsFound, egg.DiscsNeeded) + "/" + egg.DiscsNeeded + ")" : ""), DiscsDone),
            ("Tocar la campana del campanile", Power),
            ("Encontrar al gondolero de oro", Gondolier),
            (Shovel ? "Desenterrar a Giuseppe (tienes la pala)" : "Desenterrar a Giuseppe (compra la pala)", Giuseppe),
            ("Encontrar la llave del vaporetto", KeyDone),
        };
    }

    public void BuyShovel()
    {
        if (!Net.Authority || Shovel) return;
        Shovel = true;
        RunLog.Note("PALA");
        Game.ShowMessage(Pueblo ? "¡Tenéis el MECHERO! El chupinazo se tira desde el balcón del ayuntamiento" : "¡Tenéis la PALA! Hay alguien esperando bajo tierra en el Cimitero");
        Notify();
    }

    public void Dig()
    {
        if (!Net.Authority || Giuseppe || !Shovel) return;
        Giuseppe = true;
        RunLog.Note("GIUSEPPE");
        Game.ShowMessage(Pueblo ? "¡CHUPINAZO! ¡Viva Villajubilada! El del tractor lo ha oído seguro" : "¡GIUSEPPE desenterrado! Llevaba cuarenta años esperando el vaporetto");
        Notify();
    }

    public void MeetGondolier()
    {
        if (!Net.Authority || Gondolier) return;
        Gondolier = true;
        RunLog.Note("GONDOLERO");
        Game.ShowMessage(Pueblo ? "¡El CABEZUDO DE ORO! Dice que él avisa al del tractor, que hagáis lo demás" : "¡El GONDOLERO DE ORO! Dice que él avisa al vaporetto, que hagáis lo demás");
        Notify();
    }

    void Update()
    {
        if (!HasTasks || !Net.Authority || Called || !(Power && Gondolier && Giuseppe && DiscsDone)) return;
        Called = true;
        RunLog.Note("TAREAS COMPLETAS");
        var rm = RoundManager.Instance;
        DefenseUntilRound = Mathf.Max(1, (rm != null ? rm.Round : 0) + DefenseRounds);
        Game.ShowMessage("¡Todo listo! " + (Pueblo ? "El tractor" : "El vaporetto") + " viene de camino: aguantad hasta el final de la ronda " + DefenseUntilRound);
        Notify();
    }

    // True when machines may be used (no switch on this map = always).
    public static bool PowerOn => Instance == null || !Instance.HasSwitch || Instance.Power;
    // True when the dock may be opened (no mission on this map = always).
    public static bool DockOpen => Instance == null || !Instance.HasMission || Instance.Complete;

    void Awake()
    {
        Instance = this;
        HasSwitch = FindAnyObjectByType<PowerSwitch>() != null;
        var zoned = new System.Collections.Generic.List<PowerSwitch>();
        foreach (var s in FindObjectsByType<PowerSwitch>()) if (s.Zone != null) zoned.Add(s);
        zoned.Sort((a, b) => string.CompareOrdinal(a.Zone.name, b.Zone.name));
        zoneSwitches = zoned.ToArray();
        HasTasks = FindAnyObjectByType<MissionTask>() != null;
        HasMission = HasTasks || FindAnyObjectByType<CallStation>() != null;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        var rm = RoundManager.Instance;
        if (rm != null) rm.RoundEnded += OnRoundEnded;
        if (Net.Authority && HasTasks)
        {
            int spots = 0;
            foreach (var t in FindObjectsByType<MissionTask>()) if (t.TaskKind == MissionTask.Kind.Gondolier) spots++;
            GondolierSpot = spots > 0 ? UnityEngine.Random.Range(0, spots) : -1;
        }
    }

    static GameManager Game => GameManager.Instance;
    int DefenseRounds => Game != null && Game.Economy != null ? Mathf.Max(1, Game.Economy.missionDefenseRounds) : 1;

    // ---------------------------------------------------------------- steps (offline / host)
    public void TurnPowerOn(Zone zone = null)
    {
        if (!Net.Authority) return;
        if (zone != null && HasZonePower)   // one zone: its lights and machines; the whole village lit = the task
        {
            int i = System.Array.FindIndex(zoneSwitches, s => s.Zone == zone);
            if (i < 0 || (litMask & (1 << i)) != 0) return;
            litMask |= 1 << i;
            RunLog.Note("LUZ " + zone.displayName);
            bool all = LitZones >= ZonesToLight;
            if (all && !Power) { Power = true; RunLog.Note("LUZ"); }
            Game.ShowMessage(all && zoneSwitches[i].Counts ? "¡TODO EL PUEBLO ENCENDIDO! Que empiecen las fiestas" : "¡Luces en " + zone.displayName + "! Sus máquinas ya funcionan");
            Notify();
            return;
        }
        if (Power) return;
        Power = true;
        RunLog.Note("LUZ");
        Game.ShowMessage(HasTasks ? "¡DONG! Suenan las campanas: hay luz, las bebidas y el Microondas ya funcionan" : "¡HAY LUZ! Las bebidas y el Microondas ya funcionan");
        Notify();
    }

    public void Pick(MissionItem.Kind kind)
    {
        if (!Net.Authority) return;
        if (kind == MissionItem.Kind.Gasolina) { if (Gas) return; Gas = true; RunLog.Note("GASOLINA"); Game.ShowMessage("¡Tenéis la GASOLINA para el barco!"); }
        else { if (Radio) return; Radio = true; RunLog.Note("RADIO"); Game.ShowMessage("¡Tenéis la RADIO para llamar al barco!"); }
        Notify();
    }

    public int MissingPieces => (Gas ? 0 : 1) + (Radio ? 0 : 1);

    // What the station says to this player ("" = can call).
    public string CallBlocker(PlayerInteractor player)
    {
        if (Complete) return "El barco ya está en el muelle";
        if (Called) return "Barco en camino: aguantad hasta el final de la ronda " + DefenseUntilRound;
        if (!PowerOn) return "La radio no tiene corriente: dad la luz";
        int missing = MissingPieces;
        bool key = MasterKey.Instance != null && MasterKey.Instance.IsHeldBy(player);
        if (missing == 0 || (missing == 1 && key)) return "";
        return "Falta " + (!Gas && !Radio ? "la GASOLINA y la RADIO" : !Gas ? "la GASOLINA" : "la RADIO") + (missing == 1 ? " (o la LLAVE MAESTRA)" : "");
    }

    public void Call(PlayerInteractor player)
    {
        if (!Net.Authority || CallBlocker(player).Length > 0) return;
        if (MissingPieces == 1)
        {
            if (MasterKey.Instance == null || !MasterKey.Instance.Use(player)) return;
            if (!Gas) Gas = true; else Radio = true;
            RunLog.Note("LLAVE POR PIEZA");
            Game.ShowMessage("La LLAVE MAESTRA abre la taquilla del farero: dentro estaba la pieza que faltaba");
        }
        Called = true;
        RunLog.Note("LLAMADA");
        var rm = RoundManager.Instance;
        int round = rm != null ? rm.Round : 0;
        // the round in progress doesn't count (in the intermission `round` is already over): the boat comes when
        // a round that starts after the call ends
        DefenseUntilRound = Mathf.Max(1, round + DefenseRounds);
        Game.ShowMessage("«Aquí Salvamento Marítimo: vamos para allá». ¡Aguantad hasta el final de la ronda " + DefenseUntilRound + "!");
        Notify();
    }

    void OnRoundEnded(int round)
    {
        if (!Net.Authority || !Called || Complete || round < DefenseUntilRound) return;
        Finish();
    }

    void Finish()
    {
        Complete = true;
        RunLog.Note("BARCO");
        Game.ShowMessage(Pueblo ? "¡EL TRACTOR ESTÁ EN LAS ERAS! Abrid la barricada con la llave... si Don Anselmo os deja" : HasTasks ? "¡EL VAPORETTO HA LLEGADO! Abrid la puerta con la llave... si Don Anselmo os deja" : "¡EL BARCO ESTÁ EN EL MUELLE! Abrid el muelle... si Don Anselmo os deja");
        Notify();
    }

    // Debug menu / tests: skip the whole thing.
    public void DebugComplete()
    {
        if (!Net.Authority) return;
        Power = Gas = Radio = Called = true;
        litMask = (1 << zoneSwitches.Length) - 1;
        Shovel = Giuseppe = Gondolier = DebugSkipped = true;
        if (!Complete) Finish(); else Notify();
    }

    void Notify()
    {
        Changed?.Invoke();
        if (NetGame.H != null) NetGame.H.QuestChanged();
    }

    // ---------------------------------------------------------------- online
    public void WriteNet(NetBuf b) => b.Bool(Power).Bool(Gas).Bool(Radio).Bool(Called).Bool(Complete).Int(DefenseUntilRound).Bool(Shovel).Bool(Giuseppe).Bool(Gondolier).Bool(DebugSkipped).Byte((byte)(GondolierSpot + 1)).Int(litMask);

    public void ReadNet(NetRead r)
    {
        Power = r.Bool(); Gas = r.Bool(); Radio = r.Bool(); Called = r.Bool(); Complete = r.Bool(); DefenseUntilRound = r.Int();
        Shovel = r.Bool(); Giuseppe = r.Bool(); Gondolier = r.Bool(); DebugSkipped = r.Bool();
        GondolierSpot = r.Byte() - 1;
        litMask = r.Int();
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- HUD
    public string Objective()
    {
        if (Game != null && Game.BossDefeated) return BoardText;
        if (!HasMission) return HasSwitch && !Power ? "Da la luz" : "";
        if (HasTasks)
        {
            if (Complete) return BossEncounter.Instance != null && BossEncounter.Instance.Boss != null ? "Acaba con Don Anselmo"
                : Pueblo ? (DoorNeedsKey ? "Abre la barricada de Las Eras con la llave" : "Abre la barricada de Las Eras")
                : DoorNeedsKey ? "Abre la puerta del vaporetto con la llave" : "Abre la puerta del vaporetto";
            if (Called) return "Aguanta hasta el final de la ronda " + DefenseUntilRound;
            var list = TaskList();
            int done = 0;
            foreach (var t in list) if (t.done) done++;
            return (Pueblo ? "Tareas del pregón " : "Tareas del vaporetto ") + done + "/" + list.Count;
        }
        if (Complete) return BossEncounter.Instance != null && BossEncounter.Instance.Boss != null ? "Acaba con Don Anselmo" : "Abre el muelle";
        if (Called) return "Aguanta hasta el final de la ronda " + DefenseUntilRound;
        string s = HasSwitch && !Power ? "Da la luz · " : "";
        if (!Gas || !Radio) return s + "Busca " + (!Gas && !Radio ? "la gasolina y la radio" : !Gas ? "la gasolina" : "la radio");
        return s + "Llama al barco por radio";
    }
}
