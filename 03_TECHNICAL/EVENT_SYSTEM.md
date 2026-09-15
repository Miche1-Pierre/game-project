# EVENT SYSTEM

_Decoupling backbone. Systems talk through events, not hard references._

## Approach
- A lightweight typed event bus (C# events or ScriptableObject-based events) for gameplay signals (object_grabbed, alarm_raised, round_ended).
- Publishers do not know subscribers. UI, audio and VFX subscribe to gameplay events.
- Keep events coarse and meaningful; do not turn everything into an event.

## Rule
If two systems need each other's internals, prefer an event over a direct reference. Keeps systems testable and swappable (`CSHARP_ARCHITECTURE.md`).
