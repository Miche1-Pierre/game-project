# ADR-011: Assets go through working references, a style profile and a human verdict

## Status
**Proposed (2026-09-27), Jonathan.** It becomes "Accepted, and flagged" when both developers agree, and Pierre decides the environment tiers. Numbered 011: the plan called it ADR-008, and Pierre's decisions took 008 (everything breaks), 009 (the vertical slice) and 010 (the presentation layer) in the meantime.

Nothing here outranks action 1 of `00_PROJECT/PROJECT_STATE.md`: play the house with a second person.

## Context
A proposed AI asset workflow arrived while the go / no-go is still untested: text to Blender to Unity, a persistent "style DNA", references ranked Canonical / Acceptable / Legacy / DoNotCopy, three modes (STRICT_MATCH, STYLE_CONSISTENT, IMPROVE), promotion by humans only.

Half of it was already practised, informally:
- **Pierre** built the PierreKit by hand (26 modules in `_ArtSource/assets.blend`), named it the reference style, and had an agent make the PKX pieces in that style. On 2026-09-20 he had the prefab factories removed (commit 7e70bd7): Map 1 is assembled by hand.
- **Jonathan** writes versioned Blender scripts whose checks refuse an export, renders four views and gives the verdict.

The gaps were precise. The robe's references lived in a temporary folder. The family conventions were written nowhere (import 1.5 and outside face +Z for the PierreKit, -Z elsewhere). Several palettes competed, and the two garment scripts used two different pinks. `export()` existed in five copies with different flags, one of them falling back to an exporter that writes centimetres. No register said which assets are models and which must not be copied.

So the workflow was run once, end to end, on a real asset in progress: the grandmother's bathrobe, second version, in IMPROVE mode (2026-09-25 and 26, `changelog/CHANGELOG.md`). Only what that pilot used is formalised here.

What the pilot taught, each point measured:
1. **The agent's own review passed the robe twice where the team's eyes did not** (v1, then v2 flat shaded). The human verdict is the gate.
2. **Two of the robe's faults only showed in Unity, worn in the map:** bind poses that tore it apart, and three hem vertices that followed the hands. The body it was judged on had no pose for empty hands. Validation in context is the final gate.
3. **The review renders lied about colour.** The project is Gamma and Blender renders through AgX by default: Unity shows #F294B8 where the render showed a mauve. Review with the Standard view transform, colours converted to what Unity shows.
4. **`blender -b` exits 0 when the script raises,** so a refused export looked like a success. Pass `--python-exit-code 1`.
5. **A compile error keeps Unity busy in batchmode for over ten minutes.** Compile on its own before any `-executeMethod`.
6. **A skinned renderer baked before its first draw returns its bind pose,** which let the needles pass the stretch check once. Check after a draw.
7. **A hint printed on every run and judged by nobody** ("skirt weight share ... hand.L 1%") was the needle bug in plain sight. Every hint a script prints gets a threshold or a named reader.

## Decision
1. **No new style document.** `05_ART/STYLE_GUIDE.md` gains three sections (18 to 20): working references, generation modes, the review checklist with its failure owners. One data file, `05_ART/style/profile.json`, is read by the scripts: palette, families, references, export presets, review settings. It records current practice; the final direction stays open (ART_DIRECTION §16).
2. **The palette is in sRGB, as Unity shows it.** Seeded from Pierre's `PK_*.mat`, the four `MAT_Crew_0N` and the garment pink.
3. **References are a register, not folders.** Each entry is a glob with a tier: canonical, acceptable, legacy, do_not_copy or anti. Files never move. The last matching entry wins, so an exception follows its rule. A tier changes only with a named approver and a date: Jonathan for garments and crew, Pierre for the environment.
4. **Three modes, each a contract** of what is preserved, what may improve and what is avoided (STYLE_GUIDE §19). IMPROVE never touches Pierre's files, and never moves a gameplay dimension without a Design flag.
5. **The spec is the generator's docstring,** announced before building: family, mode, references kept and ignored with the reason for each, what is locked, the budget. No YAML: Blender ships without PyYAML.
6. **One Blender module,** `tools/blender/movers_blender.py`: repo-relative paths, the body import, materials from the palette, three export presets (rigid, skinned, animation) that fail loudly, and the review render (Standard view transform, silhouette, the 8 m view). The versioned script is the source and runs headless; the Blender MCP is for looking at a scene live.
7. **Gates, in order:** the script's checks, the review sheet, the human verdict, the Unity import report (`MoversInspectCLI.ReportAsset`), the piece in context (`MoversWearCLI` for anything worn, the final gate), the human verdict again. A failure is fixed by its owner: Blender, Unity, Design, Code or Animation.
8. **FBX only, Built-in RP, Standard shader, Gamma colour space.**
9. **Three skills** (`make-asset`, `asset-to-unity`, `art-style`) live in Jonathan's user folder and move to `.claude/skills/` in the repo once this ADR is accepted.

## Why
- **The eye failed where the numbers passed, and the other way round.** The checks caught every measurable fault (enclosure, weights), the eye caught what no check could name (armour, lamellar plates). Neither replaces the other, so both are gates.
- **A register keeps what already works.** Moving files into tier folders would break every GUID in Pierre's scenes and every path he knows, to say something a glob says as well.
- **One module stops a drift that already happened.** The copies of `export()` had drifted apart, and one fell back to `wm.fbx_export`, which writes centimetres: a mesh lands 100 times off. A preset that fails loudly is cheaper than a fallback that succeeds wrongly.
- **Formalising only what ran keeps it small.** Everything here was used on the robe. What the proposal had and the robe did not need is under "What was not done".

## What it costs
- **Human time at every gate.** The verdict cannot be delegated. That is the point, and it is also the bottleneck.
- **A register to keep honest.** Tiers go stale unless every promotion is written down, and a stale register is worse than none, because an agent trusts it.
- **The Gamma quirk lives in one function.** The FBX carries the sRGB value, which Unity shows as it is (`useSRGBMaterialColor` on); Workbench gets its linear conversion, so a Standard render shows what Unity shows. Anyone opening the `.blend` in Material Preview sees the colours too light.
- **The skills are invisible to Pierre until this is accepted,** because they live outside the repo.

## What was not done
Rejected or reduced from the proposal. Reasons also in `04_PRODUCTION/REJECTED.md`.
- **"Native MCP operations before Python":** the Blender MCP only changes a scene through Python. The versioned script is the source.
- **A dedicated `AssetValidation.unity` scene:** the real context exists, `Map01_PierreKit_House` with its mirror and the 1.80 m body. Captures run in Play mode and never save.
- **GLB as an option, and URP in the example:** FBX only, Built-in RP, Gamma.
- **Automated import, prefab and collider for the environment:** Pierre removed the prefab factories on 2026-09-20 and assembles Map 1 by hand. A carried object's collider is a design decision. A material remap by name reuses `MoversMaterialFixCLI`.
- **Generating environment pieces:** not before the go / no-go and Pierre's agreement (the missing roof valley, verge, dormer, leaves and shutters).
- **Tier folders:** replaced by the register.
- **A YAML spec per asset:** replaced by the docstring.
- **Six skills:** three.
- **Multi-agent judging by default:** a ranked vote exhausted the session limit during the pilot, and the human verdict is the gate anyway.
- **Automatic promotion:** never.
- **Fine-tuning:** the proposal itself concludes against it.

## Revisit when
- **The two-player session** (PROJECT_STATE action 1). If nobody laughs, art tooling stops and the verb comes first.
- **Pierre answers on the environment tiers and on `05_ART/style/metrics.json`.** If the measurements separate his hand-made pieces from the PKX, the gap becomes the first concrete IMPROVE rule for the environment. If they do not, the style guard stays visual and human.
- **The next asset goes through.** If a gate is skipped or a skill step ignored, simplify the workflow rather than add to it.
- **The final art direction is decided** (ART_DIRECTION §16). The profile then stops being a record of practice and becomes a target.
