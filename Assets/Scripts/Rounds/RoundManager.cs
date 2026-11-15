using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// Zombies-style rounds. Each round spawns a number of old men (growing with the round) from the
// spawn points of the zones that are open, never more than maxAlive at once. Enemy health grows
// linearly until round 9 and exponentially after. Faster / tougher variants appear in later rounds.
// Every nightEvery rounds it is a night round: only the night variant (abuelo senderista), fewer of them.
// Spawn points with a SpawnEntrance make the old man come out of it (visible ~1 s before he is in play).
// The spawn point is chosen by walking distance to the player and by where the camera looks: sides and
// back are favoured over the front, so no spot is safe for long. Co-op: every standing player counts (a
// point gets the best weight it has for any of them, never right next to anyone) and there are more old
// men per round (CoopData.enemiesPerExtraPlayer).
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [SerializeField] GameObject enemyPrefab;
    [SerializeField] EnemyVariant baseVariant;
    [SerializeField] EnemyVariant[] specialVariants;
    [SerializeField] EnemyVariant nightVariant;
    [Tooltip("'Anselmo cagón' mode numbers (shortcuts through the spawn entrances).")]
    [SerializeField] TunnelData tunnels;

    [Header("Pacing")]
    [SerializeField] float firstRoundDelay = 6f;
    [SerializeField] float timeBetweenRounds = 10f;
    [SerializeField] int maxAlive = 24;
    [SerializeField] float spawnIntervalStart = 2f;
    [SerializeField] float spawnIntervalMin = 0.4f;

    [Header("Night rounds")]
    [SerializeField] int nightEvery = 5;
    [SerializeField] float nightCountFactor = 0.6f;

    [Header("Spawn choice (walking distance to the player, metres)")]
    [SerializeField] float minSpawnDistance = 5f;
    [SerializeField] float nearDistance = 28f;       // full weight up to here
    [SerializeField] float farDistance = 50f;        // reduced weight up to here, then a trickle
    [SerializeField] float frontWeight = 0.45f;      // spawn in front of the camera
    [SerializeField] float sideWeight = 1.2f;
    [SerializeField] float backWeight = 1.6f;
    [SerializeField] float closeFrontWeight = 0.2f;  // in front and closer than 12 m: rarely
    [SerializeField] float recentPenalty = 0.3f;     // the same entrance used in the last 5 s

    public int Round { get; private set; }
    public int RemainingInRound => Net.IsClient ? netRemaining : toSpawn + alive.Count;
    public bool InIntermission => intermission;
    public bool SpawningPaused { get; set; }   // the boss fight stops regular rounds
    public bool IsNight { get; private set; }
    public bool NextRoundNight => Net.IsClient ? netNextNight : IsNightRound(Round + 1);
    public GameObject EnemyPrefab => enemyPrefab;
    // Online: the host waits until everyone has loaded the map before the first countdown.
    public bool WaitingForPlayers => Net.IsHost && (NetGame.Instance == null || !NetGame.Instance.Ready) || Net.IsClient && (NetGame.Instance == null || !NetGame.Instance.Ready);
    int netRemaining;
    bool netNextNight;
    public bool ForceNextNight { get; set; }    // debug menu
    // "Anselmo cagón" mode (menu option, GameSettings.AnselmoCagon): half the old men take shortcuts.
    public bool TunnelMode { get; set; }
    public TunnelData Tunnels => tunnels;

    public event Action<int> RoundStarted;
    public event Action<int> RoundEnded;
    public event Action<EnemyAI, Transform> EnemySpawned;   // (enemy, spawn point) for tests / stats

    readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    int toSpawn;
    float nextSpawnTime;
    float intermissionEnd;
    bool intermission = true;
    NavMeshPath path;

    void Awake()
    {
        Instance = this;
        path = new NavMeshPath();
        TunnelMode = Net.Online ? NetSession.Instance.AnselmoCagon : GameSettings.AnselmoCagon;   // online: the host's mode
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        intermissionEnd = Time.time + firstRoundDelay;
    }

    // ---------------------------------------------------------------- formulas
    public static float BaseHealth(int round)
    {
        if (round < 10) return 150f + 100f * (round - 1);
        return 950f * Mathf.Pow(1.1f, round - 9);
    }

    public float HealthForRound(int round) => BaseHealth(round) * RunModifiers.Get(Stat.EnemyHealth);

    // x(1 + 0.5 per extra player) by default
    public float PlayerCountFactor
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.PlayerCount <= 1 || gm.Coop == null) return 1f;
            return 1f + gm.Coop.enemiesPerExtraPlayer * (gm.PlayerCount - 1);
        }
    }

    public static int EnemyCount(int round) => Mathf.RoundToInt(6f + 2.2f * round + 0.08f * round * round);

    public bool IsNightRound(int round) => nightVariant != null && (ForceNextNight && round == Round + 1 || nightEvery > 0 && round > 0 && round % nightEvery == 0);

    // night rounds: the senderistas are strong, so only a few at once (EnemyVariant.maxAliveAtOnce)
    public int MaxAliveNow => IsNight && nightVariant != null && nightVariant.maxAliveAtOnce > 0 ? Mathf.Min(maxAlive, nightVariant.maxAliveAtOnce) : maxAlive;
    public int AliveCount => alive.Count;

    float SpawnInterval => Mathf.Max(spawnIntervalMin, spawnIntervalStart - 0.1f * (Round - 1)) * (IsNight ? 1.4f : 1f);

    // ---------------------------------------------------------------- loop
    void Update()
    {
        if (!Net.Authority) return;   // online client: the host's rounds (NetTick / NetRoundStarted)
        alive.RemoveAll(e => e == null || e.IsDead);
        var gm = GameManager.Instance;
        if (gm == null || gm.State == GameState.GameOver || gm.State == GameState.Victory) return;

        if (intermission)
        {
            if (WaitingForPlayers) { intermissionEnd = Time.time + firstRoundDelay; return; }
            if (!SpawningPaused && Time.time >= intermissionEnd) StartRound(Round + 1);
            return;
        }

        if (toSpawn > 0 && !SpawningPaused && alive.Count < MaxAliveNow && Time.time >= nextSpawnTime)
        {
            if (SpawnEnemy(IsNight ? nightVariant : PickVariant(), null) != null) toSpawn--;
            nextSpawnTime = Time.time + SpawnInterval;
        }

        if (toSpawn <= 0 && alive.Count == 0)
        {
            intermission = true;
            intermissionEnd = Time.time + timeBetweenRounds;
            RoundEnded?.Invoke(Round);
            IsNight = false;
            if (NetGame.H != null) NetGame.H.RoundEvent(false, Round, false);
        }
    }

    // ---------------------------------------------------------------- online client (the host's rounds)
    // (the event or the tick, whichever arrives first, raises it once)
    public void NetRoundStarted(int round, bool night)
    {
        if (round == Round && !intermission) return;
        Round = round;
        IsNight = night;
        intermission = false;
        RoundStarted?.Invoke(round);
    }

    public void NetRoundEnded(int round)
    {
        if (intermission) return;
        intermission = true;
        RoundEnded?.Invoke(round);
        IsNight = false;
    }

    public void NetTick(int round, int remaining, bool inIntermission, bool paused, bool nextNight, bool night)
    {
        if (!inIntermission) NetRoundStarted(round, night);
        else if (round == Round) NetRoundEnded(round);
        netRemaining = remaining;
        SpawningPaused = paused;
        netNextNight = nextNight;
    }

    void StartRound(int round)
    {
        IsNight = IsNightRound(round);
        ForceNextNight = false;
        Round = round;
        intermission = false;
        float count = EnemyCount(round) * RunModifiers.Get(Stat.EnemyCount) * (IsNight ? nightCountFactor : 1f) * PlayerCountFactor;
        toSpawn = Mathf.Max(1, Mathf.RoundToInt(count));
        nextSpawnTime = Time.time + (IsNight ? 3f : 1f);   // night: a moment for the dusk
        RoundStarted?.Invoke(round);
        if (NetGame.H != null) NetGame.H.RoundEvent(true, round, IsNight);
    }

    // Stop the current round's queue (used when the boss appears).
    public void ClearQueue() => toSpawn = 0;

    // Debug / tests: more old men in the current round's queue.
    public void DebugAddToQueue(int count) { if (!intermission) toSpawn += count; }

    // Debug: kill everything alive and end the round now (no points).
    public void DebugEndRound()
    {
        toSpawn = 0;
        foreach (var e in alive.ToList())
            if (e != null && !e.IsDead && e.GetComponent<BossAI>() == null) e.TakeDamage(e.Current + 1f, false, true);
        if (intermission) intermissionEnd = Time.time;
    }

    EnemyVariant PickVariant()
    {
        if (specialVariants != null)
            foreach (var v in specialVariants)
                if (v != null && UnityEngine.Random.value < v.ChanceAt(Round)) return v;
        return baseVariant;
    }

    // Spawns one enemy at a valid point (near `near` if given, otherwise an open-zone spawn point).
    public EnemyAI SpawnEnemy(EnemyVariant variant, Vector3? near)
    {
        Transform point = null;
        Vector3? pos;
        if (near.HasValue) pos = SampleAround(near.Value, 4f);
        else
        {
            point = PickSpawnPoint();
            var entranceAt = point != null ? point.GetComponent<SpawnEntrance>() : null;
            pos = point == null ? null : entranceAt != null ? entranceAt.ExitPoint : SampleAround(point.position, 1.5f);
        }
        if (!pos.HasValue) return null;
        var player = GameManager.Instance != null ? GameManager.Instance.NearestStanding(pos.Value) : null;
        Quaternion rot = Quaternion.identity;
        if (player != null)
        {
            Vector3 d = player.transform.position - pos.Value;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(d);
        }
        var go = Instantiate(enemyPrefab, pos.Value, rot);
        go.name = enemyPrefab.name + "_R" + Round;
        var ai = go.GetComponent<EnemyAI>();
        var v = variant != null ? variant : baseVariant;
        ai.Configure(v, HealthForRound(Mathf.Max(1, Round)) * (v != null ? v.healthMultiplier : 1f));
        if (TunnelMode && tunnels != null) ai.EnableTunnels(tunnels, UnityEngine.Random.value < tunnels.chance);   // fixed for his whole life
        var entrance = point != null ? point.GetComponent<SpawnEntrance>() : null;
        if (entrance != null) ai.EmergeFrom(entrance);
        alive.Add(go.GetComponent<EnemyHealth>());
        if (NetGame.H != null) NetGame.H.EnemySpawned(go.GetComponent<EnemyHealth>(), 0, v != null ? v.name : "", go.GetComponent<EnemyHealth>().MaxHealth);
        EnemySpawned?.Invoke(ai, point);
        return ai;
    }

    // Weighted choice among the open spawn points: walking distance to the player, angle to the camera
    // (sides / back favoured), busy or recently used entrances avoided.
    Transform PickSpawnPoint()
    {
        var zm = ZoneManager.Instance;
        if (zm == null) return null;
        var points = zm.OpenSpawnPoints().ToList();
        if (points.Count == 0) return null;
        var gm = GameManager.Instance;
        var standing = gm != null ? gm.Standing.ToList() : new List<PlayerHealth>();
        if (standing.Count == 0) return points[UnityEngine.Random.Range(0, points.Count)];
        var player = standing[0];

        // each standing player: position on the NavMesh and where their camera looks
        var views = new List<(Vector3 p, Vector3 onMesh, Vector3 look)>();
        foreach (var sp in standing)
        {
            Vector3 lk = PlayerSlot.LookOf(sp);   // online: remote players send where their camera looks
            NavAgents.Sample(sp.transform.position, 3f, out var onMesh);
            views.Add((sp.transform.position, onMesh, lk));
        }
        Vector3 p = player.transform.position;

        var weights = new List<float>(points.Count);
        float total = 0f;
        foreach (var t in points)
        {
            float w = 0f;
            Vector3 spot = Spot(t);
            if (!views.Exists(v => Vector3.Distance(spot, v.p) < minSpawnDistance))   // never right next to anyone
                foreach (var v in views) w = Mathf.Max(w, SpawnWeight(t, v.p, v.onMesh, v.look));
            weights.Add(w);
            total += w;
        }
        if (total <= 0f)
        {
            // everything busy or too close: any free point not right next to the player
            var free = points.Where(t => { var e = t.GetComponent<SpawnEntrance>(); return (e == null || !e.Busy) && Vector3.Distance(Spot(t), p) >= minSpawnDistance * 0.5f; }).ToList();
            return free.Count > 0 ? free[UnityEngine.Random.Range(0, free.Count)] : null;
        }
        float r = UnityEngine.Random.value * total;
        for (int i = 0; i < points.Count; i++)
        {
            r -= weights[i];
            if (r <= 0f) return points[i];
        }
        return points[points.Count - 1];
    }

    static Vector3 Spot(Transform t)
    {
        var e = t.GetComponent<SpawnEntrance>();
        return e != null ? e.ExitPoint : t.position;
    }

    float SpawnWeight(Transform t, Vector3 player, Vector3 playerOnMesh, Vector3 look)
    {
        var e = t.GetComponent<SpawnEntrance>();
        if (e != null && e.Busy) return 0f;
        Vector3 spot = Spot(t);
        float straight = Vector3.Distance(spot, player);
        if (straight < minSpawnDistance) return 0f;

        // walking distance (closed doors are carved out of the NavMesh, so other sides are unreachable)
        float walk = straight;
        if (straight < farDistance * 1.5f && NavAgents.Sample(spot, 2f, out var s))
        {
            if (NavMesh.CalculatePath(s, playerOnMesh, NavAgents.Filter(0), path) && path.status == NavMeshPathStatus.PathComplete)
            {
                walk = 0f;
                for (int i = 1; i < path.corners.Length; i++) walk += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            else return 0f;
        }
        float w = walk <= nearDistance ? 1f : walk <= farDistance ? 0.35f : 0.06f;

        Vector3 to = spot - player;
        to.y = 0f;
        float angle = Vector3.Angle(look, to);
        if (angle < 55f) w *= straight < 12f ? closeFrontWeight : frontWeight;
        else if (angle < 125f) w *= sideWeight;
        else w *= backWeight;

        if (e != null && Time.time - e.LastUsed < 5f) w *= recentPenalty;
        return w;
    }

    static Vector3? SampleAround(Vector3 center, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 o = UnityEngine.Random.insideUnitCircle * radius;
            if (NavAgents.Sample(center + new Vector3(o.x, 0f, o.y), 2f, out var hit)) return hit;
        }
        return null;
    }

    // Moves an enemy that cannot reach the player to a spawn point closer to them.
    public void Relocate(EnemyAI enemy)
    {
        // (the spawn choice already favours points near the standing players)
        var t = PickSpawnPoint();
        if (t == null) return;
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh && NavAgents.Sample(Spot(t), 2f, out var p, agent.agentTypeID)) agent.Warp(p);
    }
}
