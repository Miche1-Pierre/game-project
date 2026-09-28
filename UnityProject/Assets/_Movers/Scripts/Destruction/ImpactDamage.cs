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
            public float minRatio;         // how light the other side may count, at least; <= 0: the table's minMassRatio
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
            minRatio = DestructionMaterialTable.Current.glassMinMassRatio,
        };

        // Cargo knocking against its own truck's box: cushioned, so one ram does not wipe the load.
        public static Rules Cargo
        {
            get
            {
                var t = DestructionMaterialTable.Current;
                return new Rules
                {
                    speedAllowance = t.cargoSpeedAllowance,
                    maxRatio = t.cargoMaxMassRatio,
                    debrisMinMass = t.crushMinMass,
                };
            }
        }

        // ---- the energy model (DEV 2, 03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md section 2) ----

        public struct ImpactInput
        {
            public float normalSpeed;       // m/s, >= 0
            public float strikerMass;       // kg of the other side; float.PositiveInfinity when static or kinematic
            public float receiverMass;      // kg: its own body, a pane's mass, or structureReferenceMass for a built piece
            public bool receiverAnchored;   // built piece: infinitely heavy in the reduced mass
            public BreakMaterial material;
            public DamageType type;
            public float speedAllowance;    // m/s added to the material's minimum speed
            public float minMassRatio;      // <= 0: the table's minMassRatio
            public float maxMassRatio;      // <= 0: the table's (vehicleMaxMassRatio for Vehicle, else maxMassRatio)
            public float strikeFactor;      // <= 0: 1. structureChunkStrikeFactor for a launched wall chunk
        }

        public struct ImpactOutcome
        {
            public float energy;            // J, 0.5 * mu * v^2
            public float effectiveSpeed;    // m/s
            public float damage;            // HP before the receiver's material factor
            public float spread;            // m (0 for Blast)
            public float push;              // N.s
            public bool Hurts => damage > 0f;
        }

        // One rule for every collision: the collision energy E = 0.5 * mu * v^2 (mu the reduced
        // mass of the two sides) against the receiver's resisting mass m_res gives an effective
        // speed v_eff = v * sqrt(clamp(mu / m_res)); past the material's minimum speed the damage
        // grows with its square. A prop hit by something static has ratio 1, so energyDamageScale
        // 27 = 9 x 3 keeps every number a prop suffered before. The receiver applies its material
        // factor afterwards, as before.
        public static ImpactOutcome Evaluate(in ImpactInput input)
        {
            var o = default(ImpactOutcome);
            float v = Mathf.Max(0f, input.normalSpeed);
            float mRes = Mathf.Max(0.01f, input.receiverMass);
            float mu = ReducedMass(input.receiverAnchored ? float.PositiveInfinity : mRes, input.strikerMass);
            if (!(v > 0f) || !(mu > 0f)) return o;

            var t = DestructionMaterialTable.Current;
            float minRatio = input.minMassRatio > 0f ? input.minMassRatio : t.minMassRatio;
            float maxRatio = input.maxMassRatio > 0f ? input.maxMassRatio
                           : input.type == DamageType.Vehicle ? t.vehicleMaxMassRatio : t.maxMassRatio;
            minRatio = Mathf.Max(0f, minRatio);
            maxRatio = Mathf.Max(minRatio, maxRatio);
            float ratio = Mathf.Clamp(mu / mRes, minRatio, maxRatio);

            o.energy = 0.5f * mu * v * v;
            o.effectiveSpeed = v * Mathf.Sqrt(ratio);
            float gate = DestructionMaterialTable.Get(input.material).minImpactSpeed + Mathf.Max(0f, input.speedAllowance);
            float extra = Mathf.Max(0f, o.effectiveSpeed - gate);
            float strike = input.strikeFactor > 0f ? input.strikeFactor : 1f;
            o.damage = t.energyDamageScale * DestructionMaterialTable.TypeMultiplier(input.type) * strike * extra * extra;
            o.spread = input.type != DamageType.Blast && o.damage > 0f
                ? Mathf.Min(t.impactMaxSpread, t.impactSpreadPerCubeRootKJ * Mathf.Pow(o.energy / 1000f, 1f / 3f))
                : 0f;
            o.push = t.impactPushFactor * v * Mathf.Min(mu, mRes);
            return o;
        }

        // m_a * m_b / (m_a + m_b), where an infinite side (static, kinematic, anchored) leaves the
        // other side's mass. 0 when both are infinite: two immovable things do not collide.
        public static float ReducedMass(float a, float b)
        {
            bool aInf = float.IsPositiveInfinity(a), bInf = float.IsPositiveInfinity(b);
            if (aInf && bInf) return 0f;
            if (aInf) return Mathf.Max(0f, b);
            if (bInf) return Mathf.Max(0f, a);
            a = Mathf.Max(0f, a);
            b = Mathf.Max(0f, b);
            float sum = a + b;
            return sum > 0f ? a * b / sum : 0f;
        }

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
