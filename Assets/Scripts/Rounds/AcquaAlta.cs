using System.Linq;
using UnityEngine;

// La Serenisima: instead of night rounds, every `every` rounds the tide comes in (acqua alta). The water rises over
// the low floor (level 0): whoever wades in it walks slower, the old men don't, so the high ground (terraces, bridges,
// the cemetery mound) is where you want to be. It goes down when the round ends and leaves a Nevera de munición.
// The state only depends on the round number, so online every machine works it out by itself (no message); only the
// host drops the reward and shows the toast (its ShowMessage is broadcast).
// While it floods the canals can be swum (user, 16/11: "the old men always follow your route"): a hidden floor under
// every canal (`swimFloor`, its top at `swimY`, 0.25 m under the quays so you walk out) is switched on, and the old men
// get the NavMesh area "Canal" in their mask; both wade slower in it. Posts along every quay edge show where the
// quay ends under the water. When the tide goes down, players still in a canal are put back on the quay (no damage)
// and old men are moved to the nearest dry NavMesh.
public class AcquaAlta : MonoBehaviour
{
    [SerializeField] int every = 5;
    [SerializeField] Transform water;        // the sea plane
    [SerializeField] Transform floating;     // boats and mooring poles: they rise with the water
    [SerializeField] float lowY = -0.8f;
    [SerializeField] float highY = 0.45f;    // over the level-0 floor: knee deep
    [SerializeField] float riseTime = 7f;
    [SerializeField] float wadeFactor = 0.6f;   // player speed while the feet are under water
    [SerializeField] float wadeDepth = 0.15f;
    [SerializeField] Transform swimFloor;    // colliders under the canals (baked as NavMesh area "Canal")
    [SerializeField] float swimY = -0.25f;
    [SerializeField] float swimFactor = 0.5f;   // players and old men in a canal

    public static AcquaAlta Instance { get; private set; }
    public bool Flooding { get; private set; }
    public float WaterY { get; private set; }
    public bool ForceNext { get; set; }      // debug menu / tests: the next round floods
    public int Every => every;
    public bool SwimActive { get; private set; }
    public float SwimY => swimY;

    float floatBaseY;
    bool forced;
    int seenRound;
    float nextWadeCheck;

    public void Configure(Transform waterPlane, Transform floaters, float low, Transform swim, float swimTop)
    {
        water = waterPlane; floating = floaters; lowY = low; swimFloor = swim; swimY = swimTop;
    }

    void Awake()
    {
        Instance = this;
        WaterY = lowY;
        if (floating != null) floatBaseY = floating.position.y;
        SetSwim(false);
    }

    void SetSwim(bool on)
    {
        SwimActive = on;
        NavAgents.SwimOpen = on;
        if (swimFloor != null)
            foreach (var c in swimFloor.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        foreach (var ai in FindObjectsByType<EnemyAI>())
        {
            if (ai == null) continue;
            if (!on) ai.SetWading(false, 1f);
            if (!on && !ai.IsPuppet) ToDryLand(ai);
            ai.SetCanalAccess(on);
        }
        if (on || GameManager.Instance == null || CanalRescue.Instance == null) return;
        foreach (var pl in GameManager.Instance.Players)
            if (pl != null && !pl.IsDead && !PlayerSlot.IsProxy(pl) && CanalRescue.Instance.InWater(pl.transform.position))
                CanalRescue.Instance.ToShore(pl);
    }

    static void ToDryLand(EnemyAI ai)
    {
        var agent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        var canal = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = 1 << NavAgents.CanalArea };
        if (!UnityEngine.AI.NavMesh.SamplePosition(ai.transform.position, out _, 0.4f, canal)) return;   // not in the water
        var dry = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas & ~(1 << NavAgents.CanalArea) };
        for (float r = 2f; r <= 16f; r *= 2f)
            if (UnityEngine.AI.NavMesh.SamplePosition(ai.transform.position, out var hit, r, dry)) { agent.Warp(hit.position); return; }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool IsFloodRound(int round) => every > 0 && round > 0 && round % every == 0;

    void Update()
    {
        var rm = RoundManager.Instance;
        if (rm == null) return;
        bool inRound = rm.Round > 0 && !rm.InIntermission;
        if (rm.Round != seenRound) { seenRound = rm.Round; forced = ForceNext; ForceNext = false; }   // decided when a round starts
        bool flood = inRound && (IsFloodRound(rm.Round) || forced);
        if (flood != Flooding)
        {
            Flooding = flood;
            var gm = GameManager.Instance;
            if (Net.Authority && gm != null) gm.ShowMessage(flood ? "¡ACQUA ALTA! Sube a lo alto: en el agua vas lento." : "Baja la marea.");
            SetSwim(flood);   // first: the reward must land on dry ground, not on the canal floor that is going away
            if (!flood && Net.Authority) Reward();
        }
        float target = Flooding ? highY : lowY;
        WaterY = Mathf.MoveTowards(WaterY, target, (highY - lowY) / Mathf.Max(0.1f, riseTime) * Time.deltaTime);
        if (water != null) { var p = water.position; p.y = WaterY; water.position = p; }
        if (floating != null) { var p = floating.position; p.y = floatBaseY + (WaterY - lowY); floating.position = p; }

        var players = GameManager.Instance != null ? GameManager.Instance.Players : null;
        if (players == null) return;
        foreach (var pl in players)
        {
            if (pl == null) continue;
            var pc = pl.GetComponent<PlayerController>();
            float y = pl.transform.position.y;
            if (pc != null) pc.ExternalSpeedFactor = SwimActive && y < swimY + 0.15f ? swimFactor : y < WaterY - wadeDepth ? wadeFactor : 1f;
        }
        if (!SwimActive || CanalRescue.Instance == null || Time.time < nextWadeCheck) return;
        nextWadeCheck = Time.time + 0.25f;
        foreach (var ai in FindObjectsByType<EnemyAI>())
            if (ai != null && !ai.IsPuppet) ai.SetWading(CanalRescue.Instance.InWater(ai.transform.position), swimFactor);
    }

    void Reward()
    {
        var gm = GameManager.Instance;
        var pm = PowerUpManager.Instance;
        PlayerHealth who = gm != null ? gm.Standing.FirstOrDefault() : null;
        if (who == null && gm != null) who = gm.Player;
        if (who == null || pm == null) return;
        // dry ground in an open zone, not on a quay edge: 2 m in front of the player if that is fine, else at his feet
        // (in front of a player on a quay is often the canal, and the nearest NavMesh then is the far quay, maybe
        // in a barrio that is still closed)
        var rescue = CanalRescue.Instance;
        bool Good(Vector3 q)
        {
            if (rescue == null) return true;
            var z = rescue.ZoneAt(q);
            if (z == null || !z.IsOpen) return false;
            foreach (var d in new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (rescue.InWater(q + d * 0.6f)) return false;
            return true;
        }
        // rings round the player (2 m steps, 8 directions, his forward first): the first good point
        Vector3 c0 = who.transform.position, p = c0;
        Vector3 fwd = who.transform.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
        bool found = false;
        for (float d = 2f; d <= 24f && !found; d += 2f)
            for (int k = 0; k < 8 && !found; k++)
                if (NavAgents.Sample(c0 + Quaternion.Euler(0f, k * 45f, 0f) * fwd * d, 1f, out var s) && Good(s)) { p = s; found = true; }
        if (!found && NavAgents.Sample(c0, 24f, out var any)) p = any;
        pm.Spawn(p + Vector3.up * 0.6f);
    }
}
