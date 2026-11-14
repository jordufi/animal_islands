using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// "Máquina de Granizados": the only way to get weapons. Pay, it spins, offers a random weapon.
// It can be used ONCE; after that it disappears and only comes back (at a random box spot of an
// already open zone) when a new zone is opened. If firstZones is set (Isla del Faro: Parking, Volley) the
// box is not on the map at the start: it first appears in one of those zones when it is opened.
public class MysteryBox : Interactable
{
    enum State { Ready, Rolling, Offering, Gone }

    [SerializeField] WeaponData[] pool;
    [SerializeField] int baseCost = 950;
    [SerializeField] float jokeChance = 0.06f;
    [SerializeField] float rollTime = 2.5f;
    [SerializeField] float offerTime = 8f;
    [SerializeField] GameObject visual;
    [SerializeField] TextMesh worldLabel;
    [SerializeField] TextMesh rollLabel;
    [Tooltip("If set, the box is hidden at the start and first appears in one of these zones when it opens.")]
    [SerializeField] Zone[] firstZones;

    State state = State.Ready;
    WeaponData offered;
    Transform currentSpot;

    public int Cost => Mathf.RoundToInt(baseCost * RunModifiers.Get(Stat.BoxCost) / 10f) * 10;
    public string StateName => state.ToString();          // tests
    public WeaponData Offered => offered;                  // tests
    public bool IsAvailable => state == State.Ready;
    public bool WaitingForFirstZone => waitingFirst;
    public Transform CurrentSpot => currentSpot;

    bool waitingFirst;

    void Start()
    {
        if (Net.IsClient)
        {
            // online client: the host says where it is and what it does (ReadNet)
            state = State.Gone;
            SetVisible(false);
            SetRollText("");
            return;
        }
        waitingFirst = firstZones != null && System.Array.Exists(firstZones, z => z != null);
        if (ZoneManager.Instance != null)
        {
            ZoneManager.Instance.ZoneOpened += OnZoneOpened;
            if (!waitingFirst) MoveToRandomSpot();
        }
        if (waitingFirst)
        {
            state = State.Gone;
            SetVisible(false);
        }
        SetRollText("");
        Sync();
    }

    void OnDestroy()
    {
        if (ZoneManager.Instance != null) ZoneManager.Instance.ZoneOpened -= OnZoneOpened;
    }

    void Update()
    {
        if (worldLabel != null) worldLabel.text = state == State.Ready ? "MÁQUINA DE GRANIZADOS\n" + Cost + " conchas" : "";
    }

    void OnZoneOpened(Zone zone)
    {
        if (waitingFirst)
        {
            if (System.Array.IndexOf(firstZones, zone) < 0) return;   // not yet: the first box is in Parking / Volley
            var spots = zone.BoxSpots.ToList();
            if (spots.Count == 0) return;
            waitingFirst = false;
            currentSpot = spots[Random.Range(0, spots.Count)];
            transform.SetPositionAndRotation(currentSpot.position, currentSpot.rotation);
            state = State.Ready;
            SetVisible(true);
            if (Game != null) Game.ShowMessage("¡La Máquina de Granizados ha aparecido en " + zone.displayName + "!");
            Sync();
            return;
        }
        if (state != State.Gone) return;   // one box per opened zone: it only comes back if it was used
        MoveToRandomSpot();
        state = State.Ready;
        SetVisible(true);
        if (Game != null) Game.ShowMessage("La Máquina de Granizados ha reaparecido en algún sitio de la isla...");
        Sync();
    }

    void MoveToRandomSpot()
    {
        var spots = ZoneManager.Instance.OpenBoxSpots().ToList();
        if (spots.Count > 1 && currentSpot != null) spots.Remove(currentSpot);
        if (spots.Count == 0) return;
        currentSpot = spots[Random.Range(0, spots.Count)];
        transform.SetPositionAndRotation(currentSpot.position, currentSpot.rotation);
    }

    void SetVisible(bool on)
    {
        if (visual != null) visual.SetActive(on);
    }

    void SetRollText(string s)
    {
        if (rollLabel != null) rollLabel.text = s;
    }

    public override string Prompt(PlayerInteractor player)
    {
        switch (state)
        {
            case State.Ready: return "□  Máquina de Granizados: arma aleatoria (" + CostText(Cost) + ")";
            case State.Offering: return "□  Coger " + offered.displayName;
            default: return "";
        }
    }

    public override bool CanInteract(PlayerInteractor player) => state == State.Ready || state == State.Offering;

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority) return;   // online client: PlayerInteractor asks the host instead
        if (state == State.Ready)
        {
            if (!Pay(Cost, player)) return;
            StartCoroutine(Roll(player.Shooter));
        }
        else if (state == State.Offering)
        {
            player.Shooter.GiveWeapon(offered);
            Game.ShowMessage("¡" + offered.displayName + "!");
            StopAllCoroutines();
            StartCoroutine(Vanish(0.2f));
        }
    }

    IEnumerator Roll(PlayerShooter shooter)
    {
        state = State.Rolling;
        Sync();
        var candidates = pool.Where(w => w != null && (shooter == null || !shooter.HasWeapon(w))).ToList();
        if (candidates.Count == 0) candidates = pool.Where(w => w != null).ToList();

        for (float t = 0f; t < rollTime; t += 0.12f)
        {
            SetRollText(candidates[Random.Range(0, candidates.Count)].displayName);
            yield return new WaitForSeconds(0.12f);
        }

        if (Random.value < jokeChance)
        {
            SetRollText("¡GRANIZADO DE LIMÓN!");
            Sync("¡GRANIZADO DE LIMÓN!");
            Game.ShowMessage("Te ha tocado un granizado de limón. Refrescante. Inútil.");
            yield return new WaitForSeconds(2f);
            yield return Vanish(0f);
            yield break;
        }

        offered = PickWeighted(candidates);
        state = State.Offering;
        offerEnd = Time.time + offerTime;
        Sync();
        for (float t = offerTime; t > 0f; t -= Time.deltaTime)
        {
            SetRollText(offered.displayName + "\n" + Mathf.CeilToInt(t));
            yield return null;
        }
        Game.ShowMessage("La máquina se ha quedado tu arma. Haber estado atento.");
        yield return Vanish(0f);
    }

    static WeaponData PickWeighted(List<WeaponData> list)
    {
        float total = list.Sum(w => Mathf.Max(0.01f, w.boxWeight));
        float r = Random.value * total;
        foreach (var w in list)
        {
            r -= Mathf.Max(0.01f, w.boxWeight);
            if (r <= 0f) return w;
        }
        return list[list.Count - 1];
    }

    IEnumerator Vanish(float delay)
    {
        state = State.Gone;
        offered = null;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SetRollText("");
        SetVisible(false);
        Sync();
    }

    // ---------------------------------------------------------------- online
    float offerEnd;

    void Sync(string text = "")
    {
        if (NetGame.H != null) NetGame.H.BoxChanged(this, text);
    }

    public void WriteNet(NetBuf b, string text = "")
    {
        var net = NetGame.Instance;
        b.Byte((byte)state).Short(net != null ? net.BoxSpotIndex(currentSpot) : -1).Bool(visual == null || visual.activeSelf)
         .Str(offered != null ? offered.name : "").Float(Mathf.Max(0f, offerEnd - Time.time)).Str(text);
    }

    // Online client: what the host's box is doing.
    public void ReadNet(NetRead r)
    {
        var s = (State)r.Byte();
        int spot = r.Short();
        bool visible = r.Bool();
        var weapon = NetGame.FindAsset<WeaponData>(r.Str());
        float left = r.Float();
        string text = r.Str();
        var t = NetGame.Instance != null ? NetGame.Instance.BoxSpot(spot) : null;
        if (t != null) { currentSpot = t; transform.SetPositionAndRotation(t.position, t.rotation); }
        StopAllCoroutines();
        state = s;
        offered = weapon;
        SetVisible(visible);
        if (text.Length > 0) SetRollText(text);
        else if (s == State.Rolling) StartCoroutine(NetRolling());
        else if (s == State.Offering && weapon != null) StartCoroutine(NetOffering(left));
        else SetRollText("");
    }

    IEnumerator NetRolling()
    {
        var names = pool.Where(w => w != null).Select(w => w.displayName).ToList();
        while (names.Count > 0)
        {
            SetRollText(names[Random.Range(0, names.Count)]);
            yield return new WaitForSeconds(0.12f);
        }
    }

    IEnumerator NetOffering(float left)
    {
        for (float t = left; t > 0f; t -= Time.deltaTime)
        {
            SetRollText(offered.displayName + "\n" + Mathf.CeilToInt(t));
            yield return null;
        }
    }
}
