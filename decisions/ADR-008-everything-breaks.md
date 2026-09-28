# ADR-008: Everything breaks, grenades in the cellar, pockets, and an action key

## Status
**Accepted (2026-09-25), Pierre's decision, taken on the grandmother's house after walking it.** To be read by the other developer: it moves a line of the greybox spec that both of you wrote.

Amends:
- `02_GAME_DESIGN/GREYBOX_SPEC.md`, the out-of-scope list: "destruction / fragmentation" no longer holds for `Map01_PierreKit_House`. Tutorial_01 is unchanged.
- [ADR-007](ADR-007-starting-items-are-objects.md): the "no inventory" half, and the breaking bottle kept to "no shards, no particles, no fragmentation". The other half stands: the items are still real objects, and the smoke still leaves the tip.

## Context
Pierre's requests, 2026-09-25, in his words: "tout peut être cassé techniquement", "des grenades dans la cave", "casser les vitres, casser les objets, tout devrait être cassable", "une touche d'action... ouvrir la porte, fermer la porte, ouvrir les fenêtres, garage pareil", and for the cigarette, which is lost the moment you grab anything else, "un inventaire avec des touches exprès".

Each of these crosses a written line. The spec keeps destruction out of scope, ADR-007 says no inventory and no slot, and the spec says there is no third item.

## Options
- Keep the lines and refuse. The house stays a moving job with a grey tint for "broken".
- Build destruction as a tint plus a sound, and nothing else. It is cheap, but it doesn't answer what Pierre asked.
- **Build it for real on the grandmother's house only, and record it.** Tutorial_01 keeps its narrow question.

## Decision
**1. One health model for everything that can break** (`Breakable`, eight materials: glass, ceramic, plastic, wood, fabric, metal, stone, plaster).
- **Damage from hits.** A hit only hurts above a speed set by the material: a plate minds a 3 m/s knock, and a wall doesn't care below 8 m/s. Past that, damage grows with the square of the extra speed, scaled by the other body's weight.
- **Carrying cushions it.** What you carry is softened by your arms, so a clumsy carry costs you and a graze doesn't.
- **Half health:** a movable is broken, which means half pay, the rule that already existed, and it goes darker.
- **Zero health:** it shatters into physical debris and switches off. The contract then shows it as destroyed.
- **Scope in the house.** Every wall, gable, chimney, fence, railing, post, veranda panel, door leaf, window pane and movable object breaks. **Floors, roofs and stairs do not:** see `04_PRODUCTION/REJECTED.md`.
- **Nothing is authored by hand.** `HouseDestruction` sets it up when Play starts. It cuts the kit's window glass into 128 separate panes and gives 196 structural pieces and 102 movables their health. The kit stays as Pierre built it.

**2. The money follows.** A destroyed required object leaves the checklist, so it no longer blocks delivery, and the client bills its full value instead ("Smashed" on the HUD). Money can go negative: a crew that levels the house owes more than it earned.

**3. Grenades.** A crate of six sits in the cellar, and there is no refill.
- **Throwing:** hold the right button to pull the pin (3.5 s fuse), let go to throw.
- **The blast:** radius 6.5 m. It damages what is in reach nearest first, so a wall that gives way lets the blast through. Something that held cuts it to 35 %. It also pushes objects, knocks the crew back and adds concussion to the drunkenness meter.
- **Chain reaction:** a blast lights any grenade within 5.2 m that is **in the open**, a fraction of a second later. A floor, a wall or a shut door keeps its pin in.
- **Walls** give way only to a grenade within about 0.6 m. A cellar wall survives one grenade.
- **Debris** is capped at 450 pieces, and the oldest shrink away first.

**4. Four pockets, keys 1 to 4.**
- **A pocket holds the object itself,** switched off and riding along. What goes in comes out unchanged: a half-drunk beer is still half drunk, and a grenade with its pin out keeps counting and goes off in the pocket.
- **Only things with a use fit:** the cigarette, the beer and the grenade. A chair doesn't.
- **The start:** the crew arrives with the cigarette in pocket 1 and the beer in pocket 2. The van still lays out spares.

**5. The action key, E.**
- **What it works:** every hinged door, all 21 windows (two casements each, opening outward like an old French house) and the garage door, which is up-and-over.
- **The crosshair decides.** The prompt names the verb under the crosshair. A wall in the way hides the door behind it, and what you carry does not.
- **E is also DELIVER.** The action claims the key first, so opening the front door next to the truck never settles the contract by accident.

## Why
- **It is the go / no-go's own currency.** Spontaneous laughter is the only criterion that matters, and a sofa going through a window is readable by a spectator in well under the 10 seconds of H1.
- **The spec already needed half of it.** The larger scope's second entry is "break a window", so glass had to break sooner or later.
- **It keeps ADR-007's gain.** The pocket holds the real object, so the smoke still comes off the tip and the beer still breaks where it lands. Only the hands stop being the one place to keep them.
- **The grenade adds no new input.** It follows the cigarette's rule: hold the right button to use, release to throw. ADR-007 called that modal button the last one that fits, and this doesn't add another.

## Consequences
- **Scope grows, openly.** Destruction, a third item and slots are now in the house. Tutorial_01 still answers only the carry question.
- **The verb question gets a third candidate.** Moving and stealing were already contested, and "wrecking" is now available. If a session is nothing but grenades by minute three, that tells us about the verb. It is not a success.
- **Performance, measured once:** a grenade against a window wall spends 117 ms in its one frame, with wall, panes and debris together. Debris peaks at 1792 pieces and settles back under the cap within seconds. At rest the house holds 57 fps, running unfocused. Profile if the hitch shows in play.
- **Thresholds are first guesses.** Every material number is a starting point, gathered in one place in `Breakable` so that a note like "vases are too fragile" maps to one number.
- **Fixed during testing, 2026-09-25:**
  - **Cover for the chain.** At first the chain reaction ignored walls and floors, so a blast upstairs lit the cellar crate through the concrete.
  - **Blast buffer.** A blast could miss colliders once fresh debris filled its buffer. The buffer now grows instead.

## Open calls for Pierre (tuning, not decided here)
- The fragility numbers.
- Walls breaking only to a grenade within about 0.6 m, and cellar walls surviving one grenade.
- **Damage behind cover:** a blast behind a floor or wall still does 35 %. A grenade on the ground floor broke the cellar crate underneath without lighting its grenades. Should floors block more than walls?
- **Gables break, the roof above stays in the air.**
- **Doors swing too slowly to break anything** (1.8 m/s at the edge). Should a slammed door smash a vase?
- ~~Exterior doors open outward~~ Settled 2026-09-28 by Jonathan's hot-fix ticket: every door swings away from whoever opens it (a side the house blocks is left for the other one), and a door waits against a person instead of shoving them. See `changelog/CHANGELOG.md`. The garage door stays up-and-over and the windows open outward.

## Revisit if
- **At the two-player session,** people only blow things up and nobody carries anything.
- **The house is a crater every run** before the contract is half done. The crate is then too big, or too easy to reach.
- **Breaking something by accident never gets a laugh** and only gets a groan. The thresholds are then wrong, not the idea.
