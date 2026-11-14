using UnityEngine;

// Top-down picture of the map (rendered by the map setup tool) and the world area (x, z) it covers.
// Used by the pause-menu map.
public class MapInfo : MonoBehaviour
{
    public static MapInfo Instance { get; private set; }

    [SerializeField] Texture2D mapTexture;
    [SerializeField] Vector2 worldMin, worldMax;

    public Texture2D Texture => mapTexture;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public Vector2 ToUV(Vector3 p) => new Vector2(Mathf.InverseLerp(worldMin.x, worldMax.x, p.x), Mathf.InverseLerp(worldMin.y, worldMax.y, p.z));
}
