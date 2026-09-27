# STYLE GUIDE

**Status:** Pre-concept / pre-greybox. **Decision:** Preliminary constraints only. **Owner:** Pierre + Jonathan. **Last updated:** 2026-09-27.

Preliminary visual consistency rules. Their purpose is to keep asset generation (Claude, Astra, Blender) from drifting once we start producing. The final style guide is completed after the concept and visual prototype are validated.

Sections 18 to 20 record current practice, measured on the bathrobe pilot and read by the scripts through `style/profile.json` ([ADR-011](../decisions/ADR-011-asset-workflow.md), proposed). They are not the final direction (§16).

## 1. Current style hypothesis
Default: clean stylized 3D, low-poly, smooth forms. May change after concept validation. The goal is not maximum detail but a visual language that is immediately readable, recognizable, coherent, expressive and feasible to produce.

## 2. Shape language
Prefer simple geometric forms, clean silhouettes, rounded or intentionally exaggerated shapes, strong primary forms, readable proportions. Avoid unnecessary micro-detail. Between a complex realistic object and a simplified recognizable version, prefer the simplified one unless the detail has gameplay value.

## 3. Silhouette rule
Silhouette matters more than surface detail. Players, important NPCs and gameplay-critical objects should be identifiable primarily through shape. If an object only becomes recognizable from its texture, reconsider it.

## 4. Material rule
Limited material vocabulary: few materials per asset, consistent roughness/value relationships, simple textures, controlled surface variation. Do not generate every asset with a different material language.

## 5. Color rule
Color primarily communicates gameplay importance, team/player distinction, interaction states, environmental hierarchy and visual identity. Final palette is TBD. No permanent palette before the concept exists. The colours in use today are recorded, not chosen, in `style/profile.json` (`palette`, sRGB as Unity shows them).

## 6. Character rule
Characters should be recognizable, visually distinct from the environment, cheap to rig, cheap to animate, and readable during multiplayer action. Prefer simple bodies and strong proportions over detailed models.

## 7. Animation rule
Prioritize readability and expression over realism. Reuse existing animations whenever possible. Procedural or physics-based reactions are preferred when they create more gameplay value for less effort. Avoid bespoke animation for actions that simple movement or procedural reactions can communicate.

## 8. Environment rule
Environment assets are modular and reusable. Prefer asset families over one-off objects: one structural kit, several variations, reusable props, controlled material variations, simple transformations. Create visual variety without multiplying production cost.

## 9. Interaction readability
Interactive objects should be distinguishable without relying only on UI. Important interactions have clear feedback through some combination of shape, placement, animation, state change, VFX, sound or restrained UI. Players should not need to memorize obscure visual conventions.

## 10. Chaos readability
When several players and systems interact at once: important actors stay visible, important objects stay distinguishable, major events have clear feedback, VFX do not overwhelm the scene, and visual noise is avoided. The style should become more readable under chaos, not less.

## 11. Asset generation checks
Every generated asset must pass three checks:
1. **Style:** does it belong to the same visual world?
2. **Readability:** can the player understand what it is and whether it matters?
3. **Production:** can we reproduce or modify it efficiently?

If no, simplify or regenerate.

## 12. AI generation constraints
AI-generated assets must not dictate the style. The pipeline is: style, then asset requirements, then generation, then cleanup, then integration, then validation. Not: generate, accept whatever appears, build the game around it. Normalize generated assets where needed in scale, proportions, materials, texture density, polygon density, naming, pivot, rig and animation compatibility.

## 13. Production constraints
The style must stay compatible with Unity 6, Blender, AI-assisted 3D generation, simple texture workflows, limited custom rigging, reusable animation assets, and a two-person team. Any technique that requires disproportionate manual work is challenged.

## 14. Quality bar
Aim for **simple + coherent + intentional** rather than **complex + detailed + inconsistent**. A simple asset that belongs perfectly to the game's visual language beats a technically impressive asset that looks imported from another game.

## 15. References
Current references:
- **Jusant:** clean stylized 3D, strong environmental presentation.
- **Bruno Simon:** playful, lightweight, interactive 3D.

Directional only. More added once the concept is selected.

## 16. Not yet defined
Intentionally TBD: final art style, palette, typography, character proportions, environment language, lighting, shaders, VFX, UI style, animation language, texture treatment, camera presentation. Do not lock prematurely.

## 17. Core rule
**Silhouette first. Readability second. Consistency third. Detail last.** The visual style serves the game rather than determining what the game becomes.

## 18. Working references v0
A working reference is a file in the project that a new asset is measured against: its proportions, density, construction and colours. §15 lists directional references outside the project; this section is about the ones inside it.

**Where they live.** `style/profile.json`, key `references`. One entry per glob, with the family it informs, a tier, an origin (hand, agent or pack), whether the map uses it, and who approved it and when. Files never move: tier folders would break the Unity GUIDs and Pierre's paths. The last matching entry wins, so an exception is written after the rule it refines.

| Tier | Meaning | For new work |
|---|---|---|
| canonical | the style, as its author made it | copy its construction |
| acceptable | consistent with it, made by an agent or a pack | a model, checked against a canonical one |
| legacy | superseded, may still be placed in the map | do not model new pieces on it |
| do_not_copy | reads wrong for this game | never a model, never a source of parts |
| anti | a version the team rejected, kept with its diagnosis | check every new version against its listed faults |

**v0, proposed.**
- **Canonical:** the PierreKit (Pierre, by hand), and the Floreswa crew body every worn piece is fitted to.
- **Acceptable:** the PKX pieces (agent, pending Pierre), the GrandmaKit furniture and props placed in the map, the BrokenVector storage, car and tree packs, the bathrobe v2.
- **Legacy:** the GrandmaKit and CottageKit architecture, and the first greybox pieces of 2026-09-17.
- **Do not copy:** the dungeon and pistol packs.
- **Anti:** the bathrobe v1, kept by its commit (`b64b648`); its five faults are listed in `_ArtSource/references/bathrobe/SOURCE.md`.

**Promotion is human.** A tier changes only with `approved_by` and `date` filled in. Jonathan approves garments and crew, Pierre the environment. Nothing is promoted by a script or an agent.

**Families.** The conventions of each family are in `profile.json` (`families`), recorded from the files rather than chosen:
- **PierreKit structure:** a 2 BU module imported at 1.5, length on X, outside face +Z, pivot at the bottom centre. A 1.80 m player is 1.20 BU in Blender.
- **Garments:** modelled on the imported crew body (2.588 BU, imported at 0.6956 to stand 1.80 m), skinned preset, smooth shaded below 45 degrees, only the sleeves follow the arms, judged on the crew with its arms down.

**Measured, not guessed.** `tools/blender/measure_assets.py` writes `style/metrics.json` for the PierreKit (`PK_*`) and its extension (`PKX_*`), from the exported FBX only.

**For Pierre: do the numbers tell your pieces from the PKX?** Almost, not completely. No metric splits the 26 hand-made modules from the 26 PKX pieces without overlap, but three come close, and they say the same thing:
- **The PKX lean off the grid far more.** The share of surface tilted by more than half a degree: median 7 % for yours, 53 % for theirs (a threshold at 18 % puts 47 files of 52 on the right side). The agent built each piece as "a bevelled box with a little jitter" (`export_pierrekit.py`), and its jitter is about five times your wonkiness.
- **The PKX chamfer almost every edge.** Of the edges where the surface turns, the share that is a chamfer: median 0.53 for yours, 0.90 for theirs (45 of 52).
- **Your modules are nearly all triangles, the PKX mostly quads:** 4 % against 69 % (51 of 52). Both go through the same exporter, so this comes from how the sources were built. It may say nothing about the eye.

Size and density do not separate them. The question is whether your eye agrees: if the PKX read as too wonky and too bevelled to you, lean and chamfer become the first concrete IMPROVE rule for the kit. If they do not, the style guard stays visual and yours.

## 19. Generation modes
Every asset is announced before it is built: family, mode, references kept and ignored with the reason for each, what is locked, the budget. The announcement becomes the generator script's docstring.

| Mode | For | Preserve | May improve | Avoid | Human check |
|---|---|---|---|---|---|
| STRICT_MATCH | a missing piece of a family with a canonical reference | module, import scale, orientation, pivot, material names, edge treatment, density | nothing | new materials, new proportions | the family's owner |
| STYLE_CONSISTENT | a new asset in an existing family | the family's conventions, the palette, the budget of comparable references | what the spec asks for | the traits of do_not_copy and anti entries | a verdict on the review sheet |
| IMPROVE | a new version of an asset a verdict rejected | the fit, the slot, gameplay dimensions, whatever the verdict did not criticise | the diagnosed faults, one by one, each with a check | the anti reference's listed faults | two verdicts: the review sheet, then in context |

In every mode:
- **IMPROVE never touches Pierre's files** (`_ArtSource/assets.blend`, the PierreKit sources), and never changes a dimension that gameplay depends on without a Design flag: a sofa that fits through the door is a level design fact, not a style.
- **A new family always gets a human verdict,** whatever the mode.
- **`_ArtSource/assets.blend` is never written, never rendered, and its frame never changes:** 673 of its objects carry stale keys and snap to old positions.
- **No environment generation and no prefab factory** before the go / no-go and Pierre's agreement (ADR-011).

## 20. Review checklist and failure owners
**The gates, in order.** A piece moves on only when the previous gate passed.
1. **The script's checks.** Numbers that refuse the export: enclosure, weights, budget. Run Blender with `--python-exit-code 1`, or a refusal looks like a success.
2. **The review sheet.** Four views, the body's silhouette with and without the piece, the piece seen at 8 m, and the references side by side. Workbench, Standard view transform, colours as Unity shows them (`movers_blender.render_turnaround`).
3. **The human verdict.** This is the gate. The agent may pre-check the anti reference's faults as a list of PASS and FAIL, never as a pass: its own review passed the robe twice where the team's eyes did not.
4. **The import report.** `MoversInspectCLI.ReportAsset`: triangles per submesh equal to Blender's, the Standard shader (magenta fails), and for a skinned piece its bones and the joint fit.
5. **In context, the final gate.** For a worn piece, `MoversWearCLI`: on the player's body in `Map01_PierreKit_House`, at rest and carrying, at 3 m and 8 m, no `[CrewEquip]` warning, no stretched edge. Then the human verdict again, on those shots.

**What is checked,** the three tests of `CHARACTERS.md` first:
- **The 8 metre test:** another player can say who it is and what they are wearing.
- **The silhouette test:** cut to black, the piece changes the outline, or it is a texture.
- **The social test:** seeing it on someone tells you something useful.
- **Its material reads:** cloth as cloth, not plate (smooth shading, folds).
- **Colours** match the palette as Unity shows them.
- **Budget** against references of the same class, not against a guessed range.
- **Construction:** normals out (reviewed with back faces culled), the family's pivot and orientation, the import scale in the `.meta`.

**Every hint a script prints is judged, not only displayed.** A `MEASURE` line either has a threshold that fails the run or a named person who reads it at the verdict. The robe's needles were printed on every run ("hand.L 1%") and read by nobody.

**Failure owners.** A fault is fixed where it is owned, not where it shows: a rotation that is wrong in Unity is fixed in the importer, not in Blender.

| Owner | Owns | Example from the pilot |
|---|---|---|
| Blender | geometry, weights, normals, shading, export flags, the colour in the FBX | three hem vertices weighted to the hands; `check_weights` now refuses the export |
| Unity | importer settings: scale, axis conversion, Read/Write, material remap, avatar | a skinned piece imports with Read/Write on |
| Design | dimensions and weight that gameplay uses, the collider, the slot, what the piece says | the robe closes, so its trim carries the player colour |
| Code | `EquipItem`, the `CrewEquip` binding, the review tools | bind poses rebaked into the body's space (`BakeIntoBodySpace`) |
| Animation | poses, clips, controller layers | the carry pose's forearms through the chest, and no pose for empty hands |
