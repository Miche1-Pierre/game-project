using System;
using UnityEngine;

namespace Movers
{
    // Every number that decides how a material breaks, in one asset, so a playtest note like
    // "the vases are too fragile" or "glass stops a blast" maps to one field (ADR-009: tuning
    // lives in data). The code defaults below are the numbers the game had before the table
    // existed, bit for bit (Breakable's old switch tables, Explosion's old constants), so a
    // scene without the asset plays exactly as before. Brick and Concrete are new rows: nothing
    // in the house uses them yet, their numbers are first guesses.
    //
    // One asset per project, assigned on HouseDestruction. With none assigned the defaults are
    // used, including in Tutorial_01, which has no HouseDestruction at all.
    [CreateAssetMenu(fileName = "DestructionMaterials", menuName = "Movers/Destruction Material Table")]
    public sealed class DestructionMaterialTable : ScriptableObject
    {
        [Serializable]
        public struct Row
        {
            public BreakMaterial material;
            [Tooltip("Health before the mass scaling movables get.")]
            public float durability;
            [Tooltip("m/s. A hit slower than this along the contact normal does nothing.")]
            public float minImpactSpeed;
            [Tooltip("kg per cubic metre: debris and chunk masses.")]
            public float density;
            [Tooltip("Multiplies blast damage before it lands. 1 = the blast as it comes.")]
            public float blastFactor;
            [Tooltip("Multiplies impact damage (throws, falls, crushes).")]
            public float impactFactor;
            [Tooltip("Share of a blast that still gets through this material when it stands intact between the blast and a target. 1 = no cover.")]
            [Range(0f, 1f)] public float cover;
            public ImpactAudio.Kind sound;

            public Row(BreakMaterial m, float durability, float minSpeed, float density, float cover, ImpactAudio.Kind sound)
            {
                material = m;
                this.durability = durability;
                minImpactSpeed = minSpeed;
                this.density = density;
                blastFactor = 1f;
                impactFactor = 1f;
                this.cover = cover;
                this.sound = sound;
            }
        }

        [Tooltip("One row per material. A material missing here uses its code default.")]
        public Row[] rows = DefaultRows();

        [Header("Blast")]
        [Tooltip("Damage at the centre of a power-1 blast, before falloff, cover and material.")]
        public float blastDamage = 1600f;
        [Tooltip("A window pane takes this share of a blast (500 / 1600: the old glass number).")]
        public float paneBlastFactor = 0.3125f;
        [Tooltip("Share that gets through anything that is not breakable: floors, fixtures, roofs, the grandmother.")]
        [Range(0f, 1f)] public float solidCover = 0.35f;
        [Tooltip("How far a Damaged occluder has opened towards no cover at all.")]
        [Range(0f, 1f)] public float damagedCoverOpening = 0.15f;
        [Tooltip("How far a Fractured occluder has opened towards no cover at all.")]
        [Range(0f, 1f)] public float fracturedCoverOpening = 0.4f;
        [Tooltip("m. A wall takes a blast where it lands: on a wall, damage is also multiplied by 1 / (1 + (d / focus)^2). " +
                 "Without it the falloff is so flat near the centre that one grenade strips a whole wall.")]
        public float structureFocus = 0.5f;

        [Header("Impacts")]
        public float impactDamageScale = 9f;
        [Tooltip("Debris lighter than this never hurts a prop or a wall: a shower of shards is not a crash. Heavier debris crushes.")]
        public float crushMinMass = 30f;

        [Header("Wall chunks")]
        [Tooltip("A chunk at or below this share of its health is Damaged (tinted).")]
        [Range(0f, 1f)] public float chunkDamagedAt = 0.6f;
        [Tooltip("At or below this share it is Fractured: still in place, holds what is above it, no longer holds anything beside it.")]
        [Range(0f, 1f)] public float chunkFracturedAt = 0.25f;
        [Tooltip("A wall with less than this share of its chunks left collapses whole.")]
        [Range(0f, 1f)] public float collapseBelowShare = 0.3f;
        [Tooltip("A wall swaps to its chunks on the first hit that takes at least this share of one chunk's health (0.4: " +
                 "enough to show as Damaged). Smaller hits are remembered on the intact wall.")]
        [Range(0f, 1f)] public float fractureAtShare = 0.4f;

        [Header("Debris lifetimes (s)")]
        public float structureDebrisLifetime = 25f;
        public float propDebrisLifetime = 18f;
        public float glassShardLifetime = 8f;

        // ---- the one table in use ----

        static DestructionMaterialTable active;
        static DestructionMaterialTable defaults;
        static readonly Row[] byMaterial = new Row[16];
        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active = null;
            built = false;
        }

        // Never null. The defaults instance is made once, hidden, and reused across play
        // sessions (it is plain data).
        public static DestructionMaterialTable Current
        {
            get
            {
                if (active != null) return active;
                if (defaults == null)
                {
                    defaults = CreateInstance<DestructionMaterialTable>();
                    defaults.name = "DestructionMaterials (code defaults)";
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                    built = false;
                }
                return defaults;
            }
        }

        public static void Use(DestructionMaterialTable table)
        {
            active = table;
            built = false;
        }

        public static Row Get(BreakMaterial m)
        {
            if (!built) Build();
            int i = (int)m;
            return i >= 0 && i < byMaterial.Length ? byMaterial[i] : DefaultRow(m);
        }

        // What a hit of this type is multiplied by before it lands on this material. A pane is
        // glass, but a window takes a blast differently from a wine glass: see paneBlastFactor.
        public static float Factor(DamageType type, BreakMaterial m)
        {
            Row r = Get(m);
            switch (type)
            {
                case DamageType.Blast: return r.blastFactor;
                case DamageType.Tool: return 1f;
                default: return r.impactFactor;
            }
        }

        // What a blast does to a wall at d metres from its centre, on top of the falloff.
        public static float Focus(float d)
        {
            float f = Mathf.Max(0.01f, Current.structureFocus);
            float x = d / f;
            return 1f / (1f + x * x);
        }

        // Share of a blast that gets through an occluder of this material in this state.
        public static float CoverOf(BreakMaterial m, DestructionState s)
        {
            var t = Current;
            float c = Get(m).cover;
            if (s == DestructionState.Damaged) c = Mathf.Lerp(c, 1f, t.damagedCoverOpening);
            else if (s == DestructionState.Fractured) c = Mathf.Lerp(c, 1f, t.fracturedCoverOpening);
            else if (s == DestructionState.Destroyed) c = 1f;
            return Mathf.Clamp01(c);
        }

        void OnValidate() { built = false; }

        static void Build()
        {
            for (int i = 0; i < byMaterial.Length; i++) byMaterial[i] = DefaultRow((BreakMaterial)i);
            var t = Current;
            if (t.rows != null)
                for (int i = 0; i < t.rows.Length; i++)
                {
                    int m = (int)t.rows[i].material;
                    if (m >= 0 && m < byMaterial.Length) byMaterial[m] = t.rows[i];
                }
            built = true;
        }

        // ---- code defaults: the pre-table numbers ----

        public static Row[] DefaultRows()
        {
            var values = (BreakMaterial[])Enum.GetValues(typeof(BreakMaterial));
            var rows = new Row[values.Length];
            for (int i = 0; i < values.Length; i++) rows[i] = DefaultRow(values[i]);
            return rows;
        }

        // Durability, minimum speed, density and sound are Breakable's old tables. Cover is new
        // (the old blast had one 0.35 for everything standing): stone-like materials keep 0.35,
        // lighter ones let more through. Unknown values fall back like the old switch did.
        public static Row DefaultRow(BreakMaterial m)
        {
            switch (m)
            {
                case BreakMaterial.Glass: return new Row(m, 25f, 2.2f, 2500f, 0.9f, ImpactAudio.Kind.Glass);
                case BreakMaterial.Ceramic: return new Row(m, 35f, 2.8f, 2000f, 0.8f, ImpactAudio.Kind.Glass);
                case BreakMaterial.Plastic: return new Row(m, 80f, 4f, 900f, 0.8f, ImpactAudio.Kind.Thud);
                case BreakMaterial.Wood: return new Row(m, 180f, 4.5f, 600f, 0.5f, ImpactAudio.Kind.Wood);
                case BreakMaterial.Fabric: return new Row(m, 260f, 7f, 300f, 0.6f, ImpactAudio.Kind.Thud);
                case BreakMaterial.Metal: return new Row(m, 420f, 7f, 3000f, 0.35f, ImpactAudio.Kind.Crunch);
                case BreakMaterial.Stone: return new Row(m, 2200f, 9f, 2200f, 0.35f, ImpactAudio.Kind.Crunch);
                case BreakMaterial.Plaster: return new Row(m, 1200f, 8f, 1400f, 0.35f, ImpactAudio.Kind.Crunch);
                // New rows, first guesses: brick a little tougher than plaster, concrete tougher
                // than stone.
                case BreakMaterial.Brick: return new Row(m, 1800f, 9f, 1900f, 0.3f, ImpactAudio.Kind.Crunch);
                case BreakMaterial.Concrete: return new Row(m, 3000f, 10f, 2400f, 0.25f, ImpactAudio.Kind.Crunch);
                default: return new Row(m, 180f, 4.5f, 1000f, 0.35f, ImpactAudio.Kind.Crunch);
            }
        }
    }
}
