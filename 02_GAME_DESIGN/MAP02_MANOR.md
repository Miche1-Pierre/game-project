# MAP 02, THE MANOR (proposal)

_Status: proposal, 2026-09-30. Not decided, not built._

> **Scope flag (`/CLAUDE.md` rules 15, 16, 18).** `GREYBOX_SPEC.md` lists "a second map" as out of scope, in both of its versions. This file is a plan to have ready, not permission to build. Building it needs both developers and an ADR, and it comes after `PROJECT_STATE.md` action 1 (play with two people).

- **Plan data:** `_ArtSource/manor_plan.txt`, same format as the cottage's `house_plan.txt`.
- **Check and drawings:** `python3 tools/plan/plan_check.py _ArtSource/manor_plan.txt --html manor.html`. The plan passes with 0 errors, and the cottage plan passes too.

## 1. Intent
A rich owner's country manor, built from the PierreKit with no new structure piece. The feel we aim for is R.E.P.O.'s manor: many rooms, two storeys and a cellar, clutter, loops and dead ends. It is hand-made, with no procedural generation, which is also out of scope.

What each feature does for the core loop (steal under watch, carry past the owner):
- **Two stair cores (the grand stair and the servants' stair)** make one loop across both floors. The crew can always go round the owner instead of waiting for them.
- **Dead ends with the best loot:** the owner's suite upstairs west, the cabinet over the entrance, and the cellar. A choice between risk and reward that a spectator can read.
- **Hidden rooms, opened with the carry itself:** see section 4. No new mechanic.
- **Long carries:** the deepest room is 44 m from the nearest exit, against 17 m in the cottage. Physics and geometry get more time to go wrong (H6).

## 2. Hard dimensions (Unity metres, from `05_ART/ASSET_LIST.md`)
| Item | Value | Consequence for the plan |
|---|---|---|
| Cell | 3.00 m (2 BU module at import scale 1.5) | every wall is on the 3 m grid |
| Storey | 3.06 m; ground +0.30, first floor +3.36, cellar -2.76 | same heights as Map01 |
| Exterior door | 1.20 x 2.25 m | **the narrowest point of every route out** |
| Interior door | 1.35 x 2.40 m | |
| Interior arch (`Wall_Int_Arch`) | 2.25 x 2.55 m | used on the main axis and to enter the salon |
| Player | 1.80 x 0.70 m capsule, crouch 1.00 m, step 0.30 m | stairs need the Map01 ramp |
| Stair flight | 4.9 m run, 2.79 m rise on a 0.27 m plinth | 2 cells + 1 exit cell in line, with void cells above |
| Gable roof | 30 degrees, **span exactly 2 cells (6 m)** | every wing is 6 m deep; wings meet as crosses, as on Map01 |

## 3. Layout
Footprint 39 x 18 m (13 x 6 cells), in an H shape around a front court that faces the truck. The street side is south.

| Level | Floor cells | Area | Rooms |
|---|---|---|---|
| L1 | 40 (+6 void) | 360 m² | gallery and landing, cabinet of curiosities, bathroom, anteroom, **owner's suite** (bedroom, nursery, dressing, bath), corridor, guest room, maid's room, **hidden room** |
| L0 | 52 (+2 void) | 468 m² | vestibule, hall with a double (imperial) stair, garden hall, salon, library, **hidden study**, music room, orangerie, dining, office, kitchen, pantry, service hall, servants' stair |
| L-1 | 14 | 126 m² | cellar (boiler), wine cellar, **hidden vault** |
| Total | 106 | 954 m² | 2.4 times the cottage (44 cells, 396 m²) |

- **Ground floor spine:** the twin front doors lead to the vestibule, through two arches to the hall, then through two more arches to the garden hall, which has twin garden doors. The salon is west through an arch, the dining room east.
- **West wing, reception rooms:** library (front), salon (middle), music room (back). A glass orangerie runs along the west side and loops the salon to the music room. It is the cottage's veranda, stretched to 4 cells.
- **East wing, service:** office (front), dining (middle), kitchen and pantry (back). Behind them are the service hall with the back door, the servants' stair and the stone cellar stair.
- **Upstairs:** a gallery over the hall, with railings over the two stairwells. The west side is the owner's suite: one door in, three rooms behind the bedroom. The east side is the corridor to the servants' stair, the guest room and the maid's room.
- **Exits:** 6 (2 front, 2 garden, the service door, the orangerie door), plus every window (ADR-008 lets them open and break).

## 4. Hidden rooms
**Mechanism: a normal door hidden behind a heavy movable.** You find the room by moving the furniture, so it uses the carry and needs no new code. **The tell:** from outside, a window with no room behind it.

| Room | Level | Hidden behind | Tell |
|---|---|---|---|
| Hidden study, 9 m² | L0, in the library | `Bookshelf_01` against the library's north door | a small window onto the front court, next to the library |
| Hidden room, 18 m² | L1, front of the east wing | `Wardrobe_01` in the guest room | two windows on the street front |
| Hidden vault, 18 m² | L-1, off the wine cellar | a loaded `Shelving_Unit_Cave` | none: it is underground, so it is the hardest to find |

In `manor_plan.txt` these are `INT_DOOR` edges into rooms named `hidden_*`. The drawing marks them in red.

## 5. Routes, measured by `plan_check.py`
- **Loops:** 9 on L0 (outdoor loops included), 1 on L1, plus the loop across both floors through the two stair cores. The owner's suite is a dead end on purpose.
- **Bottleneck:** every route out of every room passes a 1.20 m exterior door. This is H6 friction (turn the sofa in the doorway), the same as the cottage without its garage.
- **Distance to the nearest exit:**

| Room | Distance |
|---|---|
| master bath | 44 m |
| dressing | 41 m |
| nursery | 38 m |
| master bedroom | 35 m |
| cabinet | 26 m |
| hidden vault | 23 m |
| hidden study | 21 m |
| hidden room | 20 m |
| hall | 3 m |
| vestibule | 0 m (it has an exterior door) |

## 6. Kit use and gaps
- **Structure:** nothing new. The existing pieces cover it: walls with doors and windows, interior door and arch, railing, wood and stone stairs, veranda glass, door and cheeks, cellar wall, gables, chimney.
- **Kit gaps, same as Map01:** no roof valley or verge piece. The roof crossings copy the cottage's cross: main ridge X over x0..11, wings and pavilion with ridge Z, and a single-storey kitchen ell.
- **Props:** optional and new, not needed in greybox: piano, billiard table, safe, display cases.
- **Stairs:** each of the three wooden flights and the stone flight needs the Map01 plinth and invisible ramp.

## 7. Hypotheses (not requirements)
- **Objects:** 180 to 220 movables, of which about 20 are on the contract. The cottage density would give about 240.
- **Timer:** 20 to 25 minutes. It is tuned for 2 to 4 players.
- **Performance risk, to measure before optimising:** 67 windows against the cottage's 21 means about 400 glass panes cut at Play instead of 128, and about 2.4 times the breakable structural pieces.

## 8. Open decisions (for Pierre and Jonathan)
1. Do we build a second map at all? It is out of scope (ADR needed).
2. **The owner:** reuse the grandmother as the widow of the house (no new NPC asset), or add a new character.
3. Should the owner react when a masking piece of furniture has been moved?
4. **Front door:** keep the 1.20 m bottleneck, or add a new double-door piece (about 2.25 m).
5. **Optional crawl hatch:** a new piece, 0.9 x 1.2 m. A crouched player (1.00 m) gets through; the owner (1.58 m) does not. It would be a refuge, which softens the watch. Not in the plan.
6. **ADR-008 in the manor:** grenades and everything breaking, yes or no.
