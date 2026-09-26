"""Pre-fracture the structural wall modules into chunk sets for the slice's structural destruction.

ADR-009 decision 6: a wall is swapped for pre-cut chunks the first time it is hurt, damage lands per chunk,
and a support graph brings down what nothing holds up. The chunks are cut here, in Blender, once; the game only
instantiates them (DESTRUCTION's DestructibleModule). This script is owner "Fracture" in
03_TECHNICAL/SLICE_ARCHITECTURE.md. It grew out of the A7 investigation prototype (fracture_module.py, 110
runs, 0 non-manifold chunks) and keeps its core: Voronoi cut per loose part, every cut capped with the part's
own material, closed manifold chunks, origin at the volume centroid, a JSON sidecar.

For each of the 13 modules in MODULES it writes two variants, PKF_<Module>_v1 and _v2 (different seeds, so
two neighbouring walls do not break the same way), each as:
  PKF_<Module>_v<k>.fbx    one object per chunk, <Module>_Chunk_NN, masonry first (bottom to top), then wood
  PKF_<Module>_v<k>.json   the sidecar: per chunk centroid, bounds, volume, mass_share, anchors, neighbours,
                           kind (masonry or frame), hull measures; the adjacency list; the validation
  PKF_<Module>_v<k>.fbx.meta  with --write-meta, when none exists: the intact module's .meta, new GUID

Run headless, no GUI and no MCP (never build in Pierre's open Blender):

    "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup
        --python tools/blender/fracture_modules.py -- [options]

Options after a bare `--`:
  --src DIR        a kit root holding PierreKit/ and PierreKit_Ext/; repeatable, the first one that has the module
                   wins (default: UnityProject/Assets/_Project/Art). The FBX Unity imports is the source, read only.
  --out DIR        where the PKF files go (default: UnityProject/Assets/_Project/Art/PierreKit_Fracture)
  --modules A B    only these modules
  --variants N     variants per module (default 2)
  --renders DIR    Workbench renders, back-face culling on: the intact module, the chunks in colour, the chunks
                   exploded, the exploded back with cut faces orange and wood notches yellow
  --report FILE    the summary JSON: every variant's validation and the round-trip check
  --write-meta     write the .meta of a new PKF FBX (existing .meta files are never touched)
  --no-verify      skip the round-trip re-import

Eight decisions worth reading before changing anything:

1. **Walls are cut through their thickness, denser in the middle.** Seeds lie on the wall's mid-plane, so every
   Voronoi plane is perpendicular to the wall and every cell goes through it. A warp sign(t)|t|^1.5 and a spacing
   that tightens towards the centre put small chunks in the middle and big ones at the edges. A seed is kept only
   where a ray through the thickness hits masonry, so no cell starts in a door or a window.

2. **Wood is never cut, glass is never exported.** Every wood part (wood, wood.001, metal: jambs, lintels,
   posts, the fixed window frame, the gable's beam and braces) is one chunk of kind "frame", whole. Glass stays a
   runtime GlassPane (the window glass is in KIT's sashes now; the gable window's pane stays in the intact module).
   Only masonry (wall, brique, brique.001) is cut, and every cut face takes the material of the part it cuts.

3. **Wood embedded in the masonry is taken out of it.** In the doors, the arch, the garage and the gables the
   wood sits 20 to 82 % inside the plaster (measured). Kept as it is, a wood chunk and the masonry chunks would
   overlap, and PhysX pushes overlapping bodies apart the moment one of them falls. So the masonry is cut by a
   boolean DIFFERENCE with the wood part's box (its two largest face directions and their cross product, sized
   to its vertices), not with the bevelled part itself: the bevels left plaster slivers a few mm thick. Each
   boolean is checked (closed result, volume removed between 0 and the box's), EXACT first then MANIFOLD, and
   the notch faces take the masonry's material. Slivers under 0.1 L (Blender) left by the box are dropped.

4. **A chunk's convex hull must not cover an opening.** DESTRUCTION gives every chunk a convex MeshCollider. A
   Voronoi cell that holds a corner of a door comes out L-shaped, and its hull would fill that corner of the door:
   a static collider that stops the door leaf, the sashes and the player. HullProbe measures it: at five depths
   through the slab, on a 1 cm grid, the hull area that is neither this chunk nor any other part and lies inside
   the wall's convex outline. A chunk over VOID_MAX_UNITY (20 cm2) is split on the plane of an opening face
   (shifted 0.3 mm into the opening), the split that leaves the least hull over openings wins, and small pieces
   are merged only into a neighbour that keeps the rule. Hull area over other chunks or the wood is measured too
   and avoided at a quarter of the weight; it only costs a push when a chunk falls.

5. **Stacks break at the mortar joints.** PKX_Corner_Quoin (7 blocks) and PKX_Chimney_Stack (16 blocks in 8
   courses) have no thin axis to cut through. Their chunks are whole blocks grouped by courses: one or two courses
   near the middle split into their blocks, pairs of courses merged at the ends until the count is in range, so
   every chunk is a convex slab or block. The joints are up to 2.7 cm wide (measured), so their contact tolerance
   is 3 cm instead of 1 cm.

6. **Sizes.** 8 to 15 masonry chunks per wall (4 to 6 quoin, 6 to 10 chimney). A masonry chunk is at least the
   module's floor wide in the wall plane (0.12 m, 0.18 in Unity; 0.10 for the arch and 0.045 for the garage,
   whose plaster is that narrow once the posts are out of it) and at least 20 % of the mean volume. Undersized
   cells lose their seed and are recut; undersized pieces merge into the neighbour sharing the most cut area; a
   sliver no neighbour can take without covering an opening is dropped (volume reported). Each variant tries up
   to 10 seed sets and keeps the best: in range, near the target, no hull over an opening, nothing undersized,
   no edge without exactly two faces, no zero-area triangle.

7. **Anchors test the core bounds, in Unity axes.** anchors.bottom / top / side_pos / side_neg say that a chunk
   touches, within 1 cm, the bottom, top, Unity +x or Unity -x face of the module's core: the big masonry parts,
   where the floor and the neighbour modules meet it. Not the full module bounds: the corner bricks overhang the
   wall end by 2.8 cm and the door threshold dips 4.3 cm below the floor, so no plaster chunk would ever touch
   those. Unity's x is Blender's -x.

8. **Export exactly like the intact module.** export_pierrekit.py's FBX call, one object per chunk at its
   centroid, identity rotation and scale. With the intact module's import settings (globalScale 1.5), the chunk
   root placed on the module at identity overlays it. Seeds come from crc32(module name) and the variant, so a
   re-run gives the same chunks; the GUID of a written .meta comes from the file name, so it is stable too.

Checks per variant (sidecar "validation", all must hold for "ok"): every chunk closed and manifold; chunk volume
within 3 % of the body (closed non-glass parts once the embedded wood is out of the masonry; the raw sum of parts
is reported too); masonry count in range; no masonry chunk under the size floor; adjacency graph connected and
every chunk connected to an anchor; hull over openings under VOID_MAX_UNITY; no glass material anywhere; cut
faces only in masonry materials. Then every FBX is re-imported in a fresh session and compared with the intact
module (bounds within 5 mm, chunk origins on the sidecar centroids, closed, same volume and triangles).

Validated 2026-09-26, Blender 5.1.2, sources: KIT's re-exported PK_Wall_Plain, _Interior, _Window_Small and
_Window_Big (no glass, no bars), the other 9 from the Unity Assets. 26 of 26 variants pass every check above,
in under a minute with renders (about 40 s without). Masonry chunks: 12 for most walls, 9 to 13 in all, 5 for the quoin, 8 for
the chimney; wood chunks: 3 to 10. Volume error -1.12 % (garage, slivers dropped) to +0.09 %, under 0.22 %
everywhere else. Hull over openings at most 19.8 cm2 per chunk (Unity); what is left is 1 cm lines along the
contact of the plaster with the wood or the sill, and pockets inside the wall where the wood's box took out more
than the bevelled wood fills. Convex hulls at most 92 triangles (Unity's limit for a convex MeshCollider is 255).
Round trip: every file re-imports closed, chunk origins within 7.7e-6 m of the sidecar, bounds within 0.3 mm of
the intact module except PKF_PKX_Wall_Garage_v1 (4.3 mm, a dropped 6 mm sliver of a corner brick's end). An FBX
parser outside Blender maps the files to Unity like the importer does: it reproduces the bounds Unity measures on
today's intact modules to 0.1 mm, and puts every chunk within 1e-5 m of its centroid_unity. DESTRUCTION's own
sidecar reader (ChunkSetData.Parse) reads all 26 sidecars in the editor. A second run gives identical sidecars.
"""
import bpy
import bmesh
import sys
import os
import json
import time
import math
import random
import argparse
import hashlib
import itertools
import zlib
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ART = os.path.join(ROOT, 'UnityProject', 'Assets', '_Project', 'Art')
OUT_FOLDER = 'PierreKit_Fracture'

# Every module: (name, kit folder, kind, min, max, target structural chunks, size floor in Blender m).
# The floor is the smallest caliper width of a masonry chunk in the wall plane (x1.5 in Unity). It is 0.12
# except where the masonry itself is narrower once the embedded wood is taken out of it (decision 3).
MODULES = [
    ('PK_Wall_Plain', 'PierreKit', 'wall', 8, 15, 12, 0.12),
    ('PK_Wall_Window_Small', 'PierreKit', 'wall', 8, 15, 12, 0.12),
    ('PK_Wall_Window_Big', 'PierreKit', 'wall', 8, 15, 12, 0.12),
    ('PK_Wall_Door', 'PierreKit', 'wall', 8, 15, 12, 0.12),
    ('PKX_Wall_Garage', 'PierreKit_Ext', 'wall', 8, 15, 10, 0.045),
    ('PK_Wall_Interior', 'PierreKit', 'wall', 8, 15, 12, 0.12),
    ('PKX_Wall_Int_Door', 'PierreKit_Ext', 'wall', 8, 15, 12, 0.12),
    ('PKX_Wall_Int_Arch', 'PierreKit_Ext', 'wall', 8, 15, 10, 0.10),
    ('PKX_Wall_Cellar', 'PierreKit_Ext', 'wall', 8, 15, 12, 0.12),
    ('PKX_Corner_Quoin', 'PierreKit_Ext', 'stack', 4, 6, 5, 0.12),
    ('PKX_Gable_4m', 'PierreKit_Ext', 'wall', 8, 15, 12, 0.12),
    ('PKX_Gable_4m_Window', 'PierreKit_Ext', 'wall', 8, 15, 12, 0.12),
    ('PKX_Chimney_Stack', 'PierreKit_Ext', 'stack', 6, 10, 8, 0.12),
]

FBX_SETTINGS = dict(use_selection=True, object_types={'MESH'}, apply_unit_scale=True, global_scale=1.0,
                    bake_space_transform=True, axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                    use_mesh_modifiers=True)      # exactly export_pierrekit.py's, so the chunks overlay the module

EPS = 1e-5            # bisect on-plane tolerance (m, Blender module space)
ON_PLANE = 2e-4       # boundary edges closer than this to the cut plane are capped
TOUCH = 0.0105        # 1 cm (+0.5 mm float slack, inclusive): contact tolerance for islands, adjacency and anchors
STACK_TOUCH = 0.03    # stacked blocks sit on mortar joints up to 2.7 cm wide (measured on PKX_Corner_Quoin)
CAP_NEAR = 0.003      # a cap face whose centre is within 3 mm of another chunk is a shared cut face
WELD = 1e-5           # 0.01 mm: coincident source verts are merged before cutting
RETRY_SHRINK = (0.0002, 0.0005, 0.001, 0.002)
COPLANAR_PUSH = 0.0004  # 0.4 mm (0.6 mm in Unity): anti z-fight where a brick crosses the slab on a cut plane
SPLIT_OFFSET = 0.0003   # an opening split plane sits 0.3 mm into the opening, never on the opening face itself
GRID = 0.01             # hull probe pitch (Blender m)
DEPTH_FRACS = (0.103, 0.307, 0.501, 0.699, 0.897)   # hull probe depths through the slab (off any face)
VOID_MAX_UNITY = 0.002  # m2: a masonry chunk's convex hull may cover at most this much opening (mean section)
OVER_MAX_UNITY = 0.02   # m2: hull over another chunk or the wood, above which a merge or a split is worth avoiding
OVER_WEIGHT = 0.25      # overlap counts a quarter of opening area when choosing a split or a merge
TAG_SPLIT = 20000       # cap tags: 1..19999 Voronoi planes, 20000.. opening splits, 30000 frame notch faces
TAG_NOTCH = 30000
SLIVER_VOL = 1e-4       # m3 (Blender): a masonry shell or island smaller than this is a boolean or Voronoi sliver
BOUNDS_TOL = 0.005      # m (Blender): chunks vs intact module bounds after re-import. A dropped sliver at the outline
                        # (the garage brick end, 4.3 mm) is the largest measured difference; a wrong transform is metres


def log(*a):
    print('[fracture]', *a, flush=True)


def parse_args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser(prog='fracture_modules')
    ap.add_argument('--src', action='append', default=[],
                    help='kit root holding PierreKit/ and PierreKit_Ext/ (repeatable, first match wins; default the Unity Art folder)')
    ap.add_argument('--out', default=os.path.join(ART, OUT_FOLDER), help='where PKF_*.fbx and .json go')
    ap.add_argument('--modules', nargs='*', default=None, help='subset of module names')
    ap.add_argument('--variants', type=int, default=2)
    ap.add_argument('--renders', default=None, help='folder for Workbench renders (back-face culling on)')
    ap.add_argument('--report', default=None, help='summary JSON of every variant and of the round-trip check')
    ap.add_argument('--write-meta', action='store_true',
                    help='write a .meta next to each new PKF FBX that has none: the intact module .meta, deterministic GUID')
    ap.add_argument('--no-verify', action='store_true', help='skip the round-trip re-import check')
    return ap.parse_args(argv)


# ============================================================ bmesh helpers (A7 prototype) ======================

def new_bm_like(has_uv):
    nb = bmesh.new()
    if has_uv:
        nb.loops.layers.uv.new('UVMap')
    nb.faces.layers.int.new('cut')
    return nb


def extract(src, faces):
    """Copy a set of faces of src into a new bmesh (keeps material, smooth flag, UVs, 'cut' tag)."""
    uvs = src.loops.layers.uv.active
    nb = new_bm_like(uvs is not None)
    uvd = nb.loops.layers.uv.active
    cs = src.faces.layers.int.get('cut'); cd = nb.faces.layers.int.get('cut')
    vmap = {}
    for f in faces:
        for v in f.verts:
            if v not in vmap:
                vmap[v] = nb.verts.new(v.co)
    dup = 0
    for f in faces:
        try:
            nf = nb.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            dup += 1
            continue
        nf.material_index = f.material_index; nf.smooth = f.smooth
        if cs is not None:
            nf[cd] = f[cs]
        if uvs is not None:
            for l, nl in zip(f.loops, nf.loops):
                nl[uvd].uv = l[uvs].uv
    nb.normal_update()
    return nb, dup


def face_components(bm):
    seen = set(); comps = []
    for f0 in bm.faces:
        if f0 in seen:
            continue
        seen.add(f0); stack = [f0]; comp = []
        while stack:
            f = stack.pop(); comp.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g not in seen:
                        seen.add(g); stack.append(g)
        comps.append(comp)
    return comps


def signed_volume(bm):
    v = 0.0
    for f in bm.faces:
        cs = [x.co for x in f.verts]
        for i in range(1, len(cs) - 1):
            v += cs[0].dot(cs[i].cross(cs[i + 1]))
    return v / 6.0


def volume_centroid(bm):
    V = 0.0; C = Vector((0, 0, 0))
    for f in bm.faces:
        cs = [x.co for x in f.verts]
        for i in range(1, len(cs) - 1):
            a, b, c = cs[0], cs[i], cs[i + 1]
            d = a.dot(b.cross(c)) / 6.0
            V += d; C += d * (a + b + c) / 4.0
    if abs(V) < 1e-12:
        ps = [v.co for v in bm.verts]
        return V, sum(ps, Vector()) / max(1, len(ps))
    return V, C / V


def is_closed(bm):
    return len(bm.faces) > 0 and all(len(e.link_faces) == 2 for e in bm.edges)


def closed_when_triangulated(bm):
    """An n-gon that touches itself (a keyhole left by a dissolve or a boolean) looks closed, and only shows as an
    edge with four faces once triangulated. Tested on a copy."""
    t = bm.copy()
    bmesh.ops.triangulate(t, faces=t.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
    ok = is_closed(t)
    t.free()
    return ok


def bbox_of(points):
    mn = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    mx = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return mn, mx


def bbox_union(boxes):
    return (Vector((min(b[0].x for b in boxes), min(b[0].y for b in boxes), min(b[0].z for b in boxes))),
            Vector((max(b[1].x for b in boxes), max(b[1].y for b in boxes), max(b[1].z for b in boxes))))


def boxes_touch(a, b, m=TOUCH):
    return all(a[0][i] - m <= b[1][i] and b[0][i] - m <= a[1][i] for i in range(3))


def planar_basis(n):
    t1 = n.orthogonal().normalized(); t2 = n.cross(t1).normalized()
    return t1, t2


# ============================================================ parts ============================================

def make_part(bm, pid, mats):
    area = {}
    for f in bm.faces:
        area[f.material_index] = area.get(f.material_index, 0.0) + f.calc_area()
    mat = max(area, key=area.get) if area else 0
    ps = [v.co.copy() for v in bm.verts]
    mn, mx = bbox_of(ps)
    big = max(bm.faces, key=lambda f: f.calc_area())
    n = big.normal.copy(); c = big.calc_center_median()
    sheet = n.length > 0.5 and max(abs((p - c).dot(n)) for p in ps) < 1e-4
    name = mats[mat].name if mats[mat] else ''
    low = name.lower()
    role = 'glass' if any(k in low for k in ('glass', 'verre', 'vitre')) else \
        ('frame' if low.startswith('wood') or low.startswith('metal') else 'masonry')
    return {'id': pid, 'bm': bm, 'mat': mat, 'mat_name': name, 'bbox': (mn, mx), 'closed': is_closed(bm),
            'sheet': sheet, 'glass': role == 'glass', 'role': role,
            'vol': signed_volume(bm), 'faces': len(bm.faces), 'repaired': False, 'normals_fixed': False, 'dropped': None}


def refresh_part(p):
    ps = [v.co.copy() for v in p['bm'].verts]
    p['cos'] = ps
    p['bbox'] = bbox_of(ps)
    p['center'] = (p['bbox'][0] + p['bbox'][1]) * 0.5
    p['radius'] = max((q - p['center']).length for q in ps)
    p['closed'] = is_closed(p['bm'])
    p['vol'] = signed_volume(p['bm'])


def part_like(comp):
    """A clip-able part dict around one chunk component (used by the opening splits)."""
    ps = comp['pts']
    bb = comp['bbox']; c = (bb[0] + bb[1]) * 0.5
    return {'id': comp['part'], 'vol': comp['part_vol'], 'bm': comp['bm'], 'mat': comp['mat'], 'glass': False, 'closed': comp['closed'],
            'center': c, 'radius': max((q - c).length for q in ps), 'cos': ps}


# ============================================================ clipping ========================================

def clip_part(p, planes, stats, shrink=0.0):
    """Intersection of loose part p with the convex region {x : (x - c).n <= 0 for every (c, n, tag)}; None if empty.
    Every cut is capped with the part's own material (flat, planar UVs, 'cut' = tag). shrink > 0 pulls every plane
    towards the kept side (used to retry a cut that grazed a vertex)."""
    live = []
    for c, n, tag in planes:
        c = c - n * shrink
        dc = (p['center'] - c).dot(n)
        if dc + p['radius'] <= EPS:
            continue
        if dc - p['radius'] >= -EPS:
            return None
        ds = [(q - c).dot(n) for q in p['cos']]
        if max(ds) <= EPS:
            continue
        if min(ds) >= -EPS:
            return None
        live.append((c, n, tag))
    bm = p['bm'].copy()
    if not live:
        return bm
    cutl = bm.faces.layers.int.get('cut')
    uvl = bm.loops.layers.uv.active
    for c, n, tag in live:
        ds = [(v.co - c).dot(n) for v in bm.verts]
        if not ds:
            bm.free(); return None
        if max(ds) <= EPS:
            continue
        if min(ds) >= -EPS:
            bm.free(); return None
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, dist=EPS, plane_co=c, plane_no=n, clear_outer=True, clear_inner=False)
        stats['bisect'] += 1
        loose_e = [e for e in bm.edges if not e.link_faces]
        if loose_e:
            bmesh.ops.delete(bm, geom=loose_e, context='EDGES')
        loose_v = [v for v in bm.verts if not v.link_edges]
        if loose_v:
            bmesh.ops.delete(bm, geom=loose_v, context='VERTS')
        if not bm.faces:
            bm.free(); return None
        if not p['closed']:
            continue
        bnd = [e for e in bm.edges if e.is_boundary and abs((e.verts[0].co - c).dot(n)) < ON_PLANE
               and abs((e.verts[1].co - c).dot(n)) < ON_PLANE]
        if not bnd:
            continue
        res = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=bnd, normal=n)
        new = [g for g in res['geom'] if isinstance(g, bmesh.types.BMFace)]
        rest = [e for e in bm.edges if e.is_boundary and abs((e.verts[0].co - c).dot(n)) < ON_PLANE
                and abs((e.verts[1].co - c).dot(n)) < ON_PLANE]
        if rest:
            stats['cap_fail'] += 1
            hf = bmesh.ops.holes_fill(bm, edges=rest, sides=0)
            new += hf['faces']
            stats['holes_fill_fallback'] = stats.get('holes_fill_fallback', 0) + len(hf['faces'])
        t1, t2 = planar_basis(n)
        for f in new:
            f.normal_update()
            if f.normal.dot(n) < 0:
                f.normal_flip()
            f.material_index = p['mat']; f.smooth = False
            f[cutl] = tag
            if uvl is not None:
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.dot(t1), l.vert.co.dot(t2))
        stats['caps'] += len(new)
    return bm


def clip_closed(p, planes, stats):
    """clip_part, retried in a region shrunk by a fraction of a millimetre if a cut grazed a vertex and the piece
    came out open (leaves a hairline gap, never an overlap)."""
    piece = clip_part(p, planes, stats)
    if piece is not None and p['closed'] and not (is_closed(piece) and closed_when_triangulated(piece)):
        for sh in RETRY_SHRINK:
            piece.free()
            stats['retries'] = stats.get('retries', 0) + 1
            piece = clip_part(p, planes, stats, shrink=sh)
            if piece is None or (is_closed(piece) and closed_when_triangulated(piece)):
                stats['retry_ok'] = stats.get('retry_ok', 0) + 1
                break
        else:
            stats['retry_failed'] = stats.get('retry_failed', 0) + 1
    return piece


COMP_UID = itertools.count()


def make_comp(bm, p, cell, group):
    ps = [v.co.copy() for v in bm.verts]
    bb = bbox_of(ps)
    cutl = bm.faces.layers.int.get('cut')
    caps = [(f.calc_center_median().copy(), f.calc_area(), f.normal.copy()) for f in bm.faces if f[cutl] != 0]
    step = max(1, len(ps) // 300)
    return {'uid': next(COMP_UID), 'bm': bm, 'part': p['id'], 'part_vol': p['vol'], 'mat': p['mat'], 'glass': False,
            'cell': cell, 'group': group,
            'bbox': bb, 'vol': signed_volume(bm), 'closed': is_closed(bm), 'bvh': BVHTree.FromBMesh(bm),
            'caps': caps, 'sample': ps[::step], 'pts': ps}


def split_piece(bm):
    comps = face_components(bm)
    if len(comps) == 1:
        return [bm]
    out = []
    for fs in comps:
        nb, _ = extract(bm, fs)
        out.append(nb)
    bm.free()
    return out


def resolve_coplanar_caps(raw, plane_of, stats):
    """Two parts that interpenetrate (a corner brick wrapped around the slab) get coplanar caps on the same cut
    plane: they would z-fight. The larger part's cap is pushed back by COPLANAR_PUSH so the smaller, enclosing
    part (brick) shows on the broken face. plane_of(tag) -> (c, n outward) or None."""
    by_tag = {}
    for k, (bm, p) in enumerate(raw):
        cutl = bm.faces.layers.int.get('cut')
        for tag in {f[cutl] for f in bm.faces if f[cutl] != 0}:
            by_tag.setdefault(tag, []).append(k)
    for tag, ks in by_tag.items():
        if len(ks) < 2:
            continue
        pl = plane_of(tag)
        if pl is None:
            continue
        c, n = pl
        t1, t2 = planar_basis(n)
        rect = {}
        for k in ks:
            bm = raw[k][0]; cutl = bm.faces.layers.int.get('cut')
            pts = [v.co.copy() for f in bm.faces if f[cutl] == tag for v in f.verts]
            us = [q.dot(t1) for q in pts]; vs = [q.dot(t2) for q in pts]
            rect[k] = (min(us), max(us), min(vs), max(vs))
        losers = set()
        for a in ks:
            for b in ks:
                if a >= b or raw[a][1]['id'] == raw[b][1]['id']:
                    continue
                ra, rb = rect[a], rect[b]
                ou = min(ra[1], rb[1]) - max(ra[0], rb[0]); ov = min(ra[3], rb[3]) - max(ra[2], rb[2])
                if ou > 1e-4 and ov > 1e-4:
                    losers.add(a if abs(raw[a][1]['vol']) > abs(raw[b][1]['vol']) else b)
        for k in losers:
            bm = raw[k][0]
            for v in bm.verts:
                if abs((v.co - c).dot(n)) < ON_PLANE:
                    v.co -= n * COPLANAR_PUSH
            bm.normal_update()
            stats['coplanar_pushed'] = stats.get('coplanar_pushed', 0) + 1


def voronoi(parts, seeds, tag_base, group, stats):
    cells = [[] for _ in seeds]
    raw = [[] for _ in seeds]
    for p in parts:
        for i in range(len(seeds)):
            si = seeds[i]; planes = []
            for j, sj in enumerate(seeds):
                if j == i:
                    continue
                d = sj - si
                if d.length < 1e-9:
                    continue
                n = d.normalized()
                planes.append(((si + sj) * 0.5, n, tag_base + j + 1))
            piece = clip_closed(p, planes, stats)
            if piece is None:
                continue
            for cb in split_piece(piece):
                raw[i].append((cb, p))
    for i in range(len(seeds)):
        if len(raw[i]) > 1:
            def plane_of(tag, i=i):
                j = tag - tag_base - 1
                if j < 0 or j >= len(seeds) or j == i:
                    return None
                return (seeds[i] + seeds[j]) * 0.5, (seeds[j] - seeds[i]).normalized()
            resolve_coplanar_caps(raw[i], plane_of, stats)
        for cb, p in raw[i]:
            cells[i].append(make_comp(cb, p, i, group))
    return cells


def comps_touch(a, b, tol=TOUCH):
    if not boxes_touch(a['bbox'], b['bbox'], tol):
        return False
    if a['bvh'].overlap(b['bvh']):
        return True
    for q in a['sample']:
        if b['bvh'].find_nearest(q, tol)[0] is not None:
            return True
    for q in b['sample']:
        if a['bvh'].find_nearest(q, tol)[0] is not None:
            return True
    return False


def shared_cut_area(A, B, tol=TOUCH):
    """Area of cap faces of chunk A lying on chunk B (and vice versa), averaged. A, B = lists of comps."""
    bbA = bbox_union([c['bbox'] for c in A]); bbB = bbox_union([c['bbox'] for c in B])
    if not boxes_touch(bbA, bbB, tol):
        return 0.0, False

    def one_way(X, Y):
        s = 0.0
        for x in X:
            for (ctr, ar, nrm) in x['caps']:
                for y in Y:
                    if not boxes_touch(x['bbox'], y['bbox']):
                        continue
                    if y['bvh'].find_nearest(ctr, CAP_NEAR)[0] is not None:
                        s += ar; break
        return s
    a1 = one_way(A, B); a2 = one_way(B, A)
    touch = a1 > 0 or a2 > 0 or any(comps_touch(x, y, tol) for x in A for y in B)
    return 0.5 * (a1 + a2), touch


def islands(comps, tol=TOUCH):
    n = len(comps); par = list(range(n))

    def f(x):
        while par[x] != x:
            par[x] = par[par[x]]; x = par[x]
        return x
    for i in range(n):
        for j in range(i + 1, n):
            if f(i) != f(j) and comps_touch(comps[i], comps[j], tol):
                par[f(i)] = f(j)
    groups = {}
    for i in range(n):
        groups.setdefault(f(i), []).append(comps[i])
    return list(groups.values())


# ============================================================ seeds ============================================

def gen_seeds(N, rng, lo, hi, axes, fixed, gamma, ok_fn, shrink=0.94):
    """Jittered seeds over the box lo..hi on the given axes (fixed = {axis: value}), denser in the middle."""
    c = (lo + hi) * 0.5; h = (hi - lo) * 0.5 * shrink

    def warp(t):
        return math.copysign(abs(t) ** gamma, t)
    ok = tot = 0
    grid = 14
    for k in range(grid ** len(axes)):
        p = c.copy(); r = k
        for ax in axes:
            p[ax] = lo[ax] + (hi[ax] - lo[ax]) * ((r % grid) + 0.5) / grid; r //= grid
        for ax, val in fixed.items():
            p[ax] = val
        tot += 1; ok += 1 if ok_fn(p) else 0
    meas = max(0.05, ok / tot)
    for ax in axes:
        meas *= (hi[ax] - lo[ax])
    rmin = 0.8 * (meas / N) ** (1.0 / len(axes))
    seeds = []; tries = 0; fails = 0
    while len(seeds) < N and tries < 40000:
        tries += 1
        p = c.copy()
        for ax in axes:
            p[ax] = c[ax] + h[ax] * warp(rng.uniform(-1, 1))
        for ax, val in fixed.items():
            p[ax] = val
        if not ok_fn(p):
            continue
        rel = max(abs(p[ax] - c[ax]) / max(h[ax], 1e-6) for ax in axes)
        rr = rmin * (0.55 + 0.45 * rel)      # tighter spacing near the middle
        if any((p - s).length < rr for s in seeds):
            fails += 1
            if fails > 300:
                rmin *= 0.9; fails = 0
            continue
        seeds.append(p)
    if len(seeds) < min(2, N):
        return gen_seeds(N, rng, lo, hi, axes, fixed, gamma, lambda q: True, shrink)
    return seeds


# ============================================================ chunk metrics ====================================

def width_dirs(axes):
    """Directions for the caliper width: in the wall plane (72 steps) or over the sphere (3D)."""
    if len(axes) == 2:
        out = []
        for k in range(72):
            a = math.pi * k / 72; d = Vector((0, 0, 0)); d[axes[0]] = math.cos(a); d[axes[1]] = math.sin(a)
            out.append(d)
        return out
    out = []; n = 200; g = math.pi * (3 - math.sqrt(5))
    for k in range(n):
        z = 1 - (k + 0.5) / n; r = math.sqrt(max(0.0, 1 - z * z))
        if z < 0:
            break
        out.append(Vector((r * math.cos(g * k), r * math.sin(g * k), z)))
    return out


def min_width(pts, dirs):
    best = 1e9
    for d in dirs:
        ds = [p.dot(d) for p in pts]
        best = min(best, max(ds) - min(ds))
    return best


def chunk_stats(comps, axes):
    bb = bbox_union([c['bbox'] for c in comps])
    vol = sum(c['vol'] for c in comps)
    pts = [q for c in comps for q in c['pts']]
    ext = min_width(pts, width_dirs(axes))
    return vol, ext, bb, (bb[0] + bb[1]) * 0.5


# ============================================================ convex hull probe ================================

def section_segments(bm, t_ax, t, axes):
    """Segments of the section of a closed mesh by the plane x[t_ax] = t, in the plane axes (numpy K x 4)."""
    segs = []
    for f in bm.faces:
        vs = f.verts; n = len(vs)
        ds = [v.co[t_ax] - t for v in vs]
        if all(d < 0 for d in ds) or all(d >= 0 for d in ds):
            continue
        pts = []
        for k in range(n):
            a, b = ds[k], ds[(k + 1) % n]
            if (a < 0) != (b < 0):
                s = a / (a - b)
                p = vs[k].co.lerp(vs[(k + 1) % n].co, s)
                pts.append((p[axes[0]], p[axes[1]]))
        if len(pts) == 2:
            segs.append(pts[0] + pts[1])
        elif len(pts) > 2:
            nrm = f.normal
            dvec = Vector((0, 0, 0)); dvec[t_ax] = 1.0
            line = nrm.cross(dvec)
            lu, lv = line[axes[0]], line[axes[1]]
            pts.sort(key=lambda q: q[0] * lu + q[1] * lv)
            for k in range(0, len(pts) - 1, 2):
                segs.append(pts[k] + pts[k + 1])
    return np.array(segs, dtype=np.float64).reshape(-1, 4)


def inside_eo(P, S):
    """Even-odd point in polygon for points P (N x 2) against a closed segment soup S (K x 4)."""
    if len(P) == 0 or len(S) == 0:
        return np.zeros(len(P), dtype=bool)
    out = np.zeros(len(P), dtype=bool)
    for s0 in range(0, len(P), 3000):
        x = P[s0:s0 + 3000, 0:1]; y = P[s0:s0 + 3000, 1:2]
        x0, y0, x1, y1 = S[:, 0], S[:, 1], S[:, 2], S[:, 3]
        cond = (y0 > y) != (y1 > y)
        with np.errstate(divide='ignore', invalid='ignore'):
            xi = x0 + (y - y0) * (x1 - x0) / (y1 - y0)
        cross = cond & (x < xi)
        out[s0:s0 + 3000] = (cross.sum(axis=1) % 2) == 1
    return out


def hull_of(points):
    """Convex hull of a point set: (face planes N x 3, offsets, volume, triangle count)."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in points]
    res = bmesh.ops.convex_hull(bm, input=vs, use_existing_faces=False)
    kill = list({g for g in res['geom_interior'] + res['geom_unused'] if isinstance(g, bmesh.types.BMVert)})
    if kill:
        bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bm.normal_update()
    vol = signed_volume(bm)
    if vol < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.normal_update(); vol = -vol
    N = []; D = []
    for f in bm.faces:
        n = f.normal
        if n.length < 0.5:
            continue
        N.append(tuple(n)); D.append(n.dot(f.verts[0].co))
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    bm.free()
    return np.array(N), np.array(D), vol, tris


class HullProbe:
    """Measures how much of a chunk's convex hull is not the chunk: the runtime gives every chunk a convex
    MeshCollider, so hull area over an opening is a collider that blocks a door, a sash or the player."""

    def __init__(self, t_ax, axes, band, clo, chi, solids, core):
        self.t_ax = t_ax; self.axes = axes
        # an opening is empty space inside the convex outline of the wall body: above a gable's slope or past the
        # module's end it is the roof's or the neighbour's space, not an opening
        self.CN, self.CD, _, _ = hull_of([q for p in core for q in p['cos']])
        self.depths = [band[0] + (band[1] - band[0]) * f for f in DEPTH_FRACS]
        self.lo = np.array([clo[axes[0]], clo[axes[1]]]); self.hi = np.array([chi[axes[0]], chi[axes[1]]])
        self.solids = [(p['bbox'], [section_segments(p['bm'], t_ax, d, axes) for d in self.depths]) for p in solids]
        self.cache = {}

    def segs(self, comp, di):
        key = (comp['uid'], di)          # never id(): a freed bmesh's id is reused by the next one
        s = self.cache.get(key)
        if s is None:
            s = section_segments(comp['bm'], self.t_ax, self.depths[di], self.axes)
            self.cache[key] = s
        return s

    def measure(self, comps):
        pts = [q for c in comps for q in c['pts']]
        N, D, hvol, htris = hull_of(pts)
        a0, a1 = self.axes
        if len(N) < 4:
            return {'void': 0.0, 'void_max': 0.0, 'overlap': 0.0, 'hull_vol': hvol, 'hull_tris': htris}
        mn = np.array([min(p[a0] for p in pts), min(p[a1] for p in pts)])
        mx = np.array([max(p[a0] for p in pts), max(p[a1] for p in pts)])
        mn = np.maximum(mn, self.lo); mx = np.minimum(mx, self.hi)
        void = []; over = []; wh = []
        if np.any(mx - mn <= GRID):
            return {'void': 0.0, 'void_max': 0.0, 'overlap': 0.0, 'hull_vol': hvol, 'hull_tris': htris}
        us = np.arange(mn[0] + GRID * 0.5, mx[0], GRID); vs = np.arange(mn[1] + GRID * 0.5, mx[1], GRID)
        U, V = np.meshgrid(us, vs); P2 = np.stack([U.ravel(), V.ravel()], axis=1)
        for di, t in enumerate(self.depths):
            P3 = np.zeros((len(P2), 3)); P3[:, a0] = P2[:, 0]; P3[:, a1] = P2[:, 1]; P3[:, self.t_ax] = t
            inh = np.all(P3 @ N.T - D <= 1e-7, axis=1)
            Q = P2[inh]
            if len(Q) == 0:
                void.append(0); over.append(0); continue
            inc = np.zeros(len(Q), dtype=bool)
            for c in comps:
                inc |= inside_eo(Q, self.segs(c, di))
            R = Q[~inc]
            occ = np.zeros(len(R), dtype=bool)
            if len(R):
                for bb, ss in self.solids:
                    if bb[1][a0] < mn[0] or bb[0][a0] > mx[0] or bb[1][a1] < mn[1] or bb[0][a1] > mx[1]:
                        continue
                    occ |= inside_eo(R, ss[di])
            R3 = np.zeros((len(R), 3)); R3[:, a0] = R[:, 0]; R3[:, a1] = R[:, 1]; R3[:, self.t_ax] = t
            inside_outline = np.all(R3 @ self.CN.T - self.CD <= 1e-7, axis=1) if len(R) else np.zeros(0, dtype=bool)
            vm = ~occ & inside_outline
            void.append(int(vm.sum())); over.append(int(occ.sum()))
            for q in R3[vm]:
                wh.append(q)
        a = GRID * GRID
        where = None
        if wh:
            W = np.array(wh)
            where = (Vector(W.min(axis=0)), Vector(W.max(axis=0)))
        return {'void': a * sum(void) / len(void), 'void_max': a * max(void), 'overlap': a * sum(over) / len(over), 'where': where,
                'hull_vol': hvol, 'hull_tris': htris}


def candidate_planes(core_parts, t_ax, axes, clo, chi):
    """Planes of the core faces that stand across the wall (the sides of openings and notches), shifted 0.3 mm
    into the opening. Splitting a chunk on them removes the corners that make its hull cover the opening."""
    groups = {}
    corners = [Vector((x, y, z)) for x in (clo.x, chi.x) for y in (clo.y, chi.y) for z in (clo.z, chi.z)]
    for p in core_parts:
        for f in p['bm'].faces:
            n = f.normal.copy(); n[t_ax] = 0.0
            if n.length < 0.95:
                continue
            n.normalize()
            c = f.calc_center_median()
            d = n.dot(c)
            key = (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(d, 3))
            g = groups.setdefault(key, {'n': n, 'd': d, 'area': 0.0, 'c': c})
            g['area'] += f.calc_area()
    out = []
    for g in groups.values():
        if g['area'] < 0.002:
            continue
        n, d = g['n'], g['d']
        sd = [n.dot(q) - d for q in corners]
        if min(sd) > -0.002 or max(sd) < 0.002:
            continue                    # the module outline: nothing on the far side
        c = n * (d + SPLIT_OFFSET)
        out.append((c, n, g['area']))
    out.sort(key=lambda g: -g[2])
    return [(c, n) for c, n, _ in out[:32]]


def split_comps(comps, c, n, tag, stats):
    """Cut a chunk (list of comps) by one plane; returns the comps on each side."""
    sides = ([], [])
    raw = ([], [])
    for comp in comps:
        ds = [(q - c).dot(n) for q in comp['pts']]
        if max(ds) <= 1e-4:
            sides[0].append(comp); continue
        if min(ds) >= -1e-4:
            sides[1].append(comp); continue
        tp = part_like(comp)
        for k, nn in ((0, n), (1, -n)):
            piece = clip_closed(tp, [(c, nn, tag)], stats)
            if piece is None:
                continue
            for cb in split_piece(piece):
                raw[k].append((cb, tp))
    for k, nn in ((0, n), (1, -n)):
        if len(raw[k]) > 1:
            resolve_coplanar_caps(raw[k], lambda t, nn=nn: (c, nn) if t == tag else None, stats)
        for cb, tp in raw[k]:
            sides[k].append(make_comp(cb, tp, -1, 'structure'))
    return sides


def crosses(comps, c, n, margin=0.002):
    ds = [(q - c).dot(n) for comp in comps for q in comp['pts']]
    return min(ds) < -margin and max(ds) > margin


# ============================================================ wall fracture ====================================

class WallJob:
    def __init__(self, masonry, core, t_ax, axes, clo, chi, band, floor, lo_n, hi_n, target, probe, planes, stats):
        self.masonry = masonry; self.core = core; self.t_ax = t_ax; self.axes = axes
        self.clo = clo; self.chi = chi; self.band = band; self.floor = floor
        self.lo_n = lo_n; self.hi_n = hi_n; self.target = target
        self.probe = probe; self.planes = planes; self.stats = stats
        self.split_tag = TAG_SPLIT
        self.min_frac = 0.2
        self.dropped_vol = 0.0
        self.s2 = 2.25
        self.has_uv = any(p['bm'].loops.layers.uv.active is not None for p in masonry)
        self.frame_bvh = []

    def void_u(self, comps):
        return self.probe.measure(comps)['void'] * self.s2 if self.probe else 0.0     # Unity m2

    def hull_u(self, comps):
        """(opening area, overlap area) under the chunk's convex hull, Unity m2."""
        if not self.probe:
            return 0.0, 0.0
        m = self.probe.measure(comps)
        return m['void'] * self.s2, m['overlap'] * self.s2

    def hidden_by_wood(self, comps):
        """A sliver left between the masonry and a wood part's box: nine points in ten within 1.5 cm of the wood."""
        if not self.frame_bvh:
            return False
        pts = [q for c in comps for q in c['sample']]
        near = sum(1 for q in pts if any(b.find_nearest(q, 0.015)[0] is not None for b in self.frame_bvh))
        return near >= 0.9 * len(pts)

    def _clean_merges(self, chunks, k):
        """Every touching neighbour, as (None, index, shared cut area, opening area after the merge, 0)."""
        out = []
        for m, other in enumerate(chunks):
            if m == k:
                continue
            area, touch = shared_cut_area(chunks[k]['comps'], other['comps'])
            if not touch:
                continue
            v, o = self.hull_u(chunks[k]['comps'] + other['comps'])
            out.append((None, m, area, v, 0.0))
        return out

    def small(self, comps, mean):
        vol, ext, _, _ = chunk_stats(comps, self.axes)
        return vol < self.min_frac * mean or ext < self.floor

    def convex_fix(self, comps, mean, depth=0, log_=None):
        """Split a chunk on the planes of opening faces while its hull covers more than VOID_MAX_UNITY of opening (or
        more than OVER_MAX_UNITY of wood or other chunks). The best split, scored on what the pieces' hulls still cover,
        must cut that by a fifth at least; pieces are split again, three levels at most."""
        v, o = self.hull_u(comps)
        if (v <= VOID_MAX_UNITY and o <= OVER_MAX_UNITY) or depth >= 3:
            return [comps]
        f0 = v + OVER_WEIGHT * o
        best = None
        for c, n in self.planes:
            if not crosses(comps, c, n):
                continue
            self.split_tag += 1
            A, B = split_comps(comps, c, n, self.split_tag, self.stats)
            pieces = []
            for side in (A, B):
                if side:
                    pieces += islands(side)
            if len(pieces) < 2:
                continue
            score = 0.0; n_small = 0
            for pc in pieces:
                pv, po = self.hull_u(pc)
                score += pv + OVER_WEIGHT * po
                if self.small(pc, mean):
                    n_small += 1
            # a split that leaves pieces under the floor only pays for itself against an opening, not an overlap
            if v <= VOID_MAX_UNITY and n_small:
                continue
            score += n_small * VOID_MAX_UNITY * 0.5
            if best is None or score < best[0]:
                best = (score, pieces, (list(c), list(n)))
        if best is None or best[0] >= f0 * 0.8:
            return [comps]
        if log_ is not None:
            log_.append({'void_before_unity': round(v, 5), 'overlap_before_unity': round(o, 5),
                         'plane_n': [round(x, 3) for x in best[2][1]], 'plane_c': [round(x, 4) for x in best[2][0]],
                         'pieces': len(best[1]), 'score_after_unity': round(best[0], 5)})
        out = []
        for pc in best[1]:
            out += self.convex_fix(pc, mean, depth + 1, log_)
        return out

    def merge_small(self, chunks, report):
        """Merge chunks under the size floor into the touching neighbour whose merge keeps the hull off the
        openings, then shares the most cut area. A piece that no neighbour takes cleanly is dropped if it is a
        sliver, and kept as its own (undersized, reported) chunk otherwise: a small chunk is better than a
        convex collider across a door."""
        kept = set()
        while len(chunks) > 1:
            st = [chunk_stats(ch['comps'], self.axes) for ch in chunks]
            mean = sum(s[0] for s in st) / len(st)
            bad = [k for k, s in enumerate(st) if (s[0] < self.min_frac * mean or s[1] < self.floor) and id(chunks[k]) not in kept]
            if not bad:
                break
            k = min(bad, key=lambda q: st[q][0])
            best = None
            for m, other in enumerate(chunks):
                if m == k:
                    continue
                area, touch = shared_cut_area(chunks[k]['comps'], other['comps'])
                if not touch:
                    continue
                v, o = self.hull_u(chunks[k]['comps'] + other['comps'])
                o0 = self.hull_u(other['comps'])[1]
                do = max(0.0, o - o0)
                key = (v > VOID_MAX_UNITY, do > OVER_MAX_UNITY, v if v > VOID_MAX_UNITY else 0.0, do, -area)
                if best is None or key < best[0]:
                    best = (key, m, area, v, do)
            how = None
            thin = st[k][0] < SLIVER_VOL or (st[k][1] < 0.02 and st[k][0] < 10 * SLIVER_VOL)
            if not thin and best is not None and best[3] > VOID_MAX_UNITY:
                report.append({'kept': sorted(chunks[k]['cells']), 'vol': round(st[k][0], 6), 'extent': round(st[k][1], 4),
                               'how': 'undersized, but every merge would cover an opening: kept as its own chunk'})
                kept.add(id(chunks[k]))
                continue
            hidden = thin and self.hidden_by_wood(chunks[k]['comps'])
            if thin and not hidden and best is not None and best[3] <= VOID_MAX_UNITY:
                # a sliver in the open (a brick end a cell barely clipped): merged into the neighbour sharing the most
                # cut area among those that keep the hull off the openings, even over some overlap
                best = max((b for b in self._clean_merges(chunks, k) if b[3] <= VOID_MAX_UNITY), key=lambda b: b[2])
            elif thin and (hidden or best is None or best[3] > VOID_MAX_UNITY):
                # a sliver (plaster left in a bevel of the wood, a corner a cell barely clipped) that no neighbour can
                # take without its hull covering an opening: dropped, never glued far away
                report.append({'dropped': sorted(chunks[k]['cells']), 'vol': round(st[k][0], 7), 'extent': round(st[k][1], 4),
                               'bounds': [[round(x, 4) for x in st[k][2][0]], [round(x, 4) for x in st[k][2][1]]],
                               'how': 'sliver hidden by the wood: dropped' if hidden else
                                      'sliver, no neighbour takes it without covering an opening: dropped'})
                self.dropped_vol += st[k][0]
                chunks.pop(k)
                continue
            if best is None:
                m = min((m for m in range(len(chunks)) if m != k), key=lambda m: (st[m][3] - st[k][3]).length)
                how = 'nearest (no contact)'; v = self.void_u(chunks[k]['comps'] + chunks[m]['comps']); area = 0.0
            else:
                _, m, area, v, _ = best
                how = 'shared cut %.4f m2' % area
            report.append({'merged': sorted(chunks[k]['cells']), 'into': sorted(chunks[m]['cells']), 'vol': round(st[k][0], 6),
                           'extent': round(st[k][1], 4), 'how': how, 'hull_void_after_unity': round(v, 5)})
            chunks[m]['comps'] += chunks[k]['comps']; chunks[m]['cells'] |= chunks[k]['cells']
            chunks.pop(k)
        return chunks

    def run(self, seed_rng_base, ok_fn, fixed, gamma):
        best = None; attempts = []
        N = self.target
        for attempt in range(10):
            rng = random.Random(seed_rng_base * 1000 + attempt)
            self.dropped_vol = 0.0
            seeds = gen_seeds(N, rng, self.clo, self.chi, self.axes, fixed, gamma, ok_fn)
            pruned = 0
            while True:
                cells = voronoi(self.masonry, seeds, 0, 'structure', self.stats)
                keep = [i for i, c in enumerate(cells) if c]
                if len(keep) < len(seeds):
                    seeds = [seeds[i] for i in keep]; cells = [cells[i] for i in keep]
                st = [chunk_stats(c, self.axes) for c in cells]
                mean = sum(s[0] for s in st) / len(st)
                bad = [k for k, s in enumerate(st) if s[0] < self.min_frac * mean or s[1] < self.floor]
                if not bad or len(seeds) <= 2:
                    break
                k = min(bad, key=lambda q: st[q][0])
                seeds.pop(k); pruned += 1
            chunks = []; n_isl = 0
            for i, c in enumerate(cells):
                isl = islands(c)
                n_isl += len(isl)
                for comps in isl:
                    chunks.append({'comps': comps, 'cells': {i}})
            mean = sum(chunk_stats(ch['comps'], self.axes)[0] for ch in chunks) / len(chunks)
            splits = []
            fixed_chunks = []
            for ch in chunks:
                for comps in self.convex_fix(ch['comps'], mean, 0, splits):
                    fixed_chunks.append({'comps': comps, 'cells': set(ch['cells'])})
            merges = []
            fixed_chunks = self.merge_small(fixed_chunks, merges)
            cnt = len(fixed_chunks)
            vmax = max(self.void_u(ch['comps']) for ch in fixed_chunks)
            stf = [chunk_stats(ch['comps'], self.axes) for ch in fixed_chunks]
            mf = sum(x[0] for x in stf) / len(stf)
            n_under = sum(1 for x in stf if x[0] < self.min_frac * mf or x[1] < self.floor - 1e-6)
            defects = [chunk_defects(ch['comps'], self.has_uv) for ch in fixed_chunks]
            n_defect = sum(1 for d in defects if d[0])
            n_degen = sum(d[1] for d in defects)
            attempts.append({'attempt': attempt, 'seeds_asked': N, 'seeds_kept': len(seeds), 'pruned_seeds': pruned,
                             'islands': n_isl, 'opening_splits': len(splits), 'merges': len(merges), 'chunks': cnt,
                             'undersized': n_under, 'nonmanifold_chunks': n_defect, 'zero_area_tris': n_degen,
                             'max_hull_void_unity': round(vmax, 5)})
            score = 0 if self.lo_n <= cnt <= self.hi_n else min(abs(cnt - self.lo_n), abs(cnt - self.hi_n))
            score = score * 100 + abs(cnt - self.target) + (50 if vmax > VOID_MAX_UNITY else 0) + 30 * n_under + 200 * n_defect + 5 * n_degen
            res = {'seeds': seeds, 'chunks': fixed_chunks, 'cells': len(cells), 'islands': n_isl, 'pruned_seeds': pruned,
                   'merges': merges, 'splits': splits, 'dropped_vol': self.dropped_vol}
            if best is None or score < best[0]:
                best = (score, res)
            if self.lo_n <= cnt <= self.hi_n and abs(cnt - self.target) <= 1 and vmax <= VOID_MAX_UNITY and not n_under and not n_defect and not n_degen:
                break
            N = max(2, N + (self.target - cnt))
        best[1]['attempts'] = attempts
        return best[1]


# ============================================================ stack fracture ===================================

def stack_partition(blocks, lo_n, hi_n, target, rng, H, zmid, avoid):
    """Group whole blocks into chunks along the mortar joints: courses split into their blocks near the middle,
    pairs of courses merged at the ends. Returns a list of block lists."""
    blocks = sorted(blocks, key=lambda p: p['center'].z)
    courses = []
    for b in blocks:
        for c in courses:
            lo = max(min(x['bbox'][0].z for x in c), b['bbox'][0].z)
            hi = min(max(x['bbox'][1].z for x in c), b['bbox'][1].z)
            h = min(b['bbox'][1].z - b['bbox'][0].z, max(x['bbox'][1].z for x in c) - min(x['bbox'][0].z for x in c))
            if hi - lo > 0.5 * h:
                c.append(b); break
        else:
            courses.append([b])
    for _ in range(20):
        units = [{'blocks': list(c), 'split': False} for c in courses]

        def zc(u):
            return sum(b['center'].z for b in u['blocks']) / len(u['blocks'])
        splittable = [u for u in units if len(u['blocks']) > 1]
        n_split = min(len(splittable), rng.choice([1, 2])) if splittable else 0
        for _ in range(n_split):
            cand = [u for u in units if not u['split'] and len(u['blocks']) > 1]
            if not cand:
                break
            u = min(cand, key=lambda u: abs(zc(u) - zmid) + rng.uniform(0, 0.3 * H))
            i = units.index(u)
            units[i:i + 1] = [{'blocks': [b], 'split': True} for b in sorted(u['blocks'], key=lambda b: (b['center'].x, b['center'].y))]
        while len(units) > target:
            pairs = [i for i in range(len(units) - 1) if not units[i]['split'] and not units[i + 1]['split']]
            if not pairs:
                break
            i = max(pairs, key=lambda i: abs((zc(units[i]) + zc(units[i + 1])) * 0.5 - zmid) + rng.uniform(0, 0.35 * H))
            units[i:i + 2] = [{'blocks': units[i]['blocks'] + units[i + 1]['blocks'], 'split': False}]
        part = [sorted(b['id'] for b in u['blocks']) for u in units]
        if lo_n <= len(units) <= hi_n and part not in avoid:
            return units, part, len(courses)
    return units, part, len(courses)


# ============================================================ module =========================================

def read_unity_meta(module, folder):
    meta = os.path.join(ART, folder, module + '.fbx.meta')
    scale = None; text = None
    try:
        with open(meta, 'r', encoding='utf-8') as fh:
            text = fh.read()
        for line in text.splitlines():
            s = line.strip()
            if s.startswith('globalScale:'):
                scale = float(s.split(':')[1]); break
    except OSError:
        pass
    return scale, text, meta


def find_source(module, folder, roots):
    for r in roots:
        p = os.path.join(r, folder, module + '.fbx')
        if os.path.isfile(p):
            return p
    return None


def import_module(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx, use_custom_normals=False)
    src_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    mats, keys = [], []
    bm_all = bmesh.new()
    for o in src_objs:
        tmp = bmesh.new(); tmp.from_mesh(o.data); tmp.transform(o.matrix_world)
        if o.matrix_world.determinant() < 0:
            bmesh.ops.reverse_faces(tmp, faces=tmp.faces[:])
        remap = {}
        for k, s in enumerate(o.material_slots):
            key = s.material.name if s.material else '__none__'
            if key not in keys:
                keys.append(key); mats.append(s.material)
            remap[k] = keys.index(key)
        if not o.material_slots:
            if '__none__' not in keys:
                keys.append('__none__'); mats.append(None)
            remap = {0: keys.index('__none__')}
        for f in tmp.faces:
            f.material_index = remap.get(f.material_index, 0)
        me = bpy.data.meshes.new('_t'); tmp.to_mesh(me); tmp.free(); bm_all.from_mesh(me); bpy.data.meshes.remove(me)
    if not bm_all.faces.layers.int.get('cut'):
        bm_all.faces.layers.int.new('cut')
    return bm_all, mats, src_objs


def prepare_parts(bm_all, mats, rep):
    """Loose parts, repaired, welded, with outward normals (A7 prototype steps 2)."""
    parts = []; dup_faces = 0
    for k, fs in enumerate(face_components(bm_all)):
        nb, dup = extract(bm_all, fs); dup_faces += dup
        parts.append(make_part(nb, k, mats))
    filled = []
    for p in parts:
        if p['closed'] or p['sheet']:
            continue
        bm = p['bm']
        bnd = [e for e in bm.edges if e.is_boundary]
        res = bmesh.ops.holes_fill(bm, edges=bnd, sides=0)
        for f in res['faces']:
            nbf = [g for e in f.edges for g in e.link_faces if g is not f]
            f.material_index = nbf[0].material_index if nbf else p['mat']; f.smooth = False
        p['repaired'] = True; p['filled_faces'] = len(res['faces'])
        if is_closed(bm):
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            if signed_volume(bm) < 0:
                bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
            bm.normal_update()
            for f in res['faces']:
                ps = [v.co.copy() for v in f.verts]
                filled.append((f.calc_center_median().copy(), f.normal.copy(), bbox_of(ps)))
    for p in parts:
        if not p['sheet']:
            continue
        ok = True
        for f in p['bm'].faces:
            fc = f.calc_center_median(); fb = bbox_of([v.co for v in f.verts])
            hit = False
            for (c, n, bb) in filled:
                if abs((fc - c).dot(n)) < 1e-3 and abs(f.normal.dot(n)) > 0.99 and \
                        all(fb[0][i] >= bb[0][i] - 2e-3 and fb[1][i] <= bb[1][i] + 2e-3 for i in range(3)):
                    hit = True; break
            if not hit:
                ok = False; break
        if ok:
            p['dropped'] = 'cover quad over a hole the repair filled'
    for p in parts:
        if p['dropped'] or not is_closed(p['bm']):
            continue
        v0 = signed_volume(p['bm'])
        bmesh.ops.recalc_face_normals(p['bm'], faces=p['bm'].faces[:])
        if signed_volume(p['bm']) < 0:
            bmesh.ops.reverse_faces(p['bm'], faces=p['bm'].faces[:])
        p['bm'].normal_update()
        p['normals_fixed'] = v0 < 0
    live = [p for p in parts if not p['dropped']]
    weld = {'parts_welded': 0, 'verts_removed': 0, 'reverted': 0}
    for p in live:
        if not is_closed(p['bm']):
            continue
        nv = len(p['bm'].verts); keep = p['bm'].copy(); v0 = signed_volume(p['bm'])
        bmesh.ops.remove_doubles(p['bm'], verts=p['bm'].verts[:], dist=WELD)
        bmesh.ops.dissolve_degenerate(p['bm'], dist=WELD, edges=p['bm'].edges[:])
        p['bm'].normal_update()
        if len(p['bm'].verts) == nv:
            keep.free(); continue
        if not is_closed(p['bm']) or abs(signed_volume(p['bm']) - v0) > 1e-6 * max(1.0, abs(v0)):
            p['bm'].free(); p['bm'] = keep; weld['reverted'] += 1
        else:
            keep.free(); weld['parts_welded'] += 1; weld['verts_removed'] += nv - len(p['bm'].verts)
    for p in live:
        refresh_part(p)
    rep['loose_parts'] = len(parts); rep['duplicate_faces_skipped'] = dup_faces; rep['weld'] = weld
    rep['repaired_parts'] = [{'part': p['id'], 'mat': p['mat_name'], 'filled_faces': p.get('filled_faces', 0),
                              'closed_after': is_closed(p['bm'])} for p in parts if p['repaired']]
    rep['dropped_parts'] = [{'part': p['id'], 'mat': p['mat_name'], 'why': p['dropped']} for p in parts if p['dropped']]
    rep['normals_fixed_parts'] = [{'part': p['id'], 'mat': p['mat_name']} for p in parts if p['normals_fixed']]
    return parts, live


def bm_object(bm, name, mats):
    me = bpy.data.meshes.new(name); bm.to_mesh(me)
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def boolean_difference(target, cutter, mats, solver):
    a = bm_object(target, '_bool_a', mats); b = bm_object(cutter, '_bool_b', mats)
    md = a.modifiers.new('diff', 'BOOLEAN'); md.operation = 'DIFFERENCE'; md.solver = solver; md.object = b
    try:
        md.material_mode = 'INDEX'
    except (AttributeError, TypeError):
        pass
    dg = bpy.context.evaluated_depsgraph_get()
    ev = a.evaluated_get(dg); me = ev.to_mesh()
    out = bmesh.new(); out.from_mesh(me); ev.to_mesh_clear()
    for ob in (a, b):
        m = ob.data; bpy.data.objects.remove(ob); bpy.data.meshes.remove(m)
    if not out.faces.layers.int.get('cut'):
        out.faces.layers.int.new('cut')
    if out.loops.layers.uv.active is None and target.loops.layers.uv.active is not None:
        out.loops.layers.uv.new('UVMap')
    out.normal_update()
    return out


def frame_box(bm):
    """The box a wood part fills: its largest face and the largest face across it give two axes, the third is
    their cross product, and the extents are the part's own vertices. Subtracting the box instead of the bevelled
    part leaves no plaster slivers in the bevels (they came out as loose islands a few mm thick)."""
    groups = []
    for f in bm.faces:
        n = f.normal
        if n.length < 0.5:
            continue
        for g in groups:
            if abs(g['n'].dot(n)) > 0.985:
                g['a'] += f.calc_area(); break
        else:
            groups.append({'n': n.copy(), 'a': f.calc_area()})
    groups.sort(key=lambda g: -g['a'])
    n1 = groups[0]['n'].normalized()
    n2 = next((g['n'] for g in groups[1:] if abs(g['n'].dot(n1)) < 0.3), n1.orthogonal())
    n2 = (n2 - n1 * n2.dot(n1)).normalized()
    n3 = n1.cross(n2).normalized()
    ext = []
    for ax in (n1, n2, n3):
        ds = [v.co.dot(ax) for v in bm.verts]
        ext.append((min(ds), max(ds)))
    c = n1 * sum(ext[0]) * 0.5 + n2 * sum(ext[1]) * 0.5 + n3 * sum(ext[2]) * 0.5
    M = Matrix.Identity(4)
    for i, (ax, (lo, hi)) in enumerate(zip((n1, n2, n3), ext)):
        for r in range(3):
            M[r][i] = ax[r] * (hi - lo) * 0.5
    for r in range(3):
        M[r][3] = c[r]
    box = bmesh.new()
    bmesh.ops.create_cube(box, size=2.0)
    box.transform(M)
    if signed_volume(box) < 0:
        bmesh.ops.reverse_faces(box, faces=box.faces[:])
    box.normal_update()
    box.faces.layers.int.new('cut')
    return box


def subtract_frames(live, mats, rep):
    """Wood that is embedded in the masonry (door jambs, lintels, posts) is taken out of the masonry, so the frame
    chunk and the masonry chunks never overlap (decision 3). Checked per operation: the result must be closed,
    and the volume removed must lie between 0 and the frame part's volume."""
    frames = [p for p in live if p['role'] == 'frame' and p['closed']]
    log_ = []
    for p in [q for q in live if q['role'] == 'masonry' and q['closed']]:
        for f in frames:
            if not boxes_touch(p['bbox'], f['bbox'], -1e-4):
                continue
            v0 = signed_volume(p['bm'])
            box = frame_box(f['bm']); fv = signed_volume(box)
            res = None; used = None
            tried = []; fallback = None
            for solver in ('EXACT', 'MANIFOLD'):
                out = boolean_difference(p['bm'], box, mats, solver)
                if not closed_when_triangulated(out):
                    bmesh.ops.triangulate(out, faces=out.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
                    out.normal_update()
                removed = v0 - signed_volume(out)
                bow = [e for e in out.edges if len(e.link_faces) == 4]
                tried.append({'solver': solver, 'closed': is_closed(out), 'bowtie_edges': len(bow), 'removed': round(removed, 7)})
                if is_closed(out) and -1e-7 <= removed <= fv + 1e-6:
                    res = out; used = solver; break
                if bow and fallback is None and -1e-7 <= removed <= fv + 1e-6:
                    # two pieces of plaster meeting along one edge of the wood: separated there, each stays closed
                    bmesh.ops.split_edges(out, edges=bow)
                    out.normal_update()
                    if is_closed(out):
                        fallback = (out, solver + '+split_edges'); continue
                out.free()
            if res is None and fallback is not None:
                res, used = fallback
            elif fallback is not None:
                fallback[0].free()
            if res is None:
                box.free()
                if all(abs(t['removed']) < 1e-6 for t in tried):
                    continue                    # the wood only touches the masonry: nothing to take out
                log_.append({'masonry': p['id'], 'frame': f['id'], 'result': 'boolean failed, overlap kept', 'tried': tried})
                continue
            removed = v0 - signed_volume(res)
            if removed < 1e-7:
                res.free(); box.free(); continue
            fb = BVHTree.FromBMesh(box)
            box.free()
            cutl = res.faces.layers.int.get('cut'); uvl = res.loops.layers.uv.active
            own = {f2.material_index for f2 in p['bm'].faces}
            notch = 0
            for fc in res.faces:
                ctr = fc.calc_center_median()
                hit = fb.find_nearest(ctr, 2e-4)
                if hit[0] is not None or fc.material_index not in own:
                    fc.material_index = p['mat']; fc.smooth = False; fc[cutl] = TAG_NOTCH; notch += 1
                    if uvl is not None:
                        t1, t2 = planar_basis(fc.normal if fc.normal.length > 0.5 else Vector((0, 0, 1)))
                        for l in fc.loops:
                            l[uvl].uv = (l.vert.co.dot(t1), l.vert.co.dot(t2))
            p['bm'].free(); p['bm'] = res; refresh_part(p)
            log_.append({'masonry': p['id'], 'frame': f['id'], 'solver': used, 'removed': round(removed, 6),
                         'frame_volume': round(abs(f['vol']), 6), 'frame_box_volume': round(fv, 6), 'notch_faces': notch})
    rep['frame_subtraction'] = log_
    return sum(e.get('removed', 0.0) for e in log_)


def dissolve_parts(live, rep):
    d = {'tris_before': 0, 'tris_after': 0, 'reverted_parts': 0}
    for p in live:
        d['tris_before'] += sum(len(f.verts) - 2 for f in p['bm'].faces)
        if is_closed(p['bm']) and p['role'] == 'masonry':      # frames are never cut: they stay exactly as modelled
            keep = p['bm'].copy(); v0 = signed_volume(p['bm'])
            bmesh.ops.dissolve_limit(p['bm'], angle_limit=math.radians(0.5), use_dissolve_boundaries=False,
                                     verts=p['bm'].verts[:], edges=p['bm'].edges[:], delimit={'MATERIAL'})
            p['bm'].normal_update()
            if not is_closed(p['bm']) or not closed_when_triangulated(p['bm']) or \
                    abs(signed_volume(p['bm']) - v0) > 1e-5 * abs(v0):
                d.setdefault('reverted', []).append({'part': p['id'], 'role': p['role'], 'closed': is_closed(p['bm']),
                                                     'dvol': signed_volume(p['bm']) - v0})
                p['bm'].free(); p['bm'] = keep; d['reverted_parts'] += 1
                if not closed_when_triangulated(p['bm']):
                    bmesh.ops.triangulate(p['bm'], faces=p['bm'].faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
                    p['bm'].normal_update()
                    d.setdefault('triangulated', []).append(p['id'])
            else:
                keep.free()
        d['tris_after'] += sum(len(f.verts) - 2 for f in p['bm'].faces)
        refresh_part(p)
    rep['dissolve'] = d


def split_multi_shell(live, mats, rep):
    """A boolean can leave a masonry part in several pieces: each becomes its own part, except slivers under
    SLIVER_VOL (plaster left in the bevels of a jamb or a post, hidden by the wood), which are dropped."""
    out = []; nid = max(p['id'] for p in live) + 1
    dropped = []
    for p in live:
        comps = face_components(p['bm']) if p['role'] == 'masonry' else [None]
        if len(comps) == 1:
            out.append(p); continue
        for fs in comps:
            nb, _ = extract(p['bm'], fs)
            v = signed_volume(nb)
            if abs(v) < SLIVER_VOL:
                dropped.append({'from_part': p['id'], 'volume': round(v, 7), 'faces': len(nb.faces)})
                nb.free(); continue
            q = dict(p); q['bm'] = nb; q['id'] = nid; nid += 1; q['split_from'] = p['id']
            refresh_part(q); out.append(q)
    rep['slivers_dropped'] = dropped
    return out, sum(d['volume'] for d in dropped)


def module_seed(name, k):
    return zlib.crc32(name.encode()) % 100000 + 7919 * k


def fracture_module(spec, src_fbx, uscale, A, renders):
    name, folder, kind, lo_n, hi_n, target, floor = spec
    T = {}; t0 = time.perf_counter(); tl = [t0]

    def lap(k):
        now = time.perf_counter(); T[k] = round(T.get(k, 0) + now - tl[0], 3); tl[0] = now
    rep = {}
    bm_all, mats, src_objs = import_module(src_fbx)
    intact_tris = sum(len(f.verts) - 2 for f in bm_all.faces)
    glass_idx = {i for i, m in enumerate(mats) if m and any(k in m.name.lower() for k in ('glass', 'verre', 'vitre'))}
    ps_all = [v.co for v in bm_all.verts]
    intact_bb = bbox_of(ps_all)
    ps_ng = [v.co for f in bm_all.faces if f.material_index not in glass_idx for v in f.verts]
    intact_bb_ng = bbox_of(ps_ng)
    lap('import')
    parts, live = prepare_parts(bm_all, mats, rep)
    glass = [p for p in live if p['role'] == 'glass']
    live = [p for p in live if p['role'] != 'glass']
    open_parts = [p for p in live if not p['closed']]
    raw_volume = sum(p['vol'] for p in live if p['closed'])
    removed = subtract_frames(live, mats, rep)
    live, sliver_vol = split_multi_shell(live, mats, rep)
    removed += sliver_vol
    dissolve_parts(live, rep)
    masonry = [p for p in live if p['role'] == 'masonry']
    frames = [p for p in live if p['role'] == 'frame']
    body_volume = sum(p['vol'] for p in live if p['closed'])
    lap('prepare')

    vmax = max(abs(p['vol']) for p in masonry)
    core = [p for p in masonry if abs(p['vol']) >= 0.1 * vmax] if kind == 'wall' else list(masonry)
    clo, chi = bbox_union([p['bbox'] for p in core])
    dims = chi - clo
    t_ax = min(range(3), key=lambda i: dims[i]) if kind == 'wall' else None
    axes = [i for i in range(3) if i != t_ax] if kind == 'wall' else [0, 1, 2]
    slab = max(masonry, key=lambda p: abs(p['vol']))
    band = (slab['bbox'][0][t_ax], slab['bbox'][1][t_ax]) if kind == 'wall' else None
    probe = None; planes = []
    if kind == 'wall':
        probe = HullProbe(t_ax, axes, band, clo, chi, [p for p in live if p['closed']], core)
        planes = candidate_planes(core, t_ax, axes, clo, chi)
    core_bm = bmesh.new()
    for p in core:
        me = bpy.data.meshes.new('_c'); p['bm'].to_mesh(me); core_bm.from_mesh(me); bpy.data.meshes.remove(me)
    core_bvh = BVHTree.FromBMesh(core_bm)
    lap('frame')

    results = []; prev_parts = []
    for k in range(1, A.variants + 1):
        seed = module_seed(name, k)
        stats = {'bisect': 0, 'caps': 0, 'cap_fail': 0}
        tv = time.perf_counter()
        if kind == 'wall':
            t_mid = (clo[t_ax] + chi[t_ax]) * 0.5

            def ok_fn(p):
                o = p.copy(); o[t_ax] = clo[t_ax] - 1.0
                d = Vector((0, 0, 0)); d[t_ax] = 1.0
                return core_bvh.ray_cast(o, d, dims[t_ax] + 2.0)[0] is not None
            job = WallJob(masonry, core, t_ax, axes, clo, chi, band, floor, lo_n, hi_n, target, probe, planes, stats)
            job.s2 = uscale * uscale
            job.frame_bvh = [BVHTree.FromBMesh(f['bm']) for f in frames]
            sres = job.run(seed, ok_fn, {t_ax: t_mid}, 1.5)
            m_chunks = sres['chunks']
            variant_dropped = sres['dropped_vol']
            frac_info = {'structure': {'seeds': [[round(x, 5) for x in s] for s in sres['seeds']], 'cells': sres['cells'],
                                       'pruned_seeds': sres['pruned_seeds'], 'islands': sres['islands'], 'merges': sres['merges'],
                                       'opening_splits': sres['splits'], 'attempts': sres['attempts']}}
        else:
            rng = random.Random(seed)
            variant_dropped = 0.0
            units, part_ids, n_courses = stack_partition(masonry, lo_n, hi_n, target, rng, dims.z, (clo.z + chi.z) * 0.5, prev_parts)
            prev_parts.append(part_ids)
            m_chunks = [{'comps': [make_comp(b['bm'].copy(), b, -1, 'structure') for b in u['blocks']], 'cells': set()} for u in units]
            frac_info = {'structure': {'courses': n_courses, 'blocks': len(masonry), 'groups_of_parts': part_ids,
                                       'split_courses': sum(1 for u in units if u['split'])}}
        f_chunks = [{'comps': [make_comp(f['bm'].copy(), f, -1, 'frame')], 'cells': set(), 'frame': True} for f in frames]
        T['fracture_v%d' % k] = round(time.perf_counter() - tv, 3)
        r = build_variant(name, k, kind, m_chunks, f_chunks, mats, axes, t_ax, clo, chi, probe, uscale, A, rep, stats,
                          frac_info, dict(intact_tris=intact_tris, intact_bb=intact_bb, intact_bb_ng=intact_bb_ng,
                                          raw_volume=raw_volume, removed=removed, body_volume=body_volume,
                                          dropped=variant_dropped,
                                          glass=glass, open_parts=open_parts, floor=floor, lo_n=lo_n, hi_n=hi_n,
                                          target=target, seed=seed, src_fbx=src_fbx, core_count=len(core),
                                          band=band), src_objs, renders)
        results.append(r)
    T['total'] = round(time.perf_counter() - t0, 3)
    for r in results:
        r['module_timings_s'] = T
    return results


def assemble_chunk(comps, has_uv):
    """Join a chunk's components into one triangulated mesh. Zero-area triangles are dissolved only when that
    leaves every edge with two faces (dissolving can pinch two sides together into an edge with four faces)."""
    bm = new_bm_like(has_uv)
    for c in comps:
        me = bpy.data.meshes.new('_x'); c['bm'].to_mesh(me); bm.from_mesh(me); bpy.data.meshes.remove(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
    degen0 = sum(1 for f in bm.faces if f.calc_area() < 1e-9)
    nonman0 = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    if degen0:
        t = bm.copy()
        bmesh.ops.dissolve_degenerate(t, dist=1e-6, edges=t.edges[:])
        bmesh.ops.triangulate(t, faces=[f for f in t.faces if len(f.verts) > 3], quad_method='BEAUTY', ngon_method='BEAUTY')
        if all(len(e.link_faces) == 2 for e in t.edges):
            bm.free(); bm = t
        else:
            t.free()
    bm.normal_update()
    return bm, degen0, nonman0


def chunk_defects(comps, has_uv):
    """(edges without exactly two faces, zero-area triangles) once the chunk is assembled. A zero-area triangle is
    dropped by the FBX importers: harmless in Unity (a crack of zero width), but counted."""
    bm, _, _ = assemble_chunk(comps, has_uv)
    n = sum(1 for e in bm.edges if len(e.link_faces) != 2), sum(1 for f in bm.faces if f.calc_area() < 1e-9)
    bm.free()
    return n


def build_variant(name, k, kind, m_chunks, f_chunks, mats, axes, t_ax, clo, chi, probe, s, A, rep, stats, frac_info, info,
                  src_objs, renders):
    tb = time.perf_counter()

    def order_key(ch):
        bb = bbox_union([c['bbox'] for c in ch['comps']]); c = (bb[0] + bb[1]) * 0.5
        return (round(c.z, 2), c.x, c.y)
    all_chunks = sorted(m_chunks, key=order_key) + sorted(f_chunks, key=order_key)
    fname = 'PKF_%s_v%d' % (name, k)
    ex = bpy.data.scenes.new('PKF_Export')
    objs = []; recs = []
    has_uv = any(c['bm'].loops.layers.uv.active is not None for ch in all_chunks for c in ch['comps'])
    wdirs_plane = width_dirs(axes)
    wdirs_3d = width_dirs([0, 1, 2])
    tol = STACK_TOUCH if kind == 'stack' else TOUCH
    for idx, ch in enumerate(all_chunks):
        cname = '%s_Chunk_%02d' % (name, idx)
        bm, degen0, nonman0 = assemble_chunk(ch['comps'], has_uv)
        bow = [e for e in bm.edges if len(e.link_faces) == 4]
        cutl = bm.faces.layers.int.get('cut')
        nonman = sum(1 for e in bm.edges if len(e.link_faces) != 2)
        nm_where = [{'mid': [round(x, 4) for x in (e.verts[0].co + e.verts[1].co) * 0.5], 'faces': len(e.link_faces),
                     'len': round(e.calc_length(), 5)} for e in bm.edges if len(e.link_faces) != 2][:6]
        shells = len(face_components(bm))
        vol, cen = volume_centroid(bm)
        degen = sum(1 for f in bm.faces if f.calc_area() < 1e-9)
        cap_area = sum(f.calc_area() for f in bm.faces if 0 < f[cutl] < TAG_NOTCH)
        notch_area = sum(f.calc_area() for f in bm.faces if f[cutl] == TAG_NOTCH)
        n_caps = sum(1 for f in bm.faces if 0 < f[cutl] < TAG_NOTCH)
        cap_mats = sorted({mats[f.material_index].name for f in bm.faces if f[cutl] != 0 and mats[f.material_index]})
        used = sorted({f.material_index for f in bm.faces})
        remap = {m: j for j, m in enumerate(used)}
        for f in bm.faces:
            f.material_index = remap[f.material_index]
        bb = bbox_of([v.co for v in bm.verts])
        is_frame = ch.get('frame', False)
        mwidth = min_width([v.co for v in bm.verts], wdirs_3d if (is_frame or kind == 'stack') else wdirs_plane)
        bm.transform(Matrix.Translation(-cen))
        me = bpy.data.meshes.new(cname); bm.to_mesh(me); bm.free()
        for m in used:
            me.materials.append(mats[m])
        ob = bpy.data.objects.new(cname, me); ob.location = cen
        if ob.name != cname or me.name != cname:
            raise RuntimeError('name clash: wanted %s, got object %s mesh %s' % (cname, ob.name, me.name))
        ex.collection.objects.link(ob); objs.append(ob)
        internal = 0.0
        for x in ch['comps']:
            for (ctr, ar, nrm) in x['caps']:
                for y in ch['comps']:
                    if y is x or y['part'] != x['part'] or not boxes_touch(x['bbox'], y['bbox']):
                        continue
                    if y['bvh'].find_nearest(ctr, CAP_NEAR)[0] is not None:
                        internal += ar; break
        hm = probe.measure(ch['comps']) if probe is not None else None
        if hm is None:
            _, _, hv, ht = hull_of([q for c in ch['comps'] for q in c['pts']])
            hm = {'void': None, 'void_max': None, 'overlap': None, 'hull_vol': hv, 'hull_tris': ht}
        recs.append({'name': cname, 'kind': 'frame' if is_frame else 'masonry', 'cells': sorted(ch['cells']),
                     'parts': sorted({c['part'] for c in ch['comps']}), 'components': len(ch['comps']),
                     'materials': [mats[m].name if mats[m] else None for m in used], 'cap_materials': cap_mats,
                     'centroid': cen, 'bb': bb, 'volume': vol, 'min_width': mwidth, 'tris': len(me.polygons), 'shells': shells,
                     'nonmanifold_edges': nonman, 'nonmanifold_where': nm_where, 'four_face_edges': len(bow),
                     'manifold': nonman == 0, 'degenerate_tris': degen,
                     'degenerate_tris_before_cleanup': degen0, 'nonmanifold_before_cleanup': nonman0,
                     'open_comps': [{'part': c['part'], 'boundary_edges': sum(1 for e in c['bm'].edges if len(e.link_faces) != 2)}
                                    for c in ch['comps'] if not c['closed']],
                     'cut_faces': n_caps, 'cut_area': cap_area, 'notch_area': notch_area, 'internal_cut_area': internal,
                     'hull': hm, 'comps': ch['comps']})
    t_build = time.perf_counter() - tb

    # anchors (faces of the core bounds, 1 cm), adjacency, connectivity
    ta = time.perf_counter()
    for r in recs:
        mn, mx = r['bb']
        r['anchor'] = {'bottom': mn.z <= clo.z + TOUCH, 'top': mx.z >= chi.z - TOUCH,
                       'side_pos': mn.x <= clo.x + TOUCH,    # Unity +x is Blender -x
                       'side_neg': mx.x >= chi.x - TOUCH}
    adj = []
    for i in range(len(recs)):
        for j in range(i + 1, len(recs)):
            area, touch = shared_cut_area(recs[i]['comps'], recs[j]['comps'], tol)
            if touch or area > 0:
                adj.append({'a': i, 'b': j, 'shared_cut_area': area})
    nbrs = {i: [] for i in range(len(recs))}
    for e in adj:
        nbrs[e['a']].append(e['b']); nbrs[e['b']].append(e['a'])
    seen = {0}; stack = [0]
    while stack:
        q = stack.pop()
        for m in nbrs[q]:
            if m not in seen:
                seen.add(m); stack.append(m)
    connected = len(seen) == len(recs)
    anchored = [i for i, r in enumerate(recs) if r['anchor']['bottom'] or r['anchor']['side_pos'] or r['anchor']['side_neg']]
    reach = set(anchored); stack = list(anchored)
    while stack:
        q = stack.pop()
        for m in nbrs[q]:
            if m not in reach:
                reach.add(m); stack.append(m)
    floating = [recs[i]['name'] for i in range(len(recs)) if i not in reach]
    t_adj = time.perf_counter() - ta

    # export
    te = time.perf_counter()
    os.makedirs(A.out, exist_ok=True)
    fbx_out = os.path.join(A.out, fname + '.fbx')
    vl = ex.view_layers[0]
    for o in ex.collection.objects:
        o.select_set(True, view_layer=vl)
    vl.objects.active = objs[0]
    with bpy.context.temp_override(scene=ex, view_layer=vl, active_object=objs[0], selected_objects=objs,
                                   selected_editable_objects=objs):
        bpy.ops.export_scene.fbx(filepath=fbx_out, **FBX_SETTINGS)
    t_exp = time.perf_counter() - te

    def R(v, n=5):
        return [round(x, n) for x in v]

    def U(v):
        return [round(-v.x * s, 5), round(v.z * s, 5), round(-v.y * s, 5)]

    def UB(mn, mx):
        return {'min': [round(-mx.x * s, 5), round(mn.z * s, 5), round(-mx.y * s, 5)],
                'max': [round(-mn.x * s, 5), round(mx.z * s, 5), round(-mn.y * s, 5)]}
    chunk_vol = sum(r['volume'] for r in recs)
    masonry_recs = [r for r in recs if r['kind'] == 'masonry']
    frame_recs = [r for r in recs if r['kind'] == 'frame']
    mmean = sum(r['volume'] for r in masonry_recs) / len(masonry_recs)
    floor = info['floor']
    under = [r['name'] for r in masonry_recs if r['min_width'] < floor - 1e-6 or r['volume'] < 0.2 * mmean]
    body = info['body_volume']; rawv = info['raw_volume']
    vol_err = (chunk_vol - body) / body
    hv = [r['hull']['void'] for r in masonry_recs if r['hull']['void'] is not None]
    per_kind = {}
    for kk, rs in (('masonry', masonry_recs), ('frame', frame_recs)):
        if not rs:
            continue
        mean = sum(r['volume'] for r in rs) / len(rs)
        per_kind[kk] = {'count': len(rs), 'mean_volume': round(mean, 6), 'min_volume': round(min(r['volume'] for r in rs), 6),
                        'min_volume_frac_of_mean': round(min(r['volume'] for r in rs) / mean, 3),
                        'min_width': round(min(r['min_width'] for r in rs), 4), 'min_width_unity': round(min(r['min_width'] for r in rs) * s, 4),
                        'tris_min': min(r['tris'] for r in rs), 'tris_max': max(r['tris'] for r in rs),
                        'tris_total': sum(r['tris'] for r in rs),
                        'hull_tris_max': max(r['hull']['hull_tris'] for r in rs)}
    validation = {
        'all_manifold': all(r['manifold'] for r in recs),
        'nonmanifold_chunks': [r['name'] for r in recs if not r['manifold']],
        'intact_volume': round(body, 6),
        'intact_volume_note': 'closed non-glass parts after the embedded wood is taken out of the masonry (the union '
                              'of masonry and wood); raw sum of parts before that: %.6f' % rawv,
        'intact_volume_raw_parts': round(rawv, 6), 'frame_overlap_removed': round(info['removed'], 6),
        'island_slivers_dropped_volume': round(info['dropped'], 7),
        'chunk_volume': round(chunk_vol, 6),
        'volume_error_pct': round(100 * vol_err, 4),
        'volume_within_3pct': abs(vol_err) <= 0.03,
        'masonry_count': len(masonry_recs), 'masonry_count_range': [info['lo_n'], info['hi_n']],
        'masonry_count_in_range': info['lo_n'] <= len(masonry_recs) <= info['hi_n'],
        'frame_count': len(frame_recs),
        'min_size_rule': {'min_caliper_width_masonry': floor, 'min_caliper_width_masonry_unity': round(floor * s, 4),
                          'min_vol_frac_of_masonry_mean': 0.2, 'frames': 'kept whole: exempt (reported in per_kind.frame)'},
        'chunks_under_size_floor': under,
        'per_kind': per_kind,
        'intact_tris': info['intact_tris'], 'chunk_tris_total': sum(r['tris'] for r in recs),
        'degenerate_tris': sum(r['degenerate_tris'] for r in recs),
        'adjacency_connected': connected, 'adjacency_edges': len(adj),
        'floating_chunks': floating,
        'glass_parts_excluded': len(info['glass']),
        'cut_face_materials': sorted({m for r in recs for m in r['cap_materials']}),
        'hull_void_max_unity': round(max(hv) * 2.25, 5) if hv else None,
        'hull_void_limit_unity': VOID_MAX_UNITY,
        'hull_void_ok': (max(hv) * 2.25 <= VOID_MAX_UNITY + 1e-9) if hv else True,
        'open_source_parts': [{'part': p['id'], 'mat': p['mat_name'], 'faces': p['faces']} for p in info['open_parts']],
        'holes_fill_fallback_faces': stats.get('holes_fill_fallback', 0), 'cap_failures_first_try': stats['cap_fail'],
        'clip_retries': stats.get('retries', 0), 'clip_retry_failed': stats.get('retry_failed', 0),
        'coplanar_caps_pushed': stats.get('coplanar_pushed', 0),
        'internal_cut_area_total': round(sum(r['internal_cut_area'] for r in recs), 5),
    }
    validation['ok'] = bool(validation['all_manifold'] and validation['volume_within_3pct'] and validation['masonry_count_in_range']
                            and not under and connected and not floating and validation['hull_void_ok']
                            and validation['glass_parts_excluded'] >= 0 and not any('glass' in m for m in validation['cut_face_materials'])
                            and not any(any(m and 'glass' in m for m in r['materials']) for r in recs))
    sidecar = {
        'schema': 'movers-chunkset-1',
        'module': name, 'variant': k, 'source_fbx': info['src_fbx'].replace('\\', '/'), 'fbx': os.path.basename(fbx_out),
        'kind': kind,
        'unity': {'import_scale': s,
                  'mapping': 'unity_local = (-bx, bz, -by) * import_scale; import the chunk FBX with the same ModelImporter '
                             'settings as the intact module (globalScale, useFileScale, materials remapped by name); the '
                             'chunk root placed on the intact module at identity overlays it',
                  'units_note': 'values without suffix are Blender module space (m, Z up, before import scale); *_unity '
                                'values are in the Unity model space of the module'},
        'params': {'seed': info['seed'], 'target_chunks': info['target'], 'range': [info['lo_n'], info['hi_n']],
                   'gamma': 1.5, 'min_extent': floor, 'min_vol_frac': 0.2,
                   'touch_tolerance': tol, 'anchor_tolerance': TOUCH, 'hull_void_limit_unity': VOID_MAX_UNITY},
        'frame': {'thickness_axis': 'xyz'[t_ax] if t_ax is not None else None, 'plane_axes': ['xyz'[a] for a in axes],
                  'core_bounds': {'min': R(clo), 'max': R(chi)}, 'core_bounds_unity': UB(clo, chi),
                  'anchor_bounds_unity': UB(clo, chi),
                  'anchor_note': 'anchors test the core bounds (the masonry body without trim that overhangs it: corner '
                                 'bricks, a threshold below the floor), which is where the floor and the neighbour modules meet it',
                  'module_bounds_unity': UB(info['intact_bb'][0], info['intact_bb'][1]),
                  'module_bounds_no_glass_unity': UB(info['intact_bb_ng'][0], info['intact_bb_ng'][1]),
                  'slab_band': R(info['band']) if info['band'] else None},
        'intact': {'tris': info['intact_tris'], 'bounds': {'min': R(info['intact_bb'][0]), 'max': R(info['intact_bb'][1])},
                   'volume': round(body, 6), 'volume_unity': round(body * s ** 3, 6), 'volume_raw_parts': round(rawv, 6),
                   'loose_parts': rep['loose_parts'], 'duplicate_faces_skipped': rep['duplicate_faces_skipped'],
                   'repaired_parts': rep['repaired_parts'], 'dropped_parts': rep['dropped_parts'],
                   'normals_fixed_parts': rep['normals_fixed_parts'], 'weld': rep['weld'], 'dissolve': rep['dissolve'],
                   'glass_parts_excluded': [{'part': p['id'], 'mat': p['mat_name'], 'volume': round(p['vol'], 6)} for p in info['glass']],
                   'frame_subtraction': rep['frame_subtraction'], 'slivers_dropped': rep.get('slivers_dropped', [])},
        'fracture': frac_info,
        'chunks': [], 'adjacency': [], 'validation': validation, 'timings_s': {},
    }
    sidecar['fracture']['bisect_ops'] = stats['bisect']; sidecar['fracture']['cap_faces_created'] = stats['caps']
    for i, r in enumerate(recs):
        mn, mx = r['bb']
        hm = r['hull']
        sidecar['chunks'].append({
            'index': i, 'name': r['name'], 'kind': r['kind'], 'pane': None, 'materials': r['materials'],
            'centroid': R(r['centroid']), 'centroid_unity': U(r['centroid']),
            'bounds': {'min': R(mn), 'max': R(mx)}, 'bounds_unity': UB(mn, mx),
            'volume': round(r['volume'], 7), 'volume_unity': round(r['volume'] * s ** 3, 7),
            'mass_share': round(r['volume'] / chunk_vol, 6),
            'min_width': round(r['min_width'], 4), 'min_width_unity': round(r['min_width'] * s, 4),
            'tris': r['tris'], 'shells': r['shells'], 'manifold': r['manifold'], 'degenerate_tris': r['degenerate_tris'],
            'nonmanifold_edges': r['nonmanifold_edges'], 'nonmanifold_before_cleanup': r['nonmanifold_before_cleanup'],
            'nonmanifold_where': r['nonmanifold_where'], 'four_face_edges': r['four_face_edges'],
            'open_comps': r['open_comps'],
            'cut_area_unity': round(r['cut_area'] * s * s, 5), 'internal_cut_area_unity': round(r['internal_cut_area'] * s * s, 5),
            'frame_notch_area_unity': round(r['notch_area'] * s * s, 5), 'cut_materials': r['cap_materials'],
            'touches_bottom': r['anchor']['bottom'], 'touches_top': r['anchor']['top'],
            'touches_side': [w for w, f in (('side_ux_pos', r['anchor']['side_pos']), ('side_ux_neg', r['anchor']['side_neg'])) if f],
            'anchors': dict(r['anchor']),
            'neighbors': sorted(({'chunk': (e['b'] if e['a'] == i else e['a']), 'shared_cut_area_unity': round(e['shared_cut_area'] * s * s, 5)}
                                 for e in adj if i in (e['a'], e['b'])), key=lambda d: d['chunk']),
            'hull_volume_unity': round(hm['hull_vol'] * s ** 3, 7), 'hull_tris': hm['hull_tris'],
            'hull_excess_frac': round(hm['hull_vol'] / r['volume'] - 1.0, 4) if r['volume'] > 0 else None,
            'hull_void_area_unity': round(hm['void'] * s * s, 5) if hm['void'] is not None else None,
            'hull_overlap_area_unity': round(hm['overlap'] * s * s, 5) if hm['overlap'] is not None else None,
            'hull_void_bounds_unity': UB(hm['where'][0], hm['where'][1]) if hm.get('where') else None,
            'source_cells': r['cells'], 'source_parts': r['parts'], 'detached_kept_whole': r['kind'] == 'frame',
            'floating': r['name'] in floating, 'gap_to_nearest_unity': None, 'nearest_chunk': None})
    for e in adj:
        sidecar['adjacency'].append({'a': e['a'], 'b': e['b'], 'shared_cut_area_unity': round(e['shared_cut_area'] * s * s, 5),
                                     'kind': 'cut_face' if e['shared_cut_area'] > 1e-6 else 'touch'})
    sidecar['timings_s'] = {'build_chunks': round(t_build, 3), 'adjacency': round(t_adj, 3), 'export_fbx': round(t_exp, 3)}
    json_out = os.path.join(A.out, fname + '.json')
    with open(json_out, 'w') as fh:
        json.dump(sidecar, fh, indent=1)
    if A.write_meta:
        write_meta(fbx_out, name, info)
    log('%s: %s masonry=%d frame=%d vol_err=%.3f%% manifold=%s connected=%s under=%s void_max=%s tris %d -> %d ok=%s' % (
        fname, kind, len(masonry_recs), len(frame_recs), 100 * vol_err, validation['all_manifold'], connected, under,
        validation['hull_void_max_unity'], info['intact_tris'], validation['chunk_tris_total'], validation['ok']))

    if renders:
        tr = time.perf_counter()
        render_views(name, k, fname, renders, objs, recs, src_objs, kind, t_ax, (clo + chi) * 0.5)
        sidecar['timings_s']['render'] = round(time.perf_counter() - tr, 3)
        with open(json_out, 'w') as fh:
            json.dump(sidecar, fh, indent=1)
    # clean up: the next variant must be free to use the same object names
    for ob in objs:
        me = ob.data; bpy.data.objects.remove(ob, do_unlink=True); bpy.data.meshes.remove(me)
    bpy.data.scenes.remove(ex)
    for r in recs:
        for c in r['comps']:
            c['bm'].free()
    return {'file': fname, 'module': name, 'variant': k, 'fbx': fbx_out, 'json': json_out, 'validation': validation,
            'chunks': len(recs), 'fracture_summary': {kk: v for kk, v in frac_info['structure'].items() if kk not in ('seeds',)}}


def write_meta(fbx_out, name, info):
    """A new PKF FBX gets the intact module's import settings, under a GUID derived from its file name (stable
    across re-runs). An existing .meta is never touched."""
    meta = fbx_out + '.meta'
    if os.path.exists(meta):
        return
    folder = next(m[1] for m in MODULES if m[0] == name)
    _, text, _ = read_unity_meta(name, folder)
    if text is None:
        raise RuntimeError('no intact .meta to clone for ' + name)
    guid = hashlib.md5(('movers-fracture:' + os.path.basename(fbx_out)).encode()).hexdigest()
    lines = text.splitlines()
    out = [('guid: ' + guid) if l.startswith('guid: ') else l for l in lines]
    with open(meta, 'w', newline='\n') as fh:
        fh.write('\n'.join(out) + '\n')


# ============================================================ renders ==========================================

MAT_VIEW = {'wall': (0.72, 0.71, 0.69), 'brique': (0.55, 0.20, 0.18), 'brique.001': (0.42, 0.42, 0.44),
            'wood': (0.45, 0.28, 0.14), 'wood.001': (0.30, 0.19, 0.11)}


def setup_render_scene():
    sc = bpy.context.scene
    try:
        sc.render.engine = 'BLENDER_WORKBENCH'
    except TypeError as e:
        log('engine', e)
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_backface_culling = True
    sh.show_object_outline = True; sh.show_cavity = True
    try:
        sh.cavity_type = 'WORLD'
    except Exception:
        pass
    sc.render.resolution_x = 1200; sc.render.resolution_y = 860; sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    if sc.world is None:
        w = bpy.data.worlds.new('bg'); w.color = (0.93, 0.93, 0.95); sc.world = w
    for m in bpy.data.materials:
        if m.name in MAT_VIEW:
            m.diffuse_color = MAT_VIEW[m.name] + (1.0,)
    cam = sc.camera
    if cam is None:
        cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cd.clip_end = 200.0
        cam = bpy.data.objects.new('cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    return sc, cam


def frame_camera(sc, cam, pts, d):
    d = Vector(d).normalized()
    mn, mx = bbox_of(pts); c = (mn + mx) * 0.5
    rot = (-d).to_track_quat('-Z', 'Y')
    Rv = rot @ Vector((1, 0, 0)); Uv = rot @ Vector((0, 1, 0))
    xs = [(p - c).dot(Rv) for p in pts]; ys = [(p - c).dot(Uv) for p in pts]
    c = c + Rv * (max(xs) + min(xs)) * 0.5 + Uv * (max(ys) + min(ys)) * 0.5
    wdt = max(xs) - min(xs); hgt = max(ys) - min(ys)
    aspect = sc.render.resolution_x / sc.render.resolution_y
    cam.data.ortho_scale = max(wdt, hgt * aspect) * 1.12
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = rot
    cam.location = c + d * 50.0


def shoot(sc, path):
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def render_views(name, k, fname, out, objs, recs, src_objs, kind, t_ax, center):
    os.makedirs(out, exist_ok=True)
    sc, cam = setup_render_scene()
    for o in objs:
        sc.collection.objects.link(o)
    front = (-0.55, -1.0, 0.45) if kind == 'wall' else (-0.8, -1.0, 0.5)
    back = (0.6, 1.0, 0.35)

    def world_pts(ob_list, src=False):
        pts = []
        for o in ob_list:
            M = o.matrix_world if src else Matrix.Translation(o.location)
            pts += [M @ v.co for v in o.data.vertices]
        return pts
    if k == 1:
        for o in objs:
            o.hide_render = True
        for o in src_objs:
            o.hide_render = False
        frame_camera(sc, cam, world_pts(src_objs, True), front)
        shoot(sc, os.path.join(out, '%s_0_intact.png' % name))
    for o in src_objs:
        o.hide_render = True
    for o in objs:
        o.hide_render = False
    sh = sc.display.shading
    sh.color_type = 'RANDOM'
    frame_camera(sc, cam, world_pts(objs), (0, -1, 0) if kind == 'wall' else front)
    shoot(sc, os.path.join(out, '%s_1_cells.png' % fname))
    sh.color_type = 'MATERIAL'
    rng = random.Random(3)
    for o, r in zip(objs, recs):
        c = r['centroid']
        if kind == 'wall':
            off = (c - center) * 0.55
            off[t_ax] = -rng.uniform(0.0, 0.15) - (0.45 if r['kind'] == 'frame' else 0.0)
        else:
            off = (c - center) * 0.6
            off.z = (c.z - center.z) * 0.5
        o.location = c + off
    bpy.context.view_layer.update()
    pts = []
    for o in objs:
        pts += [o.location + v.co for v in o.data.vertices]
    frame_camera(sc, cam, pts, front)
    shoot(sc, os.path.join(out, '%s_2_exploded_front.png' % fname))
    hl = bpy.data.materials.get('CUT_HIGHLIGHT') or bpy.data.materials.new('CUT_HIGHLIGHT')
    hl.diffuse_color = (1.0, 0.42, 0.05, 1.0)
    hn = bpy.data.materials.get('NOTCH_HIGHLIGHT') or bpy.data.materials.new('NOTCH_HIGHLIGHT')
    hn.diffuse_color = (0.95, 0.85, 0.1, 1.0)
    for o in objs:
        me = o.data; me.materials.append(hl); kc = len(me.materials) - 1
        me.materials.append(hn); kn = len(me.materials) - 1
        att = me.attributes.get('cut')
        if att is None:
            continue
        for poly, a in zip(me.polygons, att.data):
            if a.value == TAG_NOTCH:
                poly.material_index = kn
            elif a.value != 0:
                poly.material_index = kc
    frame_camera(sc, cam, pts, back)
    shoot(sc, os.path.join(out, '%s_3_exploded_back_cuts.png' % fname))
    for o in objs:
        sc.collection.objects.unlink(o)


# ============================================================ round-trip check =================================

def load_fbx_world(path, skip_glass=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=False)
    res = []
    for o in bpy.context.scene.objects:
        if o.type != 'MESH':
            continue
        bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
        mats = [m.name if m else '' for m in o.data.materials]
        if skip_glass:
            kill = [f for f in bm.faces if 'glass' in mats[f.material_index].lower()]
            if kill:
                bmesh.ops.delete(bm, geom=kill, context='FACES')
        ps = [v.co.copy() for v in bm.verts]
        res.append({'name': o.name, 'mesh': o.data.name, 'loc_world': o.matrix_world.translation.copy(),
                    'min': [min(p[i] for p in ps) for i in range(3)], 'max': [max(p[i] for p in ps) for i in range(3)],
                    'vol': signed_volume(bm), 'tris': sum(len(f.verts) - 2 for f in bm.faces),
                    'nonmanifold': sum(1 for e in bm.edges if len(e.link_faces) != 2), 'mats': mats})
        bm.free()
    return res


def verify(results, sources):
    out = []
    for r in results:
        A = load_fbx_world(sources[r['module']], skip_glass=True)
        B = load_fbx_world(r['fbx'])
        side = json.load(open(r['json']))
        amin = [min(o['min'][i] for o in A) for i in range(3)]; amax = [max(o['max'][i] for o in A) for i in range(3)]
        bmin = [min(o['min'][i] for o in B) for i in range(3)]; bmax = [max(o['max'][i] for o in B) for i in range(3)]
        by_name = {c['name']: c for c in side['chunks']}
        loc_err = 0.0; missing = []
        for o in B:
            c = by_name.get(o['name'])
            if c is None or o['mesh'] != o['name']:
                missing.append(o['name']); continue
            loc_err = max(loc_err, (o['loc_world'] - Vector(c['centroid'])).length)
        v = {'file': r['file'], 'objects': len(B), 'sidecar_chunks': len(side['chunks']),
             'names_not_matching_sidecar': missing + [n for n in by_name if n not in {o['name'] for o in B}],
             'bounds_max_diff_m': round(max(max(abs(amin[i] - bmin[i]), abs(amax[i] - bmax[i])) for i in range(3)), 8),
             'origin_vs_sidecar_centroid_max_err_m': round(loc_err, 8),
             'volume_after_roundtrip': round(sum(o['vol'] for o in B), 6), 'sidecar_chunk_volume': side['validation']['chunk_volume'],
             'tris_after_roundtrip': sum(o['tris'] for o in B), 'sidecar_tris': side['validation']['chunk_tris_total'],
             'nonmanifold_edges_after_roundtrip': sum(o['nonmanifold'] for o in B),
             'materials': sorted({m for o in B for m in o['mats']})}
        v['ok'] = (not v['names_not_matching_sidecar'] and v['bounds_max_diff_m'] < BOUNDS_TOL and v['origin_vs_sidecar_centroid_max_err_m'] < 1e-4
                   and v['nonmanifold_edges_after_roundtrip'] == 0 and v['tris_after_roundtrip'] == v['sidecar_tris']
                   and abs(v['volume_after_roundtrip'] - v['sidecar_chunk_volume']) < 1e-4
                   and not any('glass' in m for m in v['materials']))
        log('verify %s: bounds diff %.2e origin err %.2e nonmanifold %d ok=%s' % (r['file'], v['bounds_max_diff_m'],
            v['origin_vs_sidecar_centroid_max_err_m'], v['nonmanifold_edges_after_roundtrip'], v['ok']))
        out.append(v)
    return out


# ============================================================ main =============================================

def main():
    A = parse_args()
    roots = A.src or [ART]
    todo = [m for m in MODULES if A.modules is None or m[0] in A.modules]
    all_res = []; sources = {}; errors = []
    t0 = time.perf_counter()
    for spec in todo:
        name, folder = spec[0], spec[1]
        src = find_source(name, folder, roots)
        if src is None:
            errors.append('%s: no source FBX under %s' % (name, roots)); continue
        scale, _, meta = read_unity_meta(name, folder)
        if scale is None:
            errors.append('%s: no globalScale in %s' % (name, meta)); continue
        sources[name] = src
        log('module %s from %s (globalScale %s)' % (name, src, scale))
        try:
            all_res += fracture_module(spec, src, scale, A, A.renders)
        except Exception as e:
            import traceback
            traceback.print_exc()
            errors.append('%s: %s' % (name, e))
    ver = [] if A.no_verify else verify(all_res, sources)
    summary = {'modules': len(todo), 'files': len(all_res), 'errors': errors, 'seconds': round(time.perf_counter() - t0, 1),
               'sources': {k: v.replace('\\', '/') for k, v in sources.items()},
               'all_ok': not errors and all(r['validation']['ok'] for r in all_res) and all(v['ok'] for v in ver),
               'variants': [{kk: r[kk] for kk in ('file', 'chunks', 'validation', 'fracture_summary')} for r in all_res],
               'roundtrip': ver}
    if A.report:
        os.makedirs(os.path.dirname(os.path.abspath(A.report)), exist_ok=True)
        with open(A.report, 'w') as fh:
            json.dump(summary, fh, indent=1)
    log('DONE files=%d errors=%d all_ok=%s in %.1fs' % (len(all_res), len(errors), summary['all_ok'], summary['seconds']))


if __name__ == '__main__':
    main()
