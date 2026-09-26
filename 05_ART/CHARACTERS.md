# CHARACTERS

_Simple, silhouette-first, cheap to rig and animate (risk R5). Brief written 2026-09-17, for the neutral outfit and the equip slots. First lot chosen by the team the same day: grandmother's things, stolen and worn. Produced since: the slippers, the glasses (the pack's own), and the bathrobe, validated worn in the map on 2026-09-26._

## Approach
- Low-poly bodies, strong proportions, recognizable from silhouette.
- A shared humanoid rig so animations are reusable; avoid bespoke rigs.
- Minimal or no facial rig, no cloth or hair simulation.
- Team / player distinction through color, not detail.
- Free or generated base meshes, cleaned in Blender.

## The two layers

A character is **one base outfit that is never removed** plus **a few equippable pieces**. The split is not cosmetic. The base carries identity and must survive anything the player does; the equippables are objects in the world that change the silhouette and one number each.

---

## Layer 0, the base outfit

**What it is:** a one-piece work coverall. Sleeves rolled to the forearm, straight legs, heavy boots. Slightly boxy torso volume.

**Why a coverall.** It reads "manual labour" at any distance and in any culture, it needs no logo to be understood, and it is the one costume that survives the open verb divergence: a mover wears it, and a thief wearing it is a thief pretending to be a mover, which is funnier than a balaclava and is a real trope. Choosing it now does not decide the verb (`/00_PROJECT/PROJECT_STATE.md`).

**Proportions:** big hands, big boots, small head. At low poly this sells strength and cartoon in one move, and it gives the gloves and the helmet something to sit on. The height stays 1.80 m, matching the CharacterController (`ASSET_STATUS.md`).

**Color, one rule:** exactly one saturated area per character, the torso panel, using the four existing crew materials. Everything else stays desaturated: dark grey legs, near-black boots, tan straps. Four players in a corridor must stay separable, and that only works if the rest of the body is quiet.

**Hard constraint:** no equippable may cover, tint or obscure the identity panel. Identity is not equipment. A player who takes everything off is still recognizable.

**Amended 2026-09-20, for the bathrobe.** A real bathrobe wraps over and closes, so it hides the torso panel, and the reference the team supplied is closed. Keeping it hanging open to obey the letter of the rule produced an open tunic that did not read as a bathrobe at all. The amendment: **a garment that has to close carries the player colour on its own trim**, here the collar, the belt and the cuffs, as a second material slot Unity paints. The rule's purpose is that four players in a corridor stay separable, and that still holds; the base panel is still underneath when the robe comes off, so a stripped player is still recognizable. This is an exception for full garments, not a licence for accessories to start carrying identity.

---

## Layer 1, the equip slots

Five slots. Each one owes the core loop something, per `/CLAUDE.md` rule 4.

**What counts as paying.** The first version of this brief said a piece must change a number. That was too narrow. A piece pays in **a stat, or in social information the silhouette carries**. Grandmother's slippers change nothing and tell every other player that you have been in her bedroom. Under H1 that is the loop's actual currency, and it adds no system, so it cannot creep. A piece that pays in neither does not get a slot, it gets painted on the base.

| Slot | Anchor (`HumanBodyBones`) | Build | What it owes the loop |
|---|---|---|---|
| Head | `Head` | rigid | Light where the house is dark (cellar, attic), or protection from what falls. Biggest silhouette change for the lowest cost. |
| Back / torso | `Chest` or `UpperChest` | rigid | The carry itself: straps, a harness, a back brace. This is the slot closest to the core verb, so it is the one to get right first. |
| Hands | `LeftHand`, `RightHand` | skinned | Grip. Less sag, fewer drops, fragile things survive the trip. |
| Waist | `Hips` | rigid | Carrying small things without occupying the hands. |
| Feet | `LeftFoot`, `RightFoot` | rigid | Added 2026-09-17 for the slippers. Cheap, and the feet are the one zone nothing else competes for. |

**The face slot, reopened.** The first version of this brief kept it shut because a balaclava is the most verb-loaded object in the game. Grandmother's glasses reopen it without deciding anything: they conceal nothing, they humiliate. The slot is open for comedy and stays shut for concealment until the divergence closes (rule 16).

### Where a piece comes from
Proposal, not a decision: **an equippable is a `MovableObject` like any other.** You find it in the house, you grab it with the grab that already exists, and equipping is a second verb on an already-held object. No pickup system, no inventory, no UI. It also means the gloves you are wearing are worth money in the truck, which is a choice the player has to make and costs nothing to build.

### Readability, the tests a piece must pass
1. **The 8 metre test.** Backlit, two objects in the way: another player can name who it is and what they are wearing. If the piece only reads up close, it fails.
2. **The silhouette test.** Cut the piece to a black shape. If it does not change the outline, it is a texture, not equipment.
3. **The social test.** Seeing the piece on someone else tells you something useful, for example that they should be the one taking the fridge. This is hypothesis H1 paying rent.
4. **First person.** You never see your own outfit while playing. Design effort goes where it is seen: on the others, at 3 to 10 m, and in the Steam capsule.

---

## Technical rules

- **Design every piece so it can be rigid.** A mesh parented to a bone costs almost nothing and works because the character imports with `optimizeGameObjects: 0`, so the bone hierarchy exists in the scene. Go skinned only when the piece crosses a joint that really bends, which in practice means the gloves.
- **Skinned pieces share the base armature.** Modeled over the imported `male01_1.fbx` body, exported with that same armature, re-bound in Unity by bone name. The names are not enough: Blender's re-import turns the bone frames, so the piece's own bind poses tear it apart on an animated body (the robe, 2026-09-25: arms off by 160 to 170 degrees). `CrewEquip.BakeIntoBodySpace` fits the piece's joints onto the body's and skins it with the body's bind poses; a skinned piece therefore imports with Read/Write on.
- **Only the sleeves take an arm.** A garment's body, skirt, collar and belt get their weights from the torso alone. Looked up at hip height, the widest fold of a hem lands next to the hanging hands of the bind pose and flies off with them (`FAR_ARM_GROUPS` and `check_weights` in `tools/blender/model_bathrobe.py`).
- **Cloth shades smooth.** Smooth by angle at 45 degrees, rims kept sharp. Flat shaded, the robe's vertical facets read as lamellar plates, which is where "samurai" came from.
- **A piece is judged worn, in the map, in both poses.** `MoversWearCLI` puts it on the player's body in `Map01_PierreKit_House` through the F key's own call and shoots it at rest and carrying, at 3 m and 8 m. A Blender render has no scene light, no crew colour and no animation; two of the robe's three faults only showed in the map.
- **The crew has two poses.** Relaxed with empty hands, the carry pose while carrying (`CrewPose`, `AC_Crew`). A garment is designed against the relaxed one, which is how every other player sees you most of the time.
- **`bake_space_transform`: ON for rigid pieces, OFF for skinned ones.** The earlier blanket "always off" was wrong and cost a round trip. Off, the Blender to Unity axis conversion is not baked into the mesh, it lands on the imported root instead: the slipper arrived with the foot running along Y and lay on its back the moment anything set a world rotation. On is what the static kit already does, and it is only unsafe for armatures.
- **Export with `apply_unit_scale=True` and `apply_scale_options="FBX_SCALE_ALL"`.** With `False` and `FBX_SCALE_NONE`, copied from `author_carry_clip.py`, the mesh arrives 100 times too small with a compensating 100 on the root scale. That script's note that the unit flags change nothing is true for an animation, which retargets through the avatar and carries no scale, and false for a mesh.
- **Nothing that places a piece may assign over its root transform.** `CrewEquip` composes with the imported rotation and multiplies into the imported scale, because an FBX can legitimately carry either. Both bugs looked like modelling errors and were not.
- **Pivot at the anchor**, piece modeled in place on the body, exported alone.
- **Author the fit in the body frame, never the bone frame.** Measured on this pack 2026-09-17: the head bone is `spine.005` and its local `+Z` points at the floor, because bone axes are whatever the rig author did in Blender. An offset of "4 cm up" in bone space put the glasses 10 cm below the skull. `CrewEquip` places a piece against the character's own right / up / forward instead, so x, y and z mean what an artist tuning a slipper expects. The same fact is why anchors resolve through `HumanBodyBones` and not by name: nothing in this rig is called "Head".
- **Blender local space is not world space.** The crew mesh object carries an import rotation, so the face sits at -Y in the mesh local frame and at +Y in the world. Cuts and measurements are local and agree with each other; a render camera is not, and aiming one with a local number renders the back of the head. Settle the question with the eyes submesh centroid, not with a bone.
- **Check the prop's real size before wearing it.** `SM_Glasses` measures 23 cm across, because it was modeled to sit on a bedside table. A house prop reused as a worn piece needs a scale factor, and that factor belongs in `EquipItem`, not in a second copy of the mesh.
- **Budget, by class.** An accessory stays well under a tenth of the body's triangle count. A full garment is a different object and the rule was wrong to lump them: the bathrobe is 784 triangles against a body of 1862, and the reference asset the team supplied is 1.2K. Judge a garment against comparable garments, an accessory against the tenth.
- **Superseded budget line:** a piece stays well under a tenth of the body's triangle count and adds **zero new materials**. Reuse `Floreswa/Materials` and the four crew materials. The exact triangle number gets fixed when the rig is opened, not guessed here.
- **Naming:** `SM_Crew_<Slot>_<Name>.fbx`, sources in `_ArtSource/`, consistent with the kit convention in `/tools/README.md`.
- **The multiplier collision.** `speedMultiplier` and `jumpMultiplier` on `PlayerController` already exist and are already written every frame by `PlayerGrab` from the carried weight. Gear must not become a second writer of the same field. Whatever the system ends up being, it contributes a factor that the carry logic multiplies in, it does not assign.

## First lot: grandmother's things

Chosen by the team, 2026-09-17. Three objects the player finds in the house, steals and puts on. They beat the obvious lot (hard hat, harness, gloves) on three counts: every co-op game has a hard hat, none has a stolen pink dressing gown (R17, instant cloning); what you wear says where you have been, which is spatial information on a silhouette; and the go / no-go is spontaneous laughter, which a hard hat has never produced.

| Piece | Slot | Build | Found in | Says |
|---|---|---|---|---|
| Slippers | Feet | rigid | by the bed | you went through her bedroom |
| Glasses | Face | rigid | nightstand or kitchen table | you went through her things |
| Pink dressing gown | Torso | **skinned** | bathroom door, or on the dress form | you took your time |

Two rigid, one skinned, so the lot still validates both technical paths.

### What already exists
- **`Floreswa/Prefabs/glasses01.prefab` and `glasses02.prefab`.** The character pack ships glasses authored for this exact head. The head anchor is already solved and the slot can be prototyped with zero modelling.
- **`SM_Glasses.fbx` / `PF_Glasses.prefab`** in `GrandmaKit/Props/Misc`. The house version of the same object.
- **`SM_Dress_Form.fbx`** in `GrandmaKit/Props/Special`. Hang the gown on it in the bedroom: free staging, and a readable "something wearable lives here" signal.

**The house prop and the worn piece are the same mesh.** One `SM_Glasses`, sitting on the nightstand as a `MovableObject`, parented to the head bone when equipped. Zero extra art, and it proves the "an equippable is just a MovableObject" proposal on a real case.

### What has to be modelled
- **Slippers.** The base outfit has heavy work boots. Do not split the base mesh to remove them: model the slipper big enough to swallow the boot. A pink fluffy slipper visibly too small for a mover's foot is the joke, and it costs one mesh instead of a mesh plus a base-outfit change.
- **The gown.** The only real work in the lot, and the only piece that collides with the identity rule above: a closed pink gown covers the torso panel.

### The gown, resolved
**Closed, mid-calf, the trim in the player colour.** The first answer here was worn open and knee length, to keep the identity panel visible. Built that way it read as an open tunic, not a bathrobe, so the robe closes and its collar, belt and cuffs carry the player colour instead (the amendment under Layer 0). Mid-calf rather than floor length because the skirt follows the pelvis, not the legs, and a longer hem would let a knee through; the slippers still show under it. Validated worn in the map, in both crew poses, on 2026-09-26.

### The choice it creates for free
Each of these is a `MovableObject` with a `contractValue`. Wear it or sell it. Nothing to build, and under the theft reading later, wearing her dressing gown while she is still in the house is the whole game in one image.

## Open, not decided here
- **Which scene is the greybox.** `Tutorial_01`, `Map01_Grandma` and `Map01_GrandmaHouse` all exist. `PROJECT_STATE.md` names Tutorial_01 as the current build. The lot needs one target.
- Whether any gear ever modifies gameplay numbers. The first lot deliberately does not.
- Concealment on the face slot, which still waits on the verb.

## Scope note
The line holds where it was drawn: **cosmetic stolen loot is in, stat modifiers stay out.** Stat gear is progression, which `GREYBOX_SPEC.md` puts out of scope, and it is flagged not started (rules 15 and 18). This lot adds no ledger, no score, no owner and no detection, so it breaches none of that list, and it serves the one thing the greybox exists for: the laughter test with a second player, which is action 1 in `PROJECT_STATE.md`.

One inconsistency worth fixing in passing: the same out-of-scope list says "real assets", and the current build already ships GrandmaKit meshes with `PROJECT_STATE.md` recording it as done. That line is overtaken by practice and should be updated rather than left saying something untrue.
