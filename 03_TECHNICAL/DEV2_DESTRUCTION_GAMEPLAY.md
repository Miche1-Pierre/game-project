# DEV 2: destruction, world and gameplay (build spec)

_Written 2026-09-28 from Pierre's "DEV 2" list and QA note ("fractures judged unrealistic, damage insufficient"), revised the same day after a gameplay and a tech review (appendix A). Decision record: [ADR-013](../decisions/ADR-013-dev2-destruction-police.md)._

**Scope rule.** Keep the architecture, improve it. No new framework, no rebuild. Code is born only where an item needs it. Every number below is a first guess in data (ADR-009), to be confirmed in Play.

**Branch.** `dev2/destruction-gameplay` (already created from `c3861d1`). Tracks branch from the CONTRACTS commit in their own worktrees and never touch the Unity editor (NETCODE_SLICE 13.2 workflow). Merging happens in a separate worktree too (section 15): nobody runs `git checkout` or `git merge` in `C:/GameProject` while the editor may be open, except the single switch of 15.3.

**Units.** Metres, seconds, kilograms, joules (J), newton-seconds (N.s). "HP" means the health units of `Breakable`, `DestructibleModule` and chunks. Distances to a wall are to the chunk's nearest point. Chunk counts are for the re-fractured sets (section 3.9, about 20 masonry chunks per wall); for the old 12-chunk sets read the percentages.

---

## 1. Goals and measurable targets

| Item | Target (checked in Play, Map01, offline and online host) |
|---|---|
| 1 Destruction | One grenade on the floor at the foot of an exterior plain wall: at 0.25 m, at least 50 % of the chunks (and of the volume) come out, those within 0.8 m as rubble (3 to 6 pieces each); at 1.0 m, 25 to 45 %; at 1.5 m, at most 15 % and a visible tint; at 3 m, nothing. Interior wall at 0.25 m: at least 60 %. Two grenades at the same spot 0.5 m away: the wall collapses. **Thrown:** five grenades thrown at a plain exterior wall from 5 m: at least 20 % of the chunks per grenade on average, and at least one breach 1.5 m wide or more. A grenade in the middle of a 3 m room: each wall loses at most 15 % of its chunks. A grenade 0.25 m from one wall of a 3 m room: the opposite wall loses at most 2 chunks (launched slabs do not domino, 3.14). A 2 kg cup thrown at 6 m/s never marks a wall. Holes read as broken masonry, not a jigsaw of metre slabs (re-fractured sets, compared before and after for Pierre). |
| 2 Mass and speed | One function, `ImpactDamage.Evaluate` (section 2), calibrated on speeds players reach (standing throw 6 m/s, sprint throw 8 to 9.6 m/s, carried piano 8.5 m/s at most). Against a plaster wall: cup 2 kg at 6 m/s 0; chair 10 kg at 7 m/s 0; sofa 60 kg standing throw 0, at 7 m/s dust; a sprint-thrown sofa cracks the wall and a second or third opens a hole; a piano thrown at 7 m/s opens a hole about 1 m across. A sofa thrown at 6 m/s explodes a wooden fence. The truck scales from dust at 10 km/h to an opened wall at 90 km/h (row 5). A thrown cup still breaks a window. All numbers editable in `DestructionMaterials.asset`. |
| 3 Explosions | Damage, push, wall breach and debris launch all follow the distance (curve in 3.1). Chunks within 0.5 m fly out at 5 to 10 m/s along the blast direction instead of hopping in place. A thrown grenade stops dead at the foot of the wall it hits (no bounce). Destruction cost per frame (section 11) at most 16 ms on the host and the client, over the blast frame and the 5 frames after it. |
| 4 Debris | No wall chunk ever vanishes, on the host or the client (a refused chunk waits at most 3 frames). Lifetimes: structure 60 s, props 40 s, glass 20 s, each plus or minus 25 %. An expired piece shrinks only when no crew camera sees it or it is farther than 12 m from every crew eye; hard stop at twice its lifetime. At most 450 dynamic and 300 frozen pieces, at most 90 spawned and 30 destroyed per frame. Props keep all 90 spawns when no wall is being broken. |
| 5 Truck ram | Fence or gate at 11 km/h: explodes. At 30 km/h the truck keeps at least 90 % of its speed through a fence. Exterior plaster wall, empty truck: 10 km/h dust only (a parking bump never opens the house), 15 km/h cracks, 25 km/h a hole about 1 m across, 40 km/h 4 to 6 chunks of the old size (about 30 % of the wall) and the truck almost stops, 90 km/h the wall is opened or collapses and the truck keeps at least 70 % of its speed. The loaded truck (6 t) at 40 km/h opens visibly more than the empty one. A 20 kg prop hit at 90 km/h leaves the truck's path within 0.5 s. Hedges, bushes, the mailbox, the two driveway posts and the small garden props break (6.5). Everything the truck breaks is blamed on its driver. |
| 6 Driving | Top speed 90 km/h (governor). 0 to 50 km/h: at most 4 s empty with no wheelspin, at most 7 s loaded (6 t). 0 to 90 km/h: at most 9 s empty, at most 15 s loaded. Stop from 90 km/h in at most 45 m, empty or loaded. Climbs a 26 % grade loaded at 15 km/h or more. Steering limit about 12 degrees at 40 km/h, 2.4 degrees at 90 km/h: no slide at full lock. A truck on its side rights itself after 2 s. |
| 7 Grandmother | Every priced stimulus moves the bar and gets at least a head turn (no silent cap). A blast, a theft and the truck hitting her are never dropped from her queue. The last warning happens once per run, is shown on the HUD for its whole duration, and can only be ended by a player's own offence costing 2 or more, at least 3 s after it began. At patience 0 with the warning used, the police call is immediate. A truck nudge at walking pace costs a bump, not a run-over. Garden damage costs little and never counts as "breaking through a wall". |
| 8 Police and flee | At the call: sirens grow, HUD "POLICE IN 1:15", exit marker shown, the crew may keep looting. Lead car arrives at the house exactly at the end of the countdown. Then "ESCAPE 1:30" counts down: at 0 the police have surrounded the house (failed). Truck in the exit volume with at least one free crew member aboard: YOU GOT AWAY. **For the same truck load, the escape pays 40 to 60 % of a clean delivery.** A police car stays within 6 m of the truck while it is under 8 km/h for 4 s: failed. A car can get ahead of a truck doing 80 km/h on a straight and block the road. Truck parked off the route after arrival: failed within the escape time. A crew member on foot caught: arrested (frozen, face down), fined. All crew arrested: failed. **Every failure is announced on screen at least 2 s before it happens.** Everything from markers and data: no Map01 name or position in code. |
| 9 Timer | 20 minutes (1200 s) on Map01. |
| Online | The client arrested cannot move on either screen. The client as passenger: no pose fight in `net_host.log`. Peak bandwidth under 80 KB/s during two grenades at one furnished wall and a 90 km/h ram through fence, yard props and a wall. No pops of the police cars at 100 km/h. NetIds count and tracked-body count identical with the debris pool on and off. |

---

## 2. Impact energy model (item 2)

### 2.1 Formula
One function, `ImpactDamage.Evaluate(in ImpactInput) -> ImpactOutcome`, used by every collision receiver (`Breakable`, `DestructibleModule`, `GlassPane` through `ImpactDamage.TryMeasure`) and by the truck's ram sweep (section 6.4).

```
v    = approach speed along the contact normal (m/s)
mu   = reduced mass of the two sides (kg)
         receiver dynamic, other static or kinematic    mu = m_receiver
         receiver anchored (myBody null or kinematic),
         other dynamic                                  mu = m_other
         both dynamic                                   mu = m_r * m_o / (m_r + m_o)
E    = 0.5 * mu * v^2                                   collision energy (J)
m_res = the receiver's resisting mass:
          dynamic props: their body mass
          glass panes: their pane mass (10 kg per m2, as today)
          other built pieces (walls, chunks, fences, posts, yard props): structureReferenceMass
ratio = clamp(mu / m_res, minRatio, maxRatio)
          minRatio: the rules' minRatio (Glass 0.5) or the table's minMassRatio (0.1)
          maxRatio: Vehicle vehicleMaxMassRatio (200); held and cargo 1; otherwise maxMassRatio (12)
v_eff = v * sqrt(ratio)            (= sqrt(2 E / m_res) inside the clamp)
gate  = row.minImpactSpeed + speedAllowance
damage = energyDamageScale * TypeMultiplier(type) * strikeFactor * max(0, v_eff - gate)^2   (HP, before the material factor)
          strikeFactor = structureChunkStrikeFactor (0.1) when the striker is a launched wall chunk, else 1
spread = (type != Blast && damage > 0) ? min(impactMaxSpread, impactSpreadPerCubeRootKJ * cbrt(E / 1000)) : 0   (m)
push   = impactPushFactor * v * min(mu, m_res)                                               (N.s)
speed  = v                                                                                    (DamageEvent.speed)
```
- **Energy-based:** `v_eff^2 = 2E / m_res`, so the gate is an energy threshold per unit of resisting mass (`E_min = 0.5 * m_res * gate^2`: 1.1 kJ for plaster at 35 kg), and above it damage grows with the energy. A prop receiving a hit from something static has `ratio = 1`, and `energyDamageScale 27 = 9 x 3` gives exactly today's numbers for the prop (today a static other counted as ratio 3). So **everything a prop suffers is unchanged**; only built receivers are recalibrated.
- **`structureReferenceMass` 35 kg** is calibrated on reachable speeds (2.2): throws cannot go faster than about 9.6 m/s, so a heavy thrown object must count for more than its speed.
- **Vehicles.** A body with a `TruckVehicle` or a `PoliceCar` striking something outside it is `DamageType.Vehicle`: its real mass counts up to `vehicleMaxMassRatio` (200, so 3.5 t, 6 t loaded and a 1.4 t police car all differ), and `vehicleMultiplier` (0.01) stands for the bumper absorbing most of the energy. Props struck by a vehicle survive the bumper more often than not and are then flung (6.4), which is what breaks them.
- **Cargo against its own truck** (a cargo body in `TruckCargo.inside` hitting that truck's colliders) is **not** Vehicle: type Impact with the cargo rules (`cargoSpeedAllowance`, ratio at most 1).
- **Glass keeps small strikers dangerous:** `Rules.Glass.minRatio` 0.5, so a 2 kg cup at 6 m/s still breaks a 10 kg pane (113 against 12 HP), as today.
- **The material factor** (`row.impactFactor`, `row.blastFactor`) is still applied by the receiver, as today.
- **The spread** follows crater scaling (radius grows with the cube root of the energy). It is written into `DamageEvent.radius` for non-blast hits, and `DestructibleModule.ApplyDamage` spreads such a hit over the chunks within it (3.5). Breakables and panes ignore it.
- **Held** keeps its cushioning (+1.5 m/s, ratio at most 1).
- **Who and how** (`Classify`) is unchanged except Vehicle (instigator `TruckVehicle.DriverActor` for the truck, `Actors.World` for a police car) and the launched-chunk strike factor. A launched chunk striking an attached chunk of its own module does no damage (3.14).
- **`TryMeasure` stays as it is** (signature, `Rules`, `Plain`, `Held`, `Glass`, `BlameSeconds`, `HeldSpeedAllowance`, section 13); it returns false for a receiver collider that `TruckRam.Handled` reports, and counts launched-chunk strikes against `debrisStrikesPerFrame`.

### 2.2 Calibration (defaults, all in `DestructionMaterials.asset`)

| Field | Default | Note |
|---|---|---|
| `energyDamageScale` | 27 | replaces `impactDamageScale` 9 (kept serialized, unused) |
| `structureReferenceMass` | 35 kg | replaces `Breakable.referenceMass` (100) and `DestructibleModule.ReferenceMass` (100) for built receivers; panes keep their own mass |
| `minMassRatio` / `maxMassRatio` | 0.1 / 12 | non-vehicle strikers over 420 kg count as 420 kg against a built piece |
| `vehicleMaxMassRatio` / `vehicleMultiplier` | 200 / 0.01 | vehicles' real mass counts (up to 7 t); the bumper absorbs 99 % |
| `impactPushFactor` | 0.5 | today's factor |
| `impactSpreadPerCubeRootKJ` / `impactMaxSpread` | 0.3 m / 2.5 m | 30 kJ gives 0.94 m, 216 kJ gives 1.8 m |
| `heldSpeedAllowance` / `heldMaxMassRatio` | 1.5 / 1 | today's `ImpactDamage.Held` |
| `cargoSpeedAllowance` / `cargoMaxMassRatio` | 3 / 1 | new: cargo hitting its own truck's box |
| `glassMinMassRatio` | 0.5 | `Rules.Glass.minRatio` |
| `glassDebrisMinMass` | 2 kg | today's constant |
| type multipliers `Impact, Thrown, Fall, Crush, Tool` | 1, 1, 1, 0.5, 1 | Crush halved: falling roofs crush softly |
| `structureChunkStrikeFactor` / `debrisStrikesPerFrame` | 0.1 / 20 | launched wall chunks hit 10 times softer; at most 20 debris strikes damage anything per frame |
| `debrisPhysicsMassCap` | 150 kg | a chunk's Rigidbody mass is capped on both machines (the real mass stays for `StructureCollapsed` and crush blame) |

Worked outcomes (Python replay of the formula, `calib.py` in the design scratchpad; plaster `vMin 8`, fence wood `vMin 4.5` and 140 HP; wall fracture threshold 120 HP and chunk HP 210 to 390 after the re-fracture, 3.9):

| Striker | v | vs plaster wall | vs wooden fence |
|---|---|---|---|
| cup 2 kg | 6 m/s | 0 | 0 |
| chair 10 kg | 7 m/s | 0 | 0 |
| sofa 60 kg | 6 m/s (standing throw) | 0 | 304: explodes |
| sofa 60 kg | 7 m/s | 37: dust, stored | explodes |
| sofa 60 kg | 9 m/s (sprint throw) | 387, spread 0.4 m: cracks, at most 1 small chunk | explodes |
| piano 180 kg | 7 m/s | 1.7k, spread 0.5 m: a hole about 1 m | explodes |
| piano 180 kg | 8.5 m/s | 3.4k, spread 0.56 m | explodes |
| truck 3.5 t | 10 km/h | 106: dust | 146: explodes |
| truck | 15 km/h | 306, spread 0.94 m: cracks | explodes |
| truck | 25 km/h | 1.0k, spread 1.3 m: a hole about 1 m | explodes |
| truck | 40 km/h | 2.9k, spread 1.8 m: about 30 % of the wall | explodes |
| truck | 90 km/h | 15.8k, spread 2.5 m: opened or collapsed | explodes |
| truck loaded 6 t | 10 / 40 / 90 km/h | 217 / 5.1k (spread 2.15 m) / 27.5k | explodes |
| police car 1.4 t | 80 km/h | 4.7k | explodes |
| launched chunk 150 kg (strike factor 0.1) | 10 / 5 m/s | 218 (a crack) / 7 | explodes / 0 |
| falling roof 1 t (Crush) | 6.3 m/s | 2.6k | explodes |
| truck against a 10 kg chair | 90 km/h | the chair takes 113 of 153 HP, is flung at 35 m/s and breaks on landing | |

---

## 3. Destruction changes, walls (item 1)

The pre-fractured architecture stays; the wall chunk sets are re-cut finer (3.9). What changes:

1. **Structure falloff curve (blast).** `DestructionMaterialTable.structureFalloff` (AnimationCurve, share against metres) replaces the `1 / (1 + (d / 0.5)^2)` focus; `structureFocus` stays as the fallback when the curve has fewer than 2 keys. Default keys: (0, 1) (0.5, 0.95) (1.0, 0.7) (1.5, 0.35) (2.0, 0.15) (3.0, 0.03) (4.0, 0). The distance is divided by `cbrt(power)` (`focusScalesWithPower`), so a stronger explosive opens a bigger hole. The same curve applies to whole structural Breakables (pillars, `PK_Wall_Corner`, chimney cap, fireplace, posts), which today take the blast unfocused (audit P12).
2. **Chunk HP by size, narrowed.** `chunkHealthClamp` (0.7, 1.3) replaces the hard-coded 0.5 to 2 at `DestructibleModule.cs:608`.
3. **Accumulated damage breaks a wall up.** When the intact wall's stored health falls to `MaxHealth * (1 - fractureAtShare)` or lower, it fractures (`fractureOnAccumulated`). The intact body is tinted `DamagedTint` once Damaged, so repeated small hits read.
4. **Propagation.** A chunk removed by damage (not by a fall) passes `overflowShare` (0.35) of its overkill (`-health`) to its attached sidecar neighbours, weighted by shared cut area, depth 1, same type and instigator. The neighbour list is kept per chunk when `BuildSpecs` reads `ChunkSetData.neighbours`.
5. **Impacts spread.** The intact wall's `OnCollisionEnter` and `OnChunkCollision` go through `ApplyDamage`, never straight to one chunk. For a non-blast event, the spread radius is `e.radius` (from `Evaluate`) and weights are `(1 - d / spread)^1.3`; a blast event routed through `ApplyDamage` keeps today's `clamp(radius * 0.25, 0.5, 1.5)`. The 0.1 s cooldown stays per module (one hit spreads). `DamageResult.applied` is the HP actually removed, capped at each chunk's remaining health and summed over the spread chunks.
6. **Launch.** In `RemoveChunk` (not for falls): `v = dir * e.speed * share * clamp(sqrt(chunkLaunchReferenceMass / physMass), 0.4, 1.5) + up * chunkUpBias * e.speed`, spin `chunkSpin` (2 to 5 rad/s), `share = 1` for blasts and `impactChunkLaunchShare` (0.7) for impacts. For blasts `e.speed` is `blastChunkEjectSpeed (10) * curve(d) * cover`, written by `BlastSolver`. `physMass = min(mass, debrisPhysicsMassCap)`. The piece gets `DebrisPiece.structureChunk = true`, `structureSource = the module` and `launchedAt = Time.time` (host in `RemoveChunk`, client in `NetDetach`). `Explosion.PushBodies` and `PlayCosmetic` skip only pieces with `structureChunk && Time.time - launchedAt < structureLaunchGrace` (0.3 s): the blast that launched them does not push them twice on either machine, and later blasts push old rubble as today.
7. **Rubble for big hits.** A chunk removed with overkill at least `rubbleOverkill` (1.5) times its max HP, or by a blast whose centre is within `breachRadius` (0.8 m) of it, is cut with `MeshShatter.ShatterChunk` into `rubblePieces` (3 to 6, more with more overkill) pieces with the same launch. If the budget refuses, the whole slab detaches (never nothing). Small hits and support falls keep detaching whole slabs.
8. **Never vanish, on both machines.** Before a blast or a ram applies damage, the caller announces it (`DebrisManager.ExpectStructure(n)`, 5). `Detach` asks `DebrisManager.TryReserveStructure(1)`. On refusal the chunk's collider goes off at once, its renderer stays, and it joins a per-module queue retried on the next 3 frames with the same velocity, then forced (a structure chunk is always granted on the 3rd retry). The client's `NetDetach` does the same (reserve, same 3-frame queue, rubble only when the budget allows, else the whole slab) and applies `min(mass, debrisPhysicsMassCap)` like the host. `SetActive(false)` remains only for a null transform.
9. **Feedback by energy, never doubled.** Chunk removal and any damaging hit on a wall call `ImpactFeedback.Hit(point, energy, size, kind, instigator)`: dust count and radius, sound volume, and a camera shake over 50 kJ follow the energy. A damaging hit that removes nothing spawns 1 to 3 chips (a small `MeshShatter` of the hit chunk, 0.1 of its mass, budget permitting). **Online:** the host sends `ImpactFx` only for hits that remove nothing (dust, chips, sound) and, with `size 0` (shake only), for hits over 50 kJ; chunk removals reach the client as `ChunkDetached`, whose `NetDetach` plays the dust and sound scaled by the received speed. The client never gets two puffs for one removal.
10. **Join snapshot to 32 chunks.** `ModuleSnapshot` carries `ulong attachedMask` and `ulong looks` (2 bits x 32). Fixes `PKX_Gable_4m_Window` (22 chunks) and carries the re-fractured sets (at most 28).
11. **Cost.** `ApplyPerChunk` computes `ClosestPoint` once per chunk (cached). Every chunk mesh of every chunk set the catalog uses is pre-cooked at load with `Physics.BakeMesh` (non-convex for the attached chunk, convex for its debris), in `HouseDestruction` setup on both machines, spread over the load frames. Unconditional: `Detach` then never cooks a hull at runtime.
12. **Yard props (item 5).** `HouseDestruction` also wires `extraRoots` (default `HouseContents/Garden/Dressing`) against new catalog rows, as whole-piece anchored Breakables of `Kind.Element` flagged `yard` (table in 6.5). A yard piece sets `Breakable.isYard`: its destruction raises `DestructionEvents.Garden` (`WorldEventType.GardenDamaged`), never `StructureDamaged`, so it is neither a wall for the grandmother nor a break-in.
13. **Dead data.** The `PKX_Wall_Cellar` chunk sets stay unused (Foundation by design).
14. **Contained dominoes.** A launched chunk (`structureChunk`) striking an attached chunk of the module it came from does no damage (`OnChunkCollision` checks `structureSource`). Against other built pieces it strikes at `structureChunkStrikeFactor` (0.1), and at most `debrisStrikesPerFrame` (20) debris strikes apply damage per frame (the rest are ignored that frame). `DestructionDebug` shows the per-frame count. Pierre chooses domino or contained (decision 17); the default is contained.

### 3.9 Re-fracture (done in this pass, revert if Pierre prefers the slabs)
QA's first complaint is the look of the slabs, which curves and rubble alone do not fix for support falls and medium hits. WALLS re-runs `tools/blender/fracture_modules.py` headless from its worktree (the script resolves `ROOT` from its own path, so it writes into the worktree): `"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup --python tools/blender/fracture_modules.py -- --modules PK_Wall_Plain PK_Wall_Window_Small PK_Wall_Window_Big PK_Wall_Door PKX_Wall_Garage PK_Wall_Interior PKX_Wall_Int_Door PKX_Wall_Int_Arch --renders <scratch> --report <scratch>`, about 40 s.
- `MODULES` rows: target 20, range 16 to 24 for Plain, Window_Small, Window_Big, Door, Interior, Int_Door; target 16, range 12 to 20 for Garage and Int_Arch (were 10). Size floors unchanged. Seeding uniform (gamma 1.0 at line 1485 for these rows). Wood frames unchanged (4 per window or door wall), so at most 28 chunks per module (32-chunk snapshot, 3.10).
- `DestructibleModules.asset`: `chunkHealth` 500 to 300 (exterior, garage) and 350 to 210 (interior), so a wall keeps its total toughness. The fracture threshold (`chunkHealth * fractureAtShare`) becomes 120 exterior and 84 interior: walls crack more readily, as QA asked, and 2.2 is calibrated on it.
- Existing `.meta` files and GUIDs are kept (the script never touches them), chunk names and counts change: this is why the 32-chunk snapshot and Protocol 2 go together with it.
- One separate commit (`feat(destruction): re-fracture the wall chunk sets finer`), so reverting it restores the 12-slab sets. The Workbench renders (before and after) go to Pierre with the first Play capture.
- ADR-013 amends ADR-009's "8 to 15 per module" to "up to 24 masonry chunks per module".

---

## 4. Explosions (item 3)

- **Damage:** `blastDamage * power * (1 - d / r)^blastExponent * cover`, times `structureFalloff(d / cbrt(power))` for walls and structural Breakables (3.1). Props keep the plain falloff (they already die across most of the radius).
- **Wall chunks:** `BlastSolver` writes `speed = blastChunkEjectSpeed * curve(d) * cover` into each chunk's event (3.6). Rubble inside `breachRadius` (3.7). Before `Apply`, `BlastSolver` calls `DebrisManager.ExpectStructure(n)` with the number of chunks within the curve's reach.
- **Push, props:** `dv = min(pushImpulse * power * f / mass, maxLaunchSpeed * f^lightDebrisLaunchFalloff)` with linear `f`, from the centre lowered `pushUplift`. Light shards then slow with distance instead of all flying at the cap. Freshly launched structure chunks skipped (3.6). No cover ray for Debris-layer bodies (perf).
- **Players:** unchanged (knock 10 / 4.5 m/s, reach 1.2 x r, concussion 0.45), values moved to the table.
- **Feedback:** camera shake strength `min(power, shakeMaxStrength 2)`, range `shakeReach 3 x r`.
- **Grenade:** radius and power read from `grenadeRadius` / `grenadePower` in the table when the grenade is created (the crate builds them in code, so their serialized fields never mattered). **Dead bounce:** `GrenadeItem.Create` gives the collider a runtime `PhysicsMaterial` (bounciness 0, `bounceCombine Minimum`, dynamic and static friction 1, `frictionCombine Maximum`), and the body's angular damping rises from 0.6 to 3 after its first contact: a grenade thrown at a wall drops at its foot instead of rolling back 1 to 2 m.
- **Every constant** of `Explosion` and `BlastSolver` moves to the table's Blast block (section 13), code defaults equal to today's values except where this section says otherwise.
- **Performance:** a blast touching many objects stays bounded by the spawn budget (90 per frame, a reserve for chunks only when chunks are expected), the deferred chunk queue on both machines, the cached closest points, the pre-cooked chunk colliders and the skipped debris cover rays. `Explosion.MaxFrameMsAfterBlast` (section 11) measures the result.

---

## 5. Debris lifecycle and budgets (item 4)

- **Numbers move to the table** (Debris block): `maxDynamicPieces 450`, `maxFrozenPieces 300`, `maxSpawnPerFrame 90`, `structureReservePerFrame 30`, `destroyPerFrame 30`, `lifetimeJitter 0.25`, `cleanupMinDistance 12`, `hardLifetimeFactor 2`, `cleanupShrinkTime 1.5`, `overflowShrinkTime 0.6`, `freezeAfterSleep 3`, `smallPieceNoShadow 0.3`, `frozenNoShadowBelow 0.5`, `structureLaunchGrace 0.3`. Lifetimes: structure 60, props 40, glass 20. The frozen cap is raised in data only after the draw-call check of section 11.
- **Reserve, only when needed:** `ExpectStructure(n)` sets this frame's reserve to `min(structureReservePerFrame, n)`. `TryReserve` (props, glass) never takes the reserved slots; `TryReserveStructure` can. With no call this frame the reserve is 0 and props keep all 90 (today's behaviour in a furnished room with no wall in reach).
- **Expiry:** at `born + lifetime * (1 +- jitter)` a piece becomes eligible; it starts shrinking only when it is outside every crew camera's frustum (planes computed once per frame per crew camera) or farther than `cleanupMinDistance` from every crew eye. At `hardLifetimeFactor x lifetime` it shrinks regardless.
- **Overflow:** dynamic count over the cap culls invisible pieces first, then by size and distance, over `overflowShrinkTime`. Frozen (kinematic) pieces count against `maxFrozenPieces` only, with the same order.
- **Destroy budget:** at most `destroyPerFrame` pieces destroyed (or returned to the pool) per frame; the rest wait shrunk and inactive.
- **Sleep:** unchanged rule (asleep 3 s on static ground: kinematic), plus freezing on top of a frozen piece. A frozen piece that loses its support is thawed by `WakeInBounds` as today.
- **Shadows:** pieces smaller than `smallPieceNoShadow` (m, bounds extent) cast no shadow; frozen pieces smaller than `frozenNoShadowBelow`, or farther than `cleanupMinDistance` from every crew eye, cast none either (checked when they freeze and every 1 s).
- **Pooling:** `DebrisPool` (new, owned by DebrisManager) rents and returns MeshShatter shells (GameObject + MeshFilter + MeshRenderer + BoxCollider + Rigidbody + DebrisPiece, and its Mesh, cleared and refilled). Wall chunk debris is the chunk itself and is not pooled. Capacity 256 shells, 64 warmed. **NetIds rule:** every shell is created with `hideFlags = HideFlags.DontSaveInEditor` (NetIds skips any non-None flag, `NetIds.cs:250`) and `rb.isKinematic = true` while pooled; warm-up starts in `DebrisManager.Update` no earlier than 2 frames after the scene loaded (after the NetIds sweep, `NetIds.cs:465-471`); shells keep the flag when rented and are never registered; `DebrisPool.OnDestroy` destroys every shell it owns, so none survives a scene change. Acceptance: the NetIds id count and tracked-body count are the same with the pool enabled and disabled; fewer GC allocations per blast; the grenade frame no slower.
- **Online:** debris stays local per machine (unchanged). The client never freezes (unchanged).

---

## 6. Truck driving and the truck as a ram (items 5 and 6)

### 6.1 TruckTuning (new data asset)
`Vehicles/TruckTuning.cs`, `Assets/_Movers/Data/TruckTuning.asset`, assigned on `TruckVehicle.tuning`. Without an asset the component's own fields are the fallback (today's values), so a scene never breaks. With it, the asset wins.

| Field | Default | Was |
|---|---|---|
| `maxSpeedKmh` / `governorBand` | 90 / 0.05 | 40 / 0.15 hard-coded |
| `motorTorque` (per rear wheel) / `enginePowerKw` | 5200 Nm / 260 kW | 2500 / none |
| `maxReverseKmh` / `reverseTorque` | 18 / 3500 | 12 / 1800 |
| `linearDamping` / `aeroDrag` / `rollingResistance` | 0.01 / 5 N per (m/s)^2 / 0.012 | 0.05 hard-coded / none / none |
| `brakeDecel` / `brakeBiasFront` / `absSlip` | 7.5 m/s^2 / 0.6 / 0.4 | 3000 Nm fixed |
| `handbrakeTorque` / `handbrakeSidewaysGrip` | 6000 / 0.7 | same |
| `coastBrakeTorque` / `holdBrakeTorque` | 300 / 3000 | 150 / 3000 |
| `maxLateralAccel` / `maxSteerSlow` / `maxSteerFast` / `steerRate` | 6.5 m/s^2 / 35 / 2.5 / 120 | lerp 35 to 10 by speed/cap |
| `forwardGrip` / `sidewaysGrip` | 1.5 / 1.3 | 1.5 / 1.6 |
| `substeps` (threshold, below, above) | 5, 12, 20 | 5, 12, 15 |
| `autoRightUpDot` / `autoRightSeconds` | 0.3 / 2 | none |
| `cargoCcdSpeed` / `cargoAboardMaxSpeed` | 12 m/s / 2 m/s | cargo under 40 kg only / none |
| ram: `ramMinSpeed` / `ramSweepExtra` / `ramFrontSlab` / `ramJoulesPerHp` / `ramCooldown` | 3 m/s / 0.3 m / 0.3 m / 80 J / 0.3 s | none |
| fling: `flingMassKg` / `flingUpShare` / `flingLateralShare` / `flingMaxSpeed` / `flingHandledSeconds` | 80 / 0.3 / 0.35 / 35 m/s / 1 s | none |
| `runOverMinSpeed` | 3 m/s | none |
| feel: `audioTopSpeed` / `gearBands` / `cameraExtraDistance` / `cameraExtraFov` / `crashShakeEnergy` | 25 m/s / 4 / 2 m / 8 / 50 kJ | 8 m/s, none |

Expected from the model (checked against the limits of section 1): 0 to 50 km/h about 3 s empty and 5 s loaded, 0 to 90 about 7 s and 12 s, 42 m to stop from 90. Drive force 2 x 5200 / 0.552 = 18.8 kN, 20 % over the 15.5 kN a loaded truck needs on 26 %; 9.4 kN per rear wheel against about 12.9 kN of rear grip per wheel empty, so no wheelspin at launch.

### 6.2 Driving model changes (`TruckVehicle`)
- **Motor:** force per driven wheel `min(motorTorque / r, (P / 2) / max(v, 1))`, then the governor fades it over the last 5 % below `maxSpeedKmh`. Strong launch that tapers, no gears in the physics.
- **Resistance:** `-aeroDrag * v * |v|` and `-rollingResistance * m * g` along the velocity, in FixedUpdate; `linearDamping` from the asset.
- **Brakes:** torque per wheel `brakeDecel * rb.mass * r / 4`, split `brakeBiasFront` front and the rest rear; a wheel whose `WheelHit.forwardSlip` exceeds `absSlip` has its brake released that step. Stopping distance becomes independent of the load.
- **Steering:** `limit = clamp(atan(maxLateralAccel * wheelbase / v^2), maxSteerFast, maxSteerSlow)` with the wheelbase from the axle positions (3.985 m on Map01).
- **Recovery:** `transform.up . Vector3.up < autoRightUpDot` and speed under 1 m/s for `autoRightSeconds`: `PlaceAt(position + up, yaw only)`.
- **Hills:** measured targets in section 1. If the Road_East climb (23 to 26 %) still stalls loaded, raise `motorTorque` in data.
- **Feel:** `TruckAudio` rpm from `gearBands` virtual gears (saw-tooth rpm, a dip at each shift) and road rumble normalised to `audioTopSpeed`; crash volume by `speed / 25`; `ChaseCamera` distance and FOV grow with speed; a camera shake on crashes over `crashShakeEnergy` through `ImpactFeedback`.
- **Cargo:** every riding cargo body gets ContinuousDynamic while the truck is faster than `cargoCcdSpeed` (not only cargo under 40 kg); cargo hitting its own truck uses the cargo cushioning of 2.1 (type Impact, not Vehicle).

### 6.3 Seats (flee support)
- **A passenger seat.** `VehicleSeat` gains `seatIndex` (0 driver, 1 passenger) and `drives` (bool). A passenger seat has no controls except look and exit, and uses the chase camera. Prompt "Ride" / "Monter". Enter and exit rules as for the driver.
- **`CrewMember.IsDriving` means "seated in the truck"**, set by both seats: every reader that means "seated" (`NetPlayerDriver` 124/165/200/252, `KnockdownTumble`, `CrewFootsteps`, `HeldPose`, `FirstPersonBody`, `FirstPersonHands`, `HandHeldProp`, `CrewAnimator`, `CrewBumper`, `GameHUD`) stays as it is, so the host stops posing a seated client and the seat alone places it. **Only the driver seat calls `TruckVehicle.SetDriver`.** Code that means "at the wheel" reads `TruckVehicle.IsAtWheel(m)` (section 13): `TruckAudio` and the ramp stow (TRUCK), `PlayerHudModel.cs:147` and `DriveView`, `VerbHints.cs:54`, `CrewIndicatorView.cs:191` (HUD).
- **TruckSync** binds the seats of the crew truck in `seatIndex` order; SeatEnter, SeatExit and SeatNotice carry the seat index (wire change). `OnPeerLeft` force-releases every seat the client holds. `LocalChaseYaw` / `RemoteChaseYaw` follow the client's current seat, driver or passenger.
- **`TruckVehicle.IsAboard(CrewMember)`:** seated in any of its seats; or the member's capsule centre inside the `TruckCargo` trigger box, tested in the trigger's local space, **only while the truck is under `cargoAboardMaxSpeed` (2 m/s)**. There is no platform riding for players: at speed only the two seats carry people (decision 5).

### 6.4 Ram sweep (`TruckRam`, plain serialized class inside TruckVehicle, like CrewBumper)
Host only, every FixedUpdate while `|v| > ramMinSpeed`:
1. **Oriented sweep:** `Physics.BoxCastNonAlloc(center, halfExtents, dir, hits, orientation, distance, mask, QueryTriggerInteraction.Ignore)` with `center = transform.TransformPoint(Hull.center) - dir * ramSweepExtra`, `halfExtents = Vector3.Scale(Hull.extents, transform.lossyScale)`, `orientation = rb.rotation`, `dir = velocity.normalized`, `distance = v * dt * 1.5 + 2 * ramSweepExtra`. Plus `Physics.OverlapBoxNonAlloc` of a slab `ramFrontSlab` deep on the hull's face in the direction of travel, for what already touches the bumper (a box cast never reports what it overlaps at its start). Excluded: layers Debris, Ignore Raycast, Crew and NPC, triggers, the truck's own colliders, bodies in `TruckCargo.inside`, and bodies held by a player (`MovableObject.holder != null`, never flung).
2. Per hit, with a per-collider cooldown `ramCooldown`:
   - **Built target** (`DestructibleModule` via `TryGetChunk` or its collider, structural `Breakable`, `GlassPane`): `ImpactDamage.Evaluate` with `strikerMass = rb.mass`, `receiverAnchored = true`, `receiverMass = structureReferenceMass` (a pane: its mass), `v = dot(vel, -hit.normal)`, `type = Vehicle`; `ApplyDamage` with the spread radius, instigator `DriverActor`, `speed = v`. Remember the collider for `TruckRam.Handled` (0.2 s). Before the first built hit of a step, `DebrisManager.ExpectStructure(8)`.
   - **Dynamic prop** (Rigidbody, MovableObject or not) under `flingMassKg`: impact damage to it (`receiverMass` = its mass, type Vehicle), then its velocity set to `vel * 1.1 + lateral * flingLateralShare * |vel| + up * flingUpShare * |vel|`, capped `flingMaxSpeed` (35 m/s, 1.4 x the governor), where `lateral` points away from the truck's centre line on the side of the hit point. The prop's colliders join `Handled` for `flingHandledSeconds` (1 s), so it is not flung again. Heavier bodies are left to physics.
3. The same sweep again. If nothing built is left in the path (the fence is gone, the chunks under the bumper are out), the truck keeps going minus a toll: `v' = v * sqrt(max(0, 1 - E_abs / KE))`, `E_abs = ramJoulesPerHp * sum(DamageResult.applied)` (HP actually removed, capped per target or chunk). It is written to `rb.linearVelocity` before the solver step, so the static contact never happens. If something built is still there, nothing is changed and physics stops the truck against it.
4. `DebrisManager.WakeInBounds(swept box)` in the same pass, so frozen debris is dynamic before the contact.
5. Contacts the sweep missed fall back to the receivers' own `OnCollisionEnter`, which now classify the truck as `Vehicle` and blame the driver. `ImpactDamage.TryMeasure` returns false when `TruckRam.Handled(contact.thisCollider)` is true for any contact of the collision, so nothing is counted twice.
- **The grandmother:** `CrewBumper` also covers her: a truck about to hit her calls `GrandmaMover.Knock(velocityChange, driver)` (she staggers aside). She perceives `RunOver` when the closing speed is at least `runOverMinSpeed` (3 m/s), otherwise `Bumped` (section 7).
- **Police cars:** dynamic bodies, rammed by physics; a hit over their stun impulse stuns them (section 8.5).

### 6.5 What takes part now (item 5 inventory)

| Object (Map01) | Today | After DEV 2 |
|---|---|---|
| 46 fence sections, 4 gate leaves | Breakable, 140, but stop the truck dead | break in the sweep, truck goes through |
| `PKX_Fence_Post` x2 at the scene root (8.96, -14.96) and (17.99, -14.98) | indestructible (outside `GrandmaHouse_PierreKit`) | reparented under `GrandmaHouse_PierreKit/Garden` (Unity stage, with Pierre): Wood 120 |
| `Hedge_00..13` (PF_Hedge) | indestructible | yard, Plant 90 (new `BreakMaterial.Plant`: durability 60, vMin 1.5, density 300, cover 0.8, Wood sound) |
| `Bush_00..07` | indestructible | yard, Plant 60 |
| `Mailbox` | indestructible | yard, Metal 80 |
| `Birdhouse`, `Trellis`, `Scarecrow` | indestructible | yard, Wood 40, 60, 60 |
| `Vegetable_Bed_0..3`, `Garden_Bench` | indestructible | yard, Wood 120, 150 |
| walls, gables, quoins, chimney stack | one chunk at most per truck hit | spread hits (3.5) |
| `Grandma_Car` 1100 kg | pushed, never damaged | unchanged (decision for Pierre) |
| trees (garden and country trunks), rocks, well, fountain, floors, foundation | solid | stay solid |
| country hedgerows, bushes, stones | no collider | unchanged |

---

## 7. Grandmother reliability (item 7)

Fixes, one per audit problem (GRANDMA files only unless stated):
1. **P1/P2, warning escalation:** `GrandmaMood.Apply(float cost, in Stimulus s)` (single caller). During the warning, only a stimulus with `Actors.IsPlayer(s.instigator)`, a kind other than SmallNoise and BehindSchedule, `cost >= warningOffenceMinCost` (2) and `Time.time >= warningStart + warningGraceSeconds` (3) ends it and calls the police. Anything else costs nothing during the warning.
2. **P3, once per run:** `lastWarningsPerRun` (1). When used up, patience 0 fires `ReachedZero` at once. After a survived warning patience returns to `lastWarningRecover` (10) as today.
3. **P5, the cap:** `maxLossPerWindow` applies to breakage kinds and SmallNoise only (`capOnlyBreakage`). Offences always pay full price. Priority is computed from the priced cost, not from what was lost, and `GrandmaNoticed` is raised for every priced stimulus (magnitude = lost, may be 0). The HUD toasts `GrandmaNoticed` only when `magnitude > 0` (HUD: `ToastFeed.cs`, `HudToasts.cs`).
4. **P6, the queue:** 64 entries; when full, the newcomer replaces the lowest-priority entry if it outranks it. Explosion and TheftWitnessed are never dropped.
5. **P4, readable warning:** the batch that starts the warning skips its `Respond`. `GrandmaSpeech.Say` gains a held flag: LastWarning, Police and OnThePhone lines cannot be replaced by an ordinary line while shown. `WorldEventType.GrandmaLastWarning` is raised (forwarded online, drives the toast and the HUD chip).
6. **P7, structure:** new stimulus `StructureBroken` from `StructureDamaged` with state Destroyed and from `StructureCollapsed`, default loudness 0.8, cost `structureBroken` 6, per-instigator cooldown `structureCooldown` 3 s. `StructureBroken` from `StructureDamaged` is a breakage kind (under the loss cap); from `StructureCollapsed` it pays full price. New stimulus `GardenBroken` from `GardenDamaged` (3.12): cost `gardenBroken` 1.5, breakage kind, line "My roses!". Optional `ObjectDamaged` cost 1.5 for her own things cracked.
7. **P9, hearing:** `explosionHearing` 1 (the deafness factor does not apply to explosions: 30 m). The Perceive default loudnesses move to `MoodCosts`.
8. **P8, the truck:** truck damage is blamed on the driver (IMPACT, 2.1). `GrandmaMover.Knock(velocityChange, by)` perceives `RunOver` (cost `runOver` 10, cooldown 3 s, line "Are you trying to run me over?!") when `|velocityChange|` is at least `runOverMinSpeed` (3 m/s, in `MoodCosts`), else `Bumped` (2, today's cost).
9. **P10, wearing:** a seen player wearing one of her theft targets for `carryWitnessSeconds` is witnessed like carrying.
10. **P17:** no Goodbye after an escape (`Session.Escaped`). On `PoliceArrived` she says "They went that way, officer!".
11. **Debug** (offline, registered through DebugCommands, listed by F12): Keypad 7 patience to 5; Keypad 8 force the police call.

Deterministic ladder: Sweet (70+), Annoyed, Angry (hurries), Furious (confronts), then at 0: the one warning (25 s, shown), then the call. Balance coupling: she now hears explosions farther and notices walls and the truck, while the truck and grenades destroy more. Retune `GrandmaMoodTable.asset` in the same playtest.

---

## 8. Reusable mission system: police, flee, checkpoint (item 8)

### 8.1 Types, phases, session plumbing
- `MissionPhase { Job, PoliceIncoming, PoliceHere }` on `Session.Phase`. No new `SessionState`: `IsRunning` is read in 8 files and the run stays InProgress until Completed or Failed.
- `FailReason` gains `Intercepted` and `CrewArrested` (appended).
- `Session.ArrestedMask` (bit per crew member), `Session.ArrestPendingMask` (members an officer zone or car is about to arrest) and `Session.Escaped` (Completed through the exit).
- **Flow:** GrandmaCalledPolice. If the scene has an `EscapeMission` and `policeEndsRun` is false:
  - **If `Session.State == Intro`:** `HideIntroCard()` and `StartContract(Actors.World, false, "", "")` first, so the flee always runs in InProgress (IsRunning true, the HUD past "keys first", nobody frozen by the card).
  - Phase PoliceIncoming, `policeAt = now + policeCountdown` (arrival time of the lead car). Otherwise today's path (Fail after `policeCountdown`), which Tutorial_01 and scenes without markers keep.
  - At arrival: PoliceHere, `PoliceArrived`, `fleeUntil = now + fleeTimeLimit` (90 s). At `fleeUntil`: `Fail(Intercepted)` with the settlement line "The police surrounded the house: the contract is void".
  - Delivery is refused once the police are called (as today).
- **The mission clock stops** at the call (`stopClockOnPolice`): the host's `TickClock` and the client's `TickReplicaClock` both return while `Session.Phase != MissionPhase.Job && Numbers.stopClockOnPolice`.
- **Replication hooks** (MISSION, `GameSession`): `SessionSync.SendState(this)` on every Phase change, every arrest, every change of `ArrestPendingMask` or of the interception warning (start and stop edges only), and on Escaped. `ApplyReplica` writes Phase, ArrestedMask, ArrestPendingMask, the interception warning, `FleeLeft` and Escaped, and applies the arrest mutes, **before** the `if (r.state == Session.State) return;` early return (`GameSession.cs:451`).
- **Scene change:** `GameSession.OnDestroy` also resets `Session.Phase = MissionPhase.Job`, `ArrestedMask = 0`, `ArrestPendingMask = 0`, `Escaped = false`, next to the existing resets, so Tutorial_01 after a flee inherits nothing.

### 8.2 Scene markers (found by type with `SceneLookup`, never by name)
| Component | What it is |
|---|---|
| `EscapeMission` | on `_Systems`. Host authority. Owns dispatch, pursuit targets, arrests, interception, the flee timer and the checkpoint test. Optional per-map overrides of the countdown and the flee time. |
| `EscapeCheckpoint` | the exit: a BoxCollider **kept disabled** (it only gives the size and the gizmo; no trigger, so no query or ray ever hits it), on the Ignore Raycast layer. `Contains(p)` tests `transform.InverseTransformPoint(p)` against the box's center and size (an oriented box, not a world AABB). `Center`, `MarkerPosition`. One per scene is active. |
| `PoliceSpawn` | where the cars start: a Transform with the list of `PoliceCar`s parked at it and `stagger` seconds between departures. |
| `MissionRoute` | the police road: ordered segments, each a `RoadPath` span (`fromS`, `toS`) or the route's own child waypoints, sampled into one polyline (2 m step, XZ only; heights come from physics). `Length`, `Sample(d, lateral)`, `ProgressOf(position, hint)`. `arrival` (Transform): its projection is the arrival distance. Calls `BuildCenterline` on a segment that is not Ready. |
| `RoadAnchor` | on a marker or a parked car: in Start, snaps its position and yaw to `road.Sample(distance, lateral)` plus `CountryLand.HeightAt`, or to `HeightAt(x, z)` when no road is set. Deterministic on both machines, runs before the NetIds sweep (order 10000). |

A route-only `RoadPath` (for example the street) must never be added to `CountryLand.roads`, or the land draws asphalt over it.

### 8.3 Map01 default layout (Unity stage)
- **PoliceSpawn:** Road_West at s about 580, (-595, about 49, 38), facing east, behind the crest at s 550 (hidden from the yard, inside the land collider square).
- **Route:** Road_West s 580 to 0, then `Route_Street` (a standalone RoadPath, points (-29.5, 0, -23.6) and (50.5, 0, -23.6), not in `CountryLand.roads`), then Road_East s 0 to 360. **Arrival** marker in the street at (0, 0, -23.6).
- **EscapeCheckpoint:** Road_East at s about 320, (261.8, about 3.9, 194.7), yaw 31, 14 x 8 x 10 m. Flat (no climb), in the wood corridor, 351 m from the parked truck. Option after the climb is measured: s about 660 over the crest (691 m).
- **Cars:** 2 placeholder `Assets/BrokenVector/LowPolyCarPack/Prefabs/Policecar.prefab` instances parked at the spawn, each with a non-kinematic Rigidbody (1400 kg, ContinuousDynamic, interpolate), a BoxCollider, `PoliceCar` and `RoadAnchor`. Active at load, so NetIds registers them as Bodies and the transform stream carries them.

### 8.4 Timings and rules (`GameLoopNumbers`, "Police and escape" block)
| Field | Default |
|---|---|
| `policeEndsRun` | false (true: today's rule) |
| `policeCountdown` | 75 s, call to arrival of the lead car |
| `fleeTimeLimit` | 90 s from arrival; at 0 the house is surrounded (failed) |
| `stopClockOnPolice` | true |
| `policeCarCount` (cars used, at most the parked ones) / `policeStagger` | 2 / 2 s |
| `policeCruiseKmh` / `policeChaseKmh` / `policeAccel` / `policeBrake` | 80 / 100 / 6 / 9 m/s^2 |
| `overtakeOffset` / `roadblockLead` / `roadblockYaw` | 2.5 m / 10 m / 70 degrees |
| `offRouteChaseRange` | 60 m (a target off the route but this close: straight pursuit) |
| `interceptRadius` / `interceptTruckMaxKmh` / `interceptSeconds` | 6 m / 8 / 4 s |
| `arrestRadius` / `arrestCarMinSpeed` | 1.8 m / 2 m/s (hit by a moving car) |
| `officerZoneRadius` / `officerZoneAfterStop` / `officerZoneSeconds` | 5 m / 1 s stopped / 2 s continuous inside |
| `policeStunImpulse` / `policeStunSeconds` | 15000 N.s / 3 s |
| `finePerArrest` | 500 |
| `escapeCargoPayFraction` | 0.5 (goods sold off the back of the truck go at half price) |
| `minCrewAboard` | 1 |
| `defaultTimeLimit` | 1200 (was 600) |

Dispatch: the lead car leaves at `callTime + policeCountdown - routeTravel`, where `routeTravel = (arrivalS - spawnS) / cruise + cruise / (2 * accel)`, clamped at 0; followers `policeStagger` later. Lights and siren on from departure.

### 8.5 Police cars (`PoliceCar`)
- **Driving (host):** a force-steered dynamic body, no WheelColliders, no NavMesh. Each FixedUpdate: a look-ahead point on the route (8 m + 0.4 s x speed) with a lateral offset; horizontal velocity driven toward `targetSpeed * dir` within `policeAccel` / `policeBrake`; yaw turned toward the velocity (max 90 degrees per second); an upright torque. Gravity and colliders give the height.
- **Blocking:** a forward BoxCast of 0.8 s at speed: another body on the lane slows it and shifts its lateral offset by `overtakeOffset`. The crew truck, when **stopped or slow** (under `interceptTruckMaxKmh`), makes it stop at `interceptRadius` behind it (interception).
- **Overtake and roadblock:** when the target is the crew truck moving faster than `interceptTruckMaxKmh`, the forward cast ignores the truck; the car shifts its lateral offset by `overtakeOffset` to the free side and drives at chase speed (100 km/h against the truck's 90) to the truck's route progress plus `roadblockLead`. Once ahead by that much, it brakes to a stop turned `roadblockYaw` across the lane: a roadblock (officer zone once stopped). The truck rams it (stun, 1.4 t against 3.5 t) or drives round it; a truck that slows down next to it is intercepted.
- **PoliceHere targets** (every 0.25 s): the truck if any crew is aboard or it moves over 2 m/s, otherwise the nearest crew member on foot; the second car prefers the other target. On the route: as above. **Off the route:** a target more than 12 m off the route but within `offRouteChaseRange` (60 m) of the car: the car leaves the route and steers straight at it (force steering needs no NavMesh), with the stuck recovery below. Farther: the car goes to the nearest route point and stops there (officer zone); the flee timer (8.4) still runs, so hiding off route fails within `fleeTimeLimit`.
- **Stun:** a collision from the crew truck with impulse over `policeStunImpulse`: no drive for `policeStunSeconds` (lights stay on). Tilted over 60 degrees for 2 s, or stuck under 1 m/s for 4 s while it wants to move: placed back on the route at the nearest point behind, `NetTransforms.Snap`.
- **Lights and siren (both machines, local):** a light bar built in Awake: two emissive unlit quads alternating red and blue at 2 Hz plus one additive flare billboard per colour (the `ExplosionFX` flare style). **No `Light` component** (in Built-in forward a pixel light adds a pass to every renderer in range, next to a 25k-draw-call house). A spatial `AudioDirector.StartLoop(SfxKind.SirenLoop, transform, ...)`. On when `IsDispatched`: on the host from the dispatch, on the client once the replicated car has moved more than 1 m from its parked pose while Phase is not Job. Unlit and emissive, so it reads through the fog.

### 8.6 Arrest, interception, success (host, `EscapeMission`)
- **On foot:** `TruckVehicle.IsAboard` false (6.3).
- **Arrest:** a crew member on foot within `arrestRadius` of a car moving faster than `arrestCarMinSpeed` (immediate: the car itself is the on-screen warning), or inside the officer zone of a car stopped for `officerZoneAfterStop`, continuously for `officerZoneSeconds` (2 s; the member's bit is in `ArrestPendingMask` meanwhile, and the HUD shows it). Then `GameSession.Arrest(member)`:
  - host: `Session.ArrestedMask` bit, `member.Input.Muted = true`, `CrewArrested` event, State sent at once;
  - `NetPlayerDriver` on the host stops applying P2's InputPose positions once P2 is arrested (the body stays at the host's last pose), so the two screens agree;
  - client: `ApplyReplica`, for each ArrestedMask bit that turns on, mutes `CrewRoster.Get(i).Input` locally: that is how the client stops its own body (`Net.LocalMember`);
  - `GameSession.Holds(m)` includes `Session.IsArrested(m.index)`, so closing the pause menu (`HudPauseMenu.cs:166`) or the client's pause (`RemoteInputSource.cs:87`) never unmutes an arrested player;
  - the body plays the knocked-down clip and holds it (face down, `CrewAnimator`). An arrested member stays arrested for the run.
- **Interception:** a car within `interceptRadius` of the truck hull while the truck's speed is under `interceptTruckMaxKmh`, `interceptSeconds` (4 s) in a row, in PoliceHere: the warning is on (`InterceptLeft` counts down, HUD 8.7) from the first second; at 0 `TruckIntercepted`, `Fail(Intercepted)`.
- **All arrested:** every crew member arrested: `Fail(CrewArrested)`.
- **Success:** the truck's centre of mass inside `EscapeCheckpoint.Contains` with at least `minCrewAboard` non-arrested member aboard: `EscapeReached`, `GameSession.CompleteEscape`. Members not aboard at that moment are counted as arrested for the fine.
- **Settlement (`Settlement.ForEscape`), paid at most about half a clean delivery:**
  - "You got away: the contract is void" (0).
  - "Sold from the truck: N" = `escapeCargoPayFraction` x (what `ForDelivery` would pay for the list items in the truck, intact at contract value and damaged at `damagedPayFraction`, plus the unseen theft worth of her things in the truck or in the pockets of members aboard).
  - Her things she **witnessed** being taken are confiscated: the existing "Confiscated: N she saw you take" line, 0, and no fine (the police have them).
  - The existing "Billed: N on the list destroyed" line at full value, and the existing "Break-in (...)" line when the contract started by a break-in.
  - "Fine: N arrested" (-`finePerArrest` x N).
  - `ProjectedEscape` for the HUD money during the flee.
  - Failures: "The police stopped the truck: the contract is void", "The police surrounded the house: the contract is void", "Everyone was arrested: the contract is void", each followed by the existing "Her things you had to leave" line.
- **Normal delivery** stays the success path when the police are never called.

### 8.7 HUD, markers, sound
- **Clock slot:** PoliceIncoming "POLICE 1:12" (existing `ClockKind.Police`); PoliceHere "ESCAPE 1:30" counting down `FleeLeft` (`ClockKind.Flee`), red under 20 s.
- **Banner:** PoliceIncoming "POLICE IN {0}: load up and head for the exit"; PoliceHere "THE POLICE ARE HERE: DRIVE TO THE EXIT"; during the grandmother's warning "LAST WARNING {0}" (red, pulsing), also a chip on her mood view.
- **Failure warnings (every failure announced at least 2 s ahead):** interception "BLOCKED! {0}" (red, 4.. 3.. 2.. 1, from `InterceptLeft`) plus a siren whoop; arrest pending: a shrinking red ring around that player's crosshair with "CAUGHT" (from `ArrestPendingMask` and `officerZoneSeconds`, animated locally from the edge); the escape clock under 20 s; each arrest toasts, so "all arrested" is preceded by the last arrest's ring.
- **Markers:** `IndicatorKind.Exit` (flag icon) from PoliceIncoming until the end, shown also to the driver and the passenger, exempt from `ringMaxDistance` (ring or edge arrow at any distance). `IndicatorKind.Police` (car icon) for dispatched cars.
- **Toasts:** police here, arrested (name), intercepted, got away, last warning. `GrandmaNoticed` only when its magnitude is over 0.
- **End card:** "YOU GOT AWAY" on an escape; reasons for Intercepted (stopped or surrounded) and CrewArrested.
- **Sound:** the 2D siren bed grows over the real countdown (not 20 s) and stops at session end; the cars' spatial loops take over at arrival. The win jingle plays on an escape.

### 8.8 Loc strings (HUD track, `UI/Loc.cs`)
| Key | FR | EN |
|---|---|---|
| banner.policeIncoming | LA POLICE ARRIVE DANS {0} : chargez et filez vers la sortie | POLICE IN {0}: load up and head for the exit |
| banner.flee | LA POLICE EST LÀ : FONCEZ VERS LA SORTIE | THE POLICE ARE HERE: DRIVE TO THE EXIT |
| banner.blocked | BLOQUÉS ! {0} | BLOCKED! {0} |
| banner.lastWarning | DERNIER AVERTISSEMENT  {0} | LAST WARNING  {0} |
| hud.caught | ATTRAPÉ | CAUGHT |
| flee.short | FUITE | ESCAPE |
| marker.exit | SORTIE | EXIT |
| mood.lastWarning | Dernier avertissement | Last warning |
| toast.lastWarning | Mamie : dernier avertissement ! | Grandma: last warning! |
| toast.policeHere | La police est arrivée ! | The police are here! |
| toast.arrested | {0} s'est fait arrêter ! | {0} got arrested! |
| toast.intercepted | La police a bloqué le camion ! | The police stopped the truck! |
| toast.escaped | Vous êtes passés ! | You made it out! |
| end.escaped | VOUS AVEZ FILÉ | YOU GOT AWAY |
| end.intercepted | La police a arrêté le camion. | The police stopped the truck. |
| end.surrounded | La police a encerclé la maison. | The police surrounded the house. |
| end.arrested | Toute l'équipe s'est fait arrêter. | The whole crew was arrested. |
| settle.escapeVoid | Vous avez filé : contrat annulé | You got away: the contract is void |
| settle.escapeCargo | Vendu depuis le camion : {0} | Sold from the truck: {0} |
| settle.arrestFine | Amende : {0} arrêté(s) | Fine: {0} arrested |
| settle.voidIntercepted | La police a arrêté le camion : contrat annulé | The police stopped the truck: the contract is void |
| settle.voidSurrounded | La police a encerclé la maison : contrat annulé | The police surrounded the house: the contract is void |
| settle.voidArrested | Tout le monde a été arrêté : contrat annulé | Everyone was arrested: the contract is void |
| seat.ride | Monter | Ride |
| intro.patience (rewritten) | Casse, bruit, lenteur : elle perd patience. À bout, elle appelle la police, et il faudra filer avec le camion. | Break things, make noise, dawdle: she loses patience. At the end of it, she calls the police, and you run for it with the truck. |

Grandmother lines (GRANDMA track, `GrandmaLines`): PoliceArrived "Ils sont partis par là, monsieur l'agent !" / "They went that way, officer!"; StructureBroken "Mes murs !" / "My walls!"; GardenBroken "Mes rosiers !" / "My roses!"; RunOver "Vous voulez m'écraser ?!" / "Are you trying to run me over?!".

---

## 9. Mission timer (item 9)
`ContractManager.timeLimit` 900 to 1200 in Map01 (Unity stage, editor), `ContractManager.cs` default 600 to 1200, `GameLoopNumbers.defaultTimeLimit` and `GameLoopTuning.asset` 600 to 1200. The schedule check scales by itself (13 checks instead of 10).

---

## 10. Online replication (ADR-012, host-authoritative)

| New state, body or event | Simulated | Reaches the client through |
|---|---|---|
| Energy damage, spread, propagation, rubble decision, ram sweep, fling | host only (existing gates) | existing Breakable / Glass / Structure records |
| Chunk launch velocity | host | `ChunkDetached` v and w (existing); both machines stamp `launchedAt`, and `PushBodies` / `PlayCosmetic` skip a chunk only within `structureLaunchGrace` of its launch |
| Chunk rubble | host decides | `ChunkDetached` flags value `4` = shattered, `(count & 15) << 4` = rubble pieces; the client shatters locally (its own budget, whole slab on refusal) |
| Deferred detach | host queue; client queue of its own | the record is written when the chunk really detaches; the client's `NetDetach` reserves and defers like the host (3.8) |
| Join snapshot up to 32 chunks | host | `ModuleSnapshot` v2: `ulong attached, ulong looks, byte fixtures` |
| Wall hit feedback that removes nothing, and shakes over 50 kJ | host | new Props op 6 `ImpactFx` (U): pos 12, strength unit8, size unit8 (0 = shake only); chunk removals are fed back by `NetDetach` only (3.9) |
| Driving model, auto-right | host | unchanged (TruckSync State, transform stream) |
| Passenger seat | host | Truck SeatEnter / SeatExit / SeatNotice + seat index u8; `OnPeerLeft` frees every seat of the client |
| Grandmother fixes, warning, RunOver, Knock, GardenBroken | host | GrandmaSync Mood and Flags (unchanged), her pose on the transform stream, `GrandmaLastWarning` and `GardenDamaged` forwarded |
| Phase, countdown, flee time, arrested and pending masks, interception warning, escaped | host (`GameSession`, `EscapeMission`) | Session State, sent on every edge (8.1): + phase u8, + arrested mask u8, + pending mask u8, + intercept tenths u8 (255 none), + flee seconds float (-1 none); flags value `8` = escaped; `policeRemaining` = seconds to arrival. Applied before the early return of `ApplyReplica`. |
| Arrest freeze on the client | host decides | `ApplyReplica` mutes the arrested member locally; the host ignores P2's InputPose positions once arrested; `Holds` keeps the mute through pauses; `CrewAnimator` reads `Session.ArrestedMask` |
| Police cars | host (FixedUpdate gated) | scene-placed active non-kinematic bodies: NetIds Body, transform stream; lights and siren derived locally |
| Settlement of an escape | host | SettlementLine (English shapes of 8.6) + SettlementEnd flags (value `1` completed, `2` escaped) |
| New WorldEvents (GrandmaLastWarning, PoliceArrived, CrewArrested, TruckIntercepted, EscapeReached, GardenDamaged) | host | WorldEventRelay (automatic); subjects: CrewMember (Crew), the truck (Truck), a car's Rigidbody (Body), a yard Breakable |
| Debris lifecycle, pool | each machine | local, unchanged; pool shells invisible to NetIds (5) |

**`NetSession.Protocol` 1 to 2** (CONTRACTS). Both builds must match. Offline: every new behaviour is host code that also runs offline (`Net.HasAuthority` true), every send is `Net.IsHost` gated, no new listener or Random offline beyond the gameplay itself.

**Bandwidth.** Reliable records stay small: a grenade detaching 10 to 15 chunks about 300 B; a 90 km/h ram through a wall about 400 B. **The transform stream is the real load:** every pushed or flung MovableObject is a tracked body at 20 Hz with no cap (`NetTransforms.HostTick`), and NETCODE_SLICE 4.4 already puts a floor collapse at about 60 KB/s against an 80 KB/s threshold. The online run (12) records NetStats peak and steady during (a) two grenades at one wall inside a furnished room and (b) a 90 km/h ram through the fence, the yard props and one house wall. Limit: peak under 80 KB/s. **Pre-authorised fallback,** owned by MISSION (`Net/NetTransforms.cs`) and applied at the UNITY stage only if that measurement fails: the NETCODE_SLICE 4.4 rule (bodies farther than 15 m from the client's camera at 10 Hz).

**Known risk, fixed now: fast bodies and `TeleportJump`.** `NetTransforms.cs:23/161` treats a jump over 3 m between two sent samples as a teleport. A car at 100 km/h moves 1.4 m per sample, so one skipped send pops it. MISSION makes the threshold speed-aware on the host: `3 m + |v| * 0.15 s` (v = the body's velocity); `Snap` stays explicit. Checked by the 100 km/h chase online.

---

## 11. Performance budgets (measured with the F3 overlay and the Profiler, split screen)

| Budget | Value | Counter |
|---|---|---|
| Destruction cost per frame, host and client | at most 16 ms, worst of the blast frame and the 5 frames after it (deferred detaches, rubble, client `NetDetach` bursts) | `Explosion.LastFrameBlastMs` (blast frame) and the new `Explosion.MaxFrameMsAfterBlast` (worst frame time over the 5 frames after a blast minus the median of the 30 frames before it) |
| Wall fracture swap | at most 3 ms per wall with the pre-cooked colliders | `DestructibleModule.LastFractureMs` |
| Ram sweep | at most 0.2 ms per step | Profiler |
| Police system (2 cars + mission) | at most 0.2 ms per frame | Profiler |
| Debris | 450 dynamic, 300 frozen, 90 spawns (reserve only when chunks are expected) and 30 destroys per frame | `DebrisManager.Count`, `SpawnedLastFrame` |
| Draw calls | after two grenades and a truck ram, split screen: at most +3k draw calls over the same view before, and 60 fps held; raise `maxFrozenPieces` in data only if this holds with margin | Stats / F3 |
| Police lights | read the draw-call change at the arrival point (no pixel light) | Stats |
| Frame target | 60 fps in the greybox on our machines (CLAUDE.md 11) | |

---

## 12. Test checks (short, Pierre wants speed)

**Offline, Map01 split screen, one pass:**
1. Walls: Shift+F9 on the floor 0.25 m, 1.0 m and 1.5 m from an exterior plain wall (F11 between): section 1 shares, rubble at 0.25 m, no chunk vanishes. Throw five grenades at a plain exterior wall from 5 m: section 1 thrown target. One grenade in the middle of a 3 m room, one 0.25 m from one wall (the opposite wall: at most 2 chunks). `LastFrameBlastMs` and `MaxFrameMsAfterBlast` at most 16. A capture of the re-fractured wall for Pierre.
2. Throws: sofa standing and sprint, piano, against a plaster wall and a fence: 2.2 outcomes.
3. Drive: 0 to 50 and top speed on the street (DriveView), brake from 90 on Road_East.
4. Ram a fence at 30 km/h, a wall at 10, 25, 40 and 90 km/h, a chair at 90: section 1 outcomes; F7 log shows the driver as instigator.
5. Keypad 8 (police call), keep looting, drive to the exit: YOU GOT AWAY and about half the truck's delivery value. Again, stay parked in the street: "BLOCKED!" then intercepted. Again, park off the route: failed at the end of "ESCAPE". Again, stand in the street on foot: the "CAUGHT" ring, arrested, the second player escapes with a fine.
6. Keypad 7 then one theft: the warning shows 25 s; a second offence calls the police; a noise does not.
7. Draw calls after two grenades and a ram (section 11). Console: 0 errors. Tutorial_01 still plays after a flee on Map01.

**Online:** the loopback test of NETCODE_SLICE 14 on two fresh builds (protocol 2), then one manual host plus client run: the client drives at 90 km/h, then rides as passenger (no pose fight in `net_host.log`), the client is arrested (cannot move on either screen, opening and closing the pause menu does not free it), a 100 km/h chase shows no pops, NetStats peak during check 1's grenades and check 4's 90 km/h ram under 80 KB/s, and the NetIds counts match with the pool on and off.

---

## 13. FROZEN CONTRACTS

Written by the CONTRACTS stage, compiled at 0 errors, then frozen. Tracks build against them and never change them. All in namespace `Movers`, paths under `UnityProject/Assets/_Movers/Scripts/`. Wire flags are given as values, not bit numbers.

```csharp
// ===== Core/DamageEvent.cs =====
public enum DamageType : byte { Blast, Impact, Thrown, Fall, Crush, Tool, Vehicle }   // Vehicle appended

public readonly struct DamageEvent
{
    // existing fields unchanged; radius now reads: Blast = blast radius; any other type = impact spread (m), 0 = point hit
    public readonly float speed;   // m/s. Impacts: the striker's approach speed along the normal. Blasts: the eject speed
                                   // at this point (BlastSolver). 0 = unknown (old callers): receivers fall back to impulse/mass.
    public DamageEvent(Vector3 position, Vector3 direction, float damage, float impulse, float radius,
                       DamageType type, int instigator = Actors.World, Object sourceObject = null, float speed = 0f);
    public DamageEvent With(float newDamage, float newImpulse, Vector3 at, Vector3 dir);   // keeps speed
    public DamageEvent WithSpeed(float newSpeed);
}
// DamageResult (existing): applied = HP actually removed, capped at each target's or chunk's remaining health,
// summed over the chunks a spread hit reached. Unchanged fields.

// ===== Core/SessionState.cs =====
public enum FailReason { None, TimeUp, PoliceCalled, Other, Intercepted, CrewArrested }   // appended
public enum MissionPhase : byte { Job = 0, PoliceIncoming = 1, PoliceHere = 2 }
public static class Session
{
    // existing members unchanged
    public static MissionPhase Phase { get; internal set; }      // Job; reset at SubsystemRegistration, GameSession.Awake and OnDestroy
    public static byte ArrestedMask { get; internal set; }       // bit i: crew member i is arrested (reset likewise)
    public static byte ArrestPendingMask { get; internal set; }  // bit i: an officer zone is about to arrest member i (reset likewise)
    public static bool Escaped { get; internal set; }            // Completed through the exit checkpoint (reset likewise)
    public static bool IsArrested(int member);                   // member in 0..7 and its bit set
}

// ===== Core/WorldEvents.cs: WorldEventType, appended after PlayerDrinking =====
GrandmaLastWarning,   // her one warning began. instigator = worst offender, magnitude = seconds of warning
PoliceArrived,        // the lead police car reached the arrival point. subject = its Rigidbody
CrewArrested,         // instigator = the member, subject = the CrewMember
TruckIntercepted,     // the police stopped the truck or surrounded the house: the run fails. subject = the TruckVehicle
EscapeReached,        // the truck entered the exit with crew aboard. value = the payout
GardenDamaged,        // a yard piece (hedge, bush, mailbox...) broke. subject = its Breakable, magnitude = DestructionState
// and the comment of GrandmaCalledPolice becomes: "her patience ran out: the police are coming (EscapeMission) or the run fails"

// ===== Destruction/DestructionEvents.cs (CONTRACTS writes it in full) =====
public static void Garden(Object subject, Vector3 at, int instigator);   // raises GardenDamaged, magnitude Destroyed

// ===== Destruction/BreakMaterial.cs =====
public enum BreakMaterial { Glass, Ceramic, Plastic, Wood, Fabric, Metal, Stone, Plaster, Brick, Concrete, Plant }  // Plant appended

// ===== Destruction/Breakable.cs (field added by CONTRACTS; IMPACT uses it, WALLS sets it) =====
public bool isYard;   // a garden piece: its destruction raises DestructionEvents.Garden, never Structure

// ===== Destruction/DestructionMaterialTable.cs: new fields (code default = chosen tuning) =====
// Impacts
public float energyDamageScale = 27f;          // replaces impactDamageScale (kept, unused after DEV 2)
public float structureReferenceMass = 35f;
public float minMassRatio = 0.1f;
public float maxMassRatio = 12f;
public float vehicleMaxMassRatio = 200f;
public float impactPushFactor = 0.5f;
public float impactSpreadPerCubeRootKJ = 0.3f;
public float impactMaxSpread = 2.5f;
public float heldSpeedAllowance = 1.5f;
public float heldMaxMassRatio = 1f;
public float cargoSpeedAllowance = 3f;
public float cargoMaxMassRatio = 1f;
public float glassMinMassRatio = 0.5f;
public float glassDebrisMinMass = 2f;
public float impactMultiplier = 1f, thrownMultiplier = 1f, fallMultiplier = 1f, crushMultiplier = 0.5f,
             toolMultiplier = 1f, vehicleMultiplier = 0.01f;
public float structureChunkStrikeFactor = 0.1f;
public int debrisStrikesPerFrame = 20;
// Blast
public AnimationCurve structureFalloff = DefaultStructureFalloff();   // keys (0,1)(0.5,0.95)(1,0.7)(1.5,0.35)(2,0.15)(3,0.03)(4,0)
public bool focusScalesWithPower = true;
public float blastExponent = 1.3f;
public float blastDamageImpulse = 40f;
public float pushImpulse = 900f;
public float pushUplift = 0.8f;
public float maxLaunchSpeed = 28f;
public float lightDebrisLaunchFalloff = 0.5f;
public float maxSpin = 7f;
public float playerReach = 1.2f, knockHorizontal = 10f, knockUp = 4.5f, knockedDownSpeed = 2f, concussion = 0.45f;
public float shakeReach = 3f, shakeMaxStrength = 2f;
public float grenadeRadius = 6.5f, grenadePower = 1f;
public float blastChunkEjectSpeed = 10f;
public float breachRadius = 0.8f;
// Wall chunks
public Vector2 chunkHealthClamp = new Vector2(0.7f, 1.3f);
public bool fractureOnAccumulated = true;
public float overflowShare = 0.35f;
public float rubbleOverkill = 1.5f;
public Vector2Int rubblePieces = new Vector2Int(3, 6);
public float impactChunkLaunchShare = 0.7f;
public float chunkLaunchReferenceMass = 150f;
public Vector2 chunkLaunchMassFactor = new Vector2(0.4f, 1.5f);
public float chunkUpBias = 0.25f;
public Vector2 chunkSpin = new Vector2(2f, 5f);
public float debrisPhysicsMassCap = 150f;
public float structureLaunchGrace = 0.3f;
// Debris (existing lifetimes: code defaults become structure 60, props 40, glass 20)
public int maxDynamicPieces = 450, maxFrozenPieces = 300, maxSpawnPerFrame = 90, structureReservePerFrame = 30, destroyPerFrame = 30;
public float lifetimeJitter = 0.25f, cleanupMinDistance = 12f, hardLifetimeFactor = 2f, cleanupShrinkTime = 1.5f,
             overflowShrinkTime = 0.6f, freezeAfterSleep = 3f, explosionWakeSpeed = 8f, smallPieceNoShadow = 0.3f,
             frozenNoShadowBelow = 0.5f;
// static helpers
public static AnimationCurve DefaultStructureFalloff();
public static float TypeMultiplier(DamageType type);          // the per-type multiplier above (Blast: 1)
public static float Focus(float d);                            // existing: now the curve (fallback structureFocus)
public static float Focus(float d, float power);               // d / cbrt(power) when focusScalesWithPower
// DefaultRow(Plant) = (durability 60, minSpeed 1.5, density 300, cover 0.8, ImpactAudio.Kind.Wood)

// ===== Destruction/ImpactDamage.cs =====
// FROZEN, unchanged: TryMeasure(Collision c, BreakMaterial mat, float myMass, in Rules rules, Vector3 fallbackPoint,
//                               Rigidbody myBody, out DamageEvent e)
//   myBody == null or kinematic means "receiver anchored". It returns false when TruckRam.Handled(contact.thisCollider)
//   is true for any contact of c.
// FROZEN, unchanged: Rules { speedAllowance, maxRatio, debrisMinMass }, Plain, Held, Glass, MaxRatio,
//                    HeldSpeedAllowance, HeldMaxRatio, GlassDebrisMinMass, BlameSeconds.
// Added by CONTRACTS:
//   Rules.minRatio (float; <= 0: the table's minMassRatio). Glass sets it to the table's glassMinMassRatio.
//   public static Rules Cargo { get; }   // cargoSpeedAllowance, cargoMaxMassRatio, crushMinMass
public struct ImpactInput
{
    public float normalSpeed;       // m/s, >= 0
    public float strikerMass;       // kg of the other side; float.PositiveInfinity when static or kinematic
    public float receiverMass;      // kg: its own body, a pane's mass, or structureReferenceMass for a built piece
    public bool receiverAnchored;   // built piece: infinitely heavy in the reduced mass
    public BreakMaterial material;
    public DamageType type;
    public float speedAllowance;    // m/s added to the material's minimum speed
    public float minMassRatio;      // <= 0: the table's minMassRatio
    public float maxMassRatio;      // <= 0: the table's (vehicleMaxMassRatio for Vehicle, else maxMassRatio)
    public float strikeFactor;      // <= 0: 1. structureChunkStrikeFactor for a launched wall chunk
}
public struct ImpactOutcome
{
    public float energy;            // J, 0.5 * mu * v^2
    public float effectiveSpeed;    // m/s
    public float damage;            // HP before the receiver's material factor
    public float spread;            // m (0 for Blast)
    public float push;              // N.s
    public bool Hurts { get; }      // damage > 0
}
public static ImpactOutcome Evaluate(in ImpactInput input);   // CONTRACTS writes it in full; IMPACT wires it
public static float ReducedMass(float a, float b);            // PositiveInfinity-safe; 0 when both are infinite

// ===== Destruction/ImpactFeedback.cs (new; stub by CONTRACTS: Dust + Play; BLAST fills) =====
public static class ImpactFeedback
{
    public static float Strength01(float energyJoules);   // 0 at 1 kJ, 1 at 1 MJ, log10 scale
    // Host or offline: dust, a sound whose volume follows the energy, a shake over 50 kJ; sends Props ImpactFx
    // only when removedSomething is false, or with size 0 (shake only) over 50 kJ.
    public static void Hit(Vector3 at, float energyJoules, float size, ImpactAudio.Kind kind, int instigator,
                           bool removedSomething);
}

// ===== Destruction/Explosion.cs (stub by CONTRACTS; BLAST fills) =====
public static float MaxFrameMsAfterBlast { get; }   // worst frame over the 5 frames after the last blast, minus the pre-blast median

// ===== Destruction/DebrisManager.cs, DebrisPiece.cs, MeshShatter.cs (stubs by CONTRACTS; DEBRIS fills) =====
public static void ExpectStructure(int chunks);                  // DebrisManager; this frame's reserve = min(structureReservePerFrame, sum). stub: nothing
public bool TryReserveStructure(int wanted, out int granted);    // DebrisManager; stub: TryReserve
public bool structureChunk;                                      // DebrisPiece; a wall chunk its wall launched
public Component structureSource;                                // DebrisPiece; the DestructibleModule it came from
public float launchedAt = float.NegativeInfinity;                // DebrisPiece; Time.time of its launch (host RemoveChunk, client NetDetach)
public static List<GameObject> ShatterChunk(Renderer chunk, int pieces, float totalMass, Vector3 velocity,
                                            Vector3 point, float lifetime, int instigator);
                                                                 // MeshShatter; stub: Shatter(new[]{chunk}, ...)

// ===== Vehicles/TruckRam.cs (new; stub by CONTRACTS; TRUCK fills) =====
[System.Serializable]
public sealed class TruckRam
{
    public static bool Handled(Collider c);   // true: the ram sweep hit or flung this collider recently (stub: false)
}

// ===== Vehicles/TruckVehicle.cs (stubs by CONTRACTS; TRUCK fills) =====
public bool IsAboard(CrewMember m);                // stub: m != null && Driver == m
public static bool IsAtWheel(CrewMember m);        // in the driving seat. stub: m != null && m.IsDriving
// CrewMember.IsDriving keeps meaning "seated in the truck" (driver or passenger).

// ===== NPC/GrandmaMover.cs (stub by CONTRACTS; GRANDMA fills) =====
public void Knock(Vector3 velocityChange, int by);   // host: she staggers aside and perceives RunOver or Bumped. stub: empty

// ===== Gameplay/GameSession.cs (stubs by CONTRACTS; MISSION fills) =====
public bool Holds(CrewMember m);   // stub: Session.IsOver || IntroCardShowing || CrewReleasePending
                                   // MISSION adds: || (m != null && Session.IsArrested(m.index))
public float FleeLeft { get; }     // seconds of escape left in PoliceHere, else -1. stub: -1
public float InterceptLeft { get; }// seconds before interception while the warning is on, else -1. stub: -1
// PoliceIn (existing) keeps meaning seconds to the police's arrival.
// CONTRACTS switches the two copies of the release predicate to it:
//   UI/Hud/HudPauseMenu.cs:166   -> GameSession.Current == null ? Session.IsOver : GameSession.Current.Holds(member)
//   Net/RemoteInputSource.cs:87  -> caches `readonly CrewMember member` in the constructor
//                                   (member = input != null ? input.GetComponent<CrewMember>() : null; CrewInput sits on
//                                   the member, CrewMember.cs:40) and uses
//                                   GameSession.Current == null ? Session.IsOver : GameSession.Current.Holds(member)

// ===== Mission/EscapeCheckpoint.cs (new; stub by CONTRACTS; MISSION fills) =====
[RequireComponent(typeof(BoxCollider))]
public sealed class EscapeCheckpoint : MonoBehaviour
{
    public static EscapeCheckpoint Active { get; }   // the enabled one in the game scene, null when none
    public Vector3 Center { get; }                   // world centre of its (disabled) box
    public Vector3 MarkerPosition { get; }           // Center + 2 m up
    public bool Contains(Vector3 worldPoint);        // oriented box test in local space
}

// ===== Mission/PoliceCar.cs (new; stub by CONTRACTS; MISSION fills) =====
[RequireComponent(typeof(Rigidbody))]
public sealed class PoliceCar : MonoBehaviour
{
    public static IReadOnlyList<PoliceCar> All { get; }   // enabled cars of the game scene
    public bool IsDispatched { get; }                     // lights and siren on (both machines)
    public bool IsStopped { get; }                        // officer zone active (host truth; client: speed under 0.5 m/s)
    public Rigidbody Body { get; }
}

// ===== Net/NetSession.cs =====
public const ushort Protocol = 2;

// ===== Wire formats changed (owners write them, all behind Protocol 2); flags are VALUES =====
// Structure op 2 ChunkDetached: flags 1 fell, 2 vanished, 4 shattered, ((count & 15) << 4) rubble pieces
// Structure op 6 ModuleSnapshot: id u32, left unit8, state u8, attached u64, looks u64 (2 bits x 32), fixtures u8
// Props op 6 ImpactFx (new, U): pos 12, strength unit8, size unit8 (0 = shake only)
// Truck ops 2, 3, 4: + seat index u8 at the end
// Session op 1 State: + phase u8, + arrested mask u8, + pending mask u8, + intercept tenths u8 (255 none),
//                     + flee seconds f32 (-1 none) at the end; flags value 8 = escaped;
//                     policeRemaining = seconds to arrival
// Session op 5 SettlementEnd: the completed byte becomes flags (1 completed, 2 escaped)

// ===== Settlement English line shapes (MISSION writes, HUD parses in SettlementText) =====
// "You got away: the contract is void"                   -> settle.escapeVoid
// "Sold from the truck: {N}"                             -> settle.escapeCargo
// "Fine: {N} arrested"                                   -> settle.arrestFine
// "The police stopped the truck: the contract is void"   -> settle.voidIntercepted
// "The police surrounded the house: the contract is void"-> settle.voidSurrounded
// "Everyone was arrested: the contract is void"          -> settle.voidArrested
// Reused as they are: "Confiscated: N she saw you take", "Billed: N on the list destroyed", "Break-in (...)"
```

**Data asset classes.** Existing, gaining fields: `DestructionMaterialTable` (CONTRACTS), `GameLoopTuning` / `GameLoopNumbers` police block (MISSION), `GrandmaMoodTable` / `MoodCosts` (GRANDMA), `IndicatorTuning` exit and police colours (HUD), `DestructibleModuleCatalog` rows gain `bool yard` (WALLS). New: `TruckTuning : ScriptableObject` (`[CreateAssetMenu(menuName = "Movers/Truck Tuning", fileName = "TruckTuning")]`, fields of 6.1, TRUCK). Every new field's code default equals the tuning chosen here, so Tutorial_01 and a missing asset behave like the tuned game.

**Enum rule.** Values are appended only (they travel as bytes or size arrays). Track-internal enums (`StimulusKind`, `GrandmaLines.Line`, `IndicatorKind`, `IndicatorIcon`, `ClockKind`, `BannerKind`) are appended by their owner, before `Count` where there is one.

---

## 14. Tracks and file ownership

**Order.** CONTRACTS (one agent, commits first), then the 8 tracks in parallel worktrees from the CONTRACTS commit, then INTEGRATION (merges, no editor, 15.2), then UNITY (one agent, the only one with the editor). One owner per file. A change needed in another track's file is written as a request, not made. Each track: `bash tools/net/compilecheck.sh` from its worktree root at 0 errors before every commit, small conventional commits ending with the Co-Authored-By line, a gate list (every new `Net.*` gate with file and line) in its report.

| Track | Owns (edits) | New files |
|---|---|---|
| CONTRACTS | Core/DamageEvent.cs, Core/SessionState.cs, Core/WorldEvents.cs, Destruction/BreakMaterial.cs, Destruction/DestructionMaterialTable.cs, Destruction/DestructionEvents.cs (Garden), Net/NetSession.cs (Protocol only), UI/Hud/HudPauseMenu.cs and Net/RemoteInputSource.cs (the Holds line only); stub lines only in Destruction/ImpactDamage.cs (Rules.minRatio, Cargo, Evaluate), Breakable.cs (isYard), Explosion.cs (MaxFrameMsAfterBlast), DebrisManager.cs, DebrisPiece.cs, MeshShatter.cs, Vehicles/TruckVehicle.cs, NPC/GrandmaMover.cs, Gameplay/GameSession.cs | Destruction/ImpactFeedback.cs, Vehicles/TruckRam.cs, Mission/EscapeCheckpoint.cs, Mission/PoliceCar.cs (stubs); 03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md, decisions/ADR-013-dev2-destruction-police.md |
| IMPACT | Destruction/ImpactDamage.cs, Breakable.cs, GlassPane.cs | none |
| WALLS | Destruction/DestructibleModule.cs, StructureGraph.cs, HouseDestruction.cs, DestructibleModuleCatalog.cs, ChunkSetData.cs, ChunkCollisionRelay.cs, DestructibleChunk.cs, RoofSection.cs, Net/Sync/StructureSync.cs, Data/DestructibleModules.asset (yard rows, chunkHealth, YAML), tools/blender/fracture_modules.py, Assets/_Project/Art/PierreKit_Fracture/PKF_* of the 8 re-fractured modules (.fbx and .json; .meta untouched) | none |
| BLAST | Destruction/BlastSolver.cs, Explosion.cs, ExplosionFX.cs, DestructionDebug.cs, ImpactAudio.cs, Items/GrenadeItem.cs, Effects/CameraShake.cs, Net/Sync/PropsSync.cs, Destruction/ImpactFeedback.cs (after the stub) | none |
| DEBRIS | Destruction/DebrisManager.cs, DebrisPiece.cs, MeshShatter.cs, DestructionFX.cs | Destruction/DebrisPool.cs |
| TRUCK | Vehicles/TruckVehicle.cs, VehicleSeat.cs, TruckCargo.cs, CrewBumper.cs, ChaseCamera.cs, TruckWheelVisuals.cs, TruckRamp.cs, Vehicles/TruckRam.cs (after the stub), Audio/TruckAudio.cs, Net/Sync/TruckSync.cs | Vehicles/TruckTuning.cs |
| GRANDMA | NPC/GrandmaMood.cs, GrandmaMoodTable.cs, GrandmaBrain.cs, GrandmaSenses.cs, GrandmaSpeech.cs, GrandmaLines.cs, Stimulus.cs, GrandmaMover.cs, GrandmaDebug.cs, Audio/GrandmaVoice.cs, Net/Sync/GrandmaSync.cs | none |
| MISSION | Gameplay/GameSession.cs, GameLoopTuning.cs, TheftLedger.cs, Contracts/Settlement.cs, DeliverPoint.cs, ContractManager.cs, Net/Sync/SessionSync.cs, Net/NetPlayerDriver.cs (ignore an arrested P2's pose), Net/NetTransforms.cs (speed-aware TeleportJump; the far-body fallback only if the UNITY measurement fails), Player/CrewAnimator.cs (arrest pose only), Mission/EscapeCheckpoint.cs and Mission/PoliceCar.cs (after the stubs) | Mission/EscapeMission.cs, Mission/MissionRoute.cs, Mission/RoadAnchor.cs, Mission/PoliceSpawn.cs |
| HUD | UI/Loc.cs, UI/SessionCards.cs, UI/HudToasts.cs, UI/Hud/SharedHudModel.cs, PlayerHudModel.cs, ToastFeed.cs, SettlementText.cs, UI/Hud/Views/CardsView.cs, GrandmaView.cs, DriveView.cs, PlayerHudView.cs (the CAUGHT ring), UI/Hints/VerbHints.cs, UI/Indicators/IndicatorTypes.cs, IndicatorTargets.cs, CrewIndicatorView.cs, IndicatorArt.cs, IndicatorShape.cs, IndicatorTuning.cs, Audio/WorldSoundEvents.cs | none |
| INTEGRATION | no source file: merges in `C:/GameProject-dev2` (15.2) | none |
| UNITY | scenes and assets under Assets/_Movers/Data and Scenes, new .meta files, 00_PROJECT/PROJECT_STATE.md, 03_TECHNICAL/SLICE_ARCHITECTURE.md, 07_MULTIPLAYER/NETCODE_SLICE.md, 02_GAME_DESIGN/GREYBOX_SPEC.md, decisions/ADR-009 (pointer), changelog/CHANGELOG.md; Net/NetTransforms.cs only for the pre-authorised fallback of section 10 | Data/TruckTuning.asset |

Never touched by anyone: `UnityProject/ProjectSettings/ProjectVersion.txt`, `_ArtSource/assets.blend`. Blender only headless (`blender.exe -b --factory-startup --python`), never Pierre's open Blender.

### Track tasks and acceptance (short)
- **CONTRACTS:** everything in section 13, stubs offline-correct (today's behaviour), `energyDamageScale` unused until IMPACT, Protocol 2, spec and ADR committed. Acceptance: compile 0 errors (both passes); offline behaviour unchanged except the new debris lifetime defaults.
- **IMPACT:** 2.1 in `TryMeasure` (Evaluate, Vehicle classify for the truck and police cars, driver blame, cargo as Impact with the cargo rules, glass min ratio, launched-chunk strike factor and per-frame cap, Handled skip over every contact, the spread into `radius`, `speed`), `structureReferenceMass` in Breakable, the yard event from `isYard`, the pane keeps its own mass. Acceptance: the 2.2 table reproduced by a small editor-free static check (run by the Unity stage); a prop's own damage unchanged; a thrown cup still breaks a pane.
- **WALLS:** 3.2 to 3.14, the 32-chunk snapshot, the rubble flag, `NetDetach` reserve and deferral and mass cap, pre-cooking, yard rows and flag, then the re-fracture as its own last commit (3.9). Acceptance: compile; chunk launch uses `e.speed`; no `SetActive(false)` on a live chunk on either machine; the script's report shows every re-fractured variant valid with 16 to 24 masonry chunks.
- **BLAST:** section 4, `ImpactFeedback`, `ImpactFx` op, cached closest points, `ExpectStructure` before Apply, the launch-grace skip in `PushBodies` and `PlayCosmetic`, grenade values from the table and the dead bounce, `MaxFrameMsAfterBlast`, overlay lines for the new numbers (strikes per frame included).
- **DEBRIS:** section 5, `ExpectStructure`, `TryReserveStructure`, `ShatterChunk`, `DebrisPool` with the NetIds rule. Acceptance: the pool's id-count check.
- **TRUCK:** section 6 (TruckTuning, drive model, seats with the IsDriving rule and `IsAtWheel`, TruckSync seats and OnPeerLeft and chase yaw, the oriented ram sweep and front slab, fling with lateral and Handled, bumper and the grandmother's Knock with the speed, feel, cargo CCD, auto-right, seat index on the wire). Acceptance: compile; the client as passenger online shows no pose fight in `net_host.log` (checked at UNITY).
- **GRANDMA:** section 7 (RunOver speed gate, GardenBroken, StructureBroken cap rule).
- **MISSION:** sections 8 and 9 (phases with the Intro path, send hooks and ApplyReplica order, OnDestroy resets, clock stop on both machines, markers, cars with overtake, roadblock and off-route pursuit, flee timer, arrests with the mute path and NetPlayerDriver, pending and interception warnings, settlement at half price, session wire fields, arrest pose, timer defaults, speed-aware TeleportJump).
- **HUD:** 8.7 and 8.8 (banners including BLOCKED, CAUGHT ring, ESCAPE clock, warning chip, markers for driver and passenger, toasts with the `GrandmaNoticed` magnitude rule, end card, SettlementText shapes, siren bed, `IsAtWheel` in PlayerHudModel, DriveView, VerbHints and CrewIndicatorView).
- **INTEGRATION and UNITY:** section 15.

---

## 15. Integration and Unity stage

1. **Why the split.** `C:/GameProject` is on `main` and is the checkout Pierre's editor has open. A `git checkout` or `git merge` there rewrites scripts and `Map01_PierreKit_House.unity` under the editor, which may be in Play or hold a dirty scene (Unity then offers to reload from disk and his changes are lost). So every merge happens elsewhere, and the editor's checkout is switched once, with his go.
2. **INTEGRATION (no editor).** `git worktree add C:/GameProject-dev2 dev2/destruction-gameplay`. Merge the tracks there one at a time: IMPACT, DEBRIS, WALLS, BLAST, TRUCK, GRANDMA, MISSION, HUD. After each merge: `bash tools/net/compilecheck.sh` from `C:/GameProject-dev2` at 0 errors (runtime, then runtime + editor). A conflict outside the frozen contracts goes back to the owning track. No editor at this step.
3. **Switch the editor's checkout once, with Pierre's explicit go in chat.** Before it, through the MCP: `EditorApplication.isPlaying == false` and no open scene `isDirty`. If either fails: stop and ask; never save or discard for him. Then in `C:/GameProject`: `git checkout dev2/destruction-gameplay` (`ProjectVersion.txt` stays modified and is never committed; git carries it over). Remove the `C:/GameProject-dev2` worktree afterwards.
4. **In the editor, once:** one compile at 0 errors (console), then the data and scene edits below, then one offline Play pass (section 12) and the builds.
   - **Data assets:** `DestructionMaterials.asset`: `structureDebrisLifetime 60`, `propDebrisLifetime 40`, `glassShardLifetime 20` (serialized values override code defaults), the new fields at their defaults (`structureReferenceMass 35`, `maxFrozenPieces 300`), the Plant row, the curve keys. `GameLoopTuning.asset`: `policeCountdown 75`, `defaultTimeLimit 1200`, the police block at its defaults (`escapeCargoPayFraction 0.5`, `fleeTimeLimit 90`, `interceptSeconds 4`, `officerZoneSeconds 2`). Create `TruckTuning.asset` and assign it on `MovingTruck`'s TruckVehicle. `GrandmaMoodTable.asset`, `IndicatorTuning.asset` (exit colour): new fields at defaults. `DestructibleModules.asset`: yard rows and new `chunkHealth` present (WALLS YAML) and read by the catalog. Let the re-fractured PKF FBX reimport (the prefabs keep their GUIDs).
   - **Map01 scene:** `ContractManager.timeLimit` 1200. Reparent the two root `PKX_Fence_Post` under `GrandmaHouse_PierreKit/Garden` (with Pierre: hand-placed). Add `EscapeMission` on `_Systems`; `Route_Street` (RoadPath, not in `CountryLand.roads`); `MissionRoute` with the three segments and the Arrival marker; `PoliceSpawn` with `RoadAnchor`; two Policecar instances (Rigidbody 1400 kg non-kinematic, ContinuousDynamic, BoxCollider, `PoliceCar`, `RoadAnchor`) parked at the spawn; `EscapeCheckpoint` (disabled BoxCollider 14 x 8 x 10, Ignore Raycast layer, `RoadAnchor` Road_East s 320). Passenger `VehicleSeat` on the truck cab (seatIndex 1, seat point right of the driver). `CountryLand.colliderRange` 520 to 1400 if Pierre agrees (decision 12). The sunk driveway tile at (19.5, -10.5), y -0.292 to 0, only if Pierre confirms it is a stray edit.
5. **Checks:** section 12, offline then online (two builds with `BuildGreybox.NetClient()`, loopback test). If the bandwidth peak goes over 80 KB/s, apply the pre-authorised far-body fallback in `NetTransforms.cs` (section 10) and measure again. Record numbers (chunk shares, 0 to 50 time, stop distance, `LastFrameBlastMs`, `MaxFrameMsAfterBlast`, draw calls, NetStats peak) in PROJECT_STATE.
6. **Docs:** PROJECT_STATE, SLICE_ARCHITECTURE (session: the police now start a flee; Mission folder in the ownership table), NETCODE_SLICE (records of section 10, Protocol 2), GREYBOX_SPEC (police and flee in scope for Map01), a pointer in ADR-009 line 32 to ADR-013, CHANGELOG.
7. **Commits:** small conventional commits (`chore(data): ...`, `feat(mission): wire Map01 markers`, `docs: ...`), each ending with the Co-Authored-By line. Push only when Pierre asks. At the end, check out `main` again in `C:/GameProject` with the same preconditions, unless Pierre says to stay on dev2.

---

## 16. Decisions for Pierre (defaults implemented, listed for confirmation)
1. **The police call no longer ends the run:** 75 s to the police's arrival, looting allowed, the mission clock stops, then 90 s of escape before the house is surrounded (fail); interception = a car within 6 m of the truck under 8 km/h for 4 s (announced "BLOCKED!"); police cars overtake a fleeing truck and set roadblocks, and chase off the route within 60 m; all arrested = failed; the truck can ram and stun police cars.
   - **1b. Escape pay:** the truck's contents at **half** value (`escapeCargoPayFraction` 0.5), what she saw you take confiscated (no fine), destroyed list items and a break-in still billed, 500 per arrested member. So a clean delivery always pays about twice an escape: calling the police is never the best plan.
2. **Exit on Map01:** flat Road_East s 320 (351 m) by default; s 660 over the crest (691 m) once the loaded climb is measured.
3. **Police spawn:** Road_West s 580, behind the crest, the opposite side from the exit.
4. **A passenger seat** in the truck (small scope growth) so both players can flee; otherwise only the driver ever escapes.
5. **"Aboard"** = seated, or standing inside the cargo box **while the truck is nearly stopped (under 2 m/s)**. Nobody rides in the box at speed (no platform riding for players in this pass): at speed only the two seats carry people. Crew left behind at the exit count as arrested (fined).
6. **Pockets:** pocketed loot of crew aboard counts at half price like the truck.
7. **A police call during the Intro** starts the contract and the flee at once (instead of failing).
8. **The last warning** is once per run, lasts 25 s, and only a player's own offence costing 2 or more (3 s after it began) ends it.
9. **She gets angrier faster:** offences are no longer capped, she hears explosions at 30 m, notices broken walls (6), garden damage (1.5) and being run over (10, from 3 m/s; a slower nudge is a bump). Retune her table in the same playtest.
10. **Hole size:** one grenade at the foot of a wall opens about 2 m; a thrown grenade drops dead at the wall; a standing-thrown sofa never marks a plaster wall, a sprint-thrown one cracks it, a thrown piano opens a hole.
11. **Walls re-fractured to about 20 chunks** now (was 12): revert that one commit if you prefer the big slabs (before and after renders provided).
12. **Land colliders to 1400 m** (cheap, removes the fall-through on Road_West past s 610) or keep every marker inside 600 m.
13. **Cargo cushioning:** cargo hitting its own truck's box gets +3 m/s and no mass multiplier, so one ram does not wipe the load.
14. **Garden objects** now breakable (posts, hedges, bushes, mailbox, small props), counted as garden damage, not walls; `Grandma_Car`, trees, rocks, the well and the fountain stay solid.
15. **Truck feel:** expected 0 to 50 km/h in about 3 s empty and 5 s loaded, 90 km/h top, 42 m to stop (section 1 holds the limits). Powerful rather than sluggish; tell us if it should feel heavier. Parking bumps under 10 km/h only raise dust.
16. **Arrested pose:** the existing knocked-down clip held face down (no new animation). **An arrested player stays out for the run** with a frozen view (under 30 s on Map01). Options, not built without your yes: a spectator camera following the truck or the partner; a rescue (the truck stopping within 3 m of an arrested member for 2 s picks them up, ramming the car that holds them frees them).
17. **Launched wall chunks do not domino** (they strike 10 times softer and never hurt their own wall); falling roofs still crush walls at half strength. Say if you want the domino.
18. **Not changed, open:** throw speed does not drop with weight (a 180 kg piano still leaves the hands at 6 m/s plus the carry speed).

---

## Appendix A. Review log (2026-09-28)

Every issue was checked against the sources. Accepted and fixed unless marked.

| Issue | Outcome |
|---|---|
| Escape pays more than a clean delivery | Fixed: half price, witnessed confiscated, bills kept (8.6, 16.1b, section 1 row 8). `Settlement.ForDelivery` checked: witnessed items are confiscated and fined, destroyed billed, break-in 250. |
| Endless looting after arrival, uncatchable moving truck | Fixed: `fleeTimeLimit` 90 s, overtake and roadblock, off-route pursuit within 60 m (8.1, 8.4, 8.5). |
| Thrown sofa does 0, piano barely dents | Fixed: `structureReferenceMass` 35 kg, rows at reachable speeds (carry 1 - kg/120 clamped 0.35 checked in `PlayerGrab.CarryMultipliers`). Vehicle mass handled separately (vehicle ratio 200, multiplier 0.01) so the truck rows hold. |
| Grenade targets measured with placed grenades | Fixed: dead bounce and a wider mid curve (0.5, 0.95)(1, 0.7)(1.5, 0.35)(2, 0.15), not the proposed (1.5, 0.45), which would strip interior walls from a room's centre; thrown check added. |
| Re-fracture deferred by default | Fixed: re-fracture in WALLS as its own commit, chunkHealth rescaled (3.9). |
| Flung props slower than the truck | Fixed: cap 35 m/s, lateral share, Handled for 1 s (6.4). |
| Failures with no warning | Fixed: BLOCKED banner, CAUGHT ring, 4 s and 2 s, pending and intercept fields sent on edges only (not per frame). |
| Garden props count as walls | Fixed: `isYard`, `GardenDamaged` event (a separate type, so `OnWayInBeforeKeys` ignores it), `GardenBroken` 1.5. |
| RunOver for any nudge | Fixed: `runOverMinSpeed` 3 m/s, else Bumped. |
| Arrested player idle for the run | Partly: listed for Pierre (16.16) with the rescue; no spectator camera built (Map01 arrests last under 30 s, code born when needed). |
| Mass clamp hides the truck's weight | Fixed: `vehicleMaxMassRatio` 200 (6 t counts), loaded check added. |
| Parking bump opens the house | Fixed by the vehicle calibration (10 km/h dust, 25 km/h first hole), not by a speed allowance, which would act on `v_eff` and do nothing at a ratio of 100. |
| Launched chunks knock down the opposite wall | Fixed: own-module immunity, strike factor 0.1, 20 strikes per frame, Play check, decision 17. |
| Acceleration numbers disagree, thin hill margin | Fixed: 5200 Nm, section 1 = limits, 6.1 and 16.15 = expected, ADR gives no number. |
| Merges under Pierre's open editor | Fixed: INTEGRATION worktree, one guarded checkout switch (15). `git worktree list` confirmed only `C:/GameProject` on main. |
| Pool shells become NetIds bodies | Fixed: hideFlags, kinematic, late warm-up, destroyed with the pool, id-count check (5). `NetIds.cs:250` confirmed. |
| Passenger seat vs IsDriving | Fixed: IsDriving = seated, `IsAtWheel` for "at the wheel", OnPeerLeft and chase yaw (6.3). Readers grepped. |
| Arrest cannot freeze the client | Fixed: explicit mutes on both machines, NetPlayerDriver ignores the arrested pose (8.6). |
| Phase and arrests never replicated | Fixed: send hooks, fields before the early return (`GameSession.cs:451` confirmed), clock stop on both machines (8.1). |
| Chunks immune to later blasts | Fixed with a 0.3 s launch-grace time window instead of a frame test, because the client's deferred detach can land a frame or more after ExplosionFx. |
| Session statics leak into Tutorial_01 | Fixed: OnDestroy resets (8.1). |
| Intro call leaves the session in Intro | Fixed: StartContract and HideIntroCard first; "17 files" corrected to 8. |
| Bandwidth ignores the transform stream | Fixed: measurement, 80 KB/s limit, pre-authorised fallback owned by MISSION / UNITY (10). |
| 900 frozen pieces | Fixed: 300, draw-call budget, frozen shadow rules (5, 11). |
| Frame budget only covers the blast frame | Fixed: `MaxFrameMsAfterBlast` on host and client, unconditional pre-cook, client `NetDetach` budgeted (3.8, 3.11, 11). |
| Ram BoxCast misses touching targets, AABB | Fixed: oriented cast started behind, front slab overlap, exclusions (6.4). |
| ImpactDamage members not frozen | Fixed: TryMeasure, Rules and helpers frozen, anchored and `applied` semantics stated (13). |
| Holds line does not compile | Fixed: member cached from `input.GetComponent<CrewMember>()` (CrewInput sits on the member, `CrewMember.cs:40`). |
| Flag bits inconsistent | Fixed: values everywhere (10, 13). |
| Checkpoint and IsAboard AABBs | Fixed: local-space tests, disabled collider on Ignore Raycast (8.2, 6.3). |
| Aboard in the cargo box at speed | Fixed: under 2 m/s only, listed (16.5). |
| Police point light cost | Fixed: no Light, emissive quads and flares (8.5). |
| TeleportJump pops | Fixed now rather than deferred: speed-aware threshold (10). |
| Spawn reserve taken from props | Fixed: `ExpectStructure` (5). |
| Client chunk mass and doubled dust | Fixed: mass cap in `NetDetach`, ImpactFx only for hits that remove nothing (3.8, 3.9). |
| GrandmaNoticed at 0 toasts | Fixed: HUD toasts only magnitude over 0; `HudToasts.cs` given to HUD. |
| Debris damage cascade | Fixed with the dominoes issue (3.14). |
| Found while verifying | A thrown cup would no longer break a window under the new ratio floor: `Rules.Glass.minRatio` 0.5 keeps it (113 against 12 HP). Cargo hitting its own truck would have been classified Vehicle: it is now Impact with the cargo rules. |
