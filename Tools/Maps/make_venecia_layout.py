# Builds Assets/Maps/Venecia/Venecia_Layout.txt (the "La Serenisima" map) from rectangles, so lanes and blocks stay
# easy to reason about. Run:  python Tools/Maps/make_venecia_layout.py   then Tools > Island > Setup La Serenisima.
# Grid: 1 cell = 2 m, row 0 = north. '.' = canal / lagoon (you can fall in), '#' = building, 'Z' = monument stone.
import os

W, H = 70, 48
M = [['.'] * W for _ in range(H)]
HT = [[0] * W for _ in range(H)]      # level per cell
RAMP = {}                             # (c, r) -> arrow


def fill(c0, r0, c1, r1, ch):
    for r in range(r0, r1 + 1):
        for c in range(c0, c1 + 1):
            M[r][c] = ch


def put(c, r, ch):
    M[r][c] = ch


def level(c0, r0, c1, r1, lv):
    for r in range(r0, r1 + 1):
        for c in range(c0, c1 + 1):
            HT[r][c] = lv


def ramp(cells, arrow):
    for c, r in cells:
        RAMP[(c, r)] = arrow


def blocks(ch, *rects):
    for c0, r0, c1, r1 in rects:
        fill(c0, r0, c1, r1, ch)


def marks(ch, *cells):
    for c, r in cells:
        put(c, r, ch)


# ---------------------------------------------------------------- a: Ferrovia (start), SW
fill(2, 33, 26, 45, 'a')
blocks('#', (2, 33, 2, 45), (2, 45, 26, 45),
       (3, 42, 10, 44),                      # station
       (4, 35, 7, 37), (9, 35, 13, 36), (15, 35, 19, 36),
       (22, 35, 24, 36), (22, 41, 24, 43), (11, 44, 21, 44))
blocks('o', (15, 39, 16, 41))                # the well block: a ring to run around
marks('x', (12, 38), (19, 38), (12, 42), (19, 42))
level(3, 39, 8, 41, 1)                       # station terrace
ramp([(9, 40), (10, 40)], '<')
marks('@', (13, 40)); marks('K', (4, 40)); marks('B', (16, 43))
marks('s', (3, 34), (11, 34), (20, 33), (26, 36), (26, 42), (3, 38), (21, 36), (10, 41), (17, 43), (25, 44), (7, 41))

# ---------------------------------------------------------------- r: Mercato, W centre
fill(2, 17, 20, 30, 'r')
blocks('#', (2, 17, 2, 30),
       (3, 18, 7, 19), (9, 18, 11, 19), (15, 18, 18, 19),
       (3, 20, 4, 27),
       (3, 28, 7, 29), (11, 28, 14, 29), (16, 28, 18, 29),
       (17, 21, 18, 22), (17, 25, 18, 26))
blocks('o', (10, 23, 13, 24))                # central stall block
marks('o', (10, 21), (12, 21), (14, 21), (15, 26), (11, 26))
level(5, 20, 7, 27, 1)                       # loggia terrace
ramp([(8, 20), (9, 20)], '<')
ramp([(8, 27), (9, 27)], '<')
marks('G', (5, 24)); marks('D', (13, 27)); marks('B', (16, 23))
marks('s', (8, 18), (20, 20), (20, 27), (5, 17), (15, 30), (5, 21), (5, 26), (16, 22), (19, 25))

# ---------------------------------------------------------------- g: Cannaregio (maze), NW
fill(2, 2, 26, 14, 'g')
blocks('#', (2, 2, 26, 2), (2, 2, 2, 14),
       (4, 4, 7, 5), (9, 4, 11, 5), (13, 4, 16, 5), (18, 4, 20, 5), (22, 4, 24, 5),
       (4, 7, 6, 9), (8, 7, 10, 8), (12, 7, 13, 9),
       (22, 8, 24, 9),
       (4, 11, 6, 13), (8, 10, 10, 13), (12, 11, 13, 13), (15, 12, 19, 13), (21, 11, 24, 13),
       # the narrow barrio: no campo, only 1-cell alleys that turn and dead-end (the Piazza is the wide one)
       (15, 7, 16, 8), (18, 7, 20, 8), (15, 10, 17, 11), (19, 10, 20, 10))
level(3, 3, 25, 3, 1)                        # fondamenta alta
ramp([(8, 5), (8, 4)], '^')
ramp([(21, 5), (21, 4)], '^')
marks('U', (14, 7)); marks('D', (18, 11)); marks('L', (25, 4)); marks('L', (3, 4)); marks('J', (12, 4)); marks('B', (17, 5))
marks('s', (5, 3), (19, 3), (3, 8), (26, 10), (10, 14), (20, 14), (7, 8), (11, 9), (14, 12), (3, 12), (17, 7), (21, 10))

# ---------------------------------------------------------------- u: Arsenale, NE
fill(33, 2, 58, 14, 'u')
blocks('#', (33, 2, 58, 2), (58, 2, 58, 14),
       (40, 4, 45, 5), (47, 4, 52, 5), (55, 4, 57, 5),
       (52, 9, 56, 12), (38, 12, 40, 13), (34, 11, 35, 13))
blocks('Z', (35, 4, 36, 5), (35, 8, 36, 9))  # gate towers
fill(43, 9, 49, 11, '.')                     # darsena: water inside the yard
marks('o', (38, 9), (39, 9), (51, 7), (41, 11), (47, 13), (48, 13))
level(34, 3, 57, 3, 1)                       # rampart
ramp([(38, 5), (38, 4)], '^')
ramp([(54, 5), (54, 4)], '^')
marks('J', (46, 4)); marks('B', (57, 13))
marks('s', (33, 10), (40, 3), (50, 3), (57, 7), (45, 14), (51, 10), (42, 10), (37, 13), (53, 4), (57, 9))

# ---------------------------------------------------------------- e: Piazza del Dux, E centre
fill(39, 17, 58, 30, 'e')
blocks('#', (58, 17, 58, 22), (58, 25, 58, 30))   # the wide one: a single open square, 38 x 28 m, only the palace wall
blocks('Z', (46, 22, 48, 24))                # campanile
marks('o', (41, 21), (41, 26))               # the two columns of the molo
marks('o', (43, 19), (46, 19), (50, 19), (53, 19), (43, 28), (50, 28), (53, 28))   # colonnades along the north and south sides
marks('x', (43, 26), (51, 22), (51, 25), (44, 21), (42, 18), (50, 18), (43, 29), (50, 29))
level(55, 18, 57, 22, 1); level(55, 25, 57, 29, 1)   # palace loggias, the gate to the vaporetto between them
ramp([(53, 20), (54, 20)], '>')
ramp([(53, 27), (54, 27)], '>')
marks('M', (57, 28)); marks('D', (44, 20)); marks('B', (52, 27))
marks('s', (39, 20), (39, 27), (43, 17), (51, 30), (47, 21), (57, 21), (39, 29), (54, 17), (57, 26), (47, 25), (45, 30), (49, 17))

# ---------------------------------------------------------------- n: Carnevale, SE
fill(33, 33, 58, 45, 'n')
blocks('#', (33, 45, 58, 45), (58, 33, 58, 45),
       (35, 35, 38, 35), (40, 35, 44, 35), (48, 35, 51, 35), (53, 35, 57, 35),
       (35, 43, 41, 44), (43, 43, 49, 44), (51, 43, 57, 44),
       (54, 37, 57, 41), (35, 41, 37, 42))
marks('o', (40, 37), (51, 37), (40, 41), (51, 41))
marks('x', (38, 37), (46, 36), (46, 42))
level(43, 38, 48, 40, 1)                     # the stage
ramp([(41, 39), (42, 39)], '>')
ramp([(50, 39), (49, 39)], '<')
marks('L', (42, 44)); marks('B', (50, 44)); marks('D', (53, 42))
marks('s', (33, 35), (33, 42), (40, 33), (55, 33), (38, 34), (45, 42), (53, 39), (57, 34), (38, 41), (51, 34))

# ---------------------------------------------------------------- i: Cimitero Monumentale (island in the basin)
fill(23, 17, 36, 30, 'i')
blocks('Z', (24, 18, 25, 19), (34, 18, 35, 19), (24, 28, 25, 29), (34, 28, 35, 29))   # corner mausoleums
marks('o', (24, 21), (24, 26), (35, 21), (35, 26), (27, 18), (32, 18), (27, 29), (32, 29))
marks('x', (24, 23), (35, 24), (30, 17))   # no low cover in the 1-cell south aisle (row 30): the user could not jump it
level(26, 20, 33, 27, 1)                     # the terrace of the monuments
level(29, 23, 30, 24, 2)                     # tower top
ramp([(29, 18), (29, 19)], 'v')
ramp([(30, 29), (30, 28)], '^')
ramp([(27, 23), (28, 23)], '>')
marks('o', (27, 21), (32, 21), (27, 26), (32, 26))
marks('A', (26, 20), (33, 20), (26, 27), (33, 27))
marks('h', (26, 24), (33, 23))
# more graves you can't walk through: a wall of tombs down the middle of the west and east rings (two 1-cell aisles
# each), clusters on the terrace and beside the ramps; (29, 24) on the top = the spiral tower
marks('o', (24, 22), (24, 24), (24, 25), (35, 22), (35, 23), (35, 25))
marks('o', (28, 21), (31, 21), (28, 25), (31, 26))
marks('o', (28, 18), (31, 18), (28, 29))
marks('o', (29, 24))
marks('P', (30, 24)); marks('J', (32, 24)); marks('B', (23, 30))
marks('s', (23, 20), (36, 26), (26, 18), (33, 29), (26, 25), (33, 22), (36, 20), (23, 27))

# ---------------------------------------------------------------- d: Vaporetto pier (boss + extraction)
fill(60, 22, 67, 25, 'd')
marks('#', (59, 22), (59, 25))
fill(58, 23, 58, 24, 'e')
marks('o', (62, 25), (65, 22))
marks('b', (63, 23)); marks('X', (67, 24))
marks('s', (61, 22), (64, 25), (66, 22))

# ---------------------------------------------------------------- vaporetto task list (BoatMission.HasTasks)
# S = the stall that sells the shovel (Mercato), F = Giuseppe's grave (Cimitero terrace), O = the golden gondolier
# (dead end of the Carnevale). The bell is on top of the campanile (Piazza); discs J, karaoke K and key L as usual.
marks('S', (12, 22)); marks('F', (33, 25)); marks('O', (57, 42), (57, 3), (57, 36))   # golden gondolier: the host picks one of these dead ends (B2)

# ---------------------------------------------------------------- gondola stops (N): a quay cell beside the water
marks('N', (26, 44), (33, 44), (39, 24), (33, 13), (26, 13), (20, 29), (31, 30))

# ---------------------------------------------------------------- bridges (the paid doors)
# small flat bridges: one door cell + one floor cell of the far zone
put(9, 32, '1'); put(9, 31, 'r')             # Ferrovia -> Mercato
put(13, 16, '2'); put(13, 15, 'g')           # Mercato -> Cannaregio
put(21, 23, '4'); put(22, 23, 'i')           # Mercato -> Cimitero
put(37, 23, '5'); put(38, 23, 'e')           # Cimitero -> Piazza
put(46, 31, '6'); put(46, 32, 'n')           # Piazza -> Carnevale
put(48, 16, '8'); put(48, 15, 'u')           # Piazza -> Arsenale
marks('9', (59, 23), (59, 24))               # Piazza -> vaporetto
# big arched bridges over the Canal Grande (2 cells wide): the door stands between two bridge-head pillars on the
# west bank (a door never touches a ramp), then a flat cell, the arch (ramps up to level 1 and down) and the east bank
for rows, digit, far in (((38, 39), '3', 'n'), ((6, 7), '7', 'u')):
    put(26, rows[0] - 1, '#'); put(26, rows[1] + 1, '#')
    for r in rows:
        put(26, r, digit)
        for c in range(27, 33): put(c, r, far)
        ramp([(28, r), (29, r)], '>')
        HT[r][30] = 1
        ramp([(32, r), (31, r)], '<')

HEADER = """# La Serenisima - third map: Venice + the Monumental cemetery (Tools > Island > Setup La Serenisima).
# GENERATED by Tools/Maps/make_venecia_layout.py: edit the script (rectangles), not this file.
# 1 character = 2 x 2 m. Row 0 = north. Format: Docs/FASE3_FORMATO.md. Theme "venecia" (IslaZombieSetup.Venecia.cs):
#   .  canal / lagoon: no invisible walls, a player who falls in climbs back where he fell (Zones/CanalRescue)
#   #  building (facades)   Z  monument stone (gate towers, campanile, mausoleums)
#   o  tall prop (well, stalls, tombs, gondolas on trestles, columns)   x  low cover (tables, low tombs)
#   h  mausoleum (old men come out)   A  cypress
#   1-9 bridges = paid doors; level-0 floor floods in the acqua alta rounds
#   markers as in Isla del Faro: @ s B D P K J b X L G U M  N gondola stop  S shovel stall  F Giuseppe's grave  O golden gondolier (the bell is on the campanile)

[zones]
a | Ferrovia   | Ferrovia            | 0 | B9AFA0 | start
r | Mercato    | Mercato di Rialto   | 1 | C9A98A | firstbox
g | Cannaregio | Cannaregio          | 2 | A89F94
n | Carnevale  | Campo del Carnevale | 3 | C4A0A0 | firstbox
i | Cimitero   | Cimitero Monumentale| 4 | 9AA08C
e | Piazza     | Piazza del Dux      | 5 | D2C8B4
u | Arsenale   | Arsenale            | 6 | A7998A
d | Vaporetto  | Vaporetto           | 7 | 8C6642

[doors]
1 | Ponte dei Sospiri (de cansancio) | 750   | a r
2 | Ponte del Bingo                  | 1000  | r g
3 | Ponte della Ferrovia             | 1500  | a n
4 | Ponte del Cimitero               | 1250  | r i
5 | Ponte delle Vedove               | 1500  | i e
6 | Ponte del Carnevale              | 1250  | e n
7 | Ponte dell'Arsenale              | 2000  | g u
8 | Ponte dei Pensionati             | 1500  | e u
9 | VAPORETTO - JEFE FINAL           | 10000 | e d | llave
"""

bad = []
for r in range(H):
    for c in range(W):
        if M[r][c] == 's' and not any(M[r + dr][c + dc] in '#Zoh.' for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            bad.append((c, r))
if bad: print('SPAWNS WITH NOTHING SOLID BESIDE THEM:', bad)

heights = []
for r in range(H):
    row = ''
    for c in range(W):
        if M[r][c] == '.': row += '.'
        elif (c, r) in RAMP: row += RAMP[(c, r)]
        else: row += str(HT[r][c])
    heights.append(row)

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Maps', 'Venecia', 'Venecia_Layout.txt')
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, 'w', encoding='utf-8', newline='\n') as f:
    f.write(HEADER + '\n[heights]\n' + '\n'.join(heights) + '\n\n[map]\n' + '\n'.join(''.join(row) for row in M) + '\n')
print('\n'.join(''.join(row) for row in M))
