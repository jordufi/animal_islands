using UnityEngine;

// A flip-flop thrown in an arc by the night old men. Hurts the player on contact; anything else stops it.
public class ChanclaProjectile : MonoBehaviour
{
    const float Gravity = -9f;
    const float Radius = 0.18f;

    Vector3 velocity;
    int damage;
    float dieAt;
    bool spent;
    int mask;
    Transform owner;

    static Material sole, strap;

    public static ChanclaProjectile Throw(Vector3 from, Vector3 target, float speed, int damage, Transform owner)
    {
        if (NetGame.H != null) NetGame.H.ChanclaThrown(from, target, speed);   // the others see it fly
        return Create(from, target, speed, damage, owner);
    }

    // Online client: the same flip-flop, harmless (the host decides who it hits).
    public static ChanclaProjectile ThrowVisual(Vector3 from, Vector3 target, float speed)
    {
        var c = Create(from, target, speed, 0, null);
        c.visualOnly = true;
        return c;
    }

    bool visualOnly;
    public static int Created { get; private set; }   // tests

    static ChanclaProjectile Create(Vector3 from, Vector3 target, float speed, int damage, Transform owner)
    {
        Created++;
        var go = new GameObject("Chancla");
        go.transform.position = from;
        if (sole == null) sole = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.25f, 0.75f, 0.3f) };
        if (strap == null) strap = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.95f, 0.85f, 0.2f) };
        Part(go.transform, new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.035f, 0.32f), sole);
        Part(go.transform, new Vector3(0f, 0.035f, 0.06f), new Vector3(0.12f, 0.03f, 0.03f), strap);
        var c = go.AddComponent<ChanclaProjectile>();
        c.owner = owner;
        c.damage = damage;
        c.dieAt = Time.time + 4f;
        c.mask = ~((1 << 9) | (1 << 2) | (1 << 4));   // not enemies, Ignore Raycast or water blockers
        // ballistic arc that reaches the target in distance / speed seconds
        float T = Mathf.Clamp(Vector3.Distance(from, target) / Mathf.Max(1f, speed), 0.35f, 1.6f);
        c.velocity = (target - from) / T - 0.5f * new Vector3(0f, Gravity, 0f) * T;
        return c;
    }

    static void Part(Transform parent, Vector3 pos, Vector3 size, Material m)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false);
        p.transform.localPosition = pos;
        p.transform.localScale = size;
        p.GetComponent<Renderer>().sharedMaterial = m;
    }

    void Update()
    {
        if (Time.time >= dieAt) { Destroy(gameObject); return; }
        if (spent) return;
        float dt = Time.deltaTime;
        Vector3 step = velocity * dt + 0.5f * new Vector3(0f, Gravity, 0f) * dt * dt;
        velocity += new Vector3(0f, Gravity, 0f) * dt;
        if (Physics.SphereCast(transform.position, Radius, step.normalized, out var hit, step.magnitude, mask, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.collider.transform.IsChildOf(owner)) { transform.position += step; return; }
            var player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                if (!player.IgnoredByEnemies && !visualOnly) player.TakeDamage(Mathf.RoundToInt(damage * RunModifiers.Get(Stat.EnemyDamage)));
                Destroy(gameObject);
                return;
            }
            // hit scenery: drop there
            transform.position = hit.point + hit.normal * Radius;
            spent = true;
            dieAt = Time.time + 2.5f;
            return;
        }
        transform.position += step;
        transform.Rotate(720f * dt, 0f, 0f, Space.Self);
    }
}
