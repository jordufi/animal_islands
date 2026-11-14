# Low-poly pistol. Origin = grip (where the hand holds it), barrel points along -Y (Unity +Z), up = +Z.
import bpy, bmesh, os, json, math
from mathutils import Vector, Matrix

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)

def mat(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.5
    return m

m_body = mat("Pistol_Body", (0.06, 0.06, 0.07))
m_grip = mat("Pistol_Grip", (0.25, 0.12, 0.05))
m_metal = mat("Pistol_Metal", (0.45, 0.46, 0.5))

bm = bmesh.new()
def box(size, center, mat_index, rot_x_deg=0.0):
    geom = bmesh.ops.create_cube(bm, size=1.0)
    verts = geom["verts"]
    bmesh.ops.scale(bm, vec=Vector(size), verts=verts)
    if rot_x_deg:
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(rot_x_deg), 3, 'X'), verts=verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat_index

def cyl(radius, depth, center, mat_index):
    geom = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=radius, radius2=radius, depth=depth)
    verts = geom["verts"]
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(90), 3, 'X'), verts=verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat_index

# sizes in metres (stylised, slightly chunky so it reads on the small characters)
box((0.045, 0.24, 0.055), (0, -0.07, 0.085), 0)          # slide
box((0.040, 0.20, 0.025), (0, -0.05, 0.05), 0)           # frame
box((0.038, 0.06, 0.13), (0, 0.02, 0.0), 1, rot_x_deg=-15)  # grip, tilted back
box((0.008, 0.012, 0.03), (0, -0.04, 0.025), 2)          # trigger
box((0.010, 0.07, 0.008), (0, -0.045, 0.006), 0)         # trigger guard (bottom)
box((0.012, 0.02, 0.012), (0, -0.175, 0.118), 2)         # front sight
cyl(0.011, 0.03, (0, -0.2, 0.085), 2)                     # muzzle

me = bpy.data.meshes.new("Pistol")
bm.to_mesh(me)
bm.free()
for m in (m_body, m_grip, m_metal):
    me.materials.append(m)
ob = bpy.data.objects.new("Pistol", me)
bpy.context.scene.collection.objects.link(ob)

muzzle = bpy.data.objects.new("Muzzle", None)
muzzle.empty_display_size = 0.03
muzzle.location = (0, -0.22, 0.085)
muzzle.parent = ob
bpy.context.scene.collection.objects.link(muzzle)

blend_dir = r"C:\Users\Usuario\Documents\BeachAnimals\Weapons"
os.makedirs(blend_dir, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, "Pistol.blend"))

out_dir = r"C:\Users\Usuario\animal_island1\Assets\Weapons\Pistol"
os.makedirs(out_dir, exist_ok=True)
for o in bpy.context.view_layer.objects:
    o.select_set(True)
bpy.context.view_layer.objects.active = ob
fbx = os.path.join(out_dir, "Pistol.fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH', 'EMPTY'},
    apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
    bake_space_transform=True, mesh_smooth_type='FACE', bake_anim=False, path_mode='STRIP')
print("JSONOUT" + json.dumps({"verts": len(me.vertices), "faces": len(me.polygons), "dims": [round(v, 3) for v in ob.dimensions], "fbx": os.path.getsize(fbx)}))
