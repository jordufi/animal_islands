using UnityEngine;

// Floating 3D text above props (prices, names). Fixed facing the open side (pickups face the camera); hidden behind walls
// (AnimalIsland/WorldText shader; falls back to the font's own material if the shader is missing).
public class WorldLabel : MonoBehaviour
{
    static Material material, oneSided;

    static Material SharedMaterial
    {
        get
        {
            if (material != null) return material;
            var shader = Shader.Find("AnimalIsland/WorldText");
            if (shader == null) return UIKit.Font.material;
            material = new Material(shader) { name = "WorldText", mainTexture = UIKit.Font.material.mainTexture };
            Font.textureRebuilt += f =>
            {
                if (f != UIKit.Font) return;
                if (material != null) material.mainTexture = f.material.mainTexture;
                if (oneSided != null) oneSided.mainTexture = f.material.mainTexture;
            };
            return material;
        }
    }

    // Fixed labels: only the readable face is drawn (a TextMesh reads right from its -Z side), so the front text and its
    // back copy never show through each other mirrored.
    static Material OneSidedMaterial
    {
        get
        {
            if (oneSided != null) return oneSided;
            var shared = SharedMaterial;
            if (shared == UIKit.Font.material) return shared;   // no WorldText shader
            oneSided = new Material(shared) { name = "WorldTextOneSided" };
            oneSided.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            return oneSided;
        }
    }

    public static TextMesh Create(Transform parent, Vector3 localPosition, string text, float size, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var tm = go.AddComponent<TextMesh>();
        tm.font = UIKit.Font;
        go.GetComponent<MeshRenderer>().sharedMaterial = UIKit.Font.material;   // swapped for the depth-tested one at runtime
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;
        tm.characterSize = size / 10f;
        tm.text = text;
        tm.color = color;
        go.AddComponent<WorldLabel>();
        // undo parent scale so labels keep a constant size
        Vector3 s = parent.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
        return tm;
    }

    // Labels on things that move or are picked up keep turning to the camera; the rest (machines, doors, wall
    // buys, cabins...) stay still, facing the open side of their spot, with a copy on the back (user feedback
    // 2026-11-17: "taquilla" and weapon names followed the camera).
    bool billboard;
    [SerializeField] Vector3 readFrom;   // fixed reading side set by the builder (a cabin's plaque); zero = the most open side
    TextMesh text, back;

    // The label reads right from this horizontal direction (and from the opposite one through its back copy).
    public static void ReadFrom(TextMesh label, Vector3 direction)
    {
        var wl = label != null ? label.GetComponent<WorldLabel>() : null;
        if (wl != null) { direction.y = 0f; wl.readFrom = direction.normalized; }
    }
    MeshRenderer backRenderer, ownRenderer;

    void Start()
    {
        var r = GetComponent<MeshRenderer>();
        if (r != null && Application.isPlaying) r.sharedMaterial = SharedMaterial;
        if (!Application.isPlaying) return;
        text = GetComponent<TextMesh>();
        billboard = GetComponentInParent<Rigidbody>() != null || GetComponentInParent<Carryable>() != null
            || GetComponentInParent<DiscPickup>() != null || GetComponentInParent<MasterKey>() != null
            || GetComponentInParent<MissionItem>() != null || GetComponentInParent<AmmoPickup>() != null;
        if (billboard || text == null) return;
        transform.rotation = Quaternion.LookRotation(-(readFrom.sqrMagnitude > 0.01f ? readFrom : OpenSide()), Vector3.up);
        if (r != null) r.sharedMaterial = OneSidedMaterial;
        back = MakeBack();
    }

    // Horizontal direction with the most free room round the label's owner (walls, props, facades), smoothed with
    // its neighbours. Measured from the parent so the black shadow copies of a sign pick the same side.
    Vector3 OpenSide()
    {
        Transform owner = transform.parent != null ? transform.parent : transform;
        Vector3 origin = new Vector3(transform.position.x, owner.position.y + 1.2f, transform.position.z);
        const int N = 16; const float Reach = 5f;
        var free = new float[N];
        int mask = ~((1 << 4) | (1 << 8) | (1 << 9));   // not water, players, enemies
        for (int i = 0; i < N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            free[i] = Reach;
            var hits = Physics.RaycastAll(origin, dir, Reach, mask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (!h.collider.transform.IsChildOf(owner) && h.distance < free[i]) free[i] = h.distance;
        }
        int best = 0; float bestScore = -1f;
        for (int i = 0; i < N; i++)
        {
            float s = free[i] * 2f + free[(i + 1) % N] + free[(i + N - 1) % N];
            if (s > bestScore + 0.01f) { bestScore = s; best = i; }
        }
        float ba = best * Mathf.PI * 2f / N;
        return new Vector3(Mathf.Sin(ba), 0f, Mathf.Cos(ba));
    }

    TextMesh MakeBack()
    {
        var go = new GameObject("Back");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = text.font; tm.anchor = text.anchor; tm.alignment = text.alignment; tm.fontSize = text.fontSize;
        tm.characterSize = text.characterSize; tm.text = text.text; tm.color = text.color; tm.lineSpacing = text.lineSpacing;
        go.GetComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        return tm;
    }

    void LateUpdate()
    {
        if (!billboard)
        {
            if (back != null)
            {
                if (!ReferenceEquals(back.text, text.text)) back.text = text.text;
                if (back.color != text.color) back.color = text.color;
                if (back.characterSize != text.characterSize) back.characterSize = text.characterSize;
                if (backRenderer == null) { backRenderer = back.GetComponent<MeshRenderer>(); ownRenderer = GetComponent<MeshRenderer>(); }
                if (backRenderer.enabled != ownRenderer.enabled) backRenderer.enabled = ownRenderer.enabled;
            }
            return;
        }
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
