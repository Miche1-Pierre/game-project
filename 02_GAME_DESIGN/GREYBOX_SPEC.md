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
2. Stance: Shift sprints (x1.6), Ctrl crouches (capsule 1.80 m to 1.00 m, eye 1.60 m to 0.80 m, speed x0.45). Both are hold, not toggle. Crouch beats sprint, no jumping while crouched, and you cannot stand up under something. No stamina.
3. Physics grab: look + LMB to grab, carry as an unwieldy rigidbody (the object fights you), RMB to throw, LMB again to drop.
4. Reach: the scroll wheel pushes the held object out or pulls it in, 1.0 m to 3.2 m, about 0.35 m per notch. Heavy objects cannot go as far out (the sofa stops at 2.8 m, the fridge at 2.4 m). The chosen reach is kept between grabs.
5. Carried rotation: hold R and the mouse turns the object instead of your head (mouse to turn, scroll to roll). The object keeps the orientation you gave it as you walk, and heavy things turn slowly. No 90 degree snap key: the obvious pair, Q and E, is unusable because E is DELIVER.
6. `MovableObject` data: weight (sets mass and slows you), contractValue, requiredForContract, fragile + breakThreshold (fragile marks "broken" on a hard impact, no fragmentation yet).
7. Truck cargo trigger: objects inside the truck bed count as loaded.
8. Contract: required checklist, money, timer; press E when all required are loaded to DELIVER and get paid (broken items pay less).
9. HUD (OnGUI, ugly on purpose): checklist, money, timer, controls.
10. Cigarette. An object, lying on the ground by the truck when the job starts. You pick it up, carry it and drop it like a chair. Holding it, the right button is modal: a **tap throws it away**, **holding it smokes**. Every half second of smoking leaves a puff at the tip, each puff lasts exactly **7 seconds** and takes the view of anyone standing in it, you included. The smoke comes off the cigarette, so you can hold it out to fog a doorway or pull it in and blind only yourself. Throw it and a fresh one turns up at the van; the old one stays where it landed. See `../decisions/ADR-005-cigarette-smoke-screen.md` for what the smoke does and `../decisions/ADR-007-starting-items-are-objects.md` for why it is an object. No art, no inventory: both textures are generated in code and the item is two primitives.
11. Beer. The other object by the truck, same rules. Hold **F** while carrying it and you drink: four seconds empties the bottle for good and leaves you as drunk as this game gets. Drunk takes your aim (the view wanders, the horizon tips), your heading (you no longer walk where you point) and your grip (what you carry lags and swings), for about **25 seconds**. It never takes your speed and never takes the controls. Throw the bottle and it **breaks**, full or empty, leaving one flat mark on the floor; a new one turns up at the van. Drunkenness is a state on the player, not a property of the bottle, so the next thing that should wreck the crew feeds the same meter. `../decisions/ADR-006-beer-makes-you-drunk.md`.

## The first emergent problem (no special code)
The sofa is wider than the narrow interior door. The player must rotate it or find another route. That single geometry fact already forces thinking. A later layer: an object that only fits through a window.

Rotating it is possible as of 2026-09-17 (system 5). Before that the problem had one answer, walk around, which is a wall rather than a problem. Whether the turn is satisfying or fiddly is a playtest question, not a settled one.

## Out of scope (do NOT build yet)
Theft / extra-value scoring, NPCs / owner, cameras / alarms, fire / consequences, destruction / fragmentation, progression / upgrades, multiple maps, real assets, menus, save, and networking. All deferred until the core feel is validated (risk R2).

## Success criteria (go / no-go)
After ~30-60 minutes: does moving the furniture already create little stories and problem-solving ("how do we get the sofa out"), and is there an urge to do it faster or better? If yes, add co-op next. If the carry / load feel is bad, fix the grab before anything else.

## How to run
Unity editor (6000.6.0f1): menu **The Movers > Create Greybox Scene**, then press Play. Or: create an empty scene, add an empty GameObject, add the `GreyboxBootstrap` component, press Play.
Controls: WASD move, Shift sprint, Ctrl crouch, Space jump, mouse look, LMB grab/drop, RMB throw, E deliver (when all loaded), Esc frees the cursor.
While carrying: scroll to push the object out or pull it in. Hold R and the mouse turns it instead of your head, scroll rolls it. Look is suspended while R is held, so the mouse belongs to the object. One wheel, two jobs, split by whether R is down.
The cigarette and the beer are on the ground by the truck: grab them like anything else. Holding one, hold RMB to smoke it or F to drink it, and tap RMB to throw it away. On anything else RMB still throws the instant you press it.

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
- **Starting inventory,** deliberately lean and absurd: a beer and a cigarette, usable at any time, useful for nothing. **Both of them are useful for something** as of 2026-09-17 (ADR-005, ADR-006), and as of 2026-09-20 neither of them is an inventory at all (ADR-007): they are two objects on the ground by the truck, picked up with the same grab as a chair, and thrown away when you are done. The sentence is spent: there is no third item, and nothing here is a slot.
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
