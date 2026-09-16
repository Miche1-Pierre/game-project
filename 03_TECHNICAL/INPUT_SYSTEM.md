# INPUT SYSTEM

_Unity's Input System package, set up once, mapped later._

## Approach
- Use the **Input System** package (not legacy Input), with an Input Actions asset.
- Action maps: Gameplay, UI. Rebindable later.
- Keyboard + mouse first; controller support is a later, low-cost add (`../11_TESTING/COMPATIBILITY.md`).
- Read input through an abstraction so systems do not poll raw devices.

_Concrete actions are defined with the concept and the greybox._
