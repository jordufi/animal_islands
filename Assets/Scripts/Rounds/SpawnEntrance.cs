using System.Collections;
using UnityEngine;

// A visible, predictable place where old men come out: a WC cabin / shed in a wall, a crack in the rock,
// a pool ladder out of the sea, or a prop (bus, tent, stall, container). The old man appears hidden
// inside, the door shakes and opens, he walks out and climbs over the barrier (about one second
// visible) and only then joins the NavMesh at Exit. Sits on a spawn point of its zone
// (Zone/SpawnPoints/<this>); children: Inside, Barrier, Exit, optional Door (hinge).
// "Anselmo cagón" mode: old men can also go IN (Enter) and come out of another entrance. Only one old man
// at a time uses an entrance (coming out or going in): Busy until he is through.
public class SpawnEntrance : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<SpawnEntrance> All = new System.Collections.Generic.List<SpawnEntrance>();
    public static int Overlaps { get; set; }   // Use / Enter while already busy (two old men at once): tests expect 0

    public enum Kind { Cabina, Grieta, Escalerilla, Objeto }

    [SerializeField] Kind kind = Kind.Cabina;
    [SerializeField] Transform inside;
    [SerializeField] Transform barrier;
    [SerializeField] Transform exit;
    [SerializeField] Transform door;            // hinge, rotated around Y to open
    [SerializeField] float barrierHeight = 0.6f;

    [Header("Timing (seconds)")]
    [SerializeField] float shakeTime = 0.3f;     // door rattles, the old man is still hidden
    [SerializeField] float openTime = 0.2f;
    [SerializeField] float walkTime = 0.45f;     // inside -> barrier
    [SerializeField] float climbTime = 0.55f;    // over the barrier -> exit (lands on the NavMesh)
    [SerializeField] float reuseDelay = 0.6f;    // extra time before the next old man can use it

    float busyUntil;
    Quaternion doorClosed;
    Coroutine doorRoutine;

    public Kind EntranceKind => kind;
    public Vector3 InsidePoint => inside != null ? inside.position : transform.position;
    public Vector3 BarrierPoint => barrier != null ? barrier.position : transform.position;
    public Vector3 ExitPoint => exit != null ? exit.position : transform.position;
    public Transform Exit => exit;
    public float BarrierHeight => barrierHeight;
    public float ShakeTime => shakeTime + (door != null ? openTime : 0f);
    public float WalkTime => walkTime;
    public float ClimbTime => climbTime;
    public float TotalTime => ShakeTime + walkTime + climbTime;
    public bool Busy => Time.time < busyUntil;
    public float LastUsed { get; private set; } = -100f;

    Zone zone;
    bool zoneCached;
    public Zone Zone
    {
        get
        {
            if (!zoneCached) { zone = GetComponentInParent<Zone>(); zoneCached = true; }
            return zone;
        }
    }
    public bool InOpenZone => Zone == null || Zone.IsOpen;
    public float LastEntered { get; private set; } = -100f;
    public int TimesEntered { get; private set; }

    void Awake()
    {
        if (door != null) doorClosed = door.localRotation;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // An old man goes in (Anselmo cagón): reserves the entrance for `seconds` and opens / closes the door.
    public void Enter(float seconds)
    {
        if (Busy && Net.Authority) Overlaps++;
        LastEntered = Time.time;
        TimesEntered++;
        busyUntil = Time.time + seconds + reuseDelay;
        if (NetGame.H != null) NetGame.H.EntranceUsed(this, true, seconds);
        if (door != null)
        {
            if (doorRoutine != null) StopCoroutine(doorRoutine);
            doorRoutine = StartCoroutine(EnterDoorSequence(seconds));
        }
    }

    IEnumerator EnterDoorSequence(float seconds)
    {
        float t = 0f;
        while (t < openTime)
        {
            t += Time.deltaTime;
            door.localRotation = doorClosed * Quaternion.Euler(0f, -105f * Mathf.SmoothStep(0f, 1f, t / openTime), 0f);
            yield return null;
        }
        yield return new WaitForSeconds(Mathf.Max(0f, seconds - openTime));
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            door.localRotation = doorClosed * Quaternion.Euler(0f, -105f * (1f - t / 0.3f), 0f);
            yield return null;
        }
        door.localRotation = doorClosed;
        doorRoutine = null;
    }

    // Reserves the entrance for one old man and plays the door.
    public void Use()
    {
        if (Busy && Net.Authority) Overlaps++;
        LastUsed = Time.time;
        busyUntil = Time.time + TotalTime + reuseDelay;
        if (NetGame.H != null) NetGame.H.EntranceUsed(this, false, 0f);
        if (door != null)
        {
            if (doorRoutine != null) StopCoroutine(doorRoutine);
            doorRoutine = StartCoroutine(DoorSequence());
        }
    }

    public void Release() => busyUntil = Mathf.Min(busyUntil, Time.time + reuseDelay);

    IEnumerator DoorSequence()
    {
        float t = 0f;
        while (t < shakeTime)   // rattle
        {
            t += Time.deltaTime;
            door.localRotation = doorClosed * Quaternion.Euler(0f, Mathf.Sin(t * 70f) * 4f, 0f);
            yield return null;
        }
        t = 0f;
        while (t < openTime)
        {
            t += Time.deltaTime;
            door.localRotation = doorClosed * Quaternion.Euler(0f, -105f * Mathf.SmoothStep(0f, 1f, t / openTime), 0f);
            yield return null;
        }
        yield return new WaitForSeconds(walkTime + climbTime + 0.4f);
        t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            door.localRotation = doorClosed * Quaternion.Euler(0f, -105f * (1f - t / 0.35f), 0f);
            yield return null;
        }
        door.localRotation = doorClosed;
        doorRoutine = null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(InsidePoint, 0.3f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(BarrierPoint, 0.3f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ExitPoint, 0.3f);
    }
}
