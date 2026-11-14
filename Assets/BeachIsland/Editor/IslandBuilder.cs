// Beach Island scene builder.
// Reads Assets/BeachIsland/Island_Layout.json (exported from Island.blend) and places one prefab
// instance of Assets/BeachIsland/Models/<asset>.fbx per entry, plus the characters at their spawns.
// Menu: Tools > Beach Island > Build Island. Re-running it replaces the existing "BeachIsland" object.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class IslandBuilder
{
    const string Root = "Assets/BeachIsland";
    const string CharactersRoot = "Assets/Characters";

    [System.Serializable] class Item { public string asset; public string name; public float[] pos; public float[] rot; public float[] scale; }
    [System.Serializable] class Spawn { public string character; public float[] pos; public float[] rot; }
    [System.Serializable] class Layout { public Item[] items; public Spawn[] spawns; }

    // Asset name prefixes (first token before '_') that get a MeshCollider
    static readonly HashSet<string> SolidGroups = new HashSet<string>
    {
        "Terrain", "CliffBlock", "Ruin", "Temple", "Stairs", "Bridge", "Dock", "Rock", "Tree", "Grave"
    };
    static readonly HashSet<string> SolidAssets = new HashSet<string>
    {
        "Prop_Tent", "Prop_TikiBar", "Prop_PicnicTable", "Prop_LogBench", "Prop_Rowboat", "Prop_Crate",
        "Prop_Barrel", "Prop_Fence", "Prop_Totem", "Prop_TreasureChest", "Prop_FirePit", "Log_Mossy"
    };
    static readonly HashSet<string> NonSolidAssets = new HashSet<string> { "Rock_Pebble", "Tree_Stump", "Grave_Gate" }; // the gate is the way into the graveyard

    [MenuItem("Tools/Beach Island/Build Island")]
    public static void Build()
    {
        string jsonPath = Root + "/Island_Layout.json";
        if (!File.Exists(jsonPath)) { Debug.LogError("Island layout not found: " + jsonPath); return; }
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(jsonPath));

        var old = GameObject.Find("BeachIsland");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var parent = new GameObject("BeachIsland");
        Undo.RegisterCreatedObjectUndo(parent, "Build Island");
        var environment = new GameObject("Environment").transform;
        environment.SetParent(parent.transform, false);

        var groups = new Dictionary<string, Transform>();
        int placed = 0, missing = 0;
        foreach (var it in layout.items)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + it.asset + ".fbx");
            if (prefab == null) { Debug.LogWarning("Missing model: " + it.asset); missing++; continue; }

            string group = it.asset.Split('_')[0];
            if (!groups.TryGetValue(group, out var g))
            {
                g = new GameObject(group).transform;
                g.SetParent(environment, false);
                groups[group] = g;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            go.name = it.name;
            go.transform.localPosition = new Vector3(it.pos[0], it.pos[1], it.pos[2]);
            go.transform.localRotation = new Quaternion(it.rot[0], it.rot[1], it.rot[2], it.rot[3]);
            go.transform.localScale = new Vector3(it.scale[0], it.scale[1], it.scale[2]);

            bool solid = (SolidGroups.Contains(group) || SolidAssets.Contains(it.asset)) && !NonSolidAssets.Contains(it.asset);
            if (solid)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    if (!mf.GetComponent<Collider>()) mf.gameObject.AddComponent<MeshCollider>();

            if (group == "Water")
                go.layer = 4; // built-in "Water" layer
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                                                       StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            placed++;
        }

        Debug.Log(OriginalMapColliders.Apply(environment));   // trunks instead of whole trees, props that look solid

        int chars = 0;
        if (layout.spawns != null)
        {
            var charRoot = new GameObject("Characters").transform;
            charRoot.SetParent(parent.transform, false);
            foreach (var sp in layout.spawns)
            {
                var go = InstantiateCharacter(sp.character, charRoot);
                if (go == null) { go = new GameObject("Spawn_" + sp.character); go.transform.SetParent(charRoot, false); }
                else chars++;
                go.transform.localPosition = new Vector3(sp.pos[0], sp.pos[1], sp.pos[2]);
                go.transform.localRotation = new Quaternion(sp.rot[0], sp.rot[1], sp.rot[2], sp.rot[3]);
            }
        }

        Selection.activeGameObject = parent;
        Debug.Log($"Beach Island built: {placed} objects placed, {missing} missing, {chars} characters.");
    }

    // Instantiates Assets/Characters/<name>/<name>.fbx with an Animator playing its walk clip.
    public static GameObject InstantiateCharacter(string name, Transform parent)
    {
        string fbx = $"{CharactersRoot}/{name}/{name}.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = GetOrCreateController(name);
        return go;
    }

    static RuntimeAnimatorController GetOrCreateController(string name)
    {
        string path = $"{CharactersRoot}/{name}/{name}.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl != null) return ctrl;
        AnimationClip walk = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath($"{CharactersRoot}/{name}/{name}.fbx"))
            if (a is AnimationClip c && !c.name.StartsWith("__preview")) { walk = c; break; }
        return walk != null ? AnimatorController.CreateAnimatorControllerAtPathWithClip(path, walk) : null;
    }
}
