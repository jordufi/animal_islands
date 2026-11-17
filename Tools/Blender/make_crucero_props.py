# Kit of pieces for the map Crucero "Reina del Imserso" (an old cruise ship full of retirees: act 1 in the port, act 2 at
# sea at night off Benidorm): one FBX per piece in Assets/Maps/Crucero/Models (+ a .blend copy). Run in background:
#   blender.exe -b --factory-startup --python make_crucero_props.py                    (kit + coplanar-face report)
#   blender.exe -b --factory-startup --python make_crucero_props.py -- sheet           (also renders Renders/CruceroKit_*.png)
#   ... -- sheet only=CR_Proa,CR_Popa out=<folder> per=4                               (bigger look at a few pieces)
# Same conventions and the same Builder as make_venecia_props.py (its helper section is reused as is): origin = centre of
# the footprint on the ground, front = Blender -Y (= Unity +Z), up = +Z, 1 cell = 2 x 2 m, Unity (x, y, z) = Blender
# (-x, z, -y). A deck storey is 3.6 m of room plus a 0.4 m slab (4 m): floor 0 = the quay and the cabin deck inside the
# hull, the main deck at 4 m, the sun deck at 8 m. The sea is at -0.8 m (WL), the keel at -3 m.
# Ship edge pieces (CR_Casco_*, CR_Proa, CR_Popa, CR_Borda*, CR_BotePescantes, CR_Pescante, CR_Garfio*, CR_Escalerilla):
# origin on the deck edge line (y = 0) at deck level, -Y = OUTBOARD (the sea). Hull strips hang on the hull cell's face
# from the keel to the main deck (z -3 .. 4 above floor 0). Rails stand on the deck edge.
# Wall pieces (CR_Fachada_*, CR_PuenteVentana, CR_PuertaCamarote_*, CR_Pasillo): 2 m wide, hang on the wall plane y = 0
# with everything sticking out towards -Y, one room tall (3.6 m). Pieces that tile (hull strips, rails, wall strips,
# counters, belts) are 2 m wide (CR_Borda_45 = 2.83 m for the 45-degree bow edges).
# Hanging pieces (CR_Lampara) have the origin at the ceiling hook. Worn accessories say where their origin is.
# Materials: M_* = the island's textured materials (remapped on import), CR_*Paint = recoloured per instance (Repaint),
# CR_Emit_<Name> = everything that glows, one slot per kind of light so the light kit can switch each group on and off:
# the FBX keeps a dim "off" colour, EMIT below is the colour when it is on (the contact sheet shows them on).
# Faces: no two faces of different pieces in one plane (they flicker when the camera moves): pieces sit >= 1.5 cm apart
# (a few mm on hand-sized accessories); the coplanar check at the end reports any pair left.
import os, sys, math, json, random

_here = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(_here, "make_venecia_props.py"), encoding="utf-8").read().split("# ================================================================== facades")[0])

OUT = r"C:\Users\Usuario\animal_island1\Assets\Maps\Crucero\Models"
BLEND = r"C:\Users\Usuario\Documents\BeachAnimals\Crucero\Crucero_Props.blend"
RENDERS = r"C:\Users\Usuario\animal_island1\Renders"
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

WL, KEEL, DECK, ROOM = -0.8, -3.0, 4.0, 3.6

COLORS.update({
    "M_Leaves": (0.2, 0.42, 0.18), "M_Bark": (0.42, 0.32, 0.2), "M_Canvas": (0.8, 0.74, 0.6),
    "CR_Blanco": (0.92, 0.92, 0.9), "CR_Azul": (0.07, 0.13, 0.3), "CR_Antifouling": (0.55, 0.12, 0.1), "CR_Oxido": (0.48, 0.24, 0.1),
    "CR_Acero": (0.55, 0.57, 0.6), "CR_Gris": (0.33, 0.35, 0.38), "CR_Cromo": (0.82, 0.84, 0.87), "CR_Amarillo": (0.95, 0.75, 0.12),
    "CR_Naranja": (0.95, 0.42, 0.08), "CR_Rojo": (0.75, 0.1, 0.1), "CR_Negro": (0.05, 0.05, 0.06), "CR_Dorado": (0.95, 0.75, 0.2),
    "CR_Laton": (0.78, 0.6, 0.25), "CR_Cristal": (0.13, 0.19, 0.24), "CR_VidrioClaro": (0.6, 0.78, 0.85), "CR_Verde": (0.06, 0.36, 0.17),
    "CR_Terciopelo": (0.5, 0.06, 0.1), "CR_Madera": (0.36, 0.22, 0.12), "CR_Azulejo": (0.18, 0.5, 0.72), "CR_AguaPiscina": (0.22, 0.7, 0.85),
    "CR_Goma": (0.08, 0.08, 0.08), "CR_Cuerda": (0.75, 0.65, 0.45), "CR_Hormigon": (0.62, 0.6, 0.56), "CR_Crema": (0.88, 0.82, 0.7),
    "CR_Rosa": (0.95, 0.5, 0.65), "CR_Celeste": (0.5, 0.75, 0.9), "CR_Paja": (0.85, 0.72, 0.35), "CR_Hoja": (0.18, 0.42, 0.2),
    "CR_VerdeMaquina": (0.3, 0.42, 0.35), "CR_Carne": (0.9, 0.55, 0.55), "CR_Morado": (0.45, 0.2, 0.55), "CR_Marron": (0.45, 0.3, 0.18),
    "CR_Silueta": (0.3, 0.32, 0.38), "CR_SiluetaLejos": (0.42, 0.45, 0.52), "CR_Monte": (0.38, 0.4, 0.3),
    "CR_ContainerPaint": (0.2, 0.4, 0.65), "CR_Container2Paint": (0.7, 0.22, 0.15), "CR_DoorPaint": (0.55, 0.36, 0.2),
    "CR_AwningPaint": (0.15, 0.4, 0.75), "CR_TowelPaint": (0.9, 0.3, 0.4), "CR_SlidePaint": (0.95, 0.72, 0.1), "CR_Slide2Paint": (0.2, 0.45, 0.85),
    "CR_LaunchPaint": (0.9, 0.9, 0.88), "CR_JetskiPaint": (0.9, 0.2, 0.2), "CR_NoodlePaint": (0.95, 0.4, 0.6), "CR_FloatPaint": (0.95, 0.85, 0.2),
    "CR_CapPaint": (0.95, 0.55, 0.75), "CR_Label": (0.02, 0.02, 0.03),
})
EMIT = {   # the colour of each light when it is on (the FBX keeps a dim version of it: the light kit switches them)
    "CR_Emit_OjoBuey": (1.0, 0.8, 0.45), "CR_Emit_Ventana": (1.0, 0.78, 0.45), "CR_Emit_Puente": (0.45, 0.95, 0.65),
    "CR_Emit_Guirnalda": (1.0, 0.9, 0.6), "CR_Emit_Chimenea": (1.0, 0.95, 0.8), "CR_Emit_Piscina": (0.3, 0.9, 1.0),
    "CR_Emit_Foco": (1.0, 0.98, 0.85), "CR_Emit_FocoCono": (1.0, 0.95, 0.75), "CR_Emit_Skyline": (1.0, 0.85, 0.5),
    "CR_Emit_Neon": (1.0, 0.25, 0.7), "CR_Emit_Ciudad": (1.0, 0.8, 0.5), "CR_Emit_FaroPuerto": (1.0, 0.95, 0.7),
    "CR_Emit_Farol": (1.0, 0.82, 0.5), "CR_Emit_Lampara": (1.0, 0.88, 0.6), "CR_Emit_Tragaperras": (1.0, 0.6, 0.2),
    "CR_Emit_Escenario": (1.0, 0.85, 0.55), "CR_Emit_Consola": (0.3, 1.0, 0.5), "CR_Emit_Cuadro": (1.0, 0.3, 0.1),
    "CR_Emit_Mastil": (1.0, 1.0, 0.9), "CR_Emit_Salida": (0.15, 1.0, 0.35),
}
for k, v in EMIT.items(): COLORS[k] = tuple(c * 0.3 for c in v)

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

# ================================================================== builder with extra shapes + coplanar check
CHECKS = {}

class CB(Builder):
    def __init__(self, name, joint=None, wall=False, ceil=None, tol=0.0145):
        super().__init__(name)
        self.pl = self.bm.faces.layers.int.new("part")
        self.part, self.joint, self.wall, self.ceil, self.tol = 0, joint, wall, ceil, tol
        self.recalc = True

    def _finish(self, verts, m, rot=None, at=(0, 0, 0)):
        Builder._finish(self, verts, m, rot, at)
        self.part += 1
        for f in {f for v in verts for f in v.link_faces}: f[self.pl] = self.part

    def mark(self):   # the verts that exist now (bmesh reuses freed slots, so new verts are not always last)
        return set(self.bm.verts)

    def move(self, before, rot=None, at=(0, 0, 0), scale=None):   # transform everything built since mark()
        vs = [v for v in self.bm.verts if v not in before]
        if scale: bmesh.ops.scale(self.bm, vec=Vector(scale), verts=vs)
        for axis, deg in (rot or []):
            bmesh.ops.rotate(self.bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(deg), 3, axis), verts=vs)
        bmesh.ops.translate(self.bm, vec=Vector(at), verts=vs)

    def poly(self, pts, z0, z1, m, rot=None, at=(0, 0, 0)):   # prism of a 2D outline (XY), from z0 to z1
        bm = self.bm
        lo = [bm.verts.new((x, y, z0)) for x, y in pts]
        hi = [bm.verts.new((x, y, z1)) for x, y in pts]
        bm.faces.new(lo[::-1]); bm.faces.new(hi)
        k = len(pts)
        for i in range(k): bm.faces.new([lo[i], lo[(i + 1) % k], hi[(i + 1) % k], hi[i]])
        self._finish(lo + hi, m, rot, at)

    def wpoly(self, pts, ya, yb, m, at=(0, 0, 0)):   # outline in the XZ plane (x, z), from y = ya to y = yb (ya < yb)
        self.poly(pts, -yb, -ya, m, rot=[('X', 90)], at=at)

    def ring(self, R, r, at, m, seg=16, sides=6, rot=None, arc=1.0, phase=0.0, scale=None, mfn=None):   # torus in XY
        bm = self.bm
        closed = arc >= 0.999
        n = seg if closed else seg + 1
        rings = []
        for i in range(n):
            a = 2 * math.pi * arc * i / seg
            d = Vector((math.cos(a), math.sin(a), 0))
            rings.append([bm.verts.new(d * (R + r * math.cos(t)) + Vector((0, 0, r * math.sin(t))))
                          for t in [phase + 2 * math.pi * j / sides for j in range(sides)]])
        faces = []
        for i in range(seg):
            r0, r1 = rings[i], rings[(i + 1) % n]
            for j in range(sides): faces.append((i, bm.faces.new([r0[j], r1[j], r1[(j + 1) % sides], r0[(j + 1) % sides]])))
        if not closed:
            bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
        vs = [v for rr in rings for v in rr]
        if scale: bmesh.ops.scale(bm, vec=Vector(scale), verts=vs)
        self._finish(vs, m, rot, at)
        if mfn:
            for i, f in faces: f.material_index = self._mi(mfn(i))

    def tube(self, pts, r, m, sides=8, caps=True, cap_m=None, mfn=None):   # tube along a polyline (parallel transport)
        bm = self.bm
        P = [Vector(p) for p in pts]
        rads = list(r) if isinstance(r, (list, tuple)) else [r] * len(P)
        rings, nrm = [], None
        for i, p in enumerate(P):
            if i == 0: t = P[1] - P[0]
            elif i == len(P) - 1: t = P[-1] - P[-2]
            else: t = (P[i + 1] - P[i]).normalized() + (P[i] - P[i - 1]).normalized()
            t.normalize()
            if nrm is None:
                up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
                nrm = t.cross(up).normalized()
            else:
                nrm = (nrm - t * nrm.dot(t)).normalized()
            bi = t.cross(nrm)
            rings.append([bm.verts.new(p + (nrm * math.cos(a) + bi * math.sin(a)) * rads[i]) for a in [2 * math.pi * j / sides for j in range(sides)]])
        segf = []
        for i in range(len(rings) - 1):
            for j in range(sides):
                segf.append((i, bm.faces.new([rings[i][j], rings[i + 1][j], rings[i + 1][(j + 1) % sides], rings[i][(j + 1) % sides]])))
        capf = [bm.faces.new(rings[0][::-1]), bm.faces.new(rings[-1])] if caps else []
        self._finish([v for rr in rings for v in rr], m)
        if mfn:
            for i, f in segf: f.material_index = self._mi(mfn(i))
        if cap_m:
            for f in capf: f.material_index = self._mi(cap_m)

    def loft(self, sections, mfn, caps=True):   # closed loops of equal length -> skin; mfn(centre, normal) picks the material
        bm = self.bm
        rings = [[bm.verts.new(p) for p in s] for s in sections]
        k = len(sections[0])
        faces = []
        for a, c in zip(rings, rings[1:]):
            for i in range(k): faces.append(bm.faces.new([a[i], a[(i + 1) % k], c[(i + 1) % k], c[i]]))
        if caps: faces += [bm.faces.new(rings[0][::-1]), bm.faces.new(rings[-1])]
        self._finish([v for r in rings for v in r], mfn(None, None))
        for f in faces:
            f.normal_update()
            f.material_index = self._mi(mfn(f.calc_center_median(), f.normal))

    def text(self, s, size, at, m, depth=0.03, rot=(('X', 90),), align='CENTER'):   # default: reads along +X, faces -Y
        cu = bpy.data.curves.new("txt", 'FONT')
        cu.body, cu.size, cu.extrude, cu.resolution_u = s, size, depth / 2, 2
        cu.align_x, cu.align_y = align, 'CENTER'
        ob = bpy.data.objects.new("txt", cu)
        bpy.context.scene.collection.objects.link(ob)
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        tmp = bmesh.new(); tmp.from_mesh(me)
        bmesh.ops.remove_doubles(tmp, verts=tmp.verts[:], dist=1e-5)   # weld caps and walls so the normals can be recalculated
        tmp.to_mesh(me); tmp.free()
        before = set(self.bm.verts)
        self.bm.from_mesh(me)
        self.pl = self.bm.faces.layers.int.get("part") or self.bm.faces.layers.int.new("part")
        vs = [v for v in self.bm.verts if v not in before]
        bpy.data.objects.remove(ob); bpy.data.curves.remove(cu); bpy.data.meshes.remove(me)
        self._finish(vs, m, list(rot) if rot else None, at)

    def build(self):
        bm = self.bm
        if self.recalc: bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        CHECKS[self.name] = coplanar_pairs(self)
        bm.faces.layers.int.remove(self.pl)
        for l in list(bm.loops.layers.uv): bm.loops.layers.uv.remove(l)
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

def _hidden(b, n, c):   # faces nobody sees: on the floor, against the wall, against the ceiling, at a tiling joint
    if n.z < -0.99 and abs(c.z) < 0.004: return True
    if b.wall and n.y > 0.99 and abs(c.y) < 0.004: return True
    if b.ceil is not None and n.z > 0.99 and abs(c.z - b.ceil) < 0.004: return True
    if b.joint is not None and abs(n.x) > 0.99 and abs(abs(c.x) - b.joint) < 0.004: return True
    return False

def _overlap(n, A, B, tol=0.003):
    u = n.orthogonal().normalized(); v = n.cross(u)
    a = [(p.dot(u), p.dot(v)) for p in A]; b2 = [(p.dot(u), p.dot(v)) for p in B]
    if min(max(x for x, _ in a), max(x for x, _ in b2)) - max(min(x for x, _ in a), min(x for x, _ in b2)) < tol: return False
    if min(max(y for _, y in a), max(y for _, y in b2)) - max(min(y for _, y in a), min(y for _, y in b2)) < tol: return False
    for poly in (a, b2):
        for i in range(len(poly)):
            (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % len(poly)]
            ax, ay = y1 - y0, x0 - x1
            ln = math.hypot(ax, ay)
            if ln < 1e-9: continue
            ax, ay = ax / ln, ay / ln
            pa = [x * ax + y * ay for x, y in a]; pb = [x * ax + y * ay for x, y in b2]
            if min(max(pa), max(pb)) - max(min(pa), min(pb)) < tol: return False
    return True

def coplanar_pairs(b):
    """Pairs of faces of different parts that face the same way, lie within b.tol of one plane and overlap."""
    tb = b.bm.copy()
    pl = tb.faces.layers.int.get("part")
    bmesh.ops.triangulate(tb, faces=tb.faces[:])
    tb.normal_update()
    buckets = {}
    for f in tb.faces:
        n = f.normal.copy()
        if n.length < 0.5 or f.calc_area() < 1e-7: continue
        c = f.calc_center_median()
        if _hidden(b, n, c): continue
        key = (round(n.x * 20), round(n.y * 20), round(n.z * 20))
        buckets.setdefault(key, []).append((n.dot(f.verts[0].co), f[pl], n, [v.co.copy() for v in f.verts], c.copy(), f.material_index))
    hits, seen = [], set()
    for lst in buckets.values():
        lst.sort(key=lambda t: t[0])
        for i in range(len(lst)):
            da, pa, na, A, ca, ma = lst[i]
            for j in range(i + 1, len(lst)):
                db, pb, nb, B, cb, mb = lst[j]
                if db - da > b.tol: break
                if pa == pb or ma == mb or (pa, pb) in seen or na.dot(nb) < 0.9995: continue   # same material: same shading
                if _overlap(na, A, B):
                    seen.add((pa, pb))
                    hits.append("%s/%s at (%.2f, %.2f, %.2f) n(%.0f, %.0f, %.0f) parts %d/%d" % (b.mats[ma], b.mats[mb], ca.x, ca.y, ca.z, na.x, na.y, na.z, pa, pb))
    tb.free()
    return hits

def rrect(w, h, r, n=3, cx=0.0, cy=0.0):   # rounded rectangle outline, counter-clockwise
    pts = []
    for sx, sy, a0 in ((1, -1, -90), (1, 1, 0), (-1, 1, 90), (-1, -1, 180)):
        ccx, ccy = cx + sx * (w / 2 - r), cy + sy * (h / 2 - r)
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((ccx + r * math.cos(a), ccy + r * math.sin(a)))
    return pts

def catmull(pts, n=4):
    P = [Vector(p) for p in pts]
    out = []
    for i in range(len(P) - 1):
        p0 = P[i - 1] if i > 0 else P[i] * 2 - P[i + 1]
        p1, p2 = P[i], P[i + 1]
        p3 = P[i + 2] if i + 2 < len(P) else P[i + 1] * 2 - P[i]
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-1])
    return out

def streak(b, x, ztop, length, y0=-0.12, w=0.045):   # rust running down from a hole: a thin tapered wedge on the plate
    b.cyl(0.012, w, length, (x, y0, ztop - length / 2), "CR_Oxido", seg=4, scale=(1, 0.25, 1))

def porthole(b, x, z, r=0.2, y0=-0.12, eyebrow=True):   # brass ring + glowing glass, on a face at y = y0
    b.disc(r + 0.07, 0.06, (x, y0 - 0.03, z), "CR_Laton", seg=12)
    b.disc(r, 0.06, (x, y0 - 0.05, z), "CR_Emit_OjoBuey", seg=12)
    if eyebrow: b.box((2 * r + 0.2, 0.12, 0.04), (x, y0 - 0.06, z + r + 0.12), "CR_Blanco")

# ================================================================== the ship: hull, bow, stern, rails
def hull_bands(b, x0=-1.0, x1=1.0, top=3.85):
    w, cx = x1 - x0, (x0 + x1) / 2
    for z0, z1, m in ((KEEL, -0.95, "CR_Antifouling"), (-0.95, 0.7, "CR_Azul"), (0.7, top, "CR_Blanco")):
        b.box((w, 0.12, z1 - z0), (cx, -0.06, (z0 + z1) / 2), m)
    b.box((w, 0.09, 0.08), (cx, -0.095, 0.77), "CR_Dorado")                       # gold pinstripe, 2 cm proud
    b.box((w, 0.2, DECK - top), (cx, -0.1, (top + DECK) / 2), "CR_Azul")          # rubbing strake under the deck edge

def casco_a():   # hull strip with two portholes of the cabin deck
    b = CB("CR_Casco_A", joint=1.0, wall=True)
    hull_bands(b)
    for x in (-0.5, 0.5): porthole(b, x, 1.7)
    streak(b, 0.5, 1.46, 1.0)
    return b.build()

def casco_b():   # one porthole, a welded patch with rivets, a scupper weeping rust
    b = CB("CR_Casco_B", joint=1.0, wall=True)
    hull_bands(b)
    porthole(b, -0.45, 1.7)
    b.box((0.7, 0.025, 0.6), (0.4, -0.1325, 1.45), "CR_Blanco")                  # patch plate, 2.5 cm proud
    for i in range(4):
        for z in (1.2, 1.7): b.ball(0.022, (0.12 + i * 0.185, -0.145, z), "CR_Acero", seg=6)
    streak(b, 0.6, 1.2, 0.9, y0=-0.145)
    b.cyl(0.09, 0.09, 0.1, (0.6, -0.15, 3.45), "CR_Acero", seg=10, rot=[('X', 90)])
    b.cyl(0.06, 0.06, 0.14, (0.6, -0.15, 3.45), "CR_Negro", seg=10, rot=[('X', 90)])
    streak(b, 0.6, 3.36, 2.4, w=0.06)
    return b.build()

def casco_c():   # the pilot door in the hull, black fender scuffs
    b = CB("CR_Casco_C", joint=1.0, wall=True)
    hull_bands(b)
    b.box((1.3, 0.025, 2.0), (-0.2, -0.1325, 1.85), "CR_Blanco")                 # frame
    b.box((1.1, 0.025, 1.8), (-0.2, -0.1525, 1.85), "CR_Acero")                  # door leaf, 2 cm in front of the frame
    for z in (1.2, 1.85, 2.5): b.cyl(0.03, 0.03, 0.14, (-0.78, -0.17, z), "CR_Gris", seg=6)   # hinges
    b.box((0.3, 0.05, 0.05), (0.2, -0.19, 1.7), "CR_Gris")                       # handle
    b.text("PRÁCTICO", 0.12, (-0.2, -0.135, 3.1), "CR_Azul", depth=0.03)
    for x, z, w in ((0.55, 0.25, 0.5), (-0.5, 0.1, 0.7), (0.1, 0.45, 0.3)): b.box((w, 0.015, 0.08), (x, -0.1275, z), "CR_Negro")
    streak(b, 0.75, 3.4, 1.6)
    return b.build()

def proa():
    """Bow shell for the 6 tapering columns (12 m long, 24 m wide at the joint), tip towards -Y (the bow is east in the
    layout). It wraps the stepped cells from the keel to the main deck and covers the half cells outside the steps with a
    deck plate 3 cm under the deck; anchors, hawse pipes and the ship's name on both sides."""
    b = CB("CR_Proa")
    HW = 12.12
    S = [(HW, 6.0), (HW, 4.0), (HW - 2, 2.0), (HW - 4, 0.0), (HW - 6, -2.0), (HW - 8, -4.0), (HW - 10, -6.0), (1.1, -6.7)]
    loop = S + [(0.0, -7.0)] + [(-x, y) for x, y in reversed(S)]
    def level(z, k):   # k = how much the underwater body draws in (stem rake)
        out = []
        for x, y in loop:
            u = min(max((4.0 - y) / 11.0, 0.0), 1.0)
            ax = max(abs(x) - 1.0 * u * k, 0.04 if abs(x) > 0 else 0.0)
            out.append(Vector((math.copysign(ax, x) if x else 0.0, y + 1.8 * u * k, z)))
        return out
    secs = [level(KEEL, 1.0), level(-0.95, 0.35), level(0.7, 0.0), level(3.85, 0.0), level(3.97, 0.0)]
    def pick(c, n):
        if c is None: return "CR_Blanco"
        if abs(n.z) > 0.9: return "M_Wood" if c.z > 0 else "CR_Antifouling"
        return "CR_Antifouling" if c.z < -0.95 else "CR_Azul" if c.z < 0.7 else "CR_Blanco" if c.z < 3.85 else "CR_Azul"
    b.loft(secs, pick)
    for s in (1, -1):   # each side: name, anchor, hawse pipe, rust
        n0 = b.mark()
        b.text("REINA DEL IMSERSO", 0.72, (0, -0.02, 0), "CR_Azul", depth=0.06)
        b.move(n0, rot=[('Z', 45 * s)], at=(s * (HW - 3.5), 0.5, 2.55))
        n0 = b.mark()
        b.cyl(0.3, 0.3, 0.16, (0, -0.06, 1.05), "CR_Acero", seg=10, rot=[('X', 90)])
        b.cyl(0.2, 0.2, 0.12, (0, -0.11, 1.05), "CR_Negro", seg=10, rot=[('X', 90)])
        b.box((0.16, 0.12, 1.3), (0, -0.12, 0.05), "CR_Negro")                   # shank
        b.box((0.55, 0.18, 0.28), (0, -0.13, -0.66), "CR_Negro")                 # crown
        for d in (-1, 1): b.box((0.2, 0.14, 0.6), (d * 0.34, -0.13, -0.42), "CR_Negro", rot=[('Y', d * 25)])   # flukes
        streak(b, 0.12, 0.75, 1.1, y0=0.0)
        b.move(n0, rot=[('Z', 45 * s)], at=(s * (HW - 7.5), -3.5, 2.25))
    return b.build()

def popa():
    """Stern transom (24.24 m wide, keel to main deck) with the open marina of the cabin deck (16 m wide, 3 m tall), its
    gate lowered as a swim platform, the name and port of registry, and the ensign staff. Front -Y = astern (outboard)."""
    b = CB("CR_Popa")
    HW, O, OT = 12.12, 8.0, 3.0
    def bands(x0, x1, z0, z1):
        for a, c, m in ((KEEL, -0.95, "CR_Antifouling"), (-0.95, 0.7, "CR_Azul"), (0.7, 3.85, "CR_Blanco"), (3.85, DECK, "CR_Azul")):
            lo, hi = max(a, z0), min(c, z1)
            if hi > lo + 1e-4: b.box((x1 - x0, 0.12, hi - lo), ((x0 + x1) / 2, -0.06, (lo + hi) / 2), m)
    bands(-HW, -O, KEEL, DECK); bands(O, HW, KEEL, DECK); bands(-O, O, KEEL, 0.0); bands(-O, O, OT, DECK)
    b.text("REINA DEL IMSERSO", 0.42, (0, -0.135, 3.56), "CR_Azul")
    b.text("BENIDORM", 0.22, (0, -0.135, 3.2), "CR_Azul")
    b.box((16.0, 2.5, 0.15), (0, -1.37, -0.275), "CR_Acero")                       # the marina gate, lowered
    b.box((15.96, 0.14, 0.195), (0, -2.57, -0.2775), "CR_Amarillo")                # its yellow edge
    for x in (-7.6, 7.6): b.tube([(x, -2.45, -0.2), (x, -0.2, OT + 0.15)], 0.025, "CR_Gris", sides=6)
    for x in (-11.0, 11.0):                                                        # stern fairleads + rust
        b.box((0.7, 0.2, 0.35), (x, -0.22, 3.2), "CR_Gris")
        streak(b, x, 3.0, 1.4)
    b.cyl(0.045, 0.045, 3.4, (0, -0.4, 5.3), "M_Wood", seg=6)                      # ensign staff
    b.box((0.12, 0.3, 0.12), (0, -0.25, 3.7), "CR_Gris")
    for i, m in enumerate(("CR_Rojo", "CR_Amarillo", "CR_Rojo")):
        b.box((1.3, 0.02, (0.2, 0.4, 0.2)[i]), (-0.7, -0.4, (6.8, 6.5, 6.2)[i]), m)
    return b.build()

def borda(name, L=2.0, life_ring=False):
    """Ship's rail along X, 1.1 m: teak handrail, three bars, a toe board; posts at +-0.5 m (adjacent pieces never put two
    posts in one place). -Y = outboard; the life ring hangs on the deck side."""
    b = CB(name, joint=L / 2)
    b.box((L, 0.14, 0.08), (0, 0, 1.06), "M_Wood")
    for z in (0.3, 0.55, 0.8): b.cyl(0.02, 0.02, L, (0, 0, z), "CR_Blanco", seg=6, rot=[('Y', 90)])
    b.box((L, 0.025, 0.14), (0, 0, 0.07), "CR_Blanco")
    for x in ((-0.5, 0.5) if L < 2.5 else (-0.7, 0.7)): b.box((0.06, 0.06, 1.02), (x, 0, 0.51), "CR_Blanco")
    if life_ring:
        b.ring(0.3, 0.07, (0, 0.13, 0.62), "CR_Naranja", seg=16, sides=6, rot=[('X', 90)],
               mfn=lambda i: "CR_Blanco" if i % 4 == 0 else "CR_Naranja")
        b.box((0.08, 0.1, 0.12), (0, 0.06, 0.95), "CR_Gris")
    return b.build()

# ================================================================== lifeboats, funnel, bridge
def lifeboat(b, at=(0, 0, 0), name="REINA DEL IMSERSO 1"):   # enclosed lifeboat 7.4 x 2.5 x 2.6 along X, keel at z 0
    n0 = b.mark()
    half = [(0.0, 0.0), (0.4, 0.08), (0.78, 0.35), (0.95, 0.75), (1.0, 1.05), (0.96, 1.55), (0.78, 2.0), (0.45, 2.3), (0.0, 2.4)]
    secs = []
    for i in range(13):
        t = -1 + 2 * i / 12
        x = 3.7 * t
        w = max(1.25 * (1 - t ** 4) ** 0.5, 0.12)
        lift, zs = 0.25 * t * t, 1 - 0.25 * t ** 6
        loop = [(w * a, z) for a, z in half] + [(-w * a, z) for a, z in reversed(half[1:-1])]
        secs.append([Vector((x, y, z * zs + lift)) for y, z in loop])
    b.loft(secs, lambda c, n: "CR_Naranja" if c is None or c.z > 1.08 + 0.25 * (c.x / 3.7) ** 2 else "CR_Blanco")
    b.tube([(3.7 * t, -(max(1.25 * (1 - t ** 4) ** 0.5, 0.12) + 0.02), 1.05 + 0.25 * t * t) for t in [-0.95 + 0.19 * k for k in range(11)]], 0.06, "CR_Azul", sides=6)
    b.tube([(3.7 * t, (max(1.25 * (1 - t ** 4) ** 0.5, 0.12) + 0.02), 1.05 + 0.25 * t * t) for t in [-0.95 + 0.19 * k for k in range(11)]], 0.06, "CR_Azul", sides=6)
    for s in (-1, 1):
        for x in (-2.4, -1.2, 0.0, 1.2, 2.4):
            t = x / 3.7
            w = 1.25 * (1 - t ** 4) ** 0.5
            b.box((0.4, 0.12, 0.22), (x, s * w * 0.97, 1.55 + 0.25 * t * t), "CR_Cristal")
    b.box((0.9, 0.7, 0.16), (0, 0, 2.4), "CR_Naranja")                                         # hatch
    for x in (-2.6, 2.6): b.ring(0.08, 0.02, (x, 0, 2.5 + 0.25 * (x / 3.7) ** 2), "CR_Gris", seg=8, sides=4, rot=[('X', 90)])
    b.text(name, 0.17, (0.6, -1.235, 0.82), "CR_Azul", depth=0.04)
    b.text(name, 0.17, (-0.6, 1.235, 0.82), "CR_Azul", depth=0.04, rot=(('X', 90), ('Z', 180)))
    b.box((0.08, 0.5, 0.6), (3.72, 0, 0.2), "CR_Naranja")                                      # rudder
    b.move(n0, at=at)

def bote_salvavidas():   # the escape boat on the bridge wing (lowered by the crank at the end)
    b = CB("CR_BoteSalvavidas")
    lifeboat(b)
    return b.build()

def bote_pescantes():
    """Decoration along the covered promenade: a lifeboat hanging outboard of the rail from two beams under the sun deck
    (ceiling 3.6 m). Origin on the deck edge, -Y = outboard; nothing stands on the deck."""
    b = CB("CR_BotePescantes")
    lifeboat(b, at=(0, -1.8, 0.15), name="REINA DEL IMSERSO 3")
    for x in (-2.6, 2.6):
        b.box((0.25, 2.9, 0.25), (x, -0.95, 3.42), "CR_Blanco")
        b.box((0.4, 0.5, 0.05), (x, 0.25, 3.57), "CR_Gris")                        # plate under the slab
        b.wpoly([(-0.12, 3.29), (0.12, 3.29), (0.12, 3.0)], 0.06, 0.45, "CR_Blanco", at=(x, 0, 0))   # knee
        b.box((0.3, 0.3, 0.2), (x, -2.2, 3.2), "CR_Gris")                          # block
        b.tube([(x, -2.2, 3.1), (x, -1.8, 2.75)], 0.02, "CR_Negro", sides=5)
    return b.build()

def pescante():   # one standing davit (two hold the escape boat beside the bridge wing); base on deck, arm reaching to -Y
    b = CB("CR_Pescante")
    b.box((0.7, 0.7, 0.5), (0, 0.45, 0.25), "CR_Blanco")
    b.cyl(0.2, 0.2, 0.55, (0, 0.45, 0.72), "CR_Gris", seg=10, rot=[('Y', 90)])
    b.ring(0.18, 0.025, (0.33, 0.45, 0.72), "CR_Rojo", seg=10, sides=4, rot=[('Y', 90)])
    b.tube(catmull([(0, 0.45, 0.5), (0, 0.45, 2.1), (0, 0.15, 3.0), (0, -0.6, 3.45), (0, -1.5, 3.55), (0, -2.0, 3.45)], 3), 0.1, "CR_Blanco", sides=8)
    b.cyl(0.16, 0.16, 0.12, (0, -2.0, 3.28), "CR_Gris", seg=10, rot=[('Y', 90)])
    b.tube([(0, -2.0, 3.15), (0, -2.0, 1.5)], 0.015, "CR_Negro", sides=5)
    b.ring(0.07, 0.02, (0, -2.0, 1.42), "CR_Gris", seg=8, sides=4, rot=[('Y', 90)])
    return b.build()

def chimenea():
    """The funnel over the 4 x 4-cell block of the sun deck: an 8.1 x 8.1 x 3.8 m casing (walls under it must stay below
    3.7 m), an oval stack raked aft (+Y) to 12.3 m with the company band and emblem, the horn, four floodlights."""
    b = CB("CR_Chimenea")
    b.box((8.1, 8.1, 3.8), (0, 0, 1.9), "CR_Blanco")
    for s in (-1, 1):
        for d in (-1.8, 1.8):
            b.box((2.0, 0.04, 1.2), (d, s * 4.07, 2.0), "CR_Negro")
            b.box((0.04, 2.0, 1.2), (s * 4.07, d, 2.0), "CR_Negro")
            for z in (1.55, 1.85, 2.15, 2.45):
                b.box((1.96, 0.06, 0.06), (d, s * 4.11, z), "CR_Blanco", rot=[('X', 30)])
                b.box((0.06, 1.96, 0.06), (s * 4.11, d, z), "CR_Blanco", rot=[('Y', 30)])
    def oval(z):
        t = (z - 3.8) / 8.5
        a, bb, off = 2.2 - 0.3 * t, 3.4 - 0.4 * t, 1.6 * t
        return [Vector((a * math.cos(2 * math.pi * i / 16), off + bb * math.sin(2 * math.pi * i / 16), z)) for i in range(16)]
    zs = [3.8, 8.4, 9.3, 10.2, 11.4, 12.3]
    b.loft([oval(z) for z in zs], lambda c, n: "CR_Blanco" if c is None else ("CR_Negro" if abs(n.z) > 0.9 and c.z > 5 else
           "CR_Blanco" if c.z < 8.4 else "CR_Azul" if c.z < 10.2 else "CR_Blanco" if c.z < 11.4 else "CR_Negro"))
    off = 1.6 * (9.3 - 3.8) / 8.5
    a = 2.2 - 0.3 * (9.3 - 3.8) / 8.5
    for s in (-1, 1):
        b.cyl(0.75, 0.75, 0.12, (s * (a + 0.01), off, 9.3), "CR_Dorado", seg=16, rot=[('Y', 90)])
        b.text("R", 1.0, (s * (a + 0.09), off, 9.3), "CR_Azul", depth=0.06, rot=(('X', 90), ('Z', 90 * s)))
    for x in (-0.7, 0.7): b.cyl(0.3, 0.3, 1.0, (x, 1.6 + 0.3, 12.7), "CR_Negro", seg=8)
    bz = 10.8
    oz, bbz = 1.6 * (bz - 3.8) / 8.5, 3.4 - 0.4 * (bz - 3.8) / 8.5
    b.cyl(0.08, 0.26, 0.9, (0, oz - bbz - 0.45, bz), "CR_Laton", seg=10, rot=[('X', 90)])    # the ship's horn
    b.box((0.2, 0.4, 0.2), (0, oz - bbz + 0.05, bz), "CR_Gris")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.5, 0.5, 0.35), (sx * 3.4, sy * 3.4, 3.975), "CR_Gris")
            b.cyl(0.18, 0.18, 0.06, (sx * 3.4, sy * 3.4, 4.17), "CR_Emit_Chimenea", seg=10)
    return b.build()

def radar():   # mast for the wheelhouse roof: radar, horn, yard with signal flags, masthead light
    b = CB("CR_Radar")
    b.box((1.4, 1.4, 0.4), (0, 0, 0.2), "CR_Blanco")
    b.cyl(0.18, 0.1, 6.2, (0, 0, 3.5), "CR_Blanco", seg=8)
    b.box((3.0, 0.12, 0.12), (0, 0, 5.0), "CR_Blanco")
    b.box((1.3, 1.1, 0.1), (0, -0.85, 3.4), "CR_Gris")
    b.box((0.4, 0.4, 0.35), (0, -0.85, 3.63), "CR_Gris")
    b.box((3.2, 0.18, 0.28), (0, -0.85, 3.95), "CR_Blanco")
    b.ball(0.45, (1.2, 0.0, 5.45), "CR_Blanco", seg=10)
    for x in (-0.18, 0.18): b.cyl(0.04, 0.16, 0.6, (x, -0.45, 4.55), "CR_Laton", seg=8, rot=[('X', 90)])
    b.cyl(0.08, 0.08, 0.18, (0, 0, 6.72), "CR_Emit_Mastil", seg=8)
    b.cyl(0.11, 0.02, 0.1, (0, 0, 6.86), "CR_Negro", seg=8)
    b.cyl(0.015, 0.015, 1.4, (0.3, 0, 7.4), "CR_Negro", seg=4)
    cols = ("CR_Rojo", "CR_Amarillo", "CR_Azul", "CR_Blanco", "CR_Verde")
    for i in range(7):
        t = (i + 0.5) / 7
        b.box((0.24, 0.015, 0.3), (1.45 * t, 0.02, 6.6 - 1.6 * t - 0.15), cols[i % 5], rot=[('Y', 25)])
    b.tube([(0, 0.02, 6.6), (1.45, 0.02, 5.02)], 0.008, "CR_Negro", sides=4)
    return b.build()

def puente_ventana():   # wheelhouse facade strip: slanted windows that glow green at night, visor above
    b = CB("CR_PuenteVentana", joint=1.0, wall=True, ceil=ROOM)
    b.box((2.0, 0.1, 1.15), (0, -0.05, 0.575), "CR_Blanco")
    b.box((2.0, 0.25, 0.08), (0, -0.125, 1.15), "CR_Blanco")
    s15 = math.sin(math.radians(15))
    b.box((1.8, 0.04, 1.7), (0, -0.1 - 0.85 * s15, 2.0), "CR_Emit_Puente", rot=[('X', 15)])
    for x in (-0.94, 0.0, 0.94): b.box((0.1, 0.1, 1.75), (x, -0.1 - 0.85 * s15, 2.0), "CR_Blanco", rot=[('X', 15)])
    b.box((0.03, 0.06, 0.55), (-0.45, -0.17 - 0.6 * s15, 1.62), "CR_Negro", rot=[('X', 15), ('Y', 35)])   # wiper
    b.box((2.0, 0.1, 0.55), (0, -0.05, 3.175), "CR_Blanco")
    b.box((2.0, 0.75, 0.08), (0, -0.42, 2.93), "CR_Blanco", rot=[('X', -6)])
    b.box((2.0, 0.15, 0.15), (0, -0.075, 3.525), "CR_Azul")
    return b.build()

def timon():   # ship's wheel on its pedestal with the compass binnacle in front (front -Y = the helmsman)
    b = CB("CR_Timon")
    b.cyl(0.2, 0.15, 1.0, (0, 0.2, 0.5), "CR_Madera", seg=8)
    b.box((0.3, 0.35, 0.3), (0, 0.15, 1.12), "CR_Madera")
    b.cyl(0.07, 0.07, 0.3, (0, -0.1, 1.2), "CR_Laton", seg=8, rot=[('X', 90)])
    b.ring(0.45, 0.035, (0, -0.25, 1.2), "CR_Madera", seg=16, sides=6, rot=[('X', 90)])
    b.cyl(0.075, 0.075, 0.06, (0, -0.29, 1.2), "CR_Laton", seg=10, rot=[('X', 90)])
    for i in range(8):
        a = i * 45
        n0 = b.mark()
        b.box((0.04, 0.04, 0.45), (0, 0, 0.225), "CR_Madera")
        b.cyl(0.03, 0.022, 0.16, (0, 0, 0.55), "CR_Madera", seg=6)
        b.move(n0, rot=[('Y', a)], at=(0, -0.25, 1.2))
    b.cyl(0.1, 0.14, 0.95, (0, -0.85, 0.475), "CR_Madera", seg=8)
    b.ball(0.18, (0, -0.85, 1.05), "CR_Laton", scale=(1, 1, 0.7), seg=10)
    b.cyl(0.1, 0.1, 0.04, (0, -0.85, 1.17), "CR_VidrioClaro", seg=10)
    return b.build()

def consola():   # bridge console 2 m: screens, radar scope, throttles, and the captain's mug
    b = CB("CR_Consola")
    b.box((2.0, 0.7, 0.85), (0, 0.1, 0.425), "CR_Gris")
    b.box((2.0, 0.62, 0.08), (0, -0.18, 0.93), "CR_Gris", rot=[('X', 20)])
    b.box((2.0, 0.15, 0.6), (0, 0.38, 1.2), "CR_Gris")
    for x in (-0.65, 0.65): b.box((0.5, 0.04, 0.32), (x, 0.285, 1.22), "CR_Emit_Consola")
    b.disc(0.2, 0.04, (0, 0.285, 1.22), "CR_Emit_Consola", seg=16)
    b.ring(0.21, 0.02, (0, 0.27, 1.22), "CR_Negro", seg=16, sides=4, rot=[('X', 90)])
    n0 = b.mark()
    for x, c in ((-0.15, "CR_Rojo"), (0.15, "CR_Rojo")):
        b.box((0.04, 0.04, 0.25), (x, 0, 0.13), "CR_Negro", rot=[('X', -25)])
        b.ball(0.05, (x, -0.05, 0.25), c, seg=8)
    for i, c in enumerate(("CR_Rojo", "CR_Verde", "CR_Amarillo", "CR_Verde", "CR_Rojo", "CR_Amarillo")):
        b.box((0.06, 0.06, 0.03), (-0.85 + i * 0.12, 0.05, 0.02), c)
        b.box((0.06, 0.06, 0.03), (0.3 + i * 0.12, 0.05, 0.02), c)
    b.move(n0, rot=[('X', 20)], at=(0, -0.18, 0.97))
    b.cyl(0.045, 0.04, 0.1, (0.85, 0.2, 0.9), "CR_Blanco", seg=8)
    b.ring(0.03, 0.008, (0.9, 0.2, 0.9), "CR_Blanco", seg=8, sides=4, rot=[('X', 90)])
    return b.build()

def puerta_estanca():
    """Watertight door for a 2 m doorway (two side by side for a 4 m one): white bulkhead 2 x 3.6 m with a grey steel
    door on BOTH faces, red hand wheel, six dogs and a sign. The paid doors of the ship."""
    b = CB("CR_PuertaEstanca", joint=1.0, ceil=ROOM)
    b.box((2.0, 0.2, ROOM), (0, 0, ROOM / 2), "CR_Blanco")
    for rot in (0, 180):
        n0 = b.mark()
        b.wpoly(rrect(1.3, 2.3, 0.3, cy=1.4), -0.125, -0.1, "CR_Acero")
        b.wpoly(rrect(1.1, 2.1, 0.22, cy=1.4), -0.15, -0.12, "CR_Gris")
        b.cyl(0.05, 0.05, 0.09, (0, -0.185, 1.35), "CR_Rojo", seg=8, rot=[('X', 90)])
        b.ring(0.25, 0.03, (0, -0.22, 1.35), "CR_Rojo", seg=16, sides=6, rot=[('X', 90)])
        for i in range(4):
            n1 = b.mark()
            b.box((0.03, 0.03, 0.24), (0, 0, 0.12), "CR_Rojo")
            b.move(n1, rot=[('Y', 45 + 90 * i)], at=(0, -0.22, 1.35))
        for x, z in ((-0.5, 0.75), (-0.5, 1.4), (-0.5, 2.05), (0.5, 0.75), (0.5, 1.4), (0.5, 2.05)):
            b.box((0.06, 0.04, 0.2), (x, -0.165, z), "CR_Negro")
            b.cyl(0.03, 0.03, 0.06, (x, -0.18, z + 0.08), "CR_Negro", seg=6, rot=[('X', 90)])
        b.box((1.0, 0.02, 0.22), (0, -0.11, 2.85), "CR_Amarillo")
        b.text("MAMPARO ESTANCO", 0.09, (0, -0.13, 2.85), "CR_Negro", depth=0.02)
        b.box((1.1, 0.02, 0.06), (0, -0.11, 0.22), "CR_Amarillo")
        b.move(n0, rot=[('Z', rot)])
    return b.build()

# ================================================================== deckhouse walls along the promenade
def fach_base(b):
    b.box((2.0, 0.08, 3.4), (0, -0.04, 1.7), "CR_Blanco")
    b.box((2.0, 0.12, 0.25), (0, -0.06, 0.125), "CR_Azul")
    b.box((2.0, 0.12, 0.2), (0, -0.06, 3.5), "M_Wood")
    b.box((2.0, 0.095, 0.02), (0, -0.0475, 2.6), "CR_Blanco")                     # weld seam, 1.5 cm proud

def fachada_a():   # big rounded window that glows at night, an extinguisher
    b = CB("CR_Fachada_A", joint=1.0, wall=True, ceil=ROOM)
    fach_base(b)
    b.wpoly(rrect(1.6, 1.4, 0.18, cy=1.75), -0.14, -0.08, "CR_Azul")
    b.wpoly(rrect(1.44, 1.24, 0.12, cy=1.75), -0.17, -0.1, "CR_Emit_Ventana")
    b.box((1.0, 0.03, 0.04), (0, -0.185, 1.75), "CR_Azul")                       # transom bar in front of the glass
    b.box((1.7, 0.12, 0.06), (0, -0.2, 1.0), "CR_Blanco")
    b.cyl(0.09, 0.09, 0.5, (0.83, -0.2, 0.62), "CR_Rojo", seg=8)
    b.box((0.12, 0.1, 0.06), (0.83, -0.12, 0.8), "CR_Gris")
    b.box((0.05, 0.05, 0.1), (0.83, -0.2, 0.92), "CR_Negro")
    return b.build()

def fachada_b():   # glass double door into the saloon (only a picture) under a glowing exit sign
    b = CB("CR_Fachada_B", joint=1.0, wall=True, ceil=ROOM)
    fach_base(b)
    b.wpoly(rrect(1.6, 2.5, 0.25, cy=1.25), -0.14, -0.08, "CR_Laton")
    for s in (-1, 1):
        b.wpoly(rrect(0.68, 2.25, 0.18, cx=s * 0.36, cy=1.24), -0.17, -0.1, "CR_Emit_Ventana")
        b.cyl(0.02, 0.02, 0.5, (s * 0.12, -0.2, 1.1), "CR_Laton", seg=6, rot=[('Y', 90)])
    b.box((1.8, 0.4, 0.06), (0, -0.2, 0.03), "CR_Gris")
    b.box((0.55, 0.03, 0.2), (0, -0.095, 2.85), "CR_Emit_Salida")
    b.text("SALIDA", 0.11, (0, -0.12, 2.85), "CR_Blanco", depth=0.02)
    return b.build()

def fachada_c():   # porthole, the fire hose reel (the Manguera), muster point sign
    b = CB("CR_Fachada_C", joint=1.0, wall=True, ceil=ROOM)
    fach_base(b)
    porthole(b, -0.5, 1.9, r=0.22, y0=-0.08)
    b.disc(0.32, 0.04, (0.45, -0.1, 1.25), "CR_Rojo", seg=12)
    b.ring(0.22, 0.07, (0.45, -0.2, 1.25), "CR_Amarillo", seg=14, sides=6, rot=[('X', 90)])
    b.cyl(0.05, 0.05, 0.12, (0.45, -0.18, 1.25), "CR_Gris", seg=8, rot=[('X', 90)])
    b.tube([(0.67, -0.2, 1.25), (0.7, -0.24, 0.9), (0.62, -0.26, 0.75)], 0.035, "CR_Amarillo", sides=6)
    b.cyl(0.04, 0.025, 0.16, (0.56, -0.27, 0.7), "CR_Laton", seg=6, rot=[('Y', 50)])
    b.box((0.6, 0.02, 0.14), (0.45, -0.09, 1.75), "CR_Rojo")
    b.text("MANGUERA", 0.08, (0.45, -0.11, 1.75), "CR_Blanco", depth=0.02)
    b.box((0.7, 0.02, 0.36), (-0.5, -0.09, 2.85), "CR_Verde")
    b.text("PUNTO DE", 0.08, (-0.5, -0.11, 2.92), "CR_Blanco", depth=0.02)
    b.text("REUNIÓN A", 0.08, (-0.5, -0.11, 2.78), "CR_Blanco", depth=0.02)
    return b.build()

def fachada_d():   # notice board with the gala dinner and the towel rule, a teak bench
    b = CB("CR_Fachada_D", joint=1.0, wall=True, ceil=ROOM)
    fach_base(b)
    b.box((1.5, 0.04, 0.95), (0, -0.1, 1.85), "CR_Marron")
    for x, lines, m in ((-0.36, ("CENA DE GALA", "CON EL", "CAPITÁN", "20:00"), "CR_Crema"),
                        (0.36, ("PROHIBIDO", "RESERVAR", "TUMBONA", "CON TOALLA"), "CR_Blanco")):
        b.box((0.62, 0.02, 0.78), (x, -0.13, 1.85), m)
        for i, t in enumerate(lines): b.text(t, 0.075, (x, -0.15, 2.1 - i * 0.16), "CR_Rojo" if i == 0 else "CR_Negro", depth=0.02)
    b.box((1.6, 0.38, 0.06), (0, -0.3, 0.45), "M_Wood")
    for x in (-0.7, 0.7): b.box((0.06, 0.34, 0.42), (x, -0.3, 0.21), "CR_Gris")
    for z in (0.62, 0.78): b.box((1.6, 0.04, 0.1), (0, -0.14, z), "M_Wood")
    return b.build()

def taquilla():   # deck locker with life jackets and two life-raft canisters on top (cuts the view along the promenade)
    b = CB("CR_Taquilla")
    b.box((1.9, 1.0, 1.6), (0, 0, 0.8), "CR_Blanco")
    for s in (-1, 1):
        b.box((0.9, 0.03, 1.4), (s * 0.47, -0.515, 0.8), "CR_Blanco")
        for z in (0.3, 0.42, 0.54, 0.66): b.box((0.5, 0.02, 0.04), (s * 0.47, -0.54, z), "CR_Negro")
        b.box((0.04, 0.05, 0.2), (s * 0.08, -0.55, 0.85), "CR_Gris")
    b.text("CHALECOS", 0.13, (0, -0.545, 1.32), "CR_Azul", depth=0.03)
    b.box((0.1, 0.03, 0.12), (0.0, -0.58, 0.95), "CR_Laton")
    b.ring(0.04, 0.01, (0.0, -0.58, 1.03), "CR_Gris", seg=8, sides=4, rot=[('X', 90)], arc=0.5)
    for y in (-0.31, 0.31):
        for x in (-0.6, 0.6): b.box((0.1, 0.5, 0.15), (x, y, 1.675), "CR_Gris")
        b.cyl(0.3, 0.3, 1.5, (0, y, 2.05), "CR_Blanco", seg=10, rot=[('Y', 90)])
        for x in (-0.4, 0.4): b.cyl(0.32, 0.32, 0.06, (x, y, 2.05), "CR_Amarillo", seg=10, rot=[('Y', 90)])
    return b.build()

def guirnalda():   # dressing lights between two masts / lamp posts: 4 m span along X, signal flags between the bulbs
    b = CB("CR_Guirnalda")
    n = 12
    flags = ("CR_Rojo", "CR_Amarillo", "CR_Azul", "CR_Blanco")
    pts = [(-2.0 + 4.0 * i / n, -0.35 * (1 - ((2.0 * i / n) - 1) ** 2)) for i in range(n + 1)]
    b.tube([(x, 0, z) for x, z in pts], 0.012, "CR_Negro", sides=4, caps=True)
    for i in range(n):
        (x0, z0), (x1, z1) = pts[i], pts[i + 1]
        b.ball(0.07, ((x0 + x1) / 2, 0, (z0 + z1) / 2 - 0.1), "CR_Emit_Guirnalda", seg=6)
        if i % 2: b.cyl(0.14, 0.0, 0.3, ((x0 + x1) / 2, 0, (z0 + z1) / 2 - 0.2), flags[(i // 2) % 4], seg=3, rot=[('X', 180)], scale=(1, 0.08, 1))
    return b.build()

def farol():   # deck lamp post, 3 m
    b = CB("CR_Farol")
    b.cyl(0.2, 0.2, 0.2, (0, 0, 0.1), "CR_Azul", seg=8)
    b.cyl(0.06, 0.05, 2.45, (0, 0, 1.42), "CR_Laton", seg=8)
    b.cyl(0.12, 0.12, 0.06, (0, 0, 2.65), "CR_Laton", seg=8)
    b.ball(0.2, (0, 0, 2.85), "CR_Emit_Farol", seg=10)
    b.cyl(0.14, 0.04, 0.12, (0, 0, 3.08), "CR_Laton", seg=8)
    return b.build()

# ================================================================== sun deck
def torre_techo():   # canopy over the 6 x 4 m slide platform (origin = platform centre at its floor)
    b = CB("CR_Torre_Techo")
    for sx in (-1, 1):
        for sy in (-1, 1): b.cyl(0.08, 0.08, 3.0, (sx * 2.85, sy * 1.85, 1.5), "CR_Blanco", seg=8)
    b.pyramid(6.4, 4.4, 1.0, (0, 0, 3.0), "CR_AwningPaint")
    for s in (-1, 1):
        b.box((6.4, 0.04, 0.3), (0, s * 2.2, 2.9), "CR_Blanco")
        b.box((0.04, 4.36, 0.3), (s * 3.2, 0, 2.9), "CR_Blanco")
    b.box((3.6, 0.06, 0.55), (0, -2.26, 3.35), "CR_Azul")
    b.text("AQUAPARK SENIOR", 0.3, (0, -2.31, 3.35), "CR_Blanco", depth=0.03)
    return b.build()

def G(c, r, h, c0, r0, h0):   # layout cell (col, row, height in m) -> Blender offset from (c0, r0, h0)
    return (-2.0 * (c - c0), 2.0 * (r - r0), h - h0)

def slide(name, paint, path, start, supports=(), extra=None):
    b = CB(name)
    c0, r0, h0 = start
    P = catmull([G(c, r, h, c0, r0, h0) for c, r, h in path], 4)
    b.tube(P, 0.65, paint, sides=10, cap_m="CR_Negro", mfn=lambda i: "CR_Blanco" if i % 6 == 5 else paint)
    b.ring(0.7, 0.08, P[0], "CR_Blanco", seg=12, sides=4, rot=[('X', 90), ('Z', math.degrees(math.atan2(P[1][0] - P[0][0], -(P[1][1] - P[0][1]))))])
    for c, r, ground in supports:
        x, y, _ = G(c, r, 0, c0, r0, h0)
        z = min(P, key=lambda p: (p.x - x) ** 2 + (p.y - y) ** 2).z - 0.6
        g = ground - h0
        b.cyl(0.13, 0.13, z - g, (x, y, (z + g) / 2), "CR_Blanco", seg=8)
        b.box((0.9, 0.3, 0.12), (x, y, z), "CR_Blanco")
    if extra: extra(b, c0, r0, h0)
    return b.build()

def tobogan_piscina():
    """Slide 1 of the layout: from the platform cell (14, 6) at 12 m to beside the pool at (33, 8), 8 m; it runs north of
    the funnel. Origin = the start cell at the platform floor. Tube r 0.65 m, the exit mouth faces south at (33, 7.5)."""
    path = [(14.0, 6.0, 12.75), (14.0, 5.0, 12.65), (14.6, 4.3, 12.5), (16.5, 4.1, 12.3), (19.5, 4.2, 11.9), (23.0, 4.4, 11.5),
            (26.5, 5.0, 11.2), (29.5, 5.3, 10.9), (31.6, 6.0, 10.3), (32.8, 6.6, 9.6), (33.1, 7.1, 9.0), (33.0, 7.5, 8.75)]
    return slide("CR_Tobogan_Piscina", "CR_SlidePaint", path, (14, 6, 12.0), supports=((19.5, 4.4, 8.0), (25.5, 4.9, 8.0), (30.5, 5.8, 8.0)))

def tobogan_casco():
    """Slide 2: from the platform cell (16, 7) at 12 m out over the port side, a helix over the sea beside the stern, and
    in over the rail to the stern deck at (9, 4), 4 m. Origin = the start cell at the platform floor. The helix hangs on
    a short column braced to the hull face (row 2.5) under the main deck."""
    path = [(16.0, 7.0, 12.75), (16.0, 6.0, 12.65), (16.1, 4.5, 12.45), (15.8, 3.0, 12.2), (14.6, 1.8, 11.8), (12.6, 1.2, 11.3), (10.8, 1.0, 10.9)]
    C, R = (8.6, 0.3), 1.25
    for k in range(12):
        th = math.radians(35 + 385 * k / 11)
        path.append((C[0] + R * math.cos(th), C[1] + R * math.sin(th), 10.6 - 4.2 * k / 11))
    path += [(9.1, 2.0, 6.05), (9.0, 2.6, 5.8), (9.0, 3.3, 5.2), (9.0, 3.9, 4.75)]
    def column(b, c0, r0, h0):
        x, y, _ = G(C[0], C[1], 0, c0, r0, h0)
        b.cyl(0.18, 0.18, 8.3, (x, y, (2.5 + 10.8) / 2 - h0), "CR_Blanco", seg=8)
        for h in (3.0, 3.6):
            yh = G(0, 2.44, 0, 0, r0, 0)[1]
            b.box((0.16, yh - y, 0.16), (x, (y + yh) / 2, h - h0), "CR_Blanco")
    return slide("CR_Tobogan_Casco", "CR_Slide2Paint", path, (16, 7, 12.0), extra=column)

def piscina():
    """The raised pool of the sun deck, 12 x 10 m (cells 34-39 x 6-10): tiled walls 0.95 m, white coping, 65 cm of water,
    six lights under the water, two ladders, a flamingo float and the aquagym timetable."""
    b = CB("CR_Piscina")
    for s in (-1, 1):
        b.box((12.0, 0.3, 0.95), (0, s * 4.85, 0.475), "CR_Azulejo")
        b.box((0.3, 9.4, 0.95), (s * 5.85, 0, 0.475), "CR_Azulejo")
        b.box((12.3, 0.55, 0.08), (0, s * 4.85, 0.99), "CR_Blanco")
        b.box((0.55, 9.15, 0.08), (s * 5.85, 0, 0.99), "CR_Blanco")
        for x in (-3.0, 0.0, 3.0): b.disc(0.14, 0.06, (x, s * 4.7, 0.45), "CR_Emit_Piscina", seg=10)
    b.box((11.4, 9.4, 0.1), (0, 0, 0.05), "CR_Azulejo")
    b.box((11.38, 9.38, 0.04), (0, 0, 0.78), "CR_AguaPiscina")
    for x in (-3.5, 3.5):
        for d in (-0.25, 0.25):
            b.tube([(x + d, -4.55, 0.3), (x + d, -4.55, 1.2), (x + d, -4.85, 1.38), (x + d, -5.15, 1.2), (x + d, -5.2, 0.0)], 0.025, "CR_Cromo", sides=6)
        for z in (0.35, 0.6): b.cyl(0.02, 0.02, 0.5, (x, -4.58, z), "CR_Cromo", seg=6, rot=[('Y', 90)])
        b.box((0.9, 0.35, 0.45), (x, -5.32, 0.225), "CR_Blanco")
    b.ring(0.45, 0.15, (2.5, 1.5, 0.8), "CR_Rosa", seg=14, sides=6)
    b.cyl(0.06, 0.05, 0.6, (2.5, 1.0, 1.15), "CR_Rosa", seg=6, rot=[('X', 20)])
    b.ball(0.11, (2.5, 0.86, 1.47), "CR_Rosa", seg=8)
    b.cyl(0.04, 0.0, 0.16, (2.5, 0.72, 1.44), "CR_Negro", seg=6, rot=[('X', 100)])
    b.cyl(0.03, 0.03, 1.2, (5.75, -4.75, 1.6), "CR_Gris", seg=6)
    b.box((0.7, 0.03, 0.4), (5.75, -4.75, 2.3), "CR_Blanco")
    b.text("AQUAGYM", 0.09, (5.75, -4.775, 2.38), "CR_Azul", depth=0.02)
    b.text("11:00", 0.12, (5.75, -4.775, 2.22), "CR_Rojo", depth=0.02)
    return b.build()

def jacuzzi():   # round teak tub 1.9 m, bubbles, the light, a rubber duck and somebody's dentures in a glass
    b = CB("CR_Jacuzzi")
    b.cyl(0.95, 0.95, 0.62, (0, 0, 0.31), "M_Wood", seg=8)
    b.cyl(0.82, 0.82, 0.06, (0, 0, 0.64), "CR_AguaPiscina", seg=16)
    b.ring(0.87, 0.1, (0, 0, 0.66), "CR_Blanco", seg=8, sides=4, phase=math.pi / 4, rot=[('Z', 22.5)])
    b.cyl(0.14, 0.14, 0.02, (0, 0, 0.635), "CR_Emit_Piscina", seg=10)
    rnd = random.Random(3)
    for i in range(10):
        a, r = rnd.uniform(0, 6.28), rnd.uniform(0.1, 0.6)
        b.ball(rnd.uniform(0.03, 0.06), (r * math.cos(a), r * math.sin(a), 0.675), "CR_Blanco", seg=6)
    b.ball(0.08, (-0.3, 0.2, 0.71), "CR_Amarillo", seg=8)
    b.ball(0.05, (-0.36, 0.2, 0.8), "CR_Amarillo", seg=8)
    b.cyl(0.02, 0.0, 0.05, (-0.42, 0.2, 0.8), "CR_Naranja", seg=6, rot=[('Y', -90)])
    b.cyl(0.05, 0.05, 0.13, (0.62, -0.42, 0.825), "CR_VidrioClaro", seg=8)
    b.ring(0.03, 0.012, (0.62, -0.42, 0.85), "CR_Carne", seg=8, sides=4, arc=0.5)
    b.box((0.05, 0.02, 0.02), (0.62, -0.44, 0.85), "CR_Blanco")
    return b.build()

def lounger(b, x0, towel=False, hat=False):
    b.box((0.62, 1.2, 0.07), (x0, 0.3, 0.35), "CR_Blanco")
    n0 = b.mark()
    b.box((0.62, 0.75, 0.07), (0, -0.375, 0), "CR_Blanco")
    if towel: b.box((0.56, 0.7, 0.03), (0, -0.375, 0.05), "CR_TowelPaint")
    b.move(n0, rot=[('X', -40)], at=(x0, -0.3, 0.385))
    for dx in (-0.27, 0.27):
        for y in (-0.25, 0.85): b.box((0.05, 0.05, 0.32), (x0 + dx, y, 0.16), "CR_Blanco")
    if towel:
        b.box((0.56, 1.0, 0.03), (x0, 0.38, 0.4), "CR_TowelPaint")
        b.box((0.56, 0.03, 0.3), (x0, 0.925, 0.26), "CR_TowelPaint")
    if hat:
        b.cyl(0.22, 0.22, 0.02, (x0, 0.35, 0.395), "CR_Paja", seg=12)
        b.cyl(0.11, 0.1, 0.09, (x0, 0.35, 0.45), "CR_Paja", seg=10)
        b.cyl(0.112, 0.112, 0.025, (x0, 0.35, 0.4325), "CR_Rojo", seg=10)
        b.cyl(0.03, 0.03, 0.14, (x0 + 0.18, 0.7, 0.455), "CR_Naranja", seg=6)

def tumbona():   # two sun loungers: one "reserved" with a towel, one with a straw hat; drinks and socks-with-sandals
    b = CB("CR_Tumbona")
    lounger(b, -0.5, towel=True)
    lounger(b, 0.5, hat=True)
    b.cyl(0.16, 0.16, 0.04, (0, -0.55, 0.45), "CR_Blanco", seg=10)
    b.cyl(0.03, 0.03, 0.43, (0, -0.55, 0.215), "CR_Blanco", seg=6)
    b.cyl(0.0, 0.05, 0.08, (0.04, -0.55, 0.51), "CR_Naranja", seg=8)
    b.cyl(0.006, 0.006, 0.14, (0.06, -0.55, 0.56), "CR_Rojo", seg=4, rot=[('Y', 15)])
    for i, x in enumerate((0.18, 0.31)):
        b.box((0.1, 0.26, 0.03), (x, 1.05 - 0.15 * i, 0.015), "CR_Marron", rot=[('Z', 8 - 16 * i)])
        b.box((0.09, 0.2, 0.08), (x, 1.04 - 0.15 * i, 0.07), "CR_Blanco", rot=[('Z', 8 - 16 * i)])
    return b.build()

def sombrilla():
    b = CB("CR_Sombrilla")
    b.cyl(0.25, 0.25, 0.08, (0, 0, 0.04), "CR_Gris", seg=10)
    b.cyl(0.03, 0.03, 2.22, (0, 0, 1.19), "CR_Blanco", seg=6)
    b.cyl(1.2, 0.05, 0.45, (0, 0, 2.15), "CR_AwningPaint", seg=8)
    b.ball(0.05, (0, 0, 2.42), "CR_Blanco", seg=6)
    return b.build()

def bar_cubierta():   # deck bar under a straw roof: counter, bottles, blender, the sign
    b = CB("CR_BarCubierta")
    for y in (-0.8, 0.8): b.box((1.9, 0.3, 1.05), (0, y, 0.525), "CR_Paja")
    b.box((0.3, 1.3, 1.05), (-0.8, 0, 0.525), "CR_Paja")
    b.box((2.0, 0.42, 0.05), (0, -0.8, 1.075), "M_Wood")
    b.box((2.0, 0.42, 0.05), (0, 0.8, 1.075), "M_Wood")
    b.box((0.42, 1.18, 0.05), (-0.8, 0, 1.075), "M_Wood")
    for sx in (-1, 1):
        for sy in (-1, 1): b.cyl(0.06, 0.06, 2.6, (sx * 0.88, sy * 0.88, 1.3), "M_Wood", seg=6)
    b.cyl(1.5, 0.0, 1.0, (0, 0, 3.0), "CR_Paja", seg=10)
    for i, m in enumerate(("CR_Verde", "CR_Marron", "CR_VidrioClaro", "CR_Amarillo", "CR_Verde")):
        b.cyl(0.04, 0.04, 0.3, (-0.5 + i * 0.22, 0.82, 1.25), m, seg=6)
    b.box((0.18, 0.18, 0.2), (0.6, -0.75, 1.2), "CR_Gris")
    b.cyl(0.07, 0.06, 0.2, (0.6, -0.75, 1.4), "CR_VidrioClaro", seg=8)
    b.cyl(0.06, 0.06, 0.12, (-0.5, -0.78, 1.16), "CR_VidrioClaro", seg=8)
    b.box((1.4, 0.04, 0.32), (0, -0.92, 2.35), "CR_Madera")
    b.text("PIÑA COLADA", 0.09, (0, -0.95, 2.41), "CR_Blanco", depth=0.02)
    b.text("SIN AZÚCAR", 0.09, (0, -0.95, 2.28), "CR_Amarillo", depth=0.02)
    return b.build()

def palmera():   # potted palm, 3.3 m
    b = CB("CR_Palmera")
    b.box((1.0, 1.0, 0.7), (0, 0, 0.35), "CR_Blanco")
    b.box((1.06, 1.06, 0.06), (0, 0, 0.72), "M_Wood")
    b.box((0.9, 0.9, 0.04), (0, 0, 0.76), "CR_Marron")
    pts = [(0.03 * i * i, 0.0, 0.75 + 0.42 * i) for i in range(7)]
    b.tube(pts, [0.12 - 0.008 * i for i in range(7)], "M_Bark", sides=7)
    top = Vector(pts[-1])
    for k in range(8):
        n0 = b.mark()
        b.box((0.3, 1.5, 0.03), (0, -0.75, 0), "CR_Hoja", rot=[('X', 28 + 8 * (k % 2))])
        b.move(n0, rot=[('Z', k * 45 + 10)], at=top)
    for k in range(3): b.ball(0.09, (top.x + 0.13 * math.cos(k * 2.1), 0.13 * math.sin(k * 2.1), top.z - 0.15), "CR_Marron", seg=6)
    return b.build()

def cabana():   # cabana: daybed behind white curtains on three sides, canvas roof, "RESERVADO"
    b = CB("CR_Cabana")
    b.box((1.9, 1.9, 0.12), (0, 0, 0.06), "M_Wood")
    for sx in (-1, 1):
        for sy in (-1, 1): b.cyl(0.06, 0.06, 2.4, (sx * 0.88, sy * 0.88, 1.32), "CR_Blanco", seg=6)
    for s in (-1, 1):
        b.box((1.9, 0.08, 0.1), (0, s * 0.88, 2.47), "CR_Blanco")
        b.box((0.08, 1.74, 0.1), (s * 0.88, 0, 2.47), "CR_Blanco")
    b.pyramid(2.1, 2.1, 0.5, (0, 0, 2.52), "CR_AwningPaint")
    n = 7
    for i in range(n):
        x = -0.8 + (i + 0.5) * 1.6 / n
        b.box((1.6 / n, 0.04, 2.25), (x, 0.88 + (0.02 if i % 2 else -0.02), 1.255), "CR_Crema")
        for s in (-1, 1): b.box((0.04, 1.6 / n, 2.25), (s * 0.88 + (0.02 if i % 2 else -0.02), x, 1.255), "CR_Crema")
    for s in (-1, 1):
        b.cyl(0.11, 0.08, 2.2, (s * 0.72, -0.86, 1.25), "CR_Crema", seg=6)
        b.ring(0.1, 0.02, (s * 0.72, -0.86, 1.2), "CR_AwningPaint", seg=8, sides=4)
    b.box((1.5, 1.2, 0.3), (0, 0.2, 0.27), "CR_Blanco")
    for x in (-0.35, 0.35): b.box((0.5, 0.25, 0.14), (x, 0.65, 0.49), "CR_AwningPaint", rot=[('X', -20)])
    b.box((0.5, 0.03, 0.2), (0, -0.55, 0.62), "CR_Dorado", rot=[('X', -15)])
    b.text("RESERVADO", 0.06, (0, -0.57, 0.625), "CR_Negro", depth=0.02, rot=(('X', 90), ('X', -15)))
    return b.build()

def tejo():   # shuffleboard corner (low cover, <= 0.9 m): the scoring end of a lane, pucks, cue rack, bench, scoreboard
    b = CB("CR_Tejo")
    b.box((1.8, 1.1, 0.012), (0, -0.35, 0.006), "CR_Verde")
    tri = [(-0.75, 0.1), (0.75, 0.1), (0.0, -0.85)]
    for i in range(3):
        (x0, y0), (x1, y1) = tri[i], tri[(i + 1) % 3]
        b.box((math.hypot(x1 - x0, y1 - y0), 0.04, 0.02), ((x0 + x1) / 2, (y0 + y1) / 2, 0.022), "CR_Amarillo", rot=[('Z', math.degrees(math.atan2(y1 - y0, x1 - x0)))])
    b.box((1.2, 0.04, 0.02), (0, -0.25, 0.022), "CR_Amarillo")
    for t, x, y in (("10", 0, -0.55), ("8", -0.25, -0.1), ("7", 0.25, -0.1)): b.text(t, 0.14, (x, y, 0.025), "CR_Blanco", depth=0.02, rot=None)
    for i, (x, y) in enumerate(((-0.5, -0.2), (0.4, -0.5), (0.1, -0.8), (-0.2, 0.05))):
        b.cyl(0.075, 0.075, 0.055, (x, y, 0.0275), "CR_Rojo" if i % 2 else "CR_Negro", seg=10)
    b.box((1.8, 0.25, 0.06), (0, 0.85, 0.03), "CR_Madera")
    for x in (-0.85, 0.85): b.box((0.06, 0.25, 0.8), (x, 0.85, 0.4), "CR_Madera")
    for z in (0.5, 0.72):
        for k in range(2): b.cyl(0.015, 0.015, 1.7, (0, 0.8 + 0.08 * k, z), "M_Wood", seg=5, rot=[('Y', 90)])
    b.box((0.9, 0.35, 0.05), (-0.35, 0.42, 0.45), "M_Wood")
    for x in (-0.72, 0.02): b.box((0.05, 0.3, 0.43), (x, 0.42, 0.215), "CR_Gris")
    b.box((0.5, 0.04, 0.32), (0.62, 0.42, 0.7), "CR_Negro")
    for x in (0.42, 0.82): b.cyl(0.015, 0.015, 0.6, (x, 0.47, 0.3), "CR_Gris", seg=5)
    b.text("ROJO 75", 0.06, (0.62, 0.39, 0.77), "CR_Rojo", depth=0.02)
    b.text("NEGRO 3", 0.06, (0.62, 0.39, 0.64), "CR_Blanco", depth=0.02)
    return b.build()

def tejo_pista():   # a whole shuffleboard lane painted on the deck, 12 x 1.8 m along X (decoration, no collider)
    b = CB("CR_Tejo_Pista")
    b.box((12.0, 1.8, 0.012), (0, 0, 0.006), "CR_Verde")
    for s in (-1, 1):
        tri = [(s * 4.0, -0.75), (s * 4.0, 0.75), (s * 5.6, 0.0)]
        for i in range(3):
            (x0, y0), (x1, y1) = tri[i], tri[(i + 1) % 3]
            b.box((math.hypot(x1 - x0, y1 - y0), 0.04, 0.02), ((x0 + x1) / 2, (y0 + y1) / 2, 0.022), "CR_Amarillo", rot=[('Z', math.degrees(math.atan2(y1 - y0, x1 - x0)))])
        b.box((0.04, 1.6, 0.02), (s * 3.3, 0, 0.022), "CR_Amarillo")
        b.text("10 OFF", 0.22, (s * 2.6, 0, 0.025), "CR_Blanco", depth=0.02, rot=(('Z', -90 * s),))
    for i in range(-5, 6): b.box((0.4, 0.04, 0.02), (i * 0.6, 0.0, 0.022), "CR_Blanco")
    return b.build()

# ================================================================== interiors
def lampara():   # crystal chandelier of the atrium: origin at the ceiling hook, hangs 1.9 m down, 2.6 m across
    b = CB("CR_Lampara")
    b.cyl(0.25, 0.25, 0.08, (0, 0, -0.04), "CR_Laton", seg=10)
    b.cyl(0.04, 0.04, 0.8, (0, 0, -0.48), "CR_Laton", seg=6)
    b.ball(0.18, (0, 0, -0.95), "CR_Laton", seg=10)
    for R, z, nd in ((1.2, -1.35, 16), (0.85, -1.05, 12), (0.5, -0.78, 8)):
        b.ring(R, 0.03, (0, 0, z), "CR_Laton", seg=24, sides=4)
        for k in range(nd):
            a = 2 * math.pi * (k + 0.5) / nd
            b.cyl(0.0, 0.035, 0.16, (R * math.cos(a), R * math.sin(a), z - 0.12), "CR_VidrioClaro", seg=4)
    for k in range(8):
        a = 2 * math.pi * k / 8
        b.tube([(0.15 * math.cos(a), 0.15 * math.sin(a), -0.95), (0.7 * math.cos(a), 0.7 * math.sin(a), -1.15), (1.2 * math.cos(a), 1.2 * math.sin(a), -1.35)], 0.02, "CR_Laton", sides=4)
    for k in range(12):
        a = 2 * math.pi * k / 12
        b.cyl(0.025, 0.025, 0.12, (1.2 * math.cos(a), 1.2 * math.sin(a), -1.25), "CR_Blanco", seg=6)
        b.ball(0.045, (1.2 * math.cos(a), 1.2 * math.sin(a), -1.15), "CR_Emit_Lampara", seg=6)
    b.cyl(0.0, 0.09, 0.4, (0, 0, -1.55), "CR_VidrioClaro", seg=6)
    return b.build()

def barandilla_lujo():   # brass and glass balustrade of the atrium stair, 2 m along X, 1.1 m
    b = CB("CR_BarandillaLujo", joint=1.0)
    for x in (-0.5, 0.5):
        b.cyl(0.035, 0.035, 1.05, (x, 0, 0.525), "CR_Laton", seg=8)
        b.ball(0.05, (x, 0, 1.12), "CR_Laton", seg=8)
    b.cyl(0.035, 0.035, 2.0, (0, 0, 1.07), "CR_Laton", seg=8, rot=[('Y', 90)])
    b.cyl(0.025, 0.025, 2.0, (0, 0, 0.08), "CR_Laton", seg=6, rot=[('Y', 90)])
    b.box((1.9, 0.02, 0.85), (0, 0, 0.55), "CR_VidrioClaro")
    return b.build()

def piano():   # baby grand with the lid closed (0.9 m) and a candelabra: low cover of the atrium; keyboard towards -Y
    b = CB("CR_Piano")
    s = 0.75
    pts = [(-0.75, -0.9), (0.75, -0.9), (0.75, -0.3), (0.6, 0.05), (0.3, 0.25), (0.12, 0.6), (0.05, 1.0), (-0.2, 1.25), (-0.55, 1.3), (-0.75, 1.15)]
    b.poly([(x * s, y * s + 0.25) for x, y in pts], 0.6, 0.9, "CR_Negro")
    for x, y in ((-0.45, -0.45), (0.45, -0.45), (-0.3, 1.0)): b.box((0.1, 0.1, 0.6), (x, y, 0.3), "CR_Negro")
    b.box((1.05, 0.28, 0.08), (0, -0.56, 0.7), "CR_Blanco")
    for i in range(14):
        if i % 7 in (2, 6): continue
        b.box((0.03, 0.14, 0.03), (-0.48 + i * 0.072, -0.5, 0.755), "CR_Negro")
    b.box((1.1, 0.06, 0.14), (0, -0.4, 0.82), "CR_Negro")
    b.box((0.5, 0.04, 0.25), (0, -0.25, 1.0), "CR_Negro", rot=[('X', -15)])
    b.cyl(0.07, 0.07, 0.03, (0.2, 0.55, 0.915), "CR_Laton", seg=8)
    b.cyl(0.02, 0.02, 0.17, (0.2, 0.55, 0.985), "CR_Laton", seg=6)
    for dx in (-0.12, 0.0, 0.12):
        b.cyl(0.022, 0.022, 0.14, (0.2 + dx, 0.55, 1.16), "CR_Blanco", seg=6)
        b.ball(0.025, (0.2 + dx, 0.55, 1.26), "CR_Emit_Farol", seg=6)
    b.box((0.26, 0.03, 0.02), (0.2, 0.55, 1.08), "CR_Laton")
    return b.build()

def tragaperras():   # LA TRAGAPERRAS: the golden slot machine = the mystery box of this map
    b = CB("CR_Tragaperras")
    b.box((1.4, 0.8, 0.9), (0, 0.05, 0.45), "CR_Rojo")
    b.box((1.3, 0.7, 1.0), (0, 0.1, 1.4), "CR_Dorado")
    b.box((1.4, 0.8, 0.35), (0, 0.05, 2.075), "CR_Rojo")
    b.box((1.3, 0.03, 0.25), (0, -0.36, 2.075), "CR_Negro")
    b.text("LA TRAGAPERRAS", 0.12, (0, -0.385, 2.075), "CR_Dorado", depth=0.02)
    for i in range(12):
        for z in (1.91, 2.24): b.ball(0.03, (-0.66 + i * 0.12, -0.35, z), "CR_Emit_Tragaperras", seg=6)
    b.box((1.0, 0.04, 0.45), (0, -0.26, 1.55), "CR_Negro")
    for x in (-0.3, 0.0, 0.3): b.box((0.26, 0.03, 0.36), (x, -0.295, 1.55), "CR_Emit_Tragaperras")
    b.ball(0.05, (-0.33, -0.33, 1.53), "CR_Rojo", seg=8); b.ball(0.05, (-0.27, -0.33, 1.51), "CR_Rojo", seg=8)
    b.box((0.015, 0.015, 0.1), (-0.3, -0.33, 1.6), "CR_Verde", rot=[('Y', 20)])
    b.cyl(0.08, 0.02, 0.12, (0.0, -0.33, 1.56), "CR_Amarillo", seg=8)
    b.text("7", 0.22, (0.3, -0.33, 1.55), "CR_Rojo", depth=0.02)
    b.text("JACKPOT", 0.09, (0, -0.27, 1.18), "CR_Rojo", depth=0.02)
    b.box((0.8, 0.25, 0.12), (0, -0.45, 0.95), "CR_Cromo")
    for i in range(5): b.cyl(0.035, 0.035, 0.02, (-0.2 + i * 0.09, -0.46, 1.02 + (i % 2) * 0.006), "CR_Dorado", seg=8)
    b.box((0.12, 0.2, 0.2), (0.71, 0.1, 1.4), "CR_Cromo")
    b.cyl(0.03, 0.03, 0.6, (0.79, 0.1, 1.72), "CR_Cromo", seg=6, rot=[('Y', 8)])
    b.ball(0.08, (0.83, 0.1, 2.03), "CR_Rojo", seg=8)
    return b.build()

def slot_machine(b, x):
    b.box((0.8, 0.6, 0.8), (x, 0.6, 0.4), "CR_Morado")
    b.box((0.75, 0.5, 0.9), (x, 0.65, 1.25), "CR_Gris")
    b.box((0.8, 0.6, 0.3), (x, 0.6, 1.85), "CR_Morado")
    b.box((0.55, 0.03, 0.45), (x, 0.385, 1.3), "CR_Emit_Tragaperras")
    b.box((0.7, 0.25, 0.06), (x, 0.32, 0.86), "CR_Negro", rot=[('X', -15)])
    for i in range(5): b.ball(0.03, (x - 0.3 + i * 0.15, 0.29, 1.9), "CR_Emit_Tragaperras", seg=6)

def tragaperras_fila():   # two plain slot machines with their stools (a cardigan and a handbag "keep" them)
    b = CB("CR_Tragaperras_Fila")
    for x in (-0.47, 0.47):
        slot_machine(b, x)
        b.cyl(0.2, 0.2, 0.08, (x, -0.45, 0.7), "CR_Terciopelo", seg=10)
        b.cyl(0.03, 0.03, 0.62, (x, -0.45, 0.35), "CR_Cromo", seg=6)
        b.cyl(0.2, 0.2, 0.03, (x, -0.45, 0.015), "CR_Cromo", seg=10)
    b.box((0.36, 0.42, 0.05), (-0.47, -0.48, 0.765), "CR_Celeste")
    b.box((0.36, 0.04, 0.3), (-0.47, -0.69, 0.62), "CR_Celeste")
    b.box((0.26, 0.12, 0.18), (0.47, -0.45, 0.83), "CR_Marron")
    b.ring(0.08, 0.012, (0.47, -0.45, 0.92), "CR_Marron", seg=8, sides=4, rot=[('X', 90)], arc=0.5)
    return b.build()

def ruleta():   # roulette table, 1.9 x 1.1 m, <= 0.9 m
    b = CB("CR_Ruleta")
    for x in (-0.8, 0.8):
        for y in (-0.4, 0.4): b.box((0.08, 0.08, 0.66), (x, y, 0.33), "CR_Madera")
    b.box((1.9, 1.1, 0.12), (0, 0, 0.72), "CR_Madera")
    b.box((1.8, 1.0, 0.02), (0, 0, 0.79), "CR_Verde")
    for s in (-1, 1): b.box((1.9, 0.05, 0.06), (0, s * 0.525, 0.81), "CR_Terciopelo")
    for i in range(4): b.box((0.02, 0.8, 0.02), (-0.1 + i * 0.28, 0, 0.81), "CR_Blanco")
    for j in range(3): b.box((0.95, 0.02, 0.02), (0.32, -0.3 + j * 0.3, 0.81), "CR_Blanco")
    b.cyl(0.35, 0.35, 0.06, (-0.6, 0, 0.84), "CR_Madera", seg=16)
    b.cyl(0.28, 0.28, 0.03, (-0.6, 0, 0.88), "CR_Rojo", seg=16)
    for k in range(6):
        n0 = b.mark()
        b.box((0.26, 0.08, 0.02), (0.13, 0, 0), "CR_Negro")
        b.move(n0, rot=[('Z', k * 60)], at=(-0.6, 0, 0.9))
    b.cyl(0.03, 0.03, 0.05, (-0.6, 0, 0.93), "CR_Dorado", seg=6)
    for x, y, m, n in ((0.04, 0.15, "CR_Rojo", 3), (0.6, -0.15, "CR_Azul", 2), (0.6, 0.15, "CR_Amarillo", 4)):
        b.cyl(0.03, 0.03, 0.012 * n, (x, y, 0.8 + 0.006 * n), m, seg=8)
    return b.build()

def escenario():   # theatre stage with the karaoke: 8 x 3 m platform 0.25 m, gold proscenium, curtains, footlights
    b = CB("CR_Escenario")
    b.box((8.0, 3.0, 0.25), (0, 0, 0.125), "CR_Madera")
    b.box((8.0, 0.03, 0.22), (0, -1.515, 0.12), "CR_Terciopelo")
    for i in range(8):
        x = -3.5 + i
        b.box((0.2, 0.12, 0.08), (x, -1.38, 0.29), "CR_Negro")
        b.ball(0.045, (x, -1.42, 0.33), "CR_Emit_Escenario", seg=6)
    for s in (-1, 1): b.box((0.56, 0.56, 3.0), (s * 3.74, -1.28, 1.5), "CR_Dorado")
    b.box((8.0, 0.5, 0.6), (0, -1.25, 3.3), "CR_Terciopelo")
    b.box((8.0, 0.04, 0.08), (0, -1.52, 3.05), "CR_Dorado")
    b.text("GALA DE KARAOKE", 0.34, (0, -1.515, 3.33), "CR_Dorado", depth=0.03)
    n = 14
    for i in range(n):
        x = -3.5 + (i + 0.5) * 7.0 / n
        b.box((7.0 / n, 0.06, 3.35), (x, 1.4 + (0.03 if i % 2 else -0.03), 1.925), "CR_Terciopelo")
    for s in (-1, 1):
        b.cyl(0.25, 0.15, 3.1, (s * 3.25, -0.95, 1.8), "CR_Terciopelo", seg=8)
        b.ring(0.2, 0.03, (s * 3.25, -0.95, 1.4), "CR_Dorado", seg=8, sides=4)
    b.cyl(0.15, 0.15, 0.03, (0, -0.6, 0.265), "CR_Negro", seg=8)
    b.cyl(0.015, 0.015, 1.35, (0, -0.6, 0.95), "CR_Cromo", seg=6)
    b.ball(0.05, (0, -0.62, 1.65), "CR_Negro", seg=8)
    b.cyl(0.03, 0.03, 1.0, (-2.2, -0.3, 0.75), "CR_Negro", seg=6)
    b.box((1.2, 0.08, 0.7), (-2.2, -0.3, 1.6), "CR_Negro")
    b.box((1.1, 0.03, 0.6), (-2.2, -0.355, 1.6), "CR_Emit_Escenario")
    for z in (1.5, 1.65): b.box((0.8, 0.02, 0.05), (-2.2, -0.38, z), "CR_Blanco")
    for s in (-1, 1):
        b.box((0.5, 0.45, 0.9), (s * 3.3, -0.2, 0.7), "CR_Negro")
        b.disc(0.16, 0.04, (s * 3.3, -0.43, 0.82), "CR_Gris", seg=10)
        b.disc(0.08, 0.04, (s * 3.3, -0.43, 1.05), "CR_Gris", seg=8)
        b.box((0.2, 0.2, 0.25), (s * 1.6, -1.25, 2.85), "CR_Negro")
        b.cyl(0.07, 0.07, 0.04, (s * 1.6, -1.25, 2.71), "CR_Emit_Escenario", seg=8)
    return b.build()

def mesa_redonda():   # dining room table for six with tablecloth, plates, candle and gold banquet chairs
    b = CB("CR_MesaRedonda")
    b.cyl(0.66, 0.66, 0.3, (0, 0, 0.63), "CR_Blanco", seg=14)
    b.cyl(0.08, 0.1, 0.48, (0, 0, 0.24), "CR_Dorado", seg=8)
    for k in range(6):
        a = math.radians(30 + 60 * k)
        ca, sa = math.cos(a), math.sin(a)
        b.cyl(0.11, 0.11, 0.02, (0.47 * ca, 0.47 * sa, 0.79), "CR_Crema", seg=10)
        b.cyl(0.025, 0.02, 0.1, (0.5 * ca + 0.16 * sa, 0.5 * sa - 0.16 * ca, 0.83), "CR_VidrioClaro", seg=6)
        n0 = b.mark()
        b.box((0.42, 0.4, 0.06), (0, 0, 0.46), "CR_Terciopelo")
        b.box((0.42, 0.05, 0.5), (0, 0.2, 0.74), "CR_Dorado")
        for dx in (-0.18, 0.18):
            for dy in (-0.16, 0.16): b.cyl(0.015, 0.015, 0.43, (dx, dy, 0.215), "CR_Dorado", seg=5)
        b.move(n0, rot=[('Z', math.degrees(a) - 90)], at=(0.78 * ca, 0.78 * sa, 0))
    b.cyl(0.06, 0.06, 0.03, (0, 0, 0.795), "CR_Laton", seg=8)
    b.cyl(0.02, 0.02, 0.2, (0, 0, 0.91), "CR_Blanco", seg=6)
    b.ball(0.025, (0, 0, 1.03), "CR_Emit_Farol", seg=6)
    for k in range(4): b.ball(0.05, (0.12 * math.cos(k * 1.57), 0.12 * math.sin(k * 1.57), 0.83), "CR_Rosa", seg=6)
    return b.build()

def bufe():   # the buffet: paella, croquetas, flan, heat lamps and the rule about the tuppers
    b = CB("CR_Bufe")
    b.box((1.95, 0.75, 0.9), (0, 0, 0.45), "CR_Madera")
    b.box((1.95, 0.8, 0.04), (0, 0, 0.92), "CR_Cromo")
    for i, x in enumerate((-0.62, 0.0, 0.62)):
        b.box((0.55, 0.35, 0.06), (x, 0.05, 0.97), "CR_Cromo")
    b.cyl(0.24, 0.24, 0.03, (-0.62, 0.05, 1.01), "CR_Amarillo", seg=12)
    for k in range(5): b.ball(0.03, (-0.62 + 0.14 * math.cos(k * 1.26), 0.05 + 0.14 * math.sin(k * 1.26), 1.04), "CR_Rojo" if k % 2 else "CR_Verde", seg=6)
    for k in range(6): b.ball(0.035, (-0.1 + (k % 3) * 0.1, (k // 3) * 0.1, 1.03), "CR_Marron", scale=(1.4, 1, 1), seg=6)
    for k in range(3):
        b.cyl(0.05, 0.06, 0.06, (0.5 + k * 0.12, 0.05, 1.03), "CR_Amarillo", seg=8)
        b.cyl(0.05, 0.05, 0.03, (0.5 + k * 0.12, 0.05, 1.075), "CR_Marron", seg=8)
    for x in (-0.9, 0.9):
        b.box((0.04, 0.04, 0.7), (x, 0.3, 1.29), "CR_Cromo")
    b.box((1.9, 0.02, 0.45), (0, -0.12, 1.32), "CR_VidrioClaro", rot=[('X', 30)])
    b.box((1.9, 0.08, 0.06), (0, 0.3, 1.64), "CR_Cromo")
    for x in (-0.62, 0.0, 0.62):
        b.box((0.3, 0.2, 0.08), (x, 0.2, 1.58), "CR_Gris")
        b.box((0.24, 0.14, 0.03), (x, 0.2, 1.525), "CR_Emit_Farol")
    b.box((1.25, 0.02, 0.2), (0, -0.385, 0.65), "CR_Blanco")
    b.text("PROHIBIDO LLEVARSE TÁPERS", 0.065, (0, -0.405, 0.65), "CR_Rojo", depth=0.02)
    return b.build()

def corridor_base(b):
    b.box((2.0, 0.06, 3.4), (0, -0.03, 1.7), "CR_Crema")
    b.box((2.0, 0.1, 0.2), (0, -0.05, 3.5), "M_Wood")
    b.box((2.0, 0.09, 0.15), (0, -0.045, 0.075), "CR_Madera")

def sconce(b, x, z):
    b.box((0.14, 0.03, 0.22), (x, -0.075, z), "CR_Laton")
    b.ball(0.09, (x, -0.13, z), "CR_Emit_Farol", scale=(1, 0.6, 1.3), seg=8)

def puerta_camarote(name, dx, hanger):   # corridor wall with a numbered cabin door (CR_DoorPaint: spawn doors go green)
    b = CB(name, joint=1.0, wall=True, ceil=ROOM)
    corridor_base(b)
    b.box((1.1, 0.05, 2.25), (dx, -0.085, 1.125), "M_Wood")
    b.box((0.9, 0.05, 2.1), (dx, -0.115, 1.05), "CR_DoorPaint")
    b.box((0.18, 0.02, 0.1), (dx, -0.15, 1.7), "CR_Laton")
    hx = dx + 0.35 * (1 if dx > 0 else -1)
    b.cyl(0.03, 0.03, 0.06, (hx, -0.17, 1.0), "CR_Laton", seg=6, rot=[('X', 90)])
    b.box((0.14, 0.03, 0.025), (hx - 0.05 * (1 if dx > 0 else -1), -0.2, 1.0), "CR_Laton")
    b.disc(0.015, 0.04, (dx, -0.16, 1.55), "CR_Negro", seg=6)
    if hanger:
        b.box((0.08, 0.01, 0.2), (hx, -0.22, 0.85), "CR_Rojo")
    rail_x0, rail_x1 = (-1.0, dx - 0.6) if dx > 0 else (dx + 0.6, 1.0)
    b.cyl(0.03, 0.03, rail_x1 - rail_x0, ((rail_x0 + rail_x1) / 2, -0.15, 0.92), "M_Wood", seg=6, rot=[('Y', 90)])
    bx = (rail_x0 + rail_x1) / 2
    b.cyl(0.015, 0.015, 0.12, (bx, -0.09, 0.92), "CR_Laton", seg=6, rot=[('X', 90)])
    sconce(b, (rail_x0 + rail_x1) / 2, 2.3)
    return b.build()

def pasillo():   # corridor wall without a door: handrail, a painting of the ship itself, two sconces
    b = CB("CR_Pasillo", joint=1.0, wall=True, ceil=ROOM)
    corridor_base(b)
    b.cyl(0.03, 0.03, 2.0, (0, -0.15, 0.92), "M_Wood", seg=6, rot=[('Y', 90)])
    for x in (-0.5, 0.5): b.cyl(0.015, 0.015, 0.12, (x, -0.09, 0.92), "CR_Laton", seg=6, rot=[('X', 90)])
    b.box((0.9, 0.04, 0.6), (0, -0.08, 1.75), "CR_Dorado")
    b.box((0.8, 0.02, 0.25), (0, -0.11, 1.6), "CR_Azul")
    b.box((0.8, 0.02, 0.25), (0, -0.11, 1.85), "CR_Celeste")
    b.box((0.4, 0.02, 0.08), (0.05, -0.13, 1.74), "CR_Blanco")
    b.box((0.08, 0.02, 0.1), (0.0, -0.13, 1.83), "CR_Amarillo")
    for x in (-0.75, 0.75): sconce(b, x, 2.3)
    return b.build()

def litera():   # bunk bed 2 x 0.95 x 1.85 along X: blankets, ladder, a suitcase on top, a walking stick
    b = CB("CR_Litera")
    for x in (-0.97, 0.97):
        for y in (-0.445, 0.445): b.box((0.06, 0.06, 1.85), (x, y, 0.925), "M_Wood")
    for z in (0.3, 1.3):
        b.box((1.96, 0.91, 0.15), (0, 0, z), "CR_Madera")
        b.box((1.9, 0.86, 0.14), (0, 0, z + 0.145), "CR_Blanco")
        b.box((1.3, 0.9, 0.04), (0.3, 0, z + 0.235), "CR_Azul")
        b.box((0.35, 0.6, 0.12), (-0.72, 0, z + 0.275), "CR_Crema")
    b.box((1.2, 0.05, 0.2), (0.2, -0.45, 1.65), "M_Wood")
    for d in (-0.2, 0.2): b.box((0.04, 0.04, 1.6), (1.03, d, 0.8), "M_Wood")
    for z in (0.5, 0.85, 1.2): b.cyl(0.018, 0.018, 0.4, (1.03, 0, z), "M_Wood", seg=5, rot=[('X', 90)])
    b.box((0.55, 0.3, 0.38), (0.3, 0.1, 1.75), "CR_Rojo", rot=[('Z', 6)])
    b.ring(0.06, 0.012, (0.3, 0.1, 1.95), "CR_Negro", seg=8, sides=4, rot=[('X', 90), ('Z', 6)], arc=0.5)
    b.cyl(0.015, 0.015, 0.85, (-0.6, -0.5, 0.43), "CR_Marron", seg=5, rot=[('X', 8)])
    b.ring(0.06, 0.015, (-0.6, -0.56, 0.86), "CR_Marron", seg=8, sides=4, rot=[('X', 90)], arc=0.5)
    return b.build()

def motor():   # engine of the engine room (1.9 x 1.9, 2.2 m + the exhaust up to the ceiling)
    b = CB("CR_Motor")
    b.box((1.9, 1.6, 0.3), (0, 0, 0.15), "CR_Gris")
    b.box((1.5, 1.1, 1.1), (0, 0, 0.85), "CR_VerdeMaquina")
    for i in range(6): b.box((0.22, 0.9, 0.25), (-0.6 + i * 0.24, 0, 1.525), "CR_VerdeMaquina")
    b.cyl(0.12, 0.12, 1.4, (0, -0.65, 1.35), "CR_Oxido", seg=8, rot=[('Y', 90)])
    for i in range(6): b.cyl(0.05, 0.05, 0.25, (-0.6 + i * 0.24, -0.5, 1.4), "CR_Oxido", seg=6, rot=[('X', 90)])
    b.cyl(0.2, 0.2, 0.35, (0.8, -0.65, 1.35), "CR_Oxido", seg=10, rot=[('Y', 90)])
    b.cyl(0.18, 0.18, 2.25, (0.8, -0.65, 2.475), "CR_Oxido", seg=10)
    for z in (2.0, 2.6, 3.2): b.cyl(0.2, 0.2, 0.06, (0.8, -0.65, z), "CR_Blanco", seg=10)
    b.cyl(0.55, 0.55, 0.2, (-0.85, 0, 0.8), "CR_Gris", seg=14, rot=[('Y', 90)])
    b.box((0.4, 0.05, 0.3), (0.2, -0.575, 0.75), "CR_Negro")
    for x in (0.08, 0.2, 0.32): b.disc(0.045, 0.03, (x, -0.61, 0.78), "CR_Blanco", seg=10)
    b.ball(0.05, (0.45, -0.6, 0.95), "CR_Emit_Cuadro", seg=6)
    b.cyl(0.07, 0.08, 0.18, (0.6, 0.66, 0.39), "CR_Rojo", seg=8)
    b.cyl(0.01, 0.01, 0.18, (0.66, 0.66, 0.5), "CR_Rojo", seg=4, rot=[('Y', 40)])
    b.box((0.25, 0.18, 0.02), (-0.4, 0.68, 0.31), "CR_Crema", rot=[('Z', 20)])
    return b.build()

def cuadro_electrico():   # switchboard: the big lever gives power to the lifeboat davits
    b = CB("CR_CuadroElectrico")
    b.box((1.95, 0.55, 2.1), (0, 0.05, 1.05), "CR_Gris")
    for x in (-0.64, 0.0, 0.64):
        b.box((0.6, 0.02, 1.7), (x, -0.235, 1.0), "CR_Acero")
        b.disc(0.08, 0.03, (x - 0.12, -0.255, 1.6), "CR_Blanco", seg=10)
        b.ball(0.035, (x + 0.15, -0.25, 1.62), "CR_Emit_Cuadro", seg=6)
    b.box((0.3, 0.1, 0.3), (0, -0.29, 1.15), "CR_Negro")
    b.box((0.05, 0.05, 0.5), (0, -0.36, 1.35), "CR_Gris", rot=[('X', 25)])
    b.ball(0.07, (0, -0.47, 1.57), "CR_Rojo", seg=8)
    b.box((0.7, 0.02, 0.16), (-0.64, -0.255, 0.4), "CR_Azul")
    b.text("PESCANTES", 0.08, (-0.64, -0.27, 0.4), "CR_Blanco", depth=0.02)
    b.cyl(0.2, 0.2, 0.02, (0.64, -0.255, 0.55), "CR_Amarillo", seg=3, rot=[('X', 90), ('Y', 30)])
    b.text("!", 0.18, (0.64, -0.27, 0.53), "CR_Negro", depth=0.02)
    b.box((0.4, 0.4, 1.5), (0.6, 0.1, 2.85), "CR_Gris")
    return b.build()

def moto_agua():   # jet ski 3 m, bow towards -Y, origin at the waterline
    b = CB("CR_MotoAgua")
    half = [(0.0, -0.25), (0.35, -0.18), (1.0, 0.0), (1.0, 0.28), (0.6, 0.42), (0.0, 0.46)]
    secs = []
    for i in range(9):
        t = i / 8
        y = -1.5 + 3.0 * t
        w = 0.04 + 0.51 * math.sin(math.pi * min(t * 1.6, 1.0) / 2)
        lift, zs = 0.22 * (1 - t) ** 3, 0.45 + 0.55 * min(t * 2.5, 1.0)   # raked, lower bow
        loop = [(w * a, z) for a, z in half] + [(-w * a, z) for a, z in reversed(half[1:-1])]
        secs.append([Vector((x, y, z * zs + lift)) for x, z in loop])
    b.loft(secs, lambda c, n: "CR_JetskiPaint" if c is None or c.z > 0.3 else "CR_Blanco")
    b.box((0.4, 1.0, 0.16), (0, 0.55, 0.53), "CR_Negro")
    b.box((0.18, 0.18, 0.4), (0, -0.4, 0.62), "CR_Negro", rot=[('X', -25)])
    b.cyl(0.025, 0.025, 0.64, (0, -0.5, 0.84), "CR_Gris", seg=6, rot=[('Y', 90)])
    for s in (-1, 1): b.cyl(0.035, 0.035, 0.12, (s * 0.38, -0.5, 0.84), "CR_Negro", seg=6, rot=[('Y', 90)])
    b.cyl(0.08, 0.1, 0.2, (0, 1.55, 0.1), "CR_Gris", seg=8, rot=[('X', 90)])
    b.box((0.36, 0.14, 0.42), (0.2, -0.58, 0.7), "CR_Naranja", rot=[('Z', 15)])
    return b.build()

def escalerilla():   # steel ladder hanging from the deck edge into the sea (-Y = outboard), grab handles over the rail
    b = CB("CR_Escalerilla")
    for x in (-0.25, 0.25):
        b.box((0.06, 0.06, 5.9), (x, -0.4, -2.35), "CR_Acero")
        b.tube([(x, -0.4, 0.55), (x, -0.4, 1.25), (x, -0.15, 1.42), (x, 0.2, 1.3), (x, 0.32, 0.0)], 0.03, "CR_Acero", sides=6)
        for z in (-0.6, -2.6, -4.6): b.box((0.05, 0.27, 0.05), (x, -0.235, z), "CR_Gris")
    for k in range(16): b.cyl(0.02, 0.02, 0.5, (0, -0.4, -5.1 + k * 0.35), "CR_Acero", seg=6, rot=[('Y', 90)])
    return b.build()

# ================================================================== the port
def mostrador():   # check-in counter (tiles along X): scale, monitor, bell, the FACTURACION sign, belt behind
    b = CB("CR_Mostrador", joint=1.0)
    b.box((1.95, 0.7, 1.05), (0, 0.05, 0.525), "CR_Azul")
    b.box((2.0, 0.8, 0.05), (0, 0.0, 1.075), "CR_Blanco")
    b.box((2.0, 0.02, 0.12), (0, -0.31, 0.8), "CR_Amarillo")
    b.box((0.6, 0.55, 0.12), (0.55, -0.62, 0.06), "CR_Gris")
    b.box((0.2, 0.1, 0.14), (0.55, -0.34, 0.95), "CR_Negro")
    b.box((0.45, 0.05, 0.32), (-0.3, 0.2, 1.36), "CR_Negro")
    b.box((0.06, 0.06, 0.15), (-0.3, 0.24, 1.17), "CR_Gris")
    b.box((0.4, 0.15, 0.02), (-0.3, -0.05, 1.11), "CR_Gris")
    b.ball(0.04, (0.2, -0.2, 1.1), "CR_Laton", scale=(1, 1, 0.8), seg=8)
    b.box((2.0, 0.5, 0.1), (0, 0.72, 0.55), "CR_Goma")
    b.cyl(0.03, 0.03, 2.5, (0.9, 0.42, 1.25), "CR_Gris", seg=6)
    b.box((0.9, 0.06, 0.3), (0.5, 0.42, 2.4), "CR_Azul")
    b.text("FACTURACIÓN", 0.09, (0.5, 0.38, 2.4), "CR_Blanco", depth=0.02)
    return b.build()

def cinta_equipaje():   # luggage belt segment 2 m along X with two suitcases
    b = CB("CR_CintaEquipaje", joint=1.0)
    for s in (-1, 1):
        b.box((2.0, 0.08, 0.45), (0, s * 0.42, 0.275), "CR_Acero")
        b.box((2.0, 0.04, 0.12), (0, s * 0.42, 0.55), "CR_Gris")
    b.box((2.0, 0.76, 0.06), (0, 0, 0.48), "CR_Goma")
    for x in (-0.8, 0.8):
        for s in (-1, 1): b.box((0.06, 0.06, 0.05), (x, s * 0.42, 0.025), "CR_Gris")
    b.box((0.7, 0.45, 0.22), (-0.45, 0.02, 0.62), "CR_Rojo", rot=[('Z', 8)])
    b.box((0.55, 0.38, 0.25), (0.5, -0.05, 0.635), "CR_Morado", rot=[('Z', -5)])
    return b.build()

def estante_tienda():   # duty-free shelving, double sided: bottles, tobacco, perfume, a flamenco doll and a toy bull
    b = CB("CR_EstanteTienda", joint=1.0)
    for x in (-0.95, 0.0, 0.95): b.box((0.05, 0.6, 1.9), (x, 0, 0.95), "CR_Blanco")
    b.box((1.9, 0.03, 1.8), (0, 0, 0.95), "CR_Blanco")
    for z in (0.1, 0.55, 1.0, 1.45): b.box((1.9, 0.6, 0.03), (0, 0, z), "CR_Blanco")
    b.box((1.9, 0.04, 0.25), (0, 0, 2.05), "CR_Rojo")
    b.text("DUTY FREE", 0.13, (0, -0.035, 2.05), "CR_Blanco", depth=0.02)
    b.text("LIBRE DE IMPUESTOS", 0.09, (0, 0.035, 2.05), "CR_Blanco", depth=0.02, rot=(('X', 90), ('Z', 180)))
    rnd = random.Random(21)
    for s in (-1, 1):
        for zi, z in enumerate((0.115, 0.565, 1.015)):
            x = -0.9
            while x < 0.82:
                kind = rnd.choice(("bottle", "bottle", "box", "perfume", "jug"))
                if kind == "bottle":
                    h = rnd.uniform(0.24, 0.32)
                    b.cyl(0.04, 0.04, h, (x + 0.05, s * 0.18, z + h / 2), rnd.choice(("CR_Verde", "CR_Marron", "CR_VidrioClaro")), seg=6)
                    b.cyl(0.015, 0.015, 0.06, (x + 0.05, s * 0.18, z + h + 0.03), "CR_Dorado", seg=5)
                    x += 0.13
                elif kind == "box":
                    b.box((0.16, 0.12, 0.22), (x + 0.09, s * 0.2, z + 0.11), rnd.choice(("CR_Blanco", "CR_Rojo", "CR_Dorado")), rot=[('Z', rnd.uniform(-6, 6))])
                    x += 0.21
                elif kind == "perfume":
                    b.box((0.08, 0.06, 0.12), (x + 0.05, s * 0.2, z + 0.06), "CR_Rosa")
                    b.ball(0.02, (x + 0.05, s * 0.2, z + 0.14), "CR_Dorado", seg=6)
                    x += 0.12
                else:
                    b.cyl(0.07, 0.06, 0.22, (x + 0.08, s * 0.18, z + 0.11), "CR_Rojo", seg=8)
                    x += 0.19
        n0 = b.mark()
        b.cyl(0.0, 0.1, 0.22, (0, 0, 0.11), "CR_Rojo", seg=8, rot=[('X', 180)])
        b.ball(0.05, (0, 0, 0.27), "CR_Carne", seg=6)
        b.ball(0.05, (0, 0, 0.31), "CR_Negro", seg=6)
        b.box((0.24, 0.1, 0.1), (0.3, 0, 0.1), "CR_Negro")
        for d in (-1, 1): b.cyl(0.012, 0.0, 0.08, (0.43, d * 0.05, 0.17), "CR_Blanco", seg=4, rot=[('X', -d * 40)])
        b.move(n0, rot=[('Z', 0 if s < 0 else 180)], at=(-0.3 * s, s * 0.18, 1.465))
    return b.build()

def container(b, at=(0, 0, 0), paint="CR_ContainerPaint", rz=0.0, logo=True):   # 3.9 x 1.9 x 2.6 along X
    n0 = b.mark()
    b.box((3.86, 1.86, 2.56), (0, 0, 1.3), paint)
    for sx in (-1, 1):
        for sy in (-1, 1): b.box((0.15, 0.15, 2.6), (sx * 1.875, sy * 0.875, 1.3), "CR_Gris")
        for z in (0.06, 2.54): b.box((3.6, 0.1, 0.12), (0, sx * 0.9, z), "CR_Gris")
        for k in range(14): b.box((0.08, 0.03, 2.3), (-1.62 + k * 0.25, sx * 0.945, 1.3), paint)
        for z in (0.06, 2.54): b.box((0.1, 1.6, 0.12), (sx * 1.9, 0, z), "CR_Gris")
    for d in (-0.45, 0.45):
        b.box((0.02, 0.78, 2.3), (1.94, d * 0.9, 1.3), paint)
        for e in (-0.2, 0.2): b.cyl(0.025, 0.025, 2.2, (1.97, d + e, 1.3), "CR_Gris", seg=6)
    if logo:
        b.text("IMSERSO LINES", 0.32, (0, -0.975, 1.5), "CR_Blanco", depth=0.03)
        b.text("IMSERSO LINES", 0.32, (0, 0.975, 1.5), "CR_Blanco", depth=0.03, rot=(('X', 90), ('Z', 180)))
    b.move(n0, rot=[('Z', rz)], at=at)

def contenedor():
    b = CB("CR_Contenedor")
    container(b)
    return b.build()

def contenedores():   # two stacked (the upper one turned a little): blocks the view on the quay
    b = CB("CR_Contenedores")
    container(b)
    container(b, at=(0.1, 0.05, 2.6), paint="CR_Container2Paint", rz=3.0, logo=False)
    return b.build()

def maletas():   # the retirees' luggage on a pallet: suitcases, guitar case, walker, birdcage, a ham leg
    b = CB("CR_Maletas")
    b.box((1.9, 1.9, 0.14), (0, 0, 0.07), "M_Wood")
    items = [((0.8, 0.55, 0.3), (-0.45, -0.45, 0.29), 3, "CR_Rojo"), ((0.75, 0.5, 0.32), (0.45, -0.5, 0.3), -4, "CR_Azul"),
             ((0.8, 0.6, 0.28), (-0.4, 0.4, 0.28), -2, "CR_Verde"), ((0.7, 0.55, 0.34), (0.45, 0.42, 0.31), 5, "CR_Morado"),
             ((0.75, 0.5, 0.26), (-0.35, -0.3, 0.6), 9, "CR_Marron"), ((0.65, 0.45, 0.3), (0.4, -0.1, 0.63), -7, "CR_Rosa"),
             ((0.7, 0.5, 0.24), (-0.3, 0.45, 0.56), 4, "CR_Amarillo"), ((0.6, 0.4, 0.5), (0.1, 0.1, 1.04), 12, "CR_Celeste"),
             ((0.5, 0.35, 0.28), (-0.45, -0.25, 0.9), -10, "CR_Rojo"), ((0.45, 0.3, 0.22), (0.15, 0.05, 1.4), 20, "CR_Verde")]
    for i, (size, at, rz, m) in enumerate(items):
        b.box(size, at, m, rot=[('Z', rz)])
        if i not in (7, 9): b.box((0.16, 0.03, 0.04), (at[0], at[1], at[2] + size[2] / 2 + 0.02), "CR_Negro", rot=[('Z', rz)])
    n0 = b.mark()
    b.ball(0.2, (0, 0, 0), "CR_Negro", scale=(1, 0.4, 1.0), seg=8)
    b.ball(0.13, (0, 0, 0.32), "CR_Negro", scale=(1, 0.4, 1.0), seg=8)
    b.box((0.1, 0.08, 0.5), (0, 0, 0.6), "CR_Negro")
    b.move(n0, rot=[('Y', -6)], at=(0.72, -0.86, 0.36))
    for x in (-0.82, -0.52):
        for y in (0.35, 0.75): b.cyl(0.015, 0.015, 0.92, (x, y, 1.14), "CR_Cromo", seg=5)
    for x in (-0.82, -0.52): b.tube([(x, 0.35, 1.6), (x, 0.55, 1.68), (x, 0.75, 1.6)], 0.018, "CR_Cromo", sides=5)
    b.cyl(0.15, 0.15, 0.02, (0.2, 0.15, 1.52), "CR_Laton", seg=10)
    for k in range(10):
        a = 2 * math.pi * k / 10
        b.cyl(0.006, 0.006, 0.3, (0.2 + 0.14 * math.cos(a), 0.15 + 0.14 * math.sin(a), 1.68), "CR_Laton", seg=4)
    b.ball(0.15, (0.2, 0.15, 1.83), "CR_Laton", scale=(1, 1, 0.5), seg=8)
    b.ball(0.05, (0.2, 0.15, 1.62), "CR_Amarillo", seg=6)
    b.cyl(0.06, 0.13, 0.55, (-0.6, -0.65, 1.2), "CR_Marron", seg=8, rot=[('Y', 70)])
    return b.build()

def carretilla():   # forklift with a pallet of suitcases on the forks (forks towards -Y)
    b = CB("CR_Carretilla")
    b.box((1.1, 1.5, 0.7), (0, 0.3, 0.6), "CR_Amarillo")
    b.box((1.14, 0.4, 0.76), (0, 1.1, 0.65), "CR_Gris")
    for x in (-0.5, 0.5):
        for y in (-0.25, 0.85): b.cyl(0.28, 0.28, 0.2, (x * 1.06, y, 0.28), "CR_Goma", seg=10, rot=[('Y', 90)])
    for x in (-0.5, 0.5):
        for y in (-0.3, 0.8): b.box((0.06, 0.06, 1.2), (x, y, 1.55), "CR_Negro")
    b.box((1.1, 1.2, 0.06), (0, 0.25, 2.18), "CR_Negro")
    b.ball(0.08, (0.4, 0.7, 2.27), "CR_Naranja", seg=6)
    b.box((0.45, 0.4, 0.12), (0, 0.45, 1.0), "CR_Negro")
    b.box((0.45, 0.08, 0.45), (0, 0.65, 1.25), "CR_Negro")
    b.cyl(0.025, 0.025, 0.4, (0, 0.0, 1.1), "CR_Negro", seg=6, rot=[('X', -30)])
    b.ring(0.15, 0.02, (0, -0.1, 1.28), "CR_Negro", seg=10, sides=4, rot=[('X', -30)])
    for x in (-0.35, 0.35): b.box((0.1, 0.1, 2.2), (x, -0.55, 1.1), "CR_Gris")
    b.box((0.9, 0.06, 0.4), (0, -0.65, 0.75), "CR_Gris")
    for x in (-0.25, 0.25): b.box((0.12, 1.0, 0.05), (x, -1.15, 0.58), "CR_Gris")
    b.box((1.0, 1.0, 0.12), (0, -1.15, 0.665), "M_Wood")
    b.box((0.7, 0.5, 0.28), (-0.1, -1.2, 0.865), "CR_Rojo", rot=[('Z', 6)])
    b.box((0.55, 0.4, 0.3), (0.15, -1.1, 1.155), "CR_Azul", rot=[('Z', -9)])
    return b.build()

def bolardo():   # double bitt on the quay edge with a slack mooring rope going over the edge (-Y)
    b = CB("CR_Bolardo")
    b.box((1.0, 0.5, 0.08), (0, 0, 0.04), "CR_Negro")
    for x in (-0.27, 0.27):
        b.cyl(0.16, 0.14, 0.6, (x, 0, 0.38), "CR_Negro", seg=10)
        b.cyl(0.2, 0.2, 0.06, (x, 0, 0.71), "CR_Negro", seg=10)
        b.ring(0.2, 0.045, (x, 0, 0.42), "CR_Cuerda", seg=10, sides=5)
    b.tube([(0.3, -0.2, 0.42), (0.2, -0.8, 0.35), (0.1, -1.4, 0.1), (0.0, -1.9, -0.5), (0.0, -2.2, -1.3)], 0.045, "CR_Cuerda", sides=6)
    return b.build()

def grua():   # harbour portal crane, 6 x 6 m portal, jib to about 31 m
    b = CB("CR_Grua")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.6, 0.6, 9.0), (sx * 3.0, sy * 3.0, 4.5), "CR_Amarillo")
            b.box((1.2, 0.8, 0.5), (sx * 3.0, sy * 3.0, 0.25), "CR_Gris")
        b.box((6.6, 0.5, 0.8), (0, sx * 3.0, 8.8), "CR_Amarillo")
        b.box((0.5, 5.4, 0.8), (sx * 3.0, 0, 8.8), "CR_Amarillo")
    b.cyl(2.5, 2.5, 0.6, (0, 0, 9.5), "CR_Gris", seg=12)
    b.box((4.5, 3.5, 3.5), (0, 0.8, 11.55), "CR_Amarillo")
    b.box((4.52, 0.04, 0.6), (0, -0.97, 12.2), "CR_Cristal")
    b.box((1.6, 1.8, 2.0), (1.5, -1.9, 10.8), "CR_Blanco")
    b.box((1.4, 0.04, 0.9), (1.5, -2.82, 11.2), "CR_Cristal")
    b.box((3.6, 1.6, 1.6), (0, 3.2, 11.0), "CR_Gris")
    for s in (-1, 1): b.box((0.3, 0.3, 4.2), (s * 1.2, 0.8, 14.8), "CR_Amarillo", rot=[('Y', -s * 12)])
    L, ang = 26.0, math.radians(50)
    p0 = Vector((0, -1.2, 10.5))
    d = Vector((0, -math.cos(ang), math.sin(ang)))
    for k in range(10):
        a, c = p0 + d * (L * k / 10), p0 + d * (L * (k + 1) / 10)
        for x in (-0.6, 0.6):
            b.box((0.3, 0.3, L / 10), (x, (a.y + c.y) / 2, (a.z + c.z) / 2), "CR_Rojo" if k % 2 else "CR_Blanco", rot=[('X', -math.degrees(ang) + 90)])
        b.tube([(-0.6, a.y, a.z), (0.6, c.y, c.z)], 0.06, "CR_Gris", sides=4)
    tip = p0 + d * L
    b.tube([(0, 0.8, 16.8), tuple(tip)], 0.04, "CR_Negro", sides=4)
    b.tube([tuple(tip), (0, tip.y, 13.0)], 0.03, "CR_Negro", sides=4)
    b.box((0.6, 0.4, 0.8), (0, tip.y, 12.6), "CR_Amarillo")
    b.ring(0.18, 0.06, (0, tip.y, 12.0), "CR_Gris", seg=8, sides=4, rot=[('X', 90)], arc=0.6)
    return b.build()

def surf(y):   # walking surface of the gangway (rises towards -Y): run, landing, run
    if y >= 1.0: return (5.0 - y) * 0.5
    if y >= -1.0: return 2.0
    return 2.0 + (-1.0 - y) * 0.5

def pasarela():
    """The covered gangway over the harbour water (cells 32-33 x 17-21: 4 m wide, 10 m long, quay end at +Y z 0, ship end at
    -Y z 4): side posts, rails, canvas skirts with BIENVENIDOS A BORDO, sloped canopy and the truss under the walkway.
    No floor: the setup builds the ramps. It is one piece so it can be pulled back when the ship sails."""
    b = CB("CR_Pasarela")
    ys = [5.0, 1.0, -1.0, -5.0]
    for s in (-1, 1):
        x = s * 2.2
        for y in (5.0, 3.0, 1.0, -1.0, -3.0, -5.0): b.cyl(0.06, 0.06, 2.8, (x, y, surf(y) + 1.0), "CR_Blanco", seg=6)
        for h in (0.5, 1.0): b.tube([(s * 2.05, y, surf(y) + h) for y in ys], 0.03, "CR_Blanco", sides=6, caps=True)
        pts = [(y, surf(y)) for y in ys] + [(y, surf(y) + 0.95) for y in reversed(ys)]
        b.poly([(-z, y) for y, z in pts], s * 2.1 - 0.01, s * 2.1 + 0.01, "M_Canvas", rot=[('Y', 90)])
        for y0, y1 in ((5.0, 1.0), (1.0, -1.0), (-1.0, -5.0)):
            ln, a = math.hypot(y1 - y0, surf(y1) - surf(y0)), math.degrees(math.atan2(surf(y1) - surf(y0), y0 - y1))
            b.box((0.1, ln, 0.18), (s * 1.95, (y0 + y1) / 2, (surf(y0) + surf(y1)) / 2 - 0.3), "CR_Gris", rot=[('X', -a)])
            b.box((0.05, ln, 0.3), (s * 2.3, (y0 + y1) / 2, (surf(y0) + surf(y1)) / 2 + 2.25), "CR_Azul", rot=[('X', -a)])
        rz = 90 * s
        b.text("BIENVENIDOS A BORDO", 0.24, (s * 2.135, 3.0, surf(3.0) + 0.5), "CR_Azul", depth=0.03, rot=(('X', 90), ('Z', rz), ('X', -26.565)))
    for y0, y1 in ((5.0, 1.0), (1.0, -1.0), (-1.0, -5.0)):
        ln, a = math.hypot(y1 - y0, surf(y1) - surf(y0)), math.degrees(math.atan2(surf(y1) - surf(y0), y0 - y1))
        b.box((4.7, ln + 0.1, 0.06), (0, (y0 + y1) / 2, (surf(y0) + surf(y1)) / 2 + 2.45), "CR_AwningPaint", rot=[('X', -a)])
    for y in (4.9, 3.0, 1.0, -1.0, -3.0, -4.9): b.box((3.8, 0.12, 0.12), (0, y, surf(y) - 0.32), "CR_Gris")
    return b.build()

def puerta_embarque():   # the boarding gate Q over the 4 m gangway mouth (the leaves are CR_PuertaEmbarque_Hoja)
    b = CB("CR_PuertaEmbarque")
    for s in (-1, 1):
        b.box((0.25, 0.25, 3.2), (s * 2.2, 0, 1.6), "CR_Acero")
        b.box((0.4, 0.4, 0.1), (s * 2.2, 0, 0.05), "CR_Gris")
    b.box((4.7, 0.2, 0.7), (0, 0, 3.55), "CR_Azul")
    for rot, y in ((0, -0.115), (180, 0.115)):
        b.text("EMBARQUE", 0.3, (0, y, 3.63), "CR_Blanco", depth=0.03, rot=(('X', 90), ('Z', rot)))
        b.text("REINA DEL IMSERSO", 0.13, (0, y, 3.36), "CR_Amarillo", depth=0.03, rot=(('X', 90), ('Z', rot)))
    b.cyl(0.04, 0.04, 1.1, (1.85, -0.35, 0.55), "CR_Gris", seg=6)
    b.box((0.18, 0.1, 0.25), (1.85, -0.35, 1.2), "CR_Negro")
    b.ball(0.03, (1.85, -0.41, 1.27), "CR_Rojo", seg=6)
    return b.build()

def hoja_embarque():   # one gate leaf, 1.95 m along X, 1.2 m; origin at its hinge (x = 0), the leaf runs to +X
    b = CB("CR_PuertaEmbarque_Hoja")
    b.cyl(0.04, 0.04, 1.25, (0.0, 0, 0.625), "CR_Acero", seg=6)
    for z in (0.15, 1.15): b.box((1.95, 0.04, 0.05), (0.975, 0, z), "CR_Acero")
    for k in range(12): b.box((0.025, 0.025, 0.95), (0.12 + k * 0.155, 0, 0.65), "CR_Acero")
    b.box((0.04, 0.05, 1.05), (1.93, 0, 0.65), "CR_Acero")
    b.box((0.5, 0.02, 0.18), (0.98, -0.03, 0.85), "CR_Rojo")
    b.text("PROHIBIDO EL PASO", 0.045, (0.98, -0.05, 0.85), "CR_Blanco", depth=0.02)
    return b.build()

def letrero_terminal():   # big sign for the terminal building, 12 m wide, hangs on the wall plane y = 0
    b = CB("CR_Letrero_Terminal", wall=True)
    b.box((12.0, 0.15, 1.9), (0, -0.075, 0.95), "CR_Azul")
    b.text("ESTACIÓN MARÍTIMA", 0.9, (0.6, -0.2, 1.15), "CR_Blanco", depth=0.1)
    b.text("CRUCEROS DEL IMSERSO", 0.35, (0.6, -0.18, 0.4), "CR_Amarillo", depth=0.06)
    n0 = b.mark()
    b.ring(0.32, 0.06, (0, 0, 0.62), "CR_Dorado", seg=12, sides=4, rot=[('X', 90)])
    b.box((0.1, 0.08, 1.1), (0, 0, 0.6), "CR_Dorado")
    b.box((0.6, 0.08, 0.1), (0, 0, 0.95), "CR_Dorado")
    b.wpoly([(-0.55, 0.3), (0.55, 0.3), (0.0, -0.05)], -0.04, 0.04, "CR_Dorado")
    b.move(n0, at=(-5.2, -0.2, 0.45))
    return b.build()

def puerto_silueta():
    """The port and the city behind it as one backdrop (about 180 x 125 m, 75 m high), front -Y = the sea, origin at the
    quay edge centre at sea level. It is what is left on the horizon when the ship sails (Rounds/Zarpar moves it away)."""
    b = CB("CR_Puerto_Silueta")
    rnd = random.Random(11)
    Gz = 1.4
    b.box((180.0, 10.0, 2.2), (0, 5.0, 0.3), "CR_Hormigon")
    def lit(x0, x1, yf, z0, z1, p=0.25):   # lit windows on a front face at y = yf
        nx, nz = int((x1 - x0) / 3), int((z1 - z0) / 3)
        for i in range(nx):
            for j in range(nz):
                if rnd.random() < p: b.box((1.4, 0.3, 1.2), (x0 + (i + 0.5) * (x1 - x0) / nx, yf - 0.15, z0 + (j + 0.5) * (z1 - z0) / nz), "CR_Emit_Ciudad")
    b.box((30, 12, 9), (-25, 12, Gz + 4.5), "CR_Blanco")
    b.box((28, 0.3, 2.5), (-25, 5.9, Gz + 5.0), "CR_Cristal")
    b.text("ESTACIÓN MARÍTIMA", 1.4, (-25, 5.7, Gz + 7.8), "CR_Azul", depth=0.2)
    for x in (12, 36, 60):
        b.box((22, 12, 7), (x, 14, Gz + 3.5), rnd.choice(("CR_Crema", "CR_Silueta")))
        b.wpoly([(-11, 0), (11, 0), (0, 3)], 8.0, 20.0, "CR_Rojo", at=(x, 0, Gz + 7))
        b.box((6, 0.3, 4.5), (x, 7.9, Gz + 2.27), "CR_Gris")
    for x in (-60.0, 0.0, 25.0):
        for sx in (-1, 1):
            for sy in (-1, 1): b.box((0.8, 0.8, 10), (x + sx * 3, 3.5 + sy * 3, Gz + 5), "CR_Rojo")
        b.box((5, 4, 4), (x, 3.5, Gz + 12), "CR_Blanco")
        b.box((1.0, 24.0, 1.0), (x, -6.0, Gz + 22.0), "CR_Rojo", rot=[('X', -38)])
        b.tube([(x, -15.0, Gz + 29.0), (x, -15.0, Gz + 14.0)], 0.12, "CR_Negro", sides=4)
    for k in range(14):
        x, z = 33 + (k % 7) * 6.2, (k // 7) * 2.6
        b.box((6.0, 2.4, 2.6), (x, 4.0, Gz + 1.3 + z), rnd.choice(("CR_Rojo", "CR_Azul", "CR_Amarillo", "CR_Verde", "CR_Naranja")), rot=[('Z', rnd.uniform(-2, 2))])
    b.box((34, 6, 3), (78, -8, 0.7), "CR_Hormigon", rot=[('Z', 18)])
    for i, m in enumerate(("CR_Blanco", "CR_Rojo", "CR_Blanco", "CR_Rojo")): b.cyl(1.6 - 0.1 * i, 1.5 - 0.1 * i, 3.5, (92, -13, 2.2 + 1.75 + 3.5 * i), m, seg=10)
    b.cyl(1.2, 1.2, 1.6, (92, -13, 2.2 + 14 + 0.8), "CR_Emit_FaroPuerto", seg=10)
    b.cyl(1.4, 0.2, 1.4, (92, -13, 2.2 + 16.3), "CR_Rojo", seg=10)
    x = -88.0
    while x < 85:
        w, h = rnd.uniform(8, 14), rnd.uniform(12, 36)
        y0 = rnd.uniform(24, 34)
        m = rnd.choice(("CR_Crema", "CR_Blanco", "CR_Silueta", "CR_Crema"))
        b.box((w, 10, h), (x + w / 2, y0 + 5, Gz + h / 2), m)
        b.box((w + 0.6, 10.6, 0.6), (x + w / 2, y0 + 5, Gz + h + 0.3), "CR_Silueta")
        lit(x + 1, x + w - 1, y0, Gz + 3, Gz + h - 1)
        x += w + rnd.uniform(1.5, 4.0)
    b.box((5, 5, 30), (-40, 48, Gz + 15), "CR_Crema")
    b.pyramid(5.6, 5.6, 4, (-40, 48, Gz + 30), "CR_Rojo")
    b.ball(7, (-30, 50, Gz + 18), "CR_Celeste", scale=(1, 1, 0.8), seg=10)
    b.cyl(60, 15, 55, (20, 100, Gz + 27.5), "CR_Monte", seg=7, scale=(1.4, 0.5, 1.0))
    for k in range(7): b.box((5, 5, 5), (6 + k * 4.5, 100, Gz + 57 + (k % 2) * 2), "CR_Crema")
    for k in range(10):
        x = -80 + k * 9.0
        b.cyl(0.3, 0.25, 8, (x, 1.5, Gz + 4), "M_Bark", seg=5)
        b.ball(2.0, (x, 1.5, Gz + 8.5), "CR_Hoja", scale=(1, 1, 0.5), seg=6)
    return b.build()

def skyline():
    """The Benidorm coast seen from the sea at night: beach, a promenade of lights, ~30 towers with lit windows (two very
    tall ones joined at the top, a tall slab), the Hotel Jubiles tower with its pink neon, the Sierra Helada and the
    wedge-shaped island in front. About 440 x 260 m, 200 m high; origin on the shore line centre at sea level, front -Y."""
    b = CB("CR_Skyline_Benidorm")
    rnd = random.Random(5)
    b.box((440, 14, 3), (0, 7, 0.7), "CR_Paja")
    for x in range(-210, 211, 7): b.ball(0.7, (x, 1.5, 3.5), "CR_Emit_Skyline", seg=6)
    def tower(x, w, d, h, y0=18.0, m="CR_Silueta", p=0.2):
        b.box((w, d, h), (x, y0 + d / 2, 2 + h / 2), m)
        nx, nz = max(int(w / 4), 1), int(h / 3.2)
        for j in range(1, nz - 1):
            i = 0
            while i < nx:
                if rnd.random() < p:
                    run = 1
                    while i + run < nx and rnd.random() < 0.5: run += 1
                    cw = w / nx
                    b.box((run * cw - 1.0, 0.6, 1.4), (x - w / 2 + (i + run / 2) * cw, y0 - 0.3, 2 + (j + 0.5) * h / nz), "CR_Emit_Skyline")
                    i += run
                i += 1
    x = -205.0
    while x < 205:
        w = rnd.uniform(12, 24)
        cx = x + w / 2
        if abs(cx - 60) > 30 and abs(cx + 70) > 16 and abs(cx + 10) > 22:
            far = rnd.random() < 0.35
            tower(cx, w, rnd.uniform(10, 18), rnd.uniform(30, 95) * (0.8 if far else 1.0), y0=40.0 if far else 18.0,
                  m="CR_SiluetaLejos" if far else "CR_Silueta")
        x += w + rnd.uniform(3, 9)
    for s in (-1, 1):
        n0 = b.mark()
        tower(0, 20, 20, 160)
        b.move(n0, rot=[('Y', -s * 2.0)], at=(60 + s * 14, 0, 0))
    b.box((50, 20, 10), (60, 28, 2 + 158), "CR_Silueta")
    b.pyramid(30, 18, 14, (60, 28, 2 + 163), "CR_Silueta")
    tower(-70, 32, 16, 185)
    tower(-10, 30, 18, 110)
    b.box((70, 1.0, 1.0), (-10, 17.0, 2 + 110 + 0.5), "CR_Gris")
    for x in (-40, -20, 0, 20): b.box((1.0, 1.0, 16), (x, 17.5, 2 + 110 + 8), "CR_Gris")
    b.text("HOTEL JUBILES", 9.0, (-10, 15.8, 2 + 110 + 8.5), "CR_Emit_Neon", depth=0.8)
    for z in (2 + 110 + 2.0, 2 + 110 + 15.0): b.box((74, 0.6, 0.6), (-10, 15.6, z), "CR_Emit_Neon")
    for x in (-47, 27): b.box((0.6, 0.6, 13.6), (x, 15.6, 2 + 110 + 8.5), "CR_Emit_Neon")
    b.cyl(80, 8, 120, (-190, 70, 60), "CR_SiluetaLejos", seg=6, scale=(1.3, 0.5, 1.0))
    b.cyl(70, 5, 40, (150, 90, 20), "CR_SiluetaLejos", seg=6, scale=(1.4, 0.4, 1.0))
    b.wpoly([(-40, -2), (40, -2), (28, 55), (18, 62)], -170, -140, "CR_Silueta", at=(60, 0, 0))
    return b.build()

# ================================================================== boarding
def lancha():
    """The Imserso launch of the boardings: 7.5 m, bow towards -Y, origin at the waterline; orange inflatable collar,
    T-top with the searchlight (CR_Emit_Foco, pointing -Y; add CR_Foco_Cono in front of it), outboard motor."""
    b = CB("CR_Lancha")
    half = [(0.0, -0.45), (0.4, -0.35), (0.85, -0.05), (1.0, 0.3), (1.0, 0.75), (0.0, 0.75)]
    secs = []
    for i in range(11):
        t = i / 10
        y = -3.75 + 7.5 * t
        w = 0.15 + 1.05 * math.sin(math.pi * min(t * 1.7, 1.0) / 2) - 0.08 * max(t - 0.8, 0) * 5
        lift = 0.35 * max((0.35 - t) / 0.35, 0) ** 2
        loop = [(w * a, z) for a, z in half[:-1]] + [(0.0, 0.75)] + [(-w * a, z) for a, z in reversed(half[1:-1])]
        secs.append([Vector((x, y, z + lift)) for x, z in loop])
    b.loft(secs, lambda c, n: "CR_LaunchPaint" if c is None else ("M_Wood" if n.z > 0.9 and c.z > 0.5 else "CR_Azul" if c.z < 0.2 else "CR_LaunchPaint"))
    def ww(t): return 0.15 + 1.05 * math.sin(math.pi * min(t * 1.7, 1.0) / 2) - 0.08 * max(t - 0.8, 0) * 5
    col = [(ww(t) + 0.12, -3.75 + 7.5 * t, 0.88 + 0.35 * max((0.35 - t) / 0.35, 0) ** 2) for t in [0.98, 0.8, 0.6, 0.4, 0.25, 0.12, 0.04]]
    pts = col + [(0.0, -3.82, 1.22)] + [(-x, y, z) for x, y, z in reversed(col)]
    b.tube(pts, 0.22, "CR_Naranja", sides=8)
    b.box((0.7, 0.6, 0.6), (0, 0.2, 1.05), "CR_Blanco")
    b.box((0.9, 0.04, 0.45), (0, -0.2, 1.5), "CR_VidrioClaro", rot=[('X', -25)])
    b.ring(0.15, 0.02, (0, 0.0, 1.45), "CR_Negro", seg=10, sides=4, rot=[('X', 60)])
    for sx in (-1, 1):
        for y in (-0.3, 1.1): b.cyl(0.035, 0.035, 1.6, (sx * 0.7, y, 1.55), "CR_Cromo", seg=6)
    b.box((1.7, 1.7, 0.08), (0, 0.4, 2.38), "CR_AwningPaint")
    b.box((1.66, 0.03, 0.25), (0, -0.45, 2.25), "CR_Blanco")
    b.text("VIAJES IMSERSO", 0.12, (0, -0.47, 2.25), "CR_Azul", depth=0.02)
    b.cyl(0.18, 0.18, 0.35, (0, -0.3, 2.62), "CR_Negro", seg=10, rot=[('X', 90)])
    b.cyl(0.15, 0.15, 0.05, (0, -0.49, 2.62), "CR_Emit_Foco", seg=10, rot=[('X', 90)])
    b.box((0.08, 0.1, 0.2), (0, -0.3, 2.47), "CR_Negro")
    for y in (-1.6, 1.6): b.box((1.6, 0.4, 0.12), (0, y, 1.0), "M_Wood")
    b.box((0.5, 0.6, 0.7), (0, 3.95, 1.0), "CR_Negro")
    b.box((0.16, 0.2, 1.1), (0, 4.05, 0.15), "CR_Negro")
    b.cyl(0.18, 0.18, 0.05, (0, 4.1, -0.35), "CR_Gris", seg=3, rot=[('X', 90)])
    for s in (-1, 1): b.text("IMSERSO 3", 0.24, (s * (ww(0.55) + 0.016), 0.3, 0.45), "CR_Azul", depth=0.03, rot=(('X', 90), ('Z', 90 * s)))
    return b.build()

def foco_cono():   # fake beam of a searchlight: open cone, apex at the origin, 1 m along -Y, 0.35 m radius (scale it)
    b = CB("CR_Foco_Cono")
    bm = b.bm
    apex = bm.verts.new((0, 0, 0))
    ring = [bm.verts.new((0.35 * math.cos(a), -1.0, 0.35 * math.sin(a))) for a in [2 * math.pi * k / 16 for k in range(16)]]
    for k in range(16): bm.faces.new([apex, ring[(k + 1) % 16], ring[k]])
    b._finish([apex] + ring, "CR_Emit_FocoCono")
    b.recalc = False   # open cone: the winding above already faces out
    return b.build()

def garfio(name, rope):   # grappling hook caught on the handrail from the deck side, knotted rope down into the sea
    b = CB(name)
    b.tube([(0, 0.3, 1.0), (0, -0.12, 1.22)], 0.035, "CR_Gris", sides=6)
    for dx in (-0.16, 0.0, 0.16):
        b.tube([(0, 0.3, 1.0), (dx, 0.42, 0.94 if dx else 0.86), (dx * 1.2, 0.33, 1.12), (dx * 1.1, 0.14, 1.18)], 0.025, "CR_Gris", sides=5)
    b.ring(0.05, 0.015, (0, -0.15, 1.24), "CR_Gris", seg=8, sides=4, rot=[('X', 90)])
    pts = [(0, -0.15, 1.2), (0, -0.3, 1.05), (0, -0.32, 0.6), (0.03, -0.32, -rope * 0.35), (-0.02, -0.33, -rope * 0.7), (0, -0.32, -rope)]
    b.tube(pts, 0.025, "CR_Cuerda", sides=6)
    z = 0.3
    while z > -rope + 0.3:
        b.ball(0.045, (0.0, -0.32, z), "CR_Cuerda", seg=6)
        z -= 0.6
    return b.build()

# ================================================================== mission items, the crank
def tarjeta():   # the boarding pass on an acrylic stand (twice real size so it reads as a pickup)
    b = CB("CR_TarjetaEmbarque", tol=0.003)
    b.box((0.3, 0.12, 0.04), (0, 0, 0.02), "CR_VidrioClaro")
    n0 = b.mark()
    b.box((0.4, 0.012, 0.14), (0, 0, 0.07), "CR_Blanco")
    b.box((0.4, 0.012, 0.06), (0, 0, 0.17), "CR_Azul")
    b.text("REINA DEL IMSERSO", 0.03, (0, -0.008, 0.17), "CR_Blanco", depth=0.006)
    b.text("TARJETA DE EMBARQUE", 0.024, (-0.04, -0.008, 0.115), "CR_Azul", depth=0.006)
    b.text("CAMAROTE 101", 0.02, (-0.06, -0.008, 0.08), "CR_Negro", depth=0.006)
    for k in range(12): b.box((0.004 + 0.003 * (k % 3), 0.006, 0.05), (0.09 + k * 0.009, -0.007, 0.05), "CR_Negro")
    b.move(n0, rot=[('X', 10)], at=(0, 0.0, 0.035))
    return b.build()

def cartas():   # the captain's sea charts: one unrolled with brass dividers, three rolls tied with string
    b = CB("CR_CartasNauticas", tol=0.003)
    b.box((0.6, 0.45, 0.008), (0, 0, 0.004), "CR_Crema")
    b.box((0.3, 0.25, 0.004), (0.12, -0.05, 0.01), "CR_Celeste")
    b.box((0.18, 0.1, 0.004), (-0.15, 0.12, 0.01), "CR_Celeste")
    b.tube([(-0.25, -0.15, 0.012), (-0.1, -0.05, 0.012), (0.0, 0.1, 0.012), (0.2, 0.18, 0.012)], 0.004, "CR_Rojo", sides=4)
    for d in (-1, 1): b.cyl(0.005, 0.002, 0.2, (0.05 + d * 0.03, 0.0, 0.03), "CR_Laton", seg=4, rot=[('Y', 90 + d * 12), ('X', 8)])
    for k, (x, y, rz) in enumerate(((-0.05, 0.3, 5), (0.1, 0.36, -8), (0.02, 0.33, 2))):
        b.cyl(0.04, 0.04, 0.65, (x, y, 0.04 + (0.07 if k == 2 else 0)), "CR_Crema", seg=8, rot=[('Y', 90), ('Z', rz)])
        for e in (-0.15, 0.15): b.cyl(0.046, 0.046, 0.012, (x + e, y + e * math.tan(math.radians(rz)), 0.04 + (0.07 if k == 2 else 0)), "CR_Rojo", seg=8, rot=[('Y', 90), ('Z', rz)])
    return b.build()

def bidon():   # jerrycan of petrol for the lifeboat
    b = CB("CR_Bidon", tol=0.003)
    b.box((0.35, 0.17, 0.42), (0, 0, 0.21), "CR_Rojo")
    for s in (-1, 1):
        for a in (35, -35): b.box((0.4, 0.008, 0.03), (0, s * 0.087, 0.2), "CR_Rojo", rot=[('Y', a)])
    for x in (-0.08, 0.0, 0.08): b.cyl(0.012, 0.012, 0.12, (x, 0, 0.45), "CR_Rojo", seg=6, rot=[('X', 90)])
    for x in (-0.08, 0.08): b.box((0.015, 0.1, 0.03), (x, 0, 0.432), "CR_Rojo")
    b.cyl(0.03, 0.03, 0.06, (0.13, 0, 0.44), "CR_Amarillo", seg=8, rot=[('Y', 30)])
    b.text("GASOLINA", 0.04, (0, -0.091, 0.33), "CR_Blanco", depth=0.004)
    return b.build()

def llave_bote():   # the lifeboat key: a big brass key on a cork float
    b = CB("CR_LlaveBote", tol=0.003)
    b.ring(0.045, 0.012, (-0.1, 0, 0.012), "CR_Laton", seg=12, sides=4)
    b.cyl(0.01, 0.01, 0.18, (0.0, 0, 0.012), "CR_Laton", seg=6, rot=[('Y', 90)])
    b.box((0.025, 0.004, 0.04), (0.07, 0.022, 0.012), "CR_Laton", rot=[('X', 90)])
    b.box((0.02, 0.004, 0.025), (0.035, 0.016, 0.012), "CR_Laton", rot=[('X', 90)])
    b.ring(0.025, 0.004, (-0.16, 0, 0.006), "CR_Gris", seg=8, sides=4)
    b.cyl(0.028, 0.028, 0.12, (-0.25, 0.01, 0.028), "CR_Naranja", seg=8, rot=[('Y', 90), ('Z', 15)])
    b.text("BOTE 1", 0.022, (-0.25, 0.01, 0.06), "CR_Negro", depth=0.004, rot=(('Z', 15),))
    return b.build()

def manivela():   # the lifeboat crank on the bridge wing: winch drum with the wire and a big red handle (+X side)
    b = CB("CR_Manivela")
    b.box((0.6, 0.5, 0.15), (0, 0, 0.075), "CR_Gris")
    for x in (-0.22, 0.22):
        for y in (-0.17, 0.17): b.cyl(0.03, 0.03, 0.04, (x, y, 0.17), "CR_Negro", seg=6)
    b.box((0.35, 0.35, 0.8), (0, 0, 0.55), "CR_Blanco")
    b.cyl(0.18, 0.18, 0.45, (0, 0, 1.08), "CR_Gris", seg=12, rot=[('Y', 90)])
    for x in (-0.15, -0.05, 0.05, 0.15): b.ring(0.185, 0.02, (x, 0, 1.08), "CR_Negro", seg=12, sides=4, rot=[('Y', 90)])
    b.cyl(0.24, 0.24, 0.04, (-0.25, 0, 1.08), "CR_Gris", seg=16, rot=[('Y', 90)])
    b.cyl(0.03, 0.03, 0.18, (0.31, 0, 1.08), "CR_Gris", seg=6, rot=[('Y', 90)])
    b.box((0.05, 0.05, 0.4), (0.4, 0, 1.24), "CR_Gris", rot=[('X', 30)])
    b.cyl(0.04, 0.04, 0.22, (0.5, -0.1, 1.41), "CR_Rojo", seg=8, rot=[('Y', 90)])
    b.box((0.3, 0.02, 0.12), (0, -0.185, 0.75), "CR_Rojo")
    b.text("GIRAR", 0.06, (0, -0.2, 0.75), "CR_Blanco", depth=0.02)
    return b.build()

# ================================================================== old men's accessories (origins as said)
def flotador():   # swim ring with a duck head, worn at the waist: origin = ring centre, inner radius 0.23
    b = CB("CR_Flotador", tol=0.003)
    b.ring(0.36, 0.13, (0, 0, 0), "CR_FloatPaint", seg=16, sides=8, mfn=lambda i: "CR_Blanco" if (i // 2) % 2 else "CR_FloatPaint")
    b.cyl(0.06, 0.05, 0.22, (0, -0.47, 0.17), "CR_FloatPaint", seg=8, rot=[('X', -20)])
    b.ball(0.1, (0, -0.5, 0.33), "CR_FloatPaint", seg=10)
    b.cyl(0.045, 0.02, 0.1, (0, -0.62, 0.31), "CR_Naranja", seg=8, rot=[('X', 90)])
    for s in (-1, 1): b.ball(0.018, (s * 0.05, -0.585, 0.37), "CR_Negro", seg=6)
    return b.build()

def gorro_bano():   # flowery rubber swimming cap: origin = centre of the head (cap radius 0.135)
    b = CB("CR_GorroBano", tol=0.003)
    lats = [-0.22, 0.05, 0.4, 0.75, 1.1, 1.45]   # dome from just under the ears to the crown (radians)
    b.loft([[Vector((0.135 * math.cos(la) * math.cos(th), 0.135 * 1.12 * math.cos(la) * math.sin(th), 0.135 * 0.95 * math.sin(la)))
             for th in [2 * math.pi * k / 14 for k in range(14)]] for la in lats], lambda c, n: "CR_CapPaint")
    rnd = random.Random(9)
    for k in range(11):
        th, ph = rnd.uniform(0, 2 * math.pi), rnd.uniform(math.radians(15), math.radians(75))
        n = Vector((math.cos(th) * math.cos(ph), math.sin(th) * math.cos(ph) * 1.12, math.sin(ph) * 0.95)).normalized()
        p = Vector((0.135 * math.cos(th) * math.cos(ph), 0.135 * 1.12 * math.sin(th) * math.cos(ph), 0.135 * 0.95 * math.sin(ph)))
        rx, rz = math.degrees(math.acos(max(-1, min(1, n.z)))), math.degrees(math.atan2(n.y, n.x)) - 90
        n1 = b.mark()
        b.cyl(0.03, 0.03, 0.012, (0, 0, 0), ("CR_Rosa", "CR_Amarillo", "CR_Celeste", "CR_Blanco")[k % 4], seg=5)
        b.ball(0.012, (0, 0, 0.009), "CR_Amarillo" if k % 4 else "CR_Naranja", seg=6)
        b.move(n1, rot=[('X', -rx), ('Z', rz)], at=tuple(p + n * 0.004))
    return b.build()

def churro():   # pool noodle held in the hand: origin = the grip at its middle, 1.4 m along X, bent a little
    b = CB("CR_Churro", tol=0.003)
    R = 2.5
    pts = [(R * math.sin(a), 0.0, R * (1 - math.cos(a))) for a in [(-0.28 + 0.56 * k / 10) for k in range(11)]]
    b.tube(pts, 0.035, "CR_NoodlePaint", sides=6)
    for p0, p1 in ((pts[0], pts[1]), (pts[-1], pts[-2])):
        d = (Vector(p0) - Vector(p1)).normalized()
        n1 = b.mark()
        b.cyl(0.012, 0.012, 0.01, (0, 0, 0), "CR_Negro", seg=6)
        b.move(n1, rot=[('Y', 90 if d.x > 0 else -90)], at=tuple(Vector(p0) + d * 0.002))
    return b.build()

def silbato():   # the aquagym instructor's whistle on a lanyard: origin = centre of the neck, the whistle hangs in front
    b = CB("CR_Silbato", tol=0.003)
    b.ring(0.13, 0.006, (0, 0, 0), "CR_Azul", seg=16, sides=4)
    for s in (-1, 1): b.tube([(s * 0.06, -0.115, 0.0), (s * 0.02, -0.15, -0.12), (0.0, -0.16, -0.2)], 0.006, "CR_Azul", sides=4)
    b.cyl(0.016, 0.016, 0.05, (0.01, -0.165, -0.225), "CR_Rojo", seg=8, rot=[('Y', 90)])
    b.box((0.04, 0.02, 0.014), (-0.03, -0.165, -0.225), "CR_Rojo")
    b.ring(0.008, 0.003, (0.0, -0.16, -0.205), "CR_Cromo", seg=6, sides=4, rot=[('X', 90)])
    return b.build()

def gorra_capitan():   # captain's cap for the boss: origin = centre of the head (scale with the giant)
    b = CB("CR_GorraCapitan", tol=0.003)
    b.cyl(0.142, 0.142, 0.05, (0, 0, 0.065), "CR_Negro", seg=16)
    b.cyl(0.14, 0.165, 0.06, (0, 0, 0.12), "CR_Blanco", seg=16)
    b.cyl(0.165, 0.165, 0.03, (0, 0.01, 0.165), "CR_Blanco", seg=16)
    b.cyl(0.14, 0.14, 0.012, (0, -0.13, 0.05), "CR_Negro", seg=12, scale=(1, 0.6, 1), rot=[('X', -14)])
    for z in (0.075, 0.09): b.cyl(0.003, 0.003, 0.2, (0, -0.146, z), "CR_Dorado", seg=4, rot=[('Y', 90)])
    b.disc(0.032, 0.006, (0, -0.15, 0.125), "CR_Dorado", seg=10)
    b.box((0.008, 0.006, 0.04), (0, -0.156, 0.125), "CR_Azul")
    b.box((0.03, 0.006, 0.008), (0, -0.156, 0.138), "CR_Azul")
    for s in (-1, 1): b.box((0.03, 0.004, 0.012), (s * 0.045, -0.15, 0.12), "CR_Dorado", rot=[('Y', s * 30)])
    return b.build()

# ================================================================== build everything
PIECES = [
    (casco_a,), (casco_b,), (casco_c,), (proa,), (popa,),
    (borda, "CR_Borda"), (borda, "CR_Borda_45", 2.83), (borda, "CR_Borda_Salvavidas", 2.0, True),
    (bote_salvavidas,), (bote_pescantes,), (pescante,), (chimenea,), (radar,), (puente_ventana,), (timon,), (consola,),
    (puerta_estanca,), (fachada_a,), (fachada_b,), (fachada_c,), (fachada_d,), (taquilla,), (guirnalda,), (farol,),
    (torre_techo,), (tobogan_piscina,), (tobogan_casco,), (piscina,), (jacuzzi,), (tumbona,), (sombrilla,), (bar_cubierta,),
    (palmera,), (cabana,), (tejo,), (tejo_pista,),
    (lampara,), (barandilla_lujo,), (piano,), (tragaperras,), (tragaperras_fila,), (ruleta,), (escenario,), (mesa_redonda,),
    (bufe,), (puerta_camarote, "CR_PuertaCamarote_A", 0.3, False), (puerta_camarote, "CR_PuertaCamarote_B", -0.3, True),
    (pasillo,), (litera,), (motor,), (cuadro_electrico,), (moto_agua,), (escalerilla,),
    (mostrador,), (cinta_equipaje,), (estante_tienda,), (contenedor,), (contenedores,), (maletas,), (carretilla,), (bolardo,),
    (grua,), (pasarela,), (puerta_embarque,), (hoja_embarque,), (letrero_terminal,), (puerto_silueta,), (skyline,),
    (lancha,), (foco_cono,), (garfio, "CR_Garfio", 5.2), (garfio, "CR_Garfio_Alto", 9.2),
    (tarjeta,), (cartas,), (bidon,), (llave_bote,), (manivela,),
    (flotador,), (gorro_bano,), (churro,), (silbato,), (gorra_capitan,),
]

res, row_x = [], 0.0

def make_piece(fn, *args):
    global row_x
    ob = fn(*args)
    res.append(export(ob))
    ob.location.x = row_x + ob.dimensions.x / 2
    row_x += ob.dimensions.x + 3.0

# ================================================================== contact sheet (workbench, labels under each piece)
def contact_sheets(names, prefix, per=12, cols=4):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    sh = scene.display.shading
    sh.light, sh.color_type, sh.show_shadows = 'STUDIO', 'MATERIAL', False   # flat label text breaks shadow volumes
    sh.show_cavity = True
    w = bpy.data.worlds.new("Sheet"); scene.world = w; w.color = (0.75, 0.79, 0.84)
    scene.render.resolution_x, scene.render.resolution_y = 2400, 1760
    scene.render.film_transparent = False
    cd = bpy.data.cameras.new("SheetCam"); cd.type = 'ORTHO'
    cam = bpy.data.objects.new("SheetCam", cd); scene.collection.objects.link(cam); scene.camera = cam
    lab = mat("CR_Label")
    info = {r["name"]: r["dims"] for r in res}
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = True
    files = []
    for s0 in range(0, len(names), per):
        group, temp = names[s0:s0 + per], []
        rows = (len(group) + cols - 1) // cols
        for i, nm in enumerate(group):
            o = bpy.data.objects[nm]
            d = o.copy(); scene.collection.objects.link(d); d.hide_render = False; temp.append(d)
            vs = [v.co for v in o.data.vertices]
            mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
            mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
            sc = 1.35 / max((mx - mn)[:])
            rz = math.radians(-28)
            c = (mn + mx) / 2 * sc
            c.rotate(Matrix.Rotation(rz, 3, 'Z'))
            col, row = i % cols, i // cols
            d.scale, d.rotation_euler = (sc, sc, sc), (0, 0, rz)
            d.location = Vector((col * 2.0, 0, -row * 2.15)) - c
            cu = bpy.data.curves.new("lab", 'FONT')
            dims = info.get(nm, [0, 0, 0])
            cu.body = "%s  %.2f x %.2f x %.2f m" % (nm, dims[0], dims[1], dims[2])
            cu.size, cu.align_x, cu.align_y = 0.085, 'CENTER', 'CENTER'
            cu.materials.append(lab)
            t = bpy.data.objects.new("lab", cu); scene.collection.objects.link(t); temp.append(t)
            t.location, t.rotation_euler = (col * 2.0, -1.5, -row * 2.15 - 0.92), (math.radians(80), 0, 0)
        cam.rotation_euler = (math.radians(80), 0, 0)
        fwd = Vector((0, math.sin(math.radians(80)), -math.cos(math.radians(80))))
        cen = Vector(((cols - 1), 0, -(rows - 1) * 1.075 - 0.2))
        cam.location = cen - fwd * 40
        cd.ortho_scale = max(cols * 2.0 + 0.3, (rows * 2.15 + 0.3) * 2400 / 1760)
        scene.render.resolution_y = 1760 if rows >= 3 else int(1760 * rows / 3 + 200)
        cd.ortho_scale = max(cols * 2.0 + 0.3, (rows * 2.15 + 0.3) * 2400 / scene.render.resolution_y)
        path = os.path.join(prefix + "%02d.png" % (s0 // per + 1))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        files.append(path)
        for t in temp: bpy.data.objects.remove(t)
    return files

clear()
for p in PIECES: make_piece(*p)
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("PROPS_RESULT " + json.dumps(res))
print("EMIT " + json.dumps(EMIT))
bad = {k: v for k, v in CHECKS.items() if v}
print("COPLANAR %d pieces with pairs" % len(bad))
for k, v in bad.items(): print("  %s: %d  %s" % (k, len(v), "; ".join(v[:6])))
if "sheet" in ARGS:
    opts = dict(a.split("=", 1) for a in ARGS if "=" in a)
    names = opts["only"].split(",") if "only" in opts else [r["name"] for r in res]
    out = opts.get("out", RENDERS)
    per = int(opts.get("per", 12))
    files = contact_sheets(names, os.path.join(out, "CruceroKit_" if "out" not in opts else "CruceroDetail_"), per=per, cols=4 if per > 4 else 2)
    print("SHEETS " + json.dumps(files))
