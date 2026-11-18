# Builds Assets/Maps/Villajubilada/Villajubilada_Layout.txt (the "Fiestas de Villajubilada" map) from rectangles.
# Run:  python Tools/Maps/make_villajubilada_layout.py   then Tools > Island > Setup Villajubilada.
# (--check = print the map and the checks, write nothing.)
# Grid: 1 cell = 2 m, row 0 = north. '.' = fields outside the village (never reachable), '#' = houses.
#
# Two acts (user, 2026-11-16):
#   act 1, sunset: La Parada (start) + two small zones you pay to open (El Bar, Los Corrales). The way out (the
#          Porton, 'Q') is shut. In round 5 the bulls come out of the cattle lorry: the Porton opens by itself and you
#          run up the Calle del Encierro ahead of them. It shuts behind the herd for good: act 1 is gone.
#   act 2, night: the village. A ring of streets round the Plaza Mayor; every zone has its own fuse box.
import os
import sys
from collections import deque

W, H = 58, 60
M = [['.'] * W for _ in range(H)]
HT = [[0] * W for _ in range(H)]
RAMP = {}


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


def marks(ch, *cells):
    for c, r in cells:
        put(c, r, ch)


fill(2, 1, 56, 58, '#')                      # the village: everything is houses until a street is carved

# ================================================================ ACT 2: the village
# ---------------------------------------------------------------- p: Plaza Mayor (the wide one), centre
fill(23, 17, 34, 26, 'p')
level(27, 21, 30, 22, 1)                     # the bandstand (templete): karaoke on top
ramp([(25, 21), (26, 21)], '>'); ramp([(25, 22), (26, 22)], '>')
ramp([(32, 21), (31, 21)], '<'); ramp([(32, 22), (31, 22)], '<')
marks('o', (24, 18), (27, 18), (30, 18), (33, 18))          # arcade columns of the town hall (north side)
marks('o', (31, 25))                                         # fountain
marks('x', (24, 24), (25, 24), (32, 24), (24, 20), (33, 21))   # folding tables of the verbena
marks('K', (28, 21)); marks('P', (34, 17)); marks('B', (23, 26)); marks('E', (34, 24))
marks('F', (26, 17))                                         # the town hall balcony: light the chupinazo here
marks('s', (23, 17), (34, 26), (23, 22), (34, 22), (26, 26), (31, 17))

# ---------------------------------------------------------------- the ring: 2 cells wide round the plaza block
fill(19, 13, 21, 14, 'u'); fill(23, 13, 38, 14, 'e')        # north street
fill(19, 15, 20, 22, 'u'); fill(19, 24, 20, 30, 'c')        # west street
fill(37, 15, 38, 22, 'e'); fill(37, 24, 38, 30, 'c')        # east street
fill(19, 29, 38, 30, 'c')                                   # south street = Calle Mayor

# ---------------------------------------------------------------- c: Calle Mayor + Calle del Encierro (the way in)
fill(28, 31, 29, 33, 'c'); fill(28, 34, 35, 35, 'c'); fill(34, 36, 35, 39, 'c'); fill(28, 40, 35, 41, 'c'); fill(28, 42, 29, 45, 'c')
fill(20, 31, 25, 32, 'c')                    # the bar's little square
marks('c', (24, 28), (33, 28), (35, 31), (33, 31), (18, 26), (39, 26))   # portales on the ring: 1-cell doorways to dodge the bulls
marks('c', (27, 43), (31, 42), (33, 37), (36, 38), (31, 33))             # portales on the Calle del Encierro
marks('x', (22, 32), (24, 32))
marks('D', (20, 32)); marks('B', (25, 32)); marks('E', (20, 31)); marks('%', (36, 29))
marks('s', (19, 30), (38, 29), (21, 32), (26, 29), (36, 30), (19, 25), (38, 27), (31, 30), (28, 44), (35, 39), (30, 34))

# ---------------------------------------------------------------- u: Barrio de las Cuestas (the narrow one), west
fill(17, 16, 18, 16, 'u'); fill(17, 21, 18, 21, 'u')        # two ways in from the west street
fill(16, 10, 16, 25, 'u'); fill(11, 10, 11, 25, 'u'); fill(6, 10, 6, 25, 'u')       # three alleys north-south
fill(6, 10, 16, 10, 'u'); fill(6, 16, 16, 16, 'u'); fill(11, 21, 16, 21, 'u'); fill(6, 25, 16, 25, 'u')
fill(12, 13, 14, 13, 'u')                    # dead end (drink machine)
fill(7, 18, 8, 18, 'u'); fill(8, 18, 8, 22, 'u')            # dead end (record)
fill(4, 12, 5, 14, 'u')                      # the little square on top (mirador)
level(4, 9, 9, 26, 1)                        # the upper half of the barrio
ramp([(10, 10), (9, 10)], '<'); ramp([(10, 16), (9, 16)], '<'); ramp([(10, 25), (9, 25)], '<')
marks('I', (5, 14))                          # La Lijadora del Taller (+1 star), far from the Microondas
marks('D', (14, 13)); marks('J', (8, 22)); marks('L', (4, 12)); marks('B', (16, 25)); marks('U', (11, 19)); marks('%', (11, 11)); marks('E', (16, 10))
marks('s', (16, 18), (11, 23), (6, 20), (13, 16), (6, 11), (19, 17), (20, 13), (12, 25), (4, 14), (14, 21))

# ---------------------------------------------------------------- e: Calle de la Iglesia (north + east streets, atrium, penas' yard)
fill(24, 6, 33, 12, 'e')                     # the atrium
fill(26, 3, 31, 5, 'Z')                      # the church
level(24, 6, 33, 9, 1)
ramp([(25, 11), (25, 10)], '^'); ramp([(32, 11), (32, 10)], '^')
fill(24, 10, 24, 11, '#')                    # (feedback 1) the corner west of the ramp was a pocket only a jumping player reached
marks('o', (29, 11)); marks('x', (27, 12), (30, 12))
fill(36, 9, 40, 12, 'e')                     # the penas' yard
marks('e', (39, 15), (39, 21))               # portales
marks('E', (33, 6)); marks('D', (24, 12)); marks('J', (24, 6)); marks('B', (29, 7), (40, 9)); marks('%', (38, 15))
marks('s', (26, 6), (31, 6), (24, 9), (33, 11), (23, 13), (34, 14), (30, 14), (37, 9), (40, 12), (38, 17), (37, 20), (38, 13), (36, 11))

# ---------------------------------------------------------------- f: Recinto ferial, east
fill(42, 10, 55, 30, 'f')
fill(45, 13, 48, 16, 'o')                    # bumper cars
fill(51, 20, 53, 22, 'o')                    # big wheel
marks('o', (51, 12), (52, 12))               # churreria
marks('o', (44, 20), (45, 20), (47, 20), (48, 20), (44, 25), (45, 25), (46, 25), (49, 26), (50, 26))   # stalls
marks('o', (50, 16), (51, 16), (47, 28), (48, 28), (53, 26), (43, 17), (54, 20))   # more stalls: the fair read too open
marks('x', (53, 16), (54, 16), (46, 23), (52, 28))
marks('S', (44, 11)); marks('M', (55, 24)); marks('D', (43, 29)); marks('B', (55, 10)); marks('J', (54, 29)); marks('L', (55, 30)); marks('%', (43, 22)); marks('E', (55, 14))
marks('s', (42, 14), (55, 18), (47, 10), (50, 30), (49, 16), (52, 23), (42, 26), (55, 27), (46, 26))

# ---------------------------------------------------------------- l: Las Eras (boss + the tractor), north-east
fill(44, 3, 55, 8, 'l')
marks('o', (46, 4), (47, 6), (53, 7))        # straw bales
marks('b', (50, 5)); marks('X', (55, 4))
marks('s', (44, 5), (51, 3), (46, 8))   # the tractor stands along the east wall

# ---------------------------------------------------------------- the golden cabezudo: the host picks one of these
marks('O', (5, 13), (40, 11), (42, 30))

# ================================================================ ACT 1: where you start (lost after the first encierro)
# ---------------------------------------------------------------- a: La Parada (start)
fill(27, 47, 30, 48, 'a')                    # the funnel up to the Porton
fill(24, 49, 33, 55, 'a')
marks('Q', (28, 46), (29, 46))               # the Porton del encierro: shut until round 5, shut again behind the herd
marks('V', (28, 54), (29, 54), (28, 55), (29, 55))   # the cattle lorry (4 m long, tailgate to the north): the bulls come out of it
marks('o', (31, 50))                         # bus shelter
marks('x', (26, 51), (26, 52))
marks('@', (25, 50)); marks('B', (24, 54))   # a box spot the setup wants in the start zone (the box first shows up in the Calle Mayor)
marks('s', (24, 50), (33, 53), (27, 47), (30, 48), (25, 55), (32, 55), (33, 49))

# ---------------------------------------------------------------- r: El Bar (west of the bus stop)
fill(14, 49, 21, 55, 'r'); fill(22, 51, 22, 52, 'r')
marks('o', (15, 50), (16, 50), (17, 50))     # the bar counter
marks('x', (18, 53), (19, 53))
marks('D', (14, 55)); marks('E', (21, 49)); marks('&', (14, 49))
marks('s', (14, 52), (21, 55), (18, 49), (16, 51))

# ---------------------------------------------------------------- g: Los Corrales (east of the bus stop)
fill(36, 49, 43, 55, 'g'); fill(35, 51, 35, 52, 'g')
marks('o', (38, 51), (39, 51), (41, 53))     # straw bales
marks('x', (37, 54))
marks('G', (43, 52)); marks('&', (43, 49))
marks('s', (36, 49), (43, 55), (40, 49), (36, 55), (40, 51))

# ================================================================ barricades (the paid doors)
marks('1', (23, 51), (23, 52))               # La Parada -> El Bar
marks('2', (34, 51), (34, 52))               # La Parada -> Los Corrales
marks('3', (19, 23), (20, 23))               # Calle Mayor -> Cuestas (west street)
marks('4', (37, 23), (38, 23))               # Calle Mayor -> Iglesia (east street)
marks('5', (22, 13), (22, 14))               # Cuestas -> Iglesia (north street): closes the ring
marks('6', (28, 28), (29, 28)); fill(28, 27, 29, 27, 'p')   # Calle Mayor -> Plaza
marks('7', (28, 15), (29, 15)); fill(28, 16, 29, 16, 'p')   # Iglesia -> Plaza
marks('8', (39, 18), (39, 19)); fill(40, 18, 41, 19, 'f')   # Iglesia -> Ferial
marks('9', (48, 9), (49, 9))                 # Ferial -> Las Eras (mission)

# zone: (id, name, group, colour, flags). La Parada and Calle Mayor share a group: no door between them, the Porton.
ZONES = [('a', 'Parada', 'La Parada', 0, 'B8A58A', 'start'), ('r', 'Bar', 'El Bar', 1, 'B09A86', ''),
         ('g', 'Corrales', 'Los Corrales', 2, 'B49A5A', ''), ('c', 'CalleMayor', 'Calle Mayor', 0, 'A89C8C', 'firstbox'),
         ('u', 'Cuestas', 'Barrio de las Cuestas', 3, '9C8F80', ''), ('e', 'Iglesia', 'Calle de la Iglesia', 4, 'A8A294', ''),
         ('p', 'Plaza', 'Plaza Mayor', 5, 'C4B49A', ''), ('f', 'Ferial', 'Recinto ferial', 6, '8E8674', ''),
         ('l', 'Eras', 'Las Eras', 7, 'B49A5A', '')]
DOORS = [('1', 'Barricada del Bar', 750, 'a r', ''), ('2', 'Barricada de los Corrales', 750, 'a g', ''),
         ('3', 'Peña El Sintrom', 1000, 'c u', ''), ('4', 'Peña Los del Imserso', 1000, 'c e', ''),
         ('5', 'Peña La Dentadura', 1500, 'u e', ''), ('6', 'Arco de la Plaza', 1250, 'c p', ''),
         ('7', 'Arco del Ayuntamiento', 1250, 'e p', ''), ('8', 'Entrada del ferial', 1500, 'e f', ''),
         ('9', 'LAS ERAS - JEFE FINAL', 10000, 'f l', 'llave')]

# the encierro route (cell corners between the two lanes): the lorry, the Calle del Encierro, then the ring clockwise.
# RING_FROM = index of the first corner of the ring (the last corner closes it).
ROUTE = [(29, 54), (29, 41), (35, 41), (35, 35), (29, 35), (29, 30), (20, 30), (20, 14), (38, 14), (38, 30), (29, 30)]
RING_FROM = 5

# ---------------------------------------------------------------- checks
letters = ''.join(z[0] for z in ZONES)
SOLID = '#ZoV.'
problems = []


def zone_of(c, r):
    """Zone letter of a walkable cell (markers take the zone of a floor neighbour), None for solids / doors."""
    ch = M[r][c]
    if ch in letters: return ch
    if ch in SOLID or ch.isdigit(): return None
    seen, q = {(c, r)}, deque([(c, r)])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n in seen or not (0 <= n[0] < W and 0 <= n[1] < H): continue
            nch = M[n[1]][n[0]]
            if nch in letters: return nch
            if nch in SOLID or nch.isdigit(): continue
            seen.add(n); q.append(n)
    return None


Z = [[zone_of(c, r) for c in range(W)] for r in range(H)]
group = {z[0]: z[3] for z in ZONES}
for ch in letters:
    if ch in '#RZoxhAVWwH.!tq@sBDPKJbXLGUMEYTCNSFO%Q&': problems.append(f'zone letter {ch} is a legend character')
for r in range(H):
    for c in range(W):
        ch = M[r][c]
        if ch not in letters and ch not in SOLID and not ch.isdigit() and Z[r][c] is None: problems.append(f'marker {ch} at {(c, r)} touches no floor')
        if ch == 's' and not any(M[r + dr][c + dc] in SOLID for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1))): problems.append(f'spawn {(c, r)} has nothing solid beside it')
        if ch == 'x' and sum(Z[r + dr][c + dc] is not None for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 3: problems.append(f'low cover {(c, r)} in a 1-cell lane')
        if ch in 'EGUM' and not any(M[r + dr][c + dc] in '#Z' for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1))): problems.append(f'{ch} at {(c, r)} needs a wall beside it')
for z in letters:
    cells = [(c, r) for r in range(H) for c in range(W) if Z[r][c] == z]
    seen, q = {cells[0]}, deque([cells[0]])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n in seen or Z[n[1]][n[0]] != z: continue
            seen.add(n); q.append(n)
    if len(seen) != len(cells): problems.append(f'zone {z} is in {len(cells) - len(seen)} cells of separate pieces')
    print(f'zone {z}: {len(cells)} cells = {len(cells) * 4} m2')
for r in range(H - 1):
    for c in range(W - 1):
        for n in ((c + 1, r), (c, r + 1)):
            a, b = Z[r][c], Z[n[1]][n[0]]
            if a and b and group[a] != group[b]: problems.append(f'zones {a} and {b} touch without a door at {(c, r)}')
            if a and b and a != b and group[a] == group[b] and 'Q' not in (M[r][c], M[n[1]][n[0]]): problems.append(f'zones {a} and {b} touch away from the Porton at {(c, r)}')
for d, name, cost, zs, flag in DOORS:
    touch = set()
    for r in range(H):
        for c in range(W):
            if M[r][c] != d: continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if Z[r + dy][c + dx]: touch.add(Z[r + dy][c + dx])
    if touch != set(zs.split()): problems.append(f'door {d} touches {sorted(touch)}, wants {zs}')
# the route: both lanes beside every corner-to-corner segment must be street (floor, marker or door), never solid
for (c0, r0), (c1, r1) in zip(ROUTE, ROUTE[1:]):
    n = max(abs(c1 - c0), abs(r1 - r0))
    for i in range(n + 1):
        c, r = c0 + (c1 - c0) * i // n, r0 + (r1 - r0) * i // n
        for cc, rr in ((c, r), (c - 1, r)) if c0 == c1 else ((c, r), (c, r - 1)):
            if M[rr][cc] in '#Zo.': problems.append(f'route crosses a solid cell {(cc, rr)}')
seg = lambda pts: sum(abs(c1 - c0) + abs(r1 - r0) for (c0, r0), (c1, r1) in zip(pts, pts[1:])) * 2
print(f'encierro: lorry -> ring {seg(ROUTE[:RING_FROM + 1])} m, ring {seg(ROUTE[RING_FROM:])} m')
print('PROBLEMS:' if problems else 'checks OK')
for p in sorted(set(problems)): print('  ' + p)

HEADER = """# Fiestas de Villajubilada - fourth map: an inland village during its fiestas (Tools > Island > Setup Villajubilada).
# GENERATED by Tools/Maps/make_villajubilada_layout.py: edit the script (rectangles), not this file.
# 1 character = 2 x 2 m. Row 0 = north. Format: Docs/FASE3_FORMATO.md. Theme "pueblo" (IslaZombieSetup.Pueblo.cs):
#   .  fields outside the village (never reachable)   #  houses (facades)   Z  the church
#   o  tall prop (columns, stalls, attractions, straw bales)   x  low cover (tables): always jumpable, never in a 1-cell lane
#   V  the cattle lorry (the encierro starts here)   Q  the Porton del encierro   %  traca post (buyable trap)
#   1-9 barricades of the penas = paid doors
#   markers as in Isla del Faro: @ s B D P K J b X L G U M I   E fuse box of its zone (lights + machines of that zone)
#   S firecracker stall (sells the lighter if you left without it)   & where the lighter may lie (the second zone you open)
#   F the town hall balcony (light the chupinazo)   O the golden cabezudo (the host picks one)
# route = the encierro, in cell corners; ring = index of the corner where the ring starts

[route]
""" + ' '.join(f'{c},{r}' for c, r in ROUTE) + f' | {RING_FROM}' + """

[zones]
""" + '\n'.join(f'{z} | {i} | {n} | {g} | {col}' + (f' | {fl}' if fl else '') for z, i, n, g, col, fl in ZONES) + """

[doors]
""" + '\n'.join(f'{d} | {n} | {cost} | {zs}' + (f' | {fl}' if fl else '') for d, n, cost, zs, fl in DOORS) + '\n'

heights = []
for r in range(H):
    row = ''
    for c in range(W):
        if M[r][c] == '.': row += '.'
        elif (c, r) in RAMP: row += RAMP[(c, r)]
        else: row += str(HT[r][c])
    heights.append(row)

print('\n'.join(''.join(row) for row in M))
if '--check' not in sys.argv and not problems:
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Maps', 'Villajubilada', 'Villajubilada_Layout.txt')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write(HEADER + '\n[heights]\n' + '\n'.join(heights) + '\n\n[map]\n' + '\n'.join(''.join(row) for row in M) + '\n')
    print('written', out)
