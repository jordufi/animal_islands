using UnityEngine;

// La Serenisima carnival masks: the VN_MaskPaint slot of the model gets one of three colour schemes per old man
// (ivory, black and gold, red), seeded by EnemyHealth.NetId so every machine of an online match sees the same mask.
// Shatter() replaces the mask with 4 shards that fly off when the shield breaks.
public class MaskLook : MonoBehaviour
{
    static readonly Color[] Paints = { new Color(0.93f, 0.9f, 0.82f), new Color(0.07f, 0.07f, 0.08f), new Color(0.68f, 0.07f, 0.1f) };
    static Material[] paintMats;

    EnemyHealth owner;

    public static void Dress(GameObject mask, EnemyHealth owner)
    {
        if (mask == null) return;
        var look = mask.AddComponent<MaskLook>();
        look.owner = owner;
    }

    void Start()
    {
        // Start runs a frame after the spawn, when the host has already given the old man his NetId
        int seed = owner != null && owner.NetId != 0 ? owner.NetId : Random.Range(0, 3);
        if (paintMats == null)
        {
            paintMats = new Material[Paints.Length];
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            for (int i = 0; i < Paints.Length; i++)
            {
                paintMats[i] = new Material(sh) { name = "VN_MaskPaint_" + i, color = Paints[i] };
                paintMats[i].SetFloat("_Smoothness", i == 1 ? 0.7f : 0.4f);
            }
        }
        var m = paintMats[Mathf.Abs(seed) % paintMats.Length];
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            bool hit = false;
            for (int i = 0; i < ms.Length; i++)
                if (ms[i] != null && ms[i].name.StartsWith("VN_MaskPaint")) { ms[i] = m; hit = true; }
            if (hit) r.sharedMaterials = ms;
        }
    }

    // Four shards in the mask's main colour, thrown from where the mask was; the mask itself is destroyed.
    public static void Shatter(GameObject mask, Vector3 push)
    {
        if (mask == null) return;
        var r = mask.GetComponentInChildren<Renderer>();
        Material mat = null;
        if (r != null)
            foreach (var m in r.sharedMaterials)
                if (m != null && (m.name.StartsWith("VN_MaskPaint") || m.name.StartsWith("VN_Gold") || m.name.StartsWith("Gold"))) { mat = m; break; }
        var b = r != null ? r.bounds : new Bounds(mask.transform.position, Vector3.one * 0.4f);
        for (int i = 0; i < 4; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "MaskShard";
            Destroy(s.GetComponent<Collider>());
            s.transform.position = b.center + new Vector3((i % 2 - 0.5f) * b.extents.x, (i / 2 - 0.5f) * b.extents.y, 0f);
            s.transform.rotation = Random.rotation;
            s.transform.localScale = new Vector3(b.size.x * 0.45f, b.size.y * 0.35f, 0.04f);
            if (mat != null) s.GetComponent<Renderer>().sharedMaterial = mat;
            var rb = s.AddComponent<Rigidbody>();
            rb.linearVelocity = push + Vector3.up * Random.Range(2.5f, 4f) + Random.insideUnitSphere * 1.8f;
            rb.angularVelocity = Random.insideUnitSphere * 14f;
            Destroy(s, 2.2f);
        }
        Destroy(mask);
    }
}
