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
    // DEV 2 (ADR-013, 03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md) adds the impact energy model, the blast
    // curve, the wall chunk launch and the debris budgets. Their code defaults are the tuning
    // chosen there, not the old numbers: a missing asset plays like the tuned game.
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
        [Tooltip("m. Fallback of structureFalloff when the curve has fewer than 2 keys: on a wall, damage is also multiplied by 1 / (1 + (d / focus)^2). " +
                 "Without a focus the falloff is so flat near the centre that one grenade strips a whole wall.")]
        public float structureFocus = 0.5f;
        [Tooltip("Share of a blast a wall or a structural piece takes against its distance (m) to the centre, on top of the falloff.")]
        public AnimationCurve structureFalloff = DefaultStructureFalloff();
        [Tooltip("The curve's distance is divided by the cube root of the blast's power: a stronger explosive opens a bigger hole.")]
        public bool focusScalesWithPower = true;
        [Tooltip("Blast damage falls as (1 - d / r) to this power.")]
        public float blastExponent = 1.3f;
        [Tooltip("N.s of push per point of blast damage on the debris of what it breaks.")]
        public float blastDamageImpulse = 40f;
        [Tooltip("N.s at the centre of a power-1 blast on loose bodies, linear falloff.")]
        public float pushImpulse = 900f;
        [Tooltip("m the push centre is lowered, so things lift as they fly.")]
        public float pushUplift = 0.8f;
        [Tooltip("m/s. Faster than this a light object tunnels through a wall.")]
        public float maxLaunchSpeed = 28f;
        [Tooltip("The launch cap falls with distance as f to this power: light shards slow down away from the centre.")]
        public float lightDebrisLaunchFalloff = 0.5f;
        [Tooltip("rad/s of tumble given to launched bodies, at most.")]
        public float maxSpin = 7f;
        [Tooltip("Players feel a blast this many radii out.")]
        public float playerReach = 1.2f;
        [Tooltip("m/s of horizontal knock on a player at point blank.")]
        public float knockHorizontal = 10f;
        [Tooltip("m/s of upward knock on a player at point blank.")]
        public float knockUp = 4.5f;
        [Tooltip("m/s of shove before it counts as being knocked down.")]
        public float knockedDownSpeed = 2f;
        [Tooltip("Drunkenness at point blank.")]
        public float concussion = 0.45f;
        [Tooltip("Cameras shake out to this many radii.")]
        public float shakeReach = 3f;
        public float shakeMaxStrength = 2f;
        [Tooltip("m. The radius of a grenade, read when it is created.")]
        public float grenadeRadius = 6.5f;
        public float grenadePower = 1f;
        [Tooltip("m/s a wall chunk flies out at point blank (times the curve and the cover).")]
        public float blastChunkEjectSpeed = 10f;
        [Tooltip("m. Chunks this close to the blast centre come out as rubble.")]
        public float breachRadius = 0.8f;

        [Header("Impacts")]
        [Tooltip("Before DEV 2: damage = scale * ratio * extra speed^2. Kept for old assets, replaced by energyDamageScale.")]
        public float impactDamageScale = 9f;
        [Tooltip("Debris lighter than this never hurts a prop or a wall: a shower of shards is not a crash. Heavier debris crushes.")]
        public float crushMinMass = 30f;
        [Tooltip("HP per (m/s)^2 of effective speed over the gate (ImpactDamage.Evaluate).")]
        public float energyDamageScale = 27f;
        [Tooltip("kg. The resisting mass of a built piece (wall, chunk, fence, post, yard prop). Panes keep their own mass.")]
        public float structureReferenceMass = 35f;
        [Tooltip("The striker's reduced mass counts at least this share of the receiver's.")]
        public float minMassRatio = 0.1f;
        [Tooltip("The striker's reduced mass counts at most this many times the receiver's (not vehicles).")]
        public float maxMassRatio = 12f;
        [Tooltip("The same cap for a truck or a police car: their real mass counts.")]
        public float vehicleMaxMassRatio = 200f;
        [Tooltip("N.s of push per (m/s times kg) of an impact on its debris.")]
        public float impactPushFactor = 0.5f;
        [Tooltip("m of spread per cube root of the energy in kJ: a big hit reaches the chunks around it.")]
        public float impactSpreadPerCubeRootKJ = 0.3f;
        [Tooltip("m. The spread never exceeds this.")]
        public float impactMaxSpread = 2.5f;
        [Tooltip("m/s added to the gate for an object in someone's hands: the arms give.")]
        public float heldSpeedAllowance = 1.5f;
        public float heldMaxMassRatio = 1f;
        [Tooltip("m/s added to the gate for cargo hitting its own truck, so one ram does not wipe the load.")]
        public float cargoSpeedAllowance = 3f;
        public float cargoMaxMassRatio = 1f;
        [Tooltip("A pane counts a striker as at least this share of its mass: a thrown cup still breaks a window.")]
        public float glassMinMassRatio = 0.5f;
        [Tooltip("kg. Debris lighter than this never breaks a pane.")]
        public float glassDebrisMinMass = 2f;
        [Tooltip("Damage multiplier per kind of hit.")]
        public float impactMultiplier = 1f, thrownMultiplier = 1f, fallMultiplier = 1f, crushMultiplier = 0.5f,
                     toolMultiplier = 1f;
        [Tooltip("Vehicles: the bumper absorbs most of the energy.")]
        public float vehicleMultiplier = 0.01f;
        [Tooltip("A launched wall chunk strikes other built pieces this much softer: no domino through the house.")]
        public float structureChunkStrikeFactor = 0.1f;
        [Tooltip("At most this many debris strikes damage anything per frame.")]
        public int debrisStrikesPerFrame = 20;

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
        [Tooltip("A chunk's health follows its size, clamped to this range of the wall's chunkHealth.")]
        public Vector2 chunkHealthClamp = new Vector2(0.7f, 1.3f);
        [Tooltip("An intact wall whose stored damage reaches the fracture share breaks up into its chunks.")]
        public bool fractureOnAccumulated = true;
        [Tooltip("Share of a removed chunk's overkill passed to its attached neighbours.")]
        [Range(0f, 1f)] public float overflowShare = 0.35f;
        [Tooltip("A chunk removed with this many times its max health of overkill comes out as rubble.")]
        public float rubbleOverkill = 1.5f;
        [Tooltip("Rubble pieces per chunk, more with more overkill.")]
        public Vector2Int rubblePieces = new Vector2Int(3, 6);
        [Tooltip("Share of the striker's speed an impact gives a chunk it removes (blasts: 1).")]
        [Range(0f, 1f)] public float impactChunkLaunchShare = 0.7f;
        [Tooltip("kg. Chunks heavier than this fly slower, lighter ones faster, within chunkLaunchMassFactor.")]
        public float chunkLaunchReferenceMass = 150f;
        public Vector2 chunkLaunchMassFactor = new Vector2(0.4f, 1.5f);
        [Tooltip("Share of the launch speed added upwards.")]
        public float chunkUpBias = 0.25f;
        [Tooltip("rad/s of spin on a launched chunk, random in this range.")]
        public Vector2 chunkSpin = new Vector2(2f, 5f);
        [Tooltip("kg. A chunk's Rigidbody mass is capped here on both machines (its real mass still counts for blame and events).")]
        public float debrisPhysicsMassCap = 150f;
        [Tooltip("s. The blast that launched a chunk does not push it again within this time.")]
        public float structureLaunchGrace = 0.3f;

        [Header("Debris lifetimes (s)")]
        public float structureDebrisLifetime = 60f;
        public float propDebrisLifetime = 40f;
        public float glassShardLifetime = 20f;

        [Header("Debris budgets")]
        [Tooltip("Dynamic pieces at most; over it the least visible ones shrink away first.")]
        public int maxDynamicPieces = 450;
        [Tooltip("Frozen (kinematic) pieces at most. Raise only after the draw-call check.")]
        public int maxFrozenPieces = 300;
        [Tooltip("New pieces per frame at most.")]
        public int maxSpawnPerFrame = 90;
        [Tooltip("Of those, kept for wall chunks in a frame a blast or a ram announced them (DebrisManager.ExpectStructure).")]
        public int structureReservePerFrame = 30;
        [Tooltip("Pieces destroyed (or pooled) per frame at most.")]
        public int destroyPerFrame = 30;
        [Tooltip("Lifetimes vary by this share either way.")]
        [Range(0f, 1f)] public float lifetimeJitter = 0.25f;
        [Tooltip("m. An expired piece shrinks only out of every crew camera's view or farther than this from every crew eye.")]
        public float cleanupMinDistance = 12f;
        [Tooltip("At this many lifetimes a piece shrinks whoever sees it.")]
        public float hardLifetimeFactor = 2f;
        [Tooltip("s. How long an expired piece takes to shrink away.")]
        public float cleanupShrinkTime = 1.5f;
        [Tooltip("s. The same for a piece culled over the cap.")]
        public float overflowShrinkTime = 0.6f;
        [Tooltip("s asleep on static ground before a piece freezes (kinematic).")]
        public float freezeAfterSleep = 3f;
        [Tooltip("m/s a blast gives the frozen pieces it wakes.")]
        public float explosionWakeSpeed = 8f;
        [Tooltip("m (bounds extent). Smaller pieces cast no shadow.")]
        public float smallPieceNoShadow = 0.3f;
        [Tooltip("m (bounds extent). Smaller frozen pieces cast no shadow.")]
        public float frozenNoShadowBelow = 0.5f;

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

        // The multiplier of one kind of hit (ImpactDamage.Evaluate). A blast is 1: it has its own numbers.
        public static float TypeMultiplier(DamageType type)
        {
            var t = Current;
            switch (type)
            {
                case DamageType.Impact: return t.impactMultiplier;
                case DamageType.Thrown: return t.thrownMultiplier;
                case DamageType.Fall: return t.fallMultiplier;
                case DamageType.Crush: return t.crushMultiplier;
                case DamageType.Tool: return t.toolMultiplier;
                case DamageType.Vehicle: return t.vehicleMultiplier;
                default: return 1f;
            }
        }

        // What a blast does to a wall at d metres from its centre, on top of the falloff: the
        // structureFalloff curve, or the old focus when the curve has fewer than 2 keys.
        public static float Focus(float d)
        {
            var t = Current;
            d = Mathf.Max(0f, d);
            var curve = t.structureFalloff;
            if (curve != null && curve.length >= 2) return Mathf.Clamp01(curve.Evaluate(d));
            float f = Mathf.Max(0.01f, t.structureFocus);
            float x = d / f;
            return 1f / (1f + x * x);
        }

        // The same for a blast of this power: a stronger one reaches further (focusScalesWithPower).
        public static float Focus(float d, float power)
        {
            if (Current.focusScalesWithPower && power > 0f) d /= Mathf.Pow(power, 1f / 3f);
            return Focus(d);
        }

        public static AnimationCurve DefaultStructureFalloff()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.5f, 0.95f), new Keyframe(1f, 0.7f), new Keyframe(1.5f, 0.35f),
                new Keyframe(2f, 0.15f), new Keyframe(3f, 0.03f), new Keyframe(4f, 0f));
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
                // DEV 2: hedges and bushes. Soft, light, easy to break, they barely cover a blast.
                case BreakMaterial.Plant: return new Row(m, 60f, 1.5f, 300f, 0.8f, ImpactAudio.Kind.Wood);
                default: return new Row(m, 180f, 4.5f, 1000f, 0.35f, ImpactAudio.Kind.Crunch);
            }
        }
    }
}
