using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Gondola stop (layout marker 'N', La Serenisima): pay a few conchas and the gondola takes you along the canals to
// another stop whose barrio is already open. An escape route: the old men stay behind, you
// can shoot from the boat. One passenger at a time per stop; it never lands in a closed barrio.
// The setup bakes the canal paths. Every player rides on their own machine (online: a client
// asks the host, who charges and tells that player's machine to do the ride).
// User feedback 1 (2026-11-18): it goes to a RANDOM open stop (never a closed barrio), each player rides once per
// round, and only some stops have a gondola each round (worked out from the round number on every machine, so
// no network message; at least one open stop always has one).
public class GondolaStop : Interactable
{
    [SerializeField] Zone zone;
    [SerializeField] Vector3 landing;            // floor point of this stop
    [SerializeField] Transform boat;             // the gondola moored here
    [SerializeField] GondolaStop[] next;         // the other stops, in ring order from this one
    [SerializeField] Vector3[] pathPoints;       // canal paths to next[i], flattened ...
    [SerializeField] int[] pathStart;            // ... path i = pathPoints[pathStart[i] .. pathStart[i + 1])
    [SerializeField] float speed = 11f;
    [SerializeField] float maxRide = 10f;        // seconds: long routes go faster

    bool busy;
    int planned = -1;                            // the random destination of the next ride (rolled once, kept until used)
    const float boatChance = 0.6f;
    public static bool TestAllBoats;             // tests: every stop has its gondola
    static readonly Dictionary<PlayerHealth, int> lastRide = new Dictionary<PlayerHealth, int>();   // player -> round
    static GondolaStop[] all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { lastRide.Clear(); all = null; TestAllBoats = false; }

    static int Round => RoundManager.Instance != null ? RoundManager.Instance.Round : 0;
    static GondolaStop[] Stops()
    {
        if (all == null || all.Any(s => s == null)) all = FindObjectsByType<GondolaStop>().OrderBy(s => s.name).ToArray();
        return all;
    }
    static float Roll(int round, int i) { uint h = (uint)(round * 73856093) ^ (uint)((i + 1) * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h % 10000) / 10000f; }

    // this round's gondolas: ~60 % of the stops, and at least one open stop always has one
    public bool HasBoat
    {
        get
        {
            if (TestAllBoats) return true;
            var stops = Stops();
            int me = System.Array.IndexOf(stops, this), round = Round;
            if (Roll(round, me) < boatChance) return true;
            GondolaStop best = null; float low = 2f;
            for (int i = 0; i < stops.Length; i++)
            {
                var s = stops[i];
                if (s.zone == null || !s.zone.IsOpen) continue;
                float h = Roll(round, i);
                if (h < boatChance) return false;
                if (h < low) { low = h; best = s; }
            }
            return best == this;
        }
    }

    public static bool RodeThisRound(PlayerHealth p) => p != null && lastRide.TryGetValue(p, out int r) && r == Round;
    float duck;                                  // how far the passenger ducks under a bridge
    const float passengerHeight = 1.7f;
    Vector3 boatHome;
    Quaternion boatHomeRot;

    public Zone Zone => zone;
    public Vector3 Landing => landing;
    public bool Busy => busy;
    public Transform Boat => boat;
    public Vector3[] PathPoints => pathPoints;   // setup check: the hull is swept along every route

    public void Configure(Zone z, Vector3 land, Transform gondola, GondolaStop[] others, Vector3[] points, int[] starts)
    {
        zone = z; landing = land; boat = gondola; next = others; pathPoints = points; pathStart = starts;
    }

    int Cost => Game != null && Game.Economy != null ? Game.Economy.gondolaCost : 250;

    static bool Valid(GondolaStop s) => s != null && s.zone != null && s.zone.IsOpen && !s.busy;

    // the random open stop of the next ride (only the host's roll matters: it sends the route to the rider)
    int Pick()
    {
        if (next == null) return -1;
        if (planned >= 0 && planned < next.Length && Valid(next[planned])) return planned;
        var ok = new List<int>();
        for (int i = 0; i < next.Length; i++) if (Valid(next[i])) ok.Add(i);
        planned = ok.Count == 0 ? -1 : ok[Random.Range(0, ok.Count)];
        return planned;
    }

    public bool CanRide => !busy && zone != null && zone.IsOpen && HasBoat && Pick() >= 0;

    void Update()
    {
        if (boat == null) return;
        bool show = busy || HasBoat;
        if (boat.gameObject.activeSelf != show) boat.gameObject.SetActive(show);
    }
    public Vector3 Destination => Pick() >= 0 ? next[Pick()].landing : landing;

    public override Vector3 InteractPoint => landing;

    public override string Prompt(PlayerInteractor player)
    {
        if (busy) return "";
        if (!HasBoat) return "Hoy no hay góndola en este embarcadero";
        if (RodeThisRound(player.GetComponent<PlayerHealth>())) return "Ya has cogido la góndola esta ronda";
        if (Pick() < 0) return "Góndola: no hay otro embarcadero abierto";
        return "□  Góndola a un embarcadero al azar (" + CostText(Cost) + ")";
    }

    public override bool CanInteract(PlayerInteractor player) => CanRide && !RodeThisRound(player.GetComponent<PlayerHealth>());

    public override void Interact(PlayerInteractor player)
    {
        int i = Pick();
        if (!Net.Authority || busy || i < 0 || !HasBoat) return;   // online client: PlayerInteractor asks the host
        var p = player.GetComponent<PlayerHealth>();
        if (p == null || RodeThisRound(p) || !Pay(Cost, player)) return;
        lastRide[p] = Round;
        planned = -1;
        // a remote player rides on their own machine (they move their own body): the host only charges
        if (PlayerSlot.IsProxy(player)) { if (NetGame.H != null) NetGame.H.Forward(player, NetGame.Fwd.Gondola, name, i); return; }
        StartCoroutine(Ride(p, i));
    }

    // free height over the passenger's feet at a point of the route (bridges, the arches of B4)
    static float Room(Vector3 boatPos)
    {
        const int mask = ~((1 << 8) | (1 << 9) | (1 << 4));   // not players, old men, water
        return Physics.Raycast(boatPos + Vector3.up * 0.4f, Vector3.up, out var hit, 4f, mask, QueryTriggerInteraction.Ignore)
            ? hit.point.y - (boatPos.y + 0.35f) - 0.1f : 99f;
    }

    // Online client: the host's go-ahead for the local player.
    public void StartRide(PlayerHealth p, int route)
    {
        if (p == null || busy || next == null || route < 0 || route >= next.Length) return;
        lastRide[p] = Round;
        StartCoroutine(Ride(p, route));
    }

    IEnumerator Ride(PlayerHealth p, int route)
    {
        busy = true;
        duck = 0f;
        var dest = next[route];
        dest.busy = true;
        var cc = p.GetComponent<CharacterController>();
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        if (boat != null) { boatHome = boat.localPosition; boatHomeRot = boat.localRotation; }

        int a = pathStart[route], b = route + 1 < pathStart.Length ? pathStart[route + 1] : pathPoints.Length;
        Vector3 pos = boat != null ? boat.position : p.transform.position;
        // since the arched bridges (B4) a route may go round half the map: never longer than maxRide seconds
        float length = 0f;
        for (int k = a + 1; k < b; k++) length += Vector3.Distance(pathPoints[k - 1], pathPoints[k]);
        float rideSpeed = Mathf.Max(speed, length / maxRide);
        for (int k = a; k < b; k++)
        {
            while (true)
            {
                Vector3 target = pathPoints[k];
                target.y = pos.y;                     // the water level of the moment (acqua alta)
                Vector3 d = target - pos;
                float stepLen = rideSpeed * Time.deltaTime;
                if (d.magnitude <= stepLen) { pos = target; break; }
                pos += d.normalized * stepLen;
                if (boat != null)
                {
                    boat.position = pos;
                    boat.rotation = Quaternion.Slerp(boat.rotation, Quaternion.LookRotation(-d.normalized), 6f * Time.deltaTime);   // bow is -Z
                }
                if (p != null)
                {
                    // under an arched bridge (the acqua alta lifts the boat) the passenger ducks into the hull
                    float room = Mathf.Min(Room(pos), Room(pos + d.normalized * 1.5f));   // and a little ahead: it ducks in time
                    float want = Mathf.Clamp(passengerHeight - room, 0f, 1.2f);
                    duck = Mathf.MoveTowards(duck, want, 6f * Time.deltaTime);
                    p.transform.position = pos + Vector3.up * (0.35f - duck);
                }
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
