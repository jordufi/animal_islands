using UnityEngine;

public enum PowerUpKind : byte { MaxAmmo, InstaKill, DoublePoints, Nuke }

// Old men have a small chance to drop a power-up when they die (numbers in PowerUpData):
//   Nevera de munición (max ammo for the team), Siesta fulminante (insta-kill), 2x1 en conchas (double points),
//   Paella para todos (kills every old man on the map, pays a bonus).
// The timed ones are team-wide timers (InstaKill / DoublePoints). Online: the host drops them, decides who takes
// them and applies the effects; clients get the pickups and the timers (for the HUD).
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [SerializeField] PowerUpData data;

    int active, droppedThisRound;
    bool nuking;
    float instaUntil, doubleUntil;

    public static bool InstaKill => Instance != null && Time.time < Instance.instaUntil;
    public static bool DoublePoints => Instance != null && Time.time < Instance.doubleUntil;
    public float InstaKillLeft => Mathf.Max(0f, instaUntil - Time.time);
    public float DoublePointsLeft => Mathf.Max(0f, doubleUntil - Time.time);
    public PowerUpData Data => data;

    void Awake()
    {
        Instance = this;
        if (data == null) data = ScriptableObject.CreateInstance<PowerUpData>();   // scenes built before the asset existed
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (RoundManager.Instance != null) RoundManager.Instance.RoundStarted += _ => droppedThisRound = 0;
    }

    void OnEnable() => EnemyHealth.AnyEnemyDied += OnEnemyDied;
    void OnDisable() => EnemyHealth.AnyEnemyDied -= OnEnemyDied;

    void OnEnemyDied(EnemyHealth enemy, DamageInfo info)
    {
        if (!Net.Authority || nuking) return;   // online: the host drops them
        if (active >= data.maxActive || droppedThisRound >= data.maxPerRound || enemy.GetComponent<BossAI>() != null) return;
        float ammo = data.ammoDropChance * RunModifiers.Get(Stat.AmmoDropChance);
        float roll = Random.value;
        PowerUpKind kind;
        if (roll < ammo) kind = PowerUpKind.MaxAmmo;
        else if (roll < ammo + data.otherDropChance) kind = PickOther();
        else return;
        droppedThisRound++;
        Spawn(enemy.transform.position + Vector3.up * 0.6f, kind);
    }

    PowerUpKind PickOther()
    {
        float total = data.instaKillWeight + data.doublePointsWeight + data.nukeWeight;
        float r = Random.value * total;
        if (r < data.instaKillWeight) return PowerUpKind.InstaKill;
        if (r < data.instaKillWeight + data.doublePointsWeight) return PowerUpKind.DoublePoints;
        return PowerUpKind.Nuke;
    }

    public void Spawn(Vector3 position, PowerUpKind kind = PowerUpKind.MaxAmmo)
    {
        var pickup = Create(position, kind);
        if (NetGame.H != null) NetGame.H.PowerUpSpawned(pickup);
    }

    // Online client: the host dropped one here (the host also decides who takes it).
    public AmmoPickup NetSpawn(Vector3 position, PowerUpKind kind) => Create(position, kind);

    static string Label(PowerUpKind k) => k == PowerUpKind.MaxAmmo ? "NEVERA DE\nMUNICIÓN" : k == PowerUpKind.InstaKill ? "SIESTA\nFULMINANTE"
        : k == PowerUpKind.DoublePoints ? "2x1 EN\nCONCHAS" : "PAELLA\nPARA TODOS";

    static Color Tint(PowerUpKind k) => k == PowerUpKind.MaxAmmo ? new Color(0.2f, 0.6f, 1f) : k == PowerUpKind.InstaKill ? new Color(0.9f, 0.15f, 0.15f)
        : k == PowerUpKind.DoublePoints ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.5f, 0.1f);

    AmmoPickup Create(Vector3 position, PowerUpKind kind)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "PowerUp_" + kind;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.6f, 0.45f, 0.4f);
        go.GetComponent<Renderer>().material.color = Tint(kind);
        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(2.5f, 4f, 3f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var label = WorldLabel.Create(go.transform, Vector3.zero, Label(kind), 0.25f, Color.Lerp(Tint(kind), Color.white, 0.5f));
        label.transform.position = position + Vector3.up * 0.8f;
        var pickup = go.AddComponent<AmmoPickup>();
        pickup.Init(this, data.lifetime, kind);
        active++;
        return pickup;
    }

    public void Released() => active = Mathf.Max(0, active - 1);

    // ---------------------------------------------------------------- effects (offline / host)
    public void Apply(PowerUpKind kind, PlayerShooter taker)
    {
        var gm = GameManager.Instance;
        RunLog.Note("POWERUP " + kind);
        switch (kind)
        {
            case PowerUpKind.MaxAmmo:
                // like Zombies' Max Ammo: whoever picks it up refills the whole team
                if (gm != null && gm.PlayerCount > 0)
                    foreach (var p in gm.Players) { var s = p != null ? p.GetComponent<PlayerShooter>() : null; if (s != null) s.RefillAllAmmo(); }
                else if (taker != null) taker.RefillAllAmmo();
                gm?.ShowMessage("¡NEVERA DE MUNICIÓN! Munición al máximo");
                break;
            case PowerUpKind.InstaKill:
                instaUntil = Time.time + data.instaKillSeconds;
                gm?.ShowMessage("¡SIESTA FULMINANTE! Los abuelos caen de un golpe (" + Mathf.RoundToInt(data.instaKillSeconds) + " s)");
                break;
            case PowerUpKind.DoublePoints:
                doubleUntil = Time.time + data.doublePointsSeconds;
                gm?.ShowMessage("¡2x1 EN CONCHAS! Todo paga el doble (" + Mathf.RoundToInt(data.doublePointsSeconds) + " s)");
                break;
            case PowerUpKind.Nuke:
                nuking = true;
                foreach (var e in FindObjectsByType<EnemyHealth>())
                    if (!e.IsDead && e.GetComponent<BossAI>() == null) e.TakeDamage(e.Current + 1f, false, true);
                nuking = false;
                gm?.AddPoints(data.nukePoints, PointSource.Bonus);
                gm?.ShowMessage("¡PAELLA PARA TODOS! Los abuelos se van a comer (+" + data.nukePoints + ")");
                break;
        }
        if (NetGame.H != null) NetGame.H.BuffsChanged();
    }

    // Online client: the timers the host is running (HUD only).
    public void NetSetBuffs(float instaLeft, float doubleLeft)
    {
        instaUntil = Time.time + instaLeft;
        doubleUntil = Time.time + doubleLeft;
    }

    // HUD line ("" when nothing is active).
    public string Status()
    {
        string s = "";
        if (InstaKill) s = "SIESTA FULMINANTE " + Mathf.CeilToInt(InstaKillLeft) + " s";
        if (DoublePoints) s += (s.Length > 0 ? "  ·  " : "") + "CONCHAS x2 " + Mathf.CeilToInt(DoublePointsLeft) + " s";
        return s;
    }
}

// A power-up lying on the ground (the name stays from when the only one was the ammo cooler).
public class AmmoPickup : MonoBehaviour
{
    PowerUpManager owner;
    float despawnAt;
    Renderer rend;

    public int NetId { get; set; }
    public PowerUpKind Kind { get; private set; }

    public void Init(PowerUpManager manager, float lifetime, PowerUpKind kind)
    {
        owner = manager;
        Kind = kind;
        bornAt = Time.time;
        despawnAt = Time.time + lifetime;
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        float left = despawnAt - Time.time;
        if (left < 5f) rend.enabled = Mathf.Repeat(Time.time * 4f, 1f) > 0.3f;
        if (left <= 0f) Destroy(gameObject);
        // online host: remote players are moved by their snapshots (no trigger events): check the distance
        if (Net.IsHost && GameManager.Instance != null && Time.time - bornAt > 0.6f)   // (a moment so everyone sees it appear)
            foreach (var p in GameManager.Instance.Standing)
            {
                Vector3 d = p.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 2f && new Vector2(d.x, d.z).magnitude < 1.6f) { Take(p.GetComponent<PlayerShooter>()); return; }
            }
    }

    void OnTriggerEnter(Collider other)
    {
        if (Net.Online) return;
        var shooter = other.GetComponentInParent<PlayerShooter>();
        if (shooter != null) Take(shooter);
    }

    void Take(PlayerShooter shooter)
    {
        if (shooter == null || taken) return;
        taken = true;
        if (NetGame.H != null) NetGame.H.PowerUpTaken(this);
        if (owner != null) owner.Apply(Kind, shooter);
        Destroy(gameObject);
    }

    bool taken;
    float bornAt;

    void OnDestroy()
    {
        if (owner != null) owner.Released();
    }
}
