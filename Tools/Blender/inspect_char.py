import bpy, json, os
sc = bpy.context.scene
vl = bpy.context.view_layer
res = {"file": os.path.basename(bpy.data.filepath), "unit": sc.unit_settings.scale_length}
res["objs"] = [{"n": o.name, "t": o.type, "p": o.parent.name if o.parent else None, "vis": o.visible_get(), "coll": [c.name for c in o.users_collection], "mods":[m.type for m in o.modifiers], "scale":[round(s,3) for s in o.scale]} for o in sc.objects if o.type in ('MESH','ARMATURE','EMPTY')]
res["actions"] = [(a.name, list(a.frame_range), a.users, a.use_fake_user) for a in bpy.data.actions]
res["imgs"] = [(i.name, i.filepath, i.packed_file is not None) for i in bpy.data.images if i.source=='FILE']
res["missing_imgs"] = [i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file and not os.path.exists(bpy.path.abspath(i.filepath))]
print("JSONOUT" + json.dumps(res))
