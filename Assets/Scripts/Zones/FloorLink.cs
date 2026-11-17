using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A one-way drop between real floors ([links] in a layout, Docs/PLANTAS_FORMATO.md): the laundry chute of the hotel,
// the slides of the cruise. A player who walks onto the mouth disappears and comes out at the exit after `travel`
// seconds, if the zone of the exit is open; otherwise the mouth is jammed ("Atascada"). Old men use a one-way
// NavMeshLink built next to it by the setup. Only local players are moved (online every player moves their own body).
public class FloorLink : MonoBehaviour
{
    [SerializeField] Vector3 entry;
    [SerializeField] Vector3 exit;
    [SerializeField] Zone exitZone;
    [SerializeField] string label = "Tolva";
    [SerializeField] float radius = 0.7f;
    [SerializeField] float travel = 1.2f;

    public Vector3 Entry => entry;
    public Vector3 Exit => exit;
    public bool Jammed => exitZone != null && !exitZone.IsOpen;
    public static event System.Action<PlayerHealth, FloorLink> Used;   // tests

    readonly HashSet<PlayerHealth> busy = new HashSet<PlayerHealth>();
    float nextJamMessage;

    public void Configure(Vector3 from, Vector3 to, Zone zoneOfExit, string name, float seconds)
    {
        entry = from;
        exit = to;
        exitZone = zoneOfExit;
        label = name;
        travel = seconds;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        foreach (var p in gm.Players)
        {
            if (p == null || busy.Contains(p) || p.IsDead || PlayerSlot.IsProxy(p)) continue;
            Vector3 d = p.transform.position - entry;
            if (Mathf.Abs(d.y) > 1.2f) continue;
            d.y = 0f;
            if (d.magnitude > radius) continue;
            if (Jammed)
            {
                if (Time.time > nextJamMessage) { gm.ShowMessageTo(p, label + ": atascada (abajo está cerrado)"); nextJamMessage = Time.time + 3f; }
                continue;
            }
            StartCoroutine(Carry(p));
        }
    }

    IEnumerator Carry(PlayerHealth p)
    {
        busy.Add(p);
        var cc = p.GetComponent<CharacterController>();
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        var renderers = p.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;
        p.transform.position = entry + Vector3.down * 0.5f;
        yield return new WaitForSeconds(travel);
        p.transform.position = exit + Vector3.up * 0.05f;
        foreach (var r in renderers) if (r != null) r.enabled = true;
        if (cc != null) cc.enabled = true;
        if (pc != null) pc.enabled = true;
        busy.Remove(p);
        Used?.Invoke(p, this);
    }

    void OnDisable()
    {
        foreach (var p in busy)
        {
            if (p == null) continue;
            foreach (var r in p.GetComponentsInChildren<Renderer>()) r.enabled = true;
            var cc = p.GetComponent<CharacterController>();
            var pc = p.GetComponent<PlayerController>();
            if (cc != null) cc.enabled = true;
            if (pc != null) pc.enabled = true;
        }
        busy.Clear();
    }
}
