# STYLE GUIDE

**Status:** Pre-concept / pre-greybox. **Decision:** Preliminary constraints only. **Owner:** Pierre + Jonathan. **Last updated:** 2026-09-15.

Preliminary visual consistency rules. Their purpose is to keep asset generation (Claude, Astra, Blender) from drifting once we start producing. The final style guide is completed after the concept and visual prototype are validated.

## 1. Current style hypothesis
Default: clean stylized 3D, low-poly, smooth forms. May change after concept validation. The goal is not maximum detail but a visual language that is immediately readable, recognizable, coherent, expressive and feasible to produce.

## 2. Shape language
Prefer simple geometric forms, clean silhouettes, rounded or intentionally exaggerated shapes, strong primary forms, readable proportions. Avoid unnecessary micro-detail. Between a complex realistic object and a simplified recognizable version, prefer the simplified one unless the detail has gameplay value.

## 3. Silhouette rule
Silhouette matters more than surface detail. Players, important NPCs and gameplay-critical objects should be identifiable primarily through shape. If an object only becomes recognizable from its texture, reconsider it.

## 4. Material rule
Limited material vocabulary: few materials per asset, consistent roughness/value relationships, simple textures, controlled surface variation. Do not generate every asset with a different material language.

## 5. Color rule
Color primarily communicates gameplay importance, team/player distinction, interaction states, environmental hierarchy and visual identity. Final palette is TBD. No permanent palette before the concept exists.

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
