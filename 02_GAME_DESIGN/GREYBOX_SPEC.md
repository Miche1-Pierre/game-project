# GREYBOX SPEC

_Scope frozen 2026-09-16 at the concept meeting. This document is the gate named in `decisions/ADR-002-methodology-spec-first.md`: once it exists, the Unity project may be created._

## The one question
**Is it funny?** Nothing in this build exists for any other reason. Every item below either produces a laugh or gets cut.

## Map: the grandmother's house
One handcrafted map. No procedural generation.

### Rooms (10)
Entrance, kitchen, living room, bedroom, attic, **garage**, cellar, garden, bathroom, main hallway.

**Delegated decision, garage over barn.** The meeting left this choice to the agent. Garage, for four reasons: it is attached, so it needs no second building shell and no long outdoor traversal that would eat the session; it faces the truck, which makes it the natural loading path; it justifies bulky, heavy, awkward objects that exercise the physics hardest; and its door is a large openable or breakable surface, which gives a second entry route without extra art. Revisit if the outdoor space turns out to be where the comedy lives.

### Objects
- **About 80 inert manipulable objects.** Weight, fragility and value vary. They are the game.
- **2 living objects.** The cat and the fish.
  - Cat: screams when picked up. Audible through walls. It is a portable alarm.
  - Fish: dies about 40 seconds out of water. On death, a notification fires and money is lost.
- No other creatures.

### The owner (one NPC)
The grandmother. **One scripted interaction only:** press to interact, she hands over the keys, a tutorial box opens naming the key, and that is the end of the dialogue system.

She also states one house rule out loud at handover, for example asking you to be careful with her cat. The rule exists to plant the idea, not to be enforced by code.

Movement is **A\* pathfinding on a fixed patrol**. This is a standard algorithm, not AI, and the code is a paste-in. She walks. She does not evaluate.

### Entering
Two routes. Take the keys from her, or break a window. Breaking the window works and **costs money**. That is the first choice the game asks.

## Systems in scope
| System | Greybox implementation |
|---|---|
| Object handling | Pick up, carry with real weight, drop, break |
| Value and damage | Each object has a value, damage reduces it |
| Contract | A fixed list of objects to bring to the truck |
| Theft | Any non-contract object carried to the truck pays extra |
| Detection | Hard-coded conditions only, minimal, just enough to prove the idea reads |
| Money | One settlement screen at the end. Contract, minus damage, minus penalties, plus theft |
| Timer | A recommended duration, with a visible degradation past it |
| Starting inventory | Deliberately lean and absurd. A beer and a cigarette, usable at any time, useful for nothing |
| Truck | A static hollow box. It does not move |
| Multiplayer | 4 players maximum, host is a player, no backend |

## Explicitly out of scope
Cut at the meeting, do not build:
- Procedural or modular generation.
- The shop.
- A drivable truck.
- The vigilance meter and any behaviour AI for detection. Deferred to V2, conditional on V1 working.
- Progression between maps.
- Any second map.
- Final art, audio design, menus.

## Multiplayer plan
1. Each developer tests alone locally.
2. Two clients on one machine, using the school VMs.
3. Steam networking last, and not during the greybox.

No paid networking. Free only, through Unity or Steam. See `decisions/ADR-003-netcode-free-only.md`.

## Suggested build order
Not a decision, a sequencing proposal so the funny test arrives early rather than last.

1. **One room, five objects, one player.** Pick up, carry, drop, break, value lost. If carrying a lamp badly is not already slightly funny, stop here.
2. **Add the cat.** One asset, screams on pickup. This is the cheapest laugh in the build.
3. **Add a second player.** Handing objects over, getting in each other's way.
4. **Add the grandmother walking her patrol.** No detection yet, just a body in the corridor that must be routed around.
5. **Add the truck, the contract and the settlement.** Now the run has a beginning and an end.
6. **Add the fish, the timer and the window entry.**
7. **Fill out the remaining rooms and objects.**

Steps 1 to 4 already answer the one question. Steps 5 to 7 make it a run.

## Go / no-go criteria
Decided at the meeting, in priority order.

1. **Spontaneous laughter. Mandatory.** If nobody laughs, the concept goes back to design and we do not build assets.
2. **Wanting to try something.** A player says they would like to test a thing, or wants to see how the owner reacts to it.
3. **Being pulled in by the money.** Wanting the next map, wanting a better run.

Tension and an immediate urge to replay, per `00_PROJECT/SUCCESS_CRITERIA.md`.

## Assets
Free packs as the base, completed by AI 3D generation on existing subscriptions. Money is spent only if the greybox validates and we can picture the final look. See `05_ART/ASSET_STATUS.md`.

**Consequence to record now:** generated assets that ship must be declared on the Steam page (`00_PROJECT/OPEN_QUESTIONS.md` Q23).

## What happens after
Per the meeting plan: greybox, then V1 the following week (first playable map plus truck plus shop), then a month of communication and wishlist work while the next maps are built, then the beta. See `04_PRODUCTION/MILESTONES.md`.

_Status: frozen. This unlocks Unity creation._
