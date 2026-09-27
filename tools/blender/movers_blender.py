"""Shared Blender helpers for The Movers' asset scripts (decisions/ADR-011-asset-workflow.md).

One module, not a package. A generator next to it imports it with:

    import os, sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import movers_blender as mb

What is here is what the bathrobe pilot used, and nothing more:

  repo_path, profile   paths from the repository root, so a script runs on any checkout;
                       05_ART/style/profile.json, read once
  import_body          the crew body every worn piece is modelled on
  material, palette    a flat colour, given in sRGB as Unity shows it
  export_rigid         the FBX presets of the profile. They run in Object mode and fail
  export_skinned       loudly: no fallback to wm.fbx_export, which writes centimetres and
  export_animation     lands a mesh 100 times off
  render_turnaround    the review renders: Standard view transform, back faces culled, the
                       body's silhouette with and without the piece, the piece at 8 m
  contact_sheet        images side by side, for the verdict
  check_profile        every reference glob still finds a file (run this file on its own)

Colour is the one subtle thing. Unity is in Gamma space and the models import with
useSRGBMaterialColor on, so Unity shows the number in the FBX as it is: the robe's
(0.95, 0.58, 0.72) is #F294B8 on screen. Blender reads the same number as linear and renders
through AgX by default, which is why the pilot's first review renders showed a mauve. So
material() writes the sRGB value unchanged into the Principled base colour, the one the FBX
exporter writes, and its linear conversion into the viewport colour, the one Workbench draws.
Rendered through the Standard view transform, a review then shows what Unity shows. Material
Preview and Cycles read the base colour, and show it too light.

Run anything that uses this with --python-exit-code 1: without it blender -b exits 0 when the
script raises, and a refused export looks like a success.

    blender -b --factory-startup --python-exit-code 1 --python tools/blender/movers_blender.py
"""

import fnmatch
import glob
import json
import math
import os
import subprocess

import bpy
from mathutils import Vector

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(REPO, "UnityProject", "Assets")
PROFILE_PATH = os.path.join(REPO, "05_ART", "style", "profile.json")
BODY_FBX = os.path.join(ASSETS, "Floreswa", "Models", "male01_1.fbx")
PLAYER_HEIGHT_M = 1.80   # the CharacterController, and the crew body once imported
TIERS = ("canonical", "acceptable", "legacy", "do_not_copy", "anti")


def repo_path(*parts):
    return os.path.join(REPO, *parts)


def _rel(path):
    try:
        return os.path.relpath(path, REPO)
    except ValueError:      # another drive
        return path


# ------------------------------------------------------------------------ profile

_profile = None


def profile():
    """05_ART/style/profile.json, loaded once."""
    global _profile
    if _profile is None:
        with open(PROFILE_PATH, encoding="utf-8") as f:
            _profile = json.load(f)
    return _profile


def family(name):
    families = profile()["families"]
    if name not in families:
        raise KeyError(f"no family {name!r} in {PROFILE_PATH}; known: {', '.join(families)}")
    return families[name]


def bu_per_metre(family_name):
    """Blender units per metre the player sees, from the family's mannequin height."""
    return family(family_name)["mannequin_bu"] / PLAYER_HEIGHT_M


def palette(name):
    """A palette colour as (r, g, b, a), sRGB as Unity shows it."""
    colours = profile()["palette"]
    if name not in colours:
        raise KeyError(f"no colour {name!r} in the palette of {PROFILE_PATH}")
    c = colours[name]
    r, g, b = c["srgb"]
    return (r, g, b, c.get("alpha", 1.0))


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


# -------------------------------------------------------------------------- scene


def import_body(path=BODY_FBX):
    """An empty scene holding the crew body. Returns (mesh, armature).

    Blender 5's own importer (wm.fbx_import) is tried first and the Python one second, as the
    garment scripts always did; the IMPORT line says which one ran. The body's own colours get
    the treatment material() gives ours: Unity shows its FBX values as they are, so Workbench
    gets their linear conversion. The body is never exported, so no file changes with it.
    """
    bpy.ops.wm.read_homefile(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
        used = "wm.fbx_import"
    except Exception as e:
        print(f"IMPORT wm.fbx_import failed ({e}), trying import_scene.fbx")
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        used = "import_scene.fbx"
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for m in {s.material for s in mesh.material_slots if s.material}:
        bsdf = m.node_tree.nodes.get("Principled BSDF") if m.node_tree else None
        c = bsdf.inputs["Base Color"].default_value if bsdf else m.diffuse_color
        m.diffuse_color = (srgb_to_linear(c[0]), srgb_to_linear(c[1]), srgb_to_linear(c[2]), c[3])
    print(f"IMPORT {_rel(path)} with {used}: mesh {mesh.name}, armature {arm.name}")
    return mesh, arm


def material(name, srgb, roughness=0.9):
    """A flat material Unity shows as `srgb`, and a Standard Workbench render too.

    `srgb` is (r, g, b) or (r, g, b, a), for example palette("garment_pink"). The base colour
    gets it unchanged, because the FBX carries the base colour and Unity shows it as it is;
    the viewport colour gets its linear conversion, because Workbench draws that one.
    """
    r, g, b = srgb[0], srgb[1], srgb[2]
    a = srgb[3] if len(srgb) > 3 else 1.0
    m = bpy.data.materials.new(name)
    if m.node_tree is None:     # Blender 4 starts a material without nodes; 5 has them
        m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (r, g, b, a)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
    m.diffuse_color = (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), a)
    return m


# ------------------------------------------------------------------------- export


def _preset(name):
    presets = profile()["export_presets"]
    if name not in presets:
        raise KeyError(f"no export preset {name!r} in {PROFILE_PATH}")
    kw = dict(presets[name]["fbx"])
    if "object_types" in kw:
        kw["object_types"] = set(kw["object_types"])
    return kw


def _export(objs, path, preset):
    """Select exactly `objs`, export them with a preset, and prove a file was written."""
    objs = list(objs)
    if not objs:
        raise ValueError("nothing to export to " + path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    active = bpy.context.view_layer.objects.active
    if active is not None and active.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    before = os.stat(path).st_mtime_ns if os.path.exists(path) else None
    result = bpy.ops.export_scene.fbx(filepath=path, use_selection=True, **_preset(preset))
    if "FINISHED" not in result:
        raise RuntimeError(f"FBX export of {path} returned {result}")
    if not os.path.isfile(path) or os.path.getsize(path) == 0:
        raise RuntimeError(f"FBX export wrote nothing to {path}")
    if before is not None and os.stat(path).st_mtime_ns == before:
        raise RuntimeError(f"FBX export left {path} as it was")
    print(f"EXPORTED {preset} {path} ({os.path.getsize(path)} bytes)")
    return path


def export_rigid(objs, path):
    """A rigid piece: the axis conversion is baked into the mesh (export_presets.rigid)."""
    return _export(objs, path, "rigid")


def export_skinned(objs, path):
    """A skinned piece and its armature: the axis conversion stays on the imported root."""
    return _export(objs, path, "skinned")


def export_animation(arm, path, take=None):
    """An armature's action as a humanoid clip, the armature alone.

    Blender names the FBX take after the scene, so `take` renames the scene for the export and
    puts its name back afterwards.
    """
    scene = bpy.context.scene
    old = scene.name
    if take:
        scene.name = take
    try:
        return _export([arm], path, "animation")
    finally:
        scene.name = old


# ------------------------------------------------------------------------- review


def review_scene():
    """Workbench as a review uses it: the profile's view transform (Standard, never AgX),
    studio light, material colours, back faces culled as Unity culls them, so a face pointing
    the wrong way disappears here rather than in the editor.

    It gives the scene a world if it has none, and the FBX exporter copies a world's colour
    into every material's AmbientColor. A script whose FBX must not change renders after
    exporting; the bathrobe always rendered first, so its FBX carries the review grey.
    """
    rv = profile()["review"]
    scene = bpy.context.scene
    scene.render.engine = rv["engine"]
    scene.view_settings.view_transform = rv["view_transform"]
    scene.view_settings.look = "None"
    sh = scene.display.shading
    sh.light = rv["light"]
    sh.color_type = "MATERIAL"
    sh.show_backface_culling = rv["backface_culling"]
    sh.show_shadows = False
    sh.show_cavity = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.color = rv["world_color"]
    return scene


def _camera(scene, name="C"):
    cam = scene.objects.get(name)
    if cam is None or cam.type != "CAMERA":
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def _aim(cam, target, deg, distance, height):
    a = math.radians(deg)
    cam.location = target + Vector((math.cos(a) * distance, math.sin(a) * distance, height))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()


def _render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED " + path)
    return path


def _dark_pixels(path):
    import numpy as np
    img = bpy.data.images.load(path, check_existing=False)
    try:
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        return int((px.reshape(w * h, 4)[:, 0] < 0.5).sum())
    finally:
        bpy.data.images.remove(img)


def render_turnaround(folder, prefix, piece=None, body=None, family_name="garments",
                      target=(0.0, 0.0, 1.45), distance=None, height=0.30, lens=56,
                      review_folder=None):
    """The review renders of a piece. Returns {name: path}.

    Into `folder`, the profile's four views: <prefix>_1_front.png and so on. The character's
    front is +Y in Blender world, because the imported object carries a rotation, so 90
    degrees faces it.

    Into `review_folder` (default <folder>/review), when `piece` (one object or a list) and
    `body` are given:
      <prefix>_sil_body_<view>.png and <prefix>_sil_piece_<view>.png: the body alone, then
      wearing the piece, as flat black shapes. The SILHOUETTE line says how much outline the
      piece adds: a piece that adds none is a texture, not equipment (CHARACTERS.md).
      <prefix>_8m.png: seen from 8 m through the player camera's field of view, the distance
      at which another player has to read it.

    `distance` is in Blender units; left out, it is the profile's near distance converted with
    the family's mannequin. `target` is where the camera looks, in Blender world.
    """
    rv = profile()["review"]
    scene = review_scene()
    bu = bu_per_metre(family_name)
    near_m, far_m = rv["distances_m"]
    distance = near_m * bu if distance is None else distance
    target = Vector(target)
    w, h = rv["resolution"]
    scene.render.resolution_x, scene.render.resolution_y = w, h
    cam = _camera(scene)
    cam.data.lens = lens

    os.makedirs(folder, exist_ok=True)
    out = {}
    for name, deg in rv["views_deg"].items():
        _aim(cam, target, deg, distance, height)
        out[name] = _render(scene, os.path.join(folder, f"{prefix}_{name}.png"))

    if piece is None or body is None:
        return out
    pieces = list(piece) if isinstance(piece, (list, tuple)) else [piece]
    review_folder = review_folder or os.path.join(folder, "review")
    os.makedirs(review_folder, exist_ok=True)

    sh = scene.display.shading
    if rv.get("silhouette", True):
        saved = (sh.light, sh.color_type, tuple(sh.single_color), tuple(scene.world.color))
        sh.light, sh.color_type, sh.single_color = "FLAT", "SINGLE", (0.0, 0.0, 0.0)
        scene.world.color = (1.0, 1.0, 1.0)
        for name in rv["silhouette_views"]:
            _aim(cam, target, rv["views_deg"][name], distance, height)
            for p in pieces:
                p.hide_render = True
            alone = _render(scene, os.path.join(review_folder, f"{prefix}_sil_body_{name}.png"))
            for p in pieces:
                p.hide_render = False
            worn = _render(scene, os.path.join(review_folder, f"{prefix}_sil_piece_{name}.png"))
            a, b = _dark_pixels(alone), _dark_pixels(worn)
            print(f"SILHOUETTE {prefix} {name}: body {a} px, with the piece {b} px, "
                  f"outline grows {100.0 * (b - a) / max(a, 1):.0f} %")
            out["sil_body_" + name], out["sil_piece_" + name] = alone, worn
        sh.light, sh.color_type, sh.single_color = saved[0], saved[1], saved[2]
        scene.world.color = saved[3]

    fw, fh = rv["far_resolution"]
    scene.render.resolution_x, scene.render.resolution_y = fw, fh
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(rv["far_fov_deg"])
    _aim(cam, target, rv["views_deg"]["1_front"] + rv["far_yaw_deg"], far_m * bu, height)
    out["8m"] = _render(scene, os.path.join(review_folder, f"{prefix}_8m.png"))
    cam.data.sensor_fit = "AUTO"
    cam.data.lens = lens
    scene.render.resolution_x, scene.render.resolution_y = w, h
    return out


def contact_sheet(rows, out_path, tile_h=540, gap=12):
    """Rows of images side by side, every tile scaled to one height, for the verdict: the
    piece next to its references and its anti reference. WebP reads too."""
    import numpy as np

    def tile(path):
        img = bpy.data.images.load(path, check_existing=False)
        try:
            w, h = img.size
            nw = max(1, round(w * tile_h / h))
            img.scale(nw, tile_h)
            px = np.empty(nw * tile_h * 4, dtype=np.float32)
            img.pixels.foreach_get(px)
            return px.reshape(tile_h, nw, 4)[::-1]    # Blender rows run bottom up
        finally:
            bpy.data.images.remove(img)

    built = []
    for row in rows:
        tiles = [tile(p) for p in row]
        width = sum(t.shape[1] for t in tiles) + gap * (len(tiles) - 1)
        canvas = np.ones((tile_h, width, 4), dtype=np.float32)
        x = 0
        for t in tiles:
            canvas[:, x:x + t.shape[1]] = t
            x += t.shape[1] + gap
        built.append(canvas)
    width = max(r.shape[1] for r in built)
    height = tile_h * len(built) + gap * (len(built) - 1)
    sheet = np.ones((height, width, 4), dtype=np.float32)
    y = 0
    for r in built:
        sheet[y:y + tile_h, :r.shape[1]] = r
        y += tile_h + gap
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img = bpy.data.images.new("contact_sheet", width, height, alpha=False, float_buffer=False)
    img.pixels.foreach_set(np.ascontiguousarray(sheet[::-1]).ravel())
    img.filepath_raw = out_path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("SHEET " + out_path)
    return out_path


# ----------------------------------------------------------------------- register


def _git_files(rev):
    run = subprocess.run(["git", "-C", REPO, "ls-tree", "-r", "--name-only", rev],
                         capture_output=True, text=True)
    if run.returncode != 0:
        raise RuntimeError(f"git ls-tree {rev}: {run.stderr.strip()}")
    return run.stdout.splitlines()


def check_profile():
    """Check the register and the palette against the repository. Raises on any problem.

    Every reference glob must still find a file (a 'rev' entry in its commit), every tier and
    family must be known, and every palette hex must match its sRGB value the way Unity rounds
    it. It prints how many files each entry governs once later entries have taken theirs:
    the last matching entry wins.
    """
    p = profile()
    problems = []
    for name, c in p["palette"].items():
        hx = "#" + "".join(f"{int(round(v * 255)):02X}" for v in c["srgb"])
        if hx != c["hex"]:
            problems.append(f"palette {name}: srgb gives {hx}, hex says {c['hex']}")

    owner = {}
    matched = []
    for i, r in enumerate(p["references"]):
        if r["tier"] not in TIERS:
            problems.append(f"reference {r['glob']}: unknown tier {r['tier']!r}")
        if r["family"] != "*" and r["family"] not in p["families"]:
            problems.append(f"reference {r['glob']}: unknown family {r['family']!r}")
        if r.get("approved_by") and not r.get("date"):
            problems.append(f"reference {r['glob']}: approved without a date")
        if "rev" in r:
            hits = [f for f in _git_files(r["rev"]) if fnmatch.fnmatchcase(f, r["glob"])]
        else:
            hits = [_rel(f).replace(os.sep, "/")
                    for f in glob.glob(os.path.join(REPO, r["glob"]), recursive=True)]
            for f in hits:
                owner[f] = i
        matched.append(hits)
        if not hits:
            problems.append(f"reference {r['glob']}" + (f" @ {r['rev']}" if "rev" in r else "")
                            + ": no file")

    governs = {}
    for i in owner.values():
        governs[i] = governs.get(i, 0) + 1
    for i, r in enumerate(p["references"]):
        n = len(matched[i]) if "rev" in r else governs.get(i, 0)
        where = f" @ {r['rev']}" if "rev" in r else ""
        status = "approved " + r["approved_by"] if r.get("approved_by") else "proposed"
        print(f"REF {r['tier']:<11} {n:>4} of {len(matched[i]):>4} files  {r['glob']}{where}  ({status})")
    for line in problems:
        print("PROFILE_FAIL " + line)
    if problems:
        raise RuntimeError(f"{len(problems)} problems in {PROFILE_PATH}")
    print(f"PROFILE_OK {len(p['palette'])} colours, {len(p['families'])} families, "
          f"{len(p['references'])} reference entries")


if __name__ == "__main__":
    check_profile()
