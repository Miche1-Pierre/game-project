using UnityEngine;

namespace Movers
{
    // Where damage comes from. The receiver never needs to know the source object, only this.
    public enum DamageType : byte { Blast, Impact, Thrown, Fall, Crush, Tool }

    // One ladder for everything that breaks. Each kind caps it and names the steps:
    //   glass       Intact > Damaged ("Cracked") > Destroyed ("Broken")
    //   wall        Intact > Damaged > Fractured > Destroyed ("Collapsed")
    //   foundation  Intact > Damaged, never further
    //   prop        Intact > Damaged ("Broken", half pay) > Destroyed
    public enum DestructionState : byte { Intact = 0, Damaged = 1, Fractured = 2, Destroyed = 3 }

    // A hit, decoupled from what caused it (grenade, thrown sofa, falling roof, a future tool).
    // Immutable and free of references except the local-only sourceObject, so it can be sent
    // over a network later (CLAUDE.md section 12).
    public readonly struct DamageEvent
    {
        public readonly Vector3 position;     // where it lands, world space
        public readonly Vector3 direction;    // unit, away from the source
        public readonly float damage;         // before the receiver's material resistance
        public readonly float impulse;        // N.s along direction, for the debris
        public readonly float radius;         // blast radius, 0 for a point hit
        public readonly DamageType type;
        public readonly int instigator;       // see Actors
        public readonly Object sourceObject;  // local only, may be null (the grenade is already gone)

        public DamageEvent(Vector3 position, Vector3 direction, float damage, float impulse, float radius,
                           DamageType type, int instigator = Actors.World, Object sourceObject = null)
        {
            this.position = position;
            this.direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.up;
            this.damage = damage;
            this.impulse = impulse;
            this.radius = radius;
            this.type = type;
            this.instigator = instigator;
            this.sourceObject = sourceObject;
        }

        public Vector3 ImpulseVector => direction * impulse;

        // The per-target copy a blast makes: same type, radius and instigator, local numbers.
        public DamageEvent With(float newDamage, float newImpulse, Vector3 at, Vector3 dir)
        {
            return new DamageEvent(at, dir, newDamage, newImpulse, radius, type, instigator, sourceObject);
        }
    }

    public struct DamageResult
    {
        public float applied;                 // after resistance
        public DestructionState before, after;
        public bool removed;                  // this hit took the thing (or a chunk) out of the world

        public static DamageResult None(DestructionState s) =>
            new DamageResult { applied = 0f, before = s, after = s, removed = false };
    }

    // Anything that takes damage: props (Breakable), panes (GlassPane), wall modules.
    public interface IDamageable
    {
        BreakMaterial Material { get; }
        DestructionState State { get; }
        float Health { get; }
        float MaxHealth { get; }
        bool IsGone { get; }                  // destroyed, broken or switched off
        Bounds WorldBounds { get; }
        DamageResult ApplyDamage(in DamageEvent e);
    }
}
