"""Shared helpers and checks for layouts with real floors, one above another (Docs/PLANTAS_FORMATO.md).

A plan is a stack of floors over the same grid: cell (c, r) is the same x, z on every floor. Each floor is a 2.5D grid
like the one-floor maps ([heights] + [map], Docs/FASE3_FORMATO.md) standing at its base level (1 level = 2 m; a storey is
STOREY levels = 4 m: 3.6 m of room and a 0.4 m slab). New map characters:
  ~  void: no floor and no slab on this floor (open to the floor below: stairwells, double-height halls)
  =  lift cab (one cell, the same cell on every floor the lift serves; the shaft is solid on floors it skips)
  $  service door (only for the wearer of the pulsera de puertas; counts as closed in every check)
Used by Tools/Maps/make_hotel_layout.py and make_crucero_layout.py. Run those, not this.
"""
import os
from collections import deque

SOLID = set('#RZ')
PROPS = set('ohxVWAH')
WATER = 'w'
VOID = '~'
LIFT = '='
NOTHING = '.'
STOREY = 2
ARROWS = {'^': (0, -1), 'v': (0, 1), '<': (-1, 0), '>': (1, 0)}
DIRS = ((1, 0), (-1, 0), (0, 1), (0, -1))
MARKERS = set('@sBDPKJbXLGUMEYTCNSFO%Q&:')
SERVICE = '$'     # service door: opens only for whoever wears the pulsera de puertas (hotel); closed for every check
LEGEND = SOLID | PROPS | {WATER, VOID, LIFT, NOTHING, '!', SERVICE} | set('123456789') | MARKERS


def arrow_of(d):
    return {v: k for k, v in ARROWS.items()}[tuple(d)]


class Floor:
    def __init__(self, plan, fid, name, base):
        self.plan, self.id, self.name, self.base = plan, fid, name, base
        self.M = [['.'] * plan.W for _ in range(plan.H)]
        self.HT = [[0] * plan.W for _ in range(plan.H)]
        self.RAMP = {}

    def fill(self, c0, r0, c1, r1, ch):
        for r in range(min(r0, r1), max(r0, r1) + 1):
            for c in range(min(c0, c1), max(c0, c1) + 1):
                self.M[r][c] = ch

    def put(self, c, r, ch):
        self.M[r][c] = ch

    def marks(self, ch, *cells):
        for c, r in cells:
            self.M[r][c] = ch

    def level(self, c0, r0, c1, r1, lv):
        for r in range(r0, r1 + 1):
            for c in range(c0, c1 + 1):
                self.HT[r][c] = lv

    def ramp(self, cells, arrow):
        for c, r in cells:
            self.RAMP[(c, r)] = arrow

    def at(self, c, r):
        return self.M[r][c] if 0 <= c < self.plan.W and 0 <= r < self.plan.H else NOTHING


class Plan:
    def __init__(self, W, H):
        self.W, self.H = W, H
        self.floors = []    # bottom to top
        self.zones = []     # (letter, id, name, group, colour, flags)
        self.doors = []     # (digit, name, cost, 'a b', flags)
        self.lifts = []     # (id, name, c, r, [floor ids])
        self.links = []     # (id, name, (fid, c, r), (fid, c, r), kind)
        self.points = []    # (name, fid, c, r)
        self.extra = []     # (section, text) written as they are

    def floor(self, fid, name, base):
        f = Floor(self, fid, name, base)
        self.floors.append(f)
        return f

    def F(self, fid):
        return next(f for f in self.floors if f.id == fid)

    def above(self, f):
        return next((g for g in self.floors if g.base == f.base + STOREY), None)

    # ------------------------------------------------------------------ building blocks
    def stair(self, lower, entry, fwd, side, zone, upper_zone=None):
        """Switchback stair from `lower` up one storey. entry = the cell you start from on `lower` (level 0, the
        caller makes it floor); fwd = direction of the first flight; side = where the second flight comes back.
        Box: 3 cells wide (side) x 3 long (fwd) beyond the entry, walled round on both floors. Returns the arrival
        cell (entry + 2 * side), which is floor of `upper_zone` (default: `zone`) on the floor above."""
        up = self.above(lower)
        assert up is not None, f'no floor above {lower.id}'
        ex, ey = entry
        fx, fy = fwd
        sx, sy = side

        def cell(a, b):
            return ex + fx * a + sx * b, ey + fy * a + sy * b

        run1, run2 = [cell(1, 0), cell(2, 0)], [cell(2, 2), cell(1, 2)]
        land = [cell(3, 0), cell(3, 1), cell(3, 2)]
        divider = [cell(1, 1), cell(2, 1)]
        box = run1 + land + run2 + divider
        rim = [cell(a, -1) for a in range(1, 5)] + [cell(a, 3) for a in range(1, 5)] + [cell(4, b) for b in range(0, 3)]
        for c, r in rim + divider + [cell(0, 2)]:
            lower.put(c, r, '#')
        for c, r in run1 + land + run2:
            lower.put(c, r, zone)
        lower.ramp(run1, arrow_of(fwd))
        lower.ramp(run2, arrow_of((-fx, -fy)))
        for c, r in land:
            lower.HT[r][c] = 1
        for c, r in rim + divider:
            up.put(c, r, '#')
        for c, r in run1 + land + run2:
            up.put(c, r, VOID)
        arrival = cell(0, 2)
        up.put(*arrival, upper_zone or zone)
        return arrival

    def lift(self, lid, name, c, r, fids):
        served = [f for f in self.floors if f.id in fids]
        lo, hi = min(f.base for f in served), max(f.base for f in served)
        for f in self.floors:
            if f in served:
                f.put(c, r, LIFT)
            elif lo < f.base < hi:
                f.put(c, r, '#')     # the shaft where the lift doesn't stop
        self.lifts.append((lid, name, c, r, list(fids)))

    def link(self, lid, name, a, b, kind):
        self.links.append((lid, name, a, b, kind))

    def point(self, name, fid, c, r):
        self.points.append((name, fid, c, r))

    # ------------------------------------------------------------------ checks
    def check(self):
        letters = {z[0] for z in self.zones}
        group = {z[0]: z[3] for z in self.zones}
        problems, warnings = [], []
        for ch in letters:
            if ch in LEGEND or ch in 'tq^v<>':
                problems.append(f'zone letter {ch} is a legend character')

        # ramp runs: level at the low end, run length, index
        runs = {}
        for f in self.floors:
            for (c, r), a in f.RAMP.items():
                dx, dy = ARROWS[a]
                i = 0
                while f.RAMP.get((c - dx * (i + 1), r - dy * (i + 1))) == a:
                    i += 1
                lc, lr = c - dx * (i + 1), r - dy * (i + 1)
                n = 0
                while f.RAMP.get((lc + dx * (n + 1), lr + dy * (n + 1))) == a:
                    n += 1
                lv = f.HT[lr][lc] if (lc, lr) not in f.RAMP else 0
                runs[(f.id, c, r)] = (a, i, n, lv, (lc, lr))

        def zone_of(f, c, r):
            ch = f.at(c, r)
            if ch in letters:
                return ch
            if ch not in MARKERS:
                return None
            seen, q = {(c, r)}, deque([(c, r)])
            while q:
                x, y = q.popleft()
                for dx, dy in DIRS:
                    n = (x + dx, y + dy)
                    if n in seen:
                        continue
                    nch = f.at(*n)
                    if nch in letters:
                        return nch
                    if nch in MARKERS:
                        seen.add(n)
                        q.append(n)
            return None

        Z = {f.id: [[zone_of(f, c, r) for c in range(self.W)] for r in range(self.H)] for f in self.floors}

        def walk(f, c, r):
            return Z[f.id][r][c] is not None

        def lvl(f, c, r):
            return runs[(f.id, c, r)][3] if (c, r) in f.RAMP else f.HT[r][c]

        # per floor: characters, spawns, covers, wall machines, voids, headroom, ramps
        for f in self.floors:
            up = self.above(f)
            below = next((g for g in self.floors if g.base == f.base - STOREY), None)
            for r in range(self.H):
                for c in range(self.W):
                    ch = f.M[r][c]
                    where = f'{f.id}:{c},{r}'
                    if ch not in LEGEND and ch not in letters:
                        problems.append(f'unknown character {ch!r} at {where}')
                    if ch in MARKERS and not walk(f, c, r):
                        problems.append(f'marker {ch} at {where} touches no floor')
                    nb = [f.at(c + dx, r + dy) for dx, dy in DIRS]
                    if ch == 's' and not any(n in SOLID or n in PROPS or n in (NOTHING, LIFT, WATER) for n in nb):
                        problems.append(f'spawn at {where} has nothing to come out of beside it')
                    if ch == 'x' and sum(walk(f, c + dx, r + dy) for dx, dy in DIRS) < 3:
                        problems.append(f'low cover at {where} in a 1-cell lane')
                    if ch in 'EGUMC' and not any(n in SOLID for n in nb):
                        problems.append(f'{ch} at {where} needs a wall beside it')
                    if ch == VOID and (below is None or below.at(c, r) == NOTHING):
                        problems.append(f'void at {where} has nothing below it')
                    if up is not None and walk(f, c, r) and ((c, r) in f.RAMP or f.HT[r][c] >= 1) and up.at(c, r) != VOID:
                        problems.append(f'no headroom at {where}: raised floor or stair under the slab of floor {up.id} (put ~ above)')
                    if ch.isdigit() and ch != '0' and (c, r) in f.RAMP:
                        problems.append(f'door on a ramp at {where}')
            for (c, r), a in f.RAMP.items():
                _, i, n, lv, (lc, lr) = runs[(f.id, c, r)]
                where = f'{f.id}:{c},{r}'
                if i == 0 and not 2 <= n <= 3:
                    problems.append(f'ramp run at {where} is {n} cells long (2 or 3)')
                if i == 0 and (not walk(f, lc, lr) or (lc, lr) in f.RAMP):
                    problems.append(f'ramp run at {where} has no flat floor at its low end')
                if i == n - 1:
                    dx, dy = ARROWS[a]
                    hc, hr = c + dx, r + dy
                    if lv + 1 >= STOREY and up is not None:
                        if not walk(up, hc, hr) or up.HT[hr][hc] != 0:
                            problems.append(f'stair at {where} reaches floor {up.id} but {up.id}:{hc},{hr} is not floor at level 0')
                    elif not walk(f, hc, hr) or lvl(f, hc, hr) != lv + 1 or (hc, hr) in f.RAMP and runs[(f.id, hc, hr)][1] != 0:
                        problems.append(f'ramp run ending at {where} has no floor of level {lv + 1} at its high end')

        # graph of walkable cells; doors are nodes that are passable only when open
        nodes = {}
        for f in self.floors:
            for r in range(self.H):
                for c in range(self.W):
                    if walk(f, c, r) or f.M[r][c].isdigit():
                        nodes[(f.id, c, r)] = f.M[r][c] if f.M[r][c].isdigit() else ''
        edges = {k: set() for k in nodes}

        def join(a, b):
            edges[a].add(b)
            edges[b].add(a)

        for f in self.floors:
            up = self.above(f)
            for r in range(self.H):
                for c in range(self.W):
                    k = (f.id, c, r)
                    if k not in nodes:
                        continue
                    for dx, dy in ((1, 0), (0, 1)):
                        k2 = (f.id, c + dx, r + dy)
                        if k2 not in nodes:
                            continue
                        a_ramp, b_ramp = (c, r) in f.RAMP, (c + dx, r + dy) in f.RAMP
                        if nodes[k] or nodes[k2]:
                            if not a_ramp and not b_ramp:
                                join(k, k2)       # a door joins whatever stands on both sides of it
                            continue
                        if not a_ramp and not b_ramp:
                            if f.HT[r][c] == f.HT[r + dy][c + dx]:
                                join(k, k2)
                            continue
                        # at least one ramp: only along the run direction
                        for (rc, rr), (oc, orr) in (((c, r), (c + dx, r + dy)), ((c + dx, r + dy), (c, r))):
                            if (rc, rr) not in f.RAMP:
                                continue
                            a, i, n, lv, _ = runs[(f.id, rc, rr)]
                            ax, ay = ARROWS[a]
                            if (oc - rc, orr - rr) == (ax, ay):          # the other cell is uphill of this one
                                if (oc, orr) in f.RAMP and f.RAMP[(oc, orr)] == a:
                                    join((f.id, rc, rr), (f.id, oc, orr))
                                elif i == n - 1 and (oc, orr) not in f.RAMP and f.HT[orr][oc] == lv + 1:
                                    join((f.id, rc, rr), (f.id, oc, orr))
                            elif (oc - rc, orr - rr) == (-ax, -ay):      # downhill
                                if (oc, orr) in f.RAMP and f.RAMP[(oc, orr)] == a:
                                    join((f.id, rc, rr), (f.id, oc, orr))
                                elif i == 0 and (oc, orr) not in f.RAMP and f.HT[orr][oc] == lv:
                                    join((f.id, rc, rr), (f.id, oc, orr))
            for (c, r), a in f.RAMP.items():   # stairs that arrive on the floor above
                _, i, n, lv, _ = runs[(f.id, c, r)]
                dx, dy = ARROWS[a]
                if i == n - 1 and lv + 1 >= STOREY and up is not None and (up.id, c + dx, r + dy) in nodes:
                    join((f.id, c, r), (up.id, c + dx, r + dy))

        def components(open_doors):
            comp, cid = {}, 0
            for k in nodes:
                if k in comp or (nodes[k] and nodes[k] not in open_doors):
                    continue
                comp[k] = cid
                q = deque([k])
                while q:
                    x = q.popleft()
                    for y in edges[x]:
                        if y in comp or (nodes[y] and nodes[y] not in open_doors):
                            continue
                        comp[y] = cid
                        q.append(y)
                cid += 1
            return comp

        cells_of = {z: [k for k in nodes if not nodes[k] and Z[k[0]][k[2]][k[1]] == z] for z in letters}
        for z in sorted(letters):
            if not cells_of[z]:
                problems.append(f'zone {z} has no cells')
        closed = components(set())
        for z in sorted(letters):
            pieces = {closed[k] for k in cells_of[z]}
            if len(pieces) > 1:
                problems.append(f'zone {z} is in {len(pieces)} separate pieces with the doors closed')
        zl = sorted(z for z in letters if cells_of[z])
        for i, a in enumerate(zl):
            for b in zl[i + 1:]:
                reach = closed[cells_of[a][0]] == closed[cells_of[b][0]]
                if reach != (group[a] == group[b]):
                    problems.append(f'zones {a} and {b} are {"connected" if reach else "NOT connected"} with the doors closed (groups {group[a]}, {group[b]})')
        reps = {}
        for z in zl:
            reps.setdefault(group[z], z)
        for d, name, cost, zs, fl in self.doors:
            where = [k for k in nodes if nodes[k] == d]
            if not where:
                problems.append(f'door {d} ({name}) is not on the map')
                continue
            if len({k[0] for k in where}) > 1:
                problems.append(f'door {d} is on more than one floor')
            touch = set()
            for fid, c, r in where:
                for dx, dy in DIRS:
                    z = Z[fid][r + dy][c + dx]
                    if z:
                        touch.add(z)
            if touch != set(zs.split()):
                problems.append(f'door {d} ({name}) touches zones {sorted(touch)}, wants {zs}')
            comp = components({d})
            joined = set()
            gl = sorted(reps)
            for i, ga in enumerate(gl):
                for gb in gl[i + 1:]:
                    if comp.get(cells_of[reps[ga]][0]) == comp.get(cells_of[reps[gb]][0]):
                        joined.add((ga, gb))
            ga, gb = sorted(group[z] for z in zs.split())
            if joined != {(ga, gb)}:
                problems.append(f'door {d} alone joins groups {sorted(joined)}, wants {[(ga, gb)]}')

        # lifts, links, points
        for lid, name, c, r, fids in self.lifts:
            for fid in fids:
                f = self.F(fid)
                landings = [(c + dx, r + dy) for dx, dy in DIRS if walk(f, c + dx, r + dy)]
                if f.at(c, r) != LIFT or len(landings) != 1:
                    problems.append(f'lift {lid} on floor {fid}: cab {f.at(c, r)!r}, {len(landings)} landings (wants 1)')
        for lid, name, a, b, kind in self.links:
            for fid, c, r in (a, b):
                if not walk(self.F(fid), c, r):
                    problems.append(f'link {lid} ({name}): {fid}:{c},{r} is not floor')
        for name, fid, c, r in self.points:
            if not (0 <= c < self.W and 0 <= r < self.H):
                problems.append(f'point {name} is outside the grid')

        # report
        total = 0
        for z in sorted(letters):
            per = {}
            for fid, c, r in cells_of[z]:
                per[fid] = per.get(fid, 0) + 1
            n = len(cells_of[z])
            total += n
            name = next(x[2] for x in self.zones if x[0] == z)
            print(f'zone {z} {name:28s} {n:4d} cells = {n * 4:5d} m2   ' + '  '.join(f'floor {k}: {v}' for k, v in per.items()))
        print(f'total {total} walkable cells = {total * 4} m2')
        for f in self.floors:
            spawns = sum(row.count('s') for row in f.M)
            print(f'floor {f.id} ({f.name}, base {f.base * 2} m): {spawns} spawns')
        return problems, warnings

    # ------------------------------------------------------------------ output
    def show(self, heights=False):
        for f in reversed(self.floors):
            print(f'\n=== floor {f.id}: {f.name} (base level {f.base} = {f.base * 2} m)')
            print('    ' + ''.join(str(c // 10) for c in range(self.W)))
            print('    ' + ''.join(str(c % 10) for c in range(self.W)))
            for r in range(self.H):
                row = ''.join(f.M[r])
                if heights:
                    row += '   ' + self.heights_row(f, r)
                print(f'{r:3d} ' + row)

    def heights_row(self, f, r):
        out = ''
        for c in range(self.W):
            ch = f.M[r][c]
            if (c, r) in f.RAMP:
                out += f.RAMP[(c, r)]
            elif ch in SOLID or ch in (NOTHING, VOID, LIFT, '!') or ch.isdigit():
                out += '.'
            else:
                out += str(f.HT[r][c])
        return out

    def text(self, header):
        out = [header.rstrip(), '', '[zones]']
        out += [f'{z} | {i} | {n} | {g} | {col}' + (f' | {fl}' if fl else '') for z, i, n, g, col, fl in self.zones]
        out += ['', '[doors]']
        out += [f'{d} | {n} | {cost} | {zs}' + (f' | {fl}' if fl else '') for d, n, cost, zs, fl in self.doors]
        out += ['', '[floors]'] + [f'{f.id} | {f.name} | {f.base}' for f in self.floors]
        if self.lifts:
            out += ['', '[lifts]'] + [f'{l} | {n} | {c},{r} | {" ".join(fs)}' for l, n, c, r, fs in self.lifts]
        if self.links:
            out += ['', '[links]'] + [f'{l} | {n} | {a[0]}:{a[1]},{a[2]} > {b[0]}:{b[1]},{b[2]} | {k}' for l, n, a, b, k in self.links]
        if self.points:
            out += ['', '[points]'] + [f'{n} | {fid}:{c},{r}' for n, fid, c, r in self.points]
        for sec, body in self.extra:
            out += ['', f'[{sec}]', body]
        for f in self.floors:
            out += ['', f'[heights {f.id}]'] + [self.heights_row(f, r) for r in range(self.H)]
            out += ['', f'[map {f.id}]'] + [''.join(row) for row in f.M]
        return '\n'.join(out) + '\n'

    def draw(self, path, title, cols=2, px=14):
        """PNG of every floor (top floor first), zones in their colour, for reviewing a plan without Unity."""
        from PIL import Image, ImageDraw, ImageFont

        def ttf(size):
            for name in ('segoeui.ttf', 'arial.ttf', 'DejaVuSans.ttf'):
                try:
                    return ImageFont.truetype(name, size)
                except OSError:
                    pass
            return ImageFont.load_default(size=size)

        font = ttf(int(px * 0.8))
        big = ttf(int(px * 1.3))
        colour = {z[0]: '#' + z[4] for z in self.zones}
        names = {z[0]: z[2] for z in self.zones}
        floors = list(reversed(self.floors))
        rows = (len(floors) + cols - 1) // cols
        # the same crop on every floor (the building plus a margin), so the floors line up
        used = [(c, r) for f in self.floors for r in range(self.H) for c in range(self.W) if f.M[r][c] != NOTHING]
        c0, c1 = max(0, min(c for c, _ in used) - 1), min(self.W - 1, max(c for c, _ in used) + 1)
        r0, r1 = max(0, min(r for _, r in used) - 1), min(self.H - 1, max(r for _, r in used) + 1)
        pw, ph = (c1 - c0 + 1) * px, (r1 - r0 + 1) * px + 2 * px
        legend_h = (len(self.zones) // 3 + 3) * int(px * 1.6)
        img = Image.new('RGB', (cols * pw + (cols + 1) * px, rows * ph + (rows + 1) * px + legend_h + 3 * px), '#F4F1EA')
        d = ImageDraw.Draw(img)
        d.text((px, px // 2), title, fill='#222', font=big)
        for i, f in enumerate(floors):
            ox = px + (i % cols) * (pw + px)
            oy = 3 * px + (i // cols) * (ph + px)
            d.text((ox, oy), f'{f.name}  ({f.base * 2:+d} m)', fill='#222', font=big)
            oy += 2 * px
            for r in range(r0, r1 + 1):
                for c in range(c0, c1 + 1):
                    ch = f.M[r][c]
                    x0, y0 = ox + (c - c0) * px, oy + (r - r0) * px
                    box = (x0, y0, x0 + px - 1, y0 + px - 1)
                    zone = None
                    for dx, dy in ((0, 0),) + DIRS:
                        n = f.at(c + dx, r + dy)
                        if n in colour:
                            zone = n
                            break
                    if ch == NOTHING:
                        continue
                    if ch in SOLID:
                        d.rectangle(box, fill='#4A4642')
                    elif ch == VOID:
                        d.rectangle(box, fill='#2B2B33')
                        d.line((x0, y0 + px - 1, x0 + px - 1, y0), fill='#77778A')
                    elif ch == WATER:
                        d.rectangle(box, fill='#4FA8D8')
                    elif ch == LIFT:
                        d.rectangle(box, fill='#111111')
                        d.text((x0 + 3, y0), '=', fill='#FFD24A', font=font)
                    elif ch.isdigit():
                        d.rectangle(box, fill='#D23A2E')
                        d.text((x0 + 3, y0), ch, fill='white', font=font)
                    elif ch == SERVICE:
                        d.rectangle(box, fill='#7A3FB0')
                        d.text((x0 + 3, y0), '$', fill='white', font=font)
                    else:
                        d.rectangle(box, fill=colour.get(zone, '#BBBBBB'))
                        if (c, r) in f.RAMP:
                            d.text((x0 + 3, y0), f.RAMP[(c, r)], fill='#222', font=font)
                        elif ch in PROPS:
                            d.ellipse((x0 + 3, y0 + 3, x0 + px - 4, y0 + px - 4), fill='#5C4A36' if ch == 'o' else '#9C8A70')
                        elif ch != zone:
                            d.text((x0 + 3, y0), ch, fill='#000', font=font)
                        elif f.HT[r][c]:
                            d.text((x0 + 4, y0), str(f.HT[r][c]), fill='#555', font=font)
        ly = img.height - legend_h - px
        for i, z in enumerate(self.zones):
            x = px + (i % 3) * (img.width // 3)
            y = ly + (i // 3) * int(px * 1.6)
            d.rectangle((x, y, x + px, y + px), fill=colour[z[0]])
            d.text((x + px + 6, y), f'{z[0]}  {names[z[0]]}', fill='#222', font=font)
        y = ly + (len(self.zones) // 3 + 1) * int(px * 1.6)
        d.text((px, y), 'rojo = puerta de pago (número)   morado = puerta de servicio   negro = hueco / ascensor   ^ v < > = rampa / escalera   s = entrada de abuelos', fill='#444', font=font)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path)
        print('drawn', path)

    def write(self, path, header):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8', newline='\n') as fh:
            fh.write(self.text(header))
        print('written', path)
