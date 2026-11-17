using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// Old man enemy: chases the player over the NavMesh, and when in range plays the cane attack.
// Damage is applied at the impact frame of the animation (if the player is still in range),
// then the enemy waits attackCooldown before it can swing again.
// Animator parameters: Speed (float, m/s at normal scale: idle / walk / run blend), Attack (trigger), Die (trigger).
// Getting shot makes the upper body flinch back for a moment.
// New old men usually come out of a SpawnEntrance (EmergeFrom): hidden inside, walk out, climb the
// barrier, then join the NavMesh. Night variants (EnemyVariant.jumper) use the "Saltador" NavMesh and
// hop on top of the props they cross; throwsChanclas makes them throw flip-flops from mid range.
// Day old men climb too (UpdateClimb): when the player they chase stands on a prop out of their reach, the ones
// close by switch to the "Saltador" NavMesh and hop onto the props like the night ones ("donde subes tú, sube el abuelo").
// "Anselmo cagón" mode (EnableTunnels): a tunneler walks into a spawn entrance when going in, through the
// "pipes" and out of another entrance near the player is shorter than walking; the others chase as usual.
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class EnemyAI : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] int damage = 25;
    [SerializeField] float attackRange = 1.3f;       // start swinging when this close (flat distance)
    [SerializeField] float hitRange = 1.7f;          // player must still be this close at the impact frame
    [SerializeField] float hitDelay = 0.45f;         // seconds from swing start to impact (Attack clip frame 14 @ 30 fps)
    [SerializeField] float attackDuration = 0.9f;    // length of the Attack clip
    [SerializeField] float attackCooldown = 1.5f;    // minimum time between two swings

    [Header("Chase")]
    [SerializeField] float repathInterval = 0.2f;
    [SerializeField] float turnSpeed = 540f;
    [SerializeField] float walkClipSpeed = 1.7f;     // agent speed at which the walk clip plays at normal speed
    [SerializeField] float stuckRelocateTime = 6f;   // no valid path for this long -> respawn closer

    [Header("Hit flinch")]
    [SerializeField] float flinchAngle = 14f;
    [SerializeField] float flinchTime = 0.18f;

    [Header("Death")]
    [SerializeField] float destroyDelay = 4f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int WalkSpeedHash = Animator.StringToHash("WalkSpeed");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int DieHash = Animator.StringToHash("Die");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    NavMeshAgent agent;
    EnemyHealth health;
    Animator animator;
    float nextRepathTime;
    float nextAttackTime;
    float stunnedUntil;
    float noPathSince = -1f;
    bool attacking;
    Transform spine;
    float flinchUntil;
    bool emerging;
    SpawnEntrance entrance;
    float nextThrowTime;
    Transform rightHand, head;
    float hopVelocity;
    int sightMask;

    // Anselmo cagón
    TunnelData tunnels;
    SpawnEntrance tunnelIn;
    float nextPlanTime;
    NavMeshPath planPath;
    Renderer[] renderers;
    Collider[] colliders;
    public bool Tunneler { get; private set; }
    public bool InTunnel { get; private set; }
    public int TunnelsTaken { get; private set; }
    public SpawnEntrance TunnelTarget => tunnelIn;
    public static event System.Action<EnemyAI, SpawnEntrance, SpawnEntrance> Tunnelled;   // (enemy, in, out) for tests / stats

    public bool IsAttacking => attacking;
    public bool IsEmerging => emerging;
    public EnemyVariant Variant { get; private set; }

    // Online client: this old man is the host's puppet (NetEnemy moves him; no AI, no NavMesh).
    public bool IsPuppet { get; private set; }
    public void MakePuppet()
    {
        IsPuppet = true;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
    }

    // Co-op: the nearest standing player he can walk to (re-chosen every targetInterval); downed, dead and
    // hidden players are ignored.
    [SerializeField] float targetInterval = 0.75f;
    PlayerHealth target;
    float nextTargetTime;
    NavMeshPath targetPath;

    PlayerHealth Target
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm == null) return null;
            if (gm.PlayerCount <= 1) return gm.Player;
            if (Time.time >= nextTargetTime || target == null || !target.CanBeTargeted)
            {
                nextTargetTime = Time.time + targetInterval;
                target = PickTarget(gm);
            }
            return target;
        }
    }

    PlayerHealth PickTarget(GameManager gm)
    {
        PlayerHealth first = null;
        foreach (var p in gm.Targetable.OrderBy(p => (p.transform.position - transform.position).sqrMagnitude))
        {
            if (first == null) first = p;
            if (!agent.enabled || !agent.isOnNavMesh) return p;
            if (targetPath == null) targetPath = new NavMeshPath();
            if (NavAgents.Sample(p.transform.position, 2f, out var at, agent.agentTypeID) &&
                NavMesh.CalculatePath(transform.position, at, AgentFilter, targetPath) && targetPath.status == NavMeshPathStatus.PathComplete) return p;
        }
        return first != null ? first : gm.NearestStanding(transform.position);
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        groundAgentType = agent.agentTypeID;
        agent.autoTraverseOffMeshLink = false;   // ledge drops / slides: jumped in an arc by TraverseLink
        agent.areaMask = NavAgents.EnemyMask;    // no canal beds unless the acqua alta is on
        health = GetComponent<EnemyHealth>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        health.Died += OnDied;
        health.Damaged += (_, __) => { flinchUntil = Time.time + flinchTime; CheckHurt(); };
        hurtSeed = Random.Range(1, 1 << 30);
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            if (t.name == "Spine" && spine == null) spine = t;
            else if (t.name == "RightHand") rightHand = t;
            else if (t.name == "Head") head = t;
        }
        sightMask = (1 << 0) | (1 << 11) | (1 << NavAgents.PropLayer);
    }

    void Start()
    {
        nextAttackTime = Time.time + 0.5f;
    }

    // Carnival mask (EnemyVariant.maskHits): hangs on the face until EnemyHealth.MaskBroken, then flies off.
    GameObject mask;

    void WearMask(GameObject prefab)
    {
        if (mask != null) Destroy(mask);
        if (prefab == null || head == null) return;
        float k = transform.lossyScale.y;   // the old man's head is big: the mask model is 0.34 m wide
        mask = Instantiate(prefab, head.position + transform.forward * 0.3f * k + Vector3.up * 0.26f * k, transform.rotation, head);
        mask.transform.localScale *= 2.1f;
        foreach (var c in mask.GetComponentsInChildren<Collider>()) Destroy(c);
        MaskLook.Dress(mask, health);
        health.MaskBroken -= DropMask;
        health.MaskBroken += DropMask;
    }

    void DropMask(EnemyHealth _)
    {
        if (mask == null) return;
        var m = mask;
        mask = null;
        MaskLook.Shatter(m, transform.forward * 2.5f + transform.right * Random.Range(-1.5f, 1.5f));
    }

    // La Serenisima acqua alta: old men can follow you into the flooded canals (area "Canal" only in their mask
    // while it floods) and wade slower there. AcquaAlta drives both.
    float baseSpeed = -1f;
    bool wading;

    public void SetCanalAccess(bool open)
    {
        if (agent == null) return;
        agent.areaMask = open ? NavMesh.AllAreas : NavMesh.AllAreas & ~(1 << NavAgents.CanalArea);
    }

    public void SetWading(bool on, float factor)
    {
        if (agent == null || on == wading) return;
        wading = on;
        if (baseSpeed < 0f) baseSpeed = agent.speed / auraFactor;
        agent.speed = (on ? baseSpeed * factor : baseSpeed) * auraFactor;
    }

    // ---- Villajubilada: el del bombo. While a drummer lives, the old men around him walk faster.
    static readonly System.Collections.Generic.List<EnemyAI> drummers = new System.Collections.Generic.List<EnemyAI>();
    float auraFactor = 1f, nextAuraCheck;
    GameObject worn;
    public float AuraFactor => auraFactor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDrummers() => drummers.Clear();

    // ---------------------------------------------------------------- hurt (user feedback 1, idea 11)
    // Low on health (EnemyVariant.hurtShare) he loses his glasses (walks slower) or drops his cane (runs faster), 50/50.
    // Online every machine picks the same from the NetId and drops the same prop (health reaches the clients through
    // EnemyHealth.Damaged); only the host's old man changes speed.
    bool hurt;
    int hurtSeed;
    float hurtFactor = 1f;
    public int HurtState { get; private set; }   // 0 fine, 1 lost his glasses, 2 dropped his cane (tests)

    void CheckHurt()
    {
        if (hurt || health == null || health.IsDead) return;
        float share = Variant != null ? Variant.hurtShare : 0.25f;
        if (share <= 0f || health.Current > health.MaxHealth * share) return;
        hurt = true;
        uint seed = (uint)(health.NetId != 0 ? health.NetId : hurtSeed) * 2654435761u;
        bool cane = ((seed >> 16) & 1u) == 1u;
        HurtState = cane ? 2 : 1;
        hurtFactor = cane ? (Variant != null ? Variant.caneSpeedFactor : 1.4f) : (Variant != null ? Variant.glassesSpeedFactor : 0.6f);
        nextAuraCheck = 0f;   // UpdateAura applies the new factor now
        if (cane) DropCane(); else DropGlasses();
    }

    void DropCane()
    {
        Renderer cane = null;
        foreach (var r in GetComponentsInChildren<Renderer>()) if (r.name.Contains("Cane")) { cane = r; break; }
        if (cane == null) return;
        Mesh mesh = null;
        if (cane is SkinnedMeshRenderer sk) { mesh = new Mesh(); sk.BakeMesh(mesh, true); }
        else { var mf = cane.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
        cane.enabled = false;
        if (mesh == null) return;
        var go = new GameObject("BastonCaido");
        go.transform.SetPositionAndRotation(cane.transform.position, cane.transform.rotation);
        go.transform.localScale = cane is SkinnedMeshRenderer ? Vector3.one : cane.transform.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = cane.sharedMaterials;
        Fall(go, mesh.bounds.size);
    }

    void DropGlasses()
    {
        Transform at = head != null ? head : transform;
        Material mat = null;
        foreach (var r in GetComponentsInChildren<Renderer>()) if (r.enabled && r.sharedMaterial != null) { mat = r.sharedMaterial; break; }
        float k = transform.lossyScale.y;
        var go = new GameObject("GafasCaidas");
        go.transform.SetPositionAndRotation(at.position + transform.forward * 0.28f * k + Vector3.up * 0.12f * k, transform.rotation);
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, new Color(0.08f, 0.08f, 0.1f));
        block.SetColor(ColorId, new Color(0.08f, 0.08f, 0.1f));
        void Part(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var p = GameObject.CreatePrimitive(type);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.SetPropertyBlock(block);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var lens = Quaternion.Euler(90f, 0f, 0f);
        Part(PrimitiveType.Cylinder, new Vector3(-0.055f, 0f, 0f), new Vector3(0.075f, 0.006f, 0.075f), lens);
        Part(PrimitiveType.Cylinder, new Vector3(0.055f, 0f, 0f), new Vector3(0.075f, 0.006f, 0.075f), lens);
        Part(PrimitiveType.Cube, Vector3.zero, new Vector3(0.04f, 0.008f, 0.008f), Quaternion.identity);
        foreach (float s in new[] { -1f, 1f }) Part(PrimitiveType.Cube, new Vector3(s * 0.095f, 0f, -0.06f), new Vector3(0.006f, 0.008f, 0.12f), Quaternion.identity);
        go.transform.localScale = Vector3.one * Mathf.Max(1f, k * 1.6f);   // the old man's head is big
        Fall(go, new Vector3(0.2f, 0.03f, 0.14f));
    }

    // A dropped prop: falls with physics, bounces a bit and goes after a few seconds (visual only).
    void Fall(GameObject go, Vector3 size)
    {
        go.layer = 2;   // Ignore Raycast: bullets, the camera and the interaction rays go through it
        var box = go.AddComponent<BoxCollider>();
        box.size = Vector3.Max(size, Vector3.one * 0.03f);
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.linearVelocity = transform.forward * 1.2f + Vector3.up * 1.5f + Random.insideUnitSphere * 0.4f;
        rb.angularVelocity = Random.insideUnitSphere * 6f;
        Destroy(go, 6f);
    }

    // Feedback 1 (idea 3): once the Julio Iglesias records go into the karaoke, every old man walks faster for the rest
    // of the run (EconomyData.julioSpeedFactor). auraFactor holds every speed factor of this kind together.
    static float JulioFactor => EasterEgg.Motivated && GameManager.Instance != null && GameManager.Instance.Economy != null
        ? GameManager.Instance.Economy.julioSpeedFactor : 1f;

    void UpdateAura()
    {
        if (Time.time < nextAuraCheck || agent == null) return;
        nextAuraCheck = Time.time + 0.5f;
        float f = 1f;
        for (int i = drummers.Count - 1; i >= 0; i--)
        {
            var d = drummers[i];
            if (d == null || d.health == null || d.health.IsDead) { drummers.RemoveAt(i); continue; }
            if (d == this || d.Variant == null) continue;
            if ((d.transform.position - transform.position).sqrMagnitude <= d.Variant.auraRadius * d.Variant.auraRadius) f = Mathf.Max(f, d.Variant.auraSpeedFactor);
        }
        f *= JulioFactor * hurtFactor;
        if (Mathf.Approximately(f, auraFactor)) return;
        agent.speed = agent.speed / auraFactor * f;
        auraFactor = f;
        if (animator != null) animator.SetFloat(WalkSpeedHash, agent.speed / walkClipSpeed);
    }

    void WearProp(GameObject prefab)
    {
        if (worn != null) Destroy(worn);
        if (prefab == null) return;
        Transform bone = spine != null ? spine : transform;
        float k = transform.lossyScale.y;
        worn = Instantiate(prefab, bone.position + transform.forward * 0.45f * k + Vector3.down * 0.05f * k, transform.rotation, bone);
        foreach (var c in worn.GetComponentsInChildren<Collider>()) Destroy(c);
    }


    // Called by the RoundManager right after spawning.
    public void Configure(EnemyVariant variant, float maxHealth)
    {
        Variant = variant;
        health.Init(maxHealth);
        if (variant == null) return;
        if (variant.maskHits > 0) { health.SetMask(variant.maskHits, variant.maskShare); WearMask(variant.maskPrefab); }
        if (variant.wornPrefab != null) WearProp(variant.wornPrefab);
        if (variant.auraRadius > 0f && !drummers.Contains(this)) drummers.Add(this);
        health.PointsMultiplier = variant.pointsMultiplier;
        damage = variant.damage;
        agent.speed = variant.moveSpeed * RunModifiers.Get(Stat.EnemySpeed);
        baseSpeed = agent.speed;
        transform.localScale = Vector3.one * variant.scale;
        if (animator != null) animator.SetFloat(WalkSpeedHash, agent.speed / walkClipSpeed);
        if (variant.clothingColor.a > 0f) TintClothes(variant.clothingColor);
        nextThrowTime = Time.time + 1.5f + Random.value * 2f;
        // night old men use the "Saltador" NavMesh (props ignored) when this map has one baked
        if (variant.jumper && NavAgents.JumperId != agent.agentTypeID && NavAgents.Sample(transform.position, 3f, out var p, NavAgents.JumperId))
        {
            bool was = agent.enabled;
            agent.enabled = false;
            agent.agentTypeID = NavAgents.JumperId;
            if (was) { agent.enabled = true; agent.Warp(p); }
        }
    }

    // ---------------------------------------------------------------- coming out of an entrance
    public void EmergeFrom(SpawnEntrance e)
    {
        if (e == null) return;
        entrance = e;
        e.Use();
        StartCoroutine(Emerge(e));
    }

    IEnumerator Emerge(SpawnEntrance e)
    {
        emerging = true;
        agent.enabled = false;
        Vector3 a = e.InsidePoint, b = e.BarrierPoint, c = e.ExitPoint;
        Vector3 flat = c - a; flat.y = 0f;
        Quaternion face = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : transform.rotation;
        transform.SetPositionAndRotation(a, face);
        if (animator != null) animator.SetFloat(SpeedHash, 0f);
        yield return new WaitForSeconds(e.ShakeTime);

        // walk out to the barrier
        float t = 0f;
        if (animator != null) animator.SetFloat(SpeedHash, 1.7f);
        while (t < e.WalkTime)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(a, b, t / e.WalkTime);
            yield return null;
        }
        // climb over it (arc + lean forward), land on the NavMesh
        t = 0f;
        float h = e.BarrierHeight + 0.15f;
        while (t < e.ClimbTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / e.ClimbTime);
            transform.position = Vector3.Lerp(b, c, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * h;
            transform.rotation = face * Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 28f, 0f, 0f);
            yield return null;
        }
        transform.rotation = face;
        agent.enabled = true;
        if (NavAgents.Sample(c, 2f, out var land, agent.agentTypeID)) agent.Warp(land);
        emerging = false;
        entrance = null;
        e.Release();
        nextRepathTime = 0f;
    }

    void TintClothes(Color color)
    {
        var block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            string n = r.name;
            if (!(n.Contains("Shirt") || n.Contains("Vest") || n.Contains("Shorts"))) continue;
            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                r.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block, i);
            }
        }
    }

    public void Stun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        if (agent.isOnNavMesh) agent.isStopped = true;
    }

    public void Knockback(Vector3 from, float distance)
    {
        if (!agent.isOnNavMesh) return;
        Vector3 dir = transform.position - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
        Vector3 target = transform.position + dir.normalized * distance;
        if (NavAgents.Sample(target, 1.5f, out var hit, agent.agentTypeID)) agent.Warp(hit);
    }

    void Update()
    {
        if (health.IsDead || emerging || IsPuppet) return;
        UpdateAura();
        if (onLink) return;
        if (agent.enabled && agent.isOnOffMeshLink) { StartCoroutine(TraverseLink()); return; }
        if (Variant != null && Variant.jumper) UpdateHop();
        else UpdateClimb();
        if (animator != null) animator.SetFloat(SpeedHash, agent.velocity.magnitude / Mathf.Max(0.1f, transform.localScale.x), 0.1f, Time.deltaTime);

        var player = Target;
        if (player == null || !player.CanBeTargeted || Time.time < stunnedUntil)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }
        if (attacking) { FacePlayer(player); return; }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        float reach = attackRange * Mathf.Max(1f, transform.localScale.x);

        if (Variant != null && Variant.throwsChanclas && Time.time >= nextThrowTime && distance >= Variant.throwMinRange && distance <= Variant.throwMaxRange && CanSee(player))
        {
            StartCoroutine(ThrowChancla(player));
            return;
        }

        if (distance <= reach && Mathf.Abs(player.transform.position.y - transform.position.y) < 1.5f + agent.baseOffset)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            FacePlayer(player);
            if (Time.time >= nextAttackTime) StartCoroutine(Attack(player));
        }
        else if (agent.isOnNavMesh)
        {
            if (Tunneler && !climbing && UpdateTunnel(player)) return;
            agent.isStopped = false;
            if (Time.time >= nextRepathTime)
            {
                agent.SetDestination(player.transform.position);
                nextRepathTime = Time.time + repathInterval;
            }
            CheckStuck();
        }
    }

    // ---------------------------------------------------------------- Anselmo cagón (shortcuts through entrances)
    // Fixed for the old man's whole life: `through` = he may take shortcuts (50 % by default), else classic chase.
    public void EnableTunnels(TunnelData data, bool through)
    {
        tunnels = data;
        Tunneler = data != null && through;
        nextPlanTime = Time.time + Random.value * (data != null ? data.replanInterval : 1f);
    }

    // Returns true while the shortcut drives the agent (walking to the entrance or queuing at it).
    bool UpdateTunnel(PlayerHealth player)
    {
        if (Time.time >= nextPlanTime)
        {
            nextPlanTime = Time.time + tunnels.replanInterval;
            tunnelIn = PlanTunnel(player.transform.position);
        }
        if (tunnelIn == null) return false;
        if (!tunnelIn.InOpenZone) { tunnelIn = null; return false; }
        Vector3 to = tunnelIn.ExitPoint - transform.position;
        to.y = 0f;
        if (to.magnitude > 1.4f)   // close enough: others queuing around the door push each other a bit
        {
            agent.isStopped = false;
            if (Time.time >= nextRepathTime)
            {
                agent.SetDestination(tunnelIn.ExitPoint);
                nextRepathTime = Time.time + repathInterval;
            }
            return true;
        }
        agent.isStopped = true;   // at the door: wait while someone else is using it
        if (!tunnelIn.Busy) StartCoroutine(GoThrough(tunnelIn));
        return true;
    }

    NavMeshQueryFilter AgentFilter => new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

    float PathLength(Vector3 from, Vector3 to)
    {
        if (planPath == null) planPath = new NavMeshPath();
        if (!NavAgents.Sample(from, 2f, out var a, agent.agentTypeID) || !NavAgents.Sample(to, 2.5f, out var b, agent.agentTypeID)) return float.PositiveInfinity;
        if (!NavMesh.CalculatePath(a, b, AgentFilter, planPath) || planPath.status != NavMeshPathStatus.PathComplete) return float.PositiveInfinity;
        float len = 0f;
        var c = planPath.corners;
        for (int i = 1; i < c.Length; i++) len += Vector3.Distance(c[i - 1], c[i]);
        return len;
    }

    // Best entrance to go in, or null if walking straight to the player is about as fast.
    SpawnEntrance PlanTunnel(Vector3 player)
    {
        float direct = PathLength(transform.position, player);
        float speed = Mathf.Max(0.5f, agent.speed);
        float overhead = (tunnels.travelTime + tunnels.enterClimbTime + tunnels.enterWalkTime + 1.5f) * speed;   // + coming out (~1.5 s)
        if (direct < overhead + tunnels.minSaving) return null;
        var exits = ExitCandidates(player, tunnels.candidates * 2);
        if (exits.Count == 0) return null;
        var ins = SpawnEntrance.All.Where(e => e != null && e.InOpenZone && (!e.Busy || e == tunnelIn))
            .OrderBy(e => (e.ExitPoint - transform.position).sqrMagnitude).Take(tunnels.candidates).ToList();
        SpawnEntrance best = null;
        float bestCost = direct - tunnels.minSaving;
        foreach (var e in ins)
        {
            float walkOut = float.PositiveInfinity;
            foreach (var x in exits) if (x.e != e && x.e.Zone != e.Zone) { walkOut = x.len; break; }   // sorted by length
            if (float.IsInfinity(walkOut)) continue;
            float cost = PathLength(transform.position, e.ExitPoint) + overhead + walkOut;
            if (cost < bestCost) { bestCost = cost; best = e; }
        }
        return best;
    }

    // Entrances of open zones nearest to the player (not in his face), with their walking length to him
    // (+6 m if busy right now), shortest first.
    List<(SpawnEntrance e, float len)> ExitCandidates(Vector3 player, int count)
    {
        var list = new List<(SpawnEntrance e, float len)>();
        foreach (var e in SpawnEntrance.All.Where(e => e != null && e.InOpenZone).OrderBy(e => (e.ExitPoint - player).sqrMagnitude).Take(count))
        {
            Vector3 flat = e.ExitPoint - player;
            flat.y = 0f;
            if (flat.magnitude < 4f) continue;   // not right in the player's face
            float len = PathLength(e.ExitPoint, player);
            if (!float.IsInfinity(len)) list.Add((e, len + (e.Busy ? 6f : 0f)));
        }
        list.Sort((a, b) => a.len.CompareTo(b.len));
        return list;
    }

    IEnumerator GoThrough(SpawnEntrance into)
    {
        emerging = true;
        entrance = into;
        tunnelIn = null;
        float climb = tunnels.enterClimbTime, walk = tunnels.enterWalkTime;
        into.Enter(climb + walk);
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        Vector3 a = transform.position, b = into.BarrierPoint, c = into.InsidePoint;
        Vector3 flat = c - a; flat.y = 0f;
        Quaternion face = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : transform.rotation;
        if (animator != null) animator.SetFloat(SpeedHash, 1.7f);
        // climb back over the barrier, walk inside
        float t = 0f, h = into.BarrierHeight + 0.15f;
        while (t < climb)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / climb);
            transform.position = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * h;
            transform.rotation = face * Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 28f, 0f, 0f);
            yield return null;
        }
        transform.rotation = face;
        t = 0f;
        while (t < walk)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(b, c, Mathf.Clamp01(t / walk));
            yield return null;
        }
        SetHidden(true);
        InTunnel = true;
        entrance = null;
        if (animator != null) animator.SetFloat(SpeedHash, 0f);
        yield return new WaitForSeconds(tunnels.travelTime);

        // out of the entrance nearest to wherever the player is now (waits for it to be free)
        SpawnEntrance outOf = null;
        while (outOf == null)
        {
            var player = Target;
            Vector3 p = player != null ? player.transform.position : c;
            foreach (var x in ExitCandidates(p, tunnels.candidates * 2))
                if (x.e != into && x.e.Zone != into.Zone && !x.e.Busy) { outOf = x.e; break; }
            if (outOf == null) yield return new WaitForSeconds(0.25f);
        }
        InTunnel = false;
        TunnelsTaken++;
        Tunnelled?.Invoke(this, into, outOf);
        transform.position = outOf.InsidePoint;
        SetHidden(false);
        entrance = outOf;
        outOf.Use();
        yield return Emerge(outOf);
        nextPlanTime = Time.time + tunnels.replanInterval;
    }

    // Inside the pipes: not drawn, cannot be hit.
    void SetHidden(bool hidden)
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
        if (colliders == null) colliders = GetComponentsInChildren<Collider>();
        foreach (var r in renderers) if (r != null) r.enabled = !hidden;
        foreach (var c in colliders) if (c != null) c.enabled = !hidden;
    }

    void CheckStuck()
    {
        if (agent.pathPending) return;
        bool noPath = agent.pathStatus != NavMeshPathStatus.PathComplete;
        if (!noPath) { noPathSince = -1f; return; }
        if (noPathSince < 0f) noPathSince = Time.time;
        else if (Time.time - noPathSince > stuckRelocateTime && RoundManager.Instance != null)
        {
            noPathSince = -1f;
            RoundManager.Instance.Relocate(this);
        }
    }

    IEnumerator Attack(PlayerHealth player)
    {
        attacking = true;
        nextAttackTime = Time.time + attackCooldown;
        if (animator != null) animator.SetTrigger(AttackHash);
        if (NetGame.H != null) NetGame.H.EnemyAttack(health);

        yield return new WaitForSeconds(hitDelay);
        if (!health.IsDead && player != null && player.CanBeTargeted)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude <= hitRange * Mathf.Max(1f, transform.localScale.x))
                player.TakeDamage(Mathf.RoundToInt(damage * RunModifiers.Get(Stat.EnemyDamage)));
        }

        yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - hitDelay));
        attacking = false;
    }

    bool CanSee(PlayerHealth player)
    {
        Vector3 eye = head != null ? head.position : transform.position + Vector3.up * 1.2f;
        Vector3 chest = player.transform.position + Vector3.up * 0.8f;
        return !Physics.Linecast(eye, chest, sightMask, QueryTriggerInteraction.Ignore);
    }

    IEnumerator ThrowChancla(PlayerHealth player)
    {
        attacking = true;
        nextThrowTime = Time.time + Variant.throwCooldown + Random.value * 1.5f;
        if (agent.isOnNavMesh) agent.isStopped = true;
        if (animator != null) animator.SetTrigger(AttackHash);
        if (NetGame.H != null) NetGame.H.EnemyAttack(health);
        float until = Time.time + 0.35f;
        while (Time.time < until) { FacePlayer(player); yield return null; }
        if (!health.IsDead && player != null && !player.IsDead)
        {
            Vector3 from = rightHand != null ? rightHand.position : transform.position + Vector3.up * 1.2f;
            var cc = player.GetComponent<CharacterController>();
            Vector3 lead = cc != null ? cc.velocity * 0.35f : Vector3.zero;   // aims a bit ahead of the player
            lead.y = 0f;
            ChanclaProjectile.Throw(from, player.transform.position + Vector3.up * 0.8f + lead, Variant.throwSpeed, Variant.throwDamage, transform);
            Thrown?.Invoke(this);
        }
        yield return new WaitForSeconds(0.45f);
        attacking = false;
    }

    public static event System.Action<EnemyAI> Thrown;   // a chancla left the hand (tests, future audio)

    // Night old men walk over props: the body rises onto whatever prop is under it (hop up, drop down).
    void UpdateHop()
    {
        if (!agent.enabled) return;
        float top = 0f;
        Vector3 p = transform.position;
        float ground = p.y - agent.baseOffset;
        if (Physics.Raycast(new Vector3(p.x, ground + 6f, p.z), Vector3.down, out var hit, 8f, 1 << NavAgents.PropLayer, QueryTriggerInteraction.Ignore))
            top = Mathf.Max(0f, hit.point.y - ground);
        float off = agent.baseOffset;
        if (top > off + 0.05f && hopVelocity <= 0f) hopVelocity = Mathf.Sqrt(2f * 22f * (top - off + 0.3f));   // jump up onto it
        hopVelocity -= 22f * Time.deltaTime;
        off += hopVelocity * Time.deltaTime;
        if (off <= top) { off = top; hopVelocity = 0f; }
        agent.baseOffset = off;
        Hopping = off > 0.05f;
    }

    public bool Hopping { get; private set; }

    // ---------------------------------------------------------------- climbing after the player (feedback 1)
    // A player standing on a prop (car roof, crate stack, the bus, a drink machine...) is out of reach of the normal
    // NavMesh, where props block. Old men within climbStartRadius of him switch to the "Saltador" NavMesh (props
    // ignored) and hop onto the props like the night senderistas; once nobody has been up there for a while and they
    // are back on the ground they switch back. Maps without a Saltador NavMesh keep the old behaviour.
    [Header("Climbing")]
    [SerializeField] float climbStartRadius = 9f;
    [SerializeField] float climbMinHeight = 0.6f;    // the prop top must be this high over the ground to count
    [SerializeField] float climbLinger = 2.5f;       // seconds without anyone up there before climbing down for good
    int groundAgentType;
    float groundStopping = -1f;
    bool climbing;
    float nextClimbCheck, perchedSeenAt = -99f;
    public bool Climbing => climbing;
    public static event System.Action<EnemyAI> ClimbStarted;   // tests

    // Standing on a prop whose top is high over the ground (the "Saltador" NavMesh runs under props).
    public static bool Perched(Vector3 feet, float minHeight = 0.6f)
    {
        if (!Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, 1 << NavAgents.PropLayer, QueryTriggerInteraction.Ignore)) return false;
        int jumper = NavAgents.JumperId;
        if (jumper == 0) return false;
        return NavMesh.SamplePosition(hit.point + Vector3.down * 1.5f, out var ground, 2.5f, NavAgents.Filter(jumper))
            && hit.point.y - ground.position.y >= minHeight;
    }

    void UpdateClimb()
    {
        if (!agent.enabled || NavAgents.JumperId == 0 || NavAgents.JumperId == groundAgentType) return;
        if (climbing) UpdateHop();
        if (Time.time < nextClimbCheck) return;
        nextClimbCheck = Time.time + 0.4f;
        var p = Target;
        // close to him, or with no way to him on the normal NavMesh (a prop top is not on it)
        if (p != null && p.CanBeTargeted && Perched(p.transform.position, climbMinHeight)
            && ((p.transform.position - transform.position).sqrMagnitude < climbStartRadius * climbStartRadius || agent.pathStatus != NavMeshPathStatus.PathComplete))
            perchedSeenAt = Time.time;
        bool wanted = Time.time - perchedSeenAt < climbLinger;
        if (wanted && !climbing && !InTunnel && tunnelIn == null) SetClimbing(true);
        else if (!wanted && climbing && agent.baseOffset < 0.05f) SetClimbing(false);
    }

    void SetClimbing(bool on)
    {
        int id = on ? NavAgents.JumperId : groundAgentType;
        Vector3 feet = transform.position - Vector3.up * agent.baseOffset;
        if (!NavAgents.Sample(feet, on ? 2f : 1.5f, out var at, id)) return;   // (climbing down inside a prop: try again later)
        agent.enabled = false;
        agent.agentTypeID = id;
        agent.baseOffset = 0f;
        hopVelocity = 0f;
        Hopping = false;
        agent.enabled = true;
        agent.Warp(at);
        // he must get right under the player to hop onto the prop (a crate is 0.9 m wide: the usual 1 m stop is beside it)
        if (groundStopping < 0f) groundStopping = agent.stoppingDistance;
        agent.stoppingDistance = on ? 0.1f : groundStopping;
        climbing = on;
        nextRepathTime = 0f;
        if (on) ClimbStarted?.Invoke(this);
    }

    // ---------------------------------------------------------------- one-way links (ledge drops, slides)
    bool onLink;
    public static event System.Action<EnemyAI> LinkJumped;   // tests

    // Jumps from the link start to its end in an arc (old men only go DOWN ledges: the links are one-way).
    IEnumerator TraverseLink()
    {
        onLink = true;
        var data = agent.currentOffMeshLinkData;
        Vector3 a = transform.position;
        Vector3 b = data.endPos + Vector3.up * agent.baseOffset;
        Vector3 flat = b - a; flat.y = 0f;
        if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
        float drop = Mathf.Max(0f, a.y - b.y);
        float time = Mathf.Clamp(Mathf.Max(flat.magnitude / Mathf.Max(1f, agent.speed), Mathf.Sqrt(2f * drop / 20f) + 0.2f), 0.35f, 1.2f);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            transform.position = Vector3.Lerp(a, b, k) + Vector3.up * (0.6f * 4f * k * (1f - k));
            yield return null;
        }
        transform.position = b;
        if (agent.enabled && agent.isOnOffMeshLink) agent.CompleteOffMeshLink();
        onLink = false;
        LinkJumped?.Invoke(this);
    }

    void FacePlayer(PlayerHealth player)
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), turnSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        // runs after the Animator: tilt the upper body back while flinching
        if (spine == null || Time.time >= flinchUntil || health.IsDead) return;
        float k = (flinchUntil - Time.time) / flinchTime;
        spine.rotation = Quaternion.AngleAxis(-flinchAngle * k, transform.right) * spine.rotation;
    }

    void OnDied(EnemyHealth _, DamageInfo __)
    {
        drummers.Remove(this);
        if (Variant != null && Variant.deathCloudRadius > 0f) GasCloud.Spawn(transform.position, Variant.deathCloudRadius, Variant.deathCloudSeconds, Variant.deathCloudDps);
        StopAllCoroutines();
        attacking = false;
        onLink = false;
        if (entrance != null) { entrance.Release(); entrance = null; }
        emerging = false;
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
        }
        Destroy(gameObject, destroyDelay);
    }
}
