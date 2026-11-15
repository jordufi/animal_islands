// Layout maps, part 3: heights (Docs/FASE3_FORMATO.md). Grid checks of the [heights] section, ramps (wedges),
// open ledges (a kerb you can see + one-way NavMeshLinks for the old men), slides '!' between zones, and the
// checks that need the baked NavMesh (walkable without dropping, slide rule, circuits, single-exit zones).
// A layout without [heights] and without '!' builds exactly as before.
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public static partial class IslaZombieSetup
{
    static readonly Vector2Int[] Four = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    // A cell you stand on: zone floor, marker, prop cell or pond (not sea, solid, door or slide).
    static bool IsStand(Layout L, char ch) => ch != '.' && !IsSolid(ch) && !IsDoor(ch) && ch != '!';
    // Plain walkable floor (zone letter or marker): ledges / links only between these.
    static bool IsWalk(Layout L, int c, int r) => L.InGrid(c, r) && IsFloor(L, L.At(c, r));
    static int GroupOf(Layout L, int c, int r)
    {
        var zd = L.InGrid(c, r) && L.cellZone[c, r] != '\0' ? L.Zone(L.cellZone[c, r]) : null;
        return zd != null ? zd.group : -1;
    }

    // ------------------------------------------------------------------ grid checks (before building)
    static List<string> CheckHeights(Layout L)
    {
        var report = new List<string>();
        bool slides = L.map.Any(row => row.Contains('!'));
        if (!L.HasHeights && !slides) return report;
        int errors = 0;
        void Err(string s) { errors++; if (errors <= 25) report.Add("ERROR heights: " + s); }

        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                char ch = L.At(c, r);
                char h = L.HeightAt(c, r);
                // ramps: on plain floor, run of 2-3, low side level L, high side level L+1
                if (L.IsRamp(c, r))
                {
                    if (L.Zone(ch) == null) Err($"ramp at ({c},{r}) must be plain zone floor in [map] (found '{ch}')");
                    if (L.rampIdx[c, r] != 0) continue;   // the rest is checked once per run, from its low end
                    var d = RampDir(L.ramp[c, r]);
                    int n = L.rampLen[c, r];
                    if (n < 2 || n > 3) Err($"ramp run at ({c},{r}) is {n} cells long (2 or 3 per level)");
                    int lc = c - d.x, lr = r - d.y, hc = c + d.x * n, hr = r + d.y * n;
                    bool lowOk = L.InGrid(lc, lr) && IsStand(L, L.At(lc, lr)) && !L.IsRamp(lc, lr);
                    bool highOk = L.InGrid(hc, hr) && IsStand(L, L.At(hc, hr)) && !L.IsRamp(hc, hr);
                    if (!lowOk) Err($"ramp run at ({c},{r}) has no floor at its low end ({lc},{lr})");
                    else if (!highOk) Err($"ramp run at ({c},{r}) ends in '{L.At(hc, hr)}' at ({hc},{hr}) (needs floor)");
                    else if (L.level[hc, hr] != L.level[lc, lr] + 1) Err($"ramp run at ({c},{r}) climbs from level {L.level[lc, lr]} to {L.level[hc, hr]} (must be +1)");
                    continue;
                }
                if (h != '.' && !(h >= '0' && h <= '9') && !IsRampChar(h)) Err($"unknown height '{h}' at ({c},{r})");
                // doors: both sides at the same level, never a ramp
                if (IsDoor(ch))
                {
                    var lv = new HashSet<int>();
                    foreach (var d in Four)
                    {
                        int nc = c + d.x, nr = r + d.y;
                        if (!L.InGrid(nc, nr) || !IsStand(L, L.At(nc, nr))) continue;
                        if (L.IsRamp(nc, nr)) Err($"door at ({c},{r}) touches a ramp at ({nc},{nr})");
                        lv.Add(L.level[nc, nr]);
                    }
                    if (lv.Count > 1) Err($"door at ({c},{r}) joins floors of levels {string.Join("/", lv)} (must be one level)");
                }
                // pond: same level as the floor around it
                if (ch == 'w')
                    foreach (var d in Four)
                    {
                        int nc = c + d.x, nr = r + d.y;
                        if (L.InGrid(nc, nr) && IsStand(L, L.At(nc, nr)) && L.At(nc, nr) != 'w' && !L.IsRamp(nc, nr) && L.level[nc, nr] != L.level[c, r])
                            Err($"pond at ({c},{r}) is level {L.level[c, r]} but the floor at ({nc},{nr}) is {L.level[nc, nr]}");
                    }
                // slides: two opposite floor neighbours at different levels
                if (ch == '!')
                {
                    var s = SlideEnds(L, c, r);
                    if (s == null) Err($"slide '!' at ({c},{r}) needs exactly two opposite floor neighbours at different levels");
                }
                // pool ladders (spawn out of the sea) only at sea level
                if (ch == 's' && L.level[c, r] > 0 && Four.Any(d => L.At(c + d.x, r + d.y) == '.') && !Four.Any(d => EntranceScore(L.At(c + d.x, r + d.y), L.cellZone[c, r]) > 2))
                    Err($"spawn at ({c},{r}) would climb out of the sea at level {L.level[c, r]} (sea is level 0)");
            }
        report.Add(errors == 0 ? $"OK  heights grid: {(L.HasHeights ? "levels " + string.Join("/", Enumerable.Range(0, 10).Where(lv => Cells(L).Any(p => !L.IsRamp(p.x, p.y) && IsStand(L, L.At(p.x, p.y)) && L.level[p.x, p.y] == lv))) : "flat")}, {Cells(L).Count(p => L.IsRamp(p.x, p.y) && L.rampIdx[p.x, p.y] == 0)} ramp runs, {Cells(L).Count(p => L.At(p.x, p.y) == '!')} slides"
                               : $"FAIL heights grid: {errors} errors");
        return report;
    }

    static IEnumerable<Vector2Int> Cells(Layout L)
    {
        for (int r = 0; r < L.H; r++) for (int c = 0; c < L.W; c++) yield return new Vector2Int(c, r);
    }

    // (high side, low side) floor cells of a slide, or null if it is malformed.
    static (Vector2Int hi, Vector2Int lo)? SlideEnds(Layout L, int c, int r)
    {
        foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) })
        {
            var a = new Vector2Int(c + d.x, r + d.y);
            var b = new Vector2Int(c - d.x, r - d.y);
            if (!L.InGrid(a.x, a.y) || !L.InGrid(b.x, b.y)) continue;
            if (!IsWalk(L, a.x, a.y) || !IsWalk(L, b.x, b.y) || L.IsRamp(a.x, a.y) || L.IsRamp(b.x, b.y)) continue;
            // the other two sides must be closed (solid / sea), so the slide is a gap in a straight wall
            var o1 = new Vector2Int(c + d.y, r + d.x);
            var o2 = new Vector2Int(c - d.y, r - d.x);
            if (IsWalk(L, o1.x, o1.y) || IsWalk(L, o2.x, o2.y)) continue;
            int la = L.level[a.x, a.y], lb = L.level[b.x, b.y];
            if (la == lb) return null;
            return la > lb ? (a, b) : (b, a);
        }
        return null;
    }

    // Open edges: two plain floor cells side by side in the same group, not ramps, at different levels.
    static List<(Vector2Int hi, Vector2Int lo)> OpenEdges(Layout L)
    {
        var edges = new List<(Vector2Int, Vector2Int)>();
        if (!L.HasHeights) return edges;
        foreach (var p in Cells(L))
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) })
            {
                var q = p + d;
                if (!L.InGrid(q.x, q.y)) continue;
                if (!IsStand(L, L.At(p.x, p.y)) || !IsStand(L, L.At(q.x, q.y))) continue;
                if (L.IsRamp(p.x, p.y) || L.IsRamp(q.x, q.y)) continue;
                if (GroupOf(L, p.x, p.y) != GroupOf(L, q.x, q.y)) continue;
                int a = L.level[p.x, p.y], b = L.level[q.x, q.y];
                if (a == b) continue;
                edges.Add(a > b ? (p, q) : (q, p));
            }
        return edges;
    }

    // ------------------------------------------------------------------ ramps
    // One wedge per ramp column: from the floor of its low level up to the next level, textured like the zone floor.
    static void BuildRamps(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        if (!L.HasHeights) return;
        var parent = Child(root, "Rampas", Vector3.zero).transform;
        var cliff = Shared("M_Rock", new Color(0.5f, 0.5f, 0.48f));
        foreach (var p in Cells(L))
        {
            if (!L.IsRamp(p.x, p.y) || L.rampIdx[p.x, p.y] != 0) continue;
            var d = RampDir(L.ramp[p.x, p.y]);
            int n = L.rampLen[p.x, p.y];
            float y0 = L.level[p.x, p.y] * LevelH, y1 = y0 + LevelH;
            // world axes: grid +x = world +x, grid +row = world -z
            Vector3 up = new Vector3(d.x, 0f, -d.y);                  // uphill, horizontal
            Vector3 side = new Vector3(up.z, 0f, -up.x);              // across the ramp
            Vector3 low = Flat(World(p.x, p.y)) - up * (Cell / 2f);   // centre of the low edge
            Vector3 high = low + up * (n * Cell);
            var zd = L.Zone(L.cellZone[p.x, p.y]);
            var mat = zd != null ? GroundMaterial(zd) : cliff;
            var go = new GameObject($"Rampa_{p.x}_{p.y}");
            go.transform.SetParent(parent, false);
            var mesh = WedgeMesh(low, high, side * (Cell / 2f), y0, y1);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { mat, cliff };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        // Ramp sides over lower floor: along the lowest cell the side is under the NavMesh climb (0.45 m) but over the
        // player's step (0.3 m), so the old men would walk onto the ramp sideways where a player is stopped (a NavMesh
        // "Not Walkable" volume does not cut it). A visible low stone kerb along that side stops both the same way;
        // higher up the side is too tall for anyone and stays an open drop.
        foreach (var p in Cells(L))
        {
            if (!L.IsRamp(p.x, p.y) || L.rampIdx[p.x, p.y] != 0) continue;
            var d = RampDir(L.ramp[p.x, p.y]);
            int n = L.rampLen[p.x, p.y];
            foreach (var s in new[] { new Vector2Int(-d.y, d.x), new Vector2Int(d.y, -d.x) })
            {
                int nc = p.x + s.x, nr = p.y + s.y;
                if (!IsWalk(L, nc, nr) || L.IsRamp(nc, nr) || World(nc, nr).y >= World(p.x, p.y).y - 0.05f) continue;
                Vector3 sw = new Vector3(s.x, 0f, -s.y);
                float floor = World(nc, nr).y, top = World(p.x, p.y).y + LevelH / (2f * n) + 0.25f - floor;
                Vector3 c = Flat(World(p.x, p.y)) + sw * (Cell / 2f + 0.125f) + Vector3.up * floor;
                var kerb = TexBox(parent, $"MuretRampa_{p.x}_{p.y}", c, s.x != 0 ? new Vector3(0.25f, top, Cell) : new Vector3(Cell, top, 0.25f), cliff, cliff, 0f);
                kerb.layer = TestMapSetup.WallLayer;
            }
        }
        // Same mismatch at the top cell beside HIGHER floor (the ramp ends ~0.3-0.45 m under it): a parapet 0.6 m tall on
        // the edge of the higher floor, so the ramp is entered from its top end only.
        foreach (var p in Cells(L))
        {
            if (!L.IsRamp(p.x, p.y) || L.rampIdx[p.x, p.y] != L.rampLen[p.x, p.y] - 1) continue;
            var d = RampDir(L.ramp[p.x, p.y]);
            foreach (var s in new[] { new Vector2Int(-d.y, d.x), new Vector2Int(d.y, -d.x) })
            {
                int nc = p.x + s.x, nr = p.y + s.y;
                if (!IsWalk(L, nc, nr) || L.IsRamp(nc, nr) || World(nc, nr).y <= World(p.x, p.y).y + 0.05f) continue;
                Vector3 sw = new Vector3(s.x, 0f, -s.y);
                float floor = World(nc, nr).y;
                Vector3 c = Flat(World(p.x, p.y)) + sw * (Cell / 2f + 0.125f) + Vector3.up * floor;
                var wall = TexBox(parent, $"PretilRampa_{p.x}_{p.y}", c, s.x != 0 ? new Vector3(0.25f, 0.6f, Cell) : new Vector3(Cell, 0.6f, 0.25f), cliff, cliff, 0f);
                wall.layer = TestMapSetup.WallLayer;
            }
        }
    }

    // Wedge in world space: slope from (low, y0) to (high, y1), a vertical back at `high`, two triangle sides, a bottom.
    // Submesh 0 = the slope (floor material), 1 = the rest (rock). UVs in world metres / 2 like the ground blocks.
    static Mesh WedgeMesh(Vector3 low, Vector3 high, Vector3 halfSide, float y0, float y1)
    {
        Vector3 L0 = low - halfSide + Vector3.up * y0, L1 = low + halfSide + Vector3.up * y0;
        Vector3 H0 = high - halfSide + Vector3.up * y1, H1 = high + halfSide + Vector3.up * y1;
        Vector3 B0 = high - halfSide + Vector3.up * y0, B1 = high + halfSide + Vector3.up * y0;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var slope = new List<int>();
        var rest = new List<int>();
        void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            int i = verts.Count;
            verts.AddRange(new[] { a, b, c, d });
            Vector3 n = Vector3.Cross(b - a, c - a);
            bool flip = Vector3.Dot(n, outward) < 0f;
            foreach (var v in new[] { a, b, c, d }) uvs.Add(Mathf.Abs(outward.y) > 0.5f ? new Vector2(v.x, v.z) / 2f : new Vector2(v.x + v.z, v.y) / 2f);
            if (!flip) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        void Tri(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            int i = verts.Count;
            verts.AddRange(new[] { a, b, c });
            foreach (var v in new[] { a, b, c }) uvs.Add(new Vector2(v.x + v.z, v.y) / 2f);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f) tris.AddRange(new[] { i, i + 1, i + 2 });
            else tris.AddRange(new[] { i, i + 2, i + 1 });
        }
        Vector3 upDir = (high - low).normalized;
        Quad(slope, L0, L1, H1, H0, Vector3.up);
        Quad(rest, B0, B1, H1, H0, upDir);
        Quad(rest, L0, L1, B1, B0, Vector3.down);
        Tri(rest, L0, B0, H0, -halfSide);
        Tri(rest, L1, B1, H1, halfSide);
        var mesh = new Mesh { name = "Rampa" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(slope, 0);
        mesh.SetTriangles(rest, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------ ledges and slides
    // A 15 cm kerb on the high side of every open edge (no collider: you can drop off, rule 1 "if it looks
    // passable, it is"), and the slide mouths.
    static void BuildLedgeLips(Transform root, Layout L)
    {
        var edges = OpenEdges(L);
        var slides = Cells(L).Where(p => L.At(p.x, p.y) == '!').ToList();
        if (edges.Count == 0 && slides.Count == 0) return;
        var parent = Child(root, "Bordes", Vector3.zero).transform;
        var kerb = Shared("M_StoneMoss", new Color(0.5f, 0.53f, 0.47f));
        foreach (var (hi, lo) in edges)
        {
            Vector3 a = World(hi.x, hi.y), b = World(lo.x, lo.y);
            Vector3 dir = Flat(b - a).normalized;
            Vector3 c = a + dir * (Cell / 2f - 0.1f);
            Vector3 size = Mathf.Abs(dir.x) > 0.5f ? new Vector3(0.2f, 0.15f, Cell) : new Vector3(Cell, 0.15f, 0.2f);
            Box(parent, "Bordillo", c, size, kerb, 0f, false);
        }
        var tube = M("Tobogan", new Color(0.95f, 0.45f, 0.1f));
        var hatch = M("Tobogan_Trampilla", new Color(0.3f, 0.3f, 0.32f));
        foreach (var p in slides)
        {
            var ends = SlideEnds(L, p.x, p.y);
            if (ends == null) continue;
            var (hi, lo) = ends.Value;
            Vector3 top = World(hi.x, hi.y), bottom = World(lo.x, lo.y), wall = Flat(World(p.x, p.y));
            Vector3 dir = Flat(bottom - top).normalized;
            Quaternion rot = Quaternion.LookRotation(dir);
            // top mouth: an orange tube entrance sticking out of the wall, the label says where it goes
            var mouth = Child(parent, $"Tobogan_{p.x}_{p.y}", new Vector3(wall.x, top.y, wall.z) - dir * (Cell / 2f));
            mouth.transform.rotation = rot;
            var ring = TexBox(mouth.transform, "Boca", mouth.transform.position - dir * 0.15f, new Vector3(1.6f, 1.9f, 0.3f), tube, tube, 0f, false);
            ring.transform.rotation = rot;
            var dark = TexBox(mouth.transform, "Hueco", mouth.transform.position - dir * 0.32f, new Vector3(1.1f, 1.5f, 0.05f), M("Oscuridad", new Color(0.02f, 0.02f, 0.025f)), M("Oscuridad", new Color(0.02f, 0.02f, 0.025f)), 0.05f, false);
            dark.transform.rotation = rot;
            var label = WorldLabel.Create(mouth.transform, Vector3.zero, "TOBOGÁN\n(solo bajada)", 0.22f, new Color(1f, 0.85f, 0.3f));
            label.transform.position = mouth.transform.position - dir * 0.5f + Vector3.up * 2.4f;
            // bottom: a closed hatch on the wall (no way up)
            var exitGo = TexBox(parent, $"Trampilla_{p.x}_{p.y}", new Vector3(wall.x, bottom.y, wall.z) + dir * (Cell / 2f + 0.05f), new Vector3(1.4f, 1.6f, 0.1f), hatch, hatch, 0f, false);
            exitGo.transform.rotation = rot;
            var slide = mouth.AddComponent<OneWaySlide>();
            slide.Configure(top + dir * (Cell / 2f - 0.6f), bottom + dir * 0.2f);
        }
    }

    // After the bake: one-way NavMeshLinks (both agent types) down every open ledge and through every slide.
    static int BuildLinks(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        var edges = OpenEdges(L).Where(e => IsWalk(L, e.hi.x, e.hi.y) && IsWalk(L, e.lo.x, e.lo.y)).ToList();
        var slides = Cells(L).Where(p => L.At(p.x, p.y) == '!').Select(p => SlideEnds(L, p.x, p.y)).Where(s => s != null).Select(s => s.Value).ToList();
        if (edges.Count == 0 && slides.Count == 0) return 0;
        var parent = Child(root, "Enlaces", Vector3.zero).transform;
        int n = 0;
        void Link(string name, Vector3 a, Vector3 b, float width, int area)
        {
            var go = Child(parent, name, (a + b) / 2f);
            foreach (int agent in new[] { 0, NavAgents.JumperId })
            {
                var link = go.AddComponent<NavMeshLink>();
                link.agentTypeID = agent;
                link.startPoint = go.transform.InverseTransformPoint(a);
                link.endPoint = go.transform.InverseTransformPoint(b);
                link.width = width;
                link.bidirectional = false;
                link.area = area;
                link.autoUpdate = false;
                n++;
            }
        }
        foreach (var (hi, lo) in edges)
        {
            Vector3 a = World(hi.x, hi.y), b = World(lo.x, lo.y);
            Vector3 dir = Flat(b - a).normalized;
            Vector3 edge = Flat(a) + dir * (Cell / 2f);
            Link($"Caida_{hi.x}_{hi.y}", edge - dir * 0.5f + Vector3.up * a.y, edge + dir * 0.6f + Vector3.up * b.y, Cell - 0.7f, LedgeArea);
        }
        foreach (var (hi, lo) in slides)
            Link($"Tobogan_{hi.x}_{hi.y}", World(hi.x, hi.y), World(lo.x, lo.y), 0.8f, SlideArea);
        return n;
    }

    // ------------------------------------------------------------------ checks on the baked NavMesh
    static List<string> CheckHeightsNav(Layout L, Dictionary<char, Zone> zones, Points pts)
    {
        var report = new List<string>();
        var filter = NavAgents.Filter(0);
        var path = new NavMeshPath();
        int walkOnly = NavMesh.AllAreas & ~(1 << LedgeArea) & ~(1 << SlideArea);   // every door open, no drops

        // 5) every floor cell walkable from the start without dropping (no terrace you only reach by falling)
        if (L.HasHeights && NavMesh.SamplePosition(pts.playerSpawn, out var start, 2f, filter))
        {
            int cells = 0, bad = 0;
            var worst = new List<string>();
            foreach (var p in Cells(L))
            {
                if (!IsWalk(L, p.x, p.y)) continue;
                if (!NavMesh.SamplePosition(World(p.x, p.y), out var h, 0.9f, filter)) continue;   // a machine stands here
                if (Mathf.Abs(h.position.y - World(p.x, p.y).y) > 0.6f) continue;               // snapped to another level
                cells++;
                if (NavMesh.CalculatePath(start.position, h.position, new NavMeshQueryFilter { agentTypeID = 0, areaMask = walkOnly }, path) && path.status == NavMeshPathStatus.PathComplete) continue;
                bad++;
                if (worst.Count < 12) worst.Add($"({p.x},{p.y})");
            }
            report.Add(bad == 0 ? $"OK  heights: all {cells} floor cells reachable walking (ramps, no drops) with every door open"
                                : $"ERROR heights: {bad}/{cells} floor cells only reachable by dropping or not at all: {string.Join(" ", worst)}");
        }

        // 7) slides
        report.AddRange(SlideReport(L));

        // 8) circuits in the zones flagged `circuito`
        foreach (var zd in L.zones.Where(z => z.circuit))
        {
            var (len, wide) = Circuit(L, zd.letter);
            report.Add(len >= 25f && wide >= 0.5f ? $"OK  circuit {zd.id}: {len:F0} m around the biggest block, {wide:P0} of it 2 cells wide"
                                                  : $"ERROR circuit {zd.id}: best loop {len:F0} m, {wide:P0} 2 cells wide (needs 25 m and 50 %)");
        }

        // 9) zones with a single way out (doors + floor contact with same-group zones + slides out)
        var single = new List<string>();
        foreach (var zd in L.zones)
        {
            int exits = L.doors.Count(d => d.zones.Contains(zd.letter));
            var touching = new HashSet<char>();
            foreach (var p in Cells(L))
            {
                if (L.cellZone[p.x, p.y] != zd.letter || !IsStand(L, L.At(p.x, p.y))) continue;
                foreach (var d in Four)
                {
                    int nc = p.x + d.x, nr = p.y + d.y;
                    if (!L.InGrid(nc, nr) || !IsStand(L, L.At(nc, nr))) continue;
                    char z2 = L.cellZone[nc, nr];
                    if (z2 != zd.letter && z2 != '\0') touching.Add(z2);
                }
            }
            exits += touching.Count;
            exits += Cells(L).Count(p => L.At(p.x, p.y) == '!' && SlideEnds(L, p.x, p.y) is var s && s != null && L.cellZone[s.Value.hi.x, s.Value.hi.y] == zd.letter);
            if (exits <= 1) single.Add(zd.id);
        }
        report.Add($"{(single.Count <= 2 ? "OK " : "WARN")} {single.Count} zones with a single way out (max 2): {string.Join(", ", single)}");
        return report;
    }

    // 7) a slide from zone A to zone B: B is always open when A is (B's group can't be skipped on the way to A).
    // Pure door-graph logic, no NavMesh.
    static List<string> SlideReport(Layout L)
    {
        var report = new List<string>();
        foreach (var p in Cells(L).Where(p => L.At(p.x, p.y) == '!'))
        {
            var ends = SlideEnds(L, p.x, p.y);
            if (ends == null) continue;
            int ga = GroupOf(L, ends.Value.hi.x, ends.Value.hi.y), gb = GroupOf(L, ends.Value.lo.x, ends.Value.lo.y);
            var startZone = L.zones.FirstOrDefault(z => z.start);
            int g0 = startZone != null ? startZone.group : 0;
            bool reachWithoutB = ga != gb && gb != g0 && GroupReach(L, g0, gb).Contains(ga);
            var za = L.Zone(L.cellZone[ends.Value.hi.x, ends.Value.hi.y]);
            var zb = L.Zone(L.cellZone[ends.Value.lo.x, ends.Value.lo.y]);
            report.Add(!reachWithoutB ? $"OK  slide ({p.x},{p.y}) {za?.id} -> {zb?.id}: {zb?.id} is always open when {za?.id} is"
                                      : $"ERROR slide ({p.x},{p.y}) {za?.id} -> {zb?.id}: {za?.id} can be opened without {zb?.id} (the player would land in a closed zone)");
        }
        return report;
    }

    // Groups reachable from `from` through the door graph, never entering `without`.
    static HashSet<int> GroupReach(Layout L, int from, int without)
    {
        var seen = new HashSet<int> { from };
        var q = new Queue<int>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            int g = q.Dequeue();
            foreach (var d in L.doors)
            {
                var gs = d.zones.Select(z => L.Zone(z)).Where(z => z != null).Select(z => z.group).Distinct().ToList();
                if (!gs.Contains(g)) continue;
                foreach (int o in gs)
                    if (o != without && seen.Add(o)) q.Enqueue(o);
            }
        }
        return seen;
    }

    // The loop around the biggest block of non-walkable cells (props, ponds, solids) enclosed by the zone's floor:
    // its ring of floor cells (8-neighbours of the block) must be all walkable. Returns (length m, share of ring cells
    // with a second walkable cell outward = 2-cell lane).
    static (float length, float wide) Circuit(Layout L, char zone)
    {
        bool InZone(int c, int r) => L.InGrid(c, r) && L.cellZone[c, r] == zone;
        bool Walk(int c, int r) => InZone(c, r) && IsWalk(L, c, r);
        var seen = new bool[L.W, L.H];
        float bestLen = 0f, bestWide = 0f;
        foreach (var p in Cells(L))
        {
            if (seen[p.x, p.y] || !InZone(p.x, p.y) || Walk(p.x, p.y)) continue;
            // flood the block
            var block = new List<Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(p); seen[p.x, p.y] = true;
            bool open = true;
            while (q.Count > 0)
            {
                var b = q.Dequeue();
                block.Add(b);
                foreach (var d in Four)
                {
                    var n = b + d;
                    if (!L.InGrid(n.x, n.y)) { open = false; continue; }
                    if (Walk(n.x, n.y)) continue;
                    if (!InZone(n.x, n.y)) { open = false; continue; }   // touches the zone border: not an island
                    if (!seen[n.x, n.y]) { seen[n.x, n.y] = true; q.Enqueue(n); }
                }
            }
            if (!open) continue;
            var ring = new HashSet<Vector2Int>();
            foreach (var b in block)
                for (int dr = -1; dr <= 1; dr++) for (int dc = -1; dc <= 1; dc++)
                {
                    var n = new Vector2Int(b.x + dc, b.y + dr);
                    if (Walk(n.x, n.y)) ring.Add(n);
                }
            float len = ring.Count * Cell;
            int wide = ring.Count(r => Four.Any(d => ring.Contains(r + d) == false && Walk(r.x + d.x, r.y + d.y)));
            float share = ring.Count > 0 ? (float)wide / ring.Count : 0f;
            // a loop that meets the rule (25 m, half of it 2 cells wide) beats any longer one that doesn't
            bool good = len >= 25f && share >= 0.5f, bestGood = bestLen >= 25f && bestWide >= 0.5f;
            if ((good && !bestGood) || (good == bestGood && len > bestLen)) { bestLen = len; bestWide = share; }
        }
        return (bestLen, bestWide);
    }
}
