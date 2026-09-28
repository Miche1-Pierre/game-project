using UnityEngine;

namespace Movers
{
    // Where damage comes from. The receiver never needs to know the source object, only this.
    // Values travel as bytes: append only. Vehicle = a truck or a police car driving into it.
    public enum DamageType : byte { Blast, Impact, Thrown, Fall, Crush, Tool, Vehicle }

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
        public readonly float radius;         // Blast: blast radius. Any other type: impact spread (m), 0 = point hit
        public readonly DamageType type;
        public readonly int instigator;       // see Actors
        public readonly Object sourceObject;  // local only, may be null (the grenade is already gone)
        // m/s. Impacts: the striker's approach speed along the normal. Blasts: the eject speed at
        // this point (BlastSolver). 0 = unknown (older callers): receivers fall back to impulse / mass.
        public readonly float speed;

        public DamageEvent(Vector3 position, Vector3 direction, float damage, float impulse, float radius,
                           DamageType type, int instigator = Actors.World, Object sourceObject = null,
                           float speed = 0f)
        {
            this.position = position;
            this.direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.up;
            this.damage = damage;
            this.impulse = impulse;
            this.radius = radius;
            this.type = type;
            this.instigator = instigator;
            this.sourceObject = sourceObject;
            this.speed = speed;
        }

        public Vector3 ImpulseVector => direction * impulse;

        // The per-target copy a blast makes: same type, radius, instigator and speed, local numbers.
        public DamageEvent With(float newDamage, float newImpulse, Vector3 at, Vector3 dir)
        {
            return new DamageEvent(at, dir, newDamage, newImpulse, radius, type, instigator, sourceObject, speed);
        }

        public DamageEvent WithSpeed(float newSpeed)
        {
            return new DamageEvent(position, direction, damage, impulse, radius, type, instigator, sourceObject, newSpeed);
        }
    }

    public struct DamageResult
    {
        // HP actually removed after resistance, capped at each target's or chunk's remaining
        // health, summed over the chunks a spread hit reached.
        public float applied;
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
