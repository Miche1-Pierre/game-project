# GAME CONCEPT

**SELECTED CONCEPT: The Movers** (2026-09-16, ADR-003). A 1-4 player co-op physical moving game.

## Pitch
You are a small moving crew. Each contract gives you a property to clear and a list of items to recover. Get the items, load the truck, deliver them without wrecking them, get paid, and upgrade. The catch: everything is physical. Furniture falls, glass breaks, a sofa gets stuck in a doorway, a fridge is too heavy for one, a statue will not fit in the lift. The absurdity and the stories come from players solving physical problems the game never explicitly asked them to solve.

## Core promise
The mission stays real, the money matters, progression motivates, and the comedy emerges because players look for solutions, not because the game tells them to mess around. Opportunistic extra value (non-contract objects worth money) and a light traces / consequences layer are secondary, and must not become the core.

## The verb
**MOVE / CARRY.** One image: four people trying to get a piano out of a house.

## Loop
Contract -> scout the house -> move the items out -> load the truck (real physical packing) -> transport -> deliver -> money -> upgrade the crew and unlock harder contracts. Between each step: "do we do this cleanly, or do we take a risk?"

## Why it fits our framework
Real mission + emergent absurdity (the constraint), systemic and cheap (physics + players = stories, one map + ~15-20 objects), one strong verb, 10-second legible, clip-native, greyboxable in days, no backend. Full rationale: ADR-003. First test: `GREYBOX_SPEC.md`.

## Detailed design
See the other files in this folder (CORE_LOOP, GAME_RULES, MECHANICS, SYSTEMS, OBJECTS, PROGRESSION, WORLD, SOCIAL_DESIGN, FAILURE_STATES, SCORING). Being filled now that the concept is chosen.

---

## Earlier candidates (archived, for reference)
Before The Movers, the benchmark surfaced these candidates (`../01_RESEARCH/WHITE_SPACES.md`): CAND office / sabotage, OPP-1 competence comedy, OPP-2 deduction through work, OPP-3 hidden threat / shared danger, OPP-4 the talkative sim, plus a Claude proposal "REWIND" (security-tape deduction). The Movers is closest in spirit to OPP-1 (competence comedy) grounded in a real job. Kept here in case we pivot.
