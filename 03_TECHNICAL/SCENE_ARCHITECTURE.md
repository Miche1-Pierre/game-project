# SCENE ARCHITECTURE

_How Unity scenes are organized._

## Approach
- A small **Bootstrap** scene loads core services, then the game scene(s).
- One gameplay scene per map (greybox: a single scene).
- Prefabs for everything reusable (player, interactive objects, spawn points).
- Clear scene hierarchy: `_Systems`, `Environment`, `Spawns`, `UI`.

## Conventions
- Prefab-first: build in prefabs, place instances in scenes.
- No gameplay logic hard-wired into a scene object that should be a prefab.
- Naming and folders mirror the code (`ARCHITECTURE.md`).

_Refined with the concept and the first greybox._
