# tools

Scripts that run outside Unity and Blender's UI. Both were validated headless on 2026-09-17.

## Generate mesh variants (Blender)

Derives several meshes from one purchased model: two proportional variants, one worn,
one broken. One second, no GUI, no MCP.

    blender --background --python tools/blender/generate_variants.py

Edit `SRC` at the top of the script to point at another model. Check the `UP` constant
first: this pack keeps Y as the up axis after FBX import, other packs use Z.

## Author a carry clip (Blender)

The character pack ships three rigged bodies and zero animations. This writes the carry
pose onto the rig plus a two-second looping breath, and exports an FBX that Unity imports
as Humanoid and retargets onto the character.

    blender --background --python-exit-code 1 --python tools/blender/author_carry_clip.py

Defaults to `male01_1` and the `Generated/Characters` folder. Override with `--src`,
`--out` and `--preview` after a bare `--`. It reads up and forward off the bones instead
of assuming an axis, so it runs on the other two bodies in the pack unedited.

Two manual steps remain in Unity. Blender names the FBX take after the scene, so rename
the clip to `Carry_Idle` and tick Loop Time. And set the avatar to **Create From This
Model**, not Copy From Other Avatar: the reason is in the script's `export` docstring.

The clip plays on the "Carry" layer of Pierre's `AC_Crew_Slice`, whose idle is the pose of a
body with empty hands (clips from `author_clips.py`). The `--pose relaxed` option and
`MoversCrewPoseCLI`, written for the bathrobe pilot, were dropped when that controller was
merged on 2026-09-26.

## Headless runs, five rules learned the hard way

1. **Pass `--python-exit-code 1` to Blender.** Without it `blender -b` exits 0 when the script
   raises, so a generator that refuses to export (the bathrobe's checks do exactly that) looks
   like a success to whatever called it.
2. **Compile Unity on its own before any `-executeMethod`.** A compile error keeps batchmode
   busy for more than ten minutes before it exits. `Unity.exe -batchmode -quit -projectPath
   ... -logFile compile.log` takes about 35 s here; grep the log for `error CS`.
3. **Nothing in batchmode while the editor is open on the project.** The editor holds the
   project lock. Use the Unity MCP instead, or close the editor.
4. **Read the real exit code.** From Git Bash, `blender ...; echo $?` is reliable. From
   PowerShell, `&` does not wait for Unity.exe, which is a windowed program, and
   `Start-Process -PassThru` only reports `ExitCode` if the process handle was read while it
   ran (`$null = $p.Handle`). A probe that raises must come back 1 before a 0 means anything.
5. **Batchmode cannot sign in.** A package from the Asset Store that is missing from
   `Library/PackageCache` stops Unity at package resolution, before any compile or import,
   with "Cannot install package. You are not signed in" and exit 1. It happened on 2026-09-27
   with `com.sahland.lumaflow`. Open the project once from the signed-in Unity Hub; never put
   credentials on a command line.

## Model an asset (Blender, headless)

The asset workflow of ADR-011: each asset is a versioned generator script on one shared
module, `tools/blender/movers_blender.py`: paths from the repository root, the crew body
import, materials from the palette, the FBX presets and review settings of
`05_ART/style/profile.json`. The rules are in `05_ART/STYLE_GUIDE.md` sections 18 to 20.

    blender -b --factory-startup --python-exit-code 1 --python tools/blender/model_bathrobe.py
    blender -b --factory-startup --python-exit-code 1 --python tools/blender/model_slippers.py

- **Scratch first.** A generator reads `OUT`, `BLEND` and `PREVIEW` when `main()` runs, so a
  small runner that imports it and points them at a scratch folder tries a change without
  touching a tracked file. `model_slippers.py` also takes `--out`, `--preview` and `--blend`
  after a bare `--`.
- **Review renders.** `render_turnaround` draws Workbench through the Standard view transform,
  colours as Unity shows them, back faces culled. Four views next to the preview; in its
  `review/` folder (gitignored) the body's silhouette with and without the piece, with a
  `SILHOUETTE` line saying how much outline it adds, and the piece seen from 8 m.
  `contact_sheet` lays images side by side for a verdict.
- **Exports fail loudly.** The rigid, skinned and animation presets run in Object mode, check
  that a file was written, and never fall back to `wm.fbx_export`, which writes centimetres.
- **Two traps found while moving the scripts onto the module (2026-09-27).** A review gives
  the scene a world, and the FBX exporter copies the world's colour into every material's
  AmbientColor, so a script whose FBX must not change renders after exporting. And the
  imported crew rig stands turned 180 degrees about Z: a worn piece built in the body's frame
  renders on the other foot, back to front, unless it is parented to the armature first. Every
  slipper preview until then showed the right boot's toe through the left slipper.
- **Proof the move changed nothing.** Both generators, before and after, print the same
  `CHECK`, `MEASURE`, `ROBE_` and `SLIPPER_` lines and exit 0; with a fixed `PYTHONHASHSEED`
  (and `--python-use-system-env`) their FBX files are identical byte for byte outside the
  header's creation timestamp. Without the seed they differ, because the exporter derives its
  ids from Python's randomised `hash()`.

## Check the style register, measure the kit (Blender, headless)

    blender -b --factory-startup --python-exit-code 1 --python tools/blender/movers_blender.py
    blender -b --factory-startup --python-exit-code 1 --python tools/blender/measure_assets.py

The first checks `05_ART/style/profile.json`: every reference glob finds a file (the anti
entry in its commit), every tier and family is known, every palette `hex` matches its `srgb`.
It exits 1 with `PROFILE_FAIL` lines otherwise. The second measures `PK_*.fbx` and
`PKX_*.fbx` into `05_ART/style/metrics.json`, and never opens `_ArtSource/assets.blend`.

## Check a model the way Unity imports it (Unity, no clicking)

What the game actually gets from a file Blender wrote: importer settings, triangles per
submesh, bounds, shaders (the magenta error shader fails the run), and for a skinned piece,
its bones and bind poses against the body's, plus the joint fit `CrewEquip` uses. Exits 0 when
clean, 2 otherwise, no `-quit` needed:

    Unity.exe -batchmode -projectPath C:\dev\game-project\UnityProject \
      -executeMethod Movers.EditorTools.MoversInspectCLI.ReportAsset \
      -asset Assets/_Project/Art/Crew/SM_Crew_Chest_Bathrobe.fbx \
      -body Assets/Floreswa/Models/male01_1.fbx -logFile inspect.log

## Wear a piece in the house (Unity, no clicking)

Opens `Map01_PierreKit_House`, puts the piece on the player's crew body through
`CrewEquip.Equip` (the call the F key makes), and shoots it at rest and carrying, at 3 m and at
8 m, then from the player's eyes in the bathroom mirror. Fails on a CrewEquip warning, an
error, or any edge of the piece stretched past 2.5 times its bind length. Play mode, never
saves a scene; images go to `Assets/_Movers/Generated/review/`, gitignored. Do NOT pass
`-quit`:

    Unity.exe -batchmode -screen-width 1920 -screen-height 1080 \
      -projectPath C:\dev\game-project\UnityProject \
      -executeMethod Movers.EditorTools.MoversWearCLI.Run \
      -piece Assets/_Project/Art/Crew/SM_Crew_Chest_Bathrobe.fbx -slot Chest -logFile wear.log

Not covered: the F key itself, which is input. The mirror shot shows an empty pane in
batchmode; whether the mirror reflects in the editor is unverified.

## Drive the Blender MCP bridge

The addon will not start from a cold command line without the online flag, and the error
message does not say so clearly.

    blender --background --online-mode --command blender_mcp --port 9876

## Apply the visual swap (Unity, no clicking)

Opens Tutorial_01, nests a real mesh inside every MovableObject, saves the scene.
Physics is never touched. The reverse method is `RunRestore`.

    Unity.exe -batchmode -quit \
      -projectPath C:\dev\game-project\UnityProject \
      -executeMethod Movers.EditorTools.MoversVisualSwapCLI.RunSwap \
      -logFile swap.log

The same operation is available from the editor menu: **The Movers > Visual Swap**.

## Check the starting items (Unity, no clicking)

The cigarette and the beer, from the editor or from a command line. All of these are also in
the editor menu under **The Movers**. None of them saves a scene, the puffs they spawn are
removed on the way out, and the images they leave under `Generated/` are gitignored debug
output, regenerated on every run: worth looking at, not worth arguing with in a diff.

Put the two items on the ground by the truck, in scenes saved before they were objects, and
strip the dead components the viewmodel version left behind. Idempotent, and it refuses to run
while the game is playing, because a scene edited in Play mode is thrown away on exit:

    Unity.exe -batchmode -quit \
      -projectPath C:\dev\game-project\UnityProject \
      -executeMethod Movers.EditorTools.MoversStartingInventoryCLI.RunInstall \
      -logFile inventory.log

Both items end to end: laid out, smoked, thrown, replaced, drunk, broken, replaced, and every
effect back to zero once sober. Enters Play for about fifteen seconds, shortens the sobering
clock so the run is not half a minute, says so in the log, and restores every project setting
it touched. Do NOT pass `-quit`, it exits by itself:

    ... -executeMethod Movers.EditorTools.MoversItemsPlaytestCLI.RunPlaytest

It drives the items through their own API rather than a keyboard, so one thing is deliberately
**not covered**: whether a tap of the right button throws and a hold smokes. That is input
timing. Press it yourself, it is the first thing to check by hand.

### The smoke itself, in two more entry points

Numbers only, no graphics device needed. Prints the density of one puff over its whole life,
the falloff by distance, and what stacking puffs does:

    Unity.exe -batchmode -quit \
      -projectPath C:\dev\game-project\UnityProject \
      -executeMethod Movers.EditorTools.MoversSmokeCLI.RunProbe \
      -logFile smoke.log

Images, needs a graphics device, so do not pass `-nographics`. Renders the player camera with
the cloud in front of it and paints the real overlay on top, into
`Assets/_Movers/Generated/smoke/`. It calls the game's own `SmokeVision.DrawSmoke`, so the
preview cannot drift from what ships:

    ... -executeMethod Movers.EditorTools.MoversSmokeCLI.RunPreview

## Pack inventory

`blender/pack_inventory.txt` lists every file in the dungeon pack, for grepping without
opening Unity.

## Agent access: Blender and Unity over MCP

Both bridges let an agent drive the tools directly instead of going through batch-mode
command lines. Set up 2026-09-17.

### Blender

The addon ships with Blender 5 and refuses to start from a cold command line without the
online flag, with an error that does not say so.

    "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" \
      --background --online-mode --command blender_mcp --port 9876

Leave it running. The agent then has full scene access: import, mesh edits, modifiers,
export, render.

### Unity

`com.unity.ai.assistant` is in `UnityProject/Packages/manifest.json`. It provides the Unity
MCP Server, which is free on every plan. Only the AI asset generation tools inside it cost
credits, and those can be unchecked one by one in **Edit > Project Settings > AI > Unity MCP
Server**. Do that: it removes the risk rather than managing it.

The bridge starts on its own when the editor opens. An external client connects through a
relay binary Unity installs at `%USERPROFILE%\.unity\relay\relay_win.exe`.

Registered for this project with:

    claude mcp add unity-mcp --scope local -- \
      "C:\Users\jonat\.unity\relay\relay_win.exe" --mcp \
      --project-path "C:\dev\game-project\UnityProject"

The `--project-path` matters: without it the relay attaches to whichever editor it finds
first, and there is more than one Unity project on this machine.

Two things the agent cannot do for itself:

1. **Unity has to be open** on this project. The bridge does not exist without an editor.
2. **The first connection needs approval** in the Unity MCP Server settings page, and a
   session restart on the client side, because an MCP server added mid-conversation is not
   picked up until then.

## Check and draw a house plan (Python, stdlib only)

The PierreKit maps are data on the 3 m grid: `_ArtSource/house_plan.txt` (Map01, the
cottage) and `_ArtSource/manor_plan.txt` (the manor proposal, `02_GAME_DESIGN/MAP02_MANOR.md`).

    python3 tools/plan/plan_check.py _ArtSource/manor_plan.txt --html manor.html

It fails (exit 1) on: a perimeter or room-to-room side not listed, a stair that does not
line up, an upper cell over nothing or a void over no stair, a gable wider than the kit's
two-cell span, and a cell that cannot be reached from outside. Then it prints the loops per
level, and per room the widest route out and the metres to the nearest exit. `--html` and
`--svg-dir` draw each level to scale; rooms named `hidden_*` and the doors into them are
drawn in red. Both plans pass.
