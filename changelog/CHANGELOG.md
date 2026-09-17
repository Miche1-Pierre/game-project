# CHANGELOG

_Not just code. Categories: DESIGN, TECH, RESEARCH, DOCS, CONTENT, BALANCE, BUSINESS, MARKETING._

## 2026-09-17

### Grandma Kit: full asset list frozen + interior furniture batch (Blender)
- **DOCS:** froze the complete asset inventory for the grandma-house game in `05_ART/ASSET_LIST.md` (~340 targets across architecture, furniture per room, kitchen, bath, bedroom, living, office, basement, garden, cat, food, props, nature, materials), with naming/family/scale conventions and folder structure. Scope decision (Pierre): list first, he drives production, start with a vertical slice. Max interior scope; cat deferred; roof pitch left non-uniform (he will adjust).
- **ART:** modeled interior furniture batch 1 (14 pieces, low-poly, no textures): Bed_Old, Nightstand_01, Dresser_01, Wardrobe_01, Sofa_01, Armchair_01, Rocking_Chair, Coffee_Table, Side_Table, Bookshelf_01 (with books), TV_Old, Fireplace_Living, Dining_Table_01, Dining_Chair_01. Exported to `Assets/_Project/Art/GrandmaKit/Furniture`.
- **ART:** modeled interior batch 2 (12 pieces): kitchen (Counter_Straight, Cabinet_Upper, Sink, Stove_Old, Fridge_Old retro, Range_Hood, Kitchen_Island), bathroom (Bathtub_Old clawfoot, Toilet, Sink_Bath pedestal, Shower), office (Desk). Exported to `Assets/_Project/Art/GrandmaKit/{Kitchen,Bathroom,Office}` with mirrored prefabs.
- **ART:** modeled batch 3 (16 pieces): lighting (Table_Lamp, Floor_Lamp, Chandelier), decoration (Vase_Plant, Picture_Frame_01, Wall_Clock, Rug_01), basement (Shelving_Unit_Cave, Barrel, Crate), garden (Garden_Bench, Wheelbarrow, Mailbox), nature (Flower_01, Mushroom_01, Rock_01).
- **ART:** modeled batch 4 (18: kitchenware + food) and batch 5 (14: tools, books, luggage, sewing, special). Running total: 101 kit pieces (27 architecture + 74 interior/props/food/garden/nature), all FBX + flat-color drag-and-drop prefabs with colliders. Palette extended (Pot, Mush, FabMint, Bread, Cheese, Wine). Prefab builder mirrors the whole Art/GrandmaKit tree into Prefabs.
- **ART:** modeled batch 6 (16: furniture variants + garden + nature): Sofa_02, Armchair_02 (wingback), Display_Cabinet, Coat_Rack, Chest, Wood_Stove, Vanity, Well, Planter_Box, Birdhouse, Garden_Arch, Flower_02, Mushroom_02, Rock_02, Bush_01, Grass_Tuft. Running total: 117 kit pieces.
- **ART:** modeled batch 7 (16: textiles + lighting/deco + special + misc): Curtains, Cushion_01/02, Rug_02, Wall_Sconce, Candlestick, Oil_Lamp, Painting, Mirror_Wall, Vase_02, Telescope, Bird_Cage, Dress_Form, Model_Ship, Crucifix, Alarm_Clock. Running total: 133 kit pieces.
- **ART:** modeled batch 8 (18: archi variants + garden + lab + big furniture + misc): Wall_2m, Wall_Corner, Window_Round, Door_Interior, Stairs_Spiral, Fence_Stone, Trellis, Fountain, Scarecrow, Lab_Table, Microscope, Flask_Set, Piano, Secretary_Desk, Keys, Glasses, Perfume, Medicine_Box. Running total: 151 kit pieces covering every list category (most with variants).
- **ART:** structure-completion lot (12): interior partitions (Wall_Interior_Solid/Door/Opening), Wall_1m, Wall_3m, Wall_Corner_Inner, Terrace_Deck, and a veranda set (Veranda_Panel/Corner/Roof/Door + faceted rounded Veranda_Bay). Fills the gaps for building + partitioning the house with a terrace and a straight or rounded conservatory. Running total: 163 pieces.
- **ART:** consolidated all 163 pieces into a single editable source `C:\GameProject\_ArtSource\GrandmaKit_All.blend` (22 collections by category, upright, correct scale, flat colors). Fixed a Blender FBX re-import gotcha (pieces came back 100x and lying down; corrected to metric scale + standing).
- **TECH:** generalized the prefab builder (menu The Movers > Build Kit Prefabs) to scan CottageKit + GrandmaKit, mirror the folder tree into `/Prefabs`, and color per submesh from any `*_mats.json`. 14 furniture prefabs generated (flat color + collider).

### Cottage Kit: complete modular low-poly building asset pack (Blender)
- **ART:** modeled a complete modular low-poly building kit in Blender ("Cottage Kit", 27 pieces, no textures, detail carried by geometry): walls (solid / window / door / garage) with plinth, cornice, corner boards and board battens; shuttered multi-pane window; decorative Craftsman gable end (fascia, tie beam, king post, struts, vent) plus a plain gable infill; roof slope and ridge cap; dormer; chimney; bay window; porch column (tapered on a pier), beam and railing; deck tile; exterior stoop steps; interior stairs; front door (paneled, with a hood) and sectional garage door; brick foundation strip; 4x4 floor; stepping stones; fence section, post and gate; hedge; bush. Exported to `Assets/_Project/Art/CottageKit/SM_*.fbx` with the validated upright / correct-scale settings.
- **TECH:** generated flat-colored, collider-ready drag-and-drop prefabs (`PF_*` in `Assets/_Project/Prefabs/CottageKit`) via `CottageKitPrefabBuilder` (menu The Movers > Build Cottage Kit Prefabs); colors are assigned per submesh from a Blender-exported material map (`cottage_kit_mats.json`), so they survive any FBX material import mode. Added a browsable `CottageKit_Catalog` scene (menu The Movers > Open Cottage Kit Catalog). Verified in-engine: pieces import upright, correct scale, colored, with mesh colliders.
- **DESIGN:** per Pierre, the deliverable is the KIT itself (he assembles Map 1 in Unity), quality target = cozy low-poly cottage references (Craftsman brackets, shutters, columns, brick base). Module = 4m walls, 3m high.

### Map 01 environment greybox (modular Blender kit)
- **ART:** modeled a modular architecture kit in Blender (21 pieces, greybox, no textures, boolean-cut openings): exterior walls (solid / door / window / big-opening / bay), interior walls (solid / door / opening), window frame, door, 4x4 floor, straight stairs, chimney, garage roller door, fence, gate, ladder, two gable roofs, plus rock and garden table. Exported to `Assets/_Project/Art/{Architecture,Props}` with `bake_space_transform` (upright, correct scale).
- **CONTENT:** assembled the full Map 01 environment scene `Map01_House` (`Map01EnvBuilder`, menu The Movers > Build Map 01 Environment): 2-storey house (multi-room ground floor + attic, gable roof, framed windows, front door + rear bay), attached garage, front terrace + table + railings, underground cellar with interior stairs, small hangar, quick farm (barn + fenced field), garden (reused Broken Vector trees, rocks, driveway, front fence + gate). Walls use mesh colliders so door/window openings are walkable. Runs in Play, 0 errors. Structural / environmental only (empty interior, no textures) per request; objects and physics come after.
- **TECH:** generated drag-and-drop prefabs (`PF_*` in `Assets/_Project/Prefabs/{Architecture,Props,Furniture}`) from the kit models, each with a shared material + mesh collider (`KitPrefabBuilder`, menu The Movers > Build Kit Prefabs). The environment scene is assembled from prefab instances, so making pieces breakable later = add one component to a prefab and every placed instance inherits it.
- **ART:** upgraded the kit from plain blocks to designed low-poly assets: beveled edges, wall plinth + cornice, framed windows (sill / mullion / lintel), panelled door with handle, pitched roof with ridge cap + fascia + overhang, chimney with cap, sectional garage door, post-cap fence + gate, balcony, porch post. Rebuilt the house as **1 storey + accessible attic** (gable roof directly on the ground-floor walls) with a covered entrance porch + roof chimney. 28 prefabs total.
- **ART:** confirmed only 5 Asset Store packs exist in the project (Cars, Storage, Tree, Ultimate Low Poly Dungeon, Pistol); no modern house/fence/rock pack. So created a **modern low-poly house pack** with a flat-colour palette (no textures, Broken Vector style): cream walls, terracotta roof, white window frames, wood trim, blue front door, grey garage door. **Reused real Broken Vector assets** for props: `Table_Big` + `Bench` (terrace), `Balcony_Railing` (fences), `Car_2_Blue` (driveway), Storage barrels + crates (yard/garage). Front door placed in the entrance. Result reads as a coherent modern cottage.
- **CONTENT:** polish / levelling pass on `Map01_House`. Reused props now **ground-snap** (the mesh bottom is placed exactly on the target surface), which fixed the sunk car, floating railings and crates in one go. Replaced the flat ground cube with a **Unity Terrain** (flat plateau under the property, gentle Perlin hills beyond, a lowered pocket under the cellar so the stairs stay passable; green flat material). Terrace: `Table_Big` -> `Table_Small`, benches turned to face the table, porch posts + door seated on the deck. Shed rebuilt as a lean-to (sheet roof sized to its posts) instead of the oversized gable. Verified by editor captures + object bounds; walkthrough (cellar pocket, hills) still to playtest.

### Unity MCP wired + Map 01 greybox built (driven through the Editor)
- **TECH:** fixed the Unity MCP connection. The `.mcp.json` `unity` entry used the wrong transport (`unity.exe mcp`, connected but 0 tools); switched to the Editor's "Unity MCP Server" relay (`unity-mcp` = `%USERPROFILE%\.unity\relay\relay_win.exe --mcp`, 54 tools). Mechanism documented in `03_TECHNICAL/ASSET_AUDIT.md` and memory.
- **TECH:** audited the Unity project. Built-in RP (not URP); imported Broken Vector Storage / Cars / Trees / Dungeon packs; off-theme Pistol pack flagged. Full inventory + REUSE/ADAPT/CREATE in `03_TECHNICAL/ASSET_AUDIT.md`.
- **CONTENT:** built `Map01_Grandma` greybox via editor menu (`Map01Builder`): ground floor (4 zones, narrow-door sofa puzzle) + upstairs + ramp stairs + garden (real trees) + open-bed truck; 11 movable objects (8 required); contract + HUD; debug tools (R reset, T respawn). Clean hierarchy `_Systems/Environment/Gameplay/Player/Lighting/UI`. Runs in Play with 0 errors; feel not yet playtested.
- **ART:** made 5 low-poly furniture models in Blender (sofa, bed, table, TV, fridge), exported to `Assets/_Project/Art/Furniture/SM_Furniture_*.fbx` and wired in. Boxes / dresser / suitcase reuse Broken Vector prefabs.
- **DOCS:** synced `PROJECT_STATE.md`. `CORE_LOOP.md` still a stub to fill.

## 2026-09-16

### Deep fill pass across folders (01, 03 to 13)
- **RESEARCH:** added `DEVELOPMENT_CONTEXT.md` (team size, dev time, background per game) and two games (Meccha Chameleon, Megabonk); filled the virality / social / replayability pattern files.
- **TECH:** architecture, C# conventions, data (no database), save (minimal), input, audio system, physics, event system, object interaction, performance, build, debugging, third-party. OOP + modular + config-driven + no backend by default. Gameplay/detection/AI get principles pending the concept.
- **ART:** environment, characters, props, materials, textures, lighting, VFX, animation, specs (low-poly, free-first, lighting-driven, minimal rigging).
- **AUDIO:** direction, sound design, music, voice, ambience, asset list (free / open-source SFX, minimal music).
- **PRODUCTION:** milestones, tasks, backlog, priorities, content/asset/build pipelines, readiness checklist.
- **MULTIPLAYER:** host-authoritative Steam P2P approach across sync, authority, lobby, connection, disconnect, host migration (deferred), voice, cheating (minimal), performance, testing.
- **BUSINESS:** model, pricing, budget, revenue, third-party costs (premium one-time, 50/50, EU selling worldwide).
- **STEAM:** setup, store page, capsules, screenshots, trailer, playtest, wishlists, checklist.
- **MARKETING:** competitors (from the benchmark), content / creator / social / trailer / clip / wishlist / launch strategy. Positioning, USP and audience kept light pending the concept.
- **RELEASE:** plan, build, QA, submission, launch, creator outreach, support, incident response, post-launch.
- **DESIGN:** `DESIGN_PRINCIPLES` derived from VISION. Only `02_GAME_DESIGN` detail remains, pending the concept.

## 2026-09-15

### Repository setup
- **TECH:** created the decision repository `C:\GameProject` (full structure, `CLAUDE.md`, `PROJECT_STATE.md`), pushed to private GitHub `Miche1-Pierre/game-project`.
- **RESEARCH:** benchmark "Atlas Coop Viral" transferred into `01_RESEARCH/` (game fiches, mechanics matrix, patterns, design space, white spaces, sources).
- **TECH:** Unity environment verified (CLI beta.8, editor 6.6.0f1, Personal license, URP template). Unity project NOT created, by decision (ADR-002).
- **DECISION:** ADR-001 (Unity 6), ADR-002 (spec first).

### Guided documentation pass (with Pierre)
- **DOCS:** filled `OBJECTIVES`, `VISION`, `OPEN_QUESTIONS` (21, Jonathan-tagged), `RISKS` (16, P/I/M/Trigger).
- **BUSINESS:** `TEAM_AGREEMENT` (50/50 proposed, pending) + `IP`/`PROFIT`/`LEGAL`/`TAXES` pointers; `COST_MODEL`.
- **ART:** `ART_DIRECTION` and `STYLE_GUIDE` (silhouette-first, low-poly not locked).
- **DESIGN:** neutralized `GAME_CONCEPT` (candidates unranked).

### English standardization + game analyses
- **DOCS:** whole repository standardized to English (streamer guardrail added to CLAUDE.md, timeline to ROADMAP, time budget to CONSTRAINTS).
- **RESEARCH:** the game analysis fiches completed.
- **TESTING / ANALYTICS:** filled (lean, grounded in OBJECTIVES/RISKS).
