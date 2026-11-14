using UnityEngine;

// Client side of an old man (or the boss) online: a puppet that the host's snapshots move. Keeps its
// colliders so the local player can shoot it (the hit goes to the host), plays the walk / attack / death
// animations, and hides while he is inside the "pipes" (Anselmo cagón).
public class NetEnemy : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    public int Id { get; set; }

    readonly NetInterp interp = new NetInterp();
    Animator animator;
    EnemyHealth health;
    Renderer[] renderers;
    Collider[] colliders;
    bool hidden;
    float speed;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<EnemyHealth>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public void Push(float t, Vector3 p, float yaw, float pitch, float animSpeed, bool isHidden, float hpFraction)
    {
        interp.Add(t, p, Quaternion.Euler(pitch, yaw, 0f));
        speed = animSpeed;
        if (health != null && !health.IsDead) health.NetSync(hpFraction * health.MaxHealth);
        if (isHidden != hidden)
        {
            hidden = isHidden;
            foreach (var r in renderers) if (r != null) r.enabled = !hidden;
            foreach (var c in colliders) if (c != null) c.enabled = !hidden;
        }
    }

    public void Attack()
    {
        if (animator != null) animator.SetTrigger("Attack");
    }

    void Update()
    {
        if (health != null && health.IsDead) return;
        if (interp.Sample(Time.deltaTime, out var p, out var r)) transform.SetPositionAndRotation(p, r);
        if (animator != null) animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
    }
}
