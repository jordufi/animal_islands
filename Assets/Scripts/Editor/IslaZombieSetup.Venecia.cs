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
                        PartColliders(PlaceInRect(pr, Island("Prop_Crate"), p + new Vector3(-0.35f, 0f, -0.3f), 1.1f, 1.1f, h * 90f));
                        PartColliders(PlaceInRect(pr, Island("Prop_Crate"), p + new Vector3(0.4f, 0f, 0.35f), 1.0f, 1.0f, h * 200f));
                        PartColliders(PlaceInRect(pr, Island("Prop_Crate"), p + new Vector3(-0.3f, 1.05f, -0.25f), 0.9f, 0.9f, h * 300f, 0f, 1.05f));
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
                    if (z == 'i') PartColliders(PlaceInRect(pr, VN(h < 0.5f ? "VN_Tumba_A" : "VN_Tumba_B"), p, 1.9f, 1.9f, Mathf.Floor(Hash(c, row, 4) * 4f) * 90f), NavAgents.PropLayer, 0.25f);
                    else PartColliders(PlaceInRect(pr, VN("VN_Mesa"), p, 1.9f, 1.9f, Mathf.Floor(Hash(c, row, 4) * 4f) * 90f, 0f, 0f, false), NavAgents.PropLayer, 0.25f);
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
        tide.Configure(sea, root.Find("Flotantes"), CanalY);
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
        // the boss wears a golden mask here
        var dux = Child(root, "DuxMask", Vector3.zero).AddComponent<DuxMask>();
        dux.Configure(VN("VN_Mascara_Bauta"), TestMapSetup.Mat("Gold", new Color(1f, 0.8f, 0.1f), 0.8f));
        EditorUtility.SetDirty(dux);
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
        var bone = M("VN_Giuseppe", new Color(0.88f, 0.86f, 0.78f));

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
                Box(after, "Giuseppe_Cuerpo", p + Vector3.left * 0.7f, new Vector3(0.42f, 0.75f, 0.3f), bone, 0f, false);
                TestMapSetup.Prim(PrimitiveType.Sphere, after, after.InverseTransformPoint(p + Vector3.left * 0.7f + Vector3.up * 0.98f), Vector3.one * 0.38f, bone);
                Box(after, "Giuseppe_Boina", p + Vector3.left * 0.7f, new Vector3(0.42f, 0.06f, 0.42f), dark, 1.14f, false);
                Box(after, "Giuseppe_Baston", p + Vector3.left * 0.7f + Vector3.forward * 0.3f, new Vector3(0.04f, 0.8f, 0.04f), wood, 0f, false);
            }
            else if (ch == 'O')   // the golden gondolier: a gold figure with his oar, against the end of a dead end
            {
                Task("Gondolero_De_Oro", cell, MissionTask.Kind.Gondolier, out var before, out var after, false);
                foreach (var t in new[] { before, after })
                {
                    Box(t, "Peana", p, new Vector3(0.9f, 0.2f, 0.9f), istria, 0f, false);
                    Box(t, "Cuerpo", p, new Vector3(0.45f, 1.15f, 0.32f), gold, 0.2f, false);
                    TestMapSetup.Prim(PrimitiveType.Sphere, t, t.InverseTransformPoint(p + Vector3.up * 1.55f), Vector3.one * 0.36f, gold);
                    Box(t, "Sombrero", p, new Vector3(0.6f, 0.05f, 0.6f), gold, 1.72f, false);
                    Box(t, "Remo", p + Vector3.right * 0.42f, new Vector3(0.05f, 2.3f, 0.05f), gold, 0.2f, false);
                    Box(t, "Remo_Pala", p + Vector3.right * 0.42f, new Vector3(0.05f, 0.5f, 0.2f), gold, 2.3f, false);
                }
                Blocker(parent, "Gondolero_Bloqueo", p, new Vector3(0.9f, 1.9f, 0.9f), NavAgents.PropLayer);
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
        bool Passable(int c, int r) => L.InGrid(c, r) && (IsWater(L, c, r) || VenWalk(L, c, r) && (Deck(c, r, Vector2Int.right) || Deck(c, r, Vector2Int.up)));
        var water = cells.Select(p => p + Dirs.First(d => IsWater(L, p.x + d.x, p.y + d.y))).ToList();
        Vector3 W(Vector2Int p) => Flat(World(p.x, p.y)) + Vector3.up * CanalY;

        var stops = new List<GondolaStop>();
        var boats = new List<Transform>();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 land = World(cells[i].x, cells[i].y), toWater = Flat(W(water[i]) - land).normalized;
            var go = Child(parent, "Gondola_" + L.Zone(L.cellZone[cells[i].x, cells[i].y]).id, land);
            stops.Add(go.AddComponent<GondolaStop>());
            var boat = VenPlace(floating != null ? floating : parent, VN("VN_Gondola"), W(water[i]) - toWater * 0.15f, Yaw(new Vector3(toWater.z, 0f, -toWater.x)));
            boats.Add(boat != null ? boat.transform : null);
            foreach (float s in new[] { -1f, 1f })
                VenPlace(floating != null ? floating : parent, VN("VN_PaloRayas"), W(water[i]) - toWater * 0.75f + new Vector3(toWater.z, 0f, -toWater.x) * s * 0.9f, 0f);
            var label = WorldLabel.Create(go.transform, new Vector3(0f, 2.4f, 0f), "GÓNDOLA", 0.3f, new Color(1f, 0.85f, 0.4f));
            label.transform.position = land + toWater * 0.7f + Vector3.up * 2.4f;
        }
        for (int i = 0; i < cells.Count; i++)
        {
            // BFS over the canals from this stop's water cell
            var prev = new Dictionary<Vector2Int, Vector2Int> { [water[i]] = water[i] };
            var q = new Queue<Vector2Int>();
            q.Enqueue(water[i]);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = p + d;
                    if (prev.ContainsKey(n) || !Passable(n.x, n.y)) continue;
                    prev[n] = p;
                    q.Enqueue(n);
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
                for (int m = 0; m < path.Count; m++)   // corners only
                    if (m == path.Count - 1 || m == 0 || path[m] - path[m - 1] != path[m + 1] - path[m]) points.Add(W(path[m]));
            }
            stops[i].Configure(zones[L.cellZone[cells[i].x, cells[i].y]], World(cells[i].x, cells[i].y), boats[i], others.ToArray(), points.ToArray(), starts.ToArray());
            EditorUtility.SetDirty(stops[i]);
        }
    }
}
