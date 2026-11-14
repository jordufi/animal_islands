import bpy, collections, json
def walk(c, d=0, out=None):
    out.append({"name": c.name, "depth": d, "n_obj": len(c.objects), "types": dict(collections.Counter(o.type for o in c.objects)), "children": len(c.children)})
    for ch in c.children: walk(ch, d+1, out)
    return out
sc = bpy.context.scene
res = {"scenes": [s.name for s in bpy.data.scenes], "scene": sc.name}
res["tree"] = walk(sc.collection, 0, [])
objs = list(sc.objects)
res["n_objects"] = len(objs)
res["types"] = dict(collections.Counter(o.type for o in objs))
res["instancers"] = sum(1 for o in objs if o.instance_type == 'COLLECTION')
res["n_meshes"] = len(bpy.data.meshes); res["n_mats"] = len(bpy.data.materials); res["n_images"] = len(bpy.data.images)
md = collections.Counter(o.data.name for o in objs if o.type=='MESH')
res["n_unique_mesh_used"] = len(md)
res["shared_mesh_top"] = md.most_common(10)
res["sample_names"] = [o.name for o in objs[:40]]
res["mods"] = dict(collections.Counter(m.type for o in objs for m in o.modifiers))
res["armatures"] = [o.name for o in objs if o.type=='ARMATURE']
res["empties"] = [o.name for o in objs if o.type=='EMPTY'][:20]
res["lights"] = len([o for o in objs if o.type=='LIGHT'])
res["actions"] = [a.name for a in bpy.data.actions]
res["props"] = list({k for o in objs for k in o.keys()})[:30]
res["missing_imgs"] = [i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file and not __import__('os').path.exists(bpy.path.abspath(i.filepath))]
print("JSONOUT" + json.dumps(res))
