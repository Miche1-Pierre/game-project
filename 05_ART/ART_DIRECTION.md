# ART DIRECTION

**Status:** Pre-concept / pre-greybox. **Decision:** Not locked. **Owner:** Pierre + Jonathan. **Last updated:** 2026-09-15.

This document defines the current artistic constraints and production philosophy. It does not define the final visual identity. The final art direction is decided after the concept and first greybox are validated.

## 1. Core principle
Art comes after gameplay. The project must first prove its core interaction, multiplayer dynamics and emergent situations are fun without relying on visual production. The visual direction exists to make the validated game readable, distinctive, coherent, pleasant to play, commercially presentable, and feasible for a two-person team. Art must not compensate for weak gameplay.

## 2. Current visual direction
Current default: stylized, clean, low-poly, readable 3D. This is a starting hypothesis, not a locked identity. It generally favors simple geometry, smooth surfaces, controlled detail, strong silhouettes, limited material complexity, coherent proportions, readable colors and shapes, clean lighting, and visually understandable interactions. The game should avoid requiring large quantities of highly detailed bespoke assets.

## 3. Primary constraint: readability
Because the game is expected to contain multiplayer chaos, readability has priority over visual complexity. At any important moment, players should quickly understand where the other players are, what they are doing, which objects matter, what can be interacted with, what changed, where an important event is happening, and what caused a visible consequence. Effects, environments and props support gameplay rather than obscure it.

## 4. Production philosophy
The pipeline is designed for 2 developers + AI-assisted production + limited time. Favor assets that can be generated or adapted quickly, reused across situations, modified without specialist workflows, assembled from simple components, and kept coherent with each other. Prefer systemic visual variety over quantities of unique assets: a small number of reusable environment pieces, materials, props and effects should produce multiple distinct situations.

## 5. Geometry
Default: low-poly or moderately low-detail geometry, clean topology where required, simple recognizable forms, exaggerated proportions when useful for readability, minimal unnecessary detail. Detailed geometry is introduced only when it materially improves readability, feedback, identity or commercial presentation.

## 6. Materials
Keep materials simple: few per asset, coherent material families, limited shader complexity, restrained texture requirements, strong distinction between interactive objects and background. Texture detail must not become a major production bottleneck.

## 7. Characters
Priority order: silhouette, readability, animation cost, visual identity. Characters should be recognizable from silhouette and proportions before texture detail. Avoid designs that require complex facial rigs, extensive deformation, large animation libraries, cloth or hair simulation, or highly detailed bespoke models. Keep designs compatible with inexpensive animation.

## 8. Animation
Proportionate to gameplay needs. Prefer existing reusable animations, Unity-compatible assets, simple procedural animation, animation reuse, and exaggerated physical reactions. Custom animation only when it adds clear gameplay or presentation value. Animation need not be realistic: for a chaotic multiplayer game, readable and expressive beats technically sophisticated.

## 9. Environment
Favor modular construction, reusable assets, simple recognizable architecture, strong spatial readability, clear gameplay spaces, controlled visual density. Environments support systemic interactions and stay understandable even when many players, objects and effects are active at once.

## 10. Lighting
Supports readability, navigation, object recognition, atmosphere and visual hierarchy. Lighting must not become a production-heavy dependency. The final model depends on concept and environment.

## 11. Visual effects
VFX primarily communicate gameplay events (impacts, destruction, alerts, state changes, interactions, environmental reactions, success/failure feedback). They must be readable, lightweight, coherent and inexpensive to produce.

## 12. AI-assisted asset production
May cover 3D assets, textures, concept references, materials, variations, animation support, promotional imagery. Every generated asset must pass a manual quality and consistency check. Do not accumulate visually incompatible assets just because they are individually fast to generate. The final result must feel like one game, not a collection of generated assets.

## 13. Reference direction
Current references:
- **Jusant:** clean stylized 3D presentation, visual simplicity, environmental readability.
- **Bruno Simon:** playful, lightweight, interactive 3D presentation.

These describe useful qualities. They are not direct visual targets and must not constrain the final concept. More references are added after the concept is selected.

## 14. What we explicitly avoid
Unless the concept requires them: photorealism, highly detailed characters, large bespoke environments, extensive manual texturing, complex rigging, large custom animation libraries, expensive simulation, excessive shader complexity, VFX that reduce readability, and asset production before gameplay validation.

## 15. Art production gate
Order: greybox, then gameplay validation, then visual direction, then style prototype, then reusable asset pipeline, then production assets, then polish. No large-scale art production begins before the core gameplay is validated.

## 16. Final direction decision
Intentionally open: final art style, color palette, character design, environment style, lighting style, material language, VFX language, UI visual identity, animation style, final references. Decided after the concept and greybox establish what the game actually needs visually.

## 17. Success criteria
The final art direction should be:
1. **Readable:** players understand what is happening quickly.
2. **Distinctive:** a recognizable visual identity.
3. **Coherent:** assets look like the same game.
4. **Affordable:** two developers can produce and maintain the pipeline.
5. **Commercially presentable:** competes visually with its Steam market.

Current art principle: **simple enough to produce, distinctive enough to remember, readable enough to survive chaos.**
