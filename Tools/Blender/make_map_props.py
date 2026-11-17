# Low-poly props for the Isla Zombie map: faro, coche, autobus, puesto, tienda, caseta, contenedor,
# barco de rescate, avioneta estrellada, grada. One FBX per prop in Assets/Maps/IslaZombie/Models
# (+ a .blend copy). Run in background:
#   blender.exe -b --factory-startup --python make_map_props.py
# Conventions: origin = centre of the footprint on the ground, length along Blender Y (= Unity Z),
# up = +Z. Materials named like the island's shared ones (M_Wood, M_Stone, M_Canvas, M_Iron, M_Rock...)
# are remapped by BeachIslandImportSettings to the textured .mat files; IZ_* materials are flat colours
# (IZ_*Paint ones get colour variants in Unity). UVs are box-projected at 1 tile = 2 m.
import bpy, bmesh, os, math, json
from mathutils import Vector, Matrix

OUT = r"C:\Users\Usuario\animal_island1\Assets\Maps\IslaZombie\Models"
BLEND = r"C:\Users\Usuario\Documents\BeachAnimals\IslaZombie\IslaZombie_Props.blend"

COLORS = {
    "M_Wood": (0.45, 0.28, 0.14), "M_Stone": (0.55, 0.55, 0.5), "M_Canvas": (0.8, 0.72, 0.55),
    "M_Iron": (0.25, 0.25, 0.27), "M_Rock": (0.5, 0.5, 0.48), "M_StoneMoss": (0.5, 0.55, 0.45),
    "IZ_White": (0.92, 0.92, 0.9), "IZ_Red": (0.75, 0.12, 0.1), "IZ_Glass": (0.15, 0.22, 0.28),
    "IZ_Tire": (0.05, 0.05, 0.05), "IZ_Lamp": (1.0, 0.9, 0.5), "IZ_Thatch": (0.75, 0.6, 0.3),
    "IZ_CarPaint": (0.7, 0.15, 0.12), "IZ_BusPaint": (0.95, 0.72, 0.1), "IZ_AwningPaint": (0.2, 0.55, 0.35),
    "IZ_TentPaint": (0.9, 0.5, 0.15), "IZ_ContainerPaint": (0.2, 0.4, 0.6), "IZ_Dark": (0.08, 0.07, 0.07),
    "IZ_Fruit": (0.95, 0.55, 0.1), "IZ_Fruit2": (0.5, 0.75, 0.15),
}

def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)

def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (*COLORS[name], 1)
        if name == "IZ_Lamp":
            bsdf.inputs["Emission Color"].default_value = (*COLORS[name], 1)
            bsdf.inputs["Emission Strength"].default_value = 3.0
    return m

class Builder:
    def __init__(self, name):
        self.name, self.bm, self.mats = name, bmesh.new(), []

    def _mi(self, m):
        if m not in self.mats: self.mats.append(m)
        return self.mats.index(m)

    def _finish(self, verts, m, rot=None, at=(0, 0, 0)):
        if rot:
            for axis, deg in rot:
                bmesh.ops.rotate(self.bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(deg), 3, axis), verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(at), verts=verts)
        mi = self._mi(m)
        for f in {f for v in verts for f in v.link_faces}: f.material_index = mi

    def box(self, size, at, m, rot=None):
        g = bmesh.ops.create_cube(self.bm, size=1.0)
        bmesh.ops.scale(self.bm, vec=Vector(size), verts=g["verts"])
        self._finish(g["verts"], m, rot, at)

    def cyl(self, r1, r2, depth, at, m, seg=8, rot=None):
        g = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=depth)
        self._finish(g["verts"], m, rot, at)

    def prism(self, w, length, h, at, m, rot=None):   # triangular A-frame, ridge along Y
        bm = self.bm
        vs = [bm.verts.new(v) for v in [(-w / 2, -length / 2, 0), (w / 2, -length / 2, 0), (0, -length / 2, h),
                                        (-w / 2, length / 2, 0), (w / 2, length / 2, 0), (0, length / 2, h)]]
        for f in [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]:
            bm.faces.new([vs[i] for i in f])
        self._finish(vs, m, rot, at)

    def pyramid(self, w, d, h, at, m):
        g = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=4, radius1=1.0, radius2=0.0, depth=h)
        bmesh.ops.rotate(self.bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(45), 3, 'Z'), verts=g["verts"])
        bmesh.ops.scale(self.bm, vec=Vector((w / math.sqrt(2), d / math.sqrt(2), 1)), verts=g["verts"])
        self._finish(g["verts"], m, None, (at[0], at[1], at[2] + h / 2))

    def build(self):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        # box-projection UVs, 1 tile = 2 m
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for l in f.loops:
                co = l.vert.co
                u, v = [(co.y, co.z), (co.x, co.z), (co.x, co.y)][ax]
                l[uv].uv = (u / 2.0, v / 2.0)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats: me.materials.append(mat(m))
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob

def export(ob):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.data.objects: o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', bake_anim=False, path_mode='STRIP')
    return {"name": ob.name, "verts": len(ob.data.vertices), "dims": [round(v, 2) for v in ob.dimensions], "mats": [m.name for m in ob.data.materials]}

res = []
all_objs = []

def make(fn):
    clear_scene_keep = False
    ob = fn()
    res.append(export(ob))
    ob.location.x = len(all_objs) * 12   # spread them in the saved .blend
    all_objs.append(ob.name)

# ------------------------------------------------------------------ faro (lighthouse), 4 x 4 m footprint
def faro():
    b = Builder("IZ_Faro")
    b.cyl(2.3, 2.3, 0.6, (0, 0, 0.3), "M_Stone", seg=8)                  # plinth
    h, r0, r1, bands = 13.0, 1.9, 1.35, 5
    for i in range(bands):
        z0, z1 = i * h / bands, (i + 1) * h / bands
        ra, rb = r0 + (r1 - r0) * z0 / h, r0 + (r1 - r0) * z1 / h
        b.cyl(ra, rb, z1 - z0, (0, 0, 0.6 + (z0 + z1) / 2), "IZ_Red" if i % 2 else "IZ_White", seg=8)
    top = 0.6 + h
    b.cyl(2.0, 2.0, 0.3, (0, 0, top + 0.15), "M_Iron", seg=8)            # gallery
    for i in range(8):
        a = i * math.pi / 4
        b.box((0.08, 0.08, 0.9), (1.9 * math.cos(a), 1.9 * math.sin(a), top + 0.75), "M_Iron")
    b.cyl(1.1, 1.1, 1.8, (0, 0, top + 1.2), "IZ_Lamp", seg=8)            # lamp room
    b.cyl(1.4, 0.0, 1.5, (0, 0, top + 2.85), "IZ_Red", seg=8)            # roof
    b.box((1.0, 0.3, 1.9), (0, -1.85, 1.55), "IZ_Dark")                   # door
    return b.build()

# ------------------------------------------------------------------ coche (car), 1.8 x 4 m
def coche():
    b = Builder("IZ_Coche")
    b.box((1.8, 4.0, 0.65), (0, 0, 0.62), "IZ_CarPaint")
    b.box((1.6, 2.1, 0.62), (0, 0.15, 1.25), "IZ_CarPaint")
    b.box((1.62, 1.9, 0.42), (0, 0.15, 1.27), "IZ_Glass")                 # side windows
    b.box((1.5, 2.15, 0.44), (0, 0.15, 1.27), "IZ_Glass")                 # front / rear windows
    b.box((1.85, 0.2, 0.25), (0, 2.02, 0.45), "M_Iron")                   # bumpers
    b.box((1.85, 0.2, 0.25), (0, -2.02, 0.45), "M_Iron")
    for x in (-0.85, 0.85):
        for y in (-1.3, 1.3):
            b.cyl(0.36, 0.36, 0.28, (x, y, 0.36), "IZ_Tire", seg=10, rot=[('Y', 90)])
    return b.build()

# ------------------------------------------------------------------ autobus, 2.5 x 7 m
def autobus():
    b = Builder("IZ_Autobus")
    b.box((2.5, 7.0, 2.3), (0, 0, 1.55), "IZ_BusPaint")
    b.box((2.52, 6.2, 0.8), (0, 0.2, 2.0), "IZ_Glass")
    b.box((2.3, 0.05, 1.1), (0, 3.51, 1.9), "IZ_Glass")
    b.box((2.4, 6.8, 0.15), (0, 0, 2.75), "IZ_White")
    for x in (-1.2, 1.2):
        for y in (-2.4, 2.3):
            b.cyl(0.5, 0.5, 0.35, (x, y, 0.5), "IZ_Tire", seg=10, rot=[('Y', 90)])
    return b.build()

# ------------------------------------------------------------------ puesto de mercadillo, 2 x 2 m cell
def puesto():
    b = Builder("IZ_Puesto")
    b.box((1.8, 1.0, 0.9), (0, -0.35, 0.45), "M_Wood")                    # counter (front = -Y)
    b.box((1.8, 0.08, 1.9), (0, 0.85, 0.95), "M_Canvas")                  # back cloth (blocks the view)
    for x in (-0.85, 0.85):
        for y in (-0.85, 0.85):
            b.box((0.08, 0.08, 2.3), (x, y, 1.15), "M_Wood")
    for i in range(4):                                                    # striped awning
        b.box((0.47, 2.1, 0.06), (-0.7 + i * 0.47, 0, 2.35), "IZ_AwningPaint" if i % 2 == 0 else "IZ_White", rot=[('X', -10)])
    for i in range(5):                                                    # fruit / goods
        b.box((0.25, 0.25, 0.2), (-0.7 + i * 0.35, -0.4, 1.0), "IZ_Fruit" if i % 2 else "IZ_Fruit2")
    return b.build()

# ------------------------------------------------------------------ tienda de campaña (big tent), 2 x 2 m cell
def tienda():
    b = Builder("IZ_Tienda")
    b.prism(1.9, 1.9, 2.3, (0, 0, 0), "IZ_TentPaint")
    b.prism(0.7, 0.05, 1.0, (0, -0.96, 0), "IZ_Dark")                     # opening
    b.box((0.05, 0.05, 2.5), (0, -1.0, 1.25), "M_Wood")
    b.box((0.05, 0.05, 2.5), (0, 1.0, 1.25), "M_Wood")
    return b.build()

# ------------------------------------------------------------------ caseta (beach hut), 2 x 2 m cell
def caseta():
    b = Builder("IZ_Caseta")
    b.box((1.8, 1.8, 2.1), (0, 0, 1.05), "M_Wood")
    b.box((0.7, 0.05, 1.6), (0, -0.91, 0.8), "IZ_Dark")
    b.box((0.5, 0.05, 0.4), (0.55, -0.91, 1.45), "IZ_Glass")
    b.pyramid(2.4, 2.4, 1.1, (0, 0, 2.1), "IZ_Thatch")
    return b.build()

# ------------------------------------------------------------------ contenedor (shipping container), 2 x 4 m
def contenedor():
    b = Builder("IZ_Contenedor")
    b.box((1.9, 3.9, 2.4), (0, 0, 1.2), "IZ_ContainerPaint")
    for i in range(9):
        y = -1.8 + i * 0.45
        b.box((1.96, 0.08, 2.3), (0, y, 1.2), "IZ_ContainerPaint")       # ribs
    b.box((1.7, 0.06, 2.2), (0, -1.96, 1.2), "M_Iron")                    # doors
    return b.build()

# ------------------------------------------------------------------ barco de rescate, 3.4 x 9 m, origin at the waterline
def barco():
    b = Builder("IZ_Barco")
    b.box((3.2, 7.0, 1.4), (0, -0.5, 0.2), "IZ_White")                    # hull
    b.box((2.2, 2.0, 1.4), (0, 3.6, 0.3), "IZ_White", rot=[('Z', 45)])    # bow
    b.box((3.25, 7.0, 0.3), (0, -0.5, 0.75), "IZ_Red")                    # stripe
    b.box((2.4, 2.8, 1.8), (0, -1.0, 1.8), "IZ_White")                    # cabin
    b.box((2.45, 2.5, 0.6), (0, -1.0, 2.1), "IZ_Glass")
    b.box((0.12, 0.12, 3.0), (0, -1.0, 4.2), "M_Iron")                    # mast
    b.box((0.9, 0.06, 0.5), (0.45, -1.0, 5.4), "IZ_Red")                  # flag
    return b.build()

# ------------------------------------------------------------------ avioneta estrellada (crashed light plane), ~8 x 9 m
def avioneta():
    b = Builder("IZ_Avioneta")
    b.box((1.3, 6.5, 1.3), (0, 0, 0.6), "IZ_White", rot=[('X', 4)])      # fuselage, nose dug in
    b.box((1.0, 1.4, 0.8), (0, 2.4, 1.3), "IZ_Glass")                     # cockpit
    b.box((4.5, 1.6, 0.18), (-2.8, 1.2, 0.9), "IZ_White", rot=[('Y', -8)])   # left wing
    b.box((2.2, 1.6, 0.18), (1.7, 1.2, 0.5), "IZ_White", rot=[('Y', 25)])    # broken right wing
    b.box((1.2, 1.4, 0.18), (3.4, 0.4, 0.1), "IZ_White", rot=[('Z', 30)])    # wing piece on the sand
    b.box((0.15, 1.2, 1.4), (0, -3.0, 1.6), "IZ_Red")                     # tail fin
    b.box((2.4, 0.9, 0.12), (0, -3.0, 1.0), "IZ_White")                   # tail plane
    b.box((0.12, 0.1, 1.6), (0, 3.3, 0.6), "M_Iron", rot=[('Y', 35)])     # bent propeller
    b.box((1.32, 6.0, 0.25), (0, -0.2, 0.9), "IZ_Red")                    # stripe
    return b.build()

# ------------------------------------------------------------------ grada (bleachers), 2 m segment, 1 m tall
def grada():
    b = Builder("IZ_Grada")
    for i in range(3):
        b.box((1.95, 0.6, 0.3), (0, -0.6 + i * 0.6, 0.15 + i * 0.3), "M_Wood")
        # iron frame under the seat, inset and below it: the old one shared the seat's side faces (flicker)
        if i > 0:
            b.box((1.89, 0.54, i * 0.3), (0, -0.6 + i * 0.6, i * 0.15), "M_Iron")
    return b.build()

clear()
for fn in (faro, coche, autobus, puesto, tienda, caseta, contenedor, barco, avioneta, grada):
    make(fn)
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("PROPS_RESULT " + json.dumps(res))
