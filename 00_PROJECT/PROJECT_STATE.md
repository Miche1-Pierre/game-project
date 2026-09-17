# PROJECT STATE

_Living dashboard. The agent reads it at start and updates it at end of session. Last update: 2026-09-17, after adding jump and carried rotation._

**PHASE:** Greybox built, waiting to be played

**CONCEPT:** The Movers. A 1-4 player co-op physical moving game. Contract, carry, load the truck, get paid, upgrade. See `02_GAME_DESIGN/GAME_CONCEPT.md` and ADR-003.
**CORE LOOP:** Carry the owner's belongings out and load them into the truck, while physics and geometry fight you. See `02_GAME_DESIGN/CORE_LOOP.md`.
**SIGNATURE VERB:** MOVE / CARRY, **contested.** See the open divergence below.

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
Added 2026-09-17: **Space to jump** (flattened by the weight you carry) and **hold R to turn the object in your hands** (mouse turns, scroll rolls). The rotation exists because the spec's one emergent problem, the sofa wider than the door, had no solution without it. Both are unplayed by a human: compile and console are clean, the feel is not measured.

**NEXT DECISION:** the verb divergence, settled by playing Tutorial_01.

**BLOCKERS:** none technical.

**SCOPE:** Tutorial_01 as specified in `02_GAME_DESIGN/GREYBOX_SPEC.md`. A larger scope from the concept meeting is recorded in the same file and deliberately held.
**TECH:** Unity 6.6.1f1 + Built-in RP + MCP (Unity and Blender both driven over MCP since 2026-09-17). Networking free only, Steam P2P or Unity Netcode, host is a player, no backend, 4 players. Photon excluded (ADR-004). Networking comes last, not in the greybox.
**ART:** free packs as the base, completed by AI 3D generation. Greybox uses primitives only. Generated assets will be declared on the Steam page.
**BUSINESS:** price range $4.99 to $14.99, volume over margin, settled later by the final look and the wishlist curve. Team agreement deliberately not formalised.
**STEAM:** page opens when the tutorial works end to end in V1, not before.

**NEXT 3 ACTIONS:**
1. **Play it with a second person.** The go / no-go is spontaneous laughter and one player cannot produce it. This is the only action that matters.
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
1. **The verb is contested and the code has already picked a side.** Every hour spent on the current build before this is settled is a bet on one reading.
2. **The reason to buy does not exist yet.** R6, R21.
3. **Networked physics.** Object physics is the core mechanic, and both benchmarked teams named physics in co-op as their hardest problem. R12, R18.
4. **Instant cloning.** R17. A concept readable in 10 seconds is reproducible in 10 days.

## What the September 16 to 17 work changed
Two concept meetings resolved 22 of 29 open questions, declined 5, and left 2 open. In parallel, `main` locked the concept, filled every concept-independent folder and shipped the first working Unity greybox. The merge preserved both and surfaced one contradiction between them. Full journal in `OPEN_QUESTIONS.md`, short list in `QUESTIONS_RESTANTES.md`.
