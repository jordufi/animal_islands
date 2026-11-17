# Builds Assets/Maps/HotelJubiles/HotelJubiles_Layout.txt (the "Hotel Jubiles" map) from rectangles, floor by floor.
# Run:  python Tools/Maps/make_hotel_layout.py --check   (prints every floor and the checks, writes nothing)
#       python Tools/Maps/make_hotel_layout.py           (writes the layout when the checks pass; --heights also prints them)
#       --png draws every floor to Renders/Plano_Hotel.png (with --check: draws, writes no layout)
# Format with real floors: Docs/PLANTAS_FORMATO.md. Design: Docs/HOTEL_JUBILES_DISENO.md. Grid: 1 cell = 2 m, row 0 = north.
#
# A 70s Benidorm hotel on a stormy night (user, 2026-11-17). Five floors: Sotano (-4 m), Planta baja (0), 1a planta (4 m),
# 2a planta (8 m), Azotea (12 m). Starts in a blackout in Recepcion; the boilers (power) are two doors away: Bufe + kitchen
# stair, or pool terrace + garage ramp. The tower (cols 14-29, rows 12-28) stands over the lobby; its floors are a ring
# corridor round an inner block crossed by a middle corridor (two loops) with rooms off them. Street to the south
# (revolving door), pool terrace and hedge maze to the north and east.
# ORDER MATTERS: every floor gets its base fill first; stairs then carve voids in the floor above.
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from floors import Plan  # noqa: E402

P = Plan(46, 36)
S = P.floor('S', 'Sotano', -2)
B = P.floor('0', 'Planta baja', 0)
P1 = P.floor('1', 'Primera planta', 2)
P2 = P.floor('2', 'Segunda planta', 4)
R = P.floor('3', 'Azotea', 6)

# zone: (letter, id, name, group, colour, flags). techo = interior (roofed where nothing is built above).
P.zones = [
    ('a', 'Recepcion', 'Recepción', 0, 'B79A6E', 'start techo'),
    ('f', 'Bufe', 'Bufé libre', 1, 'C2A27A', 'firstbox techo'),
    ('c', 'Sotano', 'Sótano', 2, '6E6A64', 'techo'),
    ('p', 'Piscina', 'Piscina y jardines', 3, '7FB3A8', 'firstbox'),
    ('d', 'SalonDorado', 'Salón Dorado', 4, 'C9A44C', 'techo'),
    ('m', 'Planta1', 'Primera planta', 5, 'A0523C', 'techo'),
    ('n', 'Planta2', 'Segunda planta', 6, '8E4A36', 'techo'),
    ('r', 'Azotea', 'Azotea', 7, '8C8C8C', ''),
]
P.doors = [
    ('1', 'Puerta del comedor', 750, 'a f', ''),
    ('2', 'Escalera de las cocinas', 1000, 'f c', ''),
    ('3', 'Puertas de la piscina', 1000, 'a p', ''),
    ('4', 'Rampa del garaje', 1250, 'c p', ''),
    ('5', 'Salón Dorado (terraza)', 1250, 'p d', ''),
    ('6', 'Salón Dorado (bufé)', 1250, 'f d', ''),
    ('7', 'Escalera principal', 1250, 'a m', ''),
    ('8', 'Escalera de la 2ª planta', 1500, 'm n', ''),
    ('9', 'AZOTEA - JEFE FINAL', 10000, 'n r', 'llave'),
]

# =============================================================== base fills (before any stair)
S.fill(2, 16, 36, 35, '#')
B.fill(2, 1, 42, 1, '#'); B.fill(2, 1, 2, 18, '#'); B.fill(42, 1, 42, 15, '#'); B.fill(34, 15, 42, 15, '#')
B.fill(3, 2, 28, 7, 'p')                    # pool terrace (north)
B.fill(29, 2, 33, 20, 'p')                  # east terrace
B.fill(34, 2, 41, 14, 'p')                  # the hedge maze (north-east)
B.fill(34, 16, 34, 21, '#'); B.fill(28, 21, 34, 29, '#')
B.fill(14, 8, 28, 19, '#')                  # the tower block on the planta baja: back of house
B.fill(3, 8, 15, 18, '#')                   # Salon Dorado walls
B.fill(3, 18, 15, 29, '#')                  # Bufe walls
B.fill(15, 20, 28, 29, '#')                 # Recepcion walls


def tower(F, z):
    F.fill(14, 12, 29, 28, '#')
    F.fill(16, 14, 27, 14, z); F.fill(16, 26, 27, 26, z)          # ring corridor
    F.fill(16, 14, 16, 26, z); F.fill(27, 14, 27, 26, z)
    F.fill(17, 20, 26, 20, z)                                      # middle corridor (lift landings)
    F.fill(17, 21, 19, 22, z)                                      # inner rooms off the middle corridor
    F.put(17, 24, z)                                               # the linen room (laundry chute)
    for c0, r0, c1, r1 in ((19, 12, 19, 13), (25, 12, 25, 13), (18, 27, 18, 28), (24, 27, 24, 28),
                           (14, 18, 15, 18), (14, 23, 15, 23), (28, 17, 29, 17), (28, 23, 29, 23)):
        F.fill(c0, r0, c1, r1, z)                                  # open rooms: dead ends off the ring


tower(P1, 'm')
tower(P2, 'n')
R.fill(14, 12, 29, 28, 'r')

# =============================================================== SOTANO (calderas, lavanderia, garaje): c
S.fill(10, 20, 27, 20, 'c')                 # main corridor (lift landings at 22 and 24, service lift at 15)
S.fill(10, 21, 10, 30, 'c')                 # west corridor
S.fill(4, 27, 9, 32, 'c')                   # the boiler room
S.fill(11, 23, 17, 27, 'c')                 # the laundry
S.fill(10, 25, 11, 25, 'c')
S.fill(20, 22, 28, 29, 'c')                 # the garage
S.fill(20, 30, 33, 31, 'c')
S.put(26, 21, 'c')
S.fill(18, 26, 19, 26, 'c')                 # laundry <-> garage
S.marks('o', (12, 23), (14, 23), (16, 23), (12, 27), (16, 27))                    # washing machines
S.marks('o', (5, 29), (5, 31), (8, 31))                                           # boilers
S.marks('o', (22, 23), (23, 23), (22, 26), (23, 26), (27, 25), (27, 26), (22, 30))   # cars
S.marks('o', (25, 25), (25, 28))                                                   # columns
S.marks('E', (9, 32))                       # the power: cuadro general by the boilers
S.marks('T', (17, 25))                      # the hotel bathrobe (albornoz: x2 score if you leave with it)
S.marks('M', (28, 23))                      # La Thermomix on the garage wall
S.marks('B', (11, 24), (28, 29))
S.marks('%', (10, 23))                      # mopped floor (suelo mojado)
S.marks('s', (4, 32), (11, 27), (17, 23), (20, 22), (20, 31), (33, 31), (27, 20), (10, 21), (9, 27))
# the garage ramp north up to the east terrace: 2 levels in two runs of 3; door 4 on the planta baja
for c in (31, 32):
    for r in range(23, 30):
        S.put(c, r, 'c')
S.ramp([(31, 29), (31, 28), (31, 27), (32, 29), (32, 28), (32, 27)], '^')
S.level(31, 26, 32, 26, 1)
S.ramp([(31, 25), (31, 24), (31, 23), (32, 25), (32, 24), (32, 23)], '^')
S.fill(30, 22, 30, 29, '#'); S.fill(33, 22, 33, 29, '#'); S.fill(31, 22, 32, 22, '#')
B.fill(31, 23, 32, 29, '~'); B.fill(31, 22, 32, 22, 'c'); B.marks('4', (31, 21), (32, 21))

# =============================================================== PLANTA BAJA
# ---- p: pool terrace, east terrace, hedge maze (the wide zone)
B.fill(14, 3, 23, 5, 'w')                   # the pool
B.marks('x', (5, 3), (7, 3), (9, 3), (5, 6), (26, 3), (26, 6), (30, 5))   # sun loungers
B.marks('o', (11, 4), (25, 4), (12, 6))     # parasols
B.marks('o', (31, 9), (30, 13), (32, 17))   # palm trees
for c0, r0, c1, r1 in ((35, 3, 35, 7), (35, 9, 35, 13), (37, 2, 37, 5), (37, 7, 39, 7), (37, 9, 37, 13),
                       (39, 9, 39, 11), (39, 3, 40, 3), (40, 5, 40, 7), (41, 9, 41, 13), (38, 13, 39, 13)):
    B.fill(c0, r0, c1, r1, '#')              # hedges of the maze
B.marks('D', (28, 2))                       # the pool bar
B.marks('B', (3, 2), (41, 2))
B.marks('s', (3, 7), (18, 2), (33, 2), (41, 14), (15, 6), (21, 6), (33, 20), (36, 14), (38, 6))
# ---- a: Recepcion (start)
B.fill(16, 20, 27, 28, 'a')
B.marks('o', (19, 23), (24, 23), (19, 26), (24, 26))   # columns
B.marks('x', (20, 25), (21, 25))                       # the reception desk
B.marks('x', (17, 27), (26, 27))                       # sofas
B.marks('C', (27, 22))                                  # the reception phone (call the helicopter)
B.marks('@', (21, 27)); B.marks('B', (16, 28))
B.marks('s', (16, 21), (27, 21), (27, 28), (23, 28))
B.marks('3', (28, 20))                                  # -> east terrace
# the main stair up to the 1a planta, paid on its landing (door 7)
B.marks('7', (18, 20)); B.marks('#', (17, 20), (19, 20))
B.put(18, 19, 'm')
P.stair(B, (18, 19), (0, -1), (1, 0), 'm')             # arrives on the 1a planta at (20, 19)
# ---- f: Bufe libre (west) with the kitchen
B.fill(4, 19, 14, 28, 'f')
B.marks('o', (10, 21), (12, 21), (10, 24), (12, 24), (10, 27), (12, 27))   # buffet tables
B.marks('o', (5, 20), (6, 20), (7, 20))                                      # kitchen counters
B.marks('S', (4, 19))                       # the coffee machine (the pilot's coffee)
B.marks('D', (14, 19)); B.marks('G', (9, 28)); B.marks('B', (14, 28))
B.marks('s', (4, 22), (14, 21), (11, 28))
B.marks('1', (15, 23), (15, 24))
# the kitchen stair from the basement (after the bufe fill: it carves the bufe); door 2 opens its top to the bufe
S.put(5, 27, 'c')
P.stair(S, (5, 27), (0, -1), (1, 0), 'c')
B.marks('2', (8, 27)); B.fill(4, 27, 6, 28, '#'); B.put(7, 28, '#')
# ---- d: Salon Dorado (bingo night), north-west
B.fill(4, 9, 14, 17, 'd')
B.marks('x', (5, 10), (7, 10), (11, 10), (13, 10), (5, 16), (13, 16))   # tables round the dance floor
B.marks('o', (4, 13), (14, 11))                                          # the animador's bandstand, a column
B.marks('D', (9, 17)); B.marks('B', (14, 9))
B.marks('s', (4, 9), (14, 17), (4, 17))
B.marks('5', (8, 8), (9, 8)); B.marks('6', (9, 18), (10, 18))
# ---- the staff passage behind reception: only the pulsera de puertas opens its service door ($)
B.fill(16, 13, 16, 19, 'a'); B.put(15, 13, '$')

# =============================================================== 1a and 2a planta (the tower)
# PB -> 1a: the stair above arrives at (20, 19). 1a -> 2a: door 8, then the stair south of the middle corridor
P1.put(21, 19, '#')
P1.marks('8', (22, 21)); P1.put(22, 22, 'n')
P.stair(P1, (22, 22), (1, 0), (0, 1), 'n')             # arrives on the 2a planta at (22, 24)
P2.put(22, 25, 'n')
# 2a -> azotea: the stair over the first one; door 9 at its top, on the roof
P2.put(18, 19, 'n')
P.stair(P2, (18, 19), (0, -1), (1, 0), 'n')            # arrives on the azotea at (20, 19)
P2.marks('#', (21, 19), (17, 19), (19, 19))
# ---- 1a planta: the gym cuts through the inner block (middle corridor <-> south ring): one more loop
P1.fill(17, 21, 20, 25, 'm')
P1.marks('o', (18, 23), (19, 23))           # treadmills
P1.marks('O', (19, 12), (14, 23))           # two of the three rooms where the pulsera de puertas may hide
P1.marks('D', (29, 17)); P1.marks('U', (16, 17)); P1.marks('B', (25, 12), (18, 22))
P1.marks('%', (27, 21))
P1.marks('s', (16, 15), (21, 14), (27, 15), (16, 25), (21, 26), (27, 25), (24, 28), (26, 20), (20, 21))
# ---- 2a planta: an outdoor balcony along the east facade, reached through two rooms: a loop in the storm, over the pool
P2.fill(30, 14, 30, 26, 'n')
P2.marks('O', (14, 23))                     # the third room of the pulsera
P2.marks('P', (14, 18))                     # the Microondas, in the caretaker's room (with the typewriter)
P2.marks('F', (24, 28))                     # the pilot asleep in his room
P2.marks('E', (16, 19))                     # the helipad lights switch, by the roof stair
P2.marks('D', (19, 13)); P2.marks('B', (29, 17), (19, 22))
P2.marks('%', (16, 23))
P2.marks('s', (16, 15), (23, 14), (27, 16), (16, 25), (20, 26), (27, 24), (25, 12), (18, 28), (19, 21), (30, 15), (30, 25))

# =============================================================== AZOTEA (boss + helicopter): r
R.marks('#', (19, 19), (21, 19))
R.marks('9', (20, 20))
R.fill(23, 23, 28, 27, '#')                 # the penthouse suite (the boss breaks its door)
R.marks('o', (15, 13), (16, 13), (28, 13), (15, 27), (22, 14), (24, 14), (18, 22), (19, 22), (20, 25), (26, 20), (27, 20))   # tanks, lift rooms, air conditioning
R.marks('x', (26, 17), (17, 24))
R.marks('X', (27, 15)); R.marks('b', (25, 22))
R.marks('s', (14, 15), (29, 20), (14, 26), (22, 28), (18, 12), (22, 23))

# =============================================================== lifts, chutes, points
P.lift('A', 'Ascensor A', 22, 19, ['S', '0', '1', '2'])
P.lift('B', 'Ascensor B', 24, 19, ['S', '0', '1', '2'])
for F in (S, B, P1, P2):
    for c, r in ((23, 19), (25, 19), (21, 19), (22, 18), (24, 18)):
        if F.M[r][c] not in '=~':
            F.put(c, r, '#')
P.lift('M', 'Montacargas (pulsera de puertas)', 15, 21, ['S', '2'])
P.link('1', 'Tolva de la lavandería (1a)', ('1', 17, 24), ('S', 13, 27), 'tolva')
P.link('2', 'Tolva de la lavandería (2a)', ('2', 17, 24), ('S', 15, 27), 'tolva')
for name, fid, c, r in (
        ('puerta_giratoria', '0', 21, 29), ('arana', '0', 21, 24), ('escenario_animador', '0', 4, 13),
        ('barman_fantasma', '0', 9, 17), ('foto_salon', '0', 9, 9), ('maquina_escribir', '2', 15, 18),
        ('ognib', '1', 28, 23), ('habitacion_237', '2', 18, 27), ('gemelas', '2', 27, 14),
        ('puerta_suite', '3', 25, 23), ('helipuerto', '3', 27, 15), ('neon', '3', 21, 28),
        ('laberinto_centro', '0', 41, 21)):
    P.point(name, fid, c, r)

HEADER = """# Hotel Jubiles - a 70s Benidorm hotel on a stormy night (Tools > Island > Setup Hotel Jubiles).
# GENERATED by Tools/Maps/make_hotel_layout.py: edit the script, not this file. Format: Docs/PLANTAS_FORMATO.md.
# 1 character = 2 x 2 m, row 0 = north. Theme "hotel":
#   .  outside   #  walls / rooms / hedges (in the maze)   ~  void (stairwells)   =  lift cab   $  service door (pulsera)
#   o  tall prop (columns, tables, cars, washing machines, palm trees)   x  low cover (sofas, loungers, tables)   w  pool
#   1-9 paid doors (stair landings and hotel doors); 9 also needs the pulsera de puertas in the opener's pocket
#   markers: @ start, s spawn, B box (Objetos perdidos), D drink (minibar), P Microondas, G U M wall weapons,
#   E switch (Sotano = the power, 2a planta = helipad lights), C reception phone, % mopped floor trap,
#   S coffee machine, F the sleeping pilot, O where the pulsera de puertas may hide, T the hotel bathrobe, b boss, X helicopter
"""

TITLE = 'Hotel Jubiles: plano por plantas (borrador del 2026-11-17)'
problems, warnings = P.check()
P.show(heights='--heights' in sys.argv)
print('PROBLEMS:' if problems else 'checks OK')
for p in problems:
    print('  ' + p)
if '--png' in sys.argv:
    P.draw(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Renders', 'Plano_Hotel.png'), TITLE, cols=2)
if '--check' not in sys.argv and not problems:
    P.write(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Maps', 'HotelJubiles', 'HotelJubiles_Layout.txt'), HEADER)
