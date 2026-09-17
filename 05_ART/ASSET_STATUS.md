# ASSET STATUS

_Every asset follows this pipeline. No final asset before the relevant system is validated._

## Stages
Concept -> Greybox -> Placeholder -> Generated -> Cleaned -> Integrated -> Final

## Register
| Asset | System | Stage | Note |
|---|---|---|---|
| Crew, 4 colours | Core loop | Placeholder | `UnityProject/Assets/_Movers/Generated/Characters/PF_Crew_0*.prefab`. One Floreswa body, shirt material swapped, shared mesh. |
| Carry idle clip | Carry | Placeholder | `Anim_Carry_Idle.fbx`, authored in Blender, humanoid, looping. The pack ships no animation. |

## Pipeline decision (2026-09-16)
**Free asset packs as the base, completed by AI 3D generation on our existing subscriptions.** Both, not one or the other.

- Start from free packs. Spend money only if the greybox validates and we can picture the final look.
- Generation sits on top of the packs, to fill gaps and to unify, not to produce everything from scratch.
- Greybox uses grey boxes and placeholders only. No pack integration, no generation, before the go / no-go.

**Steam consequence, accepted:** generated assets that ship must be declared on the store page. This is a form to fill honestly at store-page time (`../09_STEAM/STORE_PAGE.md`), not a constraint on the pipeline. Code assistants are out of scope of that declaration.

**Art budget trigger:** the final look drives the price decision (`../08_BUSINESS/PRICING.md`). Spending on assets after the greybox is therefore a commercial lever, not a vanity cost.

_Status: pipeline decided, register empty until visual production starts._