using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A lift between real floors (Docs/PLANTAS_FORMATO.md, section 5). One cab per floor it stops at, all in the same
// x, z: a ride closes the doors, waits, and moves everyone standing in the cab to the same spot of the target floor
// (the cabs look the same, so it reads as a ride). Two buttons inside (LiftButton): up / down to the next stop whose
// landing zone is open. It can need the power (hotel) or the pulsera de puertas (the service lift).
// Old men never ride it: they take the stairs; each stop is also a spawn entrance on maps that want it.
// Only the local players are moved (online every player moves their own body, like the slides).
public class Lift : MonoBehaviour
{
    [SerializeField] string liftName = "Ascensor";
    [SerializeField] Transform[] stops;          // cab floor centres, bottom to top
    [SerializeField] Zone[] stopZones;           // zone of each landing (null = always open)
    [SerializeField] string[] stopNames;         // floor names for the prompts
    [SerializeField] bool needsPower = true;     // only works once someone gives the power (maps with a power switch)
    [SerializeField] bool needsBracelet;         // the service lift: only for whoever wears the pulsera de puertas
    [SerializeField] float doorTime = 1f;
    [SerializeField] float travelPerFloor = 2.5f;

    public static readonly List<Lift> All = new List<Lift>();
    public static event System.Action<Lift, int, int> Rode;   // tests: lift, from stop, to stop

    public string LiftName => liftName;
    public int StopCount => stops != null ? stops.Length : 0;
    public Transform Stop(int i) => stops[i];
    public bool Busy { get; private set; }

    public void Configure(string name, Transform[] stopPoints, Zone[] zones, string[] names, bool power, bool bracelet)
    {
        liftName = name;
        stops = stopPoints;
        stopZones = zones;
        stopNames = names;
        needsPower = power;
        needsBracelet = bracelet;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public int StopOf(Vector3 p)
    {
        int best = 0;
        for (int i = 1; i < stops.Length; i++)
            if ((stops[i].position - p).sqrMagnitude < (stops[best].position - p).sqrMagnitude) best = i;
        return best;
    }

    // The next stop up (dir 1) or down (-1) whose landing zone is open, or -1.
    public int NextStop(int from, int dir)
    {
        for (int i = from + dir; i >= 0 && i < stops.Length; i += dir)
            if (stopZones == null || i >= stopZones.Length || stopZones[i] == null || stopZones[i].IsOpen) return i;
        return -1;
    }

    public string StopName(int i) => stopNames != null && i < stopNames.Length ? stopNames[i] : "planta " + i;

    // Why it can't be used now, or null.
    public string Blocked(PlayerInteractor who)
    {
        var bm = BoatMission.Instance;
        if (needsPower && bm != null && bm.HasSwitch && !bm.Power) return "No hay luz";
        if (needsBracelet && !ServiceDoor.HasDoorBracelet(who)) return "Solo personal: hace falta la pulsera de puertas";
        return null;
    }

    public void Ride(int from, int to)
    {
        if (Busy || to < 0 || to >= stops.Length || from == to) return;
        StartCoroutine(RideRoutine(from, to));
    }

    IEnumerator RideRoutine(int from, int to)
    {
        Busy = true;
        var riders = new List<PlayerHealth>();
        var gm = GameManager.Instance;
        if (gm != null)
            foreach (var p in gm.Players)
            {
                if (p == null || p.IsDead || PlayerSlot.IsProxy(p)) continue;
                Vector3 d = p.transform.position - stops[from].position;
                if (Mathf.Abs(d.x) < 1.05f && Mathf.Abs(d.z) < 1.05f && Mathf.Abs(d.y) < 1.6f) riders.Add(p);
            }
        foreach (var p in riders) Freeze(p, true);
        yield return new WaitForSeconds(doorTime);
        yield return new WaitForSeconds(travelPerFloor * Mathf.Abs(to - from));
        Vector3 delta = stops[to].position - stops[from].position;
        foreach (var p in riders)
        {
            if (p == null) continue;
            p.transform.position += delta;
            Freeze(p, false);
        }
        Busy = false;
        Rode?.Invoke(this, from, to);
        if (gm != null && riders.Count > 0) gm.ShowMessageTo(riders[0], StopName(to));
    }

    static void Freeze(PlayerHealth p, bool on)
    {
        var cc = p.GetComponent<CharacterController>();
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = !on;
        if (cc != null) cc.enabled = !on;
    }
}
