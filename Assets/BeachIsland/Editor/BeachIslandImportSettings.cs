// Import settings for the island models exported from Island.blend (and the map props made by
// Tools/Blender/make_map_props.py under Assets/Maps/*/Models). Every such FBX is a static mesh. Its materials are remapped to the
// shared .mat files in Assets/BeachIsland/Models/Materials (one per Blender material), so props
// that use the same palette batch together.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BeachIslandImportSettings : AssetPostprocessor
{
    const string ModelsFolder = "Assets/BeachIsland/Models/";
    const string MapsFolder = "Assets/Maps/";
    const string MaterialsFolder = "Assets/BeachIsland/Models/Materials";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ModelsFolder) && !(assetPath.StartsWith(MapsFolder) && assetPath.Contains("/Models/"))) return;
        var imp = (ModelImporter)assetImporter;
        if (!imp.importSettingsMissing) return; // respect manual changes after the first import
        Configure(imp);
    }

    public static void Configure(ModelImporter imp)
    {
        imp.animationType = ModelImporterAnimationType.None;
        imp.importAnimation = false;
        imp.importCameras = false;
        imp.importLights = false;
        imp.importBlendShapes = false;
        imp.isReadable = false;
        imp.addCollider = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        // remap every shared material by name (names the model doesn't use are simply ignored)
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name), mat);
        }
    }

    // Converts models imported with the old (unsupported) external-materials mode.
    [MenuItem("Tools/Beach Island/Fix Model Material Import")]
    public static int FixExisting()
    {
        int changed = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') + "/" != ModelsFolder) continue;
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
#pragma warning disable 618
                if (imp.materialLocation == ModelImporterMaterialLocation.InPrefab && imp.GetExternalObjectMap().Count > 0) continue;
#pragma warning restore 618
                Configure(imp);
                imp.SaveAndReimport();
                changed++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Debug.Log("Beach Island: material import fixed on " + changed + " models");
        return changed;
    }
}
