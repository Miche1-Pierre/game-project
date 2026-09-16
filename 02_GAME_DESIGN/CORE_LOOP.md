# CORE LOOP

_The most important file after the concept. If the core loop needs three pages, that is a bad sign._

## Concept
Steal under watch, during a legitimate moving job. See `GAME_CONCEPT.md` and `decisions/ADR-004-concept-steal-under-watch.md`.

## The 6 questions (one line each)
1. **What does the player do?** Carries the owner's belongings to the truck, and pockets what the owner will not notice.
2. **Why?** Both pay. The contract pays safely, the theft pays more.
3. **What can go wrong?** The object breaks, the owner walks in, the pet screams, the clock runs out.
4. **How does the world react?** Value drops, money is lost, the owner gets harder to avoid as time passes.
5. **What does the player do next?** Cover, drop it, hide it, hand it to a teammate, or run for the truck.
6. **Why replay?** To get away with more, and because the others will fail differently next time.

## Moment-to-moment loop (the 10 seconds)
See an object. Judge it: is it on the contract, is it worth stealing, is it fragile, who can see me right now. Pick it up. Carry it, badly, because it has weight and it catches on doorframes. Commit to the truck or to your pocket. React to what just went wrong.

The unit of comedy is the **re-judgement mid-carry**: you decide to steal something and the owner appears while it is already in your hands.

## Macro loop (the run)
1. The owner hands over the keys and states one or two house rules.
2. Read the contract: which objects must go in the truck.
3. Work the house room by room, splitting up because nobody can carry everything.
4. Steal opportunistically, one decision at a time.
5. The clock passes the recommended duration and the situation degrades.
6. Load the truck, settle up. Contract money, minus breakages, minus penalties, plus whatever you got away with.

## The two ledgers
The whole design rests on one idea: **the contract and the theft use the same verbs**. No separate steal button, no separate mode. An object is stolen only because of where it ends up and who saw it happen.

## What makes it emergent
Four systems crossing, with no scripted events:
- Physics decides whether the object survives the trip.
- The owner's position decides whether the theft is possible right now.
- The clock decides how much risk is worth taking.
- The other players decide everything else, because they are in the way, they are watching, and they can be handed a stolen lamp at the worst moment.

## The pet clause
Pets are the smallest system with the largest output. A cat that screams when picked up is a portable alarm, a comedy object and a moral choice in one asset. The fish dies on a timer out of water, which turns a pocketed object into a countdown the player forgot about.

## Not in the loop yet
Shop, progression between maps, procedural layout, drivable truck, vigilance meter. All deferred. See `GREYBOX_SPEC.md`.

_Status: filled 2026-09-17 from the 2026-09-16 concept meetings._
