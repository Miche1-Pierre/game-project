# INPUT SYSTEM

_Unity's Input System package, set up once, mapped later._

## Approach
- Use the **Input System** package (not legacy Input), with an Input Actions asset.
- Action maps: Gameplay, UI. Rebindable later.
- Keyboard + mouse first; controller support is a later, low-cost add (`../11_TESTING/COMPATIBILITY.md`).
- Read input through an abstraction so systems do not poll raw devices.

_Concrete actions are defined with the concept and the greybox._

**Amended 2026-09-26 (ADR-009):** the slice stays on the legacy Input Manager, behind a per-player `CrewInput` (keyboard and mouse source, gamepad source on per-pad axes `J1_*`/`J2_*`). No gameplay script reads `UnityEngine.Input`. Moving to the Input System package later means writing new input sources only.
