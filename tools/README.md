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
