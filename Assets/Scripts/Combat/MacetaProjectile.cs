using UnityEngine;

// Villajubilada: a flowerpot lobbed from a window during the first encierro. Slow on purpose: a red ring on the
// ground shows where it lands, you step aside. It only hurts the players of this machine (owner-authoritative).
public class MacetaProjectile : MonoBehaviour
{
    Vector3 from, to;
    float duration, t, radius;
    int damage;
    Transform ring;
    static Material pot, plant, mark;

    public static int Landed { get; private set; }   // tests
    public static int Hits { get; private set; }

    public static MacetaProjectile Throw(Vector3 from, Vector3 target, float speed, int damage, float radius)
    {
        if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out var g, 8f, ~((1 << 8) | (1 << 9) | (1 << 4)), QueryTriggerInteraction.Ignore)) target = g.point;
        var go = new GameObject("Maceta");
        go.transform.position = from;
        var m = go.AddComponent<MacetaProjectile>();
        m.from = from; m.to = target; m.damage = damage; m.radius = radius;
        m.duration = Mathf.Max(0.8f, Vector3.Distance(from, target) / Mathf.Max(0.5f, speed));
        m.Build();
        return m;
    }

    void Build()
    {
        if (pot == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            pot = new Material(sh) { color = new Color(0.72f, 0.4f, 0.25f) };
            plant = new Material(sh) { color = new Color(0.85f, 0.12f, 0.15f) };
            mark = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.15f, 0.1f, 0.45f) };
        }
        Part(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(0.36f, 0.16f, 0.36f), pot);
        Part(PrimitiveType.Sphere, transform, Vector3.up * 0.24f, Vector3.one * 0.36f, plant);
        ring = Part(PrimitiveType.Cylinder, null, to + Vector3.up * 0.03f, new Vector3(radius * 2f, 0.01f, radius * 2f), mark);
    }

    static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        if (parent != null) g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    void Update()
    {
        t += Time.deltaTime / duration;
        float k = Mathf.Clamp01(t);
        transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * 1.2f * k * (1f - k));   // a lob 1.2 m over the line
        transform.Rotate(200f * Time.deltaTime, 90f * Time.deltaTime, 0f);
        if (t < 1f) return;
        Landed++;
        var gm = GameManager.Instance;
        if (gm != null)
            foreach (var p in gm.Players)
            {
                if (p == null || p.IsDead || p.IsDowned || PlayerSlot.IsProxy(p)) continue;
                Vector3 d = p.transform.position - to;
                if (Mathf.Abs(d.y) > 2f) continue;
                d.y = 0f;
                if (d.magnitude > radius) continue;
                p.TakeDamage(Mathf.RoundToInt(damage * RunModifiers.Get(Stat.EnemyDamage)));
                Hits++;
            }
        if (ring != null) Destroy(ring.gameObject);
        Destroy(gameObject);
    }

    void OnDestroy() { if (ring != null) Destroy(ring.gameObject); }
}
