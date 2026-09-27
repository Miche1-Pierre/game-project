# Bathrobe references

Inputs the team supplied for the grandmother's bathrobe (`SM_Crew_Chest_Bathrobe`, built by `tools/blender/model_bathrobe.py`). Internal reference only: nothing here ships, and nothing here is copied into an asset.

| File | What it is | Source | What it is used for |
|---|---|---|---|
| `concept_sheet.webp` | Concept sheet "GRAND-MA BATHROBE, LOW POLY ASSET": front, side and back views, a wireframe at about 1.2K tris, five pink swatches, a heart texture | Supplied by Jonathan in a Claude session on 2026-09-20, recovered from that session's temporary files on 2026-09-25. Where the image itself came from is not recorded | Silhouette and parts: shawl collar, knotted belt with hanging ends, patch pockets, folded cuffs, ankle length, soft faceted cloth. The heart pattern is ignored: the project is flat colour |
| not stored | Sketchfab model "Female Long Bathrobe 4 textures" | https://sketchfab.com/3d-models/female-long-bathrobe-4-textures-34a7debfe6064d85a4669e35582715fa | A third-party model, looked at for proportions only. The screenshot supplied at the time is deliberately not kept, because it shows a personal browser bookmarks bar |

## Anti-reference
Version 1 of the robe, commit `b64b648` (784 tris), judged by the team on 2026-09-20 to read as samurai armour rather than cloth. Its renders are `_ArtSource/bathrobe_*.png` at that commit, for example `git show b64b648:_ArtSource/bathrobe_1_front.png`. Any later version is checked against the five faults diagnosed then:

1. The cross section is a 12 sided superellipse with hard corners.
2. Every part meets its neighbour in a step rather than a blend.
3. The shoulder ring is wide and square enough to read as a pauldron.
4. The cuff stands too proud.
5. The skirt is a smooth cone with no vertical folds.
