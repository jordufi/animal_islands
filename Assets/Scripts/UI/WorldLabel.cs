using UnityEngine;

// Floating 3D text above props (prices, names). Always faces the camera and is hidden behind walls
// (AnimalIsland/WorldText shader; falls back to the font's own material if the shader is missing).
public class WorldLabel : MonoBehaviour
{
    static Material material;

    static Material SharedMaterial
    {
        get
        {
            if (material != null) return material;
            var shader = Shader.Find("AnimalIsland/WorldText");
            if (shader == null) return UIKit.Font.material;
            material = new Material(shader) { name = "WorldText", mainTexture = UIKit.Font.material.mainTexture };
            Font.textureRebuilt += f => { if (f == UIKit.Font && material != null) material.mainTexture = f.material.mainTexture; };
            return material;
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

    void Start()
    {
        var r = GetComponent<MeshRenderer>();
        if (r != null && Application.isPlaying) r.sharedMaterial = SharedMaterial;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
