# CHANGELOG

_Not just code. Categories: DESIGN, TECH, RESEARCH, DOCS, CONTENT, BALANCE, BUSINESS, MARKETING._

## 2026-09-27

### The asset workflow, written down from what the bathrobe pilot used (ADR-011, proposed)

Phase 2 of the robe pilot: only what the pilot actually ran is formalised, and none of it outranks playing the house with a second person. Branch `art/asset-workflow`, from `art/bathrobe-pilot`.

- **DOCS, ADR-011, proposed by Jonathan:** working references, a style profile, and a human verdict as the gate. Numbered 011 because Pierre's decisions took 008 (everything breaks), 009 (the vertical slice) and 010 (the presentation layer) in the meantime. It becomes "Accepted, and flagged" when Pierre agrees, and the environment tiers are his to decide.
- **DOCS, STYLE_GUIDE sections 18 to 20:**
  - working references v0: five tiers, promotion by a named human with a date, the family conventions;
  - generation modes: STRICT_MATCH, STYLE_CONSISTENT and IMPROVE, each with what it preserves, may improve and avoids;
  - the review checklist: the gates in order, the human verdict as the gate and `MoversWearCLI` in the map as the final one, five failure owners (Blender, Unity, Design, Code, Animation). Every hint a script prints now gets a threshold or a reader.
- **DATA, `05_ART/style/profile.json` v0:**
  - a palette in sRGB as Unity shows it: Pierre's nine `PK_*.mat`, the four crew colours, the garment pink #F294B8 and the robe's trim placeholder;
  - three families: PierreKit structure, garments, and house props (left unspecified);
  - a register of 17 glob entries with tiers, the export presets and the review settings.
  - **Jonathan approved the garment and crew entries (H7):** the Floreswa body canonical, the robe v2 acceptable, the robe v1 anti, and the garment conventions. Every environment tier stays proposed, for Pierre.
- **TECH, `tools/blender/movers_blender.py`, one module for the asset scripts:**
  - paths from the repository root, so a script also runs from Pierre's `C:\GameProject`;
  - the body import, and materials from the palette;
  - rigid, skinned and animation export presets that fail loudly, with no fallback to `wm.fbx_export`, which writes centimetres;
  - review renders in the Standard view transform, with the body's silhouette with and without the piece and the piece at 8 m;
  - contact sheets, and a check of the register.
  `model_bathrobe.py` and `model_slippers.py` run on it. The proof: the same `CHECK`, `MEASURE`, `ROBE_` and `SLIPPER_` lines before and after, exit 0 with `--python-exit-code 1`, and FBX files identical byte for byte outside the creation timestamp, with a fixed hash seed.
- **CONTENT, the slippers take the garment pink:** #ED8CB2 becomes #F294B8, on Jonathan's verdict on a before and after render. Their FBX changed in its material colour only, checked by re-import. The robe is untouched.
- **CONTENT, the slippers' previews had been wrong since 2026-09-17:** the crew rig stands turned 180 degrees about Z, and the slippers, built in the body's frame, were drawn on the other foot, back to front, with the right boot's toe through the left slipper. They are parented to the armature for the renders now, and each one swallows its boot. The silhouette test: they add 3 % of outline from the front, 4 % at three quarters. Also found: a review's world colour lands in every FBX material's AmbientColor, so the slippers render after exporting.
- **MEASURED, `05_ART/style/metrics.json`:** Pierre's 26 modules against the 26 PKX pieces. No metric splits them cleanly. Three nearly do, and they agree that the agent overdid Pierre's wonkiness:
  - the PKX lean off the grid far more: median share of tilted surface 53 %, against 7 %;
  - they chamfer almost every edge: 0.90 against 0.53;
  - they are mostly quads: 69 % against 4 %.
  The question for Pierre is in STYLE_GUIDE section 18.
- **DOCS, contradictions fixed:**
  - glTF becomes FBX in ASSET_SPECS, ASSET_PIPELINE and ARCHITECTURE;
  - ASSET_LIST's palette pointer named section 13, which holds the totals; it now points at the profile, and its orientation and budget lines point at the measured values;
  - the three "Build Kit Prefabs" instructions are marked removed (7e70bd7);
  - REJECTED.md gains ten rejections from the proposal, with their reasons;
  - tools/README.md documents the module, the register check and the measurements, with two more headless rules: read the real exit code, and batchmode cannot sign in.
- **TOOLING, three skills in Jonathan's user folder:** `make-asset`, `asset-to-unity` and `art-style`, written from the commands the pilot ran. They move into the repo once ADR-011 is accepted. A fresh session lists all three, and a read-only dry run of `make-asset` (the slippers to the garment pink) picked the skill unprompted and stopped after announcing its references.
- **BLOCKED, Unity in batchmode:** it stops at package resolution. Pierre's `com.sahland.lumaflow` comes from the Asset Store, is not in this machine's package cache, and batchmode cannot sign in. So the slippers' new colour is not checked in Unity yet (`ReportAsset`), and nothing was compiled in this session. Opening the project once from the signed-in Unity Hub unblocks it.
- **NOT DONE:** the environment tiers and the PKX question wait for Pierre. The slippers have still never been seen worn in Unity, and the F key has never been pressed by a human. The robe's tracked renders stay the old AgX ones until its script next runs for real: nothing in its FBX changed, so it was not re-exported.

## 2026-09-26

### The grandmother's house becomes a systemic vertical slice (ADR-009)

- **DESIGN, Pierre's decision, ADR-009:**
  - The verb is steal under watch: the moving contract is the cover, and the grandmother's things that are not on the list are the prize.
  - Two local players share a split screen.
  - The grandmother lives in the house on her own, with patience that runs out.
  - Walls break structurally, and the truck drives.
  - Architecture and file ownership are in `03_TECHNICAL/SLICE_ARCHITECTURE.md`.
- **TECH, shared contracts:**
  - One event bus: `WorldEvents`, one struct and one list of facts.
  - A reference-free `DamageEvent`.
  - Per-player input, `CrewInput`: the legacy Input Manager stays behind it, and the keyboard reproduces the validated carry numbers exactly.
  - A crew roster, a read-only `Session`, debug keys registered through `DebugCommands` and listed with F12, and HUD regions per viewport.
  - `MovableObject` gains a physical profile: can carry, push, throw, be loaded or be pocketed, and whether it belongs to the grandmother.
- **DESIGN, the players:**
  - P1 plays on keyboard and mouse, P2 on a gamepad.
  - F1 hands the keyboard to the other player, and F2 switches between split, solo P1 and solo P2.
  - Holding is exclusive: two players cannot share one object.
  - Objects too heavy to lift are dragged along the floor.
  - Pockets take small valuables.
  - The crew bodies stand on the floor and walk, run and crouch, and carrying raises only the arms.
- **DESIGN, the grandmother:**
  - **Movement and routine:** she moves on a NavMesh built at Play from the colliders, with no package, and opens doors herself. She keeps a routine at 11 activity spots: rocking chair, reading, tea, cooking, plants, the fireplace (she lights a real fire), TV, the veranda.
  - **Senses:** she sees in a 110 degree, 14 m cone and hears world events through walls.
  - **Intro:** she hands over the keys during the intro.
  - **Theft and patience:** she witnesses thefts, and a patience table decides her reactions and confrontations.
  - **Softened the same evening** at Pierre's request ("a bit deaf, not too hard"):
    - she hears at 60 % of the range, and costs are about half;
    - she loses at most 20 points per 10 s;
    - at zero she gives a 25 s last warning, and she calls the police only if something else happens during it.
- **DESIGN, the run:**
  - The session goes Intro, then contract, then settlement.
  - The front door is locked until she hands over the keys; breaking in also starts the contract, and costs.
  - Delivery happens at the truck.
  - An itemised settlement pays contract items, bills destroyed ones, pays unseen theft and confiscates witnessed theft with a fine.
  - Ten small valuables are hidden in the house.
  - F5 reloads the run.
- **DESIGN, destruction:**
  - 13 wall types are pre-fractured in Blender, from Pierre's own meshes, 8 to 15 chunks each, two variants.
  - A wall swaps to its chunks the first time it is hurt. Damage lands per chunk by distance, cover and material.
  - A support graph drops what nothing holds up: a second grenade brings down what the first one weakened.
  - Roofs fall as whole sections when their walls are gone. The foundation stops at Damaged. Floors and stairs never break.
  - Glass cracks before it breaks.
  - Debug keys: F3 overlay, F9 grenade, Shift+F9 explosion, F10 damage, F11 reset.
- **DESIGN, the truck:** it can be driven (E at the cab door) with arcade handling. The cargo stays physical in the box, the ramp stows while driving, and it has a capacity and weight.
- **CONTENT, the kit:**
  - `tools/blender/export_pierrekit.py` fixes the normals of Pierre's kit at export: 86 inside-out parts become 0. That fixes the ridge tiles that vanished from above, the gutters, and the window-sized holes in the 32 plain and interior walls.
  - Every window gets two real sashes (frame plus glass) that swing outward without clipping.
  - `assets.blend` is never written.
- **CONTENT, animation:** 22 humanoid clips on the crew rig (`tools/blender/author_clips.py`), shared by the crew and the grandmother. Her skirt is re-weighted for sitting.
- **CONTENT, the scene:**
  - The wine bottles are put back on their shelf, and the slippers lie flat.
  - The crew body now faces its camera: it was modelled facing -Z.
  - Pierre's imported "Target Indicators" package keeps its runtime. Its Samples need TextMeshPro, uGUI and the Input System, and are moved out of the project.
- **TESTING, in Play by script, in the shared editor:**
  - **Pass counts by block:**
    - game loop: 39 checks;
    - grandmother: 33;
    - players: 16;
    - truck: 17;
    - doors and windows: 20;
    - destruction: 65 of 66;
    - final regression: 30.
  - **Console:** 0 errors.
  - **Seven game bugs were found and fixed on the way:**
    - plinths counted as windows;
    - unreadable meshes in her NavMesh;
    - she dropped every walk after a wall broke;
    - she looped on the garage door;
    - grenades counted as hers;
    - dragged crates tipped over;
    - her arms were frozen by an animator layer.
- **MEASURED, the blast:** it took 27 ms warm, with 150 debris pieces at about 0.13 ms each, plus a one-off 40 to 55 ms on the first blast. The per-frame debris budget is now 90, for about 12 ms.
- **OPEN, for Pierre:**
  - The blast push throws light props upstairs.
  - The bathroom mirror and other hand placements (A4 items 3 and 4).
  - Eye height: 1.62 against the spec's 1.60.
  - The truck can leave the map.
  - Skirt clipping on the chair.
  - A 3 to 5 mm light slit round shut sashes.
- **NOT DONE YET:** the presentation wave Pierre asked for the same day. It is being built in staging:
  - a cozy low-poly UI on LumaFlow, with key hints for every object;
  - a title screen with the truck on a road, and the running-and-falling loader;
  - target indicators;
  - first-person smoking and drinking with body animations;
  - synthesised audio.

### The bathrobe is worn in the house, and the carry pose keeps its arms outside the chest

The first run of an asset workflow tried end to end on one real piece: spec, references, review renders, human verdict, export, import report, worn in the map, human verdict again. Jonathan judged every step.

- **CONTENT:** the robe's second version fixed the four shapes diagnosed on 2026-09-20 and the team still read armour: flat shaded, its twenty vertical facets are lamellar plates. It now shades smooth below 45 degrees, rims kept sharp (`SMOOTH_ANGLE` in `model_bathrobe.py`). Geometry unchanged: 1144 tris (740 pink, 404 trim), mid-calf, enclosure 0 under 0.8 cm. A cinched waist was tried at three strengths and dropped: every one put a hard ledge over the belt that read as the lower edge of a cuirass.
- **TECH, the robe had never been worn, and it tore apart the first time:** all 19 weighted bones disagree with the body's bind poses, the arms by 160 to 170 degrees, because the robe's armature is Blender's re-import of the body's FBX and the re-import turns every bone frame. The joints themselves match to 0.00 cm under one uniform scale of 0.6956. `CrewEquip` now rebakes a skinned piece into the body's bind space (`BakeIntoBodySpace`, a least squares fit on the joints) and skins it with the body's own bind poses, refusing a piece whose joints miss by more than 1 cm. The check it replaces looked at the first shared bone only, the spine, and passed. Nine weightless leaf bones (`hand.L_end` and the like) now resolve through the rig. Skinned pieces import with Read/Write on.
- **TECH, then two needles:** three hem vertices were weighted 90 % to the hands. Below the hip the skirt looks its weights up at hip height, where the hanging hands of the bind pose are nearer than the hips. Only the sleeves may take an arm now, and `check_weights` refuses the export otherwise. The hint had been printed on every run ("skirt weight share ... hand.L 1%") and judged by nobody.
- **CONTENT, the pose for empty hands is Pierre's:** `AC_Crew` had the carry pose only, so every player stood with his arms out in front of him and a garment could not be judged on that body (Jonathan's call). The robe was first judged on a relaxed pose written for it. Pierre's `AC_Crew_Slice`, merged the same evening, already plays an idle, walk, run and crouch, with the carry pose on its own "Carry" layer driven by `CrewAnimator`, so the relaxed pose and its `CrewPose` driver were dropped in its favour and the robe re-checked on his idle.
- **CONTENT, the carry pose fixed:** its elbows sat 12 cm inside the shoulder line and the forearms ran through the chest, because on this rig `right` is the character's own right and `right * s` moved both arms inward. Found by the team on the robe, whose front the hands came out of. Elbows now 12 cm out, hands at shoulder width. The clip plays in the Carry layer of `AC_Crew_Slice`, so every crew member carries this way, robe or not.
- **TECH, tooling:** `MoversInspectCLI.ReportAsset` (what Unity makes of a model: tris per submesh, bounds, shaders, bones and bind poses against the body, joint fit) and `MoversWearCLI` (wears a piece on the player's body in `Map01_PierreKit_House` through the same call as the F key, at rest and carrying, shots at 3 m and 8 m, fails on any edge stretched past 2.5 times its bind length). How to run them: `tools/README.md`.
- **DOCS:** the robe's references are in `_ArtSource/references/bathrobe/`. The concept sheet only existed in a temporary folder; the Sketchfab model is kept by its URL, the screenshot showed a personal bookmarks bar.
- **Lessons for the asset pipeline, each measured:** Blender renders through AgX and the project is Gamma, so the review renders showed a mauve where Unity shows #F294B8; review with the Standard view transform. `blender -b` exits 0 when the script raises, so a refused export looks like a success; pass `--python-exit-code 1`. A skinned renderer baked before its first draw returns its bind pose, which let the needles pass the stretch check once; check after a draw. A compile error keeps Unity busy in batchmode for over ten minutes before it exits; compile on its own first. The agent's own five-point review passed the robe twice where the team's eyes did not; the human verdict stays the gate.
- **NOT DONE:** the F key has still never been pressed by a human. The mirror renders an empty pane in the batchmode captures; whether it reflects in the editor is unverified. The robe is not placed in the map, which is Pierre's scene.

## 2026-09-25

### Everything in the house breaks (ADR-008)

- **DESIGN, Pierre's decision, recorded in ADR-008:** the grandmother's house now breaks: walls, gables, chimneys, fences, railings, posts, veranda panels, door leaves, window glass and every movable object. Floors, roofs and stairs stay solid, and the reason is in `04_PRODUCTION/REJECTED.md`. It amends the greybox spec's out-of-scope line for this map only (Tutorial_01 is untouched), and the "no inventory" half of ADR-007.
- **DESIGN:** one health model, `Breakable`, eight materials. A hit hurts only above the material's speed threshold, and past it the damage grows with the square of the extra speed, scaled by the other body's weight. What you carry is cushioned by your arms. At half health an object is broken (half pay, as before), and at zero it shatters into physical debris and switches off. This replaces the `breakThreshold` impulse test flagged in the entry below.
- **DESIGN:** a smashed required object leaves the checklist, so it no longer blocks delivery, and the client bills its full value ("Smashed" on the HUD). Money can go negative.
- **DESIGN:** a crate of six grenades in the cellar, no refill. Hold RMB to pull the pin (3.5 s fuse), let go to throw. The blast has a 6.5 m radius and damages the nearest things first, so a wall that gives way lets the blast through. It pushes objects, knocks the crew back and adds concussion to the drunkenness meter. It lights any grenade in the open within 5.2 m a fraction of a second later. Walls give way only to a grenade within about 0.6 m, and cellar walls survive one grenade. Debris is capped at 450 pieces.
- **DESIGN:** four pockets on keys 1 to 4. A pocket holds the object itself, switched off, so a lit grenade keeps counting in there and goes off in your trousers. Only usables fit (cigarette, beer, grenade). The crew starts with the smoke in 1 and the beer in 2, and the van still lays out spares.
- **DESIGN:** the action key. E opens and closes 14 doors, 21 windows (two casements each, opening outwards) and the up-and-over garage door. The verb shows under the crosshair. E is also DELIVER: the action claims the key first (`InputClaims`), so opening the front door beside the truck never delivers by accident.
- **CONTENT:** the house is raised 0.30 m on its plinth, with a stone step at each of the four entrances. The step mesh's top is uneven, so each step has an invisible flat slab at 0.15 m, and all four entrances pass both ways. The veranda stands on a plinth base with a floor. A ceiling closes the well over the cellar stair, where the roof used to show. The garage, garden and street stay at ground level.
- **CONTENT:** the grandmother's car stands on the driveway in front of the garage, nose to the garage door, as Pierre asked. It has a rigidbody and is not breakable.
- **CONTENT:** three door leaves continue the extension kit (interior door, garage door, veranda door), which brings it to 22 pieces. The grandmother is her own model now: `SM_Grandma`, Humanoid, 1.58 m, with a 3.5 s idle loop, looking out of the veranda. Still no behaviour.
- **TECH:** 21 new scripts in `Core`, `Destruction`, `Effects`, `Interaction` and `Items`. Five existing ones are extended: `MovableObject`, `ContractManager`, `GameHUD`, `PlayerController` and `PlayerGrab`. `HouseDestruction` does all the setup at Play, so the kit and the scene stay as built. It cuts every window's glass submesh into its own panes and tags structure by kit module name.
- **TECH, two traps:** static batching merges meshes and would stop the glass from being cut into panes, so the house is not static-batched. A Humanoid avatar copied from another rig fails with "Parent for 'spine' differs" unless its skeleton list is left empty and only the bone mapping is kept.
- **TESTING, in Play, by script, not by a person:**
  - **Pass:** doors and windows open outwards and close again; a thrown 1 kg object hurts a pane; a blast removes a door leaf and leaves the frame unusable; a blast breaks all six panes of a window and leaves its wall standing; a blast 0.3 m from a wall destroys it.
  - **Pass:** an object smashed in the hands frees them and lifts every carry penalty; a live grenade goes off in a pocket and leaves the other pockets intact; a live grenade in the hands goes off and leaves nothing held.
  - **Pass:** a blast beside the crate lights all six grenades, and none goes off in the same frame. The console showed zero errors.
- **TESTING, two faults found and fixed in that run:**
  - The chain reaction ignored cover. A blast on the ground floor lit the cellar crate through the floor, and nobody watching could have read why the cellar exploded. A floor, a wall or a shut door now keeps the pin in, and loose things don't. Retested both ways.
  - A blast gathers its targets into a fixed buffer, and during the crate chain fresh debris filled it, so colliders were dropped in no particular order, walls included. The buffer now grows.
- **TESTING, a near miss worth remembering:** the pocket test first reported PASS without having done anything. The tool refuses reflection, so the step that pockets the grenade never ran, and the check that followed only saw empty pockets. It was caught because the first half of the test had errored, and it was redone through `SendMessage`. A pass is only worth something if the setup visibly happened.
- **MEASURED:**
  - **Hitch:** a grenade against a window wall spends 117 ms in its one frame.
  - **Debris:** it peaks at 1792 pieces and falls back under the 450 cap within seconds.
  - **Frame rate:** 57 fps at rest, running unfocused in the editor, and above 100 after the chain cleared.
- **OPEN, for Pierre (listed in ADR-008):**
  - Every fragility number.
  - How close a grenade must be to breach a wall.
  - That a blast behind a floor still does 35 %: a grenade upstairs broke the cellar crate below without lighting it.
  - Gables that break under a roof that stays in the air.
  - Whether a slammed door should break things.
- **NOT DONE:**
  - The car cannot break.
  - Tutorial_01 has not been run in Play since the five shared scripts changed. It compiles, but Pierre was working in the editor at the time.
  - Nothing here is committed.

### The house becomes a playable greybox

- **CONTENT:** Pierre rebuilt one plank floor with square-cut ends. Measured by raycasting its top surface, the holes drop from 22 % to 1 %. It now floors every wooden room, the porch and the terrace (36 tiles). His three other plank floors still use the staggered pattern and still show 21 to 27 % holes, so they are no longer placed.
- **CONTENT:** two extension pieces close the glass lean-to, `PKX_Veranda_Cheek` (a glazed triangle at each end) and `PKX_Veranda_Lintel` (a beam over the veranda doorway). `assets_extension.blend` now holds 19 pieces. `assets.blend` is still only read.
- **DESIGN:** `Map01_PierreKit_House` is playable with the Tutorial_01 systems and nothing else. 102 movable objects in ten rooms on three levels, 12 on the contract (sofa, armchair, bookshelf, dining table, two chairs, fridge, the cellar wine barrel, bed, wardrobe, dresser, attic trunk), 26 fragile. Everything else is extra value, since `ContractManager` already pays for whatever is in the truck at delivery. 15 minute timer. Positions, weights and values live in one table, `_ArtSource/house_objects.txt`.
- **DESIGN:** the truck is the car pack's box truck with its closed cargo box hidden and an open-backed box built on its bed: 2.2 x 2.3 x 5.7 m inside, floor at 1.39 m, backed up to the driveway gate with a 16 degree ramp. The ramp fills that gate, so the driveway is the loading dock and the pedestrian gate is the way in. The garage faces the truck, as the concept meeting asked.
- **DESIGN:** the player starts on the driveway with the red crew body and the equip key. The cigarette and the beer lie on the grass by the ramp. The grandmother's glasses and slippers (copied from Map01_Grandma with their tuned offsets) are on her nightstand and by her bed, and her working mirror hangs over the bathroom sink.
- **DESIGN, flagged:** a grandmother body stands on the veranda. Static, no behaviour, no detection. The NPC and everything else that depends on the verb (theft ledger, cat, fish, window entry) stays unbuilt until the divergence in `PROJECT_STATE.md` is settled.
- **CONTENT:** the garden is dressed from packs already in the project (8 trees, hedges along the street fence, bushes, flower beds along the path, fountain, well, vegetable beds, scarecrow, bench, birdhouse, mailbox), plus a street with a kerb and 16 warm room lights without shadows.
- **TESTING, measured before anyone opened it:** all 102 objects start clear of every wall and of each other. All 19 doors and openings pass the player capsule with the furniture in place, both stairs climb, and 9 carry routes walk end to end: spawn to front door, driveway through garage and dining to the hall, up the ramp into the truck, living room to veranda, hall to kitchen to dining, back door over the terrace past the chimney, and round both sides of the garden. The first pass found 8 faults (an armchair and a rocking chair in doorways, the guest bed across its door, four overlaps, the back door leaf across the terrace) and all were fixed. In Play, after 12 s every object was asleep, none fell through and none broke. One perfume slid into the concave sink basin and now stands on the floor. The console was clean, about 260 fps.
- **TECH, for the breakage system:** `breakThreshold` is compared with the collision impulse, which is mass times velocity change. A 20 kg television breaks on a 0.3 m/s bump while a 0.5 kg plate dropped from a table never reaches 6. Worth switching to relative velocity, or impulse divided by mass, when breakage is built.
- **KEPT:** Pierre's hand edits to the scene (two open double gates, their posts, the flipped driveway tile). His porch decks overlapped by half a tile to hide the floor holes are replaced by the square floor that makes them unnecessary.
- **NOT DONE:** breakage is still a grey tint (Pierre builds that system). Object values and weights are first guesses. The grandmother is a male body from the character pack in its bind pose. Roof crossings are still visible from inside the attic.

## 2026-09-21

### Pierre's hand-made kit reaches Unity, and a real house gets built from it

- **CONTENT:** Pierre modelled a structure kit by hand in `_ArtSource/assets.blend` (walls, floors, stairs, tiled roof sections, glass veranda). It is exported as 26 joined modules to `Art/PierreKit` with prefabs and colliders. His file is only ever read, never written.
- **CONTENT:** 17 structure pieces continue his list in his style and materials, in a separate `_ArtSource/assets_extension.blend`: partition with door, carry arch, garage wall, stone cellar wall, two gables, brick quoin, plinth, railing, chimney, fireplace, fence, gate, step. Details and what is still missing in `05_ART/ASSET_LIST.md`.
- **TECH, a scale decision:** the kit is imported at 1.5. It is authored on a 2 m module with a 1.5 m door and the player is 1.80 m. At 1.5 the grid is 3 m and the door 1.2 x 2.25 m. One number on the importer, reversible, the Blender file untouched.
- **DESIGN:** `Map01_PierreKit_House.unity` is a cross-shaped cottage, not a box: two-storey body, front-gabled cross wing with the entrance and porch, veranda west, garage east, cellar stair in a back ell, terrace, chimney, garden. The plan is data (`_ArtSource/house_plan.json`), chosen by a panel of three designs scored against Pierre's reference plans, then machine validated.
- **TESTING, measured instead of believed:** the last generated house could not be entered and its upper floor could not be reached, and a playtester had to find that out. This one was walked by physics queries before anyone opened it: 62 checks, every floor height, all 19 doors and openings with the player capsule, both stairs, spawn to front door. The first pass failed on three real faults (no room at the foot of the cellar stair, two door leaves swinging across a route) and they were fixed before the report, not after.
- **TECH, what the kit cannot do alone:** the stair rises 2.79 m for a 3.06 m storey and its steps are 0.44 m once scaled, above the 0.30 m the player climbs. The map sets each flight on a 0.27 m stone plinth and lays an invisible ramp over it. The kit corner covers half a module per side, which puts windows half a module off the partitions, so corners are closed with a brick quoin.
- **TECH, a trap in the source file:** 673 objects in `assets.blend` carry stale keyframes inherited by duplication. A frame change or a render moves 377 of them. Clear the keyframes in Blender before anything animates that file.
- **NOT DONE:** no contract, no movable objects, no grandmother in this scene. Roof valleys are two roofs passing through each other, fine from outside, visible from inside the attic. The veranda roof has open end triangles.

## 2026-09-20

### The starting items stop being an inventory

- **DESIGN, from the first hands-on QA:** the smoke was judged right and the delivery was not. The cigarette and the beer were viewmodels welded to the camera, which made them a two-slot inventory in a game whose whole premise is that you carry things with your hands. They are ordinary `MovableObject`s now, lying on the ground by the truck, picked up with the grab that already exists. **ADR-007** records it and amends the delivery half of ADR-005 and ADR-006; what a puff does and what a beer does are untouched.
- **DESIGN:** the right button is modal on a held usable. A tap throws it away, holding it smokes. On everything else it still throws the instant you press, because a sofa has nothing to offer a long press and a delayed throw would feel broken. The throw moves to the release for those two items only: 0.18 s is the line, and it is the one thing the automated test cannot check.
- **DESIGN, a gain nobody designed:** the puff is born at the tip of the cigarette, and the tip is wherever your hands are. You can hold it out at arm's length and fog a doorway without fogging yourself, or pull it in with the scroll wheel and blind nobody but you. Aiming the smoke is now something you can be bad at.
- **DESIGN:** throw one away and a fresh one turns up at the van after half a second; the thrown one stays where it landed, as litter. The beer breaks on impact, full or empty, and leaves one flat mark on the floor. Losing a full beer to a bad throw is the funnier of the two outcomes and one rule beats two.
- **TECH:** `HeldUsable` (the base every usable carried object answers to), `CigaretteItem`, `BeerItem`, `StartingItemSpawner` (a place, not a slot: it lays one down and replaces it when the last is gone) and `ItemArt`. `PlayerCigarette` and `PlayerBeer` are deleted.
- **TECH:** a cigarette is 13 mm across and nobody can put a crosshair on that at three metres, so the object you grab is a fist-sized invisible box around a thin visual. Deliberate, and written down rather than discovered later.
- **TECH:** the migration also strips the two dead component entries the old version left in four scenes. A removed script leaves a yellow warning in the inspector forever, so `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` cleans up after ADR-005 and ADR-006 rather than leaving it for someone to find.
- **TECH:** F is shared with `PlayerEquip` and they do not collide, because that one acts on what is in your hands and gives up at once when it is not a wearable. Noted in both files so the next person to add a case knows.
- **TESTING:** the two separate smoke and beer playtests are replaced by one `MoversItemsPlaytestCLI`: both items laid out, the cigarette smoked through its own API, the puffs checked to land on the cigarette rather than on the player, the thrown one checked to still be lying there while a new one turns up at the van, the bottle drunk, broken, and replaced, and every effect checked back to exactly zero once sober.
- **TECH, two traps the test caught and nothing else would have:** `HeldUsable` requires `MovableObject`, which requires a `Rigidbody`, so `AddComponent` on the item brings both along. Adding either one again returns **null**, and the factories threw a NullReferenceException on every spawn: neither item ever reached the ground. And `Awake` fires the instant `AddComponent` runs, so the factory and `Awake` both reached `Build()` and would have doubled every visual piece. Both are guarded now, with the reason in the code.
- **TESTING, a near miss worth remembering:** the first run reported zero errors and was believed for about a minute. The console had been cleared by a recompile, so "no errors" meant "no log". What gave it away was a missing output file: the run had never reached the step that writes it. Check that a test produced what it was supposed to produce, not only that it complained about nothing.

## 2026-09-20

### The bathrobe, rebuilt to the reference, and still not cloth

- **ART:** `model_gown.py` and `SM_Crew_Chest_Gown.fbx` are gone, replaced by `model_bathrobe.py` and `SM_Crew_Chest_Bathrobe.fbx`. The gown was an open short-sleeved tunic; the reference the team supplied is a full-length wrap bathrobe with a shawl collar, a knotted belt, patch pockets and folded cuffs. Those are shapes rather than parameters, so it is built part by part instead of as one swept band. 784 triangles, against 1.2K for the reference asset.
- **DESIGN, an amendment to a hard rule:** the robe closes, so it hides the torso identity panel, and `05_ART/CHARACTERS.md` forbids that. Keeping it hanging open to obey the letter of the rule is exactly what produced the tunic. The collar, the belt and the cuffs now carry the player colour on a second material slot. The rule's purpose holds, four players in a corridor still separate, and the panel is still underneath when the robe comes off. Recorded as an exception for full garments, not a licence for accessories.
- **TECH:** the skirt is weighted to the pelvis rather than by proximity. Bound to the thighs it would scissor open at every step, and cloth simulation is out by the brief. It swings as one cone and the legs travel inside it. The cost is that a high knee can pierce the front, which is why the hem stops at mid calf rather than the floor.
- **TECH, the same failure as the slipper and the boot:** hand written ring sizes put the robe's waist inside the torso and the shirt showed through two wedges at the belt. The ring table is now measured off the body with the arms excluded, and everything that sits on the robe (collar, belt, pockets) is placed through the same interpolation, so changing the silhouette moves them with it.
- **NOT FINISHED, judged by the team:** the shapes read as samurai armour rather than cloth. The silhouette and the parts are right, the surface language is not. Causes, all cheap and none structural: a 12 sided superellipse with hard corners, every part meeting its neighbour in a step instead of a blend, a shoulder ring wide and square enough to read as a pauldron, a cuff that stands too proud, and a skirt that is a smooth cone with no vertical folds. Next pass: rounder sections, sloped shoulders, blended joins, folds in the skirt.
- **DESIGN:** the heart pattern in the reference is deliberately absent. It is a texture and the whole project is flat colour. A direction decision, not an oversight.
- **NOT VERIFIED:** it has never been seen in Unity. Like the slippers since the axis fix and the mirror pane since the slot fix, it waits on one editor pass.


## 2026-09-17

### Grandmother's clothes, and an equip system to wear them

- **DESIGN:** the first lot of wearables is hers, stolen: slippers, glasses, a pink dressing gown. Chosen over the obvious lot (hard hat, harness, gloves) on three counts: every co-op game has a hard hat and none has a stolen dressing gown (R17), what you wear says which room you went through, and the go / no-go is spontaneous laughter, which a hard hat has never produced. The brief's own rule had to be amended for it: a piece pays in a stat **or** in social information the silhouette carries.
- **DESIGN:** the face slot, deliberately shut in the first draft because a balaclava is the most verb-loaded object in the game, reopens for the glasses. They conceal nothing, they humiliate. Open for comedy, shut for concealment until the verb divergence closes.
- **TECH:** `EquipItem` + `CrewEquip` + `PlayerEquip`. A wearable is a `MovableObject` first, so there is no pickup code, no inventory and no UI. Five slots resolve through `HumanBodyBones`, not by name: this rig is Rigify and nothing in it is called "Head".
- **TECH:** `MirrorSurface`, planar reflection on the GrandmaKit wall mirror, upstairs in the bedroom. Verified reflecting in Play. It exists because first person means you never see your own outfit and there is no second player yet.
- **ART:** slippers modelled in `tools/blender/model_slippers.py`, 122 triangles, watertight, left and right as two real meshes because mirroring by negative scale flips the winding. Built to swallow the work boot rather than replace it, which avoids editing the base mesh and a material swap on all four crew variants. Zero of 136 boot vertices outside the shell.
- **ART:** dressing gown in `tools/blender/model_gown.py`, 628 triangles, the only skinned piece. Derived from the body mesh rather than modelled beside it, so it arrives carrying the body's own 27 vertex groups: the skinning is exact and free, and it cannot clip through the torso because it is the torso pushed outward. Worn open, because no equippable may cover the crew identity colour.
- **TECH, three export traps, all measured and all now in the brief:** `bake_space_transform` is ON for rigid pieces and OFF for skinned ones, and the earlier blanket "always off" put the slipper's length on Y and laid it on its back. `apply_unit_scale=True` with `FBX_SCALE_ALL`, because the settings copied from `author_carry_clip.py` delivered a mesh 100 times too small with a compensating 100 on the root. And nothing that places a piece may assign over its root transform, since an import can legitimately carry rotation or scale there. Two of the three looked like modelling errors and were not.
- **TECH:** a fit is authored in the body frame, never the bone frame. On this rig the head bone's local +Z points at the floor, so "4 cm up" in bone space put the glasses 10 cm below the skull.
- **NOT VERIFIED:** the F key. Every equip was driven from a script, never through `PlayerEquip`. The gown is exported but unwired: a skinned piece needs its bones re-bound to the body's and that path is unwritten.


### Sprint, crouch, reach, and a crew that fits its own doors

- **DESIGN:** Shift sprints (x1.6), Ctrl crouches (capsule 1.80 m to 1.00 m, eye 1.60 m to 0.80 m, speed x0.45). Both are hold, not toggle. Crouch beats sprint, you do not jump while crouched, and you cannot stand up under something. No stamina: a meter to watch is a system nobody asked for.
- **DESIGN:** the scroll wheel now pushes the carried object out or pulls it in, 1.0 m to 3.2 m, about 0.35 m per notch, kept between grabs because how far out you hold things is a stance and not a per-object setting. Heavy things cannot go as far out: the sofa stops at 2.8 m and the fridge at 2.4 m, on the same weight factor that already makes them lag and turn slowly.
- **TECH:** no new input conflict, the same modal trick the codebase already uses twice. The wheel rolls the object while R is held and adjusts the reach while it is not. The 90 degree snap that would have paired with it was dropped rather than shipped broken: the obvious keys are Q and E, and E is DELIVER, so a quarter turn at the truck could have settled the contract by accident.
- **TECH, bug caught by the test rather than by play:** standing up casts a sphere upward, and that cast returns the player's own CharacterController. Without the self filter the check always answers "blocked" and the player stays crouched forever. Verified three ways in the live scene: open room clear, slab overhead blocked, slab removed clear again.
- **ART, fixed:** the crew imported at **2.59 m** sole to crown, against a 1.80 m player capsule. Corrected at the source, `male01_1.fbx` scale factor 1 to 0.6956, not on the prefabs: the four are variants, so the model is the one place to change size and a fifth recruit is born correct. Re-import rebuilds a Humanoid avatar, so it was checked afterwards: 22 mapped bones and 39 skeleton entries unchanged, the eye-bone removal from the previous session survived, avatar still valid, `Carry_Idle` still retargets.
- **TECH:** `Map01_GrandmaHouse` has a player in it now, movement only, so the dressed map can be walked at all.
- **CONTENT, three faults found by walking it, none fixed:** the front stoop faces the wrong way, so from the path you meet a 0.95 m wall and the steps then descend toward the house; there is a 0.60 m hole between the back of the stoop and the floor; and the ground floor sits 0.80 m above the terrain with nothing bridging it on any side (veranda 0.10, terrace 0.45). Player `stepOffset` is 0.30, so the house is enterable only because of the jump added the same day. The upper floor is unreachable: `PF_Stairs_Interior` runs from -3.00 to -0.20 and serves the basement only. The floors themselves are sound, ground 25/25 standable, upper 25/25, basement 19/25, and the interiors are bare shells.

### The beer, and the end of the starting inventory

- **DESIGN, FLAGGED:** the beer does something too. Hold F with your hands empty and you drink it; four seconds empties the bottle for good and leaves you as drunk as this game gets, for about 25 s. The question was **asked, not assumed**: ADR-005 had recorded that "give the beer a function too" was not automatically right, so the team was offered three shapes (inert, drunkenness, or strength) and picked drunkenness. **ADR-006** records it, including why strength was refused: it would rewrite the weight rules, and the carry is the only thing a playtest has ever validated.
- **DESIGN:** drunk takes your aim (the view wanders, the horizon tips), your heading (you no longer walk where you point) and your grip (what you carry lags and swings). It never takes your speed and never takes the controls. Losing control is frustrating, being bad at something is funny, CLAUDE.md rule 4.
- **TECH:** `Drunkenness` is a state on the player, kept apart from the bottle that caused it, so the next thing that should wreck the crew feeds the same meter instead of growing a second one. It adds no system: it writes `PlayerController.lookSway` and `moveDrift` and `PlayerGrab.carrySlop`, all new, all `[HideInInspector]`, all zero while sober. A drunk player is the sober player with worse numbers.
- **TECH:** `PlayerBeer` builds a bottle in your other hand at Play and tips it to your mouth while you drink. The bottle is finite on purpose: an endless source is a permanently broken player, which is a punishment with no decision in it.
- **TECH:** `MoversCigaretteCLI` is now `MoversStartingInventoryCLI`, because it installs two items and the old name was a lie. It also refuses to run in Play mode now: a scene edited while the game runs is thrown away on exit, so it used to look like it worked, change nothing, and then throw on save. Found with someone playing in the other window.
- **TESTING:** `MoversBeerPlaytestCLI` drinks the bottle through the game's own `Drink` method rather than by setting fields, then checks that the view wanders, that the wander keeps moving instead of sitting at an offset, that the grip goes loose, that an empty bottle gives nothing back, and that the player is left with exactly zero sway once sober. It shortens the sobering clock for the run and says so in the log.
- **TECH, caught by looking rather than by a check:** the bottle was out of frame. At y -0.24 it fell under the bottom edge of a 16:9 view and the contract panel ate what was left, so the player was told they had a beer and never saw one. Moved to (-0.23, -0.17, 0.44). Changing the default was not enough, because the component was already serialised into three scenes with the old value, so those three were migrated once by hand; from here the value is the designer's. The test now also renders the player camera to a PNG, which is the check that would have caught it: `ScreenCapture` takes the HUD too but does nothing in batch mode, a camera render has no HUD but works headless.
- **TESTING:** run headless after the fact, all three green. Install idempotent (four players, none needing anything). Smoke unchanged by the beer work, which mattered because `PlayerGrab` now multiplies its hold by a grip factor: 0.70 on screen at 1.2 s, 0.51 at 4 s, nothing at 6.9 s, cloud gone at 7.6 s, exactly as before. Beer green.

### The cigarette blinds people

- **DESIGN, FLAGGED:** the cigarette in the starting inventory now does something. Hold RMB with your hands empty and it smokes; every half second it drops a puff, each puff lasts exactly 7 seconds and takes the view of anyone inside it, the smoker included. `GREYBOX_SPEC` called that item "useful for nothing", so this is a design change and not an implementation detail: **ADR-005** records the decision, why the version that spares the smoker was refused (rule 4, funny failure over frustrating punishment), and the three playtest outcomes that would change it.
- **TECH:** four new components. `SmokeCloud` is the puff, a volume plus the particles that show it, simulating in local space so what you see and what blinds you can never disagree. `SmokeVision` sits on the camera, not on the world, so the cloud is shared and the blindness is not: four players will each answer for their own head with no extra code. `SmokeTextures` generates the puff sprite and a tileable blob noise in code, so the feature ships no art. `PlayerCigarette` builds the thing in your hand at Play, two primitives and an ember.
- **TECH:** no new input conflict. RMB throws only while something is in your hands and lights the cigarette only while they are empty, which is the same modal trick the scroll wheel already plays with the rotate key. Pick up a sofa and the cigarette leaves the frame.
- **TECH:** `The Movers > Install Cigarette in All Scenes` puts it on players in scenes that were saved before it existed. Tutorial_01, Map01_House and Map01_Grandma equipped; the catalogue scenes and Map01_GrandmaHouse have no player.
- **TECH, gotcha worth keeping:** `Graphics.DrawTexture` doubles the colour it is handed, its neutral value is (0.5, 0.5, 0.5, 0.5), not white. Measured, not guessed: drawing a 0.42 grey at alpha 0.50 over mid grey returned 0.839 where straight alpha owes 0.460, and halving the colour first returned 0.459. Before the fix every overlay layer landed nearly opaque, the screen went pure white, and making the colours *darker* made it whiter. The halving lives in `SmokeVision.Tint` with the numbers in the comment.
- **TECH:** two entry points to check the smoke without playing. `MoversSmokeCLI.RunProbe` prints the density of a puff over its whole life and needs no graphics device; `RunPreview` renders the player camera with the cloud in front of it and paints the real overlay on top, calling the game's own `DrawSmoke` so the preview cannot drift from what ships. `MoversSmokePlaytestCLI.RunPlaytest` enters Play mode for about ten seconds, watches one puff live and die on the wall clock, and restores the Enter-Play-Mode project setting it had to change.
- **TESTING:** verified in a live Play session rather than on paper. The density envelope reaches 0.000 at t=7.00 exactly; six puffs were alive at once while the button was held, with density 0.994 at the smoker's own eyes; nine seconds later, zero clouds, zero leftover objects, clear screen, clean console. The scripted test also leaves `smoke_gameview.png`, the one frame no offline render can produce: the overlay taking the view while the contract checklist stays readable on top of it.
- **BALANCE, learned by measuring:** a **single** puff peaks near 0.71 on screen and is down to 0.51 by 4 s, not the flat 1.0 the static maths predicts, because the puff rises at 0.35 m/s and is pushed away as it is exhaled, so it leaves your own face while it grows. Holding the button is what blinds you outright, by stacking. That is the better behaviour of the two and it was kept: one drag is a nuisance, a held button is a wall. The playtest thresholds were written down from these measurements rather than from the theory.
- **TECH:** a scripted Play-mode test has to set `Application.runInBackground`. An unfocused editor freezes the player loop while `EditorApplication.update` keeps ticking, so the harness measured a working feature as a dead one for a full run. Saved and restored, like the Enter-Play-Mode options.
- **UI:** the HUD controls strip is 70 px tall instead of 50. The third line printed on top of the second.

### Tutorial_01 re-skinned, placeholder crew, and the URP claim corrected

- **TECH:** the working line is now `main` only. `strat-jo` had been merged and deleted on GitHub; its one surviving local commit (the batchmode build entry point) was cherry-picked and the local branch deleted.
- **TECH:** Tutorial_01 no longer reads as a dungeon. `MoversVisualSwap` mapped greybox names onto `BrokenVector/LowPolyDungeon` (sofa to Bench, vase to Amphora, lamp to Candlestick) and scanned all of `Assets`, so even the fallback name match landed in the dungeon pack. Remapped onto the house kit under `_Project/Prefabs` and scoped to that folder. Television and fridge had no dungeon equivalent and stayed grey boxes: all 12 movable objects now carry a mesh instead of 10. Physics untouched, each object still has exactly one collider and each visual measures the same world size as it.
- **DESIGN:** carried objects can be turned. Hold R and the mouse turns the object instead of your head, scroll rolls it, heavy things turn slowly. Space jumps, sized to climb into the truck bed, flattened by what you carry, with a coyote window. Q and E were the obvious snap keys and are unusable: E already delivers. `GREYBOX_SPEC` records it as system 3, which is what turns the sofa wider than the door from a wall into a problem.
- **CONTENT:** four placeholder crew members, from the Floreswa Low Poly Character Pack. `male01_1` converted from Generic to Humanoid: the Rigify metarig maps every bone Unity requires. Unity's auto-mapper had bound RightEye to the skull-top bone, which would have rotated the head on any clip touching the eyes; that mapping was removed. The four prefabs share one mesh and ten of their eleven materials, only the shirt slot differs, so a fifth member costs a material and a prefab.
- **CONTENT:** the pack ships zero animations, so `Carry_Idle` was authored in Blender, headless, and retargets onto the character through the humanoid layer. Copying the character avatar onto the clip fails (the round trip shifts leg bone lengths by up to 400 mm), so the clip carries its own avatar. The authoring script is `tools/blender/author_carry_clip.py`, which reads the rig axes off the bones rather than assuming one, so it runs on the other two bodies unedited.
- **TECH:** Blender confirmed drivable headless (5.1.2, no GUI and no MCP addon needed), which is how every asset step above was done.
- **DOCS:** the URP claim corrected wherever it was still stated. `ASSET_AUDIT` recorded Built-in RP on 2026-09-16 but the finding was never propagated. Fixed in `CLAUDE.md` rule 6, `PROJECT_STATE`, `TECH_STACK`, `UNITY_SETUP`, `PERFORMANCE`, `THIRD_PARTY`, `DEVELOPMENT_PLAN`, `LIGHTING` and `MATERIALS`. `ADR-001` is amended rather than rewritten: the decision stands as recorded, the pipeline line is annotated above it.
- **DOCS:** the `ASSET_STATUS` register has its first real entries, replacing the example row.
- **TECH:** `Assets/_Recovery/` is gitignored. It is the copy Unity writes of the open scene after a crash, and it was a byte-for-byte duplicate of Tutorial_01.

### Merge of `strat-jo` into the main line
Branch `strat-jo` (benchmark research and the concept-meeting decisions) merged with `main` (deep doc fill, concept lock, and the first working Unity greybox). Both sides preserved.

- **MERGE:** `UnityProject/` taken from `main` untouched. Tutorial_01 runs: grab, carry, truck loading, contract, HUD.
- **MERGE:** ADR numbering reconciled. `main` had taken ADR-003 for the concept, so the netcode ADR was renumbered to **ADR-004**. The duplicate concept ADR written on `strat-jo` was removed in favour of `main`'s ADR-003.
- **MERGE:** the two Meccha Chameleon fiches combined. `main`'s design analysis kept, engine corrected from unknown to Unreal Engine 5, and the verified technical and production facts added.
- **MERGE:** `VIRALITY_PATTERNS` now has both halves, the design properties from `main` and the distribution mechanics from `strat-jo`.
- **MERGE:** `MARKET_MAP` carries Megabonk, Meccha Chameleon and Dear Passengers.
- **DESIGN, FLAGGED NOT DECIDED:** `GAME_CONCEPT` gained an **open divergence** section. ADR-003 says the verb is MOVE / CARRY and that theft must not become the core. The later concept meeting, with both developers, recorded the verb as STEAL UNDER WATCH. The two readings produce different games. Proposed tie-breaker: play Tutorial_01 and see whether carrying alone already generates stories.
- **DESIGN:** `GREYBOX_SPEC` keeps `main`'s Tutorial_01 as the current build, with the larger meeting scope recorded below it and explicitly held until the verb question is settled.
- **DESIGN:** `CORE_LOOP` filled against the current concept, with the alternative reading documented.

### Concept meeting decisions (branch `strat-jo`)
Two concept meetings on 16 and 17 September resolved 22 of 29 open questions, declined 5, and left 2 open. The project moved from "no concept" to "ready to build".

- **DECISION:** the concept meeting recorded the signature verb as **steal under watch**, not carry. On merge this became the open divergence above rather than an accepted ADR, because `main` had already locked MOVE / CARRY in ADR-003.
- **DECISION:** ADR-003 rewritten and **accepted**, renamed to `ADR-004-netcode-free-only`. Free solutions only, Steam or Unity Netcode, host is a player, no backend, 4 players. Photon excluded. The earlier Epic Online Services proposal is demoted to a documented alternative.
- **DESIGN:** `GAME_CONCEPT` rewritten, the five candidates moved to an archive section. `CORE_LOOP` filled, including the two-ledger idea: the contract and the theft use the same verbs.
- **DESIGN:** a fuller greybox scope was specified. Scope: grandmother's house, 10 rooms, about 80 objects, cat and fish as living objects, one NPC with a single interaction, A* patrol only, static truck, no shop, no procedural generation, no detection AI.
- **DESIGN:** delegated decision taken, garage over barn, with reasons recorded in the spec.
- **PRODUCTION:** `MILESTONES` filled with the team's own plan. Greybox, V1, one month of communication in parallel with map production, beta. Earlier agent-produced duration estimates were rejected as unreliable and removed rather than argued.
- **MARKETING:** `TARGET_AUDIENCE` filled. Casual evening-with-friends audience, groups of four, discovery through French-speaking streamers first, purchase driven by trend effect, non-evergreen by design.
- **MARKETING:** `WISHLIST_STRATEGY` sequencing replaced by the team's real plan, triggered by the tutorial working end to end in V1.
- **BUSINESS:** `PRICING` updated. Range $4.99 to $14.99, volume over margin, settled by the final look and the wishlist curve. Caution recorded that the upper end and the volume strategy pull in opposite directions.
- **TESTING:** `PLAYTESTS` gained the external protocol, 16 players in 4 teams of 4, with the two stranger teams as the real signal.
- **ART:** `ASSET_STATUS` gained the pipeline decision. Free packs as the base, completed by AI 3D generation. Steam disclosure accepted as a consequence.
- **DOCS:** `RISKS` gained R21 (the reason to buy is unwritten) and R22 (unsigned team agreement), and R18 marked largely closed by ADR-004.
- **DOCS:** H3 retired from `PROJECT_STATE`, H6 added. `OPEN_QUESTIONS` and `QUESTIONS_RESTANTES` rewritten as a decision journal plus a 2-item open list.

**Still open:** the reason to buy (Q2, deferred to just after the greybox) and the commercial change-of-direction thresholds (Q21). Five team questions declined deliberately.
### Grandma Kit: full asset list frozen + interior furniture batch (Blender)
- **DOCS:** froze the complete asset inventory for the grandma-house game in `05_ART/ASSET_LIST.md` (~340 targets across architecture, furniture per room, kitchen, bath, bedroom, living, office, basement, garden, cat, food, props, nature, materials), with naming/family/scale conventions and folder structure. Scope decision (Pierre): list first, he drives production, start with a vertical slice. Max interior scope; cat deferred; roof pitch left non-uniform (he will adjust).
- **ART:** modeled interior furniture batch 1 (14 pieces, low-poly, no textures): Bed_Old, Nightstand_01, Dresser_01, Wardrobe_01, Sofa_01, Armchair_01, Rocking_Chair, Coffee_Table, Side_Table, Bookshelf_01 (with books), TV_Old, Fireplace_Living, Dining_Table_01, Dining_Chair_01. Exported to `Assets/_Project/Art/GrandmaKit/Furniture`.
- **ART:** modeled interior batch 2 (12 pieces): kitchen (Counter_Straight, Cabinet_Upper, Sink, Stove_Old, Fridge_Old retro, Range_Hood, Kitchen_Island), bathroom (Bathtub_Old clawfoot, Toilet, Sink_Bath pedestal, Shower), office (Desk). Exported to `Assets/_Project/Art/GrandmaKit/{Kitchen,Bathroom,Office}` with mirrored prefabs.
- **ART:** modeled batch 3 (16 pieces): lighting (Table_Lamp, Floor_Lamp, Chandelier), decoration (Vase_Plant, Picture_Frame_01, Wall_Clock, Rug_01), basement (Shelving_Unit_Cave, Barrel, Crate), garden (Garden_Bench, Wheelbarrow, Mailbox), nature (Flower_01, Mushroom_01, Rock_01).
- **ART:** modeled batch 4 (18: kitchenware + food) and batch 5 (14: tools, books, luggage, sewing, special). Running total: 101 kit pieces (27 architecture + 74 interior/props/food/garden/nature), all FBX + flat-color drag-and-drop prefabs with colliders. Palette extended (Pot, Mush, FabMint, Bread, Cheese, Wine). Prefab builder mirrors the whole Art/GrandmaKit tree into Prefabs.
- **ART:** modeled batch 6 (16: furniture variants + garden + nature): Sofa_02, Armchair_02 (wingback), Display_Cabinet, Coat_Rack, Chest, Wood_Stove, Vanity, Well, Planter_Box, Birdhouse, Garden_Arch, Flower_02, Mushroom_02, Rock_02, Bush_01, Grass_Tuft. Running total: 117 kit pieces.
- **ART:** modeled batch 7 (16: textiles + lighting/deco + special + misc): Curtains, Cushion_01/02, Rug_02, Wall_Sconce, Candlestick, Oil_Lamp, Painting, Mirror_Wall, Vase_02, Telescope, Bird_Cage, Dress_Form, Model_Ship, Crucifix, Alarm_Clock. Running total: 133 kit pieces.
- **ART:** modeled batch 8 (18: archi variants + garden + lab + big furniture + misc): Wall_2m, Wall_Corner, Window_Round, Door_Interior, Stairs_Spiral, Fence_Stone, Trellis, Fountain, Scarecrow, Lab_Table, Microscope, Flask_Set, Piano, Secretary_Desk, Keys, Glasses, Perfume, Medicine_Box. Running total: 151 kit pieces covering every list category (most with variants).
- **ART:** structure-completion lot (12): interior partitions (Wall_Interior_Solid/Door/Opening), Wall_1m, Wall_3m, Wall_Corner_Inner, Terrace_Deck, and a veranda set (Veranda_Panel/Corner/Roof/Door + faceted rounded Veranda_Bay). Fills the gaps for building + partitioning the house with a terrace and a straight or rounded conservatory. Running total: 163 pieces.
- **ART:** consolidated all 163 pieces into a single editable source `C:\GameProject\_ArtSource\GrandmaKit_All.blend` (22 collections by category, upright, correct scale, flat colors). Fixed a Blender FBX re-import gotcha (pieces came back 100x and lying down; corrected to metric scale + standing).
- **TECH:** generalized the prefab builder (menu The Movers > Build Kit Prefabs) to scan CottageKit + GrandmaKit, mirror the folder tree into `/Prefabs`, and color per submesh from any `*_mats.json`. 14 furniture prefabs generated (flat color + collider).

### Cottage Kit: complete modular low-poly building asset pack (Blender)
- **ART:** modeled a complete modular low-poly building kit in Blender ("Cottage Kit", 27 pieces, no textures, detail carried by geometry): walls (solid / window / door / garage) with plinth, cornice, corner boards and board battens; shuttered multi-pane window; decorative Craftsman gable end (fascia, tie beam, king post, struts, vent) plus a plain gable infill; roof slope and ridge cap; dormer; chimney; bay window; porch column (tapered on a pier), beam and railing; deck tile; exterior stoop steps; interior stairs; front door (paneled, with a hood) and sectional garage door; brick foundation strip; 4x4 floor; stepping stones; fence section, post and gate; hedge; bush. Exported to `Assets/_Project/Art/CottageKit/SM_*.fbx` with the validated upright / correct-scale settings.
- **TECH:** generated flat-colored, collider-ready drag-and-drop prefabs (`PF_*` in `Assets/_Project/Prefabs/CottageKit`) via `CottageKitPrefabBuilder` (menu The Movers > Build Cottage Kit Prefabs); colors are assigned per submesh from a Blender-exported material map (`cottage_kit_mats.json`), so they survive any FBX material import mode. Added a browsable `CottageKit_Catalog` scene (menu The Movers > Open Cottage Kit Catalog). Verified in-engine: pieces import upright, correct scale, colored, with mesh colliders.
- **DESIGN:** per Pierre, the deliverable is the KIT itself (he assembles Map 1 in Unity), quality target = cozy low-poly cottage references (Craftsman brackets, shutters, columns, brick base). Module = 4m walls, 3m high.

### Map 01 environment greybox (modular Blender kit)
- **ART:** modeled a modular architecture kit in Blender (21 pieces, greybox, no textures, boolean-cut openings): exterior walls (solid / door / window / big-opening / bay), interior walls (solid / door / opening), window frame, door, 4x4 floor, straight stairs, chimney, garage roller door, fence, gate, ladder, two gable roofs, plus rock and garden table. Exported to `Assets/_Project/Art/{Architecture,Props}` with `bake_space_transform` (upright, correct scale).
- **CONTENT:** assembled the full Map 01 environment scene `Map01_House` (`Map01EnvBuilder`, menu The Movers > Build Map 01 Environment): 2-storey house (multi-room ground floor + attic, gable roof, framed windows, front door + rear bay), attached garage, front terrace + table + railings, underground cellar with interior stairs, small hangar, quick farm (barn + fenced field), garden (reused Broken Vector trees, rocks, driveway, front fence + gate). Walls use mesh colliders so door/window openings are walkable. Runs in Play, 0 errors. Structural / environmental only (empty interior, no textures) per request; objects and physics come after.
- **TECH:** generated drag-and-drop prefabs (`PF_*` in `Assets/_Project/Prefabs/{Architecture,Props,Furniture}`) from the kit models, each with a shared material + mesh collider (`KitPrefabBuilder`, menu The Movers > Build Kit Prefabs). The environment scene is assembled from prefab instances, so making pieces breakable later = add one component to a prefab and every placed instance inherits it.
- **ART:** upgraded the kit from plain blocks to designed low-poly assets: beveled edges, wall plinth + cornice, framed windows (sill / mullion / lintel), panelled door with handle, pitched roof with ridge cap + fascia + overhang, chimney with cap, sectional garage door, post-cap fence + gate, balcony, porch post. Rebuilt the house as **1 storey + accessible attic** (gable roof directly on the ground-floor walls) with a covered entrance porch + roof chimney. 28 prefabs total.
- **ART:** confirmed only 5 Asset Store packs exist in the project (Cars, Storage, Tree, Ultimate Low Poly Dungeon, Pistol); no modern house/fence/rock pack. So created a **modern low-poly house pack** with a flat-colour palette (no textures, Broken Vector style): cream walls, terracotta roof, white window frames, wood trim, blue front door, grey garage door. **Reused real Broken Vector assets** for props: `Table_Big` + `Bench` (terrace), `Balcony_Railing` (fences), `Car_2_Blue` (driveway), Storage barrels + crates (yard/garage). Front door placed in the entrance. Result reads as a coherent modern cottage.
- **CONTENT:** polish / levelling pass on `Map01_House`. Reused props now **ground-snap** (the mesh bottom is placed exactly on the target surface), which fixed the sunk car, floating railings and crates in one go. Replaced the flat ground cube with a **Unity Terrain** (flat plateau under the property, gentle Perlin hills beyond, a lowered pocket under the cellar so the stairs stay passable; green flat material). Terrace: `Table_Big` -> `Table_Small`, benches turned to face the table, porch posts + door seated on the deck. Shed rebuilt as a lean-to (sheet roof sized to its posts) instead of the oversized gable. Verified by editor captures + object bounds; walkthrough (cellar pocket, hills) still to playtest.

### Unity MCP wired + Map 01 greybox built (driven through the Editor)
- **TECH:** fixed the Unity MCP connection. The `.mcp.json` `unity` entry used the wrong transport (`unity.exe mcp`, connected but 0 tools); switched to the Editor's "Unity MCP Server" relay (`unity-mcp` = `%USERPROFILE%\.unity\relay\relay_win.exe --mcp`, 54 tools). Mechanism documented in `03_TECHNICAL/ASSET_AUDIT.md` and memory.
- **TECH:** audited the Unity project. Built-in RP (not URP); imported Broken Vector Storage / Cars / Trees / Dungeon packs; off-theme Pistol pack flagged. Full inventory + REUSE/ADAPT/CREATE in `03_TECHNICAL/ASSET_AUDIT.md`.
- **CONTENT:** built `Map01_Grandma` greybox via editor menu (`Map01Builder`): ground floor (4 zones, narrow-door sofa puzzle) + upstairs + ramp stairs + garden (real trees) + open-bed truck; 11 movable objects (8 required); contract + HUD; debug tools (R reset, T respawn). Clean hierarchy `_Systems/Environment/Gameplay/Player/Lighting/UI`. Runs in Play with 0 errors; feel not yet playtested.
- **ART:** made 5 low-poly furniture models in Blender (sofa, bed, table, TV, fridge), exported to `Assets/_Project/Art/Furniture/SM_Furniture_*.fbx` and wired in. Boxes / dresser / suitcase reuse Broken Vector prefabs.
- **DOCS:** synced `PROJECT_STATE.md`. `CORE_LOOP.md` still a stub to fill.

## 2026-09-16

### Concept locked and first Unity greybox (branch `main`)
- **DECISION:** ADR-003, concept accepted. **The Movers**, a 1-4 player co-op physical moving game. Verb MOVE / CARRY.
- **DESIGN:** `GAME_CONCEPT` and `GREYBOX_SPEC` written for Tutorial_01.
- **TECH:** Unity project created (6000.6.0f1). `GreyboxBootstrap`, `PlayerController`, `PlayerGrab`, `MovableObject`, `TruckCargo`, `GameHUD`. Grab, carry, truck loading, contract and delivery work. Primitives only, single player, no networking.
- **TECH:** greybox rebuilt as visible editor objects rather than runtime-only, plus the Tutorial_01 scene and greybox materials.


### Deep fill pass across folders (01, 03 to 13)
- **RESEARCH:** added `DEVELOPMENT_CONTEXT.md` (team size, dev time, background per game) and two games (Meccha Chameleon, Megabonk); filled the virality / social / replayability pattern files.
- **TECH:** architecture, C# conventions, data (no database), save (minimal), input, audio system, physics, event system, object interaction, performance, build, debugging, third-party. OOP + modular + config-driven + no backend by default. Gameplay/detection/AI get principles pending the concept.
- **ART:** environment, characters, props, materials, textures, lighting, VFX, animation, specs (low-poly, free-first, lighting-driven, minimal rigging).
- **AUDIO:** direction, sound design, music, voice, ambience, asset list (free / open-source SFX, minimal music).
- **PRODUCTION:** milestones, tasks, backlog, priorities, content/asset/build pipelines, readiness checklist.
- **MULTIPLAYER:** host-authoritative Steam P2P approach across sync, authority, lobby, connection, disconnect, host migration (deferred), voice, cheating (minimal), performance, testing.
- **BUSINESS:** model, pricing, budget, revenue, third-party costs (premium one-time, 50/50, EU selling worldwide).
- **STEAM:** setup, store page, capsules, screenshots, trailer, playtest, wishlists, checklist.
- **MARKETING:** competitors (from the benchmark), content / creator / social / trailer / clip / wishlist / launch strategy. Positioning, USP and audience kept light pending the concept.
- **RELEASE:** plan, build, QA, submission, launch, creator outreach, support, incident response, post-launch.
- **DESIGN:** `DESIGN_PRINCIPLES` derived from VISION. Only `02_GAME_DESIGN` detail remains, pending the concept.

### Benchmark pass: "vibe coded" hits (branch `strat-jo`)
Studied two 2026 games reputed to be AI-built, Meccha Chameleon and Dear Passengers. Neither is. Both are made by experienced teams, and Steam's January 2026 rules put AI code assistants outside disclosure entirely.

- **RESEARCH:** two new `GAME_ANALYSIS` fiches. Meccha Chameleon (Unreal 5 + Epic Online Services, 2 people, 2 months, 20 M+ copies, 340 k peak CCU, zero ad spend, zero server cost, shipped executable still named `PenguinHotel.exe`). Dear Passengers (Unity, FLEXUS 70+ staff, unreleased, 2 M+ wishlists, netcode and player count never stated).
- **RESEARCH:** filled `VIRALITY_PATTERNS` (9 distribution patterns, the reveal artefact as the whole campaign, the anti-trailer, the beta as seeding, wishlist softness, instant cloning).
- **RESEARCH:** `SUCCESS_PATTERNS` gained K (reusable capital beats a lucky concept) and L (the cheapest asset is sometimes the mechanic). `DIFFERENTIATORS` gained BLEND. `MARKET_MAP` gained two rows and lost the absolute "Unity everywhere" claim.
- **TECH:** networking default direction moved from Steam Networking to Epic Online Services via its official Unity plugin, free at any scale. Rationale is cost: per-CCU pricing at the scale of the games we study would exceed our entire budget many times over.
- **DECISION:** ADR-003 written as **Proposed, not accepted**. Blocked on Q5 (player count) and Q9 (minimum architecture), both Jonathan-tagged.
- **TECH:** `TECH_STACK` gained a note on Valve's January 2026 AI disclosure rules. Code assistants are out of scope; shipped generated assets are not.
- **BUSINESS:** filled `PRICING` with the corpus price table and the wishlist conversion data. Working band $6 to $9, not a decision.
- **MARKETING:** filled `WISHLIST_STRATEGY` with the reveal sequencing and the forecasting rule.
- **DOCS:** `RISKS` gained R17 to R20 (instant cloning, netcode cost scaling with success, wishlist-based revenue forecasting, revealing before the build can follow up). `OPEN_QUESTIONS` gained Q22 to Q25. `RESEARCH_SOURCES` gained 21 sources.
- **DOCS:** `PROJECT_STATE` updated, including the unresolved tension between ADR-002 (spec first) and the build-first method used by the team we studied. Flagged, not decided.

## 2026-09-15

### Repository setup
- **TECH:** created the decision repository `C:\GameProject` (full structure, `CLAUDE.md`, `PROJECT_STATE.md`), pushed to private GitHub `Miche1-Pierre/game-project`.
- **RESEARCH:** benchmark "Atlas Coop Viral" transferred into `01_RESEARCH/` (game fiches, mechanics matrix, patterns, design space, white spaces, sources).
- **TECH:** Unity environment verified (CLI beta.8, editor 6.6.0f1, Personal license, URP template). Unity project NOT created, by decision (ADR-002).
- **DECISION:** ADR-001 (Unity 6), ADR-002 (spec first).

### Guided documentation pass (with Pierre)
- **DOCS:** filled `OBJECTIVES`, `VISION`, `OPEN_QUESTIONS` (21, Jonathan-tagged), `RISKS` (16, P/I/M/Trigger).
- **BUSINESS:** `TEAM_AGREEMENT` (50/50 proposed, pending) + `IP`/`PROFIT`/`LEGAL`/`TAXES` pointers; `COST_MODEL`.
- **ART:** `ART_DIRECTION` and `STYLE_GUIDE` (silhouette-first, low-poly not locked).
- **DESIGN:** neutralized `GAME_CONCEPT` (candidates unranked).

### English standardization + game analyses
- **DOCS:** whole repository standardized to English (streamer guardrail added to CLAUDE.md, timeline to ROADMAP, time budget to CONSTRAINTS).
- **RESEARCH:** the game analysis fiches completed.
- **TESTING / ANALYTICS:** filled (lean, grounded in OBJECTIVES/RISKS).
