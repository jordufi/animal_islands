# Builds Assets/Maps/Crucero/Crucero_Layout.txt (the cruise "Reina del Imserso") from rectangles, floor by floor.
# Run:  python Tools/Maps/make_crucero_layout.py --check   (prints every floor and the checks, writes nothing)
#       python Tools/Maps/make_crucero_layout.py           (writes the layout when the checks pass; --heights also prints them)
#       --png draws every floor to Renders/Plano_Crucero.png (with --check: draws, writes no layout)
# Format with real floors: Docs/PLANTAS_FORMATO.md. Design: Docs/CRUCERO_DISENO.md. Grid: 1 cell = 2 m, row 0 = north.
#
# Two acts (user, 2026-11-17). Act 1 in the port: the quay (start) and two paid zones (terminal, luggage hall); the
# boarding pass lies in the second one you open and opens the gangway gate (Q). Round 4: horn + 2 min countdown, the
# ship sails, the port is gone for good (players left ashore: game over solo, out until next round in co-op).
# Act 2 at sea, at night: three decks over the same hull (cols 6-65, bow to the east). Floor 0 = the quay and the lower
# deck (cabins, engine room, stern marina with the jet skis); floor 1 = the main deck: a promenade ring round the
# deckhouse (atrium, dining room aft, casino and theatre fore); floor 2 = the sun deck (pool, slide tower, funnel)
# and the bridge (boss + lifeboat). Boardings by launches in rounds 5, 10, 15 along one rail (':' = grappling hooks).
# ORDER MATTERS: base fills first; stairs then carve voids in the floor above.
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from floors import Plan  # noqa: E402

P = Plan(72, 34)
D0 = P.floor('0', 'Muelle y cubierta inferior', 0)
D1 = P.floor('1', 'Cubierta principal', 2)
D2 = P.floor('2', 'Cubierta de sol', 4)

P.zones = [
    ('a', 'Muelle', 'El muelle', 0, '9C9488', 'start'),
    ('r', 'Terminal', 'Terminal marítima', 1, 'B8AE9C', 'techo'),
    ('g', 'Almacen', 'Almacén de equipajes', 2, '8F8576', 'techo'),
    ('c', 'Paseo', 'Paseo de cubierta', 0, 'C8B48C', ''),
    ('i', 'Camarotes', 'Cubierta de camarotes', 3, '6F7C8C', 'techo'),
    ('e', 'Casino', 'Casino y teatro', 4, '9C3C4C', 'techo firstbox'),
    ('f', 'Comedor', 'Comedor', 5, 'C29C6C', 'techo firstbox'),
    ('p', 'CubiertaSol', 'Cubierta de sol', 6, '7FB8C8', ''),
    ('l', 'Puente', 'Puente de mando', 7, 'E8E8E0', 'techo'),
]
P.doors = [
    ('1', 'Puerta de la terminal', 750, 'a r', ''),
    ('2', 'Persiana del almacén', 750, 'a g', ''),
    ('3', 'Escalera central (camarotes)', 1000, 'c i', ''),
    ('4', 'Casino y teatro', 1000, 'c e', ''),
    ('5', 'Comedor', 1250, 'c f', ''),
    ('6', 'Escalera de popa (marina)', 1250, 'c i', ''),
    ('7', 'Escalera de proa (cubierta de sol)', 1500, 'e p', ''),
    ('8', 'Escalera de popa (cubierta de sol)', 1500, 'f p', ''),
    ('9', 'PUENTE DE MANDO - JEFE FINAL', 10000, 'p l', 'llave'),
]

# =============================================================== base fills
HULL = (6, 3, 65, 14)
def hull_shape(F, ch, rows=(3, 14)):
    """The hull outline: square stern (west), pointed bow (east)."""
    r0, r1 = rows
    F.fill(6, r0, 59, r1, ch)
    for i, c in enumerate(range(60, 66)):          # the bow narrows by a row each side every column
        F.fill(c, r0 + i, c, r1 - i, ch)


hull_shape(D0, '#')
hull_shape(D1, '#')
D2.fill(12, 3, 57, 14, '#')

# =============================================================== FLOOR 0: the port (south)
D0.fill(6, 22, 58, 33, '#')
D0.fill(25, 22, 40, 26, 'a')                # el muelle
D0.fill(12, 23, 23, 30, 'r')                # la terminal
D0.fill(42, 23, 52, 30, 'g')                # el almacen de equipajes
D0.marks('1', (24, 23), (24, 24))
D0.marks('2', (41, 23), (41, 24))
# ---- a: el muelle
D0.marks('o', (27, 24), (28, 24), (37, 24), (38, 24))             # containers
D0.marks('x', (26, 22), (30, 22), (35, 22), (39, 22))             # bollards on the quay edge
D0.marks('@', (32, 25)); D0.marks('B', (25, 26))
D0.marks('s', (25, 22), (40, 22), (29, 26), (36, 26), (40, 26))
D0.marks('Q', (32, 22), (33, 22))           # the gangway gate (the boarding pass opens it)
# ---- r: la terminal (check-in, luggage belts, duty free)
D0.marks('o', (14, 25), (15, 25), (16, 25), (18, 25), (19, 25), (20, 25))   # check-in counters
D0.marks('o', (14, 28), (15, 28), (19, 28), (20, 28))                       # duty-free shelves
D0.marks('x', (17, 27), (13, 30))
D0.marks('&', (12, 23)); D0.marks('D', (22, 30)); D0.marks('B', (12, 30))
D0.marks('s', (12, 26), (22, 23), (16, 30), (12, 28))
# ---- g: el almacen de equipajes
D0.marks('o', (44, 25), (45, 25), (44, 28), (45, 28), (48, 25), (49, 25), (48, 28), (49, 28))   # luggage piles, containers
D0.marks('x', (51, 26), (47, 30))
D0.marks('&', (52, 23)); D0.marks('G', (52, 27)); D0.marks('B', (42, 30))
D0.marks('s', (42, 26), (52, 30), (46, 23), (50, 23))
# ---- the gangway: a ramp over the harbour water up to the main deck (2 levels: run, landing, run)
D0.fill(32, 17, 33, 21, 'c')
D0.ramp([(32, 21), (33, 21), (32, 20), (33, 20)], '^')
D0.level(32, 19, 33, 19, 1)
D0.ramp([(32, 18), (33, 18), (32, 17), (33, 17)], '^')
D0.fill(31, 17, 31, 21, '#'); D0.fill(34, 17, 34, 21, '#')

# =============================================================== FLOOR 0: the lower deck (i) inside the hull
D0.fill(10, 6, 58, 6, 'i'); D0.fill(10, 11, 58, 11, 'i')          # two long corridors
for c in (14, 26, 41, 50):
    D0.fill(c, 6, c, 11, 'i')                                      # cross corridors
D0.fill(58, 6, 58, 11, 'i')
D0.fill(6, 5, 9, 12, 'i')                   # the stern: engine room and marina
D0.fill(10, 4, 12, 5, 'i'); D0.fill(10, 12, 12, 13, 'i')
for c0, r0, c1, r1 in ((17, 7, 18, 7), (22, 10, 23, 10), (30, 7, 30, 8), (45, 7, 46, 7), (54, 10, 55, 10),
                       (36, 4, 36, 5), (20, 12, 20, 13), (47, 12, 47, 13), (53, 4, 53, 5)):
    D0.fill(c0, r0, c1, r1, 'i')                                   # open cabins: dead ends
D0.marks('o', (7, 6), (8, 6), (7, 9))      # engines
D0.marks('E', (6, 12))                      # power to the lifeboat davits (pescantes)
D0.marks('Y', (6, 7))                       # petrol for the lifeboat (in the marina)
D0.marks('N', (6, 10))                      # jet ski stop (stern marina): round the hull to the bow
D0.marks('O', (17, 7), (45, 7), (55, 10))   # the captain's sea charts: one of three cabins
D0.marks('U', (28, 6)); D0.marks('D', (53, 4)); D0.marks('B', (30, 7), (47, 13))
D0.marks('s', (10, 4), (12, 13), (20, 6), (32, 6), (44, 6), (56, 6), (17, 11), (29, 11), (46, 11), (58, 8), (36, 4), (22, 10), (20, 13))
D0.marks('s', (34, 11), (38, 11), (57, 11))  # both sides of the central stair, the bow end (coverage)

# =============================================================== FLOOR 1: the main deck
D1.fill(6, 3, 59, 4, 'c'); D1.fill(6, 13, 59, 14, 'c')            # promenade, both sides
D1.fill(6, 3, 7, 14, 'c')                                          # round the stern
for i, c in enumerate(range(60, 66)):                              # the foredeck (bow)
    D1.fill(c, 3 + i, c, 14 - i, 'c')
D1.fill(58, 5, 59, 12, 'c')
D1.fill(8, 3, 11, 14, 'c'); D1.fill(12, 3, 12, 4, 'c'); D1.fill(12, 13, 12, 14, 'c')   # the stern deck
D1.fill(13, 6, 28, 11, 'f')                 # comedor (aft)
D1.fill(30, 5, 37, 12, 'c')                 # atrium (open to both promenades), the central stair in the middle
D1.fill(39, 6, 56, 11, 'e')                 # casino and theatre (fore)
D1.marks('4', (38, 8), (38, 9))
D1.marks('5', (29, 8), (29, 9))
# ---- f: comedor
D1.marks('o', (15, 7), (17, 7), (15, 10), (17, 10), (25, 7), (27, 7), (25, 10), (27, 10))   # round tables
D1.marks('D', (13, 6)); D1.marks('M', (28, 11)); D1.marks('B', (13, 11))
D1.marks('s', (14, 6), (24, 6), (24, 11), (13, 9), (28, 7))
# ---- e: casino (slot machines, roulette) and theatre (stage with the karaoke)
D1.marks('o', (47, 6), (48, 6), (49, 6), (47, 11), (48, 11), (49, 11))   # slot machines
D1.marks('x', (52, 8), (52, 9))             # the roulette
D1.marks('K', (55, 8)); D1.marks('D', (39, 11)); D1.marks('B', (56, 6)); D1.marks('J', (51, 6))
D1.marks('s', (39, 6), (53, 6), (56, 11), (51, 11), (40, 11))
# ---- c: atrium, promenade, stern deck, foredeck
D1.marks('x', (31, 8), (37, 6))
# lifeboat davits and deck lockers on the inner side of the promenade: no 100 m line of sight along the ship
D1.marks('o', (17, 4), (28, 4), (44, 4), (54, 4), (17, 13), (28, 13), (44, 13), (54, 13))
D1.marks('B', (31, 5))
D1.marks('N', (65, 8))                      # jet ski stop at the bow (a ladder up from the sea)
D1.marks('L', (64, 7), (8, 3), (59, 14))    # where the lifeboat key may lie
D1.marks('J', (6, 14)); D0.marks('J', (36, 4))
D1.marks(':', (15, 3), (25, 3), (35, 3), (45, 3), (55, 3), (15, 14), (25, 14), (45, 14), (55, 14))   # grappling hooks (boardings)
D1.marks('s', (20, 4), (40, 4), (50, 13), (20, 13), (6, 3), (6, 14), (63, 6), (63, 11), (12, 5))
# crew doors in the superstructure every ~8 cells (a promenade with spawns 20 cells apart had 134 safe cells) and the atrium
D1.marks('s', (27, 4), (48, 4), (56, 4), (26, 13), (42, 13), (56, 13), (30, 6), (37, 11))
# the gangway arrives at the starboard promenade
D1.fill(32, 15, 33, 16, 'c'); D1.fill(32, 17, 33, 21, '~')

# =============================================================== FLOOR 2: the sun deck (p) and the bridge (l)
D2.fill(12, 3, 49, 14, 'p')
D2.fill(26, 7, 29, 10, '#')                 # the funnel
D2.fill(34, 6, 39, 10, 'w')                 # the pool
D2.marks('w', (31, 4), (31, 13))            # jacuzzis
D2.marks('x', (33, 4), (35, 4), (37, 4), (33, 13), (35, 13), (37, 13), (41, 5), (39, 12))   # sun loungers
D2.marks('D', (44, 3)); D2.marks('B', (24, 13))
D2.marks('o', (20, 6), (20, 11), (44, 6), (47, 9), (32, 9))   # deck bar, potted palms, the slides' supports
D2.marks('o', (22, 5), (22, 12), (46, 5))                      # cabanas
D2.marks('x', (24, 5), (24, 12), (48, 6), (48, 12))            # shuffleboard lanes (the retirees' deck game)
D2.marks(':', (20, 3), (40, 3), (20, 14), (42, 14))
D2.marks('s', (25, 7), (30, 10), (12, 3), (12, 14), (49, 4), (49, 13), (18, 9))
D2.marks('s', (35, 5), (36, 11), (40, 7), (46, 10))   # out of the pool by its ladders, doors of the fore stair housing
# ---- the slide tower (stern end of the sun deck): stairs up to a platform 4 m above the deck
D2.fill(13, 5, 17, 13, '#')
D2.fill(14, 6, 16, 7, 'p'); D2.level(14, 6, 16, 7, 2)                 # the platform (level 2 = 12 m)
D2.fill(15, 8, 15, 13, 'p')
D2.ramp([(15, 12), (15, 11)], '^'); D2.level(15, 10, 15, 10, 1); D2.ramp([(15, 9), (15, 8)], '^')
D2.marks('P', (16, 6))                      # the Microondas, at the top of the slides
# ---- l: el puente de mando (bow end)
D2.fill(50, 3, 57, 14, '#')
D2.fill(51, 3, 57, 14, 'l'); D2.fill(53, 6, 56, 11, '#')       # a ring of deck and wings round the wheelhouse: room to kite the boss
D2.marks('9', (50, 8), (50, 9))
D2.marks('b', (52, 8)); D2.marks('X', (54, 3))
D2.marks('s', (51, 3), (57, 8), (55, 14), (54, 5), (51, 13))

# =============================================================== stairs
# lower deck <-> main deck: the central stair (door 3 on the main deck, in the atrium)
D0.put(33, 11, 'i')
P.stair(D0, (33, 11), (0, -1), (1, 0), 'i')            # arrives on the main deck at (35, 11)
D1.marks('#', (34, 11), (36, 11)); D1.marks('3', (35, 12))
# lower deck <-> stern deck: the marina stair (door 6 on the main deck)
D0.put(9, 11, 'i')
P.stair(D0, (9, 11), (0, -1), (1, 0), 'i')             # arrives on the main deck at (11, 11)
D1.marks('#', (10, 11), (12, 11)); D1.marks('6', (11, 12))
# main deck -> sun deck: fore stair (door 7, in the casino) and aft stair (door 8, in the dining room)
D1.put(42, 11, 'p'); D1.marks('7', (41, 11)); D1.put(43, 11, '#')
P.stair(D1, (42, 11), (0, -1), (1, 0), 'p')            # arrives on the sun deck at (44, 11)
D1.put(20, 11, 'p'); D1.marks('8', (19, 11)); D1.put(21, 11, '#')
P.stair(D1, (20, 11), (0, -1), (1, 0), 'p')            # arrives on the sun deck at (22, 11)

# =============================================================== slides, points
P.link('1', 'Tobogán de la piscina', ('2', 14, 6), ('2', 33, 8), 'tobogan')
P.link('2', 'Tobogán por fuera del casco', ('2', 16, 7), ('1', 9, 4), 'tobogan')
for name, fid, c, r in (
        ('pasarela', '0', 32, 21), ('puerto_skyline', '0', 32, 33), ('neon_hotel_jubiles', '0', 70, 30),
        ('bote_salvavidas', '2', 53, 2), ('lanchas_babor', '1', 35, 0), ('lanchas_estribor', '1', 35, 17),
        ('timon', '2', 54, 8), ('chimenea', '2', 27, 8), ('escenario_teatro', '1', 55, 9)):
    P.point(name, fid, c, r)

HEADER = """# Crucero "Reina del Imserso" - a cruise ship in two acts: the port, then the open sea (Tools > Island > Setup Crucero).
# GENERATED by Tools/Maps/make_crucero_layout.py: edit the script, not this file. Format: Docs/PLANTAS_FORMATO.md.
# 1 character = 2 x 2 m, row 0 = north, the bow points east. Theme "crucero":
#   .  sea (floor 0) / outside   #  hull, cabins, port buildings   ~  void (stairwells, the gangway)
#   o  tall prop (containers, counters, slot machines, tables, engines)   x  low cover (bollards, loungers, roulette)   w  pool
#   Q  the gangway gate (opens with the boarding pass, shuts when the ship sails)   :  grappling hook spot (boardings)
#   1-9 paid doors (9 = the bridge: mission + the lifeboat key)
#   markers: @ start, s spawn, B box (La Tragaperras), D drink, P Microondas, G U M wall weapons, K karaoke, J record,
#   & where the boarding pass may lie (the second port zone you open), E power to the davits, Y petrol (lifeboat),
#   O where the sea charts may lie, L where the lifeboat key may lie, N jet ski stop, b boss, X lifeboat crank
"""

TITLE = 'Crucero "Reina del Imserso": plano por plantas (borrador del 2026-11-17)'
problems, warnings = P.check()
P.show(heights='--heights' in sys.argv)
print('PROBLEMS:' if problems else 'checks OK')
for p in problems:
    print('  ' + p)
if '--png' in sys.argv:
    P.draw(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Renders', 'Plano_Crucero.png'), TITLE, cols=1)
if '--check' not in sys.argv and not problems:
    P.write(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Maps', 'Crucero', 'Crucero_Layout.txt'), HEADER)
