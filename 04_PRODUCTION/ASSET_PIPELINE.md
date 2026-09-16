# ASSET PIPELINE

_The 3D / asset path specifically._

## Flow
Free asset (Unity Asset Store free) or AI generation (Astra / GPT) -> Blender (cleanup, scale, pivots, materials, rig if needed) -> export glTF / GLB -> Unity import + normalize -> prefab -> validate -> status update.

## Rules
- Free first; generate only when it beats free (`../08_BUSINESS/COST_MODEL.md`).
- One shared rig, minimal rigging (`../05_ART/CHARACTERS.md`).
- Everything conforms to `../05_ART/ASSET_SPECS.md`.
