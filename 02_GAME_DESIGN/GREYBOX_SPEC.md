# GREYBOX SPEC, Tutorial_01 (The Movers)

_The smallest test that answers one brutal question: is physically grabbing, carrying and loading furniture into a truck to fill a contract fun? Primitives only, no art, no theft, no NPC, no networking. Concept: `GAME_CONCEPT.md`, `../decisions/ADR-003-concept-the-movers.md`._

## The one question
Can a player pick up furniture and boxes in a house and load them into a truck to complete a contract, and is that already fun and problem-generating? (Co-op is the very next step.)

## Scope (Tutorial_01)
- One small house: two rooms split by a wall with a NARROW interior door, a wide front opening to a garden, and a truck parked outside.
- ~12 primitive objects, ~10 required by the contract, 2 non-required but valuable (a TV, a vase) as a silent tease of the later "extra value" layer.
- One player (first person), single machine.

## Systems (minimal)
1. Player movement (WASD + mouse look + Space to jump), CharacterController. The jump is sized to climb into the truck bed and step over a dropped crate, and it flattens with the weight you carry.
2. Physics grab: look + LMB to grab, carry as an unwieldy rigidbody (the object fights you), RMB to throw, LMB again to drop.
3. Carried rotation: hold R and the mouse turns the object instead of your head (mouse to turn, scroll to roll). The object keeps the orientation you gave it as you walk, and heavy things turn slowly. No 90 degree snap key: the obvious pair, Q and E, is unusable because E is DELIVER.
4. `MovableObject` data: weight (sets mass and slows you), contractValue, requiredForContract, fragile + breakThreshold (fragile marks "broken" on a hard impact, no fragmentation yet).
5. Truck cargo trigger: objects inside the truck bed count as loaded.
6. Contract: required checklist, money, timer; press E when all required are loaded to DELIVER and get paid (broken items pay less).
7. HUD (OnGUI, ugly on purpose): checklist, money, timer, controls.

## The first emergent problem (no special code)
The sofa is wider than the narrow interior door. The player must rotate it or find another route. That single geometry fact already forces thinking. A later layer: an object that only fits through a window.

Rotating it is possible as of 2026-09-17 (system 3). Before that the problem had one answer, walk around, which is a wall rather than a problem. Whether the turn is satisfying or fiddly is a playtest question, not a settled one.

## Out of scope (do NOT build yet)
Theft / extra-value scoring, NPCs / owner, cameras / alarms, fire / consequences, destruction / fragmentation, progression / upgrades, multiple maps, real assets, menus, save, and networking. All deferred until the core feel is validated (risk R2).

## Success criteria (go / no-go)
After ~30-60 minutes: does moving the furniture already create little stories and problem-solving ("how do we get the sofa out"), and is there an urge to do it faster or better? If yes, add co-op next. If the carry / load feel is bad, fix the grab before anything else.

## How to run
Unity editor (6000.6.0f1): menu **The Movers > Create Greybox Scene**, then press Play. Or: create an empty scene, add an empty GameObject, add the `GreyboxBootstrap` component, press Play.
Controls: WASD move, Space jump, mouse look, LMB grab/drop, RMB throw, E deliver (when all loaded), Esc frees the cursor.
To turn what you carry: hold R, then move the mouse to turn it or scroll to roll it. Look is suspended while R is held, so the mouse belongs to the object.

---

## Next scope, pending the verb question
The concept meeting of 2026-09-16 specified a **larger greybox** than Tutorial_01, built on the "steal under watch" reading of the concept. It is recorded here because the decisions are real, and held here because `GAME_CONCEPT.md` carries an unresolved divergence on the core verb.

Do not build this until that divergence is settled.

- **Map:** the grandmother's house, 10 rooms. Entrance, kitchen, living room, bedroom, attic, garage, cellar, garden, bathroom, hallway. Handcrafted, no procedural generation.
- **Garage over barn.** Delegated to the agent at the meeting and decided: it is attached, so no second building shell and no long outdoor traversal; it faces the truck, so it is the natural loading path; it justifies bulky heavy objects that exercise the physics hardest; and its door is a large openable or breakable surface, which gives a second entry route with no extra art.
- **About 80 inert manipulable objects**, varying in weight, fragility and value.
- **2 living objects.** The cat screams when picked up and is audible through walls, which makes it a portable alarm. The fish dies about 40 seconds out of water, firing a notification and a money loss.
- **One NPC, the grandmother.** A single scripted interaction: she hands over the keys and a tutorial box opens. She states one house rule out loud, for example asking you to be careful with her cat, to plant the idea without enforcing it in code. Movement is **A\* pathfinding on a fixed patrol**, which is a standard algorithm and not AI. She walks, she does not evaluate.
- **Two entries.** Take the keys, or break a window. Breaking the window works and costs money. That is the first choice the game asks.
- **Theft ledger.** Any non-contract object reaching the truck pays extra. Detection stays hard-coded conditions only.
- **Starting inventory,** deliberately lean and absurd: a beer and a cigarette, usable at any time, useful for nothing.
- **Truck:** a static hollow box, not drivable. The real vehicle comes in V1.
- **Multiplayer:** 4 players maximum, host is a player, no backend.

**Still out of scope in that larger version:** the shop, procedural or modular generation, a drivable truck, the vigilance meter and any behaviour AI for detection (deferred to V2), progression between maps, a second map, final art, audio design, menus.

### Suggested build order for that version
A sequencing proposal so the funny test arrives early rather than last, not a decision.

1. One room, five objects, one player. Pick up, carry, drop, break, value lost. This is what Tutorial_01 already does.
2. Add the cat. One asset, screams on pickup. The cheapest laugh in the build.
3. Add a second player. Handing objects over, getting in each other's way.
4. Add the grandmother walking her patrol. No detection yet, just a body in the corridor to route around.
5. Add the theft ledger and the settlement.
6. Add the fish, the timer and the window entry.
7. Fill out the remaining rooms and objects.

Steps 1 to 4 already answer the one question. Steps 5 to 7 make it a run.

### Go / no-go criteria from the meeting
In priority order, and compatible with the Tutorial_01 criteria above.

1. **Spontaneous laughter. Mandatory.** If nobody laughs, the concept goes back to design and we do not build assets.
2. **Wanting to try something.** A player says they would like to test a thing, or wants to see how the owner reacts.
3. **Being pulled in by the money.** Wanting the next map, wanting a better run.

### Assets
Free packs as the base, completed by AI 3D generation on existing subscriptions. Money is spent only if the greybox validates. Generated assets that ship must be declared on the Steam page. See `../05_ART/ASSET_STATUS.md`.

### Multiplayer validation order
Each developer local first, then two clients on one machine using the school VMs, then Steam networking last and not during the greybox. No paid networking. See `../decisions/ADR-004-netcode-free-only.md`.
