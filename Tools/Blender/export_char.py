import bpy, os, shutil, json, sys
name = sys.argv[sys.argv.index("--")+1]
out_dir = os.path.join(r"C:\Users\Usuario\animal_island1\Assets\Characters", name)
tex_dir = os.path.join(out_dir, "Textures")
os.makedirs(tex_dir, exist_ok=True)
rig = bpy.data.objects[name + "_Rig"]
objs = [rig] + [o for o in rig.children_recursive if o.type == 'MESH']
for o in bpy.context.view_layer.objects: o.select_set(False)
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = rig
copied = set()
for o in objs:
    for s in getattr(o, "material_slots", []):
        m = s.material
        if m and m.use_nodes:
            for nd in m.node_tree.nodes:
                if nd.type == 'TEX_IMAGE' and nd.image and nd.image.source == 'FILE':
                    src = bpy.path.abspath(nd.image.filepath)
                    shutil.copy2(src, tex_dir); copied.add(os.path.basename(src))
fbx = os.path.join(out_dir, name + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'ARMATURE','MESH'},
    apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
    bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    path_mode='STRIP', embed_textures=False)
print("JSONOUT" + json.dumps({"name": name, "objs": [o.name for o in objs], "tex": sorted(copied), "size": os.path.getsize(fbx)}))
