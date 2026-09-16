# ARCHITECTURE

_The overall shape of the code. Built like production software, because we are software engineers: robust, modular, testable._

## Principles
- Object-oriented C#, composition over inheritance.
- **Modular** systems with clear boundaries (`CSHARP_ARCHITECTURE.md`).
- **Mirror folder structure**: the on-disk layout matches the system layout (Core/, Gameplay/, Player/, World/, Interaction/, Net/, UI/).
- **Config-driven**: tunable values live in ScriptableObject config assets, not hard-coded (`DATA_ARCHITECTURE.md`).
- **No backend, no database** by default. The game runs client-side over Steam P2P (like Meccha, PEAK, Lethal). Server infrastructure only if a concept demands it, with an ADR.
- Assets: 3D as glTF / GLB out of Blender, imported and normalized in Unity.

## Layers (indicative, refined with the concept)
Core (bootstrap, services, events) -> Gameplay (rules, systems) -> Entities (player, NPC, objects) -> Presentation (view, audio, VFX) -> Net (sync).

## Rule
Define boundaries in docs before writing classes. Do not generate a large class graph before the prototype needs it (`../CLAUDE.md` section 7).
