using System;
using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // The truck as a ram (03_TECHNICAL/DEV2_DESTRUCTION_GAMEPLAY.md 6.4): a look-ahead sweep, host
    // only, that applies the collision's energy to what is in the truck's path before the physics
    // contact, so a fence or a wall that gives way lets the truck through, and flings small props
    // out of the way. Lives inside TruckVehicle, like CrewBumper.
    //
    // Every physics step above ramMinSpeed:
    //   1. the hull's box, turned with the truck, is cast along the velocity from ramSweepExtra
    //      behind it, plus an overlap of a slab on the face it travels toward (a cast never
    //      reports what already touches the bumper);
    //   2. a built piece in the way (wall chunk, anchored Breakable such as a fence or a hedge,
    //      a pane) takes ImpactDamage.Evaluate as a Vehicle hit spread over its surroundings,
    //      blamed on the driver; a loose body under flingMassKg takes its share and is flung
    //      out of the lane;
    //   3. swept again: when nothing built is left in the path, the truck goes on minus the
    //      energy it spent (ramJoulesPerHp for each HP removed), written to its velocity before
    //      the solver runs, so the contact never happens. Something built still there: nothing
    //      changes and physics stops the truck against it.
    // Contacts the sweep missed fall back on the receivers' own collision callbacks; Handled
    // tells them which colliders the sweep already counted.
    //
    // Plain data plus one method, owned by TruckVehicle and called from its FixedUpdate. The
    // numbers are TruckTuning's.
    [Serializable]
    public sealed class TruckRam
    {
        const float BuiltHandledSeconds = 0.2f;
        const int MaxHits = 32;

        // Collider -> Time.time until which it counts as handled. Shared by every truck.
        static readonly Dictionary<Collider, float> handledUntil = new Dictionary<Collider, float>(64);
        static readonly List<Collider> expired = new List<Collider>(64);
        static float nextPurge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            handledUntil.Clear();
            expired.Clear();
            nextPurge = 0f;
        }

        // True: the ram sweep hit or flung this collider recently, so its own collision callback
        // must not count the same hit again (ImpactDamage.TryMeasure).
        public static bool Handled(Collider c)
        {
            return c != null && handledUntil.TryGetValue(c, out float until) && Time.time < until;
        }

        static void MarkHandled(Collider c, float seconds)
        {
            if (c == null) return;
            float until = Time.time + seconds;
            if (!handledUntil.TryGetValue(c, out float was) || was < until) handledUntil[c] = until;
        }

        static void PurgeHandled()
        {
            float now = Time.time;
            if (now < nextPurge || handledUntil.Count == 0) return;
            nextPurge = now + 1f;
            expired.Clear();
            foreach (var kv in handledUntil)
                if (kv.Key == null || kv.Value <= now) expired.Add(kv.Key);
            for (int i = 0; i < expired.Count; i++) handledUntil.Remove(expired[i]);
            expired.Clear();
        }

        // What one collider in the path turned out to be.
        enum Kind { None, Built, Fling }

        struct Target
        {
            public Kind kind;
            public Component receiver;          // DestructibleModule, Breakable or GlassPane; the body's Breakable (or null) for a fling
            public Rigidbody body;              // Fling only
            public Collider collider;
            public Vector3 point;
            public float normalSpeed;
        }

        // Scratch, made on first use: the serializer may skip initialisers.
        [NonSerialized] RaycastHit[] castHits;
        [NonSerialized] Collider[] overlaps;
        [NonSerialized] List<Target> targets;
        [NonSerialized] List<Collider> bodyColliders;
        [NonSerialized] List<Component> reachedKeys;
        [NonSerialized] List<Collider> reachedColliders;
        [NonSerialized] List<Component> staleCooldowns;
        [NonSerialized] Dictionary<Component, float> cooldownUntil;
        [NonSerialized] int mask;

        public float LastSweepMs { get; private set; }

        void Init()
        {
            if (castHits != null) return;
            castHits = new RaycastHit[MaxHits];
            overlaps = new Collider[MaxHits];
            targets = new List<Target>(16);
            bodyColliders = new List<Collider>(8);
            reachedKeys = new List<Component>(MaxHits * 2);
            reachedColliders = new List<Collider>(MaxHits * 2);
            staleCooldowns = new List<Component>(32);
            cooldownUntil = new Dictionary<Component, float>(32);
            // Debris, the crew, the grandmother and whatever asked not to be hit by rays: none is
            // the ram's business (the crew and the grandmother are CrewBumper's).
            mask = ~0;
            foreach (var layerName in new[] { "Debris", "Ignore Raycast", "Crew", "NPC" })
            {
                int l = LayerMask.NameToLayer(layerName);
                if (l >= 0) mask &= ~(1 << l);
            }
        }

        // The hull in the physics pose (not the interpolated transform), like TruckCargo's box.
        struct Frame
        {
            public Vector3 centre, half, dir, face;   // face: world normal of the face travelled toward
            public Quaternion rotation;
            public float speed, distance;
            public Vector3 slabCentre, slabHalf;
        }

        public void Tick(TruckVehicle truck, Rigidbody rb, Bounds hull, TruckTuning t, float dt)
        {
            if (!Net.HasAuthority) return;   // online, the host breaks things; the client gets the records
            PurgeHandled();
            if (truck == null || rb == null || t == null || hull.size == Vector3.zero) return;
            Vector3 vel = rb.linearVelocity;
            float speed = vel.magnitude;
            if (speed < Mathf.Max(0.1f, t.ramMinSpeed)) return;
            Init();
            float started = Time.realtimeSinceStartup;

            var f = MakeFrame(truck.transform, rb, hull, vel, speed, t, dt);
            DebrisManager.WakeInBounds(SweptBounds(f));

            Gather(truck, rb, f, t);
            if (targets.Count == 0)
            {
                LastSweepMs = (Time.realtimeSinceStartup - started) * 1000f;
                return;
            }

            float strikerMass = rb.mass + (truck.cargo != null ? truck.cargo.LoadedKg : 0f);
            float applied = 0f;
            bool expected = false;
            float now = Time.time;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                Component key = Key(target);
                if (key == null || (cooldownUntil.TryGetValue(key, out float until) && now < until)) continue;
                if (target.kind == Kind.Built)
                {
                    if (!expected)
                    {
                        DebrisManager.ExpectStructure(8);
                        expected = true;
                    }
                    applied += HitBuilt(truck, target, f, strikerMass);
                }
                else if (target.kind == Kind.Fling) Fling(truck, target, f, strikerMass, t);
                if (key != null) cooldownUntil[key] = now + t.ramCooldown;
            }
            if (cooldownUntil.Count > 64) ForgetCooldowns(now);

            // The toll: what broke is paid from the truck's energy, if the way is now clear.
            if (applied > 0f && !BuiltInPath(truck, rb, f, t))
            {
                float kinetic = 0.5f * rb.mass * speed * speed;
                float spent = t.ramJoulesPerHp * applied;
                float keep = kinetic > 0f ? Mathf.Sqrt(Mathf.Max(0f, 1f - spent / kinetic)) : 0f;
                rb.linearVelocity = vel * keep;
            }
            LastSweepMs = (Time.realtimeSinceStartup - started) * 1000f;
        }

        static Frame MakeFrame(Transform root, Rigidbody rb, Bounds hull, Vector3 vel, float speed, TruckTuning t, float dt)
        {
            var f = new Frame();
            Vector3 scale = root.lossyScale;
            f.rotation = rb.rotation;
            f.centre = rb.position + f.rotation * Vector3.Scale(hull.center, scale);
            f.half = new Vector3(Mathf.Abs(hull.extents.x * scale.x), Mathf.Abs(hull.extents.y * scale.y), Mathf.Abs(hull.extents.z * scale.z));
            f.speed = speed;
            f.dir = vel / speed;
            f.distance = speed * dt * 1.5f + 2f * t.ramSweepExtra;

            // The face it travels toward: front going forward, back reversing.
            Vector3 local = Quaternion.Inverse(f.rotation) * f.dir;
            float sign = local.z >= 0f ? 1f : -1f;
            f.face = f.rotation * new Vector3(0f, 0f, sign);
            // The slab lies on that face, inside the hull as deep as the cast starts behind it,
            // and 5 cm out.
            float depth = Mathf.Max(t.ramFrontSlab, t.ramSweepExtra) + 0.05f;
            f.slabHalf = new Vector3(f.half.x, f.half.y, depth * 0.5f);
            f.slabCentre = f.centre + f.face * (f.half.z - depth * 0.5f + 0.05f);
            return f;
        }

        static Bounds SweptBounds(in Frame f)
        {
            // The hull's world AABB, stretched along the sweep.
            Vector3 right = f.rotation * new Vector3(f.half.x, 0f, 0f);
            Vector3 up = f.rotation * new Vector3(0f, f.half.y, 0f);
            Vector3 fwd = f.rotation * new Vector3(0f, 0f, f.half.z);
            Vector3 ext = new Vector3(
                Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(fwd.x),
                Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(fwd.y),
                Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(fwd.z));
            var b = new Bounds(f.centre, ext * 2f);
            b.Encapsulate(new Bounds(f.centre + f.dir * f.distance, ext * 2f));
            return b;
        }

        // Fills 'targets' with what the cast and the slab found, one entry per receiver.
        void Gather(TruckVehicle truck, Rigidbody rb, in Frame f, TruckTuning t)
        {
            targets.Clear();
            reachedKeys.Clear();
            reachedColliders.Clear();
            Vector3 vel = f.dir * f.speed;
            Vector3 start = f.centre - f.dir * t.ramSweepExtra;
            int n = Physics.BoxCastNonAlloc(start, f.half, f.dir, castHits, f.rotation, f.distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = castHits[i];
                // Overlapping at the start (along the sides too): the slab below decides those.
                if (h.distance <= 0f || h.collider == null) continue;
                Consider(truck, rb, h.collider, h.point, Mathf.Max(0f, Vector3.Dot(vel - BodyVelocity(h.collider), -h.normal)), t);
            }
            int k = Physics.OverlapBoxNonAlloc(f.slabCentre, f.slabHalf, overlaps, f.rotation, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < k; i++)
            {
                var c = overlaps[i];
                if (c == null) continue;
                Vector3 point = c.ClosestPointOnBounds(f.slabCentre);
                Consider(truck, rb, c, point, Mathf.Max(0f, Vector3.Dot(vel - BodyVelocity(c), f.face)), t);
            }
        }

        static Vector3 BodyVelocity(Collider c)
        {
            var b = c.attachedRigidbody;
            return b != null && !b.isKinematic ? b.linearVelocity : Vector3.zero;
        }

        void Consider(TruckVehicle truck, Rigidbody rb, Collider c, Vector3 point, float normalSpeed, TruckTuning t)
        {
            var target = Resolve(truck, rb, c, t);
            if (target.kind == Kind.None) return;
            target.collider = c;
            target.point = point;
            target.normalSpeed = normalSpeed;

            // One entry per receiver (a wall hit on four chunks is one spread hit), the fastest
            // approach wins; every collider it was reached through is marked if the hit hurts.
            Component key = Key(target);
            if (target.kind == Kind.Built)
            {
                reachedKeys.Add(key);
                reachedColliders.Add(c);
            }
            for (int i = 0; i < targets.Count; i++)
            {
                if (Key(targets[i]) != key) continue;
                if (target.normalSpeed > targets[i].normalSpeed) targets[i] = target;
                return;
            }
            targets.Add(target);
        }

        static Component Key(in Target target) => target.kind == Kind.Fling ? target.body : target.receiver;

        // Every collider the sweep reached this receiver through: its own callbacks skip them.
        void MarkReached(Component key)
        {
            for (int i = 0; i < reachedKeys.Count; i++)
                if (reachedKeys[i] == key) MarkHandled(reachedColliders[i], BuiltHandledSeconds);
        }

        Target Resolve(TruckVehicle truck, Rigidbody rb, Collider c, TruckTuning t)
        {
            var none = new Target { kind = Kind.None };
            if (c is WheelCollider || c is CharacterController) return none;
            Rigidbody body = c.attachedRigidbody;
            if (body == rb || c.transform.IsChildOf(truck.transform)) return none;   // itself, its ramp, whoever sits in it

            MovableObject mo = null;
            if (body != null && body.TryGetComponent(out mo) && mo.holder != null) return none;   // in someone's hands: never flung

            if (body != null && !body.isKinematic)
            {
                var cargo = truck.cargo;
                if (cargo != null && ((mo != null && cargo.inside.Contains(mo)) || cargo.ContainsPoint(body.worldCenterOfMass))) return none;
                if (body.mass >= t.flingMassKg || Handled(c)) return none;   // heavy: physics; flung already
                body.TryGetComponent(out Breakable prop);
                return new Target { kind = Kind.Fling, body = body, receiver = prop };
            }

            // Anchored: a wall chunk, an intact wall, a pane, or an anchored Breakable (fence,
            // gate, post, hedge, yard prop, pillar).
            if (DestructibleModule.TryGetChunk(c, out DestructibleChunk chunk))
                return chunk.Attached && chunk.Owner != null && !chunk.Owner.IsGone
                    ? new Target { kind = Kind.Built, receiver = chunk.Owner } : none;
            var module = c.GetComponentInParent<DestructibleModule>();
            if (module != null) return module.IsGone || !module.enabled ? none : new Target { kind = Kind.Built, receiver = module };
            var pane = c.GetComponentInParent<GlassPane>();
            if (pane != null) return pane.IsGone ? none : new Target { kind = Kind.Built, receiver = pane };
            var piece = c.GetComponentInParent<Breakable>();
            if (piece != null) return piece.IsGone ? none : new Target { kind = Kind.Built, receiver = piece };
            return none;
        }

        // A Vehicle hit on a built piece. Returns the HP it really removed.
        float HitBuilt(TruckVehicle truck, in Target target, in Frame f, float strikerMass)
        {
            var d = target.receiver as IDamageable;
            if (d == null || d.IsGone || !(target.normalSpeed > 0f)) return 0f;
            var table = DestructionMaterialTable.Current;
            float receiverMass = target.receiver is GlassPane ? PaneMass(d.WorldBounds) : table.structureReferenceMass;
            var input = new ImpactDamage.ImpactInput
            {
                normalSpeed = target.normalSpeed,
                strikerMass = strikerMass,
                receiverMass = receiverMass,
                receiverAnchored = true,
                material = d.Material,
                type = DamageType.Vehicle,
            };
            var o = ImpactDamage.Evaluate(input);
            if (!o.Hurts) return 0f;
            var e = new DamageEvent(target.point, f.dir, o.damage, o.push, o.spread, DamageType.Vehicle,
                                    truck.DriverActor, truck.gameObject, target.normalSpeed);
            MarkReached(target.receiver);
            return Mathf.Max(0f, d.ApplyDamage(e).applied);
        }

        // As GlassPane measures its own: 10 kg per square metre of its largest face.
        static float PaneMass(Bounds b)
        {
            Vector3 s = b.size;
            float face = Mathf.Max(s.x * s.y, Mathf.Max(s.y * s.z, s.x * s.z));
            return Mathf.Clamp(face * 10f, 0.3f, 30f);
        }

        // A loose body in the lane: its share of the hit, then out of the way, ahead, to the side
        // it was hit on and a little up.
        void Fling(TruckVehicle truck, in Target target, in Frame f, float strikerMass, TruckTuning t)
        {
            var body = target.body;
            if (body == null) return;
            int driver = truck.DriverActor;
            if (target.receiver is Breakable prop && !prop.IsGone && target.normalSpeed > 0f)
            {
                var input = new ImpactDamage.ImpactInput
                {
                    normalSpeed = target.normalSpeed,
                    strikerMass = strikerMass,
                    receiverMass = body.mass,
                    receiverAnchored = false,
                    material = prop.Material,
                    type = DamageType.Vehicle,
                };
                var o = ImpactDamage.Evaluate(input);
                if (o.Hurts)
                    prop.ApplyDamage(new DamageEvent(target.point, f.dir, o.damage, o.push, o.spread, DamageType.Vehicle,
                                                     driver, truck.gameObject, target.normalSpeed));
            }
            if (body == null || !body.gameObject.activeInHierarchy || body.isKinematic) return;   // shattered by the hit

            Vector3 vel = f.dir * f.speed;
            Vector3 side = f.rotation * Vector3.right;
            side.y = 0f;
            float along = Vector3.Dot(body.worldCenterOfMass - f.centre, side);
            Vector3 lateral = side.normalized * (along >= 0f ? 1f : -1f);
            Vector3 fling = vel * 1.1f + lateral * (t.flingLateralShare * f.speed) + Vector3.up * (t.flingUpShare * f.speed);
            if (fling.magnitude > t.flingMaxSpeed) fling = fling.normalized * t.flingMaxSpeed;
            body.WakeUp();
            body.linearVelocity = fling;
            body.angularVelocity += UnityEngine.Random.insideUnitSphere * 4f;

            // What it breaks on landing is the driver's doing (ImpactDamage reads a throw).
            if (Actors.IsPlayer(driver) && body.TryGetComponent(out MovableObject mo))
            {
                mo.lastThrownBy = driver;
                mo.lastThrownTime = Time.time;
            }

            body.GetComponentsInChildren(false, bodyColliders);
            for (int i = 0; i < bodyColliders.Count; i++)
                if (bodyColliders[i] != null && bodyColliders[i].attachedRigidbody == body) MarkHandled(bodyColliders[i], t.flingHandledSeconds);
            bodyColliders.Clear();
        }

        // After the hits: is anything built still in the way?
        bool BuiltInPath(TruckVehicle truck, Rigidbody rb, in Frame f, TruckTuning t)
        {
            Gather(truck, rb, f, t);
            for (int i = 0; i < targets.Count; i++)
                if (targets[i].kind == Kind.Built) return true;
            return false;
        }

        void ForgetCooldowns(float now)
        {
            staleCooldowns.Clear();
            foreach (var kv in cooldownUntil)
                if (kv.Key == null || kv.Value <= now) staleCooldowns.Add(kv.Key);
            for (int i = 0; i < staleCooldowns.Count; i++) cooldownUntil.Remove(staleCooldowns[i]);
            staleCooldowns.Clear();
        }
    }
}
