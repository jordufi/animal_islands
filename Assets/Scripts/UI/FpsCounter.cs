using UnityEngine;
using UnityEngine.UI;

// Frame counter at the top centre of the screen (Ajustes > Contador de FPS). Lives for the whole session
// (menu and maps); shows the average of the last half second: "60 FPS · 16.7 ms".
public class FpsCounter : MonoBehaviour
{
    const float Window = 0.5f;

    Text label;
    GameObject canvasGo;
    int frames;
    float elapsed;

    public static FpsCounter Instance { get; private set; }
    public float Fps { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("FpsCounter");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<FpsCounter>();
    }

    void Awake()
    {
        var canvas = UIKit.CreateCanvas("FpsCanvas", 90);
        canvasGo = canvas.gameObject;
        canvasGo.transform.SetParent(transform, false);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        label = UIKit.Label("Fps", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(360, 34), "", 24, TextAnchor.UpperCenter, new Color(0.7f, 1f, 0.7f));
        label.raycastTarget = false;
        label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        bool on = GameSettings.ShowFps;
        if (canvasGo.activeSelf != on) canvasGo.SetActive(on);
        frames++;
        elapsed += Time.unscaledDeltaTime;   // keeps counting while paused (timeScale 0)
        if (elapsed < Window) return;
        Fps = frames / elapsed;
        frames = 0;
        elapsed = 0f;
        if (!on) return;
        label.text = Mathf.RoundToInt(Fps) + " FPS · " + (1000f / Mathf.Max(1f, Fps)).ToString("0.0") + " ms";
        label.color = Fps >= 55f ? new Color(0.7f, 1f, 0.7f) : Fps >= 30f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.45f, 0.4f);
    }
}
