using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Villajubilada: every `data.every` rounds a herd of bulls comes out of the cattle lorry and runs the streets
// (instead of the night round). They trample whatever is on the route: players (damage + a shove) and old men
// (dead, no conchas). You are safe anywhere off the two lanes of the street: a doorway, a side alley, the plaza.
//
// The route is baked by the setup (`route`, one sample per metre): the Calle del Encierro from the lorry up to the
// ring, then the ring round the plaza block. The herd only runs through open zones: with the whole ring open it
// does laps, otherwise it runs out and back along each open arm (it turns round at a closed barricade).
//
// The FIRST encierro (round 5) is the end of act 1: the Porton opens by itself, night falls, you run up the Calle
// del Encierro ahead of the herd (old ladies throw flowerpots from the windows) and the Porton shuts behind the
// last bull for good. Whoever is still in the start zones is dragged through by the herd; the old men left there
// are gone; the start zones close (no more spawns there).
//
// Everything follows from the round number and the open zones, so online every machine runs its own herd (like the
// acqua alta): the host kills old men, each machine hurts its own players. The host only says when one starts
// (NetMsg.Encierro): a forced one (debug menu) would otherwise never reach the clients.
public class Encierro : MonoBehaviour
{
    [SerializeField] EncierroData data;
    [SerializeField] Vector3[] route;         // one sample per metre, on the ground
    [SerializeField] int ringFrom;            // first sample of the ring; the last sample is the same point again
    [SerializeField] int gateIndex;           // sample where the Porton stands
    [SerializeField] Zone[] sampleZone;       // zone of every sample
    [SerializeField] ZoneDoor[] sampleDoor;   // the barricade standing on a sample (gone once it is opened)
    [SerializeField] Zone[] startZones;       // act 1: closed for good after the first encierro
    [SerializeField] Zone streetZone;         // opened by the first encierro (the Calle del Encierro / Calle Mayor)
    [SerializeField] EncierroGate gate;
    [SerializeField] GameObject bullPrefab;
    [SerializeField] GameObject legsPrefab;   // a pair of legs hanging from the hip: two per bull, swung for the gallop
    [SerializeField] Transform lorry;
    [SerializeField] Transform[] windows;     // where the old ladies throw from
    [SerializeField] Transform playerSpawn;   // moved past the Porton when act 1 ends
    [SerializeField] Zone[] lighterZones;     // the two start zones you pay for: the lighter lies in the one opened second

    public static Encierro Instance { get; private set; }
    public EncierroData Data => data;
    public bool ActOne { get; private set; } = true;
    public bool Warning { get; private set; }
    public bool Running { get; private set; }
    public bool FirstRun { get; private set; }      // this encierro is the run out of act 1
    public int Done { get; private set; }
    public bool ForceNext { get; set; }             // debug menu / tests: the next round has an encierro
    public Zone SecondZone { get; private set; }    // the start zone that was opened second (the lighter is there)
    public float LeaderDistance => leader;
    public float PathLength => total;
    public int EnemiesTrampled { get; private set; }
    public int PlayersHit { get; private set; }
    public int PotsThrown { get; private set; }
    public IReadOnlyList<Transform> Bulls => bulls;
    public Vector3[] Route => route;
    public int RingFrom => ringFrom;
    public int GateIndex => gateIndex;
    public Vector3 PastTheGate => route[Mathf.Min(route.Length - 1, gateIndex + 4)];

    readonly List<Transform> bulls = new List<Transform>();
    readonly List<Transform> frontLegs = new List<Transform>(), hindLegs = new List<Transform>();
    readonly List<Vector3> path = new List<Vector3>();
    readonly List<float> cum = new List<float>();
    readonly Dictionary<PlayerHealth, float> immune = new Dictionary<PlayerHealth, float>();
    float[] nextThrow;
    float leader, total, stemLength, gateDistance, startAt, closeAt = -1f;
    int seenRound, opened;
    bool forced;
    static readonly Collider[] hits = new Collider[32];
    const float RowGap = 2.4f, LaneGap = 1.05f;

    public void Configure(EncierroData d, Vector3[] samples, int ring, int gateSample, Zone[] zones, ZoneDoor[] doors, Zone[] act1, Zone street,
        EncierroGate g, GameObject bull, GameObject legs, Transform truck, Transform[] wins, Transform spawn, Zone[] lighter)
    {
        data = d; route = samples; ringFrom = ring; gateIndex = gateSample; sampleZone = zones; sampleDoor = doors; startZones = act1; streetZone = street;
        gate = g; bullPrefab = bull; legsPrefab = legs; lorry = truck; windows = wins; playerSpawn = spawn; lighterZones = lighter;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (ZoneManager.Instance != null) ZoneManager.Instance.ZoneOpened += OnZoneOpened;
        nextThrow = new float[windows != null ? windows.Length : 0];
    }

    void OnZoneOpened(Zone z)
    {
        if (lighterZones == null || !lighterZones.Contains(z)) return;
        if (++opened == 2) SecondZone = z;
    }

    public bool IsEncierroRound(int round) => data != null && data.every > 0 && round > 0 && round % data.every == 0;

    // South of the Porton = the start zones (nothing else of the map is there).
    public bool InActOneArea(Vector3 p) => route != null && route.Length > gateIndex && p.z < route[gateIndex].z - 0.2f;

    void Update()
    {
        var rm = RoundManager.Instance;
        if (rm == null || data == null || route == null || route.Length < 2) return;
        if (rm.Round != seenRound)
        {
            seenRound = rm.Round;
            forced = ForceNext; ForceNext = false;
            // online: the host decides (round, debug force) and tells the clients; from there every machine runs its own herd
            if (!Net.IsClient && rm.Round > 0 && !Warning && !Running && (IsEncierroRound(rm.Round) || forced)) Begin();
        }
        if (Warning && Time.time >= startAt) Release();
        if (Running) Run();
        if (closeAt > 0f && Time.time >= closeAt) CloseActOne();
        if (FirstRun && (Running || Warning)) ThrowFromWindows();
    }

    // ---------------------------------------------------------------- start
    // Online client: the host's encierro started.
    public void NetBegin() { if (!Warning && !Running) Begin(); }

    void Begin()
    {
        if (NetGame.H != null) NetGame.H.EncierroStarted();
        Warning = true;
        FirstRun = ActOne;
        startAt = Time.time + data.warning;
        var gm = GameManager.Instance;
        if (FirstRun)
        {
            if (streetZone != null && ZoneManager.Instance != null) ZoneManager.Instance.Open(streetZone);
            if (gate != null) { gate.SetBlocked(false); gate.SetLeaves(true); }
            var night = FindAnyObjectByType<DayNightCycle>();
            if (night != null) night.FallForGood(data.nightFall);
            if (Net.Authority && gm != null) gm.ShowMessage("¡COHETE! Se abre el portón: CORRED calle arriba, que vienen los toros");
        }
        else
        {
            if (gate != null) gate.SetLeaves(true);
            if (Net.Authority && gm != null) gm.ShowMessage("¡ENCIERRO! Fuera de la calle: vienen los toros");
        }
    }

    // From the lorry to the Porton is the herd's own corridor (the start zones close after act 1, the bulls still come).
    bool Passable(int i) => i <= gateIndex || sampleDoor[i] == null && sampleZone[i] != null && sampleZone[i].IsOpen;

    // The samples the herd runs through, in order.
    public List<int> PlanSamples()
    {
        var seq = new List<int>();
        int n = route.Length, ring = n - 1 - ringFrom;   // steps round the ring
        int stemEnd = 0;
        while (stemEnd < ringFrom && Passable(stemEnd + 1)) stemEnd++;
        for (int i = 0; i <= stemEnd; i++) seq.Add(i);
        if (stemEnd == ringFrom && ring > 0)
        {
            int fwd = 0, back = 0;
            while (fwd < ring && Passable(ringFrom + fwd + 1)) fwd++;
            while (back < ring && Passable(n - 1 - (back + 1))) back++;
            if (fwd >= ring)
                for (int lap = 0; lap < Mathf.Max(1, data.laps); lap++)
                    for (int k = 1; k <= ring; k++) seq.Add(ringFrom + k);
            else
                for (int pass = 0; pass < Mathf.Max(1, data.partialPasses); pass++)
                {
                    for (int k = 1; k <= fwd; k++) seq.Add(ringFrom + k);
                    for (int k = fwd - 1; k >= 0; k--) seq.Add(ringFrom + k);
                    for (int k = 1; k <= back; k++) seq.Add(n - 1 - k);
                    for (int k = back - 1; k >= 1; k--) seq.Add(n - 1 - k);
                    if (back > 0) seq.Add(ringFrom);
                }
        }
        for (int i = stemEnd - 1; i >= 0; i--) seq.Add(i);   // and back into the lorry
        return seq;
    }

    void Release()
    {
        Warning = false;
        Running = true;
        path.Clear(); cum.Clear();
        foreach (int i in PlanSamples())
        {
            var p = route[i];
            if (path.Count > 0 && (path[path.Count - 1] - p).sqrMagnitude < 0.0001f) continue;
            cum.Add(path.Count == 0 ? 0f : cum[cum.Count - 1] + Vector3.Distance(path[path.Count - 1], p));
            path.Add(p);
        }
        total = cum.Count > 0 ? cum[cum.Count - 1] : 0f;
        stemLength = Length(0, ringFrom);
        gateDistance = Length(0, gateIndex);
        leader = 0f;
        while (bulls.Count < data.bulls) bulls.Add(MakeBull(bulls.Count));
        foreach (var b in bulls) b.gameObject.SetActive(false);
    }

    float Length(int from, int to)
    {
        float l = 0f;
        for (int i = from + 1; i <= to && i < route.Length; i++) l += Vector3.Distance(route[i - 1], route[i]);
        return l;
    }

    Transform MakeBull(int i)
    {
        GameObject go = bullPrefab != null ? Instantiate(bullPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Toro" + i;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        go.transform.SetParent(transform, false);
        // the legs hang from two hips (the model's head looks down +Z) and swing against each other
        Transform Legs(float z)
        {
            if (legsPrefab == null) return null;
            var l = Instantiate(legsPrefab, go.transform);
            foreach (var c in l.GetComponentsInChildren<Collider>()) Destroy(c);
            l.transform.localPosition = new Vector3(0f, 0.78f, z);
            return l.transform;
        }
        frontLegs.Add(Legs(0.55f));
        hindLegs.Add(Legs(-0.75f));
        // dust under the hooves
        var dust = new GameObject("Polvo").AddComponent<ParticleSystem>();
        dust.transform.SetParent(go.transform, false);
        dust.transform.localPosition = new Vector3(0f, 0.1f, -0.6f);
        var main = dust.main;
        main.startLifetime = 0.6f; main.startSpeed = 0.5f; main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startColor = new Color(0.42f, 0.37f, 0.28f, 0.16f);   // faint and dark: the sprite shader is unlit and would glow at night main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 40;
        var em = dust.emission; em.rateOverTime = 16f;
        var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
        var pr = dust.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = DustMaterial();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static Material dustMat;
    static Material DustMaterial()
    {
        if (dustMat == null) dustMat = new Material(Shader.Find("Sprites/Default"));
        return dustMat;
    }

    Vector3 At(float d, out Vector3 dir)
    {
        d = Mathf.Clamp(d, 0f, total);
        int i = 1;
        while (i < cum.Count - 1 && cum[i] < d) i++;
        Vector3 a = path[i - 1], b = path[i];
        float seg = Mathf.Max(0.0001f, cum[i] - cum[i - 1]);
        dir = (b - a).normalized;
        return Vector3.Lerp(a, b, (d - cum[i - 1]) / seg);
    }

    // ---------------------------------------------------------------- the run
    void Run()
    {
        if (path.Count < 2) { Finish(); return; }
        float v = FirstRun && leader < stemLength ? data.firstRunSpeed : data.speed;
        leader += v * Time.deltaTime;
        float tail = leader - ((bulls.Count - 1) / 3) * RowGap;
        var gm = GameManager.Instance;
        bool any = false;
        for (int k = 0; k < bulls.Count; k++)
        {
            float d = leader - (k / 3) * RowGap;
            bool on = d > 0f && d < total;
            if (bulls[k].gameObject.activeSelf != on) bulls[k].gameObject.SetActive(on);
            if (!on) continue;
            any = true;
            Vector3 pos = At(d, out var dir);
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            pos += side * ((k % 3) - 1) * LaneGap;
            float gallop = Mathf.Abs(Mathf.Sin((Time.time * 9f) + k * 1.7f));
            bulls[k].SetPositionAndRotation(pos + Vector3.up * gallop * 0.18f,
                Quaternion.Slerp(bulls[k].rotation, Quaternion.LookRotation(dir) * Quaternion.Euler(-6f + gallop * 10f, 0f, 0f), 12f * Time.deltaTime));
            float swing = Mathf.Sin(Time.time * 18f + k * 1.7f) * 42f;
            if (frontLegs[k] != null) frontLegs[k].localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (hindLegs[k] != null) hindLegs[k].localRotation = Quaternion.Euler(-swing, 0f, 0f);
            Trample(pos, dir, side, gm);
        }
        // act 1 ends when the last bull is through the Porton
        if (FirstRun && ActOne && closeAt < 0f && tail > gateDistance) closeAt = Time.time + data.gateCloseDelay;
        if (!any && leader > total) Finish();
    }

    void Trample(Vector3 pos, Vector3 dir, Vector3 side, GameManager gm)
    {
        if (gm != null)
            foreach (var p in gm.Players)
            {
                if (p == null || p.IsDead || p.IsDowned || PlayerSlot.IsProxy(p)) continue;
                Vector3 d = p.transform.position - pos;
                if (Mathf.Abs(d.y) > 2f) continue;
                d.y = 0f;
                if (d.magnitude > data.hitRadius) continue;
                if (immune.TryGetValue(p, out var until) && Time.time < until) continue;
                immune[p] = Time.time + data.playerImmunity;
                PlayersHit++;
                p.TakeDamage(data.playerDamage);
                var pc = p.GetComponent<PlayerController>();
                if (pc != null && pc.enabled) pc.Dash(side * (Vector3.Dot(d, side) >= 0f ? 7f : -7f) + dir * 3f, 0.25f);
            }
        if (!Net.Authority) return;
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up, data.hitRadius, hits, 1 << 9, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var e = hits[i].GetComponentInParent<EnemyHealth>();
            if (e == null || e.IsDead || e.GetComponent<BossAI>() != null) continue;
            e.TakeDamage(e.Current + 1f, false, true);   // trampled: nobody is paid for it
            EnemiesTrampled++;
        }
    }

    void Finish()
    {
        Running = false;
        Done++;
        foreach (var b in bulls) b.gameObject.SetActive(false);
        if (gate != null) gate.SetLeaves(false);
        if (FirstRun && ActOne) CloseActOne();   // a herd that never got past the Porton (should not happen)
        FirstRun = false;
        if (!Net.Authority) return;
        var gm = GameManager.Instance;
        if (gm != null) gm.ShowMessage("Los toros vuelven al camión. Una nevera por las molestias");
        var pm = PowerUpManager.Instance;
        PlayerHealth who = gm != null ? gm.Standing.FirstOrDefault() : null;
        if (who == null && gm != null) who = gm.Player;
        if (who == null || pm == null) return;
        Vector3 at = who.transform.position + who.transform.forward * 2f;
        if (NavAgents.Sample(at, 3f, out var s)) at = s;
        pm.Spawn(at + Vector3.up * 0.6f);
    }

    // ---------------------------------------------------------------- the end of act 1
    void CloseActOne()
    {
        closeAt = -1f;
        if (!ActOne) return;
        ActOne = false;
        if (gate != null) { gate.SetBlocked(true); gate.SetLeaves(Running); }
        var gm = GameManager.Instance;
        Vector3 dest = PastTheGate;
        if (playerSpawn != null) playerSpawn.position = dest;
        if (gm != null)
            foreach (var p in gm.Players)
            {
                if (p == null || PlayerSlot.IsProxy(p) || !InActOneArea(p.transform.position)) continue;
                var cc = p.GetComponent<CharacterController>();
                bool was = cc != null && cc.enabled;
                if (cc != null) cc.enabled = false;
                p.transform.position = dest + Vector3.up * 0.2f;
                if (cc != null) cc.enabled = was;
                if (!p.IsDead && !p.IsDowned) p.TakeDamage(data.playerDamage);
                PlayersHit++;
                gm.ShowMessageTo(p, "Los toros te han arrastrado calle arriba");
            }
        if (startZones != null) foreach (var z in startZones) if (z != null) z.Close();
        if (!Net.Authority) return;
        foreach (var e in FindObjectsByType<EnemyHealth>())
            if (e != null && !e.IsDead && e.GetComponent<BossAI>() == null && InActOneArea(e.transform.position)) e.TakeDamage(e.Current + 1f, false, true);
        var bm = BoatMission.Instance;
        if (gm != null) gm.ShowMessage(bm != null && bm.Pueblo && !bm.Shovel
            ? "El portón se cierra: ya no hay vuelta atrás. Os habéis dejado el mechero: lo venden en el ferial"
            : "El portón se cierra: ya no hay vuelta atrás. Dad la luz, que no se ve nada");
    }

    // ---------------------------------------------------------------- old ladies at the windows
    void ThrowFromWindows()
    {
        var gm = GameManager.Instance;
        if (gm == null || windows == null) return;
        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] == null || Time.time < nextThrow[i]) continue;
            PlayerHealth best = null;
            float bestD = data.throwRange;
            foreach (var p in gm.Players)
            {
                if (p == null || p.IsDead || p.IsDowned || PlayerSlot.IsProxy(p)) continue;   // each machine throws at its own players
                Vector3 d = p.transform.position - windows[i].position; d.y = 0f;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = p; }
            }
            if (best == null) { nextThrow[i] = Time.time + 0.4f; continue; }
            nextThrow[i] = Time.time + data.throwInterval * (0.8f + Random.value * 0.6f);
            var cc = best.GetComponent<CharacterController>();
            Vector3 lead = cc != null ? cc.velocity * 0.5f : Vector3.zero; lead.y = 0f;
            MacetaProjectile.Throw(windows[i].position, best.transform.position + lead, data.throwSpeed, data.throwDamage, data.throwRadius);
            PotsThrown++;
        }
    }

    // ---------------------------------------------------------------- tests / debug
    public void DebugStartNow()
    {
        if (Warning || Running) return;
        Begin();
        startAt = Time.time;
    }
}
