"""Author the vertical slice's character clips on the crew rig, one FBX per clip.

ADR-009 makes the grandmother an autonomous NPC who walks her house, sits, reads, drinks, lights
the fire, gets angry and hands over the keys, and gives the crew bodies a walk, a run and a
crouch so the other player can read what you are doing. SLICE_ARCHITECTURE section 13 lists the
22 clips. There is no mocap and no animator: every clip here is a pose function of time on the
36 bones of male01_1.fbx, baked and exported exactly like model_grandma.py's Grandma_Idle. The
grandmother is the same skeleton at another import scale (model_grandma.py decision 1), so every
clip plays on the crew and on her through Unity's Humanoid retargeting.

Clips (24 fps, in place, N frames per cycle, the take runs 1..N+1, a loop's last frame is its
first; speeds are the ground speed at which the planted foot does not slide):
  Crew_Idle          72  loop   two breaths, a weight shift, a look round, arms clear of the torso
  Crew_Walk          24  loop   A6's walk: 1.20 m/s on the crew (the 1.2 blend threshold)
  Crew_Run           16  loop   A6's run, stride 1.064 BU: 3.71 m/s on the crew (threshold 3.7)
  Crew_CrouchIdle    48  loop   hips 0.35 BU down, torso 20 degrees forward, knees apart
  Crew_CrouchWalk    24  loop   the walk generator crouched: 1.39 m/s on the crew
  Grandma_Walk       28  loop   short steps, a stoop, a waddle: 0.50 m/s on her
  Sit_Down           38  once   from standing to Sit_Idle's first frame, hands back to the armrests
  Sit_Idle           96  loop   upright, hands on the thighs, one breath, a look to her left
  Stand_Up           48  once   Sit_Down reversed, with a stalled push on the thighs
  Sit_Read          144  loop   a book in both hands, head down, a page turned at f96-f114
  Sit_Drink          96  loop   cup from the thigh to the mouth, a sip, back
  Kneel_Down         48  once   a step towards the hearth, the right knee down, then the left
  Kneel_LightFire   120  loop   strike, the match reached onto the logs, shaken out, back to the box
  Kneel_Up           60  once   half kneel, a stalled push on the left knee, up on the root
  Stand_Cook         96  loop   leaning over the stove, stirring a pot, the left hand on her hip
  Stand_Water        96  loop   the watering can held forward, tipped 35 degrees
  Stand_Drink        96  loop   cup and saucer, a sip with the head back
  Angry_ShakeFist    36  loop   upper-body layer: fist up by the head, shaken at 4 Hz
  Angry_HandsOnHips  48  loop   upper-body layer: hands on hips, the head shaking
  Angry_Point        36  loop   upper-body layer: the right arm straight at the culprit
  Talk               72  loop   upper-body layer: palms up, alternating hand beats, nods
  Give_Keys          48  once   the hand out, palm up; the hand-off at 1.0 s (normalized 0.5)

The crew's actions (CHARACTERS, 2026-09-26), authored and rendered on the crew only. The upper-body
ones play on AC_Crew_Slice's "Actions" layer (right arm and head); the falls are base-layer states:
  Crew_Smoke         40  loop   the filter to the lips, a drag, the hand down as the smoke comes out;
                                played by Motion Time on the cigarette's own cycle (SmokeTimeline.cs)
  Crew_Drink         60  loop   the bottle at the lips tipped 22 degrees, head back, four gulps;
                                Motion Time on the drink's clock (four swallows of BeerItem)
  Crew_Throw         20  once   cocked behind the head, released at f4, followed through
  Crew_Pocket        20  once   the right hand into the trouser pocket and out
  Crew_Wear          24  once   the hand on the top of the head, a pat, a tug by the ear
  Crew_Wave          24  loop   the right hand up, waved twice a second (the menus)
  Crew_KnockedDown   30  once   flinch, thrown back off the feet, lands on the back
  Crew_GetUp         40  once   from Crew_KnockedDown's last frame: sits up, squats, stands on the root
  Crew_Fall          56  once   tripped at a run: dive, belly, all fours, one knee, back in the run
                                stride (the loading screen runs, falls and runs on one spot)
Props in the hand are placed from HandSockets' frame (socket_frame), in metres, with the same
numbers as HandHeldProp.cs (CIG, BEER): the clip solves the wrist FROM the prop ("the filter on the
lips"), and grip_report measures where the posed hand really puts it (0.8 cm for the cigarette,
1.9 cm for the bottle's lip). The falls go through floor_guard, so no in-between frame goes under
the floor (a blend of a flat foot into a pointed one dipped 0.20 BU in the first version).

Five decisions worth reading before changing anything:

1. **The rig is not touched and the export is model_grandma.py's.** Bones are posed with
   pose-bone matrices and keyed as quaternions on 20 bones plus the hips location, LINEAR, with
   sign continuity. The FBX is written from frame 0, where a rest-pose key is added for the
   export and removed afterwards: Unity builds the clip's avatar from the file's bone nodes, so
   they must be the rest A pose (model_grandma.py decision 4). Armature only, no leaf bones, -Z
   forward, Y up, no unit scale; the scene is renamed so the take carries the clip's name.

2. **Up is the floor normal, not hips to head.** author_carry_clip.py and model_grandma.py take
   "up" as the hips-to-head axis, which leans 8.2 degrees forward on male01_1 (A6). That is
   harmless for a pose held in place and wrong for a foot that travels: A6's first walk slid its
   planted foot along an 8 degree slope. rig_axes() takes world Z in armature space.

3. **Seats follow A6's placement: the seated hips are over the root.** GRANDMA's ActivitySpot
   for a seat is her pose inside the chair, with the seat's front edge 0.29 m ahead of it, and
   her mover slides the root onto it while Sit_Down plays. So Sit_Idle, Sit_Read and Sit_Drink
   have the hip joints over the root, 0.554 m high, fitted to SM_Rocking_Chair (seat top 0.47 m,
   her lowest point 2 cm into the cushion). Sit_Down starts, and Stand_Up ends, standing 0.34 m
   ahead of the root, calves at the seat edge. The poses are written standing on the root and
   moved SIT_BACK forward at bake time (bake(shift)): spine is the only root bone, so only its
   location key changes. Other seats need their own fit: the armchair (0.57 m) is A6's open
   question 2.

4. **The kneel and the stirring are fitted to the real furniture, where GRANDMA's spots put her.**
   Kneel: Spot_LightFire stands her root 0.61 m in front of PKX_Fireplace's hearth slab (A6:
   "root 0.6 m from the hearth"). Measured on the model at the kit's scale 1.5: the slab is 0.137 m tall, the logs'
   front face is 0.276 m behind its edge, their top 0.539 m up. She steps towards the hearth
   with the left foot, puts the right knee down behind it, then the left, and kneels nearly
   upright with the knees 0.09 m ahead of the root. At the reach, the fingertips stop 4 cm short
   of the logs, 0.44 m up, pointing down, so the match touches them. No vertex of either body
   enters the slab, the logs or the jambs in any frame. Kneel_Up ends standing on the root.
   Cook: Spot_Cook_Stove stands her 0.54 m from SM_Stove_Old's front face, whose front burners
   are 0.20 m further and 0.94 m up. She leans about 30 degrees and her grip circles 0.46 to 0.55 m
   ahead at 1.12 m, so a 0.30 m spoon reaches a pot on the front right burner; nothing of her
   goes past the stove's front below its cooktop. The stand-up activities without furniture
   contact (Stand_Water, Stand_Drink) need no fit.

5. **Limbs are solved hinge-aligned, for Humanoid.** A Humanoid avatar gives the forearm and the
   shin only a bend and a twist; any other rotation of those bones is dropped on import and the
   hand or foot lands somewhere else in Unity. So the upper bone is rolled until the joint's
   rest bend axis lies across the new bend, and the lower bone turns about that axis only. What
   is left off the hinge is reported per clip: 0.054 degrees at worst. Gaits are A6's
   generator: shoe transforms that pivot on the heel and near the toe (the pack weights the whole shoe to one
   bone), two-bone IK, hips lowered until no frame needs more than 99.2 % of the leg. Left heel
   contact at t = 0 and right at 0.5 in every gait, so blend-tree children stay in phase.

Run headless, no GUI and no MCP:

    blender -b --factory-startup --python tools/blender/author_clips.py -- --verify \
        --renders C:/tmp/clips --report C:/tmp/clips/report.txt

Arguments after a bare `--`: --out-dir (one FBX per clip, by default where Unity keeps them,
Assets/_Project/Art/Characters/Clips), --renders (PNG folder, never Assets; none if omitted),
--report (the measurements as text), --clips (comma separated names, default all), --crew and
--grandma (the two bodies, read only; the grandmother's skirt is only measured, so pass the
re-skinned SM_Grandma.fbx of model_grandma.py decision 6 until it is in Assets), --verify
(re-import every FBX in a fresh scene and compare it with both rigs). Reads SM_Rocking_Chair,
PKX_Fireplace and SM_Stove_Old for the renders. About 60 s for all clips with renders.

Crew actions validated 2026-09-26, Blender 5.1.2: re-import of the 9 FBX: 36 bones equal to both
rigs, take named like the file, loops close (0.0e+00); worst leg reach 0.985 (Crew_Fall 0.957),
lowest body point +0.0030 BU on the falls, off-hinge 0.07 degrees at worst; renders on two sides.

Validated 2026-09-26, Blender 5.1.2. Re-import of all 22 FBX: 36 bones, names, parents and rest
positions equal to male01_1's and SM_Grandma's (delta 0.00000 BU), take named like the file, 369
curves, first frame equal to last on every loop. Planted pivots, on real shoe vertices, move
backward in a straight line within 0.0003 BU at 1.727 BU/s (Crew_Walk), 5.327 (Crew_Run), 2.000
(Crew_CrouchWalk) and 0.805 (Grandma_Walk), less than 6 mm off the floor. Planted contacts of
every standing, sitting and kneeling loop drift 0.0000 BU. Seated, no skirt vertex over the seat
goes below its top (lowest 0.476 m), and with the chair mesh itself nothing of her is deeper
than 2 cm (the cushion sink, the cardigan on the backrest, the hands on the armrests). Renders
(Workbench, back-face culling on, as Unity) show every clip on both bodies with its prop.
"""

import argparse
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = r"C:\GameProject"
ASSETS = os.path.join(ROOT, r"UnityProject\Assets")
CREW_SRC = os.path.join(ASSETS, r"Floreswa\Models\male01_1.fbx")
GRANDMA_SRC = os.path.join(ASSETS, r"_Project\Art\Characters\Grandma\SM_Grandma.fbx")
ART = os.path.join(ASSETS, r"_Project\Art")
DEFAULT_OUT = os.path.join(ASSETS, r"_Project\Art\Characters\Clips")

FPS = 24
CREW_SCALE = 0.6956183      # male01_1.fbx.meta globalScale: 2.59 BU -> 1.80 m
GRANDMA_SCALE = 0.616       # SM_Grandma.fbx.meta globalScale: 2.565 BU -> 1.58 m
BODY_VERTS = 933            # SM_Grandma keeps the crew body's vertex order: 0..932 are the body
SEAT_M = 0.47               # SM_Rocking_Chair seat top, measured by rays (A6)
SINK_M = 0.02               # her lowest body point sits at 0.45 m, the seat height the slice targets
SEAT_DEPTH_M = 0.444        # SM_Rocking_Chair seat depth, measured
SEAT_HALF_M = 0.275         # SM_Rocking_Chair half width
SEAT_EDGE_FROM_HIPS = 0.47  # BU: seat front edge ahead of the seated hip joints (A6, calves clear)
SIT_BACK = 0.55             # BU: from where she stands to where her hips land on the seat

KEYED = ["spine", "spine.001", "spine.002", "spine.003", "spine.004", "spine.005",
         "shoulder.L", "shoulder.R", "upper_arm.L", "upper_arm.R", "forearm.L", "forearm.R",
         "hand.L", "hand.R", "thigh.L", "thigh.R", "shin.L", "shin.R", "foot.L", "foot.R"]


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="author_clips")
    p.add_argument("--out-dir", default=DEFAULT_OUT, help="one FBX per clip is written here")
    p.add_argument("--renders", default=None, help="PNG folder (never Assets); no renders if omitted")
    p.add_argument("--clips", default="all", help="comma separated clip names, or all")
    p.add_argument("--crew", default=CREW_SRC)
    p.add_argument("--grandma", default=GRANDMA_SRC)
    p.add_argument("--report", default=None, help="write the measurements here (text)")
    p.add_argument("--verify", action="store_true",
                   help="re-import every FBX in --out-dir in a fresh scene and compare with the rig")
    return p.parse_args(argv)


# ----------------------------------------------------------------------------- helpers


def smoothstep(e0, e1, x):
    if e1 <= e0:
        return 1.0 if x >= e1 else 0.0
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def envelope(t, a, b, c, d):
    """0 until a, eases to 1 by b, holds until c, eases back to 0 by d."""
    if t <= a or t >= d:
        return 0.0
    if t < b:
        return smoothstep(a, b, t)
    if t <= c:
        return 1.0
    return 1.0 - smoothstep(c, d, t)


def hermite(p0, p1, m0, m1, w):
    w2, w3 = w * w, w * w * w
    return ((2 * w3 - 3 * w2 + 1) * p0 + (w3 - 2 * w2 + w) * m0
            + (-2 * w3 + 3 * w2) * p1 + (w3 - w2) * m1)


def upd():
    bpy.context.view_layer.update()


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


def rig_axes(arm):
    """Up is the FLOOR normal, forward the toe direction flattened onto the floor.

    author_carry_clip.py and model_grandma.py take up = hips -> head top. On male01_1 that axis
    leans 8.2 degrees forward (the head sits 0.13 BU in front of the hips): harmless for a pose
    held in place, wrong for a foot that travels along `forward` (A6: the planted foot slid
    along an 8 degree slope). Read off the rest bones, so the current pose does not matter."""
    up = (arm.matrix_world.inverted().to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
    b = arm.data.bones["toe.L"]
    toe = b.tail_local - b.head_local
    fwd = (toe - up * toe.dot(up)).normalized()
    return up, fwd, fwd.cross(up).normalized()


def frame3(d, h):
    """Orthonormal 3x3 with columns (d, h made perpendicular to d, d x h)."""
    d = d.normalized()
    h = (h - d * h.dot(d)).normalized()
    return Matrix((d, h, d.cross(h))).transposed()


def signed_angle(a, b, axis):
    return math.atan2(axis.dot(a.cross(b)), a.dot(b))


# ------------------------------------------------------------------------ key interpolation
# A clip is a list of keys (frame, {channel: value}). Numbers and vectors go through a monotone
# cubic (Fritsch-Carlson): motion flows through a key when it keeps going the same way and
# eases to a stop only at a turning point or a hold, with no overshoot. Other values (a target
# computed from the posed body, None) blend pairwise and are resolved at pose time.


class Mix:
    def __init__(self, a, b, w):
        self.a, self.b, self.w = a, b, w


def _mono(ts, ys, t, loop):
    n = len(ts)
    if n == 1:
        return ys[0]
    d = [(ys[i + 1] - ys[i]) / (ts[i + 1] - ts[i]) if ts[i + 1] > ts[i] else 0.0 for i in range(n - 1)]
    m = [0.0] * n
    for i in range(1, n - 1):
        if d[i - 1] * d[i] > 0.0:
            m[i] = 2.0 / (1.0 / d[i - 1] + 1.0 / d[i])
    if loop and n > 2 and d[-1] * d[0] > 0.0:
        m[0] = m[-1] = 2.0 / (1.0 / d[-1] + 1.0 / d[0])
    i = 0
    while i < n - 2 and t > ts[i + 1]:
        i += 1
    h = ts[i + 1] - ts[i]
    if h <= 0.0:
        return ys[i + 1]
    w = min(max((t - ts[i]) / h, 0.0), 1.0)
    return hermite(ys[i], ys[i + 1], m[i] * h, m[i + 1] * h, w)


def _is_num(v):
    return isinstance(v, (int, float))


def sample(keys, f, loop=False):
    """Channel values at frame f (0..N) from keys [(frame, dict)]. Every key has every channel."""
    ts = [k[0] for k in keys]
    out = {}
    for name in keys[0][1]:
        vals = [k[1][name] for k in keys]
        if all(_is_num(v) for v in vals):
            out[name] = _mono(ts, [float(v) for v in vals], f, loop)
        elif all(isinstance(v, Vector) for v in vals):
            out[name] = Vector([_mono(ts, [v[c] for v in vals], f, loop) for c in range(len(vals[0]))])
        else:
            i = 0
            while i < len(ts) - 2 and f > ts[i + 1]:
                i += 1
            w = smoothstep(ts[i], ts[i + 1], f)
            a, b = vals[i], vals[i + 1]
            out[name] = a if (a is b or w <= 0.0) else (b if w >= 1.0 else Mix(a, b, w))
    return out


def resolve(fig, v):
    if isinstance(v, Mix):
        a, b = resolve(fig, v.a), resolve(fig, v.b)
        if a is None or b is None:
            return a if v.w < 0.5 else b
        return a.lerp(b, v.w) if isinstance(a, Vector) else a + (b - a) * v.w
    if callable(v):
        return v(fig)
    return v


# ------------------------------------------------------------------------------- body


class Figure:
    """Poses the crew skeleton from a flat dict of channels, in armature space (BU).

    Directions are built from the floor frame: U up, F forward, L her left (+X), R her right.
    V(f, l, u) is a point `f` forward of the root, `l` to her left, `u` up.

    Limbs use HINGE-ALIGNED IK. The Humanoid avatar gives the forearm and the shin only a bend
    and a twist: a rotation of the lower bone about any other axis is dropped on import, and the
    hand or foot then lands somewhere else in Unity. So the upper bone is rolled until the
    rest bend axis of the joint (the slight bend of this A pose, which is the flexion plane)
    lies across the new bend, and the lower bone then turns about that axis only.
    """

    def __init__(self, arm):
        self.arm = arm
        self.pb = arm.pose.bones
        bones = arm.data.bones
        self.U, self.F, self.R = rig_axes(arm)
        self.L = -self.R
        self.flex = self.U.cross(self.F)       # + about this bends forward
        self.len = {b.name: b.length for b in bones}
        self.hip_mid0 = (bones["thigh.L"].head_local + bones["thigh.R"].head_local) * 0.5
        self.ankle0 = {s: bones["shin." + s].tail_local.copy() for s in "LR"}
        self.foot_rest = {s: bones["foot." + s].matrix_local.copy() for s in "LR"}
        self.tip0 = {s: bones["toe." + s].tail_local.copy() for s in "LR"}   # the shoe tip
        self.hinge = {}
        for s in "LR":
            for upper, lower in (("upper_arm." + s, "forearm." + s), ("thigh." + s, "shin." + s)):
                bu, bl = bones[upper], bones[lower]
                n = (bu.tail_local - bu.head_local).normalized().cross(
                    (bl.tail_local - bl.head_local).normalized()).normalized()
                self.hinge[upper] = bu.matrix_local.to_3x3().inverted() @ n
        self.palm_local = {}
        for s, sign in (("L", -1.0), ("R", 1.0)):
            bh = bones["hand." + s]
            ax = (bh.tail_local - bh.head_local).normalized()
            p = Vector((sign, 0.0, 0.0))
            p = (p - ax * p.dot(ax)).normalized()     # the A pose palm faces the thigh
            self.palm_local[s] = bh.matrix_local.to_3x3().inverted() @ p
        self.max_reach = 0.0
        self.mouth_local = None                 # set by measure_face()
        # Metres per BU in Unity: the body's import scale. The crew's by default; main() sets the
        # grandmother's. Props and hand sockets are authored in metres (HandSockets, HandHeldProp).
        self.m_per_bu = CREW_SCALE

    # -- small tools -------------------------------------------------------------------
    def V(self, f, l, u):
        return self.F * f + self.L * l + self.U * u

    def out(self, s):
        return self.L if s == "L" else self.R

    def comp(self, v):
        """(forward, left, up) components of an armature-space vector."""
        return (v.dot(self.F), v.dot(self.L), v.dot(self.U))

    def reset(self):
        for pb in self.pb:
            pb.rotation_mode = "QUATERNION"
            pb.location = (0.0, 0.0, 0.0)
            pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
            pb.scale = (1.0, 1.0, 1.0)
        upd()

    def rot(self, name, axis, deg, pivot=None):
        if abs(deg) < 1e-9:
            return
        pb = self.pb[name]
        c = pb.head.copy() if pivot is None else pivot
        pb.matrix = (Matrix.Translation(c) @ Matrix.Rotation(math.radians(deg), 4, axis.normalized())
                     @ Matrix.Translation(-c) @ pb.matrix)
        upd()

    def point(self, name, direction):
        pb = self.pb[name]
        cur = (pb.tail - pb.head).normalized()
        q = cur.rotation_difference(direction.normalized())
        head = pb.head.copy()
        pb.matrix = (Matrix.Translation(head) @ q.to_matrix().to_4x4()
                     @ Matrix.Translation(-head) @ pb.matrix)
        upd()

    def hip_mid(self):
        return (self.pb["thigh.L"].head + self.pb["thigh.R"].head) * 0.5

    def bone_point(self, name, frac, offset=None):
        pb = self.pb[name]
        p = pb.head.lerp(pb.tail, frac)
        return p if offset is None else p + offset

    def mouth(self):
        return self.pb["spine.005"].matrix @ self.mouth_local

    # -- limbs -------------------------------------------------------------------------
    def orient(self, name, direction, hinge):
        """Turn a limb's upper bone to `direction`, with its rest bend axis along `hinge`."""
        pb = self.pb[name]
        r = pb.matrix.to_3x3()
        cur_d = (r @ Vector((0.0, 1.0, 0.0))).normalized()
        cur_h = (r @ self.hinge[name]).normalized()
        m = frame3(direction, hinge) @ frame3(cur_d, cur_h).transposed()
        head = pb.head.copy()
        pb.matrix = Matrix.Translation(head) @ m.to_4x4() @ Matrix.Translation(-head) @ pb.matrix
        upd()

    def limb(self, upper, lower, target, pole):
        """Two-bone IK, hinge aligned. Returns reach = distance / (a + b) before the clamp."""
        s = self.pb[upper].head.copy()
        a, b = self.len[upper], self.len[lower]
        dv = target - s
        reach = dv.length / (a + b)
        d = min(max(dv.length, abs(a - b) + 1e-4), a + b - 1e-4)
        u = dv.normalized()
        alpha = math.acos(max(-1.0, min(1.0, (a * a + d * d - b * b) / (2 * a * d))))
        v = pole - u * pole.dot(u)
        if v.length < 1e-6:
            v = u.orthogonal()
        v.normalize()
        d1 = (u * math.cos(alpha) + v * math.sin(alpha)).normalized()
        elbow = s + d1 * a
        d2 = (s + u * d - elbow).normalized()
        self.orient(upper, d1, v.cross(u))
        self.point(lower, d2)
        return reach

    def limb_dirs(self, upper, lower, d1, d2, pole=None):
        """Forward kinematics with the same hinge alignment: upper along d1, lower along d2."""
        n = d1.cross(d2)
        if n.length < 1e-5:
            p = pole if pole is not None else self.F
            n = (p - d1 * p.dot(d1)).cross(d1)
        self.orient(upper, d1.normalized(), n.normalized())
        self.point(lower, d2)

    def palm_to(self, s, palm):
        """Twist the forearm about its own axis (a Humanoid twist) so the palm faces `palm`."""
        fa = self.pb["forearm." + s]
        ax = (fa.tail - fa.head).normalized()
        c = self.pb["hand." + s].matrix.to_3x3() @ self.palm_local[s]
        c = c - ax * c.dot(ax)
        p = palm - ax * palm.dot(ax)
        if c.length < 1e-6 or p.length < 1e-6:
            return
        self.rot("forearm." + s, ax, math.degrees(signed_angle(c.normalized(), p.normalized(), ax)))

    def palm_now(self, s):
        return (self.pb["hand." + s].matrix.to_3x3() @ self.palm_local[s]).normalized()

    def foot_rot(self, pitch, yaw):
        return (Matrix.Rotation(math.radians(yaw), 3, self.U)
                @ Matrix.Rotation(math.radians(pitch), 3, self.R))

    def toe_of(self, s, ankle, pitch, yaw=0.0):
        """Where the shoe tip lands for this ankle and foot angle (the inverse of leg%s_toe)."""
        return ankle + self.foot_rot(pitch, yaw) @ (self.tip0[s] - self.ankle0[s])

    def set_foot(self, s, pitch, yaw):
        """Rigid shoe: the rest foot, turned `pitch` (+ toe up) and `yaw` (+ toe to her left),
        hung on the ankle where the IK left it."""
        rot = self.foot_rot(pitch, yaw).to_4x4()
        ank = self.pb["shin." + s].tail.copy()
        self.pb["foot." + s].matrix = (Matrix.Translation(ank) @ rot
                                       @ Matrix.Translation(-self.ankle0[s]) @ self.foot_rest[s])
        upd()

    # -- the whole body from channels ---------------------------------------------------
    def apply(self, P):
        U, F, L, flex = self.U, self.F, self.L, self.flex

        def g(k, d=0.0):
            return P.get(k, d)

        self.reset()
        hm = self.hip_mid()
        tgt = resolve(self, g("hip", None))
        if tgt is not None:
            hips = self.pb["spine"]
            hips.matrix = Matrix.Translation(tgt - hm) @ hips.matrix
            upd()
        piv = self.hip_mid()
        self.rot("spine", U, g("pel_yaw"), piv)
        self.rot("spine", F, -g("pel_side"), piv)          # + lifts her right hip
        self.rot("spine", flex, g("pel_flex"), piv)          # + tilts the pelvis forward
        for i, name in enumerate(("spine.001", "spine.002", "spine.003")):
            self.rot(name, U, g("s_yaw") / 3.0)
            self.rot(name, F, -g("s_side") / 3.0)            # + bends the torso to her left
            self.rot(name, flex, g("s%d" % (i + 1)))
        self.rot("spine.004", U, g("head_yaw") * 0.35)
        self.rot("spine.004", flex, g("neck"))
        self.rot("spine.005", U, g("head_yaw") * 0.65)
        self.rot("spine.005", F, -g("head_roll"))
        self.rot("spine.005", flex, g("head"))
        self.rot("shoulder.L", F, g("shrugL"))
        self.rot("shoulder.R", -F, g("shrugR"))
        self.rot("shoulder.L", U, -g("clavfL"))
        self.rot("shoulder.R", U, g("clavfR"))
        for s in "LR":
            ank = resolve(self, g("leg%s_ankle" % s, None))
            toe = resolve(self, g("leg%s_toe" % s, None))
            if toe is not None:              # the foot rolls on its toe: the ankle follows
                ank = toe - self.foot_rot(g("leg%s_pitch" % s), g("leg%s_yaw" % s)) @ (self.tip0[s] - self.ankle0[s])
            if ank is None:
                continue
            pole = resolve(self, g("leg%s_pole" % s, None)) or F
            self.max_reach = max(self.max_reach, self.limb("thigh." + s, "shin." + s, ank, pole))
            self.set_foot(s, g("leg%s_pitch" % s), g("leg%s_yaw" % s))
        for s in "LR":
            w = resolve(self, g("arm%s_wrist" % s, None))
            if w is not None:
                pole = resolve(self, g("arm%s_pole" % s, None)) or (-F + self.out(s) - U)
                self.limb("upper_arm." + s, "forearm." + s, w, pole)
            palm = resolve(self, g("arm%s_palm" % s, None))
            if palm is not None:
                self.palm_to(s, palm)
            hd = resolve(self, g("arm%s_hand" % s, None))
            if hd is not None:
                self.point("hand." + s, hd)
            hf = g("arm%s_flex" % s)
            if hf:
                hb = self.pb["hand." + s]
                ax = (hb.tail - hb.head).normalized()
                self.rot("hand." + s, ax.cross(self.palm_now(s)), hf)   # + curls towards the palm


# ------------------------------------------------------------------------ mesh measuring


def dominant(body):
    gi = {g.index: g.name for g in body.vertex_groups}
    out = []
    for v in body.data.vertices:
        if v.groups:
            g = max(v.groups, key=lambda g: g.weight)
            out.append(gi.get(g.group, ""))
        else:
            out.append("")
    return out


def vert_materials(body):
    names = [m.name for m in body.data.materials]
    vm = [set() for _ in body.data.vertices]
    for p in body.data.polygons:
        for i in p.vertices:
            vm[i].add(names[p.material_index])
    return vm


def rest_coords(body, arm):
    M = arm.matrix_world.inverted() @ body.matrix_world
    return [M @ v.co for v in body.data.vertices]


def evaluated_coords(body, arm):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    M = arm.matrix_world.inverted() @ body.matrix_world
    co = [M @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    return co


def torso_half_width(fig, body, u0, u1):
    """Widest |left| of the torso (body and garments, arms excluded) between heights u0 and u1."""
    best = 0.0
    for c, name in zip(rest_coords(body, fig.arm), dominant(body)):
        if name.startswith(("upper_arm", "forearm", "hand")):
            continue
        f, l, u = fig.comp(c)
        if u0 < u < u1:
            best = max(best, abs(l))
    return best


def shoe_verts(body, side):
    names = ("toe." + side, "foot." + side, "heel.02." + side)
    return [i for i, n in enumerate(dominant(body)) if n in names]


# ------------------------------------------------------------------------------ arms


def hang_dirs(fig, s, half, gap, swing, bend, fore_in):
    """Upper arm and forearm directions of a hanging arm, in the chest frame (A6 walk arms).
    The elbow is held `gap` outside the torso half width `half`, so the arm clears the body."""
    U, F = fig.U, fig.F
    out = fig.out(s)
    chest = fig.pb["spine.003"]
    crot = (chest.matrix @ fig.arm.data.bones["spine.003"].matrix_local.inverted()).to_3x3()
    fwd_c = (crot @ F).normalized()
    sh = fig.pb["upper_arm." + s].head
    l1 = fig.len["upper_arm." + s]
    abd = math.asin(max(0.0, min(0.9, (half + gap - abs(sh.dot(fig.L))) / l1)))
    base = -U * math.cos(abd) + out * math.sin(abd)
    ang = -swing if s == "L" else swing           # + swing = forward, about +X for both
    d1 = crot @ (Matrix.Rotation(math.radians(ang), 3, out) @ base)
    hinge = d1.cross(fwd_c).normalized()
    d2 = Matrix.Rotation(math.radians(bend), 3, hinge) @ d1
    d2 = (d2 - out * fore_in).normalized()
    return d1.normalized(), d2, hinge


def hang(s, half, gap=0.085, swing=0.0, bend=12.0, fore_in=0.10, wrist_deg=6.0):
    """Channels for a relaxed hanging arm: wrist, pole and hand as functions of the posed body."""
    def dirs(fig):
        return hang_dirs(fig, s, resolve(fig, half), gap, resolve(fig, swing),
                         resolve(fig, bend), fore_in)

    def wrist(fig):
        d1, d2, _h = dirs(fig)
        sh = fig.pb["upper_arm." + s].head
        return sh + d1 * fig.len["upper_arm." + s] + d2 * fig.len["forearm." + s]

    def pole(fig):
        return dirs(fig)[0]

    def hand(fig):
        _d1, d2, h = dirs(fig)
        return Matrix.Rotation(math.radians(wrist_deg), 3, h) @ d2

    return {"arm%s_wrist" % s: wrist, "arm%s_pole" % s: pole, "arm%s_hand" % s: hand,
            "arm%s_palm" % s: keep_palm(s), "arm%s_flex" % s: 0.0}


def keep_palm(s):
    """The palm the IK left: no forearm twist. Lets a key blend into a twisted palm smoothly."""
    return lambda fig: fig.palm_now(s)


# ------------------------------------------------------------------------------- gait


class Gait:
    """A walk or a run as a function of the loop phase t in [0, 1). Periodic by construction.
    A6's generator (author_locomotion.py), unchanged in its numbers, on hinge-aligned limbs.

    `stride` is how far the planted ankle travels backward during stance, `duty` the stance
    fraction: the ground speed at which the clip reads as planted is
    stride / (duty * cycle_seconds) BU/s. Left heel contact at t = 0, right at t = 0.5, so gaits
    of different lengths stay in phase inside one blend tree.
    """

    def __init__(self, fig, p, torso_half):
        self.fig, self.p = fig, p
        self.ankle = fig.ankle0
        self.foot_rest = fig.foot_rest
        V = fig.V
        # sole pivots measured off the shoe mesh: heel 0.060 behind the ankle, ball near the tip
        self.heel = {s: V(-0.060, fig.ankle0[s].dot(fig.L), 0.0) for s in "LR"}
        self.ball = {s: V(0.285, fig.ankle0[s].dot(fig.L), 0.0) for s in "LR"}
        self.torso_half = torso_half
        self.drop = p["drop0"]
        self.max_reach = 0.0

    def pitch(self, u):
        p = self.p
        D, hs, to = p["duty"], p["heel_strike"], -p["toe_off"]
        if u < D:
            return piecewise(u, [(0.0, hs), (p["flat_at"] * D, 0.0),
                                 (p["heel_off_at"] * D, 0.0), (D, to)])
        w = (u - D) / (1.0 - D)
        return piecewise(w, [(0.0, to), (0.40, p["mid_swing_pitch"]), (0.85, hs), (1.0, hs)])

    def foot_frame(self, s, u):
        """Rigid transform of the shoe (armature space) for foot s at its own phase u."""
        p = self.p
        fig = self.fig
        D, S, ahead = p["duty"], p["stride"], p["ahead"]
        if u < D:
            x = S * ahead - S * (u / D)
            lift = 0.0
        else:
            w = (u - D) / (1.0 - D)
            m = -S * (1.0 - D) / D * p["swing_tangent"]
            x = hermite(-S * (1.0 - ahead), S * ahead, m, m, w)
            lift = p["lift"] * math.sin(math.pi * (w ** p["lift_skew"])) ** 1.2
        pitch = self.pitch(u)
        pivot = self.heel[s] if pitch > 0 else self.ball[s]
        inward = -fig.L * (self.ankle[s].dot(fig.L) * p["narrow"])
        disp = fig.F * x + fig.U * lift + inward
        rot = Matrix.Rotation(math.radians(pitch), 4, fig.R)    # + = toe up
        return Matrix.Translation(disp + pivot) @ rot @ Matrix.Translation(-pivot)

    def pose(self, t):
        fig, p = self.fig, self.p
        U, F, R, L, flex = fig.U, fig.F, fig.R, fig.L, fig.flex
        fig.reset()
        c2 = math.cos(2.0 * math.tau * (t - p["bob_phase"]))           # twice per cycle
        c1 = math.sin(math.tau * (t - p["sway_phase"]))                 # once per cycle
        hips = fig.pb["spine"]
        hips.matrix = Matrix.Translation(U * (-self.drop + p["bob"] * c2) + L * (p["sway"] * c1)
                                         + F * p["hip_forward"]) @ hips.matrix
        upd()
        yaw = p["pelvis_yaw"] * math.cos(math.tau * t)   # L hip forward at L contact
        fig.rot("spine", U, -yaw)
        fig.rot("spine", F, p["pelvis_roll"] * c1)        # swing-side hip drops
        fig.rot("spine", flex, p["lean"] * 0.4)
        fig.rot("spine.001", F, -p["pelvis_roll"] * c1 * 0.8)
        fig.rot("spine.001", flex, p["lean"] * 0.3)
        fig.rot("spine.002", flex, p["lean"] * 0.3 + p["stoop"] * 0.4)
        fig.rot("spine.002", U, yaw * (1.0 + p["chest_counter"]) * 0.5)
        fig.rot("spine.003", U, yaw * (1.0 + p["chest_counter"]) * 0.5)
        fig.rot("spine.003", flex, p["stoop"] * 0.6)
        net_yaw = yaw * p["chest_counter"]
        fig.rot("spine.004", U, -net_yaw * 0.5)
        fig.rot("spine.005", U, -net_yaw * 0.5)
        fig.rot("spine.004", flex, p["neck_forward"])
        fig.rot("spine.005", flex, -(p["lean"] + p["stoop"] + p["neck_forward"]) * 0.9
                + p["head_bob"] * c2)

        for s, off in (("L", 0.0), ("R", 0.5)):
            u = (t - off) % 1.0
            Fm = self.foot_frame(s, u)
            reach = fig.limb("thigh." + s, "shin." + s, Fm @ self.ankle[s],
                             F + fig.out(s) * p.get("knee_out", 0.12))
            self.max_reach = max(self.max_reach, reach)
            fig.pb["foot." + s].matrix = Fm @ self.foot_rest[s]
            upd()

        for s in "LR":
            sign = -1.0 if s == "L" else 1.0            # L arm back when the L leg is forward
            swing = sign * p["arm_swing"] * math.cos(math.tau * (t - p["arm_lag"])) + p["arm_forward"]
            frac = max(0.0, swing) / max(1e-3, p["arm_swing"] + abs(p["arm_forward"]))
            bend = p["elbow"] + p["elbow_swing"] * frac
            d1, d2, h = hang_dirs(fig, s, self.torso_half, p["elbow_gap"], swing, bend, p["forearm_in"])
            fig.limb_dirs("upper_arm." + s, "forearm." + s, d1, d2)
            fig.point("hand." + s, Matrix.Rotation(math.radians(p["wrist"]), 3, h) @ d2)


def piecewise(u, pts):
    """Smoothstep between successive (u, value) points; pts sorted, covering [0, 1]."""
    for (u0, v0), (u1, v1) in zip(pts, pts[1:]):
        if u0 <= u <= u1:
            return v0 + (v1 - v0) * smoothstep(u0, u1, u)
    return pts[-1][1]


def fit_drop(gait, frames, limit=0.992):
    """Lower the hips until no frame asks for more than `limit` of the leg length (no knee snap)."""
    for _ in range(40):
        gait.max_reach = 0.0
        for f in range(frames):
            gait.pose(f / frames)
        if gait.max_reach <= limit:
            break
        gait.drop += 0.01
    return gait.drop, gait.max_reach


# A6's numbers. Crew_Run's stride goes from 1.05 to 1.064 so its native speed is 3.70 m/s,
# the Run threshold of the Locomotion blend tree (SLICE_ARCHITECTURE section 13).
WALK = dict(frames=24, duty=0.62, stride=1.07, ahead=0.44, swing_tangent=0.55,
            lift=0.10, lift_skew=0.85, heel_strike=16.0, toe_off=20.0, flat_at=0.16,
            heel_off_at=0.55, mid_swing_pitch=4.0, narrow=0.10,
            drop0=0.03, bob=0.018, bob_phase=0.29, sway=0.022, sway_phase=0.06,
            hip_forward=0.0, pelvis_yaw=5.0, pelvis_roll=3.0, lean=3.0, stoop=0.0,
            chest_counter=1.4, neck_forward=0.0, head_bob=0.8,
            arm_swing=17.0, arm_forward=2.0, arm_lag=0.04, elbow=14.0, elbow_swing=16.0,
            elbow_gap=0.085, forearm_in=0.10, wrist=6.0)

RUN = dict(frames=16, duty=0.30, stride=1.064, ahead=0.30, swing_tangent=0.35,
           lift=0.34, lift_skew=0.62, heel_strike=6.0, toe_off=28.0, flat_at=0.25,
           heel_off_at=0.45, mid_swing_pitch=-10.0, narrow=0.14,
           drop0=0.08, bob=0.040, bob_phase=0.43, sway=0.012, sway_phase=0.10,
           hip_forward=0.02, pelvis_yaw=7.0, pelvis_roll=2.5, lean=11.0, stoop=0.0,
           chest_counter=1.5, neck_forward=0.0, head_bob=1.2,
           arm_swing=32.0, arm_forward=6.0, arm_lag=0.05, elbow=78.0, elbow_swing=18.0,
           elbow_gap=0.095, forearm_in=0.28, wrist=8.0)

# Crouched: hips 0.33 BU down, torso 20 degrees forward, knees apart, feet a little wider,
# a flatter longer step. 1.20 BU in 0.6 s of stance: 2.0 BU/s, 1.39 m/s on the crew.
CROUCH_WALK = dict(frames=24, duty=0.60, stride=1.20, ahead=0.46, swing_tangent=0.50,
                   lift=0.09, lift_skew=0.85, heel_strike=8.0, toe_off=14.0, flat_at=0.18,
                   heel_off_at=0.60, mid_swing_pitch=2.0, narrow=-0.10, knee_out=0.45,
                   drop0=0.33, bob=0.010, bob_phase=0.29, sway=0.030, sway_phase=0.06,
                   hip_forward=-0.05, pelvis_yaw=4.0, pelvis_roll=2.0, lean=20.0, stoop=0.0,
                   chest_counter=1.0, neck_forward=-4.0, head_bob=0.5,
                   arm_swing=12.0, arm_forward=12.0, arm_lag=0.05, elbow=42.0, elbow_swing=12.0,
                   elbow_gap=0.10, forearm_in=0.15, wrist=8.0)

# An old woman: short steps, long stance, low feet, a stoop, arms close and barely swinging.
GRANDMA_WALK = dict(frames=28, duty=0.66, stride=0.62, ahead=0.42, swing_tangent=0.45,
                    lift=0.055, lift_skew=0.9, heel_strike=8.0, toe_off=10.0, flat_at=0.2,
                    heel_off_at=0.6, mid_swing_pitch=3.0, narrow=0.0,
                    drop0=0.04, bob=0.010, bob_phase=0.31, sway=0.045, sway_phase=0.06,
                    hip_forward=0.0, pelvis_yaw=3.0, pelvis_roll=4.0, lean=5.0, stoop=9.0,
                    chest_counter=0.6, neck_forward=8.0, head_bob=0.5,
                    arm_swing=7.0, arm_forward=6.0, arm_lag=0.06, elbow=28.0, elbow_swing=10.0,
                    elbow_gap=0.11, forearm_in=0.18, wrist=10.0)


# --------------------------------------------------------------------------- the context


class Ctx:
    """Measured numbers every pose function reads: widths, the seat fit, the kneel fit."""
    crew_half = 0.243
    g_half = 0.30
    g_hip_half = 0.30
    sit = None
    kneel = None


def measure_face(figs, body):
    """The mouth, as a point fixed to the head bone (for the cup)."""
    vm = vert_materials(body)
    co = rest_coords(body, figs[0].arm)
    pts = [c for c, m in zip(co, vm) if "mouth" in m]
    mouth = sum(pts, Vector()) / len(pts)
    head = figs[0].arm.data.bones["spine.005"].matrix_local
    for fig in figs:
        fig.mouth_local = head.inverted() @ mouth
    return mouth


def rest_ankle(fig, s, df=0.0, dl=0.0):
    return fig.ankle0[s] + fig.F * df + fig.L * dl


def planted(fig, df=(0.0, 0.0), dl=(0.0, 0.0), yaw=(0.0, 0.0)):
    """Both feet flat on the floor at the rest stance (optionally moved or turned)."""
    P = {}
    for i, s in enumerate("LR"):
        P["leg%s_ankle" % s] = rest_ankle(fig, s, df[i], dl[i])
        P["leg%s_pole" % s] = fig.F + fig.out(s) * 0.10
        P["leg%s_pitch" % s] = 0.0
        P["leg%s_yaw" % s] = yaw[i]
    return P


def g_stand(ctx, fig):
    """Her standing base, Grandma_Idle's posture: a slight stoop, the forward head of old age."""
    P = dict(hip=fig.hip_mid0 - fig.U * 0.016, pel_yaw=0.0, pel_side=0.0, pel_flex=0.0,
             s1=3.0, s2=4.0, s3=3.0, s_yaw=0.0, s_side=0.0, neck=10.0, head=-12.0,
             head_yaw=0.0, head_roll=0.0, shrugL=0.0, shrugR=0.0, clavfL=0.0, clavfR=0.0)
    P.update(planted(fig))
    return P


def c_stand(ctx, fig):
    P = dict(hip=fig.hip_mid0 - fig.U * 0.012, pel_yaw=0.0, pel_side=0.0, pel_flex=0.0,
             s1=1.0, s2=0.0, s3=0.0, s_yaw=0.0, s_side=0.0, neck=2.0, head=-2.0,
             head_yaw=0.0, head_roll=0.0, shrugL=0.0, shrugR=0.0, clavfL=0.0, clavfR=0.0)
    P.update(planted(fig))
    return P


def arm_ch(s, wrist, pole, hand, palm, flex=0.0):
    return {"arm%s_wrist" % s: wrist, "arm%s_pole" % s: pole, "arm%s_hand" % s: hand,
            "arm%s_palm" % s: palm, "arm%s_flex" % s: flex}


def hand_on_hip(ctx, fig, s):
    """Knuckles on the hip, elbow out: the wrist at the waist side, a little forward."""
    out = fig.out(s)
    half = ctx.g_hip_half

    def wrist(f):
        c = f.hip_mid()
        return c + f.U * 0.16 + f.F * 0.02 + out * (half + 0.05)

    return arm_ch(s, wrist, -fig.F * 0.3 + out * 1.0 - fig.U * 0.2,
                  -fig.U * 0.75 - fig.F * 0.35 - out * 0.25, -out, 30.0)


def hand_on_thigh(ctx, fig, s, frac=0.55, lift=0.12, fwd=-0.07):
    """A6's seated hands, resting on the lap, fingers towards the knee."""
    out = fig.out(s)

    def rest_on(f):
        th = f.pb["thigh." + s]
        return th.head.lerp(th.tail, frac) + f.U * lift

    def wrist(f):
        return rest_on(f) + f.F * fwd + f.U * 0.03 + out * 0.03

    def hand(f):
        return (rest_on(f) + f.F * 0.20 - f.U * 0.03 - out * 0.02) - f.pb["hand." + s].head

    return arm_ch(s, wrist, out * 0.9 - fig.F * 0.5, hand, -fig.U)


# ------------------------------------------------------------------------------ sitting


def sit_geometry(ctx, fig, hz):
    """Legs of the seated pose (A6's Sit): thighs 4 degrees below level and a little apart,
    shins down to the floor, or, when the seat is higher than her lower leg, 5 degrees forward
    with the shoe tipped toe-down until the toe touches (heels off the floor)."""
    U, F = fig.U, fig.F
    slope = math.radians(4.0)
    geo = dict(hz=hz, ankle={}, pole={}, pitch={}, shin={})
    for s in "LR":
        out = fig.out(s)
        hip = fig.V(-SIT_BACK, 0.0, hz) + out * abs(fig.hip_mid0.dot(fig.L) - fig.pb["thigh." + s].bone.head_local.dot(fig.L))
        d1 = (F * math.cos(slope) - U * math.sin(slope) + out * 0.07).normalized()
        knee = hip + d1 * fig.len["thigh." + s]
        b = fig.len["shin." + s]
        h = knee.dot(U) - fig.ankle0[s].dot(U)
        psi = 0.0
        if h < b:
            phi = math.acos(h / b)
        else:
            phi = math.radians(5.0)
        d2 = -U * math.cos(phi) + F * math.sin(phi)
        ankle = knee + d2 * b
        if h >= b:
            tip = fig.arm.data.bones["toe." + s].tail_local - fig.ankle0[s]
            for i in range(0, 601):
                a = math.radians(i * 0.1)
                r = Matrix.Rotation(-a, 3, fig.R) @ tip
                if ankle.dot(U) + r.dot(U) <= 0.0:
                    psi = a
                    break
        geo["ankle"][s] = ankle
        geo["pole"][s] = knee - hip
        geo["pitch"][s] = -math.degrees(psi)
        geo["shin"][s] = math.degrees(phi)
    return geo


def sit_legs(ctx, fig):
    geo = ctx.sit
    P = {}
    for s in "LR":
        P["leg%s_ankle" % s] = geo["ankle"][s].copy()
        P["leg%s_pole" % s] = geo["pole"][s].copy()
        P["leg%s_pitch" % s] = geo["pitch"][s]
        P["leg%s_yaw" % s] = 0.0
    return P


def sit_params(ctx, fig, t):
    """Sit_Idle at phase t: upright, pelvis rolled back, hands on the thighs, one breath and a
    slow look to her left (A6's Sit, on hinge-aligned legs)."""
    breath = 0.5 - 0.5 * math.cos(math.tau * t)
    look = smoothstep(0.15, 0.35, t) * (1.0 - smoothstep(0.65, 0.85, t))
    P = dict(hip=fig.V(-SIT_BACK, 0.0, ctx.sit["hz"]), pel_yaw=0.0, pel_side=0.0, pel_flex=-14.0,
             s1=7.0 - 1.0 * breath, s2=3.0 - 1.5 * breath, s3=2.0 - 1.0 * breath,
             s_yaw=0.0, s_side=0.0, neck=5.0, head=1.0 + 3.0 * look, head_yaw=22.0 * look,
             head_roll=0.0, shrugL=1.2 * breath, shrugR=1.2 * breath, clavfL=0.0, clavfR=0.0)
    P.update(sit_legs(ctx, fig))
    for s in "LR":
        P.update(hand_on_thigh(ctx, fig, s))
    return P


def fit_sit(ctx, fig, body, log):
    """Lower the seated hips until her lowest body point over the seat sits at 0.45 m."""
    k = 1.0 / GRANDMA_SCALE
    contact = (SEAT_M - SINK_M) * k
    edge = -SIT_BACK + SEAT_EDGE_FROM_HIPS
    depth, half = SEAT_DEPTH_M * k, SEAT_HALF_M * k
    dom = dominant(body)
    seat_groups = ("thigh.L", "thigh.R", "pelvis.L", "pelvis.R", "spine", "spine.001")
    ids = [i for i in range(BODY_VERTS) if dom[i] in seat_groups]
    hz = 0.90
    for it in range(12):
        ctx.sit = sit_geometry(ctx, fig, hz)
        fig.apply(sit_params(ctx, fig, 0.0))
        co = evaluated_coords(body, fig.arm)
        over = []
        for i in ids:
            f, l, u = fig.comp(co[i])
            if edge - depth <= f <= edge and abs(l) < half:
                over.append(u)
        err = min(over) - contact
        log("  sit fit {}: hips {:.4f} BU, lowest body point over the seat {:.4f}, target {:.4f}, err {:+.4f}".format(
            it, hz, min(over), contact, err))
        if abs(err) < 0.0015:
            break
        hz -= err
    ctx.sit = sit_geometry(ctx, fig, hz)
    # The seat as the placed clips see it: every sit clip is moved SIT_BACK forward at bake
    # time (decision 3), so the seated hips are over the root and the seat edge ahead of it.
    ctx.seat = dict(top=SEAT_M * k, contact=contact, edge=edge + SIT_BACK, back=edge - depth + SIT_BACK,
                    half=half)
    return hz


# ------------------------------------------------------------------------------ kneeling

# The hearth (decision 4). GRANDMA's Spot_LightFire stands her root 0.61 m in front of the front
# edge of the hearth slab (A6: "root 0.6 m from the hearth"). PKX_Fireplace.fbx, measured at the
# kit's scale 1.5 with rays: the slab is 0.137 m tall, the logs' front face is 0.276 m behind the
# slab's edge and their top 0.539 m above the floor, the jambs are 0.44 m behind the edge, the
# mantel starts 1.61 m up. The model's origin is 0.722 m behind the slab's edge.
HEARTH_M = 0.61
SLAB_TOP_M = 0.137
LOGS_BACK_M = 0.276
LOGS_TOP_M = 0.539
JAMBS_BACK_M = 0.44
FIREPLACE_ORIGIN_M = 0.722

KNEEL_THETA = 10.0     # thighs this far forward of vertical: almost upright, to lean and reach
KNEE_F = 0.15          # knees on the floor this far ahead of the root (BU): 0.09 m
KNEE_L = 0.20
FEET_L = 0.21
LEFT_STEP = 0.45       # the left foot's step towards the hearth in the half kneel (BU): 0.28 m


def hearth_zones():
    """The fireplace in her units (BU ahead of the root, BU up): what a body must stay out of."""
    k = 1.0 / GRANDMA_SCALE
    return dict(edge=HEARTH_M * k, slab=SLAB_TOP_M * k, logs=(HEARTH_M + LOGS_BACK_M) * k,
                logs_top=LOGS_TOP_M * k, jambs=(HEARTH_M + JAMBS_BACK_M) * k)


def in_hearth(hz, f, u):
    """Depth (BU) of a point inside the slab, the logs or behind the jambs; 0 when outside."""
    d = 0.0
    if f > hz["edge"] and u < hz["slab"]:
        d = max(d, min(f - hz["edge"], hz["slab"] - u))
    if f > hz["logs"] and u < hz["logs_top"]:
        d = max(d, min(f - hz["logs"], hz["logs_top"] - u))
    if f > hz["jambs"]:
        d = max(d, f - hz["jambs"])
    return d


def ankle_for_toe(fig, s, toe, pitch, yaw=0.0):
    """The ankle that puts shoe `s`'s tip on `toe` at this foot angle (toe_of, inverted)."""
    return toe - fig.foot_rot(pitch, yaw) @ (fig.tip0[s] - fig.ankle0[s])


def kneel_geometry(ctx, fig, kh, beta, theta=KNEEL_THETA, knee_f=KNEE_F):
    """Both knees on the floor at height kh (the knee joint centre), shins rising `beta`
    degrees towards the heels, toes tucked (the shoe keeps its rest angle to the shin, so the
    Humanoid foot muscles stay near zero), thighs `theta` forward of vertical."""
    U, F, L = fig.U, fig.F, fig.L
    a = fig.len["thigh.L"]
    b = fig.len["shin.L"]
    hip_l = abs(fig.arm.data.bones["thigh.L"].head_local.dot(L))
    df = a * math.sin(math.radians(theta))
    du = math.sqrt(max(1e-6, a * a - df * df - (KNEE_L - hip_l) ** 2))
    geo = dict(kh=kh, beta=beta, theta=theta, hip=fig.V(knee_f - df, 0.0, kh + du),
               knee={}, ankle={}, pitch=-(90.0 + beta))
    for s, sg in (("L", 1.0), ("R", -1.0)):
        knee = fig.V(knee_f, sg * KNEE_L, kh)
        d2 = fig.V(-math.cos(math.radians(beta)), sg * (FEET_L - KNEE_L) / b, math.sin(math.radians(beta))).normalized()
        geo["knee"][s] = knee
        geo["ankle"][s] = knee + d2 * b
    return geo


def kneel_legs(ctx, fig, geo=None):
    geo = geo or ctx.kneel
    P = {}
    for s in "LR":
        P["leg%s_ankle" % s] = geo["ankle"][s].copy()
        P["leg%s_pole" % s] = fig.F.copy()
        P["leg%s_pitch" % s] = geo["pitch"]
        P["leg%s_yaw" % s] = 0.0
    return P


def fit_kneel(ctx, fig, body, log):
    """Knees and toes on the floor, measured on her body: the knee joint height until the
    lowest leg vertex touches, then the shin angle until the lowest shoe vertex touches."""
    dom = dominant(body)
    legs = [i for i in range(BODY_VERTS) if dom[i].startswith(("thigh", "shin"))]
    shoes = shoe_verts(body, "L") + shoe_verts(body, "R")
    def lows(kh, beta):
        geo = kneel_geometry(ctx, fig, kh, beta)
        P = g_stand(ctx, fig)
        P["hip"] = geo["hip"]
        P.update(kneel_legs(ctx, fig, geo))
        fig.apply(P)
        co = evaluated_coords(body, fig.arm)
        return min(fig.comp(co[i])[2] for i in legs), min(fig.comp(co[i])[2] for i in shoes)

    kh, beta = 0.08, 15.0
    for it in range(8):
        low_leg, low_shoe = lows(kh, beta)
        log("  kneel fit {}: knee joint {:.4f} BU, shin rise {:.2f} deg, lowest knee point {:+.4f}, "
            "lowest shoe point {:+.4f}".format(it, kh, beta, low_leg, low_shoe))
        if abs(low_leg - 0.004) < 0.0015 and abs(low_shoe - 0.002) < 0.0015:
            break
        kh -= low_leg - 0.004                         # the knee first, then the shin at that knee
        s0 = lows(kh, beta)[1]
        s1 = lows(kh, beta + 1.0)[1]
        if abs(s1 - s0) > 1e-5:
            beta += max(-8.0, min(8.0, (0.002 - s0) / (s1 - s0)))
    ctx.kneel = kneel_geometry(ctx, fig, kh, beta)
    return kh, beta


# ------------------------------------------------------------------------------- clips
# Every builder returns dict(frames, loop, author, pose(t) -> channels, keys (render frames),
# checks, props). t = (frame - 1) / frames; a loop's last frame is its first.


def keyed(keys, frames, loop):
    return lambda t: sample(keys, t * frames, loop)


def clip_crew_idle(ctx, fig):
    """Crew_Idle, 3 s: two breaths, the weight onto the left leg and back, a slow look round,
    arms hanging clear of the torso. The crew body's first idle: AC_Crew only had Carry_Idle."""
    def pose(t):
        breath = 0.5 - 0.5 * math.cos(2.0 * math.tau * t)
        shift = 0.5 - 0.5 * math.cos(math.tau * t)
        P = c_stand(ctx, fig)
        P["hip"] = fig.hip_mid0 + fig.U * (-0.012 - 0.006 * shift) + fig.L * (0.028 * shift)
        P["pel_side"] = -2.2 * shift
        P["s_side"] = 2.6 * shift
        P["s2"] = 1.0 - 1.4 * breath
        P["s3"] = 0.5 - 0.8 * breath
        P["head_yaw"] = 9.0 * math.sin(math.tau * t)
        P["head"] = -2.0 + 1.5 * math.sin(2.0 * math.tau * t)
        P["shrugL"] = P["shrugR"] = 1.5 * breath
        for s in "LR":
            P.update(hang(s, ctx.crew_half, gap=0.07, swing=2.0 + 1.0 * breath, bend=10.0 + 3.0 * breath))
        return P
    return dict(frames=72, loop=True, author="crew", pose=pose, keys=[1, 19, 37, 55],
                checks=["planted"])


def clip_crew_crouch_idle(ctx, fig):
    """Crew_CrouchIdle, 2 s: hips 0.35 BU down and back, torso 20 degrees forward, knees apart,
    one breath, a small sway."""
    def pose(t):
        breath = 0.5 - 0.5 * math.cos(math.tau * t)
        sway = math.sin(math.tau * t)
        P = c_stand(ctx, fig)
        P.update(planted(fig, df=(0.02, 0.02), dl=(0.05, -0.05), yaw=(10.0, -10.0)))
        for s in "LR":
            P["leg%s_pole" % s] = fig.F + fig.out(s) * 0.5
        P["hip"] = fig.hip_mid0 + fig.V(-0.08, 0.012 * sway, -0.35 + 0.006 * breath)
        P["pel_flex"] = 13.0
        P["s1"], P["s2"], P["s3"] = 4.0, 2.0 + 1.0 * breath, 1.0 - 1.0 * breath
        P["neck"], P["head"] = -8.0, -11.0
        P["head_yaw"] = 6.0 * sway
        P["shrugL"] = P["shrugR"] = 1.0 * breath
        for s in "LR":
            out = fig.out(s)
            P.update(arm_ch(s, fig.V(0.30, 0.0, 1.16) + out * 0.25 + fig.U * 0.01 * breath,
                            -fig.F * 0.4 + out * 0.8 - fig.U * 0.5,
                            fig.F * 0.35 - fig.U * 0.9 - out * 0.15, -out))
        return P
    return dict(frames=48, loop=True, author="crew", pose=pose, keys=[1, 13, 25, 37],
                checks=["planted"])


def gait_clip(params, author, half_key):
    def build(ctx, fig):
        g = Gait(fig, dict(params), getattr(ctx, half_key))
        return dict(frames=params["frames"], loop=True, author=author, gait=g, pose=None,
                    keys=[1 + params["frames"] * i // 8 for i in range(8)], checks=["gait"])
    return build


def clip_sit_idle(ctx, fig):
    return dict(frames=96, loop=True, author="grandma", pose=lambda t: sit_params(ctx, fig, t),
                keys=[1, 49], checks=["planted", "seat"], props="chair", shift=SIT_BACK)


def clip_sit_down(ctx, fig):
    """Sit_Down, 1.6 s: from her standing pose at the root, hips back and down with the torso
    forward and the hands reaching back for the armrests, contact, then Sit_Idle's first frame."""
    N = 38
    stand = g_stand(ctx, fig)
    for s in "LR":
        stand.update(hang(s, ctx.g_half, gap=0.07, swing=4.0, bend=16.0))
    P11 = dict(stand)
    P11.update(hip=fig.V(-0.22, 0.0, 1.12), pel_flex=18.0, s1=10.0, s2=6.0, s3=2.0, neck=-6.0, head=-14.0)
    P23 = dict(stand)
    P23.update(hip=fig.V(-SIT_BACK, 0.0, ctx.sit["hz"] + 0.012), pel_flex=4.0, s1=8.0, s2=4.0, s3=2.0,
               neck=0.0, head=-8.0)
    for P in (P11, P23):
        for s in "LR":
            out = fig.out(s)
            P.update(arm_ch(s, fig.V(-0.30 if P is P11 else -0.34, 0.0, 1.13) + out * 0.43,
                            -fig.F * 0.8 + out * 0.6 - fig.U * 0.1,
                            fig.F * 0.6 - fig.U * 0.8 - out * 0.1, -fig.U))
    P38 = sit_params(ctx, fig, 0.0)
    for P in (stand, P11, P23):
        for s in "LR":
            P["leg%s_pole" % s] = fig.F + fig.out(s) * 0.07
    keys = [(0, stand), (11, P11), (23, P23), (38, P38)]
    return dict(frames=N, loop=False, author="grandma", pose=keyed(keys, N, False),
                keys=[1, 7, 12, 18, 24, 31, 39], checks=[], props="chair", shift=SIT_BACK)


def clip_stand_up(ctx, fig):
    """Stand_Up, 2 s: Sit_Down reversed, plus the old-age effort: hands pushing on the thighs,
    a stalled push with a wobble at the hardest point, then up."""
    N = 48
    P0 = sit_params(ctx, fig, 0.0)
    P10 = dict(P0)
    P10.update(hip=fig.V(-SIT_BACK + 0.04, 0.0, ctx.sit["hz"]), pel_flex=6.0, s1=12.0, s2=7.0, s3=3.0,
               neck=-2.0, head=-10.0)
    for s in "LR":
        P10.update(hand_on_thigh(ctx, fig, s, frac=0.85, lift=0.10, fwd=-0.05))
    stand_feet = planted(fig)
    for s in "LR":
        stand_feet["leg%s_pole" % s] = fig.F + fig.out(s) * 0.07
    P20 = dict(P10)
    P20.update(stand_feet)
    P20.update(hip=fig.V(-0.40, 0.0, 0.99), pel_flex=20.0, s1=12.0, s2=7.0, s3=3.0, neck=-6.0, head=-12.0)
    P27 = dict(P20)
    P27.update(hip=fig.V(-0.39, 0.0, 0.975), s1=13.0)          # the stall
    P34 = dict(P20)
    P34.update(hip=fig.V(-0.18, 0.0, 1.18), pel_flex=15.0, s1=8.0, s2=5.0, s3=3.0, neck=0.0, head=-12.0)
    for s in "LR":
        P34.update(hand_on_thigh(ctx, fig, s, frac=0.75, lift=0.14, fwd=-0.02))
    P48 = g_stand(ctx, fig)
    for s in "LR":
        P48.update(hang(s, ctx.g_half, gap=0.07, swing=4.0, bend=16.0))
        P48["leg%s_pole" % s] = fig.F + fig.out(s) * 0.07
    P41 = dict(P48)
    P41.update(hip=fig.V(-0.04, 0.0, 1.31), pel_flex=4.0, s1=5.0, s2=5.0, s3=3.0)
    keys = [(0, P0), (10, P10), (20, P20), (27, P27), (34, P34), (41, P41), (48, P48)]
    return dict(frames=N, loop=False, author="grandma", pose=keyed(keys, N, False),
                keys=[1, 11, 21, 28, 35, 42, 49], checks=[], props="chair", shift=SIT_BACK)


def clip_sit_read(ctx, fig):
    """Sit_Read, 6 s: Sit_Idle's legs, a book held in both hands at lower chest height, head
    down 25 degrees, eyes moving along the lines, a page turned by the right hand at f96-f114."""
    N = 144
    book = fig.V(-0.10, 0.0, 1.42)

    def pose(t):
        f = t * N
        breath = 0.5 - 0.5 * math.cos(2.0 * math.tau * t)
        P = sit_params(ctx, fig, 0.0)
        P.update(s1=10.0 - 1.0 * breath, s2=6.0 - 1.0 * breath, s3=4.0 - 0.5 * breath,
                 neck=12.0, head=10.0 + 1.5 * math.sin(3.0 * math.tau * t),
                 head_yaw=4.0 * math.sin(6.0 * math.tau * t), shrugL=1.0 * breath, shrugR=1.0 * breath)
        turn = envelope(f, 96.0, 103.0, 106.0, 114.0)
        for s in "LR":
            out = fig.out(s)
            w = book + fig.V(-0.06, 0.0, -0.10) + out * 0.17
            hd = fig.U * 0.75 + fig.F * 0.45 - out * 0.45
            palm = -out * 0.8 + fig.U * 0.4 - fig.F * 0.2
            if s == "R" and turn > 0.0:
                w = w.lerp(book + fig.V(-0.02, 0.02, 0.06), turn)
                hd = hd.lerp(fig.U * 0.3 + fig.F * 0.6 + fig.L * 0.7, turn)
                palm = palm.lerp(-fig.F * 0.7 - fig.U * 0.3, turn)
            P.update(arm_ch(s, w, -fig.F * 0.6 + out * 0.5 - fig.U * 0.7, hd, palm, 10.0))
        return P
    return dict(frames=N, loop=True, author="grandma", pose=pose, keys=[1, 49, 97, 105, 113],
                checks=["planted", "seat"], props="chair", shift=SIT_BACK)


def clip_sit_drink(ctx, fig):
    """Sit_Drink, 4 s loop: cup on the right thigh, up to the mouth, a sip with the head back
    10 degrees, back down. The left hand stays on its thigh."""
    N = 96
    base = sit_params(ctx, fig, 0.0)

    def cup_low(f):
        th = f.pb["thigh.R"]
        return th.head.lerp(th.tail, 0.6) + f.U * 0.17 + f.R * 0.01

    low = dict(base)
    low.update(drink_arm(fig, cup_low, "low"))
    up = dict(base)
    up.update(neck=4.0, head=-2.0, s1=8.0)
    up.update(drink_arm(fig, cup_at_mouth, "hold"))
    sip = dict(up)
    sip.update(neck=2.0, head=-12.0)
    sip.update(drink_arm(fig, cup_at_mouth, "tip"))
    keys = [(0, low), (10, low), (24, up), (32, sip), (60, sip), (70, up), (84, low), (96, low)]
    return dict(frames=N, loop=True, author="grandma", pose=keyed(keys, N, True),
                keys=[1, 25, 45, 85], checks=["planted", "seat"], props="chair", shift=SIT_BACK)


def kneel_params(ctx, fig, t=0.0):
    """Kneel_LightFire's base: nearly upright on the knees, the pelvis tipped forward and the
    torso bent over the hearth slab."""
    breath = 0.5 - 0.5 * math.cos(2.0 * math.tau * t)
    P = g_stand(ctx, fig)
    P.update(hip=ctx.kneel["hip"].copy(), pel_flex=26.0, s1=10.0, s2=8.0 - 1.0 * breath, s3=6.0 - 1.0 * breath,
             neck=-16.0, head=-14.0, shrugL=1.0 * breath, shrugR=1.0 * breath)
    P.update(kneel_legs(ctx, fig))
    return P


def fire_arms(ctx, fig, f):
    """Left hand holds the matchbox in front of the chest, the right strikes, reaches over the slab
    to the logs, holds the lit match there, shakes it out, comes back to the box. f is the frame
    0..120. The box follows the posed chest, so it stays in front of her while she leans. The
    reach is aimed at the logs of decision 4: the fingertips stop a few centimetres short of their
    front face with the hand pointing forward and down, so the match touches them."""
    hz = hearth_zones()
    hd_reach = (fig.F * 0.8 - fig.U * 0.6).normalized()
    tip = fig.V(hz["logs"] - 0.07, -0.12, 0.72)
    hearth = tip - hd_reach * fig.len["hand.R"]          # the wrist for that fingertip
    w = piecewise(f / 120.0, [(0.0, 0.0), (12 / 120.0, 0.0), (20 / 120.0, 1.0), (1.0, 1.0)])
    reach = envelope(f, 20.0, 30.0, 92.0, 104.0)
    back = smoothstep(104.0, 118.0, f)
    shake = envelope(f, 92.0, 95.0, 101.0, 104.0) * math.sin(math.tau * (f - 92.0) / 4.0)

    def box(fg):
        return fg.pb["spine.002"].head.lerp(fg.pb["spine.002"].tail, 0.5) + fg.F * 0.50 + fg.L * 0.04

    def left_wrist(fg):
        # At the box, or, while the right hand is in the hearth, lowered in front of her knees:
        # held at the chest through the lean, the left elbow would fold past 150 degrees.
        low = fg.pb["upper_arm.L"].head + (fg.F * 0.45 - fg.U * 0.85 + fg.L * 0.05).normalized() * 0.55
        return (box(fg) + fg.V(-0.10, 0.10, -0.04)).lerp(low, reach)

    def right_wrist(fg):
        b = box(fg)
        strike0 = b + fg.V(-0.02, -0.10, 0.06)
        strike1 = b + fg.V(0.10, -0.32, 0.10)
        pos = strike0.lerp(strike1, w).lerp(hearth, reach)
        if f > 104.0:
            pos = pos.lerp(strike0, back)
        return pos + fg.R * 0.05 * shake + fg.U * 0.01 * math.sin(math.tau * f / 17.0) * reach

    P = {}
    P.update(arm_ch("L", left_wrist, -fig.F * 0.4 + fig.L * 0.8 - fig.U * 0.4,
                    (fig.F * 0.6 - fig.L * 0.8 + fig.U * 0.1).lerp(fig.F * 0.35 - fig.U * 0.9 - fig.L * 0.2, reach),
                    fig.U.lerp(-fig.L, reach), 20.0))
    hd = (fig.F * 0.7 + fig.L * 0.7).lerp(hd_reach, reach)
    P.update(arm_ch("R", right_wrist, -fig.F * 0.3 + fig.R * 0.8 - fig.U * 0.5, hd, -fig.L * 0.2 + fig.U * 0.1,
                    15.0 * (1.0 - reach)))
    return P, reach


def clip_kneel_fire(ctx, fig):
    """Kneel_LightFire, 5 s loop: strike (f12-f20), the lit match forward and down onto the
    logs (f24-f84), shaken out (f96-f108), back to the box. Matchbox left socket, match right."""
    N = 120

    def pose(t):
        f = t * N
        P = kneel_params(ctx, fig, t)
        arms, reach = fire_arms(ctx, fig, f)
        P.update(arms)
        P["head_yaw"] = -6.0 * (1.0 - reach)
        P["head"] = -14.0 + 4.0 * reach
        P["pel_flex"] = 26.0 + 10.0 * reach
        P["s1"] = 10.0 + 6.0 * reach
        P["s2"] = P["s2"] + 4.0 * reach
        P["clavfR"] = 10.0 * reach
        return P
    return dict(frames=N, loop=True, author="grandma", pose=pose, keys=[1, 13, 21, 49, 97, 111],
                checks=["planted", "floor_skirt", "hearth"], props="fire")


def clip_kneel_down(ctx, fig):
    """Kneel_Down, 2 s: the left foot steps towards the hearth (f8-f14) and the weight goes onto
    it, the right foot is lifted back (f20) and set down on its toes, the right knee goes down
    behind the left foot (half kneel, f26), the left knee comes down beside it (f32-f37), then she
    settles (f48 = Kneel_LightFire f1). The knees end KNEE_F ahead of the root, well clear of the
    hearth slab, and the left toe stays off the slab at the longest step. Feet are keyed by their
    shoe tips, so a foot rolls on its toe instead of dipping through the floor."""
    N = 48
    stand = g_stand(ctx, fig)
    for s in "LR":
        stand.update(hang(s, ctx.g_half, gap=0.07, swing=4.0, bend=16.0))
        stand["leg%s_pole" % s] = fig.F + fig.out(s) * 0.07
    upright = kneel_at(ctx, fig, 4.0)
    lunge_hip = fig.V(KNEE_F - 0.05, 0.0, upright["hip"].dot(fig.U))
    tipR = fig.tip0["R"].copy()

    P8 = dict(stand)                                   # the left foot lifting forward
    P8.update(hip=fig.hip_mid0 + fig.V(0.02, -0.035, -0.05), pel_flex=4.0, s1=4.0, head=-12.0,
              legL_ankle=rest_ankle(fig, "L", LEFT_STEP * 0.4) + fig.U * 0.09, legL_pitch=6.0,
              legL_pole=fig.F + fig.U * 0.2)
    P14 = dict(P8)                                     # left foot down, the weight onto it, right heel up
    P14.update(hip=fig.V(0.20, 0.01, 1.22), pel_flex=8.0, s1=6.0, s2=3.0, head=-12.0,
               legL_ankle=rest_ankle(fig, "L", LEFT_STEP), legL_pitch=0.0,
               legR_ankle=ankle_for_toe(fig, "R", tipR, -30.0), legR_pitch=-30.0, legR_pole=fig.F.copy())
    P20 = dict(P14)                                    # the right foot lifted back, toe down
    P20.update(hip=fig.V(0.28, 0.03, 1.02), pel_flex=12.0, s1=8.0, s2=4.0, neck=0.0, head=-12.0,
               legR_ankle=fig.V(KNEE_F - 0.45, -FEET_L, 0.30), legR_pitch=-80.0)
    P20.update(knee_on_thigh(fig, "L", "L", 0.9, 0.12))
    P20.update(knee_on_thigh(fig, "R", "L", 0.8, 0.14))
    P26 = dict(P20)                                    # the right knee down: half kneel
    P26.update(hip=lunge_hip.copy(), pel_flex=10.0, s1=8.0, s2=5.0, neck=-2.0, head=-10.0,
               legR_ankle=upright["legR_ankle"].copy(), legR_pitch=upright["legR_pitch"])
    P32 = dict(P26)                                    # the left knee low, the foot swinging back
    P32.update(hip=lunge_hip + fig.U * 0.01, legL_ankle=fig.V(KNEE_F - 0.40, FEET_L, 0.30),
               legL_pitch=-88.0, legL_pole=fig.F.copy())
    P32.update(knee_on_thigh(fig, "L", "L", 0.8, 0.14))
    P32.update(knee_on_thigh(fig, "R", "R", 0.8, 0.14))
    P37 = dict(P32)                                    # both knees down, upright
    P37.update(upright)
    P37.update(pel_flex=6.0, s1=6.0, s2=4.0, s3=3.0, neck=0.0, head=-10.0)
    P37.update(knee_on_thigh(fig, "L", "L", 0.75, 0.12))
    P37.update(knee_on_thigh(fig, "R", "R", 0.75, 0.12))
    P42 = dict(P37)                                    # settling
    P42.update(kneel_at(ctx, fig, 7.0))
    P42.update(pel_flex=14.0, s1=8.0, s2=6.0)
    P48 = fire_start(ctx, fig)
    keys = [(0, stand), (8, P8), (14, P14), (20, P20), (26, P26), (32, P32), (37, P37), (42, P42), (48, P48)]
    keys = [(f, with_toes(fig, P)) for f, P in keys]
    return dict(frames=N, loop=False, author="grandma", pose=keyed(keys, N, False),
                keys=[1, 9, 15, 21, 27, 33, 38, 49], checks=["floor_skirt", "hearth"], props="fire")


def kneel_at(ctx, fig, theta):
    """The hips and legs of the kneel at thigh angle theta (4 is upright, 35 sits back)."""
    geo = kneel_geometry(ctx, fig, ctx.kneel["kh"], ctx.kneel["beta"], theta=theta)
    P = {"hip": geo["hip"].copy()}
    P.update(kneel_legs(ctx, fig, geo))
    return P


def with_toes(fig, P):
    """Key both feet by their shoe tips, computed from the ankle and the foot angle: between
    keys the tip is what blends, so a foot rolls on its toe instead of dipping through the floor."""
    Q = dict(P)
    for s in "LR":
        Q["leg%s_toe" % s] = fig.toe_of(s, resolve(fig, P["leg%s_ankle" % s]), P["leg%s_pitch" % s],
                                        P.get("leg%s_yaw" % s, 0.0))
    return Q


def knee_on_thigh(fig, s, thigh, frac, lift):
    """Hand `s` pressed on the `thigh` side's thigh, near the knee: the push of getting up."""
    out = fig.out(s)

    def w(f):
        th = f.pb["thigh." + thigh]
        return th.head.lerp(th.tail, frac) + f.U * lift - f.F * 0.06 + f.out(thigh) * 0.02
    return arm_ch(s, w, out * 0.9 - fig.F * 0.4 - fig.U * 0.2, fig.F * 0.8 - fig.U * 0.6, -fig.U, 10.0)


def fire_start(ctx, fig):
    """Kneel_LightFire's first frame, which ends Kneel_Down and starts Kneel_Up."""
    return clip_kneel_fire(ctx, fig)["pose"](0.0)


def clip_kneel_up(ctx, fig):
    """Kneel_Up, 2.5 s: upright on the knees with the hands on the thighs (f10), the left foot
    brought forward into a half kneel (f26), a push on the left knee that stalls halfway (f33-f41),
    the right foot brought under her (f45-f48), the left foot stepped back (f54), up (f60: her
    standing pose on the root, so the Locomotion idle follows without a jump)."""
    N = 60
    upright = kneel_at(ctx, fig, 4.0)
    lunge_hip = fig.V(KNEE_F - 0.05, 0.0, upright["hip"].dot(fig.U))

    P0 = fire_start(ctx, fig)
    P4 = dict(P0)
    P4.update(kneel_at(ctx, fig, 8.0))
    P4.update(pel_flex=11.0, s1=8.0, s2=6.0, clavfR=0.0)
    P4.update(knee_on_thigh(fig, "L", "L", 0.75, 0.12))
    P4.update(knee_on_thigh(fig, "R", "R", 0.75, 0.12))
    P7 = dict(P4)
    P7.update(kneel_at(ctx, fig, 6.0))
    P7.update(pel_flex=8.0, s1=7.0, s2=5.0)
    P10 = dict(P7)                                     # upright on the knees
    P10.update(upright)
    P10.update(pel_flex=6.0, s1=6.0, s2=4.0, s3=3.0, neck=0.0, head=-10.0)
    P15 = dict(P10)                                    # the left knee lifting, the foot behind
    P15.update(legL_ankle=fig.V(KNEE_F - 0.40, FEET_L, 0.30), legL_pitch=-88.0, hip=lunge_hip + fig.U * 0.01)
    P21 = dict(P15)                                    # the foot coming through, low
    P21.update(legL_ankle=fig.V(KNEE_F + 0.20, FEET_L + 0.04, 0.26), legL_pitch=-20.0,
               legL_pole=fig.F + fig.L * 0.2)
    P26 = dict(P21)                                    # the left foot planted forward: half kneel
    P26.update(legL_ankle=rest_ankle(fig, "L", LEFT_STEP), legL_pitch=0.0, legL_pole=fig.F + fig.U * 0.2,
               hip=lunge_hip.copy(), pel_flex=10.0, s1=10.0, s2=6.0)
    P26.update(knee_on_thigh(fig, "L", "L", 0.92, 0.10))
    P26.update(knee_on_thigh(fig, "R", "L", 0.80, 0.14))
    P33 = dict(P26)                                    # the push: the right knee leaves the floor
    P33.update(hip=fig.V(0.26, 0.0, 0.93), pel_flex=18.0, s1=12.0,
               legR_ankle=upright["legR_ankle"] + fig.V(0.10, 0.0, 0.14), legR_pitch=-75.0, legR_pole=fig.F.copy())
    P41 = dict(P33)
    P41.update(hip=fig.V(0.28, 0.0, 0.95), s1=13.0)                   # the stall
    P45 = dict(P41)                                    # the right foot swinging under her
    P45.update(hip=fig.V(0.28, 0.02, 1.08), pel_flex=14.0, s1=10.0, legR_pitch=-10.0,
               legR_ankle=rest_ankle(fig, "R", -0.05) + fig.U * 0.12)
    P48 = dict(P45)                                    # the right foot down on the root
    P48.update(hip=fig.V(0.22, -0.02, 1.20), pel_flex=10.0, s1=8.0, legR_pitch=0.0,
               legR_ankle=rest_ankle(fig, "R"), legR_pole=fig.F + fig.out("R") * 0.07)
    stand = g_stand(ctx, fig)
    for s in "LR":
        stand.update(hang(s, ctx.g_half, gap=0.07, swing=4.0, bend=16.0))
        stand["leg%s_pole" % s] = fig.F + fig.out(s) * 0.07
    P54 = dict(stand)                                  # the left foot stepping back to the root
    P54.update(hip=fig.hip_mid0 + fig.V(0.03, -0.035, -0.04), pel_flex=5.0, s1=6.0,
               legL_ankle=rest_ankle(fig, "L", LEFT_STEP * 0.35) + fig.U * 0.08, legL_pitch=4.0,
               legL_pole=fig.F + fig.U * 0.2)
    P60 = dict(stand)
    keys = [(0, P0), (4, P4), (7, P7), (10, P10), (15, P15), (21, P21), (26, P26), (33, P33), (41, P41),
            (45, P45), (48, P48), (54, P54), (60, P60)]
    keys = [(f, with_toes(fig, P)) for f, P in keys]
    return dict(frames=N, loop=False, author="grandma", pose=keyed(keys, N, False),
                keys=[1, 11, 22, 27, 34, 42, 49, 55, 61], checks=["floor_skirt", "hearth"], props="fire")


# The stove, as GRANDMA's Spot_Cook_Stove sees it: her root 0.54 m in front of SM_Stove_Old's
# front face (stove front z 5.14, spot z 4.60). Measured on the model with rays: the cooktop is
# 0.925 m up, the front burners 0.94 m up and 0.20 m behind the front face, 0.15 m either side of
# its centre; the model's origin is 0.335 m behind the front face. No pot is modelled.
STOVE_M = 0.54
BURNER_BACK_M = 0.20
BURNER_TOP_M = 0.94
STOVE_ORIGIN_M = 0.335
COOKTOP_M = 0.925


def clip_stand_cook(ctx, fig):
    """Stand_Cook, 4 s loop: leaning over the stove, the right hand stirring a pot on the front
    right burner in a circle, one turn a second, the left hand on her hip, head down. The grip
    circles 0.53 m ahead of the root and 1.12 m up, so a 0.30 m wooden spoon held forward and
    down 45 degrees reaches the bottom of a pot on that burner."""
    N = 96
    k = 1.0 / GRANDMA_SCALE
    pot = fig.V(0.53 * k, -0.16, 1.12 * k)

    def pose(t):
        a = 4.0 * math.tau * t
        breath = 0.5 - 0.5 * math.cos(2.0 * math.tau * t)
        P = g_stand(ctx, fig)
        P.update(planted(fig, df=(0.05, 0.0), dl=(0.02, -0.02)))
        P.update(pel_flex=13.0, s1=9.0, s2=7.0 - 1.0 * breath, s3=4.0, neck=16.0, head=10.0, clavfR=10.0,
                 s_yaw=2.0 * math.sin(a), head_yaw=-6.0 + 2.0 * math.sin(a))
        hd = (fig.F * 0.65 - fig.U * 0.65 + fig.L * 0.30).normalized()
        centre = pot + fig.F * (0.07 * math.cos(a)) + fig.L * (0.07 * math.sin(a))
        P.update(arm_ch("R", centre - hd * 0.14, -fig.F * 0.2 + fig.R * 0.7 - fig.U * 0.8, hd,
                        fig.L * 0.8 - fig.F * 0.2, 25.0))
        P.update(hand_on_hip(ctx, fig, "L"))
        return P
    return dict(frames=N, loop=True, author="grandma", pose=pose, keys=[1, 7, 13, 19], checks=["planted", "stove"],
                props="stove")


def clip_stand_water(ctx, fig):
    """Stand_Water, 4 s loop: the can held forward at waist height, tipped 35 degrees to pour
    (f24-f72), head down towards the plant, the left hand on her belly."""
    N = 96
    level = dict(g_stand(ctx, fig))
    level.update(pel_flex=2.0, s1=4.0, s2=4.0, s3=3.0, neck=14.0, head=6.0)
    level.update(arm_ch("R", fig.V(0.40, -0.22, 1.60), -fig.F * 0.3 + fig.R * 0.8 - fig.U * 0.6,
                        fig.F * 0.95 - fig.U * 0.05, fig.L, 30.0))
    level.update(arm_ch("L", fig.V(0.26, 0.10, 1.52), -fig.F * 0.3 + fig.L * 0.8 - fig.U * 0.6,
                        fig.F * 0.2 - fig.L * 0.9 - fig.U * 0.2, -fig.F, 5.0))
    pour = dict(level)
    pour.update(s1=6.0, neck=16.0, head=8.0)
    pour.update(arm_ch("R", fig.V(0.43, -0.22, 1.64), -fig.F * 0.3 + fig.R * 0.8 - fig.U * 0.6,
                       fig.F * 0.82 - fig.U * 0.57, fig.L, 30.0))
    keys = [(0, level), (24, pour), (72, pour), (84, level), (96, level)]
    return dict(frames=N, loop=True, author="grandma", pose=keyed(keys, N, True),
                keys=[1, 25, 49, 85], checks=["planted"])


def clip_stand_drink(ctx, fig):
    """Stand_Drink, 4 s loop: a cup and saucer at the belly (the saucer stays in the left hand),
    the cup up to the mouth, a sip with the head back 10 degrees, back onto the saucer."""
    N = 96
    base = g_stand(ctx, fig)
    saucer = fig.V(0.30, 0.02, 1.50)
    base.update(arm_ch("L", saucer + fig.V(-0.10, 0.10, -0.02), -fig.F * 0.3 + fig.L * 0.9 - fig.U * 0.5,
                       fig.F * 0.5 - fig.L * 0.85, fig.U, 5.0))

    low = dict(base)
    low.update(drink_arm(fig, saucer + fig.U * 0.06, "low"))
    up = dict(base)
    up.update(neck=6.0, head=-6.0)
    up.update(drink_arm(fig, cup_at_mouth, "hold"))
    sip = dict(up)
    sip.update(neck=4.0, head=-16.0)
    sip.update(drink_arm(fig, cup_at_mouth, "tip"))
    keys = [(0, low), (10, low), (24, up), (32, sip), (60, sip), (70, up), (84, low), (96, low)]
    return dict(frames=N, loop=True, author="grandma", pose=keyed(keys, N, True),
                keys=[1, 25, 45, 85], checks=["planted"])


def cup_at_mouth(f):
    """The cup's centre just below and in front of the lips."""
    return f.mouth() + f.F * 0.09 - f.U * 0.06


def drink_arm(fig, cup, grip):
    """The right hand holding a cup from its right side: palm towards the cup, the hand upright,
    the elbow down in front of the ribs. `low` has the fingers leaning forward (the cup resting),
    `hold` upright at the mouth, `tip` leaning back towards the face (the cup tips to the lips)."""
    hd = {"low": fig.U * 0.55 + fig.F * 0.70 + fig.L * 0.30,
          "hold": fig.U * 0.85 + fig.F * 0.25 + fig.L * 0.30,
          "tip": fig.U * 0.90 - fig.F * 0.30 + fig.L * 0.30}[grip].normalized()

    def wrist(f):
        return resolve(f, cup) + f.R * 0.05 - hd * 0.13
    return arm_ch("R", wrist, -fig.U * 0.9 + fig.F * 0.25 + fig.R * 0.35, hd, fig.L * 0.9 - fig.F * 0.2, 20.0)


def angry_base(ctx, fig):
    P = g_stand(ctx, fig)
    P.update(pel_flex=3.0, s1=3.0, s2=2.0, s3=0.0, neck=-2.0, head=-4.0)
    return P


def clip_shake_fist(ctx, fig):
    """Angry_ShakeFist, 1.5 s loop (upper-body layer): the right fist up by her head and
    forward, shaken +/-8 degrees at 4 Hz, the left hand on her hip, leaning in 8 degrees.
    It starts and ends in the held pose: the 0.25 s cross-fade does the raise."""
    N = 36

    def pose(t):
        P = angry_base(ctx, fig)
        sh = math.sin(6.0 * math.tau * t)
        P.update(head_yaw=3.0 * sh, head_roll=2.0 * sh)
        P.update(arm_ch("R", fig.V(0.40 + 0.04 * sh, -0.30, 2.30 + 0.02 * sh), -fig.F * 0.3 + fig.R * 1.0 - fig.U * 0.6,
                        fig.U * 0.8 + fig.F * 0.55, -fig.F * 0.8 + fig.L * 0.5, 75.0))
        P.update(hand_on_hip(ctx, fig, "L"))
        return P
    return dict(frames=N, loop=True, author="grandma", pose=pose, keys=[1, 4, 7, 19], checks=["planted"], yaw=20.0)


def clip_hands_on_hips(ctx, fig):
    """Angry_HandsOnHips, 2 s loop: both hands on the hips, elbows out, chest out, the head
    shaking +/-12 degrees twice."""
    N = 48

    def pose(t):
        P = angry_base(ctx, fig)
        P.update(pel_flex=1.0, s1=1.0, s2=-3.0, s3=-3.0, clavfL=-4.0, clavfR=-4.0, neck=4.0, head=-6.0,
                 head_yaw=12.0 * math.sin(2.0 * math.tau * t))
        P.update(hand_on_hip(ctx, fig, "L"))
        P.update(hand_on_hip(ctx, fig, "R"))
        return P
    return dict(frames=N, loop=True, author="grandma", pose=pose, keys=[1, 7, 19, 31], checks=["planted"])


def clip_point(ctx, fig):
    """Angry_Point, 1.5 s loop: the right arm straight at shoulder height towards the culprit,
    two jabs. The left hand on her hip. Right-hand IK can aim it at runtime."""
    N = 36

    def pose(t):
        P = angry_base(ctx, fig)
        jab = 0.5 - 0.5 * math.cos(2.0 * math.tau * t)
        P.update(s1=3.0 + 2.0 * jab, head_yaw=-6.0, neck=-4.0, head=-2.0)
        aim = (fig.F * 0.95 + fig.R * 0.25 + fig.U * 0.10).normalized()

        def wrist(f):
            return f.pb["upper_arm.R"].head + aim * (0.64 - 0.06 * jab)
        P.update(arm_ch("R", wrist, -fig.U * 0.8 + fig.R * 0.6, aim + fig.U * 0.05, -fig.U * 0.3 + fig.L * 0.7, 5.0))
        P.update(hand_on_hip(ctx, fig, "L"))
        return P
    return dict(frames=N, loop=True, author="grandma", pose=pose, keys=[1, 10, 19, 28], checks=["planted"], yaw=80.0)


def clip_talk(ctx, fig):
    """Talk, 3 s loop (upper-body layer): forearms forward, palms up, alternating hand beats
    (four a loop), a nod every 0.75 s."""
    N = 72

    def pose(t):
        breath = 0.5 - 0.5 * math.cos(math.tau * t)
        P = g_stand(ctx, fig)
        P.update(s2=4.0 - 1.0 * breath, head=-12.0 + 4.0 * max(0.0, math.sin(4.0 * math.tau * t)) ** 2,
                 head_yaw=5.0 * math.sin(math.tau * t), shrugL=breath, shrugR=breath)
        for s, ph in (("L", 0.0), ("R", 0.25)):
            out = fig.out(s)
            beat = max(0.0, math.sin(2.0 * math.tau * (t - ph))) ** 3
            w = fig.V(0.30 + 0.03 * beat, 0.0, 1.72 + 0.07 * beat) + out * (0.25 + 0.04 * beat)
            P.update(arm_ch(s, w, -fig.F * 0.5 + out * 0.5 - fig.U * 0.8,
                            fig.F * 0.85 + out * 0.2 + fig.U * 0.15 * beat, fig.U * 0.85 - out * 0.4, -10.0 * beat))
        return P
    return dict(frames=N, loop=True, author="grandma", pose=pose, keys=[1, 7, 19, 37], checks=["planted"])


def clip_give_keys(ctx, fig):
    """Give_Keys, 2 s: the right hand out in front, palm up, held (f18-f36), back. The hand-off
    moment is frame index 24 (Unity frame 25), 1.0 s, normalized time 0.5, which is what GRANDMA's
    code assumes."""
    N = 48
    stand = g_stand(ctx, fig)
    for s in "LR":
        stand.update(hang(s, ctx.g_half, gap=0.07, swing=4.0, bend=16.0))
    out_ = dict(stand)
    out_.update(s1=5.0, s2=5.0, neck=6.0, head=-8.0, head_yaw=-4.0, clavfR=6.0)
    out_.update(arm_ch("R", fig.V(0.50, -0.14, 1.80), -fig.U * 0.8 + fig.R * 0.6 - fig.F * 0.2,
                       fig.F * 0.95 + fig.U * 0.10, fig.U, 0.0))
    hold = dict(out_)
    hold["armR_wrist"] = fig.V(0.50, -0.14, 1.79)
    keys = [(0, stand), (18, out_), (36, hold), (48, stand)]
    return dict(frames=N, loop=False, author="grandma", pose=keyed(keys, N, False),
                keys=[1, 10, 19, 25, 37, 49], checks=["planted"])


# ----------------------------------------------------------------- crew actions (2026-09-26)
# The crew's hands and falls: holding a small thing, smoking, drinking, throwing, pocketing,
# putting something on, waving, and three full-body falls (knocked down by a blast, getting up,
# tripping at a run). All authored on the crew rig.
#
# The upper-body ones play on AC_Crew_Slice's "Hands" layer, masked to the right arm and the
# head (AM_RightArmHead): hips, spine and legs stay the Locomotion's, so they play while
# standing, walking or crouching (layering_check measures it). The falls are base-layer states.
#
# Hands and props share one frame, HandSockets.cs (NPC/), reproduced by socket_frame(): local Z
# along the fingers, Y out of the palm, X the thumb on the right hand; the origin palmAlong of
# the hand's length from the wrist and palmOut metres out of the palm. A prop is authored once in
# that frame in metres (HandHeld.cs grips, the same numbers), and the clips place the hand FROM
# the prop: "the filter on the lips" is solved as a wrist position (hand_at), then measured back
# on the posed body (grip_report), which is what Unity will show.
#
# Motion-time clips: Crew_Smoke and Crew_Drink are played by normalized time from the item's own
# clock (AnimatorState Motion Time = "UseTime"), so the hand is at the lips exactly while the
# ember glows. Their key frames below ARE the item timelines of UseTimeline.cs.

PALM_ALONG = 0.6          # HandSockets.palmAlong
PALM_OUT_M = 0.03         # HandSockets.palmOut; the crew bones import at scale 1, so metres

# The props as HandHeld.cs grips them in the right-hand socket (metres; x thumb, y out of the
# palm, z along the fingers). `axis` is the item's +Y (the cylinder axis, towards the cigarette's
# lit tip, towards the bottle's neck), as a socket direction. Sizes are the game's primitives.
CIG = dict(center=(0.012, -0.012, 0.062), axis=(0.0, -1.0, 0.0), half=0.045, radius=0.0065)
BEER = dict(center=(-0.02, 0.022, 0.0), axis=(1.0, 0.0, 0.0), half=0.055, radius=0.0175,
            neck_top=0.105, neck_mid=0.08, neck_half=0.025, neck_radius=0.007)


def _along(prop, dist):
    c, a = prop["center"], prop["axis"]
    return tuple(c[i] + a[i] * dist for i in range(3))


CIG_FILTER = _along(CIG, -CIG["half"])      # the end that goes in the mouth
BEER_NECK = _along(BEER, BEER["neck_top"])  # the bottle's lip

# Smoke cycle (UseTimeline.Smoke): 40 frames, 1.667 s. Aside 0, at the lips by f7 (0.175),
# inhale until f18 (0.45), exhale from f19 (0.475) while the hand goes back down, aside by f26
# (0.65). Drink loop (UseTimeline.Drink): 60 frames, 2.5 s, a gulp every 15 frames (0.625 s).
SMOKE_N = 40
SMOKE_KEYS = dict(lips=7, inhale_end=18, exhale=19, aside=26)
DRINK_N = 60
DRINK_GULPS = 4


def socket_frame(fig, s="R"):
    """HandSockets.Build on the posed hand: (origin, thumb, palm, fingers), armature space."""
    pb = fig.pb["hand." + s]
    z = (pb.tail - pb.head).normalized()
    y = fig.palm_now(s)
    y = (y - z * y.dot(z)).normalized()
    x = z.cross(y)                      # right hand: the thumb side (left hand: the little finger)
    o = pb.head + z * (PALM_ALONG * fig.len["hand." + s]) + y * (PALM_OUT_M / fig.m_per_bu)
    return o, x, y, z


def socket_point(fig, frame, p):
    o, x, y, z = frame
    return o + (x * p[0] + y * p[1] + z * p[2]) / fig.m_per_bu


def socket_dir(frame, d):
    _o, x, y, z = frame
    return (x * d[0] + y * d[1] + z * d[2]).normalized()


def grip_report(fig, kind, frame):
    """Where the posed hand really puts the prop's business end, against the lips (metres)."""
    sf = socket_frame(fig)
    end = CIG_FILTER if kind == "cig" else BEER_NECK
    d = (socket_point(fig, sf, end) - fig.mouth()).length * fig.m_per_bu
    what = "cigarette filter" if kind == "cig" else "bottle lip"
    return "grip f{}: {} {:.3f} m from the lips".format(frame, what, d)


def make_hand_props():
    """The cigarette and the beer as the game builds them (ItemArt primitives, CigaretteItem and
    BeerItem sizes), in metres along local Z (the item's +Y), hidden until a clip holds one."""
    import bmesh

    def part(name, pieces, color):
        me = bpy.data.meshes.new(name)
        bm = bmesh.new()
        for r, z0, z1 in pieces:
            bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=12, radius1=r, radius2=r,
                                  depth=z1 - z0, matrix=Matrix.Translation((0.0, 0.0, (z0 + z1) * 0.5)))
        bm.to_mesh(me)
        bm.free()
        m = bpy.data.materials.new(name + "_mat")
        m.diffuse_color = color
        me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.hide_render = True
        return ob

    h = CIG["half"]
    cig = [part("prop_cig_paper", [(CIG["radius"], -h, h)], (0.93, 0.92, 0.88, 1.0)),
           part("prop_cig_ember", [(CIG["radius"] * 1.15, h, h + 0.008)], (1.0, 0.42, 0.10, 1.0))]
    b = BEER
    beer = [part("prop_beer", [(b["radius"], -b["half"], b["half"]),
                               (b["neck_radius"], b["neck_mid"] - b["neck_half"], b["neck_top"])],
                 (0.55, 0.28, 0.06, 1.0))]
    return {"cig": (None, cig), "beer": (None, beer)}


def place_hand_prop(props, kind, fig, arm):
    """Show prop `kind` in the right-hand socket of the posed `fig` (None hides every hand prop)."""
    for key in ("cig", "beer"):
        if key in props:
            show(props[key][1], False)
    if kind is None or fig is None or kind not in props:
        return
    prop = CIG if kind == "cig" else BEER
    sf = socket_frame(fig)
    o = socket_point(fig, sf, prop["center"])
    z = socket_dir(sf, prop["axis"])
    x = z.orthogonal().normalized()
    y = z.cross(x)
    rot = Matrix((x, y, z)).transposed()
    mw = arm.matrix_world
    k = 1.0 / fig.m_per_bu
    local = Matrix.Translation(o) @ (rot.to_4x4() @ Matrix.Diagonal((k, k, k, 1.0)))
    for ob in props[kind][1]:
        ob.matrix_world = mw @ local
        ob.hide_render = False
    upd()


def crew_only(builder):
    """A clip for the crew alone: measured and rendered on the crew body only."""
    def build(ctx, fig):
        spec = builder(ctx, fig)
        spec["crew_only"] = True
        return spec
    return build


def hand_frame(fig, fingers, palm):
    z = resolve(fig, fingers).normalized()
    y = resolve(fig, palm)
    y = (y - z * y.dot(z)).normalized()
    return z.cross(y), y, z


def hand_at(s, point, grip, fingers, palm, pole):
    """Arm channels that put the socket-frame point `grip` (metres) of hand s on `point` (a
    Vector or a function of the posed body), fingers along `fingers`, palm facing `palm`. The
    wrist is solved from that ideal socket frame; grip_report measures where the posed hand
    really puts the prop."""
    def wrist(f):
        x, y, z = hand_frame(f, fingers, palm)
        o = resolve(f, point) - (x * grip[0] + y * grip[1] + z * grip[2]) / f.m_per_bu
        return o - z * (PALM_ALONG * f.len["hand." + s]) - y * (PALM_OUT_M / f.m_per_bu)
    return arm_ch(s, wrist, pole, lambda f: hand_frame(f, fingers, palm)[2],
                  lambda f: hand_frame(f, fingers, palm)[1], 0.0)


def fingers_from(thumb, palm):
    """The finger direction of a right hand whose thumb and palm point this way (thumb =
    fingers x palm, so fingers = palm x thumb)."""
    def fingers(f):
        t = resolve(f, thumb).normalized()
        p = resolve(f, palm)
        p = (p - t * p.dot(t)).normalized()
        return p.cross(t)
    return fingers


def arm_dir(s, direction, reach, pole, hand, palm, flex=0.0):
    """An arm pointed along `direction` from its shoulder, the wrist `reach` of the arm's length
    out: for the full-body falls, where the shoulder is wherever the torso went."""
    def wrist(f):
        d = resolve(f, direction).normalized()
        return f.pb["upper_arm." + s].head + d * (reach * (f.len["upper_arm." + s] + f.len["forearm." + s]))
    return arm_ch(s, wrist, pole, hand, palm, flex)


def on_knee(s, knee_side, up=0.07, fwd=0.02):
    """Hand s pushing on the `knee_side` knee (the push of getting up)."""
    def wrist(f):
        k = f.pb["shin." + knee_side].head
        return k + f.U * up + f.F * fwd + f.out(knee_side) * 0.02 - f.F * 0.10
    return arm_ch(s, wrist, fig_out_pole(s), lambda f: f.F * 0.7 - f.U * 0.7, lambda f: -f.U, 15.0)


def fig_out_pole(s):
    return lambda f: -f.F * 0.3 + f.out(s) * 0.9 - f.U * 0.3


def crew_base(ctx, fig, t=0.0, breaths=1):
    """The crew standing base of the action clips: Crew_Idle's stance and hanging arms, one breath
    per loop. Only the right arm and the head reach Unity through the Hands mask; the rest keeps
    the renders honest."""
    b = 0.5 - 0.5 * math.cos(breaths * math.tau * t)
    P = c_stand(ctx, fig)
    P.update(s2=0.5 - 1.2 * b, s3=0.3 - 0.6 * b, shrugL=1.2 * b, shrugR=1.2 * b)
    for s in "LR":
        P.update(hang(s, ctx.crew_half, gap=0.07, swing=2.0 + 1.0 * b, bend=10.0 + 3.0 * b))
    return P


def fit_floor(ctx, fig, P, target=0.004, ids=None):
    """Raise or lower the hips of pose P until its lowest body vertex sits `target` BU above the
    floor. For the poses that lie, sit or kneel on it. Returns the hip height it settled on."""
    body = ctx.bodies[id(fig)]
    for _ in range(6):
        fig.apply(P)
        co = evaluated_coords(body, fig.arm)
        rng = ids if ids is not None else range(len(co))
        low = min(co[i].dot(fig.U) for i in rng)
        if abs(low - target) < 0.001:
            break
        P["hip"] = resolve(fig, P["hip"]) + fig.U * (target - low)
    return resolve(fig, P["hip"]).dot(fig.U)


def floor_guard(ctx, fig, pose, floor=0.003):
    """A pose function that never goes through the floor. The keys of a fall are fitted onto the
    floor (fit_floor), but the frames between them are blends, and a foot turning from flat to
    toes-down on its way (or a thigh landing) dips under it: up to 0.20 BU in the first Crew_Fall.
    Per frame: a shoe under the floor lifts its own ankle target, anything else lifts the whole
    body (hips and both ankle targets together, so the legs keep their shape)."""
    body = ctx.bodies[id(fig)]
    if not hasattr(ctx, "doms"):
        ctx.doms = {}
    if id(fig) not in ctx.doms:
        dom = dominant(body)
        shoe = {s: set(shoe_verts(body, s)) for s in "LR"}
        rest = [i for i in range(len(dom)) if i not in shoe["L"] and i not in shoe["R"]]
        ctx.doms[id(fig)] = (shoe, rest)
    shoe, rest = ctx.doms[id(fig)]

    def guarded(t):
        P = dict(pose(t))
        for _ in range(4):
            fig.apply(P)
            co = evaluated_coords(body, fig.arm)
            U = fig.U
            changed = False
            low = min(co[i].dot(U) for i in rest)
            if low < floor - 1e-4:
                lift = U * (floor - low)
                P["hip"] = resolve(fig, P["hip"]) + lift
                for sd in "LR":
                    a = resolve(fig, P.get("leg%s_ankle" % sd))
                    if a is not None:
                        P["leg%s_ankle" % sd] = a + lift
                changed = True
            else:
                for sd in "LR":
                    lo = min(co[i].dot(U) for i in shoe[sd])
                    a = resolve(fig, P.get("leg%s_ankle" % sd))
                    if lo < floor - 1e-4 and a is not None:
                        P["leg%s_ankle" % sd] = a + U * (floor - lo)
                        changed = True
            if not changed:
                break
        return P
    return guarded


def smoke_aside(fig):
    """The smoker's rest: elbow at the ribs, hand up by the shoulder, palm in, the cigarette
    pointing out and forward."""
    return hand_at("R", fig.V(0.30, -0.36, 1.98), CIG["center"],
                   fig.U * 0.8 + fig.F * 0.35 + fig.L * 0.15, -fig.F * 0.55 + fig.L * 0.8,
                   -fig.U * 0.8 + fig.R * 0.6 - fig.F * 0.1)


def smoke_lips(fig):
    """The filter on the lips: palm to the face, fingers up across it, the cigarette straight out."""
    return hand_at("R", lambda f: f.mouth() + f.F * 0.012, CIG_FILTER,
                   fig.U * 0.75 + fig.L * 0.45 + fig.F * 0.1, -fig.F,
                   -fig.U * 0.8 + fig.R * 0.5 + fig.F * 0.2)


def clip_crew_smoke(ctx, fig):
    """Crew_Smoke, 40 frames (Hands layer, played by motion time from CigaretteItem's cycle): from
    the aside pose the filter to the lips (f7), the drag with the head a touch forward then
    lifting (f7-f18), the hand down and the head up and to the left as the smoke comes out
    (f19-f26), aside again. Loops on the aside pose."""
    N = SMOKE_N
    k = SMOKE_KEYS

    def base(t, **head):
        P = crew_base(ctx, fig, t)
        P.update(head)
        return P

    aside0 = base(0.0, neck=2.0, head=-2.0)
    aside0.update(smoke_aside(fig))
    lips0 = base(k["lips"] / N, neck=4.0, head=2.0)
    lips0.update(smoke_lips(fig))
    lips1 = base(k["inhale_end"] / N, neck=3.0, head=-3.0, shrugR=2.5, shrugL=2.5)
    lips1.update(smoke_lips(fig))
    blow = base(k["aside"] / N, neck=0.0, head=-9.0, head_yaw=7.0)
    blow.update(smoke_aside(fig))
    settle = base(33 / N, neck=2.0, head=-4.0, head_yaw=2.0)
    settle.update(smoke_aside(fig))
    keys = [(0, aside0), (k["lips"], lips0), (k["inhale_end"], lips1), (k["aside"], blow), (33, settle), (N, aside0)]
    return dict(frames=N, loop=True, author="crew", pose=keyed(keys, N, True),
                keys=[1, 5, 8, 19, 23, 27], checks=["planted"], hand_prop="cig", view="upper",
                grips=[(k["lips"] + 1, "cig"), (k["inhale_end"] + 1, "cig")])


def beer_arm(fig, angle, gulp=0.0):
    """The bottle's lip on the lips, its bottom `angle` degrees above its neck. The fist holds it
    from the right (palm to the left, thumb along the bottle towards the
    mouth), the elbow forward and out. The grip lands 1.6 to 2.2 cm off the lips at 20 to 24
    degrees (more tilt drifts further: 3.6 cm at 34; a palm turned up drifts 11 to 16 cm, the
    forearm twist cannot reach it), so the tilt stays 22 and the head's 22 degrees back does the rest. (Not drink_arm:
    that name is the grandmother's cup, which Sit_Drink and Stand_Drink use.)"""
    def thumb(f):
        a = math.radians(angle + 6.0 * gulp)
        return -f.F * math.cos(a) - f.U * math.sin(a)
    palm = fig.L
    return hand_at("R", lambda f: f.mouth() + f.F * 0.010, BEER_NECK, fingers_from(thumb, palm), palm,
                   fig.R * 0.6 - fig.U * 0.3 + fig.F * 0.6)


def clip_crew_drink(ctx, fig):
    """Crew_Drink, 60 frames loop (Hands layer, motion time from BeerItem's clock): the bottle at
    the mouth tipped 22 degrees, the head back 22 degrees, four gulps a loop (the head and the
    bottle dip back 3 and 6 degrees on each). The raise and the lowering are the Actions layer's
    weight blend (CrewAnimator), from whatever the arm was doing."""
    N = DRINK_N

    def pose(t):
        g = max(0.0, math.sin(DRINK_GULPS * math.tau * t)) ** 4
        P = crew_base(ctx, fig, t, breaths=2)
        P.update(neck=-7.0 - 1.0 * g, head=-15.0 - 3.0 * g, shrugR=3.0, s3=-1.0)
        P.update(beer_arm(fig, 22.0, g))
        return P
    return dict(frames=N, loop=True, author="crew", pose=pose, keys=[1, 5, 16, 31], checks=["planted"],
                hand_prop="beer", view="upper", grips=[(1, "beer"), (5, "beer"), (31, "beer")])


def clip_crew_throw(ctx, fig):
    """Crew_Throw, 0.83 s one-shot (Hands layer): starts cocked (the hand behind the head, which
    the 0.08 s cross-fade snaps to), whips forward to the release at f4, follows through across
    the body (f9), and hangs by f20. Entered on ObjectThrown, so the whip is at most 0.17 s behind
    the object leaving: it reads as the throw that sent it."""
    N = 20
    cock = crew_base(ctx, fig, 0.0, breaths=0)
    cock.update(head=-4.0, head_yaw=-6.0, shrugR=4.0)
    cock.update(hand_at("R", fig.V(-0.36, -0.40, 2.42), (0.0, 0.0, 0.0), fig.U * 0.45 - fig.F * 0.8 - fig.R * 0.1,
                        fig.F * 0.6 + fig.U * 0.4, fig.U * 0.25 + fig.R * 0.9 - fig.F * 0.3))
    release = crew_base(ctx, fig, 0.0, breaths=0)
    release.update(head=2.0, head_yaw=4.0)
    release.update(hand_at("R", fig.V(0.58, -0.22, 2.22), (0.0, 0.0, 0.0), fig.F * 0.9 + fig.U * 0.2,
                           -fig.U * 0.5 + fig.F * 0.5, -fig.U * 0.4 + fig.R * 0.9))
    follow = crew_base(ctx, fig, 0.0, breaths=0)
    follow.update(head=6.0, head_yaw=8.0, shrugR=-2.0)
    follow.update(hand_at("R", fig.V(0.40, 0.02, 1.62), (0.0, 0.0, 0.0), fig.F * 0.45 - fig.U * 0.75 + fig.L * 0.45,
                          -fig.F * 0.4 + fig.L * 0.6, -fig.U * 0.8 + fig.R * 0.5))
    end = crew_base(ctx, fig, 0.0, breaths=0)
    keys = [(0, cock), (4, release), (9, follow), (N, end)]
    return dict(frames=N, loop=False, author="crew", pose=keyed(keys, N, False),
                keys=[1, 3, 5, 8, 10, 15, 21], checks=["planted"], view="upper")


def clip_crew_pocket(ctx, fig):
    """Crew_Pocket, 0.83 s one-shot (Hands layer): the right hand down into the right front
    trouser pocket (f7), in (f10), out (f14), back by f20. Entered on ItemPocketed and
    ItemUnpocketed: the thing appears or vanishes as the hand gets there."""
    N = 20
    fingers = -fig.U * 0.85 + fig.F * 0.2 + fig.R * 0.1
    palm = fig.L * 0.8 - fig.F * 0.2
    pole = -fig.F * 0.5 + fig.R * 0.7 - fig.U * 0.2
    stand = crew_base(ctx, fig, 0.0, breaths=0)
    at = dict(stand)
    at.update(head=8.0, neck=4.0, shrugR=-3.0, s_side=-2.0)
    at.update(hand_at("R", fig.V(0.12, -0.31, 1.36), (0.0, 0.0, 0.0), fingers, palm, pole))
    deep = dict(at)
    deep.update(hand_at("R", fig.V(0.12, -0.31, 1.30), (0.0, 0.0, 0.0), fingers, palm, pole))
    out_ = dict(at)
    out_.update(head=5.0)
    keys = [(0, stand), (7, at), (10, deep), (14, out_), (N, stand)]
    return dict(frames=N, loop=False, author="crew", pose=keyed(keys, N, False),
                keys=[1, 8, 11, 15, 21], checks=["planted"], view="upper")


def clip_crew_wear(ctx, fig):
    """Crew_Wear, 1 s one-shot (Actions layer): the right hand lifted in front of the chest (f6),
    palm down on the top of the head (f10), a pat (f13), a tug by the ear (f16), down in front of
    the chest (f19), hanging by f24: putting a thing on, whatever the slot. Entered when the set
    of worn pieces changes (CrewAnimator)."""
    N = 24
    out_pole = fig.R * 0.9 - fig.U * 0.35 + fig.F * 0.1
    high_pole = fig.R * 0.9 + fig.F * 0.25 - fig.U * 0.1
    stand = crew_base(ctx, fig, 0.0, breaths=0)
    lift = dict(stand)
    lift.update(head=2.0, shrugR=2.0)
    lift.update(hand_at("R", fig.V(0.34, -0.30, 2.02), (0.0, 0.0, 0.0), fig.F * 0.7 + fig.U * 0.6,
                        fig.L * 0.9 - fig.U * 0.1, out_pole))
    on = dict(stand)
    on.update(head=8.0, neck=3.0, shrugR=6.0)
    on.update(hand_at("R", fig.V(0.07, -0.03, 2.53), (0.0, 0.0, 0.0), fig.F * 0.7 + fig.L * 0.55,
                      -fig.U, high_pole))
    pat = dict(on)
    pat.update(head=11.0, neck=4.0)
    pat.update(hand_at("R", fig.V(0.07, -0.03, 2.49), (0.0, 0.0, 0.0), fig.F * 0.7 + fig.L * 0.55,
                       -fig.U, high_pole))
    tug = dict(stand)
    tug.update(head=5.0, neck=2.0, head_roll=-4.0, shrugR=4.0)
    tug.update(hand_at("R", fig.V(0.10, -0.26, 2.42), (0.0, 0.0, 0.0), fig.U * 0.75 + fig.F * 0.45,
                       fig.L * 0.9 - fig.F * 0.1, out_pole))
    lower = dict(stand)
    lower.update(head=1.0)
    lower.update(hand_at("R", fig.V(0.28, -0.32, 1.86), (0.0, 0.0, 0.0), fig.F * 0.9 - fig.U * 0.2,
                         fig.L * 0.9, out_pole))
    keys = [(0, stand), (6, lift), (10, on), (13, pat), (16, tug), (19, lower), (N, stand)]
    return dict(frames=N, loop=False, author="crew", pose=keyed(keys, N, False),
                keys=[1, 4, 7, 11, 14, 17, 20, 25], checks=["planted"], view="upper")


def clip_crew_wave(ctx, fig):
    """Crew_Wave, 1 s loop (Hands layer): the right hand up beside the head, palm forward, waved
    +/-22 degrees twice a second. For an emote key and the title screen."""
    def pose(t):
        w = math.radians(22.0 * math.sin(2.0 * math.tau * t))
        P = crew_base(ctx, fig, t)
        P.update(head=-3.0, head_yaw=-5.0, shrugR=6.0)

        def fingers(f):
            return f.U * math.cos(w) + f.R * math.sin(w)
        P.update(hand_at("R", fig.V(0.16, -0.55, 2.60), (0.0, 0.0, 0.0), fingers, fig.F,
                         -fig.U * 0.6 + fig.R * 0.8 - fig.F * 0.1))
        return P
    return dict(frames=24, loop=True, author="crew", pose=pose, keys=[1, 4, 7, 13], checks=["planted"],
                view="upper")


def lying_back(ctx, fig, head=0.0, spread=0.0):
    """On the back: hips 0.5 BU behind the root, torso flat along -F, legs out along +F with the
    toes up, arms spread on the floor. Fitted onto the floor."""
    P = c_stand(ctx, fig)
    P.update(hip=fig.V(-0.50, 0.0, 0.30), pel_flex=-86.0, s1=0.0, s2=0.0, s3=0.0, neck=2.0, head=head)
    for s, sg in (("L", 1.0), ("R", -1.0)):
        P["leg%s_ankle" % s] = fig.V(0.46, sg * 0.25, 0.16)
        P["leg%s_pole" % s] = fig.U + fig.F * 0.3
        P["leg%s_pitch" % s] = 72.0
        P["leg%s_yaw" % s] = sg * 10.0
        out = fig.out(s)
        P.update(arm_dir(s, -fig.F * (0.35 - spread) + out * 0.95 - fig.U * 0.05, 0.96,
                         fig.U * 0.6 + fig.F * 0.3, -fig.F * 0.2 + out * 0.9, fig.U))
    fit_floor(ctx, fig, P)
    return P


def clip_crew_knocked_down(ctx, fig):
    """Crew_KnockedDown, 1.25 s one-shot (base layer, full body): a blast in front. A flinch with
    the arms up (f3), thrown back off the feet with the legs out and the arms flung (f9), the seat
    lands (f15), the back lands (f21), a small bounce of the head (f24), lying still (f30, which
    Crew_GetUp starts from). In place: the capsule carries the flight, CrewAnimator turns the body
    to face the blast and holds that yaw while it lies."""
    N = 30
    stand = crew_base(ctx, fig, 0.0, breaths=0)
    flinch = dict(stand)
    flinch.update(hip=fig.hip_mid0 + fig.V(-0.04, 0.0, -0.12), pel_flex=8.0, s1=6.0, s2=4.0, neck=6.0, head=10.0)
    for s in "LR":
        out = fig.out(s)
        flinch.update(arm_ch(s, fig.V(0.36, 0.0, 2.22) + out * 0.16, -fig.U * 0.7 + out * 0.6,
                             fig.U * 0.9 + fig.F * 0.2 - out * 0.2, fig.F, 0.0))
    fly = c_stand(ctx, fig)
    fly.update(hip=fig.V(-0.35, 0.0, 1.42), pel_flex=-32.0, s1=-6.0, s2=-6.0, s3=-4.0, neck=8.0, head=12.0)
    for s, sg in (("L", 1.0), ("R", -1.0)):
        out = fig.out(s)
        fly["leg%s_ankle" % s] = fig.V(0.28, sg * 0.20, 0.62)
        fly["leg%s_pole" % s] = fig.U + fig.F
        fly["leg%s_pitch" % s] = 25.0
        fly.update(arm_dir(s, -fig.F * 0.2 + out * 0.7 + fig.U * 0.75, 0.97, -fig.U * 0.3 + out * 0.8,
                           fig.U * 0.7 + out * 0.6, fig.F))
    seat = c_stand(ctx, fig)
    seat.update(hip=fig.V(-0.55, 0.0, 0.35), pel_flex=-52.0, s1=-4.0, s2=-4.0, s3=-2.0, neck=6.0, head=10.0)
    for s, sg in (("L", 1.0), ("R", -1.0)):
        out = fig.out(s)
        seat["leg%s_ankle" % s] = fig.V(0.42, sg * 0.22, 0.34)
        seat["leg%s_pole" % s] = fig.U + fig.F * 0.5
        seat["leg%s_pitch" % s] = 45.0
        seat.update(arm_dir(s, -fig.F * 0.5 + out * 0.8 - fig.U * 0.25, 0.96, fig.U * 0.4 + out * 0.8,
                            -fig.F * 0.4 + out * 0.8, -fig.U))
    fit_floor(ctx, fig, seat)
    flat = lying_back(ctx, fig, head=0.0)
    bounce = lying_back(ctx, fig, head=-7.0)
    bounce["hip"] = bounce["hip"] + fig.U * 0.02
    rest = lying_back(ctx, fig, head=-3.0, spread=0.08)
    keys = [(0, stand), (3, flinch), (9, fly), (15, seat), (21, flat), (24, bounce), (N, rest)]
    return dict(frames=N, loop=False, author="crew", pose=floor_guard(ctx, fig, keyed(keys, N, False)),
                keys=[1, 4, 10, 16, 22, 31], checks=[], view="floor", yaw=80.0)


def clip_crew_get_up(ctx, fig):
    """Crew_GetUp, 1.67 s one-shot (base layer, full body): from Crew_KnockedDown's last frame,
    sits up with the hands behind (f8), pulls the feet in flat by the seat, one hand on the floor
    and one on a knee (f16), pushes into a squat with both hands on the knees (f24), rises (f32),
    stands on the root (f40: Crew_Idle's stance, so Locomotion follows without a jump). The feet
    are planted from f16 on."""
    N = 40
    start = clip_crew_knocked_down(ctx, fig)["pose"](1.0)
    sit = c_stand(ctx, fig)
    sit.update(hip=fig.V(-0.52, 0.0, 0.30), pel_flex=-14.0, s1=6.0, s2=4.0, s3=2.0, neck=4.0, head=6.0)
    for s, sg in (("L", 1.0), ("R", -1.0)):
        out = fig.out(s)
        sit["leg%s_ankle" % s] = fig.V(0.30, sg * 0.22, 0.14)
        sit["leg%s_pole" % s] = fig.U + fig.F
        sit["leg%s_pitch" % s] = 40.0
        sit.update(arm_dir(s, -fig.F * 0.55 + out * 0.3 - fig.U * 0.8, 0.97, -fig.F * 0.5 + out * 0.8,
                           -fig.U * 0.8 - fig.F * 0.3, -fig.U))
    fit_floor(ctx, fig, sit)
    feet = planted(fig)
    for s in "LR":
        feet["leg%s_pole" % s] = fig.F + fig.out(s) * 0.25
    tuck = c_stand(ctx, fig)
    tuck.update(feet)
    tuck.update(hip=fig.V(-0.48, 0.0, 0.30), pel_flex=12.0, s1=8.0, s2=4.0, neck=0.0, head=-4.0)
    tuck.update(arm_dir("R", -fig.U * 0.9 + fig.R * 0.35 - fig.F * 0.1, 0.97, -fig.F * 0.3 + fig.R * 0.9,
                        -fig.U * 0.8 + fig.F * 0.2, -fig.U))
    tuck.update(on_knee("L", "L"))
    fit_floor(ctx, fig, tuck)
    squat = c_stand(ctx, fig)
    squat.update(feet)
    squat.update(hip=fig.V(-0.24, 0.0, 0.74), pel_flex=38.0, s1=12.0, s2=6.0, neck=-10.0, head=-10.0)
    squat.update(on_knee("L", "L"))
    squat.update(on_knee("R", "R"))
    rise = c_stand(ctx, fig)
    rise.update(feet)
    rise.update(hip=fig.V(-0.07, 0.0, 1.18), pel_flex=18.0, s1=6.0, s2=3.0, neck=-2.0, head=-4.0)
    for s in "LR":
        rise.update(hang(s, ctx.crew_half, gap=0.08, swing=10.0, bend=20.0))
    stand = crew_base(ctx, fig, 0.0, breaths=0)
    keys = [(0, start), (8, sit), (16, tuck), (24, squat), (32, rise), (N, stand)]
    return dict(frames=N, loop=False, author="crew", pose=floor_guard(ctx, fig, keyed(keys, N, False)),
                keys=[1, 9, 17, 25, 33, 41], checks=[], view="floor", yaw=80.0)


def run_pose(ctx, fig, lead="R"):
    """A running pose as channels: the lead leg forward, the other pushing off, arms bent and
    swinging opposite, leaning 11 degrees (Crew_Run's numbers, frozen at mid stride)."""
    P = c_stand(ctx, fig)
    P.update(hip=fig.hip_mid0 + fig.V(0.03, 0.0, -0.10), pel_flex=5.0, s1=4.0, s2=3.0, s3=2.0,
             neck=-3.0, head=-3.0)
    back = "L" if lead == "R" else "R"
    for s, fwd in ((lead, True), (back, False)):
        sg = 1.0 if s == "L" else -1.0
        P["leg%s_ankle" % s] = fig.V(0.32, sg * 0.14, 0.24) if fwd else fig.V(-0.42, sg * 0.14, 0.42)
        P["leg%s_pole" % s] = fig.F.copy()
        P["leg%s_pitch" % s] = 12.0 if fwd else -38.0
        P.update(hang(s, ctx.crew_half, gap=0.095, swing=-30.0 if fwd else 30.0, bend=78.0, fore_in=0.28))
    return P


def clip_crew_fall(ctx, fig):
    """Crew_Fall, 2.33 s one-shot (base layer, full body): tripped at a run. From a run stride
    (f0) the back foot catches and the arms go out (f5), a dive (f10), the hands hit (f14), flat
    on the belly with the heels up (f18), the legs flop (f24), a dazed head shake (f27-f33),
    hands and knees (f38), one knee (f45), up (f51), back into the run stride (f56). In place, so
    the title-screen loader can run, fall and run again on one spot (Run, Fall, Run...)."""
    N = 56
    run = run_pose(ctx, fig, "R")
    trip = c_stand(ctx, fig)
    trip.update(hip=fig.hip_mid0 + fig.V(0.22, 0.0, -0.16), pel_flex=30.0, s1=8.0, s2=6.0, neck=-14.0, head=-12.0)
    trip.update(legL_ankle=fig.V(0.46, 0.14, 0.26), legL_pole=fig.F.copy(), legL_pitch=15.0, legL_yaw=0.0,
                legR_ankle=fig.V(-0.42, -0.14, 0.34), legR_pole=fig.F.copy(), legR_pitch=-55.0, legR_yaw=0.0)
    for s in "LR":
        out = fig.out(s)
        trip.update(arm_dir(s, fig.F * 0.8 + fig.U * 0.25 + out * 0.25, 0.95, -fig.U * 0.6 + out * 0.6,
                            fig.F * 0.8 + fig.U * 0.2, -fig.U * 0.3 + fig.F * 0.3))
    dive = c_stand(ctx, fig)
    dive.update(hip=fig.V(0.55, 0.0, 0.85), pel_flex=62.0, s1=8.0, s2=5.0, neck=-24.0, head=-20.0)
    for s, sg in (("L", 1.0), ("R", -1.0)):
        out = fig.out(s)
        dive["leg%s_ankle" % s] = fig.V(-0.35, sg * 0.16, 1.00)
        dive["leg%s_pole" % s] = fig.F * 0.5 - fig.U * 0.5
        dive["leg%s_pitch" % s] = -70.0
        dive["leg%s_yaw" % s] = 0.0
        dive.update(arm_dir(s, fig.F * 0.75 - fig.U * 0.55 + out * 0.25, 0.95, fig.U * 0.4 + out * 0.8,
                            fig.F * 0.6 - fig.U * 0.6, -fig.U))
    hands = c_stand(ctx, fig)
    hands.update(hip=fig.V(0.55, 0.0, 0.50), pel_flex=80.0, s1=4.0, s2=2.0, neck=-26.0, head=-22.0)
    for s, sg in (("L", 1.0), ("R", -1.0)):
        out = fig.out(s)
        hands["leg%s_ankle" % s] = fig.V(-0.45, sg * 0.16, 0.82)
        hands["leg%s_pole" % s] = -fig.U + fig.F * 0.3
        hands["leg%s_pitch" % s] = -80.0
        hands["leg%s_yaw" % s] = 0.0
        hands.update(arm_ch(s, fig.V(1.36, sg * 0.34, 0.14), fig.U * 0.3 + out * 0.8,
                            fig.F * 0.9 + out * 0.1, -fig.U, 0.0))
    fit_floor(ctx, fig, hands)

    leg = fig.len["thigh.L"] + fig.len["shin.L"]

    def prone(head, heels_up, head_yaw=0.0):
        P = c_stand(ctx, fig)
        P.update(hip=fig.V(0.42, 0.0, 0.25), pel_flex=87.0, s1=2.0, s2=2.0, s3=0.0, neck=-28.0, head=head,
                 head_yaw=head_yaw)
        for s, sg in (("L", 1.0), ("R", -1.0)):
            out = fig.out(s)
            P["leg%s_ankle" % s] = (fig.V(0.42 - 0.62 * leg, sg * 0.18, 0.62) if heels_up
                                    else fig.V(0.42 - 0.94 * leg, sg * 0.20, 0.14))
            P["leg%s_pole" % s] = -fig.U + fig.F * 0.2
            # Flat: the tops of the feet on the floor, toes pointing back (-165: a toe-down pitch
            # of -95 drove the toes 0.15 BU into the floor and fit_floor lifted the hips into a plank).
            P["leg%s_pitch" % s] = -40.0 if heels_up else -165.0
            P["leg%s_yaw" % s] = 0.0
            P.update(arm_ch(s, fig.V(1.28, sg * 0.42, 0.10), fig.U * 0.2 + out * 0.9,
                            fig.F * 0.9 + out * 0.2, -fig.U, 0.0))
        fit_floor(ctx, fig, P)
        return P
    belly = prone(-22.0, True)
    flop = prone(-4.0, False)
    shake1 = prone(-18.0, False, 16.0)
    shake2 = prone(-18.0, False, -16.0)
    shake3 = prone(-16.0, False, 8.0)
    fours = c_stand(ctx, fig)
    fours.update(hip=fig.V(0.10, 0.0, 0.74), pel_flex=80.0, s1=2.0, s2=0.0, neck=-22.0, head=-14.0)
    for s, sg in (("L", 1.0), ("R", -1.0)):
        out = fig.out(s)
        fours["leg%s_ankle" % s] = fig.V(-0.50, sg * 0.18, 0.12)
        fours["leg%s_pole" % s] = fig.F.copy()
        fours["leg%s_pitch" % s] = -165.0          # tops of the feet on the floor
        fours["leg%s_yaw" % s] = 0.0
        fours.update(arm_ch(s, fig.V(0.86, sg * 0.30, 0.12), -fig.F * 0.4 + out * 0.8,
                            fig.F * 0.9 + out * 0.1, -fig.U, 0.0))
    fit_floor(ctx, fig, fours)
    kneel = c_stand(ctx, fig)
    kneel.update(hip=fig.V(-0.02, 0.0, 0.98), pel_flex=24.0, s1=8.0, s2=4.0, neck=-8.0, head=-6.0)
    kneel.update(legL_ankle=rest_ankle(fig, "L", 0.36), legL_pole=fig.F.copy(), legL_pitch=0.0, legL_yaw=0.0,
                 legR_ankle=fig.V(-0.46, -0.16, 0.30), legR_pole=fig.F.copy(), legR_pitch=-45.0, legR_yaw=0.0)
    kneel.update(on_knee("R", "L"))
    kneel.update(hang("L", ctx.crew_half, gap=0.08, swing=6.0, bend=20.0))
    fit_floor(ctx, fig, kneel)
    up = c_stand(ctx, fig)
    up.update(hip=fig.hip_mid0 + fig.V(0.05, 0.0, -0.12), pel_flex=14.0, s1=6.0, s2=3.0, neck=-4.0, head=-4.0)
    up.update(legL_ankle=rest_ankle(fig, "L", 0.18), legL_pole=fig.F.copy(), legL_pitch=0.0, legL_yaw=0.0,
              legR_ankle=rest_ankle(fig, "R", -0.28) + fig.U * 0.06, legR_pole=fig.F.copy(), legR_pitch=-25.0, legR_yaw=0.0)
    for s, sw in (("L", 12.0), ("R", -12.0)):
        up.update(hang(s, ctx.crew_half, gap=0.09, swing=sw, bend=40.0))
    keys = [(0, run), (5, trip), (10, dive), (14, hands), (18, belly), (24, flop), (27, shake1), (30, shake2),
            (33, shake3), (38, fours), (45, kneel), (51, up), (N, run)]
    return dict(frames=N, loop=False, author="crew", pose=floor_guard(ctx, fig, keyed(keys, N, False)),
                keys=[1, 6, 11, 15, 19, 25, 31, 36, 39, 43, 46, 52, 57], checks=[], view="floor", yaw=85.0)


CREW_ACTIONS = [
    ("Crew_Smoke", crew_only(clip_crew_smoke)),
    ("Crew_Drink", crew_only(clip_crew_drink)),
    ("Crew_Throw", crew_only(clip_crew_throw)),
    ("Crew_Pocket", crew_only(clip_crew_pocket)),
    ("Crew_Wear", crew_only(clip_crew_wear)),
    ("Crew_Wave", crew_only(clip_crew_wave)),
    ("Crew_KnockedDown", crew_only(clip_crew_knocked_down)),
    ("Crew_GetUp", crew_only(clip_crew_get_up)),
    ("Crew_Fall", crew_only(clip_crew_fall)),
]


CLIPS = [
    ("Crew_Idle", clip_crew_idle),
    ("Crew_Walk", gait_clip(WALK, "crew", "crew_half")),
    ("Crew_Run", gait_clip(RUN, "crew", "crew_half")),
    ("Crew_CrouchIdle", clip_crew_crouch_idle),
    ("Crew_CrouchWalk", gait_clip(CROUCH_WALK, "crew", "crew_half")),
    ("Grandma_Walk", gait_clip(GRANDMA_WALK, "grandma", "g_half")),
    ("Sit_Down", clip_sit_down),
    ("Sit_Idle", clip_sit_idle),
    ("Stand_Up", clip_stand_up),
    ("Sit_Read", clip_sit_read),
    ("Sit_Drink", clip_sit_drink),
    ("Kneel_Down", clip_kneel_down),
    ("Kneel_LightFire", clip_kneel_fire),
    ("Kneel_Up", clip_kneel_up),
    ("Stand_Cook", clip_stand_cook),
    ("Stand_Water", clip_stand_water),
    ("Stand_Drink", clip_stand_drink),
    ("Angry_ShakeFist", clip_shake_fist),
    ("Angry_HandsOnHips", clip_hands_on_hips),
    ("Angry_Point", clip_point),
    ("Talk", clip_talk),
    ("Give_Keys", clip_give_keys),
] + CREW_ACTIONS


# ------------------------------------------------------------------------------- scene


def import_body(path, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    body = max((o for o in new if o.type == "MESH"), key=lambda o: len(o.data.vertices))
    arm["orig_name"] = arm.name.split(".")[0]
    arm["tag"] = tag
    for o in new:
        o["tag"] = tag
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm, body


def assign_action(obj, action):
    obj.animation_data_create()
    obj.animation_data.action = action
    ad = obj.animation_data
    if hasattr(ad, "action_slot") and ad.action_slot is None:
        slots = getattr(action, "slots", [])
        if len(slots):
            ad.action_slot = slots[0]


def bake(fig, name, frames, loop, pose_fn, shift=0.0):
    """Key every frame 1..frames+1 on the KEYED bones (quaternions, plus the hips location),
    LINEAR, with quaternion sign continuity. A loop's last frame is its first by construction.
    `shift` moves the whole body `shift` BU forward after posing (decision 3): spine is the
    rig's only root bone, so translating it moves every bone and changes only its location key."""
    arm = fig.arm
    assert [b.name for b in arm.data.bones if b.parent is None] == ["spine"]
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 1, frames + 1
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    assign_action(arm, action)
    prev = {}
    fig.max_reach = 0.0
    for frame in range(1, frames + 2):
        t = ((frame - 1) % frames) / frames if loop else (frame - 1) / frames
        pose_fn(t)
        if shift:
            hips = arm.pose.bones["spine"]
            hips.matrix = Matrix.Translation(fig.F * shift) @ hips.matrix
            upd()
        for bn in KEYED:
            pb = arm.pose.bones[bn]
            q = pb.rotation_quaternion.copy()
            if bn in prev and prev[bn].dot(q) < 0.0:
                q.negate()
            pb.rotation_quaternion = q
            prev[bn] = q
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            if bn == "spine":
                pb.keyframe_insert("location", frame=frame)
    for fc in all_fcurves(action):
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    return action


def check_loop(action, frames):
    return max(abs(fc.evaluate(1) - fc.evaluate(frames + 1)) for fc in all_fcurves(action))


def export_clip(arm, others, action, name, path):
    """model_grandma.py export_clip: a temporary rest key at frame 0, export from frame 0 so the
    FBX bone nodes (from which Unity builds the clip's avatar) are the rest A pose, armature
    only, the scene renamed so the take carries the clip name, then the frame-0 key removed."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene = bpy.context.scene
    for bn in KEYED:
        pb = arm.pose.bones[bn]
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.keyframe_insert("rotation_quaternion", frame=0)
        if bn == "spine":
            pb.keyframe_insert("location", frame=0)
    names = {}
    for o in others:                      # the exported root node keeps the rig's own name
        names[o] = o.name
        o.name = "_other_" + o.name
    own = arm.name
    arm.name = arm["orig_name"]
    scene.frame_set(0)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    scene.name = name
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"},
        global_scale=1.0, apply_unit_scale=False, apply_scale_options="FBX_SCALE_NONE",
        add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
        axis_forward="-Z", axis_up="Y",
    )
    arm.name = own
    for o, n in names.items():
        o.name = n
    for fc in all_fcurves(action):
        for kp in [k for k in fc.keyframe_points if abs(k.co.x) < 1e-6]:
            fc.keyframe_points.remove(kp)
    scene.frame_set(1)
    return os.path.getsize(path)


# ---------------------------------------------------------------------------- measuring


def joint_report(fig):
    """Humanoid safety of the current pose: how much of each lower limb's rotation is NOT about
    its hinge or its own axis (Unity drops that part), and the bend angles."""
    arm = fig.arm
    worst = 0.0
    bends = {}
    for s in "LR":
        for upper, lower in (("upper_arm." + s, "forearm." + s), ("thigh." + s, "shin." + s)):
            bu, bl = arm.data.bones[upper], arm.data.bones[lower]
            pu, pl = fig.pb[upper], fig.pb[lower]
            rest_rel = bu.matrix_local.to_3x3().inverted() @ bl.matrix_local.to_3x3()
            pose_rel = pu.matrix.to_3x3().inverted() @ pl.matrix.to_3x3()
            extra = pose_rel @ rest_rel.inverted()       # the joint's rotation, parent frame
            tw = (bu.matrix_local.to_3x3().inverted() @ (bl.tail_local - bl.head_local)).normalized()
            tw2 = (extra @ tw).normalized()
            swing = tw.angle(tw2)                        # swing-twist: the twist is allowed,
            if swing > math.radians(0.2):               # the swing must be about the hinge
                a_s = tw.cross(tw2).normalized()
                h = fig.hinge[upper].normalized()
                worst = max(worst, math.degrees(swing) * math.sqrt(max(0.0, 1.0 - a_s.dot(h) ** 2)))
            du = (pu.tail - pu.head).normalized()
            dl = (pl.tail - pl.head).normalized()
            bends[lower] = math.degrees(du.angle(dl))
        th = fig.pb["thigh." + s]
        bends["thigh_flex." + s] = math.degrees((th.tail - th.head).normalized().angle(-fig.U))
    return worst, bends


def measure_gait(fig, body, gait, frames, log):
    """A6: two real vertices of each skinned shoe, nearest the ball pivot and the heel pivot.
    While a pivot carries the weight it must stay on the floor and move backward in a straight
    line at the planted speed."""
    scene = bpy.context.scene
    U, F = fig.U, fig.F
    D = gait.p["duty"]
    ids = {s: shoe_verts(body, s) for s in "LR"}
    rc = rest_coords(body, fig.arm)
    sole = {s: [i for i in ids[s] if rc[i].dot(U) < 0.01] for s in "LR"}
    ball = {s: min(sole[s], key=lambda i: (rc[i] - gait.ball[s]).length) for s in "LR"}
    heel = {s: min(sole[s], key=lambda i: (rc[i] - gait.heel[s]).length) for s in "LR"}
    track = {(s, k): [] for s in "LR" for k in ("ball", "heel")}
    minh = 1e9
    for f in range(1, frames + 2):
        scene.frame_set(f)
        co = evaluated_coords(body, fig.arm)
        t = (f - 1) / frames
        for s, off in (("L", 0.0), ("R", 0.5)):
            u = (t - off) % 1.0
            for k, vi in (("ball", ball[s]), ("heel", heel[s])):
                c = co[vi]
                fl = gait.p["flat_at"] * D
                carrying = ((k == "ball" and fl <= u <= D) or
                            (k == "heel" and 0.0 <= u <= gait.p["heel_off_at"] * D))
                if carrying and f <= frames:
                    track[(s, k)].append((u * frames, c.dot(F), c.dot(U)))
        minh = min(minh, min(co[i].dot(U) for s in "LR" for i in ids[s]))
    res = []
    for key, st in sorted(track.items()):
        if len(st) < 3:
            continue
        ts = [f / FPS for f, _x, _h in st]
        xs = [x for _f, x, _h in st]
        n = len(ts)
        mt, mx = sum(ts) / n, sum(xs) / n
        b = sum((a - mt) * (x - mx) for a, x in zip(ts, xs)) / sum((a - mt) ** 2 for a in ts)
        dev = max(abs(x - (mx + b * (a - mt))) for a, x in zip(ts, xs))
        hmax = max(abs(h) for _f, _x, h in st)
        res.append((key, -b, dev, hmax))
    return res, minh


def lowest_contacts(fig, body):
    """Shoe vertices within 5 mm (BU) of each shoe's lowest point in the current pose."""
    co = evaluated_coords(body, fig.arm)
    out = []
    for s in "LR":
        ids = shoe_verts(body, s)
        lo = min(co[i].dot(fig.U) for i in ids)
        out += [i for i in ids if co[i].dot(fig.U) < lo + 0.005 and co[i].dot(fig.U) < 0.03]
    return out, co


def measure_clip(fig, body, frames, checks, ctx, is_grandma, skirt_ids, step):
    """Per frame: the lowest body point, planted-foot drift, the skirt against the seat or the
    floor. Returns a dict of worst values."""
    scene = bpy.context.scene
    U = fig.U
    r = dict(low=1e9, drift=0.0, seat_n=0, seat_min=1e9, floor_skirt=1e9, garment_floor=1e9,
             hearth_n=0, hearth_depth=0.0)
    hz = hearth_zones()
    contacts, co0 = None, None
    frames_list = list(range(1, frames + 2, step))
    if frames_list[-1] != frames + 1:
        frames_list.append(frames + 1)
    for f in frames_list:
        scene.frame_set(f)
        co = evaluated_coords(body, fig.arm)
        body_ids = range(BODY_VERTS) if is_grandma else range(len(co))
        r["low"] = min(r["low"], min(co[i].dot(U) for i in body_ids))
        if is_grandma:
            r["garment_floor"] = min(r["garment_floor"], min(co[i].dot(U) for i in range(BODY_VERTS, len(co))))
        if "planted" in checks:
            if contacts is None:
                contacts, co0 = lowest_contacts(fig, body)
            r["drift"] = max([r["drift"]] + [(co[i] - co0[i]).length for i in contacts])
        if is_grandma and skirt_ids:
            if "seat" in checks and ctx.seat:
                st = ctx.seat
                n = 0
                for i in skirt_ids:
                    ff, ll, uu = fig.comp(co[i])
                    if st["back"] <= ff <= st["edge"] and abs(ll) < st["half"]:
                        r["seat_min"] = min(r["seat_min"], uu)
                        if uu < st["top"]:
                            n += 1
                r["seat_n"] = max(r["seat_n"], n)
            r["floor_skirt"] = min(r["floor_skirt"], min(co[i].dot(U) for i in skirt_ids))
        if "stove" in checks:
            front, top = STOVE_M / GRANDMA_SCALE, COOKTOP_M / GRANDMA_SCALE
            r["stove_n"] = max(r.get("stove_n", 0), sum(1 for c in co if fig.comp(c)[0] > front and fig.comp(c)[2] < top))
        if "hearth" in checks:
            depths = [in_hearth(hz, *fig.comp(c)[0::2]) for c in co]
            r["hearth_n"] = max(r["hearth_n"], sum(1 for d in depths if d > 0.0))
            r["hearth_depth"] = max([r["hearth_depth"]] + depths)
    return r


# ----------------------------------------------------------------------------- render


def setup_render(res=(360, 520)):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = False
    # Unity's Standard shader culls back faces, so the renders do too: a face turned the wrong
    # way, or the inside of a single sheet, shows here as a hole, as it will in game.
    scene.display.shading.show_backface_culling = True
    scene.display.shading.show_cavity = False
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("clips_bg")
    scene.world.color = (0.80, 0.80, 0.82)
    scene.render.image_settings.file_format = "PNG"
    cam_data = bpy.data.cameras.new("clips_cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("clips_cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def make_floor(size=16.0, grid=0.25):
    """A floor striped every `grid` BU along world Y, so a planted foot visibly keeps its stripe."""
    import bmesh
    me = bpy.data.meshes.new("clips_floor")
    bm = bmesh.new()
    n = int(size / grid)
    for i in range(n):
        y0 = -size / 2 + i * grid
        vs = [bm.verts.new(c) for c in ((-4, y0, 0), (4, y0, 0), (4, y0 + grid, 0), (-4, y0 + grid, 0))]
        bm.faces.new(vs).material_index = i % 2
    bm.to_mesh(me)
    ob = bpy.data.objects.new("clips_floor", me)
    for c in ((0.60, 0.62, 0.56, 1.0), (0.45, 0.47, 0.42, 1.0)):
        m = bpy.data.materials.new("clips_floor_mat")
        m.diffuse_color = c
        me.materials.append(m)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def make_label():
    cu = bpy.data.curves.new("clips_label", type="FONT")
    cu.body = ""
    ob = bpy.data.objects.new("clips_label", cu)
    m = bpy.data.materials.new("clips_label_mat")
    m.diffuse_color = (0.05, 0.05, 0.05, 1.0)
    cu.materials.append(m)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def aim(cam, loc, target):
    cam.location = loc
    cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()


def put_label(lab, cam, text, dist=1.3):
    upd()
    m = cam.matrix_world.to_3x3()
    right_v, up_v, back_v = m.col[0], m.col[1], m.col[2]
    r = bpy.context.scene.render
    w, h = r.resolution_x, r.resolution_y
    half = dist * 0.5 * cam.data.sensor_width / cam.data.lens
    hh, hw = (half, half * w / h) if h >= w else (half * h / w, half)
    lab.location = cam.location - back_v * dist - right_v * hw * 0.94 + up_v * hh * 0.88
    lab.rotation_euler = cam.rotation_euler
    lab.data.size = hh * 0.065
    lab.data.body = text


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def sheet(paths, out, cols):
    imgs = [bpy.data.images.load(p) for p in paths]
    w, h = imgs[0].size
    rows = (len(imgs) + cols - 1) // cols
    canvas = np.ones((rows * h, cols * w, 4), dtype=np.float32)
    for i, im in enumerate(imgs):
        px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
        r, c = divmod(i, cols)
        y0 = (rows - 1 - r) * h
        canvas[y0:y0 + h, c * w:(c + 1) * w] = px
        canvas[y0:y0 + h, c * w:c * w + 2] = (0.2, 0.2, 0.2, 1.0)
        canvas[y0:y0 + 2, c * w:(c + 1) * w] = (0.2, 0.2, 0.2, 1.0)
    o = bpy.data.images.new("clips_sheet", cols * w, rows * h, alpha=True)
    o.pixels = canvas.ravel()
    o.filepath_raw = out
    o.file_format = "PNG"
    o.save()
    for im in imgs:
        bpy.data.images.remove(im)
    bpy.data.images.remove(o)
    for p in paths:
        try:
            os.remove(p)
        except OSError:
            pass


def strobe(paths, out):
    """All frames of a fixed camera in one image, darkest pixel wins: a planted shoe prints as one
    shoe, a sliding one smears."""
    imgs = [bpy.data.images.load(p) for p in paths]
    w, h = imgs[0].size
    acc = None
    for im in imgs:
        px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
        acc = px if acc is None else np.minimum(acc, px)
    o = bpy.data.images.new("clips_strobe", w, h, alpha=True)
    o.pixels = acc.ravel()
    o.filepath_raw = out
    o.file_format = "PNG"
    o.save()
    for im in imgs:
        bpy.data.images.remove(im)
    bpy.data.images.remove(o)
    for p in paths:
        try:
            os.remove(p)
        except OSError:
            pass


def import_prop(rel, scale_m):
    """A kit model, converted from metres into her units (1 m = 1/0.616 BU), hidden."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, rel))
    new = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.new("prop_" + os.path.basename(rel), None)
    bpy.context.scene.collection.objects.link(root)
    for o in new:
        if o.parent is None:
            o.parent = root
    root.scale = (scale_m / GRANDMA_SCALE,) * 3
    for o in new + [root]:
        o.hide_render = True
    return root, new


def show(objs, flag):
    for o in objs:
        o.hide_render = not flag


# ----------------------------------------------------------------------------- verify


def verify_exports(out_dir, crew_src, grandma_src, specs, log):
    """Re-import every clip FBX in a fresh scene and compare it with both rigs."""
    rigs = {}
    for tag, src in (("male01_1", crew_src), ("SM_Grandma", grandma_src)):
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=src)
        a = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        rigs[tag] = {b.name: (b.head_local.copy(), b.tail_local.copy(), b.parent.name if b.parent else None)
                     for b in a.data.bones}
    ok = True
    for name, spec in specs:
        path = os.path.join(out_dir, name + ".fbx")
        if not os.path.exists(path):
            log("VERIFY {}: MISSING".format(name))
            ok = False
            continue
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        act = arm.animation_data.action if arm.animation_data else None
        names = {b.name for b in arm.data.bones}
        row = "VERIFY {:<18} bones {}".format(name, len(names))
        for tag, rest in rigs.items():
            missing = sorted(set(rest) - names)
            extra = sorted(names - set(rest))
            common = names & set(rest)
            dh = max(max((arm.data.bones[n].head_local - rest[n][0]).length,
                         (arm.data.bones[n].tail_local - rest[n][1]).length) for n in common)
            par = all((arm.data.bones[n].parent.name if arm.data.bones[n].parent else None) == rest[n][2]
                      for n in common)
            row += " | vs {}: missing {} extra {} parents {} rest delta {:.5f}".format(
                tag, len(missing), len(extra), "ok" if par else "DIFFER", dh)
            ok = ok and not missing and not extra and par and dh < 1e-4
        fcs = all_fcurves(act) if act else []
        fr = tuple(act.frame_range) if act else (0, 0)
        take = act.name.split("|")[-1] if act else None
        row += " | take '{}' frames {:.0f}..{:.0f} curves {}".format(take, fr[0], fr[1], len(fcs))
        ok = ok and take == name
        if spec["loop"]:
            worst = max(abs(fc.evaluate(fr[0]) - fc.evaluate(fr[1])) for fc in fcs)
            row += " loop first-vs-last {:.1e}".format(worst)
            ok = ok and worst < 1e-4
        log(row)
    log("VERIFY {}".format("ALL OK" if ok else "FAILED"))
    return ok


# -------------------------------------------------------------------------------- main


def main():
    args = parse_args()
    lines = []

    def log(s):
        print(s)
        lines.append(s)

    wanted = [c[0] for c in CLIPS] if args.clips == "all" else args.clips.split(",")
    bpy.ops.wm.read_homefile(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    arm_c, body_c = import_body(args.crew, "crew")
    arm_g, body_g = import_body(args.grandma, "grandma")
    fig_c, fig_g = Figure(arm_c), Figure(arm_g)
    fig_g.m_per_bu = GRANDMA_SCALE
    ctx = Ctx()
    ctx.seat = None
    ctx.bodies = {id(fig_c): body_c, id(fig_g): body_g}   # fit_floor measures the skinned mesh
    ctx.crew_half = torso_half_width(fig_c, body_c, 1.70, 1.90)
    ctx.g_half = torso_half_width(fig_g, body_g, 1.70, 1.90)
    ctx.g_hip_half = torso_half_width(fig_g, body_g, 1.40, 1.55)
    mouth = measure_face([fig_c, fig_g], body_c)
    log("axes: up {} forward {} left {}".format(tuple(fig_c.U), tuple(fig_c.F), tuple(fig_c.L)))
    log("torso half width at elbow height: crew {:.3f} BU, grandma (cardigan) {:.3f} BU; her hip half "
        "width {:.3f} BU; mouth {}".format(ctx.crew_half, ctx.g_half, ctx.g_hip_half,
                                           tuple(round(c, 3) for c in fig_c.comp(mouth))))
    fit_sit(ctx, fig_g, body_g, log)
    kh, beta = fit_kneel(ctx, fig_g, body_g, log)
    sit = ctx.sit
    log("seat: top {:.3f} m, her lowest body point at {:.3f} m; seated hip joints {:.3f} BU = {:.3f} m high, "
        "over the root (A6 placement); seat front edge {:.3f} BU = {:.3f} m ahead of the root; she stands "
        "{:.3f} BU = {:.3f} m ahead of the root at the start of Sit_Down and the end of Stand_Up; "
        "shoe toe-down {:.1f} deg, shin forward {:.1f} deg".format(
            SEAT_M, SEAT_M - SINK_M, sit["hz"], sit["hz"] * GRANDMA_SCALE, ctx.seat["edge"],
            ctx.seat["edge"] * GRANDMA_SCALE, SIT_BACK, SIT_BACK * GRANDMA_SCALE, -sit["pitch"]["L"], sit["shin"]["L"]))
    log("kneel: knee joint {:.3f} BU above the floor and {:.3f} BU = {:.3f} m ahead of the root, shins rising "
        "{:.1f} deg, thighs {:.0f} deg forward, hip joints {:.3f} BU = {:.3f} m high".format(
            kh, KNEE_F, KNEE_F * GRANDMA_SCALE, beta, KNEEL_THETA, ctx.kneel["hip"].dot(fig_g.U),
            ctx.kneel["hip"].dot(fig_g.U) * GRANDMA_SCALE))
    # The hearth is where GRANDMA's Spot_LightFire puts it (decision 4). The match must reach the
    # logs; the standing shoe tips must stay off the hearth slab.
    hz = hearth_zones()
    ctx.hearth_edge = hz["edge"]
    fig_g.apply(clip_kneel_fire(ctx, fig_g)["pose"](0.5))
    tip = fig_g.pb["hand.R"].tail
    tf, tl, tu = fig_g.comp(tip)
    fig_g.apply(g_stand(ctx, fig_g))
    toes = max(fig_g.comp(c)[0] for c in evaluated_coords(body_g, arm_g))
    log("hearth: slab front edge {:.3f} BU = {:.2f} m ahead of the root, logs' front face {:.3f} BU = {:.3f} m; "
        "at mid Kneel_LightFire her right fingertips are {:.3f} BU = {:.3f} m ahead and {:.3f} m high, "
        "{:.3f} m short of the logs (the match spans the gap); standing shoe tips {:.3f} BU = {:.3f} m, "
        "{:.3f} m clear of the slab".format(
            hz["edge"], hz["edge"] * GRANDMA_SCALE, hz["logs"], hz["logs"] * GRANDMA_SCALE, tf,
            tf * GRANDMA_SCALE, tu * GRANDMA_SCALE, (hz["logs"] - tf) * GRANDMA_SCALE, toes,
            toes * GRANDMA_SCALE, (hz["edge"] - toes) * GRANDMA_SCALE))
    fig_g.reset()
    vm = vert_materials(body_g)
    skirt_ids =[i for i in range(BODY_VERTS, len(body_g.data.vertices)) if vm[i] == {"grandma_dress"}]
    log("grandma: {} vertices, skirt {} ({}..{})".format(len(body_g.data.vertices), len(skirt_ids),
                                                        min(skirt_ids), max(skirt_ids)))

    renders = args.renders
    cam = lab = None
    props = {}
    if renders:
        os.makedirs(renders, exist_ok=True)
        cam = setup_render()
        make_floor()
        lab = make_label()
        props["chair"] = import_prop(r"GrandmaKit\Furniture\SM_Rocking_Chair.fbx", 1.0)
        props["fire"] = import_prop(r"PierreKit_Ext\PKX_Fireplace.fbx", 1.5)
        props["stove"] = import_prop(r"GrandmaKit\Kitchen\SM_Stove_Old.fbx", 1.0)
        props.update(make_hand_props())
    mw = arm_c.matrix_world.to_3x3()
    wf = (mw @ fig_c.F).normalized()
    wr = (mw @ fig_c.R).normalized()
    wu = (mw @ fig_c.U).normalized()
    to_w = lambda v: arm_c.matrix_world @ v

    summary = []
    specs = []
    for name, builder in CLIPS:
        if name not in wanted:
            continue
        fig = fig_c if builder is not None else None
        spec_c = builder(ctx, fig_c)
        author = spec_c["author"]
        fig = fig_c if author == "crew" else fig_g
        spec = builder(ctx, fig) if fig is fig_g else spec_c
        specs.append((name, spec))
        frames, loop = spec["frames"], spec["loop"]
        gait = spec.get("gait")
        if gait is not None:
            drop, reach = fit_drop(gait, frames)
            pose_fn = gait.pose
        else:
            pose_fn = (lambda sp, fg: (lambda t: fg.apply(sp["pose"](t))))(spec, fig)
        for o in (arm_c, arm_g):
            if o.animation_data:
                o.animation_data.action = None
        fig_c.reset()
        fig_g.reset()
        action = bake(fig, name, frames, loop, pose_fn, spec.get("shift", 0.0))
        reach = gait.max_reach if gait is not None else fig.max_reach
        other = arm_g if fig is fig_c else arm_c
        size = export_clip(fig.arm, [other], action, name, os.path.join(args.out_dir, name + ".fbx"))
        assign_action(other, action)
        mism = check_loop(action, frames) if loop else float("nan")
        log("")
        log("{} ({}): frames 1..{} = {:.3f} s, {}, authored on the {} rig, worst leg reach {:.3f}, "
            "FBX {} bytes{}".format(name, author, frames + 1, frames / FPS, "loop" if loop else "one-shot",
                                    "crew" if author == "crew" else "grandma (same 36 bones)", reach, size,
                                    ", loop mismatch {:.1e}".format(mism) if loop else ""))
        # joints
        worst_off, maxb = 0.0, {}
        for fr in range(1, frames + 2, 2):
            scene.frame_set(fr)
            w_, b_ = joint_report(fig)
            worst_off = max(worst_off, w_)
            for k, v in b_.items():
                maxb[k] = max(maxb.get(k, 0.0), v)
        log("  joints: off-hinge rotation of forearms and shins {:.3f} deg (Humanoid drops it); max bend "
            "elbow {:.0f}, knee {:.0f}, thigh from vertical {:.0f} deg".format(
                worst_off, max(maxb["forearm.L"], maxb["forearm.R"]), max(maxb["shin.L"], maxb["shin.R"]),
                max(maxb["thigh_flex.L"], maxb["thigh_flex.R"])))
        row = dict(name=name, frames=frames, loop=loop, author=author, off=worst_off, reach=reach)
        if gait is not None:
            body = body_c if author == "crew" else body_g
            res, minh = measure_gait(fig, body, gait, frames, log)
            sc = CREW_SCALE if author == "crew" else GRANDMA_SCALE
            v_design = gait.p["stride"] / (gait.p["duty"] * frames / FPS)
            speeds = [r_[1] for r_ in res]
            v = sum(speeds) / len(speeds)
            log("  hips dropped {:.3f} BU; planted speed designed {:.3f} BU/s, measured {:.3f} BU/s on "
                "the shoe vertices".format(gait.drop, v_design, v))
            for (s, k), spd, dev, hmax in res:
                log("    {} {} pivot: backward {:.3f} BU/s, off a straight line {:.4f} BU, off the floor "
                    "{:.4f} BU".format(s, k, spd, dev, hmax))
            log("  native ground speed: crew {:.2f} m/s, grandma {:.2f} m/s; lowest shoe point {:+.4f} BU".format(
                v * CREW_SCALE, v * GRANDMA_SCALE, minh))
            row.update(speed_crew=v * CREW_SCALE, speed_g=v * GRANDMA_SCALE,
                       slide=max(r_[2] for r_ in res), lift=max(r_[3] for r_ in res), minh=minh)
        step = 1 if frames <= 60 else 2
        bodies = (("crew", fig_c, body_c, False), ("grandma", fig_g, body_g, True))
        if spec.get("crew_only"):
            bodies = bodies[:1]           # the crew's actions: the grandmother never plays them
        for tag, fg, body, is_g in bodies:
            r = measure_clip(fg, body, frames, spec["checks"] + (["seat"] if name in ("Sit_Down", "Stand_Up") else []),
                             ctx, is_g, skirt_ids, step)
            txt = "  on {}: lowest body point {:+.4f} BU".format(tag, r["low"])
            if "planted" in spec["checks"]:
                txt += ", planted shoe contacts drift {:.4f} BU".format(r["drift"])
            if is_g:
                txt += ", lowest garment point {:+.4f} BU".format(r["garment_floor"])
                if ctx.seat and ("seat" in spec["checks"] or name in ("Sit_Down", "Stand_Up")):
                    txt += "; skirt over the seat: {} vertices below the seat top ({:.3f} BU), lowest {:.4f} BU = " \
                           "{:.3f} m".format(r["seat_n"], ctx.seat["top"], r["seat_min"], r["seat_min"] * GRANDMA_SCALE)
                if "floor_skirt" in spec["checks"]:
                    txt += "; lowest skirt point {:+.4f} BU".format(r["floor_skirt"])
            if "hearth" in spec["checks"]:
                txt += "; inside the fireplace: {} vertices, deepest {:.4f} BU".format(r["hearth_n"], r["hearth_depth"])
            if "stove" in spec["checks"]:
                txt += "; inside the stove (past its front, under its cooktop): {} vertices".format(r.get("stove_n", 0))
            log(txt)
            row[tag] = r
        for gf, kind in spec.get("grips", []):
            scene.frame_set(gf)
            log("  " + grip_report(fig_c, kind, gf))
        summary.append(row)

        if renders:
            render_clip(name, spec, fig, arm_c, arm_g, cam, lab, props, renders, ctx, wf, wr, wu, log)

    if args.report:
        with open(args.report, "w") as fh:
            fh.write("\n".join(lines) + "\n")
    if args.verify:
        vlines = []

        def vlog(s):
            print(s)
            vlines.append(s)
        verify_exports(args.out_dir, args.crew, args.grandma, specs, vlog)
        if args.report:
            with open(args.report, "a") as fh:
                fh.write("\n" + "\n".join(vlines) + "\n")
    print("AUTHOR_CLIPS_DONE")


def render_clip(name, spec, fig, arm_c, arm_g, cam, lab, props, renders, ctx, wf, wr, wu, log):
    scene = bpy.context.scene
    tags = {"crew": [o for o in bpy.data.objects if o.get("tag") == "crew"],
            "grandma": [o for o in bpy.data.objects if o.get("tag") == "grandma"]}
    for root, objs in props.values():
        show(objs, False)
    kind = spec.get("props")
    centre = Vector((0.0, 0.0, 1.25))
    yaw, dist = 35.0, 5.2
    if spec.get("gait") is not None:
        yaw = 90.0
    if kind == "chair":
        root, objs = props["chair"]
        k = 1.0 / GRANDMA_SCALE
        front = 0.246                                   # chair seat front edge, chair y (m)
        root.location = wf * (ctx.seat["edge"] - front * k)
        show(objs, True)
        centre = wf * (SIT_BACK - 0.30) + Vector((0, 0, 1.05))
        yaw = 55.0
    elif kind == "fire":
        root, objs = props["fire"]
        root.location = wf * (ctx.hearth_edge + FIREPLACE_ORIGIN_M / GRANDMA_SCALE)
        show(objs, True)
        centre = wf * 0.65 + Vector((0, 0, 0.80))
        yaw = 95.0                                      # her right side: the reach, the slab, the logs
    elif kind == "stove":
        root, objs = props["stove"]
        root.location = wf * ((STOVE_M + STOVE_ORIGIN_M) / GRANDMA_SCALE)
        show(objs, True)
        centre = wf * 0.55 + Vector((0, 0, 1.30))
        yaw = 70.0
    view = spec.get("view")
    if view == "upper":                                 # the hands at the face: chest and head
        centre, dist = Vector((0.0, 0.0, 2.05)), 3.0
    elif view == "floor":                               # a fall: the whole body, low
        centre, dist = Vector((0.0, 0.0, 0.85)), 6.2
    if spec.get("yaw") is not None:
        yaw = spec["yaw"]
    # The crew's own actions render on the crew only, from two sides (the second row is the
    # first turned 70 degrees), since the grandmother never plays them.
    rows = [("crew", yaw), ("crew", yaw + 70.0)] if spec.get("crew_only") else [("crew", yaw), ("grandma", yaw)]
    held = spec.get("hand_prop")
    paths = []
    for tag, row_yaw in rows:
        show(tags["crew"], tag == "crew")
        show(tags["grandma"], tag == "grandma")
        for f in spec["keys"]:
            scene.frame_set(f)
            place_hand_prop(props, held, fig if tag == spec["author"] else None, fig.arm)
            a = math.radians(row_yaw)
            d = wf * math.cos(a) + wr * math.sin(a)
            aim(cam, centre + d * dist + Vector((0, 0, 0.45)), centre)
            put_label(lab, cam, "{} f{} {}".format(name, f, tag))
            p = os.path.join(renders, "_tmp_{}_{}_{}_{}.png".format(name, tag, int(row_yaw), f))
            render(p)
            paths.append(p)
    place_hand_prop(props, None, None, None)
    sheet(paths, os.path.join(renders, "{}_sheet.png".format(name)), len(spec["keys"]))
    if spec.get("gait") is not None:
        gait = spec["gait"]
        tag = spec["author"]
        arm = arm_c if tag == "crew" else arm_g
        show(tags["crew"], tag == "crew")
        show(tags["grandma"], tag == "grandma")
        frames = spec["frames"]
        v = gait.p["stride"] / (gait.p["duty"] * frames / FPS)
        D = gait.p["duty"]
        first = 1 + int(round(0.5 * frames))
        last = first + int(math.floor(D * frames))
        loc0 = arm.location.copy()
        res = scene.render.resolution_x, scene.render.resolution_y
        scene.render.resolution_x, scene.render.resolution_y = 1000, 480
        target = Vector((0, 0, 0.55)) + wf * (v * ((first + last) * 0.5 - 1) / FPS)
        aim(cam, target + wr * 6.2 + Vector((0, 0, 0.35)), target)
        put_label(lab, cam, "{}: root moved {:.2f} BU/s, right-foot stance frames {}..{}".format(name, v, first, last))
        paths = []
        for f in range(first, last + 1):
            scene.frame_set(1 + (f - 1) % frames)
            arm.location = loc0 + wf * (v * (f - 1) / FPS)
            upd()
            p = os.path.join(renders, "_tmp_{}_track_{}.png".format(name, f))
            render(p)
            paths.append(p)
        arm.location = loc0
        strobe(paths, os.path.join(renders, "{}_rootmotion_strobe.png".format(name)))
        scene.render.resolution_x, scene.render.resolution_y = res
    show(tags["crew"], True)
    show(tags["grandma"], True)


if __name__ == "__main__":
    main()
