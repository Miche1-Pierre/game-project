"""
Generate mesh variants from one source model, for The Movers greybox.

Why this exists: a purchased pack gives you one mesh per object. A moving game needs
several crates that read as different objects. Buying more packs is the expensive answer.
Deriving variants from a mesh you already own is the cheap one.

Run headless, no GUI needed:

    blender --background --online-mode --command blender_mcp --port 9876
    (then drive it over MCP)

or directly:

    blender --background --python tools/blender/generate_variants.py

Validated 2026-09-17 on BrokenVector/LowPolyDungeon/Models/Furniture/Chest.fbx
(218 verts, 173 faces, 3 materials, 1 UV map). Four variants produced in under a second.
"""

import bpy
import bmesh
import os
import random
from mathutils import Vector

SRC = r"C:\dev\game-project\UnityProject\Assets\BrokenVector\LowPolyDungeon\Models\Furniture\Chest.fbx"
OUT = r"C:\dev\game-project\UnityProject\Assets\_Movers\Generated"

# The FBX in this pack keeps Y as the up axis once imported. Check before reusing
# this script on another pack: a Z-up source needs the axis indices below swapped.
UP = 1


def clear():
    bpy.ops.wm.read_homefile(use_empty=True)


def import_source(path):
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o.type == "MESH"][0]


def duplicate(base, name):
    o = base.copy()
    o.data = base.data.copy()
    o.name = name
    bpy.context.scene.collection.objects.link(o)
    return o


def select_only(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def bake_scale(o, scale):
    """Proportional variant. Scale is applied into the mesh, not left on the transform,
    so Unity receives real geometry rather than a stretched instance."""
    o.scale = scale
    select_only(o)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return o


def erode(o, face_ratio, sag, jitter_base, jitter_top):
    """Remove a share of faces, then deform what is left, more at the top than the base.

    Deleting faces beats a boolean here: these game meshes are not watertight, and an
    EXACT boolean on a non-manifold mesh leaks cutter geometry into the result.
    Face deletion works on any topology and never changes the bounding box by surprise.
    """
    me = o.data
    bm = bmesh.new()
    bm.from_mesh(me)

    if face_ratio > 0:
        faces = list(bm.faces)
        random.shuffle(faces)
        victims = faces[: max(1, int(len(faces) * face_ratio))]
        bmesh.ops.delete(bm, geom=victims, context="FACES")

    bm.verts.ensure_lookup_table()
    heights = [v.co[UP] for v in bm.verts]
    lo, hi = min(heights), max(heights)
    span = max(1e-6, hi - lo)

    for v in bm.verts:
        t = (v.co[UP] - lo) / span          # 0 at the floor, 1 at the top
        v.co[UP] -= sag * t * t
        amp = jitter_base + (jitter_top - jitter_base) * t
        for axis in (0, 1, 2):
            if axis != UP:
                v.co[axis] += random.uniform(-1, 1) * amp

    bm.to_mesh(me)
    bm.free()
    me.update()
    return o


def bevel(o, width, segments=1, angle=0.55):
    select_only(o)
    m = o.modifiers.new("Bevel", "BEVEL")
    m.width = width
    m.segments = segments
    m.limit_method = "ANGLE"
    m.angle_limit = angle
    bpy.ops.object.modifier_apply(modifier=m.name)
    return o


def export(o, path):
    """Export one object as FBX, in metres.

    The legacy exporter is tried FIRST on purpose. Blender 5 ships wm.fbx_export, which
    succeeds but writes centimetre units; Unity then applies its own 0.01 conversion and
    the mesh lands 100x too small. export_scene.fbx takes explicit unit flags, so it is
    the one that produces a mesh Unity reads at its real size.
    """
    select_only(o)
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True,
            global_scale=1.0, apply_unit_scale=False,
            apply_scale_options="FBX_SCALE_NONE",
            axis_forward="-Z", axis_up="Y",
        )
    except Exception:
        bpy.ops.wm.fbx_export(filepath=path, export_selected_objects=True)


def main():
    random.seed(23)
    clear()
    os.makedirs(OUT, exist_ok=True)
    base = import_source(SRC)

    variants = []

    # Proportional: same object, different silhouette. Zero authoring cost.
    variants.append(bake_scale(duplicate(base, base.name + "_Wide"), (1.35, 0.85, 1.10)))
    variants.append(bake_scale(duplicate(base, base.name + "_Tall"), (0.80, 1.60, 0.90)))

    # Worn: must be visible at gameplay distance. Micro-noise is invisible, so this
    # sags the top and drops a few planks to change the silhouette itself.
    worn = duplicate(base, base.name + "_Worn")
    bevel(worn, 0.018)
    erode(worn, face_ratio=0.05, sag=0.035, jitter_base=0.006, jitter_top=0.026)
    variants.append(worn)

    # Broken: feeds MovableObject.broken, which today only tints the object grey.
    # A real mesh turns an invisible state change into a readable consequence.
    broken = duplicate(base, base.name + "_Broken")
    erode(broken, face_ratio=0.18, sag=0.0, jitter_base=0.012, jitter_top=0.057)
    variants.append(broken)

    for v in variants:
        export(v, os.path.join(OUT, v.name + ".fbx"))
        print("exported {}: {} faces".format(v.name, len(v.data.polygons)))


if __name__ == "__main__":
    main()
