# Low-poly shotgun, sniper and SMG. Same convention as the pistol: origin = grip,
# barrel along -Y (Unity +Z), up = +Z, child empty "Muzzle" at the barrel tip.
import bpy, bmesh, os, json, math
from mathutils import Vector, Matrix

OUT = r"C:\Users\Usuario\animal_island1\Assets\Weapons"
BLEND_DIR = r"C:\Users\Usuario\Documents\BeachAnimals\Weapons"

def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)

def mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    return m

def build(name, parts, muzzle, materials):
    bm = bmesh.new()
    for kind, size, center, mi, rot in parts:
        if kind == "box":
            g = bmesh.ops.create_cube(bm, size=1.0)
            vs = g["verts"]
            bmesh.ops.scale(bm, vec=Vector(size), verts=vs)
        else:
            g = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=size[0], radius2=size[0], depth=size[1])
            vs = g["verts"]
            bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(90), 3, 'X'), verts=vs)
        if rot:
            bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(rot), 3, 'X'), verts=vs)
        bmesh.ops.translate(bm, vec=Vector(center), verts=vs)
        for f in {f for v in vs for f in v.link_faces}:
            f.material_index = mi
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in materials:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    mz = bpy.data.objects.new("Muzzle", None)
    mz.location = muzzle
    mz.parent = ob
    bpy.context.scene.collection.objects.link(mz)
    return ob

def export(ob, name):
    d = os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    for o in bpy.data.objects:
        o.select_set(o == ob or o.parent == ob)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(d, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', bake_anim=False, path_mode='STRIP')
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, name + ".blend"))
    return {"name": name, "verts": len(ob.data.vertices), "dims": [round(v, 3) for v in ob.dimensions]}

os.makedirs(BLEND_DIR, exist_ok=True)
res = []

# Shotgun "La Chancla": pump action, wooden stock and pump
clear()
dark, wood, metal = mat("Gun_Dark", (0.06, 0.06, 0.07)), mat("Gun_Wood", (0.35, 0.17, 0.06)), mat("Gun_Metal", (0.45, 0.46, 0.5))
ob = build("Shotgun", [
    ("box", (0.05, 0.22, 0.07), (0, -0.05, 0.07), 0, 0),        # receiver
    ("cyl", (0.016, 0.42), (0, -0.37, 0.09), 2, 0),               # barrel
    ("cyl", (0.014, 0.36), (0, -0.33, 0.055), 0, 0),              # magazine tube
    ("box", (0.05, 0.13, 0.045), (0, -0.3, 0.05), 1, 0),          # pump
    ("box", (0.04, 0.07, 0.12), (0, 0.03, -0.01), 1, -20),        # grip
    ("box", (0.045, 0.26, 0.07), (0, 0.18, 0.035), 1, -8),        # stock
], (0, -0.59, 0.09), [dark, wood, metal])
res.append(export(ob, "Shotgun"))

# Sniper "El Catalejo": long barrel + big scope
clear()
dark, wood, metal = mat("Gun_Dark", (0.06, 0.06, 0.07)), mat("Gun_Olive", (0.25, 0.3, 0.15)), mat("Gun_Metal", (0.45, 0.46, 0.5))
ob = build("Sniper", [
    ("box", (0.045, 0.3, 0.07), (0, -0.07, 0.07), 1, 0),          # body
    ("cyl", (0.012, 0.6), (0, -0.52, 0.085), 0, 0),               # barrel
    ("cyl", (0.026, 0.26), (0, -0.08, 0.15), 0, 0),               # scope
    ("box", (0.012, 0.03, 0.03), (0, -0.12, 0.12), 2, 0),         # scope mount
    ("box", (0.04, 0.07, 0.12), (0, 0.04, -0.01), 1, -20),        # grip
    ("box", (0.045, 0.3, 0.08), (0, 0.22, 0.04), 1, -6),          # stock
    ("box", (0.008, 0.04, 0.03), (0, -0.03, 0.015), 2, 0),        # trigger
], (0, -0.83, 0.085), [dark, wood, metal])
res.append(export(ob, "Sniper"))

# SMG "La Thermomix": compact, long magazine
clear()
dark, orange, metal = mat("Gun_Dark", (0.06, 0.06, 0.07)), mat("Gun_Orange", (0.85, 0.35, 0.05)), mat("Gun_Metal", (0.45, 0.46, 0.5))
ob = build("SMG", [
    ("box", (0.05, 0.26, 0.075), (0, -0.07, 0.07), 0, 0),         # body
    ("cyl", (0.014, 0.12), (0, -0.26, 0.08), 2, 0),               # barrel
    ("box", (0.03, 0.04, 0.17), (0, -0.1, -0.03), 1, 0),          # magazine
    ("box", (0.04, 0.06, 0.11), (0, 0.03, -0.0), 0, -15),         # grip
    ("box", (0.02, 0.18, 0.02), (0, 0.13, 0.06), 2, 0),           # wire stock
    ("box", (0.035, 0.04, 0.06), (0, 0.23, 0.04), 2, 0),          # butt
], (0, -0.33, 0.08), [dark, orange, metal])
res.append(export(ob, "SMG"))

print("JSONOUT" + json.dumps(res))
