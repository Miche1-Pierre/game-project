# ASSET SPECS

_Baseline specs so generated and free assets stay consistent. Refined with the concept._

## Baseline
- Format: FBX out of Blender, never glTF / GLB: every importer setting and every tool in the project is FBX. Built-in RP, Standard shader, Gamma colour space. Export presets and per-family conventions: `style/profile.json`, used through `tools/blender/movers_blender.py` (ADR-011).
- Poly budget: low-poly; keep well within the 60 fps target.
- Scale: 1 unit = 1 meter; consistent pivots and orientation.
- Materials: from the shared library (`MATERIALS.md`); few per asset.
- Naming and folders mirror the code (`../03_TECHNICAL/ARCHITECTURE.md`).
- Rig: shared humanoid rig for characters (`CHARACTERS.md`).
- Source: free assets first; Astra / AI generation only when it beats free (`../08_BUSINESS/COST_MODEL.md`).

## Status pipeline
Tracked in `ASSET_STATUS.md`: Concept -> Greybox -> Placeholder -> Generated -> Cleaned -> Integrated -> Final.
