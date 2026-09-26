# ADR-009: The grandmother's house becomes a systemic vertical slice

## Status
**Accepted (2026-09-25), Pierre's decision.** To be read by the other developer (Spykernv): it settles the verb question in code, which PROJECT_STATE named as the next decision.

It amends:
- `02_GAME_DESIGN/GREYBOX_SPEC.md`: the "Next scope, pending the verb question" becomes the current scope for `Map01_PierreKit_House`. Its "still out of scope" list loses the drivable truck, the vigilance meter and the grandmother's behaviour.
- `03_TECHNICAL/INPUT_SYSTEM.md`: the legacy Input Manager stays for now, behind a per-player abstraction.
- `04_PRODUCTION/REJECTED.md`: roofs may now fall. Floors and stairs still never break.
- [ADR-008](ADR-008-everything-breaks.md): destruction becomes structural instead of one health bar per wall.

## Context
Pierre, 2026-09-25, after playing the house (translated):
- **The goal:** "we can steal the more valuable things, that is the goal of the game; if the grandmother notices, it will go badly".
- **The owner:** the grandmother is "fully autonomous in the house". She can drink, light the fireplace, sit down and take a book. She has an anger and patience bar that moves when things break, when she is annoyed, when there is too much noise or when the crew is too slow. "A* could be relevant."
- **The crew:** "for now it is 2 players".
- **Destruction:** "realistic enough". The grenades are "what I will do my first fracture test with".
- **Pockets:** pockets with valuables to steal, and "put in place as many systems as possible".

With it came a written brief that he endorsed: a vertical slice of the systems that define the game. It covers game states, contract objectives, generic interactions, physical object properties, structural destruction, doors with locks and keys, an autonomous NPC, an event bus, a drivable truck, audio and VFX feedback, and debug tools.

The concept meeting of 2026-09-16, with both developers present, had already recorded the verb as STEAL UNDER WATCH. Pierre's sentence confirms it.

## Decision
1. **The verb is steal under watch.** The moving contract is the legitimate job and the cover. The extra money is in what the crew takes that is not on the list, while the owner lives in the house. MOVE / CARRY stays the body of the game, not its point.
2. **Two local players.** Split screen on one machine: P1 on keyboard and mouse, P2 on a gamepad. A debug key moves the keyboard to the other player for solo testing. No netcode yet (CLAUDE.md section 12).
3. **Input stays on the legacy Input Manager, behind `CrewInput`.** Every gameplay script asks its player's `CrewInput`, never `UnityEngine.Input`. Moving to the Input System package later means writing new input sources only.
4. **The grandmother is an autonomous NPC.**
   - **Movement:** Unity's built-in NavMesh, built at Play from the physics colliders, with no package. NavMesh path search is A* over polygons, which is the spec's "A* pathfinding".
   - **Behaviour:** a small state machine: intro and keys, routine, activities, observe, react, confront.
   - **Senses:** a vision cone and hearing driven by world events.
   - **Patience:** a meter from 100 to 0, moved by events through a data table. At 0 she calls the police and the run fails.
5. **Theft.** Anything of hers that is not on the contract and leaves in the truck, or in a pocket, is stolen and pays its value. If she saw it happen, it is confiscated and fined instead. The numbers are first guesses in data.
6. **Structural destruction.**
   - **Pre-fractured walls:** walls are cut into chunks in Blender, 8 to 15 per module, and swapped in the first time a wall is hurt.
   - **Local damage:** damage lands per chunk, by distance, cover and material.
   - **Support graph:** unsupported chunks fall, so a second grenade can bring down what the first one weakened.
   - **What never breaks:** the foundation (cellar walls, plinths, ground slab) stops at Damaged. Floors and stairs never break.
   - **Roofs:** they don't break, but they fall as whole sections when nothing holds them up any more.
   - **Glass:** it cracks before it breaks.
7. **The truck can be driven.** It is arcade handling, and the cargo stays physical in the box: no load button, no inventory.
8. **One event bus** (`WorldEvents`) carries the facts that several systems care about. Tuning lives in data, not in code.
9. **Debug tools are part of the slice:** grenade, explosion, damage, reset, overlays for health, material, state and support, and the grandmother's mind.

The architecture, file boundaries and contracts are in `03_TECHNICAL/SLICE_ARCHITECTURE.md`.

## Why
- **The go / no-go needs two people.** Spontaneous laughter cannot be tested solo, and PROJECT_STATE's first action has been "play it with a second person" since the first greybox. Two local players make that possible tonight, not after netcode.
- **Steal under watch needs a watcher.** A static grandmother is decor. A grandmother who hears the vase break, walks in, and catches P2 with her watch in his pocket is the situation the whole concept promises, and a spectator reads it in under 10 seconds (H1).
- **Structural destruction makes damage readable.** Today a grenade either does nothing visible or deletes a whole 3 m wall. A hole where the grenade was, and the wall above it sagging after the second one, reads at a glance.
- **Built-in NavMesh over a hand-written A*.** The investigation built one at Play in 81 to 234 ms from the colliders. All 28 named places in the house and garden were reachable, and a 242 m patrol completed. It stays correct when Pierre edits the house or a wall comes down, and it needs no package.

## Consequences
- **Scope grows a lot, openly.** The spec's larger scope is now being built on this map. Tutorial_01 still asks only the carry question and keeps working.
- **The feel of the carry must not change.** It is the only validated result. The keyboard source reproduces today's numbers exactly.
- **Split screen doubles rendering.** Profile before optimising (CLAUDE.md section 11).
- **Every number is a hypothesis.** This covers patience costs, theft fines, wall health and chunk counts. They sit in one table per system so a playtest note maps to one edit.
- **The verb still has to be proven by playing.** If two people play this and nobody laughs, the answer is about the concept, not about missing systems.

## Revisit if
- **The two-player session** shows the crew ignoring the grandmother, or ignoring the contract.
- **Theft is never attempted** because the risk is unreadable. Then detection needs clearer signals, not more rules.
- **Destruction dominates** every run by minute three.
- **Split screen is unplayable** on the team's machines. The fallback is two instances over the network, which is ADR-004's territory.
