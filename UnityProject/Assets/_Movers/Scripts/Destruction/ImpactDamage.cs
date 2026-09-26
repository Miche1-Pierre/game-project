using UnityEngine;

namespace Movers
{
    // How hard a collision hits, as a DamageEvent. The rule every breakable shares (it used to
    // live inside Breakable as MeasureImpact, same maths):
    //   only the approach speed along the contact normal counts, and only above the material's
    //   minimum speed; past that the damage grows with the square of the extra speed, times
    //   how heavy the other side is against this one (capped: a wall does not give way).
    //
    // New here is who and how: the event says whether it was a throw, a fall or something
    // heavy landing (a chunk of wall, a roof section), and who is to blame, so the grandmother
    // knows that the vase P2 threw a second ago is P2's doing (SLICE_ARCHITECTURE section 5).
    //
    // No state of its own, nothing cached. (Who a blast launched is Explosion's to remember.)
    public static class ImpactDamage
    {
        public struct Rules
        {
            public float speedAllowance;   // m/s added to the material's minimum speed
            public float maxRatio;         // how heavy the other side may count, at most
            public float debrisMinMass;    // debris lighter than this does not count at all
        }

        // The other body's weight counts up to three times this one's. Anything static or
        // kinematic is taken as that heavy.
        public const float MaxRatio = 3f;
        // In someone's hands a knock is softened twice (see Breakable): 1.5 m/s more to hurt,
        // and a wall counts no heavier than the object itself, because the arms give.
        public const float HeldSpeedAllowance = 1.5f;
        public const float HeldMaxRatio = 1f;
        // Wall chunks flying through a window take the glass with them; glass shards hitting the
        // next pane do not, or one broken window would chain through the whole veranda.
        public const float GlassDebrisMinMass = 2f;
        // "Who did it" reaches back this far: a vase thrown by P2 that breaks a window a second
        // later was P2's doing (MovableObject.RecentHandler).
        public const float BlameSeconds = 2f;

        public static Rules Plain => new Rules
        {
            speedAllowance = 0f,
            maxRatio = MaxRatio,
            debrisMinMass = DestructionMaterialTable.Current.crushMinMass,
        };

        public static Rules Held => new Rules
        {
            speedAllowance = HeldSpeedAllowance,
            maxRatio = HeldMaxRatio,
            debrisMinMass = DestructionMaterialTable.Current.crushMinMass,
        };

        public static Rules Glass => new Rules
        {
            speedAllowance = 0f,
            maxRatio = MaxRatio,
            debrisMinMass = GlassDebrisMinMass,
        };

        // False means "this contact does not hurt". myBody is the receiver's own dynamic body
        // (a thrown vase), or null for something built in.
        public static bool TryMeasure(Collision c, BreakMaterial mat, float myMass, in Rules rules,
                                      Vector3 fallbackPoint, Rigidbody myBody, out DamageEvent e)
        {
            e = default;
            Collider other = c.collider;
            if (other == null) return false;
            // The crew walks on a CharacterController. Brushing past a vase is not an impact,
            // and the capsule has no mass to put in the formula anyway.
            if (other is CharacterController) return false;
            Rigidbody otherBody = c.rigidbody;

            DebrisPiece debris = FindDebris(other, otherBody);
            if (debris != null && (otherBody == null || otherBody.mass < rules.debrisMinMass)) return false;

            float v = c.relativeVelocity.magnitude;
            Vector3 normal = Vector3.zero;
            if (c.contactCount > 0)
            {
                ContactPoint cp = c.GetContact(0);
                normal = cp.normal;
                v = Mathf.Abs(Vector3.Dot(c.relativeVelocity, normal));
            }
            var row = DestructionMaterialTable.Get(mat);
            float vMin = row.minImpactSpeed + Mathf.Max(0f, rules.speedAllowance);
            if (!(v > vMin)) return false;

            float cap = Mathf.Max(0.3f, rules.maxRatio);
            float ratio = otherBody != null && !otherBody.isKinematic
                ? Mathf.Clamp(otherBody.mass / Mathf.Max(0.01f, myMass), 0.3f, cap)
                : cap;
            float extra = v - vMin;
            float damage = extra * extra * DestructionMaterialTable.Current.impactDamageScale * ratio;

            Vector3 point = fallbackPoint;
            if (c.contactCount > 0) point = c.GetContact(0).point;
            Vector3 dir = Vector3.zero;
            float push = 0f;
            if (otherBody != null)
            {
                dir = point - otherBody.worldCenterOfMass;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                    push = v * Mathf.Min(otherBody.mass, myMass) * 0.5f;
                }
            }
            if (dir == Vector3.zero) dir = normal != Vector3.zero ? normal : Vector3.up;

            DamageType type = Classify(debris, otherBody, myBody, out int instigator);
            e = new DamageEvent(point, dir, damage, push, 0f, type, instigator,
                                otherBody != null ? (Object)otherBody.gameObject : other.gameObject);
            return true;
        }

        // Crush: something heavy that came off the house landed on it. Thrown: a crew member
        // threw one of the two bodies in the last two seconds. Fall: this thing dropped onto
        // something that does not move. Otherwise a plain knock.
        static DamageType Classify(DebrisPiece debris, Rigidbody otherBody, Rigidbody myBody, out int instigator)
        {
            if (debris != null)
            {
                instigator = debris.instigator;
                return DamageType.Crush;
            }

            MovableObject otherMo = null, myMo = null;
            if (otherBody != null) otherBody.TryGetComponent(out otherMo);
            if (myBody != null) myBody.TryGetComponent(out myMo);
            float now = Time.time;
            if (otherMo != null && now - otherMo.lastThrownTime <= BlameSeconds)
            {
                instigator = otherMo.lastThrownBy;
                return DamageType.Thrown;
            }
            if (myMo != null && now - myMo.lastThrownTime <= BlameSeconds)
            {
                instigator = myMo.lastThrownBy;
                return DamageType.Thrown;
            }

            instigator = otherMo != null ? otherMo.RecentHandler(BlameSeconds) : Actors.World;
            if (instigator == Actors.World && myMo != null) instigator = myMo.RecentHandler(BlameSeconds);
            // Nobody's hands on it: maybe a blast threw it (a chair flung into a window).
            if (instigator == Actors.World) instigator = Explosion.LaunchedBy(otherBody, BlameSeconds);
            if (instigator == Actors.World) instigator = Explosion.LaunchedBy(myBody, BlameSeconds);

            bool otherStill = otherBody == null || otherBody.isKinematic;
            if (otherStill && myBody != null && !myBody.isKinematic)
            {
                Vector3 vel = myBody.linearVelocity;
                if (vel.y < 0f && -vel.y > new Vector2(vel.x, vel.z).magnitude) return DamageType.Fall;
            }
            return DamageType.Impact;
        }

        static DebrisPiece FindDebris(Collider other, Rigidbody otherBody)
        {
            if (other.TryGetComponent(out DebrisPiece d)) return d;
            if (otherBody != null && otherBody.TryGetComponent(out d)) return d;
            return null;
        }
    }
}
