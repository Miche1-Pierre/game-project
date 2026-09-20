"""Model grandmother's dressing gown on the crew rig, skinned to the body armature.

The third piece of the first lot (05_ART/CHARACTERS.md), and the only skinned one. Slippers
and glasses are rigid meshes parented to a bone; a gown crosses the shoulders, the elbows and
the hips, so it has to deform with the body.

The method is the cheap one and it is deliberate: the gown is **derived from the body mesh**
rather than modelled beside it. Duplicate the body, keep the band from mid thigh to shoulder,
cut the front open, push the surface out along its normals, give it thickness. Because every
vertex is a copy of a body vertex, it arrives carrying the body's own vertex groups, so the
skinning is exact and free. No automatic weights, no weight painting, no armature to rebuild.

Two consequences worth knowing:

1. It deforms exactly like the body, which is the point, and it can never clip through the
   torso because it is the torso pushed outward.
2. It is open at the front so the crew identity colour stays visible down the middle. That is
   a hard rule in the brief, not a style choice: four players in a corridor have to stay apart.

Run:

    blender --background --python tools/blender/model_gown.py
"""

import bpy
import bmesh
import os
import sys
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

SRC = r"C:\dev\game-project\UnityProject\Assets\Floreswa\Models\male01_1.fbx"
OUT = r"C:\dev\game-project\UnityProject\Assets\_Project\Art\Crew"
BLEND = r"C:\dev\game-project\_ArtSource\Crew_Outfit_Gown.blend"
PREVIEW = r"C:\dev\game-project\_ArtSource\preview_gown.png"

# Rig units, character 2.588 tall. Bone heights measured off the metarig:
# thigh head 1.358, spine.002 head 1.729, spine.003 head 1.916, shoulder 2.122, head 2.236.
Z_LOW = 1.10        # mid thigh, so it reads as a gown rather than a jacket
Z_HIGH = 2.16       # just under the collar, the neck stays bare
ARM_X = 0.46        # keep the torso and a stub of upper arm: a rolled sleeve, not a full one
# The character faces -Y IN THE MESH LOCAL FRAME. Checked against the eyes and mouth submeshes,
# whose centroids sit at y -0.198 and -0.218. The imported object carries a rotation, so that
# same direction is +Y in world space: every cut below is local, and a render camera is not.
FRONT_Y = -0.05
# The opening is a V, narrow at the hem and wide at the collar, which is how a gown hangs when
# it is thrown on rather than tied. A constant 0.105 cut 20 faces out of 1164 on a body this
# low poly, which is a slit and not an opening.
OPEN_HEM = 0.07
OPEN_COLLAR = 0.19
PUFF = 0.035        # how far the surface is pushed off the body
THICK = 0.012       # solidify, so the open edges are not paper

PINK = (0.91, 0.47, 0.64, 1.0)


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
    m = bpy.data.materials.new("gown_pink")
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    if b:
        b.inputs["Base Color"].default_value = PINK
        if "Roughness" in b.inputs:
            b.inputs["Roughness"].default_value = 0.9
    m.diffuse_color = PINK
    return m


def build_gown(body, arm, mat):
    gown = body.copy()
    gown.data = body.data.copy()      # carries the vertex groups, which is the whole trick
    gown.name = "SM_Crew_Chest_Gown"
    gown.data.name = gown.name
    bpy.context.scene.collection.objects.link(gown)

    bm = bmesh.new()
    bm.from_mesh(gown.data)
    bm.faces.ensure_lookup_table()

    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        if c.z < Z_LOW or c.z > Z_HIGH:
            doomed.append(f)
        elif abs(c.x) > ARM_X:
            doomed.append(f)
        else:
            t = (c.z - Z_LOW) / max(1e-6, Z_HIGH - Z_LOW)
            if c.y < FRONT_Y and abs(c.x) < OPEN_HEM + (OPEN_COLLAR - OPEN_HEM) * t:
                doomed.append(f)      # the V down the front
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")

    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * PUFF

    bm.to_mesh(gown.data)
    bm.free()

    gown.data.materials.clear()
    gown.data.materials.append(mat)
    for p in gown.data.polygons:
        p.use_smooth = False

    bpy.ops.object.select_all(action="DESELECT")
    gown.select_set(True)
    bpy.context.view_layer.objects.active = gown

    sol = gown.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = THICK
    sol.offset = 0.0
    bpy.ops.object.modifier_apply(modifier=sol.name)

    # The armature goes on after the thickness, so the deformer sees the finished surface.
    mod = gown.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    gown.parent = arm

    return gown


def export(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    # bake_space_transform stays OFF here, unlike the slippers: this one is skinned, and baking
    # the space transform is the documented way to wreck an armature. The axis conversion
    # therefore lands on the imported root, and CrewEquip composes with it instead of
    # overwriting it (05_ART/CHARACTERS.md).
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True,
        global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        add_leaf_bones=False, bake_anim=False,
        axis_forward="-Z", axis_up="Y",
    )


def render_preview(path, gown):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 900, 1100
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"

    target = Vector((0.0, 0.0, 1.65))
    cam_data = bpy.data.cameras.new("C")
    cam = bpy.data.objects.new("C", cam_data)
    scene.collection.objects.link(cam)
    cam_data.lens = 60
    # +Y, not -Y. The cuts above are authored in the mesh local frame where the face is at -Y,
    # but the imported object is rotated, so in world space the front is +Y. Aiming a camera
    # with the local number renders the back of the head and hides the whole opening.
    cam.location = target + Vector((-1.15, 2.30, 0.30))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    body, arm = import_body()
    gown = build_gown(body, arm, pink_material())

    tris = sum(len(p.vertices) - 2 for p in gown.data.polygons)
    print(f"GOWN_TRIS {tris}")
    print("GOWN_DIMS " + " ".join(f"{v:.4f}" for v in gown.dimensions))
    print(f"GOWN_GROUPS {len(gown.vertex_groups)}")

    render_preview(PREVIEW, gown)
    export([gown, arm], os.path.join(OUT, "SM_Crew_Chest_Gown.fbx"))

    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("GOWN_DONE")


if __name__ == "__main__":
    main()
