"""Model grandmother's bathrobe on the crew rig, to the reference the team supplied.

Replaces model_gown.py, which produced an open short-sleeved tunic rather than a bathrobe.
The reference asks for five things the tunic did not have: full length, a shawl collar, a
knotted belt, patch pockets and folded cuffs. Those are shapes, not parameters, so this is
built part by part instead of as one swept band.

Parts, all procedural, joined into one mesh:
  body      a closed tube from the neck to mid calf: a yoke sloping from the neck to the
            shoulder point, then flaring below the hips with soft vertical folds that grow
            toward the hem
  sleeves   one tube each along the arm bones, starting under the yoke, bending at the elbow,
            stopping at three quarters of the forearm
  cuffs     the last stretch of each sleeve, belled out progressively, the folded-back look
  collar    a shawl ribbon from the nape, round the neck opening, tapering down to the belt
  belt      a flattened ring, a knot at the front, two hanging ends
  pockets   two plates on the front of the skirt

Three decisions worth reading before changing anything:

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

3. **Cloth is a surface language, and here it is four cheap things.** The first version had
   the right silhouette and the team read it as samurai armour: a 12 sided superellipse with
   hard corners, parts meeting in steps, one wide square shoulder ring with the sleeve tops
   standing above it, a cuff stepping out of the sleeve, a skirt that was a smooth cone.
   What says cloth instead: a rounder section (SEGS, SHAPE); a yoke that slopes from the neck
   to the shoulder point 3 cm above the body's own measured shoulder line, with the sleeves
   emerging from under it; a sleeve that is one tube through the elbow and bells into its
   cuff; and a sinusoidal fold on every skirt ring, growing toward the hem (FOLDS, FOLD_AMP).
   None of it is texture: the project is flat colour and the reference's heart pattern is
   deliberately absent. The second version fixed all four and the team still read armour:
   flat shaded, twenty vertical facets are lamellar plates. It shades smooth now, with only
   the rims kept sharp (SMOOTH_ANGLE).

Weights everywhere else are transferred from the nearest body vertex, so the robe deforms
with the body without a single weight painted by hand. Only the sleeves may take an arm: the
first export bound the widest hem folds to the hanging hands, and they flew off with the arms
as needles the moment the body moved in the map (FAR_ARM_GROUPS, check_weights).

Nothing here is judged by eye. The ring table is a floor, the real sizes are measured off
the body (measure_torso, fit_rings), and check_enclosure refuses to export if any body vertex
the robe is meant to cover ends up outside it. Every real fault of this lot so far was found
by a number and none by a render.

Run:

    blender --background --python tools/blender/model_bathrobe.py
"""

import bpy
import bmesh
import math
import os
from mathutils import Vector, kdtree

SRC = r"C:\dev\game-project\UnityProject\Assets\Floreswa\Models\male01_1.fbx"
OUT = r"C:\dev\game-project\UnityProject\Assets\_Project\Art\Crew"
BLEND = r"C:\dev\game-project\_ArtSource\Crew_Bathrobe.blend"
PREVIEW = r"C:\dev\game-project\_ArtSource\preview_bathrobe.png"

NAME = "SM_Crew_Chest_Bathrobe"

PINK = (0.95, 0.58, 0.72, 1.0)
TRIM = (0.88, 0.36, 0.55, 1.0)      # placeholder, Unity paints this with the crew colour

SEGS = 20          # around the body. 12 read as a barrel with corners.
SHAPE = 2.05       # superellipse exponent. 2.4 kept the corners full and they read as plate;
                   # 2.0 is a plain ellipse, this keeps the faintest flat at the sides.
SMOOTH_ANGLE = 45  # degrees. Faces meeting at less than this shade smooth, so only the rims
                   # stay sharp. Flat shaded, the twenty vertical facets read as lamellar
                   # plates, which is where "samurai" came from (v2 review, 2026-09-25).
CY = -0.035        # the body centre line drifts back about this much through the torso
HIP_Z = 1.36       # below this the skirt is weighted at hip height, see the docstring

# Skirt folds. Each ring's radius is modulated around the circumference by a sinusoid whose
# amplitude is zero at FOLD_TOP and reaches FOLD_AMP, a fraction of the radius, at the hem.
# Odd counts use a sine and even counts a cosine, so the pattern mirrors left to right and the
# centre front always sits in a trough, under the belt ends. FOLDS2 / FOLD_AMP2 add a finer
# second harmonic when non zero.
FOLDS = 5
FOLD_AMP = 0.18    # 0.06 was invisible: flat shading needs the facets to turn by 25 deg or so
FOLDS2 = 9         # a second harmonic breaks the lathe regularity of a single sine
FOLD_AMP2 = 0.05
FOLD_TOP = 1.50    # just under the belt
FOLD_POW = 1.0     # 1 grows linearly, higher keeps the folds off the hips
HEM_WAVE = 0.030   # the hem edge itself dips at each fold peak; a flat hem line reads as a rim

SLEEVE_SEGS = 10   # 8 read as an octagonal prism

# (z, half width, half depth). These are floors: fit_rings widens any ring the measured body
# would poke through, and pads it for the fold trough. The four rings above the chest are the
# yoke. The body's own shoulder line runs from z 2.25 at the neck to 2.18 at x 0.30 (measured
# 2026-09-20) and these follow it about 3 cm above. The first version had a single ring at
# (2.08, 0.328) and the sleeve tops stood above it, which is what a pauldron is.
BODY_RINGS = [
    (2.245, 0.150, 0.165),  # neck opening
    (2.225, 0.235, 0.180),  # yoke, over the trapezius
    (2.200, 0.295, 0.190),  # the shoulder point
    (2.120, 0.305, 0.205),  # the side drops straight from there, the sleeve covers the deltoid
    (1.950, 0.290, 0.210),  # chest
    (1.780, 0.256, 0.198),
    (1.620, 0.246, 0.190),  # the waist, where the belt sits
    (1.450, 0.263, 0.206),
    (1.250, 0.291, 0.226),
    (1.000, 0.318, 0.246),
    (0.700, 0.342, 0.263),
    (0.450, 0.355, 0.272),
    (0.420, 0.352, 0.270),  # the hem
]
HEM_Z = BODY_RINGS[-1][0]
NECK_Z = BODY_RINGS[0][0]

BELT_Z = 1.62
COLLAR_T = 0.020
# The shawl narrows from the shoulders to the belt. 0.075 all the way down read as two straps;
# a constant 0.135 read as a bib. Round the neck it leans back over the yoke (COLLAR_TILT, in
# degrees off the horizontal): standing straight up it read as a gorget, which is armour again.
COLLAR_W = [0.095, 0.110, 0.125, 0.135, 0.135, 0.125, 0.110, 0.092, 0.075]
COLLAR_TILT = [50, 50, 45, 40, 25, 8, 0, 0, 0]

# Sleeve profile: (fraction along the bone chain, radius, material). 0 to 1 is the upper arm,
# 1 to 2 the forearm, below 0 is inside the body. The first ring is small on purpose: its top
# has to stay under the yoke slope, so the sleeve comes out from beneath the shoulder instead
# of sitting on it. The cuff is the belled tail in the trim colour; a cuff that was a wider
# ring butted onto the sleeve end read as an armband.
SLEEVE_END = 1.72
SLEEVE = [
    (-0.22, 0.085, 0),
    (0.05, 0.150, 0),            # over the deltoid
    (0.50, 0.130, 0),
    (1.00, 0.104, 0),            # elbow
    (SLEEVE_END - 0.30, 0.092, 1),   # cuff start
    (SLEEVE_END - 0.15, 0.100, 1),
    (SLEEVE_END, 0.110, 1),          # cuff edge
]
CUFF_RIM = 0.030

MIN_CLEARANCE = 0.008   # metres, in the body's frame. Under this the skin fights the cloth.


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


def dominant_group(body):
    """A function giving the vertex group with the largest weight on a body vertex."""
    gi = {g.index: g.name for g in body.vertex_groups}

    def dominant(v):
        best, bw = "", -1.0
        for g in v.groups:
            if g.weight > bw:
                bw, best = g.weight, gi.get(g.group, "")
        return best
    return dominant


ARM_GROUPS = ("upper_arm", "forearm", "hand", "shoulder")
HEAD_GROUPS = ("spine.006",)


def measure_torso(body):
    """The body vertices each ring answers for, arms and head excluded.

    Hand written ring sizes put the waist inside the torso and the shirt showed through two
    wedges at the belt. Measuring is the only way to be sure a garment encloses what it is
    supposed to enclose, exactly as with the slipper and the boot. A ring answers for every
    vertex between its two neighbouring rings: the surface between two rings is their
    interpolation, and it holds a vertex only if both of them do. The shoulders are left out
    because the yoke has to pass over them rather than around them; check_enclosure settles
    those per vertex against the yoke and the sleeves instead.
    """
    dominant = dominant_group(body)

    def excluded(v):
        d = dominant(v)
        return d in HEAD_GROUPS or any(d.startswith(a) for a in ARM_GROUPS)

    torso = [v.co for v in body.data.vertices if not excluded(v)]
    zs = [z for z, _hx, _hy in BODY_RINGS]
    out = {}
    for i, z in enumerate(zs):
        # 60 % of the way to each neighbour: a vertex in the middle fifth of a gap is held by
        # both rings, one nearer a ring is held by that ring with most of its PAD, and
        # check_enclosure has the last word. Answering for the whole gap put 6 cm of air at
        # the waist, because each waist ring then had to clear the hips.
        lo = z - 0.6 * (z - zs[i + 1]) if i + 1 < len(zs) else z - 0.03
        hi = z + 0.6 * (zs[i - 1] - z) if i > 0 else z + 0.06
        near = [c for c in torso if lo - 1e-6 <= c.z <= hi + 1e-6]
        # The centre needs a few vertices to mean anything; the yoke rings are close together
        # and may hold one or two, so the centre is read off a wider band there. Every vertex
        # in the ring's own interval still counts for its size.
        wide = near if len(near) >= 4 else [c for c in torso if abs(c.z - z) <= 0.12]
        out[z] = (near, wide)
    return out


def fit_rings(body):
    """BODY_RINGS, widened wherever the measured body would otherwise poke through.

    The first version took the widest x and the y extent separately, and a rounder section
    then lost the corner where the hip actually sits: two pelvis vertices ended up exactly on
    the surface. Each vertex is now tested along its own direction, with the superellipse
    norm, and the ring is scaled until every one of them has PAD of cloth in front of it.
    The fold trough dips inward by up to FOLD_AMP of the radius, so a ring in the fold zone
    is widened by that much again: the clearance is owed at the trough, not at the mean.
    """
    PAD = 0.030
    measured = measure_torso(body)
    rings = []
    for z, hx, hy in BODY_RINGS:
        near, wide = measured.get(z, ([], []))
        cy = CY
        trough = fold_envelope(z) * (abs(FOLD_AMP) + abs(FOLD_AMP2))
        if len(wide) >= 4:
            cy = (min(c.y for c in wide) + max(c.y for c in wide)) * 0.5
        # Grow the axis the worst vertex leans on, until every vertex has its PAD. Growing
        # both at once let the back of the neck base widen the yoke sideways, which put the
        # shoulder bulge back.
        for _ in range(40):
            worst, axis = 1.0, None
            for c in near:
                rx, ry = abs(c.x), abs(c.y - cy)
                r = math.hypot(rx, ry)
                if r < 1e-6:
                    continue
                norm = ((rx / hx) ** SHAPE + (ry / hy) ** SHAPE) ** (1.0 / SHAPE)
                need = norm * (1.0 + PAD / r)
                if need > worst:
                    worst, axis = need, ("x" if rx / hx >= ry / hy else "y")
            if worst <= 1.0 + 1e-4:
                break
            if axis == "x":
                hx *= worst
            else:
                hy *= worst
        rings.append((z, hx / (1.0 - trough), hy / (1.0 - trough), cy))
    return rings


def fold_envelope(z):
    """How much of FOLD_AMP applies at a height: 0 above FOLD_TOP, 1 at the hem."""
    t = (FOLD_TOP - z) / (FOLD_TOP - HEM_Z)
    t = min(1.0, max(0.0, t))
    return t ** FOLD_POW


def harmonic(k, a):
    # Sine for odd counts, cosine for even ones: both are symmetric under x -> -x.
    return math.sin(k * a) if k % 2 else math.cos(k * a)


def fold_scale(a, z):
    """Radial scale of the skirt at ring parameter a and height z. 1.0 means no fold."""
    env = fold_envelope(z)
    if env <= 0.0:
        return 1.0
    return 1.0 + env * (FOLD_AMP * harmonic(FOLDS, a) + FOLD_AMP2 * harmonic(FOLDS2, a))


FITTED = []


def ring_at(z):
    """The robe's half width, half depth and centre at a height, interpolated from the table.

    Everything that has to sit ON the robe (collar, belt, pockets) is placed through this
    rather than with its own numbers, so changing the silhouette moves them with it.
    """
    table = FITTED if FITTED else [(z0, x0, y0, CY) for z0, x0, y0 in BODY_RINGS]
    if z >= table[0][0]:
        return table[0][1], table[0][2], table[0][3]
    if z <= table[-1][0]:
        return table[-1][1], table[-1][2], table[-1][3]
    for i in range(len(table) - 1):
        z0, x0, y0, c0 = table[i]
        z1, x1, y1, c1 = table[i + 1]
        if z1 <= z <= z0:
            t = (z0 - z) / (z0 - z1)
            return x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, c0 + (c1 - c0) * t
    return table[-1][1], table[-1][2], table[-1][3]


def super_xy(a):
    ca, sa = math.cos(a), math.sin(a)
    px = math.copysign(abs(ca) ** (2.0 / SHAPE), ca)
    py = math.copysign(abs(sa) ** (2.0 / SHAPE), sa)
    return px, py


def ring_point(z, a, out=0.0):
    """A point on the robe surface at height z and ring parameter a, pushed out by `out`.

    a = pi/2 is the centre back, 0 the wearer's left side, -pi/2 the centre front.
    """
    hx, hy, cy = ring_at(z)
    px, py = super_xy(a)
    s = fold_scale(a, z)
    p = Vector((hx * px * s, cy + hy * py * s, z))
    n = Vector((p.x, p.y - cy, 0.0))
    if n.length > 1e-6:
        n.normalize()
    return p + n * out


def front_point(z, sideways=0.0, out=0.0):
    """A point on the front face of the robe at height z, sideways in metres from centre."""
    hx, hy, cy = ring_at(z)
    u = min(1.0, abs(sideways) / max(hx, 1e-6))
    shrink = (1.0 - u ** SHAPE) ** (1.0 / SHAPE)
    ca = math.copysign(u ** (SHAPE / 2.0), sideways) if sideways else 0.0
    a = -math.acos(max(-1.0, min(1.0, ca)))
    s = fold_scale(a, z)
    return Vector((sideways * s, cy - hy * shrink * s - out, z))


# --------------------------------------------------------------------- primitives

# Every part records its faces with a rule for which way they face, so the mesh arrives in
# Unity with its normals out. Workbench draws both sides and would hide an inverted face;
# the standard shader culls it. (faces, function of a face centre giving the outward direction)
PARTS = []


def super_ring(bm, z, hx, hy, segs=None, cy=CY):
    """One closed cross section, a superellipse with the skirt fold applied."""
    segs = segs or SEGS
    out = []
    for i in range(segs):
        a = math.tau * i / segs
        px, py = super_xy(a)
        s = fold_scale(a, z)
        out.append(bm.verts.new((hx * px * s, cy + hy * py * s, z)))
    return out


def loft(bm, rings, mat=0, closed=True):
    n = len(rings[0])
    faces = []
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(n if closed else n - 1):
            k2 = (k + 1) % n
            f = bm.faces.new((a[k], b[k], b[k2], a[k2]))
            f.material_index = mat
            faces.append(f)
    return faces


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
    faces = loft(bm, [ring, inner], mat)
    # The visible side of a flange is the side of the opening it folds away from.
    PARTS.append((faces, lambda c, up=Vector((0, 0, -1.0 if drop > 0 else 1.0)): up))
    return inner


def rim_in_plane(bm, ring, centre, axis, inward, mat=0):
    """The same flange for a ring that is not horizontal: shrunk in its own plane."""
    inner = []
    for v in ring:
        d = v.co - centre
        inner.append(bm.verts.new(centre + d * (1.0 - inward / max(d.length, 1e-6))))
    faces = loft(bm, [ring, inner], mat)
    PARTS.append((faces, lambda c, ax=axis.normalized(): ax))
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
    faces = loft(bm, out, mat)

    def outward(c, p0=p0.copy(), ax=axis.copy()):
        rel = c - p0
        return rel - ax * rel.dot(ax)
    PARTS.append((faces, outward))
    return out


def poly_tube(bm, pts, radii, segs, mats):
    """One tube through a chain of points, rings tilted to bisect each bend.

    The frame is carried along the chain rather than rebuilt per segment, so the rings do not
    twist against each other at the elbow. mats[i] is the material of the band after pts[i].
    """
    n = len(pts)
    tangents = []
    for i in range(n):
        t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        tangents.append(t.normalized())
    up = Vector((0, 0, 1))
    if abs(tangents[0].dot(up)) > 0.95:
        up = Vector((0, 1, 0))
    side = tangents[0].cross(up).normalized()

    rings = []
    for i in range(n):
        t = tangents[i]
        side = (side - t * side.dot(t)).normalized()
        upv = side.cross(t).normalized()
        ring = []
        for k in range(segs):
            a = math.tau * k / segs
            ring.append(bm.verts.new(pts[i] + side * (math.cos(a) * radii[i]) + upv * (math.sin(a) * radii[i])))
        rings.append(ring)

    for i in range(n - 1):
        faces = loft(bm, [rings[i], rings[i + 1]], mats[i])

        def outward(c, a=pts[i].copy(), b=pts[i + 1].copy()):
            e = b - a
            t = max(0.0, min(1.0, (c - a).dot(e) / max(e.length_squared, 1e-9)))
            return c - (a + e * t)
        PARTS.append((faces, outward))
    return rings


def ribbon(bm, path, widths, thick, mat=0, tilts=None):
    """A flat band swept along a path, facing outward from the body axis.

    tilts, in degrees per point, lean the band back from the horizontal outward direction
    toward straight up, so a collar can lie over a shoulder instead of standing on it.
    """
    if not isinstance(widths, (list, tuple)):
        widths = [widths] * len(path)
    tilts = tilts or [0.0] * len(path)
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
        tilt = math.radians(tilts[i])
        outward = (outward * math.cos(tilt) + Vector((0, 0, math.sin(tilt)))).normalized()
        across = t.cross(outward).normalized()
        w = widths[i]
        rings.append([
            bm.verts.new(p + across * w * 0.5),
            bm.verts.new(p + across * w * 0.5 + outward * thick),
            bm.verts.new(p - across * w * 0.5 + outward * thick),
            bm.verts.new(p - across * w * 0.5),
        ])
    for i in range(len(rings) - 1):
        faces = loft(bm, [rings[i], rings[i + 1]], mat)
        mid = (path[i] + path[i + 1]) * 0.5

        def outward(c, m=mid):
            return c - m
        PARTS.append((faces, outward))
    for r, d in ((rings[0], path[0] - path[1]), (rings[-1], path[-1] - path[-2])):
        f = bm.faces.new(r)
        f.material_index = mat
        PARTS.append(([f], lambda c, d=d.normalized(): d))
    return rings


def plate(bm, z, sideways, half_w, half_h, thick, mat=0):
    """A patch pocket: a slab lying against the skirt.

    Each corner is laid on the robe surface through front_point, so the pocket bends with the
    fold under it instead of standing off the skirt where a trough runs behind it.
    """
    outward = Vector((0.0, -1.0, 0.0))
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    inner, outer = [], []
    for sx, sy in corners:
        base = front_point(z + half_h * sy, sideways + half_w * sx, 0.0)
        inner.append(bm.verts.new(base))
        outer.append(bm.verts.new(base + outward * thick))
    centre = (inner[0].co + inner[1].co + inner[2].co + inner[3].co) * 0.25
    faces = [bm.faces.new(outer)]
    for i in range(4):
        j = (i + 1) % 4
        faces.append(bm.faces.new((inner[i], inner[j], outer[j], outer[i])))
    for f in faces:
        f.material_index = mat
    mid = centre + outward * (thick * 0.5)
    PARTS.append((faces, lambda c, m=mid: c - m))


# ------------------------------------------------------------------------- robe

RING_SNAPSHOT = []   # (z, [Vector]) per body ring, for check_enclosure
SLEEVES = []         # (points, radii) per sleeve, for check_enclosure
SLEEVE_VERTS = []    # vertex indices of the sleeves and cuffs, the only part that may follow an arm

# Bones that swing far from the torso. Only the sleeves may follow them. A skirt vertex looked up
# at hip height lands next to the hands in the bind pose, and bound to a hand it flies off with
# the arm: the two needles seen on the robe in the map, 2026-09-25.
FAR_ARM_GROUPS = ("upper_arm", "forearm", "hand")


def sleeve_path(arm, side):
    sh = arm.data.bones["upper_arm.L"].head_local.copy()
    el = arm.data.bones["forearm.L"].head_local.copy()
    wr = arm.data.bones["forearm.L"].tail_local.copy()
    for p in (sh, el, wr):
        p.x *= side
    pts, radii, mats = [], [], []
    for f, r, m in SLEEVE:
        p = sh + (el - sh) * f if f <= 1.0 else el + (wr - el) * (f - 1.0)
        pts.append(p)
        radii.append(r)
        mats.append(m)
    return pts, radii, mats


def build(body, arm, mat_pink, mat_trim):
    bm = bmesh.new()
    PARTS.clear()
    RING_SNAPSHOT.clear()
    SLEEVES.clear()
    SLEEVE_VERTS.clear()

    # Body and skirt, one closed tube.
    rings = [super_ring(bm, z, hx, hy, cy=cy) for z, hx, hy, cy in FITTED]
    RING_SNAPSHOT[:] = [(z, [v.co.copy() for v in ring]) for (z, _, _, _), ring in zip(FITTED, rings)]
    for k, v in enumerate(rings[-1]):
        v.co.z -= HEM_WAVE * harmonic(FOLDS, math.tau * k / len(rings[-1]))
    faces = loft(bm, rings, 0)

    def body_out(c):
        _hx, _hy, cy = ring_at(c.z)
        return Vector((c.x, c.y - cy, 0.0))
    PARTS.append((faces, body_out))
    rim(bm, rings[0], 0.055, -0.045, 0)      # neck opening
    rim(bm, rings[-1], 0.050, 0.040, 0)      # hem

    # Sleeves: one tube each, from under the yoke through the elbow to the cuff. Their vertices
    # are the ones created here: bmesh appends in order and nothing below deletes any.
    first = len(bm.verts)
    for side in (1.0, -1.0):
        pts, radii, mats = sleeve_path(arm, side)
        srings = poly_tube(bm, pts, radii, SLEEVE_SEGS, mats)
        rim_in_plane(bm, srings[-1], pts[-1], pts[-1] - pts[-2], CUFF_RIM, 1)
        SLEEVES.append((pts, radii))
    SLEEVE_VERTS[:] = range(first, len(bm.verts))

    # Shawl collar: round the nape on the neck ring, over each shoulder, then down the front
    # to the belt, laid on the robe surface through ring_point / front_point so it follows
    # whatever shape the table gives. The two sides meet at the waist, which is what makes it
    # read as a wrap. Ring parameters are for the wearer's left; the right is mirrored.
    for side in (1.0, -1.0):
        def mirror(a):
            return a if side > 0 else math.pi - a
        path = [ring_point(z, mirror(a), 0.004) for z, a in (
            (2.230, 1.55),    # nape, a hair off centre so the two sides meet without overlapping
            (2.235, 0.90),    # over the trapezius
            (2.240, 0.10),    # beside the neck
            (2.220, -0.55),   # front of the shoulder
            (2.150, -0.95),   # turning down the chest
        )]
        path += [
            front_point(2.030, 0.140 * side, 0.004),
            front_point(1.900, 0.105 * side, 0.004),
            front_point(1.770, 0.068 * side, 0.004),
            front_point(1.655, 0.028 * side, 0.004),
        ]
        ribbon(bm, path, COLLAR_W, COLLAR_T, 1, tilts=COLLAR_TILT)

    # Belt: a flattened ring at the waist, a knot at the front, two hanging ends.
    belt_hx, belt_hy, belt_cy = ring_at(BELT_Z)
    belt_hx += 0.012
    belt_hy += 0.012
    b_top = super_ring(bm, BELT_Z + 0.026, belt_hx, belt_hy, cy=belt_cy)
    b_out = super_ring(bm, BELT_Z, belt_hx + 0.016, belt_hy + 0.016, cy=belt_cy)
    b_bot = super_ring(bm, BELT_Z - 0.026, belt_hx, belt_hy, cy=belt_cy)
    faces = loft(bm, [b_top, b_out, b_bot], 1)
    PARTS.append((faces, lambda c, cy=belt_cy: Vector((c.x, c.y - cy, 0.0))))

    knot = Vector((0.0, belt_cy - belt_hy - 0.030, BELT_Z))
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
        plate(bm, 1.140, 0.175 * side, 0.078, 0.082, 0.020, 0)

    orient_outward(bm)

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


def orient_outward(bm):
    """Flip every face whose normal disagrees with its part's outward rule."""
    bm.normal_update()
    flipped = 0
    for faces, rule in PARTS:
        for f in faces:
            if f.normal.dot(rule(f.calc_center_median())) < 0.0:
                f.normal_flip()
                flipped += 1
    print(f"ROBE_FLIPPED {flipped} of {len(bm.faces)} faces turned outward")


# ---------------------------------------------------------------------- checks


def tube_clearance(p):
    """How far inside the body tube a point is, in metres; negative means outside."""
    snap = RING_SNAPSHOT
    if p.z > snap[0][0] or p.z < snap[-1][0]:
        return None
    poly = None
    for i in range(len(snap) - 1):
        z0, r0 = snap[i]
        z1, r1 = snap[i + 1]
        if z1 <= p.z <= z0:
            t = (z0 - p.z) / max(z0 - z1, 1e-9)
            poly = [(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t) for a, b in zip(r0, r1)]
            break
    if poly is None:
        return None
    n = len(poly)
    cx = sum(x for x, _ in poly) / n
    cy = sum(y for _, y in poly) / n
    dx, dy = p.x - cx, p.y - cy
    dist = math.hypot(dx, dy)
    if dist < 1e-9:
        return min(math.hypot(x - cx, y - cy) for x, y in poly)
    ux, uy = dx / dist, dy / dist
    R = None
    for k in range(n):
        ax, ay = poly[k]
        bx, by = poly[(k + 1) % n]
        ex, ey = bx - ax, by - ay
        den = ux * ey - uy * ex
        if abs(den) < 1e-12:
            continue
        s = ((ax - cx) * ey - (ay - cy) * ex) / den
        w = ((ax - cx) * uy - (ay - cy) * ux) / den
        if s > 0.0 and -1e-9 <= w <= 1.0 + 1e-9:
            R = s if R is None else min(R, s)
    return None if R is None else R - dist


def sleeve_clearance(p, sleeve):
    pts, radii = sleeve
    best = -1e9
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        e = b - a
        t = max(0.0, min(1.0, (p - a).dot(e) / max(e.length_squared, 1e-9)))
        r = radii[i] + (radii[i + 1] - radii[i]) * t
        best = max(best, r - (p - (a + e * t)).length)
    return best


def check_enclosure(body, arm):
    """Every body vertex the robe is meant to cover must be inside it, by at least MIN_CLEARANCE.

    Torso and legs against the body tube; shoulders and upper arms against the tube or the
    sleeve, whichever holds them; forearms against the sleeve up to where it ends. The neck,
    the head, the hands and the feet are not covered and are not tested. Fails the run,
    because a robe the shirt shows through is not a robe.
    """
    dominant = dominant_group(body)
    el = arm.data.bones["forearm.L"].head_local
    wr = arm.data.bones["forearm.L"].tail_local
    fore = (wr - el)
    worst, fails, tested = {}, [], 0
    for v in body.data.vertices:
        d = dominant(v)
        c = v.co
        if d in ("spine.004", "spine.005") or d in HEAD_GROUPS:
            continue
        if any(d.startswith(g) for g in ("hand", "foot", "toe", "heel")):
            continue
        if c.z > NECK_Z - 0.005 or c.z < HEM_Z + 0.010:
            continue
        if d.startswith("forearm"):
            p = Vector((abs(c.x), c.y, c.z))
            t = (p - el).dot(fore) / fore.length_squared
            if t > SLEEVE_END - 1.0 - 0.02:
                continue
        cb = tube_clearance(c)
        cs = max(sleeve_clearance(c, s) for s in SLEEVES) if SLEEVES else -1e9
        if any(d.startswith(g) for g in ("shoulder", "upper_arm", "forearm")):
            clr = max(cb if cb is not None else -1e9, cs)
        else:
            clr = cb if cb is not None else -1e9
        tested += 1
        worst[d] = min(worst.get(d, 9.0), clr)
        if clr < MIN_CLEARANCE:
            fails.append((d, c.copy(), clr))
    for d in sorted(worst):
        print(f"CHECK clearance {d:12s} {worst[d] * 100:+6.1f} cm")
    print(f"CHECK enclosure {tested} vertices tested, {len(fails)} under {MIN_CLEARANCE * 100:.1f} cm")
    for d, c, clr in sorted(fails, key=lambda f: f[2])[:25]:
        print(f"CHECK   FAIL {d:12s} at ({c.x:+.3f}, {c.y:+.3f}, {c.z:.3f}) clearance {clr * 100:+.1f} cm")
    if fails:
        raise RuntimeError(f"{len(fails)} body vertices are not inside the robe, not exporting")


def report_measurements(robe):
    """The numbers the team judged by eye last time, printed so the next judgement is not."""
    z_hem, hem = RING_SNAPSHOT[-1]
    hx, hy, cy = ring_at(z_hem)
    scales, radii = [], []
    for k in range(len(hem)):
        a = math.tau * k / len(hem)
        px, py = super_xy(a)
        scales.append(fold_scale(a, z_hem))
        radii.append(math.hypot(hx * px, hy * py))
    mean_r = sum(radii) / len(radii)
    print(f"MEASURE hem fold peak to trough {(max(scales) - min(scales)) * mean_r * 100:.1f} cm "
          f"on a mean radius of {mean_r * 100:.1f} cm, {FOLDS} folds")
    z0, hx0, _, _ = FITTED[0]
    z2, hx2, _, _ = FITTED[2]
    print(f"MEASURE shoulder slope {math.degrees(math.atan2(z0 - z2, hx2 - hx0)):.0f} deg from the neck ring to the shoulder point")
    print(f"MEASURE section {SEGS} sides, exponent {SHAPE}, sleeves {SLEEVE_SEGS} sides")
    per = {}
    for p in robe.data.polygons:
        per[p.material_index] = per.get(p.material_index, 0) + len(p.vertices) - 2
    print("MEASURE tris per material " + ", ".join(f"slot {k}: {v}" for k, v in sorted(per.items())))
    tris = sum(per.values())
    print(f"ROBE_TRIS {tris}")
    if tris > 1250:
        print(f"WARNING {tris} triangles is over the 1.2K the reference garment spends")
    return tris


# ---------------------------------------------------------------------- weights


def transfer_weights(robe, body):
    """Copy the body's vertex groups onto the robe, nearest vertex wins.

    Below the hip the lookup is done at hip height, so the skirt binds to the pelvis and
    swings as one cone instead of scissoring with the legs.

    Only the sleeves may take an arm. Everything else looks up the torso alone, and any arm
    weight the chosen torso vertex carries is dropped, the rest renormalised: the widest fold
    peaks of the hem, lifted to hip height, sat nearer the hanging hands than the hips, and
    rode off with them as soon as the arms moved.
    """
    for g in body.vertex_groups:
        robe.vertex_groups.new(name=g.name)

    verts = body.data.vertices
    dominant = dominant_group(body)

    def tree_of(indices):
        t = kdtree.KDTree(len(indices))
        for i in indices:
            t.insert(verts[i].co, i)
        t.balance()
        return t

    everything = tree_of(range(len(verts)))
    torso = tree_of([i for i, v in enumerate(verts) if not dominant(v).startswith(FAR_ARM_GROUPS)])
    sleeves = set(SLEEVE_VERTS)

    by_name = {g.index: g.name for g in body.vertex_groups}
    target = {g.name: g for g in robe.vertex_groups}

    for rv in robe.data.vertices:
        sleeve = rv.index in sleeves
        probe = rv.co.copy()
        if probe.z < HIP_Z:
            probe.z = HIP_Z
        _, idx, _ = (everything if sleeve else torso).find(probe)
        picked = [(by_name.get(g.group), g.weight) for g in verts[idx].groups if g.weight > 0.0]
        picked = [(n, w) for n, w in picked if n and (sleeve or not n.startswith(FAR_ARM_GROUPS))]
        total = sum(w for _, w in picked) or 1.0
        for name, w in picked:
            target[name].add([rv.index], w / total, "REPLACE")

    mod = robe.modifiers.new("Armature", "ARMATURE")
    mod.object = body.parent if body.parent else None
    return mod


def check_weights(robe):
    """Every robe vertex is weighted, nothing under the hip line follows a shin or a foot, and
    nothing outside the sleeves follows an arm.

    The skirt is looked up at hip height, and on this rig a hip vertex carries some thigh
    weight along with the pelvis, so a share of thigh is expected there. It is printed rather
    than judged: what the skirt actually follows is a number the next person should see.
    """
    names = {g.index: g.name for g in robe.vertex_groups}
    lower = tuple(g.index for g in robe.vertex_groups if g.name.startswith(("shin", "foot", "toe", "heel")))
    far_arm = tuple(g.index for g in robe.vertex_groups if g.name.startswith(FAR_ARM_GROUPS))
    sleeves = set(SLEEVE_VERTS)
    unweighted, on_lower, on_arm, skirt = 0, 0, 0, 0
    share = {}
    for v in robe.data.vertices:
        if not any(g.weight > 0.0 for g in v.groups):
            unweighted += 1
        if v.index not in sleeves and any(g.group in far_arm and g.weight > 0.0 for g in v.groups):
            on_arm += 1
        if v.co.z < HIP_Z:
            skirt += 1
            total = sum(g.weight for g in v.groups) or 1.0
            for g in v.groups:
                share[names[g.group]] = share.get(names[g.group], 0.0) + g.weight / total
            if any(g.group in lower and g.weight > 0.0 for g in v.groups):
                on_lower += 1
    top = sorted(share.items(), key=lambda kv: -kv[1])[:6]
    print(f"CHECK weights {unweighted} unweighted vertices, {on_lower} skirt vertices bound below the thigh, "
          f"{on_arm} vertices outside the sleeves bound to an arm")
    print("MEASURE skirt weight share " + ", ".join(f"{n} {s / max(skirt, 1) * 100:.0f}%" for n, s in top))
    if unweighted or on_lower or on_arm:
        raise RuntimeError("weights are wrong, not exporting")


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
    # Cull back faces as Unity will: a face pointing the wrong way disappears here too,
    # rather than being drawn by Workbench and found later in the editor.
    scene.display.shading.show_backface_culling = True
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
        print(f"RING z={z:.3f} hx={hx:.3f} hy={hy:.3f} cy={cy:+.3f}")
    robe = build(body, arm, material("robe_pink", PINK), material("robe_trim", TRIM))
    mod = transfer_weights(robe, body)
    if mod.object is None:
        mod.object = arm
    robe.parent = arm

    check_enclosure(body, arm)
    check_weights(robe)
    report_measurements(robe)
    print("ROBE_DIMS " + " ".join(f"{v:.4f}" for v in robe.dimensions))
    print(f"ROBE_GROUPS {len(robe.vertex_groups)}")

    # Cloth shades smooth; the hem, cuff, collar and belt rims turn by more than SMOOTH_ANGLE
    # and stay sharp. The FBX carries these normals and Unity imports them as they are.
    robe.data.shade_smooth()
    robe.data.set_sharp_from_angle(angle=math.radians(SMOOTH_ANGLE))
    sharp = robe.data.attributes.get("sharp_edge")
    n_sharp = sum(1 for d in sharp.data if d.value) if sharp else 0
    print(f"MEASURE shading smooth below {SMOOTH_ANGLE} deg, "
          f"{n_sharp} of {len(robe.data.edges)} edges sharp")

    render_turnaround(os.path.dirname(PREVIEW))
    export([robe, arm], os.path.join(OUT, NAME + ".fbx"))

    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("ROBE_DONE")


if __name__ == "__main__":
    main()
