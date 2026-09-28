# PROJECT STATE

_Living dashboard. The agent reads it at start and updates it at end of session. Last update: 2026-09-28, DEV 2 wired on Map01 and checked by script (ADR-013)._

**PHASE:** Vertical slice built (ADR-009) and dressed (ADR-010: wooden UI with key hints, title menu, loader, target indicators, smoking and drinking, synthesised audio). Waiting to be played by two people, now possible from two PCs (ADR-011).

**CONCEPT:** The Movers. A 1-4 player co-op physical moving game. Contract, carry, load the truck, get paid, upgrade. See `02_GAME_DESIGN/GAME_CONCEPT.md` and ADR-003.
**CORE LOOP:** Carry the owner's belongings out and load them into the truck, while physics and geometry fight you. See `02_GAME_DESIGN/CORE_LOOP.md`.
**SIGNATURE VERB:** STEAL UNDER WATCH, the moving contract as the cover. Pierre's decision of 2026-09-26 (ADR-009), matching the concept meeting; the other developer to confirm. The divergence below is kept for its history.

**CURRENT HYPOTHESES:**
- H1: social friction readable in under 10 s is the real engine, to exploit as explicit intent.
- H2: the clip as an output of the loop is a white space.
- H4: reusable capital, not concept quality, is what makes a fast build possible.
- H5: the reveal artefact is the entire marketing campaign, and third parties distribute it.
- H6: the comedy comes from the physical problem the game never asked, such as a sofa wider than the door.

**RETIRED:** H3 (players as the detection system rather than NPC AI). Both readings of the concept put the players on the same side, so the comparison does not apply. Retired 2026-09-16, not invalidated.

**VALIDATED:** the carry. Playtest 001, solo: picking up and moving an object was judged correct after tuning ("c'est parfait"). That is the feel, not the loop.
**NOT VALIDATED:** the go / no-go. Spontaneous laughter has not been tested, and cannot be solo.
**INVALIDATED:** "Unity everywhere" as a corpus rule. The largest 2026 hit is Unreal 5. Our Unity choice stands (ADR-001) as a preference, not as a benchmark finding.
**RUNNING EXPERIMENT:** none
**CURRENT BUILD:** **Tutorial_01 exists, runs, and has been played.** `UnityProject/` (Unity 6000.6.1f1, pinned to Direct3D11: D3D12 crashes the editor on a hybrid NVIDIA plus Intel laptop). Grab, carry, truck loading, contract, delivery, HUD. All twelve objects show a real mesh from the house kit (`_Project/Prefabs`, GrandmaKit). The first pass used the dungeon pack, which read wrong; remapped 2026-09-17. Single player, no networking. Open the scene and press Play.
Added 2026-09-17: **Space to jump** (flattened by the weight you carry), **hold R to turn the object in your hands** (mouse turns, scroll rolls), **scroll to push the carried object out or pull it in** (1.0 m to 3.2 m, less for heavy things), **Shift to sprint** and **Ctrl to crouch**. The rotation exists because the spec's one emergent problem, the sofa wider than the door, had no solution without it. The wheel is modal: R down rolls, R up adjusts reach, so the two never clash. None of it is played by a human yet: compile and console are clean and the logic is tested, the feel is not measured.
Crew scaled to 1.80 m the same day, see the note in `05_ART/ASSET_STATUS.md`. The pack imported at 2.59 m.
**`Map01_GrandmaHouse` now has a player in it** (movement only, no truck, no contract, no movable objects) so the dressed map can be walked. Walking it found three geometry faults, none of them fixed: the front stoop faces the wrong way, so from the path you meet a 0.95 m wall and the steps then descend toward the house; there is a 0.60 m hole between the back of the stoop and the floor; and the ground floor sits 0.80 m above the terrain with nothing bridging it, on every side (veranda 0.10, terrace 0.45). The player `stepOffset` is 0.30, so the house is enterable only because of the jump added the same day. The upper floor is unreachable: `PF_Stairs_Interior` runs from -3.00 to -0.20 and serves the basement only, while the ground floor is 0.80 and the upper floor 3.80. Interiors are bare shells. Floors themselves are sound: ground 25/25 standable, upper 25/25, basement 19/25.
**The grandmother's house is playable, 2026-09-25, at Pierre's request.** `Map01_PierreKit_House` is the cross-shaped cottage built from Pierre's hand-made kit, now furnished and wired with the Tutorial_01 systems and nothing else: grab, carry, contract, truck, HUD, cigarette, beer, equip. 102 movable objects over three levels, 12 of them on the contract, 26 fragile, the rest paid as extra value at delivery; a box truck backed up to the driveway gate with a ramp; a 15 minute timer. Every door, both stairs and nine carry routes were walked with the player capsule, and a Play run left every object asleep, none broken, console clean. It is not a decision on the verb: the grandmother is a static body on her veranda, and the owner's patrol, the theft ledger, the cat, the fish and the window entry are not built. It does answer something Tutorial_01 cannot: whether the carry survives a real house, with stairs, a cellar and a sofa that has to turn in a doorway. Detail in `changelog/CHANGELOG.md` (2026-09-25) and `05_ART/ASSET_LIST.md` section 11.
**Later the same day, Pierre's second pass on the house (ADR-008).** **Everything breaks except floors, roofs and stairs.** Window glass is cut into 128 panes at Play, and 196 structural pieces and 102 movables get one shared health model. A smashed object shatters into physical debris and leaves the checklist, billed at full value. **A crate of six grenades in the cellar:** hold RMB to pull the pin, release to throw. A blast lights any grenade in the open within 5.2 m, and a floor, wall or shut door stops the chain. **Four pockets on keys 1 to 4** hold the object itself, and the crew starts with the smoke and the beer in them. **E opens and closes** 14 doors, 21 windows (two outward casements each) and the up-and-over garage door, and it gives way to DELIVER when nothing is under the crosshair. The house is raised 0.30 m with a step at each entrance. The veranda stands on a plinth base, a ceiling closes the well over the cellar stair, and the grandmother's car stands on the driveway, nose to the garage door. The grandmother is now her own modelled body, 1.58 m, with an idle loop, looking out of the veranda. **Tested in Play, not played by a person:** doors and windows open and close, a thrown object hurts a pane, blasts take out a door leaf, a window's glass and a whole wall, an object smashed in the hands frees them, grenades go off in a pocket and in the hands, and the cellar chain fires one grenade per frame or later. Two faults were found and fixed during that run: the chain lit grenades through floors, and a blast could miss colliders when debris filled its buffer. The console showed zero errors. **Open tuning calls for Pierre are listed in ADR-008,** and the scope change should be read by the other developer.

**The vertical slice, 2026-09-26 (ADR-009, `03_TECHNICAL/SLICE_ARCHITECTURE.md`).**
- **Two local players** in split screen: P1 on keyboard and mouse, P2 on a pad; F1 swaps the keyboard for solo tests.
- **An autonomous grandmother:** NavMesh, routine and activities, senses, patience, the key handover. Softened at Pierre's request: a bit deaf, and a last warning before she calls the police.
- **The run:** a session with an intro, the contract, delivery at the truck, an itemised settlement and a theft ledger.
- **Structural destruction:** pre-fractured walls, a support graph, falling roofs, cracking glass.
- **A drivable truck** with physical cargo.
- **Kit fixes:** the kit's normals and holed walls are fixed, and the windows have real sashes.
- **Animation:** 22 humanoid clips.

**Hot-fix 2026-09-27, body and shadow in sync:** one skeleton now drives the crew. The first-person arms are drawn on the body's bones, so the shadow and the other player's view show the same arms. What the hands hold is put on the body by IK. Walk and sprint play at their real ground speed, jumps tuck, and the crouch goes as low as the eyes. Measured in Play (hands 0.000 m apart, head at the eyes crouched), not played by a person. At rest your hands now hang out of view, as the shadow's do: a change from the "hands visible" pass, to confirm with Pierre. Detail in `changelog/CHANGELOG.md`.

Every automated Play test block passes in the shared editor, 0 console errors. The one exception is a blast-frame target, tuned since. Nobody has played it with two people yet: that remains the only test that matters. Detail in `changelog/CHANGELOG.md` (2026-09-26).

**Online co-op, 2026-09-27 (ADR-012, `07_MULTIPLAYER/NETCODE_SLICE.md`).** Built on `feat/online-coop`, merged into main the same day.
- **What exists:** Pierre and Jonathan can play `Map01_PierreKit_House` from two PCs. Host = P1, client = P2, one player per PC, each on keyboard, mouse and the first pad at once. NGO carries connection and messages only, over Unity Relay (free tier, a 6-character join code) or direct IP. The host simulates everything; the client runs its own body and draws the rest. Offline play is unchanged, and no net object exists offline.
- **How to play:** both run the same development build (`BuildGreybox.NetClient()`, to `UnityProject/Build/Movers.exe`); the id digest refuses a mismatch. Host: "Jouer en ligne", "Héberger", send the code, "Lancer la partie". Client: "Jouer en ligne", "Rejoindre", paste the code, "Se connecter". If Relay fails: "Héberger en IP directe" over LAN or a free VPN. Detail in NETCODE_SLICE 17.4.
- **Tested:** two builds on one PC pass the automated loopback test over direct IP and over Relay (host 12 of 12 checks, client 21 of 21), including the replay for both and a disconnect either way. 2 to 3 KB/s steady, 70 to 90 ms RTT over Relay, no payment asked. An editor host and a build client share the same ids. The offline smoke (split screen) is clean. Offline baseline: 1bd50e5 (step 0).
- **Not tested:** a session from two homes (and its 60 fps), a real pad online, the client driving the truck, typing the code in the join field, a wrong code, a pulled cable. Solo P1, solo P2 and the older CLIs were not rerun.
- **Known limits:** the client's own grab, throw, open and drive land one round trip late (about 80 to 170 ms over Relay). Debris, smoke and splats differ between the screens. Pause does not stop the world. No join mid-run, no reconnect, no host migration: the host leaving ends the run. The test bot's pane throw is flaky (a bot fault, not the netcode).
- **Pierre's calls:** the defaults are kept. Either player skips the intro and replays for both, the client's Menu leaves alone, the host's ends it for both, and a dropped player stays as an idle body.

**DEV 2, destruction, world and gameplay, 2026-09-28 (ADR-013, `03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md`). Built, wired on Map01, checked by script in Play and online by the loopback bots; not yet played by hand.**
- **What:** Pierre's list after QA ("fractures judged unrealistic, damage insufficient"): damage from mass and relative speed, bigger and finer grenade breaches, debris that stays and cleans up out of sight, the truck as a ram at up to 90 km/h, breakable garden pieces, a reliable grandmother ladder with one readable last warning, the police flee (call, countdown, cars on the road, exit checkpoint, interception, arrests) as a reusable marker-driven mission, a 20 minute timer.
- **How:** frozen contracts (c4db70c), 8 parallel tracks, an integration merge on `dev2/destruction-gameplay`, then the Unity stage (spec section 15). `NetSession.Protocol` 2.
- **Unity stage, done 2026-09-28 in the editor:** Map01 has a `Mission` root (the `Route_Street` path, the `MissionRoute` Road_West 580 to 0, street, Road_East 0 to 360 with its arrival at (0, 0, -23.6), the `PoliceSpawn` and two parked police cars at Road_West s 580, the exit checkpoint at Road_East s 320), `EscapeMission` on `_Systems`, the passenger seat on the truck cab (seatIndex 1) and the two driveway posts under the Garden. The truck's `TruckTuning` reference was lost on the first import after the branch switch (the asset imported before its script compiled) and is restored. The land colliders stay at 520 m (decision 12 open): the roads carry their own colliders over their whole length, so the parked cars at s 580 stand on tarmac.
- **Fixed in the Play pass:** the lead police car reached the house 84 m (about 4 s) late, slowed by the bends of Road_West; it now drives at the pace the remaining distance needs, and arrives at 75.0 s.
- **Decisions for Pierre:** the 18 defaults of spec section 16 (flee rules, escape at half pay, exit and spawn places, passenger seat, re-fractured walls, garden breakables, truck feel, arrested pose).

**DEV 2 checks (spec section 12), measured 2026-09-28 by script in the editor's Play mode on Map01 (editor unfocused, 25 to 30 fps baseline). Anything not listed as measured was not run.**

| Check | Target | Result |
|---|---|---|
| 1 Walls: grenade on the floor 0.25 m from a plain exterior wall (garage south wall) | at least 50 %, rubble | 9 of 20 chunks out (45 %: 7 detached, 2 fell); slightly under |
| 1 Grenade 0.8 m out, 0.6 m high, same wall type | 25 to 45 % at 1.0 m | 20 of 20 (14 detached, 6 fell): the whole module went; too strong at this height |
| 1 Five thrown grenades; mid-room; 1.0 and 1.5 m | spec rows | not run |
| 1 `LastFrameBlastMs`, `MaxFrameMsAfterBlast` | at most 16 ms | editor only: blast frame 23 ms warm (62 ms for the session's first blast), worst of the 5 frames after 118 to 140 ms over the median; two grenades in the living room 36 ms and 64 ms. Over budget in the editor; to measure in a build |
| 2 Throws at a plaster wall (standing throw 6 m/s) | cup 0; standing sofa no mark; sprint sofa cracks; piano a hole | cup 0.3 kg: 0 HP; sofa 80 kg: 31 HP of 1400, no chunk; sofa at 12 m/s (above the 8 to 9.6 calibration): 761 HP, 6 of 20 chunks out; piano 180 kg: 443 HP, 4 of 20 chunks out |
| 3 Drive on Road_East, empty | 0 to 50 at most 4 s, 90 km/h, stop from 90 at most 45 m | 0 to 50 in 3.34 s, top 88.9 km/h, 36.9 m to stop from 88 km/h |
| 4 Ram: garden fence at 30 km/h | keeps at least 90 % | 33.1 to 28.8 km/h across the fence (87 %, about 91 % net of the coasting loss); 3 fence panels and 3 hedges broken; then 28 to 15 km/h over 8 m of garden grass. `ram.LastSweepMs` 0.03 to 0.06 ms. A tree behind the fence stops the truck (solid by design). Walls at 10 to 90 km/h not run |
| 5 Keypad 8 (`DebugCallPolice`) during the intro | starts the contract and the flee | yes, "POLICE IN" 75 s, clock 1200 s |
| 5 Lead car at the end of the countdown | at the house | before the fix 84 m short; after it 2 to 3 m from the arrival at 75.0 s |
| 5 Drive to the exit (left 15 s before the police) | YOU GOT AWAY | Completed, escaped: "You got away: the contract is void", "Sold from the truck: 3" +248, "Fine: 1 arrested" -500 (P2 left behind, decision 5) |
| 5 Truck leaves as the police arrive | BLOCKED then intercepted | the lead car overtook at 100 km/h and stopped the truck: BLOCKED (4 s), then "The police stopped the truck: the contract is void" |
| 5 Surrounded at ESCAPE 0; arrests on foot | spec rows | not run |
| 6 Grandmother ladder | every stimulus moves the bar; warning 25 s; a noise does not end it; an offence of 2 or more after 3 s calls | breakage in her sight -6.5, a blast 13 m away outside -4.5, witnessed thefts -15 each; 35.5, 20.5, 5.5, 0: LAST WARNING 25 s; a world blast 1 s in did not end it; a theft 4 s in called the police at once. A carried theft only counts after 2 s in her sight (my scripted carry never was) |
| 7 Debris during two grenades | at most 90 spawned per frame, 450 dynamic | 90 spawned in the blast frame, peak 102 pieces; 15 dynamic and 87 frozen after 300 frames |
| 7 Console | 0 errors | 0 errors in the final Play session (errors seen once only after a script recompile during Play) |
| 7 Draw calls; Tutorial_01 after a flee | spec rows | not run |
| Online: loopback on two protocol 2 builds (direct IP) | host and client all PASS | host 12 of 12 PASS; client 19 of 20: `3-pane-broken` failed (the known flaky check, 2 passes in 5 on 2026-09-27: no run-up for the chair). Same ids (546), digest and tracked bodies (128) on both; peak down 14.4 / 17.0 KB/s; 0 console errors; no exception in either player log |
| Online: client drives, rides as passenger, is arrested, 100 km/h chase; NetStats peak with grenades and a 90 km/h ram; pool on and off | spec rows | not run (need two people) |

**The starting items, reworked 2026-09-20 (ADR-007).** A cigarette and a beer lie on the ground by the truck when the job starts. You pick them up with the grab that already exists, carry them, drop them, throw them. Holding one, the right button is modal: a tap throws it away, holding it smokes the cigarette; the beer keeps F. Throw either and a fresh one turns up at the van, and the bottle breaks where it lands. Each puff blinds anyone standing in it for exactly 7 s, the smoker included (ADR-005); four seconds of drinking empties the bottle and takes your aim, heading and grip for about 25 s, never your speed and never the controls (ADR-006). Both were first built on 2026-09-17 as viewmodels welded to the camera, and the first hands-on QA changed that: the smoke was judged right, the delivery was not. The gain nobody designed is that the puff now leaves the cigarette, so the smoke can be aimed. Whether any of it is funny is still the unanswered question, like everything else here.

Also added 2026-09-17, **an equip system and the first clothes to put in it**. A wearable is an ordinary `MovableObject`: you find it in the house, you grab it with the grab that already exists, and **F** puts it on, or takes off the last piece when your hands are empty. No inventory, no UI. Five slots resolve through the Humanoid avatar rather than by bone name, which matters because this rig is Rigify and its head bone is called `spine.005`. The player now has a body under the camera, and the grandmother's wall mirror upstairs is a real planar reflection, which in a first person game with no second player is the only way to see what you are wearing. Three pieces, all of them hers and all of them stealable: her glasses (the character pack's own, fitted against the baked skinned mesh), her slippers (modelled to swallow the work boot, containment checked vertex by vertex), and her bathrobe (skinned, mid-calf, shawl collar, knotted belt, patch pockets, folded cuffs, 1144 triangles). The robe closes, so its collar, belt and cuffs carry the player colour on a second material slot: an amendment to the identity rule, recorded in `05_ART/CHARACTERS.md`, not a slip. Its skirt is weighted to the pelvis rather than by proximity, so it swings as one cone instead of scissoring with the legs. The lot pays in social information rather than in stats: what you are wearing says which room you went through. No ledger, no score, no detection, so nothing on the out-of-scope list is touched. Brief and rules in `05_ART/CHARACTERS.md`.
**What is not verified:** the F key itself. Every equip so far was called from a script, never through `PlayerEquip`, so the one path a player actually uses has no evidence behind it. The slippers have not been seen on the body since the axis fix; their Blender previews showed them on the wrong foot until 2026-09-27, and their new garment pink is not yet checked in Unity. The mirror pane renders empty in the batchmode captures of 2026-09-26; whether it reflects in the editor is unverified.
**The bathrobe is finished, 2026-09-26.** Judged armour twice: first for its shapes (fixed in the second version), then for its surface, twenty flat vertical facets that read as lamellar plates; it shades smooth now. It is wired and worn: `CrewEquip` binds a skinned piece to the body, and it is validated worn on the player's body in `Map01_PierreKit_House`, both poses, by Jonathan. Two faults only the map could show were fixed on the way: the piece's bind poses did not match the body's (it is rebaked into the body's space), and three hem vertices followed the hands. The crew also got a pose for empty hands; before it every player stood with his arms out, and the carry pose had the forearms through the chest. It is not placed in the map yet, that scene is Pierre's. It was the pilot for an asset workflow, written down on 2026-09-27 as ADR-011 (proposed: Pierre to accept it and to decide the environment tiers): STYLE_GUIDE sections 18 to 20, `05_ART/style/profile.json`, one Blender module, three skills in Jonathan's user folder. None of it outranks action 1 below.

**NEXT DECISION:** the verb divergence, settled by playing Tutorial_01.

**BLOCKERS:** none. DEV 2 is on `dev2/destruction-gameplay`, pushed, not merged into main: it waits for Pierre and Jonathan's playtest. Unity in batchmode stops at package resolution on Jonathan's machine until the project is opened once from the signed-in Unity Hub: Pierre's LumaFlow package comes from the Asset Store (2026-09-27).

**SCOPE:** Tutorial_01 as specified in `02_GAME_DESIGN/GREYBOX_SPEC.md`. The grandmother's house is the vertical slice of ADR-009 (the spec's larger scope, now current for that map), on top of ADR-008.
**TECH:** Unity 6.6.1f1 + Built-in RP + MCP (Unity and Blender both driven over MCP since 2026-09-17). Networking free only, Steam P2P or Unity Netcode, host is a player, no backend, 4 players. Photon excluded (ADR-004). Networking came forward for the slice only (ADR-012, 2026-09-27): NGO over Unity Relay's free tier or direct IP, host-authoritative, 2 players online. Steam networking at launch is unchanged.
**ART:** free packs as the base, completed by AI 3D generation. Greybox uses primitives only. Generated assets will be declared on the Steam page.
**BUSINESS:** price range $4.99 to $14.99, volume over margin, settled later by the final look and the wishlist curve. Team agreement deliberately not formalised.
**STEAM:** page opens when the tutorial works end to end in V1, not before.

**NEXT 3 ACTIONS:**
1. **Play it with a second person** (in person, or online with Jonathan since ADR-012). The go / no-go is spontaneous laughter and one player cannot produce it. This is the only action that matters.
2. Decide the verb divergence from what happens in that session, not from the documents.
3. Only then, decide whether anything else gets built.

## Open divergence: which verb is the core?
**The most important open item. Flagged, not decided, per `/CLAUDE.md` rule 16.**

ADR-003 and the working Unity build say **MOVE / CARRY**, with theft explicitly secondary and told not to become the core. The later concept meeting, with both developers present, recorded the verb as **STEAL UNDER WATCH** and specified a house with an owner on patrol, a cat that screams, and about 80 objects.

These are different games. MOVE / CARRY needs no owner and no detection, and sits directly next to R.E.P.O., which keeps R6 active. STEAL UNDER WATCH needs a present witness and occupies a position nothing in the corpus holds.

Proposed tie-breaker, in `02_GAME_DESIGN/GAME_CONCEPT.md`: play the greybox that already exists. If carrying alone already produces stories, the current ADR stands. If it feels like R.E.P.O. without the monsters, the owner is what the game is missing.

## Open questions: 3 of 29
- **The verb divergence** above. New, and it outranks everything else.
- **Q2, the reason to buy.** Deferred by decision until the greybox is playable. R6 stays active until the sentence works without naming another game.
- **Q21, the commercial change-of-direction signal.** The wishlist curve is designated, the thresholds are not set.

Five team questions (Q12 to Q15, Q22) were deliberately declined. The stated reason is that the project is also for fun. They stay documented in `08_BUSINESS/TEAM_AGREEMENT.md`, unsigned.

## Timeline the team stated
Greybox about one week, then V1 about one week, then one month of communication running in parallel with map production, then the beta. Earlier agent-produced duration estimates were rejected as unreliable and removed. See `04_PRODUCTION/MILESTONES.md`.

## Largest known risks right now
0. **Everything measured so far is feel and tooling, not fun.** The carry works, the meshes are in, the console is clean, the bridges are wired. None of that answers the one question the greybox exists for.
1. **The verb is contested and the code has already picked a side.** Every hour spent on the current build before this is settled is a bet on one reading. Since ADR-008 there is a third candidate, wrecking the house: if a session is only grenades, that is an answer about the verb, not a success.
2. **The reason to buy does not exist yet.** R6, R21.
3. **Networked physics.** Object physics is the core mechanic, and both benchmarked teams named physics in co-op as their hardest problem. R12, R18.
4. **Instant cloning.** R17. A concept readable in 10 seconds is reproducible in 10 days.

## What the September 16 to 17 work changed
Two concept meetings resolved 22 of 29 open questions, declined 5, and left 2 open. In parallel, `main` locked the concept, filled every concept-independent folder and shipped the first working Unity greybox. The merge preserved both and surfaced one contradiction between them. Full journal in `OPEN_QUESTIONS.md`, short list in `QUESTIONS_RESTANTES.md`.
