using System.Collections;
using UnityEngine;

// One-way slide between two zones (layout marker '!', Docs/FASE3_FORMATO.md): a player who walks into the top mouth
// is carried in an arc over the wall to the exit at the bottom. There is no way back (the exit is a closed hatch,
// the cell between them stays a solid wall). Old men use a one-way NavMeshLink built next to it.
// Only the local players are moved: online every player moves their own body, so nothing is sent over the network.
public class OneWaySlide : MonoBehaviour
{
    [SerializeField] Vector3 entry;          // world point in front of the top mouth (on the upper floor)
    [SerializeField] Vector3 exit;           // world point where the player lands (on the lower floor)
    [SerializeField] float radius = 0.75f;   // how close to `entry` (horizontally) starts the slide
    [SerializeField] float duration = 0.6f;

    public Vector3 Entry => entry;
    public Vector3 Exit => exit;
    public static event System.Action<PlayerHealth> Slid;   // tests

    readonly System.Collections.Generic.HashSet<PlayerHealth> sliding = new System.Collections.Generic.HashSet<PlayerHealth>();

    public void Configure(Vector3 entryPoint, Vector3 exitPoint)
    {
        entry = entryPoint;
        exit = exitPoint;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        foreach (var p in gm.Players)
        {
            if (p == null || sliding.Contains(p) || p.IsDead || PlayerSlot.IsProxy(p)) continue;
            Vector3 d = p.transform.position - entry;
            if (Mathf.Abs(d.y) > 1.2f) continue;
            d.y = 0f;
            if (d.magnitude <= radius) StartCoroutine(Carry(p));
        }
    }

    IEnumerator Carry(PlayerHealth p)
    {
        sliding.Add(p);
        var cc = p.GetComponent<CharacterController>();
        var pc = p.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        Vector3 a = p.transform.position;
        Vector3 flat = exit - a; flat.y = 0f;
        if (flat.sqrMagnitude > 0.001f) p.transform.rotation = Quaternion.LookRotation(flat);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            p.transform.position = Vector3.Lerp(a, exit, k) + Vector3.up * (1.6f * 4f * k * (1f - k));   // over the wall
            yield return null;
        }
        p.transform.position = exit + Vector3.up * 0.05f;
        if (cc != null) cc.enabled = true;
        if (pc != null) pc.enabled = true;
        sliding.Remove(p);
        Slid?.Invoke(p);
    }

    void OnDisable()
    {
        // never leave a player frozen if the slide goes away mid-carry
        foreach (var p in sliding)
        {
            if (p == null) continue;
            var cc = p.GetComponent<CharacterController>();
            var pc = p.GetComponent<PlayerController>();
            if (cc != null) cc.enabled = true;
            if (pc != null) pc.enabled = true;
        }
        sliding.Clear();
    }
}
