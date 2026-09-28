using System.Collections.Generic;
using Movers.AudioSynth;
using UnityEngine;

namespace Movers
{
    // A police car of the flee (ADR-013): scene-placed, parked at the PoliceSpawn and active at
    // load, so NetIds registers its body and the transform stream carries it online. The host
    // drives it (EscapeMission dispatches it and names its target); lights and siren are derived
    // locally on both machines.
    //
    // Driving (host, FixedUpdate): a force-steered dynamic body, no WheelColliders, no NavMesh.
    // It heads for a point (a look-ahead point on the MissionRoute, or the target itself off
    // the route), turns at most maxYawRate (less at speed, within lateralGrip), and its flat
    // velocity is pushed toward that heading within policeAccel, policeBrake and lateralGrip.
    // Its colliders have no friction: the drive is the grip. Gravity and the colliders give the
    // height, an upright torque keeps it on its wheels.
    //
    //   no target       drive to the parking distance EscapeMission gave it and stop (the arrival)
    //   the truck, slow stop just behind it, on its side of the road: that is an interception
    //   the truck, fast pass it on the free side at chase speed, and once roadblockLead ahead of
    //                   it stop across the lane (a roadblock) until the truck is past
    //   a member        drive at them and stop close: the officers step out (IsStopped)
    //   off the route   a target more than 12 m from the route: straight at it within
    //                   offRouteChaseRange, else to the route point nearest to it
    // Stunned by a hard hit from the crew truck, it coasts. Tipped over or stuck, it is put back
    // on the route behind where it was (NetTransforms.Snap, so the client does not slide it).
    //
    // Lights: two unlit additive blocks on the roof alternating red and blue, each with a flare
    // billboard. No Light component: in Built-in forward a pixel light adds a pass to every
    // renderer in range, next to a house already at 25k draw calls.
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(9100)]   // Start after RoadAnchor (9000): the parked pose is the anchored one
    public sealed class PoliceCar : MonoBehaviour
    {
        static readonly List<PoliceCar> all = new List<PoliceCar>();

        [Header("Driving")]
        [Tooltip("Metres right of the route's centre line the car keeps to.")]
        public float lane = 1.6f;
        [Tooltip("Degrees per second the heading turns at most (a turn on the spot, slow).")]
        public float maxYawRate = 90f;
        [Tooltip("m/s2 of sideways grip: the yaw rate at speed is held under it.")]
        public float lateralGrip = 12f;
        public float uprightStrength = 25f;
        public float uprightDamping = 5f;

        [Header("Lights and siren")]
        public float flashHz = 2f;
        public float sirenVolume = 0.9f;

        const float OffRouteDistance = 12f;   // a target this far from the route is off it
        const float StopGap = 2f;             // metres left between the car and what it stops at
        const float RecoverTiltDegrees = 60f, RecoverTiltSeconds = 2f;
        const float RecoverStuckSpeed = 1f, RecoverStuckSeconds = 4f;
        const float StoppedSpeed = 0.5f;
        const float NudgeSeconds = 2f;
        const float StunCoastDecel = 3f;
        const float BlockingMass = 40f;       // kg: a body lighter than this does not block the lane
        const float LatePaceMargin = 1.1f;    // the pace to the house is aimed 10 % over the bare need (bends)

        Rigidbody body;
        Vector3 localCenter, localHalf;
        float pivotAboveBottom;

        // host
        MissionRoute route;
        bool dispatched;
        float parkS, parkLateral;
        TruckVehicle targetTruck;
        CrewMember targetMember;
        float progress = -1f, targetHint = -1f, travelDir = 1f;
        float stunnedUntil = -1f, stoppedFor, stuckFor, tiltFor;
        bool roadblock;
        float roadblockS, roadblockLateral, roadblockYaw;
        float nudgeUntil = -1f, nudgeLateral;
        bool ignoreTruck, grounded;
        Vector3 groundNormal = Vector3.up;

        // client
        Vector3 parkedPosition, lastPosition;
        bool replicaDispatched;
        float replicaSpeed;

        // lights and sound
        Renderer red, blue, redFlare, blueFlare;
        bool lightsOn;
        int siren = -1;

        static readonly RaycastHit[] hits = new RaycastHit[16];
        static Material barMaterial, flareMaterial;
        static PhysicsMaterial noFriction;
        static MaterialPropertyBlock block;
        static readonly int TintId = Shader.PropertyToID("_TintColor");
        static readonly Color RedTint = new Color(1f, 0.08f, 0.05f, 0.6f), BlueTint = new Color(0.1f, 0.3f, 1f, 0.6f);
        static readonly Color RedFlareTint = new Color(1f, 0.1f, 0.05f, 0.35f), BlueFlareTint = new Color(0.15f, 0.35f, 1f, 0.35f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        // The enabled cars of the game scene.
        public static IReadOnlyList<PoliceCar> All => all;

        // Lights and siren on (both machines). Client: the car has left its parked pose since the call.
        public bool IsDispatched => Net.HasAuthority ? dispatched : replicaDispatched;
        // Officer zone active (host truth: stopped for officerZoneAfterStop and not stunned;
        // client: speed under 0.5 m/s).
        public bool IsStopped => Net.HasAuthority
            ? dispatched && !IsStunned && stoppedFor >= Numbers.officerZoneAfterStop
            : replicaDispatched && replicaSpeed < StoppedSpeed;
        public bool IsStunned => Time.time < stunnedUntil;
        // Flat speed, m/s (client: estimated from the replicated poses).
        public float Speed
        {
            get
            {
                if (!Net.HasAuthority) return replicaSpeed;
                Vector3 v = Body.linearVelocity;
                v.y = 0f;
                return v.magnitude;
            }
        }
        public float HalfLength => Mathf.Abs(localHalf.z * transform.lossyScale.z);
        public Vector3 Center => transform.TransformPoint(localCenter);

        public Rigidbody Body
        {
            get
            {
                if (body == null) body = GetComponent<Rigidbody>();
                return body;
            }
        }

        static GameLoopNumbers Numbers => GameSession.Current != null ? GameSession.Current.Numbers : GameLoopNumbers.Defaults;

        void Awake()
        {
            MeasureShape();
            BuildLights();
        }

        void Start()
        {
            parkedPosition = lastPosition = transform.position;
            if (!Net.HasAuthority || Body.isKinematic) return;
            // Low, so a ram spins it rather than rolls it.
            Body.centerOfMass = localCenter - Vector3.up * (localHalf.y * 0.5f);
            var mat = NoFriction;
            foreach (var c in GetComponentsInChildren<Collider>())
                if (c != null && !c.isTrigger) c.sharedMaterial = mat;
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
            StopSiren();
        }

        // ---- what EscapeMission calls (host) ----

        // Lights and siren on, then off to parkS (distance along the route) at parkLateral.
        internal void Dispatch(MissionRoute route, float parkS, float parkLateral)
        {
            if (!Net.HasAuthority || route == null) return;
            this.route = route;
            this.parkS = parkS;
            this.parkLateral = parkLateral;
            dispatched = true;
            progress = route.ProgressOf(transform.position, -1f);
        }

        internal void Chase(TruckVehicle truck)
        {
            if (targetTruck == truck && targetMember == null) return;
            targetTruck = truck;
            targetMember = null;
            targetHint = -1f;
            roadblock = false;
        }

        internal void Chase(CrewMember member)
        {
            if (targetMember == member && targetTruck == null) return;
            targetMember = member;
            targetTruck = null;
            targetHint = -1f;
            roadblock = false;
        }

        internal void ClearTarget()
        {
            targetTruck = null;
            targetMember = null;
            roadblock = false;
        }

        internal bool Targets(TruckVehicle truck) => truck != null && targetTruck == truck;
        internal bool Targets(CrewMember member) => member != null && targetMember == member;

        // Metres from a point to this car's box (0 inside).
        public float DistanceTo(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point) - localCenter;
            Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -localHalf.x, localHalf.x),
                                          Mathf.Clamp(local.y, -localHalf.y, localHalf.y),
                                          Mathf.Clamp(local.z, -localHalf.z, localHalf.z));
            return Vector3.Distance(point, transform.TransformPoint(localCenter + clamped));
        }

        // ---- driving (host) ----

        void FixedUpdate()
        {
            if (!Net.HasAuthority) return;
            var rb = Body;
            if (rb == null || rb.isKinematic) return;
            var n = Numbers;
            float dt = Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;
            grounded = ProbeGround();

            if (!dispatched || route == null || Session.IsOver)
            {
                if (!rb.IsSleeping()) { Brake(rb, flat, n.policeBrake * dt); Upright(rb); }
                stoppedFor = 0f;
                return;
            }
            if (IsStunned)
            {
                Brake(rb, flat, StunCoastDecel * dt);
                Upright(rb);
                stoppedFor = stuckFor = 0f;
                return;
            }

            progress = route.ProgressOf(rb.position, progress);
            ignoreTruck = false;
            Plan(n, speed, out Vector3 aim, out float topSpeed, out float stopDistance, out bool holdYaw, out float yaw);
            stopDistance = Mathf.Min(stopDistance, Blocked(flat, speed, n));
            float target = Drive(rb, n, flat, speed, aim, topSpeed, stopDistance, holdYaw, yaw, dt);
            Upright(rb);
            Watch(rb, target, speed, dt);
        }

        // Where to head, how fast at most, and how far the stop is.
        void Plan(GameLoopNumbers n, float speed, out Vector3 aim, out float topSpeed, out float stopDistance,
                  out bool holdYaw, out float yaw)
        {
            holdYaw = false;
            yaw = 0f;
            topSpeed = n.policeChaseKmh / 3.6f;
            if (targetTruck != null && targetTruck.isActiveAndEnabled) { PlanTruck(n, speed, out aim, out stopDistance, out holdYaw, out yaw); return; }
            if (targetMember != null && targetMember.isActiveAndEnabled) { PlanMember(n, speed, out aim, out stopDistance); return; }

            // To the parking distance, at the pace that makes the arrival time: cruise when on
            // time, faster (up to chase speed) when bends and climbs made it late, and chase
            // speed once the police are due. The final stop is counted in the distance.
            float remaining = Mathf.Abs(parkS - progress);
            var s = GameSession.Current;
            float cruise = n.policeCruiseKmh / 3.6f;
            if (s != null && s.PoliceIn > 0.5f)
            {
                float stopRun = cruise * cruise / (2f * Mathf.Max(0.1f, n.policeBrake));
                float needed = (remaining + stopRun) / s.PoliceIn;
                topSpeed = Mathf.Clamp(needed * LatePaceMargin, cruise, topSpeed);
            }
            else if (s == null) topSpeed = cruise;
            aim = OnRoute(parkS, parkLateral, speed, out stopDistance);
        }

        void PlanTruck(GameLoopNumbers n, float speed, out Vector3 aim, out float stopDistance, out bool holdYaw, out float yaw)
        {
            holdYaw = false;
            yaw = 0f;
            var tb = targetTruck.Body;
            Vector3 tp = tb != null ? tb.worldCenterOfMass : targetTruck.transform.position;
            float ts = route.ProgressOf(tp, targetHint, out float tLat);
            targetHint = ts;
            float truckHalf = targetTruck.Hull.extents.z;
            float gap = truckHalf + HalfLength + StopGap;
            if (Mathf.Abs(tLat) > OffRouteDistance) { roadblock = false; aim = OffRoute(n, tp, ts, gap, speed, out stopDistance); return; }

            Vector3 tv = targetTruck.Velocity;
            tv.y = 0f;
            bool fast = tv.magnitude * 3.6f > n.interceptTruckMaxKmh;
            if (!fast)
            {
                // Pull up behind it (on its side of the car): the interception.
                roadblock = false;
                float side = ts >= progress ? 1f : -1f;
                aim = OnRoute(ts - side * gap, tLat, speed, out stopDistance);
                return;
            }

            ignoreTruck = true;
            float truckDir = Vector3.Dot(tv, route.Forward(ts)) >= 0f ? 1f : -1f;
            float ahead = (progress - ts) * truckDir;   // > 0: the car is ahead of the truck
            if (roadblock)
            {
                if (ahead < -1f) roadblock = false;     // it got past: chase again
                else
                {
                    aim = OnRoute(roadblockS, roadblockLateral, speed, out stopDistance);
                    holdYaw = true;
                    yaw = roadblockYaw;
                    return;
                }
            }
            float aimS = ts + truckDir * n.roadblockLead;
            if (ahead >= n.roadblockLead * 0.8f && Mathf.Abs(progress - aimS) < 6f)
            {
                // Far enough ahead: stop across its lane.
                roadblock = true;
                roadblockS = progress;
                roadblockLateral = tLat;
                Vector3 f = route.Forward(progress) * truckDir;
                roadblockYaw = Quaternion.LookRotation(f).eulerAngles.y + n.roadblockYaw * (tLat >= 0f ? 1f : -1f);
                aim = OnRoute(roadblockS, roadblockLateral, speed, out stopDistance);
                holdYaw = true;
                yaw = roadblockYaw;
                return;
            }
            // Behind it: pass on the free side. Ahead of it: take its lane.
            float free = tLat >= 0f ? -1f : 1f;
            float lat = ahead < 0f ? tLat + free * (n.overtakeOffset + 0.5f) : tLat;
            aim = OnRoute(aimS, lat, speed, out stopDistance);
            // A moving aim: arrive at the truck's pace, not stopped.
            if (ahead < 0f) stopDistance = float.PositiveInfinity;
        }

        void PlanMember(GameLoopNumbers n, float speed, out Vector3 aim, out float stopDistance)
        {
            Vector3 mp = targetMember.transform.position;
            float ms = route.ProgressOf(mp, targetHint, out float mLat);
            targetHint = ms;
            float gap = HalfLength + StopGap;
            if (Mathf.Abs(mLat) > OffRouteDistance) { aim = OffRoute(n, mp, ms, gap, speed, out stopDistance); return; }
            float side = ms >= progress ? 1f : -1f;
            aim = OnRoute(ms - side * gap, mLat, speed, out stopDistance);
        }

        // A target off the route: straight at it when close enough, else to the route point
        // nearest to it (the officers wait there).
        Vector3 OffRoute(GameLoopNumbers n, Vector3 target, float targetS, float gap, float speed, out float stopDistance)
        {
            Vector3 d = target - Body.position;
            d.y = 0f;
            if (d.magnitude <= n.offRouteChaseRange)
            {
                stopDistance = d.magnitude - gap;
                return target;
            }
            return OnRoute(targetS, lane * travelDir, speed, out stopDistance);
        }

        // A look-ahead point on the route toward distance aimS, at lateral metres (plus the
        // sideways nudge round a body in the lane). stopDistance: how far aimS is.
        Vector3 OnRoute(float aimS, float lateral, float speed, out float stopDistance)
        {
            float delta = aimS - progress;
            stopDistance = Mathf.Abs(delta);
            if (stopDistance > 0.5f) travelDir = Mathf.Sign(delta);
            if (Time.time < nudgeUntil) lateral += nudgeLateral;
            float look = Mathf.Min(stopDistance, 8f + 0.4f * speed);
            route.Sample(progress + Mathf.Sign(delta) * look, lateral, out Vector3 p, out _);
            // Almost there: aim past the stop point, so the heading stays along the road.
            if (look < 3f) route.Sample(aimS + travelDir * 3f, lateral, out p, out _);
            return p;
        }

        // Metres free in the lane ahead (a body there: stop short of it and move over).
        float Blocked(Vector3 flat, float speed, GameLoopNumbers n)
        {
            Vector3 dir = speed > 1f ? flat / speed : Flat(transform.forward);
            if (dir.sqrMagnitude < 1e-4f) return float.PositiveInfinity;
            Vector3 half = new Vector3(Mathf.Abs(localHalf.x * transform.lossyScale.x) * 0.9f,
                                       Mathf.Abs(localHalf.y * transform.lossyScale.y) * 0.4f, 0.3f);
            float reach = HalfLength + Mathf.Max(3f, speed * 0.8f);
            int count = Physics.BoxCastNonAlloc(Center, half, dir, hits, Quaternion.LookRotation(dir), reach,
                                                QueryMask, QueryTriggerInteraction.Ignore);
            float free = float.PositiveInfinity;
            Rigidbody blocker = null;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                var other = h.rigidbody;
                // Small props are driven through (the bumper throws them), like debris.
                if (other == null || other.isKinematic || h.distance <= 0f || other.mass < BlockingMass) continue;
                if (other.transform.IsChildOf(transform)) continue;
                bool isTruck = targetTruck != null && other == targetTruck.Body;
                if (isTruck && ignoreTruck) continue;
                float d = h.distance - HalfLength;
                if (d < free) { free = d; blocker = other; }
            }
            if (blocker == null) return float.PositiveInfinity;
            // Something in the lane that is not the truck it is pulling up behind: go round it.
            if (targetTruck == null || blocker != targetTruck.Body)
            {
                nudgeUntil = Time.time + NudgeSeconds;
                nudgeLateral = n.overtakeOffset * (travelDir >= 0f ? -1f : 1f);
            }
            return Mathf.Max(0f, free - StopGap);
        }

        // Pushes the flat velocity toward the heading; returns the speed it aimed at.
        float Drive(Rigidbody rb, GameLoopNumbers n, Vector3 flat, float speed, Vector3 aim, float topSpeed,
                    float stopDistance, bool holdYaw, float yaw, float dt)
        {
            Vector3 fwd = Flat(transform.forward);
            if (fwd.sqrMagnitude < 1e-4f) fwd = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            Vector3 to = aim - rb.position;
            to.y = 0f;
            Vector3 want = to.sqrMagnitude > 0.04f ? to.normalized : fwd;

            float target = Mathf.Min(topSpeed, Mathf.Sqrt(2f * n.policeBrake * Mathf.Max(0f, stopDistance)));
            if (holdYaw && stopDistance < 2f)
            {
                want = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                target = 0f;
            }
            float angle = Vector3.SignedAngle(fwd, want, Vector3.up);
            // A sharp turn is taken slowly; a turn round, almost on the spot.
            float abs = Mathf.Abs(angle);
            if (abs > 15f) target = Mathf.Min(target, Mathf.Lerp(topSpeed, 6f, (abs - 15f) / 45f));
            if (!grounded) return target;

            float yawCap = Mathf.Min(maxYawRate, Mathf.Rad2Deg * lateralGrip / Mathf.Max(speed, 1f));
            float turn = Mathf.Clamp(angle, -yawCap * dt, yawCap * dt);
            Vector3 heading = Quaternion.AngleAxis(turn, Vector3.up) * fwd;

            Vector3 dv = heading * target - flat;
            float along = Vector3.Dot(dv, heading);
            Vector3 side = dv - heading * along;
            along = Mathf.Clamp(along, -n.policeBrake * dt, n.policeAccel * dt);
            side = Vector3.ClampMagnitude(side, lateralGrip * dt);
            rb.AddForce(heading * along + side, ForceMode.VelocityChange);

            Vector3 av = rb.angularVelocity;
            rb.angularVelocity = av - Vector3.up * Vector3.Dot(av, Vector3.up) + Vector3.up * (turn / dt * Mathf.Deg2Rad);
            return target;
        }

        void Brake(Rigidbody rb, Vector3 flat, float maxChange)
        {
            if (!grounded || flat.sqrMagnitude < 1e-6f) return;
            rb.AddForce(-Vector3.ClampMagnitude(flat, maxChange), ForceMode.VelocityChange);
        }

        // Back on its wheels, against the ground under it.
        void Upright(Rigidbody rb)
        {
            Vector3 up = grounded ? groundNormal : Vector3.up;
            Vector3 axis = Vector3.Cross(transform.up, up);
            Vector3 av = rb.angularVelocity;
            Vector3 tilt = av - Vector3.up * Vector3.Dot(av, Vector3.up);
            rb.AddTorque(axis * uprightStrength - tilt * uprightDamping, ForceMode.Acceleration);
        }

        bool ProbeGround()
        {
            float reach = Mathf.Abs(localHalf.y * transform.lossyScale.y) + 0.6f;
            int count = Physics.RaycastNonAlloc(Center, Vector3.down, hits, reach, QueryMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; groundNormal = hits[i].normal; }
            }
            if (best == float.MaxValue) groundNormal = Vector3.up;
            return best < float.MaxValue;
        }

        // Stopped (the officer zone), stuck or tipped over (put back on the route).
        void Watch(Rigidbody rb, float target, float speed, float dt)
        {
            stoppedFor = speed < StoppedSpeed ? stoppedFor + dt : 0f;
            stuckFor = target > RecoverStuckSpeed * 1.5f && speed < RecoverStuckSpeed ? stuckFor + dt : 0f;
            tiltFor = Vector3.Angle(transform.up, Vector3.up) > RecoverTiltDegrees ? tiltFor + dt : 0f;
            if (stuckFor >= RecoverStuckSeconds || tiltFor >= RecoverTiltSeconds || rb.position.y < -20f) Recover(rb);
        }

        void Recover(Rigidbody rb)
        {
            stuckFor = tiltFor = 0f;
            roadblock = false;
            float s = progress - travelDir * 6f;
            route.Sample(s, lane * travelDir, out Vector3 p, out Vector3 f);
            p.y = RoadAnchor.GroundHeight(p.x, p.z, transform, rb.position.y) + pivotAboveBottom + 0.1f;
            Quaternion rot = Quaternion.LookRotation(f * travelDir, Vector3.up);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = p;
            rb.rotation = rot;
            transform.SetPositionAndRotation(p, rot);
            progress = s;
            NetTransforms.Snap(gameObject);
        }

        // A hard hit from the crew truck stuns the officers (the lights stay on).
        void OnCollisionEnter(Collision c)
        {
            if (!Net.HasAuthority || !dispatched || c.rigidbody == null) return;
            if (!c.rigidbody.TryGetComponent(out TruckVehicle _)) return;
            var n = Numbers;
            if (c.impulse.magnitude >= n.policeStunImpulse) stunnedUntil = Time.time + n.policeStunSeconds;
        }

        // ---- both machines: the client's view, lights and siren ----

        void Update()
        {
            if (!Net.HasAuthority)
            {
                float dt = Time.deltaTime;
                Vector3 p = transform.position;
                if (dt > 1e-5f)
                {
                    Vector3 d = p - lastPosition;
                    d.y = 0f;
                    replicaSpeed = Mathf.Lerp(replicaSpeed, d.magnitude / dt, 0.2f);
                }
                lastPosition = p;
                if (!replicaDispatched && Session.Phase != MissionPhase.Job && (p - parkedPosition).sqrMagnitude > 1f)
                    replicaDispatched = true;
            }
            UpdateLights();
            UpdateSiren();
        }

        void UpdateLights()
        {
            bool on = IsDispatched;
            lightsOn = on;
            bool redPhase = Mathf.Repeat(Time.time * flashHz, 1f) < 0.5f;
            SetOn(red, on && redPhase);
            SetOn(redFlare, on && redPhase);
            SetOn(blue, on && !redPhase);
            SetOn(blueFlare, on && !redPhase);
        }

        static void SetOn(Renderer r, bool on)
        {
            if (r != null && r.enabled != on) r.enabled = on;
        }

        void UpdateSiren()
        {
            bool want = lightsOn && !Session.IsOver;
            if (want && siren < 0)
                siren = AudioDirector.StartLoop(SfxKind.SirenLoop, transform, Vector3.up * 1.5f, SoundPreset.Engine, sirenVolume, 1f, 0.5f);
            else if (!want && siren >= 0) StopSiren();
        }

        void StopSiren()
        {
            if (siren < 0) return;
            AudioDirector.Stop(siren, 1f);
            siren = -1;
        }

        // ---- building ----

        // The car's box in its own space: its BoxCollider, else what its renderers cover.
        void MeasureShape()
        {
            if (TryGetComponent(out BoxCollider box))
            {
                localCenter = box.center;
                localHalf = box.size * 0.5f;
            }
            else
            {
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    var b = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1));
                        Vector3 l = transform.InverseTransformPoint(corner);
                        min = Vector3.Min(min, l);
                        max = Vector3.Max(max, l);
                    }
                }
                if (float.IsInfinity(min.x)) { min = new Vector3(-0.9f, 0f, -2.2f); max = new Vector3(0.9f, 1.5f, 2.2f); }
                localCenter = (min + max) * 0.5f;
                localHalf = (max - min) * 0.5f;
            }
            pivotAboveBottom = (transform.position - transform.TransformPoint(localCenter - Vector3.up * localHalf.y)).y;
        }

        void BuildLights()
        {
            if (!Application.isPlaying) return;
            float top = localCenter.y + localHalf.y;
            float w = Mathf.Max(0.6f, localHalf.x * 2f);
            Vector3 blockSize = new Vector3(w * 0.26f, 0.12f, 0.24f);
            float x = w * 0.16f;
            red = Part("PoliceLight_Red", PrimitiveType.Cube, new Vector3(localCenter.x - x, top + 0.07f, localCenter.z), blockSize, BarMaterial, RedTint);
            blue = Part("PoliceLight_Blue", PrimitiveType.Cube, new Vector3(localCenter.x + x, top + 0.07f, localCenter.z), blockSize, BarMaterial, BlueTint);
            Vector3 flareSize = new Vector3(1.4f, 1.4f, 1f);
            redFlare = Part("PoliceFlare_Red", PrimitiveType.Quad, new Vector3(localCenter.x - x, top + 0.12f, localCenter.z), flareSize, FlareMaterial, RedFlareTint);
            blueFlare = Part("PoliceFlare_Blue", PrimitiveType.Quad, new Vector3(localCenter.x + x, top + 0.12f, localCenter.z), flareSize, FlareMaterial, BlueFlareTint);
            if (redFlare != null) redFlare.gameObject.AddComponent<PoliceFlare>();
            if (blueFlare != null) blueFlare.gameObject.AddComponent<PoliceFlare>();
            UpdateLights();
        }

        // A light part: a builtin mesh with no collider, never cast or received shadows, and
        // flags that keep it out of the network id sweep (a runtime child, like the debris pool).
        Renderer Part(string partName, PrimitiveType shape, Vector3 localPosition, Vector3 size, Material mat, Color tint)
        {
            if (mat == null) return null;
            var go = new GameObject(partName) { hideFlags = HideFlags.DontSaveInEditor };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            Vector3 s = transform.lossyScale;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(1e-3f, Mathf.Abs(s.x)),
                                                  size.y / Mathf.Max(1e-3f, Mathf.Abs(s.y)),
                                                  size.z / Mathf.Max(1e-3f, Mathf.Abs(s.z)));
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(shape == PrimitiveType.Quad ? "Quad.fbx" : "Cube.fbx");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(TintId, tint);
            r.SetPropertyBlock(block);
            r.enabled = false;
            return r;
        }

        static Material BarMaterial
        {
            get
            {
                if (barMaterial == null) barMaterial = Additive("MAT_PoliceLight", SmokeTextures.Flat);
                return barMaterial;
            }
        }

        static Material FlareMaterial
        {
            get
            {
                if (flareMaterial == null) flareMaterial = Additive("MAT_PoliceFlare", SmokeTextures.Puff);
                return flareMaterial;
            }
        }

        // Unlit and additive, as ExplosionFX's sparks: it reads through the fog and needs no light.
        static Material Additive(string matName, Texture tex)
        {
            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            return new Material(sh) { name = matName, mainTexture = tex, hideFlags = HideFlags.HideAndDontSave };
        }

        static PhysicsMaterial NoFriction
        {
            get
            {
                if (noFriction == null)
                    noFriction = new PhysicsMaterial("PoliceCar_NoFriction")
                    {
                        dynamicFriction = 0f,
                        staticFriction = 0f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                return noFriction;
            }
        }

        // Everything but debris (a chunk on the road is driven through) and the triggers' layer.
        static int QueryMask => DestructionLayers.QueryMask & ~(1 << 2);

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }
    }

    // Turns a flare quad to face whichever camera draws it (each split-screen view gets its own).
    internal sealed class PoliceFlare : MonoBehaviour
    {
        void OnWillRenderObject()
        {
            var cam = Camera.current;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
