using System.Collections.Generic;
using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // What a floor is made of, for footsteps: read from the names, because that is what the
    // house has. Map01 names its pieces well (Floor_cellar_2_1 under Level_-1_Cellar, Path,
    // Step_Porch, Rug_Living, Ground/Grass_3_2, MovingTruck/Ramp) and its materials a little
    // (PK_grass, PK_wood, MAT_Asphalt). So: the collider's own name, then its parents' (three
    // up), then its material's; the first word that means something wins, the most specific
    // first (a rug over a wooden floor is a rug). Anything unknown is wood, the most common
    // floor in the house.
    //
    // A collider is judged once and remembered: names allocate, and a player stands on the
    // same hundred colliders all game.
    public static class Surfaces
    {
        struct Rule
        {
            public string word;
            public Sfx.Surface surface;
            public Rule(string word, Sfx.Surface surface) { this.word = word; this.surface = surface; }
        }

        // Order matters inside one name: the first rule that matches decides.
        static readonly Rule[] Rules =
        {
            new Rule("rug", Sfx.Surface.Carpet), new Rule("carpet", Sfx.Surface.Carpet), new Rule("mattress", Sfx.Surface.Carpet),
            new Rule("doormat", Sfx.Surface.Carpet), new Rule("fabric", Sfx.Surface.Carpet),
            // Furniture stood on (a garden table is not a lawn).
            new Rule("table", Sfx.Surface.Wood), new Rule("bench", Sfx.Surface.Wood), new Rule("chair", Sfx.Surface.Wood),
            new Rule("crate", Sfx.Surface.Wood), new Rule("barrel", Sfx.Surface.Wood),
            new Rule("grass", Sfx.Surface.Grass), new Rule("lawn", Sfx.Surface.Grass), new Rule("garden", Sfx.Surface.Grass),
            new Rule("soil", Sfx.Surface.Grass), new Rule("dirt", Sfx.Surface.Grass), new Rule("hedge", Sfx.Surface.Grass),
            new Rule("vegetable", Sfx.Surface.Grass), new Rule("flowerbed", Sfx.Surface.Grass),
            new Rule("stone", Sfx.Surface.Stone), new Rule("cellar", Sfx.Surface.Stone), new Rule("garage", Sfx.Surface.Stone),
            new Rule("workshop", Sfx.Surface.Stone), new Rule("path", Sfx.Surface.Stone), new Rule("road", Sfx.Surface.Stone),
            new Rule("street", Sfx.Surface.Stone), new Rule("asphalt", Sfx.Surface.Stone), new Rule("kerb", Sfx.Surface.Stone),
            new Rule("curb", Sfx.Surface.Stone), new Rule("pave", Sfx.Surface.Stone), new Rule("slab", Sfx.Surface.Stone),
            new Rule("step_", Sfx.Surface.Stone), new Rule("plinth", Sfx.Surface.Stone), new Rule("concrete", Sfx.Surface.Stone),
            new Rule("brick", Sfx.Surface.Stone), new Rule("brique", Sfx.Surface.Stone), new Rule("tile", Sfx.Surface.Stone),
            new Rule("l-1", Sfx.Surface.Stone), new Rule("gravel", Sfx.Surface.Stone), new Rule("driveway", Sfx.Surface.Stone),
            new Rule("truck", Sfx.Surface.Metal), new Rule("metal", Sfx.Surface.Metal), new Rule("steel", Sfx.Surface.Metal),
            new Rule("floor", Sfx.Surface.Wood), new Rule("plank", Sfx.Surface.Wood), new Rule("wood", Sfx.Surface.Wood),
            new Rule("stair", Sfx.Surface.Wood), new Rule("deck", Sfx.Surface.Wood), new Rule("porch", Sfx.Surface.Wood),
            new Rule("terrace", Sfx.Surface.Wood), new Rule("veranda", Sfx.Surface.Wood), new Rule("board", Sfx.Surface.Wood),
            new Rule("parquet", Sfx.Surface.Wood),
            // The garden ground, last: "Level_0_Ground" is a parent of every wooden floor.
            new Rule("ground", Sfx.Surface.Grass),
        };

        static readonly Dictionary<Collider, Sfx.Surface> known = new Dictionary<Collider, Sfx.Surface>(256);
        static readonly RaycastHit[] hits = new RaycastHit[4];

        // The floor under a point (feet, or a little above them), or false over nothing.
        public static bool Under(Vector3 feet, Transform self, out Sfx.Surface surface, out Vector3 point)
        {
            surface = Sfx.Surface.Wood;
            point = feet;
            int n = Physics.RaycastNonAlloc(feet + Vector3.up * 0.4f, Vector3.down, hits, 1.2f, FloorMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider floor = null;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c is CharacterController) continue;
                if (self != null && c.transform.IsChildOf(self)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; floor = c; point = hits[i].point; }
            }
            if (floor == null) return false;
            surface = Of(floor);
            return true;
        }

        public static Sfx.Surface Of(Collider c)
        {
            if (c == null) return Sfx.Surface.Wood;
            if (known.TryGetValue(c, out Sfx.Surface s)) return s;
            s = Classify(c);
            known[c] = s;
            return s;
        }

        static Sfx.Surface Classify(Collider c)
        {
            // Something movable underfoot (a rug, a crate, a mattress): its name first (the rugs
            // have no Breakable, and a movable without one reports wood), then its material.
            var mo = c.GetComponentInParent<MovableObject>();
            if (mo != null)
            {
                if (Match(mo.name, out Sfx.Surface named) || Match(mo.displayName, out named)) return named;
                if (!mo.CanBreak) return Sfx.Surface.Wood;
                switch (mo.Material)
                {
                    case BreakMaterial.Metal: return Sfx.Surface.Metal;
                    case BreakMaterial.Fabric: return Sfx.Surface.Carpet;
                    case BreakMaterial.Stone: case BreakMaterial.Brick: case BreakMaterial.Concrete:
                    case BreakMaterial.Ceramic: case BreakMaterial.Glass: return Sfx.Surface.Stone;
                    default: return Sfx.Surface.Wood;
                }
            }
            Transform t = c.transform;
            for (int depth = 0; depth < 4 && t != null; depth++, t = t.parent)
                if (Match(t.name, out Sfx.Surface s)) return s;
            var r = c.GetComponent<Renderer>();
            if (r == null) r = c.GetComponentInParent<Renderer>();
            if (r != null && r.sharedMaterial != null && Match(r.sharedMaterial.name, out Sfx.Surface m)) return m;
            return Sfx.Surface.Wood;
        }

        static bool Match(string name, out Sfx.Surface surface)
        {
            surface = Sfx.Surface.Wood;
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < Rules.Length; i++)
            {
                if (name.IndexOf(Rules[i].word, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    surface = Rules[i].surface;
                    return true;
                }
            }
            return false;
        }

        static int floorMask;
        static int FloorMask
        {
            get
            {
                if (floorMask == 0)
                {
                    floorMask = Physics.DefaultRaycastLayers;
                    int crew = LayerMask.NameToLayer("Crew"), npc = LayerMask.NameToLayer("NPC"), debris = LayerMask.NameToLayer("Debris");
                    if (crew >= 0) floorMask &= ~(1 << crew);
                    if (npc >= 0) floorMask &= ~(1 << npc);
                    if (debris >= 0) floorMask &= ~(1 << debris);
                }
                return floorMask;
            }
        }

        public static SfxKind Step(Sfx.Surface s)
        {
            switch (s)
            {
                case Sfx.Surface.Stone: return SfxKind.StepStone;
                case Sfx.Surface.Grass: return SfxKind.StepGrass;
                case Sfx.Surface.Carpet: return SfxKind.StepCarpet;
                case Sfx.Surface.Metal: return SfxKind.StepMetal;
                default: return SfxKind.StepWood;
            }
        }

        public static SfxKind Land(Sfx.Surface s)
        {
            switch (s)
            {
                case Sfx.Surface.Stone: return SfxKind.LandStone;
                case Sfx.Surface.Grass: return SfxKind.LandGrass;
                case Sfx.Surface.Carpet: return SfxKind.LandCarpet;
                case Sfx.Surface.Metal: return SfxKind.LandMetal;
                default: return SfxKind.LandWood;
            }
        }

        // Her slippers have no metal variant: on the truck's ramp they sound like wood.
        public static SfxKind Slipper(Sfx.Surface s)
        {
            switch (s)
            {
                case Sfx.Surface.Stone: return SfxKind.SlipperStone;
                case Sfx.Surface.Grass: return SfxKind.SlipperGrass;
                case Sfx.Surface.Carpet: return SfxKind.SlipperCarpet;
                default: return SfxKind.SlipperWood;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            known.Clear();
            floorMask = 0;
        }

        // A scene change destroys the colliders; the dictionary would keep dead keys forever.
        public static void Forget() { known.Clear(); }
    }
}
