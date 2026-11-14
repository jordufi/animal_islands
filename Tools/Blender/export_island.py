# Export Island.blend for Unity:
#  - one FBX per unique mesh used in the "Island" collection (object at identity, axis baked)
#  - all textures used by those materials
#  - Island_Layout.json: Unity-space position / rotation (quaternion) / scale of every placed object,
#    plus character spawn points from the "Characters" collection rigs.
import bpy, os, shutil, json, collections
from mathutils import Matrix

ROOT = r"C:\Users\Usuario\animal_island1\Assets\BeachIsland"
MODELS = os.path.join(ROOT, "Models"); TEX = os.path.join(ROOT, "Textures")
os.makedirs(MODELS, exist_ok=True); os.makedirs(TEX, exist_ok=True)

# Blender (x, y, z) -> Unity (-x, z, -y) for FBX exported with forward=-Z, up=Y
C = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))
CI = C.transposed()
def to_unity(mw):
    loc, rot, scl = (C @ mw @ CI).decompose()
    return [round(v, 5) for v in loc], [round(rot.x, 6), round(rot.y, 6), round(rot.z, 6), round(rot.w, 6)], [round(v, 5) for v in scl]

island = bpy.data.collections["Island"].all_objects
meshes = collections.OrderedDict()
for o in island:
    if o.type == 'MESH': meshes.setdefault(o.data.name, o.data)

# textures + material report
tex_copied, mat_info = set(), {}
for me in meshes.values():
    for m in me.materials:
        if not m or m.name in mat_info: continue
        info = {"tex": [], "emit": False, "alpha": 1.0, "vcol": False}
        if m.use_nodes:
            for nd in m.node_tree.nodes:
                if nd.type == 'TEX_IMAGE' and nd.image and nd.image.source == 'FILE':
                    src = bpy.path.abspath(nd.image.filepath)
                    if os.path.exists(src):
                        shutil.copy2(src, TEX); tex_copied.add(os.path.basename(src)); info["tex"].append(os.path.basename(src))
                elif nd.type in ('VERTEX_COLOR', 'ATTRIBUTE'): info["vcol"] = True
                elif nd.type == 'BSDF_PRINCIPLED':
                    es = nd.inputs.get("Emission Strength")
                    if es and es.default_value > 0 and tuple(nd.inputs["Emission Color"].default_value)[:3] != (0, 0, 0): info["emit"] = True
                    info["alpha"] = round(nd.inputs["Alpha"].default_value, 3)
                elif nd.type == 'EMISSION': info["emit"] = True
        mat_info[m.name] = info

# per-mesh FBX export through a temporary object at the origin
scene = bpy.context.scene
tmp_coll = bpy.data.collections.new("_export_tmp"); scene.collection.children.link(tmp_coll)
for o in bpy.context.view_layer.objects: o.select_set(False)
bad = []
for name, me in meshes.items():
    tmp = bpy.data.objects.new(name, me)
    tmp_coll.objects.link(tmp)
    tmp.select_set(True); bpy.context.view_layer.objects.active = tmp
    try:
        bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, name + ".fbx"), use_selection=True,
            object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True,
            axis_forward='-Z', axis_up='Y', bake_space_transform=True, use_mesh_modifiers=False,
            mesh_smooth_type='FACE', bake_anim=False, path_mode='STRIP', embed_textures=False,
            use_custom_props=False)
    except Exception as e:
        bad.append((name, str(e)))
    tmp.select_set(False)
    bpy.data.objects.remove(tmp)
bpy.data.collections.remove(tmp_coll)

# layout
items, neg = [], 0
for o in island:
    if o.type != 'MESH': continue
    if o.matrix_world.determinant() < 0: neg += 1
    p, r, s = to_unity(o.matrix_world)
    items.append({"asset": o.data.name, "name": o.name, "pos": p, "rot": r, "scale": s})
spawns = []
for o in bpy.data.collections["Characters"].all_objects:
    if o.type == 'ARMATURE':
        p, r, s = to_unity(o.matrix_world)
        spawns.append({"character": o.name.replace("_Rig", ""), "pos": p, "rot": r, "scale": s})
with open(os.path.join(ROOT, "Island_Layout.json"), "w") as f:
    json.dump({"items": items, "spawns": spawns}, f, indent=1)

special = {k: v for k, v in mat_info.items() if v["emit"] or v["alpha"] < 1 or v["vcol"]}
print("JSONOUT" + json.dumps({"fbx": len(meshes), "items": len(items), "spawns": spawns, "negative_scale": neg,
    "tex": len(tex_copied), "mats": len(mat_info), "no_tex_mats": sum(1 for v in mat_info.values() if not v["tex"]),
    "special": special, "bad": bad}))
