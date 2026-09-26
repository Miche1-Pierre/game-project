# ASSET STATUS

_Every asset follows this pipeline. No final asset before the relevant system is validated._

## Stages
Concept -> Greybox -> Placeholder -> Generated -> Cleaned -> Integrated -> Final

## Register
| Asset | System | Stage | Note |
|---|---|---|---|
| Crew, 4 colours | Core loop | Placeholder | `UnityProject/Assets/_Movers/Generated/Characters/PF_Crew_0*.prefab`. One Floreswa body, shirt material swapped, shared mesh. **Height 1.80 m**, matching the player CharacterController, set by the scale factor on `male01_1.fbx` (0.6956). The pack imports at 2.59 m. Keep the prefab root scale at 1: the four are variants, so the model is the one place to change size. Since 2026-09-26 each carries `CrewPose`: relaxed with empty hands, carry pose while carrying. |
| Carry idle clip | Carry | Placeholder | `Anim_Carry_Idle.fbx`, authored in Blender, humanoid, looping. The pack ships no animation. Pose fixed 2026-09-26: the elbows ran inside the chest, they now sit 12 cm out. |
| Relaxed idle clip | Crew | Placeholder | `Anim_Relaxed_Idle.fbx`, `tools/blender/author_carry_clip.py --pose relaxed`, the default state of `AC_Crew` (2026-09-26). Imported with the carry clip's settings by `MoversCrewPoseCLI.Setup`. |
| Grandmother's bathrobe | Equip | Integrated | `_Project/Art/Crew/SM_Crew_Chest_Bathrobe.fbx` from `tools/blender/model_bathrobe.py`: skinned, 1144 tris, smooth shaded, trim painted with the crew colour by `CrewEquip`. Validated worn in `Map01_PierreKit_House`, both poses, by Jonathan on 2026-09-26. Not placed in the map yet. References: `_ArtSource/references/bathrobe/`. |

## Pipeline decision (2026-09-16)
**Free asset packs as the base, completed by AI 3D generation on our existing subscriptions.** Both, not one or the other.

- Start from free packs. Spend money only if the greybox validates and we can picture the final look.
- Generation sits on top of the packs, to fill gaps and to unify, not to produce everything from scratch.
- Greybox uses grey boxes and placeholders only. No pack integration, no generation, before the go / no-go.

**Steam consequence, accepted:** generated assets that ship must be declared on the store page. This is a form to fill honestly at store-page time (`../09_STEAM/STORE_PAGE.md`), not a constraint on the pipeline. Code assistants are out of scope of that declaration.

**Art budget trigger:** the final look drives the price decision (`../08_BUSINESS/PRICING.md`). Spending on assets after the greybox is therefore a commercial lever, not a vanity cost.

_Status: pipeline decided. The register holds the crew and what is worn on it; the house kits are listed in `ASSET_LIST.md` section 11._