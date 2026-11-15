# Kit of pieces for the map "La Serenisima" (Venice + the Monumental cemetery): one FBX per piece in
# Assets/Maps/Venecia/Models (+ a .blend copy). Run in background:
#   blender.exe -b --factory-startup --python make_venecia_props.py
# Conventions as make_map_props.py: origin = centre of the footprint on the ground, front = Blender -Y (= Unity +Z),
# up = +Z, UVs box-projected at 1 tile = 2 m. M_* materials are remapped to the island's textured .mat files;
# VN_* are flat colours (VN_*Paint get colour variants in Unity).
# Facade strips (VN_Fachada_*) are 2 m wide details that hang on the face of a building box: the wall is y = 0 and
# everything sticks out towards -Y.
import bpy, bmesh, os, math, json
from mathutils import Vector, Matrix

OUT = r"C:\Users\Usuario\animal_island1\Assets\Maps\Venecia\Models"
BLEND = r"C:\Users\Usuario\Documents\BeachAnimals\Venecia\Venecia_Props.blend"

COLORS = {
    "M_Wood": (0.45, 0.28, 0.14), "M_Stone": (0.55, 0.55, 0.5), "M_Iron": (0.25, 0.25, 0.27), "M_StoneMoss": (0.5, 0.55, 0.45),
    "VN_Istria": (0.9, 0.88, 0.82), "VN_Dark": (0.06, 0.06, 0.07), "VN_Glass": (0.13, 0.19, 0.24), "VN_Shutter": (0.16, 0.33, 0.22),
    "VN_ShutterBrown": (0.36, 0.2, 0.12), "VN_Brick": (0.62, 0.3, 0.2), "VN_Tile": (0.72, 0.36, 0.22), "VN_Copper": (0.36, 0.6, 0.5),
    "VN_Gold": (0.95, 0.75, 0.2), "VN_Lamp": (1.0, 0.8, 0.75), "VN_Black": (0.05, 0.05, 0.06), "VN_Red": (0.7, 0.08, 0.1),
    "VN_AwningPaint": (0.75, 0.2, 0.15), "VN_Bronze": (0.3, 0.36, 0.3), "VN_Marble": (0.82, 0.82, 0.8), "VN_Granite": (0.4, 0.4, 0.42),
    "VN_Leaves": (0.13, 0.3, 0.16), "VN_White": (0.93, 0.93, 0.9), "VN_FloatPaint": (0.55, 0.2, 0.6), "VN_Flower": (0.9, 0.35, 0.5),
    "VN_Pole": (0.33, 0.24, 0.16), "VN_Blue": (0.15, 0.3, 0.6),
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
        if name == "VN_Lamp":
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

    def cyl(self, r1, r2, depth, at, m, seg=8, rot=None, scale=None):
        g = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=depth)
        if scale: bmesh.ops.scale(self.bm, vec=Vector(scale), verts=g["verts"])
        self._finish(g["verts"], m, rot, at)

    def ball(self, r, at, m, scale=(1, 1, 1), seg=8):
        g = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=max(4, seg // 2), radius=r)
        bmesh.ops.scale(self.bm, vec=Vector(scale), verts=g["verts"])
        self._finish(g["verts"], m, None, at)

    def disc(self, r, thick, at, m, seg=12):   # round piece facing -Y (window tops, clock faces)
        self.cyl(r, r, thick, at, m, seg=seg, rot=[('X', 90)])

    def pyramid(self, w, d, h, at, m):
        g = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=4, radius1=1.0, radius2=0.0, depth=h)
        bmesh.ops.rotate(self.bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(45), 3, 'Z'), verts=g["verts"])
        bmesh.ops.scale(self.bm, vec=Vector((w / math.sqrt(2), d / math.sqrt(2), 1)), verts=g["verts"])
        self._finish(g["verts"], m, None, (at[0], at[1], at[2] + h / 2))

    def build(self):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
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
    return {"name": ob.name, "verts": len(ob.data.vertices), "dims": [round(v, 2) for v in ob.dimensions]}

res, all_objs = [], []

def make(fn, *args):
    ob = fn(*args)
    res.append(export(ob))
    ob.location.x = len(all_objs) * 8
    all_objs.append(ob.name)

# ================================================================== facades (2 m wide strips, 7.6 m tall)
FLOOR = (0.45, 3.0, 5.3)   # sill heights of the three floors
TOP = 7.6

def window(b, x, z, w, h, arch="round", shutter="VN_Shutter", open_shutters=True):
    """Window with stone frame at wall y = 0; (x, z) = centre of the sill."""
    b.box((w + 0.24, 0.1, h + 0.2), (x, -0.05, z + h / 2), "VN_Istria")           # frame
    b.box((w, 0.06, h), (x, -0.1, z + h / 2), "VN_Glass")
    b.box((w + 0.4, 0.22, 0.1), (x, -0.11, z - 0.05), "VN_Istria")                # sill
    if arch == "round":
        b.disc(w / 2 + 0.12, 0.1, (x, -0.05, z + h), "VN_Istria")
        b.disc(w / 2, 0.14, (x, -0.06, z + h), "VN_Glass")
    elif arch == "gothic":
        b.box((w * 0.75 + 0.2, 0.1, w * 0.75 + 0.2), (x, -0.05, z + h), "VN_Istria", rot=[('Y', 45)])
        b.box((w * 0.72, 0.14, w * 0.72), (x, -0.06, z + h), "VN_Glass", rot=[('Y', 45)])
    b.box((0.05, 0.08, h), (x, -0.14, z + h / 2), "VN_Istria")                    # mullion
    if shutter:
        sw = w / 2 + 0.02
        for s in (-1, 1):
            if open_shutters: b.box((sw, 0.05, h), (x + s * (w / 2 + sw / 2 + 0.12), -0.07, z + h / 2), shutter)
            else: b.box((sw, 0.05, h), (x + s * sw / 2, -0.17, z + h / 2), shutter)

def facade_base(b):
    b.box((2.0, 0.12, 0.45), (0, -0.06, 0.225), "VN_Istria")                      # stone plinth
    b.box((2.0, 0.3, 0.22), (0, -0.15, TOP - 0.11), "VN_Istria")                  # cornice
    for i in range(4): b.box((0.14, 0.2, 0.22), (-0.75 + i * 0.5, -0.22, TOP - 0.36), "VN_Istria")   # dentils
    b.box((2.0, 0.06, 0.1), (0, -0.03, 2.75), "VN_Istria")                        # string course

def fachada_a():   # door + two floors of arched windows with green shutters
    b = Builder("VN_Fachada_A")
    facade_base(b)
    b.box((1.2, 0.14, 2.2), (0, -0.07, 1.1), "VN_Istria")
    b.disc(0.6, 0.14, (0, -0.07, 2.2), "VN_Istria")
    b.box((0.94, 0.1, 2.1), (0, -0.13, 1.05), "M_Wood")
    b.disc(0.47, 0.1, (0, -0.13, 2.2), "VN_Dark")
    b.ball(0.05, (0.3, -0.2, 1.05), "VN_Gold")
    b.box((1.5, 0.4, 0.14), (0, -0.2, 0.07), "VN_Istria")                         # step
    window(b, 0, FLOOR[1] + 0.3, 0.8, 1.3)
    window(b, 0, FLOOR[2] + 0.3, 0.8, 1.2)
    return b.build()

def fachada_b():   # shop with awning, balcony with two tall windows, square windows above
    b = Builder("VN_Fachada_B")
    facade_base(b)
    b.box((1.6, 0.1, 1.7), (0, -0.05, 1.3), "VN_Istria")
    b.box((1.44, 0.08, 1.54), (0, -0.1, 1.3), "VN_Glass")
    b.box((1.9, 0.9, 0.08), (0, -0.5, 2.35), "VN_AwningPaint", rot=[('X', -22)])
    b.box((1.9, 0.05, 0.25), (0, -0.92, 2.08), "VN_AwningPaint")
    for s in (-1, 1): b.box((0.04, 0.04, 0.5), (s * 0.9, -0.9, 2.3), "M_Iron", rot=[('X', -60)])
    b.box((1.9, 0.6, 0.1), (0, -0.3, FLOOR[1] + 0.05), "VN_Istria")               # balcony slab
    for s in (-1, 1): b.box((0.14, 0.3, 0.3), (s * 0.7, -0.2, FLOOR[1] - 0.15), "VN_Istria")
    for i in range(9): b.cyl(0.035, 0.035, 0.75, (-0.84 + i * 0.21, -0.55, FLOOR[1] + 0.47), "VN_Istria", seg=6)
    b.box((1.9, 0.1, 0.08), (0, -0.55, FLOOR[1] + 0.88), "VN_Istria")
    for s in (-1, 1):
        b.box((0.07, 0.07, 0.86), (s * 0.92, -0.55, FLOOR[1] + 0.5), "VN_Istria")
        b.box((0.07, 0.5, 0.08), (s * 0.92, -0.3, FLOOR[1] + 0.88), "VN_Istria")
        window(b, s * 0.45, FLOOR[1] + 0.1, 0.55, 1.7, shutter=None)
    for s in (-1, 1): window(b, s * 0.45, FLOOR[2] + 0.45, 0.55, 0.9, arch="none", shutter="VN_ShutterBrown", open_shutters=False)
    return b.build()

def fachada_c():   # barred low window, gothic window with white trim, round oculus
    b = Builder("VN_Fachada_C")
    facade_base(b)
    b.box((0.9, 0.1, 0.9), (0, -0.05, 1.5), "VN_Istria")
    b.box((0.74, 0.06, 0.74), (0, -0.09, 1.5), "VN_Dark")
    for i in range(4): b.box((0.035, 0.035, 0.8), (-0.27 + i * 0.18, -0.13, 1.5), "M_Iron")
    for i in range(2): b.box((0.8, 0.035, 0.035), (0, -0.13, 1.3 + i * 0.4), "M_Iron")
    window(b, 0, FLOOR[1] + 0.25, 0.9, 1.4, arch="gothic", shutter=None)
    for s in (-1, 1): b.cyl(0.07, 0.07, 1.5, (s * 0.6, -0.12, FLOOR[1] + 0.95), "VN_Istria", seg=6)
    b.disc(0.42, 0.1, (0, -0.05, FLOOR[2] + 1.0), "VN_Istria")
    b.disc(0.3, 0.14, (0, -0.06, FLOOR[2] + 1.0), "VN_Glass")
    b.box((0.05, 0.16, 0.6), (0, -0.07, FLOOR[2] + 1.0), "VN_Istria")
    b.box((0.6, 0.16, 0.05), (0, -0.07, FLOOR[2] + 1.0), "VN_Istria")
    return b.build()

def fachada_d():   # worn wall: brick patches, drainpipe, one small window, washing line pole, flower box
    b = Builder("VN_Fachada_D")
    facade_base(b)
    for x, z, w, h in ((-0.5, 1.4, 0.8, 1.0), (0.45, 3.9, 0.7, 0.8), (-0.3, 6.2, 1.0, 0.6), (0.55, 0.9, 0.5, 0.5)):
        b.box((w, 0.035, h), (x, -0.017, z), "VN_Brick")
    b.cyl(0.05, 0.05, TOP - 0.5, (0.88, -0.09, (TOP - 0.5) / 2 + 0.2), "M_Iron", seg=6)
    for z in (1.5, 3.6, 5.8): b.box((0.16, 0.14, 0.06), (0.88, -0.07, z), "M_Iron")
    window(b, -0.2, FLOOR[1] + 0.6, 0.7, 1.0, arch="none", shutter="VN_ShutterBrown")
    b.box((0.9, 0.22, 0.2), (-0.2, -0.24, FLOOR[1] + 0.48), "VN_Brick")           # flower box
    for i in range(4): b.ball(0.11, (-0.5 + i * 0.2, -0.26, FLOOR[1] + 0.64), "VN_Flower" if i % 2 else "VN_Leaves")
    window(b, -0.2, FLOOR[2] + 0.5, 0.6, 0.8, arch="none", shutter="VN_Shutter", open_shutters=False)
    return b.build()

def frente_templo():   # 4 m wide classical front for the mausoleums: columns, pediment, bronze door
    b = Builder("VN_FrenteTemplo")
    b.box((4.0, 0.5, 0.3), (0, -0.25, 0.15), "VN_Marble")
    b.box((3.4, 0.35, 0.2), (0, -0.45, 0.1), "VN_Marble")
    for x in (-1.6, -0.75, 0.75, 1.6):
        b.box((0.42, 0.42, 0.25), (x, -0.3, 0.42), "VN_Marble")
        b.cyl(0.16, 0.14, 3.5, (x, -0.3, 2.3), "VN_Marble", seg=10)
        b.box((0.4, 0.4, 0.2), (x, -0.3, 4.15), "VN_Marble")
    b.box((4.0, 0.55, 0.45), (0, -0.28, 4.48), "VN_Marble")
    bm = b.bm                                                                      # pediment (triangle)
    vs = [bm.verts.new(v) for v in [(-2.05, -0.55, 4.7), (2.05, -0.55, 4.7), (0, -0.55, 5.75), (-2.05, 0, 4.7), (2.05, 0, 4.7), (0, 0, 5.75)]]
    for f in [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]: bm.faces.new([vs[i] for i in f])
    b._finish(vs, "VN_Marble")
    b.box((1.2, 0.1, 2.9), (0, -0.05, 1.75), "VN_Granite")
    b.box((1.0, 0.1, 2.7), (0, -0.1, 1.65), "VN_Bronze")
    b.box((0.04, 0.12, 2.7), (0, -0.12, 1.65), "VN_Dark")
    b.box((0.1, 0.1, 0.9), (0, -0.3, 6.2), "VN_Marble")                            # cross
    b.box((0.5, 0.1, 0.1), (0, -0.3, 6.35), "VN_Marble")
    return b.build()

# ================================================================== rooftops
def chimenea():   # Venetian flared chimney
    b = Builder("VN_Chimenea")
    b.box((0.5, 0.5, 1.3), (0, 0, 0.65), "VN_Brick")
    b.cyl(0.3, 0.6, 0.7, (0, 0, 1.65), "VN_Istria", seg=8)
    b.cyl(0.62, 0.5, 0.15, (0, 0, 2.07), "VN_Tile", seg=8)
    return b.build()

def cupula():   # green copper dome with lantern, 4 m across (on top of a mausoleum / church block)
    b = Builder("VN_Cupula")
    b.cyl(2.0, 2.0, 0.5, (0, 0, 0.25), "VN_Marble", seg=12)
    b.cyl(1.7, 1.7, 1.0, (0, 0, 1.0), "VN_Marble", seg=12)
    for i in range(12):
        a = i * math.pi / 6
        b.box((0.12, 0.3, 0.7), (1.72 * math.cos(a), 1.72 * math.sin(a), 1.0), "VN_Dark", rot=None)
    b.ball(1.85, (0, 0, 1.5), "VN_Copper", scale=(1, 1, 1.0), seg=12)
    b.cyl(0.45, 0.45, 0.9, (0, 0, 3.6), "VN_Marble", seg=8)
    b.cyl(0.55, 0.0, 0.7, (0, 0, 4.4), "VN_Copper", seg=8)
    b.ball(0.12, (0, 0, 4.85), "VN_Gold")
    b.box((0.06, 0.06, 0.6), (0, 0, 5.2), "VN_Gold")
    b.box((0.36, 0.06, 0.06), (0, 0, 5.3), "VN_Gold")
    return b.build()

def campanile_top():   # belfry + spire for the 6 x 6 m brick shaft; its 0.5 m floor slab is sunk into the shaft top
    b = Builder("VN_CampanileTop")
    b.box((6.4, 6.4, 0.5), (0, 0, 0.25), "VN_Istria")
    for sx in (-1, 1):
        for sy in (-1, 1): b.box((0.8, 0.8, 3.6), (sx * 2.6, sy * 2.6, 2.3), "VN_Istria")
    # open belfry: only the corner piers, so you can walk around the bell and jump off any side
    b.box((0.5, 0.5, 0.9), (0, 0, 3.65), "M_Wood")                                  # bell yoke
    b.cyl(0.9, 1.1, 1.3, (0, 0, 2.6), "VN_Bronze", seg=10)                         # the bell
    b.box((6.4, 6.4, 0.6), (0, 0, 4.4), "VN_Istria")
    b.box((5.2, 5.2, 2.6), (0, 0, 6.0), "VN_Brick")
    b.box((5.6, 5.6, 0.4), (0, 0, 7.5), "VN_Istria")
    b.pyramid(5.2, 5.2, 7.5, (0, 0, 7.7), "VN_Copper")
    b.ball(0.3, (0, 0, 15.3), "VN_Gold")
    b.box((0.12, 0.12, 1.2), (0, 0, 16.0), "VN_Gold")                              # golden angel (very stylised)
    b.box((0.9, 0.08, 0.5), (0, 0, 16.2), "VN_Gold")
    return b.build()

def almenas():   # crenellated crown for the 4 x 4 m Arsenale gate towers
    b = Builder("VN_Almenas")
    b.box((4.5, 4.5, 0.5), (0, 0, 0.25), "VN_Istria")
    for i in range(5):
        t = -1.9 + i * 0.95
        for s in (-1, 1):
            b.box((0.5, 0.4, 0.9), (t, s * 2.0, 0.95), "VN_Brick")
            b.box((0.4, 0.5, 0.9), (s * 2.0, t, 0.95), "VN_Brick")
            b.box((0.6, 0.5, 0.12), (t, s * 2.0, 1.46), "VN_Istria")
            b.box((0.5, 0.6, 0.12), (s * 2.0, t, 1.46), "VN_Istria")
    b.cyl(0.05, 0.05, 2.6, (0, 0, 1.8), "M_Iron", seg=6)
    b.box((1.1, 0.04, 0.7), (0.58, 0, 2.7), "VN_Red")                              # flag
    return b.build()

# ================================================================== street furniture
def balaustrada():   # 2 m of bridge / quay balustrade, 0.95 m tall, along X
    b = Builder("VN_Balaustrada")
    b.box((2.0, 0.3, 0.16), (0, 0, 0.08), "VN_Istria")
    for i in range(7): b.cyl(0.075, 0.05, 0.62, (-0.75 + i * 0.25, 0, 0.47), "VN_Istria", seg=6)
    for i in range(7): b.ball(0.085, (-0.75 + i * 0.25, 0, 0.36), "VN_Istria", seg=6)
    b.box((2.0, 0.26, 0.14), (0, 0, 0.86), "VN_Istria")
    for s in (-1, 1): b.box((0.2, 0.3, 0.95), (s * 0.9, 0, 0.475), "VN_Istria")
    return b.build()

def farola():   # three-armed street lamp with pink glass, 3.4 m
    b = Builder("VN_Farola")
    b.cyl(0.16, 0.12, 0.5, (0, 0, 0.25), "M_Iron", seg=8)
    b.cyl(0.06, 0.05, 2.5, (0, 0, 1.7), "M_Iron", seg=8)
    for i, (dx, dz) in enumerate(((0, 0.45), (-0.45, 0.1), (0.45, 0.1))):
        if dx: b.box((abs(dx), 0.04, 0.04), (dx / 2, 0, 2.85), "M_Iron")
        b.cyl(0.06, 0.13, 0.32, (dx, 0, 2.9 + dz), "VN_Lamp", seg=6)
        b.cyl(0.15, 0.03, 0.12, (dx, 0, 3.12 + dz), "M_Iron", seg=6)
    return b.build()

def palo(name, striped):   # mooring pole: 3.2 m above the origin (waterline), 1 m below
    b = Builder(name)
    if striped:
        for i in range(8): b.cyl(0.11, 0.11, 0.4, (0, 0, 0.2 + i * 0.4), "VN_Red" if i % 2 else "VN_White", seg=8)
        b.cyl(0.11, 0.11, 1.0, (0, 0, -0.5), "VN_Pole", seg=8)
        b.ball(0.15, (0, 0, 3.3), "VN_Gold")
    else:
        b.cyl(0.12, 0.09, 4.0, (0, 0, 1.0), "VN_Pole", seg=8, rot=[('X', 3)])
        b.cyl(0.1, 0.1, 0.25, (0, 0.1, 2.95), "VN_Black", seg=8)
    return b.build()

def gondola():   # 9 m gondola, origin at the waterline, bow towards -Y
    b = Builder("VN_Gondola")
    n = 12
    for i in range(n):
        t = (i + 0.5) / n
        y = -4.2 + t * 8.4
        w = 1.3 * math.sin(math.pi * (0.08 + 0.84 * t)) ** 0.7
        lift = 0.9 * abs(t - 0.5) ** 2.2 * 4
        b.box((w, 8.4 / n + 0.02, 0.5), (0, y, 0.12 + lift), "VN_Black")
        if 0.12 < t < 0.88: b.box((w * 0.82, 8.4 / n + 0.02, 0.06), (0, y, 0.36 + lift), "M_Wood")
    b.box((0.06, 0.5, 1.0), (0, -4.45, 1.15), "M_Iron", rot=[('X', -12)])           # ferro
    for i in range(5): b.box((0.07, 0.22, 0.07), (0, -4.62, 0.95 + i * 0.13), "M_Iron")
    b.box((0.06, 0.3, 0.6), (0, 4.4, 1.2), "M_Iron", rot=[('X', 20)])
    b.box((0.85, 0.7, 0.3), (0, 0.6, 0.5), "VN_Red")                               # seats
    b.box((0.85, 0.15, 0.6), (0, 1.0, 0.75), "VN_Red")
    b.box((0.9, 0.06, 0.08), (0, 1.08, 1.06), "VN_Gold")
    for s in (-1, 1): b.box((0.35, 0.35, 0.28), (s * 0.25, -0.5, 0.5), "VN_Red")
    b.cyl(0.03, 0.03, 3.6, (0.5, 2.2, 1.0), "M_Wood", seg=6, rot=[('X', 62)])        # oar
    return b.build()

def lancha():   # water taxi, 6.5 m, origin at the waterline
    b = Builder("VN_Lancha")
    b.box((1.9, 5.0, 0.8), (0, 0.4, 0.15), "VN_White")
    b.box((1.35, 1.6, 0.8), (0, -2.5, 0.2), "VN_White", rot=[('Z', 45)])
    b.box((1.92, 5.0, 0.12), (0, 0.4, 0.5), "M_Wood")
    b.box((1.6, 2.4, 0.75), (0, 1.0, 0.9), "M_Wood")
    b.box((1.62, 2.0, 0.35), (0, 1.0, 1.0), "VN_Glass")
    b.box((1.7, 2.6, 0.08), (0, 1.0, 1.3), "VN_White")
    b.box((1.4, 0.08, 0.5), (0, -0.9, 0.85), "VN_Glass", rot=[('X', -25)])
    b.box((0.5, 0.4, 0.5), (0, 3.0, 0.3), "VN_Dark")
    return b.build()

def pozzo():   # wellhead on two octagonal steps (vera da pozzo)
    b = Builder("VN_Pozzo")
    b.cyl(1.5, 1.5, 0.18, (0, 0, 0.09), "VN_Istria", seg=8)
    b.cyl(1.15, 1.15, 0.18, (0, 0, 0.27), "VN_Istria", seg=8)
    b.cyl(0.6, 0.72, 0.95, (0, 0, 0.83), "VN_Marble", seg=8)
    b.cyl(0.8, 0.8, 0.14, (0, 0, 1.36), "VN_Marble", seg=8)
    b.cyl(0.6, 0.6, 0.06, (0, 0, 1.45), "M_Iron", seg=8)
    return b.build()

def monumento():   # campo monument: steps, pedestal and a bronze pensioner with his cane, 3.6 x 3.6 x 5 m
    b = Builder("VN_Monumento")
    b.box((3.6, 3.6, 0.3), (0, 0, 0.15), "VN_Istria")
    b.box((2.9, 2.9, 0.3), (0, 0, 0.45), "VN_Istria")
    b.box((1.9, 1.9, 1.9), (0, 0, 1.55), "VN_Granite")
    b.box((2.1, 2.1, 0.2), (0, 0, 2.6), "VN_Istria")
    b.box((1.2, 0.06, 0.6), (0, -0.96, 1.7), "VN_Bronze")                          # plaque
    for s in (-1, 1): b.cyl(0.13, 0.11, 1.0, (s * 0.2, 0, 3.2), "VN_Bronze", seg=6)   # legs
    b.cyl(0.36, 0.3, 0.95, (0, 0, 4.15), "VN_Bronze", seg=8)                       # body
    b.ball(0.26, (0, -0.02, 4.85), "VN_Bronze")
    b.cyl(0.3, 0.3, 0.06, (0, -0.02, 5.02), "VN_Bronze", seg=8)                    # beret
    b.cyl(0.07, 0.07, 0.8, (0.5, -0.15, 4.2), "VN_Bronze", seg=6, rot=[('Y', -50)])  # arm pointing
    b.cyl(0.03, 0.03, 1.5, (-0.5, -0.2, 3.45), "VN_Bronze", seg=6)                  # cane
    return b.build()

def columna():   # column of the molo with a winged lion, 6.5 m
    b = Builder("VN_Columna")
    b.cyl(1.0, 1.0, 0.25, (0, 0, 0.125), "VN_Istria", seg=8)
    b.cyl(0.75, 0.75, 0.25, (0, 0, 0.375), "VN_Istria", seg=8)
    b.cyl(0.42, 0.34, 4.6, (0, 0, 2.8), "VN_Granite", seg=10)
    b.box((1.1, 1.1, 0.35), (0, 0, 5.28), "VN_Istria")
    b.box((0.4, 1.1, 0.45), (0, 0, 5.7), "VN_Bronze")                              # lion
    b.ball(0.26, (0, -0.6, 5.95), "VN_Bronze")
    for s in (-1, 1): b.box((0.06, 0.7, 0.5), (s * 0.24, 0.1, 6.15), "VN_Bronze", rot=[('Y', s * 25)])
    return b.build()

def mesa():   # cafe table, two chairs and a closed parasol
    b = Builder("VN_Mesa")
    b.cyl(0.45, 0.45, 0.05, (0, 0, 0.75), "VN_White", seg=10)
    b.cyl(0.04, 0.04, 0.72, (0, 0, 0.37), "M_Iron", seg=6)
    b.cyl(0.25, 0.25, 0.04, (0, 0, 0.02), "M_Iron", seg=8)
    for s in (-1, 1):
        b.box((0.42, 0.42, 0.05), (s * 0.72, 0, 0.45), "M_Wood")
        b.box((0.05, 0.42, 0.5), (s * 0.92, 0, 0.72), "M_Wood")
        for dx in (-0.17, 0.17):
            for dy in (-0.17, 0.17): b.box((0.04, 0.04, 0.45), (s * 0.72 + dx, dy, 0.225), "M_Iron")
    b.cyl(0.03, 0.03, 2.3, (0, 0.62, 1.15), "M_Wood", seg=6)
    b.cyl(0.05, 0.16, 1.2, (0, 0.62, 1.7), "VN_AwningPaint", seg=8)
    b.cyl(0.06, 0.04, 0.2, (0.12, -0.1, 0.87), "VN_Glass", seg=6)                   # spritz
    return b.build()

def carroza():   # carnival float: cart with a giant mask and ribbons, 1.9 x 1.9 x 2.6 m
    b = Builder("VN_Carroza")
    b.box((1.9, 1.9, 0.5), (0, 0, 0.55), "VN_FloatPaint")
    b.box((1.95, 1.95, 0.08), (0, 0, 0.82), "VN_Gold")
    for sx in (-1, 1):
        for sy in (-1, 1): b.cyl(0.3, 0.3, 0.12, (sx * 0.98, sy * 0.6, 0.3), "M_Wood", seg=10, rot=[('Y', 90)])
    b.ball(0.75, (0, 0, 1.75), "VN_White", scale=(1, 0.55, 1.15), seg=10)            # the mask
    for s in (-1, 1):
        b.ball(0.16, (s * 0.3, -0.36, 1.9), "VN_Dark", scale=(1.3, 0.5, 0.8))
        b.cyl(0.03, 0.03, 1.4, (s * 0.85, 0.5, 1.6), "VN_Gold", seg=6, rot=[('Y', s * 12)])
        b.box((0.5, 0.03, 0.22), (s * 1.0, 0.5, 2.2), "VN_Red")
    b.box((0.5, 0.25, 0.12), (0, -0.4, 1.35), "VN_Red")                             # lips
    b.pyramid(1.0, 0.5, 0.7, (0, 0.05, 2.5), "VN_Gold")
    return b.build()

def caballete():   # gondola hull on trestles (squero), 3.9 x 1.9 x 2.4 m, along X
    b = Builder("VN_Caballete")
    for x in (-1.2, 1.2):
        for s in (-1, 1): b.box((0.12, 0.12, 1.5), (x, s * 0.45, 0.7), "M_Wood", rot=[('X', s * 18)])
        b.box((0.14, 1.3, 0.12), (x, 0, 1.3), "M_Wood")
    n = 8
    for i in range(n):
        t = (i + 0.5) / n
        w = 1.2 * math.sin(math.pi * (0.1 + 0.8 * t)) ** 0.7
        b.box((3.8 / n + 0.02, w, 0.55), (-1.9 + t * 3.8, 0, 1.65 + 0.5 * abs(t - 0.5) ** 2 * 4), "VN_Black")
    b.box((0.3, 0.06, 0.8), (-2.0, 0, 2.3), "M_Iron")
    b.box((0.8, 0.5, 0.35), (0.3, -0.75, 0.18), "M_Wood")                           # tool chest
    b.cyl(0.2, 0.2, 0.5, (-0.6, 0.8, 0.25), "VN_Black", seg=8)                      # tar bucket
    return b.build()

# ================================================================== cemetery
def tumba_a():   # slab + headstone with a cross, low cover (1.3 m)
    b = Builder("VN_Tumba_A")
    b.box((1.1, 1.9, 0.3), (0, 0, 0.15), "VN_Granite")
    b.box((0.95, 1.7, 0.1), (0, -0.05, 0.35), "VN_Marble")
    b.box((0.95, 0.22, 1.0), (0, 0.82, 0.8), "VN_Marble")
    b.disc(0.47, 0.22, (0, 0.82, 1.3), "VN_Marble")
    b.box((0.08, 0.05, 0.4), (0, 0.69, 1.0), "VN_Bronze")
    b.box((0.26, 0.05, 0.08), (0, 0.69, 1.08), "VN_Bronze")
    b.cyl(0.1, 0.13, 0.25, (0.3, -0.6, 0.52), "VN_Bronze", seg=6)                   # vase
    for i in range(3): b.ball(0.09, (0.3 + (i - 1) * 0.08, -0.6, 0.72), "VN_Flower")
    b.box((0.16, 0.16, 0.26), (-0.3, -0.6, 0.53), "VN_Dark")                        # lantern
    b.box((0.1, 0.1, 0.14), (-0.3, -0.6, 0.55), "VN_Lamp")
    return b.build()

def tumba_b():   # sarcophagus on lion feet with a pitched lid, low cover (1.25 m)
    b = Builder("VN_Tumba_B")
    b.box((1.3, 2.0, 0.2), (0, 0, 0.1), "VN_Granite")
    for sx in (-1, 1):
        for sy in (-1, 1): b.box((0.22, 0.22, 0.25), (sx * 0.4, sy * 0.75, 0.32), "VN_Bronze")
    b.box((1.0, 1.8, 0.55), (0, 0, 0.72), "VN_Marble")
    b.box((1.1, 1.9, 0.08), (0, 0, 1.03), "VN_Marble")
    bm = b.bm
    vs = [bm.verts.new(v) for v in [(-0.55, -0.95, 1.07), (0.55, -0.95, 1.07), (0, -0.95, 1.3), (-0.55, 0.95, 1.07), (0.55, 0.95, 1.07), (0, 0.95, 1.3)]]
    for f in [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]: bm.faces.new([vs[i] for i in f])
    b._finish(vs, "VN_Marble")
    b.disc(0.2, 0.04, (0, -0.91, 0.72), "VN_Bronze")
    return b.build()

def tumba_c():   # granite pyramid monument with a bust, 1.9 x 1.9 x 3.2 m
    b = Builder("VN_Tumba_C")
    b.box((1.9, 1.9, 0.3), (0, 0, 0.15), "VN_Granite")
    b.cyl(1.2, 0.55, 2.0, (0, 0, 1.3), "VN_Granite", seg=4, rot=[('Z', 45)])
    b.box((0.85, 0.85, 0.15), (0, 0, 2.37), "VN_Marble")
    b.cyl(0.26, 0.2, 0.4, (0, 0, 2.65), "VN_Bronze", seg=8)
    b.ball(0.22, (0, 0, 3.02), "VN_Bronze")
    b.box((0.7, 0.05, 0.4), (0, -0.74, 1.0), "VN_Bronze", rot=[('X', 17)])
    return b.build()

def angel():   # angel on a pedestal, 1.6 x 1.6 x 3.3 m
    b = Builder("VN_Angel")
    b.box((1.6, 1.6, 0.3), (0, 0, 0.15), "VN_Granite")
    b.box((1.0, 1.0, 1.1), (0, 0, 0.85), "VN_Marble")
    b.box((1.15, 1.15, 0.12), (0, 0, 1.46), "VN_Marble")
    b.cyl(0.42, 0.24, 1.2, (0, 0, 2.12), "VN_Bronze", seg=8)                        # robe
    b.ball(0.19, (0, -0.04, 2.92), "VN_Bronze")
    for s in (-1, 1):
        b.box((0.09, 0.5, 1.25), (s * 0.38, 0.22, 2.6), "VN_Bronze", rot=[('Y', s * 24), ('Z', s * -18)])   # wings
        b.cyl(0.06, 0.05, 0.7, (s * 0.3, -0.22, 2.75), "VN_Bronze", seg=6, rot=[('Y', s * 50), ('X', 30)])     # arms raised
    return b.build()

def mausoleo():   # small family chapel, 1.95 x 1.95 x 3.6 m, door on the front
    b = Builder("VN_Mausoleo")
    b.box((1.95, 1.95, 0.25), (0, 0, 0.125), "VN_Granite")
    b.box((1.7, 1.7, 2.3), (0, 0, 1.4), "VN_Marble")
    b.box((1.95, 1.95, 0.2), (0, 0, 2.65), "VN_Marble")
    b.pyramid(1.9, 1.9, 0.9, (0, 0, 2.75), "VN_Copper")
    b.box((0.08, 0.08, 0.5), (0, 0, 3.85), "VN_Gold")
    b.box((0.3, 0.08, 0.08), (0, 0, 3.95), "VN_Gold")
    b.box((0.9, 0.08, 1.7), (0, -0.86, 1.1), "VN_Dark")
    b.disc(0.45, 0.08, (0, -0.86, 1.95), "VN_Dark")
    for s in (-1, 1): b.cyl(0.09, 0.08, 1.9, (s * 0.62, -0.9, 1.2), "VN_Granite", seg=8)
    for s in (-1, 1): b.disc(0.2, 0.06, (s * 0.87, 0, 1.7), "VN_Glass", seg=8) if False else None
    return b.build()

def cipres():   # cypress, 6.5 m
    b = Builder("VN_Cipres")
    b.cyl(0.14, 0.11, 1.2, (0, 0, 0.6), "VN_Pole", seg=6)
    b.cyl(0.55, 0.75, 1.6, (0, 0, 1.6), "VN_Leaves", seg=8)
    b.cyl(0.75, 0.55, 2.6, (0, 0, 3.7), "VN_Leaves", seg=8)
    b.cyl(0.55, 0.0, 1.6, (0, 0, 5.8), "VN_Leaves", seg=8)
    return b.build()

def torre():   # the leaning spiral tower of the cemetery: marble drum with a ramp of figures winding up, 1.9 m base, 9 m
    b = Builder("VN_TorreEspiral")
    b.box((1.9, 1.9, 1.4), (0, 0, 0.7), "VN_Marble")
    b.box((0.6, 0.06, 1.0), (0, -0.96, 0.6), "VN_Dark")
    tilt = [('X', 5), ('Y', 4)]
    turns, steps = 5, 50
    for i in range(steps):
        t = i / steps
        a = t * turns * 2 * math.pi
        r = 0.86 - 0.22 * t
        z = 1.5 + t * 7.0
        p = Vector((r * math.cos(a), r * math.sin(a), z))
        b.box((0.42, 0.3, 0.2), tuple(p), "VN_Marble", rot=None)
        if i % 3 == 0: b.cyl(0.08, 0.06, 0.5, (p.x, p.y, z + 0.35), "VN_Istria", seg=5)   # figures
    for k in range(6):
        z = 1.5 + k * 1.25
        b.cyl(0.6 - k * 0.045, 0.56 - k * 0.045, 1.2, (0, 0, z + 0.6), "VN_Glass" if k % 2 else "VN_Marble", seg=10)
    b.cyl(0.75, 0.75, 0.2, (0, 0, 8.9), "VN_Marble", seg=10)
    # lean the shaft (everything above the plinth)
    verts = [v for v in b.bm.verts if v.co.z > 1.45]
    for axis, deg in tilt:
        bmesh.ops.rotate(b.bm, cent=Vector((0, 0, 1.45)), matrix=Matrix.Rotation(math.radians(deg), 3, axis), verts=verts)
    return b.build()

def portal():   # bridge gate: two stone posts with lamps and an iron arch, 2 m opening (scaled in Unity)
    b = Builder("VN_PortalPuente")
    for s in (-1, 1):
        b.box((0.4, 0.4, 2.6), (s * 1.2, 0, 1.3), "VN_Istria")
        b.box((0.5, 0.5, 0.15), (s * 1.2, 0, 2.67), "VN_Istria")
        b.cyl(0.07, 0.15, 0.36, (s * 1.2, 0, 2.95), "VN_Lamp", seg=6)
        b.cyl(0.17, 0.03, 0.14, (s * 1.2, 0, 3.2), "M_Iron", seg=6)
    n = 9
    for i in range(n):
        a0 = math.pi * i / n
        a1 = math.pi * (i + 1) / n
        x0, z0, x1, z1 = 1.2 * math.cos(a0), 0.7 * math.sin(a0), 1.2 * math.cos(a1), 0.7 * math.sin(a1)
        ln = math.hypot(x1 - x0, z1 - z0)
        b.box((ln + 0.02, 0.06, 0.06), ((x0 + x1) / 2, 0, 2.75 + (z0 + z1) / 2), "M_Iron", rot=[('Y', -math.degrees(math.atan2(z1 - z0, x1 - x0)))])
    return b.build()

def mascara(name, peste):   # carnival mask worn by the old men: origin = centre of the face, front = -Y
    b = Builder(name)
    if peste:
        b.ball(0.16, (0, 0, 0), "VN_White", scale=(1, 0.5, 1.15))
        b.cyl(0.07, 0.015, 0.34, (0, -0.2, -0.04), "VN_White", seg=6, rot=[('X', 80)])   # beak
        for s in (-1, 1): b.disc(0.045, 0.03, (s * 0.07, -0.085, 0.045), "VN_Dark", seg=8)
        b.cyl(0.24, 0.24, 0.025, (0, 0.02, 0.17), "VN_Black", seg=10)                   # hat brim
        b.cyl(0.13, 0.13, 0.12, (0, 0.02, 0.24), "VN_Black", seg=10)
    else:
        b.ball(0.17, (0, 0, 0), "VN_White", scale=(1, 0.45, 1.2))
        b.box((0.2, 0.12, 0.14), (0, -0.07, -0.1), "VN_White", rot=[('X', 25)])            # bauta jaw
        for s in (-1, 1): b.ball(0.04, (s * 0.07, -0.075, 0.05), "VN_Dark", scale=(1.4, 0.5, 0.8))
        b.box((0.4, 0.2, 0.03), (0, 0.02, 0.19), "VN_Black")                               # tricorn
        b.pyramid(0.36, 0.2, 0.12, (0, 0.02, 0.2), "VN_Black")
        b.box((0.3, 0.02, 0.03), (0, -0.085, 0.13), "VN_Gold")
    return b.build()

clear()
for fn in (fachada_a, fachada_b, fachada_c, fachada_d, frente_templo, chimenea, cupula, campanile_top, almenas, balaustrada, farola,
           gondola, lancha, pozzo, monumento, columna, mesa, carroza, caballete, tumba_a, tumba_b, tumba_c, angel, mausoleo, cipres,
           torre, portal):
    make(fn)
make(palo, "VN_Palo", False)
make(palo, "VN_PaloRayas", True)
make(mascara, "VN_Mascara_Bauta", False)
make(mascara, "VN_Mascara_Peste", True)
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("PROPS_RESULT " + json.dumps(res))
