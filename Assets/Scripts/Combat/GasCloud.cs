using UnityEngine;

// The cloud a plague doctor leaves where he dies (EnemyVariant.deathCloudRadius): it hurts whoever stands in it for
// a few seconds, so you can't hold the spot where you killed him. Every machine spawns its own from the death it
// sees and only hurts its local players (owner-authoritative players): nothing goes over the network.
public class GasCloud : MonoBehaviour
{
    float radius, until, nextTick, total;
    int damagePerTick;
    Transform visual;
    Material mat;
    const float Tick = 0.5f;

    public static int Alive { get; private set; }   // tests

    public static GasCloud Spawn(Vector3 position, float radius, float seconds, float dps)
    {
        var go = new GameObject("NubePeste");
        go.transform.position = position;
        var c = go.AddComponent<GasCloud>();
        c.radius = radius;
        c.total = seconds;
        c.until = Time.time + seconds;
        c.damagePerTick = Mathf.Max(1, Mathf.RoundToInt(dps * Tick));
        c.Build();
        return c;
    }

    void Build()
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(s.GetComponent<Collider>());
        visual = s.transform;
        visual.SetParent(transform, false);
        visual.localPosition = Vector3.up * 0.7f;
        visual.localScale = new Vector3(radius * 2f, 1.6f, radius * 2f);
        var shader = Shader.Find("Sprites/Default");   // unlit, alpha blended, always in the build (UI uses it)
        if (shader != null)
        {
            mat = new Material(shader) { color = new Color(0.45f, 0.75f, 0.2f, 0.4f) };
            s.GetComponent<Renderer>().sharedMaterial = mat;
        }
        s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void OnEnable() => Alive++;
    void OnDisable() => Alive--;

    void Update()
    {
        float left = until - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        if (visual != null)
        {
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 5f);
            visual.localScale = new Vector3(radius * 2f * pulse, 1.6f, radius * 2f * pulse);
            if (mat != null) mat.color = new Color(0.45f, 0.75f, 0.2f, 0.4f * Mathf.Clamp01(left / Mathf.Min(1f, total)));
        }
        if (Time.time < nextTick) return;
        nextTick = Time.time + Tick;
        var gm = GameManager.Instance;
        if (gm == null) return;
        foreach (var p in gm.Players)
        {
            if (p == null || p.IsDead || p.IsDowned || PlayerSlot.IsProxy(p)) continue;
            Vector3 d = p.transform.position - transform.position;
            if (Mathf.Abs(d.y) > 2f) continue;
            d.y = 0f;
            if (d.magnitude <= radius) p.TakeDamage(damagePerTick);
        }
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}
