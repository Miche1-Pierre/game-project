# SLICE ARCHITECTURE

_How the systems of the vertical slice (ADR-009) fit together in `Map01_PierreKit_House`. Boundaries first, classes second (CLAUDE.md section 7). Written 2026-09-25, before the code._

## 1. Principles
- **Clean architecture, prototype scope.** Clean, modular and extensible code, without over-engineering: no generic framework, no interface without a second implementation in sight, no class per object.
- **Pipeline:** walk, interact, manipulate, destroy, react, load, deliver. Every system serves one step of it.
- **Composition:** small components, plain C# where no engine glue is needed, namespace `Movers`, code under `Assets/_Movers/Scripts`.
- **Events for facts, references for ownership.** A system that owns something calls it directly. A system that only needs to know something happened listens to `WorldEvents`.
- **Tuning in data.** A tuning class per system with code defaults. The integrator creates a ScriptableObject asset under `Assets/_Movers/Data/` when a system asks for one, and a missing asset falls back to the defaults.
- **Zero console errors** is part of done. Every system survives a scene reload (the debug reset).
- **The carry feel is frozen:** the keyboard numbers must not change (playtest 001).

## 2. System map

| System | Folder | Owns | Talks to others through |
|---|---|---|---|
| Core contracts | `Core/`, `UI/ViewportGUI.cs`, `Debug/SliceDebug.cs`, `Physics/MovableObject.cs` | events, actors, damage event, per-player input, crew roster, session state, debug keys, HUD regions, object profile | everyone reads them |
| Player | `Player/`, `Interaction/PlayerInteract.cs`, `Items/` (except grenades), `Effects/` (except CameraShake) | movement, look, carry, push and drag, throw, pockets, equip, smoke, drink, split screen, second player, crew animation driver, gamepad | CrewInput, CrewRoster, WorldEvents |
| Interaction | `Interaction/` (except PlayerInteract) | doors, windows (sashes), hinges, locks, keys | Interactable, WorldEvents |
| Destruction | `Destruction/`, `Items/GrenadeItem.cs`, `Items/GrenadeCrate.cs`, `Effects/CameraShake.cs` | damage events, material table, props, glass, pre-fractured walls, support graph, collapse, explosions, debris, impact audio, destruction debug | IDamageable, WorldEvents |
| Game loop | `Gameplay/`, `Contracts/`, `UI/` (except ViewportGUI), `Debug/` (except SliceDebug) | session states, contract objectives, delivery, settlement, theft ledger, HUD, end screen, event log | Session, WorldEvents |
| Grandmother | `NPC/` | navigation, brain, senses, patience, activities, intro and keys, reactions, voice, speech bubbles, patience HUD | WorldEvents, CrewRoster, HingedPanel.SetOpen |
| Truck | `Vehicles/` | driving, seat, cargo zone, capacity and weight | CrewInput, WorldEvents |
| Kit (Blender) | `tools/blender/export_pierrekit.py` | normals fix, window sashes, kit FBX | files |
| Fracture (Blender) | `tools/blender/fracture_modules.py` | chunk sets and their sidecar JSON | files |
| Animation (Blender) | `tools/blender/author_clips.py`, `model_grandma.py` | clips for the crew and the grandmother | files |

## 3. File ownership
**One owner per file.** No other agent edits a file it does not own. If you need a change in someone else's file, write a request in your report. The contracts in section 4 are frozen: build against them, don't change them.

| Owner | Files (paths under `Assets/_Movers/Scripts/` unless stated) |
|---|---|
| CONTRACTS | `Core/Actors.cs`, `Core/WorldEvents.cs`, `Core/DamageEvent.cs`, `Core/CrewInput.cs`, `Core/CrewRoster.cs`, `Core/CrewMember.cs` (its own file: Unity only restores a scene component whose file carries its name), `Core/SessionState.cs`, `Core/DebugCommands.cs`, `Debug/SliceDebug.cs`, `UI/ViewportGUI.cs`, `Physics/MovableObject.cs`, `Destruction/BreakMaterial.cs` |
| PLAYER | `Player/*`, `Interaction/PlayerInteract.cs`, `Items/PlayerPockets.cs`, `Items/PlayerEquip.cs`, `Items/CrewEquip.cs`, `Items/EquipItem.cs`, `Items/HeldUsable.cs`, `Items/CigaretteItem.cs`, `Items/BeerItem.cs`, `Items/StartingItemSpawner.cs`, `Items/ItemArt.cs`, `Effects/Drunkenness.cs`, `Effects/SmokeVision.cs`, `Effects/SmokeCloud.cs`, `Effects/SmokeTextures.cs`, `Effects/MirrorSurface.cs`, `Core/InputClaims.cs` (to delete), `Core/GamepadSource.cs` (new), `Greybox/GreyboxBootstrap.cs`, `Assets/_Movers/Editor/*` (the CLIs that drive players), `ProjectSettings/InputManager.asset` (staged) |
| INTERACTION | `Interaction/Interactable.cs`, `HingedPanel.cs`, `HingedGroup.cs`, `HingeCollisionRelay.cs`, `HouseInteractionSetup.cs`, new `DoorLock.cs`, `KeyItem.cs` |
| DESTRUCTION | `Destruction/*` except `BreakMaterial.cs`, `Items/GrenadeItem.cs`, `Items/GrenadeCrate.cs`, `Effects/CameraShake.cs` |
| GAMELOOP | new `Gameplay/*`, `Contracts/*`, `UI/GameHUD.cs` and new `UI/*` except ViewportGUI, `Debug/MoversDebugTools.cs`, new `Debug/*` except SliceDebug |
| GRANDMA | new `NPC/*` |
| TRUCK | `Vehicles/*` |

**Online co-op (ADR-012):** the `Net/` layer (`Scripts/Net/`, `Net/Sync/`, `Net/Test/`), the online gates added to the files above, and the track ownership used to build them are specified in `07_MULTIPLAYER/NETCODE_SLICE.md` (contracts in section 12, file ownership in section 13).

## 4. Frozen contracts
Written first, compiled against the current code, source of truth in the files themselves.

- **`Actors`:**
  - players are 0..3;
  - `Actors.World = -1`, `Actors.Grandma = 100`.
- **`WorldEvents` / `WorldEvent` / `WorldEventType`:** one struct, one bus.
  - `Raise(type, position, instigator, loudness, magnitude, value, subject)`.
  - `Subscribe` and `Unsubscribe` in `OnEnable` / `OnDisable`.
  - `GetRecent(i)` returns the last 64 events.
  - `HearingRadius(loudness) = loudness x 30 m`.
- **`DamageEvent`, `DamageType`, `DestructionState`, `DamageResult`, `IDamageable`.**
- **`CrewInput` (order -500):**
  - reading: `Move`, `LookDelta`, `Scroll`, `RollDelta`, `Held` / `Down` / `Up(CrewButton)`;
  - `TryConsume(button)`: first come, per player, per frame;
  - control: `Muted`, `SetSource(ICrewInputSource)`;
  - sources: `KeyboardMouseSource`, `NullInputSource`. PLAYER adds `GamepadSource` and a scripted source.
- **`CrewMember` / `CrewRoster`:**
  - `CrewMember`: `index`, `color`, `Input`, `Controller`, `Grab`, `Interact`, `Pockets`, `View`, `IsDriving`, `Held`, `EyePosition`;
  - `CrewRoster`: `All`, `Get(i)`, `Owner(transform)`, `Nearest(p)`, `Joined` / `Left`.
- **`Session`** (read-only outside GameSession): `State`, `Failure`, `TimeLeft`, `TimeLimit`, `IsRunning`, `IsOver`. A scene without a GameSession reads as InProgress.
- **`DebugCommands.Register(key, shift, label, action, owner)`**, `Unregister(owner)` and `Toast(text)`. SliceDebug is the only reader of F-keys, and F12 lists the registered commands.
- **`ViewportGUI`:**
  - `RectFor(camera or member)`, `Region(view, HudRegion, w, h)`, `WorldToGUI`;
  - `Label`, `Bar`, `Panel`, `FontSize`.
- **`MovableObject` adds:**
  - profile flags: `canCarry`, `canPush`, `canThrow`, `canBeLoaded`, `pocketable`, `ownedByGrandma`;
  - runtime fields: `holder`, `lastHandler`, `lastHandledTime`, `lastThrownBy`, `lastThrownTime`, `inPocket`, `worn`;
  - derived values: `Mass`, `Volume`, `CanBreak`, `Material`, `Durability`, `CanOpen`, `IsTheftTarget`, `RecentHandler(s)`.
- **`BreakMaterial` adds `Brick` and `Concrete`,** at the end.

**Existing APIs that other systems call:** their signatures stay. Adding optional parameters or overloads is fine.
- **Doors and interaction:**
  - `HingedPanel`: `SetOpen(bool)` (lock-agnostic: the grandmother has keys), `Toggle()`, `IsOpen`, `IsMoving`, `CanSwing`, `IsWrecked`, `TryGetClosedBounds(...)`, `Group`;
  - `HingedGroup`: `SetOpen(bool)`, `AnyOpen`, `panels`;
  - `Interactable`: `Prompt`, `CanInteract`, `Covers`, `Interact(PlayerInteract by)`.
- **Player:**
  - `PlayerGrab`: `Held`, `IsCarrying`, `Hold(MovableObject)`, `Release(bool thrown)`;
  - `PlayerController`: `cam`, `AddImpulse(Vector3)`, `crouching`.
- **Destruction:**
  - `Breakable`: `material`, `Health`, `MaxHealth`, `IsDestroyed`, `ApplyDamage(float, Vector3, Vector3)`, `Shatter`, `event Destroyed`, `static ReviveAll()`;
  - `GlassPane`: `IsBroken`, `ApplyDamage`, `Shatter`, `event Broken`;
  - `Explosion.Detonate(Vector3, float radius = 6.5f, float power = 1f)` and `event Detonated` (an optional trailing `int instigator` may be added);
  - `ImpactAudio.Play(Kind, Vector3, float)` (an overload with an instigator may be added);
  - `GrenadeItem.Create(Vector3)`.
- **Truck:** `TruckCargo.inside`.

## 5. World events: who raises what

| Event | Raised by | Main listeners |
|---|---|---|
| `LoudNoise` | DESTRUCTION in `ImpactAudio.Play` (every audible sound), with loudness from kind and volume | GRANDMA |
| `Explosion`, `PlayerKnockedDown` | DESTRUCTION (`Explosion`) | GRANDMA, GAMELOOP log |
| `ObjectDamaged`, `ObjectDestroyed`, `ContractObjectDamaged`, `ContractObjectDestroyed`, `StructureDamaged`, `StructureCollapsed`, `WindowBroken`, `DoorBroken` | DESTRUCTION | GRANDMA, GAMELOOP |
| `ObjectPickedUp`, `ObjectReleased`, `ObjectThrown`, `FurnitureMoved`, `ItemPocketed`, `ItemUnpocketed`, `PlayerSmoking`, `PlayerDrinking` | PLAYER | GRANDMA, GAMELOOP |
| `DoorOpened`, `DoorClosed`, `DoorLockedRattle`, `DoorUnlocked`, `WindowOpened`, `WindowClosed` | INTERACTION | GRANDMA, GAMELOOP log |
| `CargoLoaded`, `CargoUnloaded` | TRUCK (`TruckCargo`) | GAMELOOP, GRANDMA |
| `KeysHandedOver`, `TheftWitnessed`, `GrandmaNoticed`, `GrandmaMoodChanged`, `GrandmaCalledPolice`, `GrandmaBumped` | GRANDMA | GAMELOOP, INTERACTION (keys unlock the doors) |
| `ItemStolen`, `SessionStateChanged`, `ContractDelivered` | GAMELOOP | GRANDMA, HUD |

The instigator is always filled when known. For breakage caused by a thrown or carried object, use `MovableObject.RecentHandler(2f)`.

## 6. The session
- **Intro.** The crew arrives at the truck. The exterior doors are locked (`DoorLock`), and the grandmother waits on the front porch. Talking to her (E, "Talk") plays her lines and `Give_Keys`, then raises `KeysHandedOver`. INTERACTION unlocks the house, GAMELOOP moves to ContractStarted, then InProgress, and the timer starts.
- **Breaking in.** Breaking a window or a door during the Intro also starts the contract, flagged as a break-in (it costs money and patience).
- **Also starting the contract:** breaking through a wall during the Intro counts as a break-in too. Opening a window starts the clock without a bill.
- **InProgress.** Delivery happens at the truck: `DeliverPoint`, an Interactable on the right side at the back of the truck (it rides with the truck), prompts "Deliver 7/12" and is enabled when every remaining required item is loaded.
- **Completed.** The settlement is itemised:
  - contract pay, with half pay for broken items;
  - destroyed required items billed at full value;
  - unseen theft paid at its value;
  - witnessed theft confiscated and fined at its value;
  - break-in costs.
- **Failed.** The time is up, or the grandmother called the police.
- **The end screen** shows the settlement. F5, or E after 3 s, reloads the scene.
- **A scene without a grandmother** (Tutorial_01) starts directly in InProgress.

## 7. Players and split screen
- **Two crew members:** P1 is the existing `Player`, and P2 is a copy with the blue crew body. Each has `CrewMember`, `CrewInput`, its own camera (side by side, vertical FOV about 75) and its own HUD pieces. There is exactly one AudioListener.
- **Input sources:**
  - P1 plays on keyboard and mouse.
  - P2 plays on gamepad 1 when one is connected, otherwise on `NullInputSource`.
  - **F1** swaps the keyboard to the other player, and **F2** cycles the layout: split, solo P1, solo P2.
- **Holding is exclusive,** through `MovableObject.holder`.
- **Heavy objects:** above the carry limit (`canCarry` false, or too heavy), the grab drags the object along the floor (push and pull) instead of lifting it.
- **Crew bodies:** they stand on the floor, feet at the capsule bottom. The Animator gets `Speed`, `MoveScale`, `Crouch` and `Grounded`, and a carry layer weight. A player's own camera does not render the inside of its own head; it draws the body's arms (section 13).

## 8. The grandmother
- **Components:** `GrandmaBrain` (the state machine), `GrandmaMover` (NavMesh built at Play, CharacterController movement, door opening), `GrandmaSenses` (vision cone and hearing), `GrandmaMood` (patience), `GrandmaActivities` with `ActivitySpot` markers in the scene, `GrandmaSpeech` (bubbles and voice), `GrandmaHUD` (the patience bar top right in each viewport) and `GrandmaTalk` (Interactable).
- **States:** Intro, GiveKeys, Routine (walk to an activity), PerformActivity, Observe (she noticed something and turns to look), Investigate (she walks to a noise), React (speech and anger clip), Confront (at low patience she follows the offender and scolds) and CallPolice (at 0).
- **Patience:** 100 to 0. The costs per event are data, and the thresholds are 70, 40 and 20. She recovers slowly when nothing happens.
- **Vision:** 110 degrees, 14 m, a line of sight from her head to the player's eyes or chest. Intact glass does not block it.
- **Hearing:** events within `HearingRadius(loudness)`, halved per wall or floor in between.
- **Theft witnessed:**
  - she sees a player pocket an item of hers that is not on the list, or load one into the truck;
  - or she sees them carrying one for more than 2 s.
- **Activities:** sit in the rocking chair, read a book, drink tea, cook, water the plants, light the fireplace, watch TV and look out of the veranda. Each is an `ActivitySpot` with a clip, a duration and a prop. If the object of a spot was moved away (for example the armchair is in the truck), she comments and picks another.

## 9. Destruction
The plan follows the investigation in `scratchpad/slice/investigation/A5a_destruction_code.md`, sections 3.3 to 3.10:
- `DamageEvent`, and `IDamageable` on `Breakable`, `GlassPane` and `DestructibleModule`.
- A material table (ScriptableObject), with today's numbers kept exactly.
- `BlastSolver`, with local damage per chunk.
- A lazy chunk swap from the pre-fractured assets.
- `StructureGraph`, where support runs through RestsOn edges and a limited number of lateral hops.
- A collapse queue of at most 40 per frame.
- Cracked glass, a debris budget at spawn time, the Structure, Props, Glass and Debris layers, and plinths kept when a wall goes.

What stays solid:
- **Floors and stairs:** never.
- **Foundation:** capped at Damaged.
- **Roofs:** they fall as whole rigid sections when unsupported.

Debug keys: overlay, grenade, explosion, damage and reset.

## 10. Layers and execution order
**Layers** (the integrator adds them to TagManager): 8 Structure, 9 Props, 10 Glass, 11 Debris, 12 Crew, 13 NPC. Code looks them up by name, and falls back to Default when a layer is missing.

**Execution order:**

| Order | Script |
|---|---|
| -600 | SliceDebug |
| -560 | CrewSpawner (clones P2) |
| -550 | CrewMember |
| -500 | CrewInput |
| -400 | GameSession |
| -200 | HouseDestruction |
| -100 | PlayerInteract |
| -10 | PlayerPockets |
| 0 | everything else |
| 50 | HouseInteractionSetup |
| 60 | GrandmaMover (NavMesh build) |
| 1000 | CameraShake |

## 11. Debug keys (F-keys only, registered through DebugCommands)

| Key | Command | Owner |
|---|---|---|
| F1 / Shift+F1 | swap which player the keyboard drives / bring the other player here | PLAYER |
| F2 | layout: split, solo P1, solo P2 | PLAYER |
| F3 / Shift+F3 | destruction overlay (HP, material, state, support) / collider and rigidbody view | DESTRUCTION |
| F4 / Shift+F4 | grandmother overlay (state, patience, cone, hearing) / her AI on and off | GRANDMA |
| F5 | reset the scene (reload) | GAMELOOP |
| F6 / Shift+F6 | force Completed / force Failed | GAMELOOP |
| F7 | event log (last WorldEvents) | GAMELOOP |
| F8 | give or take the house keys | INTERACTION |
| F9 / Shift+F9 | a grenade in your hands / an explosion at the crosshair | DESTRUCTION |
| F10 / Shift+F10 | apply the selected damage at the crosshair / cycle it 10, 25, 50, 100 | DESTRUCTION |
| F11 | reset destruction only | DESTRUCTION |
| F12 | list of debug keys | SliceDebug |

## 12. HUD regions (per viewport, `ViewportGUI.Region`)

| Region | Content | Owner |
|---|---|---|
| TopLeft | contract panel (objectives, money, timer) | GAMELOOP |
| TopCenter | toasts from events | GAMELOOP |
| TopRight | the grandmother's patience and mood | GRANDMA |
| Center | crosshair | PLAYER |
| BottomCenter | interaction prompt; the driving controls while at the wheel | PLAYER; TRUCK (VehicleSeat, while PlayerInteract is off) |
| BottomRight | pockets | PLAYER |
| BottomLeft | one-line controls hint | GAMELOOP |

Full-screen cards (intro title, end screen) are drawn by GAMELOOP over both views. Debug overlays draw where they like but stay off the Center region.

## 13. Animation contract
**Clips** (FBX, Humanoid, in place, 24 fps, from `tools/blender/author_clips.py`):
- Crew_Idle, Crew_Walk, Crew_Run, Crew_CrouchIdle, Crew_CrouchWalk;
- Grandma_Walk, Sit_Down, Sit_Idle, Stand_Up, Sit_Read, Sit_Drink;
- Kneel_Down, Kneel_LightFire, Kneel_Up, Stand_Cook, Stand_Water, Stand_Drink;
- Angry_ShakeFist, Angry_HandsOnHips, Angry_Point, Talk, Give_Keys.

`Grandma_Idle` and `Anim_Carry_Idle` already exist.

**`AC_Crew_Slice`** (built by the integrator):
- **Parameters:** `Speed` (float, m/s), `Crouch` (bool), `Grounded` (bool, default true) and `MoveScale` (float, default 1).
- **Base layer:** the `Locomotion` blend tree (Crew_Idle 0, Crew_Walk 1.2, Crew_Run 3.7), the `Crouch` blend tree, and `Air` (Crew_CrouchIdle). `MoveScale` is the speed multiplier of Locomotion and Crouch: past a clip's authored ground speed the cycle plays faster instead of the feet sliding (walk 4.5 m/s plays the run at x1.22, the sprint at the cap of x1.8). `Air` is entered when `Grounded` is false and left on landing.
- **Layer 1, `Carry`:** override, arms mask, Anim_Carry_Idle. Its weight is set by code, 0 while a small item is held in one hand.

**One skeleton (hot-fix 2026-09-27).** The body's Animator is the only source of the crew's movement; everything else is posed on its bones after it:
- `CrewAnimator` lifts the body by 0.22 m in `Air` (a tuck, the feet come up), and crouches it as low as the eyes (GREYBOX_SPEC: eye 0.80 m): hips down and back, the spine folded until the head is at the eyes, the legs refolded on the feet by `LimbIK`.
- `FirstPersonHands` puts the body's hands where they hold, carry or reach (`LimbIK`), then draws the first-person forearms, hands and upper arms on those bones for the owner's camera only. The shadow, the other player's view and your own arms are the same arms. At rest the hands hang out of view, as the shadow's do.
- The body always animates (`AlwaysAnimate`), since your own arms are drawn on it.

**`AC_Grandma_Slice`:**
- **Parameter:** `Speed`.
- **Base layer:**
  - the `Locomotion` blend tree: Grandma_Idle 0, Grandma_Walk 0.5, Crew_Walk 1.1;
  - one state per activity clip, named after the clip;
  - automatic transitions: Sit_Down to Sit_Idle, Stand_Up to Locomotion, Kneel_Down to Kneel_LightFire, Kneel_Up to Locomotion, Give_Keys to Locomotion.
- **Layer 1, `UpperBody`** (override, upper-body mask): an `Empty` state, plus Talk and the three Angry clips, which return to Empty.

GRANDMA code drives it with `CrossFadeInFixedTime(stateName, 0.25f, layer)` and `SetFloat("Speed")`.

## 14. Integration protocol
- **Staging.** Agents never write into `Assets/`, `ProjectSettings/` or the scene. C# goes to a staging folder that mirrors `Assets/_Movers/Scripts/`. Blender outputs go to a staging folder that mirrors `Assets/`.
- **Compile.** Every agent compiles its staging against the contracts with the harness (`scratchpad/compilecheck/check.sh contracts mine`).
- **Recipe.** Each agent also writes `INTEGRATION.md`, a recipe: the scene and prefab changes, as Unity_RunCommand C# snippets, and the Play-mode tests that prove the system works.
- **One integrator** installs everything after a joint compile of all staging folders. It copies FBX files over existing ones while keeping their `.meta` files, runs the recipes, and runs the tests in Play mode with `Application.runInBackground = true`.
- **The editor is shared with Pierre.** Never stop Play mode under him, never save a scene he has unsaved changes in, and check `isDirty` and the selection first.
- **Blender:** background processes only, never the Blender MCP, and never a write to `_ArtSource/assets.blend`.

## 15. Validation tests (the brief's, made concrete)
1. **A grenade in a room:**
   - a window breaks, a few objects are damaged, and the wall is locally damaged (chunks gone near the blast);
   - the rest of the house is intact;
   - zero errors, and the destruction frame is measured.
2. **A grenade against a partition:** several chunks are removed, some fall, and the rest of the partition stays up.
3. **A grenade against a cellar wall:** limited damage, the wall caps at Damaged, and nothing collapses.
4. **Repeated blasts on the same wall:**
   - damage accumulates;
   - the upper part falls once its supports are gone;
   - a roof section falls when every wall under it is gone.
5. **Doors:**
   - the front door is locked at the start;
   - the grandmother hands over the keys and it opens and closes;
   - a door leaf can be destroyed;
   - a door opens in under 0.5 s.
6. **Windows:**
   - the sashes open and close without clipping;
   - glass cracks, then breaks;
   - a broken pane lets a thrown object through.
7. **The grandmother:**
   - the intro and the keys work;
   - she resumes her routine, walks between rooms and floors, and performs at least three activities;
   - she hears a smash and investigates, and her patience drops;
   - she sees a theft, and the ledger marks it witnessed;
   - at 0 patience the run fails.
8. **The full run,** in two-player split screen: intro, contract, exploration, interaction, some destruction, loading the truck, delivery and settlement.
9. **Regression:**
   - the carry numbers are unchanged;
   - pockets, cigarette, beer and equip work for both players;
   - Tutorial_01 still plays;
   - a scene reload leaves no errors.
