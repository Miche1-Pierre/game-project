# CORE LOOP

_The most important file after the concept. If the core loop needs three pages, that is a bad sign._

## Concept
The Movers. See `GAME_CONCEPT.md` and `../decisions/ADR-003-concept-the-movers.md`.

**Read the open divergence in `GAME_CONCEPT.md` before treating this file as settled.** The core verb is contested between MOVE / CARRY and STEAL UNDER WATCH. What follows describes the loop as currently decided and implemented, then what changes under the other reading.

## The 6 questions (one line each)
1. **What does the player do?** Carries the owner's belongings out of the house and loads them into the truck.
2. **Why?** The contract pays, and broken things pay less.
3. **What can go wrong?** The sofa does not fit through the door, the object is too heavy for one, glass breaks, the clock runs out.
4. **How does the world react?** Physics, geometry and weight push back. Value drops on impact.
5. **What does the player do next?** Rotate it, find another route, call someone, or take the risk.
6. **Why replay?** Harder contracts, better crew, and the others will fail differently next time.

## Moment-to-moment loop (the 10 seconds)
See an object. Judge it: how heavy, how fragile, how much is it worth, can I get it through that door alone. Pick it up. Carry it badly, because it has weight and it catches on frames. Solve the geometry problem in front of you. React to what just broke.

The unit of comedy is the **physical problem the game never asked**: the sofa is wider than the door, and nothing in the code says so.

## Macro loop (the run)
1. Read the contract: which items must reach the truck.
2. Scout the property.
3. Move the items out, splitting up because nobody can carry everything.
4. Load the truck, physically.
5. Deliver, settle up. Contract money, minus breakages.
6. Upgrade the crew, unlock a harder contract.

## What makes it emergent
Three systems crossing, with no scripted events:
- Physics decides whether the object survives the trip.
- Geometry decides whether it fits at all.
- The other players decide everything else, because they are in the way, they are carrying the other end, and they let go at the wrong moment.

## Under the other reading (steal under watch)
If the divergence resolves toward theft, the loop gains a fourth system and one idea:

**The contract and the theft use the same verbs.** No steal button, no separate mode. An object is stolen only because of where it ends up and who saw it happen. The owner becomes a present, moving witness who has to be read and routed around, and the unit of comedy shifts to the **re-judgement mid-carry**: you decide to pocket something and the owner appears while it is already in your hands.

Pets become the smallest system with the largest output. A cat that screams when picked up is a portable alarm, a comedy object and a moral choice in one asset.

That version is specified in `GREYBOX_SPEC.md`, under "Next scope, pending the verb question". It is not built.

## Not in the loop yet
Shop, progression between maps, procedural layout, drivable truck, owner, detection. See `GREYBOX_SPEC.md` for what Tutorial_01 actually contains.

_Status: filled 2026-09-17. Provisional until the verb divergence is settled._
