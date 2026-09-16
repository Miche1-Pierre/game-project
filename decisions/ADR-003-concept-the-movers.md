# ADR-003: Concept, The Movers

## Status
Accepted (2026-09-16) as the working concept to validate in greybox (per ADR-002).

## Context
Concept selection. From the benchmark white spaces and the constraint "the mission stays real, the money matters, and the absurdity emerges from players solving physical problems, not from the game telling them to mess around", Pierre proposed The Movers.

## Decision
Build **The Movers**: a 1-4 player co-op physical moving game. Players fulfill a moving contract (collect the required items, load the truck, deliver, get paid, progress). Everything is physical; problems emerge from objects, buildings and physics. Opportunistic extra value (non-contract objects) and a light traces / consequences layer are secondary and must never become the core: the game says "do the move", it does not say "steal" or "make a mess".

## Why
- Real mission + money + progression (the constraint Pierre wanted), with emergent absurdity from physical problem-solving.
- Systemic and cheap: physics + players generate the stories; one map, ~15-20 objects, a handful of systems.
- One strong verb (MOVE / CARRY), 10-second legible, clip-native (the disasters film themselves).
- Greyboxable with Unity primitives in days; no backend.

## Consequences
- First greybox is single-player core feel (grab / carry / load / contract), primitives only, no theft / NPC / net (`../02_GAME_DESIGN/GREYBOX_SPEC.md`).
- Co-op (hot-seat, 2 PC, then Steam P2P) is the immediate next step once the feel is good.

## Revisit if
The physical carry / load loop is not fun at greybox, or the emergent-problem promise does not materialize.
