using System.Linq;
using UnityEngine;

// La Serenisima: instead of night rounds, every `every` rounds the tide comes in (acqua alta). The water rises over
// the low floor (level 0): whoever wades in it walks slower, the old men don't, so the high ground (terraces, bridges,
// the cemetery mound) is where you want to be. It goes down when the round ends and leaves a Nevera de munición.
// The state only depends on the round number, so online every machine works it out by itself (no message); only the
// host drops the reward and shows the toast (its ShowMessage is broadcast).
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

    public static AcquaAlta Instance { get; private set; }
    public bool Flooding { get; private set; }
    public float WaterY { get; private set; }
    public bool ForceNext { get; set; }      // debug menu / tests: the next round floods
    public int Every => every;

    float floatBaseY;
    bool forced;
    int seenRound;

    public void Configure(Transform waterPlane, Transform floaters, float low)
    {
        water = waterPlane; floating = floaters; lowY = low;
    }

    void Awake()
    {
        Instance = this;
        WaterY = lowY;
        if (floating != null) floatBaseY = floating.position.y;
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
            if (pc != null) pc.ExternalSpeedFactor = pl.transform.position.y < WaterY - wadeDepth ? wadeFactor : 1f;
        }
    }

    void Reward()
    {
        var gm = GameManager.Instance;
        var pm = PowerUpManager.Instance;
        PlayerHealth who = gm != null ? gm.Standing.FirstOrDefault() : null;
        if (who == null && gm != null) who = gm.Player;
        if (who == null || pm == null) return;
        Vector3 p = who.transform.position + who.transform.forward * 2f;
        if (NavAgents.Sample(p, 3f, out var s)) p = s;
        pm.Spawn(p + Vector3.up * 0.6f);
    }
}
