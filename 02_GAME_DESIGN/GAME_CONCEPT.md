# GAME CONCEPT

**SELECTED. Decided 2026-09-16, see `decisions/ADR-004-concept-steal-under-watch.md`.**

_The five earlier candidates are archived at the bottom. They are no longer the fallback, see `00_PROJECT/OPEN_QUESTIONS.md` Q17._

## One line
A moving crew empties a house while the owner watches, and steals whatever it can get away with.

## Signature verb
**STEAL UNDER WATCH.**

Moving is the fiction. Stealing under observation is the game. This distinction was made explicitly at the concept meeting and it governs everything: level design, the AI budget, the loop and the pitch. We are not building a hauling game.

## Fantasy
You have been let into someone's home, legitimately, with their keys. You have a job to do and a reason to be in every room. Every object you pick up is defensible right up until it goes in your pocket.

## Core tension
The contract and the theft use the same actions. Carrying a lamp to the truck and stealing a lamp look identical until the moment they do not. The owner cannot tell which one you are doing, and neither can they watch every room.

## Systems (5, deliberately few)
1. **Object physics, fragility, value.** Objects break, and broken objects lose money.
2. **Contract versus theft.** Two ledgers on the same actions.
3. **The owner.** A present, moving witness who has to be read and routed around.
4. **Time pressure.** A recommended duration, after which the situation degrades.
5. **Living objects.** Pets are objects that react, complain, and can die.

## What we do not build
- No procedural generation until the loop is proven.
- No shop until the loop is proven.
- No drivable vehicle until V1.
- No detection AI until V2.

See `GREYBOX_SPEC.md` for exactly what ships first.

## Tone
Deliberate derision, no self-imposed ceiling on the absurd. The team decided on 2026-09-16 that the absurd has no cut-off, and that the intent is openly ridiculous rather than mean. Practical consequence: Steam content descriptors will need to be filled honestly at store-page time (`09_STEAM/STORE_PAGE.md`), which is a form to complete, not a constraint on the design.

## Reason to buy
**Still open.** Deferred by decision until the greybox is playable, because the team wants to feel the concept before writing the sentence. The working direction from the meeting is the "why not" impulse: why not take that, why not try this, what happens if. See `00_PROJECT/OPEN_QUESTIONS.md` Q2.

This is the single most important unresolved item in the project. `00_PROJECT/RISKS.md` R6 stays active until it is answered without naming another game.

---

## Archive: the five pre-selection candidates
Kept for the record. None was selected. The retained concept came from the brainstorm, which `GAME_CONCEPT.md` explicitly allowed.

- **Office under surveillance** (CAND). A mundane office, one saboteur, detection by the other players.
- **Competence comedy** (OPP-1). An absurd job done together, filmed.
- **Deduction through work** (OPP-2). Unmask a liar with no meeting and no vote.
- **Hidden threat, shared danger** (OPP-3). An imitator among the survivors.
- **The talkative sim** (OPP-4). Management depth negotiated by voice.

Note: the retained concept is the closest to CAND, with one inversion. The witness is an NPC, not the other players. The players are on the same side.
