"""Render the interface kit: low-poly, cozy, moving-house sprites for the game's UI.

Pierre, 2026-09-26 (translated): "improve the interface to the max: texts, buttons, something
really nice, cozy, low poly, with the keys shown so we know what to do with each object ...
buttons with branches as borders or such things tied to moving house". Until now the HUD is
IMGUI text on black boxes. This script makes the pieces a real interface is built from, in the
spirit of Pierre's hand-made kit: wooden battens lashed with rope, branches with a few leaves,
cardboard and packing tape, a paper luggage tag, a tape measure for the bars, chunky key caps,
mouse and pad glyphs, bold icons, off-screen indicators, a compass strip and the title logo.

Every sprite is modelled here, in code, as low-poly geometry lying on the XY plane, lit by
one warm sun from the top left, and rendered straight down by an orthographic camera to a
PNG with alpha. Nothing is painted by hand, so a colour, a size or a border is one edit and
one run. The sprite names are fixed (the UI code loads them by name) and listed in register().

Ten decisions worth reading before changing anything:

1. **One Blender unit is one pixel at 1x, and the PNGs are 2x.** A sprite is specified by its
   1x canvas (for example 64 x 64 for a key cap) and built in those units; the render is
   SCALE (2) texture pixels per unit. The UI shows the art at half its pixel size on the 1080p
   reference (UiTheme.spriteSliceScale = 0.5, a 9-slice's `-unity-slice-scale`; a skin may use
   less, decision 9), so it stays sharp at 1440p and 4K. The importer therefore keeps Unity's default 100 pixels per unit: the 2x is
   the UI's slice scale, never both (sprites.json, "uiScale" and "pixelsPerUnit").

2. **The colours are Pierre's kit colours as the game shows them.** The project runs in Gamma
   colour space, where a material colour is displayed as written, so PK_wood (0.648, 0.500,
   0.369) is the sRGB colour #A57F5E on screen. PALETTE holds those values (read from
   Assets/_Project/Art/PierreKit/Materials/PK_*.mat) plus the warm colours the kit lacks
   (rope, cardboard, tape, paper). Base Color gets their linear equivalent, because Blender
   shades in linear and the Standard view transform writes sRGB back out. Text contrast was
   chosen with the UI's two ink colours in mind: frame_wood, frame_cardboard, frame_tag and
   key caps carry dark ink text, frame_wood_dark, the compass and the buttons carry cream text
   (sprites.json "text"). Measured on the rendered sprites: 5.5:1 or better everywhere except
   the button, 4.1:1 (3.6:1 hovered, 5.3:1 pressed) for its large bold label.

3. **EEVEE with one soft sun, not Workbench flat with outlines.** Both were rendered on the key
   cap, pad A and the box icon (`--compare`, kept as review/ui_style_compare.png) and looked at
   full size and downscaled to 48 and 32 px. Flat light drops the chamfers and the cap's front
   lip, which are the whole low-poly look: the Workbench key cap reads as a flat sticker, the
   EEVEE one as a key with a lit top and a darker lip, still at 32 px. The light is calibrated
   so a face turned to the camera shows about its palette colour: one sun (SUN_E, 32 degrees
   off the view axis, from the top left) plus a flat warm world.

4. **The outline and the drop shadow are image operations, not geometry.** After the render,
   the alpha is dilated by a soft disc into a dark-wood outline (ink) and blurred and offset
   into a shadow, then composited under the sprite with numpy. Every sprite gets the same
   sticker edge whatever its shape, which is what keeps it readable over a busy 3D scene. The
   canvas margin (Spec.pad) is sized to hold it.

5. **9-slice sprites keep every unique detail in their corners.** Lashings, nails, knots,
   leaf clusters, stamps, the tag's hole and string, the tape measure's case and hook sit
   inside the border values; what lies in the edges and the centre is uniform along the
   direction it will be stretched in (battens and grain run along their edge, the centre is
   one flat board). The border values are written to sprites.json in texture pixels, and the
   contact sheet re-imports every PNG and draws the 9-slice sprites stretched with those
   exact numbers, so a wrong border shows in the review image, not in Unity.

6. **Button states come from one render.** Normal, Hover, Pressed and Disabled are the same
   geometry treated four ways in post: Hover is lighter, lifted (longer shadow) and ringed in
   warm light; Pressed is darker, 2 px lower, with a short shadow; Disabled is desaturated,
   with a pale outline and no shadow. The four line up to the pixel, which a UI that swaps
   sprites on hover needs, and they share one set of border values.

7. **Letters are Fredoka, and only where they are part of the picture.** Key caps are blank:
   the UI draws the key's name in Fredoka, so one sprite serves every key and every language.
   The pad buttons (A, B, X, Y, LB ...) and the logo carry their letters, rendered from the
   Fredoka TTFs Pierre approved (SIL Open Font License, which allows rendering the font into
   images). The fonts are read from --font-dir; Blender's own Inter is the fallback so the
   script still runs before the fonts are in the project.

8. **Tintable sprites are white.** indicator_arrow, indicator_ring and compass_tick are drawn
   in white with neutral grey facets and the ink outline, so the UI multiplies them by a
   player's colour and the outline stays dark. bg_paper is the one sprite not rendered: it is
   seamless noise made with a periodic FFT filter, so it tiles with no seam by construction.

9. **An element fits when its borders fit and its content rect holds its child.** A 9-slice
   drawn smaller than its borders is squashed, corner art first (a tag's string, a key's
   lip, a tape measure's hub), so the corner details are packed tight: key caps slice down
   to 24 x 26 px, the luggage tag to 44 x 36, the button to 80 x 44, the cardboard to 56 x 56,
   all at slice scale 0.5 on the 1080p reference (sprites.json `minSize`). That is half of
   it. The content rect shrinks with the same slice scale, and the child must fit in it: at
   0.5 a 26 px key cap leaves 6 px of height for a 13 px letter, and a 78 x 84 pocket slot
   38 x 36 px for an icon and a caption. Both fit at UICORE's own scales: key caps at 0.22,
   about height / 128, the cap drawn whole; slots at 0.2. So a skin's scale is the largest
   at which both hold (UiArtMetrics.Slice.FitScale with the content size; Holds checks one).
   The content rect is where a block of text should sit; `face` is where the plain surface
   really ends, the same except on the cardboard's sides (plain from 9 px in, measured), so
   a one-word pocket caption padded 4 px at 0.2 is on plain cardboard, not on its outline.
   The tape bar and the compass strip stretch in width only ("stretch": "x"): their case and
   rope wraps span the height, so they are drawn `drawHeight` tall, or at slice scale =
   height / texture height. --cs writes the numbers as UiArtMetrics.cs. The contact sheet
   draws the kit at UICORE's sizes and scales with every child laid out in the content rect
   as UiSkin.Padding, UiKit.KeyFace and PocketsView do, then a 2x row outlining the content
   rect (the face at a pocket's sides), green where the child fits and red where it does not,
   so either failure shows there.

10. **Whole pictures get mipmaps, slices do not.** Glyphs, icons, indicators and the logo
   are 128 px (the logo 992) and are shown at 18 to 58 px (the logo at 300 to 500): one
   bilinear tap per pixel, with no mipmaps, drops the letters on LB/RB and the mouse's split
   line below about 28 px. They import with mipmaps (sprites.json "mipmaps": true). The
   9-slice art is drawn near its slice scale and stays sharper without. The contact sheet
   shows both at 18 to 32 px, computed the way the GPU samples (bilinear_tap, trilinear_tap)
   and pasted after the render, because EEVEE's own supersampling would hide the difference.

Run headless, no GUI and no MCP:

    blender -b --factory-startup --python tools/blender/render_ui_kit.py -- --out <dir>

Arguments after a bare `--`:
  --out DIR        where the PNGs and sprites.json go, by default the Unity folder
                   Assets/_Movers/UI/Sprites (pass a staging folder while the editor is busy)
  --font-dir DIR   the Fredoka TTFs, by default Assets/_Movers/UI/Fonts
  --only A,B       render only the sprites whose names start with one of these prefixes; the
                   manifest keeps the other entries
  --sheet PATH     also write the contact sheet (every sprite, a title and a split-screen HUD
                   mock-up, the kit at UICORE's sizes and scales, the content-rect check, the
                   9-slice sprites stretched at their UI size) to this PNG
  --sheet-only     rebuild the contact sheet from the PNGs already in --out, render nothing;
                   sprites.json keeps its sprite lines and gets this script's header
  --cs PATH        also write the 9-slice numbers as UiArtMetrics.cs (decision 9) to this path,
                   for the UI code (Assets/_Movers/Scripts/UI/Theme/UiArtMetrics.cs)
  --compare PATH   render the key cap, pad A and an icon in EEVEE and in Workbench side by
                   side, at full size and downscaled, to this PNG (decision 3), then stop
  --work DIR       raw renders before post-processing (default: the system temp folder)

Written 2026-09-26 against Blender 5.1.2 (EEVEE). Result of the full run: 55 PNGs (52 sprites, the
button in 4 states) and one sprites.json in about 16 s (the sheet about 15 s more), 2x resolution,
straight alpha; the logo trimmed to 992 x 320.
"""

import argparse
import json
import math
import os
import random
import struct
import sys
import tempfile
import time
import zlib

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = r"C:\GameProject"
DEFAULT_OUT = os.path.join(ROOT, r"UnityProject\Assets\_Movers\UI\Sprites")
DEFAULT_FONTS = os.path.join(ROOT, r"UnityProject\Assets\_Movers\UI\Fonts")
MANIFEST = "sprites.json"

SCALE = 2            # texture pixels per 1x pixel (decision 1)
UI_SCALE = 0.5       # screen pixels per texture pixel on the 1080p reference
PPU = 100            # Unity pixels per unit: the default, the 2x is the UI's slice scale
# The slice scales UICORE's skins draw the kit at (presentation/UICORE/Scripts/UI/Kit/UiSkins.cs,
# 2026-09-26 22:09). The contact sheet uses them to show the kit as the HUD shows it; update
# them with UiSkins.cs (decision 9).
UICORE_SCALE = {"KeyCap": 0.22, "Tag": 0.3, "TagBig": 0.36, "Slot": 0.2, "Cardboard": 0.4,
                "Button": 0.42, "TapeBg": 24.0 / 80.0, "WoodDark": 0.3, "Wood": 0.4, "Branch": 0.4}

# Light (decision 3). The sun comes from the top left and from the viewer.
SUN_DIR = (0.40, -0.48, -1.0)   # direction the light travels
SUN_E = 2.35                    # irradiance, W/m2
SUN_ANGLE = 12.0                # degrees: soft shadow edges
AMBIENT = 0.42                  # flat world radiance
AMBIENT_TINT = "#FFF1E0"        # warm

STYLE = "eevee"                 # or "workbench", only for --compare

# ----------------------------------------------------------------------------- palette
# sRGB, as the game shows them (decision 2). The PK_ entries are Pierre's kit materials.
PALETTE = {
    # Pierre's kit, Assets/_Project/Art/PierreKit/Materials
    "wood": "#A57F5E",          # PK_wood       (0.648, 0.500, 0.369)
    "wood_dark": "#71573F",     # PK_wood_001   (0.442, 0.341, 0.253)
    "wall": "#FFF3ED",          # PK_wall       (1.000, 0.955, 0.930)
    "grass": "#709952",         # PK_grass      (0.440, 0.600, 0.320)
    "brick": "#AD6060",         # PK_brique     (0.679, 0.375, 0.378)
    "stone": "#8B8E8E",         # PK_brique_001 (0.544, 0.555, 0.557)
    "metal": "#B6B6B6",         # PK_metal      (0.715, 0.715, 0.715)
    "glass": "#B8DEEB",         # PK_glass      (0.720, 0.870, 0.920)
    "black": "#0D0D0D",         # PK_Black_001  (0.050, 0.050, 0.050)
    # wood family, around PK_wood
    "wood_light": "#B88E69",
    "wood_pale": "#D2B08A",
    "wood_warm": "#A8744E",
    "pine": "#E6CDA3",          # frame_wood's board: dark ink text reads on it (8:1)
    "walnut": "#5A3F2B",        # frame_wood_dark's board: cream text reads on it (8:1)
    "plank": "#9C6B43",         # button face: cream bold text at 4.3:1
    "plank_side": "#734C2F",
    "bark": "#5E4330",
    "bark_dark": "#47321F",
    "cut_wood": "#E2C49A",      # the end grain of a sawn branch or log
    "honey": "#D39A5C",         # the logo's letters
    "honey_light": "#E2AE6E",
    "honey_dark": "#BD8047",
    # moving house
    "rope": "#D4B680",
    "rope_dark": "#B39462",
    "cardboard": "#C99A64",
    "cardboard_dark": "#A97C4B",
    "tape": "#DEC38C",          # packing tape, glossy
    "paper": "#F4E6CB",
    "paper_edge": "#E2CFAA",
    "ring": "#D6B585",          # tag reinforcement ring
    "tape_yellow": "#EDBE3C",   # tape measure
    "brass": "#CFA04A",
    "iron": "#6B6560",
    "rubber": "#4A3F37",
    "pad_body": "#554A43",
    # ink and accents
    "ink": "#3B2A1E",           # outline, print, letters: a darker PK_wood_001 (the UI's Ink)
    "cream": "#FFF7E8",         # the UI's Cream
    "key_top": "#F6EBDC",
    "key_side": "#D3BDA2",
    "accent": "#E8893A",        # highlighted mouse button, d-pad arm, arrows
    "leaf": "#709952",
    "leaf_dark": "#587E3F",
    "leaf_light": "#8DB866",
    "berry": "#C0443C",
    "gold": "#E6B84A",
    "gold_dark": "#B8862E",
    "note": "#86A860",
    "note_dark": "#5F8445",
    "skin": "#EEC6A6",
    "skin_hot": "#E9A58C",
    "hair": "#D6D6DB",          # the grandmother's hair (model_grandma.py)
    "cardigan": "#B59ECC",      # her cardigan (model_grandma.py)
    "cheek": "#E79A94",
    "denim": "#52739C",
    "denim_dark": "#435F83",
    "stitch": "#E6A650",
    "truck_body": "#F2EBDD",
    "truck_cab": "#C0504A",
    "tyre": "#3B3531",
    "beer_glass": "#7A3F16",
    "label": "#F2E6CF",
    "olive": "#5E6A33",         # the grenade, a touch lighter than GrenadeItem's olive
    "olive_dark": "#454E24",
    "ember": "#FF7A2A",
    "ash": "#9A948E",
    "smoke": "#DAD6D1",
    "filter": "#D98E4A",
    "sign_yellow": "#EDBD45",
    "ok_green": "#7FAE5A",
    "no_red": "#C4544A",
    # tintable sprites (decision 8): white tops, neutral grey sides
    "white": "#FFFFFF",
    "white_side": "#CFCFCF",
    # pad face buttons, Xbox colours warmed to the palette
    "pad_a": "#6FA14E",
    "pad_b": "#C4544A",
    "pad_x": "#4F86C0",
    "pad_y": "#DDA932",
    # grandmother mood badges, MoodTier order (the UI's Mood colours, muted)
    "mood_calm": "#7FB870",
    "mood_annoyed": "#E6B845",
    "mood_angry": "#E5853C",
    "mood_furious": "#CF4B3B",
    # post-processing
    "shadow": "#2A1C12",
    "glow": "#FFE3A0",
    "disabled_outline": "#7A6B5E",
}

INK = "ink"


def hex_rgb(c):
    """A palette key, a '#RRGGBB' string or an (r, g, b) tuple, as sRGB floats."""
    if isinstance(c, (tuple, list)):
        return tuple(c[:3])
    h = PALETTE.get(c, c).lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def to_linear(rgb):
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(c) for c in rgb)


def shade(c, k):
    """The colour c times k, in sRGB, clamped: a darker or lighter variant."""
    r, g, b = hex_rgb(c)
    return (min(r * k, 1.0), min(g * k, 1.0), min(b * k, 1.0))


def mix(a, b, t):
    ra, rb = hex_rgb(a), hex_rgb(b)
    return tuple(ra[i] + (rb[i] - ra[i]) * t for i in range(3))


# ----------------------------------------------------------------------------- materials

_MATS = {}


def principled(m):
    return next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)


def set_input(node, names, value):
    for n in names:
        if n in node.inputs:
            node.inputs[n].default_value = value
            return


def mat(colour, rough=0.85, spec=0.25, emit=0.0):
    """A cached Principled material. Base Color holds the linear equivalent of the sRGB
    palette colour (decision 2); emit > 0 adds emission of the same colour. The viewport
    colour holds the same linear value, for the Workbench comparison."""
    rgb = hex_rgb(colour)
    key = (tuple(round(c, 4) for c in rgb), rough, spec, emit)
    if key in _MATS:
        return _MATS[key]
    name = colour if isinstance(colour, str) else "rgb_%02x%02x%02x" % tuple(int(c * 255) for c in rgb)
    m = bpy.data.materials.new("UI_" + name.lstrip("#"))
    if m.node_tree is None:
        m.use_nodes = True
    b = principled(m)
    lin = to_linear(rgb)
    b.inputs["Base Color"].default_value = (*lin, 1.0)
    b.inputs["Roughness"].default_value = rough
    set_input(b, ("Specular IOR Level", "Specular"), spec)
    if emit > 0:
        set_input(b, ("Emission Color", "Emission"), (*lin, 1.0))
        set_input(b, ("Emission Strength",), emit)
    m.diffuse_color = (*lin, 1.0)
    _MATS[key] = m
    return m


# ----------------------------------------------------------------------------- 2D shapes
# Polygons are lists of 2D Vectors, counter-clockwise seen from +Z (the camera).


def V(x, y, z=None):
    return Vector((x, y)) if z is None else Vector((x, y, z))


def rrect(x0, y0, x1, y1, r, seg=2):
    """A rounded rectangle, CCW, with `seg` straight segments per corner (low poly)."""
    r = max(0.0, min(r, (x1 - x0) / 2 - 1e-3, (y1 - y0) / 2 - 1e-3))
    if r <= 1e-3:
        return [V(x0, y0), V(x1, y0), V(x1, y1), V(x0, y1)]
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90.0 * k / seg)
            pts.append(V(cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def ngon(cx, cy, r, n, phase=0.0, ry=None):
    ry = r if ry is None else ry
    return [V(cx + r * math.cos(phase + 2 * math.pi * k / n), cy + ry * math.sin(phase + 2 * math.pi * k / n))
            for k in range(n)]


def poly_area(pts):
    return 0.5 * sum(pts[i - 1].x * pts[i].y - pts[i].x * pts[i - 1].y for i in range(len(pts)))


def ccw(pts):
    return pts if poly_area(pts) > 0 else list(reversed(pts))


def inset(pts, d):
    """Offset a CCW polygon inwards by d (outwards when d < 0), mitred."""
    out = []
    n = len(pts)
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        e1 = (p1 - p0)
        e2 = (p2 - p1)
        if e1.length < 1e-6 or e2.length < 1e-6:
            out.append(p1.copy())
            continue
        e1.normalize()
        e2.normalize()
        n1 = V(-e1.y, e1.x)
        n2 = V(-e2.y, e2.x)
        k = 1.0 + n1.dot(n2)
        off = n1 * d if k < 1e-3 else (n1 + n2) * (d / k)
        if off.length > abs(d) * 3.0:        # very sharp corner: cap the mitre
            off = off.normalized() * abs(d) * 3.0
        out.append(p1 + off)
    return out


def clip(pts, a, b, c):
    """Sutherland-Hodgman: the part of the polygon where a*x + b*y + c >= 0."""
    out = []
    n = len(pts)
    for i in range(n):
        p, q = pts[i], pts[(i + 1) % n]
        fp, fq = a * p.x + b * p.y + c, a * q.x + b * q.y + c
        if fp >= 0:
            out.append(p)
        if (fp >= 0) != (fq >= 0):
            t = fp / (fp - fq)
            out.append(p + (q - p) * t)
    return out


def transform2(pts, dx=0.0, dy=0.0, rot=0.0, sx=1.0, sy=None):
    sy = sx if sy is None else sy
    c, s = math.cos(rot), math.sin(rot)
    return [V(dx + (p.x * sx) * c - (p.y * sy) * s, dy + (p.x * sx) * s + (p.y * sy) * c) for p in pts]


def arrow_poly(cx, cy, size, angle=90.0, shaft=0.36, head=0.55):
    """A block arrow of total length `size`, pointing at `angle` degrees."""
    L = size
    hw = size * head * 0.5
    sw = size * shaft * 0.5
    hl = size * 0.48
    pts = [V(-L / 2, -sw), V(L / 2 - hl, -sw), V(L / 2 - hl, -hw), V(L / 2, 0), V(L / 2 - hl, hw),
           V(L / 2 - hl, sw), V(-L / 2, sw)]
    return transform2(pts, cx, cy, math.radians(angle))


def triangle(cx, cy, size, angle=90.0):
    pts = [V(size * 0.55, 0), V(-size * 0.45, size * 0.5), V(-size * 0.45, -size * 0.5)]
    return transform2(pts, cx, cy, math.radians(angle))


# ----------------------------------------------------------------------------- 3D builders

COL = None           # the collection each sprite is built into


def link(ob):
    COL.objects.link(ob)
    return ob


def mesh_object(name, bm, mats, xf=None):
    if xf is not None:
        bmesh.ops.transform(bm, matrix=xf, verts=bm.verts)
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = False
    return link(bpy.data.objects.new(name, me))


def slab(pts, z0, z1, colour, chamfer=0.0, top=None, rough=0.85, spec=0.25, xf=None,
         tilt=None, name="slab", emit=0.0):
    """A low-poly extrusion of a CCW polygon from z0 to z1, its top edge chamfered.
    `top` gives the top face its own colour; `tilt` (dx, dy) slopes the top plane by that
    many units per unit of x and y, for hand-made unevenness."""
    pts = ccw([p.copy() for p in pts])
    side_m = mat(colour, rough, spec, emit)
    top_m = mat(top, rough, spec, emit) if top is not None else side_m
    bm = bmesh.new()
    n = len(pts)

    def zt(p, z):
        if tilt is None:
            return z
        return z + tilt[0] * p.x + tilt[1] * p.y

    ch = min(chamfer, (z1 - z0) * 0.9)
    lo = [bm.verts.new((p.x, p.y, z0)) for p in pts]
    if ch > 0:
        mid = [bm.verts.new((p.x, p.y, zt(p, z1 - ch))) for p in pts]
        tp = inset(pts, ch)
        hi = [bm.verts.new((p.x, p.y, zt(p, z1))) for p in tp]
    else:
        mid = None
        hi = [bm.verts.new((p.x, p.y, zt(p, z1))) for p in pts]
    ring = mid if mid else hi
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((lo[i], lo[j], ring[j], ring[i]))
        f.material_index = 0
    if mid:
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((mid[i], mid[j], hi[j], hi[i]))
            f.material_index = 1
    f = bm.faces.new(hi)
    f.material_index = 1
    f = bm.faces.new(list(reversed(lo)))
    f.material_index = 0
    return mesh_object(name, bm, [side_m, top_m], xf)


def band(outer, inner, z0, z1, colour, rough=0.85, spec=0.25, xf=None, name="band", emit=0.0):
    """A flat ring between two CCW loops with the same vertex count (stripes, rims)."""
    outer = ccw(outer)
    inner = ccw(inner)
    assert len(outer) == len(inner)
    m = mat(colour, rough, spec, emit)
    bm = bmesh.new()
    n = len(outer)
    ot = [bm.verts.new((p.x, p.y, z1)) for p in outer]
    it = [bm.verts.new((p.x, p.y, z1)) for p in inner]
    ob = [bm.verts.new((p.x, p.y, z0)) for p in outer]
    ib = [bm.verts.new((p.x, p.y, z0)) for p in inner]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ot[i], ot[j], it[j], it[i]))          # top
        bm.faces.new((ob[i], ob[j], ot[j], ot[i]))          # outer wall
        bm.faces.new((ib[j], ib[i], it[i], it[j]))          # inner wall
    return mesh_object(name, bm, [m], xf)


def resample(pts, closed, step):
    """Points every `step` along a polyline, and the length."""
    pts = [p.copy() for p in pts]
    if closed:
        pts = pts + [pts[0]]
    seg = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]
    L = sum(seg)
    n = max(2, int(round(L / step)))
    out = []
    count = n if closed else n + 1
    for i in range(count):
        s = L * i / n
        acc = 0.0
        for k, l in enumerate(seg):
            if acc + l >= s - 1e-9 or k == len(seg) - 1:
                t = 0.0 if l < 1e-9 else (s - acc) / l
                out.append(pts[k].lerp(pts[k + 1], min(max(t, 0.0), 1.0)))
                break
            acc += l
    return out, L


def tube(path, radius, colour, sides=6, closed=False, caps=True, cap_colour=None, normals=None,
         phase=None, rough=0.85, spec=0.25, xf=None, name="tube", emit=0.0):
    """A low-poly tube along a 3D polyline, radius per point or constant. Frames are
    parallel-transported from +Z (a ridge or a face turned to the camera), or given."""
    P = [Vector(p) if len(p) == 3 else Vector((p[0], p[1], 0.0)) for p in path]
    n = len(P)
    R = list(radius) if isinstance(radius, (list, tuple)) else [radius] * n
    T = []
    for i in range(n):
        if closed:
            a, b = P[i - 1], P[(i + 1) % n]
        else:
            a, b = P[max(i - 1, 0)], P[min(i + 1, n - 1)]
        t = b - a
        T.append(t.normalized() if t.length > 1e-9 else Vector((1, 0, 0)))
    frames = []
    if normals is not None:
        for i in range(n):
            N = Vector(normals[i])
            N = (N - T[i] * N.dot(T[i]))
            N.normalize()
            frames.append((N, T[i].cross(N)))
    else:
        up = Vector((0, 0, 1))
        N = up - T[0] * up.dot(T[0])
        if N.length < 1e-4:
            N = Vector((1, 0, 0)) - T[0] * T[0].x
        N.normalize()
        for i in range(n):
            if i > 0:
                N = T[i - 1].rotation_difference(T[i]) @ N
                N = (N - T[i] * N.dot(T[i])).normalized()
            frames.append((N, T[i].cross(N)))
    ph = math.pi / sides if phase is None else phase
    bm = bmesh.new()
    rings = []
    for i in range(n):
        N, B = frames[i]
        rings.append([bm.verts.new(P[i] + R[i] * (math.cos(ph + 2 * math.pi * k / sides) * N +
                                                  math.sin(ph + 2 * math.pi * k / sides) * B))
                      for k in range(sides)])
    for i in range(n if closed else n - 1):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(sides):
            k1 = (k + 1) % sides
            f = bm.faces.new((r0[k], r0[k1], r1[k1], r1[k]))
            f.material_index = 0
    mats = [mat(colour, rough, spec, emit)]
    if caps and not closed:
        mats.append(mat(cap_colour if cap_colour is not None else colour, rough, spec, emit))
        f = bm.faces.new(list(reversed(rings[0])))
        f.material_index = 1
        f = bm.faces.new(rings[-1])
        f.material_index = 1
    ob = mesh_object(name, bm, mats, xf)
    # the frame orientation above is not guaranteed outward for every path: recalc
    me = ob.data
    bm2 = bmesh.new()
    bm2.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm2, faces=bm2.faces)
    bm2.to_mesh(me)
    bm2.free()
    return ob


def lathe(profile, sides, colours, xf=None, phase=None, rough=0.85, spec=0.25, name="lathe"):
    """A surface of revolution round +Z from a bottom-to-top profile of (radius, z, colour
    index). A radius of 0 closes the end in a fan."""
    ph = math.pi / sides if phase is None else phase
    bm = bmesh.new()
    rings = []
    for r, z, _ in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new((0.0, 0.0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(ph + 2 * math.pi * k / sides),
                                        r * math.sin(ph + 2 * math.pi * k / sides), z))
                          for k in range(sides)])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        mi = profile[i + 1][2]
        for k in range(sides):
            k1 = (k + 1) % sides
            if len(a) == 1:
                f = bm.faces.new((a[0], b[k1], b[k]))
            elif len(b) == 1:
                f = bm.faces.new((a[k], a[k1], b[0]))
            else:
                f = bm.faces.new((a[k], a[k1], b[k1], b[k]))
            f.material_index = mi
    if len(rings[0]) > 1:
        f = bm.faces.new(list(reversed(rings[0])))
        f.material_index = profile[0][2]
    if len(rings[-1]) > 1:
        f = bm.faces.new(rings[-1])
        f.material_index = profile[-1][2]
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_object(name, bm, [mat(c, rough, spec) for c in colours], xf)


def blob(center, radius, colour, subdiv=1, squash=(1.0, 1.0, 1.0), rough=0.9, xf=None, name="blob", emit=0.0):
    """A faceted icosphere: smoke puffs, berries, knots."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    for v in bm.verts:
        v.co = Vector((v.co.x * squash[0], v.co.y * squash[1], v.co.z * squash[2])) + Vector(center)
    return mesh_object(name, bm, [mat(colour, rough, 0.2, emit)], xf)


# ----------------------------------------------------------------------------- fonts

FONT_DIR = DEFAULT_FONTS
_FONTS = {}


def font(weight="Bold"):
    """Fredoka at a weight (decision 7), or Blender's Inter when the TTF is not there."""
    if weight in _FONTS:
        return _FONTS[weight]
    path = os.path.join(FONT_DIR, "Fredoka-%s.ttf" % weight)
    if os.path.isfile(path):
        f = bpy.data.fonts.load(path, check_existing=True)
    else:
        print("[ui_kit] WARNING: %s missing, falling back to Inter" % path)
        d = bpy.utils.system_resource("DATAFILES", path="fonts")
        f = bpy.data.fonts.load(os.path.join(d, "Inter.woff2"), check_existing=True)
    _FONTS[weight] = f
    return f


class FaceMetrics:
    """A TTF's advance widths and vertical metrics (cmap format 4, hmtx, hhea), read with
    struct. The contact sheet lays text out the way UI Toolkit does: a Label is a line box of
    ascender minus descender (1.21 em for Fredoka, no line gap), as wide as its advances, and
    it measures a letter's ink as 1 em when checking a content rect (decision 9)."""

    def __init__(self, path):
        self.upm, self.asc, self.desc, self.adv, self.cmap = 1000, 950, -250, [550], {}
        if not os.path.isfile(path):
            return                           # Inter fallback: rough Latin proportions
        with open(path, "rb") as f:
            d = f.read()
        tables = {}
        for i in range(struct.unpack(">H", d[4:6])[0]):
            tag, _, off, _ = struct.unpack(">4sIII", d[12 + 16 * i:28 + 16 * i])
            tables[tag.decode("latin-1")] = off
        self.upm = struct.unpack(">H", d[tables["head"] + 18:tables["head"] + 20])[0]
        o = tables["hhea"]
        self.asc, self.desc = struct.unpack(">hh", d[o + 4:o + 8])
        count = struct.unpack(">H", d[o + 34:o + 36])[0]
        o = tables["hmtx"]
        self.adv = [struct.unpack(">H", d[o + 4 * i:o + 4 * i + 2])[0] for i in range(count)]
        c = tables["cmap"]
        for i in range(struct.unpack(">H", d[c + 2:c + 4])[0]):
            pid, _, off = struct.unpack(">HHI", d[c + 4 + 8 * i:c + 12 + 8 * i])
            s = c + off
            if pid not in (0, 3) or struct.unpack(">H", d[s:s + 2])[0] != 4:
                continue
            seg = struct.unpack(">H", d[s + 6:s + 8])[0] // 2
            ends = struct.unpack(">%dH" % seg, d[s + 14:s + 14 + 2 * seg])
            p = s + 16 + 2 * seg
            starts = struct.unpack(">%dH" % seg, d[p:p + 2 * seg])
            deltas = struct.unpack(">%dh" % seg, d[p + 2 * seg:p + 4 * seg])
            ro = p + 4 * seg
            offs = struct.unpack(">%dH" % seg, d[ro:ro + 2 * seg])
            for k in range(seg):
                for ch in range(starts[k], min(ends[k], 0x2FFF) + 1):   # Latin, enough for FR/EN
                    if offs[k] == 0:
                        g = (ch + deltas[k]) & 0xFFFF
                    else:
                        a = ro + 2 * k + offs[k] + 2 * (ch - starts[k])
                        g = struct.unpack(">H", d[a:a + 2])[0]
                        g = (g + deltas[k]) & 0xFFFF if g else 0
                    self.cmap.setdefault(ch, g)
            break

    def width(self, s, size):
        last = len(self.adv) - 1
        return sum(self.adv[min(self.cmap.get(ord(ch), 0), last)] for ch in s) * size / float(self.upm)

    def line(self, size):
        return (self.asc - self.desc) * size / float(self.upm)

    def ascender(self, size):
        return self.asc * size / float(self.upm)


_METRICS = {}


def metrics(weight="Bold"):
    if weight not in _METRICS:
        _METRICS[weight] = FaceMetrics(os.path.join(FONT_DIR, "Fredoka-%s.ttf" % weight))
    return _METRICS[weight]


def text(s, size, x, y, z, colour, depth=0.8, bold=0.0, max_w=None, max_h=None, rough=0.7,
         bevel=0.25, xf=None, rot=0.0, name="text", emit=0.0, weight="Bold"):
    """Extruded, chamfered text centred on (x, y) by its real outline (decision 7)."""
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = s
    cu.font = font(weight)
    cu.size = size
    cu.extrude = depth * 0.5
    cu.offset = bold * size
    cu.bevel_depth = min(bevel, depth * 0.45)
    cu.bevel_resolution = 0
    cu.resolution_u = 5
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    ob = link(bpy.data.objects.new(name, cu))
    ob.data.materials.append(mat(colour, rough, 0.2, emit))
    bpy.context.view_layer.update()
    bb = [Vector(c) for c in ob.bound_box]
    w = max(c.x for c in bb) - min(c.x for c in bb)
    h = max(c.y for c in bb) - min(c.y for c in bb)
    k = 1.0
    if max_w and w > max_w:
        k = min(k, max_w / w)
    if max_h and h > max_h:
        k = min(k, max_h / h)
    cx = (max(c.x for c in bb) + min(c.x for c in bb)) * 0.5
    cy = (max(c.y for c in bb) + min(c.y for c in bb)) * 0.5
    local = (Matrix.Translation((x, y, z + depth * 0.5)) @ Matrix.Rotation(rot, 4, "Z") @
             Matrix.Diagonal((k, k, 1.0, 1.0)) @ Matrix.Translation((-cx, -cy, 0.0)))
    ob.matrix_world = (xf @ local) if xf is not None else local
    return ob


def inked_text(s, size, x, y, z, colour, ink=INK, drop=(0.45, -0.8), **kw):
    """Text with an ink copy just under it and offset down-right: a printed letter that
    keeps its contrast on a yellow button as on a blue one."""
    kw_ink = dict(kw)
    kw_ink["name"] = "ink_" + kw.get("name", "text")
    text(s, size, x + drop[0], y + drop[1], z - 0.3, ink, **kw_ink)
    return text(s, size, x, y, z, colour, **kw)


# ----------------------------------------------------------------------------- props


def nail(x, y, z, r=2.2, colour="iron", xf=None):
    slab(ngon(x, y, r, 6, math.radians(15)), z - 0.5, z + 1.2, colour, chamfer=0.7, rough=0.5,
         spec=0.5, xf=xf, name="nail")


def grain_lines(x0, y0, x1, y1, z, colour, count, horizontal=True, width=0.7):
    """Straight grain lines along a batten: uniform along its length, so it stretches."""
    for _ in range(count):
        if horizontal:
            gy = random.uniform(y0 + (y1 - y0) * 0.22, y1 - (y1 - y0) * 0.22)
            pts = rrect(x0, gy - width / 2, x1, gy + width / 2, 0)
        else:
            gx = random.uniform(x0 + (x1 - x0) * 0.22, x1 - (x1 - x0) * 0.22)
            pts = rrect(gx - width / 2, y0, gx + width / 2, y1, 0)
        slab(pts, z - 0.4, z + 0.25, colour, name="grain")


def rope(path, R, zc=0.0, closed=True, period=None, colours=("rope", "rope_dark", "rope"), strands=3,
         sides=5, name="rope"):
    """A three-strand twisted rope along a path: 2D points at height zc, or 3D points."""
    period = period or R * 7.0
    step = period / 12.0
    P = [Vector((q[0], q[1], q[2] if len(q) > 2 else zc)) for q in path]
    pts, L = resample(P, closed, step)
    if closed:
        period = L / max(1, round(L / period))
    n = len(pts)
    tang = []
    for i in range(n):
        if closed:
            a, b = pts[i - 1], pts[(i + 1) % n]
        else:
            a, b = pts[max(i - 1, 0)], pts[min(i + 1, n - 1)]
        t = (b - a)
        tang.append(t.normalized() if t.length > 1e-9 else Vector((1, 0, 0)))
    s_ = [0.0]
    for i in range(1, n):
        s_.append(s_[-1] + (pts[i] - pts[i - 1]).length)
    up = Vector((0, 0, 1))
    tube([tuple(q) for q in pts], R * 0.72, colours[1], sides=6, closed=closed, cap_colour="rope_dark",
         name=name + "_core")
    for k in range(strands):
        path3, norms = [], []
        for i in range(n):
            t = tang[i]
            N = (up - t * up.dot(t))
            N = N.normalized() if N.length > 1e-6 else Vector((1, 0, 0))
            B = t.cross(N)
            th = 2 * math.pi * s_[i] / period + k * 2 * math.pi / strands
            radial = math.cos(th) * N + math.sin(th) * B
            path3.append(pts[i] + radial * (R * 0.46))
            norms.append(radial)
        tube(path3, R * 0.56, colours[k % len(colours)], sides=sides, closed=closed, normals=norms,
             name=name + "_strand")


def branch(p0, p1, r0, r1, zc, wobble=0.8, seed_phase=0.0, colour="bark", sides=6, stubs=(),
           name="branch"):
    """A tapered low-poly branch from p0 to p1 with a gentle wobble and sawn ends."""
    p0, p1 = V(*p0), V(*p1)
    d = p1 - p0
    L = d.length
    t = d.normalized()
    nrm = V(-t.y, t.x)
    n = max(6, int(L / 6))
    path, rad = [], []
    for i in range(n + 1):
        u = i / n
        w = wobble * math.sin(u * math.pi * 2.3 + seed_phase) * math.sin(u * math.pi)
        p = p0 + d * u + nrm * w
        path.append((p.x, p.y, zc + 0.4 * math.sin(u * 7.0 + seed_phase)))
        rad.append(r0 + (r1 - r0) * u)
    tube(path, rad, colour, sides=sides, cap_colour="cut_wood", name=name, phase=0.0)
    for u, side, length in stubs:
        base = p0 + d * u
        direc = (t * 0.6 + nrm * side).normalized()
        tip = base + direc * length
        r = r0 + (r1 - r0) * u
        tube([(base.x, base.y, zc), (tip.x, tip.y, zc + 1.0)], [r * 0.55, r * 0.3], colour, sides=5,
             cap_colour="cut_wood", name=name + "_stub")


def leaf(x, y, z, angle, length, width, colour, droop=1.2, lift=1.4, name="leaf"):
    """A folded low-poly leaf: two facets per side round a raised midrib."""
    L, w = length, width
    loc = [(0, 0, 0), (0, L, -droop), (0, 0.5 * L, lift), (-w, 0.35 * L, 0), (-0.55 * w, 0.72 * L, -0.4 * droop),
           (w, 0.35 * L, 0), (0.55 * w, 0.72 * L, -0.4 * droop)]
    bm = bmesh.new()
    vs = [bm.verts.new(c) for c in loc]
    b, t, m, l1, l2, r1, r2 = vs
    for tri in ((b, m, l1), (l1, m, l2), (l2, m, t), (b, r1, m), (r1, r2, m), (r2, t, m)):
        bm.faces.new(tri)
    xf = Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(angle - 90.0), 4, "Z")
    ob = mesh_object(name, bm, [mat(colour, 0.8, 0.3)], xf)
    bm2 = bmesh.new()
    bm2.from_mesh(ob.data)
    for f in bm2.faces:                     # every facet faces the camera
        if f.normal.z < 0:
            f.normal_flip()
    bm2.to_mesh(ob.data)
    bm2.free()


def leaf_cluster(cx, cy, z, direction, count, spread=150.0, length=(9, 14), width=(3.2, 4.4),
                 berries=0):
    greens = ("leaf", "leaf_dark", "leaf_light")
    for i in range(count):
        a = direction + (i / max(1, count - 1) - 0.5) * spread + random.uniform(-12, 12)
        L = random.uniform(*length)
        off = random.uniform(1.0, 3.0)
        leaf(cx + math.cos(math.radians(a)) * off, cy + math.sin(math.radians(a)) * off,
             z + random.uniform(0.0, 2.5), a, L, random.uniform(*width), greens[i % 3])
    for i in range(berries):
        a = math.radians(direction + random.uniform(-50, 50))
        rr = random.uniform(3.5, 6.0)
        blob((cx + math.cos(a) * rr, cy + math.sin(a) * rr, z + 3.0), 1.7, "berry", rough=0.4, name="berry")


def lashing(cx, cy, z, size, turns=3, radius=1.8):
    """Rope lashed over a crossing of two battens or branches: `turns` wraps along each
    diagonal, the second diagonal over the first, every wrap arching over the crossing and
    diving at its ends, as rope pulled tight round two pieces of wood does."""
    c = V(cx, cy)
    for layer, d in enumerate((1, -1)):
        u = V(1, d).normalized()
        nrm = V(-d, 1).normalized()
        for k in range(turns):
            off = (k - (turns - 1) / 2) * radius * 1.85
            pts = []
            for i in range(9):
                t = (i / 8 * 2 - 1) * size / 2
                q = c + u * t + nrm * off
                pts.append((q.x, q.y, z + layer * radius * 1.1 + 2.4 * (1 - (2 * t / size) ** 2) - 0.8))
            rope(pts, radius, closed=False, period=radius * 6.0, name="lash")


def stamp_arrows(cx, cy, size, colour="brick", z=0.0):
    """The 'this side up' mark printed on moving boxes: two up arrows over a bar."""
    for dx in (-size * 0.32, size * 0.32):
        slab(arrow_poly(cx + dx, cy + size * 0.08, size * 0.78, 90, shaft=0.28, head=0.62), z, z + 0.14, colour,
             name="stamp")
    slab(rrect(cx - size * 0.55, cy - size * 0.5, cx + size * 0.55, cy - size * 0.4, 0), z, z + 0.14, colour,
         name="stamp")


def pose(yaw=0.0, pitch=25.0, roll=0.0, at=(0, 0, 0), scale=1.0):
    """A matrix that shows a Z-up object from `pitch` degrees above its horizon, turned by
    `yaw`, for the 3/4 icons: the object's front (-Y) faces the camera."""
    return (Matrix.Translation(at) @ Matrix.Rotation(math.radians(roll), 4, "Z") @
            Matrix.Rotation(math.radians(-(90.0 - pitch)), 4, "X") @
            Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Scale(scale, 4))


def box_faces(sx, sy, sz):
    """Frames (origin, u, v, n) on the +Z top, -Y front and +X right faces of a box centred
    on the origin with its base at z = 0: for slabs laid on those faces."""
    return {
        "top": (Vector((0, 0, sz)), Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))),
        "front": (Vector((0, -sy / 2, sz / 2)), Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))),
        "right": (Vector((sx / 2, 0, sz / 2)), Vector((0, 1, 0)), Vector((0, 0, 1)), Vector((1, 0, 0))),
        "left": (Vector((-sx / 2, 0, sz / 2)), Vector((0, -1, 0)), Vector((0, 0, 1)), Vector((-1, 0, 0))),
    }


def face_matrix(frame):
    o, u, v, n = frame
    m = Matrix.Identity(4)
    for r in range(3):
        m[r][0], m[r][1], m[r][2], m[r][3] = u[r], v[r], n[r], o[r]
    return m


def moving_box(xf, sx=25.0, sy=20.0, sz=18.0, arrows=True):
    """A taped cardboard box, base at z = 0, seen through `xf`."""
    slab(rrect(-sx / 2, -sy / 2, sx / 2, sy / 2, 0.6, 1), 0.0, sz, "cardboard_dark", chamfer=1.0, top="cardboard",
         xf=xf, name="box")
    faces = box_faces(sx, sy, sz)
    top = xf @ face_matrix(faces["top"])
    slab(rrect(-sx / 2 - 0.3, -sy * 0.12, sx / 2 + 0.3, sy * 0.12, 0), 0.0, 0.35, "tape", rough=0.3, spec=0.6,
         xf=top, name="tape")
    slab(rrect(-sx / 2 + 1, -0.25, sx / 2 - 1, 0.25, 0), 0.3, 0.45, shade("tape", 0.8), xf=top, name="seam")
    front = xf @ face_matrix(faces["front"])
    slab(rrect(-sx * 0.1, sz * 0.25, sx * 0.1, sz / 2 + 0.3, 0), 0.0, 0.35, "tape", rough=0.3, spec=0.6,
         xf=front, name="tape")
    if arrows:
        for dx in (-sx * 0.26, sx * 0.26):
            slab(arrow_poly(dx, -sz * 0.08, sz * 0.39, 90, shaft=0.3, head=0.64), 0.0, 0.2, "brick", xf=front,
                 name="stamp")


# ----------------------------------------------------------------------------- sprite specs


def LBRT(left, bottom, right, top):
    """9-slice borders in 1x pixels, in Unity's Sprite.border order."""
    return (left, bottom, right, top)


SHADOW = (2, -4, 3, 0.32)       # texture px: dx, dy, blur radius, opacity


class Spec:
    """One sprite. w, h and every length are 1x pixels; outline radii and the shadow are
    texture pixels. `design` is the canvas size a builder was written for, when it differs:
    the built scene is scaled to the canvas. `mips`: import with mipmaps (decision 10).
    `stretch`: "xy" for a 9-slice that grows both ways, "x" for one drawn at its own
    height scaled as a whole (decision 9). `trim`: crop the finished PNG to its art plus
    `pad` (the logo), so the sprite rect is the art. `face`: where the plain surface ends,
    when that is closer to the rim than `content` (decision 9); by default the content rect."""

    def __init__(self, name, group, w, h, build, kind="simple", border=None, content=None, pad=6,
                 outline=5.0, shadow=SHADOW, states=None, design=None, pivot=(0.5, 0.5), tint=False,
                 text=None, slice_mode=None, tile_period=None, desc="", mips=None, stretch=None, trim=False,
                 face=None):
        self.name, self.group, self.w, self.h, self.build = name, group, w, h, build
        self.kind, self.border, self.content, self.pad = kind, border, content, pad
        self.face = face or content
        self.outline, self.shadow, self.states, self.design = outline, shadow, states, design
        self.pivot, self.tint, self.text = pivot, tint, text
        self.slice_mode, self.tile_period, self.desc = slice_mode, tile_period, desc
        # whole pictures are drawn much smaller than their texture: mipmaps; slices are drawn
        # at about the slice scale and stay sharper without
        self.mips = (kind == "simple") if mips is None else mips
        self.stretch = stretch or ("xy" if kind == "sliced" else None)
        self.trim = trim


SPECS = []


def spec(*a, **k):
    s = Spec(*a, **k)
    SPECS.append(s)
    return s


BUTTON_STATES = ("normal", "hover", "pressed", "disabled")

# ---------------------------------------------------------------- frames and panels


def build_frame_wood(s, dark=False):
    """Four battens lashed with rope at the corners round one flat board (decision 5): the
    board is uniform, the battens run along their edges, everything else is in the corners."""
    W, H, p = s.w, s.h, s.pad
    t = 22.0                                   # batten width
    x0, y0, x1, y1 = p, p, W - p, H - p
    board = "walnut" if dark else "pine"
    bat_top = ("wood_dark", shade("wood_dark", 0.9)) if dark else ("wood", "wood_warm")
    bat_side = "bark_dark" if dark else "bark"
    # the board, recessed under the battens so they cast a soft shadow onto it
    slab(rrect(x0 + 5, y0 + 5, x1 - 5, y1 - 5, 3, 1), 0.0, 2.0, shade(board, 0.85), chamfer=0.5, top=board,
         name="board")
    # left and right battens, under the others, grain along y
    for i, (xa, xb) in enumerate(((x0, x0 + t), (x1 - t, x1))):
        col = bat_top[(i + 1) % 2]
        slab(rrect(xa, y0 + 6, xb, y1 - 6, 3, 1), 1.5, 9.0, bat_side, chamfer=2.6, top=col, name="batten_v")
        grain_lines(xa + 3, y0 + 14, xb - 3, y1 - 14, 9.0, shade(col, 0.8), 2, horizontal=False, width=0.9)
    # top and bottom battens over them, grain along x
    for i, (ya, yb) in enumerate(((y0, y0 + t), (y1 - t, y1))):
        col = bat_top[i % 2]
        slab(rrect(x0 - 0.5, ya, x1 + 0.5, yb, 3.5, 1), 3.5, 11.5, bat_side, chamfer=2.8, top=col, name="batten_h")
        grain_lines(x0 + 14, ya + 3, x1 - 14, yb - 3, 11.5, shade(col, 0.8), 2, horizontal=True, width=0.9)
    # rope lashings over the four crossings, and a nail on two of the battens
    for (cx, cy) in ((x0 + t / 2, y0 + t / 2), (x1 - t / 2, y0 + t / 2), (x0 + t / 2, y1 - t / 2),
                     (x1 - t / 2, y1 - t / 2)):
        lashing(cx, cy, 12.0, 17.0, radius=1.9)
    for (cx, cy) in ((x0 + t + 6, y0 + t / 2), (x1 - t - 6, y1 - t / 2)):
        nail(cx, cy, 11.8, r=2.3)


def build_frame_branch(s):
    """Four branches lashed at the corners, a thin twig riding along each, leaf clusters
    and berries at the corners only; the inside is transparent, so the title art or a panel
    shows through."""
    W, H, p = s.w, s.h, s.pad
    a = p + 23
    e = p + 3
    branch((e, H - a), (W - e, H - a), 7.8, 6.4, 8.5, wobble=1.0, seed_phase=0.3, sides=7)
    branch((W - e, a), (e, a), 7.8, 6.4, 8.5, wobble=1.0, seed_phase=1.7, sides=7)
    branch((a, e), (a, H - e), 7.4, 6.2, 15.0, wobble=0.9, seed_phase=2.4, sides=7)
    branch((W - a, H - e), (W - a, e), 7.4, 6.2, 15.0, wobble=0.9, seed_phase=0.9, sides=7)
    # a thin twig along each branch, crossing it gently
    for (q0, q1, zc, ph) in (((e + 8, H - a + 3), (W - e - 8, H - a - 2), 16.8, 0.0),
                             ((W - e - 8, a - 3), (e + 8, a + 2), 16.8, 1.1),
                             ((a - 3, e + 8), (a + 2, H - e - 8), 23.0, 2.0),
                             ((W - a + 3, H - e - 8), (W - a - 2, e + 8), 23.0, 2.9)):
        branch(q0, q1, 2.2, 1.6, zc, wobble=4.0, seed_phase=ph, colour="bark_dark", sides=5)
    for (x, y), d in (((a, a), 225.0), ((a, H - a), 135.0), ((W - a, a), 315.0), ((W - a, H - a), 45.0)):
        lashing(x, y, 24.0, 15.0, radius=1.6)
        leaf_cluster(x, y, 27.0, d, 9, spread=180, length=(14, 22), width=(4.8, 7.0), berries=3)
        leaf_cluster(x + math.cos(math.radians(d + 90)) * 12, y + math.sin(math.radians(d + 90)) * 12, 21.0,
                     d + 65, 3, spread=55, length=(10, 15), width=(3.6, 5.2))
        leaf_cluster(x + math.cos(math.radians(d - 90)) * 12, y + math.sin(math.radians(d - 90)) * 12, 21.0,
                     d - 65, 3, spread=55, length=(10, 15), width=(3.6, 5.2))


def build_frame_cardboard(s):
    """A box flap: packing tape across the top, torn at both ends, a crease under it, the
    'this side up' stamp bottom right and a mover's tick bottom left. Everything that is not
    uniform sits within 30 px of an edge (1x). A pocket slot of 78 x 84 px is drawn at 0.2,
    the scale at which its content rect (below the tape) holds an icon and a caption; at 0.5
    it would hold only 38 x 36 px (decision 9)."""
    W, H, p = s.w, s.h, s.pad
    x0, y0, x1, y1 = p + 1, p + 1, W - p - 1, H - p - 1
    slab(rrect(x0, y0, x1, y1, 4, 1), 0.0, 3.0, "cardboard_dark", chamfer=1.4, top="cardboard", name="flap")
    ty0, ty1 = y1 - 16.0, y1 - 3.0
    slab(rrect(x0 + 2, ty0 - 2.9, x1 - 2, ty0 - 2.1, 0), 2.8, 3.15, shade("cardboard", 0.84), name="crease")
    xl, xr = x0 - 2.5, x1 + 2.5
    teeth = 5
    pts = [V(xl + 1.0, ty0), V(xr - 1.0, ty0)]
    for i in range(1, teeth + 1):
        pts.append(V(xr + (1.3 if i % 2 else -0.4), ty0 + (ty1 - ty0) * i / (teeth + 1)))
    pts += [V(xr - 0.6, ty1), V(xl + 0.6, ty1)]
    for i in range(teeth, 0, -1):
        pts.append(V(xl - (1.3 if i % 2 else -0.4), ty0 + (ty1 - ty0) * i / (teeth + 1)))
    slab(pts, 3.0, 3.6, "tape", chamfer=0.25, rough=0.3, spec=0.6, name="tape")
    slab(rrect(xl + 1, ty1 - 1.0, xr - 1, ty1 - 0.3, 0), 3.55, 3.7, shade("tape", 1.12), rough=0.3, name="tape_edge")
    stamp_arrows(W - 20.0, 18.5, 9.0, z=3.0)
    # a mover's marker tick, bottom left
    tick = [V(-12, 1), V(-7.5, 5.5), V(-3.5, 1.5), V(9.5, 14.5), V(14, 10), V(-3.5, -7.5)]
    slab(transform2(tick, 17.0, 16.5, math.radians(-6), 0.38), 3.0, 3.14, "ink", name="tick")


def build_frame_tag(s):
    """A paper luggage label: clipped corners on the left, a reinforced hole near the top-left
    corner with a red string knotted just off the edge, a thin printed border. The hole sits
    in the corner, not at mid-height, so a taller tag (a speech bubble of three lines)
    stretches plain paper (decision 5). Compact on purpose: the hole, the string and the
    clipped corners fit in 34 x 22 px (1x) at the top left and 14 px at the bottom, so a
    one-line prompt of 36 px is tall enough even at 0.5. UICORE draws tags at 0.3 to 0.36,
    where the content rect, right of the string, holds a 30 px key row (decision 9)."""
    W, H, p = s.w, s.h, s.pad
    x0, y0, x1, y1, c = 18.0, p + 0.5, W - p - 1.0, H - p - 1.0, 6.0
    tag = [V(x0 + c, y0), V(x1, y0), V(x1, y1), V(x0 + c, y1), V(x0, y1 - c), V(x0, y0 + c)]
    paper = slab(tag, 0.0, 1.2, "paper_edge", chamfer=0.5, top="paper", name="paper")
    band(inset(tag, 2.6), inset(tag, 3.5), 1.15, 1.23, "brick", name="stripe")
    hx, hy = x0 + 10.0, y1 - 8.5
    cutter = slab(ngon(hx, hy, 2.3, 12), -2.0, 4.0, "ink", name="cutter")
    cutter.hide_render = True
    mod = paper.modifiers.new("hole", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = cutter
    band(ngon(hx, hy, 4.4, 12), ngon(hx, hy, 2.3, 12), 1.1, 1.7, "ring", name="ring")
    kx, ky = x0 - 7.5, hy + 3.2
    # over the paper from the hole to the edge, then to the knot; the other strand comes from
    # under the paper; two short tails hang from the knot
    tube([(hx - 1.1, hy, 1.0), (hx - 3.8, hy + 1.1, 2.2), (x0 - 1.0, hy + 2.3, 2.0), (kx + 1.6, ky - 0.4, 1.8)],
         0.95, "brick", sides=6, name="string")
    tube([(x0 + 0.8, hy - 2.4, -0.8), (x0 - 2.4, hy - 1.3, 0.7), (kx + 1.6, ky - 1.2, 1.6)], 0.95,
         shade("brick", 0.85), sides=6, name="string")
    blob((kx, ky, 2.1), 2.0, "brick", squash=(1.1, 1.0, 0.8), name="knot")
    for (tx, ty) in ((kx - 6.0, ky + 1.2), (kx - 5.0, ky - 4.4)):
        tube([(kx - 0.8, ky, 1.9), ((kx + tx) / 2, (ky + ty) / 2 + 0.5, 1.6), (tx, ty, 1.2)], [0.9, 0.8, 0.65],
             "brick", sides=6, cap_colour=shade("brick", 0.8), name="tail")


def build_button_wood(s):
    """A thick nailed plank with sawn ends; knots only in the corners (decision 5). The grain,
    nails and knots hug the long edges (within 22 px, 1x, pressed state included), so the
    plank still stretches cleanly at 44 px tall, the height of a one-line menu button."""
    W, H, p = s.w, s.h, s.pad
    x0, y0, x1, y1 = p + 2, p + 4, W - p - 2, H - p - 2
    pts = rrect(x0, y0, x1, y1, 7, 2)
    # sawn ends a little out of square: the end vertices move in x only, so the long edges stay
    # straight and stretch cleanly; the slanted ends stay straight lines when stretched
    pts = [q + V(random.uniform(-0.8, 0.8), 0.0) if (q.x < x0 + 8 or q.x > x1 - 8) else q for q in pts]
    slab(pts, 0.0, 9.0, "plank_side", chamfer=3.0, top="plank", name="plank")
    # grain, knots and nails stay in the top and bottom border rows and the end columns, so a
    # taller or wider button stretches plain plank
    for gy in (y0 + 9.0, y1 - 8.5):
        slab(rrect(x0 + 13, gy - 0.5, x1 - 13, gy + 0.5, 0), 8.2, 9.35, shade("plank", 0.82), name="grain")
    for (kx, ky, r) in ((x0 + 25, y0 + 6.0, 2.4), (x1 - 24, y1 - 5.5, 2.0)):
        band(ngon(kx, ky, r + 1.2, 10, ry=(r + 1.2) * 0.62), ngon(kx, ky, r, 10, ry=r * 0.62), 8.4, 9.3,
             shade("plank", 0.8), name="knot_ring")
        slab(ngon(kx, ky, r * 0.7, 8, ry=r * 0.45), 8.4, 9.3, shade("plank", 0.62), name="knot")
    for nx in (x0 + 12, x1 - 12):
        for ny in (y0 + 6.5, y1 - 6.0):
            nail(nx, ny, 9.6, r=2.2)


# ---------------------------------------------------------------- tape measure


def tape_case(x0, y0, x1, y1, z=0.0, k=1.0):
    """The tape measure's case seen from the side: a rounded yellow body the height of the
    bar, a rubber plate and a screw hub in its middle. `k` scales depths and bevels."""
    h = y1 - y0
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    slab(rrect(x0, y0, x1, y1, h * 0.32, 3), z, z + 6.0 * k, shade("tape_yellow", 0.78), chamfer=1.6 * k,
         top="tape_yellow", rough=0.5, spec=0.4, name="case")
    slab(ngon(cx, cy, h * 0.33, 14), z + 5.7 * k, z + 6.5 * k, "rubber", chamfer=0.5 * k, name="plate")
    slab(ngon(cx, cy, h * 0.13, 10), z + 6.4 * k, z + 7.4 * k, "metal", chamfer=0.5 * k, rough=0.35, spec=0.6,
         name="hub")


def build_bar_tape_bg(s):
    """The slot the tape runs in, with the case at its left end. The case is as tall as the
    bar and the vertical borders cover the whole height, so the sprite never stretches in
    height: the UI stretches its width and scales it as a whole to the bar's height
    (sprites.json "stretch": "x"; decision 9). Designed 20 units tall and built at the
    canvas height (80 texels, kept for the HUD, which shows it 24 px tall at 24 / 80), with
    lines thick enough for that 0.3 scale."""
    W, H = s.w, s.h
    k = H / 20.0
    rim = rrect(18.0 * k, 4.5 * k, W - 3.5 * k, H - 3.0 * k, 3.5 * k, 2)
    groove = rrect(20.0 * k, 6.0 * k, W - 5.0 * k, H - 4.5 * k, 2.2 * k, 2)
    slab(rim, 0.0, 3.0 * k, "bark", chamfer=0.7 * k, top="wood_dark", name="rim")
    slab(groove, 0.0, 3.2 * k, shade("rubber", 0.9), name="groove")
    tape_case(3.0 * k, 3.5 * k, 23.0 * k, H - 2.5 * k, k=k)


def build_bar_tape_fill(s):
    """The yellow tape: a flat left end, tick marks every 8 px with a long one every 32 (the
    centre is 4 whole periods, so a tiled centre has no seam), the brass hook in the right
    border. As tall as bar_tape_bg's groove at the same density; ticks 2 px wide and 8 px
    apart so they stay even at the 0.3 scale the HUD draws the bar at."""
    W, H = s.w, s.h
    k = H / 10.0
    L, R = 3.0, 9.0 * k
    xe = W - R
    slab(rrect(-0.5, 0.5 * k, xe + 1.0, H - 0.5 * k, 0), 0.0, 1.0 * k, shade("tape_yellow", 0.86), chamfer=0.4 * k,
         top="tape_yellow", rough=0.45, spec=0.35, name="tape")
    for i in range(int((xe - L) / 8.0)):
        x = L + 4.0 + 8.0 * i
        ln = (5.5 if i % 4 == 0 else (3.8 if i % 2 == 0 else 2.4)) * k
        slab(rrect(x - 1.0, H - 0.5 * k - ln, x + 1.0, H - 0.5 * k, 0), 0.9 * k, 1.15 * k, INK, name="tick")
    slab(rrect(xe, 0.0, xe + 6.0 * k, H, 1.0 * k, 1), 0.0, 2.4 * k, shade("brass", 0.8), chamfer=0.6 * k, top="brass",
         rough=0.35, spec=0.6, name="hook")
    slab(rrect(xe + 5.0 * k, 0.0, W - 0.3, H, 0.9 * k, 1), 0.0, 3.4 * k, shade("brass", 0.78), chamfer=0.7 * k,
         top="brass", rough=0.35, spec=0.6, name="lip")
    for y in (2.6 * k, H - 2.6 * k):
        slab(ngon(xe + 2.6 * k, y, 0.9 * k, 6), 2.2 * k, 2.8 * k, "iron", chamfer=0.25 * k, name="rivet")


# ---------------------------------------------------------------- keyboard and mouse


def build_keycap(s):
    """A chunky rounded key cap, blank: the UI writes the key's name in Fredoka. The cap's
    top sits high on its skirt, so a thicker lip shows at the front (the bottom). Its corners
    and lip fit in 12 px (14 at the bottom, 1x). The UI draws the cap whole, at slice scale
    height / 128 (about 0.22 for the HUD's 26 to 32 px keys), where the content rect (the top
    face) holds the letter. At 0.5 the corners would still fit a 26 px key, but the face would
    shrink to 6 px for a 13 px letter (decision 9)."""
    W, H, p = s.w, s.h, s.pad
    x0, y0, x1, y1 = p, p, W - p, H - p
    slab(rrect(x0, y0, x1, y1, 6.5, 3), 0.0, 5.0, shade("key_side", 0.84), chamfer=1.4, top="key_side",
         name="skirt")
    slab(rrect(x0 + 2.5, y0 + 5.0, x1 - 2.5, y1 - 1.5, 4.0, 3), 4.2, 9.5, "key_side", chamfer=2.0,
         top="key_top", name="cap")


def mouse_body(x0, y0, x1, y1, highlight=None, wheel_hi=False):
    """A mouse seen from above; highlight is 'left', 'right' or None."""
    body = rrect(x0, y0, x1, y1, (x1 - x0) * 0.5 - 0.01, 5)
    slab(body, 0.0, 7.5, "key_side", chamfer=3.0, top="key_top", name="mouse")
    top = inset(body, 3.0)
    cx = (x0 + x1) * 0.5
    ys = y0 + (y1 - y0) * 0.56
    g = 0.55
    if highlight in ("left", "right"):
        part = clip(top, 0, 1, -(ys + g))
        part = clip(part, -1, 0, cx - g) if highlight == "left" else clip(part, 1, 0, -(cx + g))
        slab(part, 7.0, 7.9, "accent", chamfer=0.4, name="button")
    slab(rrect(cx - 0.5, ys, cx + 0.5, y1 - 2.5, 0), 7.3, 7.95, INK, name="seam")
    seam = clip(clip(top, 0, 1, -(ys - 0.5)), 0, -1, ys + 0.5)
    if len(seam) >= 3:
        slab(seam, 7.3, 7.95, INK, name="seam")
    wy = ys + (y1 - ys) * 0.42
    slab(rrect(cx - 2.3, wy - 4.0, cx + 2.3, wy + 4.0, 2.0, 2), 7.0, 9.8,
         "accent" if wheel_hi else "pad_body", chamfer=0.8, name="wheel")
    return cx, ys


def build_mouse(s, which):
    W, H, p = s.design, s.design, s.pad
    if which in ("left", "right"):
        mouse_body(12.0, p + 1, W - 12.0, H - p - 1, highlight=which)
    else:   # wheel: the wheel lit, arrows up and down beside it
        mouse_body(p + 3, p + 1, W - 15.5, H - p - 1, wheel_hi=True)
        ax = W - p - 4.5
        slab(triangle(ax, H / 2 + 7.0, 7.5, 90), 0.0, 3.0, "accent", chamfer=0.8, name="arrow")
        slab(triangle(ax, H / 2 - 7.0, 7.5, 270), 0.0, 3.0, "accent", chamfer=0.8, name="arrow")


# ---------------------------------------------------------------- gamepad


def face_button(s, letter, colour):
    W = H = s.design
    cx, cy = W / 2, H / 2
    R = W / 2 - s.pad - 0.5
    slab(ngon(cx, cy, R, 16), 0.0, 3.5, shade("pad_body", 0.8), chamfer=1.0, top="pad_body", name="housing")
    slab(ngon(cx, cy + 0.8, R - 2.4, 16), 2.0, 9.0, shade(colour, 0.76), chamfer=2.6, top=colour, name="button")
    inked_text(letter, 21.0, cx, cy + 0.8, 9.0, "cream", depth=0.6, max_h=R * 1.02, bevel=0.2, name="letter")


def shoulder(s, label, trigger=False):
    W = H = s.design
    p = s.pad
    x0, x1 = p + 1, W - p - 1
    xm = (x0 + x1) / 2
    if trigger:
        # a trigger seen from behind: flat bottom, straight sides, a rounded top
        y0, y1 = p + 1.0, H - p - 1.0
        hw = (x1 - x0) / 2 - 3.0
        yc = y1 - hw * 0.8
        pts = [V(xm - hw, y0 + 3.0), V(xm - hw + 3.0, y0), V(xm + hw - 3.0, y0), V(xm + hw, y0 + 3.0)]
        for k in range(0, 9):
            a = math.radians(0 + 180.0 * k / 8)
            pts.append(V(xm + hw * math.cos(a), yc + hw * 0.8 * math.sin(a)))
        slab(ccw(pts), 0.0, 8.0, shade("pad_body", 0.76), chamfer=2.4, top="pad_body", name="trigger")
        # letters as big as the shape allows: they must still read at the HUD's 26 px (decision 10)
        inked_text(label, 17.0, xm, (y0 + yc) / 2 + 2.0, 8.0, "cream", depth=0.5, max_w=2 * hw - 4, bevel=0.15,
                   name="label")
    else:
        y0, y1 = p + 5.5, H - p - 5.5
        slab(rrect(x0, y0, x1, y1, (y1 - y0) / 2 - 0.01, 4), 0.0, 7.0, shade("pad_body", 0.76), chamfer=2.4,
             top="pad_body", name="bumper")
        inked_text(label, 17.5, xm, (y0 + y1) / 2 + 0.5, 7.0, "cream", depth=0.5, max_w=(x1 - x0) - 9,
                   bevel=0.15, name="label")


def dpad(s, highlight):
    W = H = s.design
    cx, cy = W / 2, H / 2
    a = W / 2 - s.pad - 0.5     # arm length from the centre
    t = 7.0                     # half the arm width
    plus = [V(cx - t, cy - a), V(cx + t, cy - a), V(cx + t, cy - t), V(cx + a, cy - t), V(cx + a, cy + t),
            V(cx + t, cy + t), V(cx + t, cy + a), V(cx - t, cy + a), V(cx - t, cy + t), V(cx - a, cy + t),
            V(cx - a, cy - t), V(cx - t, cy - t)]
    slab(plus, 0.0, 7.0, shade("pad_body", 0.76), chamfer=1.8, top="pad_body", name="dpad")
    arms = {"up": (0, 1, 90), "down": (0, -1, 270), "right": (1, 0, 0), "left": (-1, 0, 180)}
    dx, dy, _ = arms[highlight]
    top = inset(plus, 1.8)
    part = clip(top, dx, dy, -(dx * cx + dy * cy) - t)
    slab(part, 6.6, 7.6, "accent", chamfer=0.5, name="lit")
    for ax, ay, ang in arms.values():
        slab(triangle(cx + ax * (a - 6.5), cy + ay * (a - 6.5), 6.2, ang), 7.0, 8.0,
             "cream", chamfer=0.3, name="arrow")
    slab(ngon(cx, cy, 3.2, 10), 6.6, 7.1, shade("pad_body", 0.66), name="dimple")


def stick(s, letter):
    W = H = s.design
    cx, cy = W / 2, H / 2
    R = W / 2 - s.pad - 0.5
    slab(ngon(cx, cy, R, 18), 0.0, 3.5, shade("pad_body", 0.8), chamfer=1.0, top="pad_body", name="well")
    slab(ngon(cx, cy + 0.6, R - 3.6, 16), 2.0, 10.0, "key_side", chamfer=2.2, top="key_top", name="cap")
    band(ngon(cx, cy + 0.6, R - 6.8, 16), ngon(cx, cy + 0.6, R - 8.0, 16), 10.0, 10.25,
         shade("key_top", 0.8), name="grip")
    text(letter, 15.0, cx, cy + 0.6, 10.0, INK, depth=0.4, bevel=0.1, name="letter")


def start_button(s):
    """The pad's menu button: three lines on a small round button."""
    W = H = s.design
    cx, cy = W / 2, H / 2
    R = W / 2 - s.pad - 2.5
    slab(ngon(cx, cy, R, 18), 0.0, 3.5, shade("pad_body", 0.8), chamfer=1.0, top="pad_body", name="well")
    slab(ngon(cx, cy + 0.5, R - 3.0, 18), 2.0, 8.0, shade("pad_body", 0.7), chamfer=2.0, top=shade("pad_body", 1.12),
         name="button")
    for dy in (-4.6, 0.0, 4.6):
        slab(rrect(cx - 7.5, cy + 0.5 + dy - 1.1, cx + 7.5, cy + 0.5 + dy + 1.1, 1.09, 1), 7.7, 8.7, "cream",
             chamfer=0.3, name="line")


# ---------------------------------------------------------------- icons (designed on 48)


def icon_money(s):
    W = H = s.design
    note = rrect(-13, -7.5, 13, 7.5, 1.2, 1)
    xf = Matrix.Translation((W / 2 - 3, H / 2 + 6, 0)) @ Matrix.Rotation(math.radians(14), 4, "Z")
    slab(note, 0.0, 1.4, "note_dark", chamfer=0.5, top="note", xf=xf, name="note")
    band(inset(note, 1.6), inset(note, 2.5), 1.35, 1.5, "note_dark", xf=xf, name="note_frame")
    slab(ngon(0, 0, 4.2, 10), 1.35, 1.55, "note_dark", xf=xf, name="note_seal")
    for (ox, count) in ((-7.5, 3), (7.0, 5)):
        for i in range(count):
            xfc = pose(0, 28, 0, at=(W / 2 + ox, H / 2 - 12 + i * 3.0, 6 + i * 2.2))
            slab(ngon(0, 0, 7.6, 12), 0.0, 2.4, "gold_dark", chamfer=0.7, top="gold", rough=0.4, spec=0.5, xf=xfc,
                 name="coin")
        xft = pose(0, 28, 0, at=(W / 2 + ox, H / 2 - 12 + (count - 1) * 3.0, 6 + (count - 1) * 2.2))
        band(ngon(0, 0, 5.8, 12), ngon(0, 0, 4.9, 12), 2.35, 2.55, "gold_dark", xf=xft, name="coin_rim")


def icon_clock(s):
    W = H = s.design
    cx, cy = W / 2, H / 2 - 1.5
    for sx in (-1, 1):
        slab(ngon(cx + sx * 10.5, cy + 13.0, 5.2, 10), 0.0, 5.0, "gold_dark", chamfer=1.6, top="gold", rough=0.4,
             spec=0.5, name="bell")
        slab(rrect(cx + sx * 9.5 - 1.6, cy - 17.5, cx + sx * 9.5 + 1.6, cy - 12, 0.8, 1), 0.0, 4.0, "iron",
             chamfer=0.5, name="leg")
    slab(ngon(cx, cy, 15.5, 16), 1.0, 9.0, shade("brick", 0.8), chamfer=2.6, top="brick", name="body")
    slab(ngon(cx, cy, 11.6, 16), 8.4, 9.6, "wall", chamfer=0.6, name="face")
    for k in range(12):
        a = math.radians(90 - 30 * k)
        r0, r1 = (8.2, 10.4) if k % 3 == 0 else (9.4, 10.4)
        w = 0.8 if k % 3 == 0 else 0.55
        q = [V(r0, -w), V(r1, -w), V(r1, w), V(r0, w)]
        slab(transform2(q, cx, cy, a), 9.6, 9.8, INK, name="tick")
    for ang, ln, w in ((125, 5.8, 1.3), (60, 8.6, 0.95)):   # 10:10
        q = [V(-1.4, -w), V(ln, -w * 0.6), V(ln, w * 0.6), V(-1.4, w)]
        slab(transform2(q, cx, cy, math.radians(ang)), 9.8, 10.3, INK, name="hand")
    slab(ngon(cx, cy, 1.4, 8), 10.2, 10.8, "gold", name="pin")
    slab(rrect(cx - 2.5, cy + 15.0, cx + 2.5, cy + 17.8, 1.0, 1), 0.0, 5.0, "gold_dark", chamfer=0.6, top="gold",
         name="knob")


MOODS = ("calm", "annoyed", "angry", "furious")


def icon_grandma(s, mood):
    """The grandmother's face on a badge in her mood's colour (MoodTier Sweet, Annoyed,
    Angry, Furious; Police is the end screen, not a HUD state)."""
    W = H = s.design
    cx, cy = W / 2, H / 2 - 1.0
    ring = "mood_" + mood
    slab(ngon(cx, cy, W / 2 - s.pad - 0.5, 20), 0.0, 3.0, shade(ring, 0.78), chamfer=1.0, top=ring, name="badge")
    skin = "skin_hot" if mood == "furious" else "skin"
    slab(ngon(cx, cy + 14.0, 5.6, 10), 2.5, 8.0, shade("hair", 0.85), chamfer=1.8, top="hair", name="bun")
    head = ngon(cx, cy - 1.0, 12.5, 16, 0.0, ry=13.5)
    slab(head, 3.0, 10.0, shade(skin, 0.86), chamfer=3.0, top=skin, name="head")
    hair = clip(ngon(cx, cy - 0.2, 13.4, 16, 0.0, ry=14.4), 0, 1, -(cy + 4.0))
    fringe = [V(cx + 12.8, cy + 4.0), V(cx + 6, cy + 6.5), V(cx, cy + 5.2), V(cx - 6, cy + 6.5), V(cx - 12.8, cy + 4.0)]
    hair = ccw([q for q in hair if q.y > cy + 4.0 + 1e-6] + fringe)
    slab(hair, 3.0, 11.2, shade("hair", 0.82), chamfer=2.2, top="hair", name="hair")
    ey = cy - 1.2
    if mood == "calm":
        for sx in (-1, 1):
            slab(ngon(cx + sx * 7.6, cy - 6.0, 2.4, 8, ry=1.6), 9.8, 10.2, "cheek", name="cheek")
    for sx in (-1, 1):
        ex = cx + sx * 4.8
        if mood == "calm":
            arc = [(ex + math.cos(math.radians(a)) * 2.0, ey - 0.6 + math.sin(math.radians(a)) * 1.6, 10.4)
                   for a in range(20, 161, 35)]
            tube(arc, 0.6, INK, sides=4, name="eye")
        elif mood == "annoyed":
            slab(ngon(ex, ey - 0.6, 1.4, 8, ry=1.0), 10.0, 10.5, INK, name="eye")
            slab(rrect(ex - 2.2, ey + 0.1, ex + 2.2, ey + 0.9, 0), 10.0, 10.6, INK, name="lid")
        else:
            slab(ngon(ex, ey, 1.5, 8), 10.0, 10.5, INK, name="eye")
    for sx in (-1, 1):
        band(ngon(cx + sx * 4.8, ey, 3.9, 12), ngon(cx + sx * 4.8, ey, 3.0, 12), 10.6, 11.3, "iron", name="rim")
    slab(rrect(cx - 1.0, ey + 0.4, cx + 1.0, ey + 1.1, 0), 10.6, 11.2, "iron", name="bridge")
    brow = {"calm": (8, -8), "annoyed": (0, -14), "angry": (-22, 22), "furious": (-32, 32)}[mood]
    for sx, ang in ((-1, brow[0]), (1, brow[1])):
        q = rrect(-2.5, -0.7, 2.5, 0.7, 0.0)
        slab(transform2(q, cx + sx * 4.9, ey + 4.9 + (0.8 if mood == "annoyed" and sx > 0 else 0.0),
                        math.radians(ang)), 11.0, 11.6, shade("hair", 0.55), name="brow")
    my = cy - 8.3
    if mood == "calm":
        arc = [(cx + math.cos(math.radians(a)) * 3.6, my + 1.8 + math.sin(math.radians(a)) * 2.6, 10.3)
               for a in range(200, 341, 28)]
        tube(arc, 0.65, INK, sides=4, name="mouth")
    elif mood == "annoyed":
        slab(transform2(rrect(-2.8, -0.6, 2.8, 0.6, 0), cx + 0.6, my + 0.3, math.radians(-8)), 10.0, 10.5, INK,
             name="mouth")
    elif mood == "angry":
        arc = [(cx + math.cos(math.radians(a)) * 3.4, my - 1.8 + math.sin(math.radians(a)) * 2.4, 10.3)
               for a in range(25, 156, 26)]
        tube(arc, 0.65, INK, sides=4, name="mouth")
    else:
        slab(ngon(cx, my, 3.2, 10, ry=2.4), 9.9, 10.5, "#5B2320", name="mouth")
        slab(ngon(cx, my - 1.2, 1.8, 8, ry=0.9), 10.4, 10.6, "cheek", name="tongue")
    if mood == "furious":
        for k in range(4):
            a = math.radians(45 + 90 * k)
            q = [V(1.0, -0.6), V(3.1, -0.6), V(3.1, 0.6), V(1.0, 0.6)]
            slab(transform2(q, cx + 9.5, cy + 9.0, a), 11.5, 12.2, "mood_furious", name="vein")
        for sx in (-1, 1):
            for k, (dx, dy, r) in enumerate(((13.0, 9.0, 2.4), (15.2, 12.8, 1.9), (15.8, 16.4, 1.5))):
                blob((cx + sx * dx, cy + dy, 12.0 + k), r, "smoke", name="steam")


def icon_pocket(s):
    W = H = s.design
    cx, cy = W / 2, H / 2
    pocket = transform2([V(-14, -5), V(0, -16), V(14, -5), V(14, 13), V(-14, 13)], cx, cy - 1.0)
    xf = Matrix.Translation((cx - 4.0, cy + 12.0, 0)) @ Matrix.Rotation(math.radians(-18), 4, "Z")
    slab(rrect(-8, -6, 8, 6, 0.8, 1), 0.0, 3.0, "note_dark", chamfer=0.5, top="note", xf=xf, name="note")
    slab(ngon(0, -1, 2.6, 8), 2.9, 3.1, "note_dark", xf=xf, name="note_seal")
    slab(ngon(cx + 6.5, cy + 15.0, 5.0, 12), 1.0, 4.5, "gold_dark", chamfer=1.4, top="gold", rough=0.35, spec=0.5,
         name="watch")
    slab(ngon(cx + 6.5, cy + 15.0, 3.3, 12), 4.2, 4.7, "wall", name="watch_face")
    slab(pocket, 4.0, 8.0, "denim_dark", chamfer=2.0, top="denim", name="pocket")
    hem = clip(inset(pocket, 2.0), 0, -1, cy + 7.0)
    hem = clip(hem, 0, 1, -cy)
    slab(hem, 7.6, 8.3, shade("denim", 0.9), name="hem")
    pts, _ = resample(inset(pocket, 2.6), True, 2.6)
    for i in range(0, len(pts) - 1, 2):
        a, b = pts[i], pts[i + 1]
        d = (b - a)
        slab(transform2(rrect(-0.8, -0.38, 0.8, 0.38, 0), (a.x + b.x) / 2, (a.y + b.y) / 2, math.atan2(d.y, d.x)),
             7.9, 8.5, "stitch", name="stitch")
    for x in np.arange(cx - 11.0, cx + 11.5, 2.6):
        slab(rrect(x - 0.8, cy + 4.6 - 0.38, x + 0.8, cy + 4.6 + 0.38, 0), 8.2, 8.7, "stitch", name="stitch")


def icon_key(s):
    W = H = s.design
    xf = Matrix.Translation((W / 2, H / 2, 0)) @ Matrix.Rotation(math.radians(40), 4, "Z")
    kw = dict(chamfer=1.1, rough=0.35, spec=0.55, xf=xf)
    band(ngon(-11.0, 0, 7.6, 12), ngon(-11.0, 0, 3.6, 12), 0.0, 4.5, "brass", xf=xf, name="bow")
    slab(rrect(-4.5, -2.7, -2.0, 2.7, 0.8, 1), 0.0, 4.4, shade("brass", 0.85), top="brass", name="collar", **kw)
    slab(rrect(-4.0, -1.6, 15.0, 1.6, 0.5, 1), 0.0, 3.4, shade("brass", 0.85), top="brass", name="shaft", **kw)
    bit = [V(8.0, -1.4), V(15.0, -1.4), V(15.0, -7.2), V(12.8, -7.2), V(12.8, -4.6), V(10.8, -4.6), V(10.8, -6.3),
           V(8.0, -6.3)]
    slab(bit, 0.0, 3.4, shade("brass", 0.85), top="brass", name="bit", **kw)


def icon_truck(s):
    W = H = s.design
    xf = pose(yaw=-24, pitch=14, at=(W / 2 + 1.0, H / 2 - 5.0, 20), scale=0.92)
    slab(rrect(-19, -7, 6, 7, 1.0, 1), 3.0, 22.0, shade("truck_body", 0.84), chamfer=1.4, top="truck_body",
         name="cargo", xf=xf)
    slab(rrect(6.5, -6.5, 15, 6.5, 1.0, 1), 3.0, 15.0, shade("truck_cab", 0.8), chamfer=1.4, top="truck_cab",
         name="cab", xf=xf)
    slab(rrect(15, -6.5, 20, 6.5, 1.0, 1), 3.0, 9.5, shade("truck_cab", 0.8), chamfer=1.2, top="truck_cab",
         name="hood", xf=xf)
    fr = face_matrix((Vector((0, -7.0, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))))
    slab(rrect(-18, 9.5, 5, 12.5, 0), 0.0, 0.3, "brick", xf=xf @ fr, name="stripe")
    frc = face_matrix((Vector((0, -6.5, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))))
    slab(rrect(8.5, 9.0, 14.0, 13.5, 0.8, 1), 0.0, 0.35, "glass", rough=0.2, spec=0.7, xf=xf @ frc, name="window")
    for x in (-12.0, 12.5):
        wm = xf @ Matrix.Translation((x, -6.6, 4.2)) @ Matrix.Rotation(math.radians(90), 4, "X")
        slab(ngon(0, 0, 4.4, 10), -1.0, 3.0, "tyre", chamfer=0.8, xf=wm, name="tyre")
        slab(ngon(0, 0, 1.9, 8), 2.6, 3.4, "metal", xf=wm, name="hub")


def icon_box(s):
    W = H = s.design
    moving_box(pose(yaw=32, pitch=26, at=(W / 2 - 0.5, H / 2 - 12.5, 12), scale=1.0))


def icon_cigarette(s):
    W = H = s.design
    xf = Matrix.Translation((W / 2 - 3.0, H / 2 - 6.0, 6.0)) @ Matrix.Rotation(math.radians(24), 4, "Z")
    L = 30.0
    r = 2.9
    tube([(-L / 2, 0, 0), (-L / 2 + 9, 0, 0)], r, "filter", sides=8, cap_colour="filter", xf=xf, name="filter")
    tube([(-L / 2 + 9, 0, 0), (L / 2 - 2.2, 0, 0)], r, "wall", sides=8, cap_colour="wall", xf=xf, name="paper")
    tube([(L / 2 - 2.2, 0, 0), (L / 2 - 0.8, 0, 0)], r * 0.96, "ash", sides=8, cap_colour="ember", xf=xf, name="ash")
    tube([(L / 2 - 0.8, 0, 0), (L / 2, 0, 0)], r * 0.9, "ember", sides=8, cap_colour="ember", xf=xf, emit=1.2,
         name="ember")
    tip = xf @ Vector((L / 2, 0, 0))
    for k, (dx, dy, rr) in enumerate(((0.5, 5.0, 2.6), (-1.8, 9.5, 3.2), (1.2, 14.5, 3.6))):
        blob((tip.x + dx, tip.y + dy, 12.0 + k), rr, "smoke", subdiv=1, name="smoke")


def icon_beer(s):
    W = H = s.design
    xf = pose(yaw=0, pitch=10, roll=-12, at=(W / 2 + 1.0, H / 2 - 16.5, 10), scale=1.0)
    prof = [(0.0, 0.0, 0), (6.4, 0.0, 0), (7.0, 1.0, 0), (7.0, 15.0, 0), (5.8, 19.5, 0), (3.0, 23.0, 0),
            (2.7, 29.0, 0), (3.1, 29.6, 1), (3.1, 31.6, 1), (0.0, 31.6, 1)]
    lathe(prof, 10, ["beer_glass", "gold"], xf=xf, rough=0.25, spec=0.6, name="bottle")
    lathe([(0.0, 3.9, 0), (7.25, 3.9, 0), (7.25, 12.6, 0), (0.0, 12.6, 0)], 10, ["label"], xf=xf, name="label")
    lathe([(0.0, 22.8, 0), (3.15, 22.8, 0), (2.9, 26.5, 0), (0.0, 26.5, 0)], 10, ["gold"], xf=xf, rough=0.4,
          name="neck_label")


def icon_grenade(s):
    W = H = s.design
    xf = pose(yaw=0, pitch=18, roll=14, at=(W / 2 - 1.5, H / 2 - 16.0, 10), scale=1.0)
    prof = [(0.0, 0.0, 0)]
    rows = [(2.0, 1.0), (5.6, 3.0), (6.4, 4.2), (5.9, 5.4), (7.9, 6.6), (8.3, 9.0), (7.7, 10.2), (8.4, 11.4),
            (8.2, 14.0), (7.4, 15.2), (7.8, 16.4), (7.0, 18.6), (5.8, 19.8), (6.0, 21.0), (4.0, 23.0)]
    for r, z in rows:
        prof.append((r, z, 0))
    prof += [(3.0, 23.2, 1), (3.0, 26.5, 1), (0.0, 26.5, 1)]
    lathe(prof, 8, ["olive", "iron"], xf=xf, rough=0.7, name="grenade")
    spoon = [(2.0, -3.4, 26.2), (4.2, -4.6, 24.5), (7.8, -4.8, 20.0), (9.4, -4.4, 14.0), (9.0, -3.8, 10.5)]
    tube(spoon, 1.25, "metal", sides=4, phase=math.pi / 4, xf=xf, rough=0.4, spec=0.5, name="spoon")
    ring = [(-5.0 + 3.6 * math.cos(2 * math.pi * k / 12), -5.2, 24.0 + 3.6 * math.sin(2 * math.pi * k / 12))
            for k in range(12)]
    tube(ring, 0.8, "metal", sides=5, closed=True, xf=xf, rough=0.4, spec=0.5, name="pin")
    tube([(-1.5, -3.0, 24.5), (-2.2, -4.6, 24.2)], 0.6, "metal", sides=5, xf=xf, name="pin_shaft")


def icon_crate(s):
    W = H = s.design
    xf = pose(yaw=-30, pitch=24, at=(W / 2 + 0.5, H / 2 - 12.5, 12), scale=0.97)
    sx, sy, sz = 25.0, 20.0, 17.0
    slab(rrect(-sx / 2 + 0.6, -sy / 2 + 0.6, sx / 2 - 0.6, sy / 2 - 0.6, 0), 0.4, sz - 0.4, "wood_dark",
         xf=xf, name="core")
    faces = box_faces(sx, sy, sz)
    for face, (fw, fh) in (("front", (sx, sz)), ("right", (sy, sz)), ("left", (sy, sz))):
        m = xf @ face_matrix(faces[face])
        for i in range(3):
            h = fh / 3
            ya = -fh / 2 + i * h
            slab(rrect(-fw / 2, ya + 0.35, fw / 2, ya + h - 0.35, 0), -0.6, 0.6, "wood_warm", chamfer=0.5,
                 top=("wood_light" if i % 2 else "wood"), xf=m, name="board")
        for x in (-fw / 2 + 1.6, fw / 2 - 1.6):
            slab(rrect(x - 1.6, -fh / 2, x + 1.6, fh / 2, 0), 0.5, 1.6, "wood_warm", chamfer=0.5, top="wood_pale",
                 xf=m, name="post")
        if face == "front":
            d = (V(fw / 2 - 3.2, fh / 2 - 1.5) - V(-fw / 2 + 3.2, -fh / 2 + 1.5))
            slab(transform2(rrect(-d.length / 2, -1.5, d.length / 2, 1.5, 0), 0, 0, math.atan2(d.y, d.x)), 0.5, 1.5,
                 "wood_warm", chamfer=0.45, top="wood_pale", xf=m, name="brace")
    top = xf @ face_matrix(faces["top"])
    for i in range(4):
        w = sy / 4
        ya = -sy / 2 + i * w
        slab(rrect(-sx / 2, ya + 0.3, sx / 2, ya + w - 0.3, 0), -0.6, 0.6, "wood_warm", chamfer=0.4,
             top=("wood_pale" if i % 2 else "wood_light"), xf=top, name="lid")
    for x in (-sx / 2 + 1.5, sx / 2 - 1.5):
        for i in range(4):
            slab(ngon(x, -sy / 2 + (i + 0.5) * sy / 4, 0.7, 6), 0.5, 0.9, "iron", xf=top, name="nail")


def icon_warning(s):
    W = H = s.design
    cx, cy = W / 2, H / 2 - 1.5
    tri = ngon(cx, cy, 20.5, 3, math.radians(90))
    slab(tri, 0.0, 4.0, INK, chamfer=1.2, name="rim")
    slab(inset(tri, 3.0), 2.0, 7.0, shade("sign_yellow", 0.8), chamfer=2.0, top="sign_yellow", name="face")
    slab(rrect(cx - 1.8, cy - 2.5, cx + 1.8, cy + 8.5, 1.2, 1), 7.0, 7.8, INK, chamfer=0.3, name="bang")
    slab(ngon(cx, cy - 6.0, 2.0, 8), 7.0, 7.8, INK, chamfer=0.3, name="dot")


def icon_check(s):
    W = H = s.design
    q = [V(-12, 1), V(-7.5, 5.5), V(-3.5, 1.5), V(9.5, 14.5), V(14, 10), V(-3.5, -7.5)]
    slab(transform2(q, W / 2 - 1.0, H / 2 - 2.5), 0.0, 6.0, shade("ok_green", 0.74), chamfer=2.2, top="ok_green",
         name="check")


def icon_cross(s):
    W = H = s.design
    a, t = 13.5, 3.7
    arm = [V(-a, -t), V(a, -t), V(a, t), V(-a, t)]
    for ang in (45, -45):
        slab(transform2(arm, W / 2, H / 2, math.radians(ang)), 0.0, 6.0 if ang > 0 else 6.2,
             shade("no_red", 0.74), chamfer=2.0, top="no_red", name="cross")


# ---------------------------------------------------------------- indicators and small marks


def build_indicator_arrow(s):
    """A chunky arrowhead pointing right (+X), the package's rotation-0 direction (Target
    Indicators: 'rotation relative to the right vector'). White, to tint (decision 8)."""
    W, H = s.w, s.h
    cx, cy = W / 2, H / 2
    head = [V(22, 0), V(-15, 19), V(-8, 0), V(-15, -19)]     # tip, back corner, notch, back corner
    slab(transform2(head, cx + 1.0, cy), 0.0, 8.0, "white_side", chamfer=3.2, top="white", rough=0.6, name="arrow")


def build_indicator_ring(s):
    """A faceted ring (a low-poly torus), white, to tint: marks a crew mate on screen."""
    W, H = s.w, s.h
    cx, cy = W / 2, H / 2
    R, r = 17.0, 5.2
    path = [(cx + R * math.cos(2 * math.pi * k / 20), cy + R * math.sin(2 * math.pi * k / 20), 6.0) for k in range(20)]
    tube(path, r, "white", sides=6, closed=True, rough=0.6, name="ring")


def build_compass_tape_bg(s):
    """A dark wooden strip for the compass, rope wrapped at both ends; its centre is uniform
    so it stretches to any width."""
    W, H = s.w, s.h
    y0, y1 = 7.0, H - 7.0
    slab(rrect(4, y0, W - 4, y1, 6.5, 2), 0.0, 6.0, "bark_dark", chamfer=1.4, top="wood_dark", name="strip")
    grain_lines(14, y0, W - 14, y1, 6.0, shade("wood_dark", 0.82), 2, horizontal=True)
    for cx in (17.0, W - 17.0):
        for k in range(3):
            x = cx + (k - 1) * 2.4
            path = [(x - 1.0, y0 - 0.5, 3.0), (x - 0.4, y0 + 2.0, 6.6), (x + 0.4, y1 - 2.0, 6.6), (x + 1.0, y1 + 0.5, 3.0)]
            tube(path, 1.15, "rope" if k % 2 == 0 else "rope_dark", sides=5, name="wrap")
    for cx in (8.5, W - 8.5):
        nail(cx, H / 2, 6.3, r=1.7)


def build_compass_tick(s):
    W, H = s.w, s.h
    slab(rrect(W / 2 - 2.2, 4.0, W / 2 + 2.2, H - 4.0, 2.0, 2), 0.0, 3.0, "white_side", chamfer=1.0, top="white",
         rough=0.6, name="tick")


def build_crosshair_dot(s):
    W, H = s.w, s.h
    slab(ngon(W / 2, H / 2, 3.4, 12), 0.0, 2.4, shade("cream", 0.85), chamfer=0.9, top="cream", name="dot")


# ---------------------------------------------------------------- logo


def islands(bm):
    """Groups of faces connected by edges: the letters of a text mesh."""
    seen = set()
    groups = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack = [f]
        seen.add(f.index)
        group = []
        while stack:
            g = stack.pop()
            group.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        groups.append(group)
    return groups


def nail_spot(bm, depth, r):
    """A point on a letter's front face with room for a nail head of radius r round it,
    found by casting rays down at the letter (near its upper middle first), or None."""
    from mathutils.bvhtree import BVHTree
    bm.normal_update()
    tree = BVHTree.FromBMesh(bm)
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    top = max(v.co.z for v in bm.verts)
    cands = [(cx + dx * (max(xs) - min(xs)) * 0.5, cy + dy * (max(ys) - min(ys)) * 0.5)
             for dy in (0.62, 0.45, 0.25, -0.5, 0.0) for dx in (0.0, -0.35, 0.35, -0.6, 0.6)]
    for qx, qy in cands:
        ok = True
        hit_z = None
        for ox, oy in ((0, 0), (r, 0), (-r, 0), (0, r), (0, -r)):
            loc, nrm, _, _ = tree.ray_cast(Vector((qx + ox, qy + oy, top + 5.0)), Vector((0, 0, -1)))
            if loc is None or nrm.z < 0.95 or loc.z < top - 0.05 * depth - 0.2:
                ok = False
                break
            if hit_z is None:
                hit_z = loc.z
        if ok:
            return Vector((qx, qy, hit_z))
    return None


def block_word(word, size, x, y, depth, colours, tilt=-24.0, jitter=4.0, bounce=1.6, weight="Bold",
               spacing=0.04, nails=False, name="word"):
    """Wooden block letters: the word in Fredoka, extruded and chamfered, cut into one object
    per letter, each nudged and turned a few degrees like letters nailed up by hand, then the
    whole word tipped back by `tilt` so the lower sides of the blocks show. Returns the
    world-space bounds of the word (x0, y0, x1, y1)."""
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = word
    cu.font = font(weight)
    cu.size = size
    cu.space_character = 1.0 + spacing
    cu.extrude = depth * 0.5
    cu.bevel_depth = depth * 0.16
    cu.bevel_resolution = 0
    cu.resolution_u = 4
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    tmp = bpy.data.objects.new(name + "_src", cu)
    COL.objects.link(tmp)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp, do_unlink=True)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    groups = islands(bm)
    groups.sort(key=lambda g: sum(v.co.x for f in g for v in f.verts) / max(1, sum(len(f.verts) for f in g)))
    tip = Matrix.Translation((x, y, 0.0)) @ Matrix.Rotation(math.radians(tilt), 4, "X")
    xs, ys = [], []
    for i, g in enumerate(groups):
        verts = sorted({v for f in g for v in f.verts}, key=lambda v: v.index)
        index = {v: k for k, v in enumerate(verts)}
        lb = bmesh.new()
        lv = [lb.verts.new(v.co + Vector((0.0, 0.0, depth * 0.5))) for v in verts]
        for f in g:
            try:
                lb.faces.new([lv[index[v]] for v in f.verts])
            except ValueError:
                pass
        c = sum((v.co for v in lb.verts), Vector()) / max(1, len(lb.verts))
        c.z = 0.0
        turn = Matrix.Rotation(math.radians(random.uniform(-jitter, jitter)), 4, "Z")
        lift = Vector((random.uniform(-0.6, 0.6), random.uniform(-bounce, bounce), 0.0))
        xf = tip @ Matrix.Translation(c + lift) @ turn @ Matrix.Translation(-c)
        col = colours[i % len(colours)]
        spot = nail_spot(lb, depth, size * 0.05) if nails else None
        ob = mesh_object("%s_%d" % (name, i), lb, [mat(col, 0.75, 0.25)], xf)
        if spot is not None:
            nail(spot.x, spot.y, spot.z + 0.3, r=size * 0.028, xf=xf)
        for v in ob.data.vertices:
            w = ob.matrix_world @ v.co
            xs.append(w.x)
            ys.append(w.y)
    bm.free()
    bpy.data.meshes.remove(me)
    return (min(xs), min(ys), max(xs), max(ys))


def build_logo(s):
    """THE MOVERS in wooden block letters, 'THE' small and cream-painted on a plank, a taped
    moving box leaning on the S."""
    W, H = s.w, s.h
    cx = W / 2 - 24.0
    b = block_word("MOVERS", 116.0, cx, H * 0.40, 20.0, ("honey", "honey_light", "honey_dark", "honey_light"),
                   spacing=0.09, nails=True, name="movers")
    x0, y0, x1, y1 = b
    # the small word on its own plank, up and to the left, tipped a little
    tb = block_word("THE", 44.0, x0 + 66.0, y1 + 24.0, 10.0, ("cream", "paper", "cream"), jitter=6.0, bounce=1.0,
                    spacing=0.06, name="the")
    px0, py0, px1, py1 = tb
    slab(rrect(px0 - 10, py0 - 8, px1 + 10, py0 + 5, 2.5, 1), -7.0, -1.0, "bark", chamfer=1.2, top="wood_dark",
         name="the_plank")
    for nx in (px0 - 5.0, px1 + 5.0):
        nail(nx, py0 - 1.5, -1.0, r=2.0)
    # a box leaning against the last letter
    moving_box(pose(yaw=-22, pitch=18, roll=-9, at=(x1 + 25.0, y0 + 2.0, 34.0), scale=1.95), arrows=True)


# ---------------------------------------------------------------- the paper texture (not rendered)


def paper_texture(size, seed=7):
    """Seamless warm paper: 1/f mottling and fine grain from a periodic FFT filter, plus a
    few short fibres drawn with wrap-around, so the tile has no seam by construction
    (decision 8). Returns an (h, w, 4) straight-alpha float array, row 0 at the bottom."""
    rng = np.random.default_rng(seed)
    n = size
    fy = np.fft.fftfreq(n)[:, None]
    fx = np.fft.fftfreq(n)[None, :]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0

    def field(power, lo, hi):
        spec_ = np.fft.fft2(rng.standard_normal((n, n)))
        filt = (1.0 / f ** power) * ((f >= lo) & (f <= hi))
        out = np.real(np.fft.ifft2(spec_ * filt))
        out -= out.mean()
        return out / (out.std() + 1e-9)

    mottle = field(1.6, 1.0 / n, 0.08)
    grain = field(0.3, 0.12, 0.5)
    # fibres: short, gently curved strokes, lighter or darker
    fib = np.zeros((n, n))
    for _ in range(int(n * 0.9)):
        x, y = rng.uniform(0, n, 2)
        a = rng.uniform(0, math.pi)
        L = rng.uniform(6, 22)
        bend = rng.uniform(-0.03, 0.03)
        sgn = 1.0 if rng.random() < 0.55 else -1.0
        for t in np.linspace(0, L, int(L * 1.5)):
            aa = a + bend * t
            xi = int(x + math.cos(aa) * t) % n
            yi = int(y + math.sin(aa) * t) % n
            fib[yi, xi] += sgn
    base = np.array(hex_rgb("paper"), dtype=np.float64)
    lum = 1.0 + 0.013 * mottle + 0.014 * grain + 0.030 * np.clip(fib, -1.5, 1.5)
    warm = 0.006 * mottle   # the darker blotches are a touch warmer, as old paper is
    rgb = np.stack([base[0] * lum + warm * 0.4, base[1] * lum, base[2] * lum - warm * 0.6], -1)
    # a few specks
    for _ in range(int(n * 0.05)):
        x, y = rng.integers(0, n, 2)
        r = rng.uniform(0.6, 1.4)
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                if dx * dx + dy * dy <= r * r:
                    rgb[(y + dy) % n, (x + dx) % n] *= 0.88
    out = np.ones((n, n, 4), dtype=np.float32)
    out[..., :3] = np.clip(rgb, 0, 1)
    return out


# ----------------------------------------------------------------------------- registry

WOOD_CONTENT = LBRT(34, 34, 34, 34)


def register():
    # frames: the UI stretches them; borders in 1x pixels (texture = x2)
    spec("frame_wood", "frames", 256, 256, lambda s: build_frame_wood(s, False), kind="sliced", pad=8,
         border=LBRT(40, 40, 40, 40), content=WOOD_CONTENT, outline=4.0, text="ink",
         desc="Pale board in four wooden battens lashed with rope: the default panel, dark ink text.")
    spec("frame_wood_dark", "frames", 256, 256, lambda s: build_frame_wood(s, True), kind="sliced", pad=8,
         border=LBRT(40, 40, 40, 40), content=WOOD_CONTENT, outline=4.0, text="cream",
         desc="Walnut board in dark battens lashed with rope: HUD panels over the scene, cream text.")
    spec("frame_branch", "frames", 256, 256, build_frame_branch, kind="sliced", pad=8,
         border=LBRT(53, 53, 53, 53), content=LBRT(40, 40, 40, 40), outline=4.0,
         desc="Four lashed branches, leaves and berries at the corners, transparent inside: the title frame. "
              "A slice scale of 0.75 to 1 gives a lusher frame on the title screen.")
    # face: the flap's sides are plain cardboard from 9 px in (its outline ends there, measured
    # on the PNG); the content rect keeps text blocks 20 px in for comfort, and a one-word pocket
    # caption may use the rest (UICORE's PocketsView pads its sides 4 px at 0.2 = 10 px here)
    spec("frame_cardboard", "frames", 256, 256, build_frame_cardboard, kind="sliced", pad=8,
         border=LBRT(28, 26, 28, 30), content=LBRT(20, 18, 20, 30), face=LBRT(10, 18, 10, 30), outline=4.0,
         text="ink",
         desc="A box flap with packing tape across the top and a 'this side up' stamp: lists, contract, pockets.")
    spec("frame_tag", "frames", 256, 64, build_frame_tag, kind="sliced", pad=5,
         border=LBRT(34, 14, 10, 22), content=LBRT(34, 10, 12, 9), outline=4.0, shadow=(1, -2, 2, 0.28),
         text="ink", desc="A paper luggage label with a red string: tooltips, interaction prompts, speech bubbles. "
                          "Keep children out of the left 34 px (the hole and the string).")
    spec("button_wood", "buttons", 256, 72, build_button_wood, kind="sliced", pad=8, states=BUTTON_STATES,
         border=LBRT(40, 22, 40, 22), content=LBRT(36, 13, 36, 12), outline=4.0, text="cream",
         desc="A thick nailed plank: the menu button, four states from one render (decision 6).")
    spec("bar_tape_bg", "bars", 256, 40, build_bar_tape_bg, kind="sliced", pad=4, stretch="x",
         border=LBRT(52, 19, 18, 19), content=LBRT(46, 12, 11, 9), outline=5.0, shadow=(2, -3, 2, 0.30),
         desc="The tape measure's case and the slot the tape runs in. Stretch the width only and scale it as a "
              "whole to the bar's height (slice scale = height / 80: 0.3 for a 24 px bar); the fill goes in the "
              "content rect.")
    spec("bar_tape_fill", "bars", 150, 20, build_bar_tape_fill, kind="sliced", pad=0, stretch="x",
         border=LBRT(4, 0, 18, 0), outline=0.0, shadow=None, slice_mode="tiled", tile_period=32,
         desc="The yellow tape with its brass hook, as tall as the bar's groove at the same density. Draw it at "
              "the bar's slice scale and tile the centre (UI Toolkit SliceType.Tiled, uGUI Tiled): 4 whole tick "
              "periods, seamless; stretched, the ticks blur.")
    # keys: blank, the UI writes the name (decision 7)
    spec("key_blank", "keys", 64, 64, build_keycap, kind="sliced", pad=5, border=LBRT(12, 14, 12, 12),
         content=LBRT(9, 12, 9, 8), outline=4.0, shadow=(1, -2, 2, 0.30), text="ink",
         desc="A chunky key cap, blank: letters and digits, Fredoka SemiBold in ink on its top face.")
    spec("key_wide", "keys", 128, 64, build_keycap, kind="sliced", pad=5, border=LBRT(12, 14, 12, 12),
         content=LBRT(9, 12, 9, 8), outline=4.0, shadow=(1, -2, 2, 0.30), text="ink",
         desc="The same cap, wide: Espace, Maj, Ctrl, Echap, Tab. Same borders as key_blank.")
    for which in ("left", "right", "wheel"):
        spec("mouse_" + which, "keys", 64, 64, (lambda s, w=which: build_mouse(s, w)), design=48, pad=8,
             desc="Mouse, the %s lit in orange." % ("wheel" if which == "wheel" else which + " button"))
    # gamepad, Xbox layout as GamepadSource reads it
    for letter in "abxy":
        spec("pad_" + letter, "pad", 64, 64, (lambda s, l=letter: face_button(s, l.upper(), "pad_" + l)), design=48, pad=8,
             desc="Pad face button %s." % letter.upper())
    for name, trig in (("lb", False), ("rb", False), ("lt", True), ("rt", True)):
        spec("pad_" + name, "pad", 64, 64, (lambda s, n=name, t=trig: shoulder(s, n.upper(), t)), design=48, pad=8,
             desc="Pad %s %s." % ("trigger" if trig else "bumper", name.upper()))
    for d in ("up", "down", "left", "right"):
        spec("pad_dpad_" + d, "pad", 64, 64, (lambda s, d=d: dpad(s, d)), design=48, pad=8,
             desc="Pad d-pad, %s lit." % d)
    spec("pad_ls", "pad", 64, 64, lambda s: stick(s, "L"), design=48, pad=8, desc="Left stick (move; click: sprint).")
    spec("pad_rs", "pad", 64, 64, lambda s: stick(s, "R"), design=48, pad=8, desc="Right stick (look).")
    spec("pad_start", "pad", 64, 64, start_button, design=48, pad=8, desc="Pad menu / Start button (pause).")
    icons = [("money", icon_money), ("clock", icon_clock), ("truck", icon_truck), ("box", icon_box),
             ("key", icon_key), ("pocket", icon_pocket), ("cigarette", icon_cigarette), ("beer", icon_beer),
             ("grenade", icon_grenade), ("crate", icon_crate), ("warning", icon_warning), ("check", icon_check),
             ("cross", icon_cross)]
    for n, fn in icons:
        spec("icon_" + n, "icons", 64, 64, fn, design=48, pad=8, desc="Icon: %s." % n)
    for m in MOODS:
        spec("icon_grandma_" + m, "icons", 64, 64, (lambda s, m=m: icon_grandma(s, m)), design=48, pad=8,
             desc="The grandmother, %s (MoodTier %s)." % (m, "Sweet" if m == "calm" else m.capitalize()))
    spec("indicator_arrow", "indicators", 64, 64, build_indicator_arrow, pad=8, outline=6.0, tint=True,
         desc="Off-screen arrow, points right (+X) at rotation 0, white: tint with the crew colour.")
    spec("indicator_ring", "indicators", 64, 64, build_indicator_ring, pad=8, outline=6.0, tint=True,
         desc="On-screen marker ring, white: tint with the crew colour.")
    spec("compass_tape_bg", "indicators", 256, 36, build_compass_tape_bg, kind="sliced", pad=6,
         border=LBRT(24, 14, 24, 14), content=LBRT(26, 10, 26, 10), outline=4.0, shadow=(2, -3, 2, 0.3),
         stretch="x", text="cream", desc="The compass strip, dark wood with rope at both ends; stretch the width.")
    spec("compass_tick", "indicators", 12, 28, build_compass_tick, pad=3, outline=3.0, shadow=None, tint=True,
         desc="A compass tick, white: tint it; place every 15 degrees, N/E/S/W in Fredoka.")
    spec("crosshair_dot", "indicators", 16, 16, build_crosshair_dot, pad=4, outline=3.0, shadow=None,
         desc="The resting crosshair.")
    spec("logo_the_movers", "title", 512, 256, build_logo, pad=12, outline=8.0, shadow=(4, -8, 5, 0.35), trim=True,
         desc="The title logo: THE MOVERS in wooden block letters, a moving box leaning on the S.")
    spec("bg_paper", "title", 256, 256, None, kind="tiled", pad=0, outline=0.0, shadow=None,
         desc="Warm paper, seamless: tile it (wrap Repeat) behind lists and the controls sheet.")


# ----------------------------------------------------------------------------- scene


SC = None
CAM = None


def setup_scene(style="eevee"):
    global SC, CAM, COL
    SC = bpy.context.scene
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    if style == "workbench":
        SC.render.engine = "BLENDER_WORKBENCH"
        sh = SC.display.shading
        sh.light = "FLAT"
        sh.color_type = "MATERIAL"
        sh.show_object_outline = True
        sh.object_outline_color = to_linear(hex_rgb(INK))
        sh.show_cavity = False
        try:
            SC.display.render_aa = "32"
        except TypeError:
            pass
    else:
        try:
            SC.render.engine = "BLENDER_EEVEE"
        except TypeError:
            SC.render.engine = "BLENDER_EEVEE_NEXT"
        SC.eevee.taa_render_samples = 48
    SC.render.film_transparent = True
    try:
        SC.view_settings.view_transform = "Standard"
    except TypeError:
        print("[ui_kit] WARNING: no Standard view transform, colours will be off")
    SC.view_settings.look = "None"
    SC.view_settings.exposure = 0.0
    SC.view_settings.gamma = 1.0
    SC.render.image_settings.file_format = "PNG"
    SC.render.image_settings.color_mode = "RGBA"
    SC.render.image_settings.color_depth = "8"
    SC.render.resolution_percentage = 100
    SC.render.filter_size = 1.0
    world = bpy.data.worlds.new("UIWorld")
    if world.node_tree is None:
        world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (*to_linear(hex_rgb(AMBIENT_TINT)), 1.0)
    bg.inputs[1].default_value = AMBIENT
    SC.world = world
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = SUN_E
    sun.angle = math.radians(SUN_ANGLE)
    sun.color = (1.0, 0.97, 0.92)
    sob = bpy.data.objects.new("Sun", sun)
    sob.rotation_euler = Vector(SUN_DIR).normalized().to_track_quat("-Z", "Y").to_euler()
    SC.collection.objects.link(sob)
    cd = bpy.data.cameras.new("Cam")
    cd.type = "ORTHO"
    cd.clip_start = 1.0
    cd.clip_end = 2000.0
    CAM = bpy.data.objects.new("Cam", cd)
    SC.collection.objects.link(CAM)
    SC.camera = CAM
    COL = bpy.data.collections.new("Sprite")
    SC.collection.children.link(COL)


def clear_sprite():
    for ob in list(COL.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.images):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)


def frame_camera(w, h, scale=SCALE):
    SC.render.resolution_x = int(round(w * scale))
    SC.render.resolution_y = int(round(h * scale))
    CAM.data.ortho_scale = max(w, h)
    CAM.location = (w / 2.0, h / 2.0, 800.0)
    CAM.rotation_euler = (0.0, 0.0, 0.0)


def render_to(path):
    SC.render.filepath = path
    bpy.ops.render.render(write_still=True)


def build_sprite(s):
    """Build a spec's geometry at its canvas size (scaling a design-size build)."""
    clear_sprite()
    random.seed(zlib.crc32(s.name.encode()))
    design_pad = s.pad
    if s.design:
        k = s.w / float(s.design)
        s.pad = design_pad * s.design / float(s.w)   # builders read the pad in design units
        s.build(s)
        s.pad = design_pad
        m = Matrix.Scale(k, 4)
        for ob in COL.objects:
            ob.matrix_world = m @ ob.matrix_world
    else:
        s.build(s)
    bpy.context.view_layer.update()


# ----------------------------------------------------------------------------- post (numpy)


def load_px(path):
    """A PNG as floats, (h, w, 4), row 0 at the bottom, the stored sRGB values unchanged."""
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)


def save_png(path, arr):
    """Write straight-alpha RGBA floats (row 0 at the bottom) as an 8-bit PNG, with zlib:
    no colour management between the numbers here and the bytes in the file."""
    h, w, _ = arr.shape
    data = (np.clip(arr[::-1], 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)
    raw = b"".join(b"\x00" + data[y].tobytes() for y in range(h))

    def chunk(tag, body):
        c = struct.pack(">I", len(body)) + tag + body
        return c + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def shift(a, dx, dy):
    """Shift a 2D array by whole pixels, x right, y up (row 0 is the bottom), zero fill."""
    out = np.zeros_like(a)
    h, w = a.shape
    xs0, xs1 = max(0, -dx), min(w, w - dx)
    ys0, ys1 = max(0, -dy), min(h, h - dy)
    if xs1 > xs0 and ys1 > ys0:
        out[ys0 + dy:ys1 + dy, xs0 + dx:xs1 + dx] = a[ys0:ys1, xs0:xs1]
    return out


def dilate(a, r):
    """Soft disc dilation of an alpha map: an anti-aliased outline of radius r pixels."""
    R = int(math.ceil(r + 1))
    out = np.zeros_like(a)
    for dy in range(-R, R + 1):
        for dx in range(-R, R + 1):
            wgt = min(1.0, max(0.0, r + 0.5 - math.hypot(dx, dy)))
            if wgt <= 0:
                continue
            np.maximum(out, shift(a, dx, dy) * wgt, out=out)
    return out


def box_blur(a, r):
    if r <= 0:
        return a
    k = 2 * r + 1
    for axis in (0, 1):
        p = np.pad(a, [(r, r) if i == axis else (0, 0) for i in range(2)], mode="constant")
        c = np.cumsum(p, axis=axis, dtype=np.float64)
        c = np.concatenate([np.zeros_like(np.take(c, [0], axis=axis)), c], axis=axis)
        a = ((np.take(c, range(k, c.shape[axis]), axis=axis) - np.take(c, range(0, c.shape[axis] - k), axis=axis))
             / k).astype(np.float32)
    return a


def over(dst_rgb, dst_a, src_rgb, src_a):
    """Premultiplied 'src over dst'."""
    return src_rgb * src_a[..., None] + dst_rgb * (1 - src_a[..., None]), src_a + dst_a * (1 - src_a)


def finish(px, s, state=None):
    """Outline and shadow (decision 4), and the button states (decision 6)."""
    rgb = px[..., :3].copy()
    a = px[..., 3].copy()
    outline = [(INK, s.outline)] if s.outline > 0 else []
    shadow = s.shadow
    dy_state = 0
    if state == "hover":
        rgb = np.clip(rgb * 1.05 + 0.012, 0, 1)      # lighter, but cream text keeps 3.5:1
        outline = outline + [("glow", s.outline + 3.0)]
        shadow = (3, -7, 4, 0.30)
    elif state == "pressed":
        rgb = rgb * 0.86
        dy_state = -4
        shadow = (1, -2, 2, 0.30)
    elif state == "disabled":
        lum = (rgb * np.array([0.3, 0.59, 0.11], dtype=np.float32)).sum(-1, keepdims=True)
        grey = lum * 0.8 + 0.14
        rgb = np.clip(rgb * 0.25 + grey * np.array(hex_rgb("#C9BDB0"), dtype=np.float32) * 0.75 / 0.8, 0, 1)
        outline = [("disabled_outline", o[1]) for o in outline]
        shadow = None
    if dy_state:
        rgb = np.stack([shift(rgb[..., i], 0, dy_state) for i in range(3)], -1)
        a = shift(a, 0, dy_state)
    H, W = a.shape
    acc_rgb = np.zeros((H, W, 3), dtype=np.float32)
    acc_a = np.zeros((H, W), dtype=np.float32)
    sil = a
    rings = [(colour, dilate(a, radius)) for colour, radius in sorted(outline, key=lambda o: -o[1])]
    if rings:
        sil = np.maximum(a, rings[0][1])
    if shadow:
        dx, dy, blur, op = shadow
        sa = box_blur(box_blur(shift(sil, dx, dy), blur), blur) * op
        acc_rgb, acc_a = over(acc_rgb, acc_a, np.broadcast_to(np.array(hex_rgb("shadow"), np.float32), rgb.shape), sa)
    for colour, ra in rings:
        c = np.broadcast_to(np.array(hex_rgb(colour), np.float32), rgb.shape)
        acc_rgb, acc_a = over(acc_rgb, acc_a, c, ra)
    acc_rgb, acc_a = over(acc_rgb, acc_a, rgb, a)
    out = np.zeros((H, W, 4), dtype=np.float32)
    nz = acc_a > 1e-5
    out[..., :3][nz] = acc_rgb[nz] / acc_a[nz][..., None]
    out[..., 3] = acc_a
    return out


def trim_to_art(img, margin, step=8):
    """Crop a finished sprite to its opaque art (alpha > 0.5, outline included) plus `margin`
    texture px on every side, the size rounded up to a multiple of `step` with the extra split
    evenly. The shadow's faint tail fits in the margin (it is checked: `lost`). Returns the
    image, the margin actually left (the smallest gap from an edge to the opaque art) and
    the alpha sum cropped away."""
    a = img[..., 3]
    ys, xs = np.nonzero(a > 0.5)
    H, W = a.shape

    def span(lo, hi):
        extra = (-(hi - lo)) % step
        return lo - extra // 2, hi + extra - extra // 2

    y0, y1 = span(int(ys.min()) - margin, int(ys.max()) + 1 + margin)
    x0, x1 = span(int(xs.min()) - margin, int(xs.max()) + 1 + margin)
    out = np.zeros((y1 - y0, x1 - x0, 4), dtype=np.float32)
    sy0, sy1, sx0, sx1 = max(0, y0), min(H, y1), max(0, x0), min(W, x1)
    out[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0] = img[sy0:sy1, sx0:sx1]
    lost = float(a.sum() - out[..., 3].sum())
    left = min(int(xs.min()) - x0, x1 - 1 - int(xs.max()), int(ys.min()) - y0, y1 - 1 - int(ys.max()))
    return out, left, lost


def downscale(img, size):
    """Area-average an (h, w, 4) straight-alpha image to about `size` px tall, in
    premultiplied space: what a mipmapped or supersampled UI would show."""
    h, w, _ = img.shape
    k = max(1, int(round(h / float(size))))
    h2, w2 = h // k, w // k
    pm = img.copy()
    pm[..., :3] *= pm[..., 3:4]
    pm = pm[:h2 * k, :w2 * k].reshape(h2, k, w2, k, 4).mean(axis=(1, 3))
    out = pm.copy()
    nz = out[..., 3] > 1e-5
    out[..., :3][nz] = pm[..., :3][nz] / pm[..., 3][nz][..., None]
    return out


def premultiply(img):
    pm = img.copy()
    pm[..., :3] *= pm[..., 3:4]
    return pm


def unpremultiply(pm):
    out = pm.copy()
    nz = out[..., 3] > 1e-5
    out[..., :3][nz] = pm[..., :3][nz] / pm[..., 3][nz][..., None]
    return out


def bilinear_tap(pm, w2, h2):
    """One bilinear sample at each destination pixel centre, edges clamped: what the GPU
    draws from a texture without mipmaps, however small (decision 10). Premultiplied in and
    out, which is what Unity's alpha-is-transparency import makes a straight-alpha texture
    filter like."""
    h, w, _ = pm.shape
    u = (np.arange(w2) + 0.5) * (w / float(w2)) - 0.5
    v = (np.arange(h2) + 0.5) * (h / float(h2)) - 0.5
    x0 = np.floor(u).astype(int)
    y0 = np.floor(v).astype(int)
    fx = (u - x0)[None, :, None]
    fy = (v - y0)[:, None, None]
    xa, xb = np.clip(x0, 0, w - 1), np.clip(x0 + 1, 0, w - 1)
    ya, yb = np.clip(y0, 0, h - 1), np.clip(y0 + 1, 0, h - 1)
    near = pm[ya][:, xa] * (1 - fx) + pm[ya][:, xb] * fx
    far = pm[yb][:, xa] * (1 - fx) + pm[yb][:, xb] * fx
    return near * (1 - fy) + far * fy


def mip_chain(pm):
    """Box-filtered mip levels, as Unity builds them (mipmap filter Box)."""
    levels = [pm]
    while min(levels[-1].shape[:2]) > 1:
        a = levels[-1]
        h, w = a.shape[0] // 2 * 2, a.shape[1] // 2 * 2
        levels.append(a[:h, :w].reshape(h // 2, 2, w // 2, 2, 4).mean(axis=(1, 3)))
    return levels


def trilinear_tap(levels, w2, h2):
    """Trilinear sampling with a mip chain: bilinear in the two levels around the LOD, blended."""
    h, w, _ = levels[0].shape
    lod = max(0.0, math.log2(max(h / float(h2), w / float(w2))))
    i = min(int(lod), len(levels) - 1)
    j = min(i + 1, len(levels) - 1)
    t = lod - int(lod)
    return bilinear_tap(levels[i], w2, h2) * (1 - t) + bilinear_tap(levels[j], w2, h2) * t


def paste(canvas, img, x, y):
    """Composite a straight-alpha image onto an opaque RGB canvas at (x, y), bottom-left."""
    h, w, _ = img.shape
    H, W, _ = canvas.shape
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return
    src = img[y0 - y:y1 - y, x0 - x:x1 - x]
    al = src[..., 3:4]
    canvas[y0:y1, x0:x1, :3] = src[..., :3] * al + canvas[y0:y1, x0:x1, :3] * (1 - al)


# ----------------------------------------------------------------------------- manifest


def names_of(s):
    return [s.name + "_" + st for st in s.states] if s.states else [s.name]


def entry(s, name, size=None, margin=None):
    """One sprites.json line. `size` and `margin` are the finished PNG's when post changed
    them (a trimmed logo), so the manifest always describes the file on disk."""
    tw, th = size or (int(round(s.w * SCALE)), int(round(s.h * SCALE)))
    border = [int(round(v * SCALE)) for v in (s.border or (0, 0, 0, 0))]
    e = {"name": name, "file": name + ".png", "group": s.group, "size": [tw, th],
         "size1x": [tw // SCALE, th // SCALE] if size else [s.w, s.h],
         "kind": s.kind, "border": border, "pivot": [s.pivot[0], s.pivot[1]],
         "margin": int(round(s.pad * SCALE)) if margin is None else margin, "tint": s.tint, "mipmaps": s.mips}
    if s.kind == "sliced":
        # decision 9: the smallest element, in 1080p panel px at uiScale, whose slices fit; an
        # "x" sprite has (almost) no vertical stretch region: it is drawn drawHeight tall at
        # uiScale, and at any other height with slice scale = height / texture height
        e["stretch"] = s.stretch
        e["minSize"] = [int(math.ceil((border[0] + border[2]) * UI_SCALE)),
                        int(math.ceil((border[1] + border[3]) * UI_SCALE))]
        if s.stretch == "x":
            e["drawHeight"] = int(round(th * UI_SCALE))
    if s.content:
        e["content"] = [int(round(v * SCALE)) for v in s.content]
        e["face"] = [int(round(v * SCALE)) for v in s.face]
    if s.text:
        e["text"] = s.text
    if s.slice_mode:
        e["slice"] = s.slice_mode
    if s.tile_period:
        e["tilePeriod"] = int(s.tile_period * SCALE)
    if s.states:
        e["state"] = name[len(s.name) + 1:]
    e["desc"] = s.desc
    return e


def write_manifest(out_dir, entries):
    path = os.path.join(out_dir, MANIFEST)
    old = {}
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8") as f:
            try:
                for e in json.load(f).get("sprites", []):
                    old[e["name"]] = e
            except ValueError:
                old = {}
    old.update({e["name"]: e for e in entries})
    order = [old[n] for s in SPECS for n in names_of(s) if n in old]
    data = {
        "generator": "tools/blender/render_ui_kit.py",
        "note": "Pixel values are texture pixels (2x). border and content are in Unity Sprite.border order: "
                "left, bottom, right, top. border is the 9-slice; content is the inner padding where text and "
                "children go; face is where the plain surface ends (the same as content, except the cardboard's "
                "sides: a child between content and face sits on plain cardboard, closer to the rim than a text "
                "block should, but on no detail); margin is the transparent edge that holds the outline and the "
                "shadow (whole pictures too: an icon's art is inset by it). Show the art at "
                "uiScale screen pixels per texture pixel on the 1080p reference (UI Toolkit -unity-slice-scale, "
                "uGUI Image.pixelsPerUnitMultiplier = 1 / uiScale with pixelsPerUnit 100). minSize is the smallest "
                "element, in 1080p panel px at uiScale, whose slices fit: below it draw at a smaller slice scale, "
                "min(uiScale, width / (left + right), height / (bottom + top)). minSize covers the borders only: an "
                "element fits when its borders fit AND its content rect at that slice scale (content x scale) holds "
                "its child, so a skin uses the largest scale at which both hold; key caps are drawn whole, at slice "
                "scale = height / texture height (about 0.22 for 26 to 32 px keys). stretch 'xy': a 9-slice that grows "
                "both ways; 'x': no vertical stretch region, draw it at slice scale = height / texture height. "
                "mipmaps: import with mipmaps (whole pictures shown far below their texture size). text is the ink "
                "colour that reads on the sprite. tint: white art meant to be multiplied by a colour. slice "
                "'tiled': tile the centre, tilePeriod is its pattern period.",
        "density": SCALE,
        "uiScale": UI_SCALE,
        "pixelsPerUnit": PPU,
        "import": {"textureType": "Sprite", "spriteMode": "Single", "meshType": "FullRect", "mipmaps": "per sprite",
                   "filterMode": "Bilinear", "wrapMode": "Clamp", "wrapModeTiled": "Repeat",
                   "compression": "None", "alphaIsTransparency": True, "sRGB": True, "maxSize": 2048},
        "fonts": {"family": "Fredoka", "files": ["Fredoka-Regular.ttf", "Fredoka-Medium.ttf", "Fredoka-SemiBold.ttf",
                                                 "Fredoka-Bold.ttf", "Fredoka-Variable.ttf"], "licence": "OFL.txt"},
        "palette": {k: PALETTE[k] for k in ("ink", "cream", "wood", "wood_dark", "pine", "walnut", "plank", "paper",
                                             "cardboard", "tape", "rope", "tape_yellow", "leaf", "brick", "accent",
                                             "mood_calm", "mood_annoyed", "mood_angry", "mood_furious",
                                             "pad_a", "pad_b", "pad_x", "pad_y")},
        "states": list(BUTTON_STATES),
        "sprites": order,
    }
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("{\n")
        keys = [k for k in data if k != "sprites"]
        for k in keys:
            f.write(" %s: %s,\n" % (json.dumps(k), json.dumps(data[k])))
        # one sprite per line: easy to diff, easy for the importer snippet to read
        f.write(' "sprites": [\n')
        for i, e in enumerate(order):
            f.write("  %s%s\n" % (json.dumps(e), "," if i < len(order) - 1 else ""))
        f.write(" ]\n}\n")
    return data


CS_HEADER = """// Generated by tools/blender/render_ui_kit.py (--cs) from the same numbers as sprites.json.
// Do not edit by hand: change the script, re-render, and this file is rewritten.
using UnityEngine;

namespace Movers
{
    // The layout numbers of the interface kit's 9-slice sprites (UIART), for the UI code that
    // draws them. A Sprite carries its border, but not the art's content rect (where children
    // go: right of a luggage tag's string, inside a board's battens) nor the smallest size its
    // slices fit in. An element fits when both hold at its slice scale: its borders are not
    // squashed (a slice drawn smaller than them loses its corner art first), and its content
    // rect, which shrinks with the same scale, still holds its child.
    // Numbers are texture pixels of the 2x art; UiScale turns them into 1080p panel pixels.
    public static class UiArtMetrics
    {
        // Panel pixels per texture pixel on the 1080p reference: the kit is rendered at 2x.
        public const float UiScale = 0.5f;

        public readonly struct Slice
        {
            public readonly Vector2 size;       // the texture, px
            public readonly Vector4 border;     // left, bottom, right, top (Sprite.border order)
            public readonly Vector4 content;    // left, bottom, right, top: where children go
            public readonly Vector4 face;       // where the plain surface ends: content, except the
                                                // cardboard's sides (a one-word caption may use them)
            public readonly bool widthOnly;     // no vertical stretch: scale it whole to its height

            public Slice(float width, float height, Vector4 border, Vector4 content, Vector4 face, bool widthOnly)
            {
                size = new Vector2(width, height);
                this.border = border;
                this.content = content;
                this.face = face;
                this.widthOnly = widthOnly;
            }

            // The slice scale to draw an element of width x height panel px with: `preferred`
            // (the theme's scale) when the slices fit, smaller when they do not, so corners
            // shrink evenly instead of being squashed. A width-only sprite (the tape bar, the
            // compass strip) is scaled whole to the element's height.
            public float FitScale(float width, float height, float preferred = UiScale)
            {
                if (widthOnly) return size.y > 0f && height > 0f ? height / size.y : preferred;
                float s = preferred;
                float across = border.x + border.z, up = border.y + border.w;
                if (across > 0f && width > 0f) s = Mathf.Min(s, width / across);
                if (up > 0f && height > 0f) s = Mathf.Min(s, height / up);
                return s;
            }

            // The same, and also small enough that the content rect at that scale holds a child
            // of contentWidth x contentHeight panel px (0: that direction is not checked). The
            // borders alone are not enough: a 26 px key cap's corners fit at 0.5, but its
            // content rect is then 6 px tall for a 13 px letter. When even a tiny scale cannot
            // make room (Holds is still false at the result), the element is too small for its
            // child: make it bigger instead. A width-only sprite keeps its height scale.
            public float FitScale(float width, float height, float preferred, float contentWidth, float contentHeight)
            {
                float s = FitScale(width, height, preferred);
                if (widthOnly) return s;
                float across = content.x + content.z, up = content.y + content.w;
                if (across > 0f && contentWidth > 0f) s = Mathf.Min(s, Mathf.Max(0f, width - contentWidth) / across);
                if (up > 0f && contentHeight > 0f) s = Mathf.Min(s, Mathf.Max(0f, height - contentHeight) / up);
                return s;
            }

            // Whether an element of width x height drawn at `scale` fits: its borders are not
            // squashed and its content rect holds a child of contentWidth x contentHeight.
            public bool Holds(float width, float height, float scale, float contentWidth, float contentHeight)
            {
                const float Slack = 0.5f;
                Vector2 min = MinSize(scale);
                if (min.x > width + Slack || min.y > height + Slack) return false;
                return width - (content.x + content.z) * scale + Slack >= contentWidth
                    && height - (content.y + content.w) * scale + Slack >= contentHeight;
            }

            // The scale that draws the whole texture `height` panel px tall: a key cap at its own
            // proportions (height / 128: 0.2 to 0.25 for the HUD's 26 to 32 px keys).
            public float WholeScale(float height) => size.y > 0f ? height / size.y : UiScale;

            // The padding that keeps children inside the content rect at a slice scale, panel
            // px, in the order of LumaFlow's EdgeInsets.Only: x left, y top, z right, w bottom.
            public Vector4 Padding(float scale) =>
                new Vector4(content.x * scale, content.w * scale, content.z * scale, content.y * scale);

            // The same for the plain surface: a child inside this padding but outside Padding's
            // sits closer to the rim than designed, on no detail of the art (same order).
            public Vector4 FacePadding(float scale) =>
                new Vector4(face.x * scale, face.w * scale, face.z * scale, face.y * scale);

            // The smallest element, panel px, whose slices fit at `scale` (sprites.json minSize).
            // Borders only: see Holds for the content.
            public Vector2 MinSize(float scale = UiScale) =>
                new Vector2((border.x + border.z) * scale, (border.y + border.w) * scale);
        }

        // The numbers of a 9-slice sprite by its fixed name (UiSprites), false for the others.
        public static bool TryGet(string sprite, out Slice slice)
        {
            switch (sprite)
            {
"""

CS_MIDDLE = """                default:
                    slice = default;
                    return false;
            }
        }

        // The transparent edge of any kit sprite, texture px, on every side: it holds the
        // outline's outer blur and the drop shadow, so the visible art of a 128 px glyph drawn
        // 35 px tall is about 26 px (sprites.json margin). 0 for a name the kit does not have.
        public static float Margin(string sprite)
        {
            switch (sprite)
            {
"""

CS_FOOTER = """                default:
                    return 0f;
            }
        }
    }
}
"""


def write_cs(path, manifest):
    """The sliced sprites' numbers as UiArtMetrics.cs (decision 9), and every sprite's
    transparent margin: switches, no parsing and no allocation at run time."""
    lines = [CS_HEADER]
    for e in manifest["sprites"]:
        if e["kind"] != "sliced":
            continue
        c = e.get("content", [0, 0, 0, 0])
        f = e.get("face", c)
        lines.append('                case "%s":\n' % e["name"])
        lines.append("                    slice = new Slice(%df, %df, new Vector4(%df, %df, %df, %df), "
                     "new Vector4(%df, %df, %df, %df), new Vector4(%df, %df, %df, %df), %s);\n"
                     % (e["size"][0], e["size"][1], *e["border"], *c, *f,
                        "true" if e.get("stretch") == "x" else "false"))
        lines.append("                    return true;\n")
    lines.append(CS_MIDDLE)
    by_margin = {}
    for e in manifest["sprites"]:
        if e.get("margin", 0) > 0:
            by_margin.setdefault(e["margin"], []).append(e["name"])
    for m in sorted(by_margin):
        for n in by_margin[m]:
            lines.append('                case "%s":\n' % n)
        lines.append("                    return %df;\n" % m)
    lines.append(CS_FOOTER)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("".join(lines))


# ----------------------------------------------------------------------------- contact sheet


def sheet_material(img, key, repeat=False):
    m = bpy.data.materials.new("Sheet_" + key)
    if m.node_tree is None:
        m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    # "Closest": EEVEE builds mipmaps for "Linear" textures, which smears a 9-slice's squashed
    # centre strip into lines no mipmap-free UI would draw; the 16 TAA samples still average
    # each pixel's footprint, so scaled art stays smooth
    tex.interpolation = "Closest"
    tex.extension = "REPEAT" if repeat else "EXTEND"
    em = nt.nodes.new("ShaderNodeEmission")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mx = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(tex.outputs["Color"], em.inputs["Color"])
    nt.links.new(tex.outputs["Alpha"], mx.inputs[0])
    nt.links.new(tr.outputs[0], mx.inputs[1])
    nt.links.new(em.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = "BLENDED"
    except (AttributeError, TypeError):
        m.blend_method = "BLEND"
    return m


def tinted_material(img, key, tint):
    """The sprite multiplied by a colour, as the UI tints the white sprites."""
    m = sheet_material(img, key + "_tint")
    nt = m.node_tree
    tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
    em = next(n for n in nt.nodes if n.type == "EMISSION")
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs["Color"], mul.inputs[6])
    mul.inputs[7].default_value = (*to_linear(hex_rgb(tint)), 1.0)
    nt.links.new(mul.outputs[2], em.inputs["Color"])
    return m


_FLAT = {}


def flat_material(colour):
    key = str(colour)
    if key in _FLAT:
        return _FLAT[key]
    m = bpy.data.materials.new("SheetFlat_" + key)
    if m.node_tree is None:
        m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*to_linear(hex_rgb(colour)), 1.0)
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    _FLAT[key] = m
    return m


def quad_mesh(name, quads, m, z):
    """Quads given as (x0, y0, x1, y1, u0, v0, u1, v1)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for x0, y0, x1, y1, u0, v0, u1, v1 in quads:
        vs = [bm.verts.new(c) for c in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
        f = bm.faces.new(vs)
        for loop, c in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
            loop[uv].uv = c
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    COL.objects.link(ob)
    return ob


def slice_quads(x, y, w, h, tw, th, border, disp, tiled=False):
    """What Unity does with Sprite.border: a 3 x 3 grid, borders kept at `disp` sheet pixels
    per texture pixel; with `tiled`, the centre column repeats instead of stretching. An
    element smaller than its borders gets them squashed together to its size, as uGUI's
    sliced Image does: the sheet shows that failure instead of drawing crossed quads."""
    L, Bm, R, T = border
    kx = min(1.0, w / ((L + R) * disp)) if L + R else 1.0
    ky = min(1.0, h / ((Bm + T) * disp)) if Bm + T else 1.0
    xs = [x, x + L * disp * kx, x + w - R * disp * kx, x + w]
    ys = [y, y + Bm * disp * ky, y + h - T * disp * ky, y + h]
    us = [0.0, L / tw, 1 - R / tw, 1.0]
    vs = [0.0, Bm / th, 1 - T / th, 1.0]
    quads = []
    for j in range(3):
        for i in range(3):
            if tiled and i == 1:
                period = (tw - L - R) * disp
                cx = xs[1]
                while cx < xs[2] - 1e-6:
                    seg = min(period, xs[2] - cx)
                    quads.append((cx, ys[j], cx + seg, ys[j + 1], us[1], vs[j],
                                  us[1] + (us[2] - us[1]) * seg / period, vs[j + 1]))
                    cx += seg
            else:
                quads.append((xs[i], ys[j], xs[i + 1], ys[j + 1], us[i], vs[j], us[i + 1], vs[j + 1]))
    return quads


def build_sheet(out_dir, sheet_path, manifest):
    """Every sprite at 2x on dark and light grounds, then the UI at its 1080p size: a title
    screen and a half-width HUD (the hints tag, the patience bar, the compass, indicators),
    the glyphs sampled like the GPU (decision 10), the kit at UICORE's sizes and scales with
    every child in its content rect, a 2x check that outlines the content rect green or red
    (decision 9), and the 9-slice sprites stretched with the manifest's borders (decision 5)."""
    clear_sprite()
    SC.render.film_transparent = False
    sprites = {e["name"]: e for e in manifest["sprites"]}
    mats = {}
    z = [1.0]

    def nz():
        z[0] += 0.02
        return z[0]

    def image(name):
        img = bpy.data.images.load(os.path.join(out_dir, sprites[name]["file"]), check_existing=True)
        img.alpha_mode = "STRAIGHT"
        return img

    def m_for(name, repeat=False, tint=None):
        key = (name, repeat, tint)
        if key not in mats:
            mats[key] = tinted_material(image(name), name, tint) if tint else sheet_material(image(name), name, repeat)
        return mats[key]

    def label(s, x, y, size=13.0, colour="cream", align="LEFT", weight="Medium"):
        cu = bpy.data.curves.new("lbl", "FONT")
        cu.body = s
        cu.font = font(weight)
        cu.size = size
        cu.align_x = align
        ob = bpy.data.objects.new("lbl", cu)
        COL.objects.link(ob)
        ob.location = (x, y, nz())
        ob.data.materials.append(flat_material(colour))
        return ob

    def rect(x, y, w, h, colour):
        quad_mesh("bg", [(x, y, x + w, y + h, 0, 0, 1, 1)], flat_material(colour), nz() - 0.9)

    def sprite_at(name, x, y, disp=1.0, w=None, h=None, tint=None):
        e = sprites[name]
        tw, th = e["size"]
        w = tw * disp if w is None else w
        h = th * disp if h is None else h
        if e["kind"] == "tiled":
            reps_x, reps_y = w / (tw * disp), h / (th * disp)
            return quad_mesh(name, [(x, y, x + w, y + h, 0, 0, reps_x, reps_y)], m_for(name, repeat=True), nz())
        quads = slice_quads(x, y, w, h, tw, th, e["border"], disp, tiled=e.get("slice") == "tiled")
        return quad_mesh(name, quads, m_for(name, tint=tint), nz())

    SHEET_W = 2800
    margin = 40
    grounds = ("#5E554D", "#D8CDBE")
    sections = [("Frames and panels (2x, as rendered)", ("frame_",)), ("Buttons: normal, hover, pressed, disabled",
                                                                       ("button_",)),
                ("Bars, keys and mouse", ("bar_", "key_", "mouse_")), ("Gamepad", ("pad_",)),
                ("Icons", ("icon_",)), ("Indicators, compass, crosshair", ("indicator_", "compass_", "crosshair_")),
                ("Title", ("logo_", "bg_paper"))]
    placed = []
    cursor_y = margin
    for title, prefixes in sections:
        names = [n for n in sprites if n.startswith(prefixes)]
        if not names:
            continue
        placed.append(("title", title, margin, cursor_y))
        cursor_y += 40
        x = margin
        row_h = 0
        for n in names:
            tw, th = sprites[n]["size"]
            cell_w = max(tw, 150) + 22
            if x + cell_w > SHEET_W - margin:
                x = margin
                cursor_y += row_h + 34
                row_h = 0
            placed.append(("sprite", n, x, cursor_y))
            x += cell_w
            row_h = max(row_h, th)
        cursor_y += row_h + 34 + 30
    demo_y = cursor_y
    demo_h = 2350
    total_h = int(demo_y + demo_h + margin)

    def Y(top_y, h):
        """Sheet coordinates: y measured downwards from the top, to Blender's y up."""
        return total_h - top_y - h

    rect(0, 0, SHEET_W / 2, total_h, grounds[0])
    rect(SHEET_W / 2, 0, SHEET_W / 2, total_h, grounds[1])
    for kind, n, x, ty in placed:
        if kind == "title":
            label(n, x, Y(ty, 24) + 4, size=26.0, colour="#FFF3E0", weight="SemiBold")
            continue
        tw, th = sprites[n]["size"]
        sprite_at(n, x, Y(ty, th))
        light = x >= SHEET_W / 2
        label(n, x, Y(ty, th) - 20, size=15.0, colour=INK if light else "#F4E8D8")

    # ---- the UI at its real 1080p size (disp = uiScale): a title screen and a half-width HUD
    d = manifest.get("uiScale", 0.5)

    def note(s, x, y):
        middle = x + metrics("Medium").width(s, 14.0) / 2.0              # the ground most of it lands on
        label(s, x, y, 14.0, INK if middle >= SHEET_W / 2 else "#F4E8D8")

    def fit(name, w, h):
        """The largest slice scale up to uiScale at which the borders fit; an 'x' sprite is
        scaled whole to its height. Borders only: the content rect is checked by the helpers
        below (decision 9)."""
        e = sprites[name]
        L, Bm, R, T = e["border"]
        if e.get("stretch") == "x":
            return h / float(e["size"][1])
        return min(d, w / float(L + R) if L + R else d, h / float(Bm + T) if Bm + T else d)

    # ---- UICORE's layout rules (decision 9), so the sheet shows the kit as the HUD lays it out.
    # Sizes are 1080p panel px; `m` magnifies a piece on the sheet (the 2x check row); y is up.
    fits_colour, fails_colour = "#3E9B55", "#E0442E"

    def pad_of(name, s, minimum=0.0, key="content"):
        """UiSkin.Padding: the sprite's content rect at slice scale s, at least `minimum`, in
        LumaFlow's EdgeInsets order: left, top, right, bottom. key "face": where the plain
        surface ends instead (the cardboard's sides are plain nearer the rim)."""
        c = sprites[name].get(key, sprites[name].get("content", [0, 0, 0, 0]))   # left, bottom, right, top
        return (max(minimum, c[0] * s), max(minimum, c[3] * s), max(minimum, c[2] * s), max(minimum, c[1] * s))

    def borders_fit(name, s, w, h):
        """Borders at slice scale s fit a w x h piece (a width-only sprite's vertical borders
        span almost its whole height: they fit at height / texture height)."""
        L, Bm, R, T = sprites[name]["border"]
        return (L + R) * s <= w + 0.5 and (Bm + T) * s <= h + 0.5

    def holds(name, s, w, h, pad, ink_w, ink_h):
        """Both halves of decision 9: the borders fit, the padding keeps the child on the
        sprite's plain face (its content rect, or the face where that is nearer the rim), and
        what is left holds the child's ink (text: 1 em tall)."""
        rect_ = pad_of(name, s, key="face")
        inside = all(g + 0.5 >= r for g, r in zip(pad, rect_))
        return (borders_fit(name, s, w, h) and inside and
                w - pad[0] - pad[2] + 0.5 >= ink_w and h - pad[1] - pad[3] + 1.0 >= ink_h)

    def ellipsize(text_, width, weight="Medium", size=15.0):
        """A one-line LumaFlow Text with TextOverflow.Ellipsis, `width` wide: cut, then an
        ellipsis, as the pocket captions are ("Vase en ...")."""
        fm = metrics(weight)
        if fm.width(text_, size) <= width:
            return text_
        dots = "…" if 0x2026 in fm.cmap else "..."
        while text_ and fm.width(text_ + dots, size) > width:
            text_ = text_[:-1]
        return text_ + dots

    def text_line(s_, x, cy, size, colour, weight, align="CENTER", m=1.0):
        """One Label whose line box (ascender to descender) is centred on cy; x is its centre,
        or its left edge with align LEFT."""
        fm = metrics(weight)
        label(s_, x, cy + (fm.line(size) / 2.0 - fm.ascender(size)) * m, size * m, colour, align, weight)

    def flat(x, y, w, h, colour):
        quad_mesh("flat", [(x, y, x + w, y + h, 0, 0, 1, 1)], flat_material(colour), nz())

    def outline(box, good, t=2.0):
        """A content rect as a thin frame over the sprite; collapsed to a line when it has no room."""
        x0, y0, x1, y1 = box
        if x1 < x0:
            x0 = x1 = (x0 + x1) / 2.0
        if y1 < y0:
            y0 = y1 = (y0 + y1) / 2.0
        h_ = t / 2.0
        quad_mesh("content_rect", [(x0 - h_, y0 - h_, x1 + h_, y0 + h_, 0, 0, 1, 1),
                                   (x0 - h_, y1 - h_, x1 + h_, y1 + h_, 0, 0, 1, 1),
                                   (x0 - h_, y0, x0 + h_, y1, 0, 0, 1, 1),
                                   (x1 - h_, y0, x1 + h_, y1, 0, 0, 1, 1)],
                  flat_material(fits_colour if good else fails_colour), nz())

    def content_box(x, yb, w, h, pad, m=1.0):
        """The rect a padding leaves inside a w x h piece drawn at (x, yb), on the sheet."""
        return (x + pad[0] * m, yb + pad[3] * m, x + (w - pad[2]) * m, yb + (h - pad[1]) * m)

    def key_face(name, key, h, x, yb, s, size=None, weight="SemiBold", wide=False, m=1.0):
        """UiKit.KeyFace: the cap at slice scale s, h tall and at least h wide; the word in the
        cap's face, padded by the content rect at s (a wide key by at least 0.28 h at the
        sides), and that padded box centred on the cap. Returns (width, content rect, fits)."""
        fs = size or max(11.0, h * (0.42 if wide else 0.5))
        p = pad_of(name, s)
        side = max(p[0], h * 0.28) if wide else p[0]
        tw = metrics(weight).width(key, fs)
        w = max(h, tw + 2 * side)
        sprite_at(name, x, yb, s * m, w * m, h * m)
        # centring the padded box puts the line (bottom - top padding) / 2 above the middle
        text_line(key, x + w * m / 2.0, yb + (h / 2.0 + (p[3] - p[1]) / 2.0) * m, fs, INK, weight, "CENTER", m)
        return w, content_box(x, yb, w, h, p, m), holds(name, s, w, h, p, tw, fs)

    def glyph_key(name, h, x, yb, m=1.0):
        """UiKit.Key for a pad or mouse glyph: the art 1.25 h tall (its margin holds the outline
        and the shadow), centred in a box 0.86 times that wide. Returns the box width."""
        g = h * 1.25
        bw = g * 0.86
        sprite_at(name, x + (bw - g) / 2.0 * m, yb + (h - g) / 2.0 * m, g * m / sprites[name]["size"][1])
        return bw

    def hint_width(kind, key, verb, K=30.0):
        if kind == "glyph":
            kw = K * 1.25 * 0.86
        else:
            wide = kind == "wide"
            fs = max(11.0, K * (0.42 if wide else 0.5))
            pk = pad_of("key_wide" if wide else "key_blank", UICORE_SCALE["KeyCap"])
            kw = max(K, metrics("SemiBold").width(key, fs) + 2 * (max(pk[0], K * 0.28) if wide else pk[0]))
        return kw + 8.0 + metrics("Bold").width(verb, 19.0)

    def hint_tag(x, y_top, s, rows, m=1.0):
        """HintsView: UiKit.Tag(a column of HintRows, 4) with 30 px keys at KeyCap's scale and
        BodyBold verbs, as big as its rows plus the content rect at s (at least 4 px). y_top is
        its top edge. Returns (width, height)."""
        K = 30.0
        p = pad_of("frame_tag", s, 4.0)
        cw = max(hint_width(k, key, v) for k, key, v in rows)
        w, h = cw + p[0] + p[2], K * len(rows) + 6.0 * (len(rows) - 1) + p[1] + p[3]
        sprite_at("frame_tag", x, y_top - h * m, s * m, w * m, h * m)
        for i, (kind, key, verb) in enumerate(rows):
            row_b = y_top - (p[1] + i * (K + 6.0) + K) * m
            kx = x + p[0] * m
            if kind == "glyph":
                kw = glyph_key(key, K, kx, row_b, m)
            else:
                kw = key_face("key_wide" if kind == "wide" else "key_blank", key, K, kx, row_b,
                              UICORE_SCALE["KeyCap"], wide=kind == "wide", m=m)[0]
            text_line(verb, kx + (kw + 8.0) * m, row_b + K / 2.0 * m, 19.0, INK, "Bold", "LEFT", m)
        return w, h

    def pocket(x, yb, s, icon, caption, active=False, m=1.0):
        """PocketsView.Slot: a 78 x 84 box at slice scale s, its column (a 34 px icon, 2 px, a
        15 px caption; 30 px and greyed for what is in the hands) centred, padded top and
        bottom by the content rect at s (at least 3 px) and 4 px at the sides (CaptionMargin),
        the caption one line cut with an ellipsis at 70 px. The sides sit on the cardboard's
        plain face (sprites.json face), nearer the rim than its content rect. Returns (the rect
        the child may use: face at the sides, content above and below; its width and height;
        fits)."""
        W, H, side = 78.0, 84.0, 4.0
        rect_ = pad_of("frame_cardboard", s, 3.0)
        p = (side, rect_[1], side, rect_[3])
        caption = ellipsize(caption, W - 2 * side)
        sprite_at("frame_cardboard", x, yb, s * m, W * m, H * m, tint="#FFE6A8" if active else None)
        ic = 30.0 if active else 34.0
        lh = metrics("Medium").line(15.0)
        col_h = ic + 2.0 + lh
        cx = x + (W / 2.0 + (p[0] - p[2]) / 2.0) * m
        top_ = yb + (H / 2.0 + (p[3] - p[1]) / 2.0 + col_h / 2.0) * m
        sprite_at(icon, cx - ic / 2.0 * m, top_ - ic * m, ic * m / sprites[icon]["size"][1],
                  tint="#CDBFAE" if active else None)
        text_line(caption, cx, top_ - (ic + 2.0 + lh / 2.0) * m, 15.0, "#6E5644" if active else INK, "Medium",
                  "CENTER", m)
        tw = metrics("Medium").width(caption, 15.0)
        limit = pad_of("frame_cardboard", s, key="face")
        return (content_box(x, yb, W, H, limit, m), (max(ic, tw), ic + 2.0 + 15.0),
                holds("frame_cardboard", s, W, H, p, max(ic, tw), ic + 2.0 + 15.0))

    top = demo_y + 10
    label("At 1080p size (uiScale %.1f): title screen, and one half of the split screen (960 x 1080)" % d,
          margin, Y(top, 24) + 4, size=26.0, colour="#FFF3E0", weight="SemiBold")
    top += 50
    # title screen mock, 1100 x 620: a warm sky, the logo in the branch frame, three buttons
    tx, tw_, th_ = margin, 1100, 620
    rect(tx, Y(top, th_), tw_, th_, "#9CC9DE")
    rect(tx, Y(top + th_ * 0.62, th_ * 0.38), tw_, th_ * 0.38, "#86AE63")
    rect(tx, Y(top + th_ * 0.80, th_ * 0.07), tw_, th_ * 0.07, "#8B8378")
    lw, lh = sprites["logo_the_movers"]["size"]
    fx, fy, fw, fh = tx + 70, Y(top + 40, 330), 560, 330
    sprite_at("frame_branch", fx, fy, d, fw, fh)
    sprite_at("logo_the_movers", fx + (fw - lw * d) / 2, fy + (fh - lh * d) / 2 + 6, d)
    for i, (st, word) in enumerate((("hover", "Jouer"), ("normal", "Options"), ("normal", "Quitter"))):
        bx, by = tx + 720, Y(top + 110 + i * 92, 72)
        sprite_at("button_wood_" + st, bx, by, d, 300, 72)
        label(word, bx + 150, by + 25 - (2 if st == "pressed" else 0), 30.0, "cream", align="CENTER", weight="SemiBold")
    sprite_at("button_wood_disabled", tx + 720, Y(top + 110 + 3 * 92, 72), d, 300, 72)
    label("Continuer", tx + 870, Y(top + 110 + 3 * 92, 72) + 25, 30.0, "#EFE7DC", align="CENTER", weight="SemiBold")
    # a tag with a hint under the buttons
    hx, hy = tx + 700, Y(top + 500, 64)
    sprite_at("frame_tag", hx, hy, d, 360, 64)
    sprite_at("pad_a", hx + 54, hy + 12, 0.32)
    label("Valider", hx + 104, hy + 22, 22.0, INK, weight="SemiBold")
    sprite_at("pad_b", hx + 206, hy + 12, 0.32)
    label("Retour", hx + 256, hy + 22, 22.0, INK, weight="SemiBold")

    # the HUD half, 960 x 1080 scaled to 0.5 of 1080p would be too small to judge: shown at 1:1,
    # cropped to its top 620 px and its bottom prompts side by side
    hx0 = margin + 1100 + 40
    hw, hh = 960, 620
    rect(hx0, Y(top, hh), hw, hh, "#7C8C8F")
    rect(hx0, Y(top + 360, 260), hw, 260, "#9C8870")
    # contract card, top left: ContractBoardView's 310 px card at the Cardboard skin's scale
    px, py, pw, ph = hx0 + 20, Y(top + 20, 250), 310, 250
    sprite_at("frame_cardboard", px, py, UICORE_SCALE["Cardboard"], pw, ph)
    rows = [("icon_box", "Fauteuil  3/5"), ("icon_crate", "Cartons  7/12"), ("icon_money", "1 240 $"),
            ("icon_clock", "07:32")]
    for i, (ic, t) in enumerate(rows):
        iy = py + ph - 64 - i * 42
        sprite_at(ic, px + 22, iy - 6, 0.3)
        label(t, px + 68, iy + 4, 22.0, INK, weight="Medium")
    # patience bar, top right
    bh = 24.0                                             # the HUD's bar height (UICORE TapeHeight)
    bs = bh / sprites["bar_tape_bg"]["size"][1]           # "stretch": "x": scaled whole to its height
    bx, by = hx0 + 440, Y(top + 40, bh)
    sprite_at("icon_grandma_annoyed", bx - 50, by - 10, 0.36)
    sprite_at("bar_tape_bg", bx, by, bs, 440, bh)
    fe = sprites["bar_tape_bg"]["content"]
    sprite_at("bar_tape_fill", bx + fe[0] * bs, by + fe[1] * bs, bs, 250, bh - (fe[1] + fe[3]) * bs)
    label("Patience", bx + 10, by + bh + 6, 20.0, "cream", weight="SemiBold")
    # compass, top centre, with ticks and two crew marks
    cx0, cy0, cw = hx0 + 330, Y(top + 110, 32), 420
    sprite_at("compass_tape_bg", cx0, cy0, d, cw, 36)
    tk = sprites["compass_tick"]["size"]
    for i in range(9):
        sprite_at("compass_tick", cx0 + 40 + i * 42 - tk[0] * d / 2, cy0 + 18 - tk[1] * d / 2, d,
                  tint="cream" if i % 2 == 0 else "#CDBBA0")
    label("N", cx0 + 40 + 4 * 42, cy0 + 40, 20.0, "cream", align="CENTER", weight="Bold")
    sprite_at("indicator_ring", cx0 + 40 + 2 * 42 - 16, cy0 - 36, 0.25, tint="#4F86C0")
    # off-screen indicators on the right edge, tinted per crew colour
    sprite_at("indicator_arrow", hx0 + hw - 60, Y(top + 280, 32), d, tint="#D9573B")
    sprite_at("indicator_arrow", hx0 + hw - 60, Y(top + 330, 32), d, tint="#4F86C0")
    sprite_at("crosshair_dot", hx0 + hw / 2 - 8, Y(top + 310, 16), d)
    # the hints tag beside the aim, where HintsView puts it (30, 26 px from the crosshair): up to
    # four "[key] verb" rows, 30 px keys at KeyCap's scale, the tag at Tag's scale
    hint_tag(hx0 + hw / 2 + 30, Y(top + 318 + 26, 0), UICORE_SCALE["Tag"],
             [("key", "E", "Parler"), ("glyph", "mouse_left", "Porter"), ("key", "F", "Boire"),
              ("wide", "Espace", "Sauter")])

    # glyph readability, smooth reference: the renderer supersamples, close to mipmapped drawing
    gy = top + 660
    label("Glyphs and icons at 44 px and 32 px, supersampled by the renderer (the look with mipmaps)", margin,
          Y(gy, 24) + 4, 22.0, "#FFF3E0", weight="SemiBold")
    glyphs = [n for n in sprites if n.startswith(("mouse_", "pad_", "icon_"))]
    for row, (size, ground) in enumerate(((44, "#3F3A35"), (32, "#3F3A35"), (44, "#E9DFD0"), (32, "#E9DFD0"))):
        yy = Y(gy + 40 + row * 60, 56)
        rect(margin, yy - 6, SHEET_W - 2 * margin, 58, ground)
        for i, n in enumerate(glyphs):
            sprite_at(n, margin + 10 + i * 64, yy, size / float(sprites[n]["size"][1]))

    # glyph readability, runtime-true: computed the way the GPU samples them (decision 10) and
    # pasted into the sheet after the render, so the renderer's own filtering cannot smooth them
    ry = gy + 300
    label("Runtime check at the HUD's glyph sizes, sampled like the GPU: mipmaps off (one bilinear tap), "
          "then mipmaps on (trilinear, what step 01 sets)", margin, Y(ry, 24) + 4, 22.0, "#FFF3E0", weight="SemiBold")
    post = []
    cell, rh = 40, 38
    sizes = (18, 22, 26, 28, 32)
    pix = {n: premultiply(load_px(os.path.join(out_dir, sprites[n]["file"]))) for n in glyphs}
    chains = {n: mip_chain(pix[n]) for n in glyphs}
    for block, mode in enumerate(("off", "on")):
        by0 = ry + 34 + block * (len(sizes) * rh + 36)
        label("mipmaps %s" % mode, margin, Y(by0, 18) + 2, 16.0, "#F4E8D8", weight="SemiBold")
        for k, size in enumerate(sizes):
            top_y = by0 + 22 + k * rh
            rect(margin + 110, Y(top_y, rh - 2), len(glyphs) * cell + 10, rh - 2, "paper")
            label("%d px" % size, margin + 100, Y(top_y, rh - 2) + 10, 15.0, "#F4E8D8", align="RIGHT")
            for i, n in enumerate(glyphs):
                img = bilinear_tap(pix[n], size, size) if mode == "off" else trilinear_tap(chains[n], size, size)
                post.append((margin + 118 + i * cell, int(Y(top_y, rh - 2) + (rh - 2 - size) // 2),
                             unpremultiply(img)))

    # the kit as UICORE draws it: UiSkins.cs's scales (UICORE_SCALE), every child laid out in
    # the content rect at that scale, as UiSkin.Padding and UiKit.KeyFace do (decision 9)
    cy = ry + 480
    label("At UICORE's sizes and slice scales (UiSkins.cs): every child laid out in the sprite's content rect "
          "at that scale, as UiSkin.Padding and UiKit.KeyFace do", margin, Y(cy, 24) + 4, 22.0, "#FFF3E0",
          weight="SemiBold")
    mid = cy + 112                                        # every piece is centred on this line

    def at_mid(h):
        return Y(mid - h / 2.0, h)

    def group(x, width, text_):
        """A note under a piece; returns where the next piece starts."""
        note(text_, x, Y(mid + 66, 14))
        return x + max(width, metrics("Medium").width(text_, 14.0)) + 28

    kc, tag_s = UICORE_SCALE["KeyCap"], UICORE_SCALE["Tag"]
    x = margin
    x0 = x
    for name, key, h in (("key_blank", "E", 26), ("key_blank", "F", 28), ("key_blank", "1", 30),
                         ("key_blank", "Q", 32), ("key_wide", "Maj", 28), ("key_wide", "Espace", 30)):
        x += key_face(name, key, h, x, at_mid(h), kc, wide=name == "key_wide")[0] + 10
    x = group(x0, x - x0, "keys 26 to 32 px, KeyCap %.2f" % kc)
    w = key_face("key_blank", "<", 30, x, at_mid(30), kc, size=21.0, weight="Bold")[0]
    x = group(x, w, "arrow, 30 px")
    rows = [("key", "E", "Parler"), ("glyph", "mouse_left", "Porter")]
    for s_, text_ in ((tag_s, "hints, Tag %.2f" % tag_s), (0.5, "the same at 0.5")):
        th_ = 2 * 30.0 + 6.0 + sum(pad_of("frame_tag", s_, 4.0)[1::2])
        w, _ = hint_tag(x, Y(mid - th_ / 2.0, 0), s_, rows)
        x = group(x, w, text_)
    # the grandmother's words: UiKit.Speech, LabelBold on the TagBig skin
    sb = UICORE_SCALE["TagBig"]
    p = pad_of("frame_tag", sb, 8.0)
    lines_ = ("Mais qu'est-ce que vous", "faites avec mon buffet ?")
    lh = metrics("Bold").line(21.0)
    w = max(metrics("Bold").width(t, 21.0) for t in lines_) + p[0] + p[2]
    h = 2 * lh + p[1] + p[3]
    yb = at_mid(h)
    sprite_at("frame_tag", x, yb, sb, w, h)
    for i, t in enumerate(lines_):
        text_line(t, x + p[0], yb + h - p[1] - (i + 0.5) * lh, 21.0, INK, "Bold", "LEFT")
    x = group(x, w, "speech, TagBig %.2f" % sb)
    # UiKit.TapeBar: the case 24 px tall at 24 / 80, the tape in its content rect at that scale
    bs = UICORE_SCALE["TapeBg"]
    p = pad_of("bar_tape_bg", bs)
    yb = at_mid(24.0)
    sprite_at("bar_tape_bg", x, yb, bs, 200.0, 24.0)
    sprite_at("bar_tape_fill", x + p[0], yb + p[3], bs, (200.0 - p[0] - p[2]) * 0.6, 24.0 - p[1] - p[3])
    x = group(x, 200.0, "bar 24 px, %.2f" % bs)
    # WoodButton: LabelBold, centred, padded by the content rect at 0.42 (at least 10), 220 wide at least
    bsc = UICORE_SCALE["Button"]
    p = pad_of("button_wood_normal", bsc, 10.0)
    lh = metrics("Bold").line(21.0)
    w, h = max(220.0, metrics("Bold").width("Reprendre", 21.0) + p[0] + p[2]), lh + p[1] + p[3]
    yb = at_mid(h)
    sprite_at("button_wood_normal", x, yb, bsc, w, h)
    text_line("Reprendre", x + p[0] + (w - p[0] - p[2]) / 2.0, yb + p[3] + lh / 2.0, 21.0, "#FFF7E8", "Bold")
    x = group(x, w, "button, %.2f" % bsc)
    # PocketsView: 78 x 84 boxes at the Slot skin's scale, the key pinned on the corner (-6, -10)
    ss = UICORE_SCALE["Slot"]
    yb = at_mid(84.0)
    x0 = x
    for i, (icon, caption, active) in enumerate((("icon_beer", "Bi\u00e8re", False),
                                                 ("icon_money", "Vase en porcelaine", False),
                                                 ("icon_cigarette", "Cigarette", True))):
        pocket(x, yb, ss, icon, caption, active)
        key_face("key_blank", str(i + 1), 26.0, x - 6.0, yb + 84.0 + 10.0 - 26.0, kc)
        x += 78.0 + 8.0
    x = group(x0, x - x0, "pockets 78x84, Slot %.2f" % ss)
    # UiKit.Sign: walnut, padded by its content rect at 0.3 (at least 8)
    wd = UICORE_SCALE["WoodDark"]
    p = pad_of("frame_wood_dark", wd, 8.0)
    lh = metrics("Bold").line(21.0)
    w, h = metrics("Bold").width("Livrer ici", 21.0) + p[0] + p[2], lh + p[1] + p[3]
    yb = at_mid(h)
    sprite_at("frame_wood_dark", x, yb, wd, w, h)
    text_line("Livrer ici", x + w / 2.0, yb + p[3] + lh / 2.0, 21.0, "#FFF7E8", "Bold")
    x = group(x, w, "sign, WoodDark %.2f" % wd)
    # UiKit.TitleSign: a flat walnut plate (padding 22, 6) inside the branches, padded by their
    # content rect at 0.4
    br = UICORE_SCALE["Branch"]
    p = pad_of("frame_branch", br, 4.0)
    lh = metrics("Bold").line(30.0)
    pw_, ph_ = metrics("Bold").width("PAUSE", 30.0) + 44.0, lh + 12.0
    w, h = pw_ + p[0] + p[2], ph_ + p[1] + p[3]
    yb = at_mid(h)
    sprite_at("frame_branch", x, yb, br, w, h)
    flat(x + p[0] - 2, yb + p[3] - 3, pw_ + 4, ph_ + 5, "#3C2A1C")
    flat(x + p[0], yb + p[3], pw_, ph_, "#5A3F2B")
    text_line("PAUSE", x + w / 2.0, yb + p[3] + ph_ / 2.0, 30.0, "#FFF7E8", "Bold")
    group(x, w, "title, Branch %.2f" % br)

    # the content rect at 2x: each pair is UICORE's setup and the other one; the outline is
    # the content rect at the drawn scale, green when the child fits in it, red when not
    ky = mid + 104
    label("Content-rect check at 2x: the outline is the sprite's content rect at the drawn slice scale, green "
          "when the child fits in it, red when it does not", margin, Y(ky, 24) + 4, 22.0, "#FFF3E0",
          weight="SemiBold")
    m2 = 2.0
    top2 = ky + 44                                        # the pieces hang from this line
    notes_y = top2 + 84 * m2 + 12

    def check_note(x, lines):
        for i, t in enumerate(lines):
            note(t, x, Y(notes_y + i * 18, 14))

    def room(box):
        return (box[2] - box[0]) / m2, (box[3] - box[1]) / m2

    x = margin
    for name, key, h, wide in (("key_blank", "E", 26.0, False), ("key_wide", "Maj", 28.0, True)):
        fs = max(11.0, h * (0.42 if wide else 0.5))
        for s_, who in ((kc, "UICORE %.2f" % kc), (0.5, "0.5")):
            w, box, ok = key_face(name, key, h, x, Y(top2, h * m2), s_, wide=wide, m=m2)
            outline(box, ok)
            check_note(x, (who, "%.0f px tall" % room(box)[1], "for %.0f px" % fs))
            x += max(w * m2, 72.0) + 14
        x += 26
    # the pocket with its longest caption: the rect outlined is the plain face at the sides
    # (PocketsView pads them 4 px) and the content rect above and below
    for s_, who in ((ss, "UICORE %.2f" % ss), (0.5, "0.5")):
        box, (iw, ih), ok = pocket(x, Y(top2, 84 * m2), s_, "icon_money", "Vase en porcelaine", m=m2)
        outline(box, ok)
        rw, rh = room(box)
        check_note(x, (who, "%.0f x %.0f px" % (rw, rh), "for %.0f x %.0f" % (iw, ih)))
        x += 78 * m2 + 18
    x += 22
    # the clock on the contract card: UICORE pads it 10 x 4 on walnut at 0.3; the sign recipe
    # pads it by the content rect
    fm = metrics("Bold")
    lh = fm.line(21.0)
    cw = 20.0 + 6.0 + fm.width("07:32", 21.0)
    for pad, who in (((10.0, 4.0, 10.0, 4.0), "UICORE: WoodDark 0.3, pad 10 x 4"),
                     (pad_of("frame_wood_dark", wd, 8.0), "WoodDark.Padding (a sign)")):
        w, h = cw + pad[0] + pad[2], lh + pad[1] + pad[3]
        yb = Y(top2, h * m2)
        sprite_at("frame_wood_dark", x, yb, wd * m2, w * m2, h * m2)
        rb = yb + (pad[3] + lh / 2.0) * m2
        sprite_at("icon_clock", x + pad[0] * m2, rb - 10.0 * m2, 20.0 * m2 / sprites["icon_clock"]["size"][1])
        text_line("07:32", x + (pad[0] + 26.0) * m2, rb, 21.0, "#FFF7E8", "Bold", "LEFT", m2)
        ok = holds("frame_wood_dark", wd, w, h, pad, cw, 21.0)
        outline(content_box(x, yb, w, h, pad_of("frame_wood_dark", wd), m2), ok)
        check_note(x, (who, "%.0f px tall" % h, "fits" if ok else "squashed, on the battens"))
        x += max(w * m2, fm.width(who, 14.0)) + 18
    x += 22
    # PauseView's option bar, 150 x 14: UICORE draws the case at 0.3 and pads the tape by 3;
    # a width-only sprite at 14 / 80 with the tape in its content rect. On the light ground,
    # so neither piece straddles the two.
    x = max(x, SHEET_W / 2 + 20)
    for s_, pad, fill_s, who in ((bs, (3.0, 3.0, 3.0, 3.0), bs, "UICORE: 0.3, pad 3"),
                                 (14.0 / 80.0, pad_of("bar_tape_bg", 14.0 / 80.0), 14.0 / 80.0,
                                  "14 / 80, content rect")):
        yb = Y(top2, 14.0 * m2)
        sprite_at("bar_tape_bg", x, yb, s_ * m2, 150.0 * m2, 14.0 * m2)
        sprite_at("bar_tape_fill", x + pad[0] * m2, yb + pad[3] * m2, fill_s * m2,
                  (150.0 - pad[0] - pad[2]) * 0.6 * m2, (14.0 - pad[1] - pad[3]) * m2)
        ok = holds("bar_tape_bg", s_, 150.0, 14.0, pad, 0.0, 0.0)
        outline(content_box(x, yb, 150.0, 14.0, pad_of("bar_tape_bg", s_), m2), ok)
        check_note(x, (who, "fits" if ok else "case squashed, tape on the case"))
        x += 150.0 * m2 + 18

    # stretch demo of the frames at 1x
    sy = ky + 330
    label("9-slice at 1080p size, stretched with the manifest borders", margin, Y(sy, 24) + 4, 22.0, "#FFF3E0",
          weight="SemiBold")
    x = margin
    for n, w, h in (("frame_wood", 300, 190), ("frame_wood_dark", 420, 140), ("frame_cardboard", 260, 220),
                    ("frame_branch", 380, 210), ("frame_tag", 300, 56), ("key_wide", 150, 44),
                    ("compass_tape_bg", 300, 36)):
        sprite_at(n, x, Y(sy + 40, h), d, w, h)
        x += w + 30
    frame_camera(SHEET_W, total_h, scale=1.0)
    SC.eevee.taa_render_samples = 16
    render_to(sheet_path)
    SC.render.film_transparent = True
    SC.eevee.taa_render_samples = 48
    # the runtime-true glyphs, pasted over the render (row 0 at the bottom, as load_px reads)
    sheet = load_px(sheet_path)
    for px_, py_, img in post:
        paste(sheet, img, px_, py_)
    sheet[..., 3] = 1.0
    save_png(sheet_path, sheet)


# ----------------------------------------------------------------------------- style comparison


def render_compare(path, work):
    """Decision 3: the key cap, pad A and the box icon in EEVEE and in Workbench (flat light,
    object outline), each at 2x, then downscaled to 48 and 32 px, on dark and light grounds."""
    names = ("key_blank", "pad_a", "icon_box")
    rows = []
    for style in ("eevee", "workbench"):
        setup_scene(style)
        row = []
        for n in names:
            s = next(x for x in SPECS if x.name == n)
            build_sprite(s)
            frame_camera(s.w, s.h)
            raw = os.path.join(work, "compare_%s_%s.png" % (style, n))
            render_to(raw)
            row.append(finish(load_px(raw), s))
        rows.append(row)
    cell = 150
    W = len(names) * (cell + 48 + 32 + 60) + 40
    Hh = 2 * 2 * (cell + 20) + 40
    canvas = np.zeros((Hh, W, 3), dtype=np.float32)
    for gi, ground in enumerate(("#3F3A35", "#E9DFD0")):
        y0 = Hh - (gi + 1) * Hh // 2
        canvas[y0:y0 + Hh // 2, :] = np.array(hex_rgb(ground), np.float32)
    for gi in range(2):
        for ri, row in enumerate(rows):
            y = Hh - 20 - (gi * 2 + ri + 1) * (cell + 20)
            x = 20
            for img in row:
                paste(canvas, img, x, y)
                x += img.shape[1] + 20
                for size in (48, 32):
                    paste(canvas, downscale(img, size), x, y)
                    x += size + 20
                x += 10
    out = np.ones((Hh, W, 4), dtype=np.float32)
    out[..., :3] = canvas
    save_png(path, out)
    print("[ui_kit] style comparison (top rows EEVEE, then Workbench; dark ground then light)", path)


# ----------------------------------------------------------------------------- main


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="render_ui_kit")
    p.add_argument("--out", default=DEFAULT_OUT)
    p.add_argument("--font-dir", default=DEFAULT_FONTS)
    p.add_argument("--only", default="")
    p.add_argument("--sheet", default=None)
    p.add_argument("--sheet-only", action="store_true")
    p.add_argument("--cs", default=None)
    p.add_argument("--compare", default=None)
    p.add_argument("--work", default=os.path.join(tempfile.gettempdir(), "render_ui_kit"))
    return p.parse_args(argv)


def main():
    global FONT_DIR
    args = parse_args()
    FONT_DIR = args.font_dir
    register()
    os.makedirs(args.out, exist_ok=True)
    os.makedirs(args.work, exist_ok=True)
    if args.compare:
        render_compare(args.compare, args.work)
        return
    setup_scene("eevee")
    prefixes = [p for p in args.only.split(",") if p]
    todo = [s for s in SPECS if not prefixes or any(s.name.startswith(p) for p in prefixes)]
    entries = []
    t0 = time.time()
    if not args.sheet_only:
        for s in todo:
            if s.build is None:          # bg_paper: made, not rendered
                save_png(os.path.join(args.out, s.name + ".png"), paper_texture(int(s.w * SCALE)))
                entries.append(entry(s, s.name))
                print("[ui_kit] %-26s %4dx%-4d (noise)" % (s.name, s.w * SCALE, s.h * SCALE))
                continue
            build_sprite(s)
            frame_camera(s.w, s.h)
            raw = os.path.join(args.work, s.name + ".png")
            render_to(raw)
            px = load_px(raw)
            for n in names_of(s):
                state = n[len(s.name) + 1:] if s.states else None
                img = finish(px, s, state)
                size = margin = None
                if s.trim:
                    img, margin, lost = trim_to_art(img, int(round(s.pad * SCALE)))
                    size = (img.shape[1], img.shape[0])
                    print("[ui_kit] %s trimmed to %dx%d, margin %d, alpha cropped away %.3f"
                          % (n, size[0], size[1], margin, lost))
                save_png(os.path.join(args.out, n + ".png"), img)
                entries.append(entry(s, n, size, margin))
            print("[ui_kit] %-26s %4dx%-4d %s" % (s.name, img.shape[1], img.shape[0],
                                                  "x%d states" % len(s.states) if s.states else ""))
        manifest = write_manifest(args.out, entries)
    else:
        # nothing rendered: the sprite lines stay as they are on disk, and the header (the note,
        # the palette) is rewritten from this script so the manifest never lags its documentation
        manifest = write_manifest(args.out, [])
    print("[ui_kit] %d sprites in %.1f s" % (len(entries), time.time() - t0))
    if args.cs:
        write_cs(args.cs, manifest)
        print("[ui_kit] C# metrics", args.cs)
    if args.sheet or args.sheet_only:
        path = args.sheet or os.path.join(args.out, "contact_sheet.png")
        os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
        build_sheet(args.out, path, manifest)
        print("[ui_kit] contact sheet", path)


if __name__ == "__main__":
    main()
