// Layout maps, part 4: real floors, one above another (Docs/PLANTAS_FORMATO.md). Each floor is a grid like the
// one-floor maps standing at its base level; the setup builds them one by one with `cur` = that floor (World() adds the
// base), then bakes one NavMesh for all of them. New pieces: slabs between floors, roofs over "techo" zones, railings
// at voids and at the edge of upper floors, lifts, one-way links between floors (chutes, slides) and service doors.
// A map without [floors] never comes here (BuildOneFloor): the five old maps build exactly as before.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class IslaZombieSetup
{
    public const int Storey = 2;            // levels per storey: 3.6 m of room + a 0.4 m slab
    const float SlabT = 0.4f;
    const char VoidCh = '~', LiftCh = '=', ServiceCh = '$';
    const int ServiceArea = 16;             // NavMesh area of the service doors: always closed for the checks

    class LiftDef { public string id, name; public Vector2Int cell; public string[] floors; }
    class LinkDef { public string id, name, kind; public (string floor, Vector2Int cell) from, to; }

    public static readonly MapConfig PruebaPlantas = new MapConfig { name = "Prueba de plantas", folder = "Assets/Maps/PruebaPlantas", file = "PruebaPlantas", scene = "PruebaPlantas", originCol = 12, originRow = 8, isla = false };

    [MenuItem("Tools/Island/Setup Prueba de plantas")]
    public static void SetupPruebaPlantas() => Setup(PruebaPlantas);

    // The two maps with floors, built without their theme for now (grey boxes): the setup checks their layouts on the
    // real NavMesh. Their themes (IslaZombieSetup.Hotel.cs / .Crucero.cs) come later (Docs/HOTEL_JUBILES_DISENO.md, CRUCERO_DISENO.md).
    public static readonly MapConfig HotelJubiles = new MapConfig { name = "Hotel Jubiles", folder = "Assets/Maps/HotelJubiles", file = "HotelJubiles", scene = "HotelJubiles", originCol = 23, originRow = 18, isla = false };
    public static readonly MapConfig Crucero = new MapConfig { name = "Crucero Reina del Imserso", folder = "Assets/Maps/Crucero", file = "Crucero", scene = "Crucero", originCol = 36, originRow = 17, isla = false };

    [MenuItem("Tools/Island/Setup Hotel Jubiles (sin tema)")]
    public static void SetupHotelJubiles() => Setup(HotelJubiles);

    [MenuItem("Tools/Island/Setup Crucero (sin tema)")]
    public static void SetupCrucero() => Setup(Crucero);

    // The floor a world point stands on (the highest floor whose base is under it).
    static Layout FloorAt(Layout L, Vector3 p)
    {
        if (L.floors == null || L.floors.Count < 2) return L;
        Layout best = L.floors[0];
        foreach (var F in L.floors) if (F.BaseY <= p.y + 0.6f) best = F;
        return best;
    }

    // ------------------------------------------------------------------ the build
    static void BuildFloors(Transform root, TestMapSetup.Data data, Layout L, List<string> report)
    {
        var floors = L.floors;
        foreach (var F in floors)
        {
            cur = F;
            report.AddRange(F.heightErrors.Select(e => $"[floor {F.floorId}] " + e));
            report.AddRange(CheckHeights(F));
        }
        report.AddRange(CheckFloorsGrid(L));
        cur = L;
        var zones = BuildZones(root, L);
        var pts = new Points();
        var doors = new List<BuiltDoor>();
        var parents = new Dictionary<Layout, Transform>();
        foreach (var F in floors)
        {
            cur = F;
            var fr = Child(root, "Planta_" + F.floorId, Vector3.zero).transform;
            parents[F] = fr;
            var entrances = PlanEntrances(F, report);
            BuildFloorGround(fr, F);
            BuildFloorSolids(fr, F);
            BuildRoofs(fr, F);
            BuildProps(fr, F);
            BuildDecor(fr, F);
            // no coast blockers: they are built round the lowest floor and would stand as invisible walls through the floors
            // above (the hotel basement); a fall off a floor is the rescue's job (Docs/PLANTAS_FORMATO.md 5)
            doors.AddRange(BuildDoors(fr, F, zones));
            MergePoints(pts, PlaceMarkers(F, zones, entrances), F);
            BuildEntrances(fr, F, zones, entrances);
            BuildRamps(fr, F, zones);
            BuildLedgeLips(fr, F);
            BuildRailings(fr, F);
            BuildServiceDoors(fr, F, zones);
        }
        cur = FloorAt(L, pts.playerSpawn);
        BuildGameplay(root, data, cur, zones, pts, doors);
        foreach (var F in floors) { cur = F; BuildLabels(parents[F], F, pts); }
        report.Add(BuildLifts(root, L, zones));
        var floorLinks = BuildFloorLinks(root, L, zones);

        Physics.SyncTransforms();
        cur = FloorAt(L, pts.playerSpawn);
        BakeNavMesh(root, cur);
        int links = 0;
        foreach (var F in floors) { cur = F; links += BuildLinks(parents[F], F, zones); }
        links += BuildFloorNavLinks(root, floorLinks);
        if (links > 0) report.Add($"OK  {links} one-way NavMesh links (ledges, slides, chutes between floors)");
        cur = L;
        report.AddRange(CheckMap(L, zones, pts));
        var seen = new HashSet<string>();   // the zone-graph lines (single way out) are the same on every floor: once
        foreach (var F in floors) { cur = F; report.AddRange(CheckHeightsNav(F, zones, pts).Where(x => seen.Add(x)).Select(x => $"[floor {F.floorId}] " + x)); }
        int totalCells = 0, safe = 0;
        foreach (var F in floors)
        {
            cur = F;
            foreach (var line in SpawnCoverage(F, zones))
            {
                if (line.Contains(" 0 cells,")) continue;   // the zone is on another floor
                if (line.StartsWith("Coverage total"))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)/(\d+)");
                    if (m.Success) { safe += int.Parse(m.Groups[1].Value); totalCells += int.Parse(m.Groups[2].Value); }
                    continue;
                }
                report.Add($"[floor {F.floorId}] " + line);
            }
            report.AddRange(CheckProps(F, parents[F]));
        }
        report.Add($"Coverage total: {safe}/{totalCells} safe cells (old men from a single direction), all floors");
        report.Add(navIslandsLeft == 0 ? $"OK  NavMesh: {LastNavIslands} unreachable islands cut (slab and roof tops), none left" : $"ERROR: {navIslandsLeft} NavMesh islands still unreachable from the start");
        report.Add(CheckHeadroom(L));
        foreach (var d in doors) { TestMapSetup.AddDoorObstacle(d.door.transform.Find("Barrier").gameObject, d.width); CapDoor(d); }
        foreach (var sd in root.GetComponentsInChildren<ServiceDoor>(true)) AddServiceObstacle(sd);
        cur = L;
        report.Add(BuildFloorMaps(root, L, pts, doors, parents));
    }

    // Markers of one floor into the map's points (the single ones only from the floor that has them).
    static void MergePoints(Points dst, Points src, Layout F)
    {
        bool Has(char ch) => F.map.Any(row => row.IndexOf(ch) >= 0);
        if (Has('@')) dst.playerSpawn = src.playerSpawn;
        if (Has('b')) dst.bossSpawn = src.bossSpawn;
        if (Has('X')) dst.extraction = src.extraction;
        if (Has('@')) dst.firstBox = src.firstBox;
        dst.drinks.AddRange(src.drinks); dst.paps.AddRange(src.paps); dst.lijadoras.AddRange(src.lijadoras); dst.karaokes.AddRange(src.karaokes);
        dst.records.AddRange(src.records); dst.llaves.AddRange(src.llaves); dst.llaveZones.AddRange(src.llaveZones);
        dst.switches.AddRange(src.switches); dst.stations.AddRange(src.stations);
        dst.petrol.AddRange(src.petrol); dst.radios.AddRange(src.radios); dst.wallBuys.AddRange(src.wallBuys);
        foreach (var kv in src.probe) if (!dst.probe.ContainsKey(kv.Key)) dst.probe[kv.Key] = kv.Value;
    }

    // ------------------------------------------------------------------ grid checks across floors
    static List<string> CheckFloorsGrid(Layout L)
    {
        var report = new List<string>();
        int errors = 0;
        void Err(string s) { errors++; if (errors <= 25) report.Add("ERROR floors: " + s); }
        foreach (var F in L.floors)
        {
            var B = F.below;
            foreach (var p in Cells(F))
            {
                char ch = F.At(p.x, p.y);
                if (ch == VoidCh && (B == null || B.At(p.x, p.y) == '.')) Err($"void at {F.floorId}:{p.x},{p.y} has nothing below it");
                // headroom: a raised floor or a stair needs the floor above open over it
                if (F.above != null && IsWalk(F, p.x, p.y) && (F.IsRamp(p.x, p.y) || F.level[p.x, p.y] >= 1) && F.above.At(p.x, p.y) != VoidCh)
                    Err($"no headroom at {F.floorId}:{p.x},{p.y} (raised floor or stair under the slab of floor {F.above.floorId}: put ~ above)");
            }
        }
        foreach (var d in L.doors)
        {
            int on = L.floors.Count(F => F.map.Any(row => row.IndexOf(d.digit) >= 0));
            if (on != 1) Err($"door {d.digit} is on {on} floors (must be on one)");
        }
        foreach (var lift in L.lifts)
            foreach (var fid in lift.floors)
            {
                var F = L.Floor(fid);
                if (F == null) { Err($"lift {lift.id}: no floor {fid}"); continue; }
                int landings = Four.Count(d => IsWalk(F, lift.cell.x + d.x, lift.cell.y + d.y));
                if (F.At(lift.cell.x, lift.cell.y) != LiftCh || landings != 1) Err($"lift {lift.id} on floor {fid}: cab '{F.At(lift.cell.x, lift.cell.y)}', {landings} landings (wants 1)");
            }
        foreach (var link in L.links)
            foreach (var (fid, cell) in new[] { link.from, link.to })
            {
                var F = L.Floor(fid);
                if (F == null || !IsWalk(F, cell.x, cell.y)) Err($"link {link.id}: {fid}:{cell.x},{cell.y} is not floor");
            }
        report.Add(errors == 0 ? $"OK  floors grid: {L.floors.Count} floors ({string.Join(", ", L.floors.Select(f => f.floorId + " at " + f.BaseY + " m"))}), {L.lifts.Count} lifts, {L.links.Count} links between floors, voids and headroom OK"
                               : $"FAIL floors grid: {errors} errors");
        return report;
    }

    // After building: from every walkable cell of every floor a 2.2 m ray up hits nothing (no slab, roof or beam
    // in your face), apart from the cells of a stair (their headroom is checked on the grid).
    static string CheckHeadroom(Layout L)
    {
        Physics.SyncTransforms();
        int bad = 0, total = 0;
        var where = new List<string>();
        int mask = (1 << 0) | (1 << TestMapSetup.WallLayer) | (1 << NavAgents.PropLayer);
        foreach (var F in L.floors)
        {
            var keep = cur; cur = F;
            foreach (var p in Cells(F))
            {
                if (!IsWalk(F, p.x, p.y) || F.IsRamp(p.x, p.y)) continue;
                total++;
                var o = World(p.x, p.y) + Vector3.up * 0.3f;
                if (Physics.Raycast(o, Vector3.up, out var hit, 1.9f, mask, QueryTriggerInteraction.Ignore) && hit.collider.gameObject.layer != NavAgents.PropLayer)
                {
                    bad++;
                    if (where.Count < 12) where.Add($"{F.floorId}:{p.x},{p.y} ({hit.collider.name})");
                }
            }
            cur = keep;
        }
        return bad == 0 ? $"OK  headroom: {total} cells with 2.2 m free above" : $"ERROR headroom: {bad}/{total} cells hit something above: {string.Join(" ", where)}";
    }

    // ------------------------------------------------------------------ geometry
    // One block per (zone, level): the lowest floor from 3 m under its base (its sides are the ground), the others a
    // 0.4 m slab under the floor (the ceiling of the floor below). Never under voids or outside the building.
    static void BuildFloorGround(Transform fr, Layout F)
    {
        var gr = Child(fr, "Ground", Vector3.zero).transform;
        bool lowest = F == F.floors[0];
        float thick = lowest ? 3f : SlabT;
        var sides = lowest ? Shared("M_Rock", new Color(0.5f, 0.5f, 0.48f)) : M("Forjado", new Color(0.62f, 0.6f, 0.56f));
        foreach (var zd in F.zones)
            foreach (var r in Rects(F, (c, row) => F.cellZone[c, row] == zd.letter && F.At(c, row) != VoidCh && F.At(c, row) != '.', (c, row) => GroundLevel(F, c, row)))
            {
                int lv = GroundLevel(F, r.x, r.y);
                var ctr = RectCenter(r);
                ctr.y = F.BaseY;
                TexBox(gr, "Suelo_" + zd.id + (lv > 0 ? "_N" + lv : ""), ctr, RectSize(r, thick + lv * LevelH), GroundMaterial(zd), sides, -thick);
            }
        if (lowest && F.baseLevel == 0)
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            sea.transform.SetParent(gr, false);
            sea.transform.position = new Vector3(0f, -0.8f, 0f);
            sea.transform.localScale = new Vector3(60f, 1f, 60f);
            sea.layer = WaterLayer;
            var seaMat = AssetDatabase.LoadAssetAtPath<Material>(IslandMats + "M_Water_Ocean.mat");
            sea.GetComponent<Renderer>().sharedMaterial = seaMat != null ? seaMat : M("Sea", new Color(0.12f, 0.5f, 0.65f));
            Object.DestroyImmediate(sea.GetComponent<Collider>());
        }
    }

    // Walls stand on their floor's base. Under something of the floor above they reach into its slab (3.8 m: no
    // gap and no face in the same plane); with nothing above, the usual height and the tall collider that can't be
    // jumped. Under a floor the collider is the wall itself (a tall one would block the floor above).
    static void BuildFloorSolids(Transform fr, Layout F)
    {
        var sr = Child(fr, "Walls", Vector3.zero).transform;
        var stone = Shared("M_StoneMoss", new Color(0.5f, 0.53f, 0.47f));
        bool Covered(int c, int r) => F.above != null && F.above.At(c, r) != '.';
        float Height(int c, int r) => Covered(c, r) ? Storey * LevelH - 0.2f : SolidBase(F, c, r) + WallH;
        // the tall collider of an open-air wall stops under any higher floor over it (a balcony two floors up)
        float Top(int c, int r) => Mathf.Min(ColliderHeight, CeilingY(F, c, r) - 0.05f - F.BaseY);
        int n = 0;
        foreach (var r in Rects(F, (c, row) => (IsSolid(F.At(c, row)) || F.At(c, row) == '!') && !F.cabins.Contains(new Vector2Int(c, row)),
            (c, row) => Mathf.RoundToInt(Height(c, row) * 100f) * 2000 + (Covered(c, row) ? 0 : Mathf.RoundToInt(Top(c, row) * 10f))))
        {
            var ctr = RectCenter(r);
            ctr.y = F.BaseY;
            float h = Height(r.x, r.y);
            if (!Covered(r.x, r.y))
            {
                var wall = WallBox(sr, "Muro" + n++, ctr, RectSize(r, h), stone, stone);
                float top = Top(r.x, r.y);
                if (top < ColliderHeight)
                {
                    var box = wall.GetComponent<BoxCollider>();
                    box.size = new Vector3(box.size.x, Mathf.Max(h, top), box.size.z);
                    box.center = new Vector3(0f, (box.size.y - h) / 2f, 0f);
                }
                continue;
            }
            var go = TexBox(sr, "Muro" + n++, ctr, RectSize(r, h), stone, stone, 0f, true);
            go.layer = TestMapSetup.WallLayer;
        }
    }

    // World y where a tall collider of floor F at cell (c, r) must stop: under the slab of the first floor above that has
    // something there (voids are holes: look higher). Nothing above (or a one-floor map) = no limit.
    static float CeilingY(Layout F, int c, int r)
    {
        for (var A = F.above; A != null; A = A.above)
            if (A.At(c, r) != '.' && A.At(c, r) != VoidCh) return A.BaseY - SlabT;
        return float.PositiveInfinity;
    }

    // A door under a floor or a balcony: its barrier collider and carving obstacle (5 m) stop under it, or they would
    // block and carve the floor above.
    static void CapDoor(BuiltDoor d)
    {
        var bar = d.door.transform.Find("Barrier");
        float top = float.PositiveInfinity;
        for (int y = d.cells.yMin; y < d.cells.yMax; y++)
            for (int x = d.cells.xMin; x < d.cells.xMax; x++)
                top = Mathf.Min(top, CeilingY(d.floor, x, y));
        float h = top - 0.05f - bar.position.y;
        if (h >= 5f) return;
        var box = bar.GetComponent<BoxCollider>();
        box.size = new Vector3(box.size.x, h, box.size.z);
        box.center = new Vector3(box.center.x, h / 2f, box.center.z);
        var ob = bar.GetComponent<NavMeshObstacle>();
        if (ob == null) return;
        ob.size = new Vector3(ob.size.x, h, ob.size.z);
        ob.center = new Vector3(ob.center.x, h / 2f, ob.center.z);
    }

    // Roofs over the "techo" zones where nothing of the floor above covers them (it is the slab otherwise).
    static void BuildRoofs(Transform fr, Layout F)
    {
        var roofs = Child(fr, "Roofs", Vector3.zero).transform;
        var mat = M("Tejado", new Color(0.5f, 0.42f, 0.36f));
        foreach (var r in Rects(F, (c, row) =>
        {
            char ch = F.At(c, row);
            if (ch == '.' || ch == VoidCh || F.cellZone[c, row] == '\0') return false;
            var zd = F.Zone(F.cellZone[c, row]);
            return zd != null && zd.roofed && (F.above == null || F.above.At(c, row) == '.');
        }, (c, row) => 0))
        {
            var ctr = RectCenter(r);
            ctr.y = F.BaseY + Storey * LevelH;
            TexBox(roofs, "Techo", ctr, RectSize(r, SlabT), mat, mat, -SlabT);
        }
    }

    // A railing (1.1 m, Wall layer: it blocks the NavMesh too) on every edge between a walkable cell and a void, except
    // where a stair comes out of that void, and on every edge of an upper floor with the outside.
    static void BuildRailings(Transform fr, Layout F)
    {
        var parent = Child(fr, "Barandillas", Vector3.zero).transform;
        var mat = M("Barandilla", new Color(0.82f, 0.8f, 0.74f));
        int n = 0;
        foreach (var p in Cells(F))
        {
            if (!IsStand(F, F.At(p.x, p.y)) || F.IsRamp(p.x, p.y)) continue;
            foreach (var d in Four)
            {
                var q = p + d;
                char ch = F.At(q.x, q.y);
                bool rail = ch == VoidCh ? !IsStairArrival(F, p, q) : ch == '.' && F != F.floors[0];
                if (!rail) continue;
                Vector3 dir = new Vector3(d.x, 0f, -d.y);
                var c = World(p.x, p.y) + dir * (Cell / 2f - 0.06f);
                var go = Box(parent, "Barandilla" + n++, c, d.x != 0 ? new Vector3(0.12f, 1.1f, Cell) : new Vector3(Cell, 1.1f, 0.12f), mat, 0f, true);
                go.layer = TestMapSetup.WallLayer;
            }
        }
    }

    // p (on floor F) is where the stair under the void q comes out.
    static bool IsStairArrival(Layout F, Vector2Int p, Vector2Int q)
    {
        var B = F.below;
        if (B == null || !B.IsRamp(q.x, q.y)) return false;
        var dir = RampDir(B.ramp[q.x, q.y]);
        return B.rampIdx[q.x, q.y] == B.rampLen[q.x, q.y] - 1 && B.level[q.x, q.y] + 1 >= Storey && q + dir == p;
    }

    // Service doors ($): a closed panel (Door layer: not baked, it carves once the checks are done) on a NavMesh area
    // the checks always treat as closed.
    static void BuildServiceDoors(Transform fr, Layout F, Dictionary<char, Zone> zones)
    {
        var cells = Cells(F).Where(p => F.At(p.x, p.y) == ServiceCh).ToList();
        if (cells.Count == 0) return;
        var parent = Child(fr, "PuertasServicio", Vector3.zero).transform;
        var mat = M("PuertaServicio", new Color(0.45f, 0.25f, 0.6f));
        foreach (var p in cells)
        {
            bool alongX = IsSolid(F.At(p.x - 1, p.y)) || IsSolid(F.At(p.x + 1, p.y));   // wall runs along x: the panel too
            var pos = World(p.x, p.y);
            var go = Child(parent, $"Servicio_{p.x}_{p.y}", pos);
            var panel = Box(go.transform, "Panel", pos, alongX ? new Vector3(Cell, 2.6f, 0.15f) : new Vector3(0.15f, 2.6f, Cell), mat, 0f, true);
            panel.layer = 10;   // Door layer: not in the bake
            var side = Four.Select(d => p + d).Where(q => IsWalk(F, q.x, q.y)).Select(q => F.cellZone[q.x, q.y]).Distinct().Where(z => zones.ContainsKey(z)).Select(z => zones[z]).ToArray();
            var sd = go.AddComponent<ServiceDoor>();
            sd.Configure(panel, side);
            WorldLabel.Create(go.transform, new Vector3(0f, 2.9f, 0f), "SOLO PERSONAL", 0.22f, new Color(0.85f, 0.7f, 1f));
            var vol = Child(go.transform, "ServiceArea", pos).AddComponent<NavMeshModifierVolume>();
            vol.center = new Vector3(0f, 1.5f, 0f);
            vol.size = new Vector3(Cell - 0.1f, 4f, Cell - 0.1f);
            vol.area = ServiceArea;
        }
    }

    static void AddServiceObstacle(ServiceDoor sd)
    {
        var panel = sd.transform.Find("Panel");
        if (panel == null) return;
        var ob = panel.gameObject.AddComponent<NavMeshObstacle>();
        ob.shape = NavMeshObstacleShape.Box;
        ob.size = Vector3.one;   // the panel's scale makes it its own size
        ob.carving = true;
        ob.carveOnlyStationary = false;
    }

    // ------------------------------------------------------------------ lifts and links between floors
    static string BuildLifts(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        if (L.lifts.Count == 0) return "OK  no lifts";
        var parent = Child(root, "Ascensores", Vector3.zero).transform;
        var metal = M("Ascensor", new Color(0.7f, 0.68f, 0.62f));
        var button = M("Ascensor_Boton", new Color(0.95f, 0.75f, 0.2f));
        int built = 0;
        foreach (var def in L.lifts)
        {
            var go = Child(parent, "Ascensor_" + def.id, Vector3.zero);
            var lift = go.AddComponent<Lift>();
            var stops = new List<Transform>();
            var stopZones = new List<Zone>();
            var names = new List<string>();
            var served = def.floors.Select(L.Floor).Where(f => f != null).OrderBy(f => f.baseLevel).ToList();
            foreach (var F in served)
            {
                var keep = cur; cur = F;
                var pos = World(def.cell.x, def.cell.y);
                var landing = Four.Select(d => def.cell + d).FirstOrDefault(q => IsWalk(F, q.x, q.y));
                var toLanding = new Vector3(landing.x - def.cell.x, 0f, -(landing.y - def.cell.y));
                var stop = Child(go.transform, "Parada_" + F.floorId, pos, Mathf.Atan2(toLanding.x, toLanding.z) * Mathf.Rad2Deg);
                stops.Add(stop.transform);
                char z = F.cellZone[landing.x, landing.y];
                stopZones.Add(zones.TryGetValue(z, out var zone) ? zone : null);
                names.Add(string.IsNullOrEmpty(F.floorName) ? "planta " + F.floorId : F.floorName.ToLowerInvariant());
                Box(stop.transform, "Techo", pos + Vector3.up * 3.0f, new Vector3(Cell - 0.1f, 0.1f, Cell - 0.1f), metal, 0f, false);
                var back = -toLanding.normalized;
                var across = Vector3.Cross(Vector3.up, back);
                for (int k = 0; k < 2; k++)
                {
                    var b = Box(stop.transform, k == 0 ? "Subir" : "Bajar", pos + back * (Cell / 2f - 0.15f) + across * (k == 0 ? 0.35f : -0.35f) + Vector3.up * 1.1f, new Vector3(0.18f, 0.18f, 0.18f), button, 0f, false);
                    b.AddComponent<LiftButton>().Configure(lift, stops.Count - 1, k == 0 ? 1 : -1);
                }
                WorldLabel.Create(stop.transform, toLanding.normalized * (Cell / 2f) + Vector3.up * 2.7f, "ASCENSOR", 0.22f, new Color(1f, 0.9f, 0.6f));
                cur = keep;
            }
            lift.Configure(def.name, stops.ToArray(), stopZones.ToArray(), names.ToArray(), true, def.name.ToLowerInvariant().Contains("pulsera"));
            built++;
        }
        return $"OK  {built} lifts ({string.Join(", ", L.lifts.Select(l => l.name + ": " + string.Join("/", l.floors)))})";
    }

    static List<FloorLink> BuildFloorLinks(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        var result = new List<FloorLink>();
        if (L.links.Count == 0) return result;
        var parent = Child(root, "EnlacesPlantas", Vector3.zero).transform;
        var hatch = M("Tolva", new Color(0.25f, 0.25f, 0.28f));
        foreach (var def in L.links)
        {
            var A = L.Floor(def.from.floor);
            var B = L.Floor(def.to.floor);
            var keep = cur;
            cur = A; var a = World(def.from.cell.x, def.from.cell.y);
            cur = B; var b = World(def.to.cell.x, def.to.cell.y);
            char zb = B.cellZone[def.to.cell.x, def.to.cell.y];
            cur = keep;
            var go = Child(parent, "Enlace_" + def.id, a);
            Box(go.transform, "Boca", a + Vector3.up * 0.02f, new Vector3(1.4f, 0.04f, 1.4f), hatch, 0f, false);
            Box(go.transform, "Salida", b + Vector3.up * 0.02f, new Vector3(1.4f, 0.04f, 1.4f), hatch, 0f, false);
            string label = def.kind == "tobogan" ? "TOBOGÁN" : "TOLVA";
            WorldLabel.Create(go.transform, Vector3.up * 2.2f, label + "\n(solo bajada)", 0.22f, new Color(1f, 0.85f, 0.3f));
            var fl = go.AddComponent<FloorLink>();
            fl.Configure(a, b, zones.TryGetValue(zb, out var z) ? z : null, def.name, def.kind == "tobogan" ? 0.8f : 1.2f);
            result.Add(fl);
        }
        return result;
    }

    // After the bake: a one-way NavMeshLink (both agent types, area Tobogan) per link, so the old men follow.
    static int BuildFloorNavLinks(Transform root, List<FloorLink> links)
    {
        int n = 0;
        foreach (var fl in links)
            foreach (int agent in new[] { 0, NavAgents.JumperId })
            {
                var link = fl.gameObject.AddComponent<NavMeshLink>();
                link.agentTypeID = agent;
                link.startPoint = fl.transform.InverseTransformPoint(fl.Entry);
                link.endPoint = fl.transform.InverseTransformPoint(fl.Exit);
                link.width = 0.8f;
                link.bidirectional = false;
                link.area = SlideArea;
                link.autoUpdate = false;
                n++;
            }
        return n;
    }

    // ------------------------------------------------------------------ map pictures, one per floor
    static string BuildFloorMaps(Transform root, Layout L, Points pts, List<BuiltDoor> doors, Dictionary<Layout, Transform> parents)
    {
        int minC = int.MaxValue, maxC = 0, minR = int.MaxValue, maxR = 0;
        foreach (var F in L.floors)
            foreach (var p in Cells(F))
                if (F.At(p.x, p.y) != '.') { minC = Mathf.Min(minC, p.x); maxC = Mathf.Max(maxC, p.x); minR = Mathf.Min(minR, p.y); maxR = Mathf.Max(maxR, p.y); }
        cur = L.floors[0];
        Vector3 a = World(minC - 0.5f, maxR + 0.5f), b = World(maxC + 0.5f, minR - 0.5f);
        var center = (a + b) / 2f;
        float side = Mathf.Max(b.x - a.x, b.z - a.z) + 6f;
        Directory.CreateDirectory(SignFolder);
        var camGo = new GameObject("MapCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
        cam.orthographic = true;
        cam.orthographicSize = side / 2f;
        cam.farClipPlane = 400f;
        var shots = new Dictionary<Layout, Texture2D>();
        foreach (var F in L.floors)
        {
            foreach (var G in L.floors)
            {
                parents[G].gameObject.SetActive(G.baseLevel <= F.baseLevel);
                var labels = parents[G].Find("ZoneLabels");
                if (labels != null) labels.gameObject.SetActive(G == F);
                var roofs = parents[G].Find("Roofs");
                if (roofs != null) roofs.gameObject.SetActive(G != F);
            }
            camGo.transform.SetPositionAndRotation(new Vector3(center.x, F.BaseY + 150f, center.z), Quaternion.Euler(90f, 0f, 0f));
            string path = $"{cfg.folder}/{cfg.file}_Map_{F.floorId}.png";
            RenderToPng(cam, 1024, 1024, path);
            shots[F] = ImportTexture(path);
        }
        foreach (var G in L.floors)
        {
            parents[G].gameObject.SetActive(true);
            var labels = parents[G].Find("ZoneLabels");
            if (labels != null) labels.gameObject.SetActive(false);
            var roofs = parents[G].Find("Roofs");
            if (roofs != null) roofs.gameObject.SetActive(true);
        }
        Object.DestroyImmediate(camGo);
        var start = FloorAt(L, pts.playerSpawn);
        var info = Object.FindAnyObjectByType<MapInfo>();
        if (info != null)
        {
            var so = new SerializedObject(info);
            so.FindProperty("mapTexture").objectReferenceValue = shots[start];
            so.FindProperty("worldMin").vector2Value = new Vector2(center.x - side / 2f, center.z - side / 2f);
            so.FindProperty("worldMax").vector2Value = new Vector2(center.x + side / 2f, center.z + side / 2f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        return $"OK  {shots.Count} floor map pictures ({string.Join(", ", shots.Keys.Select(f => f.floorId))}); the pause map shows floor {start.floorId} (per-floor pause map and door signs: not done yet)";
    }
}
