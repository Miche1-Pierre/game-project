"""Model grandmother's bathrobe on the crew rig, to the reference the team supplied.

Replaces model_gown.py, which produced an open short-sleeved tunic rather than a bathrobe.
The reference asks for five things the tunic did not have: full length, a shawl collar, a
knotted belt, patch pockets and folded cuffs. Those are shapes, not parameters, so this is
built part by part instead of as one swept band.

Parts, all procedural, joined into one mesh:
  body      a closed tube from the neck to mid calf, flaring below the hips
  sleeves   two tapered tubes along the arm bones, stopping at three quarters
  cuffs     a wider ring at each sleeve end, the folded-back look
  collar    a shawl ribbon from the nape, over both shoulders, crossing at the waist
  belt      a flattened ring, a knot at the front, two hanging ends
  pockets   two plates on the front of the skirt

Two decisions worth reading before changing anything:

1. **The robe is closed, and the trim carries the player colour.** The reference wraps over,
   so it covers the torso panel, and 05_ART/CHARACTERS.md forbids an equippable from hiding
   the crew identity colour. Rather than keep it hanging open, the collar, the belt and the
   cuffs take the player colour (material slot 1) and the body stays pink (slot 0). Four
   players in a corridor stay separable, which is what that rule is actually protecting, and
   the base panel is still underneath when the robe comes off.

2. **The skirt is weighted to the pelvis, not to the legs.** Weighting it by proximity would
   bind the hem to the thighs and the robe would scissor open at every step. Below the hip
   line the weights are looked up at hip height instead, so the skirt swings as one cone and
   the legs travel inside it. No cloth simulation, which the brief rules out anyway. The cost
   is that a high knee can poke through the front: acceptable in greybox, and the reason the
   hem stops at mid calf rather than the floor.

Weights everywhere else are transferred from the nearest body vertex, so the robe deforms
with the body without a single weight painted by hand.

Run:

    blender --background --python tools/blender/model_bathrobe.py
"""

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, kdtree

SRC = r"C:\dev\game-project\UnityProject\Assets\Floreswa\Models\male01_1.fbx"
OUT = r"C:\dev\game-project\UnityProject\Assets\_Project\Art\Crew"
BLEND = r"C:\dev\game-project\_ArtSource\Crew_Bathrobe.blend"
PREVIEW = r"C:\dev\game-project\_ArtSource\preview_bathrobe.png"

NAME = "SM_Crew_Chest_Bathrobe"

PINK = (0.95, 0.58, 0.72, 1.0)
TRIM = (0.88, 0.36, 0.55, 1.0)      # placeholder, Unity paints this with the crew colour

SEGS = 12          # around the body
SHAPE = 2.4        # superellipse exponent. 3.0 read as a sandwich board, 2 is a flat ellipse.
CY = -0.035        # the body centre line drifts back about this much through the torso
HIP_Z = 1.36       # below this the skirt is weighted at hip height, see the docstring

# (z, half width, half depth). Measured off the body with the arms excluded, plus looseness.
# The first two rings make the shoulder slope: without them the tube started below the
# deltoids and the shirt showed through, which read as a pinafore rather than a robe.
BODY_RINGS = [
    (2.17, 0.150, 0.150),   # neck opening
    (2.08, 0.328, 0.216),   # over the shoulders
    (1.95, 0.290, 0.210),   # chest
    (1.78, 0.256, 0.198),
    (1.62, 0.246, 0.190),   # the waist, where the belt sits
    (1.45, 0.263, 0.206),
    (1.25, 0.291, 0.226),
    (1.00, 0.318, 0.246),
    (0.70, 0.342, 0.263),
    (0.45, 0.355, 0.272),
    (0.42, 0.352, 0.270),   # the hem
]

BELT_Z = 1.62
COLLAR_W = 0.135   # a shawl lies flat and wide. 0.075 read as two straps.
COLLAR_T = 0.020


# --------------------------------------------------------------------------- body


def import_body():
    bpy.ops.wm.read_homefile(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=SRC)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=SRC)
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    return mesh, arm


def material(name, colour):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    if b:
        b.inputs["Base Color"].default_value = colour
        if "Roughness" in b.inputs:
            b.inputs["Roughness"].default_value = 0.92
    m.diffuse_color = colour
    return m


# --------------------------------------------------------------------- primitives


def measure_torso(body):
    """The body silhouette per height, arms excluded, so the robe can be built around it.

    Hand written ring sizes put the waist inside the torso and the shirt showed through two
    wedges at the belt. Measuring is the only way to be sure a garment encloses what it is
    supposed to enclose, exactly as with the slipper and the boot.
    """
    gi = {g.index: g.name for g in body.vertex_groups}
    ARM = ("upper_arm", "forearm", "hand", "shoulder")

    def dominant_is_arm(v):
        best, bw = "", -1.0
        for g in v.groups:
            if g.weight > bw:
                bw, best = g.weight, gi.get(g.group, "")
        return any(best.startswith(a) for a in ARM)

    torso = [v.co for v in body.data.vertices if not dominant_is_arm(v)]

    def at(z, band):
        near = [c for c in torso if abs(c.z - z) < band]
        if len(near) < 4:
            return None
        return (max(abs(c.x) for c in near),
                min(c.y for c in near),
                max(c.y for c in near))

    out = {}
    for z, _hx, _hy in BODY_RINGS:
        m = at(z, 0.07) or at(z, 0.12) or at(z, 0.20)
        out[z] = m
    return out


def fit_rings(body):
    """BODY_RINGS, widened wherever the measured body would otherwise poke through."""
    PAD = 0.030
    measured = measure_torso(body)
    rings = []
    for z, hx, hy in BODY_RINGS:
        m = measured.get(z)
        cy = CY
        if m:
            bx, ymin, ymax = m
            cy = (ymin + ymax) * 0.5
            hx = max(hx, bx + PAD)
            hy = max(hy, (ymax - ymin) * 0.5 + PAD)
        rings.append((z, hx, hy, cy))
    return rings


FITTED = []


def ring_at(z):
    """The robe's half width and half depth at a height, interpolated from the table.

    Everything that has to sit ON the robe (collar, belt, pockets) is placed through this
    rather than with its own numbers, so changing the silhouette moves them with it.
    """
    table = FITTED if FITTED else [(z0, x0, y0, CY) for z0, x0, y0 in BODY_RINGS]
    if z >= table[0][0]:
        return table[0][1], table[0][2]
    if z <= table[-1][0]:
        return table[-1][1], table[-1][2]
    for i in range(len(table) - 1):
        z0, x0, y0, _ = table[i]
        z1, x1, y1, _ = table[i + 1]
        if z1 <= z <= z0:
            t = (z0 - z) / (z0 - z1)
            return x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
    return table[-1][1], table[-1][2]


def front_point(z, sideways=0.0, out=0.0):
    """A point on the front face of the robe at height z, sideways in metres from centre."""
    hx, hy = ring_at(z)
    shrink = max(0.0, 1.0 - (abs(sideways) / max(hx, 1e-6)) ** SHAPE) ** (1.0 / SHAPE)
    return Vector((sideways, CY - hy * shrink - out, z))


def super_ring(bm, z, hx, hy, segs=SEGS, cy=CY):
    """One closed cross section, a superellipse so the corners stay full."""
    out = []
    for i in range(segs):
        a = math.tau * i / segs
        ca, sa = math.cos(a), math.sin(a)
        px = math.copysign(abs(ca) ** (2.0 / SHAPE), ca)
        py = math.copysign(abs(sa) ** (2.0 / SHAPE), sa)
        out.append(bm.verts.new((hx * px, cy + hy * py, z)))
    return out


def loft(bm, rings, mat=0, closed=True):
    n = len(rings[0])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(n if closed else n - 1):
            k2 = (k + 1) % n
            f = bm.faces.new((a[k], b[k], b[k2], a[k2]))
            f.material_index = mat


def rim(bm, ring, inward, drop, mat=0):
    """A short flange folded back inside an opening, so the edge is not paper thin."""
    centre = Vector((0, 0, 0))
    for v in ring:
        centre += v.co
    centre /= len(ring)
    inner = []
    for v in ring:
        d = (v.co - centre)
        d.z = 0.0
        if d.length > 1e-6:
            d.normalize()
        inner.append(bm.verts.new(v.co - d * inward + Vector((0, 0, drop))))
    loft(bm, [ring, inner], mat)
    return inner


def tube(bm, p0, p1, r0, r1, segs=8, rings=3, mat=0):
    """A tapered tube from p0 to p1, ring sections perpendicular to the axis."""
    axis = (p1 - p0)
    length = axis.length
    axis.normalize()
    up = Vector((0, 0, 1))
    if abs(axis.dot(up)) > 0.95:
        up = Vector((0, 1, 0))
    side = axis.cross(up).normalized()
    up = side.cross(axis).normalized()

    out = []
    for j in range(rings + 1):
        t = j / rings
        c = p0 + axis * (length * t)
        r = r0 + (r1 - r0) * t
        ring = []
        for i in range(segs):
            a = math.tau * i / segs
            ring.append(bm.verts.new(c + side * (math.cos(a) * r) + up * (math.sin(a) * r)))
        out.append(ring)
    loft(bm, out, mat)
    return out


def ribbon(bm, path, width, thick, mat=0):
    """A flat band swept along a path, kept facing outward from the body axis."""
    rings = []
    for i, p in enumerate(path):
        nxt = path[min(i + 1, len(path) - 1)]
        prv = path[max(i - 1, 0)]
        t = (nxt - prv)
        if t.length < 1e-6:
            t = Vector((0, 0, 1))
        t.normalize()
        outward = Vector((p.x, p.y - CY, 0.0))
        if outward.length < 1e-6:
            outward = Vector((0, -1, 0))
        outward.normalize()
        across = t.cross(outward).normalized()
        rings.append([
            bm.verts.new(p + across * width * 0.5),
            bm.verts.new(p + across * width * 0.5 + outward * thick),
            bm.verts.new(p - across * width * 0.5 + outward * thick),
            bm.verts.new(p - across * width * 0.5),
        ])
    loft(bm, rings, mat)
    for r in (rings[0], rings[-1]):
        f = bm.faces.new(r)
        f.material_index = mat
    return rings


def plate(bm, centre, half_w, half_h, thick, mat=0):
    """A patch pocket: a rounded slab lying against the skirt."""
    outward = Vector((0.0, -1.0, 0.0))
    across = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    inner, outer = [], []
    for sx, sy in corners:
        base = centre + across * (half_w * sx) + up * (half_h * sy)
        inner.append(bm.verts.new(base))
        outer.append(bm.verts.new(base + outward * thick))
    f = bm.faces.new(outer)
    f.material_index = mat
    for i in range(4):
        j = (i + 1) % 4
        f = bm.faces.new((inner[i], inner[j], outer[j], outer[i]))
        f.material_index = mat


# ------------------------------------------------------------------------- robe


def build(body, arm, mat_pink, mat_trim):
    bm = bmesh.new()

    # Body and skirt, one closed tube.
    rings = [super_ring(bm, z, hx, hy, cy=cy) for z, hx, hy, cy in FITTED]
    loft(bm, rings, 0)
    rim(bm, rings[0], 0.055, -0.045, 0)      # neck opening
    rim(bm, rings[-1], 0.050, 0.040, 0)      # hem

    # Sleeves, along the real arm bones, stopping three quarters down the forearm.
    for side in (1.0, -1.0):
        sh = arm.data.bones["upper_arm.L"].head_local.copy()
        el = arm.data.bones["forearm.L"].head_local.copy()
        wr = arm.data.bones["forearm.L"].tail_local.copy()
        for p in (sh, el, wr):
            p.x *= side
        end = el + (wr - el) * 0.72
        # Started a little inboard of the shoulder joint so the sleeve buries itself in the
        # body tube instead of leaving a ring of shirt showing at the join.
        sh -= (el - sh).normalized() * 0.090
        upper = tube(bm, sh, el, 0.152, 0.100, 8, 3, 0)
        lower = tube(bm, el, end, 0.098, 0.090, 8, 2, 0)
        loft(bm, [upper[-1], lower[0]], 0)
        # Folded cuff: a slightly wider band at the end, in the trim colour.
        cuff = tube(bm, end - (wr - el).normalized() * 0.055, end, 0.106, 0.104, 8, 1, 1)
        rim(bm, cuff[-1], 0.030, 0.0, 1)

    # Shawl collar: round the nape, over each shoulder, then down the front to the belt,
    # laid on the robe surface through front_point so it follows whatever shape the table
    # gives. The two sides meet at the waist, which is what makes it read as a wrap.
    for side in (1.0, -1.0):
        nape_hx, nape_hy = ring_at(2.13)
        path = [
            Vector((0.010 * side, CY + nape_hy * 0.92, 2.130)),
            Vector((0.100 * side, CY + nape_hy * 0.55, 2.150)),
            Vector((0.140 * side, CY - nape_hy * 0.30, 2.125)),
            front_point(2.030, 0.150 * side, 0.004),
            front_point(1.900, 0.115 * side, 0.004),
            front_point(1.770, 0.075 * side, 0.004),
            front_point(1.655, 0.028 * side, 0.004),
        ]
        ribbon(bm, path, COLLAR_W, COLLAR_T, 1)

    # Belt: a flattened ring at the waist, a knot at the front, two hanging ends.
    belt_hx, belt_hy = ring_at(BELT_Z)
    belt_hx += 0.012
    belt_hy += 0.012
    b_top = super_ring(bm, BELT_Z + 0.026, belt_hx, belt_hy)
    b_out = super_ring(bm, BELT_Z, belt_hx + 0.016, belt_hy + 0.016)
    b_bot = super_ring(bm, BELT_Z - 0.026, belt_hx, belt_hy)
    loft(bm, [b_top, b_out, b_bot], 1)

    knot = Vector((0.0, CY - belt_hy - 0.030, BELT_Z))
    tube(bm, knot + Vector((-0.062, 0, 0)), knot + Vector((0.062, 0, 0)), 0.046, 0.046, 6, 1, 1)
    for side in (1.0, -1.0):
        tail = [
            knot + Vector((0.030 * side, -0.010, -0.020)),
            knot + Vector((0.050 * side, -0.015, -0.130)),
            knot + Vector((0.038 * side, -0.010, -0.260)),
            knot + Vector((0.055 * side, -0.005, -0.380)),
        ]
        ribbon(bm, tail, 0.052, 0.020, 1)

    # Patch pockets, sat on the front of the skirt.
    for side in (1.0, -1.0):
        base = front_point(1.140, 0.175 * side, 0.0)
        plate(bm, base, 0.078, 0.082, 0.020, 0)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    me = bpy.data.meshes.new(NAME)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat_pink)
    me.materials.append(mat_trim)
    for p in me.polygons:
        p.use_smooth = False

    obj = bpy.data.objects.new(NAME, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------------- weights


def transfer_weights(robe, body):
    """Copy the body's vertex groups onto the robe, nearest vertex wins.

    Below the hip the lookup is done at hip height, so the skirt binds to the pelvis and
    swings as one cone instead of scissoring with the legs.
    """
    for g in body.vertex_groups:
        robe.vertex_groups.new(name=g.name)

    verts = body.data.vertices
    tree = kdtree.KDTree(len(verts))
    for i, v in enumerate(verts):
        tree.insert(v.co, i)
    tree.balance()

    by_name = {g.index: g.name for g in body.vertex_groups}
    target = {g.name: g for g in robe.vertex_groups}

    for rv in robe.data.vertices:
        probe = rv.co.copy()
        if probe.z < HIP_Z:
            probe.z = HIP_Z
        _, idx, _ = tree.find(probe)
        for g in verts[idx].groups:
            name = by_name.get(g.group)
            if name and g.weight > 0.0:
                target[name].add([rv.index], g.weight, "REPLACE")

    mod = robe.modifiers.new("Armature", "ARMATURE")
    mod.object = body.parent if body.parent else None
    return mod


# ------------------------------------------------------------------------ output


def export(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    # Skinned, so bake_space_transform stays OFF: it is the documented way to wreck an
    # armature. The axis conversion lands on the imported root and CrewEquip composes with
    # it rather than overwriting it (05_ART/CHARACTERS.md).
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True,
        global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        add_leaf_bones=False, bake_anim=False,
        axis_forward="-Z", axis_up="Y",
    )


def render_turnaround(folder):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 760, 1080
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.93, 0.93, 0.94)

    target = Vector((0.0, 0.0, 1.45))
    cam_data = bpy.data.cameras.new("C")
    cam = bpy.data.objects.new("C", cam_data)
    scene.collection.objects.link(cam)
    cam_data.lens = 56
    scene.camera = cam

    # The front of the character is +Y in world space: the imported object carries a
    # rotation, so the local -Y where the face sits comes out the other way round.
    os.makedirs(folder, exist_ok=True)
    for name, deg in (("1_front", 90.0), ("2_three_quarter", 140.0),
                      ("3_side", 180.0), ("4_back", 270.0)):
        a = math.radians(deg)
        cam.location = target + Vector((math.cos(a) * 3.6, math.sin(a) * 3.6, 0.30))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(folder, "bathrobe_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDERED " + scene.render.filepath)


def main():
    body, arm = import_body()
    FITTED[:] = fit_rings(body)
    for z, hx, hy, cy in FITTED:
        print(f"RING z={z:.2f} hx={hx:.3f} hy={hy:.3f} cy={cy:+.3f}")
    robe = build(body, arm, material("robe_pink", PINK), material("robe_trim", TRIM))
    mod = transfer_weights(robe, body)
    if mod.object is None:
        mod.object = arm
    robe.parent = arm

    tris = sum(len(p.vertices) - 2 for p in robe.data.polygons)
    print(f"ROBE_TRIS {tris}")
    print("ROBE_DIMS " + " ".join(f"{v:.4f}" for v in robe.dimensions))
    print(f"ROBE_GROUPS {len(robe.vertex_groups)}")

    render_turnaround(os.path.dirname(PREVIEW))
    export([robe, arm], os.path.join(OUT, NAME + ".fbx"))

    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("ROBE_DONE")


if __name__ == "__main__":
    main()
