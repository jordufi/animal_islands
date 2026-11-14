// Reachability on a grid of standing spots (Original map, BeachIsland scene).
// ReachGraph: every spot where a capsule fits on a walkable surface, linked by walking, jumping up to `reach`,
// dropping, and jumping over obstacles up to `range` metres away (player) or only walking within the
// climb height (old men, like the NavMesh).
// OriginalMapAudit.Run: with every door closed, flood-fills from each zone's spawn points and reports every
// way into another zone. Picture in Renders/Original_reach_<tag>.png (zone colours, red = more than one zone).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ReachGraph
{
    public struct Node { public int cx, cz; public float y; }
    public struct Edge { public int to; public byte kind; }   // 0 walk, 1 jump up, 2 drop, 3 jump over

    public float cell, x0, z0;
    public int nx, nz;
    public List<Node> nodes = new List<Node>();
    public int[] cellStart;
    public List<Edge>[] edges;
    float lift, top, radius;
    public static readonly int Mask = ~((1 << 2) | (1 << 4) | (1 << TestMapSetup.PlayerLayer) | (1 << TestMapSetup.EnemyLayer));

    public Vector3 Pos(int cx, int cz, float y) => new Vector3(x0 + (cx + 0.5f) * cell, y, z0 + (cz + 0.5f) * cell);
    public Vector3 Pos(int n) => Pos(nodes[n].cx, nodes[n].cz, nodes[n].y);
    public int Cell(int n) => nodes[n].cz * nx + nodes[n].cx;

    bool Fits(Vector3 feet) =>
        !Physics.CheckCapsule(feet + Vector3.up * lift, feet + Vector3.up * top, radius, Mask, QueryTriggerInteraction.Ignore);

    bool Cast(Vector3 feet, Vector3 dir, float dist, out RaycastHit hit) =>
        Physics.CapsuleCast(feet + Vector3.up * lift, feet + Vector3.up * top, radius, dir, out hit, dist, Mask, QueryTriggerInteraction.Ignore);

    // capRadius / capHeight = the character; climb = walking step; reach = highest ledge reachable by jumping
    // (<= climb: no jumps); drops = may fall from any height; range = farthest jump over an obstacle; minY = lowest spot.
    public static ReachGraph Build(Rect area, float cell, float capRadius, float capHeight, float climb, float reach, bool drops, float range, float minY)
    {
        Physics.SyncTransforms();
        var g = new ReachGraph { cell = cell, x0 = area.xMin, z0 = area.yMin, nx = Mathf.CeilToInt(area.width / cell), nz = Mathf.CeilToInt(area.height / cell) };
        g.radius = capRadius - 0.02f;
        g.lift = capRadius + 0.15f;   // slopes don't touch it
        g.top = Mathf.Max(g.lift, capHeight - capRadius + 0.05f);
        g.cellStart = new int[g.nx * g.nz + 1];
        for (int cz = 0; cz < g.nz; cz++)
            for (int cx = 0; cx < g.nx; cx++)
            {
                g.cellStart[cz * g.nx + cx] = g.nodes.Count;
                var o = g.Pos(cx, cz, 40f);
                float last = float.PositiveInfinity;
                for (int guard = 0; guard < 10 && Physics.Raycast(o, Vector3.down, out var h, o.y + 20f, Mask, QueryTriggerInteraction.Ignore); guard++)
                {
                    if (h.normal.y >= 0.64f && last - h.point.y > 0.3f && h.point.y >= minY && g.Fits(h.point))
                    {
                        g.nodes.Add(new Node { cx = cx, cz = cz, y = h.point.y });
                        last = h.point.y;
                    }
                    o = h.point + Vector3.down * 0.02f;
                }
            }
        g.cellStart[g.nx * g.nz] = g.nodes.Count;

        g.edges = new List<Edge>[g.nodes.Count];
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        for (int a = 0; a < g.nodes.Count; a++)
        {
            var A = g.nodes[a];
            var list = g.edges[a] = new List<Edge>(8);
            Vector3 pa = g.Pos(A.cx, A.cz, A.y);
            float up = reach > climb ? (g.Cast(pa, Vector3.up, reach, out var uh) ? uh.distance : reach) : 0f;
            float peak = A.y + up;
            for (int d = 0; d < 8; d++)
            {
                int bx = A.cx + dx[d], bz = A.cz + dz[d];
                if (bx < 0 || bz < 0 || bx >= g.nx || bz >= g.nz) continue;
                bool any = false;
                int ci = bz * g.nx + bx;
                for (int b = g.cellStart[ci]; b < g.cellStart[ci + 1]; b++)
                {
                    float dy = g.nodes[b].y - A.y;
                    if (Mathf.Abs(dy) <= climb) { list.Add(new Edge { to = b, kind = 0 }); any = true; }
                    else if (dy > 0f && dy <= reach && peak >= g.nodes[b].y + 0.05f) { list.Add(new Edge { to = b, kind = 1 }); any = true; }
                    else if (dy < 0f && drops && g.Fits(g.Pos(bx, bz, A.y))) { list.Add(new Edge { to = b, kind = 2 }); any = true; }
                }
                if (any || range <= 0f) continue;
                // blocked this way: jump over (up at A, across at the peak, down at the landing)
                Vector3 dir = new Vector3(dx[d], 0f, dz[d]).normalized;
                float stepLen = cell * new Vector2(dx[d], dz[d]).magnitude;
                Vector3 pk = new Vector3(pa.x, peak, pa.z);
                for (int s = 2; s * stepLen <= range; s++)
                {
                    int cx = A.cx + dx[d] * s, cz = A.cz + dz[d] * s;
                    if (cx < 0 || cz < 0 || cx >= g.nx || cz >= g.nz) break;
                    if (g.Cast(pk, dir, s * stepLen, out _)) break;
                    int cc = cz * g.nx + cx;
                    bool landed = false;
                    for (int b = g.cellStart[cc]; b < g.cellStart[cc + 1]; b++)
                    {
                        float yb = g.nodes[b].y;
                        if (yb > peak - 0.05f) continue;
                        if (g.Cast(g.Pos(cx, cz, peak), Vector3.down, peak - yb - 0.02f, out _)) continue;
                        list.Add(new Edge { to = b, kind = 3 });
                        landed = true;
                    }
                    if (landed) break;
                }
            }
        }
        return g;
    }

    // Nearest spot to a world point (same or nearby cell, similar height), -1 if none.
    public int NodeAt(Vector3 p, float searchRadius = 0.6f)
    {
        int cx = Mathf.FloorToInt((p.x - x0) / cell), cz = Mathf.FloorToInt((p.z - z0) / cell);
        int best = -1, rad = Mathf.CeilToInt(searchRadius / cell);
        float bd = 1.5f + searchRadius;
        for (int ox = -rad; ox <= rad; ox++)
            for (int oz = -rad; oz <= rad; oz++)
            {
                int x = cx + ox, z = cz + oz;
                if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                int c = z * nx + x;
                for (int n = cellStart[c]; n < cellStart[c + 1]; n++)
                {
                    float d = Mathf.Abs(nodes[n].y - p.y) + 0.3f * cell * (Mathf.Abs(ox) + Mathf.Abs(oz));
                    if (d < bd) { bd = d; best = n; }
                }
            }
        return best;
    }

    public BitArray Flood(IEnumerable<int> sources)
    {
        var r = new BitArray(nodes.Count);
        var q = new Queue<int>();
        foreach (var s in sources) if (s >= 0 && !r[s]) { r[s] = true; q.Enqueue(s); }
        while (q.Count > 0)
        {
            int a = q.Dequeue();
            foreach (var e in edges[a]) if (!r[e.to]) { r[e.to] = true; q.Enqueue(e.to); }
        }
        return r;
    }
}

public static class OriginalMapAudit
{
    public static readonly Rect Area = new Rect(-40f, -37f, 68f, 69f);
    static readonly Color[] ZoneColors =
    {
        new Color(1f, 0.6f, 0.2f), new Color(1f, 0.9f, 0.3f), new Color(0.6f, 0.5f, 0.8f),
        new Color(0.3f, 0.8f, 0.3f), new Color(0.1f, 0.5f, 0.45f), new Color(0.3f, 0.5f, 1f),
    };
    public static string LastReport;

    public static Zone[] Zones() => Object.FindObjectsByType<Zone>().OrderBy(z => z.transform.GetSiblingIndex()).ToArray();

    // Player capsule (CharacterController r 0.3, h 1.06, step 0.3). Normal jump 1.2 m; the Frog's super jump
    // (4 m, also from the air) reaches ~5.2 m.
    public static string Run(float reach, float range, string tag)
    {
        var zones = Zones();
        var g = ReachGraph.Build(Area, 0.25f, 0.3f, 1.06f, 0.4f, reach, true, range, -50f);
        var sources = zones.Select(z => z.SpawnPoints.Select(t => g.NodeAt(t.position)).Where(n => n >= 0).ToList()).ToArray();
        var reached = sources.Select(s => g.Flood(s)).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine($"[{tag}] salto {reach} m, alcance {range} m: {g.nodes.Count} puntos");
        string[] kinds = { "andando", "saltando arriba", "cayendo", "saltando por encima" };
        int leaks = 0;
        for (int x = 0; x < zones.Length; x++)
            for (int y = 0; y < zones.Length; y++)
            {
                if (x == y) continue;
                int hit = sources[y].Count(s => reached[x][s]);
                if (hit == 0) continue;
                leaks++;
                sb.AppendLine($"FUGA {zones[x].name} -> {zones[y].name}: {hit}/{sources[y].Count} spawns");
                var spots = new Dictionary<Vector2Int, string>();
                for (int a = 0; a < g.nodes.Count; a++)
                {
                    if (!reached[x][a] || reached[y][a]) continue;
                    foreach (var e in g.edges[a])
                        if (reached[y][e.to])
                        {
                            var p = g.Pos(a);
                            var key = new Vector2Int(Mathf.RoundToInt(p.x / 3f), Mathf.RoundToInt(p.z / 3f));
                            if (!spots.ContainsKey(key)) spots[key] = $"   ({p.x:F1}, {p.y:F1}, {p.z:F1}) -> y {g.nodes[e.to].y:F1} {kinds[e.kind]}";
                        }
                }
                foreach (var s in spots.Values.Take(12)) sb.AppendLine(s);
            }
        if (leaks == 0) sb.AppendLine("OK: ninguna zona llega a otra con las puertas cerradas");
        WriteImage(g, reached, zones, $"Renders/Original_reach_{tag}.png");
        LastReport = sb.ToString();
        Debug.Log(LastReport);
        return LastReport;
    }

    // Graph with every door open; fwd = where a player gets to from the start, back = from where they can return.
    static ReachGraph DoorsOpen(float reach, float range, out int start, out BitArray fwd, out BitArray back)
    {
        var zones = Zones();
        var barriers = Object.FindObjectsByType<ZoneDoor>().Select(d => new SerializedObject(d).FindProperty("barrier").objectReferenceValue as GameObject)
            .Where(b => b != null && b.activeSelf).ToList();
        foreach (var b in barriers) b.SetActive(false);
        ReachGraph g;
        try { g = ReachGraph.Build(Area, 0.25f, 0.3f, 1.06f, 0.4f, reach, true, range, -50f); }
        finally { foreach (var b in barriers) b.SetActive(true); Physics.SyncTransforms(); }
        var gm = Object.FindAnyObjectByType<GameManager>();
        var sp = gm != null ? new SerializedObject(gm).FindProperty("playerSpawn").objectReferenceValue as Transform : null;
        int s = start = g.NodeAt(sp != null ? sp.position : zones[0].SpawnPoints.First().position);
        fwd = g.Flood(new[] { s });
        // back to the start: flood the reversed edges
        var rev = new List<int>[g.nodes.Count];
        for (int a = 0; a < g.nodes.Count; a++) foreach (var e in g.edges[a]) (rev[e.to] ??= new List<int>()).Add(a);
        back = new BitArray(g.nodes.Count);
        var q = new Queue<int>(); back[s] = true; q.Enqueue(s);
        while (q.Count > 0) { int a = q.Dequeue(); if (rev[a] != null) foreach (var b in rev[a]) if (!back[b]) { back[b] = true; q.Enqueue(b); } }
        return g;
    }

    // Pits a player can fall into but not climb out of (gaps between the rocks around the graveyard, cut by a
    // border): an invisible floor just under the rim, so nobody gets stuck. Only covers spots with no way back,
    // so it never closes a real path. Returns the number of boxes.
    public static int FillTraps(Transform parent, float reach, float range)
    {
        int boxes = 0;
        for (int iter = 0; iter < 4; iter++)
        {
            var g = DoorsOpen(reach, range, out _, out var fwd, out var back);
            var lids = new Dictionary<int, float>();   // node -> top of the lid
            for (int n = 0; n < g.nodes.Count; n++)
            {
                if (!fwd[n] || back[n]) continue;
                var A = g.nodes[n];
                float rim = float.NegativeInfinity;
                for (int ox = -3; ox <= 3; ox++)
                    for (int oz = -3; oz <= 3; oz++)
                    {
                        int x = A.cx + ox, z = A.cz + oz;
                        if (x < 0 || z < 0 || x >= g.nx || z >= g.nz) continue;
                        int c = z * g.nx + x;
                        for (int m = g.cellStart[c]; m < g.cellStart[c + 1]; m++)
                            if (back[m] && g.nodes[m].y > A.y && g.nodes[m].y < A.y + 4f) rim = Mathf.Max(rim, g.nodes[m].y);
                    }
                if (rim > float.NegativeInfinity) lids[n] = Mathf.Max(A.y + 0.05f, rim - 0.25f);
            }
            if (lids.Count == 0) break;
            foreach (var kv in lids)
            {
                var p = g.Pos(kv.Key);
                var go = new GameObject("Tapa" + (boxes++));
                go.transform.SetParent(parent, false);
                float bottom = p.y - 0.5f;
                go.transform.position = new Vector3(p.x, (bottom + kv.Value) / 2f, p.z);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(g.cell + 0.05f, kv.Value - bottom, g.cell + 0.05f);
            }
            Physics.SyncTransforms();
        }
        return boxes;
    }

    // With every door open: can a player walk/jump from the start to every zone's spawn points, and back from
    // everywhere they can get to (no pits / pockets to get stuck in)? Returns the number of problems.
    public static int CheckDoorsOpen(float reach, float range, out string detail)
    {
        var zones = Zones();
        var g = DoorsOpen(reach, range, out int s, out var fwd, out var back);
        var missing = zones.SelectMany(z => z.SpawnPoints.Select(t => (z, t))).Where(p => { int n = g.NodeAt(p.t.position); return n < 0 || !fwd[n]; })
            .Select(p => p.z.name + "/" + p.t.name).ToList();
        var traps = new Dictionary<Vector2Int, Vector3>();
        int trapNodes = 0;
        for (int n = 0; n < g.nodes.Count; n++)
            if (fwd[n] && !back[n])
            {
                trapNodes++;
                var p = g.Pos(n);
                var key = new Vector2Int(Mathf.RoundToInt(p.x / 2f), Mathf.RoundToInt(p.z / 2f));
                if (!traps.ContainsKey(key)) traps[key] = p;
            }
        // every open door can be walked through: from 1.5 m in front to 1.5 m behind in a short path (<= 15 m)
        var blockedDoors = new List<string>();
        // standing spots within 0.8 m of a point, near the door's height (some may be on a trunk or a rock)
        List<int> Spots(Vector3 p, float doorY)
        {
            var list = new List<int>();
            int cx = Mathf.FloorToInt((p.x - g.x0) / g.cell), cz = Mathf.FloorToInt((p.z - g.z0) / g.cell);
            for (int ox = -3; ox <= 3; ox++)
                for (int oz = -3; oz <= 3; oz++)
                {
                    int x = cx + ox, z = cz + oz;
                    if (x < 0 || z < 0 || x >= g.nx || z >= g.nz) continue;
                    int c = z * g.nx + x;
                    for (int n = g.cellStart[c]; n < g.cellStart[c + 1]; n++)
                        if (Mathf.Abs(g.nodes[n].y - doorY) <= 2.5f) list.Add(n);
                }
            return list;
        }
        HashSet<int> Near(int from)   // within 60 steps (~15 m)
        {
            var depth = new Dictionary<int, int> { [from] = 0 };
            var dq = new Queue<int>(); dq.Enqueue(from);
            while (dq.Count > 0)
            {
                int n = dq.Dequeue();
                if (depth[n] >= 60) continue;
                foreach (var e in g.edges[n]) if (!depth.ContainsKey(e.to)) { depth[e.to] = depth[n] + 1; dq.Enqueue(e.to); }
            }
            return new HashSet<int>(depth.Keys);
        }
        foreach (var d in Object.FindObjectsByType<ZoneDoor>())
        {
            var front = Spots(d.transform.position + d.transform.forward * 1.5f, d.transform.position.y);
            var behind = Spots(d.transform.position - d.transform.forward * 1.5f, d.transform.position.y);
            bool through = false;
            foreach (int a in front.Take(12))
            {
                var fromA = Near(a);
                foreach (int b in behind.Where(fromA.Contains).Take(3))
                    if (Near(b).Contains(a)) { through = true; break; }
                if (through) break;
            }
            if (!through) blockedDoors.Add(d.name);
        }
        var ok = new System.Collections.BitArray(fwd).And(back);
        var trap = new System.Collections.BitArray(back).Not().And(fwd);
        WriteImage(g, new[] { ok, trap }, zones, "Renders/Original_traps.png");   // orange = fine, yellow = can't get back
        var sb = new StringBuilder();
        sb.AppendLine(missing.Count == 0 ? "OK: todas las zonas accesibles con las puertas abiertas" : "INACCESIBLES: " + string.Join(", ", missing));
        sb.AppendLine(traps.Count == 0 ? "OK: ningún sitio del que no se pueda volver" :
            $"TRAMPAS ({trapNodes} puntos): " + string.Join(" ", traps.Values.Take(20).Select(p => $"({p.x:F1}, {p.y:F1}, {p.z:F1})")));
        sb.AppendLine(blockedDoors.Count == 0 ? "OK: todas las puertas abiertas se cruzan por un camino corto" : "PUERTAS SIN PASO: " + string.Join(", ", blockedDoors));
        detail = sb.ToString();
        return missing.Count + traps.Count + blockedDoors.Count;
    }

    public static void WriteImage(ReachGraph g, BitArray[] reached, Zone[] zones, string path)
    {
        const int S = 3;
        int W = g.nx * S, H = g.nz * S;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var px = new Color[W * H];
        for (int cz = 0; cz < g.nz; cz++)
            for (int cx = 0; cx < g.nx; cx++)
            {
                int c = cz * g.nx + cx;
                Color col = Color.black;
                float topY = float.NegativeInfinity;
                for (int n = g.cellStart[c]; n < g.cellStart[c + 1]; n++)
                {
                    int count = 0; Color zc = Color.black;
                    for (int z = 0; z < reached.Length; z++) if (reached[z][n]) { count++; zc = ZoneColors[z % ZoneColors.Length]; }
                    float shade = Mathf.Clamp01(0.55f + g.nodes[n].y * 0.12f);
                    Color nc = count == 0 ? new Color(0.25f, 0.25f, 0.25f) * shade : count == 1 ? zc * shade : Color.red;
                    if (count > 1 || g.nodes[n].y > topY) { col = nc; topY = count > 1 ? float.PositiveInfinity : g.nodes[n].y; }
                }
                for (int i = 0; i < S; i++) for (int k = 0; k < S; k++) px[(cz * S + k) * W + cx * S + i] = col;
            }
        foreach (var z in zones)
            foreach (var t in z.SpawnPoints)
            {
                int cx = Mathf.FloorToInt((t.position.x - g.x0) / g.cell), cz = Mathf.FloorToInt((t.position.z - g.z0) / g.cell);
                for (int i = -4; i <= 4; i++) for (int k = -4; k <= 4; k++)
                {
                    int x = cx * S + i, y = cz * S + k;
                    if (x >= 0 && y >= 0 && x < W && y < H && Mathf.Abs(i) + Mathf.Abs(k) <= 4) px[y * W + x] = Color.white;
                }
            }
        tex.SetPixels(px);
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
