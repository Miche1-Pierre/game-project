#!/usr/bin/env python3
"""Validate a PierreKit house plan (the flat house_plan.txt format) and draw it.

    python3 tools/plan/plan_check.py _ArtSource/house_plan.txt
    python3 tools/plan/plan_check.py _ArtSource/manor_plan.txt --html out.html

Format, one record per line (grid of 3 m cells, N = +z = back, S = -z = street):
    C level cx cz room isVoid spec                 cell
    E level cx cz side type                        edge (listed once per shared side)
    S lower upper c0x c0z c1x c1z dir ex ez kind   stair: foot c0, top c1, exit on upper
    R kind x0 z0 x1 z1 ridge high eave             roof (eave in storeys)
    P cx cz | T cx cz | K x z | H cx cz side       porch, terrace, truck, chimney

Rooms named hidden_* are secret rooms: the INT_DOOR into one is drawn as masked.
Exit code 1 when the plan has errors. Stdlib only.
"""
import argparse
import heapq
import html
import sys
from collections import defaultdict, deque

CELL = 3.0  # metres, PierreKit module after the 1.5 import scale
STOREY = 3.06
GROUND = 0.30
# Clear widths in metres (05_ART/ASSET_LIST.md, PierreKit sections). VERANDA_DOOR is assumed.
WIDTH = {"NONE": 3.0, "INT_WIDE_OPENING": 2.25, "GARAGE_DOOR": 2.55, "INT_DOOR": 1.35,
         "EXT_DOOR": 1.20, "VERANDA_DOOR": 1.20}
PASSABLE = set(WIDTH)
EXTERIOR = {"EXT_PLAIN", "EXT_WINDOW_SMALL", "EXT_WINDOW_BIG", "EXT_DOOR", "GARAGE_DOOR",
            "VERANDA_GLASS", "VERANDA_DOOR", "CELLAR_WALL"}
INTERIOR = {"INT_PLAIN", "INT_DOOR", "INT_WIDE_OPENING", "RAILING", "NONE"}
DIRS = {"N": (0, 1), "S": (0, -1), "E": (1, 0), "W": (-1, 0)}
LEVEL_NAME = {-1: "Cellar", 0: "Ground floor", 1: "First floor", 2: "Second floor"}


def canon(level, x, z, side):
    """One key per shared side: every edge is stored as the N or E side of a cell."""
    if side == "S":
        return (level, x, z - 1, "N")
    if side == "W":
        return (level, x - 1, z, "E")
    return (level, x, z, side)


def cells_of(key):
    level, x, z, side = key
    dx, dz = DIRS[side]
    return (level, x, z), (level, x + dx, z + dz)


class Plan:
    def __init__(self, path):
        self.cells, self.edges, self.stairs, self.roofs = {}, {}, [], []
        self.porch, self.terrace, self.truck, self.chimneys = [], [], None, []
        self.errors, self.warnings = [], []
        with open(path, encoding="utf-8") as f:
            for n, raw in enumerate(f, 1):
                t = raw.split()
                if not t or t[0].startswith("#"):
                    continue
                try:
                    self._record(t)
                except (ValueError, IndexError):
                    self.errors.append(f"line {n}: cannot parse '{raw.strip()}'")

    def _record(self, t):
        k = t[0]
        if k == "C":
            key = (int(t[1]), int(t[2]), int(t[3]))
            if key in self.cells:
                self.errors.append(f"cell {key} listed twice")
            self.cells[key] = {"room": t[4], "void": t[5] == "1", "spec": t[6]}
        elif k == "E":
            key = canon(int(t[1]), int(t[2]), int(t[3]), t[4])
            if key in self.edges:
                self.errors.append(f"edge {key} listed twice")
            self.edges[key] = t[5]
        elif k == "S":
            v = [int(a) for a in t[1:7]]
            self.stairs.append({"lower": v[0], "upper": v[1], "c0": (v[2], v[3]),
                                "c1": (v[4], v[5]), "dir": t[7],
                                "exit": (int(t[8]), int(t[9])), "kind": t[10]})
        elif k == "R":
            self.roofs.append({"kind": t[1], "x0": int(t[2]), "z0": int(t[3]), "x1": int(t[4]),
                               "z1": int(t[5]), "ridge": t[6], "high": t[7], "eave": int(t[8])})
        elif k == "P":
            self.porch.append((int(t[1]), int(t[2])))
        elif k == "T":
            self.terrace.append((int(t[1]), int(t[2])))
        elif k == "K":
            self.truck = (int(t[1]), int(t[2]))
        elif k == "H":
            self.chimneys.append((int(t[1]), int(t[2]), t[3]))
        else:
            raise ValueError(k)

    # ---- queries -------------------------------------------------------------------
    def levels(self):
        return sorted({c[0] for c in self.cells})

    def edge_type(self, key):
        """Listed type, None for an open side inside one room, 'MISSING' otherwise."""
        if key in self.edges:
            return self.edges[key]
        a, b = cells_of(key)
        if a in self.cells and b in self.cells and self.cells[a]["room"] == self.cells[b]["room"]:
            return None
        return "MISSING"

    def stair_cells(self):
        """Lower-level cells under the upper half of a flight: not walkable."""
        return {(s["lower"],) + s["c1"] for s in self.stairs}

    # ---- validation ----------------------------------------------------------------
    def validate(self):
        err, warn = self.errors, self.warnings
        for key in self.edges:
            a, b = cells_of(key)
            if a not in self.cells and b not in self.cells:
                err.append(f"edge {key} touches no cell")
        for (level, x, z), c in self.cells.items():
            for side, (dx, dz) in DIRS.items():
                other = (level, x + dx, z + dz)
                key = canon(level, x, z, side)
                t = self.edge_type(key)
                if other not in self.cells:
                    if t == "MISSING":
                        err.append(f"L{level} ({x},{z}) {side}: perimeter side not listed")
                    elif t not in EXTERIOR:
                        err.append(f"L{level} ({x},{z}) {side}: {t} on the perimeter")
                    elif level < 0 and t != "CELLAR_WALL":
                        warn.append(f"L{level} ({x},{z}) {side}: {t} underground")
                elif t == "MISSING":
                    err.append(f"L{level} ({x},{z}) {side}: side between "
                               f"{c['room']} and {self.cells[other]['room']} not listed")
                elif t is not None and t not in INTERIOR and side in ("N", "E"):
                    err.append(f"L{level} ({x},{z}) {side}: {t} between two cells")
            if level > 0 and (level - 1, x, z) not in self.cells:
                err.append(f"L{level} ({x},{z}) stands on nothing")
        self._check_stairs()
        voids = {k for k, c in self.cells.items() if c["void"]}
        over = {(s["upper"],) + s[c] for s in self.stairs for c in ("c0", "c1")}
        for v in sorted(voids - over):
            err.append(f"L{v[0]} ({v[1]},{v[2]}) is void but no stair is under it")
        for r in self.roofs:
            span = (r["z1"] - r["z0"] + 1) if r["ridge"] == "X" else (r["x1"] - r["x0"] + 1)
            if r["kind"] == "GABLE" and span != 2:
                err.append(f"roof {r['x0']},{r['z0']}..{r['x1']},{r['z1']}: gable span {span} "
                           "cells, the kit's roof and Gable_4m pieces cover 2")
        reach = self._reachable()
        need = {k for k, c in self.cells.items() if not c["void"]} - self.stair_cells()
        for k in sorted(need - reach):
            err.append(f"L{k[0]} ({k[1]},{k[2]}) {self.cells[k]['room']}: unreachable")
        return not err

    def _check_stairs(self):
        err = self.errors
        for s in self.stairs:
            lo, up, d = s["lower"], s["upper"], DIRS[s["dir"]]
            tag = f"stair L{lo}->L{up} {s['c0']}"
            if up != lo + 1:
                err.append(f"{tag}: must rise one storey")
            if (s["c0"][0] + d[0], s["c0"][1] + d[1]) != s["c1"]:
                err.append(f"{tag}: top cell is not next to the foot in the climb direction")
            if (s["c1"][0] + d[0], s["c1"][1] + d[1]) != s["exit"]:
                err.append(f"{tag}: exit is not in line with the flight")
            for c in ("c0", "c1"):
                low, high = (lo,) + s[c], (up,) + s[c]
                if low not in self.cells or self.cells[low]["void"]:
                    err.append(f"{tag}: {c} has no floor on L{lo}")
                if high not in self.cells or not self.cells[high]["void"]:
                    err.append(f"{tag}: L{up} above {c} must be a void cell")
            ex = (up,) + s["exit"]
            if ex not in self.cells or self.cells[ex]["void"]:
                err.append(f"{tag}: exit {s['exit']} has no floor on L{up}")
            else:
                t = self.edge_type(canon(up, s["c1"][0], s["c1"][1], s["dir"]))
                if t not in (None, "NONE"):
                    err.append(f"{tag}: the top of the flight runs into {t}")
            ok = False
            for n in (n for n, dd in DIRS.items() if dd != d):
                dx, dz = DIRS[n]
                nb = (lo, s["c0"][0] + dx, s["c0"][1] + dz)
                if nb in self.cells and not self.cells[nb]["void"] and nb not in self.stair_cells():
                    t = self.edge_type(canon(lo, s["c0"][0], s["c0"][1], n))
                    ok |= t is None or t in PASSABLE
            if not ok:
                err.append(f"{tag}: nothing walkable leads to the foot")

    # ---- graph ---------------------------------------------------------------------
    def neighbours(self):
        """Walkable links: cell -> [(cell or 'OUT', width, via)]."""
        blocked = self.stair_cells()
        g = defaultdict(list)
        for k, c in self.cells.items():
            if k in blocked:
                continue
            level, x, z = k
            for side, (dx, dz) in DIRS.items():
                o = (level, x + dx, z + dz)
                t = self.edge_type(canon(level, x, z, side))
                if t == "MISSING" or (t is not None and t not in PASSABLE):
                    continue
                w = WIDTH.get(t, CELL) if t else CELL
                if o not in self.cells:
                    if level == 0:
                        g[k].append(("OUT", w, t))
                        g["OUT"].append((k, w, t))
                elif o not in blocked:
                    g[k].append((o, w, t))
        for s in self.stairs:
            a, b = (s["lower"],) + s["c0"], (s["upper"],) + s["exit"]
            g[a].append((b, CELL, "STAIR"))
            g[b].append((a, CELL, "STAIR"))
        return g

    def _reachable(self):
        g, seen, q = self.neighbours(), {"OUT"}, deque(["OUT"])
        while q:
            for o, _, _ in g[q.popleft()]:
                if o not in seen:
                    seen.add(o)
                    q.append(o)
        return seen - {"OUT"}

    def routes(self):
        """Per room: widest bottleneck to the outside, and walking metres to the nearest exit."""
        g = self.neighbours()
        best = {"OUT": 99.0}
        heap = [(-99.0, "OUT")]
        while heap:  # maximin Dijkstra from the outside
            w, n = heapq.heappop(heap)
            if -w < best.get(n, 0):
                continue
            for o, ow, _ in g[n]:
                cand = min(-w, ow)
                if cand > best.get(o, 0):
                    best[o] = cand
                    heapq.heappush(heap, (-cand, o))
        dist = {"OUT": 0}
        q = deque(["OUT"])
        while q:
            n = q.popleft()
            for o, _, via in g[n]:
                if o not in dist:
                    step = 0 if n == "OUT" else (5.0 if via == "STAIR" else CELL)
                    dist[o] = dist[n] + step
                    q.append(o)
        rooms = defaultdict(list)
        for k, c in self.cells.items():
            if not c["void"] and k in best:
                rooms[(k[0], c["room"])].append(k)
        out = []
        for (level, room), ks in sorted(rooms.items()):
            out.append({"level": level, "room": room, "cells": len(ks),
                        "width": max(best[k] for k in ks),
                        "metres": min(dist.get(k, 999) for k in ks)})
        return out

    def loops(self):
        """Independent loops per level in the room graph (outside counts as a room on L0)."""
        g = self.neighbours()
        room = lambda k: "OUT" if k == "OUT" else self.cells[k]["room"]
        res = {}
        for level in self.levels():
            links = set()  # one per opening between two different rooms
            for k, lst in g.items():
                if k == "OUT" or k[0] != level:
                    continue
                for o, _, via in lst:
                    if via != "STAIR" and room(o) != room(k):
                        links.add(frozenset((k, o)))
            nodes = {room(k) for k in g if k != "OUT" and k[0] == level}
            nodes |= {room(k) for link in links for k in link}
            parent = {n: n for n in nodes}

            def find(n):
                while parent[n] != n:
                    n = parent[n]
                return n
            for link in links:
                a, b = (room(k) for k in link)
                parent[find(a)] = find(b)
            res[level] = len(links) - len(nodes) + len({find(n) for n in nodes})
        return res


# ---- drawing ----------------------------------------------------------------------------
PX = 26  # pixels per metre
C = CELL * PX
WALL = "#3b3530"
GLASS = "#5b9bd5"
HIDDEN = "#c0392b"
PALETTE = ["#e8d9c4", "#d9e4c8", "#cfdce8", "#ecd2cc", "#e3d5ea", "#f1e6b8", "#d2e6df",
           "#e6dccf", "#dcdcec", "#efd9b9", "#cfe3cf", "#e9cfdc"]


def room_colour(room, rooms):
    if room.startswith("hidden_"):
        return "#f4c7c0"
    return PALETTE[sorted(rooms).index(room) % len(PALETTE)]


def draw_level(plan, level, bounds):
    x0, z0, x1, z1 = bounds
    w, h = (x1 - x0 + 1) * C + 80, (z1 - z0 + 1) * C + 90
    ox, oy = 40, 50

    def px(x, z):  # cell corner (x, z) -> svg; z grows up the page (north)
        return ox + (x - x0) * C, oy + (z1 + 1 - z) * C

    s = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w:.0f} {h:.0f}" '
         f'width="{w:.0f}" height="{h:.0f}" font-family="system-ui,sans-serif">',
         '<defs><pattern id="hatch" width="8" height="8" patternUnits="userSpaceOnUse" '
         'patternTransform="rotate(45)"><rect width="8" height="8" fill="#f7f5f2"/>'
         '<line x1="0" y1="0" x2="0" y2="8" stroke="#b9b2a8" stroke-width="2"/></pattern>'
         '<marker id="arr" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" '
         'markerHeight="7" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="#3b3530"/>'
         '</marker></defs>', f'<rect width="{w:.0f}" height="{h:.0f}" fill="#fbfaf8"/>']
    floor = GROUND + level * STOREY
    s.append(f'<text x="{ox}" y="28" font-size="17" font-weight="600" fill="#2b2622">'
             f'L{level} {LEVEL_NAME.get(level, "")}, floor {floor:+.2f} m</text>')
    rooms = {c["room"] for c in plan.cells.values()}
    if level == 0:
        for (x, z), fill in [(p, "#e9e4dc") for p in plan.porch] + \
                             [(t, "#e4ebdc") for t in plan.terrace]:
            a, b = px(x, z + 1)
            s.append(f'<rect x="{a}" y="{b}" width="{C}" height="{C}" fill="{fill}" '
                     'stroke="#c9c2b8" stroke-dasharray="4 3"/>')
        if plan.truck:
            a, b = px(plan.truck[0], plan.truck[1] + 1)
            if b + C <= h:
                s.append(f'<rect x="{a}" y="{b + 8}" width="{2 * C}" height="{C - 16}" rx="6" '
                         'fill="#d8d3cc" stroke="#8a8279"/><text x="{0}" y="{1}" '
                         'font-size="12" text-anchor="middle" fill="#4a443e">truck</text>'
                         .format(a + C, b + C / 2 + 4))
    here = {k: c for k, c in plan.cells.items() if k[0] == level}
    for (_, x, z), c in here.items():
        a, b = px(x, z + 1)
        fill = "url(#hatch)" if c["void"] else room_colour(c["room"], rooms)
        s.append(f'<rect x="{a}" y="{b}" width="{C}" height="{C}" fill="{fill}"/>')
    for st in plan.stairs:
        if st["lower"] != level:
            continue
        (ax, az), (bx, bz) = st["c0"], st["c1"]
        lo_x, hi_x = min(ax, bx), max(ax, bx)
        lo_z, hi_z = min(az, bz), max(az, bz)
        a, b = px(lo_x, hi_z + 1)
        ww, hh = (hi_x - lo_x + 1) * C, (hi_z - lo_z + 1) * C
        pad = 10
        s.append(f'<rect x="{a + pad}" y="{b + pad}" width="{ww - 2 * pad}" '
                 f'height="{hh - 2 * pad}" fill="#fff" stroke="#6d655c"/>')
        horizontal = st["dir"] in "EW"
        n = 14
        for i in range(1, n):
            if horizontal:
                xx = a + pad + i * (ww - 2 * pad) / n
                s.append(f'<line x1="{xx:.1f}" y1="{b + pad}" x2="{xx:.1f}" y2="{b + hh - pad}" '
                         'stroke="#b3aba1"/>')
            else:
                yy = b + pad + i * (hh - 2 * pad) / n
                s.append(f'<line x1="{a + pad}" y1="{yy:.1f}" x2="{a + ww - pad}" y2="{yy:.1f}" '
                         'stroke="#b3aba1"/>')
        f0 = px(ax + 0.5, az + 0.5)
        f1 = px(bx + 0.5, bz + 0.5)
        d = DIRS[st["dir"]]
        f1 = (f1[0] + d[0] * C * 0.3, f1[1] - d[1] * C * 0.3)
        f0 = (f0[0] - d[0] * C * 0.3, f0[1] + d[1] * C * 0.3)
        s.append(f'<line x1="{f0[0]:.1f}" y1="{f0[1]:.1f}" x2="{f1[0]:.1f}" y2="{f1[1]:.1f}" '
                 'stroke="#3b3530" stroke-width="2" marker-end="url(#arr)"/>')
        s.append(f'<text x="{f0[0]:.1f}" y="{f0[1] + (14 if horizontal else 0):.1f}" '
                 'font-size="10" fill="#3b3530" text-anchor="middle">'
                 f'up L{st["upper"]}</text>')
    for (lv, x, z), c in here.items():
        for side in DIRS:
            key = canon(lv, x, z, side)
            other = cells_of(key)[1] if key[1:3] == (x, z) else cells_of(key)[0]
            if other in here and side in ("S", "W"):
                continue  # drawn from the other cell
            t = plan.edge_type(key)
            if t is None:
                continue
            s.append(draw_edge(plan, key, t, px))
    labels = defaultdict(list)
    for (_, x, z), c in here.items():
        if not c["void"]:
            labels[c["room"]].append((x, z))
    for room, ks in labels.items():
        cx = sum(k[0] for k in ks) / len(ks) + 0.5
        cz = sum(k[1] for k in ks) / len(ks) + 0.5
        if (cx - 0.5, cz - 0.5) not in ks:  # L-shaped room: put the label on a real cell
            cx, cz = ks[0][0] + 0.5, ks[0][1] + 0.5
        a, b = px(cx, cz)
        col = HIDDEN if room.startswith("hidden_") else "#2b2622"
        s.append(f'<text x="{a:.1f}" y="{b - 2:.1f}" font-size="12" font-weight="600" '
                 f'text-anchor="middle" fill="{col}">{html.escape(room.replace("_", " "))}</text>')
        s.append(f'<text x="{a:.1f}" y="{b + 12:.1f}" font-size="10.5" text-anchor="middle" '
                 f'fill="#6d655c">{len(ks) * CELL * CELL:.0f} m²</text>')
    # scale bar and north
    bx, by = ox, h - 22
    s.append(f'<line x1="{bx}" y1="{by}" x2="{bx + 2 * C}" y2="{by}" stroke="#3b3530" '
             'stroke-width="3"/>')
    for i in range(3):
        s.append(f'<line x1="{bx + i * C}" y1="{by - 5}" x2="{bx + i * C}" y2="{by + 5}" '
                 'stroke="#3b3530" stroke-width="2"/>')
    s.append(f'<text x="{bx + 2 * C + 8}" y="{by + 4}" font-size="11" fill="#3b3530">'
             '6 m (one cell = 3 m)</text>')
    s.append(f'<text x="{w - 30}" y="30" font-size="13" font-weight="700" '
             'text-anchor="middle" fill="#3b3530">N</text><line x1="{0}" y1="54" x2="{0}" '
             'y2="36" stroke="#3b3530" stroke-width="2" marker-end="url(#arr)"/>'
             .format(w - 30))
    s.append("</svg>")
    return "\n".join(s)


def draw_edge(plan, key, t, px):
    level, x, z, side = key
    if side == "N":
        (ax, ay), (bx, by) = px(x, z + 1), px(x + 1, z + 1)
    else:
        (ax, ay), (bx, by) = px(x + 1, z + 1), px(x + 1, z)
    a, b = cells_of(key)
    masked = t == "INT_DOOR" and any(
        k in plan.cells and plan.cells[k]["room"].startswith("hidden_") for k in (a, b))

    def seg(f0, f1, colour=WALL, width=5, dash=""):
        x0, y0 = ax + (bx - ax) * f0, ay + (by - ay) * f0
        x1, y1 = ax + (bx - ax) * f1, ay + (by - ay) * f1
        d = f' stroke-dasharray="{dash}"' if dash else ""
        return (f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" '
                f'stroke="{colour}" stroke-width="{width}" stroke-linecap="square"{d}/>')

    if t == "NONE":
        return ""
    if t == "RAILING":
        return seg(0, 1, "#8a6d4b", 3, "7 4")
    if t == "VERANDA_GLASS":
        return seg(0, 1, GLASS, 4)
    if t in ("EXT_WINDOW_SMALL", "EXT_WINDOW_BIG"):
        g = 0.18 if t == "EXT_WINDOW_SMALL" else 0.32
        return seg(0, 1) + seg(0.5 - g, 0.5 + g, GLASS, 4) + \
            seg(0.5 - g, 0.5 + g, "#ffffff", 1.2)
    if t in WIDTH:
        g = WIDTH[t] / CELL / 2
        colour = HIDDEN if masked else WALL
        out = seg(0, 0.5 - g, colour) + seg(0.5 + g, 1, colour)
        if t in ("INT_DOOR", "EXT_DOOR", "VERANDA_DOOR"):
            hx, hy = ax + (bx - ax) * (0.5 - g), ay + (by - ay) * (0.5 - g)
            ex, ey = ax + (bx - ax) * (0.5 + g), ay + (by - ay) * (0.5 + g)
            r = WIDTH[t] * PX
            # swing into the cell on the north or east side, drawn as a quarter arc
            nx, ny = (0, -1) if side == "N" else (1, 0)
            tx, ty = hx + nx * r, hy + ny * r
            sweep = 1 if side == "N" else 0
            out += (f'<path d="M{ex:.1f},{ey:.1f} A{r:.1f},{r:.1f} 0 0 {sweep} {tx:.1f},{ty:.1f}'
                    f' L{hx:.1f},{hy:.1f}" fill="none" stroke="{colour}" stroke-width="1.2"/>')
        if masked:
            mx, my = (ax + bx) / 2, (ay + by) / 2
            out += (f'<circle cx="{mx:.1f}" cy="{my:.1f}" r="9" fill="none" stroke="{HIDDEN}" '
                    'stroke-width="2"/>')
        return out
    return seg(0, 1, "#8a8279" if t == "CELLAR_WALL" else WALL, 6 if t.startswith("EXT") or
               t == "CELLAR_WALL" else 3.5)


def bounds_of(plan):
    xs = [k[1] for k in plan.cells] + [p[0] for p in plan.porch + plan.terrace]
    zs = [k[2] for k in plan.cells] + [p[1] for p in plan.porch + plan.terrace]
    if plan.truck:
        xs += [plan.truck[0], plan.truck[0] + 1]
        zs.append(plan.truck[1])
    return min(xs), min(zs), max(xs), max(zs)


def report(plan):
    lines = []
    for level in plan.levels():
        n = sum(1 for k, c in plan.cells.items() if k[0] == level and not c["void"])
        lines.append(f"L{level}: {n} floor cells, {n * CELL * CELL:.0f} m²")
    lines.append("independent loops per level: " +
                 ", ".join(f"L{k} {v}" for k, v in plan.loops().items()))
    lines.append("room | cells | widest route out (m) | metres to the nearest exit")
    for r in plan.routes():
        lines.append(f"  L{r['level']} {r['room']:<16} {r['cells']:>3} {r['width']:>5.2f} "
                     f"{r['metres']:>6.0f}")
    return lines


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("plan")
    ap.add_argument("--html", help="write one page with every level drawn")
    ap.add_argument("--svg-dir", help="write one SVG per level")
    a = ap.parse_args()
    plan = Plan(a.plan)
    ok = plan.validate()
    for e in plan.errors:
        print("ERROR", e)
    for w in plan.warnings:
        print("WARN ", w)
    for line in report(plan):
        print(line)
    b = bounds_of(plan)
    svgs = {lv: draw_level(plan, lv, b) for lv in sorted(plan.levels(), reverse=True)}
    if a.svg_dir:
        for lv, svg in svgs.items():
            with open(f"{a.svg_dir}/L{lv}.svg", "w", encoding="utf-8") as f:
                f.write(svg)
    if a.html:
        body = "\n".join(f"<section>{svg}</section>" for svg in svgs.values())
        rep = html.escape("\n".join(plan.errors + report(plan)))
        with open(a.html, "w", encoding="utf-8") as f:
            f.write(f"<!doctype html><meta charset='utf-8'><title>{html.escape(a.plan)}</title>"
                    "<style>body{background:#fbfaf8;margin:16px;font-family:system-ui}"
                    "section{margin:0 0 24px;overflow-x:auto}svg{max-width:100%;height:auto}"
                    f"pre{{font-size:12px}}</style>{body}<pre>{rep}</pre>")
    print("OK" if ok else f"{len(plan.errors)} error(s)")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
