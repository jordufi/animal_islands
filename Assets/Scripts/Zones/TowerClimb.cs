using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The campanile of La Serenisima: walk into the door at its foot and you are taken up the stairs to the belfry,
// where the bell is (a PowerSwitch). The way down is to jump off any side (there is no fall damage in this game).
// Old men can't come up, so nobody stays: after `maxStay` seconds the bell ringer throws you off.
// Only the local players are moved (online every player moves their own body: no network message).
public class TowerClimb : MonoBehaviour
{
    [SerializeField] Vector3 entry;          // floor point in front of the door
    [SerializeField] Vector3 top;            // where you arrive, on the belfry floor
    [SerializeField] float radius = 0.8f;
    [SerializeField] float climbTime = 1.6f;
    [SerializeField] float maxStay = 20f;
    [SerializeField] float topRadius = 4.5f;

    public Vector3 Entry => entry;
    public Vector3 Top => top;
    public float MaxStay { get => maxStay; set => maxStay = value; }
    public static event System.Action<PlayerHealth> Climbed;     // tests
    public static event System.Action<PlayerHealth> ThrownOff;

    readonly HashSet<PlayerHealth> moving = new HashSet<PlayerHealth>();
    readonly Dictionary<PlayerHealth, float> upSince = new Dictionary<PlayerHealth, float>();

    public void Configure(Vector3 entryPoint, Vector3 topPoint)
    {
        entry = entryPoint; top = topPoint;
    }

    bool OnTop(Vector3 p)
    {
        Vector3 d = p - top;
        return Mathf.Abs(d.y) < 2.5f && new Vector2(d.x, d.z).magnitude < topRadius;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        foreach (var p in gm.Players)
        {
            if (p == null || moving.Contains(p) || p.IsDead || PlayerSlot.IsProxy(p)) continue;
            Vector3 pos = p.transform.position;
            if (OnTop(pos))
            {
                if (!upSince.ContainsKey(p)) upSince[p] = Time.time;
                else if (Time.time - upSince[p] > maxStay)
                {
                    // off you go, away from the centre, towards where you are looking
                    Vector3 dir = p.transform.forward; dir.y = 0f;
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                    StartCoroutine(Carry(p, top + dir.normalized * (topRadius + 1.5f) + Vector3.up * 0.5f, 0.6f, false));
                    gm.ShowMessageTo(p, "El campanero no quiere visitas: ¡abajo!");
                }
                continue;
            }
            upSince.Remove(p);
            Vector3 d = pos - entry;
            if (Mathf.Abs(d.y) > 1.2f) continue;
            d.y = 0f;
            if (d.magnitude <= radius) StartCoroutine(Carry(p, top + Vector3.up * 0.1f, climbTime, true));
        }
    }

    IEnumerator Carry(PlayerHealth p, Vector3 target, float time, bool up)
    {
        moving.Add(p);
        var cc = p.GetComponent<CharacterController>();
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        Vector3 a = p.transform.position;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            // going up: straight up the outside of the shaft first, then in over the parapet
            Vector3 pos = up ? Vector3.Lerp(new Vector3(a.x, Mathf.Lerp(a.y, target.y + 0.6f, Mathf.Clamp01(k / 0.75f)), a.z),
                                            target + Vector3.up * 0.6f, Mathf.Clamp01((k - 0.75f) / 0.25f))
                             : Vector3.Lerp(a, target, k) + Vector3.up * (1.2f * 4f * k * (1f - k));
            p.transform.position = pos;
            yield return null;
        }
        p.transform.position = target;
        if (cc != null) cc.enabled = true;
        if (pc != null) pc.enabled = true;
        moving.Remove(p);
        if (up) { upSince[p] = Time.time; Climbed?.Invoke(p); }
        else { upSince.Remove(p); ThrownOff?.Invoke(p); }
    }

    void OnDisable()
    {
        foreach (var p in moving)
        {
            if (p == null) continue;
            var cc = p.GetComponent<CharacterController>();
            var pc = p.GetComponent<PlayerController>();
            if (cc != null) cc.enabled = true;
            if (pc != null) pc.enabled = true;
        }
        moving.Clear();
    }
}
