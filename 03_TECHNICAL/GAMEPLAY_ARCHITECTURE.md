# GAMEPLAY ARCHITECTURE

_How gameplay systems fit together. Concept-dependent: fleshed out once the concept is chosen._

## Principles (set now)
- Systems are small, single-purpose, and communicate through events (`EVENT_SYSTEM.md`).
- Rules and tuning live in config assets (`DATA_ARCHITECTURE.md`), not code.
- The core loop is one clear pipeline; new mechanics plug in, they do not tangle.
- Prefer systemic interactions over scripted content (VISION).

## To define with the concept
Core-loop systems, the interaction model (`OBJECT_INTERACTION.md`), win/lose evaluation, and any role or detection systems (`DETECTION_SYSTEM.md`).

_Status: awaiting the concept._
