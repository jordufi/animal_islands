// Zone borders for the Original map (BeachIsland scene): every zone gets a sealed area and an invisible,
// very tall wall runs along every border between two areas and around the island (a little into the sea),
// except across the doors. Nobody can jump (or Frog-super-jump) from one zone into another.
// The areas: with every door closed, the old men's walkable ground (like the NavMesh) is flood-filled from each
// zone's spawn points / box spots; every other cell (obstacles, water, ledges) goes to the nearest area and the
// sea further than SeaMargin from land is outside.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class OriginalMapBorders
{
    public const float Cell = 0.5f, SeaY = -0.05f, SeaMargin = 1.5f;
    public const float WallBottom = -6f, WallTop = 30f, WallThick = 0.3f;
    public static string LastReport;

    public class Result
    {
        public ReachGraph g;
        public int[] label;          // per cell: zone index, -1 outside
        public bool[] land, open;    // open = a player can stand there (no obstacle)
        public List<(Vector2 a, Vector2 b)> segments = new List<(Vector2, Vector2)>();
        public string report;
    }

    public static Result Compute(Zone[] zones, ZoneDoor[] doors)
    {
        var res = new Result();
        var sb = new StringBuilder();
        // old men: r 0.35, h 1.4, climb 0.45, no jumps, sea not walkable
        var g = res.g = ReachGraph.Build(OriginalMapAudit.Area, Cell, 0.35f, 1.4f, 0.45f, 0.45f, false, 0f, SeaY);
        int nx = g.nx, nz = g.nz, N = nx * nz;

        // 1) flood the walkable ground from every zone at once
        var nodeLabel = Enumerable.Repeat(-1, g.nodes.Count).ToArray();
        var q = new Queue<int>();
        for (int z = 0; z < zones.Length; z++)
            foreach (var t in zones[z].SpawnPoints.Concat(zones[z].BoxSpots))
            {
                int n = g.NodeAt(t.position, 1.5f);
                if (n < 0) { sb.AppendLine($"AVISO: {zones[z].name}/{t.name} sin suelo"); continue; }
                if (nodeLabel[n] >= 0 && nodeLabel[n] != z) sb.AppendLine($"CONFLICTO: {t.name} de {zones[z].name} ya es de {zones[nodeLabel[n]].name}");
                nodeLabel[n] = z;
                q.Enqueue(n);
            }
        var conflicts = new HashSet<Vector2Int>();
        while (q.Count > 0)
        {
            int a = q.Dequeue();
            foreach (var e in g.edges[a])
            {
                if (nodeLabel[e.to] < 0) { nodeLabel[e.to] = nodeLabel[a]; q.Enqueue(e.to); }
                else if (nodeLabel[e.to] != nodeLabel[a])
                {
                    var p = g.Pos(a);
                    if (conflicts.Add(new Vector2Int(Mathf.RoundToInt(p.x / 3f), Mathf.RoundToInt(p.z / 3f))))
                        sb.AppendLine($"CONFLICTO: {zones[nodeLabel[a]].name} toca {zones[nodeLabel[e.to]].name} en ({p.x:F1}, {p.z:F1}) sin puerta");
                }
            }
        }

        // 2) cells: land / open / label
        var label = res.label = Enumerable.Repeat(-1, N).ToArray();
        var land = res.land = new bool[N];
        var open = res.open = new bool[N];
        var pg = ReachGraph.Build(OriginalMapAudit.Area, Cell, 0.3f, 1.06f, 0.4f, 0.4f, false, 0f, -50f);   // player spots
        int groundMask = ReachGraph.Mask & ~((1 << TestMapSetup.WallLayer) | (1 << TestMapSetup.DoorLayer));
        // spread the areas over the ground a player walks on (same level, no jumps): strips at the foot of a
        // ledge belong to the zone down there, so the border sits on the ledge and leaves no pit to fall into
        var pLabel = Enumerable.Repeat(-1, pg.nodes.Count).ToArray();
        for (int c = 0; c < N; c++)
            for (int n = pg.cellStart[c]; n < pg.cellStart[c + 1]; n++)
                for (int m = g.cellStart[c]; m < g.cellStart[c + 1]; m++)
                    if (nodeLabel[m] >= 0 && Mathf.Abs(g.nodes[m].y - pg.nodes[n].y) < 0.3f) { pLabel[n] = nodeLabel[m]; q.Enqueue(n); }
        while (q.Count > 0)
        {
            int a = q.Dequeue();
            foreach (var e in pg.edges[a])
                if (pLabel[e.to] < 0 && pg.nodes[e.to].y >= SeaY) { pLabel[e.to] = pLabel[a]; q.Enqueue(e.to); }
        }
        for (int c = 0; c < N; c++)
        {
            for (int n = pg.cellStart[c]; n < pg.cellStart[c + 1]; n++)
                if (pLabel[n] >= 0) label[c] = pLabel[n];
            for (int n = g.cellStart[c]; n < g.cellStart[c + 1]; n++)
                if (nodeLabel[n] >= 0) label[c] = nodeLabel[n];
            var o = g.Pos(c % nx, c / nx, 40f);
            bool ground = Physics.Raycast(o, Vector3.down, out var h, 80f, groundMask, QueryTriggerInteraction.Ignore);
            land[c] = ground && h.point.y >= SeaY;
            // a player can stand there (not on top of a wall)
            for (int n = pg.cellStart[c]; n < pg.cellStart[c + 1]; n++)
                if (land[c] && pg.nodes[n].y <= h.point.y + 2.5f) open[c] = true;
        }
        // nearest area for everything else
        var dq = new Queue<int>();
        for (int c = 0; c < N; c++) if (label[c] >= 0) dq.Enqueue(c);
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        while (dq.Count > 0)
        {
            int c = dq.Dequeue();
            for (int d = 0; d < 8; d++)
            {
                int x = c % nx + dx[d], z = c / nx + dz[d];
                if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                int m = z * nx + x;
                if (label[m] < 0) { label[m] = label[c]; dq.Enqueue(m); }
            }
        }
        // the sea further than SeaMargin from land is outside
        var seaDist = Enumerable.Repeat(int.MaxValue, N).ToArray();
        for (int c = 0; c < N; c++) if (land[c]) { seaDist[c] = 0; dq.Enqueue(c); }
        while (dq.Count > 0)
        {
            int c = dq.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int x = c % nx + dx[d], z = c / nx + dz[d];
                if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                int m = z * nx + x;
                if (seaDist[m] == int.MaxValue) { seaDist[m] = seaDist[c] + 1; dq.Enqueue(m); }
            }
        }
        // ...when connected to the open sea (the pond stays inside)
        int margin = Mathf.RoundToInt(SeaMargin / Cell);
        var outside = new bool[N];
        for (int c = 0; c < N; c++)
        {
            int x = c % nx, z = c / nx;
            if ((x == 0 || z == 0 || x == nx - 1 || z == nz - 1) && seaDist[c] > margin) { outside[c] = true; dq.Enqueue(c); }
        }
        while (dq.Count > 0)
        {
            int c = dq.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int x = c % nx + dx[d], z = c / nx + dz[d];
                if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                int m = z * nx + x;
                if (!outside[m] && seaDist[m] > margin) { outside[m] = true; dq.Enqueue(m); }
            }
        }
        for (int c = 0; c < N; c++) if (outside[c]) label[c] = -1;

        // 3) at each door one side is one of its zones and the other side the other one (the areas may wrap
        //    around the end of a barrier that doesn't span the whole gap); only the door line itself has no wall
        var doorSide = new int[N];   // (door index + 1) * side
        for (int di = 0; di < doors.Length; di++)
        {
            var door = doors[di];
            var box = door.GetComponentInChildren<BoxCollider>(true);
            float w = box != null ? box.size.x : 4.6f;
            var conn = new SerializedObject(door).FindProperty("connects");
            var zi = Enumerable.Range(0, conn.arraySize).Select(k => System.Array.IndexOf(zones, conn.GetArrayElementAtIndex(k).objectReferenceValue as Zone)).Where(k => k >= 0).ToArray();
            if (zi.Length != 2) { sb.AppendLine($"AVISO: {door.name} no une dos zonas"); continue; }
            var cells = new List<(int c, float lx, float lz)>();
            for (int c = 0; c < N; c++)
            {
                var lp = door.transform.InverseTransformPoint(g.Pos(c % nx, c / nx, door.transform.position.y));
                if (Mathf.Abs(lp.x) <= w / 2f + 2f && Mathf.Abs(lp.z) <= 3f) cells.Add((c, lp.x, lp.z));
            }
            // which zone is on the +z side: where its open cells are, a little away from the barrier
            int score = 0;
            foreach (var (c, lx, lz) in cells)
                if (open[c] && Mathf.Abs(lx) <= w / 2f && Mathf.Abs(lz) > 1f)
                    score += (label[c] == zi[0] ? 1 : label[c] == zi[1] ? -1 : 0) * (lz > 0 ? 1 : -1);
            if (score == 0) sb.AppendLine($"AVISO: {door.name}: no se sabe qué zona queda a cada lado");
            int plus = score >= 0 ? zi[0] : zi[1], minus = score >= 0 ? zi[1] : zi[0];
            foreach (var (c, lx, lz) in cells)
                if (Mathf.Abs(lx) <= w / 2f && Mathf.Abs(lz) <= 1f)
                {
                    label[c] = lz > 0 ? plus : minus;
                    doorSide[c] = (di + 1) * (lz > 0 ? 1 : -1);
                }
        }
        // the edge between cells a and b is the door line (no wall) if they are on opposite sides of a door,
        // away from the ends of the barrier
        bool Exempt(int a, int b, Vector2 mid)
        {
            if (a < 0 || b < 0 || doorSide[a] == 0 || doorSide[a] != -doorSide[b]) return false;
            var door = doors[Mathf.Abs(doorSide[a]) - 1];
            var box = door.GetComponentInChildren<BoxCollider>(true);
            float w = box != null ? box.size.x : 4.6f;
            var lp = door.transform.InverseTransformPoint(new Vector3(mid.x, door.transform.position.y, mid.y));
            return Mathf.Abs(lp.x) <= w / 2f - 0.3f;
        }
        int Idx(int x, int z) => x < 0 || z < 0 || x >= nx || z >= nz ? -1 : z * nx + x;
        int L(int x, int z) => x < 0 || z < 0 || x >= nx || z >= nz ? -1 : label[z * nx + x];
        // walls on x = const (between x-1 and x), merged along z
        for (int x = 0; x <= nx; x++)
        {
            int start = -1;
            for (int z = 0; z <= nz; z++)
            {
                bool wall = false;
                if (z < nz)
                {
                    int a = L(x - 1, z), b = L(x, z);
                    float wx = g.x0 + x * Cell, wz = g.z0 + (z + 0.5f) * Cell;
                    wall = a != b && !Exempt(Idx(x - 1, z), Idx(x, z), new Vector2(wx, wz));
                }
                if (wall && start < 0) start = z;
                if (!wall && start >= 0)
                {
                    float wx = g.x0 + x * Cell;
                    res.segments.Add((new Vector2(wx, g.z0 + start * Cell), new Vector2(wx, g.z0 + z * Cell)));
                    start = -1;
                }
            }
        }
        // walls on z = const, merged along x
        for (int z = 0; z <= nz; z++)
        {
            int start = -1;
            for (int x = 0; x <= nx; x++)
            {
                bool wall = false;
                if (x < nx)
                {
                    int a = L(x, z - 1), b = L(x, z);
                    float wx = g.x0 + (x + 0.5f) * Cell, wz = g.z0 + z * Cell;
                    wall = a != b && !Exempt(Idx(x, z - 1), Idx(x, z), new Vector2(wx, wz));
                }
                if (wall && start < 0) start = x;
                if (!wall && start >= 0)
                {
                    float wz = g.z0 + z * Cell;
                    res.segments.Add((new Vector2(g.x0 + start * Cell, wz), new Vector2(g.x0 + x * Cell, wz)));
                    start = -1;
                }
            }
        }
        sb.Insert(0, $"[Bordes] {res.segments.Count} tramos de muro, {conflicts.Count} conflictos\n");
        res.report = sb.ToString();
        return res;
    }

    // Invisible walls (Wall layer) under `parent`.
    public static void Build(Transform parent, Result r)
    {
        int i = 0;
        foreach (var (a, b) in r.segments)
        {
            var mid = (a + b) / 2f;
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
            var go = new GameObject("Borde" + (i++));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(mid.x, (WallBottom + WallTop) / 2f, mid.y);
            go.layer = TestMapSetup.WallLayer;
            var box = go.AddComponent<BoxCollider>();
            float len = (b - a).magnitude + WallThick;   // overlap at the corners
            box.size = alongX ? new Vector3(len, WallTop - WallBottom, WallThick) : new Vector3(WallThick, WallTop - WallBottom, len);
        }
    }

    // Points every `spacing` m along the borders where a player could stand right next to the wall on both
    // sides (open ground or a ledge): an invisible wall there needs something you can see. Not near doors.
    public static List<Vector3> Exposed(Result r, ZoneDoor[] doors, float spacing)
    {
        var g = r.g;
        var spots = new List<Vector3>();
        bool Open(Vector2 p)
        {
            int x = Mathf.FloorToInt((p.x - g.x0) / Cell), z = Mathf.FloorToInt((p.y - g.z0) / Cell);
            return x >= 0 && z >= 0 && x < g.nx && z < g.nz && r.open[z * g.nx + x];
        }
        foreach (var (a, b) in r.segments)
        {
            Vector2 dir = (b - a).normalized, nrm = new Vector2(-dir.y, dir.x);
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / spacing));
            for (int i = 0; i < n; i++)
            {
                var p = Vector2.Lerp(a, b, (i + 0.5f) / n);
                if (!Open(p + nrm * Cell * 0.5f) || !Open(p - nrm * Cell * 0.5f)) continue;
                bool nearDoor = false;
                foreach (var door in doors)
                {
                    var box = door.GetComponentInChildren<BoxCollider>(true);
                    float w = box != null ? box.size.x : 4.6f;
                    var lp = door.transform.InverseTransformPoint(new Vector3(p.x, door.transform.position.y, p.y));
                    if (Mathf.Abs(lp.x) <= w / 2f + 1.6f && Mathf.Abs(lp.z) <= 2f) nearDoor = true;
                }
                if (!nearDoor) spots.Add(new Vector3(p.x, 0f, p.y));
            }
        }
        return spots;
    }

    // A border wall continues past the end of a door: until now that stretch (about 1.6 m beside every door) had
    // nothing to see, so the player ran into an invisible wall right next to the door. The flank is a fence with
    // the door's own posts and cloth, every `spacing` m, from the door's end up to FlankLength m beside it.
    public const float FlankLength = 4f;
    public struct Flank { public Vector3 pos; public Vector2 dir; public ZoneDoor door; }

    public static List<Flank> Flanks(Result r, ZoneDoor[] doors, float spacing)
    {
        var g = r.g;
        var res = new List<Flank>();
        bool Open(Vector2 p)
        {
            int x = Mathf.FloorToInt((p.x - g.x0) / Cell), z = Mathf.FloorToInt((p.y - g.z0) / Cell);
            return x >= 0 && z >= 0 && x < g.nx && z < g.nz && r.open[z * g.nx + x];
        }
        foreach (var (a, b) in r.segments)
        {
            Vector2 dir = (b - a).normalized, nrm = new Vector2(-dir.y, dir.x);
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / spacing));
            for (int i = 0; i < n; i++)
            {
                var p = Vector2.Lerp(a, b, (i + 0.5f) / n);
                if (!Open(p + nrm * Cell * 0.5f) || !Open(p - nrm * Cell * 0.5f)) continue;
                foreach (var door in doors)
                {
                    var box = door.GetComponentInChildren<BoxCollider>(true);
                    float w = box != null ? box.size.x : 4.6f;
                    var lp = door.transform.InverseTransformPoint(new Vector3(p.x, door.transform.position.y, p.y));
                    float off = Mathf.Abs(lp.x) - w / 2f;
                    if (off > -0.2f && off <= FlankLength && Mathf.Abs(lp.z) <= 2f)
                    {
                        res.Add(new Flank { pos = new Vector3(p.x, 0f, p.y), dir = dir, door = door });
                        break;
                    }
                }
            }
        }
        return res;
    }

    public static int DecorateFlanks(Transform parent, List<Flank> flanks, float spacing)
    {
        int groundMask = ReachGraph.Mask & ~((1 << TestMapSetup.WallLayer) | (1 << TestMapSetup.DoorLayer));
        int i = 0;
        foreach (var f in flanks)
        {
            var barrier = f.door.GetComponentInChildren<BoxCollider>(true).transform;
            Material pole = barrier.GetChild(0).GetComponent<Renderer>().sharedMaterial, cloth = barrier.GetChild(2).GetComponent<Renderer>().sharedMaterial;
            float y = Physics.Raycast(f.pos + Vector3.up * 40f, Vector3.down, out var h, 80f, groundMask, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;
            var go = new GameObject("VallaPuerta" + (i++)).transform;
            go.SetParent(parent, false);
            go.position = new Vector3(f.pos.x, y, f.pos.z);
            go.rotation = Quaternion.LookRotation(new Vector3(f.dir.x, 0f, f.dir.y));
            TestMapSetup.Prim(PrimitiveType.Cylinder, go, new Vector3(0, 0.8f, 0), new Vector3(0.12f, 0.8f, 0.12f), pole);
            TestMapSetup.Prim(PrimitiveType.Cube, go, new Vector3(0, 1.0f, spacing / 2f), new Vector3(0.05f, 1.0f, spacing), cloth);
        }
        return i;
    }

    // Rocks (with collision) on the exposed border points.
    public static int Decorate(Transform parent, List<Vector3> spots, GameObject[] rocks)
    {
        int i = 0;
        int groundMask = ReachGraph.Mask & ~((1 << TestMapSetup.WallLayer) | (1 << TestMapSetup.DoorLayer));
        foreach (var s in spots)
        {
            float y = Physics.Raycast(s + Vector3.up * 40f, Vector3.down, out var h, 80f, groundMask, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;
            var rock = (GameObject)PrefabUtility.InstantiatePrefab(rocks[i % rocks.Length], parent);
            rock.name = "RocaBorde" + i;
            rock.transform.position = new Vector3(s.x, y - 0.3f, s.z);
            rock.transform.rotation = Quaternion.Euler(0f, (i * 73) % 360, 0f);
            rock.transform.localScale = Vector3.one * (0.95f + 0.1f * ((i * 37) % 3));
            foreach (var mf in rock.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
            i++;
        }
        return i;
    }

    // Top-down picture: area colours, borders white, cells a player can stand on brighter, exposed points red.
    public static void Preview(Result r, string path, List<Vector3> exposed = null)
    {
        const int S = 6;
        var g = r.g;
        int W = g.nx * S, H = g.nz * S;
        Color[] zc =
        {
            new Color(1f, 0.6f, 0.2f), new Color(1f, 0.9f, 0.3f), new Color(0.6f, 0.5f, 0.8f),
            new Color(0.3f, 0.8f, 0.3f), new Color(0.1f, 0.6f, 0.55f), new Color(0.3f, 0.5f, 1f),
        };
        var px = new Color[W * H];
        for (int c = 0; c < g.nx * g.nz; c++)
        {
            int cx = c % g.nx, cz = c / g.nx;
            Color col = r.label[c] < 0 ? new Color(0.05f, 0.1f, 0.25f) : zc[r.label[c] % zc.Length] * (r.open[c] ? 1f : 0.45f);
            if (!r.land[c] && r.label[c] >= 0) col = Color.Lerp(col, new Color(0.1f, 0.3f, 0.7f), 0.5f);
            for (int i = 0; i < S; i++) for (int k = 0; k < S; k++) px[(cz * S + k) * W + cx * S + i] = col;
        }
        foreach (var (a, b) in r.segments)
        {
            int steps = Mathf.CeilToInt((b - a).magnitude / Cell * S) + 1;
            for (int s = 0; s <= steps; s++)
            {
                var p = Vector2.Lerp(a, b, s / (float)steps);
                int x = Mathf.RoundToInt((p.x - g.x0) / Cell * S), y = Mathf.RoundToInt((p.y - g.z0) / Cell * S);
                for (int o = -1; o <= 1; o++)
                {
                    int xx = Mathf.Abs(b.x - a.x) > 0.01f ? x : x + o, yy = Mathf.Abs(b.x - a.x) > 0.01f ? y + o : y;
                    if (xx >= 0 && yy >= 0 && xx < W && yy < H) px[yy * W + xx] = Color.white;
                }
            }
        }
        if (exposed != null)
            foreach (var e in exposed)
            {
                int x = Mathf.RoundToInt((e.x - g.x0) / Cell * S), y = Mathf.RoundToInt((e.z - g.z0) / Cell * S);
                for (int i = -4; i <= 4; i++) for (int k = -4; k <= 4; k++)
                    if (x + i >= 0 && y + k >= 0 && x + i < W && y + k < H) px[(y + k) * W + x + i] = Color.red;
            }
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.SetPixels(px);
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
