"""Model the grandmother, the owner of the house, on the crew rig, and author her idle.

Pierre, 2026-09-25: "on pourrait faire la grand-mere", and "attention au rig". She is the
one character who watches while the crew empties her house (ADR-004), so she has to stand
there, breathe and turn her head, and later walk and point. All of that is animation, and
the cheapest animation is the one the crew already uses. So she is not a new character: she
is male01_1.fbx, the crew body, with the armature left exactly as it is and the meshes
changed around it.

Parts, all procedural, joined into one skinned mesh:
  body      the crew mesh, reshaped: sloped shoulders, bust, belly, fuller hips, slim
            stockinged calves, smaller shoes, a smoothed hair cap. Same vertices, same
            weights: only positions and colours change. The T-shirt becomes the bodice.
  skirt     an A-line cone from the waist to mid calf, with a lining below the hip
  cardigan  an open shell over the torso, with thickness, sleeves to the wrist
  bun       a round bun at the back of the head, rigid on spine.006, the unanimated child
            of the head bone spine.005 that the crew's own hair is weighted to
  pearls    beads round the base of the neck, 16 of 18 kept (the cardigan covers the rest),
            128 triangles, inside the accessory budget of 05_ART/CHARACTERS.md
  idle      Grandma_Idle, 3.5 s loop at 24 fps: a slight stoop, forward head, two breaths,
            a weight shift onto the left leg with the feet held by leg IK, a slow look to
            her left, hands clasped in front of the belly

Six decisions worth reading before changing anything:

1. **The rig is not touched.** No bone is renamed, added, removed, re-parented or moved,
   the armature object keeps its transform, and the body keeps its vertex order. Unity builds
   the same Humanoid avatar from her as from the crew, so every clip made for the crew plays
   on her, and hers on them. Her height is an import scale in Unity, not a skeleton edit: she
   is authored at the crew's scale. The crew body is 2.59 Blender units, which Unity brings
   to 1.80 m through the ModelImporter globalScale 0.6956 in male01_1.fbx.meta (the PF_Crew
   roots are unscaled). She measures 2.565, the flatter hair cap being the difference, so
   her globalScale for 1.58 m is 0.616.

2. **Garments are fitted by casting rays at the body, then skinned by surface lookup.**
   Every ring of the skirt and the cardigan is the body's own outline at that height, found
   with rays, plus a gap, so nothing pokes through in the rest pose. Weights are read off the
   closest point of the body surface and interpolated across that face, which gives smooth
   weights instead of nearest-vertex jumps. Below the hip the skirt looks its weights up at
   hip height, as one cone on the pelvis (the bathrobe trick, model_bathrobe.py decision 2),
   and decision 6 then hands most of that cone to the legs. No cloth simulation. The skirt is a single
   sheet and Unity culls back faces, so the sheet folds up at the hem into a lining that
   runs back up to the hip: from a low camera the back panel no longer vanishes.

3. **Body faces under the clothes take the colour of the clothes.** Only the bodice between
   the cardigan's front edges stays dress teal; the rest of the T-shirt and the arm skin to
   the wrist are painted cardigan, the legs above the hem dress. A shoulder the cardigan does
   not quite wrap, or an elbow that pokes through in some pose, then reads as more cardigan
   instead of a hole. Before this, teal patches showed at both shoulder blades.

4. **The clip file is written in the rest pose.** Blender's FBX exporter writes the bone
   nodes in the pose of the current frame, and Unity builds a clip file's avatar from those
   nodes. The idle is therefore exported while the scene sits on frame 0, where a rest-pose
   key is added for the duration of the export and removed afterwards. The baked range is
   still frames 1 to 85, so the clip is unaffected, and its avatar is the same A pose as the
   one in SM_Grandma.fbx.

5. **Colours are written as the game shows them.** The Unity project runs in Gamma colour
   space, where an FBX DiffuseColor is displayed as it is. So Base Color, which the exporter
   writes, holds the sRGB colour unconverted, as in model_bathrobe.py; the first version
   converted it to linear and the dress would have imported near black. The viewport colour,
   which the Workbench renders use, holds the linear equivalent, so the renders show the
   colours of the game (the crew's skin included) with Unity's back-face culling.

6. **Below the hip the skirt rides on the legs, not on the pelvis** (2026-09-25, for the
   slice's Sit and Kneel clips). A rigid cone cannot sit: on the rocking chair 92 of its 256
   vertices went through the seat, and kneeling it reached 0.35 BU under the floor. Each
   skirt vertex below the hip ring now blends its hip lookup into thigh.L/R and shin.L/R,
   by a small table over its ring height and its angle round the body (SKIRT_LEGS): the
   front and sides lie on the thighs and drape over the knees; the back panel between the
   buttocks and the knees takes the shins, which barely move when she sits with her feet
   where she stood (author_clips.py), so it folds up into the thigh volume instead of hanging
   through the seat; below the knee the front and sides follow the shins. Left and right are
   split by x: nearly even at the hip, so the two halves of the back do not shear apart at
   full stride, and 70 % one-sided from the knee down, so the legs stay inside the skirt
   when she walks. In a symmetric pose (sitting, kneeling) both legs move alike and the
   split changes nothing. The table came out of a search scored on the real poses of
   author_clips.py (sit, read, drink, kneel, both walks), then checked in renders. The rig,
   the body and every other garment are untouched. Measured with author_clips.py on
   2026-09-26: no skirt vertex over the rocking chair's seat goes below its top in Sit_Idle,
   Sit_Read or Sit_Drink (lowest 0.476 m on a 0.47 m seat). Kneeling, the front panel below
   the knee follows the shins backwards and folds under them, down to 0.158 BU (9.7 cm) below
   the floor: hidden by the floor, and inside the ground floor's 0.29 m slab where she kneels.
   A skinned sheet cannot pool on the floor; a cloth or a corrective shape would.

Exports follow the two validated scripts: the mesh like model_bathrobe.py (unit scale
applied, bake_space_transform OFF because it wrecks armatures), the clip like
author_carry_clip.py (Unity: Humanoid, Create From This Model, Loop Time on).

Run headless, no GUI and no MCP:

    blender -b --factory-startup --python tools/blender/model_grandma.py

Arguments after a bare `--`: --src, --out-dir (the two FBX, by default where Unity keeps
them, Assets/_Project/Art/Characters/Grandma), --renders (PNG folder, by default the --blend
folder, never Assets),
--blend, --debug (extra close-ups, a top and a low view). Written 2026-09-25 against
male01_1.fbx (933 verts, 36-bone Rigify metarig). Result, checked by re-importing both FBX
in a fresh session: 1731 vertices, 3334 triangles, 10 materials, the 36 bones with the
crew's names, parents and rest positions, every vertex weighted with at most 4 normalized
bone influences, the clip 85 frames with frame 85 equal to frame 1 and the feet not sliding.
"""

import argparse
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc

ROOT = r"C:\GameProject"
DEFAULT_SRC = os.path.join(ROOT, r"UnityProject\Assets\Floreswa\Models\male01_1.fbx")
DEFAULT_OUT = os.path.join(ROOT, r"UnityProject\Assets\_Project\Art\Characters\Grandma")
DEFAULT_BLEND = os.path.join(ROOT, r"_ArtSource\Grandma.blend")

NAME = "SM_Grandma"
CLIP = "Grandma_Idle"
FPS = 24
LOOP = 84            # frames per cycle. Frame 85 repeats frame 1: 3.5 s at 24 fps.

CY = -0.035          # the body's centre line sits this far back (local -Y is the face)
HIP_Z = 1.36         # below this the skirt takes its weights at hip height
SHAPE = 2.3          # superellipse exponent of the skirt hem
ARM_BONES = ("upper_arm", "forearm", "hand")
ARM_SLIM = (0.80, 0.90)   # arm radius kept, at the shoulder and at the elbow


# The colours as the game shows them. The Unity project runs in Gamma colour space, where an
# FBX DiffuseColor is displayed as it is, so these go into Base Color unconverted, exactly as
# model_bathrobe.py writes its pink (decision 5).
COLOURS = {
    "dress":     (0.12, 0.33, 0.36, 1.0),   # dark teal
    "cardigan":  (0.71, 0.62, 0.80, 1.0),   # dusty lavender, away from the pink bathrobe
    "stockings": (0.80, 0.68, 0.56, 1.0),   # thick beige
    "shoes":     (0.27, 0.16, 0.09, 1.0),   # dark brown
    "hair":      (0.84, 0.84, 0.86, 1.0),   # light grey
    "pearl":     (0.96, 0.94, 0.88, 1.0),
}


def to_linear(colour):
    """The viewport colour that Blender's Standard view displays as `colour`."""
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(colour[0]), lin(colour[1]), lin(colour[2]), 1.0)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="model_grandma")
    p.add_argument("--src", default=DEFAULT_SRC)
    p.add_argument("--out-dir", default=DEFAULT_OUT)
    p.add_argument("--renders", default=None,
                   help="PNG folder, defaults to the --blend folder (PNGs never go to Assets)")
    p.add_argument("--blend", default=DEFAULT_BLEND)
    p.add_argument("--debug", action="store_true", help="extra close-up renders")
    return p.parse_args(argv)


# ----------------------------------------------------------------------------- helpers


def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def bump(x, mu, width):
    return math.exp(-((x - mu) / width) ** 2)


def principled(m):
    if m.node_tree is None:
        return None
    return next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)


def material(name, colour):
    """Base Color holds the game colour, because it is what the FBX exporter writes. The
    viewport colour holds its linear equivalent, because it is what the Workbench renders
    show (decision 5)."""
    m = bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True       # Blender 5 already creates the node tree
    bsdf = principled(m)
    if bsdf:
        bsdf.inputs["Base Color"].default_value = colour
        bsdf.inputs["Roughness"].default_value = 0.9
    m.diffuse_color = to_linear(colour)
    return m


def dominant_groups(obj):
    gi = {g.index: g.name for g in obj.vertex_groups}
    out = []
    for v in obj.data.vertices:
        best, bw = "", -1.0
        for g in v.groups:
            if g.weight > bw:
                bw, best = g.weight, gi.get(g.group, "")
        out.append(best)
    return out


def clean_weights(obj, bone_names, limit=4):
    """At most `limit` influences per vertex, only bone groups, normalized. Returns the
    number of vertices left without any weight, which must be zero."""
    for g in list(obj.vertex_groups):
        if g.name not in bone_names:
            obj.vertex_groups.remove(g)
    groups = {g.index: g for g in obj.vertex_groups}
    empty = 0
    for v in obj.data.vertices:
        ws = sorted(((g.weight, g.group) for g in v.groups if g.weight > 1e-4), reverse=True)
        keep, drop = ws[:limit], ws[limit:]
        for g in v.groups:
            if g.weight <= 1e-4:
                drop.append((g.weight, g.group))
        total = sum(w for w, _ in keep)
        if total <= 0.0:
            empty += 1
            continue
        for _, gidx in drop:
            groups[gidx].remove([v.index])
        for w, gidx in keep:
            groups[gidx].add([v.index], w / total, "REPLACE")
    return empty


def import_body(path):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    body = next(o for o in bpy.data.objects if o.type == "MESH")
    for pb in arm.pose.bones:      # the import leaves the pose at rest, make sure of it
        pb.rotation_mode = "QUATERNION"
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()
    return body, arm


# -------------------------------------------------------------------------------- body


def closest_on_segment(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return a + ab * t


def reshape_body(body, arm):
    """Move vertices only. Weights, vertex order and bones stay the crew's."""
    me = body.data
    dom = dominant_groups(body)
    names = [m.name for m in me.materials]
    vmats = [set() for _ in me.vertices]
    for p in me.polygons:
        for vi in p.vertices:
            vmats[vi].add(names[p.material_index])
    bones = arm.data.bones
    head_mats = {"hair", "eyes", "eyes_white", "mouth", "beard", "mustache", "goatee"}

    new = [v.co.copy() for v in me.vertices]
    for i, v in enumerate(me.vertices):
        co = new[i]
        d = dom[i]
        is_arm = d.startswith(ARM_BONES)
        is_head = bool(vmats[i] & head_mats) or d in ("spine.005", "spine.006")
        torso = (not is_arm and not is_head
                 and (d.startswith(("spine", "shoulder", "pelvis")) or
                      (d.startswith("thigh") and co.z > 1.15)))
        ax = abs(co.x)

        # Thin arms, pulled in towards their own bone so the skinning is unchanged. The
        # crew's deltoid stands 10 to 13 cm off the bone; hers must not widen the shoulder.
        if d.startswith(("upper_arm", "forearm")):
            b = bones[d]
            ab = b.tail_local - b.head_local
            t = min(max((co - b.head_local).dot(ab) / ab.length_squared, 0.0), 1.0)
            if d.startswith("upper_arm"):
                f = ARM_SLIM[0] + (ARM_SLIM[1] - ARM_SLIM[0]) * t
            else:
                f = ARM_SLIM[1] + (1.0 - ARM_SLIM[1]) * smoothstep(0.45, 0.95, t)
            c = closest_on_segment(co, b.head_local, b.tail_local)
            co = c + (co - c) * f
            ax = abs(co.x)

        # Sloped shoulders: the top of the shoulder line comes down towards the arm. On
        # the arm itself the drop fades out past the joint: lowering the top of an arm
        # that already slopes 45 degrees down only moves its outline outwards.
        if (torso or d.startswith("upper_arm")) and co.z > 1.98 and ax > 0.12 and not is_head:
            drop = 0.20 * min(ax - 0.12, 0.22) * smoothstep(1.98, 2.12, co.z)
            if d.startswith("upper_arm"):
                drop *= 1.0 - smoothstep(0.25, 0.34, ax)
            co.z -= drop

        if torso:
            # Narrower chest, fuller hips.
            co.x *= 1.0 - 0.06 * smoothstep(1.80, 1.98, co.z) * (1.0 - smoothstep(2.12, 2.24, co.z))
            co.x *= 1.0 + 0.08 * bump(co.z, 1.36, 0.10)
            if co.y < CY - 0.05:
                # Bust and belly, pushed forward. The body is low poly, so these are
                # broad shapes on a few vertices, not sculpting.
                co.y -= 0.085 * bump(ax, 0.105, 0.09) * bump(co.z, 1.86, 0.085)
                co.y -= 0.045 * bump(co.x, 0.0, 0.17) * bump(co.z, 1.58, 0.10)
            elif co.y > CY + 0.05:
                co.y += 0.025 * bump(co.z, 1.32, 0.10)

        # Stockinged legs: the trouser legs pulled in round the leg bones. The part above
        # the hem is inside the skirt, and slimmer thighs give it more room to swing.
        if "pants" in vmats[i] and (d.startswith(("thigh", "shin", "foot"))) and co.z < 1.20:
            side = "L" if co.x > 0 else "R"
            th, sh = bones["thigh." + side], bones["shin." + side]
            c1 = closest_on_segment(co, th.head_local, th.tail_local)
            c2 = closest_on_segment(co, sh.head_local, sh.tail_local)
            c = c1 if (co - c1).length < (co - c2).length else c2
            f = 0.80 + 0.12 * smoothstep(0.62, 1.05, co.z)
            f -= 0.06 * (1.0 - smoothstep(0.18, 0.36, co.z))   # the ankle
            new[i] = c + (co - c) * f
            co = new[i]
        new[i] = co

    # Smaller, lower shoes, scaled about each shoe's own centre line and heel.
    for side in (1.0, -1.0):
        idx = [i for i in range(len(new)) if "shoes" in vmats[i] and new[i].x * side > 0]
        if not idx:
            continue
        xc = sum(new[i].x for i in idx) / len(idx)
        heel = max(new[i].y for i in idx)
        for i in idx:
            new[i].x = xc + (new[i].x - xc) * 0.86
            new[i].y = heel + (new[i].y - heel) * 0.90
            new[i].z *= 0.92

    for v, co in zip(me.vertices, new):
        v.co = co
    smooth_hair(body)


def smooth_hair(body):
    """Turn the crew haircut into a close cap: Laplacian smoothing of the scalp only.

    The hair material also carries the eyebrows, as separate islands, and the hairline is
    shared with the skin. Only the largest island moves, and its border stays pinned, so the
    face and the brows are exactly the crew's.
    """
    me = body.data
    hair_idx = next(i for i, m in enumerate(me.materials) if m.name == "hair")
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    hair_faces = {f for f in bm.faces if f.material_index == hair_idx}
    seen, islands = set(), []
    for f in hair_faces:
        if f in seen:
            continue
        stack, isl = [f], []
        seen.add(f)
        while stack:
            cur = stack.pop()
            isl.append(cur)
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf in hair_faces and nf not in seen:
                        seen.add(nf)
                        stack.append(nf)
        islands.append(isl)
    scalp = max(islands, key=len)
    verts = {v for f in scalp for v in f.verts}
    free = [v for v in verts if all(f.material_index == hair_idx for f in v.link_faces)]
    for _ in range(10):
        moved = {}
        for v in free:
            nbrs = [e.other_vert(v).co for e in v.link_edges]
            avg = sum(nbrs, Vector()) / len(nbrs)
            moved[v] = v.co + (avg - v.co) * 0.5
        for v, co in moved.items():
            v.co = co
    bm.to_mesh(me)
    bm.free()
    print("HAIR islands {} scalp faces {} smoothed verts {}".format(
        len(islands), len(scalp), len(free)))


def restyle_materials(body):
    """Beard, moustache and goatee are skin-coloured faces of the jaw: fold them into skin.
    The shirt becomes the dress bodice, the trousers stockings, and so on."""
    me = body.data
    names = [m.name for m in me.materials]
    skin = names.index("skin")
    for p in me.polygons:
        if names[p.material_index] in ("beard", "mustache", "goatee"):
            p.material_index = skin
    replace = {"tshirt": "dress", "pants": "stockings", "shoes": "shoes", "hair": "hair"}
    made = {}
    for i, n in enumerate(names):
        if n in replace:
            key = replace[n]
            made[key] = material("grandma_" + key, COLOURS[key])
            me.materials[i] = made[key]
    while me.materials and me.materials[-1].name in ("beard", "mustache", "goatee"):
        me.materials.pop()
    # The crew's own slots (skin, eyes, mouth) keep the pack's DiffuseColor in Base Color,
    # untouched. Their viewport colour gets the same linear preview as hers.
    for m in me.materials:
        bsdf = principled(m)
        if bsdf:
            m.diffuse_color = to_linear(bsdf.inputs["Base Color"].default_value)
    return made


def gap_at(z):
    """Half width of the cardigan's front opening at height z, from CARD_RINGS."""
    rows = [(r[0], r[2]) for r in CARD_RINGS]
    if z >= rows[0][0]:
        return rows[0][1]
    for (z0, g0), (z1, g1) in zip(rows, rows[1:]):
        if z1 <= z <= z0:
            return g0 + (g1 - g0) * (z0 - z) / (z0 - z1)
    return rows[-1][1]


def paint_under_clothes(body, mat_card, mat_dress):
    """Body faces that the clothes cover take the colour of what covers them.

    Only the bodice between the cardigan's front edges is seen as the dress; the rest of
    the T-shirt, and the arm skin down to the wrist, sit under the cardigan, and the legs
    above the hem sit under the skirt. Painting them the covering colour means that a
    shoulder the cardigan does not quite wrap, or an elbow that pokes through in some
    pose, reads as more cardigan instead of a teal or skin coloured hole. Same trick as
    the bathrobe keeping the player colour on its trim: colour does the work geometry
    would cost.
    """
    me = body.data
    me.materials.append(mat_card)
    card = len(me.materials) - 1
    names = [m.name for m in me.materials]
    dress = names.index(mat_dress.name)
    stock = names.index("grandma_stockings")
    skin = names.index("skin")
    dom = dominant_groups(body)
    wrist_t = 0.90
    bones = body.parent.data.bones
    n_card = n_dress = 0
    for p in me.polygons:
        c = p.center
        doms = [dom[i] for i in p.vertices]
        # sorted: a tie between two bones must not be broken by the string hash order,
        # which Python changes on every run and made the painted faces drift
        main = max(sorted(set(doms)), key=doms.count)
        if p.material_index == dress:
            front = c.y < CY - 0.05 and abs(c.x) < gap_at(c.z) + 0.03
            if not front:
                p.material_index = card
                n_card += 1
        elif p.material_index == skin and main.startswith(("upper_arm", "forearm")):
            if main.startswith("forearm"):
                b = bones[main]
                ab = b.tail_local - b.head_local
                t = (c - b.head_local).dot(ab) / ab.length_squared
                if t > wrist_t:
                    continue
            p.material_index = card
            n_card += 1
        elif p.material_index == stock and c.z > HEM_Z + 0.06:
            p.material_index = dress
            n_dress += 1
    print("PAINTED under the cardigan {}, under the skirt {}".format(n_card, n_dress))


def flat_shade(obj):
    """The pack ships flat custom normals. Once vertices move they are stale, so drop them
    and let flat shading rebuild the same look from the new positions."""
    me = obj.data
    if "custom_normal" in me.attributes:
        me.attributes.remove(me.attributes["custom_normal"])
    elif getattr(me, "has_custom_normals", False):
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
    me.shade_flat()


# --------------------------------------------------------------------- fitting and skin


def body_bvh(body, keep_face=None, extra=None):
    """BVH of the body faces, arms excluded unless asked, plus optional extra meshes."""
    me = body.data
    verts = [v.co.copy() for v in me.vertices]
    polys = [list(p.vertices) for p in me.polygons if keep_face is None or keep_face(p)]
    for obj in extra or []:
        base = len(verts)
        verts += [v.co.copy() for v in obj.data.vertices]
        polys += [[base + i for i in p.vertices] for p in obj.data.polygons]
    return BVHTree.FromPolygons(verts, polys)


def torso_filter(body):
    dom = dominant_groups(body)
    arm = [d.startswith(ARM_BONES) for d in dom]
    return lambda p: sum(arm[i] for i in p.vertices) * 2 < len(p.vertices)


def arm_filter(body):
    dom = dominant_groups(body)
    arm = [d.startswith(ARM_BONES) for d in dom]
    return lambda p: sum(arm[i] for i in p.vertices) * 2 >= len(p.vertices)


def outline(bvh, z, n=48, far=1.2):
    """The body's outline at height z: radius from (0, CY) for n directions."""
    c = Vector((0.0, CY, z))
    rs = []
    for i in range(n):
        th = math.tau * i / n
        d = Vector((math.sin(th), -math.cos(th), 0.0))
        loc, _n, _i, dist = bvh.ray_cast(c + d * far, -d, far)
        rs.append(far - dist if loc is not None else None)
    known = [r for r in rs if r is not None] or [0.1]
    rs = [r if r is not None else min(known) for r in rs]
    for _ in range(2):   # smooth, only ever outwards, so the fit still encloses the body
        rs = [max(rs[i], (rs[i - 1] + 2 * rs[i] + rs[(i + 1) % n]) / 4) for i in range(n)]
    return rs


def radius_at(rs, th):
    n = len(rs)
    f = (th % math.tau) / math.tau * n
    i = int(f) % n
    t = f - int(f)
    return rs[i] * (1 - t) + rs[(i + 1) % n] * t


def direction(th):
    """th = 0 is the front (local -Y), th = pi/2 is her left (+X)."""
    return Vector((math.sin(th), -math.cos(th), 0.0))


class Skin:
    """Weights read off the closest point of the body surface, barycentric on that face."""

    def __init__(self, body, keep_face=None):
        me = body.data
        self.verts = [v.co.copy() for v in me.vertices]
        self.polys = [list(p.vertices) for p in me.polygons if keep_face is None or keep_face(p)]
        self.bvh = BVHTree.FromPolygons(self.verts, self.polys)
        gi = {g.index: g.name for g in body.vertex_groups}
        self.w = [{gi[g.group]: g.weight for g in v.groups if g.weight > 0.0}
                  for v in me.vertices]

    def at(self, p):
        loc, _n, idx, _d = self.bvh.find_nearest(p)
        poly = self.polys[idx]
        bary = poly_3d_calc([self.verts[i] for i in poly], loc)
        out = {}
        for vi, b in zip(poly, bary):
            for name, w in self.w[vi].items():
                out[name] = out.get(name, 0.0) + w * max(b, 0.0)
        return out


def apply_weights(obj, weights):
    """weights: one {bone: weight} per vertex."""
    groups = {}
    for i, ws in enumerate(weights):
        for name, w in ws.items():
            if w <= 0.0:
                continue
            if name not in groups:
                groups[name] = obj.vertex_groups.new(name=name)
            groups[name].add([i], w, "REPLACE")


def mesh_object(name, bm, mats):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    me.shade_flat()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def loft(bm, rings, mat=0, closed=True):
    n = len(rings[0])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(n if closed else n - 1):
            k2 = (k + 1) % n
            f = bm.faces.new((a[k], b[k], b[k2], a[k2]))
            f.material_index = mat


# ------------------------------------------------------------------------------- skirt

SKIRT_SEGS = 16
# (z, gap outside the body). The top three follow the body, below the hip it is a cone.
SKIRT_FIT = [(1.68, 0.012), (1.57, 0.020), (1.46, 0.028), (1.34, 0.036)]
SKIRT_CONE = [1.14, 0.94, 0.74, 0.58]
HEM_Z = 0.48          # mid calf: the knee is at 0.76 and the ankle at 0.16
HEM_HX, HEM_HY = 0.425, 0.315
LINING_GAP = 0.008    # the lining sits this far inside the skirt, below the hip


def superellipse(th, hx, hy):
    s, c = math.sin(th), -math.cos(th)
    return Vector((hx * math.copysign(abs(s) ** (2.0 / SHAPE), s),
                   hy * math.copysign(abs(c) ** (2.0 / SHAPE), c), 0.0))


def build_skirt(body, mat):
    bvh = body_bvh(body, torso_filter(body))
    bm = bmesh.new()
    angles = [math.tau * k / SKIRT_SEGS for k in range(SKIRT_SEGS)]
    rings, rel = [], []
    body_top = None
    for z, pad in SKIRT_FIT:
        rs = outline(bvh, z)
        if body_top is None:
            body_top = rs
        pts = [direction(th) * (radius_at(rs, th) + pad) for th in angles]
        rel.append((z, pts))
    hip_z, hip = rel[-1]
    hem = [superellipse(th, HEM_HX, HEM_HY) for th in angles]
    for z in SKIRT_CONE + [HEM_Z]:
        t = (hip_z - z) / (hip_z - HEM_Z)
        t = t ** 0.85       # a little more flare near the hip than a straight cone
        pts = [hip[k].lerp(hem[k], t) for k in range(SKIRT_SEGS)]
        # never narrower than the hip outline, so the cone keeps enclosing the thighs
        pts = [p if p.length >= hip[k].length else hip[k].normalized() * hip[k].length
               for k, p in enumerate(pts)]
        rel.append((z, pts))
    for z, pts in rel:
        rings.append([bm.verts.new((p.x, CY + p.y, z)) for p in pts])
    loft(bm, rings, 0)

    # Waistband rim: folded in to the body so there is no slot to look into from above.
    top = rings[0]
    inner = [bm.verts.new((direction(th) * (radius_at(body_top, th) - 0.004)).to_tuple()) for th in angles]
    for v, th in zip(inner, angles):
        v.co.y += CY
        v.co.z = SKIRT_FIT[0][0] - 0.02
    loft(bm, [top, inner], 0)
    # Hem rim: folded up inside, so the edge has a visible thickness.
    hem_ring = rings[-1]
    hem_in = []
    for v in hem_ring:
        d = Vector((v.co.x, v.co.y - CY, 0.0)).normalized()
        hem_in.append(bm.verts.new(v.co - d * 0.035 + Vector((0, 0, 0.035))))
    loft(bm, [hem_ring, hem_in], 0)
    # Lining: the cone again, a few millimetres inside, from the hem fold up to the hip.
    # The skirt is a single sheet and Unity's Standard shader culls back faces, so from a
    # low camera (the foot of the stairs, a player on the floor) the inside of the back
    # panel vanished and the room showed through under the hem. The lining continues the
    # sheet from the fold, so the consistent normals face inward on it without flipping.
    lining = [hem_in]
    for ring in reversed(rings[len(SKIRT_FIT) - 1:-1]):     # 0.58 up to the hip ring
        layer = []
        for v in ring:
            d = Vector((v.co.x, v.co.y - CY, 0.0)).normalized()
            layer.append(bm.verts.new(v.co - d * LINING_GAP))
        lining.append(layer)
    loft(bm, lining, 0)
    return mesh_object("grandma_skirt", bm, [mat])


# ---------------------------------------------------------------------------- cardigan

CARD_ARC = 15          # points from her left front edge, round the back, to the right one
CARD_THICK = 0.016
# (z, gap outside body and skirt, half width of the front opening, widest |x| or None).
# The caps on the top three rings are the sloped shoulders: fitted to the body alone, the
# crew's square shoulders turned the cardigan into a jacket with shoulder pads. The sleeve
# head covers what the caps leave out.
CARD_RINGS = [
    (2.170, 0.020, 0.070, 0.190),   # round the back of the neck
    (2.100, 0.022, 0.088, 0.250),
    (2.000, 0.024, 0.102, 0.290),
    (1.880, 0.026, 0.112, None),    # over the bust
    (1.760, 0.028, 0.118, None),
    (1.640, 0.030, 0.124, None),
    (1.500, 0.036, 0.134, None),
    (1.380, 0.046, 0.150, None),    # hem, just below the hip joint, a little flared
]


def edge_angle(rs, pad, gap, side):
    """The angle where the fitted outline crosses x = gap * side on the front half."""
    lo, hi = (0.0, math.pi / 2) if side > 0 else (1.5 * math.pi, math.tau)
    def x_at(th):
        return side * math.sin(th) * (radius_at(rs, th) + pad)
    # x_at rises from 0 at the front centre to the full width at the side
    a, b = (lo, hi) if side > 0 else (hi, lo)
    for _ in range(40):
        m = (a + b) / 2
        if x_at(m) < gap:
            a = m
        else:
            b = m
    return (a + b) / 2


def build_cardigan(body, arm, skirt, mat):
    bvh = body_bvh(body, torso_filter(body), extra=[skirt])
    bm = bmesh.new()
    outer, inner = [], []
    side_at = {}
    for z, pad, gap, cap in CARD_RINGS:
        rs = outline(bvh, z)
        if cap is not None:
            n = len(rs)
            rs = [min(r, (cap - pad) / max(abs(math.sin(math.tau * i / n)), 1e-3))
                  for i, r in enumerate(rs)]
        th_l = edge_angle(rs, pad, gap, 1.0)
        th_r = edge_angle(rs, pad, gap, -1.0)
        span = th_r - th_l
        o_ring, i_ring = [], []
        for k in range(CARD_ARC):
            th = th_l + span * k / (CARD_ARC - 1)
            d = direction(th)
            r = radius_at(rs, th) + pad
            o_ring.append(bm.verts.new((d.x * r, CY + d.y * r, z)))
            ri = r - CARD_THICK
            i_ring.append(bm.verts.new((d.x * ri, CY + d.y * ri, z)))
        outer.append(o_ring)
        inner.append(i_ring)
        side_at[z] = max(abs(v.co.x) for v in o_ring)
    loft(bm, outer, 0, closed=False)
    loft(bm, inner, 0, closed=False)
    # Close the shell: both front edges, the collar and the hem.
    for k in (0, CARD_ARC - 1):
        loft(bm, [[r[k] for r in outer], [r[k] for r in inner]], 0, closed=False)
    loft(bm, [outer[0], inner[0]], 0, closed=False)
    loft(bm, [outer[-1], inner[-1]], 0, closed=False)
    n_shell = len(bm.verts)

    # Sleeves along the real arm bones, to the wrist, with a turned-back cuff. The sleeve
    # head starts inside the torso shell and is then pulled onto the body (hug), so it
    # follows the sloped shoulder instead of standing up past it as a pad or a peak.
    arm_bvh = body_bvh(body, arm_filter(body))
    full_bvh = body_bvh(body)
    bones = arm.data.bones
    for side in ("L", "R"):
        sh = bones["upper_arm." + side].head_local.copy()
        el = bones["forearm." + side].head_local.copy()
        wr = bones["forearm." + side].tail_local.copy()
        start = sh - (el - sh).normalized() * 0.06      # buried in the torso shell
        end = el + (wr - el) * 0.96
        path = [(start, 0.100), (sh.lerp(el, 0.12), None), (sh.lerp(el, 0.30), None),
                (sh.lerp(el, 0.70), None), (el, None), (el.lerp(end, 0.5), None), (end, None)]
        rings = []
        for p, fixed in path:
            axis = ((el - sh) if (p - sh).length < (el - sh).length - 1e-4 else (wr - el)).normalized()
            rings.append(sleeve_ring(bm, arm_bvh, p, axis, fixed))
        for ring in rings[1:SLEEVE_HEAD_RINGS]:
            hug(ring, full_bvh, SLEEVE_HEAD_PAD)
        # The buried end goes under the body surface itself. Hugged to the pad like the
        # rings after it, its cap came out where the torso shell stops short of the
        # shoulder and showed as a dark fleck on top of each shoulder.
        bury(rings[0], full_bvh, SLEEVE_CAP_DEPTH)
        loft(bm, rings, 0)
        bm.faces.new(rings[0]).material_index = 0      # close the buried end: no open edge
        # cuff: a slightly wider band, then folded in to the wrist
        axis = (wr - el).normalized()
        cuff_c = end
        last = rings[-1]
        cuff = []
        for v in last:
            d = (v.co - cuff_c)
            d -= axis * d.dot(axis)
            cuff.append(bm.verts.new(cuff_c + axis * 0.012 + d * 1.14))
        loft(bm, [last, cuff], 0)
        fold = []
        for v in last:
            d = (v.co - cuff_c)
            d -= axis * d.dot(axis)
            fold.append(bm.verts.new(cuff_c + axis * 0.004 + d * 0.62))
        loft(bm, [cuff, fold], 0)
    obj = mesh_object("grandma_cardigan", bm, [mat])
    obj["n_shell"] = n_shell        # vertices below this index are the torso shell
    return obj, side_at


def tuck_body(body, cardigan):
    """Pull torso vertices that reach past the cardigan's inner surface back inside it.

    The shell is lofted between rings a dozen centimetres apart, so a vertex that bulges
    between two rings (the bust) can poke through as a small teal fleck on the cardigan.
    A horizontal ray from the centre line through each torso vertex finds the shell; a
    vertex beyond the first hit moves back to just inside it. Only the shell counts, not
    the sleeves, whose buried heads lie inside the shoulder on purpose.
    """
    cme = cardigan.data
    n_shell = cardigan["n_shell"]
    bvh = BVHTree.FromPolygons([v.co.copy() for v in cme.vertices],
                               [list(p.vertices) for p in cme.polygons
                                if all(i < n_shell for i in p.vertices)])
    z_lo, z_hi = CARD_RINGS[-1][0], CARD_RINGS[0][0]
    dom = dominant_groups(body)
    moved = 0
    for v, d in zip(body.data.vertices, dom):
        co = v.co
        if not (z_lo < co.z < z_hi) or d.startswith(ARM_BONES) or d in ("spine.005", "spine.006"):
            continue
        c = Vector((0.0, CY, co.z))
        out = co - c
        r = out.length
        if r < 1e-4:
            continue
        loc, _n, _i, dist = bvh.ray_cast(c, out / r, r + 0.01)
        if loc is not None and dist < r + 0.006:
            v.co = c + out / r * (dist - 0.006)
            moved += 1
    print("TUCKED {} body vertices under the cardigan".format(moved))


SLEEVE_SEGS = 8
SLEEVE_HEAD_RINGS = 3     # the rings from inside the torso to a third of the upper arm
SLEEVE_HEAD_PAD = 0.014   # the most the sleeve head may stand off the body
SLEEVE_CAP_DEPTH = 0.015  # the sleeve's closed start ring sits this far under the body


def hug(verts, bvh, pad):
    """Pull vertices that stand off the body by more than `pad` back to `pad`, along the
    line to the closest body point. Vertices inside the body stay where they are."""
    for v in verts:
        loc, nrm, _i, dist = bvh.find_nearest(v.co)
        if loc is None or dist <= pad:
            continue
        out = v.co - loc
        if out.dot(nrm) <= 0.0:
            continue
        v.co = loc + out.normalized() * pad


def bury(verts, bvh, depth):
    """Put vertices that are outside the body, or less than `depth` inside it, `depth`
    under the closest body face. Vertices deeper inside stay where they are."""
    for v in verts:
        loc, nrm, _i, _d = bvh.find_nearest(v.co)
        if loc is None:
            continue
        if (v.co - loc).dot(nrm) > -depth:
            v.co = loc - nrm * depth


def sleeve_ring(bm, bvh, centre, axis, fixed):
    up = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((0, 1, 0))
    s = axis.cross(up).normalized()
    u = s.cross(axis).normalized()
    dirs = [s * math.cos(math.tau * i / SLEEVE_SEGS) + u * math.sin(math.tau * i / SLEEVE_SEGS)
            for i in range(SLEEVE_SEGS)]
    if fixed is not None:
        rs = [fixed] * SLEEVE_SEGS
    else:
        rs = []
        for d in dirs:
            loc, _n, _i, dist = bvh.ray_cast(centre + d * 0.4, -d, 0.4)
            rs.append(0.4 - dist if loc is not None else None)
        known = [r for r in rs if r is not None] or [0.06]
        rs = [r if r is not None else max(known) for r in rs]
        rs = [max(r, (rs[i - 1] + rs[i] + rs[(i + 1) % SLEEVE_SEGS]) / 3) + 0.020
              for i, r in enumerate(rs)]
    return [bm.verts.new(centre + d * r) for d, r in zip(dirs, rs)]


# ----------------------------------------------------------------------- hair and pearls


def build_bun(body, mat):
    """A round bun at the back of the head, half sunk into the scalp."""
    me = body.data
    hair_idx = next(i for i, m in enumerate(me.materials) if m.name.startswith("grandma_hair"))
    hv = [me.vertices[i].co for p in me.polygons if p.material_index == hair_idx for i in p.vertices]
    top = max(c.z for c in hv)
    z = top - 0.095
    back = max(c.y for c in hv if abs(c.z - z) < 0.04 and abs(c.x) < 0.05)
    centre = Vector((0.0, back + 0.030, z))
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=1.0)
    for v in bm.verts:
        v.co = centre + Vector((v.co.x * 0.080, v.co.y * 0.066, v.co.z * 0.074))
    for f in bm.faces:
        f.material_index = 0
    return mesh_object("grandma_bun", bm, [mat]), centre


# 18 beads, the few the cardigan covers left out: 16 octahedra, 128 triangles, inside the
# accessory budget of 05_ART/CHARACTERS.md (well under a tenth of the 1862-tri body, 186).
# The first version had 26 icosahedra, 520 triangles, 15% of her whole mesh.
PEARL_COUNT = 18
PEARL_R = 0.015


def build_pearls(body, cardigan, mat):
    """Beads round the base of the neck, dipping onto the chest at the front.

    The strand is high at the sides and back, where the neck is still a neck (lower down,
    a horizontal ray hits the top of the shoulder instead), and low at the front. Beads the
    cardigan covers are left out: they would be invisible at best, and poke through the
    collar at worst.
    """
    bvh = body_bvh(body, torso_filter(body))
    cme = cardigan.data
    card_bvh = BVHTree.FromPolygons([v.co.copy() for v in cme.vertices],
                                    [list(p.vertices) for p in cme.polygons])
    bm = bmesh.new()
    count, r_bead = PEARL_COUNT, PEARL_R
    kept = 0
    for k in range(count):
        th = math.tau * k / count
        u = abs(math.sin(th / 2.0))            # 0 at the front, 1 at the back
        z = 2.070 + 0.165 * u ** 0.6
        c = Vector((0.0, CY, z))
        d = direction(th)
        loc, _n, _i, dist = bvh.ray_cast(c + d * 0.5, -d, 0.5)
        r = (0.5 - dist) if loc is not None else 0.11
        p = c + d * (r + r_bead * 0.7)
        hit = card_bvh.ray_cast(p, d, 0.08)
        if hit[0] is not None:
            continue
        up_hit = card_bvh.ray_cast(p, Vector((0, 0, 1)), 0.05)
        if up_hit[0] is not None:
            continue
        octahedron(bm, p, d, r_bead)
        kept += 1
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    print("PEARLS {} of {} beads visible, {} tris".format(kept, count, tris))
    return mesh_object("grandma_pearls", bm, [mat])


def octahedron(bm, centre, outward, r):
    """A bead of 8 triangles. At 3 to 10 m a pearl is a light dot of a few pixels, so an
    icosahedron's 20 triangles bought nothing but budget. It is turned to show a face, not
    a tip, to the outside: seen that way its outline is a hexagon, the roundest it has,
    where a tip facing out read as a stud."""
    f = outward.normalized()
    u = Vector((0.0, 0.0, 1.0))
    u = (u - f * u.dot(f)).normalized()
    s = f.cross(u)
    # canonical frame: the face normal (1,1,1) goes to f, (-1,-1,2) to u
    e1 = Vector((1.0, 1.0, 1.0)).normalized()
    e2 = Vector((-1.0, -1.0, 2.0)).normalized()
    e3 = e1.cross(e2)

    def place(v):
        return centre + (f * v.dot(e1) + u * v.dot(e2) + s * v.dot(e3)) * r

    px, py, pz = (bm.verts.new(place(a)) for a in (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))))
    nx, ny, nz = (bm.verts.new(place(-a)) for a in (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))))
    for a, b in ((px, py), (py, nx), (nx, ny), (ny, px)):
        bm.faces.new((a, b, pz))
        bm.faces.new((b, a, nz))


# ------------------------------------------------------------------------------ skinning


def skin_garment(obj, skin, probe=None, fixed=None):
    ws = []
    for v in obj.data.vertices:
        if fixed is not None:
            ws.append(dict(fixed))
            continue
        p = v.co.copy()
        if probe:
            p = probe(p)
        ws.append(skin.at(p))
    apply_weights(obj, ws)


def skirt_probe(p):
    """Below the hip, look up at hip height, pulled in onto the hip outline."""
    if p.z >= HIP_Z:
        return p
    q = Vector((p.x * 0.72, CY + (p.y - CY) * 0.72, HIP_Z))
    return q


def cardigan_probe(p):
    if p.z < 1.46:
        return Vector((p.x, p.y, 1.46))
    return p


# Decision 6. Values at the skirt's ring heights, the hip ring first, linear in between.
# A: how much of a vertex rides on the legs instead of its hip lookup. SF, SS, SB: the shin's
# share of that ride at the front, the sides and the back (the thigh has the rest). SPLIT:
# how one-sided the left/right split is, 0 both legs alike, 1 all on the leg of its side.
SKIRT_RING_Z = (1.34, 1.14, 0.94, 0.74, 0.58, 0.48)
SKIRT_LEGS = {
    "A":     (0.0, 0.75, 1.0, 1.0, 1.0, 1.0),
    "SF":    (0.0, 0.0, 0.3, 0.6, 0.6, 1.0),
    "SS":    (0.0, 0.0, 0.6, 0.9, 0.9, 0.8),
    "SB":    (0.0, 0.6, 1.0, 1.0, 0.5, 0.5),
    "SPLIT": (0.2, 0.2, 0.4, 0.7, 0.7, 0.7),
}
SPLIT_X = 0.08       # the split is blended across this half width of the centre line


def ring_value(key, z):
    zs, vs = SKIRT_RING_Z, SKIRT_LEGS[key]
    if z >= zs[0]:
        return vs[0]
    if z <= zs[-1]:
        return vs[-1]
    for z0, z1, v0, v1 in zip(zs, zs[1:], vs, vs[1:]):
        if z1 <= z <= z0:
            return v0 + (v1 - v0) * (z0 - z) / (z0 - z1)
    return vs[-1]


def blend_skirt_into_legs(skirt):
    """Decision 6: below the hip ring, hand the skirt's hip lookup over to the legs.
    The lining and the hem fold sit at the same heights and angles as the outer sheet, so
    they get the same weights and stay inside it."""
    me = skirt.data
    gi = {g.index: g.name for g in skirt.vertex_groups}
    rigid = [{gi[g.group]: g.weight for g in v.groups if g.weight > 0.0} for v in me.vertices]
    out = []
    for v, ws in zip(me.vertices, rigid):
        co = v.co
        c = math.cos(math.atan2(co.x, -(co.y - CY)))      # 1 at the front, -1 at the back
        front, back = max(c, 0.0), max(-c, 0.0)
        side = 1.0 - front - back
        a = ring_value("A", co.z)
        s = min(1.0, max(0.0, front * ring_value("SF", co.z) + side * ring_value("SS", co.z)
                         + back * ring_value("SB", co.z)))
        left = 0.5 + ring_value("SPLIT", co.z) * (smoothstep(-SPLIT_X, SPLIT_X, co.x) - 0.5)
        total = sum(ws.values()) or 1.0
        new = {k: (1.0 - a) * w / total for k, w in ws.items()}
        for bone, w in (("thigh.L", (1.0 - s) * left), ("thigh.R", (1.0 - s) * (1.0 - left)),
                        ("shin.L", s * left), ("shin.R", s * (1.0 - left))):
            new[bone] = new.get(bone, 0.0) + a * w
        out.append(new)
    for g in list(skirt.vertex_groups):
        skirt.vertex_groups.remove(g)
    apply_weights(skirt, out)
    moved = sum(1 for v in me.vertices if ring_value("A", v.co.z) > 0.0)
    print("SKIRT_LEGS {} of {} skirt vertices ride on the legs".format(moved, len(me.vertices)))


# ------------------------------------------------------------------------------- idle


def rig_axes(arm):
    """Up, forward, right read off the bones, as in author_carry_clip.py."""
    pb = arm.pose.bones
    up = (pb["spine.005"].tail - pb["spine"].head).normalized()
    toe = pb["toe.L"].tail - pb["toe.L"].head
    fwd = (toe - up * toe.dot(up)).normalized()
    return up, fwd, fwd.cross(up).normalized()


def upd():
    bpy.context.view_layer.update()


def rotate(arm, name, axis, deg):
    pb = arm.pose.bones[name]
    head = pb.head.copy()
    pb.matrix = (Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg), 4, axis.normalized())
                 @ Matrix.Translation(-head) @ pb.matrix)
    upd()


def point_bone(arm, name, target_dir):
    pb = arm.pose.bones[name]
    cur = (pb.tail - pb.head).normalized()
    q = cur.rotation_difference(target_dir.normalized())
    head = pb.head.copy()
    pb.matrix = (Matrix.Translation(head) @ q.to_matrix().to_4x4()
                 @ Matrix.Translation(-head) @ pb.matrix)
    upd()


def two_bone(arm, upper, lower, target, pole):
    pu, pl = arm.pose.bones[upper], arm.pose.bones[lower]
    s = pu.head.copy()
    a = (pu.tail - pu.head).length
    b = (pl.tail - pl.head).length
    dv = target - s
    d = min(max(dv.length, abs(a - b) + 1e-4), a + b - 1e-4)
    u = dv.normalized()
    alpha = math.acos(max(-1.0, min(1.0, (a * a + d * d - b * b) / (2 * a * d))))
    v = (pole - u * pole.dot(u)).normalized()
    elbow = s + (u * math.cos(alpha) + v * math.sin(alpha)) * a
    point_bone(arm, upper, elbow - s)
    point_bone(arm, lower, (s + u * d) - arm.pose.bones[lower].head)


def pose_reset(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    upd()


def envelope(t, a, b, c, d):
    """0 until a, eases to 1 by b, holds until c, eases back to 0 by d."""
    if t <= a or t >= d:
        return 0.0
    if t < b:
        return smoothstep(a, b, t)
    if t <= c:
        return 1.0
    return 1.0 - smoothstep(c, d, t)


KEYED = ["spine", "spine.001", "spine.002", "spine.003", "spine.004", "spine.005",
         "shoulder.L", "shoulder.R", "upper_arm.L", "upper_arm.R", "forearm.L", "forearm.R",
         "hand.L", "hand.R", "thigh.L", "thigh.R", "shin.L", "shin.R", "foot.L", "foot.R"]


class IdleRig:
    """The idle as a function of the loop phase t in [0, 1). Periodic, so t = 1 is t = 0."""

    def __init__(self, arm, front_y, side_x):
        self.arm = arm
        self.axes = rig_axes(arm)
        bones = arm.data.bones
        self.ankle = {s: bones["shin." + s].tail_local.copy() for s in "LR"}
        self.foot_rest = {s: bones["foot." + s].matrix_local.copy() for s in "LR"}
        self.belly_rest = bones["spine.001"].matrix_local.copy()
        # Hands clasped in front of the belly. Everything is measured off her clothes:
        # `front_y` is the front of the cardigan and dress at the belly, `side_x` the side
        # of the cardigan at elbow height. The elbows sit just outside the cardigan (this
        # rig's shoulder joints are inside the torso's width, so a hanging upper arm would
        # go through it), the wrists in front of the belly, and each hand points past the
        # middle so the fingers overlap. The left hand lies over the right.
        self.elbow = {"L": Vector((side_x + 0.068, CY - 0.070, 1.755)),
                      "R": Vector((-side_x - 0.068, CY - 0.070, 1.755))}
        self.wrist = {"L": Vector((0.125, front_y - 0.060, 1.625)),
                      "R": Vector((-0.125, front_y - 0.034, 1.615))}
        self.grip = {"L": Vector((-0.050, front_y - 0.072, 1.515)),
                     "R": Vector((0.050, front_y - 0.044, 1.505))}

    def pose(self, t):
        arm = self.arm
        up, fwd, right = self.axes
        left = -right
        flex = up.cross(fwd)            # rotating about this bends forward
        pose_reset(arm)

        breath = 0.5 - 0.5 * math.cos(2.0 * math.tau * t)   # two breaths per loop
        shift = 0.5 - 0.5 * math.cos(math.tau * t)          # onto the left leg and back
        look = envelope(t, 0.10, 0.36, 0.58, 0.88)           # a slow look to her left

        hips = arm.pose.bones["spine"]
        hips.matrix = Matrix.Translation(left * (0.020 * shift) - up * (0.016 + 0.006 * shift)) @ hips.matrix
        upd()
        rotate(arm, "spine", fwd, 2.2 * shift)               # the free hip drops
        rotate(arm, "spine.001", flex, 3.0)
        rotate(arm, "spine.001", fwd, -2.6 * shift)          # the torso stays upright
        rotate(arm, "spine.002", flex, 4.0 - 1.3 * breath)   # chest lifts on the breath
        rotate(arm, "spine.003", flex, 3.0 - 0.9 * breath)
        rotate(arm, "spine.004", flex, 10.0)                 # the forward head of old age
        rotate(arm, "spine.004", up, 5.0 * look)
        rotate(arm, "spine.005", flex, -12.0 + 2.0 * look)   # eyes back to level, a nod
        rotate(arm, "spine.005", up, 13.0 * look)
        rotate(arm, "spine.005", fwd, -2.5 * look)
        rotate(arm, "shoulder.L", fwd, 1.6 * breath)
        rotate(arm, "shoulder.R", -fwd, 1.6 * breath)

        belly = arm.pose.bones["spine.001"].matrix @ self.belly_rest.inverted()
        for s in "LR":
            e = belly @ self.elbow[s]
            w = belly @ self.wrist[s]
            point_bone(arm, "upper_arm." + s, e - arm.pose.bones["upper_arm." + s].head)
            point_bone(arm, "forearm." + s, w - arm.pose.bones["forearm." + s].head)
            hand = arm.pose.bones["hand." + s]
            point_bone(arm, "hand." + s, belly @ self.grip[s] - hand.head)

        for s, out in (("L", left), ("R", right)):
            two_bone(arm, "thigh." + s, "shin." + s, self.ankle[s], fwd + out * 0.15)
            foot = arm.pose.bones["foot." + s]
            m = self.foot_rest[s].copy()
            m.translation = arm.pose.bones["shin." + s].tail
            foot.matrix = m
            upd()


def all_fcurves(action):
    """Blender 5 keeps F-Curves in layers -> strips -> channelbags (author_carry_clip.py)."""
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    out = []
    for layer in action.layers:
        for strip in layer.strips:
            for cb in getattr(strip, "channelbags", []):
                out.extend(cb.fcurves)
    return out


def build_idle(arm, rig):
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 1, LOOP + 1
    arm.animation_data_create()
    action = bpy.data.actions.new(CLIP)
    action.use_fake_user = True
    arm.animation_data.action = action
    prev = {}
    for frame in range(1, LOOP + 2):
        t = ((frame - 1) % LOOP) / LOOP       # frame LOOP+1 is exactly frame 1
        rig.pose(t)
        for name in KEYED:
            pb = arm.pose.bones[name]
            q = pb.rotation_quaternion.copy()
            if name in prev and prev[name].dot(q) < 0.0:
                q.negate()
            pb.rotation_quaternion = q
            prev[name] = q
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            if name == "spine":
                pb.keyframe_insert("location", frame=frame)
    for fc in all_fcurves(action):
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    return action


def check_loop(action):
    worst = 0.0
    for fc in all_fcurves(action):
        worst = max(worst, abs(fc.evaluate(1) - fc.evaluate(LOOP + 1)))
    return worst


# ------------------------------------------------------------------------------ output


def export_mesh(arm, obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        global_scale=1.0, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type="FACE",
        add_leaf_bones=False, bake_anim=False,
        axis_forward="-Z", axis_up="Y",
    )


def export_clip(arm, action, path):
    """As author_carry_clip.py, from frame 0 holding a temporary rest key (decision 3)."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene = bpy.context.scene
    for name in KEYED:
        pb = arm.pose.bones[name]
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.keyframe_insert("rotation_quaternion", frame=0)
        if name == "spine":
            pb.keyframe_insert("location", frame=0)
    scene.frame_set(0)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    scene.name = CLIP              # the FBX take is named after the scene
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"},
        global_scale=1.0, apply_unit_scale=False, apply_scale_options="FBX_SCALE_NONE",
        add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
        axis_forward="-Z", axis_up="Y",
    )
    for fc in all_fcurves(action):
        for kp in [k for k in fc.keyframe_points if abs(k.co.x) < 1e-6]:
            fc.keyframe_points.remove(kp)
    scene.frame_set(1)


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 760, 1080
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = False
    # Unity's Standard shader culls back faces, so the renders do too: a face turned the
    # wrong way, or the inside of a single sheet, shows here as a hole, as it will in game.
    scene.display.shading.show_backface_culling = True
    scene.view_settings.view_transform = "Standard"
    scene.display.shading.show_cavity = False
    scene.world = bpy.data.worlds.new("grandma_bg")
    scene.world.color = (0.80, 0.80, 0.82)
    scene.render.image_settings.file_format = "PNG"
    cam_data = bpy.data.cameras.new("grandma_cam")
    cam_data.lens = 60
    cam = bpy.data.objects.new("grandma_cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render(cam, arm, path, yaw_deg, frame=None, dist=6.2, target_z=1.30, pitch_deg=None):
    """yaw 0 looks at her face. The armature carries a 180 degree import rotation, so her
    front is world +Y: aim through the object matrix, never with local numbers. pitch_deg
    raises the camera above (or, negative, below) the target; by default it sits 0.25 up."""
    scene = bpy.context.scene
    if frame is not None:
        scene.frame_set(frame)
    mw = arm.matrix_world
    target = mw @ Vector((0.0, CY, target_z))
    a = math.radians(yaw_deg)
    local_dir = Vector((math.sin(a), -math.cos(a), 0.0))
    world_dir = (mw.to_3x3() @ local_dir).normalized()
    if pitch_deg is None:
        cam.location = target + world_dir * dist + Vector((0, 0, 0.25))
    else:
        e = math.radians(pitch_deg)
        cam.location = target + (world_dir * math.cos(e) + Vector((0, 0, math.sin(e)))) * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED " + path)


# -------------------------------------------------------------------------------- main


def main():
    args = parse_args()
    renders = args.renders or os.path.dirname(os.path.abspath(args.blend))
    body, arm = import_body(args.src)
    bone_names = {b.name for b in arm.data.bones}
    body.name = "Grandma"
    body.data.name = NAME

    print("BODY_EMPTY_AFTER_CLEAN", clean_weights(body, bone_names))
    reshape_body(body, arm)
    made = restyle_materials(body)
    flat_shade(body)

    mat_card = material("grandma_cardigan", COLOURS["cardigan"])
    mat_pearl = material("grandma_pearl", COLOURS["pearl"])

    skirt = build_skirt(body, made["dress"])
    cardigan, card_side = build_cardigan(body, arm, skirt, mat_card)
    tuck_body(body, cardigan)
    paint_under_clothes(body, mat_card, made["dress"])
    bun, _bun_c = build_bun(body, made["hair"])
    pearls = build_pearls(body, cardigan, mat_pearl)

    skin_all = Skin(body)
    skin_torso = Skin(body, torso_filter(body))
    skin_garment(skirt, skin_torso, skirt_probe)
    blend_skirt_into_legs(skirt)
    skin_garment(cardigan, skin_all, cardigan_probe)
    skin_garment(bun, None, fixed={"spine.006": 1.0})
    skin_garment(pearls, skin_torso)

    # One skinned mesh, one renderer in Unity. The parts were built in the body's local
    # frame, so they take the same parent first, or the join would bake the armature's
    # 180 degree import rotation into them.
    parts = [skirt, cardigan, bun, pearls]
    for o in parts:
        o.parent = arm
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts + [body]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    grandma = body
    empty = clean_weights(grandma, bone_names)
    grandma.parent = arm
    me = grandma.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print("GRANDMA verts {} faces {} tris {} materials {} unweighted {}".format(
        len(me.vertices), len(me.polygons), tris, [m.name for m in me.materials], empty))
    zs = [(grandma.matrix_world @ v.co).z for v in me.vertices]
    print("GRANDMA_HEIGHT {:.4f}".format(max(zs) - min(zs)))

    cam = setup_render()
    render(cam, arm, os.path.join(renders, "grandma_front.png"), 0)
    render(cam, arm, os.path.join(renders, "grandma_side.png"), 90)
    render(cam, arm, os.path.join(renders, "grandma_threequarter.png"), 35)
    if args.debug:
        render(cam, arm, os.path.join(renders, "debug_rest_close.png"), 30, dist=2.4, target_z=2.0)
        render(cam, arm, os.path.join(renders, "debug_rest_back.png"), 180)
        # a player (1.80 m) looks down on her shoulders; someone at the foot of the stairs
        # looks up under the hem
        render(cam, arm, os.path.join(renders, "debug_rest_top.png"), 20, dist=2.6,
               target_z=2.0, pitch_deg=62)
        render(cam, arm, os.path.join(renders, "debug_rest_low.png"), 10, dist=2.4,
               target_z=0.9, pitch_deg=-28)
        render(cam, arm, os.path.join(renders, "debug_rest_neck.png"), 15, dist=1.3,
               target_z=2.08, pitch_deg=15)
    export_mesh(arm, grandma, os.path.join(args.out_dir, NAME + ".fbx"))

    # Measurements the idle needs: the front of her clothes at the belly, the side of the
    # cardigan at elbow height.
    front = min(v.co.y for v in me.vertices if 1.52 < v.co.z < 1.72 and abs(v.co.x) < 0.2)
    side = card_side[1.760]
    print("IDLE_MEASURE front_y {:.3f} side_x {:.3f}".format(front, side))
    rig = IdleRig(arm, front, side)
    action = build_idle(arm, rig)
    print("IDLE frames 1..{} loop mismatch {:.2e}".format(LOOP + 1, check_loop(action)))
    mid = 1 + LOOP // 2
    bpy.context.scene.frame_set(mid)
    for s in "LR":
        pb = arm.pose.bones
        print("IDLE_MID {} elbow {} wrist {} fingertips {}".format(
            s, tuple(round(c, 3) for c in pb["forearm." + s].head),
            tuple(round(c, 3) for c in pb["hand." + s].head),
            tuple(round(c, 3) for c in pb["hand." + s].tail)))
    render(cam, arm, os.path.join(renders, "grandma_idle.png"), 35, frame=mid)
    if args.debug:
        render(cam, arm, os.path.join(renders, "debug_idle_front.png"), 0, frame=mid)
        render(cam, arm, os.path.join(renders, "debug_idle_side.png"), 90, frame=mid)
        render(cam, arm, os.path.join(renders, "debug_idle_close.png"), 20, frame=mid,
               dist=2.6, target_z=1.75)
    export_clip(arm, action, os.path.join(args.out_dir, "Anim_" + CLIP + ".fbx"))

    bpy.data.objects.remove(cam)
    os.makedirs(os.path.dirname(args.blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=args.blend)
    print("GRANDMA_DONE")


if __name__ == "__main__":
    main()
