using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Final boss "Don Anselmo, presidente de la comunidad": a giant old man that chases the player,
// does a telegraphed cane slam (area damage) and keeps calling other old men. Below half health
// he gets angry: faster, slams more often and calls more neighbours.
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class BossAI : MonoBehaviour
{
    [SerializeField] float moveSpeed = 1.8f;
    [SerializeField] float angrySpeed = 2.6f;
    [SerializeField] float slamRange = 3.2f;
    [SerializeField] float slamRadius = 4.2f;
    [SerializeField] int slamDamage = 50;
    [SerializeField] float slamWindup = 0.8f;
    [SerializeField] float slamCooldown = 3f;
    [SerializeField] float summonInterval = 14f;
    [SerializeField] int summonCount = 4;
    [SerializeField] float walkClipSpeed = 1.7f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int WalkSpeedHash = Animator.StringToHash("WalkSpeed");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int DieHash = Animator.StringToHash("Die");

    NavMeshAgent agent;
    EnemyHealth health;
    Animator animator;
    float nextSlamTime, nextSummonTime;
    bool slamming, angry;

    public EnemyHealth Health => health;
    public bool Angry => angry;
    public event Action Defeated;

    // Online client: the host's puppet (NetEnemy moves him).
    public bool IsPuppet { get; private set; }
    public void MakePuppet()
    {
        IsPuppet = true;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
    }

    PlayerHealth Target => GameManager.Instance != null ? GameManager.Instance.NearestTarget(transform.position) : null;   // co-op: the nearest standing player

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        animator = GetComponentInChildren<Animator>();
        health.Died += OnDied;
    }

    public void Configure(float maxHealth)
    {
        health.Init(maxHealth);
        health.PointsMultiplier = 10f;
        agent.speed = moveSpeed * RunModifiers.Get(Stat.EnemySpeed);
        nextSummonTime = Time.time + summonInterval * 0.5f;
        nextSlamTime = Time.time + 2f;
        UpdateWalkSpeed();
    }

    void UpdateWalkSpeed()
    {
        if (animator != null) animator.SetFloat(WalkSpeedHash, agent.speed / (walkClipSpeed * transform.localScale.x) * 1.6f);
    }

    void Update()
    {
        if (health.IsDead || IsPuppet) return;
        if (animator != null) animator.SetFloat(SpeedHash, agent.velocity.magnitude / Mathf.Max(0.1f, transform.localScale.x), 0.1f, Time.deltaTime);

        if (!angry && health.Current < health.MaxHealth * 0.5f)
        {
            angry = true;
            agent.speed = angrySpeed * RunModifiers.Get(Stat.EnemySpeed);
            UpdateWalkSpeed();
            GameManager.Instance?.ShowMessage("¡Don Anselmo se ha enfadado! \"¡ESTO VA AL ACTA!\"");
        }

        var player = Target;
        if (player == null || !player.CanBeTargeted) { if (agent.isOnNavMesh) agent.isStopped = true; return; }

        if (Time.time >= nextSummonTime) Summon();
        if (slamming) return;

        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude <= slamRange && Time.time >= nextSlamTime)
        {
            StartCoroutine(Slam(player));
        }
        else if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.transform.position);
        }
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
    }

    IEnumerator Slam(PlayerHealth player)
    {
        slamming = true;
        if (agent.isOnNavMesh) agent.isStopped = true;
        if (animator != null) animator.SetTrigger(AttackHash);
        if (NetGame.H != null) NetGame.H.EnemyAttack(health);
        yield return new WaitForSeconds(slamWindup);
        if (!health.IsDead && GameManager.Instance != null)
            foreach (var p in GameManager.Instance.Players)   // the slam hits everyone in the area
            {
                if (p == null || p.IsDead || p.IsDowned) continue;
                Vector3 d = p.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= slamRadius) p.TakeDamage(Mathf.RoundToInt(slamDamage * RunModifiers.Get(Stat.EnemyDamage)));
            }
        yield return new WaitForSeconds(0.6f);
        nextSlamTime = Time.time + slamCooldown * (angry ? 0.6f : 1f);
        slamming = false;
    }

    void Summon()
    {
        nextSummonTime = Time.time + summonInterval * (angry ? 0.7f : 1f);
        var rm = RoundManager.Instance;
        if (rm == null) return;
        int n = summonCount + (angry ? 2 : 0);
        for (int i = 0; i < n; i++) rm.SpawnEnemy(null, transform.position);
        GameManager.Instance?.ShowMessage("Don Anselmo convoca a la junta de vecinos");
    }

    void OnDied(EnemyHealth _, DamageInfo __)
    {
        StopAllCoroutines();
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (animator != null) animator.SetTrigger(DieHash);
        Defeated?.Invoke();
    }
}
