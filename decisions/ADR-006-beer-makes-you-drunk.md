# ADR-006: The beer wrecks the person holding it

## Status
**Amended 2026-09-20 by [ADR-007](ADR-007-starting-items-are-objects.md):** the beer is an object by the truck now, not a bottle welded to the camera, and it breaks when you throw it. What drinking does, below, is unchanged.

**Accepted (2026-09-17), and flagged**, on the same terms as [ADR-005](ADR-005-cigarette-smoke-screen.md). The team was asked the question rather than told the answer, because ADR-005 had recorded that the beer was next and that "give it a function too" was not automatically right. The answer came back: it makes you drunk.

## Context
`02_GAME_DESIGN/GREYBOX_SPEC.md` lists the starting inventory as "a beer and a cigarette, usable at any time, useful for nothing". ADR-005 took the cigarette out of that sentence. This takes the beer.

The sentence is now spent. There is no third absurd item, and the inventory is closed until somebody has a reason to open it.

## Options
- **A. Leave it inert.** What the spec says. The joke stays a joke a player finds once.
- **B. Drunkenness: it wrecks the person who drank it.** Aim, heading and grip go, speed and control stay.
- **C. Liquid courage: it makes you stronger.** Carry alone what needs two people, at the cost of precision.
- **D. Give it to a crewmate.** A social item. Needs handing objects between players, which does not exist.

## Decision
**B.** Drinking turns your own aim, your own heading and your own grip against you for about 25 seconds. It never touches your speed and never takes the controls away.

C was the tempting one and it was refused. Weight is the core of this game and the carry is the **only** thing a playtest has validated (`00_PROJECT/PROJECT_STATE.md`). An item that rewrites the weight rules is not an item, it is a balance change to the one system we know works, and it would have to be tuned against a loop nobody has played co-op yet. That is a decision for after the go / no-go, not before it.

D is a good idea with no floor under it: handing objects between players does not exist, and building it for a beer would be the tail wagging the dog.

## Why this shape, and not a worse one
- **It is symmetric with the cigarette.** Both items punish their owner first. The smoke blinds you before it blinds anyone else; the beer only ever hits you. Nothing in the starting inventory is a weapon, which keeps the crew on the same side.
- **It takes aim, not agency.** You can always cross the room. You just cannot do it in a straight line, and what you are carrying swings while you try. Losing control is frustrating; being bad at something is funny. CLAUDE.md rule 4.
- **The bottle is finite.** Four seconds of drinking and it is empty, permanently. An endless source would be a permanently broken player, which is a punishment with no decision in it. Finite makes "when do I drink this" a real question.
- **It adds no system.** `Drunkenness` writes fields that `PlayerController` and `PlayerGrab` already expose for this exact purpose, the way `PlayerGrab` already writes `speedMultiplier`. A drunk player is the sober player with worse numbers.
- **It is honest about the state.** Drunkenness is separate from the bottle, so the next thing that should make the crew useless (a bang on the head, a cat to the face) feeds the same meter instead of growing a second one.

## What it costs
- A second self-inflicted item in a greybox that has still never been played by two people. If both of them are funny alone but tiresome together, one of them goes.
- One more key. F, on top of RMB for the cigarette. Both are hold-to-use with your hands empty, which is one rule, but the greybox is now at three modal inputs (R, the wheel, and hands-empty).
- The sway is authored with sines. It is readable and it never repeats, but it is not physics, and a playtester who leans on it will find the pattern eventually.

## Revisit when
The first two-player playtest, with ADR-005.
- **It is only annoying.** Cut the drift, keep the sway, or cut the whole thing back to a prop.
- **It is funny but it eats the run.** Shorten `soberSeconds`, or make the bottle smaller.
- **It works, and people drink it on purpose to make a friend laugh.** Then the question is whether drunkenness should come from anything else, and the answer is still not automatic.

## Consequences
- `Drunkenness` in `Scripts/Effects`, `PlayerBeer` in `Scripts/Items`.
- `PlayerController` gains `lookSway` and `moveDrift`, `PlayerGrab` gains `carrySlop`. All three are `[HideInInspector]`, all three are zero unless something is drunk, and nothing else writes them.
- `GREYBOX_SPEC` gains system 11 and a controls line. The HUD shows a drunk readout, for the person watching over the shoulder, not for the player.
- `MoversCigaretteCLI` became `MoversStartingInventoryCLI`: it installs both items now, and the old name was a lie.
