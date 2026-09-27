"""Measure the PierreKit and its extension: can numbers tell Pierre's hand from an agent's?

Reads only the exported FBX files of the Unity project:
  PK_*.fbx   in UnityProject/Assets/_Project/Art/PierreKit, modelled by Pierre, by hand
  PKX_*.fbx  in UnityProject/Assets/_Project/Art/PierreKit_Ext, made by an agent in his style
and never _ArtSource/assets.blend: 673 of its objects carry stale keys and snap to old
positions on any frame change, so tooling never opens, renders or writes it.

Run, from the repository root:

    blender -b --factory-startup --python-exit-code 1 --python tools/blender/measure_assets.py

Optional, after a bare --:  --out <json>  (default 05_ART/style/metrics.json).

Per file:
  tris            triangles, as Unity counts them
  size_m          width, height and depth in Unity metres: the size in Blender times the
                  importer's globalScale, read from the .meta
  materials       material names
  tris_per_m2     triangles per square metre of surface, in Unity metres
  quad_share, ngon_share   share of faces with four sides, and with more
  bevel_share     of the edges where the surface turns by 20 to 160 degrees, the share that
                  turns by less than 65: a chamfer turns a right-angled corner in two steps of
                  about 45 degrees, a plain box in one of 90
  off_axis_deg    how far faces lean off the nearest axis, area weighted, over the faces within
                  15 degrees of one: 0 for a mesh laid on a grid, a few degrees for wood made
                  slightly irregular on purpose. Roof pitches and chamfers lean more than 15
                  degrees and are left out
  off_axis_share  the share of that area leaning by more than half a degree
  open_edge_share edges with one face only, over all edges

Then per metric: each group's range and median, and the best single threshold between them
with how many of the files it puts on the right side. A metric that splits the two groups
with no overlap is a candidate for a first concrete IMPROVE rule; if none does, the style
guard stays visual and human (ADR-011).
"""

import glob
import json
import math
import os
import re
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import movers_blender as mb  # noqa: E402

GROUPS = (
    ("PK", "hand", "UnityProject/Assets/_Project/Art/PierreKit/PK_*.fbx"),
    ("PKX", "agent", "UnityProject/Assets/_Project/Art/PierreKit_Ext/PKX_*.fbx"),
)
OUT = mb.repo_path("05_ART", "style", "metrics.json")
AXIS_WINDOW_DEG = 15.0
LEAN_DEG = 0.5
TURN_MIN_DEG, CHAMFER_MAX_DEG, TURN_MAX_DEG = 20.0, 65.0, 160.0
# Pierre's module is 2 BU, 3 m once imported at 1.5 (ASSET_LIST section 11). If the plain wall
# does not measure that, the units below are wrong and nothing else is worth reading.
SANITY = ("PK_Wall_Plain.fbx", 0, 3.0, 0.1)

METRICS = ("tris", "tris_per_m2", "quad_share", "ngon_share", "bevel_share",
           "off_axis_deg", "off_axis_share", "open_edge_share")


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = OUT
    for i, a in enumerate(argv):
        if a == "--out" and i + 1 < len(argv):
            out = argv[i + 1]
    return out


def import_scale(fbx):
    """ModelImporter.globalScale from the .meta: the first one after 'meshes:'."""
    with open(fbx + ".meta", encoding="utf-8") as f:
        text = f.read()
    m = re.search(r"\n  meshes:.*?\n    globalScale: ([0-9.eE+-]+)", text, re.S)
    if not m:
        raise RuntimeError("no globalScale in " + fbx + ".meta")
    return float(m.group(1))


def load(path):
    if path.lower().endswith(".blend"):
        raise RuntimeError("measure_assets never opens a .blend: " + path)
    bpy.ops.wm.read_homefile(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o.type == "MESH"]


def measure(path, scale):
    objs = load(path)
    if not objs:
        raise RuntimeError("no mesh in " + path)
    tris = faces = quads = ngons = 0
    edges = open_edges = turning = chamfers = 0
    area_total = near_axis = lean_sum = lean_area = 0.0
    lo = [math.inf] * 3
    hi = [-math.inf] * 3
    materials = set()
    for o in objs:
        mw = o.matrix_world
        for s in o.material_slots:
            if s.material:
                materials.add(s.material.name)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.transform(mw)
        bm.normal_update()
        for v in bm.verts:
            for i in range(3):
                lo[i] = min(lo[i], v.co[i])
                hi[i] = max(hi[i], v.co[i])
        for f in bm.faces:
            n = len(f.verts)
            faces += 1
            tris += n - 2
            quads += n == 4
            ngons += n > 4
            a = f.calc_area()
            area_total += a
            nrm = f.normal
            if nrm.length < 1e-9:
                continue
            lean = math.degrees(math.acos(min(1.0, max(abs(nrm.x), abs(nrm.y), abs(nrm.z)))))
            if lean <= AXIS_WINDOW_DEG:
                near_axis += a
                lean_sum += a * lean
                if lean > LEAN_DEG:
                    lean_area += a
        for e in bm.edges:
            edges += 1
            if len(e.link_faces) == 1:
                open_edges += 1
            elif len(e.link_faces) == 2:
                turn = math.degrees(e.calc_face_angle(0.0))
                if TURN_MIN_DEG <= turn <= TURN_MAX_DEG:
                    turning += 1
                    chamfers += turn < CHAMFER_MAX_DEG
        bm.free()
    size_bu = [hi[i] - lo[i] for i in range(3)]
    # Blender is Z up, Unity Y up: width X, height Z, depth Y.
    size_m = [round(size_bu[0] * scale, 3), round(size_bu[2] * scale, 3), round(size_bu[1] * scale, 3)]
    area_m2 = area_total * scale * scale
    return {
        "tris": tris,
        "size_m": size_m,
        "materials": sorted(materials),
        "tris_per_m2": round(tris / area_m2, 2) if area_m2 else None,
        "quad_share": round(quads / faces, 3) if faces else None,
        "ngon_share": round(ngons / faces, 3) if faces else None,
        "bevel_share": round(chamfers / turning, 3) if turning else 0.0,
        "off_axis_deg": round(lean_sum / near_axis, 3) if near_axis else None,
        "off_axis_share": round(lean_area / near_axis, 3) if near_axis else None,
        "open_edge_share": round(open_edges / edges, 3) if edges else None,
    }


def split(values_a, values_b):
    """The best single threshold between two lists: how many of all the values it puts on
    their group's side, and whether the two ranges overlap at all."""
    pts = sorted(set(values_a + values_b))
    cuts = [(pts[i] + pts[i + 1]) / 2.0 for i in range(len(pts) - 1)] or pts
    best = (0, None, None)
    total = len(values_a) + len(values_b)
    for t in cuts:
        for a_low in (True, False):
            ok = sum((v < t) == a_low for v in values_a) + sum((v >= t) == a_low for v in values_b)
            if ok > best[0]:
                best = (ok, t, a_low)
    ok, t, a_low = best
    overlap = not (max(values_a) < min(values_b) or max(values_b) < min(values_a))
    return {"threshold": None if t is None else round(t, 4),
            "PK_below": a_low, "right_side": f"{ok} of {total}", "ranges_overlap": overlap}


def summary(values):
    s = sorted(values)
    mid = s[len(s) // 2] if len(s) % 2 else (s[len(s) // 2 - 1] + s[len(s) // 2]) / 2.0
    return {"min": round(s[0], 4), "median": round(mid, 4), "max": round(s[-1], 4)}


def main():
    out = parse_args()
    assets = []
    for group, origin, pattern in GROUPS:
        files = sorted(glob.glob(mb.repo_path(pattern)))
        if not files:
            raise RuntimeError("no file for " + pattern)
        for path in files:
            record = {"file": mb._rel(path).replace(os.sep, "/"), "group": group, "origin": origin,
                      "import_scale": import_scale(path)}
            record.update(measure(path, record["import_scale"]))
            assets.append(record)
            print("MEASURE_ASSET {group:<4} {name:<34} {tris:>5} tris  {w:>6.2f} x {h:>5.2f} x {d:>5.2f} m"
                  "  bevel {bevel:.2f}  lean {lean}".format(
                      group=group, name=os.path.basename(path), tris=record["tris"],
                      w=record["size_m"][0], h=record["size_m"][1], d=record["size_m"][2],
                      bevel=record["bevel_share"], lean=record["off_axis_deg"]))

    name, axis, expected, tol = SANITY
    wall = next(a for a in assets if a["file"].endswith("/" + name))
    if abs(wall["size_m"][axis] - expected) > tol:
        raise RuntimeError(f"{name} measures {wall['size_m']} m, expected {expected} m wide: units are off")
    print(f"MEASURE_SANITY {name} is {wall['size_m'][axis]} m wide, as ASSET_LIST says")

    groups, separation = {}, {}
    for group, origin, pattern in GROUPS:
        rows = [a for a in assets if a["group"] == group]
        groups[group] = {"origin": origin, "glob": pattern, "files": len(rows),
                         "metrics": {m: summary([a[m] for a in rows if a[m] is not None]) for m in METRICS}}
    for m in METRICS:
        pk = [a[m] for a in assets if a["group"] == "PK" and a[m] is not None]
        pkx = [a[m] for a in assets if a["group"] == "PKX" and a[m] is not None]
        separation[m] = split(pk, pkx)
        print(f"MEASURE_SPLIT {m:<16} PK {groups['PK']['metrics'][m]}  PKX {groups['PKX']['metrics'][m]}  "
              f"best threshold {separation[m]['threshold']} puts {separation[m]['right_side']} right, "
              f"overlap {separation[m]['ranges_overlap']}")

    report = {
        "about": "Measured by tools/blender/measure_assets.py on the exported FBX, never on "
                 "_ArtSource/assets.blend. What each metric means: the script's docstring. "
                 "The reading for Pierre: 05_ART/STYLE_GUIDE.md section 18.",
        "groups": groups,
        "separation": separation,
        "assets": assets,
    }
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, indent=1)
        f.write("\n")
    print(f"MEASURE_DONE {len(assets)} files -> {out}")


if __name__ == "__main__":
    main()
