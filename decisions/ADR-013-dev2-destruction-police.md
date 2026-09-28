# ADR-013: DEV 2, destruction by energy, the truck as a ram, and a police flee

## Status
**Accepted (2026-09-28), Pierre's decision (the "DEV 2: destruction, world and gameplay" list).** The defaults of the police flee (Decision 4) were not specified by Pierre: they are implemented and listed for his confirmation in `03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md` section 16. Revised the same day after a gameplay and a tech review (DEV2_DESTRUCTION_GAMEPLAY appendix A). To be read by Spykernv.

It amends:
- [ADR-009](ADR-009-vertical-slice.md):
  - point 4, "At 0 she calls the police and the run fails": the call now starts a flee phase on a map that has the markers for it. Without them (Tutorial_01, any scene without `EscapeMission`) the old rule stands.
  - point 6, "8 to 15 per module": the exterior and interior wall sets are re-cut to about 20 masonry chunks; up to 24 masonry chunks per module are allowed (32 with wooden frames, the join snapshot's limit).
- [ADR-008](ADR-008-everything-breaks.md) point 1, damage from hits: the "scaled by the other body's weight, capped" rule becomes one energy-based function (below).
- `02_GAME_DESIGN/GREYBOX_SPEC.md`: police cars, a flee objective and an exit checkpoint leave the out-of-scope list for `Map01_PierreKit_House` only.

## Context
Pierre, 2026-09-28, after QA on the online slice (ADR-012): "fractures judged unrealistic, damage insufficient". His DEV 2 list, in short:
1. Rework destruction: fracture size and realism, damage propagation, chunk detachment, debris impulse, small versus big impacts, feedback. A grenade against a wall must destroy far more than a small object thrown normally. Keep the existing system.
2. Damage from mass and relative velocity, one coherent function on impact energy, multipliers in data.
3. Grenades: damage, force, surface destruction and projection by distance, with a progressive falloff; very close walls largely opened; watch performance.
4. Debris kept long enough, cleaned up progressively, never vanishing in front of a player; lifetime, distance, sleep, pooling, a global cap.
5. The truck as a destruction tool: barriers, plants and small elements stop it absurdly today.
6. The truck: much more power, top speed near 90 km/h instead of about 40, heavy but usable as a ram.
7. The grandmother's annoyance: reliable reactions and states, a deterministic police call at the threshold, except the one "last warning" he asked for earlier (she is a bit deaf and warns once).
8. When she calls the police: an incoming state, sirens and lights, a flee objective, an exit checkpoint, the truck to flee, the police can intercept (failure), the checkpoint means success. A reusable mission system from data and scene markers, because a much bigger map with a longer road is being designed.
9. A 20-minute timer instead of 15.

The audits (five, 2026-09-28) found:
- **Walls barely feel a grenade.** A focus term `1 / (1 + (d / 0.5)^2)` cuts wall damage beyond 0.5 m; a thrown grenade bounces and lands 0.5 to 2 m away, where no chunk comes out. Walls are 12 full-thickness slabs of 150 to 880 kg, which get about 0.6 m/s and hop in place.
- **Mass hardly counts.** The mass ratio is clamped to 3 against a 100 kg stand-in, so the 3.5 t truck hits like 300 kg, a thrown sofa (at most about 9.6 m/s) never marks a wall, and every hit lands on one chunk. Static colliders stop the truck dead before the receiver even measures the hit.
- **Chunks can vanish** when the per-frame debris budget is spent, and every piece of a blast shrinks away at the same second, in view.
- **The truck is capped by a governor** at about 40 km/h, with constant torque and no power curve.
- **The last warning** can be consumed by the blast that started it, ended by a noise or by the schedule check, repeats forever, and is invisible on the HUD; a per-window loss cap and a 16-entry queue silently swallow thefts and explosions.
- **The police call** ends the run 8 s later; nothing exists to flee with.

## Options
- **Rebuild destruction** (runtime Voronoi, hierarchical chunks). Rejected: Pierre said keep the system; too big for the greybox.
- **Keep the 12-slab sets and fix only the numbers** (distance curve, propagation, launch, rubble). Rejected after review: it fixes "damage insufficient", but support falls and medium hits still drop metre slabs, which is QA's first complaint. The re-fracture is one headless command with the existing pipeline.
- **Tune the existing formula only** (raise the mass cap). Rejected: it does not give a coherent small-to-huge range and keeps the dead stop against static pieces.
- **Police as NavMesh officers and wheel-collider cars.** Rejected for the greybox: force-steered bodies on a route polyline, with overtaking, roadblocks and an "officer zone" around a stopped car, give the same situation at a fraction of the code and cost.
- **Chosen:** keep the architecture, improve it in place, as below.

## Decision
1. **One impact function on collision energy.** `E = 0.5 * mu * v^2` with the reduced mass (anchored pieces count as infinitely heavy), normalised by the receiver's resisting mass into an equivalent speed, gated by the material's minimum speed, with per-type multipliers and mass-ratio clamps in `DestructionMaterials.asset`. Built receivers resist like 35 kg, calibrated on the speeds players can actually throw, so a sprint-thrown sofa cracks a wall and a piano opens one; what a prop itself suffers is unchanged. Vehicles count with their real mass (up to 7 t) through a bumper factor, so a parking bump raises dust and a 90 km/h ram opens the wall. Big hits spread over the chunks around the contact with a radius that grows with the cube root of the energy.
2. **Walls, kept and improved:** a distance curve for blasts on structure (also on whole structural pieces), accumulated damage breaks a wall up, overkill propagates to neighbouring chunks, broken-off chunks are launched by speed along the blast or the striker, big overkill turns a chunk into rubble, a chunk is never switched off for want of budget on either machine, feedback scales with energy, launched slabs do not domino through other walls, the join snapshot grows to 32 chunks, and grenades stop dead where they hit. The exterior and interior wall sets are re-fractured to about 20 chunks with the chunk health rescaled, in one commit Pierre can revert.
3. **The truck is a ram.** A data asset (`TruckTuning`) with a power-limited engine and a 90 km/h governor, aerodynamic drag, load-independent brakes, a lateral-acceleration steering limit and auto-righting (targets in DEV2_DESTRUCTION_GAMEPLAY section 1). An oriented look-ahead sweep applies the ram's energy before the physics contact, so a fence or a wall that gives way lets the truck through, minus an energy toll, and small props are flung out of its path. Its damage is blamed on the driver. Garden posts, hedges, bushes, the mailbox and small garden props become breakable, as garden damage rather than walls; trees, rocks, the well and the fountain stay solid.
4. **The police call starts a flee, not a failure**, on a map with the markers:
   - `EscapeMission` on `_Systems`, and scene markers `EscapeCheckpoint` (an oriented volume), `PoliceSpawn`, `MissionRoute` (road spans or waypoints) and `RoadAnchor` (snaps a marker to the road at Play). No map name or position in code.
   - Defaults: 75 s until the police arrive, looting allowed, the mission clock stops; then 90 s to get away before the house is surrounded; the truck in the exit volume with a free crew member aboard succeeds and is paid **half** the value of what it carries (the contract is void, witnessed loot is confiscated, bills stay), so a clean delivery always pays more; police cars overtake a fleeing truck and block the road; a car staying near the slow truck for 4 s fails the run; a crew member on foot caught by a car or an officer zone is arrested (frozen face down, fined); all arrested fails; the truck can ram the police cars; every failure is announced on screen at least 2 s before it happens.
   - A passenger seat lets both players flee; the cargo box counts as aboard only when the truck is nearly stopped.
5. **The grandmother becomes readable and deterministic:** one last warning per run, shown for its whole duration, ended only by a player's own offence; no silent loss cap on offences; a queue that never drops a theft or an explosion; she notices broken walls, garden damage and being run over (a nudge at walking pace is only a bump).
6. **Debris lives longer and leaves out of sight:** 60 / 40 / 20 s with jitter, cleaned up only out of view or far from every player, with a destroy budget, a frozen cap (300) separate from the dynamic cap, a spawn reserve for wall chunks only when chunks are expected, and a pool for shattered pieces that the network id sweep never sees.
7. **Online stays host-authoritative:** every new behaviour runs where `Net.HasAuthority`, reaches the client through the existing syncs (Structure, Props, Truck, Session) and WorldEventRelay, and the police cars are scene-placed bodies on the transform stream with a speed-aware teleport threshold. Arrests freeze the client on both screens. `NetSession.Protocol` goes to 2. Bandwidth is measured with the heavier blasts and rams; the 15 m / 10 Hz far-body fallback of NETCODE_SLICE 4.4 is pre-authorised if the peak passes 80 KB/s.
8. **The timer is 20 minutes** on Map01.
9. **Integration never touches the editor's checkout until the end:** tracks merge in a separate worktree, and `C:/GameProject` is switched once, with Pierre's go, when the editor is not playing and holds no dirty scene.

The contracts, tracks and numbers are in `03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md`.

## Why
- **QA's two complaints have measured causes** (the focus term, the ratio cap, the one-chunk point hit, the slow slabs, the bouncing grenade, the big slabs themselves), and each fix is a local change in the class or the pipeline that already owns it. Nothing proven (the carry, the online layer, the fracture pipeline) is rebuilt.
- **Energy is the one quantity that ranks a cup, a sofa, a grenade and a truck** on the same scale, and a data table keeps it tunable after a playtest note.
- **A flee phase keeps the laugh instead of cutting it.** The old call ended the run 8 s after the worst moment. Now that moment starts a scramble a spectator reads at once (sirens, a truck, a roadblock, someone left behind), which is H1's 10-second test. Paying half keeps "steal under watch" intact: getting caught is a funny rescue, never the best plan.
- **Markers over map code**, because the bigger map with a longer road is the real client of this system; Map01's 80 m street is only its first test.

## Consequences
- **Scope grows openly:** police cars with simple AI (route following, overtaking, roadblocks, short off-route pursuit), a flee objective and timer, arrests, a passenger seat, finer wall sets. They are new NPC-like systems (CLAUDE.md sections 15 and 16), recorded here.
- **Balance moves together:** walls and the truck destroy much more while the grandmother notices more. Her table, the chunk health and the ram toll must be retuned in one playtest, not separately.
- **Performance risk:** more and smaller chunks per blast, rubble and longer-lived debris. Bounded by the spawn and destroy budgets, the frozen cap, pre-cooked chunk colliders and pooling; measured with the F3 overlay (destruction cost at most 16 ms over the blast frame and the 5 after it, on host and client; at most +3k draw calls in split screen).
- **The two screens differ a little more:** rubble pieces are local cosmetic debris on each machine, as all debris already is.
- **Both builds must be rebuilt** (protocol 2).
- **Map01 is a short test of the flee:** at 90 km/h the exit is 15 to 30 s away. The system matters on the bigger map.
- **Hand-placed scene objects are touched** (two driveway posts reparented, markers added); done in the editor with Pierre, never over his unsaved scene.

## Revisit if
- **Pierre prefers the big slabs:** revert the re-fracture commit; the curves, propagation and rubble still apply.
- **The flee never happens** (the crew always leaves before the police arrive) or always fails: tune the countdown, the escape time, the chase speed or the interception rule, in data.
- **Players call the police on purpose** despite the half pay: lower `escapeCargoPayFraction`.
- **The truck turns every run into demolition** by minute three (ADR-008's revisit condition): raise the ram toll or lower `vehicleMultiplier` in data.
- **The grandmother calls the police in the first minutes** of normal play: retune her table.
- **The destruction cost goes over 16 ms**, split screen drops under 60 fps, or the online peak passes 80 KB/s: lower the caps, then apply the far-body fallback.
- **A passenger seat is not wanted:** remove it; only the driver will ever escape.
- **Arrested players are bored** on the bigger map: add the spectator camera or the rescue listed in DEV2_DESTRUCTION_GAMEPLAY 16.16.
