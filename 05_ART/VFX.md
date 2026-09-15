# VFX

_Communicate gameplay events. Simple colors, lightweight._

## Approach
- VFX exist to signal events (impact, break, alert, success/failure), not to decorate.
- Simple particle shapes, simple colors, readable at a glance and under chaos.
- Cheap to produce and cheap to render (bounded particle counts, `../03_TECHNICAL/PERFORMANCE.md`).
- Fire VFX off gameplay events (`../03_TECHNICAL/EVENT_SYSTEM.md`).
