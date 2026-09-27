# ADR-010: A presentation layer before the two-player test

## Status
**Accepted (2026-09-27), Pierre's decision.** It is an exception to CLAUDE.md section 13 ("no final asset until the relevant system is validated") and section 18 ("do not produce final assets"). Pierre asked for it explicitly, knowing the go / no-go has not been played yet.

## Context
Pierre, 2026-09-26, after seeing the slice (translated):
- **Interface:** "Improve the interface to the max: texts, buttons, something nice, cozy, low poly, with the keys to know what to do with each object." He wants buttons framed by branches, moving-house themes, and a loader where a mover runs, falls and gets up.
- **Title screen:** a UI when you arrive, with a background in the spirit of Mecha Chameleon: the truck on a road through a landscape.
- **Characters:** "really smoke, drink, with animations".
- **Sound:** "there is no sound?" He wants her walking, her talking.
- **Tools:** he installed LumaFlow (a declarative UI framework on UI Toolkit) and bought the Off-Screen & Compass Tape Target Indicators asset. He approved downloading the Fredoka font (SIL OFL).

## Decision
1. **The in-game UI moves from OnGUI to LumaFlow on UI Toolkit.**
   - **One HUD root:** a single UIDocument, with one container per player viewport and a scale-down for split screen.
   - **A theme asset:** fonts, palette and 9-slice sprites.
   - **Key hints:** a VerbHints provider shows, for whatever a player looks at or holds, every action available and its key, keyboard or pad.
   - **Pause menu per player,** with options for volumes, sensitivity, language and layout.
   - **Localisation:** a French and English table.
   - **Debug overlays stay on OnGUI.**
2. **The art is rendered in Blender from low-poly models:**
   - wooden planks with rope, and branches for the title;
   - cardboard with tape, and paper tags;
   - a tape-measure bar, key caps and pad glyphs, icons, and the logo;
   - rendered by `tools/blender/render_ui_kit.py`.
3. **A title scene (MainMenu) with a live diorama:**
   - the truck drives a country road, movers carry boxes;
   - a loading screen shows a mover who runs, trips, falls and gets up;
   - `SceneFlow` owns scene loading, and the menu asks for one or two players.
4. **The target indicators use the asset's runtime math only.** Its samples need TextMeshPro, uGUI and the Input System, none of which this project has. The markers are drawn in the game's own UI: a compass tape and edge arrows per viewport for the other player, the grandmother, the truck and the keys.
5. **Smoking and drinking become physical in first person:**
   - the cigarette comes to the lips, the ember glows, the smoke leaves the mouth;
   - the bottle tilts to the lips;
   - the other player sees upper-body clips on the crew body (smoke, drink, throw, pocket, wear, knocked down, get up).
6. **All audio is synthesised at runtime,** with no recorded files and no downloads:
   - footsteps by surface, and the grandmother's shuffle and babbled voice;
   - crew efforts, smoking and drinking;
   - doors, the truck engine, ambience, UI clicks, and a gentle music loop;
   - volumes per channel through `GameAudio`.

## Why
- **Pierre wants to play the complete experience with Jonathan now,** and show it. A readable interface and sound also help the go / no-go, because a spectator must read the situation in 10 seconds (H1).
- **Synthesis over recorded sound:** nothing to license, nothing to download, and every sound tunable in code.
- **LumaFlow:** Pierre chose it. It is at version 0.1.1, so the HUD keeps a legacy OnGUI switch as a way back.

## Consequences
- **Art direction is now committed** earlier than CLAUDE.md wanted. If the go / no-go fails, this layer is sunk cost.
- **Young dependency:** a 0.1.1 UI package. It is wrapped by a small kit (UiKit, UiSkin, UiProbe), so replacing it touches the kit, not every screen.
- **Sound quality:** the synthesised sound is a placeholder by nature. Real recordings would be a later decision.

## Revisit if
- The two-player session shows people reading the HUD instead of the room. The answer is less HUD, not more.
- LumaFlow breaks on an update, or costs frames in split screen.
