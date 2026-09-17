# ASSET LIST (Grandma House, complete production list)

_The exhaustive asset inventory for The Movers Map 1 (grandma house + garden), built from the reference atlas and the category breakdown. This is the "what to make" list. Specs (scale, poly, pipeline) live in `ASSET_SPECS.md`; visual direction in `ART_DIRECTION.md` / `STYLE_GUIDE.md`. Per-asset progress is tracked in `ASSET_STATUS.md`. Last update: 2026-09-17._

> Scope decision (Pierre): freeze the full list first. Production is a separate, later step (AI + Blender + human validation), not "generate 300 finished models at once". Start with a ~30 asset vertical slice, validate coherence, then industrialize.

## 1. Conventions (so 300 assets stay coherent)

- **No textures**: detail comes from geometry + flat per-material colors. See the palette in section 13.
- **Scale**: 1 unit = 1 m. Real-world sizes (a chair ~0.9 m tall, a door ~2.1 m).
- **Orientation**: front faces -Z in Unity (walls/furniture face the viewer at yaw 0).
- **Origin / pivot**: base-center for placeable objects (sits on the floor at its position); door/window units centered on the opening; wall modules base-center.
- **Naming**: `SM_<Name>.fbx` (Blender mesh) -> `PF_<Name>.prefab` (Unity, flat color + collider). PascalCase, numeric variants as `_01`, `_02`.
- **Families**: same style, proportions and poly density across a family (`Chair_01`..`Chair_04`, `Flower_01`..`Flower_06`). Variation is geometry, not scale.
- **Poly budget** (guideline): small props 0.5k-5k tris; furniture 5k-15k; large architecture 10k-30k.
- **Materials**: pull from the shared list (section 13), few slots per asset.
- **Modules**: wall grid = 4 m wide x 3 m high (sub-modules 1 m / 2 m / 3 m for flexibility). Roofs share one pitch (pick one, see section 12).
- **Status stages** (`ASSET_STATUS.md`): Concept -> Greybox -> Placeholder -> Generated -> Cleaned -> Integrated -> Final.

## 2. Folder structure

```
Assets/_Project/Art/            (SM_*.fbx)      + mirror in /Prefabs (PF_*.prefab)
├── Architecture/  Walls, Floors, Roofs, Doors, Windows, Stairs, Exterior
├── Furniture/     LivingRoom, Bedroom, Kitchen, Bathroom, Office, Basement, Outdoor
├── Props/         Lighting, Decoration, Books, Luggage, Kitchenware, Tools, Sewing, Special, Lab, Misc
├── Food/
├── Nature/        Flowers, Mushrooms, Plants, Rocks, Ground
├── Animals/       Cat
└── Materials/     shared flat-color materials
```

Legend below: **[DONE]** = already produced in the Cottage Kit (27 pieces, `Assets/_Project/Art/CottageKit`). **[VAR]** = make as a family of variants. Counts are targets.

---

## 3. ARCHITECTURE

### Walls (~18)
- Wall_1m, Wall_2m, Wall_3m, Wall_4m **[DONE: 4m Solid]**
- Wall_Corner_Outer, Wall_Corner_Inner
- Wall_Half (knee wall / low wall)
- Wall_Window **[DONE]**, Wall_Window_Double, Wall_Window_Bay
- Wall_Door **[DONE]**, Wall_DoubleDoor, Wall_Garage **[DONE]**
- Wall_Gable_Bracket (Craftsman) **[DONE: Roof_GableEnd]**, Wall_Gable_Plain **[DONE: Gable_Infill]**
- Partition_Solid, Partition_Door, Partition_Opening (interior thin walls)

### Structure (~6)
- Beam_Ceiling, Beam_Post, Truss, Column_Structural
- Foundation_Brick **[DONE]**, Foundation_Stone

### Floors & Ceilings (~9)
- Floor_1x1, Floor_2x2, Floor_4x4 **[DONE]** [VAR: wood plank / stone / tile]
- Ceiling_Panel, Ceiling_Beam
- Stair_Landing_Floor

### Roof (~14)
- Roof_Slope **[DONE]**, Roof_Slope_Half, Roof_Ridge **[DONE]**, Roof_Hip, Roof_Valley, Roof_End
- Roof_Corner, Roof_Dormer **[DONE: Dormer]**, Roof_Flat
- Chimney **[DONE]**, Chimney_Cap
- Gutter, Downspout, Eave_Bracket

### Stairs & access (~8)
- Stairs_Straight **[DONE: Stairs_Interior]**, Stairs_Winder, Stairs_Spiral, Stairs_Landing
- Railing_Interior, Baluster, Newel_Post
- Ladder_Attic

### Openings (~14)
- Window_Single, Window_Double, Window_Bay **[DONE: Bay_Window]**, Window_Dormer, Window_Round, Window_Attic
- Shutter_Pair
- Door_Front **[DONE]**, Door_Interior, Door_Double, Door_Garage **[DONE]**, Door_Cellar_Hatch
- Frame_Casing, Sill

### Porch & exterior structure (~12)
- Porch_Column **[DONE]**, Porch_Beam **[DONE]**, Porch_Railing **[DONE]**, Porch_Deck **[DONE: Deck_Tile]**, Porch_Roof, Steps_Stoop **[DONE]**
- Veranda/Greenhouse: Greenhouse_Wall, Greenhouse_Roof, Greenhouse_Door (the verriere)
- Balcony, Awning_Canopy, Cellar_Access_Exterior

---

## 4. FURNITURE

### Bedroom / Chambre (~16)
- Bed_Single, Bed_Double, Bed_Old (grandma), Headboard
- Bedspread, Pillow, Blanket
- Nightstand_01-02 [VAR], Dresser_01-02 [VAR], Wardrobe_01-02 [VAR]
- Vanity (coiffeuse), Mirror_Standing, Mirror_Wall
- Armchair_Bedroom, Stool, Coat_Rack, Chest

### Living room / Salon (~18)
- Sofa_01-02 [VAR], Armchair_01-02 [VAR], Rocking_Chair, Ottoman
- Coffee_Table, Side_Table, TV_Stand
- Bookshelf_01-02 [VAR], Display_Cabinet (vaisselier)
- TV_Old, Radio_Old, Record_Player, Clock_Wall, Clock_Grandfather (comtoise)
- Fireplace_Living, Wood_Stove (poele)
- Piano_Upright, Piano_Stool

### Kitchen & dining / Cuisine (~18)
- Counter_Straight, Counter_Corner, Cabinet_Upper, Cabinet_Lower (modular)
- Sink, Stove_Old, Oven, Fridge_Old, Range_Hood
- Kitchen_Island
- Dining_Table_01-02 [VAR], Dining_Chair_01-04 [VAR], Dining_Bench
- Shelf_Kitchen, Spice_Rack, Pot_Rack_Hanging

### Bathroom / Salle de bain (~10)
- Bathtub_Old, Sink_Bath, Vanity_Sink, Mirror_Bath
- Toilet, Shower
- Laundry_Basket, Bath_Shelf, Bath_Rug, Towel_Rail

### Office / Workshop / Bureau (~8)
- Desk, Secretary_Desk, Office_Chair
- Shelf_Files, Drawing_Board, Typewriter, Pegboard, Workbench_Office

### Basement / Cave (~10)
- Shelving_Unit_Cave, Bottle_Rack, Preserve_Jars_Shelf
- Workbench, Boiler, Pipes_Set, Electric_Meter
- Old_Bicycle, Furniture_Covered (dust sheet), Coal_Bin

### Outdoor furniture / Mobilier exterieur (~6)
- Garden_Bench, Garden_Table, Garden_Chair, Hammock, Parasol, Sun_Lounger

---

## 5. PROPS

### Lighting / Luminaires (~9)
- Ceiling_Lamp, Chandelier, Wall_Sconce, Floor_Lamp, Table_Lamp, Lantern, Candle, Candlestick, Oil_Lamp

### Decoration & objects / Decoration (~14)
- Picture_Frame_01-03 [VAR], Family_Photo, Painting, Mirror_Decor
- Vase_01-03 [VAR], Potted_Plant_Indoor_01-03 [VAR]
- Clock_Small, Figurine_01-03 [VAR], Doily, Crucifix (per lore), Music_Box, Jewelry_Box
- Textiles: Rug_01-03 [VAR], Cushion_01-02 [VAR], Curtains, Blanket_Throw, Wall_Tapestry

### Books & papers / Livres (~8)
- Book_Single_01-03 [VAR], Book_Stack, Books_Row (shelf filler), Open_Book
- Newspaper, Magazine, Letter, Map, Diploma_Framed

### Luggage & chests / Bagages (~7)
- Suitcase_01-02 [VAR], Trunk, Chest, Cardboard_Box, Crate, Basket

### Kitchenware (~12)
- Plate, Bowl, Cup, Pot, Pan, Kettle, Coffee_Pot, Toaster, Jar_01-02 [VAR], Bottle_01-02 [VAR], Cutting_Board, Utensils_Set, Bread_Basket, Fruit_Bowl

### Tools / Outils (~12)
- Hammer, Saw, Axe, Shovel, Pickaxe, Wrench, Screwdriver
- Broom, Bucket, Rope, Watering_Can, Rake

### Sewing & knitting (grandma) (~7)
- Sewing_Machine, Yarn_Ball [VAR], Knitting_Needles, Knitting_Basket, Fabric_Roll, Embroidery_Hoop, Pin_Cushion

### Special elements / Elements speciaux (~6)
- Telescope, Globe, Dress_Form (mannequin), Bird_Cage, Model_Ship, Spinning_Wheel

### Laboratory / scientific (if needed) (~7)
- Lab_Table, Microscope, Flask_Set, Chem_Bottles, Science_Poster, Bookshelf_Lab, Monitor_Old

### Misc small / Petits objets (~10)
- Keys, Glasses, Perfume, Medicine_Box, Matches, Umbrella, Ball, Clock_Alarm, Ashtray, Photo_Album

---

## 6. FOOD / Nourriture (~14)
- Bread, Baguette, Croissant, Cake
- Apple, Pear, Grapes, Carrot, Tomato
- Fish, Meat, Cheese, Egg, Milk_Bottle, Wine_Bottle, Jam_Jar, Canned_Food

---

## 7. NATURE (families, no full forest needed) (~24)
- Flower_01-06 [VAR], Sunflower, Herb_Tuft_01-02 [VAR]
- Mushroom_01-04 [VAR]
- Grass_Tuft_01-03 [VAR], Fern, Ivy_Climbing, Shrub_01-03 [VAR] **[DONE: Hedge, Bush]**
- Rock_01-04 [VAR], Ground_Tile [VAR: grass/dirt/gravel], Path_Stone **[DONE]**, Paving_Tile

_Trees reuse the existing Broken Vector Tree Pack; make custom only if the style clashes._

## 8. GARDEN elements / Jardin (~14)
- Fence_Section **[DONE]**, Fence_Post **[DONE]**, Gate **[DONE]**, Fence_Stone, Garden_Arch (roses), Trellis
- Well, Fountain, Small_Bridge, Planter_Box, Vegetable_Bed, Wheelbarrow
- Bird_Feeder, Birdhouse, Scarecrow, Mailbox, Woodpile, Lamp_Post, Compost_Bin

## 9. ANIMALS / Chat (~14)
- Cat poses: Cat_Stand, Cat_Sit, Cat_Lie, Cat_Walk, Cat_Sleep, Cat_Look, Cat_Eat, Cat_Groom, Cat_Play, Cat_On_Furniture
- Morphology variants: Cat_A, Cat_B (color/pattern) [VAR]
- Accessories: Cat_Bed, Cat_Bowl, Cat_Toy, Scratching_Post
- _Animation is a separate pipeline; model static poses first._

## 10. MATERIALS (shared, flat colors, no texture) (~13)
- Wood_Light, Wood_Dark, Stone, Brick, Metal, Ceramic, Fabric, Glass, Wall_Cream, Trim_White, Roof, Plaster, Foliage, Water

---

## 11. Already produced

### Furniture batch 1 (14, `Assets/_Project/Art/GrandmaKit/Furniture` + `/Prefabs/GrandmaKit/Furniture`)
Bed_Old, Nightstand_01, Dresser_01, Wardrobe_01 (bedroom); Sofa_01, Armchair_01, Rocking_Chair, Coffee_Table, Side_Table, Bookshelf_01 (with books), TV_Old, Fireplace_Living (living); Dining_Table_01, Dining_Chair_01 (dining). Flat-colored prefabs + colliders. Rebuild via menu **The Movers > Build Kit Prefabs** (scans CottageKit + GrandmaKit).

### Interior batch 2 (12: Kitchen 7, Bathroom 4, Office 1)
Kitchen: Counter_Straight, Cabinet_Upper, Sink, Stove_Old, Fridge_Old (retro), Range_Hood, Kitchen_Island. Bathroom: Bathtub_Old (clawfoot), Toilet, Sink_Bath (pedestal), Shower. Office: Desk. In `Assets/_Project/Art/GrandmaKit/{Kitchen,Bathroom,Office}` + mirrored prefabs.

### Batch 3 (16: props + basement + garden + nature)
Lighting: Table_Lamp, Floor_Lamp, Chandelier. Decoration: Vase_Plant, Picture_Frame_01, Wall_Clock, Rug_01. Basement: Shelving_Unit_Cave, Barrel, Crate. Garden: Garden_Bench, Wheelbarrow, Mailbox. Nature: Flower_01, Mushroom_01, Rock_01. In `Assets/_Project/Art/GrandmaKit/{Props/*,Garden,Nature}` + mirrored prefabs.

### Batch 4 (18: kitchenware + food)
Kitchenware: Plate, Bowl, Cup, Pot_Cook, Pan, Kettle, Coffee_Pot, Cutting_Board, Jar_01, Bottle_01, Bread_Basket, Fruit_Bowl, Utensils_Jar. Food: Bread, Apple, Cheese_Wedge, Cake, Wine_Bottle. In `.../Props/Kitchenware` and `.../Food`.

### Batch 5 (14: tools + books + luggage + sewing + special)
Tools: Hammer, Saw, Axe, Shovel, Broom, Bucket. Books: Book_Stack, Newspaper. Luggage: Suitcase, Trunk. Sewing: Sewing_Machine, Yarn_Ball, Knitting_Basket. Special: Globe. In `.../Props/{Tools,Books,Luggage,Sewing,Special}`.

### Batch 6 (16: furniture variants + garden + nature)
Furniture: Sofa_02, Armchair_02 (wingback), Display_Cabinet, Coat_Rack, Chest, Wood_Stove, Vanity. Garden: Well, Planter_Box, Birdhouse, Garden_Arch. Nature: Flower_02, Mushroom_02, Rock_02, Bush_01, Grass_Tuft.

### Batch 7 (16: textiles + lighting/deco + special + misc)
Textiles: Curtains, Cushion_01, Cushion_02, Rug_02. Lighting: Wall_Sconce, Candlestick, Oil_Lamp. Decoration: Painting, Mirror_Wall, Vase_02. Special: Telescope, Bird_Cage, Dress_Form, Model_Ship. Misc: Crucifix, Alarm_Clock. (Wall-mounted pieces (Painting, Mirror_Wall, Wall_Sconce) use a centered pivot for free wall placement.)

### Batch 8 (18: archi variants + garden + lab + big furniture + misc)
Architecture: Wall_2m, Wall_Corner, Window_Round, Door_Interior, Stairs_Spiral. Garden: Fence_Stone, Trellis, Fountain, Scarecrow. Lab: Lab_Table, Microscope, Flask_Set. Furniture: Piano, Secretary_Desk. Misc: Keys, Glasses, Perfume, Medicine_Box.

### Structure lot (12: fills the house-structure gaps)
Interior: Wall_Interior_Solid, Wall_Interior_Door, Wall_Interior_Opening (cloisons). Walls: Wall_1m, Wall_3m, Wall_Corner_Inner. Terrace_Deck (large wooden terrace). Veranda (in `GrandmaKit/Veranda`): Veranda_Panel, Veranda_Corner, Veranda_Roof, Veranda_Door, Veranda_Bay (faceted rounded conservatory). Now the house can be fully built AND partitioned into rooms, with a terrace and a straight or rounded veranda.

**Running total: 163 kit pieces**, all FBX + flat-color prefabs with colliders. Rebuild all via **The Movers > Build Kit Prefabs**.

### Consolidated source (review + edit)
All 163 pieces live upright, correct-scale, colored, in **`C:\GameProject\_ArtSource\GrandmaKit_All.blend`** (22 collections, one per category). Pierre reviews/edits there. To re-export an edited piece: select it, Alt+G (origin), File > Export > FBX into the matching `Art/...` folder with the settings in the FBX-pipeline memory, then run Build Kit Prefabs.

### Cottage Kit, 27 pieces (architecture)

`Assets/_Project/Art/CottageKit/SM_*.fbx` + `Assets/_Project/Prefabs/CottageKit/PF_*.prefab` (flat color + collider). Covers part of ARCHITECTURE + a few Nature/Garden: Wall_Solid, Wall_Window, Wall_Door, Wall_Garage, Gable_Infill, Roof_GableEnd, Roof_Slope, Roof_Ridge, Dormer, Chimney, Bay_Window, Porch_Column, Porch_Beam, Porch_Railing, Deck_Tile, Steps_Stoop, Stairs_Interior, Door_Front, Door_Garage, Foundation_Brick, Floor_4x4, Path_Stone, Fence_Section, Fence_Post, Gate, Hedge, Bush.

## 12. Open decisions before production
- **Roof pitch**: pick one angle for all roof pieces so they snap (current kit mixes 36.9 deg gable end and 38.7 deg slope). Recommend one shared value.
- **Wall sub-modules**: confirm 1 m / 2 m / 3 m in addition to 4 m, or 4 m only.
- **Interior scope**: full furnished interior (all rooms) or facade + a few key rooms first.
- **Cat**: how many poses / morphologies are actually needed for the loop.

## 13. Rough totals
Architecture ~81, Furniture ~86, Props ~92, Food ~14, Nature ~24, Garden ~18, Animals ~14, Materials ~13. **Target ~340 assets** (families counted per variant). Already done: 27.

## 14. Suggested vertical slice (first ~30, to validate coherence)
5 Architecture (Wall_2m, Wall_Corner, Window_Single, Door_Interior, Roof_Hip) + 5 Furniture (Bed_Old, Wardrobe_01, Sofa_01, Dining_Table_01, Dining_Chair_01) + 5 Kitchen (Counter_Straight, Stove_Old, Sink, Fridge_Old, Cabinet_Upper) + 5 Decoration (Table_Lamp, Picture_Frame_01, Vase_01, Rug, Clock_Wall) + 5 Basement/Tools (Shelving_Unit_Cave, Barrel, Crate, Hammer, Workbench) + 5 Garden/Cat (Garden_Bench, Flower_01, Rock_01, Cat_Sit, Cat_Bowl). Validate style, then industrialize.
