using UnityEngine;

// Enemies have a small chance to drop a "Nevera de munición" (max ammo). Since weapons only come
// from the mystery box, this is the main way to refill reserves.
public class PowerUpManager : MonoBehaviour
{
    [SerializeField] float ammoDropChance = 0.025f;
    [SerializeField] float lifetime = 25f;
    [SerializeField] int maxActive = 2;

    int active;

    void OnEnable() => EnemyHealth.AnyEnemyDied += OnEnemyDied;
    void OnDisable() => EnemyHealth.AnyEnemyDied -= OnEnemyDied;

    void OnEnemyDied(EnemyHealth enemy, DamageInfo info)
    {
        if (!Net.Authority) return;   // online: the host drops them
        if (active >= maxActive || enemy.GetComponent<BossAI>() != null) return;
        if (Random.value > ammoDropChance * RunModifiers.Get(Stat.AmmoDropChance)) return;
        Spawn(enemy.transform.position + Vector3.up * 0.6f);
    }

    public void Spawn(Vector3 position)
    {
        var pickup = Create(position);
        if (NetGame.H != null) NetGame.H.PowerUpSpawned(pickup);
    }

    // Online client: the host dropped one here (the host also decides who takes it).
    public AmmoPickup NetSpawn(Vector3 position) => Create(position);

    AmmoPickup Create(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "PowerUp_MaxAmmo";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.6f, 0.45f, 0.4f);
        go.GetComponent<Renderer>().material.color = new Color(0.2f, 0.6f, 1f);
        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(2.5f, 4f, 3f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        WorldLabel.Create(go.transform, new Vector3(0f, 1.8f, 0f), "NEVERA DE\nMUNICIÓN", 0.25f, new Color(0.6f, 0.9f, 1f));
        var pickup = go.AddComponent<AmmoPickup>();
        pickup.Init(this, lifetime);
        active++;
        return pickup;
    }

    public void Released() => active = Mathf.Max(0, active - 1);
}

public class AmmoPickup : MonoBehaviour
{
    PowerUpManager owner;
    float despawnAt;
    Renderer rend;

    public int NetId { get; set; }

    public void Init(PowerUpManager manager, float lifetime)
    {
        owner = manager;
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
        // like Zombies' Max Ammo: whoever picks it up refills the whole team
        var gm = GameManager.Instance;
        if (gm != null && gm.PlayerCount > 0)
            foreach (var p in gm.Players) { var s = p != null ? p.GetComponent<PlayerShooter>() : null; if (s != null) s.RefillAllAmmo(); }
        else shooter.RefillAllAmmo();
        GameManager.Instance?.ShowMessage("¡NEVERA DE MUNICIÓN! Munición al máximo");
        Destroy(gameObject);
    }

    bool taken;
    float bornAt;

    void OnDestroy()
    {
        if (owner != null) owner.Released();
    }
}
