# ASSET AUDIT + GREYBOX MAP 1 PLAN

_Audit of `UnityProject` for The Movers greybox. Written 2026-09-17. Reference for the session that builds Map 1 via Unity MCP. Concept: `../02_GAME_DESIGN/GAME_CONCEPT.md`, spec: `../02_GAME_DESIGN/GREYBOX_SPEC.md`._

## Environment reality (differs from older docs)
- **Render pipeline: Built-in RP, not URP.** No `com.unity.render-pipelines.universal` in `Packages/manifest.json` or `packages-lock.json`, and the greybox already calls `Shader.Find("Standard")` first. The Broken Vector packs ship Built-in RP materials, so they render correctly as-is. CLAUDE.md and PROJECT_STATE previously said URP: treat the project as Built-in RP (confirm in Edit > Project Settings > Graphics). Switching to URP later would break the third-party materials until upgraded.
- **Input: Input Manager (Old)** (`activeInputHandler: 0`), classic `Input.GetAxis`. No Input System package.
- **Unity 6.6.0f1.** Packages: ai.assistant, ai.inference, ide.rider/visualstudio + default modules. No Cinemachine, ProBuilder, Netcode, extra TMP.

## Unity MCP mechanism (how to get Editor tools into a Claude session)
- The Editor runs the **"Unity MCP Server"** (Edit > Project Settings > Unity MCP Server): a Unity Bridge that exposes ~54 Editor tools when its Status shows **Running**. (The separate "Assistant MCP Extensions" page is the opposite direction, Unity consuming external MCP, ignore it here.)
- Claude connects to that server through a per-user **RELAY**, not the Unity CLI. Correct `.mcp.json` entry (applied):
  `"unity-mcp": { "command": "C:\\Users\\pierr\\.unity\\relay\\relay_win.exe", "args": ["--mcp"] }`
- Gotcha (2026-09-17): the first `.mcp.json` used `unity.exe mcp --project-path ...` (Unity CLI bridge). It reported `connected` with `tool_count: 0` because that transport does NOT attach to the "Unity MCP Server" (which showed "No clients connected"). Switching the command to the relay fixes it.
- To use: Editor open on UnityProject with Unity MCP Server Status = Running, then (re)open the Claude Code session rooted at C:\GameProject so the relay bridge starts. Confirm `session_connectors_status` shows `unity-mcp` tool_count > 0 (and the Editor's Connected Clients shows a client) before driving the Editor. Blender MCP loads in the same session.

## Imported third-party packs (on disk = actually imported, not just "in My Assets")
- **LowPolyStoragePack** (~88 prefabs): Box_01/02, Crate_01/02, Barrel_01-04, Suitcase, Bag, Container, Giftbox, Basket, Cabinet_01-04, Locker, Storage_Rack, EUR-Pallet, Trashcan, Electric_Box, Amp_Rack. Modern, on-theme. PRIMARY reuse source.
- **LowPolyCarPack** (~55): Truck_1, Truck_2, Bus, Car_1-6, Policecar. Truck = the moving van.
- **LowPolyTreePack** (~38): 8 tree types. Garden.
- **LowPolyDungeon** (~200+): medieval stone modular kit (walls/floors/doors/stairs), furniture (Chair, Stool, Shelf, Table_Big, Desk, Chest, Fireplace, CoatHook, Carpet), clutter, torches. OFF-THEME (medieval): greybox structural placeholder or cellar flavor only, not a modern house look.
- **Low Poly Pistol Weapon Pack 1**: off-theme (guns). Recommend removing.

## REUSE / ADAPT / CREATE (Map 1 objects)
| Need | REUSE (on disk) | ADAPT | CREATE later (primitive now) |
|---|---|---|---|
| Cartons, caisses, tonneaux | Box, Crate, Container, Barrel (Storage) | - | - |
| Commode, armoire | Cabinet, Locker (Storage) as placeholder | material/scale, project prefab proxy | real modern dresser/wardrobe |
| Camion | Truck_1/2 (Cars) | add CargoZone trigger, verify a loadable bed | - |
| Voiture d'allee, bus | Cars/Bus | - | - |
| Jardin | Trees (Tree) | - | fence, gate, shed |
| Malles/valises grenier | Suitcase, Bag (Storage) | - | - |
| Gros meubles | - | - | sofa, bed, dining table, armchair, TV unit, bookshelf |
| Electromenager | - | - | TV, fridge, oven, microwave, washing machine, vacuum |
| Structure maison | primitives | - | modern house kit (walls/doors/windows/roof/stairs) |

Rule: reuse before create. No Blender until the carry/load loop is validated. The packs give props + truck + trees, but NOT a modern house structure or big furniture/appliances (those are primitive-now, Blender-later).

## Greybox Map 1 build approach (enterprise-clean, prototype-scope)
- Extend the existing procedural pattern (`GreyboxBootstrap` + menu `The Movers > Create Greybox Scene`) into small builders: `HouseBuilder`, `TruckBuilder`, `ObjectPlacer`, `ContractBuilder`. In editor build, instantiate real prefabs via `AssetDatabase.LoadAssetAtPath` + `PrefabUtility.InstantiatePrefab`, with a primitive fallback. (With Unity MCP live, you can also create/place objects directly in the Editor and verify.)
- Data-light `MovableObjectDef` per contract item (displayName, weight, value, requiredForContract, fragile, prefab path or primitive). Adding a furniture/contract = editing data, not code.
- No GameManager god-object, no global singleton (keep co-op addable later), no netcode, no ScriptableObject framework for 6 objects, no speculative abstraction.
- Scene hierarchy: `_Systems / Environment / Gameplay / Player / Lighting / UI`.
- Assets reorg target, do it IN-EDITOR to preserve GUIDs: `_Project/` (own) + `ThirdParty/BrokenVector/`. Never edit third-party originals, wrap them in project prefab proxies.

## First-playable scope (compact but nervous)
Ground floor: entree, salon, salle a manger, cuisine + one NARROW interior door (the sofa puzzle) + a front opening to the garden. Upstairs: a staircase + grandma's bedroom + one guest room. Outside: driveway + the real Truck prefab + a few trees. Contract: sofa, dining table, TV, bed, dresser, 3 boxes (8 items) + 2 non-required valuables (the extra-value tease). Defer basement / attic / garage / shed / back garden (trivial to add with the kit).

## Success test
Press Play, and it reads instantly: "I am a mover, empty this house into the truck." walk -> grab -> carry (weight fights you) -> route the sofa through the narrow door and down the stairs -> load -> deliver -> paid. If moving objects is already fun, the greybox succeeds, then co-op. If the carry feel is bad, fix `PlayerGrab` before anything else.
