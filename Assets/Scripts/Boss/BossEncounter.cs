using System;
using UnityEngine;

// Starts the final fight when the dock zone is opened: stops regular rounds, spawns the boss,
// and when he dies enables the extraction (the boat).
public class BossEncounter : MonoBehaviour
{
    public static BossEncounter Instance { get; private set; }

    [SerializeField] Zone triggerZone;
    [SerializeField] BossAI bossPrefab;
    [SerializeField] Transform spawnPoint;
    // health: EconomyData.bossHealthInEnemies x a normal old man of the current round, at least bossMinHealth

    public BossAI Boss { get; private set; }
    public BossAI BossPrefab => bossPrefab;
    public event Action<BossAI> BossSpawned;
    public event Action BossDefeated;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (ZoneManager.Instance != null) ZoneManager.Instance.ZoneOpened += OnZoneOpened;
    }

    void OnZoneOpened(Zone zone)
    {
        if (zone != triggerZone || Boss != null || !Net.Authority) return;   // online: the host spawns him
        var rm = RoundManager.Instance;
        rm.ClearQueue();
        rm.SpawningPaused = true;
        Boss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation);
        Boss.name = "Boss_DonAnselmo";
        var gm = GameManager.Instance;
        var eco = gm != null ? gm.Economy : null;
        float hp = Mathf.Max(eco != null ? eco.bossMinHealth : 30000f,
                             (eco != null ? eco.bossHealthInEnemies : 45f) * rm.HealthForRound(Mathf.Max(1, rm.Round)));
        if (gm != null && gm.PlayerCount > 1 && gm.Coop != null) hp *= 1f + gm.Coop.bossHealthPerExtraPlayer * (gm.PlayerCount - 1);   // co-op scaling
        Boss.Configure(hp);
        Boss.Defeated += OnBossDefeated;
        if (NetGame.H != null) NetGame.H.EnemySpawned(Boss.Health, 1, "", hp);
        GameManager.Instance.ShowMessage("¡DON ANSELMO, PRESIDENTE DE LA COMUNIDAD, bloquea el muelle!");
        BossSpawned?.Invoke(Boss);
    }

    // ---------------------------------------------------------------- online client
    public void NetBossSpawned(BossAI boss)
    {
        Boss = boss;
        BossSpawned?.Invoke(boss);
    }

    public void NetBossDefeated()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.BossDefeated = true;
        BossDefeated?.Invoke();
    }

    void OnBossDefeated()
    {
        var gm = GameManager.Instance;
        gm.BossDefeated = true;
        gm.AddPoints(gm.Economy != null ? gm.Economy.bossKillPoints : 2000, PointSource.Kill);
        foreach (var e in FindObjectsByType<EnemyHealth>())
            if (!e.IsDead && e.GetComponent<BossAI>() == null) e.TakeDamage(e.Current + 1f, false, true);
        gm.ShowMessage("¡Don Anselmo ha caído! Corre al final del muelle y sube al barco");
        if (NetGame.H != null) NetGame.H.BossDefeated();
        BossDefeated?.Invoke();
    }
}
