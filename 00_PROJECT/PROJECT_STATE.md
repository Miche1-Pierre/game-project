# PROJECT STATE

_Living dashboard. The agent reads it at start and updates it at end of session. Last update: 2026-09-17._

**PHASE:** Greybox specification done, Unity creation unblocked

**CONCEPT:** Steal under watch. A moving crew empties a house while the owner is present, and steals what it can get away with. See `02_GAME_DESIGN/GAME_CONCEPT.md` and ADR-004.
**CORE LOOP:** Carry the owner's belongings to the truck, pocket what the owner will not notice. The contract and the theft use the same verbs. See `02_GAME_DESIGN/CORE_LOOP.md`.
**SIGNATURE VERB:** STEAL UNDER WATCH.

**CURRENT HYPOTHESES:**
- H1: social friction readable in under 10 s is the real engine, to exploit as explicit intent.
- H2: the clip as an output of the loop is a white space.
- H4: reusable capital, not concept quality, is what makes a fast build possible.
- H5: the reveal artefact is the entire marketing campaign, and third parties distribute it.
- H6 (new): the comedy comes from re-judging mid-carry, when the owner appears while the object is already in your hands.

**RETIRED:** H3 (players as the detection system rather than NPC AI). The chosen concept puts the players on the same side, so the comparison does not apply. Retired 2026-09-16, not invalidated.

**VALIDATED:** nothing in play. No build exists yet.
**INVALIDATED:** "Unity everywhere" as a corpus rule. The largest 2026 hit is Unreal 5. Our Unity choice stands (ADR-001) as a preference, not as a benchmark finding.
**RUNNING EXPERIMENT:** none
**CURRENT BUILD:** none. **Unity creation is now unblocked**, because `02_GAME_DESIGN/GREYBOX_SPEC.md` exists and ADR-002 required only that.

**NEXT DECISION:** none blocking. The next real decision is the go / no-go after the greybox.

**BLOCKERS:** none.

**SCOPE:** greybox frozen. Grandmother's house, 10 rooms, about 80 objects, 2 living objects, 1 NPC with a single interaction. No shop, no procedural generation, no drivable truck, no detection AI.
**TECH:** Unity 6.6.0f1 + URP + MCP. Networking free only, Steam or Unity Netcode, host is a player, no backend, 4 players. Photon excluded (ADR-003). Steam networking comes last.
**ART:** free packs as the base, completed by AI 3D generation. Greybox uses grey boxes only. Generated assets will be declared on the Steam page.
**BUSINESS:** price range $4.99 to $14.99, volume over margin, settled later by the final look and the wishlist curve. Team agreement deliberately not formalised.
**STEAM:** page opens when the tutorial works end to end in V1, not before.

**NEXT 3 ACTIONS:**
1. Create the Unity project. The gate is passed.
2. Build steps 1 to 4 of the suggested order in `GREYBOX_SPEC.md`: one room, five objects, the cat, a second player, the grandmother on patrol.
3. Play it and find out whether anyone laughs.

## Open questions: 2 of 29
- **Q2, the reason to buy.** Deferred by decision until the greybox is playable. The most important unresolved item in the project. R6 stays active until the sentence works without naming another game.
- **Q21, the commercial change-of-direction signal.** The wishlist curve is designated, the thresholds are not set.

Five team questions (Q12 to Q15, Q22) were deliberately declined. The stated reason is that the project is also for fun. They stay documented in `08_BUSINESS/TEAM_AGREEMENT.md`, unsigned.

## Timeline the team stated
Greybox about one week, then V1 about one week, then one month of communication running in parallel with map production, then the beta. Earlier agent-produced duration estimates were rejected as unreliable and removed. See `04_PRODUCTION/MILESTONES.md`.

## Largest known risks right now
1. **The reason to buy does not exist yet.** R6.
2. **Networked physics.** Object physics is the core mechanic, and both benchmarked teams named physics in co-op as their hardest problem. R12, R18.
3. **No fallback.** Decided deliberately, but it means the go / no-go is binary. R17 context in `OPEN_QUESTIONS.md`.
4. **Instant cloning.** R17 in the risk register. A concept readable in 10 seconds is reproducible in 10 days.

## What the September 16 to 17 meetings changed
Two concept meetings resolved 22 of 29 open questions, declined 5, and left 2 open. The project moved from "no concept, nothing buildable" to "scope frozen, ready to build". Full journal in `OPEN_QUESTIONS.md`, short list in `QUESTIONS_RESTANTES.md`.
