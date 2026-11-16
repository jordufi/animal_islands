using System.Collections;
using UnityEngine;

// Villajubilada: the fiesta lights of one zone (garlands of coloured bulbs across the streets, the bulbs of the fair
// rides, a few real lights). Dark until the zone's fuse box is switched on (BoatMission.ZoneLit); then they come on
// one string after another, starting at the fuse box, like someone flicking the fuses.
// The bulbs are material slots named VJ_Bulb_R / _Y / _G / _B on the models; switched on = the same slot with the
// glowing material (`onMaterials`, made by the setup so the emission shader variant is in the build).
public class FiestaLights : MonoBehaviour
{
    [SerializeField] Zone zone;
    [SerializeField] Renderer[] bulbs;          // sorted by distance to the fuse box
    [SerializeField] Light[] lights;
    [SerializeField] string[] slots;            // "VJ_Bulb_R", ...
    [SerializeField] Material[] onMaterials;    // same order as slots
    [SerializeField] Renderer[] windows;        // facade strips whose glass lights up (someone is home)
    [SerializeField] Material windowOn;
    [SerializeField] string windowSlot = "VN_Glass";
    [SerializeField] float stepSeconds = 0.06f;
    [SerializeField] float lightIntensity = 9f;
    [SerializeField] float glow = 0.13f;        // ambient light this zone gives the whole village once it is on

    public Zone Zone => zone;
    public bool On { get; private set; }
    public int BulbsOn { get; private set; }
    public int BulbCount => bulbs != null ? bulbs.Length : 0;

    public void Configure(Zone z, Renderer[] renderers, Light[] realLights, string[] slotNames, Material[] glowing)
    {
        zone = z; bulbs = renderers; lights = realLights; slots = slotNames; onMaterials = glowing;
    }

    public void SetWindows(Renderer[] strips, Material glowing) { windows = strips; windowOn = glowing; }
    public int WindowCount => windows != null ? windows.Length : 0;

    void Start()
    {
        if (lights != null) foreach (var l in lights) if (l != null) { l.intensity = 0f; l.enabled = false; }
    }

    void Update()
    {
        if (On || BoatMission.Instance == null || zone == null || !BoatMission.Instance.ZoneLit(zone)) return;
        On = true;
        var night = FindAnyObjectByType<DayNightCycle>();
        if (night != null && glow > 0f) night.AddGlow(glow);
        StartCoroutine(TurnOn());
    }

    IEnumerator TurnOn()
    {
        if (bulbs != null)
            foreach (var r in bulbs)
            {
                if (r == null) continue;
                var ms = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    for (int k = 0; k < slots.Length; k++)
                        if (ms[i].name.StartsWith(slots[k]) && onMaterials[k] != null) { ms[i] = onMaterials[k]; changed = true; break; }
                }
                if (changed) r.sharedMaterials = ms;
                BulbsOn++;
                yield return new WaitForSeconds(stepSeconds);
            }
        if (windows != null && windowOn != null)
        {
            int n = 0;
            foreach (var r in windows)
            {
                if (r == null) continue;
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name.StartsWith(windowSlot)) ms[i] = windowOn;
                r.sharedMaterials = ms;
                if (++n % 4 == 0) yield return null;
            }
        }
        if (lights == null) yield break;
        foreach (var l in lights) if (l != null) l.enabled = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
        {
            foreach (var l in lights) if (l != null) l.intensity = lightIntensity * t;
            yield return null;
        }
        foreach (var l in lights) if (l != null) l.intensity = lightIntensity;
    }
}
