// Isla Zombie setup, theme "venecia": the map "La Serenisima" (Venice + the Monumental cemetery).
// Same layout format and checks as Isla del Faro; this file only changes what the cells LOOK like and adds the
// open-water rules:
//   '.' inside the map = canals. No invisible coast walls: Zones/CanalRescue puts back whoever falls in (or lands
//       in a barrio that is still closed).
//   '#' = buildings: a plaster box per rectangle + 2 m facade strips (VN_Fachada_*) on every face that can be seen,
//       tiled roof, chimneys.   'Z' = monument stone: mausoleums (dome + temple fronts), campanile, gate towers.
//   doors = bridges (VN_PortalPuente); every cell with water on two opposite sides gets balustrades.
// The pieces come from Tools/Blender/make_venecia_props.py (Assets/Maps/Venecia/Models); their origin is the
// centre of the footprint on the ground and their front is +Z, so VenPlace puts the origin where it is told.
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class IslaZombieSetup
{
    public static readonly MapConfig Venecia = new MapConfig { name = "La Serenísima", folder = "Assets/Maps/Venecia", file = "Venecia", scene = "Venecia", originCol = 34, originRow = 24, isla = false, menu = true, theme = "venecia" };

    static bool Ven => cfg.theme == "venecia";
    const string VnModels = "Assets/Maps/Venecia/Models/";
    const float CanalY = -0.8f;
    static GameObject VN(string name) => Model(VnModels + name + ".fbx");
    static readonly HashSet<(Vector2Int wall, Vector2Int dir)> venDoorFaces = new HashSet<(Vector2Int, Vector2Int)>();   // faces with a spawn doorway

    [MenuItem("Tools/Island/Setup La Serenisima")]
    public static void SetupVenecia() => Setup(Venecia);

    // Model origin at `pos` (no bounds recentring: the strips hang on a wall plane), no colliders.
    static GameObject VenPlace(Transform parent, GameObject prefab, Vector3 pos, float rotY, float scale = 1f)
    {
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotY, 0f));
        go.transform.localScale = Vector3.one * scale;
        foreach (var col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
        return go;
    }

    static Vector3 GridDir(Vector2Int d) => new Vector3(d.x, 0f, -d.y);          // grid +row = world -z
    static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;  // model front (+Z) towards dir
    static bool IsWater(Layout L, int c, int r) => L.At(c, r) == '.';
    static bool VenWalk(Layout L, int c, int r) => IsFloor(L, L.At(c, r)) || IsDoor(L.At(c, r));

    // B4: the top (level >= 1, flat) cells of the big humped bridges that cross a canal are an open arch: a deck slab
    // instead of a block down to the canal bed. Water, the swim floor and the gondola routes go under them.
    static bool VenArched(Layout L, int c, int r)
    {
        if (!Ven || !L.InGrid(c, r) || !VenWalk(L, c, r) || L.IsRamp(c, r) || L.floorY[c, r] < LevelH - 0.1f) return false;
        bool Open(Vector2Int d) => IsWater(L, c + d.x, r + d.y) || VenWalk(L, c + d.x, r + d.y) && !L.IsRamp(c + d.x, r + d.y)
            && Mathf.Abs(L.floorY[c + d.x, r + d.y] - L.floorY[c, r]) < 0.1f && IsWater(L, c + 2 * d.x, r + 2 * d.y);
        return Open(Vector2Int.up) && Open(Vector2Int.down) || Open(Vector2Int.left) && Open(Vector2Int.right);
    }
    static bool VenWet(Layout L, int c, int r) => IsWater(L, c, r) || VenArched(L, c, r);
    const float ArchDeck = 0.3f;   // slab thickness of an arched bridge top

    // Under an arch cell: the space between a half circle (radius Cell/2, its top at the slab) and the slab, extruded
    // one cell along the canal. Submesh 0 = the vault (brick), 1 = the faces (white stone) at the open ends.
    // No collider: it is above any passenger (the gondola ducks under the slab anyway).
    static Mesh VaultMesh(float top, Vector3 along, Vector3 across, bool faceFront, bool faceBack)
    {
        const int seg = 10;
        float rad = Cell / 2f, spring = top - rad;
        var v = new List<Vector3>();
        var vault = new List<int>();
        var faces = new List<int>();
        Vector3 Arc(int i, float l) { float a = Mathf.PI * i / seg; return across * (Mathf.Cos(a) * rad) + Vector3.up * (spring + Mathf.Sin(a) * rad) + along * l; }
        Vector3 Top(int i, float l) { float a = Mathf.PI * i / seg; return across * (Mathf.Cos(a) * rad) + Vector3.up * top + along * l; }
        float h = Cell / 2f;
        void Quad(List<int> into, Vector3 a, Vector3 b, Vector3 c, Vector3 d)   // both sides, own vertices each
        {
            int k = v.Count;
            v.AddRange(new[] { a, b, c, d, a, b, c, d });
            into.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3, k + 4, k + 6, k + 5, k + 4, k + 7, k + 6 });
        }
        for (int i = 0; i < seg; i++)
        {
            Quad(vault, Arc(i, -h), Arc(i + 1, -h), Arc(i + 1, h), Arc(i, h));
            if (faceFront) Quad(faces, Arc(i, h), Arc(i + 1, h), Top(i + 1, h), Top(i, h));
            if (faceBack) Quad(faces, Arc(i, -h), Arc(i + 1, -h), Top(i + 1, -h), Top(i, -h));
        }
        var m = new Mesh { name = "VN_Boveda" };
        m.SetVertices(v);
        m.subMeshCount = 2;
        m.SetTriangles(vault, 0);
        m.SetTriangles(faces, 1);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void VenBuildArches(Transform ground, Layout L)
    {
        int n = 0;
        foreach (var p in Cells(L))
        {
            if (!VenArched(L, p.x, p.y)) continue;
            var zd = L.Zone(L.cellZone[p.x, p.y]);
            var top = zd != null ? VenGroundMaterial(zd) : M("VN_Ladrillo", new Color(0.52f, 0.33f, 0.27f));
            var brick = M("VN_Ladrillo", new Color(0.52f, 0.33f, 0.27f));
            float y = L.floorY[p.x, p.y];
            TexBox(ground, $"Arco_{p.x}_{p.y}", Flat(World(p.x, p.y)), new Vector3(Cell, ArchDeck, Cell), top, brick, y - ArchDeck);
            // a round vault under the slab (brick) with a white stone face where the canal goes in / out
            bool flowZ = IsWater(L, p.x, p.y - 1) || VenArched(L, p.x, p.y - 1) && !VenArched(L, p.x - 1, p.y);
            Vector3 along = flowZ ? Vector3.forward : Vector3.right, across = flowZ ? Vector3.right : Vector3.forward;
            var nb = flowZ ? new[] { new Vector2Int(0, -1), new Vector2Int(0, 1) } : new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0) };
            var vault = Child(ground, $"Boveda_{p.x}_{p.y}", Flat(World(p.x, p.y)));
            vault.AddComponent<MeshFilter>().sharedMesh = VaultMesh(y - ArchDeck, along, across, !VenArched(L, p.x + nb[0].x, p.y + nb[0].y), !VenArched(L, p.x + nb[1].x, p.y + nb[1].y));
            vault.AddComponent<MeshRenderer>().sharedMaterials = new[] { brick, M("VN_Istria", new Color(0.9f, 0.88f, 0.82f)) };
            n++;
        }
        if (n > 0) Debug.Log($"[Venecia] {n} arched bridge cells (open underneath)");
    }

    static Material VenGroundMaterial(ZoneDef zd)
    {
        switch (zd.letter)
        {
            case 'i': return M("VN_Grava", new Color(0.63f, 0.61f, 0.55f));     // cemetery gravel
            case 'e': return M("VN_Piazza", new Color(0.76f, 0.74f, 0.68f));
            case 'd': return Shared("M_Wood", zd.color);
            default: return Shared("M_Stone", zd.color);
        }
    }

    // ------------------------------------------------------------------ buildings and monuments
    static void VenBuildSolids(Transform root, Layout L)
    {
        var sr = Child(root, "Walls", Vector3.zero).transform;
        var fac = Child(root, "Fachadas", Vector3.zero).transform;
        var roofs = Child(root, "Roofs", Vector3.zero).transform;
        var plaster = new[]
        {
            M("VN_Yeso_Rosa", new Color(0.86f, 0.6f, 0.55f)), M("VN_Yeso_Ocre", new Color(0.85f, 0.68f, 0.42f)), M("VN_Yeso_Teja", new Color(0.74f, 0.42f, 0.32f)),
            M("VN_Yeso_Crema", new Color(0.88f, 0.82f, 0.68f)), M("VN_Yeso_Salmon", new Color(0.9f, 0.66f, 0.5f)), M("VN_Yeso_Gris", new Color(0.66f, 0.62f, 0.58f)),
        };
        var awnings = new[] { M("VN_Toldo_Rojo", new Color(0.75f, 0.2f, 0.15f)), M("VN_Toldo_Verde", new Color(0.18f, 0.45f, 0.3f)), M("VN_Toldo_Azul", new Color(0.2f, 0.35f, 0.65f)), M("VN_Toldo_Crema", new Color(0.9f, 0.82f, 0.6f)) };
        var tile = M("VN_Tejas", new Color(0.7f, 0.36f, 0.24f));
        var brick = M("VN_Ladrillo", new Color(0.52f, 0.33f, 0.27f));
        var marble = M("VN_Marmol", new Color(0.82f, 0.82f, 0.8f));
        var istria = M("VN_Istria", new Color(0.9f, 0.88f, 0.82f));
        string[] strips = { "VN_Fachada_A", "VN_Fachada_C", "VN_Fachada_D", "VN_Fachada_A", "VN_Fachada_C", "VN_Fachada_B" };
        System.Func<int, int, int> top = (c, row) => Mathf.RoundToInt(SolidBase(L, c, row) * 100f);
        int n = 0;

        // is there land within `reach` cells beyond this water cell? (faces towards the open lagoon get no strips)
        bool SeenAcross(int c, int r, Vector2Int d, int reach = 9)
        {
            for (int i = 1; i <= reach; i++) if (L.InGrid(c + d.x * i, r + d.y * i) && !IsWater(L, c + d.x * i, r + d.y * i)) return true;
            return false;
        }

        foreach (var r in Rects(L, (c, row) => L.At(c, row) == '#', top))
        {
            float baseY = SolidBase(L, r.x, r.y);
            float h = 7.9f + Mathf.Floor(Hash(r.x, r.y, 21) * 3f) * 0.8f;
            var paint = plaster[(int)(Hash(r.x, r.y, 22) * 97f) % plaster.Length];
            WallBox(sr, "Casa" + (n++), Flat(RectCenter(r)), RectSize(r, baseY + h), tile, paint);
            TexBox(roofs, "Alero", Flat(RectCenter(r)), new Vector3(r.width * Cell + 0.5f, 0.22f, r.height * Cell + 0.5f), tile, tile, baseY + h, false);
            for (int k = 0; k < 1 + r.width * r.height / 6; k++)
            {
                var cc = new Vector2Int(r.x + (int)(Hash(r.x, r.y, 23 + k) * r.width), r.y + (int)(Hash(r.x, r.y, 33 + k) * r.height));
                VenPlace(roofs, VN("VN_Chimenea"), Flat(World(cc.x, cc.y)) + Vector3.up * (baseY + h + 0.2f) + new Vector3(Hash(cc.x, cc.y, 24) - 0.5f, 0f, Hash(cc.x, cc.y, 25) - 0.5f), Hash(cc.x, cc.y, 26) * 90f);
            }
            foreach (var cell in Cells(r))
                foreach (var d in Dirs)
                {
                    int nc = cell.x + d.x, nr = cell.y + d.y;
                    char nch = L.At(nc, nr);
                    if (!L.InGrid(nc, nr) || IsSolid(nch) || venDoorFaces.Contains((cell, d))) continue;
                    bool water = nch == '.';
                    if (water && !SeenAcross(nc, nr, d)) continue;
                    Vector3 wd = GridDir(d);
                    float y = water ? 0f : L.IsRamp(nc, nr) ? L.level[nc, nr] * LevelH : L.Y(nc, nr);
                    // the shop front with its awning only where the street is wide enough to stand under it
                    bool wide = !water && VenWalk(L, nc + d.x, nr + d.y);
                    int pick = (int)(Hash(cell.x * 7 + d.x, cell.y * 5 + d.y, 27) * 97f) % (wide ? strips.Length : strips.Length - 1);
                    var strip = VenPlace(fac, VN(strips[pick]), Flat(World(cell.x, cell.y)) + wd * (Cell / 2f) + Vector3.up * y, Yaw(wd));
                    Repaint(strip, "VN_AwningPaint", awnings[(int)(Hash(cell.x, cell.y, 28) * 97f) % awnings.Length]);
                }
        }

        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'Z', top))
        {
            float baseY = SolidBase(L, r.x, r.y);
            char z = L.cellZone[r.x, r.y];
            Vector3 centre = Flat(RectCenter(r));
            float side = Mathf.Min(r.width, r.height) * Cell;
            if (z == 'e')        // the campanile
            {
                WallBox(sr, "Campanile", centre, RectSize(r, baseY + 16f), istria, brick);
                VenPlace(roofs, VN("VN_CampanileTop"), centre + Vector3.up * (baseY + 16f - 0.5f * side / 6f), 0f, side / 6f);   // its floor slab flush with the shaft top (you walk on it)
            }
            else if (z == 'u')   // Arsenale gate towers
            {
                WallBox(sr, "Torre" + (n++), centre, RectSize(r, baseY + 9f), istria, brick);
                VenPlace(roofs, VN("VN_Almenas"), centre + Vector3.up * (baseY + 9f), 0f, side / 4f);
            }
            else                 // mausoleums: marble block, dome, a temple front on every side with floor before it
            {
                WallBox(sr, "Mausoleo" + (n++), centre, RectSize(r, baseY + 5.2f), marble, marble);
                VenPlace(roofs, VN("VN_Cupula"), centre + Vector3.up * (baseY + 5.2f), 0f, side / 4.4f);
                foreach (var d in Dirs)
                {
                    bool alongX = d.y != 0;
                    int len = alongX ? r.width : r.height;
                    int open = 0;
                    float y = 0f;
                    for (int i = 0; i < len; i++)
                    {
                        int c = alongX ? r.x + i : (d.x > 0 ? r.xMax : r.x - 1), row = alongX ? (d.y > 0 ? r.yMax : r.y - 1) : r.y + i;
                        if (VenWalk(L, c, row)) { open++; y = Mathf.Max(y, L.Y(c, row)); }
                    }
                    if (open == 0) continue;
                    Vector3 wd = GridDir(d);
                    VenPlace(fac, VN("VN_FrenteTemplo"), centre + wd * ((alongX ? r.height : r.width) * Cell / 2f) + Vector3.up * y, Yaw(wd), len * Cell / 4.1f);
                }
            }
        }
    }

    // ------------------------------------------------------------------ props
    const float LowCoverTop = 0.9f;

    // Cuts every box collider of a placed model at world height maxY (things above it are visual only).
    static GameObject CapColliders(GameObject go, float maxY)
    {
        if (go == null) return null;
        Physics.SyncTransforms();
        foreach (var box in go.GetComponentsInChildren<BoxCollider>())
        {
            var b = box.bounds;
            if (b.max.y <= maxY + 0.01f) continue;
            float sy = Mathf.Max(0.0001f, box.transform.lossyScale.y);
            float cut = (b.max.y - maxY) / sy;
            if (b.min.y >= maxY - 0.05f) { Object.DestroyImmediate(box); continue; }
            box.size -= new Vector3(0f, cut, 0f);
            box.center -= new Vector3(0f, cut / 2f, 0f);
        }
        return go;
    }

    static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / Cell + OriginCol), Mathf.RoundToInt(OriginRow - p.z / Cell));

    // Setup checks: every collider of a low-cover cell ('x') is jumpable (A3), and no prop collider floats in the
    // air (A1: the top crate of a stack used to hang 1 m above the others). Both maps use the second one.
    static List<string> CheckProps(Layout L, Transform root)
    {
        var rep = new List<string>();
        Physics.SyncTransforms();
        int tall = 0;
        var floating = new List<string>();
        int mask = ~(1 << 4);   // water blockers are not a floor
        foreach (var col in root.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || !col.enabled || col.gameObject.layer != NavAgents.PropLayer) continue;
            var b = col.bounds;
            var cell = CellOf(b.center);
            bool inside = cell.x >= 0 && cell.y >= 0 && cell.x < L.W && cell.y < L.H;
            if (Ven && inside && L.At(cell.x, cell.y) == 'x' && b.max.y - World(cell.x, cell.y).y > LowCoverTop + 0.02f)
            { tall++; rep.Add($"ERROR: low cover at {cell} can't be jumped ({col.name}, {b.max.y - World(cell.x, cell.y).y:0.00} m)"); }
            // floating: nothing under the bottom of the collider within 0.2 m (probe the centre and four inner corners)
            bool rests = false;
            float y0 = b.min.y + 0.03f;
            foreach (var o in new[] { Vector2.zero, new Vector2(-0.35f, -0.35f), new Vector2(0.35f, -0.35f), new Vector2(-0.35f, 0.35f), new Vector2(0.35f, 0.35f) })
            {
                var from = new Vector3(b.center.x + o.x * b.size.x, y0, b.center.z + o.y * b.size.z);
                foreach (var h in Physics.RaycastAll(from, Vector3.down, 0.25f, mask, QueryTriggerInteraction.Ignore))
                    if (h.collider != col) { rests = true; break; }
                if (rests) break;
            }
            if (!rests && b.min.y > GroundAt(b.center) + 0.2f) floating.Add($"{col.transform.parent?.name}/{col.name} {cell} +{b.min.y - GroundAt(b.center):0.0} m");
        }
        if (Ven) rep.Add(tall == 0 ? "OK  low cover: every 'x' can be jumped (<= 0.9 m)" : $"ERROR: {tall} low-cover colliders too tall");
        // A4: sweep the gondola hull (5.6 x 0.8 m) along every route and over every mooring: it must touch nothing solid
        int gMask = (1 << 0) | (1 << TestMapSetup.WallLayer) | (1 << NavAgents.PropLayer);
        var half = new Vector3(0.33f, 0.28f, GondolaHalfLength - 0.15f);
        int samples = 0, clips = 0;
        var clipNames = new List<string>();
        void Hull(Vector3 at, Quaternion rot)
        {
            samples++;
            var hits = Physics.OverlapBox(new Vector3(at.x, CanalY + 0.35f, at.z), half, rot, gMask, QueryTriggerInteraction.Ignore)
                .Where(h => h.GetComponentInParent<AcquaAlta>() == null && h.transform.parent?.name != "SueloNado").ToArray();
            if (hits.Length == 0) return;
            clips++;
            if (clipNames.Count < 8) clipNames.Add($"{hits[0].name} at {CellOf(at)}");
        }
        foreach (var gs in root.GetComponentsInChildren<GondolaStop>())
        {
            if (gs.Boat != null) Hull(gs.Boat.position, gs.Boat.rotation);
            var pts = gs.PathPoints;
            if (pts == null) continue;
            for (int i = 1; i < pts.Length; i++)
            {
                Vector3 d = pts[i] - pts[i - 1]; d.y = 0f;
                if (d.sqrMagnitude < 0.0001f || d.sqrMagnitude > 2.25f) continue;   // > 1.5 m: the start of the next route
                Hull(pts[i], Quaternion.LookRotation(d.normalized));
            }
        }
        if (Ven) rep.Add(clips == 0 ? $"OK  gondolas: the hull touches nothing on {samples} route / mooring samples" : $"WARNING: gondola hull touches something on {clips}/{samples} samples: " + string.Join("; ", clipNames));
        rep.Add(floating.Count == 0 ? "OK  no floating prop colliders" : $"WARNING: {floating.Count} prop colliders float: " + string.Join("; ", floating.Take(12)));
        return rep;
    }

    static void VenBuildProps(Transform root, Layout L)
    {
        var pr = Child(root, "Props", Vector3.zero).transform;
        var col = Child(root, "PropColliders", Vector3.zero).transform;
        var awnings = new[] { TestMapSetup.Mat("IZ_Awning_Red", new Color(0.8f, 0.2f, 0.15f)), TestMapSetup.Mat("IZ_Awning_Green", new Color(0.2f, 0.55f, 0.35f)), TestMapSetup.Mat("IZ_Awning_Blue", new Color(0.2f, 0.4f, 0.75f)), TestMapSetup.Mat("IZ_Awning_Yellow", new Color(0.95f, 0.75f, 0.2f)) };
        var floats = new[] { M("VN_Carroza_Morada", new Color(0.5f, 0.2f, 0.6f)), M("VN_Carroza_Verde", new Color(0.15f, 0.5f, 0.4f)), M("VN_Carroza_Roja", new Color(0.7f, 0.15f, 0.2f)), M("VN_Carroza_Azul", new Color(0.2f, 0.3f, 0.7f)) };

        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'o'))
        {
            char z = L.cellZone[r.x, r.y];
            if (r.width >= 2 && r.height >= 2 && z != 'r')   // the monument / well a campo turns around
            {
                PartColliders(PlaceInRect(pr, VN(z == 'g' ? "VN_Pozzo" : "VN_Monumento"), RectCenter(r), r.width * Cell - 0.3f, r.height * Cell - 0.3f));
                continue;
            }
            if (z == 'u' && r.width * r.height == 2)
            {
                PartColliders(PlaceInRect(pr, VN("VN_Caballete"), RectCenter(r), r.width * Cell - 0.1f, r.height * Cell - 0.1f));
                continue;
            }
            foreach (var cell in Cells(r))
            {
                var p = World(cell.x, cell.y);
                float h = Hash(cell.x, cell.y);
                switch (z)
                {
                    case 'r':
                        PartColliders(Repaint(PlaceInRect(pr, IZ("IZ_Puesto"), p, 1.95f, 1.95f, FaceOpen(L, cell)), "IZ_AwningPaint", awnings[(int)(h * 97) % awnings.Length]));
                        break;
                    case 'i':
                        if (L.level[cell.x, cell.y] >= 2) PartColliders(VenPlace(pr, VN("VN_TorreEspiral"), p, 0f));
                        else PartColliders(PlaceInRect(pr, VN(new[] { "VN_Tumba_C", "VN_Angel", "VN_Mausoleo" }[(cell.x * 2 + cell.y) % 3]), p, 1.9f, 1.9f, FaceOpen(L, cell)));
                        break;
                    case 'e':
                        PartColliders(PlaceInRect(pr, VN("VN_Columna"), p, 1.9f, 1.9f));
                        break;
                    case 'n':
                        PartColliders(Repaint(PlaceInRect(pr, VN("VN_Carroza"), p, 1.95f, 1.95f, FaceOpen(L, cell)), "VN_FloatPaint", floats[(int)(h * 97) % floats.Length]));
                        break;
                    default:   // crate stacks
                        CrateStack(pr, p, h);
                        PartColliders(PlaceInRect(pr, Island("Prop_Barrel"), p + new Vector3(0.45f, 0f, -0.5f), 0.7f, 0.7f));
                        break;
                }
            }
        }

        for (int row = 0; row < L.H; row++)
            for (int c = 0; c < L.W; c++)
            {
                char ch = L.At(c, row), z = L.cellZone[c, row];
                var cell = new Vector2Int(c, row);
                var p = World(c, row);
                float h = Hash(c, row, 3);
                if (ch == 'x')
                {
                    // 'x' = low cover: always jumpable (jump 1.2 m), so every collider stops at LowCoverTop (the parasol of
                    // the cafe table has none above it). Checked in VenCheckLowCover.
                    if (z == 'i') CapColliders(PartColliders(PlaceInRect(pr, VN(h < 0.5f ? "VN_Tumba_A" : "VN_Tumba_B"), p, 1.9f, 1.9f, Mathf.Floor(Hash(c, row, 4) * 4f) * 90f, LowCoverTop), NavAgents.PropLayer, 0.25f), p.y + LowCoverTop);
                    else CapColliders(PartColliders(PlaceInRect(pr, VN("VN_Mesa"), p, 1.9f, 1.9f, Mathf.Floor(Hash(c, row, 4) * 4f) * 90f, 0f, 0f, false), NavAgents.PropLayer, 0.25f), p.y + LowCoverTop);
                }
                else if (ch == 'h') PartColliders(PlaceInRect(pr, VN("VN_Mausoleo"), p, 1.95f, 1.95f, FaceOpen(L, cell)));
                else if (ch == 'A')
                {
                    VenPlace(pr, VN("VN_Cipres"), p, h * 360f, 0.9f + h * 0.3f);
                    Blocker(col, "Cipres_Tronco", p, new Vector3(0.5f, 3f, 0.5f), NavAgents.PropLayer);
                }
            }
    }

    // ------------------------------------------------------------------ decoration: quay edges, balustrades, lamps, boats
    static void VenBuildDecor(Transform root, Layout L)
    {
        var dr = Child(root, "Decor", Vector3.zero).transform;
        var floating = Child(root, "Flotantes", Vector3.zero).transform;   // rises with the acqua alta
        var istria = M("VN_Istria", new Color(0.9f, 0.88f, 0.82f));
        var sets = new Dictionary<char, (string[] names, float chance, float size)>
        {
            ['i'] = (new[] { "Flower_White", "Flower_Pink", "Prop_Candles", "Grass_Tuft_A", "Leaf_Litter_A", "Flower_Yellow" }, 0.45f, 0.6f),
            ['a'] = (new[] { "Leaf_Litter_B", "Prop_Bottle" }, 0.1f, 0.5f),
            ['r'] = (new[] { "Leaf_Litter_B", "Prop_Bottle", "Leaf_Litter_A" }, 0.15f, 0.5f),
            ['g'] = (new[] { "Leaf_Litter_B", "Prop_Bottle" }, 0.1f, 0.5f),
            ['n'] = (new[] { "Prop_Bottle", "Leaf_Litter_A" }, 0.15f, 0.5f),
            ['u'] = (new[] { "Rock_Pebble", "Leaf_Litter_B" }, 0.12f, 0.5f),
        };

        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                char ch = L.At(c, r);
                if (L.Zone(ch) != null && !L.IsRamp(c, r) && sets.TryGetValue(ch, out var set) && Hash(c, r, 11) <= set.chance)
                {
                    var name = set.names[(int)(Hash(c, r, 12) * set.names.Length) % set.names.Length];
                    PlaceInRect(dr, Island(name), World(c, r) + new Vector3((Hash(c, r, 13) - 0.5f) * 1.2f, 0f, (Hash(c, r, 14) - 0.5f) * 1.2f), set.size, set.size, Hash(c, r, 15) * 360f, 0f, 0f, false);
                }
                if (!VenWalk(L, c, r)) continue;
                foreach (var d in Dirs)
                {
                    if (!IsWater(L, c + d.x, r + d.y)) continue;
                    Vector3 wd = GridDir(d), p = World(c, r);
                    bool bridge = IsWater(L, c - d.x, r - d.y);
                    if (!bridge)
                    {
                        // white stone lip of the quay (flat: nothing to trip on) and a lamp now and then
                        Box(dr, "Bordillo", p + wd * (Cell / 2f - 0.15f), Mathf.Abs(wd.x) > 0.5f ? new Vector3(0.3f, 0.05f, Cell) : new Vector3(Cell, 0.05f, 0.3f), istria, 0f, false);
                        if (Hash(c, r, 16 + d.x * 2 + d.y) < 0.14f) VenPlace(dr, VN("VN_Farola"), p + wd * (Cell / 2f - 0.12f), Yaw(wd));
                        continue;
                    }
                    // bridge cell: balustrade on the water side (0.95 m: you can still jump over it), tilted on the arch
                    var bal = VenPlace(dr, VN("VN_Balaustrada"), p + wd * (Cell / 2f - 0.16f), Yaw(wd));
                    if (bal == null) continue;
                    if (L.IsRamp(c, r))
                    {
                        Vector3 uphill = GridDir(RampDir(L.ramp[c, r]));
                        float slope = Mathf.Atan2(LevelH, L.rampLen[c, r] * Cell) * Mathf.Rad2Deg;
                        float s = Mathf.Sign(Vector3.Dot(bal.transform.right, uphill));
                        bal.transform.rotation = Quaternion.AngleAxis(s * slope, wd) * bal.transform.rotation;
                    }
                    bal.layer = TestMapSetup.WallLayer;
                    var box = bal.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 0.475f, 0f);
                    box.size = new Vector3(2.1f, 0.95f, 0.3f);
                }
            }

        // on the water, along the quays: mooring poles, gondolas and water taxis (no colliders: nobody stands there)
        var hulls = new List<Vector3>();
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                if (!IsWater(L, c, r)) continue;
                foreach (var d in Dirs)
                {
                    if (!L.InGrid(c + d.x, r + d.y) || IsWater(L, c + d.x, r + d.y) || IsDoor(L.At(c + d.x, r + d.y))) continue;
                    Vector3 toLand = GridDir(d), p = Flat(World(c, r)) + Vector3.up * CanalY;
                    Vector3 along = new Vector3(toLand.z, 0f, -toLand.x);
                    var a = new Vector2Int(d.y, -d.x);   // grid direction along the quay
                    float h = Hash(c, r, 40 + d.x * 2 + d.y);
                    if (h < 0.3f) VenPlace(floating, VN(Hash(c, r, 41) < 0.3f ? "VN_PaloRayas" : "VN_Palo"), p + toLand * 0.45f + along * (Hash(c, r, 42) - 0.5f), Hash(c, r, 43) * 360f);
                    bool clear = true;   // room for a 9 m hull: water two cells each way, land on this side all along
                    for (int i = -2; i <= 2 && clear; i++) clear = IsWater(L, c + a.x * i, r + a.y * i) && !IsWater(L, c + a.x * i + d.x, r + a.y * i + d.y) && !VenWalk(L, c + a.x * i - d.x, r + a.y * i - d.y);
                    if (!clear || L.cellZone[c + d.x, r + d.y] == 'd' || hulls.Any(q => (q - p).sqrMagnitude < 11f * 11f)) continue;
                    float hb = Hash(c, r, 44);
                    if (hb < 0.13f) hulls.Add(p);
                    if (hb < 0.09f) VenPlace(floating, VN("VN_Gondola"), p + toLand * 0.15f, Yaw(along) + (Hash(c, r, 45) < 0.5f ? 0f : 180f));
                    else if (hb < 0.13f) VenPlace(floating, VN("VN_Lancha"), p + toLand * 0.05f, Yaw(along) + (Hash(c, r, 45) < 0.5f ? 0f : 180f));
                    break;
                }
            }
    }

    // ------------------------------------------------------------------ runtime rules of the open water
    static void VenBuildRuntime(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        var rows = new string[L.H];
        for (int r = 0; r < L.H; r++)
        {
            var sb = new System.Text.StringBuilder(L.W);
            for (int c = 0; c < L.W; c++)
            {
                char ch = L.At(c, r);
                sb.Append(ch == '.' ? '.' : IsDoor(ch) || IsSolid(ch) ? '+' : L.cellZone[c, r] == '\0' ? '.' : L.cellZone[c, r]);
            }
            rows[r] = sb.ToString();
        }
        var letters = string.Concat(L.zones.Select(z => z.letter));
        var rescue = Child(root, "CanalRescue", Vector3.zero).AddComponent<CanalRescue>();
        rescue.Configure(rows, letters, L.zones.Select(z => zones[z.letter]).ToArray(), OriginCol, OriginRow, Cell);
        EditorUtility.SetDirty(rescue);

        // acqua alta instead of night rounds: no senderistas here, the tide comes in every 5 rounds
        var sea = root.Find("Ground/Sea");
        var tide = Child(root, "AcquaAlta", Vector3.zero).AddComponent<AcquaAlta>();
        var swim = VenBuildSwimFloor(root, L);
        VenBuildBeacons(root, L);
        tide.Configure(sea, root.Find("Flotantes"), CanalY, swim, SwimY);
        EditorUtility.SetDirty(tide);
        var rm = root.GetComponentInChildren<RoundManager>();
        if (rm != null)
        {
            var so = new SerializedObject(rm);
            so.FindProperty("nightVariant").objectReferenceValue = null;
            // carnival masks first (PickVariant rolls them in order), then the usual specials
            var masks = TestMapSetup.CreateData().maskVariants;
            var sv = so.FindProperty("specialVariants");
            var old = Enumerable.Range(0, sv.arraySize).Select(i => sv.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            sv.arraySize = masks.Length + old.Count;
            for (int i = 0; i < masks.Length; i++) sv.GetArrayElementAtIndex(i).objectReferenceValue = masks[i];
            for (int i = 0; i < old.Count; i++) sv.GetArrayElementAtIndex(masks.Length + i).objectReferenceValue = old[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        VenBuildGondolas(root, L, zones);
        VenBuildTasks(root, L);
        VenBuildFences(root, L, zones);
        // the boss wears a golden mask here
        var dux = Child(root, "DuxMask", Vector3.zero).AddComponent<DuxMask>();
        dux.Configure(VN("VN_Mascara_Dux"), TestMapSetup.Mat("Gold", new Color(1f, 0.8f, 0.1f), 0.8f));
        EditorUtility.SetDirty(dux);
    }

    // Acqua alta (A7): a floor under every canal, its top SwimY (0.25 m under the quays: you walk out without
    // jumping, and the NavMesh (climb 0.45) joins it to every quay). Baked as NavMesh area "Canal" (NavAgents.CanalArea),
    // which the old men only get while it floods; AcquaAlta switches the colliders on and off. The outer ring of the
    // grid is open sea and has none.
    const float SwimY = -0.25f;

    static Transform VenBuildSwimFloor(Transform root, Layout L)
    {
        var g = Child(root, "SueloNado", Vector3.zero);
        var mod = g.AddComponent<NavMeshModifier>();
        mod.overrideArea = true;
        mod.area = NavAgents.CanalArea;
        foreach (var r in Rects(L, (c, row) => c > 0 && row > 0 && c < L.W - 1 && row < L.H - 1 && VenWet(L, c, row), (c, row) => 0))
        {
            var ctr = Flat(RectCenter(r));
            var box = Child(g.transform, "Fondo", new Vector3(ctr.x, SwimY - 0.1f, ctr.z));
            box.AddComponent<BoxCollider>().size = new Vector3(r.width * Cell, 0.2f, r.height * Cell);
        }
        return g.transform;
    }

    // Posts on every quay edge (A7): they stick out of the acqua alta, so you see where the quay ends under the water.
    // One combined mesh for the poles and one for the red tops (hundreds of posts).
    // Stylised low-poly water (user 2026-11-16: "not realistic, but better, still optimised and low poly").
    // One mesh over the whole grid, a vertex every metre (jittered away from the quays: irregular facets), plus a strip
    // out to the horizon; shader Island/LowPolyWater. Vertex colour R = distance to the nearest quay / 3 m (foam band,
    // shallow-deep), G = over a street (light and see-through when the acqua alta floods it). Lives under Ground/Sea,
    // which AcquaAlta moves up and down.
    static void VenBuildWater(Transform sea, Layout L)
    {
        const float Step = 1f, FoamRange = 3f, Horizon = 320f;
        float x0 = (0 - OriginCol) * Cell - Cell / 2f, x1 = (L.W - 1 - OriginCol) * Cell + Cell / 2f;
        float z1 = (OriginRow - 0) * Cell + Cell / 2f, z0 = (OriginRow - (L.H - 1)) * Cell - Cell / 2f;
        int nx = Mathf.RoundToInt((x1 - x0) / Step) + 1, nz = Mathf.RoundToInt((z1 - z0) / Step) + 1;

        // distance from a point to the nearest cell (land or water) within reach, capped
        float DistTo(float px, float pz, bool land, float cap)
        {
            int cc = Mathf.RoundToInt(px / Cell + OriginCol), cr = Mathf.RoundToInt(OriginRow - pz / Cell);
            int reach = Mathf.CeilToInt(cap / Cell) + 1;
            float best = cap;
            for (int r = cr - reach; r <= cr + reach; r++)
                for (int c = cc - reach; c <= cc + reach; c++)
                {
                    if (!L.InGrid(c, r) || VenWet(L, c, r) != !land) continue;
                    float cx = (c - OriginCol) * Cell, cz = (OriginRow - r) * Cell;
                    float dx = Mathf.Max(Mathf.Abs(px - cx) - Cell / 2f, 0f), dz = Mathf.Max(Mathf.Abs(pz - cz) - Cell / 2f, 0f);
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
                }
            return best;
        }

        var rnd = new System.Random(4242);
        var verts = new List<Vector3>(nx * nz + 600);
        var cols = new List<Color32>(nx * nz + 600);
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float px = x0 + i * Step, pz = z0 + j * Step;
                float toLand = DistTo(px, pz, true, FoamRange);
                bool street = DistTo(px, pz, false, 0.05f) > 0.01f;   // no water cell touches it
                bool border = i == 0 || j == 0 || i == nx - 1 || j == nz - 1;
                if (!border && !street && toLand > 0.9f)   // irregular facets, but straight foam along the quays
                {
                    px += ((float)rnd.NextDouble() - 0.5f) * 0.6f;
                    pz += ((float)rnd.NextDouble() - 0.5f) * 0.6f;
                }
                verts.Add(new Vector3(px, 0f, pz));
                cols.Add(new Color32((byte)Mathf.RoundToInt(255f * toLand / FoamRange), (byte)(street ? 255 : 0), 0, 255));
            }
        var tris = new List<int>(nx * nz * 6 + 3000);
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                // skip quads deep inside land (never seen, not even flooded: they are under the higher levels too)
                if (cols[a].g == 255 && cols[b].g == 255 && cols[c].g == 255 && cols[d].g == 255 && !LowStreet(verts[a], verts[d])) continue;
                if (((i + j) & 1) == 0) { tris.AddRange(new[] { a, c, d, a, d, b }); }
                else { tris.AddRange(new[] { a, c, b, b, c, d }); }
            }
        // a street the acqua alta can reach: level 0 floor under the quad
        bool LowStreet(Vector3 p, Vector3 q)
        {
            int c = Mathf.RoundToInt((p.x + q.x) / 2f / Cell + OriginCol), r = Mathf.RoundToInt(OriginRow - (p.z + q.z) / 2f / Cell);
            return L.InGrid(c, r) && !IsSolid(L.At(c, r)) && L.floorY[c, r] < 0.1f;
        }

        // strip to the horizon: one outer vertex per border vertex, pushed straight out (corners diagonally)
        var ring = new List<int>();
        for (int i = 0; i < nx; i++) ring.Add(i);
        for (int j = 1; j < nz; j++) ring.Add(j * nx + nx - 1);
        for (int i = nx - 2; i >= 0; i--) ring.Add((nz - 1) * nx + i);
        for (int j = nz - 2; j >= 1; j--) ring.Add(j * nx);
        int outer0 = verts.Count;
        foreach (int k in ring)
        {
            Vector3 p = verts[k];
            float ox = p.x <= x0 + 0.01f ? -1f : p.x >= x1 - 0.01f ? 1f : 0f, oz = p.z <= z0 + 0.01f ? -1f : p.z >= z1 - 0.01f ? 1f : 0f;
            verts.Add(p + new Vector3(ox, 0f, oz) * Horizon);
            cols.Add(new Color32(255, 0, 0, 255));
        }
        for (int k = 0; k < ring.Count; k++)
        {
            int a = ring[k], b = ring[(k + 1) % ring.Count], oa = outer0 + k, ob = outer0 + (k + 1) % ring.Count;
            tris.AddRange(new[] { a, oa, ob, a, ob, b });
        }
        // the ring goes round counter-clockwise seen from above only if the winding above is right: make every
        // triangle face up
        for (int t = 0; t < tris.Count; t += 3)
        {
            Vector3 n = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
            if (n.y < 0f) { int s = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = s; }
        }

        var mesh = new Mesh { name = "VN_AguaLowPoly", indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        var b2 = mesh.bounds; b2.Expand(new Vector3(0f, 0.6f, 0f)); mesh.bounds = b2;   // the waves

        var go = Child(sea, "Agua", sea.position);
        go.layer = WaterLayer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = WaterMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        Debug.Log($"[Venecia] water: {verts.Count} vertices, {tris.Count / 3} triangles");
    }

    static Material WaterMaterial()
    {
        const string path = TestMapSetup.DataRoot + "/Materials/VN_Agua.mat";
        var shader = Shader.Find("Island/LowPolyWater");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void VenBuildBeacons(Transform root, Layout L)
    {
        var poles = new List<CombineInstance>();
        var tops = new List<CombineInstance>();
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        foreach (var cell in Cells(L))
        {
            char ch = L.At(cell.x, cell.y);
            if (L.cellZone[cell.x, cell.y] == (char)0 || IsSolid(ch) || IsDoor(ch)) continue;
            Vector3 p0 = World(cell.x, cell.y);
            if (p0.y > 0.1f) continue;   // only the low floor floods
            foreach (var d in Dirs)
            {
                if (!IsWater(L, cell.x + d.x, cell.y + d.y)) continue;
                Vector3 at = Flat(p0) + GridDir(d) * (Cell / 2f + 0.12f);
                poles.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(at + Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.12f, 1.9f, 0.12f)) });
                tops.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(at + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.16f, 0.2f, 0.16f)) });
            }
        }
        var g = Child(root, "Balizas", Vector3.zero);
        void Part(string name, List<CombineInstance> list, Material m)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(list.ToArray(), true, true);
            var go = Child(g.transform, name, Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
        Part("Postes", poles, M("VN_Baliza", new Color(0.92f, 0.92f, 0.88f)));
        Part("Cabezas", tops, M("VN_BalizaRoja", new Color(0.85f, 0.12f, 0.1f)));
        Debug.Log($"[Venecia] {poles.Count} beacons on the quay edges");
    }

    // Fence obstacles (A8 + A7): the old men's NavMeshAgents ignore colliders, so each straight run of fence also
    // carves the NavMesh while it stands (they could otherwise swim into a closed barrio during the acqua alta).
    // Added after the setup checks, like the door obstacles (carving works in the editor too).
    static void VenFenceObstacles(Transform root)
    {
        var vallas = root.Find("Vallas");
        if (vallas == null) return;
        foreach (var fence in vallas.GetComponentsInChildren<ZoneFence>(true))
        {
            var tramos = fence.transform.Find("Tramos");
            if (tramos == null) continue;
            var cols = tramos.Cast<Transform>().Where(t => t.name == "Colision").ToList();
            // merge collinear neighbours: same orientation, same line, 2 m apart
            var left = new HashSet<Transform>(cols);
            while (left.Count > 0)
            {
                var a = left.First(); left.Remove(a);
                var run = new List<Transform> { a };
                Vector3 along = a.right;
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    foreach (var b in left.ToList())
                    {
                        if (Mathf.Abs(Vector3.Dot(b.right, along)) < 0.99f) continue;
                        foreach (var r in run)
                        {
                            Vector3 d = b.position - r.position;
                            if (Mathf.Abs(Vector3.Dot(d, along)) <= Cell + 0.05f && Vector3.Cross(d, along).magnitude < 0.05f)
                            { run.Add(b); left.Remove(b); grew = true; break; }
                        }
                    }
                }
                float min = run.Min(t => Vector3.Dot(t.position, along)), max = run.Max(t => Vector3.Dot(t.position, along));
                Vector3 mid = a.position + along * ((min + max) / 2f - Vector3.Dot(a.position, along));
                var ob = Child(tramos, "Obstaculo", mid);
                ob.transform.rotation = a.rotation;
                var nmo = ob.AddComponent<NavMeshObstacle>();
                nmo.shape = NavMeshObstacleShape.Box;
                nmo.size = new Vector3(max - min + Cell, 3f, 0.3f);
                nmo.carving = true;
                nmo.carveOnlyStationary = true;
            }
        }
    }

    // Closed barrios (A8, user's decision): a 3.2 m iron fence (VN_Verja) on every edge between a floor cell of the
    // zone and the canal, one group per zone (ZoneFence) that disappears when the zone opens. The start zone has none.
    // The collider (Wall layer) is 6 m tall although the model is 3.2 m: the only wall taller than it looks, so the
    // Frog's super jump (4 m) can't clear it. CanalRescue stays as the safety net.
    const float FenceColliderTop = 6f;

    static void VenBuildFences(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        var parent = Child(root, "Vallas", Vector3.zero).transform;
        int pieces = 0;
        foreach (var zd in L.zones)
        {
            if (zd.start || !zones.TryGetValue(zd.letter, out var zone)) continue;
            var g = Child(parent, "Valla_" + zd.id, Vector3.zero);
            var fence = g.AddComponent<ZoneFence>();
            fence.Configure(zone);
            var holder = Child(g.transform, "Tramos", Vector3.zero).transform;
            foreach (var cell in Cells(L))
            {
                if (L.cellZone[cell.x, cell.y] != zd.letter || IsSolid(L.At(cell.x, cell.y)) || IsDoor(L.At(cell.x, cell.y))) continue;
                foreach (var d in Dirs)
                {
                    if (!IsWater(L, cell.x + d.x, cell.y + d.y)) continue;
                    Vector3 dir = GridDir(d), p = World(cell.x, cell.y) + dir * (Cell / 2f - 0.12f);
                    var piece = VenPlace(holder, VN("VN_Verja"), p, Yaw(dir));   // the fence runs along local X
                    if (piece == null) continue;
                    piece.name = "Verja";
                    var col = new GameObject("Colision");
                    col.transform.SetParent(holder, false);
                    col.transform.SetPositionAndRotation(p + Vector3.up * FenceColliderTop / 2f, Quaternion.Euler(0f, Yaw(dir), 0f));
                    col.transform.localScale = Vector3.one;
                    col.AddComponent<BoxCollider>().size = new Vector3(2.0f, FenceColliderTop, 0.3f);
                    col.layer = TestMapSetup.WallLayer;
                    pieces++;
                }
            }
            EditorUtility.SetDirty(fence);
        }
        Debug.Log($"[Venecia] {pieces} fence pieces on closed barrios");
    }

    // The vaporetto task list (BoatMission.HasTasks): the campanile you climb to ring the bell (= dar la luz), the
    // shovel stall (S), Giuseppe's grave (F) and the golden gondolier (O). Discs (J), karaoke (K) and key (L) are the
    // usual ones. Built before the NavMesh bake.
    static void VenBuildTasks(Transform root, Layout L)
    {
        var parent = Child(root, "Tareas", Vector3.zero).transform;
        var istria = M("VN_Istria", new Color(0.9f, 0.88f, 0.82f));
        var dark = M("Oscuridad", new Color(0.02f, 0.02f, 0.025f));
        var wood = Shared("M_Wood", new Color(0.45f, 0.28f, 0.14f));
        var iron = M("VN_Hierro", new Color(0.3f, 0.3f, 0.33f));
        var gold = TestMapSetup.Mat("Gold", new Color(1f, 0.8f, 0.1f), 0.8f);
        var earth = M("VN_Tierra", new Color(0.36f, 0.26f, 0.18f));
        int gondolierSpots = 0;

        // ---- campanile: door on its west face, belfry floor on top of the shaft, the bell = the power switch
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'Z' && L.cellZone[c, row] == 'e'))
        {
            if (r.width < 3) continue;
            Vector3 centre = Flat(RectCenter(r));
            float half = r.width * Cell / 2f, topY = SolidBase(L, r.x, r.y) + 16f;
            int midRow = r.y + r.height / 2;
            Vector3 foot = World(r.x - 1, midRow);
            Vector3 face = new Vector3(centre.x - half, foot.y, foot.z);
            Box(parent, "Campanile_Puerta", face + Vector3.left * 0.02f, new Vector3(0.04f, 2.3f, 1.3f), dark, 0f, false);
            foreach (float s in new[] { -1f, 1f }) Box(parent, "Campanile_Marco", face + Vector3.left * 0.1f + Vector3.forward * s * 0.8f, new Vector3(0.25f, 2.5f, 0.3f), istria, 0f, false);
            Box(parent, "Campanile_Dintel", face + Vector3.left * 0.1f, new Vector3(0.25f, 0.35f, 1.9f), istria, 2.4f, false);
            var doorLabel = WorldLabel.Create(parent, Vector3.zero, "SUBIR AL CAMPANILE", 0.26f, new Color(1f, 0.9f, 0.5f));
            doorLabel.transform.position = face + Vector3.left * 0.3f + Vector3.up * 3.1f;
            doorLabel.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var climb = Child(parent, "Campanile", centre + Vector3.up * topY).AddComponent<TowerClimb>();
            climb.Configure(face + Vector3.left * 0.55f, centre + Vector3.up * topY + Vector3.left * (half - 1.1f));
            EditorUtility.SetDirty(climb);
            var bell = Child(parent, "Campana", centre + Vector3.up * topY).AddComponent<PowerSwitch>();
            var so = new SerializedObject(bell);
            so.FindProperty("action").stringValue = "Tocar la campana";
            so.FindProperty("interactRadius").floatValue = 3.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        MissionTask Task(string name, Vector2Int cell, MissionTask.Kind kind, out Transform before, out Transform after, bool label)
        {
            var go = Child(parent, name, World(cell.x, cell.y));
            before = Child(go.transform, "Antes", go.transform.position).transform;
            after = Child(go.transform, "Despues", go.transform.position).transform;
            TextMesh tm = label ? WorldLabel.Create(go.transform, new Vector3(0f, 2.3f, 0f), "", 0.26f, new Color(1f, 0.9f, 0.5f)) : null;
            var task = go.AddComponent<MissionTask>();
            task.Configure(kind, before.gameObject, after.gameObject, tm);
            after.gameObject.SetActive(false);
            EditorUtility.SetDirty(task);
            return task;
        }

        foreach (var cell in Cells(L))
        {
            char ch = L.At(cell.x, cell.y);
            Vector3 p = World(cell.x, cell.y);
            if (ch == 'S')        // the shovel stall: shovels leaning on the stall beside this cell
            {
                var side = Dirs.FirstOrDefault(d => L.At(cell.x + d.x, cell.y + d.y) == 'o');
                Vector3 to = side == Vector2Int.zero ? Vector3.forward : GridDir(side);
                Task("Puesto_Palas", cell, MissionTask.Kind.Shovel, out var before, out _, true);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 at = p + to * 0.8f + new Vector3(to.z, 0f, -to.x) * (i - 1) * 0.35f;
                    Box(before, "Pala_Mango", at, new Vector3(0.05f, 1.2f, 0.05f), wood, 0.25f, false);
                    Box(before, "Pala_Hoja", at, new Vector3(0.24f, 0.3f, 0.04f), iron, 0f, false).transform.rotation = Quaternion.LookRotation(to);
                }
            }
            else if (ch == 'F')   // Giuseppe: a mound with a cross; dug up = a pit and Giuseppe sitting on its edge
            {
                Task("Tumba_Giuseppe", cell, MissionTask.Kind.Grave, out var before, out var after, true);
                Box(before, "Monticulo", p, new Vector3(1.0f, 0.28f, 1.7f), earth, 0f, false);
                Box(before, "Cruz", p + Vector3.forward * 0.8f, new Vector3(0.1f, 1.1f, 0.1f), wood, 0f, false);
                Box(before, "Cruz_Brazo", p + Vector3.forward * 0.8f, new Vector3(0.6f, 0.1f, 0.1f), wood, 0.7f, false);
                Box(after, "Hoyo", p, new Vector3(1.0f, 0.03f, 1.7f), dark, 0f, false);
                Box(after, "Tierra", p + Vector3.right * 0.75f, new Vector3(0.5f, 0.35f, 1.2f), earth, 0f, false);
                VenPlace(after, VN("VN_Giuseppe"), p, FaceOpen(L, cell));   // skeleton with beret and cane climbing out
            }
            else if (ch == 'O')   // the golden gondolier: one of several dead ends / jetties, the host picks one (B2)
            {
                var gt = Task("Gondolero_De_Oro_" + gondolierSpots, cell, MissionTask.Kind.Gondolier, out var before, out var after, false);
                var spotRoot = Child(gt.transform, "Sitio", gt.transform.position);
                before.SetParent(spotRoot.transform, true); after.SetParent(spotRoot.transform, true);
                gt.SetSpot(gondolierSpots++, spotRoot);
                foreach (var t in new[] { before, after })
                {
                    Box(t, "Peana", p, new Vector3(0.9f, 0.2f, 0.9f), istria, 0f, false);
                    VenPlace(t, VN("VN_Gondolero"), p + Vector3.up * 0.2f, FaceOpen(L, cell));   // straw hat, striped shirt, oar
                }
                Blocker(spotRoot.transform, "Gondolero_Bloqueo", p, new Vector3(0.9f, 1.9f, 0.9f), NavAgents.PropLayer);
                after.gameObject.SetActive(true);   // he stays where he is, found or not
            }
        }
    }

    // Gondola stops ('N' on a quay cell): each one carries to the next stop round the map that is open. The path
    // follows the canals (BFS over water cells; it may pass a bridge cell: gondolas go under bridges).
    static void VenBuildGondolas(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        var cells = Cells(L).Where(p => L.At(p.x, p.y) == 'N').ToList();
        if (cells.Count < 2) return;
        var parent = Child(root, "Gondolas", Vector3.zero).transform;
        var floating = root.Find("Flotantes");
        Vector2 mid = new Vector2((float)cells.Average(p => p.x), (float)cells.Average(p => p.y));
        cells = cells.OrderBy(p => Mathf.Atan2(p.y - mid.y, p.x - mid.x)).ToList();
        // a bridge cell: walkable, with water on both sides across a deck at most 2 cells wide (gondolas go under it)
        int Side(int c, int r, Vector2Int d)   // walkable cells between this one and the water that way; -1 = no water there
        {
            for (int i = 1; i <= 2; i++)
            {
                if (IsWater(L, c + d.x * i, r + d.y * i)) return i - 1;
                if (!VenWalk(L, c + d.x * i, r + d.y * i)) return -1;
            }
            return -1;
        }
        bool Deck(int c, int r, Vector2Int d) { int a = Side(c, r, d), b = Side(c, r, -d); return a >= 0 && b >= 0 && a + b <= 1; }
        // A4: only open water. The bridges are solid down to the canal bed (no arch yet, see B4 in
        // Docs/VENECIA_FALLOS_TRASPASO.md), so a route under them went through the stone. Deck() stays for when they open.
        bool Passable(int c, int r) => L.InGrid(c, r) && VenWet(L, c, r);   // B4: and under the arched bridges
        bool NearBank(Vector2Int p)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (L.InGrid(p.x + dx, p.y + dy) && !VenWet(L, p.x + dx, p.y + dy)) return true;
            return false;
        }
        var water = cells.Select(p => p + Dirs.First(d => IsWater(L, p.x + d.x, p.y + d.y))).ToList();
        Vector3 W(Vector2Int p) => Flat(World(p.x, p.y)) + Vector3.up * CanalY;

        var stops = new List<GondolaStop>();
        var boats = new List<Transform>();
        var routePoints = new List<Vector3>();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 land = World(cells[i].x, cells[i].y), toWater = Flat(W(water[i]) - land).normalized;
            var go = Child(parent, "Gondola_" + L.Zone(L.cellZone[cells[i].x, cells[i].y]).id, land);
            stops.Add(go.AddComponent<GondolaStop>());
            // A4: a passenger gondola is the 9.4 m model at 0.5 (4.7 x 0.65 m). Moored along the quay if the canal has room
            // along it (water on both sides of the mooring cell), else bow out, square to the quay; poles outside the hull.
            Vector3 along = new Vector3(toWater.z, 0f, -toWater.x);
            var side = new Vector2Int(Mathf.RoundToInt(along.x), -Mathf.RoundToInt(along.z));
            bool parallel = IsWater(L, water[i].x + side.x, water[i].y + side.y) && IsWater(L, water[i].x - side.x, water[i].y - side.y);
            Vector3 quayEdge = Flat(land) + toWater * (Cell / 2f) + Vector3.up * CanalY;
            Vector3 hullAt = parallel ? quayEdge + toWater * 0.75f : quayEdge + toWater * (GondolaHalfLength + 0.35f);
            var boat = VenPlace(floating != null ? floating : parent, VN("VN_Gondola"), hullAt, Yaw(parallel ? along : -toWater), GondolaScale);
            boats.Add(boat != null ? boat.transform : null);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 pole = parallel ? quayEdge + toWater * 0.3f + along * s * (GondolaHalfLength + 0.45f) : quayEdge + toWater * 0.3f + along * s * 0.85f;
                VenPlace(floating != null ? floating : parent, VN("VN_PaloRayas"), pole, 0f);
            }
            var label = WorldLabel.Create(go.transform, new Vector3(0f, 2.4f, 0f), "GÓNDOLA", 0.3f, new Color(1f, 0.85f, 0.4f));
            label.transform.position = land + toWater * 0.7f + Vector3.up * 2.4f;
        }
        for (int i = 0; i < cells.Count; i++)
        {
            // shortest way over the canals from this stop's water cell (Dijkstra: a cell beside a quay costs more,
            // so in wide water the gondola keeps off the banks and turns in the open; B4)
            var prev = new Dictionary<Vector2Int, Vector2Int> { [water[i]] = water[i] };
            var dist = new Dictionary<Vector2Int, int> { [water[i]] = 0 };
            var open = new SortedSet<(int cost, int x, int y)> { (0, water[i].x, water[i].y) };
            while (open.Count > 0)
            {
                var top = open.Min; open.Remove(top);
                var p = new Vector2Int(top.x, top.y);
                if (top.cost > dist[p]) continue;
                foreach (var d in Dirs)
                {
                    var n = p + d;
                    if (!Passable(n.x, n.y)) continue;
                    int cost = top.cost + 10 + (NearBank(n) ? 8 : 0);
                    if (dist.TryGetValue(n, out var old) && old <= cost) continue;
                    if (dist.ContainsKey(n)) open.Remove((old, n.x, n.y));
                    dist[n] = cost; prev[n] = p;
                    open.Add((cost, n.x, n.y));
                }
            }
            var others = new List<GondolaStop>();
            var points = new List<Vector3>();
            var starts = new List<int>();
            for (int k = 1; k < cells.Count; k++)
            {
                int j = (i + k) % cells.Count;
                if (!prev.ContainsKey(water[j])) continue;
                var path = new List<Vector2Int>();
                for (var p = water[j]; p != water[i]; p = prev[p]) path.Add(p);
                path.Reverse();
                others.Add(stops[j]);
                starts.Add(points.Count);
                // A4: every cell of the path moved to the middle of the canal across its direction (the BFS hugs a quay),
                // then only the points where the direction changes, then rounded corners
                path.Insert(0, water[i]);
                var mids = new List<Vector3>();
                for (int m = 0; m < path.Count; m++)
                {
                    var dir = m + 1 < path.Count ? path[m + 1] - path[m] : path[m] - path[m - 1];
                    var n = new Vector2Int(-dir.y, dir.x);
                    int left = 0, right = 0;
                    while (right < 1 && VenWet(L, path[m].x + n.x * (right + 1), path[m].y + n.y * (right + 1))) right++;
                    while (left < 1 && VenWet(L, path[m].x - n.x * (left + 1), path[m].y - n.y * (left + 1))) left++;
                    mids.Add(W(path[m]) + GridDir(n) * ((right - left) * Cell / 2f));
                }
                // B4: pulled taut: from each point straight on to the farthest point the hull reaches without touching
                // anything (long straight runs in open water, turns only where the canal turns)
                var corners = new List<Vector3> { mids[0] };
                for (int at = 0; at < mids.Count - 1;)
                {
                    int best = at + 1;
                    for (int e = at + 2; e < mids.Count && e - best <= 8; e++) if (SegmentClear(mids[at], mids[e], 0.65f)) best = e;   // with a margin
                    corners.Add(mids[best]);
                    at = best;
                }
                var smooth = Densify(RoundCorners(corners));   // rounded corners where the hull fits (A4, B4)
                // a crossing (stops facing each other) points the hull at the quays: start and stop where it fits
                while (smooth.Count > 2 && !HullClear(smooth[0], smooth[1] - smooth[0])) smooth.RemoveAt(0);
                while (smooth.Count > 2 && !HullClear(smooth[smooth.Count - 1], smooth[smooth.Count - 1] - smooth[smooth.Count - 2])) smooth.RemoveAt(smooth.Count - 1);
                points.AddRange(smooth);
            }
            stops[i].Configure(zones[L.cellZone[cells[i].x, cells[i].y]], World(cells[i].x, cells[i].y), boats[i], others.ToArray(), points.ToArray(), starts.ToArray());
            EditorUtility.SetDirty(stops[i]);
            routePoints.AddRange(points);
        }
        // A4: decoration boats out of the way of the routes and the moored gondolas
        if (floating != null)
        {
            var keep = new HashSet<Transform>(boats.Where(b => b != null));
            var busy = new List<Bounds>();
            foreach (var b in keep) busy.Add(RenderBounds(b.gameObject));
            int removed = 0;
            foreach (Transform t in floating.Cast<Transform>().ToList())
            {
                if (keep.Contains(t) || !(t.name.StartsWith("VN_Gondola") || t.name.StartsWith("VN_Lancha"))) continue;
                var bb = RenderBounds(t.gameObject);
                bb.Expand(new Vector3(1.4f, 10f, 1.4f));
                if (routePoints.Any(q => bb.Contains(new Vector3(q.x, bb.center.y, q.z))) || busy.Any(o => o.Intersects(bb)))
                { Object.DestroyImmediate(t.gameObject); removed++; }
            }
            Debug.Log($"[Venecia] {removed} decoration boats moved out of the gondola routes");
        }
    }

    const float GondolaScale = 0.5f, GondolaHalfLength = 9.44f * GondolaScale / 2f;

    static bool SegmentClear(Vector3 a, Vector3 b, float halfWidth = 0.33f)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        for (float t = 0f; t < len; t += 0.5f) if (!HullClear(a + d * (t / len), d, halfWidth)) return false;
        return HullClear(b, d, halfWidth);
    }

    // Each corner of a taut route replaced by a short curve (3 points) when the hull fits along it.
    static List<Vector3> RoundCorners(List<Vector3> pts)
    {
        var res = new List<Vector3> { pts[0] };
        for (int i = 1; i < pts.Count - 1; i++)
        {
            float max = Mathf.Min(3f, Vector3.Distance(pts[i], pts[i - 1]) / 2.2f, Vector3.Distance(pts[i], pts[i + 1]) / 2.2f);
            bool done = false;
            foreach (float f in new[] { 1f, 0.66f, 0.4f })   // the widest curve that fits
            {
                float cut = max * f;
                if (cut < 0.3f) break;
                Vector3 a = pts[i] + (pts[i - 1] - pts[i]).normalized * cut, b = pts[i] + (pts[i + 1] - pts[i]).normalized * cut;
                Vector3 m = (a + 2f * pts[i] + b) / 4f;   // middle of the quadratic curve a - corner - b
                Vector3 q1 = (a + m) / 2f + (pts[i] - (a + b) / 2f) * 0.125f, q2 = (m + b) / 2f + (pts[i] - (a + b) / 2f) * 0.125f;
                if (!(SegmentClear(res[res.Count - 1], a) && SegmentClear(a, q1) && SegmentClear(q1, m) && SegmentClear(m, q2) && SegmentClear(q2, b))) continue;
                res.AddRange(new[] { a, q1, m, q2, b });
                done = true;
                break;
            }
            if (!done) res.Add(pts[i]);
        }
        res.Add(pts[pts.Count - 1]);
        return res;
    }

    // points every <= 1 m
    static List<Vector3> Densify(List<Vector3> cur)
    {
        var res = new List<Vector3> { cur[0] };
        for (int i = 1; i < cur.Count; i++)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(cur[i - 1], cur[i])));
            for (int k = 1; k <= n; k++) res.Add(Vector3.Lerp(cur[i - 1], cur[i], k / (float)n));
        }
        return res;
    }

    // The gondola hull (oriented along dir) at a point touches nothing solid.
    static bool HullClear(Vector3 at, Vector3 dir, float halfWidth = 0.33f)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return true;
        Physics.SyncTransforms();
        int mask = (1 << 0) | (1 << TestMapSetup.WallLayer) | (1 << NavAgents.PropLayer);
        var hits = Physics.OverlapBox(new Vector3(at.x, CanalY + 0.35f, at.z), new Vector3(halfWidth, 0.28f, GondolaHalfLength - 0.15f), Quaternion.LookRotation(dir.normalized), mask, QueryTriggerInteraction.Ignore);
        return hits.All(h => h.transform.parent != null && h.transform.parent.name == "SueloNado");
    }

    static Bounds RenderBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Chaikin corner cutting (2 passes) of a polyline; the ends stay where they are. Points every <= 1 m afterwards.
    static List<Vector3> Smooth(List<Vector3> pts)
    {
        var cur = pts;
        for (int pass = 0; pass < 2 && cur.Count > 2; pass++)
        {
            var nxt = new List<Vector3> { cur[0] };
            for (int i = 0; i < cur.Count - 1; i++)
            {
                nxt.Add(Vector3.Lerp(cur[i], cur[i + 1], 0.25f));
                nxt.Add(Vector3.Lerp(cur[i], cur[i + 1], 0.75f));
            }
            nxt.Add(cur[cur.Count - 1]);
            cur = nxt;
        }
        var res = new List<Vector3> { cur[0] };
        for (int i = 1; i < cur.Count; i++)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(cur[i - 1], cur[i])));
            for (int k = 1; k <= n; k++) res.Add(Vector3.Lerp(cur[i - 1], cur[i], k / (float)n));
        }
        return res;
    }
}
