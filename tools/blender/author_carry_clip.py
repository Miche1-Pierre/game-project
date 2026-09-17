"""
Author a carry pose and a looping idle clip on a rigged character, for The Movers.

Why this exists: the Floreswa Low Poly Character Pack ships three rigged bodies and
zero animations, and the one pose this game needs is "holding something in front of
you". That pose is not in a generic locomotion library either. Authoring it costs a
minute here and nothing afterwards.

Run headless, no GUI and no MCP addon needed:

    blender --background --python tools/blender/author_carry_clip.py

Arguments go after a bare `--`:

    blender --background --python tools/blender/author_carry_clip.py -- \
        --src  ".../Floreswa/Models/male02_1.fbx" \
        --out  ".../Generated/Characters/Anim_Carry_Idle_male02.fbx" \
        --preview "C:/tmp/pose.png"

Validated 2026-09-17 on male01_1.fbx (933 verts, 36-bone Rigify metarig, 11 materials).
The exported clip imports into Unity as Humanoid and retargets onto the character.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Matrix

ROOT = r"C:\dev\game-project\UnityProject\Assets"
DEFAULT_SRC = os.path.join(ROOT, r"Floreswa\Models\male01_1.fbx")
DEFAULT_OUT = os.path.join(ROOT, r"_Movers\Generated\Characters\Anim_Carry_Idle.fbx")

FPS = 24
LAST_FRAME = 48          # 2 seconds. Frame 49 repeats frame 1 so the clip loops.

# Bones the clip writes. Everything else keeps its rest pose, which is what makes the
# clip cheap to blend with a future locomotion layer.
KEYED = ["upper_arm.L", "forearm.L", "upper_arm.R", "forearm.R", "spine.002"]


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="author_carry_clip")
    p.add_argument("--src", default=DEFAULT_SRC)
    p.add_argument("--out", default=DEFAULT_OUT)
    p.add_argument("--preview", default=None, help="write a PNG of the pose here")
    return p.parse_args(argv)


def import_source(path):
    bpy.ops.wm.read_homefile(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=path)
    return next(o for o in bpy.data.objects if o.type == "ARMATURE")


def rig_axes(arm):
    """Derive up / forward / right from the rig itself.

    Do not assume Y-up or Z-up. Blender's FBX importer converts axes, the exporter
    converts them back, and packs disagree about which way a character faces. Reading
    the answer off the bones costs three lines and makes this work on any rig that uses
    Rigify metarig names.
    """
    pb = arm.pose.bones
    hips = pb["spine"].head
    head_top = pb["spine.005"].tail
    up = (head_top - hips).normalized()
    toe = pb["toe.L"].tail - pb["toe.L"].head
    forward = (toe - up * toe.dot(up)).normalized()
    return up, forward, forward.cross(up).normalized()


def point_bone(arm, name, target_dir):
    """Rotate a pose bone so it points along target_dir, in armature space."""
    pb = arm.pose.bones[name]
    cur = (pb.tail - pb.head).normalized()
    q = cur.rotation_difference(target_dir.normalized())
    head = pb.head.copy()
    pb.matrix = (Matrix.Translation(head) @ q.to_matrix().to_4x4()
                 @ Matrix.Translation(-head) @ pb.matrix)
    bpy.context.view_layer.update()


def carry_pose(arm, axes, lift=0.0):
    """Upper arms down and forward, forearms level, as if holding a crate.

    `lift` is the breath: 0 at rest, 1 at the top of the cycle. It moves the arms a few
    centimetres rather than bobbing the whole body, because a mover under load does not
    bounce.
    """
    up, fwd, right = axes
    for side, s in (("L", 1.0), ("R", -1.0)):
        point_bone(arm, "upper_arm." + side,
                   -up * (0.74 - lift * 0.10) + fwd * (0.58 + lift * 0.06) + right * (0.30 * s))
        point_bone(arm, "forearm." + side,
                   fwd * 0.95 + up * (0.18 + lift * 0.10) + right * (-0.12 * s))


def all_fcurves(action):
    """Blender 5 moved F-Curves into slotted actions: layers -> strips -> channelbags.

    `action.fcurves` no longer exists and the AttributeError does not hint at the
    replacement. Handles both shapes so the script survives a Blender downgrade.
    """
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    out = []
    for layer in action.layers:
        for strip in layer.strips:
            for cb in getattr(strip, "channelbags", []):
                out.extend(cb.fcurves)
    return out


def build_action(arm, axes):
    arm.animation_data_create()
    action = bpy.data.actions.new("Carry_Idle")
    arm.animation_data.action = action
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"

    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 1, LAST_FRAME
    scene.render.fps = FPS

    up, fwd, right = axes
    for frame, lift in ((1, 0.0), (LAST_FRAME // 2 + 1, 1.0), (LAST_FRAME + 1, 0.0)):
        scene.frame_set(frame)
        bpy.ops.pose.select_all(action="SELECT")
        bpy.ops.pose.transforms_clear()
        carry_pose(arm, axes, lift)

        chest = arm.pose.bones["spine.002"]
        chest.rotation_quaternion = (
            Matrix.Rotation(math.radians(-3.0), 4, right).to_quaternion()
            if lift else (1.0, 0.0, 0.0, 0.0))

        for name in KEYED:
            arm.pose.bones[name].keyframe_insert("rotation_quaternion", frame=frame)

    for fc in all_fcurves(action):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
    return action


def render_preview(arm, axes, path):
    """Workbench, so it renders on a machine with no GPU and no scene lighting."""
    up, fwd, right = axes
    scene = bpy.context.scene
    scene.frame_set(LAST_FRAME // 2 + 1)
    bpy.ops.object.mode_set(mode="OBJECT")

    centre = arm.pose.bones["spine"].head + up * 0.45
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.location = centre + right * 3.4 + up * 0.2
    cam.rotation_euler = (centre - cam.location).normalized().to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = scene.render.resolution_y = 700
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def export(path):
    """Export armature plus mesh as FBX, in metres.

    The legacy exporter is tried FIRST, for the reason documented in
    generate_variants.py: Blender 5's wm.fbx_export writes centimetre units, Unity then
    applies its own 0.01 conversion, and the mesh lands 100x off. export_scene.fbx takes
    explicit unit flags. Measured on this pack the unit flags change nothing, the source
    is already in metres, so they are a precaution against the next pack rather than a
    fix for this one.

    Known and unresolved: importing this file into Unity with "Copy From Other Avatar"
    against the source character fails, leg bone positions differ by up to 400 mm. It is
    not a unit problem, the exported bone lengths match the source. The clip is therefore
    imported with CreateFromThisModel and retargets through the humanoid layer, which is
    the normal workflow and works. Anyone who later needs an exact skeleton match should
    start by re-importing the source with Ignore Leaf Bones, which the pack description
    asks for and this script does not do.
    """
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True,
            global_scale=1.0, apply_unit_scale=False,
            apply_scale_options="FBX_SCALE_NONE",
            add_leaf_bones=False,
            bake_anim=True, bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
            axis_forward="-Z", axis_up="Y",
        )
    except Exception:
        bpy.ops.wm.fbx_export(filepath=path, export_selected_objects=True)


def main():
    args = parse_args()
    arm = import_source(args.src)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")

    axes = rig_axes(arm)
    action = build_action(arm, axes)

    if args.preview:
        render_preview(arm, axes, args.preview)

    export(args.out)
    print("exported {}: action '{}', {} curves, {} bytes".format(
        args.out, action.name, len(all_fcurves(action)), os.path.getsize(args.out)))
    print("NOTE: Blender names the FBX take after the scene, not after the action. "
          "Rename the clip to Carry_Idle in Unity and tick Loop Time.")


if __name__ == "__main__":
    main()
