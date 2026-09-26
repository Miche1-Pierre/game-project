using System;
using UnityEngine;

namespace Movers
{
    // What every kit piece of the house is, for destruction: found by the name of its mesh,
    // like HouseDestruction always did, so Pierre's kit stays exactly as he built it.
    //
    // A row says what kind of piece it is (a wall that can be cut into chunks, a whole element
    // like a fence or a door leaf, the foundation, a roof section, a floor), what it is made of,
    // how much it takes, how far it may go down the damage ladder, whether it holds itself up,
    // and which pre-fractured chunk sets it can swap to. The rules behind the kinds are ADR-009:
    // - walls break chunk by chunk; without a chunk set they break whole, as before;
    // - the foundation (cellar walls, plinths, the ground slab) stops at Damaged;
    // - floors and stairs never break and never fall;
    // - roofs never break, they fall as whole sections when nothing holds them up.
    //
    // The code defaults hold today's health numbers and no chunk sets (code cannot reference an
    // imported FBX). The integrator creates the asset and fills the chunk sets.
    [CreateAssetMenu(fileName = "DestructibleModules", menuName = "Movers/Destructible Module Catalog")]
    public sealed class DestructibleModuleCatalog : ScriptableObject
    {
        public enum Kind : byte
        {
            Wall,           // breaks chunk by chunk when it has chunk sets, whole when it has none
            Element,        // breaks whole: fences, posts, railings, door leaves, veranda panels
            Foundation,     // cellar walls: takes damage, never past Damaged, holds everything up
            RoofSection,    // never damaged; falls as one rigid piece when unsupported
            Floor,          // never damaged, never falls; the ground slab holds, an upper slab carries
            Stairs,         // never damaged, never falls
            Footing,        // plinths and steps: never damaged, holds up what stands on it
        }

        [Serializable]
        public sealed class ChunkVariant
        {
            [Tooltip("The imported PKF_<Module>_vN FBX: a root with one child per chunk, named <Module>_Chunk_NN.")]
            public GameObject prefab;
            [Tooltip("Its sidecar JSON (anchors, mass shares, neighbours). Optional: without it the chunk graph is guessed from the boxes.")]
            public TextAsset graph;
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Kit module = mesh name. Copies ('PK_Wall_Plain.001', 'PK_Wall_Plain Instance') match too.")]
            public string module;
            public Kind kind;
            public BreakMaterial material = BreakMaterial.Plaster;
            [Tooltip("Health of the whole piece: what it takes to break it when it has no chunks.")]
            public float health;
            [Tooltip("Health of one average-sized chunk. 0 = same as the whole piece.")]
            public float chunkHealth;
            [Tooltip("How far down the damage ladder it may go. The foundation stops at Damaged.")]
            public DestructionState stateCap = DestructionState.Destroyed;
            [Tooltip("Holds itself up: never needs anything under it.")]
            public bool anchor;
            [Tooltip("Breakable even though the kit files it under Roofs, Stairs or Floors (the gables).")]
            public bool inSolidGroup;
            public ChunkVariant[] variants = new ChunkVariant[0];

            public bool IsDamageable => kind == Kind.Wall || kind == Kind.Element || kind == Kind.Foundation;
            public float ChunkHealth => chunkHealth > 0f ? chunkHealth : health;

            public bool HasChunks
            {
                get
                {
                    if (variants == null) return false;
                    for (int i = 0; i < variants.Length; i++)
                        if (variants[i] != null && variants[i].prefab != null) return true;
                    return false;
                }
            }

            // The same wall always breaks the same way (after a reset too), two walls of the
            // same module usually do not: the variant follows the position.
            public ChunkVariant PickVariant(Vector3 position)
            {
                if (!HasChunks) return null;
                int h = Mathf.RoundToInt(position.x * 7f) * 73856093 ^ Mathf.RoundToInt(position.y * 7f) * 19349663
                        ^ Mathf.RoundToInt(position.z * 7f) * 83492791;
                int n = variants.Length;
                int start = ((h % n) + n) % n;
                for (int i = 0; i < n; i++)
                {
                    var v = variants[(start + i) % n];
                    if (v != null && v.prefab != null) return v;
                }
                return null;
            }

            public Entry() { }

            public Entry(string module, Kind kind, BreakMaterial material, float health, float chunkHealth = 0f)
            {
                this.module = module;
                this.kind = kind;
                this.material = material;
                this.health = health;
                this.chunkHealth = chunkHealth;
                anchor = kind == Kind.Foundation || kind == Kind.Stairs || kind == Kind.Footing;
                stateCap = kind == Kind.Foundation ? DestructionState.Damaged : DestructionState.Destroyed;
                inSolidGroup = kind == Kind.RoofSection || kind == Kind.Floor || kind == Kind.Stairs;
            }
        }

        public Entry[] entries = Defaults();

        // First match wins, so a longer name must come before a shorter one that it starts
        // with only if IsModule would let the short one claim it (it does not: see IsModule).
        public bool TryGet(string meshName, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(meshName) || entries == null) return false;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.module)) continue;
                if (!IsModule(meshName, e.module)) continue;
                entry = e;
                return true;
            }
            return false;
        }

        // Exact name, or the name followed by what Unity or Blender append to copies
        // ("PK_Wall_Plain Instance", "PK_Wall_Plain.001"). Never a longer module name:
        // "PK_Wall_Door" must not claim a "PK_Wall_Door_Frame", and the glass split names a
        // gable window's pane "PKX_Gable_4m_Window_Glass0", which is not a gable.
        public static bool IsModule(string meshName, string module)
        {
            if (!meshName.StartsWith(module, StringComparison.Ordinal)) return false;
            if (meshName.Length == module.Length) return true;
            char next = meshName[module.Length];
            return next == ' ' || next == '.';
        }

        public static DestructibleModuleCatalog CreateDefault()
        {
            var c = CreateInstance<DestructibleModuleCatalog>();
            c.name = "DestructibleModules (code defaults)";
            c.hideFlags = HideFlags.HideAndDontSave;
            return c;
        }

        // Health numbers are HouseDestruction's old table, unchanged. chunkHealth is new, set
        // against the wall focus of the material table (0.5 m): a grenade on the floor next to
        // a wall takes out the chunks within about 0.7 m (exterior, 500) or 0.8 m (interior, 350),
        // cracks the ring around them, and a second grenade at the same spot reaches about 1 m.
        // Checked offline on the A7 prototype chunk sets (a Python port of these rules, chunk boxes
        // for hulls); first guesses, to tune in play.
        public static Entry[] Defaults()
        {
            const Kind W = Kind.Wall, E = Kind.Element;
            const BreakMaterial Plaster = BreakMaterial.Plaster, Stone = BreakMaterial.Stone, Wood = BreakMaterial.Wood;
            var gable = new Entry("PKX_Gable_4m", W, Wood, 900f, 400f) { inSolidGroup = true };
            var gableWindow = new Entry("PKX_Gable_4m_Window", W, Wood, 900f, 400f) { inSolidGroup = true };
            return new[]
            {
                // Exterior walls.
                new Entry("PK_Wall_Plain", W, Plaster, 1400f, 500f),
                new Entry("PK_Wall_Window_Small", W, Plaster, 1400f, 500f),
                new Entry("PK_Wall_Window_Big", W, Plaster, 1400f, 500f),
                new Entry("PK_Wall_Door", W, Plaster, 1400f, 500f),
                new Entry("PKX_Wall_Garage", W, Plaster, 1400f, 500f),
                // Interior walls.
                new Entry("PK_Wall_Interior", W, Plaster, 650f, 350f),
                new Entry("PKX_Wall_Int_Door", W, Plaster, 650f, 350f),
                new Entry("PKX_Wall_Int_Arch", W, Plaster, 650f, 350f),
                // The foundation: Damaged at most, and it holds the house up.
                new Entry("PKX_Wall_Cellar", Kind.Foundation, Stone, 2600f),
                // Gables sit under Roofs in the kit, next to the roof pieces they hold up.
                gable,
                gableWindow,
                new Entry("PKX_Corner_Quoin", W, Stone, 1200f, 500f),
                new Entry("PK_Wall_Corner", W, Stone, 1200f, 500f),
                new Entry("PKX_Chimney_Stack", W, Stone, 1500f, 600f),
                new Entry("PKX_Chimney_Cap", E, Stone, 1500f),
                new Entry("PKX_Fireplace", E, Stone, 900f),
                new Entry("PKX_Fence_2m", E, Wood, 140f),
                new Entry("PKX_Fence_Post", E, Wood, 120f),
                new Entry("PKX_Fence_Gate", E, Wood, 140f),
                new Entry("PKX_Railing_2m", E, Wood, 170f),
                new Entry("PKX_Railing_Post", E, Wood, 150f),
                new Entry("PK_Post_T", E, Wood, 320f),
                new Entry("PK_Pillar_Brick", E, Stone, 900f),
                new Entry("PK_Veranda_Panel_Wide", E, Wood, 260f),
                new Entry("PK_Veranda_Panel_Narrow", E, Wood, 260f),
                new Entry("PK_Veranda_Glass_Wall", E, Wood, 260f),
                new Entry("PKX_Veranda_Cheek", E, Wood, 260f),
                new Entry("PKX_Veranda_Lintel", E, Wood, 260f),
                new Entry("PK_Door_Leaf", E, Wood, 220f),
                new Entry("PKX_Door_Leaf_Int", E, Wood, 220f),
                new Entry("PKX_Veranda_Door_Leaf", E, Wood, 220f),
                new Entry("PKX_Garage_Door_Leaf", E, Wood, 400f),
                // Never damaged: they only hold things up, or fall.
                new Entry("PK_Roof_8", Kind.RoofSection, Wood, 0f),
                new Entry("PK_Roof_11", Kind.RoofSection, Wood, 0f),
                new Entry("PK_Roof_12", Kind.RoofSection, Wood, 0f),
                new Entry("PK_Veranda_Roof", Kind.RoofSection, BreakMaterial.Glass, 0f),
                new Entry("PK_Veranda_Glass_Flat", Kind.RoofSection, BreakMaterial.Glass, 0f),
                new Entry("PK_Floor_Upper_B", Kind.Floor, Wood, 0f),
                new Entry("PK_Floor_Stone", Kind.Floor, Stone, 0f),
                new Entry("PK_Stairs_Wood", Kind.Stairs, Wood, 0f),
                new Entry("PK_Stairs_Stone", Kind.Stairs, Stone, 0f),
                new Entry("PKX_Plinth_Stone", Kind.Footing, Stone, 0f),
                new Entry("PKX_Step_Stone", Kind.Footing, Stone, 0f),
            };
        }
    }
}
