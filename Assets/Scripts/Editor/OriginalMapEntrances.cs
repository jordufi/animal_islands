// Visible spawn entrances for the Original map: every spawn point gets a free-standing green WC cabin (the same
// colour and behaviour as the Isla del Faro entrances): the old man appears hidden inside, the door rattles and
// opens, he walks out and steps over a little fence onto the NavMesh at the spawn point (~1 s in sight).
// The cabin is a solid box on the Wall layer (so it carves the NavMesh and blocks the player). It is turned
// to the side of the spawn point that has room and faces the middle of its zone.
// Built by TestMapSetup.BuildGameScene before the zone borders / NavMesh.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OriginalMapEntrances
{
    public static string LastReport;
    static Material Paint, Body, RoofMat, DoorMat, Dark, Inox, White;
    static Transform EntradasRoot;
    public static bool Enabled = true;   // tests / diagnosis

    const float Depth = 1.1f, Width = 1.2f, Height = 2.3f;
    const float MinDryY = 0.12f;
    const float ExitFromCabin = 1.35f;   // spawn point (Exit) is this far in front of the cabin centre

    static int FreeMask => ~((1 << 2) | (1 << 4) | (1 << TestMapSetup.PlayerLayer) | (1 << TestMapSetup.EnemyLayer) | (1 << TestMapSetup.DoorLayer));

    public static int Build(Transform root, IEnumerable<Zone> zones)
    {
        if (!Enabled) { LastReport = "casetas desactivadas"; return 0; }
        LastPlain.Clear();
        Physics.SyncTransforms();
        var paint = Paint = TestMapSetup.Mat("Spawn_Pintura", IslaZombieSetup.SpawnColor);
        var body = Body = TestMapSetup.Mat("Spawn_Cabina", IslaZombieSetup.SpawnColor * 0.9f);
        var roofMat = RoofMat = TestMapSetup.Mat("Spawn_Techo", IslaZombieSetup.SpawnColor * 1.25f);
        var doorMat = DoorMat = TestMapSetup.Mat("Spawn_Puerta", IslaZombieSetup.SpawnColor * 0.8f);
        var dark = Dark = TestMapSetup.Mat("Oscuridad", new Color(0.02f, 0.02f, 0.025f));
        var inox = Inox = TestMapSetup.Mat("Inox", new Color(0.75f, 0.77f, 0.8f));
        var white = White = TestMapSetup.Mat("CartelBlanco", new Color(0.95f, 0.95f, 0.92f));
        EntradasRoot = root;

        var sb = new System.Text.StringBuilder();
        int built = 0, skipped = 0, moved = 0, n = 0;
        foreach (var zone in zones)
        {
            // mystery box spots standing in the sea (Playa/Box0) go to the nearest dry, clear ground
            foreach (var bx in zone.BoxSpots)
            {
                if (bx.position.y >= MinDryY) continue;
                Vector3 from = bx.position;
                bool ok = false;
                for (float rad = 1f; rad <= 8f && !ok; rad += 1f)
                    for (int a = 0; a < 360 && !ok; a += 30)
                    {
                        var c = from + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * rad;
                        if (!Physics.Raycast(c + Vector3.up * 30f, Vector3.down, out var gh, 60f, FreeMask & ~(1 << TestMapSetup.WallLayer), QueryTriggerInteraction.Ignore) || gh.point.y < MinDryY) continue;
                        bool clear = true;
                        foreach (var col in Physics.OverlapCapsule(gh.point + Vector3.up * 0.5f, gh.point + Vector3.up * 1.6f, 0.7f, FreeMask, QueryTriggerInteraction.Ignore))
                            if (!IsTerrain(col)) { clear = false; break; }
                        if (!clear) continue;
                        bx.position = gh.point;
                        sb.AppendLine($"caja movida a tierra firme: {zone.name}/{bx.name} {from} -> {gh.point}");
                        ok = true;
                    }
                if (!ok) sb.AppendLine($"caja en el agua sin sitio: {zone.name}/{bx.name} {from}");
            }
            var points = zone.SpawnPoints.ToList();
            if (points.Count == 0) continue;
            Vector3 centre = points.Aggregate(Vector3.zero, (a, t) => a + t.position) / points.Count;
            foreach (var t in points)
            {
                Vector3 d;
                if (!Place(t.position, centre, out d))
                {
                    // wet or cramped: look for the nearest dry spot with room and move the spawn point there
                    Vector3 from = t.position;
                    bool found = false;
                    for (float rad = 1f; rad <= 10f && !found; rad += 1f)
                        for (int a = 0; a < 360 && !found; a += 30)
                        {
                            var c = from + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * rad;
                            if (!Physics.Raycast(c + Vector3.up * 30f, Vector3.down, out var gh, 60f, FreeMask & ~(1 << TestMapSetup.WallLayer), QueryTriggerInteraction.Ignore)) continue;
                            c.y = gh.point.y;
                            if (!Place(c, centre, out d)) continue;
                            t.position = c;
                            moved++;
                            sb.AppendLine($"spawn movido a tierra firme: {zone.name}/{t.name} {from} -> {c}");
                            found = true;
                        }
                    if (!found) { skipped++; sb.AppendLine($"sin sitio para caseta: {zone.name}/{t.name} {t.position}"); continue; }
                }
                BuildCabin(root, t, d, $"Caseta_{zone.name}_{t.name}", paint, body, roofMat, doorMat, dark, inox, white);
                built++;
            }
        }
        LastReport = $"{built} casetas de entrada de abuelos, {skipped} puntos de spawn sin sitio\n" + sb;
        return built;
    }

    // The spawn point stays (it is a valid, reachable point) but loses its cabin and entrance: the old man simply
    // appears there, like before the cabins existed.
    public static void ToPlain(Transform spawn)
    {
        var zone = spawn.GetComponentInParent<Zone>();
        var cabin = GameObject.Find($"TestMap/Entradas/Caseta_{(zone != null ? zone.name : "")}_{spawn.name}");
        if (cabin != null) Object.DestroyImmediate(cabin);
        foreach (var n in new[] { "Inside", "Barrier", "Exit" })
        {
            var c = spawn.Find(n);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
        var e = spawn.GetComponent<SpawnEntrance>();
        if (e != null) Object.DestroyImmediate(e);
        LastPlain.Add(zone != null ? $"{zone.name}/{spawn.name}" : spawn.name);
    }

    public static readonly List<string> LastPlain = new List<string>();

    // Cabins (with their entrance) whose exit lies within `radius` of any of `points` go away, to unplug lanes.
    public static int RemoveNear(IEnumerable<Vector3> points, float radius)
    {
        var targets = points.ToList();
        int n = 0;
        foreach (var zone in Object.FindObjectsByType<Zone>())
            foreach (var t in zone.SpawnPoints.ToList())
            {
                var e = t.GetComponent<SpawnEntrance>();
                if (e == null) continue;
                if (!targets.Any(q => Vector3.Distance(q, e.ExitPoint) <= radius)) continue;
                LastReport += $"caseta quitada para liberar un hueco: {zone.name}/{t.name}\n";
                ToPlain(t);
                n++;
            }
        return n;
    }

    // A cabin whose body lies (even partly) in the area of another zone would let a player stand on its roof on the
    // far side of a border: turn it to another side of the spawn point that stays inside its zone, else drop the cabin.
    public static int PruneAcrossBorders(OriginalMapBorders.Result r, Zone[] zones)
    {
        var g = r.g;
        int Label(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - g.x0) / OriginalMapBorders.Cell), z = Mathf.FloorToInt((p.z - g.z0) / OriginalMapBorders.Cell);
            return x >= 0 && z >= 0 && x < g.nx && z < g.nz ? r.label[z * g.nx + x] : -1;
        }
        bool Inside(Vector3 s, Vector3 d, int zi)
        {
            var rot = Quaternion.LookRotation(d);
            Vector3 c = s - d * ExitFromCabin;
            foreach (var o in new[] { Vector3.zero, new Vector3(0.7f, 0, 0.6f), new Vector3(-0.7f, 0, 0.6f), new Vector3(0.7f, 0, -0.6f), new Vector3(-0.7f, 0, -0.6f) })
                if (Label(c + rot * o) != zi) return false;
            return Label(s) == zi;
        }
        int changed = 0;
        for (int zi = 0; zi < zones.Length; zi++)
            foreach (var t in zones[zi].SpawnPoints.ToList())
            {
                var e = t.GetComponent<SpawnEntrance>();
                if (e == null) continue;
                var cabin = GameObject.Find($"TestMap/Entradas/Caseta_{zones[zi].name}_{t.name}");
                if (cabin == null) continue;
                if (Inside(t.position, t.forward, zi)) continue;
                Vector3 s = t.position;
                Vector3 pick = Vector3.zero;
                for (int i = 0; i < 24 && pick == Vector3.zero; i++)
                {
                    float a = i * 15f * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    if (Fits(s, d) && Inside(s, d, zi)) pick = d;
                }
                ToPlain(t);
                if (pick != Vector3.zero)
                {
                    BuildCabin(EntradasRoot, t, pick, $"Caseta_{zones[zi].name}_{t.name}", Paint, Body, RoofMat, DoorMat, Dark, Inox, White);
                    LastReport += $"caseta girada para no cruzar una frontera: {zones[zi].name}/{t.name}\n";
                    LastPlain.RemoveAt(LastPlain.Count - 1);
                }
                else LastReport += $"caseta sobre una frontera, quitada (el punto sigue): {zones[zi].name}/{t.name}\n";
                changed++;
            }
        return changed;
    }

    // direction (flat unit vector) from the cabin towards the spawn point and on to the floor
    static bool Place(Vector3 s, Vector3 centre, out Vector3 best)
    {
        best = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        Vector3 toCentre = new Vector3(centre.x - s.x, 0f, centre.z - s.z);
        toCentre = toCentre.sqrMagnitude > 0.01f ? toCentre.normalized : Vector3.forward;
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            if (!Fits(s, d)) continue;
            float score = Vector3.Dot(d, toCentre);
            if (score > bestScore) { bestScore = score; best = d; }
        }
        return bestScore > float.NegativeInfinity;
    }

    static bool Fits(Vector3 s, Vector3 d)
    {
        var rot = Quaternion.LookRotation(d);
        Vector3 c = s - d * ExitFromCabin;
        // dry land (the sea is at about y = -0.05) and flat ground under the whole footprint
        float y0 = s.y;
        if (y0 < MinDryY) return false;
        foreach (var o in new[] { Vector3.zero, rot * new Vector3(0.6f, 0, 0.55f), rot * new Vector3(-0.6f, 0, 0.55f), rot * new Vector3(0.6f, 0, -0.55f), rot * new Vector3(-0.6f, 0, -0.55f) })
        {
            if (!Physics.Raycast(c + o + Vector3.up * 3f, Vector3.down, out var h, 8f, FreeMask & ~(1 << TestMapSetup.WallLayer), QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(h.point.y - y0) > 0.8f || h.point.y < MinDryY - 0.3f) return false;   // a cabin may be dug into a slope
        }
        // nothing solid in the cabin's volume (above the grass) or in the doorway lane in front of it
        foreach (var col in Physics.OverlapBox(c + Vector3.up * (0.45f + 0.95f), new Vector3(Width / 2f + 0.15f, 0.95f, Depth / 2f + 0.15f), rot, FreeMask, QueryTriggerInteraction.Ignore))
            if (!IsTerrain(col)) return false;
        foreach (var col in Physics.OverlapCapsule(s + Vector3.up * 0.5f, s + Vector3.up * 1.3f, 0.4f, FreeMask, QueryTriggerInteraction.Ignore))
            if (!IsTerrain(col)) return false;
        return true;
    }

    // the island's ground pieces (named Terrain_*, children of an Environment group) don't count as obstacles
    static bool IsTerrain(Collider c)
    {
        var t = c.transform;
        while (t.parent != null && t.parent.parent != null && t.parent.parent.name != "Environment") t = t.parent;
        return t.name.StartsWith("Terrain");
    }

    static void BuildCabin(Transform parent, Transform spawn, Vector3 d, string cabinName, Material paint, Material body, Material roofMat, Material doorMat, Material dark, Material inox, Material white)
    {
        Vector3 s = spawn.position;
        var rot = Quaternion.LookRotation(d);
        var root = new GameObject(cabinName).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(s - d * ExitFromCabin, rot);
        const float wall = 0.06f;
        // shell: back, two sides, roof (the front is the door); a dark box behind the doorway reads as the inside
        TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(0, Height / 2f, -Depth / 2f + wall / 2f), new Vector3(Width, Height, wall), body);
        foreach (float side in new[] { -1f, 1f })
            TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(side * (Width / 2f - wall / 2f), Height / 2f, 0f), new Vector3(wall, Height, Depth), body);
        TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(0, Height + 0.04f, 0f), new Vector3(Width + 0.12f, 0.1f, Depth + 0.12f), roofMat);
        TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.02f, 0f), new Vector3(Width - 0.1f, 0.04f, Depth - 0.1f), dark);
        TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(0, 1.0f, -Depth / 2f + wall + 0.03f), new Vector3(Width - 0.2f, 2.0f, 0.02f), dark);
        // door on the front face, hinged on its left edge
        var hinge = new GameObject("Door").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(-0.51f, 0f, Depth / 2f + 0.02f);
        TestMapSetup.Prim(PrimitiveType.Cube, hinge, new Vector3(0.51f, 1.0f, 0f), new Vector3(1.02f, 2.0f, 0.05f), doorMat);
        TestMapSetup.Prim(PrimitiveType.Cube, hinge, new Vector3(0.88f, 1.0f, 0.04f), new Vector3(0.06f, 0.06f, 0.06f), inox);
        // WC sign above the door
        TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(0, 2.14f, Depth / 2f + 0.03f), new Vector3(1.0f, 0.3f, 0.04f), white);
        var label = WorldLabel.Create(root, Vector3.zero, "WC", 0.28f, new Color(0.1f, 0.15f, 0.35f));
        label.transform.SetPositionAndRotation(root.TransformPoint(new Vector3(0, 2.14f, Depth / 2f + 0.07f)), rot);
        // the little fence the old man steps over (spawn colour on every entrance)
        TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.4f, Depth / 2f + 0.4f), new Vector3(1.1f, 0.1f, 0.05f), paint);
        foreach (float side in new[] { -1f, 1f })
            TestMapSetup.Prim(PrimitiveType.Cube, root, new Vector3(side * 0.55f, 0.25f, Depth / 2f + 0.4f), new Vector3(0.08f, 0.5f, 0.08f), paint);
        // solid for everyone (Wall layer: carves the NavMesh, the borders see it as an obstacle)
        var solid = root.gameObject.AddComponent<BoxCollider>();
        solid.center = new Vector3(0, Height / 2f, 0);
        solid.size = new Vector3(Width, Height, Depth);
        root.gameObject.layer = TestMapSetup.WallLayer;

        // the spawn point itself: face outwards and become an entrance
        spawn.rotation = rot;
        var inside = TestMapSetup.Child(spawn, "Inside", root.position + Vector3.up * 0.05f).transform;
        var barrier = TestMapSetup.Child(spawn, "Barrier", s - d * 0.4f).transform;
        var exit = TestMapSetup.Child(spawn, "Exit", s).transform;
        var se = spawn.gameObject.AddComponent<SpawnEntrance>();
        var so = new SerializedObject(se);
        so.FindProperty("kind").enumValueIndex = (int)SpawnEntrance.Kind.Cabina;
        so.FindProperty("inside").objectReferenceValue = inside;
        so.FindProperty("barrier").objectReferenceValue = barrier;
        so.FindProperty("exit").objectReferenceValue = exit;
        so.FindProperty("door").objectReferenceValue = hinge;
        so.FindProperty("barrierHeight").floatValue = 0.5f;
        so.FindProperty("walkTime").floatValue = 0.4f;
        so.FindProperty("climbTime").floatValue = 0.6f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
