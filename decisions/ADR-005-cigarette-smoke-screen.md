# ADR-005: The cigarette is a smoke screen, not a prop

## Status
**Accepted (2026-09-17), and flagged.** Asked for by the team and built the same day. It is flagged because it does two things the current documents do not allow, and both are recorded below rather than quietly absorbed: it gives a gameplay function to an item the spec calls useless, and it is built while the core verb is still contested (`00_PROJECT/PROJECT_STATE.md`, open divergence).

## Context
`02_GAME_DESIGN/GREYBOX_SPEC.md` lists the starting inventory in the larger, held scope: "a beer and a cigarette, usable at any time, **useful for nothing**". Deliberately absurd, deliberately inert.

The request of 2026-09-17 turns the cigarette into a tool: hold the right mouse button, it produces a lot of smoke, and the smoke blocks the vision of anyone standing in it for 7 seconds.

That is not a bigger version of a prop. An item that is useful for nothing is a joke the player finds; an item that blinds the crew is a mechanic the player plans with. The two are different objects that happen to share a model.

## Options
- **A. Leave it inert.** Follows the spec as written. Costs nothing, adds nothing.
- **B. A pure visual effect.** Smoke that looks good and blocks nothing. Cheap, and a lie: the player will test whether it hides them within a minute of finding it.
- **C. A smoke screen that blinds everyone in it, smoker included.** What was asked for.
- **D. A smoke screen that blinds everyone except the smoker.** A tactical tool, the version a competitive game would ship.

## Decision
**C.** Everyone in the cloud loses their view, including whoever lit it. Seven seconds from the puff leaving the mouth to the screen being clear again, with no exception for the person who caused it.

D was not chosen, and the reason is rule 4 of the constitution: funny failure over frustrating punishment. A smoke screen that spares its owner is a weapon, it belongs in a game where players compete. This crew is on the same side (H3 was retired for exactly that reason), so the only reading that produces comedy is the one where you fog yourself, your friend walks in carrying a fridge, and neither of you can see the door.

## Why this is defensible against rule 15 (scope)
- **It adds no system.** It reuses the player, the camera, and the existing greybox loop. No inventory system, no item framework, no pickups. One component on the player, one component on the camera, one object per puff.
- **It builds no art.** Both textures are generated in code, the cigarette is two primitives (rule 13, no final asset before the system is validated).
- **It does not pick a side in the verb divergence.** It works in a moving game and in a stealing game, because it is about the crew seeing each other, not about the owner seeing the crew. Nothing about it assumes a witness.
- **It answers the go / no-go question, which is laughter.** The cheapest laugh in the build list was the cat that screams. This is in the same family and needs no asset at all.

## What it costs
- It is the first mechanic in the project whose purpose is to make the game **harder to see**, and the greybox has never been played by two people. If it ruins the carry rather than complicating it, it goes.
- It spends time before the verb question is settled, which is the bet `PROJECT_STATE` warns about under risk 1.
- It puts a second job on the right mouse button (throw while carrying, smoke while empty-handed). That reads as one rule, but it is the second modal input in the game after the rotate key and the scroll wheel. A third would be too many.

## Revisit when
The first two-player playtest. Three outcomes are worth acting on:
- **Nobody laughs and the smoke only annoys.** Delete the blinding, keep the prop, ADR-005 is superseded.
- **People laugh but it eats the session.** Keep it, add a cost: a cigarette that burns out. The hook is documented in `PlayerCigarette`.
- **It works.** Then the beer is the next question, and the answer is not automatically "give it a function too".

## Consequences
- `SmokeCloud`, `SmokeVision`, `SmokeTextures`, `PlayerCigarette` in `UnityProject/Assets/_Movers/Scripts`.
- `GREYBOX_SPEC` gains system 10 and a controls line.
- The blindness is per camera, not global, so it already behaves correctly with four players on four screens. That is a consequence of where the component sits, not a networking feature: nothing here is networked (ADR-004 stands, netcode comes last).
