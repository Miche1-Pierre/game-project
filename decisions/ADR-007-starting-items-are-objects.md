# ADR-007: The starting items are objects, and the right button is modal

## Status
**Accepted (2026-09-20).** Comes out of the first hands-on QA of the two items. The verdict on the smoke itself was "parfaite"; the verdict on how you hold it was that it should be a thing in the world, not a thing welded to the camera.

Supersedes the **delivery** half of [ADR-005](ADR-005-cigarette-smoke-screen.md) and [ADR-006](ADR-006-beer-makes-you-drunk.md). What a puff does and what a beer does are untouched: 7 seconds, everyone in the cloud; 25 seconds, aim and heading and grip. Only where the items come from changes.

## Context
Until now the cigarette and the beer were viewmodels parented to the player camera, always present, usable only with empty hands. That made them a two-slot inventory in a game whose whole premise is that you carry things with your hands and physics fights you.

It also created an odd exception: everything else in the house is a rigidbody you grab, turn and drop, and these two were the only objects the crew could not put down.

## Decision
Both are ordinary `MovableObject`s.

- They are **laid on the ground by the truck** when the game starts, and you go and get them.
- You pick them up, carry them, turn them and drop them with the grab that already exists. No pickup code, no inventory, no UI.
- The right button is **modal on a held usable**: a tap throws it, holding it uses it. On everything else it throws on press, exactly as before.
- **Throwing one away frees the spot** and a fresh one turns up at the van after half a second. The thrown one stays where it landed.
- The beer keeps **F**, and **breaks** when it hits anything hard, full or empty.

## Why
- **One system, fewer rules.** The grab, the reach, the carried rotation and the weight feel all apply to a beer bottle for free. A player who understands a chair understands a cigarette.
- **The smoke leaves the cigarette, not your face.** This is the real gameplay gain and it was not designed, it fell out: the puff is born at the tip, and the tip is wherever your hands are. You can hold it at arm's length and fog a doorway, or pull it in with the scroll wheel and blind only yourself. Aiming the smoke is now something you can be bad at, which is the good kind of skill for this game.
- **A place, not a slot.** "Where are the smokes" has an answer a second player can be told out loud: by the van. That is the sort of thing a crew says to each other, which is the point of the whole project (H1).
- **Throwing one away costs a walk.** Free and infinite, but not free and instant.

## What it costs
- **A third modal input**, after the rotate key and the scroll wheel. This is the last one that fits. If a fourth is ever needed, the answer is a different design, not a longer press.
- **The throw moves to the release** for usable objects, because a tap and a hold cannot be told apart at the moment of pressing. 0.18 s is the line. It only affects those two items; the sofa still leaves your hands the instant you click.
- **A cigarette is 13 mm across** and nobody can put a crosshair on that at three metres, so the object you actually grab is a fist-sized invisible box around a thin visual. Honest, and worth saying out loud.
- **Litter accumulates.** A flicked cigarette stays on the floor for the whole run, by design. If a long session ever fills a room with them, the fix is a burn-out timer, not a rule change.
- The **breaking bottle** touches the "destruction" line the greybox spec keeps off the list. Kept to the smallest thing that reads: the bottle is destroyed and leaves one flat mark. No shards, no particles, no fragmentation.

## What was not done
Drinking still works at whatever distance you happen to be holding the bottle, which is not how anyone drinks. Pulling it to your mouth means driving the carry from inside an item, and the carry is the one thing a playtest has validated. Left alone deliberately; it is a feel question for the next session, not a bug.

## Revisit when
The same two-player playtest that gates ADR-005 and ADR-006. Three things to watch, in order:
- **Does the tap-versus-hold read without being told?** If people throw their cigarette while trying to smoke it, the modal button is wrong and the use should move to its own key.
- **Does anyone walk back to the van for a second one,** or does the first throw end the joke for the session?
- **Does the smoke get aimed?** If nobody ever holds it out to fog a doorway, the gain above is imaginary and the simpler viewmodel was better.
