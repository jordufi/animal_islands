// Isla Zombie setup, part 2: enemy spawn entrances and the spawn coverage check.
// Every spawn marker (s) of the layout becomes a SpawnEntrance built into the solid cell next to it:
//   wall (#)     -> a cabin with a door built into the wall (WC on the beach, showers at the camping...)
//   rock / temple -> a dark crack in the rock
//   sea (.)      -> a pool ladder: the old man climbs out of the water
//   prop         -> the prop itself (bus, beach hut, tent, market stall, container)
// The old man waits hidden inside, the door rattles and opens, he walks out and climbs the barrier
// (~1 s in sight) and lands on the NavMesh at the spawn cell.
// Every entrance wears the same colour (SpawnColor: cabins, doors, fences, ladder rails) so they read as
// "old men come out here" and are never confused with the paid zone doors (red / blue / yellow / gold /
// purple cloths) or the beach towels.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class IslaZombieSetup
{
    class EntrancePlan
    {
        public Vector2Int cell;     // the spawn (floor) cell
        public Vector2Int n;        // the solid / prop / sea cell the old man comes out of
        public char kind;           // char of n: # R Z . o h W
        public char zone;
    }

    static readonly Vector2Int[] Dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };

    // How good a neighbour is to come out of (higher = preferred), -1 = impossible.
    static int EntranceScore(char ch, char zone)
    {
        switch (ch)
        {
            case 'W': return 7;                                    // bus del Imserso
            case 'h': return 6;                                    // beach hut / tiki bar
            case 'o': return Urb ? (zone == 'n' || zone == 'i' ? 5 : 1) : zone == 'k' || zone == 'm' || zone == 'd' ? 5 : zone == 'f' ? 3 : 1;   // tent / stall / container, faro rocks
            case '#': return 4;
            case 'R': case 'Z': return 3;
            case '.': return 2;
            default: return -1;
        }
    }

    static List<EntrancePlan> PlanEntrances(Layout L, List<string> report)
    {
        var plans = new List<EntrancePlan>();
        L.cabins.Clear();
        venDoorFaces.Clear();
        int plain = 0;
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                if (L.At(c, r) != 's') continue;
                char z = L.cellZone[c, r];
                EntrancePlan best = null;
                int bestScore = -1;
                foreach (var d in Dirs.OrderBy(d => Hash(c, r, d.x * 3 + d.y + 40)))
                {
                    var n = new Vector2Int(c + d.x, r + d.y);
                    int score = EntranceScore(L.At(n.x, n.y), z);
                    if (score > bestScore) { bestScore = score; best = new EntrancePlan { cell = new Vector2Int(c, r), n = n, kind = L.At(n.x, n.y), zone = z }; }
                }
                if (best == null) { plain++; report.Add($"WARNING spawn at cell ({c},{r}) has nothing solid next to it: plain spawn point (no entrance)"); continue; }
                plans.Add(best);
                if (best.kind == '#' && !Urb) L.cabins.Add(best.n);
                if (Urb) venDoorFaces.Add((best.n, best.cell - best.n));   // no facade strip over the doorway
            }
        report.Add($"OK  {plans.Count} spawn entrances: " + string.Join(", ", plans.GroupBy(p => KindName(p)).Select(g => g.Key + " " + g.Count())) + (plain > 0 ? $" ({plain} plain spawn points)" : ""));
        return plans;
    }

    static string KindName(EntrancePlan p)
    {
        switch (p.kind)
        {
            case '#': return CabinSign(p.zone).Length > 0 ? CabinSign(p.zone) : "Cabina";
            case 'R': case 'Z': return "Grieta";
            case '.': return "Escalerilla";
            case 'W': return "Autobus";
            case 'h': return "Caseta";
            case 'o': return p.zone == 'k' ? "Tienda" : p.zone == 'm' ? "Puesto" : p.zone == 'd' ? "Contenedor" : "Objeto";
            default: return "Entrada";
        }
    }

    static string CabinSign(char zone)
    {
        switch (zone)
        {
            case 'c': case 'v': return "WC";
            case 'k': return "DUCHAS";
            case 'p': return "TAQUILLA";
            case 'f': return "ALMACÉN";
            case 'l': return "CASETA";
            case 'm': return "TRASTIENDA";
            case 'd': return "LONJA";
            default: return "";
        }
    }

    // one colour for every spawn entrance (portaloo green), different from door cloths and towels
    public static readonly Color SpawnColor = new Color(0.1f, 0.52f, 0.3f);

    static void BuildEntrances(Transform root, Layout L, Dictionary<char, Zone> zones, List<EntrancePlan> plans)
    {
        var cabinRoot = Child(root, "Cabinas", Vector3.zero).transform;
        // every cabin is built once, with a doorway on each face that has an entrance
        foreach (var g in plans.Where(p => p.kind == '#' && !Urb).GroupBy(p => p.n))
            BuildCabin(cabinRoot, World(g.Key.x, g.Key.y), g.First().zone, g.Select(p => Flat(World(p.cell.x, p.cell.y) - World(p.n.x, p.n.y))).Select(v => v.normalized).ToList());
        var black = M("Oscuridad", new Color(0.02f, 0.02f, 0.025f));
        var rock = Shared("M_Rock", new Color(0.45f, 0.41f, 0.37f));
        var temple = Shared("M_TempleDark", new Color(0.35f, 0.33f, 0.3f));
        var inox = M("Inox", new Color(0.75f, 0.77f, 0.8f));
        var white = M("CartelBlanco", new Color(0.95f, 0.95f, 0.92f));
        var paint = M("Spawn_Pintura", SpawnColor);           // fences / rails of every entrance
        var doorMat = M("Spawn_Puerta", SpawnColor * 0.8f);
        int i = 0;
        foreach (var pl in plans)
        {
            Vector3 ps = World(pl.cell.x, pl.cell.y), pn = World(pl.n.x, pl.n.y);
            Vector3 d = Flat(pn - ps).normalized;        // towards the place he comes out of
            Vector3 B = ps + d * (Cell / 2f);            // the boundary between the two cells
            Quaternion R = Quaternion.LookRotation(-d);  // entrance faces the floor
            Vector3 right = R * Vector3.right;
            var go = Child(zones[pl.zone].transform.Find("SpawnPoints"), $"Entrada{i++:00}_{KindName(pl)}", B);
            go.transform.rotation = R;
            var t = go.transform;
            var exit = Child(t, "Exit", ps - d * 0.15f).transform;
            Transform inside, barrier, door = null;
            float barrierHeight = 0.6f, walkTime = 0.45f, climbTime = 0.55f;
            SpawnEntrance.Kind kind;

            switch (Urb && pl.kind == '#' ? 'Z' : pl.kind)   // Venice: a dark doorway in the facade instead of a cabin
            {
                case '#':
                {
                    // visible in the doorway as soon as the door opens: walk out, step over the planks (~1.2 s)
                    kind = SpawnEntrance.Kind.Cabina;
                    inside = Child(t, "Inside", B + d * 0.6f).transform;
                    barrier = Child(t, "Barrier", B + d * 0.1f).transform;
                    walkTime = 0.4f; climbTime = 0.6f;
                    var hinge = Child(t, "Door", B - right * 0.52f - d * 0.05f).transform;
                    hinge.rotation = R;
                    door = hinge;
                    Box(hinge, "Puerta", hinge.position + right * 0.52f, new Vector3(1.02f, 1.98f, 0.05f), doorMat, 0.02f, false);
                    Box(hinge, "Pomo", hinge.position + right * 0.88f - d * 0.04f, new Vector3(0.06f, 0.06f, 0.06f), inox, 1.0f, false);
                    string sign = CabinSign(pl.zone);
                    if (sign.Length > 0)
                    {
                        Box(t, "Cartel", B - d * 0.04f, new Vector3(1.0f, 0.34f, 0.04f), white, 2.12f, false);
                        var label = WorldLabel.Create(t, Vector3.zero, sign, 0.28f, new Color(0.1f, 0.15f, 0.35f));
                        label.transform.position = B - d * 0.15f + Vector3.up * 2.29f;
                    }
                    Box(t, "Valla", B - d * 0.08f + Vector3.up * 0f, new Vector3(1.1f, 0.12f, 0.05f), paint, 0.42f, false);
                    Box(t, "Valla", B - d * 0.08f, new Vector3(1.1f, 0.12f, 0.05f), paint, 0.15f, false);
                    break;
                }
                case 'R':
                case 'Z':
                {
                    // comes out of the rock face, then climbs the rubble (~0.9 s in sight)
                    kind = SpawnEntrance.Kind.Grieta;
                    var frame = Urb ? M("VN_Istria", new Color(0.9f, 0.88f, 0.82f)) : pl.kind == 'Z' ? temple : rock;
                    inside = Child(t, "Inside", B + d * 0.35f).transform;
                    barrier = Child(t, "Barrier", B - d * 0.2f).transform;
                    barrierHeight = 0.45f; walkTime = 0.5f; climbTime = 0.7f;
                    Box(t, "Oscuridad", B - d * 0.012f, new Vector3(1.15f, 1.95f, 0.01f), black, 0.02f, false);
                    foreach (float side in new[] { -1f, 1f })
                        TexBox(t, "Marco", B + right * side * 0.72f - d * 0.08f, new Vector3(0.32f, 2.2f, 0.18f), frame, frame, 0f, false);
                    TexBox(t, "Dintel", B - d * 0.08f, new Vector3(1.8f, 0.35f, 0.2f), frame, frame, 1.95f, false);
                    if (Urb)   // B5: a Venetian doorway: flood board (paratia) to climb over and a metal shutter half up, in the spawn colour
                    {
                        Box(t, "Paratia", B - d * 0.2f, new Vector3(1.12f, 0.45f, 0.06f), paint, 0f, false);
                        foreach (float side in new[] { -1f, 1f }) Box(t, "Guia", B - d * 0.2f + right * side * 0.58f, new Vector3(0.06f, 0.55f, 0.1f), inox, 0f, false);
                        Box(t, "Serranda", B - d * 0.05f, new Vector3(1.15f, 0.62f, 0.04f), doorMat, 1.33f, false);
                        for (int k = 0; k < 4; k++) Box(t, "Serranda_Lama", B - d * 0.075f, new Vector3(1.15f, 0.025f, 0.02f), paint, 1.38f + k * 0.15f, false);
                        break;
                    }
                    for (int k = 0; k < 3; k++)   // rubble across the mouth
                        TexBox(t, "Escombro", B + right * (k - 1) * 0.38f - d * 0.12f, new Vector3(0.36f, 0.3f + Hash(pl.cell.x, pl.cell.y, k) * 0.2f, 0.3f), frame, frame, 0f, false);
                    Box(t, "Valla", B - d * 0.3f, new Vector3(1.1f, 0.1f, 0.05f), paint, 0.42f, false);   // the spawn colour on every entrance
                    foreach (float side in new[] { -1f, 1f }) Box(t, "Poste", B - d * 0.3f + right * side * 0.55f, new Vector3(0.08f, 0.52f, 0.08f), paint, 0f, false);
                    break;
                }
                case '.':
                {
                    // head out of the water, up the ladder, over the handrails (~1.3 s in sight)
                    kind = SpawnEntrance.Kind.Escalerilla;
                    inside = Child(t, "Inside", B + d * 0.75f + Vector3.down * 1.7f).transform;
                    barrier = Child(t, "Barrier", B + d * 0.12f + Vector3.down * 0.05f).transform;
                    barrierHeight = 0.55f; walkTime = 0.8f; climbTime = 0.5f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var rail = TestMapSetup.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.06f, 1.35f, 0.06f), paint);
                        rail.transform.position = B + right * side * 0.3f + d * 0.1f + Vector3.up * -0.45f;
                        var top = TestMapSetup.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.06f, 0.25f, 0.06f), paint);
                        top.transform.position = B + right * side * 0.3f - d * 0.12f + Vector3.up * 0.9f;
                        top.transform.rotation = Quaternion.FromToRotation(Vector3.up, d);
                        var down = TestMapSetup.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.06f, 0.35f, 0.06f), paint);
                        down.transform.position = B + right * side * 0.3f - d * 0.34f + Vector3.up * 0.6f;
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        var rung = TestMapSetup.Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.6f, 0.04f, 0.05f), inox);
                        rung.transform.SetPositionAndRotation(B + d * 0.1f + Vector3.up * (-0.25f - k * 0.4f), R);
                    }
                    break;
                }
                default:   // props: bus, hut, tent, stall, container, rocks
                {
                    // pushes out of the bus / tent / stall and climbs the little fence (~0.9 s in sight)
                    kind = SpawnEntrance.Kind.Objeto;
                    inside = Child(t, "Inside", B + d * 0.45f).transform;
                    barrier = Child(t, "Barrier", B - d * 0.2f).transform;
                    barrierHeight = 0.45f; walkTime = 0.5f; climbTime = 0.7f;
                    Box(t, "Valla", B - d * 0.1f, new Vector3(1.0f, 0.1f, 0.05f), paint, 0.35f, false);
                    foreach (float side in new[] { -1f, 1f }) Box(t, "Poste", B - d * 0.1f + right * side * 0.5f, new Vector3(0.08f, 0.5f, 0.08f), paint, 0f, false);
                    if (pl.kind == 'W')
                    {
                        var label = WorldLabel.Create(t, Vector3.zero, "IMSERSO", 0.3f, new Color(1f, 0.85f, 0.2f));
                        label.transform.position = B + Vector3.up * 3.3f;
                    }
                    break;
                }
            }

            var se = go.AddComponent<SpawnEntrance>();
            var so = new SerializedObject(se);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("inside").objectReferenceValue = inside;
            so.FindProperty("barrier").objectReferenceValue = barrier;
            so.FindProperty("exit").objectReferenceValue = exit;
            so.FindProperty("door").objectReferenceValue = door;
            so.FindProperty("barrierHeight").floatValue = barrierHeight;
            so.FindProperty("walkTime").floatValue = walkTime;
            so.FindProperty("climbTime").floatValue = climbTime;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // A cabin / shed filling one wall cell: thin walls with a doorway on every face in `doorFaces` (unit
    // vectors from the centre towards the floor side), a dark partition behind the doorways and a roof.
    // Solid like the wall for everyone (one Wall-layer box over the whole cell); the doors are per entrance.
    static void BuildCabin(Transform parent, Vector3 center, char zone, List<Vector3> doorFaces)
    {
        var body = M("Cabina_Spawn", SpawnColor);
        var roof = M("Cabina_Techo", new Color(0.25f, 0.25f, 0.28f));
        var dark = M("Cabina_Interior", new Color(0.05f, 0.05f, 0.06f));
        const float h = 2.7f, wall = 0.12f, doorW = 1.05f, doorH = 2.0f;
        var cab = Child(parent, "Cabina_" + zone, center);
        cab.layer = TestMapSetup.WallLayer;
        var box = cab.AddComponent<BoxCollider>();
        box.size = new Vector3(Cell, ColliderHeight, Cell);
        box.center = new Vector3(0f, ColliderHeight / 2f - 0.5f, 0f);
        foreach (var f in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
        {
            Vector3 side = Vector3.Cross(Vector3.up, f);   // along the face
            Vector3 faceC = center + f * (Cell / 2f - wall / 2f);
            Vector3 Size(float along, float height) => side.x != 0f ? new Vector3(along, height, wall) : new Vector3(wall, height, along);
            bool hasDoor = doorFaces.Any(v => Vector3.Dot(v, f) > 0.9f);
            if (!hasDoor) { TexBox(cab.transform, "Pared", faceC, Size(Cell, h), body, body, 0f, false); continue; }
            float piece = (Cell - doorW) / 2f;
            foreach (float sgn in new[] { -1f, 1f })
                TexBox(cab.transform, "Pared", faceC + side * sgn * (doorW / 2f + piece / 2f), Size(piece, h), body, body, 0f, false);
            TexBox(cab.transform, "Dintel", faceC, Size(doorW, h - doorH), body, body, doorH, false);
            // dark partition across the middle so the inside reads as a dark doorway
            TexBox(cab.transform, "Fondo", center - f * 0.05f, Size(Cell - 2f * wall, h), dark, dark, 0f, false);
            TexBox(cab.transform, "Suelo", center + f * (Cell / 4f), Size(Cell - 2f * wall, 0.02f) + (side.x != 0f ? new Vector3(0f, 0f, Cell / 2f - wall) : new Vector3(Cell / 2f - wall, 0f, 0f)), dark, dark, 0f, false);
        }
        TexBox(cab.transform, "Tejado", center, new Vector3(Cell + 0.3f, 0.15f, Cell + 0.3f), roof, roof, h, false);
    }

    // ------------------------------------------------------------------ spawn coverage ("no safe spots")
    // For every floor cell: from how many directions (8 sectors) can old men arrive, counting the entrances
    // whose walking path to the cell is at most CoverageRange metres (all doors open)? A cell reached from
    // a single direction is a safe spot (you watch one lane and see everything coming); 2 directions is a
    // lane where they come from both ends (front and back), fine for corridors.
    const float CoverageRange = 24f;

    static List<string> SpawnCoverage(Layout L, Dictionary<char, Zone> zones)
    {
        var report = new List<string>();
        var exits = new List<Vector3>();
        foreach (var z in zones.Values)
            foreach (var sp in z.SpawnPoints)
            {
                var e = sp.GetComponent<SpawnEntrance>();
                exits.Add(e != null ? e.ExitPoint : sp.position);
            }
        var filter = NavAgents.Filter(0);
        var path = new NavMeshPath();
        var worst = new List<string>();
        int totalCells = 0, totalSafe = 0;
        foreach (var zd in L.zones)
        {
            int cells = 0, safe = 0, two = 0, sumSectors = 0;
            for (int r = 0; r < L.H; r++)
                for (int c = 0; c < L.W; c++)
                {
                    if (!IsFloor(L, L.At(c, r)) || L.cellZone[c, r] != zd.letter) continue;
                    Vector3 p = World(c, r);
                    if (!NavMesh.SamplePosition(p, out var hp, 0.9f, filter)) continue;   // a prop / machine stands here
                    cells++;
                    int sectors = 0;
                    foreach (var ex in exits)
                    {
                        if ((ex - p).sqrMagnitude > CoverageRange * CoverageRange) continue;
                        if (!NavMesh.SamplePosition(ex, out var he, 2f, filter)) continue;
                        if (!NavMesh.CalculatePath(he.position, hp.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        float len = 0f;
                        for (int k = 1; k < path.corners.Length; k++) len += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                        if (len > CoverageRange) continue;
                        // direction the old man arrives from = last path segment, reversed
                        Vector3 from = path.corners.Length >= 2 ? path.corners[path.corners.Length - 2] : he.position;
                        Vector3 dir = from - hp.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude < 0.01f) continue;
                        int sector = Mathf.FloorToInt((Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg + 360f + 22.5f) % 360f / 45f);
                        sectors |= 1 << sector;
                    }
                    int n = 0;
                    for (int k = 0; k < 8; k++) if ((sectors & (1 << k)) != 0) n++;
                    sumSectors += n;
                    if (n <= 1) { safe++; if (worst.Count < 40) worst.Add($"{zd.id}({c},{r}):{n}"); }
                    else if (n == 2) two++;
                }
            totalCells += cells;
            totalSafe += safe;
            int entrances = zones[zd.letter].SpawnPoints.Count();
            report.Add($"{(safe * 20 <= cells ? "OK " : "WARN")} coverage {zd.id}: {entrances} entrances, {cells} cells, avg {(cells > 0 ? (float)sumSectors / cells : 0f):F1} directions, {safe} safe cells (1 direction), {two} with 2");
        }
        report.Add($"Coverage total: {totalSafe}/{totalCells} safe cells (old men from a single direction)" + (worst.Count > 0 ? ": " + string.Join(" ", worst.Take(20)) : ""));
        return report;
    }
}
