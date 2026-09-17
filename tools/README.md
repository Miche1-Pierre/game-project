# tools

Scripts that run outside Unity and Blender's UI. Both were validated headless on 2026-09-17.

## Generate mesh variants (Blender)

Derives several meshes from one purchased model: two proportional variants, one worn,
one broken. One second, no GUI, no MCP.

    blender --background --python tools/blender/generate_variants.py

Edit `SRC` at the top of the script to point at another model. Check the `UP` constant
first: this pack keeps Y as the up axis after FBX import, other packs use Z.

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

## Pack inventory

`blender/pack_inventory.txt` lists every file in the dungeon pack, for grepping without
opening Unity.
