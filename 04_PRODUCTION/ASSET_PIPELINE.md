# ASSET PIPELINE

_The 3D / asset path specifically._

## Flow
Free asset (Unity Asset Store free) or AI generation (Astra / GPT) -> Blender (cleanup, scale, pivots, materials, rig if needed) -> export FBX -> Unity import + normalize -> prefab, by hand -> validate -> status update.

FBX only (not glTF / GLB), Built-in RP, Gamma. The prefab factories were removed on 2026-09-20 (commit 7e70bd7): Map 1 is assembled by hand. The gates between these steps, from the script's checks to the human verdict in context, are in `../05_ART/STYLE_GUIDE.md` section 20 (ADR-011).

## Rules
- Free first; generate only when it beats free (`../08_BUSINESS/COST_MODEL.md`).
- One shared rig, minimal rigging (`../05_ART/CHARACTERS.md`).
- Everything conforms to `../05_ART/ASSET_SPECS.md`.
