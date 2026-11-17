# Kit of pieces for the map "Hotel Jubiles" (a 70s Benidorm hotel on a stormy night, full of The Shining parodies): one FBX
# per piece in Assets/Maps/HotelJubiles/Models (+ a .blend copy). Run in background:
#   blender.exe -b --factory-startup --python make_hotel_props.py              (kit + coplanar-face report)
#   blender.exe -b --factory-startup --python make_hotel_props.py -- sheet     (also renders Renders/HotelKit_*.png)
# Same conventions and the same Builder as make_venecia_props.py (its helper section is reused as is): origin = centre of
# the footprint on the ground, front = Blender -Y (= Unity +Z), up = +Z, 1 cell = 2 x 2 m. A storey is 3.6 m of room plus
# a 0.4 m slab (4 m).
# Wall pieces (HJ_Fachada_*, door frames, lamps, signs, panels) hang on the wall plane y = 0 with the origin at floor
# level and everything sticking out towards -Y; facade strips are 2 m wide and one storey (4 m) tall.
# Hanging pieces (HJ_Arana, HJ_BolaEspejos) have the origin at the ceiling hook and hang down (-Z). Door leaves have the
# origin at the hinge (floor level). Worn accessories say where their origin is.
# Materials: M_* = the island's textured materials (remapped on import), HJ_*Paint = recoloured per instance (Repaint),
# HJ_Emit_<Name> = everything that glows, one slot per kind of light so the light kit can switch each group on and off:
# the FBX keeps the dim "off" colour, EMIT below is the colour when it is on.
# Faces: no two faces of different materials in one plane (they flicker when the camera moves): pieces sit >= 1.5 cm
# apart (a few mm on hand-sized accessories); `coplanar_pairs` reports any pair left.
import os, sys, math, json

_here = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(_here, "make_venecia_props.py"), encoding="utf-8").read().split("# ================================================================== facades")[0])

OUT = r"C:\Users\Usuario\animal_island1\Assets\Maps\HotelJubiles\Models"
BLEND = r"C:\Users\Usuario\Documents\BeachAnimals\HotelJubiles\HotelJubiles_Props.blend"
RENDERS = r"C:\Users\Usuario\animal_island1\Renders"

COLORS.update({
    "M_Leaves": (0.2, 0.42, 0.18), "M_Bark": (0.42, 0.32, 0.2), "M_Palm": (0.3, 0.52, 0.22), "M_Canvas": (0.8, 0.74, 0.6),
    "HJ_Hormigon": (0.9, 0.88, 0.83), "HJ_Gresite": (0.86, 0.5, 0.18), "HJ_Marron": (0.36, 0.2, 0.1), "HJ_Naranja": (0.92, 0.45, 0.1),
    "HJ_Mostaza": (0.85, 0.65, 0.15), "HJ_Aguacate": (0.5, 0.56, 0.22), "HJ_Crema": (0.93, 0.88, 0.74), "HJ_Rosa": (0.95, 0.55, 0.68),
    "HJ_Aluminio": (0.72, 0.73, 0.75), "HJ_Cromo": (0.86, 0.87, 0.9), "HJ_Laton": (0.78, 0.6, 0.25), "HJ_Oro": (0.95, 0.75, 0.2),
    "HJ_Metal": (0.3, 0.31, 0.33), "HJ_Negro": (0.05, 0.05, 0.06), "HJ_Blanco": (0.95, 0.95, 0.93), "HJ_Oscuro": (0.03, 0.03, 0.035),
    "HJ_Madera": (0.45, 0.27, 0.13), "HJ_MaderaOscura": (0.24, 0.13, 0.07), "HJ_Marmol": (0.86, 0.84, 0.8), "HJ_Terrazo": (0.76, 0.71, 0.63),
    "HJ_Cristal": (0.16, 0.24, 0.3), "HJ_CristalAmbar": (0.88, 0.7, 0.42), "HJ_Espejo": (0.72, 0.78, 0.84), "HJ_Cristalito": (0.86, 0.9, 0.95),
    "HJ_Rojo": (0.78, 0.1, 0.1), "HJ_Granate": (0.45, 0.05, 0.08), "HJ_Amarillo": (0.96, 0.8, 0.1), "HJ_Azul": (0.3, 0.45, 0.8),
    "HJ_Turquesa": (0.15, 0.62, 0.68), "HJ_Asfalto": (0.26, 0.27, 0.28), "HJ_Uralita": (0.68, 0.7, 0.68), "HJ_Goma": (0.08, 0.08, 0.08),
    "HJ_Piel": (0.9, 0.7, 0.58), "HJ_Foto": (0.55, 0.45, 0.32), "HJ_Comida": (0.92, 0.7, 0.2), "HJ_Gamba": (0.95, 0.45, 0.35),
    "HJ_Botella": (0.2, 0.42, 0.22), "HJ_Cafe": (0.25, 0.13, 0.06), "HJ_Oxido": (0.55, 0.28, 0.14), "HJ_Ladrillo": (0.62, 0.3, 0.2),
    "HJ_Agua": (0.6, 0.78, 0.86), "HJ_Paja": (0.82, 0.68, 0.36), "HJ_Verde": (0.15, 0.5, 0.25), "HJ_Asado": (0.62, 0.34, 0.12),
    "HJ_TelaPaint": (0.85, 0.42, 0.12), "HJ_ToallaPaint": (0.2, 0.5, 0.85), "HJ_ToldoPaint": (0.9, 0.32, 0.15),
})
EMIT = {   # the colour of each light when it is on (the FBX keeps a dim version of it: the light kit switches them)
    "HJ_Emit_Ventana": (1.0, 0.74, 0.4), "HJ_Emit_NeonRosa": (1.0, 0.3, 0.72), "HJ_Emit_NeonCian": (0.2, 0.95, 1.0),
    "HJ_Emit_Salida": (0.15, 1.0, 0.35), "HJ_Emit_Emergencia": (1.0, 0.1, 0.05), "HJ_Emit_Arana": (1.0, 0.85, 0.55),
    "HJ_Emit_Aplique": (1.0, 0.7, 0.38), "HJ_Emit_Lampara": (1.0, 0.76, 0.45), "HJ_Emit_Piscina": (0.3, 0.95, 1.0),
    "HJ_Emit_Bola": (0.92, 0.92, 1.0), "HJ_Emit_Helipuerto": (1.0, 0.85, 0.2), "HJ_Emit_Ascensor": (1.0, 0.55, 0.15),
    "HJ_Emit_Cabina": (1.0, 0.95, 0.85), "HJ_Emit_Minibar": (1.0, 0.45, 0.2), "HJ_Emit_Barra": (1.0, 0.72, 0.3),
    "HJ_Emit_BarPiscina": (1.0, 0.35, 0.55), "HJ_Emit_Caldera": (1.0, 0.42, 0.06), "HJ_Emit_Piloto": (1.0, 0.15, 0.1),
    "HJ_Emit_Bingo": (1.0, 0.8, 0.3), "HJ_Emit_Faro": (1.0, 0.95, 0.75), "HJ_Emit_Gafas": (0.6, 1.0, 0.9), "HJ_Emit_Numero": (1.0, 0.95, 0.6),
}
for _k, _v in EMIT.items(): COLORS[_k] = tuple(round(0.06 + 0.3 * c, 3) for c in _v)

def mat(name):   # replaces the Venice one: dim base colour for the lights; the viewport (and the contact sheet) shows them on
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (*COLORS[name], 1)
        bsdf.inputs["Roughness"].default_value = 0.6
        m.diffuse_color = (*EMIT.get(name, COLORS[name]), 1)
    return m

ROOM, ST = 3.6, 4.0          # room height, storey height
RISE = 2.0 / 3.0             # a stair flight climbs one level (2 m) over three cells

# ================================================================== helpers
def tube(b, p0, p1, r, m, seg=6):
    cone(b, p0, p1, r, r, m, seg)

def cone(b, p0, p1, r0, r1, m, seg=8):   # tapered cylinder from p0 (radius r0) to p1 (radius r1)
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    g = bmesh.ops.create_cone(b.bm, cap_ends=True, segments=seg, radius1=r0, radius2=r1, depth=d.length)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(b.bm, cent=Vector(), matrix=q.to_matrix(), verts=g["verts"])
    b._finish(g["verts"], m, None, tuple((p0 + p1) / 2))

def path(b, pts, r, m, seg=6, joints=True):   # tube through a list of points
    for p, q in zip(pts, pts[1:]): tube(b, p, q, r, m, seg)
    if joints:
        for p in pts[1:-1]: b.ball(r, p, m, seg=seg)

def ring(b, c, r, tr, m, axis='Z', n=12, rz=None, seg=5):   # a ring of tube (torus-like); axis = normal of its plane
    rz = r if rz is None else rz
    pts = []
    for i in range(n + 1):
        a = 2 * math.pi * i / n
        u, v = r * math.cos(a), rz * math.sin(a)
        pts.append((c[0], c[1] + u, c[2] + v) if axis == 'X' else (c[0] + u, c[1], c[2] + v) if axis == 'Y' else (c[0] + u, c[1] + v, c[2]))
    for p, q in zip(pts, pts[1:]): tube(b, p, q, tr, m, seg)

def prism(b, pts, a0, a1, m, axis='Y'):   # convex polygon extruded along `axis` from a0 to a1 (pts = the other two coordinates)
    bm = b.bm
    mk = {'X': lambda p, a: (a, p[0], p[1]), 'Y': lambda p, a: (p[0], a, p[1]), 'Z': lambda p, a: (p[0], p[1], a)}[axis]
    f = [bm.verts.new(mk(p, a0)) for p in pts]
    k = [bm.verts.new(mk(p, a1)) for p in pts]
    n = len(pts)
    bm.faces.new(f)
    bm.faces.new(k[::-1])
    for i in range(n): bm.faces.new([f[i], f[(i + 1) % n], k[(i + 1) % n], k[i]])
    b._finish(f + k, m)

def tri_panel(b, a, p, q, t, m):   # thin triangular plate (parasol panels), thickness t below
    bm = b.bm
    top = [bm.verts.new(v) for v in (a, p, q)]
    bot = [bm.verts.new((v[0], v[1], v[2] - t)) for v in (a, p, q)]
    bm.faces.new(top)
    bm.faces.new(bot[::-1])
    for i in range(3): bm.faces.new([top[i], top[(i + 1) % 3], bot[(i + 1) % 3], bot[i]])
    b._finish(top + bot, m)

def dome(b, r, at, m, scale=(1, 1, 1), seg=12, rings=4):   # upper half of a sphere with a flat bottom
    bm = b.bm
    rows, verts = [], []
    for j in range(rings):
        ph = (math.pi / 2) * j / rings
        row = [bm.verts.new((r * math.cos(ph) * math.cos(2 * math.pi * i / seg) * scale[0], r * math.cos(ph) * math.sin(2 * math.pi * i / seg) * scale[1],
                             r * math.sin(ph) * scale[2])) for i in range(seg)]
        rows.append(row)
        verts += row
    top = bm.verts.new((0, 0, r * scale[2]))
    verts.append(top)
    for j in range(rings - 1):
        for i in range(seg): bm.faces.new([rows[j][i], rows[j][(i + 1) % seg], rows[j + 1][(i + 1) % seg], rows[j + 1][i]])
    for i in range(seg): bm.faces.new([rows[-1][i], rows[-1][(i + 1) % seg], top])
    bm.faces.new(rows[0][::-1])
    b._finish(verts, m, None, at)

def tilt(center, deg, local, axis='X'):   # a point given in the frame of a piece rotated by `deg` about `axis` round `center`
    return tuple(Vector(center) + Matrix.Rotation(math.radians(deg), 3, axis) @ Vector(local))

def arcp(cx, cz, r, a0, a1, n, rz=None):   # points of an arc (degrees) in a 2D plane
    rz = r if rz is None else rz
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cz + rz * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

def door_frame(b, w, h, jamb, depth, m):   # jambs and head round a w x h opening centred at x = 0 on the wall, dark recess behind
    for s in (-1, 1): b.box((jamb, depth, h + jamb), (s * (w / 2 + jamb / 2), -depth / 2, (h + jamb) / 2), m)
    b.box((w, depth, jamb), (0, -depth / 2, h + jamb / 2), m)
    b.box((w, 0.015, h), (0, -0.0075, h / 2), "HJ_Oscuro")

def chair(b, cx, cy, ang, seat, frame, h=0.45):   # chair whose back points towards `ang` (degrees, from +X)
    a = math.radians(ang - 90)
    ca, sa = math.cos(a), math.sin(a)
    P = lambda lx, ly, z: (cx + lx * ca - ly * sa, cy + lx * sa + ly * ca, z)
    rot = [('Z', ang - 90)]
    b.box((0.4, 0.4, 0.06), P(0, 0, h), seat, rot=rot)
    for lx in (-0.17, 0.17):
        for ly in (-0.17, 0.17): b.box((0.03, 0.03, h - 0.03), P(lx, ly, (h - 0.03) / 2), frame, rot=rot)
        b.box((0.03, 0.03, 0.45), P(lx, 0.18, h + 0.255), frame, rot=rot)
    b.box((0.38, 0.03, 0.12), P(0, 0.18, h + 0.4), frame, rot=rot)

# ================================================================== facade of the tower (2 m strips, one storey each)
def facade_band(b):   # the edge of the slab at the top of every storey (z 3.6 - 4.0)
    b.box((2.0, 0.08, 0.4), (0, -0.04, ROOM + 0.2), "HJ_Hormigon")

def fachada_balcon(name, kind):   # A: Benidorm balcony, half-drawn curtains in front of a lit room, plastic chair, towel on the rail
    b = Builder(name)             # B: the same balcony with the roller blind down (light under it), air conditioner, a plant
    facade_band(b)
    b.box((1.97, 1.0, 0.26), (0, -0.5, -0.09), "HJ_Hormigon")                      # balcony slab (the storey's floor is z = 0)
    b.box((1.97, 0.12, 0.92), (0, -0.92, 0.5), "HJ_Hormigon")                      # parapet z 0.04..0.96
    b.box((1.97, 0.03, 0.2), (0, -0.995, 0.7), "HJ_Gresite" if kind == "A" else "HJ_Marron")   # coloured band
    tube(b, (-1.0, -0.92, 1.06), (1.0, -0.92, 1.06), 0.03, "HJ_Aluminio")          # rail
    for x in (-0.6, 0.6): tube(b, (x, -0.92, 0.96), (x, -0.92, 1.06), 0.02, "HJ_Aluminio")
    b.box((1.8, 0.04, 2.5), (0, -0.02, 1.29), "HJ_Aluminio")                       # door frame z 0.04..2.54
    if kind == "A":
        b.box((0.6, 0.02, 2.3), (0, -0.05, 1.27), "HJ_Emit_Ventana")               # the lit room between the curtains
        for s in (-1, 1):
            b.box((0.56, 0.02, 2.3), (s * 0.59, -0.05, 1.27), "HJ_TelaPaint")
            for i in range(3): b.cyl(0.025, 0.025, 2.15, (s * (0.4 + i * 0.18), -0.075, 1.275), "HJ_TelaPaint", seg=6)   # folds
        for x in (-0.3, 0.3): b.box((0.05, 0.04, 2.3), (x, -0.08, 1.27), "HJ_Aluminio")   # sliding door mullions
        for z in (0.15, 2.4): b.box((1.8, 0.04, 0.06), (0, -0.08, z), "HJ_Aluminio")
        cx, cy = 0.55, -0.5                                                         # white plastic chair facing the sea
        b.box((0.42, 0.42, 0.04), (cx, cy, 0.45), "HJ_Blanco")
        for dx in (-0.18, 0.18):
            for dy in (-0.18, 0.18): tube(b, (cx + dx, cy + dy, 0.04), (cx + dx, cy + dy, 0.43), 0.018, "HJ_Blanco")
        b.box((0.42, 0.04, 0.4), (cx, cy + 0.2, 0.69), "HJ_Blanco", rot=[('X', -8)])
        b.box((0.5, 0.12, 0.02), (-0.5, -0.92, 1.1), "HJ_ToallaPaint")             # beach towel over the rail
        b.box((0.5, 0.02, 0.6), (-0.5, -1.03, 0.8), "HJ_ToallaPaint")
        b.box((0.5, 0.02, 0.35), (-0.5, -0.83, 0.93), "HJ_ToallaPaint")
    else:
        b.box((1.7, 0.02, 0.18), (0, -0.05, 0.21), "HJ_Emit_Ventana")              # light under the blind
        b.box((1.7, 0.02, 2.12), (0, -0.05, 1.36), "HJ_Marron")                    # roller blind z 0.3..2.42
        for i in range(12): b.box((1.7, 0.015, 0.03), (0, -0.0675, 0.42 + i * 0.17), "HJ_Marron")
        b.box((1.86, 0.22, 0.26), (0, -0.11, 2.67), "HJ_Hormigon")                 # roller box
        b.box((0.8, 0.3, 0.5), (0.45, -0.17, 3.1), "HJ_Uralita")                   # air conditioner, dripping
        b.cyl(0.18, 0.18, 0.02, (0.45, -0.33, 3.1), "HJ_Oscuro", seg=10, rot=[('X', 90)])
        ring(b, (0.45, -0.345, 3.1), 0.18, 0.012, "HJ_Cromo", axis='Y', n=10)
        tube(b, (0.75, -0.2, 2.85), (0.75, -0.6, 0.06), 0.012, "HJ_Negro")
        b.cyl(0.16, 0.12, 0.3, (-0.6, -0.55, 0.19), "HJ_Ladrillo", seg=8)          # potted dwarf palm
        for a in range(5): b.box((0.5, 0.12, 0.02), tilt((-0.6, -0.55, 0.45), a * 72, (0.22, 0, 0.08), 'Z'), "M_Palm", rot=[('Y', -25), ('Z', a * 72)])
    return b.build()

def fachada_muro():   # wall between balconies: 70s supergraphic stripes, frosted bathroom window (lit), vent, drainpipe
    b = Builder("HJ_Fachada_C")
    facade_band(b)
    for x, c in ((-0.75, "HJ_Naranja"), (-0.4, "HJ_Mostaza"), (-0.05, "HJ_Marron")):
        b.box((0.3, 0.03, ROOM), (x, -0.015, ROOM / 2), c)
    b.box((0.64, 0.04, 0.74), (0.5, -0.02, 2.05), "HJ_Aluminio")
    b.box((0.54, 0.02, 0.64), (0.5, -0.05, 2.05), "HJ_Emit_Ventana")
    b.box((0.04, 0.03, 0.64), (0.5, -0.075, 2.05), "HJ_Aluminio")
    b.box((0.76, 0.15, 0.05), (0.5, -0.075, 1.655), "HJ_Hormigon")                  # sill
    b.box((0.3, 0.02, 0.16), (0.5, -0.01, 3.1), "HJ_Oscuro")                        # vent
    for i in range(4): b.box((0.3, 0.02, 0.015), (0.5, -0.03, 3.04 + i * 0.04), "HJ_Aluminio")
    b.cyl(0.05, 0.05, ST, (0.92, -0.15, ST / 2), "HJ_Metal", seg=8)                 # drainpipe
    for z in (0.8, 2.6): b.box((0.14, 0.14, 0.05), (0.92, -0.08, z), "HJ_Metal")
    return b.build()

def fachada_baja():   # ground floor: plinth, tall aluminium glazing (the lit lobby), tiled fascia
    b = Builder("HJ_Fachada_Baja")
    facade_band(b)
    b.box((2.0, 0.1, 0.35), (0, -0.05, 0.175), "HJ_Terrazo")
    b.box((1.9, 0.04, 2.75), (0, -0.02, 1.725), "HJ_Aluminio")                      # z 0.35..3.1
    b.box((1.76, 0.02, 2.6), (0, -0.05, 1.72), "HJ_Emit_Ventana")
    b.box((0.06, 0.05, 2.6), (0, -0.085, 1.72), "HJ_Aluminio")
    b.box((1.76, 0.05, 0.06), (0, -0.085, 2.4), "HJ_Aluminio")
    b.box((2.0, 0.1, 0.5), (0, -0.05, 3.35), "HJ_Gresite")                          # fascia z 3.1..3.6
    for i in range(6): b.box((0.02, 0.1, 0.46), (-0.835 + i * 0.334, -0.07, 3.35), "HJ_Marron")
    return b.build()

# ================================================================== the roof sign (loose neon letters + frame)
LETTERS = {   # width, strokes in a w x 1.6 box (x right, z up)
    "H": (1.0, [[(0, 0), (0, 1.6)], [(1, 0), (1, 1.6)], [(0, 0.8), (1, 0.8)]]),
    "O": (1.0, [arcp(0.5, 0.8, 0.5, -90, 270, 16, 0.8)]),
    "T": (1.0, [[(0, 1.6), (1, 1.6)], [(0.5, 1.6), (0.5, 0)]]),
    "E": (0.9, [[(0.9, 1.6), (0, 1.6), (0, 0), (0.9, 0)], [(0, 0.8), (0.7, 0.8)]]),
    "L": (0.85, [[(0, 1.6), (0, 0), (0.85, 0)]]),
    "J": (0.9, [[(0.3, 1.6), (0.9, 1.6)], [(0.75, 1.6), (0.75, 0.4)] + arcp(0.4, 0.4, 0.35, 0, -180, 6)[1:]]),
    "U": (1.0, [[(0, 1.6), (0, 0.5)] + arcp(0.5, 0.5, 0.5, 180, 360, 8)[1:] + [(1, 1.6)]]),
    "B": (0.95, [[(0, 0), (0, 1.6), (0.55, 1.6)] + arcp(0.55, 1.2, 0.4, 90, -90, 6)[1:] + [(0, 0.8)],
                 arcp(0.55, 0.4, 0.4, 90, -90, 6) + [(0, 0)]]),
    "I": (0.5, [[(0.25, 0), (0.25, 1.6)], [(0, 1.6), (0.5, 1.6)], [(0, 0), (0.5, 0)]]),
    "S": (0.9, [arcp(0.45, 1.2, 0.4, 15, 270, 8) + arcp(0.45, 0.4, 0.4, 90, -165, 8)[1:]]),
}

def neon_letter(ch):   # one channel letter, 1.6 m tall, standing 0.3 m above the roof on two legs; tube = HJ_Emit_NeonRosa
    w, strokes = LETTERS[ch]
    b = Builder("HJ_Neon_" + ch)
    ox, oz = -w / 2, 0.3
    for st in strokes:
        pts = [(ox + x, oz + z) for x, z in st]
        for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
            ln = math.hypot(x1 - x0, z1 - z0)
            b.box((ln, 0.12, 0.2), ((x0 + x1) / 2, 0, (z0 + z1) / 2), "HJ_Metal", rot=[('Y', -math.degrees(math.atan2(z1 - z0, x1 - x0)))])
            tube(b, (x0, -0.1, z0), (x1, -0.1, z1), 0.035, "HJ_Emit_NeonRosa")
        for x, z in pts:
            b.cyl(0.1, 0.1, 0.12, (x, 0, z), "HJ_Metal", seg=8, rot=[('X', 90)])
            b.ball(0.035, (x, -0.1, z), "HJ_Emit_NeonRosa", seg=6)
    low = [x for st in strokes for x, z in st if z <= min(z for st2 in strokes for _, z in st2) + 0.15]
    xs = (min(low), max(low)) if max(low) - min(low) > 0.25 else ((min(low) + max(low)) / 2,)
    for x in xs: b.box((0.08, 0.08, 0.5), (ox + x, 0.08, 0.25), "HJ_Metal")       # legs under the lowest strokes
    return b.build()

def neon_tramo():   # 2 m of the steel truss behind the letters, with the cyan tubes above and below them
    b = Builder("HJ_Neon_Tramo")
    y = 0.35
    for z in (0.6, 2.2): b.box((2.0, 0.08, 0.08), (0, y, z), "M_Iron")
    for x in (-0.96, 0.96): b.box((0.08, 0.08, 2.3), (x, y, 1.15), "M_Iron")
    for x0, x1 in ((-0.92, 0.0), (0.0, 0.92)): tube(b, (x0, y, 0.64), (x1, y, 2.16), 0.025, "M_Iron")
    for x in (-0.96, 0.96): b.box((0.3, 0.3, 0.03), (x, y, 0.015), "M_Iron")
    for x in (-0.6, 0.6): b.box((0.05, 0.45, 0.1), (x, 0.13, 2.3), "M_Iron")        # brackets of the top tube
    tube(b, (-1.0, -0.1, 2.3), (1.0, -0.1, 2.3), 0.035, "HJ_Emit_NeonCian")
    for x in (-0.6, 0.6): b.box((0.04, 0.04, 0.12), (x, -0.12, 0.06), "M_Iron")
    tube(b, (-1.0, -0.12, 0.13), (1.0, -0.12, 0.13), 0.03, "HJ_Emit_NeonCian")
    return b.build()

def neon_coctel():   # the end piece of the sign: a cyan cocktail glass with a pink straw on a dark board, 1.5 x 0.3 x 2.3
    b = Builder("HJ_Neon_Coctel")
    b.box((1.4, 0.06, 1.8), (0, 0, 1.2), "HJ_Metal")                                # board z 0.3..2.1
    for x in (-0.4, 0.4): b.box((0.08, 0.08, 0.32), (x, 0.06, 0.16), "HJ_Metal")
    g = [(-0.55, 1.95), (0.55, 1.95), (0.0, 1.05), (0.0, 0.55)]
    path(b, [(x, -0.08, z) for x, z in (g[0], g[1], g[2], g[0])], 0.035, "HJ_Emit_NeonCian")
    path(b, [(x, -0.08, z) for x, z in (g[2], g[3])], 0.035, "HJ_Emit_NeonCian")
    path(b, [(-0.3, -0.08, 0.55), (0.3, -0.08, 0.55)], 0.035, "HJ_Emit_NeonCian")
    path(b, [(0.15, -0.12, 1.4), (0.45, -0.12, 2.25)], 0.03, "HJ_Emit_NeonRosa")   # straw
    ring(b, (-0.15, -0.12, 1.65), 0.1, 0.025, "HJ_Emit_NeonRosa", axis='Y', n=10)   # olive
    return b.build()

# ================================================================== lobby
def puerta_giratoria():   # revolving door drum (one cell): curved glass sides, wooden canopy with brass; wings = HJ_PuertaGiratoria_Aspas
    b = Builder("HJ_PuertaGiratoria")
    R = 0.95
    b.cyl(R + 0.03, R + 0.03, 0.03, (0, 0, 0.015), "HJ_Laton", seg=16)
    b.cyl(R - 0.06, R - 0.06, 0.015, (0, 0, 0.0375), "HJ_Granate", seg=16)
    for c in (0, 180):                                                              # curved glass on both sides (in / out on -Y / +Y)
        for i in range(6):
            a0, a1 = math.radians(c - 55 + i * 110 / 6), math.radians(c - 55 + (i + 1) * 110 / 6)
            am = (a0 + a1) / 2
            ln = 2 * R * math.sin((a1 - a0) / 2)
            b.box((0.03, ln, 2.3), (R * math.cos((a1 - a0) / 2) * math.cos(am), R * math.cos((a1 - a0) / 2) * math.sin(am), 1.2), "HJ_Cristal", rot=[('Z', math.degrees(am))])
        for a in (c - 55, c + 55): tube(b, (R * math.cos(math.radians(a)), R * math.sin(math.radians(a)), 0.03), (R * math.cos(math.radians(a)), R * math.sin(math.radians(a)), 2.33), 0.03, "HJ_Aluminio")
    b.cyl(R + 0.12, R + 0.12, 0.06, (0, 0, 2.38), "HJ_Laton", seg=16)
    b.cyl(R + 0.1, R + 0.1, 0.4, (0, 0, 2.61), "HJ_MaderaOscura", seg=16)
    b.cyl(R + 0.12, R + 0.12, 0.06, (0, 0, 2.84), "HJ_Laton", seg=16)
    b.box((1.0, 0.03, 0.22), (0, -(R + 0.13), 2.61), "HJ_Oro")                       # name plate
    return b.build()

def aspas():   # the four glass wings of the revolving door round their brass post (origin = the axis on the floor)
    b = Builder("HJ_PuertaGiratoria_Aspas")
    b.cyl(0.06, 0.06, 2.3, (0, 0, 1.2), "HJ_Laton", seg=8)
    for a in (0, 90, 180, 270):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        b.box((0.76, 0.025, 2.1), (0.45 * ca, 0.45 * sa, 1.2), "HJ_Cristal", rot=[('Z', a)])
        for z in (0.12, 2.28): b.box((0.82, 0.05, 0.06), (0.47 * ca, 0.47 * sa, z), "HJ_Aluminio", rot=[('Z', a)])
        b.box((0.05, 0.05, 2.2), (0.86 * ca, 0.86 * sa, 1.2), "HJ_Aluminio", rot=[('Z', a)])
        b.box((0.55, 0.03, 0.04), (0.5 * ca - 0.045 * sa, 0.5 * sa + 0.045 * ca, 1.0), "HJ_Laton", rot=[('Z', a)])
    return b.build()

def mostrador():   # reception counter, 3.9 x 1.0 x 1.3: teak with vertical ribs, marble top, bell, guest book, RECEPCION plate
    b = Builder("HJ_Mostrador")
    b.box((3.9, 0.7, 1.05), (0, 0, 0.525), "HJ_Madera")
    for i in range(15): b.box((0.12, 0.05, 0.9), (-1.82 + i * 0.26, -0.375, 0.58), "HJ_MaderaOscura")
    b.box((3.9, 0.04, 0.1), (0, -0.37, 0.05), "HJ_Laton")                           # kick plate (front -0.39)
    b.box((4.0, 1.0, 0.06), (0, 0, 1.08), "HJ_Marmol")
    b.ball(0.07, (1.2, -0.15, 1.11), "HJ_Laton", scale=(1, 1, 0.7), seg=10)          # service bell
    b.cyl(0.012, 0.012, 0.04, (1.2, -0.15, 1.18), "HJ_Laton", seg=6)
    b.box((0.42, 0.3, 0.05), (-0.6, -0.1, 1.135), "HJ_Crema", rot=[('Z', 8)])        # guest book
    b.box((0.02, 0.33, 0.08), (-0.6, -0.1, 1.135), "HJ_Granate", rot=[('Z', 8)])
    tube(b, (-0.3, -0.2, 1.17), (-0.15, -0.05, 1.17), 0.008, "HJ_Negro")
    b.box((0.7, 0.05, 0.2), (0, -0.3, 1.26), "HJ_Laton")                             # RECEPCION plate (text = label)
    b.box((0.3, 0.12, 0.05), (0, -0.3, 1.135), "HJ_Laton")
    b.box((0.3, 0.25, 0.25), (-1.5, 0.2, 1.235), "HJ_Crema")                         # a box of room keys on the back
    return b.build()

def casillero():   # key rack behind reception (wall, 1.95 m): pigeonholes with brass fobs, three clocks above
    b = Builder("HJ_Casillero")
    b.box((1.9, 0.02, 1.4), (0, -0.03, 1.7), "HJ_Oscuro")                            # back y -0.04..-0.02, z 1.0..2.4
    for s in (-1, 1): b.box((0.06, 0.27, 1.46), (s * 0.98, -0.135, 1.7), "HJ_Madera")
    for z in (0.97, 2.43): b.box((2.02, 0.27, 0.06), (0, -0.135, z), "HJ_Madera")
    for i in range(1, 6): b.box((0.02, 0.23, 1.4), (-0.95 + i * 1.9 / 6, -0.155, 1.7), "HJ_Madera")
    for j in range(1, 5): b.box((1.9, 0.23, 0.02), (0, -0.155, 1.0 + j * 0.28), "HJ_Madera")
    for i in range(6):
        for j in range(5):
            if (i * 7 + j * 3) % 5 == 0: continue                                    # some keys are out: guests in
            x, z = -0.95 + (i + 0.5) * 1.9 / 6, 1.0 + (j + 0.5) * 0.28
            b.box((0.05, 0.015, 0.12), (x, -0.12, z - 0.02), "HJ_Laton", rot=[('Y', 45)])
            b.box((0.015, 0.012, 0.06), (x, -0.13, z + 0.06), "HJ_Laton")
    for i, x in enumerate((-0.6, 0.0, 0.6)):                                         # MADRID / BENIDORM / IMSERSO (labels)
        b.cyl(0.16, 0.16, 0.04, (x, -0.02, 2.75), "HJ_Laton", seg=12, rot=[('X', 90)])
        b.cyl(0.13, 0.13, 0.03, (x, -0.04, 2.75), "HJ_Blanco", seg=12, rot=[('X', 90)])
        b.box((0.012, 0.012, 0.1), (x, -0.08, 2.79), "HJ_Negro")
        b.box((0.08, 0.012, 0.012), (x + 0.03, -0.08, 2.75), "HJ_Negro")
    return b.build()

def columna():   # lobby column clad in mirror tiles with brass rings, 3.65 m (its top is inside the slab)
    b = Builder("HJ_Columna")
    b.box((0.7, 0.7, 0.15), (0, 0, 0.075), "HJ_Terrazo")
    b.cyl(0.26, 0.26, 3.2, (0, 0, 1.75), "HJ_Espejo", seg=12)
    for z in (0.3, 1.1, 2.7): b.cyl(0.28, 0.28, 0.06, (0, 0, z), "HJ_Laton", seg=12)
    b.cyl(0.26, 0.46, 0.3, (0, 0, 3.5), "HJ_Oro", seg=12)
    return b.build()

def sofa():   # 70s velvet sofa (HJ_TelaPaint), 1.9 x 0.85 x 0.85, low cover
    b = Builder("HJ_Sofa")
    for x in (-0.85, 0.85):
        for y in (-0.32, 0.32): b.cyl(0.035, 0.025, 0.12, (x, y, 0.06), "HJ_Laton", seg=6)
    b.box((1.9, 0.8, 0.28), (0, 0.0, 0.26), "HJ_Marron")                              # base z 0.12..0.4
    for i in range(3):
        b.box((0.52, 0.6, 0.15), (-0.55 + i * 0.55, -0.08, 0.475), "HJ_TelaPaint")
        b.box((0.52, 0.2, 0.44), (-0.55 + i * 0.55, 0.28, 0.62), "HJ_TelaPaint", rot=[('X', -10)])
    for s in (-1, 1): b.box((0.14, 0.8, 0.3), (s * 0.88, 0, 0.55), "HJ_TelaPaint")
    return b.build()

def arana():   # The Shining chandelier: brass crown, three tiers of crystal drops, 12 candle bulbs; hangs from the hook (origin)
    b = Builder("HJ_Arana")
    tube(b, (0, 0, 0), (0, 0, -0.55), 0.02, "HJ_Laton")
    b.cyl(0.12, 0.05, 0.1, (0, 0, -0.05), "HJ_Laton", seg=10)
    b.cyl(0.1, 0.25, 0.35, (0, 0, -0.75), "HJ_Laton", seg=10)
    for r, z, n in ((0.85, -1.0, 16), (0.6, -1.35, 12), (0.35, -1.65, 8)):
        ring(b, (0, 0, z), r, 0.02, "HJ_Laton", n=n)
        for i in range(n):
            a = 2 * math.pi * (i + 0.5) / n
            b.ball(0.045, (r * math.cos(a), r * math.sin(a), z - 0.12), "HJ_Cristalito", scale=(1, 1, 1.8), seg=4)
        for i in range(4): tube(b, (0, 0, -0.85), (r * math.cos(i * math.pi / 2 + 0.4), r * math.sin(i * math.pi / 2 + 0.4), z), 0.015, "HJ_Laton")
    for i in range(12):
        a = 2 * math.pi * i / 12
        x, y = 0.85 * math.cos(a), 0.85 * math.sin(a)
        b.cyl(0.035, 0.035, 0.05, (x, y, -0.97), "HJ_Laton", seg=6)
        b.cyl(0.018, 0.018, 0.12, (x, y, -0.89), "HJ_Blanco", seg=6)
        b.ball(0.035, (x, y, -0.8), "HJ_Emit_Arana", scale=(1, 1, 1.5), seg=6)
    b.ball(0.18, (0, 0, -1.1), "HJ_Emit_Arana", seg=10)                             # glowing heart
    b.cyl(0.05, 0.0, 0.3, (0, 0, -1.85), "HJ_Cristalito", seg=6)
    return b.build()

def telefono():   # the reception phone (mission: call the helicopter): orange rotary phone on a pedestal table, 0.6 x 0.6 x 1.0
    b = Builder("HJ_Telefono")
    b.cyl(0.25, 0.28, 0.04, (0, 0, 0.02), "HJ_Laton", seg=10)
    b.cyl(0.03, 0.03, 0.72, (0, 0, 0.4), "HJ_Laton", seg=8)
    b.cyl(0.3, 0.3, 0.04, (0, 0, 0.78), "HJ_MaderaOscura", seg=12)
    b.box((0.24, 0.22, 0.1), (0, 0.02, 0.85), "HJ_Naranja", rot=[('X', -12)])
    b.cyl(0.075, 0.075, 0.02, (0, -0.05, 0.9), "HJ_Blanco", seg=12, rot=[('X', -12)])   # dial
    for i in range(10):
        a = math.radians(30 + i * 30)
        b.ball(0.012, tilt((0, -0.05, 0.9), -12, (0.05 * math.cos(a), 0.05 * math.sin(a), 0.012)), "HJ_Oscuro", seg=4)
    b.box((0.3, 0.06, 0.05), (0, 0.06, 0.95), "HJ_Naranja")                         # handset
    for s in (-1, 1): b.box((0.07, 0.08, 0.06), (s * 0.13, 0.06, 0.94), "HJ_Naranja")
    path(b, [(0.12, 0.13, 0.85), (0.18, 0.18, 0.82), (0.14, 0.24, 0.81), (0.2, 0.28, 0.81)], 0.008, "HJ_Negro")
    b.box((0.2, 0.02, 0.12), (-0.14, -0.2, 0.86), "HJ_Crema", rot=[('X', -20)])     # "SOLO HELICOPTEROS" card (label)
    return b.build()

# ================================================================== lifts
def puerta_ascensor():   # lift doors on the landing wall (2 m strip): marble, brass frame, dial floor indicator, call button;
    b = Builder("HJ_PuertaAscensor")   # the opening is 1.2 x 2.4; the leaves are HJ_HojaAscensor (x = +-0.3, y = -0.06, closed)
    b.box((1.96, 0.015, 3.2), (0, -0.0075, 1.6), "HJ_Marmol")
    b.box((1.2, 0.015, 2.4), (0, -0.0275, 1.2), "HJ_Oscuro")
    for s in (-1, 1): b.box((0.2, 0.1, 2.65), (s * 0.7, -0.065, 1.325), "HJ_Laton")
    b.box((1.2, 0.1, 0.25), (0, -0.065, 2.525), "HJ_Laton")
    b.half_disc(0.42, 0.03, (0, -0.03, 2.75), "HJ_Laton", seg=10)                    # floor indicator (S 0 1 2)
    b.half_disc(0.36, 0.02, (0, -0.055, 2.78), "HJ_Emit_Ascensor", seg=10)
    b.box((0.025, 0.012, 0.3), (0.08, -0.085, 2.9), "HJ_Negro", rot=[('Y', 20)])
    for i in range(4):
        a = math.radians(160 - i * 46.7)
        b.ball(0.025, (0.29 * math.cos(a), -0.07, 2.78 + 0.29 * math.sin(a)), "HJ_Negro", seg=6)
    b.box((0.13, 0.03, 0.3), (0.89, -0.03, 1.2), "HJ_Laton")                        # call button
    b.cyl(0.035, 0.035, 0.02, (0.89, -0.055, 1.22), "HJ_Emit_Ascensor", seg=8, rot=[('X', 90)])
    return b.build()

def hoja_ascensor():   # one lift door leaf, 0.6 x 2.4, brass with a 70s sunburst; origin = bottom centre (the meeting edge is x = +-0.3)
    b = Builder("HJ_HojaAscensor")
    b.box((0.6, 0.04, 2.39), (0, 0, 1.205), "HJ_Laton")
    for i in range(5):
        b.box((0.5, 0.015, 0.02), (0, -0.0275, 0.5 + i * 0.35), "HJ_Oro")
        b.box((0.5, 0.015, 0.02), (0, 0.0275, 0.5 + i * 0.35), "HJ_Oro")
    for a in (-50, -25, 0, 25, 50): b.box((0.015, 0.016, 0.35), tilt((0, -0.028, 1.9), a, (0, 0, 0.17), 'Y'), "HJ_Oro", rot=[('Y', a)])
    return b.build()

def cabina():   # lift cab, one 2 x 2 cell open towards -Y (its landing): wood panels, mirror, brass rails, light panel; 2.58 m
    b = Builder("HJ_Cabina")
    b.box((1.8, 1.875, 0.03), (0, -0.0425, 0.015), "HJ_Granate")                     # carpet
    b.box((1.76, 0.06, 0.05), (0, -0.965, 0.025), "HJ_Laton")                         # threshold
    b.box((1.96, 0.08, 2.5), (0, 0.94, 1.25), "HJ_Madera")
    for s in (-1, 1):
        b.box((0.08, 1.96, 2.5), (s * 0.94, 0, 1.25), "HJ_Madera")
        b.box((0.02, 0.15, 2.5), (s * 0.88, -0.9, 1.25), "HJ_Laton")
        tube(b, (s * 0.83, -0.6, 0.95), (s * 0.83, 0.7, 0.95), 0.02, "HJ_Laton")
        for y in (-0.5, 0.6): tube(b, (s * 0.83, y, 0.95), (s * 0.9, y, 0.95), 0.012, "HJ_Laton")
    b.box((1.96, 1.96, 0.08), (0, 0, 2.54), "HJ_Madera")
    b.box((1.2, 1.2, 0.02), (0, 0, 2.48), "HJ_Emit_Cabina")
    b.box((1.8, 0.04, 0.12), (0, -0.96, 2.44), "HJ_Laton")
    b.box((1.4, 0.02, 1.2), (0, 0.88, 1.6), "HJ_Espejo")
    tube(b, (-0.8, 0.83, 0.95), (0.8, 0.83, 0.95), 0.02, "HJ_Laton")
    return b.build()

def botonera():   # the cab's SUBIR / BAJAR panel (wall, origin at floor level), with the floor lights S 0 1 2; centre at 1.25 m
    b = Builder("HJ_Botonera")
    b.box((0.26, 0.03, 0.56), (0, -0.015, 1.25), "HJ_Laton")
    b.box((0.2, 0.02, 0.1), (0, -0.04, 1.44), "HJ_Negro")
    for i in range(4): b.cyl(0.018, 0.018, 0.02, (-0.06 + i * 0.04, -0.06, 1.44), "HJ_Emit_Ascensor", seg=8, rot=[('X', 90)])
    for z, up in ((1.29, True), (1.11, False)):
        b.cyl(0.065, 0.065, 0.02, (0, -0.04, z), "HJ_Cromo", seg=12, rot=[('X', 90)])
        pts = [(-0.035, z - 0.025), (0.035, z - 0.025), (0, z + 0.03)] if up else [(-0.035, z + 0.025), (0, z - 0.03), (0.035, z + 0.025)]
        prism(b, pts, -0.05, -0.065, "HJ_Emit_Ascensor")
    return b.build()

# ================================================================== stairs, voids, balcony
def barandilla_escalera():   # one cell of stair railing: 2 m along +X climbing RISE (a 3-cell flight); origin = ramp surface at the cell centre
    b = Builder("HJ_BarandillaEscalera")
    zs = lambda x: x * RISE / 2.0
    ang = -math.degrees(math.atan2(RISE, 2.0))
    L = math.hypot(2.0, RISE)
    for x in (-0.75, 0.25):
        b.box((0.05, 0.05, 1.0), (x, 0, zs(x) + 0.5), "HJ_Metal")
    for i in range(16):
        x = -0.94 + i * 0.125
        if abs(x + 0.75) < 0.04 or abs(x - 0.25) < 0.04: continue
        b.box((0.016, 0.016, 0.8), (x, 0, zs(x) + 0.55), "HJ_Metal")
    b.box((L, 0.03, 0.03), (0, 0, 0.15), "HJ_Metal", rot=[('Y', ang)])
    b.box((L, 0.03, 0.03), (0, 0, 0.5), "HJ_Metal", rot=[('Y', ang)])
    b.box((L, 0.08, 0.06), (0, 0, 1.0), "HJ_MaderaOscura", rot=[('Y', ang)])
    for x in (-0.25, 0.75): ring(b, (x, 0, zs(x) + 0.32), 0.1, 0.01, "HJ_Metal", axis='Y', n=10)
    return b.build()

def pretil():   # masonry parapet of the stairwells and voids, 2 m along X, 1.1 m: plinth, breeze blocks (celosia), terrazzo cap
    b = Builder("HJ_Pretil")
    b.box((1.96, 0.2, 0.35), (0, 0, 0.175), "HJ_Hormigon")
    b.box((1.96, 0.16, 0.65), (0, 0, 0.675), "HJ_Hormigon")
    for x in (-0.49, 0.0, 0.49): b.box((0.02, 0.2, 0.61), (x, 0, 0.675), "HJ_Terrazo")
    for x in (-0.735, -0.245, 0.245, 0.735):
        b.cyl(0.17, 0.17, 0.2, (x, 0, 0.675), "HJ_Oscuro", seg=10, rot=[('X', 90)])
        for y in (-0.1, 0.1): ring(b, (x, y, 0.675), 0.17, 0.02, "HJ_Hormigon", axis='Y', n=10)
    b.box((2.0, 0.26, 0.1), (0, 0, 1.05), "HJ_Terrazo")
    return b.build()

def baranda_balcon():   # railing of the walkable balcony (2nd floor), 2 m along X, 1.1 m: aluminium frame, amber glass, mid rail
    b = Builder("HJ_BarandaBalcon")
    for s in (-1, 1): b.box((0.06, 0.06, 1.05), (s * 0.97, 0, 0.525), "HJ_Aluminio")
    b.box((2.0, 0.08, 0.05), (0, 0, 1.075), "HJ_Aluminio")
    b.box((1.88, 0.04, 0.05), (0, 0, 0.12), "HJ_Aluminio")
    b.box((1.88, 0.015, 0.85), (0, 0, 0.58), "HJ_CristalAmbar")
    b.box((1.88, 0.05, 0.035), (0, 0, 0.6), "HJ_Aluminio")                         # mid rail across the glass
    return b.build()

# ================================================================== buffet and Salon Dorado
def mesa_bufe():   # buffet table 1.9 x 0.9: cloth to the floor, two chafing dishes, a paella, sneeze guard, NO REPETIR card; 1.35 m
    b = Builder("HJ_MesaBufe")
    b.box((1.86, 0.86, 0.78), (0, 0, 0.39), "HJ_Blanco")
    b.box((1.92, 0.92, 0.03), (0, 0, 0.795), "HJ_Crema")
    b.box((1.9, 0.02, 0.14), (0, -0.44, 0.66), "HJ_Naranja")
    for x in (-0.62, 0.62):
        b.cyl(0.25, 0.25, 0.1, (x, -0.05, 0.86), "HJ_Cromo", seg=12, scale=(1, 0.65, 1))
        dome(b, 0.24, (x, -0.05, 0.91), "HJ_Cromo", scale=(1, 0.65, 0.45), seg=12, rings=3)
        b.ball(0.025, (x, -0.05, 1.03), "HJ_Negro", seg=6)
    b.cyl(0.3, 0.3, 0.04, (0, -0.05, 0.83), "HJ_Metal", seg=14)                      # paella
    b.cyl(0.27, 0.27, 0.03, (0, -0.05, 0.855), "HJ_Comida", seg=14)
    for s in (-1, 1): b.box((0.1, 0.04, 0.02), (s * 0.34, -0.05, 0.84), "HJ_Metal")
    for i in range(6):
        a = 2 * math.pi * i / 6
        cone(b, (0.16 * math.cos(a), -0.05 + 0.16 * math.sin(a), 0.875), (0.16 * math.cos(a + 0.4), -0.05 + 0.16 * math.sin(a + 0.4), 0.88), 0.022, 0.012, "HJ_Gamba", seg=6)
    for x, y in ((0.05, 0.05), (-0.1, -0.12), (0.1, -0.15)): b.ball(0.02, (x, y, 0.875), "HJ_Verde", seg=6)
    for x in (-0.82, 0.82): tube(b, (x, 0.32, 0.81), (x, 0.32, 1.3), 0.015, "HJ_Cromo")
    b.box((1.7, 0.015, 0.42), (0, 0.18, 1.2), "HJ_Cristalito", rot=[('X', 40)])
    prism(b, [(0.75, 0.81), (0.9, 0.81), (0.825, 0.92)], -0.3, -0.24, "HJ_Crema")
    return b.build()

def cafetera():   # industrial espresso machine on its cabinet (the coffee for the pilot), 1.04 x 0.66 x 1.6
    b = Builder("HJ_Cafetera")
    b.box((1.0, 0.6, 0.9), (0, 0.02, 0.45), "HJ_Marron")
    b.box((1.04, 0.66, 0.05), (0, 0.02, 0.925), "HJ_Cromo")
    b.box((0.9, 0.02, 0.1), (0, -0.29, 0.1), "HJ_Cromo")
    for x in (-0.24, 0.24): b.box((0.44, 0.02, 0.6), (x, -0.295, 0.5), "HJ_Madera")
    b.box((0.85, 0.45, 0.4), (0, 0.025, 1.15), "HJ_Cromo")
    b.box((0.85, 0.02, 0.25), (0, -0.215, 1.175), "HJ_Rojo")
    for x in (-0.27, 0.0, 0.27):
        b.cyl(0.06, 0.06, 0.08, (x, -0.27, 1.08), "HJ_Cromo", seg=8)
        b.box((0.04, 0.2, 0.04), (x, -0.4, 1.08), "HJ_Negro")
    b.box((0.8, 0.2, 0.03), (0, -0.32, 0.965), "HJ_Metal")
    for x in (-0.27, 0.27): b.cyl(0.035, 0.03, 0.05, (x, -0.3, 1.005), "HJ_Blanco", seg=8)
    for i in range(4): b.cyl(0.035, 0.03, 0.06, (-0.3 + i * 0.2, 0.15, 1.38), "HJ_Blanco", seg=8)
    dome(b, 0.16, (0, 0.05, 1.35), "HJ_Cromo", scale=(1, 1, 0.8), seg=10, rings=3)
    b.box((0.25, 0.06, 0.05), (0, 0.05, 1.52), "HJ_Oro")                             # the eagle on top
    b.ball(0.04, (0, 0.02, 1.57), "HJ_Oro", seg=6)
    tube(b, (0.4, -0.22, 1.2), (0.45, -0.32, 1.0), 0.012, "HJ_Cromo")
    b.cyl(0.06, 0.06, 0.03, (0.25, -0.24, 1.22), "HJ_Laton", seg=10, rot=[('X', 90)])
    b.cyl(0.05, 0.05, 0.02, (0.25, -0.265, 1.22), "HJ_Blanco", seg=10, rot=[('X', 90)])
    b.box((0.01, 0.01, 0.04), (0.26, -0.285, 1.23), "HJ_Rojo", rot=[('Y', 30)])
    b.ball(0.022, (-0.35, -0.235, 1.25), "HJ_Emit_Piloto", seg=6)
    return b.build()

def encimera():   # stainless kitchen counter 1.95 x 0.7 x 0.95 with a stock pot, a stack of plates and a fish
    b = Builder("HJ_Encimera")
    b.box((1.9, 0.66, 0.88), (0, 0, 0.44), "HJ_Aluminio")
    b.box((1.95, 0.7, 0.05), (0, 0, 0.905), "HJ_Cromo")
    for x in (-0.62, 0.0, 0.62):
        b.box((0.56, 0.02, 0.7), (x, -0.34, 0.42), "HJ_Cromo")
        b.box((0.2, 0.03, 0.03), (x, -0.365, 0.72), "HJ_Negro")
    b.cyl(0.25, 0.25, 0.4, (-0.5, 0.05, 1.13), "HJ_Cromo", seg=12)
    b.cyl(0.22, 0.22, 0.02, (-0.5, 0.05, 1.34), "HJ_Comida", seg=12)
    for s in (-1, 1): b.box((0.08, 0.04, 0.04), (-0.5 + s * 0.28, 0.05, 1.28), "HJ_Negro")
    tube(b, (-0.45, 0.05, 1.2), (-0.3, 0.2, 1.6), 0.012, "HJ_Cromo")
    b.cyl(0.13, 0.13, 0.12, (0.6, 0.1, 0.99), "HJ_Blanco", seg=12)
    b.box((0.42, 0.26, 0.03), (0.08, -0.08, 0.945), "HJ_Madera")
    b.ball(0.15, (0.08, -0.08, 0.99), "HJ_Cromo", scale=(1, 0.35, 0.28), seg=8)
    prism(b, [(0.2, 0.0), (0.3, 0.06), (0.3, -0.06)], 0.96, 1.02, "HJ_Cromo", axis='Z')
    return b.build()

def tarima():   # the animador's stage 3.9 x 1.9 x 0.28 (walkable): red curtain, mic, bingo cage, the 1-90 board (HJ_Emit_Bingo); 3.4 m
    b = Builder("HJ_Tarima")
    b.box((3.9, 1.9, 0.28), (0, 0, 0.14), "HJ_MaderaOscura")
    b.box((3.94, 0.02, 0.06), (0, -0.96, 0.25), "HJ_Oro")
    for i in range(15): b.cyl(0.14, 0.14, 2.9, (-1.82 + i * 0.26, 0.8, 1.73), "HJ_Granate", seg=8)
    b.box((3.9, 0.26, 0.35), (0, 0.79, 3.2), "HJ_Oro")
    for i in range(8): b.ball(0.13, (-1.7 + i * 0.486, 0.62, 3.02), "HJ_Granate", scale=(1.6, 0.4, 0.8), seg=8)
    b.cyl(0.15, 0.15, 0.02, (0.3, -0.45, 0.29), "HJ_Cromo", seg=10)                  # microphone
    tube(b, (0.3, -0.45, 0.3), (0.3, -0.45, 1.45), 0.015, "HJ_Cromo")
    tube(b, (0.3, -0.45, 1.45), (0.3, -0.65, 1.6), 0.012, "HJ_Cromo")
    b.ball(0.04, (0.3, -0.68, 1.62), "HJ_Negro", seg=8)
    cx, cz, R = -1.1, 1.15, 0.32                                                     # bingo cage
    for s in (-1, 1):
        b.box((0.05, 0.05, 0.95), (cx + s * 0.42, -0.2, 0.75), "HJ_Laton", rot=[('Y', s * 8)])
    tube(b, (cx - 0.45, -0.2, cz), (cx + 0.45, -0.2, cz), 0.015, "HJ_Laton")
    for dx in (-0.2, 0.0, 0.2): ring(b, (cx + dx, -0.2, cz), R * math.sqrt(1 - (dx / R) ** 2) if abs(dx) < R else 0.05, 0.01, "HJ_Laton", axis='X', n=12)
    for k in range(4):
        a = k * math.pi / 2
        tube(b, (cx - 0.2, -0.2 + 0.24 * math.cos(a), cz + 0.24 * math.sin(a)), (cx + 0.2, -0.2 + 0.24 * math.cos(a), cz + 0.24 * math.sin(a)), 0.008, "HJ_Laton")
    for i in range(10): b.ball(0.04, (cx - 0.15 + (i % 5) * 0.075, -0.2 + (i // 5) * 0.08 - 0.04, cz - 0.24 + (i % 2) * 0.04), "HJ_Blanco", seg=6)
    tube(b, (cx + 0.45, -0.2, cz), (cx + 0.45, -0.38, cz - 0.15), 0.012, "HJ_Laton")
    b.ball(0.03, (cx + 0.45, -0.4, cz - 0.16), "HJ_Rojo", seg=6)
    bx = 1.35                                                                        # the number board
    b.box((0.98, 0.06, 1.0), (bx, 0.3, 1.2), "HJ_MaderaOscura")
    for s in (-1, 1): b.box((0.05, 0.05, 0.45), (bx + s * 0.4, 0.3, 0.505), "HJ_MaderaOscura")
    for r in range(9):
        for c in range(10):
            b.cyl(0.03, 0.03, 0.02, (bx - 0.3825 + c * 0.085, 0.26, 1.6 - r * 0.1), "HJ_Emit_Bingo", seg=6, rot=[('X', 90)])
    return b.build()

def barra_salon():   # one 1.95 m piece of the Salon Dorado bar (the ghost barman's): padded red front, studs, brass foot rail
    b = Builder("HJ_BarraSalon")
    b.box((1.95, 0.6, 1.05), (0, 0.05, 0.525), "HJ_MaderaOscura")
    b.box((1.95, 0.8, 0.06), (0, 0.0, 1.08), "HJ_MaderaOscura")
    b.box((1.95, 0.03, 0.04), (0, -0.415, 1.03), "HJ_Oro")
    for x in (-0.6, 0.0, 0.6):
        b.box((0.56, 0.04, 0.85), (x, -0.27, 0.55), "HJ_Granate")
        for i in range(3):
            for j in range(4): b.ball(0.014, (x - 0.18 + i * 0.18, -0.29, 0.25 + j * 0.2), "HJ_Oro", seg=4)
    tube(b, (-0.975, -0.4, 0.18), (0.975, -0.4, 0.18), 0.025, "HJ_Laton")
    for x in (-0.6, 0.6): b.box((0.04, 0.145, 0.04), (x, -0.3425, 0.18), "HJ_Laton")
    for i, x in enumerate((-0.7, -0.55, -0.4)): b.cyl(0.035, 0.03, 0.1, (x, -0.1, 1.16), "HJ_Cristalito", seg=8)
    b.cyl(0.045, 0.045, 0.2, (0.1, 0.05, 1.21), "HJ_Cromo", seg=8)
    b.cyl(0.03, 0.04, 0.06, (0.1, 0.05, 1.34), "HJ_Cromo", seg=8)
    b.cyl(0.1, 0.09, 0.18, (0.6, 0.0, 1.2), "HJ_Cromo", seg=10)                      # champagne in the ice bucket
    tube(b, (0.6, 0.0, 1.2), (0.65, 0.08, 1.45), 0.04, "HJ_Botella", seg=8)
    b.box((0.25, 0.12, 0.02), (-0.2, 0.15, 1.12), "HJ_Blanco", rot=[('Z', 20)])
    return b.build()

def barra_fondo():   # back bar (wall, 1.95 m): low cabinet, mirror, two lit glass shelves with bottles, gold frame; 2.45 m
    b = Builder("HJ_BarraFondo")
    b.box((1.95, 0.45, 0.9), (0, -0.225, 0.45), "HJ_MaderaOscura")
    for x in (-0.62, 0.0, 0.62): b.box((0.58, 0.02, 0.7), (x, -0.46, 0.45), "HJ_Madera")
    b.box((1.95, 0.5, 0.05), (0, -0.25, 0.925), "HJ_Marmol")
    b.box((1.8, 0.02, 1.25), (0, -0.01, 1.7), "HJ_Espejo")
    b.box((1.95, 0.06, 0.12), (0, -0.03, 2.39), "HJ_Oro")
    for s in (-1, 1): b.box((0.075, 0.06, 1.38), (s * 0.9375, -0.03, 1.64), "HJ_Oro")
    cols = ("HJ_Botella", "HJ_CristalAmbar", "HJ_Cristalito", "HJ_Rojo", "HJ_Botella", "HJ_Mostaza")
    for k, z in enumerate((1.35, 1.85)):
        b.box((1.8, 0.25, 0.025), (0, -0.145, z), "HJ_Cristalito")
        b.box((1.7, 0.04, 0.02), (0, -0.25, z - 0.03), "HJ_Emit_Barra")
        for i in range(9):
            h = 0.22 + ((i * 7 + k * 3) % 4) * 0.03
            x = -0.8 + i * 0.2
            b.cyl(0.04, 0.04, h, (x, -0.15, z + 0.0125 + h / 2), cols[(i + k) % 6], seg=8)
            b.cyl(0.015, 0.015, 0.1, (x, -0.15, z + 0.0125 + h + 0.05), cols[(i + k) % 6], seg=6)
    for x in (-0.5, 0.5): b.cyl(0.04, 0.035, 0.12, (x, -0.3, 1.01), "HJ_Cristalito", seg=8)
    return b.build()

def mesa_redonda():   # Salon Dorado table: cloth to the floor, red lamp, bingo cards, four gold chairs; 1.9 x 1.9 x 0.95
    b = Builder("HJ_MesaRedonda")
    b.cyl(0.66, 0.6, 0.74, (0, 0, 0.37), "HJ_Blanco", seg=14)
    b.cyl(0.64, 0.64, 0.03, (0, 0, 0.755), "HJ_Crema", seg=14)
    b.cyl(0.06, 0.07, 0.04, (0, 0, 0.79), "HJ_Laton", seg=8)
    tube(b, (0, 0, 0.8), (0, 0, 0.92), 0.01, "HJ_Laton")
    b.cyl(0.12, 0.07, 0.12, (0, 0, 0.95), "HJ_Emit_Lampara", seg=8)
    for i, a in enumerate((20, 160)):
        x, y = 0.38 * math.cos(math.radians(a)), 0.38 * math.sin(math.radians(a))
        b.box((0.2, 0.13, 0.006), (x, y, 0.773), "HJ_Crema", rot=[('Z', a + 90)])
        tube(b, (x + 0.08, y - 0.08, 0.78), (x + 0.16, y - 0.02, 0.78), 0.008, "HJ_Rojo")
    for a in (45, 135, 225, 315): chair(b, 0.62 * math.cos(math.radians(a)), 0.62 * math.sin(math.radians(a)), a, "HJ_Granate", "HJ_Oro")
    return b.build()

def bola_espejos():   # mirror ball (HJ_Emit_Bola) hanging from the ceiling hook (origin), 0.7 m, 1.43 m drop
    b = Builder("HJ_BolaEspejos")
    b.box((0.2, 0.2, 0.08), (0, 0, -0.04), "HJ_Metal")
    tube(b, (0, 0, -0.08), (0, 0, -0.75), 0.012, "HJ_Cromo")
    b.ball(0.35, (0, 0, -1.08), "HJ_Emit_Bola", seg=16)
    return b.build()

def marco_foto():   # the gilt-framed photo of the Salon (wall, origin at floor level): photo slot HJ_Foto for the sepia texture
    b = Builder("HJ_MarcoFoto")
    b.box((1.3, 0.02, 0.9), (0, -0.04, 1.9), "HJ_Foto")                              # y -0.05..-0.03
    b.box((1.3, 0.02, 0.9), (0, -0.01, 1.9), "HJ_MaderaOscura")
    for s in (-1, 1):
        b.box((1.6, 0.08, 0.15), (0, -0.04, 1.9 + s * 0.525), "HJ_Oro")
        b.box((0.15, 0.08, 0.9), (s * 0.725, -0.04, 1.9), "HJ_Oro")
        for t in (-1, 1): b.ball(0.06, (s * 0.725, -0.08, 1.9 + t * 0.525), "HJ_Oro", seg=6)
    b.box((0.4, 0.02, 0.1), (0, -0.01, 1.2), "HJ_Laton")
    b.box((0.08, 0.04, 0.08), (0, -0.02, 2.65), "HJ_Laton")                          # picture light
    tube(b, (0, -0.04, 2.65), (0, -0.25, 2.7), 0.012, "HJ_Laton")
    b.cyl(0.05, 0.05, 0.6, (0, -0.27, 2.7), "HJ_Laton", seg=8, rot=[('Y', 90)])
    b.box((0.56, 0.04, 0.02), (0, -0.27, 2.645), "HJ_Emit_Lampara")
    return b.build()

# ================================================================== basement
def caldera():   # boiler room boiler, 1.9 x 1.9 x 3.5: brick base, riveted drum, firebox glow, gauge, valve wheel, pipes
    b = Builder("HJ_Caldera")
    b.box((1.8, 1.8, 0.3), (0, 0, 0.15), "HJ_Ladrillo")
    b.cyl(0.75, 0.75, 2.0, (0, 0, 1.3), "M_Iron", seg=14)
    for z in (0.6, 1.3, 2.0): b.cyl(0.77, 0.77, 0.06, (0, 0, z), "HJ_Metal", seg=14)
    dome(b, 0.75, (0, 0, 2.3), "M_Iron", scale=(1, 1, 0.45), seg=14, rings=3)
    b.box((0.55, 0.12, 0.45), (0, -0.76, 0.85), "HJ_Metal")
    b.box((0.35, 0.02, 0.08), (0, -0.83, 0.9), "HJ_Emit_Caldera")
    b.box((0.1, 0.05, 0.03), (0.2, -0.845, 0.75), "HJ_Negro")
    b.cyl(0.12, 0.12, 0.04, (0.35, -0.69, 1.65), "HJ_Laton", seg=12, rot=[('X', 90)])
    b.cyl(0.1, 0.1, 0.02, (0.35, -0.72, 1.65), "HJ_Blanco", seg=12, rot=[('X', 90)])
    b.box((0.015, 0.01, 0.08), (0.37, -0.75, 1.67), "HJ_Rojo", rot=[('Y', -40)])
    tube(b, (0, 0, 2.55), (0, 0, 3.4), 0.12, "HJ_Metal", seg=8)
    b.ball(0.14, (0, 0, 3.4), "HJ_Metal", seg=8)
    tube(b, (0, 0, 3.4), (0, 0.98, 3.4), 0.12, "HJ_Metal", seg=8)
    tube(b, (0.7, 0, 1.6), (0.9, 0, 1.6), 0.07, "HJ_Metal", seg=8)
    tube(b, (0.9, 0, 1.6), (0.9, 0, 0.3), 0.07, "HJ_Metal", seg=8)
    b.ball(0.08, (0.9, 0, 1.6), "HJ_Metal", seg=8)
    ring(b, (0.98, 0, 1.15), 0.15, 0.02, "HJ_Rojo", axis='X', n=12)
    for a in (0, 90): b.box((0.02, 0.3, 0.02), (0.98, 0, 1.15), "HJ_Rojo", rot=[('X', a)])
    b.box((0.4, 0.02, 0.22), (-0.45, -0.915, 0.15), "HJ_Amarillo")                   # PELIGRO plate
    return b.build()

def lavadora():   # two industrial front-loading washers on a plinth, 1.9 x 1.0 x 1.85 (the laundry: chutes end here)
    b = Builder("HJ_Lavadora")
    b.box((1.9, 0.95, 0.15), (0, 0.025, 0.075), "HJ_Hormigon")
    for s in (-1, 1):
        x = s * 0.475
        b.box((0.9, 0.85, 1.35), (x, 0.05, 0.825), "HJ_Cromo")
        b.cyl(0.32, 0.32, 0.04, (x, -0.395, 0.75), "HJ_Metal", seg=14, rot=[('X', 90)])
        b.cyl(0.26, 0.26, 0.06, (x, -0.4, 0.75), "HJ_Agua", seg=14, rot=[('X', 90)])
        b.box((0.06, 0.05, 0.14), (x + 0.3, -0.44, 0.75), "HJ_Negro")
        b.box((0.86, 0.18, 0.2), (x, -0.29, 1.6), "HJ_Crema", rot=[('X', -30)])
        for dx in (-0.25, -0.1): b.cyl(0.04, 0.04, 0.03, tilt((x, -0.29, 1.6), -30, (dx, -0.1, 0.0)), "HJ_Negro", seg=8, rot=[('X', 60)])
        b.ball(0.02, tilt((x, -0.29, 1.6), -30, (0.25, -0.095, 0.0)), "HJ_Emit_Piloto", seg=6)
        tube(b, (x, 0.5, 1.2), (x, 0.5, 0.15), 0.03, "HJ_Goma")
    b.box((0.3, 0.2, 0.35), (-0.5, 0.2, 1.675), "HJ_Naranja")                        # detergent
    b.box((0.33, 0.23, 0.07), (-0.5, 0.2, 1.7), "HJ_Blanco")
    return b.build()

def carro_ropa():   # laundry cart, 1.2 x 0.7 x 1.0: metal frame, canvas bin full of towels and sheets, castors
    b = Builder("HJ_CarroRopa")
    b.box((1.1, 0.6, 0.65), (0, 0, 0.53), "M_Canvas")
    c = [(-0.57, -0.32), (0.57, -0.32), (0.57, 0.32), (-0.57, 0.32)]
    for z in (0.18, 0.86): path(b, [(x, y, z) for x, y in c + c[:1]], 0.02, "HJ_Metal")
    for x, y in c:
        tube(b, (x, y, 0.12), (x, y, 0.86), 0.02, "HJ_Metal")
        b.cyl(0.06, 0.06, 0.04, (x, y, 0.06), "HJ_Goma", seg=8, rot=[('X', 90)])
    for i, (x, y, m) in enumerate(((-0.3, -0.1, "HJ_ToallaPaint"), (0.1, 0.1, "HJ_Blanco"), (0.35, -0.12, "HJ_ToallaPaint"), (-0.05, -0.15, "HJ_Blanco"))):
        b.ball(0.22, (x, y, 0.9), m, scale=(1.2, 0.9, 0.35), seg=8)
    b.box((0.5, 0.02, 0.38), (0.15, -0.355, 0.75), "HJ_Blanco", rot=[('X', -8)])
    return b.build()

def cuadro_electrico():   # the main switchboard ("dar la luz"; also the helipad switch). Wall, origin at floor level; the lever is
    b = Builder("HJ_CuadroElectrico")   # HJ_Palanca, placed at (0.4, -0.45, 0.9) and turned about X (modelled up = ON)
    b.box((1.3, 0.35, 1.7), (0, -0.175, 1.35), "HJ_Uralita")
    b.box((0.02, 0.02, 1.3), (-0.15, -0.36, 1.3), "HJ_Metal")
    for i in range(3):
        x = -0.48 + i * 0.2
        b.cyl(0.08, 0.08, 0.03, (x, -0.365, 1.85), "HJ_Negro", seg=12, rot=[('X', 90)])
        b.cyl(0.06, 0.06, 0.02, (x, -0.385, 1.85), "HJ_Blanco", seg=12, rot=[('X', 90)])
        b.box((0.01, 0.01, 0.06), (x + 0.01, -0.41, 1.86), "HJ_Rojo", rot=[('Y', 30 - i * 30)])
    for x in (-0.48, -0.32): b.ball(0.03, (x, -0.36, 1.6), "HJ_Emit_Piloto", seg=6)
    prism(b, [(-0.48, 0.95), (-0.16, 0.95), (-0.32, 1.23)], -0.36, -0.38, "HJ_Amarillo")
    prism(b, [(-0.33, 1.15), (-0.29, 1.15), (-0.33, 1.06), (-0.3, 1.06)], -0.38, -0.395, "HJ_Negro")
    b.box((0.35, 0.04, 0.6), (0.4, -0.37, 1.1), "HJ_Negro")                          # knife-switch plate
    for x in (0.33, 0.47):
        b.box((0.05, 0.06, 0.08), (x, -0.42, 1.3), "HJ_Laton")
        b.box((0.04, 0.06, 0.06), (x, -0.42, 0.9), "HJ_Laton")
    b.box((0.5, 0.02, 0.12), (0, -0.36, 2.08), "HJ_Rojo")
    for x in (-0.4, 0.0, 0.4): tube(b, (x, -0.12, 2.2), (x, -0.12, 3.55), 0.035, "HJ_Metal")
    return b.build()

def palanca():   # the big lever of the switchboard: origin = pivot, blades pointing up (= in the contacts = ON), red grip
    b = Builder("HJ_Palanca")
    b.cyl(0.02, 0.02, 0.24, (0, 0, 0), "HJ_Metal", seg=8, rot=[('Y', 90)])
    for s in (-1, 1): b.box((0.02, 0.03, 0.44), (s * 0.07, 0, 0.22), "HJ_Laton")
    b.box((0.18, 0.03, 0.03), (0, 0, 0.4), "HJ_Laton")
    b.cyl(0.035, 0.035, 0.24, (0, -0.0, 0.46), "HJ_Rojo", seg=8, rot=[('Y', 90)])
    tube(b, (0, 0, 0.46), (0, -0.12, 0.5), 0.02, "HJ_Rojo")
    b.ball(0.045, (0, -0.15, 0.51), "HJ_Rojo", seg=8)
    return b.build()

# ================================================================== rooms and corridors
def cama():   # hotel double bed with its bedside table and lamp, 1.9 x 2.0 x 1.1
    b = Builder("HJ_Cama")
    bx = 0.24
    for x in (bx - 0.65, bx + 0.65):
        for y in (-0.92, 0.85): b.box((0.06, 0.06, 0.12), (x, y, 0.06), "HJ_MaderaOscura")
    b.box((1.4, 1.95, 0.28), (bx, 0.0, 0.26), "HJ_Madera")                           # z 0.12..0.4
    b.box((1.36, 1.86, 0.18), (bx, 0.02, 0.49), "HJ_Blanco")                         # mattress z 0.4..0.58
    b.box((1.44, 1.53, 0.24), (bx, -0.225, 0.49), "HJ_TelaPaint")                    # bedspread y -0.99..0.54, z 0.37..0.61
    for s in (-1, 1): b.box((0.55, 0.32, 0.14), (bx + s * 0.32, 0.72, 0.65), "HJ_Blanco", rot=[('X', 12)])
    b.box((1.5, 0.1, 1.1), (bx, 0.96, 0.55), "HJ_Marron")                            # padded headboard
    for i in range(4):
        for j in range(2): b.ball(0.02, (bx - 0.54 + i * 0.36, 0.905, 0.75 + j * 0.25), "HJ_Laton", seg=4)
    b.box((0.42, 0.4, 0.5), (-0.7, 0.7, 0.25), "HJ_MaderaOscura")                    # bedside table
    b.box((0.36, 0.02, 0.14), (-0.7, 0.49, 0.36), "HJ_Madera")
    b.cyl(0.06, 0.08, 0.2, (-0.7, 0.72, 0.6), "HJ_Naranja", seg=8)
    b.cyl(0.12, 0.08, 0.16, (-0.7, 0.72, 0.8), "HJ_Emit_Lampara", seg=8)
    return b.build()

def banera():   # avocado-green bathtub 1.7 x 0.8 with water, taps, a rubber duck and a pink shower curtain (room 237: an old-lady exit)
    b = Builder("HJ_Banera")
    W, D, H, T = 1.7, 0.8, 0.58, 0.1
    for s in (-1, 1):
        b.box((W, T, H), (0, s * (D - T) / 2, H / 2), "HJ_Aguacate")
        b.box((T, D - 2 * T, H), (s * (W - T) / 2, 0, H / 2), "HJ_Aguacate")
    b.box((W - 2 * T, D - 2 * T, 0.4), (0, 0, 0.2), "HJ_Aguacate")
    b.box((W - 2 * T, D - 2 * T, 0.03), (0, 0, 0.415), "HJ_Turquesa")
    tube(b, (0.8, 0.2, 0.68), (0.66, 0.2, 0.64), 0.018, "HJ_Cromo")
    tube(b, (0.8, 0.2, 0.58), (0.8, 0.2, 0.68), 0.018, "HJ_Cromo")
    for y in (0.05, 0.33): b.ball(0.035, (0.8, y, 0.62), "HJ_Cromo", seg=6)
    path(b, [(0.8, 0.33, 0.62), (0.86, 0.36, 1.2), (0.84, 0.36, 1.8)], 0.012, "HJ_Cromo")
    b.cyl(0.06, 0.03, 0.06, (0.84, 0.33, 1.8), "HJ_Cromo", seg=8, rot=[('X', -30)])
    for x in (-0.85, 0.85): tube(b, (x, 0.4, 2.0), (x, -0.42, 2.0), 0.012, "HJ_Cromo")
    tube(b, (-0.85, -0.42, 2.0), (0.85, -0.42, 2.0), 0.012, "HJ_Cromo")
    for i in range(5): b.cyl(0.05, 0.05, 1.3, (-0.8 + i * 0.06, -0.42 + (i % 2) * 0.04, 1.33), "HJ_Rosa", seg=6)
    b.ball(0.06, (-0.3, 0.05, 0.47), "HJ_Amarillo", scale=(1.25, 1, 0.8), seg=8)      # rubber duck
    b.ball(0.04, (-0.24, 0.05, 0.54), "HJ_Amarillo", seg=8)
    b.box((0.04, 0.03, 0.015), (-0.2, 0.05, 0.535), "HJ_Naranja")
    return b.build()

def minibar():   # this map's drink machine: a 70s minibar fridge, lit glass door with bottles, MINIBAR sign; 1.2 x 0.8 x 2.25
    b = Builder("HJ_Minibar")
    b.box((1.2, 0.75, 1.85), (0, 0.025, 0.925), "HJ_Crema")
    b.cyl(0.375, 0.375, 1.2, (0, 0.025, 1.85), "HJ_Crema", seg=12, rot=[('Y', 90)])
    b.box((1.2, 0.02, 0.12), (0, -0.36, 0.06), "HJ_Marron")
    b.box((1.2, 0.02, 0.08), (0, -0.36, 1.78), "HJ_Naranja")
    b.box((0.9, 0.03, 1.4), (-0.08, -0.365, 1.0), "HJ_Cromo")
    b.box((0.76, 0.02, 1.2), (-0.08, -0.39, 1.0), "HJ_Emit_Minibar")
    cols = ("HJ_Botella", "HJ_CristalAmbar", "HJ_Rojo", "HJ_Cristalito", "HJ_Botella")
    for k, z in enumerate((0.55, 0.92, 1.29)):
        b.box((0.76, 0.03, 0.02), (-0.08, -0.415, z), "HJ_Cromo")
        for i in range(5):
            x = -0.38 + i * 0.15
            b.cyl(0.035, 0.035, 0.18, (x, -0.42, z + 0.1), cols[(i + k) % 5], seg=8)
            b.cyl(0.013, 0.013, 0.07, (x, -0.42, z + 0.225), cols[(i + k) % 5], seg=6)
    tube(b, (0.355, -0.43, 0.8), (0.355, -0.43, 1.3), 0.02, "HJ_Cromo")
    for z in (0.8, 1.3): tube(b, (0.355, -0.38, z), (0.355, -0.43, z), 0.012, "HJ_Cromo")
    b.box((0.18, 0.03, 0.3), (0.47, -0.365, 1.2), "HJ_Cromo")                        # coin slot
    b.box((0.03, 0.02, 0.08), (0.47, -0.39, 1.25), "HJ_Oscuro")
    b.box((1.0, 0.12, 0.3), (0, -0.3, 2.05), "HJ_Rojo")
    b.box((0.9, 0.02, 0.22), (0, -0.37, 2.05), "HJ_Emit_Minibar")
    return b.build()

def escritorio():   # the concierge's desk with the typewriter (its sheets: "No por mucho madrugar..."), chair and lamp; 1.6 x 1.3 x 1.2
    b = Builder("HJ_Escritorio")
    b.box((1.6, 0.75, 0.05), (0, 0, 0.745), "HJ_Madera")
    b.box((0.45, 0.7, 0.72), (0.55, 0, 0.36), "HJ_MaderaOscura")
    for i in range(3):
        b.box((0.4, 0.02, 0.2), (0.55, -0.36, 0.13 + i * 0.23), "HJ_Madera")
        b.box((0.12, 0.03, 0.02), (0.55, -0.38, 0.17 + i * 0.23), "HJ_Laton")
    for y in (-0.33, 0.33): b.box((0.05, 0.05, 0.72), (-0.75, y, 0.36), "HJ_MaderaOscura")
    b.box((0.45, 0.35, 0.12), (-0.15, -0.05, 0.83), "HJ_Negro", rot=[('X', -6)])     # typewriter
    for j in range(3): b.box((0.38, 0.06, 0.03), (-0.15, -0.17 + j * 0.06, 0.88 + j * 0.012), "HJ_Metal")
    b.cyl(0.05, 0.05, 0.52, (-0.15, 0.1, 0.92), "HJ_Negro", seg=8, rot=[('Y', 90)])
    b.box((0.21, 0.01, 0.3), (-0.15, 0.12, 1.08), "HJ_Blanco", rot=[('X', 10)])
    b.box((0.21, 0.3, 0.05), (0.25, 0.05, 0.795), "HJ_Blanco", rot=[('Z', -6)])
    b.cyl(0.04, 0.035, 0.09, (-0.55, -0.15, 0.815), "HJ_Blanco", seg=8)
    b.cyl(0.06, 0.07, 0.02, (-0.55, 0.15, 0.78), "HJ_Metal", seg=8)                  # flexo
    path(b, [(-0.55, 0.15, 0.79), (-0.5, 0.2, 1.1), (-0.4, 0.05, 1.2)], 0.012, "HJ_Metal")
    b.cyl(0.06, 0.1, 0.12, (-0.38, 0.03, 1.15), "HJ_Emit_Lampara", seg=8)
    chair(b, -0.1, 0.55, 90, "HJ_Granate", "HJ_MaderaOscura")
    return b.build()

def puerta_habitacion():   # numbered room door frame (wall, 2 m strip): opening 1.0 x 2.15, number plate beside (the digits are a
    b = Builder("HJ_PuertaHabitacion")   # label); the leaf is HJ_HojaPuerta (hinge at x = -0.5, y = -0.05). Also an old-man exit
    door_frame(b, 1.0, 2.15, 0.1, 0.1, "HJ_MaderaOscura")
    b.cyl(0.13, 0.13, 0.024, (0.75, -0.012, 1.55), "HJ_Laton", seg=12, rot=[('X', 90)], scale=(1, 1, 0.6))
    for s in (-1, 1): b.box((0.38, 0.02, 0.08), (s * 0.79, -0.01, 0.95), "HJ_MaderaOscura")   # dado rail
    return b.build()

def hoja_puerta():   # room door leaf: teak, raised panels, lever, peephole, NO MOLESTAR hanger; origin = hinge at the floor, along +X
    b = Builder("HJ_HojaPuerta")
    b.box((0.98, 0.05, 2.13), (0.49, 0, 1.075), "HJ_Madera")
    for z0, z1 in ((0.2, 0.92), (1.1, 1.95)):
        for s in (-1, 1): b.box((0.7, 0.015, z1 - z0), (0.49, s * 0.0325, (z0 + z1) / 2), "HJ_MaderaOscura")
    for s in (-1, 1):
        b.cyl(0.035, 0.035, 0.03, (0.86, s * 0.04, 1.0), "HJ_Laton", seg=8, rot=[('X', 90)])
        b.box((0.14, 0.025, 0.025), (0.8, s * 0.065, 1.0), "HJ_Laton")
    b.cyl(0.012, 0.012, 0.11, (0.49, 0, 1.55), "HJ_Laton", seg=6, rot=[('X', 90)])
    b.box((0.1, 0.008, 0.22), (0.8, -0.085, 0.85), "HJ_Rojo")
    return b.build()

def poste_mojado():   # wet-floor trap post: yellow A-frame sign, mop bucket with wringer, the mop; 0.9 x 0.6 x 1.3
    b = Builder("HJ_PosteMojado")
    c = (-0.2, 0, 0.3)
    for s in (-1, 1): b.box((0.34, 0.025, 0.62), (c[0], s * 0.065, 0.3), "HJ_Amarillo", rot=[('X', s * 12)])
    b.box((0.3, 0.015, 0.06), tilt((c[0], -0.065, 0.3), -12, (0, -0.02, 0.22)), "HJ_Negro", rot=[('X', -12)])   # SUELO MOJADO band
    b.ball(0.03, tilt((c[0], -0.065, 0.3), -12, (0.04, -0.025, 0.08)), "HJ_Negro", seg=6)                       # falling man
    b.box((0.03, 0.012, 0.13), tilt((c[0], -0.065, 0.3), -12, (0.0, -0.022, 0.0)), "HJ_Negro", rot=[('X', -12), ('Y', 50)])
    b.box((0.025, 0.012, 0.12), tilt((c[0], -0.065, 0.3), -12, (-0.07, -0.022, -0.06)), "HJ_Negro", rot=[('X', -12), ('Y', -20)])
    b.box((0.25, 0.012, 0.012), tilt((c[0], -0.065, 0.3), -12, (0, -0.022, -0.17)), "HJ_Negro", rot=[('X', -12)])
    b.cyl(0.17, 0.2, 0.27, (0.22, 0.05, 0.135), "HJ_Azul", seg=10)                   # bucket
    b.cyl(0.17, 0.17, 0.02, (0.22, 0.05, 0.28), "HJ_Agua", seg=10)
    ring(b, (0.22, 0.05, 0.29), 0.18, 0.015, "HJ_Azul", n=10)
    b.box((0.25, 0.12, 0.12), (0.22, 0.2, 0.33), "HJ_Amarillo")
    tube(b, (0.25, 0.05, 0.2), (0.08, 0.22, 1.3), 0.014, "HJ_Aluminio")
    for i in range(6): tube(b, (0.25, 0.05, 0.22), (0.25 + 0.08 * math.cos(i), 0.05 + 0.08 * math.sin(i), 0.06), 0.012, "HJ_Blanco", seg=4)
    return b.build()

def cinta():   # gym treadmill, 0.8 x 1.9 x 1.45: you run towards -Y (the console, display = HJ_Emit_Piloto); a towel on the rail
    b = Builder("HJ_Cinta")
    b.box((0.8, 1.7, 0.2), (0, 0.12, 0.12), "HJ_Marron")
    b.box((0.6, 1.62, 0.03), (0, 0.12, 0.235), "HJ_Goma")
    for s in (-1, 1):
        b.box((0.09, 1.66, 0.04), (s * 0.355, 0.12, 0.24), "HJ_Aluminio")
        tube(b, (s * 0.36, -0.7, 0.24), (s * 0.36, -0.82, 1.25), 0.03, "HJ_Aluminio")
        tube(b, (s * 0.36, -0.79, 1.0), (s * 0.36, -0.25, 1.0), 0.025, "HJ_Aluminio")
        b.ball(0.025, (s * 0.36, -0.25, 1.0), "HJ_Goma", seg=6)
    b.cyl(0.07, 0.07, 0.86, (0, 0.95, 0.12), "HJ_Metal", seg=8, rot=[('Y', 90)])
    cc = (0, -0.86, 1.33)
    b.box((0.84, 0.22, 0.24), cc, "HJ_Marron", rot=[('X', 20)])
    b.box((0.4, 0.02, 0.1), tilt(cc, 20, (0, 0.12, 0.02)), "HJ_Emit_Piloto", rot=[('X', 20)])
    for i in range(3): b.cyl(0.02, 0.02, 0.02, tilt(cc, 20, (-0.24 + i * 0.24, 0.12, -0.07)), "HJ_Rojo", seg=6, rot=[('X', -70)])
    b.box((0.02, 0.32, 0.36), (-0.4, -0.5, 0.84), "HJ_ToallaPaint")
    b.box((0.08, 0.32, 0.02), (-0.37, -0.5, 1.035), "HJ_ToallaPaint")
    return b.build()

def aplique():   # corridor wall lamp (wall, origin at floor level): brass plate, two amber tulip shades at 2 m
    b = Builder("HJ_Aplique")
    b.box((0.12, 0.03, 0.22), (0, -0.015, 2.0), "HJ_Laton")
    for s in (-1, 1):
        tube(b, (0, -0.03, 1.95), (s * 0.16, -0.14, 2.0), 0.012, "HJ_Laton")
        b.cyl(0.035, 0.035, 0.05, (s * 0.16, -0.14, 2.02), "HJ_Laton", seg=8)
        b.cyl(0.05, 0.1, 0.16, (s * 0.16, -0.14, 2.12), "HJ_Emit_Aplique", seg=8)
    return b.build()

def cartel_salida():   # green SALIDA sign (wall, origin at floor level, at 2.5 m), lit even in the blackout; the word is a label
    b = Builder("HJ_CartelSalida")
    b.box((0.5, 0.08, 0.2), (0, -0.04, 2.5), "HJ_Blanco")
    b.box((0.46, 0.02, 0.16), (0, -0.09, 2.5), "HJ_Emit_Salida")
    y = -0.1075
    b.ball(0.018, (-0.15, y, 2.56), "HJ_Blanco", seg=6)                              # running man
    b.box((0.025, 0.015, 0.07), (-0.16, y, 2.5), "HJ_Blanco", rot=[('Y', -15)])
    for a, dx in ((35, -0.18), (-35, -0.14)): b.box((0.018, 0.015, 0.07), (dx, y, 2.44), "HJ_Blanco", rot=[('Y', a)])
    b.box((0.06, 0.015, 0.016), (-0.155, y, 2.52), "HJ_Blanco", rot=[('Y', 25)])
    b.box((0.08, 0.015, 0.11), (-0.05, y, 2.49), "HJ_Blanco")                        # door
    b.box((0.08, 0.015, 0.02), (0.13, y, 2.5), "HJ_Blanco")                          # arrow
    prism(b, [(0.17, 2.47), (0.21, 2.5), (0.17, 2.53)], -0.1, -0.115, "HJ_Blanco")
    for s in (-1, 1): b.box((0.03, 0.04, 0.06), (s * 0.2, -0.02, 2.63), "HJ_Metal")
    return b.build()

def luz_emergencia():   # red emergency light of the stairwells (wall, origin at floor level, at 2.8 m); it blinks in the blackout
    b = Builder("HJ_LuzEmergencia")
    b.box((0.2, 0.04, 0.2), (0, -0.02, 2.8), "HJ_Metal")
    b.cyl(0.08, 0.08, 0.06, (0, -0.07, 2.8), "HJ_Metal", seg=10, rot=[('X', 90)])
    b.ball(0.07, (0, -0.11, 2.8), "HJ_Emit_Emergencia", scale=(1, 1.1, 1), seg=10)
    ring(b, (0, -0.12, 2.8), 0.075, 0.008, "HJ_Metal", axis='Y', n=10)
    for a in (0, 90): b.box((0.008, 0.16, 0.008), (0, -0.13, 2.8), "HJ_Metal", rot=[('Y', a)])
    return b.build()

def tolva():   # laundry chute hatch (wall, origin at floor level): steel frame, open flap, dark mouth, ROPA SUCIA plate
    b = Builder("HJ_Tolva")
    b.box((1.1, 0.06, 1.1), (0, -0.03, 0.95), "HJ_Aluminio")
    b.box((0.9, 0.02, 0.9), (0, -0.07, 0.95), "HJ_Oscuro")
    hinge = (0, -0.11, 0.52)
    b.box((0.9, 0.03, 0.6), tilt(hinge, 35, (0, 0, 0.3)), "HJ_Aluminio", rot=[('X', 35)])
    b.box((0.3, 0.04, 0.04), tilt(hinge, 35, (0, -0.035, 0.55)), "HJ_Negro", rot=[('X', 35)])
    tube(b, (-0.48, -0.11, 0.52), (0.48, -0.11, 0.52), 0.02, "HJ_Metal")
    b.box((0.5, 0.02, 0.12), (0, -0.01, 1.6), "HJ_Azul")
    return b.build()

def puerta_servicio():   # SOLO PERSONAL door frame (wall, 2 m strip): steel frame, card reader with LED, red sign; leaf = HJ_HojaServicio
    b = Builder("HJ_PuertaServicio")
    door_frame(b, 1.0, 2.15, 0.1, 0.1, "HJ_Metal")
    b.box((0.08, 0.03, 0.12), (0.72, -0.015, 1.2), "HJ_Negro")
    b.ball(0.012, (0.72, -0.035, 1.24), "HJ_Emit_Piloto", seg=6)
    b.box((0.04, 0.03, 0.006), (0.72, -0.045, 1.18), "HJ_Metal")
    b.box((0.64, 0.02, 0.18), (0, -0.01, 2.45), "HJ_Blanco")
    b.box((0.6, 0.02, 0.14), (0, -0.03, 2.45), "HJ_Rojo")
    return b.build()

def hoja_servicio():   # steel service door leaf: push bar, kick plate, wired-glass window; origin = hinge at the floor, along +X
    b = Builder("HJ_HojaServicio")
    b.box((0.98, 0.05, 2.13), (0.49, 0, 1.075), "HJ_Uralita")
    b.box((0.3, 0.08, 0.45), (0.49, 0, 1.6), "HJ_Cristal")
    b.box((0.9, 0.08, 0.25), (0.49, 0, 0.17), "HJ_Aluminio")
    tube(b, (0.12, -0.09, 1.0), (0.9, -0.09, 1.0), 0.025, "HJ_Cromo")
    for x in (0.15, 0.87): b.box((0.04, 0.07, 0.05), (x, -0.06, 1.0), "HJ_Cromo")
    return b.build()

# ================================================================== outside (pool, gardens)
def seto():   # hedge of the maze, one 2 x 2 cell, 3 m tall, slightly irregular (all M_Leaves)
    b = Builder("HJ_Seto")
    b.box((1.96, 1.96, 2.9), (0, 0, 1.45), "M_Leaves")
    k = 0
    for a in (0, 90, 180, 270):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        for t, z in ((-0.45, 0.9 + (k % 3) * 0.4), (0.4, 1.8 - (k % 2) * 0.5)):
            k += 1
            b.box((0.12, 0.9, 0.8), (0.95 * ca - t * sa, 0.95 * sa + t * ca, z), "M_Leaves", rot=[('X', 4 + k), ('Z', a + 3 * (k % 3))])
    for x, y, r in ((-0.45, -0.4, 5), (0.4, 0.35, -7), (0.3, -0.45, 12)):
        b.box((0.9, 0.85, 0.25), (x, y, 2.95), "M_Leaves", rot=[('Z', r), ('X', r / 3)])
    return b.build()

def tumbona():   # pool lounger 0.7 x 1.9: white plastic, striped cushion (HJ_ToldoPaint), folded towel; low cover (0.85 m)
    b = Builder("HJ_Tumbona")
    b.box((0.66, 1.2, 0.05), (0, -0.35, 0.33), "HJ_Blanco")
    bc = (0, 0.25 + 0.375 * math.cos(math.radians(40)), 0.33 + 0.375 * math.sin(math.radians(40)))
    b.box((0.66, 0.75, 0.05), bc, "HJ_Blanco", rot=[('X', 40)])
    for x in (-0.3, 0.3):
        for y in (-0.88, 0.15): b.box((0.05, 0.05, 0.31), (x, y, 0.155), "HJ_Blanco")
        b.cyl(0.08, 0.08, 0.08, (x, 0.62, 0.08), "HJ_Goma", seg=8, rot=[('Y', 90)])
        tube(b, (x, 0.62, 0.08), (x, 0.45, 0.4), 0.02, "HJ_Blanco")
    for i in range(6): b.box((0.6, 0.19, 0.05), (0, -0.9 + i * 0.2 + 0.095, 0.38), "HJ_ToldoPaint" if i % 2 == 0 else "HJ_Blanco")
    for i in range(4): b.box((0.6, 0.18, 0.05), tilt(bc, 40, (0, -0.27 + i * 0.18, 0.05)), "HJ_ToldoPaint" if i % 2 else "HJ_Blanco", rot=[('X', 40)])
    b.box((0.45, 0.3, 0.08), (0, -0.75, 0.445), "HJ_ToallaPaint")
    return b.build()

def sombrilla():   # beach parasol, 2.4 m across, 2.6 m: striped canopy, fringe, concrete base
    b = Builder("HJ_Sombrilla")
    b.cyl(0.28, 0.25, 0.12, (0, 0, 0.06), "HJ_Hormigon", seg=10)
    tube(b, (0, 0, 0.12), (0, 0, 2.55), 0.025, "HJ_Aluminio")
    n, R, z0, z1 = 8, 1.2, 2.05, 2.45
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        m = "HJ_ToldoPaint" if i % 2 == 0 else "HJ_Blanco"
        tri_panel(b, (0, 0, z1), (R * math.cos(a0), R * math.sin(a0), z0), (R * math.cos(a1), R * math.sin(a1), z0), 0.02, m)
        am = (a0 + a1) / 2
        b.box((0.88, 0.015, 0.12), (R * 0.93 * math.cos(am), R * 0.93 * math.sin(am), z0 - 0.08), m, rot=[('Z', math.degrees(am) + 90)])
        tube(b, (0, 0, 2.3), (R * 0.98 * math.cos(a0), R * 0.98 * math.sin(a0), z0 - 0.025), 0.01, "HJ_Aluminio")
    b.ball(0.05, (0, 0, 2.58), "HJ_Blanco", seg=6)
    return b.build()

def palmera():   # palm tree, ~6 m: curved ringed trunk (origin at its base), eight drooping fronds, coconuts
    b = Builder("HJ_Palmera")
    n, H = 9, 5.4
    pts = [(0.45 * (i / n) ** 2, 0, H * i / n) for i in range(n + 1)]
    for i in range(n):
        r0, r1 = 0.22 - 0.08 * i / n, 0.22 - 0.08 * (i + 1) / n
        cone(b, pts[i], pts[i + 1], r0, r1 * 0.85, "M_Bark", seg=8)
    top = Vector(pts[-1])
    for k in range(8):
        az = k * 45 + 10
        p = top.copy()
        for j, (ln, el, w) in enumerate(((0.9, 25, 0.5), (0.8, -5, 0.45), (0.7, -35, 0.3))):
            el += (k % 3) * 5
            d = Vector((math.cos(math.radians(el)) * math.cos(math.radians(az)), math.cos(math.radians(el)) * math.sin(math.radians(az)), math.sin(math.radians(el))))
            q = p + d * ln
            b.box((ln + 0.05, w, 0.04), tuple((p + q) / 2), "M_Palm", rot=[('Y', -el), ('Z', az)])
            p = q
    for k in range(4):
        a = k * math.pi / 2 + 0.3
        b.ball(0.12, (top.x + 0.17 * math.cos(a), 0.17 * math.sin(a), top.z - 0.15), "HJ_Marron", seg=8)
    return b.build()

def bar_piscina():   # pool bar kiosk (one cell, a drink machine): bamboo counter, thatched roof, bottles, lit BAR sign; 2.2 x 2.2 x 3.6
    b = Builder("HJ_BarPiscina")
    for sx in (-1, 1):
        for sy in (-1, 1): b.cyl(0.07, 0.07, 2.6, (sx * 0.88, sy * 0.88, 1.3), "M_Bark", seg=8)
    b.box((1.7, 0.4, 1.05), (0, -0.65, 0.525), "HJ_Madera")
    for i in range(11): b.cyl(0.04, 0.04, 1.0, (-0.8 + i * 0.16, -0.88, 0.52), "HJ_Paja", seg=6)
    b.box((1.9, 0.55, 0.06), (0, -0.65, 1.08), "HJ_MaderaOscura")
    b.box((1.7, 0.25, 0.04), (0, 0.78, 1.4), "HJ_MaderaOscura")                      # back shelf
    for i in range(7): b.cyl(0.035, 0.035, 0.24, (-0.6 + i * 0.2, 0.78, 1.54), ("HJ_Botella", "HJ_CristalAmbar", "HJ_Rojo")[i % 3], seg=6)
    for x in (-0.5, 0.0, 0.45):
        b.cyl(0.04, 0.02, 0.1, (x, -0.65, 1.16), "HJ_Cristalito", seg=6)
        tube(b, (x, -0.65, 1.2), (x + 0.03, -0.66, 1.3), 0.004, "HJ_Blanco", seg=4)
        b.cyl(0.06, 0.0, 0.03, (x + 0.03, -0.66, 1.31), "HJ_ToldoPaint", seg=6)       # paper umbrella
    b.cyl(0.06, 0.07, 0.25, (0.7, -0.6, 1.235), "HJ_Cromo", seg=8)                    # blender
    b.box((2.1, 2.1, 0.12), (0, 0, 2.66), "HJ_MaderaOscura")
    b.pyramid(2.3, 2.3, 0.95, (0, 0, 2.72), "HJ_Paja")
    for s in (-1, 1):
        b.box((2.3, 0.06, 0.25), (0, s * 1.12, 2.55), "HJ_Paja")
        b.box((0.06, 2.18, 0.25), (s * 1.12, 0, 2.55), "HJ_Paja")
    b.box((0.9, 0.08, 0.3), (0, -0.99, 2.25), "HJ_Rojo")                              # BAR sign (label)
    b.box((0.8, 0.02, 0.22), (0, -1.04, 2.25), "HJ_Emit_BarPiscina")
    return b.build()

def luz_piscina():   # underwater pool light (on the pool wall, origin at its centre, facing -Y)
    b = Builder("HJ_LuzPiscina")
    b.cyl(0.2, 0.2, 0.04, (0, -0.02, 0), "HJ_Cromo", seg=14, rot=[('X', 90)])
    b.cyl(0.15, 0.15, 0.06, (0, -0.04, 0), "HJ_Emit_Piscina", seg=14, rot=[('X', 90)])
    for a in range(4): b.ball(0.012, (0.175 * math.cos(a * math.pi / 2 + 0.78), -0.045, 0.175 * math.sin(a * math.pi / 2 + 0.78)), "HJ_Metal", seg=4)
    return b.build()

# ================================================================== roof
def helipuerto():   # helipad, 8 x 8 x 0.12 m: yellow circle, white H, 16 edge lights (the mission's helipad lights), windsock
    b = Builder("HJ_Helipuerto")
    b.box((8.0, 8.0, 0.12), (0, 0, 0.06), "HJ_Asfalto")
    n, R = 28, 3.0
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        b.box((2 * R * math.sin(math.pi / n) + 0.02, 0.3, 0.02), (R * math.cos(a), R * math.sin(a), 0.13), "HJ_Amarillo", rot=[('Z', math.degrees(a) + 90)])
    for s in (-1, 1): b.box((0.5, 3.0, 0.02), (s * 0.8, 0, 0.13), "HJ_Blanco")
    b.box((1.1, 0.5, 0.02), (0, 0, 0.13), "HJ_Blanco")
    for s in (-1, 1):
        b.box((7.7, 0.15, 0.02), (0, s * 3.775, 0.13), "HJ_Blanco")
        b.box((0.15, 7.4, 0.02), (s * 3.775, 0, 0.13), "HJ_Blanco")
    for i in range(4):
        for t in (-3, -1, 1, 3):
            x, y = [(t, -3.9), (3.9, t), (-t, 3.9), (-3.9, -t)][i]
            b.cyl(0.09, 0.09, 0.05, (x, y, 0.145), "HJ_Metal", seg=8)
            b.ball(0.07, (x, y, 0.17), "HJ_Emit_Helipuerto", scale=(1, 1, 0.8), seg=8)
    tube(b, (3.7, 3.7, 0.12), (3.7, 3.7, 2.6), 0.03, "HJ_Blanco")                    # windsock
    for i in range(4):
        cone(b, (3.7 - i * 0.25, 3.7, 2.5 - i * 0.04), (3.7 - (i + 1) * 0.25, 3.7, 2.5 - (i + 1) * 0.04), 0.2 - i * 0.025, 0.2 - (i + 1) * 0.025, "HJ_Naranja" if i % 2 == 0 else "HJ_Blanco", seg=8)
    return b.build()

def helicoptero():   # the rescue helicopter (70s, white and orange), nose towards -Y; the main rotor is HJ_Rotor, hub at (0, -0.4, 3.0)
    b = Builder("HJ_Helicoptero")
    b.ball(1.0, (0, -1.0, 1.55), "HJ_Blanco", scale=(1.0, 1.9, 1.0), seg=16)
    dome(b, 1.0, (0, -1.0, 1.3), "HJ_Naranja", scale=(1.015, 1.92, -0.76), seg=16, rings=4)   # orange belly: a bowl round the lower shell
    b.ball(0.9, (0, -2.1, 1.85), "HJ_Cristal", scale=(0.95, 1.05, 0.78), seg=14)    # windscreen bubble
    for s in (-1, 1): b.ball(0.45, (s * 0.72, -1.25, 1.95), "HJ_Cristal", scale=(0.5, 1.0, 0.6), seg=10)
    cone(b, (0, 0.7, 1.75), (0, 5.6, 2.15), 0.34, 0.12, "HJ_Naranja", seg=10)
    b.box((0.08, 0.7, 1.1), (0, 5.5, 2.55), "HJ_Naranja", rot=[('X', -25)])
    b.box((1.5, 0.35, 0.06), (0, 4.5, 2.05), "HJ_Blanco")
    b.cyl(0.06, 0.06, 0.2, (0.15, 5.55, 2.65), "HJ_Metal", seg=8, rot=[('Y', 90)])
    for a in (30, 210): b.box((0.03, 0.12, 0.55), tilt((0.26, 5.55, 2.65), a, (0, 0, 0.27), 'X'), "HJ_Negro", rot=[('X', a)])
    for s in (-1, 1):
        tube(b, (s * 0.95, -2.0, 0.045), (s * 0.95, 1.4, 0.045), 0.045, "HJ_Aluminio", seg=8)
        tube(b, (s * 0.95, -2.0, 0.045), (s * 0.95, -2.35, 0.3), 0.045, "HJ_Aluminio", seg=8)
        for y in (-1.4, 0.6): tube(b, (s * 0.95, y, 0.045), (s * 0.55, y, 0.8), 0.035, "HJ_Aluminio", seg=6)
        b.box((0.02, 0.45, 0.12), (s * 0.99, -0.55, 1.75), "HJ_Rojo")                  # red cross
        b.box((0.02, 0.12, 0.45), (s * 0.99, -0.55, 1.75), "HJ_Rojo")
    b.box((0.9, 1.6, 0.5), (0, 0.0, 2.6), "HJ_Blanco")
    tube(b, (0, 0.75, 2.7), (0, 1.0, 2.75), 0.13, "HJ_Metal", seg=8)
    b.cyl(0.08, 0.08, 0.2, (0, -0.4, 2.9), "HJ_Metal", seg=8)
    b.cyl(0.07, 0.07, 0.12, (0, -2.6, 0.75), "HJ_Metal", seg=8, rot=[('X', 70)])     # searchlight
    b.cyl(0.055, 0.055, 0.03, (0, -2.65, 0.7), "HJ_Emit_Faro", seg=8, rot=[('X', 70)])
    return b.build()

def rotor():   # main rotor, 9.6 m, two blades with yellow tips; origin = the hub (spins about Z)
    b = Builder("HJ_Rotor")
    b.cyl(0.18, 0.15, 0.15, (0, 0, 0.075), "HJ_Metal", seg=10)
    b.box((9.6, 0.32, 0.05), (0, 0, 0.1), "HJ_Negro")
    for s in (-1, 1): b.box((0.32, 0.36, 0.09), (s * 4.66, 0, 0.1), "HJ_Amarillo")
    return b.build()

def deposito():   # rooftop water tank (fibre cement) on a steel stand with a ladder, 1.9 x 2.0 x 3.55
    b = Builder("HJ_Deposito")
    for sx in (-1, 1):
        for sy in (-1, 1): b.box((0.1, 0.1, 1.4), (sx * 0.8, sy * 0.8, 0.7), "M_Iron")
        tube(b, (sx * 0.8, -0.8, 0.1), (sx * 0.8, 0.8, 1.3), 0.02, "M_Iron")
        tube(b, (-0.8, sx * 0.8, 0.1), (0.8, sx * 0.8, 1.3), 0.02, "M_Iron")
    b.box((1.9, 1.9, 0.08), (0, 0, 1.44), "M_Iron")
    b.cyl(0.85, 0.85, 1.7, (0, 0, 2.33), "HJ_Uralita", seg=14)
    for z in (2.0, 2.7): b.cyl(0.87, 0.87, 0.05, (0, 0, z), "HJ_Uralita", seg=14)
    b.cyl(0.88, 0.15, 0.35, (0, 0, 3.355), "HJ_Uralita", seg=14)
    for x in (0.35, 0.65): tube(b, (x, -0.97, 0.0), (x, -0.97, 3.2), 0.022, "HJ_Metal")
    for i in range(10): tube(b, (0.35, -0.97, 0.3 + i * 0.3), (0.65, -0.97, 0.3 + i * 0.3), 0.015, "HJ_Metal")
    tube(b, (-0.4, 0.4, 1.4), (-0.4, 0.4, 0.0), 0.05, "HJ_Metal")
    return b.build()

def aire():   # rooftop air-conditioning unit, 1.9 x 1.2 x 1.5: louvred box, two fans on top, rust
    b = Builder("HJ_AireAcondicionado")
    b.box((1.8, 1.1, 1.2), (0, 0, 0.7), "HJ_Uralita")
    for s in (-1, 1):
        b.box((1.8, 0.12, 0.1), (0, s * 0.45, 0.05), "M_Iron")
        for i in range(8): b.box((1.6, 0.03, 0.06), (0, s * 0.57, 0.35 + i * 0.1), "HJ_Uralita", rot=[('X', s * 30)])
        b.box((0.02, 0.4, 0.3), (s * 0.915, 0.1 * s, 0.5 + 0.3 * (s > 0)), "HJ_Oxido")
    for x in (-0.45, 0.45):
        b.cyl(0.42, 0.42, 0.1, (x, 0, 1.35), "HJ_Metal", seg=14)
        b.cyl(0.38, 0.38, 0.02, (x, 0, 1.41), "HJ_Oscuro", seg=14)
        for a in (0, 60, 120): b.box((0.34, 0.08, 0.01), (x, 0, 1.43), "HJ_Metal", rot=[('Z', a)])
        for a in (0, 90): b.box((0.82, 0.03, 0.03), (x, 0, 1.455), "HJ_Cromo", rot=[('Z', a)])
        ring(b, (x, 0, 1.45), 0.4, 0.015, "HJ_Cromo", n=14)
    b.box((0.3, 0.02, 0.1), (-0.6, -0.6, 1.2), "HJ_Blanco")
    return b.build()

def gondola_limpia():   # window cleaner's cradle at the roof edge (an old-man entrance): origin = the roof edge at roof level;
    b = Builder("HJ_Gondola")   # the cradle hangs outside (-Y), its rail 0.55 m above the roof; the davits stand on the roof (+Y)
    yc = -0.55
    b.box((1.9, 0.7, 0.06), (0, yc, -0.5), "HJ_Aluminio")
    for s in (-1, 1):
        b.box((1.8, 0.03, 0.25), (0, yc + s * 0.335, -0.345), "HJ_Amarillo")
        b.box((0.03, 0.64, 0.25), (s * 0.885, yc, -0.345), "HJ_Amarillo")
    for sx in (-1, 1):
        for sy in (-1, 1): tube(b, (sx * 0.93, yc + sy * 0.33, -0.47), (sx * 0.93, yc + sy * 0.33, 0.55), 0.022, "HJ_Aluminio")
    for z in (0.05, 0.55):
        for sy in (-1, 1): tube(b, (-0.93, yc + sy * 0.33, z), (0.93, yc + sy * 0.33, z), 0.02, "HJ_Aluminio")
        for sx in (-1, 1): tube(b, (sx * 0.93, yc - 0.33, z), (sx * 0.93, yc + 0.33, z), 0.02, "HJ_Aluminio")
    for s in (-1, 1):
        x = s * 0.75
        b.box((0.4, 0.4, 0.25), (x, 0.6, 0.125), "M_Iron")
        tube(b, (x, 0.6, 0.25), (x, 0.6, 1.9), 0.05, "M_Iron", seg=8)
        tube(b, (x, 0.6, 1.9), (x, yc, 1.9), 0.04, "M_Iron", seg=8)
        b.cyl(0.07, 0.07, 0.04, (x, yc, 1.84), "HJ_Metal", seg=8, rot=[('Y', 90)])
        tube(b, (x, yc, 1.8), (x, yc, 0.55), 0.008, "HJ_Negro", seg=4)
    b.cyl(0.12, 0.14, 0.22, (-0.5, yc, -0.36), "HJ_Azul", seg=8)
    tube(b, (0.3, yc, -0.47), (0.55, yc - 0.1, 0.4), 0.012, "HJ_Negro")
    b.box((0.35, 0.04, 0.05), (0.56, yc - 0.1, 0.42), "HJ_Goma")
    return b.build()

def puerta_atico(broken):   # door of the penthouse suite (wall, 2 m strip): double padded leaves, gold frame, SUITE plate;
    b = Builder("HJ_PuertaAticoRota" if broken else "HJ_PuertaAtico")   # broken = Anselmo's axe went through it ("Aqui esta Anselmo!")
    door_frame(b, 1.5, 2.4, 0.16, 0.14, "HJ_Oro")
    b.box((1.95, 0.16, 0.12), (0, -0.08, 2.62), "HJ_Oro")
    b.box((0.6, 0.02, 0.16), (0, -0.01, 2.85), "HJ_Negro")
    hole = (-0.38, 1.6)
    for s in (-1, 1):
        if broken and s > 0:                                                         # the right leaf hangs from one hinge, open
            th = 32
            c = (0.75 - 0.37 * math.cos(math.radians(th)), -0.05 - 0.37 * math.sin(math.radians(th)), 1.2)
            b.box((0.74, 0.06, 2.38), c, "HJ_MaderaOscura", rot=[('Y', 3), ('Z', th)])
            continue
        x = s * 0.375
        b.box((0.74, 0.06, 2.38), (x, -0.05, 1.2), "HJ_MaderaOscura")
        for i in range(3):
            for j in range(6):
                px, pz = x - 0.22 + i * 0.22, 0.4 + j * 0.34
                if broken and math.hypot(px - hole[0], pz - hole[1]) < 0.36: continue
                b.ball(0.016, (px, -0.085, pz), "HJ_Oro", seg=4)
        tube(b, (x - s * 0.3, -0.11, 0.95), (x - s * 0.3, -0.11, 1.35), 0.018, "HJ_Laton")
    if broken:
        pts = [(hole[0] + 0.3 * math.cos(a) * (0.75 + 0.3 * ((k * 7) % 3) / 2), hole[1] + 0.32 * math.sin(a) * (0.8 + 0.25 * ((k * 5) % 3) / 2))
               for k, a in enumerate([2 * math.pi * k / 9 for k in range(9)])]
        prism(b, pts, -0.081, -0.095, "HJ_Oscuro")
        for k in range(12):
            a = 2 * math.pi * k / 12 + 0.2
            px, pz = hole[0] + 0.3 * math.cos(a), hole[1] + 0.3 * math.sin(a)
            b.box((0.03, 0.025, 0.2), (px, -0.1, pz), "HJ_Madera", rot=[('X', -25), ('Y', -math.degrees(a) + 90)])
        for k in range(6): b.box((0.05, 0.25, 0.025), (-0.6 + k * 0.22, -0.45 - (k % 2) * 0.2, 0.0125), "HJ_Madera", rot=[('Z', k * 37)])
    return b.build()

# ================================================================== pickups and carried things
def objetos_perdidos():   # this map's weapon box: a brass bellhop luggage cart full of lost property, 1.4 x 0.8 x 1.95
    b = Builder("HJ_ObjetosPerdidos")
    b.box((1.3, 0.7, 0.08), (0, 0, 0.24), "HJ_Granate")
    b.box((1.34, 0.74, 0.03), (0, 0, 0.185), "HJ_Laton")
    for x in (-0.55, 0.55):
        for y in (-0.28, 0.28): b.cyl(0.09, 0.09, 0.05, (x, y, 0.09), "HJ_Goma", seg=10, rot=[('X', 90)])
    for x in (-0.62, 0.62):
        for y in (-0.3, 0.3): path(b, [(x, y, 0.28), (x, y, 1.75), (x, 0, 1.92)], 0.02, "HJ_Laton")
    tube(b, (-0.62, 0, 1.92), (0.62, 0, 1.92), 0.025, "HJ_Laton")
    b.box((0.75, 0.5, 0.22), (-0.2, 0, 0.39), "HJ_Marron")
    for x in (-0.4, 0.0): b.box((0.05, 0.53, 0.25), (x, 0, 0.39), "HJ_Negro")
    b.box((0.6, 0.42, 0.2), (-0.15, 0.02, 0.61), "HJ_Mostaza", rot=[('Z', 8)])
    b.box((0.18, 0.5, 0.65), (0.45, 0, 0.605), "HJ_Aguacate")
    b.box((0.04, 0.12, 0.05), (0.45, 0, 0.955), "HJ_Negro")
    b.cyl(0.18, 0.18, 0.2, (-0.1, 0, 0.81), "HJ_Rosa", seg=10)
    ring(b, (0.25, -0.15, 1.15), 0.24, 0.07, "HJ_Rosa", axis='Y', n=12, seg=6)       # inflatable flamingo
    path(b, [(0.45, -0.15, 1.25), (0.5, -0.15, 1.5), (0.42, -0.15, 1.6)], 0.03, "HJ_Rosa")
    b.box((0.08, 0.04, 0.04), (0.36, -0.15, 1.58), "HJ_Negro")
    tube(b, (-0.5, 0.25, 0.28), (-0.35, 0.3, 1.3), 0.018, "HJ_Negro")                # umbrella
    b.box((0.74, 0.015, 0.22), (0, 0, 1.68), "HJ_Rojo")                              # OBJETOS PERDIDOS (label)
    b.box((0.7, 0.05, 0.18), (0, 0, 1.68), "HJ_Crema")
    for x in (-0.3, 0.3): tube(b, (x, 0, 1.77), (x, 0, 1.9), 0.006, "HJ_Negro", seg=4)
    return b.build()

def cafe():   # the pilot's coffee (carried in both hands): small tray with cup, saucer, sugar and a churro; origin at the tray bottom
    b = Builder("HJ_Cafe")
    b.box((0.34, 0.24, 0.015), (0, 0, 0.0075), "HJ_Cromo")
    for s in (-1, 1):
        b.box((0.34, 0.01, 0.025), (0, s * 0.125, 0.0125), "HJ_Cromo")
        b.box((0.01, 0.25, 0.025), (s * 0.175, 0, 0.0125), "HJ_Cromo")
    b.cyl(0.075, 0.075, 0.012, (-0.05, 0, 0.021), "HJ_Blanco", seg=12)
    b.cyl(0.04, 0.048, 0.065, (-0.05, 0, 0.0595), "HJ_Blanco", seg=12)
    b.cyl(0.04, 0.04, 0.012, (-0.05, 0, 0.092), "HJ_Cafe", seg=12)
    ring(b, (-0.05, 0, 0.097), 0.045, 0.005, "HJ_Blanco", n=12, seg=4)
    ring(b, (-0.1, 0, 0.06), 0.02, 0.006, "HJ_Blanco", axis='Y', n=8, seg=4)
    b.box((0.08, 0.012, 0.004), (0.06, -0.08, 0.021), "HJ_Cromo", rot=[('Z', 30)])
    b.box((0.05, 0.03, 0.008), (0.1, 0.06, 0.019), "HJ_Rosa")
    b.cyl(0.015, 0.015, 0.14, (0.1, -0.05, 0.03), "HJ_Comida", seg=6, rot=[('Y', 90), ('Z', 20)])
    return b.build()

def albornoz():   # the hotel bathrobe, folded (carried in both hands: x2 at the extraction), gold piping and badge; 0.5 x 0.36 x 0.2
    b = Builder("HJ_Albornoz")
    b.box((0.48, 0.34, 0.16), (0, 0, 0.08), "HJ_Blanco")
    b.box((0.46, 0.3, 0.03), (0, 0.01, 0.175), "HJ_Blanco", rot=[('Z', 2)])
    b.box((0.3, 0.07, 0.05), (0, -0.13, 0.2), "HJ_Blanco")
    b.box((0.51, 0.05, 0.18), (0.0, 0.05, 0.09), "HJ_Blanco")                        # belt round the bundle
    for s in (-1, 1): b.box((0.49, 0.008, 0.012), (0, s * 0.172, 0.15), "HJ_Oro")
    b.box((0.08, 0.06, 0.006), (0.12, -0.05, 0.197), "HJ_Oro")
    return b.build()

def pulsera_puertas():   # staff bracelet (opens SOLO PERSONAL, the service lift, the roof): orange coil band + brass master key with
    b = Builder("HJ_PulseraPuertas")   # a red diamond fob; lying flat, ~0.27 m (pickup size, 1.5 x real)
    pts = [(0.06 * math.cos(2 * math.pi * 3 * t / 36), 0.06 * math.sin(2 * math.pi * 3 * t / 36), 0.012 + 0.03 * t / 36) for t in range(37)]
    path(b, pts, 0.008, "HJ_Naranja", seg=5, joints=False)
    ring(b, (0.08, 0, 0.02), 0.02, 0.003, "HJ_Laton", axis='Y', n=8, seg=4)
    b.cyl(0.022, 0.022, 0.006, (0.12, 0, 0.004), "HJ_Laton", seg=10)
    b.box((0.08, 0.012, 0.006), (0.17, 0, 0.004), "HJ_Laton")
    for i in range(3): b.box((0.01, 0.012, 0.006), (0.15 + i * 0.02, -0.01, 0.004), "HJ_Laton")
    prism(b, [(0.08, -0.035), (0.11, -0.075), (0.08, -0.115), (0.05, -0.075)], 0.0, 0.012, "HJ_Rojo", axis='Z')
    tube(b, (0.08, -0.04, 0.008), (0.08, -0.012, 0.016), 0.004, "HJ_Laton", seg=4)
    return b.build()

def pulsera_todo():   # all-inclusive wristband (the bingo prize): gold plastic band, snap tab, little plaque; lying flat, 0.11 m
    b = Builder("HJ_PulseraTodoIncluido")
    n, R = 16, 0.045
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        b.box((2 * R * math.sin(math.pi / n) + 0.002, 0.004, 0.02), (R * math.cos(a), R * math.sin(a), 0.01), "HJ_Oro", rot=[('Z', math.degrees(a) + 90)])
    b.box((0.03, 0.012, 0.026), (R + 0.004, 0, 0.013), "HJ_Oro")
    b.box((0.04, 0.006, 0.018), (0, -R - 0.005, 0.01), "HJ_Blanco")
    return b.build()

# ================================================================== worn by the old men
def moto():   # mobility scooter (a tricycle) ridden by an old man: red body, tiller, basket with a baguette, orange flag;
    b = Builder("HJ_MotoMovilidad")   # origin on the ground under the seat, front -Y; 0.65 x 1.4 x 1.7
    b.box((0.5, 1.05, 0.1), (0, -0.2, 0.2), "HJ_Metal")
    b.box((0.56, 0.5, 0.18), (0, 0.15, 0.33), "HJ_Rojo")
    b.box((0.4, 0.35, 0.3), (0, -0.7, 0.35), "HJ_Rojo", rot=[('X', -25)])
    b.box((0.44, 0.5, 0.02), (0, -0.3, 0.26), "HJ_Goma")
    b.cyl(0.13, 0.13, 0.08, (0, -0.8, 0.13), "HJ_Goma", seg=10, rot=[('Y', 90)])
    for s in (-1, 1): b.cyl(0.13, 0.13, 0.08, (s * 0.3, 0.22, 0.13), "HJ_Goma", seg=10, rot=[('Y', 90)])
    for x, y in ((0.0, -0.8), (-0.3, 0.22), (0.3, 0.22)): b.cyl(0.05, 0.05, 0.1, (x, y, 0.13), "HJ_Cromo", seg=8, rot=[('Y', 90)])   # hubs
    tube(b, (0, -0.72, 0.45), (0, -0.6, 0.95), 0.025, "HJ_Metal")
    tube(b, (-0.18, -0.58, 0.95), (0.18, -0.58, 0.95), 0.018, "HJ_Metal", seg=5)   # its ends are inside the grips
    for s in (-1, 1): tube(b, (s * 0.17, -0.58, 0.95), (s * 0.27, -0.58, 0.95), 0.025, "HJ_Goma")
    b.box((0.2, 0.08, 0.1), (0, -0.63, 0.98), "HJ_Negro")
    b.cyl(0.05, 0.05, 0.04, (0, -0.84, 0.62), "HJ_Cromo", seg=8, rot=[('X', 90)])
    b.cyl(0.04, 0.04, 0.02, (0, -0.87, 0.62), "HJ_Emit_Faro", seg=8, rot=[('X', 90)])
    c = [(-0.16, -0.82), (0.16, -0.82), (0.16, -1.02), (-0.16, -1.02)]
    for z in (0.7, 0.88): path(b, [(x, y, z) for x, y in c + c[:1]], 0.008, "HJ_Cromo", seg=4)
    for x, y in c: tube(b, (x, y, 0.7), (x, y, 0.88), 0.008, "HJ_Cromo", seg=4)
    tube(b, (-0.05, -0.92, 0.72), (0.08, -0.98, 1.05), 0.035, "HJ_Comida", seg=6)   # baguette
    tube(b, (0.06, -0.88, 0.72), (-0.06, -0.86, 1.0), 0.02, "HJ_Verde", seg=6)       # leek
    tube(b, (0, 0.0, 0.42), (0, 0.0, 0.5), 0.03, "HJ_Metal")
    b.box((0.46, 0.42, 0.1), (0, 0.0, 0.55), "HJ_Crema")
    b.box((0.44, 0.08, 0.4), (0, 0.22, 0.82), "HJ_Crema", rot=[('X', -10)])
    for s in (-1, 1): b.box((0.05, 0.3, 0.04), (s * 0.24, -0.02, 0.75), "HJ_Negro")
    tube(b, (0.22, 0.32, 0.42), (0.22, 0.36, 1.7), 0.008, "HJ_Aluminio", seg=4)
    prism(b, [(0.36, 1.68), (0.36, 1.5), (0.6, 1.59)], 0.215, 0.225, "HJ_Naranja", axis='X')
    b.box((0.18, 0.02, 0.08), (0, 0.41, 0.33), "HJ_Blanco")
    return b.build()

def bandeja():   # the buffet lady's tray (her shield), piled high; origin = centre of the tray (held flat at the chest); 0.62 x 0.42 x 0.4
    b = Builder("HJ_Bandeja")
    b.cyl(0.31, 0.31, 0.02, (0, 0, 0.01), "HJ_Cromo", seg=16, scale=(1, 0.68, 1))
    pts = [(0.31 * math.cos(2 * math.pi * i / 16), 0.31 * 0.68 * math.sin(2 * math.pi * i / 16), 0.025) for i in range(17)]
    path(b, pts, 0.008, "HJ_Cromo", seg=4, joints=False)
    b.ball(0.14, (-0.12, 0.02, 0.02), "HJ_Comida", scale=(1, 0.9, 0.5), seg=10)        # paella mound
    for i in range(4): cone(b, (-0.2 + i * 0.05, 0.0, 0.08), (-0.18 + i * 0.05, 0.05, 0.085), 0.015, 0.008, "HJ_Gamba", seg=5)
    for i in range(6): b.cyl(0.022, 0.022, 0.07, (0.08 + (i % 3) * 0.05, -0.08, 0.042 + (i // 3) * 0.045), "HJ_Marron", seg=6, rot=[('Y', 90)])
    b.cyl(0.06, 0.045, 0.06, (0.18, 0.06, 0.05), "HJ_Mostaza", seg=10)              # flan
    b.cyl(0.044, 0.044, 0.01, (0.18, 0.06, 0.085), "HJ_Marron", seg=10)
    for i in range(3): b.box((0.11, 0.015, 0.08), (0.03 + i * 0.035, 0.12, 0.06), "HJ_Rojo", rot=[('Y', 30 - i * 15)])   # watermelon
    b.ball(0.11, (0.0, 0.0, 0.19), "HJ_Asado", scale=(1.35, 0.95, 0.75), seg=10)      # the roast chicken on top
    for s in (-1, 1):
        cone(b, (s * 0.08, -0.06, 0.2), (s * 0.15, -0.1, 0.27), 0.045, 0.02, "HJ_Asado", seg=8)
        b.ball(0.018, (s * 0.16, -0.105, 0.28), "HJ_Blanco", seg=6)
    tube(b, (0.0, 0.0, 0.26), (0.0, 0.0, 0.39), 0.003, "HJ_Madera", seg=4)
    b.box((0.06, 0.003, 0.035), (0.03, 0.0, 0.355), "HJ_Amarillo")                    # little flag
    return b.build()

def gorro_dormir():   # sleepwalker's nightcap (pyjama stripes, droopy, pompom); origin = centre of the rim on the head, front -Y
    b = Builder("HJ_GorroDormir")
    b.cyl(0.125, 0.12, 0.05, (0, 0, 0.025), "HJ_Blanco", seg=12)
    p = [(0, 0, 0.05), (0.02, 0, 0.14), (0.07, 0, 0.21), (0.15, 0, 0.24), (0.22, 0, 0.2), (0.26, 0, 0.12)]
    r = [0.115, 0.095, 0.075, 0.055, 0.035, 0.02]
    for i in range(5): cone(b, p[i], p[i + 1], r[i], r[i + 1], "HJ_Azul" if i % 2 == 0 else "HJ_Blanco", seg=10)
    for i in range(1, 5): b.ball(r[i], p[i], "HJ_Azul" if i % 2 == 0 else "HJ_Blanco", seg=10)
    b.ball(0.04, p[5], "HJ_Blanco", seg=8)
    return b.build()

def gorro_ducha():   # Anselmo's shower cap (also the room-237 lady's): puffy pink dome, frilly rim, white dots; origin = centre of the rim
    b = Builder("HJ_GorroDucha")
    dome(b, 0.14, (0, 0.01, 0.0), "HJ_Rosa", scale=(1.0, 1.05, 0.8), seg=14, rings=4)
    for i in range(18):
        a = 2 * math.pi * i / 18
        b.ball(0.03, (0.142 * math.cos(a), 0.01 + 0.149 * math.sin(a), 0.01), "HJ_Rosa", scale=(1, 1, 0.6), seg=6)
    for i in range(8):
        a = 2 * math.pi * i / 8 + 0.3
        b.ball(0.016, (0.1 * math.cos(a), 0.01 + 0.105 * math.sin(a), 0.08 + 0.02 * (i % 2)), "HJ_Blanco", scale=(1, 1, 0.5), seg=6)
    return b.build()

def hacha():   # Anselmo's fire axe: origin at the grip, handle along +Z, red head with a steel edge towards -Y
    b = Builder("HJ_Hacha")
    b.cyl(0.022, 0.026, 0.9, (0, 0, 0.3), "HJ_Madera", seg=8)
    b.cyl(0.03, 0.03, 0.2, (0, 0, -0.04), "HJ_Negro", seg=8)
    b.box((0.05, 0.22, 0.12), (0, 0.0, 0.7), "HJ_Rojo")
    prism(b, [(-0.11, 0.64), (-0.25, 0.58), (-0.27, 0.7), (-0.25, 0.82), (-0.11, 0.76)], -0.015, 0.015, "HJ_Rojo", axis='X')
    prism(b, [(-0.25, 0.58), (-0.285, 0.59), (-0.3, 0.7), (-0.285, 0.81), (-0.25, 0.82), (-0.27, 0.7)], -0.01, 0.01, "HJ_Cromo", axis='X')
    b.cyl(0.04, 0.0, 0.18, (0, 0.2, 0.7), "HJ_Rojo", seg=4, rot=[('X', -90)])
    return b.build()

def gafas():   # old man's glasses, lenses that glow in the dark (act 1, blackouts); origin = bridge of the nose, front -Y; 0.16 m
    b = Builder("HJ_Gafas")
    for s in (-1, 1):
        ring(b, (s * 0.04, 0, 0), 0.032, 0.005, "HJ_Marron", axis='Y', n=12, rz=0.026, seg=4)
        b.cyl(0.03, 0.03, 0.004, (s * 0.04, -0.002, 0), "HJ_Emit_Gafas", seg=12, rot=[('X', 90)], scale=(1, 0.85, 1))
        tube(b, (s * 0.072, 0, 0.008), (s * 0.077, 0.13, 0.0), 0.004, "HJ_Marron", seg=4)
    tube(b, (-0.01, 0, 0.012), (0.01, 0, 0.012), 0.004, "HJ_Marron", seg=4)
    return b.build()

def cartel_numero():   # bingo number worn on bingo nights (the card glows; the digits are a label); origin = back of the neck,
    b = Builder("HJ_CartelNumero")   # the card hangs on the chest (-Y)
    b.box((0.24, 0.01, 0.18), (0, -0.15, -0.3), "HJ_Rojo")
    b.box((0.21, 0.01, 0.15), (0, -0.16, -0.3), "HJ_Emit_Numero")
    for s in (-1, 1): path(b, [(s * 0.1, -0.15, -0.22), (s * 0.09, -0.08, -0.05), (s * 0.06, 0.0, 0.0), (s * 0.02, 0.04, 0.0)], 0.004, "HJ_Blanco", seg=4)
    tube(b, (-0.02, 0.04, 0.0), (0.02, 0.04, 0.0), 0.004, "HJ_Blanco", seg=4)
    return b.build()

# ================================================================== figures
def piloto():   # the sleeping pilot (the coffee wakes him): old aviator in leather cap and goggles, snoring in an armchair, facing -Y
    b = Builder("HJ_Piloto")
    b.box((0.8, 0.7, 0.25), (0, 0.15, 0.125), "HJ_Marron")
    b.box((0.8, 0.7, 0.2), (0, 0.15, 0.35), "HJ_Aguacate")
    b.box((0.8, 0.18, 0.75), (0, 0.48, 0.82), "HJ_Aguacate", rot=[('X', -12)])
    for s in (-1, 1): b.box((0.14, 0.7, 0.25), (s * 0.37, 0.15, 0.55), "HJ_Aguacate")
    b.box((0.5, 0.4, 0.35), (0, -0.75, 0.175), "HJ_Aguacate")
    for s in (-1, 1):
        x = s * 0.12
        tube(b, (x, 0.1, 0.55), (x, -0.35, 0.55), 0.085, "HJ_Mostaza", seg=8)
        tube(b, (x, -0.35, 0.55), (x, -0.72, 0.43), 0.07, "HJ_Mostaza", seg=8)
        b.box((0.12, 0.26, 0.12), (x, -0.82, 0.46), "HJ_Negro")
    cone(b, (0, 0.18, 0.5), (0, 0.32, 1.05), 0.21, 0.17, "HJ_Marron", seg=10)
    b.ball(0.21, (0, 0.05, 0.66), "HJ_Marron", seg=10)
    b.box((0.08, 0.03, 0.45), (0.06, -0.12, 0.85), "HJ_Blanco", rot=[('X', -20), ('Y', -10)])
    b.ball(0.04, (0.04, -0.06, 1.02), "HJ_Blanco", seg=6)
    for s in (-1, 1): path(b, [(s * 0.21, 0.3, 1.0), (s * 0.18, 0.0, 0.78), (-s * 0.05, -0.15, 0.72)], 0.055, "HJ_Marron", seg=8)
    for s in (-1, 1): b.ball(0.05, (-s * 0.06, -0.16, 0.72), "HJ_Piel", seg=6)
    head = (0, 0.33, 1.22)
    b.ball(0.14, head, "HJ_Piel", seg=10)
    dome(b, 0.155, (0, 0.35, 1.25), "HJ_Marron", scale=(1, 1, 0.9), seg=12, rings=3)
    for s in (-1, 1):
        b.box((0.04, 0.1, 0.12), (s * 0.14, 0.35, 1.18), "HJ_Marron")
        b.cyl(0.045, 0.045, 0.03, (s * 0.055, 0.25, 1.37), "HJ_Laton", seg=10, rot=[('X', 50)])
        b.cyl(0.035, 0.035, 0.06, (s * 0.055, 0.25, 1.37), "HJ_Cristal", seg=10, rot=[('X', 50)])
        b.ball(0.05, (s * 0.06, 0.2, 1.17), "HJ_Blanco", scale=(1.4, 0.6, 0.6), seg=6)
    b.ball(0.035, (0, 0.18, 1.22), "HJ_Piel", seg=6)
    b.ball(0.03, (0, 0.2, 1.12), "HJ_Oscuro", seg=6)
    return b.build()

def animador():   # the bingo host: gold sequinned jacket, ruffled shirt, red bow tie, black quiff, microphone up, a bingo ball; 1.9 m
    b = Builder("HJ_Animador")
    for s in (-1, 1):
        cone(b, (s * 0.1, 0, 0.08), (s * 0.1, 0, 0.9), 0.1, 0.07, "HJ_Negro", seg=8)
        b.box((0.11, 0.24, 0.08), (s * 0.1, -0.05, 0.04), "HJ_Blanco")
    cone(b, (0, 0, 0.88), (0, 0, 1.42), 0.19, 0.22, "HJ_Oro", seg=10)
    b.box((0.14, 0.04, 0.4), (0, -0.2, 1.2), "HJ_Blanco")
    for i in range(3): b.ball(0.035, (0, -0.225, 1.12 + i * 0.1), "HJ_Blanco", scale=(1.6, 0.6, 0.8), seg=6)
    for s in (-1, 1):
        b.box((0.06, 0.03, 0.3), (s * 0.1, -0.225, 1.27), "HJ_Negro", rot=[('Y', s * 15)])
        b.pyramid(0.08, 0.04, 0.06, (s * 0.05, -0.23, 1.39), "HJ_Rojo")
    b.ball(0.025, (0, -0.24, 1.42), "HJ_Rojo", seg=6)
    b.cyl(0.06, 0.06, 0.1, (0, 0, 1.47), "HJ_Piel", seg=8)
    b.ball(0.13, (0, 0, 1.62), "HJ_Piel", seg=10)
    b.ball(0.12, (0, -0.05, 1.76), "HJ_Negro", scale=(0.95, 1.3, 0.6), seg=10)
    for s in (-1, 1):
        b.box((0.03, 0.05, 0.12), (s * 0.125, 0.0, 1.6), "HJ_Negro")
        b.ball(0.035, (s * 0.045, -0.115, 1.65), "HJ_Marron", scale=(1.2, 0.5, 0.9), seg=6)
    b.box((0.08, 0.02, 0.02), (0, -0.125, 1.56), "HJ_Blanco")
    path(b, [(0.22, 0, 1.38), (0.35, -0.05, 1.6), (0.3, -0.12, 1.82)], 0.05, "HJ_Oro", seg=8)
    b.ball(0.045, (0.3, -0.13, 1.84), "HJ_Piel", seg=6)
    tube(b, (0.3, -0.14, 1.8), (0.28, -0.2, 1.93), 0.018, "HJ_Negro")
    b.ball(0.035, (0.275, -0.205, 1.95), "HJ_Cromo", seg=8)
    path(b, [(-0.22, 0, 1.38), (-0.38, -0.1, 1.2), (-0.45, -0.25, 1.25)], 0.05, "HJ_Oro", seg=8)
    b.ball(0.045, (-0.46, -0.27, 1.26), "HJ_Piel", seg=6)
    b.ball(0.06, (-0.47, -0.3, 1.33), "HJ_Rojo", seg=10)
    b.cyl(0.035, 0.035, 0.125, (-0.47, -0.3, 1.33), "HJ_Blanco", seg=10, rot=[('X', 90)])
    return b.build()

# ================================================================== checks, export, contact sheet
def coplanar_pairs(ob, tol):   # faces of different pieces and materials, same normal, < tol apart and overlapping (they flicker)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    comp, cid = {}, 0
    for f in bm.faces:
        if f.index in comp: continue
        stack = [f]
        comp[f.index] = cid
        while stack:
            g = stack.pop()
            for v in g.verts:
                for h in v.link_faces:
                    if h.index not in comp:
                        comp[h.index] = cid
                        stack.append(h)
        cid += 1
    lo, hi = {}, {}
    for f in bm.faces:
        k = comp[f.index]
        for v in f.verts:
            lo[k] = Vector(tuple(min(a, b) for a, b in zip(lo.get(k, v.co), v.co)))
            hi[k] = Vector(tuple(max(a, b) for a, b in zip(hi.get(k, v.co), v.co)))

    def hidden(k, p):
        return any(j != k and all(lo[j][i] + 1e-4 < p[i] < hi[j][i] - 1e-4 for i in range(3)) for j in lo)

    info, buckets = [], {}
    for f in bm.faces:
        if f.calc_area() < 1e-6: continue
        n = f.normal.normalized()
        c = f.calc_center_median()
        if n.z < -0.99 and abs(c.z) < 0.003: continue
        if hidden(comp[f.index], c + n * 0.004): continue
        key = (round(n.x, 2), round(n.y, 2), round(n.z, 2))
        nk = Vector(key).normalized()
        u = nk.orthogonal().normalized()
        w = nk.cross(u)
        info.append((comp[f.index], f.material_index, n, n.dot(f.verts[0].co), [(v.co.dot(u), v.co.dot(w)) for v in f.verts], tuple(round(x, 2) for x in c)))
        buckets.setdefault((key, int(math.floor(info[-1][3] / tol))), []).append(len(info) - 1)

    def sat(P, Q, eps=0.002):
        for poly in (P, Q):
            for i in range(len(poly)):
                (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % len(poly)]
                ax, ay = y0 - y1, x1 - x0
                L = math.hypot(ax, ay)
                if L < 1e-9: continue
                pa = [(x * ax + y * ay) / L for x, y in P]
                pb = [(x * ax + y * ay) / L for x, y in Q]
                if min(pa) >= max(pb) - eps or min(pb) >= max(pa) - eps: return False
        return True

    out = []
    for (key, k), idx in buckets.items():
        cand = buckets.get((key, k + 1), [])
        for i, a in enumerate(idx):
            for j in idx[i + 1:] + cand:
                A, B = info[a], info[j]
                if A[0] == B[0] or A[1] == B[1] or abs(A[3] - B[3]) >= tol or A[2].dot(B[2]) < 0.999: continue
                if sat(A[4], B[4]): out.append((ob.data.materials[A[1]].name, ob.data.materials[B[1]].name, A[5], B[5]))
    bm.free()
    return out

LAY = {"x": 0.0, "y": 0.0, "row": 0.0}

def make(fn, *args):
    ob = fn(*args)
    res.append(export(ob))
    w, d = ob.dimensions.x, ob.dimensions.y
    if LAY["x"] > 60:
        LAY["x"], LAY["y"], LAY["row"] = 0.0, LAY["y"] - LAY["row"] - 3, 0.0
    ob.location = (LAY["x"] + w / 2, LAY["y"] - d / 2, 0)
    LAY["x"] += w + 2
    LAY["row"] = max(LAY["row"], d)
    all_objs.append(ob.name)

def contact_sheet(names, tile=320, cols=6, rows=5):
    import numpy as np, tempfile
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light, sh.color_type = 'STUDIO', 'MATERIAL'
    sh.show_cavity, sh.cavity_type = True, 'WORLD'
    sh.show_object_outline = True
    sc.display.render_aa = '8'
    sc.view_settings.view_transform = 'Standard'
    sc.world.color = (0.5, 0.55, 0.6)
    sc.render.resolution_x = sc.render.resolution_y = tile
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGB'
    cd = bpy.data.cameras.new("SheetCam")
    cd.type, cd.clip_end = 'ORTHO', 400
    cam = bpy.data.objects.new("SheetCam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    tm = bpy.data.materials.new("SheetText")
    tm.diffuse_color = (0.02, 0.02, 0.03, 1)
    td = bpy.data.curves.new("SheetLabel", 'FONT')
    td.materials.append(tm)
    txt = bpy.data.objects.new("SheetLabel", td)
    sc.collection.objects.link(txt)
    txt.parent = cam
    d = Vector((0.55, -1.0, 0.6)).normalized()
    q = d.to_track_quat('Z', 'Y')
    right, up = q @ Vector((1, 0, 0)), q @ Vector((0, 1, 0))
    tmp = os.path.join(tempfile.gettempdir(), "hj_sheet")
    os.makedirs(tmp, exist_ok=True)
    tiles = []
    for name in names:
        ob = bpy.data.objects[name]
        for o in bpy.data.objects:
            if o.type == 'MESH': o.hide_render = o != ob
        cs = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        ctr = sum(cs, Vector()) / 8
        xs, ys = [(c - ctr).dot(right) for c in cs], [(c - ctr).dot(up) for c in cs]
        S = max(max(xs) - min(xs), (max(ys) - min(ys)) / 0.8) * 1.12
        cam.rotation_euler = q.to_euler()
        cam.location = ctr + right * ((max(xs) + min(xs)) / 2) + up * ((max(ys) + min(ys)) / 2 - 0.08 * S) + d * 150
        cd.ortho_scale = S
        sz = S * 0.042
        td.size = sz
        td.body = "%s\n%.2f x %.2f x %.2f m" % (name, ob.dimensions.x, ob.dimensions.y, ob.dimensions.z)
        txt.location = (-S * 0.47, -S * 0.5 + sz * 1.6, -1)
        p = os.path.join(tmp, name + ".png")
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        tiles.append(p)
    per = cols * rows
    for si in range(0, len(tiles), per):
        chunk = tiles[si:si + per]
        H, W = rows * tile, cols * tile
        canvas = np.full((H, W, 4), 0.2, dtype=np.float32)
        canvas[..., 3] = 1
        for i, p in enumerate(chunk):
            img = bpy.data.images.load(p)
            buf = np.empty(tile * tile * 4, dtype=np.float32)
            img.pixels.foreach_get(buf)
            r, c = divmod(i, cols)
            y0 = H - (r + 1) * tile
            canvas[y0 + 1:y0 + tile - 1, c * tile + 1:(c + 1) * tile - 1] = buf.reshape(tile, tile, 4)[1:-1, 1:-1]
            bpy.data.images.remove(img)
        out = bpy.data.images.new("HotelKitSheet", W, H, alpha=False)
        out.pixels.foreach_set(canvas.ravel())
        out.filepath_raw = os.path.join(RENDERS, "HotelKit_%d.png" % (si // per + 1))
        out.file_format = 'PNG'
        out.save()
        print("SHEET " + out.filepath_raw)

PIECES = [
    (fachada_balcon, "HJ_Fachada_A", "A"), (fachada_balcon, "HJ_Fachada_B", "B"), (fachada_muro,), (fachada_baja,),
    *[(neon_letter, ch) for ch in "HOTELJUBIS"], (neon_tramo,), (neon_coctel,),
    (puerta_giratoria,), (aspas,), (mostrador,), (casillero,), (columna,), (sofa,), (arana,), (telefono,),
    (puerta_ascensor,), (hoja_ascensor,), (cabina,), (botonera,),
    (barandilla_escalera,), (pretil,), (baranda_balcon,),
    (mesa_bufe,), (cafetera,), (encimera,), (tarima,), (barra_salon,), (barra_fondo,), (mesa_redonda,), (bola_espejos,), (marco_foto,),
    (caldera,), (lavadora,), (carro_ropa,), (cuadro_electrico,), (palanca,),
    (cama,), (banera,), (minibar,), (escritorio,), (puerta_habitacion,), (hoja_puerta,), (poste_mojado,), (cinta,), (aplique,),
    (cartel_salida,), (luz_emergencia,), (tolva,), (puerta_servicio,), (hoja_servicio,),
    (seto,), (tumbona,), (sombrilla,), (palmera,), (bar_piscina,), (luz_piscina,),
    (helipuerto,), (helicoptero,), (rotor,), (deposito,), (aire,), (gondola_limpia,), (puerta_atico, False), (puerta_atico, True),
    (objetos_perdidos,), (cafe,), (albornoz,), (pulsera_puertas,), (pulsera_todo,),
    (moto,), (bandeja,), (gorro_dormir,), (gorro_ducha,), (hacha,), (gafas,), (cartel_numero,),
    (piloto,), (animador,),
]

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
clear()
for fn, *a in PIECES: make(fn, *a)
flick = {}
for name in all_objs:
    ob = bpy.data.objects[name]
    pairs = coplanar_pairs(ob, 0.0145 if max(ob.dimensions) > 1.0 else 0.004)
    if pairs: flick[name] = [len(pairs)] + pairs[:40]
print("COPLANAR " + json.dumps(flick))
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("PROPS_RESULT " + json.dumps(res))
if "sheet" in args: contact_sheet(all_objs)
