using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// La dentadura postiza (user feedback 1, idea 7): the funny grenade. R1 / G throws a set of false teeth in an arc; when
// it lands (or its fuse runs out) it bites: damage, stun and a little push on every old man in the radius, paid like an
// ability kill (numbers in EconomyData, "Dentadura postiza"). It spins in the air and chomps big when it bites.
// The thrower's machine does the damage through EnemyHits (online: a client sends its hits to the host); the other
// machines only see it (NetPlayer, PlayerFx kind 3).
public class Dentadura : MonoBehaviour
{
    Vector3 velocity;
    float fuseLeft;
    Component owner;
    bool hurts, bitten;
    Transform jawTop, jawBottom;

    public static event System.Action<Dentadura, int> Bit;   // (teeth, old men hit) for tests
    public bool Hurts => hurts;

    static Material gum, tooth;
    const int Mask = (1 << 0) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12);   // default, enemies, doors, walls, props

    public static Dentadura Throw(Vector3 from, Vector3 velocity, Component owner, bool hurts)
    {
        var go = new GameObject("Dentadura");
        go.transform.position = from;
        var d = go.AddComponent<Dentadura>();
        d.velocity = velocity;
        d.owner = owner;
        d.hurts = hurts;
        var eco = GameManager.Instance != null ? GameManager.Instance.Economy : null;
        d.fuseLeft = eco != null ? eco.dentaduraFuse : 1.5f;
        d.Build();
        return d;
    }

    static Material Mat(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader);
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        return m;
    }

    // Two jaws: a pink gum arch with white teeth on top, the lower one upside down. ~0.16 m wide.
    void Build()
    {
        if (gum == null) gum = Mat(new Color(0.95f, 0.45f, 0.55f));
        if (tooth == null) tooth = Mat(new Color(0.97f, 0.96f, 0.9f));
        jawTop = Jaw("Arriba", 1f);
        jawBottom = Jaw("Abajo", -1f);
    }

    Transform Jaw(string name, float up)
    {
        var jaw = new GameObject(name).transform;
        jaw.SetParent(transform, false);
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.Lerp(-80f, 80f, i / 6f) * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(a) * 0.07f, 0f, Mathf.Cos(a) * 0.055f);
            Part(jaw, PrimitiveType.Cube, p + Vector3.up * 0.012f * up, new Vector3(0.026f, 0.018f, 0.022f), gum, a);
            Part(jaw, PrimitiveType.Cube, p - Vector3.up * 0.006f * up, new Vector3(0.018f, 0.02f, 0.012f), tooth, a);
        }
        return jaw;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, float yaw)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        if (bitten) return;
        float dt = Time.deltaTime;
        fuseLeft -= dt;
        velocity += Vector3.up * -20f * dt;
        Vector3 step = velocity * dt;
        // chatters in the air
        float chat = Mathf.Abs(Mathf.Sin(Time.time * 25f)) * 25f;
        jawTop.localRotation = Quaternion.Euler(-chat, 0f, 0f);
        jawBottom.localRotation = Quaternion.Euler(chat, 0f, 0f);
        transform.Rotate(Vector3.up, 540f * dt, Space.World);
        if (step.sqrMagnitude > 0f && Physics.SphereCast(transform.position, 0.08f, step.normalized, out var hit, step.magnitude, Mask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + hit.normal * 0.08f;
            StartCoroutine(BiteNow());
            return;
        }
        transform.position += step;
        if (fuseLeft <= 0f || transform.position.y < -20f) StartCoroutine(BiteNow());
    }

    IEnumerator BiteNow()
    {
        bitten = true;
        var eco = GameManager.Instance != null ? GameManager.Instance.Economy : null;
        float radius = eco != null ? eco.dentaduraRadius : 3.5f;
        int hits = 0;
        if (hurts)
        {
            var done = new HashSet<EnemyHealth>();
            foreach (var c in Physics.OverlapSphere(transform.position, radius, 1 << 9, QueryTriggerInteraction.Collide))
            {
                var h = c.GetComponentInParent<EnemyHealth>();
                if (h == null || h.IsDead || !done.Add(h)) continue;
                EnemyHits.Apply(h, eco != null ? eco.dentaduraDamage : 400f, false, HitKind.Ability, owner, true,
                    eco != null ? eco.dentaduraStun : 2.5f, 1.2f);
                hits++;
            }
        }
        Bit?.Invoke(this, hits);
        // the bite: big, three chomps, gone
        foreach (var p in GameManager.Instance != null ? GameManager.Instance.Players : new List<PlayerHealth>())
        {
            if (p == null || (p.transform.position - transform.position).sqrMagnitude > 64f) continue;
            var rig = PlayerSlot.RigOf(p);
            if (rig != null) rig.Shake(0.25f, 0.15f);
        }
        transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Camera.main != null ? Camera.main.transform.position - transform.position : Vector3.forward, Vector3.up).normalized + Vector3.forward * 0.001f);
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.12f);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 7f, k) * (t > 0.55f ? Mathf.Clamp01((0.7f - t) / 0.15f) : 1f);
            float open = Mathf.Abs(Mathf.Sin(t / 0.7f * Mathf.PI * 3f)) * 40f;
            jawTop.localRotation = Quaternion.Euler(-open, 0f, 0f);
            jawBottom.localRotation = Quaternion.Euler(open, 0f, 0f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
