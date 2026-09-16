# OBJECT INTERACTION

_How players interact with the world. Likely a core system; refined with the concept._

## Approach (set now)
- A single **interaction interface** (`IInteractable`) implemented by objects (pick up, use, toggle, carry).
- The player has one interaction probe (raycast or trigger) that finds the best interactable and shows a prompt.
- Interaction outcomes fire events (`EVENT_SYSTEM.md`) so audio, VFX and net react uniformly.
- Physics-based carrying / handling if the concept wants R.E.P.O.-style friction (`PHYSICS.md`).

## Rule
One clean interaction model reused everywhere beats bespoke code per object (systems over content).

_Status: core shape set, specifics await the concept._
