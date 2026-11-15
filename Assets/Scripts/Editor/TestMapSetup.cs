// Builds the playable test map in the BeachIsland scene (Tools > Island > Setup Test Map).
// Safe to re-run: generated objects are replaced; data assets (weapons, drinks, cards, animals,
// enemy variants) are only created when missing, so balance tweaks made in the Inspector survive.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public static class TestMapSetup
{
    internal const string GameScenePath = "Assets/Scenes/BeachIsland.unity";
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    internal const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
    internal const string DataRoot = "Assets/Data";
    internal const string PrefabRoot = "Assets/Prefabs";
    const string OldManFbx = "Assets/Characters/OldMan/OldMan.fbx";
    const string EnemyControllerPath = "Assets/Characters/OldMan/OldMan_Enemy.controller";
    public const int PlayerLayer = 8, EnemyLayer = 9, DoorLayer = 10, WallLayer = 11, PropLayer = NavAgents.PropLayer;
    static readonly string[] AnimalIds = { "RedPanda", "Panda", "Frog", "Crocodile", "Toucan" };

    // ------------------------------------------------------------------ map layout (Unity x, z)
    struct ZoneDef { public string id, name; public bool start; public Vector2[] spawns, boxSpots; }
    static readonly ZoneDef[] ZoneDefs =
    {
        new ZoneDef { id = "Chiringuito", name = "Chiringuito", start = true,
            spawns = V(19, -22, 6, -25, 18, -10, 4, -16, 15, -26), boxSpots = V(16.5f, -21.5f, 7, -12) },
        new ZoneDef { id = "Playa", name = "Playa",
            spawns = V(15, -2, 18, 6, 9, -3, 6, 6, 15, 7), boxSpots = V(17, 0.5f) },
        new ZoneDef { id = "Cementerio", name = "Cementerio y Templo",
            spawns = V(-6, -19, -1, -17, -15, -19, -16, -7, -6, -4), boxSpots = V(-2.5f, -12) },
        new ZoneDef { id = "Campamento", name = "Campamento",
            spawns = V(-12, 16, -3, 22, 1, 12, -15, 9, -6, 3), boxSpots = V(-6, 9) },
        new ZoneDef { id = "Jungla", name = "Jungla y Estanque",
            spawns = V(-29, -10, -31, -2, -18, -24, -8, -26, -26, 9, -20, 14), boxSpots = V(-24, -4) },
        new ZoneDef { id = "Muelle", name = "Muelle",
            spawns = V(6, 22, 18, 12, 6, 15, 14, 10), boxSpots = new Vector2[0] },
    };

    struct DoorDef { public string name, a, b; public Vector2 pos; public float rotY; public int cost; public float width; }
    static readonly DoorDef[] DoorDefs =
    {
        new DoorDef { name = "Toalla gigante", a = "Chiringuito", b = "Playa", pos = new Vector2(13, -6), rotY = 0, cost = 750 },
        new DoorDef { name = "Escalera VIP", a = "Chiringuito", b = "Cementerio", pos = new Vector2(2.3f, -9.7f), rotY = 90, cost = 1000, width = 7f },
        new DoorDef { name = "Hamacas apiladas", a = "Playa", b = "Campamento", pos = new Vector2(3, 4), rotY = 90, cost = 1000 },
        new DoorDef { name = "Cinta de obra", a = "Cementerio", b = "Campamento", pos = new Vector2(-10.2f, 2.9f), rotY = 0, cost = 1250 },
        new DoorDef { name = "Escalera del jardinero", a = "Cementerio", b = "Jungla", pos = new Vector2(-21.9f, -14.8f), rotY = 90, cost = 1250 },
        new DoorDef { name = "Valla del camping", a = "Jungla", b = "Campamento", pos = new Vector2(-17, 8), rotY = 90, cost = 1500 },
        new DoorDef { name = "MUELLE - JEFE FINAL", a = "Campamento", b = "Muelle", pos = new Vector2(3, 15), rotY = 90, cost = 2500 },
    };

    // wall polylines (door gaps are left open; doors fill them)
    static readonly Vector2[][] Walls =
    {
        V(1.0f, -6, 11, -6), V(15, -6, 28, -6),                  // chiringuito | playa
        V(1.0f, -6, 0.5f, -1.5f),                                  // playa | cementerio + campamento (was open)
        V(1.5f, -18, 1.5f, -36),                                   // chiringuito | south beach (jungla)
        V(0.5f, -1.5f, 3, -0.5f, 3, 2), V(3, 6, 3, 9),             // playa | campamento
        V(3, 9, 28, 9),                                            // playa | muelle (sealed)
        V(3, 9, 3, 13), V(3, 17, 3, 33),                           // campamento | muelle
        V(-17, -1, -17, 6), V(-17, 10, -17, 33),                   // jungla | campamento
    };

    static Vector2[] V(params float[] xz)
    {
        var r = new Vector2[xz.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = new Vector2(xz[2 * i], xz[2 * i + 1]);
        return r;
    }

    // ------------------------------------------------------------------ entry
    [MenuItem("Tools/Island/Setup Test Map")]
    public static void SetupAll()
    {
        // open the scene first: opening a scene unloads unused assets, which would invalidate
        // references to data assets loaded before it
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        EnsureLayers();
        ConfigureImports();
        var data = CreateData();
        var enemyCtrl = CreateEnemyController();
        CreateEnemyPrefab(enemyCtrl);
        CreateBossPrefab(enemyCtrl);
        CreateGoldenDiscPrefab();
        foreach (var a in data.animals)
        {
            a.playerPrefab = CreatePlayerPrefab(a, data.pistol);
            EditorUtility.SetDirty(a);
        }
        AssetDatabase.SaveAssets();

        data = CreateData();   // reload (idempotent) so every reference points at the persistent assets
        BuildGameScene(data, Model(PrefabRoot + "/Enemy_OldMan.prefab"),
            Model(PrefabRoot + "/Boss_DonAnselmo.prefab").GetComponent<BossAI>(),
            Model(PrefabRoot + "/GoldenDisc.prefab").GetComponent<Carryable>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        CreateEnvironmentPrefab();

        BuildMenuScene();
        SetBuildScenes();
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Verify();
        Debug.Log("Test map setup done.");
    }

    // Build order: menu (0), then the scene of every map in Assets/Data/Maps (the menu's map selector).
    internal const string IslaZombieScenePath = "Assets/Scenes/IslaZombie.unity";
    internal static string PlayableScenePath => File.Exists(IslaZombieScenePath) ? IslaZombieScenePath : GameScenePath;
    const string OriginalPreviewPath = "Assets/Maps/Original/Original_Map.png";
    const string IslaDelFaroPreviewPath = "Assets/Maps/IslaZombie/IslaZombie_Map.png";

    internal static void SetBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(MenuScenePath, true) };
        foreach (var guid in AssetDatabase.FindAssets("t:MapData", new[] { DataRoot + "/Maps" }))
        {
            var map = AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(guid));
            string path = map != null ? "Assets/Scenes/" + map.sceneName + ".unity" : null;
            if (path != null && File.Exists(path) && !scenes.Any(sc => sc.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        if (scenes.Count == 1) scenes.Add(new EditorBuildSettingsScene(PlayableScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Gives the menu its map list (map selector) and saves the menu scene; also refreshes the build scenes.
    [MenuItem("Tools/Island/Setup Map Selector")]
    public static void SetupMenuMaps()
    {
        var menu = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        var data = CreateData();   // after opening the scene (it unloads assets)
        var mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm != null)
        {
            SetMenuMaps(mm, data.maps);
            EditorSceneManager.SaveScene(menu);
        }
        SetBuildScenes();
    }

    static void SetMenuMaps(MainMenu mm, MapData[] maps)
    {
        var so = new SerializedObject(mm);
        var arr = so.FindProperty("maps");
        arr.arraySize = maps.Length;
        for (int i = 0; i < maps.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = maps[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Top-down picture of the original test map (with its zone names) for the map selector card.
    // Opens the BeachIsland scene without changing it.
    [MenuItem("Tools/Island/Render Original Map Preview")]
    public static void RenderOriginalPreview()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        RenderOriginalPreviewCore();
    }

    // Top-down picture of the open scene (labels of the zones, doors' own labels hidden); also feeds the pause-menu
    // map (MapInfo) when the scene has one.
    static void RenderOriginalPreviewCore()
    {
        var terrain = Object.FindObjectsByType<Renderer>().Where(r => r.name.StartsWith("Terrain")).ToArray();
        if (terrain.Length == 0) { Debug.LogError("[Maps] no Terrain renderers in " + GameScenePath); return; }
        var b = terrain[0].bounds;
        foreach (var r in terrain) b.Encapsulate(r.bounds);
        var temp = new GameObject("PreviewTemp").transform;
        float size = Mathf.Max(b.size.x, b.size.z) * 0.55f;
        foreach (var zd in ZoneDefs)
        {
            var c = zd.spawns.Aggregate(Vector2.zero, (a, p) => a + p) / zd.spawns.Length;
            PreviewLabel(temp, new Vector3(c.x, b.max.y + 5f, c.y), zd.name.ToUpperInvariant(), size * 0.035f, Color.white);
        }
        PreviewLabel(temp, new Vector3(18.2f, b.max.y + 5f, 24.6f + size * 0.06f), "SALIDA", size * 0.04f, new Color(0.4f, 1f, 0.4f));
        var hidden = Object.FindObjectsByType<WorldLabel>().Where(l => l.gameObject.activeSelf).Select(l => l.gameObject).ToList();
        foreach (var h in hidden) h.SetActive(false);
        var cam = new GameObject("PreviewCam").AddComponent<Camera>();
        cam.transform.SetParent(temp, false);
        cam.orthographic = true;
        cam.orthographicSize = size;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.42f, 0.6f);
        cam.farClipPlane = 400f;
        cam.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 150f, b.center.z), Quaternion.Euler(90f, 0f, 0f));
        var rt = new RenderTexture(1024, 1024, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Directory.CreateDirectory(Path.GetDirectoryName(OriginalPreviewPath));
        File.WriteAllBytes(OriginalPreviewPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(temp.gameObject);
        foreach (var h in hidden) if (h != null) h.SetActive(true);
        AssetDatabase.ImportAsset(OriginalPreviewPath, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(OriginalPreviewPath) as TextureImporter;
        if (imp != null) { imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.SaveAndReimport(); }
        var info = Object.FindAnyObjectByType<MapInfo>();
        if (info != null)
        {
            var so = new SerializedObject(info);
            so.FindProperty("mapTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(OriginalPreviewPath);
            so.FindProperty("worldMin").vector2Value = new Vector2(b.center.x - size, b.center.z - size);
            so.FindProperty("worldMax").vector2Value = new Vector2(b.center.x + size, b.center.z + size);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log("[Maps] rendered " + OriginalPreviewPath);
    }

    static void PreviewLabel(Transform parent, Vector3 pos, string text, float size, Color color)
    {
        foreach (var o in new[] { new Vector3(0.12f, -0.05f, -0.12f), new Vector3(-0.12f, -0.05f, 0.12f), Vector3.zero })
        {
            var tm = WorldLabel.Create(parent, pos + o * size * 0.7f, text, size, o == Vector3.zero ? color : Color.black);
            tm.fontStyle = FontStyle.Bold;
            Object.DestroyImmediate(tm.GetComponent<WorldLabel>());
            tm.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    // Checks the saved scene really references the data assets (a missing reference breaks the run).
    static void Verify()
    {
        var problems = new List<string>();
        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null || gm.Animals == null || gm.Animals.Any(a => a == null || a.playerPrefab == null)) problems.Add("GameManager animals / player prefabs");
        if (Object.FindObjectsByType<DrinkMachine>().Any(d => d.Drink == null)) problems.Add("drink machines");
        var box = Object.FindAnyObjectByType<MysteryBox>();
        if (box == null || new SerializedObject(box).FindProperty("pool").GetArrayElementAtIndex(0).objectReferenceValue == null) problems.Add("mystery box pool");
        var rm = Object.FindAnyObjectByType<RoundManager>();
        if (rm == null || new SerializedObject(rm).FindProperty("baseVariant").objectReferenceValue == null) problems.Add("round manager variants");
        if (problems.Count > 0) Debug.LogError("[TestMap] Missing references after setup: " + string.Join(", ", problems));
        else Debug.Log("[TestMap] Verified: all references in place");
    }

    // ------------------------------------------------------------------ project settings
    internal static void EnsureLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        layers.GetArrayElementAtIndex(PlayerLayer).stringValue = "Player";
        layers.GetArrayElementAtIndex(EnemyLayer).stringValue = "Enemy";
        layers.GetArrayElementAtIndex(DoorLayer).stringValue = "Door";
        layers.GetArrayElementAtIndex(WallLayer).stringValue = "Wall";
        layers.GetArrayElementAtIndex(PropLayer).stringValue = "Prop";
        tm.ApplyModifiedProperties();
    }

    static void ConfigureImports()
    {
        foreach (var w in new[] { "Pistol", "Shotgun", "Sniper", "SMG" })
        {
            var imp = AssetImporter.GetAtPath($"Assets/Weapons/{w}/{w}.fbx") as ModelImporter;
            if (imp == null) continue;
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.SaveAndReimport();
        }
        foreach (var id in AnimalIds.Concat(new[] { "OldMan" }))
        {
            var imp = AssetImporter.GetAtPath($"Assets/Characters/{id}/{id}.fbx") as ModelImporter;
            if (imp == null) continue;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // one clip per Blender action, named <id>_<Action>; locomotion clips loop
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                string action = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                if (!action.StartsWith(id + "_")) action = id + "_" + action.Split('_').Last();
                c.name = action;
                c.loopTime = action.EndsWith("_Idle") || action.EndsWith("_Walk") || action.EndsWith("_Run") || action.EndsWith("_Jump");
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ data assets
    internal class Data
    {
        public WeaponData pistol;
        public WeaponData[] boxWeapons;
        public EnemyVariant baseVariant;
        public EnemyVariant[] specials;
        public DrinkData[] drinks;
        public CardData[] cards;
        public AnimalData[] animals;
        public MapData[] maps;
        public EconomyData economy;
        public MeleeData melee;
        public EnemyVariant nightVariant;
        public WeaponData wallWeapon;   // Lentos-peta (wall buy, Isla del Faro camping)
        public WeaponData wallShotgun, wallSmg;   // La Chancla / La Thermomix also hang on walls (WeaponData.wallCost)
        public TunnelData tunnels;      // "Anselmo cagón" mode numbers
        public CoopData coop;           // co-op: down / revive, scaling, cards
    }

    static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        a = ScriptableObject.CreateInstance<T>();
        init(a);
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    internal static GameObject Model(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    internal static Data CreateData()
    {
        var d = new Data();
        d.pistol = Asset<WeaponData>(DataRoot + "/Weapons/Pistol.asset", w =>
        {
            w.id = "pistol"; w.displayName = "La Jubilada"; w.upgradedName = "La Jubilada Deluxe";
            w.damage = 50; w.headshotMultiplier = 3; w.pellets = 1; w.penetration = 1; w.range = 80; w.falloffStart = 40; w.minDamageFactor = 0.6f;
            w.fireInterval = 0.2f; w.automatic = false; w.hipSpread = 1.5f; w.aimSpread = 0.3f; w.aimFieldOfView = 45;
            w.magazineSize = 8; w.maxReserve = 64; w.reloadTime = 1.4f; w.boxWeight = 0f;
        });
        var shotgun = Asset<WeaponData>(DataRoot + "/Weapons/Shotgun.asset", w =>
        {
            w.id = "shotgun"; w.displayName = "La Chancla"; w.upgradedName = "La Escupeconchas";
            w.damage = 40; w.headshotMultiplier = 1.5f; w.pellets = 8; w.penetration = 1; w.range = 25; w.falloffStart = 6; w.minDamageFactor = 0.3f;
            w.fireInterval = 0.85f; w.automatic = false; w.hipSpread = 6; w.aimSpread = 4; w.aimFieldOfView = 50;
            w.magazineSize = 6; w.maxReserve = 48; w.reloadTime = 2.6f; w.boxWeight = 1f; w.tracerColor = new Color(1f, 0.7f, 0.4f);
        });
        var sniper = Asset<WeaponData>(DataRoot + "/Weapons/Sniper.asset", w =>
        {
            w.id = "sniper"; w.displayName = "El Catalejo"; w.upgradedName = "El Catalejo Pirata";
            w.damage = 700; w.headshotMultiplier = 4; w.pellets = 1; w.penetration = 4; w.range = 200; w.falloffStart = 200; w.minDamageFactor = 1f;
            w.fireInterval = 1.3f; w.automatic = false; w.hipSpread = 8; w.aimSpread = 0f; w.aimFieldOfView = 20;
            w.magazineSize = 5; w.maxReserve = 30; w.reloadTime = 3f; w.boxWeight = 0.8f; w.tracerColor = new Color(0.6f, 0.9f, 1f);
        });
        var smg = Asset<WeaponData>(DataRoot + "/Weapons/SMG.asset", w =>
        {
            w.id = "smg"; w.displayName = "La Thermomix"; w.upgradedName = "La Thermomix Turbo";
            w.damage = 70; w.headshotMultiplier = 2; w.pellets = 1; w.penetration = 1; w.range = 60; w.falloffStart = 25; w.minDamageFactor = 0.6f;
            w.fireInterval = 0.075f; w.automatic = true; w.hipSpread = 3.5f; w.aimSpread = 1.5f; w.aimFieldOfView = 45;
            w.magazineSize = 40; w.maxReserve = 240; w.reloadTime = 2.2f; w.boxWeight = 0.8f;
        });
        // wall buy (not in the box): a slow shotgun that loads its shells one at a time
        d.wallWeapon = Asset<WeaponData>(DataRoot + "/Weapons/LentosPeta.asset", w =>
        {
            w.id = "lentospeta"; w.displayName = "Lentos-peta"; w.upgradedName = "Lentos-peta Exprés";
            w.damage = 45; w.headshotMultiplier = 1.5f; w.pellets = 8; w.penetration = 1; w.range = 22; w.falloffStart = 5; w.minDamageFactor = 0.3f;
            w.fireInterval = 1.35f; w.automatic = false; w.hipSpread = 7; w.aimSpread = 4.5f; w.aimFieldOfView = 52;
            w.magazineSize = 5; w.maxReserve = 30; w.reloadTime = 4f; w.reloadOneByOne = true; w.reloadStartTime = 0.5f; w.shellLoadTime = 0.75f;
            w.boxWeight = 0f; w.wallCost = 500; w.wallAmmoCost = 250; w.recoil = 4.5f; w.tracerColor = new Color(0.9f, 0.6f, 0.35f);
        });
        d.tunnels = Asset<TunnelData>(DataRoot + "/AnselmoCagon.asset", t => { });
        d.coop = Asset<CoopData>(DataRoot + "/Coop.asset", c => { });
        // models are (re)assigned every run so a re-export from Blender is picked up
        d.pistol.model = Model("Assets/Weapons/Pistol/Pistol.fbx");
        shotgun.model = Model("Assets/Weapons/Shotgun/Shotgun.fbx");
        sniper.model = Model("Assets/Weapons/Sniper/Sniper.fbx");
        smg.model = Model("Assets/Weapons/SMG/SMG.fbx");
        d.wallWeapon.model = shotgun.model;
        if (d.pistol.recoil < 0f) d.pistol.recoil = 0.8f;
        if (shotgun.recoil < 0f) shotgun.recoil = 3.5f;
        if (sniper.recoil < 0f) sniper.recoil = 5f;
        if (smg.recoil < 0f) smg.recoil = 0.35f;
        foreach (var w in new[] { d.pistol, shotgun, sniper, smg, d.wallWeapon }) EditorUtility.SetDirty(w);
        d.boxWeapons = new[] { shotgun, sniper, smg };
        d.wallShotgun = shotgun;
        d.wallSmg = smg;

        d.baseVariant = Asset<EnemyVariant>(DataRoot + "/Enemies/Jubilado.asset", v =>
        {
            v.displayName = "Jubilado"; v.healthMultiplier = 1; v.moveSpeed = 1.8f; v.damage = 25; v.scale = 1; v.pointsMultiplier = 1;
        });
        var runner = Asset<EnemyVariant>(DataRoot + "/Enemies/JubiladoChandal.asset", v =>
        {
            v.displayName = "Jubilado en chándal"; v.healthMultiplier = 0.8f; v.moveSpeed = 4f; v.damage = 25; v.scale = 0.95f; v.pointsMultiplier = 1.2f;
            v.clothingColor = new Color(0.85f, 0.12f, 0.1f, 1f); v.firstRound = 4; v.baseChance = 0.08f; v.chancePerRound = 0.05f; v.maxChance = 0.5f;
        });
        var tank = Asset<EnemyVariant>(DataRoot + "/Enemies/AbueloCulturista.asset", v =>
        {
            v.displayName = "Abuelo culturista"; v.healthMultiplier = 3f; v.moveSpeed = 1.5f; v.damage = 40; v.scale = 1.3f; v.pointsMultiplier = 2f;
            v.clothingColor = new Color(0.45f, 0.15f, 0.6f, 1f); v.firstRound = 7; v.baseChance = 0.06f; v.chancePerRound = 0.01f; v.maxChance = 0.15f;
        });
        d.specials = new[] { tank, runner };
        // night rounds (every 5): only these, green tracksuit, walk over props and throw flip-flops
        d.nightVariant = Asset<EnemyVariant>(DataRoot + "/Enemies/AbueloSenderista.asset", v =>
        {
            v.displayName = "Abuelo senderista"; v.healthMultiplier = 0.9f; v.moveSpeed = 3.2f; v.damage = 20; v.scale = 1f; v.pointsMultiplier = 1.5f;
            v.clothingColor = new Color(0.15f, 0.6f, 0.2f, 1f); v.firstRound = 5; v.maxAliveAtOnce = 5;
            v.jumper = true; v.throwsChanclas = true; v.throwMinRange = 4f; v.throwMaxRange = 14f; v.throwCooldown = 3.5f; v.throwDamage = 15; v.throwSpeed = 13f;
        });
        d.economy = Asset<EconomyData>(DataRoot + "/Economy.asset", e => { });
        d.melee = Asset<MeleeData>(DataRoot + "/Weapons/Ukulele.asset", m => { });

        d.drinks = new[]
        {
            Asset<DrinkData>(DataRoot + "/Drinks/PinaColapso.asset", x => { x.id = "pina"; x.displayName = "Piña Colapso"; x.cost = 2500; x.color = new Color(1f, 0.85f, 0.2f);
                x.effectDescription = "+75% de vida máxima"; x.sideEffectDescription = "hipo"; x.mods = new[] { new StatMod(Stat.MaxHealth, 1.75f) }; x.sideEffect = DrinkSpecial.Hiccups; }),
            Asset<DrinkData>(DataRoot + "/Drinks/MojitoTurbo.asset", x => { x.id = "mojito"; x.displayName = "Mojito Turbo"; x.cost = 3000; x.color = new Color(0.4f, 0.9f, 0.4f);
                x.effectDescription = "recargas el doble de rápido"; x.sideEffectDescription = "hablas a 2x (por ahora solo en tu cabeza)"; x.mods = new[] { new StatMod(Stat.ReloadSpeed, 2f) }; }),
            Asset<DrinkData>(DataRoot + "/Drinks/TintoDeVerano.asset", x => { x.id = "tinto"; x.displayName = "Tinto de Verano Eterno"; x.cost = 1500; x.color = new Color(0.65f, 0.1f, 0.2f);
                x.effectDescription = "te levantas solo una vez"; x.sideEffectDescription = "al levantarte pierdes todas las bebidas"; x.special = DrinkSpecial.SelfRevive; }),
            Asset<DrinkData>(DataRoot + "/Drinks/CarajilloDelAbuelo.asset", x => { x.id = "carajillo"; x.displayName = "Carajillo del Abuelo"; x.cost = 2000; x.color = new Color(0.55f, 0.35f, 0.2f);
                x.effectDescription = "al recibir un golpe los abuelos te ignoran 4 s"; x.sideEffectDescription = "hueles a farmacia"; x.special = DrinkSpecial.Stealth; x.mods = new[] { new StatMod(Stat.MoveSpeed, 1.05f) }; }),
        };

        CardData Card(string file, string title, string bless, StatMod[] b, string curse, StatMod[] c, Color col) =>
            Asset<CardData>(DataRoot + "/Cards/" + file + ".asset", x => { x.title = title; x.blessingText = bless; x.blessing = b; x.curseText = curse; x.curse = c; x.color = col; });
        Color gold = new Color(0.95f, 0.7f, 0.2f), teal = new Color(0.2f, 0.8f, 0.8f), pink = new Color(0.95f, 0.4f, 0.6f);
        d.cards = new[]
        {
            Card("BarraLibre", "Barra libre", "Bebidas a mitad de precio", new[] { new StatMod(Stat.DrinkCost, 0.5f) }, "Los abuelos son un 15% más rápidos", new[] { new StatMod(Stat.EnemySpeed, 1.15f) }, gold),
            Card("GafasDeSol", "Gafas de sol", "+25% de daño", new[] { new StatMod(Stat.PlayerDamage, 1.25f) }, "Salen un 20% más de abuelos", new[] { new StatMod(Stat.EnemyCount, 1.2f) }, teal),
            Card("JubilacionAnticipada", "Jubilación anticipada", "Los abuelos tienen un 20% menos de vida", new[] { new StatMod(Stat.EnemyHealth, 0.8f) }, "Salen un 40% más de abuelos", new[] { new StatMod(Stat.EnemyCount, 1.4f) }, pink),
            Card("TarjetaImserso", "Tarjeta del IMSERSO", "Puertas un 30% más baratas", new[] { new StatMod(Stat.DoorCost, 0.7f) }, "La Máquina de Granizados cuesta un 50% más", new[] { new StatMod(Stat.BoxCost, 1.5f) }, gold),
            Card("CremaFactor50", "Crema factor 50", "+50% de vida máxima", new[] { new StatMod(Stat.MaxHealth, 1.5f) }, "Te mueves un 10% más lento", new[] { new StatMod(Stat.MoveSpeed, 0.9f) }, teal),
            Card("ChanclasDeGel", "Chanclas de gel", "Te mueves un 20% más rápido", new[] { new StatMod(Stat.MoveSpeed, 1.2f) }, "Los abuelos pegan un 25% más fuerte", new[] { new StatMod(Stat.EnemyDamage, 1.25f) }, pink),
            Card("HappyHour", "Happy Hour", "+50% de conchas", new[] { new StatMod(Stat.PointsGain, 1.5f) }, "Los abuelos tienen un 25% más de vida", new[] { new StatMod(Stat.EnemyHealth, 1.25f) }, gold),
            Card("SiestaObligatoria", "Siesta obligatoria", "Empiezas a curarte el doble de pronto", new[] { new StatMod(Stat.RegenDelay, 0.5f) }, "Disparas un 15% más lento", new[] { new StatMod(Stat.FireRate, 0.85f) }, teal),
            Card("BufeLibre", "Bufé libre", "Neveras de munición x2.5 más frecuentes", new[] { new StatMod(Stat.AmmoDropChance, 2.5f) }, "Cargadores un 25% más pequeños", new[] { new StatMod(Stat.MagazineSize, 0.75f) }, pink),
            Card("GorraDeCapitan", "Gorra de capitán", "+50% de daño a la cabeza", new[] { new StatMod(Stat.HeadshotDamage, 1.5f) }, "Los abuelos son un 10% más rápidos", new[] { new StatMod(Stat.EnemySpeed, 1.1f) }, gold),
            Card("ManosRapidas", "Manos rápidas", "Recargas un 50% más rápido", new[] { new StatMod(Stat.ReloadSpeed, 1.5f) }, "-20% de conchas", new[] { new StatMod(Stat.PointsGain, 0.8f) }, teal),
            Card("PulseraTodoIncluido", "Pulsera todo incluido", "La Máquina de Granizados un 40% más barata", new[] { new StatMod(Stat.BoxCost, 0.6f) }, "Bebidas un 50% más caras", new[] { new StatMod(Stat.DrinkCost, 1.5f) }, pink),
        };

        AnimalData Animal(string id, string name, string tagline, Color col, string pName, string pDesc, StatMod[] pMods, AbilityType ab, string abName, string abDesc, float cd, float dur) =>
            Asset<AnimalData>(DataRoot + "/Animals/" + id + ".asset", x =>
            {
                x.id = id; x.displayName = name; x.tagline = tagline; x.uiColor = col; x.passiveName = pName; x.passiveDescription = pDesc; x.passiveMods = pMods;
                x.ability = ab; x.abilityName = abName; x.abilityDescription = abDesc; x.abilityCooldown = cd; x.abilityDuration = dur;
            });
        d.animals = new[]
        {
            Animal("RedPanda", "Panda Rojo", "Dispara primero, pregunta nunca.", new Color(0.9f, 0.4f, 0.2f), "Dedos ágiles", "recarga +15%", new[] { new StatMod(Stat.ReloadSpeed, 1.15f) },
                AbilityType.RedFury, "Furia Roja", "7 s disparando un 50% más rápido y recargando el doble", 40, 7),
            Animal("Panda", "Panda", "Abraza fuerte. Demasiado fuerte.", new Color(0.9f, 0.9f, 0.9f), "Lorza protectora", "+25% vida", new[] { new StatMod(Stat.MaxHealth, 1.25f) },
                AbilityType.BearHug, "Abrazo de Oso", "onda que daña, empuja y aturde a los abuelos cercanos", 35, 0.5f),
            Animal("Frog", "Rana", "Salta primero, cae encima después.", new Color(0.3f, 0.85f, 0.3f), "Ancas de acero", "+10% velocidad", new[] { new StatMod(Stat.MoveSpeed, 1.1f) },
                AbilityType.SuperJump, "Superrana", "salto enorme que aturde al caer", 30, 1.5f),
            Animal("Crocodile", "Cocodrilo", "Piel de bolso de lujo.", new Color(0.3f, 0.6f, 0.3f), "Escamas", "-10% daño recibido", new[] { new StatMod(Stat.DamageTaken, 0.9f) },
                AbilityType.Armor, "Coraza", "6 s recibiendo un 70% menos de daño", 40, 6),
            Animal("Toucan", "Tucán", "Lo ve todo. Lo juzga todo.", new Color(1f, 0.6f, 0.1f), "Pico certero", "+25% daño a la cabeza", new[] { new StatMod(Stat.HeadshotDamage, 1.25f) },
                AbilityType.EagleEye, "Ojo de Tucán", "8 s viendo a los abuelos a través de las paredes y +50% daño a la cabeza", 40, 8),
        };
        // maps of the menu's map selector (previews are re-assigned every run)
        d.maps = new[]
        {
            Asset<MapData>(DataRoot + "/Maps/IslaDelFaro.asset", m => { m.id = "isla-del-faro"; m.displayName = "Isla del Faro"; m.sceneName = "IslaZombie";
                m.description = "Isla redonda de callejones, templo, cueva laberinto y faro. Escapa en el barco del muelle."; }),
            Asset<MapData>(DataRoot + "/Maps/Original.asset", m => { m.id = "original"; m.displayName = "Original"; m.sceneName = "BeachIsland";
                m.description = "La primera isla: pequeña, rápida y muy caótica."; }),
            // Phase 3 workspace: Isla del Faro rebuilt with heights (Docs/FASE3_FORMATO.md); the original stays untouched
            Asset<MapData>(DataRoot + "/Maps/IslaFaroAlturas.asset", m => { m.id = "isla-del-faro-alturas"; m.displayName = "Isla del Faro (alturas)"; m.sceneName = "IslaFaroAlturas";
                m.description = "En obras: la Isla del Faro con terrazas, rampas, caídas y toboganes."; }),
        };
        d.maps[0].preview = AssetDatabase.LoadAssetAtPath<Texture2D>(IslaDelFaroPreviewPath);
        d.maps[1].preview = AssetDatabase.LoadAssetAtPath<Texture2D>(OriginalPreviewPath);
        d.maps[2].preview = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Maps/IslaFaroAlturas/IslaFaroAlturas_Map.png");
        foreach (var m in d.maps) EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return d;
    }

    // ------------------------------------------------------------------ enemy / boss
    static AnimationClip Clip(string fbx, string contains) =>
        AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__") && c.name.Contains(contains));

    static AnimatorController CreateEnemyController()
    {
        AssetDatabase.DeleteAsset(EnemyControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "WalkSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        // locomotion: Speed in m/s at normal scale -> idle / walk (1.7) / run (4)
        var idle = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip(OldManFbx, "_Idle"), 0f);
        tree.AddChild(Clip(OldManFbx, "_Walk"), 1.7f);
        tree.AddChild(Clip(OldManFbx, "_Run"), 4f);
        var attack = sm.AddState("Attack"); attack.motion = Clip(OldManFbx, "_Attack");
        var death = sm.AddState("Death"); death.motion = Clip(OldManFbx, "_Death");
        sm.defaultState = idle;
        AnimatorStateTransition t;
        var a = sm.AddAnyStateTransition(attack); a.AddCondition(AnimatorConditionMode.If, 0, "Attack"); a.hasExitTime = false; a.duration = 0.05f; a.canTransitionToSelf = false;
        t = attack.AddTransition(idle); t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.1f;
        var dth = sm.AddAnyStateTransition(death); dth.AddCondition(AnimatorConditionMode.If, 0, "Die"); dth.hasExitTime = false; dth.duration = 0.1f; dth.canTransitionToSelf = false;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static GameObject BuildOldMan(string name, RuntimeAnimatorController ctrl, int layer)
    {
        var root = new GameObject(name);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Model(OldManFbx));
        model.transform.SetParent(root.transform, false);
        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var capsule = root.AddComponent<CapsuleCollider>();   // body: below the head so headshots hit the head sphere
        capsule.center = new Vector3(0f, 0.48f, 0f);
        capsule.height = 0.96f;
        capsule.radius = 0.38f;
        var headBone = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Head");
        var head = new GameObject("HeadHitbox");
        head.transform.SetParent(headBone != null ? headBone : root.transform, false);
        head.transform.position = root.transform.position + Vector3.up * 1.14f;
        var sphere = head.AddComponent<SphereCollider>();
        sphere.radius = 0.26f / Mathf.Max(0.01f, head.transform.lossyScale.x);
        head.AddComponent<HitZone>().isHead = true;

        foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
        return root;
    }

    static GameObject CreateEnemyPrefab(RuntimeAnimatorController ctrl)
    {
        var root = BuildOldMan("Enemy_OldMan", ctrl, EnemyLayer);
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f; agent.height = 1.35f; agent.speed = 1.8f; agent.angularSpeed = 540f; agent.acceleration = 12f;
        agent.stoppingDistance = 1.0f; agent.autoBraking = true; agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        root.AddComponent<EnemyHealth>();
        root.AddComponent<EnemyAI>();
        return SavePrefab(root, PrefabRoot + "/Enemy_OldMan.prefab");
    }

    static BossAI CreateBossPrefab(RuntimeAnimatorController ctrl)
    {
        var root = BuildOldMan("Boss_DonAnselmo", ctrl, EnemyLayer);
        root.transform.localScale = Vector3.one * 2.4f;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(0.95f, 0.75f, 0.15f));
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            if (r.name.Contains("Vest") || r.name.Contains("Shirt")) r.SetPropertyBlock(block);
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f; agent.height = 1.35f; agent.speed = 1.8f; agent.angularSpeed = 300f; agent.acceleration = 10f; agent.stoppingDistance = 2.5f;
        root.AddComponent<EnemyHealth>();
        root.AddComponent<BossAI>();
        return SavePrefab(root, PrefabRoot + "/Boss_DonAnselmo.prefab").GetComponent<BossAI>();
    }

    static Carryable CreateGoldenDiscPrefab()
    {
        var root = new GameObject("GoldenDisc");
        var disc = Prim(PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(0.55f, 0.03f, 0.55f), Mat("Gold", new Color(1f, 0.8f, 0.1f), 0.8f));
        disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var col = root.AddComponent<SphereCollider>();
        col.radius = 0.4f;
        col.isTrigger = true;
        root.AddComponent<Carryable>();
        WorldLabel.Create(root.transform, new Vector3(0, 0.7f, 0), "DISCO DE ORO", 0.3f, new Color(1f, 0.85f, 0.2f));
        return SavePrefab(root, PrefabRoot + "/GoldenDisc.prefab").GetComponent<Carryable>();
    }

    static GameObject SavePrefab(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ players
    static GameObject CreatePlayerPrefab(AnimalData animal, WeaponData pistol)
    {
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        string id = animal.id;
        var root = new GameObject("Player_" + id);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Model($"Assets/Characters/{id}/{id}.fbx"));
        model.transform.SetParent(root.transform, false);
        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = WalkController(id);
        animator.applyRootMotion = false;

        var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        var cc = root.AddComponent<CharacterController>();
        cc.height = Mathf.Clamp(bounds.size.y * 0.9f, 0.8f, 1.8f);
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, cc.height / 2f + 0.02f, 0f);
        cc.stepOffset = 0.3f;
        cc.slopeLimit = 50f;
        cc.skinWidth = 0.03f;

        Transform Bone(string n) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        var hand = Bone("RightHand");
        var lower = Bone("RightLowerArm");
        var upper = Bone("RightUpperArm");
        Transform mount = null;
        if (hand != null && lower != null)
        {
            mount = new GameObject("WeaponMount").transform;
            Vector3 armDir = (hand.position - lower.position).normalized;
            mount.SetPositionAndRotation(hand.position + armDir * 0.07f, Quaternion.LookRotation(armDir, Vector3.up));
            mount.SetParent(hand, true);
        }
        else Debug.LogWarning(id + ": right arm bones not found, the weapon will not be held");

        root.AddComponent<PlayerStats>();
        Set(root.AddComponent<PlayerController>(), "actions", actions);
        var health = root.AddComponent<PlayerHealth>();
        Set(health, "visualRoot", model.transform.Find(id + "_Rig"));
        root.AddComponent<PlayerDrinks>();
        root.AddComponent<PlayerCarry>();
        var shooter = root.AddComponent<PlayerShooter>();
        var so = new SerializedObject(shooter);
        so.FindProperty("actions").objectReferenceValue = actions;
        so.FindProperty("weaponMount").objectReferenceValue = mount;
        so.FindProperty("startingWeapon").objectReferenceValue = pistol;
        so.FindProperty("hitLayers").intValue = ~((1 << PlayerLayer) | (1 << 2) | (1 << 4));   // water blockers and nets let bullets through
        so.FindProperty("upperArm").objectReferenceValue = upper;
        so.FindProperty("lowerArm").objectReferenceValue = lower;
        so.FindProperty("hand").objectReferenceValue = hand;
        so.ApplyModifiedPropertiesWithoutUndo();
        Set(root.AddComponent<PlayerInteractor>(), "actions", actions);
        Set(root.AddComponent<PlayerAbility>(), "actions", actions);

        // ukulele: on the back (Spine/Chest bone) and a copy in the right hand shown while swinging
        float uke = Mathf.Clamp(cc.height * 0.48f, 0.4f, 0.7f);
        var spine = Bone("Chest") != null ? Bone("Chest") : Bone("Spine");
        GameObject back = null, inHand = null;
        if (spine != null)
        {
            back = BuildUkulele(spine, uke);
            float depth = Mathf.Max(0.12f, bounds.extents.z * 0.75f);
            back.transform.position = spine.position - root.transform.forward * depth + Vector3.up * uke * 0.45f;
            // neck up over the left shoulder, strings facing out
            back.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(-25f, Vector3.forward) * Vector3.down, -Vector3.forward);
        }
        if (mount != null)
        {
            inHand = BuildUkulele(mount, uke);
            inHand.name = "UkuleleInHand";
            inHand.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            inHand.transform.localRotation = Quaternion.identity;
        }
        var melee = root.AddComponent<PlayerMelee>();
        var mso = new SerializedObject(melee);
        mso.FindProperty("actions").objectReferenceValue = actions;
        mso.FindProperty("data").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MeleeData>(DataRoot + "/Weapons/Ukulele.asset");
        mso.FindProperty("backUkulele").objectReferenceValue = back;
        mso.FindProperty("handUkulele").objectReferenceValue = inHand;
        mso.FindProperty("upperArm").objectReferenceValue = upper;
        mso.FindProperty("lowerArm").objectReferenceValue = lower;
        mso.FindProperty("hand").objectReferenceValue = hand;
        mso.ApplyModifiedPropertiesWithoutUndo();

        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PlayerLayer;
        root.tag = "Player";
        return SavePrefab(root, PrefabRoot + "/Players/Player_" + id + ".prefab");
    }

    // Low-poly ukulele from primitives, `length` metres long along local +Z (handle end at 0), strings on +Y.
    static GameObject BuildUkulele(Transform parent, float length)
    {
        var root = new GameObject("Ukulele").transform;
        root.SetParent(parent, false);
        var wood = Mat("Ukulele_Wood", new Color(0.82f, 0.52f, 0.24f));
        var dark = Mat("Ukulele_Dark", new Color(0.3f, 0.17f, 0.08f));
        var hole = Mat("Ukulele_Hole", new Color(0.05f, 0.03f, 0.02f));
        var str = Mat("Ukulele_String", new Color(0.95f, 0.95f, 0.9f));
        float s = length / 0.6f;
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.02f) * s, new Vector3(0.07f, 0.025f, 0.08f) * s, dark);         // headstock
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.17f) * s, new Vector3(0.045f, 0.03f, 0.24f) * s, dark);         // neck
        Prim(PrimitiveType.Sphere, root, new Vector3(0f, 0f, 0.34f) * s, new Vector3(0.17f, 0.065f, 0.16f) * s, wood);       // upper bout
        Prim(PrimitiveType.Sphere, root, new Vector3(0f, 0f, 0.48f) * s, new Vector3(0.22f, 0.07f, 0.21f) * s, wood);        // lower bout
        Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.033f, 0.39f) * s, new Vector3(0.06f, 0.003f, 0.06f) * s, hole); // sound hole
        Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.036f, 0.52f) * s, new Vector3(0.08f, 0.012f, 0.02f) * s, dark);     // bridge
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cube, root, new Vector3((i - 1.5f) * 0.009f, 0.037f, 0.27f) * s, new Vector3(0.0025f, 0.0025f, 0.5f) * s, str);
        return root.gameObject;
    }

    // Player animator: Locomotion blend (Speed: idle 0 / walk 2.5 / run 5), Jump while not Grounded,
    // Death on Die, back to Locomotion on Revive.
    static RuntimeAnimatorController WalkController(string id)
    {
        string fbx = $"Assets/Characters/{id}/{id}.fbx";
        string path = $"Assets/Characters/{id}/{id}_Player.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Revive", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip(fbx, "_Idle"), 0f);
        tree.AddChild(Clip(fbx, "_Walk"), 2.5f);
        tree.AddChild(Clip(fbx, "_Run"), 5f);
        var jump = sm.AddState("Jump"); jump.motion = Clip(fbx, "_Jump");
        var death = sm.AddState("Death"); death.motion = Clip(fbx, "_Death");
        sm.defaultState = loco;
        var t = loco.AddTransition(jump); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); t.hasExitTime = false; t.duration = 0.1f;
        t = jump.AddTransition(loco); t.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); t.hasExitTime = false; t.duration = 0.12f;
        var d = sm.AddAnyStateTransition(death); d.AddCondition(AnimatorConditionMode.If, 0, "Die"); d.hasExitTime = false; d.duration = 0.1f; d.canTransitionToSelf = false;
        t = death.AddTransition(loco); t.AddCondition(AnimatorConditionMode.If, 0, "Revive"); t.hasExitTime = false; t.duration = 0.3f;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    internal static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ materials / primitives
    internal static Material Mat(string name, Color color, float emission = 0f)
    {
        string path = DataRoot + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Directory.CreateDirectory(DataRoot + "/Materials");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
        }
        if (color.a < 1f)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static readonly string[] Walkable = { "Terrain", "Dock", "Stairs", "Bridge", "Temple", "CliffBlock", "Ruin", "PathStone" };

    // Highest walkable surface under (x, z): terrain, dock, stairs... (trees, rocks and props are ignored).
    static Vector3 Ground(Vector2 xz)
    {
        var hits = Physics.RaycastAll(new Vector3(xz.x, 40f, xz.y), Vector3.down, 80f, 1 << 0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits)
        {
            string n = h.collider.transform.root == h.collider.transform ? h.collider.name : FindEnvName(h.collider.transform);
            if (Walkable.Any(w => n.StartsWith(w)) && h.point.y > best) best = h.point.y;
        }
        return new Vector3(xz.x, best > float.NegativeInfinity ? best : 0.3f, xz.y);
    }

    // name of the placed island object (child of an Environment group) that owns this collider
    static string FindEnvName(Transform t)
    {
        while (t.parent != null && t.parent.parent != null && t.parent.parent.name != "Environment") t = t.parent;
        return t.name;
    }

    internal static GameObject Child(Transform parent, string name, Vector3 pos, float rotY = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, rotY, 0);
        return go;
    }

    // ------------------------------------------------------------------ game scene
    static void BuildGameScene(Data data, GameObject enemyPrefab, BossAI bossPrefab, Carryable discPrefab)
    {
        foreach (var n in new[] { "TestMap", "Navigation", "HUD", "EnemySpawner_Graveyard", "BeachIsland/Characters" })
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }
        Physics.SyncTransforms();

        var env = GameObject.Find("BeachIsland/Environment");
        if (env != null) Debug.Log(OriginalMapColliders.Apply(env.transform));

        var map = new GameObject("TestMap").transform;
        var systems = Child(map, "Systems", Vector3.zero).transform;

        // zones
        var zonesRoot = Child(map, "Zones", Vector3.zero).transform;
        var zones = new Dictionary<string, Zone>();
        foreach (var zd in ZoneDefs)
        {
            var zgo = Child(zonesRoot, "Zone_" + zd.id, Vector3.zero);
            var zone = zgo.AddComponent<Zone>();
            zone.displayName = zd.name;
            zone.openAtStart = zd.start;
            var sp = Child(zgo.transform, "SpawnPoints", Vector3.zero).transform;
            for (int i = 0; i < zd.spawns.Length; i++) Child(sp, "Spawn" + i, Ground(zd.spawns[i]));
            var bs = Child(zgo.transform, "BoxSpots", Vector3.zero).transform;
            for (int i = 0; i < zd.boxSpots.Length; i++) Child(bs, "Box" + i, Ground(zd.boxSpots[i]), 90f);
            zones[zd.id] = zone;
        }

        // spawn entrances: a visible cabin at every spawn point (before the borders / NavMesh: it is solid)
        int cabins = OriginalMapEntrances.Build(Child(map, "Entradas", Vector3.zero).transform, ZoneDefs.Select(z => zones[z.id]));
        Debug.Log("[TestMap] " + OriginalMapEntrances.LastReport);

        // walls
        var wallsRoot = Child(map, "Walls", Vector3.zero).transform;
        var rock = Model("Assets/BeachIsland/Models/CliffBlock_A.fbx");
        int wi = 0;
        foreach (var line in Walls)
            for (int i = 0; i < line.Length - 1; i++) BuildWall(wallsRoot, "Wall" + (wi++), line[i], line[i + 1], rock);

        // doors
        var doorsRoot = Child(map, "Doors", Vector3.zero).transform;
        var towel = new[] { Mat("Towel_Red", new Color(0.9f, 0.2f, 0.2f)), Mat("Towel_Blue", new Color(0.2f, 0.45f, 0.9f)), Mat("Towel_Yellow", new Color(1f, 0.8f, 0.2f)) };
        var pole = Mat("Pole", new Color(0.45f, 0.3f, 0.15f));
        var doors = new List<ZoneDoor>();
        for (int i = 0; i < DoorDefs.Length; i++)
        {
            var dd = DoorDefs[i];
            Vector3 doorPos = Ground(dd.pos);
            float doorRot = dd.rotY, doorWidth = dd.width > 0f ? dd.width : 4.6f;
            // a door at the bottom of stairs goes square across their foot (a skewed one left the side of the
            // stairs open to the zone below)
            foreach (var f in OriginalMapColliders.Feet)
                if (new Vector2(f.foot.x - doorPos.x, f.foot.z - doorPos.z).magnitude < 3.5f)
                {
                    doorPos = f.foot - f.up * 0.5f;
                    doorRot = Quaternion.LookRotation(f.up).eulerAngles.y;
                    doorWidth = Mathf.Max(doorWidth, f.width + 0.6f);
                    break;
                }
            var door = BuildDoor(doorsRoot, "Door_" + dd.a + "_" + dd.b, dd.name, new[] { zones[dd.a], zones[dd.b] }, doorPos, doorRot, dd.cost,
                doorWidth, pole, dd.b == "Muelle" ? Mat("Towel_Boss", new Color(0.6f, 0.1f, 0.5f)) : towel[i % towel.Length], obstacle: false);
            if (dd.b == "Muelle")   // the dock opens with the boat mission, not with conchas
            {
                var dso = new SerializedObject(door);
                dso.FindProperty("requiresMission").boolValue = true;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
            // as tall as the walls: nobody jumps over a closed door (the Frog's super jump reaches ~5 m)
            var box = door.GetComponentInChildren<BoxCollider>();
            box.center = new Vector3(0f, (OriginalMapBorders.WallTop + OriginalMapBorders.WallBottom) / 2f - door.transform.position.y, 0f);
            box.size = new Vector3(box.size.x, OriginalMapBorders.WallTop - OriginalMapBorders.WallBottom, box.size.z);
            doors.Add(door);
        }

        // shop
        var shop = Child(map, "Shop", Vector3.zero).transform;
        var box0 = BuildMysteryBox(shop, data.boxWeapons, Ground(ZoneDefs[0].boxSpots[0]));
        var drinkSpots = new[] { new Vector2(16.5f, -12.5f), new Vector2(18.5f, 2.5f), new Vector2(-8, 11), new Vector2(-23, -12) };
        for (int i = 0; i < data.drinks.Length; i++) BuildDrinkMachine(shop, data.drinks[i], Ground(drinkSpots[i]));
        BuildPackAPunch(shop, Ground(new Vector2(-6, -8)));

        // boat mission: light in the Jungla (the far end), petrol on the Playa, radio in the Cementerio, call from the
        // Campamento (next to the dock door)
        var mission = Child(map, "Mision", Vector3.zero).transform;
        BuildPowerSwitch(mission, ClearSpot(new Vector2(-28, -3)), Vector3.zero);
        BuildMissionItem(mission, MissionItem.Kind.Gasolina, ClearSpot(new Vector2(8, 2)));
        BuildMissionItem(mission, MissionItem.Kind.Radio, ClearSpot(new Vector2(-9, -6)));
        BuildCallStation(mission, ClearSpot(new Vector2(-4, 14)), Vector3.zero);

        // easter egg
        var eggRoot = Child(map, "EasterEgg", Vector3.zero).transform;
        var discMat = Mat("Vinyl", new Color(0.05f, 0.05f, 0.05f));
        foreach (var p in new[] { new Vector2(21, 4), new Vector2(-5.5f, -17.5f), new Vector2(-26, -12) })
        {
            var d = Child(eggRoot, "JulioDisc", Ground(p) + Vector3.up * 0.5f);
            var v = Prim(PrimitiveType.Cylinder, d.transform, Vector3.zero, new Vector3(0.35f, 0.012f, 0.35f), discMat);
            v.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cylinder, v.transform, Vector3.zero, new Vector3(0.35f, 1.2f, 0.35f), Mat("Gold", new Color(1f, 0.8f, 0.1f), 0.8f));
            d.AddComponent<DiscPickup>();
        }
        BuildKaraoke(eggRoot, Ground(new Vector2(10.5f, -19.5f)), discPrefab);

        // boss + extraction
        var bossSpawn = Child(map, "BossSpawn", Ground(new Vector2(12, 15)), 220f);
        var extraction = Child(map, "Extraction", Ground(new Vector2(18.2f, 24.6f)));
        var beacon = Prim(PrimitiveType.Cylinder, extraction.transform, new Vector3(0, 15, 0), new Vector3(0.6f, 15f, 0.6f), Mat("Beacon", new Color(0.2f, 1f, 0.4f, 0.35f), 2f));
        var ext = extraction.AddComponent<ExtractionPoint>();
        Set(ext, "beacon", beacon);
        WorldLabel.Create(extraction.transform, new Vector3(0, 2.2f, 0), "BARCO DE RESCATE", 0.3f, new Color(0.6f, 1f, 0.6f));

        var spawn = Child(map, "PlayerSpawn", Ground(new Vector2(12, -13)), 0f);

        BuildSystems(systems.gameObject, data, spawn.transform, zones["Muelle"], bossSpawn.transform, enemyPrefab, bossPrefab);
        systems.gameObject.AddComponent<MapInfo>();   // pause-menu map (filled in after the NavMesh bake)

        // sealed zones: invisible walls along every border (and around the island, a little into the sea),
        // rocks where a border crosses open ground
        var zoneArr = ZoneDefs.Select(z => zones[z.id]).ToArray();
        var borders = OriginalMapBorders.Compute(zoneArr, doors.ToArray());
        if (OriginalMapEntrances.PruneAcrossBorders(borders, zoneArr) > 0)
        {
            Physics.SyncTransforms();
            borders = OriginalMapBorders.Compute(zoneArr, doors.ToArray());   // the removed cabins no longer count as obstacles
            Debug.Log("[TestMap] " + OriginalMapEntrances.LastReport);
        }
        OriginalMapBorders.Build(Child(wallsRoot, "Bordes", Vector3.zero).transform, borders);
        var exposed = OriginalMapBorders.Exposed(borders, doors.ToArray(), 1.6f);
        var rocks = new[] { "CliffBlock_A", "CliffBlock_B", "CliffBlock_C" }.Select(n => Model("Assets/BeachIsland/Models/" + n + ".fbx")).Where(m => m != null).ToArray();
        int placed = OriginalMapBorders.Decorate(Child(wallsRoot, "RocasBorde", Vector3.zero).transform, exposed, rocks);
        var flanks = OriginalMapBorders.Flanks(borders, doors.ToArray(), 1.0f);
        int fences = OriginalMapBorders.DecorateFlanks(Child(wallsRoot, "VallasPuerta", Vector3.zero).transform, flanks, 1.0f);
        OriginalMapBorders.Preview(borders, "Renders/Original_borders.png", exposed);
        var lidRoot = Child(wallsRoot, "Tapas", Vector3.zero).transform;
        int lids = OriginalMapAudit.FillTraps(lidRoot, 1.3f, 3.5f) + OriginalMapAudit.FillTraps(lidRoot, 5.3f, 6f);   // normal jump, Frog
        OriginalMapBorders.LastReport = borders.report + $"{placed} rocas en tramos abiertos, {lids} tapas en huecos sin salida\n";
        Debug.Log(OriginalMapBorders.LastReport);

        // bake; a cabin can plug a narrow lane and cut a pocket off: remove the cabins near any point left isolated
        // and bake again (at most 3 times)
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var isolated = BakeNavMesh(zones, spawn.transform.position);
            if (isolated.Count == 0) break;
            int gone = OriginalMapEntrances.RemoveNear(isolated, 5f);
            Debug.Log($"[TestMap] bake {attempt + 1}: {isolated.Count} puntos aislados, {gone} casetas quitadas cerca");
            // FixIslands may also have dropped cabins: always bake again (3 attempts at most)
            Physics.SyncTransforms();
        }
        // the mission objects must be reachable from the start (doors still open on the mesh here)
        {
            var mpath = new NavMeshPath();
            NavMesh.SamplePosition(spawn.transform.position, out var ms, 3f, NavMesh.AllAreas);
            foreach (Transform m in mission)
            {
                bool ok = NavMesh.SamplePosition(m.position, out var mh, 2f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(ms.position, mh.position, NavMesh.AllAreas, mpath) && mpath.status == NavMeshPathStatus.PathComplete;
                if (ok) Debug.Log($"[TestMap] misión: {m.name} en {m.position} alcanzable");
                else Debug.LogError($"[TestMap] misión: {m.name} en {m.position} NO es alcanzable desde el inicio");
            }
        }
        // the carving door obstacles go in last: they would cut the NavMesh in every path check above
        foreach (var d in doors)
        {
            var bx = d.GetComponentInChildren<BoxCollider>();
            AddDoorObstacle(bx.gameObject, bx.size.x);
        }
        RenderOriginalPreviewCore();
    }

    // A paid barrier (posts + cloth) that opens `connects` when bought. The barrier carves the NavMesh while closed.
    internal static ZoneDoor BuildDoor(Transform parent, string objName, string doorName, Zone[] connects, Vector3 pos, float rotY, int cost, float width, Material pole, Material cloth, bool obstacle = true)
    {
        var door = Child(parent, objName, pos, rotY);
        var barrier = Child(door.transform, "Barrier", pos, rotY);
        barrier.layer = DoorLayer;
        Prim(PrimitiveType.Cylinder, barrier.transform, new Vector3(-width / 2f + 0.2f, 1.2f, 0), new Vector3(0.15f, 1.2f, 0.15f), pole);
        Prim(PrimitiveType.Cylinder, barrier.transform, new Vector3(width / 2f - 0.2f, 1.2f, 0), new Vector3(0.15f, 1.2f, 0.15f), pole);
        Prim(PrimitiveType.Cube, barrier.transform, new Vector3(0, 1.3f, 0), new Vector3(width - 0.4f, 1.9f, 0.08f), cloth);
        var box = barrier.AddComponent<BoxCollider>();
        box.center = new Vector3(0, 2.5f, 0);
        box.size = new Vector3(width, 5f, 0.8f);
        if (obstacle) AddDoorObstacle(barrier, width);
        foreach (Transform t in barrier.transform) t.gameObject.layer = DoorLayer;
        var label = WorldLabel.Create(door.transform, new Vector3(0, 3.3f, 0), doorName, 0.28f, Color.white);
        var zd = door.AddComponent<ZoneDoor>();
        var so = new SerializedObject(zd);
        so.FindProperty("doorName").stringValue = doorName;
        so.FindProperty("cost").intValue = cost;
        so.FindProperty("barrier").objectReferenceValue = barrier;
        so.FindProperty("worldLabel").objectReferenceValue = label;
        so.FindProperty("interactRadius").floatValue = 3.2f;
        var c = so.FindProperty("connects");
        c.arraySize = connects.Length;
        for (int i = 0; i < connects.Length; i++) c.GetArrayElementAtIndex(i).objectReferenceValue = connects[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return zd;
    }

    // Carving obstacle that keeps enemies out while the door is closed. It also carves in the editor,
    // so maps that check paths through doors add it after the check.
    internal static void AddDoorObstacle(GameObject barrier, float width)
    {
        var obstacle = barrier.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0, 2.5f, 0);
        obstacle.size = new Vector3(width, 5f, 1.2f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
    }

    // Camera rig + GameManager, RoundManager, zones, cards, easter egg, power-ups, boss encounter and HUD on `gmObj`.
    internal static void BuildSystems(GameObject gmObj, Data data, Transform spawn, Zone bossZone, Transform bossSpawn, GameObject enemyPrefab, BossAI bossPrefab)
    {
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        var cam = Camera.main;
        var camRig = cam.GetComponent<ThirdPersonCamera>();
        if (camRig == null) camRig = cam.gameObject.AddComponent<ThirdPersonCamera>();
        var cso = new SerializedObject(camRig);
        cso.FindProperty("target").objectReferenceValue = null;
        cso.FindProperty("actions").objectReferenceValue = actions;
        cso.FindProperty("collisionLayers").intValue = ~((1 << PlayerLayer) | (1 << EnemyLayer) | (1 << 2) | (1 << 4));
        cso.ApplyModifiedPropertiesWithoutUndo();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1000f;
        cam.transform.position = spawn.transform.position + new Vector3(0, 2.5f, -4f);

        var gm = gmObj.AddComponent<GameManager>();
        var gso = new SerializedObject(gm);
        var arr = gso.FindProperty("animals");
        arr.arraySize = data.animals.Length;
        for (int i = 0; i < data.animals.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = data.animals[i];
        gso.FindProperty("playerSpawn").objectReferenceValue = spawn.transform;
        gso.FindProperty("cameraRig").objectReferenceValue = camRig;
        gso.FindProperty("actions").objectReferenceValue = actions;
        gso.FindProperty("economy").objectReferenceValue = data.economy;
        gso.FindProperty("coop").objectReferenceValue = data.coop;
        gso.ApplyModifiedPropertiesWithoutUndo();

        var rm = gmObj.AddComponent<RoundManager>();
        var rso = new SerializedObject(rm);
        rso.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
        rso.FindProperty("baseVariant").objectReferenceValue = data.baseVariant;
        var sv = rso.FindProperty("specialVariants");
        sv.arraySize = data.specials.Length;
        for (int i = 0; i < data.specials.Length; i++) sv.GetArrayElementAtIndex(i).objectReferenceValue = data.specials[i];
        rso.FindProperty("nightVariant").objectReferenceValue = data.nightVariant;
        rso.FindProperty("tunnels").objectReferenceValue = data.tunnels;
        rso.ApplyModifiedPropertiesWithoutUndo();

        gmObj.AddComponent<ZoneManager>();
        var cm = gmObj.AddComponent<CardManager>();
        var cmso = new SerializedObject(cm);
        var pool = cmso.FindProperty("pool");
        pool.arraySize = data.cards.Length;
        for (int i = 0; i < data.cards.Length; i++) pool.GetArrayElementAtIndex(i).objectReferenceValue = data.cards[i];
        cmso.ApplyModifiedPropertiesWithoutUndo();
        gmObj.AddComponent<EasterEgg>();
        var pum = gmObj.AddComponent<PowerUpManager>();
        Set(pum, "data", Asset<PowerUpData>(DataRoot + "/PowerUps.asset", x => { }));
        gmObj.AddComponent<BoatMission>();
        var be = gmObj.AddComponent<BossEncounter>();
        var bso = new SerializedObject(be);
        bso.FindProperty("triggerZone").objectReferenceValue = bossZone;
        bso.FindProperty("bossPrefab").objectReferenceValue = bossPrefab;
        bso.FindProperty("spawnPoint").objectReferenceValue = bossSpawn.transform;
        bso.ApplyModifiedPropertiesWithoutUndo();
        gmObj.AddComponent<GameHUD>();
        gmObj.AddComponent<RunLog>();
        gmObj.AddComponent<DayNightCycle>();
        gmObj.AddComponent<DebugMenu>();
    }

    static void BuildWall(Transform parent, string name, Vector2 a, Vector2 b, GameObject rock)
    {
        Vector3 pa = Ground(a), pb = Ground(b);
        Vector3 mid = (pa + pb) / 2f;
        Vector3 dir = new Vector3(b.x - a.x, 0, b.y - a.y);
        float len = dir.magnitude;
        var wall = Child(parent, name, new Vector3(mid.x, 0f, mid.z), Quaternion.LookRotation(dir).eulerAngles.y);
        wall.layer = WallLayer;
        var col = wall.AddComponent<BoxCollider>();   // invisible, very tall: cannot be jumped (not even by the Frog)
        col.center = new Vector3(0, (OriginalMapBorders.WallTop + OriginalMapBorders.WallBottom) / 2f, 0);
        col.size = new Vector3(1.2f, OriginalMapBorders.WallTop - OriginalMapBorders.WallBottom, len + 1f);
        if (rock == null) return;
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 2.2f));
        for (int i = 0; i <= n; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)n);
            var g = Ground(p);
            var r = (GameObject)PrefabUtility.InstantiatePrefab(rock, wall.transform);
            r.transform.position = g + Vector3.down * 0.3f;
            r.transform.rotation = Quaternion.Euler(0, (i * 73) % 360, 0);
            r.transform.localScale = Vector3.one * (1.25f + 0.2f * ((i * 37) % 3));
            r.layer = WallLayer;
            foreach (var c in r.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }
    }

    internal static MysteryBox BuildMysteryBox(Transform parent, WeaponData[] pool, Vector3 pos)
    {
        var go = Child(parent, "MysteryBox", pos, 90f);
        var visual = Child(go.transform, "Visual", pos, 90f);
        Prim(PrimitiveType.Cube, visual.transform, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.8f), Mat("Box_Body", new Color(0.2f, 0.75f, 0.9f)));
        Prim(PrimitiveType.Cube, visual.transform, new Vector3(0, 1.0f, 0), new Vector3(1.2f, 0.2f, 0.6f), Mat("Box_Top", new Color(1f, 0.4f, 0.7f), 0.5f));
        Prim(PrimitiveType.Cylinder, visual.transform, new Vector3(0, 15f, 0), new Vector3(0.35f, 14f, 0.35f), Mat("Box_Beam", new Color(0.4f, 0.9f, 1f, 0.25f), 1.5f));
        var label = WorldLabel.Create(go.transform, new Vector3(0, 1.8f, 0), "", 0.28f, new Color(0.7f, 0.95f, 1f));
        var roll = WorldLabel.Create(go.transform, new Vector3(0, 1.35f, 0), "", 0.32f, Color.white);
        var box = go.AddComponent<MysteryBox>();
        var so = new SerializedObject(box);
        var p = so.FindProperty("pool");
        p.arraySize = pool.Length;
        for (int i = 0; i < pool.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("worldLabel").objectReferenceValue = label;
        so.FindProperty("rollLabel").objectReferenceValue = roll;
        so.ApplyModifiedPropertiesWithoutUndo();
        return box;
    }

    // Weapon on a wall: a board on the wall face (toWall = unit vector from `floor` towards the wall,
    // `faceDist` = distance from `floor` to the wall face) with the gun model and its price.
    internal static WallWeapon BuildWallWeapon(Transform parent, WeaponData weapon, Vector3 floor, Vector3 toWall, float faceDist)
    {
        Vector3 face = floor + toWall * faceDist;
        Quaternion rot = Quaternion.LookRotation(-toWall);   // board faces the floor
        Vector3 right = rot * Vector3.right;
        var go = Child(parent, "WallWeapon_" + weapon.id, floor + toWall * (faceDist - 0.6f));
        go.transform.rotation = rot;
        var board = Child(go.transform, "Tablon", face);
        board.transform.rotation = rot;
        // local +z = out of the wall
        Prim(PrimitiveType.Cube, board.transform, new Vector3(0, 1.55f, 0.04f), new Vector3(1.9f, 1.05f, 0.08f), Mat("WallBuy_Frame", new Color(1f, 0.72f, 0.2f)));
        Prim(PrimitiveType.Cube, board.transform, new Vector3(0, 1.55f, 0.09f), new Vector3(1.75f, 0.9f, 0.04f), Mat("WallBuy_Board", new Color(0.32f, 0.2f, 0.12f)));
        if (weapon.model != null)
        {
            var gun = (GameObject)PrefabUtility.InstantiatePrefab(weapon.model, board.transform);
            foreach (var c in gun.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            gun.transform.rotation = Quaternion.LookRotation(right, Vector3.up);
            gun.transform.localScale = Vector3.one * 1.3f;
            var b = new Bounds(gun.transform.position, Vector3.zero);
            foreach (var r in gun.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            gun.transform.position += face + Vector3.up * 1.62f - toWall * 0.2f - b.center;
        }
        var label = WorldLabel.Create(go.transform, Vector3.zero, "", 0.24f, new Color(1f, 0.8f, 0.3f));
        label.transform.position = face - toWall * 0.15f + Vector3.up * 2.4f;
        var ww = go.AddComponent<WallWeapon>();
        var so = new SerializedObject(ww);
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.FindProperty("worldLabel").objectReferenceValue = label;
        so.FindProperty("interactRadius").floatValue = 2f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ww;
    }

    // ------------------------------------------------------------------ boat mission objects (no colliders: thin
    // posts / floating pickups, they never close a lane). `toWall` = flat direction to something solid beside the
    // spot (or zero): posts stand against it and face away from it.
    static Transform MissionRoot(Transform parent, string name, Vector3 pos, Vector3 toWall, float back)
    {
        var go = Child(parent, name, toWall == Vector3.zero ? pos : pos + toWall * back);
        if (toWall != Vector3.zero) go.transform.rotation = Quaternion.LookRotation(-toWall);
        return go.transform;
    }

    internal static PowerSwitch BuildPowerSwitch(Transform parent, Vector3 pos, Vector3 toWall)
    {
        var t = MissionRoot(parent, "CuadroElectrico", pos, toWall, 0.75f);
        var grey = Mat("Cuadro_Gris", new Color(0.55f, 0.57f, 0.6f));
        Prim(PrimitiveType.Cube, t, new Vector3(0, 0.65f, 0), new Vector3(0.12f, 1.3f, 0.12f), grey);
        Prim(PrimitiveType.Cube, t, new Vector3(0, 1.5f, 0), new Vector3(0.7f, 0.9f, 0.22f), grey);
        Prim(PrimitiveType.Cube, t, new Vector3(0, 1.5f, 0.115f), new Vector3(0.5f, 0.6f, 0.02f), Mat("Cuadro_Aviso", new Color(1f, 0.85f, 0.1f)));
        var lamp = Prim(PrimitiveType.Sphere, t, new Vector3(0, 2.05f, 0), Vector3.one * 0.2f, Mat("Cuadro_Luz", new Color(1f, 0.15f, 0.1f), 1.5f));
        var pivot = Child(t, "Palanca", t.TransformPoint(new Vector3(0.22f, 1.5f, 0.14f))).transform;
        pivot.rotation = t.rotation;
        Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0.12f, 0.03f), new Vector3(0.05f, 0.28f, 0.05f), Mat("Cuadro_Palanca", new Color(0.8f, 0.1f, 0.1f)));
        var label = WorldLabel.Create(t, new Vector3(0, 2.6f, 0), "CUADRO ELÉCTRICO\nDAR LA LUZ", 0.25f, new Color(1f, 0.9f, 0.3f));
        var ps = t.gameObject.AddComponent<PowerSwitch>();
        Set(ps, "lamp", lamp.GetComponent<Renderer>());
        Set(ps, "lever", pivot);
        Set(ps, "worldLabel", label);
        return ps;
    }

    internal static MissionItem BuildMissionItem(Transform parent, MissionItem.Kind kind, Vector3 pos)
    {
        bool gas = kind == MissionItem.Kind.Gasolina;
        var go = Child(parent, gas ? "Gasolina" : "Radio", pos);
        var visual = Child(go.transform, "Visual", pos + Vector3.up * 0.8f).transform;
        Color glowColor;
        if (gas)
        {
            var red = Mat("Bidon_Rojo", new Color(0.85f, 0.12f, 0.1f));
            Prim(PrimitiveType.Cube, visual, Vector3.zero, new Vector3(0.38f, 0.5f, 0.2f), red);
            Prim(PrimitiveType.Cylinder, visual, new Vector3(0.1f, 0.3f, 0), new Vector3(0.08f, 0.06f, 0.08f), Mat("Bidon_Tapon", new Color(0.1f, 0.1f, 0.1f)));
            Prim(PrimitiveType.Cube, visual, new Vector3(-0.06f, 0.3f, 0), new Vector3(0.16f, 0.04f, 0.06f), red);
            glowColor = new Color(1f, 0.4f, 0.2f);
        }
        else
        {
            var dark = Mat("Radio_Negro", new Color(0.12f, 0.13f, 0.15f));
            Prim(PrimitiveType.Cube, visual, Vector3.zero, new Vector3(0.5f, 0.3f, 0.16f), dark);
            Prim(PrimitiveType.Cylinder, visual, new Vector3(-0.12f, 0f, 0.085f), new Vector3(0.18f, 0.01f, 0.18f), Mat("Radio_Altavoz", new Color(0.6f, 0.6f, 0.62f))).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cylinder, visual, new Vector3(0.2f, 0.4f, 0), new Vector3(0.02f, 0.3f, 0.02f), Mat("Inox", new Color(0.75f, 0.77f, 0.8f)));
            glowColor = new Color(0.4f, 0.8f, 1f);
        }
        var glow = new GameObject("Brillo").AddComponent<Light>();
        glow.transform.SetParent(visual, false);
        glow.type = LightType.Point; glow.range = 3.5f; glow.intensity = 2.5f; glow.color = glowColor;
        var label = WorldLabel.Create(go.transform, new Vector3(0, 1.7f, 0), gas ? "GASOLINA" : "RADIO", 0.28f, Color.Lerp(glowColor, Color.white, 0.4f));
        var mi = go.AddComponent<MissionItem>();
        var so = new SerializedObject(mi);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("worldLabel").objectReferenceValue = label;
        so.FindProperty("interactRadius").floatValue = 2f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return mi;
    }

    internal static CallStation BuildCallStation(Transform parent, Vector3 pos, Vector3 toWall)
    {
        var t = MissionRoot(parent, "Emisora", pos, toWall, 0.7f);
        var wood = Mat("Pole", new Color(0.45f, 0.3f, 0.15f));
        Prim(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.35f), wood);
        Prim(PrimitiveType.Cube, t, new Vector3(0, 1.08f, 0), new Vector3(0.6f, 0.35f, 0.28f), Mat("Radio_Negro", new Color(0.12f, 0.13f, 0.15f)));
        Prim(PrimitiveType.Cylinder, t, new Vector3(0.35f, 2.0f, 0), new Vector3(0.04f, 1.1f, 0.04f), Mat("Inox", new Color(0.75f, 0.77f, 0.8f)));
        Prim(PrimitiveType.Sphere, t, new Vector3(0.35f, 3.15f, 0), Vector3.one * 0.14f, Mat("Cuadro_Luz", new Color(1f, 0.15f, 0.1f), 1.5f));
        var label = WorldLabel.Create(t, new Vector3(0, 3.5f, 0), "EMISORA\nLLAMAR AL BARCO", 0.25f, new Color(0.6f, 0.9f, 1f));
        var cs = t.gameObject.AddComponent<CallStation>();
        Set(cs, "worldLabel", label);
        return cs;
    }

    // Original map: the nearest dry spot around (x, z) with room for a person and nothing solid in it.
    static Vector3 ClearSpot(Vector2 xz)
    {
        Physics.SyncTransforms();
        int mask = (1 << 0) | (1 << WallLayer) | (1 << PropLayer) | (1 << DoorLayer);
        for (float rad = 0f; rad <= 8f; rad += 1f)
            for (int a = 0; a < 360; a += (rad == 0f ? 360 : 30))
            {
                var c = Ground(xz + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * rad);
                if (c.y < 0.12f || c.y > 3.2f) continue;   // dry, and not on top of the temple / a ruin (the island's floors are 0-2.7 m)
                if (Physics.CheckCapsule(c + Vector3.up * 0.85f, c + Vector3.up * 1.7f, 0.6f, mask, QueryTriggerInteraction.Ignore)) continue;
                return c;
            }
        Debug.LogError("[TestMap] no clear spot near " + xz);
        return Ground(xz);
    }

    internal static void BuildDrinkMachine(Transform parent, DrinkData drink, Vector3 pos)
    {
        var go = Child(parent, "Drink_" + drink.id, pos);
        var body = Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.9f, 0), new Vector3(0.9f, 1.8f, 0.7f), Mat("Drink_" + drink.id, drink.color, 0.3f), true);
        body.layer = 0;
        Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 2.0f, 0), new Vector3(0.35f, 0.2f, 0.35f), Mat("Glass", new Color(1, 1, 1, 0.6f)));
        var label = WorldLabel.Create(go.transform, new Vector3(0, 2.6f, 0), drink.displayName, 0.25f, drink.color);
        var m = go.AddComponent<DrinkMachine>();
        var so = new SerializedObject(m);
        so.FindProperty("drink").objectReferenceValue = drink;
        so.FindProperty("worldLabel").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void BuildPackAPunch(Transform parent, Vector3 pos)
    {
        var go = Child(parent, "PackAPunch_Microondas", pos);
        var body = Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 0.9f), Mat("Microwave", new Color(0.85f, 0.85f, 0.85f)), true);
        body.layer = 0;
        Prim(PrimitiveType.Cube, go.transform, new Vector3(-0.15f, 0.65f, -0.46f), new Vector3(0.9f, 0.7f, 0.02f), Mat("MicrowaveGlass", new Color(1f, 0.6f, 0.1f), 1.2f));
        var label = WorldLabel.Create(go.transform, new Vector3(0, 2.0f, 0), "", 0.25f, new Color(1f, 0.7f, 0.3f));
        var pap = go.AddComponent<PackAPunch>();
        Set(pap, "worldLabel", label);
    }

    internal static void BuildKaraoke(Transform parent, Vector3 pos, Carryable discPrefab)
    {
        var go = Child(parent, "Karaoke", pos, 180f);
        var body = Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0.7f, 0), new Vector3(0.8f, 1.4f, 0.6f), Mat("Karaoke", new Color(0.55f, 0.2f, 0.8f), 0.4f), true);
        body.layer = 0;
        Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0.6f, 0.75f, 0), new Vector3(0.05f, 0.75f, 0.05f), Mat("Pole", new Color(0.45f, 0.3f, 0.15f)));
        var label = WorldLabel.Create(go.transform, new Vector3(0, 2.0f, 0), "KARAOKE", 0.25f, new Color(0.85f, 0.6f, 1f));
        var reward = Child(go.transform, "RewardPoint", pos + go.transform.forward * 1.3f + Vector3.up * 0.6f);
        var k = go.AddComponent<Karaoke>();
        var so = new SerializedObject(k);
        so.FindProperty("goldenDiscPrefab").objectReferenceValue = discPrefab;
        so.FindProperty("rewardPoint").objectReferenceValue = reward.transform;
        so.FindProperty("worldLabel").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ navigation
    // baked a little fatter than the old men (0.35) so paths keep clear of rock edges at stair tops: the player
    // capsule (0.3) was stopped by slanted rock faces the 0.35 mesh ran right along
    const float OriginalBakeRadius = 0.35f;

    static List<Vector3> BakeNavMesh(Dictionary<string, Zone> zones, Vector3 start)
    {
        var oldNav = GameObject.Find("TestMap/Navigation");
        if (oldNav != null) Object.DestroyImmediate(oldNav);
        var navSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        var agent0 = navSettings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
        agent0.FindPropertyRelative("agentRadius").floatValue = OriginalBakeRadius;
        agent0.FindPropertyRelative("agentHeight").floatValue = 1.4f;
        // this map is baked with the player's step (0.3): the old men only walk where the player can walk (with
        // 0.45 their paths cut over rock edges / temple steps the player has to jump). Isla del Faro keeps 0.45
        // (its props are >= 0.5 m), restored below.
        agent0.FindPropertyRelative("agentClimb").floatValue = 0.28f;
        agent0.FindPropertyRelative("agentSlope").floatValue = 45f;
        navSettings.ApplyModifiedProperties();

        foreach (var t in Object.FindObjectsByType<Transform>())
            if (t.name.StartsWith("Grave_Gate")) foreach (var c in t.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

        var go = new GameObject("Navigation");
        go.transform.SetParent(GameObject.Find("TestMap").transform, false);
        // the sea floor is not walkable
        var sea = go.AddComponent<NavMeshModifierVolume>();
        sea.center = new Vector3(-6f, -5.05f, -3f);
        sea.size = new Vector3(120f, 10f, 120f);
        sea.area = 1;
        // two slanted rock lips (0.2 m high) at the top of the Cinta de obra and Escalera del jardinero stairs: the mesh
        // crosses them (climb 0.28) but the player capsule is stopped by their face, so they are cut out of the mesh
        foreach (var t in Object.FindObjectsByType<Transform>())
            if (t.name == "CliffBlock_A_11" || t.name == "CliffBlock_A_02")
            {
                var cols = t.GetComponentsInChildren<Collider>();
                if (cols.Length == 0) continue;
                var bb = cols[0].bounds;
                foreach (var c in cols) bb.Encapsulate(c.bounds);
                var cut = new GameObject("SinMalla_" + t.name);
                cut.transform.SetParent(go.transform, false);
                cut.transform.position = bb.center;
                var vol = cut.AddComponent<NavMeshModifierVolume>();
                vol.size = bb.size + new Vector3(0.2f, 0.6f, 0.2f);
                vol.area = 1;
            }
        var surface = go.AddComponent<NavMeshSurface>();
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // 1) bake WITH the doors to check that every zone is sealed off
        surface.layerMask = (1 << 0) | (1 << WallLayer) | (1 << DoorLayer) | (1 << PropLayer);
        surface.BuildNavMesh();
        var report = new List<string>();
        var path = new NavMeshPath();
        NavMesh.SamplePosition(start, out var s, 3f, NavMesh.AllAreas);
        foreach (var kv in zones)
        {
            var pts = kv.Value.SpawnPoints.ToList();
            int reachable = 0;
            foreach (var p in pts)
                if (NavMesh.SamplePosition(p.position, out var h, 3f, NavMesh.AllAreas) && NavMesh.CalculatePath(s.position, h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    reachable++;
            report.Add($"{kv.Key}: {reachable}/{pts.Count} reachable with doors closed{(kv.Value.openAtStart ? " (start zone)" : "")}");
        }
        Debug.Log("[TestMap] Zone isolation check:\n" + string.Join("\n", report));

        // 2) final bake WITHOUT doors (they block at runtime with carving NavMeshObstacles)
        surface.layerMask = (1 << 0) | (1 << WallLayer) | (1 << PropLayer);
        surface.BuildNavMesh();
        const string dataPath = "Assets/Scenes/BeachIsland_NavMesh.asset";
        AssetDatabase.DeleteAsset(dataPath);
        AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
        surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(dataPath);
        EditorUtility.SetDirty(surface);
        agent0.FindPropertyRelative("agentClimb").floatValue = 0.45f;
        agent0.FindPropertyRelative("agentRadius").floatValue = 0.35f;
        navSettings.ApplyModifiedProperties();

        // snap spawn points / box spots to the NavMesh, report the ones in the void
        var bad = new List<string>();
        foreach (var z in zones.Values)
            foreach (var t in z.SpawnPoints.Concat(z.BoxSpots))
            {
                if (NavMesh.SamplePosition(t.position, out var h, 4f, NavMesh.AllAreas)) t.position = h.position;
                else bad.Add(z.name + "/" + t.name);
            }
        if (bad.Count > 0) Debug.LogWarning("[TestMap] Points without NavMesh: " + string.Join(", ", bad));
        return FixIslands(zones, start);
    }

    // A spawn point / box spot on a NavMesh pocket cut off from the start (props or a cabin plug a lane) would trap the
    // old man spawned there (or hide the box): move it to the nearest point that can be reached from the start. A
    // cabin spawn that cannot be saved is removed with its cabin. Returns where the trouble was (to remove cabins
    // around it and bake again).
    static List<Vector3> FixIslands(Dictionary<string, Zone> zones, Vector3 start)
    {
        var trouble = new List<Vector3>();
        var path = new NavMeshPath();
        NavMesh.SamplePosition(start, out var s0, 3f, NavMesh.AllAreas);
        bool Reach(Vector3 b) => NavMesh.CalculatePath(s0.position, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        var report = new List<string>();
        foreach (var z in zones.Values)
            foreach (var p in z.SpawnPoints.Concat(z.BoxSpots).ToList())
            {
                if (Reach(p.position)) continue;
                trouble.Add(p.position);
                bool moved = false;
                if (p.GetComponent<SpawnEntrance>() == null)
                    for (float rad = 1f; rad <= 10f && !moved; rad += 1f)
                        for (int a = 0; a < 360 && !moved; a += 20)
                        {
                            var c = p.position + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * rad;
                            if (NavMesh.SamplePosition(c, out var q, 0.8f, NavMesh.AllAreas) && Reach(q.position))
                            {
                                report.Add($"{z.name}/{p.name} estaba aislado en {p.position}, movido a {q.position}");
                                p.position = q.position;
                                moved = true;
                            }
                        }
                if (moved) continue;
                if (p.GetComponent<SpawnEntrance>() != null)
                {
                    report.Add($"{z.name}/{p.name} AISLADO en {p.position}: se le quita la caseta (el punto sigue)");
                    OriginalMapEntrances.ToPlain(p);
                }
                else report.Add($"{z.name}/{p.name} AISLADO en {p.position} y sin sitio al que moverlo");
            }
        Debug.Log("[TestMap] Puntos aislados en la malla:\n" + (report.Count == 0 ? "ninguno" : string.Join("\n", report)));
        return trouble;
    }

    // ------------------------------------------------------------------ menu scene
    const string EnvironmentPrefabPath = PrefabRoot + "/IslandEnvironment.prefab";

    // Copy of the island scenery (no gameplay objects) used as the menu backdrop.
    static void CreateEnvironmentPrefab()
    {
        var island = GameObject.Find("BeachIsland");
        if (island == null) return;
        PrefabUtility.SaveAsPrefabAsset(island, EnvironmentPrefabPath);
    }

    [MenuItem("Tools/Island/Rebuild Main Menu")]
    public static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var data = CreateData();

        var env = (GameObject)PrefabUtility.InstantiatePrefab(Model(EnvironmentPrefabPath));
        env.name = "Island";
        Physics.SyncTransforms();

        // warm late-afternoon sun from behind the camera
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.86f, 0.7f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(28f, 70f, 0f);
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        // camera looks out to sea over the line-up
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.transform.position = Ground(new Vector2(10.6f, -11.3f)) + Vector3.up * 1.55f;
        cam.transform.LookAt(Ground(new Vector2(16.5f, -10.3f)) + Vector3.up * 0.9f);   // line-up sits right of centre, the menu covers the left

        // clear props standing between the camera and the line-up
        var corridorMin = new Vector2(10.6f, -18.5f);
        var corridorMax = new Vector2(17.8f, -8.2f);
        foreach (Transform group in env.transform.Find("Environment"))
        {
            if (group.name == "Terrain" || group.name == "Water") continue;
            foreach (Transform item in group)
            {
                var p = item.position;
                if (p.x > corridorMin.x && p.x < corridorMax.x && p.z > corridorMin.y && p.z < corridorMax.y) item.gameObject.SetActive(false);
            }
        }

        // the five animals, facing the camera
        var lineup = new GameObject("Lineup").transform;
        var showcase = new Transform[data.animals.Length];
        for (int i = 0; i < data.animals.Length; i++)
        {
            string id = data.animals[i].id;
            float z = -9.9f - i * 1.0f;   // first animal on the left, as in the menu
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Model($"Assets/Characters/{id}/{id}.fbx"), lineup);
            model.name = "Showcase_" + id;
            model.transform.position = Ground(new Vector2(16.4f, z));
            model.transform.rotation = Quaternion.LookRotation(new Vector3(cam.transform.position.x - 16.4f, 0f, cam.transform.position.z - z));
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Characters/{id}/{id}_Player.controller");
            animator.applyRootMotion = false;
            showcase[i] = model.transform;
        }

        // an old man strolling along the shore behind them
        // far sea in a deep teal instead of near-black (also used by the game scene)
        var farSea = AssetDatabase.LoadAssetAtPath<Material>("Assets/BeachIsland/Models/Materials/M_Water_OceanFar.mat");
        if (farSea != null) { farSea.SetColor("_BaseColor", new Color(0.05f, 0.36f, 0.43f)); farSea.SetFloat("_Smoothness", 0.85f); EditorUtility.SetDirty(farSea); }

        var oldMan = (GameObject)PrefabUtility.InstantiatePrefab(Model(OldManFbx));
        oldMan.name = "StrollingOldMan";
        var from = Ground(new Vector2(19.3f, -17.5f));
        var to = Ground(new Vector2(19.3f, -5.5f));
        oldMan.transform.position = from;
        oldMan.transform.rotation = Quaternion.LookRotation(to - from);
        var oa = oldMan.GetComponent<Animator>();
        if (oa == null) oa = oldMan.AddComponent<Animator>();
        oa.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyControllerPath);
        oa.applyRootMotion = false;

        var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        var so = new SerializedObject(menu);
        var arr = so.FindProperty("animals");
        arr.arraySize = data.animals.Length;
        for (int i = 0; i < data.animals.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = data.animals[i];
        var sc = so.FindProperty("showcase");
        sc.arraySize = showcase.Length;
        for (int i = 0; i < showcase.Length; i++) sc.GetArrayElementAtIndex(i).objectReferenceValue = showcase[i];
        so.FindProperty("menuCamera").objectReferenceValue = cam;
        so.FindProperty("gameScene").stringValue = Path.GetFileNameWithoutExtension(PlayableScenePath);
        so.ApplyModifiedPropertiesWithoutUndo();
        SetMenuMaps(menu, data.maps);
        so.Update();
        so.FindProperty("oldMan").objectReferenceValue = oldMan.transform;
        so.FindProperty("oldManFrom").vector3Value = from;
        so.FindProperty("oldManTo").vector3Value = to;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }
}
