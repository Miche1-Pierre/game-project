# DEBUGGING

_How we find and fix problems fast, with the MCP in the loop._

## Approach
- Zero Unity console errors is part of "done" (`../04_PRODUCTION/DEFINITION_OF_DONE.md`).
- Use the Unity MCP to inspect the scene and read the console after each change (`MCP_WORKFLOW.md`).
- Cheap in-game debug overlays (state, counts, net role) behind a toggle.
- Deterministic RNG seeds so bugs reproduce.
- Funny vs destructive bugs are triaged differently (`../11_TESTING/BUG_POLICY.md`).

## Rule
Reproduce, then fix. Do not paper over a bug you do not understand (risk R7).
