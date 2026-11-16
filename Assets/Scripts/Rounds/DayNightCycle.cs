using System.Collections;
using System.Linq;
using UnityEngine;

// Night rounds: when the RoundManager starts a night round the island goes dark (sun, ambient,
// skybox exposure, fog), the lighthouse gets brighter with a rotating beam and the camera carries a
// weak torch so the player can still read the lanes. When the round ends it dawns again and a
// "Nevera de munición" drops next to the player (the reward, like the dog rounds in Zombies).
public class DayNightCycle : MonoBehaviour
{
    [SerializeField] float transitionTime = 3f;
    [SerializeField] Color nightSunColor = new Color(0.45f, 0.55f, 0.95f);
    [SerializeField] float nightSunIntensity = 0.12f;
    [SerializeField] float nightAmbient = 0.28f;
    [SerializeField] float nightSkyExposure = 0.12f;
    [SerializeField] Color nightFog = new Color(0.03f, 0.05f, 0.1f);
    [SerializeField] float nightFogDensity = 0.035f;
    [SerializeField] float torchIntensity = 2.2f;

    Light sun, beam, faro;
    readonly System.Collections.Generic.List<Light> torches = new System.Collections.Generic.List<Light>();   // one per player camera
    Color daySunColor;
    float daySunIntensity, dayAmbient, dayReflection, dayFaroIntensity, dayFaroRange;
    bool dayFog;
    Color dayFogColor;
    float dayFogDensity;
    FogMode dayFogMode;
    Material sky;
    float daySkyExposure = 1f;
    float night;   // 0 day .. 1 night
    Coroutine routine;

    public bool IsDark => night > 0.5f;
    float glow;   // Villajubilada: every zone whose fiesta lights are on takes some of the dark away
    public void AddGlow(float amount) { glow += amount; Apply(night); }
    public bool HoldNight { get; private set; }   // Villajubilada: it never dawns again

    // Night falls in `seconds` and stays for the rest of the run (no senderistas, no dawn reward).
    public void FallForGood(float seconds)
    {
        HoldNight = true;
        EnsureTorch();
        if (routine != null) StopCoroutine(routine);
        transitionTime = Mathf.Max(0.1f, seconds);
        routine = StartCoroutine(Transition(1f));
    }
    public float Darkness => night;

    void Start()
    {
        sun = RenderSettings.sun;
        if (sun == null) foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null) { daySunColor = sun.color; daySunIntensity = sun.intensity; }
        dayAmbient = RenderSettings.ambientIntensity;
        dayReflection = RenderSettings.reflectionIntensity;
        dayFog = RenderSettings.fog;
        dayFogColor = RenderSettings.fogColor;
        dayFogDensity = RenderSettings.fogDensity;
        dayFogMode = RenderSettings.fogMode;
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
        {
            sky = new Material(RenderSettings.skybox);   // a copy: never touch the asset
            daySkyExposure = sky.GetFloat("_Exposure");
            RenderSettings.skybox = sky;
        }
        var faroGo = GameObject.Find("Faro_Luz");
        if (faroGo != null)
        {
            faro = faroGo.GetComponent<Light>();
            dayFaroIntensity = faro.intensity;
            dayFaroRange = faro.range;
            beam = new GameObject("Faro_Haz").AddComponent<Light>();
            beam.transform.SetParent(faroGo.transform, false);
            beam.type = LightType.Spot;
            beam.spotAngle = 28f;
            beam.range = 60f;
            beam.color = new Color(1f, 0.92f, 0.65f);
            beam.intensity = 0f;
            beam.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            beam.enabled = false;
        }

        var rm = RoundManager.Instance;
        if (rm != null)
        {
            rm.RoundStarted += r => { if (rm.IsNight) Go(true); };
            rm.RoundEnded += r => { if (night > 0f && !HoldNight) { Go(false); Reward(); } };
        }
    }

    void OnDestroy()
    {
        if (sky != null) Destroy(sky);
    }

    void EnsureTorch()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.PlayerCount > 0)
            foreach (var p in gm.Players) AddTorch(PlayerSlot.CameraOf(p));
        else AddTorch(Camera.main);
    }

    void AddTorch(Camera cam)
    {
        if (cam == null || torches.Exists(t => t != null && t.transform.parent == cam.transform)) return;
        var torch = new GameObject("Linterna").AddComponent<Light>();
        torches.Add(torch);
        torch.transform.SetParent(cam.transform, false);
        torch.transform.localPosition = new Vector3(0.3f, -0.2f, 0f);
        torch.type = LightType.Spot;
        torch.spotAngle = 55f;
        torch.innerSpotAngle = 30f;
        torch.range = 22f;
        torch.color = new Color(1f, 0.95f, 0.85f);
        torch.shadows = LightShadows.None;
        torch.intensity = 0f;
    }

    public void Go(bool toNight)
    {
        EnsureTorch();
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Transition(toNight ? 1f : 0f));
        var gm = GameManager.Instance;
        if (gm != null) gm.ShowMessage(toNight ? "¡ANOCHECE! Llegan los abuelos senderistas: saltan todo y tiran chanclas" : "Amanece... la nevera de munición es tuya");
    }

    IEnumerator Transition(float target)
    {
        float start = night;
        float t = 0f;
        while (t < transitionTime)
        {
            t += Time.deltaTime;
            Apply(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / transitionTime)));
            yield return null;
        }
        Apply(target);
        routine = null;
    }

    void Apply(float k)
    {
        night = k;
        if (sun != null)
        {
            sun.color = Color.Lerp(daySunColor, nightSunColor, k);
            sun.intensity = Mathf.Lerp(daySunIntensity, nightSunIntensity, k);
        }
        RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbient, nightAmbient + glow, k);
        RenderSettings.reflectionIntensity = Mathf.Lerp(dayReflection, 0.2f, k);
        if (sky != null) sky.SetFloat("_Exposure", Mathf.Lerp(daySkyExposure, nightSkyExposure, k));
        RenderSettings.fog = dayFog || k > 0.01f;
        RenderSettings.fogMode = k > 0.01f ? FogMode.ExponentialSquared : dayFogMode;
        RenderSettings.fogColor = Color.Lerp(dayFog ? dayFogColor : nightFog, nightFog, k);
        RenderSettings.fogDensity = Mathf.Lerp(dayFog ? dayFogDensity : 0f, nightFogDensity, k);
        foreach (var torch in torches) if (torch != null) { torch.intensity = torchIntensity * k; torch.enabled = k > 0.01f; }
        if (faro != null) { faro.intensity = Mathf.Lerp(dayFaroIntensity, 6f, k); faro.range = Mathf.Lerp(dayFaroRange, 30f, k); }
        if (beam != null) { beam.intensity = 40f * k; beam.enabled = k > 0.01f; }
    }

    void Update()
    {
        if (beam != null && beam.enabled) beam.transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
    }

    void Reward()
    {
        if (!Net.Authority) return;   // online: the host drops it (and tells everyone)
        var gm = GameManager.Instance;
        var pm = FindAnyObjectByType<PowerUpManager>();
        PlayerHealth who = gm != null ? gm.Standing.FirstOrDefault() : null;   // co-op: next to a standing player (it refills everyone)
        if (who == null && gm != null) who = gm.Player;
        if (who == null || pm == null) return;
        Vector3 p = who.transform.position + who.transform.forward * 2f;
        if (NavAgents.Sample(p, 3f, out var s)) p = s;
        pm.Spawn(p + Vector3.up * 0.6f);
    }
}
