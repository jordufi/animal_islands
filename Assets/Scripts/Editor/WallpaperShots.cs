// Phone wallpapers / promo shots of the game, rendered from the editor (Tools > Island > Render Wallpapers).
// Opens a map scene, poses the animals and old men (prefabs with one animation frame sampled), sets the light,
// renders a portrait camera at 2x the phone size (the PNG is downscaled later for anti-aliasing) and reopens the
// scene WITHOUT saving, so the maps are never touched. Output: Renders/Wallpaper_<name>_raw.png.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class WallpaperShots
{
    public const int PhoneW = 1344, PhoneH = 2992;   // Pixel 8 Pro
    static readonly List<GameObject> spawned = new List<GameObject>();

    public static void Open(string scenePath)
    {
        spawned.Clear();
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    // Discards every change (posed characters, lights) by reloading the scene from disk.
    public static void Close(string scenePath)
    {
        spawned.Clear();
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    // Layout cell -> world, for La Serenisima (origin cell 34, 24) or Isla del Faro (21, 18).
    public static Vector3 Cell(float c, float r, float y, int oc, int or) => new Vector3((c - oc) * 2f, y, (or - r) * 2f);

    static AnimationClip Clip(string character, string clip)
    {
        return AssetDatabase.LoadAllAssetsAtPath($"Assets/Characters/{character}/{character}.fbx").OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == character + "_" + clip);
    }

    // An animal (RedPanda, Panda, Frog, Crocodile, Toucan) in a pose; weapon = a WeaponData asset name or null.
    public static GameObject Animal(string animal, Vector3 pos, float yaw, string clip, float t, string weapon = null)
    {
        var go = Place($"Assets/Prefabs/Players/Player_{animal}.prefab", pos, yaw, animal, clip, t);
        if (go != null && weapon != null)
        {
            var shooter = go.GetComponent<PlayerShooter>();
            var mount = shooter != null ? new SerializedObject(shooter).FindProperty("weaponMount").objectReferenceValue as Transform : null;
            var data = AssetDatabase.FindAssets("t:WeaponData").Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(w => w != null && w.name == weapon);
            if (mount != null && data != null && data.model != null)
            {
                var gun = Object.Instantiate(data.model, mount, false);
                gun.transform.localPosition = Vector3.zero;
                gun.transform.localRotation = Quaternion.identity;
            }
        }
        return go;
    }

    // An old man (scale > 2 = the boss prefab); mask = "VN_Mascara_Bauta" / "VN_Mascara_Peste" / null; gold = golden mask.
    public static GameObject OldMan(Vector3 pos, float yaw, string clip, float t, Color? clothes = null, string mask = null, bool boss = false, bool gold = false)
    {
        var go = Place(boss ? "Assets/Prefabs/Boss_DonAnselmo.prefab" : "Assets/Prefabs/Enemy_OldMan.prefab", pos, yaw, "OldMan", clip, t);
        if (go == null) return null;
        if (clothes.HasValue)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = clothes.Value };
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.name.Contains("Shirt") || r.name.Contains("Shorts") || r.name.Contains("Vest")) r.sharedMaterial = m;
        }
        if (mask != null)
        {
            var head = go.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "Head");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Maps/Venecia/Models/{mask}.fbx");
            if (head != null && prefab != null)
            {
                float k = go.transform.lossyScale.y;
                var mk = (GameObject)Object.Instantiate(prefab, head.position + go.transform.forward * 0.3f * k + Vector3.up * 0.26f * k, go.transform.rotation, head);
                mk.transform.localScale *= 2.1f;
                if (gold)
                {
                    var g = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.78f, 0.15f) };
                    g.SetFloat("_Metallic", 0.8f); g.SetFloat("_Smoothness", 0.7f);
                    foreach (var r in mk.GetComponentsInChildren<Renderer>())
                    {
                        var ms = r.sharedMaterials;
                        for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name.StartsWith("VN_White")) ms[i] = g;
                        r.sharedMaterials = ms;
                    }
                }
            }
        }
        return go;
    }

    static GameObject Place(string prefabPath, Vector3 pos, float yaw, string character, string clip, float t)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError("[Wallpaper] missing " + prefabPath); return null; }
        var go = (GameObject)Object.Instantiate(prefab);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var anim = go.GetComponentInChildren<Animator>();
        var c = Clip(character, clip);
        if (anim != null && c != null)
        {
            bool root = anim.applyRootMotion;
            c.SampleAnimation(anim.gameObject, t * c.length);
            anim.transform.localPosition = Vector3.zero;   // keep the feet where we put them
        }
        // stand on the ground: the lowest foot / toe bone goes to pos.y
        var feet = go.GetComponentsInChildren<Transform>().Where(x => x.name.Contains("Toes") || x.name.Contains("Foot")).ToList();
        if (feet.Count > 0) go.transform.position += Vector3.up * (pos.y + 0.04f * go.transform.lossyScale.y - feet.Min(f => f.position.y));
        spawned.Add(go);
        return go;
    }

    // Ground under (x, z): the first collider hit from above that is not a roof (y below `below`).
    public static Vector3 Ground(float x, float z, float below = 30f)
    {
        foreach (var h in Physics.RaycastAll(new Vector3(x, below, z), Vector3.down, 80f).OrderByDescending(h => h.point.y))
            if (!h.collider.isTrigger) return h.point;
        return new Vector3(x, 0f, z);
    }

    // Hides world labels, the extraction beacon and anything else that only makes sense in play.
    public static void HideGameplayClutter()
    {
        foreach (var t in Object.FindObjectsByType<TextMesh>()) { var r = t.GetComponent<Renderer>(); if (r != null) r.enabled = false; }
        var ex = GameObject.Find("Extraction");
        if (ex != null) foreach (var r in ex.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    // A big glowing moon `distance` m from the camera, in the direction `dir`.
    public static void Moon(Vector3 cam, Vector3 dir, float distance = 160f, float size = 14f)
    {
        var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(m.GetComponent<Collider>());
        m.transform.position = cam + dir.normalized * distance;
        m.transform.localScale = Vector3.one * size;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.97f, 0.85f) };
        m.GetComponent<Renderer>().sharedMaterial = mat;
        m.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public static GameObject Model(string path, Vector3 pos, float yaw, float scale = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;
        var go = (GameObject)Object.Instantiate(prefab);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    public static Light PointLight(Vector3 pos, Color color, float range, float intensity)
    {
        var l = new GameObject("WallpaperLight").AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        l.transform.position = pos;
        return l;
    }

    public static Light Sun()
    {
        var sun = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        return sun;
    }

    // Night look: dim blue moon, dark sky, every lamp model of the map gets a warm point light.
    public static void Night(float lampRange = 7f, float lampIntensity = 2.2f)
    {
        var sun = Sun();
        if (sun != null) { sun.color = new Color(0.5f, 0.6f, 1f); sun.intensity = 0.22f; sun.transform.rotation = Quaternion.Euler(35f, 140f, 0f); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.24f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.1f);
        if (RenderSettings.skybox != null)
        {
            var sky = new Material(RenderSettings.skybox);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.08f);
            RenderSettings.skybox = sky;
        }
        foreach (var r in Object.FindObjectsByType<Renderer>())
        {
            if (!r.name.StartsWith("VN_Farola") && !r.name.StartsWith("Prop_Lantern") && !r.name.StartsWith("Prop_TikiTorch")) continue;
            PointLight(r.bounds.center + Vector3.up * 0.6f, new Color(1f, 0.72f, 0.4f), lampRange, lampIntensity);
        }
    }

    public static void Render(string name, Vector3 camPos, Vector3 lookAt, float fov, float roll = 0f, int scale = 2)
    {
        int w = PhoneW * scale, h = PhoneH * scale;
        var go = new GameObject("WallpaperCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.LookAt(lookAt);
        cam.transform.Rotate(0f, 0f, roll, Space.Self);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.08f;
        cam.farClipPlane = 600f;
        cam.allowMSAA = true;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.aspect = (float)w / h;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        Directory.CreateDirectory("Renders");
        File.WriteAllBytes($"Renders/Wallpaper_{name}_raw.png", tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
    }
}
