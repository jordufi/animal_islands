# Kit of pieces for the map "Fiestas de Villajubilada" (an inland village during its fiestas): one FBX per piece in
# Assets/Maps/Villajubilada/Models (+ a .blend copy). Run in background:
#   blender.exe -b --factory-startup --python make_villajubilada_props.py
# Same conventions and the same Builder as make_venecia_props.py (its helper section is reused as is): origin = centre
# of the footprint on the ground, front = Blender -Y (= Unity +Z), up = +Z. Facade strips (VJ_Fachada_*) are 2 m wide
# and 5.6 m tall (two floors: the village is lower than Venice) and hang on the wall plane y = 0.
# Material names: M_* = the island's textured materials, VN_AwningPaint = recoloured per instance in Unity,
# VJ_Bulb_* = the fiesta lights (dark until the zone's fuse box is switched on: Zones/FiestaLights).
import os, math, json

_here = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(_here, "make_venecia_props.py"), encoding="utf-8").read().split("# ================================================================== facades")[0])

OUT = r"C:\Users\Usuario\animal_island1\Assets\Maps\Villajubilada\Models"
BLEND = r"C:\Users\Usuario\Documents\BeachAnimals\Villajubilada\Villajubilada_Props.blend"

COLORS.update({
    "VJ_Cal": (0.93, 0.92, 0.88), "VJ_Zocalo": (0.55, 0.5, 0.45), "VJ_ZocaloAzul": (0.25, 0.4, 0.6), "VJ_Teja": (0.7, 0.36, 0.22),
    "VJ_Madera": (0.36, 0.22, 0.12), "VJ_MaderaClara": (0.62, 0.45, 0.26), "VJ_Persiana": (0.2, 0.42, 0.3), "VJ_Reja": (0.1, 0.1, 0.11),
    "VJ_Geranio": (0.85, 0.12, 0.15), "VJ_Hoja": (0.18, 0.4, 0.18), "VJ_Barro": (0.72, 0.4, 0.25), "VJ_Piedra": (0.66, 0.62, 0.55),
    "VJ_Paja": (0.86, 0.72, 0.32), "VJ_Toro": (0.09, 0.07, 0.06), "VJ_Cuerno": (0.9, 0.86, 0.72), "VJ_Chapa": (0.6, 0.62, 0.64),
    "VJ_Rojo": (0.78, 0.1, 0.1), "VJ_Verde": (0.12, 0.5, 0.25), "VJ_Azul": (0.15, 0.3, 0.7), "VJ_Amarillo": (0.95, 0.78, 0.15),
    "VJ_Negro": (0.05, 0.05, 0.06), "VJ_Blanco": (0.95, 0.95, 0.93), "VJ_Goma": (0.08, 0.08, 0.08), "VJ_Oro": (0.95, 0.75, 0.2),
    "VJ_Piel": (0.9, 0.7, 0.58), "VJ_Bulb_R": (0.5, 0.1, 0.1), "VJ_Bulb_G": (0.1, 0.4, 0.15), "VJ_Bulb_B": (0.1, 0.2, 0.5),
    "VJ_Bulb_Y": (0.5, 0.42, 0.1), "VJ_Cable": (0.04, 0.04, 0.04), "VJ_Rosa": (0.9, 0.4, 0.6),
})

TOP = 5.6
UP = 2.9   # sill of the upper floor

def base(b, zocalo="VJ_Zocalo"):
    b.box((2.0, 0.06, 0.9), (0, -0.03, 0.45), zocalo)                              # painted / stone plinth band
    for i in range(5): b.cyl(0.11, 0.11, 0.5, (-0.8 + i * 0.4, -0.2, TOP - 0.02), "VJ_Teja", seg=6, rot=[('X', 78)])   # eave of roof tiles
    b.box((2.0, 0.12, 0.1), (0, -0.06, TOP - 0.2), "VJ_Cal")

def win(b, x, z, w, h, shutter="VJ_Madera", reja=False, closed=False):
    b.box((w + 0.2, 0.08, h + 0.2), (x, -0.04, z + h / 2), "VJ_Piedra")
    b.box((w, 0.06, h), (x, -0.07, z + h / 2), "VN_Glass" if not closed else shutter)
    b.box((w + 0.3, 0.2, 0.1), (x, -0.1, z - 0.05), "VJ_Piedra")
    if reja:
        n = max(3, int(w / 0.16))
        for i in range(n): b.box((0.03, 0.03, h + 0.1), (x - w / 2 + (i + 0.5) * w / n, -0.2, z + h / 2), "VJ_Reja")
        for dz in (0.12, h - 0.12): b.box((w + 0.1, 0.035, 0.035), (x, -0.2, z + dz), "VJ_Reja")
    elif shutter and not closed:
        for s in (-1, 1): b.box((w / 2, 0.04, h), (x + s * (w / 2 + w / 4 + 0.1), -0.06, z + h / 2), shutter)

def balcon(b, z, w=1.7):
    b.box((w, 0.55, 0.08), (0, -0.28, z), "VJ_Piedra")
    n = int(w / 0.14)
    for i in range(n + 1): b.box((0.025, 0.025, 0.8), (-w / 2 + i * w / n, -0.53, z + 0.42), "VJ_Reja")
    b.box((w, 0.04, 0.04), (0, -0.53, z + 0.84), "VJ_Reja")
    for s in (-1, 1):
        b.box((0.03, 0.5, 0.04), (s * w / 2, -0.28, z + 0.84), "VJ_Reja")
        b.cyl(0.11, 0.08, 0.2, (s * (w / 2 - 0.2), -0.42, z + 0.94), "VJ_Barro", seg=6)       # geranium pots on the rail
        b.ball(0.15, (s * (w / 2 - 0.2), -0.42, z + 1.12), "VJ_Geranio", seg=6)
        b.ball(0.12, (s * (w / 2 - 0.2), -0.36, z + 1.05), "VJ_Hoja", seg=6)

def fachada_a():   # wooden door under a stone lintel, balcony with geraniums above
    b = Builder("VJ_Fachada_A")
    base(b)
    b.box((1.24, 0.12, 2.3), (0, -0.06, 1.15), "VJ_Piedra")
    b.box((1.0, 0.08, 2.1), (0, -0.11, 1.05), "VJ_Madera")
    b.box((0.03, 0.1, 2.1), (0, -0.12, 1.05), "VJ_Negro")
    for s in (-1, 1): b.ball(0.045, (s * 0.12, -0.17, 1.1), "VJ_Oro", seg=6)
    b.box((1.4, 0.35, 0.12), (0, -0.18, 0.06), "VJ_Piedra")
    balcon(b, UP)
    win(b, 0, UP + 0.1, 0.9, 1.8, shutter="VJ_Madera")
    return b.build()

def fachada_b():   # the bar / shop: wide glass front with an awning, two shuttered windows above
    b = Builder("VJ_Fachada_B")
    base(b, "VJ_ZocaloAzul")
    b.box((1.7, 0.08, 1.5), (0, -0.04, 1.65), "VJ_Madera")
    b.box((1.5, 0.06, 1.3), (0, -0.08, 1.65), "VN_Glass")
    b.box((1.9, 0.9, 0.07), (0, -0.48, 2.55), "VN_AwningPaint", rot=[('X', -20)])
    b.box((1.9, 0.04, 0.22), (0, -0.9, 2.3), "VN_AwningPaint")
    for s in (-1, 1): win(b, s * 0.48, UP + 0.5, 0.5, 1.1, shutter="VJ_Persiana", closed=(s > 0))
    return b.build()

def fachada_c():   # barred window at street level, balcony above
    b = Builder("VJ_Fachada_C")
    base(b)
    win(b, 0, 1.1, 0.9, 1.2, reja=True)
    balcon(b, UP, 1.5)
    win(b, 0, UP + 0.1, 0.8, 1.7, shutter="VJ_Persiana")
    return b.build()

def fachada_d():   # plain whitewashed wall: drainpipe, small window, a rolled-down green blind, a bench
    b = Builder("VJ_Fachada_D")
    base(b, "VJ_ZocaloAzul")
    b.cyl(0.05, 0.05, TOP - 0.5, (0.86, -0.08, (TOP - 0.5) / 2 + 0.1), "VJ_Chapa", seg=6)
    win(b, -0.25, UP + 0.6, 0.6, 0.9, shutter="VJ_Madera")
    b.box((0.7, 0.05, 1.2), (-0.25, -0.12, 1.75), "VJ_Persiana")                   # esparto blind over a low window
    b.box((0.8, 0.08, 0.08), (-0.25, -0.12, 2.38), "VJ_Madera")
    for x, z, w, h in ((0.45, 1.6, 0.5, 0.7), (-0.6, 4.6, 0.6, 0.4)): b.box((w, 0.03, h), (x, -0.015, z), "VJ_Piedra")   # plaster fallen off
    return b.build()

def frente_iglesia():   # 12 m wide church front: door, rose window, espadana with two bells (scaled in Unity by width / 12)
    b = Builder("VJ_FrenteTemplo")
    b.box((12.0, 0.3, 0.6), (0, -0.15, 0.3), "VJ_Piedra")
    for s in (-1, 1): b.box((1.0, 0.7, 9.0), (s * 5.5, -0.35, 4.5), "VJ_Piedra")   # buttresses
    b.box((3.4, 0.5, 4.4), (0, -0.25, 2.2), "VJ_Piedra")
    b.half_disc(1.7, 0.5, (0, -0.25, 4.4), "VJ_Piedra", seg=10)
    b.box((2.4, 0.2, 3.6), (0, -0.55, 1.8), "VJ_Madera")
    b.half_disc(1.2, 0.2, (0, -0.55, 3.6), "VJ_Madera", seg=10)
    b.box((0.06, 0.24, 3.6), (0, -0.57, 1.8), "VJ_Negro")
    b.disc(1.1, 0.3, (0, -0.15, 7.0), "VJ_Piedra", seg=12)
    b.disc(0.85, 0.34, (0, -0.16, 7.0), "VN_Glass", seg=12)
    for a in range(4): b.box((0.08, 0.38, 1.7), (0, -0.17, 7.0), "VJ_Piedra", rot=[('Y', a * 45)])
    b.box((12.0, 0.5, 0.4), (0, -0.25, 9.2), "VJ_Piedra")
    b.box((5.0, 0.8, 3.2), (0, 0.2, 11.0), "VJ_Cal")                               # espadana
    for s in (-1, 1):
        b.box((1.0, 0.9, 1.6), (s * 1.2, 0.2, 10.9), "VJ_Negro")
        b.cyl(0.4, 0.22, 0.7, (s * 1.2, 0.2, 10.9), "VJ_Oro", seg=8)
    b.pyramid(5.4, 1.2, 1.2, (0, 0.2, 12.6), "VJ_Teja")
    b.box((0.12, 0.12, 1.3), (0, 0.2, 14.3), "VJ_Reja")
    b.box((0.7, 0.12, 0.12), (0, 0.2, 14.5), "VJ_Reja")
    return b.build()

def chimenea():
    b = Builder("VJ_Chimenea")
    b.box((0.6, 0.6, 1.0), (0, 0, 0.5), "VJ_Cal")
    b.pyramid(0.8, 0.8, 0.35, (0, 0, 1.0), "VJ_Teja")
    return b.build()

def arco():   # barricade arch of a pena: two timber posts, a crossbeam, a cloth banner and bunting; 2.4 m opening
    b = Builder("VJ_PortalPuente")
    for s in (-1, 1):
        b.box((0.3, 0.3, 3.4), (s * 1.35, 0, 1.7), "VJ_Madera")
        b.box((0.5, 0.5, 0.3), (s * 1.35, 0, 0.15), "VJ_Piedra")
        b.box((0.9, 0.12, 0.12), (s * 0.95, 0, 3.05), "VJ_Madera", rot=[('Y', s * 40)])
    b.box((3.3, 0.25, 0.25), (0, 0, 3.45), "VJ_Madera")
    b.box((2.2, 0.04, 0.55), (0, -0.14, 3.05), "VN_AwningPaint")
    cols = ("VJ_Rojo", "VJ_Amarillo", "VJ_Verde", "VJ_Azul")
    for i in range(10): b.box((0.2, 0.02, 0.26), (-1.2 + i * 0.27, 0.14, 3.2), cols[i % 4], rot=[('Y', 45)])
    return b.build()

def toro():   # fighting bull at a gallop, 2.3 m long, 1.5 m at the hump; origin on the ground, head towards -Y
    b = Builder("VJ_Toro")
    b.box((0.75, 1.5, 0.8), (0, 0.15, 1.0), "VJ_Toro")
    b.box((0.8, 0.7, 0.95), (0, -0.4, 1.1), "VJ_Toro")                             # hump / shoulders
    b.box((0.55, 0.5, 0.55), (0, -0.95, 0.95), "VJ_Toro", rot=[('X', 25)])         # neck, head down
    b.box((0.42, 0.55, 0.42), (0, -1.25, 0.72), "VJ_Toro", rot=[('X', 35)])
    b.box((0.3, 0.14, 0.2), (0, -1.5, 0.55), "VJ_Piel")
    for s in (-1, 1):
        b.cyl(0.07, 0.02, 0.5, (s * 0.38, -1.25, 0.95), "VJ_Cuerno", seg=6, rot=[('Y', s * 60), ('X', 30)])
        b.ball(0.05, (s * 0.2, -1.42, 0.86), "VJ_Rojo", seg=6)                            # angry eyes
    b.box((0.06, 0.06, 0.7), (0, 1.0, 1.1), "VJ_Toro", rot=[('X', -50)])           # tail
    return b.build()

def patas():   # a pair of bull legs hanging from the origin (the hip): Rounds/Encierro swings them for the gallop
    b = Builder("VJ_Toro_Patas")
    for s in (-1, 1):
        b.box((0.16, 0.18, 0.8), (s * 0.26, 0, -0.4), "VJ_Toro")
        b.box((0.18, 0.2, 0.12), (s * 0.26, 0, -0.78), "VJ_Cuerno")
    return b.build()

def camion():   # cattle lorry, 4 x 1.9 x 2.7 m along X: cab on +X, slatted box, the tailgate (-X) is the ramp
    b = Builder("VJ_Camion")
    b.box((2.7, 1.9, 1.9), (-0.55, 0, 1.55), "VJ_MaderaClara")
    for z in (0.95, 1.4, 1.85, 2.3):
        for s in (-1, 1): b.box((2.74, 0.05, 0.16), (-0.55, s * 0.96, z), "VJ_Madera")
    b.box((0.06, 1.7, 1.8), (-1.93, 0, 1.55), "VJ_Negro")                          # the dark inside (the bulls come out here)
    b.box((1.1, 1.85, 1.6), (1.4, 0, 1.25), "VJ_Rojo")
    b.box((0.06, 1.6, 0.6), (1.96, 0, 1.6), "VN_Glass")
    b.box((4.0, 1.7, 0.3), (0, 0, 0.5), "VJ_Reja")
    for x in (-1.3, 1.3):
        for s in (-1, 1): b.cyl(0.42, 0.42, 0.3, (x, s * 0.85, 0.42), "VJ_Goma", seg=10, rot=[('X', 90)])
    return b.build()

def tractor():   # tractor + trailer with benches (the way out), 6.4 x 2.2 x 2.4 m along X, tractor on +X
    b = Builder("VJ_Tractor")
    b.box((1.7, 0.9, 0.8), (2.2, 0, 1.2), "VJ_Verde")
    b.box((0.9, 1.0, 1.2), (1.2, 0, 1.6), "VJ_Verde")
    b.box((0.95, 1.05, 0.08), (1.2, 0, 2.3), "VJ_Amarillo")
    b.cyl(0.05, 0.05, 0.9, (2.7, 0.25, 2.0), "VJ_Reja", seg=6)
    for s in (-1, 1):
        b.cyl(0.75, 0.75, 0.4, (1.2, s * 0.85, 0.75), "VJ_Goma", seg=12, rot=[('X', 90)])
        b.cyl(0.3, 0.3, 0.42, (1.2, s * 0.85, 0.75), "VJ_Amarillo", seg=8, rot=[('X', 90)])
        b.cyl(0.42, 0.42, 0.25, (2.8, s * 0.6, 0.42), "VJ_Goma", seg=10, rot=[('X', 90)])
        b.cyl(0.38, 0.38, 0.25, (-1.6, s * 1.0, 0.38), "VJ_Goma", seg=10, rot=[('X', 90)])
        b.box((3.4, 0.08, 0.6), (-1.5, s * 1.05, 1.15), "VJ_Madera")
        b.box((3.0, 0.4, 0.08), (-1.5, s * 0.75, 1.2), "VJ_MaderaClara")            # benches
    b.box((3.4, 2.1, 0.14), (-1.5, 0, 0.82), "VJ_MaderaClara")
    b.box((0.08, 2.1, 0.6), (-3.2, 0, 1.15), "VJ_Madera")
    b.box((0.9, 0.1, 0.1), (0.3, 0, 0.7), "VJ_Reja")
    cols = ("VJ_Rojo", "VJ_Amarillo", "VJ_Verde", "VJ_Azul")
    for i in range(12): b.box((0.2, 0.02, 0.24), (-3.0 + i * 0.27, -1.1, 1.3), cols[i % 4], rot=[('Y', 45)])
    return b.build()

def paca():   # stack of straw bales, 1.9 x 1.9 x 1.7 m
    b = Builder("VJ_Paca")
    for x, y, z, r in ((-0.48, 0, 0.42, 0), (0.48, 0, 0.42, 0), (0, 0, 1.26, 90)):
        b.box((0.9, 1.8, 0.84), (x, y, z), "VJ_Paja", rot=[('Z', r)])
        for d in (-0.5, 0.5): b.box((0.93, 0.04, 0.87) if r == 0 else (0.04, 0.93, 0.87), (x + (d if r else 0), y + (0 if r else d), z), "VJ_Madera")
    return b.build()

def mesa():   # folding table with two benches and plastic cups: low cover you can jump (0.8 m)
    b = Builder("VJ_Mesa")
    b.box((1.8, 0.7, 0.05), (0, 0, 0.74), "VJ_Blanco")
    for s in (-1, 1):
        b.box((0.05, 0.6, 0.72), (s * 0.75, 0, 0.36), "VJ_Chapa")
        b.box((1.7, 0.28, 0.05), (0, s * 0.62, 0.44), "VJ_MaderaClara")
        for x in (-0.7, 0.7): b.box((0.05, 0.24, 0.42), (x, s * 0.62, 0.21), "VJ_Chapa")
    for x, c in ((-0.5, "VJ_Rojo"), (0.1, "VJ_Blanco"), (0.55, "VJ_Rojo")): b.cyl(0.04, 0.05, 0.1, (x, 0.1, 0.8), c, seg=6)
    return b.build()

def columna():   # stone column of the town hall arcade, 0.7 m shaft, 3.6 m
    b = Builder("VJ_Columna")
    b.box((1.0, 1.0, 0.3), (0, 0, 0.15), "VJ_Piedra")
    b.cyl(0.36, 0.32, 2.9, (0, 0, 1.75), "VJ_Piedra", seg=10)
    b.box((1.0, 1.0, 0.4), (0, 0, 3.4), "VJ_Piedra")
    return b.build()

def fuente():   # village fountain: octagonal basin, pillar with four spouts, 1.9 m across
    b = Builder("VJ_Fuente")
    b.cyl(0.95, 0.95, 0.6, (0, 0, 0.3), "VJ_Piedra", seg=8)
    b.cyl(0.8, 0.8, 0.05, (0, 0, 0.58), "VJ_Azul", seg=8)
    b.cyl(0.18, 0.14, 1.5, (0, 0, 1.2), "VJ_Piedra", seg=8)
    b.ball(0.24, (0, 0, 2.05), "VJ_Piedra", seg=8)
    for a in range(4): b.cyl(0.03, 0.03, 0.3, (0.2 * math.cos(a * math.pi / 2), 0.2 * math.sin(a * math.pi / 2), 1.3), "VJ_Reja", seg=6, rot=[('X', 90), ('Z', a * 90 + 90)])
    return b.build()

def marquesina():   # bus shelter with a bench and the timetable, 1.9 x 1.2 x 2.4 m
    b = Builder("VJ_Marquesina")
    for s in (-1, 1): b.box((0.08, 0.08, 2.3), (s * 0.9, 0.5, 1.15), "VJ_Chapa")
    b.box((1.9, 0.05, 1.6), (0, 0.55, 1.4), "VN_Glass")
    b.box((2.0, 1.3, 0.08), (0, 0, 2.34), "VJ_Verde")
    b.box((1.6, 0.4, 0.06), (0, 0.3, 0.5), "VJ_MaderaClara")
    for s in (-1, 1): b.box((0.06, 0.36, 0.5), (s * 0.7, 0.3, 0.25), "VJ_Chapa")
    b.box((0.5, 0.03, 0.7), (0.55, 0.5, 1.5), "VJ_Blanco")
    return b.build()

def barra():   # one 1.95 m piece of the bar counter with its striped canopy and a beer tap
    b = Builder("VJ_Barra")
    b.box((1.95, 0.7, 1.05), (0, 0.2, 0.525), "VJ_Madera")
    b.box((1.95, 0.9, 0.06), (0, 0.15, 1.08), "VJ_Chapa")
    for s in (-1, 1): b.box((0.08, 0.08, 2.5), (s * 0.9, 0.7, 1.25), "VJ_Chapa")
    b.box((1.95, 1.7, 0.07), (0, 0.0, 2.45), "VN_AwningPaint", rot=[('X', -8)])
    b.cyl(0.04, 0.04, 0.4, (0.3, 0.2, 1.3), "VJ_Oro", seg=6)
    b.box((0.06, 0.2, 0.05), (0.3, 0.1, 1.48), "VJ_Oro")
    for x in (-0.6, -0.4, -0.2): b.cyl(0.05, 0.04, 0.16, (x, 0.0, 1.19), "VJ_Amarillo", seg=6)
    return b.build()

def puesto():   # fair stall: counter, back wall with prizes, striped awning; 1.95 x 1.95 x 2.7 m, front -Y
    b = Builder("VJ_Puesto")
    b.box((1.95, 0.5, 1.0), (0, -0.6, 0.5), "VJ_Rojo")
    b.box((1.95, 0.56, 0.06), (0, -0.6, 1.03), "VJ_Blanco")
    b.box((1.95, 0.1, 2.5), (0, 0.9, 1.25), "VJ_Azul")
    for s in (-1, 1): b.box((0.1, 1.6, 2.5), (s * 0.92, 0.1, 1.25), "VJ_Azul")
    b.box((2.0, 2.0, 0.08), (0, 0, 2.55), "VN_AwningPaint")
    b.box((2.0, 0.05, 0.35), (0, -0.98, 2.4), "VJ_Blanco")
    for i, c in enumerate(("VJ_Rosa", "VJ_Amarillo", "VJ_Verde", "VJ_Rosa", "VJ_Azul")): b.ball(0.16, (-0.7 + i * 0.35, 0.75, 1.7 + (i % 2) * 0.35), c, seg=6)   # prizes
    return b.build()

def coches_choque():   # bumper-car rink, 8 x 8 m: floor, roof on posts with a light fascia, cars parked inside
    b = Builder("VJ_CochesChoque")
    b.box((8.0, 8.0, 0.25), (0, 0, 0.125), "VJ_Chapa")
    b.box((8.0, 8.0, 0.35), (0, 0, 3.6), "VJ_Rojo")
    for sx in (-1, 1):
        for sy in (-1, 1): b.box((0.3, 0.3, 3.4), (sx * 3.8, sy * 3.8, 1.7), "VJ_Amarillo")
    for s in (-1, 1):
        b.box((8.1, 0.1, 0.5), (0, s * 4.0, 3.4), "VJ_Amarillo")
        b.box((0.1, 8.1, 0.5), (s * 4.0, 0, 3.4), "VJ_Amarillo")
    cols = ("VJ_Bulb_R", "VJ_Bulb_Y", "VJ_Bulb_G", "VJ_Bulb_B")
    for i in range(16):
        for s in (-1, 1):
            b.ball(0.1, (-3.75 + i * 0.5, s * 4.08, 3.4), cols[i % 4], seg=6)
            b.ball(0.1, (s * 4.08, -3.75 + i * 0.5, 3.4), cols[(i + 2) % 4], seg=6)
    for x, y, c, r in ((-2.2, -1.5, "VJ_Azul", 20), (1.6, 0.8, "VJ_Rojo", 110), (-0.5, 2.4, "VJ_Verde", 250), (2.4, -2.4, "VJ_Amarillo", 330), (-2.6, 2.0, "VJ_Rosa", 70)):
        b.box((1.0, 1.5, 0.45), (x, y, 0.5), c, rot=[('Z', r)])
        b.box((1.15, 1.65, 0.18), (x, y, 0.36), "VJ_Goma", rot=[('Z', r)])
        b.cyl(0.02, 0.02, 2.9, (x, y, 2.0), "VJ_Reja", seg=4)
    return b.build()

def noria():   # big wheel, 6 x 3 m footprint, 11 m tall: A-frame legs, rim with eight gondolas and lights
    b = Builder("VJ_Noria")
    for s in (-1, 1):
        for d in (-1, 1): b.box((0.25, 0.25, 6.6), (d * 1.6, s * 1.2, 3.1), "VJ_Blanco", rot=[('Y', d * 28)])
        b.box((5.4, 0.3, 0.3), (0, s * 1.2, 0.15), "VJ_Blanco")
    b.cyl(0.25, 0.25, 2.8, (0, 0, 6.0), "VJ_Rojo", seg=8, rot=[('X', 90)])
    R, n = 4.6, 16
    cols = ("VJ_Bulb_R", "VJ_Bulb_Y", "VJ_Bulb_G", "VJ_Bulb_B")
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        x0, z0, x1, z1 = R * math.cos(a0), R * math.sin(a0), R * math.cos(a1), R * math.sin(a1)
        ln = math.hypot(x1 - x0, z1 - z0)
        for s in (-1, 1):
            b.box((ln + 0.05, 0.1, 0.12), ((x0 + x1) / 2, s * 0.7, 6.0 + (z0 + z1) / 2), "VJ_Blanco", rot=[('Y', -math.degrees(math.atan2(z1 - z0, x1 - x0)))])
            b.ball(0.11, (x0, s * 0.78, 6.0 + z0), cols[i % 4], seg=6)
        if i % 2 == 0:
            b.box((R, 0.08, 0.08), (x0 / 2, 0, 6.0 + z0 / 2), "VJ_Blanco", rot=[('Y', -math.degrees(a0))])
            b.box((0.9, 1.2, 0.7), (x0, 0, 6.0 + z0 - 0.6), ("VJ_Rojo", "VJ_Azul", "VJ_Amarillo", "VJ_Verde")[(i // 2) % 4])
            b.box((1.0, 1.3, 0.06), (x0, 0, 6.0 + z0 + 0.1), "VJ_Blanco")
    b.box((1.6, 1.2, 2.2), (-2.4, -1.9, 1.1), "VJ_Rojo")                           # ticket booth
    b.box((1.0, 0.06, 0.7), (-2.4, -2.52, 1.5), "VN_Glass")
    return b.build()

def churreria():   # churros caravan, 3.9 x 1.9 x 2.8 m along X, serving hatch on -Y
    b = Builder("VJ_Churreria")
    b.box((3.9, 1.9, 2.1), (0, 0, 1.35), "VJ_Blanco")
    b.box((3.0, 0.06, 0.9), (0, -0.96, 1.6), "VJ_Negro")
    b.box((3.2, 0.8, 0.06), (0, -1.3, 2.2), "VN_AwningPaint", rot=[('X', -15)])
    b.box((3.2, 0.3, 0.06), (0, -1.05, 1.12), "VJ_Chapa")
    b.box((3.9, 0.06, 0.5), (0, -0.97, 2.65), "VJ_Amarillo")
    for x in (-1.3, 1.3): b.cyl(0.32, 0.32, 0.25, (x, 0, 0.32), "VJ_Goma", seg=10, rot=[('X', 90)])
    b.cyl(0.12, 0.12, 0.8, (1.4, 0.4, 2.8), "VJ_Chapa", seg=6)
    for i in range(5): b.cyl(0.03, 0.03, 0.4, (-0.8 + i * 0.15, -1.1, 1.2), "VJ_Paja", seg=5, rot=[('Y', 90)])
    return b.build()

def templete():   # bandstand roof for the 8 x 4 m raised platform: eight iron posts, pointed roof, a ring of lights
    b = Builder("VJ_Templete")
    for x in (-3.8, -1.3, 1.3, 3.8):
        for s in (-1, 1): b.cyl(0.07, 0.07, 3.0, (x, s * 1.85, 1.5), "VJ_Verde", seg=6)
    b.box((8.0, 4.1, 0.14), (0, 0, 3.05), "VJ_Verde")
    b.pyramid(8.4, 4.5, 1.5, (0, 0, 3.1), "VJ_Teja")
    b.ball(0.2, (0, 0, 4.7), "VJ_Oro", seg=6)
    cols = ("VJ_Bulb_R", "VJ_Bulb_Y", "VJ_Bulb_G", "VJ_Bulb_B")
    for i in range(17):
        for s in (-1, 1): b.ball(0.09, (-4.0 + i * 0.5, s * 2.1, 2.95), cols[i % 4], seg=6)
    for i in range(8):
        for s in (-1, 1): b.ball(0.09, (s * 4.05, -1.75 + i * 0.5, 2.95), cols[(i + 1) % 4], seg=6)
    return b.build()

def guirnalda():   # a string of coloured bulbs and bunting across a 4 m street: along X, sagging, origin at mid span on the cable
    b = Builder("VJ_Guirnalda")
    n = 12
    cols = ("VJ_Bulb_R", "VJ_Bulb_Y", "VJ_Bulb_G", "VJ_Bulb_B")
    flags = ("VJ_Rojo", "VJ_Amarillo", "VJ_Verde", "VJ_Azul")
    pts = [(-2.0 + 4.0 * i / n, -0.35 * (1 - ((2.0 * i / n) - 1) ** 2)) for i in range(n + 1)]
    for i in range(n):
        (x0, z0), (x1, z1) = pts[i], pts[i + 1]
        ln = math.hypot(x1 - x0, z1 - z0)
        b.box((ln + 0.01, 0.02, 0.02), ((x0 + x1) / 2, 0, (z0 + z1) / 2), "VJ_Cable", rot=[('Y', -math.degrees(math.atan2(z1 - z0, x1 - x0)))])
        b.ball(0.085, ((x0 + x1) / 2, 0, (z0 + z1) / 2 - 0.1), cols[i % 4], seg=6)
        if i % 2 == 0: b.box((0.2, 0.015, 0.26), ((x0 + x1) / 2, 0.04, (z0 + z1) / 2 - 0.32), flags[(i // 2) % 4], rot=[('Y', 45)])
    return b.build()

def bombo():   # the big drum of the charanga, worn on the belly: origin = centre, skin towards -Y... it hangs sideways
    b = Builder("VJ_Bombo")
    b.cyl(0.42, 0.42, 0.38, (0, 0, 0), "VJ_Rojo", seg=12, rot=[('Y', 90)])
    for s in (-1, 1):
        b.cyl(0.4, 0.4, 0.02, (s * 0.2, 0, 0), "VJ_Blanco", seg=12, rot=[('Y', 90)])
        b.cyl(0.44, 0.44, 0.04, (s * 0.17, 0, 0), "VJ_Oro", seg=12, rot=[('Y', 90)])
    return b.build()

def cabezudo():   # the golden cabezudo: a fiesta big-head figure, 2.1 m, all gold (the mission's hidden figure)
    b = Builder("VJ_Cabezudo")
    b.cyl(0.32, 0.26, 0.9, (0, 0, 0.75), "VJ_Oro", seg=8)
    for s in (-1, 1):
        b.cyl(0.1, 0.09, 0.5, (s * 0.14, 0, 0.25), "VJ_Oro", seg=6)
        b.cyl(0.07, 0.06, 0.6, (s * 0.4, -0.05, 0.9), "VJ_Oro", seg=6, rot=[('Y', s * 20)])
    b.ball(0.62, (0, 0, 1.6), "VJ_Oro", scale=(1, 0.95, 1.05), seg=10)
    b.ball(0.16, (0, -0.6, 1.55), "VJ_Oro", seg=6)                                 # nose
    for s in (-1, 1):
        b.ball(0.09, (s * 0.24, -0.52, 1.75), "VJ_Negro", seg=6)
        b.ball(0.16, (s * 0.62, 0, 1.6), "VJ_Oro", seg=6)                          # ears
    b.box((0.4, 0.06, 0.07), (0, -0.56, 1.3), "VJ_Negro")
    b.cyl(0.66, 0.66, 0.05, (0, 0, 2.05), "VJ_Oro", seg=10)                        # beret
    b.ball(0.5, (0, 0, 2.1), "VJ_Oro", scale=(1, 1, 0.35), seg=8)
    return b.build()

clear()
for fn in (fachada_a, fachada_b, fachada_c, fachada_d, frente_iglesia, chimenea, arco, toro, patas, camion, tractor, paca, mesa, columna,
           fuente, marquesina, barra, puesto, coches_choque, noria, churreria, templete, guirnalda, bombo, cabezudo):
    make(fn)
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("PROPS_RESULT " + json.dumps(res))
