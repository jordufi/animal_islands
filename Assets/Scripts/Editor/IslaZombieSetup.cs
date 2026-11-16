// Builds the "Isla Zombie" map in Assets/Scenes/IslaZombie.unity from the hand-edited layout file
// Assets/Maps/IslaZombie/IslaZombie_Layout.txt (Tools > Island > Setup Isla Zombie).
// 1 character of the [map] = 2 x 2 m: zones, walls, rocks, props, doors and gameplay markers (legend in
// the file). Walls / rocks collide as grid boxes; every prop model gets one box collider per mesh part
// (layer Prop), so what you see is what blocks (no invisible walls). Visuals are the low-poly models of
// Assets/Maps/IslaZombie/Models (Tools/Blender/make_map_props.py) and the island props.
// Every enemy spawn (s) becomes a SpawnEntrance built into the solid cell next to it: a WC / shed in a
// wall, a crack in the rock, a pool ladder out of the sea, or the prop itself (bus, tent, stall...).
// Two NavMeshes: the default one (props block) and "Saltador" (props ignored) for the night old men. Also renders the map picture used by the "ESTÁS AQUÍ" signs at every door and
// by the pause menu. Safe to re-run: the generated "IslaZombie" root is replaced, the NavMesh rebaked
// and the zone graph checked (zones sealed with doors closed, each door joins exactly its zones, routes).
// Reuses the builders of TestMapSetup (doors, systems, shop, karaoke).
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static partial class IslaZombieSetup
{
    // One map built from an ASCII layout. Isla del Faro, its copy with heights and the small heights test map
    // share all the code; only these paths change (Docs/FASE3_FORMATO.md).
    public class MapConfig
    {
        public string name, folder, file, scene, root = "IslaZombie";
        public int originCol = 21, originRow = 18;   // this cell is the world origin
        public bool isla = true;                     // Isla del Faro checks (named routes) and menu registration
        public bool menu;                            // other maps listed in the menu's map selector
        public string theme = "";                    // "venecia": canals, facades, bridges (IslaZombieSetup.Venecia.cs)
        public string LayoutPath => $"{folder}/{file}_Layout.txt";
        public string MapTexturePath => $"{folder}/{file}_Map.png";
        public string SignFolder => folder + "/Signs";
        public string ScenePath => $"Assets/Scenes/{scene}.unity";
        public string NavMeshPath => $"Assets/Scenes/{scene}_NavMesh.asset";
        public string JumperNavMeshPath => $"Assets/Scenes/{scene}_NavMesh_Saltador.asset";
    }

    public static readonly MapConfig Isla = new MapConfig { name = "Isla del Faro", folder = "Assets/Maps/IslaZombie", file = "IslaZombie", scene = "IslaZombie" };
    public static readonly MapConfig IslaAlturas = new MapConfig { name = "Isla del Faro (alturas)", folder = "Assets/Maps/IslaFaroAlturas", file = "IslaFaroAlturas", scene = "IslaFaroAlturas" };
    public static readonly MapConfig Prueba = new MapConfig { name = "Prueba de alturas", folder = "Assets/Maps/PruebaAlturas", file = "PruebaAlturas", scene = "PruebaAlturas", originCol = 8, originRow = 4, isla = false };

    static MapConfig cfg = Isla;
    static Layout cur;   // the layout being built: World() reads its heights

    static string ScenePath => cfg.ScenePath;
    static string LayoutPath => cfg.LayoutPath;
    static string MapTexturePath => cfg.MapTexturePath;
    static string SignFolder => cfg.SignFolder;
    static string NavMeshDataPath => cfg.NavMeshPath;
    static string RootName => cfg.root;
    static int OriginCol => cfg.originCol;
    static int OriginRow => cfg.originRow;
    const string ModelFolder = "Assets/Maps/IslaZombie/Models/";   // props are shared by every layout map
    const string IslandModels = "Assets/BeachIsland/Models/";
    const string IslandMats = "Assets/BeachIsland/Models/Materials/";
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const float Cell = 2f;
    public const float LevelH = 2f;             // one height level (Docs/FASE3_FORMATO.md): a normal jump (1.2 m) can't climb it
    const int DoorAreaBase = 3;                 // NavMesh area of door i = DoorAreaBase + i (used by the connection check)
    const int LedgeArea = 12, SlideArea = 13;   // one-way links: drop off a ledge / slide between zones
    const int WaterLayer = 4;
    const float ColliderHeight = 10f, RoofY = 3.4f, WallH = 3.5f, RockH = 6f;
    public const float SprintSpeed = 5f;        // PlayerController.sprintSpeed, for the crossing-time report

    public static string LastReport = "";

    // ------------------------------------------------------------------ layout
    class ZoneDef { public char letter; public string id, name; public int group; public Color color; public bool start, firstBox, circuit; }
    class DoorDef { public char digit; public string name; public int cost; public char[] zones; public bool key; }

    class Layout
    {
        public List<ZoneDef> zones = new List<ZoneDef>();
        public List<DoorDef> doors = new List<DoorDef>();
        public string[] map;
        public int W, H;
        public char At(int c, int r) => r >= 0 && r < H && c >= 0 && c < map[r].Length ? map[r][c] : '.';
        public ZoneDef Zone(char letter) => zones.FirstOrDefault(z => z.letter == letter);
        public char[,] cellZone;   // zone letter of every land cell (nearest floor), '\0' for sea
        public HashSet<Vector2Int> cabins = new HashSet<Vector2Int>();   // wall cells replaced by a spawn cabin

        // ---- heights ([heights] section; without it every cell is level 0)
        public string[] heights;            // raw rows, null = flat map
        public int[,] level;                // level of a cell; ramps: the level at their LOW end
        public char[,] ramp;                // '^' 'v' '<' '>' (uphill direction) or '\0'
        public int[,] rampIdx, rampLen;     // position of a ramp cell in its run (0 = low end) and the run length
        public float[,] floorY;             // walking height at the cell centre (m); solids: the highest floor beside them
        public bool HasHeights => heights != null;
        public List<string> heightErrors = new List<string>();
        public bool InGrid(int c, int r) => c >= 0 && r >= 0 && c < W && r < H;
        public float Y(int c, int r) => InGrid(c, r) ? floorY[c, r] : 0f;
        public bool IsRamp(int c, int r) => InGrid(c, r) && ramp[c, r] != '\0';
        public char HeightAt(int c, int r) => heights != null && r < heights.Length && c < heights[r].Length ? heights[r][c] : '.';
    }

    public static Vector2Int RampDir(char ch) => ch == '^' ? new Vector2Int(0, -1) : ch == 'v' ? new Vector2Int(0, 1) : ch == '<' ? new Vector2Int(-1, 0) : ch == '>' ? new Vector2Int(1, 0) : Vector2Int.zero;
    static bool IsRampChar(char ch) => ch == '^' || ch == 'v' || ch == '<' || ch == '>';

    // Fills level / ramp / floorY from the [heights] rows (Docs/FASE3_FORMATO.md, section 2). Errors go to `errors`.
    static void ComputeHeights(Layout L, List<string> errors)
    {
        L.level = new int[L.W, L.H];
        L.ramp = new char[L.W, L.H];
        L.rampIdx = new int[L.W, L.H];
        L.rampLen = new int[L.W, L.H];
        L.floorY = new float[L.W, L.H];
        if (!L.HasHeights) return;
        if (L.heights.Length != L.H || L.heights.Any(row => row.Length != L.W))
            errors.Add($"ERROR heights: [heights] is {L.heights.Max(r => r.Length)}x{L.heights.Length}, [map] is {L.W}x{L.H}");
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                char h = L.HeightAt(c, r);
                if (h >= '0' && h <= '9') L.level[c, r] = h - '0';
                else if (IsRampChar(h)) L.ramp[c, r] = h;
            }
        // ramp runs: walk downhill to the low end, then the run length; the low end's floor gives the level
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                char h = L.ramp[c, r];
                if (h == '\0') continue;
                var d = RampDir(h);
                int i = 0;
                while (L.InGrid(c - d.x * (i + 1), r - d.y * (i + 1)) && L.ramp[c - d.x * (i + 1), r - d.y * (i + 1)] == h) i++;
                int sc = c - d.x * i, sr = r - d.y * i, n = 0;   // low end of the run
                while (L.InGrid(sc + d.x * n, sr + d.y * n) && L.ramp[sc + d.x * n, sr + d.y * n] == h) n++;
                L.rampIdx[c, r] = i;
                L.rampLen[c, r] = n;
                int lc = c - d.x * (i + 1), lr = r - d.y * (i + 1);
                L.level[c, r] = L.InGrid(lc, lr) && !L.IsRamp(lc, lr) ? L.level[lc, lr] : 0;
            }
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
                L.floorY[c, r] = L.IsRamp(c, r) ? (L.level[c, r] + (L.rampIdx[c, r] + 0.5f) / L.rampLen[c, r]) * LevelH : L.level[c, r] * LevelH;
        // solids, doors and slides stand at the highest walking floor beside them (signs, cabins, door posts)
        var y = (float[,])L.floorY.Clone();
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                char ch = L.At(c, r);
                if (!(IsSolid(ch) || IsDoor(ch) || ch == '!')) continue;
                float best = 0f;
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    int nc = c + d.x, nr = r + d.y;
                    char nch = L.At(nc, nr);
                    if (nch == '.' || IsSolid(nch) || nch == '!') continue;
                    best = Mathf.Max(best, L.floorY[nc, nr]);
                }
                y[c, r] = best;
                L.level[c, r] = Mathf.RoundToInt(best / LevelH);
            }
        L.floorY = y;
    }

    const string Markers = "@sBDPKJbXLGUMEYTCNSFO%Q&";   // pueblo: % traca post, Q the Porton del encierro, & where the lighter may lie     // venecia: N gondola stop, S shovel stall, F Giuseppe's grave, O golden gondolier   // N = gondola stop (theme venecia)
    const string Roofed = "tq";   // temple and cave floors

    static Layout Load() => Load(File.ReadAllLines(LayoutPath));

    // Grid-only checks of a layout given as text (no scene, no NavMesh): heights grid + slide rule. For tests.
    public static List<string> CheckLayoutLines(string[] lines)
    {
        var L = Load(lines);
        cur = L;
        var report = new List<string>(L.heightErrors);
        report.AddRange(CheckHeights(L));
        report.AddRange(SlideReport(L));
        return report;
    }

    static Layout Load(string[] lines)
    {
        var L = new Layout();
        var mapLines = new List<string>();
        List<string> heightLines = null;
        string section = "";
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (section != "map" && (line.Length == 0 || line.StartsWith("#"))) continue;
            if (line.StartsWith("[")) { section = line.Trim('[', ']'); if (section == "heights") heightLines = new List<string>(); continue; }
            if (section == "heights") { if (line.Length > 0 && !line.StartsWith("#")) heightLines.Add(line); continue; }
            var f = line.Split('|').Select(s => s.Trim()).ToArray();
            if (section == "zones")
                L.zones.Add(new ZoneDef
                {
                    letter = f[0][0], id = f[1], name = f[2], group = int.Parse(f[3]),
                    color = ColorUtility.TryParseHtmlString("#" + f[4], out var col) ? col : Color.gray,
                    start = f.Skip(5).Contains("start"),
                    firstBox = f.Skip(5).Contains("firstbox"),
                    circuit = f.Skip(5).Contains("circuito")
                });
            else if (section == "doors")
                L.doors.Add(new DoorDef
                {
                    digit = f[0][0], name = f[1], cost = int.Parse(f[2], CultureInfo.InvariantCulture),
                    zones = f[3].Split(' ').Select(s => s[0]).ToArray(), key = f.Length > 4 && f[4] == "llave"
                });
            else if (section == "map" && line.Length > 0) mapLines.Add(line);
        }
        L.map = mapLines.ToArray();
        L.H = L.map.Length;
        L.W = L.map.Max(r => r.Length);
        L.heights = heightLines != null ? heightLines.ToArray() : null;
        ComputeHeights(L, L.heightErrors);

        // every land cell gets the zone of the nearest floor cell (multi-source BFS) ...
        L.cellZone = new char[L.W, L.H];
        var q = new Queue<Vector2Int>();
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
                if (L.Zone(L.At(c, r)) != null) { L.cellZone[c, r] = L.At(c, r); q.Enqueue(new Vector2Int(c, r)); }
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = p + d;
                if (n.x < 0 || n.y < 0 || n.x >= L.W || n.y >= L.H || L.At(n.x, n.y) == '.' || L.cellZone[n.x, n.y] != '\0') continue;
                L.cellZone[n.x, n.y] = L.cellZone[p.x, p.y];
                q.Enqueue(n);
            }
        }
        // ... and markers take the most common floor letter around them (a spawn at a cave mouth stays outside)
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                if (Markers.IndexOf(L.At(c, r)) < 0) continue;
                var count = new Dictionary<char, int>();
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        char ch = L.At(c + dc, r + dr);
                        if (L.Zone(ch) != null) count[ch] = (count.TryGetValue(ch, out var k) ? k : 0) + 1;
                    }
                if (count.Count > 0) L.cellZone[c, r] = count.OrderByDescending(kv => kv.Value).First().Key;
            }
        return L;
    }

    // Cell -> world point ON THE FLOOR of that cell (its height level; ramps at mid height). `y` is added on top.
    static Vector3 World(float c, float r, float y = 0f) => new Vector3((c - OriginCol) * Cell, y + CellY(c, r), (OriginRow - r) * Cell);
    static float CellY(float c, float r) => cur != null && cur.floorY != null ? cur.Y(Mathf.FloorToInt(c + 0.5f), Mathf.FloorToInt(r + 0.5f)) : 0f;
    // Floor height under a world point (nearest cell).
    static float GroundAt(Vector3 p) => CellY(p.x / Cell + OriginCol, OriginRow - p.z / Cell);
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    static bool IsFloor(Layout L, char ch) => L.Zone(ch) != null || Markers.IndexOf(ch) >= 0;
    static bool IsDoor(char ch) => ch >= '1' && ch <= '9';
    static bool IsSolid(char ch) => ch == '#' || ch == 'R' || ch == 'Z';

    // ------------------------------------------------------------------ materials / models
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
    static readonly List<string> missing = new List<string>();

    static Material M(string name, Color c)
    {
        if (!mats.TryGetValue(name, out var m)) mats[name] = m = TestMapSetup.Mat("GB_" + name, c);
        return m;
    }

    // Shared textured material of the island (M_Wood, M_StoneMoss...), or a flat fallback.
    static Material Shared(string name, Color fallback)
    {
        if (mats.TryGetValue(name, out var m)) return m;
        m = AssetDatabase.LoadAssetAtPath<Material>(IslandMats + name + ".mat");
        if (m == null) { missing.Add(name + ".mat"); m = TestMapSetup.Mat("GB_" + name, fallback); }
        return mats[name] = m;
    }

    static GameObject Model(string path)
    {
        if (models.TryGetValue(path, out var g)) return g;
        g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (g == null) missing.Add(path);
        return models[path] = g;
    }

    static GameObject IZ(string name) => Model(ModelFolder + name + ".fbx");
    static GameObject Island(string name) => Model(IslandModels + name + ".fbx");

    // ------------------------------------------------------------------ entry
    [MenuItem("Tools/Island/Setup Isla Zombie")]
    public static void Setup() => Setup(Isla);

    [MenuItem("Tools/Island/Setup Isla del Faro (alturas)")]
    public static void SetupAlturas() => Setup(IslaAlturas);

    [MenuItem("Tools/Island/Setup Prueba de alturas")]
    public static void SetupPrueba() => Setup(Prueba);

    public static void Setup(MapConfig config)
    {
        cfg = config;
        mats.Clear();
        models.Clear();
        missing.Clear();
        var report = new List<string>();
        var L = Load();
        cur = L;
        report.AddRange(L.heightErrors);
        report.AddRange(CheckHeights(L));
        ConfigureModelImports();

        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var data = TestMapSetup.CreateData();   // after opening the scene (it unloads assets)

        var root = new GameObject(RootName).transform;
        TestMapSetup.EnsureLayers();
        SetupLight();
        var entrances = PlanEntrances(L, report);
        var zones = BuildZones(root, L);
        BuildGround(root, L);
        BuildSolids(root, L);
        BuildProps(root, L);
        BuildDecor(root, L);
        BuildCoast(root, L);
        var doors = BuildDoors(root, L, zones);
        var points = PlaceMarkers(L, zones, entrances);
        BuildEntrances(root, L, zones, entrances);
        BuildGameplay(root, data, L, zones, points, doors);
        if (Ven) VenBuildRuntime(root, L, zones);
        if (Pue) PueBuildRuntime(root, L, zones, doors);
        BuildLabels(root, L, points);
        BuildRamps(root, L, zones);
        BuildLedgeLips(root, L);

        var vallas = root.Find("Vallas");   // La Serenisima fences: not baked (they go away), they carve instead
        if (vallas != null) vallas.gameObject.SetActive(false);
        Physics.SyncTransforms();
        BakeNavMesh(root, L);
        if (vallas != null) vallas.gameObject.SetActive(true);
        int links = BuildLinks(root, L, zones);
        if (links > 0) report.Add($"OK  {links} one-way links (ledges and slides)");
        report.AddRange(CheckMap(L, zones, points));
        report.AddRange(CheckHeightsNav(L, zones, points));
        report.AddRange(SpawnCoverage(L, zones));
        report.AddRange(CheckProps(L, root));
        if (Urb) report.Add(navIslandsLeft == 0 ? $"OK  NavMesh: {LastNavIslands} unreachable islands cut (stall counters, roofs, canal beds), none left" : $"ERROR: {navIslandsLeft} NavMesh islands still unreachable from the start");
        foreach (var d in doors) TestMapSetup.AddDoorObstacle(d.door.transform.Find("Barrier").gameObject, d.width);
        if (Ven) VenFenceObstacles(root);
        if (Pue) report.AddRange(PueAfterChecks(root, L));
        report.Add(BuildMapAndSigns(root, L, points, doors));
        if (missing.Count > 0) report.Add("WARNING missing assets (flat fallbacks used): " + string.Join(", ", missing.Distinct()));

        if (!EditorSceneManager.SaveScene(scene, ScenePath)) report.Add("ERROR: could not save " + ScenePath);
        if (cfg.isla || cfg.menu) PointMenuToMap();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        LastReport = string.Join("\n", report);
        Debug.Log($"[IslaZombie] Setup of {cfg.name} done\n" + LastReport);
    }

    // The props FBX use the island's shared material remap (BeachIslandImportSettings); make sure they
    // were imported with it even if they arrived before that rule existed.
    static void ConfigureModelImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder.TrimEnd('/') }))
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (imp == null || imp.addCollider == false && imp.materialLocation == ModelImporterMaterialLocation.InPrefab && imp.userData == "iz") continue;
            BeachIslandImportSettings.Configure(imp);
            imp.userData = "iz";
            imp.SaveAndReimport();
        }
    }

    static void SetupLight()
    {
        var sun = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null) sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        if (Urb)   // sunset: low warm sun, long shadows, a little warm haze
        {
            sun.color = new Color(1f, 0.68f, 0.42f); sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(22f, -62f, 0f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.006f; RenderSettings.fogColor = new Color(0.95f, 0.66f, 0.5f);
        }
        if (Pue)   // later in the evening than Venice: the sun almost down, everything orange (night falls at the first encierro)
        {
            sun.color = new Color(1f, 0.55f, 0.32f); sun.intensity = 1.15f; sun.transform.rotation = Quaternion.Euler(13f, -62f, 0f);
            RenderSettings.fogDensity = 0.008f; RenderSettings.fogColor = new Color(0.95f, 0.55f, 0.4f);
        }
        RenderSettings.sun = sun;
        if (Camera.main == null)
        {
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
        }
    }

    // The menu's map selector lists this map (and the others in Assets/Data/Maps); build scenes follow.
    static void PointMenuToMap() => TestMapSetup.SetupMenuMaps();

    // ------------------------------------------------------------------ geometry helpers
    static GameObject Child(Transform parent, string name, Vector3 pos, float rotY = 0f) => TestMapSetup.Child(parent, name, pos, rotY);

    // Box mesh with world-space UVs (1 texture tile = 2 m) so the island textures don't stretch.
    // Submesh 0 = top face, submesh 1 = the other faces.
    static Mesh TexBoxMesh(Vector3 size, Vector3 worldCenter)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var top = new List<int>();
        var rest = new List<int>();
        Vector3 h = size / 2f;
        void Face(Vector3 n, Vector3 u, Vector3 v, List<int> tris)
        {
            int i = verts.Count;
            Vector3 c = Vector3.Scale(n, h);
            Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
            foreach (var p in new[] { c - du - dv, c - du + dv, c + du + dv, c + du - dv })
            {
                verts.Add(p);
                Vector3 w = p + worldCenter;
                uvs.Add(new Vector2(Vector3.Dot(w, u), Vector3.Dot(w, v)) / 2f);
            }
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        Face(Vector3.up, Vector3.right, Vector3.forward, top);
        Face(Vector3.down, Vector3.right, Vector3.back, rest);
        Face(Vector3.forward, Vector3.left, Vector3.up, rest);
        Face(Vector3.back, Vector3.right, Vector3.up, rest);
        Face(Vector3.right, Vector3.forward, Vector3.up, rest);
        Face(Vector3.left, Vector3.back, Vector3.up, rest);
        var mesh = new Mesh { name = "TexBox" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(top, 0);
        mesh.SetTriangles(rest, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // A textured box from baseY up size.y metres, optional collider.
    static GameObject TexBox(Transform parent, string name, Vector3 center, Vector3 size, Material top, Material sides, float baseY, bool collider = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var pos = new Vector3(center.x, center.y + baseY + size.y / 2f, center.z);   // baseY is above the centre's floor
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = TexBoxMesh(size, pos);
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { top, sides };
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    // A textured box that blocks like a wall: Wall layer, collider ColliderHeight tall (cannot be jumped).
    static GameObject WallBox(Transform parent, string name, Vector3 center, Vector3 size, Material top, Material sides)
    {
        var go = TexBox(parent, name, center, size, top, sides, 0f, false);
        go.layer = TestMapSetup.WallLayer;
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(size.x, Mathf.Max(size.y, ColliderHeight), size.z);
        box.center = new Vector3(0f, (box.size.y - size.y) / 2f, 0f);
        return go;
    }

    // Invisible collision box standing on the ground (the visual is a model).
    static GameObject Blocker(Transform parent, string name, Vector3 center, Vector3 size, int layer = 0)
    {
        var go = Child(parent, name, new Vector3(center.x, center.y + size.y / 2f, center.z));
        go.layer = layer;
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    // Flat-colour primitive box (no texture), from baseY up.
    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float baseY, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(center.x, center.y + baseY + size.y / 2f, center.z);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Bounds ModelBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Instantiates a model scaled to fit a w x d (world x, z) footprint, turned 0/90 degrees automatically
    // to fit best, plus `turn` (e.g. 180 to face the other way). Bottom on baseY. No colliders.
    static GameObject PlaceInRect(Transform parent, GameObject prefab, Vector3 center, float w, float d, float turn = 0f, float maxH = 0f, float baseY = 0f, bool allowGrow = true)
    {
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        var b = ModelBounds(go);
        float sx = b.size.x, sz = b.size.z;
        float s0 = Mathf.Min(w / sx, d / sz), s90 = Mathf.Min(w / sz, d / sx);
        bool rot90 = s90 > s0 * 1.05f;
        float s = rot90 ? s90 : s0;
        if (!allowGrow) s = Mathf.Min(1f, s);
        if (maxH > 0f) s = Mathf.Min(s, maxH / Mathf.Max(0.01f, b.size.y));
        var rot = Quaternion.Euler(0f, (rot90 ? 90f : 0f) + turn, 0f);
        go.transform.localScale = Vector3.one * s;
        go.transform.rotation = rot;
        go.transform.position = new Vector3(center.x, center.y + baseY, center.z) - rot * new Vector3(b.center.x * s, b.min.y * s, b.center.z * s);
        foreach (var col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
        return go;
    }

    // Crate stack (two on the floor, one on top, plus a barrel by the caller). The top crate is set on the
    // real top of the crate below it: a fixed 1.05 m was added twice before and the crate floated (A1).
    static void CrateStack(Transform pr, Vector3 p, float h)
    {
        var a = PlaceInRect(pr, Island("Prop_Crate"), p + new Vector3(-0.35f, 0f, -0.3f), 1.1f, 1.1f, h * 90f);
        PartColliders(PlaceInRect(pr, Island("Prop_Crate"), p + new Vector3(0.4f, 0f, 0.35f), 1.0f, 1.0f, h * 200f));
        float top = p.y + 1.05f;
        if (a != null)
        {
            var rs = a.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); top = b.max.y; }
            PartColliders(a);
        }
        var c = PlaceInRect(pr, Island("Prop_Crate"), new Vector3(p.x - 0.3f, p.y, p.z - 0.25f), 0.9f, 0.9f, h * 300f, 0f, top - p.y);
        PartColliders(c);
    }

    // Tree trunk collision: the models are centred on their canopy, so the trunk base is ~1 m off the
    // bounds centre. Move the tree so the trunk base (bark vertices near the ground) stands on the cell
    // centre, then follow the trunk up to 3 m with a chain of capsules through the centres of the bark at
    // 0.2 / 1.2 / 2.2 / 3.0 m (palms lean, jungle trees curve).
    static void TrunkCollider(Transform colParent, GameObject tree, Vector3 cell)
    {
        var pts = new List<Vector3>();
        if (tree != null)
            foreach (var mf in tree.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var verts = mesh.vertices;
                var mr = mf.GetComponent<MeshRenderer>();
                int sub = -1;
                if (mr != null)
                    for (int i = 0; i < mr.sharedMaterials.Length && i < mesh.subMeshCount; i++)
                        if (mr.sharedMaterials[i] != null && mr.sharedMaterials[i].name.Contains("Bark")) sub = i;
                IEnumerable<int> idx = sub >= 0 ? mesh.GetTriangles(sub).Distinct() : Enumerable.Range(0, verts.Length);
                foreach (int i in idx) pts.Add(mf.transform.TransformPoint(verts[i]));
            }
        var chain = new List<Vector3>();
        float radius = 0.3f;
        if (pts.Count > 0)
        {
            float minY = pts.Min(v => v.y);
            foreach (float h in new[] { 0.2f, 1.2f, 2.2f, 3.0f })
            {
                var slice = pts.Where(v => Mathf.Abs(v.y - (minY + h)) <= 0.25f).ToList();
                if (slice.Count > 0) chain.Add(slice.Aggregate(Vector3.zero, (a, b) => a + b) / slice.Count);
            }
            var low = pts.Where(v => v.y <= minY + 0.45f).ToList();
            Vector3 lc = low.Aggregate(Vector3.zero, (a, b) => a + b) / low.Count;
            radius = Mathf.Clamp(low.Average(v => new Vector2(v.x - lc.x, v.z - lc.z).magnitude), 0.18f, 0.45f);
            if (chain.Count == 0) chain.Add(lc);
            Vector3 shift = new Vector3(cell.x - chain[0].x, 0f, cell.z - chain[0].z);
            tree.transform.position += shift;
            for (int i = 0; i < chain.Count; i++) chain[i] += shift;
            chain[0] = new Vector3(chain[0].x, minY, chain[0].z);
        }
        else chain.Add(cell);
        if (chain.Count == 1) chain.Add(chain[0] + Vector3.up * 3f);
        for (int i = 1; i < chain.Count; i++)
        {
            Vector3 axis = chain[i] - chain[i - 1];
            var t = Child(colParent, "Tronco", chain[i - 1]);
            t.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            var cap = t.AddComponent<CapsuleCollider>();
            cap.radius = radius * (i == 1 ? 1f : 0.85f);
            cap.height = axis.magnitude + cap.radius * 2f;
            cap.center = new Vector3(0f, axis.magnitude / 2f, 0f);
        }
    }

    // Same, at an explicit rotation and uniform scale (natural size x scale).
    static GameObject PlaceAt(Transform parent, GameObject prefab, Vector3 pos, float rotY, float scale)
    {
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        var b = ModelBounds(go);
        var rot = Quaternion.Euler(0f, rotY, 0f);
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = rot;
        go.transform.position = pos - rot * new Vector3(b.center.x * scale, b.min.y * scale, b.center.z * scale);
        foreach (var col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
        return go;
    }

    // Replaces the material slots whose name starts with `slot` (e.g. "IZ_CarPaint") by `variant`.
    static GameObject Repaint(GameObject go, string slot, Material variant)
    {
        if (go == null) return null;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < ms.Length; i++)
                if (ms[i] != null && ms[i].name.StartsWith(slot)) { ms[i] = variant; changed = true; }
            if (changed) r.sharedMaterials = ms;
        }
        return go;
    }

    // Merges the cells where pick(c, r) is true into rectangles (greedy), in grid coordinates.
    static List<RectInt> Rects(Layout L, System.Func<int, int, bool> pick, System.Func<int, int, int> key = null)
    {
        var used = new bool[L.W, L.H];
        var rects = new List<RectInt>();
        if (key == null) key = (c, r) => L.floorY != null ? Mathf.RoundToInt(L.Y(c, r) * 100f) : 0;   // same height only
        int k0 = 0;
        bool Ok(int c, int r) => c < L.W && r < L.H && !used[c, r] && pick(c, r) && key(c, r) == k0;
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                if (c >= L.W || r >= L.H || used[c, r] || !pick(c, r)) continue;
                k0 = key(c, r);
                int w = 1;
                while (Ok(c + w, r)) w++;
                int h = 1;
                while (r + h < L.H && Enumerable.Range(c, w).All(x => Ok(x, r + h))) h++;
                for (int x = c; x < c + w; x++) for (int y = r; y < r + h; y++) used[x, y] = true;
                rects.Add(new RectInt(c, r, w, h));
            }
        return rects;
    }

    static IEnumerable<Vector2Int> Cells(RectInt r)
    {
        for (int y = r.y; y < r.y + r.height; y++) for (int x = r.x; x < r.x + r.width; x++) yield return new Vector2Int(x, y);
    }

    static Vector3 RectCenter(RectInt r) => World(r.x + (r.width - 1) / 2f, r.y + (r.height - 1) / 2f);
    static Vector3 RectSize(RectInt r, float y, float inset = 0f) => new Vector3(r.width * Cell - inset, y, r.height * Cell - inset);

    // Deterministic pseudo-random number in [0, 1) per cell (so re-running the setup gives the same map).
    static float Hash(int c, int r, int salt = 0)
    {
        uint h = (uint)(c * 73856093) ^ (uint)(r * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    // ------------------------------------------------------------------ zones + ground
    static Dictionary<char, Zone> BuildZones(Transform root, Layout L)
    {
        var zr = Child(root, "Zones", Vector3.zero).transform;
        var zones = new Dictionary<char, Zone>();
        foreach (var zd in L.zones)
        {
            var zgo = Child(zr, "Zone_" + zd.id, Vector3.zero);
            var z = zgo.AddComponent<Zone>();
            z.displayName = zd.name;
            z.openAtStart = zd.start;
            Child(zgo.transform, "SpawnPoints", Vector3.zero);
            Child(zgo.transform, "BoxSpots", Vector3.zero);
            zones[zd.letter] = z;
        }
        return zones;
    }

    static Material GroundMaterial(ZoneDef zd)
    {
        switch (zd.letter)
        {
            case 'c': case 'v': return Shared("M_Sand", zd.color);
            case 'k': case 'f': case 'l': return Shared("M_Grass", zd.color);
            case 't': return Shared("M_Stone", zd.color);
            case 'q': return Shared("M_Rock", zd.color);
            case 'm': return Shared("M_Canvas", zd.color);
            case 'd': return Shared("M_Wood", zd.color);
            default: return M("Ground_" + zd.id, zd.color);   // parking: flat asphalt
        }
    }

    // Land = 3 m thick tiles textured per zone (their sides are the coast cliffs); sea plane around.
    static void BuildGround(Transform root, Layout L)
    {
        var gr = Child(root, "Ground", Vector3.zero).transform;
        var cliff = Ven ? M("VN_Ladrillo", new Color(0.52f, 0.33f, 0.27f)) : Shared("M_Rock", new Color(0.5f, 0.5f, 0.48f));
        foreach (var zd in L.zones)
        {
            var mat = Pue ? PueGroundMaterial(zd) : Ven ? VenGroundMaterial(zd) : GroundMaterial(zd);
            // one block per (zone, level) from y = -3 up to the floor: its sides are the coast cliffs and the slopes
            // between levels. Ramps stand on their low level; solids and slides on level 0 (their box covers the rest).
            foreach (var r in Rects(L, (c, row) => L.cellZone[c, row] == zd.letter && !VenArched(L, c, row), (c, row) => GroundLevel(L, c, row)))
            {
                int lv = GroundLevel(L, r.x, r.y);
                TexBox(gr, "Ground_" + zd.id + (lv > 0 ? "_N" + lv : ""), Flat(RectCenter(r)), RectSize(r, 3f + lv * LevelH), mat, zd.letter == 'd' ? mat : cliff, -3f);
            }
        }
        if (Ven)   // stylised low-poly water mesh under an empty "Sea" (AcquaAlta moves it)
        {
            VenBuildArches(gr, L);
            var vsea = Child(gr, "Sea", new Vector3(0f, -0.8f, 0f));
            vsea.layer = WaterLayer;
            VenBuildWater(vsea.transform, L);
            return;
        }
        var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sea.name = "Sea";
        sea.transform.SetParent(gr, false);
        sea.transform.position = new Vector3(0f, -0.8f, 0f);
        sea.transform.localScale = new Vector3(60f, 1f, 60f);
        sea.layer = WaterLayer;
        var seaMat = AssetDatabase.LoadAssetAtPath<Material>(IslandMats + "M_Water_Ocean.mat");
        sea.GetComponent<Renderer>().sharedMaterial = Pue ? M("VJ_Campo", new Color(0.2f, 0.17f, 0.1f)) : Ven ? M("VN_Canal", new Color(0.12f, 0.27f, 0.25f)) : seaMat != null ? seaMat : M("Sea", new Color(0.12f, 0.5f, 0.65f));
        Object.DestroyImmediate(sea.GetComponent<Collider>());
    }

    // Level of the ground block under a cell (ramps: their low end; solids, slides, sea: 0).
    static int GroundLevel(Layout L, int c, int r)
    {
        char ch = L.At(c, r);
        if (ch == '.' || IsSolid(ch) || ch == '!') return 0;
        return L.level[c, r];
    }

    // Highest walking floor around a solid cell (8 neighbours; a ramp counts with its top), in metres.
    static float SolidBase(Layout L, int c, int r)
    {
        float best = 0f;
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int nc = c + dc, nr = r + dr;
                char ch = L.At(nc, nr);
                if (!L.InGrid(nc, nr) || ch == '.' || IsSolid(ch) || ch == '!') continue;
                best = Mathf.Max(best, L.IsRamp(nc, nr) ? (L.level[nc, nr] + 1) * LevelH : L.floorY[nc, nr]);
            }
        return best;
    }

    // Walls (#), rocks (R), temple stone (Z) and the roofs over the temple / cave floors.
    static void BuildSolids(Transform root, Layout L)
    {
        if (Urb) { VenBuildSolids(root, L); return; }
        var sr = Child(root, "Walls", Vector3.zero).transform;
        var stone = Shared("M_StoneMoss", new Color(0.5f, 0.53f, 0.47f));
        var rock = Shared("M_Rock", new Color(0.45f, 0.41f, 0.37f));
        var temple = Shared("M_TempleStone", new Color(0.62f, 0.58f, 0.47f));
        var carved = Shared("M_TempleCarved", new Color(0.62f, 0.58f, 0.47f));
        int n = 0;
        // a solid stands on y = 0 and rises its height above the highest floor next to it (8 neighbours, ramp tops)
        System.Func<int, int, int> top = (c, row) => Mathf.RoundToInt(SolidBase(L, c, row) * 100f);
        foreach (var r in Rects(L, (c, row) => (L.At(c, row) == '#' || L.At(c, row) == '!') && !L.cabins.Contains(new Vector2Int(c, row)), top))
            WallBox(sr, "Muro" + (n++), Flat(RectCenter(r)), RectSize(r, SolidBase(L, r.x, r.y) + WallH), stone, stone);
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'R', top)) WallBox(sr, "Roca" + (n++), Flat(RectCenter(r)), RectSize(r, SolidBase(L, r.x, r.y) + RockH), rock, rock);
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'Z', top)) WallBox(sr, "Templo" + (n++), Flat(RectCenter(r)), RectSize(r, SolidBase(L, r.x, r.y) + RockH), temple, n % 3 == 0 ? carved : temple);
        var roofs = Child(root, "Roofs", Vector3.zero).transform;
        var templeDark = Shared("M_TempleDark", new Color(0.35f, 0.33f, 0.3f));
        foreach (var r in Rects(L, (c, row) => (IsFloor(L, L.At(c, row)) || IsDoor(L.At(c, row))) && L.cellZone[c, row] == 't'))
            TexBox(roofs, "Techo", RectCenter(r), RectSize(r, 0.5f), temple, templeDark, RoofY);
        foreach (var r in Rects(L, (c, row) => (IsFloor(L, L.At(c, row)) || IsDoor(L.At(c, row))) && L.cellZone[c, row] == 'q'))
            TexBox(roofs, "Techo", RectCenter(r), RectSize(r, 0.5f), rock, rock, RoofY);
    }

    // One box collider per mesh part of a placed model (layer Prop by default), the exact mesh for big parts:
    // collision matches what you see. Flat parts (towels, mats, sticks on the ground) get none.
    static GameObject PartColliders(GameObject go, int layer = NavAgents.PropLayer, float minTop = 0.35f)
    {
        if (go == null) return null;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var r = mf.GetComponent<Renderer>();
            if (r == null || mf.sharedMesh == null) continue;
            var b = r.bounds;
            float g = GroundAt(b.center);   // floor under the part
            if (b.max.y - g < minTop || Mathf.Max(b.size.x, b.size.z) < 0.1f) continue;
            if (Mathf.Max(b.size.x, b.size.z) > 2.6f) { Footprint(mf, layer); continue; }   // big single meshes (crashed plane, bus)
            var box = mf.gameObject.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size;
            mf.gameObject.layer = layer;
            // at least MinPropTop tall: lower things are a "step" for the NavMesh (climb 0.45) but a wall for the player (step 0.3)
            if (b.max.y - g < MinPropTop)
            {
                float extra = (MinPropTop - (b.max.y - g)) / Mathf.Max(0.01f, mf.transform.lossyScale.y);
                box.size += new Vector3(0f, extra, 0f);
                box.center += new Vector3(0f, extra / 2f, 0f);
            }
        }
        return go;
    }

    // Collision for a big mesh as seen from above: 0.4 m columns from the ground up to the mesh, merged into
    // boxes. Nothing can pass under a wing (the NavMesh and the player agree) and what you see is what blocks.
    const float MinPropTop = 0.5f;

    static void Footprint(MeshFilter mf, int layer)
    {
        const float step = 0.4f;
        var mc = mf.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        Physics.SyncTransforms();
        var b = mc.bounds;
        float g = GroundAt(b.center);   // columns stand on the floor of the cell, not on y = 0
        int nx = Mathf.CeilToInt(b.size.x / step), nz = Mathf.CeilToInt(b.size.z / step);
        var top = new float[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                var o = new Vector3(b.min.x + (i + 0.5f) * step, b.max.y + 1f, b.min.z + (k + 0.5f) * step);
                top[i, k] = mc.Raycast(new Ray(o, Vector3.down), out var hit, b.size.y + 2f) && hit.point.y > g + 0.3f ? hit.point.y - g : 0f;
            }
        Object.DestroyImmediate(mc);
        var holder = new GameObject("Huella");
        holder.transform.SetParent(mf.transform, true);
        holder.layer = layer;
        // merge runs along x with similar height
        for (int k = 0; k < nz; k++)
            for (int i = 0; i < nx;)
            {
                if (top[i, k] <= 0f) { i++; continue; }
                int j = i;
                float h = Mathf.Max(MinPropTop, top[i, k]);
                while (j + 1 < nx && top[j + 1, k] > 0f && Mathf.Abs(top[j + 1, k] - h) < 0.4f) { j++; h = Mathf.Max(h, top[j, k]); }
                var col = holder.AddComponent<BoxCollider>();
                var center = new Vector3(b.min.x + (i + j + 1) * step / 2f, g + h / 2f, b.min.z + (k + 0.5f) * step);
                col.center = holder.transform.InverseTransformPoint(center);
                var ls = holder.transform.lossyScale;
                col.size = new Vector3((j - i + 1) * step / ls.x, h / ls.y, step / ls.z);
                i = j + 1;
            }
    }

    // Props: low-poly models with colliders fitted to their parts; the model depends on the zone.
    static void BuildProps(Transform root, Layout L)
    {
        if (Ven) { VenBuildProps(root, L); return; }
        if (Pue) { PueBuildProps(root, L); return; }
        var pr = Child(root, "Props", Vector3.zero).transform;
        var col = Child(root, "PropColliders", Vector3.zero).transform;
        var tentPaints = new[] { TestMapSetup.Mat("IZ_Tent_Orange", new Color(0.9f, 0.5f, 0.15f)), TestMapSetup.Mat("IZ_Tent_Blue", new Color(0.2f, 0.4f, 0.75f)), TestMapSetup.Mat("IZ_Tent_Green", new Color(0.3f, 0.6f, 0.25f)), TestMapSetup.Mat("IZ_Tent_Yellow", new Color(0.95f, 0.8f, 0.3f)) };
        var awnings = new[] { TestMapSetup.Mat("IZ_Awning_Red", new Color(0.8f, 0.2f, 0.15f)), TestMapSetup.Mat("IZ_Awning_Green", new Color(0.2f, 0.55f, 0.35f)), TestMapSetup.Mat("IZ_Awning_Blue", new Color(0.2f, 0.4f, 0.75f)), TestMapSetup.Mat("IZ_Awning_Yellow", new Color(0.95f, 0.75f, 0.2f)) };
        var paints = new[] { TestMapSetup.Mat("IZ_Car_Red", new Color(0.75f, 0.15f, 0.12f)), TestMapSetup.Mat("IZ_Car_White", new Color(0.85f, 0.85f, 0.85f)), TestMapSetup.Mat("IZ_Car_Blue", new Color(0.2f, 0.3f, 0.6f)), TestMapSetup.Mat("IZ_Car_Burnt", new Color(0.18f, 0.16f, 0.15f)), TestMapSetup.Mat("IZ_Car_Green", new Color(0.25f, 0.45f, 0.3f)) };
        var containers = new[] { TestMapSetup.Mat("IZ_Container_Blue", new Color(0.2f, 0.4f, 0.6f)), TestMapSetup.Mat("IZ_Container_Red", new Color(0.7f, 0.2f, 0.15f)), TestMapSetup.Mat("IZ_Container_Green", new Color(0.25f, 0.5f, 0.3f)) };

        // tall props (o): tents / stalls / containers + crates / rocks / huts
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'o'))
        {
            char z = L.cellZone[r.x, r.y];
            if (z == 'd' && (r.width == 2 && r.height == 1 || r.width == 1 && r.height == 2))
            {
                var g = PlaceInRect(pr, IZ("IZ_Contenedor"), RectCenter(r), r.width * Cell - 0.1f, r.height * Cell - 0.1f);
                Repaint(g, "IZ_ContainerPaint", containers[(r.x + r.y) % containers.Length]);
                PartColliders(g);
                continue;
            }
            foreach (var cell in Cells(r))
            {
                var p = World(cell.x, cell.y);
                float h = Hash(cell.x, cell.y);
                switch (z)
                {
                    case 'k':
                        PartColliders(Repaint(PlaceInRect(pr, IZ("IZ_Tienda"), p, 1.95f, 1.95f, Mathf.Floor(h * 4f) * 90f), "IZ_TentPaint", tentPaints[(int)(h * 97) % tentPaints.Length]));
                        break;
                    case 'm':
                        PartColliders(Repaint(PlaceInRect(pr, IZ("IZ_Puesto"), p, 1.95f, 1.95f, FaceOpen(L, cell)), "IZ_AwningPaint", awnings[(int)(h * 97) % awnings.Length]));
                        break;
                    case 'f':
                        PartColliders(PlaceInRect(pr, Island(h < 0.5f ? "Rock_Large" : "Rock_Medium"), p, 2.3f, 2.3f, h * 360f, 2.8f));
                        break;
                    default:   // crate stacks (muelle, volley...)
                        CrateStack(pr, p, h);
                        PartColliders(PlaceInRect(pr, Island("Prop_Barrel"), p + new Vector3(0.45f, 0f, -0.5f), 0.7f, 0.7f));
                        break;
                }
            }
        }

        // huts (h): the first hut pair at the beach is the tiki bar, the rest beach huts
        bool tiki = false;
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'h'))
        {
            if (!tiki && L.cellZone[r.x, r.y] == 'c' && r.width * r.height == 2)
            {
                tiki = true;
                PartColliders(PlaceInRect(pr, Island("Prop_TikiBar"), RectCenter(r), r.width * Cell, r.height * Cell));
                continue;
            }
            foreach (var cell in Cells(r)) PartColliders(PlaceInRect(pr, IZ("IZ_Caseta"), World(cell.x, cell.y), 1.95f, 1.95f, FaceOpen(L, cell)));
        }

        // cars (V) along their rectangle, single cells parked diagonally; bus (W)
        int car = 0;
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'V'))
        {
            if (r.width == 1 && r.height == 1)
            {
                var g = PlaceInRect(pr, IZ("IZ_Coche"), RectCenter(r), 2.6f, 2.6f, 45f);
                Repaint(g, "IZ_CarPaint", paints[car++ % paints.Length]);
                PartColliders(g);
                continue;
            }
            bool alongX = r.width >= r.height;
            int len = alongX ? r.width : r.height, n = Mathf.Max(1, len / 2);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                var p = alongX ? World(r.x - 0.5f + t * r.width, r.y + (r.height - 1) / 2f) : World(r.x + (r.width - 1) / 2f, r.y - 0.5f + t * r.height);
                float segLen = len * Cell / n - 0.2f;
                var g = PlaceInRect(pr, IZ("IZ_Coche"), p, alongX ? segLen : r.width * Cell - 0.2f, alongX ? r.height * Cell - 0.2f : segLen, Hash(r.x, r.y) < 0.5f ? 0f : 180f);
                Repaint(g, "IZ_CarPaint", paints[car++ % paints.Length]);
                PartColliders(g);
            }
        }
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'W'))
        {
            PartColliders(PlaceInRect(pr, IZ("IZ_Autobus"), RectCenter(r), r.width * Cell, r.height * Cell));
        }

        // low cover (x): crashed plane on the beach, bleachers at the volley court, campfire, loungers
        var xs = Rects(L, (c, row) => L.At(c, row) == 'x');
        var planeCells = new List<Vector2Int>();
        for (int row = 0; row < L.H; row++)
            for (int c = 0; c < L.W; c++)
                if (L.At(c, row) == 'x' && L.cellZone[c, row] == 'c' && CountAround(L, c, row, 'x') >= 1 && row >= 19) planeCells.Add(new Vector2Int(c, row));
        if (planeCells.Count > 0)
        {
            var center = planeCells.Select(v => World(v.x, v.y)).Aggregate(Vector3.zero, (a, b) => a + b) / planeCells.Count;
            PartColliders(PlaceAt(pr, IZ("IZ_Avioneta"), center, 35f, 0.7f));
        }
        var volleyX = new List<Vector2Int>();
        foreach (var r in xs)
            foreach (var cell in Cells(r))
            {
                char z = L.cellZone[cell.x, cell.y];
                var p = World(cell.x, cell.y);
                if (z == 'c' && !planeCells.Contains(cell)) PartColliders(PlaceInRect(pr, Island("Prop_Lounger"), p, 1.9f, 1.9f, 90f), NavAgents.PropLayer, 0.25f);
                else if (z == 'k') PartColliders(PlaceInRect(pr, Island("Prop_FirePit"), p, 1.8f, 1.8f), NavAgents.PropLayer, 0.25f);
                else if (z == 'v') volleyX.Add(cell);
                else if (z != 'c') PartColliders(PlaceInRect(pr, Island("Prop_LogBench"), p, 1.9f, 1.9f), NavAgents.PropLayer, 0.25f);
            }
        if (volleyX.Count > 0)
        {
            float midRow = (float)volleyX.Average(v => v.y);
            foreach (var cell in volleyX)
            {
                bool rowOfThree = CountAround(L, cell.x, cell.y, 'x', true) >= 1;
                if (rowOfThree) PartColliders(PlaceInRect(pr, IZ("IZ_Grada"), World(cell.x, cell.y), 1.95f, 1.95f, cell.y < midRow ? 180f : 0f, 0f, 0f, false));
                else PartColliders(PlaceInRect(pr, Island("Prop_Sticks"), World(cell.x, cell.y), 0.8f, 0.8f));   // net posts (thin; flat sticks get no collider)
            }
            var posts = volleyX.Where(v => CountAround(L, v.x, v.y, 'x', true) == 0).ToList();
            if (posts.Count >= 2)
            {
                var a = World(posts[0].x, posts[0].y);
                var b = World(posts[posts.Count - 1].x, posts[posts.Count - 1].y);
                // the net blocks walking (water layer: bullets and the camera go through)
                PartColliders(PlaceInRect(pr, Island("Prop_VolleyNet"), (a + b) / 2f, Mathf.Abs(a.x - b.x) + 0.5f, Mathf.Abs(a.z - b.z) + 0.6f), WaterLayer);
            }
        }

        // trees (A): palms on the beach, jungle trees inland (trunk collider only)
        for (int row = 0; row < L.H; row++)
            for (int c = 0; c < L.W; c++)
            {
                if (L.At(c, row) != 'A') continue;
                var p = World(c, row);
                bool beach = L.cellZone[c, row] == 'c' || L.cellZone[c, row] == 'v';
                float h = Hash(c, row, 3);
                var tree = PlaceInRect(pr, Island(beach ? (h < 0.5f ? "Tree_Palm" : "Tree_Palm_Tall") : (h < 0.5f ? "Tree_Jungle" : "Tree_Jungle_B")), p, 4.5f, 4.5f, h * 360f);
                TrunkCollider(col, tree, p);
            }

        // water (w): pond surface + low invisible blocker (Water layer, ignored by the camera)
        var pond = AssetDatabase.LoadAssetAtPath<Material>(IslandMats + "M_Water_Pond.mat");
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'w'))
        {
            Box(pr, "Agua", RectCenter(r), RectSize(r, 0.06f), pond != null ? pond : M("Pond", new Color(0.2f, 0.55f, 0.7f)), 0.01f, false).layer = WaterLayer;
            Blocker(col, "Agua_Bloqueo", RectCenter(r), RectSize(r, 1.6f), WaterLayer);
        }
        for (int row = 0; row < L.H; row++)
            for (int c = 0; c < L.W; c++)
                if (L.At(c, row) == 'w' && Hash(c, row, 5) < 0.6f)
                    PlaceInRect(pr, Island(Hash(c, row, 6) < 0.5f ? "LilyPad" : "LilyPad_Flower"), World(c, row) + new Vector3(Hash(c, row, 7) - 0.5f, 0f, Hash(c, row, 8) - 0.5f), 0.9f, 0.9f, Hash(c, row, 9) * 360f, 0f, 0.08f);

        // lighthouse (H)
        foreach (var r in Rects(L, (c, row) => L.At(c, row) == 'H'))
        {
            var faro = PartColliders(PlaceInRect(pr, IZ("IZ_Faro"), RectCenter(r), r.width * Cell, r.height * Cell), 0);
            if (faro != null)
            {
                var light = new GameObject("Faro_Luz").AddComponent<Light>();
                light.transform.SetParent(faro.transform, false);
                light.transform.position = RectCenter(r) + Vector3.up * 15f;
                light.type = LightType.Point; light.range = 18f; light.intensity = 2f; light.color = new Color(1f, 0.9f, 0.6f);
            }
        }
    }

    static int CountAround(Layout L, int c, int r, char ch, bool horizontalOnly = false)
    {
        int n = 0;
        if (L.At(c - 1, r) == ch) n++;
        if (L.At(c + 1, r) == ch) n++;
        if (!horizontalOnly) { if (L.At(c, r - 1) == ch) n++; if (L.At(c, r + 1) == ch) n++; }
        return n;
    }

    // Rotation (degrees) that makes a model whose front is +Z face an open neighbour cell (a lane).
    static float FaceOpen(Layout L, Vector2Int cell)
    {
        var dirs = new[] { (new Vector2Int(0, -1), 0f), (new Vector2Int(1, 0), 90f), (new Vector2Int(0, 1), 180f), (new Vector2Int(-1, 0), 270f) };
        foreach (var (d, ang) in dirs.OrderBy(x => Hash(cell.x, cell.y, (int)x.Item2)))
        {
            char ch = L.At(cell.x + d.x, cell.y + d.y);
            if (IsFloor(L, ch)) return ang;
        }
        return 0f;
    }

    // Small decoration without colliders: grass, flowers, shells, candles... never blocks a lane, and nothing
    // that looks solid (crates, barrels, logs) so nothing looks like an obstacle you can walk through.
    static void BuildDecor(Transform root, Layout L)
    {
        if (Ven) { VenBuildDecor(root, L); return; }
        if (Pue) { PueBuildDecor(root, L); return; }
        var dr = Child(root, "Decor", Vector3.zero).transform;
        var sets = new Dictionary<char, (string[] names, float chance, float size)>
        {
            ['c'] = (new[] { "Shell", "Starfish_Orange", "Starfish_Pink", "Driftwood", "Prop_BeachBall", "Prop_Towel_A", "Prop_Towel_B", "Prop_BucketSpade", "Grass_Tuft_Dry" }, 0.35f, 0.8f),
            ['v'] = (new[] { "Shell", "Starfish_Pink", "Prop_BeachBall", "Prop_Towel_A", "Prop_Sandcastle", "Grass_Tuft_Dry", "Driftwood" }, 0.3f, 0.8f),
            ['k'] = (new[] { "Grass_Tuft_A", "Grass_Tuft_B", "Grass_Tuft_Tall", "Flower_Yellow", "Flower_White", "Fern_A", "Prop_Lantern", "Leaf_Litter_A" }, 0.45f, 0.7f),
            ['f'] = (new[] { "Grass_Tuft_A", "Grass_Tuft_Tall", "Flower_Pink", "Flower_White", "Fern_B", "Rock_Small", "Bush_A" }, 0.45f, 0.8f),
            ['l'] = (new[] { "Fern_A", "Fern_B", "Grass_Tuft_Tall", "Mushrooms", "Leaf_Litter_A", "Leaf_Litter_B", "Reeds", "Bush_B" }, 0.55f, 0.9f),
            ['p'] = (new[] { "Leaf_Litter_B", "Grass_Tuft_Dry", "Rock_Pebble" }, 0.2f, 0.7f),
            ['t'] = (new[] { "Prop_Candles", "Leaf_Litter_A", "Rock_Pebble" }, 0.35f, 0.5f),
            ['q'] = (new[] { "Mushrooms", "Rock_Small", "Rock_Pebble", "Prop_Candles" }, 0.4f, 0.6f),
            ['m'] = (new[] { "Prop_Bottle", "Leaf_Litter_B", "Leaf_Litter_A", "Rock_Pebble" }, 0.3f, 0.6f),
            ['d'] = (new[] { "Prop_Lantern", "Seaweed", "Shell", "Rock_Pebble" }, 0.25f, 0.6f),
        };
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                char ch = L.At(c, r);
                if (L.Zone(ch) == null) continue;   // plain floor only (markers stay clean)
                if (!sets.TryGetValue(ch, out var set) || Hash(c, r, 11) > set.chance) continue;
                var name = set.names[(int)(Hash(c, r, 12) * set.names.Length) % set.names.Length];
                var p = World(c, r) + new Vector3((Hash(c, r, 13) - 0.5f) * 1.4f, 0f, (Hash(c, r, 14) - 0.5f) * 1.4f);
                PlaceInRect(dr, Island(name), p, set.size, set.size, Hash(c, r, 15) * 360f, 0f, 0f, false);
            }
    }

    // Invisible walls on the sea cells that touch land (nobody walks into the sea).
    static void BuildCoast(Transform root, Layout L)
    {
        if (Urb) return;   // Venice: open water (Zones/CanalRescue); the village: houses all round, the fields are never reached
        var cr = Child(root, "CoastWalls", Vector3.zero).transform;
        bool Ring(int c, int r)
        {
            if (L.At(c, r) != '.') return false;
            for (int dr = -1; dr <= 1; dr++) for (int dc = -1; dc <= 1; dc++) if (L.At(c + dc, r + dr) != '.') return true;
            return false;
        }
        foreach (var r in Rects(L, Ring))
        {
            var go = Child(cr, "Coast", RectCenter(r));
            go.layer = TestMapSetup.WallLayer;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, ColliderHeight / 2f - 1f, 0f);
            box.size = RectSize(r, ColliderHeight);
        }
    }

    class BuiltDoor { public ZoneDoor door; public float width; public RectInt cells; public bool alongX; public DoorDef def; }

    static List<BuiltDoor> BuildDoors(Transform root, Layout L, Dictionary<char, Zone> zones)
    {
        var dr = Child(root, "Doors", Vector3.zero).transform;
        var pole = M("DoorPole", new Color(0.4f, 0.28f, 0.15f));
        var cloth = new[] { M("DoorCloth_A", new Color(0.9f, 0.25f, 0.2f)), M("DoorCloth_B", new Color(0.2f, 0.45f, 0.9f)), M("DoorCloth_C", new Color(1f, 0.8f, 0.2f)) };
        var stone = Shared("M_StoneMoss", new Color(0.5f, 0.53f, 0.47f));
        var temple = Shared("M_TempleStone", new Color(0.62f, 0.58f, 0.47f));
        var result = new List<BuiltDoor>();
        for (int i = 0; i < L.doors.Count; i++)
        {
            var d = L.doors[i];
            var cells = Rects(L, (c, row) => L.At(c, row) == d.digit);
            if (cells.Count != 1) { Debug.LogError($"[IslaZombie] door {d.digit} must be one straight run of cells (found {cells.Count})"); continue; }
            var r = cells[0];
            // the barrier runs along the wall: horizontal wall if the cell left of the run is wall
            bool alongX = r.width > r.height || (r.width == r.height && (IsSolid(L.At(r.x - 1, r.y)) || L.At(r.x - 1, r.y) == '.'));   // sea beside it: a bridge
            float width = (alongX ? r.width : r.height) * Cell;
            var mat = d.zones.Contains('d') ? M("DoorCloth_Boss", new Color(0.6f, 0.1f, 0.5f))
                    : d.cost >= 5000 ? M("DoorCloth_Gold", new Color(1f, 0.75f, 0.1f)) : cloth[i % cloth.Length];
            var zd = TestMapSetup.BuildDoor(dr, $"Door{d.digit}_{string.Join("_", d.zones.Select(z => L.Zone(z).id))}", d.name,
                d.zones.Select(z => zones[z]).ToArray(), RectCenter(r), alongX ? 0f : 90f, d.cost, width, pole, mat, obstacle: false);   // obstacles after the path check
            if (d.key)
            {
                var so = new SerializedObject(zd);
                // the dock: free once the boat mission is complete (the Llave Maestra is used at the call station)
                so.FindProperty("requiresMission").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // NavMesh area marking the doorway, so the check can treat each door as open/closed
            var vol = Child(zd.transform, "DoorArea", zd.transform.position, alongX ? 0f : 90f).AddComponent<NavMeshModifierVolume>();
            vol.center = new Vector3(0f, 1.5f, 0f);
            vol.size = new Vector3(width + 0.5f, 4f, Cell + 0.5f);
            vol.area = DoorAreaBase + i;
            // stone gate: two pillars and a lintel over the doorway (visual only)
            if (Urb) { VenPlace(dr, VN(Pue ? "VJ_PortalPuente" : "VN_PortalPuente"), RectCenter(r), alongX ? 0f : 90f, (width + 0.4f) / 2.4f); result.Add(new BuiltDoor { door = zd, width = width, cells = r, alongX = alongX, def = d }); continue; }
            var gate = Child(dr, "Portal" + d.digit, RectCenter(r)).transform;
            var along = alongX ? Vector3.right : Vector3.forward;
            var gm = d.cost >= 5000 ? temple : stone;
            foreach (float s in new[] { -1f, 1f })
                TexBox(gate, "Pilar", RectCenter(r) + along * s * (width / 2f + 0.45f), new Vector3(1.1f, 4.4f, 1.1f) + (alongX ? Vector3.forward : Vector3.right) * 1.1f, gm, gm, 0f, false);
            TexBox(gate, "Dintel", RectCenter(r), (alongX ? new Vector3(width + 2f, 0.7f, 2.3f) : new Vector3(2.3f, 0.7f, width + 2f)), gm, gm, 4.0f, false);
            result.Add(new BuiltDoor { door = zd, width = width, cells = r, alongX = alongX, def = d });
        }
        return result;
    }

    // ------------------------------------------------------------------ markers
    class Points
    {
        public Vector3 playerSpawn, bossSpawn, extraction, firstBox;
        public List<Vector3> drinks = new List<Vector3>(), paps = new List<Vector3>(), karaokes = new List<Vector3>(), records = new List<Vector3>(), llaves = new List<Vector3>();
        public List<char> llaveZones = new List<char>();
        // boat mission: E power switch, Y petrol, T radio, C call station (posts lean on something solid beside them)
        public List<(Vector3 p, Vector3 toWall)> switches = new List<(Vector3, Vector3)>(), stations = new List<(Vector3, Vector3)>();
        public List<Vector3> petrol = new List<Vector3>(), radios = new List<Vector3>();
        public List<(Vector3 floor, Vector3 toWall, char zone, char kind)> wallBuys = new List<(Vector3, Vector3, char, char)>();   // kind: G Lentos-peta, U escopeta, M Thermomix
        public Dictionary<char, Vector3> probe = new Dictionary<char, Vector3>();   // one walkable point per zone
    }

    static Points PlaceMarkers(Layout L, Dictionary<char, Zone> zones, List<EntrancePlan> entrances)
    {
        var planned = new HashSet<Vector2Int>(entrances.Select(e => e.cell));
        var pts = new Points();
        bool needBox = true;
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
            {
                char ch = L.At(c, r), z = L.cellZone[c, r];
                if (!IsFloor(L, ch)) continue;
                var p = World(c, r);
                if (ch == 's' && !pts.probe.ContainsKey(z)) pts.probe[z] = p;
                switch (ch)
                {
                    case 's': if (!planned.Contains(new Vector2Int(c, r))) Child(zones[z].transform.Find("SpawnPoints"), "Spawn", p); break;
                    case 'B':
                        Child(zones[z].transform.Find("BoxSpots"), "Box", p, 90f);
                        if (needBox && L.Zone(z).start) { pts.firstBox = p; needBox = false; }
                        break;
                    case '@': pts.playerSpawn = p; break;
                    case 'b': pts.bossSpawn = p; break;
                    case 'X': pts.extraction = p; break;
                    case 'D': pts.drinks.Add(p); break;
                    case 'P': pts.paps.Add(p); break;
                    case 'K': pts.karaokes.Add(p); break;
                    case 'J': pts.records.Add(p); break;
                    case 'L': pts.llaves.Add(p); pts.llaveZones.Add(z); break;
                    case 'Y': pts.petrol.Add(p); break;
                    case 'T': pts.radios.Add(p); break;
                    case 'E':
                    case 'C':
                    {
                        var side = Dirs.FirstOrDefault(d => { char n = L.At(c + d.x, r + d.y); return !IsFloor(L, n) && !IsDoor(n); });
                        var toWall = side == Vector2Int.zero ? Vector3.zero : Flat(World(c + side.x, r + side.y) - p).normalized;
                        (ch == 'E' ? pts.switches : pts.stations).Add((p, toWall));
                        break;
                    }
                    case 'G':
                    case 'U':
                    case 'M':
                    {
                        var wall = Dirs.FirstOrDefault(d => IsSolid(L.At(c + d.x, r + d.y)));
                        if (wall == Vector2Int.zero) { Debug.LogError($"[IslaZombie] wall weapon {ch} at ({c},{r}) needs a wall next to it"); break; }
                        pts.wallBuys.Add((p, Flat(World(c + wall.x, r + wall.y) - p).normalized, z, ch));
                        break;
                    }
                }
            }
        if (needBox) Debug.LogError("[IslaZombie] the start zone needs a box spot (B)");
        return pts;
    }

    // ------------------------------------------------------------------ gameplay objects
    static void BuildGameplay(Transform root, TestMapSetup.Data data, Layout L, Dictionary<char, Zone> zones, Points pts, List<BuiltDoor> doors)
    {
        var shop = Child(root, "Shop", Vector3.zero).transform;
        var box = TestMapSetup.BuildMysteryBox(shop, data.boxWeapons, pts.firstBox);
        // the box is not on the map at the start: it first appears in Parking or Volley (zones flagged firstbox)
        var first = L.zones.Where(z => z.firstBox).Select(z => zones[z.letter]).ToArray();
        if (first.Length > 0)
        {
            var bso = new SerializedObject(box);
            var fz = bso.FindProperty("firstZones");
            fz.arraySize = first.Length;
            for (int i = 0; i < first.Length; i++) fz.GetArrayElementAtIndex(i).objectReferenceValue = first[i];
            bso.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var wb in pts.wallBuys)
        {
            var weapon = wb.kind == 'U' ? data.wallShotgun : wb.kind == 'M' ? data.wallSmg : data.wallWeapon;
            TestMapSetup.BuildWallWeapon(shop, weapon, wb.floor, wb.toWall, Cell / 2f);
        }
        for (int i = 0; i < pts.drinks.Count && data.drinks.Length > 0; i++) TestMapSetup.BuildDrinkMachine(shop, data.drinks[i % data.drinks.Length], pts.drinks[i]);
        foreach (var p in pts.paps) TestMapSetup.BuildPackAPunch(shop, p);

        var egg = Child(root, "EasterEgg", Vector3.zero).transform;
        var vinyl = TestMapSetup.Mat("Vinyl", new Color(0.05f, 0.05f, 0.05f));
        var gold = TestMapSetup.Mat("Gold", new Color(1f, 0.8f, 0.1f), 0.8f);
        foreach (var p in pts.records)
        {
            var d = Child(egg, "JulioDisc", p + Vector3.up * 0.5f);
            var rec = TestMapSetup.Prim(PrimitiveType.Cylinder, d.transform, Vector3.zero, new Vector3(0.35f, 0.012f, 0.35f), vinyl);
            rec.transform.localRotation = Quaternion.Euler(90, 0, 0);
            TestMapSetup.Prim(PrimitiveType.Cylinder, rec.transform, Vector3.zero, new Vector3(0.35f, 1.2f, 0.35f), gold);
            d.AddComponent<DiscPickup>();
        }
        var disc = TestMapSetup.Model(TestMapSetup.PrefabRoot + "/GoldenDisc.prefab").GetComponent<Carryable>();
        foreach (var p in pts.karaokes) TestMapSetup.BuildKaraoke(egg, p, disc);

        BuildMasterKey(root, pts, gold);

        // boat mission
        var mission = Child(root, "Mision", Vector3.zero).transform;
        foreach (var sw in pts.switches) TestMapSetup.BuildPowerSwitch(mission, sw.p, sw.toWall);
        foreach (var p in pts.petrol) TestMapSetup.BuildMissionItem(mission, MissionItem.Kind.Gasolina, p);
        foreach (var p in pts.radios) TestMapSetup.BuildMissionItem(mission, MissionItem.Kind.Radio, p);
        foreach (var st in pts.stations) TestMapSetup.BuildCallStation(mission, st.p, st.toWall);
        if (pts.stations.Count > 0 && (pts.petrol.Count == 0 || pts.radios.Count == 0)) Debug.LogError("[IslaZombie] a call station (C) needs petrol (Y) and a radio (T) on the map");

        // boss + extraction at the pier end, rescue boat moored next to it
        var bossSpawn = Child(root, "BossSpawn", pts.bossSpawn, 0f);
        var extraction = Child(root, "Extraction", pts.extraction);
        var beacon = TestMapSetup.Prim(PrimitiveType.Cylinder, extraction.transform, new Vector3(0, 15, 0), new Vector3(0.6f, 15f, 0.6f), TestMapSetup.Mat("Beacon", new Color(0.2f, 1f, 0.4f, 0.35f), 2f));
        TestMapSetup.Set(extraction.AddComponent<ExtractionPoint>(), "beacon", beacon);
        WorldLabel.Create(extraction.transform, new Vector3(0, 2.2f, 0), Pue ? "TRACTOR DE LA PEÑA" : Ven ? "VAPORETTO DE RESCATE" : "BARCO DE RESCATE", 0.3f, new Color(0.6f, 1f, 0.6f));
        var boatSide = FindSea(L, pts.extraction);
        if (!Pue) PlaceInRect(Child(root, "Barco", Vector3.zero).transform, IZ("IZ_Barco"), pts.extraction + boatSide * 3.4f + Vector3.left * 3f, 9f, 3.4f, 0f, 0f, -0.9f);

        var spawn = Child(root, "PlayerSpawn", pts.playerSpawn, 90f);
        var systems = Child(root, "Systems", Vector3.zero);
        // the boss appears when the dock (d) opens; maps without a dock: the zone of the extraction point
        int xc = Mathf.RoundToInt(pts.extraction.x / Cell) + OriginCol, xr = OriginRow - Mathf.RoundToInt(pts.extraction.z / Cell);
        var bossZone = zones.TryGetValue('d', out var dock) ? dock : zones.TryGetValue(L.InGrid(xc, xr) ? L.cellZone[xc, xr] : '\0', out var xz) ? xz : zones.Values.Last();
        TestMapSetup.BuildSystems(systems, data, spawn.transform, bossZone, bossSpawn.transform,
            TestMapSetup.Model(TestMapSetup.PrefabRoot + "/Enemy_OldMan.prefab"),
            TestMapSetup.Model(TestMapSetup.PrefabRoot + "/Boss_DonAnselmo.prefab").GetComponent<BossAI>());
        systems.AddComponent<MapInfo>();
    }

    // Direction (world) from a point towards the nearest sea cell beside it (for mooring the boat).
    static Vector3 FindSea(Layout L, Vector3 p)
    {
        int c = Mathf.RoundToInt(p.x / Cell) + OriginCol, r = OriginRow - Mathf.RoundToInt(p.z / Cell);
        if (L.At(c, r + 1) == '.') return Vector3.back;
        if (L.At(c, r - 1) == '.') return Vector3.forward;
        return Vector3.right;
    }

    // Llave Maestra: one pickup that the game moves to a random spot (L markers) at the start of the run.
    static void BuildMasterKey(Transform root, Points pts, Material gold)
    {
        if (pts.llaves.Count == 0) return;
        var go = Child(root, "LlaveMaestra", pts.llaves[0]);
        var spots = Child(go.transform.parent, "LlaveSpots", Vector3.zero).transform;
        var spotList = pts.llaves.Select((p, i) => Child(spots, "Llave" + i, p).transform).ToArray();
        var visual = Child(go.transform, "Visual", pts.llaves[0] + Vector3.up * 0.9f).transform;
        var ring = TestMapSetup.Prim(PrimitiveType.Cylinder, visual, new Vector3(0, 0.22f, 0), new Vector3(0.32f, 0.03f, 0.32f), gold);
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        TestMapSetup.Prim(PrimitiveType.Cube, visual, new Vector3(0, -0.12f, 0), new Vector3(0.07f, 0.5f, 0.05f), gold);
        TestMapSetup.Prim(PrimitiveType.Cube, visual, new Vector3(0.08f, -0.3f, 0), new Vector3(0.12f, 0.06f, 0.05f), gold);
        TestMapSetup.Prim(PrimitiveType.Cube, visual, new Vector3(0.07f, -0.2f, 0), new Vector3(0.1f, 0.05f, 0.05f), gold);
        var glow = new GameObject("Brillo").AddComponent<Light>();
        glow.transform.SetParent(visual, false);
        glow.type = LightType.Point; glow.range = 3.5f; glow.intensity = 2.5f; glow.color = new Color(1f, 0.8f, 0.2f);
        var label = WorldLabel.Create(go.transform, new Vector3(0, 1.8f, 0), "LLAVE MAESTRA", 0.28f, new Color(1f, 0.85f, 0.2f));
        var key = go.AddComponent<MasterKey>();
        var so = new SerializedObject(key);
        var sp = so.FindProperty("spots");
        sp.arraySize = spotList.Length;
        for (int i = 0; i < spotList.Length; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = spotList[i];
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("worldLabel").objectReferenceValue = label;
        so.FindProperty("interactRadius").floatValue = 2f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Big zone names for the map pictures. Kept inactive: only shown while rendering them.
    static void BuildLabels(Transform root, Layout L, Points pts)
    {
        var lr = Child(root, "ZoneLabels", Vector3.zero).transform;
        lr.gameObject.SetActive(false);
        foreach (var zd in L.zones)
        {
            var cells = new List<Vector3>();
            for (int r = 0; r < L.H; r++) for (int c = 0; c < L.W; c++) if (L.At(c, r) == zd.letter) cells.Add(World(c, r));
            if (cells.Count == 0) continue;
            var center = cells.Aggregate(Vector3.zero, (a, b) => a + b) / cells.Count;
            OutlinedLabel(lr, "Label_" + zd.id, center + Vector3.up * 30f, zd.name.ToUpperInvariant(), 2.4f, Color.white);
        }
        OutlinedLabel(lr, "Label_Salida", pts.extraction + new Vector3(-3f, 30f, -4f), "SALIDA", 2.8f, new Color(0.4f, 1f, 0.4f));
    }

    // White (or coloured) map text with a dark outline so it reads on any ground.
    static void OutlinedLabel(Transform parent, string name, Vector3 pos, string text, float size, Color color)
    {
        var holder = Child(parent, name, pos).transform;
        foreach (var o in new[] { new Vector3(0.18f, -0.05f, -0.18f), new Vector3(-0.18f, -0.05f, 0.18f), new Vector3(0.18f, -0.05f, 0.18f), new Vector3(-0.18f, -0.05f, -0.18f) })
        {
            var sh = WorldLabel.Create(holder, o, text, size, Color.black);
            sh.fontStyle = FontStyle.Bold;
            Object.DestroyImmediate(sh.GetComponent<WorldLabel>());
        }
        var tm = WorldLabel.Create(holder, Vector3.zero, text, size, color);
        tm.fontStyle = FontStyle.Bold;
        Object.DestroyImmediate(tm.GetComponent<WorldLabel>());
        foreach (Transform t in holder) t.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    // ------------------------------------------------------------------ map picture + "ESTÁS AQUÍ" signs
    // Renders the top-down map (Assets/Maps/IslaZombie/IslaZombie_Map.png) for the pause menu, and one picture
    // per door with a big red arrow "ESTÁS AQUÍ" pointing at that door, shown on boards beside the door.
    static string BuildMapAndSigns(Transform root, Layout L, Points pts, List<BuiltDoor> doors)
    {
        // square area around the land
        int minC = L.W, maxC = 0, minR = L.H, maxR = 0;
        for (int r = 0; r < L.H; r++)
            for (int c = 0; c < L.W; c++)
                if (L.At(c, r) != '.') { minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c); minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r); }
        Vector3 a = World(minC - 0.5f, maxR + 0.5f), b = World(maxC + 0.5f, minR - 0.5f);
        var center = (a + b) / 2f;
        float side = Mathf.Max(b.x - a.x, b.z - a.z) + 6f;
        Directory.CreateDirectory(SignFolder);

        var labels = root.Find("ZoneLabels");
        var roofs = root.Find("Roofs");
        labels.gameObject.SetActive(true);
        roofs.gameObject.SetActive(false);
        var camGo = new GameObject("MapCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.42f, 0.6f);
        cam.orthographic = true;
        cam.orthographicSize = side / 2f;
        cam.farClipPlane = 300f;
        camGo.transform.SetPositionAndRotation(center + Vector3.up * 120f, Quaternion.Euler(90f, 0f, 0f));

        RenderToPng(cam, 1024, 1024, MapTexturePath);
        var mapTex = ImportTexture(MapTexturePath);

        // the overlay draws after the zone names (their font shader ignores depth)
        var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { renderQueue = 3100 };
        red.SetColor("_BaseColor", new Color(0.95f, 0.08f, 0.05f));
        var black = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { renderQueue = 3100 };
        black.SetColor("_BaseColor", new Color(0f, 0f, 0f));
        var islandCenter = World((minC + maxC) / 2f, (minR + maxR) / 2f);
        int boards = 0;
        var signsRoot = Child(root, "Carteles", Vector3.zero).transform;
        var wood = Shared("M_Wood", new Color(0.45f, 0.28f, 0.14f));
        foreach (var d in doors)
        {
            var doorPos = RectCenter(d.cells);
            var outward = doorPos - islandCenter; outward.y = 0f;
            if (outward.magnitude < 6f) outward = Vector3.left;
            outward.Normalize();
            // first direction (outwards, then turning) where the "ESTÁS AQUÍ" plate stays inside the picture
            var dir = outward;
            foreach (float turn in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                var cand = Quaternion.Euler(0f, turn, 0f) * outward;
                var plate = doorPos + cand * side * 0.27f;
                if (Mathf.Abs(plate.x - center.x) < side / 2f - side * 0.16f && Mathf.Abs(plate.z - center.z) < side / 2f - side * 0.05f) { dir = cand; break; }
            }
            var overlay = new GameObject("Overlay").transform;
            BuildArrow(overlay, doorPos + Vector3.up * 40f, dir, side, red, black);
            string path = $"{SignFolder}/Cartel_Puerta{d.def.digit}.png";
            RenderToPng(cam, 1024, 1024, path);
            Object.DestroyImmediate(overlay.gameObject);
            var tex = ImportTexture(path);
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{SignFolder}/Cartel_Puerta{d.def.digit}.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, $"{SignFolder}/Cartel_Puerta{d.def.digit}.mat");
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            boards += PlaceSignBoards(signsRoot, L, d, mat, wood);
        }
        Object.DestroyImmediate(camGo);
        labels.gameObject.SetActive(false);
        roofs.gameObject.SetActive(true);
        AssetDatabase.SaveAssets();

        var info = Object.FindAnyObjectByType<MapInfo>();
        if (info != null)
        {
            var so = new SerializedObject(info);
            so.FindProperty("mapTexture").objectReferenceValue = mapTex;
            so.FindProperty("worldMin").vector2Value = new Vector2(center.x - side / 2f, center.z - side / 2f);
            so.FindProperty("worldMax").vector2Value = new Vector2(center.x + side / 2f, center.z + side / 2f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return $"{(boards >= doors.Count ? "OK " : "FAIL")} map picture + {doors.Count} 'ESTÁS AQUÍ' pictures, {boards} sign boards beside the doors";
    }

    // Flat red arrow (tip at `tip`, coming from `dir` outwards) with "ESTÁS AQUÍ" on a dark plate at its tail.
    static void BuildArrow(Transform parent, Vector3 tip, Vector3 dir, float mapSide, Material red, Material black)
    {
        float len = mapSide * 0.2f, shaftW = mapSide * 0.025f, headW = mapSide * 0.075f, headL = mapSide * 0.07f;
        var right = Vector3.Cross(Vector3.up, dir);
        var verts = new List<Vector3>
        {
            tip, tip + dir * headL + right * headW / 2f, tip + dir * headL - right * headW / 2f,                         // head
            tip + dir * headL + right * shaftW / 2f, tip + dir * len + right * shaftW / 2f,
            tip + dir * len - right * shaftW / 2f, tip + dir * headL - right * shaftW / 2f,                                // shaft
        };
        var mesh = new Mesh { name = "Flecha" };
        mesh.SetVertices(verts.Select(v => v - tip).ToList());
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 1, 3, 4, 5, 3, 5, 6, 3, 5, 4, 3, 6, 5 }, 0);
        mesh.RecalculateNormals();
        var arrow = new GameObject("Flecha");
        arrow.transform.SetParent(parent, false);
        arrow.transform.position = tip;
        arrow.AddComponent<MeshFilter>().sharedMesh = mesh;
        arrow.AddComponent<MeshRenderer>().sharedMaterial = red;
        // text plate at the tail
        var textPos = tip + dir * (len + mapSide * 0.07f) + Vector3.up * 1f;
        var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plate.transform.SetParent(parent, false);
        plate.transform.position = textPos - Vector3.up * 0.5f;
        plate.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        plate.transform.localScale = new Vector3(mapSide * 0.3f, mapSide * 0.075f, 1f);
        plate.GetComponent<Renderer>().sharedMaterial = black;
        var tm = WorldLabel.Create(parent, textPos, "ESTÁS AQUÍ", mapSide * 0.042f, new Color(1f, 0.25f, 0.2f));
        tm.fontStyle = FontStyle.Bold;
        Object.DestroyImmediate(tm.GetComponent<WorldLabel>());
        tm.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        tm.GetComponent<MeshRenderer>().sharedMaterial = new Material(UIKit.Font.material) { renderQueue = 3200 };
    }

    // Boards on the wall faces next to the doorway, on both sides. Returns how many were placed.
    static int PlaceSignBoards(Transform parent, Layout L, BuiltDoor d, Material picture, Material wood)
    {
        var r = d.cells;
        var candidates = d.alongX
            ? new[] { new Vector2Int(r.x - 1, r.y), new Vector2Int(r.x + r.width, r.y) }
            : new[] { new Vector2Int(r.x, r.y - 1), new Vector2Int(r.x, r.y + r.height) };
        var normals = d.alongX ? new[] { new Vector2Int(0, -1), new Vector2Int(0, 1) } : new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0) };
        int placed = 0;
        foreach (var n in normals)
        {
            bool done = false;
            foreach (var wall in candidates)
            {
                if (!IsSolid(L.At(wall.x, wall.y)) || !IsFloor(L, L.At(wall.x + n.x, wall.y + n.y))) continue;
                var nWorld = new Vector3(n.x, 0f, -n.y);
                float face = L.At(wall.x, wall.y) == '#' ? Cell / 2f : Cell / 2f;
                Board(World(wall.x, wall.y) + nWorld * (face + 0.06f) + Vector3.up * 2f, nWorld, 1f);
                done = true;
                break;
            }
            // a bridge has no wall beside it: a smaller board on a post at the foot of the bridge
            if (!done && Urb && IsFloor(L, L.At(r.x + n.x, r.y + n.y)))
            {
                var nWorld = new Vector3(n.x, 0f, -n.y);
                var side = d.alongX ? Vector3.left : Vector3.forward;
                var foot = World(r.x + n.x, r.y + n.y) - nWorld * 0.55f + side * 0.78f;
                Box(parent, "Poste_Cartel" + d.def.digit, foot, new Vector3(0.1f, 1.2f, 0.1f), wood, 0f, false);
                Board(foot + Vector3.up * 1.75f, nWorld, 0.6f);
            }
        }
        return placed;

        void Board(Vector3 pos, Vector3 nWorld, float scale)
        {
            {
                var board = Child(parent, "Cartel_Puerta" + d.def.digit, pos).transform;
                board.rotation = Quaternion.LookRotation(nWorld);
                board.localScale = Vector3.one * scale;
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "Marco";
                frame.transform.SetParent(board, false);
                frame.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                frame.transform.localScale = new Vector3(1.85f, 1.85f, 0.08f);
                frame.GetComponent<Renderer>().sharedMaterial = wood;
                Object.DestroyImmediate(frame.GetComponent<Collider>());
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Mapa";
                quad.transform.SetParent(board, false);
                quad.transform.localPosition = new Vector3(0f, 0f, 0.02f);
                quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // quads face -Z; turn it to face out
                quad.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
                quad.GetComponent<Renderer>().sharedMaterial = picture;
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                placed++;
            }
        }
    }

    static void RenderToPng(Camera cam, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        cam.targetTexture = rt;
        cam.aspect = w / (float)h;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static Texture2D ImportTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && (imp.wrapMode != TextureWrapMode.Clamp || imp.textureCompression != TextureImporterCompression.Uncompressed))
        {
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ navigation
    static void BakeNavMesh(Transform root, Layout L)
    {
        var navSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        var areas = navSettings.FindProperty("areas");
        if (areas != null)
        {
            for (int i = 0; i < L.doors.Count; i++) areas.GetArrayElementAtIndex(DoorAreaBase + i).FindPropertyRelative("name").stringValue = "Puerta" + (i + 1);
            areas.GetArrayElementAtIndex(LedgeArea).FindPropertyRelative("name").stringValue = "Caida";
            areas.GetArrayElementAtIndex(SlideArea).FindPropertyRelative("name").stringValue = "Tobogan";
            var canal = areas.GetArrayElementAtIndex(NavAgents.CanalArea);
            canal.FindPropertyRelative("name").stringValue = "Canal";
            canal.FindPropertyRelative("cost").floatValue = 3f;   // swim only when it is clearly shorter
        }
        navSettings.ApplyModifiedProperties();

        var go = Child(root, "Navigation", Vector3.zero);
        var surface = go.AddComponent<NavMeshSurface>();
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = (1 << 0) | (1 << TestMapSetup.WallLayer) | (1 << WaterLayer) | (1 << NavAgents.PropLayer);   // doors block at runtime (carving obstacles)
        Bake(surface, NavMeshDataPath);
        LastNavIslands = 0;
        if (Urb && CutNavIslands(go.transform, L, 0) > 0) Bake(surface, NavMeshDataPath);   // islands on stall counters / roofs / canal beds (A5)
        if (Urb) navIslandsLeft = CutNavIslands(go.transform, L, 0, false);

        // "Saltador": the same without the props, for the night old men who walk over them
        var jumper = go.AddComponent<NavMeshSurface>();
        jumper.agentTypeID = EnsureJumperAgentType();
        jumper.collectObjects = CollectObjects.All;
        jumper.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        jumper.layerMask = (1 << 0) | (1 << TestMapSetup.WallLayer) | (1 << WaterLayer);
        Bake(jumper, JumperNavMeshDataPath);
    }

    public static int LastNavIslands;
    static int navIslandsLeft;

    // NavMesh "islands" nobody can walk to (stall counters, roofs, canal beds): code that looks for "the nearest NavMesh"
    // (push-back, spawning near a point, the exit of a shortcut) could put an old man up there. Every part of the mesh
    // of agent type `agent` that has no path from the player start is covered with a Not Walkable modifier volume
    // (tight around the island, so the floor beside a stall is never cut); the caller bakes again.
    static int CutNavIslands(Transform parent, Layout L, int agent, bool apply = true)
    {
        Vector3? start = null;
        foreach (var c in Cells(L)) if (L.At(c.x, c.y) == '@') { start = World(c.x, c.y); break; }
        var f = new NavMeshQueryFilter { agentTypeID = agent, areaMask = NavMesh.AllAreas };
        if (start == null || !NavMesh.SamplePosition(start.Value, out var sh, 2f, f)) return 0;
        var tri = NavMesh.CalculateTriangulation();
        var v = tri.vertices; var ix = tri.indices;
        var weld = new int[v.Length];
        var key = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++) { var k = Vector3Int.RoundToInt(v[i] * 50f); if (!key.TryGetValue(k, out var w)) key[k] = w = i; weld[i] = w; }
        var par = Enumerable.Range(0, v.Length).ToArray();
        int Find(int a) { while (par[a] != a) a = par[a] = par[par[a]]; return a; }
        for (int t = 0; t < ix.Length; t += 3)
        {
            int a = Find(weld[ix[t]]);
            par[Find(weld[ix[t + 1]])] = a;
            par[Find(weld[ix[t + 2]])] = a;
        }
        var comps = new Dictionary<int, (Bounds b, Vector3 rep, float area)>();
        for (int t = 0; t < ix.Length; t += 3)
        {
            Vector3 A = v[ix[t]], B = v[ix[t + 1]], C = v[ix[t + 2]];
            float ar = Vector3.Cross(B - A, C - A).magnitude / 2f;
            int r = Find(weld[ix[t]]);
            if (!comps.TryGetValue(r, out var e)) e = (new Bounds(A, Vector3.zero), (A + B + C) / 3f, 0f);
            e.b.Encapsulate(A); e.b.Encapsulate(B); e.b.Encapsulate(C);
            if (ar > e.area) { e.rep = (A + B + C) / 3f; e.area = ar; }
            comps[r] = e;
        }
        var holder = new GameObject("SinMalla_Islas").transform;
        holder.SetParent(parent, false);
        var path = new NavMeshPath();
        int cut = 0;
        foreach (var e in comps.Values)
        {
            if (!NavMesh.SamplePosition(e.rep, out var h, 0.1f, f)) continue;   // the other agent type's mesh
            if (NavMesh.CalculatePath(sh.position, h.position, f, path) && path.status == NavMeshPathStatus.PathComplete) continue;
            cut++;
            if (!apply) continue;
            var vol = new GameObject("Isla").AddComponent<NavMeshModifierVolume>();
            vol.transform.SetParent(holder, false);
            vol.transform.position = new Vector3(e.b.center.x, e.b.center.y, e.b.center.z);
            vol.size = new Vector3(e.b.size.x + 0.2f, e.b.size.y + 0.5f, e.b.size.z + 0.2f);
            vol.center = Vector3.zero;   // the component defaults to (0, 1, 0)
            vol.area = 1;   // Not Walkable
        }
        if (!apply) Object.DestroyImmediate(holder.gameObject);
        else LastNavIslands = cut;
        return cut;
    }

    static string JumperNavMeshDataPath => cfg.JumperNavMeshPath;

    static void Bake(NavMeshSurface surface, string path)
    {
        surface.BuildNavMesh();
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        surface.RemoveData();
        surface.AddData();
        EditorUtility.SetDirty(surface);
    }

    // Adds the "Saltador" agent type (a copy of the default humanoid) to the project if missing.
    const int JumperAgentTypeId = 1479372276;
    static int EnsureJumperAgentType()
    {
        NavAgents.ResetCache();
        if (NavAgents.JumperId != 0) return NavAgents.JumperId;
        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        var settings = so.FindProperty("m_Settings");
        var names = so.FindProperty("m_SettingNames");
        settings.arraySize++;
        var e = settings.GetArrayElementAtIndex(settings.arraySize - 1);
        var first = settings.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("agentTypeID").intValue = JumperAgentTypeId;
        foreach (var f in new[] { "agentRadius", "agentHeight", "agentSlope", "agentClimb", "cellSize" })
            e.FindPropertyRelative(f).floatValue = first.FindPropertyRelative(f).floatValue;
        names.arraySize++;
        names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = NavAgents.JumperName;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        NavAgents.ResetCache();
        if (NavAgents.JumperId == 0) Debug.LogError("[IslaZombie] could not create the Saltador agent type");
        return NavAgents.JumperId;
    }

    // Verifies the map against the design: zone groups sealed, each door joins exactly the expected groups,
    // spawn points / box spots / key spots reachable inside their zone, and crossing times for the two routes.
    static List<string> CheckMap(Layout L, Dictionary<char, Zone> zones, Points pts)
    {
        var report = new List<string>();
        int allDoorBits = 0;
        for (int i = 0; i < L.doors.Count; i++) allDoorBits |= 1 << (DoorAreaBase + i);
        // slides are one-way shortcuts between groups: checked apart (CheckHeightsNav), never part of the door graph
        int Mask(params int[] open) { int m = NavMesh.AllAreas & ~allDoorBits & ~(1 << SlideArea) & ~(1 << NavAgents.CanalArea); foreach (var i in open) m |= 1 << (DoorAreaBase + i); return m; }
        var path = new NavMeshPath();
        Vector3? Snap(Vector3 p) => NavMesh.SamplePosition(p, out var h, 1.5f, NavAgents.Filter(0)) ? h.position : (Vector3?)null;
        bool Reach(Vector3 a, Vector3 b, int mask) => NavMesh.CalculatePath(a, b, new NavMeshQueryFilter { agentTypeID = 0, areaMask = mask }, path) && path.status == NavMeshPathStatus.PathComplete;
        float Length() { float s = 0; for (int i = 1; i < path.corners.Length; i++) s += Vector3.Distance(path.corners[i - 1], path.corners[i]); return s; }

        var probes = new Dictionary<char, Vector3>();
        foreach (var zd in L.zones)
        {
            var p = pts.probe.ContainsKey(zd.letter) ? Snap(pts.probe[zd.letter]) : null;
            if (p.HasValue) probes[zd.letter] = p.Value; else report.Add($"ERROR: no spawn point on the NavMesh for zone {zd.id}");
        }
        if (probes.Count < L.zones.Count) return report;

        // 1) with every door closed, zones connect exactly within their design group
        int closed = Mask();
        bool ok = true;
        foreach (var a in L.zones)
            foreach (var b in L.zones)
            {
                if (a.letter >= b.letter) continue;
                bool reach = Reach(probes[a.letter], probes[b.letter], closed);
                if (reach != (a.group == b.group)) { ok = false; report.Add($"ERROR: {a.id} <-> {b.id} {(reach ? "connected" : "NOT connected")} with doors closed"); }
            }
        report.Add(ok ? "OK  zone groups sealed with doors closed: " + string.Join(" | ", L.zones.GroupBy(z => z.group).Select(g => string.Join("+", g.Select(z => z.id))))
                      : "FAIL zone groups with doors closed");

        // 2) each door alone joins exactly its groups
        var groups = L.zones.GroupBy(z => z.group).Select(g => g.First()).ToList();
        for (int i = 0; i < L.doors.Count; i++)
        {
            var d = L.doors[i];
            int ga = L.Zone(d.zones[0]).group, gb = L.Zone(d.zones[1]).group;
            var joined = new List<string>();
            foreach (var a in groups)
                foreach (var b in groups)
                    if (a.group < b.group && Reach(probes[a.letter], probes[b.letter], Mask(i))) joined.Add(a.group + "-" + b.group);
            string expected = Mathf.Min(ga, gb) + "-" + Mathf.Max(ga, gb);
            bool good = joined.Count == 1 && joined[0] == expected;
            report.Add($"{(good ? "OK " : "ERROR")} door {d.digit} '{d.name}' ({d.cost}{(d.key ? " / llave" : "")}) joins groups [{string.Join(", ", joined)}], expected [{expected}]");
        }

        // 3) spawn points, box spots and key spots inside their zone
        int bad = 0, total = 0;
        foreach (var zd in L.zones)
        {
            var z = zones[zd.letter];
            foreach (var sp in z.SpawnPoints.Concat(z.BoxSpots))
            {
                total++;
                var entrance = sp.GetComponent<SpawnEntrance>();
                var t = entrance != null && entrance.Exit != null ? entrance.Exit : sp;   // entrances: where the old man lands
                var s = Snap(t.position);
                if (s.HasValue && Reach(probes[zd.letter], s.Value, closed)) { t.position = s.Value; continue; }
                bad++;
                report.Add($"ERROR: {zd.id}/{t.name} at {t.position} is not reachable inside its zone");
            }
        }
        for (int i = 0; i < pts.llaves.Count; i++)
        {
            total++;
            var s = Snap(pts.llaves[i]);
            if (s.HasValue && Reach(probes[pts.llaveZones[i]], s.Value, closed)) continue;
            bad++;
            report.Add($"ERROR: llave spot {i} at {pts.llaves[i]} is not reachable inside its zone");
        }
        foreach (var wb in pts.wallBuys)
        {
            total++;
            var s = Snap(wb.floor);
            if (s.HasValue && Reach(probes[wb.zone], s.Value, closed)) continue;
            bad++;
            report.Add($"ERROR: wall weapon at {wb.floor} is not reachable inside its zone");
        }
        // boat mission objects (E / Y / T / C): the spot in front of them must be reachable inside its zone
        var missionSpots = pts.switches.Select(x => ("cuadro eléctrico", x.p)).Concat(pts.stations.Select(x => ("emisora", x.p)))
            .Concat(pts.petrol.Select(p => ("gasolina", p))).Concat(pts.radios.Select(p => ("radio", p))).ToList();
        foreach (var (what, p) in missionSpots)
        {
            total++;
            int mc = Mathf.RoundToInt(p.x / Cell) + OriginCol, mr = OriginRow - Mathf.RoundToInt(p.z / Cell);
            char mz = L.cellZone[mc, mr];
            var s = Snap(p);
            if (probes.ContainsKey(mz) && s.HasValue && Reach(probes[mz], s.Value, closed)) continue;
            bad++;
            report.Add($"ERROR: {what} at {p} is not reachable inside its zone");
        }
        report.Add(bad == 0 ? $"OK  all {total} spawn points / box spots / {pts.llaves.Count} llave spots / {pts.wallBuys.Count} wall weapons / {missionSpots.Count} mission objects on the NavMesh inside their zone" : $"FAIL {bad}/{total} points");

        // 4) routes start -> extraction
        var start = Snap(pts.playerSpawn);
        var end = Snap(pts.extraction);
        int Door(char digit) => L.doors.FindIndex(x => x.digit == digit);
        if (start.HasValue && end.HasValue && !cfg.isla)
            report.Add(Reach(start.Value, end.Value, Mask(Enumerable.Range(0, L.doors.Count).ToArray())) ? $"OK  all doors open: {Length():F0} m to the extraction" : "ERROR: no path to the extraction");
        else if (start.HasValue && end.HasValue)
            foreach (var (label, open) in new[]
            {
                ("Templo route (doors 1,4,5,7)", new[] { Door('1'), Door('4'), Door('5'), Door('7') }),
                ("Cueva route (doors 1,2,7)", new[] { Door('1'), Door('2'), Door('7') }),
                ("All doors open", Enumerable.Range(0, L.doors.Count).ToArray()),
            })
                report.Add(Reach(start.Value, end.Value, Mask(open)) ? $"OK  {label}: {Length():F0} m = {Length() / SprintSpeed:F0} s sprinting" : $"ERROR: {label}: no path to the extraction");
        else report.Add("ERROR: player spawn or extraction not on the NavMesh");
        return report;
    }

    // ------------------------------------------------------------------ capture (top-down / 3/4 view PNGs)
    [MenuItem("Tools/Island/Capture Isla Zombie Map")]
    public static void CaptureMenu() => Capture("Renders/IslaZombie_top.png", "Renders/IslaZombie_persp.png");

    public static void Capture(string topPath, string perspPath)
    {
        var rootGo = GameObject.Find(RootName);
        var labels = rootGo != null ? rootGo.transform.Find("ZoneLabels") : null;
        var roofs = rootGo != null ? rootGo.transform.Find("Roofs") : null;
        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.42f, 0.6f);
        cam.farClipPlane = 600f;
        // top-down orthographic, roofs hidden so the temple / cave layout is readable
        if (labels != null) labels.gameObject.SetActive(true);
        if (roofs != null) roofs.gameObject.SetActive(false);
        cam.orthographic = true;
        cam.orthographicSize = 40f;
        go.transform.SetPositionAndRotation(new Vector3(4f, 200f, 0f), Quaternion.Euler(90f, 0f, 0f));
        RenderToPng(cam, 1860, 1500, topPath);
        if (labels != null) labels.gameObject.SetActive(false);
        if (roofs != null) roofs.gameObject.SetActive(true);
        // 3/4 view like the reference diorama
        cam.orthographic = false;
        cam.fieldOfView = 45f;
        go.transform.position = new Vector3(4f, 62f, -70f);
        go.transform.LookAt(new Vector3(4f, 0f, 4f));
        RenderToPng(cam, 1920, 1080, perspPath);
        Object.DestroyImmediate(go);
    }
}
