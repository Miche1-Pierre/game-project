# GREYBOX SPEC, Tutorial_01 (The Movers)

_The smallest test that answers one brutal question: is physically grabbing, carrying and loading furniture into a truck to fill a contract fun? Primitives only, no art, no theft, no NPC, no networking. Concept: `GAME_CONCEPT.md`, `../decisions/ADR-003-concept-the-movers.md`._

## The one question
Can a player pick up furniture and boxes in a house and load them into a truck to complete a contract, and is that already fun and problem-generating? (Co-op is the very next step.)

## Scope (Tutorial_01)
- One small house: two rooms split by a wall with a NARROW interior door, a wide front opening to a garden, and a truck parked outside.
- ~12 primitive objects, ~10 required by the contract, 2 non-required but valuable (a TV, a vase) as a silent tease of the later "extra value" layer.
- One player (first person), single machine.

## Systems (minimal)
1. Player movement (WASD + mouse look), CharacterController.
2. Physics grab: look + LMB to grab, carry as an unwieldy rigidbody (the object fights you), RMB to throw, LMB again to drop.
3. `MovableObject` data: weight (sets mass and slows you), contractValue, requiredForContract, fragile + breakThreshold (fragile marks "broken" on a hard impact, no fragmentation yet).
4. Truck cargo trigger: objects inside the truck bed count as loaded.
5. Contract: required checklist, money, timer; press E when all required are loaded to DELIVER and get paid (broken items pay less).
6. HUD (OnGUI, ugly on purpose): checklist, money, timer, controls.

## The first emergent problem (no special code)
The sofa is wider than the narrow interior door. The player must rotate it or find another route. That single geometry fact already forces thinking. A later layer: an object that only fits through a window.

## Out of scope (do NOT build yet)
Theft / extra-value scoring, NPCs / owner, cameras / alarms, fire / consequences, destruction / fragmentation, progression / upgrades, multiple maps, real assets, menus, save, and networking. All deferred until the core feel is validated (risk R2).

## Success criteria (go / no-go)
After ~30-60 minutes: does moving the furniture already create little stories and problem-solving ("how do we get the sofa out"), and is there an urge to do it faster or better? If yes, add co-op next. If the carry / load feel is bad, fix the grab before anything else.

## How to run
Unity editor (6000.6.0f1): menu **The Movers > Create Greybox Scene**, then press Play. Or: create an empty scene, add an empty GameObject, add the `GreyboxBootstrap` component, press Play.
Controls: WASD move, mouse look, LMB grab/drop, RMB throw, E deliver (when all loaded), Esc frees the cursor.
