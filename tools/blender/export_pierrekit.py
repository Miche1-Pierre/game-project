"""Export Pierre's structure kit and our extension pieces to Unity, with the normals fixed and real window sashes.

Pierre models the house kit by hand in `_ArtSource/assets.blend` (walls, floors, roofs, stairs, windows,
the veranda). Our own pieces, which never go into his file, live in `_ArtSource/assets_extension.blend`
(collection `EXT Structure`, the PKX_ pieces). Unity needs one mesh per module, and Pierre's modules are
groups of loose objects. This script is the single path from those two files to the FBX files under
`UnityProject/Assets/_Project/Art/PierreKit` (PK_) and `PierreKit_Ext` (PKX_). It replaces the session
scripts `pk_reexport2.py` (the module export) and the PKX exports of 2026-09-21, keeps everything they did,
and adds two things the vertical slice needs (ADR-009, 03_TECHNICAL/SLICE_ARCHITECTURE.md, owner "Kit").

What it does, in order:
  1. Links assets.blend read only, clears its stale keyframes before anything is evaluated, drops the
     ground plate (anything wider than 12 m), and clusters the loose objects into modules by their
     world boxes (union-find, 6 cm margin). Each module is named by one of Pierre's anchor objects (SPEC).
  2. Joins each module into one mesh: world transform baked, faces reversed for mirrored objects, one
     material slot per distinct material, pivot and rotation per module exactly as the 2026-09-25 export.
  3. Fixes the normals of every module and every PKX piece just before the mesh is written (fix_normals).
  4. Builds the two window modules differently: the wall with its opening, the sill, the caps and a thin
     wooden fixed frame. The glass and the bars leave the module and become two sash leaves per window,
     PKX_Window_<Small|Big>_Sash_<L|R>, each with its pivot on its hinge line.
  5. Exports every piece at the origin with the kit's FBX settings, then compares each file with the one
     already in Unity (a second Blender process imports both) and writes only the files that really
     changed: same vertex set, faces, winding, materials, bounds and pivot means the file is left alone,
     so a re-export never puts byte-only changes in the diff.
  6. Refreshes `_ArtSource/PierreKit_export_modules.blend` (the joined modules as exported, plus the
     sashes in their windows) and writes the four sashes into `assets_extension.blend`, `EXT Structure`,
     so Pierre can see and append them. Both files get a `.blend1` copy first, like a Blender save.

Six decisions worth reading before changing anything:

1. **Normals are fixed at export, not in Pierre's file.** We never write assets.blend. The investigation
   of 2026-09-25 (A2) found 62 inside-out closed parts in the used kit: every ridge tile and gutter of
   PK_Roof_8/11/12 (the "vanishing ridge" seen from above), the glass boxes, two brackets of PK_Post_T,
   and the fence picket tips of our own generator. Blender draws both sides, so none of it shows there;
   Unity culls back faces, so all of it shows in game. fix_normals() repairs a part only when a
   measurement says it is wrong:
     - a closed part is made consistent, then flipped whole if its signed volume is negative. Signed
       volume, not recalc_face_normals, because recalc misses folded parts;
     - a small closed part (64 faces or fewer) still seen only from the back is folded (a bevel wider
       than the tapered box it bevels): it becomes its convex hull, if the hull is at most twice its volume;
     - an open sheet seen more from the back than from the front is reversed whole;
     - a single face of an open sheet that is hidden from the front and open from the back is reversed.
       This is the window-sized hole in PK_Wall_Plain and PK_Wall_Interior (Pierre's `wall 0` and
       `Wall`, face 360: the filler quad on one side of the old window opening points into the wall);
     - a face open on both sides is a real two-sided sheet and gets a reversed twin. Nothing else is
       doubled. Visibility is measured with 64 rays from each side of each face, on the module alone.

2. **The window module keeps its name, pivot and bounds.** 21 windows in Map01 are instances of
   PK_Wall_Window_Small/Big. Same object and mesh name keeps the Unity fileIDs (the prefab's MeshCollider
   points at the mesh by fileID), and the same pivot and bounds keep every instance in place. The module
   loses its glass slot (brique, wall, wood remain); the prefabs have no material overrides, so that is safe.

3. **The sash is Pierre's glass and bars in a new frame.** Stiles and rails (SASH_W wide) are new, in his
   bevelled, slightly wonky style. His horizontal bars are cut in half at the leaf edges, his vertical
   centre bar is dropped (the two meeting stiles replace it), and his glass panes are given to the leaf
   they sit in and clamped so their edges end halfway into the frame, never in the masonry (they were
   buried 9 to 44 mm in the jambs, head and sill). The glass keeps its own material slot named `glass`,
   so the Unity remap to PK_glass, and with it the runtime glass split, still finds it. The leaves are
   rebuilt from assets.blend on every run: Pierre's edits to his glass or bars carry over, and the copies
   in assets_extension.blend are a snapshot for him to look at, overwritten each time and never exported.

4. **The sash pivot is the hinge.** Outer face of the leaf, jamb edge, bottom. A casement 7 to 10 cm
   thick that turns about the middle of its thickness clips its frame from 5 degrees on (measured); about
   this pivot it clears everything from 0 to 95 degrees. The window opens outwards (ADR-008). Two leaves
   opening together still meet: the room-side corner of a meeting stile sits at sqrt(w^2 + t^2) from its
   hinge and sweeps up to 1 cm past the midline in the first 25 degrees. So each leaf is trimmed to a
   cylinder around its own hinge (trim_to_hinge_radius), the rounded meeting stile of a French casement,
   and the two leaves then clear each other at any pair of angles.

5. **L and R follow the module's own axes.** In Unity module space, +x along the wall, +z outside.
   `_L` hinges on the module's -x jamb and its leaf runs towards +x (HingedPanel Hinge.Left); `_R`
   hinges on the +x jamb (Hinge.Right). Seen from the street, `_L` is on your right. The prefab
   children are named Sash_L and Sash_R, and the report gives their local positions in Unity metres.

6. **PKX pieces keep their own pivots.** They are exported from assets_extension.blend at the origin
   with their mesh as it is, as on 2026-09-21. Only the normals pass touches them.

Run headless, no GUI and no MCP (assets.blend is usually open in Pierre's Blender, never build there):

    "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup
        --python tools/blender/export_pierrekit.py -- [options]

Options after a bare `--`:
  --out-root DIR      where the FBX go, as <DIR>/PierreKit and <DIR>/PierreKit_Ext
                      (default: UnityProject/Assets/_Project/Art)
  --compare-root DIR  the FBX to compare with (default: the same Assets folder)
  --all               write every file, changed or not
  --no-fix            no normals pass (control run: must reproduce the 2026-09-25 FBX)
  --no-sashes         the old window modules, glass and bars welded in (control run)
  --no-blends         do not write PierreKit_export_modules.blend or assets_extension.blend
  --report FILE       the JSON report: per file changed or not and why, the fix log per module,
                      the sash placements for the prefabs, and the self-checks below
  --renders DIR       optional Workbench renders (back-face culling on) of the windows, closed and open
  --force             write even if a self-check fails (by default nothing is written then, see below)

Self-checks, in the report (on the built meshes, before export). With the normals pass on, a failed check means
something in the source that these rules do not cover: then no FBX and no blend is written, the report says why,
and --force overrides.
  - closed parts with a negative signed volume, per module (must be 0);
  - both sides of every wall module, a 5 cm grid of rays through the wall with back faces culled per
    triangle and glass counted as see-through: a sample is one-sided when only one direction hits
    something, confirmed by four rays 0.3 mm around it (an opening's exact edge is float noise). Must be 0;
  - the window modules' bounds against the old window modules (must be equal);
  - each sash swung from 0 to 95 degrees about its pivot, in 1 degree steps, against the module and the
    other sash (BVH triangle overlap, must be 0 pairs).

The FBX go to Unity only; the prefab children Sash_L and Sash_R are added by a Unity-side recipe, never
by this script.

Validated 2026-09-25 against assets.blend of 14:45 (Blender 5.1.2), every figure measured on the FBX
re-imported in a fresh session: the control run (--no-fix --no-sashes) reproduces all 48 FBX in Assets.
The full run changes 16 PK modules and 2 PKX pieces (the fences), adds the 4 sashes and leaves 30 files
alone. Inside-out closed parts 86 -> 0 (62 -> 0 in the files the scene uses), faces seen only from the back
5784 -> 0. One-sided samples 324 -> 0 on PK_Wall_Plain and PK_Wall_Interior, 0 on the 9 other wall
modules before and after. Window module bounds and pivots equal to the old ones. Sashes: 0 overlapping
triangle pairs from 0 to 95 degrees, against the module and each other, first contact at 99 degrees.
Workbench renders with back-face culling: the ridge whole from above, the wall holes gone. A run with a
check made to fail (SWING_MAX 100) wrote nothing. Re-run against its own output: 52 files unchanged.
"""

import argparse
import json
import math
import os
import random
import shutil
import subprocess
import sys
import tempfile
import zlib

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = r"C:\GameProject"
SRC = os.path.join(ROOT, r"_ArtSource\assets.blend")
EXT = os.path.join(ROOT, r"_ArtSource\assets_extension.blend")
MODBLEND = os.path.join(ROOT, r"_ArtSource\PierreKit_export_modules.blend")
ART = os.path.join(ROOT, r"UnityProject\Assets\_Project\Art")
PK_DIR, PKX_DIR = "PierreKit", "PierreKit_Ext"
EXT_COLLECTION = "EXT Structure"
MOD_COLLECTION = "PK Export Modules"
SASH_COLLECTION = "PKX Window Sashes"
UNITY_SCALE = 1.5          # ModelImporter globalScale of every kit FBX
BIG = 12.0                 # anything wider than this (Blender units) is a ground plate, not a kit piece
MARGIN = 0.06              # clustering margin between object boxes

# Every module: (name, anchor, pivot_from, rotz, pivot mode). A string anchor takes the whole cluster of that
# object, a list takes exactly those objects. Names are Pierre's objects, as of 2026-09-25.
SPEC = [
    ('PK_Wall_Plain', 'wall 0', ['wall 0'], -90, 'bottom_center'),
    ('PK_Wall_Window_Small', 'wall 1', ['wall 1'], -90, 'bottom_center'),
    ('PK_Wall_Window_Big', 'wall 1.001', ['wall 1.001'], -90, 'bottom_center'),
    ('PK_Wall_Door', 'wall 1.002', ['wall 1.002'], -90, 'bottom_center'),
    ('PK_Door_Leaf', ['plank 1.017'], None, -90, 'bottom_center'),
    ('PK_Wall_Interior', 'Wall', None, -90, 'bottom_center'),
    ('PK_Wall_Corner', 'Plane', ['Plane'], 0, 'min_corner'),
    ('PK_Floor_Plank_A', 'plank big.001', None, 0, 'top_center'),
    ('PK_Floor_Plank_B', 'plank big.022', None, 0, 'top_center'),
    ('PK_Floor_Stone', 'brique.010', None, 0, 'top_center'),
    ('PK_Floor_Upper_A', ['plank big.058'], None, 0, 'top_center'),
    ('PK_Floor_Upper_B', ['plank big.151'], None, 0, 'top_center'),
    ('PK_Post_T', 'Cube.005', ['brique.065'], 0, 'bottom_center'),
    ('PK_Stairs_Wood', 'plank 1.005', None, 0, 'stairs'),
    ('PK_Stairs_Stone', 'plank 1.023', None, 0, 'stairs'),
    ('PK_Veranda_Roof', 'glass alone.012', None, 0, 'bottom_center'),
    ('PK_Veranda_Panel_Narrow', 'glass alone.011', None, -90, 'bottom_center'),
    ('PK_Veranda_Glass_Flat', 'glass alone.021', None, 0, 'bottom_center'),
    ('PK_Veranda_Panel_Wide', 'glass alone.007', None, -90, 'bottom_center'),
    ('PK_Veranda_Glass_Wall', 'glass alone.013', None, -90, 'bottom_center'),
    ('PK_Pillar_Brick', 'brique.005', None, 0, 'bottom_center'),
    ('PK_Roof_8', 'plank big.063', None, 0, 'eave'),
    ('PK_Roof_10', 'plank big.062', None, 0, 'eave'),
    ('PK_Roof_11', 'plank big.106', None, 0, 'eave'),
    ('PK_Roof_12', 'plank big.135', None, 0, 'eave'),
    ('PK_Roof_9', 'plank big.093', None, 0, 'eave'),
]
WINDOWS = {'PK_Wall_Window_Small': 'Small', 'PK_Wall_Window_Big': 'Big'}

# Window parameters, Blender units (x 1.5 in Unity).
FRAME_W = 0.018            # fixed frame (the "dormant" lining the opening): bar width
SASH_W = 0.030             # sash stiles and rails
CLEAR = 0.003              # sash to fixed frame and sill
MEET_GAP = 0.002           # between the two meeting stiles
SINK = 0.002               # the fixed frame sinks this far into the plaster, so no seam shows
SWING_MAX = 95             # the sashes must clear everything from 0 to this many degrees
TRIM_CLEAR = 0.0005        # each leaf stays this far short of the midline, at any angle

# Wall modules checked from both sides, and the grid pitch (Unity metres).
WALL_MODULES = ['PK_Wall_Plain', 'PK_Wall_Interior', 'PK_Wall_Window_Small', 'PK_Wall_Window_Big', 'PK_Wall_Door',
                'PKX_Wall_Cellar', 'PKX_Wall_Garage', 'PKX_Wall_Int_Arch', 'PKX_Wall_Int_Door', 'PKX_Gable_4m',
                'PKX_Gable_4m_Window']
GRID_PITCH_M = 0.05

FBX_SETTINGS = dict(use_selection=True, object_types={'MESH'}, apply_unit_scale=True, global_scale=1.0,
                    bake_space_transform=True, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                    use_mesh_modifiers=True)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="export_pierrekit")
    p.add_argument("--out-root", default=ART)
    p.add_argument("--compare-root", default=ART)
    p.add_argument("--all", action="store_true")
    p.add_argument("--no-fix", action="store_true")
    p.add_argument("--no-sashes", action="store_true")
    p.add_argument("--no-blends", action="store_true")
    p.add_argument("--report", default=None)
    p.add_argument("--renders", default=None)
    p.add_argument("--force", action="store_true")
    p.add_argument("--compare-worker", nargs=2, default=None, help=argparse.SUPPRESS)   # internal: <jobs.json> <out.json>
    return p.parse_args(argv)


# ============================================================ normals ==========================================

DIRS = []
for _i in range(64):                                   # 64 fixed directions on a Fibonacci sphere
    _z = 1 - (_i + 0.5) * 2 / 64
    _r = math.sqrt(max(0.0, 1 - _z * _z))
    _phi = _i * math.pi * (3 - math.sqrt(5))
    DIRS.append(Vector((_r * math.cos(_phi), _r * math.sin(_phi), _z)))
RAY_EPS = 2e-4


def islands(bm):
    """Faces grouped by connected vertices (loose parts)."""
    bm.verts.index_update()
    bm.verts.ensure_lookup_table()
    par = list(range(len(bm.verts)))

    def fd(a):
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a
    for e in bm.edges:
        a, b = fd(e.verts[0].index), fd(e.verts[1].index)
        if a != b:
            par[a] = b
    groups = {}
    for f in bm.faces:
        groups.setdefault(fd(f.verts[0].index), []).append(f)
    return list(groups.values())


def signed_volume(faces):
    ps = [v.co for f in faces for v in f.verts]
    c = sum(ps, Vector()) / max(1, len(ps))
    vol = 0.0
    for f in faces:
        vs = [v.co - c for v in f.verts]
        for i in range(1, len(vs) - 1):
            vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6
    return vol


def edge_stats(faces):
    """Boundary edges, non-manifold edge count and orientation-inconsistent edge count of one part."""
    fs = set(faces)
    es = set(e for f in faces for e in f.edges)
    bnd, nonman, incons = [], 0, 0
    for e in es:
        lf = [l for l in e.link_loops if l.face in fs]
        if len(lf) == 1:
            bnd.append(e)
        elif len(lf) > 2:
            nonman += 1
        elif lf[0].vert == lf[1].vert:
            incons += 1
    return bnd, nonman, incons


def visibility(bvh, f):
    """Share of 64 rays that escape from the front side and from the back side of a face."""
    c = f.calc_center_median()
    n = f.normal
    fr = fo = br = bo = 0
    if n.length < 0.5:
        return 1.0, 0.0
    for d in DIRS:
        dn = d.dot(n)
        if dn > 0.05:
            fr += 1
            fo += bvh.ray_cast(c + n * RAY_EPS, d, 200.0)[0] is None
        elif dn < -0.05:
            br += 1
            bo += bvh.ray_cast(c - n * RAY_EPS, d, 200.0)[0] is None
    return fo / max(1, fr), bo / max(1, br)


def new_fix_log():
    return {'inside_out_parts_flipped': 0, 'inside_out_faces_flipped': 0, 'recalc_inconsistent_parts': 0,
            'closed_parts_still_suspect': [], 'open_parts_made_consistent': 0, 'open_parts_reversed': 0,
            'open_single_faces_reversed': 0, 'reversed_faces': [], 'folded_parts_hulled': 0,
            'folded_faces_replaced': 0, 'hull_skipped': [], 'sheet_faces_doubled': 0}


def fix_normals(bm, log):
    """Decision 1 of the docstring. Works in place on a module mesh, in its export frame."""
    bm.normal_update()
    # 1. closed parts: consistent winding, then outward by signed volume
    for part in islands(bm):
        bnd, nonman, incons = edge_stats(part)
        if bnd or nonman:
            continue
        if incons:
            bmesh.ops.recalc_face_normals(bm, faces=part)
            log['recalc_inconsistent_parts'] += 1
        if signed_volume(part) < 0:
            bmesh.ops.reverse_faces(bm, faces=part)
            log['inside_out_parts_flipped'] += 1
            log['inside_out_faces_flipped'] += len(part)
    bm.normal_update()
    # 2. what is still wrong, by rays: folded small solids, reversed sheets, single reversed faces, true sheets
    bm.faces.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm)
    hull_parts, twins = [], []
    for part in islands(bm):
        bnd, nonman, incons = edge_stats(part)
        v = {f: visibility(bvh, f) for f in part}
        if not bnd and not nonman:
            bad = [f for f in part if v[f][0] < 0.02 and v[f][1] > 0.1]
            if bad and len(part) <= 64:
                hull_parts.append(part)
            elif bad:
                log['closed_parts_still_suspect'].append(len(bad))
            continue
        if incons:
            bmesh.ops.recalc_face_normals(bm, faces=part)
            bm.normal_update()
            log['open_parts_made_consistent'] += 1
            v = {f: visibility(bvh, f) for f in part}
        area = sum(f.calc_area() for f in part) or 1e-12
        fo = sum(v[f][0] * f.calc_area() for f in part) / area
        bo = sum(v[f][1] * f.calc_area() for f in part) / area
        if bo > fo + 0.1:                                             # the whole sheet faces the wrong way
            bmesh.ops.reverse_faces(bm, faces=part)
            log['open_parts_reversed'] += 1
            v = {f: (b, a) for f, (a, b) in v.items()}
        flip1 = [f for f in part if v[f][0] < 0.02 and v[f][1] > 0.1]     # one face turned inward in a sound sheet
        if flip1:
            for f in flip1:
                log['reversed_faces'].append({'centre': [round(x, 4) for x in f.calc_center_median()],
                                              'area': round(f.calc_area(), 4),
                                              'normal_before': [round(x, 3) for x in f.normal]})
            bmesh.ops.reverse_faces(bm, faces=flip1)
            log['open_single_faces_reversed'] += len(flip1)
        twins += [f for f in part if f not in flip1 and v[f][0] > 0.1 and v[f][1] > 0.1]
    # 2a. folded small solids (a bevel wider than the tapered box it bevels): their convex hull
    for part in hull_parts:
        mat = part[0].material_index
        vol = abs(signed_volume(part))
        t = bmesh.new()
        for x in set(vv for f in part for vv in f.verts):
            t.verts.new(x.co)
        bmesh.ops.convex_hull(t, input=t.verts[:])
        t.normal_update()
        hv = abs(signed_volume(t.faces[:])) if t.faces else 0
        if not t.faces or (vol > 0 and hv / vol > 2.0):
            log['hull_skipped'].append((len(part), round(hv / max(vol, 1e-12), 2)))
            t.free()
            continue
        if signed_volume(t.faces[:]) < 0:
            bmesh.ops.reverse_faces(t, faces=t.faces[:])
        verts = set(vv for f in part for vv in f.verts)
        bmesh.ops.delete(bm, geom=list(part), context='FACES_ONLY')
        bmesh.ops.delete(bm, geom=[x for x in verts if x.is_valid and not x.link_faces], context='VERTS')
        vm = {tv: bm.verts.new(tv.co) for tv in t.verts}
        for tf in t.faces:
            nf = bm.faces.new([vm[x] for x in tf.verts])
            nf.material_index = mat
            nf.smooth = False
        log['folded_parts_hulled'] += 1
        log['folded_faces_replaced'] += len(part)
        t.free()
    # 2b. genuinely single-sided sheets seen from both sides: a reversed twin
    twins = [f for f in twins if f.is_valid]
    if twins:
        res = bmesh.ops.duplicate(bm, geom=twins)
        nf = [g for g in res['geom'] if isinstance(g, bmesh.types.BMFace)]
        bmesh.ops.reverse_faces(bm, faces=nf)
        log['sheet_faces_doubled'] += len(nf)
    bm.normal_update()


def inside_out_parts(bm):
    n = 0
    for part in islands(bm):
        bnd, nonman, _ = edge_stats(part)
        if not bnd and not nonman and signed_volume(part) < 0:
            n += 1
    return n


def wall_grid(bm, glass_slots, pitch_m=GRID_PITCH_M):
    """Both sides of a wall along X, thickness along Y (export frame), the way Unity sees it: a ray stops at the
    first triangle it meets from the front (the triangle's own normal, not its polygon's: a warped quad is two
    triangles, and a ray grazing the garage lintel's underside enters through one and leaves through the other),
    back faces are culled and glass lets rays through. A sample is one-sided when one direction
    stops and the other does not, and only if four rays 0.3 mm around it agree: a sample lying exactly on the edge
    of an opening is decided by float noise, a hole is not. Returns the counts; the one-sided samples are defects."""
    bm.normal_update()
    bm.verts.index_update()
    tris = bm.calc_loop_triangles()             # FromBMesh would report each polygon's averaged normal
    bvh = BVHTree.FromPolygons([v.co.copy() for v in bm.verts],
                               [(t[0].vert.index, t[1].vert.index, t[2].vert.index) for t in tris], all_triangles=True)
    glass = [t[0].face.material_index in glass_slots for t in tris]
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    margin = 0.5 / UNITY_SCALE
    span = (max(ys) - min(ys)) + 2 * margin
    pitch = pitch_m / UNITY_SCALE
    jig = 0.0002

    def stops(x, z, d):
        o = Vector((x, min(ys) - margin if d > 0 else max(ys) + margin, z))
        dv = Vector((0, d, 0))
        travelled = 0.0
        for _ in range(64):
            loc, n, idx, dist = bvh.ray_cast(o, dv, span - travelled)
            if loc is None:
                return False
            if n.dot(dv) < 0 and not glass[idx]:
                return True
            travelled += dist + 1e-5
            o = loc + dv * 1e-5
        return False

    def side(x, z):
        a, b = stops(x, z, 1), stops(x, z, -1)          # from -Y (outside), from +Y (inside)
        return 'solid' if a and b else 'open' if not a and not b else ('from_minus_y' if a else 'from_plus_y')
    counts = {'samples': 0, 'solid_both': 0, 'open_both': 0, 'one_sided_from_minus_y': 0, 'one_sided_from_plus_y': 0,
              'edge_samples_not_confirmed': 0}
    one_sided = []
    x = min(xs) + pitch * 0.5
    while x < max(xs):
        z = min(zs) + pitch * 0.5
        while z < max(zs):
            c = side(x, z)
            counts['samples'] += 1
            if c == 'solid':
                counts['solid_both'] += 1
            elif c == 'open':
                counts['open_both'] += 1
            elif all(side(x + dx, z + dz) == c for dx, dz in ((jig, 0), (-jig, 0), (0, jig), (0, -jig))):
                counts['one_sided_' + c] += 1
                if len(one_sided) < 20:
                    one_sided.append([round(-x * UNITY_SCALE, 3), round(z * UNITY_SCALE, 3), c])
            else:
                counts['edge_samples_not_confirmed'] += 1
            z += pitch
        x += pitch
    counts['one_sided'] = counts['one_sided_from_minus_y'] + counts['one_sided_from_plus_y']
    counts['first_one_sided_unity_x_y'] = one_sided
    return counts


# ============================================================ assets.blend ======================================

def link_source():
    """assets.blend, linked read only, stale keyframes cleared before any evaluation (they snap 377 objects
    back to old positions), the ground plate dropped, the rest clustered into modules."""
    with bpy.data.libraries.load(SRC, link=True) as (df, dt):
        dt.scenes = ['Scene']
    lib = next(l for l in bpy.data.libraries
               if os.path.normcase(os.path.abspath(bpy.path.abspath(l.filepath))) == os.path.normcase(SRC))
    lib.reload()
    scene = next(s for s in bpy.data.scenes if s.library and s.name == 'Scene')
    cleared = 0
    for o in bpy.data.objects:
        if o.library and o.animation_data:
            o.animation_data_clear()
            cleared += 1
    saved = {o.name: o.location.copy() for o in scene.objects}
    scene.view_layers[0].update()
    moved = [o.name for o in scene.objects if (o.location - saved[o.name]).length > 1e-4]

    def mw(o):
        m = o.matrix_parent_inverse @ o.matrix_basis
        return (mw(o.parent) @ m) if o.parent else m

    def world(o):
        return o.matrix_world if (o.matrix_world != Matrix.Identity(4) or mw(o) == Matrix.Identity(4)) else mw(o)

    objs0 = [o for o in scene.objects if o.type == 'MESH' and len(o.data.vertices) > 0]

    def aabb(o):
        m = world(o)
        ps = [m @ v.co for v in o.data.vertices]
        return (min(p.x for p in ps), min(p.y for p in ps), min(p.z for p in ps),
                max(p.x for p in ps), max(p.y for p in ps), max(p.z for p in ps))
    bbs_all = {o.name: aabb(o) for o in objs0}
    plates = [n for n, b in bbs_all.items() if max(b[3] - b[0], b[4] - b[1]) > BIG]
    objs = [o for o in objs0 if o.name not in plates]
    bbs = {o.name: bbs_all[o.name] for o in objs}
    names = list(bbs)
    parent = {n: n for n in names}

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for i in range(len(names)):
        a = bbs[names[i]]
        for j in range(i + 1, len(names)):
            b = bbs[names[j]]
            if (a[0] - MARGIN <= b[3] and b[0] - MARGIN <= a[3] and a[1] - MARGIN <= b[4] and b[1] - MARGIN <= a[4]
                    and a[2] - MARGIN <= b[5] and b[2] - MARGIN <= a[5]):
                ra, rb = find(names[i]), find(names[j])
                if ra != rb:
                    parent[ra] = rb
    clusters = {}
    for n in names:
        clusters.setdefault(find(n), []).append(n)
    return {'objects': {o.name: o for o in objs}, 'bbs': bbs, 'clusters': clusters, 'world': world,
            'plates': plates, 'cleared_keyframes': cleared, 'moved_on_eval': len(moved)}


def cluster_of(src, name):
    if name not in src['bbs']:
        return None
    return next(m for m in src['clusters'].values() if name in m)


def world_bm(src, o):
    """One source object as a bmesh in assets.blend world space, faces reversed if the object is mirrored."""
    t = bmesh.new()
    t.from_mesh(o.data)
    m = src['world'](o)
    t.transform(m)
    if m.determinant() < 0:
        bmesh.ops.reverse_faces(t, faces=t.faces[:])
    return t


def append_bm(dst, t, material_index=None):
    """Appends bmesh t into dst (t is freed)."""
    if material_index is not None:
        for f in t.faces:
            f.material_index = material_index
    me = bpy.data.meshes.new('_t')
    t.to_mesh(me)
    t.free()
    dst.from_mesh(me)
    bpy.data.meshes.remove(me)


def gather(src, members):
    """Members joined in world space, one slot per distinct material (by name, '__none__' for empty slots),
    in order of first use: exactly the 2026-09-25 module build."""
    bm = bmesh.new()
    mats, keys = [], []
    for mn in members:
        o = src['objects'][mn]
        t = world_bm(src, o)
        remap = {}
        slots = list(o.material_slots)
        for i, s in enumerate(slots):
            key = s.material.name if s.material else '__none__'
            if key not in keys:
                keys.append(key)
                mats.append(s.material)
            remap[i] = keys.index(key)
        if not slots:
            if '__none__' not in keys:
                keys.append('__none__')
                mats.append(None)
            remap = {0: keys.index('__none__')}
        for f in t.faces:
            f.material_index = remap.get(f.material_index, remap.get(0, 0))
        append_bm(bm, t)
    return bm, mats


def pivot_of(src, members, pivot_from, mode):
    bbs = src['bbs']
    ref = pivot_from if pivot_from else members
    mnv = Vector([min(bbs[m][i] for m in ref) for i in range(3)])
    mxv = Vector([max(bbs[m][i + 3] for m in ref) for i in range(3)])
    amn = Vector([min(bbs[m][i] for m in members) for i in range(3)])
    amx = Vector([max(bbs[m][i + 3] for m in members) for i in range(3)])
    if mode == 'bottom_center':
        return Vector(((mnv.x + mxv.x) / 2, (mnv.y + mxv.y) / 2, mnv.z))
    if mode == 'top_center':
        return Vector(((mnv.x + mxv.x) / 2, (mnv.y + mxv.y) / 2, amx.z))
    if mode in ('stairs', 'eave'):
        return Vector(((amn.x + amx.x) / 2, amn.y, amn.z))
    if mode == 'min_corner':
        return Vector((amn.x, amn.y, amn.z))
    raise ValueError(mode)


def to_export_frame(pivot, rotz):
    """World of assets.blend to the module's export frame: pivot to the origin, then the kit's Z rotation."""
    m = Matrix.Translation(-pivot)
    if rotz:
        m = Matrix.Rotation(math.radians(rotz), 4, 'Z') @ m
    return m


def members_of(src, name, anchor, report):
    if isinstance(anchor, list):
        members = [m for m in anchor if m in src['objects']]
        if len(members) != len(anchor):
            report['missing_anchors'].append([name, [m for m in anchor if m not in src['objects']]])
        return members
    members = cluster_of(src, anchor)
    if members is None:
        report['missing_anchors'].append([name, anchor])
        return []
    if name == 'PK_Wall_Door':
        members = [m for m in members if m != 'plank 1.004']
    return members


# ============================================================ windows ===========================================

def box(dst, lo, hi, mi, rnd, bevel=0.004, jit=0.001):
    """A bevelled box with a little jitter: the PKX construction, in Pierre's slightly wonky style."""
    t = bmesh.new()
    bmesh.ops.create_cube(t, size=1.0)
    size = [hi[i] - lo[i] for i in range(3)]
    ctr = [(hi[i] + lo[i]) / 2 for i in range(3)]
    for v in t.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    bv = min(bevel, min(size) * 0.3)
    if bv > 0:
        bmesh.ops.bevel(t, geom=list(t.edges), offset=bv, segments=1, affect='EDGES', profile=0.5)
    for v in t.verts:
        v.co += Vector((rnd.uniform(-jit, jit), rnd.uniform(-jit, jit), rnd.uniform(-jit, jit))) + Vector(ctr)
    append_bm(dst, t, mi)


def clip_y(t, y0, y1):
    """Keeps the part of t between y0 and y1 (source frame), caps the cuts."""
    for co, no in (((0, y0, 0), (0, -1, 0)), ((0, y1, 0), (0, 1, 0))):
        bmesh.ops.bisect_plane(t, geom=t.verts[:] + t.edges[:] + t.faces[:], dist=1e-6, plane_co=co, plane_no=no,
                               clear_outer=True)
        bnd = [e for e in t.edges if e.is_boundary]
        if bnd:
            bmesh.ops.holes_fill(t, edges=bnd, sides=0)
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    return t


def vert_islands(t):
    t.verts.index_update()
    t.verts.ensure_lookup_table()
    seen, out = set(), []
    for v in t.verts:
        if v.index in seen:
            continue
        st, comp = [v], []
        seen.add(v.index)
        while st:
            x = st.pop()
            comp.append(x)
            for e in x.link_edges:
                w = e.other_vert(x)
                if w.index not in seen:
                    seen.add(w.index)
                    st.append(w)
        out.append(comp)
    return out


def trim_to_hinge_radius(t, radius, steps=8):
    """Cuts away whatever of a leaf lies farther than `radius` from its hinge axis (Z through the origin), with
    planes tangent to that cylinder. Two leaves opening outwards together sweep their meeting stiles' room-side
    corners past the midline (that corner sits at sqrt(w^2 + t^2) from the hinge): up to 1 cm at 13 degrees.
    Rounding that edge is what a French casement's meeting stiles do. Returns what was cut (Blender units)."""
    xs = [v.co.x for v in t.verts]
    ys = [v.co.y for v in t.verts]
    s = -1.0 if abs(min(xs)) > abs(max(xs)) else 1.0          # the free edge side along X
    far = max(Vector((v.co.x, v.co.y)).length for v in t.verts)
    if far <= radius:
        return {'radius': round(radius, 5), 'farthest_before': round(far, 5), 'cut': 0.0}
    a_max = math.atan2(max(max(ys), 0.0) + 0.01, radius)
    a_min = math.atan2(min(min(ys), 0.0) - 0.005, radius)
    for k in range(steps + 1):
        a = a_min + (a_max - a_min) * k / steps
        n = Vector((s * math.cos(a), math.sin(a), 0.0))
        bmesh.ops.bisect_plane(t, geom=t.verts[:] + t.edges[:] + t.faces[:], dist=1e-7, plane_co=n * radius,
                               plane_no=n, clear_outer=True)
        bnd = [e for e in t.edges if e.is_boundary]
        if bnd:
            bmesh.ops.holes_fill(t, edges=bnd, sides=0)
    after = max(Vector((v.co.x, v.co.y)).length for v in t.verts)
    return {'radius': round(radius, 5), 'farthest_before': round(far, 5), 'farthest_after': round(after, 5),
            'cut': round(far - after, 5)}


def build_window(src, name, key, members, pivot_from, rotz, mode, do_fix):
    """Decisions 2 to 5 of the docstring. Returns the module (bmesh, materials, log) and the two leaves."""
    anchor = pivot_from[0]
    objs, bbs = src['objects'], src['bbs']
    glass = [n for n in members if objs[n].material_slots
             and all(s.material and s.material.name == 'glass' for s in objs[n].material_slots)]
    planks = [n for n in members if n.startswith('plank')]
    vbars = [n for n in planks if (bbs[n][5] - bbs[n][2]) > (bbs[n][4] - bbs[n][1])]
    hbars = [n for n in planks if n not in vbars]
    sill = [n for n in members if n.startswith('support')]
    body = [n for n in members if n not in glass + planks]
    if not glass or not hbars or len(sill) != 1 or anchor not in body:
        raise RuntimeError('%s: the window members changed (glass %s, bars %s, sill %s); check SPEC and build_window'
                           % (name, glass, planks, sill))
    wood = next(s.material for s in objs[hbars[0]].material_slots if s.material)
    glass_mat = next(s.material for s in objs[glass[0]].material_slots if s.material)

    # the opening at the glass plane, by rays on the wall mesh only
    tw = world_bm(src, objs[anchor])
    tree = BVHTree.FromBMesh(tw)
    tw.free()
    gmin = Vector([min(bbs[n][i] for n in glass) for i in range(3)])
    gmax = Vector([max(bbs[n][i + 3] for n in glass) for i in range(3)])
    gc = (gmin + gmax) / 2

    def hit(d, axis):
        h = tree.ray_cast(gc, Vector(d), 5.0)
        if h[0] is None:
            raise RuntimeError('%s: no jamb found from the glass centre along %s' % (name, d))
        return h[0][axis]
    oy0, oy1, oz0, oz1 = hit((0, -1, 0), 1), hit((0, 1, 0), 1), hit((0, 0, -1), 2), hit((0, 0, 1), 2)
    zs = bbs[sill[0]][5]                                     # sill top
    xs = [bbs[n][0] for n in glass + planks] + [bbs[n][3] for n in glass + planks]
    sx0, sx1 = min(xs), max(xs)                              # sash depth = Pierre's glass and bars; +X is outside
    P = pivot_of(src, members, pivot_from, mode)
    E = to_export_frame(P, rotz)

    # (a) the module: Pierre's wall, sill and caps, plus the fixed frame
    bm, mats = gather(src, body)
    if wood not in mats:
        mats.append(wood)
    wi = mats.index(wood)
    rnd = random.Random(zlib.crc32(name.encode()))
    dx0, dx1 = sx0 - 0.01, sx1
    box(bm, (dx0, oy0 - SINK, zs - SINK), (dx1, oy0 + FRAME_W, oz1 + SINK), wi, rnd)          # jamb
    box(bm, (dx0, oy1 - FRAME_W, zs - SINK), (dx1, oy1 + SINK, oz1 + SINK), wi, rnd)          # jamb
    box(bm, (dx0, oy0 + FRAME_W, oz1 - FRAME_W), (dx1, oy1 - FRAME_W, oz1 + SINK), wi, rnd)   # head
    box(bm, (dx0, oy0 + FRAME_W, zs - SINK), (dx1, oy1 - FRAME_W, zs + FRAME_W), wi, rnd)     # bottom bar on the sill
    bm.transform(E)
    log = new_fix_log()
    log['faces_before'] = len(bm.faces)
    if do_fix:
        fix_normals(bm, log)
    log['faces_after'] = len(bm.faces)
    info = {'members': {'wall': anchor, 'glass': glass, 'vertical_bars_dropped': vbars, 'horizontal_bars_cut': hbars,
                        'sill': sill, 'caps': [n for n in body if n not in sill + [anchor]]},
            'opening_src_y0_y1_z0_z1': [round(oy0, 4), round(oy1, 4), round(oz0, 4), round(oz1, 4)],
            'sill_top': round(zs, 4), 'sash_depth_src_x': [round(sx0, 4), round(sx1, 4)], 'pivot_src': [round(x, 4) for x in P]}
    # the old module's bounds (everything, glass and bars included), for the bounds check
    old = [E @ Vector(c) for n in members for c in _corners(bbs[n])]
    info['old_module_bounds_export'] = [[round(min(p[i] for p in old), 4) for i in range(3)],
                                        [round(max(p[i] for p in old), 4) for i in range(3)]]

    # (b) the two leaves
    lz0, lz1 = zs + FRAME_W + CLEAR, oz1 - FRAME_W - CLEAR
    ymid = (oy0 + oy1) / 2
    spans = {'A': (oy0 + FRAME_W + CLEAR, ymid - MEET_GAP / 2), 'B': (ymid + MEET_GAP / 2, oy1 - FRAME_W - CLEAR)}
    leaves = {}
    for side, (y0, y1) in spans.items():
        # A = low source y = export -X = Unity +x: hinged on the module's +x jamb = Hinge.Right, "_R"
        uside = 'R' if side == 'A' else 'L'
        lname = 'PKX_Window_%s_Sash_%s' % (key, uside)
        lrnd = random.Random(zlib.crc32(lname.encode()))
        t = bmesh.new()
        box(t, (sx0, y0, lz0), (sx1, y0 + SASH_W, lz1), 0, lrnd)                       # stile
        box(t, (sx0, y1 - SASH_W, lz0), (sx1, y1, lz1), 0, lrnd)                       # stile
        box(t, (sx0, y0 + SASH_W, lz0), (sx1, y1 - SASH_W, lz0 + SASH_W), 0, lrnd)     # bottom rail
        box(t, (sx0, y0 + SASH_W, lz1 - SASH_W), (sx1, y1 - SASH_W, lz1), 0, lrnd)     # top rail
        for n in hbars:                                                                 # Pierre's bars, cut
            append_bm(t, clip_y(world_bm(src, objs[n]), y0 + SASH_W - 0.003, y1 - SASH_W + 0.003), 0)
        g = bmesh.new()
        for n in glass:
            append_bm(g, world_bm(src, objs[n]))
        kill = []
        for comp in vert_islands(g):                                                    # the panes of this leaf
            cy = sum(v.co.y for v in comp) / len(comp)
            if not (y0 <= cy <= y1):
                kill += comp
        bmesh.ops.delete(g, geom=kill, context='VERTS')
        for v in g.verts:                                                               # clamped into the frame
            v.co.y = min(max(v.co.y, y0 + SASH_W / 2), y1 - SASH_W / 2)
            v.co.z = min(max(v.co.z, lz0 + SASH_W / 2), lz1 - SASH_W / 2)
        panes = len(vert_islands(g))
        append_bm(t, g, 1)
        hinge_y = y0 if side == 'A' else y1                                             # the jamb side
        H = Vector((sx1, hinge_y, lz0))                                                 # outer face, jamb edge, bottom
        t.transform(Matrix.Rotation(math.radians(rotz), 4, 'Z') @ Matrix.Translation(-H))
        trimmed = trim_to_hinge_radius(t, abs(hinge_y - ymid) - TRIM_CLEAR)
        bmesh.ops.dissolve_degenerate(t, dist=1e-6, edges=t.edges[:])               # slivers left by the cuts
        llog = new_fix_log()
        llog['faces_before'] = len(t.faces)
        if do_fix:
            fix_normals(t, llog)
        llog['faces_after'] = len(t.faces)
        q = Matrix.Rotation(math.radians(rotz), 4, 'Z') @ (H - P)                      # hinge in the module's frame
        leaves[lname] = {'bm': t, 'mats': [wood, glass_mat], 'log': llog, 'place_export': q, 'panes': panes,
                         'hinge': 'Left' if uside == 'L' else 'Right', 'child': 'Sash_' + uside,
                         'src_y': [round(y0, 4), round(y1, 4)], 'src_z': [round(lz0, 4), round(lz1, 4)],
                         'meeting_stile_trim': trimmed,
                         'hinge_src': [round(x, 4) for x in H]}
    return bm, mats, log, info, leaves


def merge_bm(dst, src, mat_offset=0):
    """Copies src's faces into dst directly (no mesh round trip, so material indices are kept as they are)."""
    vm = {v: dst.verts.new(v.co) for v in src.verts}
    for f in src.faces:
        nf = dst.faces.new([vm[v] for v in f.verts])
        nf.material_index = f.material_index + mat_offset
    dst.normal_update()


def _corners(b):
    return [(x, y, z) for x in (b[0], b[3]) for y in (b[1], b[4]) for z in (b[2], b[5])]


def is_sash(name):
    return name.startswith('PKX_Window_') and '_Sash_' in name


def unity_pos(q):
    """Export frame (Blender units) to Unity module space (metres): x = -X, y = Z, z = -Y, times 1.5."""
    return [round(-q.x * UNITY_SCALE, 4), round(q.z * UNITY_SCALE, 4), round(-q.y * UNITY_SCALE, 4)]


def open_sign(leaf_bm):
    """The rotation sign about Z (export frame) that takes the leaf's free edge outside (-Y)."""
    xs = [v.co.x for v in leaf_bm.verts]
    free = Vector((min(xs) if abs(min(xs)) > abs(max(xs)) else max(xs), 0, 0))
    plus = Matrix.Rotation(math.radians(30), 4, 'Z') @ free
    return 1 if plus.y < 0 else -1


def bm_tree(bm, m=None):
    t = bm.copy()
    if m is not None:
        t.transform(m)
    tree = BVHTree.FromBMesh(t)
    t.free()
    return tree


def swing_check(module_bm, leaves):
    """Both sashes swung together from 0 to SWING_MAX degrees about their pivots: overlapping triangle pairs
    against the module and against each other, per degree."""
    tm = bm_tree(module_bm)
    rows, worst = [], 0
    names = sorted(leaves)
    for ang in range(0, SWING_MAX + 1):
        trees = {}
        for n in names:
            lf = leaves[n]
            trees[n] = bm_tree(lf['bm'], Matrix.Translation(lf['place_export'])
                               @ Matrix.Rotation(math.radians(lf['open_sign'] * ang), 4, 'Z'))
        pairs = {n: len(tm.overlap(trees[n])) for n in names}
        pairs['leaf_vs_leaf'] = len(trees[names[0]].overlap(trees[names[1]]))
        worst = max(worst, max(pairs.values()))
        if ang % 5 == 0 or any(pairs.values()):
            rows.append([ang, pairs])
    return {'max_pairs_0_to_%d' % SWING_MAX: worst, 'rows_every_5_degrees': rows}


# ============================================================ export and compare ================================

def export_object(ob, scene, path):
    vl = scene.view_layers[0]
    for o in scene.collection.objects:
        o.select_set(False, view_layer=vl)
    ob.select_set(True, view_layer=vl)
    vl.objects.active = ob
    with bpy.context.temp_override(scene=scene, view_layer=vl, active_object=ob, selected_objects=[ob],
                                   selected_editable_objects=[ob]):
        bpy.ops.export_scene.fbx(filepath=path, **FBX_SETTINGS)


def new_mesh_object(name, bm, mats):
    """An object and a mesh called exactly `name` (the FBX node and geometry names are Unity's fileIDs)."""
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    if ob.name != name or me.name != name:
        raise RuntimeError('name clash: wanted %s, got object %s mesh %s' % (name, ob.name, me.name))
    return ob


def fbx_signature(path):
    """Import one FBX in an emptied session and describe it: names, pivot, vertices, faces with winding."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials):
        for x in list(coll):
            coll.remove(x)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if len(objs) != 1:
        return {'error': '%d mesh objects' % len(objs)}
    o = objs[0]
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.transform(o.matrix_world)
    if o.matrix_world.determinant() < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.normal_update()
    mats = [m.name if m else None for m in o.data.materials]
    faces = [(f.calc_center_median().copy(), f.normal.copy(), mats[f.material_index] if f.material_index < len(mats) else '?',
              f.calc_area()) for f in bm.faces]
    verts = [v.co.copy() for v in bm.verts]
    sig = {'object': o.name, 'mesh': o.data.name, 'mats': mats, 'verts': len(bm.verts), 'faces': len(bm.faces),
           'tris': sum(len(f.verts) - 2 for f in bm.faces),
           'bounds_min': [round(min(p[i] for p in verts), 4) for i in range(3)],
           'bounds_max': [round(max(p[i] for p in verts), 4) for i in range(3)],
           'matrix': [[round(x, 4) for x in row] for row in o.matrix_world],
           '_faces': faces, '_verts': verts}
    bm.free()
    return sig


def unmatched(a, b, same, tol):
    """How many items of a have no item of b within tol (KD-tree on the first field) that `same` accepts."""
    from mathutils.kdtree import KDTree
    kd = KDTree(len(b))
    for i, x in enumerate(b):
        kd.insert(x[0], i)
    kd.balance()
    return sum(1 for x in a if not any(same(x, b[i]) for (_c, i, _d) in kd.find_range(x[0], tol)))


def same_face(x, y):
    """Same material, and same winding unless the face has no area (its normal is then meaningless)."""
    if x[2] != y[2]:
        return False
    if x[3] < 1e-9 and y[3] < 1e-9:
        return True
    return x[1].dot(y[1]) > 0.95


def compare_worker(jobs_path, out_path):
    """Runs in a second Blender process, so imported material names are never renamed by a clash.
    Tolerances, in Blender units: 1e-4 on vertices, 2e-4 on face centres (0.15 and 0.3 mm in Unity); 18 degrees
    on face normals. Tighter, and float noise between two exports of the same mesh reads as a change."""
    jobs = json.load(open(jobs_path))
    res = {}
    for key, new, old in jobs:
        a = fbx_signature(new)
        clean = lambda s: {k: v for k, v in s.items() if not k.startswith('_')}
        if old is None or not os.path.exists(old):
            res[key] = {'status': 'new', 'new': clean(a)}
            continue
        b = fbx_signature(old)
        diff = []
        if 'error' in a or 'error' in b:
            diff.append('import')
        else:
            for k in ('object', 'mesh', 'mats', 'verts', 'faces', 'tris'):
                if a[k] != b[k]:
                    diff.append(k)
            for k in ('bounds_min', 'bounds_max'):
                if any(abs(x - y) > 2e-4 for x, y in zip(a[k], b[k])):
                    diff.append(k)
            if any(abs(x - y) > 1e-4 for ra, rb in zip(a['matrix'], b['matrix']) for x, y in zip(ra, rb)):
                diff.append('pivot_or_axes')
            va = [(v,) for v in a['_verts']]
            vb = [(v,) for v in b['_verts']]
            nv = unmatched(va, vb, lambda x, y: True, 1e-4) + unmatched(vb, va, lambda x, y: True, 1e-4)
            if nv:
                diff.append('vertex_set (%d unmatched)' % nv)
            na = unmatched(a['_faces'], b['_faces'], same_face, 2e-4)
            nb = unmatched(b['_faces'], a['_faces'], same_face, 2e-4)
            if na or nb:
                diff.append('faces_or_winding (%d only in new, %d only in old)' % (na, nb))
        res[key] = {'status': 'changed' if diff else 'unchanged', 'diff': diff, 'new': clean(a), 'old': clean(b)}
    json.dump(res, open(out_path, 'w'), indent=1)


def run_compare(pairs):
    tmp = tempfile.mkdtemp(prefix='pk_cmp_')
    jobs, out = os.path.join(tmp, 'jobs.json'), os.path.join(tmp, 'out.json')
    json.dump(pairs, open(jobs, 'w'))
    script = os.path.abspath(__file__) if '__file__' in globals() else sys.argv[sys.argv.index('--python') + 1]
    subprocess.run([bpy.app.binary_path, '-b', '--factory-startup', '--python', script, '--',
                    '--compare-worker', jobs, out], check=True, stdout=subprocess.DEVNULL)
    res = json.load(open(out))
    shutil.rmtree(tmp, ignore_errors=True)
    return res


def backup_and_write(path, datablocks):
    if os.path.exists(path):
        shutil.copy2(path, path + '1')                 # like a Blender save: the previous file becomes .blend1
    bpy.data.libraries.write(path, datablocks, fake_user=True, compress=True)


# ============================================================ renders ===========================================

RENDER_COLOURS = {'wall': (1.0, 0.95, 0.93, 1), 'brique': (0.68, 0.37, 0.38, 1), 'brique.001': (0.55, 0.55, 0.55, 1),
                  'wood': (0.65, 0.50, 0.37, 1), 'wood.001': (0.44, 0.34, 0.25, 1), 'metal': (0.60, 0.62, 0.66, 1),
                  'glass': (0.55, 0.80, 0.95, 0.35)}


def render_copy(data, rs, placed):
    """A copy of a mesh with local coloured materials of the same names (linked materials cannot be recoloured)."""
    me = data.copy()
    for i, m in enumerate(me.materials):
        key = m.name if m else 'wall'
        rm = bpy.data.materials.get('render_' + key)
        if rm is None:
            rm = bpy.data.materials.new('render_' + key)
            rm.diffuse_color = RENDER_COLOURS.get(key, (0.8, 0.2, 0.8, 1))
            if key == 'glass':
                try:
                    rm.surface_render_method = 'BLENDED'
                except (AttributeError, TypeError):
                    pass
        me.materials[i] = rm
    ob = bpy.data.objects.new('r_' + data.name, me)
    rs.collection.objects.link(ob)
    placed.append(ob)
    return ob


def render_windows(folder, modules, leaves_by_window):
    """Workbench, back-face culling on (Unity culls back faces): each window closed and open, from outside."""
    os.makedirs(folder, exist_ok=True)
    rs = bpy.data.scenes.new('PK_Render')
    rs.render.engine = 'BLENDER_WORKBENCH'
    sh = rs.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'MATERIAL'
    sh.show_backface_culling = True
    sh.show_cavity = True
    sh.show_object_outline = True
    rs.render.resolution_x, rs.render.resolution_y = 1400, 900
    rs.world = bpy.data.worlds.new('PK_World')
    rs.world.color = (0.55, 0.72, 0.92)
    cam = bpy.data.objects.new('PK_Cam', bpy.data.cameras.new('PK_Cam'))
    rs.collection.objects.link(cam)
    rs.camera = cam
    out = []
    for mod_name, leaves in leaves_by_window.items():
        for ang in (0, 45, SWING_MAX):
            placed = []
            render_copy(modules[mod_name].data, rs, placed)
            for lf in leaves.values():
                li = render_copy(lf['object'].data, rs, placed)
                li.location = lf['place_export']
                li.rotation_euler = (0, 0, math.radians(lf['open_sign'] * ang))
            cam.location = Vector((1.1, -2.6, 1.9))
            cam.rotation_euler = (Vector((0.0, 0.0, 1.25)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
            cam.data.lens = 40
            path = os.path.join(folder, '%s_open%02d.png' % (mod_name, ang))
            rs.render.filepath = path
            with bpy.context.temp_override(scene=rs):
                bpy.ops.render.render(write_still=True, scene=rs.name)
            out.append(path)
            for o in placed:
                me = o.data
                bpy.data.objects.remove(o, do_unlink=True)
                bpy.data.meshes.remove(me)
    return out


# ============================================================ main ==============================================

def main(args):
    do_fix, do_sashes = not args.no_fix, not args.no_sashes
    report = {'args': vars(args), 'modules': {}, 'missing_anchors': [], 'errors': [], 'files': {}, 'checks': {},
              'sashes': {}}
    src_mtime = os.path.getmtime(SRC)
    src = link_source()
    report.update({'objects': len(src['objects']), 'clusters': len(src['clusters']), 'plates': src['plates'],
                   'cleared_keyframes': src['cleared_keyframes'], 'moved_on_eval': src['moved_on_eval']})
    if len(src['clusters']) < 20 or len(src['clusters']) > 200:
        raise RuntimeError('clustering collapsed or exploded: %d clusters' % len(src['clusters']))

    # our extension pieces, appended (local copies): they are exported from here and written back with the sashes
    with bpy.data.libraries.load(EXT, link=False) as (df, dt):
        dt.collections = [EXT_COLLECTION]
    extcol = dt.collections[0]
    old_sashes = [o for o in extcol.objects if is_sash(o.name)]
    if do_sashes:                          # regenerated below from assets.blend; the old snapshot goes
        for o in old_sashes:                   # meshes too, fake user or not: the new ones take their names
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.meshes.remove(me)
    ext_names_before = sorted(o.name for o in extcol.objects)

    ex = bpy.data.scenes.new('PK_Export')
    modules, leaves_by_window = {}, {}
    for name, anchor, pivot_from, rotz, mode in SPEC:
        members = members_of(src, name, anchor, report)
        if name == 'PK_Floor_Upper_B' and not members:
            members = ['plank big.058']
            report['errors'].append('PK_Floor_Upper_B = copy of Upper_A (plank big.151 is gone)')
        if pivot_from:
            pivot_from = [p for p in pivot_from if p in src['objects']] or None
        if not members or len(members) > 200:
            report['errors'].append('%s: %d members, skipped' % (name, len(members)))
            continue
        info = {}
        if do_sashes and name in WINDOWS:
            try:
                bm, mats, log, info, leaves = build_window(src, name, WINDOWS[name], members, pivot_from, rotz, mode, do_fix)
                leaves_by_window[name] = leaves
            except RuntimeError as e:          # never a half-built window: the old module instead, and say so
                report['errors'].append(str(e))
                leaves_by_window.pop(name, None)
                bm = None
        else:
            bm = None
        if bm is None:
            bm, mats = gather(src, members)
            bm.transform(to_export_frame(pivot_of(src, members, pivot_from, mode), rotz))
            log = new_fix_log()
            log['faces_before'] = len(bm.faces)
            if do_fix:
                fix_normals(bm, log)
            log['faces_after'] = len(bm.faces)
        glass_slots = {i for i, m in enumerate(mats) if m and m.name == 'glass'}
        checks = {'inside_out_parts': inside_out_parts(bm)}
        if name in WALL_MODULES:
            checks['wall_grid'] = wall_grid(bm, glass_slots)
        ob = new_mesh_object(name, bm, mats)
        ex.collection.objects.link(ob)
        modules[name] = ob
        report['modules'][name] = {'members': len(members), 'fix': log, 'checks': checks, 'window': info or None,
                                   'faces': len(ob.data.polygons), 'mats': [m.name if m else None for m in mats]}
        if name in leaves_by_window:
            wb = [[round(min(v.co[i] for v in bm.verts), 4) for i in range(3)], [round(max(v.co[i] for v in bm.verts), 4) for i in range(3)]]
            old = info['old_module_bounds_export']
            checks['bounds_export'] = wb
            checks['bounds_equal_old_module'] = all(abs(wb[k][i] - old[k][i]) < 2e-4 for k in range(2) for i in range(3))
        bm_keep = bm.copy() if name in leaves_by_window else None
        bm.free()
        if bm_keep is not None:
            # sashes: objects, open sign, clash sweep, the closed window from both sides, placements
            leaves = leaves_by_window[name]
            closed = bm_keep.copy()
            for lname, lf in leaves.items():
                lf['open_sign'] = open_sign(lf['bm'])
                tmp = lf['bm'].copy()
                tmp.transform(Matrix.Translation(lf['place_export']))
                merge_bm(closed, tmp, len(mats))                          # wood, glass after the module's slots
                tmp.free()
            checks['swing'] = swing_check(bm_keep, leaves)
            checks['closed_window_grid'] = wall_grid(closed, {len(mats) + 1})
            closed.free()
            bm_keep.free()
            for lname, lf in leaves.items():
                lob = new_mesh_object(lname, lf['bm'], lf['mats'])
                ex.collection.objects.link(lob)
                lf['object'] = lob
                vs = [v.co for v in lf['bm'].verts]
                lmin = [min(v[i] for v in vs) for i in range(3)]
                lmax = [max(v[i] for v in vs) for i in range(3)]
                report['sashes'][lname] = {
                    'window_module': name, 'prefab_child': lf['child'], 'hinge': lf['hinge'],
                    'unity_local_position': unity_pos(lf['place_export']), 'unity_local_rotation_euler': [0, 0, 0],
                    'unity_local_scale': [1, 1, 1],
                    'unity_mesh_bounds_min': [round(-lmax[0] * UNITY_SCALE, 4), round(lmin[2] * UNITY_SCALE, 4), round(-lmax[1] * UNITY_SCALE, 4)],
                    'unity_mesh_bounds_max': [round(-lmin[0] * UNITY_SCALE, 4), round(lmax[2] * UNITY_SCALE, 4), round(-lmin[1] * UNITY_SCALE, 4)],
                    'opens_towards': '+z (outside), free edge first', 'panes': lf['panes'],
                    'fix': lf['log'], 'inside_out_parts': inside_out_parts(lf['bm']),
                    'faces': len(lob.data.polygons), 'src_y': lf['src_y'], 'src_z': lf['src_z'], 'hinge_src': lf['hinge_src'],
                    'place_export': [round(x, 5) for x in lf['place_export']], 'open_sign_export_z': lf['open_sign'],
                    'meeting_stile_trim': lf['meeting_stile_trim']}
                lf['bm'].free()
                lf['bm'] = None

    # leftover clusters nobody claimed (new pieces Pierre added?)
    claimed = set()
    for name, anchor, *_ in SPEC:
        claimed.update((cluster_of(src, anchor) if not isinstance(anchor, list) else anchor) or [])
    report['unclaimed_clusters'] = []
    for mem in src['clusters'].values():
        if any(m in claimed for m in mem):
            continue
        bbs = src['bbs']
        mn = [min(bbs[m][i] for m in mem) for i in range(3)]
        mx = [max(bbs[m][i + 3] for m in mem) for i in range(3)]
        report['unclaimed_clusters'].append({'n': len(mem), 'size': [round(mx[i] - mn[i], 2) for i in range(3)],
                                             'min': [round(x, 2) for x in mn], 'sample': sorted(mem)[:4]})
    report['unclaimed_clusters'].sort(key=lambda r: -r['n'])

    # export everything to a temporary folder first
    tmp = tempfile.mkdtemp(prefix='pk_export_')
    exported = []                                                  # (sub folder, name, temp path)
    for name, ob in modules.items():
        ob.location = (0, 0, 0)
        path = os.path.join(tmp, name + '.fbx')
        export_object(ob, ex, path)
        exported.append((PK_DIR, name, path))
    for leaves in leaves_by_window.values():
        for lname, lf in leaves.items():
            path = os.path.join(tmp, lname + '.fbx')
            export_object(lf['object'], ex, path)
            exported.append((PKX_DIR, lname, path))
    exx = bpy.data.scenes.new('PKX_Export')
    for o in sorted(extcol.objects, key=lambda o: o.name):
        if o.type != 'MESH' or not o.name.startswith('PKX_') or is_sash(o.name):
            continue                       # the sash snapshot is never an export source: build_window is
        if o.parent or o.rotation_euler.to_matrix() != Matrix.Identity(3) or tuple(o.scale) != (1.0, 1.0, 1.0):
            report['errors'].append('%s: not a plain object at identity rotation and scale, exported as is' % o.name)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        log = new_fix_log()
        log['faces_before'] = len(bm.faces)
        if do_fix:
            fix_normals(bm, log)
        log['faces_after'] = len(bm.faces)
        mats = [s.material for s in o.material_slots]
        checks = {'inside_out_parts': inside_out_parts(bm)}
        if o.name in WALL_MODULES:
            checks['wall_grid'] = wall_grid(bm, {i for i, m in enumerate(mats) if m and m.name == 'glass'})
        # the object keeps its exact name; its mesh steps aside while a fixed copy with the same name is exported
        orig, orig_name, loc = o.data, o.data.name, o.location.copy()
        orig.name = orig_name + '__src'
        me = bpy.data.meshes.new(orig_name)
        bm.to_mesh(me)
        bm.free()
        for m in mats:
            me.materials.append(m)
        if me.name != orig_name:
            raise RuntimeError('name clash on ' + orig_name)
        o.data = me
        o.location = (0, 0, 0)
        exx.collection.objects.link(o)
        path = os.path.join(tmp, o.name + '.fbx')
        export_object(o, exx, path)
        exx.collection.objects.unlink(o)
        o.data = orig
        o.location = loc
        bpy.data.meshes.remove(me)
        orig.name = orig_name
        exported.append((PKX_DIR, o.name, path))
        report['modules'][o.name] = {'fix': log, 'checks': checks, 'faces': log['faces_after'],
                                     'mats': [m.name if m else None for m in mats]}

    # the gate: with the normals pass on, every self-check must hold before anything is written
    failed = []
    for n, m in report['modules'].items():
        c = m['checks']
        if c['inside_out_parts']:
            failed.append('%s: %d inside-out parts' % (n, c['inside_out_parts']))
        if c.get('wall_grid', {}).get('one_sided'):
            failed.append('%s: %d one-sided samples' % (n, c['wall_grid']['one_sided']))
        if c.get('closed_window_grid', {}).get('one_sided'):
            failed.append('%s with its sashes: %d one-sided samples' % (n, c['closed_window_grid']['one_sided']))
        if 'swing' in c and c['swing']['max_pairs_0_to_%d' % SWING_MAX]:
            failed.append('%s: the sashes clash within %d degrees' % (n, SWING_MAX))
        if c.get('bounds_equal_old_module') is False:
            failed.append('%s: bounds differ from the old module' % n)
    for n, sd in report['sashes'].items():
        if sd['inside_out_parts']:
            failed.append('%s: %d inside-out parts' % (n, sd['inside_out_parts']))
    report['failed_checks'] = failed
    allowed = not (do_fix and failed) or args.force
    if not allowed:
        print('EXPORT_PIERREKIT_CHECKS_FAILED nothing written (use --force to override):', failed)

    # compare with what Unity has, write what changed
    pairs = [[sub + '/' + name, path, os.path.join(args.compare_root, sub, name + '.fbx')] for sub, name, path in exported]
    cmp = run_compare(pairs)
    for sub, name, path in exported:
        key = sub + '/' + name
        r = cmp[key]
        write = allowed and (args.all or r['status'] != 'unchanged')
        if write:
            dst = os.path.join(args.out_root, sub)
            os.makedirs(dst, exist_ok=True)
            shutil.copy2(path, os.path.join(dst, name + '.fbx'))
        r['written'] = write
        report['files'][key] = r
    shutil.rmtree(tmp, ignore_errors=True)

    if args.renders and leaves_by_window:
        report['renders'] = render_windows(args.renders, modules, leaves_by_window)

    # the derived blends
    if allowed and not args.no_blends:
        col = bpy.data.collections.new(MOD_COLLECTION)
        order = list(modules.values())
        for i, ob in enumerate(order):
            ob.location = Vector(((i % 7) * 5.0, -(i // 7) * 5.5, 0))
            col.objects.link(ob)
        written = {col}
        if leaves_by_window:
            scol = bpy.data.collections.new(SASH_COLLECTION)
            for mod_name, leaves in leaves_by_window.items():
                for lf in leaves.values():                     # in their window, closed
                    lf['object'].location = modules[mod_name].location + lf['place_export']
                    scol.objects.link(lf['object'])
            written.add(scol)
        backup_and_write(MODBLEND, written)
        report['modules_blend'] = {'path': MODBLEND, 'modules': len(order),
                                   'sashes': sum(len(v) for v in leaves_by_window.values())}
        if leaves_by_window:
            # the snapshot in assets_extension.blend: our local materials, parked beside the other pieces, closed
            local = {m.name: m for m in bpy.data.materials if m.library is None}
            for k, (mod_name, leaves) in enumerate(sorted(leaves_by_window.items())):
                for lf in leaves.values():
                    ob = lf['object']
                    for i, m in enumerate(ob.data.materials):
                        ob.data.materials[i] = local[m.name]
                    ob.location = Vector((k * 2.5, -20.0, 0)) + lf['place_export']
                    extcol.objects.link(ob)
            backup_and_write(EXT, {extcol})
            report['extension_blend'] = {'path': EXT, 'objects_before_sashes': ext_names_before,
                                         'objects_after': sorted(o.name for o in extcol.objects)}

    report['assets_blend_untouched'] = os.path.getmtime(SRC) == src_mtime
    summary = {'changed': sorted(k for k, r in report['files'].items() if r['status'] == 'changed'),
               'new': sorted(k for k, r in report['files'].items() if r['status'] == 'new'),
               'unchanged': sorted(k for k, r in report['files'].items() if r['status'] == 'unchanged'),
               'inside_out_parts_after': sum(m['checks']['inside_out_parts'] for m in report['modules'].values()),
               'wall_one_sided_samples': {n: m['checks']['wall_grid']['one_sided']
                                          for n, m in report['modules'].items() if 'wall_grid' in m['checks']},
               'errors': report['errors'], 'failed_checks': failed, 'written': allowed}
    for n, m in report['modules'].items():
        if 'swing' in m['checks']:
            summary.setdefault('swing_max_pairs', {})[n] = m['checks']['swing']['max_pairs_0_to_%d' % SWING_MAX]
            summary.setdefault('window_bounds_equal', {})[n] = m['checks']['bounds_equal_old_module']
            summary.setdefault('closed_window_one_sided', {})[n] = m['checks']['closed_window_grid']['one_sided']
    report['summary'] = summary
    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        json.dump(report, open(args.report, 'w'), indent=1, default=str)
    print('EXPORT_PIERREKIT', json.dumps(summary, default=str))
    print('EXPORT_PIERREKIT_DONE assets.blend untouched:', report['assets_blend_untouched'])


if __name__ == '__main__':
    _args = parse_args()
    if _args.compare_worker:
        compare_worker(*_args.compare_worker)
    else:
        main(_args)
