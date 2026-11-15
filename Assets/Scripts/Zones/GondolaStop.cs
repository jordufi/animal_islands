using System.Collections;
using UnityEngine;

// Gondola stop (layout marker 'N', La Serenisima): pay a few conchas and the gondola takes you along the canals to
// the next stop (going round the map) whose barrio is already open. An escape route: the old men stay behind, you
// can shoot from the boat. One passenger at a time per stop; it never lands in a closed barrio.
// The setup bakes the canal paths. Every player rides on their own machine (online: a client
// asks the host, who charges and tells that player's machine to do the ride).
public class GondolaStop : Interactable
{
    [SerializeField] Zone zone;
    [SerializeField] Vector3 landing;            // floor point of this stop
    [SerializeField] Transform boat;             // the gondola moored here
    [SerializeField] GondolaStop[] next;         // the other stops, in ring order from this one
    [SerializeField] Vector3[] pathPoints;       // canal paths to next[i], flattened ...
    [SerializeField] int[] pathStart;            // ... path i = pathPoints[pathStart[i] .. pathStart[i + 1])
    [SerializeField] float speed = 11f;

    bool busy;
    Vector3 boatHome;
    Quaternion boatHomeRot;

    public Zone Zone => zone;
    public Vector3 Landing => landing;
    public bool Busy => busy;

    public void Configure(Zone z, Vector3 land, Transform gondola, GondolaStop[] others, Vector3[] points, int[] starts)
    {
        zone = z; landing = land; boat = gondola; next = others; pathPoints = points; pathStart = starts;
    }

    int Cost => Game != null && Game.Economy != null ? Game.Economy.gondolaCost : 250;

    int Pick()
    {
        if (next == null) return -1;
        for (int i = 0; i < next.Length; i++)
            if (next[i] != null && next[i].zone != null && next[i].zone.IsOpen && !next[i].busy) return i;
        return -1;
    }

    public bool CanRide => !busy && zone != null && zone.IsOpen && Pick() >= 0;
    public Vector3 Destination => Pick() >= 0 ? next[Pick()].landing : landing;

    public override Vector3 InteractPoint => landing;

    public override string Prompt(PlayerInteractor player)
    {
        if (busy) return "";
        int i = Pick();
        if (i < 0) return "Góndola: no hay otro embarcadero abierto";
        return "□  Góndola a " + next[i].zone.displayName + " (" + CostText(Cost) + ")";
    }

    public override bool CanInteract(PlayerInteractor player) => !busy && Pick() >= 0;

    public override void Interact(PlayerInteractor player)
    {
        int i = Pick();
        if (!Net.Authority || busy || i < 0) return;   // online client: PlayerInteractor asks the host
        var p = player.GetComponent<PlayerHealth>();
        if (p == null || !Pay(Cost, player)) return;
        // a remote player rides on their own machine (they move their own body): the host only charges
        if (PlayerSlot.IsProxy(player)) { if (NetGame.H != null) NetGame.H.Forward(player, NetGame.Fwd.Gondola, name, i); return; }
        StartCoroutine(Ride(p, i));
    }

    // Online client: the host's go-ahead for the local player.
    public void StartRide(PlayerHealth p, int route)
    {
        if (p == null || busy || next == null || route < 0 || route >= next.Length) return;
        StartCoroutine(Ride(p, route));
    }

    IEnumerator Ride(PlayerHealth p, int route)
    {
        busy = true;
        var dest = next[route];
        dest.busy = true;
        var cc = p.GetComponent<CharacterController>();
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        if (boat != null) { boatHome = boat.localPosition; boatHomeRot = boat.localRotation; }

        int a = pathStart[route], b = route + 1 < pathStart.Length ? pathStart[route + 1] : pathPoints.Length;
        Vector3 pos = boat != null ? boat.position : p.transform.position;
        for (int k = a; k < b; k++)
        {
            while (true)
            {
                Vector3 target = pathPoints[k];
                target.y = pos.y;                     // the water level of the moment (acqua alta)
                Vector3 d = target - pos;
                float stepLen = speed * Time.deltaTime;
                if (d.magnitude <= stepLen) { pos = target; break; }
                pos += d.normalized * stepLen;
                if (boat != null)
                {
                    boat.position = pos;
                    boat.rotation = Quaternion.Slerp(boat.rotation, Quaternion.LookRotation(-d.normalized), 6f * Time.deltaTime);   // bow is -Z
                }
                if (p != null) p.transform.position = pos + Vector3.up * 0.35f;
                yield return null;
            }
        }
        if (p != null)
        {
            p.transform.position = dest.landing + Vector3.up * 0.3f;
            if (cc != null) cc.enabled = true;
            if (pc != null) pc.enabled = true;
        }
        if (boat != null) { boat.localPosition = boatHome; boat.localRotation = boatHomeRot; }   // back at its mooring
        dest.busy = false;
        busy = false;
    }
}
