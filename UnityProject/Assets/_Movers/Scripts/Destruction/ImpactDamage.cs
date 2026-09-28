using System.Text;
using UnityEngine;

namespace Movers
{
    // How hard a collision hits, as a DamageEvent. The rule every breakable shares, since DEV 2
    // the energy model of 03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md section 2 (Evaluate):
    //   only the approach speed along the contact normal counts; the collision energy, from the
    //   reduced mass of the two sides, against the receiver's resisting mass gives an effective
    //   speed, and past the material's minimum speed the damage grows with its square.
    //
    // It also says who and how: a throw, a fall, something heavy landing (a chunk of wall, a
    // roof section) or a vehicle driving into it, and who is to blame, so the grandmother knows
    // that the vase P2 threw a second ago is P2's doing and the fence the truck went through is
    // its driver's (SLICE_ARCHITECTURE section 5).
    //
    // Its only state is a per-frame count of debris strikes (debrisStrikesPerFrame), so a wall
    // coming down does not set off a damage cascade through the house. (Who a blast launched is
    // Explosion's to remember.)
    public static class ImpactDamage
    {
        public struct Rules
        {
            public float speedAllowance;   // m/s added to the material's minimum speed
            public float maxRatio;         // how heavy the other side may count, at most
            public float debrisMinMass;    // debris lighter than this does not count at all
            public float minRatio;         // how light the other side may count, at least; <= 0: the table's minMassRatio
        }

        // The plain cap, from before the energy model: a static other counted three times this
        // one. A Rules.maxRatio of MaxRatio now means "the table's caps" (maxMassRatio, or
        // vehicleMaxMassRatio for a vehicle); a lower one cushions (Held, Cargo, see Input).
        public const float MaxRatio = 3f;
        // In someone's hands a knock is softened twice (see Breakable): 1.5 m/s more to hurt,
        // and a wall counts no heavier than the object itself, because the arms give. The table's
        // heldSpeedAllowance and heldMaxMassRatio win; these are their defaults.
        public const float HeldSpeedAllowance = 1.5f;
        public const float HeldMaxRatio = 1f;
        // Wall chunks flying through a window take the glass with them; glass shards hitting the
        // next pane do not, or one broken window would chain through the whole veranda. The
        // table's glassDebrisMinMass wins.
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

        public static Rules Held
        {
            get
            {
                var t = DestructionMaterialTable.Current;
                return new Rules
                {
                    speedAllowance = t.heldSpeedAllowance,
                    maxRatio = t.heldMaxMassRatio,
                    debrisMinMass = t.crushMinMass,
                };
            }
        }

        // A pane counts even a light striker as half its own mass: a thrown cup still breaks it.
        public static Rules Glass
        {
            get
            {
                var t = DestructionMaterialTable.Current;
                return new Rules
                {
                    speedAllowance = 0f,
                    maxRatio = MaxRatio,
                    debrisMinMass = t.glassDebrisMinMass,
                    minRatio = t.glassMinMassRatio,
                };
            }
        }

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

        // Debris strikes that did damage this frame, at most debrisStrikesPerFrame (the overlay shows it).
        public static int DebrisStrikesThisFrame => strikeFrame == Time.frameCount ? debrisStrikes : 0;
        // The same count for the last completed frame (the F3 overlay line).
        public static int DebrisStrikesLastFrame
        {
            get
            {
                int f = Time.frameCount;
                if (strikeFrame == f - 1) return debrisStrikes;
                return strikeFrame == f && previousStrikeFrame == f - 1 ? previousStrikes : 0;
            }
        }
        static int strikeFrame = -1;
        static int debrisStrikes;
        static int previousStrikeFrame = -1;
        static int previousStrikes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            strikeFrame = -1;
            debrisStrikes = 0;
            previousStrikeFrame = -1;
            previousStrikes = 0;
        }

        // False means "this contact does not hurt". myBody is the receiver's own dynamic body
        // (a thrown vase), or null or kinematic for something built in: anchored. myMass is its
        // resisting mass: the body's mass, a pane's own mass, or structureReferenceMass for any
        // other built piece.
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

            bool launchedChunk = debris != null && debris.structureChunk;
            if (launchedChunk && !(DestructionMaterialTable.Current.structureChunkStrikeFactor > 0f)) return false;

            float v = c.relativeVelocity.magnitude;
            Vector3 normal = Vector3.zero;
            Vector3 point = fallbackPoint;
            if (c.contactCount > 0)
            {
                ContactPoint cp = c.GetContact(0);
                normal = cp.normal;
                point = cp.point;
                v = Mathf.Abs(Vector3.Dot(c.relativeVelocity, normal));
            }
            if (!(v > 0f)) return false;

            bool anchored = myBody == null || myBody.isKinematic;
            bool otherMoves = otherBody != null && !otherBody.isKinematic;

            // A vehicle driving into it, or cargo knocking against its own truck.
            TruckVehicle truck = null;
            bool vehicle = false;
            Rules r = rules;
            if (otherBody != null && debris == null)
            {
                if (otherBody.TryGetComponent(out truck))
                {
                    // The ram sweep already applied this hit (TruckRam): the contact does not count twice.
                    if (RamHandled(c)) return false;
                    if (IsCargoOf(truck, myBody)) r = Softer(r, Cargo);
                    else vehicle = Drives(otherBody, anchored, point, v);
                }
                else if (otherBody.TryGetComponent(out PoliceCar _))
                {
                    vehicle = Drives(otherBody, anchored, point, v);
                }
            }

            // The kind of hit only scales the damage, never the gate: a contact too soft for any
            // kind stops here, before the blame lookups.
            ImpactInput input = MakeInput(v, otherMoves ? otherBody.mass : float.PositiveInfinity, myMass, anchored,
                                          mat, vehicle ? DamageType.Vehicle : DamageType.Impact, r, launchedChunk);
            ImpactOutcome o = Evaluate(input);
            float gate = DestructionMaterialTable.Get(mat).minImpactSpeed + Mathf.Max(0f, r.speedAllowance);
            if (!(o.effectiveSpeed > gate)) return false;

            DamageType type = Classify(debris, otherBody, myBody, out int instigator);
            if (vehicle)
            {
                type = DamageType.Vehicle;
                instigator = truck != null ? truck.DriverActor : Actors.World;
            }
            // Whatever else the truck breaks by moving (cargo thrown about in the box) is its driver's doing too.
            else if (truck != null && instigator == Actors.World)
            {
                instigator = truck.DriverActor;
            }
            if (type != input.type)
            {
                input.type = type;
                o = Evaluate(input);
            }
            if (!o.Hurts) return false;
            if (debris != null && !TakeDebrisStrike()) return false;

            Vector3 dir = Vector3.zero;
            float push = 0f;
            if (otherBody != null)
            {
                dir = point - otherBody.worldCenterOfMass;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                    push = o.push;
                }
            }
            if (dir == Vector3.zero) dir = normal != Vector3.zero ? normal : Vector3.up;

            e = new DamageEvent(point, dir, o.damage, push, o.spread, type, instigator,
                                otherBody != null ? (Object)otherBody.gameObject : other.gameObject, v);
            return true;
        }

        // A contact as Evaluate's input: what TryMeasure measured, and what the calibration check
        // replays. A Rules.maxRatio under MaxRatio (Held, Cargo) caps the ratio and also keeps
        // today's cushioning as a strike factor (maxRatio / MaxRatio): before the energy model a
        // static wall counted 3 times a prop and 1 time a held one, and energyDamageScale 27 = 9 x 3
        // turned the plain 3 into a ratio of 1, so without it a held knock would hurt 3 times more.
        static ImpactInput MakeInput(float normalSpeed, float strikerMass, float receiverMass, bool receiverAnchored,
                                     BreakMaterial mat, DamageType type, in Rules rules, bool launchedChunk)
        {
            bool cushioned = rules.maxRatio > 0f && rules.maxRatio < MaxRatio;
            float strike = launchedChunk ? DestructionMaterialTable.Current.structureChunkStrikeFactor : 1f;
            if (cushioned) strike *= rules.maxRatio / MaxRatio;
            return new ImpactInput
            {
                normalSpeed = normalSpeed,
                strikerMass = strikerMass,
                receiverMass = receiverMass,
                receiverAnchored = receiverAnchored,
                material = mat,
                type = type,
                speedAllowance = rules.speedAllowance,
                minMassRatio = rules.minRatio,
                maxMassRatio = cushioned ? rules.maxRatio : 0f,
                strikeFactor = Mathf.Max(1e-4f, strike),
            };
        }

        // A vehicle is the striker when it is the one moving: always against something built in,
        // and against a loose body when its own speed at the contact is at least half the closing
        // speed. A vase thrown at a parked truck breaks on it as on a wall; a chair the truck
        // drives into is a Vehicle hit (the bumper absorbs most of it, the fling does the rest).
        // The speeds read here are after the contact: a 3.5 t truck barely slows on a chair.
        static bool Drives(Rigidbody vehicle, bool receiverAnchored, Vector3 point, float closingSpeed)
        {
            if (receiverAnchored) return true;
            return vehicle.GetPointVelocity(point).magnitude >= 0.5f * closingSpeed;
        }

        static bool RamHandled(Collision c)
        {
            int n = c.contactCount;
            for (int i = 0; i < n; i++)
                if (TruckRam.Handled(c.GetContact(i).thisCollider)) return true;
            return false;
        }

        // A body its truck counts as loaded (TruckCargo.inside), knocking against that truck.
        static bool IsCargoOf(TruckVehicle truck, Rigidbody myBody)
        {
            if (myBody == null || truck.cargo == null) return false;
            return myBody.TryGetComponent(out MovableObject mo) && truck.cargo.inside.Contains(mo);
        }

        // The more cushioned of two rules (cargo that someone is also holding).
        static Rules Softer(in Rules a, in Rules b)
        {
            return new Rules
            {
                speedAllowance = Mathf.Max(a.speedAllowance, b.speedAllowance),
                maxRatio = Mathf.Min(a.maxRatio, b.maxRatio),
                debrisMinMass = Mathf.Max(a.debrisMinMass, b.debrisMinMass),
                minRatio = a.minRatio,
            };
        }

        // A wall coming down throws dozens of heavy pieces at once: at most debrisStrikesPerFrame
        // of them damage anything in one frame, the rest are ignored that frame.
        static bool TakeDebrisStrike()
        {
            int frame = Time.frameCount;
            if (frame != strikeFrame)
            {
                previousStrikeFrame = strikeFrame;
                previousStrikes = debrisStrikes;
                strikeFrame = frame;
                debrisStrikes = 0;
            }
            if (debrisStrikes >= DestructionMaterialTable.Current.debrisStrikesPerFrame) return false;
            debrisStrikes++;
            return true;
        }

        // Crush: something heavy that came off the house landed on it. Thrown: a crew member
        // threw one of the two bodies in the last two seconds. Fall: this thing dropped onto
        // something that does not move. Otherwise a plain knock. (Vehicle is TryMeasure's call.)
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

        // ---- calibration check (DEV2_DESTRUCTION_GAMEPLAY.md 2.2), editor-free ----

        // Replays the rows of the 2.2 table through MakeInput and Evaluate (the path TryMeasure
        // takes after reading the contact) on a fresh code-default table, the receiver's material
        // factor included, and compares with the numbers the spec was calibrated on (1 % + 0.5 HP,
        // spreads 0.02 m). Also: a prop's own damage equals the pre-DEV 2 formula, plain and held,
        // and a thrown cup still breaks a pane. Needs no editor and no scene:
        //   bool ok = Movers.ImpactDamage.CalibrationCheck(out string report);
        public static bool CalibrationCheck(out string report)
        {
            var previous = DestructionMaterialTable.Current;
            var table = ScriptableObject.CreateInstance<DestructionMaterialTable>();
            table.hideFlags = HideFlags.HideAndDontSave;
            var sb = new StringBuilder("ImpactDamage calibration (2.2, code defaults)\n");
            bool ok = true;
            try
            {
                DestructionMaterialTable.Use(table);
                const float Inf = float.PositiveInfinity;
                const BreakMaterial Plaster = BreakMaterial.Plaster, Wood = BreakMaterial.Wood;
                const DamageType Thrown = DamageType.Thrown, Vehicle = DamageType.Vehicle, Crush = DamageType.Crush;
                float wall = table.structureReferenceMass;
                Rules plain = Plain, held = Held, glass = Glass;
                float kmh = 1f / 3.6f;

                sb.Append("vs a plaster wall (thrown, vehicles, a launched chunk, a falling roof)\n");
                ok &= Row(sb, "cup 2 kg at 6 m/s", 2f, 6f, wall, true, Plaster, Thrown, plain, false, 0f);
                ok &= Row(sb, "chair 10 kg at 7 m/s", 10f, 7f, wall, true, Plaster, Thrown, plain, false, 0f);
                ok &= Row(sb, "sofa 60 kg at 6 m/s", 60f, 6f, wall, true, Plaster, Thrown, plain, false, 0f);
                ok &= Row(sb, "sofa 60 kg at 7 m/s", 60f, 7f, wall, true, Plaster, Thrown, plain, false, 36.7f);
                ok &= Row(sb, "sofa 60 kg at 9 m/s", 60f, 9f, wall, true, Plaster, Thrown, plain, false, 386.6f, 0.40f);
                ok &= Row(sb, "piano 180 kg at 7 m/s", 180f, 7f, wall, true, Plaster, Thrown, plain, false, 1674.2f, 0.49f);
                ok &= Row(sb, "piano 180 kg at 8.5 m/s", 180f, 8.5f, wall, true, Plaster, Thrown, plain, false, 3433.1f, 0.56f);
                ok &= Row(sb, "truck 3.5 t at 10 km/h", 3500f, 10f * kmh, wall, true, Plaster, Vehicle, plain, false, 105.6f);
                ok &= Row(sb, "truck 3.5 t at 15 km/h", 3500f, 15f * kmh, wall, true, Plaster, Vehicle, plain, false, 306.0f, 0.94f);
                ok &= Row(sb, "truck 3.5 t at 25 km/h", 3500f, 25f * kmh, wall, true, Plaster, Vehicle, plain, false, 1019.4f, 1.32f);
                ok &= Row(sb, "truck 3.5 t at 40 km/h", 3500f, 40f * kmh, wall, true, Plaster, Vehicle, plain, false, 2870.6f, 1.80f);
                ok &= Row(sb, "truck 3.5 t at 90 km/h", 3500f, 90f * kmh, wall, true, Plaster, Vehicle, plain, false, 15812.3f, 2.50f);
                ok &= Row(sb, "truck 6 t at 10 km/h", 6000f, 10f * kmh, wall, true, Plaster, Vehicle, plain, false, 217.3f);
                ok &= Row(sb, "truck 6 t at 40 km/h", 6000f, 40f * kmh, wall, true, Plaster, Vehicle, plain, false, 5103.1f, 2.15f);
                ok &= Row(sb, "truck 6 t at 90 km/h", 6000f, 90f * kmh, wall, true, Plaster, Vehicle, plain, false, 27531.8f);
                ok &= Row(sb, "police car 1.4 t at 80 km/h", 1400f, 80f * kmh, wall, true, Plaster, Vehicle, plain, false, 4743.5f);
                ok &= Row(sb, "launched chunk 150 kg at 10 m/s", 150f, 10f, wall, true, Plaster, Crush, plain, true, 217.8f);
                ok &= Row(sb, "launched chunk 150 kg at 5 m/s", 150f, 5f, wall, true, Plaster, Crush, plain, true, 7.5f);
                ok &= Row(sb, "falling roof 1 t at 6.3 m/s", 1000f, 6.3f, wall, true, Plaster, Crush, plain, false, 2579.8f);

                sb.Append("vs a wooden fence (140 HP)\n");
                ok &= Row(sb, "sofa 60 kg at 6 m/s: explodes", 60f, 6f, wall, true, Wood, Thrown, plain, false, 304.1f);
                ok &= Row(sb, "chair 10 kg at 7 m/s", 10f, 7f, wall, true, Wood, Thrown, plain, false, 0f);
                ok &= Row(sb, "truck 3.5 t at 10 km/h: explodes", 3500f, 10f * kmh, wall, true, Wood, Vehicle, plain, false, 146.3f);
                ok &= Row(sb, "launched chunk 150 kg at 10 m/s: explodes", 150f, 10f, wall, true, Wood, Crush, plain, true, 354.4f);
                ok &= Row(sb, "launched chunk 150 kg at 5 m/s: holds", 150f, 5f, wall, true, Wood, Crush, plain, true, 46.2f);

                sb.Append("props and panes\n");
                ok &= Row(sb, "truck at 90 km/h vs a 10 kg chair (153 HP): survives, then flung", 3500f, 25f, 10f, false, Wood,
                          Vehicle, plain, false, 113.1f);
                ok &= Row(sb, "cup 2 kg at 6 m/s vs a 10 kg pane (12 HP): breaks at once", 2f, 6f, 10f, true, BreakMaterial.Glass,
                          Thrown, glass, false, 112.7f);
                float vMin = DestructionMaterialTable.Get(BreakMaterial.Ceramic).minImpactSpeed;
                ok &= Row(sb, "5 kg vase on the floor at 6 m/s = before DEV 2", Inf, 6f, 5f, false, BreakMaterial.Ceramic,
                          DamageType.Impact, plain, false, 9f * MaxRatio * Sq(6f - vMin));
                ok &= Row(sb, "the same vase held = before DEV 2", Inf, 6f, 5f, false, BreakMaterial.Ceramic,
                          DamageType.Impact, held, false, 9f * HeldMaxRatio * Sq(6f - vMin - HeldSpeedAllowance));
            }
            finally
            {
                DestructionMaterialTable.Use(previous);
                if (Application.isPlaying) Object.Destroy(table);
                else Object.DestroyImmediate(table);
            }
            sb.Append(ok ? "ALL OK" : "SOME ROWS FAILED");
            report = sb.ToString();
            return ok;
        }

        static bool Row(StringBuilder sb, string label, float strikerKg, float speed, float receiverKg, bool anchored,
                        BreakMaterial mat, DamageType type, in Rules rules, bool launchedChunk, float expected,
                        float expectedSpread = -1f)
        {
            ImpactOutcome o = Evaluate(MakeInput(speed, strikerKg, receiverKg, anchored, mat, type, rules, launchedChunk));
            float damage = o.damage * DestructionMaterialTable.Factor(type, mat);
            bool pass = Mathf.Abs(damage - expected) <= expected * 0.01f + 0.5f
                        && (expectedSpread < 0f || Mathf.Abs(o.spread - expectedSpread) <= 0.02f);
            sb.Append(pass ? "  ok    " : "  FAIL  ").Append(label).Append(": ").Append(damage.ToString("0.0"))
              .Append(" HP (expected ").Append(expected.ToString("0.0")).Append("), spread ")
              .Append(o.spread.ToString("0.00")).Append(" m, ").Append((o.energy / 1000f).ToString("0.0")).Append(" kJ\n");
            return pass;
        }

        static float Sq(float x) => x * x;
    }
}
