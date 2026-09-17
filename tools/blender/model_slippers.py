"""Model grandmother's slippers onto the crew rig, and export a left and a right.

Why this exists: the first lot of stealable clothes (05_ART/CHARACTERS.md) is slippers,
glasses and a dressing gown. The glasses already exist in the character pack. These do not.

Two decisions are baked in here and both are measured, not taste:

1. **The slipper swallows the boot.** The crew body ships a boot as its own material slot,
   and splitting the base mesh to hide it would cost a mesh edit plus a second material
   swap on every one of the four crew variants. Measured, the boot is 0.139 x 0.377 x 0.153
   in rig units. The slipper is built around that box with a margin, so nothing pokes out.

2. **Left and right are two real meshes, not one mirrored by code.** Negating a scale axis
   in Unity flips the winding and the piece lights inside out. A mirror here is free.

The origin of each slipper is moved to its foot bone head, so CrewEquip can place it with an
offset of zero and the numbers in the builder stay readable.

Run:

    blender --background --python tools/blender/model_slippers.py

Optional, after a bare --:  --out <folder>  --preview <png>  --blend <path>
"""

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector

SRC = r"C:\dev\game-project\UnityProject\Assets\Floreswa\Models\male01_1.fbx"
OUT = r"C:\dev\game-project\UnityProject\Assets\_Project\Art\Crew"
BLEND = r"C:\dev\game-project\_ArtSource\Crew_Outfit.blend"
PREVIEW = r"C:\dev\game-project\_ArtSource\preview_slippers.png"

# Measured off the shoes submesh of male01_1, left foot, rig units (character is 2.588 tall).
BOOT = {"min": Vector((0.2206, -0.3048, 0.0)), "max": Vector((0.3592, 0.0726, 0.1532))}

# How far the slipper stands off the boot. Forward gets most: the nose tapers to a point, so
# the padding there is spent on the taper rather than on clearance. 0.022 left the tip of the
# boot 7 percent outside the shell, measured vertex by vertex.
PAD_X, PAD_FRONT, PAD_BACK, PAD_TOP = 0.017, 0.038, 0.016, 0.016

# Cross section shape. 2 is a true half ellipse, which pinches at the top corners and let the
# top inner edge of the boot through. 3 is a rounded box, which holds its corners and happens
# to read as a puffier slipper.
SHAPE = 3.0

PINK = (0.93, 0.55, 0.70, 1.0)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, preview, blend = OUT, PREVIEW, BLEND
    for i, a in enumerate(argv):
        if a == "--out" and i + 1 < len(argv):
            out = argv[i + 1]
        elif a == "--preview" and i + 1 < len(argv):
            preview = argv[i + 1]
        elif a == "--blend" and i + 1 < len(argv):
            blend = argv[i + 1]
    return out, preview, blend


def import_body():
    bpy.ops.wm.read_homefile(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=SRC)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=SRC)
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    return mesh, arm


def pink_material():
    m = bpy.data.materials.new("slipper_pink")
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = PINK
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.9
    m.diffuse_color = PINK
    return m


def build_slipper(name, mat):
    """One slipper on the left foot: a puffy form that swallows the boot, with a padded cuff.

    The cuff is what makes it read as a slipper rather than a shoe cover, and it is part of
    the same swept surface rather than a ring stuck on top.
    """
    lo = Vector((BOOT["min"].x - PAD_X, BOOT["min"].y - PAD_FRONT, 0.0))
    hi = Vector((BOOT["max"].x + PAD_X, BOOT["max"].y + PAD_BACK, BOOT["max"].z + PAD_TOP))
    centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, 0.0))
    rad = Vector(((hi.x - lo.x) / 2, (hi.y - lo.y) / 2, hi.z))

    bm = bmesh.new()

    # Body: cross sections swept from toe to heel, each a half ellipse standing on the floor.
    #
    # The first attempt was one squashed sphere, and it failed for a reason worth keeping: an
    # ellipsoid loses height as fast as it loses width, so by the heel it was 62 mm tall
    # against a 153 mm boot and the black heel stuck straight out of the pink. A swept profile
    # holds full height from the instep back, which is also what a real slipper does.
    #
    # The padded cuff is the bump at t 0.80, not a separate ring. A torus laid round a domed
    # form only touches it at one height and floats everywhere else, which is exactly what the
    # first version did. A bulge in the profile is watertight and attached by construction.
    #
    # (t along the foot, half width, height), t = 0 at the toe.
    PROFILE = [
        (0.00, 0.20, 0.20),
        (0.10, 0.66, 0.58),
        (0.26, 0.93, 0.86),
        (0.46, 1.00, 1.00),
        (0.66, 1.00, 1.00),
        (0.74, 1.02, 1.00),
        (0.80, 1.18, 1.07),
        (0.86, 1.02, 1.00),
        (1.00, 0.86, 0.98),
    ]
    ARC = 7          # points across one section, first and last sitting on the floor

    sections = []
    for t, fw, fh in PROFILE:
        y = lo.y + (hi.y - lo.y) * t
        half_w, h = rad.x * fw, rad.z * fh
        ring = []
        for k in range(ARC):
            a = math.pi * k / (ARC - 1)
            ca, sa = math.cos(a), math.sin(a)
            px = math.copysign(abs(ca) ** (2.0 / SHAPE), ca)
            pz = abs(sa) ** (2.0 / SHAPE)
            ring.append(bm.verts.new((centre.x + half_w * px, y, h * pz)))
        sections.append(ring)
    bm.verts.index_update()

    for i in range(len(sections) - 1):
        a, b = sections[i], sections[i + 1]
        for k in range(ARC - 1):
            bm.faces.new((a[k], b[k], b[k + 1], a[k + 1]))
        bm.faces.new((a[0], a[ARC - 1], b[ARC - 1], b[0]))       # the sole
    bm.faces.new(list(reversed(sections[0])))                     # toe cap
    bm.faces.new(sections[-1])                                    # heel cap

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = False

    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def mirror_to_right(left, name):
    """A real mirrored mesh, with its winding put back the right way round."""
    right = left.copy()
    right.data = left.data.copy()
    right.name = name
    right.data.name = name
    bpy.context.scene.collection.objects.link(right)

    for v in right.data.vertices:
        v.co.x = -v.co.x
    for p in right.data.polygons:
        vs = list(p.vertices)
        vs.reverse()
        for k, vi in enumerate(vs):
            right.data.loops[p.loop_start + k].vertex_index = vi
    right.data.update()
    return right


def set_origin(obj, point):
    """Move the origin to a point without moving the geometry in the world."""
    delta = Vector(point)
    for v in obj.data.vertices:
        v.co -= delta
    obj.location = delta
    obj.data.update()


def export(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # bake_space_transform=True, and the nuance matters. 05_ART/CHARACTERS.md says to leave it
    # off, and that rule is about SKINNED pieces, where it is known to wreck armatures. These
    # are rigid props, and with it off the Blender to Unity axis conversion is not baked into
    # the mesh: it goes into the imported root rotation instead, the mesh arrives with the foot
    # running along Y, and anything that sets a world rotation on the root lays the slipper on
    # its back. On is what the static kit already does.
    #
    # apply_unit_scale=True matters and cost a round trip to find. author_carry_clip.py passes
    # False and says the unit flags change nothing, which is true for an animation because a
    # humanoid clip retargets through the avatar and never carries a scale. For a mesh it is
    # not true: the values go out in metres inside a file whose header declares centimetres,
    # Unity applies its own 0.01, and the slipper arrives 1 mm long. Measured, not reasoned.
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True,
            global_scale=1.0, apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            bake_space_transform=True,
            add_leaf_bones=False, bake_anim=False,
            axis_forward="-Z", axis_up="Y",
        )
    except Exception:
        bpy.ops.wm.fbx_export(filepath=path, export_selected_objects=True)


def render_preview(path, arm):
    """A flat-shaded look at the right foot, to check the slipper covers the boot."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 1000, 700
    scene.render.film_transparent = False
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"

    foot = arm.data.bones["foot.L"].head_local
    target = Vector((foot.x, foot.y - 0.10, 0.09))

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = target + Vector((0.55, -0.62, 0.34))
    direction = target - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 70
    scene.camera = cam

    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    out, preview, blend = parse_args()
    body, arm = import_body()

    mat = pink_material()
    left = build_slipper("SM_Crew_Feet_Slipper_L", mat)
    right = mirror_to_right(left, "SM_Crew_Feet_Slipper_R")

    tris = sum(len(p.vertices) - 2 for p in left.data.polygons)
    print(f"SLIPPER_TRIS {tris}")
    print("SLIPPER_BOUNDS " + " ".join(f"{v:.4f}" for v in left.dimensions))

    render_preview(preview, arm)

    set_origin(left, arm.data.bones["foot.L"].head_local)
    set_origin(right, arm.data.bones["foot.R"].head_local)

    export(left, os.path.join(out, "SM_Crew_Feet_Slipper_L.fbx"))
    export(right, os.path.join(out, "SM_Crew_Feet_Slipper_R.fbx"))

    os.makedirs(os.path.dirname(blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    print("SLIPPER_DONE " + out)


if __name__ == "__main__":
    main()
