using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// "Donde subes tú, sube el abuelo" (Docs/FEEDBACK1_PLAN.md, phase 2; design rule: the old men must always be able to
// follow the player, except the gondola and the campanile). Works on the open scene after the NavMesh bake:
//   1. samples every standable surface on a 0.3 m grid (ground, prop tops, wall tops...), with room for the player;
//   2. floods from the surfaces on the old men's NavMesh with what a player can do with the normal jump (walk, step,
//      jump 1.15 m up onto something up to 1.2 m away, drop down) — never the Frog's super jump;
//   3. a reached surface is "out of reach" when no NavMesh point an old man can walk to is within his hit reach:
//      EnemyAI swings when the flat distance is <= 1.3 m and the height difference < 1.5 m.
// Clusters of those surfaces are the spots to fix: a ramp that the NavMesh follows, or a shape you cannot stand on.
public static class ReachCheck
{
    public const float Step = 0.3f;        // grid
    public const float StepUp = 0.3f;      // CharacterController step offset
    public const float JumpUp = 1.45f;     // PlayerController jumpHeight 1.2 + the CharacterController step offset 0.3 (catches an edge), minus a margin
    public const float JumpReach = 1.2f;   // flat distance of a jump onto something
    public const float HitHeight = 1.5f;   // EnemyAI: height difference below this
    public const float HitReach = 1.3f;    // EnemyAI attackRange (flat)
    // Old men climb onto props after a player who stands on one (EnemyAI climbing, like the night senderistas): a prop
    // top up to this height over the "Saltador" NavMesh under it counts as somewhere they follow you.
    public const float ClimbMax = 3.2f;
    const float Radius = 0.3f, Height = 1.6f;
    const int MinCluster = 3;              // ignore specks (post tops, a corner of a roof edge)

    public struct Spot { public Vector3 pos; public int cells; public float above; public string under; public Vector3 from; public string fromUnder; }
    public static readonly List<Spot> LastSpots = new List<Spot>();

    struct Node { public int ix, iz; public float y; public bool seed, reached, climb; public int parent; }

    static int Mask => ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 8) | (1 << 9));   // surfaces: no IgnoreRaycast / Water / UI / Player / Enemy
    static int Block => Mask | (1 << 4);   // what stops a body: also the water blockers (layer Water blocks walking)

    // Runs on the open scene; returns report lines ("OK ..." or "WARN ...").
    public static List<string> Run(string label = "")
    {
        LastSpots.Clear();
        var lines = new List<string>();
        Physics.SyncTransforms();
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0) { lines.Add("WARN reach check: no NavMesh in the scene"); return lines; }
        var b = new Bounds(tri.vertices[0], Vector3.zero);
        foreach (var v in tri.vertices) b.Encapsulate(v);
        b.Expand(new Vector3(6f, 0f, 6f));
        float top = b.max.y + 12f;

        int nx = Mathf.CeilToInt(b.size.x / Step), nz = Mathf.CeilToInt(b.size.z / Step);
        var nodes = new List<Node>();
        var columns = new Dictionary<long, List<int>>();
        long Key(int i, int k) => ((long)i << 32) | (uint)k;
        Vector3 Pos(Node n) => new Vector3(b.min.x + (n.ix + 0.5f) * Step, n.y, b.min.z + (n.iz + 0.5f) * Step);

        // 1. standable surfaces
        var hits = new RaycastHit[32];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                var o = new Vector3(b.min.x + (i + 0.5f) * Step, top, b.min.z + (k + 0.5f) * Step);
                int h = Physics.RaycastNonAlloc(o, Vector3.down, hits, top - b.min.y + 20f, Mask, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < h; j++)
                {
                    var hit = hits[j];
                    if (hit.normal.y < 0.7f) continue;
                    var p = hit.point;
                    if (Physics.CheckCapsule(p + Vector3.up * (Radius + 0.08f), p + Vector3.up * (Height - Radius), Radius * 0.8f, Block, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, 4f, 1 << 4, QueryTriggerInteraction.Collide)) continue;   // under water
                    if (hit.collider.transform.parent != null && hit.collider.transform.parent.name == "SueloNado") continue;   // La Serenisima canal bed (acqua alta only)
                    if (!columns.TryGetValue(Key(i, k), out var list)) columns[Key(i, k)] = list = new List<int>();
                    if (list.Any(x => Mathf.Abs(nodes[x].y - p.y) < 0.15f)) continue;
                    list.Add(nodes.Count);
                    nodes.Add(new Node { ix = i, iz = k, y = p.y, parent = -1, climb = hit.collider.gameObject.layer == NavAgents.PropLayer });
                }
            }

        // 2. seeds = surfaces on the NavMesh part old men walk on (connected to a spawn point / box spot of a zone)
        var refs = new List<Vector3>();
        var zoneRefs = new List<List<Vector3>>();
        // (the spawn points themselves sit inside the entrance cabins: their exit is where an old man joins the NavMesh)
        foreach (var z in Object.FindObjectsByType<Zone>())
        {
            var mine = new List<Vector3>();
            foreach (var t in z.BoxSpots) if (NavAgents.Sample(t.position, 1f, out var s)) mine.Add(s);
            foreach (var t in z.SpawnPoints)
            {
                var e = t.GetComponent<SpawnEntrance>();
                if (NavAgents.Sample(e != null ? e.ExitPoint : t.position, e != null ? 1f : 0.6f, out var s)) mine.Add(s);
            }
            if (mine.Count > 0) { zoneRefs.Add(mine); refs.AddRange(mine); }
        }
        var filter = NavAgents.Filter(0);
        var cache = new Dictionary<Vector3Int, bool>();
        var path = new NavMeshPath();
        bool Connected(Vector3 q)
        {
            var key = new Vector3Int(Mathf.RoundToInt(q.x / 2f), Mathf.RoundToInt(q.y), Mathf.RoundToInt(q.z / 2f));
            if (cache.TryGetValue(key, out var c)) return c;
            c = false;
            bool To(Vector3 r) => NavMesh.CalculatePath(q, r, filter, path) && path.status == NavMeshPathStatus.PathComplete;
            // the nearest few first; then the nearest of every zone (a bridge beyond a door belongs to the far side)
            c = refs.OrderBy(r => (Flat(r) - Flat(q)).sqrMagnitude).Take(4).Any(To)
                || zoneRefs.Select(z => z.OrderBy(r => (Flat(r) - Flat(q)).sqrMagnitude).First()).OrderBy(r => (Flat(r) - Flat(q)).sqrMagnitude).Any(To);
            return cache[key] = c;
        }
        for (int n = 0; n < nodes.Count; n++)
        {
            var nd = nodes[n];
            var p = Pos(nd);
            if (NavMesh.SamplePosition(p, out var hit, 0.5f, filter) && Mathf.Abs(hit.position.y - p.y) < 0.35f && (Flat(hit.position) - Flat(p)).magnitude < 0.45f && Connected(hit.position))
            {
                nd.seed = nd.reached = true;
                nodes[n] = nd;
            }
        }
        // prop tops old men climb onto: the "Saltador" NavMesh (props ignored) runs under them, not too high
        int jumper = NavAgents.JumperId;
        var jfilter = NavAgents.Filter(jumper);
        for (int n = 0; n < nodes.Count; n++)
        {
            var nd = nodes[n];
            if (!nd.climb || nd.seed) continue;
            var p = Pos(nd);
            nd.climb = jumper != 0 && NavMesh.SamplePosition(p + Vector3.down * 1.5f, out var jh, 2.5f, jfilter)
                && (Flat(jh.position) - Flat(p)).magnitude < 0.6f && p.y - jh.position.y <= ClimbMax && p.y - jh.position.y > -0.2f;
            nodes[n] = nd;
        }

        // 3. what the player reaches from there
        int reachCells = Mathf.CeilToInt(JumpReach / Step);
        var queue = new Queue<int>();
        for (int n = 0; n < nodes.Count; n++) if (nodes[n].seed) queue.Enqueue(n);
        while (queue.Count > 0)
        {
            int a = queue.Dequeue();
            var na = nodes[a];
            var pa = Pos(na);
            for (int di = -reachCells; di <= reachCells; di++)
                for (int dk = -reachCells; dk <= reachCells; dk++)
                {
                    if (!columns.TryGetValue(Key(na.ix + di, na.iz + dk), out var list)) continue;
                    float flat = Mathf.Sqrt(di * di + dk * dk) * Step;
                    if (flat > JumpReach + 0.01f) continue;
                    foreach (int m in list)
                    {
                        if (nodes[m].reached) continue;
                        var pm = Pos(nodes[m]);
                        float dy = pm.y - pa.y;
                        bool walk = flat <= Step * 1.5f && dy <= StepUp;
                        if (!walk && dy > JumpUp) continue;
                        // nothing in the way: up from a (jump), then across at the higher of the two
                        float lift = Mathf.Max(0f, dy) + 0.5f;
                        if (!walk && Physics.Linecast(pa + Vector3.up * 0.5f, pa + Vector3.up * (lift + 0.3f), Block, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Linecast(pa + Vector3.up * (walk ? 0.5f : lift), pm + Vector3.up * 0.5f, Block, QueryTriggerInteraction.Ignore)) continue;
                        var nm = nodes[m]; nm.reached = true; nm.parent = a; nodes[m] = nm;
                        queue.Enqueue(m);
                    }
                }
        }

        // 4. reached surfaces no old man can hit
        int hitCells = Mathf.CeilToInt(HitReach / Step);
        var outOfReach = new List<int>();
        for (int n = 0; n < nodes.Count; n++)
        {
            var nd = nodes[n];
            if (!nd.reached || nd.seed || nd.climb) continue;
            bool hittable = false;
            for (int di = -hitCells; di <= hitCells && !hittable; di++)
                for (int dk = -hitCells; dk <= hitCells && !hittable; dk++)
                {
                    if (Mathf.Sqrt(di * di + dk * dk) * Step > HitReach + 0.01f) continue;
                    if (!columns.TryGetValue(Key(nd.ix + di, nd.iz + dk), out var list)) continue;
                    foreach (int m in list) if ((nodes[m].seed || nodes[m].climb) && Mathf.Abs(nodes[m].y - nd.y) < HitHeight) { hittable = true; break; }
                }
            if (!hittable) outOfReach.Add(n);
        }

        // 5. clusters
        var set = new HashSet<int>(outOfReach);
        var done = new HashSet<int>();
        foreach (int s0 in outOfReach)
        {
            if (done.Contains(s0)) continue;
            var cl = new List<int>();
            var q = new Queue<int>();
            q.Enqueue(s0); done.Add(s0);
            while (q.Count > 0)
            {
                int a = q.Dequeue(); cl.Add(a);
                var na = nodes[a];
                for (int di = -1; di <= 1; di++)
                    for (int dk = -1; dk <= 1; dk++)
                        if (columns.TryGetValue(Key(na.ix + di, na.iz + dk), out var list))
                            foreach (int m in list)
                                if (set.Contains(m) && !done.Contains(m) && Mathf.Abs(nodes[m].y - na.y) < 0.6f) { done.Add(m); q.Enqueue(m); }
            }
            if (cl.Count < MinCluster) continue;
            var c = cl.Select(x => Pos(nodes[x])).Aggregate(Vector3.zero, (acc, v) => acc + v) / cl.Count;
            var at = Pos(nodes[cl.OrderBy(x => (Pos(nodes[x]) - c).sqrMagnitude).First()]);
            string under = Physics.Raycast(at + Vector3.up * 0.2f, Vector3.down, out var uh, 0.6f, Mask, QueryTriggerInteraction.Ignore) ? PathOf(uh.collider.transform) : "?";
            // height over the old men's floor around it
            float floor = float.MaxValue;
            for (int r = 0; r < 12 && floor == float.MaxValue; r++)
                if (NavMesh.SamplePosition(at + Vector3.down * 2f, out var nh, 1.5f + r, filter)) floor = nh.position.y;
            // where the player came from: back along the flood to the last surface on the old men's NavMesh
            int back = cl[0];
            while (back >= 0 && !nodes[back].seed) back = nodes[back].parent;
            var from = back >= 0 ? Pos(nodes[back]) : at;
            int jumpOff = cl[0];
            while (jumpOff >= 0 && nodes[jumpOff].parent >= 0 && !nodes[nodes[jumpOff].parent].seed) jumpOff = nodes[jumpOff].parent;
            string fromUnder = jumpOff >= 0 && Physics.Raycast(Pos(nodes[jumpOff]) + Vector3.up * 0.2f, Vector3.down, out var fh, 0.6f, Mask, QueryTriggerInteraction.Ignore) ? PathOf(fh.collider.transform) : "?";
            LastSpots.Add(new Spot { pos = at, cells = cl.Count, above = floor == float.MaxValue ? -1f : at.y - floor, under = under, from = from, fromUnder = fromUnder });
        }

        int seeds = nodes.Count(n => n.seed), reached = nodes.Count(n => n.reached);
        string head = $"reach check{(label != "" ? " " + label : "")}: {nodes.Count} surfaces, {seeds} on the old men's NavMesh, {nodes.Count(n => n.climb && !n.seed)} prop tops they climb, {reached - seeds} more reached by jumping";
        if (LastSpots.Count == 0) lines.Add($"OK  {head}, none out of the old men's reach");
        else
        {
            lines.Add($"WARN {head}; {LastSpots.Count} spots out of the old men's reach (the player is safe there):");
            foreach (var s in LastSpots.OrderByDescending(s => s.cells))
                lines.Add($"       ({s.pos.x:0.0}, {s.pos.y:0.0}, {s.pos.z:0.0}) {s.cells * Step * Step:0.0} m2, {s.above:0.0} m up, on {s.under}; reached from ({s.from.x:0.0}, {s.from.y:0.0}, {s.from.z:0.0}) over {s.fromUnder}");
        }
        return lines;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (int i = 0; i < 3 && t != null; i++, t = t.parent) parts.Insert(0, t.name);
        return string.Join("/", parts);
    }

    [MenuItem("Tools/Island/Check Player-only Spots (open scene)")]
    public static void MenuRun() => Debug.Log(string.Join("\n", Run(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)));
}
