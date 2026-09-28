# ADR-013: What you carry is in your arms

## Status
**Proposed (2026-09-28), hot-fix ticket from Jonathan (Spykernv): "the two-hand carry and the wheel's distance".** Built and measured by script, not played by a person. To be read and accepted by Pierre (Miche1-Pierre), because it changes the carry that playtest 001 validated.

## Context
- **The ticket:**
  - things seem to float between the two hands;
  - the hands must sit on the right points of the object, physically and visually;
  - the arms follow it as it comes in, goes out or moves ("tendre/détendre");
  - it can never be pushed further than 1.50 m;
  - it must work for any size without heavy set-up.
- **The cause:** the carry drove the object's centre to a point 1.0 to 3.2 m along the look (the wheel set it). The arms end at about 0.5 m (the crew body: upper arm 0.27, forearm 0.20). The hands stopped at arm's length in the air, short of the object.
- **What already existed:**
  - the carry's weight feel: lag, slower turns, heavy things hanging lower (playtest 001: "c'est parfait");
  - the first-person arms drawn on the body's bones, since the body/shadow hot-fix of 2026-09-27 (`LimbIK`), so one set of hand targets drives the first-person view, the shadow and the other player's view.

## Options
- **A. Keep the rail, stretch the arms to it.** The arms would reach 2 to 3 m: cartoon limbs, against "more realistic".
- **B. Keep the rail, cap it at 1.50 m, hands reach and stop.** Meets the distance, still floats: a 0.4 m box at 1.5 m is a metre past the hands.
- **C. Put the object in the arms.** It is held where the hands can hold it, and the wheel bends and stretches the arms. The hands sit on holds worked out from the object's box. The 1.50 m is a cap that only long objects approach.

## Decision
**Option C.**
1. **Holds from the box** (`CarryGrip`), no set-up per object:
   - a palm on each side up to 0.9 m across as seen (with a margin so the hands do not flip);
   - the palms on the near face when wider;
   - the right hand alone at 20 cm and under.
   Each hold is the nearest point of the real box, turned as it is. An optional `HoldPoints` component takes over for a shape the box reads wrong; none is needed today.
2. **The carry** (`PlayerGrab`):
   - The object rides at chest height, lower if it is tall so you can see over it, never below the floor.
   - Its holds sit at the depth the wheel sets (arms bent 0.15 to 0.27 m, stretched 0.42 m in front of the eyes), pulled in until they are within the arms' reach.
   - Its centre never goes further than 1.50 m from the eyes.
   - Heavy things cannot be stretched out fully (same weight factor as before).
3. **The frame:** the object follows the look only between 35 degrees up and 20 degrees down, so looking at your feet does not swing it into your chest.
4. **The feel:**
   - It moves with you: 60 to 100% of your velocity, by weight. Starts and turns still lag, which is the weight.
   - Past the arms' reach it is pulled back.
   - Heavy things still hang lower and turn slower.
5. **Two rules the ticket did not ask for, added so the hands stay on the object:**
   - **Grip loss:** snagged more than 0.35 m past the arms' reach for 0.3 s (a door frame), it is let go, as a drag already was.
   - **Walk stop:** blocked by a wall (a physics step took off at least a third of the forward speed the carry gave it), it stops you walking on into it: after 15 cm, or when its near face comes within 12 cm of the eyes, counting what you cover before the stop holds. The eyes are also kept 12 cm clear of anything whose height and width they are within (crouched under a tall piece). Backing off and sidestepping stay free. The carrier's own capsule lets the object through (it rides inside the capsule's radius) and collides with it again once they are apart.
6. **Unchanged:**
   - the cigarette, the beer and the grenade keep their rail, capped to 1.0 to 1.5 m, so the smoke of ADR-005 still forms clear of the smoker;
   - drags (too heavy to lift) are unchanged.

## Consequences
- **Measured by script (2026-09-27 and 28), eleven objects from the keys to the piano:**
  - the first-person palm is on its hold at 6 mm;
  - the body's hand is within 1.4 cm of the surface;
  - no centre goes past 1.45 m.
  Detail in `changelog/CHANGELOG.md`.
- **Visibility:**
  - In the side-by-side split screen each view is almost square, so on a wide object the palms sit at the left and right edges. They are in view when the arms are stretched.
  - Heavy things hang low: their hands show when you look down.
  - A wardrobe cannot be seen over, by design.
- **Loading:** posting things into the truck from 3 m away is gone; you walk up the ramp.
- **Online:**
  - the host carries for both players;
  - the walk stop is not sent to the client, which stops on its own when the load reaches its eyes;
  - the client's dropped item collides with it again only once apart.
  Not played online yet.

## Open tuning (for Pierre's hands-on pass)
All tuning values are fields on `PlayerGrab`, except the carry height, which is a constant in `CarryGrip`:
- the carry height (`holdDrop` 0.22, `CarryGrip.CarryHeight` -0.24);
- the arm depths (`bentGripDepth` 0.15, `stretchedGripDepth` 0.42);
- the wheel step (`extensionSensitivity` 3, so a notch is 0.3 of the way);
- the grip loss (`carrySlack` 0.35, `gripLossSeconds` 0.3);
- the walk stop (`jamDistance` 0.15, `jamLookahead` 0.04 s, `jamSpeedLoss` 1 m/s);
- the follow (`heavyFollowThrough` 0.6).
- Whether the grip loss and the walk stop read as funny failures or as frustration is the question to answer by playing.
